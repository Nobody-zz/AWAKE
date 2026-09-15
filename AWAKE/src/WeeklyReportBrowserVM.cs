using System;
using TaleWorlds.Library;

namespace Awake;

internal enum WeeklyReportDisplayState
{
    Formal,
    Preview,
    Unavailable
}

internal sealed class WeeklyReportDisplay
{
    internal WeeklyReportDisplayState State { get; }
    internal string Body { get; }

    internal WeeklyReportDisplay(WeeklyReportDisplayState state, string body)
    {
        State = state;
        Body = body ?? string.Empty;
    }
}

internal sealed class WeeklyReportBrowserVM : ViewModel
{
    private readonly Action _close;
    private string _titleText = string.Empty;
    private string _statusText = string.Empty;
    private string _reportText = string.Empty;

    [DataSourceProperty]
    public string TitleText
    {
        get => _titleText;
        private set => Set(ref _titleText, value, nameof(TitleText));
    }

    [DataSourceProperty]
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value, nameof(StatusText));
    }

    [DataSourceProperty]
    public string ReportText
    {
        get => _reportText;
        private set => Set(ref _reportText, value, nameof(ReportText));
    }

    internal WeeklyReportBrowserVM(Action close, WeeklyReportDisplay display)
    {
        _close = close;
        WeeklyReportDisplayState state = display?.State ?? WeeklyReportDisplayState.Unavailable;
        if (state == WeeklyReportDisplayState.Formal)
        {
            TitleText = AwakeLocalization.Resolve("awake.menu.weekly_dynamics", "本周动态");
            StatusText = AwakeLocalization.Resolve("awake.ui.weekly_formal_status", "已保存的最近一周动态");
        }
        else if (state == WeeklyReportDisplayState.Preview)
        {
            TitleText = AwakeLocalization.Resolve("awake.menu.recent_dynamics", "近期动态");
            StatusText = AwakeLocalization.Resolve("awake.ui.weekly_preview_status", "当前尚未结算的近期动态");
        }
        else
        {
            TitleText = AwakeLocalization.Resolve("awake.menu.weekly_dynamics", "本周动态");
            StatusText = AwakeLocalization.Resolve("awake.ui.weekly_unavailable_status", "暂时无法读取动态");
        }
        ReportText = string.IsNullOrWhiteSpace(display?.Body)
            ? (state == WeeklyReportDisplayState.Unavailable
                ? AwakeLocalization.Resolve("awake.ui.weekly_unavailable", "暂时无法读取动态，请稍后再试。")
                : AwakeLocalization.Resolve("awake.ui.weekly_empty", "本周没有记录。"))
            : display.Body;
    }

    public void ExecuteClose()
    {
        _close?.Invoke();
    }

    private bool Set(ref string field, string value, string name)
    {
        value ??= string.Empty;
        if (string.Equals(field, value, StringComparison.Ordinal)) return false;
        field = value;
        OnPropertyChangedWithValue(value, name);
        return true;
    }
}
