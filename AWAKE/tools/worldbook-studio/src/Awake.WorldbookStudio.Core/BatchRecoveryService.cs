using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed class BatchRecoveryService
{
    private readonly BatchWorkspaceLayout _layout;
    private readonly BatchAuthoringContractRegistry? _contract;

    public BatchRecoveryService(WorkspaceService workspace, BatchAuthoringContractRegistry? contract = null)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _contract = contract;
    }

    public int RecoverInFlight(DateTimeOffset? now = null)
    {
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var recovered = 0;
        foreach (var batchRoot in Directory.EnumerateDirectories(_layout.BatchesRoot))
        {
            var batchId = Path.GetFileName(batchRoot);
            if (string.IsNullOrWhiteSpace(batchId)) continue;
            try
            {
                using var lease = BatchWorkspaceWriteLease.Acquire(batchRoot);
                recovered += RecoverBatch(batchId, timestamp);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                TryMarkBatchNeedsReconcile(batchId, timestamp, error);
            }
        }
        return recovered;
    }

    private int RecoverBatch(string batchId, DateTimeOffset timestamp)
    {
        var manifestPath = _layout.BatchManifest(batchId);
        if (!File.Exists(manifestPath)) return 0;
        var manifest = BatchWorkspaceLayout.ReadJson(manifestPath);
        var provider = manifest["provider_selection"]?.AsObject();
        var providerId = provider?["provider_id"]?.GetValue<string>() ?? "local";
        var providerFingerprint = provider?["provider_fingerprint"]?.GetValue<string>() ?? new string('0', 64);
        var recovered = 0;
        foreach (var itemId in manifest["item_ids"]?.AsArray().Select(value => value!.GetValue<string>()) ?? [])
        {
            var itemPath = _layout.BatchItem(batchId, itemId);
            if (!File.Exists(itemPath)) continue;
            var item = BatchWorkspaceLayout.ReadJson(itemPath);
            var status = item["status"]?.GetValue<string>();
            if (status is not ("extracting" or "metadata_running")) continue;
            var stage = item["active_attempt_stage"]?.GetValue<string>()
                ?? (status == "metadata_running" ? "metadata" : "facts");
            var failure = BatchFailureDetails.From(
                stage,
                "WB-BATCH-RECOVERY-UNKNOWN-503: 工作室重启时 Provider 请求尚未结算，结果需要人工对账。");
            var lease = item["lease"]?.AsObject();
            if (lease is not null)
            {
                var attemptId = lease["attempt_id"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(attemptId))
                {
                    var attemptPath = _layout.BatchItemAttempt(batchId, itemId, attemptId);
                    if (!File.Exists(attemptPath))
                    {
                        var attempt = BatchAttemptResultFactory.CreateFailure(
                            batchId,
                            itemId,
                            item,
                            providerId,
                            providerFingerprint,
                            stage,
                            failure,
                            timestamp);
                        _contract?.ValidatePayload("attempt", attempt);
                        BatchWorkspaceLayout.WriteJsonAtomic(attemptPath, attempt);
                        item["correlation_id"] = attempt["correlation_id"]!.DeepClone();
                    }
                }
            }
            item["status"] = "unknown_result";
            item["status_reason"] = "process_restart_needs_reconcile";
            item["recovery_status"] = "needs_reconcile";
            item["last_attempt_stage"] = stage;
            item["last_error_code"] = failure.Code;
            item["last_error_message"] = failure.Message;
            item["last_error"] = failure.Message;
            item["active_attempt_stage"] = "none";
            item.Remove("lease");
            item["revision"] = (item["revision"]?.GetValue<int>() ?? 0) + 1;
            item["updated_at"] = timestamp.ToString("O");
            _contract?.ValidatePayload("item", item);
            BatchWorkspaceLayout.WriteJsonAtomic(itemPath, item);
            recovered++;
        }
        if (recovered == 0) return 0;
        manifest["status"] = "needs_reconcile";
        manifest["status_reason"] = "process_restart_needs_reconcile";
        manifest["revision"] = (manifest["revision"]?.GetValue<int>() ?? 0) + 1;
        manifest["commit_seq"] = (manifest["commit_seq"]?.GetValue<int>() ?? 0) + 1;
        manifest["updated_at"] = timestamp.ToString("O");
        _contract?.ValidatePayload("manifest", manifest);
        BatchWorkspaceLayout.WriteJsonAtomic(manifestPath, manifest);
        return recovered;
    }

    private void TryMarkBatchNeedsReconcile(string batchId, DateTimeOffset timestamp, Exception error)
    {
        try
        {
            var manifestPath = _layout.BatchManifest(batchId);
            if (!File.Exists(manifestPath)) return;
            var manifest = BatchWorkspaceLayout.ReadJson(manifestPath);
            manifest["status"] = "failed";
            manifest["recovery_status"] = "needs_reconcile";
            manifest["status_reason"] = "needs_reconcile";
            manifest["last_error"] = error.Message.Length > 2000 ? error.Message[..2000] : error.Message;
            manifest["revision"] = (manifest["revision"]?.GetValue<int>() ?? 0) + 1;
            manifest["commit_seq"] = (manifest["commit_seq"]?.GetValue<int>() ?? 0) + 1;
            manifest["updated_at"] = timestamp.ToString("O");
            _contract?.ValidatePayload("manifest", manifest);
            BatchWorkspaceLayout.WriteJsonAtomic(manifestPath, manifest);
        }
        catch
        {
        }
    }
}
