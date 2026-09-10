using System.Text;

namespace PersonaWorkbench.Core;

public sealed class PersonaDslCandidateParseResult
{
    public bool IsValid { get; init; }
    public PersonaDocument? Document { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
}

public static class PersonaDslCandidateParser
{
    private const int MaximumCandidateBytes = 32 * 1024;
    private const int MaximumLines = 256;
    private static readonly HashSet<string> AllowedSections = new HashSet<string>(StringComparer.Ordinal)
    {
        "PERSONA_LOAD",
        "PERSONALITY_CORE",
        "PERSONALITY_PUBLIC",
        "PERSONALITY_PRIVATE",
        "PERSONALITY_CONTRADICTION",
        "PERSONALITY_SUMMARY",
        "SELF_IDENTITY",
        "SELF_CLAIM_RULES",
        "REAL_SELF_BEHAVIOR",
        "SELF_CLAIM_EXAMPLES",
        "FOOD_PREFERENCE"
    };
    private static readonly AxisDefinition[] AxisDefinitions =
    {
        new("PERSONALITY_CORE", "TRAIT_RISK", "BOLD", "CAUTIOUS", (document, value) => document.TraitProfile.Caution = value),
        new("PERSONALITY_CORE", "TRAIT_AMBITION", "CONTENT", "AMBITIOUS", (document, value) => document.TraitProfile.Ambition = value),
        new("PERSONALITY_CORE", "TRAIT_PRIDE", "HUMBLE", "PROUD", (document, value) => document.TraitProfile.Pride = value),
        new("PERSONALITY_CORE", "TRAIT_PRAGMATISM", "IDEALISTIC", "PRAGMATIC", (document, value) => document.TraitProfile.Pragmatism = value),
        new("PERSONALITY_CORE", "TRAIT_IN_GROUP_LOYALTY", "INDEPENDENT", "GUARDIAN", (document, value) => document.TraitProfile.InGroupLoyalty = value),
        new("PERSONALITY_CORE", "TRAIT_TRADITION", "NOVELTY_SEEKING", "TRADITIONAL", (document, value) => document.TraitProfile.Tradition = value),
        new("PERSONALITY_PUBLIC", "EXPRESSION_RESTRAINT", "SPONTANEOUS", "MEASURED", (document, value) => document.ExpressionProfile.Restraint = value),
        new("PERSONALITY_PUBLIC", "EXPRESSION_DIRECTNESS", "INDIRECT", "DIRECT", (document, value) => document.ExpressionProfile.Directness = value),
        new("PERSONALITY_PUBLIC", "EXPRESSION_FORMALITY", "CASUAL", "FORMAL", (document, value) => document.ExpressionProfile.Formality = value),
        new("PERSONALITY_PUBLIC", "EXPRESSION_PLAYFULNESS", "SERIOUS", "TEASING", (document, value) => document.ExpressionProfile.Playfulness = value),
        new("PERSONALITY_PUBLIC", "EXPRESSION_WARMTH", "ALOOF", "WARM", (document, value) => document.ExpressionProfile.Warmth = value),
        new("PERSONALITY_PRIVATE", "BEHAVIOR_CONDITIONALITY", "GIVING_FIRST", "BARGAINING_FIRST", (document, value) => document.BehaviorProfile.Conditionality = value),
        new("PERSONALITY_PRIVATE", "BEHAVIOR_DELIBERATION", "ACT_FIRST", "OBSERVE_FIRST", (document, value) => document.BehaviorProfile.Deliberation = value),
        new("PERSONALITY_PRIVATE", "BEHAVIOR_TRUST", "TRUSTING", "LOYALTY_TESTING", (document, value) => document.BehaviorProfile.TrustTesting = value),
        new("PERSONALITY_PRIVATE", "BEHAVIOR_LEVERAGE", "REVEALS_CARDS", "KEEPS_LEVERAGE", (document, value) => document.BehaviorProfile.Leverage = value),
        new("PERSONALITY_PRIVATE", "BEHAVIOR_IN_GROUP_PRIORITY", "IMPARTIAL", "PROTECTS_IN_GROUP", (document, value) => document.BehaviorProfile.InGroupPriority = value),
        new("PERSONALITY_PRIVATE", "BEHAVIOR_LEADERSHIP", "COOPERATIVE", "COMMANDING", (document, value) => document.BehaviorProfile.Leadership = value),
        new("PERSONALITY_PRIVATE", "REACTION_CONFRONTATION", "AVOIDANT", "CONFRONTATIONAL", (document, value) => document.ReactionProfile.Confrontation = value),
        new("PERSONALITY_PRIVATE", "REACTION_EMOTION", "SUPPRESSED", "EXTERNALIZED", (document, value) => document.ReactionProfile.Expression = value),
        new("PERSONALITY_PRIVATE", "REACTION_TIMING", "DELAYED", "IMMEDIATE", (document, value) => document.ReactionProfile.Timing = value),
        new("PERSONALITY_PRIVATE", "REACTION_AFTERCARE", "RECONCILING", "RESENTFUL", (document, value) => document.ReactionProfile.Resentment = value),
        new("PERSONALITY_PRIVATE", "REACTION_SUPPORT", "SELF_RELIANT", "SUPPORT_SEEKING", (document, value) => document.ReactionProfile.SupportSeeking = value),
        new("PERSONALITY_CONTRADICTION", "COMMITMENT_PROMISE", "EASY_PROMISES", "CAUTIOUS_PROMISES", (document, value) => document.CommitmentProfile.PromiseCaution = value),
        new("PERSONALITY_CONTRADICTION", "COMMITMENT_FULFILLMENT", "FLEXIBLE_FULFILLMENT", "PERSISTENT_FULFILLMENT", (document, value) => document.CommitmentProfile.PromisePersistence = value),
        new("PERSONALITY_CONTRADICTION", "COMMITMENT_VALUE", "TRADEABLE_VALUES", "NON_TRADEABLE_VALUES", (document, value) => document.CommitmentProfile.ValueTradeability = value)
    };

    public static PersonaDslCandidateParseResult Parse(
        string? candidateDsl,
        string? sourceDescription,
        string? localId,
        string? localDisplayName,
        PersonaTagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        string normalized = NormalizeCandidate(candidateDsl ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized)) return Failure("persona.dsl_candidate_empty");
        if (Encoding.UTF8.GetByteCount(normalized) > MaximumCandidateBytes) return Failure("persona.dsl_candidate_size_invalid");

        string[] lines = normalized.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        if (lines.Length > MaximumLines) return Failure("persona.dsl_candidate_line_count_invalid");

        PersonaDocument document = new PersonaDocument
        {
            Id = string.IsNullOrWhiteSpace(localId) ? "free.generated.persona" : localId.Trim(),
            DisplayName = localDisplayName?.Trim() ?? string.Empty,
            TemplateVersion = "persona-load.v2",
            Status = PersonaReviewStatus.Draft,
            SourceDescription = sourceDescription?.Trim() ?? string.Empty
        };
        Dictionary<string, PersonaTagDefinition> canonicalTags = registry.Ids
            .Select(id => registry.TryGet(id, out PersonaTagDefinition definition) ? definition : null)
            .Where(definition => definition != null)
            .ToDictionary(definition => ToCanonicalTagId(definition!.Id), definition => definition!, StringComparer.Ordinal);
        HashSet<string> seenSections = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> seenEntries = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> seenSemanticEntries = new HashSet<string>(StringComparer.Ordinal);
        string currentSection = string.Empty;
        bool hasSelfClaimFlag = false;
        bool hasLanguageFlag = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = line[1..^1];
                if (!AllowedSections.Contains(currentSection)) return Failure("persona.dsl_candidate_section_unknown");
                if (!seenSections.Add(currentSection)) return Failure("persona.dsl_candidate_section_duplicate");
                continue;
            }
            if (currentSection.Length == 0) return Failure("persona.dsl_candidate_section_missing");

            string entryKey = currentSection + "\n" + line;
            if (!seenEntries.Add(entryKey)) return Failure("persona.dsl_candidate_entry_duplicate");

            if (currentSection == "PERSONA_LOAD")
            {
                if (line == "SELF_CLAIM_NAME") hasSelfClaimFlag = true;
                else if (line == "LANG_ZH_CN_ONLY") hasLanguageFlag = true;
                else return Failure("persona.dsl_candidate_local_metadata_forbidden");
                continue;
            }

            if (TryApplyAxis(document, currentSection, line, out string axisKey))
            {
                if (!seenSemanticEntries.Add("axis:" + axisKey)) return Failure("persona.dsl_candidate_axis_duplicate");
                continue;
            }
            if (TryApplyFacet(document, currentSection, line, canonicalTags, out string facetError, out string facetKey))
            {
                if (facetError.Length > 0) return Failure(facetError);
                if (!seenSemanticEntries.Add("facet:" + facetKey)) return Failure("persona.dsl_candidate_facet_duplicate");
                continue;
            }
            if (TryApplyTag(document, currentSection, line, canonicalTags)) continue;
            if (!TryReadAssignment(line, out string key, out string value)) return Failure("persona.dsl_candidate_entry_unknown");
            string fieldKey = currentSection + "." + key;
            if (!seenEntries.Add(fieldKey)) return Failure("persona.dsl_candidate_field_duplicate");
            if (!TryApplyAssignment(document, currentSection, key, value)) return Failure("persona.dsl_candidate_field_unknown");
        }

        if (!seenSections.Contains("PERSONA_LOAD") || !hasSelfClaimFlag || !hasLanguageFlag) return Failure("persona.dsl_candidate_load_header_invalid");
        PersonaValidationResult validation = PersonaValidator.Validate(document, registry);
        if (!validation.IsValid) return Failure(validation.Errors[0].Code);
        return new PersonaDslCandidateParseResult { IsValid = true, Document = document };
    }

    private static bool TryApplyAxis(PersonaDocument document, string section, string token, out string axisKey)
    {
        axisKey = string.Empty;
        foreach (AxisDefinition definition in AxisDefinitions)
        {
            if (definition.Section != section) continue;
            if (token == definition.Prefix + "_BALANCED") { axisKey = definition.Prefix; definition.Setter(document, 0); return true; }
            if (token == definition.Prefix + "_" + definition.Negative + "_STRONG") { axisKey = definition.Prefix; definition.Setter(document, -2); return true; }
            if (token == definition.Prefix + "_" + definition.Negative + "_SLIGHT") { axisKey = definition.Prefix; definition.Setter(document, -1); return true; }
            if (token == definition.Prefix + "_" + definition.Positive + "_SLIGHT") { axisKey = definition.Prefix; definition.Setter(document, 1); return true; }
            if (token == definition.Prefix + "_" + definition.Positive + "_STRONG") { axisKey = definition.Prefix; definition.Setter(document, 2); return true; }
        }
        return false;
    }

    private static bool TryApplyFacet(
        PersonaDocument document,
        string section,
        string token,
        IReadOnlyDictionary<string, PersonaTagDefinition> canonicalTags,
        out string errorCode,
        out string facetKey)
    {
        errorCode = string.Empty;
        facetKey = string.Empty;
        if (!token.StartsWith("FACET_", StringComparison.Ordinal)) return false;
        int strengthIndex = token.LastIndexOf("_STRENGTH_", StringComparison.Ordinal);
        if (strengthIndex <= 6 || !int.TryParse(token[(strengthIndex + 10)..], out int strength) || strength is < 1 or > 4)
        {
            errorCode = "persona.dsl_candidate_facet_invalid";
            return true;
        }
        string canonicalTag = token[6..strengthIndex];
        if (!canonicalTags.TryGetValue(canonicalTag, out PersonaTagDefinition? definition)
            || !SectionAcceptsCategory(section, definition.Category)
            || definition.Category is PersonaTagCategory.Trigger or PersonaTagCategory.Boundary)
        {
            errorCode = "persona.dsl_candidate_facet_invalid";
            return true;
        }
        facetKey = definition.Id;
        document.FacetStrengths[definition.Id] = strength;
        return true;
    }

    private static bool TryApplyTag(
        PersonaDocument document,
        string section,
        string token,
        IReadOnlyDictionary<string, PersonaTagDefinition> canonicalTags)
    {
        if (!canonicalTags.TryGetValue(token, out PersonaTagDefinition? definition) || !SectionAcceptsCategory(section, definition.Category)) return false;
        document.Tags.Add(definition.Id);
        return true;
    }

    private static bool SectionAcceptsCategory(string section, PersonaTagCategory category)
    {
        return section switch
        {
            "PERSONALITY_CORE" => category == PersonaTagCategory.Trait,
            "PERSONALITY_PUBLIC" => category == PersonaTagCategory.Expression,
            "PERSONALITY_PRIVATE" => category == PersonaTagCategory.Behavior,
            "PERSONALITY_CONTRADICTION" => category is PersonaTagCategory.Trigger or PersonaTagCategory.Boundary,
            _ => false
        };
    }

    private static bool TryApplyAssignment(PersonaDocument document, string section, string key, string value)
    {
        switch (section + "." + key)
        {
            case "PERSONALITY_CORE.DESC_CN": document.Core = value; return true;
            case "PERSONALITY_PUBLIC.DESC_CN": document.PublicDescription = value; return true;
            case "PERSONALITY_PRIVATE.SENSITIVE_CONDITIONS": document.ReactionProfile.SensitiveConditions = value; return true;
            case "PERSONALITY_PRIVATE.CONDITIONAL_RESPONSES": document.ReactionProfile.ConditionalResponses = value; return true;
            case "PERSONALITY_PRIVATE.DESC_CN": document.PrivateDescription = value; return true;
            case "PERSONALITY_CONTRADICTION.PRIORITY_ORDER": document.CommitmentProfile.PriorityOrder = value; return true;
            case "PERSONALITY_CONTRADICTION.PROTECTED_VALUES": document.CommitmentProfile.ProtectedValues = value; return true;
            case "PERSONALITY_CONTRADICTION.APPLICABLE_SCOPE": document.CommitmentProfile.ApplicableScope = value; return true;
            case "PERSONALITY_CONTRADICTION.EXCEPTION_COST": document.CommitmentProfile.ExceptionCost = value; return true;
            case "PERSONALITY_CONTRADICTION.BREACH_RESPONSE": document.CommitmentProfile.BreachResponse = value; return true;
            case "PERSONALITY_CONTRADICTION.DESC_CN": document.ContradictionDescription = value; return true;
            case "PERSONALITY_SUMMARY.DESC_CN": document.Summary = value; return true;
            case "SELF_IDENTITY.FACTS_CN": document.IdentityFacts = value; return true;
            case "FOOD_PREFERENCE.FOOD_CN": document.FoodPreference = value; return true;
        }

        if (section == "SELF_CLAIM_RULES" && TryReadIndexedKey(key, "RULE", out _)) { document.SelfClaimRules.Add(value); return true; }
        if (section == "REAL_SELF_BEHAVIOR" && TryReadIndexedKey(key, "BEHAVIOR", out _)) { document.RealSelfBehaviors.Add(value); return true; }
        if (section == "SELF_CLAIM_EXAMPLES" && TryReadIndexedKey(key, "EXAMPLE", out _)) { document.SelfClaimExamples.Add(value); return true; }
        return false;
    }

    private static bool TryReadIndexedKey(string key, string prefix, out int index)
    {
        index = 0;
        return key.StartsWith(prefix + "_", StringComparison.Ordinal)
            && int.TryParse(key[(prefix.Length + 1)..], out index)
            && index is >= 1 and <= 16;
    }

    private static bool TryReadAssignment(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        int separator = line.IndexOf('=');
        if (separator <= 0) return false;
        key = line[..separator];
        string encoded = line[(separator + 1)..];
        if (key.Any(character => !(character is >= 'A' and <= 'Z' || character is >= '0' and <= '9' || character == '_'))
            || encoded.Length < 2
            || encoded[0] != '"'
            || encoded[^1] != '"') return false;

        StringBuilder decoded = new StringBuilder(encoded.Length - 2);
        bool escaping = false;
        for (int index = 1; index < encoded.Length - 1; index++)
        {
            char character = encoded[index];
            if (escaping)
            {
                if (character is not ('\\' or '"')) return false;
                decoded.Append(character);
                escaping = false;
            }
            else if (character == '\\') escaping = true;
            else if (character == '"' || char.IsControl(character)) return false;
            else decoded.Append(character);
        }
        if (escaping) return false;
        value = decoded.ToString().Trim();
        return true;
    }

    private static string NormalizeCandidate(string candidate)
    {
        string normalized = candidate.Trim();
        if (!normalized.StartsWith("```", StringComparison.Ordinal) || !normalized.EndsWith("```", StringComparison.Ordinal)) return normalized;
        int firstLineEnd = normalized.IndexOf('\n');
        if (firstLineEnd < 0) return normalized;
        normalized = normalized[(firstLineEnd + 1)..].Trim();
        return normalized.EndsWith("```", StringComparison.Ordinal) ? normalized[..^3].Trim() : normalized;
    }

    private static string ToCanonicalTagId(string id)
    {
        return id.Replace('.', '_').Replace('-', '_').ToUpperInvariant();
    }

    private static PersonaDslCandidateParseResult Failure(string errorCode)
    {
        return new PersonaDslCandidateParseResult { IsValid = false, ErrorCode = errorCode };
    }

    private sealed record AxisDefinition(
        string Section,
        string Prefix,
        string Negative,
        string Positive,
        Action<PersonaDocument, int> Setter);
}
