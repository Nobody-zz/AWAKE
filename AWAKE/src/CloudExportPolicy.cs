using System;

namespace Awake;

internal static class CloudExportPolicy
{
    internal const string None = "none";
    internal const string PlayerState = "player_state";

    /// <summary>
    /// NPC 人设派生文本（立绘提示词由角色卡外貌/人设拼出）走这个分类。与 <see cref="PlayerState"/>
    /// 分开是有意的：把角色卡数据说成「玩家状态」是错标，而错标会让玩家那个总开关替它背书。
    /// </summary>
    internal const string NpcPersona = "npc_persona";

    internal static bool IsKnownClassification(string classification)
    {
        return StringComparer.Ordinal.Equals(classification, None)
            || StringComparer.Ordinal.Equals(classification, PlayerState)
            || StringComparer.Ordinal.Equals(classification, NpcPersona);
    }

    internal static bool IsClassificationAllowed(AwakeConfig config, string classification)
    {
        if (StringComparer.Ordinal.Equals(classification, None)) return true;
        if (config == null || !config.EnableCloudExport) return false;
        if (StringComparer.Ordinal.Equals(classification, PlayerState))
        {
            return config.AllowCloudExportPlayerState;
        }

        if (StringComparer.Ordinal.Equals(classification, NpcPersona))
        {
            return config.AllowCloudExportNpcPersona;
        }

        return false;
    }

    internal static string ResolveDialogueClassification(AwakeConfig config)
    {
        return IsClassificationAllowed(config, PlayerState) ? PlayerState : None;
    }

    /// <summary>
    /// 立绘提示词来自 NPC 人设，所以它的分类是 <see cref="NpcPersona"/>；没被允许就退回
    /// <see cref="None"/>（本地端点本来就不外发，走 none 也说得通）。
    /// </summary>
    internal static string ResolvePortraitClassification(AwakeConfig config)
    {
        return IsClassificationAllowed(config, NpcPersona) ? NpcPersona : None;
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
        bool playerState = IsClassificationAllowed(config, PlayerState);
        bool npcPersona = IsClassificationAllowed(config, NpcPersona);
        if (playerState && npcPersona) return "玩家状态、NPC 人设";
        if (playerState) return "玩家状态";
        if (npcPersona) return "NPC 人设";
        return "全部禁止";
    }
}
