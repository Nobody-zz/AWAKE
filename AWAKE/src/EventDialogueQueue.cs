using System;
using System.Collections.Generic;

namespace Awake;

internal sealed class PendingDialogue
{
    internal string HeroId { get; }
    internal string OpeningHint { get; }

    internal PendingDialogue(string heroId, string openingHint)
    {
        HeroId = heroId ?? string.Empty;
        OpeningHint = openingHint ?? string.Empty;
    }
}

internal static class EventDialogueQueue
{
    private const int MaximumPending = 32;
    private static readonly object Gate = new object();
    private static readonly Queue<PendingDialogue> Items = new Queue<PendingDialogue>();

    internal static int Count
    {
        get { lock (Gate) return Items.Count; }
    }

    internal static void Enqueue(string heroId, string openingHint)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return;
        lock (Gate)
        {
            if (Items.Count >= MaximumPending) return;
            Items.Enqueue(new PendingDialogue(heroId, openingHint));
        }
    }

    internal static bool TryDequeue(out PendingDialogue item)
    {
        lock (Gate)
        {
            if (Items.Count > 0)
            {
                item = Items.Dequeue();
                return true;
            }
        }
        item = null;
        return false;
    }

    internal static void ClearForTesting()
    {
        lock (Gate) Items.Clear();
    }
}
