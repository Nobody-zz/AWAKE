using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private bool _terminalUiActive;
    private float _lastOpenRealTime = -999f;
    private float _nextTerminalKeyRefreshRealTime = -999f;
    private InputKey _cachedTerminalKey = InputKey.Y;
    private string _cachedTerminalKeyRaw = string.Empty;

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
            string raw = AwakeSettings.Current.TerminalKey ?? "Y";
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
            _cachedTerminalKey = InputKey.Y;
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
        return InputKey.Y;
    }

    private static bool CanOpenTerminal()
    {
        try
        {
            if (Campaign.Current == null)
            {
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
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
            if (Campaign.Current.ConversationManager != null
                && Campaign.Current.ConversationManager.IsConversationInProgress)
            {
                return false;
            }
            if (NpcDialogueOverlay.IsOpen)
            {
                return false;
            }
            try
            {
                if (InformationManager.IsAnyInquiryActive())
                {
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
                    return false;
                }
            }
            catch
            {
            }
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
                "npc_talk",
                AwakeLocalization.Resolve("awake.menu.npc_talk", "深谈（醒世）"),
                (ImageIdentifier)null,
                true,
                "与附近角色展开 AI 深谈"),
            new InquiryElement(
                "developer_report",
                AwakeLocalization.Resolve("awake.menu.developer_check", "开发者检查"),
                (ImageIdentifier)null,
                AwakeSettings.Current.EnableDeveloperMenu,
                "查看运行时诊断")
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
        if (StringComparer.Ordinal.Equals(id, "npc_talk"))
        {
            OpenNpcTalkSelection();
            return;
        }
        if (StringComparer.Ordinal.Equals(id, "developer_report"))
        {
            ShowDeveloperReport();
        }
    }

    private static void OpenNpcTalkSelection()
    {
        try
        {
            if (!MainHeroIsAdult())
            {
                ShowMessage(
                    AwakeLocalization.Resolve("awake.menu.npc_talk_title", "醒世·深谈"),
                    AwakeLocalization.Resolve("awake.menu.npc_talk_too_young", "你还太年轻，无法深谈。"));
                return;
            }

            List<AwakeNpcTarget> targets = NpcDialogueLauncher.GetNearbyTargets(8);
            if (targets.Count == 0)
            {
                ShowMessage(
                    AwakeLocalization.Resolve("awake.menu.npc_talk_title", "醒世·深谈"),
                    AwakeLocalization.Resolve("awake.menu.npc_talk_no_nearby", "附近没有可以深谈的对象。"));
                return;
            }

            List<InquiryElement> elements = new List<InquiryElement>();
            foreach (AwakeNpcTarget target in targets)
            {
                string name = target.DisplayName;
                string gender = target.IsFemale ? "女" : "男";
                string age = Math.Floor(target.Age).ToString("0");
                string kind = target.IsHero ? "英雄" : "无名";
                elements.Add(new InquiryElement(
                    (object)target.StableId,
                    name + "（" + kind + "，" + gender + "，" + age + "岁）",
                    (ImageIdentifier)null,
                    true,
                    "与其深谈"));
            }

            MultiSelectionInquiryData data = new MultiSelectionInquiryData(
                AwakeLocalization.Resolve("awake.menu.npc_talk_title", "醒世·深谈"),
                AwakeLocalization.Resolve("awake.menu.npc_talk_prompt", "选择要深谈的对象："),
                elements,
                true,
                1,
                1,
                "确定",
                "取消",
                OnNpcTalkSelected,
                _ => { },
                "",
                false);
            MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_npc_talk_consequence_error error=" + ex.Message);
        }
    }

    private static void OnNpcTalkSelected(List<InquiryElement> selected)
    {
        try
        {
            if (selected == null || selected.Count == 0) return;
            string targetId = selected[0].Identifier as string;
            if (string.IsNullOrWhiteSpace(targetId)) return;
            AwakeNpcTarget target = NpcDialogueLauncher.FindTargetById(targetId);
            if (target == null || !NpcDialogueLauncher.IsEligibleNpcTarget(target))
            {
                ShowMessage(
                    AwakeLocalization.Resolve("awake.menu.npc_talk_title", "醒世·深谈"),
                    AwakeLocalization.Resolve("awake.menu.npc_talk_gone", "对方已经不在附近。"));
                return;
            }
            NpcDialogueLaunchResult result = NpcDialogueLauncher.TryOpenDialogue(target, "terminal");
            if (result == NpcDialogueLaunchResult.None)
            {
                ShowMessage(
                    AwakeLocalization.Resolve("awake.menu.npc_talk_title", "醒世·深谈"),
                    AwakeLocalization.Resolve("awake.menu.npc_talk_unavailable", "对方暂时无法交谈。"));
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_npc_talk_selected_error error=" + ex.Message);
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

    private static bool MainHeroIsAdult()
    {
        try
        {
            return Hero.MainHero != null && Hero.MainHero.Age >= 18f;
        }
        catch
        {
            return false;
        }
    }

    private static void ShowMessage(string title, string text)
    {
        try
        {
            InformationManager.ShowInquiry(
                new InquiryData(title, text, true, false, "确定", "", null, null, "", 0f, null, null, null),
                true,
                false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_show_message_error error=" + ex.Message);
        }
    }
}
