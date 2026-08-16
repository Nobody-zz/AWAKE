using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace Awake;

internal sealed class WorldEventInboxVM : ViewModel
{
    private readonly Action _close;
    private readonly MBBindingList<WorldEventRowVM> _events = new MBBindingList<WorldEventRowVM>();
    private string _titleText = string.Empty;
    private string _statusText = string.Empty;

    [DataSourceProperty]
    public MBBindingList<WorldEventRowVM> Events => _events;

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

    internal WorldEventInboxVM(Action close, IReadOnlyList<WorldEventRecord> records)
    {
        _close = close;
        TitleText = AwakeLocalization.Resolve("awake.menu.inbox", "事件收件箱");
        StatusText = records == null || records.Count == 0
            ? AwakeLocalization.Resolve("awake.ui.inbox_empty", "本周没有记录。")
            : AwakeLocalization.Resolve(
                "awake.ui.inbox_count",
                records.Count + " 条本周记录",
                new Dictionary<string, string> { ["COUNT"] = records.Count.ToString() });
        if (records != null)
        {
            foreach (WorldEventRecord record in records)
            {
                _events.Add(new WorldEventRowVM(record));
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
