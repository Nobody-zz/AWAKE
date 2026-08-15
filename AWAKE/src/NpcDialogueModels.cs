using MarcusAIFramework.Api;
using TaleWorlds.Library;

namespace Awake;

internal enum NpcDialogueUiEventKind
{
    Status,
    StreamDelta,
    TurnCompleted,
    TurnFailed
}

internal sealed class NpcDialogueUiEvent
{
    internal NpcDialogueUiEventKind Kind { get; }
    internal string Text { get; }
    internal NpcDialogueTurnResult Turn { get; }

    internal NpcDialogueUiEvent(NpcDialogueUiEventKind kind, string text, NpcDialogueTurnResult turn)
    {
        Kind = kind;
        Text = text ?? string.Empty;
        Turn = turn;
    }
}

internal sealed class NpcDialogueTurnResult
{
    internal bool Ok { get; }
    internal string Reply { get; }
    internal string ErrorDisplay { get; }
    internal string Mood { get; }
    internal FrameworkError Error { get; }

    internal NpcDialogueTurnResult(bool ok, string reply, string errorDisplay, string mood, FrameworkError error = null)
    {
        Ok = ok;
        Reply = reply ?? string.Empty;
        ErrorDisplay = errorDisplay ?? string.Empty;
        Mood = mood ?? string.Empty;
        Error = error;
    }
}

internal sealed class NpcDialogueChatEntry
{
    internal string Role { get; }
    internal string Text { get; }

    internal NpcDialogueChatEntry(string role, string text)
    {
        Role = role ?? string.Empty;
        Text = text ?? string.Empty;
    }
}

internal sealed class NpcDialogueCommandProposal
{
    internal string CommandId { get; }
    internal string ArgumentsJson { get; }
    internal string Reason { get; }

    internal NpcDialogueCommandProposal(string commandId, string argumentsJson, string reason)
    {
        CommandId = commandId ?? string.Empty;
        ArgumentsJson = argumentsJson ?? "{}";
        Reason = reason ?? string.Empty;
    }
}

internal sealed class NpcDialogueChatRowVM : ViewModel
{
    private readonly string _speaker;
    private readonly string _text;

    [DataSourceProperty]
    public string Speaker => _speaker;

    [DataSourceProperty]
    public string Text => _text;

    internal NpcDialogueChatRowVM(string speaker, string text)
    {
        _speaker = speaker ?? string.Empty;
        _text = text ?? string.Empty;
    }
}
