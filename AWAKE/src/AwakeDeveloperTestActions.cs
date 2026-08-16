using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal static class AwakeDeveloperTestActions
{
    internal static void OpenDeveloperReport()
    {
        AwakeMcmActions.ShowDeveloperReport();
    }

    internal static void TestNearbyDialogue()
    {
        List<Hero> heroes = NpcDialogueLauncher.GetNearbyHeroes(1);
        if (heroes.Count == 0)
        {
            AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                "awake.dev_tools.no_target",
                "附近没有可交谈的目标。"));
            return;
        }
        NpcDialogueLaunchResult result = NpcDialogueLauncher.TryOpenDialogue(heroes[0], "dev_test");
        if (result == NpcDialogueLaunchResult.None)
        {
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.dev_tools.dialogue_failed",
                "深谈打开失败。"));
        }
        else
        {
            AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
                "awake.dev_tools.dialogue_ok",
                "深谈已打开。"));
        }
    }

    internal static void TestWorldInbox()
    {
        AwakeTerminalBehavior.ShowWorldInboxForMcm();
    }

    internal static void TestWeeklyReport()
    {
        AwakeTerminalBehavior.ShowWeeklyReportForMcm();
    }

    internal static void ResetProactive()
    {
        NpcProactiveService.ClearForTesting();
        AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
            "awake.dev_tools.proactive_reset",
            "NPC 主动状态已重置。"));
    }
}
