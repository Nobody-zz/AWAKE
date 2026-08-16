using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Awake;

internal sealed class WorldEventInboxOverlay
{
    private static WorldEventInboxOverlay _active;

    internal static bool IsOpen => _active != null && !_active._closed;

    internal static bool Open()
    {
        try
        {
            CloseActive();
            ScreenBase screen = ScreenManager.TopScreen;
            if (screen == null)
            {
                AwakeLog.Write("world_inbox_open_failed reason=no_top_screen");
                return false;
            }
            WorldEventLedger.LoadFromStoreAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            List<WorldEventRecord> records = WorldEventLedger.SnapshotWeek(AwakeRuntime.CurrentGameDay());
            WorldEventInboxOverlay overlay = new WorldEventInboxOverlay(screen, records);
            overlay.OpenLayer();
            if (!ReferenceEquals(ScreenManager.FocusedLayer, overlay._layer))
            {
                AwakeLog.Write("world_inbox_focus_pending");
            }
            _active = overlay;
            AwakeLog.Write("world_inbox_panel_opened");
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_inbox_open_error error=" + ex.Message);
            return false;
        }
    }

    internal static void OnApplicationTick()
    {
        try
        {
            WorldEventInboxOverlay active = _active;
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
            AwakeLog.Write("world_inbox_tick_failed error=" + ex.Message);
        }
    }

    internal static void CloseActive()
    {
        _active?.Close();
    }

    private readonly ScreenBase _screen;
    private readonly GauntletLayer _layer;
    private readonly WorldEventInboxVM _dataSource;
    private object _movie;
    private bool _closed;

    private WorldEventInboxOverlay(ScreenBase screen, IReadOnlyList<WorldEventRecord> records)
    {
        _screen = screen;
        _dataSource = new WorldEventInboxVM(Close, records);
        _layer = new GauntletLayer("WorldEventInbox", 543, false);
    }

    private void OpenLayer()
    {
        _movie = _layer.LoadMovie("WorldEventInbox", _dataSource);
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
        AwakeLog.Write("world_inbox_panel_closed");
    }
}
