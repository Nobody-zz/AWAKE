using System.Security.Cryptography;
using System.Text;
using PersonaWorkbench.Core;

namespace PersonaWorkbench.Web;

public enum ProviderDraftActionStatus
{
    Success,
    InvalidRequest,
    CloudConfirmationRequired,
    SessionKeyMissing,
    ProviderFailure,
    CanonicalFailure
}

public sealed class ProviderDraftActionRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}

public sealed class ProviderDraftActionResponse
{
    public ProviderDraftActionStatus Status { get; init; }
    public ProviderDraftStatus? ProviderStatus { get; init; }
    public PersonaDocument? Draft { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string? QuarantineId { get; init; }
    public DateTimeOffset? CooldownUntilUtc { get; init; }
    public ProviderUsage? Usage { get; init; }
}

public sealed class ProviderTextExpansionActionRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ProviderProtocol { get; set; } = ProviderProtocolKind.Ollama;
    public string Description { get; set; } = string.Empty;
    public ProviderTextExpansionControls? Controls { get; set; }
}

public sealed class ProviderTextExpansionActionResponse
{
    public ProviderUsage? Usage { get; init; }
    public ProviderDraftActionStatus Status { get; init; }
    public ProviderDraftStatus? ProviderStatus { get; init; }
    public string ExpandedText { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string? QuarantineId { get; init; }
    public DateTimeOffset? CooldownUntilUtc { get; init; }
}

public sealed class ProviderDslConversionActionRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SourceText { get; set; } = string.Empty;
    public string ProviderProtocol { get; set; } = ProviderProtocolKind.Ollama;
    public string LocalId { get; set; } = string.Empty;
    public string LocalDisplayName { get; set; } = string.Empty;
}

public sealed class ProviderDslConversionActionResponse
{
    public ProviderUsage? Usage { get; init; }
    public ProviderDraftActionStatus Status { get; init; }
    public ProviderDraftStatus? ProviderStatus { get; init; }
    public string Dsl { get; init; } = string.Empty;
    public PersonaDocument? Draft { get; init; }
    public PersonaDslDiagnostic? DslDiagnostic { get; init; }
    public bool UsedLocalFallback { get; init; }
    public string WarningCode { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string? QuarantineId { get; init; }
    public DateTimeOffset? CooldownUntilUtc { get; init; }
}

public sealed class ProviderDraftFailure
{
    public string Id { get; init; } = string.Empty;
    public ProviderDraftStatus Status { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; init; }
}

public sealed class ProviderDraftActionService
{
    private const int MaximumQuarantinedFailures = 128;
    private readonly ProviderSessionKeyVault _sessionKeyVault;
    private readonly IProviderDraftClient _providerClient;
    private readonly IProviderTextExpansionClient? _textExpansionClient;
    private readonly IProviderDslConversionClient? _dslConversionClient;
    private readonly object _gate = new object();
    private bool _providerOperationInFlight;
    private readonly HashSet<string> _confirmedCloudEndpoints = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ProviderDraftFailure> _quarantinedFailures = new List<ProviderDraftFailure>();

    public ProviderDraftActionService(
        ProviderSessionKeyVault sessionKeyVault,
        IProviderDraftClient providerClient,
        IProviderTextExpansionClient? textExpansionClient = null,
        IProviderDslConversionClient? dslConversionClient = null)
    {
        _sessionKeyVault = sessionKeyVault ?? throw new ArgumentNullException(nameof(sessionKeyVault));
        _providerClient = providerClient ?? throw new ArgumentNullException(nameof(providerClient));
        _textExpansionClient = textExpansionClient;
        _dslConversionClient = dslConversionClient;
    }

    public bool TryConfirmCloudEndpoint(string? endpointValue, out string errorCode)
    {
        lock (_gate)
        {
            if (_providerOperationInFlight)
            {
                errorCode = "provider.request_in_flight";
                return false;
            }

            if (!ProviderEndpointPolicy.TryValidate(endpointValue, out Uri endpoint, out errorCode)) return false;
            if (ProviderEndpointPolicy.IsLoopbackEndpoint(endpoint)) return true;
            _confirmedCloudEndpoints.Add(ToConfirmationKey(endpoint));
            return true;
        }
    }

    public bool TrySetSessionKey(string? apiKey, out string errorCode)
    {
        lock (_gate)
        {
            if (_providerOperationInFlight)
            {
                errorCode = "provider.request_in_flight";
                return false;
            }
            return _sessionKeyVault.TrySet(apiKey, out errorCode);
        }
    }

    public bool TryClearSessionKey(out string errorCode)
    {
        lock (_gate)
        {
            if (_providerOperationInFlight)
            {
                errorCode = "provider.request_in_flight";
                return false;
            }
            _sessionKeyVault.Clear();
            errorCode = string.Empty;
            return true;
        }
    }

    public async Task<ProviderDraftActionResponse> GenerateAsync(ProviderDraftActionRequest request, CancellationToken cancellationToken = default)
    {
        using IDisposable? operationLease = TryAcquireProviderOperation();
        if (operationLease == null)
        {
            return new ProviderDraftActionResponse { Status = ProviderDraftActionStatus.ProviderFailure, ErrorCode = "provider.request_in_flight" };
        }
        return await GenerateCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProviderDraftActionResponse> GenerateCoreAsync(ProviderDraftActionRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ProviderDraftActionResponse { Status = ProviderDraftActionStatus.InvalidRequest, ErrorCode = "provider.action_request_invalid" };
        }

        if (!ProviderEndpointPolicy.TryValidate(request.Endpoint, out Uri endpoint, out string endpointError)
            || string.IsNullOrWhiteSpace(request.Model) || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return new ProviderDraftActionResponse { Status = ProviderDraftActionStatus.InvalidRequest, ErrorCode = string.IsNullOrEmpty(endpointError) ? "provider.action_request_invalid" : endpointError };
        }

        bool isLoopbackEndpoint = ProviderEndpointPolicy.IsLoopbackEndpoint(endpoint);
        if (!isLoopbackEndpoint && !IsCloudEndpointConfirmed(endpoint))
        {
            return new ProviderDraftActionResponse { Status = ProviderDraftActionStatus.CloudConfirmationRequired, ErrorCode = "provider.cloud_confirmation_required" };
        }

        string? apiKey = null;
        if (_sessionKeyVault.TryGet(out string configuredKey))
        {
            apiKey = configuredKey;
        }
        else if (!isLoopbackEndpoint)
        {
            return new ProviderDraftActionResponse { Status = ProviderDraftActionStatus.SessionKeyMissing, ErrorCode = "provider.session_key_missing" };
        }

        ProviderDraftResult providerResult = await _providerClient.GenerateAsync(new ProviderDraftRequest
        {
            Endpoint = endpoint.AbsoluteUri,
            Model = request.Model.Trim(),
            Prompt = request.Prompt,
            ApiKey = apiKey
        }, cancellationToken).ConfigureAwait(false);

        if (providerResult.Status == ProviderDraftStatus.Success && providerResult.Draft != null)
        {
            return new ProviderDraftActionResponse { Status = ProviderDraftActionStatus.Success, ProviderStatus = ProviderDraftStatus.Success, Draft = providerResult.Draft };
        }

        ProviderDraftFailure failure = QuarantineFailure(providerResult.Status, providerResult.ErrorCode);
        return new ProviderDraftActionResponse
        {
            Status = ProviderDraftActionStatus.ProviderFailure,
            ProviderStatus = providerResult.Status,
            ErrorCode = providerResult.ErrorCode,
            QuarantineId = failure.Id,
            CooldownUntilUtc = providerResult.CooldownUntilUtc
        };
    }

    public async Task<ProviderTextExpansionActionResponse> ExpandDescriptionAsync(
        ProviderTextExpansionActionRequest request,
        CancellationToken cancellationToken = default)
    {
        using IDisposable? operationLease = TryAcquireProviderOperation();
        if (operationLease == null)
        {
            return new ProviderTextExpansionActionResponse { Status = ProviderDraftActionStatus.ProviderFailure, ErrorCode = "provider.request_in_flight" };
        }
        if (request == null || _textExpansionClient == null)
        {
            return new ProviderTextExpansionActionResponse { Status = ProviderDraftActionStatus.InvalidRequest, ErrorCode = "provider.expansion_action_unavailable" };
        }

        if (!ProviderTextExpansionControlNormalizer.TryNormalize(request.Controls, out ProviderTextExpansionControls normalizedControls, out string controlsErrorCode))
        {
            return new ProviderTextExpansionActionResponse { Status = ProviderDraftActionStatus.InvalidRequest, ErrorCode = controlsErrorCode };
        }

        if (!ProviderEndpointPolicy.TryValidate(request.Endpoint, out Uri endpoint, out string endpointError)
            || string.IsNullOrWhiteSpace(request.Model)
            || string.IsNullOrWhiteSpace(request.Description))
        {
            return new ProviderTextExpansionActionResponse
            {
                Status = ProviderDraftActionStatus.InvalidRequest,
                ErrorCode = string.IsNullOrEmpty(endpointError) ? "provider.expansion_action_request_invalid" : endpointError
            };
        }

        bool isLoopbackEndpoint = ProviderEndpointPolicy.IsLoopbackEndpoint(endpoint);
        if (!isLoopbackEndpoint && !IsCloudEndpointConfirmed(endpoint))
        {
            return new ProviderTextExpansionActionResponse { Status = ProviderDraftActionStatus.CloudConfirmationRequired, ErrorCode = "provider.cloud_confirmation_required" };
        }

        string? apiKey = null;
        if (_sessionKeyVault.TryGet(out string configuredKey)) apiKey = configuredKey;
        else if (!isLoopbackEndpoint)
        {
            return new ProviderTextExpansionActionResponse { Status = ProviderDraftActionStatus.SessionKeyMissing, ErrorCode = "provider.session_key_missing" };
        }

        ProviderTextExpansionResult providerResult = await _textExpansionClient.ExpandAsync(new ProviderTextExpansionRequest
        {
            Endpoint = endpoint.AbsoluteUri,
            Model = request.Model.Trim(),
            Description = request.Description,
            ProviderProtocol = request.ProviderProtocol,
            Controls = normalizedControls,
            ApiKey = apiKey
        }, cancellationToken).ConfigureAwait(false);

        if (providerResult.Status == ProviderDraftStatus.Success && !string.IsNullOrWhiteSpace(providerResult.ExpandedText))
        {
            return new ProviderTextExpansionActionResponse
            {
                Status = ProviderDraftActionStatus.Success,
                ProviderStatus = ProviderDraftStatus.Success,
                ExpandedText = providerResult.ExpandedText,
                Usage = providerResult.Usage
            };
        }

        ProviderDraftFailure failure = QuarantineFailure(providerResult.Status, providerResult.ErrorCode);
        return new ProviderTextExpansionActionResponse
        {
            Status = ProviderDraftActionStatus.ProviderFailure,
            ProviderStatus = providerResult.Status,
            ErrorCode = providerResult.ErrorCode,
            QuarantineId = failure.Id,
            CooldownUntilUtc = providerResult.CooldownUntilUtc,
            Usage = providerResult.Usage
        };
    }

    public async Task<ProviderDslConversionActionResponse> ConvertToDslAsync(
        ProviderDslConversionActionRequest request,
        CancellationToken cancellationToken = default)
    {
        using IDisposable? operationLease = TryAcquireProviderOperation();
        if (operationLease == null)
        {
            return new ProviderDslConversionActionResponse { Status = ProviderDraftActionStatus.ProviderFailure, ErrorCode = "provider.request_in_flight" };
        }
        return await ConvertToDslCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProviderDslConversionActionResponse> ConvertToDslCoreAsync(
        ProviderDslConversionActionRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ProviderDslConversionActionResponse
            {
                Status = ProviderDraftActionStatus.InvalidRequest,
                ErrorCode = "provider.dsl_identity_required"
            };
        }

        if (_dslConversionClient == null)
        {
            return new ProviderDslConversionActionResponse { Status = ProviderDraftActionStatus.InvalidRequest, ErrorCode = "provider.intermediate_action_unavailable" };
        }

        if (!ProviderEndpointPolicy.TryValidate(request.Endpoint, out Uri endpoint, out string endpointError)
            || string.IsNullOrWhiteSpace(request.Model)
            || string.IsNullOrWhiteSpace(request.SourceText))
        {
            return new ProviderDslConversionActionResponse
            {
                Status = ProviderDraftActionStatus.InvalidRequest,
                ErrorCode = string.IsNullOrEmpty(endpointError) ? "provider.intermediate_request_invalid" : endpointError
            };
        }

        bool isLoopbackEndpoint = ProviderEndpointPolicy.IsLoopbackEndpoint(endpoint);
        if (!isLoopbackEndpoint && !IsCloudEndpointConfirmed(endpoint))
        {
            return new ProviderDslConversionActionResponse { Status = ProviderDraftActionStatus.CloudConfirmationRequired, ErrorCode = "provider.cloud_confirmation_required" };
        }

        string? apiKey = null;
        if (_sessionKeyVault.TryGet(out string configuredKey)) apiKey = configuredKey;
        else if (!isLoopbackEndpoint)
        {
            return new ProviderDslConversionActionResponse { Status = ProviderDraftActionStatus.SessionKeyMissing, ErrorCode = "provider.session_key_missing" };
        }

        string localId = string.IsNullOrWhiteSpace(request.LocalId)
            ? CreateGeneratedPersonaId(request.LocalDisplayName, request.SourceText)
            : request.LocalId.Trim();
        ProviderDslConversionResult providerResult = await _dslConversionClient.ConvertAsync(new ProviderDslConversionRequest
        {
            Endpoint = endpoint.AbsoluteUri,
            Model = request.Model.Trim(),
            ProviderProtocol = request.ProviderProtocol,
            SourceText = request.SourceText,
            LocalId = localId,
            LocalDisplayName = request.LocalDisplayName.Trim(),
            ApiKey = apiKey
        }, cancellationToken).ConfigureAwait(false);
        if (providerResult.Status != ProviderDraftStatus.Success || string.IsNullOrWhiteSpace(providerResult.CandidateJson))
        {
            ProviderDraftFailure failure = QuarantineFailure(providerResult.Status, providerResult.ErrorCode);
            return new ProviderDslConversionActionResponse
            {
                Status = ProviderDraftActionStatus.ProviderFailure,
                ProviderStatus = providerResult.Status,
                ErrorCode = providerResult.ErrorCode,
                QuarantineId = failure.Id,
                CooldownUntilUtc = providerResult.CooldownUntilUtc,
                Usage = providerResult.Usage
            };
        }

        PersonaTagRegistry registry = PersonaTagRegistry.CreateDefault();
        PersonaIntermediateCandidateParseResult parsed = PersonaIntermediateCandidateParser.Parse(
            providerResult.CandidateJson,
            request.SourceText,
            localId,
            request.LocalDisplayName,
            registry);
        if (!parsed.IsValid || parsed.Document == null)
        {
            ProviderDraftFailure failure = QuarantineFailure(ProviderDraftStatus.ResponseInvalid, parsed.ErrorCode);
            return new ProviderDslConversionActionResponse
            {
                Status = ProviderDraftActionStatus.ProviderFailure,
                ProviderStatus = ProviderDraftStatus.ResponseInvalid,
                ErrorCode = parsed.ErrorCode,
                QuarantineId = failure.Id,
                Usage = providerResult.Usage
            };
        }

        return CompileDslDraft(parsed.Document, request, registry, providerResult.Usage);
    }

    private ProviderDslConversionActionResponse CompileDslDraft(
        PersonaDocument draft,
        ProviderDslConversionActionRequest request,
        PersonaTagRegistry registry,
        ProviderUsage? usage)
    {
        draft.DisplayName = string.IsNullOrWhiteSpace(request.LocalDisplayName)
            ? draft.DisplayName
            : request.LocalDisplayName.Trim();
        PersonaIdentityNormalizer.NormalizeInPlace(draft);
        draft.Id = string.IsNullOrWhiteSpace(request.LocalId)
            ? CreateGeneratedPersonaId(draft.DisplayName, request.SourceText)
            : request.LocalId.Trim();
        draft.SourceDescription = request.SourceText.Trim();
        draft.Status = PersonaReviewStatus.Draft;
        PersonaSemanticEnricher.MergeEvidenceBackedAxes(draft, request.SourceText, registry);

        if (!HasStructuredPersonaSignal(draft, registry))
        {
            ProviderDraftFailure failure = QuarantineFailure(ProviderDraftStatus.ResponseInvalid, "provider.persona_structure_empty");
            return new ProviderDslConversionActionResponse
            {
                Status = ProviderDraftActionStatus.ProviderFailure,
                ProviderStatus = ProviderDraftStatus.ResponseInvalid,
                ErrorCode = "provider.persona_structure_empty",
                QuarantineId = failure.Id,
                Usage = usage
            };
        }

        try
        {
            PersonaDslResult result = PersonaDslGenerator.Generate(draft, registry, 4096);
            if (!result.IsSuccess)
            {
                return new ProviderDslConversionActionResponse
                {
                    Status = ProviderDraftActionStatus.CanonicalFailure,
                    ErrorCode = result.Diagnostic?.ErrorCode ?? "persona.template_budget_exceeded",
                    DslDiagnostic = result.Diagnostic,
                    Usage = usage
                };
            }
            return new ProviderDslConversionActionResponse
            {
                Status = ProviderDraftActionStatus.Success,
                ProviderStatus = ProviderDraftStatus.Success,
                Draft = draft,
                Dsl = result.Dsl,
                DslDiagnostic = result.Diagnostic,
                Usage = usage
            };
        }
        catch (InvalidOperationException exception)
        {
            ProviderDraftFailure failure = QuarantineFailure(ProviderDraftStatus.ResponseInvalid, exception.Message);
            return new ProviderDslConversionActionResponse
            {
                Status = ProviderDraftActionStatus.ProviderFailure,
                ProviderStatus = ProviderDraftStatus.ResponseInvalid,
                ErrorCode = exception.Message,
                QuarantineId = failure.Id,
                Usage = usage
            };
        }
    }

    private static string CreateGeneratedPersonaId(string displayName, string sourceText)
    {
        byte[] input = Encoding.UTF8.GetBytes((displayName ?? string.Empty).Trim() + "\n" + (sourceText ?? string.Empty).Trim());
        string digest = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        return "free.generated." + digest[..16];
    }
    private IDisposable? TryAcquireProviderOperation()
    {
        lock (_gate)
        {
            if (_providerOperationInFlight) return null;
            _providerOperationInFlight = true;
            return new ProviderOperationLease(this);
        }
    }

    private void ReleaseProviderOperation()
    {
        lock (_gate) _providerOperationInFlight = false;
    }

    private sealed class ProviderOperationLease : IDisposable
    {
        private ProviderDraftActionService? _owner;

        public ProviderOperationLease(ProviderDraftActionService owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.ReleaseProviderOperation();
        }
    }
    private static bool HasStructuredPersonaSignal(PersonaDocument document, PersonaTagRegistry registry)
    {
        if (document.Tags.Any(tag => registry.TryGet(tag, out _))) return true;
        if (document.TraitProfile.Caution.HasValue
            || document.TraitProfile.Ambition.HasValue
            || document.TraitProfile.Pride.HasValue
            || document.TraitProfile.Pragmatism.HasValue
            || document.TraitProfile.InGroupLoyalty.HasValue
            || document.TraitProfile.Tradition.HasValue) return true;
        if (document.ExpressionProfile.Restraint.HasValue
            || document.ExpressionProfile.Directness.HasValue
            || document.ExpressionProfile.Formality.HasValue
            || document.ExpressionProfile.Playfulness.HasValue
            || document.ExpressionProfile.Warmth.HasValue) return true;
        if (document.BehaviorProfile.Conditionality.HasValue
            || document.BehaviorProfile.Deliberation.HasValue
            || document.BehaviorProfile.TrustTesting.HasValue
            || document.BehaviorProfile.Leverage.HasValue
            || document.BehaviorProfile.InGroupPriority.HasValue
            || document.BehaviorProfile.Leadership.HasValue) return true;
        if (document.ReactionProfile.Confrontation.HasValue
            || document.ReactionProfile.Expression.HasValue
            || document.ReactionProfile.Timing.HasValue
            || document.ReactionProfile.Resentment.HasValue
            || document.ReactionProfile.SupportSeeking.HasValue
            || !string.IsNullOrWhiteSpace(document.ReactionProfile.SensitiveConditions)
            || !string.IsNullOrWhiteSpace(document.ReactionProfile.ConditionalResponses)) return true;
        if (!string.IsNullOrWhiteSpace(document.Summary) || !string.IsNullOrWhiteSpace(document.IdentityFacts)) return true;
        if (document.CommitmentProfile.PromiseCaution.HasValue
            || document.CommitmentProfile.PromisePersistence.HasValue
            || document.CommitmentProfile.ValueTradeability.HasValue
            || !string.IsNullOrWhiteSpace(document.CommitmentProfile.PriorityOrder)
            || !string.IsNullOrWhiteSpace(document.CommitmentProfile.ProtectedValues)
            || !string.IsNullOrWhiteSpace(document.CommitmentProfile.ApplicableScope)
            || !string.IsNullOrWhiteSpace(document.CommitmentProfile.ExceptionCost)
            || !string.IsNullOrWhiteSpace(document.CommitmentProfile.BreachResponse)) return true;
        return !string.IsNullOrWhiteSpace(document.PublicDescription)
            || !string.IsNullOrWhiteSpace(document.PrivateDescription)
            || !string.IsNullOrWhiteSpace(document.ContradictionDescription)
            || document.SelfClaimRules.Count > 0
            || document.RealSelfBehaviors.Count > 0
            || document.SelfClaimExamples.Count > 0;
    }
    public IReadOnlyList<ProviderDraftFailure> GetQuarantinedFailures()
    {
        lock (_gate)
        {
            return _quarantinedFailures.ToArray();
        }
    }

    private void AddQuarantinedFailure(ProviderDraftFailure failure)
    {
        lock (_gate)
        {
            if (_quarantinedFailures.Count >= MaximumQuarantinedFailures)
            {
                _quarantinedFailures.RemoveRange(0, _quarantinedFailures.Count - MaximumQuarantinedFailures + 1);
            }
            _quarantinedFailures.Add(failure);
        }
    }

    private bool IsCloudEndpointConfirmed(Uri endpoint)
    {
        lock (_gate)
        {
            return _confirmedCloudEndpoints.Contains(ToConfirmationKey(endpoint));
        }
    }

    private ProviderDraftFailure QuarantineFailure(ProviderDraftStatus status, string errorCode)
    {
        ProviderDraftFailure failure = new ProviderDraftFailure
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = status,
            ErrorCode = errorCode,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        AddQuarantinedFailure(failure);
        return failure;
    }

    private static string ToConfirmationKey(Uri endpoint)
    {
        return endpoint.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.SafeUnescaped);
    }
}




