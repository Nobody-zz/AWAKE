using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Awake;

internal sealed class SceneDialogueStatusOverlay
{
    private static SceneDialogueStatusOverlay _active;

    internal static bool IsOpen => _active != null && !_active._closed;

    internal static bool Open(string initialText)
    {
        try
        {
            ScreenBase screen = ScreenManager.TopScreen;
            if (screen == null)
            {
                AwakeLog.Write("scene_status_open_failed reason=no_top_screen");
                return false;
            }
            CloseActive();
            SceneDialogueStatusOverlay overlay = new SceneDialogueStatusOverlay(screen, initialText);
            overlay.OpenLayer();
            _active = overlay;
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("scene_status_open_error error=" + ex.Message);
            return false;
        }
    }

    internal static void Update(string text)
    {
        try
        {
            _active?._dataSource.UpdateText(text);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("scene_status_update_error error=" + ex.Message);
        }
    }

    internal static void OnApplicationTick()
    {
        try
        {
            SceneDialogueStatusOverlay active = _active;
            if (active == null) return;
            if (active._closed || !ReferenceEquals(ScreenManager.TopScreen, active._screen))
            {
                CloseActive();
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("scene_status_tick_error error=" + ex.Message);
        }
    }

    internal static void CloseActive()
    {
        _active?.Close();
    }

    private readonly ScreenBase _screen;
    private readonly GauntletLayer _layer;
    private readonly SceneDialogueStatusVM _dataSource;
    private object _movie;
    private bool _closed;

    private SceneDialogueStatusOverlay(ScreenBase screen, string initialText)
    {
        _screen = screen;
        _dataSource = new SceneDialogueStatusVM();
        _dataSource.UpdateText(initialText ?? string.Empty);
        _layer = new GauntletLayer("SceneDialogueStatus", 547, false);
    }

    private void OpenLayer()
    {
        _movie = _layer.LoadMovie("SceneDialogueStatus", _dataSource);
        _screen.AddLayer(_layer);
        _layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.All);
    }

    private void Close()
    {
        if (_closed) return;
        _closed = true;
        try
        {
            _layer.InputRestrictions.ResetInputRestrictions();
            _screen.RemoveLayer(_layer);
        }
        catch
        {
        }
        if (ReferenceEquals(_active, this)) _active = null;
    }
}
