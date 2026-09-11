using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Web;

internal sealed record AuthoringDraftCreatedDocument(string Path, string Id, string? CandidateFingerprint = null);

internal sealed record AuthoringDraftSession(
    string DraftId,
    string SessionId,
    string SourceName,
    string SourceNature,
    string SourceText,
    string SourceContentHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    AuthoringDraftResult? Result,
    AuthoringDraftCreatedDocument? CreatedDocument = null,
    IReadOnlyList<JsonObject>? ReviewDecisions = null,
    AuthoringDraftIntent? Intent = null);

internal sealed record AuthoringDraftConsent(
    string TokenHash,
    string SessionId,
    AuthoringDraftRequest Request,
    string AttemptId,
    DateTimeOffset ExpiresAt);

internal sealed record AuthoringDraftConsentTicket(
    string Token,
    AuthoringDraftRequest Request,
    string AttemptId,
    DateTimeOffset ExpiresAt);

internal sealed record AuthoringDraftAuthorization(
    string AttemptId,
    AuthoringDraftRequest Request,
    string? ProviderFingerprint = null);

internal sealed record AuthoringDraftAttempt(
    string AttemptId,
    string SessionId,
    string DraftId,
    AuthoringDraftRequest Request,
    string? ProviderFingerprint,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    AuthoringDraftResult? Result = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    bool ResultUnknown = false,
    QuickAuthoringSemanticPacket? SemanticPacket = null,
    string? Phase = null);

internal sealed class AuthoringDraftStore
{
    private const string LegacyStateSchemaVersion = "awake.worldbook.studio.draft-state.v1";
    private const string StateSchemaVersion = "awake.worldbook.studio.draft-state.v2";
    private static readonly TimeSpan DraftLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan ConsentLifetime = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, AuthoringDraftSession> _drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AuthoringDraftConsent> _consents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AuthoringDraftAttempt> _attempts = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly string? _statePath;
    private int _stateRevision;
    private static readonly JsonSerializerOptions StateJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private sealed record PersistedState(
        List<AuthoringDraftSession>? Drafts = null,
        List<AuthoringDraftConsent>? Consents = null,
        List<AuthoringDraftAttempt>? Attempts = null,
        int Revision = 0,
        string? SchemaVersion = null);

    public AuthoringDraftStore(WorkspaceService? workspace = null)
    {
        if (workspace is null) return;
        var stateRoot = workspace.Policy.RequireAllowed(
            Path.Combine(workspace.Root, "authoring", "draft-state"),
            "初始化 Draft 状态");
        Directory.CreateDirectory(stateRoot);
        _statePath = Path.Combine(stateRoot, "state.v1.json");
        LoadState();
    }

    public AuthoringDraftSession Create(string sessionId, string sourceName, string sourceNature, string sourceText)
    {
        var normalized = AuthoringDraftRequestFactory.NormalizeSourceText(sourceText);
        if (normalized.Length == 0) throw new InvalidOperationException("WB-AI-DRAFT-400: 参考资料不能为空。" );
        if (normalized.Length > AuthoringDraftRequestFactory.MaxSourceCharacters) throw new InvalidOperationException("WB-AI-DRAFT-413: 参考资料超过 80,000 字符限制。" );
        var normalizedSourceName = string.IsNullOrWhiteSpace(sourceName) ? "未命名参考资料" : sourceName.Trim();
        var normalizedSourceNature = string.IsNullOrWhiteSpace(sourceNature) ? "under_review" : sourceNature.Trim();
        if (normalizedSourceName.Length > AuthoringDraftRequestFactory.MaxSourceNameCharacters
            || normalizedSourceNature.Length > AuthoringDraftRequestFactory.MaxSourceNatureCharacters)
            throw new InvalidOperationException("WB-AI-DRAFT-413: 参考资料描述字段超过长度限制。" );
        var now = DateTimeOffset.UtcNow;
        var draft = new AuthoringDraftSession(
            "draft-" + Guid.NewGuid().ToString("N"),
            sessionId,
            normalizedSourceName,
            normalizedSourceNature,
            normalized,
            Hashing.Sha256Text(normalized),
            now,
            now.Add(DraftLifetime),
            null);
        lock (_gate)
        {
            CleanupExpired();
            _drafts[draft.DraftId] = draft;
            PersistStateUnsafe();
        }
        return draft;
    }

    public AuthoringDraftSession CreateImportedReviewDraft(
        string handoffId,
        string documentId,
        string sourceText,
        string expectedContentHash)
    {
        var normalized = AuthoringDraftRequestFactory.NormalizeSourceText(sourceText);
        if (normalized.Length == 0) throw new InvalidOperationException("WB-HANDOFF-422: handoff payload 不能为空。");
        var normalizedHash = Hashing.Sha256Text(normalized).ToLowerInvariant();
        if (!string.Equals(normalizedHash, expectedContentHash, StringComparison.Ordinal))
            throw new InvalidOperationException("WB-HANDOFF-422: handoff payload 在草稿写入前发生变化。");
        var draftId = "draft-handoff-" + Hashing.Sha256Text(handoffId).ToLowerInvariant()[..32];
        var sessionId = "workstation-handoff:" + handoffId;
        lock (_gate)
        {
            CleanupExpired();
            if (_drafts.TryGetValue(draftId, out var existing))
            {
                if (!string.Equals(existing.SourceContentHash, Hashing.Sha256Text(normalized), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("WB-HANDOFF-409: 已存在的 handoff 草稿内容不一致。");
                return existing;
            }
            var now = DateTimeOffset.UtcNow;
            var draft = new AuthoringDraftSession(
                draftId,
                sessionId,
                string.IsNullOrWhiteSpace(documentId) ? "Persona handoff" : documentId.Trim(),
                "persona_handoff_review_only",
                normalized,
                Hashing.Sha256Text(normalized),
                now,
                now.Add(DraftLifetime),
                null);
            _drafts[draftId] = draft;
            PersistStateUnsafe();
            return draft;
        }
    }

    public bool ImportedReviewDraftExists(string handoffId, string expectedContentHash)
    {
        var draftId = "draft-handoff-" + Hashing.Sha256Text(handoffId).ToLowerInvariant()[..32];
        lock (_gate)
        {
            CleanupExpired();
            return _drafts.TryGetValue(draftId, out var draft)
                && string.Equals(draft.SourceContentHash, expectedContentHash, StringComparison.OrdinalIgnoreCase);
        }
    }

    public AuthoringDraftSession Get(string sessionId, string draftId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(draftId)) throw NotFound();
        lock (_gate)
        {
            CleanupExpired();
            if (!_drafts.TryGetValue(draftId, out var draft) || !string.Equals(draft.SessionId, sessionId, StringComparison.Ordinal)) throw NotFound();
            return draft;
        }
    }

    public AuthoringDraftSession SaveResult(string sessionId, AuthoringDraftRequest request, AuthoringDraftResult result)
        => SaveResult(sessionId, string.Empty, request, result);

    public AuthoringDraftSession SaveResult(string sessionId, string attemptId, AuthoringDraftRequest request, AuthoringDraftResult result)
    {
        if (string.IsNullOrWhiteSpace(attemptId)) throw new InvalidOperationException("WB-AI-DRAFT-409: 生成 attempt 缺失，请重新准备本次生成。" );
        if (request.Stage != result.Stage
            || !string.Equals(request.RequestHash, result.RequestHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.SourceContentHash, result.SourceContentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-DRAFT-BIND-409: Provider 结果与当前参考资料或生成阶段不匹配。" );
        lock (_gate)
        {
            CleanupExpired();
            if (!_drafts.TryGetValue(request.DraftId, out var draft) || !string.Equals(draft.SessionId, sessionId, StringComparison.Ordinal)) throw NotFound();
            if (!_attempts.TryGetValue(attemptId, out var attempt)
                || !string.Equals(attempt.SessionId, sessionId, StringComparison.Ordinal)
                || !string.Equals(attempt.DraftId, request.DraftId, StringComparison.Ordinal)
                || !string.Equals(attempt.Request.RequestHash, request.RequestHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: 生成 attempt 与当前草稿不匹配，旧结果已拒绝。" );
            if (attempt.Status != "running")
                throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: 生成 attempt 已结算，旧结果不能再次写入。" );
            var previous = draft.Result;
            var selectedPrevious = previous is not null && !string.IsNullOrWhiteSpace(request.CandidateId)
                ? SelectCandidate(previous, request.CandidateId)
                : previous;
            var facts = result.Stage is AuthoringDraftStage.Facts or AuthoringDraftStage.Complete
                ? result.Facts
                : selectedPrevious?.Facts ?? [];
            var metadata = result.Stage is AuthoringDraftStage.Metadata or AuthoringDraftStage.Complete
                ? result.Metadata
                : selectedPrevious?.Metadata;
            var expressions = result.Stage is AuthoringDraftStage.Expressions or AuthoringDraftStage.Complete
                ? result.Expressions
                : selectedPrevious?.Expressions ?? [];
            var candidates = result.Candidates ?? selectedPrevious?.Candidates;
            if (result.Stage == AuthoringDraftStage.Facts)
            {
                metadata = null;
                expressions = [];
                candidates = result.Candidates;
            }
            else if (result.Stage == AuthoringDraftStage.Metadata)
            {
                expressions = [];
                candidates = result.Candidates;
            }
            var previousDiagnostics = result.Stage == AuthoringDraftStage.Facts ? null : selectedPrevious;
            var warnings = MergeWarnings(previousDiagnostics?.Warnings, result.Warnings);
            var unresolved = MergeUnresolved(previousDiagnostics?.Unresolved, result.Unresolved);
            var coverage = result.Coverage
                ?? (previousDiagnostics?.Coverage is null ? null : (JsonObject)previousDiagnostics.Coverage.DeepClone());
            var targetSpans = result.TargetSpans
                ?? (result.Stage == AuthoringDraftStage.Facts ? null : selectedPrevious?.TargetSpans);
            var propositions = result.Propositions
                ?? (result.Stage == AuthoringDraftStage.Facts ? null : selectedPrevious?.Propositions);
            var claims = result.Claims
                ?? (result.Stage == AuthoringDraftStage.Facts ? null : selectedPrevious?.Claims);
            var merged = new AuthoringDraftResult(
                result.SchemaVersion,
                result.Stage,
                result.RequestHash,
                result.SourceContentHash,
                true,
                facts,
                metadata,
                expressions,
                warnings,
                result.CandidateSet ?? selectedPrevious?.CandidateSet,
                candidates,
                unresolved,
                coverage,
                targetSpans,
                propositions,
                claims,
                result.SemanticPacket ?? selectedPrevious?.SemanticPacket);
            if (!string.IsNullOrWhiteSpace(request.CandidateId))
                merged = merged with { Candidates = null };
            if (result.CandidateSet is not null)
            {
                var providerFingerprint = result.CandidateSet.ProviderFingerprint;
                merged = merged with
                {
                    CandidateSet = AuthoringLifecycleFactory.FromDraftResult(
                        request with { Stage = AuthoringDraftStage.Complete },
                        merged,
                        providerFingerprint)
                };
            }
            else if (selectedPrevious?.CandidateSet is not null
                && (result.Stage == AuthoringDraftStage.Facts || result.Stage == AuthoringDraftStage.Complete))
            {
                merged = merged with
                {
                    CandidateSet = AuthoringLifecycleFactory.Invalidate(selectedPrevious.CandidateSet, "authoring_layer_regenerated")
                };
            }
            var updated = draft with { Result = merged, Intent = request.Intent ?? draft.Intent };
            if (result.CandidateSet is not null || result.Stage is AuthoringDraftStage.Facts or AuthoringDraftStage.Complete)
                updated = updated with { ReviewDecisions = InvalidateReviewDecisions(draft.ReviewDecisions, "candidate_set_regenerated") };
            _drafts[request.DraftId] = updated;
            _attempts[attemptId] = attempt with
            {
                Status = "succeeded",
                UpdatedAt = DateTimeOffset.UtcNow,
                Result = merged,
                ErrorCode = null,
                ErrorMessage = null,
                ResultUnknown = false
            };
            PersistStateUnsafe();
            return updated;
        }
    }

    public AuthoringDraftConsentTicket IssueConsent(
        string sessionId,
        AuthoringDraftRequest request,
        string? retryOfAttemptId = null,
        string? providerFingerprint = null)
    {
        var token = NewToken(32);
        var attemptId = "attempt-" + Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.Add(ConsentLifetime);
        var consent = new AuthoringDraftConsent(Hashing.Sha256Text(token), sessionId, request, attemptId, expiresAt);
        lock (_gate)
        {
            CleanupExpired();
            AuthoringDraftAttempt? previous = null;
            if (!string.IsNullOrWhiteSpace(retryOfAttemptId))
            {
                if (!_attempts.TryGetValue(retryOfAttemptId, out previous)
                    || !string.Equals(previous.SessionId, sessionId, StringComparison.Ordinal)
                    || !string.Equals(previous.DraftId, request.DraftId, StringComparison.Ordinal)
                    || previous.Request.Stage != request.Stage
                    || !string.Equals(previous.Request.RequestHash, request.RequestHash, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(previous.Request.SourceContentHash, request.SourceContentHash, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(previous.ProviderFingerprint)
                        && !string.Equals(previous.ProviderFingerprint, providerFingerprint, StringComparison.OrdinalIgnoreCase))
                    || previous.Status is not ("failed" or "unknown"))
                    throw new InvalidOperationException("WB-AI-DRAFT-RETRY-409: 只能重试当前资料对应的 failed/unknown attempt。" );
            }
            var active = _attempts.Values.FirstOrDefault(attempt =>
                (previous is null || !string.Equals(attempt.AttemptId, previous.AttemptId, StringComparison.Ordinal))
                && string.Equals(attempt.SessionId, sessionId, StringComparison.Ordinal)
                && string.Equals(attempt.DraftId, request.DraftId, StringComparison.Ordinal)
                && attempt.Status is ("prepared" or "running" or "unknown"));
            if (active is not null)
                throw new InvalidOperationException($"WB-AI-DRAFT-409: 本次生成已有进行中的 attempt，请查看状态后再操作。attempt_id={active.AttemptId}" );
            if (previous is not null)
                _attempts[previous.AttemptId] = previous with { Status = "superseded", UpdatedAt = DateTimeOffset.UtcNow };
            _consents[consent.TokenHash] = consent;
            _attempts[attemptId] = new AuthoringDraftAttempt(
                attemptId,
                sessionId,
                request.DraftId,
                request,
                providerFingerprint,
                "prepared",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                SemanticPacket: previous?.SemanticPacket);
            PersistStateUnsafe();
        }
        return new AuthoringDraftConsentTicket(token, request, attemptId, consent.ExpiresAt);
    }

    public QuickAuthoringSemanticPacket? GetSemanticPacket(string sessionId, string attemptId)
    {
        lock (_gate)
        {
            CleanupExpired();
            if (!_attempts.TryGetValue(attemptId, out var attempt)
                || !string.Equals(attempt.SessionId, sessionId, StringComparison.Ordinal))
                throw NotFound();
            return attempt.SemanticPacket;
        }
    }

    public void SaveSemanticPacket(
        string sessionId,
        string attemptId,
        QuickAuthoringSemanticPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        lock (_gate)
        {
            CleanupExpired();
            if (!_attempts.TryGetValue(attemptId, out var attempt)
                || !string.Equals(attempt.SessionId, sessionId, StringComparison.Ordinal))
                throw NotFound();
            if (attempt.Status != "running")
                throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: 当前 attempt 已结算，不能写入 semantic packet。");
            if (attempt.SemanticPacket is not null
                && !string.Equals(attempt.SemanticPacket.PacketHash, packet.PacketHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: semantic packet 已绑定另一份结果。");
            _attempts[attemptId] = attempt with { SemanticPacket = packet };
            PersistStateUnsafe();
        }
    }

    public AuthoringDraftAuthorization ConsumeConsent(string sessionId, string token, string? expectedAttemptId = null)
    {
        if (string.IsNullOrWhiteSpace(token)) throw NotFound();
        var hash = Hashing.Sha256Text(token);
        lock (_gate)
        {
            CleanupExpired();
            if (!_consents.Remove(hash, out var consent) || !string.Equals(consent.SessionId, sessionId, StringComparison.Ordinal)) throw NotFound();
            if (!string.IsNullOrWhiteSpace(expectedAttemptId) && !string.Equals(expectedAttemptId, consent.AttemptId, StringComparison.Ordinal))
                throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: 生成 attempt 与授权不匹配。" );
            if (!_attempts.TryGetValue(consent.AttemptId, out var attempt) || attempt.Status != "prepared")
                throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: 生成授权已被消费或已失效。" );
            _attempts[consent.AttemptId] = attempt with { Status = "running", Phase = "starting", UpdatedAt = DateTimeOffset.UtcNow };
            PersistStateUnsafe();
            return new AuthoringDraftAuthorization(consent.AttemptId, consent.Request, attempt.ProviderFingerprint);
        }
    }

    public void MarkFailure(string sessionId, string attemptId, InvalidOperationException error)
    {
        lock (_gate)
        {
            CleanupExpired();
            if (!_attempts.TryGetValue(attemptId, out var attempt) || !string.Equals(attempt.SessionId, sessionId, StringComparison.Ordinal)) return;
            if (attempt.Status is "succeeded" or "failed") return;
            var code = error.Message.Split(':', 2)[0].Trim();
            var unknown = IsUnknownResultCode(code);
            _attempts[attemptId] = attempt with
            {
                Status = unknown ? "unknown" : "failed",
                UpdatedAt = DateTimeOffset.UtcNow,
                ErrorCode = code,
                ErrorMessage = error.Message,
                ResultUnknown = unknown
            };
            PersistStateUnsafe();
        }
    }

    /// <summary>记录生成中 attempt 的当前阶段，仅用于前端展示真实进度；不参与任何结算或校验。</summary>
    public void SetAttemptPhase(string sessionId, string attemptId, string phase)
    {
        lock (_gate)
        {
            if (!_attempts.TryGetValue(attemptId, out var attempt)) return;
            if (!string.Equals(attempt.SessionId, sessionId, StringComparison.Ordinal)) return;
            if (attempt.Status is not ("prepared" or "running")) return;
            _attempts[attemptId] = attempt with { Phase = phase, UpdatedAt = DateTimeOffset.UtcNow };
        }
    }

    public JsonObject GetAttemptStatus(string sessionId, string draftId, string attemptId, bool reconcile = false)
    {
        lock (_gate)
        {
            CleanupExpired();
            if (!_attempts.TryGetValue(attemptId, out var attempt)
                || !string.Equals(attempt.SessionId, sessionId, StringComparison.Ordinal)
                || !string.Equals(attempt.DraftId, draftId, StringComparison.Ordinal)) throw NotFound();
            var result = new JsonObject
            {
                ["attempt_id"] = attempt.AttemptId,
                ["draft_id"] = attempt.DraftId,
                ["stage"] = AuthoringDraftStageNames.ToWire(attempt.Request.Stage),
                ["request_hash"] = attempt.Request.RequestHash,
                ["source_content_hash"] = attempt.Request.SourceContentHash,
                ["status"] = attempt.Status,
                ["phase"] = attempt.Phase,
                ["result_unknown"] = attempt.ResultUnknown,
                ["error_code"] = attempt.ErrorCode,
                ["error_message"] = attempt.ErrorMessage,
                ["reconciliation"] = reconcile && attempt.Status == "unknown" ? "manual_required" : null,
                ["can_retry"] = attempt.Status is "failed" or "unknown",
                ["result_available"] = attempt.Result is not null
            };
            if (attempt.Result is not null)
                result["result"] = JsonSerializer.SerializeToNode(attempt.Result, StateJsonOptions);
            return result;
        }
    }

    public JsonObject ReconcileAttempt(string sessionId, string draftId, string attemptId)
    {
        var status = GetAttemptStatus(sessionId, draftId, attemptId, reconcile: true);
        status["reconciliation"] = status["result_available"]?.GetValue<bool>() == true
            ? "recovered"
            : status["status"]?.GetValue<string>() == "unknown"
                ? "manual_required"
                : "not_needed";
        return status;
    }

    public AuthoringDraftCreatedDocument GetOrCreateDocument(
        string sessionId,
        string draftId,
        string? candidateFingerprint,
        Func<AuthoringDraftSession, AuthoringDraftCreatedDocument> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        lock (_gate)
        {
            CleanupExpired();
            if (!_drafts.TryGetValue(draftId, out var draft) || !string.Equals(draft.SessionId, sessionId, StringComparison.Ordinal)) throw NotFound();
            if (draft.CreatedDocument is not null)
            {
                if (!string.Equals(draft.CreatedDocument.CandidateFingerprint, candidateFingerprint, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("WB-AI-DRAFT-CANDIDATE-409: 当前草稿已经按另一条候选建档。");
                return draft.CreatedDocument;
            }
            var created = create(draft);
            _drafts[draftId] = draft with { CreatedDocument = created };
            PersistStateUnsafe();
            return created;
        }
    }

    public AuthoringDraftSession SaveReviewDecision(
        string sessionId,
        string draftId,
        JsonObject decision,
        AuthoringCandidateSet? authoritativeCandidateSet = null)
    {
        ArgumentNullException.ThrowIfNull(decision);
        lock (_gate)
        {
            CleanupExpired();
            if (!_drafts.TryGetValue(draftId, out var draft) || !string.Equals(draft.SessionId, sessionId, StringComparison.Ordinal))
                throw NotFound();
            var operationId = decision["operation_id"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(operationId))
            {
                var existing = (draft.ReviewDecisions ?? []).FirstOrDefault(item =>
                    string.Equals(item["operation_id"]?.GetValue<string>(), operationId, StringComparison.Ordinal));
                if (existing is not null)
                {
                    if (!string.Equals(existing["request_digest"]?.GetValue<string>(), decision["request_digest"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("WB-AI-REVIEW-CAS-409: operation_id 已绑定另一份审查请求。");
                    return draft;
                }
            }
            var decisions = (draft.ReviewDecisions ?? []).Select(item => (JsonObject)item.DeepClone()).ToList();
            decisions.Add((JsonObject)decision.DeepClone());
            var result = draft.Result;
            if (authoritativeCandidateSet is not null)
            {
                if (result?.CandidateSet is null
                    || !string.Equals(result.CandidateSet.GenerationId, authoritativeCandidateSet.GenerationId, StringComparison.Ordinal)
                    || !string.Equals(result.CandidateSet.SourceContentHash, authoritativeCandidateSet.SourceContentHash, StringComparison.Ordinal)
                    || !string.Equals(result.CandidateSet.PacketHash, authoritativeCandidateSet.PacketHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("WB-AI-REVIEW-CAS-409: 审查候选集已变化，请刷新后重试。");
                var expectedGeneration = decision["expected_generation_id"]?.GetValue<string>();
                var expectedSourceHash = decision["expected_source_content_hash"]?.GetValue<string>();
                var expectedPacketHash = decision["expected_packet_hash"]?.GetValue<string>();
                if ((!string.IsNullOrWhiteSpace(expectedGeneration) && !string.Equals(expectedGeneration, result.CandidateSet.GenerationId, StringComparison.Ordinal))
                    || (!string.IsNullOrWhiteSpace(expectedSourceHash) && !string.Equals(expectedSourceHash, result.CandidateSet.SourceContentHash, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(expectedPacketHash) && !string.Equals(expectedPacketHash, result.CandidateSet.PacketHash, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("WB-AI-REVIEW-CAS-409: 客户端审查基线已变化，请刷新后重试。");
                result = result with
                {
                    CandidateSet = authoritativeCandidateSet,
                    Facts = authoritativeCandidateSet.Candidates.Count == 1
                        ? authoritativeCandidateSet.Candidates[0].Facts
                        : result.Facts,
                    Metadata = authoritativeCandidateSet.Candidates.Count == 1
                        ? authoritativeCandidateSet.Candidates[0].Metadata
                        : result.Metadata,
                    Expressions = authoritativeCandidateSet.Candidates.Count == 1
                        ? authoritativeCandidateSet.Candidates[0].Expressions
                        : result.Expressions
                };
            }
            var updated = draft with { Result = result, ReviewDecisions = decisions };
            _drafts[draftId] = updated;
            PersistStateUnsafe();
            return updated;
        }
    }

    public JsonObject? FindReviewDecision(string sessionId, string draftId, string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId)) return null;
        lock (_gate)
        {
            CleanupExpired();
            if (!_drafts.TryGetValue(draftId, out var draft) || !string.Equals(draft.SessionId, sessionId, StringComparison.Ordinal))
                throw NotFound();
            var decision = (draft.ReviewDecisions ?? []).FirstOrDefault(item =>
                string.Equals(item["operation_id"]?.GetValue<string>(), operationId, StringComparison.Ordinal));
            return decision is null ? null : (JsonObject)decision.DeepClone();
        }
    }

    public static InvalidOperationException NotFound() => new("WB-AI-DRAFT-404: 草稿不存在或已过期。" );

    private static AuthoringDraftResult SelectCandidate(AuthoringDraftResult result, string candidateId)
    {
        if (result.Candidates is not { Count: > 0 })
            return result;
        var candidate = result.CandidateSet?.Candidates.FirstOrDefault(item =>
            string.Equals(item.CandidateId, candidateId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("WB-AI-DRAFT-CANDIDATE-409: 所选候选不存在或已失效，请重新生成。" );
        return result with
        {
            Facts = candidate.Facts,
            Metadata = candidate.Metadata,
            Expressions = candidate.Expressions,
            CandidateSet = result.CandidateSet with { Candidates = [candidate] },
            Candidates = null
        };
    }

    private static IReadOnlyList<JsonObject>? InvalidateReviewDecisions(
        IReadOnlyList<JsonObject>? decisions,
        string reason)
    {
        if (decisions is null or { Count: 0 }) return decisions;
        return decisions.Select(decision =>
        {
            var copy = (JsonObject)decision.DeepClone();
            copy["status"] = "stale";
            copy["stale_reason"] = reason;
            return copy;
        }).ToArray();
    }

    private static IReadOnlyList<string> MergeWarnings(
        IReadOnlyList<string>? previous,
        IReadOnlyList<string> current)
    {
        return (previous ?? [])
            .Concat(current)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<AuthoringDraftUnresolved>? MergeUnresolved(
        IReadOnlyList<AuthoringDraftUnresolved>? previous,
        IReadOnlyList<AuthoringDraftUnresolved>? current)
    {
        var items = new List<AuthoringDraftUnresolved>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in (current ?? []).Concat(previous ?? []))
        {
            if (seen.Add(item.Id))
                items.Add(item);
        }
        return items.Count == 0 ? null : items;
    }

    private bool CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var changed = false;
        foreach (var key in _drafts.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray()) { _drafts.Remove(key); changed = true; }
        foreach (var key in _consents.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray()) { _consents.Remove(key); changed = true; }
        foreach (var key in _attempts.Where(x => x.Value.UpdatedAt.Add(DraftLifetime) <= now).Select(x => x.Key).ToArray()) { _attempts.Remove(key); changed = true; }
        return changed;
    }

    private void LoadState()
    {
        if (string.IsNullOrWhiteSpace(_statePath) || !File.Exists(_statePath)) return;
        try
        {
            var state = ReadPersistedStateUnsafe();
            if (state is null) return;
            _stateRevision = Math.Max(0, state.Revision);
            foreach (var draft in state.Drafts ?? []) _drafts[draft.DraftId] = draft;
            foreach (var consent in state.Consents ?? []) _consents[consent.TokenHash] = consent;
            foreach (var attempt in state.Attempts ?? []) _attempts[attempt.AttemptId] = attempt;
            var changed = MigrateLegacyCandidatesUnsafe();
            foreach (var attempt in _attempts.Values.Where(item => item.Status == "running").ToArray())
            {
                _attempts[attempt.AttemptId] = attempt with
                {
                    Status = "unknown",
                    UpdatedAt = DateTimeOffset.UtcNow,
                    ErrorCode = "WB-AI-DRAFT-UNKNOWN-503",
                    ErrorMessage = "工作室重启时生成请求仍未结算，结果需要人工对账。",
                    ResultUnknown = true
                };
                changed = true;
            }
            changed |= CleanupExpired();
            if (changed) PersistStateUnsafe();
        }
        catch (JsonException)
        {
            QuarantineState("corrupt");
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("WB-AI-DRAFT-STATE-", StringComparison.Ordinal))
        {
            QuarantineState("invalid");
        }
    }

    private void PersistStateUnsafe()
    {
        if (string.IsNullOrWhiteSpace(_statePath)) return;
        using var processMutex = new Mutex(
            false,
            "Local\\AWAKE.WorldbookStudio.DraftState." + Hashing.Sha256Text(_statePath)[..24]);
        if (!processMutex.WaitOne(TimeSpan.FromSeconds(30)))
            throw new IOException("Draft 状态文件正在由另一个工作室实例写入。");
        try
        {
            var state = new PersistedState(
                _drafts.Values.ToList(),
                _consents.Values.ToList(),
                _attempts.Values.ToList(),
                SchemaVersion: StateSchemaVersion);
            var persistedRevision = ReadPersistedRevision();
            if (persistedRevision != _stateRevision)
            {
                ReloadStateUnsafe();
                throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: Draft 状态已被另一个工作室实例更新，请刷新后重试。");
            }
            state = state with { Revision = _stateRevision + 1 };
            var temporary = _statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(JsonSerializer.Serialize(state, StateJsonOptions));
                    writer.Flush();
                    stream.Flush(true);
                }
                File.Move(temporary, _statePath, true);
                _stateRevision = state.Revision;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
        }
        finally
        {
            processMutex.ReleaseMutex();
        }
    }

    private bool MigrateLegacyCandidatesUnsafe()
    {
        var changed = false;
        foreach (var draft in _drafts.Values.ToArray())
        {
            if (draft.Result?.CandidateSet is not null || draft.Result?.Candidates is not { Count: > 0 }) continue;
            var attempt = _attempts.Values
                .Where(item => item.DraftId == draft.DraftId && item.Result?.Candidates is { Count: > 0 })
                .OrderByDescending(item => item.UpdatedAt)
                .FirstOrDefault();
            if (attempt?.ProviderFingerprint is null) continue;
            var candidateSet = AuthoringLifecycleFactory.FromDraftResult(
                attempt.Request with { Stage = AuthoringDraftStage.Complete },
                draft.Result,
                attempt.ProviderFingerprint);
            _drafts[draft.DraftId] = draft with
            {
                Result = draft.Result with { CandidateSet = candidateSet }
            };
            changed = true;
        }
        return changed;
    }

    private int ReadPersistedRevision()
    {
        if (string.IsNullOrWhiteSpace(_statePath) || !File.Exists(_statePath)) return 0;
        try
        {
            var state = ReadPersistedStateUnsafe();
            return Math.Max(0, state?.Revision ?? 0);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: Draft 状态文件无法读取，请检查并恢复后重试。");
        }
    }

    private void ReloadStateUnsafe()
    {
        _drafts.Clear();
        _consents.Clear();
        _attempts.Clear();
        _stateRevision = 0;
        if (string.IsNullOrWhiteSpace(_statePath) || !File.Exists(_statePath)) return;
        var state = ReadPersistedStateUnsafe()
            ?? throw new InvalidOperationException("WB-AI-DRAFT-CAS-409: Draft 状态文件为空，请刷新后重试。");
        _stateRevision = Math.Max(0, state.Revision);
        foreach (var draft in state.Drafts ?? []) _drafts[draft.DraftId] = draft;
        foreach (var consent in state.Consents ?? []) _consents[consent.TokenHash] = consent;
        foreach (var attempt in state.Attempts ?? []) _attempts[attempt.AttemptId] = attempt;
    }

    private PersistedState ReadPersistedStateUnsafe()
    {
        if (string.IsNullOrWhiteSpace(_statePath) || !File.Exists(_statePath)) return new PersistedState();
        var state = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(_statePath), StateJsonOptions)
            ?? throw new InvalidOperationException("WB-AI-DRAFT-STATE-422: Draft 状态文件为空。");
        ValidatePersistedState(state);
        return state;
    }

    private static void ValidatePersistedState(PersistedState state)
    {
        var schemaVersion = string.IsNullOrWhiteSpace(state.SchemaVersion)
            ? LegacyStateSchemaVersion
            : state.SchemaVersion;
        if (schemaVersion is not (LegacyStateSchemaVersion or StateSchemaVersion))
            throw new InvalidOperationException("WB-AI-DRAFT-STATE-409: Draft 状态文件版本不受支持。");
        EnsureUniqueRecords(state.Drafts ?? [], item => item?.DraftId, "draft");
        EnsureUniqueRecords(state.Consents ?? [], item => item?.TokenHash, "consent");
        EnsureUniqueRecords(state.Attempts ?? [], item => item?.AttemptId, "attempt");
    }

    private static void EnsureUniqueRecords<T>(
        IEnumerable<T> records,
        Func<T, string?> identity,
        string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var value = identity(record);
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                throw new InvalidOperationException($"WB-AI-DRAFT-STATE-422: Draft 状态包含重复或无效的 {label} 记录。");
        }
    }

    private void QuarantineState(string reason)
    {
        if (string.IsNullOrWhiteSpace(_statePath) || !File.Exists(_statePath)) return;
        var quarantine = _statePath + "." + reason + "-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try { File.Move(_statePath, quarantine, false); } catch { }
    }

    private static bool IsUnknownResultCode(string code)
        => code is "WB-AI-CANCELLED" or "WB-AI-TIMEOUT-408" or "WB-AI-TRANSPORT-502"
            or "WB-AI-WORKER-TIMEOUT-408" or "WB-AI-WORKER-TRANSPORT-502"
            or "WB-AI-CLOUD-TIMEOUT-408" or "WB-AI-CLOUD-TRANSPORT-502";

    private static string NewToken(int bytes)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
