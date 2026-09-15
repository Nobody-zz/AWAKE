using System;
using System.Diagnostics;
using System.IO;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Awake;

/// <summary>
/// 画位探测面板（开发者用）。照着 DeveloperCheckOverlay 的骨架写：
/// GauntletLayer → LoadMovie(预制体名, 数据源) → 挂到当前 Screen。
///
/// 它不参与任何玩法判定，也不阻塞对话——它只是一块"能看见那张 png"的玻璃。
/// </summary>
internal sealed class AwakePortraitProbeOverlay
{
    private static AwakePortraitProbeOverlay _active;

    internal static bool IsOpen
    {
        get { return _active != null && !_active._closed; }
    }

    internal static bool Open()
    {
        try
        {
            CloseActive();
            ScreenBase screen = ScreenManager.TopScreen;
            if (screen == null)
            {
                AwakeLog.Write("portrait_probe_open_failed reason=no_top_screen");
                return false;
            }

            AwakePortraitProbeOverlay overlay = new AwakePortraitProbeOverlay(screen);
            overlay.OpenLayer();
            _active = overlay;
            AwakeLog.Write("portrait_probe_opened cache_dir=" + AwakePortraitCache.AbsoluteRootDirectory);
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("portrait_probe_open_error error=" + ex.Message);
            return false;
        }
    }

    internal static void OnApplicationTick()
    {
        try
        {
            AwakePortraitProbeOverlay active = _active;
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
            AwakeLog.Write("portrait_probe_tick_failed error=" + ex.Message);
        }
    }

    internal static void CloseActive()
    {
        _active?.Close();
    }

    private readonly ScreenBase _screen;
    private readonly GauntletLayer _layer;
    private readonly AwakePortraitProbeVM _dataSource;
    private object _movie;
    private bool _closed;

    private AwakePortraitProbeOverlay(ScreenBase screen)
    {
        _screen = screen;
        _dataSource = new AwakePortraitProbeVM(Close, OpenCacheFolder);
        _layer = new GauntletLayer("AwakePortraitProbe", 545, false);
    }

    private static void OpenCacheFolder()
    {
        try
        {
            string directory = AwakePortraitCache.AbsoluteRootDirectory;
            Directory.CreateDirectory(directory);
            Process.Start("explorer.exe", "\"" + directory + "\"");
            AwakeLog.Write("portrait_probe_open_folder path=" + directory);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("portrait_probe_open_folder_error error=" + ex.Message);
        }
    }

    private void OpenLayer()
    {
        _movie = _layer.LoadMovie("AwakePortraitProbe", _dataSource);
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
        AwakeLog.Write("portrait_probe_closed");
    }
}
