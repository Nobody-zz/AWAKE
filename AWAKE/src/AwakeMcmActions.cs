using System;
using MarcusAIFramework.Api;

namespace Awake;

internal static class AwakeMcmActions
{
    internal static Action ShowDeveloperReport = () =>
        AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
            "awake.mcm.actions.developer_unavailable",
            "开发者检查暂不可用。"));

    internal static void SyncRoutes()
    {
        AwakeFeedback.Show(AwakeLocalization.Resolve(
            "awake.mcm.actions.sync_routes_result",
            "路由由框架自动同步；可在 AI 设置台点击“同步路由”。"));
    }

    internal static void RefreshAiStatus()
    {
        bool connected = FrameworkHostLocator.TryGetHost(out _);
        AwakeRuntimeStatus.Update(connected
            ? AwakeLocalization.Resolve("awake.status.connected", "Connected")
            : AwakeLocalization.Resolve("awake.status.degraded_offline", "Offline (degraded)"));
        AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
            "awake.mcm.actions.status_refreshed",
            "AI 状态已刷新。"));
    }
}
