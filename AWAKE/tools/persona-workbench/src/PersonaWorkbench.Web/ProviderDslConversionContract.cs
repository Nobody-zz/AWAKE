using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PersonaWorkbench.Web;

public sealed class ProviderDslConversionRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SourceText { get; set; } = string.Empty;
    public string LocalId { get; set; } = string.Empty;
    public string LocalDisplayName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string ProviderProtocol { get; set; } = ProviderProtocolKind.Ollama;
}

public sealed class ProviderDslConversionResult
{
    public ProviderDraftStatus Status { get; init; }
    public string CandidateJson { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public DateTimeOffset? CooldownUntilUtc { get; init; }
    public ProviderUsage? Usage { get; init; }
}

public interface IProviderDslConversionClient
{
    Task<ProviderDslConversionResult> ConvertAsync(ProviderDslConversionRequest request, CancellationToken cancellationToken = default);
}

public sealed class ProviderDslConversionClient : IProviderDslConversionClient
{
    private const int MaximumResponseBytes = 128 * 1024;
    private const int MaximumSourceBytes = ProviderRequestBudget.DslMaximumInputBytes;
    private const int MaximumCandidateBytes = 64 * 1024;
    internal static string ConversionSystemPrompt =>
        "Return exactly one JSON object and nothing else. This is a sparse evidence candidate, not final Persona DSL. Start with the smallest useful result, but do not collapse directly supported semantic categories into core or summary. When the source explicitly supports a category, populate its corresponding field: identityFacts for identity, publicDescription for public conduct and appearance, privateDescription for hidden conduct, contradictionDescription for public/private conflict, reaction for pressure responses, commitment for promises and values, axes for disposition, and flags for trigger/boundary mechanisms. Omit only unsupported categories, do not repeat the source, and do not copy the same excerpt into multiple fields. Allowed root fields: summary, identityFacts, publicDescription, privateDescription, contradictionDescription, reaction, commitment, axes, flags. "
        + "All prose fields summary, identityFacts, publicDescription, privateDescription, and contradictionDescription must be one string or null, never an array or object. reaction is an object or null with only sensitiveConditions and conditionalResponses. commitment is an object or null with only priorityOrder, protectedValues, applicableScope, exceptionCost, breachResponse. Omit unsupported fields or use null; never replace these objects with strings or arrays. Only axes and flags may be arrays. "
        + "Every prose value must be a short same-language excerpt copied word-for-word from the source in original order; use exact source wording, never paraphrase, translate, summarize, invent, explain, or combine rewritten phrases. If exact wording is unavailable, omit the field or use null. Keep core as the central personality evidence, not a dump of the whole source. For every non-empty source, identityFacts must be one contiguous exact source phrase of at least 4 characters; prefer the first quoted title or the first source clause. Never leave both summary and identityFacts null. Axes and flags may both be empty when unsupported, but do not return an empty candidate when identity evidence exists. Never repeat the whole source or emit Markdown, pseudo-JSON, DSL, IDs, names, tags, metadata, or commentary. "
        + "Preserve supported contradictions and unpleasant motives in the existing fields; do not reconcile, sanitize, or morally improve them. "
        + "axes is an array of at most 8 entries, each exactly index,value,source; flags is an array of at most 2 entries, each exactly index,source. value is -2,-1,1,2; source is an exact supporting source phrase. Omit unsupported entries. "
        + "Axis direction is fixed: the left term is negative, the right term is positive. Map: 0 caution bold/cautious; 1 ambition content/ambitious; 2 pride humble/proud; 3 pragmatism idealistic/pragmatic; 4 in-group loyalty independent/guardian; 5 tradition novelty/traditional; 6 restraint spontaneous/measured; 7 directness indirect/direct; 8 formality casual/formal; 9 playfulness serious/teasing; 10 warmth aloof/warm; 11 conditionality giving-first/bargaining-first; 12 deliberation act-first/observe-first; 13 trust testing trusting/loyalty-testing; 14 leverage reveals-cards/keeps-leverage; 15 in-group priority impartial/protects-in-group; 16 leadership cooperative/commanding; 17 confrontation avoidant/confrontational; 18 emotional expression suppressed/externalized; 19 timing delayed/immediate; 20 resentment reconciling/resentful; 21 support seeking self-reliant/support-seeking; 22 promise caution easy/cautious; 23 promise persistence flexible/persistent; 24 value tradeability tradeable/non-tradeable. "
        + "Flag 0 requires both public humiliation and a supported retaliation response. Flag 1 requires evidence of refusing empty promises or not promising lightly. Never output duplicate indices or entries without matching source evidence.";
    private readonly HttpClient _httpClient;
    private readonly IProviderEndpointResolver _endpointResolver;
    private readonly TimeSpan _requestTimeout;
    private readonly ProviderRequestCapture? _requestCapture;
    private readonly ProviderRequestGate _requestGate = new ProviderRequestGate();

    public ProviderDslConversionClient(HttpMessageHandler transport, IProviderEndpointResolver? endpointResolver = null, TimeSpan? requestTimeout = null, ProviderRequestCapture? requestCapture = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _httpClient = new HttpClient(transport, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _endpointResolver = endpointResolver ?? new DnsProviderEndpointResolver();
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(ProviderRequestBudget.DefaultRequestTimeoutSeconds);
        _requestCapture = requestCapture;
        if (_requestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
    }

    public async Task<ProviderDslConversionResult> ConvertAsync(ProviderDslConversionRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Failure(ProviderDraftStatus.Cancelled, "provider.cancelled");
        if (request == null) return Failure(ProviderDraftStatus.InvalidRequest, "provider.intermediate_request_invalid");
        if (!ProviderEndpointPolicy.TryValidate(request.Endpoint, out Uri endpoint, out string endpointError)
            || string.IsNullOrWhiteSpace(request.Model)
            || string.IsNullOrWhiteSpace(request.SourceText)
            || Encoding.UTF8.GetByteCount(request.SourceText) > MaximumSourceBytes
            || !ProviderProtocolKind.TryNormalize(request.ProviderProtocol, out string providerProtocol))
        {
            return Failure(ProviderDraftStatus.InvalidRequest, string.IsNullOrEmpty(endpointError) ? "provider.intermediate_request_invalid" : endpointError);
        }

        if (!_requestGate.TryEnter(out ProviderRequestGateStatus gateStatus, out DateTimeOffset? cooldownUntilUtc))
        {
            return gateStatus == ProviderRequestGateStatus.Busy
                ? Failure(ProviderDraftStatus.Busy, "provider.request_in_flight")
                : Failure(ProviderDraftStatus.CoolingDown, "provider.cooldown_active", cooldownUntilUtc);
        }

        try
        {
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_requestTimeout);
            CancellationToken effectiveCancellationToken = deadline.Token;
            if (!await ProviderEndpointGuard.IsSafeResolvedEndpointAsync(_endpointResolver, endpoint, effectiveCancellationToken).ConfigureAwait(false)) return Failure(ProviderDraftStatus.TransportError, "provider.endpoint_resolution_rejected");

            Uri nativeEndpoint = endpoint;
            bool useNativeOllama = ProviderProtocolKind.IsOllama(providerProtocol)
                && ProviderRequestBudget.TryGetOllamaNativeEndpoint(endpoint, out nativeEndpoint);
            Uri requestEndpoint = useNativeOllama ? nativeEndpoint : endpoint;
            int outputTokens = ProviderRequestBudget.SelectDslTokens(request.SourceText);
            Dictionary<string, object> requestPayload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["model"] = request.Model.Trim(),
                ["messages"] = new[]
                {
                    new { role = "system", content = ConversionSystemPrompt },
                    new { role = "user", content = request.SourceText.Trim() }
                }
            };
            if (useNativeOllama)
            {
                requestPayload["stream"] = false;
                requestPayload["think"] = "low";
                requestPayload["format"] = BuildOllamaJsonSchema();
                requestPayload["options"] = new { num_ctx = ProviderRequestBudget.SelectDslContextTokens(request.SourceText), num_predict = outputTokens, temperature = 0, seed = 42 };
            }
            else
            {
                requestPayload["response_format"] = new { type = "json_object" };
                requestPayload["max_tokens"] = outputTokens;
                requestPayload["temperature"] = 0;
                if (ProviderProtocolKind.IsOllama(providerProtocol) && ProviderEndpointPolicy.IsLoopbackEndpoint(endpoint)) requestPayload["think"] = "low";
            }

            using HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, requestEndpoint) { Content = JsonContent.Create(requestPayload) };
            if (!string.IsNullOrWhiteSpace(request.ApiKey)) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
            byte[] requestBytes = await message.Content!.ReadAsByteArrayAsync(effectiveCancellationToken).ConfigureAwait(false);
            _requestCapture?.TryCaptureCurrent("convert", providerProtocol, ProviderEndpointPolicy.IsLoopbackEndpoint(endpoint) ? "loopback" : "cloud", request.Model.Trim(), request.SourceText, requestBytes);
            using HttpResponseMessage response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400) return Failure(ProviderDraftStatus.RedirectRejected, "provider.redirect_rejected");
            if (response.StatusCode == (HttpStatusCode)429)
            {
                DateTimeOffset cooldownUntil = ProviderRequestGate.CalculateCooldown(response.Headers.RetryAfter);
                _requestGate.SetCooldown(cooldownUntil);
                return Failure(ProviderDraftStatus.RateLimited, "provider.rate_limited", cooldownUntil);
            }
            if (!response.IsSuccessStatusCode) return Failure(ProviderDraftStatus.TransportError, "provider.http_" + (int)response.StatusCode);
            if (!await ProviderEndpointGuard.IsSafeResolvedEndpointAsync(_endpointResolver, endpoint, effectiveCancellationToken).ConfigureAwait(false)) return Failure(ProviderDraftStatus.TransportError, "provider.endpoint_resolution_rejected");

            string responseText = await response.Content.ReadAsStringAsync(effectiveCancellationToken).ConfigureAwait(false);
            if (!TryReadCandidateJson(responseText, out string candidateJson, out string parseError, out ProviderUsage? usage)) return Failure(ProviderDraftStatus.ResponseInvalid, parseError, usage: usage);
            return new ProviderDslConversionResult { Status = ProviderDraftStatus.Success, CandidateJson = candidateJson, Usage = usage };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(ProviderDraftStatus.Cancelled, "provider.cancelled");
        }
        catch (OperationCanceledException)
        {
            return Failure(ProviderDraftStatus.TransportError, "provider.timeout");
        }
        catch (HttpRequestException)
        {
            return Failure(ProviderDraftStatus.TransportError, "provider.transport_error");
        }
        finally
        {
            _requestGate.Exit();
        }
    }

    private static object BuildOllamaJsonSchema()
    {
        Dictionary<string, object> nullableText = new(StringComparer.Ordinal)
        {
            ["type"] = new[] { "string", "null" }
        };
        Dictionary<string, object> axisEntry = new(StringComparer.Ordinal)
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new[] { "index", "value", "source" },
            ["properties"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["index"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 24 },
                ["value"] = new Dictionary<string, object> { ["type"] = "integer", ["enum"] = new[] { -2, -1, 1, 2 } },
                ["source"] = new Dictionary<string, object> { ["type"] = "string", ["minLength"] = 1 }
            }
        };
        Dictionary<string, object> flagEntry = new(StringComparer.Ordinal)
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new[] { "index", "source" },
            ["properties"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["index"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 1 },
                ["source"] = new Dictionary<string, object> { ["type"] = "string", ["minLength"] = 1 }
            }
        };
        Dictionary<string, object> reaction = new(StringComparer.Ordinal)
        {
            ["type"] = new[] { "object", "null" },
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sensitiveConditions"] = nullableText,
                ["conditionalResponses"] = nullableText
            }
        };
        Dictionary<string, object> commitment = new(StringComparer.Ordinal)
        {
            ["type"] = new[] { "object", "null" },
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["priorityOrder"] = nullableText,
                ["protectedValues"] = nullableText,
                ["applicableScope"] = nullableText,
                ["exceptionCost"] = nullableText,
                ["breachResponse"] = nullableText
            }
        };
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new[] { "identityFacts" },
            ["properties"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["summary"] = nullableText,
                ["identityFacts"] = new Dictionary<string, object> { ["type"] = "string", ["minLength"] = 4 },
                ["publicDescription"] = nullableText,
                ["privateDescription"] = nullableText,
                ["contradictionDescription"] = nullableText,
                ["reaction"] = reaction,
                ["commitment"] = commitment,
                ["axes"] = new Dictionary<string, object> { ["type"] = "array", ["maxItems"] = 8, ["items"] = axisEntry },
                ["flags"] = new Dictionary<string, object> { ["type"] = "array", ["maxItems"] = 2, ["items"] = flagEntry }
            }
        };
    }
    private static bool TryReadCandidateJson(string responseText, out string candidateJson, out string errorCode, out ProviderUsage? usage)
    {
        candidateJson = string.Empty;
        ProviderChatResponseReadResult response = ProviderChatResponseReader.Read(responseText, MaximumResponseBytes);
        usage = response.Usage;
        if (!response.IsSuccess)
        {
            errorCode = response.Status switch
            {
                ProviderChatResponseReadStatus.ResponseSizeInvalid => "provider.intermediate_response_size_invalid",
                ProviderChatResponseReadStatus.ResponseShapeInvalid => "provider.intermediate_response_content_missing",
                ProviderChatResponseReadStatus.Truncated => "provider.intermediate_candidate_truncated",
                ProviderChatResponseReadStatus.Empty => "provider.intermediate_candidate_empty",
                ProviderChatResponseReadStatus.JsonInvalid => "provider.intermediate_response_json_invalid",
                _ => "provider.intermediate_response_invalid"
            };
            return false;
        }

        string normalized = NormalizeCandidate(response.Content);
        if (string.IsNullOrWhiteSpace(normalized)) { errorCode = "provider.intermediate_candidate_empty"; return false; }
        if (Encoding.UTF8.GetByteCount(normalized) > MaximumCandidateBytes) { errorCode = "provider.intermediate_candidate_size_invalid"; return false; }
        if (!TryExtractJsonObject(normalized, out candidateJson))
        {
            bool hasJsonSyntax = normalized.Contains('{') || normalized.Contains('[') || normalized.StartsWith('"');
            errorCode = hasJsonSyntax
                ? "provider.intermediate_candidate_format_invalid"
                : "provider.intermediate_candidate_object_missing";
            return false;
        }
        errorCode = string.Empty;
        return true;
    }
    private static bool TryExtractJsonObject(string value, out string candidateJson)
    {
        return TryExtractJsonObject(value, true, out candidateJson);
    }

    private static bool TryExtractJsonObject(string value, bool allowStringDecode, out string candidateJson)
    {
        candidateJson = string.Empty;
        if (TryParseJsonObject(value, out candidateJson)) return true;
        if (allowStringDecode && TryDecodeJsonString(value, out string decoded))
        {
            string normalizedDecoded = NormalizeCandidate(decoded);
            if (TryExtractJsonObject(normalizedDecoded, false, out candidateJson)) return true;
        }

        bool inString = false;
        bool escaped = false;
        int depth = 0;
        int start = -1;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '"') inString = false;
                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }
            if (character == '{')
            {
                if (depth == 0) start = index;
                depth++;
                continue;
            }
            if (character != '}' || depth == 0) continue;

            depth--;
            if (depth != 0 || start < 0) continue;
            string candidate = value[start..(index + 1)].Trim();
            if (TryParseJsonObject(candidate, out candidateJson)) return true;
            start = -1;
        }
        return false;
    }

    private static bool TryDecodeJsonString(string value, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.String) return false;
            decoded = document.RootElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(decoded);
        }
        catch (JsonException)
        {
            return false;
        }
    }
    private static bool TryParseJsonObject(string value, out string candidateJson)
    {
        candidateJson = string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            candidateJson = value.Trim();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
    private static string NormalizeCandidate(string value)
    {
        string normalized = value.Trim().TrimStart('\uFEFF');
        if (!normalized.StartsWith("```", StringComparison.Ordinal) || !normalized.EndsWith("```", StringComparison.Ordinal)) return normalized;
        int firstLineEnd = normalized.IndexOf('\n');
        if (firstLineEnd < 0) return normalized;
        normalized = normalized[(firstLineEnd + 1)..].Trim();
        return normalized.EndsWith("```", StringComparison.Ordinal) ? normalized[..^3].Trim() : normalized;
    }


    private static ProviderDslConversionResult Failure(ProviderDraftStatus status, string errorCode, DateTimeOffset? cooldownUntilUtc = null, ProviderUsage? usage = null)
    {
        return new ProviderDslConversionResult { Status = status, ErrorCode = errorCode, CooldownUntilUtc = cooldownUntilUtc, Usage = usage };
    }
}



