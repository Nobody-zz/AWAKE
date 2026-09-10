using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class WorldKnowledgeLoader
{
    internal static WorldKnowledgeSnapshot Load(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath)) throw new InvalidOperationException("WB2-MANIFEST-MISSING");
        string root = Path.GetDirectoryName(manifestPath) ?? ".";
        JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
        string schemaVersion = Str(manifest, "schemaVersion");
        if (!StringComparer.Ordinal.Equals(schemaVersion, "awake.worldbook.v2")) throw new InvalidOperationException("WB2-SCHEMA-UNSUPPORTED:" + schemaVersion);
        JObject entrypoints = manifest["entrypoints"] as JObject;
        string runtimeRelative = Str(entrypoints, "runtime");
        string runtimePath = RequireChildPath(root, runtimeRelative);
        JObject runtime = JObject.Parse(File.ReadAllText(runtimePath));
        if (!StringComparer.Ordinal.Equals(Str(runtime, "schemaVersion"), "awake.worldbook.v2")) throw new InvalidOperationException("WB2-SCHEMA-UNSUPPORTED:runtime");

        return BuildSnapshot(runtime);
    }

    internal static WorldKnowledgeSnapshot LoadVerified(WorldbookVerifiedPackage package)
    {
        if (package == null || package.Runtime == null) throw new InvalidOperationException("WB2-MANIFEST-MISSING");
        return BuildSnapshot(package.Runtime);
    }

    private static WorldKnowledgeSnapshot BuildSnapshot(JObject runtime)
    {
        var snapshot = new WorldKnowledgeSnapshot
        {
            SchemaVersion = Str(runtime, "schemaVersion"),
            PackageId = Str(runtime, "packageId"),
            Version = Str(runtime, "version"),
            WorldId = Str(runtime, "worldId"),
            ContentTier = NormalizeContentTier(Str(runtime["extensions"] as JObject, "contentTier")),
            Revision = runtime["revision"]?.Value<int>() ?? 1
        };
        LoadEntries(snapshot, runtime["entries"] as JArray);
        LoadIdentities(snapshot, runtime["identities"] as JArray);
        LoadReferrals(snapshot, runtime["referrals"] as JArray);
        BuildKeywordIndex(snapshot);
        if (snapshot.Entries.Count == 0) snapshot.Warnings.Add("WB2-EMPTY-ENTRIES");
        return snapshot;
    }

    private static void LoadEntries(WorldKnowledgeSnapshot snapshot, JArray entries)
    {
        if (entries == null) return;
        foreach (JObject value in entries)
        {
            string id = Str(value, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var entry = new WorldKnowledgeEntry
            {
                Id = id,
                Domain = Str(value, "domain"),
                Title = Localized(value["title"] as JObject),
                Summary = Localized(value["summary"] as JObject)
            };
            foreach (JToken token in (value["keywords"] as JArray) ?? new JArray())
            {
                string keyword = token.Value<string>();
                if (!string.IsNullOrWhiteSpace(keyword)) entry.Keywords.Add(keyword);
            }
            JArray expressions = value["expressions"] as JArray;
            if (expressions != null) foreach (JObject expressionValue in expressions.Children<JObject>())
            {
                var expression = new WorldKnowledgeExpression
                {
                    Id = Str(expressionValue, "id"),
                    Detail = DefaultValue(Str(expressionValue, "detail"), "rumor"),
                    Text = Localized(expressionValue["text"] as JObject),
                    Enabled = expressionValue["enabled"] == null || expressionValue["enabled"].Value<bool>()
                };
                LoadRules(expression.Grants, expressionValue["grants"] as JArray);
                LoadRules(expression.Denies, expressionValue["denies"] as JArray);
                entry.Expressions.Add(expression);
            }
            snapshot.Entries[id] = entry;
        }
    }

    private static void LoadIdentities(WorldKnowledgeSnapshot snapshot, JArray identities)
    {
        if (identities == null) return;
        foreach (JObject value in identities)
        {
            string id = Str(value, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var identity = new WorldKnowledgeIdentity { Id = id, DisplayName = Localized(value["displayName"] as JObject), BasePriority = value["basePriority"]?.Value<int>() ?? 0 };
            AddStrings(identity.Parents, value["parents"] as JArray);
            AddStrings(identity.ReferralTargetIds, value["referralTargetIds"] as JArray);
            snapshot.Identities[id] = identity;
        }
    }

    private static void LoadReferrals(WorldKnowledgeSnapshot snapshot, JArray referrals)
    {
        if (referrals == null) return;
        foreach (JObject value in referrals)
        {
            string id = Str(value, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            snapshot.Referrals[id] = new WorldKnowledgeReferral { Id = id, DisplayName = Localized(value["displayName"] as JObject), Reason = Localized(value["reason"] as JObject), Priority = value["priority"]?.Value<int>() ?? 0, PubliclyAskable = value["publiclyAskable"]?.Value<bool>() ?? false };
        }
    }

    private static void LoadRules(List<WorldKnowledgeRule> target, JArray rules)
    {
        if (rules == null) return;
        foreach (JObject value in rules)
        {
            var rule = new WorldKnowledgeRule
            {
                IdentityId = Str(value, "identity_id"),
                Scope = DefaultValue(Str(value, "scope"), "local"),
                MinDetail = DefaultValue(Str(value, "min_detail"), "rumor"),
                Conditions = LoadCondition(value["conditions"] as JObject)
            };
            AddStrings(rule.ReferralIds, value["referral_ids"] as JArray);
            target.Add(rule);
        }
    }

    private static WorldKnowledgeCondition LoadCondition(JObject value)
    {
        if (value == null) return null;
        var condition = new WorldKnowledgeCondition
        {
            IsFemale = NullableBool(value, "is_female"),
            IsClanLeader = NullableBool(value, "is_clan_leader"),
            MinAge = NullableInt(value, "min_age"),
            MaxAge = NullableInt(value, "max_age"),
            MinManagement = NullableInt(value, "min_management")
        };
        AddStrings(condition.IdentityIds, value["identity_ids"] as JArray);
        AddStrings(condition.CultureIds, value["culture_ids"] as JArray);
        AddStrings(condition.KingdomIds, value["kingdom_ids"] as JArray);
        AddStrings(condition.SettlementIds, value["settlement_ids"] as JArray);
        AddStrings(condition.RoleIds, value["role_ids"] as JArray);
        JObject skills = value["min_skill"] as JObject;
        if (skills != null)
        {
            foreach (JProperty property in skills.Properties())
            {
                int level;
                if (int.TryParse(property.Value.ToString(), out level) && level >= 0)
                    condition.MinSkills[property.Name] = level;
            }
        }
        return condition;
    }

    private static void BuildKeywordIndex(WorldKnowledgeSnapshot snapshot)
    {
        foreach (WorldKnowledgeEntry entry in snapshot.Entries.Values)
        {
            foreach (string keyword in entry.Keywords)
            {
                if (!snapshot.KeywordIndex.TryGetValue(keyword, out List<string> ids)) snapshot.KeywordIndex[keyword] = ids = new List<string>();
                if (!ids.Any(x => StringComparer.Ordinal.Equals(x, entry.Id))) ids.Add(entry.Id);
            }
        }
    }

    private static string RequireChildPath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidOperationException("WB2-PATH-ESCAPE");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("WB2-PATH-ESCAPE");
        if (!File.Exists(full)) throw new FileNotFoundException("WB2-MANIFEST-MISSING", full);
        return full;
    }

    private static void AddStrings(List<string> target, JArray values)
    {
        if (values == null) return;
        foreach (JToken value in values)
        {
            string text = value.Value<string>();
            if (!string.IsNullOrWhiteSpace(text) && !target.Any(x => StringComparer.Ordinal.Equals(x, text))) target.Add(text);
        }
    }

    private static string Str(JObject value, string name) => value?[name]?.Value<string>() ?? string.Empty;
    private static string DefaultValue(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string NormalizeContentTier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || StringComparer.OrdinalIgnoreCase.Equals(value, "base") || StringComparer.OrdinalIgnoreCase.Equals(value, "pure")) return "base";
        if (StringComparer.OrdinalIgnoreCase.Equals(value, "adult") || StringComparer.OrdinalIgnoreCase.Equals(value, "adult_optional")) return "adult_optional";
        return value.Trim().ToLowerInvariant();
    }
    private static bool? NullableBool(JObject value, string name) => value?[name]?.Type == JTokenType.Boolean ? value[name].Value<bool>() : (bool?)null;
    private static int? NullableInt(JObject value, string name) => value?[name]?.Type == JTokenType.Integer ? value[name].Value<int>() : (int?)null;
    private static string Localized(JObject value) => value?["zh-CN"]?.Value<string>() ?? value?["zh"]?.Value<string>() ?? value?["en"]?.Value<string>() ?? value?.Properties().FirstOrDefault()?.Value?.Value<string>() ?? string.Empty;
}
