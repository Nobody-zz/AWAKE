using System;
using System.Collections.Generic;
using MCM.Abstractions;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using Newtonsoft.Json;

namespace Awake;

public sealed class AwakeConfig : AttributeGlobalSettings<AwakeConfig>
{
    internal const string SettingsId = "AWAKE";

    private static AwakeConfig _instance;

    [JsonIgnore]
    public override string Id => SettingsId;

    [JsonIgnore]
    public override string DisplayName => AwakeLocalization.Resolve("AWAKE.ModuleName", "醒世");

    [JsonIgnore]
    public override string FolderName => "AWAKE";

    [JsonIgnore]
    public override string FormatType => "json";

    [JsonIgnore]
    [SettingPropertyText("{=awake.mcm.ai_status.name}AI 链路状态", Order = 0, RequireRestart = false, HintText = "{=awake.mcm.ai_status.hint}只读显示：Companion 连接、路由能力与候选 Provider 状态。Provider 配置请在 MCM → Marcus AI Framework → 01 Companion 完成，Route ID 使用模组 README 中的四条逻辑路由。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public string AiRuntimeStatus
    {
        get
        {
            string latest = AwakeRuntimeStatus.LatestText;
            return string.IsNullOrWhiteSpace(latest)
                ? AwakeLocalization.Resolve("awake.status.not_checked", "Not checked yet")
                : latest;
        }
        set { }
    }

    [SettingPropertyButton("{=awake.mcm.sync_routes.name}同步路由", -1, true, "", Content = "{=awake.mcm.sync_routes.content}同步路由", Order = 1, RequireRestart = false, HintText = "{=awake.mcm.sync_routes.hint}点击后提示路由由框架自动同步。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public Action SyncRoutes { get; set; }

    [SettingPropertyButton("{=awake.mcm.refresh_status.name}AI 自检", -1, true, "", Content = "{=awake.mcm.refresh_status.content}AI 自检", Order = 2, RequireRestart = false, HintText = "{=awake.mcm.refresh_status.hint}刷新 Companion 连接状态并显示结果。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public Action RefreshAiStatus { get; set; }

    [SettingPropertyBool("{=awake.mcm.cloud_export.name}启用云外发", Order = 0, RequireRestart = false, HintText = "{=awake.mcm.cloud_export.hint}默认关闭。开启后仍需要下面的分类开关与框架权限授权，两层都满足才会把对应分类外发到云 AI。")]
    [SettingPropertyGroup("{=awake.mcm.group.data_debug}4. 数据与调试", GroupOrder = 3)]
    public bool EnableCloudExport { get; set; }

    [SettingPropertyBool("{=awake.mcm.export_player_state.name}允许外发玩家状态", Order = 1, RequireRestart = false, HintText = "{=awake.mcm.export_player_state.hint}允许把玩家、英雄、关系等角色状态作为 player_state 分类随女神对话外发。默认关闭。")]
    [SettingPropertyGroup("{=awake.mcm.group.data_debug}4. 数据与调试", GroupOrder = 3)]
    public bool AllowCloudExportPlayerState { get; set; }

    [SettingPropertyBool("{=awake.mcm.developer_menu.name}启用开发者菜单", Order = 2, RequireRestart = false, HintText = "{=awake.mcm.developer_menu.hint}默认关闭。开启后城镇、城堡、村庄、领主府菜单显示神谕 AI 自检与开发者检查。")]
    [SettingPropertyGroup("{=awake.mcm.group.data_debug}4. 数据与调试", GroupOrder = 3)]
    public bool EnableDeveloperMenu { get; set; }

    [SettingPropertyButton("{=awake.mcm.developer_report.name}开发者检查", -1, true, "", Content = "{=awake.mcm.developer_report.content}打开", Order = 3, RequireRestart = false, HintText = "{=awake.mcm.developer_report.hint}打开运行时诊断报告。")]
    [SettingPropertyGroup("{=awake.mcm.group.data_debug}4. 数据与调试", GroupOrder = 3)]
    public Action OpenDeveloperReport { get; set; }

    [SettingPropertyText("{=awake.mcm.terminal_key.name}命令台快捷键", Order = 3, RequireRestart = false, HintText = "{=awake.mcm.terminal_key.hint}输入 InputKey 名称，例如 U、K、H。")]
    [SettingPropertyGroup("{=awake.mcm.group.command}3. 命令台", GroupOrder = 2)]
    public string TerminalKey { get; set; } = "U";

    [SettingPropertyInteger("{=awake.mcm.scene_max_range.name}场景选人最大距离（米）", 8, 150, Order = 0, RequireRestart = false, HintText = "{=awake.mcm.scene_max_range.hint}按住 T 的最大搜索半径，默认 60。使用三维空间距离，过高会把隔墙或上下楼层的人也纳入候选。")]
    [SettingPropertyGroup("{=awake.mcm.group.scene}1. 对话与场景", GroupOrder = 0)]
    public int SceneMaxRangeMeters { get; set; } = (int)SceneDialogueSelection.DefaultMaxRangeMeters;

    [SettingPropertyBool("{=awake.mcm.npc_proactive.name}启用 NPC 主动", Order = 0, RequireRestart = false, HintText = "{=awake.mcm.npc_proactive.hint}默认开启。开启后附近 NPC 有概率按关系与场合主动发起谈话。")]
    [SettingPropertyGroup("{=awake.mcm.group.behavior}2. 主动行为", GroupOrder = 1)]
    public bool EnableNpcProactive { get; set; } = true;

    [SettingPropertyInteger("{=awake.mcm.npc_proactive_chance.name}NPC 主动概率", 0, 100, Order = 1, RequireRestart = false, HintText = "{=awake.mcm.npc_proactive_chance.hint}主动发起的概率缩放，默认 35。")]
    [SettingPropertyGroup("{=awake.mcm.group.behavior}2. 主动行为", GroupOrder = 1)]
    public int NpcProactiveChance { get; set; } = 35;

    [SettingPropertyBool("{=awake.mcm.event_engine.name}启用事件引擎", Order = 2, RequireRestart = false, HintText = "{=awake.mcm.event_engine.hint}默认开启。事件引擎只负责运行时的触发、冷却与对话动作队列；具体事件内容由后续内容包注册。")]
    [SettingPropertyGroup("{=awake.mcm.group.behavior}2. 主动行为", GroupOrder = 1)]
    public bool EnableEventEngine { get; set; } = true;

    public AwakeConfig()
    {
        _instance = this;
        SyncRoutes = AwakeMcmActions.SyncRoutes;
        RefreshAiStatus = AwakeMcmActions.RefreshAiStatus;
        OpenDeveloperReport = AwakeMcmActions.ShowDeveloperReport;
    }

    internal static new AwakeConfig Instance => _instance;

    public override IEnumerable<ISettingsPreset> GetBuiltInPresets()
    {
        return AwakePresetCatalog.Build();
    }

}

internal static class AwakeRuntimeStatus
{
    internal static string LatestText { get; private set; } = string.Empty;

    internal static void Update(string value)
    {
        LatestText = string.IsNullOrWhiteSpace(value)
            ? AwakeLocalization.Resolve("awake.status.not_checked", "Not checked yet")
            : value;
    }

    internal static void ResetForTesting()
    {
        LatestText = string.Empty;
    }

    internal static void RestoreForTesting(string value)
    {
        LatestText = value ?? string.Empty;
    }
}

internal static class AwakeSettings
{
    private static AwakeConfig _config;

    internal static AwakeConfig Current
    {
        get
        {
            try
            {
                if (BaseSettingsProvider.Instance?.GetSettings(AwakeConfig.SettingsId) is AwakeConfig mcm)
                {
                    _config = mcm;
                    return mcm;
                }
            }
            catch (Exception ex)
            {
                AwakeLog.Write("mcm_settings_lookup_failed error=" + ex.Message);
            }
            return _config ??= new AwakeConfig();
        }
    }

    internal static void UpdateRuntimeStatus(string value)
    {
        try
        {
            AwakeConfig config = Current;
            AwakeRuntimeStatus.Update(value);
            config.OnPropertyChanged(nameof(AwakeConfig.AiRuntimeStatus));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("mcm_runtime_status_update_failed error=" + ex.Message);
        }
    }

    internal static void LogConfigPresence()
    {
        AwakeLog.Write("mcm_ai_config_loaded provider=marcus_framework_in_game");
    }

    internal static void SetConfigForTesting(AwakeConfig config)
    {
        _config = config;
    }

    internal static void ResetConfigForTesting()
    {
        _config = null;
    }
}
