using System;
using System.Collections.Generic;
using MarcusAIFramework.Api;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Awake;

internal enum NpcDialogueLaunchResult
{
    None,
    Overlay,
    Native
}

internal static class NpcDialogueLauncher
{
    private static readonly object CacheGate = new object();
    private static string _cacheKey = string.Empty;
    private static int _cacheDay = -1;
    private static int _cacheCount = -1;

    internal static NpcDialogueLaunchResult TryOpenDialogue(Hero hero, string entrySource)
    {
        return TryOpenDialogue(AwakeNpcTarget.FromHero(hero), entrySource);
    }

    internal static NpcDialogueLaunchResult TryOpenDialogue(AwakeNpcTarget target, string entrySource)
    {
        try
        {
            if (target == null || string.IsNullOrWhiteSpace(target.StableId) || !IsEligibleNpcTarget(target))
            {
                AwakeLog.Write("npc_dialogue_launcher_rejected target=" + (target?.StableId ?? "unknown") + " source=" + entrySource);
                return NpcDialogueLaunchResult.None;
            }
            IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
            if (host == null)
            {
                AwakeLog.Write("npc_dialogue_launcher_no_host target=" + target.StableId + " source=" + entrySource);
                return NpcDialogueLaunchResult.None;
            }
            string sourceKey = string.IsNullOrWhiteSpace(entrySource) ? "unknown" : entrySource;
            if (!AwakeDialogueSessionCoordinator.TryAcquire(sourceKey, target.StableId))
            {
                AwakeLog.Write("npc_dialogue_launcher_busy target=" + target.StableId
                    + " source=" + entrySource
                    + " active=" + AwakeDialogueSessionCoordinator.ActiveSource);
                return NpcDialogueLaunchResult.None;
            }

            NpcDialogueService service = new NpcDialogueService(host, target, CurrentSceneKeywords());
            service.Initialize();
            bool opened = NpcDialogueOverlay.Open(service, sourceKey, target.StableId);
            if (opened)
            {
                AwakeLog.Write("npc_dialogue_launcher_result target=" + target.StableId + " source=" + entrySource + " mode=overlay");
                return NpcDialogueLaunchResult.Overlay;
            }
            service.Dispose();
            AwakeDialogueSessionCoordinator.Close(sourceKey, target.StableId);

            if (StringComparer.Ordinal.Equals(entrySource, "scene"))
            {
                AwakeLog.Write("npc_dialogue_launcher_scene_native_skipped target=" + target.StableId);
                return NpcDialogueLaunchResult.None;
            }

            AwakeDialogueSessionCoordinator.Close(sourceKey, target.StableId);
            if (NpcDialogueStarter.TryOpenConversation(target))
            {
                AwakeLog.Write("npc_dialogue_launcher_result target=" + target.StableId + " source=" + entrySource + " mode=native");
                return NpcDialogueLaunchResult.Native;
            }
            AwakeLog.Write("npc_dialogue_launcher_result target=" + target.StableId + " source=" + entrySource + " mode=none");
            return NpcDialogueLaunchResult.None;
        }
        catch (Exception ex)
        {
            AwakeDialogueSessionCoordinator.Close(
                string.IsNullOrWhiteSpace(entrySource) ? "unknown" : entrySource,
                target?.StableId ?? string.Empty);
            AwakeLog.Write("npc_dialogue_launcher_error target=" + (target?.StableId ?? "unknown") + " source=" + entrySource + " error=" + ex.Message);
            return NpcDialogueLaunchResult.None;
        }
    }

    internal static List<AwakeNpcTarget> GetNearbyTargets(int limit)
    {
        List<AwakeNpcTarget> result = new List<AwakeNpcTarget>();
        if (limit <= 0) return result;
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            AddHeroCandidates(result, seen, limit);
            if (Mission.Current != null)
            {
                AddMissionAgents(result, seen, limit);
            }
            if (LocationComplex.Current != null)
            {
                AddLocationCharacters(result, seen, limit);
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_launcher_nearby_error error=" + ex.Message);
        }
        return result;
    }

    internal static List<AwakeNpcTarget> GetSceneTargets(int limit)
    {
        List<AwakeNpcTarget> result = new List<AwakeNpcTarget>();
        if (limit <= 0) return result;
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            AddMissionAgents(result, seen, limit);
            AddLocationCharacters(result, seen, limit);
            AddSceneHeroCandidates(result, seen, limit);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_launcher_scene_targets_error error=" + ex.Message);
        }
        return result;
    }

    internal static List<Hero> GetNearbyHeroes(int limit)
    {
        List<Hero> result = new List<Hero>();
        foreach (AwakeNpcTarget target in GetNearbyTargets(limit * 2))
        {
            if (result.Count >= limit) break;
            if (target.IsHero) result.Add(target.Hero);
        }
        return result;
    }

    internal static AwakeNpcTarget FindTargetById(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return null;
        string kind;
        string characterId;
        int agentIndex;
        if (!AwakeNpcTarget.TryParseStableId(targetId, out kind, out characterId, out agentIndex)) return null;
        try
        {
            if (StringComparer.Ordinal.Equals(kind, "hero"))
            {
                if (Campaign.Current?.CampaignObjectManager?.AliveHeroes == null) return null;
                foreach (Hero hero in Campaign.Current.CampaignObjectManager.AliveHeroes)
                {
                    if (hero != null && StringComparer.Ordinal.Equals(hero.StringId, characterId))
                    {
                        return AwakeNpcTarget.FromHero(hero);
                    }
                }
                return null;
            }

            CharacterObject character = FindCharacterById(characterId);
            if (character == null) return null;
            return AwakeNpcTarget.FromCharacter(character, null, agentIndex, "npc");
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_find_target_error target=" + targetId + " error=" + ex.Message);
            return null;
        }
    }

    internal static Hero FindHeroById(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return null;
        AwakeNpcTarget target = FindTargetById("hero:" + heroId);
        return target?.Hero;
    }

    internal static bool HasNearbyHero()
    {
        try
        {
            Settlement settlement = Settlement.CurrentSettlement;
            string key = settlement == null ? "party" : settlement.StringId;
            int day = (int)Math.Floor(CampaignTime.Now.ToDays);
            lock (CacheGate)
            {
                if (string.Equals(_cacheKey, key, StringComparison.Ordinal) && _cacheDay == day && _cacheCount >= 0)
                {
                    return _cacheCount > 0;
                }
            }
            int count = GetNearbyHeroes(1).Count;
            lock (CacheGate)
            {
                _cacheKey = key;
                _cacheDay = day;
                _cacheCount = count;
            }
            return count > 0;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_launcher_has_error error=" + ex.Message);
            return false;
        }
    }

    internal static void ClearCache()
    {
        lock (CacheGate)
        {
            _cacheKey = string.Empty;
            _cacheDay = -1;
            _cacheCount = -1;
        }
    }

    internal static Agent GetActiveAgent(int agentIndex)
    {
        if (agentIndex < 0 || Mission.Current?.Agents == null) return null;
        foreach (Agent agent in Mission.Current.Agents)
        {
            if (agent != null && agent.Index == agentIndex && agent.IsActive())
            {
                return agent;
            }
        }
        return null;
    }

    internal static bool IsEligibleNearbyHero(Hero hero)
    {
        return IsEligibleNpcTarget(AwakeNpcTarget.FromHero(hero));
    }

    internal static bool IsEligibleNpcTarget(AwakeNpcTarget target)
    {
        try
        {
            if (target == null || string.IsNullOrWhiteSpace(target.StableId)) return false;
            if (target.AgentIndex >= 0)
            {
                Agent agent = GetActiveAgent(target.AgentIndex);
                if (agent == null || !agent.IsActive() || agent == Agent.Main || agent.IsMainAgent) return false;
                if (agent.Character == null || agent.Character == CharacterObject.PlayerCharacter) return false;
                if (target.Age > 0f && target.Age < 18f) return false;
                return true;
            }
            if (target.IsHero)
            {
                Hero hero = target.Hero;
                if (hero == null || hero == Hero.MainHero || !hero.IsAlive || hero.Age < 18f) return false;
                Settlement settlement = Settlement.CurrentSettlement;
                MobileParty mainParty = MobileParty.MainParty;
                if (hero.PartyBelongedToAsPrisoner != null
                    && (mainParty == null || hero.PartyBelongedToAsPrisoner != mainParty.Party))
                {
                    return false;
                }
                bool nearby = settlement != null && (hero.CurrentSettlement == settlement || hero.PartyBelongedTo?.CurrentSettlement == settlement);
                if (!nearby && mainParty != null && (hero.PartyBelongedTo == mainParty || hero.PartyBelongedToAsPrisoner == mainParty.Party)) nearby = true;
                return nearby;
            }

            if (target.Character == null || target.Character == CharacterObject.PlayerCharacter) return false;
            if (target.Age < 18f) return false;
            return target.LocationCharacter != null
                || target.AgentIndex >= 0
                || IsEncounterPartyLeader(target.Character);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_target_eligibility_error error=" + ex.Message);
            return false;
        }
    }

    private static void AddHeroCandidates(List<AwakeNpcTarget> result, HashSet<string> seen, int limit)
    {
        if (Campaign.Current?.CampaignObjectManager?.AliveHeroes == null) return;
        foreach (Hero hero in Campaign.Current.CampaignObjectManager.AliveHeroes)
        {
            if (result.Count >= limit) return;
            AwakeNpcTarget target = AwakeNpcTarget.FromHero(hero);
            if (target == null || !IsEligibleNpcTarget(target)) continue;
            if (!seen.Add(target.StableId)) continue;
            result.Add(target);
        }
    }

    private static void AddSceneHeroCandidates(List<AwakeNpcTarget> result, HashSet<string> seen, int limit)
    {
        if (Mission.Current?.Agents == null) return;
        foreach (Agent agent in Mission.Current.Agents)
        {
            if (result.Count >= limit) return;
            if (agent == null || !agent.IsActive() || agent == Agent.Main || agent.IsMainAgent) continue;
            if (!(agent.Character is CharacterObject character) || character.HeroObject == null) continue;
            Hero hero = character.HeroObject;
            if (hero == null || hero == Hero.MainHero || !hero.IsAlive || hero.Age < 18f) continue;
            AwakeNpcTarget target = AwakeNpcTarget.FromHero(hero, agent.Index);
            if (target == null || !IsEligibleNpcTarget(target)) continue;
            if (!seen.Add(target.StableId)) continue;
            result.Add(target);
        }
    }

    private static void AddMissionAgents(List<AwakeNpcTarget> result, HashSet<string> seen, int limit)
    {
        Mission mission = Mission.Current;
        if (mission?.Agents == null) return;
        foreach (Agent agent in mission.Agents)
        {
            if (result.Count >= limit) return;
            if (agent == null || !agent.IsActive() || agent == Agent.Main || agent.IsMainAgent) continue;
            if (!(agent.Character is CharacterObject character)) continue;
            if (character.HeroObject != null) continue;
            if (character == CharacterObject.PlayerCharacter) continue;
            AwakeNpcTarget target = AwakeNpcTarget.FromCharacter(character, null, agent.Index, "npc");
            if (target == null || !IsEligibleNpcTarget(target)) continue;
            if (!seen.Add(target.StableId)) continue;
            result.Add(target);
        }
    }

    private static void AddLocationCharacters(List<AwakeNpcTarget> result, HashSet<string> seen, int limit)
    {
        LocationComplex complex = LocationComplex.Current;
        if (complex == null) return;
        foreach (LocationCharacter locationCharacter in complex.GetListOfCharacters())
        {
            if (result.Count >= limit) return;
            if (locationCharacter == null || !(locationCharacter.Character is CharacterObject character)) continue;
            if (character.HeroObject != null) continue;
            if (character == CharacterObject.PlayerCharacter) continue;
            int agentIndex = ResolveAgentIndex(character);
            AwakeNpcTarget target = AwakeNpcTarget.FromCharacter(character, locationCharacter, agentIndex, "npc");
            if (target == null || !IsEligibleNpcTarget(target)) continue;
            if (!seen.Add(target.StableId)) continue;
            result.Add(target);
        }
    }

    private static int ResolveAgentIndex(CharacterObject character)
    {
        Mission mission = Mission.Current;
        if (mission?.Agents == null) return -1;
        foreach (Agent agent in mission.Agents)
        {
            if (agent != null && agent.IsActive() && ReferenceEquals(agent.Character, character))
            {
                return agent.Index;
            }
        }
        return -1;
    }

    private static CharacterObject FindCharacterById(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) return null;
        foreach (CharacterObject character in CharacterObject.All)
        {
            if (character != null && StringComparer.Ordinal.Equals(character.StringId, characterId))
            {
                return character;
            }
        }
        return null;
    }

    private static bool IsEncounterPartyLeader(CharacterObject character)
    {
        if (character == null || PlayerEncounter.Current == null || PlayerEncounter.EncounteredParty == null) return false;
        try
        {
            return ReferenceEquals(
                ConversationHelper.GetConversationCharacterPartyLeader(PlayerEncounter.EncounteredParty),
                character);
        }
        catch
        {
            return false;
        }
    }

    internal static string CurrentSceneKeywords()
    {
        try
        {
            Settlement settlement = Settlement.CurrentSettlement;
            try
            {
                string menuId = Campaign.Current?.CurrentMenuContext?.StringId ?? string.Empty;
                if (string.Equals(menuId, "town_keep", StringComparison.Ordinal))
                {
                    return AwakeConstants.GetSceneKeywords("town_keep");
                }
            }
            catch
            {
            }
            if (settlement != null)
            {
                if (settlement.IsTown) return AwakeConstants.GetSceneKeywords("town");
                if (settlement.IsCastle) return AwakeConstants.GetSceneKeywords("castle");
                if (settlement.IsVillage) return AwakeConstants.GetSceneKeywords("village");
            }
            if (PartyBase.MainParty?.MobileParty?.IsCurrentlyAtSea == true) return "海上 行船 浪涛 舷窗";
            return "行军 营地 帐幕 篝火";
        }
        catch
        {
            return string.Empty;
        }
    }
}
