using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace Awake;

internal sealed class DeveloperCheckVM : ViewModel
{
    private readonly Action _close;
    private readonly MBBindingList<DeveloperCheckRowVM> _rows = new MBBindingList<DeveloperCheckRowVM>();
    private string _titleText;
    private string _statusText;

    [DataSourceProperty]
    public MBBindingList<DeveloperCheckRowVM> Rows => _rows;

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

    internal DeveloperCheckVM(Action close, IReadOnlyList<KeyValuePair<string, string>> rows)
    {
        _close = close;
        TitleText = AwakeLocalization.Resolve("awake.dev_check.title", "醒世 · 开发者检查");
        StatusText = AwakeLocalization.Resolve("awake.dev_check.status", "运行时诊断");
        if (rows != null)
        {
            foreach (KeyValuePair<string, string> row in rows)
            {
                _rows.Add(new DeveloperCheckRowVM(row.Key, row.Value));
            }
        }
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
