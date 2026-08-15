using System;

namespace Awake;

internal static class AwakeVersion
{
    internal const string Version = "0.2.0";
    internal const string InformationalVersion = "0.2.0+bannerlord.1.3.15";
}

internal static class AwakeConstants
{
    internal const string LogFileName = "Awake.log";
    internal const string ProbeLogFileName = "AwakeProbe.log";
    // 过渡期 owner：内容 ID 仍在同模块时保持旧值，内容完全拆出后改为 AWAKE。
    internal const string OwnerValue = "AWAKE";
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(90);

    internal const string PermissionPromptRegistryWrite = "prompt.registry.write";
    internal const string PermissionStorageWrite = "storage.namespace.write";
    internal const string PermissionPlayerKnownRead = "data.player_known.read";

    internal static string GetSceneKeywords(string menuId)
    {
        switch (menuId)
        {
            case "town": return "城镇 市集 平民 商旅 酒馆";
            case "castle": return "城堡 贵族 领主 宫廷 骑士";
            case "village": return "村庄 农事 村民 麦田";
            case "town_keep": return "领主府 宫廷 贵族 阴谋";
            case "sea": return "海上 行船 浪涛 舷窗";
            default: return "行军 营地 帐幕 篝火";
        }
    }
}
