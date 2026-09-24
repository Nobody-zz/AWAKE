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
            LoadLinks(entry, value["extensions"] as JObject);
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

    // 互引边（2026-09-20）：读 `entry.extensions.links`。
    // 边表缺失、字段缺失、条目是旧包编的 —— 一律只是「没有边」，不报错、不影响既有加载
    //（召回那侧对空 Links 的处理就是「不扩」，见 WorldKnowledgeQueryService.LinkExpand*）。
    private static void LoadLinks(WorldKnowledgeEntry entry, JObject extensions)
    {
        foreach (JObject value in (extensions?["links"] as JArray)?.Children<JObject>() ?? Enumerable.Empty<JObject>())
        {
            string to = Str(value, "to");
            if (string.IsNullOrWhiteSpace(to) || StringComparer.Ordinal.Equals(to, entry.Id)) continue;
            var link = new WorldKnowledgeLink
            {
                To = to,
                ViaName = Str(value, "viaName"),
                Strength = DefaultValue(Str(value, "strength"), "weak"),
                Bucket = DefaultValue(Str(value, "bucket"), "proper")
            };
            AddStrings(link.UsableAs, value["usableAs"] as JArray);
            // 同一目标可能由两个名字各连一条（正文里既叫了名也叫了别称）⇒ 只留先出现的那条。
            if (entry.Links.Any(x => StringComparer.Ordinal.Equals(x.To, to))) continue;
            entry.Links.Add(link);
        }
    }

    private static void LoadIdentities(WorldKnowledgeSnapshot snapshot, JArray identities)    {
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
        AddStrings(condition.ClanIds, value["clan_ids"] as JArray);
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
        WorldbookKeywordIndex.Build(snapshot);
        WorldbookTermIndex.Build(snapshot);
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

// 关键词索引的**唯一**建法（2026-09-17）。
// ⚠️ 与 term 索引同一个理由放在这里：`WorldKnowledgeLoader.BuildKeywordIndex`（加载时）与
//    `WorldKnowledgeQueryService.RebuildKeywordIndex`（动态条目/Overlay 时）是**两处平行实现**，
//    纪律「平行实现必须同源」⇒ 两边都只调用 `Build`，不各写一遍循环。
//
// 为什么建索引时要**剔掉**一部分关键词（红测 2026-09-17，见
// `docs/worldbook-migration/REDTEST-CHAIN-20260917.md` §5.5）：
//   检索是「双向子串」——`text` 含 `kw` 或 `kw` 含 `text` 都算命中。于是一个**覆盖上百条**的关键词
//   就是一把万能钥匙：输 `doc` 命中 448、`geography` 命中 408、`castle_village` 命中 131、`村` 命中 273。
//   这不是匹配规则的毛病，是**索引里有哪些键**的毛病。四条剔除规则：
//     ① 覆盖条目数超过 `MaxKeywordDocumentFrequency` —— 能被这么多条目共用的键，按定义不具区分度。
//        与 `WorldbookTermIndex.MaxTermDocumentFrequency` 同一条道理（那张表早就剔了 `城堡/村庄`）。
//     ② 前缀 `doc.` —— 内部文档 id。编译器 09-16 已决定不再写进包（`RuntimePackageCompiler.cs` 的 K1 修正），
//        但**出厂包还是旧编译器编的**，所以这层在运行时也拦一道。取用请走 `entry.Id`，不经过检索。
//     ③ 形状像内部标识（纯 ASCII 且含下划线，如 `castle_village_EN1_2` / `heavy_round_shield`）——
//        这是实体锚点的**原始 id**，不是 K1 说的「可读名称」，真人不会这么念。
//     ④ 把整个**泛问词**裹进去的键（如 `德里亚特·村庄`，2026-09-17 加）—— 防复发，见
//        `WrapsGenericCategoryWord` 的注释。当前全库 0 条命中 ⇒ 这是一道**空转的闸**。
// ⚠️ 剔除**不是**删数据：包里的 `keywords` 一个字不改，只是不进检索索引。要查一个键为什么没生效，
//    来这里；要改数据，去编译器的 K1。
internal static class WorldbookKeywordIndex
{
    // 覆盖条目数超过这个值的关键词不进索引（按数调，见验台 RETRIEVAL_* 行）。
    // 与 `WorldbookTermIndex.MaxTermDocumentFrequency` 取同一个值、同一个理由；改一个要问另一个。
    internal const int MaxKeywordDocumentFrequency = 40;

    // 泛问词（＝三条聚落「概念词条」`geography.settlement-types-*` 的中文入口词）。
    //
    // 为什么这张表是**写死的**，而不是从语料里算出来 —— 四种统计口径都实测过，全不成立
    // （量法见 `tools/_probe_b_generic_words_20260917.py`，包里 451 条）：
    //   ① 语料覆盖 > 40：只捞到 `城堡`（覆盖 69）；`村庄` 覆盖 12、`城镇` 覆盖 14，够不着阈值。
    //   ② 长度 2~3 字的 title：148 个 —— 把 `沙拉斯`/`吕卡隆`/`毛皮` 这些**真名**也算了进来，
    //      会剔掉 45 条正当关键词（`沙拉斯湾`、`吕卡隆·石山秃鹫`、`德里亚特·毛皮生计`…）。
    //   ③ 两者取交：只剩 `城堡`，回到 ① 的毛病。
    // ⇒ 「哪些词是类别词、不是名字」是**编辑判断**，不是统计量。改动概念词条时同步这张表。
    //
    // ⚠️ 表里为什么也收着**口语同义词**（村子/村落/城砦/堡垒/镇子/城市），而概念词条自己只认领主词：
    //    两件事的判据不同 ——
    //      · 概念词条把口语同义词写进 `aliases` ⇒ 它们会进关键词索引 ⇒ 问「村子」时主路命中概念词条
    //        ⇒ **兜底通道不再执行**，具体问题（`有大瀑布的村子是哪个？`）被挤掉（2026-09-17 实测）。
    //      · 但「口语同义词也是类别词」这件事本身没变：谁要是写一条 `某某城市` 当键，那个坑照样在。
    //    因为没有任何地方再声明这六个词，只能记在这张表里。**改一处要一起想另一处。**
    private static readonly string[] GenericCategoryWords =
        { "村庄", "村子", "村落", "城堡", "城砦", "堡垒", "城镇", "镇子", "城市" };

    internal static void Build(WorldKnowledgeSnapshot snapshot)
    {
        snapshot.KeywordIndex.Clear();
        var documentFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (WorldKnowledgeEntry entry in snapshot.Entries.Values)
            foreach (string keyword in DistinctKeywords(entry))
                documentFrequency[keyword] = documentFrequency.TryGetValue(keyword, out int count) ? count + 1 : 1;

        foreach (WorldKnowledgeEntry entry in snapshot.Entries.Values)
            foreach (string keyword in DistinctKeywords(entry))
            {
                if (IsExcluded(keyword, documentFrequency[keyword])) continue;
                if (!snapshot.KeywordIndex.TryGetValue(keyword, out List<string> ids)) snapshot.KeywordIndex[keyword] = ids = new List<string>();
                if (!ids.Any(x => StringComparer.Ordinal.Equals(x, entry.Id))) ids.Add(entry.Id);
            }
    }

    private static IEnumerable<string> DistinctKeywords(WorldKnowledgeEntry entry)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string keyword in entry.Keywords)
            if (!string.IsNullOrWhiteSpace(keyword) && seen.Add(keyword.Trim()))
                yield return keyword.Trim();
    }

    private static bool IsExcluded(string keyword, int documentFrequency)
    {
        if (documentFrequency > MaxKeywordDocumentFrequency) return true;
        if (keyword.StartsWith("doc.", StringComparison.OrdinalIgnoreCase)) return true;
        if (LooksLikeInternalIdentifier(keyword)) return true;
        return WrapsGenericCategoryWord(keyword);
    }

    // ④ 把整个泛问词裹进去的键 —— 2026-09-17 加，**防复发**。
    //
    // 为什么需要：检索是**双向子串**（`text` 含 `kw` 或 `kw` 含 `text` 都算命中）。于是一条
    // 手写的复合键 `德里亚特·村庄` 会吃掉「村庄」这句**短问句**——`kw.IndexOf("村庄") >= 0` 就命中，
    // 而它指向的是德里亚特，不是村庄。v12 的实测形态就是这个：272 个村庄的泛问全被这一条键劫走。
    // （A 项已把数据里那唯一一条复合词清掉了，所以这条规则**当前空转**——它管的是下一次。）
    //
    // 边界：**只打"裹住整个泛问词"这一种形状**。泛问词自己照旧进索引 —— 它是概念词条的名字，
    // 正是「村庄是什么」这句话该落的地方（`WorldbookTermIndex` 那张表里 `城堡` 仍按 df>40 剔，
    // 两者不冲突：那张表按**覆盖度**剔，这张表按**形状**剔）。
    // 变异检验（该剔 `德里亚特·村庄`/`厄尔凡尼亚·村庄`；该留 `拉文尼亚`/`Mecalovea Castle`/`村庄`）
    // 跑在验台里，见 `tools/worldbook-runtime-smoke/RetrievalProbeCases.cs` 的 KEYWORDGUARD 行。
    // `internal` 而不是 `private`：验台要直接调它去数真实语料里命中了哪些键
    // —— 那边自己再写一遍判据就是**平行实现**（本项目纪律不许）。
    internal static bool WrapsGenericCategoryWord(string keyword)
    {
        foreach (string word in GenericCategoryWords)
        {
            if (keyword.Length <= word.Length) continue;      // 泛问词自己放行（等长也放行）
            if (keyword.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    // 纯 ASCII ＋ 含下划线 ⇒ 原始 id 形状。判据窄到只打这一类，避免碰「Goods」「Husn Fulq」这些真可读名。
    private static bool LooksLikeInternalIdentifier(string keyword)
    {
        bool hasUnderscore = false;
        foreach (char ch in keyword)
        {
            if (ch == '_') { hasUnderscore = true; continue; }
            if (ch > 0x7E || ch < 0x20) return false;
        }
        return hasUnderscore;
    }
}

// 兜底 term 索引的**唯一**建法与切词法（2026-09-16）。
// ⚠️ 为什么放在一个类里：`WorldKnowledgeLoader.BuildKeywordIndex`（加载时）与
//    `WorldKnowledgeQueryService.RebuildKeywordIndex`（动态条目/Overlay 时）是**两处平行实现**，
//    本项目纪律「平行实现必须同源」⇒ 两边都只调用 `Build`，不各写一遍循环。
// ⚠️ 切词规则三个原则：
//    ① 查询与被检索文本**必须用同一个 `EnumerateTerms`** —— 否则查得到与查不到是两套口径。
//    ② 起点版本＝「去标点断句 → 每段原样（≥2 字）＋ 段内全部 2-gram」，**虚词暂不丢**（简单可测）。
//       词表来源＝标题＋综述；关键词**不进这张表**（它自己那条路径仍在，见 `FindCandidates`）。
//    ③ **高频 term 不进口**（见 `MaxTermDocumentFrequency`）。实测：`坐落` 覆盖 133 条、`的一` 118、
//       `城堡` 67 —— 这类 gram 谁都有，留着就是过匹配（「哪座城堡底下管着两个村子？」曾召回 67 条，
//       把正确的城堡压出前 1）。光调阈值拦不住它们（它们常常一次就贡献 2 个 gram）。
internal static class WorldbookTermIndex
{
    // 覆盖条目数超过这个值的 term 不进索引（= 全表 448 条的一小截）。按数调，见验台 RETRIEVAL_* 行。
    internal const int MaxTermDocumentFrequency = 40;

    internal static void Build(WorldKnowledgeSnapshot snapshot)
    {
        snapshot.FallbackTermIndex.Clear();
        var documentFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var perEntry = new List<KeyValuePair<string, HashSet<string>>>();
        foreach (WorldKnowledgeEntry entry in snapshot.Entries.Values)
        {
            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string term in EnumerateTerms(entry.Title)) terms.Add(term);
            foreach (string term in EnumerateTerms(entry.Summary)) terms.Add(term);
            foreach (string term in terms)
                documentFrequency[term] = documentFrequency.TryGetValue(term, out int count) ? count + 1 : 1;
            perEntry.Add(new KeyValuePair<string, HashSet<string>>(entry.Id, terms));
        }
        foreach (var pair in perEntry)
            foreach (string term in pair.Value)
                if (documentFrequency[term] <= MaxTermDocumentFrequency) Add(snapshot, term, pair.Key);
    }

    // 查询侧用：把一句玩家话切成 term 集合（与建表同一个切词器）。
    internal static HashSet<string> TermSet(string text)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string term in EnumerateTerms(text)) set.Add(term);
        return set;
    }

    // "这看起来像一个名字，而不是一句话"（2026-09-17）。
    // 判据＝**切成一段**且不超过 `maxLength` 个字。`斯特基亚`（4 字一段）算，
    // `有个半岛被三家抢过，谁说了算？`（两段）不算。
    // 用途见 `WorldKnowledgeQueryService.FindFallbackCandidates` 的 `requiredShared`：
    // 名字形状的查询必须共享 ≥2 个 term，否则一个错别字会捞回一批"碰巧含这两个字"的条目。
    internal static bool LooksLikeSingleShortToken(string text, int maxLength)
    {
        string only = null;
        foreach (string segment in SplitSegments(text ?? string.Empty))
        {
            if (only != null) return false;
            only = segment;
        }
        return only != null && only.Length <= maxLength;
    }

    internal static IEnumerable<string> EnumerateTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (string segment in SplitSegments(text))
        {
            if (segment.Length >= 2) yield return segment;
            // ⚠️ 2-gram **只对中文段切**（2026-09-17）。
            // 这张表存在的原因是「中文没有空格」；西文本来就按空格分词，再从 `geography` 里切出
            // `ge/og/gr/ra/…` 全是噪声 —— 红测里 `geography`/`economy`/`war`/`entry`/`Goods`/`HeadArmor`
            // 这些输入字面侧关键词通道明明已经零命中，却从这条腿又漏回来（报告 §五 缺口 5 的残留）。
            if (IsAsciiOnly(segment)) continue;
            for (int i = 0; i + 2 <= segment.Length; i++) yield return segment.Substring(i, 2);
        }
    }

    private static bool IsAsciiOnly(string segment)
    {
        foreach (char ch in segment) if (ch > 0x7F) return false;
        return true;
    }

    private static void Add(WorldKnowledgeSnapshot snapshot, string term, string id)
    {
        if (!snapshot.FallbackTermIndex.TryGetValue(term, out List<string> ids)) snapshot.FallbackTermIndex[term] = ids = new List<string>();
        if (!ids.Any(x => StringComparer.Ordinal.Equals(x, id))) ids.Add(id);
    }

    private static IEnumerable<string> SplitSegments(string text)
    {
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (!IsSeparator(text[i])) continue;
            if (i > start) yield return text.Substring(start, i - start);
            start = i + 1;
        }
        if (start < text.Length) yield return text.Substring(start);
    }

    private static bool IsSeparator(char ch) => char.IsPunctuation(ch) || char.IsWhiteSpace(ch) || char.IsSymbol(ch);
}
