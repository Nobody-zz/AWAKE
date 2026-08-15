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

internal sealed class AwakeEventDefinition
{
    internal string Id { get; }
    internal string Title { get; }
    internal string Body { get; }
    internal string OptionA { get; }
    internal string OptionB { get; }
    internal AwakeEventDialogueAction DialogueAction { get; }
    internal AwakeEventDialogueAction DiscussionAction { get; }

    internal AwakeEventDefinition(
        string id,
        string title,
        string body,
        string optionA,
        string optionB,
        AwakeEventDialogueAction dialogueAction = null,
        AwakeEventDialogueAction discussionAction = null)
    {
        Id = id ?? string.Empty;
        Title = title ?? string.Empty;
        Body = body ?? string.Empty;
        OptionA = optionA ?? string.Empty;
        OptionB = optionB ?? string.Empty;
        DialogueAction = dialogueAction;
        DiscussionAction = discussionAction;
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
        return true;
    }
}
