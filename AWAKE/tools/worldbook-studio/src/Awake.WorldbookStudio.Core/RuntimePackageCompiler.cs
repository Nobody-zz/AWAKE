using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public sealed record RuntimePackageCompilation(JsonObject Runtime, JsonObject Index, JsonObject Manifest, string ManifestHash, string ContentHash, string PackageHash);

public static class RuntimePackageCompiler
{
    private const int MaxAmbiguityDiagnostics = 20;
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoEntityAnchorNames =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public static RuntimePackageCompilation Build(ValidatedSnapshot snapshot, IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents, string contentTier)
    {
        // 包身份＝**常量**，不从文档推导（2026-09-14 定，见 RUNTIME-MAPPING-CONTRACT〈身份定名〉）。
        // 早先 packageId/worldId 由 first["universe"] 推出 —— 那是 schema 的「来源宇宙」枚举
        // （awake_current/bannerlord_1084/warband_future/ck3_mod/unknown），属**溯源分类**，不是世界身份；
        // displayName 又取 first["title"] ⇒ 随首篇文档改名而漂。AWAKE 只装一个世界，故写死。
        // ⚠️ 拼法受契约约束（2026-09-15 纠）：`package_id` 的 pattern 是**两段**（`ns:name`），
        //    只允许一个冒号 ⇒ 不能写 `awake:worldbook:calradia`。三段形只属于 `stable_id`（worldId 走这条）。
        //    故按**先行的** `awake:pilot.worldbook` 惯例拼成 `awake:worldbook.calradia`（ns=awake，name=worldbook.calradia）。
        const string packageId = "awake:worldbook.calradia";
        const string worldId = "awake:world:calradia";
        var entries = new JsonArray();
        var entityAnchorNames = ResolveEntityAnchorNames(snapshot);
        foreach (var item in documents.OrderBy(x => x.Document["id"]?.GetValue<string>(), StringComparer.Ordinal))
        {
            entries.Add(BuildEntry(item.Document, snapshot.Registries, entityAnchorNames));
        }

        var identities = new JsonArray();
        foreach (var profile in snapshot.Registries.ProfileObjects.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var value = profile.Value;
            identities.Add(new JsonObject
            {
                ["id"] = StableIdentity(profile.Key),
                ["displayName"] = Clone(value["display"] as JsonObject ?? new JsonObject { ["zh-CN"] = profile.Key }),
                ["basePriority"] = 0,
                ["parents"] = value["inherits"] is JsonValue ? new JsonArray(StableIdentity(value["inherits"]!.GetValue<string>())) : new JsonArray(),
                ["referralTargetIds"] = new JsonArray()
            });
        }

        var referrals = new JsonArray();
        foreach (var referral in snapshot.Registries.ReferralRegistry["referrals"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var id = referral["id"]?.GetValue<string>() ?? "referral.unknown";
            referrals.Add(new JsonObject
            {
                ["id"] = StableReferral(id),
                ["displayName"] = Clone(referral["display"] as JsonObject ?? new JsonObject { ["zh-CN"] = id }),
                ["reason"] = new JsonObject { ["zh-CN"] = "可公开询问的知识面广人物。" },
                ["priority"] = 0,
                ["publiclyAskable"] = snapshot.Registries.PubliclyAskableReferrals.Contains(id)
            });
        }

        var keywordIndex = new JsonObject();
        var domainIndex = new JsonObject();
        foreach (var entry in entries.OfType<JsonObject>())
        {
            var entryId = entry["id"]!.GetValue<string>();
            foreach (var keyword in entry["keywords"]!.AsArray().Select(x => x!.GetValue<string>()).Distinct(StringComparer.Ordinal))
                AddIndex(keywordIndex, keyword, entryId);
            AddIndex(domainIndex, entry["domain"]!.GetValue<string>(), entryId);
        }
        ReportKeywordAmbiguity(snapshot.Report, entries, keywordIndex);

        var runtime = new JsonObject
        {
            ["schemaVersion"] = "awake.worldbook.v2",
            ["packageId"] = packageId,
            ["version"] = "1.0.0",
            ["worldId"] = worldId,
            ["revision"] = 1,
            ["entries"] = entries,
            ["identities"] = identities,
            ["referrals"] = referrals,
            ["indexes"] = new JsonObject { ["keywordToEntryIds"] = keywordIndex, ["domainToEntryIds"] = domainIndex },
            ["extensions"] = new JsonObject { ["contentTier"] = contentTier, ["source"] = "awake.worldbook.studio" }
        };
        var index = new JsonObject
        {
            ["schemaVersion"] = "awake.worldbook.index.v1",
            ["entryIds"] = new JsonArray(entries.OfType<JsonObject>().Select(x => JsonValue.Create(x["id"]!.GetValue<string>())).ToArray()),
            ["keywordToEntryIds"] = Clone(keywordIndex),
            ["domainToEntryIds"] = Clone(domainIndex)
        };
        var contentHash = ContractHashing.ContentHash(new[] { ("runtime.json", (JsonNode)runtime), ("index.json", index) });
        var manifest = new JsonObject
        {
            ["schemaVersion"] = "awake.worldbook.v2",
            ["packageId"] = packageId,
            ["version"] = "1.0.0",
            ["kind"] = "universe",
            ["displayName"] = new JsonObject { ["zh-CN"] = "卡拉迪亚", ["en"] = "Calradia" },
            ["worldId"] = worldId,
            ["entrypoints"] = new JsonObject { ["runtime"] = "runtime.json", ["index"] = "index.json" },
            ["hashes"] = new JsonObject()
        };
        var manifestHash = ContractHashing.ManifestHash(manifest);
        var packageHash = ContractHashing.PackageHash(manifestHash, contentHash);
        manifest["hashes"] = new JsonObject { ["manifestHash"] = manifestHash, ["contentHash"] = contentHash, ["packageHash"] = packageHash };
        return new RuntimePackageCompilation(runtime, index, manifest, manifestHash, contentHash, packageHash);
    }

    private static JsonObject BuildEntry(JsonObject document, RegistrySnapshot registries, IReadOnlyDictionary<string, IReadOnlyList<string>> entityAnchorNames)
    {
        var sourceId = document["id"]?.GetValue<string>() ?? "doc.unknown";
        var entryId = StableEntry(sourceId);
        // K1：keywords 是运行时的可读检索词，来源为「标题（多语言）+ 别名（多语言）+ 实体锚点的可读名称」。
        // 内部文档 id 保留为兜底检索词并排在最后：它不是玩家/NPC 会用的说法，不应再充当主要关键词。
        var keywords = new JsonArray();
        var addedKeywords = new HashSet<string>(StringComparer.Ordinal);
        AddLocalizedKeywords(keywords, addedKeywords, document["title"] as JsonObject);
        AddLocalizedKeywords(keywords, addedKeywords, document["aliases"] as JsonObject);
        foreach (var entityId in ReadEntityIds(document))
            foreach (var anchorName in LookupEntityAnchorNames(entityAnchorNames, entityId))
                AddKeyword(keywords, addedKeywords, anchorName);
        AddKeyword(keywords, addedKeywords, sourceId);
        var expressions = new JsonArray();
        foreach (var assertion in document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var expressionId = StableExpression(expression["id"]?.GetValue<string>() ?? assertion["id"]?.GetValue<string>() ?? sourceId);
                expressions.Add(new JsonObject
                {
                    ["id"] = expressionId,
                    ["detail"] = MapDetail(expression["layer"]?.GetValue<string>()),
                    ["text"] = Clone(expression["text"] as JsonObject ?? new JsonObject { ["zh-CN"] = "" }),
                    ["grants"] = ConvertRules(expression["grants"]?.AsArray(), expression["fallback_referral_ids"]?.AsArray(), registries),
                    ["denies"] = ConvertRules(expression["denies"]?.AsArray()),
                    ["extensions"] = new JsonObject { ["sourceAssertionId"] = assertion["id"]?.GetValue<string>() }
                });
            }
        }
        if (expressions.Count == 0)
            expressions.Add(new JsonObject { ["id"] = $"awake:expression:{Sanitize(sourceId)}", ["detail"] = "summary", ["text"] = Clone(document["summary"] as JsonObject ?? new JsonObject { ["zh-CN"] = "" }), ["grants"] = new JsonArray(), ["denies"] = new JsonArray() });

        var entryExtensions = new JsonObject { ["sourceDocumentId"] = sourceId };
        if (document["subdomain"] is JsonValue subdomain && subdomain.TryGetValue<string>(out var subdomainValue) && !string.IsNullOrWhiteSpace(subdomainValue))
            entryExtensions["subdomain"] = subdomainValue;
        if (document["related_domains"] is JsonArray relatedDomains && relatedDomains.Count > 0)
            entryExtensions["relatedDomains"] = Clone(relatedDomains);
        if (document["entity_ids"] is JsonArray entityIds && entityIds.Count > 0)
        {
            var entityRefs = entityIds
                .Select(value => value?.GetValue<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => CanonicalEntityRef(value!))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (entityRefs.Length > 0)
                entryExtensions["entityRefs"] = new JsonArray(entityRefs.Select(value => JsonValue.Create(value) as JsonNode).ToArray()!);
        }

        var domain = document["domain"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(domain))
            throw new InvalidOperationException("WB-DOC-002: 知识档案缺少主分类，不能编译运行包。");

        return new JsonObject
        {
            ["id"] = entryId,
            ["domain"] = domain,
            ["title"] = Clone(document["title"] as JsonObject ?? new JsonObject { ["zh-CN"] = sourceId }),
            ["summary"] = Clone(document["summary"] as JsonObject ?? new JsonObject { ["zh-CN"] = "" }),
            ["keywords"] = keywords,
            ["expressions"] = expressions,
            ["extensions"] = entryExtensions
        };
    }

    private static JsonArray ConvertRules(JsonArray? rules, JsonArray? fallbackReferralIds = null, RegistrySnapshot? registries = null)
    {
        var result = new JsonArray();
        foreach (var rule in rules?.OfType<JsonObject>() ?? [])
        {
            var profile = rule["profile_id"]?.GetValue<string>() ?? "profile.anonymous";
            var converted = new JsonObject
            {
                ["identity_id"] = StableIdentity(profile),
                ["scope"] = rule["scope"]?.GetValue<string>() ?? "local",
                ["min_detail"] = MapDetail(rule["min_detail"]?.GetValue<string>()),
                ["conditions"] = ConvertConditions(rule)
            };
            if (fallbackReferralIds is not null && fallbackReferralIds.Count > 0)
            {
                var referralIds = new JsonArray();
                foreach (var referral in fallbackReferralIds.Select(x => x?.GetValue<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
                {
                    if (registries is null || !registries.Referrals.Contains(referral!))
                        throw new InvalidOperationException($"Unknown referral target: {referral}");
                    if (!registries.PubliclyAskableReferrals.Contains(referral!))
                        throw new InvalidOperationException($"Referral target is not publicly askable: {referral}");
                    referralIds.Add(StableReferral(referral!));
                }
                if (referralIds.Count > 0) converted["referral_ids"] = referralIds;
            }
            result.Add(converted);
        }
        return result;
    }

    private static JsonObject ConvertConditions(JsonObject rule)
    {
        var conditions = new JsonObject();
        CopyStringArray(rule, conditions, "culture_ids");
        CopyStringArray(rule, conditions, "kingdom_ids");
        CopyStringArray(rule, conditions, "settlement_ids");
        CopyStringArray(rule, conditions, "role_ids");
        CopyBoolean(rule, conditions, "is_female");
        CopyBoolean(rule, conditions, "is_clan_leader");
        if (TryGetInt(rule["min_age"], out var age)) conditions["min_age"] = age;
        if (TryGetInt(rule["max_age"], out var max)) conditions["max_age"] = max;
        var managementThreshold = 0;
        var hasManagementThreshold = TryGetInt(rule["min_management"], out managementThreshold);
        if (TryGetInt(rule["min_steward"], out var steward))
        {
            managementThreshold = hasManagementThreshold ? Math.Max(managementThreshold, steward) : steward;
            hasManagementThreshold = true;
        }
        if (hasManagementThreshold) conditions["min_management"] = managementThreshold;
        if (rule["min_skill"] is JsonObject minSkill) conditions["min_skill"] = Clone(minSkill);
        return conditions;
    }

    private static void CopyStringArray(JsonObject source, JsonObject target, string key)
    {
        if (source[key] is not JsonArray values || values.Count == 0) return;
        target[key] = new JsonArray(values.Select(value => JsonValue.Create(MapConditionId(key, value?.GetValue<string>() ?? string.Empty))).ToArray());
    }

    private static string MapConditionId(string field, string value)
    {
        string kind = field switch
        {
            "culture_ids" => "culture",
            "kingdom_ids" => "kingdom",
            "settlement_ids" => "settlement",
            "role_ids" => "role",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(kind)) return value;
        return CanonicalConditionId(kind, value);
    }

    private static string CanonicalEntityRef(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (!normalized.StartsWith("entity.", StringComparison.Ordinal))
            throw new InvalidOperationException($"WB-DOC-003: entity_ids 必须以 entity.<kind>.<code> 形式给出：{value}");
        var parts = normalized.Split('.', 3);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
            throw new InvalidOperationException($"WB-DOC-003: entity_ids 格式无效：{value}");
        if (parts[1] is not ("hero" or "clan" or "settlement"))
            throw new InvalidOperationException($"WB-DOC-003: entity_ids 类型不受支持（仅 hero/clan/settlement）：{value}");
        return CanonicalConditionId(parts[1], normalized);
    }

    private static string CanonicalConditionId(string kind, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        string normalizedKind = kind.Trim().ToLowerInvariant();
        string normalizedValue = value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        string[] prefixes =
        {
            "awake:entity:" + normalizedKind + ":",
            "awake:" + normalizedKind + ":",
            "calradia:" + normalizedKind + ":",
            "entity." + normalizedKind + ".",
            normalizedKind + ":"
        };
        foreach (string prefix in prefixes)
        {
            if (normalizedValue.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalizedValue = normalizedValue.Substring(prefix.Length);
                break;
            }
        }
        if (normalizedValue.StartsWith("entity.", StringComparison.Ordinal)) throw new InvalidOperationException($"Condition ID type mismatch: {value} is not {kind}");
        return "awake:" + normalizedKind + ":" + normalizedValue;
    }

    private static void CopyBoolean(JsonObject source, JsonObject target, string key)
    {
        if (source[key] is JsonValue value && value.TryGetValue<bool>(out var boolean)) target[key] = boolean;
    }

    private static bool TryGetInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue) return false;
        if (jsonValue.TryGetValue<int>(out value)) return true;
        if (jsonValue.TryGetValue<long>(out var longValue) && longValue >= int.MinValue && longValue <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }
        return false;
    }

    private static void AddIndex(JsonObject index, string key, string entryId)
    {
        if (index[key] is not JsonArray values) index[key] = values = new JsonArray();
        if (!values.Any(x => x?.GetValue<string>() == entryId)) values.Add(entryId);
    }

    // K1：keywords 只收「可读检索词」。多语言对象的字符串值、以及字符串数组值都算一个检索词。
    private static void AddLocalizedKeywords(JsonArray keywords, HashSet<string> addedKeywords, JsonObject? localized)
    {
        if (localized is null) return;
        foreach (var property in localized)
        {
            if (property.Value is JsonArray values)
            {
                foreach (var value in values) AddKeywordNode(keywords, addedKeywords, value);
                continue;
            }
            AddKeywordNode(keywords, addedKeywords, property.Value);
        }
    }

    private static void AddKeywordNode(JsonArray keywords, HashSet<string> addedKeywords, JsonNode? node)
    {
        if (!TryReadText(node, out var text)) return;
        AddKeyword(keywords, addedKeywords, text);
    }

    private static void AddKeyword(JsonArray keywords, HashSet<string> addedKeywords, string text)
    {
        var value = text.Trim();
        if (value.Length == 0) return;
        if (!addedKeywords.Add(value)) return;
        keywords.Add(value);
    }

    private static bool TryReadText(JsonNode? node, out string text)
    {
        text = string.Empty;
        if (node is JsonValue value && value.TryGetValue<string>(out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            text = raw;
            return true;
        }
        return false;
    }

    private static IEnumerable<string> ReadEntityIds(JsonObject document)
        => (document["entity_ids"] as JsonArray)?
            .Select(value => value?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!) ?? [];

    // K1：实体锚点（entity_ids）的可读名称取自「文档 entity_refs 对应的映射目录」，
    // 候选为 schemaRoot\mappings\persona-entity（打包布局）与 schemaRoot\..\mappings\persona-entity（仓库开发布局）。
    // 指针、登记表缺失或解析失败时返回空表，keywords 退化为「标题 + 别名 + 内部 id」，不阻断编译。
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveEntityAnchorNames(ValidatedSnapshot snapshot)
    {
        try
        {
            var schemaRoot = TryResolveSchemaRoot(snapshot);
            if (string.IsNullOrWhiteSpace(schemaRoot)) return NoEntityAnchorNames;
            var registryPath = TryLocateEntityRegistry(schemaRoot!);
            if (registryPath is null) return NoEntityAnchorNames;
            var registry = JsonNode.Parse(File.ReadAllText(registryPath)) as JsonObject;
            var names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var entity in registry?["entities"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var entityId = entity["entity_id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(entityId)) continue;
                var values = new List<string>();
                AddReadableValue(values, entity["display_name_zh"]);
                AddReadableValue(values, entity["english_name"]);
                AddReadableValue(values, entity["family_name_zh"]);
                if (entity["aliases"] is JsonArray aliases)
                    foreach (var alias in aliases) AddReadableValue(values, alias);
                if (values.Count == 0) continue;
                names[entityId!.Trim().ToLowerInvariant()] = values.Distinct(StringComparer.Ordinal).ToArray();
            }
            return names;
        }
        catch (Exception)
        {
            return NoEntityAnchorNames;
        }
    }

    private static void AddReadableValue(List<string> values, JsonNode? node)
    {
        if (TryReadText(node, out var text)) values.Add(text.Trim());
    }

    private static IReadOnlyList<string> LookupEntityAnchorNames(IReadOnlyDictionary<string, IReadOnlyList<string>> entityAnchorNames, string entityId)
        => entityAnchorNames.TryGetValue(entityId.Trim().ToLowerInvariant(), out var names) ? names : [];

    private static string? TryResolveSchemaRoot(ValidatedSnapshot snapshot)
    {
        var schemaPaths = snapshot.InputClosure
            .Where(x => string.Equals(x.Category, "schema", StringComparison.Ordinal))
            .Select(x => x.Path)
            .ToArray();
        var anchor = schemaPaths.FirstOrDefault(path => string.Equals(Path.GetFileName(path), "awake.worldbook.authoring.v1.schema.json", StringComparison.OrdinalIgnoreCase));
        var source = anchor ?? schemaPaths.FirstOrDefault();
        return source is null ? null : Path.GetDirectoryName(source);
    }

    private static string? TryLocateEntityRegistry(string schemaRoot)
    {
        foreach (var catalogRoot in new[]
        {
            Path.Combine(schemaRoot, "mappings", "persona-entity"),
            Path.Combine(schemaRoot, "..", "mappings", "persona-entity")
        })
        {
            var pointerPath = Path.Combine(catalogRoot, "current-pointer.v1.json");
            if (!File.Exists(pointerPath)) continue;
            var generation = (JsonNode.Parse(File.ReadAllText(pointerPath)) as JsonObject)?["generation_relative_path"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(generation)) continue;
            var registryPath = Path.Combine(catalogRoot, generation!.Replace('/', Path.DirectorySeparatorChar), "entity-registry.v1.json");
            if (File.Exists(registryPath)) return registryPath;
        }
        return null;
    }

    // D1-2：一个关键词命中多条词条时不消歧，运行时会把同一说法指向多个词条。
    // 编译阶段把这类关键词作为诊断写进既有校验报告（validation.json），只告警不阻断。
    private static void ReportKeywordAmbiguity(ValidationReport report, JsonArray entries, JsonObject keywordIndex)
    {
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries.OfType<JsonObject>())
        {
            var id = entry["id"]?.GetValue<string>();
            if (id is not null) titles[id] = FirstLocalizedText(entry["title"] as JsonObject);
        }

        var ambiguous = keywordIndex
            .Select(pair => (
                Keyword: pair.Key,
                EntryIds: pair.Value?.AsArray().Select(x => x?.GetValue<string>()).Where(x => x is not null).Select(x => x!).ToArray() ?? []))
            .Where(x => x.EntryIds.Length > 1)
            .OrderBy(x => x.Keyword, StringComparer.Ordinal)
            .ToArray();
        if (ambiguous.Length == 0) return;

        foreach (var item in ambiguous.Take(MaxAmbiguityDiagnostics))
        {
            var hits = string.Join("、", item.EntryIds.Select(id =>
                titles.TryGetValue(id, out var title) && title.Length > 0 ? $"{id}（{title}）" : id));
            report.Warning(
                "WB-INDEX-AMBIGUOUS",
                $"关键词“{item.Keyword}”同时命中 {item.EntryIds.Length} 条词条，检索时会同时返回这些词条；请确认是否需要补充区分别名或调整词条边界。",
                null,
                "命中：" + hits);
        }
        if (ambiguous.Length > MaxAmbiguityDiagnostics)
            report.Warning(
                "WB-INDEX-AMBIGUOUS",
                $"另有 {ambiguous.Length - MaxAmbiguityDiagnostics} 个关键词同时命中多条词条，未逐条列出；这通常是多个词条共用同一名称（例如同一聚落与相关事件），检索时需要消歧。");
    }

    private static string FirstLocalizedText(JsonObject? localized)
    {
        if (localized is null) return string.Empty;
        foreach (var property in localized)
            if (TryReadText(property.Value, out var text)) return text;
        return string.Empty;
    }

    private static string MapDetail(string? value) => value switch
    {
        "secret" => "secret",
        "detail" => "detail",
        "summary" => "summary",
        "rumor" => "rumor",
        _ => "rumor"
    };

    private static string StableEntry(string value) => $"awake:entry:{Sanitize(value.Replace("doc.", "", StringComparison.Ordinal))}";
    private static string StableExpression(string value) => $"awake:expression:{Sanitize(value.Replace("expr.", "", StringComparison.Ordinal))}";
    private static string StableIdentity(string value) => $"awake:identity:{Sanitize(value.Replace("profile.", "", StringComparison.Ordinal))}";
    private static string StableReferral(string value) => $"awake:referral:{Sanitize(value.Replace("referral.", "", StringComparison.Ordinal))}";
    private static string Sanitize(string value) => new string(value.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.' ? ch : '_').ToArray()).Trim('_');
    private static JsonNode Clone(JsonNode node) => JsonNode.Parse(node.ToJsonString())!;
}
