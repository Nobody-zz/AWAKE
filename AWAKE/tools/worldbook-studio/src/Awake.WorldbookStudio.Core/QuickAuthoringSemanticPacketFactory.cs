using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class QuickAuthoringSemanticPacketFactory
{
    /// <summary>
    /// 候选数量上限（G3）。每个候选都是一份要人工逐条审阅的草稿，
    /// 超过这个数量说明资料本身就该先拆开，而不是让模型拆成十几条不可审阅的碎片。
    /// </summary>
    internal const int MaxQuickAuthoringCandidates = 12;

    public static QuickAuthoringSemanticPacket Create(
        AuthoringDraftRequest request,
        AuthoringDraftResult result,
        string providerFingerprint)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        if (request.Intent?.Mode != AuthoringDraftMode.QuickAuthoring
            || request.Stage != AuthoringDraftStage.Complete
            || request.GenerationPass != "pass_a")
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-400: 当前请求不是 Quick Authoring Pass A。");
        if (!result.ReviewOnly)
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-422: Pass A 结果必须保持 review_only=true。");
        if (!string.Equals(result.RequestHash, request.RequestHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(result.SourceContentHash, request.SourceContentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-CAS-409: Pass A 结果与当前请求不一致。");
        var normalizedProviderFingerprint = providerFingerprint.Trim().ToLowerInvariant();
        if (normalizedProviderFingerprint.Length != 64
            || normalizedProviderFingerprint.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-422: provider fingerprint 无效。");
        var propositions = result.Propositions
            ?? throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-422: Pass A 缺少 propositions。");
        var claims = result.Claims
            ?? throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-422: Pass A 缺少 claims。");
        var targetSpans = result.TargetSpans
            ?? throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-422: Pass A 缺少 target spans。");
        if (propositions.Count == 0 || claims.Count == 0 || targetSpans.Count == 0)
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-422: Pass A 缺少完整的 provisional semantic packet。");

        var unresolved = result.Unresolved ?? [];
        var coverage = result.Coverage?.DeepClone()?.AsObject();
        var body = new JsonObject
        {
            ["packet_id"] = "pending",
            ["request_hash"] = request.RequestHash,
            ["source_content_hash"] = request.SourceContentHash,
            ["provider_fingerprint"] = normalizedProviderFingerprint,
            ["prompt_revision"] = request.PromptRevision,
            ["normalization_revision"] = request.NormalizationRevision,
            ["facts"] = new JsonArray(result.Facts.Select(AuthoringDraftRequestFactory.SerializeFact).ToArray()!),
            ["expressions"] = new JsonArray(result.Expressions.Select(AuthoringDraftRequestFactory.SerializeExpression).ToArray()!),
            ["propositions"] = new JsonArray(propositions.Select(AuthoringDraftRequestFactory.SerializeProposition).ToArray()!),
            ["claims"] = new JsonArray(claims.Select(AuthoringDraftRequestFactory.SerializeClaim).ToArray()!),
            ["target_spans"] = new JsonArray(targetSpans.Select(AuthoringDraftRequestFactory.SerializeTargetSpan).ToArray()!),
            ["unresolved"] = new JsonArray(unresolved.Select(AuthoringDraftRequestFactory.SerializeUnresolved).ToArray()!),
            ["coverage"] = coverage?.DeepClone(),
            ["candidate_clusters"] = result.CandidateClusters is null
                ? null
                : new JsonArray(result.CandidateClusters.Select(AuthoringDraftRequestFactory.SerializeCandidateCluster).ToArray()!),
            ["status"] = "provisional"
        };
        var packetHash = CanonicalJson.Hash(body);
        var packetId = "semantic-packet." + packetHash[..24];
        body["packet_id"] = packetId;
        packetHash = CanonicalJson.Hash(body);

        return new QuickAuthoringSemanticPacket(
            packetId,
            request.RequestHash,
            request.SourceContentHash,
            normalizedProviderFingerprint,
            request.PromptRevision,
            request.NormalizationRevision,
            result.Facts,
            result.Expressions,
            propositions,
            claims,
            targetSpans,
            unresolved,
            coverage,
            packetHash,
            "provisional",
            result.CandidateClusters);
    }

    public static AuthoringDraftResult ValidateProjection(
        AuthoringDraftRequest request,
        QuickAuthoringSemanticPacket packet,
        AuthoringDraftResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(result);
        if (request.GenerationPass != "pass_b")
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-B-400: 当前请求不是 Quick Authoring Pass B。");
        if (!result.ReviewOnly)
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-B-422: Pass B 结果必须保持 review_only=true。");
        EnsurePacketIntegrity(packet);
        if (!string.Equals(packet.SourceContentHash, request.SourceContentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-B-CAS-409: semantic packet 与当前资料不一致。");
        if (!string.Equals(packet.RequestHash, request.SemanticPacket?.RequestHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-B-CAS-409: semantic packet 与 Pass A 请求不一致。");
        if (!string.Equals(packet.PromptRevision, request.PromptRevision, StringComparison.Ordinal)
            || !string.Equals(packet.NormalizationRevision, request.NormalizationRevision, StringComparison.Ordinal)
            || !string.Equals(packet.ProviderFingerprint, request.ProviderFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-B-CAS-409: semantic packet 版本或 Provider 身份已变化。");

        var resultPropositions = result.Propositions ?? [];
        var resultClaims = result.Claims ?? [];
        var resultTargetSpans = result.TargetSpans ?? [];
        EnsureExactGraph(
            packet.Propositions,
            resultPropositions,
            AuthoringDraftRequestFactory.SerializeProposition,
            "propositions");
        EnsureExactGraph(
            packet.Claims,
            resultClaims,
            AuthoringDraftRequestFactory.SerializeClaim,
            "claims");
        EnsureExactGraph(
            packet.TargetSpans,
            resultTargetSpans,
            AuthoringDraftRequestFactory.SerializeTargetSpan,
            "target_spans");

        foreach (var candidate in result.Candidates ?? [])
        {
            EnsureProjectionFacts(packet.Facts, candidate.Facts, candidate.Id);
            EnsureProjectionExpressions(packet.Expressions, candidate.Expressions, candidate.Id);
            EnsureSubset(packet.Propositions, candidate.Propositions ?? [], AuthoringDraftRequestFactory.SerializeProposition, "propositions", candidate.Id);
            EnsureSubset(packet.Claims, candidate.Claims ?? [], AuthoringDraftRequestFactory.SerializeClaim, "claims", candidate.Id);
            EnsureSubset(packet.TargetSpans, candidate.TargetSpans ?? [], AuthoringDraftRequestFactory.SerializeTargetSpan, "target_spans", candidate.Id);
        }

        var candidates = result.Candidates ?? [];
        EnsureCandidateBoundaries(packet, candidates);
        var packetCoverage = BuildPacketCoverageReport(packet, candidates);
        var coverage = result.Coverage?.DeepClone()?.AsObject() ?? new JsonObject();
        coverage["mode"] = "quick_authoring";
        coverage["pipeline"] = "pass_a_gate_a_pass_b_gate_b";
        coverage["pass_a_status"] = packet.Status;
        coverage["pass_b_status"] = "constrained_projection";
        coverage["candidate_boundary"] = packet.CandidateClusters is { Count: > 0 } ? "enforced" : "missing";
        coverage["semantic_packet_id"] = packet.PacketId;
        coverage["semantic_packet_hash"] = packet.PacketHash;
        coverage["semantic_packet_source_content_hash"] = packet.SourceContentHash;
        coverage["semantic_packet_prompt_revision"] = packet.PromptRevision;
        coverage["semantic_packet_normalization_revision"] = packet.NormalizationRevision;
        coverage["packet_coverage"] = packetCoverage;
        // 服务端权威计数：覆盖语义以候选并集为准，不用模型自报的 coverage 数字。
        var propositionSection = packetCoverage["propositions"] as JsonObject;
        var frozenPropositions = propositionSection?["total"]?.GetValue<int>() ?? 0;
        var uncoveredPropositions = propositionSection?["uncovered"]?.GetValue<int>() ?? 0;
        coverage["source_proposition_count"] = frozenPropositions;
        coverage["supported_proposition_count"] = frozenPropositions - uncoveredPropositions;
        coverage["unsupported_proposition_count"] = uncoveredPropositions;
        if (!IsCompletePacketCoverage(packetCoverage))
            throw new InvalidOperationException(DescribeIncompleteCoverage(packetCoverage));
        return result with { Coverage = coverage, SemanticPacket = packet };
    }

    /// <summary>
    /// G2：Pass B 只能「受限投影」，所以服务端必须同时保证「候选并集覆盖冻结的 semantic packet」。
    /// 只做子集校验时，20 条命题只投影 1 条也能过闸门 —— 那是最危险的一类失败：
    /// 看起来生成了，其实丢了一半内容，作者无从察觉。
    /// </summary>
    internal static JsonObject BuildPacketCoverageReport(
        QuickAuthoringSemanticPacket packet,
        IReadOnlyList<AuthoringDraftCandidatePayload> candidates)
    {
        var candidatesList = candidates;
        var deliveredPropositions = new HashSet<string>(StringComparer.Ordinal);
        var deliveredClaims = new HashSet<string>(StringComparer.Ordinal);
        var deliveredTargetSpans = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidatesList)
        {
            deliveredPropositions.UnionWith((candidate.Propositions ?? []).Select(item => item.Id));
            deliveredClaims.UnionWith((candidate.Claims ?? []).Select(item => item.Id));
            deliveredTargetSpans.UnionWith((candidate.TargetSpans ?? []).Select(item => item.Id));
        }

        // 有意排除：Pass A 把某个 target span 标成 drop 时，它本来就不该进任何候选。
        var droppedClaimIds = packet.TargetSpans
            .Where(span => string.Equals(span.Operation, "drop", StringComparison.Ordinal))
            .SelectMany(span => span.ClaimIds)
            .ToHashSet(StringComparer.Ordinal);
        var droppedTargetSpanIds = packet.TargetSpans
            .Where(span => string.Equals(span.Operation, "drop", StringComparison.Ordinal))
            .Select(span => span.Id)
            .ToHashSet(StringComparer.Ordinal);
        var droppedPropositionIds = packet.Claims
            .Where(claim => droppedClaimIds.Contains(claim.Id))
            .Select(claim => claim.PropositionId)
            .ToHashSet(StringComparer.Ordinal);

        var report = new JsonObject
        {
            ["candidate_count"] = candidatesList.Count,
            ["propositions"] = BuildCoverageSection(
                packet.Propositions.Select(item => item.Id),
                deliveredPropositions,
                droppedPropositionIds),
            ["claims"] = BuildCoverageSection(
                packet.Claims.Select(item => item.Id),
                deliveredClaims,
                droppedClaimIds),
            ["target_spans"] = BuildCoverageSection(
                packet.TargetSpans.Select(item => item.Id),
                deliveredTargetSpans,
                droppedTargetSpanIds)
        };
        report["status"] = new[] { "propositions", "claims", "target_spans" }.All(field =>
            (report[field] as JsonObject)?["uncovered"]?.GetValue<int>() == 0) ? "complete" : "incomplete";
        return report;
    }

    private static JsonObject BuildCoverageSection(
        IEnumerable<string> expected,
        IReadOnlySet<string> delivered,
        IReadOnlySet<string> deliberatelyDropped)
    {
        var total = 0;
        var uncovered = new List<string>();
        var dropped = new List<string>();
        foreach (var id in expected)
        {
            total++;
            if (delivered.Contains(id)) continue;
            if (deliberatelyDropped.Contains(id)) dropped.Add(id);
            else uncovered.Add(id);
        }
        return new JsonObject
        {
            ["total"] = total,
            ["covered"] = total - uncovered.Count - dropped.Count,
            ["uncovered"] = uncovered.Count,
            ["uncovered_ids"] = new JsonArray(uncovered.Select(value => JsonValue.Create(value)).ToArray()!),
            ["deliberately_dropped"] = dropped.Count,
            ["deliberately_dropped_ids"] = new JsonArray(dropped.Select(value => JsonValue.Create(value)).ToArray()!)
        };
    }

    internal static bool IsCompletePacketCoverage(JsonObject report)
        => report["status"]?.GetValue<string>() == "complete";

    internal static string DescribeIncompleteCoverage(JsonObject report)
    {
        var parts = new List<string>();
        foreach (var (field, label) in new[]
        {
            ("propositions", "命题"),
            ("claims", "claim"),
            ("target_spans", "target span")
        })
        {
            if (report[field] is not JsonObject section) continue;
            var uncovered = section["uncovered"]?.GetValue<int>() ?? 0;
            if (uncovered == 0) continue;
            var total = section["total"]?.GetValue<int>() ?? 0;
            var samples = (section["uncovered_ids"] as JsonArray ?? [])
                .OfType<JsonValue>()
                .Select(value => value.GetValue<string>())
                .Take(3);
            parts.Add($"{label} 未覆盖 {uncovered}/{total}（示例：{string.Join("、", samples)}）");
        }
        return "WB-AI-DRAFT-PASS-B-422: Pass B 没有覆盖 Pass A 语义包的全部内容："
            + string.Join("；", parts)
            + "。请重新生成，或先把参考资料拆成更小的几份再重试。";
    }

    /// <summary>
    /// G3：Pass A 给出的候选边界就是唯一分组依据。缺失时降级放行（既有 Worker 不产出该字段），
    /// 只把 candidate_boundary=missing 记进 coverage；存在时每个候选取数必须落在同一条边界内，
    /// 边界不得被合并、拆分或重复使用，候选数量不得超过上限。
    /// </summary>
    private static void EnsureCandidateBoundaries(
        QuickAuthoringSemanticPacket packet,
        IReadOnlyList<AuthoringDraftCandidatePayload> candidates)
    {
        var clusters = (packet.CandidateClusters ?? [])
            .Where(cluster => cluster.PropositionIds.Count > 0 || cluster.ClaimIds.Count > 0 || cluster.TargetSpanIds.Count > 0)
            .ToArray();
        if (clusters.Length == 0) return;
        if (clusters.Length > MaxQuickAuthoringCandidates)
            throw new InvalidOperationException(
                $"WB-AI-DRAFT-PASS-A-422: Pass A 给出 {clusters.Length} 个候选边界，超过上限 {MaxQuickAuthoringCandidates}；"
                + "请先把参考资料拆成更小的几份，避免产出无法逐条审阅的碎片。");

        var clusterPropositions = clusters
            .Select(cluster => cluster.PropositionIds.ToHashSet(StringComparer.Ordinal))
            .ToArray();
        var clusterClaims = clusters
            .Select(cluster => cluster.ClaimIds.ToHashSet(StringComparer.Ordinal))
            .ToArray();
        var clusterSpans = clusters
            .Select(cluster => cluster.TargetSpanIds.ToHashSet(StringComparer.Ordinal))
            .ToArray();

        var used = new HashSet<int>();
        foreach (var candidate in candidates)
        {
            var propositions = (candidate.Propositions ?? []).Select(item => item.Id).ToArray();
            var claims = (candidate.Claims ?? []).Select(item => item.Id).ToArray();
            var spans = (candidate.TargetSpans ?? []).Select(item => item.Id).ToArray();
            var matches = new List<int>();
            var overlapping = new List<int>();
            for (var index = 0; index < clusters.Length; index++)
            {
                if (propositions.Any(clusterPropositions[index].Contains)
                    || claims.Any(clusterClaims[index].Contains)
                    || spans.Any(clusterSpans[index].Contains))
                    overlapping.Add(index);
                if (propositions.All(clusterPropositions[index].Contains)
                    && claims.All(clusterClaims[index].Contains)
                    && spans.All(clusterSpans[index].Contains))
                    matches.Add(index);
            }
            if (matches.Count != 1)
                throw new InvalidOperationException(
                    overlapping.Count > 1
                        ? $"WB-AI-DRAFT-PASS-B-422: 候选 {candidate.Id} 横跨多条 Pass A 候选边界（{string.Join("、", overlapping.Select(index => clusters[index].Id).Take(3))}），Pass B 不得跨边界合并候选。"
                        : $"WB-AI-DRAFT-PASS-B-422: 候选 {candidate.Id} 的内容不属于任何一条 Pass A 候选边界，Pass B 不得自行分组。");
            if (!used.Add(matches[0]))
                throw new InvalidOperationException(
                    $"WB-AI-DRAFT-PASS-B-422: 候选 {candidate.Id} 与另一条候选使用了同一条 Pass A 候选边界，Pass B 不得拆分边界。");
        }

        if (used.Count != clusters.Length)
            throw new InvalidOperationException(
                $"WB-AI-DRAFT-PASS-B-422: Pass A 给出 {clusters.Length} 个候选边界，但只有 {used.Count} 个被候选使用；Pass B 不得自行合并或丢弃候选边界。");
    }

    private static void EnsureProjectionFacts(
        IReadOnlyList<AuthoringDraftFact> packetFacts,
        IReadOnlyList<AuthoringDraftFact> projectionFacts,
        string candidateId)
    {
        var known = packetFacts.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var fact in projectionFacts)
        {
            if (!known.TryGetValue(fact.Id, out var source))
                throw NewProjectionError("fact", fact.Id, candidateId);
            if (!string.Equals(source.Text, fact.Text, StringComparison.Ordinal)
                || !string.Equals(source.Kind, fact.Kind, StringComparison.Ordinal)
                || !string.Equals(source.Certainty, fact.Certainty, StringComparison.Ordinal)
                || source.Inferred != fact.Inferred
                || !EvidenceEqual(source.Evidence, fact.Evidence)
                || !EvidenceGroupEqual(source.EvidenceGroup, fact.EvidenceGroup))
                throw NewProjectionError("fact", fact.Id, candidateId);
        }
    }

    private static void EnsurePacketIntegrity(QuickAuthoringSemanticPacket packet)
    {
        var body = new JsonObject
        {
            ["packet_id"] = packet.PacketId,
            ["request_hash"] = packet.RequestHash,
            ["source_content_hash"] = packet.SourceContentHash,
            ["provider_fingerprint"] = packet.ProviderFingerprint.Trim().ToLowerInvariant(),
            ["prompt_revision"] = packet.PromptRevision,
            ["normalization_revision"] = packet.NormalizationRevision,
            ["facts"] = new JsonArray(packet.Facts.Select(AuthoringDraftRequestFactory.SerializeFact).ToArray()!),
            ["expressions"] = new JsonArray(packet.Expressions.Select(AuthoringDraftRequestFactory.SerializeExpression).ToArray()!),
            ["propositions"] = new JsonArray(packet.Propositions.Select(AuthoringDraftRequestFactory.SerializeProposition).ToArray()!),
            ["claims"] = new JsonArray(packet.Claims.Select(AuthoringDraftRequestFactory.SerializeClaim).ToArray()!),
            ["target_spans"] = new JsonArray(packet.TargetSpans.Select(AuthoringDraftRequestFactory.SerializeTargetSpan).ToArray()!),
            ["unresolved"] = new JsonArray(packet.Unresolved.Select(AuthoringDraftRequestFactory.SerializeUnresolved).ToArray()!),
            ["coverage"] = packet.Coverage?.DeepClone(),
            ["candidate_clusters"] = packet.CandidateClusters is null
                ? null
                : new JsonArray(packet.CandidateClusters.Select(AuthoringDraftRequestFactory.SerializeCandidateCluster).ToArray()!),
            ["status"] = packet.Status
        };
        if (!string.Equals(CanonicalJson.Hash(body), packet.PacketHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-B-CAS-409: semantic packet hash 校验失败。");
    }

    private static void EnsureProjectionExpressions(
        IReadOnlyList<AuthoringDraftExpression> packetExpressions,
        IReadOnlyList<AuthoringDraftExpression> projectionExpressions,
        string candidateId)
    {
        var known = packetExpressions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var expression in projectionExpressions)
        {
            if (!known.TryGetValue(expression.Id, out var source))
                throw NewProjectionError("expression", expression.Id, candidateId);
            if (!string.Equals(source.Perspective, expression.Perspective, StringComparison.Ordinal)
                || !string.Equals(source.Layer, expression.Layer, StringComparison.Ordinal)
                || source.Inferred != expression.Inferred
                || !source.ProfileIds.SequenceEqual(expression.ProfileIds, StringComparer.Ordinal)
                || !source.FactIds.SequenceEqual(expression.FactIds, StringComparer.Ordinal)
                || !EvidenceEqual(source.Evidence, expression.Evidence))
                throw NewProjectionError("expression", expression.Id, candidateId);
        }
    }

    private static void EnsureExactGraph<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T>? actual,
        Func<T, JsonObject> serialize,
        string field)
    {
        if (actual is null || expected.Count != actual.Count)
            throw new InvalidOperationException($"WB-AI-DRAFT-PASS-B-422: Pass B {field} 必须完整复用 Pass A semantic packet。");
        var expectedJson = expected.Select(serialize).Select(CanonicalJson.Serialize).ToArray();
        var actualJson = actual.Select(serialize).Select(CanonicalJson.Serialize).ToArray();
        if (!expectedJson.SequenceEqual(actualJson, StringComparer.Ordinal))
            throw new InvalidOperationException($"WB-AI-DRAFT-PASS-B-422: Pass B {field} 修改了冻结的 semantic packet。");
    }

    private static void EnsureSubset<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual,
        Func<T, JsonObject> serialize,
        string field,
        string candidateId)
    {
        var known = expected.Select(serialize).Select(CanonicalJson.Serialize).ToHashSet(StringComparer.Ordinal);
        if (actual.Select(serialize).Select(CanonicalJson.Serialize).Any(value => !known.Contains(value)))
            throw new InvalidOperationException($"WB-AI-DRAFT-PASS-B-422: candidate {candidateId} 新增了 semantic packet 中不存在的 {field}。");
    }

    private static InvalidOperationException NewProjectionError(string field, string id, string candidateId)
        => new($"WB-AI-DRAFT-PASS-B-422: candidate {candidateId} 的 {field} {id} 不是冻结 semantic packet 的受限投影。");

    private static bool EvidenceEqual(AuthoringDraftEvidence? left, AuthoringDraftEvidence? right)
        => left is null && right is null
            || left is not null
            && right is not null
            && left.ReferenceId == right.ReferenceId
            && left.Locator == right.Locator
            && left.Quote == right.Quote
            && left.QuoteHash.Equals(right.QuoteHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                left.LocatorObject is null ? null : CanonicalJson.Serialize(left.LocatorObject),
                right.LocatorObject is null ? null : CanonicalJson.Serialize(right.LocatorObject),
                StringComparison.Ordinal);

    private static bool EvidenceGroupEqual(
        IReadOnlyList<AuthoringDraftEvidence>? left,
        IReadOnlyList<AuthoringDraftEvidence>? right)
        => (left ?? []).Count == (right ?? []).Count
            && (left ?? []).Zip(right ?? [], EvidenceEqual).All(value => value);
}
