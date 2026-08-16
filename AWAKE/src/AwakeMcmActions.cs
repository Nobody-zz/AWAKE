using System;
using System.Threading;
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
        AwakeBackgroundTask.Run(() => AwakeMarcusLinkService.SyncRoutesAsync(CancellationToken.None), "marcus_sync_routes");
    }

    internal static void RefreshAiStatus()
    {
        AwakeRuntimeStatus.Update(AwakeMarcusLinkService.BuildStatusText());
        AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
            "awake.mcm.actions.status_refreshed",
            "AI 状态已刷新。"));
    }

    internal static void OpenAiSetup()
    {
        AwakeMarcusLinkService.OpenAiSetup();
    }

    internal static void OpenDiagnostics()
    {
        AwakeMarcusLinkService.OpenDiagnostics();
    }
}
