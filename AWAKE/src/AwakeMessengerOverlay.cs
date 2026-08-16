using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Awake;

internal sealed class AwakeMessengerOverlay
{
    private static AwakeMessengerOverlay _active;

    internal static bool IsOpen => _active != null && !_active._closed;

    internal static bool Open()
    {
        try
        {
            CloseActive();
            ScreenBase screen = ScreenManager.TopScreen;
            if (screen == null)
            {
                AwakeLog.Write("awake_messenger_open_failed reason=no_top_screen");
                return false;
            }
            AwakeMessengerOverlay overlay = new AwakeMessengerOverlay(screen);
            overlay.OpenLayer();
            if (!ReferenceEquals(ScreenManager.FocusedLayer, overlay._layer))
            {
                AwakeLog.Write("awake_messenger_focus_pending");
            }
            _active = overlay;
            AwakeLog.Write("awake_messenger_panel_opened");
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_open_error error=" + ex.Message);
            return false;
        }
    }

    internal static void OnApplicationTick()
    {
        try
        {
            AwakeMessengerOverlay active = _active;
            if (active == null) return;
            if (active._closed || !ReferenceEquals(ScreenManager.TopScreen, active._screen))
            {
                CloseActive();
                return;
            }
            if (active._layer.Input.IsKeyPressed(InputKey.Escape))
            {
                active._dataSource.ExecuteClose();
                return;
            }
            active._dataSource.OnFrameTick();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_tick_failed error=" + ex.Message);
        }
    }

    internal static void CloseActive()
    {
        _active?.Close();
    }

    private readonly ScreenBase _screen;
    private readonly GauntletLayer _layer;
    private readonly AwakeMessengerVM _dataSource;
    private object _movie;
    private bool _closed;

    private AwakeMessengerOverlay(ScreenBase screen)
    {
        _screen = screen;
        _dataSource = new AwakeMessengerVM(Close);
        _layer = new GauntletLayer("AwakeMessenger", 542, false);
    }

    private void OpenLayer()
    {
        _movie = _layer.LoadMovie("AwakeMessenger", _dataSource);
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
        try
        {
            _dataSource.OnFinalize();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_finalize_error error=" + ex.Message);
        }
        if (ReferenceEquals(_active, this)) _active = null;
        AwakeLog.Write("awake_messenger_panel_closed");
    }
}
