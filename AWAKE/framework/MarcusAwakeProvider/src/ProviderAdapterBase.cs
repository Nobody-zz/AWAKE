using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeTransport;

namespace MarcusAwakeProvider;

public abstract class ProviderAdapterBase : IProviderAdapter
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private readonly HttpMessageInvoker invoker;
    private readonly IProviderEndpointPolicy endpointPolicy;
    private readonly IProviderClock clock;

    protected ProviderAdapterBase(
        ProviderConnectionProfile profile,
        HttpMessageInvoker invoker,
        IProviderEndpointPolicy? endpointPolicy = null,
        ProviderLimits? limits = null,
        IProviderClock? clock = null)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        this.invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        this.endpointPolicy = endpointPolicy ?? new ExactOriginEndpointPolicy();
        Limits = limits ?? new ProviderLimits();
        this.clock = clock ?? SystemProviderClock.Instance;
    }

    public ProviderConnectionProfile Profile { get; }
    protected ProviderLimits Limits { get; }
    protected IProviderClock Clock => clock;

    public abstract Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default);
    public abstract Task<ProviderResult<ProviderCompletion>> CompleteAsync(ProviderChatRequest request, ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default);
    public abstract IAsyncEnumerable<ProviderStreamEvent> StreamAsync(ProviderChatRequest request, ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default);

    protected virtual Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsForConnectionAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        return ListModelsAsync(credential, deadline, cancellationToken);
    }

    protected async Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsFromEndpointAsync(
        string endpointPath,
        string responseProperty,
        ApiKeyCredential? credential,
        DateTimeOffset deadline,
        CancellationToken cancellationToken,
        bool deduplicate)
    {
        var credentialError = ValidateCredential(credential);
        if (credentialError != null) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(credentialError);
        var responseResult = await SendAsync(HttpMethod.Get, endpointPath, credential, "models", deadline, cancellationToken, "application/json", Profile.DefaultModel).ConfigureAwait(false);
        if (!responseResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(responseResult.Error!);
        using var response = responseResult.Value!;
        var bodyResult = await ReadBoundedResponseAsync(response, "models", Profile.DefaultModel, cancellationToken, deadline, Limits.MaxResponseBytes).ConfigureAwait(false);
        if (!bodyResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(bodyResult.Error!);
        var documentResult = ParseJson(bodyResult.Value!, "models", Profile.DefaultModel);
        if (!documentResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(documentResult.Error!);
        using var document = documentResult.Value!;
        return ProviderResponseSupport.ParseModels(document, responseProperty, Profile.ProviderId, Limits.MaxModels, deduplicate);
    }

    /// <summary>
    /// 默认连通性检查：列模型 + 如实报「探过/没探过」的能力表。
    /// <c>virtual</c> 是为了让**只提供一部分能力**的适配器纠正基类的默认假设
    /// （例如只生图的 Player2 不能继承「有文字能力」这一条）。
    /// </summary>
    public virtual async Task<ProviderResult<ProviderConnectivityResult>> TestConnectionAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        var models = await ListModelsForConnectionAsync(credential, deadline, cancellationToken).ConfigureAwait(false);
        if (!models.IsSuccess) return ProviderResult<ProviderConnectivityResult>.Failed(models.Error!);
        var capabilities = new Dictionary<ProviderCapabilityId, ProviderCapabilityState>
        {
            [ProviderCapabilityId.ModelDiscovery] = ProviderCapabilityState.Available,
            [ProviderCapabilityId.TextGeneration] = ProviderCapabilityState.Unverified,
            [ProviderCapabilityId.Streaming] = ProviderCapabilityState.Unverified,
            [ProviderCapabilityId.Usage] = ProviderCapabilityState.Unverified,
            [ProviderCapabilityId.StructuredOutput] = ProviderCapabilityState.Unverified
        };
        return ProviderResult<ProviderConnectivityResult>.Succeeded(new ProviderConnectivityResult(Profile.ProviderId, models.Value!, new ProviderCapabilityReport(capabilities)));
    }

    protected ProviderError? ValidateCredential(ApiKeyCredential? credential)
    {
        if (Profile.CredentialReference == null) return null;
        if (credential == null) return new ProviderError("credential.missing", ProviderErrorCategory.Authentication, "Provider credential is missing.", false, Profile.ProviderId);
        if (credential.IsDisposed) return new ProviderError("credential.disposed", ProviderErrorCategory.Authentication, "Provider credential is disposed.", false, Profile.ProviderId);
        if (!StringComparer.Ordinal.Equals(Profile.CredentialReference, credential.Reference))
        {
            return new ProviderError("credential.reference_mismatch", ProviderErrorCategory.InvalidRequest, "Provider credential reference does not match the profile.", false, Profile.ProviderId);
        }

        return null;
    }

    protected ProviderError? ValidateChatRequest(ProviderChatRequest request, ApiKeyCredential? credential, out string model)
    {
        model = string.Empty;
        var credentialError = ValidateCredential(credential);
        if (credentialError != null) return credentialError;
        if (request == null) return new ProviderError("request.missing", ProviderErrorCategory.InvalidRequest, "Provider request is missing.", false, Profile.ProviderId);
        try
        {
            model = request.ResolveModel(Profile);
        }
        catch (ArgumentException)
        {
            return new ProviderError("request.model_invalid", ProviderErrorCategory.InvalidRequest, "Provider model is invalid.", false, Profile.ProviderId);
        }

        if (request.ResponseSchemaJson != null)
        {
            if (StrictUtf8.GetByteCount(request.ResponseSchemaJson) > Limits.MaxStructuredJsonBytes)
            {
                return new ProviderError("request.schema_too_large", ProviderErrorCategory.ResourceExhausted, "Structured output schema exceeds the configured limit.", false, Profile.ProviderId, model);
            }

            try
            {
                using var schema = JsonDocument.Parse(StrictUtf8.GetBytes(request.ResponseSchemaJson), new JsonDocumentOptions { MaxDepth = 64 });
                if (schema.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return new ProviderError("request.schema_invalid", ProviderErrorCategory.InvalidRequest, "Structured output schema must be a JSON object.", false, Profile.ProviderId, model);
                }
            }
            catch (JsonException)
            {
                return new ProviderError("request.schema_invalid", ProviderErrorCategory.InvalidRequest, "Structured output schema is invalid JSON.", false, Profile.ProviderId, model);
            }
        }

        return null;
    }

    protected ProviderError? ValidateRequestBody(byte[] body, string model)
    {
        return body.Length <= Limits.MaxRequestBytes
            ? null
            : new ProviderError("request.too_large", ProviderErrorCategory.ResourceExhausted, "Provider request exceeds the configured limit.", false, Profile.ProviderId, model);
    }

    protected ProviderResult<JsonDocument> ParseJson(byte[] body, string operation, string model)
    {
        try
        {
            return ProviderResult<JsonDocument>.Succeeded(JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 64 }));
        }
        catch (JsonException)
        {
            return ProviderResult<JsonDocument>.Failed(new ProviderError("response.json_invalid", ProviderErrorCategory.MalformedResponse, "Provider returned invalid JSON.", false, Profile.ProviderId, model, details: new Dictionary<string, string> { ["operation"] = operation }));
        }
    }

    protected ProviderResult<string> ValidateStructuredContent(string content, string model)
    {
        if (!StructuredJsonCanonicalizer.TryCanonicalizeObject(content, out var canonicalJson, out var structuredError))
        {
            var isTooLarge = StringComparer.Ordinal.Equals(structuredError, "structured_json_too_large");
            return ProviderResult<string>.Failed(new ProviderError(
                isTooLarge ? "response.structured_json_too_large" : "response.structured_json_invalid",
                isTooLarge ? ProviderErrorCategory.ResourceExhausted : ProviderErrorCategory.MalformedResponse,
                isTooLarge ? "Structured provider output exceeds the configured limit." : "Provider returned invalid structured JSON.",
                false,
                Profile.ProviderId,
                model));
        }

        return ProviderResult<string>.Succeeded(canonicalJson);
    }

    protected ProviderError? ValidateContentEncoding(HttpResponseMessage response, string? model = null)
    {
        if (response.Content.Headers.ContentEncoding.Any(value => !StringComparer.OrdinalIgnoreCase.Equals(value, "identity")))
        {
            return new ProviderError("response.compressed_unsupported", ProviderErrorCategory.Unsupported, "Compressed provider responses are not accepted.", false, Profile.ProviderId, model);
        }

        return null;
    }

    protected bool HasContentType(HttpResponseMessage response, string mediaType)
    {
        return response.Content.Headers.ContentType != null
            && StringComparer.OrdinalIgnoreCase.Equals(response.Content.Headers.ContentType.MediaType, mediaType);
    }

    protected ProviderStreamEvent Started(string model, long sequence) => new ProviderStreamEvent(ProviderStreamEventKind.Started, sequence, Profile.ProviderId, model, string.Empty, null, null, string.Empty, string.Empty);
    protected ProviderStreamEvent TextDelta(string model, string text, long sequence) => new ProviderStreamEvent(ProviderStreamEventKind.TextDelta, sequence, Profile.ProviderId, model, text, null, null, string.Empty, string.Empty);
    protected ProviderStreamEvent UsageUpdate(string model, ProviderUsage usage, long sequence) => new ProviderStreamEvent(ProviderStreamEventKind.UsageUpdate, sequence, Profile.ProviderId, model, string.Empty, usage, null, string.Empty, string.Empty);
    protected ProviderStreamEvent Completed(string model, string text, ProviderUsage? usage, long sequence) => new ProviderStreamEvent(ProviderStreamEventKind.Completed, sequence, Profile.ProviderId, model, text, usage, null, string.Empty, string.Empty);
    protected ProviderStreamEvent Cancelled(string model, ProviderError error, long sequence) => new ProviderStreamEvent(ProviderStreamEventKind.Cancelled, sequence, Profile.ProviderId, model, string.Empty, null, error, string.Empty, string.Empty);
    protected ProviderStreamEvent Failed(string model, ProviderError error, long sequence) => new ProviderStreamEvent(ProviderStreamEventKind.Failed, sequence, Profile.ProviderId, model, string.Empty, null, error, string.Empty, string.Empty);
    protected ProviderStreamEvent CompletedStructured(string model, string? structuredJson, ProviderUsage? usage, long sequence) => new ProviderStreamEvent(ProviderStreamEventKind.Completed, sequence, Profile.ProviderId, model, string.Empty, usage, null, string.Empty, string.Empty, structuredJson);

    protected Task<ProviderResult<HttpResponseMessage>> SendAsync(
        HttpMethod method,
        string endpointPath,
        ApiKeyCredential? credential,
        string operation,
        DateTimeOffset deadline,
        CancellationToken cancellationToken,
        string accept,
        string? model = null)
    {
        return SendRequestAsync(method, endpointPath, null, credential, operation, deadline, cancellationToken, accept, model);
    }

    protected Task<ProviderResult<HttpResponseMessage>> SendWithBodyAsync(
        HttpMethod method,
        string endpointPath,
        byte[] body,
        ApiKeyCredential? credential,
        string operation,
        DateTimeOffset deadline,
        CancellationToken cancellationToken,
        string accept,
        string? model = null,
        IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        if (body == null) throw new ArgumentNullException(nameof(body));
        return SendRequestAsync(method, endpointPath, body, credential, operation, deadline, cancellationToken, accept, model, extraHeaders);
    }

    private async Task<ProviderResult<HttpResponseMessage>> SendRequestAsync(
        HttpMethod method,
        string endpointPath,
        byte[]? body,
        ApiKeyCredential? credential,
        string operation,
        DateTimeOffset deadline,
        CancellationToken cancellationToken,
        string accept,
        string? model,
        IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        var cancellationError = GetCancellationError(cancellationToken, deadline);
        if (cancellationError != null) return ProviderResult<HttpResponseMessage>.Failed(cancellationError);
        var uri = Profile.BuildEndpointUri(endpointPath);
        var policy = endpointPolicy.Evaluate(Profile, uri, operation);
        if (!policy.IsSuccess) return ProviderResult<HttpResponseMessage>.Failed(policy.Error!);
        if (!policy.Value) return ProviderResult<HttpResponseMessage>.Failed(new ProviderError("endpoint.policy_denied", ProviderErrorCategory.PolicyDenied, "Endpoint denied by policy.", false, Profile.ProviderId, model));

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.AcceptEncoding.Clear();
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        if (extraHeaders != null)
        {
            foreach (var header in extraHeaders)
            {
                if (string.IsNullOrWhiteSpace(header.Key)) continue;
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (body != null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        ApplyAuthentication(request, credential);


        using var requestCancellation = CreateCancellationSource(cancellationToken, deadline);
        try
        {
            var response = await invoker.SendAsync(request, requestCancellation.Token).ConfigureAwait(false);
            if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
            {
                response.Dispose();
                return ProviderResult<HttpResponseMessage>.Failed(new ProviderError("http.redirect_rejected", ProviderErrorCategory.RedirectRejected, "Provider redirect responses are not followed.", false, Profile.ProviderId, model));
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = MapHttpFailure(response, operation, model);
                response.Dispose();
                return ProviderResult<HttpResponseMessage>.Failed(error);
            }

            var encodingError = ValidateContentEncoding(response, model);
            if (encodingError != null)
            {
                response.Dispose();
                return ProviderResult<HttpResponseMessage>.Failed(encodingError);
            }

            return ProviderResult<HttpResponseMessage>.Succeeded(response);
        }
        catch (OperationCanceledException)
        {
            return ProviderResult<HttpResponseMessage>.Failed(ClassifyCancellation(cancellationToken, deadline));
        }
        catch (HttpRequestException)
        {
            return ProviderResult<HttpResponseMessage>.Failed(new ProviderError("transport.unavailable", ProviderErrorCategory.TransportUnavailable, "Provider transport is unavailable.", true, Profile.ProviderId, model));
        }
        catch (ObjectDisposedException)
        {
            return ProviderResult<HttpResponseMessage>.Failed(new ProviderError("transport.unavailable", ProviderErrorCategory.TransportUnavailable, "Provider transport is unavailable.", true, Profile.ProviderId, model));
        }
    }

    protected async Task<ProviderResult<byte[]>> ReadBoundedResponseAsync(HttpResponseMessage response, string operation, string model, CancellationToken cancellationToken, DateTimeOffset deadline, int maximumBytes)
    {
        if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > maximumBytes)
        {
            return ProviderResult<byte[]>.Failed(new ProviderError("response.too_large", ProviderErrorCategory.ResourceExhausted, "Provider response exceeds the configured limit.", false, Profile.ProviderId, model, details: new Dictionary<string, string> { ["operation"] = operation }));
        }

        using var readCancellation = CreateCancellationSource(cancellationToken, deadline);
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(readCancellation.Token).ConfigureAwait(false);
            var buffer = ArrayPool<byte>.Shared.Rent(Math.Min(81920, Math.Max(1024, maximumBytes)));
            try
            {
                using var output = new MemoryStream(Math.Min(maximumBytes, 65536));
                while (true)
                {
                    var count = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), readCancellation.Token).ConfigureAwait(false);
                    if (count == 0) break;
                    if (output.Length + count > maximumBytes)
                    {
                        return ProviderResult<byte[]>.Failed(new ProviderError("response.too_large", ProviderErrorCategory.ResourceExhausted, "Provider response exceeds the configured limit.", false, Profile.ProviderId, model, details: new Dictionary<string, string> { ["operation"] = operation }));
                    }

                    output.Write(buffer, 0, count);
                }

                return ProviderResult<byte[]>.Succeeded(output.ToArray());
            }
            finally
            {
                Array.Clear(buffer, 0, buffer.Length);
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (OperationCanceledException)
        {
            return ProviderResult<byte[]>.Failed(ClassifyCancellation(cancellationToken, deadline));
        }
        catch (HttpRequestException)
        {
            return ProviderResult<byte[]>.Failed(new ProviderError("transport.unavailable", ProviderErrorCategory.TransportUnavailable, "Provider transport is unavailable.", true, Profile.ProviderId, model));
        }
        catch (IOException)
        {
            return ProviderResult<byte[]>.Failed(new ProviderError("transport.unavailable", ProviderErrorCategory.TransportUnavailable, "Provider transport is unavailable.", true, Profile.ProviderId, model));
        }
    }

    protected ProviderError MapHttpFailure(HttpResponseMessage response, string operation, string? model = null)
    {
        var status = (int)response.StatusCode;
        var retryAfter = ParseRetryAfter(response.Headers.RetryAfter);
        var details = new Dictionary<string, string>(StringComparer.Ordinal) { ["operation"] = operation };
        if (retryAfter.HasValue) details["retry_after_seconds"] = ((long)retryAfter.Value.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return status switch
        {
            400 or 422 => new ProviderError("http.invalid_request", ProviderErrorCategory.InvalidRequest, "Provider rejected the request.", false, Profile.ProviderId, model, status, details: details),
            401 => new ProviderError("http.authentication_failed", ProviderErrorCategory.Authentication, "Provider authentication failed.", false, Profile.ProviderId, model, status, details: details),
            403 => new ProviderError("http.forbidden", ProviderErrorCategory.Forbidden, "Provider denied the request.", false, Profile.ProviderId, model, status, details: details),
            404 => new ProviderError("http.not_found", ProviderErrorCategory.NotFound, "Provider endpoint or model was not found.", false, Profile.ProviderId, model, status, details: details),
            408 => new ProviderError("http.timeout", ProviderErrorCategory.Timeout, "Provider request timed out.", true, Profile.ProviderId, model, status, retryAfter, details),
            409 => new ProviderError("http.conflict", ProviderErrorCategory.Conflict, "Provider rejected the conflicting request.", false, Profile.ProviderId, model, status, details: details),
            429 => new ProviderError("http.rate_limited", ProviderErrorCategory.RateLimited, "Provider rate limit reached.", true, Profile.ProviderId, model, status, retryAfter, details),
            >= 500 and <= 599 => new ProviderError("http.server_unavailable", ProviderErrorCategory.ServerUnavailable, "Provider server is unavailable.", true, Profile.ProviderId, model, status, retryAfter, details),
            _ => new ProviderError("http.request_failed", ProviderErrorCategory.Unavailable, "Provider request failed.", false, Profile.ProviderId, model, status, details: details)
        };
    }

    private void ApplyAuthentication(HttpRequestMessage request, ApiKeyCredential? credential)
    {
        if (Profile.Kind == ProviderKind.Anthropic)
        {
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            if (credential != null) request.Headers.TryAddWithoutValidation("x-api-key", credential.CopySecretForRequest());
            return;
        }

        if (Profile.Kind == ProviderKind.Ollama) return;

        if (credential != null) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + credential.CopySecretForRequest());
    }

    private ProviderError? GetCancellationError(CancellationToken cancellationToken, DateTimeOffset deadline)
    {
        if (cancellationToken.IsCancellationRequested) return new ProviderError("request.cancelled", ProviderErrorCategory.Cancelled, "Provider request was cancelled.", false, Profile.ProviderId);
        if (clock.UtcNow >= deadline) return new ProviderError("request.deadline_expired", ProviderErrorCategory.Timeout, "Provider request deadline expired.", false, Profile.ProviderId);
        return null;
    }

    protected ProviderError ClassifyCancellation(CancellationToken cancellationToken, DateTimeOffset deadline)
    {
        return cancellationToken.IsCancellationRequested
            ? new ProviderError("request.cancelled", ProviderErrorCategory.Cancelled, "Provider request was cancelled.", false, Profile.ProviderId)
            : clock.UtcNow >= deadline
                ? new ProviderError("request.deadline_expired", ProviderErrorCategory.Timeout, "Provider request deadline expired.", false, Profile.ProviderId)
                : new ProviderError("request.cancelled", ProviderErrorCategory.Cancelled, "Provider request was cancelled.", false, Profile.ProviderId);
    }

    protected CancellationTokenSource CreateCancellationSource(CancellationToken cancellationToken, DateTimeOffset deadline)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = deadline - clock.UtcNow;
        var milliseconds = Math.Min(remaining.TotalMilliseconds, int.MaxValue - 1d);
        source.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1d, milliseconds)));
        return source;
    }

    private TimeSpan? ParseRetryAfter(RetryConditionHeaderValue? header)
    {
        if (header == null) return null;
        const double maximumSeconds = 300;
        if (header.Delta.HasValue)
        {
            var seconds = Math.Clamp(header.Delta.Value.TotalSeconds, 0, maximumSeconds);
            return TimeSpan.FromSeconds(seconds);
        }

        if (header.Date.HasValue)
        {
            var seconds = Math.Clamp((header.Date.Value - clock.UtcNow).TotalSeconds, 0, maximumSeconds);
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }
}
