using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchLeaseSnapshot(
    string AttemptId,
    string FenceToken,
    string OwnerInstanceId,
    int ClaimGeneration,
    DateTimeOffset DeadlineAt);

internal static class BatchLeaseGuard
{
    public static JsonObject Create(string itemId, string ownerInstanceId, int claimGeneration, DateTimeOffset timestamp)
    {
        BatchPathValidator.RequireIdentifier(itemId, "item_id");
        BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        if (claimGeneration < 0) throw new InvalidOperationException("WB-BATCH-CLAIM-422: claim_generation 无效。");
        var attemptId = $"attempt.{itemId}.{Guid.NewGuid():N}";
        var fenceToken = Hashing.Sha256Text($"{attemptId}:{ownerInstanceId}:{claimGeneration}");
        var deadline = timestamp.AddMinutes(2);
        return new JsonObject
        {
            ["attempt_id"] = attemptId,
            ["fence_token"] = fenceToken,
            ["owner_instance_id"] = ownerInstanceId,
            ["issued_at"] = timestamp.ToString("O"),
            ["expires_at"] = deadline.ToString("O"),
            ["deadline_at"] = deadline.ToString("O"),
            ["claim_generation"] = claimGeneration
        };
    }

    public static BatchLeaseSnapshot Require(
        JsonObject item,
        JsonObject manifest,
        string attemptId,
        string ownerInstanceId,
        DateTimeOffset timestamp)
    {
        BatchPathValidator.RequireIdentifier(attemptId, "attempt_id");
        BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        var lease = item["lease"]?.AsObject() ?? throw new InvalidOperationException("WB-BATCH-CLAIM-409: 当前项目缺少执行租约。");
        var actualAttemptId = lease["attempt_id"]?.GetValue<string>() ?? string.Empty;
        var actualOwner = lease["owner_instance_id"]?.GetValue<string>() ?? string.Empty;
        var actualFence = lease["fence_token"]?.GetValue<string>() ?? string.Empty;
        var actualGeneration = lease["claim_generation"]?.GetValue<int>() ?? -1;
        var manifestOwner = manifest["active_owner_instance_id"]?.GetValue<string>() ?? string.Empty;
        var manifestGeneration = manifest["claim_generation"]?.GetValue<int>() ?? -1;
        var deadline = ParseTimestamp(lease["deadline_at"]);
        if (!string.Equals(actualAttemptId, attemptId, StringComparison.Ordinal)
            || !string.Equals(actualOwner, ownerInstanceId, StringComparison.Ordinal)
            || !string.Equals(manifestOwner, ownerInstanceId, StringComparison.Ordinal)
            || actualGeneration != manifestGeneration
            || deadline <= timestamp
            || actualFence.Length != 64
            || actualFence.Any(value => !Uri.IsHexDigit(value)))
            throw new InvalidOperationException("WB-BATCH-CLAIM-409: 执行租约已过期或不再属于当前接管者。");
        return new BatchLeaseSnapshot(actualAttemptId, actualFence, actualOwner, actualGeneration, deadline);
    }

    public static JsonObject Clone(BatchLeaseSnapshot lease)
        => new()
        {
            ["attempt_id"] = lease.AttemptId,
            ["fence_token"] = lease.FenceToken,
            ["owner_instance_id"] = lease.OwnerInstanceId,
            ["issued_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["expires_at"] = lease.DeadlineAt.ToString("O"),
            ["deadline_at"] = lease.DeadlineAt.ToString("O"),
            ["claim_generation"] = lease.ClaimGeneration
        };

    private static DateTimeOffset ParseTimestamp(JsonNode? value)
        => value is not null && DateTimeOffset.TryParse(value.GetValue<string>(), out var timestamp)
            ? timestamp.ToUniversalTime()
            : DateTimeOffset.MinValue;
}
