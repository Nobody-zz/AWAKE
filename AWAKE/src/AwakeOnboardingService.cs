using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Awake;

internal enum AwakeOnboardingStep
{
    Welcome,
    AiConfig,
    CommandDeck,
    FirstDialogue,
    ContactHistory,
    Complete
}

internal sealed class AwakeOnboardingProgress
{
    internal HashSet<string> CompletedSteps { get; } = new HashSet<string>(StringComparer.Ordinal);
    internal bool SkippedThisCampaign { get; set; }
    internal bool PermanentlySkipped { get; set; }
    internal int LastReminderDay { get; set; } = -1;
}

internal static class AwakeOnboardingService
{
    private static bool _shownThisCampaign;
    private static readonly AwakeOnboardingProgress Progress = new AwakeOnboardingProgress();

    internal static AwakeOnboardingProgress Current => Progress;

    internal static bool IsComplete =>
        Progress.PermanentlySkipped
        || Progress.CompletedSteps.Contains(AwakeOnboardingStep.Complete.ToString());

    internal static bool ShouldShowGuide()
    {
        return !_shownThisCampaign && !IsComplete && !Progress.SkippedThisCampaign;
    }

    internal static void ResetForCampaign()
    {
        _shownThisCampaign = false;
        Progress.SkippedThisCampaign = false;
        Progress.LastReminderDay = -1;
    }

    internal static void ResetForTesting()
    {
        _shownThisCampaign = false;
        Progress.CompletedSteps.Clear();
        Progress.SkippedThisCampaign = false;
        Progress.PermanentlySkipped = false;
        Progress.LastReminderDay = -1;
    }

    internal static void MarkShownForTesting()
    {
        _shownThisCampaign = true;
    }

    internal static void MarkStepCompleted(AwakeOnboardingStep step)
    {
        Progress.CompletedSteps.Add(step.ToString());
    }

    internal static void MarkSkippedThisCampaign()
    {
        Progress.SkippedThisCampaign = true;
    }

    internal static void MarkSkippedForever()
    {
        Progress.PermanentlySkipped = true;
        Progress.SkippedThisCampaign = true;
    }

    internal static async Task LoadFromStoreAsync(CancellationToken cancellationToken)
    {
        await LoadFromStoreAsync(
            AwakeRuntime.SessionGeneration,
            AwakeRuntime.WorldStateStore,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task LoadFromStoreAsync(
        int sessionGeneration,
        WorldStateStore expectedStore,
        CancellationToken cancellationToken)
    {
        WorldStateStore store = expectedStore;
        if (store == null || !AwakeRuntime.IsCurrentSession(sessionGeneration, store)) return;
        try
        {
            JObject doc = await store.GetOnboardingAsync(null, cancellationToken).ConfigureAwait(false);
            if (doc == null || !AwakeRuntime.IsCurrentSession(sessionGeneration, store)) return;
            Progress.CompletedSteps.Clear();
            if (doc["completedSteps"] is JArray steps)
            {
                foreach (JToken token in steps)
                {
                    string step = token?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(step)) Progress.CompletedSteps.Add(step);
                }
            }
            Progress.SkippedThisCampaign = BoolValue(doc["skippedThisCampaign"]);
            Progress.PermanentlySkipped = BoolValue(doc["permanentlySkipped"]);
            Progress.LastReminderDay = IntValue(doc["lastReminderDay"]);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_onboarding_load_error error=" + ex.Message);
        }
    }

    internal static async Task SaveAsync(CancellationToken cancellationToken)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        int sessionGeneration = AwakeRuntime.SessionGeneration;
        if (store == null || !AwakeRuntime.IsCurrentSession(sessionGeneration, store)) return;
        try
        {
            List<string> steps = new List<string>(Progress.CompletedSteps);
            await store.UpdateOnboardingAsync(
                steps,
                Progress.SkippedThisCampaign,
                Progress.PermanentlySkipped,
                Progress.LastReminderDay,
                "onboarding|state",
                cancellationToken).ConfigureAwait(false);
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, store))
            {
                AwakeLog.Write("awake_onboarding_save_stale");
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_onboarding_save_error error=" + ex.Message);
        }
    }

    internal static bool TryShowGuide()
    {
        if (!ShouldShowGuide()) return false;
        _shownThisCampaign = true;
        Progress.LastReminderDay = AwakeRuntime.CurrentGameDay();
        if (HasConfiguredAi()) MarkStepCompleted(AwakeOnboardingStep.AiConfig);
        _ = SaveAsync(AwakeRuntime.SessionCancellationToken);
        try
        {
            AwakeLog.Write("awake_onboarding_show active_state="
                + (GameStateManager.Current?.ActiveState?.GetType().Name ?? "none"));
            ShowStep(NextIncompleteStep());
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_onboarding_show_error error=" + ex.Message);
            _shownThisCampaign = false;
            return false;
        }
    }

    internal static AwakeOnboardingStep NextIncompleteStep()
    {
        AwakeOnboardingStep[] order =
        {
            AwakeOnboardingStep.Welcome,
            AwakeOnboardingStep.AiConfig,
            AwakeOnboardingStep.CommandDeck,
            AwakeOnboardingStep.FirstDialogue,
            AwakeOnboardingStep.ContactHistory,
            AwakeOnboardingStep.Complete
        };
        foreach (AwakeOnboardingStep step in order)
        {
            if (!Progress.CompletedSteps.Contains(step.ToString())) return step;
        }
        return AwakeOnboardingStep.Complete;
    }

    private static void ShowStep(AwakeOnboardingStep step)
    {
        InformationManager.ShowInquiry(
            new InquiryData(
                AwakeLocalization.Resolve("awake.onboarding.title", "醒世 · 首启向导"),
                StepText(step),
                true,
                true,
                AwakeLocalization.Resolve(
                    step == AwakeOnboardingStep.AiConfig ? "awake.onboarding.open" : "awake.onboarding.next",
                    step == AwakeOnboardingStep.AiConfig ? "打开 AI 设置" : "下一步"),
                AwakeLocalization.Resolve("awake.onboarding.skip", "跳过"),
                () =>
                {
                    if (step == AwakeOnboardingStep.AiConfig)
                    {
                        try { AwakeMarcusLinkService.OpenAiSetup(); }
                        catch (Exception ex) { AwakeLog.Write("awake_onboarding_open_setup_error error=" + ex.Message); }
                    }
                    MarkStepCompleted(step);
                    _ = SaveAsync(AwakeRuntime.SessionCancellationToken);
                    if (step != AwakeOnboardingStep.Complete)
                    {
                        AwakeUiDispatcher.Enqueue(() => ShowStep(NextIncompleteStep()));
                    }
                },
                ShowSkipOptions,
                string.Empty,
                0f,
                null,
                null,
                null),
            true,
            false);
    }

    private static void ShowSkipOptions()
    {
        InformationManager.ShowInquiry(
            new InquiryData(
                AwakeLocalization.Resolve("awake.onboarding.skip_title", "跳过向导"),
                AwakeLocalization.Resolve("awake.onboarding.skip_text", "你可以只在本局跳过，也可以永久关闭首启向导。"),
                true,
                true,
                AwakeLocalization.Resolve("awake.onboarding.skip_campaign", "本局跳过"),
                AwakeLocalization.Resolve("awake.onboarding.skip_forever", "永久关闭"),
                () =>
                {
                    MarkSkippedThisCampaign();
                    _ = SaveAsync(AwakeRuntime.SessionCancellationToken);
                },
                () =>
                {
                    MarkSkippedForever();
                    _ = SaveAsync(AwakeRuntime.SessionCancellationToken);
                },
                string.Empty,
                0f,
                null,
                null,
                null),
            true,
            false);
    }

    private static string StepText(AwakeOnboardingStep step)
    {
        switch (step)
        {
            case AwakeOnboardingStep.Welcome:
                return AwakeLocalization.Resolve("awake.onboarding.welcome.text", "本向导会依次说明 AI 配置、指令台、首次对话和通讯录历史。");
            case AwakeOnboardingStep.AiConfig:
                return AwakeLocalization.Resolve("awake.onboarding.aiconfig.text", "先配置 AI 服务商、模型与 AWAKE 路由。");
            case AwakeOnboardingStep.CommandDeck:
                return AwakeLocalization.Resolve("awake.onboarding.commanddeck.text", "MCM 中可以调整云外发、主动对话、快捷键和引导重复间隔。");
            case AwakeOnboardingStep.FirstDialogue:
                return AwakeLocalization.Resolve("awake.onboarding.firstdialogue.text", "在场景中选择 NPC 后发起对话，地图入口也会进入同一会话。");
            case AwakeOnboardingStep.ContactHistory:
                return AwakeLocalization.Resolve("awake.onboarding.contacthistory.text", "通讯录可以继续对话、查看历史，并给远方联系人写信。");
            default:
                return AwakeLocalization.Resolve("awake.onboarding.complete.text", "基础引导完成。之后可以从 MCM 和开发者检查面板查看状态。");
        }
    }

    private static bool HasConfiguredAi()
    {
        try
        {
            if (!FrameworkHostLocator.TryGetHost(out IMarcusAiFrameworkHost host) || host == null)
            {
                return false;
            }
            HealthSnapshot health = host.Diagnostics?.GetHealth();
            if (health?.Components == null) return false;
            foreach (HealthComponent component in health.Components)
            {
                if (component == null || component.Level != HealthLevel.Healthy) continue;
                string id = component.Id ?? string.Empty;
                if (id.IndexOf("provider", StringComparison.OrdinalIgnoreCase) >= 0
                    || id.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0
                    || id.IndexOf("route", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool BoolValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Boolean) return false;
        try
        {
            return (bool)token;
        }
        catch
        {
            return false;
        }
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return -1;
        try
        {
            return (int)token;
        }
        catch
        {
            return -1;
        }
    }
}
