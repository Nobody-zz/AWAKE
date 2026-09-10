using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record AuthoringReviewApplication(
    AuthoringCandidateSet CandidateSet,
    JsonObject Decision);

internal static class AuthoringReviewDecisions
{
    private static readonly HashSet<string> Operations =
        ["keep_whole", "keep", "discard", "reorder", "merge", "split"];

    public static JsonObject Validate(
        AuthoringCandidateSet candidateSet,
        string operation,
        IReadOnlyList<string> candidateIds,
        IReadOnlyList<string>? orderedCandidateIds = null)
    {
        ArgumentNullException.ThrowIfNull(candidateSet);
        var normalizedOperation = operation.Trim().ToLowerInvariant();
        if (!Operations.Contains(normalizedOperation))
            throw new InvalidOperationException("WB-AI-REVIEW-422: 审查操作类型无效。");
        var candidates = candidateSet.Candidates.ToDictionary(item => item.CandidateId, StringComparer.Ordinal);
        var selected = candidateIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        if (selected.Length == 0)
            throw new InvalidOperationException("WB-AI-REVIEW-422: 至少选择一条候选。");
        if (selected.Any(id => !candidates.ContainsKey(id)))
            throw new InvalidOperationException("WB-AI-REVIEW-422: 审查操作包含不属于当前候选集的候选。");
        if (selected.Select(id => candidates[id]).Any(candidate => candidate.ReviewStatus is not "pending"))
            throw new InvalidOperationException("WB-AI-REVIEW-CANDIDATE-409: 只能操作当前仍处于 pending 的候选。");
        if (normalizedOperation is "merge" && selected.Length < 2)
            throw new InvalidOperationException("WB-AI-REVIEW-422: 合并至少需要两条候选。");
        if (normalizedOperation is "keep_whole" && selected.Length != candidates.Count)
            throw new InvalidOperationException("WB-AI-REVIEW-422: 保持整体必须覆盖当前候选集。");
        if (normalizedOperation == "reorder")
        {
            var order = orderedCandidateIds?.ToArray() ?? [];
            if (order.Length != candidates.Count
                || order.Distinct(StringComparer.Ordinal).Count() != candidates.Count
                || order.Any(id => !candidates.ContainsKey(id)))
                throw new InvalidOperationException("WB-AI-REVIEW-422: 重排必须包含当前候选集中的每条候选且不能重复。");
        }
        return new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring-review-decision.v1",
            ["operation"] = normalizedOperation,
            ["candidate_ids"] = new JsonArray(selected.Select(value => JsonValue.Create(value)).ToArray()!),
            ["ordered_candidate_ids"] = orderedCandidateIds is null
                ? null
                : new JsonArray(orderedCandidateIds.Select(value => JsonValue.Create(value)).ToArray()!),
            ["source_content_hash"] = candidateSet.SourceContentHash,
            ["packet_hash"] = candidateSet.PacketHash,
            ["candidate_set_generation_id"] = candidateSet.GenerationId,
            ["review_only"] = true,
            ["status"] = "proposed"
        };
    }

    public static JsonObject Materialize(
        AuthoringCandidateSet candidateSet,
        string operation,
        IReadOnlyList<string> candidateIds,
        IReadOnlyList<string>? orderedCandidateIds = null,
        IReadOnlyList<IReadOnlyList<string>>? splitGroups = null)
        => Apply(candidateSet, operation, candidateIds, orderedCandidateIds, splitGroups).Decision;

    public static AuthoringReviewApplication Apply(
        AuthoringCandidateSet candidateSet,
        string operation,
        IReadOnlyList<string> candidateIds,
        IReadOnlyList<string>? orderedCandidateIds = null,
        IReadOnlyList<IReadOnlyList<string>>? splitGroups = null)
    {
        var decision = Validate(candidateSet, operation, candidateIds, orderedCandidateIds);
        var candidates = candidateSet.Candidates.ToDictionary(item => item.CandidateId, StringComparer.Ordinal);
        var selected = candidateIds.Distinct(StringComparer.Ordinal).Select(id => candidates[id]).ToArray();
        var materialized = new List<AuthoringCandidate>();
        var normalizedOperation = operation.Trim().ToLowerInvariant();
        if (normalizedOperation == "merge")
        {
            materialized.Add(CreateDerivedCandidate(
                candidateSet,
                "merge",
                selected.SelectMany(item => item.Facts).DistinctBy(item => item.Id).ToArray(),
                selected.SelectMany(item => item.Expressions).DistinctBy(item => item.Id).ToArray(),
                 selected[0].Metadata,
                 selected.SelectMany(item => item.SegmentationReasonCodes ?? []).Append("user_merge"),
                 selected.SelectMany(item => item.SourceSpans ?? []).ToArray(),
                 selected.SelectMany(item => item.TargetSpans ?? [])
                     .GroupBy(item => item.Id, StringComparer.Ordinal)
                     .Select(group => group.First())
                     .ToArray(),
                 selected.SelectMany(item => item.Propositions ?? [])
                     .GroupBy(item => item.Id, StringComparer.Ordinal)
                     .Select(group => group.First())
                     .ToArray(),
                 selected.SelectMany(item => item.Claims ?? [])
                     .GroupBy(item => item.Id, StringComparer.Ordinal)
                     .Select(group => group.First())
                     .ToArray(),
                 selected.SelectMany(item => item.Unresolved ?? [])
                     .GroupBy(item => item.Id, StringComparer.Ordinal)
                     .Select(group => group.First())
                     .ToArray(),
                 selected.FirstOrDefault()?.Coverage));
        }
        else if (normalizedOperation == "split")
        {
            if (selected.Length != 1 || splitGroups is not { Count: >= 2 })
                throw new InvalidOperationException("WB-AI-REVIEW-422: 拆分必须选择一条候选并提供至少两个事实分组。");
            var facts = selected[0].Facts.ToDictionary(item => item.Id, StringComparer.Ordinal);
            var groups = splitGroups.Select(group => group.Distinct(StringComparer.Ordinal).ToArray()).ToArray();
            if (groups.Any(group => group.Length == 0)
                || groups.SelectMany(group => group).Distinct(StringComparer.Ordinal).Count() != facts.Count
                || groups.SelectMany(group => group).Any(id => !facts.ContainsKey(id)))
                throw new InvalidOperationException("WB-AI-REVIEW-422: 拆分分组必须完整覆盖当前候选事实且不能重复。");
            foreach (var group in groups)
            {
                var groupFacts = group.Select(id => facts[id]).ToArray();
                var groupExpressions = selected[0].Expressions
                    .Where(expression => expression.FactIds.All(group.Contains))
                    .ToArray();
                var attribution = AttributeSplit(
                    selected[0],
                    groupFacts,
                    groupExpressions);
                materialized.Add(CreateDerivedCandidate(
                    candidateSet,
                    "split",
                    groupFacts,
                    groupExpressions,
                     selected[0].Metadata,
                     (selected[0].SegmentationReasonCodes ?? []).Append("user_split"),
                     attribution.SourceSpans,
                     attribution.TargetSpans,
                     attribution.Propositions,
                     attribution.Claims,
                     attribution.Unresolved,
                     selected[0].Coverage));
            }
        }

        IReadOnlyList<AuthoringCandidate> updatedCandidates = normalizedOperation switch
        {
            "keep_whole" or "keep" => candidateSet.Candidates
                .Select(candidate => selected.Any(item => item.CandidateId == candidate.CandidateId)
                    ? candidate with { ReviewStatus = "kept" }
                    : candidate)
                .ToArray(),
            "discard" => candidateSet.Candidates
                .Select(candidate => selected.Any(item => item.CandidateId == candidate.CandidateId)
                    ? candidate with { ReviewStatus = "discarded" }
                    : candidate)
                .ToArray(),
            "reorder" => orderedCandidateIds!
                .Select(id => candidates[id])
                .ToArray(),
            "merge" or "split" => candidateSet.Candidates
                .Select(candidate => selected.Any(item => item.CandidateId == candidate.CandidateId)
                    ? candidate with { ReviewStatus = "superseded" }
                    : candidate)
                .Concat(materialized)
                .ToArray(),
            _ => candidateSet.Candidates.ToArray()
        };

        decision["materialized_candidates"] = new JsonArray(materialized.Select(SerializeCandidate).ToArray()!);
        decision["superseded_candidate_ids"] = new JsonArray(
            selected.Select(item => JsonValue.Create(item.CandidateId)).ToArray()!);
        return new AuthoringReviewApplication(
            candidateSet with { Candidates = updatedCandidates },
            decision);
    }

    private static AuthoringCandidate CreateDerivedCandidate(
        AuthoringCandidateSet candidateSet,
        string operation,
        IReadOnlyList<AuthoringDraftFact> facts,
        IReadOnlyList<AuthoringDraftExpression> expressions,
        AuthoringDraftMetadata? metadata,
        IEnumerable<string> reasonCodes,
        IReadOnlyList<JsonObject> sourceSpans,
        IReadOnlyList<AuthoringDraftTargetSpan> targetSpans,
        IReadOnlyList<AuthoringDraftProposition> propositions,
        IReadOnlyList<AuthoringDraftClaim> claims,
        IReadOnlyList<AuthoringDraftUnresolved> unresolved,
        JsonObject? coverage)
    {
        var reasons = reasonCodes.Distinct(StringComparer.Ordinal).ToArray();
        var body = new JsonObject
        {
            ["facts"] = new JsonArray(facts.Select(AuthoringDraftRequestFactory.SerializeFact).ToArray()!),
            ["metadata"] = metadata is null ? null : AuthoringDraftRequestFactory.SerializeMetadata(metadata),
            ["expressions"] = new JsonArray(expressions.Select(AuthoringDraftRequestFactory.SerializeExpression).ToArray()!),
            ["source_content_hash"] = candidateSet.SourceContentHash,
            ["packet_hash"] = candidateSet.PacketHash,
            ["provider_fingerprint"] = candidateSet.ProviderFingerprint,
            ["operation"] = operation,
            ["segmentation_reason_codes"] = new JsonArray(reasons.Select(value => JsonValue.Create(value)).ToArray()!),
            ["source_spans"] = new JsonArray(sourceSpans.Select(value => value.DeepClone()).ToArray()!),
            ["target_spans"] = new JsonArray(targetSpans
                .Select(AuthoringDraftRequestFactory.SerializeTargetSpan)
                .ToArray()!),
            ["propositions"] = new JsonArray(propositions
                .Select(AuthoringDraftRequestFactory.SerializeProposition)
                .ToArray()!),
            ["claims"] = new JsonArray(claims
                .Select(AuthoringDraftRequestFactory.SerializeClaim)
                .ToArray()!),
            ["unresolved"] = new JsonArray(unresolved
                .Select(AuthoringDraftRequestFactory.SerializeUnresolved)
                .ToArray()!),
            ["coverage"] = coverage?.DeepClone()
        };
        var fingerprint = CanonicalJson.Hash(body);
        return new AuthoringCandidate(
            "candidate." + fingerprint[..24],
            fingerprint,
            candidateSet.SourceSnapshotId,
            candidateSet.SourceContentHash,
            candidateSet.PacketHash,
            candidateSet.ProviderFingerprint,
            facts,
            metadata,
            expressions,
            SegmentationReasonCodes: reasons,
            SourceSpans: sourceSpans,
            TargetSpans: targetSpans,
            Propositions: propositions,
            Claims: claims,
            Unresolved: unresolved,
            Coverage: coverage);
    }

    private sealed record SplitAttribution(
        IReadOnlyList<JsonObject> SourceSpans,
        IReadOnlyList<AuthoringDraftTargetSpan> TargetSpans,
        IReadOnlyList<AuthoringDraftProposition> Propositions,
        IReadOnlyList<AuthoringDraftClaim> Claims,
        IReadOnlyList<AuthoringDraftUnresolved> Unresolved);

    private static SplitAttribution AttributeSplit(
        AuthoringCandidate candidate,
        IReadOnlyList<AuthoringDraftFact> facts,
        IReadOnlyList<AuthoringDraftExpression> expressions)
    {
        var evidence = facts
            .SelectMany(EvidenceFor)
            .Concat(expressions
                .Select(expression => expression.Evidence)
                .Where(item => item is not null)
                .Cast<AuthoringDraftEvidence>())
            .ToArray();
        var origins = evidence.Select(item => item.ReferenceId).ToHashSet(StringComparer.Ordinal);
        var quotes = evidence.Select(item => item.Quote).ToHashSet(StringComparer.Ordinal);
        var locators = evidence.Select(item => item.Locator).ToHashSet(StringComparer.Ordinal);
        var claimIds = (candidate.Claims ?? [])
            .Where(item => item.SourceOriginIds.Any(origins.Contains))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var propositionIds = (candidate.Claims ?? [])
            .Where(item => claimIds.Contains(item.Id))
            .Select(item => item.PropositionId)
            .ToHashSet(StringComparer.Ordinal);
        var targetSpans = (candidate.TargetSpans ?? [])
            .Where(item => item.SourceOriginIds.Any(origins.Contains)
                || item.ClaimIds.Any(claimIds.Contains))
            .ToArray();
        var claims = (candidate.Claims ?? [])
            .Where(item => claimIds.Contains(item.Id))
            .ToArray();
        var propositions = (candidate.Propositions ?? [])
            .Where(item => propositionIds.Contains(item.Id))
            .ToArray();
        var sourceSpans = new List<JsonObject>();
        var unresolved = (candidate.Unresolved ?? []).ToList();
        foreach (var sourceSpan in candidate.SourceSpans ?? [])
        {
            var copy = (JsonObject)sourceSpan.DeepClone();
            var explicitFactIds = StringValues(copy, "fact_ids");
            var explicitClaimIds = StringValues(copy, "claim_ids");
            var explicitOrigins = StringValues(copy, "source_origin_ids");
            var referenceId = copy["reference_id"]?.GetValue<string>();
            var locator = copy["locator"]?.GetValue<string>();
            var quote = copy["quote"]?.GetValue<string>();
            var hasExplicitBinding = explicitFactIds.Count > 0 || explicitClaimIds.Count > 0 || explicitOrigins.Count > 0;
            var relevant = explicitFactIds.Any(facts.Select(item => item.Id).Contains)
                || explicitClaimIds.Any(claimIds.Contains)
                || explicitOrigins.Any(origins.Contains)
                || (!hasExplicitBinding
                    && ((referenceId is not null && origins.Contains(referenceId))
                        || (locator is not null && locators.Contains(locator))
                        || (quote is not null && quotes.Contains(quote))));
            if (relevant)
            {
                TrimStringArray(copy, "fact_ids", facts.Select(item => item.Id).ToHashSet(StringComparer.Ordinal));
                TrimStringArray(copy, "claim_ids", claimIds);
                TrimStringArray(copy, "source_origin_ids", origins);
                sourceSpans.Add(copy);
                continue;
            }

            unresolved.Add(new AuthoringDraftUnresolved(
                "split-source-span-unbound-" + StableSuffix(sourceSpan),
                "source_span_attribution",
                "split",
                null,
                "error",
                true,
                "拆分后的 source span 无法归属于当前事实分组，已阻断该派生候选。",
                []));
        }

        if (candidate.SourceSpans is { Count: > 0 } && sourceSpans.Count == 0 && evidence.Length == 0)
            unresolved.Add(new AuthoringDraftUnresolved(
                "split-source-span-no-evidence",
                "source_span_attribution",
                "split",
                null,
                "error",
                true,
                "拆分候选缺少可用于归属 source span 的事实证据。",
                []));

        return new SplitAttribution(sourceSpans, targetSpans, propositions, claims, unresolved
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray());
    }

    private static IReadOnlyList<AuthoringDraftEvidence> EvidenceFor(AuthoringDraftFact fact)
        => fact.EvidenceGroup is { Count: > 0 }
            ? fact.EvidenceGroup
            : fact.Evidence is null ? [] : [fact.Evidence];

    private static IReadOnlyList<string> StringValues(JsonObject source, string key)
        => source[key] is JsonArray array
            ? array.OfType<JsonValue>()
                .Select(item => item.TryGetValue<string>(out var value) ? value : string.Empty)
                .Where(value => value.Length > 0)
                .ToArray()
            : [];

    private static void TrimStringArray(JsonObject source, string key, IReadOnlySet<string> allowed)
    {
        if (source[key] is not JsonArray) return;
        source[key] = new JsonArray(StringValues(source, key)
            .Where(allowed.Contains)
            .Select(value => JsonValue.Create(value))
            .ToArray()!);
    }

    private static string StableSuffix(JsonObject source)
        => Hashing.Sha256Text(CanonicalJson.Serialize(source))[..16];

    private static JsonObject SerializeCandidate(AuthoringCandidate candidate)
        => new()
        {
            ["candidate_id"] = candidate.CandidateId,
            ["fingerprint"] = candidate.Fingerprint,
            ["facts"] = new JsonArray(candidate.Facts.Select(AuthoringDraftRequestFactory.SerializeFact).ToArray()!),
            ["metadata"] = candidate.Metadata is null ? null : AuthoringDraftRequestFactory.SerializeMetadata(candidate.Metadata),
            ["expressions"] = new JsonArray(candidate.Expressions.Select(AuthoringDraftRequestFactory.SerializeExpression).ToArray()!),
            ["review_status"] = candidate.ReviewStatus,
            ["segmentation_reason_codes"] = new JsonArray((candidate.SegmentationReasonCodes ?? []).Select(value => JsonValue.Create(value)).ToArray()!),
            ["source_spans"] = new JsonArray((candidate.SourceSpans ?? []).Select(value => value.DeepClone()).ToArray()!),
            ["target_spans"] = new JsonArray((candidate.TargetSpans ?? []).Select(AuthoringDraftRequestFactory.SerializeTargetSpan).ToArray()!),
            ["propositions"] = new JsonArray((candidate.Propositions ?? []).Select(AuthoringDraftRequestFactory.SerializeProposition).ToArray()!),
            ["claims"] = new JsonArray((candidate.Claims ?? []).Select(AuthoringDraftRequestFactory.SerializeClaim).ToArray()!),
            ["unresolved"] = new JsonArray((candidate.Unresolved ?? []).Select(AuthoringDraftRequestFactory.SerializeUnresolved).ToArray()!),
            ["coverage"] = candidate.Coverage?.DeepClone()
        };
}
