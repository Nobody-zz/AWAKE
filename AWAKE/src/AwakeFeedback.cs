using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Awake;

internal enum AwakeFeedbackTone
{
    Info,
    Success,
    Warning,
    Error
}

internal static class AwakeFeedback
{
    internal static void Show(string text, AwakeFeedbackTone tone = AwakeFeedbackTone.Info)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            InformationManager.DisplayMessage(new InformationMessage(text, ColorFor(tone)));
        }
        catch
        {
        }
    }

    internal static void ShowSuccess(string text)
    {
        Show(text, AwakeFeedbackTone.Success);
    }

    internal static void ShowWarning(string text)
    {
        Show(text, AwakeFeedbackTone.Warning);
    }

    internal static void ShowError(string text)
    {
        Show(text, AwakeFeedbackTone.Error);
    }

    internal static Color ColorFor(AwakeFeedbackTone tone)
    {
        switch (tone)
        {
            case AwakeFeedbackTone.Success:
                return new Color(0.35f, 1f, 0.35f, 1f);
            case AwakeFeedbackTone.Warning:
                return new Color(1f, 0.95f, 0.25f, 1f);
            case AwakeFeedbackTone.Error:
                return new Color(1f, 0.3f, 0.3f, 1f);
            default:
                return new Color(0.85f, 0.9f, 1f, 1f);
        }
    }
}
