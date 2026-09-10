using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class AuthoringReviewProjection
{
    public static JsonObject Project(AuthoringCandidateSet candidateSet)
    {
        ArgumentNullException.ThrowIfNull(candidateSet);
        var duplicateIds = FindDuplicateCandidateIds(candidateSet.Candidates);
        var conflictIds = FindConflictCandidateIds(candidateSet.Candidates);
        var candidates = candidateSet.Candidates.Select(candidate => ProjectCandidate(candidate, duplicateIds, conflictIds)).ToArray();
        return new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring-review-projection.v1",
            ["generation_id"] = candidateSet.GenerationId,
            ["source_content_hash"] = candidateSet.SourceContentHash,
            ["packet_hash"] = candidateSet.PacketHash,
            ["counts"] = new JsonObject
            {
                ["total"] = candidates.Length,
                ["green"] = candidates.Count(item => item["risk"]?.GetValue<string>() == "green"),
                ["yellow"] = candidates.Count(item => item["risk"]?.GetValue<string>() == "yellow"),
                ["red"] = candidates.Count(item => item["risk"]?.GetValue<string>() == "red")
            },
            ["candidates"] = new JsonArray(candidates)
        };
    }

    private static JsonObject ProjectCandidate(
        AuthoringCandidate candidate,
        IReadOnlySet<string> duplicateIds,
        IReadOnlySet<string> conflictIds)
    {
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        var risk = "green";
        if (!candidate.EvidenceCurrent || candidate.ReviewStatus == "stale")
        {
            risk = "red";
            reasons.Add("stale_evidence");
        }
        if (candidate.Facts.Count == 0)
        {
            risk = "red";
            reasons.Add("missing_fact");
        }
        if (candidate.SourceSpans is { Count: > 0 }
            && candidate.SourceSpans.Any(span => span["verification_status"]?.GetValue<string>() != "verified"))
        {
            risk = "red";
            reasons.Add("unverified_source_span");
        }
        if (candidate.Unresolved?.Any(item => item.Blocking) == true)
        {
            risk = "red";
            reasons.Add("blocking_unresolved");
        }
        if (duplicateIds.Contains(candidate.CandidateId))
        {
            if (risk != "red") risk = "yellow";
            reasons.Add("duplicate_candidate");
        }
        if (conflictIds.Contains(candidate.CandidateId))
        {
            risk = "red";
            reasons.Add("candidate_conflict");
        }
        foreach (var fact in candidate.Facts)
        {
            if (fact.Evidence is null && fact.EvidenceGroup is not { Count: > 0 })
            {
                risk = "red";
                reasons.Add("missing_evidence");
            }
            if (fact.Evidence is { Verified: false }
                || fact.EvidenceGroup?.Any(item => !item.Verified) == true)
            {
                risk = "red";
                reasons.Add("unverified_evidence");
            }
            if (fact.Inferred || fact.Certainty is "uncertain" or "contested")
            {
                if (risk != "red") risk = "yellow";
                reasons.Add("inference_or_uncertainty");
            }
        }
        if (candidate.Expressions.Any(expression => expression.Inferred))
        {
            if (risk != "red") risk = "yellow";
            reasons.Add("inferred_expression");
        }

        var evidenceBindings = new List<(AuthoringDraftEvidence Evidence, string? FactId, string? ExpressionId)>();
        foreach (var fact in candidate.Facts)
            foreach (var item in EvidenceFor(fact))
                evidenceBindings.Add((item, fact.Id, null));
        foreach (var expression in candidate.Expressions)
            if (expression.Evidence is not null)
                evidenceBindings.Add((expression.Evidence, null, expression.Id));
        var evidence = evidenceBindings
            .GroupBy(item => $"{item.Evidence.ReferenceId}\u001f{item.Evidence.Locator}\u001f{item.Evidence.QuoteHash}", StringComparer.Ordinal)
            .Select(group =>
            {
                var evidence = AuthoringDraftRequestFactory.SerializeEvidence(group.First().Evidence);
                evidence["fact_ids"] = new JsonArray(group
                    .Select(item => item.FactId)
                    .Where(item => item is not null)
                    .Distinct(StringComparer.Ordinal)
                    .Select(item => JsonValue.Create(item))
                    .ToArray()!);
                evidence["expression_ids"] = new JsonArray(group
                    .Select(item => item.ExpressionId)
                    .Where(item => item is not null)
                    .Distinct(StringComparer.Ordinal)
                    .Select(item => JsonValue.Create(item))
                    .ToArray()!);
                return evidence;
            })
            .ToArray();
        return new JsonObject
        {
            ["candidate_id"] = candidate.CandidateId,
            ["fingerprint"] = candidate.Fingerprint,
            ["review_status"] = candidate.ReviewStatus,
            ["segmentation_reason_codes"] = new JsonArray((candidate.SegmentationReasonCodes ?? [])
                .Select(value => JsonValue.Create(value))
                .ToArray()!),
            ["reason_codes"] = new JsonArray((candidate.SegmentationReasonCodes ?? [])
                .Select(value => JsonValue.Create(value))
                .ToArray()!),
            ["source_spans"] = new JsonArray((candidate.SourceSpans ?? [])
                .Select(value => value.DeepClone())
                .ToArray()!),
            ["target_spans"] = new JsonArray((candidate.TargetSpans ?? [])
                .Select(AuthoringDraftRequestFactory.SerializeTargetSpan)
                .ToArray()!),
            ["propositions"] = new JsonArray((candidate.Propositions ?? [])
                .Select(AuthoringDraftRequestFactory.SerializeProposition)
                .ToArray()!),
            ["claims"] = new JsonArray((candidate.Claims ?? [])
                .Select(AuthoringDraftRequestFactory.SerializeClaim)
                .ToArray()!),
            ["unresolved"] = new JsonArray((candidate.Unresolved ?? [])
                .Select(AuthoringDraftRequestFactory.SerializeUnresolved)
                .ToArray()!),
            ["coverage"] = candidate.Coverage?.DeepClone(),
            ["risk"] = risk,
            ["risk_reasons"] = new JsonArray(reasons.OrderBy(value => value, StringComparer.Ordinal).Select(value => JsonValue.Create(value)).ToArray()!),
            ["evidence_count"] = evidence.Length,
            ["evidence"] = new JsonArray(evidence)
        };
    }

    private static IEnumerable<AuthoringDraftEvidence> EvidenceFor(AuthoringDraftFact fact)
        => fact.EvidenceGroup is { Count: > 0 }
            ? fact.EvidenceGroup
            : fact.Evidence is null ? [] : [fact.Evidence];

    private static IReadOnlySet<string> FindDuplicateCandidateIds(IReadOnlyList<AuthoringCandidate> candidates)
        => candidates
            .SelectMany(candidate => candidate.Facts.Select(fact => (Key: NormalizeFactText(fact.Text), Id: candidate.CandidateId)))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Where(group => group.Key.Length > 0 && group.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() > 1)
            .SelectMany(group => group.Select(item => item.Id))
            .ToHashSet(StringComparer.Ordinal);

    // 只有"同一句引文得出互相矛盾或互斥的断言"才算冲突：极性相反，或同一断言的成立时间范围互斥。
    // 同一句引文支撑同一对象的两个不同侧面（不同主语/谓语/对象）是参考簇鼓励的拆分方式，不再判红。
    private static IReadOnlySet<string> FindConflictCandidateIds(IReadOnlyList<AuthoringCandidate> candidates)
    {
        var conflicted = new HashSet<string>(StringComparer.Ordinal);
        var groups = candidates
            .SelectMany(candidate => EvidenceFor(candidate).Select(evidence => (Key: EvidenceKey(evidence), Candidate: candidate)))
            .GroupBy(item => item.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var members = group
                .Select(item => item.Candidate)
                .GroupBy(item => item.CandidateId, StringComparer.Ordinal)
                .Select(item => item.First())
                .ToArray();
            for (var left = 0; left < members.Length; left++)
            {
                for (var right = left + 1; right < members.Length; right++)
                {
                    if (!CandidatesConflict(members[left], members[right])) continue;
                    conflicted.Add(members[left].CandidateId);
                    conflicted.Add(members[right].CandidateId);
                }
            }
        }
        return conflicted;
    }

    private static bool CandidatesConflict(AuthoringCandidate left, AuthoringCandidate right)
    {
        // 双方都给出命题时按语义判断互斥；缺少命题结构时退回"首条事实文本不同"的保守规则。
        if (left.Propositions is { Count: > 0 } leftPropositions
            && right.Propositions is { Count: > 0 } rightPropositions)
            return leftPropositions.Any(item => rightPropositions.Any(other => PropositionsConflict(item, other)));
        return NormalizeFactText(left.Facts.FirstOrDefault()?.Text ?? string.Empty)
            != NormalizeFactText(right.Facts.FirstOrDefault()?.Text ?? string.Empty);
    }

    private static bool PropositionsConflict(AuthoringDraftProposition left, AuthoringDraftProposition right)
    {
        if (NormalizeFactText(left.Subject) != NormalizeFactText(right.Subject)
            || NormalizeFactText(left.Predicate) != NormalizeFactText(right.Predicate))
            return false;
        if (PolarityConflicts(left.Polarity, right.Polarity)) return true;
        return NormalizeFactText(left.ObjectValue) == NormalizeFactText(right.ObjectValue)
            && TimeScopeConflicts(left.TimeScope, right.TimeScope);
    }

    private static bool PolarityConflicts(string left, string right)
        => (left == "affirmed" && right == "negated") || (left == "negated" && right == "affirmed");

    private static bool TimeScopeConflicts(string left, string right)
        => left != right && IsExclusiveTimeScope(left) && IsExclusiveTimeScope(right);

    private static bool IsExclusiveTimeScope(string value)
        => value is "current" or "historical" or "future";

    private static IEnumerable<AuthoringDraftEvidence> EvidenceFor(AuthoringCandidate candidate)
        => candidate.Facts.SelectMany(EvidenceFor)
            .Concat(candidate.Expressions.Select(item => item.Evidence).Where(item => item is not null).Cast<AuthoringDraftEvidence>());

    private static string EvidenceKey(AuthoringDraftEvidence evidence)
        => $"{evidence.ReferenceId}\u001f{evidence.Locator}\u001f{evidence.QuoteHash}";

    private static string NormalizeFactText(string value)
        => AuthoringDraftRequestFactory.NormalizeSourceText(value).ToLowerInvariant();
}
