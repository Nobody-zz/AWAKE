using TaleWorlds.Library;

namespace Awake;

internal sealed class WorldEventRowVM : ViewModel
{
    [DataSourceProperty]
    public string DayText { get; }

    [DataSourceProperty]
    public string Kind { get; }

    [DataSourceProperty]
    public string Text { get; }

    internal WorldEventRowVM(WorldEventRecord record)
    {
        DayText = "第 " + (record?.Day ?? 0) + " 天";
        Kind = record?.Kind ?? "event";
        Text = record?.Text ?? string.Empty;
    }
}
