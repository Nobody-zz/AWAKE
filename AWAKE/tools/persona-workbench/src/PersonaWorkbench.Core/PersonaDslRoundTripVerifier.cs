using System.Text;

namespace PersonaWorkbench.Core;

internal static class PersonaDslRoundTripVerifier
{
    private static readonly string[] SectionOrder =
    {
        "PERSONA_LOAD",
        "PERSONA_CONSTRAINTS",
        "PERSONA_IDENTITY",
        "PERSONA_APPEARANCE",
        "PERSONALITY_CORE",
        "PERSONALITY_PUBLIC",
        "PERSONALITY_PRIVATE",
        "PERSONALITY_CONTRADICTION"
    };

    private static readonly HashSet<string> AllowedSections = new HashSet<string>(SectionOrder, StringComparer.Ordinal);

    private static readonly Dictionary<string, HashSet<string>> AssignmentKeys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
    {
        ["PERSONA_LOAD"] = new HashSet<string>(new[] { "TEMPLATE_VERSION", "STATUS", "SOURCE_PACK_ID" }, StringComparer.Ordinal),
        ["PERSONA_IDENTITY"] = new HashSet<string>(new[] { "ID", "NAME", "DATA_CN" }, StringComparer.Ordinal),
        ["PERSONALITY_CORE"] = new HashSet<string>(new[] { "DATA_CN" }, StringComparer.Ordinal),
        ["PERSONALITY_PUBLIC"] = new HashSet<string>(new[] { "DATA_CN" }, StringComparer.Ordinal),
        ["PERSONALITY_PRIVATE"] = new HashSet<string>(new[] { "DATA_CN", "SENSITIVE_CONDITIONS", "CONDITIONAL_RESPONSES" }, StringComparer.Ordinal),
        ["PERSONALITY_CONTRADICTION"] = new HashSet<string>(new[]
        {
            "DATA_CN", "PRIORITY_ORDER", "PROTECTED_VALUES", "APPLICABLE_SCOPE", "EXCEPTION_COST", "BREACH_RESPONSE"
        }, StringComparer.Ordinal)
    };

    public static bool TryVerify(string dsl, IReadOnlyCollection<string> protectedEntries, out string errorCode)
    {
        errorCode = string.Empty;
        if (!TryParse(dsl, out CanonicalDslSnapshot? snapshot, out errorCode)) return false;

        HashSet<string> protectedSemanticEntries = new HashSet<string>(StringComparer.Ordinal);
        foreach (string protectedEntry in protectedEntries)
        {
            if (!TryParseEntry(protectedEntry, out CanonicalDslEntry? entry, out errorCode)) return false;
            protectedSemanticEntries.Add(entry!.SemanticKey);
        }

        foreach (string protectedSemanticEntry in protectedSemanticEntries)
        {
            if (!snapshot!.SemanticEntries.Contains(protectedSemanticEntry))
            {
                errorCode = "persona.template_protected_field_lost";
                return false;
            }
        }

        if (!string.Equals(NormalizeNewlines(dsl), snapshot!.Render(), StringComparison.Ordinal))
        {
            errorCode = "persona.template_roundtrip_changed";
            return false;
        }

        return true;
    }

    private static bool TryParse(string dsl, out CanonicalDslSnapshot? snapshot, out string errorCode)
    {
        snapshot = null;
        errorCode = string.Empty;
        List<CanonicalDslSection> sections = new List<CanonicalDslSection>();
        HashSet<string> seenSections = new HashSet<string>(StringComparer.Ordinal);
        string currentSection = string.Empty;
        int previousSectionIndex = -1;

        foreach (string rawLine in NormalizeNewlines(dsl).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = line[1..^1];
                if (!AllowedSections.Contains(currentSection))
                {
                    errorCode = "persona.template_section_unknown";
                    return false;
                }
                if (!seenSections.Add(currentSection))
                {
                    errorCode = "persona.template_section_duplicate";
                    return false;
                }

                int sectionIndex = Array.IndexOf(SectionOrder, currentSection);
                if (sectionIndex <= previousSectionIndex)
                {
                    errorCode = "persona.template_section_order_invalid";
                    return false;
                }
                previousSectionIndex = sectionIndex;
                sections.Add(new CanonicalDslSection(currentSection));
                continue;
            }

            if (currentSection.Length == 0)
            {
                errorCode = "persona.template_section_missing";
                return false;
            }

            CanonicalDslSection section = sections[^1];
            if (!TryParseEntry(currentSection, line, out CanonicalDslEntry? entry, out errorCode)) return false;
            if (!section.Add(entry!))
            {
                errorCode = entry!.Key == "DATA_CN"
                    ? "persona.template_entry_duplicate"
                    : "persona.template_field_duplicate";
                return false;
            }
        }

        if (sections.Count == 0)
        {
            errorCode = "persona.template_section_missing";
            return false;
        }

        snapshot = new CanonicalDslSnapshot(sections);
        return true;
    }

    private static bool TryParseEntry(string line, out CanonicalDslEntry? entry, out string errorCode)
    {
        int separator = line.IndexOf('\n');
        if (separator <= 0 || separator == line.Length - 1)
        {
            entry = null;
            errorCode = "persona.template_section_missing";
            return false;
        }

        string section = line[..separator];
        string value = line[(separator + 1)..];
        return TryParseEntry(section, value, out entry, out errorCode);
    }

    private static bool TryParseEntry(string section, string line, out CanonicalDslEntry? entry, out string errorCode)
    {
        entry = null;
        errorCode = string.Empty;

        if (section == "PERSONA_CONSTRAINTS")
        {
            if (!line.StartsWith("TOKEN=", StringComparison.Ordinal) || !IsCanonicalIdentifier(line[6..]))
            {
                errorCode = "persona.template_token_invalid";
                return false;
            }
            entry = CanonicalDslEntry.Token(section, line);
            return true;
        }

        if (section == "PERSONA_APPEARANCE")
        {
            if (!IsCanonicalIdentifier(line))
            {
                errorCode = "persona.template_token_invalid";
                return false;
            }
            entry = CanonicalDslEntry.Token(section, line);
            return true;
        }

        if (section == "PERSONA_LOAD" && line is "SELF_CLAIM_NAME" or "LANG_ZH_CN_ONLY")
        {
            entry = CanonicalDslEntry.Token(section, line);
            return true;
        }

        if (line.Contains('='))
        {
            if (!TryReadAssignment(line, out string key, out string decodedValue))
            {
                errorCode = "persona.template_assignment_invalid";
                return false;
            }
            if (!AssignmentKeys.TryGetValue(section, out HashSet<string>? keys) || !keys.Contains(key))
            {
                errorCode = "persona.template_field_unknown";
                return false;
            }
            entry = CanonicalDslEntry.Assignment(section, key, decodedValue);
            return true;
        }

        if (!IsCanonicalToken(section, line))
        {
            errorCode = "persona.template_token_invalid";
            return false;
        }
        entry = CanonicalDslEntry.Token(section, line);
        return true;
    }

    private static bool IsCanonicalToken(string section, string token)
    {
        if (section == "PERSONA_LOAD") return false;
        if (!IsCanonicalIdentifier(token)) return false;
        if (token.StartsWith("FACET_", StringComparison.Ordinal))
        {
            if (section is not ("PERSONALITY_CORE" or "PERSONALITY_PUBLIC" or "PERSONALITY_PRIVATE")) return false;
            int strengthSeparator = token.LastIndexOf("_STRENGTH_", StringComparison.Ordinal);
            return strengthSeparator > "FACET_".Length
                && int.TryParse(token[(strengthSeparator + "_STRENGTH_".Length)..], out int strength)
                && strength is >= 1 and <= 4;
        }
        return section switch
        {
            "PERSONALITY_CORE" => token.StartsWith("TRAIT_", StringComparison.Ordinal),
            "PERSONALITY_PUBLIC" => token.StartsWith("EXPRESSION_", StringComparison.Ordinal),
            "PERSONALITY_PRIVATE" => token.StartsWith("BEHAVIOR_", StringComparison.Ordinal) || token.StartsWith("REACTION_", StringComparison.Ordinal),
            "PERSONALITY_CONTRADICTION" => token.StartsWith("COMMITMENT_", StringComparison.Ordinal)
                || token.StartsWith("TRIGGER_", StringComparison.Ordinal)
                || token.StartsWith("BOUNDARY_", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsCanonicalIdentifier(string value)
    {
        return value.Length > 0
            && value.All(character => character is >= 'A' and <= 'Z' || character is >= '0' and <= '9' || character == '_');
    }

    private static bool TryReadAssignment(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        int separator = line.IndexOf('=');
        if (separator <= 0 || separator == line.Length - 1) return false;
        key = line[..separator];
        string encoded = line[(separator + 1)..];
        if (!IsCanonicalIdentifier(key) || encoded.Length < 2 || encoded[0] != '"' || encoded[^1] != '"') return false;

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
            else if (character == '\\')
            {
                escaping = true;
            }
            else
            {
                if (character == '"' || char.IsControl(character)) return false;
                decoded.Append(character);
            }
        }
        if (escaping) return false;
        value = decoded.ToString();
        return true;
    }

    private static string NormalizeNewlines(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private sealed class CanonicalDslSnapshot
    {
        private readonly IReadOnlyList<CanonicalDslSection> _sections;

        public CanonicalDslSnapshot(IReadOnlyList<CanonicalDslSection> sections)
        {
            _sections = sections;
            SemanticEntries = sections.SelectMany(section => section.Entries.Select(entry => entry.SemanticKey)).ToHashSet(StringComparer.Ordinal);
        }

        public IReadOnlySet<string> SemanticEntries { get; }

        public string Render()
        {
            return string.Join("\n\n", _sections.Select(section => "[" + section.Name + "]\n" + string.Join("\n", section.Entries.Select(entry => entry.Render()))));
        }
    }

    private sealed class CanonicalDslSection
    {
        public CanonicalDslSection(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public List<CanonicalDslEntry> Entries { get; } = new List<CanonicalDslEntry>();

        public bool Add(CanonicalDslEntry entry)
        {
            if (entry.Key == "DATA_CN")
            {
                Entries.Add(entry);
                return true;
            }
            if (entry.IsToken && Entries.Any(existing => existing.IsToken && existing.RawToken == entry.RawToken)) return false;
            if (!entry.IsToken && Entries.Any(existing => !existing.IsToken && existing.Key == entry.Key)) return false;
            Entries.Add(entry);
            return true;
        }
    }

    private sealed class CanonicalDslEntry
    {
        private CanonicalDslEntry(string section, string key, string? value, string? rawToken)
        {
            Section = section;
            Key = key;
            Value = value;
            RawToken = rawToken;
        }

        public string Section { get; }
        public string Key { get; }
        public string? Value { get; }
        public string? RawToken { get; }
        public bool IsToken => RawToken != null;
        public string SemanticKey => IsToken
            ? Section + "\nTOKEN\n" + RawToken
            : Section + "\n" + Key + "\n" + Value;

        public static CanonicalDslEntry Token(string section, string token) => new CanonicalDslEntry(section, string.Empty, null, token);
        public static CanonicalDslEntry Assignment(string section, string key, string value) => new CanonicalDslEntry(section, key, value, null);

        public string Render()
        {
            return IsToken ? RawToken! : Key + "=\"" + Escape(Value!) + "\"";
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }
}
