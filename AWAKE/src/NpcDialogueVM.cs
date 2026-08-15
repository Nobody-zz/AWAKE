using System;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace Awake;

internal sealed class NpcDialogueVM : ViewModel
{
    private readonly NpcDialogueService _service;
    private readonly Action _close;
    private readonly MBBindingList<NpcDialogueChatRowVM> _chatRows = new MBBindingList<NpcDialogueChatRowVM>();

    private string _titleText;
    private string _statusText = string.Empty;
    private string _noticeText;
    private string _inputText = string.Empty;
    private string _streamingText = string.Empty;
    private bool _isLoading;
    private bool _closed;

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
    public bool CanSend => _service != null && _service.IsAvailable && !_isLoading && !string.IsNullOrWhiteSpace(_inputText);

    [DataSourceProperty]
    public MBBindingList<NpcDialogueChatRowVM> ChatRows => _chatRows;

    internal NpcDialogueVM(NpcDialogueService service, Action close)
    {
        _service = service;
        _close = close;
        _titleText = service.DisplayTitle;
        _noticeText = "对方似乎有话想对你说。";
        _statusText = "对话正在苏醒……";
        AddChatRow(service.SpeakerName, _noticeText);
    }

    internal void OnFrameTick()
    {
        if (_closed || _service == null) return;
        NpcDialogueUiEvent evt;
        while (_service.TryDrainUiEvent(out evt))
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
            _service?.CancelActiveAsync();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_vm_close_error error=" + ex.Message);
        }
        try
        {
            _close?.Invoke();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_vm_close_callback_error error=" + ex.Message);
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
        AddChatRow("你", text);
        IsLoading = true;
        _ = SendAsyncSafe(text);
    }

    internal new void OnFinalize()
    {
        _closed = true;
        _service?.CancelActiveAsync();
    }

    private async Task SendAsyncSafe(string text)
    {
        try
        {
            await _service.SendAsync(text, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_vm_send_error error=" + ex.Message);
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
                AddChatRow(_service.SpeakerName, turnResult.Reply);
                NoticeText = string.IsNullOrWhiteSpace(turnResult.Mood) ? "对方已回应。" : "对方已回应（" + turnResult.Mood + "）。";
                IsLoading = false;
                break;
            case NpcDialogueUiEventKind.TurnFailed:
                NpcDialogueTurnResult failedResult = evt.Turn;
                StreamingText = string.Empty;
                AddChatRow(_service.SpeakerName, failedResult.ErrorDisplay);
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
