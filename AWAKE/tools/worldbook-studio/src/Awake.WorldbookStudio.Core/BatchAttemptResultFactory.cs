using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class BatchAttemptResultFactory
{
    public static JsonObject CreateFailure(
        string batchId,
        string itemId,
        JsonObject item,
        string providerId,
        string providerFingerprint,
        string stage,
        BatchFailureDetails failure,
        DateTimeOffset finishedAt)
    {
        var lease = item["lease"]?.AsObject()
            ?? throw new InvalidOperationException("WB-BATCH-ATTEMPT-409: 失败项目缺少执行租约。");
        var attemptId = lease["attempt_id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("WB-BATCH-ATTEMPT-409: 执行租约缺少 attempt_id。");
        var startedAt = ReadTimestamp(lease["issued_at"], finishedAt);
        var outcome = failure.ErrorClass == "cancelled" && failure.Outcome == "cancelled"
            ? "unknown_result"
            : failure.Outcome;
        var errorClass = failure.ErrorClass == "unknown" ? "upstream" : failure.ErrorClass;
        var attempt = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-attempt-result.v2",
            ["attempt_id"] = attemptId,
            ["provider_fingerprint"] = providerFingerprint,
            ["outcome"] = outcome,
            ["error_class"] = errorClass,
            ["started_at"] = startedAt.ToString("O"),
            ["finished_at"] = finishedAt.ToUniversalTime().ToString("O"),
            ["latency_ms"] = ElapsedMilliseconds(startedAt, finishedAt),
            ["token_usage"] = new JsonObject
            {
                ["known"] = false,
                ["input_tokens"] = null,
                ["output_tokens"] = null
            },
            ["correlation_id"] = NewId("correlation"),
            ["logical_attempt_number"] = Math.Clamp((item["attempt_count"]?.GetValue<int>() ?? 0) + 1, 1, 3),
            ["exchange_count"] = 1,
            ["batch_id"] = batchId,
            ["item_id"] = itemId,
            ["provider_id"] = providerId,
            ["owner_instance_id"] = lease["owner_instance_id"]!.DeepClone(),
            ["fence_token"] = lease["fence_token"]!.DeepClone(),
            ["deadline_at"] = lease["deadline_at"]!.DeepClone(),
            ["lease"] = lease.DeepClone(),
            ["provider_request_id"] = "unknown:" + attemptId,
            ["item_revision"] = item["revision"]!.DeepClone(),
            ["claim_generation"] = lease["claim_generation"]!.DeepClone(),
            ["attempt_kind"] = "provider",
            ["token_usage_known"] = false,
            ["stage"] = stage,
            ["result_schema_id"] = stage == "metadata"
                ? "awake.worldbook.batch-metadata-result.v2"
                : "awake.worldbook.batch-fact-result.v2"
        };
        if (outcome == "transient_error" || errorClass == "rate_limit")
            attempt["retry_after_seconds"] = Math.Clamp(failure.RetryAfterSeconds ?? 0, 0, 300);
        if (stage == "metadata")
            attempt["source_fact_set_hash"] = ReadHash(item["accepted_fact_set_hash"]);
        return attempt;
    }

    private static string ReadHash(JsonNode? value)
    {
        var hash = value?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(hash) && hash.Length == 64 && hash.All(Uri.IsHexDigit)
            ? hash.ToUpperInvariant()
            : Hashing.Sha256Text(string.Empty);
    }

    private static DateTimeOffset ReadTimestamp(JsonNode? value, DateTimeOffset fallback)
        => value is not null && DateTimeOffset.TryParse(value.GetValue<string>(), out var timestamp)
            ? timestamp.ToUniversalTime()
            : fallback.ToUniversalTime();

    private static int ElapsedMilliseconds(DateTimeOffset startedAt, DateTimeOffset finishedAt)
    {
        var elapsed = (finishedAt.ToUniversalTime() - startedAt.ToUniversalTime()).TotalMilliseconds;
        return elapsed <= 0 ? 0 : elapsed >= int.MaxValue ? int.MaxValue : (int)elapsed;
    }

    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
}
