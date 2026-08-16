using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal sealed class AwakeContactInfo
{
    internal AwakeNpcTarget Target { get; }
    internal string TargetId { get; }
    internal string DisplayName { get; }
    internal string Identity { get; }
    internal string Status { get; }
    internal bool IsNearby { get; }

    internal AwakeContactInfo(
        AwakeNpcTarget target,
        string displayName,
        string identity,
        string status,
        bool isNearby)
    {
        Target = target;
        TargetId = target?.StableId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Identity = identity ?? string.Empty;
        Status = status ?? string.Empty;
        IsNearby = isNearby;
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
                if (target == null || !seen.Add(target.StableId)) continue;
                result.Add(ToContact(target, true));
            }
            if (Campaign.Current?.CampaignObjectManager?.AliveHeroes != null)
            {
                foreach (Hero hero in Campaign.Current.CampaignObjectManager.AliveHeroes)
                {
                    if (hero == null || hero == Hero.MainHero || !hero.HasMet) continue;
                    AwakeNpcTarget target = AwakeNpcTarget.FromHero(hero);
                    if (target == null || !seen.Add(target.StableId)) continue;
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
        return new AwakeContactInfo(target, target.DisplayName, identity, status, isNearby);
    }
}
