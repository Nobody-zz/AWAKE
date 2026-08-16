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
        DayText = AwakeLocalization.Resolve(
            "awake.ui.day",
            "第 " + (record?.Day ?? 0) + " 天",
            new System.Collections.Generic.Dictionary<string, string> { ["DAY"] = (record?.Day ?? 0).ToString() });
        Kind = record?.Kind ?? "event";
        Text = record?.Text ?? string.Empty;
    }
}
