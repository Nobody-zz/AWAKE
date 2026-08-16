using System;
using TaleWorlds.Library;

namespace Awake;

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

    internal WeeklyReportBrowserVM(Action close, string report)
    {
        _close = close;
        TitleText = AwakeLocalization.Resolve("awake.menu.weekly_report", "世界周报");
        StatusText = AwakeLocalization.Resolve("awake.ui.weekly_status", "本周世界摘要");
        ReportText = string.IsNullOrWhiteSpace(report)
            ? AwakeLocalization.Resolve("awake.ui.weekly_empty", "本周没有记录。")
            : report;
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
