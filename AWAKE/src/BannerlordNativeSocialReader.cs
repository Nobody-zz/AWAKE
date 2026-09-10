using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Awake;

internal static class BannerlordNativeSocialReader
{
    internal static NativeSocialSnapshot CapturePlayerToCurrentNpc(AwakeNpcTarget target)
    {
        Hero sourceHero = null;
        try
        {
            if (Campaign.Current != null) sourceHero = Hero.MainHero;
        }
        catch
        {
        }
        return Capture(sourceHero, target, AwakeRuntime.SessionGeneration, ReadCampaignTimeDays());
    }

    internal static NativeSocialSnapshot Capture(
        Hero sourceHero,
        AwakeNpcTarget target,
        int sessionGeneration,
        double? capturedAtGameTime = null)
    {
        Hero targetHero = target?.Hero;
        Clan sourceClan = TryGetClan(sourceHero);
        Clan targetClan = TryGetClan(targetHero);
        IdentitySnapshotInput sourceIdentity = CaptureHeroIdentity(sourceHero);
        IdentitySnapshotInput targetIdentity = CaptureTargetIdentity(target);
        NativeSocialSnapshotInput input = new NativeSocialSnapshotInput
        {
            SessionGeneration = sessionGeneration,
            SnapshotToken = Guid.NewGuid().ToString("N"),
            CapturedAtGameTime = capturedAtGameTime ?? ReadCampaignTimeDays(),
            SourceIdentity = sourceIdentity,
            TargetIdentity = targetIdentity,
            NativeFriendState = NativeFlagState.Unknown,
            NativeEnemyState = NativeFlagState.Unknown,
            NativeNeutralState = NativeFlagState.Unknown,
            SameClan = ResolveSameClan(sourceClan, targetClan),
            SameKingdom = ResolveSameKingdom(sourceClan, targetClan),
            FamilyLinks = new List<FamilyLinkSnapshotInput>()
        };

        if (sourceHero != null && targetHero != null)
        {
            try { input.BaseRelation = sourceHero.GetBaseHeroRelation(targetHero); } catch { }
            try { input.EffectiveRelation = sourceHero.GetRelation(targetHero); } catch { }
            try { input.NativeFriendState = ToFlag(sourceHero.IsFriend(targetHero)); } catch { }
            try { input.NativeEnemyState = ToFlag(sourceHero.IsEnemy(targetHero)); } catch { }
            try { input.NativeNeutralState = ToFlag(sourceHero.IsNeutral(targetHero)); } catch { }
            AddFamilyLinks(input.FamilyLinks, sourceHero, targetHero);
        }

        if (sourceClan != null && targetClan != null)
        {
            try { input.EffectiveClanRelation = sourceClan.GetRelationWithClan(targetClan); } catch { }
            try { input.WeightedClanRelation = FactionManager.GetRelationBetweenClans(sourceClan, targetClan); } catch { }
        }

        return NativeSocialSnapshot.Capture(input);
    }

    private static IdentitySnapshotInput CaptureTargetIdentity(AwakeNpcTarget target)
    {
        if (target?.Hero != null)
        {
            return CaptureHeroIdentity(target.Hero);
        }
        CharacterObject character = target?.Character;
        if (character == null) return null;
        return new IdentitySnapshotInput
        {
            StableId = target.StableId,
            CharacterId = TryString(() => character.StringId),
            CultureId = TryString(() => character.Culture?.StringId),
            Occupation = TryString(() => character.Occupation.ToString()),
            Age = IsKnownAge(target.Age) ? target.Age : (float?)null,
            IsFemale = TryBool(() => character.IsFemale)
        };
    }

    private static IdentitySnapshotInput CaptureHeroIdentity(Hero hero)
    {
        if (hero == null) return null;
        Clan clan = TryGetClan(hero);
        return new IdentitySnapshotInput
        {
            StableId = TryString(() => hero.StringId),
            HeroId = TryString(() => hero.StringId),
            CultureId = TryString(() => hero.Culture?.StringId),
            KingdomId = TryString(() => clan?.Kingdom?.StringId),
            ClanId = TryString(() => clan?.StringId),
            ClanLeaderId = TryString(() => clan?.Leader?.StringId),
            SettlementId = TryString(() => hero.CurrentSettlement?.StringId),
            Occupation = TryString(() => hero.Occupation.ToString()),
            Age = TryFloat(() => hero.Age),
            IsFemale = TryBool(() => hero.IsFemale),
            IsNoble = TryBool(() => hero.IsLord || hero.IsKingdomLeader || hero.IsFactionLeader || clan?.IsNoble == true),
            IsClanLeader = TryBool(() => hero.IsClanLeader),
            IsKingdomLeader = TryBool(() => hero.IsKingdomLeader),
            IsFactionLeader = TryBool(() => hero.IsFactionLeader),
            IsHeadman = TryBool(() => hero.IsHeadman),
            IsRuralNotable = TryBool(() => hero.IsRuralNotable),
            IsMerchant = TryBool(() => hero.IsMerchant),
            IsGangLeader = TryBool(() => hero.IsGangLeader),
            IsWanderer = TryBool(() => hero.IsWanderer)
        };
    }

    private static void AddFamilyLinks(List<FamilyLinkSnapshotInput> links, Hero source, Hero target)
    {
        if (links == null || source == null || target == null) return;
        try { if (source.Father == target) links.Add(new FamilyLinkSnapshotInput(FamilyLinkKind.Father, target.StringId)); } catch { }
        try { if (source.Mother == target) links.Add(new FamilyLinkSnapshotInput(FamilyLinkKind.Mother, target.StringId)); } catch { }
        try { if (source.Spouse == target) links.Add(new FamilyLinkSnapshotInput(FamilyLinkKind.Spouse, target.StringId)); } catch { }
        try
        {
            if (source.Children != null)
            {
                foreach (Hero child in source.Children)
                {
                    if (child == target)
                    {
                        links.Add(new FamilyLinkSnapshotInput(FamilyLinkKind.Child, target.StringId));
                        break;
                    }
                }
            }
        }
        catch
        {
        }
    }

    private static NativeFlagState ResolveSameClan(Clan source, Clan target)
    {
        if (source == null || target == null) return NativeFlagState.Unknown;
        return ToFlag(StringComparer.Ordinal.Equals(TryString(() => source.StringId), TryString(() => target.StringId)));
    }

    private static NativeFlagState ResolveSameKingdom(Clan source, Clan target)
    {
        string sourceId = TryString(() => source?.Kingdom?.StringId);
        string targetId = TryString(() => target?.Kingdom?.StringId);
        if (sourceId == null || targetId == null) return NativeFlagState.Unknown;
        return ToFlag(StringComparer.Ordinal.Equals(sourceId, targetId));
    }

    private static Clan TryGetClan(Hero hero)
    {
        try { return hero?.Clan; } catch { return null; }
    }

    private static NativeFlagState ToFlag(bool value)
    {
        return value ? NativeFlagState.True : NativeFlagState.False;
    }

    private static bool IsKnownAge(float age)
    {
        return !float.IsNaN(age) && !float.IsInfinity(age) && age >= 0f;
    }

    private static double? ReadCampaignTimeDays()
    {
        try
        {
            if (Campaign.Current == null) return null;
            return CampaignTime.Now.ToDays;
        }
        catch
        {
            return null;
        }
    }

    private static string TryString(Func<string> reader)
    {
        try { return reader(); } catch { return null; }
    }

    private static bool? TryBool(Func<bool> reader)
    {
        try { return reader(); } catch { return null; }
    }

    private static float? TryFloat(Func<float> reader)
    {
        try
        {
            float value = reader();
            return IsKnownAge(value) ? value : (float?)null;
        }
        catch
        {
            return null;
        }
    }
}
