using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public static class AuthoringEditorProjection
{
    private static readonly IReadOnlyDictionary<string, string> ProfileHelp = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["profile.villager"] = "普通村民，主要接触本地生计、传闻和传统。",
        ["profile.townsfolk"] = "城镇居民，通常接触城镇公共消息和市场信息。",
        ["profile.commoner"] = "普通平民，通常只知道本地常识、传闻和浅层消息。",
        ["profile.headman"] = "村庄或城镇头人，较了解本地行政和本国公共信息。",
        ["profile.merchant"] = "商人，较了解商品、商路、债务和跨地消息。",
        ["profile.tavernkeeper"] = "酒馆老板，接触多来源传闻，但消息可信度不一。",
        ["profile.ransom_broker"] = "赎金经纪人，较了解俘虏、赎金和战争后果。",
        ["profile.soldier"] = "士兵，较了解本国战争事务和军务常识。",
        ["profile.noble"] = "贵族，通常接触贵族政治、家族和领地信息。",
        ["profile.noble_high_steward"] = "高管理贵族，政治和经济细节掌握得更多。"
    };

    public static JsonObject Project(AuthoringDocumentFile file, RegistrySnapshot registries, ValidationReport registryReport, TaxonomyCatalog taxonomy)
    {
        var document = file.Document;
        var report = new JsonArray();
        foreach (var diagnostic in file.Report.Diagnostics.Concat(registryReport.Diagnostics)) report.Add(DiagnosticNode(diagnostic));
        var model = new JsonObject
        {
            ["documentId"] = Text(document["id"]),
            ["title"] = Localized(document["title"]),
            ["domain"] = Text(document["domain"]),
            ["subdomain"] = Text(document["subdomain"]),
            ["relatedDomains"] = Clone(document["related_domains"]),
            ["universe"] = Text(document["universe"]),
            ["status"] = Text(document["status"]),
            ["contentTier"] = Text(document["content_tier"]),
            ["summary"] = Localized(document["summary"]),
            ["era"] = ProjectEra(document["era"]?.AsObject()),
            ["sourceMode"] = SourceMode(document),
            ["editable"] = SourceMode(document) != "source" && SourceMode(document) != "conflict",
            ["assertions"] = ProjectAssertions(document["assertions"]?.AsArray(), registries),
            ["advanced"] = ProjectAdvanced(document)
        };

        return new JsonObject
        {
            ["path"] = file.Path,
            ["format"] = file.Format,
            ["sourceHash"] = file.Report.InputHash,
            ["revision"] = IntegerValue(document["revision"]),
            ["registry"] = RegistryBinding(registries),
            ["taxonomyVersion"] = taxonomy.Version,
            ["taxonomyHash"] = taxonomy.Hash,
            ["model"] = model,
            ["diagnostics"] = report,
            ["advancedAvailable"] = true
        };
    }

    internal static JsonObject BuildCatalog(RegistrySnapshot registries, ValidationReport report, EntityCatalogSnapshot? entityCatalog, TaxonomyCatalog taxonomy)
    {
        var profiles = new JsonArray();
        foreach (var profile in registries.ProfileObjects.Values.OrderBy(x => Text(x["id"]), StringComparer.Ordinal))
        {
            var id = Text(profile["id"]) ?? "";
            profiles.Add(new JsonObject
            {
                ["value"] = id,
                ["id"] = id,
                ["label"] = Localized(profile["display"]),
                ["display"] = Clone(profile["display"]),
                ["help"] = ProfileHelp.TryGetValue(id, out var help) ? help : InheritedHelp(profile, registries),
                ["inherits"] = Text(profile["inherits"]),
                ["valid"] = true,
                ["severity"] = "info"
            });
        }

        var referrals = new JsonArray();
        var referralObjects = registries.ReferralRegistry["referrals"]?.AsArray()
            .OfType<JsonObject>()
            .OrderBy(x => Text(x["id"]), StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<JsonObject>();
        foreach (var referral in referralObjects)
        {
            referrals.Add(new JsonObject
            {
                ["value"] = Text(referral["id"]),
                ["id"] = Text(referral["id"]),
                ["label"] = Localized(referral["display"]),
                ["display"] = Clone(referral["display"]),
                ["help"] = ReferralHelp(Text(referral["id"])),
                ["publiclyAskable"] = referral["publicly_askable"]?.GetValue<bool>() == true,
                ["valid"] = true,
                ["severity"] = "info"
            });
        }

        entityCatalog ??= EntityCatalogSnapshot.Unavailable([]);
        return new JsonObject
        {
            ["valid"] = report.Valid,
            ["diagnostics"] = new JsonArray(report.Diagnostics.Select(DiagnosticNode).ToArray()),
            ["profiles"] = profiles,
            ["referrals"] = referrals,
            ["profileRegistryVersion"] = registries.ProfileVersion,
            ["profileRegistryHash"] = registries.ProfileHash,
            ["referralRegistryVersion"] = registries.ReferralVersion,
            ["referralRegistryHash"] = registries.ReferralHash,
            ["profile_registry_version"] = registries.ProfileVersion,
            ["profile_registry_hash"] = registries.ProfileHash,
            ["referral_registry_version"] = registries.ReferralVersion,
            ["referral_registry_hash"] = registries.ReferralHash,
            ["taxonomyVersion"] = taxonomy.Version,
            ["taxonomyHash"] = taxonomy.Hash,
            ["domains"] = Clone(taxonomy.ToCatalogJson()["domains"]),
            ["statuses"] = Options(("needs_review", "待复核", "内容尚未完成人工复核"), ("reference_only", "仅作参考", "不进入正式正典"), ("accepted_variant", "接受的变体", "与正典并存的已接受版本"), ("rumor", "流言", "世界内流传但未必可靠的说法"), ("future", "未来内容", "当前时间线尚未发生的内容"), ("canon", "正典", "已完成审核并可作为权威知识"), ("rejected", "已否决", "不应进入运行时知识")),
            ["eraPresets"] = Options(("current", "当前世界", "当前时间线已经成立或正在发生"), ("historical", "历史时期", "发生在当前之前的历史阶段"), ("before_event", "某事件之前", "只适用于指定事件发生前"), ("after_event", "某事件之后", "只适用于指定事件发生后"), ("persistent", "长期存在", "跨越多个时期持续存在"), ("unknown", "时间未确定", "暂时无法确定具体时期"), ("custom", "自定义时期", "自行填写时期名称或年份")),
            ["certaintyLevels"] = Options(("exact", "确定", "时间或边界有明确依据"), ("bounded", "范围确定", "知道大致起止范围"), ("approximate", "大致确定", "只有近似时期"), ("unknown", "时间未确定", "目前无法可靠判断")),
            ["assertionKinds"] = Options(("fact", "客观事实", "记录世界中实际发生或存在的事情"), ("interpretation", "解释", "对事实的理解或判断"), ("rumor", "传闻", "正在流传但可靠性有限的消息"), ("relation", "关系", "人物、势力或地点之间的关系"), ("state", "状态", "某个时期内成立的状况")),
            ["expressionLayers"] = Options(("rumor", "传闻", "含糊、口耳相传的说法"), ("summary", "摘要", "普通 NPC 可以说出的简短内容"), ("detail", "详细", "知识面较广者可以说明的内容"), ("secret", "秘密", "只有特定身份或条件才能说出的内容")),
            ["scopes"] = Options(("local", "本地", "村庄、城镇或附近地区"), ("regional", "区域", "一个地区或若干相邻地点"), ("national", "全国", "本国范围内的公共知识"), ("faction", "阵营", "某个势力或组织内部"), ("elite", "贵族圈", "上层社会或管理者圈子"), ("private", "私人", "只属于个人或极少数人")),
            ["detailLevels"] = Options(("rumor", "传闻", "只能说出模糊传闻"), ("summary", "摘要", "只能说出简要内容"), ("detail", "详细", "可以说明较完整细节"), ("secret", "秘密", "可以触及秘密层内容")),
            ["contentTiers"] = Options(("base", "基础内容", "普通世界知识"), ("adult_optional", "成人拓展", "需要额外确认的可选内容")),
            ["entityCatalogAvailable"] = entityCatalog.Available,
            ["entityCatalog"] = entityCatalog.ToCatalogJson()
        };
    }

    public static JsonObject Merge(JsonObject original, JsonObject model, RegistrySnapshot registries, TaxonomyCatalog taxonomy)
    {
        var candidate = Clone(original)!.AsObject();
        var documentId = Text(original["id"]) ?? throw new InvalidOperationException("WB-EDITOR-DOC-409: 当前档案缺少稳定 ID。");
        if (!string.Equals(Text(model["documentId"]), documentId, StringComparison.Ordinal))
            throw new InvalidOperationException("WB-EDITOR-DOC-409: 作者模式不能修改档案内部 ID。");

        var domain = Text(model["domain"]) ?? Text(original["domain"]);
        if (!taxonomy.TryGetDomain(domain, out _))
            throw new InvalidOperationException("WB-EDITOR-DOMAIN-400: 知识分类无效。");
        if (!documentId.StartsWith($"doc.{domain}.", StringComparison.Ordinal))
            throw new InvalidOperationException("WB-EDITOR-DOMAIN-409: 知识分类不能与档案内部 ID 冲突，请新建对应分类的档案。");

        EnsureSame(model, "universe", original["universe"], "WB-EDITOR-ADVANCED-403: 世界观由高级信息维护。");
        SetLocalized(candidate, "title", Text(model["title"]));
        candidate["domain"] = domain;
        ApplyTaxonomyFields(candidate, model);
        var taxonomyReport = new ValidationReport();
        taxonomy.ValidateDocument(candidate, taxonomyReport);
        if (!taxonomyReport.Valid)
        {
            var first = taxonomyReport.Diagnostics.FirstOrDefault(x => x.Severity == "error");
            throw new InvalidOperationException($"WB-EDITOR-TAXONOMY-422: {first?.Message ?? "分类信息无效。"}");
        }
        candidate["status"] = Text(model["status"]) ?? Text(original["status"]);
        candidate["content_tier"] = Text(model["contentTier"]) ?? Text(original["content_tier"]);
        SetLocalized(candidate, "summary", Text(model["summary"]));
        ApplyEra(candidate, model["era"]?.AsObject());
        candidate["assertions"] = MergeAssertions(documentId, original["assertions"]?.AsArray(), model["assertions"]?.AsArray(), registries);

        EnsureNoSourceConflicts(original);
        ApplyRevisionAndReview(original, candidate);
        EnsureSourceObjectsUnchanged(original, candidate);
        return candidate;
    }

    private static void ApplyTaxonomyFields(JsonObject candidate, JsonObject model)
    {
        var subdomain = Text(model["subdomain"]);
        if (string.IsNullOrWhiteSpace(subdomain)) candidate.Remove("subdomain");
        else candidate["subdomain"] = subdomain;

        var related = model["relatedDomains"] as JsonArray ?? model["related_domains"] as JsonArray;
        if (related is null || related.Count == 0)
        {
            candidate.Remove("related_domains");
            return;
        }

        candidate["related_domains"] = new JsonArray(related.OfType<JsonValue>()
            .Select(value => Text(value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => JsonValue.Create(value) as JsonNode)
            .ToArray()!);
    }

    private static JsonArray MergeAssertions(string documentId, JsonArray? originalArray, JsonArray? modelArray, RegistrySnapshot registries)
    {
        var originals = (originalArray ?? new JsonArray()).OfType<JsonObject>().ToDictionary(x => Text(x["id"]) ?? "", StringComparer.Ordinal);
        var result = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in modelArray ?? [])
        {
            if (node is not JsonObject model) continue;
            var id = ResolveStableId(model, "assertion", documentId);
            if (!seen.Add(id)) throw new InvalidOperationException("WB-EDITOR-ID-409: 同一档案中存在重复客观事实。");
            originals.TryGetValue(id, out var original);
            var merged = original is null ? NewAssertion(id) : Clone(original)!.AsObject();
            merged["id"] = id;
            merged["kind"] = Text(model["kind"]) ?? Text(merged["kind"]) ?? "fact";
            SetLocalized(merged, "text", Text(model["text"]));
            merged["expressions"] = MergeExpressions(id, original?["expressions"]?.AsArray(), model["expressions"]?.AsArray(), registries);
            if (original is not null) ApplyRevisionAndReview(original, merged);
            result.Add(merged);
        }

        foreach (var original in originals.Values)
        {
            var id = Text(original["id"]);
            if (id is not null && !seen.Contains(id) && SourceMode(original) == "source")
                throw new InvalidOperationException("WB-EDITOR-SOURCE-403: 来源型事实不能在作者模式删除，请进入高级模式维护。");
        }
        return result;
    }

    private static JsonArray MergeExpressions(string assertionId, JsonArray? originalArray, JsonArray? modelArray, RegistrySnapshot registries)
    {
        var originals = (originalArray ?? new JsonArray()).OfType<JsonObject>().ToDictionary(x => Text(x["id"]) ?? "", StringComparer.Ordinal);
        var result = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in modelArray ?? [])
        {
            if (node is not JsonObject model) continue;
            var id = ResolveStableId(model, "expr", assertionId);
            if (!seen.Add(id)) throw new InvalidOperationException("WB-EDITOR-ID-409: 同一事实中存在重复 NPC 表达。");
            originals.TryGetValue(id, out var original);
            var merged = original is null ? NewExpression(id) : Clone(original)!.AsObject();
            merged["id"] = id;
            merged["layer"] = Text(model["layer"]) ?? Text(merged["layer"]) ?? "summary";
            SetLocalized(merged, "text", Text(model["text"]));
            merged["grants"] = MergeRules(model["grants"]?.AsArray(), original?["grants"]?.AsArray(), registries, "grants");
            merged["denies"] = MergeRules(model["denies"]?.AsArray(), original?["denies"]?.AsArray(), registries, "denies");
            var fallbackInput = model.ContainsKey("fallbackReferrals") ? model["fallbackReferrals"] : model["fallback_referral_ids"];
            var fallback = MergeFallbackReferrals(fallbackInput, original?["fallback_referral_ids"]?.AsArray(), registries);
            if (fallback.Count == 0 && original?["fallback_referral_ids"] is null)
                merged.Remove("fallback_referral_ids");
            else
                merged["fallback_referral_ids"] = fallback;
            if (original is not null) ApplyRevisionAndReview(original, merged);
            result.Add(merged);
        }

        foreach (var original in originals.Values)
        {
            var id = Text(original["id"]);
            if (id is not null && !seen.Contains(id) && SourceMode(original) == "source")
                throw new InvalidOperationException("WB-EDITOR-SOURCE-403: 来源型表达不能在作者模式删除，请进入高级模式维护。");
        }
        return result;
    }

    private static void EnsureSame(JsonObject model, string key, JsonNode? original, string message)
    {
        if (!JsonEqual(original, model[key])) throw new InvalidOperationException(message);
    }

    private static void SetLocalized(JsonObject target, string field, string? text)
    {
        if (text is null) return;
        var localized = target[field] is JsonObject existing ? Clone(existing)!.AsObject() : new JsonObject();
        localized["zh-CN"] = text;
        target[field] = localized;
    }

    private static void ApplyEra(JsonObject target, JsonObject? modelEra)
    {
        if (modelEra is null) return;
        var era = target["era"] is JsonObject existing ? Clone(existing)!.AsObject() : new JsonObject();
        var key = Text(modelEra["key"]) ?? Text(modelEra["preset"]) ?? Text(era["key"]);
        if (!string.IsNullOrWhiteSpace(key)) era["key"] = key;
        var certainty = Text(modelEra["certainty"]);
        if (!string.IsNullOrWhiteSpace(certainty)) era["certainty"] = certainty;
        ApplyNullableInteger(era, modelEra, "start_year", "startYear");
        ApplyNullableInteger(era, modelEra, "end_year", "endYear");
        target["era"] = era;
    }

    private static void ApplyNullableInteger(JsonObject target, JsonObject model, string targetKey, string modelKey)
    {
        if (!model.ContainsKey(modelKey)) return;
        if (model[modelKey] is null) target.Remove(targetKey);
        else target[targetKey] = IntegerValue(model[modelKey]);
    }

    private static string ResolveStableId(JsonObject model, string kind, string parentId)
    {
        var explicitId = Text(model["id"]);
        var prefix = kind == "assertion" ? "assertion" : "expr";
        if (!string.IsNullOrWhiteSpace(explicitId))
        {
            if (explicitId.StartsWith(prefix + ".", StringComparison.Ordinal)) return explicitId;
            throw new InvalidOperationException($"WB-EDITOR-ID-400: {prefix} 的内部 ID 不能修改。新卡片请使用稳定 clientKey。");
        }

        var clientKey = Text(model["clientKey"]);
        if (string.IsNullOrWhiteSpace(clientKey) || !System.Text.RegularExpressions.Regex.IsMatch(clientKey, "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new InvalidOperationException($"WB-EDITOR-ID-400: 新建 {prefix} 缺少有效的稳定 clientKey。");

        var digest = Hashing.Sha256Text($"{kind}|{parentId}|{clientKey}").ToLowerInvariant()[..16];
        return $"{prefix}.editor.{digest}";
    }

    private static JsonObject NewAssertion(string id)
        => new()
        {
            ["id"] = id,
            ["revision"] = 1,
            ["kind"] = "fact",
            ["text"] = new JsonObject { ["zh-CN"] = "" },
            ["author_created"] = NewAuthorCreated("作者模式新增客观事实。"),
            ["expressions"] = new JsonArray()
        };

    private static JsonObject NewExpression(string id)
        => new()
        {
            ["id"] = id,
            ["revision"] = 1,
            ["layer"] = "summary",
            ["text"] = new JsonObject { ["zh-CN"] = "" },
            ["author_created"] = NewAuthorCreated("作者模式新增 NPC 表达。"),
            ["grants"] = new JsonArray(),
            ["denies"] = new JsonArray()
        };

    private static JsonObject NewAuthorCreated(string reason)
        => new()
        {
            ["author_id"] = "author.developer",
            ["reason"] = new JsonObject { ["zh-CN"] = reason },
            ["review_status"] = "draft"
        };

    private static JsonArray MergeRules(JsonArray? modelArray, JsonArray? originalArray, RegistrySnapshot registries, string ruleKind)
    {
        var originals = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var original in originalArray?.OfType<JsonObject>() ?? []) originals[RuleBindingKey(original)] = original;

        var result = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in modelArray ?? [])
        {
            if (node is not JsonObject model) continue;
            var bindingKey = Text(model["key"]);
            originals.TryGetValue(bindingKey ?? "", out var original);
            var merged = original is null ? new JsonObject() : Clone(original)!.AsObject();

            var profileId = Text(model["profileId"]) ?? Text(model["profile_id"]) ?? Text(merged["profile_id"]);
            var scope = Text(model["scope"]) ?? Text(merged["scope"]);
            var minDetail = Text(model["minDetail"]) ?? Text(model["min_detail"]) ?? Text(merged["min_detail"]);
            if (profileId is null || !registries.Profiles.Contains(profileId))
                throw new InvalidOperationException($"WB-EDITOR-RULE-400: {ruleKind} 中的身份不存在：{profileId ?? "未填写"}。");
            if (scope is not ("local" or "regional" or "national" or "faction" or "elite" or "private"))
                throw new InvalidOperationException($"WB-EDITOR-RULE-400: {ruleKind} 的知道范围无效：{scope ?? "未填写"}。");
            if (minDetail is not ("rumor" or "summary" or "detail" or "secret"))
                throw new InvalidOperationException($"WB-EDITOR-RULE-400: {ruleKind} 的最低表达详细度无效：{minDetail ?? "未填写"}。");

            merged["profile_id"] = profileId;
            merged["scope"] = scope;
            merged["min_detail"] = minDetail;
            ApplyRuleConditions(merged, model);

            var contentKey = RuleBindingKey(merged);
            if (!seen.Add(contentKey)) throw new InvalidOperationException($"WB-EDITOR-RULE-409: {ruleKind} 中存在重复身份权限。");
            result.Add(merged);
        }
        return result;
    }

    private static void ApplyRuleConditions(JsonObject target, JsonObject model)
    {
        if (model.ContainsKey("conditions"))
        {
            if (model["conditions"] is not JsonObject conditions)
                throw new InvalidOperationException("WB-EDITOR-RULE-400: 权限条件必须是对象。");
            foreach (var key in RuleConditionKeys) target.Remove(key);
            foreach (var property in conditions)
            {
                if (!RuleConditionKeys.Contains(property.Key, StringComparer.Ordinal))
                    throw new InvalidOperationException($"WB-EDITOR-RULE-400: 权限条件字段不受作者模式支持：{property.Key}。");
                target[property.Key] = Clone(property.Value);
            }
            return;
        }

        foreach (var key in RuleConditionKeys)
            if (model.ContainsKey(key)) target[key] = Clone(model[key]);
    }

    private static JsonArray MergeFallbackReferrals(JsonNode? modelNode, JsonArray? originalArray, RegistrySnapshot registries)
    {
        if (modelNode is null) return Clone(originalArray) as JsonArray ?? new JsonArray();
        var result = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (modelNode is not JsonArray values) throw new InvalidOperationException("WB-EDITOR-REFERRAL-400: 推荐询问对象必须是列表。");
        foreach (var value in values)
        {
            var id = value is JsonObject item ? Text(item["id"]) ?? Text(item["referralId"]) : Text(value);
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!registries.Referrals.Contains(id)) throw new InvalidOperationException($"WB-EDITOR-REFERRAL-400: 推荐询问对象不存在：{id}。");
            if (seen.Add(id)) result.Add(id);
        }
        return result;
    }

    private static void ApplyRevisionAndReview(JsonObject original, JsonObject candidate)
    {
        if (SemanticEqualWithoutRevisionAndReview(original, candidate))
        {
            candidate["revision"] = IntegerValue(original["revision"]) ?? 1;
            if (original["author_created"] is null) candidate.Remove("author_created");
            else candidate["author_created"] = Clone(original["author_created"]);
            return;
        }

        candidate["revision"] = (IntegerValue(original["revision"]) ?? 1) + 1;
        if (original["author_created"] is JsonObject author)
        {
            var updated = Clone(author)!.AsObject();
            updated["review_status"] = "draft";
            updated.Remove("review_event_id");
            candidate["author_created"] = updated;
        }
    }

    private static void EnsureNoSourceConflicts(JsonObject document)
    {
        EnsureNoSourceConflict(document, "档案");
        foreach (var assertion in document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            EnsureNoSourceConflict(assertion, $"事实 {Text(assertion["id"])}");
            foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
                EnsureNoSourceConflict(expression, $"表达 {Text(expression["id"])}");
        }
    }

    private static void EnsureNoSourceConflict(JsonObject value, string label)
    {
        if (SourceMode(value) == "conflict") throw new InvalidOperationException($"WB-EDITOR-SOURCE-409: {label} 同时存在来源和原创标记，请进入高级模式修复。");
    }

    private static void EnsureSourceObjectsUnchanged(JsonObject original, JsonObject candidate)
    {
        if (SourceMode(original) == "source" && !JsonEqual(original, candidate))
            throw new InvalidOperationException("WB-EDITOR-SOURCE-403: 来源型档案不能在作者模式修改，请进入高级模式维护。");

        var candidateAssertions = candidate["assertions"]?.AsArray().OfType<JsonObject>().ToDictionary(x => Text(x["id"]) ?? "", StringComparer.Ordinal) ?? new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var originalAssertion in original["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var assertionId = Text(originalAssertion["id"]);
            if (assertionId is null || !candidateAssertions.TryGetValue(assertionId, out var candidateAssertion)) continue;
            if (SourceMode(originalAssertion) == "source" && !JsonEqual(originalAssertion, candidateAssertion))
                throw new InvalidOperationException($"WB-EDITOR-SOURCE-403: 来源型事实不能在作者模式修改：{assertionId}。");

            var candidateExpressions = candidateAssertion["expressions"]?.AsArray().OfType<JsonObject>().ToDictionary(x => Text(x["id"]) ?? "", StringComparer.Ordinal) ?? new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            foreach (var originalExpression in originalAssertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var expressionId = Text(originalExpression["id"]);
                if (expressionId is null || !candidateExpressions.TryGetValue(expressionId, out var candidateExpression)) continue;
                if (SourceMode(originalExpression) == "source" && !JsonEqual(originalExpression, candidateExpression))
                    throw new InvalidOperationException($"WB-EDITOR-SOURCE-403: 来源型 NPC 表达不能在作者模式修改：{expressionId}。");
            }
        }
    }

    private static string RuleBindingKey(JsonObject rule)
        => Hashing.Sha256Text(CanonicalJson.Serialize(rule)).Substring(0, 16);

    private static bool SemanticEqualWithoutRevisionAndReview(JsonObject left, JsonObject right)
    {
        var leftValue = Clone(left)!.AsObject();
        var rightValue = Clone(right)!.AsObject();
        leftValue.Remove("revision");
        rightValue.Remove("revision");
        leftValue.Remove("author_created");
        rightValue.Remove("author_created");
        return JsonEqual(leftValue, rightValue);
    }

    private static bool JsonEqual(JsonNode? left, JsonNode? right)
        => left is null && right is null
            || left is not null && right is not null && CanonicalJson.Serialize(left) == CanonicalJson.Serialize(right);

    private static readonly string[] RuleConditionKeys =
    [
        "culture_ids", "kingdom_ids", "settlement_ids", "role_ids", "clan_ids", "is_female", "is_clan_leader",
        "min_age", "max_age", "min_management", "min_steward", "min_skill"
    ];

    private static JsonArray ProjectAssertions(JsonArray? assertions, RegistrySnapshot registries)
    {
        var result = new JsonArray();
        foreach (var assertion in assertions?.OfType<JsonObject>() ?? [])
        {
            var sourceMode = SourceMode(assertion);
            result.Add(new JsonObject
            {
                ["id"] = Text(assertion["id"]),
                ["revision"] = IntegerValue(assertion["revision"]),
                ["kind"] = Text(assertion["kind"]),
                ["text"] = Localized(assertion["text"]),
                ["sourceMode"] = sourceMode,
                ["editable"] = sourceMode == "author_created",
                ["sourceCount"] = assertion["sources"]?.AsArray().Count ?? 0,
                ["expressions"] = ProjectExpressions(assertion["expressions"]?.AsArray(), registries)
            });
        }
        return result;
    }

    private static JsonArray ProjectExpressions(JsonArray? expressions, RegistrySnapshot registries)
    {
        var result = new JsonArray();
        foreach (var expression in expressions?.OfType<JsonObject>() ?? [])
        {
            var sourceMode = SourceMode(expression);
            result.Add(new JsonObject
            {
                ["id"] = Text(expression["id"]),
                ["revision"] = IntegerValue(expression["revision"]),
                ["layer"] = Text(expression["layer"]),
                ["text"] = Localized(expression["text"]),
                ["sourceMode"] = sourceMode,
                ["editable"] = sourceMode == "author_created",
                ["sourceCount"] = expression["sources"]?.AsArray().Count ?? 0,
                ["grants"] = ProjectRules(expression["grants"]?.AsArray(), registries),
                ["denies"] = ProjectRules(expression["denies"]?.AsArray(), registries),
                ["fallbackReferrals"] = ProjectReferralOptions(expression["fallback_referral_ids"]?.AsArray(), registries)
            });
        }
        return result;
    }

    private static JsonArray ProjectRules(JsonArray? rules, RegistrySnapshot registries)
    {
        var result = new JsonArray();
        foreach (var rule in rules?.OfType<JsonObject>() ?? [])
        {
            var profileId = Text(rule["profile_id"]);
            var profile = profileId is not null && registries.ProfileObjects.TryGetValue(profileId, out var found) ? found : null;
            result.Add(new JsonObject
            {
                ["key"] = Hashing.Sha256Text(CanonicalJson.Serialize(rule)).Substring(0, 16),
                ["profileId"] = profileId,
                ["profileLabel"] = profile is null ? "未登记身份" : Localized(profile["display"]),
                ["profileValid"] = profile is not null,
                ["scope"] = Text(rule["scope"]),
                ["minDetail"] = Text(rule["min_detail"]),
                ["conditions"] = ProjectConditions(rule),
                ["referralIds"] = Clone(rule["referral_ids"] ?? rule["fallback_referral_ids"])
            });
        }
        return result;
    }

    private static JsonArray ProjectReferralOptions(JsonArray? ids, RegistrySnapshot registries)
    {
        var result = new JsonArray();
        var referrals = registries.ReferralRegistry["referrals"]?.AsArray().OfType<JsonObject>().ToDictionary(x => Text(x["id"]) ?? "", StringComparer.Ordinal) ?? new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var idNode in ids ?? [])
        {
            var id = Text(idNode);
            if (id is null) continue;
            referrals.TryGetValue(id, out var referral);
            result.Add(new JsonObject
            {
                ["id"] = id,
                ["label"] = referral is null ? "未登记推荐对象" : Localized(referral["display"]),
                ["valid"] = referral is not null,
                ["publiclyAskable"] = referral?["publicly_askable"]?.GetValue<bool>() == true
            });
        }
        return result;
    }

    private static JsonObject ProjectConditions(JsonObject rule)
    {
        var conditions = new JsonObject();
        foreach (var key in new[] { "culture_ids", "kingdom_ids", "settlement_ids", "role_ids", "is_female", "is_clan_leader", "min_age", "max_age", "min_management", "min_steward", "min_skill" })
        {
            if (rule[key] is not null) conditions[key] = Clone(rule[key]);
        }
        return conditions;
    }

    private static JsonObject ProjectEra(JsonObject? era)
    {
        var key = Text(era?["key"]) ?? "unknown";
        return new JsonObject
        {
            ["preset"] = key,
            ["label"] = key switch
            {
                "current" => "当前世界",
                "historical" => "历史时期",
                "before_event" => "某事件之前",
                "after_event" => "某事件之后",
                "persistent" => "长期存在",
                "unknown" => "时间未确定",
                _ => "自定义时期"
            },
            ["key"] = key,
            ["startYear"] = IntegerValue(era?["start_year"]),
            ["endYear"] = IntegerValue(era?["end_year"]),
            ["certainty"] = Text(era?["certainty"]) ?? "unknown"
        };
    }

    private static JsonObject ProjectAdvanced(JsonObject document)
    {
        var fields = new JsonArray();
        foreach (var key in new[] { "universe", "registry_bindings", "authority", "entity_ids", "aliases", "redirects", "lifecycle", "sources", "author_created" })
            if (document[key] is not null) fields.Add(key);
        return new JsonObject
        {
            ["readOnly"] = true,
            ["fields"] = fields,
            ["authority"] = Clone(document["authority"]),
            ["registryBindings"] = Clone(document["registry_bindings"]),
            ["universe"] = Text(document["universe"]),
            ["entityIds"] = Clone(document["entity_ids"]),
            ["aliases"] = Clone(document["aliases"]),
            ["redirects"] = Clone(document["redirects"]),
            ["lifecycle"] = Clone(document["lifecycle"])
        };
    }

    private static JsonObject RegistryBinding(RegistrySnapshot registries)
        => new()
        {
            ["profileVersion"] = registries.ProfileVersion,
            ["profileHash"] = registries.ProfileHash,
            ["referralVersion"] = registries.ReferralVersion,
            ["referralHash"] = registries.ReferralHash
        };

    private static JsonArray Options(params (string Value, string Label, string Help)[] options)
        => new(options.Select(x => (JsonNode)new JsonObject { ["value"] = x.Value, ["label"] = x.Label, ["help"] = x.Help }).ToArray());

    private static string InheritedHelp(JsonObject profile, RegistrySnapshot registries)
    {
        var parent = Text(profile["inherits"]);
        return parent is not null && registries.ProfileObjects.TryGetValue(parent, out var parentProfile)
            ? $"继承：{Localized(parentProfile["display"])}。"
            : "身份注册表中的可用 NPC 身份。";
    }

    private static string ReferralHelp(string? id) => id switch
    {
        "referral.notary_merchant" => "可以询问公证、契约和部分公共知识。",
        "referral.ransom_broker" => "可以询问俘虏、赎金和战争后果。",
        "referral.tavernkeeper" => "可以询问多来源传闻和城镇消息。",
        "referral.headman" => "可以询问村庄或城镇的本地及本国知识。",
        _ => "注册表中的公开询问对象。"
    };

    private static JsonObject DiagnosticNode(Diagnostic diagnostic)
        => new()
        {
            ["code"] = diagnostic.Code,
            ["severity"] = diagnostic.Severity,
            ["message"] = diagnostic.Message,
            ["path"] = diagnostic.Path,
            ["detail"] = diagnostic.Detail
        };

    private static string SourceMode(JsonObject document)
        => document["sources"] is not null && document["author_created"] is not null
            ? "conflict"
            : document["sources"] is not null ? "source"
            : document["author_created"] is not null ? "author_created"
            : "missing";

    private static string? Localized(JsonNode? node)
    {
        if (node is not JsonObject value) return Text(node);
        return Text(value["zh-CN"]) ?? Text(value["zh"]) ?? Text(value["en"]) ?? value.Select(x => Text(x.Value)).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string? Text(JsonNode? node) => node?.GetValue<string>();

    private static int? IntegerValue(JsonNode? node)
    {
        if (node is null) return null;
        if (node is JsonValue value && value.TryGetValue<int>(out var integer)) return integer;
        return checked((int)node.GetValue<long>());
    }

    private static JsonNode? Clone(JsonNode? node) => node is null ? null : JsonNode.Parse(node.ToJsonString());
}

