using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class AuthoringDraftResponseNormalizer
{
    private const string ResultSchemaVersion = "worldbook.authoring-draft.result.v1";

    private static readonly HashSet<string> ResultKeys =
    [
        "schema_version", "stage", "request_hash", "source_content_hash", "review_only",
        "facts", "metadata", "expressions", "candidates", "warnings", "unresolved", "coverage",
        "target_spans", "propositions", "claims",
        "clusters",
        "worker_prompt_revision", "worker_normalization_revision",
        "provider_id", "draft_id", "perspectives"
    ];

    private static readonly HashSet<string> ClusterKeys =
    [
        "id", "title", "proposition_ids", "claim_ids", "target_span_ids"
    ];

    private readonly record struct TruncationSignal(
        string Field,
        int OriginalCharacters,
        int KeptCharacters);

    private const int WarningLimit = 64;

    [ThreadStatic]
    private static List<TruncationSignal>? _truncations;

    [ThreadStatic]
    private static int _droppedWarnings;

    private static readonly HashSet<string> FactKeys =
    [
        "id", "fact_id", "kind", "type", "text", "content", "certainty", "confidence",
        "inferred", "evidence", "source_span", "source", "quote", "reference_id", "locator",
        "evidence_group", "review_status"
    ];

    private static readonly HashSet<string> MetadataKeys =
    [
        "title", "title_text", "summary", "description", "domain", "category", "subdomain",
        "subcategory", "related_domains", "note", "notes", "era", "entity_ids"
    ];

    private static readonly HashSet<string> ExpressionKeys =
    [
        "id", "expression_id", "perspective", "identity", "layer", "detail", "detail_level",
        "text", "content", "profile_ids", "profile_id", "fact_ids", "fact_id", "inferred",
        "evidence", "source_span", "source", "quote", "review_status"
    ];

    private static readonly HashSet<string> CandidateKeys =
    [
        "id", "candidate_id", "facts", "metadata", "expressions", "review_status",
        "segmentation_reason_codes", "reason_codes", "source_spans", "source_span", "target_spans",
        "propositions", "claims", "unresolved", "coverage"
    ];

    private static readonly HashSet<string> SegmentationReasonCodes =
    [
        "topic_boundary", "entity_focus_changed", "time_period_changed",
        "causal_chain_changed", "perspective_changed", "evidence_break",
        "length_guard", "keep_whole_recommended"
    ];

    private static readonly HashSet<string> EvidenceKeys =
    [
        "reference_id", "source_id", "locator", "source_locator", "quote", "source_span", "source",
        "quote_hash", "locator_object", "evidence_verified"
    ];

    public static JsonObject Normalize(JsonNode node, AuthoringDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = node as JsonObject ?? throw Format("结果必须是 JSON 对象。");
        EnsureKeys(source, ResultKeys, "结果");
        ValidateWorkerPromptRevision(source, request);
        _truncations = [];
        _droppedWarnings = 0;

        var stage = OptionalString(source, "stage") ?? AuthoringDraftStageNames.ToWire(request.Stage);
        if (!string.Equals(stage, AuthoringDraftStageNames.ToWire(request.Stage), StringComparison.OrdinalIgnoreCase))
            throw Format("结果 stage 与当前请求不一致。");

        var schemaVersion = OptionalString(source, "schema_version") ?? ResultSchemaVersion;
        if (!string.Equals(schemaVersion, ResultSchemaVersion, StringComparison.Ordinal))
            throw Format("结果版本无效。");
        var strictQuick = request.Stage == AuthoringDraftStage.Complete
            && request.Intent?.Mode == AuthoringDraftMode.QuickAuthoring;
        if (strictQuick)
        {
            ValidateStrictQuickEnvelope(source, request);
            if (request.Intent!.RequestedPerspectives.Count == 0)
            {
                if (source["expressions"] is JsonArray topLevelExpressions && topLevelExpressions.Count > 0)
                    throw new InvalidOperationException("WB-AI-DRAFT-EXPRESSIONS-422: 未请求身份视角时，结果不得包含身份表达。");
                if (source["candidates"] is JsonArray candidateArray
                    && candidateArray.OfType<JsonObject>().Any(candidate => candidate["expressions"] is JsonArray candidateExpressions && candidateExpressions.Count > 0))
                    throw new InvalidOperationException("WB-AI-DRAFT-EXPRESSIONS-422: 未请求身份视角时，候选不得包含身份表达。");
            }
        }

        var warnings = NormalizeWarnings(source["warnings"]);
        var hasCandidates = source["candidates"] is not null;
        var facts = NormalizeFacts(
            source["facts"],
            request,
            request.Stage is (AuthoringDraftStage.Facts or AuthoringDraftStage.Complete) && !hasCandidates,
            warnings,
            strictQuick);
        var metadata = NormalizeMetadata(source["metadata"]);
        var expressions = NormalizeExpressions(source["expressions"], request, warnings, strictQuick);
        var candidates = NormalizeCandidates(source["candidates"], request, warnings, strictQuick);
        var unresolved = NormalizeUnresolved(source["unresolved"]);
        var coverage = NormalizeCoverage(source["coverage"]);
        var targetSpans = NormalizeTargetSpans(source["target_spans"]);
        var propositions = NormalizePropositions(source["propositions"]);
        var claims = NormalizeClaims(source["claims"]);
        var clusters = NormalizeCandidateClusters(source["clusters"], request);
        coverage = EnsureQuickSemanticCompleteness(
            request,
            candidates,
            facts.Count > 0 || metadata is not null || expressions.Count > 0,
            propositions,
            claims,
            targetSpans,
            unresolved,
            warnings,
            coverage);
        coverage = EnsureCoverageConsistency(
            propositions,
            claims,
            targetSpans,
            unresolved,
            warnings,
            coverage);
        EnforceMustNotInvent(
            request,
            candidates,
            facts,
            expressions,
            propositions,
            claims,
            unresolved,
            warnings);
        ValidateSemanticSourceBoundaries(
            request,
            candidates,
            facts,
            expressions,
            propositions,
            claims,
            targetSpans,
            unresolved,
            warnings);
        if (strictQuick)
            ValidateStrictQuickOriginBindings(request, facts, expressions, propositions, claims, targetSpans, candidates);
        coverage = ReportNormalizationSignals(request, warnings, coverage);

        return new JsonObject
        {
            ["schema_version"] = ResultSchemaVersion,
            ["stage"] = stage.ToLowerInvariant(),
            ["request_hash"] = NormalizeHash(source["request_hash"], request.RequestHash, "request_hash"),
            ["source_content_hash"] = NormalizeHash(source["source_content_hash"], request.SourceContentHash, "source_content_hash"),
            ["review_only"] = NormalizeBoolean(source["review_only"], true, "review_only"),
            ["facts"] = facts,
            ["metadata"] = metadata,
            ["expressions"] = expressions,
            ["candidates"] = candidates,
            ["warnings"] = warnings,
            ["unresolved"] = unresolved,
            ["coverage"] = coverage,
            ["target_spans"] = targetSpans,
            ["propositions"] = propositions,
            ["claims"] = claims,
            ["clusters"] = clusters
        };
    }

    private static void ValidateStrictQuickEnvelope(JsonObject source, AuthoringDraftRequest request)
    {
        var required = new[]
        {
            "schema_version", "stage", "request_hash", "source_content_hash", "review_only",
            "facts", "metadata", "expressions", "candidates", "warnings", "unresolved",
            "coverage", "target_spans", "propositions", "claims"
        };
        foreach (var field in required)
        {
            if (!source.ContainsKey(field))
                throw Format($"Quick Authoring complete 结果缺少严格字段：{field}。");
        }

        if (!string.Equals(source["stage"]?.GetValue<string>(), "complete", StringComparison.OrdinalIgnoreCase))
            throw Format("Quick Authoring complete 结果 stage 无效。");
        if (!string.Equals(source["request_hash"]?.GetValue<string>(), request.RequestHash, StringComparison.OrdinalIgnoreCase))
            throw Format("Quick Authoring complete 结果 request_hash 与请求不一致。");
        if (!string.Equals(source["source_content_hash"]?.GetValue<string>(), request.SourceContentHash, StringComparison.OrdinalIgnoreCase))
            throw Format("Quick Authoring complete 结果 source_content_hash 与请求不一致。");
        if (source["review_only"]?.GetValue<bool>() != true)
            throw Format("Quick Authoring complete 结果必须保持 review_only=true。");
        if (request.GenerationPass == "pass_a")
        {
            if (source["candidates"] is JsonArray passACandidates && passACandidates.Count > 0)
                throw Format("Quick Authoring Pass A 不得直接生成最终候选。");
            if (source["metadata"] is not null
                || source["expressions"] is not JsonArray passAExpressions
                || passAExpressions.Count > 0)
                throw Format("Quick Authoring Pass A 只能生成直接来源语义包。");
            ValidateStrictReviewStatuses(source["facts"], "facts", "review_status");
            ValidateStrictReviewStatuses(source["claims"], "claims", "review_status");
            ValidateStrictReviewStatuses(source["target_spans"], "target_spans", "review_state");
            return;
        }

        if (source["candidates"] is not JsonArray)
            throw Format("Quick Authoring Pass B 结果 candidates 必须是数组。");

        foreach (var candidate in source["candidates"]!.AsArray().OfType<JsonObject>())
        {
            foreach (var field in new[]
            {
                "id", "facts", "metadata", "expressions", "review_status",
                "segmentation_reason_codes", "source_spans", "target_spans",
                "propositions", "claims", "unresolved", "coverage"
            })
            {
                if (!candidate.ContainsKey(field))
                    throw Format($"Quick Authoring candidate 缺少严格字段：{field}。");
            }
            if (!string.Equals(candidate["review_status"]?.GetValue<string>(), "pending", StringComparison.Ordinal))
                throw Format("Quick Authoring candidate 必须保持 review_status=pending。");
            ValidateStrictReviewStatuses(candidate["facts"], "facts", "review_status");
            ValidateStrictReviewStatuses(candidate["expressions"], "expressions", "review_status");
            ValidateStrictReviewStatuses(candidate["claims"], "claims", "review_status");
            ValidateStrictReviewStatuses(candidate["target_spans"], "target_spans", "review_state");
        }
    }

    /// <summary>
    /// G6：本机 Worker 可以回执它执行的提示词/归一化版本。回执缺失不作阻断（既有 Worker 不回执），
    /// 回执存在但与 Studio 当前版本不一致时阻断：否则内置提示词的整轮清洗无法被验证，
    /// 会出现「以为在跑 v5 合同、实际是旧合同」的静默偏差。
    /// </summary>
    private static void ValidateWorkerPromptRevision(JsonObject source, AuthoringDraftRequest request)
    {
        if (!string.Equals(request.ProviderId, "local", StringComparison.OrdinalIgnoreCase)) return;
        var promptRevision = OptionalString(source, "worker_prompt_revision");
        var normalizationRevision = OptionalString(source, "worker_normalization_revision");
        if (promptRevision is null && normalizationRevision is null) return;
        if (promptRevision is not null
            && !string.Equals(promptRevision, request.PromptRevision, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"WB-AI-DRAFT-PROMPT-409: 本机 Worker 回执的提示词版本为 {promptRevision}，与 Studio 当前版本 {request.PromptRevision} 不一致。");
        if (normalizationRevision is not null
            && !string.Equals(normalizationRevision, request.NormalizationRevision, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"WB-AI-DRAFT-PROMPT-409: 本机 Worker 回执的归一化版本为 {normalizationRevision}，与 Studio 当前版本 {request.NormalizationRevision} 不一致。");
    }

    /// <summary>
    /// G3：Pass A 的候选边界。缺失时降级放行（既有 Worker 与既有回归样例不产出该字段），
    /// 但会在 coverage 中留下候选边界缺失信号，供 Pass B 的边界约束判定。
    /// </summary>
    private static JsonArray NormalizeCandidateClusters(JsonNode? node, AuthoringDraftRequest request)
    {
        if (node is null) return [];
        var array = node as JsonArray ?? throw Format("clusters 必须是对象数组。");
        if (array.Count > 64) throw Format("clusters 数量超限。");
        var result = new JsonArray();
        for (var index = 0; index < array.Count; index++)
        {
            var cluster = array[index] as JsonObject ?? throw Format("cluster 项目必须是对象。");
            EnsureKeys(cluster, ClusterKeys, "cluster");
            var id = OptionalString(cluster, "id");
            if (string.IsNullOrWhiteSpace(id)) throw Format("cluster 必须包含 id。");
            result.Add(new JsonObject
            {
                ["id"] = Limit(id, 128, "cluster.id"),
                ["title"] = cluster["title"] is null ? null : Limit(OptionalString(cluster, "title") ?? string.Empty, 512, "cluster.title"),
                ["proposition_ids"] = NormalizeStringList(cluster["proposition_ids"], "cluster.proposition_ids", 128),
                ["claim_ids"] = NormalizeStringList(cluster["claim_ids"], "cluster.claim_ids", 128),
                ["target_span_ids"] = NormalizeStringList(cluster["target_span_ids"], "cluster.target_span_ids", 128)
            });
        }
        return result;
    }

    /// <summary>
    /// N3：归一化阶段的超长截断不再静默。结构化信号进 coverage，同时在 warnings 里说明；
    /// 选择告警而非阻断——截断后内容仍在契约上限内可用，阻断会把整份结果废掉，
    /// 与「宁可少而准」相比更像是惩罚用户；但作者必须能看见发生了截断。
    /// </summary>
    private static JsonObject? ReportNormalizationSignals(
        AuthoringDraftRequest request,
        JsonArray warnings,
        JsonObject? coverage)
    {
        var signals = _truncations;
        _truncations = null;
        var droppedBeforeReporting = _droppedWarnings;
        _droppedWarnings = 0;
        if (signals is not { Count: > 0 } && droppedBeforeReporting == 0) return coverage;

        coverage ??= new JsonObject
        {
            ["mode"] = request.Intent?.Mode == AuthoringDraftMode.QuickAuthoring ? "quick_authoring" : "unverified",
            ["status"] = "partial",
            ["heuristic"] = true,
            ["not_semantic_migration_proof"] = true
        };
        if (signals is { Count: > 0 })
        {
            var items = new JsonArray(signals
                .Select(signal => (JsonNode)new JsonObject
                {
                    ["field"] = signal.Field,
                    ["original_characters"] = signal.OriginalCharacters,
                    ["kept_characters"] = signal.KeptCharacters
                })
                .ToArray());
            coverage["truncations"] = new JsonObject
            {
                ["status"] = "truncated",
                ["count"] = signals.Count,
                ["items"] = items
            };
            foreach (var signal in signals.Take(8))
                AddCappedWarning(warnings, $"字段 {signal.Field} 超长已截断：原有 {signal.OriginalCharacters} 字符，保留 {signal.KeptCharacters} 字符；请人工核对 AI 输出是否被中途削断。");
            if (signals.Count > 8)
                AddCappedWarning(warnings, $"另有 {signals.Count - 8} 处字段被截断，明细见 coverage.truncations。");
        }

        var droppedWarnings = droppedBeforeReporting + _droppedWarnings;
        _droppedWarnings = 0;
        if (droppedWarnings > 0)
        {
            coverage["warnings"] = new JsonObject
            {
                ["status"] = "truncated",
                ["count"] = warnings.Count + droppedWarnings,
                ["displayed"] = warnings.Count,
                ["dropped"] = droppedWarnings,
                ["limit"] = WarningLimit
            };
        }
        return coverage;
    }

    /// <summary>
    /// G11：警告条数达上限后不再静默丢弃——丢弃次数以结构化字段 coverage.warnings 暴露给界面。
    /// </summary>
    private static void AddCappedWarning(JsonArray warnings, string message)
    {
        if (warnings.Count >= WarningLimit)
        {
            _droppedWarnings++;
            return;
        }
        warnings.Add(message);
    }

    private static void ValidateStrictReviewStatuses(JsonNode? node, string field, string statusField)
    {
        if (node is not JsonArray array)
            throw Format($"Quick Authoring {field} 必须是数组。");
        foreach (var item in array.OfType<JsonObject>())
        {
            if (!item.ContainsKey(statusField))
                throw Format($"Quick Authoring {field} 项缺少 {statusField}。");
            if (!string.Equals(item[statusField]?.GetValue<string>(), "pending", StringComparison.Ordinal))
                throw Format($"Quick Authoring {field} 项必须保持 pending。");
        }
    }

    private static void ValidateStrictQuickOriginBindings(
        AuthoringDraftRequest request,
        JsonArray facts,
        JsonArray expressions,
        JsonArray propositions,
        JsonArray claims,
        JsonArray targetSpans,
        JsonArray? candidates)
    {
        ValidateGraph(facts, expressions, propositions, claims, targetSpans);
        foreach (var candidate in candidates?.OfType<JsonObject>() ?? [])
        {
            ValidateGraph(
                candidate["facts"] as JsonArray ?? [],
                candidate["expressions"] as JsonArray ?? [],
                candidate["propositions"] as JsonArray ?? [],
                candidate["claims"] as JsonArray ?? [],
                candidate["target_spans"] as JsonArray ?? []);
        }

        void ValidateGraph(
            JsonArray graphFacts,
            JsonArray graphExpressions,
            JsonArray graphPropositions,
            JsonArray graphClaims,
            JsonArray graphTargetSpans)
        {
            var origins = graphFacts
                .OfType<JsonObject>()
                .SelectMany(EvidenceReferenceIds)
                .Concat(graphExpressions
                    .OfType<JsonObject>()
                    .SelectMany(EvidenceReferenceIds))
                .ToHashSet(StringComparer.Ordinal);
            var packetEvidenceOrigins = request.GenerationPass == "pass_b" && request.SemanticPacket is not null
                ? request.SemanticPacket.Facts.SelectMany(fact => EvidenceReferenceIds(
                    AuthoringDraftRequestFactory.SerializeFact(fact)))
                    .Concat(request.SemanticPacket.Expressions.SelectMany(expression => EvidenceReferenceIds(
                        AuthoringDraftRequestFactory.SerializeExpression(expression))))
                    .ToArray()
                : [];
            origins.UnionWith(packetEvidenceOrigins);
            var knownOrigins = (request.SourceOrigins ?? [])
                .ToDictionary(item => item.Id, StringComparer.Ordinal);
            if (knownOrigins.Count == 0)
                throw Format("Quick Authoring 缺少服务端 source_origins 目录。");

            var referencedOrigins = graphPropositions
                .OfType<JsonObject>()
                .SelectMany(item => item["source_origin_ids"]?.AsArray() ?? [])
                .Concat(graphClaims
                    .OfType<JsonObject>()
                    .SelectMany(item => item["source_origin_ids"]?.AsArray() ?? []))
                .Concat(graphTargetSpans
                    .OfType<JsonObject>()
                    .SelectMany(item => item["source_origin_ids"]?.AsArray() ?? []))
                .Select(item => item?.GetValue<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (request.GenerationPass == "pass_b" && request.SemanticPacket is not null)
            {
                referencedOrigins = referencedOrigins
                    .Concat(packetEvidenceOrigins)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            foreach (var origin in referencedOrigins)
            {
                if (!knownOrigins.ContainsKey(origin))
                    throw Format($"Quick Authoring source_origin_id 不存在于服务端 source_origins：{origin}。");
                if (!origins.Contains(origin))
                    throw Format($"Quick Authoring source_origin_id 未绑定到可验证 evidence：{origin}。");
            }

            foreach (var evidence in graphFacts
                .OfType<JsonObject>()
                .SelectMany(EvidenceObjects)
                .Concat(graphExpressions.OfType<JsonObject>().SelectMany(EvidenceObjects)))
            {
                var referenceId = evidence["reference_id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(referenceId) || !knownOrigins.TryGetValue(referenceId, out var origin))
                    throw Format("Quick Authoring evidence.reference_id 必须引用服务端 source_origins。");
                if (!IsAcceptedOriginLocator(evidence["locator"]?.GetValue<string>(), origin)
                    || !string.Equals(evidence["quote"]?.GetValue<string>(), origin.Quote, StringComparison.Ordinal)
                    || !string.Equals(evidence["quote_hash"]?.GetValue<string>(), origin.QuoteHash, StringComparison.OrdinalIgnoreCase)
                    || evidence["evidence_verified"]?.GetValue<bool>() != true)
                    throw Format($"Quick Authoring evidence 未与服务端 source_origin 完全一致：{referenceId}。");
            }
        }

        // G8：服务端 locator 已统一为来源文件标识；旧 Worker 与本仓既有样例可能仍沿用合成 locator
        // （source unit NNNN），只作判等容忍，不改写结果，也不写进提示词。
        static bool IsAcceptedOriginLocator(string? value, AuthoringSourceOrigin origin)
            => string.Equals(value, origin.Locator, StringComparison.Ordinal)
                || string.Equals(value, AuthoringSourceOriginCatalog.LegacyLocatorForId(origin.Id), StringComparison.Ordinal);

        static IEnumerable<string> EvidenceReferenceIds(JsonObject item)
        {
            foreach (var evidence in EvidenceObjects(item))
            {
                if (evidence["reference_id"] is JsonValue reference
                    && reference.TryGetValue<string>(out var referenceId)
                    && !string.IsNullOrWhiteSpace(referenceId))
                    yield return referenceId;
            }
        }

        static IEnumerable<JsonObject> EvidenceObjects(JsonObject item)
        {
            if (item["evidence"] is JsonObject evidence) yield return evidence;
            if (item["evidence_group"] is JsonArray group)
                foreach (var evidenceItem in group.OfType<JsonObject>())
                    yield return evidenceItem;
        }
    }

    private static JsonArray? NormalizeCandidates(JsonNode? node, AuthoringDraftRequest request, JsonArray warnings, bool strictProvenance)
    {
        if (node is null) return null;
        var array = node as JsonArray ?? throw Format("candidates 必须是数组。");
        if (array.Count > 32) throw Format("candidates 数量超限。");
        var result = new JsonArray();
        for (var index = 0; index < array.Count; index++)
        {
            var candidate = array[index] as JsonObject ?? throw Format("candidate 项目必须是对象。");
            EnsureKeys(candidate, CandidateKeys, "候选");
            var id = OptionalString(candidate, "id", "candidate_id") ?? $"candidate-{index + 1:000}";
            var facts = NormalizeFacts(candidate["facts"], request, true, warnings, strictProvenance);
            var metadata = NormalizeMetadata(candidate["metadata"]);
            var expressions = NormalizeExpressions(candidate["expressions"], request, warnings, strictProvenance);
            var reasonCodes = NormalizeReasonCodes(
                candidate["segmentation_reason_codes"] ?? candidate["reason_codes"],
                warnings);
            var sourceSpans = NormalizeSourceSpans(candidate["source_spans"] ?? candidate["source_span"], request.SourceText, facts);
            var targetSpans = NormalizeTargetSpans(candidate["target_spans"]);
            var propositions = NormalizePropositions(candidate["propositions"]);
            var claims = NormalizeClaims(candidate["claims"]);
            var unresolved = NormalizeUnresolved(candidate["unresolved"]);
            var coverage = NormalizeCoverage(candidate["coverage"]);
            var reviewStatus = OptionalString(candidate, "review_status") ?? "pending";
            if (reviewStatus != "pending") throw Format("AI 生成的候选不能预先标记为已采纳。");
            result.Add(new JsonObject
            {
                ["id"] = id,
                ["facts"] = facts,
                ["metadata"] = metadata,
                ["expressions"] = expressions,
                ["review_status"] = reviewStatus,
                ["segmentation_reason_codes"] = reasonCodes,
                ["source_spans"] = sourceSpans,
                ["target_spans"] = targetSpans,
                ["propositions"] = propositions,
                ["claims"] = claims,
                ["unresolved"] = unresolved,
                ["coverage"] = coverage
            });
        }
        return result;
    }

    private static JsonArray NormalizeSourceSpans(JsonNode? node, string sourceText, JsonArray facts)
    {
        if (node is null) return [];
        var array = node is JsonValue value && value.TryGetValue<string>(out var legacyLocator)
            ? new JsonArray(new JsonObject { ["locator"] = legacyLocator })
            : node is JsonObject single ? new JsonArray(single) : node as JsonArray;
        if (array is null) throw Format("source_spans 必须是字符串、对象或对象数组。");
        if (array.Count > 32) throw Format("source_spans 数量超限。");
        return new JsonArray(array.Select(item =>
        {
            if (item is not JsonObject span) throw Format("source_spans 只能包含对象。");
            if (span.Count > 16) throw Format("source span 字段过多。");
            var locator = OptionalString(span, "locator", "source_unit_id");
            if (string.IsNullOrWhiteSpace(locator)) throw Format("source span 必须包含 locator 或 source_unit_id。");
            var verificationStatus = "unverified";
            if (span["start_utf16"] is not null || span["end_utf16"] is not null)
            {
                var start = ReadNonNegativeInt(span["start_utf16"], "start_utf16");
                var end = ReadNonNegativeInt(span["end_utf16"], "end_utf16");
                if (end <= start) throw Format("source span 的 UTF-16 范围无效。");
                if (end > sourceText.Length) throw Format("source span 超出当前参考资料范围。");
                if (span["quote"] is JsonValue quoteValue && quoteValue.TryGetValue<string>(out var quote))
                {
                    var quoteHash = span["quote_hash"]?.GetValue<string>();
                    var evidenceMatch = facts
                        .OfType<JsonObject>()
                        .SelectMany(fact => EvidenceQuotes(fact))
                        .Any(value => string.Equals(value, quote, StringComparison.Ordinal));
                    if (string.Equals(sourceText[start..end], quote, StringComparison.Ordinal)
                        && (string.IsNullOrWhiteSpace(quoteHash) || string.Equals(quoteHash, Hashing.Sha256Text(quote), StringComparison.OrdinalIgnoreCase))
                        && evidenceMatch)
                        verificationStatus = "verified";
                }
            }
            var normalized = span.DeepClone().AsObject();
            normalized["verification_status"] = verificationStatus;
            return normalized;
        }).ToArray()!);
    }

    private static JsonArray NormalizeTargetSpans(JsonNode? node)
    {
        if (node is null) return [];
        var array = node as JsonArray ?? throw Format("target_spans 必须是对象数组。");
        if (array.Count > 64) throw Format("target_spans 数量超限。");
        return new JsonArray(array.Select(item =>
        {
            var span = item as JsonObject ?? throw Format("target span 项目必须是对象。");
            EnsureKeys(span, new HashSet<string>(StringComparer.Ordinal)
            {
                "id", "text", "claim_ids", "source_origin_ids", "operation", "review_state"
            }, "target span");
            var id = OptionalString(span, "id");
            var text = OptionalString(span, "text");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
                throw Format("target span 必须包含 id 和 text。");
            var operation = OptionalString(span, "operation");
            if (operation is not ("preserve" or "merge" or "split" or "rephrase" or "drop" or "unresolved"))
                throw Format("target span operation 无效。");
            var reviewState = OptionalString(span, "review_state") ?? "pending";
            if (reviewState != "pending")
                throw Format("AI 生成的 target span 不能预先标记为已审阅。");
            var claimIds = NormalizeStringList(span["claim_ids"], "claim_ids", 128);
            var sourceOriginIds = NormalizeStringList(span["source_origin_ids"], "source_origin_ids", 128);
            if (claimIds.Count == 0 && operation is not ("drop" or "unresolved"))
                throw Format("target span 至少需要一个 claim_id，除非 operation 为 drop 或 unresolved。");
            return new JsonObject
            {
                ["id"] = Limit(id, 128),
                ["text"] = Limit(text, 12000),
                ["claim_ids"] = claimIds,
                ["source_origin_ids"] = sourceOriginIds,
                ["operation"] = operation,
                ["review_state"] = reviewState
            };
        }).ToArray()!);
    }

    private static JsonArray NormalizePropositions(JsonNode? node)
    {
        if (node is null) return [];
        var array = node as JsonArray ?? throw Format("propositions 必须是对象数组。");
        if (array.Count > 64) throw Format("propositions 数量超限。");
        return new JsonArray(array.Select(item =>
        {
            var proposition = item as JsonObject ?? throw Format("proposition 项目必须是对象。");
            EnsureKeys(proposition, new HashSet<string>(StringComparer.Ordinal)
            {
                "id", "subject", "predicate", "object", "epistemic_kind", "perspective",
                "time_scope", "polarity", "source_origin_ids", "confidence", "unresolved"
            }, "proposition");
            var id = OptionalString(proposition, "id");
            if (string.IsNullOrWhiteSpace(id))
                throw Format("proposition 必须包含 id。");
            return new JsonObject
            {
                ["id"] = Limit(id, 128),
                ["subject"] = Limit(OptionalString(proposition, "subject") ?? "unknown", 512),
                ["predicate"] = Limit(OptionalString(proposition, "predicate") ?? "unknown", 512),
                ["object"] = Limit(OptionalString(proposition, "object") ?? "unknown", 4000),
                ["epistemic_kind"] = NormalizeSemanticValue(proposition, "epistemic_kind", "unknown", new HashSet<string>(StringComparer.Ordinal) { "fact", "rumor", "interpretation", "inference", "unknown" }),
                ["perspective"] = Limit(OptionalString(proposition, "perspective") ?? "unknown", 512),
                ["time_scope"] = NormalizeSemanticValue(proposition, "time_scope", "unknown", new HashSet<string>(StringComparer.Ordinal) { "current", "historical", "future", "timeless", "unknown" }),
                ["polarity"] = NormalizeSemanticValue(proposition, "polarity", "unknown", new HashSet<string>(StringComparer.Ordinal) { "affirmed", "negated", "contested", "unknown" }),
                ["source_origin_ids"] = NormalizeStringList(proposition["source_origin_ids"], "source_origin_ids", 128),
                ["confidence"] = Limit(OptionalString(proposition, "confidence") ?? "unknown", 64),
                ["unresolved"] = NormalizeStringList(proposition["unresolved"], "unresolved", 512)
            };
        }).ToArray()!);
    }

    private static string NormalizeSemanticValue(
        JsonObject source,
        string field,
        string fallback,
        IReadOnlySet<string> allowed)
    {
        var value = OptionalString(source, field) ?? fallback;
        if (!allowed.Contains(value))
            throw Format($"{field} 值无效。");
        return value;
    }

    private static void ValidateSemanticSourceBoundaries(
        AuthoringDraftRequest request,
        JsonArray? candidates,
        JsonArray facts,
        JsonArray expressions,
        JsonArray propositions,
        JsonArray claims,
        JsonArray targetSpans,
        JsonArray unresolved,
        JsonArray warnings)
    {
        var sourceText = AuthoringDraftRequestFactory.NormalizeSourceText(request.SourceText);
        var sourceNature = request.SourceNature.Trim().ToLowerInvariant();
        var rumorSource = sourceNature.Contains("rumor", StringComparison.Ordinal)
            || sourceNature.Contains("传闻", StringComparison.Ordinal)
            || sourceText.Contains("传闻", StringComparison.Ordinal)
            || sourceText.Contains("据说", StringComparison.Ordinal)
            || sourceText.Contains("有人说", StringComparison.Ordinal);
        var historicalSource = sourceNature.Contains("historical", StringComparison.Ordinal)
            || sourceNature.Contains("历史", StringComparison.Ordinal)
            || sourceText.Contains("昔日", StringComparison.Ordinal)
            || sourceText.Contains("曾经", StringComparison.Ordinal)
            || sourceText.Contains("过去", StringComparison.Ordinal)
            || sourceText.Contains("历史上", StringComparison.Ordinal);

        CheckGraph(facts, expressions, propositions, claims, targetSpans, unresolved, null);
        foreach (var candidate in candidates?.OfType<JsonObject>() ?? [])
        {
            CheckGraph(
                candidate["facts"] as JsonArray ?? [],
                candidate["expressions"] as JsonArray ?? [],
                candidate["propositions"] as JsonArray ?? [],
                candidate["claims"] as JsonArray ?? [],
                candidate["target_spans"] as JsonArray ?? [],
                candidate["unresolved"] as JsonArray ?? [],
                candidate["id"]?.GetValue<string>());
        }

        void CheckGraph(
            JsonArray graphFacts,
            JsonArray graphExpressions,
            JsonArray graphPropositions,
            JsonArray graphClaims,
            JsonArray graphTargetSpans,
            JsonArray graphUnresolved,
            string? candidateId)
        {
            var requestedPerspectives = request.Intent?.RequestedPerspectives ?? [];
            var observedPerspectives = graphExpressions
                .OfType<JsonObject>()
                .Select(item => item["perspective"]?.GetValue<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (requestedPerspectives.Count > 1
                && graphExpressions.Count > 0
                && observedPerspectives.Length < 2)
                AddBlockingSemanticUnresolved(
                    graphUnresolved,
                    warnings,
                    "semantic-perspective-collapse",
                    "请求包含多个 perspective，但候选只保留单一身份表达，可能发生视角抹平。",
                    candidateId);

            foreach (var proposition in graphPropositions.OfType<JsonObject>())
            {
                var epistemicKind = proposition["epistemic_kind"]?.GetValue<string>() ?? "unknown";
                var timeScope = proposition["time_scope"]?.GetValue<string>() ?? "unknown";
                if (rumorSource && epistemicKind == "fact")
                    AddBlockingSemanticUnresolved(
                        graphUnresolved,
                        warnings,
                        "semantic-rumor-upgrade",
                        "传闻来源中的 proposition 不得静默升级为 fact。",
                        candidateId);
                if (historicalSource && timeScope == "current")
                    AddBlockingSemanticUnresolved(
                        graphUnresolved,
                        warnings,
                        "semantic-historical-current",
                        "历史来源中的 proposition 不得静默标记为 current。",
                        candidateId);
            }

            var evidenceByReference = graphFacts
                .OfType<JsonObject>()
                .SelectMany(EvidenceFor)
                .Concat(graphExpressions
                    .OfType<JsonObject>()
                    .SelectMany(EvidenceFor))
                .GroupBy(item => item.ReferenceId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Quote).ToArray(), StringComparer.Ordinal);

            foreach (var claim in graphClaims.OfType<JsonObject>())
            {
                var claimText = claim["text"]?.GetValue<string>() ?? string.Empty;
                var origins = claim["source_origin_ids"]?.AsArray()
                    .Select(item => item?.GetValue<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToArray() ?? [];
                var sourceQuotes = origins
                    .Where(evidenceByReference.ContainsKey)
                    .SelectMany(origin => evidenceByReference[origin])
                    .ToArray();
                if (sourceQuotes.Length > 0
                    && !sourceQuotes.Any(quote => claimText.Length > 0 && quote.Contains(claimText, StringComparison.Ordinal)))
                    AddBlockingSemanticUnresolved(
                        graphUnresolved,
                        warnings,
                        "semantic-claim-exceeds-quote-" + StableDiagnosticSuffix(claim["id"]?.GetValue<string>() ?? "claim"),
                        "claim 文本超出其 source origin 的可定位 quote，必须人工核对或拆分。",
                        candidateId);
            }

            foreach (var targetSpan in graphTargetSpans.OfType<JsonObject>())
            {
                var targetText = targetSpan["text"]?.GetValue<string>() ?? string.Empty;
                var origins = targetSpan["source_origin_ids"]?.AsArray()
                    .Select(item => item?.GetValue<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToArray() ?? [];
                var sourceQuotes = origins
                    .Where(evidenceByReference.ContainsKey)
                    .SelectMany(origin => evidenceByReference[origin])
                    .ToArray();
                if (sourceQuotes.Length > 0
                    && !sourceQuotes.Any(quote => targetText.Length > 0 && quote.Contains(targetText, StringComparison.Ordinal)))
                    AddBlockingSemanticUnresolved(
                        graphUnresolved,
                        warnings,
                        "semantic-target-exceeds-quote-" + StableDiagnosticSuffix(targetSpan["id"]?.GetValue<string>() ?? "target"),
                        "target span 文本超出其 source origin 的可定位 quote，不能直接写入正文。",
                        candidateId);
            }
        }

        static IEnumerable<(string ReferenceId, string Quote)> EvidenceFor(JsonObject item)
        {
            if (item["evidence"] is JsonObject evidence
                && evidence["reference_id"] is JsonValue referenceValue
                && referenceValue.TryGetValue<string>(out var referenceId)
                && evidence["quote"] is JsonValue quoteValue
                && quoteValue.TryGetValue<string>(out var quote))
                yield return (referenceId, quote);
            if (item["evidence_group"] is JsonArray evidenceGroup)
            {
                foreach (var groupEvidence in evidenceGroup.OfType<JsonObject>())
                {
                    if (groupEvidence["reference_id"] is not JsonValue groupReferenceValue
                        || !groupReferenceValue.TryGetValue<string>(out var groupReferenceId)
                        || groupEvidence["quote"] is not JsonValue groupQuoteValue
                        || !groupQuoteValue.TryGetValue<string>(out var groupQuote))
                        continue;
                    yield return (groupReferenceId, groupQuote);
                }
            }
        }
    }

    private static JsonArray NormalizeClaims(JsonNode? node)
    {
        if (node is null) return [];
        var array = node as JsonArray ?? throw Format("claims 必须是对象数组。");
        if (array.Count > 64) throw Format("claims 数量超限。");
        return new JsonArray(array.Select(item =>
        {
            var claim = item as JsonObject ?? throw Format("claim 项目必须是对象。");
            EnsureKeys(claim, new HashSet<string>(StringComparer.Ordinal)
            {
                "id", "proposition_id", "text", "source_origin_ids", "review_status"
            }, "claim");
            var id = OptionalString(claim, "id");
            var propositionId = OptionalString(claim, "proposition_id");
            var text = OptionalString(claim, "text");
            if (string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(propositionId)
                || string.IsNullOrWhiteSpace(text))
                throw Format("claim 必须包含 id、proposition_id 和 text。");
            var reviewStatus = OptionalString(claim, "review_status") ?? "pending";
            if (reviewStatus != "pending")
                throw Format("AI 生成的 claim 不能预先标记为已采纳。");
            return new JsonObject
            {
                ["id"] = Limit(id, 128),
                ["proposition_id"] = Limit(propositionId, 128),
                ["text"] = Limit(text, 12000),
                ["source_origin_ids"] = NormalizeStringList(claim["source_origin_ids"], "source_origin_ids", 128),
                ["review_status"] = reviewStatus
            };
        }).ToArray()!);
    }

    private static JsonObject? EnsureQuickSemanticCompleteness(
        AuthoringDraftRequest request,
        JsonArray? candidates,
        bool hasAuthoringOutput,
        JsonArray propositions,
        JsonArray claims,
        JsonArray targetSpans,
        JsonArray unresolved,
        JsonArray warnings,
        JsonObject? coverage)
    {
        if (request.Stage != AuthoringDraftStage.Complete
            || request.Intent?.Mode != AuthoringDraftMode.QuickAuthoring)
            return coverage;

        var hasOutput = hasAuthoringOutput
            || candidates is { Count: > 0 }
            || propositions.Count > 0
            || claims.Count > 0
            || targetSpans.Count > 0;
        if (!hasOutput) return coverage;

        if (candidates is { Count: > 0 })
        {
            foreach (var candidate in candidates.OfType<JsonObject>())
            {
                var candidateComplete =
                    candidate["propositions"] is JsonArray candidatePropositions && candidatePropositions.Count > 0
                    && candidate["claims"] is JsonArray candidateClaims && candidateClaims.Count > 0
                    && candidate["target_spans"] is JsonArray candidateTargetSpans && candidateTargetSpans.Count > 0;
                if (candidateComplete) continue;

                var candidateUnresolved = candidate["unresolved"] as JsonArray ?? [];
                if (!candidateUnresolved.OfType<JsonObject>().Any(item => item["id"]?.GetValue<string>() == "quick-semantic-graph-missing"))
                {
                    candidateUnresolved.Add(new JsonObject
                    {
                        ["id"] = "quick-semantic-graph-missing",
                        ["kind"] = "semantic_graph",
                        ["scope"] = "candidate",
                        ["candidate_id"] = candidate["id"]?.DeepClone(),
                        ["severity"] = "error",
                        ["blocking"] = true,
                        ["message"] = "Quick Authoring 候选缺少完整的 propositions、claims 或 target_spans，不能按完整候选建档。",
                        ["related_ids"] = new JsonArray()
                    });
                }
                candidate["unresolved"] = candidateUnresolved;
                var candidateCoverage = candidate["coverage"] as JsonObject ?? new JsonObject();
                candidateCoverage["mode"] = "quick_authoring";
                candidateCoverage["status"] = "partial";
                candidateCoverage["heuristic"] = true;
                candidateCoverage["not_semantic_migration_proof"] = true;
                candidate["coverage"] = candidateCoverage;
                warnings.Add($"Quick Authoring 候选 {candidate["id"]?.GetValue<string>() ?? "未命名"} 的语义链路未闭合。");
            }
            return coverage;
        }

        var complete = propositions.Count > 0 && claims.Count > 0 && targetSpans.Count > 0;
        if (complete) return coverage;

        if (!unresolved.OfType<JsonObject>().Any(item => item["id"]?.GetValue<string>() == "quick-semantic-graph-missing"))
        {
            unresolved.Add(new JsonObject
            {
                ["id"] = "quick-semantic-graph-missing",
                ["kind"] = "semantic_graph",
                ["scope"] = "complete",
                ["candidate_id"] = null,
                ["severity"] = "error",
                ["blocking"] = true,
                ["message"] = "Quick Authoring 结果缺少完整的 propositions、claims 或 target_spans，不能按完整候选建档。",
                ["related_ids"] = new JsonArray()
            });
            warnings.Add("Quick Authoring 语义链路未闭合，已阻断建档并要求重新生成或人工补齐。");
        }

        coverage ??= new JsonObject();
        coverage["mode"] = "quick_authoring";
        coverage["status"] = "partial";
        coverage["heuristic"] = true;
        coverage["not_semantic_migration_proof"] = true;
        return coverage;
    }

    private static JsonObject? EnsureCoverageConsistency(
        JsonArray propositions,
        JsonArray claims,
        JsonArray targetSpans,
        JsonArray unresolved,
        JsonArray warnings,
        JsonObject? coverage)
    {
        if (coverage is null) return null;
        var sourcePropositionCount = coverage["source_proposition_count"]?.GetValue<int>();
        if (sourcePropositionCount is not null && propositions.Count > sourcePropositionCount.Value)
        {
            AddBlockingSemanticUnresolved(
                unresolved,
                warnings,
                "coverage-proposition-overflow",
                "正文 propositions 数量超过 source_proposition_count，来源命题覆盖失败。");
        }

        if (claims.Count > propositions.Count)
        {
            AddBlockingSemanticUnresolved(
                unresolved,
                warnings,
                "coverage-claim-overflow",
                "claims 数量超过 propositions，语义绑定不完整。");
        }

        if (targetSpans.Count > 0 && claims.Count == 0)
        {
            AddBlockingSemanticUnresolved(
                unresolved,
                warnings,
                "coverage-target-without-claim",
                "target_spans 存在但没有可绑定的 claims。");
        }

        if (unresolved.OfType<JsonObject>().Any(item => item["blocking"]?.GetValue<bool>() == true))
            coverage["status"] = "partial";
        return coverage;
    }

    private static void AddBlockingSemanticUnresolved(
        JsonArray unresolved,
        JsonArray warnings,
        string id,
        string message,
        string? candidateId = null)
    {
        if (unresolved.OfType<JsonObject>().Any(item => item["id"]?.GetValue<string>() == id))
            return;
        unresolved.Add(new JsonObject
        {
            ["id"] = id,
            ["kind"] = "semantic_coverage",
            ["scope"] = "complete",
            ["candidate_id"] = candidateId,
            ["severity"] = "error",
            ["blocking"] = true,
            ["message"] = message,
            ["related_ids"] = new JsonArray()
        });
        warnings.Add(message);
    }

    private static void EnforceMustNotInvent(
        AuthoringDraftRequest request,
        JsonArray? candidates,
        JsonArray facts,
        JsonArray expressions,
        JsonArray propositions,
        JsonArray claims,
        JsonArray unresolved,
        JsonArray warnings)
    {
        var constraints = request.Intent?.MustNotInvent ?? [];
        if (constraints.Count == 0) return;
        var sourceText = AuthoringDraftRequestFactory.NormalizeSourceText(request.SourceText);
        var forbidPeople = constraints.Any(value => value.Contains("人物", StringComparison.OrdinalIgnoreCase)
            || value.Contains("character", StringComparison.OrdinalIgnoreCase)
            || value.Contains("person", StringComparison.OrdinalIgnoreCase));
        var forbidYears = constraints.Any(value => value.Contains("年份", StringComparison.OrdinalIgnoreCase)
            || value.Contains("year", StringComparison.OrdinalIgnoreCase));
        var forbidWar = constraints.Any(value => value.Contains("战争", StringComparison.OrdinalIgnoreCase)
            || value.Contains("战役", StringComparison.OrdinalIgnoreCase)
            || value.Contains("war", StringComparison.OrdinalIgnoreCase)
            || value.Contains("battle", StringComparison.OrdinalIgnoreCase));
        var forbidFormalIds = constraints.Any(value => value.Contains("entity id", StringComparison.OrdinalIgnoreCase)
            || value.Contains("正式 entity", StringComparison.OrdinalIgnoreCase)
            || value.Contains("正式 ID", StringComparison.OrdinalIgnoreCase)
            || value.Contains("正式编号", StringComparison.OrdinalIgnoreCase));
        var knownIds = request.RegistrySummary["profiles"]?.AsArray()
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [];

        CheckGraph(facts, expressions, propositions, claims, unresolved, null);
        if (candidates is null) return;
        foreach (var candidate in candidates.OfType<JsonObject>())
        {
            var candidateId = candidate["id"]?.GetValue<string>();
            CheckGraph(
                candidate["facts"] as JsonArray ?? [],
                candidate["expressions"] as JsonArray ?? [],
                candidate["propositions"] as JsonArray ?? [],
                candidate["claims"] as JsonArray ?? [],
                candidate["unresolved"] as JsonArray ?? [],
                candidateId);
        }

        void CheckGraph(
            JsonArray graphFacts,
            JsonArray graphExpressions,
            JsonArray graphPropositions,
            JsonArray graphClaims,
            JsonArray graphUnresolved,
            string? candidateId)
        {
            if (forbidPeople)
            {
                foreach (var subject in graphPropositions
                    .OfType<JsonObject>()
                    .Select(item => item["subject"]?.GetValue<string>()?.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item) && item != "unknown"))
                {
                    if (!sourceText.Contains(subject!, StringComparison.Ordinal))
                        AddBlockingSemanticUnresolved(
                            graphUnresolved,
                            warnings,
                            "must-not-invent-person-" + StableDiagnosticSuffix(subject!),
                            $"must_not_invent 禁止新增人物主体：{subject}。",
                            candidateId);
                }
            }

            var generatedText = graphFacts
                .OfType<JsonObject>()
                .Select(item => item["text"]?.GetValue<string>() ?? string.Empty)
                .Concat(graphExpressions.OfType<JsonObject>().Select(item => item["text"]?.GetValue<string>() ?? string.Empty))
                .Concat(graphPropositions.OfType<JsonObject>().SelectMany(item => new[]
                {
                    item["subject"]?.GetValue<string>() ?? string.Empty,
                    item["predicate"]?.GetValue<string>() ?? string.Empty,
                    item["object"]?.GetValue<string>() ?? string.Empty
                }))
                .Concat(graphClaims.OfType<JsonObject>().Select(item => item["text"]?.GetValue<string>() ?? string.Empty))
                .ToArray();
            var generatedTextValue = string.Join("\n", generatedText);

            if (forbidYears)
            {
                foreach (var year in System.Text.RegularExpressions.Regex.Matches(generatedTextValue, @"(?<!\d)(?:1[0-9]{3}|20[0-9]{2})(?!\d)")
                    .Select(match => match.Value)
                    .Distinct(StringComparer.Ordinal))
                {
                    if (!sourceText.Contains(year, StringComparison.Ordinal))
                        AddBlockingSemanticUnresolved(
                            graphUnresolved,
                            warnings,
                            "must-not-invent-year-" + year,
                            $"must_not_invent 禁止新增来源外年份：{year}。",
                            candidateId);
                }
            }

            if (forbidWar)
            {
                var markers = new[] { "战争", "战役", "开战", "征战", "war", "battle" };
                if (markers.Any(marker => generatedTextValue.Contains(marker, StringComparison.OrdinalIgnoreCase)
                    && !sourceText.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    AddBlockingSemanticUnresolved(
                        graphUnresolved,
                        warnings,
                        "must-not-invent-war",
                        "must_not_invent 禁止新增来源外战争或战役命题。",
                        candidateId);
                }
            }

            if (forbidFormalIds)
            {
                foreach (var formalId in System.Text.RegularExpressions.Regex.Matches(generatedTextValue, @"\b(?:entity|profile|hero|clan|settlement|kingdom)\.[A-Za-z0-9._-]+\b")
                    .Select(match => match.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!knownIds.Contains(formalId) && !sourceText.Contains(formalId, StringComparison.OrdinalIgnoreCase))
                        AddBlockingSemanticUnresolved(
                            graphUnresolved,
                            warnings,
                            "must-not-invent-id-" + StableDiagnosticSuffix(formalId),
                            $"must_not_invent 禁止新增未登记正式 ID：{formalId}。",
                            candidateId);
                }
            }
        }
    }

    private static string StableDiagnosticSuffix(string value)
        => System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))[..8]
            .Select(item => item.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))
            .Aggregate(string.Empty, (current, item) => current + item);

    private static IEnumerable<string> EvidenceQuotes(JsonObject fact)
    {
        if (fact["evidence"] is JsonObject evidence && evidence["quote"] is JsonValue quote && quote.TryGetValue<string>(out var text))
            yield return text;
        if (fact["evidence_group"] is JsonArray group)
            foreach (var item in group.OfType<JsonObject>())
                if (item["quote"] is JsonValue groupQuote && groupQuote.TryGetValue<string>(out var groupText))
                    yield return groupText;
    }

    private static JsonArray NormalizeReasonCodes(JsonNode? node, JsonArray warnings)
    {
        var values = NormalizeStringList(node, "segmentation_reason_codes", 128);
        foreach (var value in values.Select(item => item!.GetValue<string>()))
        {
            if (!SegmentationReasonCodes.Contains(value))
                throw Format($"segmentation_reason_codes 包含未知值：{value}。");
        }
        return values;
    }

    private static int ReadNonNegativeInt(JsonNode? node, string field)
    {
        if (node is not JsonValue value || !value.TryGetValue<int>(out var number) || number < 0)
            throw Format($"{field} 必须是非负整数。");
        return number;
    }

    private static JsonArray NormalizeFacts(JsonNode? node, AuthoringDraftRequest request, bool required, JsonArray warnings, bool strictProvenance)
    {
        if (node is null)
        {
            if (required) throw Format("facts 缺失。");
            return [];
        }

        var array = node as JsonArray ?? throw Format("facts 必须是数组。");
        if (array.Count > 64) throw Format("facts 数量超限。");
        var result = new JsonArray();
        for (var index = 0; index < array.Count; index++)
        {
            var source = array[index] as JsonObject ?? throw Format("facts 项目必须是对象。");
            EnsureKeys(source, FactKeys, "事实");
            var id = OptionalString(source, "id", "fact_id") ?? $"fact-{index + 1:000}";
            var text = RequiredAlias(source, "事实文本", "text", "content");
            var kind = OptionalString(source, "kind", "type") ?? "fact";
            var certainty = NormalizeCertainty(source) ?? "uncertain";
            var inferred = NormalizeBoolean(source["inferred"], false, "inferred");
            var evidence = NormalizeEvidence(source, request, text, id, warnings, strictProvenance);
            var evidenceGroup = NormalizeEvidenceGroup(source["evidence_group"], request, text, id, warnings, strictProvenance);
            var reviewStatus = OptionalString(source, "review_status") ?? "pending";
            result.Add(new JsonObject
            {
                ["id"] = id,
                ["kind"] = kind,
                ["text"] = text,
                ["certainty"] = certainty,
                ["inferred"] = inferred,
                ["evidence"] = evidence,
                ["evidence_group"] = evidenceGroup,
                ["review_status"] = reviewStatus
            });
        }
        return result;
    }

    private static JsonArray? NormalizeEvidenceGroup(
        JsonNode? node,
        AuthoringDraftRequest request,
        string factText,
        string fallbackId,
        JsonArray warnings,
        bool strictProvenance)
    {
        if (node is null) return null;
        var array = node as JsonArray ?? throw Format("evidence_group 必须是数组。");
        if (array.Count > 64) throw Format("evidence_group 数量超限。");
        var result = new JsonArray();
        foreach (var item in array)
        {
            var wrapper = new JsonObject { ["evidence"] = item?.DeepClone() };
            var evidence = NormalizeEvidence(wrapper, request, factText, fallbackId, warnings, strictProvenance);
            if (evidence is not null) result.Add(evidence);
        }
        return result;
    }

    private static JsonNode? NormalizeMetadata(JsonNode? node)
    {
        if (node is null) return null;
        var source = node as JsonObject ?? throw Format("metadata 必须是对象或 null。");
        EnsureKeys(source, MetadataKeys, "档案信息");
        return new JsonObject
        {
            ["title"] = OptionalString(source, "title", "title_text"),
            ["summary"] = OptionalString(source, "summary", "description"),
            ["domain"] = OptionalString(source, "domain", "category"),
            ["subdomain"] = OptionalString(source, "subdomain", "subcategory"),
            ["related_domains"] = NormalizeStringList(source["related_domains"], "related_domains", 128),
            ["note"] = OptionalString(source, "note", "notes"),
            ["era"] = OptionalString(source, "era"),
            ["entity_ids"] = NormalizeStringList(source["entity_ids"], "entity_ids", 128)
        };
    }

    private static JsonArray NormalizeExpressions(JsonNode? node, AuthoringDraftRequest request, JsonArray warnings, bool strictProvenance)
    {
        if (node is null) return [];
        var array = node as JsonArray ?? throw Format("expressions 必须是数组。");
        if (array.Count > 64) throw Format("expressions 数量超限。");
        var result = new JsonArray();
        for (var index = 0; index < array.Count; index++)
        {
            var source = array[index] as JsonObject ?? throw Format("expressions 项目必须是对象。");
            EnsureKeys(source, ExpressionKeys, "身份表达");
            var id = OptionalString(source, "id", "expression_id") ?? $"expr-{index + 1:000}";
            var profileIds = NormalizeStringList(source["profile_ids"], "profile_ids", 128);
            if (profileIds.Count == 0 && source["profile_id"] is not null)
                profileIds = NormalizeStringList(source["profile_id"], "profile_id", 128);
            var factIds = NormalizeStringList(source["fact_ids"], "fact_ids", 128);
            if (factIds.Count == 0 && source["fact_id"] is not null)
                factIds = NormalizeStringList(source["fact_id"], "fact_id", 128);
            if (factIds.Count == 0 && request.AcceptedFacts.Count == 1)
                factIds.Add(request.AcceptedFacts[0].Id);
            var perspective = OptionalString(source, "perspective", "identity")
                ?? (profileIds.Count == 1 ? profileIds[0]!.GetValue<string>() : "未指定身份视角");
            var layer = OptionalString(source, "layer", "detail", "detail_level") ?? "summary";
            var text = RequiredAlias(source, "身份表达文本", "text", "content");
            var inferred = NormalizeBoolean(source["inferred"], false, "inferred");
            var evidence = NormalizeEvidence(source, request, text, id, warnings, strictProvenance);
            var reviewStatus = OptionalString(source, "review_status") ?? "pending";
            result.Add(new JsonObject
            {
                ["id"] = id,
                ["perspective"] = perspective,
                ["layer"] = layer,
                ["text"] = text,
                ["profile_ids"] = profileIds,
                ["fact_ids"] = factIds,
                ["inferred"] = inferred,
                ["evidence"] = evidence,
                ["review_status"] = reviewStatus
            });
        }
        return result;
    }

    private static JsonObject? NormalizeEvidence(
        JsonObject source,
        AuthoringDraftRequest request,
        string factText,
        string fallbackId,
        JsonArray warnings,
        bool strictProvenance)
    {
        var evidenceNode = source["evidence"];
        var evidence = evidenceNode as JsonObject;
        if (evidence is not null) EnsureKeys(evidence, EvidenceKeys, "证据");
        var quote = evidence is not null
            ? OptionalString(evidence, "quote", "source_span", "source")
            : evidenceNode is JsonValue evidenceValue && evidenceValue.TryGetValue<string>(out var evidenceText)
                ? evidenceText
                : OptionalString(source, "source_span", "source", "quote");
        quote = NormalizeText(quote);
        if (quote.Length == 0) return null;

        var sourceText = AuthoringDraftRequestFactory.NormalizeSourceText(request.SourceText);
        var evidenceVerified = false;
        if (!SourceEvidenceMatcher.TryFind(sourceText, quote, out var sourceMatch))
        {
            if (strictProvenance)
                throw Format($"Quick Authoring 证据无法在当前资料中精确定位：{fallbackId}。");
            if (SourceEvidenceMatcher.TryFind(sourceText, factText, out sourceMatch))
            {
                quote = sourceMatch.Quote;
                AddEvidenceWarning(warnings, "AI 提供的原文片段未直接定位，已按事实文本找到资料原文；请人工核对。", fallbackId);
            }
            else
            {
                AddEvidenceWarning(warnings, "AI 返回的原文片段无法在当前资料中定位，已保留为待人工核对候选。", fallbackId);
                return BuildEvidence(fallbackId, quote, verified: false);
            }
        }
        else
        {
            quote = sourceMatch.Quote;
            var suppliedQuoteHash = evidence?["quote_hash"]?.GetValue<string>();
            var hashMatches = string.IsNullOrWhiteSpace(suppliedQuoteHash)
                || string.Equals(suppliedQuoteHash, Hashing.Sha256Text(quote), StringComparison.OrdinalIgnoreCase);
            if (!hashMatches)
                AddEvidenceWarning(warnings, "AI 返回的 quote_hash 与资料原文不一致，已降级为待人工核对。", fallbackId);
            if (!sourceMatch.Exact)
                AddEvidenceWarning(warnings, "AI 返回的原文仅通过格式归一化定位，未视为逐字验证。", fallbackId);
            evidenceVerified = sourceMatch.Exact && hashMatches;
        }

        var referenceId = evidence is null
            ? request.DraftId
            : OptionalString(evidence, "reference_id", "source_id") ?? request.DraftId;
        var locator = evidence is null
            ? "source"
            : OptionalString(evidence, "locator", "source_locator") ?? "source";
        if (string.IsNullOrWhiteSpace(referenceId)) referenceId = fallbackId;
        if (string.IsNullOrWhiteSpace(locator)) locator = "source";
        return BuildEvidence(referenceId, quote, locator, evidence?["locator_object"]?.AsObject(), evidenceVerified);
    }

    private static JsonObject BuildEvidence(string referenceId, string quote, string locator = "source", JsonObject? locatorObject = null, bool verified = false)
    {
        return new JsonObject
        {
            ["reference_id"] = Limit(referenceId, 128),
            ["locator"] = Limit(locator, 512),
            ["quote"] = quote,
            ["quote_hash"] = Hashing.Sha256Text(quote),
            ["locator_object"] = locatorObject?.DeepClone(),
            ["evidence_verified"] = verified
        };
    }

    private static void AddEvidenceWarning(JsonArray warnings, string message, string id)
        => AddCappedWarning(warnings, $"{message}（项目：{Limit(id, 128)}）");

    private static JsonArray NormalizeWarnings(JsonNode? node)
    {
        if (node is null) return [];
        if (node is JsonValue value && value.TryGetValue<string>(out var warning))
            return string.IsNullOrWhiteSpace(warning) ? [] : new JsonArray(warning.Trim());
        var array = node as JsonArray ?? throw Format("warnings 必须是字符串数组。");
        if (array.Count > 64) throw Format("warnings 数量超限。");
        return new JsonArray(array.Select(item =>
        {
            var text = item?.GetValue<string>() ?? throw Format("warnings 只能包含字符串。");
            if (text.Length > 1000) throw Format("warning 过长。");
            return JsonValue.Create(text);
        }).ToArray());
    }

    private static JsonArray NormalizeUnresolved(JsonNode? node)
    {
        if (node is null) return [];
        var array = node as JsonArray ?? throw Format("unresolved 必须是对象数组。");
        if (array.Count > 64) throw Format("unresolved 数量超限。");
        return new JsonArray(array.Select(item =>
        {
            var root = item as JsonObject ?? throw Format("unresolved 项目必须是对象。");
            EnsureKeys(root, new HashSet<string>(StringComparer.Ordinal)
            {
                "id", "kind", "scope", "candidate_id", "severity", "blocking", "message", "related_ids"
            }, "未解决项");
            var id = RequiredAlias(root, "未解决项编号", "id");
            var kind = RequiredAlias(root, "未解决项类型", "kind");
            var scope = RequiredAlias(root, "未解决项范围", "scope");
            var severity = RequiredAlias(root, "未解决项严重度", "severity");
            if (severity is not ("info" or "warning" or "error"))
                throw Format("unresolved severity 无效。");
            var message = RequiredAlias(root, "未解决项说明", "message");
            var blocking = NormalizeBoolean(root["blocking"], false, "blocking");
            var relatedIds = NormalizeStringList(root["related_ids"], "related_ids", 128);
            return new JsonObject
            {
                ["id"] = Limit(id, 128),
                ["kind"] = Limit(kind, 128),
                ["scope"] = Limit(scope, 128),
                ["candidate_id"] = OptionalString(root, "candidate_id"),
                ["severity"] = severity,
                ["blocking"] = blocking,
                ["message"] = Limit(message, 2000),
                ["related_ids"] = relatedIds
            };
        }).ToArray()!);
    }

    private static JsonObject? NormalizeCoverage(JsonNode? node)
    {
        if (node is null) return null;
        var root = node as JsonObject ?? throw Format("coverage 必须是对象或 null。");
        EnsureKeys(root, new HashSet<string>(StringComparer.Ordinal)
        {
            "mode", "status", "heuristic", "not_semantic_migration_proof",
            "source_proposition_count", "supported_proposition_count",
            "unresolved_proposition_count", "unsupported_proposition_count",
            "pipeline", "pass_a_status", "pass_b_status", "semantic_packet_id",
            "semantic_packet_hash", "semantic_packet_source_content_hash",
            "semantic_packet_prompt_revision", "semantic_packet_normalization_revision",
            "truncations", "warnings"
        }, "coverage");
        var result = new JsonObject
        {
            ["mode"] = OptionalString(root, "mode") ?? "unverified",
            ["status"] = OptionalString(root, "status") ?? "unverified",
            ["heuristic"] = NormalizeBoolean(root["heuristic"], true, "heuristic"),
            ["not_semantic_migration_proof"] = NormalizeBoolean(root["not_semantic_migration_proof"], true, "not_semantic_migration_proof")
        };
        foreach (var field in new[]
        {
            "pipeline", "pass_a_status", "pass_b_status", "semantic_packet_id",
            "semantic_packet_hash", "semantic_packet_source_content_hash",
            "semantic_packet_prompt_revision", "semantic_packet_normalization_revision"
        })
        {
            if (root[field] is not null)
                result[field] = Limit(OptionalString(root, field) ?? string.Empty, 512);
        }
        foreach (var field in new[]
        {
            "source_proposition_count", "supported_proposition_count",
            "unresolved_proposition_count", "unsupported_proposition_count"
        })
        {
            if (root[field] is null) continue;
            result[field] = ReadNonNegativeInt(root[field], field);
        }
        if (root["truncations"] is not null) result["truncations"] = root["truncations"]!.DeepClone();
        if (root["warnings"] is not null) result["warnings"] = root["warnings"]!.DeepClone();
        return result;
    }

    private static JsonArray NormalizeStringList(JsonNode? node, string field, int maximum)
    {
        if (node is null) return [];
        if (node is JsonValue value && value.TryGetValue<string>(out var single))
            return string.IsNullOrWhiteSpace(single) ? [] : new JsonArray(single.Trim());
        var array = node as JsonArray ?? throw Format($"{field} 必须是字符串或字符串数组。");
        if (array.Count > 64) throw Format($"{field} 数量超限。");
        return new JsonArray(array.Select(item =>
        {
            var text = item?.GetValue<string>()?.Trim() ?? throw Format($"{field} 包含无效值。");
            if (text.Length == 0 || text.Length > maximum) throw Format($"{field} 包含长度无效的值。");
            return JsonValue.Create(text);
        }).ToArray());
    }

    private static string RequiredAlias(JsonObject source, string label, params string[] keys)
    {
        var value = OptionalString(source, keys);
        return string.IsNullOrWhiteSpace(value) ? throw Format($"{label} 缺失。") : value;
    }

    private static string? NormalizeCertainty(JsonObject source)
    {
        foreach (var key in new[] { "certainty", "confidence" })
        {
            var node = source[key];
            if (node is null) continue;
            if (node is JsonValue value && value.TryGetValue<string>(out var text)) return text.Trim();
            if (node is JsonObject structured)
            {
                foreach (var alias in new[] { "level", "value", "label", "name", "certainty", "confidence" })
                {
                    if (structured[alias] is JsonValue aliasValue && aliasValue.TryGetValue<string>(out var aliasText))
                        return aliasText.Trim();
                }
                return "uncertain";
            }
            if (node is JsonValue) return "uncertain";
            throw Format($"字段 {key} 必须是字符串或可识别的对象。");
        }
        return null;
    }

    private static string? OptionalString(JsonObject source, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (source[key] is null) continue;
            if (source[key] is JsonValue value && value.TryGetValue<string>(out var text)) return text.Trim();
            throw Format($"字段 {key} 必须是字符串。");
        }
        return null;
    }

    private static bool NormalizeBoolean(JsonNode? node, bool fallback, string field)
    {
        if (node is null) return fallback;
        if (node is JsonValue value && value.TryGetValue<bool>(out var result)) return result;
        throw Format($"字段 {field} 必须是布尔值。");
    }

    private static string NormalizeHash(JsonNode? node, string fallback, string field)
    {
        var value = node is null ? fallback : OptionalValueString(node, field);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
            throw Format($"字段 {field} 不是 SHA-256。");
        return value.ToLowerInvariant();
    }

    private static string OptionalValueString(JsonNode node, string field)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text)) return text.Trim();
        throw Format($"字段 {field} 必须是字符串。");
    }

    private static void EnsureKeys(JsonObject source, HashSet<string> allowed, string label)
    {
        var unknown = source.Select(item => item.Key).Where(key => !allowed.Contains(key)).ToArray();
        if (unknown.Length > 0) throw Format($"{label}包含未声明字段：{string.Join(", ", unknown)}。");
    }

    private static string NormalizeText(string? value)
        => AuthoringDraftRequestFactory.NormalizeSourceText(value ?? string.Empty);

    private static string Limit(string value, int maximum, [CallerArgumentExpression(nameof(value))] string? field = null)
    {
        if (value.Length <= maximum) return value;
        _truncations?.Add(new TruncationSignal(TruncationFieldLabel(field), value.Length, maximum));
        return value[..maximum];
    }

    private static string TruncationFieldLabel(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return "unknown";
        var opening = expression.IndexOf('"');
        if (opening >= 0)
        {
            var closing = expression.IndexOf('"', opening + 1);
            if (closing - opening > 1) return expression[(opening + 1)..closing];
        }
        return expression.Length <= 64 ? expression : expression[..64];
    }

    private static InvalidOperationException Format(string message)
        => new($"WB-AI-DRAFT-FORMAT-JSON: {message}");
}
