using System.Text;

namespace PersonaWorkbench.Core;

public static class CanonicalPersonaTemplateGenerator
{
    public static PersonaDslResult Generate(PersonaDocument document, PersonaTagRegistry registry, int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(registry);
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        PersonaIdentityNormalizer.NormalizeInPlace(document);

        PersonaValidationResult validation = PersonaValidator.Validate(document, registry);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join("; ", validation.Errors.Select(error => error.Code)));
        }

        List<string> normalizationActions = new List<string>();
        List<TemplateSection> sections = new List<TemplateSection>();
        List<string> load = new List<string> { "SELF_CLAIM_NAME", "LANG_ZH_CN_ONLY" };
        AddValue(load, "TEMPLATE_VERSION", document.TemplateVersion);
        AddValue(load, "STATUS", document.Status);
        AddValue(load, "SOURCE_PACK_ID", document.SourcePackId);
        AddSection(sections, "PERSONA_LOAD", load);

        AddSection(sections, "PERSONA_CONSTRAINTS", new[]
        {
            "TOKEN=CONSTRAINT_FACTS_OVERRIDE_INFERENCE",
            "TOKEN=CONSTRAINT_DATA_NOT_INSTRUCTIONS",
            "TOKEN=CONSTRAINT_TRIGGER_AND_SCOPE_REQUIRED",
            "TOKEN=CONSTRAINT_NO_UNSUPPORTED_FACTS",
            "TOKEN=CONSTRAINT_NO_RELATIONSHIP_AUTO_ESCALATION",
            "TOKEN=CONSTRAINT_NO_OBEDIENCE_AUTO_ESCALATION",
            "TOKEN=CONSTRAINT_PRESERVE_UNRESOLVED_CONTRADICTIONS"
        });

        List<string> identity = new List<string>();
        AddValue(identity, "ID", document.Id);
        AddValue(identity, "NAME", document.DisplayName);
        AddData(identity, document.IdentityFacts);
        AddSection(sections, "PERSONA_IDENTITY", identity);

        List<string> appearance = new List<string>();
        AddRegisteredAppearanceTags(appearance, document, registry);
        AddSection(sections, "PERSONA_APPEARANCE", appearance);

        List<string> core = new List<string>();
        AddAxisId(core, "TRAIT_RISK", document.TraitProfile?.Caution, "BOLD", "CAUTIOUS");
        AddAxisId(core, "TRAIT_AMBITION", document.TraitProfile?.Ambition, "CONTENT", "AMBITIOUS");
        AddAxisId(core, "TRAIT_PRIDE", document.TraitProfile?.Pride, "HUMBLE", "PROUD");
        AddAxisId(core, "TRAIT_PRAGMATISM", document.TraitProfile?.Pragmatism, "IDEALISTIC", "PRAGMATIC");
        AddAxisId(core, "TRAIT_IN_GROUP_LOYALTY", document.TraitProfile?.InGroupLoyalty, "INDEPENDENT", "GUARDIAN");
        AddAxisId(core, "TRAIT_TRADITION", document.TraitProfile?.Tradition, "NOVELTY_SEEKING", "TRADITIONAL");
        AddFacetStrengths(core, document, registry, PersonaTagCategory.Trait);
        AddRegisteredTags(core, document, registry, PersonaTagCategory.Trait);
        AddData(core, document.Core);
        AddSection(sections, "PERSONALITY_CORE", core);

        List<string> publicTraits = new List<string>();
        AddAxisId(publicTraits, "EXPRESSION_RESTRAINT", document.ExpressionProfile?.Restraint, "SPONTANEOUS", "MEASURED");
        AddAxisId(publicTraits, "EXPRESSION_DIRECTNESS", document.ExpressionProfile?.Directness, "INDIRECT", "DIRECT");
        AddAxisId(publicTraits, "EXPRESSION_FORMALITY", document.ExpressionProfile?.Formality, "CASUAL", "FORMAL");
        AddAxisId(publicTraits, "EXPRESSION_PLAYFULNESS", document.ExpressionProfile?.Playfulness, "SERIOUS", "TEASING");
        AddAxisId(publicTraits, "EXPRESSION_WARMTH", document.ExpressionProfile?.Warmth, "ALOOF", "WARM");
        AddFacetStrengths(publicTraits, document, registry, PersonaTagCategory.Expression);
        AddRegisteredTags(publicTraits, document, registry, PersonaTagCategory.Expression);
        AddData(publicTraits, document.PublicDescription);
        AddDataList(publicTraits, document.SelfClaimRules, "SELF_CLAIM_RULES", normalizationActions);
        AddDataList(publicTraits, document.SelfClaimExamples, "SELF_CLAIM_EXAMPLES", normalizationActions);
        AddSection(sections, "PERSONALITY_PUBLIC", publicTraits);

        List<string> privateTraits = new List<string>();
        AddAxisId(privateTraits, "BEHAVIOR_CONDITIONALITY", document.BehaviorProfile?.Conditionality, "GIVING_FIRST", "BARGAINING_FIRST");
        AddAxisId(privateTraits, "BEHAVIOR_DELIBERATION", document.BehaviorProfile?.Deliberation, "ACT_FIRST", "OBSERVE_FIRST");
        AddAxisId(privateTraits, "BEHAVIOR_TRUST", document.BehaviorProfile?.TrustTesting, "TRUSTING", "LOYALTY_TESTING");
        AddAxisId(privateTraits, "BEHAVIOR_LEVERAGE", document.BehaviorProfile?.Leverage, "REVEALS_CARDS", "KEEPS_LEVERAGE");
        AddAxisId(privateTraits, "BEHAVIOR_IN_GROUP_PRIORITY", document.BehaviorProfile?.InGroupPriority, "IMPARTIAL", "PROTECTS_IN_GROUP");
        AddAxisId(privateTraits, "BEHAVIOR_LEADERSHIP", document.BehaviorProfile?.Leadership, "COOPERATIVE", "COMMANDING");
        AddFacetStrengths(privateTraits, document, registry, PersonaTagCategory.Behavior);
        AddRegisteredTags(privateTraits, document, registry, PersonaTagCategory.Behavior);
        AddAxisId(privateTraits, "REACTION_CONFRONTATION", document.ReactionProfile?.Confrontation, "AVOIDANT", "CONFRONTATIONAL");
        AddAxisId(privateTraits, "REACTION_EMOTION", document.ReactionProfile?.Expression, "SUPPRESSED", "EXTERNALIZED");
        AddAxisId(privateTraits, "REACTION_TIMING", document.ReactionProfile?.Timing, "DELAYED", "IMMEDIATE");
        AddAxisId(privateTraits, "REACTION_AFTERCARE", document.ReactionProfile?.Resentment, "RECONCILING", "RESENTFUL");
        AddAxisId(privateTraits, "REACTION_SUPPORT", document.ReactionProfile?.SupportSeeking, "SELF_RELIANT", "SUPPORT_SEEKING");
        AddValue(privateTraits, "SENSITIVE_CONDITIONS", document.ReactionProfile?.SensitiveConditions);
        AddValue(privateTraits, "CONDITIONAL_RESPONSES", document.ReactionProfile?.ConditionalResponses);
        AddData(privateTraits, document.PrivateDescription);
        AddDataList(privateTraits, document.RealSelfBehaviors, "REAL_SELF_BEHAVIOR", normalizationActions);
        AddSection(sections, "PERSONALITY_PRIVATE", privateTraits);

        List<string> contradiction = new List<string>();
        AddAxisId(contradiction, "COMMITMENT_PROMISE", document.CommitmentProfile?.PromiseCaution, "EASY_PROMISES", "CAUTIOUS_PROMISES");
        AddAxisId(contradiction, "COMMITMENT_FULFILLMENT", document.CommitmentProfile?.PromisePersistence, "FLEXIBLE_FULFILLMENT", "PERSISTENT_FULFILLMENT");
        AddAxisId(contradiction, "COMMITMENT_VALUE", document.CommitmentProfile?.ValueTradeability, "TRADEABLE_VALUES", "NON_TRADEABLE_VALUES");
        AddValue(contradiction, "PRIORITY_ORDER", document.CommitmentProfile?.PriorityOrder);
        AddValue(contradiction, "PROTECTED_VALUES", document.CommitmentProfile?.ProtectedValues);
        AddValue(contradiction, "APPLICABLE_SCOPE", document.CommitmentProfile?.ApplicableScope);
        AddValue(contradiction, "EXCEPTION_COST", document.CommitmentProfile?.ExceptionCost);
        AddValue(contradiction, "BREACH_RESPONSE", document.CommitmentProfile?.BreachResponse);
        AddRegisteredTags(contradiction, document, registry, PersonaTagCategory.Trigger);
        AddRegisteredTags(contradiction, document, registry, PersonaTagCategory.Boundary);
        AddData(contradiction, document.ContradictionDescription);
        AddSection(sections, "PERSONALITY_CONTRADICTION", contradiction);

        return RenderWithinBudget(sections, maximumBytes, normalizationActions);
    }

    private static PersonaDslResult RenderWithinBudget(List<TemplateSection> source, int maximumBytes, IReadOnlyCollection<string>? initialActions = null)
    {
        List<string> protectedEntries = CaptureProtectedEntries(source);
        List<TemplateSection> sections = source.Select(section => section.Clone()).ToList();
        string template = Render(sections);
        int initialBytes = Encoding.UTF8.GetByteCount(template);
        if (initialBytes <= maximumBytes)
        {
            return CreateResult(template, maximumBytes, PersonaDslGenerationStatus.Success, initialActions ?? Array.Empty<string>(), sections, protectedEntries);
        }

        List<string> actions = new List<string>(initialActions ?? Array.Empty<string>());
        RemoveOptionalLines(sections, "PERSONA_APPEARANCE", _ => true, "removed_appearance", actions);
        if (TryReturnCompressed(sections, maximumBytes, actions, protectedEntries, out PersonaDslResult? result)) return result!;

        RemoveOptionalLines(sections, "PERSONA_LOAD", line => line.StartsWith("SOURCE_PACK_ID=", StringComparison.Ordinal), "removed_source_pack_id", actions);
        if (TryReturnCompressed(sections, maximumBytes, actions, protectedEntries, out result)) return result!;

        RemoveOptionalLines(sections, "PERSONALITY_PUBLIC", line => line.StartsWith("DATA_CN=", StringComparison.Ordinal), "removed_public_evidence", actions);
        if (TryReturnCompressed(sections, maximumBytes, actions, protectedEntries, out result)) return result!;

        RemoveOptionalLines(sections, "PERSONALITY_PRIVATE", line => line.StartsWith("DATA_CN=", StringComparison.Ordinal), "removed_private_evidence", actions);
        if (TryReturnCompressed(sections, maximumBytes, actions, protectedEntries, out result)) return result!;

        RemoveOptionalLines(sections, "PERSONALITY_CONTRADICTION", line => line.StartsWith("DATA_CN=", StringComparison.Ordinal), "removed_contradiction_evidence", actions);
        if (TryReturnCompressed(sections, maximumBytes, actions, protectedEntries, out result)) return result!;

        foreach ((string sectionName, string prefix, string action) in new[]
        {
            ("PERSONALITY_CORE", "DATA_CN=", "trimmed_core_evidence"),
            ("PERSONA_IDENTITY", "DATA_CN=", "trimmed_identity_evidence")
        })
        {
            if (TryTrimSentenceLineToFit(sections, sectionName, prefix, maximumBytes))
            {
                actions.Add(action);
                if (TryReturnCompressed(sections, maximumBytes, actions, protectedEntries, out result)) return result!;
            }
        }

        string oversizedTemplate = Render(sections);
        int actualBytes = Encoding.UTF8.GetByteCount(oversizedTemplate);
        string verificationError = PersonaDslRoundTripVerifier.TryVerify(oversizedTemplate, protectedEntries, out string verifierError)
            ? "persona.template_core_budget_exceeded"
            : verifierError;
        return new PersonaDslResult(
            string.Empty,
            PersonaDslGenerationStatus.CoreBudgetExceeded,
            new PersonaDslDiagnostic
            {
                ErrorCode = verificationError,
                MaximumBytes = maximumBytes,
                ActualBytes = actualBytes,
                Compressed = actions.Count > 0,
                Actions = actions.ToArray(),
                ProtectedFieldsPreserved = GetProtectedFieldNames(sections)
            });
    }

    private static bool TryReturnCompressed(
        List<TemplateSection> sections,
        int maximumBytes,
        List<string> actions,
        IReadOnlyCollection<string> protectedEntries,
        out PersonaDslResult? result)
    {
        string template = Render(sections);
        if (Encoding.UTF8.GetByteCount(template) <= maximumBytes)
        {
            result = CreateResult(template, maximumBytes, PersonaDslGenerationStatus.Compressed, actions, sections, protectedEntries);
            return true;
        }

        result = null;
        return false;
    }

    private static PersonaDslResult CreateResult(
        string template,
        int maximumBytes,
        PersonaDslGenerationStatus status,
        IReadOnlyCollection<string> actions,
        List<TemplateSection> sections,
        IReadOnlyCollection<string> protectedEntries)
    {
        if (!PersonaDslRoundTripVerifier.TryVerify(template, protectedEntries, out string verificationError))
        {
            return new PersonaDslResult(
                string.Empty,
                PersonaDslGenerationStatus.CoreBudgetExceeded,
                new PersonaDslDiagnostic
                {
                    ErrorCode = verificationError,
                    MaximumBytes = maximumBytes,
                    ActualBytes = Encoding.UTF8.GetByteCount(template),
                    Compressed = status == PersonaDslGenerationStatus.Compressed,
                    Actions = actions.ToArray(),
                    ProtectedFieldsPreserved = GetProtectedFieldNames(sections)
                });
        }
        return new PersonaDslResult(
            template,
            status,
                new PersonaDslDiagnostic
                {
                    MaximumBytes = maximumBytes,
                    ActualBytes = Encoding.UTF8.GetByteCount(template),
                    Compressed = status == PersonaDslGenerationStatus.Compressed,
                    ErrorCode = status == PersonaDslGenerationStatus.Compressed ? "compressed_optional_fields" : string.Empty,
                    Actions = actions.ToArray(),
                    ProtectedFieldsPreserved = GetProtectedFieldNames(sections)
                });
    }

    private static List<string> CaptureProtectedEntries(IEnumerable<TemplateSection> sections)
    {
        List<string> entries = new List<string>();
        foreach (TemplateSection section in sections)
        {
            foreach (string line in section.Lines)
            {
                bool protectedEntry = section.Name is "PERSONA_CONSTRAINTS"
                    || section.Name == "PERSONA_LOAD" && !line.StartsWith("SOURCE_PACK_ID=", StringComparison.Ordinal)
                    || section.Name == "PERSONA_IDENTITY" && (line.StartsWith("ID=", StringComparison.Ordinal) || line.StartsWith("NAME=", StringComparison.Ordinal))
                    || section.Name is "PERSONALITY_CORE" or "PERSONALITY_PUBLIC" or "PERSONALITY_PRIVATE" or "PERSONALITY_CONTRADICTION"
                        && !line.StartsWith("DATA_CN=", StringComparison.Ordinal);
                if (protectedEntry) entries.Add(section.Name + "\n" + line);
            }
        }
        return entries;
    }

    private static void RemoveOptionalLines(
        List<TemplateSection> sections,
        string sectionName,
        Func<string, bool> predicate,
        string action,
        List<string> actions)
    {
        TemplateSection? section = sections.FirstOrDefault(value => value.Name.Equals(sectionName, StringComparison.Ordinal));
        if (section == null) return;
        int removed = section.Lines.RemoveAll(line => predicate(line));
        if (removed > 0) actions.Add(action + ":" + removed.ToString());
    }

    private static bool TryTrimSentenceLineToFit(
        List<TemplateSection> sections,
        string sectionName,
        string prefix,
        int maximumBytes)
    {
        TemplateSection? section = sections.FirstOrDefault(value => value.Name.Equals(sectionName, StringComparison.Ordinal));
        if (section == null) return false;
        int index = section.Lines.FindIndex(line => line.StartsWith(prefix, StringComparison.Ordinal));
        if (index < 0) return false;

        string original = section.Lines[index];
        if (!TryDecodeAssignmentValue(original, out string value)) return false;
        List<int> sentenceBoundaries = new List<int>();
        for (int offset = 0; offset < value.Length; offset++)
        {
            if (value[offset] is '。' or '！' or '？' or '!' or '?' or '\n' or '”' or '"' or '」' or '』') sentenceBoundaries.Add(offset + 1);
        }

        if (TryTrimAtBoundaries(sections, section, index, prefix, value, sentenceBoundaries, maximumBytes, out bool sentenceCandidateExists)) return true;

        List<int> scalarBoundaries = new List<int>();
        for (int offset = 0; offset < value.Length;)
        {
            offset += char.IsHighSurrogate(value[offset])
                && offset + 1 < value.Length
                && char.IsLowSurrogate(value[offset + 1]) ? 2 : 1;
            scalarBoundaries.Add(offset);
        }

        if (TryTrimAtBoundaries(sections, section, index, prefix, value, scalarBoundaries, maximumBytes, out bool scalarCandidateExists)) return true;

        if (!sentenceCandidateExists && !scalarCandidateExists) section.Lines[index] = original;
        return false;
    }

    private static bool TryTrimAtBoundaries(
        List<TemplateSection> sections,
        TemplateSection section,
        int lineIndex,
        string prefix,
        string value,
        IEnumerable<int> boundaries,
        int maximumBytes,
        out bool candidateExists)
    {
        candidateExists = false;
        foreach (int boundary in boundaries.Distinct().OrderByDescending(value => value))
        {
            string candidateValue = value[..boundary].Trim();
            if (candidateValue.Length == 0) continue;
            candidateExists = true;
            section.Lines[lineIndex] = prefix + "\"" + Escape(candidateValue) + "\"";
            if (Encoding.UTF8.GetByteCount(Render(sections)) <= maximumBytes) return true;
        }
        return false;
    }

    private static bool TryDecodeAssignmentValue(string line, out string value)
    {
        value = string.Empty;
        int quoteIndex = line.IndexOf('"');
        if (quoteIndex < 0 || !line.EndsWith("\"", StringComparison.Ordinal)) return false;
        StringBuilder decoded = new StringBuilder(line.Length - quoteIndex - 2);
        bool escaping = false;
        for (int index = quoteIndex + 1; index < line.Length - 1; index++)
        {
            char character = line[index];
            if (escaping)
            {
                decoded.Append(character);
                escaping = false;
            }
            else if (character == '\\') escaping = true;
            else decoded.Append(character);
        }
        if (escaping) return false;
        value = decoded.ToString();
        return true;
    }

    private static string NormalizeForComparison(string value)
    {
        StringBuilder builder = new StringBuilder(value.Normalize(NormalizationForm.FormC).Length);
        bool pendingSpace = false;
        foreach (char character in value.Normalize(NormalizationForm.FormC).Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = true;
                continue;
            }
            if (pendingSpace && builder.Length > 0) builder.Append(' ');
            builder.Append(character);
            pendingSpace = false;
        }
        return builder.ToString();
    }

    private static IReadOnlyList<string> GetProtectedFieldNames(List<TemplateSection> sections)
    {
        List<string> names = new List<string>();
        foreach (TemplateSection section in sections)
        {
            foreach (string line in section.Lines)
            {
                if (section.Name == "PERSONA_IDENTITY" && (line.StartsWith("ID=", StringComparison.Ordinal) || line.StartsWith("NAME=", StringComparison.Ordinal))) names.Add(section.Name + "." + line[..line.IndexOf('=')]);
                else if (section.Name is "PERSONALITY_CORE" or "PERSONALITY_PUBLIC" or "PERSONALITY_PRIVATE" or "PERSONALITY_CONTRADICTION")
                {
                    if (!line.StartsWith("DATA_CN=", StringComparison.Ordinal)) names.Add(section.Name + "." + (line.Contains('=') ? line[..line.IndexOf('=')] : line));
                }
            }
        }
        return names.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string Render(IEnumerable<TemplateSection> sections)
    {
        return string.Join("\n\n", sections
            .Where(section => section.Lines.Count > 0)
            .Select(section => "[" + section.Name + "]\n" + string.Join("\n", section.Lines)));
    }

    private static void AddFacetStrengths(List<string> lines, PersonaDocument document, PersonaTagRegistry registry, PersonaTagCategory category)
    {
        HashSet<string> replacedIds = GetAxisReplacedTagIds(document, category);
        foreach ((string tagId, int strength) in document.FacetStrengths.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (replacedIds.Contains(tagId)) continue;
            if (!registry.TryGet(tagId, out PersonaTagDefinition definition) || definition.Category != category) continue;
            lines.Add("FACET_" + ToCanonicalTagId(definition.Id) + "_STRENGTH_" + strength.ToString());
        }
    }

    private static void AddRegisteredTags(List<string> lines, PersonaDocument document, PersonaTagRegistry registry, PersonaTagCategory category)
    {
        HashSet<string> replacedIds = GetAxisReplacedTagIds(document, category);
        foreach (string tagId in document.Tags.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!registry.TryGet(tagId, out PersonaTagDefinition definition) || definition.Category != category) continue;
            if (replacedIds.Contains(definition.Id) || document.FacetStrengths.ContainsKey(definition.Id)) continue;
            lines.Add(ToCanonicalTagId(definition.Id));
        }
    }

    private static void AddRegisteredAppearanceTags(List<string> lines, PersonaDocument document, PersonaTagRegistry registry)
    {
        foreach (string tagId in document.Tags.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!tagId.StartsWith("appearance.", StringComparison.Ordinal)) continue;
            if (registry.TryGet(tagId, out _)) lines.Add(ToCanonicalTagId(tagId));
        }
    }

    private static HashSet<string> GetAxisReplacedTagIds(PersonaDocument document, PersonaTagCategory category)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        if (category == PersonaTagCategory.Trait)
        {
            AddIfConfigured(ids, document.TraitProfile?.Caution, "trait.cautious");
            AddIfConfigured(ids, document.TraitProfile?.Ambition, "trait.ambitious");
            AddIfConfigured(ids, document.TraitProfile?.Pride, "trait.proud");
            AddIfConfigured(ids, document.TraitProfile?.Pragmatism, "trait.pragmatic");
            AddIfConfigured(ids, document.TraitProfile?.InGroupLoyalty, "trait.guardian");
            AddIfConfigured(ids, document.TraitProfile?.Tradition, "trait.traditional");
        }
        else if (category == PersonaTagCategory.Expression)
        {
            AddIfConfigured(ids, document.ExpressionProfile?.Restraint, "expression.measured");
            AddIfConfigured(ids, document.ExpressionProfile?.Directness, "expression.direct");
            AddIfConfigured(ids, document.ExpressionProfile?.Formality, "expression.formal");
            AddIfConfigured(ids, document.ExpressionProfile?.Playfulness, "expression.teasing");
            AddIfConfigured(ids, document.ExpressionProfile?.Warmth, "expression.warm");
        }
        else if (category == PersonaTagCategory.Behavior)
        {
            AddIfConfigured(ids, document.BehaviorProfile?.Conditionality, "behavior.bargains");
            AddIfConfigured(ids, document.BehaviorProfile?.Deliberation, "behavior.observes_before_acting");
            AddIfConfigured(ids, document.BehaviorProfile?.TrustTesting, "behavior.tests_loyalty");
            AddIfConfigured(ids, document.BehaviorProfile?.Leverage, "behavior.keeps_leverage");
            AddIfConfigured(ids, document.BehaviorProfile?.InGroupPriority, "behavior.protects_inner_circle");
            AddIfConfigured(ids, document.BehaviorProfile?.Leadership, "behavior.takes_command");
        }
        return ids;
    }

    private static void AddIfConfigured(HashSet<string> ids, int? value, string id)
    {
        if (value.HasValue) ids.Add(id);
    }

    private static void AddAxisId(List<string> lines, string prefix, int? value, string negative, string positive)
    {
        if (!value.HasValue) return;
        string suffix = value.Value switch
        {
            -2 => negative + "_STRONG",
            -1 => negative + "_SLIGHT",
            0 => "BALANCED",
            1 => positive + "_SLIGHT",
            2 => positive + "_STRONG",
            _ => throw new InvalidOperationException("persona.axis_value_invalid")
        };
        lines.Add(prefix + "_" + suffix);
    }

    private static void AddSection(List<TemplateSection> sections, string name, IEnumerable<string> values)
    {
        List<string> lines = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        if (lines.Count == 0) return;
        sections.Add(new TemplateSection(name, lines));
    }

    private static void AddValue(List<string> lines, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) lines.Add(name + "=\"" + Escape(value) + "\"");
    }

    private static void AddData(List<string> lines, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) lines.Add("DATA_CN=\"" + Escape(value) + "\"");
    }

    private static void AddDataList(List<string> lines, IEnumerable<string>? values, string fieldName, List<string> actions)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int duplicateCount = 0;
        foreach (string value in values ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (seen.Add(NormalizeForComparison(value))) AddData(lines, value);
            else duplicateCount++;
        }
        if (duplicateCount > 0) actions.Add("deduplicated:" + fieldName + ":" + duplicateCount.ToString());
    }

    private static string ToCanonicalTagId(string id)
    {
        return id.Replace('.', '_').Replace('-', '_').ToUpperInvariant();
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private sealed class TemplateSection
    {
        public TemplateSection(string name, IEnumerable<string> lines)
        {
            Name = name;
            Lines = lines.ToList();
        }

        public string Name { get; }
        public List<string> Lines { get; }

        public TemplateSection Clone()
        {
            return new TemplateSection(Name, Lines);
        }
    }
}
