using System;
using System.Threading;
using MarcusAIFramework.Api;
using MCM.Abstractions;

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

    internal static void EnableCloudDialogueOneClick()
    {
        try
        {
            AwakeConfig config = AwakeSettings.Current;
            config.EnableCloudExport = true;
            config.AllowCloudExportPlayerState = true;

            bool saved = false;
            try
            {
                if (BaseSettingsProvider.Instance != null)
                {
                    BaseSettingsProvider.Instance.SaveSettings(config);
                    saved = true;
                }
            }
            catch (Exception saveEx)
            {
                AwakeLog.Write("mcm_cloud_oneclick_save_failed error=" + saveEx.Message);
            }

            AwakeRuntimeStatus.Update(AwakeMarcusLinkService.BuildStatusText());
            if (saved)
            {
                AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
                    "awake.mcm.actions.cloud_oneclick_ok",
                    "Cloud export enabled. Allow AWAKE.route.npc.dialogue cloud export in the AI setup console."));
            }
            else
            {
                AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                    "awake.mcm.actions.cloud_oneclick_session_only",
                    "Cloud export enabled for this session. Save MCM settings, then allow AWAKE.route.npc.dialogue cloud export in the AI setup console."));
            }

            AwakeMarcusLinkService.OpenAiSetup();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("mcm_cloud_oneclick_failed error=" + ex.Message);
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.mcm.actions.cloud_oneclick_failed",
                "One-click cloud enable failed. Enable cloud export manually."));
        }
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
