using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PersonaWorkbench.Web;

public sealed class ProviderTextExpansionRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProviderTextExpansionControls? Controls { get; set; }
    public string? ApiKey { get; set; }
    public string ProviderProtocol { get; set; } = ProviderProtocolKind.Ollama;
}

public sealed class ProviderTextExpansionControls
{
    public string Direction { get; set; } = string.Empty;
    public string FocusPreset { get; set; } = "balanced";
    public List<ProviderTextExpansionKeyword> FocusKeywords { get; set; } = new List<ProviderTextExpansionKeyword>();
    public List<string> AvoidTopics { get; set; } = new List<string>();
}

public sealed class ProviderTextExpansionKeyword
{
    public string Text { get; set; } = string.Empty;
    public string Weight { get; set; } = "medium";
}

public static class ProviderTextExpansionControlNormalizer
{
    private static readonly HashSet<string> Presets = new HashSet<string>(StringComparer.Ordinal)
    {
        "balanced", "personality_behavior", "identity_experience", "relationship_emotion", "appearance_expression"
    };

    private static readonly Dictionary<string, int> Weights = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["light"] = 1,
        ["medium"] = 2,
        ["strong"] = 3
    };

    public static bool TryNormalize(ProviderTextExpansionControls? input, out ProviderTextExpansionControls normalized, out string errorCode)
    {
        normalized = new ProviderTextExpansionControls();
        errorCode = string.Empty;
        if (input == null) return true;

        string preset = (input.FocusPreset ?? string.Empty).Trim();
        if (preset.Length == 0) preset = "balanced";
        if (!Presets.Contains(preset))
        {
            errorCode = "provider.expansion_controls_invalid";
            return false;
        }

        string direction = NormalizeLineEndings(input.Direction ?? string.Empty);
        if (Encoding.UTF8.GetByteCount(direction) > 2 * 1024)
        {
            errorCode = "provider.expansion_controls_invalid";
            return false;
        }

        List<ProviderTextExpansionKeyword> keywords = new List<ProviderTextExpansionKeyword>();
        Dictionary<string, int> keywordIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (ProviderTextExpansionKeyword? item in input.FocusKeywords ?? new List<ProviderTextExpansionKeyword>())
        {
            if (item == null) continue;
            string text = NormalizeLineEndings(item.Text ?? string.Empty).Trim();
            string weight = (item.Weight ?? string.Empty).Trim().ToLowerInvariant();
            if (text.Length == 0 || CountUnicodeScalars(text) > 64 || !Weights.TryGetValue(weight, out int strength))
            {
                errorCode = "provider.expansion_controls_invalid";
                return false;
            }
            if (keywordIndexes.TryGetValue(text, out int existingIndex))
            {
                if (strength > Weights[keywords[existingIndex].Weight]) keywords[existingIndex].Weight = weight;
                continue;
            }
            if (keywords.Count >= 12)
            {
                errorCode = "provider.expansion_controls_invalid";
                return false;
            }
            keywordIndexes[text] = keywords.Count;
            keywords.Add(new ProviderTextExpansionKeyword { Text = text, Weight = weight });
        }

        List<string> avoidTopics = new List<string>();
        HashSet<string> seenAvoidTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? item in input.AvoidTopics ?? new List<string>())
        {
            string text = NormalizeLineEndings(item ?? string.Empty).Trim();
            if (text.Length == 0) continue;
            if (CountUnicodeScalars(text) > 64 || !seenAvoidTopics.Add(text) || avoidTopics.Count >= 12)
            {
                if (CountUnicodeScalars(text) > 64 || avoidTopics.Count >= 12)
                {
                    errorCode = "provider.expansion_controls_invalid";
                    return false;
                }
                continue;
            }
            avoidTopics.Add(text);
        }

        normalized = new ProviderTextExpansionControls
        {
            Direction = direction,
            FocusPreset = preset,
            FocusKeywords = keywords,
            AvoidTopics = avoidTopics
        };
        return true;
    }

    public static string NormalizeLineEndings(string value)
    {
        return (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
    private static int CountUnicodeScalars(string value)
    {
        int count = 0;
        foreach (System.Text.Rune _ in value.EnumerateRunes()) count++;
        return count;
    }
}

public sealed class ProviderTextExpansionResult
{
    public ProviderDraftStatus Status { get; init; }
    public string ExpandedText { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public DateTimeOffset? CooldownUntilUtc { get; init; }
    public ProviderUsage? Usage { get; init; }
}

public interface IProviderTextExpansionClient
{
    Task<ProviderTextExpansionResult> ExpandAsync(ProviderTextExpansionRequest request, CancellationToken cancellationToken = default);
}

public sealed class ProviderTextExpansionClient : IProviderTextExpansionClient
{
    private const int MaximumResponseBytes = 64 * 1024;
    private const int MaximumExpandedTextBytes = 24 * 1024;
    private const int MaximumEnvelopeBytes = 32 * 1024;
    private static readonly string ExpansionSystemPrompt =
        "Rewrite the user's character concept into concise, editable character prose for later Persona DSL conversion. "
        + "The first sentence means the exact source text from the beginning through the first Chinese full stop 。 inclusive. Start the answer with that substring copied byte-for-byte, including the character name, quotes, punctuation, and all wording; never paraphrase, shorten, or reorder that first sentence. "
        + "After that exact first sentence, preserve explicit facts and contradictions, but do not repeat every visual detail. "
        + "For long input, compress repeated appearance details and prioritize identity, personality, public behavior, private behavior, pressure reactions, relationships, and commitments. Keep the result under 1200 Chinese characters when the source is long. "
        + "Explain only behavioral implications directly supported by the supplied description. Never invent people, factions, locations, family ties, titles, occupations, past events, moral beliefs, political beliefs, promises, food preferences, or goals. "
        + "Do not turn missing information into facts or make the character kinder, healthier, more moral, more trusting, or more heroic. Omit unsupported categories. "
        + "Do not produce tags, numeric axes, stable IDs, approval metadata, JSON, Markdown headings, code fences, Persona DSL, analysis, or explanations. "
        + "Write only the concise character description in the same language as the user.";
    private readonly HttpClient _httpClient;
    private readonly IProviderEndpointResolver _endpointResolver;
    private readonly TimeSpan _requestTimeout;
    private readonly ProviderRequestCapture? _requestCapture;
    private readonly ProviderRequestGate _requestGate = new ProviderRequestGate();

    public ProviderTextExpansionClient(HttpMessageHandler transport, IProviderEndpointResolver? endpointResolver = null, TimeSpan? requestTimeout = null, ProviderRequestCapture? requestCapture = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _httpClient = new HttpClient(transport, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _endpointResolver = endpointResolver ?? new DnsProviderEndpointResolver();
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(ProviderRequestBudget.DefaultRequestTimeoutSeconds);
        _requestCapture = requestCapture;
        if (_requestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
    }

    public async Task<ProviderTextExpansionResult> ExpandAsync(ProviderTextExpansionRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Failure(ProviderDraftStatus.Cancelled, "provider.cancelled");
        if (request == null) return Failure(ProviderDraftStatus.InvalidRequest, "provider.expansion_request_invalid");
        if (!ProviderTextExpansionControlNormalizer.TryNormalize(request.Controls, out ProviderTextExpansionControls normalizedControls, out string controlsErrorCode))
        {
            return Failure(ProviderDraftStatus.InvalidRequest, controlsErrorCode);
        }
        string normalizedDescription = ProviderTextExpansionControlNormalizer.NormalizeLineEndings(request.Description);
        if (!ProviderEndpointPolicy.TryValidate(request.Endpoint, out Uri endpoint, out string endpointError)
            || string.IsNullOrWhiteSpace(request.Model)
            || string.IsNullOrWhiteSpace(normalizedDescription)
            || Encoding.UTF8.GetByteCount(normalizedDescription) > ProviderRequestBudget.ExpansionMaximumInputBytes
            || !ProviderProtocolKind.TryNormalize(request.ProviderProtocol, out string providerProtocol))
        {
            return Failure(ProviderDraftStatus.InvalidRequest, string.IsNullOrEmpty(endpointError) ? "provider.expansion_request_invalid" : endpointError);
        }

        string envelope = JsonSerializer.Serialize(new
        {
            protocol = "persona-expansion.v1",
            sourceDescription = normalizedDescription,
            direction = normalizedControls.Direction,
            focusPreset = normalizedControls.FocusPreset,
            focusKeywords = normalizedControls.FocusKeywords.Select(keyword => new { text = keyword.Text, weight = keyword.Weight }).ToArray(),
            avoidTopics = normalizedControls.AvoidTopics.ToArray()
        }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        if (Encoding.UTF8.GetByteCount(envelope) > MaximumEnvelopeBytes)
        {
            return Failure(ProviderDraftStatus.InvalidRequest, "provider.expansion_envelope_size_invalid");
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
            if (!await ProviderEndpointGuard.IsSafeResolvedEndpointAsync(_endpointResolver, endpoint, effectiveCancellationToken).ConfigureAwait(false))
            {
                return Failure(ProviderDraftStatus.TransportError, "provider.endpoint_resolution_rejected");
            }

            Uri nativeEndpoint = endpoint;
            bool useNativeOllama = ProviderProtocolKind.IsOllama(providerProtocol)
                && ProviderRequestBudget.TryGetOllamaNativeEndpoint(endpoint, out nativeEndpoint);
            Uri requestEndpoint = useNativeOllama ? nativeEndpoint : endpoint;
            int outputTokens = ProviderRequestBudget.SelectExpansionTokens(normalizedDescription);
            Dictionary<string, object> requestPayload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["model"] = request.Model.Trim(),
                ["messages"] = new[]
                {
                    new { role = "system", content = ExpansionSystemPrompt },
                    new { role = "user", content = envelope }
                }
            };
            if (useNativeOllama)
            {
                requestPayload["stream"] = false;
                requestPayload["think"] = "low";
                requestPayload["options"] = new { num_ctx = 8192, num_predict = outputTokens, temperature = 0.0, seed = 42 };
            }
            else
            {
                requestPayload["max_tokens"] = outputTokens;
                requestPayload["temperature"] = 0.0;
                if (ProviderProtocolKind.IsOllama(providerProtocol) && ProviderEndpointPolicy.IsLoopbackEndpoint(endpoint)) requestPayload["think"] = "low";
            }

            using HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, requestEndpoint) { Content = JsonContent.Create(requestPayload) };
            if (!string.IsNullOrWhiteSpace(request.ApiKey)) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
            byte[] requestBytes = await message.Content!.ReadAsByteArrayAsync(effectiveCancellationToken).ConfigureAwait(false);
            _requestCapture?.TryCaptureCurrent("expand", providerProtocol, ProviderEndpointPolicy.IsLoopbackEndpoint(endpoint) ? "loopback" : "cloud", request.Model.Trim(), normalizedDescription, requestBytes);

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
            if (!TryReadExpandedText(responseText, normalizedDescription, out string expandedText, out string parseError, out ProviderUsage? usage)) return Failure(ProviderDraftStatus.ResponseInvalid, parseError, usage: usage);
            return new ProviderTextExpansionResult { Status = ProviderDraftStatus.Success, ExpandedText = expandedText, Usage = usage };
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

    private static bool TryReadExpandedText(string responseText, string sourceDescription, out string expandedText, out string errorCode, out ProviderUsage? usage)
    {
        expandedText = string.Empty;
        ProviderChatResponseReadResult response = ProviderChatResponseReader.Read(responseText, MaximumResponseBytes);
        usage = response.Usage;
        if (!response.IsSuccess)
        {
            errorCode = response.Status switch
            {
                ProviderChatResponseReadStatus.ResponseSizeInvalid => "provider.expansion_response_size_invalid",
                ProviderChatResponseReadStatus.ResponseShapeInvalid => "provider.expansion_response_shape_invalid",
                ProviderChatResponseReadStatus.Truncated => "provider.expansion_truncated",
                ProviderChatResponseReadStatus.Empty => "provider.expansion_empty",
                ProviderChatResponseReadStatus.JsonInvalid => "provider.expansion_response_json_invalid",
                _ => "provider.expansion_response_invalid"
            };
            return false;
        }

        errorCode = "provider.expansion_response_invalid";
        string normalized = NormalizeExpandedText(response.Content);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            errorCode = "provider.expansion_empty";
            return false;
        }
        if (Encoding.UTF8.GetByteCount(normalized) > MaximumExpandedTextBytes)
        {
            errorCode = "provider.expansion_size_invalid";
            return false;
        }
        if (string.Equals(NormalizeForComparison(normalized), NormalizeForComparison(sourceDescription), StringComparison.Ordinal))
        {
            errorCode = "provider.expansion_unchanged";
            return false;
        }
        if (!ContainsRequiredSourceAnchors(sourceDescription, normalized))
        {
            errorCode = "provider.expansion_evidence_missing";
            return false;
        }
        if (LooksStructuredInsteadOfProse(normalized))
        {
            errorCode = "provider.expansion_text_invalid";
            return false;
        }

        expandedText = normalized;
        return true;
    }
    private static string NormalizeExpandedText(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char character in value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim())
        {
            if (character == '\n' || character == '\t' || !char.IsControl(character)) builder.Append(character);
        }

        List<string> normalizedLines = new List<string>();
        bool previousBlank = false;
        foreach (string line in builder.ToString().Split('\n'))
        {
            string trimmed = line.Trim();
            bool blank = trimmed.Length == 0;
            if (blank && previousBlank) continue;
            normalizedLines.Add(trimmed);
            previousBlank = blank;
        }
        return string.Join("\n", normalizedLines).Trim();
    }
    private static bool ContainsRequiredSourceAnchors(string source, string expanded)
    {
        foreach (string anchor in ExtractQuotedAnchors(source).Take(1))
        {
            if (!expanded.Contains(anchor, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static IEnumerable<string> ExtractQuotedAnchors(string source)
    {
        foreach ((char open, char close) in new[] { ('“', '”'), ('「', '」'), ('『', '』'), ('"', '"') })
        {
            int searchStart = 0;
            while (searchStart < source.Length)
            {
                int start = source.IndexOf(open, searchStart);
                if (start < 0) break;
                int end = source.IndexOf(close, start + 1);
                if (end < 0) break;
                string anchor = source[(start + 1)..end].Trim();
                if (anchor.Length >= 4) yield return anchor;
                searchStart = end + 1;
            }
        }
    }
    private static bool LooksStructuredInsteadOfProse(string value)
    {
        string trimmed = value.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal)
            || trimmed.StartsWith("[", StringComparison.Ordinal)
            || trimmed.StartsWith("#", StringComparison.Ordinal)
            || trimmed.StartsWith("<", StringComparison.Ordinal)
            || trimmed.StartsWith("Analysis:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("分析：", StringComparison.Ordinal)
            || trimmed.StartsWith("[PERSONA_", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("```", StringComparison.Ordinal);
    }

    private static string NormalizeForComparison(string value)
    {
        return string.Concat((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character)));
    }


    private static ProviderTextExpansionResult Failure(ProviderDraftStatus status, string errorCode, DateTimeOffset? cooldownUntilUtc = null, ProviderUsage? usage = null)
    {
        return new ProviderTextExpansionResult { Status = status, ErrorCode = errorCode, CooldownUntilUtc = cooldownUntilUtc, Usage = usage };
    }
}




