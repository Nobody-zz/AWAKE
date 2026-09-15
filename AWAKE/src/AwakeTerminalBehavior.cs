using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MarcusAwakeFramework.Api;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
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
    private float _nextOnboardingCheckRealTime = -999f;
    private float _nextTerminalKeyRefreshRealTime = -999f;
    private float _nextSceneKeyRefreshRealTime = -999f;
    private float _nextSceneCandidateRefreshRealTime = -999f;
    private InputKey _cachedTerminalKey = InputKey.U;
    private InputKey _cachedSceneNearKey = InputKey.OpenBraces;
    private InputKey _cachedSceneFarKey = InputKey.CloseBraces;
    private InputKey _cachedSceneShoutKey = InputKey.V;
    private string _cachedTerminalKeyRaw = string.Empty;
    private bool _sceneShoutMode;
    private bool _sceneVisualEnabled;
    private readonly SceneSelectionController _sceneController = new SceneSelectionController();
    private readonly List<AwakeNpcTarget> _sceneCandidateTargets = new List<AwakeNpcTarget>();
    private float _sceneLastRangeMeters = -1f;
    private static string _lastBlockReason = string.Empty;
    private string _worldbookOverlayJson = string.Empty;
    private string _worldbookActivationJson = string.Empty;

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
        string current = WorldbookRuntime.ExportOverlayJson();
        if (!string.IsNullOrWhiteSpace(current)) _worldbookOverlayJson = current;
        dataStore.SyncData("awake_worldbook_overlay_v1", ref _worldbookOverlayJson);
        if (!string.IsNullOrWhiteSpace(_worldbookOverlayJson) && !StringComparer.Ordinal.Equals(_worldbookOverlayJson, current))
        {
            string error;
            if (!WorldbookRuntime.ImportOverlayJson(_worldbookOverlayJson, out error)) AwakeLog.Write("awake_worldbook_overlay_load_error error=" + error);
        }
        string currentActivation = WorldbookRuntime.ExportActivationJson();
        if (!string.IsNullOrWhiteSpace(currentActivation)) _worldbookActivationJson = currentActivation;
        dataStore.SyncData("awake_worldbook_activation_v1", ref _worldbookActivationJson);
        if (!string.IsNullOrWhiteSpace(_worldbookActivationJson) && !StringComparer.Ordinal.Equals(_worldbookActivationJson, currentActivation))
        {
            string error;
            if (!WorldbookRuntime.ImportActivationJson(_worldbookActivationJson, out error)) AwakeLog.Write("awake_worldbook_activation_load_error error=" + error);
        }
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
        AwakeGoldSettlementService.Tick();
        TryShowAutoGuide();
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
        if (AwakeOnboardingService.TryShowGuide())
        {
            return;
        }
        OpenRootMenu();
    }

    private void TryShowAutoGuide()
    {
        try
        {
            if (_terminalUiActive
                || AwakeDialogueSessionCoordinator.IsActive
                || AwakeMessengerOverlay.IsOpen
                || InformationManager.IsAnyInquiryActive()
                || Campaign.Current == null
                || Mission.Current != null
                || !(GameStateManager.Current?.ActiveState is MapState)
                || Campaign.Current.CurrentMenuContext != null
                || !AwakeSettings.Current.EnableInGameGuide)
            {
                return;
            }
            float now = (float)TerminalClock.Elapsed.TotalSeconds;
            if (now - _nextOnboardingCheckRealTime < 5f)
            {
                return;
            }
            _nextOnboardingCheckRealTime = now;
            int day = AwakeRuntime.CurrentGameDay();
            int lastReminder = AwakeOnboardingService.Current.LastReminderDay;
            if (lastReminder >= 0
                && day - lastReminder < AwakeSettings.Current.GuideRepeatIntervalDays)
            {
                return;
            }
            AwakeOnboardingService.TryShowGuide();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_onboarding_auto_check_error error=" + ex.Message);
        }
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
                _sceneShoutMode = false;
                _sceneVisualEnabled = AwakeSettings.Current.EnableSceneVisualSelection;
                _sceneLastRangeMeters = -1f;
                RefreshSceneKeys(now);
                ClearSceneSelection();
                if (_sceneVisualEnabled)
                {
                    SceneDialogueVisualCapabilities.Probe(Mission.Current?.MainAgent);
                    SceneDialogueStatusOverlay.Open(BuildSceneStatusText());
                }
                ShowSceneMessage(BuildSceneHintText());
            }
            else
            {
                _sceneCurrentRangeMeters = SceneDialogueSelection.CurrentRange(
                    Math.Max(0f, now - _sceneHoldStartRealTime),
                    GetSceneMaxRange());
            }

            RefreshSceneCandidates(now);
            if (Input.IsKeyPressed(InputKey.Y) || Input.IsKeyPressed(GetSceneNearKey(now)))
            {
                CycleSceneTarget(1);
            }
            else if (Input.IsKeyPressed(GetSceneFarKey(now)))
            {
                CycleSceneTarget(-1);
            }
            else if (Input.IsKeyPressed(GetSceneShoutKey(now)))
            {
                ToggleSceneShoutMode();
            }
            UpdateSceneVisuals();
            return true;
        }

        if (_sceneHoldActive)
        {
            _sceneCurrentRangeMeters = SceneDialogueSelection.CurrentRange(
                Math.Max(0f, (float)TerminalClock.Elapsed.TotalSeconds - _sceneHoldStartRealTime),
                GetSceneMaxRange());
            _sceneHoldActive = false;
            CloseSceneSelectionVisuals();
            if (_sceneShoutMode)
            {
                OpenSceneShoutAfterHold();
            }
            else
            {
                ConfirmSceneTarget();
            }
            return true;
        }

        return true;
    }

    private void RefreshSceneCandidates(float now)
    {
        if (now < _nextSceneCandidateRefreshRealTime
            && Math.Abs(_sceneCurrentRangeMeters - _sceneLastRangeMeters) < 1f)
        {
            return;
        }
        _nextSceneCandidateRefreshRealTime = now + 0.2f;
        _sceneLastRangeMeters = _sceneCurrentRangeMeters;

        Agent mainAgent = Mission.Current?.MainAgent ?? Agent.Main;
        if (mainAgent == null || !mainAgent.IsActive())
        {
            _sceneCandidateTargets.Clear();
            _sceneController.SetCandidates(new List<SceneSelectionItem>());
            return;
        }

        List<Tuple<AwakeNpcTarget, float>> scored = new List<Tuple<AwakeNpcTarget, float>>();
        float rangeSquared = _sceneCurrentRangeMeters * _sceneCurrentRangeMeters;
        float halfAngle = SceneDialoguePreviewMath.CurrentHalfAngle(
            Math.Max(0f, (float)TerminalClock.Elapsed.TotalSeconds - _sceneHoldStartRealTime));
        foreach (AwakeNpcTarget target in NpcDialogueLauncher.GetSceneCandidates())
        {
            if (target == null || target.AgentIndex < 0
                || !NpcDialogueLauncher.IsEligibleNpcTarget(target))
            {
                continue;
            }
            Agent agent = NpcDialogueLauncher.GetActiveAgent(target.AgentIndex);
            if (agent == null || !agent.IsActive()) continue;
            float distanceSquared = mainAgent.Position.DistanceSquared(agent.Position);
            if (float.IsNaN(distanceSquared)
                || float.IsInfinity(distanceSquared)
                || distanceSquared > rangeSquared)
            {
                continue;
            }
            if (!SceneDialoguePreviewMath.IsWithinCone(
                    mainAgent.Position,
                    mainAgent.LookDirection,
                    agent.Position,
                    halfAngle)
                || !HasSceneLineOfSight(mainAgent, agent, (float)Math.Sqrt(distanceSquared)))
            {
                continue;
            }
            scored.Add(Tuple.Create(target, (float)Math.Sqrt(distanceSquared)));
        }

        scored.Sort((a, b) =>
        {
            int byDistance = a.Item2.CompareTo(b.Item2);
            if (byDistance != 0) return byDistance;
            return StringComparer.Ordinal.Compare(a.Item1.StableId, b.Item1.StableId);
        });

        _sceneCandidateTargets.Clear();
        List<SceneSelectionItem> items = new List<SceneSelectionItem>();
        string preferredId = _sceneController.Selected?.Id ?? _sceneSelectedTargetId;
        foreach (Tuple<AwakeNpcTarget, float> item in scored)
        {
            if (items.Count >= SceneSelectionController.MaximumCandidates) break;
            AwakeNpcTarget target = item.Item1;
            _sceneCandidateTargets.Add(target);
            items.Add(new SceneSelectionItem(
                BuildSceneSelectionKey(target),
                target.DisplayName,
                item.Item2));
        }
        _sceneController.SetCandidates(items, preferredId);
    }

    private static bool HasSceneLineOfSight(Agent mainAgent, Agent target, float targetDistance)
    {
        try
        {
            Mission mission = Mission.Current;
            if (mission == null || mainAgent == null || target == null) return true;
            Vec3 from = mainAgent.Position + new Vec3(0f, 0f, 1.4f);
            Vec3 to = target.Position + new Vec3(0f, 0f, 1.2f);
            float hitDistance;
            Agent blocker = mission.RayCastForClosestAgent(from, to, -1, targetDistance, out hitDistance);
            if (blocker == null || blocker == Agent.Main || blocker.IsMainAgent) return true;
            if (ReferenceEquals(blocker, target)) return true;
            return hitDistance >= targetDistance - 0.5f;
        }
        catch
        {
            return true;
        }
    }

    private void ToggleSceneShoutMode()
    {
        _sceneShoutMode = !_sceneShoutMode;
        if (_sceneShoutMode)
        {
            _sceneController.ClearSelection();
            ClearSceneSelection();
            ShowSceneMessage(AwakeLocalization.Resolve(
                "awake.scene.shout_mode",
                "已切换到场景喊话，松开 T 后不指定具体人物。"));
        }
        else
        {
            float now = (float)TerminalClock.Elapsed.TotalSeconds;
            ShowSceneMessage(AwakeLocalization.Resolve(
                "awake.scene.target_mode",
                "已切回选人模式，按 " + SceneKeyDisplay(GetSceneNearKey(now)) + " 或 "
                + SceneKeyDisplay(GetSceneFarKey(now)) + " 选择目标。",
                new Dictionary<string, string>
                {
                    ["NEAR_KEY"] = SceneKeyDisplay(GetSceneNearKey(now)),
                    ["FAR_KEY"] = SceneKeyDisplay(GetSceneFarKey(now))
                }));
        }
        UpdateSceneVisuals();
    }

    private void UpdateSceneVisuals()
    {
        if (!_sceneVisualEnabled)
        {
            return;
        }
        int selectedAgentIndex = _sceneShoutMode ? -1 : _sceneSelectedAgentIndex;
        SceneDialoguePreview.Update(
            Mission.Current,
            Mission.Current?.MainAgent ?? Agent.Main,
            _sceneCandidateTargets,
            selectedAgentIndex,
            _sceneShoutMode,
            SceneDialogueVisualCapabilities.ContourCapable,
            _sceneCurrentRangeMeters);
        SceneDialogueStatusOverlay.Update(BuildSceneStatusText());
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

    private void CycleSceneTarget(int direction)
    {
        if (_sceneController.Count == 0)
        {
            ClearSceneSelection();
            ShowSceneMessage(AwakeLocalization.Resolve("awake.scene.no_candidates", "附近没有可以对话的人物。"));
            return;
        }
        _sceneController.Cycle(direction);
        int index = _sceneController.SelectedIndex;
        if (index < 0 || index >= _sceneCandidateTargets.Count)
        {
            return;
        }
        SelectSceneTarget(_sceneCandidateTargets[index]);
    }

    private void ConfirmSceneTarget()
    {
        AwakeNpcTarget target = _sceneController.Selected != null
            && _sceneController.SelectedIndex >= 0
            && _sceneController.SelectedIndex < _sceneCandidateTargets.Count
                ? _sceneCandidateTargets[_sceneController.SelectedIndex]
                : (_sceneCandidateTargets.Count > 0 ? _sceneCandidateTargets[0] : null);
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
        UpdateSceneVisuals();
        if (showHint)
        {
            string rangeText = _sceneCurrentRangeMeters.ToString("0");
            ShowSceneMessage(AwakeLocalization.Resolve(
                "awake.scene.select_hint",
                "已选中 " + target.DisplayName + "（" + rangeText + "m），按 "
                + SceneKeyDisplay(GetSceneNearKey((float)TerminalClock.Elapsed.TotalSeconds)) + " 近到远、"
                + SceneKeyDisplay(GetSceneFarKey((float)TerminalClock.Elapsed.TotalSeconds)) + " 远到近，松开 T 开始对话。",
                new Dictionary<string, string>
                {
                    ["NAME"] = target.DisplayName,
                    ["RANGE"] = rangeText
                }));
        }
    }

    private void AbortSceneSelection()
    {
        _sceneHoldActive = false;
        _sceneHoldStartRealTime = -999f;
        _sceneCurrentRangeMeters = SceneDialogueSelection.MinRangeMeters;
        _sceneShoutMode = false;
        _sceneCandidateTargets.Clear();
        _sceneController.Clear();
        CloseSceneSelectionVisuals();
        ClearSceneSelection();
    }

    private void CloseSceneSelectionVisuals()
    {
        SceneDialogueStatusOverlay.CloseActive();
        SceneDialoguePreview.Clear();
        _sceneLastRangeMeters = -1f;
    }

    private void OpenSceneShoutAfterHold()
    {
        SceneShoutAvailabilityResult availability = NpcDialogueLauncher.EvaluateSceneShoutAvailability();
        if (availability != SceneShoutAvailabilityResult.Available)
        {
            string message = availability == SceneShoutAvailabilityResult.NoPeople
                ? AwakeLocalization.Resolve("awake.scene.shout_no_people", "当前场合没有人能听见你的喊话。")
                : AwakeLocalization.Resolve("awake.scene.shout_unavailable", "当前场合无法喊话。");
            ShowSceneMessage(message);
            return;
        }
        NpcDialogueLaunchResult result = NpcDialogueLauncher.TryOpenSceneShout(NpcDialogueLauncher.CurrentSceneKeywords());
        if (result == NpcDialogueLaunchResult.None)
        {
            ShowSceneMessage(AwakeLocalization.Resolve(
                "awake.scene.shout_open_failed",
                "场景喊话暂时无法打开。"));
        }
    }

    private string BuildSceneHintText()
    {
        float now = (float)TerminalClock.Elapsed.TotalSeconds;
        return AwakeLocalization.Resolve(
            "awake.scene.hold_hint",
            "按住 T 扩大距离；" + SceneKeyDisplay(GetSceneNearKey(now)) + " 近到远、"
            + SceneKeyDisplay(GetSceneFarKey(now)) + " 远到近、"
            + SceneKeyDisplay(GetSceneShoutKey(now)) + " 场景喊话；松开 T 开始对话。",
            new Dictionary<string, string>
            {
                ["NEAR_KEY"] = SceneKeyDisplay(GetSceneNearKey(now)),
                ["FAR_KEY"] = SceneKeyDisplay(GetSceneFarKey(now)),
                ["SHOUT_KEY"] = SceneKeyDisplay(GetSceneShoutKey(now))
            });
    }

    private string BuildSceneStatusText()
    {
        string mode = _sceneShoutMode
            ? AwakeLocalization.Resolve("awake.scene.status_shout_mode", "场景喊话")
            : AwakeLocalization.Resolve("awake.scene.status_target_mode", "选人");
        string target = _sceneShoutMode
            ? AwakeLocalization.Resolve("awake.scene.status_no_target", "无目标")
            : (_sceneController.Selected?.DisplayName
                ?? AwakeLocalization.Resolve("awake.scene.status_no_selection", "未选择"));
        return AwakeLocalization.Resolve(
            "awake.scene.status_line",
            "模式 " + mode + "；目标 " + target + "；范围 " + _sceneCurrentRangeMeters.ToString("0") + " 米；候选 "
            + _sceneController.Count,
            new Dictionary<string, string>
            {
                ["MODE"] = mode,
                ["TARGET"] = target,
                ["RANGE"] = _sceneCurrentRangeMeters.ToString("0"),
                ["COUNT"] = _sceneController.Count.ToString()
            });
    }

    private static string BuildSceneSelectionKey(AwakeNpcTarget target)
    {
        if (target == null) return string.Empty;
        string missionPart = Mission.Current == null ? "m0" : "m" + Mission.Current.GetHashCode();
        string characterId = target.IsHero && target.Hero != null
            ? target.Hero.StringId
            : target.Character?.StringId ?? string.Empty;
        return missionPart + ":" + characterId + ":a" + target.AgentIndex;
    }

    private static string SceneKeyDisplay(InputKey key)
    {
        if (key == InputKey.OpenBraces) return "[";
        if (key == InputKey.CloseBraces) return "]";
        return key.ToString();
    }

    private InputKey GetSceneNearKey(float now)
    {
        RefreshSceneKeys(now);
        return _cachedSceneNearKey;
    }

    private InputKey GetSceneFarKey(float now)
    {
        RefreshSceneKeys(now);
        return _cachedSceneFarKey;
    }

    private InputKey GetSceneShoutKey(float now)
    {
        RefreshSceneKeys(now);
        return _cachedSceneShoutKey;
    }

    private void RefreshSceneKeys(float now)
    {
        if (now < _nextSceneKeyRefreshRealTime) return;
        _nextSceneKeyRefreshRealTime = now + 1f;
        _cachedSceneNearKey = SceneInputKeyMapper.ParseOrDefault(
            AwakeSettings.Current.SceneCycleNearToFarKey,
            InputKey.OpenBraces);
        _cachedSceneFarKey = SceneInputKeyMapper.ParseOrDefault(
            AwakeSettings.Current.SceneCycleFarToNearKey,
            InputKey.CloseBraces);
        _cachedSceneShoutKey = SceneInputKeyMapper.ParseOrDefault(
            AwakeSettings.Current.SceneShoutKey,
            InputKey.V);
        string shoutRaw = (AwakeSettings.Current.SceneShoutKey ?? string.Empty).Trim();
        if (StringComparer.OrdinalIgnoreCase.Equals(shoutRaw, "C")
            || StringComparer.OrdinalIgnoreCase.Equals(shoutRaw, "U"))
        {
            _cachedSceneShoutKey = InputKey.V;
            AwakeSettings.Current.SceneShoutKey = "V";
        }
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
                "nearby_dialogue",
                AwakeLocalization.Resolve("awake.menu.nearby_dialogue", "附近对话（地图）"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.nearby_dialogue_hint", "选择附近英雄发起 AI 对话")),
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
                AwakeLocalization.Resolve("awake.menu.weekly_dynamics", "本周动态"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.weekly_hint", "查看近期世界动态")),
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
        if (StringComparer.Ordinal.Equals(id, "nearby_dialogue"))
        {
            OpenNearbyDialoguePicker();
            return;
        }
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

    private static void OpenNearbyDialoguePicker()
    {
        List<InquiryElement> elements = new List<InquiryElement>
        {
            new InquiryElement(
                "party",
                AwakeLocalization.Resolve("awake.terminal.map_scope_party", "同队伍英雄"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.map_scope_party_hint", "选择玩家队伍中的英雄")),
            new InquiryElement(
                "nearby",
                AwakeLocalization.Resolve("awake.terminal.map_scope_nearby", "附近可交谈者"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.map_scope_nearby_hint", "选择当前可交谈的附近英雄")),
            new InquiryElement(
                "shout",
                AwakeLocalization.Resolve("awake.terminal.map_scope_shout", "地图公开喊话"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.map_scope_shout_hint", "不指定人物，向当前地图场合喊话"))
        };
        MBInformationManager.ShowMultiSelectionInquiry(
            new MultiSelectionInquiryData(
                AwakeLocalization.Resolve("awake.terminal.map_scope_title", "地图对话"),
                AwakeLocalization.Resolve("awake.terminal.map_scope_prompt", "先选择范围，然后从候选中选择对象："),
                elements,
                true,
                1,
                1,
                AwakeLocalization.Resolve("awake.terminal.confirm", "确定"),
                AwakeLocalization.Resolve("awake.terminal.close", "关闭"),
                selected =>
                {
                    if (selected == null || selected.Count == 0) return;
                    string scope = selected[0].Identifier as string;
                    if (StringComparer.Ordinal.Equals(scope, "shout"))
                    {
                        NpcDialogueLaunchResult shoutResult = NpcDialogueLauncher.TryOpenMapShout(NpcDialogueLauncher.CurrentSceneKeywords());
                        if (shoutResult == NpcDialogueLaunchResult.None)
                        {
                            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                                "awake.terminal.map_shout_failed",
                                "Map shout failed to open."));
                        }
                        return;
                    }
                    OpenMapHeroPicker(scope);
                },
                _ => { },
                string.Empty,
                false),
            true,
            false);
    }

    private static void OpenMapHeroPicker(string scope)
    {
        List<Hero> heroes = StringComparer.Ordinal.Equals(scope, "party")
            ? GetPartyDialogueHeroes(8)
            : NpcDialogueLauncher.GetNearbyHeroes(8);
        if (heroes.Count == 0)
        {
            AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                "awake.dev_tools.no_target",
                "Nearby target missing."));
            return;
        }
        List<InquiryElement> elements = new List<InquiryElement>();
        foreach (Hero hero in heroes)
        {
            if (hero == null || string.IsNullOrWhiteSpace(hero.StringId)) continue;
            elements.Add(new InquiryElement(
                hero.StringId,
                hero.Name?.ToString() ?? hero.StringId,
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.terminal.nearby_dialogue_hero_hint", "发起 AI 对话")));
        }
        if (elements.Count == 0)
        {
            AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                "awake.dev_tools.no_target",
                "Nearby target missing."));
            return;
        }
        MBInformationManager.ShowMultiSelectionInquiry(
            new MultiSelectionInquiryData(
                AwakeLocalization.Resolve("awake.terminal.nearby_dialogue_title", "附近对话"),
                AwakeLocalization.Resolve("awake.terminal.nearby_dialogue_prompt", "选择要交谈的对象："),
                elements,
                true,
                1,
                1,
                AwakeLocalization.Resolve("awake.terminal.confirm", "确定"),
                AwakeLocalization.Resolve("awake.terminal.close", "关闭"),
                selected =>
                {
                    if (selected == null || selected.Count == 0) return;
                    string heroId = selected[0].Identifier as string;
                    Hero hero = NpcDialogueLauncher.FindHeroById(heroId);
                    if (hero == null)
                    {
                        AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                            "awake.dev_tools.no_target",
                            "Nearby target missing."));
                        return;
                    }
                    NpcDialogueLaunchResult result = NpcDialogueLauncher.TryOpenDialogue(hero, "map");
                    if (result == NpcDialogueLaunchResult.None)
                    {
                        AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                            "awake.dev_tools.dialogue_failed",
                            "Dialogue failed to open."));
                    }
                },
                _ => { },
                string.Empty,
                false),
            true,
            false);
    }

    private static List<Hero> GetPartyDialogueHeroes(int limit)
    {
        List<Hero> result = new List<Hero>();
        if (Campaign.Current?.CampaignObjectManager?.AliveHeroes == null) return result;
        MobileParty mainParty = MobileParty.MainParty;
        if (mainParty == null) return result;
        foreach (Hero hero in Campaign.Current.CampaignObjectManager.AliveHeroes)
        {
            if (result.Count >= limit) break;
            if (hero == null || hero == Hero.MainHero || !hero.IsAlive || hero.Age < 18f) continue;
            if (hero.PartyBelongedTo != mainParty && hero.PartyBelongedToAsPrisoner != mainParty.Party) continue;
            if (NpcDialogueLauncher.IsEligibleNpcTarget(AwakeNpcTarget.FromHero(hero)))
            {
                result.Add(hero);
            }
        }
        return result;
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
                "portrait_probe",
                "画位探测（全身像）",
                (ImageIdentifier)null,
                true,
                "验证 png → 运行时纹理 → 上屏这条链；面板里可直接放一张内置测试图"),
            new InquiryElement(
                "test_dialogue",
                AwakeLocalization.Resolve("awake.dev_tools.dialogue", "强制附近深谈"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.dev_tools.dialogue_hint", "打开最近 NPC 深谈")),
            new InquiryElement(
                "test_negotiation",
                AwakeLocalization.Resolve("awake.dev_tools.negotiation", "强制附近谈判"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.dev_tools.negotiation_hint", "以谈判模式打开最近 NPC，用于测试关系指令链")),
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
                AwakeLocalization.Resolve("awake.dev_tools.reset_proactive_hint", "清空 NPC 主动聊天候选")),
            new InquiryElement(
                "worldbook_status",
                AwakeLocalization.Resolve("awake.dev_tools.worldbook_status", "世界书状态"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.dev_tools.worldbook_status_hint", "查看规则/人物/警告数量")),
            new InquiryElement(
                "worldbook_search",
                AwakeLocalization.Resolve("awake.dev_tools.worldbook_search", "世界书关键词查询"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.dev_tools.worldbook_search_hint", "输入关键词查找命中的规则")),
            new InquiryElement(
                "worldbook_reload",
                AwakeLocalization.Resolve("awake.dev_tools.worldbook_reload", "重载世界书"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.dev_tools.worldbook_reload_hint", "重新读取 ModuleData/Worldbook")),
            new InquiryElement(
                "worldbook_edit",
                "编辑已有世界知识",
                (ImageIdentifier)null,
                true,
                "修改当前战役 Overlay，不改基础世界书"),
            new InquiryElement(
                "worldbook_export_overlay",
                "导出世界书修改",
                (ImageIdentifier)null,
                true,
                "导出当前战役修改，供后续存档复用")
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
        if (StringComparer.Ordinal.Equals(id, "portrait_probe"))
        {
            AwakeDeveloperTestActions.OpenPortraitProbe();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "test_dialogue"))
        {
            AwakeDeveloperTestActions.TestNearbyDialogue();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "test_negotiation"))
        {
            AwakeDeveloperTestActions.TestNearbyNegotiation();
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
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "worldbook_status"))
        {
            AwakeDeveloperTestActions.ShowWorldbookStatus();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "worldbook_search"))
        {
            AwakeDeveloperTestActions.SearchWorldbook();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "worldbook_reload"))
        {
            AwakeDeveloperTestActions.ReloadWorldbook();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "worldbook_edit"))
        {
            AwakeDeveloperTestActions.EditWorldbook();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "worldbook_export_overlay"))
        {
            AwakeDeveloperTestActions.ExportWorldbookOverlay();
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
            int day = AwakeRuntime.CurrentGameDay();
            int sessionGeneration = AwakeRuntime.SessionGeneration;
            CancellationToken cancellationToken = AwakeRuntime.SessionCancellationToken;
            AwakeBackgroundTask.Run(async () =>
            {
                try
                {
                    IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
                    if (host == null || !await AwakeRuntime.EnsureWorldStateReadyAsync(host, cancellationToken).ConfigureAwait(false))
                    {
                        QueueWeeklyReportDisplay(sessionGeneration, new WeeklyReportDisplay(WeeklyReportDisplayState.Unavailable, string.Empty));
                        return;
                    }
                    FormalWeeklyReportResult formal = await WorldEventServices.EnsureFormalReportsReadyAsync(day, cancellationToken).ConfigureAwait(false);
                    if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration)) return;
                    if (formal.HasFormalReport)
                    {
                        string text = WeeklyReportService.BuildText(
                            formal.Report,
                            AwakeLocalization.Resolve("awake.menu.weekly_dynamics", "本周动态"),
                            AwakeLocalization.Resolve("awake.ui.weekly_empty", "本周没有记录。"));
                        QueueWeeklyReportDisplay(sessionGeneration, new WeeklyReportDisplay(WeeklyReportDisplayState.Formal, text));
                        return;
                    }
                    if (StringComparer.Ordinal.Equals(formal.Status, FormalWeeklyReportResult.Preview))
                    {
                        WorldFactQueryResult recent = await WorldEventServices.QueryRecentDynamicsAsync(day, cancellationToken).ConfigureAwait(false);
                        if (recent.Status != WorldFactQueryStatus.Success && recent.Status != WorldFactQueryStatus.Empty)
                        {
                            QueueWeeklyReportDisplay(sessionGeneration, new WeeklyReportDisplay(WeeklyReportDisplayState.Unavailable, string.Empty));
                            return;
                        }
                        string text = WeeklyReportService.BuildTextFromFacts(
                            recent.Facts,
                            AwakeLocalization.Resolve("awake.menu.recent_dynamics", "近期动态"),
                            AwakeLocalization.Resolve("awake.ui.weekly_empty", "本周没有记录。"));
                        QueueWeeklyReportDisplay(sessionGeneration, new WeeklyReportDisplay(WeeklyReportDisplayState.Preview, text));
                        return;
                    }
                    QueueWeeklyReportDisplay(sessionGeneration, new WeeklyReportDisplay(WeeklyReportDisplayState.Unavailable, string.Empty));
                }
                catch (OperationCanceledException)
                {
                    AwakeLog.Write("awake_weekly_report_load_cancelled");
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("awake_weekly_report_load_error error=" + ex.Message);
                    QueueWeeklyReportDisplay(sessionGeneration, new WeeklyReportDisplay(WeeklyReportDisplayState.Unavailable, string.Empty));
                }
            }, "weekly_report_load");
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_weekly_report_error error=" + ex.Message);
        }
    }

    private static void QueueWeeklyReportDisplay(int sessionGeneration, WeeklyReportDisplay display)
    {
        AwakeUiDispatcher.Enqueue(() => OpenWeeklyReportLoaded(sessionGeneration, display));
    }

    private static void OpenWeeklyReportLoaded(int sessionGeneration, WeeklyReportDisplay display)
    {
        if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration)) return;
        try
        {
            if (!WeeklyReportBrowserOverlay.Open(display))
            {
                AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                    "awake.feedback.weekly_report_open_failed",
                    "无法打开本周动态。"));
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_weekly_report_open_error error=" + ex.Message);
        }
    }

    private static void ShowDeveloperReport()
    {
        try
        {
            if (DeveloperCheckOverlay.Open())
            {
                return;
            }
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.mcm.actions.developer_unavailable",
                "开发者检查暂不可用。"));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_developer_report_error error=" + ex.Message);
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.mcm.actions.developer_unavailable",
                "开发者检查暂不可用。"));
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
