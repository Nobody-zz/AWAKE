using System;
using System.Collections.Generic;

namespace Awake;

internal sealed class AwakeEventRule
{
    internal AwakeEventDefinition Definition { get; }
    internal int Weight { get; }
    internal int CooldownHours { get; }
    internal int MaxPerDay { get; }
    internal AwakeEventCondition Condition { get; }
    internal string NextEventId { get; }

    internal AwakeEventRule(
        AwakeEventDefinition definition,
        int weight,
        int cooldownHours,
        AwakeEventCondition condition,
        string nextEventId = null,
        int maxPerDay = 0)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Weight = weight < 1 ? 1 : weight;
        CooldownHours = cooldownHours < 0 ? 0 : cooldownHours;
        MaxPerDay = maxPerDay < 0 ? 0 : maxPerDay;
        Condition = condition;
        NextEventId = nextEventId ?? string.Empty;
    }
}

internal static class AwakeEventChainCore
{
    internal static AwakeEventRule Resolve(
        IReadOnlyDictionary<string, AwakeEventRule> rules,
        string currentId,
        string choice)
    {
        if (rules == null
            || string.IsNullOrWhiteSpace(currentId)
            || !StringComparer.Ordinal.Equals(choice, "a"))
        {
            return null;
        }
        AwakeEventRule current;
        if (!rules.TryGetValue(currentId, out current) || string.IsNullOrWhiteSpace(current.NextEventId))
        {
            return null;
        }
        AwakeEventRule next;
        return rules.TryGetValue(current.NextEventId, out next) ? next : null;
    }
}

internal static class AwakeEventEngineCore
{
    internal static bool IsCooldownReady(double lastTriggerHour, double nowHour, int cooldownHours)
    {
        return lastTriggerHour < 0d || nowHour - lastTriggerHour >= cooldownHours;
    }

    internal static AwakeEventRule SelectWeighted(IReadOnlyList<AwakeEventRule> rules, Random random)
    {
        if (rules == null || rules.Count == 0 || random == null) return null;
        int total = 0;
        foreach (AwakeEventRule rule in rules) total += rule.Weight;
        if (total <= 0) return null;
        int roll = random.Next(0, total);
        int cursor = 0;
        foreach (AwakeEventRule rule in rules)
        {
            cursor += rule.Weight;
            if (roll < cursor) return rule;
        }
        return rules[rules.Count - 1];
    }
}
