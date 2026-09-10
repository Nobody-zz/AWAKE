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
        IWorldKnowledgeQuery service = WorldbookRuntime.Knowledge;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(WorldbookRuntime.BuildStatusText());
        if (service != null)
        {
            builder.AppendLine("新读取器已接管 NPC 知识查询。");
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

    internal static void EditWorldbook()
    {
        if (WorldbookRuntime.Knowledge == null)
        {
            AwakeFeedback.ShowWarning(AwakeLocalization.Resolve("awake.worldbook.not_loaded", "世界书未加载。"));
            return;
        }
        InformationManager.ShowTextInquiry(
            new TextInquiryData(
                "编辑世界知识",
                "输入要修改的条目 ID（例如 awake:entry:...）：",
                true,
                true,
                "下一步",
                "取消",
                input => PromptWorldbookSummary((input ?? string.Empty).Trim()),
                null,
                false,
                null,
                string.Empty,
                string.Empty),
            true,
            false);
    }

    internal static void ExportWorldbookOverlay()
    {
        string path = WorldbookRuntime.ExportOverlay();
        if (string.IsNullOrWhiteSpace(path))
        {
            AwakeFeedback.ShowWarning("世界书未加载，暂无可导出的修改。");
            return;
        }
        AwakeFeedback.ShowSuccess("世界书修改已导出：" + path);
    }

    private static void PromptWorldbookSummary(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId)) return;
        InformationManager.ShowTextInquiry(
            new TextInquiryData(
                "编辑档案摘要",
                "输入新的摘要文本；这只修改当前战役 Overlay，不改基础世界书：",
                true,
                true,
                "保存",
                "取消",
                input =>
                {
                    string error;
                    bool ok = WorldbookRuntime.TryApplyOverlay("replace_summary", entryId, input ?? string.Empty, WorldbookRuntime.OverlayRevision, "玩家在游戏内编辑档案摘要", out error);
                    if (ok) AwakeFeedback.ShowSuccess("档案已修改，Overlay revision=" + WorldbookRuntime.OverlayRevision);
                    else AwakeFeedback.ShowError("修改失败：" + error);
                },
                null,
                false,
                null,
                string.Empty,
                string.Empty),
            true,
            false);
    }

    private static void ShowWorldbookSearchResults(string input)
    {
        IWorldKnowledgeQuery service = WorldbookRuntime.Knowledge;
        if (service == null)
        {
            AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                "awake.worldbook.not_loaded",
                "Worldbook is not loaded."));
            return;
        }
        List<WorldKnowledgeEntry> hits = service.Search(input, 20);
        StringBuilder builder = new StringBuilder();
        if (hits.Count == 0)
        {
            builder.AppendLine(AwakeLocalization.Resolve("awake.worldbook.no_hits", "No matching rules."));
        }
        else
        {
            foreach (WorldKnowledgeEntry entry in hits)
            {
                builder.AppendLine("- " + entry.Id + " [" + entry.Domain + "] " + entry.Title);
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
