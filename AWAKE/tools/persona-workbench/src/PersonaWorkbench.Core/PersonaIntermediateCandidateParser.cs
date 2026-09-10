using System.Text;
using System.Text.Json;

namespace PersonaWorkbench.Core;

public sealed class PersonaIntermediateCandidateParseResult
{
    public PersonaDocument? Document { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public bool IsValid => Document != null && string.IsNullOrEmpty(ErrorCode);
}

public static class PersonaIntermediateCandidateParser
{
    private const int MaximumCandidateBytes = 24 * 1024;
    private const int MaximumTextBytes = 8 * 1024;
    private static readonly HashSet<string> RootFields = new(StringComparer.Ordinal)
    {
        "summary", "identityFacts", "publicDescription", "privateDescription", "contradictionDescription",
        "reaction", "commitment", "axes", "flags"
    };
    private static readonly HashSet<string> ReactionFields = new(StringComparer.Ordinal)
    {
        "sensitiveConditions", "conditionalResponses"
    };
    private static readonly HashSet<string> CommitmentFields = new(StringComparer.Ordinal)
    {
        "priorityOrder", "protectedValues", "applicableScope", "exceptionCost", "breachResponse"
    };
    private static readonly HashSet<string> AxisEntryFields = new(StringComparer.Ordinal)
    {
        "index", "value", "source"
    };
    private static readonly HashSet<string> FlagEntryFields = new(StringComparer.Ordinal)
    {
        "index", "source"
    };
    private static readonly AxisEvidenceRule[] AxisEvidenceRules =
    {
        new("冒进|鲁莽|莽撞|大胆|无畏|冒险|bold|reckless|risk-taking", "谨慎|小心|慎重|稳妥|戒备|cautious|careful"),
        new("安于现状|知足|淡泊|无意争权|content|unambitious", "野心|进取|上进|权力欲|追逐地位|ambitious|aspiring"),
        new("谦抑|谦逊|谦虚|自谦|humble|modest", "自尊|骄傲|高傲|傲慢|傲娇|要强|proud|pride"),
        new("理想|原则|信念|正义|公义|idealistic|principled", "现实|务实|实用|利益优先|权宜|pragmatic|practical"),
        new("独立|自持|靠自己|不依附|independent|self-reliant", "护卫自己人|保护自己人|护短|忠于家族|守护同伴|guardian|loyal to"),
        new("新变|创新|变革|新事物|打破传统|novelty|innovative", "传统|习俗|礼法|祖制|守旧|traditional|custom"),
        new("随性|流露|脱口而出|不加掩饰|spontaneous|unfiltered", "措辞克制|克制表达|斟酌措辞|谨言慎行|measured|restrained"),
        new("含蓄|迂回|委婉|暗示|indirect|subtle", "直白|直接|明确|坦率|direct|explicit"),
        new("随意|口语|不拘礼节|粗俗|casual|informal", "礼貌|正式|敬语|礼仪|formal|polite"),
        new("严肃|正经|不苟言笑|serious|solemn", "戏谑|调侃|逗弄|玩笑|teasing|playful"),
        new("疏离|冷淡|冷漠|高冷|不近人情|aloof|distant", "温和|亲近|热情|体贴|亲切|warm|affectionate"),
        new("先行付出|先付出|无条件帮助|慷慨相助|giving-first|gives first", "先谈条件|讲条件|交换条件|要求回报|bargaining|conditions first"),
        new("先行动|立刻行动|冲动行事|当机立断|act-first|acts first", "先观察|观察后行动|谋定后动|评估局势|observe-first|observes first"),
        new("容易信任|轻信|天真|涉世未深|trusting|naive", "试探忠诚|试探他人|考验忠诚|多疑|loyalty-testing|distrustful"),
        new("坦露筹码|亮出底牌|毫不隐瞒|坦诚相告|reveals cards|open hand", "保留后手|留一手|隐藏筹码|掌握把柄|keeps leverage|holds back"),
        new("一视同仁|公平对待|公事公办|impartial|fair to all", "优先自己人|偏袒自己人|照顾家族|先护同伴|in-group first|favors allies"),
        new("跟随配合|协作|服从指挥|配合他人|cooperative|follows", "主动主导|主导局面|发号施令|掌控局面|commanding|takes charge"),
        new("回避冲突|逃避|退让|息事宁人|avoidant|avoids conflict", "迎击|反击|还击|正面对抗|不退缩|confrontational|fights back"),
        new("压抑情绪|隐忍|不露声色|藏起情绪|suppressed|conceals emotion", "情绪外露|喜怒形于色|激动|直露情绪|externalized|expressive"),
        new("延迟反应|事后回应|冷静后再说|稍后处理|delayed|later response", "即时反应|立即回应|当场回应|马上反应|immediate|at once"),
        new("缓和|原谅|和解|不记仇|reconciling|forgiving", "记恨|怨恨|报复|复仇|resentful|vengeful"),
        new("独自控制|独自处理|不求人|靠自己|self-reliant|handles alone", "寻求依靠|寻求帮助|依赖他人|求助|support-seeking|seeks support"),
        new("轻易承诺|随口答应|轻诺|promises easily|easy promises", "谨慎承诺|不轻易承诺|不轻易许诺|少作承诺|cautious promises|promises rarely"),
        new("灵活履行|允许变通|可以调整承诺|视情况改变|flexible promises|adapts commitments", "坚持履行|信守承诺|一诺千金|绝不反悔|persistent promises|keeps promises"),
        new("可交换|可以交易|有价可谈|现实交换|tradeable|negotiable", "不可交换|不可交易|无价|底线|绝不交易|non-tradeable|nonnegotiable")
    };

    public static PersonaIntermediateCandidateParseResult Parse(
        string? candidateJson,
        string? sourceText,
        string? localId,
        string? localDisplayName,
        PersonaTagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        string source = (sourceText ?? string.Empty).Trim();
        string evidenceSource = source;
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(localId)) return Failure("persona.intermediate_request_invalid");

        string candidate = (candidateJson ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(candidate)) return Failure("persona.intermediate_candidate_empty");
        if (Encoding.UTF8.GetByteCount(candidate) > MaximumCandidateBytes) return Failure("persona.intermediate_candidate_size_invalid");

        try
        {
            using JsonDocument json = JsonDocument.Parse(candidate, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
            if (json.RootElement.ValueKind != JsonValueKind.Object) return Failure("persona.intermediate_root_invalid");

            PersonaDocument document = new()
            {
                Id = localId.Trim(),
                DisplayName = (localDisplayName ?? string.Empty).Trim(),
                Core = source,
                SourceDescription = source,
                Status = PersonaReviewStatus.Draft
            };
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (JsonProperty property in json.RootElement.EnumerateObject())
            {
                if (!RootFields.Contains(property.Name) || !seen.Add(property.Name)) return Failure("persona.intermediate_unknown_or_duplicate_field");
                switch (property.Name)
                {
                    case "summary":
                        document.Summary = ReadGroundedTextOrEmpty(property.Value, evidenceSource);
                        break;
                    case "identityFacts":
                        document.IdentityFacts = ReadGroundedTextOrEmpty(property.Value, source);
                        break;
                    case "publicDescription":
                        document.PublicDescription = ReadGroundedTextOrEmpty(property.Value, source);
                        break;
                    case "privateDescription":
                        document.PrivateDescription = ReadGroundedTextOrEmpty(property.Value, source);
                        break;
                    case "contradictionDescription":
                        document.ContradictionDescription = ReadGroundedTextOrEmpty(property.Value, source);
                        break;
                    case "reaction":
                        if (!ReadReaction(property.Value, evidenceSource, document.ReactionProfile)) return Failure("persona.intermediate_unknown_or_duplicate_field");
                        break;
                    case "commitment":
                        if (!ReadCommitment(property.Value, evidenceSource, document.CommitmentProfile)) return Failure("persona.intermediate_unknown_or_duplicate_field");
                        break;
                    case "axes":
                        ReadAxes(property.Value, evidenceSource, document);
                        break;
                    case "flags":
                        ReadFlags(property.Value, evidenceSource, registry, document.Tags);
                        break;
                }
            }

            PersonaValidationResult validation = PersonaValidator.Validate(document, registry);
            if (!validation.IsValid) return Failure(validation.Errors[0].Code);
            document.Tags = document.Tags.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            return new PersonaIntermediateCandidateParseResult { Document = document };
        }
        catch (JsonException)
        {
            return Failure("persona.intermediate_json_invalid");
        }
    }


    public static PersonaDocument CreateHeuristicDocument(
        string? sourceText,
        string? localId,
        string? localDisplayName,
        PersonaTagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        string source = (sourceText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(localId))
        {
            throw new ArgumentException("persona.intermediate_request_invalid");
        }

        PersonaDocument document = new()
        {
            Id = localId.Trim(),
            DisplayName = (localDisplayName ?? string.Empty).Trim(),
            Core = source,
            SourceDescription = source,
            Status = PersonaReviewStatus.Draft
        };
        for (int index = 0; index < AxisEvidenceRules.Length; index++)
        {
            AxisEvidenceRule rule = AxisEvidenceRules[index];
            bool negative = ContainsAny(source, rule.NegativeTerms);
            bool positive = ContainsAny(source, rule.PositiveTerms);
            if (negative == positive) continue;
            SetAxis(document, index, negative ? -1 : 1);
        }
        for (int index = 0; index <= 1; index++)
        {
            if (!HasFlagEvidence(index, source)) continue;
            string tagId = index == 0 ? "trigger.public_humiliation" : "boundary.no_empty_promises";
            if (registry.TryGet(tagId, out _)) document.Tags.Add(tagId);
        }
        document.Tags = document.Tags.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
        PersonaValidationResult validation = PersonaValidator.Validate(document, registry);
        if (!validation.IsValid) throw new InvalidOperationException(validation.Errors[0].Code);
        return document;
    }
    private static void ReadAxes(JsonElement element, string source, PersonaDocument document)
    {
        if (element.ValueKind != JsonValueKind.Array) return;
        HashSet<int> seenIndices = new();
        foreach (JsonElement entry in element.EnumerateArray())
        {
            if (!TryReadEntry(entry, AxisEntryFields, out Dictionary<string, JsonElement> values)) continue;
            if (!values.TryGetValue("index", out JsonElement indexElement)
                || !indexElement.TryGetInt32(out int index)
                || index is < 0 or > 24
                || !seenIndices.Add(index)) continue;
            if (!values.TryGetValue("value", out JsonElement valueElement)
                || !valueElement.TryGetInt32(out int value)
                || value is not (-2 or -1 or 1 or 2)) continue;
            if (!values.TryGetValue("source", out JsonElement sourceElement)) continue;
            string evidence = ReadGroundedTextOrEmpty(sourceElement, source);
            if (string.IsNullOrWhiteSpace(evidence) || !HasAxisEvidence(index, value, evidence)) continue;
            SetAxis(document, index, value);
        }
    }

    private static void ReadFlags(JsonElement element, string source, PersonaTagRegistry registry, List<string> tags)
    {
        if (element.ValueKind != JsonValueKind.Array) return;
        HashSet<int> seenIndices = new();
        foreach (JsonElement entry in element.EnumerateArray())
        {
            if (!TryReadEntry(entry, FlagEntryFields, out Dictionary<string, JsonElement> values)) continue;
            if (!values.TryGetValue("index", out JsonElement indexElement)
                || !indexElement.TryGetInt32(out int index)
                || index is < 0 or > 1
                || !seenIndices.Add(index)) continue;
            if (!values.TryGetValue("source", out JsonElement sourceElement)) continue;
            string evidence = ReadGroundedTextOrEmpty(sourceElement, source);
            if (string.IsNullOrWhiteSpace(evidence) || !HasFlagEvidence(index, evidence)) continue;
            string tagId = index == 0 ? "trigger.public_humiliation" : "boundary.no_empty_promises";
            if (registry.TryGet(tagId, out _)) tags.Add(tagId);
        }
    }

    private static bool TryReadEntry(JsonElement element, HashSet<string> allowedFields, out Dictionary<string, JsonElement> values)
    {
        values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object) return false;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowedFields.Contains(property.Name) || !values.TryAdd(property.Name, property.Value)) return false;
        }
        return true;
    }

    private static bool ReadReaction(JsonElement element, string source, PersonaReactionProfile profile)
    {
        if (element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.Object) return false;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!ReactionFields.Contains(property.Name) || !seen.Add(property.Name)) return false;
            string text = ReadGroundedTextOrEmpty(property.Value, source);
            if (property.Name == "sensitiveConditions") profile.SensitiveConditions = text;
            else profile.ConditionalResponses = text;
        }
        return true;
    }


    private static bool ReadCommitment(JsonElement element, string source, PersonaCommitmentProfile profile)
    {
        if (element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.Object) return false;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!CommitmentFields.Contains(property.Name) || !seen.Add(property.Name)) return false;
            string text = ReadGroundedTextOrEmpty(property.Value, source);
            switch (property.Name)
            {
                case "priorityOrder": profile.PriorityOrder = text; break;
                case "protectedValues": profile.ProtectedValues = text; break;
                case "applicableScope": profile.ApplicableScope = text; break;
                case "exceptionCost": profile.ExceptionCost = text; break;
                case "breachResponse": profile.BreachResponse = text; break;
            }
        }
        return true;
    }


    private static string ReadGroundedTextOrEmpty(JsonElement element, string source)
    {
        if (element.ValueKind != JsonValueKind.String) return string.Empty;
        string value = (element.GetString() ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(value) || Encoding.UTF8.GetByteCount(value) > MaximumTextBytes) return string.Empty;
        return IsOrderedSourceExtraction(Normalize(source), Normalize(value)) ? value : string.Empty;
    }

    private static bool HasAxisEvidence(int index, int value, string evidence)
    {
        if (index < 0 || index >= AxisEvidenceRules.Length) return false;
        AxisEvidenceRule rule = AxisEvidenceRules[index];
        return ContainsAny(evidence, value < 0 ? rule.NegativeTerms : rule.PositiveTerms);
    }

    private static bool HasFlagEvidence(int index, string evidence)
    {
        return index switch
        {
            0 => ContainsAny(evidence, "公开羞辱|当众羞辱|众目睽睽下羞辱|public humiliation")
                && ContainsAny(evidence, "反击|还击|报复|翻脸|fight back|retaliat"),
            1 => ContainsAny(evidence, "空头许诺|空头承诺|不轻易许诺|不轻易承诺|不随便答应|拒绝承诺|no empty promises|promise lightly"),
            _ => false
        };
    }

    private static bool ContainsAny(string evidence, string terms)
    {
        foreach (string term in terms.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            int searchIndex = 0;
            while (searchIndex < evidence.Length)
            {
                int matchIndex = evidence.IndexOf(term, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0) break;
                if (IsWholeTermMatch(evidence, term, matchIndex) && !IsNegatedMatch(evidence, matchIndex, term)) return true;
                searchIndex = matchIndex + Math.Max(term.Length, 1);
            }
        }
        return false;
    }

    private static bool IsWholeTermMatch(string evidence, string term, int index)
    {
        bool asciiTerm = term.All(character => character < 128 && (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character == '-'));
        if (!asciiTerm) return true;
        bool beforeOk = index == 0 || (!char.IsLetterOrDigit(evidence[index - 1]) && evidence[index - 1] != '-');
        int end = index + term.Length;
        bool afterOk = end >= evidence.Length || (!char.IsLetterOrDigit(evidence[end]) && evidence[end] != '-');
        return beforeOk && afterOk;
    }

    private static bool IsNegatedMatch(string evidence, int index, string term)
    {
        if (term.StartsWith("不", StringComparison.Ordinal) || term.StartsWith("不可", StringComparison.Ordinal) || term.StartsWith("non-", StringComparison.OrdinalIgnoreCase)) return false;
        int start = Math.Max(0, index - 6);
        string prefix = evidence[start..index];
        return prefix.EndsWith("不", StringComparison.Ordinal)
            || prefix.EndsWith("非", StringComparison.Ordinal)
            || prefix.EndsWith("未", StringComparison.Ordinal)
            || prefix.EndsWith("无", StringComparison.Ordinal)
            || prefix.EndsWith("不采用", StringComparison.Ordinal)
            || prefix.EndsWith("避免", StringComparison.Ordinal)
            || prefix.EndsWith("拒绝", StringComparison.Ordinal);
    }
    private readonly record struct AxisEvidenceRule(string NegativeTerms, string PositiveTerms);

    private static void SetAxis(PersonaDocument document, int index, int value)
    {
        switch (index)
        {
            case 0: document.TraitProfile.Caution = value; break;
            case 1: document.TraitProfile.Ambition = value; break;
            case 2: document.TraitProfile.Pride = value; break;
            case 3: document.TraitProfile.Pragmatism = value; break;
            case 4: document.TraitProfile.InGroupLoyalty = value; break;
            case 5: document.TraitProfile.Tradition = value; break;
            case 6: document.ExpressionProfile.Restraint = value; break;
            case 7: document.ExpressionProfile.Directness = value; break;
            case 8: document.ExpressionProfile.Formality = value; break;
            case 9: document.ExpressionProfile.Playfulness = value; break;
            case 10: document.ExpressionProfile.Warmth = value; break;
            case 11: document.BehaviorProfile.Conditionality = value; break;
            case 12: document.BehaviorProfile.Deliberation = value; break;
            case 13: document.BehaviorProfile.TrustTesting = value; break;
            case 14: document.BehaviorProfile.Leverage = value; break;
            case 15: document.BehaviorProfile.InGroupPriority = value; break;
            case 16: document.BehaviorProfile.Leadership = value; break;
            case 17: document.ReactionProfile.Confrontation = value; break;
            case 18: document.ReactionProfile.Expression = value; break;
            case 19: document.ReactionProfile.Timing = value; break;
            case 20: document.ReactionProfile.Resentment = value; break;
            case 21: document.ReactionProfile.SupportSeeking = value; break;
            case 22: document.CommitmentProfile.PromiseCaution = value; break;
            case 23: document.CommitmentProfile.PromisePersistence = value; break;
            case 24: document.CommitmentProfile.ValueTradeability = value; break;
        }
    }

    private static bool IsOrderedSourceExtraction(string source, string candidate)
    {
        if (candidate.Length == 0) return true;
        int sourceIndex = 0;
        foreach (char candidateCharacter in candidate)
        {
            int matchIndex = source.IndexOf(candidateCharacter, sourceIndex);
            if (matchIndex < 0) return false;
            sourceIndex = matchIndex + 1;
        }
        return true;
    }

    private static string Normalize(string value)
    {
        StringBuilder builder = new(value.Length);
        bool whitespace = false;
        foreach (char character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!whitespace) builder.Append(' ');
                whitespace = true;
            }
            else
            {
                builder.Append(char.ToLowerInvariant(character));
                whitespace = false;
            }
        }
        return builder.ToString();
    }

    private static PersonaIntermediateCandidateParseResult Failure(string errorCode) => new() { ErrorCode = errorCode };
}


