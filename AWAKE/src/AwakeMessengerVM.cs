using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using TaleWorlds.Library;

namespace Awake;

internal sealed class AwakeMessengerVM : ViewModel
{
    private readonly Action _close;
    private readonly MBBindingList<AwakeContactRowVM> _contacts = new MBBindingList<AwakeContactRowVM>();
    private readonly MBBindingList<NpcDialogueChatRowVM> _chatRows = new MBBindingList<NpcDialogueChatRowVM>();

    private NpcDialogueService _activeService;
    private string _activeTargetId = string.Empty;
    private string _titleText = AwakeLocalization.Resolve("awake.ui.messenger_title", "AWAKE 通讯录");
    private string _statusText = AwakeLocalization.Resolve("awake.ui.messenger_choose", "选择联系人开始对话。");
    private string _noticeText = string.Empty;
    private string _inputText = string.Empty;
    private string _streamingText = string.Empty;
    private bool _isLoading;
    private bool _closed;

    [DataSourceProperty]
    public MBBindingList<AwakeContactRowVM> Contacts => _contacts;

    [DataSourceProperty]
    public MBBindingList<NpcDialogueChatRowVM> ChatRows => _chatRows;

    [DataSourceProperty]
    public bool HasContacts => _contacts.Count > 0;

    [DataSourceProperty]
    public bool HasActive => _activeService != null;

    [DataSourceProperty]
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (Set(ref _isLoading, value, nameof(IsLoading)))
            {
                OnPropertyChangedWithValue(CanSend, nameof(CanSend));
            }
        }
    }

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
    public string NoticeText
    {
        get => _noticeText;
        private set => Set(ref _noticeText, value, nameof(NoticeText));
    }

    [DataSourceProperty]
    public string InputText
    {
        get => _inputText;
        set
        {
            if (Set(ref _inputText, value, nameof(InputText)))
            {
                OnPropertyChangedWithValue(CanSend, nameof(CanSend));
            }
        }
    }

    [DataSourceProperty]
    public string StreamingText
    {
        get => _streamingText;
        private set => Set(ref _streamingText, value, nameof(StreamingText));
    }

    [DataSourceProperty]
    public bool CanSend => _activeService != null
        && _activeService.IsAvailable
        && !_isLoading
        && !string.IsNullOrWhiteSpace(_inputText);

    [DataSourceProperty]
    public string ContactsTitle => AwakeLocalization.Resolve("awake.ui.contacts", "通讯录");

    [DataSourceProperty]
    public string SendButtonText => AwakeLocalization.Resolve("awake.ui.send", "发送");

    [DataSourceProperty]
    public string CloseButtonText => AwakeLocalization.Resolve("awake.ui.close", "离开");

    internal AwakeMessengerVM(Action close)
    {
        _close = close;
        try
        {
            AwakeMessengerHistory.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_history_load_error error=" + ex.Message);
        }
        foreach (AwakeContactInfo contact in AwakeMessengerService.BuildContacts())
        {
            AwakeContactInfo captured = contact;
            _contacts.Add(new AwakeContactRowVM(captured, () => SelectContact(captured.TargetId)));
        }
        OnPropertyChangedWithValue(HasContacts, nameof(HasContacts));
        AwakeContactRowVM firstNearby = null;
        foreach (AwakeContactRowVM row in _contacts)
        {
            if (row.IsNearby)
            {
                firstNearby = row;
                break;
            }
        }
        if (firstNearby != null)
        {
            firstNearby.ExecuteSelect();
        }
        else
        {
            StatusText = AwakeLocalization.Resolve("awake.ui.messenger_no_nearby", "附近暂时没有可对话对象。");
        }
    }

    internal void OnFrameTick()
    {
        if (_closed || _activeService == null) return;
        NpcDialogueUiEvent evt;
        while (_activeService.TryDrainUiEvent(out evt))
        {
            DrainEvent(evt);
        }
    }

    public void ExecuteClose()
    {
        if (_closed) return;
        _closed = true;
        try
        {
            _activeService?.CancelActiveAsync();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_vm_close_error error=" + ex.Message);
        }
        try
        {
            _close?.Invoke();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_vm_close_callback_error error=" + ex.Message);
        }
    }

    public void ExecuteSend()
    {
        if (!CanSend) return;
        string text = _inputText.Trim();
        if (text.Length > NpcDialogueConstants.MaxPlayerInputLength)
        {
            text = AwakeRuntime.TruncateTextElements(text, NpcDialogueConstants.MaxPlayerInputLength);
        }
        InputText = string.Empty;
        StreamingText = string.Empty;
        AddChatRow(AwakeLocalization.Resolve("awake.ui.you", "你"), text);
        AwakeMessengerHistory.Append(
            _activeTargetId,
            AwakeLocalization.Resolve("awake.ui.you", "你"),
            text);
        IsLoading = true;
        _ = SendAsyncSafe(text);
    }

    internal new void OnFinalize()
    {
        _closed = true;
        try
        {
            _activeService?.CancelActiveAsync();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_vm_finalize_error error=" + ex.Message);
        }
        try
        {
            _activeService?.Dispose();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_service_dispose_error error=" + ex.Message);
        }
        _activeService = null;
    }

    private void SelectContact(string targetId)
    {
        if (_closed || string.IsNullOrWhiteSpace(targetId)) return;
        if (StringComparer.Ordinal.Equals(_activeTargetId, targetId) && _activeService != null) return;

        DisposeActiveService();
        _chatRows.Clear();
        _streamingText = string.Empty;
        _isLoading = false;

        AwakeContactInfo contact = null;
        foreach (AwakeContactRowVM row in _contacts)
        {
            if (row.Contact != null && StringComparer.Ordinal.Equals(row.Contact.TargetId, targetId))
            {
                contact = row.Contact;
                break;
            }
        }
        if (contact == null || contact.Target == null)
        {
            TitleText = AwakeLocalization.Resolve("awake.ui.messenger_title", "AWAKE 通讯录");
            StatusText = AwakeLocalization.Resolve("awake.ui.contact_expired", "联系人已失效。");
            return;
        }
        if (!contact.IsNearby || !NpcDialogueLauncher.IsEligibleNpcTarget(contact.Target))
        {
            TitleText = contact.DisplayName;
            StatusText = AwakeLocalization.Resolve("awake.ui.contact_remote_letter", "远方联系人；写信功能将在后续版本开放。");
            NoticeText = AwakeLocalization.Resolve("awake.ui.contact_not_same_place", "你还没有和对方处于同一地点。");
            return;
        }

        IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
        if (host == null)
        {
            StatusText = AwakeLocalization.Resolve("awake.ui.host_missing", "AWAKE 尚未连接到 AI 宿主。");
            return;
        }

        NpcDialogueService service = new NpcDialogueService(host, contact.Target, NpcDialogueLauncher.CurrentSceneKeywords());
        service.Initialize();
        _activeService = service;
        _activeTargetId = targetId;
        TitleText = service.DisplayTitle;
        NoticeText = AwakeLocalization.Resolve("awake.ui.notice_opening", "对方似乎有话想对你说。");
        StatusText = AwakeLocalization.Resolve("awake.ui.status_starting", "对话正在苏醒……");
        foreach (AwakeMessengerChatLine line in AwakeMessengerHistory.GetHistory(targetId))
        {
            AddChatRow(line.Speaker, line.Text);
        }
        AddChatRow(
            AwakeLocalization.Resolve("awake.ui.system", "系统"),
            AwakeLocalization.Resolve("awake.ui.connecting", "正在连接……"));
        OnPropertyChangedWithValue(true, nameof(HasActive));
        OnPropertyChangedWithValue(CanSend, nameof(CanSend));
    }

    private void DisposeActiveService()
    {
        try
        {
            _activeService?.CancelActiveAsync();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_service_cancel_error error=" + ex.Message);
        }
        try
        {
            _activeService?.Dispose();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_service_dispose_error error=" + ex.Message);
        }
        _activeService = null;
        _activeTargetId = string.Empty;
        OnPropertyChangedWithValue(false, nameof(HasActive));
    }

    private async Task SendAsyncSafe(string text)
    {
        try
        {
            await _activeService.SendAsync(text, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_vm_send_error error=" + ex.Message);
            AwakeUiDispatcher.Enqueue(() => IsLoading = false);
        }
    }

    private void DrainEvent(NpcDialogueUiEvent evt)
    {
        switch (evt.Kind)
        {
            case NpcDialogueUiEventKind.Status:
                StatusText = evt.Text;
                break;
            case NpcDialogueUiEventKind.StreamDelta:
                StreamingText = AppendStream(StreamingText, evt.Text);
                break;
            case NpcDialogueUiEventKind.TurnCompleted:
                NpcDialogueTurnResult turnResult = evt.Turn;
                StreamingText = string.Empty;
                AddChatRow(_activeService.SpeakerName, turnResult.Reply);
                AwakeMessengerHistory.Append(_activeTargetId, _activeService.SpeakerName, turnResult.Reply);
                NoticeText = string.IsNullOrWhiteSpace(turnResult.Mood)
                    ? AwakeLocalization.Resolve("awake.ui.replied", "对方已回应。")
                    : AwakeLocalization.Resolve(
                        "awake.ui.replied_mood",
                        "对方已回应（" + turnResult.Mood + "）。",
                        new Dictionary<string, string> { ["MOOD"] = turnResult.Mood });
                IsLoading = false;
                break;
            case NpcDialogueUiEventKind.TurnFailed:
                NpcDialogueTurnResult failedResult = evt.Turn;
                StreamingText = string.Empty;
                AddChatRow(_activeService.SpeakerName, failedResult.ErrorDisplay);
                AwakeMessengerHistory.Append(
                    _activeTargetId,
                    AwakeLocalization.Resolve("awake.ui.system", "系统"),
                    failedResult.ErrorDisplay);
                NoticeText = failedResult.ErrorDisplay;
                IsLoading = false;
                break;
        }
    }

    private void AddChatRow(string speaker, string text)
    {
        while (_chatRows.Count >= 100) _chatRows.RemoveAt(0);
        _chatRows.Add(new NpcDialogueChatRowVM(speaker, text));
    }

    private static string AppendStream(string current, string delta)
    {
        const int maximum = 20000;
        string combined = (current ?? string.Empty) + (delta ?? string.Empty);
        return AwakeRuntime.TruncateTextElementsFromEnd(combined, maximum);
    }

    private bool Set(ref string field, string value, string name)
    {
        value ??= string.Empty;
        if (string.Equals(field, value, StringComparison.Ordinal)) return false;
        field = value;
        OnPropertyChangedWithValue(value, name);
        return true;
    }

    private bool Set(ref bool field, bool value, string name)
    {
        if (field == value) return false;
        field = value;
        OnPropertyChangedWithValue(value, name);
        return true;
    }
}
