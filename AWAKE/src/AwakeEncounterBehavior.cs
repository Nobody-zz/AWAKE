using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Awake;

internal sealed class AwakeEncounterBehavior : CampaignBehaviorBase
{
    private bool _registered;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        if (_registered || starter == null) return;
        try
        {
            starter.AddGameMenuOption(
                "encounter",
                "awake_encounter_ai_talk",
                AwakeLocalization.Resolve("awake.menu.encounter_talk", "面谈（醒世）"),
                EncounterTalkCondition,
                EncounterTalkConsequence,
                false,
                -1,
                false);
            _registered = true;
            AwakeLog.Write("awake_encounter_menu_registered");
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_encounter_menu_register_failed error=" + ex.Message);
        }
    }

    private static bool EncounterTalkCondition(MenuCallbackArgs args)
    {
        if (args != null) args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        try
        {
            if (AwakeRuntime.ResolveHost() == null
                || AwakeMessengerOverlay.IsOpen
                || NpcDialogueOverlay.IsOpen)
            {
                return false;
            }
            if (Campaign.Current?.ConversationManager != null
                && Campaign.Current.ConversationManager.IsConversationInProgress)
            {
                return false;
            }
            return ResolveEncounterTarget() != null;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_encounter_talk_condition_error error=" + ex.Message);
            return false;
        }
    }

    private static void EncounterTalkConsequence(MenuCallbackArgs args)
    {
        try
        {
            AwakeNpcTarget target = ResolveEncounterTarget();
            if (target == null)
            {
                ShowMessage(
                    AwakeLocalization.Resolve("awake.menu.encounter_talk", "面谈（醒世）"),
                    "对方暂时无法交谈。");
                return;
            }
            NpcDialogueLaunchResult result = NpcDialogueLauncher.TryOpenDialogue(target, "encounter");
            if (result == NpcDialogueLaunchResult.None)
            {
                ShowMessage(
                    AwakeLocalization.Resolve("awake.menu.encounter_talk", "面谈（醒世）"),
                    "对方暂时无法交谈。");
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_encounter_talk_consequence_error error=" + ex.Message);
        }
    }

    private static AwakeNpcTarget ResolveEncounterTarget()
    {
        PartyBase party = PlayerEncounter.EncounteredParty;
        if (party == null) return null;
        if (party.LeaderHero != null)
        {
            return AwakeNpcTarget.FromHero(party.LeaderHero);
        }
        CharacterObject leader = ConversationHelper.GetConversationCharacterPartyLeader(party);
        return leader == null ? null : AwakeNpcTarget.FromCharacter(leader, null, -1, "leader");
    }

    private static void ShowMessage(string title, string text)
    {
        try
        {
            InformationManager.ShowInquiry(
                new InquiryData(title, text, true, false, "确定", "", null, null, "", 0f, null, null, null),
                true,
                false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_encounter_show_message_error error=" + ex.Message);
        }
    }
}
