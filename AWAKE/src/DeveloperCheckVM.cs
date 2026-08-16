using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace Awake;

internal sealed class DeveloperCheckVM : ViewModel
{
    private readonly Action _close;
    private readonly Action _refresh;
    private readonly Action _openAiSetup;
    private readonly Action _openDiagnostics;
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

    [DataSourceProperty]
    public string RefreshText => AwakeLocalization.Resolve("awake.dev_check.refresh", "Refresh");

    [DataSourceProperty]
    public string AiSetupText => AwakeLocalization.Resolve("awake.dev_check.ai_setup", "AI Setup");

    [DataSourceProperty]
    public string DiagnosticsText => AwakeLocalization.Resolve("awake.dev_check.diagnostics", "Diagnostics");

    internal DeveloperCheckVM(
        Action close,
        Action refresh,
        Action openAiSetup,
        Action openDiagnostics,
        IReadOnlyList<KeyValuePair<string, string>> rows)
    {
        _close = close;
        _refresh = refresh;
        _openAiSetup = openAiSetup;
        _openDiagnostics = openDiagnostics;
        TitleText = AwakeLocalization.Resolve("awake.dev_check.title", "AWAKE Developer Check");
        StatusText = AwakeLocalization.Resolve("awake.dev_check.status", "Runtime diagnostics");
        Reload(rows);
    }

    internal void Reload(IReadOnlyList<KeyValuePair<string, string>> rows)
    {
        _rows.Clear();
        if (rows != null)
        {
            foreach (KeyValuePair<string, string> row in rows)
            {
                _rows.Add(new DeveloperCheckRowVM(row.Key, row.Value));
            }
        }
        StatusText = AwakeLocalization.Resolve("awake.dev_check.status_refreshed", "Runtime diagnostics refreshed");
    }

    public void ExecuteClose()
    {
        _close?.Invoke();
    }

    public void ExecuteRefresh()
    {
        _refresh?.Invoke();
    }

    public void ExecuteOpenAiSetup()
    {
        _openAiSetup?.Invoke();
    }

    public void ExecuteOpenDiagnostics()
    {
        _openDiagnostics?.Invoke();
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
