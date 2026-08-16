using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
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
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return;
        try
        {
            JObject doc = await store.GetOnboardingAsync(null, cancellationToken).ConfigureAwait(false);
            if (doc == null) return;
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
        if (store == null) return;
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
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_onboarding_save_error error=" + ex.Message);
        }
    }

    internal static bool TryShowGuide()
    {
        if (!ShouldShowGuide() || HasConfiguredAi()) return false;
        _shownThisCampaign = true;
        MarkStepCompleted(AwakeOnboardingStep.Welcome);
        Progress.LastReminderDay = AwakeRuntime.CurrentGameDay();
        _ = SaveAsync(CancellationToken.None);
        try
        {
            AwakeLog.Write("awake_onboarding_show active_state="
                + (GameStateManager.Current?.ActiveState?.GetType().Name ?? "none"));
            InformationManager.ShowInquiry(
                new InquiryData(
                    AwakeLocalization.Resolve("awake.onboarding.title", "醒世 · 首启向导"),
                    AwakeLocalization.Resolve(
                        "awake.onboarding.text",
                        "开始前需要先配置 AI 服务商、模型与路由。打开 AI 设置台完成配置后，就能进入 NPC 对话。"),
                    true,
                    true,
                    AwakeLocalization.Resolve("awake.onboarding.open", "打开 AI 设置"),
                    AwakeLocalization.Resolve("awake.onboarding.later", "稍后"),
                    () =>
                    {
                        try
                        {
                            AwakeMarcusLinkService.OpenAiSetup();
                        }
                        catch (Exception ex)
                        {
                            AwakeLog.Write("awake_onboarding_open_setup_error error=" + ex.Message);
                        }
                    },
                    null,
                    string.Empty,
                    0f,
                    null,
                    null,
                    null),
                true,
                false);
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_onboarding_show_error error=" + ex.Message);
            _shownThisCampaign = false;
            return false;
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
