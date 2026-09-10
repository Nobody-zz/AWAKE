using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal static class BannerlordWorldbookIdentityAdapter
{
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
            query.IdentityId = profile.ProfileId;
            query.Role = WorldbookEntityId.Canonical("role", profile.Role);
            query.KnowledgeScope = profile.KnowledgeScope;
            query.KnowledgeScopeAvailable = profile.KnowledgeScopeAvailable;
            query.EffectiveDetail = profile.EffectiveDetail;
            query.EffectiveDetailAvailable = profile.EffectiveDetailAvailable;
            return;
        }

        if (target?.Character != null)
        {
            string role = ResolveCharacterRole(target.Character, target.UnnamedRank);
            profile = WorldbookIdentityCapabilityRules.Resolve(role, false, (int)target.Age, management);
            query.IdentityId = profile.ProfileId;
            query.Role = WorldbookEntityId.Canonical("role", profile.Role);
            query.KnowledgeScope = profile.KnowledgeScope;
            query.KnowledgeScopeAvailable = profile.KnowledgeScopeAvailable;
            query.EffectiveDetail = profile.EffectiveDetail;
            query.EffectiveDetailAvailable = profile.EffectiveDetailAvailable;
            return;
        }

        profile = WorldbookIdentityCapabilityRules.Resolve(string.Empty, false, fallbackAge, management);
        query.IdentityId = profile.ProfileId;
        query.Role = WorldbookEntityId.Canonical("role", profile.Role);
        query.KnowledgeScope = profile.KnowledgeScope;
        query.KnowledgeScopeAvailable = profile.KnowledgeScopeAvailable;
        query.EffectiveDetail = profile.EffectiveDetail;
        query.EffectiveDetailAvailable = profile.EffectiveDetailAvailable;
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
            if (hero.Occupation != Occupation.NotAssigned) return hero.Occupation.ToString();
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
            if (character.Occupation != Occupation.NotAssigned) return character.Occupation.ToString();
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
