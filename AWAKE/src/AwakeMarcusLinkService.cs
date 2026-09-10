using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

internal static class AwakeMarcusLinkService
{
    internal static string BuildStatusText()
    {
        try
        {
            IMarcusAwakeFrameworkHost fullHost = FrameworkHostLocator.Resolve();
            IMarcusAiFrameworkHost host = fullHost as IMarcusAiFrameworkHost;
            if (fullHost == null || host == null)
            {
                return AwakeLocalization.Resolve("awake.status.degraded_offline", "Offline (degraded)");
            }

            int declared = AiTaskConstants.AllRouteIds.Length;
            RuntimeServiceStatus runtime = fullHost.Runtime?.Status;
            string runtimeText = AwakeLocalization.Resolve(
                "awake.status.runtime",
                "Runtime Service: {STATE}",
                new Dictionary<string, string> { ["STATE"] = DescribeRuntimeState(runtime?.State) });
            string providerText = AwakeLocalization.Resolve(
                "awake.status.provider",
                "Provider: {STATE}",
                new Dictionary<string, string> { ["STATE"] = AwakeProviderConfiguration.Status });
            string route = AwakeLocalization.Resolve(
                "awake.status.route",
                "AI routes: {ROUTE}",
                new Dictionary<string, string> { ["ROUTE"] = declared.ToString() });
            string cloud = AwakeLocalization.Resolve(
                "awake.status.cloud",
                "Cloud: {STATE}",
                new Dictionary<string, string>
                {
                    ["STATE"] = CloudExportPolicy.DescribeAllowed(AwakeSettings.Current)
                });
            string session = host.CurrentSession == null
                ? AwakeLocalization.Resolve("awake.status.session_not_ready", "Campaign session: not ready")
                : AwakeLocalization.Resolve("awake.status.session_ready", "Campaign session: ready");
            List<string> parts = new List<string> { runtimeText, providerText, route, session, cloud };
            string result = string.Join(" | ", parts);
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
            if (cancellationToken.IsCancellationRequested) return;
            OperationResult<bool> result = await AwakeProviderConfiguration.ApplyProviderConfigurationAsync(cancellationToken).ConfigureAwait(false);
            AwakeUiDispatcher.Enqueue(() =>
            {
                if (result.IsSuccess)
                {
                    AwakeFeedback.Show(AwakeLocalization.Resolve(
                        "awake.mcm.actions.sync_routes_result",
                        "AWAKE 路由已由内置 Runtime 完成同步。"));
                }
                else
                {
                    AwakeFeedback.ShowError(result.Error?.SafeFallback ?? AwakeLocalization.Resolve(
                        "awake.feedback.marcus_sync_failed",
                        "路由同步失败，请在 AWAKE MCM 中检查 AI 链路配置。"));
                }
            });
        }
        catch (Exception ex)
        {
            AwakeLog.Write("marcus_link_sync_error error=" + ex.Message);
            AwakeUiDispatcher.Enqueue(() => AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.feedback.marcus_sync_failed",
                "路由同步失败，请在 AWAKE MCM 中检查 AI 链路配置。")));
        }
    }

    internal static void OpenAiSetup()
    {
        try
        {
            AwakeFeedback.Show(AwakeLocalization.Resolve(
                "awake.feedback.open_mcm",
                "请打开 MCM → AWAKE → AI 链路，在游戏内完成 AI 配置。"));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("marcus_link_open_setup_error error=" + ex.Message);
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.feedback.marcus_open_setup_failed",
                "无法显示 AI 配置指引，请直接打开 AWAKE MCM。"));
        }
    }

    internal static void OpenDiagnostics()
    {
        try
        {
            AwakeFeedback.Show(AwakeLocalization.Resolve(
                "awake.feedback.open_diagnostics",
                "开发者诊断已归 AWAKE Developer Check 与本地日志；不会在游戏内显示完整日志。"));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("marcus_link_open_diagnostics_error error=" + ex.Message);
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.feedback.marcus_open_diagnostics_failed",
                "无法显示诊断说明，请查看本地 AWAKE 日志。"));
        }
    }

    private static string DescribeRuntimeState(RuntimeServiceState? state)
    {
        switch (state)
        {
            case RuntimeServiceState.Ready:
                return AwakeLocalization.Resolve("awake.status.ready", "已就绪");
            case RuntimeServiceState.Starting:
                return AwakeLocalization.Resolve("awake.status.starting", "启动中");
            case RuntimeServiceState.Draining:
                return AwakeLocalization.Resolve("awake.status.draining", "关闭中");
            case RuntimeServiceState.RecoveryRequired:
                return AwakeLocalization.Resolve("awake.status.recovery_required", "需要恢复");
            case RuntimeServiceState.Stopped:
                return AwakeLocalization.Resolve("awake.status.stopped", "未运行");
            default:
                return AwakeLocalization.Resolve("awake.status.unknown", "未知");
        }
    }
}
