using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class WeeklyReportService
{
    private static readonly string[] DomainOrder = { "politics", "economy", "culture", "war" };
    private static readonly string[] V2DomainOrder = { "politics", "war", "people", "local" };
    internal const string V2SchemaVersion = "awake.worldbook.weekly-report.v2";
    internal const string V2GeneratedBy = "awake:system:weekly-report-generator-v2";
    internal const string V2PolicyVersion = "awake.weekly-report.policy.v2";

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

    internal static bool TryBuildFromFacts(
        WeeklyDynamicsInput input,
        out JObject report,
        out string errorCode)
    {
        report = null;
        errorCode = string.Empty;
        if (!TryValidateWeeklyInput(input, out errorCode)) return false;

        List<JObject> ordered = input.Facts
            .OrderBy(value => (int?)value["occurred"]?["campaignDay"] ?? 0)
            .ThenBy(value => (long?)value["occurred"]?["timeSlot"] ?? 0L)
            .ThenBy(value => (string)value["factId"], StringComparer.Ordinal)
            .ToList();
        string[] domains = { "politics", "war", "people", "local" };
        var sections = new JArray();
        foreach (string domain in domains)
        {
            var domainFacts = ordered
                .Where(value => StringComparer.Ordinal.Equals(FactDomain(value), domain))
                .ToList();
            if (domainFacts.Count == 0) continue;
            var items = new JArray();
            for (int index = 0; index < domainFacts.Count; index++)
            {
                JObject fact = domainFacts[index];
                string factId = (string)fact["factId"];
                int day = (int)fact["occurred"]["campaignDay"];
                items.Add(new JObject
                {
                    ["itemId"] = "awake:item:weekly-v0-" + input.WindowEndDay + "-" + domain + "-" + index,
                    ["text"] = new JObject { ["zh-CN"] = "第 " + day + " 天：" + (string)fact["presentation"]["summary"] },
                    ["sourceFactIds"] = new JArray(factId)
                });
            }
            sections.Add(new JObject
            {
                ["sectionId"] = "awake:section:weekly-v0-" + input.WindowEndDay + "-" + domain,
                ["domain"] = domain,
                ["title"] = new JObject { ["zh-CN"] = FactDomainTitle(domain) },
                ["items"] = items
            });
        }

        report = new JObject
        {
            ["schemaVersion"] = "awake.worldbook.weekly-report.v0-preview",
            ["windowStartDay"] = input.WindowStartDay,
            ["windowEndDay"] = input.WindowEndDay,
            ["sourceFactIds"] = new JArray(input.SourceFactIds.OrderBy(value => value, StringComparer.Ordinal)),
            ["sections"] = sections
        };
        return true;
    }

    internal static bool TryBuildV2FromFacts(
        WeeklyDynamicsInput input,
        out JObject report,
        out string errorCode)
    {
        report = null;
        errorCode = string.Empty;
        if (!TryValidateWeeklyInput(input, out errorCode)) return false;

        List<JObject> ordered = OrderFacts(input.Facts);
        int startDay = input.WindowStartDay;
        int endDay = input.WindowEndDay;
        DateTimeOffset epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset start = epoch.AddDays(startDay);
        DateTimeOffset end = epoch.AddDays(endDay + 1);
        JArray sections = new JArray();
        foreach (string domain in V2DomainOrder)
        {
            List<JObject> domainFacts = ordered.Where(value => StringComparer.Ordinal.Equals(FactDomain(value), domain)).ToList();
            if (domainFacts.Count == 0) continue;
            JArray items = new JArray();
            for (int index = 0; index < domainFacts.Count; index++)
            {
                JObject fact = domainFacts[index];
                int day = (int)fact["occurred"]["campaignDay"];
                string factId = (string)fact["factId"];
                items.Add(new JObject
                {
                    ["itemId"] = "awake:item:weekly-v2-" + endDay.ToString(CultureInfo.InvariantCulture) + "-" + domain + "-" + index.ToString(CultureInfo.InvariantCulture),
                    ["text"] = new JObject { ["zh-CN"] = "第 " + day.ToString(CultureInfo.InvariantCulture) + " 天：" + (string)fact["presentation"]["summary"] },
                    ["sourceFactIds"] = new JArray(factId)
                });
            }
            sections.Add(new JObject
            {
                ["sectionId"] = "awake:section:weekly-v2-" + endDay.ToString(CultureInfo.InvariantCulture) + "-" + domain,
                ["domain"] = domain,
                ["title"] = new JObject { ["zh-CN"] = FactDomainTitle(domain) },
                ["items"] = items
            });
        }

        report = new JObject
        {
            ["schemaVersion"] = V2SchemaVersion,
            ["reportId"] = "awake:report:weekly-v2-" + endDay.ToString(CultureInfo.InvariantCulture),
            ["period"] = new JObject { ["start"] = start.ToString("O", CultureInfo.InvariantCulture), ["end"] = end.ToString("O", CultureInfo.InvariantCulture) },
            ["generatedBy"] = V2GeneratedBy,
            ["policyVersion"] = V2PolicyVersion,
            ["contentFingerprint"] = string.Empty,
            ["sourceFactIds"] = new JArray(ordered
                .Select(value => (string)value["factId"])
                .OrderBy(value => value, StringComparer.Ordinal)),
            ["sections"] = sections,
            ["visibility"] = new JObject
            {
                ["scope"] = "local",
                ["min_detail"] = "summary",
                ["identity_ids"] = new JArray()
            },
            ["extensions"] = new JObject
            {
                ["awake:windowStartDay"] = startDay,
                ["awake:windowEndDay"] = endDay
            }
        };
        report["contentFingerprint"] = ComputeV2Fingerprint(report);
        return true;
    }

    internal static string CanonicalizeV2(JObject report)
    {
        if (report == null) return string.Empty;
        JObject payload = (JObject)report.DeepClone();
        payload.Remove("contentFingerprint");
        return CanonicalizeV2Token(payload);
    }

    internal static string ComputeV2Fingerprint(JObject report)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(CanonicalizeV2(report)));
            StringBuilder result = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest) result.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }

    internal static bool TryValidateV2Report(JObject report, out string errorCode)
    {
        errorCode = string.Empty;
        if (report == null || !HasOnlyV2Properties(report, "schemaVersion", "reportId", "period", "generatedBy", "policyVersion", "contentFingerprint", "sourceFactIds", "sections", "visibility", "extensions") || ContainsNull(report))
            return V2Fail("awake.world_report.v2.invalid_schema", out errorCode);
        if ((string)report["schemaVersion"] != V2SchemaVersion || (string)report["generatedBy"] != V2GeneratedBy || (string)report["policyVersion"] != V2PolicyVersion)
            return V2Fail("awake.world_report.v2.invalid_schema", out errorCode);

        JObject extensions = report["extensions"] as JObject;
        if (extensions == null || !HasOnlyV2Properties(extensions, "awake:windowStartDay", "awake:windowEndDay")
            || !TryGetV2Int(extensions["awake:windowStartDay"], out int startDay)
            || !TryGetV2Int(extensions["awake:windowEndDay"], out int endDay)
            || startDay < 1 || endDay < 7 || endDay % 7 != 0 || startDay != endDay - 6)
            return V2Fail("awake.world_report.v2.window_invalid", out errorCode);
        string expectedReportId = "awake:report:weekly-v2-" + endDay.ToString(CultureInfo.InvariantCulture);
        if ((string)report["reportId"] != expectedReportId)
            return V2Fail("awake.world_report.v2.window_invalid", out errorCode);

        DateTimeOffset epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        string expectedStart = epoch.AddDays(startDay).ToString("O", CultureInfo.InvariantCulture);
        string expectedEnd = epoch.AddDays(endDay + 1).ToString("O", CultureInfo.InvariantCulture);
        JObject period = report["period"] as JObject;
        if (period == null || !HasOnlyV2Properties(period, "start", "end")
            || (string)period["start"] != expectedStart || (string)period["end"] != expectedEnd)
            return V2Fail("awake.world_report.v2.window_invalid", out errorCode);

        JArray sourceFactIds = report["sourceFactIds"] as JArray;
        if (sourceFactIds == null || !TryValidateV2FactIds(sourceFactIds, out errorCode) || !IsV2OrdinallySorted(sourceFactIds))
            return V2Fail("awake.world_report.v2.source_closure_invalid", out errorCode);

        JObject visibility = report["visibility"] as JObject;
        if (visibility == null || !HasOnlyV2Properties(visibility, "scope", "min_detail", "identity_ids")
            || (string)visibility["scope"] != "local" || (string)visibility["min_detail"] != "summary"
            || !(visibility["identity_ids"] is JArray identityIds) || identityIds.Count != 0)
            return V2Fail("awake.world_report.v2.invalid_schema", out errorCode);

        JArray sections = report["sections"] as JArray;
        if (sections == null) return V2Fail("awake.world_report.v2.invalid_schema", out errorCode);
        HashSet<string> sourceSet = new HashSet<string>(sourceFactIds.Values<string>(), StringComparer.Ordinal);
        HashSet<string> rendered = new HashSet<string>(StringComparer.Ordinal);
        int domainCursor = 0;
        HashSet<string> sectionIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JToken sectionToken in sections)
        {
            JObject section = sectionToken as JObject;
            if (section == null || !HasOnlyV2Properties(section, "sectionId", "domain", "title", "items"))
                return V2Fail("awake.world_report.v2.invalid_schema", out errorCode);
            string domain = (string)section["domain"];
            int domainIndex = Array.IndexOf(V2DomainOrder, domain);
            string sectionId = "awake:section:weekly-v2-" + endDay.ToString(CultureInfo.InvariantCulture) + "-" + domain;
            if (domainIndex < domainCursor || domainIndex < 0 || !sectionIds.Add((string)section["sectionId"]) || (string)section["sectionId"] != sectionId
                || !HasV2LocalizedText(section["title"] as JObject) || (string)section["title"]["zh-CN"] != FactDomainTitle(domain)
                || !(section["items"] is JArray items) || items.Count == 0)
                return V2Fail("awake.world_report.v2.invalid_schema", out errorCode);
            domainCursor = domainIndex + 1;
            for (int index = 0; index < items.Count; index++)
            {
                JObject item = items[index] as JObject;
                if (item == null || !HasOnlyV2Properties(item, "itemId", "text", "sourceFactIds")
                    || !HasV2LocalizedText(item["text"] as JObject)
                    || (string)item["itemId"] != "awake:item:weekly-v2-" + endDay.ToString(CultureInfo.InvariantCulture) + "-" + domain + "-" + index.ToString(CultureInfo.InvariantCulture)
                    || !itemIds.Add((string)item["itemId"]))
                    return V2Fail("awake.world_report.v2.invalid_schema", out errorCode);
                JArray itemSources = item["sourceFactIds"] as JArray;
                if (itemSources == null || itemSources.Count == 0 || !TryValidateV2FactIds(itemSources, out errorCode) || !IsV2OrdinallySorted(itemSources)
                    || itemSources.Any(value => !sourceSet.Contains((string)value) || !rendered.Add((string)value)))
                    return V2Fail("awake.world_report.v2.source_closure_invalid", out errorCode);
            }
        }
        if (rendered.Count != sourceSet.Count || !rendered.SetEquals(sourceSet) || (sourceSet.Count == 0 && sections.Count != 0) || (sourceSet.Count > 0 && sections.Count == 0))
            return V2Fail("awake.world_report.v2.source_closure_invalid", out errorCode);

        if (!(report["contentFingerprint"] is JValue fingerprintValue) || fingerprintValue.Type != JTokenType.String
            || !Regex.IsMatch((string)fingerprintValue, "^[0-9A-F]{64}$", RegexOptions.CultureInvariant)
            || !StringComparer.Ordinal.Equals((string)fingerprintValue, ComputeV2Fingerprint(report)))
            return V2Fail("awake.world_report.v2.fingerprint_mismatch", out errorCode);
        return true;
    }

    private static List<JObject> OrderFacts(IReadOnlyList<JObject> facts)
    {
        return (facts ?? Array.Empty<JObject>())
            .Select(value => (JObject)value.DeepClone())
            .OrderBy(value => (int)value["occurred"]["campaignDay"])
            .ThenBy(value => (long)value["occurred"]["timeSlot"])
            .ThenBy(value => (string)value["factId"], StringComparer.Ordinal)
            .ToList();
    }

    private static bool TryValidateV2FactIds(JArray values, out string errorCode)
    {
        errorCode = string.Empty;
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JToken value in values ?? new JArray())
        {
            string id = value.Type == JTokenType.String ? (string)value : string.Empty;
            if (!Regex.IsMatch(id, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant) || !seen.Add(id))
            {
                errorCode = "awake.world_report.v2.source_closure_invalid";
                return false;
            }
        }
        return true;
    }

    private static bool IsV2OrdinallySorted(JArray values)
    {
        string previous = null;
        foreach (string value in values.Values<string>())
        {
            if (previous != null && StringComparer.Ordinal.Compare(previous, value) > 0) return false;
            previous = value;
        }
        return true;
    }

    private static string CanonicalizeV2Token(JToken value)
    {
        StringBuilder builder = new StringBuilder();
        using (StringWriter writer = new StringWriter(builder, CultureInfo.InvariantCulture))
        using (JsonTextWriter json = new JsonTextWriter(writer) { Formatting = Formatting.None, Culture = CultureInfo.InvariantCulture })
        {
            WriteCanonicalV2(value, json);
        }
        return builder.ToString();
    }

    private static void WriteCanonicalV2(JToken value, JsonWriter writer)
    {
        if (value is JObject obj)
        {
            writer.WriteStartObject();
            foreach (JProperty property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonicalV2(property.Value, writer);
            }
            writer.WriteEndObject();
            return;
        }
        if (value is JArray array)
        {
            writer.WriteStartArray();
            foreach (JToken item in array) WriteCanonicalV2(item, writer);
            writer.WriteEndArray();
            return;
        }
        value.WriteTo(writer);
    }

    private static bool HasOnlyV2Properties(JObject value, params string[] allowedNames)
    {
        if (value == null) return false;
        HashSet<string> allowed = new HashSet<string>(allowedNames ?? Array.Empty<string>(), StringComparer.Ordinal);
        return value.Properties().All(property => allowed.Contains(property.Name));
    }

    private static bool HasV2LocalizedText(JObject value)
    {
        return value != null && HasOnlyV2Properties(value, "zh-CN") && value["zh-CN"]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)value["zh-CN"]);
    }

    private static bool ContainsNull(JToken value)
    {
        if (value == null || value.Type == JTokenType.Null || value.Type == JTokenType.Undefined) return true;
        return value.Children().Any(ContainsNull);
    }

    private static bool TryGetV2Int(JToken value, out int result)
    {
        result = 0;
        return value != null && value.Type == JTokenType.Integer && value.ToObject<long>() >= int.MinValue && value.ToObject<long>() <= int.MaxValue && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool V2Fail(string code, out string errorCode)
    {
        errorCode = code;
        return false;
    }

    private static bool TryValidateWeeklyInput(WeeklyDynamicsInput input, out string errorCode)
    {
        errorCode = string.Empty;
        if (input == null || input.Policy != WorldFactSelectionPolicy.WeeklyDynamics)
        {
            errorCode = "awake.world_fact.report.query_failed";
            return false;
        }
        if (input.Status != WorldFactQueryStatus.Success && input.Status != WorldFactQueryStatus.Empty)
        {
            errorCode = "awake.world_fact.report.query_failed";
            return false;
        }
        if (input.WindowStartDay < 1
            || input.WindowEndDay < 7
            || input.WindowEndDay % 7 != 0
            || input.WindowStartDay != input.WindowEndDay - 6)
        {
            errorCode = "awake.world_fact.report.window_invalid";
            return false;
        }
        if (input.Facts.Count != input.SourceFactIds.Count
            || (input.Status == WorldFactQueryStatus.Empty && input.Facts.Count != 0)
            || (input.Status == WorldFactQueryStatus.Success && input.Facts.Count == 0))
        {
            errorCode = "awake.world_fact.report.source_closure_invalid";
            return false;
        }
        HashSet<string> factIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < input.Facts.Count; index++)
        {
            JObject fact = input.Facts[index];
            string factId = (string)fact["factId"] ?? string.Empty;
            string eventKey = (string)fact["eventKey"] ?? string.Empty;
            int day = (int?)fact["occurred"]?["campaignDay"] ?? 0;
            long timeSlot = (long?)fact["occurred"]?["timeSlot"] ?? -1L;
            string summary = (string)fact["presentation"]?["summary"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factId)
                || !WorldFact.IsStableFactId(eventKey, factId)
                || !factIds.Add(factId)
                || !StringComparer.Ordinal.Equals(factId, input.SourceFactIds[index])
                || day < input.WindowStartDay
                || day > input.WindowEndDay
                || timeSlot < day * 144L
                || timeSlot >= (day + 1L) * 144L
                || string.IsNullOrWhiteSpace(summary)
                || string.IsNullOrWhiteSpace(FactDomain(fact)))
            {
                errorCode = "awake.world_fact.report.fact_invalid";
                return false;
            }
        }
        return true;
    }

    private static string FactDomain(JObject fact)
    {
        string domain = ((string)fact["presentation"]?["domain"] ?? string.Empty).Trim().ToLowerInvariant();
        if (domain == "politics") return "politics";
        if (domain == "war") return "war";
        if (domain == "culture") return "people";
        if (domain == "economy") return "local";
        return string.Empty;
    }

    private static string FactDomainTitle(string domain)
    {
        switch (domain)
        {
            case "politics": return "政治与外交";
            case "war": return "战争与领地";
            case "people": return "人物近况";
            case "local": return "地方情况";
            default: return domain;
        }
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
        return BuildText(Build(records, nowDay), "近期动态", "近期没有记录。");
    }

    internal static string BuildText(JObject report, string heading, string emptyText)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(string.IsNullOrWhiteSpace(heading) ? "近期动态" : heading);
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
        if (!hasItems) builder.AppendLine(string.IsNullOrWhiteSpace(emptyText) ? "近期没有记录。" : emptyText);
        return builder.ToString().TrimEnd();
    }

    internal static string BuildTextFromFacts(
        IReadOnlyList<JObject> facts,
        string heading,
        string emptyText)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(string.IsNullOrWhiteSpace(heading) ? "近期动态" : heading);
        builder.AppendLine("────────────");
        bool hasItems = false;
        foreach (string domain in DomainOrder)
        {
            List<JObject> domainFacts = (facts ?? new List<JObject>())
                .Where(value => StringComparer.Ordinal.Equals(
                    NormalizeDomain((string)value["presentation"]?["domain"], (string)value["kind"]), domain))
                .OrderBy(value => (int?)value["occurred"]?["campaignDay"] ?? 0)
                .ThenBy(value => (long?)value["occurred"]?["timeSlot"] ?? 0L)
                .ThenBy(value => (string)value["factId"], StringComparer.Ordinal)
                .ToList();
            if (domainFacts.Count == 0) continue;
            hasItems = true;
            builder.AppendLine(DomainTitle(domain));
            foreach (JObject fact in domainFacts)
            {
                int day = (int?)fact["occurred"]?["campaignDay"] ?? 0;
                string summary = (string)fact["presentation"]?["summary"] ?? string.Empty;
                builder.AppendLine("· 第 " + day + " 天：" + summary);
            }
        }
        if (!hasItems) builder.AppendLine(string.IsNullOrWhiteSpace(emptyText) ? "近期没有记录。" : emptyText);
        return builder.ToString().TrimEnd();
    }

    internal static string InferDomain(string kind)
    {
        string value = (kind ?? string.Empty).ToLowerInvariant();
        if (value == "war_declared" || value == "peace_made") return "politics";
        if (value == "settlement_owner_changed") return "war";
        if (value == "hero_killed" || value == "hero_prisoner_released") return "culture";
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
            case "politics": return "政治与外交";
            case "economy": return "地方情况";
            case "culture": return "人物近况";
            case "war": return "战争与领地";
            default: return domain;
        }
    }

    private static bool ContainsAny(string value, params string[] terms) => terms.Any(value.Contains);
}
