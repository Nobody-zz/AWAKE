using System;
using MarcusAwakeFramework.Api;
using TaleWorlds.Library;

namespace Awake;

internal enum NpcDialogueActionMode
{
    Chat,
    Negotiation
}

internal enum NpcDialogueUiEventKind
{
    Status,
    StreamDelta,
    CommandConfirmationRequired,
    TurnCompleted,
    TurnFailed
}

internal sealed class NpcDialogueUiEvent
{
    internal NpcDialogueUiEventKind Kind { get; }
    internal string Text { get; }
    internal NpcDialogueTurnResult Turn { get; }
    internal NpcDialogueCommandConfirmation Confirmation { get; }

    internal NpcDialogueUiEvent(NpcDialogueUiEventKind kind, string text, NpcDialogueTurnResult turn, NpcDialogueCommandConfirmation confirmation = null)
    {
        Kind = kind;
        Text = text ?? string.Empty;
        Turn = turn;
        Confirmation = confirmation;
    }
}

internal sealed class NpcDialogueCommandConfirmation
{
    internal int Generation { get; }
    internal string CorrelationId { get; }
    internal NpcDialogueCommandProposal Proposal { get; }
    internal string Reply { get; }
    internal string Mood { get; }

    internal NpcDialogueCommandConfirmation(int generation, string correlationId, NpcDialogueCommandProposal proposal, string reply, string mood)
    {
        Generation = generation;
        CorrelationId = correlationId ?? string.Empty;
        Proposal = proposal;
        Reply = reply ?? string.Empty;
        Mood = mood ?? string.Empty;
    }

    internal string DisplayText => string.IsNullOrWhiteSpace(Proposal?.Reason)
        ? (Proposal?.CommandId ?? string.Empty)
        : Proposal.Reason;
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

internal sealed class NpcDialogueCommandSettlement
{
    internal bool Succeeded { get; }
    internal string StatusText { get; }

    internal NpcDialogueCommandSettlement(bool succeeded, string statusText)
    {
        Succeeded = succeeded;
        StatusText = statusText ?? string.Empty;
    }
}

internal sealed class NpcDialogueChatRowVM : ViewModel
{
    private readonly string _speaker;
    private readonly string _text;
    private readonly bool _isFromPlayer;

    [DataSourceProperty]
    public string Speaker => _speaker;

    [DataSourceProperty]
    public string Text => _text;

    // 气泡分侧：Gauntlet XML 绑定只支持正向属性路径，左右两列气泡容器
    // 需要一对互补布尔属性控制可见性（NPC/系统行靠左，玩家行靠右）。
    [DataSourceProperty]
    public bool IsFromPlayer => _isFromPlayer;

    [DataSourceProperty]
    public bool IsFromNpc => !_isFromPlayer;

    /// <summary>
    /// 「最新一条」——整个对话流里**唯一**带视觉落点的那条（左侧 2px 金条，稿 07）。
    /// 由宿主 VM 在每加一行时推：旧的清掉、新的立起。行自身不判断。
    /// </summary>
    [DataSourceProperty]
    public bool IsLatest => _isLatest;

    private bool _isLatest;

    internal void SetLatest(bool value)
    {
        if (_isLatest == value) return;
        _isLatest = value;
        OnPropertyChangedWithValue(value, nameof(IsLatest));
    }

    internal NpcDialogueChatRowVM(string speaker, string text)
    {
        _speaker = speaker ?? string.Empty;
        _text = text ?? string.Empty;
        _isFromPlayer = IsPlayerSpeaker(_speaker);
    }

    internal static bool IsPlayerSpeaker(string speaker)
    {
        return !string.IsNullOrEmpty(speaker)
            && StringComparer.Ordinal.Equals(speaker, AwakeLocalization.Resolve("awake.ui.you", "你"));
    }
}
