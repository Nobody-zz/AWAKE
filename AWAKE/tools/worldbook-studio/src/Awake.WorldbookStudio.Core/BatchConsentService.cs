using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchConsentRequest(
    string BatchId,
    int ExpectedRevision,
    string ProviderId,
    string Scope,
    JsonObject ModelParameters,
    string SendScope,
    IReadOnlyList<string> AuthorizedItemIds,
    IReadOnlyList<string>? SelectedSnapshotIds = null);

internal sealed record BatchConsentIssue(JsonObject PublicConsent, string ConsentToken, DateTimeOffset ExpiresAt, string CorrelationId);

internal sealed class BatchConsentService
{
    private static readonly TimeSpan ConsentLifetime = TimeSpan.FromMinutes(10);
    private readonly BatchWorkspaceLayout _layout;
    private readonly BatchAuthoringContractRegistry _contract;
    private readonly BatchFactReviewRepository _factRepository;
    private readonly string _ownerId;
    private readonly string _ownerInstanceId;
    private readonly string _workspaceMarkerHash;

    public BatchConsentService(WorkspaceService workspace, BatchAuthoringContractRegistry contract, string ownerId, string ownerInstanceId, string workspaceMarkerHash)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _contract = contract;
        _factRepository = new BatchFactReviewRepository(workspace, contract, ownerId, ownerInstanceId, workspaceMarkerHash);
        _ownerId = BatchPathValidator.RequireIdentifier(ownerId, "owner_id");
        _ownerInstanceId = BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        RequireHash(workspaceMarkerHash, "workspace_marker_hash");
        _workspaceMarkerHash = workspaceMarkerHash.ToUpperInvariant();
        _ = _contract.RequireSchema("manifest");
        _ = _contract.RequireSchema("consent");
    }

    public BatchConsentIssue Issue(BatchConsentRequest request, DateTimeOffset? now = null)
    {
        ValidateRequest(request);
        using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(request.BatchId));
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var manifest = BatchWorkspaceLayout.ReadJson(_layout.BatchManifest(request.BatchId));
        var revision = manifest["revision"]?.GetValue<int>() ?? -1;
        if (revision != request.ExpectedRevision) throw new InvalidOperationException("WB-BATCH-REVISION-409: 批次已更新，请刷新后重新授权。");
        var claimGeneration = manifest["claim_generation"]?.GetValue<int>() ?? -1;
        var manifestProvider = manifest["provider_selection"]?.AsObject() ?? throw new InvalidOperationException("WB-BATCH-PROVIDER-422: 批次缺少 Provider 选择。");
        if (manifestProvider["provider_id"]?.GetValue<string>() != request.ProviderId || CanonicalJson.Serialize(manifestProvider["model_parameters"]!) != CanonicalJson.Serialize(request.ModelParameters))
            throw new InvalidOperationException("WB-BATCH-PROVIDER-409: consent 的 Provider 选择必须与批次一致。");
        var manifestItems = manifest["item_ids"]!.AsArray().Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        if (request.AuthorizedItemIds.Count == 0 || request.AuthorizedItemIds.Any(x => !manifestItems.Contains(x)))
            throw new InvalidOperationException("WB-BATCH-CONSENT-422: 授权项目不属于当前批次。");
        var scanId = manifest["source_scan_id"]!.GetValue<string>();
        var scan = BatchWorkspaceLayout.ReadJson(_layout.ScanManifest(scanId));
        var snapshotIds = scan["snapshot_ids"]!.AsArray().Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        if (request.SendScope == "selected_snapshots" && (request.SelectedSnapshotIds is null || request.SelectedSnapshotIds.Count == 0 || request.SelectedSnapshotIds.Any(x => !snapshotIds.Contains(x))))
            throw new InvalidOperationException("WB-BATCH-CONSENT-422: 选择的资料快照不属于当前扫描。");
        if (request.SendScope == "all_snapshots" && request.SelectedSnapshotIds is { Count: > 0 })
            throw new InvalidOperationException("WB-BATCH-CONSENT-422: all_snapshots 不允许同时提交 selected_snapshot_ids。");
        var selectedBindings = request.SendScope == "selected_snapshots"
            ? request.SelectedSnapshotIds!.Select(id => SnapshotBinding(_layout.BatchSnapshotMeta(request.BatchId, id))).OrderBy(x => CanonicalJson.Serialize(x), StringComparer.Ordinal).ToArray()
            : snapshotIds.Select(id => SnapshotBinding(_layout.BatchSnapshotMeta(request.BatchId, id))).OrderBy(x => CanonicalJson.Serialize(x), StringComparer.Ordinal).ToArray();
        var token = NewToken();
        var consentId = NewId("consent");
        var expires = timestamp.Add(ConsentLifetime);
        var bindingHash = CanonicalJson.Hash(new JsonObject
        {
            ["authorized_item_ids"] = new JsonArray(request.AuthorizedItemIds.OrderBy(x => x, StringComparer.Ordinal).Select(x => JsonValue.Create(x)).ToArray()),
            ["batch_id"] = request.BatchId,
            ["claim_generation"] = claimGeneration,
            ["model_parameters"] = CanonicalJson.Canonicalize(request.ModelParameters),
            ["owner_id"] = _ownerId,
            ["provider_fingerprint"] = manifestProvider["provider_fingerprint"]!.GetValue<string>(),
            ["scope"] = request.Scope,
            ["selected_snapshot_bindings"] = new JsonArray(selectedBindings.Select(binding => binding.DeepClone()).ToArray()),
            ["send_scope"] = request.SendScope,
            ["workspace_marker_hash"] = _workspaceMarkerHash
        });
        var consent = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-consent.v2",
            ["consent_id"] = consentId,
            ["token_hash"] = Hashing.Sha256Text(token),
            ["batch_id"] = request.BatchId,
            ["owner_id"] = _ownerId,
            ["owner_instance_id"] = _ownerInstanceId,
            ["provider_id"] = request.ProviderId,
            ["provider_fingerprint"] = manifestProvider["provider_fingerprint"]!.GetValue<string>(),
            ["model_parameters"] = CanonicalJson.Canonicalize(request.ModelParameters),
            ["scope"] = request.Scope,
            ["binding_hash"] = bindingHash,
            ["state"] = "issued",
            ["created_at"] = timestamp.ToString("O"),
            ["expires_at"] = expires.ToString("O"),
            ["workspace_marker_hash"] = _workspaceMarkerHash,
            ["claim_generation"] = claimGeneration,
            ["send_scope"] = request.SendScope,
            ["revision"] = 0,
            ["consume_cas"] = new JsonObject { ["state"] = "issued", ["revision"] = 0, ["claim_generation"] = claimGeneration },
            ["snapshot_bindings"] = new JsonArray(selectedBindings.Select(binding => binding.DeepClone()).ToArray()),
            ["authorized_item_ids"] = new JsonArray(request.AuthorizedItemIds.Select(x => JsonValue.Create(x)).ToArray())
        };
        BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchConsent(request.BatchId, consentId), consent);
        if (request.Scope == "facts_and_metadata")
        {
            foreach (var itemId in request.AuthorizedItemIds)
                _factRepository.QueueAcceptedItemForMetadata(request.BatchId, itemId, timestamp);
        }
        var publicConsent = ProjectPublic(consent);
        return new BatchConsentIssue(publicConsent, token, expires, NewId("correlation"));
    }

    public bool HasIssuedMetadataAuthorization(string batchId, string itemId, DateTimeOffset? now = null)
    {
        BatchPathValidator.RequireIdentifier(batchId, "batch_id");
        BatchPathValidator.RequireIdentifier(itemId, "item_id");
        var manifest = BatchWorkspaceLayout.ReadJson(_layout.BatchManifest(batchId));
        var claimGeneration = manifest["claim_generation"]?.GetValue<int>() ?? -1;
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        foreach (var path in Directory.EnumerateFiles(_layout.BatchRoot(batchId), "*.json", SearchOption.AllDirectories)
                     .Where(candidate => candidate.Contains("consents", StringComparison.OrdinalIgnoreCase)))
        {
            var consent = BatchWorkspaceLayout.ReadJson(path);
            if (consent["owner_instance_id"]?.GetValue<string>() != _ownerInstanceId
                || consent["state"]?.GetValue<string>() != "issued"
                || consent["scope"]?.GetValue<string>() != "facts_and_metadata"
                || consent["claim_generation"]?.GetValue<int>() != claimGeneration
                || ParseTime(consent["expires_at"]) <= timestamp)
                continue;
            if (consent["authorized_item_ids"]?.AsArray().Any(value => value?.GetValue<string>() == itemId) == true)
                return true;
        }
        return false;
    }

    public JsonObject Consume(string batchId, string token, string stage, IReadOnlyList<string> targetItemIds, int expectedRevision, int claimGeneration, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("WB-BATCH-CONSENT-403: consent token 不能为空。");
        var tokenHash = Hashing.Sha256Text(token);
        var consentPath = Directory.EnumerateFiles(_layout.BatchRoot(batchId), "*.json", SearchOption.AllDirectories)
            .FirstOrDefault(path => path.Contains("consents", StringComparison.OrdinalIgnoreCase)
                && BatchWorkspaceLayout.ReadJson(path)["token_hash"]?.GetValue<string>() == tokenHash);
        if (consentPath is null) throw new InvalidOperationException("WB-BATCH-CONSENT-404: consent 不存在或已失效。");
        var consent = BatchWorkspaceLayout.ReadJson(consentPath);
        if (consent["owner_instance_id"]?.GetValue<string>() != _ownerInstanceId) throw new InvalidOperationException("WB-BATCH-OWNER-403: consent 不属于当前 Studio 实例。");
        if (consent["state"]?.GetValue<string>() != "issued") throw new InvalidOperationException("WB-BATCH-CONSENT-409: consent 已被消费或撤销。");
        if (ParseTime(consent["expires_at"]) <= (now ?? DateTimeOffset.UtcNow).ToUniversalTime()) throw new InvalidOperationException("WB-BATCH-CONSENT-409: consent 已过期。");
        if (consent["revision"]?.GetValue<int>() != expectedRevision || consent["claim_generation"]?.GetValue<int>() != claimGeneration)
            throw new InvalidOperationException("WB-BATCH-CONSENT-409: consent revision 或 claim_generation 不匹配。");
        var authorized = consent["authorized_item_ids"]!.AsArray().Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        if (targetItemIds.Count == 0 || targetItemIds.Any(x => !authorized.Contains(x))) throw new InvalidOperationException("WB-BATCH-CONSENT-422: start 目标超出 consent 授权范围。");
        var scope = consent["scope"]!.GetValue<string>();
        if (stage == "metadata" && scope != "facts_and_metadata") throw new InvalidOperationException("WB-BATCH-CONSENT-422: metadata 阶段需要 facts_and_metadata consent。");
        if (stage is not ("facts" or "metadata")) throw new InvalidOperationException("WB-BATCH-STAGE-422: 阶段无效。");
        if (stage == "facts" && scope == "facts_and_metadata") return consent;
        var next = (JsonObject)consent.DeepClone();
        next["state"] = "consumed";
        next["revision"] = expectedRevision + 1;
        next["consumed_at"] = (now ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("O");
        next["consumed_by_owner_instance_id"] = _ownerInstanceId;
        BatchWorkspaceLayout.WriteJsonAtomic(consentPath, next);
        return next;
    }

    public JsonObject ValidateForStart(string batchId, string token, string stage, IReadOnlyList<string> targetItemIds, int expectedRevision, int claimGeneration, DateTimeOffset? now = null)
    {
        var consentPath = FindConsentPath(batchId, token);
        var consent = BatchWorkspaceLayout.ReadJson(consentPath);
        ValidateForStart(consent, stage, targetItemIds, expectedRevision, claimGeneration, now);
        return consent;
    }

    public void RevokeIssued(string batchId, int newClaimGeneration, DateTimeOffset timestamp)
    {
        BatchPathValidator.RequireIdentifier(batchId, "batch_id");
        foreach (var path in Directory.EnumerateFiles(_layout.BatchRoot(batchId), "*.json", SearchOption.AllDirectories)
                     .Where(candidate => candidate.Contains("consents", StringComparison.OrdinalIgnoreCase)))
        {
            var consent = BatchWorkspaceLayout.ReadJson(path);
            if (consent["batch_id"]?.GetValue<string>() != batchId || consent["state"]?.GetValue<string>() != "issued") continue;
            consent["state"] = "revoked";
            consent["revoked_at"] = timestamp.ToString("O");
            consent["revocation_reason"] = "claim_generation_changed";
            consent["claim_generation"] = newClaimGeneration;
            BatchWorkspaceLayout.WriteJsonAtomic(path, consent);
        }
    }

    public static JsonObject ProjectPublic(JsonObject consent)
        => new()
        {
            ["consent_id"] = consent["consent_id"]!.DeepClone(),
            ["batch_id"] = consent["batch_id"]!.DeepClone(),
            ["scope"] = consent["scope"]!.DeepClone(),
            ["send_scope"] = consent["send_scope"]!.DeepClone(),
            ["claim_generation"] = consent["claim_generation"]!.DeepClone(),
            ["revision"] = consent["revision"]!.DeepClone(),
            ["state"] = consent["state"]!.DeepClone(),
            ["expires_at"] = consent["expires_at"]!.DeepClone(),
            ["authorized_item_ids"] = consent["authorized_item_ids"]!.DeepClone()
        };

    private JsonObject SnapshotBinding(string path)
    {
        var snapshot = BatchWorkspaceLayout.ReadJson(path);
        return new JsonObject { ["snapshot_id"] = snapshot["snapshot_id"]!.DeepClone(), ["snapshot_hash"] = snapshot["normalized_content_hash"]!.DeepClone() };
    }

    private string FindConsentPath(string batchId, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("WB-BATCH-CONSENT-403: consent token 不能为空。");
        var tokenHash = Hashing.Sha256Text(token);
        var path = Directory.EnumerateFiles(_layout.BatchRoot(batchId), "*.json", SearchOption.AllDirectories)
            .FirstOrDefault(candidate => candidate.Contains("consents", StringComparison.OrdinalIgnoreCase)
                && BatchWorkspaceLayout.ReadJson(candidate)["token_hash"]?.GetValue<string>() == tokenHash);
        return path ?? throw new InvalidOperationException("WB-BATCH-CONSENT-404: consent 不存在或已失效。");
    }

    private void ValidateForStart(JsonObject consent, string stage, IReadOnlyList<string> targetItemIds, int expectedRevision, int claimGeneration, DateTimeOffset? now)
    {
        if (consent["owner_instance_id"]?.GetValue<string>() != _ownerInstanceId) throw new InvalidOperationException("WB-BATCH-OWNER-403: consent 不属于当前 Studio 实例。");
        if (consent["state"]?.GetValue<string>() != "issued") throw new InvalidOperationException("WB-BATCH-CONSENT-409: consent 已被消费或撤销。");
        if (ParseTime(consent["expires_at"]) <= (now ?? DateTimeOffset.UtcNow).ToUniversalTime()) throw new InvalidOperationException("WB-BATCH-CONSENT-409: consent 已过期。");
        if (consent["revision"]?.GetValue<int>() != expectedRevision || consent["claim_generation"]?.GetValue<int>() != claimGeneration)
            throw new InvalidOperationException("WB-BATCH-CONSENT-409: consent revision 或 claim_generation 不匹配。");
        var authorized = consent["authorized_item_ids"]!.AsArray().Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        if (targetItemIds.Count == 0 || targetItemIds.Any(x => !authorized.Contains(x))) throw new InvalidOperationException("WB-BATCH-CONSENT-422: start 目标超出 consent 授权范围。");
        var scope = consent["scope"]!.GetValue<string>();
        if (stage == "metadata" && scope != "facts_and_metadata") throw new InvalidOperationException("WB-BATCH-CONSENT-422: metadata 阶段需要 facts_and_metadata consent。");
        if (stage is not ("facts" or "metadata")) throw new InvalidOperationException("WB-BATCH-STAGE-422: 阶段无效。");
        if (stage == "facts" && scope == "facts_and_metadata") return;
    }

    private static void ValidateRequest(BatchConsentRequest request)
    {
        BatchPathValidator.RequireIdentifier(request.BatchId, "batch_id");
        if (request.ProviderId is not ("local" or "cloud")) throw new InvalidOperationException("WB-BATCH-PROVIDER-422: Provider 类型无效。");
        if (request.Scope is not ("facts" or "facts_and_metadata")) throw new InvalidOperationException("WB-BATCH-CONSENT-422: consent scope 无效。");
        if (request.SendScope is not ("all_snapshots" or "selected_snapshots")) throw new InvalidOperationException("WB-BATCH-CONSENT-422: send_scope 无效。");
        if (request.AuthorizedItemIds is null || request.AuthorizedItemIds.Count == 0 || request.AuthorizedItemIds.Distinct(StringComparer.Ordinal).Count() != request.AuthorizedItemIds.Count) throw new InvalidOperationException("WB-BATCH-CONSENT-422: authorized_item_ids 无效。");
    }

    private static DateTimeOffset ParseTime(JsonNode? node)
        => node is not null && DateTimeOffset.TryParse(node.GetValue<string>(), out var value) ? value.ToUniversalTime() : throw new InvalidOperationException("WB-BATCH-CONSENT-422: expires_at 格式无效。");
    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    private static void RequireHash(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(x => !Uri.IsHexDigit(x))) throw new InvalidOperationException($"WB-BATCH-REQUEST-422: {field} 必须是 SHA-256。");
    }
}
