using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Awake;

internal sealed class WeeklyReportBrowserOverlay
{
    private static WeeklyReportBrowserOverlay _active;

    internal static bool IsOpen => _active != null && !_active._closed;

    internal static bool Open(string report)
    {
        try
        {
            CloseActive();
            ScreenBase screen = ScreenManager.TopScreen;
            if (screen == null)
            {
                AwakeLog.Write("weekly_report_open_failed reason=no_top_screen");
                return false;
            }
            WeeklyReportBrowserOverlay overlay = new WeeklyReportBrowserOverlay(screen, report);
            overlay.OpenLayer();
            if (!ReferenceEquals(ScreenManager.FocusedLayer, overlay._layer))
            {
                AwakeLog.Write("weekly_report_open_failed reason=no_focus");
                overlay.Close();
                return false;
            }
            _active = overlay;
            AwakeLog.Write("weekly_report_panel_opened");
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("weekly_report_open_error error=" + ex.Message);
            return false;
        }
    }

    internal static void OnApplicationTick()
    {
        try
        {
            WeeklyReportBrowserOverlay active = _active;
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
            AwakeLog.Write("weekly_report_tick_failed error=" + ex.Message);
        }
    }

    internal static void CloseActive()
    {
        _active?.Close();
    }

    private readonly ScreenBase _screen;
    private readonly GauntletLayer _layer;
    private readonly WeeklyReportBrowserVM _dataSource;
    private object _movie;
    private bool _closed;

    private WeeklyReportBrowserOverlay(ScreenBase screen, string report)
    {
        _screen = screen;
        _dataSource = new WeeklyReportBrowserVM(Close, report);
        _layer = new GauntletLayer("WeeklyReportBrowser", 544, false);
    }

    private void OpenLayer()
    {
        _movie = _layer.LoadMovie("WeeklyReportBrowser", _dataSource);
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
        AwakeLog.Write("weekly_report_panel_closed");
    }
}
