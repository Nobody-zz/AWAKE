using System;
using MarcusAIFramework.Api;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Awake;

internal static class AwakeOnboardingService
{
    private static bool _shownThisCampaign;

    internal static void ResetForCampaign()
    {
        _shownThisCampaign = false;
    }

    internal static bool ShouldShowGuide()
    {
        return !_shownThisCampaign;
    }

    internal static bool TryShowGuide()
    {
        if (_shownThisCampaign || HasConfiguredAi()) return false;
        _shownThisCampaign = true;
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

    internal static void ResetForTesting()
    {
        _shownThisCampaign = false;
    }

    internal static void MarkShownForTesting()
    {
        _shownThisCampaign = true;
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
}
