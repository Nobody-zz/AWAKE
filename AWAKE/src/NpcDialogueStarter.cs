using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Awake;

internal static class NpcDialogueStarter
{
    internal static bool TryOpenConversation(AwakeNpcTarget target)
    {
        try
        {
            if (target == null) return false;
            if (target.IsHero) return TryOpenConversation(target.Hero);
            if (target.Character == null || Campaign.Current == null
                || Campaign.Current.ConversationManager == null)
            {
                return false;
            }

            PartyBase party = ResolveNonHeroParty(target);
            if (party == null) return false;

            bool inSettlement = Settlement.CurrentSettlement != null;
            bool inLocation = LocationComplex.Current != null;
            bool inMenu = Campaign.Current.CurrentMenuContext != null;
            bool atSea = false;
            try
            {
                atSea = PartyBase.MainParty?.MobileParty?.IsCurrentlyAtSea == true;
            }
            catch
            {
            }

            bool isPrisoner = false;
            try
            {
                isPrisoner = PartyBase.MainParty?.PrisonRoster != null
                    && PartyBase.MainParty.PrisonRoster.GetTroopCount(target.Character) > 0;
            }
            catch
            {
                isPrisoner = false;
            }

            ConversationCharacterData playerData = new ConversationCharacterData(
                CharacterObject.PlayerCharacter, PartyBase.MainParty, true, false, false, true, false, true);
            ConversationCharacterData partnerData = new ConversationCharacterData(
                target.Character, party, true, isPrisoner, false, true, false, true);

            if (inSettlement && (inLocation || inMenu))
            {
                if (inLocation)
                {
                    CampaignMission.OpenConversationMission(playerData, partnerData);
                }
                else
                {
                    CampaignMapConversation.OpenConversation(playerData, partnerData);
                }
                AwakeLog.Write("npc_dialogue_open_success target=" + target.StableId + " context=settlement");
                return true;
            }

            if (atSea)
            {
                CampaignMission.OpenConversationMission(playerData, partnerData);
                AwakeLog.Write("npc_dialogue_open_success target=" + target.StableId + " context=sea");
                return true;
            }

            if (GameStateManager.Current?.ActiveState is MapState)
            {
                CampaignMapConversation.OpenConversation(playerData, partnerData);
                AwakeLog.Write("npc_dialogue_open_success target=" + target.StableId + " context=map");
                return true;
            }

            AwakeLog.Write("npc_dialogue_open_deferred target=" + target.StableId);
            return false;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_open_error target=" + (target?.StableId ?? "unknown") + " error=" + ex.Message);
            return false;
        }
    }

    private static PartyBase ResolveNonHeroParty(AwakeNpcTarget target)
    {
        try
        {
            if (target.Character == null) return null;
            MobileParty mainParty = MobileParty.MainParty;
            if (mainParty?.MemberRoster != null && mainParty.MemberRoster.GetTroopCount(target.Character) > 0)
            {
                return mainParty.Party;
            }
            if (mainParty?.PrisonRoster != null && mainParty.PrisonRoster.GetTroopCount(target.Character) > 0)
            {
                return mainParty.Party;
            }
            if (Settlement.CurrentSettlement?.Party != null)
            {
                return Settlement.CurrentSettlement.Party;
            }
            return mainParty?.Party;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_nonhero_party_error error=" + ex.Message);
            return null;
        }
    }

    internal static bool TryOpenConversation(Hero hero)
    {
        try
        {
            if (hero == null || hero == Hero.MainHero || Campaign.Current == null
                || hero.CharacterObject == null || Campaign.Current.ConversationManager == null)
            {
                return false;
            }

            PartyBase party;
            if (hero.PartyBelongedTo != null)
            {
                party = hero.PartyBelongedTo.Party;
            }
            else if (hero.PartyBelongedToAsPrisoner != null)
            {
                party = hero.PartyBelongedToAsPrisoner;
            }
            else if (hero.CurrentSettlement != null)
            {
                party = hero.CurrentSettlement.Party;
            }
            else
            {
                party = PartyBase.MainParty;
            }
            if (party == null) return false;

            bool inSettlement = Settlement.CurrentSettlement != null;
            bool inLocation = LocationComplex.Current != null;
            bool inMenu = Campaign.Current.CurrentMenuContext != null;
            bool atSea = false;
            try
            {
                atSea = PartyBase.MainParty?.MobileParty?.IsCurrentlyAtSea == true;
            }
            catch
            {
            }

            ConversationCharacterData playerData = new ConversationCharacterData(
                CharacterObject.PlayerCharacter, PartyBase.MainParty, true, false, false, true, false, true);
            ConversationCharacterData partnerData = new ConversationCharacterData(
                hero.CharacterObject, party, true, hero.IsPrisoner, false, true, false, true);

            if (inSettlement && (inLocation || inMenu))
            {
                if (inLocation)
                {
                    CampaignMission.OpenConversationMission(playerData, partnerData);
                }
                else
                {
                    CampaignMapConversation.OpenConversation(playerData, partnerData);
                }
                AwakeLog.Write("npc_dialogue_open_success hero=" + hero.StringId + " context=settlement");
                return true;
            }

            if (atSea)
            {
                CampaignMission.OpenConversationMission(playerData, partnerData);
                AwakeLog.Write("npc_dialogue_open_success hero=" + hero.StringId + " context=sea");
                return true;
            }

            if (GameStateManager.Current?.ActiveState is MapState)
            {
                CampaignMapConversation.OpenConversation(playerData, partnerData);
                AwakeLog.Write("npc_dialogue_open_success hero=" + hero.StringId + " context=map");
                return true;
            }

            AwakeLog.Write("npc_dialogue_open_deferred hero=" + hero.StringId);
            return false;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_open_error error=" + ex.Message);
            return false;
        }
    }
}
