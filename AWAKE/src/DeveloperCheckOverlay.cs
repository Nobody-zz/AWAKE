using System;
using System.Collections.Generic;
using MarcusAIFramework.Api;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Awake;

internal sealed class DeveloperCheckOverlay
{
    private static DeveloperCheckOverlay _active;

    internal static bool IsOpen => _active != null && !_active._closed;

    internal static bool Open()
    {
        try
        {
            if (AwakeDialogueSessionCoordinator.IsActive)
            {
                AwakeLog.Write("developer_check_open_failed reason=dialogue_active");
                return false;
            }
            CloseActive();
            ScreenBase screen = ScreenManager.TopScreen;
            if (screen == null)
            {
                AwakeLog.Write("developer_check_open_failed reason=no_top_screen");
                return false;
            }
            IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
            IReadOnlyList<KeyValuePair<string, string>> rows = AwakeDeveloperReport.BuildRows(host, AwakeSettings.Current);
            DeveloperCheckOverlay overlay = new DeveloperCheckOverlay(screen, rows);
            overlay.OpenLayer();
            _active = overlay;
            AwakeLog.Write("developer_check_panel_opened rows=" + rows.Count);
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("developer_check_open_error error=" + ex.Message);
            return false;
        }
    }

    internal static void OnApplicationTick()
    {
        try
        {
            DeveloperCheckOverlay active = _active;
            if (active == null) return;
            if (active._closed || !ReferenceEquals(ScreenManager.TopScreen, active._screen))
            {
                CloseActive();
                return;
            }
            if (active._layer.Input.IsKeyPressed(InputKey.Escape))
            {
                active._dataSource.ExecuteClose();
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("developer_check_tick_failed error=" + ex.Message);
        }
    }

    internal static void CloseActive()
    {
        _active?.Close();
    }

    private readonly ScreenBase _screen;
    private readonly GauntletLayer _layer;
    private readonly DeveloperCheckVM _dataSource;
    private object _movie;
    private bool _closed;

    private DeveloperCheckOverlay(ScreenBase screen, IReadOnlyList<KeyValuePair<string, string>> rows)
    {
        _screen = screen;
        _dataSource = new DeveloperCheckVM(Close, Refresh, OpenAiSetup, OpenDiagnostics, rows);
        _layer = new GauntletLayer("DeveloperCheck", 545, false);
    }

    private void Refresh()
    {
        if (_closed) return;
        try
        {
            IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
            IReadOnlyList<KeyValuePair<string, string>> rows = AwakeDeveloperReport.BuildRows(host, AwakeSettings.Current);
            _dataSource.Reload(rows);
            AwakeLog.Write("developer_check_panel_refreshed rows=" + rows.Count);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("developer_check_refresh_error error=" + ex.Message);
        }
    }

    private static void OpenAiSetup()
    {
        AwakeMarcusLinkService.OpenAiSetup();
    }

    private static void OpenDiagnostics()
    {
        AwakeMarcusLinkService.OpenDiagnostics();
    }

    private void OpenLayer()
    {
        _movie = _layer.LoadMovie("DeveloperCheck", _dataSource);
        _screen.AddLayer(_layer);
        _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
        _layer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(_layer);
    }

    private void Close()
    {
        if (_closed) return;
        _closed = true;
        try
        {
            _layer.InputRestrictions.ResetInputRestrictions();
            _layer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(_layer);
            _screen.RemoveLayer(_layer);
        }
        catch
        {
        }
        if (ReferenceEquals(_active, this)) _active = null;
        AwakeLog.Write("developer_check_panel_closed");
    }
}
