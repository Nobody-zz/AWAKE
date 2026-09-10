using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal interface IAuthoringDraftProvider
{
    string ProviderId { get; }
    Task<AuthoringDraftResult> GenerateAsync(AuthoringDraftRequest request, CancellationToken cancellationToken = default);
}

internal static class AuthoringDraftProviderFactory
{
    public static IAuthoringDraftProvider Create(string providerId, ProviderConfiguration? configuration = null)
    {
        var settings = configuration ?? ProviderConfiguration.FromEnvironment();
        return providerId.Trim().ToLowerInvariant() switch
        {
            "cloud" => new OpenAICompatibleAuthoringDraftProvider(settings),
            "local" => new LocalWorkerAuthoringDraftProvider(settings),
            _ => throw new InvalidOperationException("WB-AI-PROVIDER-400: 未知 AI Provider。")
        };
    }
}

internal sealed class OpenAICompatibleAuthoringDraftProvider : IAuthoringDraftProvider
{
    private const int MaxResponseBytes = 2 * 1024 * 1024;
    private readonly ProviderConfiguration _configuration;
    private readonly Func<HttpMessageHandler>? _testHandlerFactory;

    public OpenAICompatibleAuthoringDraftProvider(ProviderConfiguration configuration)
        : this(configuration, null)
    {
    }

    private OpenAICompatibleAuthoringDraftProvider(ProviderConfiguration configuration, Func<HttpMessageHandler>? testHandlerFactory)
    {
        _configuration = configuration;
        _testHandlerFactory = testHandlerFactory;
    }

    public string ProviderId => "cloud";

    public static OpenAICompatibleAuthoringDraftProvider ForTesting(ProviderConfiguration configuration, Func<HttpMessageHandler> handlerFactory)
        => new(configuration, handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory)));

    public async Task<AuthoringDraftResult> GenerateAsync(AuthoringDraftRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Intent?.Mode == AuthoringDraftMode.SemanticMigration)
            throw new InvalidOperationException("WB-AI-DRAFT-MODE-501: Semantic Migration 当前由独立迁移工作流处理。");
        if (!Uri.TryCreate(_configuration.CloudBaseUrl, UriKind.Absolute, out var baseUri)) throw new InvalidOperationException("WB-AI-ENDPOINT-403: 云端 Provider 地址无效。" );
        ProviderEndpointPolicy.ValidateCloudBaseUri(baseUri);
        var endpoint = ProviderHttpTransport.BuildCloudEndpoint(baseUri);
        var apiKey = _configuration.ResolveCloudApiKey();
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("WB-AI-AUTH-401: 云端 API 凭据不可用。" );

        HttpMessageHandler? handler = null;
        try
        {
            handler = _testHandlerFactory is null ? await ProviderHttpTransport.CreateCloudHandlerAsync(endpoint, cancellationToken).ConfigureAwait(false) : _testHandlerFactory();
            using var client = new HttpClient(handler, true) { Timeout = TimeSpan.FromSeconds(_configuration.CloudTimeoutSeconds) };
            handler = null;
            var useCompletionTokens = false;
            var includeJsonMode = true;
            var includeReasoningEffort = true;
            var reasoningEffort = _configuration.CloudReasoningEffort;
            var maxTokens = _configuration.CloudMaxTokens;
            var outputRetryCount = 0;
            var failedConfigurations = new HashSet<string>(StringComparer.Ordinal);
            ProviderHttpResponse? lastFailureResponse = null;
            InvalidOperationException? lastProcessingError = null;
            for (var attempt = 0; attempt < 7; attempt++)
            {
                var payload = AuthoringDraftRequestSerializer.BuildChatRequest(request, _configuration.CloudModel!, maxTokens, useCompletionTokens, _configuration.CloudTemperature, reasoningEffort, includeJsonMode, includeReasoningEffort);
                var response = await SendAsync(client, endpoint, apiKey, payload, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    lastFailureResponse = response;
                    var configurationKey = $"{useCompletionTokens}:{includeJsonMode}:{includeReasoningEffort}:{reasoningEffort}:{maxTokens}";
                    if (!failedConfigurations.Add(configurationKey))
                        throw ProviderErrorMapping.MapCloud(response.StatusCode, response.Body);
                }
                if (!response.IsSuccessStatusCode && useCompletionTokens && IsUnsupportedParameter(response.StatusCode, response.Body, "max_completion_tokens"))
                {
                    useCompletionTokens = false;
                    continue;
                }
                if (!response.IsSuccessStatusCode && !useCompletionTokens && IsUnsupportedTokenParameter(response.StatusCode, response.Body))
                {
                    useCompletionTokens = true;
                    continue;
                }
                if (!response.IsSuccessStatusCode && includeReasoningEffort && IsUnsupportedParameter(response.StatusCode, response.Body, "reasoning_effort"))
                {
                    includeReasoningEffort = false;
                    continue;
                }
                if (!response.IsSuccessStatusCode && includeJsonMode && IsUnsupportedParameter(response.StatusCode, response.Body, "response_format"))
                {
                    includeJsonMode = false;
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw ProviderErrorMapping.MapCloud(response.StatusCode, response.Body);
                try
                {
                    var result = ParseResult(response.Body, request);
                    ValidateBinding(request, result);
                    return result;
                }
                catch (InvalidOperationException error) when (IsRecoverableOutputFailure(error.Message) && outputRetryCount < 2)
                {
                    lastProcessingError = error;
                    outputRetryCount++;
                    reasoningEffort = "none";
                    includeReasoningEffort = true;
                    useCompletionTokens = true;
                    if (IsLikelyTruncatedOutput(error.Message) && maxTokens < 8000)
                        maxTokens = Math.Min(8000, Math.Max(maxTokens + 1024, maxTokens * 2));
                }
            }
            if (lastFailureResponse is not null) throw ProviderErrorMapping.MapCloud(lastFailureResponse.StatusCode, lastFailureResponse.Body);
            throw lastProcessingError ?? new InvalidOperationException("WB-AI-HTTP-ERROR: Provider 请求未能完成。" );
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("WB-AI-CANCELLED: AI 请求已取消。" );
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("WB-AI-TIMEOUT-408: Provider 请求超时。" );
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("WB-AI-TRANSPORT-502: 无法连接云端 Provider。" );
        }
        finally
        {
            handler?.Dispose();
        }
    }

    private static async Task<ProviderHttpResponse> SendAsync(HttpClient client, Uri endpoint, string apiKey, JsonObject payload, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var body = await ProviderHttpTransport.ReadBodyAsync(response, MaxResponseBytes, cancellationToken).ConfigureAwait(false);
        return new ProviderHttpResponse(response.StatusCode, body);
    }

    private AuthoringDraftResult ParseResult(string body, AuthoringDraftRequest request)
    {
        JsonNode node;
        try { node = JsonNode.Parse(body) ?? throw new JsonException(); }
        catch (JsonException) { throw new InvalidOperationException("WB-AI-DRAFT-FORMAT-JSON: Provider 响应不是合法 JSON。" ); }
        var result = AuthoringDraftResultParser.Parse(AuthoringDraftResponseNormalizer.Normalize(OpenAICompatibleResponseParser.ExtractJson(node), request));
        return result with { CandidateSet = AuthoringLifecycleFactory.FromDraftResult(request, result, AuthoringLifecycleFactory.ResolveProviderFingerprint(_configuration, request.ProviderId)) };
    }

    private static void ValidateBinding(AuthoringDraftRequest request, AuthoringDraftResult result)
    {
        if (!string.Equals(request.RequestHash, result.RequestHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(request.SourceContentHash, result.SourceContentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-DRAFT-BIND-409: Provider 结果与当前参考资料不匹配。" );
    }

    private static bool IsUnsupportedTokenParameter(HttpStatusCode statusCode, string body)
    {
        if (statusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)) return false;
        var normalized = body.ToLowerInvariant();
        return normalized.Contains("max_tokens", StringComparison.Ordinal) && (normalized.Contains("unsupported", StringComparison.Ordinal) || normalized.Contains("unknown parameter", StringComparison.Ordinal) || normalized.Contains("unrecognized parameter", StringComparison.Ordinal) || normalized.Contains("not allowed", StringComparison.Ordinal));
    }

    private static bool IsUnsupportedParameter(HttpStatusCode statusCode, string body, string parameter)
    {
        if (statusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)) return false;
        var normalized = body.ToLowerInvariant();
        return normalized.Contains(parameter, StringComparison.Ordinal)
            && (normalized.Contains("unsupported", StringComparison.Ordinal)
                || normalized.Contains("unknown parameter", StringComparison.Ordinal)
                || normalized.Contains("unrecognized parameter", StringComparison.Ordinal)
                || normalized.Contains("not allowed", StringComparison.Ordinal)
                || normalized.Contains("additional propert", StringComparison.Ordinal));
    }

    private static bool IsRecoverableOutputFailure(string message)
        => message.StartsWith("WB-AI-FORMAT-EMPTY:", StringComparison.Ordinal)
            || message.StartsWith("WB-AI-FORMAT-JSON:", StringComparison.Ordinal)
            || message.StartsWith("WB-AI-DRAFT-FORMAT-JSON:", StringComparison.Ordinal)
            || message.StartsWith("WB-AI-OUTPUT-LENGTH-422:", StringComparison.Ordinal);

    private static bool IsLikelyTruncatedOutput(string message)
        => message.StartsWith("WB-AI-OUTPUT-LENGTH-422:", StringComparison.Ordinal)
            || message.Contains("finish_reason=length", StringComparison.Ordinal)
            || message.StartsWith("WB-AI-FORMAT-JSON:", StringComparison.Ordinal);
}

internal sealed class LocalWorkerAuthoringDraftProvider : IAuthoringDraftProvider
{
    private const int MaxResponseBytes = 2 * 1024 * 1024;
    private readonly ProviderConfiguration _configuration;
    private readonly Func<HttpMessageHandler>? _testHandlerFactory;
    private readonly HashSet<string> _usedNonces = new(StringComparer.Ordinal);
    private readonly object _nonceGate = new();

    public LocalWorkerAuthoringDraftProvider(ProviderConfiguration configuration)
        : this(configuration, null)
    {
    }

    private LocalWorkerAuthoringDraftProvider(ProviderConfiguration configuration, Func<HttpMessageHandler>? testHandlerFactory)
    {
        _configuration = configuration;
        _testHandlerFactory = testHandlerFactory;
    }

    public string ProviderId => "local";

    public static LocalWorkerAuthoringDraftProvider ForTesting(ProviderConfiguration configuration, Func<HttpMessageHandler> handlerFactory)
        => new(configuration, handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory)));

    public async Task<AuthoringDraftResult> GenerateAsync(AuthoringDraftRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Intent?.Mode == AuthoringDraftMode.SemanticMigration)
            throw new InvalidOperationException("WB-AI-DRAFT-MODE-501: Semantic Migration 当前由独立迁移工作流处理。");
        if (!Uri.TryCreate(_configuration.LocalWorkerUrl, UriKind.Absolute, out var baseUri)) throw new InvalidOperationException("WB-AI-ENDPOINT-403: 本机 Worker 地址无效。" );
        ProviderEndpointPolicy.ValidateLocalWorkerUri(baseUri);
        var secret = _configuration.ResolveLocalWorkerSecret();
        if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("WB-AI-WORKER-SECRET-401: 本机 Worker secret 不可用。" );
        var handshakeEndpoint = ProviderHttpTransport.AppendPath(baseUri, "awake/handshake");
        var analyzeEndpoint = ProviderHttpTransport.AppendPath(baseUri, "awake/analyze");
        HttpMessageHandler? handler = null;
        try
        {
            handler = _testHandlerFactory is null ? ProviderHttpTransport.CreateLoopbackHandler() : _testHandlerFactory();
            using var client = new HttpClient(handler, true) { Timeout = TimeSpan.FromSeconds(_configuration.WorkerTimeoutSeconds ?? _configuration.CloudTimeoutSeconds) };
            handler = null;
            var handshake = await WorkerHandshake.PerformAsync(client, handshakeEndpoint, secret, request.RequestHash, ReserveNonce, cancellationToken).ConfigureAwait(false);
            var payload = AuthoringDraftRequestSerializer.BuildWorkerRequest(request, handshake);
            using var message = new HttpRequestMessage(HttpMethod.Post, analyzeEndpoint) { Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json") };
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var body = await ProviderHttpTransport.ReadBodyAsync(response, MaxResponseBytes, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw ProviderErrorMapping.MapLocalWorker(response.StatusCode, body, "草稿生成");
            JsonNode node;
            try { node = JsonNode.Parse(body) ?? throw new JsonException(); }
            catch (JsonException) { throw new InvalidOperationException("WB-AI-DRAFT-FORMAT-JSON: Worker 响应不是合法 JSON。" ); }
            var result = AuthoringDraftResultParser.Parse(AuthoringDraftResponseNormalizer.Normalize(node, request));
            if (!string.Equals(request.RequestHash, result.RequestHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(request.SourceContentHash, result.SourceContentHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("WB-AI-DRAFT-BIND-409: Worker 结果与当前参考资料不匹配。" );
            return result with { CandidateSet = AuthoringLifecycleFactory.FromDraftResult(request, result, AuthoringLifecycleFactory.ResolveProviderFingerprint(_configuration, request.ProviderId)) };
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("WB-AI-CANCELLED: AI 请求已取消。" );
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("WB-AI-WORKER-TIMEOUT-408: Worker 请求超时。" );
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("WB-AI-WORKER-TRANSPORT-502: 无法连接本机 Worker。" );
        }
        finally
        {
            handler?.Dispose();
        }
    }

    private bool ReserveNonce(string nonce)
    {
        lock (_nonceGate) return _usedNonces.Add(nonce);
    }
}
