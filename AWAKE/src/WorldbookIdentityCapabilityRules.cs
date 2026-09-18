using System;

namespace Awake;

internal sealed class WorldbookIdentityCapabilityProfile
{
    internal string ProfileId { get; set; } = "profile.anonymous";
    internal string Role { get; set; } = "unknown";
    internal string KnowledgeScope { get; set; } = string.Empty;
    internal bool KnowledgeScopeAvailable { get; set; }
    internal string EffectiveDetail { get; set; } = string.Empty;
    internal bool EffectiveDetailAvailable { get; set; }
}

internal static class WorldbookIdentityCapabilityRules
{
    internal static WorldbookIdentityCapabilityProfile Resolve(string role, bool isNoble, int age, int management)
    {
        string normalizedRole = WorldbookIdentityEvaluator.NormalizeRole(role);
        if (isNoble || normalizedRole == "noble" || normalizedRole == "lord")
        {
            return ResolveNoble(age, management);
        }

        switch (normalizedRole)
        {
            case "villager":
            case "farmer":
                return Available("profile.villager", "villager", "local", "rumor");
            case "commoner":
                return Available("profile.commoner", "commoner", "local", "rumor");
            case "townsfolk":
            case "wanderer":
            case "preacher":
                return Available("profile.townsfolk", normalizedRole, "regional", "summary");
            case "headman":
            case "village_headman":
            case "town_headman":
                return Available("profile.headman", "headman", "national", "detail");
            case "rural_notable":
            case "notable":
            case "arena_master": // 竞技场主／锦标赛主持：游戏里属城镇要人一类，与 notable 同档
                return Available("profile.notable", "notable", "regional", "detail");
            case "merchant":
            case "goods_trader":
            case "horse_trader":
            case "weaponsmith":
            case "armorer":
            case "artisan":
            case "blacksmith":
            case "shop_worker":
            case "ship_wright":
                return Available("profile.merchant", "merchant", "faction", "detail");
            case "tavernkeeper":
            case "tavern_game_host":
            case "tavern_wench":
            case "musician":
                return Available("profile.tavernkeeper", "tavernkeeper", "faction", "detail");
            case "ransom_broker":
                return Available("profile.ransom_broker", "ransom_broker", "faction", "detail");
            case "soldier":
            case "guard":
            case "prison_guard":
            case "caravan_guard":
            case "banner_bearer":
            case "mercenary":
                return Available("profile.soldier", "soldier", "national", "detail");
            case "gang_leader":
                return Available("profile.notable", "gang_leader", "regional", "detail");
            case "gangster":
                return Available("profile.townsfolk", "gangster", "regional", "summary");
            // 强盗：位置先留着。世界书侧补上 awake:identity:bandit 这个身份（并授予它的说法）之前，
            // 这里给到的仍是一条空手身份——拿不到知识，但不再是「匿名」，日志里能看出他是谁。
            case "bandit":
                return Available("profile.bandit", "bandit", "local", "rumor");
            default:
                return Unknown();
        }
    }

    private static WorldbookIdentityCapabilityProfile ResolveNoble(int age, int management)
    {
        int detailRank = age >= 45 ? 4 : age >= 25 ? 3 : age >= 18 ? 2 : 1;
        if (management >= 80) detailRank = Math.Min(4, detailRank + 1);
        return new WorldbookIdentityCapabilityProfile
        {
            ProfileId = "profile.noble",
            Role = "noble",
            KnowledgeScope = "elite",
            KnowledgeScopeAvailable = true,
            EffectiveDetail = DetailForRank(detailRank),
            EffectiveDetailAvailable = age > 0
        };
    }

    private static WorldbookIdentityCapabilityProfile Available(string profileId, string role, string scope, string detail)
    {
        return new WorldbookIdentityCapabilityProfile
        {
            ProfileId = profileId,
            Role = role,
            KnowledgeScope = scope,
            KnowledgeScopeAvailable = true,
            EffectiveDetail = detail,
            EffectiveDetailAvailable = true
        };
    }

    private static WorldbookIdentityCapabilityProfile Unknown()
    {
        return new WorldbookIdentityCapabilityProfile
        {
            ProfileId = "profile.anonymous",
            Role = "unknown",
            KnowledgeScope = string.Empty,
            KnowledgeScopeAvailable = false,
            EffectiveDetail = string.Empty,
            EffectiveDetailAvailable = false
        };
    }

    private static string DetailForRank(int rank)
    {
        switch (rank)
        {
            case 4: return "secret";
            case 3: return "detail";
            case 2: return "summary";
            default: return "rumor";
        }
    }

    // 角色名归一化只有一份实现，在 WorldbookIdentityEvaluator（此前两份副本必须手工保持同步）。
}
