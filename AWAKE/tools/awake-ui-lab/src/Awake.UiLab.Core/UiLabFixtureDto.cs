using System;
using System.Collections.Generic;

namespace Awake.UiLab
{
    /// <summary>夹具内部的确认/拒绝状态。确认/拒绝只改这里，不写任何游戏数据。</summary>
    public enum UiLabDecisionState
    {
        Pending = 0,
        Confirmed = 1,
        Rejected = 2
    }

    /// <summary>列表行。对应基线 Prefab 的 ItemTemplate 绑定。</summary>
    public sealed class UiLabFixtureRow
    {
        public string RowId { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public bool IsSelected { get; internal set; }
        public bool IsDisabled { get; private set; }

        public UiLabFixtureRow(string rowId, string title, string description, bool isDisabled)
        {
            RowId = rowId ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            IsDisabled = isDisabled;
        }
    }

    /// <summary>
    /// 夹具只读视图模型。这是本地端与以后游戏内 F10 壳共用的 DTO：
    /// 它只承载假数据与显示状态，不持有服务、不访问 AI / 世界书 / 存储 / 战役状态。
    /// 属性名与 assets/GauntletUILab.baseline.xml 的绑定逐一对齐。
    /// </summary>
    public sealed class UiLabFixtureView
    {
        /// <summary>基线 Prefab 声明的绑定名（含集合绑定）。</summary>
        public static readonly string[] BindingNames = new string[]
        {
            "StatusText",
            "IsListVisible",
            "StateRows",
            "IsSelected",
            "Title",
            "Description",
            "IsDetailVisible",
            "SelectedStateTitle",
            "SelectedStateDescription"
        };

        /// <summary>夹具视图暴露的命令名。</summary>
        public static readonly string[] CommandNames = new string[]
        {
            "ExecuteClose",
            "ExecuteSelect",
            "ExecuteBack",
            "ExecuteConfirm",
            "ExecuteReject"
        };

        private readonly List<UiLabFixtureRow> _rows = new List<UiLabFixtureRow>();

        public string StateId { get; private set; }
        public string StatusText { get; private set; }
        public bool IsListVisible { get; private set; }
        public bool IsDetailVisible { get; private set; }
        public string SelectedStateTitle { get; private set; }
        public string SelectedStateDescription { get; private set; }
        public UiLabDecisionState Decision { get; private set; }

        /// <summary>每次可观察的状态变化都会 +1，便于断言"确实变了"而不是"没动"。</summary>
        public int Revision { get; private set; }

        /// <summary>ExecuteClose 被触发过（夹具内部记录，不代表宿主动作）。</summary>
        public bool CloseRequested { get; private set; }

        public IReadOnlyList<UiLabFixtureRow> StateRows
        {
            get { return _rows; }
        }

        public UiLabFixtureView(string stateId)
        {
            StateId = stateId ?? string.Empty;
            StatusText = string.Empty;
            SelectedStateTitle = string.Empty;
            SelectedStateDescription = string.Empty;
            IsListVisible = true;
            Decision = UiLabDecisionState.Pending;
        }

        internal UiLabFixtureRow AddRow(string rowId, string title, string description, bool isDisabled)
        {
            UiLabFixtureRow row = new UiLabFixtureRow(rowId, title, description, isDisabled);
            _rows.Add(row);
            return row;
        }

        internal void SetStatus(string statusText)
        {
            StatusText = statusText ?? string.Empty;
            Revision++;
        }

        internal void SetDetail(string title, string description, bool visible)
        {
            SelectedStateTitle = title ?? string.Empty;
            SelectedStateDescription = description ?? string.Empty;
            IsDetailVisible = visible;
            Revision++;
        }

        internal void SetListVisible(bool visible)
        {
            IsListVisible = visible;
            Revision++;
        }

        internal void SetDecision(UiLabDecisionState decision)
        {
            Decision = decision;
            Revision++;
        }

        /// <summary>选中某一行（只改夹具内部选中态）。</summary>
        public bool ExecuteSelect(UiLabFixtureRow row)
        {
            if (row == null || row.IsDisabled) return false;
            if (!_rows.Contains(row)) return false;
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].IsSelected = ReferenceEquals(_rows[i], row);
            }
            Revision++;
            return true;
        }

        /// <summary>返回列表：详情收起，列表重新可见。只改夹具状态。</summary>
        public bool ExecuteBack()
        {
            if (!IsDetailVisible) return false;
            IsDetailVisible = false;
            IsListVisible = true;
            Revision++;
            return true;
        }

        /// <summary>夹具内部"关闭"意图。宿主如何响应由 UiLabHostSession 决定。</summary>
        public bool ExecuteClose()
        {
            CloseRequested = true;
            Revision++;
            return true;
        }

        /// <summary>确认指令提案：只把夹具状态置为 Confirmed。</summary>
        public bool ExecuteConfirm()
        {
            if (Decision == UiLabDecisionState.Confirmed) return false;
            SetDecision(UiLabDecisionState.Confirmed);
            IsDetailVisible = false;
            IsListVisible = true;
            return true;
        }

        /// <summary>拒绝指令提案：只把夹具状态置为 Rejected。</summary>
        public bool ExecuteReject()
        {
            if (Decision == UiLabDecisionState.Rejected) return false;
            SetDecision(UiLabDecisionState.Rejected);
            IsDetailVisible = false;
            IsListVisible = true;
            return true;
        }
    }
}
