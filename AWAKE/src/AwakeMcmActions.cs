using System;
using MCM.Abstractions;

namespace Awake;

internal static class AwakeMcmActions
{
    internal static Action ShowDeveloperReport = () =>
        AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
            "awake.mcm.actions.developer_unavailable",
            "开发者检查暂不可用。"));

    internal static void ConfigureProviderApiKey()
    {
        AwakeProviderConfiguration.PromptForApiKey();
    }

    internal static void ApplyProviderConfiguration()
    {
        AwakeProviderConfiguration.ApplyProviderConfiguration();
    }

    internal static void PullProviderModels()
    {
        AwakeProviderConfiguration.PullModels();
    }

    internal static void TestProviderConnection()
    {
        AwakeProviderConfiguration.TestConnection();
    }

    internal static void RefreshAiStatus()
    {
        AwakeProviderConfiguration.RefreshRuntimeStatus();
    }

    internal static void ConfigureImageApiKey()
    {
        AwakeImageConfiguration.PromptForApiKey();
    }

    internal static void TestImageGeneration()
    {
        AwakeImageProbe.Run();
    }
}
