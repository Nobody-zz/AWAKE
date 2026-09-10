using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Web;

internal sealed record QuickAuthoringPreparation(
    AuthoringDraftSession Draft,
    AuthoringDraftStage Stage,
    AuthoringDraftRequest Request,
    AuthoringDraftConsentTicket Consent,
    ProviderStatus ProviderStatus);

internal sealed record QuickAuthoringGeneration(
    AuthoringDraftAuthorization Authorization,
    AuthoringDraftSession Draft,
    AuthoringDraftResult Result);

internal sealed class QuickAuthoringOrchestrator
{
    private readonly AuthoringDraftStore _drafts;
    private readonly Func<JsonObject> _registrySummaryFactory;
    private readonly Func<ProviderConfiguration> _configurationFactory;
    private readonly Func<string, IAuthoringDraftProvider>? _providerFactory;

    public QuickAuthoringOrchestrator(
        AuthoringDraftStore drafts,
        Func<JsonObject> registrySummaryFactory,
        Func<ProviderConfiguration> configurationFactory,
        Func<string, IAuthoringDraftProvider>? providerFactory = null)
    {
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
        _registrySummaryFactory = registrySummaryFactory ?? throw new ArgumentNullException(nameof(registrySummaryFactory));
        _configurationFactory = configurationFactory ?? throw new ArgumentNullException(nameof(configurationFactory));
        _providerFactory = providerFactory;
    }

    public QuickAuthoringPreparation Prepare(string sessionId, DraftPrepareRequest? request)
    {
        if (request is null)
            throw new InvalidOperationException("WB-AI-DRAFT-400: 草稿请求不能为空。" );

        var providerId = WorldbookInputNormalization.NormalizeProvider(request.ProviderId);
        var mode = AuthoringDraftModeNames.Parse(request.Mode);
        if (mode == AuthoringDraftMode.SemanticMigration)
            throw new InvalidOperationException("WB-AI-DRAFT-MODE-501: Semantic Migration 当前由独立迁移工作流处理。");

        var intent = AuthoringDraftIntentFactory.Create(
            AuthoringDraftModeNames.ToWire(mode),
            request.AuthoringGoal,
            request.UserInstruction,
            request.RequestedDomain,
            request.RequestedSubdomain,
            request.RequestedAudience,
            request.RequestedPerspectives ?? request.Perspectives,
            request.StyleConstraints,
            request.MustPreserve,
            request.MustNotInvent,
            request.RequestedContentTier,
            request.RequestedEntryKind);

        var stage = AuthoringDraftStageNames.Parse(
            string.IsNullOrWhiteSpace(request.Stage) ? "facts" : request.Stage);
        AuthoringDraftIntentFactory.ValidateQuickAuthoring(intent, stage);

        var settings = _configurationFactory();
        var providerStatus = settings.GetStatus(providerId);
        if (!string.Equals(providerStatus.State, "configured", StringComparison.Ordinal))
            throw new InvalidOperationException($"WB-AI-PROVIDER-409: {providerStatus.Label}。" );

        var draft = string.IsNullOrWhiteSpace(request.DraftId)
            ? _drafts.Create(
                sessionId,
                request.SourceName ?? "未命名参考资料",
                request.SourceNature ?? "under_review",
                request.SourceText ?? string.Empty)
            : _drafts.Get(sessionId, request.DraftId);

        if (!string.IsNullOrWhiteSpace(request.SourceText)
            && !string.Equals(
                Hashing.Sha256Text(AuthoringDraftRequestFactory.NormalizeSourceText(request.SourceText)),
                draft.SourceContentHash,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: 参考资料已变化，请重新开始本次创作。" );

        var acceptedFacts = Array.Empty<AuthoringDraftFact>();
        if (stage is AuthoringDraftStage.Metadata or AuthoringDraftStage.Expressions)
        {
            if (draft.Result is null)
                throw new InvalidOperationException("WB-AI-DRAFT-422: 请先生成客观事实并至少采纳一条。" );

            var stageResult = AuthoringDraftEndpoints.SelectCandidate(draft.Result, request.CandidateId);
            acceptedFacts = AuthoringDraftSubmissionValidator
                .NormalizeAcceptedFactsForContinuation(stageResult, request.AcceptedFacts)
                .ToArray();
        }

        var providerFingerprint = AuthoringLifecycleFactory.ResolveProviderFingerprint(settings, providerId);
        var generationPass = mode == AuthoringDraftMode.QuickAuthoring && stage == AuthoringDraftStage.Complete
            ? "pass_a"
            : "single";
        var draftRequest = AuthoringDraftRequestFactory.Create(
            providerId,
            draft.DraftId,
            stage,
            draft.SourceName,
            draft.SourceNature,
            draft.SourceText,
            acceptedFacts,
            request.Metadata,
            intent.RequestedPerspectives,
            _registrySummaryFactory(),
            request.CandidateId,
            intent,
            request.AdultConfirmed == true ? sessionId : null,
            generationPass,
            providerFingerprint);
        var consent = _drafts.IssueConsent(sessionId, draftRequest, request.RetryOfAttemptId, providerFingerprint);

        return new QuickAuthoringPreparation(draft, stage, draftRequest, consent, providerStatus);
    }

    public async Task<QuickAuthoringGeneration> GenerateAsync(
        string sessionId,
        string token,
        string? expectedAttemptId,
        CancellationToken cancellationToken = default)
    {
        AuthoringDraftAuthorization? authorization = null;
        try
        {
            authorization = _drafts.ConsumeConsent(sessionId, token, expectedAttemptId);
            var request = authorization.Request;
            var configuration = _configurationFactory();
            var providerFingerprint = AuthoringLifecycleFactory.ResolveProviderFingerprint(configuration, request.ProviderId);
            if (!string.IsNullOrWhiteSpace(authorization.ProviderFingerprint)
                && !string.Equals(authorization.ProviderFingerprint, providerFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-AI-DRAFT-PROVIDER-CAS-409: Provider 配置已变化，请重新准备本次生成。");
            if (!string.IsNullOrWhiteSpace(request.ProviderFingerprint)
                && !string.Equals(request.ProviderFingerprint, providerFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-AI-DRAFT-PROVIDER-CAS-409: 请求中的 Provider 身份已失效。");

            var provider = _providerFactory is null
                ? AuthoringDraftProviderFactory.Create(request.ProviderId, configuration)
                : _providerFactory(request.ProviderId);
            var result = await GenerateThroughQuickPipelineAsync(
                sessionId,
                authorization,
                provider,
                providerFingerprint,
                cancellationToken).ConfigureAwait(false);
            var draft = _drafts.SaveResult(sessionId, authorization.AttemptId, request, result);
            return new QuickAuthoringGeneration(
                authorization,
                draft,
                draft.Result ?? result);
        }
        catch (InvalidOperationException error)
        {
            if (authorization is not null)
                _drafts.MarkFailure(sessionId, authorization.AttemptId, error);
            throw;
        }
    }

    private async Task<AuthoringDraftResult> GenerateThroughQuickPipelineAsync(
        string sessionId,
        AuthoringDraftAuthorization authorization,
        IAuthoringDraftProvider provider,
        string providerFingerprint,
        CancellationToken cancellationToken)
    {
        var request = authorization.Request;
        if (request.Intent?.Mode != AuthoringDraftMode.QuickAuthoring
            || request.Stage != AuthoringDraftStage.Complete
            || request.GenerationPass == "single")
            return await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);

        var packet = _drafts.GetSemanticPacket(sessionId, authorization.AttemptId);
        if (packet is null)
        {
            if (request.GenerationPass != "pass_a")
                throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-409: 缺少 Pass A semantic packet。");
            var passAResult = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
            packet = QuickAuthoringSemanticPacketFactory.Create(request, passAResult, providerFingerprint);
            _drafts.SaveSemanticPacket(sessionId, authorization.AttemptId, packet);
        }
        else if (!string.Equals(packet.RequestHash, request.RequestHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(packet.SourceContentHash, request.SourceContentHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(packet.ProviderFingerprint, providerFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-DRAFT-PASS-A-CAS-409: 已冻结的 semantic packet 与当前请求不一致。");

        EnsurePassAGate(packet);
        var passBRequest = AuthoringDraftRequestFactory.Create(
            request.ProviderId,
            request.DraftId,
            AuthoringDraftStage.Complete,
            request.SourceName,
            request.SourceNature,
            request.SourceText,
            [],
            null,
            request.Perspectives,
            request.RegistrySummary,
            request.CandidateId,
            request.Intent,
            request.AdultConfirmation is null ? null : sessionId,
            "pass_b",
            providerFingerprint,
            packet);
        var passBResult = await provider.GenerateAsync(passBRequest, cancellationToken).ConfigureAwait(false);
        var projected = QuickAuthoringSemanticPacketFactory.ValidateProjection(passBRequest, packet, passBResult);
        projected = projected with
        {
            RequestHash = request.RequestHash,
            SourceContentHash = request.SourceContentHash,
            CandidateSet = null
        };
        return projected with
        {
            CandidateSet = AuthoringLifecycleFactory.FromDraftResult(request, projected, providerFingerprint)
        };
    }

    private static void EnsurePassAGate(QuickAuthoringSemanticPacket packet)
    {
        var blocking = packet.Unresolved.FirstOrDefault(item => item.Blocking);
        if (blocking is not null)
            throw new InvalidOperationException(
                $"WB-AI-DRAFT-PASS-A-422: Pass A 仍有阻断项，不能进入作者投影：{blocking.Message}");
    }
}
