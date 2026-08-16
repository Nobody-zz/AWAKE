using System;
using System.Collections.Generic;

namespace Awake;

internal static class WorldbookTextMappingResolver
{
    internal static string Apply(
        string content,
        IReadOnlyList<WorldbookTextMapping> mappings,
        WorldbookMappingContext context)
    {
        string result = content ?? string.Empty;
        if (mappings == null || string.IsNullOrEmpty(result)) return result;
        foreach (WorldbookTextMapping mapping in mappings)
        {
            if (mapping == null || string.IsNullOrWhiteSpace(mapping.SourceText)) continue;
            result = result.Replace(mapping.SourceText, Resolve(mapping, context));
        }
        return result;
    }

    internal static string Resolve(WorldbookTextMapping mapping, WorldbookMappingContext context)
    {
        if (mapping == null) return string.Empty;
        string kind = (mapping.Kind ?? string.Empty).Trim();
        if (StringComparer.Ordinal.Equals(kind, "status|hero|is_dead"))
        {
            if (context?.HeroIsDead == true) return mapping.TrueText ?? string.Empty;
            if (context?.HeroIsDead == false) return mapping.FalseText ?? string.Empty;
            return mapping.EmptyValueText ?? mapping.SourceText ?? string.Empty;
        }
        if (StringComparer.Ordinal.Equals(kind, "status|clan|has_any_town"))
        {
            if (context?.ClanHasTown == true) return mapping.TrueText ?? string.Empty;
            if (context?.ClanHasTown == false) return mapping.FalseText ?? string.Empty;
            return mapping.EmptyValueText ?? mapping.SourceText ?? string.Empty;
        }
        if (StringComparer.Ordinal.Equals(kind, "kingdom_leader_name"))
        {
            string name;
            if (context?.KingdomLeaderNames != null
                && !string.IsNullOrWhiteSpace(mapping.TargetId)
                && context.KingdomLeaderNames.TryGetValue(mapping.TargetId, out name)
                && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
            return mapping.SourceText ?? string.Empty;
        }
        if (StringComparer.Ordinal.Equals(kind, "settlement_owner_leader_name"))
        {
            string name;
            if (context?.SettlementOwnerLeaderNames != null
                && !string.IsNullOrWhiteSpace(mapping.TargetId)
                && context.SettlementOwnerLeaderNames.TryGetValue(mapping.TargetId, out name)
                && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
            return mapping.SourceText ?? string.Empty;
        }
        if (StringComparer.Ordinal.Equals(kind, "bound_hero_name"))
        {
            return string.IsNullOrWhiteSpace(context?.BoundHeroName)
                ? mapping.SourceText ?? string.Empty
                : context.BoundHeroName;
        }
        if (StringComparer.Ordinal.Equals(kind, "bound_clan_name"))
        {
            return string.IsNullOrWhiteSpace(context?.BoundClanName)
                ? mapping.SourceText ?? string.Empty
                : context.BoundClanName;
        }
        if (StringComparer.Ordinal.Equals(kind, "bound_settlement_name"))
        {
            return string.IsNullOrWhiteSpace(context?.BoundSettlementName)
                ? mapping.SourceText ?? string.Empty
                : context.BoundSettlementName;
        }
        if (StringComparer.Ordinal.Equals(kind, "bound_item_name"))
        {
            return string.IsNullOrWhiteSpace(context?.BoundItemName)
                ? mapping.SourceText ?? string.Empty
                : context.BoundItemName;
        }
        if (StringComparer.Ordinal.Equals(kind, "bound_troop_name"))
        {
            return string.IsNullOrWhiteSpace(context?.BoundTroopName)
                ? mapping.SourceText ?? string.Empty
                : context.BoundTroopName;
        }
        return mapping.SourceText ?? string.Empty;
    }
}
