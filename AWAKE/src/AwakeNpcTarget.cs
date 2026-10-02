using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Awake;

internal sealed class AwakeNpcTarget
{
    internal Hero Hero { get; }
    internal CharacterObject Character { get; }
    internal LocationCharacter LocationCharacter { get; }
    internal int AgentIndex { get; }
    internal string StableId { get; }
    internal string DisplayName { get; }
    internal string CultureId { get; }
    internal string TroopId { get; }
    internal string UnnamedKey { get; }
    internal string UnnamedRank { get; }
    internal bool IsFemale { get; }
    internal float Age { get; }

    internal bool IsHero => Hero != null;
    internal string CanonicalContactKey
    {
        get
        {
            if (IsHero && Hero != null && !string.IsNullOrWhiteSpace(Hero.StringId))
            {
                return "hero:" + Hero.StringId;
            }
            if (Character != null && !string.IsNullOrWhiteSpace(Character.StringId))
            {
                return "npc:" + Character.StringId;
            }
            return string.Empty;
        }
    }

    private AwakeNpcTarget(
        Hero hero,
        CharacterObject character,
        LocationCharacter locationCharacter,
        int agentIndex,
        string stableId,
        string displayName,
        string cultureId,
        string troopId,
        string unnamedKey,
        string unnamedRank,
        bool isFemale,
        float age)
    {
        Hero = hero;
        Character = character;
        LocationCharacter = locationCharacter;
        AgentIndex = agentIndex;
        StableId = stableId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        CultureId = cultureId ?? string.Empty;
        TroopId = troopId ?? string.Empty;
        UnnamedKey = unnamedKey ?? string.Empty;
        UnnamedRank = unnamedRank ?? string.Empty;
        IsFemale = isFemale;
        Age = age;
    }

    internal static AwakeNpcTarget FromHero(Hero hero, int agentIndex = -1)
    {
        if (hero == null) return null;
        string name = SafeName(hero.Name, hero.StringId);
        string cultureId = hero.Culture?.StringId ?? string.Empty;
        return new AwakeNpcTarget(
            hero,
            hero.CharacterObject,
            null,
            agentIndex,
            "hero:" + hero.StringId,
            name,
            cultureId,
            string.Empty,
            string.Empty,
            "hero",
            hero.IsFemale,
            hero.Age);
    }

    internal static AwakeNpcTarget FromCharacter(
        CharacterObject character,
        LocationCharacter locationCharacter,
        int agentIndex,
        string unnamedRank)
    {
        if (character == null) return null;
        string name = SafeName(character.Name, character.StringId);
        string cultureId = character.Culture?.StringId ?? string.Empty;
        string troopId = character.StringId ?? string.Empty;
        string gender = character.IsFemale ? "f" : "m";
        string rank = string.IsNullOrWhiteSpace(unnamedRank) ? "unknown" : unnamedRank.Trim().ToLowerInvariant();
        string agentPart = agentIndex >= 0 ? ":a" + agentIndex : ":static";
        string stableId = "npc:" + character.StringId + agentPart;
        string unnamedKey = "unnamed:" + character.StringId + ":" + cultureId + ":" + troopId + ":" + rank + ":" + gender;
        float age = ResolveAge(character);
        return new AwakeNpcTarget(
            null,
            character,
            locationCharacter,
            agentIndex,
            stableId,
            name,
            cultureId,
            troopId,
            unnamedKey,
            rank,
            character.IsFemale,
            age);
    }

    /// <summary>
    /// 把任意形态的目标 id 归一成稳定 id（hero:StringId / npc:StringId[:aN]）。
    ///
    /// 规则（幂等，可重复调用）：
    /// - 已带 scheme（即含 ':'）的一律原样返回 —— 既防 hero:hero:xxx 二次加前缀，
    ///   也保护 scene:current 这类非英雄哨兵值不被误改；
    /// - 只有完全不带 scheme 的裸 StringId 才按英雄补前缀。依据：主动对话队列的两个生产者
    ///   喂进来的都是 Hero.StringId（NpcProactiveService.cs:342 HeroId = hero.StringId；
    ///   AwakeEventEngine.ResolveDialogueTarget 返回 ...Leader?.StringId）。
    ///
    /// 2026-10-01：缺陷③ 的修复点。生产者给裸 id、消费端（NpcDialogueLauncher.FindTargetById）
    /// 只认前缀 ⇒ 解析恒失败 ⇒ npc_dialogue_open_failed:target_unavailable，NPC 的嘴张不开。
    /// </summary>
    internal static string NormalizeStableId(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId)) return string.Empty;
        string trimmed = stableId.Trim();
        if (trimmed.IndexOf(':') >= 0) return trimmed;
        return "hero:" + trimmed;
    }

    internal static bool TryParseStableId(string stableId, out string kind, out string characterId, out int agentIndex)
    {
        kind = string.Empty;
        characterId = string.Empty;
        agentIndex = -1;
        if (string.IsNullOrWhiteSpace(stableId)) return false;
        if (stableId.StartsWith("hero:", StringComparison.Ordinal))
        {
            kind = "hero";
            characterId = stableId.Substring(5);
            return true;
        }
        if (!stableId.StartsWith("npc:", StringComparison.Ordinal)) return false;
        kind = "npc";
        string rest = stableId.Substring(4);
        int agentMarker = rest.IndexOf(":a", StringComparison.Ordinal);
        if (agentMarker > 0)
        {
            characterId = rest.Substring(0, agentMarker);
            string agentText = rest.Substring(agentMarker + 2);
            int parsed;
            if (int.TryParse(agentText, out parsed)) agentIndex = parsed;
            return !string.IsNullOrWhiteSpace(characterId);
        }
        characterId = rest;
        return !string.IsNullOrWhiteSpace(characterId);
    }

    private static float ResolveAge(CharacterObject character)
    {
        try
        {
            return character.Age > 0f ? character.Age : 30f;
        }
        catch
        {
            return 30f;
        }
    }

    private static string SafeName(TextObject text, string fallback)
    {
        try
        {
            string name = text?.ToString();
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }
        catch
        {
            return fallback;
        }
    }
}
