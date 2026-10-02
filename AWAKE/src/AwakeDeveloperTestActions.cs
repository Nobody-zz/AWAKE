using System.Collections.Generic;
using System.Text;
using System.Threading;
using MarcusAwakeFramework.Api;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Awake;

internal static class AwakeDeveloperTestActions
{
    internal static void OpenDeveloperReport()
    {
        AwakeMcmActions.ShowDeveloperReport();
    }

    /// <summary>
    /// 画位探测：验证「png → 运行时纹理 → 控件上屏」这条链，不依赖出图后端。
    /// 面板里可以直接写一张内置占位图进缓存，看见图就说明链路是通的。
    /// </summary>
    internal static void OpenPortraitProbe()
    {
        if (AwakePortraitProbeOverlay.Open())
        {
            AwakeFeedback.ShowSuccess("画位探测已打开：面板里会显示缓存目录和当前画位。");
        }
        else
        {
            AwakeFeedback.ShowError("画位探测打不开，看 Logs/Awake.log 的 portrait_probe_open_* 那几行。");
        }
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

    internal static void TestNearbyNegotiation()
    {
        List<Hero> heroes = NpcDialogueLauncher.GetNearbyHeroes(1);
        if (heroes.Count == 0)
        {
            AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                "awake.dev_tools.no_target",
                "Nearby target missing."));
            return;
        }
        NpcDialogueLaunchResult result = NpcDialogueLauncher.TryOpenDialogue(
            AwakeNpcTarget.FromHero(heroes[0]),
            "dev_test.negotiation",
            NpcDialogueActionMode.Negotiation);
        if (result == NpcDialogueLaunchResult.None)
        {
            AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                "awake.dev_tools.negotiation_failed",
                "Negotiation test failed to open."));
        }
        else
        {
            AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
                "awake.dev_tools.negotiation_ok",
                "Negotiation test opened."));
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

    /// <summary>
    /// 缺陷①（2026-10-01）：坏的世界事实账本以前没有显式恢复入口 —— 读路径把 Codec 判断出的子原因
    /// 塌缩成 awake.world_fact.root_corrupt，写侧要等到"恰好又写进一条世界事实"才会隔离重建，
    /// 中间周报链一直 status=unavailable。这个动作把坏值隔离到旁路 key 并重建空账本（revision 1），
    /// 之后周报链读到的是 Empty 而不是 Corrupt。
    /// 按 docs/PLAN-REPAIR-WORLDBOOK-HASH-AND-FOUR-DEFECTS-20261001.md §4.6：这是开发者调试入口，
    /// 不是玩家选项，所以只挂在这里，不进 MCM。
    /// </summary>
    internal static void ResetWorldFactJournal()
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null)
        {
            AwakeFeedback.ShowWarning(AwakeLocalization.Resolve(
                "awake.dev_tools.world_fact_journal_reset_no_store",
                "世界事实账本还没打开（先开始一场战役）。"));
            return;
        }
        AwakeBackgroundTask.Run(
            async () =>
            {
                OperationResult<string> result = await store.ResetWorldFactJournalAsync(null, CancellationToken.None)
                    .ConfigureAwait(false);
                // 回 UI 线程再弹提示：AwakeFeedback 直接调 InformationManager，不自己排队。
                AwakeUiDispatcher.Enqueue(() =>
                {
                    if (result.IsSuccess)
                    {
                        AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
                            "awake.dev_tools.world_fact_journal_reset_ok",
                            "世界事实账本已重置，诊断码：") + result.Value);
                    }
                    else
                    {
                        AwakeFeedback.ShowError(AwakeLocalization.Resolve(
                            "awake.dev_tools.world_fact_journal_reset_failed",
                            "世界事实账本重置失败：") + (result.Error?.Code ?? "unknown"));
                    }
                });
            },
            "awake_dev_world_fact_journal_reset");
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
