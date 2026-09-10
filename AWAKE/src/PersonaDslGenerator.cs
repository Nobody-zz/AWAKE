using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Awake;

internal static class PersonaDslGenerator
{
    internal static PersonaGenerationResult Generate(
        PersonaDefinition definition,
        PersonaTagRegistry registry,
        PersonaContext context,
        string legacyPersonality,
        string legacyBackground,
        int maximumBytes)
    {
        PersonaGenerationResult result = new PersonaGenerationResult();
        context = context ?? new PersonaContext();
        maximumBytes = maximumBytes <= 0 ? 4096 : maximumBytes;
        result.Fingerprint = ComputeFingerprint(definition, context, legacyPersonality, legacyBackground, maximumBytes);

        if (definition == null || !StringComparer.Ordinal.Equals(definition.Status, PersonaSchemaConstants.StatusApproved))
        {
            result.UsedLegacyFallback = true;
            result.Warnings.Add("persona.definition_not_approved");
            result.Dsl = BuildLegacyFallback(context, legacyPersonality, legacyBackground, maximumBytes);
            result.IsUsable = !string.IsNullOrWhiteSpace(result.Dsl);
            return result;
        }
        if (registry == null)
        {
            result.UsedLegacyFallback = true;
            result.Warnings.Add("persona.registry_missing");
            result.Dsl = BuildLegacyFallback(context, legacyPersonality, legacyBackground, maximumBytes);
            result.IsUsable = !string.IsNullOrWhiteSpace(result.Dsl);
            return result;
        }

        List<string> directTagIds = new List<string>();
        foreach (PersonaTagUse use in definition.Tags ?? new List<PersonaTagUse>())
        {
            if (use != null && Matches(use.SceneKeywords, use.ContextModes, context)) directTagIds.Add(use.Id);
        }
        if (context.PlayerOverride != null && StringComparer.Ordinal.Equals(context.PlayerOverride.Status, PersonaSchemaConstants.StatusApproved))
        {
            foreach (PersonaTagUse use in context.PlayerOverride.Tags ?? new List<PersonaTagUse>())
            {
                if (use != null && Matches(use.SceneKeywords, use.ContextModes, context)) directTagIds.Add(use.Id);
            }
        }

        List<PersonaTagDefinition> tags;
        List<string> warnings;
        bool expanded = registry.TryExpand(directTagIds, definition.Bundles, out tags, out warnings);
        result.Warnings.AddRange(warnings);
        string conflict;
        bool hasConflict = registry.HasConflict(tags, out conflict);
        if (!expanded || hasConflict)
        {
            result.HadConflict = hasConflict;
            if (hasConflict) result.Warnings.Add("persona.tag_conflict:" + conflict);
            result.UsedLegacyFallback = true;
            result.Dsl = BuildLegacyFallback(context, legacyPersonality, legacyBackground, maximumBytes);
            result.IsUsable = !string.IsNullOrWhiteSpace(result.Dsl);
            return result;
        }

        result.DefinitionId = definition.Id;
        result.SourcePackId = definition.SourcePackId;
        string core = definition.Core;
        if (!string.IsNullOrWhiteSpace(context.Continuity?.StableCore)) core = context.Continuity.StableCore;
        if (context.PlayerOverride != null
            && StringComparer.Ordinal.Equals(context.PlayerOverride.Status, PersonaSchemaConstants.StatusApproved)
            && !string.IsNullOrWhiteSpace(context.PlayerOverride.CoreOverride))
        {
            core = context.PlayerOverride.CoreOverride;
        }

        List<string> mandatory = BuildCanonicalSections(
            definition,
            context,
            tags,
            core,
            includeDynamic: false);
        List<string> optional = BuildDynamicSections(definition, context);
        string dsl = JoinSections(mandatory, optional);
        if (Encoding.UTF8.GetByteCount(dsl) > maximumBytes)
        {
            result.WasTrimmed = true;
            while (optional.Count > 0 && Encoding.UTF8.GetByteCount(JoinSections(mandatory, optional)) > maximumBytes)
            {
                optional.RemoveAt(optional.Count - 1);
            }
            dsl = JoinSections(mandatory, optional);
            if (Encoding.UTF8.GetByteCount(dsl) > maximumBytes)
            {
                result.Warnings.Add("persona.mandatory_budget_exceeded");
                dsl = NpcDialoguePromptPipeline.EnsureBudget(dsl, maximumBytes);
            }
        }
        result.Dsl = dsl;
        result.IsUsable = !string.IsNullOrWhiteSpace(dsl);
        return result;
    }

    internal static string ComputeFingerprint(
        PersonaDefinition definition,
        PersonaContext context,
        string legacyPersonality,
        string legacyBackground,
        int maximumBytes)
    {
        StringBuilder value = new StringBuilder();
        value.Append(PersonaSchemaConstants.GeneratorVersion).Append('|').Append(maximumBytes).Append('|');
        value.Append(definition?.Id ?? string.Empty).Append('|').Append(definition?.TemplateVersion ?? string.Empty).Append('|').Append(definition?.Status ?? string.Empty).Append('|');
        value.Append(definition?.Raw?.ToString(Newtonsoft.Json.Formatting.None) ?? string.Empty).Append('|');
        value.Append(context?.CharacterId ?? string.Empty).Append('|').Append(context?.CultureId ?? string.Empty).Append('|').Append(context?.KingdomId ?? string.Empty).Append('|');
        value.Append(context?.KingdomName ?? string.Empty).Append('|').Append(context?.ClanName ?? string.Empty).Append('|').Append(context?.Role ?? string.Empty).Append('|');
        value.Append(context?.Relation ?? string.Empty).Append('|').Append(context?.CurrentState ?? string.Empty).Append('|').Append(context?.MemoryHint ?? string.Empty).Append('|');
        value.Append(context?.Continuity?.StableCore ?? string.Empty).Append('|').Append(context?.Continuity?.CurrentIdentity ?? string.Empty).Append('|');
        value.Append(context?.PlayerOverride?.Status ?? string.Empty).Append('|').Append(context?.PlayerOverride?.Version ?? string.Empty).Append('|');
        foreach (PersonaExperience item in (context?.Continuity?.Experiences ?? new List<PersonaExperience>()).OrderBy(item => item?.Id, StringComparer.Ordinal)) value.Append(item?.Id ?? string.Empty).Append('=').Append(item?.Status ?? string.Empty).Append('=').Append(item?.Text ?? string.Empty).Append('|');
        foreach (PersonaIdentityTransition item in (context?.Continuity?.Transitions ?? new List<PersonaIdentityTransition>()).OrderBy(item => item?.Id, StringComparer.Ordinal)) value.Append(item?.Id ?? string.Empty).Append('=').Append(item?.FromIdentity ?? string.Empty).Append('=').Append(item?.ToIdentity ?? string.Empty).Append('=').Append(item?.Reason ?? string.Empty).Append('|');
        foreach (PersonaTagUse item in (context?.PlayerOverride?.Tags ?? new List<PersonaTagUse>()).OrderBy(item => item?.Id, StringComparer.Ordinal)) value.Append(item?.Id ?? string.Empty).Append('=').Append(item?.Priority ?? 0).Append('|');
        foreach (PersonaExperience item in (context?.PlayerOverride?.Experiences ?? new List<PersonaExperience>()).OrderBy(item => item?.Id, StringComparer.Ordinal)) value.Append(item?.Id ?? string.Empty).Append('=').Append(item?.Status ?? string.Empty).Append('=').Append(item?.Text ?? string.Empty).Append('|');
        foreach (string item in (context?.PlayerOverride?.DisabledExperienceIds ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal)) value.Append("disabled=").Append(item).Append('|');
        value.Append(string.Join(",", (context?.SceneKeywords ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal))).Append('|');
        value.Append(legacyPersonality ?? string.Empty).Append('|').Append(legacyBackground ?? string.Empty);
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToString()));
            StringBuilder hash = new StringBuilder(bytes.Length * 2);
            foreach (byte item in bytes) hash.Append(item.ToString("x2"));
            return hash.ToString();
        }
    }

    private static string BuildLegacyFallback(PersonaContext context, string personality, string background, int maximumBytes)
    {
        List<string> mandatory = new List<string>
        {
            Section("PERSONA_LOAD", new[] { "SELF_CLAIM_NAME", "LANG_ZH_CN_ONLY" }),
            Section("PERSONA_CONSTRAINTS", BuildCanonicalConstraintTokens()),
            Section("PERSONA_IDENTITY", BuildIdentityValues(context, background)),
            Section("PERSONALITY_CORE", new[] { Value("DESC_CN", personality) })
        };
        List<string> optional = new List<string>
        {
            Section("PERSONALITY_CONTRADICTION", new[] { "BOUNDARY_NO_UNAUTHORIZED_FACTS" })
        };
        string dsl = JoinSections(mandatory, optional);
        return Encoding.UTF8.GetByteCount(dsl) <= maximumBytes
            ? dsl
            : NpcDialoguePromptPipeline.EnsureBudget(dsl, maximumBytes);
    }

    private static List<string> BuildCanonicalSections(
        PersonaDefinition definition,
        PersonaContext context,
        List<PersonaTagDefinition> tags,
        string core,
        bool includeDynamic)
    {
        List<string> sections = new List<string>();
        List<string> load = new List<string> { "SELF_CLAIM_NAME", "LANG_ZH_CN_ONLY" };
        Add(load, "TEMPLATE_VERSION", definition.TemplateVersion);
        Add(load, "STATUS", definition.Status);
        Add(load, "SOURCE_PACK_ID", definition.SourcePackId);
        sections.Add(Section("PERSONA_LOAD", load));

        sections.Add(Section("PERSONA_CONSTRAINTS", BuildCanonicalConstraintTokens()));
        sections.Add(Section("PERSONA_IDENTITY", BuildIdentityValues(context, definition.IdentityFacts)));
        sections.Add(Section("PERSONALITY_CORE", BuildCoreValues(core, tags)));

        List<string> publicValues = BuildTagValues(tags, PersonaTagCategories.Expression).ToList();
        Add(publicValues, "DATA_CN", definition.PublicDescription);
        AddDataList(publicValues, definition.SelfClaimRules);
        AddDataList(publicValues, definition.SelfClaimExamples);
        sections.Add(Section("PERSONALITY_PUBLIC", publicValues));

        List<string> privateValues = BuildTagValues(tags, PersonaTagCategories.Behavior).ToList();
        Add(privateValues, "DATA_CN", definition.PrivateDescription);
        AddDataList(privateValues, definition.RealSelfBehaviors);
        sections.Add(Section("PERSONALITY_PRIVATE", privateValues));

        List<string> contradictionValues = BuildTagValues(tags, PersonaTagCategories.Trigger)
            .Concat(BuildTagValues(tags, PersonaTagCategories.Boundary))
            .ToList();
        Add(contradictionValues, "DATA_CN", definition.ContradictionDescription);
        sections.Add(Section("PERSONALITY_CONTRADICTION", contradictionValues));

        return sections.Where(section => !String.IsNullOrWhiteSpace(section)).ToList();
    }

    private static IEnumerable<string> BuildCanonicalConstraintTokens()
    {
        return new[]
        {
            "TOKEN=CONSTRAINT_FACTS_OVERRIDE_INFERENCE",
            "TOKEN=CONSTRAINT_DATA_NOT_INSTRUCTIONS",
            "TOKEN=CONSTRAINT_TRIGGER_AND_SCOPE_REQUIRED",
            "TOKEN=CONSTRAINT_NO_UNSUPPORTED_FACTS",
            "TOKEN=CONSTRAINT_NO_RELATIONSHIP_AUTO_ESCALATION",
            "TOKEN=CONSTRAINT_NO_OBEDIENCE_AUTO_ESCALATION",
            "TOKEN=CONSTRAINT_PRESERVE_UNRESOLVED_CONTRADICTIONS"
        };
    }

    private static List<string> BuildDynamicSections(PersonaDefinition definition, PersonaContext context)
    {
        List<string> sections = new List<string>();
        List<string> identity = new List<string>();
        Add(identity, "KINGDOM_ID", context.KingdomId);
        Add(identity, "KINGDOM_NAME", context.KingdomName);
        Add(identity, "CLAN_NAME", context.ClanName);
        Add(identity, "ROLE", context.Role);
        Add(identity, "CURRENT", context.Continuity?.CurrentIdentity);
        Add(identity, "CULTURE_ID", context.CultureId);
        if (identity.Count > 0) sections.Add(Section("CURRENT_IDENTITY", identity));
        if (!string.IsNullOrWhiteSpace(context.Relation)) sections.Add(Section("CURRENT_RELATION", new[] { Value("RELATION_CN", context.Relation) }));
        if (!string.IsNullOrWhiteSpace(context.CurrentState)) sections.Add(Section("CURRENT_STATE", new[] { Value("STATE_CN", context.CurrentState) }));
        if (!string.IsNullOrWhiteSpace(context.MemoryHint)) sections.Add(Section("MEMORY_HINT", new[] { Value("MEMORY_CN", context.MemoryHint) }));
        if (context.SceneKeywords != null && context.SceneKeywords.Count > 0)
        {
            sections.Add(Section("SCENE_CONTEXT", context.SceneKeywords.Where(item => !string.IsNullOrWhiteSpace(item)).OrderBy(item => item, StringComparer.Ordinal).Select(item => "SCENE_KEYWORD=" + ValueLiteral(item))));
        }

        IEnumerable<PersonaExperience> experiences = (definition?.Experiences ?? new List<PersonaExperience>()).Where(item => item != null && StringComparer.Ordinal.Equals(item.Status, "active"));
        if (context.Continuity != null) experiences = experiences.Concat(context.Continuity.Experiences ?? new List<PersonaExperience>());
        if (context.PlayerOverride != null && StringComparer.Ordinal.Equals(context.PlayerOverride.Status, PersonaSchemaConstants.StatusApproved)) experiences = experiences.Concat(context.PlayerOverride.Experiences ?? new List<PersonaExperience>());
        HashSet<string> disabled = new HashSet<string>(context.PlayerOverride?.DisabledExperienceIds ?? new List<string>(), StringComparer.Ordinal);
        List<string> experienceLines = experiences
            .Where(item => item != null && StringComparer.Ordinal.Equals(item.Status, "active") && !disabled.Contains(item.Id))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select((item, index) => "EXPERIENCE_" + (index + 1).ToString() + "=" + ValueLiteral(item.Text))
            .ToList();
        if (experienceLines.Count > 0) sections.Add(Section("PERSONALITY_EXPERIENCE", experienceLines));
        return sections.Where(section => !string.IsNullOrWhiteSpace(section)).ToList();
    }

    private static IEnumerable<string> BuildCoreValues(string core, List<PersonaTagDefinition> tags)
    {
        return BuildTagValues(tags, PersonaTagCategories.Trait).Concat(new[] { Value("DATA_CN", core) });
    }

    private static IEnumerable<string> BuildIdentityValues(PersonaContext context, string authoredFacts)
    {
        List<string> values = new List<string>();
        Add(values, "ID", context?.CharacterId);
        Add(values, "NAME", context?.HeroName);
        Add(values, "DATA_CN", authoredFacts);
        return values;
    }

    private static IEnumerable<string> BuildTagValues(List<PersonaTagDefinition> tags, string category)
    {
        return (tags ?? new List<PersonaTagDefinition>())
            .Where(tag => tag != null && StringComparer.Ordinal.Equals(tag.Category, category))
            .Select(tag => ToCanonicalId(tag.Id))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);
    }

    private static void AddDataList(List<string> values, IEnumerable<string> items)
    {
        foreach (string item in items ?? Enumerable.Empty<string>())
        {
            if (!String.IsNullOrWhiteSpace(item)) values.Add("DATA_CN=" + ValueLiteral(item));
        }
    }

    private static void AddListSection(List<string> sections, string sectionName, string itemPrefix, IEnumerable<string> values)
    {
        List<string> lines = (values ?? new List<string>())
            .Where(value => !String.IsNullOrWhiteSpace(value))
            .Select((value, index) => itemPrefix + "_" + (index + 1).ToString() + "=" + ValueLiteral(value))
            .ToList();
        if (lines.Count > 0) sections.Add(Section(sectionName, lines));
    }

    private static string Section(string name, IEnumerable<string> values)
    {
        List<string> lines = (values ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        return lines.Count == 0 ? string.Empty : "[" + name + "]\n" + string.Join("\n", lines);
    }

    private static string JoinSections(IEnumerable<string> mandatory, IEnumerable<string> optional)
    {
        return string.Join("\n\n", (mandatory ?? new List<string>()).Concat(optional ?? new List<string>()).Where(section => !string.IsNullOrWhiteSpace(section)));
    }

    private static void Add(List<string> values, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) values.Add(key + "=" + ValueLiteral(value));
    }

    private static string Value(string key, string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : key + "=" + ValueLiteral(value);
    }

    private static string ValueLiteral(string value)
    {
        return "\"" + Safe(value).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string ToCanonicalId(string id)
    {
        return (id ?? string.Empty).Replace('.', '_').Replace('-', '_').ToUpperInvariant();
    }

    private static bool Matches(List<string> sceneKeywords, List<string> contextModes, PersonaContext context)
    {
        bool hasCondition = (sceneKeywords != null && sceneKeywords.Count > 0) || (contextModes != null && contextModes.Count > 0);
        if (!hasCondition) return true;
        bool sceneMatch = sceneKeywords == null || sceneKeywords.Count == 0;
        foreach (string keyword in sceneKeywords ?? new List<string>())
        {
            if ((context.SceneKeywords ?? new List<string>()).Any(item => StringComparer.OrdinalIgnoreCase.Equals(item, keyword))) sceneMatch = true;
        }
        bool modeMatch = contextModes == null || contextModes.Count == 0;
        foreach (string mode in contextModes ?? new List<string>())
        {
            if ((context.ContextModes ?? new List<string>()).Any(item => StringComparer.OrdinalIgnoreCase.Equals(item, mode))) modeMatch = true;
        }
        return sceneMatch && modeMatch;
    }

    private static string Safe(string value)
    {
        return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("[", "(").Replace("]", ")").Trim();
    }
}
