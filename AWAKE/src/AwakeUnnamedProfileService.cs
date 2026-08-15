using System;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal static class AwakeUnnamedProfileService
{
    internal static string BuildIdentity(AwakeNpcTarget target)
    {
        if (target == null || target.IsHero) return string.Empty;
        string name = string.IsNullOrWhiteSpace(target.DisplayName) ? "路人" : target.DisplayName;
        string gender = target.IsFemale ? "女性" : "男性";
        string age = Math.Floor(target.Age).ToString("0");
        string culture = CultureLabel(target.CultureId);
        string role = RoleLabel(target.Character);
        return name + "，" + culture + role + "，" + gender + "，约" + age + "岁。";
    }

    internal static string BuildStateConstraint(AwakeNpcTarget target)
    {
        if (target == null) return "当前没有可用的角色状态。";
        if (target.IsHero) return string.Empty;
        return "你是无名角色，不是有名英雄；你只了解自己的身份、所处地点与日常见闻，不掌握领主账目、王国机密或超出身份的信息。";
    }

    internal static string RoleLabel(CharacterObject character)
    {
        if (character == null) return "路人";
        try
        {
            if (character.IsSoldier) return "士兵";
        }
        catch
        {
        }
        return RoleLabel(character.Occupation);
    }

    internal static string RoleLabel(Occupation occupation)
    {
        switch (occupation)
        {
            case Occupation.Soldier:
                return "士兵";
            case Occupation.Villager:
                return "村民";
            case Occupation.Townsfolk:
                return "城镇平民";
            case Occupation.Guard:
                return "守卫";
            case Occupation.Mercenary:
                return "雇佣兵";
            case Occupation.Merchant:
            case Occupation.GoodsTrader:
            case Occupation.HorseTrader:
                return "商贩";
            case Occupation.Tavernkeeper:
                return "酒馆老板";
            case Occupation.TavernWench:
                return "酒馆女侍";
            case Occupation.TavernGameHost:
                return "棋局主持";
            case Occupation.Blacksmith:
            case Occupation.Weaponsmith:
            case Occupation.Armorer:
            case Occupation.Artisan:
            case Occupation.ShopWorker:
                return "工匠";
            case Occupation.ArenaMaster:
                return "竞技场老板";
            case Occupation.RansomBroker:
                return "赎金经纪人";
            case Occupation.Musician:
                return "乐师";
            case Occupation.Headman:
            case Occupation.RuralNotable:
            case Occupation.GangLeader:
            case Occupation.Preacher:
                return "地方要人";
            default:
                return "路人";
        }
    }

    private static string CultureLabel(string cultureId)
    {
        if (string.IsNullOrWhiteSpace(cultureId)) return string.Empty;
        switch (cultureId.Trim().ToLowerInvariant())
        {
            case "empire":
                return "帝国";
            case "vlandia":
                return "瓦兰迪亚";
            case "sturgia":
                return "斯特吉亚";
            case "battania":
                return "巴旦尼亚";
            case "khuzait":
                return "库赛特";
            case "aserai":
                return "阿塞莱";
            default:
                return cultureId;
        }
    }
}
