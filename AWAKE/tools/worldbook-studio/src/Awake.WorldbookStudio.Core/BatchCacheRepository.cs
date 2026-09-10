using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchCacheHit(string CacheKey, string ResultHash, JsonObject KeyPayload, JsonObject Payload, JsonObject Entry);

internal sealed class BatchCacheRepository
{
    private readonly BatchWorkspaceLayout _layout;
    private readonly BatchAuthoringContractRegistry _contract;

    public BatchCacheRepository(WorkspaceService workspace, BatchAuthoringContractRegistry contract)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _contract = contract;
        _ = _contract.RequireSchema("cache.key_payload");
        _ = _contract.RequireSchema("cache");
        _ = _contract.RequireSchema("cache_payload");
        _ = _contract.RequireSchema("cache_commit_marker");
    }

    public string ComputeKey(JsonObject keyPayload) => ContractHashing.HashCanonical(keyPayload);

    public BatchCacheHit? TryRead(string cacheKey)
    {
        var entryPath = _layout.CacheEntry(cacheKey);
        var payloadPath = _layout.CachePayload(cacheKey);
        var markerPath = _layout.CacheCommit(cacheKey);
        if (!File.Exists(entryPath) || !File.Exists(payloadPath) || !File.Exists(markerPath)) return null;
        try
        {
            var entry = BatchWorkspaceLayout.ReadJson(entryPath);
            var payloadEnvelope = BatchWorkspaceLayout.ReadJson(payloadPath);
            var marker = BatchWorkspaceLayout.ReadJson(markerPath);
            if (marker["state"]?.GetValue<string>() != "published") return null;
            if (entry["cache_key"]?.GetValue<string>() != cacheKey || payloadEnvelope["cache_key"]?.GetValue<string>() != cacheKey || marker["cache_key"]?.GetValue<string>() != cacheKey)
                return null;
            var payload = payloadEnvelope["payload"]?.AsObject() ?? throw new InvalidOperationException("WB-BATCH-CACHE-422: cache payload 不是对象。");
            var resultHash = payloadEnvelope["result_hash"]?.GetValue<string>() ?? string.Empty;
            if (resultHash != CanonicalJson.Hash(payload) || entry["result_hash"]?.GetValue<string>() != resultHash || marker["result_hash"]?.GetValue<string>() != resultHash)
                return null;
            var keyPayload = entry["key_payload"]?.AsObject() ?? throw new InvalidOperationException("WB-BATCH-CACHE-422: cache entry 缺少 key_payload。");
            if (ContractHashing.HashCanonical(keyPayload) != cacheKey) return null;
            return new BatchCacheHit(cacheKey, resultHash, keyPayload, payload, entry);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    public BatchCacheHit Publish(JsonObject keyPayload, string stage, string payloadSchemaId, JsonObject payload, DateTimeOffset? now = null)
    {
        if (stage is not ("facts" or "metadata")) throw new InvalidOperationException("WB-BATCH-CACHE-422: cache stage 无效。");
        var cacheKey = ComputeKey(keyPayload);
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var resultHash = CanonicalJson.Hash(payload);
        var entryPath = _layout.CacheEntry(cacheKey);
        var payloadPath = _layout.CachePayload(cacheKey);
        var markerPath = _layout.CacheCommit(cacheKey);
        var entry = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-cache.v2",
            ["cache_key"] = cacheKey,
            ["key_payload"] = CanonicalJson.Canonicalize(keyPayload),
            ["result_schema_revision"] = "batch-authoring.v2",
            ["result_hash"] = resultHash,
            ["created_at"] = timestamp.ToString("O"),
            ["last_used_at"] = timestamp.ToString("O"),
            ["result_payload_path"] = $"cache/v1/{cacheKey}.payload.json",
            ["materialization_revision"] = "materialization.v1",
            ["cache_scope"] = "workspace",
            ["cache_payload_kind"] = stage,
            ["materialization_schema_revision"] = "batch-authoring.v2",
            ["entry_path"] = $"cache/v1/{cacheKey}.json",
            ["payload_path"] = $"cache/v1/{cacheKey}.payload.json",
            ["commit_order"] = new JsonArray("entry", "payload", "marker"),
            ["commit_marker_path"] = $"cache/v1/{cacheKey}.commit.json",
            ["cache_commit_marker_ref"] = $"cache/v1/{cacheKey}.commit.json"
        };
        var payloadEnvelope = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-cache-payload.v2",
            ["cache_key"] = cacheKey,
            ["result_schema_revision"] = "batch-authoring.v2",
            ["result_hash"] = resultHash,
            ["payload_schema_id"] = payloadSchemaId,
            ["payload"] = CanonicalJson.Canonicalize(payload),
            ["created_at"] = timestamp.ToString("O"),
            ["stage"] = stage
        };
        BatchWorkspaceLayout.WriteJsonAtomic(entryPath, entry);
        BatchWorkspaceLayout.WriteJsonAtomic(payloadPath, payloadEnvelope);
        var marker = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-cache-commit-marker.v2",
            ["cache_key"] = cacheKey,
            ["entry_path"] = $"cache/v1/{cacheKey}.json",
            ["payload_path"] = $"cache/v1/{cacheKey}.payload.json",
            ["result_hash"] = resultHash,
            ["result_schema_revision"] = "batch-authoring.v2",
            ["state"] = "published",
            ["created_at"] = timestamp.ToString("O")
        };
        BatchWorkspaceLayout.WriteJsonAtomic(markerPath, marker);
        return new BatchCacheHit(cacheKey, resultHash, keyPayload, payload, entry);
    }

    public string MaterializationId(string cacheKey, string batchId, string itemId, string attemptId)
        => "materialization." + Hashing.Sha256Text(cacheKey + ":" + batchId + ":" + itemId + ":" + attemptId);

    public JsonObject WriteMaterialization(
        string batchId,
        string itemId,
        string materializationId,
        BatchCacheHit cache,
        string attemptId,
        string resultRef,
        IReadOnlyList<string> evidenceIds,
        string stage,
        DateTimeOffset? now = null)
    {
        if (stage is not ("facts" or "metadata")) throw new InvalidOperationException("WB-BATCH-CACHE-422: materialization stage 无效。");
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var materialization = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-cache-materialization.v2",
            ["materialization_id"] = materializationId,
            ["batch_id"] = batchId,
            ["item_id"] = itemId,
            ["cache_key"] = cache.CacheKey,
            ["source_result_hash"] = cache.ResultHash,
            ["created_attempt_id"] = attemptId,
            ["result_ref"] = resultRef,
            ["evidence_ids"] = new JsonArray(evidenceIds.Select(value => JsonValue.Create(value)).ToArray()),
            ["stage"] = stage,
            ["fact_id_map"] = new JsonObject(),
            ["created_at"] = timestamp.ToString("O")
        };
        BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchCacheMaterialization(batchId, materializationId), materialization);
        return materialization;
    }
}
