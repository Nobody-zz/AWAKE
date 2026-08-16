using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;

namespace Awake;

internal static class AwakeMarcusLinkService
{
    internal static string BuildStatusText()
    {
        try
        {
            bool connected = FrameworkHostLocator.TryGetHost(out IMarcusAiFrameworkHost host);
            if (!connected || host == null)
            {
                return AwakeLocalization.Resolve("awake.status.degraded_offline", "Offline (degraded)");
            }

            int declared = AiTaskConstants.AllRouteIds.Length;

            string health = string.Empty;
            try
            {
                if (host.Diagnostics != null)
                {
                    IReadOnlyList<HealthComponent> components = host.Diagnostics.GetHealth().Components;
                    if (components != null)
                    {
                        List<string> summaries = new List<string>();
                        foreach (HealthComponent component in components)
                        {
                            if (component == null || string.IsNullOrWhiteSpace(component.Summary)) continue;
                            summaries.Add(component.Id + ":" + component.Summary);
                            if (summaries.Count >= 3) break;
                        }
                        health = string.Join(" | ", summaries);
                    }
                }
            }
            catch (Exception ex)
            {
                AwakeLog.Write("marcus_link_health_error error=" + ex.Message);
            }

            string companion = AwakeLocalization.Resolve(
                "awake.status.companion",
                "Companion: {STATE}",
                new Dictionary<string, string>
                {
                    ["STATE"] = AwakeLocalization.Resolve("awake.status.connected", "Connected")
                });
            string route = AwakeLocalization.Resolve(
                "awake.status.route",
                "Route: {ROUTE}",
                new Dictionary<string, string> { ["ROUTE"] = declared.ToString() });
            string result = companion + " | " + route;
            if (!string.IsNullOrWhiteSpace(health)) result += " | " + health;
            return result;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("marcus_link_status_error error=" + ex.Message);
            return AwakeLocalization.Resolve("awake.status.degraded_offline", "Offline (degraded)");
        }
    }

    internal static async Task SyncRoutesAsync(CancellationToken cancellationToken)
    {
        try
        {
            AwakeFeedback.Show(AwakeLocalization.Resolve(
                "awake.mcm.actions.sync_routes_result",
                "路由由框架自动同步；已打开 AI 设置台。"));
            FrameworkConsole.OpenAiSetup();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("marcus_link_sync_error error=" + ex.Message);
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.feedback.marcus_sync_failed",
                "路由同步失败，请打开 AI 设置台。"));
        }
    }

    internal static void OpenAiSetup()
    {
        try
        {
            FrameworkConsole.OpenAiSetup();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("marcus_link_open_setup_error error=" + ex.Message);
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.feedback.marcus_open_setup_failed",
                "无法打开 AI 设置台。"));
        }
    }

    internal static void OpenDiagnostics()
    {
        try
        {
            FrameworkConsole.OpenDiagnostics();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("marcus_link_open_diagnostics_error error=" + ex.Message);
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.feedback.marcus_open_diagnostics_failed",
                "无法打开诊断台。"));
        }
    }
}
