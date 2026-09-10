using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchStartRequest(
    string BatchId,
    int ExpectedRevision,
    string ConsentToken,
    int ClaimGeneration,
    int ConsentRevision,
    string Stage,
    IReadOnlyList<string> TargetItemIds);

internal sealed record BatchStartResult(JsonObject PublicManifest, IReadOnlyList<JsonObject> Items);

internal sealed class BatchExecutionService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> BatchLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly BatchWorkflowService _workflow;
    private readonly BatchWorkspaceLayout _layout;
    private readonly BatchAuthoringContractRegistry _contract;
    private readonly BatchConsentService _consents;
    private readonly BatchFactReviewRepository _facts;
    private readonly BatchMetadataRepository _metadata;
    private readonly BatchCacheRepository _cache;
    private readonly ProviderConfiguration _configuration;
    private readonly Func<BatchStartRequest, JsonObject, CancellationToken, Task>? _targetExecutor;
    private readonly string _ownerId;
    private readonly string _ownerInstanceId;
    private readonly string _workspaceMarkerHash;

    public BatchExecutionService(
        WorkspaceService workspace,
        BatchAuthoringContractRegistry contract,
        BatchConsentService consents,
        string ownerId,
        string ownerInstanceId,
        string workspaceMarkerHash,
        ProviderConfiguration? configuration = null,
        Func<BatchStartRequest, JsonObject, CancellationToken, Task>? targetExecutor = null)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _contract = contract;
        _consents = consents;
        _ownerId = BatchPathValidator.RequireIdentifier(ownerId, "owner_id");
        _ownerInstanceId = BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        RequireHash(workspaceMarkerHash, "workspace_marker_hash");
        _workspaceMarkerHash = workspaceMarkerHash.ToUpperInvariant();
        _facts = new BatchFactReviewRepository(workspace, contract, ownerId, ownerInstanceId, workspaceMarkerHash);
        _metadata = new BatchMetadataRepository(workspace, contract, ownerId, ownerInstanceId, workspaceMarkerHash);
        _cache = new BatchCacheRepository(workspace, contract);
        _workflow = new BatchWorkflowService(workspace, contract, ownerId, ownerInstanceId, workspaceMarkerHash);
        _configuration = configuration ?? ProviderConfiguration.FromEnvironment();
        _targetExecutor = targetExecutor;
    }

    public async Task<BatchStartResult> StartAsync(BatchStartRequest request, CancellationToken cancellationToken = default, DateTimeOffset? now = null)
    {
        ValidateRequest(request);
        var gate = BatchLocks.GetOrAdd(_layout.BatchRoot(request.BatchId), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        BatchProviderService provider;
        JsonObject providerSelection;
        JsonObject[] items;
        JsonObject manifest;
        DateTimeOffset timestamp;
        try
        {
            using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(request.BatchId));
            manifest = BatchWorkspaceLayout.ReadJson(_layout.BatchManifest(request.BatchId));
            var revision = manifest["revision"]?.GetValue<int>() ?? -1;
            if (revision != request.ExpectedRevision) throw new InvalidOperationException("WB-BATCH-REVISION-409: 批次已更新，请刷新后重试。");
            var claimGeneration = manifest["claim_generation"]?.GetValue<int>() ?? -1;
            if (claimGeneration != request.ClaimGeneration) throw new InvalidOperationException("WB-BATCH-CLAIM-409: claim_generation 不匹配。");
            var consent = _consents.ValidateForStart(request.BatchId, request.ConsentToken, request.Stage, request.TargetItemIds, request.ConsentRevision, request.ClaimGeneration, now);
            items = request.TargetItemIds.Select(itemId => ReadItem(request.BatchId, itemId)).ToArray();
            ValidateEligibility(items, request.Stage);
            timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var operationId = NewId("start");
            var scheduled = items.Select(item => ScheduleItem(item, request.Stage, timestamp)).ToArray();
            var journal = new JsonObject
            {
                ["schema_version"] = "awake.worldbook.batch-start-journal.v2",
                ["operation_id"] = operationId,
                ["batch_id"] = request.BatchId,
                ["stage"] = request.Stage,
                ["target_item_ids"] = new JsonArray(request.TargetItemIds.Select(value => JsonValue.Create(value)).ToArray()),
                ["expected_manifest_revision"] = request.ExpectedRevision,
                ["consent_id"] = consent["consent_id"]!.DeepClone(),
                ["consent_revision"] = request.ConsentRevision,
                ["claim_generation"] = request.ClaimGeneration,
                ["state"] = "prepared",
                ["created_at"] = timestamp.ToString("O"),
                ["updated_at"] = timestamp.ToString("O")
            };
            var journalPath = _layout.BatchJournal(request.BatchId, operationId);
            BatchWorkspaceLayout.WriteJsonAtomic(journalPath, journal);
            foreach (var item in scheduled) BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchItem(request.BatchId, item["item_id"]!.GetValue<string>()), item);
            journal["state"] = "committed";
            journal["updated_at"] = timestamp.ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(journalPath, journal);
            _consents.Consume(request.BatchId, request.ConsentToken, request.Stage, request.TargetItemIds, request.ConsentRevision, request.ClaimGeneration, timestamp);
            manifest["status"] = "running";
            manifest["revision"] = request.ExpectedRevision + 1;
            manifest["commit_seq"] = manifest["commit_seq"]!.GetValue<int>() + 1;
            manifest["updated_at"] = timestamp.ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchManifest(request.BatchId), manifest);

            provider = new BatchProviderService(_configuration, _cache, _facts);
            providerSelection = manifest["provider_selection"]!.AsObject();
            items = request.TargetItemIds.Select(itemId => ReadItem(request.BatchId, itemId)).ToArray();
        }
        finally
        {
            gate.Release();
        }

        var providerId = providerSelection["provider_id"]!.GetValue<string>();
        var modelParameters = providerSelection["model_parameters"]!.AsObject();
        var providerFingerprint = providerSelection["provider_fingerprint"]!.GetValue<string>();
        await RunTargetsAsync(request, items, provider, providerId, modelParameters, providerFingerprint, timestamp, cancellationToken).ConfigureAwait(false);

        await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(request.BatchId));
            manifest = BatchWorkspaceLayout.ReadJson(_layout.BatchManifest(request.BatchId));
            var allItemIds = manifest["item_ids"]?.AsArray().Select(value => value!.GetValue<string>()).ToArray() ?? [];
            var itemStates = allItemIds.Select(itemId => ReadItem(request.BatchId, itemId)["status"]?.GetValue<string>() ?? string.Empty).ToArray();
            if (manifest["status"]?.GetValue<string>() != "cancelled")
            {
                manifest["status"] = itemStates.Any(state => state is "failed" or "unknown_result")
                    ? "failed"
                    : itemStates.All(state => state is "created" or "skipped")
                        ? "completed"
                        : "running";
            }
            manifest["revision"] = manifest["revision"]!.GetValue<int>() + 1;
            manifest["commit_seq"] = manifest["commit_seq"]!.GetValue<int>() + 1;
            manifest["updated_at"] = DateTimeOffset.UtcNow.ToUniversalTime().ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchManifest(request.BatchId), manifest);
            var publicItems = request.TargetItemIds.Select(itemId => _workflow.GetPublicItem(request.BatchId, itemId)).ToArray();
            return new BatchStartResult(BatchPublicProjection.ProjectManifest(manifest), publicItems);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RunTargetsAsync(BatchStartRequest request, IReadOnlyList<JsonObject> initialItems, BatchProviderService provider, string providerId, JsonObject modelParameters, string providerFingerprint, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        for (var index = 0; index < initialItems.Count; index++)
        {
            var initialItem = initialItems[index];
            var itemId = initialItem["item_id"]!.GetValue<string>();
            if (cancellationToken.IsCancellationRequested)
            {
                MarkNotStarted(request.BatchId, itemId, request.Stage, DateTimeOffset.UtcNow);
                for (var remaining = index + 1; remaining < initialItems.Count; remaining++)
                    MarkNotStarted(request.BatchId, initialItems[remaining]["item_id"]!.GetValue<string>(), request.Stage, DateTimeOffset.UtcNow);
                break;
            }
            var current = ReadItem(request.BatchId, itemId);
            try
            {
                if (_targetExecutor is not null)
                {
                    await _targetExecutor(request, current, cancellationToken).ConfigureAwait(false);
                }
                else if (request.Stage == "facts")
                {
                    var sourceUnit = ReadSourceUnit(request.BatchId, current["source_unit_id"]!.GetValue<string>());
                    await provider.ExtractFactsAsync(request.BatchId, itemId, current["revision"]!.GetValue<int>(), providerId, modelParameters, providerFingerprint, current, sourceUnit, _workspaceMarkerHash, cancellationToken, timestamp).ConfigureAwait(false);
                }
                else
                {
                    await RunMetadataAsync(request.BatchId, current, providerId, modelParameters, providerFingerprint, cancellationToken, timestamp).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException error)
            {
                var cancellationTimestamp = DateTimeOffset.UtcNow;
                MarkFailure(request.BatchId, itemId, request.Stage, providerId, providerFingerprint, BatchFailureDetails.FromCancellation(request.Stage, error.Message), cancellationTimestamp);
                for (var remaining = index + 1; remaining < initialItems.Count; remaining++)
                    MarkNotStarted(request.BatchId, initialItems[remaining]["item_id"]!.GetValue<string>(), request.Stage, cancellationTimestamp);
                break;
            }
            catch (Exception ex)
            {
                MarkFailure(request.BatchId, itemId, request.Stage, providerId, providerFingerprint, BatchFailureDetails.From(request.Stage, ex.Message), DateTimeOffset.UtcNow);
            }
        }
    }

    private async Task RunMetadataAsync(string batchId, JsonObject item, string providerId, JsonObject modelParameters, string providerFingerprint, CancellationToken cancellationToken, DateTimeOffset timestamp)
    {
        var result = BatchWorkspaceLayout.ReadJson(_layout.BatchItemResult(batchId, item["item_id"]!.GetValue<string>()));
        var facts = result["facts"]!.AsArray();
        var acceptedIds = item["accepted_fact_ids"]?.AsArray()?.Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
        var acceptedHash = BatchFactReviewRepository.ComputeAcceptedFactSetHash(result, acceptedIds);
        if (item["accepted_fact_set_hash"]?.GetValue<string>() != acceptedHash)
            throw new InvalidOperationException("WB-BATCH-FACT-SET-409: 已接受事实集发生变化，请重新审核事实。");
        var acceptedFacts = facts.Where(x => x is not null && acceptedIds.Contains(x["fact_id"]!.GetValue<string>())).Select(x => new AuthoringDraftFact(
            x!["fact_id"]!.GetValue<string>(),
            x["kind"]!.GetValue<string>(),
            x["text"]!.GetValue<string>(),
            x["certainty"]!.GetValue<string>(),
            false,
            null,
            "accepted")).ToArray();
        if (acceptedFacts.Length == 0) throw new InvalidOperationException("WB-BATCH-FACT-SET-409: 当前项目没有可用于元数据的已接受事实。");
        var sourceUnit = ReadSourceUnit(batchId, item["source_unit_id"]!.GetValue<string>());
        var request = AuthoringDraftRequestFactory.Create(
            providerId,
            "batch." + item["item_id"]!.GetValue<string>(),
            AuthoringDraftStage.Metadata,
            item["source_unit_id"]!.GetValue<string>(),
            "reference_material",
            sourceUnit["text"]!.GetValue<string>(),
            acceptedFacts,
            null,
            [],
            new JsonObject());
        var snapshot = AuthoringLifecycleFactory.CreateSnapshot(
            item["source_snapshot_id"]?.GetValue<string>() ?? request.DraftId,
            request.SourceName,
            request.SourceNature,
            request.SourceText);
        var packet = AuthoringLifecycleFactory.CreatePacket(
            snapshot,
            request.DraftId,
            providerId,
            providerFingerprint,
            ["metadata"],
            [],
            modelParameters,
            registrySnapshotHash: item["registry_snapshot_hash"]?.GetValue<string>());
        var provider = AuthoringDraftProviderFactory.Create(providerId, _configuration.WithModelParameters(modelParameters));
        var generated = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        var metadata = generated.Metadata ?? throw new InvalidOperationException("WB-BATCH-METADATA-422: Provider 未返回元数据候选。");
        var attemptId = item["lease"]?["attempt_id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("WB-BATCH-CLAIM-409: 当前项目缺少执行租约。");
        _metadata.Commit(new BatchMetadataCommitRequest(
            batchId,
            item["item_id"]!.GetValue<string>(),
            item["revision"]!.GetValue<int>(),
            attemptId,
            providerId,
            providerFingerprint,
            acceptedHash,
            metadata.Title,
            metadata.Summary,
            metadata.Domain,
            metadata.Subdomain,
            PacketHash: packet.PacketHash,
            Packet: AuthoringLifecycleFactory.ProjectPacket(packet)),
            timestamp);
    }

    private void MarkFailure(string batchId, string itemId, string stage, string providerId, string providerFingerprint, BatchFailureDetails failure, DateTimeOffset timestamp)
    {
        using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(batchId));
        var path = _layout.BatchItem(batchId, itemId);
        var item = BatchWorkspaceLayout.ReadJson(path);
        if (item["active_attempt_stage"]?.GetValue<string>() != stage
            || item["status"]?.GetValue<string>() is not ("extracting" or "metadata_running"))
            return;
        JsonObject? attempt = null;
        try
        {
            attempt = BatchAttemptResultFactory.CreateFailure(batchId, itemId, item, providerId, providerFingerprint, stage, failure, timestamp);
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchItemAttempt(batchId, itemId, attempt["attempt_id"]!.GetValue<string>()), attempt);
        }
        catch (InvalidOperationException)
        {
            item["recovery_status"] = "needs_reconcile";
        }
        item["status"] = failure.ErrorClass == "cancelled" || failure.Outcome == "unknown_result" ? "unknown_result" : "failed";
        if (item["recovery_status"]?.GetValue<string>() != "needs_reconcile") item["recovery_status"] = "clean";
        item["status_reason"] = failure.StatusReason;
        item["active_attempt_stage"] = "none";
        item["last_attempt_stage"] = stage;
        item["last_error_code"] = failure.Code;
        item["last_error_message"] = failure.Message;
        item["last_error"] = failure.Message;
        item["attempt_count"] = Math.Min(3, (item["attempt_count"]?.GetValue<int>() ?? 0) + 1);
        item.Remove("lease");
        item["revision"] = item["revision"]!.GetValue<int>() + 1;
        item["updated_at"] = timestamp.ToString("O");
        if (attempt is not null) item["correlation_id"] = attempt["correlation_id"]!.DeepClone();
        if (attempt is not null) _contract.ValidatePayload("attempt", attempt);
        _contract.ValidatePayload("item", item);
        BatchWorkspaceLayout.WriteJsonAtomic(path, item);
    }

    private void MarkNotStarted(string batchId, string itemId, string stage, DateTimeOffset timestamp)
    {
        using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(batchId));
        var path = _layout.BatchItem(batchId, itemId);
        if (!File.Exists(path)) return;
        var item = BatchWorkspaceLayout.ReadJson(path);
        if (item["active_attempt_stage"]?.GetValue<string>() != stage) return;
        item["status"] = "skipped";
        item["recovery_status"] = "clean";
        item["status_reason"] = "cancelled";
        item["active_attempt_stage"] = "none";
        item.Remove("lease");
        item["revision"] = (item["revision"]?.GetValue<int>() ?? 0) + 1;
        item["updated_at"] = timestamp.ToUniversalTime().ToString("O");
        _contract.ValidatePayload("item", item);
        BatchWorkspaceLayout.WriteJsonAtomic(path, item);
    }

    private JsonObject ReadItem(string batchId, string itemId)
        => BatchWorkspaceLayout.ReadJson(_layout.BatchItem(batchId, itemId));

    private JsonObject ReadSourceUnit(string batchId, string sourceUnitId)
    {
        var path = Directory.EnumerateFiles(Path.Combine(_layout.BatchRoot(batchId), "sources"), "*.json", SearchOption.AllDirectories)
            .FirstOrDefault(candidate => candidate.Contains(".units", StringComparison.Ordinal)
                && BatchWorkspaceLayout.ReadJson(candidate)["unit_id"]?.GetValue<string>() == sourceUnitId);
        return path is null ? throw new InvalidOperationException("WB-BATCH-SOURCE-404: 找不到批次 source unit。") : BatchWorkspaceLayout.ReadJson(path);
    }

    private JsonObject ScheduleItem(JsonObject item, string stage, DateTimeOffset timestamp)
    {
        var result = (JsonObject)item.DeepClone();
        result["status"] = stage == "metadata" ? "metadata_running" : "extracting";
        result["status_reason"] = "provider_started";
        result["active_attempt_stage"] = stage;
        result["revision"] = item["revision"]!.GetValue<int>() + 1;
        result["updated_at"] = timestamp.ToString("O");
        result["lease"] = BatchLeaseGuard.Create(item["item_id"]!.GetValue<string>(), _ownerInstanceId, GetClaimGeneration(item), timestamp);
        return result;
    }

    private int GetClaimGeneration(JsonObject item)
    {
        var manifest = BatchWorkspaceLayout.ReadJson(_layout.BatchManifest(item["batch_id"]!.GetValue<string>()));
        return manifest["claim_generation"]?.GetValue<int>() ?? throw new InvalidOperationException("WB-BATCH-CLAIM-409: 批次缺少 claim_generation。");
    }

    private void ValidateEligibility(IReadOnlyList<JsonObject> items, string stage)
    {
        foreach (var item in items)
        {
            var eligible = stage == "metadata"
                ? item["status"]?.GetValue<string>() == "metadata_pending"
                    && item["review_status"]?.GetValue<string>() == "accepted"
                    && item["result_stage"]?.GetValue<string>() == "facts"
                    && item["active_attempt_stage"]?.GetValue<string>() == "none"
                : item["status"]?.GetValue<string>() == "queued"
                    || item["status"]?.GetValue<string>() == "failed"
                        && item["last_attempt_stage"]?.GetValue<string>() == "facts"
                        && (item["attempt_count"]?.GetValue<int>() ?? 0) < 3;
            if (!eligible) throw new InvalidOperationException("WB-BATCH-STAGE-409: start 目标中存在不满足当前阶段的项目。");
        }
    }

    private static void ValidateRequest(BatchStartRequest request)
    {
        BatchPathValidator.RequireIdentifier(request.BatchId, "batch_id");
        if (request.Stage is not ("facts" or "metadata")) throw new InvalidOperationException("WB-BATCH-STAGE-422: stage 无效。");
        if (request.ExpectedRevision < 0 || request.ConsentRevision < 0 || request.ClaimGeneration < 0) throw new InvalidOperationException("WB-BATCH-REVISION-422: start revision 无效。");
        if (request.TargetItemIds is null || request.TargetItemIds.Count == 0 || request.TargetItemIds.Distinct(StringComparer.Ordinal).Count() != request.TargetItemIds.Count) throw new InvalidOperationException("WB-BATCH-REQUEST-422: target_item_ids 无效。");
        foreach (var itemId in request.TargetItemIds) BatchPathValidator.RequireIdentifier(itemId, "item_id");
    }

    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
    private static void RequireHash(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(x => !Uri.IsHexDigit(x))) throw new InvalidOperationException($"WB-BATCH-REQUEST-422: {field} 必须是 SHA-256。");
    }
}
