using System;
using System.Collections.Generic;

namespace Awake.UiLab
{
    /// <summary>
    /// 固定夹具状态 ID 的规范集合。
    /// 这份集合必须与 fixtures/fixture-state-ids.v1.json 完全一致；
    /// E2 用例 fixture_state_ids_match_json 会逐项比对，防止两边漂移。
    /// </summary>
    public static class UiLabFixtureStateIds
    {
        public const string PanelNormal = "panel.normal";
        public const string PanelSelected = "panel.selected";
        public const string PanelDisabled = "panel.disabled";
        public const string PanelEmpty = "panel.empty";
        public const string PanelLongText = "panel.long-text";
        public const string PanelRelationZero = "panel.relation-zero";
        public const string PanelRelationHalf = "panel.relation-half";
        public const string PanelRelationFull = "panel.relation-full";
        public const string ListFirst = "list.first";
        public const string ListLast = "list.last";
        public const string ListLowItem = "list.low-item";
        public const string ListGroupCollapsed = "list.group-collapsed";
        public const string ListGroupExpanded = "list.group-expanded";
        public const string DetailOpen = "detail.open";
        public const string DetailReturn = "detail.return";

        /// <summary>对话指令二次确认（首个稳定夹具）。</summary>
        public const string DialogueCommandConfirmation = "dialogue.command-confirmation";

        /// <summary>对话指令被拒绝后的可观察状态。</summary>
        public const string DialogueCommandRejected = "dialogue.command-rejected";

        /// <summary>规范全集。顺序与 JSON 一致。</summary>
        public static readonly string[] All = new string[]
        {
            PanelNormal,
            PanelSelected,
            PanelDisabled,
            PanelEmpty,
            PanelLongText,
            PanelRelationZero,
            PanelRelationHalf,
            PanelRelationFull,
            ListFirst,
            ListLast,
            ListLowItem,
            ListGroupCollapsed,
            ListGroupExpanded,
            DetailOpen,
            DetailReturn,
            DialogueCommandConfirmation,
            DialogueCommandRejected
        };

        /// <summary>「对话指令二次确认」一族的状态 ID。</summary>
        public static readonly string[] DialogueCommandFamily = new string[]
        {
            DialogueCommandConfirmation,
            DialogueCommandRejected
        };

        public static bool IsKnown(string stateId)
        {
            if (string.IsNullOrEmpty(stateId)) return false;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], stateId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static IReadOnlyList<string> AllIds()
        {
            return All;
        }
    }
}
