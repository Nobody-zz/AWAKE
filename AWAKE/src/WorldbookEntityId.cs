using System;

namespace Awake;

internal static class WorldbookEntityId
{
    internal static string Canonical(string kind, string value)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(value)) return string.Empty;
        string normalizedKind = Normalize(kind);
        string normalizedValue = Normalize(value);
        string[] prefixes =
        {
            "awake:entity:" + normalizedKind + ":",
            "awake:" + normalizedKind + ":",
            "calradia:" + normalizedKind + ":",
            "entity." + normalizedKind + ".",
            normalizedKind + ":"
        };
        foreach (string prefix in prefixes)
        {
            if (normalizedValue.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalizedValue = normalizedValue.Substring(prefix.Length);
                break;
            }
        }
        if (normalizedValue.StartsWith("entity.", StringComparison.Ordinal)) return string.Empty;
        return "awake:" + normalizedKind + ":" + normalizedValue;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
}
