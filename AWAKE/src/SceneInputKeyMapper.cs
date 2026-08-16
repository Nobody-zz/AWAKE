using System;
using TaleWorlds.InputSystem;

namespace Awake;

internal static class SceneInputKeyMapper
{
    internal static bool TryParse(string raw, out InputKey key)
    {
        key = InputKey.Invalid;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }
        string normalized = raw.Trim();
        if (StringComparer.OrdinalIgnoreCase.Equals(normalized, "["))
        {
            key = InputKey.OpenBraces;
            return true;
        }
        if (StringComparer.OrdinalIgnoreCase.Equals(normalized, "]"))
        {
            key = InputKey.CloseBraces;
            return true;
        }
        if (Enum.TryParse<InputKey>(normalized.ToUpperInvariant(), out InputKey parsed)
            && parsed != InputKey.Invalid)
        {
            key = parsed;
            return true;
        }
        return false;
    }

    internal static InputKey ParseOrDefault(string raw, InputKey fallback)
    {
        InputKey parsed;
        return TryParse(raw, out parsed) ? parsed : fallback;
    }
}
