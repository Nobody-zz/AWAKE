using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Awake;

internal delegate string LocalizationResolver(string id, string fallback, IReadOnlyDictionary<string, string> variables);

internal static class AwakeLocalization
{
    internal static LocalizationResolver Resolver = DefaultResolve;

    internal static string Resolve(string id, string fallback)
    {
        return Resolve(id, fallback, null);
    }

    internal static string Resolve(string id, string fallback, IReadOnlyDictionary<string, string> variables)
    {
        try
        {
            return Resolver(id, fallback, variables) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static string DefaultResolve(string id, string fallback, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(id)) return ApplyFallbackVariables(fallback, variables);
        try
        {
            TextObject text = GameTexts.FindText(id);
            if (text == null) return ApplyFallbackVariables(fallback, variables);
            if (variables != null)
            {
                foreach (KeyValuePair<string, string> pair in variables)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                    {
                        text.SetTextVariable(pair.Key, pair.Value ?? string.Empty);
                    }
                }
            }
            string value = text.ToString();
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("ERROR: Text with id", StringComparison.Ordinal))
            {
                return ApplyFallbackVariables(fallback, variables);
            }
            return value;
        }
        catch
        {
            return ApplyFallbackVariables(fallback, variables);
        }
    }

    private static string ApplyFallbackVariables(string fallback, IReadOnlyDictionary<string, string> variables)
    {
        string result = fallback ?? string.Empty;
        if (variables != null)
        {
            foreach (KeyValuePair<string, string> pair in variables)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    result = result.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
                }
            }
        }
        return result;
    }
}
