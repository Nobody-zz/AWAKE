using System;

namespace Awake;

internal enum AwakeEventCondition
{
    Always,
    InSettlement,
    InArmy,
    Camping,
    HasPrisoners
}

internal enum AwakeEventSource
{
    PresetRule,
    Situational,
    PlayerInitiated,
    NpcInitiated,
    DynamicAI
}

internal enum AwakeEventContext
{
    MapMarch,
    Camp,
    Settlement,
    Sea,
    Encounter,
    Scene
}

internal enum AwakeEventSubject
{
    PlayerNpc,
    NpcNpc,
    Clan,
    Kingdom,
    World,
    Environment
}

internal enum AwakeEventContent
{
    Daily,
    Survival,
    Military,
    Politics,
    Trade,
    Relationship,
    Intimate,
    Mystic,
    World
}

internal enum AwakeEventResolution
{
    NarrativeOnly,
    DialogueEntry,
    NumericSettlement,
    Chain,
    WorldEffect
}

internal enum AwakeEventChoiceShape
{
    Informational,
    TwoChoice,
    MultiChoice,
    DiscussionEntry,
    Timed
}

internal enum AwakeEventPersistence
{
    Repeatable,
    DailyCapped,
    OneTime,
    ChainUnique,
    CampaignPersistent,
    CrossSave
}

internal sealed class AwakeEventDialogueAction
{
    internal string Choice { get; }
    internal string TargetId { get; }
    internal string OpeningHint { get; }

    internal AwakeEventDialogueAction(string choice, string targetId, string openingHint = null)
    {
        Choice = choice ?? string.Empty;
        TargetId = targetId ?? string.Empty;
        OpeningHint = openingHint ?? string.Empty;
    }
}

internal sealed class AwakeEventEffect
{
    internal string Choice { get; }
    internal string TargetId { get; }
    internal int TrustDelta { get; }
    internal int LoveDelta { get; }
    internal int HostilityDelta { get; }
    internal string Reason { get; }

    internal AwakeEventEffect(
        string choice,
        string targetId,
        int trustDelta,
        int loveDelta,
        int hostilityDelta,
        string reason = null)
    {
        Choice = choice ?? string.Empty;
        TargetId = targetId ?? string.Empty;
        TrustDelta = trustDelta;
        LoveDelta = loveDelta;
        HostilityDelta = hostilityDelta;
        Reason = reason ?? string.Empty;
    }
}

internal sealed class AwakeEventDefinition
{
    internal string Id { get; }
    internal string Title { get; }
    internal string Body { get; }
    internal string OptionA { get; }
    internal string OptionB { get; }
    internal AwakeEventDialogueAction DialogueAction { get; }
    internal AwakeEventDialogueAction DiscussionAction { get; }
    internal AwakeEventEffect Effect { get; }
    internal AwakeEventSource? Source { get; }
    internal AwakeEventContext? Context { get; }
    internal AwakeEventSubject? Subject { get; }
    internal AwakeEventContent? Content { get; }
    internal AwakeEventResolution? Resolution { get; }
    internal AwakeEventChoiceShape? ChoiceShape { get; }
    internal AwakeEventPersistence? Persistence { get; }

    internal AwakeEventDefinition(
        string id,
        string title,
        string body,
        string optionA,
        string optionB,
        AwakeEventDialogueAction dialogueAction = null,
        AwakeEventDialogueAction discussionAction = null,
        AwakeEventSource? source = null,
        AwakeEventContext? context = null,
        AwakeEventSubject? subject = null,
        AwakeEventContent? content = null,
        AwakeEventResolution? resolution = null,
        AwakeEventChoiceShape? choiceShape = null,
        AwakeEventPersistence? persistence = null,
        AwakeEventEffect effect = null)
    {
        Id = id ?? string.Empty;
        Title = title ?? string.Empty;
        Body = body ?? string.Empty;
        OptionA = optionA ?? string.Empty;
        OptionB = optionB ?? string.Empty;
        DialogueAction = dialogueAction;
        DiscussionAction = discussionAction;
        Effect = effect;
        Source = source;
        Context = context;
        Subject = subject;
        Content = content;
        Resolution = resolution;
        ChoiceShape = choiceShape;
        Persistence = persistence;
    }
}

internal static class AwakeEventValidation
{
    internal static bool Validate(AwakeEventDefinition definition, out string error)
    {
        error = string.Empty;
        if (definition == null)
        {
            error = "definition";
            return false;
        }
        if (definition.Source == null) { error = "source"; return false; }
        if (definition.Context == null) { error = "context"; return false; }
        if (definition.Subject == null) { error = "subject"; return false; }
        if (definition.Content == null) { error = "content"; return false; }
        if (definition.Resolution == null) { error = "resolution"; return false; }
        if (definition.ChoiceShape == null) { error = "choiceShape"; return false; }
        if (definition.Persistence == null) { error = "persistence"; return false; }
        if (string.IsNullOrWhiteSpace(definition.Id) || definition.Id.Length > 60)
        {
            error = "id";
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.Title) || definition.Title.Length > 60)
        {
            error = "title";
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.Body) || definition.Body.Length > 1000)
        {
            error = "body";
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.OptionA) || definition.OptionA.Length > 40)
        {
            error = "optionA";
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.OptionB) || definition.OptionB.Length > 40)
        {
            error = "optionB";
            return false;
        }

        AwakeEventDialogueAction action = definition.DialogueAction;
        if (action != null)
        {
            if (!StringComparer.Ordinal.Equals(action.Choice, "a")
                && !StringComparer.Ordinal.Equals(action.Choice, "b"))
            {
                error = "dialogueAction.choice";
                return false;
            }
            if (string.IsNullOrWhiteSpace(action.TargetId) || action.TargetId.Length > 120)
            {
                error = "dialogueAction.targetId";
                return false;
            }
            if (action.OpeningHint.Length > 240)
            {
                error = "dialogueAction.openingHint";
                return false;
            }
        }

        AwakeEventDialogueAction discussion = definition.DiscussionAction;
        if (discussion != null)
        {
            if (!StringComparer.Ordinal.Equals(discussion.Choice, "discuss"))
            {
                error = "discussionAction.choice";
                return false;
            }
            if (string.IsNullOrWhiteSpace(discussion.TargetId) || discussion.TargetId.Length > 120)
            {
                error = "discussionAction.targetId";
                return false;
            }
            if (discussion.OpeningHint.Length > 240)
            {
                error = "discussionAction.openingHint";
                return false;
            }
        }

        AwakeEventEffect effect = definition.Effect;
        if (effect != null)
        {
            if (!StringComparer.Ordinal.Equals(effect.Choice, "a")
                && !StringComparer.Ordinal.Equals(effect.Choice, "b")
                && !StringComparer.Ordinal.Equals(effect.Choice, "discuss"))
            {
                error = "effect.choice";
                return false;
            }
            if (string.IsNullOrWhiteSpace(effect.TargetId) || effect.TargetId.Length > 120)
            {
                error = "effect.targetId";
                return false;
            }
            if (effect.TrustDelta < -100
                || effect.TrustDelta > 100
                || effect.LoveDelta < -100
                || effect.LoveDelta > 100
                || effect.HostilityDelta < -100
                || effect.HostilityDelta > 100
                || (effect.TrustDelta == 0 && effect.LoveDelta == 0 && effect.HostilityDelta == 0))
            {
                error = "effect.delta";
                return false;
            }
            if (effect.Reason.Length > 240)
            {
                error = "effect.reason";
                return false;
            }
        }
        return true;
    }
}
