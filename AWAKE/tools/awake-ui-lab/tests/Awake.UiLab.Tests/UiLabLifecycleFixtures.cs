using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Awake.UiLab;

namespace Awake.UiLab.Tests
{
    /// <summary>
    /// E2 生命周期用例。每条用例都断言：
    /// movie / layer / 输入限制 / 焦点四项释放、创建计数 == 释放计数、generation 单调。
    /// 这些用例只用假平台，不渲染、不启动 Bannerlord。
    /// </summary>
    internal static class UiLabLifecycleFixtures
    {
        private static int _movieSeq;

        internal static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAILED: " + name);
        }

        private static string NextMovieId()
        {
            _movieSeq++;
            return "AwakeUiLab.local" + _movieSeq.ToString();
        }

        private static UiLabHostSession NewSession(FakeUiLabPlatform platform, string movieId)
        {
            return new UiLabHostSession(
                platform, movieId, movieId, UiLabHostIdentity.LocalOrder,
                UiLabHostIdentity.OwnerId, UiLabHostIdentity.CandidateId);
        }

        private static bool Contains(IReadOnlyList<UiLabRequestOutcome> journal, string code)
        {
            for (int i = 0; i < journal.Count; i++)
            {
                if (journal[i].Code == code) return true;
            }
            return false;
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value) return true;
            }
            return false;
        }

        private static void AssertClean(FakeUiLabPlatform platform, UiLabHostSession session, string label)
        {
            Check(platform.IsClean, label + " 存在泄漏 -> " + platform.LeakReport());
            Check(session.AllOwnershipReleased, label + " 会话仍持有所有权");
            Check(!session.OwnerHeld, label + " owner 未释放");
        }

        // ------------------------------------------------------------------ 用例

        internal static void DuplicateOpenCoalesced()
        {
            FakeUiLabPlatform platform = new FakeUiLabPlatform();
            using (UiLabHostSession session = NewSession(platform, NextMovieId()))
            {
                UiLabRequestOutcome first = session.RequestOpen(UiLabRequestSource.Local);
                Check(first.Code == "opened", "首次打开应为 opened，实际 " + first.Code);
                Check(session.Generation == 1, "首次打开 generation 应为 1");

                UiLabRequestOutcome second = session.RequestOpen(UiLabRequestSource.F10);
                Check(second.Code == "already_open", "重复打开应合并为 already_open，实际 " + second.Code);
                Check(session.Generation == 1, "重复打开不得推进 generation");

                Check(platform.LayersCreated == 1, "重复打开不得创建第二个 layer");
                Check(platform.LayersAdded == 1, "重复打开不得重复加入 layer");
                Check(platform.MoviesLoaded == 1, "重复打开不得重复加载 movie");

                UiLabRequestOutcome close = session.RequestClose(UiLabRequestSource.Local);
                Check(close.Code == "closed", "关闭应为 closed，实际 " + close.Code);
                Check(platform.LayersRemoved == 1, "layer 释放数应为 1");
                AssertClean(platform, session, "duplicate_open_coalesced");
            }
        }

        internal static void OpenWhileClosingReopensAfterCleanup()
        {
            FakeUiLabPlatform platform = new FakeUiLabPlatform();
            platform.SetCleanupFailure("focus", 1);

            using (UiLabHostSession session = NewSession(platform, NextMovieId()))
            {
                Check(session.RequestOpen(UiLabRequestSource.Local).Code == "opened", "打开失败");

                UiLabRequestOutcome close = session.RequestClose(UiLabRequestSource.Local);
                Check(close.Code == "cleanup_incomplete", "首次清理失败应报 cleanup_incomplete，实际 " + close.Code);
                Check(session.State == UiLabSessionState.Closing, "清理未完成时应处于 Closing");
                Check(!session.AllOwnershipReleased, "清理未完成时不得宣称已释放");
                Check(platform.AnyFocusLayer, "注入故障后焦点应仍然被持有（可见，不静默）");

                UiLabRequestOutcome reopen = session.RequestOpen(UiLabRequestSource.Local);
                Check(reopen.Code == "coalesced", "关闭途中打开应合并为 coalesced，实际 " + reopen.Code);

                bool didWork = session.Tick();
                Check(didWork, "Tick 应推进清理与合并重开");
                Check(session.State == UiLabSessionState.Open, "清理完成后应自动重开，实际 " + session.State);
                Check(session.Generation == 2, "重开应拿到新 generation=2，实际 " + session.Generation.ToString());
                Check(platform.LayersCreated == 2, "重开应只新增一次 layer");
                Check(platform.LayersRemoved == 1, "重开前应已释放旧 layer");

                Check(session.RequestClose(UiLabRequestSource.Local).Code == "closed", "收尾关闭失败");
                Check(platform.LayersCreated == platform.LayersRemoved, "创建数应等于释放数");
                AssertClean(platform, session, "open_while_closing_reopens_after_cleanup");
            }
        }

        internal static void ScreenSwitchReleasesAll()
        {
            FakeUiLabPlatform platform = new FakeUiLabPlatform();

            using (UiLabHostSession session = NewSession(platform, NextMovieId()))
            {
                Check(session.RequestOpen(UiLabRequestSource.Local).Code == "opened", "打开失败");
                Check(!platform.IsClean, "打开后平台应处于持有状态");
                Check(platform.LayersAdded == 1 && platform.AnyFocusLayer && platform.AnyInputRestricted,
                    "打开后应持有 layer / 焦点 / 输入限制");

                platform.Screen = new FakeUiLabScreen("map_screen");

                bool didWork = session.Tick();
                Check(didWork, "界面切换后 Tick 应做清理");
                Check(session.State == UiLabSessionState.Closed, "界面切换后应关闭，实际 " + session.State);
                Check(Contains(session.TeardownSteps, "begin:screen_changed"), "清理原因应为 screen_changed");
                Check(platform.LayersCreated == 1 && platform.LayersRemoved == 1, "layer 应创建一次并释放一次");
                AssertClean(platform, session, "screen_switch_releases_all");
            }
        }

        internal static void PartialLoadFailureRollsBack()
        {
            // 场景 A：movie 加载返回"未成功"
            FakeUiLabPlatform platformA = new FakeUiLabPlatform();
            platformA.FailMovieLoad = true;
            using (UiLabHostSession session = NewSession(platformA, NextMovieId()))
            {
                UiLabRequestOutcome outcome = session.RequestOpen(UiLabRequestSource.Local);
                Check(outcome.Code == "movie_load_failed", "应报 movie_load_failed，实际 " + outcome.Code);
                Check(session.Generation == 0, "失败打开不得推进 generation");
                Check(platformA.LayersRemoved == 1, "已加入的 layer 应被回滚");
                AssertClean(platformA, session, "partial_load_failure(movie)");
            }

            // 场景 B：设置输入限制时抛错
            FakeUiLabPlatform platformB = new FakeUiLabPlatform();
            platformB.InputRestrictionFailures = 1;
            using (UiLabHostSession session = NewSession(platformB, NextMovieId()))
            {
                UiLabRequestOutcome outcome = session.RequestOpen(UiLabRequestSource.Local);
                Check(outcome.Code == "open_exception", "应报 open_exception，实际 " + outcome.Code);
                Check(platformB.UnreleasedMovieCount == 0, "失败打开不得留下未释放 movie");
                AssertClean(platformB, session, "partial_load_failure(input)");
            }

            // 场景 C：取得焦点后抛错（焦点已被授予，必须回滚）
            FakeUiLabPlatform platformC = new FakeUiLabPlatform();
            platformC.FocusFailures = 1;
            using (UiLabHostSession session = NewSession(platformC, NextMovieId()))
            {
                UiLabRequestOutcome outcome = session.RequestOpen(UiLabRequestSource.Local);
                Check(outcome.Code == "open_exception", "应报 open_exception，实际 " + outcome.Code);
                Check(Contains(session.TeardownSteps, "release:focus"), "焦点已授予时必须回滚焦点");
                Check(!platformC.AnyFocusLayer, "焦点不得残留");
                AssertClean(platformC, session, "partial_load_failure(focus)");
            }

            // 场景 D：movie 加载直接抛错
            FakeUiLabPlatform platformD = new FakeUiLabPlatform();
            platformD.ThrowOnMovieLoad = true;
            using (UiLabHostSession session = NewSession(platformD, NextMovieId()))
            {
                UiLabRequestOutcome outcome = session.RequestOpen(UiLabRequestSource.Local);
                Check(outcome.Code == "open_exception", "应报 open_exception，实际 " + outcome.Code);
                AssertClean(platformD, session, "partial_load_failure(throw)");
            }
        }

        internal static void CloseReopenNoLeak()
        {
            FakeUiLabPlatform platform = new FakeUiLabPlatform();

            using (UiLabHostSession session = NewSession(platform, NextMovieId()))
            {
                Check(session.RequestOpen(UiLabRequestSource.Local).Code == "opened", "首次打开失败");
                Check(session.RequestClose(UiLabRequestSource.Local).Code == "closed", "首次关闭失败");
                AssertClean(platform, session, "close_reopen_no_leak(第一次关闭后)");

                Check(session.RequestOpen(UiLabRequestSource.Local).Code == "opened", "重开失败");
                Check(session.Generation == 2, "重开 generation 应为 2，实际 " + session.Generation.ToString());
                Check(session.RequestClose(UiLabRequestSource.Local).Code == "closed", "二次关闭失败");

                Check(platform.LayersCreated == 2 && platform.LayersRemoved == 2, "layer 创建/释放各应为 2");
                Check(platform.MoviesLoaded == 2 && platform.MoviesReleased == 2, "movie 加载/释放各应为 2");
                Check(platform.InputSetCount == platform.InputResetCount, "输入限制设置/复位应配平");
                AssertClean(platform, session, "close_reopen_no_leak");
            }
        }

        internal static void UnloadCloseIntentOnlyThenTickCleanup()
        {
            FakeUiLabPlatform platform = new FakeUiLabPlatform();

            using (UiLabHostSession session = NewSession(platform, NextMovieId()))
            {
                Check(session.RequestOpen(UiLabRequestSource.Local).Code == "opened", "打开失败");
                Check(platform.ReleaseSequence().Count == 0, "打开后不应有释放动作");

                UiLabRequestOutcome unload = session.Unload();
                Check(unload.Code == "cleanup_queued", "Unload 应只写 close intent，实际 " + unload.Code);
                Check(!session.AllOwnershipReleased, "Unload 不得同步清理");
                Check(session.State == UiLabSessionState.Closing, "Unload 后应处于 Closing");

                session.Tick();
                AssertClean(platform, session, "unload_close_intent_only_then_tick_cleanup");

                IReadOnlyList<string> sequence = platform.ReleaseSequence();
                Check(sequence.Count == 4, "应有 4 个释放步骤，实际 " + sequence.Count.ToString());
                Check(sequence[0] == "focus:lost", "释放第 1 步应为失焦，实际 " + sequence[0]);
                Check(sequence[1] == "input:reset", "释放第 2 步应为复位输入限制，实际 " + sequence[1]);
                Check(sequence[2] == "movie:released", "释放第 3 步应为释放 movie，实际 " + sequence[2]);
                Check(sequence[3] == "layer:removed", "释放第 4 步应为移除 layer，实际 " + sequence[3]);

                IReadOnlyList<string> steps = session.TeardownSteps;
                Check(steps[steps.Count - 1] == "release:owner", "owner 必须在最后一步释放，实际 " + steps[steps.Count - 1]);
            }
        }

        internal static void CleanupRetryThenExhausted()
        {
            FakeUiLabPlatform platform = new FakeUiLabPlatform();
            platform.SetCleanupFailure("movie", UiLabHostSession.MaxCleanupAttempts);

            using (UiLabHostSession session = NewSession(platform, NextMovieId()))
            {
                Check(session.RequestOpen(UiLabRequestSource.Local).Code == "opened", "打开失败");

                UiLabRequestOutcome close = session.RequestClose(UiLabRequestSource.Local);
                Check(close.Code == "cleanup_incomplete", "第 1 次清理失败应报 cleanup_incomplete，实际 " + close.Code);

                session.Tick();
                Check(session.State != UiLabSessionState.Faulted, "第 2 次仍应处于重试态");

                session.Tick();
                Check(session.State == UiLabSessionState.Faulted, "重试耗尽后应 Faulted，实际 " + session.State);
                Check(session.LastFailureCode == "cleanup_retry_exhausted", "失败码应为 cleanup_retry_exhausted");
                Check(session.CleanupAttempt >= UiLabHostSession.MaxCleanupAttempts, "应记录重试次数");
                Check(!session.AllOwnershipReleased, "耗尽后必须保留持有（可见，不静默）");
                Check(session.OwnerHeld, "耗尽后不得释放 owner");

                bool didWork = session.Tick();
                Check(!didWork, "Faulted 后 Tick 不应再做动作");

                UiLabRequestOutcome reopen = session.RequestOpen(UiLabRequestSource.F10);
                Check(reopen.Code == "cleanup_retry_exhausted", "Faulted 状态下打开应被拒，实际 " + reopen.Code);
            }
        }

        internal static void StaleGenerationIgnored()
        {
            FakeUiLabPlatform platform = new FakeUiLabPlatform();

            using (UiLabHostSession session = NewSession(platform, NextMovieId()))
            {
                Check(session.RequestOpen(UiLabRequestSource.Local).Code == "opened", "首次打开失败");
                long staleGeneration = session.Generation;

                Check(session.RequestClose(UiLabRequestSource.Local).Code == "closed", "关闭失败");
                Check(session.RequestOpen(UiLabRequestSource.Local).Code == "opened", "重开失败");
                Check(session.Generation == staleGeneration + 1, "重开应推进 generation");

                UiLabRequestOutcome lateClose = session.RequestClose(UiLabRequestSource.Local, staleGeneration);
                Check(lateClose.Code == "stale_ignored", "旧 generation 关闭应被忽略，实际 " + lateClose.Code);
                Check(session.State == UiLabSessionState.Open, "迟到关闭不得误关新会话");

                bool didWork = session.Tick(staleGeneration);
                Check(!didWork, "旧 generation 的 tick 不应做动作");
                Check(session.State == UiLabSessionState.Open, "迟到 tick 不得误关新会话");
                Check(session.Generation == staleGeneration + 1, "迟到 tick 不得推进 generation");
                Check(Contains(session.Journal, "stale_ignored"), "应记账 stale_ignored");

                Check(session.RequestClose(UiLabRequestSource.Local).Code == "closed", "收尾关闭失败");
                AssertClean(platform, session, "stale_generation_ignored");
            }
        }

        internal static void DualOwnerConflict()
        {
            string sharedMovieId = NextMovieId();
            FakeUiLabPlatform platformA = new FakeUiLabPlatform();
            FakeUiLabPlatform platformB = new FakeUiLabPlatform();

            using (UiLabHostSession sessionA = NewSession(platformA, sharedMovieId))
            using (UiLabHostSession sessionB = NewSession(platformB, sharedMovieId))
            {
                Check(sessionA.RequestOpen(UiLabRequestSource.Local).Code == "opened", "A 打开失败");

                UiLabRequestOutcome conflict = sessionB.RequestOpen(UiLabRequestSource.Local);
                Check(conflict.Code == "owner_conflict", "B 应被拒 owner_conflict，实际 " + conflict.Code);
                Check(platformB.LayersCreated == 0, "被拒 owner 不得创建 layer");
                Check(platformB.MoviesLoaded == 0, "被拒 owner 不得加载 movie");
                Check(platformB.IsClean, "被拒 owner 不得留下任何持有");
                Check(sessionB.Generation == 0, "被拒 owner 不得推进 generation");

                Check(sessionA.RequestClose(UiLabRequestSource.Local).Code == "closed", "A 关闭失败");
                Check(sessionB.RequestOpen(UiLabRequestSource.Local).Code == "opened", "owner 释放后 B 应可打开");
                Check(sessionB.RequestClose(UiLabRequestSource.Local).Code == "closed", "B 关闭失败");
                AssertClean(platformB, sessionB, "dual_owner_conflict(B)");
            }
        }

        internal static void ReservedProductionMovieRejected()
        {
            FakeUiLabPlatform platform = new FakeUiLabPlatform();
            bool threw = false;
            try
            {
                new UiLabHostSession(platform, UiLabHostIdentity.ReservedProductionMovieId, null, 0, null, null);
            }
            catch (ArgumentException)
            {
                threw = true;
            }
            Check(threw, "使用生产保留 movie 名构造应被拒绝");
            Check(!UiLabHostIdentity.IsReservedProductionMovie(UiLabHostIdentity.MovieId), "自用 movie 名不得是保留名");
            Check(UiLabHostIdentity.MovieId != UiLabHostIdentity.ReservedProductionMovieId, "自用 movie 名不得等于生产名");
        }

        internal static void FixtureDecisionStateIsInternalOnly()
        {
            UiLabFixtureView confirmation = UiLabFixtureCatalog.Create(UiLabFixtureStateIds.DialogueCommandConfirmation);
            Check(confirmation.Decision == UiLabDecisionState.Pending, "二次确认夹具初始应为 pending");
            int revisionBefore = confirmation.Revision;

            Check(confirmation.ExecuteConfirm(), "确认应成功");
            Check(confirmation.Decision == UiLabDecisionState.Confirmed, "确认后夹具状态应为 confirmed");
            Check(confirmation.Revision > revisionBefore, "确认应产生可观察的状态变化");
            Check(!confirmation.ExecuteConfirm(), "重复确认应无副作用");

            UiLabFixtureView rejected = UiLabFixtureCatalog.Create(UiLabFixtureStateIds.DialogueCommandRejected);
            Check(rejected.Decision == UiLabDecisionState.Rejected, "拒绝夹具状态应为 rejected");

            UiLabFixtureView confirmThenReject = UiLabFixtureCatalog.Create(UiLabFixtureStateIds.DialogueCommandConfirmation);
            Check(confirmThenReject.ExecuteReject(), "拒绝应成功");
            Check(confirmThenReject.Decision == UiLabDecisionState.Rejected, "拒绝后夹具状态应为 rejected");

            // 每个固定状态都要造得出，且关键状态有可断言的形状
            for (int i = 0; i < UiLabFixtureStateIds.All.Length; i++)
            {
                UiLabFixtureView view = UiLabFixtureCatalog.Create(UiLabFixtureStateIds.All[i]);
                Check(view != null, "状态 " + UiLabFixtureStateIds.All[i] + " 造不出夹具");
                Check(view.StateId == UiLabFixtureStateIds.All[i], "夹具状态 ID 应回填一致");
            }

            UiLabFixtureView empty = UiLabFixtureCatalog.Create(UiLabFixtureStateIds.PanelEmpty);
            Check(empty.StateRows.Count == 0, "空状态夹具不应有列表行");
            Check(empty.IsListVisible, "空状态仍应显示列表容器");

            UiLabFixtureView longText = UiLabFixtureCatalog.Create(UiLabFixtureStateIds.PanelLongText);
            Check(longText.IsDetailVisible, "长文本夹具应展示详情");
            Check(longText.SelectedStateDescription.Length > 500, "长文本夹具详情应足够长");

            UiLabFixtureView disabled = UiLabFixtureCatalog.Create(UiLabFixtureStateIds.PanelDisabled);
            UiLabFixtureRow disabledRow = null;
            for (int i = 0; i < disabled.StateRows.Count; i++)
            {
                if (disabled.StateRows[i].IsDisabled) disabledRow = disabled.StateRows[i];
            }
            Check(disabledRow != null, "禁用夹具应含一个不可用行");
            Check(!disabled.ExecuteSelect(disabledRow), "选中不可用行应失败");
        }

        internal static void FixtureStateIdsMatchJson(string labRoot)
        {
            string path = Path.Combine(labRoot, "fixtures", "fixture-state-ids.v1.json");
            Check(File.Exists(path), "缺少 fixtures/fixture-state-ids.v1.json");

            List<string> fromJson = new List<string>();
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                JsonElement states;
                Check(document.RootElement.TryGetProperty("states", out states), "JSON 缺少 states");
                foreach (JsonElement element in states.EnumerateArray())
                {
                    fromJson.Add(element.GetString());
                }
            }

            Check(fromJson.Count == UiLabFixtureStateIds.All.Length,
                "状态数量不一致：json=" + fromJson.Count.ToString() + " code=" + UiLabFixtureStateIds.All.Length.ToString());

            for (int i = 0; i < fromJson.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < UiLabFixtureStateIds.All.Length; j++)
                {
                    if (UiLabFixtureStateIds.All[j] == fromJson[i]) found = true;
                }
                Check(found, "JSON 状态未登记到代码：" + fromJson[i]);
            }

            for (int i = 0; i < UiLabFixtureStateIds.All.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < fromJson.Count; j++)
                {
                    if (fromJson[j] == UiLabFixtureStateIds.All[i]) found = true;
                }
                Check(found, "代码状态未登记到 JSON：" + UiLabFixtureStateIds.All[i]);
            }

            Check(UiLabFixtureCatalog.Create(UiLabFixtureStateIds.DialogueCommandConfirmation).Decision
                == UiLabDecisionState.Pending, "二次确认夹具应对得上 JSON 状态 ID");
        }

        internal static void PrefabBindingParity(string labRoot)
        {
            string path = Path.Combine(labRoot, "assets", "GauntletUILab.baseline.xml");
            Check(File.Exists(path), "缺少 assets/GauntletUILab.baseline.xml");

            string xml = File.ReadAllText(path);
            UiLabPrefabContract contract = UiLabPrefabAudit.Audit(xml);

            IReadOnlyList<string> dangling = UiLabPrefabAudit.FindDanglingPrefabMembers(contract);
            Check(dangling.Count == 0, "Prefab 存在夹具 DTO 覆盖不到的绑定/命令：" + string.Join(",", dangling));
            Check(contract.Bindings.Count > 0, "应从 Prefab 抽到绑定");
            Check(contract.Commands.Count > 0, "应从 Prefab 抽到命令");
            Check(UiLabPrefabAudit.SupportsDecisionCommands(), "夹具 DTO 应提供确认/拒绝命令");

            XDocument document = XDocument.Parse(xml);
            Check(document.Descendants().Any(e => e.Name.LocalName.EndsWith("Widget") && (string)e.Attribute("Id") == "DetailPanel"),
                "Prefab 缺少 DetailPanel");
            XElement detailClip = null;
            XElement backButton = null;
            foreach (XElement element in document.Descendants())
            {
                if ((string)element.Attribute("Id") == "DetailClip") detailClip = element;
                if ((string)element.Attribute("Id") == "BackButton") backButton = element;
            }
            Check(detailClip != null, "Prefab 缺少 DetailClip");
            Check(backButton != null, "Prefab 缺少 BackButton");
            Check(!detailClip.Descendants().Any(e => (string)e.Attribute("Id") == "BackButton"),
                "返回按钮不得落在详情滚动区内");
        }

        internal static void FixtureRunnerBuildsAllKnownStates()
        {
            UiLabFixtureReport report = UiLabFixtureRunner.RunAll();
            Check(report.Total == UiLabFixtureStateIds.All.Length,
                "报告总数应等于登记状态数：" + report.Total.ToString() + " vs " + UiLabFixtureStateIds.All.Length.ToString());
            Check(report.Failed == 0,
                "存在造不出非空壳的夹具：" + string.Join(",",
                    report.Entries.Where(e => !e.Built).Select(e => e.StateId + "(" + e.Error + ")")));
            for (int i = 0; i < report.Entries.Count; i++)
            {
                Check(report.Entries[i].SupportsConfirmReject || !IsDialogueFamily(report.Entries[i].StateId),
                    "对话族夹具应支持确认/拒绝：" + report.Entries[i].StateId);
            }
        }

        internal static void FixtureDecisionLifecycleOnlyInternal()
        {
            string[] family = UiLabFixtureStateIds.DialogueCommandFamily;
            for (int i = 0; i < family.Length; i++)
            {
                Check(UiLabFixtureRunner.SimulateDecisionLifecycle(family[i]),
                    "确认/拒绝生命周期异常：" + family[i]);
            }
            UiLabFixtureView direct = UiLabFixtureCatalog.Create(UiLabFixtureStateIds.DialogueCommandConfirmation);
            direct.ExecuteConfirm();
            Check(direct.Decision == UiLabDecisionState.Confirmed, "确认后应为 Confirmed");
            Check(!direct.CloseRequested, "确认不得触发 Close");
            Check(direct.IsListVisible && !direct.IsDetailVisible, "确认后应收起详情、回到列表");
        }

        internal static void LongTextFixtureHasContent()
        {
            Check(UiLabFixtureRunner.LongTextExceeds(UiLabFixtureStateIds.PanelLongText, 500),
                "长文本夹具详情不足 500 字");
        }

        private static bool IsDialogueFamily(string stateId)
        {
            string[] family = UiLabFixtureStateIds.DialogueCommandFamily;
            for (int i = 0; i < family.Length; i++)
            {
                if (family[i] == stateId) return true;
            }
            return false;
        }
    }
}
