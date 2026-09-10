using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class WeeklyReportService
{
    private static readonly string[] DomainOrder = { "politics", "economy", "culture", "war" };

    internal static JObject Build(IReadOnlyList<WorldEventRecord> records, int nowDay)
    {
        return BuildWindow(records, nowDay - 6, nowDay);
    }

    internal static JObject BuildWindow(IReadOnlyList<WorldEventRecord> records, int startDay, int endDay)
    {
        List<WorldEventRecord> ordered = (records ?? new List<WorldEventRecord>())
            .Where(x => WorldEventContract.CanProject(x) && x.Day >= startDay && x.Day <= endDay)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.EventId) ? x.Day + "|" + x.Kind + "|" + x.Text : x.EventId, StringComparer.Ordinal)
            .Select(x => x.OrderBy(record => record.OccurredAt).ThenBy(record => record.EventId, StringComparer.Ordinal).First())
            .OrderBy(x => x.Day)
            .ThenBy(x => x.EventId, StringComparer.Ordinal)
            .ToList();
        DateTimeOffset epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset start = epoch.AddDays(startDay);
        DateTimeOffset end = epoch.AddDays(endDay + 1);
        var sourceEventIds = new JArray(ordered.Select(x => x.EventId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal));
        IReadOnlyList<string> audience = WorldEventAudience.Intersect(ordered);
        var sections = new JArray();
        foreach (string domain in DomainOrder)
        {
            JArray items = new JArray();
            int index = 0;
            foreach (WorldEventRecord record in ordered.Where(x => StringComparer.Ordinal.Equals(x.Domain, domain)))
            {
                items.Add(new JObject
                {
                    ["itemId"] = "awake:item:weekly-" + endDay + "-" + domain + "-" + index++,
                    ["text"] = new JObject { ["zh-CN"] = "第 " + record.Day + " 天：" + record.Text },
                    ["sourceEventIds"] = new JArray(record.EventId)
                });
            }
            sections.Add(new JObject
            {
                ["sectionId"] = "awake:section:weekly-" + endDay + "-" + domain,
                ["domain"] = domain,
                ["title"] = new JObject { ["zh-CN"] = DomainTitle(domain) },
                ["items"] = items
            });
        }
        return new JObject
        {
            ["schemaVersion"] = "awake.worldbook.weekly-report.v1",
            ["reportId"] = "awake:report:weekly-" + endDay,
            ["period"] = new JObject { ["start"] = start.ToString("O"), ["end"] = end.ToString("O") },
            ["generatedBy"] = "awake:system:weekly-report-generator",
            ["sourceEventIds"] = sourceEventIds,
            ["sections"] = sections,
            ["visibility"] = new JObject
            {
                ["scope"] = "local",
                ["min_detail"] = "summary",
                ["identity_ids"] = new JArray(audience.Select(value => (object)value))
            },
            ["extensions"] = new JObject
            {
                ["awake:windowStartDay"] = startDay,
                ["awake:windowEndDay"] = endDay
            }
        };
    }

    internal static List<int> CompletedWindowEnds(int currentDay)
    {
        var result = new List<int>();
        if (currentDay < 7) return result;
        int lastEnd = currentDay - currentDay % 7;
        for (int endDay = 7; endDay <= lastEnd; endDay += 7) result.Add(endDay);
        return result;
    }

    internal static string BuildText(IReadOnlyList<WorldEventRecord> records, int nowDay)
    {
        JObject report = Build(records, nowDay);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("世界周报");
        builder.AppendLine("────────────");
        bool hasItems = false;
        foreach (JObject section in (JArray)report["sections"])
        {
            JArray items = (JArray)section["items"];
            if (items == null || items.Count == 0) continue;
            hasItems = true;
            builder.AppendLine((string)section["title"]?["zh-CN"] ?? (string)section["domain"] ?? string.Empty);
            foreach (JObject item in items)
                builder.AppendLine("· " + ((string)item["text"]?["zh-CN"] ?? string.Empty));
        }
        if (!hasItems) builder.AppendLine("本周没有记录。");
        return builder.ToString().TrimEnd();
    }

    internal static string InferDomain(string kind)
    {
        string value = (kind ?? string.Empty).ToLowerInvariant();
        if (ContainsAny(value, "war", "battle", "siege", "raid", "army", "troop", "death", "casualty")) return "war";
        if (ContainsAny(value, "tax", "trade", "gold", "price", "market", "food", "grain", "economy")) return "economy";
        if (ContainsAny(value, "culture", "religion", "festival", "feast", "custom", "faith")) return "culture";
        return "politics";
    }

    internal static string NormalizeDomain(string domain, string kind)
    {
        switch ((domain ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "politics":
            case "economy":
            case "culture":
            case "war":
                return domain.Trim().ToLowerInvariant();
            default:
                return InferDomain(kind);
        }
    }

    private static string DomainTitle(string domain)
    {
        switch (domain)
        {
            case "politics": return "政治与权力";
            case "economy": return "经济与生计";
            case "culture": return "文化与信仰";
            case "war": return "战争与军务";
            default: return domain;
        }
    }

    private static bool ContainsAny(string value, params string[] terms) => terms.Any(value.Contains);
}
