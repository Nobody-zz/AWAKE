using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record AuthoringSourceSnapshot(
    string SnapshotId,
    string SourceName,
    string SourceNature,
    string Text,
    string ContentHash,
    string SchemaVersion = "awake.worldbook.authoring-source-snapshot.v1");

internal sealed record AuthoringGenerationPacket(
    string PacketId,
    string SourceSnapshotId,
    string SourceContentHash,
    string ProviderId,
    string ProviderFingerprint,
    IReadOnlyList<string> RequestedLayers,
    IReadOnlyList<string> Perspectives,
    string PacketHash,
    JsonObject? ModelParameters = null,
    string? RegistrySnapshotHash = null,
    string? NormalizationRevision = null,
    string SchemaVersion = "awake.worldbook.authoring-generation-packet.v1");

internal sealed record AuthoringCandidate(
    string CandidateId,
    string Fingerprint,
    string SourceSnapshotId,
    string SourceContentHash,
    string PacketHash,
    string ProviderFingerprint,
    IReadOnlyList<AuthoringDraftFact> Facts,
    AuthoringDraftMetadata? Metadata,
    IReadOnlyList<AuthoringDraftExpression> Expressions,
    string ReviewStatus = "pending",
    bool EvidenceCurrent = true,
    string? InvalidationReason = null,
    IReadOnlyList<string>? SegmentationReasonCodes = null,
    IReadOnlyList<JsonObject>? SourceSpans = null,
    string SchemaVersion = "awake.worldbook.authoring-candidate.v1",
    IReadOnlyList<AuthoringDraftTargetSpan>? TargetSpans = null,
    IReadOnlyList<AuthoringDraftProposition>? Propositions = null,
    IReadOnlyList<AuthoringDraftClaim>? Claims = null,
    IReadOnlyList<AuthoringDraftUnresolved>? Unresolved = null,
    JsonObject? Coverage = null);

internal sealed record AuthoringCandidateSet(
    string GenerationId,
    string SourceSnapshotId,
    string SourceContentHash,
    string PacketHash,
    string ProviderFingerprint,
    string Outcome,
    IReadOnlyList<AuthoringCandidate> Candidates,
    string? InvalidationReason = null,
    string SchemaVersion = "awake.worldbook.authoring-candidate-set.v1");

internal static class AuthoringLifecycleFactory
{
    public static AuthoringSourceSnapshot CreateSnapshot(
        string snapshotId,
        string sourceName,
        string sourceNature,
        string sourceText)
    {
        var text = AuthoringDraftRequestFactory.NormalizeSourceText(sourceText);
        if (text.Length == 0) throw new InvalidOperationException("WB-AI-AUTHORING-400: 参考资料不能为空。");
        return new AuthoringSourceSnapshot(
            RequireId(snapshotId, "snapshot_id"),
            string.IsNullOrWhiteSpace(sourceName) ? "未命名参考资料" : sourceName.Trim(),
            string.IsNullOrWhiteSpace(sourceNature) ? "under_review" : sourceNature.Trim(),
            text,
            Hashing.Sha256Text(text));
    }

    public static AuthoringGenerationPacket CreatePacket(
        AuthoringSourceSnapshot snapshot,
        string packetId,
        string providerId,
        string providerFingerprint,
        IReadOnlyList<string> requestedLayers,
        IReadOnlyList<string>? perspectives = null,
        JsonObject? modelParameters = null,
        string? registrySnapshotHash = null,
        string? normalizationRevision = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var layers = NormalizeList(requestedLayers, 8);
        if (layers.Count == 0) throw new InvalidOperationException("WB-AI-AUTHORING-422: 至少需要一个生成层。");
        var views = NormalizeList(perspectives ?? [], 32);
        var body = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring-generation-packet.v1",
            ["packet_id"] = RequireId(packetId, "packet_id"),
            ["source_snapshot_id"] = snapshot.SnapshotId,
            ["source_content_hash"] = snapshot.ContentHash,
            ["provider_id"] = providerId.Trim().ToLowerInvariant(),
            ["provider_fingerprint"] = RequireHash(providerFingerprint, "provider_fingerprint"),
            ["requested_layers"] = new JsonArray(layers.Select(value => JsonValue.Create(value)).ToArray()!),
            ["perspectives"] = new JsonArray(views.Select(value => JsonValue.Create(value)).ToArray()!)
        };
        if (modelParameters is not null) body["model_parameters"] = CanonicalJson.Canonicalize(modelParameters);
        if (!string.IsNullOrWhiteSpace(registrySnapshotHash)) body["registry_snapshot_hash"] = registrySnapshotHash;
        if (!string.IsNullOrWhiteSpace(normalizationRevision)) body["normalization_revision"] = normalizationRevision;
        return new AuthoringGenerationPacket(
            body["packet_id"]!.GetValue<string>(),
            snapshot.SnapshotId,
            snapshot.ContentHash,
            body["provider_id"]!.GetValue<string>(),
            body["provider_fingerprint"]!.GetValue<string>(),
            layers,
            views,
            CanonicalJson.Hash(body),
            modelParameters is null ? null : CanonicalJson.Canonicalize(modelParameters).AsObject(),
            string.IsNullOrWhiteSpace(registrySnapshotHash) ? null : registrySnapshotHash,
            normalizationRevision);
    }

    public static JsonObject ProjectPacket(AuthoringGenerationPacket packet)
    {
        var body = new JsonObject
        {
            ["schema_version"] = packet.SchemaVersion,
            ["packet_id"] = packet.PacketId,
            ["source_snapshot_id"] = packet.SourceSnapshotId,
            ["source_content_hash"] = packet.SourceContentHash,
            ["provider_id"] = packet.ProviderId,
            ["provider_fingerprint"] = packet.ProviderFingerprint,
            ["requested_layers"] = new JsonArray(packet.RequestedLayers.Select(value => JsonValue.Create(value)).ToArray()!),
            ["perspectives"] = new JsonArray(packet.Perspectives.Select(value => JsonValue.Create(value)).ToArray()!)
        };
        if (packet.ModelParameters is not null) body["model_parameters"] = CanonicalJson.Canonicalize(packet.ModelParameters);
        if (!string.IsNullOrWhiteSpace(packet.RegistrySnapshotHash)) body["registry_snapshot_hash"] = packet.RegistrySnapshotHash;
        if (!string.IsNullOrWhiteSpace(packet.NormalizationRevision)) body["normalization_revision"] = packet.NormalizationRevision;
        body["packet_hash"] = packet.PacketHash;
        return body;
    }

    public static string RecomputePacketHash(JsonObject packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var body = (JsonObject)packet.DeepClone();
        body.Remove("packet_hash");
        return CanonicalJson.Hash(body);
    }

    public static string ResolveProviderFingerprint(ProviderConfiguration configuration, string providerId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var normalizedProvider = providerId.Trim().ToLowerInvariant();
        var endpoint = normalizedProvider == "cloud"
            ? configuration.CloudBaseUrl
            : configuration.LocalWorkerUrl;
        var model = normalizedProvider == "cloud"
            ? configuration.CloudModel
            : string.Empty;

        return Hashing.Sha256Text(CanonicalJson.Serialize(new JsonObject
        {
            ["provider_id"] = normalizedProvider,
            ["endpoint"] = endpoint ?? string.Empty,
            ["model"] = model ?? string.Empty
        }));
    }

    public static AuthoringCandidateSet FromDraftResult(
        AuthoringDraftRequest request,
        AuthoringDraftResult result,
        string providerFingerprint)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        var snapshot = CreateSnapshot(request.DraftId, request.SourceName, request.SourceNature, request.SourceText);
        var packet = CreatePacket(
            snapshot,
            request.DraftId,
            request.ProviderId,
            providerFingerprint,
            request.Stage == AuthoringDraftStage.Complete
                ? ["facts", "metadata", "expressions"]
                : [AuthoringDraftStageNames.ToWire(request.Stage)],
            request.Perspectives,
            normalizationRevision: request.NormalizationRevision);
        if (result.Candidates is { Count: > 0 })
        {
            var candidates = result.Candidates.Select(payload =>
            {
                var reasonCodes = NormalizeCandidateReasonCodes(payload.SegmentationReasonCodes);
                var sourceSpans = NormalizeCandidateSourceSpans(
                    payload.SourceSpans,
                    request.SourceText,
                    payload.Facts,
                    payload.Expressions);
                var body = new JsonObject
                {
                    ["facts"] = new JsonArray(payload.Facts.Select(AuthoringDraftRequestFactory.SerializeFact).ToArray()!),
                    ["metadata"] = payload.Metadata is null ? null : AuthoringDraftRequestFactory.SerializeMetadata(payload.Metadata),
                    ["expressions"] = new JsonArray(payload.Expressions.Select(AuthoringDraftRequestFactory.SerializeExpression).ToArray()!),
                    ["request_hash"] = request.RequestHash,
                    ["mode"] = request.Intent is null ? "legacy_staged" : AuthoringDraftModeNames.ToWire(request.Intent.Mode),
                    ["prompt_revision"] = request.PromptRevision,
                    ["normalization_revision"] = request.NormalizationRevision,
                    ["source_content_hash"] = snapshot.ContentHash,
                    ["packet_hash"] = packet.PacketHash,
                    ["provider_fingerprint"] = packet.ProviderFingerprint,
                    ["segmentation_reason_codes"] = new JsonArray(reasonCodes.Select(value => JsonValue.Create(value)).ToArray()!),
                    ["source_spans"] = new JsonArray(sourceSpans.Select(value => value.DeepClone()).ToArray()!),
                    ["target_spans"] = new JsonArray((payload.TargetSpans ?? []).Select(AuthoringDraftRequestFactory.SerializeTargetSpan).ToArray()!),
                    ["propositions"] = new JsonArray((payload.Propositions ?? []).Select(AuthoringDraftRequestFactory.SerializeProposition).ToArray()!),
                    ["claims"] = new JsonArray((payload.Claims ?? []).Select(AuthoringDraftRequestFactory.SerializeClaim).ToArray()!),
                    ["unresolved"] = new JsonArray((payload.Unresolved ?? []).Select(AuthoringDraftRequestFactory.SerializeUnresolved).ToArray()!),
                    ["coverage"] = payload.Coverage?.DeepClone()
                };
                var fingerprint = CanonicalJson.Hash(body);
                return new AuthoringCandidate(
                    "candidate." + fingerprint[..24],
                    fingerprint,
                    snapshot.SnapshotId,
                    snapshot.ContentHash,
                    packet.PacketHash,
                    packet.ProviderFingerprint,
                    payload.Facts,
                    payload.Metadata,
                     payload.Expressions,
                     SegmentationReasonCodes: reasonCodes,
                     SourceSpans: sourceSpans,
                     TargetSpans: payload.TargetSpans,
                     Propositions: payload.Propositions,
                     Claims: payload.Claims,
                     Unresolved: payload.Unresolved,
                     Coverage: payload.Coverage);
            }).ToArray();
            EnsureCandidateIdentities(candidates);
            return new AuthoringCandidateSet(
                packet.PacketId,
                snapshot.SnapshotId,
                snapshot.ContentHash,
                packet.PacketHash,
                packet.ProviderFingerprint,
                "succeeded",
                candidates);
        }

        if (result.Facts.Count == 0 && result.Metadata is null && result.Expressions.Count == 0)
            return new AuthoringCandidateSet(packet.PacketId, snapshot.SnapshotId, snapshot.ContentHash, packet.PacketHash, packet.ProviderFingerprint, "succeeded_zero_output", []);

        var candidateBody = new JsonObject
        {
            ["facts"] = new JsonArray(result.Facts.Select(AuthoringDraftRequestFactory.SerializeFact).ToArray()),
            ["metadata"] = result.Metadata is null ? null : AuthoringDraftRequestFactory.SerializeMetadata(result.Metadata),
            ["expressions"] = new JsonArray(result.Expressions.Select(AuthoringDraftRequestFactory.SerializeExpression).ToArray()),
            ["request_hash"] = request.RequestHash,
            ["mode"] = request.Intent is null ? "legacy_staged" : AuthoringDraftModeNames.ToWire(request.Intent.Mode),
            ["prompt_revision"] = request.PromptRevision,
            ["normalization_revision"] = request.NormalizationRevision,
            ["source_content_hash"] = snapshot.ContentHash,
            ["packet_hash"] = packet.PacketHash,
            ["provider_fingerprint"] = packet.ProviderFingerprint,
            ["segmentation_reason_codes"] = new JsonArray(),
            ["source_spans"] = new JsonArray(),
            ["target_spans"] = new JsonArray((result.TargetSpans ?? []).Select(AuthoringDraftRequestFactory.SerializeTargetSpan).ToArray()!),
            ["propositions"] = new JsonArray((result.Propositions ?? []).Select(AuthoringDraftRequestFactory.SerializeProposition).ToArray()!),
            ["claims"] = new JsonArray((result.Claims ?? []).Select(AuthoringDraftRequestFactory.SerializeClaim).ToArray()!),
            ["unresolved"] = new JsonArray((result.Unresolved ?? []).Select(AuthoringDraftRequestFactory.SerializeUnresolved).ToArray()!),
            ["coverage"] = result.Coverage?.DeepClone()
        };
        var fingerprint = CanonicalJson.Hash(candidateBody);
        var candidate = new AuthoringCandidate(
            "candidate." + fingerprint[..24],
            fingerprint,
            snapshot.SnapshotId,
            snapshot.ContentHash,
            packet.PacketHash,
            packet.ProviderFingerprint,
            result.Facts,
             result.Metadata,
             result.Expressions,
             TargetSpans: result.TargetSpans,
             Propositions: result.Propositions,
             Claims: result.Claims,
             Unresolved: result.Unresolved,
             Coverage: result.Coverage);
        return new AuthoringCandidateSet(packet.PacketId, snapshot.SnapshotId, snapshot.ContentHash, packet.PacketHash, packet.ProviderFingerprint, "succeeded", [candidate]);
    }

    public static AuthoringCandidateSet MaterializeAcceptedProjection(
        AuthoringCandidateSet candidateSet,
        IReadOnlyList<AuthoringDraftFact> facts,
        AuthoringDraftMetadata? metadata,
        IReadOnlyList<AuthoringDraftExpression> expressions)
    {
        ArgumentNullException.ThrowIfNull(candidateSet);
        if (candidateSet.Candidates.Count != 1)
            throw new InvalidOperationException("WB-AI-DRAFT-422: 只有单一当前候选可以物化已采纳投影。");

        var source = candidateSet.Candidates[0];
        var body = new JsonObject
        {
            ["candidate_id"] = source.CandidateId,
            ["facts"] = new JsonArray(facts.Select(AuthoringDraftRequestFactory.SerializeFact).ToArray()!),
            ["metadata"] = metadata is null ? null : AuthoringDraftRequestFactory.SerializeMetadata(metadata),
            ["expressions"] = new JsonArray(expressions.Select(AuthoringDraftRequestFactory.SerializeExpression).ToArray()!),
            ["source_snapshot_id"] = source.SourceSnapshotId,
            ["source_content_hash"] = source.SourceContentHash,
            ["packet_hash"] = source.PacketHash,
            ["provider_fingerprint"] = source.ProviderFingerprint,
            ["target_spans"] = new JsonArray((source.TargetSpans ?? []).Select(AuthoringDraftRequestFactory.SerializeTargetSpan).ToArray()!),
            ["propositions"] = new JsonArray((source.Propositions ?? []).Select(AuthoringDraftRequestFactory.SerializeProposition).ToArray()!),
            ["claims"] = new JsonArray((source.Claims ?? []).Select(AuthoringDraftRequestFactory.SerializeClaim).ToArray()!),
            ["unresolved"] = new JsonArray((source.Unresolved ?? []).Select(AuthoringDraftRequestFactory.SerializeUnresolved).ToArray()!),
            ["coverage"] = source.Coverage?.DeepClone()
        };
        var fingerprint = CanonicalJson.Hash(body);
        var materialized = source with
        {
            Fingerprint = fingerprint,
            Facts = facts,
            Metadata = metadata,
            Expressions = expressions
        };
        return candidateSet with { Candidates = [materialized] };
    }

    private static IReadOnlyList<string> NormalizeCandidateReasonCodes(IReadOnlyList<string>? values)
    {
        var normalized = (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return normalized.Length == 0 ? ["keep_whole_recommended"] : normalized;
    }

    private static void EnsureCandidateIdentities(IReadOnlyList<AuthoringCandidate> candidates)
    {
        EnsureUnique(candidates.Select(candidate => candidate.CandidateId), "候选");
        EnsureUnique(candidates.Select(candidate => candidate.Fingerprint), "候选指纹");
        EnsureUnique(candidates.SelectMany(candidate => candidate.Facts.Select(fact => fact.Id)), "事实");
        EnsureUnique(candidates.SelectMany(candidate => candidate.Expressions.Select(expression => expression.Id)), "身份表达");
    }

    private static void EnsureUnique(IEnumerable<string> values, string label)
    {
        var normalized = values.ToArray();
        if (normalized.Any(string.IsNullOrWhiteSpace)
            || normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
            throw new InvalidOperationException($"WB-AI-DRAFT-422: 候选集合包含重复或无效的{label}编号。");
    }

    private static IReadOnlyList<JsonObject> NormalizeCandidateSourceSpans(
        IReadOnlyList<JsonObject>? values,
        string sourceText,
        IReadOnlyList<AuthoringDraftFact> facts,
        IReadOnlyList<AuthoringDraftExpression> expressions)
    {
        if (values is { Count: > 0 })
            return values.Select(value => (JsonObject)value.DeepClone()).ToArray();

        var normalizedSource = AuthoringDraftRequestFactory.NormalizeSourceText(sourceText);
        var evidence = facts
            .SelectMany(fact => fact.EvidenceGroup is { Count: > 0 }
                ? fact.EvidenceGroup
                : fact.Evidence is null ? [] : [fact.Evidence])
            .Concat(expressions.Select(expression => expression.Evidence).Where(item => item is not null).Cast<AuthoringDraftEvidence>())
            .GroupBy(item => $"{item.ReferenceId}\u001f{item.Locator}\u001f{item.QuoteHash}", StringComparer.Ordinal)
            .Select(group => group.First());
        var spans = new List<JsonObject>();
        foreach (var item in evidence)
        {
            var span = new JsonObject
            {
                ["locator"] = item.Locator,
                ["quote"] = item.Quote,
                ["quote_hash"] = item.QuoteHash,
                ["verification_status"] = "unverified"
            };
            var start = normalizedSource.IndexOf(item.Quote, StringComparison.Ordinal);
            if (start >= 0)
            {
                span["start_utf16"] = start;
                span["end_utf16"] = start + item.Quote.Length;
                span["verification_status"] = item.Verified ? "verified" : "unverified";
            }
            spans.Add(span);
        }
        return spans;
    }

    public static AuthoringCandidateSet Invalidate(AuthoringCandidateSet candidates, string reason)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var message = string.IsNullOrWhiteSpace(reason) ? "generation_invalidated" : reason.Trim();
        return candidates with
        {
            Outcome = "invalidated",
            InvalidationReason = message,
            Candidates = candidates.Candidates.Select(candidate => candidate with
            {
                EvidenceCurrent = false,
                InvalidationReason = message,
                ReviewStatus = "stale",
                SegmentationReasonCodes = candidate.SegmentationReasonCodes ?? [],
                SourceSpans = candidate.SourceSpans ?? []
            }).ToArray()
        };
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string> values, int maximum)
        => values.Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(maximum)
            .ToArray();

    private static string RequireId(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl))
            throw new InvalidOperationException($"WB-AI-AUTHORING-422: {field} 无效。");
        return value.Trim();
    }

    private static string RequireHash(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException($"WB-AI-AUTHORING-422: {field} 必须是 SHA-256。");
        return value.ToLowerInvariant();
    }
}

internal static class AuthoringCandidateSetProjection
{
    public static JsonObject Project(AuthoringCandidateSet candidateSet)
    {
        ArgumentNullException.ThrowIfNull(candidateSet);
        return new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring-candidate-set-public.v1",
            ["generation_id"] = candidateSet.GenerationId,
            ["source_content_hash"] = candidateSet.SourceContentHash,
            ["outcome"] = candidateSet.Outcome,
            ["invalidation_reason"] = candidateSet.InvalidationReason,
            ["candidates"] = new JsonArray(candidateSet.Candidates.Select(candidate => new JsonObject
            {
                ["candidate_id"] = candidate.CandidateId,
                ["fingerprint"] = candidate.Fingerprint,
                ["facts"] = new JsonArray(candidate.Facts.Select(AuthoringDraftRequestFactory.SerializeFact).ToArray()!),
                ["metadata"] = candidate.Metadata is null ? null : AuthoringDraftRequestFactory.SerializeMetadata(candidate.Metadata),
                ["expressions"] = new JsonArray(candidate.Expressions.Select(AuthoringDraftRequestFactory.SerializeExpression).ToArray()!),
                ["review_status"] = candidate.ReviewStatus,
                ["evidence_current"] = candidate.EvidenceCurrent,
                 ["invalidation_reason"] = candidate.InvalidationReason,
                 ["segmentation_reason_codes"] = new JsonArray((candidate.SegmentationReasonCodes ?? []).Select(value => JsonValue.Create(value)).ToArray()!),
                 ["source_spans"] = new JsonArray((candidate.SourceSpans ?? []).Select(value => value.DeepClone()).ToArray()!),
                 ["target_spans"] = new JsonArray((candidate.TargetSpans ?? []).Select(AuthoringDraftRequestFactory.SerializeTargetSpan).ToArray()!),
                 ["propositions"] = new JsonArray((candidate.Propositions ?? []).Select(AuthoringDraftRequestFactory.SerializeProposition).ToArray()!),
                 ["claims"] = new JsonArray((candidate.Claims ?? []).Select(AuthoringDraftRequestFactory.SerializeClaim).ToArray()!),
                 ["unresolved"] = new JsonArray((candidate.Unresolved ?? []).Select(AuthoringDraftRequestFactory.SerializeUnresolved).ToArray()!),
                 ["coverage"] = candidate.Coverage?.DeepClone()
             }).ToArray()!)
        };
    }
}
