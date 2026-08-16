using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class NpcMemoryOverviewBuilder
{
    internal const int DefaultMaximumBytes = 2000;
    internal const int MaximumEntries = 10;

    internal static string BuildOverview(JObject doc, int day, int maximumBytes = DefaultMaximumBytes)
    {
        if (doc == null || !(doc["memories"] is JArray memories)) return string.Empty;
        List<JObject> entries = new List<JObject>();
        foreach (JToken token in memories)
        {
            if (token is JObject entry) entries.Add(entry);
        }
        entries.Sort((a, b) =>
        {
            int weightCompare = IntValue(b["weight"]).CompareTo(IntValue(a["weight"]));
            if (weightCompare != 0) return weightCompare;
            int dayCompare = IntValue(b["day"]).CompareTo(IntValue(a["day"]));
            if (dayCompare != 0) return dayCompare;
            return string.CompareOrdinal((string)a["id"] ?? string.Empty, (string)b["id"] ?? string.Empty);
        });
        if (entries.Count > MaximumEntries) entries.RemoveRange(MaximumEntries, entries.Count - MaximumEntries);

        StringBuilder builder = new StringBuilder();
        int budget = 0;
        foreach (JObject entry in entries)
        {
            string line = FormatLine(entry);
            if (string.IsNullOrWhiteSpace(line)) continue;
            int consumed;
            string clamped = ClampToBytes(line, maximumBytes - budget, out consumed);
            if (consumed <= 0) break;
            if (builder.Length > 0)
            {
                budget += Encoding.UTF8.GetByteCount("\n");
                if (budget > maximumBytes) break;
                builder.Append('\n');
            }
            builder.Append(clamped);
            budget += consumed;
        }
        return builder.ToString();
    }

    private static string FormatLine(JObject entry)
    {
        int day = IntValue(entry["day"]);
        string type = (string)entry["type"] ?? "shared_experience";
        string summary = (string)entry["summary"] ?? string.Empty;
        JArray facts = entry["facts"] as JArray;
        List<string> factTexts = new List<string>();
        if (facts != null)
        {
            foreach (JToken token in facts)
            {
                if (token is JValue value && !string.IsNullOrWhiteSpace(Convert.ToString(value)))
                {
                    factTexts.Add(Convert.ToString(value));
                }
            }
        }
        StringBuilder line = new StringBuilder();
        line.Append("· 第").Append(day).Append("天（").Append(type).Append("）：");
        if (!string.IsNullOrWhiteSpace(summary)) line.Append(summary);
        if (factTexts.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(summary)) line.Append('；');
            line.Append(string.Join("、", factTexts));
        }
        return line.ToString();
    }

    private static string ClampToBytes(string value, int budget, out int consumed)
    {
        consumed = 0;
        if (string.IsNullOrEmpty(value) || budget <= 0) return string.Empty;
        StringBuilder builder = new StringBuilder();
        System.Globalization.TextElementEnumerator enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            int elementBytes = Encoding.UTF8.GetByteCount(element);
            if (consumed + elementBytes > budget) break;
            builder.Append(element);
            consumed += elementBytes;
        }
        return builder.ToString();
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }
}
