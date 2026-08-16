using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldEventRecord
{
    internal int Day { get; }
    internal string Kind { get; }
    internal string Text { get; }

    internal WorldEventRecord(int day, string kind, string text)
    {
        Day = day;
        Kind = kind ?? string.Empty;
        Text = text ?? string.Empty;
    }
}

internal static class WorldEventLedger
{
    private const int Capacity = 50;
    private const long LoadRetryIntervalMilliseconds = 10000;
    private static readonly Queue<WorldEventRecord> Records = new Queue<WorldEventRecord>();
    private static bool _loaded;
    private static long _lastLoadAttemptUtcTicks;

    internal static int Count
    {
        get { lock (Records) return Records.Count; }
    }

    internal static void Record(int day, string kind, string text)
    {
        string safeKind = (kind ?? "event");
        string safeText = (text ?? string.Empty);
        safeKind = AwakeRuntime.TruncateTextElements(safeKind, 40);
        safeText = AwakeRuntime.TruncateTextElements(safeText, 500);
        lock (Records)
        {
            Records.Enqueue(new WorldEventRecord(day, safeKind, safeText));
            while (Records.Count > Capacity) Records.Dequeue();
        }
        try
        {
            WorldStateStore store = AwakeRuntime.WorldStateStore;
            if (store != null)
            {
                AwakeBackgroundTask.Run(
                    () => store.AppendWorldEventAsync(
                        day,
                        safeKind,
                        safeText,
                        "event|" + Guid.NewGuid().ToString("N"),
                        CancellationToken.None),
                    "world_event");
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_event_persist_error error=" + ex.Message);
        }
    }

    internal static async Task LoadFromStoreAsync(CancellationToken cancellationToken)
    {
        lock (Records)
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
            doc = await store.GetWorldEventsAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_event_load_error error=" + ex.Message);
            return;
        }
        lock (Records)
        {
            Records.Clear();
            if (doc?["records"] is JArray records)
            {
                foreach (JToken token in records)
                {
                    if (token is not JObject record) continue;
                    Records.Enqueue(new WorldEventRecord(
                        IntValue(record["day"]),
                        (string)record["kind"] ?? "event",
                        (string)record["text"] ?? string.Empty));
                }
                while (Records.Count > Capacity) Records.Dequeue();
            }
            _loaded = true;
        }
    }

    internal static string FormatWeek(int nowDay)
    {
        lock (Records)
        {
            List<WorldEventRecord> week = new List<WorldEventRecord>();
            foreach (WorldEventRecord record in Records)
            {
                if (nowDay - record.Day < 7 && nowDay - record.Day >= 0)
                {
                    week.Add(record);
                }
            }
            if (week.Count == 0) return "本周没有记录。";
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("世界周报");
            builder.AppendLine("────────────");
            foreach (WorldEventRecord record in week)
            {
                builder.AppendLine("第 " + record.Day + " 天 [" + record.Kind + "] " + record.Text);
            }
            return builder.ToString();
        }
    }

    internal static List<WorldEventRecord> SnapshotWeek(int nowDay)
    {
        lock (Records)
        {
            List<WorldEventRecord> week = new List<WorldEventRecord>();
            foreach (WorldEventRecord record in Records)
            {
                if (nowDay - record.Day < 7 && nowDay - record.Day >= 0)
                {
                    week.Add(record);
                }
            }
            return week;
        }
    }

    internal static void ClearForTesting()
    {
        ResetForCampaign();
    }

    internal static void ResetForCampaign()
    {
        lock (Records)
        {
            Records.Clear();
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
