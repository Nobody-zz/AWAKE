using System;

namespace MarcusAwakeFramework.Api;

public static class FrameworkConsole
{
    public static Action OpenAiSetupAction { get; set; }

    public static Action OpenDiagnosticsAction { get; set; }

    public static void OpenAiSetup()
    {
        OpenAiSetupAction?.Invoke();
    }

    public static void OpenDiagnostics()
    {
        OpenDiagnosticsAction?.Invoke();
    }
}