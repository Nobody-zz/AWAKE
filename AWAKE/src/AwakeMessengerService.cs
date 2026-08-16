using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Awake;

internal sealed class AwakeContactInfo
{
    internal AwakeNpcTarget Target { get; }
    internal string TargetId { get; }
    internal string CanonicalContactKey { get; }
    internal string DisplayName { get; }
    internal string Identity { get; }
    internal string Status { get; }
    internal bool IsNearby { get; }
    internal bool CanTalk { get; }
    internal string Location { get; }

    internal AwakeContactInfo(
        AwakeNpcTarget target,
        string displayName,
        string identity,
        string status,
        bool isNearby,
        bool canTalk,
        string location,
        string canonicalContactKey = null)
    {
        Target = target;
        CanonicalContactKey = target?.CanonicalContactKey ?? canonicalContactKey ?? string.Empty;
        TargetId = target?.StableId ?? CanonicalContactKey;
        DisplayName = displayName ?? string.Empty;
        Identity = identity ?? string.Empty;
        Status = status ?? string.Empty;
        IsNearby = isNearby;
        CanTalk = canTalk;
        Location = location ?? string.Empty;
    }
}

internal static class AwakeMessengerService
{
    internal static List<AwakeContactInfo> BuildContacts()
    {
        List<AwakeContactInfo> result = new List<AwakeContactInfo>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (AwakeNpcTarget target in NpcDialogueLauncher.GetNearbyTargets(24))
            {
                if (target == null || string.IsNullOrWhiteSpace(target.CanonicalContactKey)
                    || !seen.Add(target.CanonicalContactKey)) continue;
                result.Add(ToContact(target, true));
            }
            if (Campaign.Current?.CampaignObjectManager?.AliveHeroes != null)
            {
                foreach (Hero hero in Campaign.Current.CampaignObjectManager.AliveHeroes)
                {
                    if (hero == null || hero == Hero.MainHero || !hero.HasMet) continue;
                    AwakeNpcTarget target = AwakeNpcTarget.FromHero(hero);
                    if (target == null || string.IsNullOrWhiteSpace(target.CanonicalContactKey)
                        || !seen.Add(target.CanonicalContactKey)) continue;
                    result.Add(ToContact(target, false));
                }
            }
            result.Sort((a, b) =>
            {
                int nearby = b.IsNearby.CompareTo(a.IsNearby);
                if (nearby != 0) return nearby;
                return StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName);
            });
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_build_contacts_error error=" + ex.Message);
        }
        return result;
    }

    private static AwakeContactInfo ToContact(AwakeNpcTarget target, bool isNearby)
    {
        string identity;
        if (target.IsHero)
        {
            identity = AwakeLocalization.Resolve("awake.ui.contact_hero", "英雄");
        }
        else
        {
            identity = AwakeUnnamedProfileService.BuildIdentity(target);
        }
        string status = isNearby
            ? AwakeLocalization.Resolve("awake.ui.contact_status_nearby", "附近")
            : AwakeLocalization.Resolve("awake.ui.contact_status_remote", "远方");
        string location = ResolveLocation(target);
        bool canTalk = isNearby && NpcDialogueLauncher.IsEligibleNpcTarget(target);
        return new AwakeContactInfo(target, target.DisplayName, identity, status, isNearby, canTalk, location);
    }

    private static string ResolveLocation(AwakeNpcTarget target)
    {
        try
        {
            if (target?.Hero != null)
            {
                return target.Hero.CurrentSettlement?.Name?.ToString()
                    ?? target.Hero.StayingInSettlement?.Name?.ToString()
                    ?? target.Hero.PartyBelongedTo?.CurrentSettlement?.Name?.ToString()
                    ?? string.Empty;
            }
            return Settlement.CurrentSettlement?.Name?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
