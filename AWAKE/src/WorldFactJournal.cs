using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Awake;

internal enum WorldFactJournalReadStatus
{
    Missing,
    Empty,
    Success,
    Corrupt,
    Unavailable
}

internal sealed class WorldFactJournalReadResult
{
    internal WorldFactJournalReadStatus Status { get; }
    internal IReadOnlyList<JObject> Facts { get; }
    internal string ErrorCode { get; }
    internal int? Revision { get; }
    internal WorldFactJournalReadResult(WorldFactJournalReadStatus status, IReadOnlyList<JObject> facts = null, string errorCode = null, int? revision = null)
    {
        Status = status;
        Facts = facts ?? new List<JObject>();
        ErrorCode = errorCode ?? string.Empty;
        Revision = revision;
    }
}

/// <summary>Canonical journal payload/root codec. Storage adapters are intentionally outside this pure layer.</summary>
internal static class WorldFactJournalCodec
{
    internal const string RootSchema = "awake.world_fact_journal.root.v1";
    internal const string ChunkSchema = "awake.world_fact_journal.chunk.v1";
    internal const int MaximumFactsPerChunk = 64;
    private static readonly Regex ChunkKeyPattern = new Regex(
        "^facts-(?<start>[0-9]{8})-(?<end>[0-9]{8})-r(?<revision>[0-9]{8})-(?<ordinal>[0-9]{4})-(?<hash>[0-9a-f]{64})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static JObject BuildChunk(int startDay, int endDay, int revision, IEnumerable<JObject> facts)
    {
        if (startDay < 1 || endDay < startDay || revision < 1) throw new ArgumentOutOfRangeException();
        JArray values = new JArray((facts ?? Enumerable.Empty<JObject>()).Where(x => x != null)
            .OrderBy(x => (int?)x["occurred"]?["campaignDay"] ?? 0)
            .ThenBy(x => (long?)x["occurred"]?["timeSlot"] ?? 0L)
            .ThenBy(x => (string)x["factId"], StringComparer.Ordinal)
            .Select(x => x.DeepClone()));
        if (values.Count > MaximumFactsPerChunk) throw new InvalidOperationException("awake.world_fact.chunk_full");
        return new JObject { ["schema"] = ChunkSchema, ["startDay"] = startDay, ["endDay"] = endDay, ["revision"] = revision, ["facts"] = values };
    }

    internal static string CanonicalJson(JToken value)
    {
        return (value ?? JValue.CreateNull()).ToString(Newtonsoft.Json.Formatting.None);
    }

    internal static string Sha256Hex(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            foreach (byte item in bytes) result.Append(item.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }

    internal static bool TryParseChunkKey(string key, out int startDay, out int endDay, out int revision, out int ordinal, out string hash)
    {
        startDay = endDay = revision = ordinal = 0;
        hash = string.Empty;
        Match match = ChunkKeyPattern.Match(key ?? string.Empty);
        if (!match.Success
            || !int.TryParse(match.Groups["start"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out startDay)
            || !int.TryParse(match.Groups["end"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out endDay)
            || !int.TryParse(match.Groups["revision"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out revision)
            || !int.TryParse(match.Groups["ordinal"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out ordinal))
            return false;
        hash = match.Groups["hash"].Value;
        return startDay >= 1 && endDay >= startDay && revision >= 1;
    }

    internal static WorldFactJournalReadResult ReadChunk(string json)
    {
        if (json == null) return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Missing);
        if (string.IsNullOrWhiteSpace(json)) return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.chunk_empty");
        try
        {
            JObject doc = JObject.Parse(json);
            int startDay = (int?)doc["startDay"] ?? 0;
            int endDay = (int?)doc["endDay"] ?? 0;
            if ((string)doc["schema"] != ChunkSchema || doc["facts"] is not JArray facts
                || !ValidBounds(doc["startDay"], doc["endDay"], doc["revision"]) || facts.Count > MaximumFactsPerChunk)
                return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.chunk_invalid");
            if (facts.Count == 0) return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Empty);
            List<JObject> valid = new List<JObject>();
            foreach (JToken token in facts)
            {
                if (!(token is JObject fact) || !ValidFact(fact))
                    return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.fact_invalid");
                valid.Add((JObject)fact.DeepClone());
            }
            if (valid.Any(x => (int?)x["occurred"]?["campaignDay"] < startDay || (int?)x["occurred"]?["campaignDay"] > endDay))
                return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.fact_out_of_bounds");
            if (valid.Select(x => (string)x["factId"]).Distinct(StringComparer.Ordinal).Count() != valid.Count)
                return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.fact_duplicate");
            return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Success, valid);
        }
        catch (Exception)
        {
            return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.chunk_json_invalid");
        }
    }

    internal static JObject BuildRoot(int startDay, int endDay, int revision, IEnumerable<string> chunkKeys)
    {
        if (startDay < 1 || endDay < startDay || revision < 1) throw new ArgumentOutOfRangeException();
        JArray keys = new JArray((chunkKeys ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));
        return new JObject { ["schema"] = RootSchema, ["startDay"] = startDay, ["endDay"] = endDay, ["revision"] = revision, ["phase"] = "committed", ["chunkKeys"] = keys };
    }

    internal static WorldFactJournalReadStatus ReadRoot(string json, out JObject root)
    {
        root = null;
        if (json == null) return WorldFactJournalReadStatus.Missing;
        // 空/空白 = 存储层在说"这个 key 还没有值"（AwakeFileStorageService 对不存在的 key
        // 返回的是 Succeeded("")，不是 storage.key_not_found）。判 Missing 而不是 Corrupt：
        // 把"还没有"当"坏了"，会让写侧（读到 Corrupt 就放弃写入）永久死锁 —— 2026-09-15 定案。
        if (string.IsNullOrWhiteSpace(json)) return WorldFactJournalReadStatus.Missing;
        try
        {
            root = JObject.Parse(json);
            JArray chunkKeys = root["chunkKeys"] as JArray;
            if ((string)root["schema"] != RootSchema || (string)root["phase"] != "committed" || chunkKeys == null
                || !ValidBounds(root["startDay"], root["endDay"], root["revision"])
                || chunkKeys.Any(x => x.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)x))
                || chunkKeys.Select(x => (string)x).Distinct(StringComparer.Ordinal).Count() != chunkKeys.Count
                || !chunkKeys.Select(x => (string)x).SequenceEqual(chunkKeys.Select(x => (string)x).OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
            { root = null; return WorldFactJournalReadStatus.Corrupt; }
            return ((JArray)root["chunkKeys"]).Count == 0 ? WorldFactJournalReadStatus.Empty : WorldFactJournalReadStatus.Success;
        }
        catch (Exception) { return WorldFactJournalReadStatus.Corrupt; }
    }

    private static bool ValidBounds(JToken start, JToken end, JToken revision)
    {
        return (int?)start >= 1 && (int?)end >= (int?)start && (int?)revision >= 1;
    }

    private static bool ValidFact(JObject fact)
    {
        JObject occurred = fact["occurred"] as JObject;
        JArray entities = fact["entities"] as JArray;
        JObject presentation = fact["presentation"] as JObject;
        return (string)fact["schema"] == WorldFact.Schema
            && !string.IsNullOrWhiteSpace((string)fact["factId"])
            && !string.IsNullOrWhiteSpace((string)fact["eventKey"])
            && WorldFact.IsStableFactId((string)fact["eventKey"], (string)fact["factId"])
            && ValidOccurrence(occurred)
            && !string.IsNullOrWhiteSpace((string)fact["kind"])
            && (string)fact["origin"] == "native_game" && (string)fact["authority"] == "game_confirmed"
            && (string)fact["initialKnowledgeScope"] == "public"
            && presentation != null
            && !string.IsNullOrWhiteSpace((string)presentation["summary"])
            && !string.IsNullOrWhiteSpace((string)presentation["domain"])
            && entities != null && entities.Count > 0
            && entities.All(x => x is JObject e && !string.IsNullOrWhiteSpace((string)e["type"])
                && !string.IsNullOrWhiteSpace((string)e["id"]) && !string.IsNullOrWhiteSpace((string)e["role"]))
            && entities.OfType<JObject>().Select(e => (string)e["type"] + "\n" + (string)e["id"] + "\n" + (string)e["role"]).Distinct(StringComparer.Ordinal).Count() == entities.Count;
    }

    private static bool ValidOccurrence(JObject occurred)
    {
        int day = (int?)occurred?["campaignDay"] ?? 0;
        long slot = (long?)occurred?["timeSlot"] ?? -1L;
        return day >= 1 && slot >= day * 144L && slot < (day + 1L) * 144L;
    }
}
