using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchMetadataCommitRequest(
    string BatchId,
    string ItemId,
    int ExpectedItemRevision,
    string AttemptId,
    string ProviderId,
    string ProviderFingerprint,
    string AcceptedFactSetHash,
    string? Title,
    string? Summary,
    string? Domain,
    string? Subdomain,
    int LogicalAttemptNumber = 1,
    int LatencyMs = 0,
    JsonObject? TokenUsage = null,
    string AttemptKind = "provider",
    string? CacheMaterializationRef = null,
    string? PacketHash = null,
    JsonObject? Packet = null);

internal sealed record BatchMetadataCommitResult(JsonObject Item, JsonObject Result, JsonObject Attempt);

internal sealed class BatchMetadataRepository
{
    private static readonly ConcurrentDictionary<string, object> ItemLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] Domains = ["politics", "economy", "culture", "military", "geography"];

    private readonly BatchWorkspaceLayout _layout;
    private readonly BatchAuthoringContractRegistry _contract;
    private readonly string _ownerInstanceId;

    public BatchMetadataRepository(WorkspaceService workspace, BatchAuthoringContractRegistry contract, string ownerId, string ownerInstanceId, string workspaceMarkerHash)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _contract = contract;
        _ownerInstanceId = BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        BatchPathValidator.RequireIdentifier(ownerId, "owner_id");
        RequireHash(workspaceMarkerHash, "workspace_marker_hash");
        _ = _contract.RequireSchema("item");
        _ = _contract.RequireSchema("metadata_result");
        _ = _contract.RequireSchema("metadata_selection");
        _ = _contract.RequireSchema("attempt");
    }

    public BatchMetadataCommitResult Commit(BatchMetadataCommitRequest request, DateTimeOffset? now = null)
    {
        ValidateRequest(request);
        var key = _layout.BatchItem(request.BatchId, request.ItemId);
        var gate = ItemLocks.GetOrAdd(key, _ => new object());
        lock (gate)
        {
            using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(request.BatchId));
            var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var item = BatchWorkspaceLayout.ReadJson(key);
            var revision = item["revision"]?.GetValue<int>() ?? -1;
            if (revision != request.ExpectedItemRevision) throw new InvalidOperationException("WB-BATCH-REVISION-409: item revision 已变化，请刷新后重试。");
            if (item["status"]?.GetValue<string>() != "metadata_running" || item["result_stage"]?.GetValue<string>() != "facts")
                throw new InvalidOperationException("WB-BATCH-STAGE-409: 该项目当前不允许提交元数据结果。");
            var manifest = BatchWorkspaceLayout.ReadJson(_layout.BatchManifest(request.BatchId));
            var lease = BatchLeaseGuard.Require(item, manifest, request.AttemptId, _ownerInstanceId, timestamp);
            var factResultPath = _layout.BatchItemResult(request.BatchId, request.ItemId);
            if (!File.Exists(factResultPath)) throw new InvalidOperationException("WB-BATCH-FACT-SET-409: 当前事实结果不存在，请重新审核事实。");
            var factResult = BatchWorkspaceLayout.ReadJson(factResultPath);
            var acceptedFactIds = item["accepted_fact_ids"]?.AsArray()?.Select(value => value!.GetValue<string>()).ToArray() ?? [];
            var recomputedHash = BatchFactReviewRepository.ComputeAcceptedFactSetHash(factResult, acceptedFactIds);
            if (item["accepted_fact_set_hash"]?.GetValue<string>() != recomputedHash
                || request.AcceptedFactSetHash != recomputedHash)
                throw new InvalidOperationException("WB-BATCH-FACT-SET-409: 已接受事实集发生变化，请重新生成元数据。");
            var domain = request.Domain?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!Domains.Contains(domain, StringComparer.Ordinal)) throw new InvalidOperationException("WB-BATCH-METADATA-422: 主分类无效。");
            var title = RequireText(request.Title, "title", 512);
            var summary = RequireText(request.Summary, "summary", 12000);
            var subdomain = RequireText(request.Subdomain, "subdomain", 128);
            var selection = new JsonObject
            {
                ["title"] = title,
                ["summary"] = summary,
                ["domain"] = domain,
                ["subdomain"] = subdomain,
                ["metadata_selection_hash"] = CanonicalJson.Hash(new JsonObject
                {
                    ["domain"] = domain,
                    ["subdomain"] = subdomain,
                    ["summary"] = summary,
                    ["title"] = title
                })
            };
            var resultId = NewId("result.metadata");
            var result = new JsonObject
            {
                ["schema_version"] = "awake.worldbook.batch-metadata-result.v2",
                ["result_id"] = resultId,
                ["batch_id"] = request.BatchId,
                ["item_id"] = request.ItemId,
                ["attempt_id"] = request.AttemptId,
                ["source_fact_set_hash"] = request.AcceptedFactSetHash,
                ["title"] = title,
                ["summary"] = summary,
                ["domain"] = domain,
                ["subdomain"] = subdomain,
                ["review_only"] = true,
                ["created_at"] = timestamp.ToString("O"),
                ["metadata_selection_hash"] = selection["metadata_selection_hash"]!.DeepClone(),
                ["created_from_item_revision"] = request.ExpectedItemRevision
            };
            var resultHashInput = (JsonObject)result.DeepClone();
            resultHashInput.Remove("result_hash");
            resultHashInput.Remove("created_at");
            result["result_hash"] = CanonicalJson.Hash(resultHashInput);
            var attempt = BuildAttempt(request, item, resultId, lease, timestamp);
            _contract.ValidatePayload("metadata_result", result);
            _contract.ValidatePayload("attempt", attempt);
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchItemMetadata(request.BatchId, request.ItemId), result);
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchItemAttempt(request.BatchId, request.ItemId, request.AttemptId), attempt);
            var nextItem = (JsonObject)item.DeepClone();
            nextItem["status"] = "ready_to_create";
            nextItem["status_reason"] = "metadata_succeeded";
            nextItem["revision"] = request.ExpectedItemRevision + 1;
            nextItem["metadata_result_ref"] = resultId;
            nextItem["metadata_selection"] = selection;
            nextItem["result_stage"] = "metadata";
            nextItem["active_attempt_stage"] = "none";
            nextItem.Remove("lease");
            nextItem["updated_at"] = timestamp.ToString("O");
            _contract.ValidatePayload("item", nextItem);
            BatchWorkspaceLayout.WriteJsonAtomic(key, nextItem);
            return new BatchMetadataCommitResult(nextItem, result, attempt);
        }
    }

    private JsonObject BuildAttempt(BatchMetadataCommitRequest request, JsonObject item, string resultId, BatchLeaseSnapshot lease, DateTimeOffset timestamp)
    {
        var attempt = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-attempt-result.v2",
            ["attempt_id"] = lease.AttemptId,
            ["provider_fingerprint"] = request.ProviderFingerprint,
            ["outcome"] = "succeeded",
            ["error_class"] = "none",
            ["started_at"] = timestamp.ToString("O"),
            ["finished_at"] = timestamp.ToString("O"),
            ["latency_ms"] = request.LatencyMs,
            ["token_usage"] = NormalizeTokenUsage(request.TokenUsage),
            ["correlation_id"] = NewId("correlation"),
            ["logical_attempt_number"] = request.LogicalAttemptNumber,
            ["exchange_count"] = 1,
            ["batch_id"] = request.BatchId,
            ["item_id"] = request.ItemId,
            ["provider_id"] = request.ProviderId,
            ["owner_instance_id"] = lease.OwnerInstanceId,
            ["fence_token"] = lease.FenceToken,
            ["deadline_at"] = lease.DeadlineAt.ToString("O"),
            ["lease"] = BatchLeaseGuard.Clone(lease),
            ["provider_request_id"] = "offline." + lease.AttemptId,
            ["item_revision"] = request.ExpectedItemRevision,
            ["claim_generation"] = lease.ClaimGeneration,
            ["attempt_kind"] = request.AttemptKind,
            ["token_usage_known"] = request.TokenUsage is not null,
            ["stage"] = "metadata",
            ["result_schema_id"] = "awake.worldbook.batch-metadata-result.v2",
            ["result_ref"] = resultId,
            ["source_fact_set_hash"] = request.AcceptedFactSetHash
        };
        if (!string.IsNullOrWhiteSpace(request.PacketHash)) attempt["packet_hash"] = request.PacketHash;
        if (request.Packet is not null) attempt["packet"] = request.Packet.DeepClone();
        if (!string.IsNullOrWhiteSpace(request.CacheMaterializationRef)) attempt["cache_materialization_ref"] = request.CacheMaterializationRef;
        return attempt;
    }

    private static JsonObject NormalizeTokenUsage(JsonObject? tokenUsage)
    {
        if (tokenUsage is null)
            return new JsonObject { ["known"] = false, ["input_tokens"] = null, ["output_tokens"] = null };
        var input = tokenUsage["input_tokens"]?.GetValue<int>() ?? tokenUsage["prompt_tokens"]?.GetValue<int>();
        var output = tokenUsage["output_tokens"]?.GetValue<int>() ?? tokenUsage["completion_tokens"]?.GetValue<int>();
        if (input is null || output is null || input < 0 || output < 0)
            throw new InvalidOperationException("WB-BATCH-ATTEMPT-422: token usage 格式无效。");
        return new JsonObject { ["known"] = true, ["input_tokens"] = input.Value, ["output_tokens"] = output.Value };
    }

    private static void ValidateRequest(BatchMetadataCommitRequest request)
    {
        BatchPathValidator.RequireIdentifier(request.BatchId, "batch_id");
        BatchPathValidator.RequireIdentifier(request.ItemId, "item_id");
        BatchPathValidator.RequireIdentifier(request.AttemptId, "attempt_id");
        RequireHash(request.ProviderFingerprint, "provider_fingerprint");
        RequireHash(request.AcceptedFactSetHash, "accepted_fact_set_hash");
        if (request.ProviderId is not ("local" or "cloud")) throw new InvalidOperationException("WB-BATCH-PROVIDER-422: Provider 类型无效。");
        if (request.AttemptKind is not ("provider" or "cache_hit")) throw new InvalidOperationException("WB-BATCH-ATTEMPT-422: attempt_kind 无效。");
        if (request.AttemptKind == "cache_hit" && string.IsNullOrWhiteSpace(request.CacheMaterializationRef)) throw new InvalidOperationException("WB-BATCH-CACHE-422: cache_hit 缺少 materialization 引用。");
    }

    private static string RequireText(string? value, string field, int maximum)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > maximum) throw new InvalidOperationException($"WB-BATCH-METADATA-422: {field} 不能为空且长度不能超过 {maximum}。");
        return text.Normalize(System.Text.NormalizationForm.FormC);
    }

    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
    private static void RequireHash(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(x => !Uri.IsHexDigit(x))) throw new InvalidOperationException($"WB-BATCH-REQUEST-422: {field} 必须是 SHA-256。");
    }
}
