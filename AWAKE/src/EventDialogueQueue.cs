using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class AwakeDialogueQueueEntry
{
    internal string Id { get; set; } = string.Empty;
    internal string Source { get; set; } = "event";
    internal string TargetId { get; set; } = string.Empty;
    internal string CanonicalContactKey { get; set; } = string.Empty;
    internal string OpeningHint { get; set; } = string.Empty;
    internal string Motive { get; set; } = string.Empty;
    internal int Day { get; set; }
    internal int ExpiryDay { get; set; }
    internal string State { get; set; } = "pending";
}

internal sealed class PendingDialogue
{
    internal string Id { get; }
    internal string HeroId { get; }
    internal string OpeningHint { get; }

    internal PendingDialogue(string heroId, string openingHint, string id = null)
    {
        HeroId = heroId ?? string.Empty;
        OpeningHint = openingHint ?? string.Empty;
        Id = id ?? string.Empty;
    }
}

internal static class EventDialogueQueue
{
    private const int MaximumPending = 32;
    private static readonly object Gate = new object();
    private static readonly Queue<PendingDialogue> Items = new Queue<PendingDialogue>();
    private static bool _loaded;

    internal static int Count
    {
        get { lock (Gate) return Items.Count; }
    }

    internal static void Enqueue(string heroId, string openingHint)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return;
        string id = "dq|" + Guid.NewGuid().ToString("N");
        lock (Gate)
        {
            if (Items.Count >= MaximumPending) return;
            Items.Enqueue(new PendingDialogue(heroId, openingHint, id));
        }
        PersistEnqueue(id, heroId, openingHint);
    }

    internal static bool TryDequeue(out PendingDialogue item)
    {
        lock (Gate)
        {
            if (Items.Count > 0)
            {
                item = Items.Dequeue();
            }
            else
            {
                item = null;
                return false;
            }
        }
        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            PersistConsume(item.Id);
        }
        return true;
    }

    internal static async Task LoadFromStoreAsync(CancellationToken cancellationToken)
    {
        bool alreadyLoaded;
        lock (Gate) alreadyLoaded = _loaded;
        if (alreadyLoaded) return;
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return;
        try
        {
            JObject doc = await store.GetDialogueQueueAsync(null, cancellationToken).ConfigureAwait(false);
            if (doc == null)
            {
                lock (Gate) _loaded = true;
                return;
            }
            lock (Gate)
            {
                Items.Clear();
                if (doc["entries"] is JArray entries)
                {
                    int day = AwakeRuntime.CurrentGameDay();
                    foreach (JToken token in entries)
                    {
                        if (token is not JObject entry) continue;
                        if (!StringComparer.Ordinal.Equals((string)entry["state"], "pending")) continue;
                        int expiry = IntValue(entry["expiryDay"]);
                        if (expiry > 0 && day > expiry) continue;
                        string targetId = (string)entry["targetId"] ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(targetId)) continue;
                        if (Items.Count >= MaximumPending) break;
                        Items.Enqueue(new PendingDialogue(
                            targetId,
                            (string)entry["openingHint"] ?? string.Empty,
                            (string)entry["id"] ?? string.Empty));
                    }
                }
                _loaded = true;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("dialogue_queue_load_error error=" + ex.Message);
        }
    }

    internal static void ClearForTesting()
    {
        lock (Gate)
        {
            Items.Clear();
            _loaded = false;
        }
    }

    internal static void ResetForCampaign()
    {
        ClearForTesting();
    }

    private static void PersistEnqueue(string id, string heroId, string openingHint)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return;
        AwakeBackgroundTask.Run(
            () => store.EnqueueDialogueAsync(
                id,
                "event",
                heroId,
                heroId,
                openingHint ?? string.Empty,
                string.Empty,
                AwakeRuntime.CurrentGameDay(),
                0,
                "enqueue|" + id,
                CancellationToken.None),
            "dialogue_queue_enqueue");
    }

    private static void PersistConsume(string id)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return;
        AwakeBackgroundTask.Run(
            () => store.ConsumeDialogueAsync(id, "consume|" + id, CancellationToken.None),
            "dialogue_queue_consume");
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try
        {
            return (int)token;
        }
        catch
        {
            return 0;
        }
    }
}
