using System;
using System.Text;

namespace Awake.UiLab
{
    /// <summary>
    /// 夹具构造器：按固定状态 ID 造出一份只读假数据视图。
    /// 只使用静态文本；不接触任何服务、存储或战役状态。
    /// </summary>
    public static class UiLabFixtureCatalog
    {
        private const string ProposalRowId = "proposal.relationship-delta";
        private const string ProposalTitle = "提案：与对方拉近关系";
        private const string ProposalSummary = "关系 +5；不涉及金钱、物品与命令执行。";

        /// <summary>按状态 ID 造夹具；未知 ID 抛异常，避免静默造出空壳。</summary>
        public static UiLabFixtureView Create(string stateId)
        {
            if (!UiLabFixtureStateIds.IsKnown(stateId))
            {
                throw new ArgumentOutOfRangeException(
                    "stateId", stateId, "未知的夹具状态 ID；请先登记到 fixtures/fixture-state-ids.v1.json");
            }

            if (string.Equals(stateId, UiLabFixtureStateIds.DialogueCommandConfirmation, StringComparison.Ordinal))
            {
                return CreateCommandProposal(stateId);
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.DialogueCommandRejected, StringComparison.Ordinal))
            {
                UiLabFixtureView rejected = CreateCommandProposal(stateId);
                rejected.ExecuteReject();
                rejected.SetStatus("已拒绝该指令提案（仅夹具状态，未写入任何游戏数据）。");
                return rejected;
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.PanelEmpty, StringComparison.Ordinal))
            {
                UiLabFixtureView empty = new UiLabFixtureView(stateId);
                empty.SetStatus("当前没有可显示的条目（空状态夹具）。");
                empty.SetListVisible(true);
                empty.SetDetail("", "", false);
                return empty;
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.PanelLongText, StringComparison.Ordinal))
            {
                UiLabFixtureView longText = CreateListFixture(stateId, 4, 0);
                longText.SetDetail("长文本验收（夹具）", BuildLongText(24), true);
                longText.SetStatus("长文本夹具：详情文本约 " + BuildLongText(24).Length.ToString() + " 字。");
                return longText;
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.PanelDisabled, StringComparison.Ordinal))
            {
                UiLabFixtureView disabled = CreateListFixture(stateId, 3, 0);
                disabled.AddRow("row.disabled", "不可用条目", "该条目在夹具里被标记为不可用，不可选中。", true);
                disabled.SetStatus("含一个不可用条目；选中不可用条目应失败。");
                return disabled;
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.PanelRelationZero, StringComparison.Ordinal)
                || string.Equals(stateId, UiLabFixtureStateIds.PanelRelationHalf, StringComparison.Ordinal)
                || string.Equals(stateId, UiLabFixtureStateIds.PanelRelationFull, StringComparison.Ordinal))
            {
                string value = stateId == UiLabFixtureStateIds.PanelRelationZero ? "0"
                    : stateId == UiLabFixtureStateIds.PanelRelationHalf ? "50" : "100";
                UiLabFixtureView relation = CreateListFixture(stateId, 3, 0);
                relation.AddRow("row.relation", "关系值（夹具）", "当前关系数值：" + value, false);
                relation.SetStatus("关系档位夹具：" + value + "。");
                return relation;
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.ListFirst, StringComparison.Ordinal))
            {
                return CreateListFixture(stateId, 4, 0);
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.ListLast, StringComparison.Ordinal))
            {
                return CreateListFixture(stateId, 4, 3);
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.ListLowItem, StringComparison.Ordinal))
            {
                UiLabFixtureView low = CreateListFixture(stateId, 3, 0);
                low.AddRow("row.low", "数量不足条目", "剩余数量 0（夹具），不足以执行。", false);
                low.SetStatus("含一个低量条目夹具。");
                return low;
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.ListGroupCollapsed, StringComparison.Ordinal)
                || string.Equals(stateId, UiLabFixtureStateIds.ListGroupExpanded, StringComparison.Ordinal))
            {
                bool expanded = stateId == UiLabFixtureStateIds.ListGroupExpanded;
                UiLabFixtureView group = CreateListFixture(stateId, 2, 0);
                group.AddRow("group.a", expanded ? "分组甲（已展开）" : "分组甲（已折叠）",
                    expanded ? "组内 2 项" : "组内 2 项（收起时不计入可见行）", false);
                group.SetStatus(expanded ? "分组展开夹具。" : "分组折叠夹具。");
                return group;
            }
            if (string.Equals(stateId, UiLabFixtureStateIds.DetailOpen, StringComparison.Ordinal)
                || string.Equals(stateId, UiLabFixtureStateIds.DetailReturn, StringComparison.Ordinal))
            {
                UiLabFixtureView detail = CreateListFixture(stateId, 3, 0);
                detail.SetDetail("详情夹具标题", "详情夹具正文：这里只展示假数据，用来验收返回按钮与滚动区层级。", true);
                if (string.Equals(stateId, UiLabFixtureStateIds.DetailReturn, StringComparison.Ordinal))
                {
                    detail.SetStatus("已从详情返回列表（ExecuteBack 夹具状态）。");
                }
                return detail;
            }

            // panel.normal / panel.selected 及其余未单独分支的通用面板状态。
            return CreateListFixture(stateId, 3, 0);
        }

        private static UiLabFixtureView CreateListFixture(string stateId, int rowCount, int selectedIndex)
        {
            UiLabFixtureView view = new UiLabFixtureView(stateId);
            for (int i = 0; i < rowCount; i++)
            {
                string title = "条目 " + (i + 1).ToString();
                string description = "夹具描述 " + (i + 1).ToString() + "（只读假数据）";
                UiLabFixtureRow row = view.AddRow("row." + (i + 1).ToString(), title, description, false);
                if (i == selectedIndex)
                {
                    view.ExecuteSelect(row);
                }
            }
            if (rowCount == 0)
            {
                view.SetStatus("空列表夹具。");
            }
            else if (view.StateRows.Count > 0 && !HasSelection(view))
            {
                view.ExecuteSelect(view.StateRows[0]);
            }
            return view;
        }

        private static bool HasSelection(UiLabFixtureView view)
        {
            for (int i = 0; i < view.StateRows.Count; i++)
            {
                if (view.StateRows[i].IsSelected) return true;
            }
            return false;
        }

        private static UiLabFixtureView CreateCommandProposal(string stateId)
        {
            UiLabFixtureView view = new UiLabFixtureView(stateId);
            view.AddRow(ProposalRowId, ProposalTitle, ProposalSummary, false);
            view.SetStatus("对方提出了一个指令提案，请确认或拒绝。此夹具不执行任何指令。");
            view.SetDetail(
                ProposalTitle,
                ProposalSummary + "\r\n"
                    + "确认后：夹具状态变为 confirmed（不会改变游戏内任何数值）。\r\n"
                    + "拒绝后：夹具状态变为 rejected（同样不写入游戏数据）。",
                true);
            return view;
        }

        private static string BuildLongText(int repeatCount)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < repeatCount; i++)
            {
                builder.Append("（");
                builder.Append((i + 1).ToString());
                builder.Append("）长文本夹具：这段文字只用于检查滚动区与换行，不代表任何游戏内容。");
            }
            return builder.ToString();
        }
    }
}
