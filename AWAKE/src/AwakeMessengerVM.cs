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
    private readonly MBBindingList<AwakeTranscriptRowVM> _historyRows = new MBBindingList<AwakeTranscriptRowVM>();
    private readonly AwakeContactCardVM _selectedCard = new AwakeContactCardVM();

    private Task _transcriptIndexTask;
    private NpcDialogueService _activeService;
    private string _activeTargetId = string.Empty;
    private string _activeContactKey = string.Empty;
    private string _titleText = AwakeLocalization.Resolve("awake.ui.messenger_title", "AWAKE 通讯录");
    private string _statusText = AwakeLocalization.Resolve("awake.ui.messenger_choose", "选择联系人开始对话。");
    private string _noticeText = string.Empty;
    private string _inputText = string.Empty;
    private string _streamingText = string.Empty;
    private string _historyStatusText = string.Empty;
    private bool _isLoading;
    private bool _isChatMode = true;
    private bool _isHistoryMode;
    private bool _closed;

    [DataSourceProperty]
    public MBBindingList<AwakeContactRowVM> Contacts => _contacts;

    [DataSourceProperty]
    public MBBindingList<NpcDialogueChatRowVM> ChatRows => _chatRows;

    [DataSourceProperty]
    public MBBindingList<AwakeTranscriptRowVM> HistoryRows => _historyRows;

    [DataSourceProperty]
    public AwakeContactCardVM SelectedCard => _selectedCard;

    [DataSourceProperty]
    public bool IsChatMode
    {
        get => _isChatMode;
        private set => Set(ref _isChatMode, value, nameof(IsChatMode));
    }

    [DataSourceProperty]
    public bool IsHistoryMode
    {
        get => _isHistoryMode;
        private set => Set(ref _isHistoryMode, value, nameof(IsHistoryMode));
    }

    [DataSourceProperty]
    public string HistoryStatusText
    {
        get => _historyStatusText;
        private set => Set(ref _historyStatusText, value, nameof(HistoryStatusText));
    }

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
    public string ChatTabText => AwakeLocalization.Resolve("awake.ui.chat_tab", "对话");

    [DataSourceProperty]
    public string HistoryTabText => AwakeLocalization.Resolve("awake.ui.history_tab", "历史");

    [DataSourceProperty]
    public string CloseButtonText => AwakeLocalization.Resolve("awake.ui.close", "离开");

    internal AwakeMessengerVM(Action close)
    {
        _close = close;
        _transcriptIndexTask = LoadTranscriptOnlyContactsAsync();
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

    public void ExecuteShowChat()
    {
        IsChatMode = true;
        IsHistoryMode = false;
    }

    public void ExecuteShowHistory()
    {
        IsChatMode = false;
        IsHistoryMode = true;
    }

    private async Task LoadHistoryAsync(string contactKey)
    {
        List<AwakeTranscriptLine> lines = new List<AwakeTranscriptLine>();
        if (_transcriptIndexTask != null)
        {
            try
            {
                await _transcriptIndexTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AwakeLog.Write("awake_messenger_transcript_index_await_error error=" + ex.Message);
            }
        }
        if (!string.IsNullOrWhiteSpace(contactKey))
        {
            try
            {
                lines = await AwakeTranscriptService.GetHistoryAsync(contactKey, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AwakeLog.Write("awake_messenger_transcript_load_error error=" + ex.Message);
            }
        }
        AwakeUiDispatcher.Enqueue(() => PopulateHistory(lines));
    }

    private void PopulateHistory(List<AwakeTranscriptLine> lines)
    {
        string expectedKey = _activeContactKey;
        _historyRows.Clear();
        if (lines != null)
        {
            foreach (AwakeTranscriptLine line in lines)
            {
                _historyRows.Add(new AwakeTranscriptRowVM(line, TogglePin));
            }
        }
        if (!StringComparer.Ordinal.Equals(expectedKey, _activeContactKey))
        {
            _historyRows.Clear();
            HistoryStatusText = string.Empty;
            return;
        }
        PopulateChatFromTranscript(lines);
        HistoryStatusText = _historyRows.Count == 0
            ? AwakeLocalization.Resolve("awake.ui.history_empty", "暂无历史记录")
            : AwakeLocalization.Resolve(
                "awake.ui.history_count",
                _historyRows.Count + " 条记录",
                new Dictionary<string, string> { ["COUNT"] = _historyRows.Count.ToString() });
    }

    private void PopulateChatFromTranscript(List<AwakeTranscriptLine> lines)
    {
        if (_closed) return;

        const int maximumChatRows = 100;
        string systemSpeaker = AwakeLocalization.Resolve("awake.ui.system", "系统");
        string connectingText = AwakeLocalization.Resolve("awake.ui.connecting", "正在连接……");
        for (int i = _chatRows.Count - 1; i >= 0; i--)
        {
            if (StringComparer.Ordinal.Equals(_chatRows[i].Speaker, systemSpeaker)
                && StringComparer.Ordinal.Equals(_chatRows[i].Text, connectingText))
            {
                _chatRows.RemoveAt(i);
            }
        }

        int lineCount = lines?.Count ?? 0;
        int capacity = Math.Max(0, maximumChatRows - _chatRows.Count);
        int start = Math.Max(0, lineCount - capacity);
        int insertIndex = 0;
        for (int i = start; i < lineCount; i++)
        {
            _chatRows.Insert(insertIndex, new NpcDialogueChatRowVM(lines[i].Speaker, lines[i].Text));
            insertIndex++;
        }
        if (_chatRows.Count == 0 && _activeService != null)
        {
            AddChatRow(systemSpeaker, connectingText);
        }
    }

    private void TogglePin(AwakeTranscriptRowVM row)
    {
        if (row?.Line == null || string.IsNullOrWhiteSpace(_activeContactKey)) return;
        bool newPinned = !row.IsPinned;
        _ = AwakeTranscriptService.PinLineAsync(_activeContactKey, row.Line.ChunkIndex, row.Line.Id, newPinned, CancellationToken.None)
            .ContinueWith(task => AwakeUiDispatcher.Enqueue(() =>
            {
                if (row.Line != null && task != null && task.IsCompleted && !task.IsFaulted && task.Result)
                {
                    row.SetPinned(newPinned);
                }
            }), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task LoadTranscriptOnlyContactsAsync()
    {
        List<string> keys = new List<string>();
        try
        {
            await AwakeTranscriptMigration.MigrateAsync(CancellationToken.None).ConfigureAwait(false);
            keys = await AwakeTranscriptService.LoadContactKeysAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_contact_index_load_error error=" + ex.Message);
        }
        AwakeUiDispatcher.Enqueue(() => AddTranscriptOnlyContacts(keys));
    }

    private void AddTranscriptOnlyContacts(List<string> keys)
    {
        if (_closed || keys == null) return;
        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            bool exists = false;
            foreach (AwakeContactRowVM row in _contacts)
            {
                if (row.Contact != null && StringComparer.Ordinal.Equals(row.Contact.CanonicalContactKey, key))
                {
                    exists = true;
                    break;
                }
            }
            if (exists) continue;
            AwakeContactInfo info = new AwakeContactInfo(
                null,
                key,
                AwakeLocalization.Resolve("awake.ui.contact_history", "历史联系人"),
                AwakeLocalization.Resolve("awake.ui.contact_status_history", "历史"),
                false,
                false,
                string.Empty,
                key);
            _contacts.Add(new AwakeContactRowVM(info, () => SelectContact(info.TargetId)));
        }
        OnPropertyChangedWithValue(HasContacts, nameof(HasContacts));
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
        if (contact == null)
        {
            _selectedCard.Clear();
            TitleText = AwakeLocalization.Resolve("awake.ui.messenger_title", "AWAKE 通讯录");
            StatusText = AwakeLocalization.Resolve("awake.ui.contact_expired", "联系人已失效。");
            return;
        }
        _selectedCard.Show(contact);
        _activeTargetId = contact.TargetId;
        _activeContactKey = contact.CanonicalContactKey;
        _ = LoadHistoryAsync(contact.CanonicalContactKey);
        if (contact.Target == null)
        {
            TitleText = contact.DisplayName;
            StatusText = AwakeLocalization.Resolve("awake.ui.contact_history_only", "历史联系人");
            NoticeText = AwakeLocalization.Resolve("awake.ui.contact_cannot_send", "当前无法发起新对话。");
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

        NpcDialogueService service = new NpcDialogueService(host, contact.Target, NpcDialogueLauncher.CurrentSceneKeywords(), "messenger");
        service.Initialize();
        _activeService = service;
        _activeTargetId = targetId;
        TitleText = service.DisplayTitle;
        NoticeText = AwakeLocalization.Resolve("awake.ui.notice_opening", "对方似乎有话想对你说。");
        StatusText = AwakeLocalization.Resolve("awake.ui.status_starting", "对话正在苏醒……");
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
        _activeContactKey = string.Empty;
        _historyRows.Clear();
        HistoryStatusText = string.Empty;
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
