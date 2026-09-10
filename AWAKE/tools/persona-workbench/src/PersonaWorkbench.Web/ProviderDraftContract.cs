using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PersonaWorkbench.Core;

namespace PersonaWorkbench.Web;

public enum ProviderDraftStatus
{
    Success,
    InvalidRequest,
    Busy,
    CoolingDown,
    RateLimited,
    RedirectRejected,
    TransportError,
    ResponseInvalid,
    Cancelled
}

public sealed class ProviderDraftRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
}

public sealed class ProviderDraftResult
{
    public ProviderDraftStatus Status { get; init; }
    public PersonaDocument? Draft { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public DateTimeOffset? CooldownUntilUtc { get; init; }
}

public interface IProviderDraftClient
{
    Task<ProviderDraftResult> GenerateAsync(ProviderDraftRequest request, CancellationToken cancellationToken = default);
}

public interface IProviderEndpointResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(Uri endpoint, CancellationToken cancellationToken = default);
}

public sealed class DnsProviderEndpointResolver : IProviderEndpointResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (IPAddress.TryParse(endpoint.Host, out IPAddress? address)) return new[] { address };
        return await Dns.GetHostAddressesAsync(endpoint.DnsSafeHost, cancellationToken).ConfigureAwait(false);
    }
}

public static class ProviderEndpointPolicy
{
    public static bool TryValidate(string? value, out Uri endpoint, out string errorCode)
    {
        endpoint = null!;
        errorCode = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed)
            || !string.IsNullOrEmpty(parsed.UserInfo)
            || string.IsNullOrWhiteSpace(parsed.Host))
        {
            errorCode = "provider.endpoint_invalid";
            return false;
        }

        if (string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            endpoint = parsed;
            return true;
        }

        if (string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && IsLoopbackHost(parsed.Host))
        {
            endpoint = parsed;
            return true;
        }

        errorCode = "provider.endpoint_scheme_or_host_rejected";
        return false;
    }

    public static bool IsLoopbackEndpoint(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return IsLoopbackHost(endpoint.Host);
    }

    public static bool AreResolvedAddressesAllowed(Uri endpoint, IReadOnlyList<IPAddress>? addresses)
    {
        if (addresses == null || addresses.Count == 0) return false;
        bool loopbackEndpoint = IsLoopbackEndpoint(endpoint);
        return addresses.All(address => loopbackEndpoint ? IPAddress.IsLoopback(address) : IsPublicAddress(address));
    }

    private static bool IsLoopbackHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address));
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            byte[] bytes = address.GetAddressBytes();
            return !IPAddress.IsLoopback(address)
                && !IPAddress.IPv6None.Equals(address)
                && !IPAddress.IPv6Any.Equals(address)
                && !address.IsIPv6LinkLocal
                && !address.IsIPv6SiteLocal
                && !address.IsIPv6Multicast
                && (bytes[0] & 0xFE) != 0xFC;
        }

        byte[] ipv4 = address.GetAddressBytes();
        if (IPAddress.IsLoopback(address) || ipv4[0] == 0 || ipv4[0] >= 224) return false;
        if (ipv4[0] == 10 || ipv4[0] == 127 || ipv4[0] == 169 && ipv4[1] == 254) return false;
        if (ipv4[0] == 172 && ipv4[1] >= 16 && ipv4[1] <= 31) return false;
        return !(ipv4[0] == 192 && ipv4[1] == 168);
    }
}

public sealed class ProviderDraftClient : IProviderDraftClient
{
    private const int MaximumCandidateBytes = 64 * 1024;
    private const int DraftMaximumTokens = 4096;
    private static readonly string DraftSystemPrompt = BuildDraftSystemPrompt();
    private readonly HttpClient _httpClient;
    private readonly IProviderEndpointResolver _endpointResolver;
    private readonly TimeSpan _requestTimeout;
    private readonly ProviderRequestGate _requestGate = new ProviderRequestGate();

    public ProviderDraftClient(HttpMessageHandler transport, IProviderEndpointResolver? endpointResolver = null, TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _httpClient = new HttpClient(transport, disposeHandler: false);
        _endpointResolver = endpointResolver ?? new DnsProviderEndpointResolver();
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(45);
        if (_requestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
    }

    public async Task<ProviderDraftResult> GenerateAsync(ProviderDraftRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ProviderDraftResult { Status = ProviderDraftStatus.Cancelled, ErrorCode = "provider.cancelled" };
        }
        if (request == null)
        {
            return new ProviderDraftResult { Status = ProviderDraftStatus.InvalidRequest, ErrorCode = "provider.request_invalid" };
        }

        if (!ProviderEndpointPolicy.TryValidate(request.Endpoint, out Uri endpoint, out string endpointError)
            || string.IsNullOrWhiteSpace(request.Model) || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return new ProviderDraftResult { Status = ProviderDraftStatus.InvalidRequest, ErrorCode = string.IsNullOrEmpty(endpointError) ? "provider.request_invalid" : endpointError };
        }

        if (!_requestGate.TryEnter(out ProviderRequestGateStatus gateStatus, out DateTimeOffset? cooldownUntilUtc))
        {
            return gateStatus == ProviderRequestGateStatus.Busy
                ? new ProviderDraftResult { Status = ProviderDraftStatus.Busy, ErrorCode = "provider.request_in_flight" }
                : new ProviderDraftResult { Status = ProviderDraftStatus.CoolingDown, ErrorCode = "provider.cooldown_active", CooldownUntilUtc = cooldownUntilUtc };
        }

        try
        {
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_requestTimeout);
            CancellationToken effectiveCancellationToken = deadline.Token;
            if (!await ProviderEndpointGuard.IsSafeResolvedEndpointAsync(_endpointResolver, endpoint, effectiveCancellationToken).ConfigureAwait(false))
            {
                return new ProviderDraftResult { Status = ProviderDraftStatus.TransportError, ErrorCode = "provider.endpoint_resolution_rejected" };
            }

            Dictionary<string, object> requestPayload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["model"] = request.Model.Trim(),
                ["messages"] = new[]
                {
                    new { role = "system", content = DraftSystemPrompt },
                    new { role = "user", content = request.Prompt }
                },
                ["response_format"] = new { type = "json_object" },
                ["max_tokens"] = DraftMaximumTokens
            };
            if (IsOfficialDeepSeekEndpoint(endpoint))
            {
                requestPayload["thinking"] = new { type = "disabled" };
            }
            using HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(requestPayload)
            };
            if (!string.IsNullOrWhiteSpace(request.ApiKey))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
            }
            using HttpResponseMessage response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
            {
                return new ProviderDraftResult { Status = ProviderDraftStatus.RedirectRejected, ErrorCode = "provider.redirect_rejected" };
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                DateTimeOffset cooldownUntil = ProviderRequestGate.CalculateCooldown(response.Headers.RetryAfter);
                _requestGate.SetCooldown(cooldownUntil);
                return new ProviderDraftResult { Status = ProviderDraftStatus.RateLimited, ErrorCode = "provider.rate_limited", CooldownUntilUtc = cooldownUntil };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ProviderDraftResult { Status = ProviderDraftStatus.TransportError, ErrorCode = "provider.http_" + (int)response.StatusCode };
            }

            if (!await ProviderEndpointGuard.IsSafeResolvedEndpointAsync(_endpointResolver, endpoint, effectiveCancellationToken).ConfigureAwait(false))
            {
                return new ProviderDraftResult { Status = ProviderDraftStatus.TransportError, ErrorCode = "provider.endpoint_resolution_rejected" };
            }

            string responseText = await response.Content.ReadAsStringAsync(effectiveCancellationToken).ConfigureAwait(false);
            if (!TryParseDraft(responseText, request.Prompt ?? string.Empty, out PersonaDocument? draft, out string parseError))
            {
                return new ProviderDraftResult { Status = ProviderDraftStatus.ResponseInvalid, ErrorCode = parseError };
            }

            return new ProviderDraftResult { Status = ProviderDraftStatus.Success, Draft = draft };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ProviderDraftResult { Status = ProviderDraftStatus.Cancelled, ErrorCode = "provider.cancelled" };
        }
        catch (OperationCanceledException)
        {
            return new ProviderDraftResult { Status = ProviderDraftStatus.TransportError, ErrorCode = "provider.timeout" };
        }
        catch (HttpRequestException)
        {
            return new ProviderDraftResult { Status = ProviderDraftStatus.TransportError, ErrorCode = "provider.transport_error" };
        }
        finally
        {
            _requestGate.Exit();
        }
    }

    private static bool IsOfficialDeepSeekEndpoint(Uri endpoint)
    {
        return string.Equals(endpoint.Host, "api.deepseek.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseDraft(string responseText, string sourcePrompt, out PersonaDocument? draft, out string errorCode)
    {
        draft = null;
        errorCode = "provider.response_invalid";
        if (string.IsNullOrWhiteSpace(responseText) || Encoding.UTF8.GetByteCount(responseText) > MaximumCandidateBytes)
        {
            errorCode = "provider.response_size_invalid";
            return false;
        }

        try
        {
            using JsonDocument outer = JsonDocument.Parse(responseText, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 16 });
            if (!outer.RootElement.TryGetProperty("choices", out JsonElement choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() != 1)
            {
                errorCode = "provider.response_choices_invalid";
                return false;
            }

            JsonElement choice = choices[0];
            if (!choice.TryGetProperty("message", out JsonElement message) || !message.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.String)
            {
                errorCode = "provider.response_content_missing";
                return false;
            }

            return TryParseCandidate(content.GetString() ?? string.Empty, sourcePrompt, out draft, out errorCode);
        }
        catch (JsonException)
        {
            errorCode = "provider.response_json_invalid";
            return false;
        }
    }

    private static string BuildDraftSystemPrompt()
    {
        string allowedTags = string.Join(", ", PersonaTagRegistry.CreateDefault().Ids);
        return "Return exactly one JSON object for an editable Persona candidate. "
            + "Allowed top-level fields are id, displayName, core, identityFacts, summary, publicDescription, privateDescription, contradictionDescription, foodPreference, selfClaimRules, realSelfBehaviors, selfClaimExamples, tags, traitProfile, expressionProfile, behaviorProfile, reactionProfile, commitmentProfile, evidence. "
            + "core must always be one non-empty JSON string. displayName and identityFacts must be one JSON string or null. Only selfClaimRules, realSelfBehaviors, and selfClaimExamples may be arrays. Never return arrays or objects for core, displayName, or identityFacts. Every non-empty author field also needs evidence. Do not return sourceDescription, templateVersion, status, or sourcePackId because those are controlled locally. Profile axis values must be -2, -1, 0, 1, 2, or null. Use null whenever the source description does not provide enough evidence; never fill fields merely for completeness. "
            + "Every non-null axis, non-empty profile text, identityFacts value, and selected tag must have one evidence entry whose key is its JSON path, for example traitProfile.caution or tags.trait.cautious. "
            + "Each evidence value must be a short exact quotation from the user description. Synthesize and refine core instead of copying the entire description verbatim. "
            + "Use only registered tag IDs. Allowed tags: " + allowedTags + ". "
            + "Do not use Markdown fences, commentary, additional keys, translated axis labels, or Persona DSL sections. "
            + "Profile fields: traitProfile={caution,ambition,pride,pragmatism,inGroupLoyalty,tradition}; "
            + "expressionProfile={restraint,directness,formality,playfulness,warmth}; "
            + "behaviorProfile={conditionality,deliberation,trustTesting,leverage,inGroupPriority,leadership}; "
            + "reactionProfile={confrontation,expression,timing,resentment,supportSeeking,sensitiveConditions,conditionalResponses}; reactionProfile and commitmentProfile narrative text fields may be one string, an array of up to eight strings, or null. "
            + "commitmentProfile={promiseCaution,promisePersistence,valueTradeability,priorityOrder,protectedValues,applicableScope,exceptionCost,breachResponse}; author arrays are selfClaimRules, realSelfBehaviors, selfClaimExamples.";
    }

    private static bool TryParseCandidate(string candidateText, string sourcePrompt, out PersonaDocument? draft, out string errorCode)
    {
        draft = null;
        errorCode = "provider.candidate_invalid";
        candidateText = NormalizeCandidateJson(candidateText);
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            errorCode = "provider.candidate_empty";
            return false;
        }
        if (Encoding.UTF8.GetByteCount(candidateText) > MaximumCandidateBytes)
        {
            errorCode = "provider.candidate_size_invalid";
            return false;
        }

        try
        {
            using JsonDocument candidate = JsonDocument.Parse(candidateText, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 16 });
            if (candidate.RootElement.ValueKind != JsonValueKind.Object)
            {
                errorCode = "provider.candidate_root_invalid";
                return false;
            }

            PersonaDocument parsed = new PersonaDocument
            {
                Id = "free.generated.persona"
            };
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> requiredEvidence = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> evidence = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty property in candidate.RootElement.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    errorCode = "provider.candidate_unknown_or_duplicate_field";
                    return false;
                }

                switch (property.Name)
                {
                    case "id":
                        if (property.Value.ValueKind == JsonValueKind.Null) break;
                        if (!TryReadOptionalText(property.Value, 256, out string id)) return Fail("provider.candidate_id_invalid", out draft, out errorCode);
                        if (!string.IsNullOrWhiteSpace(id)) parsed.Id = id;
                        break;
                    case "displayName":
                        if (!TryReadOptionalText(property.Value, 256, out string displayName)) return Fail("provider.candidate_display_name_invalid", out draft, out errorCode);
                        parsed.DisplayName = displayName;
                        break;
                    case "core":
                        if (!TryReadRequiredText(property.Value, 8192, out string core)) return Fail("provider.candidate_core_invalid", out draft, out errorCode);
                        parsed.Core = core;
                        break;
                    case "identityFacts":
                        if (!TryReadOptionalText(property.Value, 8192, out string identityFacts)) return Fail("provider.candidate_identity_invalid", out draft, out errorCode);
                        parsed.IdentityFacts = identityFacts;
                        if (!string.IsNullOrWhiteSpace(identityFacts)) requiredEvidence.Add("identityFacts");
                        break;
                    case "summary":
                        if (!TryReadEvidenceBackedText(property.Value, "summary", value => parsed.Summary = value, requiredEvidence, out errorCode)) return false;
                        break;
                    case "publicDescription":
                        if (!TryReadEvidenceBackedText(property.Value, "publicDescription", value => parsed.PublicDescription = value, requiredEvidence, out errorCode)) return false;
                        break;
                    case "privateDescription":
                        if (!TryReadEvidenceBackedText(property.Value, "privateDescription", value => parsed.PrivateDescription = value, requiredEvidence, out errorCode)) return false;
                        break;
                    case "contradictionDescription":
                        if (!TryReadEvidenceBackedText(property.Value, "contradictionDescription", value => parsed.ContradictionDescription = value, requiredEvidence, out errorCode)) return false;
                        break;
                    case "foodPreference":
                        if (!TryReadEvidenceBackedText(property.Value, "foodPreference", value => parsed.FoodPreference = value, requiredEvidence, out errorCode)) return false;
                        break;
                    case "selfClaimRules":
                        if (!TryReadEvidenceBackedList(property.Value, "selfClaimRules", parsed.SelfClaimRules, requiredEvidence, out errorCode)) return false;
                        break;
                    case "realSelfBehaviors":
                        if (!TryReadEvidenceBackedList(property.Value, "realSelfBehaviors", parsed.RealSelfBehaviors, requiredEvidence, out errorCode)) return false;
                        break;
                    case "selfClaimExamples":
                        if (!TryReadEvidenceBackedList(property.Value, "selfClaimExamples", parsed.SelfClaimExamples, requiredEvidence, out errorCode)) return false;
                        break;
                    case "tags":
                        if (!TryReadTags(property.Value, parsed.Tags, requiredEvidence, out errorCode)) return false;
                        break;
                    case "traitProfile":
                        if (!TryReadProfile(property.Value, "traitProfile", new Dictionary<string, Action<int?>>(StringComparer.Ordinal)
                        {
                            ["caution"] = value => parsed.TraitProfile.Caution = value,
                            ["ambition"] = value => parsed.TraitProfile.Ambition = value,
                            ["pride"] = value => parsed.TraitProfile.Pride = value,
                            ["pragmatism"] = value => parsed.TraitProfile.Pragmatism = value,
                            ["inGroupLoyalty"] = value => parsed.TraitProfile.InGroupLoyalty = value,
                            ["tradition"] = value => parsed.TraitProfile.Tradition = value
                        }, null, requiredEvidence, out errorCode)) return false;
                        break;
                    case "expressionProfile":
                        if (!TryReadProfile(property.Value, "expressionProfile", new Dictionary<string, Action<int?>>(StringComparer.Ordinal)
                        {
                            ["restraint"] = value => parsed.ExpressionProfile.Restraint = value,
                            ["directness"] = value => parsed.ExpressionProfile.Directness = value,
                            ["formality"] = value => parsed.ExpressionProfile.Formality = value,
                            ["playfulness"] = value => parsed.ExpressionProfile.Playfulness = value,
                            ["warmth"] = value => parsed.ExpressionProfile.Warmth = value
                        }, null, requiredEvidence, out errorCode)) return false;
                        break;
                    case "behaviorProfile":
                        if (!TryReadProfile(property.Value, "behaviorProfile", new Dictionary<string, Action<int?>>(StringComparer.Ordinal)
                        {
                            ["conditionality"] = value => parsed.BehaviorProfile.Conditionality = value,
                            ["deliberation"] = value => parsed.BehaviorProfile.Deliberation = value,
                            ["trustTesting"] = value => parsed.BehaviorProfile.TrustTesting = value,
                            ["leverage"] = value => parsed.BehaviorProfile.Leverage = value,
                            ["inGroupPriority"] = value => parsed.BehaviorProfile.InGroupPriority = value,
                            ["leadership"] = value => parsed.BehaviorProfile.Leadership = value
                        }, null, requiredEvidence, out errorCode)) return false;
                        break;
                    case "reactionProfile":
                        if (!TryReadProfile(property.Value, "reactionProfile", new Dictionary<string, Action<int?>>(StringComparer.Ordinal)
                        {
                            ["confrontation"] = value => parsed.ReactionProfile.Confrontation = value,
                            ["expression"] = value => parsed.ReactionProfile.Expression = value,
                            ["timing"] = value => parsed.ReactionProfile.Timing = value,
                            ["resentment"] = value => parsed.ReactionProfile.Resentment = value,
                            ["supportSeeking"] = value => parsed.ReactionProfile.SupportSeeking = value
                        }, new Dictionary<string, Action<string>>(StringComparer.Ordinal)
                        {
                            ["sensitiveConditions"] = value => parsed.ReactionProfile.SensitiveConditions = value,
                            ["conditionalResponses"] = value => parsed.ReactionProfile.ConditionalResponses = value
                        }, requiredEvidence, out errorCode)) return false;
                        break;
                    case "commitmentProfile":
                        if (!TryReadProfile(property.Value, "commitmentProfile", new Dictionary<string, Action<int?>>(StringComparer.Ordinal)
                        {
                            ["promiseCaution"] = value => parsed.CommitmentProfile.PromiseCaution = value,
                            ["promisePersistence"] = value => parsed.CommitmentProfile.PromisePersistence = value,
                            ["valueTradeability"] = value => parsed.CommitmentProfile.ValueTradeability = value
                        }, new Dictionary<string, Action<string>>(StringComparer.Ordinal)
                        {
                            ["priorityOrder"] = value => parsed.CommitmentProfile.PriorityOrder = value,
                            ["protectedValues"] = value => parsed.CommitmentProfile.ProtectedValues = value,
                            ["applicableScope"] = value => parsed.CommitmentProfile.ApplicableScope = value,
                            ["exceptionCost"] = value => parsed.CommitmentProfile.ExceptionCost = value,
                            ["breachResponse"] = value => parsed.CommitmentProfile.BreachResponse = value
                        }, requiredEvidence, out errorCode)) return false;
                        break;
                    case "evidence":
                        if (!TryReadEvidence(property.Value, evidence, out errorCode)) return false;
                        break;
                    default:
                        errorCode = "provider.candidate_unknown_or_duplicate_field";
                        return false;
                }
            }

            parsed.SourceDescription = sourcePrompt?.Trim() ?? string.Empty;
            parsed.TemplateVersion = "persona-load.v2";
            parsed.Status = "draft";
            parsed.SourcePackId = string.Empty;
            string normalizedSourcePrompt = sourcePrompt ?? string.Empty;
            if (IsExactCopy(parsed.Core, normalizedSourcePrompt) || IsExactCopy(parsed.IdentityFacts, normalizedSourcePrompt))
            {
                errorCode = "provider.candidate_verbatim_copy_rejected";
                return false;
            }
            PruneUnprovenFields(parsed, requiredEvidence, evidence);
            PruneInvalidEvidenceFields(parsed, requiredEvidence, evidence, normalizedSourcePrompt);
            if (!ValidateEvidence(requiredEvidence, evidence, normalizedSourcePrompt, out errorCode)) return false;
            if (!PersonaValidator.Validate(parsed, PersonaTagRegistry.CreateDefault()).IsValid)
            {
                errorCode = "provider.candidate_validation_failed";
                return false;
            }

            draft = parsed;
            return true;
        }
        catch (JsonException)
        {
            errorCode = "provider.candidate_json_invalid";
            return false;
        }
    }

    private static string NormalizeCandidateJson(string candidateText)
    {
        string normalized = (candidateText ?? string.Empty).Trim();
        if (!normalized.StartsWith("```", StringComparison.Ordinal) || !normalized.EndsWith("```", StringComparison.Ordinal)) return normalized;

        int firstLineEnd = normalized.IndexOf('\n');
        if (firstLineEnd < 0) return normalized;
        normalized = normalized[(firstLineEnd + 1)..].Trim();
        if (normalized.EndsWith("```", StringComparison.Ordinal)) normalized = normalized[..^3].Trim();
        return normalized;
    }
    private static bool TryReadEvidenceBackedText(
        JsonElement element,
        string evidenceKey,
        Action<string> setter,
        HashSet<string> requiredEvidence,
        out string errorCode)
    {
        errorCode = string.Empty;
        if (!TryReadOptionalText(element, 4096, out string value))
        {
            errorCode = "provider.candidate_author_text_invalid";
            return false;
        }
        setter(value);
        if (!string.IsNullOrWhiteSpace(value)) requiredEvidence.Add(evidenceKey);
        return true;
    }

    private static bool TryReadEvidenceBackedList(
        JsonElement element,
        string evidencePrefix,
        List<string> target,
        HashSet<string> requiredEvidence,
        out string errorCode)
    {
        errorCode = string.Empty;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > 16)
        {
            errorCode = "provider.candidate_author_list_invalid";
            return false;
        }
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (!TryReadRequiredText(item, 2048, out string value) || !seen.Add(value))
            {
                errorCode = "provider.candidate_author_list_invalid";
                return false;
            }
            target.Add(value);
            requiredEvidence.Add(evidencePrefix + "." + index.ToString());
            index++;
        }
        return true;
    }

    private static bool TryReadTags(JsonElement element, List<string> tags, HashSet<string> requiredEvidence, out string errorCode)
    {
        errorCode = string.Empty;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > 32)
        {
            errorCode = "provider.candidate_tags_invalid";
            return false;
        }
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement tag in element.EnumerateArray())
        {
            if (!TryReadRequiredText(tag, 128, out string tagId) || !seen.Add(tagId))
            {
                errorCode = "provider.candidate_tag_invalid";
                return false;
            }
            tags.Add(tagId);
            requiredEvidence.Add("tags." + tagId);
        }
        return true;
    }

    private static bool TryReadProfile(
        JsonElement element,
        string prefix,
        Dictionary<string, Action<int?>> axisSetters,
        Dictionary<string, Action<string>>? textSetters,
        HashSet<string> requiredEvidence,
        out string errorCode)
    {
        errorCode = string.Empty;
        if (element.ValueKind != JsonValueKind.Object)
        {
            errorCode = "provider.candidate_profile_invalid";
            return false;
        }
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                errorCode = "provider.candidate_profile_invalid";
                return false;
            }
            if (axisSetters.TryGetValue(property.Name, out Action<int?>? setAxis))
            {
                int? value = null;
                if (property.Value.ValueKind != JsonValueKind.Null)
                {
                    if (!TryReadAxisValue(property.Value, out int parsed))
                    {
                        continue;
                    }
                    value = parsed;
                    requiredEvidence.Add(prefix + "." + property.Name);
                }
                setAxis(value);
                continue;
            }
            if (textSetters != null && textSetters.TryGetValue(property.Name, out Action<string>? setText))
            {
                if (!TryReadOptionalTextOrList(property.Value, 2048, out string value))
                {
                    errorCode = "provider.candidate_profile_text_invalid";
                    return false;
                }
                setText(value);
                if (!string.IsNullOrWhiteSpace(value)) requiredEvidence.Add(prefix + "." + property.Name);
                continue;
            }
            errorCode = "provider.candidate_profile_unknown_field";
            return false;
        }
        return true;
    }

    private static bool TryReadAxisValue(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out int integerValue) && integerValue is >= -2 and <= 2)
            {
                value = integerValue;
                return true;
            }
            if (element.TryGetDouble(out double numericValue)
                && double.IsFinite(numericValue)
                && Math.Abs(numericValue - Math.Round(numericValue)) < 0.000001
                && numericValue is >= -2 and <= 2)
            {
                value = (int)Math.Round(numericValue);
                return true;
            }
            return false;
        }
        if (element.ValueKind != JsonValueKind.String) return false;
        string text = element.GetString()?.Trim() ?? string.Empty;
        if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsedInteger)
            && parsedInteger is >= -2 and <= 2)
        {
            value = parsedInteger;
            return true;
        }
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedNumber)
            && double.IsFinite(parsedNumber)
            && Math.Abs(parsedNumber - Math.Round(parsedNumber)) < 0.000001
            && parsedNumber is >= -2 and <= 2)
        {
            value = (int)Math.Round(parsedNumber);
            return true;
        }
        return false;
    }
    private static bool TryReadEvidence(JsonElement element, Dictionary<string, string> evidence, out string errorCode)
    {
        errorCode = string.Empty;
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Count() > 64)
        {
            errorCode = "provider.candidate_evidence_invalid";
            return false;
        }
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!TryReadPlainText(property.Name, 160, out string key)
                || !TryReadRequiredText(property.Value, 256, out string quote)
                || !evidence.TryAdd(key, quote))
            {
                errorCode = "provider.candidate_evidence_invalid";
                return false;
            }
        }
        return true;
    }

    private static void PruneUnprovenFields(PersonaDocument parsed, HashSet<string> required, Dictionary<string, string> evidence)
    {
        HashSet<string> keysToRemove = required.Where(key => !evidence.ContainsKey(key)).ToHashSet(StringComparer.Ordinal);
        PruneEvidenceKeys(parsed, required, evidence, keysToRemove);
    }

    private static void PruneInvalidEvidenceFields(
        PersonaDocument parsed,
        HashSet<string> required,
        Dictionary<string, string> evidence,
        string sourcePrompt)
    {
        HashSet<string> keysToRemove = required
            .Where(key => evidence.TryGetValue(key, out string? quote) && !ContainsEvidence(sourcePrompt, quote))
            .ToHashSet(StringComparer.Ordinal);
        PruneEvidenceKeys(parsed, required, evidence, keysToRemove);
    }

    private static void PruneEvidenceKeys(
        PersonaDocument parsed,
        HashSet<string> required,
        Dictionary<string, string> evidence,
        HashSet<string> keysToRemove)
    {
        foreach (string key in keysToRemove) ClearEvidenceField(parsed, key);
        required.ExceptWith(keysToRemove);
        foreach (string key in evidence.Keys.ToArray())
        {
            if (!required.Contains(key)) evidence.Remove(key);
        }
    }

    private static void ClearEvidenceField(PersonaDocument parsed, string key)
    {
        switch (key)
        {
            case "identityFacts": parsed.IdentityFacts = string.Empty; break;
            case "summary": parsed.Summary = string.Empty; break;
            case "publicDescription": parsed.PublicDescription = string.Empty; break;
            case "privateDescription": parsed.PrivateDescription = string.Empty; break;
            case "contradictionDescription": parsed.ContradictionDescription = string.Empty; break;
            case "foodPreference": parsed.FoodPreference = string.Empty; break;
            case "selfClaimRules": parsed.SelfClaimRules.Clear(); break;
            case "realSelfBehaviors": parsed.RealSelfBehaviors.Clear(); break;
            case "selfClaimExamples": parsed.SelfClaimExamples.Clear(); break;
            case "traitProfile.caution": parsed.TraitProfile.Caution = null; break;
            case "traitProfile.ambition": parsed.TraitProfile.Ambition = null; break;
            case "traitProfile.pride": parsed.TraitProfile.Pride = null; break;
            case "traitProfile.pragmatism": parsed.TraitProfile.Pragmatism = null; break;
            case "traitProfile.inGroupLoyalty": parsed.TraitProfile.InGroupLoyalty = null; break;
            case "traitProfile.tradition": parsed.TraitProfile.Tradition = null; break;
            case "expressionProfile.restraint": parsed.ExpressionProfile.Restraint = null; break;
            case "expressionProfile.directness": parsed.ExpressionProfile.Directness = null; break;
            case "expressionProfile.formality": parsed.ExpressionProfile.Formality = null; break;
            case "expressionProfile.playfulness": parsed.ExpressionProfile.Playfulness = null; break;
            case "expressionProfile.warmth": parsed.ExpressionProfile.Warmth = null; break;
            case "behaviorProfile.conditionality": parsed.BehaviorProfile.Conditionality = null; break;
            case "behaviorProfile.deliberation": parsed.BehaviorProfile.Deliberation = null; break;
            case "behaviorProfile.trustTesting": parsed.BehaviorProfile.TrustTesting = null; break;
            case "behaviorProfile.leverage": parsed.BehaviorProfile.Leverage = null; break;
            case "behaviorProfile.inGroupPriority": parsed.BehaviorProfile.InGroupPriority = null; break;
            case "behaviorProfile.leadership": parsed.BehaviorProfile.Leadership = null; break;
            case "reactionProfile.confrontation": parsed.ReactionProfile.Confrontation = null; break;
            case "reactionProfile.expression": parsed.ReactionProfile.Expression = null; break;
            case "reactionProfile.timing": parsed.ReactionProfile.Timing = null; break;
            case "reactionProfile.resentment": parsed.ReactionProfile.Resentment = null; break;
            case "reactionProfile.supportSeeking": parsed.ReactionProfile.SupportSeeking = null; break;
            case "reactionProfile.sensitiveConditions": parsed.ReactionProfile.SensitiveConditions = string.Empty; break;
            case "reactionProfile.conditionalResponses": parsed.ReactionProfile.ConditionalResponses = string.Empty; break;
            case "commitmentProfile.promiseCaution": parsed.CommitmentProfile.PromiseCaution = null; break;
            case "commitmentProfile.promisePersistence": parsed.CommitmentProfile.PromisePersistence = null; break;
            case "commitmentProfile.valueTradeability": parsed.CommitmentProfile.ValueTradeability = null; break;
            case "commitmentProfile.priorityOrder": parsed.CommitmentProfile.PriorityOrder = string.Empty; break;
            case "commitmentProfile.protectedValues": parsed.CommitmentProfile.ProtectedValues = string.Empty; break;
            case "commitmentProfile.applicableScope": parsed.CommitmentProfile.ApplicableScope = string.Empty; break;
            case "commitmentProfile.exceptionCost": parsed.CommitmentProfile.ExceptionCost = string.Empty; break;
            case "commitmentProfile.breachResponse": parsed.CommitmentProfile.BreachResponse = string.Empty; break;
            default:
                if (key.StartsWith("tags.", StringComparison.Ordinal)) parsed.Tags.Remove(key[5..]);
                break;
        }
    }
    private static bool ValidateEvidence(HashSet<string> required, Dictionary<string, string> evidence, string sourcePrompt, out string errorCode)
    {
        errorCode = string.Empty;
        foreach (string key in required)
        {
            if (!evidence.TryGetValue(key, out string? quote))
            {
                errorCode = "provider.candidate_evidence_missing";
                return false;
            }
            if (!ContainsEvidence(sourcePrompt, quote))
            {
                errorCode = "provider.candidate_evidence_not_in_source";
                return false;
            }
        }
        if (evidence.Keys.Any(key => !required.Contains(key)))
        {
            errorCode = "provider.candidate_evidence_orphaned";
            return false;
        }
        return true;
    }

    private static bool ContainsEvidence(string sourcePrompt, string quote)
    {
        string source = NormalizeComparison(sourcePrompt);
        string evidence = NormalizeComparison(quote);
        return evidence.Length > 0 && source.IndexOf(evidence, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsExactCopy(string value, string sourcePrompt)
    {
        string normalizedValue = NormalizeComparison(value);
        return normalizedValue.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(normalizedValue, NormalizeComparison(sourcePrompt));
    }

    private static string NormalizeComparison(string value)
    {
        return string.Concat((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character))).Trim();
    }

    private static bool TryReadRequiredText(JsonElement element, int maximumChars, out string result)
    {
        result = string.Empty;
        return element.ValueKind == JsonValueKind.String
            && TryReadPlainText(element.GetString(), maximumChars, out result)
            && !string.IsNullOrWhiteSpace(result);
    }

    private static bool TryReadOptionalText(JsonElement element, int maximumChars, out string result)
    {
        result = string.Empty;
        if (element.ValueKind == JsonValueKind.Null) return true;
        return element.ValueKind == JsonValueKind.String && TryReadPlainText(element.GetString(), maximumChars, out result);
    }

    private static bool TryReadOptionalTextOrList(JsonElement element, int maximumChars, out string result)
    {
        if (TryReadOptionalText(element, maximumChars, out result)) return true;
        result = string.Empty;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > 8) return false;

        List<string> values = new List<string>();
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (!TryReadRequiredText(item, 512, out string value)) return false;
            values.Add(value);
        }
        result = string.Join("；", values);
        return result.Length <= maximumChars;
    }
    private static bool Fail(string code, out PersonaDocument? draft, out string errorCode)
    {
        draft = null;
        errorCode = code;
        return false;
    }
    private static bool TryReadPlainText(string? value, int maximumChars, out string result)
    {
        result = NormalizePlainText(value);
        return result.Length <= maximumChars && result.All(character => !char.IsControl(character));
    }

    private static string NormalizePlainText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        StringBuilder builder = new StringBuilder(value.Length);
        bool previousWhitespace = false;
        foreach (char character in value)
        {
            if (character == '\r' || character == '\n' || character == '\t' || char.IsWhiteSpace(character))
            {
                if (!previousWhitespace && builder.Length > 0) builder.Append(' ');
                previousWhitespace = true;
                continue;
            }
            builder.Append(character);
            previousWhitespace = false;
        }
        return builder.ToString().Trim();
    }
}
