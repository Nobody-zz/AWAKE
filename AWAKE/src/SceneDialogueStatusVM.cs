using System;
using TaleWorlds.Library;

namespace Awake;

internal sealed class SceneDialogueStatusVM : ViewModel
{
    private string _text = string.Empty;

    [DataSourceProperty]
    public string Text
    {
        get => _text;
        private set => Set(ref _text, value, nameof(Text));
    }

    internal void UpdateText(string value)
    {
        Text = value ?? string.Empty;
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
