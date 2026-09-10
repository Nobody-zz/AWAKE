using Awake.WorldbookStudio.Core;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Web;

internal static class AuthoringDraftEndpoints
{
    internal static void MapAuthoringDraftEndpoints(
        this WebApplication app,
        WebAiSessionStore sessions,
        AuthoringDraftStore drafts,
        AssistanceService assistance,
        WorldbookApplicationService service,
        Func<InvalidOperationException, IResult> aiFailure,
        string routePrefix = "/api/ai/draft")
    {
        var quickAuthoring = new QuickAuthoringOrchestrator(
            drafts,
            assistance.GetRegistrySummary,
            () => ProviderConfiguration.FromEnvironment());

        app.MapPost($"{routePrefix}/prepare", (HttpContext context, DraftPrepareRequest request) =>
        {
            try
            {
                var sessionId = sessions.RequireSession(context);
                var prepared = quickAuthoring.Prepare(sessionId, request);
                return Results.Ok(new
                {
                    ok = true,
                    draftId = prepared.Draft.DraftId,
                    draftToken = prepared.Consent.Token,
                    attemptId = prepared.Consent.AttemptId,
                    stage = AuthoringDraftStageNames.ToWire(prepared.Stage),
                    provider = prepared.ProviderStatus,
                    sourceContentHash = prepared.Draft.SourceContentHash,
                    sourceCharacters = prepared.Draft.SourceText.Length,
                    note = "仅发送本次参考资料和已确认事实；AI 返回的所有内容都只是待人工审核草稿。",
                    expiresAt = prepared.Consent.ExpiresAt
                });
            }
            catch (InvalidOperationException ex) { return aiFailure(ex); }
        });
        app.MapGet($"{routePrefix}/{{draftId}}", (HttpContext context, string draftId) =>
        {
            try
            {
                var sessionId = sessions.RequireSession(context);
                var draft = drafts.Get(sessionId, draftId);
                var result = draft.Result;
                return Results.Ok(new
                {
                    ok = true,
                    draftId = draft.DraftId,
                    sourceName = draft.SourceName,
                    sourceNature = draft.SourceNature,
                    sourceContentHash = draft.SourceContentHash,
                    expiresAt = draft.ExpiresAt,
                    intent = draft.Intent,
                    result = result is null ? null : new
                    {
                        schemaVersion = result.SchemaVersion,
                        stage = AuthoringDraftStageNames.ToWire(result.Stage),
                        requestHash = result.RequestHash,
                        sourceContentHash = result.SourceContentHash,
                        reviewOnly = result.ReviewOnly,
                        facts = result.Facts,
                        metadata = result.Metadata,
                        expressions = result.Expressions,
                        candidates = result.CandidateSet is null ? result.Candidates : null,
                        warnings = result.Warnings,
                        unresolved = result.Unresolved,
                        coverage = result.Coverage,
                        targetSpans = result.TargetSpans,
                        propositions = result.Propositions,
                        claims = result.Claims,
                        candidateSet = result.CandidateSet is null ? null : AuthoringCandidateSetProjection.Project(result.CandidateSet),
                        reviewProjection = result.CandidateSet is null ? null : AuthoringReviewProjection.Project(result.CandidateSet)
                    }
                });
            }
            catch (InvalidOperationException ex) { return aiFailure(ex); }
        });
        app.MapPost($"{routePrefix}/generate", async (HttpContext context, DraftGenerateRequest request, CancellationToken cancellationToken) =>
        {
            try
            {
                var sessionId = sessions.RequireSession(context);
                var generation = await quickAuthoring.GenerateAsync(
                    sessionId,
                    request?.DraftToken ?? string.Empty,
                    request?.AttemptId,
                    cancellationToken).ConfigureAwait(false);
                var savedResult = generation.Result;
                var wireResult = new
                {
                    schemaVersion = savedResult.SchemaVersion,
                    stage = AuthoringDraftStageNames.ToWire(savedResult.Stage),
                    requestHash = savedResult.RequestHash,
                    sourceContentHash = savedResult.SourceContentHash,
                    reviewOnly = savedResult.ReviewOnly,
                    facts = savedResult.Facts,
                    metadata = savedResult.Metadata,
                    expressions = savedResult.Expressions,
                    candidates = savedResult.CandidateSet is null ? savedResult.Candidates : null,
                    warnings = savedResult.Warnings,
                    unresolved = savedResult.Unresolved,
                    coverage = savedResult.Coverage,
                    targetSpans = savedResult.TargetSpans,
                    propositions = savedResult.Propositions,
                    claims = savedResult.Claims,
                    candidateSet = savedResult.CandidateSet is null ? null : AuthoringCandidateSetProjection.Project(savedResult.CandidateSet),
                    reviewProjection = savedResult.CandidateSet is null ? null : AuthoringReviewProjection.Project(savedResult.CandidateSet)
                };
                return Results.Ok(new { ok = true, draftId = generation.Draft.DraftId, attemptId = generation.Authorization.AttemptId, sourceContentHash = generation.Draft.SourceContentHash, result = wireResult });
            }
            catch (InvalidOperationException ex)
            {
                return aiFailure(ex);
            }
        });
        app.MapGet($"{routePrefix}/attempts/{{draftId}}/{{attemptId}}", (HttpContext context, string draftId, string attemptId) =>
        {
            try
            {
                var sessionId = sessions.RequireSession(context);
                return Results.Ok(new { ok = true, attempt = drafts.GetAttemptStatus(sessionId, draftId, attemptId) });
            }
            catch (InvalidOperationException ex) { return aiFailure(ex); }
        });
        app.MapPost($"{routePrefix}/attempts/{{draftId}}/{{attemptId}}/reconcile", (HttpContext context, string draftId, string attemptId) =>
        {
            try
            {
                var sessionId = sessions.RequireSession(context);
                return Results.Ok(new { ok = true, attempt = drafts.ReconcileAttempt(sessionId, draftId, attemptId) });
            }
            catch (InvalidOperationException ex) { return aiFailure(ex); }
        });
        app.MapGet($"{routePrefix}/review-projection/{{draftId}}", (HttpContext context, string draftId) =>
        {
            try
            {
                var sessionId = sessions.RequireSession(context);
                var draft = drafts.Get(sessionId, draftId);
                var candidateSet = draft.Result?.CandidateSet
                    ?? throw new InvalidOperationException("WB-AI-REVIEW-422: 当前草稿没有可审查候选。");
                return Results.Ok(new
                {
                    ok = true,
                    draftId = draft.DraftId,
                    reviewProjection = AuthoringReviewProjection.Project(candidateSet)
                });
            }
            catch (InvalidOperationException ex) { return aiFailure(ex); }
        });
        app.MapPost($"{routePrefix}/create-document", (HttpContext context, DraftCreateDocumentRequest request) =>
        {
            try
            {
                if (request is null) throw new InvalidOperationException("WB-AI-DRAFT-400: 草稿请求不能为空。" );
                var sessionId = sessions.RequireSession(context);
                var draft = drafts.Get(sessionId, request.DraftId ?? string.Empty);
                var result = draft.Result;
                if (result is null) throw new InvalidOperationException("WB-AI-DRAFT-422: 请先生成并检查这份参考资料草稿。" );
                var selectedResult = string.IsNullOrWhiteSpace(request.CandidateId) && result.Candidates is { Count: 1 }
                    ? result with { Candidates = null }
                    : SelectCandidate(result, request.CandidateId);
                EnsureCandidateCanBeCreated(selectedResult);
                var submission = AuthoringDraftEditValidator.RevalidateAcceptedSubmission(
                    selectedResult,
                    draft.Intent,
                    request.Facts,
                    request.Expressions);
                var title = string.IsNullOrWhiteSpace(request.Title) ? selectedResult.Metadata?.Title ?? "未命名世界知识档案" : request.Title.Trim();
                var requestedDomain = request.Domain?.Trim().ToLowerInvariant();
                var metadataDomain = selectedResult.Metadata?.Domain?.Trim().ToLowerInvariant();
                var domain = string.IsNullOrWhiteSpace(requestedDomain) ? metadataDomain : requestedDomain;
                if (domain is not ("politics" or "economy" or "culture" or "war" or "geography"))
                    throw new InvalidOperationException("WB-AI-DRAFT-422: 请在“知识分类”中选择政治、经济、文化、战争或地理；程序不会替你猜分类。" );
                var contentTier = ResolveDraftContentTier(request.ContentTier, draft.Intent);
                EnsureDraftContentTierAllowed(draft, contentTier);
                AuthoringDraftAdultGate.Create(contentTier, request.AdultConfirmed == true ? sessionId : null);
                AuthoringDraftAdultGate.ValidateContentPolicy(
                    contentTier,
                    draft.SourceText,
                    draft.SourceNature);
                var subdomain = string.IsNullOrWhiteSpace(request.Subdomain) ? selectedResult.Metadata?.Subdomain : request.Subdomain.Trim();
                var relatedDomains = request.RelatedDomains ?? selectedResult.Metadata?.RelatedDomains;
                var projectionMetadata = new AuthoringDraftMetadata(
                    title,
                    request.Summary ?? selectedResult.Metadata?.Summary,
                    domain,
                    subdomain,
                    relatedDomains ?? [],
                    selectedResult.Metadata?.Note);
                var metadataReview = AuthoringDraftEditValidator.RevalidateMetadata(
                    selectedResult.Metadata,
                    projectionMetadata,
                    submission.Facts,
                    draft.Intent);
                var selectedCandidateSet = selectedResult.CandidateSet;
                if (selectedCandidateSet is not null)
                {
                    selectedCandidateSet = AuthoringLifecycleFactory.MaterializeAcceptedProjection(
                        selectedCandidateSet,
                        submission.Facts,
                        projectionMetadata,
                        submission.Expressions);
                }
                var selectedCandidateFingerprint = selectedCandidateSet?.Candidates.SingleOrDefault()?.Fingerprint;
                var created = drafts.GetOrCreateDocument(sessionId, draft.DraftId, selectedCandidateFingerprint, _ =>
                {
                    var document = service.CreateGeneratedDocumentFromDraft(
                        title,
                         request.Summary ?? selectedResult.Metadata?.Summary,
                        domain,
                        draft.DraftId,
                        submission.Facts,
                        submission.Expressions,
                        contentTier,
                         "author.developer",
                         selectedCandidateSet,
                         subdomain,
                         relatedDomains,
                         metadataReview,
                         string.IsNullOrWhiteSpace(request.Era) ? selectedResult.Metadata?.Era : request.Era!.Trim(),
                         null,
                         selectedResult.Metadata?.EntityIds);
                    return new AuthoringDraftCreatedDocument(document.Path, document.Document["id"]?.GetValue<string>() ?? string.Empty, selectedCandidateFingerprint);
                });
                return Results.Ok(new
                {
                    ok = true,
                    draftId = draft.DraftId,
                    document = new { path = created.Path, id = created.Id },
                    note = "已将已采纳内容写入 needs_review 作者草稿；重复提交会返回同一档案，未进入正典。"
                });
            }
            catch (InvalidOperationException ex) { return aiFailure(ex); }
        });
        app.MapPost($"{routePrefix}/review-decision", (HttpContext context, DraftReviewDecisionRequest request) =>
        {
            try
            {
                if (request is null || string.IsNullOrWhiteSpace(request.DraftId) || string.IsNullOrWhiteSpace(request.Operation))
                    throw new InvalidOperationException("WB-AI-REVIEW-400: 审查决策请求不完整。");
                var sessionId = sessions.RequireSession(context);
                var draft = drafts.Get(sessionId, request.DraftId);
                var candidateSet = draft.Result?.CandidateSet
                    ?? throw new InvalidOperationException("WB-AI-REVIEW-422: 当前草稿没有可审查候选。");
                var operationId = NormalizeReviewOperationId(request, candidateSet);
                var requestDigest = ReviewRequestDigest(request, candidateSet);
                var existingDecision = drafts.FindReviewDecision(sessionId, draft.DraftId, operationId);
                if (existingDecision is not null)
                {
                    if (!string.Equals(existingDecision["request_digest"]?.GetValue<string>(), requestDigest, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("WB-AI-REVIEW-CAS-409: operation_id 已绑定另一份审查请求。");
                    return Results.Ok(new
                    {
                        ok = true,
                        draftId = draft.DraftId,
                        decision = existingDecision,
                        candidateSet = AuthoringCandidateSetProjection.Project(candidateSet),
                        reviewProjection = AuthoringReviewProjection.Project(candidateSet),
                        decisionCount = draft.ReviewDecisions?.Count ?? 0
                    });
                }
                EnsureReviewBaseline(candidateSet, request);
                var application = AuthoringReviewDecisions.Apply(
                    candidateSet,
                    request.Operation,
                    request.CandidateIds ?? [],
                    request.OrderedCandidateIds,
                    request.SplitGroups);
                application.Decision["operation_id"] = operationId;
                application.Decision["expected_generation_id"] = request.ExpectedGenerationId ?? candidateSet.GenerationId;
                application.Decision["expected_source_content_hash"] = request.ExpectedSourceContentHash ?? candidateSet.SourceContentHash;
                application.Decision["expected_packet_hash"] = request.ExpectedPacketHash ?? candidateSet.PacketHash;
                application.Decision["request_digest"] = requestDigest;
                var updated = drafts.SaveReviewDecision(sessionId, draft.DraftId, application.Decision, application.CandidateSet);
                var persistedDecision = drafts.FindReviewDecision(sessionId, draft.DraftId, operationId) ?? application.Decision;
                return Results.Ok(new
                {
                    ok = true,
                    draftId = updated.DraftId,
                    decision = persistedDecision,
                    candidateSet = AuthoringCandidateSetProjection.Project(updated.Result?.CandidateSet ?? application.CandidateSet),
                    reviewProjection = AuthoringReviewProjection.Project(updated.Result?.CandidateSet ?? application.CandidateSet),
                    decisionCount = updated.ReviewDecisions?.Count ?? 0
                });
            }
            catch (InvalidOperationException ex) { return aiFailure(ex); }
        });
    }

    internal static AuthoringDraftResult SelectCandidate(AuthoringDraftResult result, string? candidateId)
    {
        if (result.Candidates is not { Count: > 0 }) return result;
        if (string.IsNullOrWhiteSpace(candidateId) && result.Candidates.Count == 1)
        {
            var only = result.CandidateSet?.Candidates.SingleOrDefault();
            return only is null
                ? result with { Candidates = null }
                : result.CandidateSet is null
                    ? result with { Facts = only.Facts, Metadata = only.Metadata, Expressions = only.Expressions, Candidates = null }
                    : result with
                {
                    Facts = only.Facts,
                    Metadata = only.Metadata,
                    Expressions = only.Expressions,
                    CandidateSet = result.CandidateSet with { Candidates = [only] },
                    Candidates = null
                };
        }
        if (string.IsNullOrWhiteSpace(candidateId))
            throw new InvalidOperationException("WB-AI-DRAFT-422: 当前结果包含多个候选，请先选择一条候选再建档。" );
        var candidate = result.CandidateSet?.Candidates.FirstOrDefault(item => string.Equals(item.CandidateId, candidateId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("WB-AI-DRAFT-422: 所选候选不存在或已失效，请重新生成。" );
        if (candidate.ReviewStatus != "pending" && candidate.ReviewStatus != "kept" || !candidate.EvidenceCurrent)
            throw new InvalidOperationException("WB-AI-DRAFT-CANDIDATE-409: 所选候选已失效或已被取代，请重新选择当前候选。" );
        return result with
        {
            Facts = candidate.Facts,
            Metadata = candidate.Metadata,
            Expressions = candidate.Expressions,
            CandidateSet = result.CandidateSet with { Candidates = [candidate] },
            Candidates = null
        };
    }

    internal static string ResolveDraftContentTier(string? requestedContentTier, AuthoringDraftIntent? intent)
    {
        var contentTier = string.IsNullOrWhiteSpace(requestedContentTier)
            ? intent?.RequestedContentTier
            : requestedContentTier.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(contentTier)
            || contentTier == AuthoringDraftIntentFactory.UnknownContentTier)
            throw new InvalidOperationException("WB-AI-DRAFT-TIER-422: 请先由作者明确选择内容层级，再创建档案。" );
        if (contentTier is not ("base" or "adult_optional"))
            throw new InvalidOperationException("WB-AI-DRAFT-TIER-422: content tier 无效，请选择 base 或 adult_optional。" );
        return contentTier;
    }

    internal static void EnsureDraftContentTierAllowed(
        AuthoringDraftSession draft,
        string contentTier)
    {
        if (contentTier == "adult_optional"
            && !string.Equals(
                draft.Intent?.RequestedContentTier,
                "adult_optional",
                StringComparison.Ordinal))
            throw new InvalidOperationException("WB-AI-ADULT-409: 成人拓展必须在生成前选择并确认，不能把基础草稿临时升级。");
    }

    internal static void EnsureCandidateCanBeCreated(AuthoringDraftResult result)
    {
        var blocking = result.Unresolved?.FirstOrDefault(item => item.Blocking);
        var candidate = result.CandidateSet?.Candidates.SingleOrDefault();
        blocking ??= candidate?.Unresolved?.FirstOrDefault(item => item.Blocking);
        if (blocking is not null)
            throw new InvalidOperationException($"WB-AI-DRAFT-422: 仍有未解决的阻断项：{blocking.Message}");
        if (candidate is null) return;
        if (candidate.ReviewStatus is not ("pending" or "kept") || !candidate.EvidenceCurrent)
            throw new InvalidOperationException("WB-AI-DRAFT-CANDIDATE-409: 当前候选已失效或已被取代，请重新选择当前候选。" );
    }

    private static void EnsureReviewBaseline(AuthoringCandidateSet candidateSet, DraftReviewDecisionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ExpectedGenerationId)
            && !string.Equals(request.ExpectedGenerationId, candidateSet.GenerationId, StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace(request.ExpectedSourceContentHash)
            && !string.Equals(request.ExpectedSourceContentHash, candidateSet.SourceContentHash, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(request.ExpectedPacketHash)
            && !string.Equals(request.ExpectedPacketHash, candidateSet.PacketHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-REVIEW-CAS-409: 客户端审查基线已变化，请刷新后重试。");
    }

    private static string NormalizeReviewOperationId(DraftReviewDecisionRequest request, AuthoringCandidateSet candidateSet)
    {
        if (!string.IsNullOrWhiteSpace(request.OperationId))
            return request.OperationId.Trim();
        var body = new JsonObject
        {
            ["generation_id"] = candidateSet.GenerationId,
            ["operation"] = request.Operation?.Trim().ToLowerInvariant(),
            ["candidate_ids"] = new JsonArray((request.CandidateIds ?? []).Select(value => JsonValue.Create(value)).ToArray()!),
            ["ordered_candidate_ids"] = request.OrderedCandidateIds is null ? null : new JsonArray(request.OrderedCandidateIds.Select(value => JsonValue.Create(value)).ToArray()!),
            ["split_groups"] = request.SplitGroups is null ? null : new JsonArray(request.SplitGroups.Select(group => new JsonArray(group.Select(value => JsonValue.Create(value)).ToArray()!)).ToArray()!)
        };
        return "legacy-review-" + Hashing.Sha256Text(CanonicalJson.Serialize(body))[..32];
    }

    private static string ReviewRequestDigest(DraftReviewDecisionRequest request, AuthoringCandidateSet candidateSet)
    {
        var body = new JsonObject
        {
            ["generation_id"] = candidateSet.GenerationId,
            ["source_content_hash"] = candidateSet.SourceContentHash,
            ["packet_hash"] = candidateSet.PacketHash,
            ["operation"] = request.Operation?.Trim().ToLowerInvariant(),
            ["candidate_ids"] = new JsonArray((request.CandidateIds ?? []).Select(value => JsonValue.Create(value)).ToArray()!),
            ["ordered_candidate_ids"] = request.OrderedCandidateIds is null ? null : new JsonArray(request.OrderedCandidateIds.Select(value => JsonValue.Create(value)).ToArray()!),
            ["split_groups"] = request.SplitGroups is null ? null : new JsonArray(request.SplitGroups.Select(group => new JsonArray(group.Select(value => JsonValue.Create(value)).ToArray()!)).ToArray()!)
        };
        return Hashing.Sha256Text(CanonicalJson.Serialize(body));
    }
}
