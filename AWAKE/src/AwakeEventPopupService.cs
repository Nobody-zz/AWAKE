using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;

namespace Awake;

internal static class AwakeEventPopupService
{
    internal static bool Show(
        AwakeEventDefinition definition,
        Action<string, string> onChoice,
        Action onTimeout = null)
    {
        if (!AwakeEventValidation.Validate(definition, out string error))
        {
            AwakeLog.Write("awake_event_popup_invalid error=" + error);
            return false;
        }

        try
        {
            AwakeLog.Write("awake_event_popup_shown id=" + definition.Id);
            if (definition.DiscussionAction != null)
            {
                return ShowWithDiscussion(definition, onChoice, onTimeout);
            }
            InformationManager.ShowInquiry(
                new InquiryData(
                    definition.Title,
                    definition.Body,
                    true,
                    true,
                    definition.OptionA,
                    definition.OptionB,
                    () =>
                    {
                        AwakeLog.Write("awake_event_choice id=" + definition.Id + " choice=a");
                        onChoice?.Invoke(definition.Id, "a");
                    },
                    () =>
                    {
                        AwakeLog.Write("awake_event_choice id=" + definition.Id + " choice=b");
                        onChoice?.Invoke(definition.Id, "b");
                    },
                    "",
                    300f,
                    onTimeout,
                    null,
                    null),
                true,
                false);
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_popup_show_error id=" + definition.Id + " error=" + ex.Message);
            return false;
        }
    }

    private static bool ShowWithDiscussion(
        AwakeEventDefinition definition,
        Action<string, string> onChoice,
        Action onTimeout)
    {
        List<InquiryElement> elements = new List<InquiryElement>
        {
            new InquiryElement(
                "a",
                definition.OptionA,
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.event.choose_hint", "选择此项")),
            new InquiryElement(
                "b",
                definition.OptionB,
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.event.choose_hint", "选择此项")),
            new InquiryElement(
                "discuss",
                AwakeLocalization.Resolve("awake.event.discuss", "参与话题"),
                (ImageIdentifier)null,
                true,
                AwakeLocalization.Resolve("awake.event.discuss_hint", "进入 AI 对话深入探讨"))
        };
        MultiSelectionInquiryData data = new MultiSelectionInquiryData(
            definition.Title,
            definition.Body,
            elements,
            true,
            1,
            1,
            AwakeLocalization.Resolve("awake.event.confirm", "确定"),
            AwakeLocalization.Resolve("awake.event.leave", "离开"),
            selected =>
            {
                if (selected == null || selected.Count == 0) return;
                string choice = selected[0].Identifier as string;
                if (string.IsNullOrWhiteSpace(choice)) return;
                AwakeLog.Write("awake_event_choice id=" + definition.Id + " choice=" + choice);
                onChoice?.Invoke(definition.Id, choice);
            },
            _ => onTimeout?.Invoke(),
            "",
            false);
        MBInformationManager.ShowMultiSelectionInquiry(data, true, false);
        return true;
    }
}
