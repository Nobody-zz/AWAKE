using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Awake;

internal sealed class WorldKnowledgeIdentityEvaluation
{
    internal HashSet<string> IdentityIds { get; } = new HashSet<string>(StringComparer.Ordinal);
    internal Dictionary<string, int> Scores { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
    internal string EffectiveScope { get; set; } = string.Empty;
    internal string EffectiveDetail { get; set; } = string.Empty;
    internal bool ScopeAvailable { get; set; }
    internal bool DetailAvailable { get; set; }
    internal bool CapabilitiesAvailable => ScopeAvailable && DetailAvailable;

    internal int ScoreFor(string identityId)
    {
        string normalized = WorldbookIdentityEvaluator.NormalizeIdentity(identityId);
        return Scores.TryGetValue(normalized, out int score) ? score : 0;
    }
}

internal static class WorldbookIdentityEvaluator
{
    internal static WorldKnowledgeIdentityEvaluation Evaluate(WorldbookQuery query, WorldKnowledgeSnapshot snapshot)
    {
        query = query ?? new WorldbookQuery();
        var result = new WorldKnowledgeIdentityEvaluation();
        result.EffectiveScope = NormalizeCapability(query.KnowledgeScope);
        result.EffectiveDetail = NormalizeCapability(query.EffectiveDetail);
        result.ScopeAvailable = query.KnowledgeScopeAvailable && ScopeRank(result.EffectiveScope) >= 0;
        result.DetailAvailable = query.EffectiveDetailAvailable && DetailRank(result.EffectiveDetail) >= 0;
        string explicitIdentity = NormalizeIdentity(query.IdentityId);
        bool hasKnownExplicitIdentity = !string.IsNullOrWhiteSpace(explicitIdentity) && snapshot.Identities.ContainsKey(explicitIdentity);
        if (hasKnownExplicitIdentity) AddIdentity(result, snapshot, explicitIdentity, 1000);

        string role = NormalizeRole(query.Role);
        if (!hasKnownExplicitIdentity && !string.IsNullOrWhiteSpace(role))
        {
            foreach (string identity in IdentitiesForRole(role, query.IsClanLeader))
                AddIdentity(result, snapshot, identity, 500);
        }
        if (IsNoble(result))
        {
            if (query.Age >= 45) AddIdentity(result, snapshot, "noble_mature", 560 + Math.Min(query.Age, 80));
            if (Management(query) >= 80) AddIdentity(result, snapshot, "noble_high_steward", 620 + Math.Min(Management(query), 120));
        }
        return result;
    }

    internal static bool Matches(WorldKnowledgeRule rule, WorldbookQuery query, WorldKnowledgeSnapshot snapshot, WorldKnowledgeIdentityEvaluation evaluation, out int score)
    {
        if (!MatchesIdentityAndConditions(rule, query, snapshot, evaluation, out score)) return false;
        if (StringComparer.Ordinal.Equals(NormalizeIdentity(rule.IdentityId), "awake:identity:public")) return true;
        return CapabilityMatches(rule, evaluation);
    }

    internal static bool MatchesIdentityAndConditions(WorldKnowledgeRule rule, WorldbookQuery query, WorldKnowledgeSnapshot snapshot, WorldKnowledgeIdentityEvaluation evaluation, out int score)
    {
        score = 0;
        if (rule == null) return false;
        string identityId = NormalizeIdentity(rule.IdentityId);
        if (!StringComparer.Ordinal.Equals(identityId, "awake:identity:public") && !evaluation.IdentityIds.Contains(identityId)) return false;
        score = StringComparer.Ordinal.Equals(identityId, "awake:identity:public") ? 1 : evaluation.ScoreFor(identityId);
        WorldKnowledgeCondition condition = rule.Conditions;
        if (condition == null) return true;
        if (condition.IdentityIds.Count > 0 && !condition.IdentityIds.Any(x => evaluation.IdentityIds.Contains(NormalizeIdentity(x)))) return false;
        if (condition.CultureIds.Count > 0 && !ContainsValue(condition.CultureIds, query.CultureId)) return false;
        if (condition.KingdomIds.Count > 0 && !ContainsValue(condition.KingdomIds, query.KingdomId)) return false;
        if (condition.SettlementIds.Count > 0 && !ContainsValue(condition.SettlementIds, query.SettlementId)) return false;
        if (condition.RoleIds.Count > 0 && !condition.RoleIds.Any(x => StringComparer.OrdinalIgnoreCase.Equals(NormalizeRole(x), NormalizeRole(query.Role)))) return false;
        if (condition.IsFemale.HasValue && (!query.IsFemale.HasValue || query.IsFemale.Value != condition.IsFemale.Value)) return false;
        if (condition.IsClanLeader.HasValue && query.IsClanLeader != condition.IsClanLeader.Value) return false;
        if (condition.MinAge.HasValue && (query.Age <= 0 || query.Age < condition.MinAge.Value)) return false;
        if (condition.MaxAge.HasValue && (query.Age <= 0 || query.Age > condition.MaxAge.Value)) return false;
        if (condition.MinManagement.HasValue && Management(query) < condition.MinManagement.Value) return false;
        foreach (KeyValuePair<string, int> required in condition.MinSkills)
        {
            if (!query.Skills.TryGetValue(required.Key, out int current) || current < required.Value) return false;
        }
        score += 10 * condition.IdentityIds.Count;
        score += 20 * condition.CultureIds.Count;
        score += 25 * condition.KingdomIds.Count;
        score += 30 * condition.SettlementIds.Count;
        score += 15 * condition.RoleIds.Count;
        score += condition.IsFemale.HasValue ? 5 : 0;
        score += condition.IsClanLeader.HasValue ? 10 : 0;
        score += condition.MinAge.HasValue || condition.MaxAge.HasValue ? 15 : 0;
        score += condition.MinManagement.HasValue ? 20 : 0;
        score += 10 * condition.MinSkills.Count;
        return true;
    }

    internal static bool CapabilityMatches(WorldKnowledgeRule rule, WorldKnowledgeIdentityEvaluation evaluation)
    {
        if (rule == null || evaluation == null) return false;
        if (StringComparer.Ordinal.Equals(NormalizeIdentity(rule.IdentityId), "awake:identity:public")) return true;
        if (!evaluation.CapabilitiesAvailable) return false;
        int requiredScope = ScopeRank(NormalizeCapability(rule.Scope));
        int requiredDetail = DetailRank(NormalizeCapability(rule.MinDetail));
        if (requiredScope < 0 || requiredDetail < 0) return false;
        return ScopeRank(evaluation.EffectiveScope) >= requiredScope
            && DetailRank(evaluation.EffectiveDetail) >= requiredDetail;
    }

    internal static string NormalizeIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string text = value.Trim();
        if (text.StartsWith("profile.", StringComparison.Ordinal)) text = text.Substring("profile.".Length);
        return text.StartsWith("awake:identity:", StringComparison.Ordinal) ? text : "awake:identity:" + text;
    }

    // 全仓唯一的角色名归一化实现（WorldbookIdentityCapabilityRules 直接调这里，不再各写一份）。
    internal static string NormalizeRole(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string role = value.Trim();
        int separator = role.LastIndexOf(':');
        if (separator >= 0) role = role.Substring(separator + 1);
        // 必须先拆驼峰再小写：游戏枚举给的是 RansomBroker，规则表与内容侧写的是 ransom_broker。
        role = SplitCamelCase(role).ToLowerInvariant();
        if (role.StartsWith("role_", StringComparison.Ordinal)) role = role.Substring(5);
        return role.Replace('-', '_').Replace(' ', '_');
    }

    // 只在「大写紧跟小写字母或数字」处插分隔符 ⇒ 纯小写字面量（villager）与全大写缩写（NPC）一字不改。
    internal static string SplitCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        var builder = new StringBuilder(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (i > 0 && char.IsUpper(current))
            {
                char previous = value[i - 1];
                if (char.IsLower(previous) || char.IsDigit(previous)) builder.Append('_');
            }
            builder.Append(current);
        }
        return builder.ToString();
    }

    private static IEnumerable<string> IdentitiesForRole(string role, bool clanLeader)
    {
        switch (role)
        {
            case "farmer":
            case "villager":
            case "commoner":
                yield return "commoner";
                break;
            case "headman":
            case "village_headman":
            case "town_headman":
                yield return "headman";
                yield return "commoner";
                break;
            case "merchant":
                yield return "merchant";
                yield return "commoner";
                break;
            case "tavernkeeper":
            case "tavern_keeper":
                yield return "tavernkeeper";
                yield return "merchant";
                yield return "commoner";
                break;
            case "ransom_broker":
            case "ransombroker":
                yield return "ransom_broker";
                yield return "merchant";
                yield return "commoner";
                break;
            case "soldier":
            case "trooper":
                yield return "soldier";
                yield return "commoner";
                break;
            case "noble":
            case "lord":
            case "clan_leader":
                yield return "noble";
                break;
            default:
                if (clanLeader) yield return "noble";
                else yield return role;
                break;
        }
    }

    private static void AddIdentity(WorldKnowledgeIdentityEvaluation result, WorldKnowledgeSnapshot snapshot, string identityId, int score)
    {
        string normalized = NormalizeIdentity(identityId);
        if (string.IsNullOrWhiteSpace(normalized)) return;
        if (!result.IdentityIds.Add(normalized))
        {
            if (result.Scores[normalized] < score) result.Scores[normalized] = score;
            return;
        }
        result.Scores[normalized] = score + (snapshot.Identities.TryGetValue(normalized, out WorldKnowledgeIdentity identity) ? identity.BasePriority : 0);
        if (snapshot.Identities.TryGetValue(normalized, out WorldKnowledgeIdentity value))
        {
            foreach (string parent in value.Parents) AddIdentity(result, snapshot, parent, Math.Max(score - 1, 1));
        }
    }

    private static bool IsNoble(WorldKnowledgeIdentityEvaluation evaluation) => evaluation.IdentityIds.Contains("awake:identity:noble") || evaluation.IdentityIds.Contains("awake:identity:noble_mature") || evaluation.IdentityIds.Contains("awake:identity:noble_high_steward");
    internal static int ScopeRank(string value) => NormalizeCapability(value) switch
    {
        "local" => 1,
        "regional" => 2,
        "national" => 3,
        "faction" => 4,
        "elite" => 5,
        "private" => 6,
        _ => -1
    };
    internal static int DetailRank(string value) => NormalizeCapability(value) switch
    {
        "rumor" => 1,
        "summary" => 2,
        "detail" => 3,
        "secret" => 4,
        _ => -1
    };
    internal static string NormalizeCapability(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
    private static int Management(WorldbookQuery query)
    {
        if (query.Skills.TryGetValue("management", out int management)) return management;
        if (query.Skills.TryGetValue("steward", out int steward)) return steward;
        return 0;
    }
    private static bool ContainsValue(List<string> values, string actual) => !string.IsNullOrWhiteSpace(actual) && values.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x, actual) || StringComparer.OrdinalIgnoreCase.Equals(LastSegment(x), LastSegment(actual)));
    private static string LastSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        int separator = value.LastIndexOf(':');
        return separator >= 0 ? value.Substring(separator + 1) : value;
    }
}
