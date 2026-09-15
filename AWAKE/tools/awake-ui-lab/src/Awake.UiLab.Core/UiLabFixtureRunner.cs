using System;
using System.Collections.Generic;

namespace Awake.UiLab
{
    /// <summary>单个夹具状态的本地验收结果。</summary>
    public sealed class UiLabFixtureReportEntry
    {
        public string StateId;
        public bool Built;
        public int RowCount;
        public string Decision;
        public int DetailLength;
        public bool HasStatus;
        public bool SupportsConfirmReject;
        public string Error;
    }

    /// <summary>本地全量 fixture 验收报告。</summary>
    public sealed class UiLabFixtureReport
    {
        public int Total;
        public int Built;
        public int Failed;
        public readonly List<UiLabFixtureReportEntry> Entries = new List<UiLabFixtureReportEntry>();
    }

    /// <summary>
    /// 本地全量 fixture 驱动器：遍历所有已知状态 ID，逐个造夹具并做基本本地断言。
    /// 纯离线、不启动游戏、不渲染 Gauntlet、不碰任何服务/存储/战役状态。
    /// 这套逻辑本地端与以后游戏内 F10 壳共用（同一份代码，不复制业务逻辑）。
    /// </summary>
    public static class UiLabFixtureRunner
    {
        /// <summary>
        /// 遍历所有已知状态 ID，逐个造夹具并判定"非空壳"。
        /// 非空壳＝至少有一行、或详情可见、或状态文本非空。
        /// </summary>
        public static UiLabFixtureReport RunAll()
        {
            UiLabFixtureReport report = new UiLabFixtureReport();
            string[] ids = UiLabFixtureStateIds.All;
            report.Total = ids.Length;

            for (int i = 0; i < ids.Length; i++)
            {
                UiLabFixtureReportEntry entry = new UiLabFixtureReportEntry();
                entry.StateId = ids[i];
                try
                {
                    UiLabFixtureView view = UiLabFixtureCatalog.Create(ids[i]);
                    bool nonEmpty = view != null
                        && (view.StateRows.Count > 0
                            || view.IsDetailVisible
                            || !string.IsNullOrEmpty(view.StatusText));
                    entry.Built = nonEmpty;
                    entry.RowCount = view.StateRows.Count;
                    entry.Decision = view.Decision.ToString();
                    entry.DetailLength = view.SelectedStateDescription == null
                        ? 0 : view.SelectedStateDescription.Length;
                    entry.HasStatus = !string.IsNullOrEmpty(view.StatusText);
                    entry.SupportsConfirmReject = UiLabPrefabAudit.SupportsDecisionCommands();
                    if (!nonEmpty) entry.Error = "empty_fixture";
                }
                catch (Exception ex)
                {
                    entry.Built = false;
                    entry.Error = ex.GetType().Name + ": " + ex.Message;
                }

                if (entry.Built) report.Built++;
                else report.Failed++;
                report.Entries.Add(entry);
            }

            return report;
        }

        /// <summary>
        /// 确认/拒绝交互模拟：构造 → 确认（Decision 变 Confirmed）→ 拒绝（变 Rejected）。
        /// 全程只改夹具内部 Decision；不触发 Close、不碰任何游戏数据。
        /// 返回 true 表示状态机按预期推进且无意外部作用。
        /// </summary>
        public static bool SimulateDecisionLifecycle(string stateId)
        {
            if (!UiLabFixtureStateIds.IsKnown(stateId)) return false;

            UiLabFixtureView view = UiLabFixtureCatalog.Create(stateId);
            bool confirmOk = view.ExecuteConfirm();
            bool isConfirmed = view.Decision == UiLabDecisionState.Confirmed;
            bool rejectOk = view.ExecuteReject();
            bool isRejected = view.Decision == UiLabDecisionState.Rejected;
            bool noCloseSideEffect = !view.CloseRequested;
            return confirmOk && isConfirmed && rejectOk && isRejected && noCloseSideEffect;
        }

        /// <summary>长文本夹具的本地边界检查：详情文本须达到给定字符数。</summary>
        public static bool LongTextExceeds(string stateId, int minChars)
        {
            if (!UiLabFixtureStateIds.IsKnown(stateId)) return false;

            UiLabFixtureView view = UiLabFixtureCatalog.Create(stateId);
            return view.IsDetailVisible
                && view.SelectedStateDescription != null
                && view.SelectedStateDescription.Length >= minChars;
        }
    }
}
