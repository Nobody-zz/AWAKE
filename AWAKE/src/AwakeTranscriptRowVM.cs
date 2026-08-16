using System;
using TaleWorlds.Library;

namespace Awake;

internal sealed class AwakeTranscriptRowVM : ViewModel
{
    private readonly AwakeTranscriptLine _line;
    private readonly Action<AwakeTranscriptRowVM> _togglePin;
    private bool _isPinned;

    internal AwakeTranscriptLine Line => _line;

    [DataSourceProperty]
    public string Speaker => _line.Speaker;

    [DataSourceProperty]
    public string Text => _line.Text;

    [DataSourceProperty]
    public string DayText => AwakeLocalization.Resolve(
        "awake.ui.day",
        "第 " + _line.Day + " 天",
        new System.Collections.Generic.Dictionary<string, string> { ["DAY"] = _line.Day.ToString() });

    [DataSourceProperty]
    public bool IsPinned
    {
        get => _isPinned;
        private set
        {
            if (Set(ref _isPinned, value, nameof(IsPinned)))
            {
                OnPropertyChangedWithValue(PinButtonText, nameof(PinButtonText));
            }
        }
    }

    [DataSourceProperty]
    public string PinButtonText => AwakeLocalization.Resolve(
        _isPinned ? "awake.ui.history_unpin" : "awake.ui.history_pin",
        _isPinned ? "取消固定" : "固定");

    internal AwakeTranscriptRowVM(AwakeTranscriptLine line, Action<AwakeTranscriptRowVM> togglePin)
    {
        _line = line ?? new AwakeTranscriptLine(string.Empty, 0, string.Empty, string.Empty, string.Empty, "system", string.Empty, "system");
        _togglePin = togglePin;
        _isPinned = _line.IsPinned;
    }

    public void ExecutePin()
    {
        try
        {
            _togglePin?.Invoke(this);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_transcript_pin_error error=" + ex.Message);
        }
    }

    internal void SetPinned(bool pinned)
    {
        _line.IsPinned = pinned;
        IsPinned = pinned;
    }

    private bool Set(ref bool field, bool value, string name)
    {
        if (field == value) return false;
        field = value;
        OnPropertyChangedWithValue(value, name);
        return true;
    }
}
