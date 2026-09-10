using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchControlResult(JsonObject Data);

internal sealed class BatchControlService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly BatchWorkspaceLayout _layout;
    private readonly string _ownerId;
    private readonly string _ownerInstanceId;
    private readonly BatchConsentService _consents;
    private readonly BatchWorkflowService _workflow;
    private readonly BatchAuthoringContractRegistry _contract;

    public BatchControlService(
        WorkspaceService workspace,
        BatchAuthoringContractRegistry contract,
        string ownerId,
        string ownerInstanceId,
        string workspaceMarkerHash)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _ownerId = BatchPathValidator.RequireIdentifier(ownerId, "owner_id");
        _ownerInstanceId = BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        _contract = contract;
        _consents = new BatchConsentService(workspace, contract, ownerId, ownerInstanceId, workspaceMarkerHash);
        _workflow = new BatchWorkflowService(workspace, contract, ownerId, ownerInstanceId, workspaceMarkerHash);
    }

    public BatchControlResult ChangeState(string batchId, int expectedRevision, string state, DateTimeOffset? now = null)
    {
        BatchPathValidator.RequireIdentifier(batchId, "batch_id");
        if (state is not ("paused" or "cancelled")) throw new InvalidOperationException("WB-BATCH-STATE-422: 批次状态无效。");
        return WithLock(batchId, () =>
        {
            using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(batchId));
            var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var manifest = ReadManifest(batchId);
            RequireRevision(manifest, expectedRevision);
            var current = manifest["status"]?.GetValue<string>() ?? string.Empty;
            if (state == "paused" && current is "cancelled" or "completed")
                throw new InvalidOperationException("WB-BATCH-STATE-409: 当前批次不能暂停。");
            if (state == "cancelled" && current == "completed")
                throw new InvalidOperationException("WB-BATCH-STATE-409: 已完成批次不能取消。");
            manifest["status"] = state;
            manifest["status_reason"] = state == "paused" ? "paused_by_editor" : "cancelled_by_editor";
            manifest["revision"] = expectedRevision + 1;
            manifest["commit_seq"] = (manifest["commit_seq"]?.GetValue<int>() ?? 0) + 1;
            manifest["updated_at"] = timestamp.ToString("O");
            if (state == "cancelled") CancelPendingItems(batchId, manifest, timestamp);
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchManifest(batchId), manifest);
            return new BatchControlResult(BatchPublicProjection.ProjectManifest(manifest));
        });
    }

    public BatchControlResult Claim(string batchId, int expectedRevision, string claimReason, DateTimeOffset? now = null)
    {
        BatchPathValidator.RequireIdentifier(batchId, "batch_id");
        if (claimReason is not ("process_restart" or "lease_expired" or "manual_reclaim"))
            throw new InvalidOperationException("WB-BATCH-CLAIM-422: 接管原因无效。");
        return WithLock(batchId, () =>
        {
            using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(batchId));
            var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var manifest = ReadManifest(batchId);
            RequireRevision(manifest, expectedRevision);
            var activeOwner = manifest["active_owner_instance_id"]?.GetValue<string>();
            var leaseExpires = ParseTimestamp(manifest["lease_expires_at"]);
            if (!string.IsNullOrWhiteSpace(activeOwner) && leaseExpires > timestamp)
                throw new InvalidOperationException("WB-BATCH-CLAIM-409: 现有批次租约仍未过期。");
            var generation = (manifest["claim_generation"]?.GetValue<int>() ?? 0) + 1;
            manifest["active_owner_instance_id"] = _ownerInstanceId;
            manifest["claim_generation"] = generation;
            manifest["lease_issued_at"] = timestamp.ToString("O");
            manifest["lease_expires_at"] = timestamp.AddMinutes(10).ToString("O");
            manifest["revision"] = expectedRevision + 1;
            manifest["commit_seq"] = (manifest["commit_seq"]?.GetValue<int>() ?? 0) + 1;
            manifest["updated_at"] = timestamp.ToString("O");
            _consents.RevokeIssued(batchId, generation, timestamp);
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchManifest(batchId), manifest);
            return new BatchControlResult(BatchPublicProjection.ProjectManifest(manifest));
        });
    }

    public BatchControlResult RetryItem(string batchId, string itemId, int expectedItemRevision, bool manualConfirmation, string stage, DateTimeOffset? now = null)
    {
        BatchPathValidator.RequireIdentifier(batchId, "batch_id");
        BatchPathValidator.RequireIdentifier(itemId, "item_id");
        if (!manualConfirmation) throw new InvalidOperationException("WB-BATCH-RETRY-422: 重试必须由编辑者确认。");
        if (stage is not ("facts" or "metadata")) throw new InvalidOperationException("WB-BATCH-RETRY-422: 重试阶段无效。");
        return WithLock(batchId + ":" + itemId, () =>
        {
            using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(batchId));
            var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var path = _layout.BatchItem(batchId, itemId);
            var item = BatchWorkspaceLayout.ReadJson(path);
            var revision = item["revision"]?.GetValue<int>() ?? -1;
            if (revision != expectedItemRevision) throw new InvalidOperationException("WB-BATCH-REVISION-409: item revision 已变化，请刷新后重试。");
            var current = item["status"]?.GetValue<string>() ?? string.Empty;
            var lastStage = item["last_attempt_stage"]?.GetValue<string>();
            var attemptCount = item["attempt_count"]?.GetValue<int>() ?? 0;
            if (attemptCount >= 3)
                throw new InvalidOperationException("WB-BATCH-RETRY-422: 该项目已达到最多 3 次 AI 尝试，不能继续重试。");
            if (current is "extracting" or "metadata_running")
                throw new InvalidOperationException("WB-BATCH-RETRY-409: 该项目仍在处理中，请先刷新状态。");
            if (current is "failed" or "unknown_result")
                if (!string.Equals(lastStage, stage, StringComparison.Ordinal))
                    throw new InvalidOperationException("WB-BATCH-RETRY-422: 重试阶段与上次失败阶段不一致。");
            if (stage == "metadata")
            {
                if (current is not ("failed" or "unknown_result") || lastStage != "metadata" || item["review_status"]?.GetValue<string>() != "accepted")
                    throw new InvalidOperationException("WB-BATCH-RETRY-422: 当前项目不能重试元数据阶段。");
                item["status"] = "metadata_pending";
                item["status_reason"] = "metadata_retry_queued";
                item["result_stage"] = "facts";
            }
            else
            {
                if (current is not ("failed" or "unknown_result" or "queued" or "no_candidate"))
                    throw new InvalidOperationException("WB-BATCH-RETRY-422: 当前项目不能重试事实阶段。");
                item["status"] = "queued";
                item["status_reason"] = "manual_retry";
                item["review_status"] = "unreviewed";
                item.Remove("facts_result_ref");
                item.Remove("accepted_fact_ids");
                item.Remove("accepted_fact_set_hash");
                item.Remove("metadata_result_ref");
                item.Remove("metadata_selection");
                item.Remove("cache_materialization_ref");
                item["result_stage"] = "none";
            }
            item.Remove("lease");
            item["active_attempt_stage"] = "none";
            item.Remove("last_error");
            item.Remove("last_error_code");
            item.Remove("last_error_message");
            item["recovery_status"] = "clean";
            item["correlation_id"] = NewId("correlation");
            item["revision"] = expectedItemRevision + 1;
            item["updated_at"] = timestamp.ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(path, item);
            return new BatchControlResult(_workflow.GetPublicReviewProjection(batchId, itemId));
        });
    }

    private void CancelPendingItems(string batchId, JsonObject manifest, DateTimeOffset timestamp)
    {
        var providerSelection = manifest["provider_selection"]?.AsObject();
        var providerId = providerSelection?["provider_id"]?.GetValue<string>() ?? "local";
        var providerFingerprint = providerSelection?["provider_fingerprint"]?.GetValue<string>() ?? new string('0', 64);
        foreach (var itemId in manifest["item_ids"]?.AsArray().Select(x => x!.GetValue<string>()) ?? [])
        {
            var path = _layout.BatchItem(batchId, itemId);
            if (!File.Exists(path)) continue;
            var item = BatchWorkspaceLayout.ReadJson(path);
            var status = item["status"]?.GetValue<string>();
            if (status is "extracting" or "metadata_running")
            {
                var stage = item["active_attempt_stage"]?.GetValue<string>() ?? (status == "metadata_running" ? "metadata" : "facts");
                var failure = BatchFailureDetails.From(stage, "WB-AI-CANCELLED: AI 请求已取消。");
                JsonObject? attempt = null;
                try
                {
                    attempt = BatchAttemptResultFactory.CreateFailure(batchId, itemId, item, providerId, providerFingerprint, stage, failure, timestamp);
                    BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchItemAttempt(batchId, itemId, attempt["attempt_id"]!.GetValue<string>()), attempt);
                    item["recovery_status"] = "clean";
                }
                catch (InvalidOperationException)
                {
                    item["recovery_status"] = "needs_reconcile";
                }
                item["status"] = "unknown_result";
                item["status_reason"] = "cancelled";
                item["active_attempt_stage"] = "none";
                item["last_attempt_stage"] = stage;
                item["last_error_code"] = failure.Code;
                item["last_error_message"] = failure.Message;
                item["last_error"] = failure.Message;
                item["attempt_count"] = Math.Min(3, (item["attempt_count"]?.GetValue<int>() ?? 0) + 1);
                item.Remove("lease");
                if (attempt is not null) item["correlation_id"] = attempt["correlation_id"]!.DeepClone();
                item["revision"] = (item["revision"]?.GetValue<int>() ?? 0) + 1;
                item["updated_at"] = timestamp.ToString("O");
                if (attempt is not null) _contract.ValidatePayload("attempt", attempt);
                _contract.ValidatePayload("item", item);
                BatchWorkspaceLayout.WriteJsonAtomic(path, item);
            }
            else if (status is "queued" or "metadata_pending")
            {
                item["status"] = "skipped";
                item["status_reason"] = "cancelled";
                item["active_attempt_stage"] = "none";
                item.Remove("lease");
                item["revision"] = (item["revision"]?.GetValue<int>() ?? 0) + 1;
                item["updated_at"] = timestamp.ToString("O");
                _contract.ValidatePayload("item", item);
                BatchWorkspaceLayout.WriteJsonAtomic(path, item);
            }
        }
    }

    private void RequireRevision(JsonObject manifest, int expectedRevision)
    {
        var actual = manifest["revision"]?.GetValue<int>() ?? -1;
        if (actual != expectedRevision) throw new InvalidOperationException("WB-BATCH-REVISION-409: 批次已更新，请刷新后重试。");
    }

    private JsonObject ReadManifest(string batchId)
        => BatchWorkspaceLayout.ReadJson(_layout.BatchManifest(batchId));

    private static DateTimeOffset ParseTimestamp(JsonNode? node)
        => node is not null && DateTimeOffset.TryParse(node.GetValue<string>(), out var value)
            ? value.ToUniversalTime()
            : DateTimeOffset.MinValue;

    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";

    private BatchControlResult WithLock(string key, Func<BatchControlResult> action)
    {
        var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        try { return action(); }
        finally { gate.Release(); }
    }
}
