using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal static class BannerlordWorldbookIdentityAdapter
{
    // 同一「认不出的身份」只记一次，避免每次对话刷日志；只用于排查，不参与任何判定。
    private static readonly HashSet<string> UnmappedLogged = new HashSet<string>(StringComparer.Ordinal);
    private static readonly object UnmappedLock = new object();

    internal static void Apply(
        WorldbookQuery query,
        AwakeNpcTarget target,
        string fallbackRole,
        int fallbackAge,
        IDictionary<string, int> skills)
    {
        if (query == null) return;
        query.CultureId = WorldbookEntityId.Canonical("culture", query.CultureId);
        query.KingdomId = WorldbookEntityId.Canonical("kingdom", query.KingdomId);
        query.SettlementId = WorldbookEntityId.Canonical("settlement", query.SettlementId);
        int management = ResolveManagement(skills);
        WorldbookIdentityCapabilityProfile profile;

        if (target?.Hero != null)
        {
            Hero hero = target.Hero;
            string role = ResolveHeroRole(hero);
            profile = WorldbookIdentityCapabilityRules.Resolve(role, IsNoble(hero), (int)hero.Age, management);
            ApplyProfile(query, profile, role, "hero", hero.StringId, OccupationText(hero));
            return;
        }

        if (target?.Character != null)
        {
            string role = ResolveCharacterRole(target.Character, target.UnnamedRank);
            profile = WorldbookIdentityCapabilityRules.Resolve(role, false, (int)target.Age, management);
            ApplyProfile(query, profile, role, "npc", target.Character.StringId, OccupationText(target.Character));
            return;
        }

        profile = WorldbookIdentityCapabilityRules.Resolve(string.Empty, false, fallbackAge, management);
        ApplyProfile(query, profile, string.Empty, "fallback", string.Empty, string.Empty);
    }

    private static void ApplyProfile(
        WorldbookQuery query,
        WorldbookIdentityCapabilityProfile profile,
        string rawRole,
        string source,
        string subjectId,
        string rawOccupation)
    {
        query.IdentityId = profile.ProfileId;
        query.Role = WorldbookEntityId.Canonical("role", profile.Role);
        query.KnowledgeScope = profile.KnowledgeScope;
        query.KnowledgeScopeAvailable = profile.KnowledgeScopeAvailable;
        query.EffectiveDetail = profile.EffectiveDetail;
        query.EffectiveDetailAvailable = profile.EffectiveDetailAvailable;
        LogIfUnmapped(profile, rawRole, source, subjectId, rawOccupation);
    }

    // 落到「匿名」＝ 该角色在全部说法上一条都拿不到，且过去完全不报错、不打日志。
    // 这里必须留痕，否则「认不出身份」这种事永远只能靠人肉比对代码才发现。
    private static void LogIfUnmapped(
        WorldbookIdentityCapabilityProfile profile,
        string rawRole,
        string source,
        string subjectId,
        string rawOccupation)
    {
        if (profile == null || !StringComparer.Ordinal.Equals(profile.ProfileId, "profile.anonymous")) return;
        string key = source + "|" + (rawRole ?? string.Empty) + "|" + (rawOccupation ?? string.Empty);
        lock (UnmappedLock)
        {
            if (!UnmappedLogged.Add(key)) return;
        }
        AwakeLog.Write("worldbook_identity_unmapped source=" + source
            + " role=" + (string.IsNullOrWhiteSpace(rawRole) ? "(empty)" : rawRole)
            + " occupation=" + (string.IsNullOrWhiteSpace(rawOccupation) ? "(none)" : rawOccupation)
            + " subject=" + (subjectId ?? string.Empty)
            + " -> profile.anonymous");
    }

    private static string OccupationText(Hero hero)
    {
        try
        {
            return hero == null ? string.Empty : hero.Occupation.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string OccupationText(CharacterObject character)
    {
        try
        {
            return character == null ? string.Empty : character.Occupation.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsNoble(Hero hero)
    {
        try
        {
            return hero != null
                && (hero.IsLord
                    || hero.IsKingdomLeader
                    || hero.IsFactionLeader
                    || hero.Occupation == Occupation.Lord
                    || hero.Clan?.IsNoble == true);
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveHeroRole(Hero hero)
    {
        try
        {
            if (IsNoble(hero)) return "noble";
            // Special 是游戏的占位值（训练假人、隐士一类），不是职业，不能当身份用。
            if (hero.Occupation != Occupation.NotAssigned && hero.Occupation != Occupation.Special) return hero.Occupation.ToString();
            if (hero.IsHeadman) return "headman";
            if (hero.IsRuralNotable) return "rural_notable";
            if (hero.IsMerchant) return "merchant";
            if (hero.IsGangLeader) return "gang_leader";
            if (hero.IsWanderer) return "wanderer";
        }
        catch
        {
        }
        return "unknown";
    }

    private static string ResolveCharacterRole(CharacterObject character, string fallbackRole)
    {
        try
        {
            // 同上：Special 是占位值，跳过它才轮到 IsSoldier 兜底。
            if (character.Occupation != Occupation.NotAssigned && character.Occupation != Occupation.Special) return character.Occupation.ToString();
            if (character.IsSoldier) return "soldier";
        }
        catch
        {
        }
        return string.IsNullOrWhiteSpace(fallbackRole) ? "unknown" : fallbackRole;
    }

    private static int ResolveManagement(IDictionary<string, int> skills)
    {
        if (skills == null) return 0;
        if (skills.TryGetValue("management", out int management)) return Math.Max(0, management);
        if (skills.TryGetValue("steward", out int steward)) return Math.Max(0, steward);
        return 0;
    }
}
