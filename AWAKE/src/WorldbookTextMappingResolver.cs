using System;
using System.Collections.Generic;
using System.Linq;

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
        bool? status = ResolveStatus(mapping, context);
        if (status.HasValue)
        {
            string text = status.Value ? mapping.TrueText : mapping.FalseText;
            return FirstNonEmpty(text, mapping.EmptyValueText, mapping.SourceText);
        }

        string targetId = mapping.TargetId ?? string.Empty;
        switch (kind)
        {
            case "bound_hero_name":
                return ValueOrSource(context?.BoundHeroName, mapping);
            case "bound_hero_title":
                return ValueOrSource(context?.BoundHeroTitle, mapping);
            case "bound_clan_name":
                return ValueOrSource(context?.BoundClanName, mapping);
            case "bound_settlement_name":
                return ValueOrSource(context?.BoundSettlementName, mapping);
            case "bound_settlement_owner_clan_name":
                return ValueOrSource(context?.BoundSettlementOwnerClanName, mapping);
            case "bound_settlement_owner_leader_name":
                return ValueOrSource(context?.BoundSettlementOwnerLeaderName, mapping);
            case "bound_kingdom_name":
                return ValueOrSource(context?.BoundKingdomName, mapping);
            case "bound_item_name":
                return ValueOrSource(context?.BoundItemName, mapping);
            case "bound_troop_name":
                return ValueOrSource(context?.BoundTroopName, mapping);
            case "bound_deity_name":
                return ValueOrSource(context?.BoundDeityName, mapping);
            case "bound_event_name":
                return ValueOrSource(context?.BoundEventName, mapping);
            case "bound_region_name":
                return ValueOrSource(context?.BoundRegionName, mapping);
            case "hero_name":
                return MapValue(context?.HeroNames, targetId, mapping);
            case "clan_name":
                return MapValue(context?.ClanNames, targetId, mapping);
            case "kingdom_name":
                return MapValue(context?.KingdomNames, targetId, mapping);
            case "settlement_name":
                return MapValue(context?.SettlementNames, targetId, mapping);
            case "kingdom_leader_name":
                return MapValue(context?.KingdomLeaderNames, targetId, mapping);
            case "clan_leader_name":
                return MapValue(context?.ClanLeaderNames, targetId, mapping);
            case "settlement_owner_clan_name":
                return MapValue(context?.SettlementOwnerClanNames, targetId, mapping);
            case "settlement_owner_leader_name":
                return MapValue(context?.SettlementOwnerLeaderNames, targetId, mapping);
            case "clan_all_towns":
                return ListValue(context?.ClanTowns, targetId, mapping);
            case "clan_all_villages":
                return ListValue(context?.ClanVillages, targetId, mapping);
            case "clan_all_settlements":
                return ListValue(context?.ClanSettlements, targetId, mapping);
            default:
                return mapping.SourceText ?? string.Empty;
        }
    }

    internal static bool IsSupportedKind(string kind)
    {
        switch ((kind ?? string.Empty).Trim())
        {
            case "bound_hero_name":
            case "bound_hero_title":
            case "bound_clan_name":
            case "bound_settlement_name":
            case "bound_settlement_owner_clan_name":
            case "bound_settlement_owner_leader_name":
            case "bound_kingdom_name":
            case "bound_item_name":
            case "bound_troop_name":
            case "bound_deity_name":
            case "bound_event_name":
            case "bound_region_name":
            case "hero_name":
            case "clan_name":
            case "kingdom_name":
            case "settlement_name":
            case "kingdom_leader_name":
            case "clan_leader_name":
            case "settlement_owner_clan_name":
            case "settlement_owner_leader_name":
            case "clan_all_towns":
            case "clan_all_villages":
            case "clan_all_settlements":
            case "status|hero|is_alive":
            case "status|hero|is_dead":
            case "status|clan|has_any_town":
            case "status|kingdom|is_eliminated":
                return true;
            default:
                return false;
        }
    }

    private static bool? ResolveStatus(WorldbookTextMapping mapping, WorldbookMappingContext context)
    {
        if (context?.Statuses == null || mapping == null) return null;
        string key = StatusKey(mapping);
        bool value;
        if (context.Statuses.TryGetValue(key, out value)) return value;
        return null;
    }

    private static string StatusKey(WorldbookTextMapping mapping)
    {
        return (mapping.Kind ?? string.Empty) + "|" + (mapping.TargetId ?? string.Empty);
    }

    private static string ValueOrSource(string value, WorldbookTextMapping mapping)
    {
        return string.IsNullOrWhiteSpace(value)
            ? FirstNonEmpty(mapping.EmptyValueText, mapping.SourceText)
            : value;
    }

    private static string MapValue(
        IReadOnlyDictionary<string, string> values,
        string targetId,
        WorldbookTextMapping mapping)
    {
        string value;
        if (!string.IsNullOrWhiteSpace(targetId)
            && values != null
            && values.TryGetValue(targetId, out value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
        return FirstNonEmpty(mapping.EmptyValueText, mapping.SourceText);
    }

    private static string ListValue(
        IReadOnlyDictionary<string, List<string>> values,
        string targetId,
        WorldbookTextMapping mapping)
    {
        List<string> list;
        if (!string.IsNullOrWhiteSpace(targetId)
            && values != null
            && values.TryGetValue(targetId, out list)
            && list != null
            && list.Count > 0)
        {
            return string.Join("，", list
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal));
        }
        return FirstNonEmpty(mapping.EmptyValueText, mapping.SourceText);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return string.Empty;
    }
}
