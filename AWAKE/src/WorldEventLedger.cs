using System;
using System.Collections.Generic;
using System.Text;

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
    private static readonly Queue<WorldEventRecord> Records = new Queue<WorldEventRecord>();

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
        lock (Records) Records.Clear();
    }
}
