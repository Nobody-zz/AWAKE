using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider;

public enum ProviderKind
{
    OpenAiCompatible,
    Anthropic,
    Ollama,

    /// <summary>只在生图这条路上使用；它不提供聊天补全。</summary>
    Player2
}

public enum ProviderCapabilityId
{
    ModelDiscovery,
    TextGeneration,
    Streaming,
    Usage,
    StructuredOutput
}

public enum ProviderCapabilityState
{
    Available,
    Degraded,
    Unavailable,
    Unsupported,
    Unverified
}

public enum ProviderErrorCategory
{
    InvalidRequest,
    Authentication,
    Forbidden,
    NotFound,
    Conflict,
    RateLimited,
    Timeout,
    Unavailable,
    ServerUnavailable,
    TransportUnavailable,
    RedirectRejected,
    PolicyDenied,
    MalformedResponse,
    IncompleteStream,
    Cancelled,
    Unsupported,
    ResourceExhausted,
    CorruptCredential,
    InternalFailure
}

public enum ProviderStreamEventKind
{
    Started,
    TextDelta,
    UsageUpdate,
    RouteChanged,
    Completed,
    Cancelled,
    Failed
}

public enum CredentialProtectionMode
{
    PlatformPreferred,
    Dpapi,
    AesGcm
}

public enum CredentialProtectionStatus
{
    WindowsDpapi,
    DegradedAesGcm,
    Unsupported
}

public sealed class ProviderResult<T>
{
    private ProviderResult(bool isSuccess, T? value, ProviderError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public ProviderError? Error { get; }

    public static ProviderResult<T> Succeeded(T value) => new ProviderResult<T>(true, value, null);

    public static ProviderResult<T> Failed(ProviderError error)
    {
        return new ProviderResult<T>(false, default, error ?? throw new ArgumentNullException(nameof(error)));
    }

    public override string ToString() => IsSuccess ? "success" : Error?.ToString() ?? "failure";
}

public sealed class ProviderError
{
    public ProviderError(
        string code,
        ProviderErrorCategory category,
        string safeMessage,
        bool retryable,
        string providerId,
        string? modelId = null,
        int? statusCode = null,
        TimeSpan? retryAfter = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        Code = ProviderContract.RequireIdentifier(code, nameof(code));
        Category = category;
        SafeMessage = safeMessage ?? string.Empty;
        Retryable = retryable;
        ProviderId = ProviderContract.RequireIdentifier(providerId, nameof(providerId));
        ModelId = modelId ?? string.Empty;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
        Details = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(details ?? new Dictionary<string, string>(), StringComparer.Ordinal));
    }

    public string Code { get; }
    public ProviderErrorCategory Category { get; }
    public string SafeMessage { get; }
    public bool Retryable { get; }
    public string ProviderId { get; }
    public string ModelId { get; }
    public int? StatusCode { get; }
    public TimeSpan? RetryAfter { get; }
    public IReadOnlyDictionary<string, string> Details { get; }

    public override string ToString()
    {
        var status = StatusCode.HasValue ? ",status=" + StatusCode.Value : string.Empty;
        var retry = Retryable ? ",retryable=true" : string.Empty;
        return Code + "(" + Category + ",provider=" + ProviderId + status + retry + "): " + SafeMessage;
    }
}

public sealed class ProviderModel
{
    public ProviderModel(string id, string? displayName = null)
    {
        Id = ProviderContract.RequireModel(id, nameof(id));
        DisplayName = displayName ?? string.Empty;
    }

    public string Id { get; }
    public string DisplayName { get; }
}

public sealed class ProviderUsage
{
    public ProviderUsage(int? inputTokens, int? outputTokens)
    {
        if (inputTokens < 0) throw new ArgumentOutOfRangeException(nameof(inputTokens));
        if (outputTokens < 0) throw new ArgumentOutOfRangeException(nameof(outputTokens));
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    public int? InputTokens { get; }
    public int? OutputTokens { get; }
}

public sealed class ProviderCompletion
{
    public ProviderCompletion(string providerId, string modelId, string content, ProviderUsage? usage, string? structuredJson = null)
    {
        ProviderId = ProviderContract.RequireIdentifier(providerId, nameof(providerId));
        ModelId = ProviderContract.RequireModel(modelId, nameof(modelId));
        Content = content ?? string.Empty;
        Usage = usage;
        StructuredJson = structuredJson;
    }

    public string ProviderId { get; }
    public string ModelId { get; }
    public string Content { get; }
    public ProviderUsage? Usage { get; }
    public string? StructuredJson { get; }
}

public sealed class ProviderMessage
{
    public ProviderMessage(string role, string content)
    {
        Role = ProviderContract.RequireMessageRole(role);
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public string Role { get; }
    public string Content { get; }
}

public sealed class ProviderChatRequest
{
    public ProviderChatRequest(
        IReadOnlyList<ProviderMessage> messages,
        string? model = null,
        int? maxOutputTokens = null,
        double? temperature = null,
        string? responseSchemaJson = null)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        if (messages.Count == 0) throw new ArgumentException("At least one message is required.", nameof(messages));
        if (messages.Any(message => message == null)) throw new ArgumentException("Messages cannot contain null values.", nameof(messages));
        if (maxOutputTokens.HasValue && maxOutputTokens.Value < 1) throw new ArgumentOutOfRangeException(nameof(maxOutputTokens));
        if (temperature.HasValue && (double.IsNaN(temperature.Value) || double.IsInfinity(temperature.Value) || temperature.Value < 0 || temperature.Value > 2))
        {
            throw new ArgumentOutOfRangeException(nameof(temperature));
        }
        if (model != null && model.Length > 0) ProviderContract.RequireModel(model, nameof(model));
        if (responseSchemaJson != null && string.IsNullOrWhiteSpace(responseSchemaJson)) throw new ArgumentException("Schema JSON cannot be blank.", nameof(responseSchemaJson));

        Messages = new ReadOnlyCollection<ProviderMessage>(messages.ToArray());
        Model = model;
        MaxOutputTokens = maxOutputTokens;
        Temperature = temperature;
        ResponseSchemaJson = responseSchemaJson;
    }

    public IReadOnlyList<ProviderMessage> Messages { get; }
    public string? Model { get; }
    public int? MaxOutputTokens { get; }
    public double? Temperature { get; }
    public string? ResponseSchemaJson { get; }

    internal string ResolveModel(ProviderConnectionProfile profile)
    {
        return ProviderContract.RequireModel(string.IsNullOrWhiteSpace(Model) ? profile.DefaultModel : Model, nameof(Model));
    }
}

public sealed class ProviderLimits
{
    public ProviderLimits(
        int maxResponseBytes = 1_048_576,
        int maxRequestBytes = 262_144,
        int maxStructuredJsonBytes = 65_536,
        int maxStreamBytes = 4_194_304,
        int maxSseEventBytes = 65_536,
        int maxSseLineBytes = 16_384,
        int maxSseDataBytes = 65_536,
        int maxDeltas = 4_096,
        int maxModels = 1_024)
    {
        MaxResponseBytes = RequirePositive(maxResponseBytes, nameof(maxResponseBytes));
        MaxRequestBytes = RequirePositive(maxRequestBytes, nameof(maxRequestBytes));
        MaxStructuredJsonBytes = RequirePositive(maxStructuredJsonBytes, nameof(maxStructuredJsonBytes));
        MaxStreamBytes = RequirePositive(maxStreamBytes, nameof(maxStreamBytes));
        MaxSseEventBytes = RequirePositive(maxSseEventBytes, nameof(maxSseEventBytes));
        MaxSseLineBytes = RequirePositive(maxSseLineBytes, nameof(maxSseLineBytes));
        MaxSseDataBytes = RequirePositive(maxSseDataBytes, nameof(maxSseDataBytes));
        MaxDeltas = RequirePositive(maxDeltas, nameof(maxDeltas));
        MaxModels = RequirePositive(maxModels, nameof(maxModels));
    }

    public int MaxResponseBytes { get; }
    public int MaxRequestBytes { get; }
    public int MaxStructuredJsonBytes { get; }
    public int MaxStreamBytes { get; }
    public int MaxSseEventBytes { get; }
    public int MaxSseLineBytes { get; }
    public int MaxSseDataBytes { get; }
    public int MaxDeltas { get; }
    public int MaxModels { get; }

    private static int RequirePositive(int value, string parameterName)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(parameterName);
        return value;
    }
}

public sealed class ProviderConnectionProfile
{
    public ProviderConnectionProfile(
        string providerId,
        ProviderKind kind,
        Uri baseUri,
        string defaultModel,
        string? credentialReference = null,
        bool? isCloud = null)
    {
        ProviderId = ProviderContract.RequireIdentifier(providerId, nameof(providerId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        BaseUri = ProviderContract.NormalizeBaseUri(baseUri, nameof(baseUri));
        DefaultModel = ProviderContract.RequireModel(defaultModel, nameof(defaultModel));
        var effectiveIsCloud = isCloud ?? kind != ProviderKind.Ollama;
        if (!effectiveIsCloud && !ProviderContract.IsLoopbackHost(BaseUri))
        {
            throw new ArgumentException("Local providers must use a loopback endpoint.", nameof(baseUri));
        }

        CredentialReference = string.IsNullOrWhiteSpace(credentialReference)
            ? null
            : ProviderContract.RequireIdentifier(credentialReference, nameof(credentialReference));
        if (effectiveIsCloud && CredentialReference == null)
        {
            throw new ArgumentException("Cloud provider profiles require a credential reference.", nameof(credentialReference));
        }

        IsCloud = effectiveIsCloud;
    }

    public string ProviderId { get; }
    public ProviderKind Kind { get; }
    public Uri BaseUri { get; }
    public string DefaultModel { get; }
    public string? CredentialReference { get; }
    public bool IsCloud { get; }

    internal Uri BuildEndpointUri(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains("..", StringComparison.Ordinal)) throw new ArgumentException("Endpoint path is not fixed.", nameof(relativePath));
        var baseText = BaseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? BaseUri.AbsoluteUri : BaseUri.AbsoluteUri + "/";
        return new Uri(new Uri(baseText, UriKind.Absolute), relativePath.TrimStart('/'));
    }

    internal string EffectiveAttemptIdentity(string model, string credentialReference)
    {
        var origin = BaseUri.Scheme.ToLowerInvariant() + "://" + BaseUri.IdnHost.ToLowerInvariant() + ":" + BaseUri.Port + BaseUri.AbsolutePath.TrimEnd('/');
        return origin + "|" + model + "|" + credentialReference;
    }
}

public sealed class ProviderCapabilityReport
{
    private readonly IReadOnlyDictionary<ProviderCapabilityId, ProviderCapabilityState> states;

    internal ProviderCapabilityReport(IReadOnlyDictionary<ProviderCapabilityId, ProviderCapabilityState> states)
    {
        this.states = new ReadOnlyDictionary<ProviderCapabilityId, ProviderCapabilityState>(new Dictionary<ProviderCapabilityId, ProviderCapabilityState>(states));
    }

    public ProviderCapabilityState this[ProviderCapabilityId capability] => states.TryGetValue(capability, out var state) ? state : ProviderCapabilityState.Unverified;
    public IReadOnlyDictionary<ProviderCapabilityId, ProviderCapabilityState> States => states;
}

public sealed class ProviderConnectivityResult
{
    public ProviderConnectivityResult(string providerId, IReadOnlyList<ProviderModel> models, ProviderCapabilityReport capabilities)
    {
        ProviderId = ProviderContract.RequireIdentifier(providerId, nameof(providerId));
        Models = new ReadOnlyCollection<ProviderModel>((models ?? throw new ArgumentNullException(nameof(models))).ToArray());
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    public string ProviderId { get; }
    public IReadOnlyList<ProviderModel> Models { get; }
    public ProviderCapabilityReport Capabilities { get; }
}

public sealed class ProviderStreamEvent
{
    internal ProviderStreamEvent(
        ProviderStreamEventKind kind,
        long sequence,
        string providerId,
        string modelId,
        string text,
        ProviderUsage? usage,
        ProviderError? error,
        string fromProviderId,
        string toProviderId)
        : this(kind, sequence, providerId, modelId, text, usage, error, fromProviderId, toProviderId, null)
    {
    }

    internal ProviderStreamEvent(
        ProviderStreamEventKind kind,
        long sequence,
        string providerId,
        string modelId,
        string text,
        ProviderUsage? usage,
        ProviderError? error,
        string fromProviderId,
        string toProviderId,
        string? structuredJson)
    {
        Kind = kind;
        Sequence = sequence;
        ProviderId = ProviderContract.RequireIdentifier(providerId, nameof(providerId));
        ModelId = modelId ?? string.Empty;
        Text = text ?? string.Empty;
        Usage = usage;
        Error = error;
        FromProviderId = fromProviderId ?? string.Empty;
        ToProviderId = toProviderId ?? string.Empty;
        StructuredJson = structuredJson;
    }

    public ProviderStreamEventKind Kind { get; }
    public long Sequence { get; }
    public string ProviderId { get; }
    public string ModelId { get; }
    public string Text { get; }
    public ProviderUsage? Usage { get; }
    public ProviderError? Error { get; }
    public string FromProviderId { get; }
    public string ToProviderId { get; }
    public string? StructuredJson { get; }

    internal ProviderStreamEvent WithSequence(long sequence)
    {
        return new ProviderStreamEvent(Kind, sequence, ProviderId, ModelId, Text, Usage, Error, FromProviderId, ToProviderId, StructuredJson);
    }
}

public sealed class ProviderRouteCandidate
{
    public ProviderRouteCandidate(IProviderAdapter adapter, ApiKeyCredential? credential = null)
    {
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        Credential = credential;
    }

    public IProviderAdapter Adapter { get; }
    public ApiKeyCredential? Credential { get; }
}

public interface IProviderAdapter
{
    ProviderConnectionProfile Profile { get; }
    Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default);
    Task<ProviderResult<ProviderConnectivityResult>> TestConnectionAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default);
    Task<ProviderResult<ProviderCompletion>> CompleteAsync(ProviderChatRequest request, ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ProviderStreamEvent> StreamAsync(ProviderChatRequest request, ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default);
}

public interface IProviderClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemProviderClock : IProviderClock
{
    public static SystemProviderClock Instance { get; } = new SystemProviderClock();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface IProviderEndpointPolicy
{
    ProviderResult<bool> Evaluate(ProviderConnectionProfile profile, Uri requestUri, string operation);
}

public sealed class ExactOriginEndpointPolicy : IProviderEndpointPolicy
{
    public ProviderResult<bool> Evaluate(ProviderConnectionProfile profile, Uri requestUri, string operation)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (requestUri == null) throw new ArgumentNullException(nameof(requestUri));
        var sameOrigin = StringComparer.OrdinalIgnoreCase.Equals(profile.BaseUri.Scheme, requestUri.Scheme)
            && StringComparer.OrdinalIgnoreCase.Equals(profile.BaseUri.IdnHost, requestUri.IdnHost)
            && profile.BaseUri.Port == requestUri.Port
            && string.IsNullOrEmpty(requestUri.UserInfo)
            && string.IsNullOrEmpty(requestUri.Query)
            && string.IsNullOrEmpty(requestUri.Fragment);
        if (sameOrigin) return ProviderResult<bool>.Succeeded(true);
        return ProviderResult<bool>.Failed(new ProviderError("endpoint.origin_denied", ProviderErrorCategory.PolicyDenied, "Endpoint origin denied by policy.", false, profile.ProviderId));
    }
}

public sealed class DelegateProviderEndpointPolicy : IProviderEndpointPolicy
{
    private readonly Func<ProviderConnectionProfile, Uri, string, ProviderResult<bool>> evaluator;

    public DelegateProviderEndpointPolicy(Func<ProviderConnectionProfile, Uri, string, ProviderResult<bool>> evaluator)
    {
        this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    public ProviderResult<bool> Evaluate(ProviderConnectionProfile profile, Uri requestUri, string operation)
    {
        return evaluator(profile, requestUri, operation);
    }
}

internal static class ProviderContract
{
    internal static string RequireIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160) throw new ArgumentException("Identifier is required and bounded.", parameterName);
        foreach (var character in value)
        {
            if (!(char.IsLetterOrDigit(character) || character is '.' or '_' or '-' or ':' or '/')) throw new ArgumentException("Identifier contains an unsupported character.", parameterName);
        }

        return value;
    }

    internal static string RequireModel(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256) throw new ArgumentException("Model is required and bounded.", parameterName);
        foreach (var character in value)
        {
            if (char.IsControl(character)) throw new ArgumentException("Model contains a control character.", parameterName);
        }

        return value;
    }

    internal static string RequireMessageRole(string value)
    {
        if (value is not ("system" or "user" or "assistant")) throw new ArgumentException("Unsupported message role.", nameof(value));
        return value;
    }

    internal static Uri NormalizeBaseUri(Uri baseUri, string parameterName)
    {
        if (baseUri == null) throw new ArgumentNullException(parameterName);
        if (!baseUri.IsAbsoluteUri || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)) throw new ArgumentException("Only absolute HTTP(S) endpoints are allowed.", parameterName);
        if (!string.IsNullOrEmpty(baseUri.UserInfo) || !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment)) throw new ArgumentException("Endpoint credentials and query strings are not allowed.", parameterName);
        var absolute = baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? baseUri.AbsoluteUri : baseUri.AbsoluteUri + "/";
        return new Uri(absolute, UriKind.Absolute);
    }

    internal static bool IsLoopbackHost(Uri baseUri)
    {
        var host = baseUri.IdnHost;
        return StringComparer.OrdinalIgnoreCase.Equals(host, "localhost")
            || StringComparer.Ordinal.Equals(host, "127.0.0.1")
            || StringComparer.Ordinal.Equals(host, "::1");
    }
}
