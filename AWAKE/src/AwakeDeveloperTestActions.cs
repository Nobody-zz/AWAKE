using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

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
                "Nearby target missing."));
            return;
        }
        NpcDialogueLaunchResult result = NpcDialogueLauncher.TryOpenDialogue(heroes[0], "dev_test");
        if (result == NpcDialogueLaunchResult.None)
        {
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.dev_tools.dialogue_failed",
                "Dialogue failed to open."));
        }
        else
        {
            AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
                "awake.dev_tools.dialogue_ok",
                "Dialogue opened."));
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
            "NPC proactive state reset."));
    }

    internal static void ShowWorldbookStatus()
    {
        WorldbookService service = WorldbookRuntime.Current;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(WorldbookRuntime.BuildStatusText());
        if (service != null)
        {
            int shown = 0;
            foreach (WorldbookImportWarning warning in service.Warnings)
            {
                if (shown >= 8) break;
                builder.AppendLine("- " + warning.Source + ": " + warning.Message);
                shown++;
            }
        }
        InformationManager.ShowInquiry(
            new InquiryData(
                AwakeLocalization.Resolve("awake.worldbook.status_title", "Worldbook Status"),
                builder.ToString(),
                true,
                false,
                AwakeLocalization.Resolve("awake.ui.close", "Close"),
                string.Empty,
                null,
                null,
                string.Empty,
                0f,
                null,
                null,
                null),
            true,
            false);
    }

    internal static void SearchWorldbook()
    {
        InformationManager.ShowTextInquiry(
            new TextInquiryData(
                AwakeLocalization.Resolve("awake.worldbook.search_title", "Worldbook Search"),
                AwakeLocalization.Resolve("awake.worldbook.search_prompt", "Enter a keyword or RuleId:"),
                true,
                true,
                AwakeLocalization.Resolve("awake.worldbook.search", "Search"),
                AwakeLocalization.Resolve("awake.ui.cancel", "Cancel"),
                input => ShowWorldbookSearchResults(input ?? string.Empty),
                null,
                false,
                null,
                string.Empty,
                string.Empty),
            true,
            false);
    }

    internal static void ReloadWorldbook()
    {
        WorldbookRuntime.Reload();
        AwakeFeedback.ShowSuccess(WorldbookRuntime.BuildStatusText());
    }

    private static void ShowWorldbookSearchResults(string input)
    {
        WorldbookService service = WorldbookRuntime.Current;
        if (service == null)
        {
            AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                "awake.worldbook.not_loaded",
                "Worldbook is not loaded."));
            return;
        }
        List<WorldbookRule> hits = service.Search(input, 20);
        StringBuilder builder = new StringBuilder();
        if (hits.Count == 0)
        {
            builder.AppendLine(AwakeLocalization.Resolve("awake.worldbook.no_hits", "No matching rules."));
        }
        else
        {
            foreach (WorldbookRule rule in hits)
            {
                builder.AppendLine("- " + rule.Id + " [priority=" + rule.Priority + "]");
            }
        }
        InformationManager.ShowInquiry(
            new InquiryData(
                AwakeLocalization.Resolve("awake.worldbook.search_result", "Worldbook Search Result"),
                builder.ToString(),
                true,
                false,
                AwakeLocalization.Resolve("awake.ui.close", "Close"),
                string.Empty,
                null,
                null,
                string.Empty,
                0f,
                null,
                null,
                null),
            true,
            false);
    }
}
