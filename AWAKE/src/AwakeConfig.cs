using System;
using System.Collections.Generic;
using MCM.Abstractions;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
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

    // ── 0. AI 链路 ────────────────────────────────────────────────────────────
    // 只装「填配置 + 验证连通」：地址、模型、Key、保存并应用、拉模型、测试连接。
    // 授权开关（允许 AI 对话与结算）归「2. 授权与外发」；
    // 整体状态刷新（AI 自检）归「5. 开发者与诊断」——它刷新的是整栈状态，不只是链路。

    [JsonIgnore]
    [SettingPropertyText("{=awake.mcm.ai_status.name}AI 链路状态", Order = 0, RequireRestart = false, HintText = "{=awake.mcm.ai_status.hint}只读显示 AWAKE Runtime、Provider 和当前配置状态。")]
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

    [SettingPropertyDropdown("{=awake.mcm.provider_kind.name}AI 服务类型", Order = 1, RequireRestart = false, HintText = "{=awake.mcm.provider_kind.hint}选择你实际使用的 AI 服务。OpenAI 兼容也适用于部分本地服务。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public Dropdown<string> ProviderKind { get; set; } = AwakeProviderConfiguration.CreateProviderKindDropdown();

    [SettingPropertyText("{=awake.mcm.provider_url.name}服务地址", Order = 2, RequireRestart = false, HintText = "{=awake.mcm.provider_url.hint}填写服务地址：可填 API 根地址（如 https://api.deepseek.com 或 https://api.openai.com/v1），也可直接粘贴完整接口地址（如 https://api.deepseek.com/chat/completions），两种都接受。不要把 API Key 写进地址。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public string ProviderBaseUrl { get; set; } = "https://api.openai.com/v1";

    [SettingPropertyText("{=awake.mcm.provider_model.name}模型名称", Order = 3, RequireRestart = false, HintText = "{=awake.mcm.provider_model.hint}填写 Provider 返回的模型 ID；可先点击“拉取可用模型”查看。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public string ProviderModel { get; set; } = "gpt-4o-mini";

    [SettingPropertyBool("{=awake.mcm.provider_cloud.name}这是云端服务", Order = 4, RequireRestart = false, HintText = "{=awake.mcm.provider_cloud.hint}云端服务通常需要 API Key；本机 Ollama 一般关闭此项。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public bool ProviderIsCloud { get; set; } = true;

    [JsonIgnore]
    [SettingPropertyText("{=awake.mcm.provider_model_status.name}模型读取状态", Order = 5, RequireRestart = false, HintText = "{=awake.mcm.provider_model_status.hint}只读显示最近一次模型列表读取结果。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public string ProviderModelStatus
    {
        get { return AwakeProviderConfiguration.ModelStatus; }
        set { }
    }

    [SettingPropertyButton("{=awake.mcm.provider_api_key.name}输入或替换 API Key", -1, true, "", Content = "{=awake.mcm.provider_api_key.content}打开输入框", Order = 6, RequireRestart = false, HintText = "{=awake.mcm.provider_api_key.hint}输入框保持可见，便于核对；密钥只写入本机保护存储，不进入 MCM、存档或日志。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public Action ConfigureProviderApiKey { get; set; }

    [SettingPropertyButton("{=awake.mcm.provider_apply.name}保存配置并应用", -1, true, "", Content = "{=awake.mcm.provider_apply.content}保存并应用", Order = 7, RequireRestart = false, HintText = "{=awake.mcm.provider_apply.hint}把服务类型、地址和模型应用到 AWAKE 的全部 AI 路由。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public Action ApplyProviderConfiguration { get; set; }

    [SettingPropertyButton("{=awake.mcm.provider_models.name}拉取可用模型", -1, true, "", Content = "{=awake.mcm.provider_models.content}拉取模型", Order = 8, RequireRestart = false, HintText = "{=awake.mcm.provider_models.hint}先应用当前地址和服务类型，再读取 Provider 返回的模型列表。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public Action PullProviderModels { get; set; }

    [SettingPropertyButton("{=awake.mcm.provider_test.name}测试连接", -1, true, "", Content = "{=awake.mcm.provider_test.content}测试连接", Order = 9, RequireRestart = false, HintText = "{=awake.mcm.provider_test.hint}通过读取模型列表测试 Runtime、地址、凭据和 Provider 是否连通。")]
    [SettingPropertyGroup("{=awake.mcm.group.ai_link}0. AI 链路", GroupOrder = -1)]
    public Action TestProviderConnection { get; set; }

    // ── 1. 图片生成 ────────────────────────────────────────────────────────────
    // 与「0. AI 链路」相邻，因为两块都是 AI 设置；但不共享地址、也不共享 Key ——
    // AI 链路的 Key 存在 Runtime 进程（net8）的保护存储里，模组进程（net472）读不到，
    // 也没有回读接口。生图要在模组侧自己出网，就必须有自己的地址和自己的钥匙。

    [JsonIgnore]
    [SettingPropertyText("{=awake.mcm.image_scope.name}本组与 AI 链路分开", Order = 0, RequireRestart = false, HintText = "{=awake.mcm.image_scope.hint}出图走 AWAKE 自己的出网口，所以需要自己的地址与凭据；「0. AI 链路」那组只管对话，两组不共享地址也不共享 Key。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public string PortraitImageScopeNote
    {
        get
        {
            return "出图有独立的地址与独立的密钥，与上面「0. AI 链路」互不影响；两把 Key 分开保存，配一个不会动另一个。";
        }
        set { }
    }

    [SettingPropertyDropdown("{=awake.mcm.image_shape.name}出图接口形状", Order = 1, RequireRestart = false, HintText = "{=awake.mcm.image_shape.hint}Player2 是 base64 进、base64 出；OpenAI 兼容适用于多数服务商与自建中转。形状选错通常表现为 404 或“响应里没有图片字段”。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public Dropdown<string> PortraitImageShape { get; set; } = AwakeImageEndpointResolver.CreateShapeDropdown();

    [SettingPropertyText("{=awake.mcm.image_url.name}出图服务地址", Order = 2, RequireRestart = false, HintText = "{=awake.mcm.image_url.hint}填写出图接口的 API 根地址，或直接粘贴完整接口地址，两种都接受。例：本机 Player2 App 为 http://127.0.0.1:4315/v1；云端 Player2 为 https://api.player2.game/v1；火山方舟为 https://ark.cn-beijing.volces.com/api/v3。不要把 API Key 写进地址。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public string PortraitImageBaseUrl { get; set; } = string.Empty;

    [SettingPropertyBool("{=awake.mcm.image_cloud.name}出图走云端服务", Order = 3, RequireRestart = false, HintText = "{=awake.mcm.image_cloud.hint}云端才带 API Key；本机 App、本机推理等免认证服务请关闭此项。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public bool PortraitImageIsCloud { get; set; }

    [SettingPropertyText("{=awake.mcm.image_model.name}出图模型名", Order = 4, RequireRestart = false, HintText = "{=awake.mcm.image_model.hint}只有「OpenAI 兼容」形状需要填：运行时那边的 profile 必须带一个非空的默认模型名。Player2 形状不认模型名，留空即可。留空且形状是 OpenAI 兼容时，立绘会退回模组侧直连那条老路。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public string PortraitImageModel { get; set; } = string.Empty;

    [SettingPropertyButton("{=awake.mcm.image_key.name}输入或替换出图 API Key", -1, true, "", Content = "{=awake.mcm.image_key.content}打开输入框", Order = 5, RequireRestart = false, HintText = "{=awake.mcm.image_key.hint}输入框保持可见，便于核对；密钥只写入本机保护存储，不进入 MCM、存档或日志。它与 AI 链路的 Key 各存各处。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public Action ConfigureImageApiKey { get; set; }

    [SettingPropertyInteger("{=awake.mcm.image_width.name}出图宽度（像素）", 128, 2048, Order = 6, RequireRestart = false, HintText = "{=awake.mcm.image_width.hint}请求尺寸，默认 512。注意服务端有权不遵守：实测有的端点直接忽略它出更大尺寸，所以落盘与布局以实际像素为准。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public int PortraitImageWidth { get; set; } = 512;

    [SettingPropertyInteger("{=awake.mcm.image_height.name}出图高度（像素）", 128, 2048, Order = 7, RequireRestart = false, HintText = "{=awake.mcm.image_height.hint}请求尺寸，默认 512。同样不保证被遵守，以实际产出为准。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public int PortraitImageHeight { get; set; } = 512;

    [SettingPropertyButton("{=awake.mcm.image_probe.name}测试出图", -1, true, "", Content = "{=awake.mcm.image_probe.content}打一发", Order = 8, RequireRestart = false, HintText = "{=awake.mcm.image_probe.hint}真往填好的地址打一发，并把图存到本机立绘目录，路径会回显。这是唯一能证明“地址 + 钥匙真的能用”的动作，会消耗服务端额度。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public Action TestImageGeneration { get; set; }

    [JsonIgnore]
    [SettingPropertyText("{=awake.mcm.image_status.name}出图状态", Order = 9, RequireRestart = false, HintText = "{=awake.mcm.image_status.hint}只读显示密钥是否已配置，以及最近一次测试出图的结果。")]
    [SettingPropertyGroup("{=awake.mcm.group.image}1. 图片生成", GroupOrder = 0)]
    public string PortraitImageStatus
    {
        get
        {
            return "Key " + AwakeImageSecretStore.DescribePresence() + " · " + AwakeImageProbe.Status;
        }
        set { }
    }

    // ── 2. 授权与外发 ──────────────────────────────────────────────────────────
    // 三个"许可"开关集中在一组：允许 AI 运行（默认关）、允许云外发、允许外发玩家状态。
    // 单独成组是因为它们是许可而不是配置；原先散在「0. AI 链路」和「4. 数据与调试」
    // 两处，玩家最容易漏掉那个默认关闭的总开关。

    [SettingPropertyBool("{=awake.mcm.allow_ai_routing.name}允许 AI 对话与结算", Order = 0, RequireRestart = false, HintText = "{=awake.mcm.allow_ai_routing.hint}默认关闭。这是显式授权开关：开启后 AWAKE 才会把对话提交给 AI 路由、按分类外发数据，并结算 AI 产出的关系与世界状态变更；关闭时只走本地兜底文案。")]
    [SettingPropertyGroup("{=awake.mcm.group.auth}2. 授权与外发", GroupOrder = 1)]
    public bool AllowAiRouting { get; set; }

    [SettingPropertyBool("{=awake.mcm.cloud_export.name}启用云外发", Order = 1, RequireRestart = false, HintText = "{=awake.mcm.cloud_export.hint}默认开启。关闭后本机 Ollama 等本地链路不受影响；开启云端对话仍需框架权限授权。")]
    [SettingPropertyGroup("{=awake.mcm.group.auth}2. 授权与外发", GroupOrder = 1)]
    public bool EnableCloudExport { get; set; } = true;

    [SettingPropertyBool("{=awake.mcm.export_player_state.name}允许外发玩家状态", Order = 2, RequireRestart = false, HintText = "{=awake.mcm.export_player_state.hint}允许把玩家、英雄、关系等角色状态作为 player_state 分类随 NPC 对话外发。默认开启。")]
    [SettingPropertyGroup("{=awake.mcm.group.auth}2. 授权与外发", GroupOrder = 1)]
    public bool AllowCloudExportPlayerState { get; set; } = true;

    [SettingPropertyBool("{=awake.mcm.export_npc_persona.name}允许外发 NPC 人设", Order = 3, RequireRestart = false, HintText = "{=awake.mcm.export_npc_persona.hint}允许把角色卡外貌与人设拼出的立绘提示词作为 npc_persona 分类外发给云端生图 Provider。默认关闭：这是一项单独授权，不随玩家状态一起放开。")]
    [SettingPropertyGroup("{=awake.mcm.group.auth}2. 授权与外发", GroupOrder = 1)]
    public bool AllowCloudExportNpcPersona { get; set; }

    // ── 3. 对话与场景 ──────────────────────────────────────────────────────────
    // 玩家侧的人物选取与按键。原先的「3. 命令台」只有一项按键设置，已并入本组末尾 ——
    // 它和 T / [ / ] / V 是同一类东西，不该独占一个顶级分组。

    [SettingPropertyInteger("{=awake.mcm.scene_max_range.name}场景选人最大距离（米）", 8, 150, Order = 0, RequireRestart = false, HintText = "{=awake.mcm.scene_max_range.hint}按住 T 的最大搜索半径，默认 60。使用三维空间距离，过高会把隔墙或上下楼层的人也纳入候选。")]
    [SettingPropertyGroup("{=awake.mcm.group.scene}3. 对话与场景", GroupOrder = 2)]
    public int SceneMaxRangeMeters { get; set; } = (int)SceneDialogueSelection.DefaultMaxRangeMeters;

    [SettingPropertyBool("{=awake.mcm.scene_visual_selection.name}启用场景可视化选人", Order = 1, RequireRestart = false, HintText = "{=awake.mcm.scene_visual_selection.hint}默认开启。按住 T 时显示地面范围、候选轮廓与当前目标脉冲；关闭时回退为基础文字提示。")]
    [SettingPropertyGroup("{=awake.mcm.group.scene}3. 对话与场景", GroupOrder = 2)]
    public bool EnableSceneVisualSelection { get; set; } = true;

    [SettingPropertyText("{=awake.mcm.scene_near_to_far_key.name}近到远选人键", Order = 2, RequireRestart = false, HintText = "{=awake.mcm.scene_near_to_far_key.hint}输入 InputKey 名称或 [ ] 字面键，默认 [。")]
    [SettingPropertyGroup("{=awake.mcm.group.scene}3. 对话与场景", GroupOrder = 2)]
    public string SceneCycleNearToFarKey { get; set; } = "[";

    [SettingPropertyText("{=awake.mcm.scene_far_to_near_key.name}远到近选人键", Order = 3, RequireRestart = false, HintText = "{=awake.mcm.scene_far_to_near_key.hint}输入 InputKey 名称或 [ ] 字面键，默认 ]。")]
    [SettingPropertyGroup("{=awake.mcm.group.scene}3. 对话与场景", GroupOrder = 2)]
    public string SceneCycleFarToNearKey { get; set; } = "]";

    [SettingPropertyText("{=awake.mcm.scene_shout_key.name}场景喊话键", Order = 4, RequireRestart = false, HintText = "{=awake.mcm.scene_shout_key.hint}按住 T 后按此键进入无目标场景喊话，默认 V。")]
    [SettingPropertyGroup("{=awake.mcm.group.scene}3. 对话与场景", GroupOrder = 2)]
    public string SceneShoutKey { get; set; } = "V";

    [SettingPropertyText("{=awake.mcm.terminal_key.name}命令台快捷键", Order = 5, RequireRestart = false, HintText = "{=awake.mcm.terminal_key.hint}输入 InputKey 名称，例如 U、K、H。")]
    [SettingPropertyGroup("{=awake.mcm.group.scene}3. 对话与场景", GroupOrder = 2)]
    public string TerminalKey { get; set; } = "U";

    // ── 4. 主动行为 ────────────────────────────────────────────────────────────

    [SettingPropertyBool("{=awake.mcm.npc_proactive.name}启用 NPC 主动", Order = 0, RequireRestart = false, HintText = "{=awake.mcm.npc_proactive.hint}默认开启。开启后附近 NPC 有概率按关系与场合主动发起谈话。")]
    [SettingPropertyGroup("{=awake.mcm.group.behavior}4. 主动行为", GroupOrder = 3)]
    public bool EnableNpcProactive { get; set; } = true;

    [SettingPropertyInteger("{=awake.mcm.npc_proactive_chance.name}NPC 主动概率", 0, 100, Order = 1, RequireRestart = false, HintText = "{=awake.mcm.npc_proactive_chance.hint}最终扰动比例，默认 35；实际触发优先由关系/事件条件决定。")]
    [SettingPropertyGroup("{=awake.mcm.group.behavior}4. 主动行为", GroupOrder = 3)]
    public int NpcProactiveChance { get; set; } = 35;

    [SettingPropertyBool("{=awake.mcm.event_engine.name}启用事件引擎", Order = 2, RequireRestart = false, HintText = "{=awake.mcm.event_engine.hint}默认开启。事件引擎只负责运行时的触发、冷却与对话动作队列；具体事件内容由后续内容包注册。")]
    [SettingPropertyGroup("{=awake.mcm.group.behavior}4. 主动行为", GroupOrder = 3)]
    public bool EnableEventEngine { get; set; } = true;

    [SettingPropertyBool("{=awake.mcm.guide.name}启用游戏内引导", Order = 3, RequireRestart = false, HintText = "{=awake.mcm.guide.hint}默认开启。新战役在安全地图状态自动提醒完成首启引导；可在命令台手动重开。")]
    [SettingPropertyGroup("{=awake.mcm.group.behavior}4. 主动行为", GroupOrder = 3)]
    public bool EnableInGameGuide { get; set; } = true;

    [SettingPropertyInteger("{=awake.mcm.guide_interval.name}引导重复间隔（天）", 1, 14, Order = 4, RequireRestart = false, HintText = "{=awake.mcm.guide_interval.hint}未完成引导时每隔多少游戏日提醒一次，默认 2。")]
    [SettingPropertyGroup("{=awake.mcm.group.behavior}4. 主动行为", GroupOrder = 3)]
    public int GuideRepeatIntervalDays { get; set; } = 2;

    // ── 5. 开发者与诊断 ────────────────────────────────────────────────────────
    // 默认关闭的一类：开发者菜单、诊断报告、整体状态自检。
    // 「AI 自检」原先排在「0. AI 链路」里，但它刷新的是整栈状态（Runtime + Provider），
    // 不是链路配置，放在这里更贴。

    [SettingPropertyBool("{=awake.mcm.developer_menu.name}启用开发者菜单", Order = 0, RequireRestart = false, HintText = "{=awake.mcm.developer_menu.hint}默认关闭。开启后城镇、城堡、村庄、领主府菜单显示 AWAKE 自检与开发者检查。")]
    [SettingPropertyGroup("{=awake.mcm.group.dev}5. 开发者与诊断", GroupOrder = 4)]
    public bool EnableDeveloperMenu { get; set; }

    [SettingPropertyButton("{=awake.mcm.developer_report.name}开发者检查", -1, true, "", Content = "{=awake.mcm.developer_report.content}打开", Order = 1, RequireRestart = false, HintText = "{=awake.mcm.developer_report.hint}打开运行时诊断报告。")]
    [SettingPropertyGroup("{=awake.mcm.group.dev}5. 开发者与诊断", GroupOrder = 4)]
    public Action OpenDeveloperReport { get; set; }

    [SettingPropertyButton("{=awake.mcm.refresh_status.name}AI 自检", -1, true, "", Content = "{=awake.mcm.refresh_status.content}AI 自检", Order = 2, RequireRestart = false, HintText = "{=awake.mcm.refresh_status.hint}刷新 AWAKE Runtime 和 Provider 状态。")]
    [SettingPropertyGroup("{=awake.mcm.group.dev}5. 开发者与诊断", GroupOrder = 4)]
    public Action RefreshAiStatus { get; set; }

    public AwakeConfig()
    {
        _instance = this;
        ConfigureProviderApiKey = AwakeMcmActions.ConfigureProviderApiKey;
        ApplyProviderConfiguration = AwakeMcmActions.ApplyProviderConfiguration;
        PullProviderModels = AwakeMcmActions.PullProviderModels;
        TestProviderConnection = AwakeMcmActions.TestProviderConnection;
        RefreshAiStatus = AwakeMcmActions.RefreshAiStatus;
        ConfigureImageApiKey = AwakeMcmActions.ConfigureImageApiKey;
        TestImageGeneration = AwakeMcmActions.TestImageGeneration;
        OpenDeveloperReport = () => AwakeMcmActions.ShowDeveloperReport();
    }

    internal static new AwakeConfig Instance => _instance;

    public override IEnumerable<ISettingsPreset> GetBuiltInPresets()
    {
        return AwakePresetCatalog.Build();
    }

}

internal static class AwakeRuntimeStatus
{
    private static readonly object StateGate = new object();
    private static string latestText = string.Empty;

    internal static string LatestText
    {
        get
        {
            lock (StateGate) return latestText;
        }
    }

    internal static void Update(string value)
    {
        lock (StateGate)
        {
            latestText = string.IsNullOrWhiteSpace(value)
                ? AwakeLocalization.Resolve("awake.status.not_checked", "Not checked yet")
                : value;
        }
    }

    internal static void ResetForTesting()
    {
        lock (StateGate) latestText = string.Empty;
    }

    internal static void RestoreForTesting(string value)
    {
        lock (StateGate) latestText = value ?? string.Empty;
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

    internal static bool TrySaveCurrentConfiguration(out string error)
    {
        error = string.Empty;
        try
        {
            AwakeConfig config = Current;
            if (BaseSettingsProvider.Instance == null)
            {
                error = "MCM 设置服务尚未就绪，配置未保存。";
                return false;
            }

            BaseSettingsProvider.Instance.SaveSettings(config);
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("mcm_configuration_save_failed error=" + ex.Message);
            error = "MCM 配置保存失败，请稍后重试。";
            return false;
        }
    }

    internal static void NotifyProviderStatusChanged()
    {
        try
        {
            Current.OnPropertyChanged(nameof(AwakeConfig.AiRuntimeStatus));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("mcm_provider_status_update_failed error=" + ex.Message);
        }
    }

    internal static void NotifyProviderModelStatusChanged()
    {
        try
        {
            Current.OnPropertyChanged(nameof(AwakeConfig.ProviderModelStatus));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("mcm_provider_model_status_update_failed error=" + ex.Message);
        }
    }

    internal static void NotifyPortraitImageStatusChanged()
    {
        try
        {
            Current.OnPropertyChanged(nameof(AwakeConfig.PortraitImageStatus));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("mcm_image_status_update_failed error=" + ex.Message);
        }
    }

    internal static void NormalizeLegacySceneShoutKey()
    {
        try
        {
            AwakeConfig config = Current;
            string raw = (config.SceneShoutKey ?? string.Empty).Trim();
            if (!StringComparer.OrdinalIgnoreCase.Equals(raw, "C")
                && !StringComparer.OrdinalIgnoreCase.Equals(raw, "U"))
            {
                return;
            }
            config.SceneShoutKey = "V";
            try
            {
                BaseSettingsProvider.Instance?.SaveSettings(config);
            }
            catch (Exception saveEx)
            {
                AwakeLog.Write("mcm_scene_shout_key_normalize_save_failed error=" + saveEx.Message);
            }
            AwakeLog.Write("mcm_scene_shout_key_normalized raw=" + raw);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("mcm_scene_shout_key_normalize_failed error=" + ex.Message);
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
