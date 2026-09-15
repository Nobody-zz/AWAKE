using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Awake;

internal sealed class AwakeWorldFactCollectorBehavior : CampaignBehaviorBase
{
    private delegate bool FactBuilder(out WorldFactCapture fact);

    public override void RegisterEvents()
    {
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnWarDeclared(IFaction factionA, IFaction factionB, DeclareWarAction.DeclareWarDetail detail)
    {
        TryCapture("war_declared", (out WorldFactCapture fact) => WorldFactCapture.TryCreateWar(CurrentDay(), CurrentTimeSlot(), IdOf(factionA), NameOf(factionA), IdOf(factionB), NameOf(factionB), out fact));
    }

    private void OnMakePeace(IFaction factionA, IFaction factionB, MakePeaceAction.MakePeaceDetail detail)
    {
        TryCapture("peace_made", (out WorldFactCapture fact) => WorldFactCapture.TryCreatePeace(CurrentDay(), CurrentTimeSlot(), IdOf(factionA), NameOf(factionA), IdOf(factionB), NameOf(factionB), out fact));
    }

    private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero previousOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        TryCapture("settlement_owner_changed", (out WorldFactCapture fact) => WorldFactCapture.TryCreateSettlementOwnerChanged(CurrentDay(), CurrentTimeSlot(), IdOf(settlement), NameOf(settlement), IdOf(previousOwner), IdOf(newOwner), NameOf(newOwner), out fact));
    }

    private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
    {
        TryCapture("hero_killed", (out WorldFactCapture fact) => WorldFactCapture.TryCreateHeroKilled(CurrentDay(), CurrentTimeSlot(), IdOf(victim), NameOf(victim), IdOf(killer), out fact));
    }

    private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification)
    {
        TryCapture("hero_prisoner_released", (out WorldFactCapture fact) => WorldFactCapture.TryCreateHeroPrisonerReleased(CurrentDay(), CurrentTimeSlot(), IdOf(prisoner), NameOf(prisoner), IdOf(capturerFaction), detail.ToString(), (int)detail, out fact));
    }

    private static void TryCapture(string expectedKind, FactBuilder create)
    {
        try
        {
            WorldFactCapture fact;
            if (!create(out fact) || fact == null)
            {
                AwakeLog.Write("awake_world_fact_capture_skipped kind=" + expectedKind);
                return;
            }
            int generation = AwakeRuntime.SessionGeneration;
            WorldStateStore store = AwakeRuntime.WorldStateStore;
            if (store == null || !AwakeRuntime.IsCurrentSession(generation, store))
            {
                AwakeLog.Write("awake_world_fact_capture_unavailable kind=" + fact.Kind + " day=" + fact.Day + " key=" + fact.EventKey);
                return;
            }
            WorldEventLedger.QueueFact(fact, generation);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_world_fact_capture_error kind=" + expectedKind + " error=" + ex.Message);
        }
    }

    private static int CurrentDay() => AwakeRuntime.CurrentGameDay();

    private static long CurrentTimeSlot() => WorldFactCapture.ToTimeSlot(CampaignTime.Now.ToDays);

    private static string IdOf(IFaction faction) => faction == null ? string.Empty : faction.StringId;
    private static string IdOf(Hero hero) => hero == null ? string.Empty : hero.StringId;
    private static string IdOf(Settlement settlement) => settlement == null ? string.Empty : settlement.StringId;
    private static string NameOf(IFaction faction) => faction == null ? string.Empty : faction.Name?.ToString() ?? string.Empty;
    private static string NameOf(Hero hero) => hero == null ? string.Empty : hero.Name?.ToString() ?? string.Empty;
    private static string NameOf(Settlement settlement) => settlement == null ? string.Empty : settlement.Name?.ToString() ?? string.Empty;
}
