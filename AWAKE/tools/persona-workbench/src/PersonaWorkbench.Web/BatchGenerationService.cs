using System.Collections.Concurrent;
using System.Text;
using PersonaWorkbench.Core;

namespace PersonaWorkbench.Web;

public static class BatchGenerationStatuses
{
    public const string Invalid = "invalid";
    public const string Busy = "busy";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Partial = "partial";
    public const string Cancelled = "cancelled";
}

public sealed class BatchGenerationItem
{
    public string ItemId { get; set; } = string.Empty;
    public int SourceOrdinal { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SourceText { get; set; } = string.Empty;
}

public sealed class BatchGenerationRequest
{
    public string BatchId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ProviderProtocol { get; set; } = ProviderProtocolKind.Ollama;
    public List<BatchGenerationItem> Items { get; set; } = new List<BatchGenerationItem>();
}

public sealed class BatchGenerationResult
{
    public string ItemId { get; init; } = string.Empty;
    public int SourceOrdinal { get; init; }
    public string SourceFile { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string SourceText { get; init; } = string.Empty;
    public bool Success { get; init; }
    public PersonaDocument? Draft { get; init; }
    public string Dsl { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public ProviderUsage? Usage { get; init; }
}

public sealed class BatchGenerationResponse
{
    public string Status { get; init; } = BatchGenerationStatuses.Invalid;
    public string BatchId { get; init; } = string.Empty;
    public int Total { get; init; }
    public int Completed { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public string CancelReason { get; init; } = string.Empty;
    public string CurrentItemId { get; init; } = string.Empty;
    public int CurrentSourceOrdinal { get; init; }
    public string CurrentTitle { get; init; } = string.Empty;
    public DateTimeOffset UpdatedUtc { get; init; }
    public List<BatchGenerationResult> Results { get; init; } = new List<BatchGenerationResult>();
    public List<string> Errors { get; init; } = new List<string>();
}

public sealed class BatchCancelRequest
{
    public string BatchId { get; set; } = string.Empty;
}

public sealed class BatchCancelResponse
{
    public bool IsAccepted { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
}

public sealed class BatchGenerationService
{
    private const int MaximumItems = 32;
    private const int MaximumItemBytes = 24 * 1024;
    private const int MaximumTotalBytes = 256 * 1024;
    private const int MaximumRetainedBatches = 64;
    private static readonly TimeSpan CompletedBatchRetention = TimeSpan.FromMinutes(10);
    private readonly ProviderDraftActionService _providerActions;
    private readonly ConcurrentDictionary<string, BatchState> _batches = new ConcurrentDictionary<string, BatchState>(StringComparer.Ordinal);

    public BatchGenerationService(ProviderDraftActionService providerActions)
    {
        _providerActions = providerActions ?? throw new ArgumentNullException(nameof(providerActions));
    }

    public async Task<BatchGenerationResponse> GenerateAsync(BatchGenerationRequest? request, CancellationToken requestCancellationToken = default)
    {
        BatchGenerationResponse? validationFailure = Validate(request);
        if (validationFailure != null) return validationFailure;
        string batchId = request!.BatchId.Trim();
        PruneCompletedBatches();
        CancellationTokenSource batchCancellation = CancellationTokenSource.CreateLinkedTokenSource(requestCancellationToken);
        BatchState state = new BatchState(batchId, request.Items.Count, batchCancellation);
        if (!_batches.TryAdd(batchId, state))
        {
            batchCancellation.Dispose();
            return new BatchGenerationResponse
            {
                Status = BatchGenerationStatuses.Busy,
                BatchId = batchId,
                Total = request.Items.Count,
                Errors = new List<string> { "batch.request_in_flight" }
            };
        }

        int processedResultCount = 0;
        bool providerCancelled = false;
        try
        {
            foreach (BatchGenerationItem item in request.Items)
            {
                if (batchCancellation.IsCancellationRequested) break;
                state.SetCurrent(item);
                ProviderDslConversionActionResponse response;
                try
                {
                    response = await _providerActions.ConvertToDslAsync(new ProviderDslConversionActionRequest
                    {
                        Endpoint = request.Endpoint,
                        Model = request.Model,
                        ProviderProtocol = request.ProviderProtocol,
                        SourceText = item.SourceText,
                        LocalId = string.Empty,
                        LocalDisplayName = item.Title
                    }, batchCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (batchCancellation.IsCancellationRequested)
                {
                    response = new ProviderDslConversionActionResponse { ErrorCode = "provider.cancelled" };
                }

                BatchGenerationResult result = new BatchGenerationResult
                {
                    ItemId = item.ItemId,
                    SourceOrdinal = item.SourceOrdinal,
                    SourceFile = item.SourceFile,
                    Title = item.Title,
                    SourceText = item.SourceText,
                    Success = response.Status == ProviderDraftActionStatus.Success && response.Draft != null && !string.IsNullOrWhiteSpace(response.Dsl),
                    Draft = response.Draft,
                    Dsl = response.Dsl,
                    ErrorCode = response.Status == ProviderDraftActionStatus.Success && response.Draft != null && !string.IsNullOrWhiteSpace(response.Dsl)
                        ? string.Empty
                        : string.IsNullOrWhiteSpace(response.ErrorCode) ? "batch.result_missing" : response.ErrorCode,
                    Usage = response.Usage
                };
                processedResultCount++;
                state.AddProcessedResult(result, processedResultCount);
                providerCancelled = string.Equals(response.ErrorCode, "provider.cancelled", StringComparison.Ordinal);
                if (batchCancellation.IsCancellationRequested || providerCancelled) break;
            }

            bool cancelled = batchCancellation.IsCancellationRequested || providerCancelled;
            for (int index = processedResultCount; index < request.Items.Count; index++)
            {
                BatchGenerationItem item = request.Items[index];
                state.AddNotStartedResult(new BatchGenerationResult
                {
                    ItemId = item.ItemId,
                    SourceOrdinal = item.SourceOrdinal,
                    SourceFile = item.SourceFile,
                    Title = item.Title,
                    SourceText = item.SourceText,
                    Success = false,
                    ErrorCode = cancelled ? "batch.not_started_after_cancel" : "batch.not_started"
                });
            }
            return state.Complete(cancelled);
        }
        catch (Exception)
        {
            bool cancelled = batchCancellation.IsCancellationRequested;
            for (int index = processedResultCount; index < request.Items.Count; index++)
            {
                BatchGenerationItem item = request.Items[index];
                state.AddNotStartedResult(new BatchGenerationResult
                {
                    ItemId = item.ItemId,
                    SourceOrdinal = item.SourceOrdinal,
                    SourceFile = item.SourceFile,
                    Title = item.Title,
                    SourceText = item.SourceText,
                    Success = false,
                    ErrorCode = cancelled ? "batch.not_started_after_cancel" : "batch.internal_error"
                });
            }
            state.AddError("batch.internal_error");
            return state.Complete(cancelled);
        }
        finally
        {
            PruneCompletedBatches();
        }
    }

    public BatchGenerationResponse? GetProgress(string? batchId)
    {
        string normalized = (batchId ?? string.Empty).Trim();
        if (normalized.Length == 0) return null;
        PruneCompletedBatches();
        if (!_batches.TryGetValue(normalized, out BatchState? state)) return null;
        return state.Snapshot();
    }

    public BatchCancelResponse Cancel(string? batchId)
    {
        string normalized = (batchId ?? string.Empty).Trim();
        if (normalized.Length == 0) return new BatchCancelResponse { ErrorCode = "batch.id_required" };
        if (!_batches.TryGetValue(normalized, out BatchState? state)) return new BatchCancelResponse { ErrorCode = "batch.not_found" };
        lock (state.Gate)
        {
            if (!state.IsActive) return new BatchCancelResponse { ErrorCode = "batch.not_active" };
            state.Cancellation.Cancel();
        }
        return new BatchCancelResponse { IsAccepted = true };
    }

    private static BatchGenerationResponse? Validate(BatchGenerationRequest? request)
    {
        if (request == null) return Invalid("", "batch.request_invalid");
        string batchId = request.BatchId.Trim();
        if (batchId.Length < 8 || batchId.Length > 80 || batchId.Any(value => !(char.IsLetterOrDigit(value) || value is '-' or '_' or '.'))) return Invalid(batchId, "batch.id_invalid");
        if (!ProviderEndpointPolicy.TryValidate(request.Endpoint, out _, out string endpointError)) return Invalid(batchId, string.IsNullOrWhiteSpace(endpointError) ? "provider.endpoint_invalid" : endpointError);
        if (string.IsNullOrWhiteSpace(request.Model)) return Invalid(batchId, "provider.model_required");
        if (!ProviderProtocolKind.TryNormalize(request.ProviderProtocol, out _)) return Invalid(batchId, "provider.protocol_invalid");
        if (request.Items == null || request.Items.Count == 0 || request.Items.Count > MaximumItems) return Invalid(batchId, "batch.item_count_invalid");

        HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<int> sourceOrdinals = new HashSet<int>();
        int totalBytes = 0;
        foreach (BatchGenerationItem? item in request.Items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || !itemIds.Add(item.ItemId.Trim())) return Invalid(batchId, "batch.item_id_invalid");
            if (!sourceOrdinals.Add(item.SourceOrdinal)) return Invalid(batchId, "batch.source_ordinal_invalid");
            int itemBytes = Encoding.UTF8.GetByteCount(item.SourceText ?? string.Empty);
            totalBytes += itemBytes;
            if (item.SourceOrdinal <= 0 || string.IsNullOrWhiteSpace(item.SourceText) || itemBytes > MaximumItemBytes) return Invalid(batchId, "batch.item_source_invalid");
            if (Encoding.UTF8.GetByteCount(item.Title ?? string.Empty) > 512 || Encoding.UTF8.GetByteCount(item.SourceFile ?? string.Empty) > 1024) return Invalid(batchId, "batch.item_metadata_invalid");
            if (totalBytes > MaximumTotalBytes) return Invalid(batchId, "batch.input_size_invalid");
        }
        return null;
    }

    private static BatchGenerationResponse Invalid(string batchId, string errorCode)
    {
        return new BatchGenerationResponse
        {
            Status = BatchGenerationStatuses.Invalid,
            BatchId = batchId,
            Errors = new List<string> { errorCode }
        };
    }

    private void PruneCompletedBatches()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (KeyValuePair<string, BatchState> pair in _batches)
        {
            if (!pair.Value.ShouldPrune(now, CompletedBatchRetention)) continue;
            if (_batches.TryRemove(new KeyValuePair<string, BatchState>(pair.Key, pair.Value))) pair.Value.Dispose();
        }

        if (_batches.Count <= MaximumRetainedBatches) return;
        List<KeyValuePair<string, BatchState>> completed = _batches
            .Where(pair => pair.Value.IsCompleted())
            .OrderBy(pair => pair.Value.GetUpdatedUtc())
            .Take(Math.Max(0, _batches.Count - MaximumRetainedBatches))
            .ToList();
        foreach (KeyValuePair<string, BatchState> pair in completed)
        {
            if (_batches.TryRemove(new KeyValuePair<string, BatchState>(pair.Key, pair.Value))) pair.Value.Dispose();
        }
    }

    private sealed class BatchState
    {
        public BatchState(string batchId, int total, CancellationTokenSource cancellation)
        {
            BatchId = batchId;
            Total = total;
            Cancellation = cancellation;
            UpdatedUtc = DateTimeOffset.UtcNow;
        }

        public object Gate { get; } = new object();
        public CancellationTokenSource Cancellation { get; }
        public string BatchId { get; }
        public int Total { get; }
        public bool IsActive { get; private set; } = true;
        public string Status { get; private set; } = BatchGenerationStatuses.Running;
        public int Completed { get; private set; }
        public int Succeeded { get; private set; }
        public int Failed { get; private set; }
        public string CancelReason { get; private set; } = string.Empty;
        public string CurrentItemId { get; private set; } = string.Empty;
        public int CurrentSourceOrdinal { get; private set; }
        public string CurrentTitle { get; private set; } = string.Empty;
        public DateTimeOffset UpdatedUtc { get; private set; }
        private List<BatchGenerationResult> Results { get; } = new List<BatchGenerationResult>();
        private List<string> Errors { get; } = new List<string>();

        public void SetCurrent(BatchGenerationItem item)
        {
            lock (Gate)
            {
                CurrentItemId = item.ItemId;
                CurrentSourceOrdinal = item.SourceOrdinal;
                CurrentTitle = item.Title;
                UpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public void AddProcessedResult(BatchGenerationResult result, int completed)
        {
            lock (Gate)
            {
                AddResultLocked(result);
                Completed = completed;
                UpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public void AddNotStartedResult(BatchGenerationResult result)
        {
            lock (Gate)
            {
                AddResultLocked(result);
                UpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public void AddError(string errorCode)
        {
            lock (Gate)
            {
                Errors.Add(errorCode);
                UpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public BatchGenerationResponse Complete(bool cancelled)
        {
            lock (Gate)
            {
                Status = cancelled
                    ? BatchGenerationStatuses.Cancelled
                    : Failed == 0 ? BatchGenerationStatuses.Completed : BatchGenerationStatuses.Partial;
                CancelReason = cancelled ? "client_cancelled" : string.Empty;
                CurrentItemId = string.Empty;
                CurrentSourceOrdinal = 0;
                CurrentTitle = string.Empty;
                IsActive = false;
                UpdatedUtc = DateTimeOffset.UtcNow;
                return SnapshotLocked();
            }
        }

        public BatchGenerationResponse Snapshot()
        {
            lock (Gate) return SnapshotLocked();
        }

        public bool ShouldPrune(DateTimeOffset now, TimeSpan retention)
        {
            lock (Gate) return !IsActive && now - UpdatedUtc >= retention;
        }

        public bool IsCompleted()
        {
            lock (Gate) return !IsActive;
        }

        public DateTimeOffset GetUpdatedUtc()
        {
            lock (Gate) return UpdatedUtc;
        }

        public void Dispose()
        {
            Cancellation.Dispose();
        }

        private void AddResultLocked(BatchGenerationResult result)
        {
            Results.Add(result);
            if (result.Success) Succeeded++;
            else Failed++;
        }

        private BatchGenerationResponse SnapshotLocked()
        {
            return new BatchGenerationResponse
            {
                Status = Status,
                BatchId = BatchId,
                Total = Total,
                Completed = Completed,
                Succeeded = Succeeded,
                Failed = Failed,
                CancelReason = CancelReason,
                CurrentItemId = CurrentItemId,
                CurrentSourceOrdinal = CurrentSourceOrdinal,
                CurrentTitle = CurrentTitle,
                UpdatedUtc = UpdatedUtc,
                Results = new List<BatchGenerationResult>(Results),
                Errors = new List<string>(Errors)
            };
        }
    }
}
