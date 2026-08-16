using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Awake;

internal sealed class NpcDialogueOverlay
{
    private static NpcDialogueOverlay _active;

    internal static bool IsOpen => _active != null && !_active._closed;

    internal static bool Open(NpcDialogueService service, string entrySource = null, string targetId = null)
    {
        try
        {
            if (service == null || !service.IsAvailable)
            {
                InformationManager.ShowInquiry(new InquiryData(
                    AwakeLocalization.Resolve("awake.ui.dialogue_unavailable_title", "AWAKE·对话"),
                    AwakeLocalization.Resolve("awake.ui.dialogue_unavailable", "对方还没有准备好交谈。"),
                    true,
                    false,
                    AwakeLocalization.Resolve("awake.ui.ok", "确定"),
                    "",
                    null,
                    null,
                    "",
                    0f,
                    null,
                    null,
                    null), true, false);
                return false;
            }

            CloseActive();
            ScreenBase screen = ScreenManager.TopScreen;
            if (screen == null)
            {
                AwakeLog.Write("npc_dialogue_open_failed reason=no_top_screen");
                return false;
            }

            NpcDialogueOverlay overlay = new NpcDialogueOverlay(screen, service, entrySource, targetId);
            overlay.OpenLayer();
            if (!ReferenceEquals(ScreenManager.FocusedLayer, overlay._layer))
            {
                AwakeLog.Write("npc_dialogue_focus_pending");
            }
            _active = overlay;
            AwakeLog.Write("npc_dialogue_panel_opened");
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_open_failed error=" + ex.Message);
            return false;
        }
    }

    internal static void OnApplicationTick()
    {
        try
        {
            NpcDialogueOverlay active = _active;
            if (active == null) return;
            if (active._closed || !ReferenceEquals(ScreenManager.TopScreen, active._screen))
            {
                CloseActive();
                return;
            }
            if (active._layer.Input.IsKeyPressed(InputKey.Escape))
            {
                if (active._service != null
                    && active._service.IsSending
                    && !active._service.CanEscCancel)
                {
                    active._dataSource.ShowWaitingHint();
                    return;
                }
                active._dataSource.ExecuteClose();
                return;
            }
            active._dataSource.OnFrameTick();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_tick_failed error=" + ex.Message);
        }
    }

    internal static void CloseActive()
    {
        _active?.Close();
    }

    private readonly ScreenBase _screen;
    private readonly GauntletLayer _layer;
    private readonly NpcDialogueVM _dataSource;
    private readonly NpcDialogueService _service;
    private readonly string _entrySource;
    private readonly string _targetId;
    private object _movie;
    private bool _closed;

    private NpcDialogueOverlay(
        ScreenBase screen,
        NpcDialogueService service,
        string entrySource,
        string targetId)
    {
        _screen = screen;
        _service = service;
        _entrySource = entrySource ?? string.Empty;
        _targetId = targetId ?? string.Empty;
        _dataSource = new NpcDialogueVM(service, Close);
        _layer = new GauntletLayer("NpcDialogue", 541, false);
    }

    private void OpenLayer()
    {
        _movie = _layer.LoadMovie("NpcDialogue", _dataSource);
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
            AwakeLog.Write("npc_dialogue_finalize_error error=" + ex.Message);
        }
        try
        {
            _service?.Dispose();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_service_dispose_error error=" + ex.Message);
        }
        if (ReferenceEquals(_active, this)) _active = null;
        AwakeDialogueSessionCoordinator.Close(_entrySource, _targetId);
        AwakeLog.Write("npc_dialogue_panel_closed");
    }
}
