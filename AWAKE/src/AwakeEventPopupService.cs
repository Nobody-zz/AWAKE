using System;
using TaleWorlds.Core;
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
}
