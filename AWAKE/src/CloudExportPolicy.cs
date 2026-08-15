using System;

namespace Awake;

internal static class CloudExportPolicy
{
    internal const string None = "none";
    internal const string PlayerState = "player_state";

    internal static bool IsKnownClassification(string classification)
    {
        return StringComparer.Ordinal.Equals(classification, None)
            || StringComparer.Ordinal.Equals(classification, PlayerState);
    }

    internal static bool IsClassificationAllowed(AwakeConfig config, string classification)
    {
        if (StringComparer.Ordinal.Equals(classification, None)) return true;
        if (StringComparer.Ordinal.Equals(classification, PlayerState))
        {
            return config != null && config.EnableCloudExport && config.AllowCloudExportPlayerState;
        }
        return false;
    }

    internal static string ResolveDialogueClassification(AwakeConfig config)
    {
        return IsClassificationAllowed(config, PlayerState) ? PlayerState : None;
    }

    internal static string[] AllowedContextClassifications(AwakeConfig config, string effectiveClassification)
    {
        if (StringComparer.Ordinal.Equals(effectiveClassification, PlayerState)
            && IsClassificationAllowed(config, PlayerState))
        {
            return new[] { PlayerState };
        }
        return Array.Empty<string>();
    }

    internal static string DescribeAllowed(AwakeConfig config)
    {
        if (config == null || !config.EnableCloudExport) return "全部禁止";
        return IsClassificationAllowed(config, PlayerState) ? "玩家状态" : "全部禁止";
    }
}
