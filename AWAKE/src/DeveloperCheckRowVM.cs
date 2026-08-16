using TaleWorlds.Library;

namespace Awake;

internal sealed class DeveloperCheckRowVM : ViewModel
{
    [DataSourceProperty]
    public string Name { get; }

    [DataSourceProperty]
    public string Value { get; }

    internal DeveloperCheckRowVM(string name, string value)
    {
        Name = name ?? string.Empty;
        Value = value ?? string.Empty;
    }
}
