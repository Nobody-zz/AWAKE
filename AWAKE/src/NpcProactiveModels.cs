using System;
using Newtonsoft.Json.Linq;

namespace Awake;

internal enum NpcProactiveMotive
{
    Casual,
    Relationship
}

internal enum NpcProactiveState
{
    None,
    Pending,
    Opening,
    Accepted,
    Rejected,
    Expired
}

internal static class NpcProactiveConstants
{
    internal const string NamespaceId = "awake.npc.proactive";
    internal const string Key = "campaign.proactive.v1";
    internal const string Schema = "awake.npc.proactive.v1";
    internal const int MaximumCandidates = 10;
    internal const int EvaluationLimit = 8;
    internal const int CooldownDays = 2;
    internal const int RejectCooldownDays = 1;
    internal const int ExpiresAfterDays = 1;
    internal const int MaximumFatigue = 3;
    internal const double BaseChance = 0.10;
    internal const double RelationshipBonusPerPoint = 0.001;
    internal const double ChanceMaximum = 0.35;
    internal const double MissingRelationshipChance = 0.02;
    internal const double NeutralChance = 0.05;
    internal const double FamiliarChance = 0.08;
    internal const double HighAffinityChance = 0.16;
    internal const double HostilityChance = 0.09;
    internal const int HighAffinityThreshold = 50;
    internal const int FamiliarAffinityThreshold = 10;
    internal const int HostilityThreshold = -30;
}

internal sealed class NpcProactiveCandidate
{
    internal string HeroId { get; set; } = string.Empty;
    internal NpcProactiveMotive Motive { get; set; } = NpcProactiveMotive.Casual;
    internal string MotiveId { get; set; } = "casual";
    internal int Urgency { get; set; } = 1;
    internal int Affinity { get; set; }
    internal NpcProactiveState State { get; set; } = NpcProactiveState.Pending;
    internal int Day { get; set; }
    internal int ExpiresAtDay { get; set; }
    internal int CooldownDay { get; set; }
    internal int Fatigue { get; set; }
    internal string OpeningHint { get; set; } = string.Empty;
    internal string TriggerReason { get; set; } = string.Empty;

    internal JObject ToJson()
    {
        return new JObject
        {
            ["heroId"] = HeroId ?? string.Empty,
            ["motive"] = Motive.ToString().ToLowerInvariant(),
            ["motiveId"] = MotiveId ?? string.Empty,
            ["urgency"] = Urgency,
            ["affinity"] = Affinity,
            ["state"] = State.ToString().ToLowerInvariant(),
            ["day"] = Day,
            ["expiresAtDay"] = ExpiresAtDay,
            ["cooldownDay"] = CooldownDay,
            ["fatigue"] = Fatigue,
            ["openingHint"] = OpeningHint ?? string.Empty,
            ["triggerReason"] = TriggerReason ?? string.Empty
        };
    }

    internal static NpcProactiveCandidate FromJson(JToken token)
    {
        NpcProactiveCandidate candidate = new NpcProactiveCandidate();
        if (token is not JObject obj) return candidate;
        candidate.HeroId = (string)obj["heroId"] ?? string.Empty;
        candidate.Motive = ParseMotive((string)obj["motive"]);
        candidate.MotiveId = (string)obj["motiveId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(candidate.MotiveId))
        {
            candidate.MotiveId = candidate.Motive == NpcProactiveMotive.Relationship
                ? "relationship"
                : "casual";
        }
        candidate.Urgency = IntValue(obj["urgency"]);
        candidate.Affinity = IntValue(obj["affinity"]);
        candidate.State = ParseState((string)obj["state"]);
        candidate.Day = IntValue(obj["day"]);
        candidate.ExpiresAtDay = IntValue(obj["expiresAtDay"]);
        candidate.CooldownDay = IntValue(obj["cooldownDay"]);
        candidate.Fatigue = IntValue(obj["fatigue"]);
        candidate.OpeningHint = (string)obj["openingHint"] ?? string.Empty;
        candidate.TriggerReason = (string)obj["triggerReason"] ?? string.Empty;
        return candidate;
    }

    private static NpcProactiveMotive ParseMotive(string value)
    {
        if (StringComparer.Ordinal.Equals(value, "relationship")) return NpcProactiveMotive.Relationship;
        return NpcProactiveMotive.Casual;
    }

    private static NpcProactiveState ParseState(string value)
    {
        if (StringComparer.Ordinal.Equals(value, "pending")) return NpcProactiveState.Pending;
        if (StringComparer.Ordinal.Equals(value, "opening")) return NpcProactiveState.Opening;
        if (StringComparer.Ordinal.Equals(value, "accepted")) return NpcProactiveState.Accepted;
        if (StringComparer.Ordinal.Equals(value, "rejected")) return NpcProactiveState.Rejected;
        if (StringComparer.Ordinal.Equals(value, "expired")) return NpcProactiveState.Expired;
        return NpcProactiveState.None;
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }
}
