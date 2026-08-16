using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class AwakeMessengerChatLine
{
    internal string Speaker { get; }
    internal string Text { get; }
    internal int Day { get; }

    internal AwakeMessengerChatLine(string speaker, string text, int day)
    {
        Speaker = speaker ?? string.Empty;
        Text = text ?? string.Empty;
        Day = day;
    }
}

internal static class AwakeMessengerHistory
{
    private const int MaximumLinesPerContact = 200;
    private const long LoadRetryIntervalMilliseconds = 10000;
    private static readonly Dictionary<string, List<AwakeMessengerChatLine>> Chats =
        new Dictionary<string, List<AwakeMessengerChatLine>>(StringComparer.Ordinal);
    private static bool _loaded;
    private static long _lastLoadAttemptUtcTicks;

    internal static void Append(string targetId, string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return;
        lock (Chats)
        {
            List<AwakeMessengerChatLine> lines;
            if (!Chats.TryGetValue(targetId, out lines))
            {
                lines = new List<AwakeMessengerChatLine>();
                Chats[targetId] = lines;
            }
            lines.Add(new AwakeMessengerChatLine(speaker, text, AwakeRuntime.CurrentGameDay()));
            while (lines.Count > MaximumLinesPerContact) lines.RemoveAt(0);
        }
        try
        {
            WorldStateStore store = AwakeRuntime.WorldStateStore;
            if (store != null)
            {
                AwakeBackgroundTask.Run(
                    () => store.AppendMessengerMessageAsync(
                        targetId,
                        speaker,
                        text,
                        AwakeRuntime.CurrentGameDay(),
                        "msg|" + Guid.NewGuid().ToString("N"),
                        CancellationToken.None),
                    "messenger_history");
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("messenger_history_persist_error error=" + ex.Message);
        }
    }

    internal static async Task LoadAsync(CancellationToken cancellationToken)
    {
        lock (Chats)
        {
            if (_loaded) return;
            long now = DateTimeOffset.UtcNow.UtcTicks;
            if (now - _lastLoadAttemptUtcTicks < LoadRetryIntervalMilliseconds * TimeSpan.TicksPerMillisecond)
            {
                return;
            }
            _lastLoadAttemptUtcTicks = now;
        }
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null)
        {
            return;
        }
        JObject doc = null;
        try
        {
            doc = await store.GetMessengerAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("messenger_history_load_error error=" + ex.Message);
            return;
        }
        lock (Chats)
        {
            Chats.Clear();
            if (doc?["chats"] is JObject chats)
            {
                foreach (JProperty property in chats.Properties())
                {
                    List<AwakeMessengerChatLine> lines = new List<AwakeMessengerChatLine>();
                    if (property.Value is JArray array)
                    {
                        foreach (JToken token in array)
                        {
                            if (token is not JObject line) continue;
                            lines.Add(new AwakeMessengerChatLine(
                                (string)line["speaker"] ?? string.Empty,
                                (string)line["text"] ?? string.Empty,
                                IntValue(line["day"])));
                        }
                    }
                    if (lines.Count > 0) Chats[property.Name] = lines;
                }
            }
            _loaded = true;
        }
    }

    internal static IReadOnlyList<AwakeMessengerChatLine> GetHistory(string targetId)
    {
        lock (Chats)
        {
            List<AwakeMessengerChatLine> lines;
            if (!string.IsNullOrWhiteSpace(targetId) && Chats.TryGetValue(targetId, out lines))
            {
                return new List<AwakeMessengerChatLine>(lines);
            }
            return new List<AwakeMessengerChatLine>();
        }
    }

    internal static void ClearForTesting()
    {
        ResetForCampaign();
    }

    internal static void ResetForCampaign()
    {
        lock (Chats)
        {
            Chats.Clear();
            _loaded = false;
            _lastLoadAttemptUtcTicks = 0;
        }
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }
}
