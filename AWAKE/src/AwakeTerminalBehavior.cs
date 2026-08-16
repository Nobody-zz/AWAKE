using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace Awake;

internal sealed class AwakeTerminalBehavior : CampaignBehaviorBase
{
    private const float OpenCooldownSeconds = 0.35f;
    private const float TerminalKeyRefreshIntervalSeconds = 1f;

    private static readonly Stopwatch TerminalClock = Stopwatch.StartNew();

    internal static AwakeTerminalBehavior Current { get; private set; }

    private bool _wasKeyDown;
    private static bool _terminalUiActive;
    private int _sceneSelectedAgentIndex = -1;
    private string _sceneSelectedTargetId = string.Empty;
    private bool _sceneHoldActive;
    private float _sceneHoldStartRealTime = -999f;
    private float _sceneCurrentRangeMeters = SceneDialogueSelection.MinRangeMeters;
    private float _lastOpenRealTime = -999f;
    private float _nextTerminalKeyRefreshRealTime = -999f;
    private InputKey _cachedTerminalKey = InputKey.U;
    private string _cachedTerminalKeyRaw = string.Empty;
    private static string _lastBlockReason = string.Empty;

    internal AwakeTerminalBehavior()
    {
        Current = this;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, args => NpcDialogueLauncher.ClearCache());
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    internal static void TickCurrent()
    {
        try
        {
            Current?.OnTick();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_terminal_tick_error error=" + ex.Message);
        }
    }

    private void OnTick()
    {
        if (TryProcessSceneDialogue())
        {
            return;
        }
        bool keyDown = false;
        try
        {
            keyDown = Input.IsKeyDown(GetTerminalKey());
        }
        catch
        {
            keyDown = false;
        }
        if (!keyDown)
        {
            _wasKeyDown = false;
            return;
        }
        if (_wasKeyDown || _terminalUiActive)
        {
            return;
        }
        if (!CanOpenTerminal())
        {
            _wasKeyDown = true;
            AwakeLog.Write("awake_terminal_blocked reason=" + _lastBlockReason);
            AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                "awake.feedback.terminal_blocked",
                "当前无法打开命令台。"));
            return;
        }
        _wasKeyDown = true;
        float now = (float)TerminalClock.Elapsed.TotalSeconds;
        if (now - _lastOpenRealTime < OpenCooldownSeconds)
        {
            return;
        }
        _lastOpenRealTime = now;
        OpenRootMenu();
    }

    private bool TryProcessSceneDialogue()
    {
        if (Mission.Current == null)
        {
            AbortSceneSelection();
            return false;
        }
        if (!CanUseSceneDialogue())
        {
            AbortSceneSelection();
            return false;
        }

        bool tDown = false;
        try
        {
            tDown = Input.IsKeyDown(InputKey.T);
        }
        catch
        {
            tDown = false;
        }

        if (tDown)
        {
            float now = (float)TerminalClock.Elapsed.TotalSeconds;
            if (!_sceneHoldActive)
            {
                _sceneHoldActive = true;
                _sceneHoldStartRealTime = now;
                _sceneCurrentRangeMeters = SceneDialogueSelection.CurrentRange(0f, GetSceneMaxRange());
                ClearSceneSelection();
                ShowSceneMessage(AwakeLocalization.Resolve(
                    "awake.scene.hold_hint",
                    "按住 T 扩大距离范围，按 Y 切换，松开 T 开始对话。"));
            }
            else
            {
                _sceneCurrentRangeMeters = SceneDialogueSelection.CurrentRange(
                    Math.Max(0f, now - _sceneHoldStartRealTime),
                    GetSceneMaxRange());
            }

            if (Input.IsKeyPressed(InputKey.Y))
            {
                CycleSceneTarget();
            }
            return true;
        }

        if (_sceneHoldActive)
        {
            _sceneCurrentRangeMeters = SceneDialogueSelection.CurrentRange(
                Math.Max(0f, (float)TerminalClock.Elapsed.TotalSeconds - _sceneHoldStartRealTime),
                GetSceneMaxRange());
            _sceneHoldActive = false;
            ConfirmSceneTarget();
            return true;
        }

        return true;
    }

    private bool CanUseSceneDialogue()
    {
        try
        {
            if (Mission.Current == null || Campaign.Current == null) return false;
            if (Campaign.Current.ConversationManager != null
                && Campaign.Current.ConversationManager.IsConversationInProgress)
            {
                return false;
            }
            if (NpcDialogueOverlay.IsOpen || AwakeMessengerOverlay.IsOpen) return false;
            try
            {
                if (InformationManager.IsAnyInquiryActive()) return false;
            }
            catch
            {
            }
            try
            {
                if (Input.IsOnScreenKeyboardActive) return false;
            }
            catch
            {
            }
            try
            {
                ScreenLayer focusedLayer = ScreenManager.FocusedLayer;
                if (focusedLayer != null && focusedLayer.IsFocusedOnInput()) return false;
            }
            catch
            {
            }
            MissionMode mode = Mission.Current.Mode;
            return mode != MissionMode.Battle
                && mode != MissionMode.Deployment
                && mode != MissionMode.Duel
                && mode != MissionMode.Stealth
                && mode != MissionMode.Tournament;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_scene_dialogue_can_open_error error=" + ex.Message);
            return false;
        }
    }

    private void CycleSceneTarget()
    {
        List<AwakeNpcTarget> candidates = GetSceneCandidates();
        if (candidates.Count == 0)
        {
            ClearSceneSelection();
            ShowSceneMessage(AwakeLocalization.Resolve("awake.scene.no_candidates", "附近没有可以对话的人物。"));
            return;
        }

        int currentIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].AgentIndex == _sceneSelectedAgentIndex
                || StringComparer.Ordinal.Equals(candidates[i].StableId, _sceneSelectedTargetId))
            {
                currentIndex = i;
                break;
            }
        }
        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % candidates.Count;
        SelectSceneTarget(candidates[nextIndex]);
    }

    private void ConfirmSceneTarget()
    {
        List<AwakeNpcTarget> candidates = GetSceneCandidates();
        AwakeNpcTarget target = null;
        foreach (AwakeNpcTarget candidate in candidates)
        {
            if (candidate.AgentIndex == _sceneSelectedAgentIndex
                || StringComparer.Ordinal.Equals(candidate.StableId, _sceneSelectedTargetId))
            {
                target = candidate;
                break;
            }
        }
        if (target == null && candidates.Count > 0)
        {
            target = candidates[0];
        }
        if (target == null)
        {
            ClearSceneSelection();
            ShowSceneMessage(AwakeLocalization.Resolve("awake.scene.no_candidates", "附近没有可以对话的人物。"));
            return;
        }
        SelectSceneTarget(target, showHint: false);
        NpcDialogueLaunchResult result = NpcDialogueLauncher.TryOpenDialogue(target, "scene");
        ClearSceneSelection();
        if (result == NpcDialogueLaunchResult.None)
        {
            ShowSceneMessage(AwakeLocalization.Resolve("awake.scene.confirm_unavailable", "对方暂时无法交谈。"));
        }
    }

    private void SelectSceneTarget(AwakeNpcTarget target, bool showHint = true)
    {
        if (target == null || target.AgentIndex < 0)
        {
            ClearSceneSelection();
            return;
        }
        SetSceneAgentHighlight(_sceneSelectedAgentIndex, false);
        _sceneSelectedAgentIndex = target.AgentIndex;
        _sceneSelectedTargetId = target.StableId;
        SetSceneAgentHighlight(_sceneSelectedAgentIndex, true);
        if (showHint)
        {
            string rangeText = _sceneCurrentRangeMeters.ToString("0");
            ShowSceneMessage(AwakeLocalization.Resolve(
                "awake.scene.select_hint",
                "已选中 " + target.DisplayName + "（" + rangeText + "m），按 Y 切换，松开 T 开始对话。",
                new Dictionary<string, string>
                {
                    ["NAME"] = target.DisplayName,
                    ["RANGE"] = rangeText
                }));
        }
    }

    private List<AwakeNpcTarget> GetSceneCandidates()
    {
        Agent mainAgent = Mission.Current?.MainAgent ?? Agent.Main;
        if (mainAgent == null || !mainAgent.IsActive())
        {
            return new List<AwakeNpcTarget>();
        }

        float rangeSquared = _sceneCurrentRangeMeters * _sceneCurrentRangeMeters;
        List<Tuple<AwakeNpcTarget, float>> scored = new List<Tuple<AwakeNpcTarget, float>>();
        foreach (AwakeNpcTarget target in NpcDialogueLauncher.GetSceneTargets(64))
        {
            if (target == null
                || target.AgentIndex < 0
                || !NpcDialogueLauncher.IsEligibleNpcTarget(target))
            {
                continue;
            }
            Agent agent = NpcDialogueLauncher.GetActiveAgent(target.AgentIndex);
            if (agent == null || !agent.IsActive())
            {
                continue;
            }

            float distanceSquared = mainAgent.Position.DistanceSquared(agent.Position);
            if (float.IsNaN(distanceSquared)
                || float.IsInfinity(distanceSquared)
                || distanceSquared > rangeSquared)
            {
                continue;
            }

            float distanceMeters = (float)Math.Sqrt(distanceSquared);
            scored.Add(Tuple.Create(target, distanceMeters));
        }

        scored.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        List<AwakeNpcTarget> result = new List<AwakeNpcTarget>();
        foreach (Tuple<AwakeNpcTarget, float> item in scored)
        {
            result.Add(item.Item1);
        }
        return result;
    }

    private void AbortSceneSelection()
    {
        _sceneHoldActive = false;
        _sceneHoldStartRealTime = -999f;
        _sceneCurrentRangeMeters = SceneDialogueSelection.MinRangeMeters;
        ClearSceneSelection();
    }

    private static float GetSceneMaxRange()
    {
        try
        {
            return SceneDialogueSelection.ClampMax(AwakeSettings.Current.SceneMaxRangeMeters);
        }
        catch
        {
            return SceneDialogueSelection.DefaultMaxRangeMeters;
        }
    }

    private void ClearSceneSelection()
    {
        if (_sceneSelectedAgentIndex >= 0)
        {
            SetSceneAgentHighlight(_sceneSelectedAgentIndex, false);
        }
        _sceneSelectedAgentIndex = -1;
        _sceneSelectedTargetId = string.Empty;
    }

    private static void SetSceneAgentHighlight(int agentIndex, bool enabled)
    {
        if (agentIndex < 0 || Mission.Current?.Agents == null) return;
        foreach (Agent agent in Mission.Current.Agents)
        {
            if (agent == null || agent.Index != agentIndex || agent.AgentVisuals == null) continue;
            try
            {
                uint? color = enabled
                    ? (uint?)new Color(1f, 0.84f, 0.2f, 1f).ToUnsignedInteger()
                    : null;
                agent.AgentVisuals.SetContourColor(color);
            }
            catch (Exception ex)
            {
                AwakeLog.Write("awake_scene_agent_highlight_error index=" + agentIndex + " error=" + ex.Message);
            }
            return;
        }
    }

    private static void ShowSceneMessage(string text)
    {
        try
        {
            InformationManager.DisplayMessage(new InformationMessage(text, new Color(0.45f, 0.9f, 0.45f)));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_scene_show_message_error error=" + ex.Message);
        }
    }

    private InputKey GetTerminalKey()
    {
        float now = (float)TerminalClock.Elapsed.TotalSeconds;
        if (now < _nextTerminalKeyRefreshRealTime)
        {
            return _cachedTerminalKey;
        }
        _nextTerminalKeyRefreshRealTime = now + TerminalKeyRefreshIntervalSeconds;
        try
        {
            string raw = AwakeSettings.Current.TerminalKey ?? "U";
            raw = raw.Trim();
            if (!StringComparer.Ordinal.Equals(raw, _cachedTerminalKeyRaw))
            {
                _cachedTerminalKeyRaw = raw;
                _cachedTerminalKey = ParseTerminalKey(raw);
            }
        }
        catch
        {
            _cachedTerminalKeyRaw = string.Empty;
            _cachedTerminalKey = InputKey.U;
        }
        return _cachedTerminalKey;
    }

    private static InputKey ParseTerminalKey(string raw)
    {
        if (!string.IsNullOrWhiteSpace(raw)
            && Enum.TryParse<InputKey>(raw.ToUpperInvariant(), out InputKey parsed)
            && parsed != InputKey.Invalid)
        {
            return parsed;
        }
        return InputKey.U;
    }

    private static bool CanOpenTerminal()
    {
        try
        {
            if (Campaign.Current == null)
            {
                _lastBlockReason = "no_campaign";
                return false;
            }
            if (Mission.Current != null)
            {
                try
                {
                    MissionMode mode = Mission.Current.Mode;
                    if (mode == MissionMode.Battle
                        || mode == MissionMode.Deployment
                        || mode == MissionMode.Duel
                        || mode == MissionMode.Stealth
                        || mode == MissionMode.Tournament)
                    {
                        _lastBlockReason = "mission_mode:" + mode;
                        return false;
                    }
                }
                catch
                {
                    _lastBlockReason = "mission_mode_error";
                    return false;
                }
            }
            if (Campaign.Current.ConversationManager != null
                && Campaign.Current.ConversationManager.IsConversationInProgress)
            {
                _lastBlockReason = "conversation_in_progress";
                return false;
            }
            if (NpcDialogueOverlay.IsOpen || AwakeMessengerOverlay.IsOpen)
            {
                _lastBlockReason = "overlay_open";
                return false;
            }
            try
            {
                if (InformationManager.IsAnyInquiryActive())
                {
                    _lastBlockReason = "inquiry_active";
                    return false;
                }
            }
            catch
            {
            }
            try
            {
                if (Input.IsOnScreenKeyboardActive)
                {
                    _lastBlockReason = "onscreen_keyboard";
                    return false;
                }
            }
            catch
            {
            }
            try
            {
                ScreenLayer focusedLayer = ScreenManager.FocusedLayer;
                if (focusedLayer != null && focusedLayer.IsFocusedOnInput())
                {
                    _lastBlockReason = "input_focused";
                    return false;
                }
            }
            catch
            {
            }
            _lastBlockReason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_terminal_can_open_error error=" + ex.Message);
            return false;
        }
    }

    private void OpenRootMenu()
    {
        _terminalUiActive = true;
        List<InquiryElement> elements = new List<InquiryElement>
        {
            new InquiryElement(
                "messenger",
                AwakeLocalization.Resolve("awake.menu.messenger", "通讯录（醒世）"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.messenger_hint", "打开通讯录并开始对话")),
            new InquiryElement(
                "inbox",
                AwakeLocalization.Resolve("awake.menu.inbox", "事件收件箱"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.inbox_hint", "查看近期世界事件")),
            new InquiryElement(
                "weekly_report",
                AwakeLocalization.Resolve("awake.menu.weekly_report", "世界周报"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.weekly_hint", "查看本周世界摘要")),
            new InquiryElement(
                "developer_report",
                AwakeLocalization.Resolve("awake.menu.developer_check", "开发者检查"),
                (ImageIdentifier)null,
                AwakeSettings.Current.EnableDeveloperMenu,
                AwakeLocalization.Resolve("awake.terminal.developer_hint", "查看运行时诊断")),
            new InquiryElement(
                "dev_tools",
                AwakeLocalization.Resolve("awake.menu.dev_tools", "开发者测试"),
                (ImageIdentifier)null,
                AwakeSettings.Current.EnableDeveloperMenu,
                AwakeLocalization.Resolve("awake.terminal.dev_tools_hint", "游戏内测试工具"))
        };
        MultiSelectionInquiryData data = new MultiSelectionInquiryData(
            AwakeLocalization.Resolve("awake.terminal.title", "醒世 · 命令台"),
            AwakeLocalization.Resolve("awake.terminal.prompt", "选择功能："),
            elements,
            true,
            1,
            1,
            AwakeLocalization.Resolve("awake.terminal.confirm", "确定"),
            AwakeLocalization.Resolve("awake.terminal.close", "关闭"),
            selected =>
            {
                _terminalUiActive = false;
                HandleRootSelection(selected);
            },
            _ => _terminalUiActive = false,
            "",
            false);
        MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
    }

    private static void HandleRootSelection(List<InquiryElement> selected)
    {
        if (selected == null || selected.Count == 0) return;
        string id = selected[0].Identifier as string;
        if (StringComparer.Ordinal.Equals(id, "inbox"))
        {
            ShowWorldInbox();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "weekly_report"))
        {
            ShowWeeklyReport();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "messenger"))
        {
            AwakeMessengerOverlay.Open();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "developer_report"))
        {
            ShowDeveloperReport();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "dev_tools"))
        {
            OpenDeveloperTestTools();
        }
    }

    private static void OpenDeveloperTestTools()
    {
        _terminalUiActive = true;
        List<InquiryElement> elements = new List<InquiryElement>
        {
            new InquiryElement(
                "developer_report",
                AwakeLocalization.Resolve("awake.menu.developer_check", "开发者检查"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.developer_hint", "查看运行时诊断")),
            new InquiryElement(
                "test_dialogue",
                AwakeLocalization.Resolve("awake.dev_tools.dialogue", "强制附近深谈"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.dev_tools.dialogue_hint", "打开最近 NPC 深谈")),
            new InquiryElement(
                "test_inbox",
                AwakeLocalization.Resolve("awake.menu.inbox", "事件收件箱"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.dev_tools.inbox_hint", "打开事件收件箱")),
            new InquiryElement(
                "test_weekly",
                AwakeLocalization.Resolve("awake.menu.weekly_report", "世界周报"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.dev_tools.weekly_hint", "打开世界周报")),
            new InquiryElement(
                "reset_proactive",
                AwakeLocalization.Resolve("awake.dev_tools.reset_proactive", "重置主动状态"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.dev_tools.reset_proactive_hint", "清空 NPC 主动聊天候选"))
        };
        MultiSelectionInquiryData data = new MultiSelectionInquiryData(
            AwakeLocalization.Resolve("awake.dev_tools.title", "醒世 · 开发者测试"),
            AwakeLocalization.Resolve("awake.dev_tools.prompt", "选择测试动作："),
            elements,
            true,
            1,
            1,
            AwakeLocalization.Resolve("awake.terminal.confirm", "确定"),
            AwakeLocalization.Resolve("awake.terminal.close", "返回"),
            selected =>
            {
                _terminalUiActive = false;
                HandleDeveloperTestSelection(selected);
            },
            _ => _terminalUiActive = false,
            "",
            false);
        MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
    }

    private static void HandleDeveloperTestSelection(List<InquiryElement> selected)
    {
        if (selected == null || selected.Count == 0) return;
        string id = selected[0].Identifier as string;
        if (StringComparer.Ordinal.Equals(id, "developer_report"))
        {
            AwakeDeveloperTestActions.OpenDeveloperReport();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "test_dialogue"))
        {
            AwakeDeveloperTestActions.TestNearbyDialogue();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "test_inbox"))
        {
            AwakeDeveloperTestActions.TestWorldInbox();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "test_weekly"))
        {
            AwakeDeveloperTestActions.TestWeeklyReport();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "reset_proactive"))
        {
            AwakeDeveloperTestActions.ResetProactive();
        }
    }

    private static void ShowWorldInbox()
    {
        try
        {
            if (!WorldEventInboxOverlay.Open())
            {
                AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                    "awake.feedback.inbox_open_failed",
                    "无法打开事件收件箱。"));
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_world_inbox_error error=" + ex.Message);
        }
    }

    private static void ShowWeeklyReport()
    {
        try
        {
            WorldEventLedger.LoadFromStoreAsync(CancellationToken.None).GetAwaiter().GetResult();
            int day = AwakeRuntime.CurrentGameDay();
            List<WorldEventRecord> week = WorldEventLedger.SnapshotWeek(day);
            string text = NarrativeReportBuilder.Build(week, day);
            if (!WeeklyReportBrowserOverlay.Open(text))
            {
                AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                    "awake.feedback.weekly_report_open_failed",
                    "无法打开世界周报。"));
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_weekly_report_error error=" + ex.Message);
        }
    }

    private static void ShowDeveloperReport()
    {
        try
        {
            string report = AwakeDeveloperReport.Build(AwakeRuntime.ResolveHost(), AwakeSettings.Current);
            ShowMessage(
                AwakeLocalization.Resolve("awake.menu.developer_check", "开发者检查"),
                report);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_developer_report_error error=" + ex.Message);
        }
    }

    internal static void ShowDeveloperReportForMcm()
    {
        ShowDeveloperReport();
    }

    internal static void ShowWorldInboxForMcm()
    {
        ShowWorldInbox();
    }

    internal static void ShowWeeklyReportForMcm()
    {
        ShowWeeklyReport();
    }

    private static void ShowMessage(string title, string text)
    {
        try
        {
            InformationManager.ShowInquiry(
                new InquiryData(
                    title,
                    text,
                    true,
                    false,
                    AwakeLocalization.Resolve("awake.ui.ok", "确定"),
                    "",
                    null,
                    null,
                    "",
                    0f,
                    null,
                    null,
                    null),
                true,
                false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_show_message_error error=" + ex.Message);
        }
    }
}
