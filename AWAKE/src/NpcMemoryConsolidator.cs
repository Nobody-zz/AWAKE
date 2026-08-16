using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class NpcMemoryConsolidationResult
{
    internal bool Changed { get; set; }
    internal int Removed { get; set; }
    internal int Merged { get; set; }
}

internal static class NpcMemoryConsolidator
{
    internal const int LowWeightMaxAgeDays = 30;
    internal const int MergeMaxAgeDays = 45;
    internal const int FactsMaximum = 8;

    internal static NpcMemoryConsolidationResult Consolidate(
        JObject doc,
        int nowDay,
        out JArray memories,
        out JArray promises)
    {
        memories = new JArray();
        promises = new JArray();
        NpcMemoryConsolidationResult result = new NpcMemoryConsolidationResult();
        if (doc == null) return result;

        List<JObject> all = new List<JObject>();
        if (doc["memories"] is JArray existing)
        {
            foreach (JToken token in existing)
            {
                if (token is JObject entry) all.Add((JObject)entry.DeepClone());
            }
        }
        if (doc["promises"] is JArray existingPromises)
        {
            foreach (JToken token in existingPromises)
            {
                if (token is JObject promise) promises.Add((JObject)promise.DeepClone());
            }
        }

        List<JObject> kept = new List<JObject>();
        Dictionary<string, List<JObject>> mergeBuckets =
            new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
        foreach (JObject entry in all)
        {
            if (IsPromise(entry))
            {
                promises.Add(entry);
                continue;
            }
            int weight = IntValue(entry["weight"], 1);
            int age = Math.Max(0, nowDay - IntValue(entry["day"], nowDay));
            if (weight <= 1 && age >= LowWeightMaxAgeDays)
            {
                result.Removed++;
                result.Changed = true;
                continue;
            }
            if (weight == 2 && age >= MergeMaxAgeDays)
            {
                string key = MergeKey(entry);
                List<JObject> bucket;
                if (!mergeBuckets.TryGetValue(key, out bucket))
                {
                    bucket = new List<JObject>();
                    mergeBuckets[key] = bucket;
                }
                bucket.Add(entry);
                continue;
            }
            kept.Add(entry);
        }

        foreach (KeyValuePair<string, List<JObject>> pair in mergeBuckets)
        {
            if (pair.Value.Count == 0) continue;
            if (pair.Value.Count > 1) result.Merged += pair.Value.Count - 1;
            result.Changed = true;
            kept.Add(MergeEntries(pair.Value));
        }

        kept.Sort((a, b) => IntValue(b["day"], 0).CompareTo(IntValue(a["day"], 0)));
        foreach (JObject entry in kept) memories.Add(entry);
        return result;
    }

    private static bool IsPromise(JObject entry)
    {
        return BoolValue(entry["promise"])
            || StringComparer.Ordinal.Equals((string)entry["type"], "promise");
    }

    private static string MergeKey(JObject entry)
    {
        string entity = (string)entry["entityId"] ?? string.Empty;
        string eventType = (string)entry["eventType"] ?? string.Empty;
        return entity + "|" + eventType;
    }

    private static JObject MergeEntries(List<JObject> entries)
    {
        JObject first = entries[0];
        List<string> summaries = new List<string>();
        List<string> facts = new List<string>();
        int oldestDay = int.MaxValue;
        foreach (JObject entry in entries)
        {
            string summary = (string)entry["summary"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(summary)) summaries.Add(summary);
            if (entry["facts"] is JArray array)
            {
                foreach (JToken token in array)
                {
                    string fact = token is JValue value ? Convert.ToString(value) : token?.ToString();
                    if (string.IsNullOrWhiteSpace(fact)) continue;
                    if (facts.Count >= FactsMaximum) break;
                    facts.Add(fact);
                }
            }
            oldestDay = Math.Min(oldestDay, IntValue(entry["day"], 0));
        }

        JArray factArray = new JArray();
        foreach (string fact in facts) factArray.Add(fact);
        JObject merged = (JObject)first.DeepClone();
        merged["day"] = oldestDay;
        merged["type"] = "merged";
        merged["summary"] = string.Join("；", summaries);
        merged["facts"] = factArray;
        merged["weight"] = 2;
        merged["result"] = "merged";
        return merged;
    }

    private static int IntValue(JToken token, int fallback)
    {
        if (token == null || token.Type != JTokenType.Integer) return fallback;
        try { return (int)token; } catch { return fallback; }
    }

    private static bool BoolValue(JToken token)
    {
        return token != null && token.Type == JTokenType.Boolean && (bool)token;
    }
}
