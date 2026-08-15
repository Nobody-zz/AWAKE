using System;

namespace Awake;

internal static class NpcDialogueContext
{
    private static readonly object Gate = new object();
    private static string _pendingHeroId = string.Empty;
    private static string _pendingText = string.Empty;

    internal static void Record(string heroId, string text)
    {
        lock (Gate)
        {
            _pendingHeroId = heroId ?? string.Empty;
            _pendingText = text ?? string.Empty;
        }
    }

    internal static bool TryTake(out string heroId, out string text)
    {
        lock (Gate)
        {
            heroId = _pendingHeroId;
            text = _pendingText;
            bool has = !string.IsNullOrWhiteSpace(heroId);
            _pendingHeroId = string.Empty;
            _pendingText = string.Empty;
            return has;
        }
    }

    internal static void ClearForTesting()
    {
        lock (Gate)
        {
            _pendingHeroId = string.Empty;
            _pendingText = string.Empty;
        }
    }
}
