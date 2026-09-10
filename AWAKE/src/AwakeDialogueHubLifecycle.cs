namespace Awake;

internal static class AwakeDialogueHubLifecycle
{
    internal static bool IsAnyOpen =>
        NpcDialogueOverlay.IsOpen
        || AwakeMessengerOverlay.IsOpen
        || WorldEventInboxOverlay.IsOpen
        || WeeklyReportBrowserOverlay.IsOpen
        || DeveloperCheckOverlay.IsOpen;

    internal static void CloseAll()
    {
        NpcDialogueOverlay.CloseActive();
        AwakeMessengerOverlay.CloseActive();
        WorldEventInboxOverlay.CloseActive();
        WeeklyReportBrowserOverlay.CloseActive();
        DeveloperCheckOverlay.CloseActive();
    }
}
