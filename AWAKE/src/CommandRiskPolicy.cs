using System;
using System.Collections.Generic;
using MarcusAIFramework.Api;

namespace Awake;

internal static class CommandRiskPolicy
{
    private static readonly Dictionary<string, CommandRiskTier> KnownCommands = new Dictionary<string, CommandRiskTier>(StringComparer.Ordinal);

    internal static bool TryGetRiskTier(string commandId, out CommandRiskTier tier)
    {
        return KnownCommands.TryGetValue(commandId ?? string.Empty, out tier);
    }

    internal static bool IsKnown(string commandId)
    {
        return KnownCommands.ContainsKey(commandId ?? string.Empty);
    }

    internal static string RiskLabel(CommandRiskTier tier)
    {
        switch (tier)
        {
            case CommandRiskTier.R0Query:
                return "R0";
            case CommandRiskTier.R1Interface:
                return "R1";
            case CommandRiskTier.R2Gameplay:
                return "R2";
            case CommandRiskTier.R3Strategic:
                return "R3";
            default:
                return tier.ToString();
        }
    }

    internal static bool IsWorldBridgeAllowed(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId)) return false;

        bool inNewCommands = false;
        foreach (string id in AiTaskConstants.NewCommandIds)
        {
            if (StringComparer.Ordinal.Equals(id, commandId))
            {
                inNewCommands = true;
                break;
            }
        }
        if (!inNewCommands) return false;

        return TryGetRiskTier(commandId, out CommandRiskTier tier) && tier != CommandRiskTier.R3Strategic;
    }
}
