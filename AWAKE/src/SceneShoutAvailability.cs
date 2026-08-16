namespace Awake;

internal enum SceneShoutMissionState
{
    Free,
    Battle,
    Deployment,
    Duel,
    Stealth,
    Tournament
}

internal enum SceneShoutAvailabilityResult
{
    Available,
    NoPeople,
    WrongContext,
    BlockedByOverlay,
    ConversationActive
}

internal static class SceneShoutAvailability
{
    internal static SceneShoutAvailabilityResult Evaluate(
        bool missionActive,
        SceneShoutMissionState missionState,
        bool conversationActive,
        bool blockingOverlayOpen,
        bool hasSettlementContext,
        int nearbyPeopleCount)
    {
        if (!missionActive)
        {
            return SceneShoutAvailabilityResult.WrongContext;
        }
        if (missionState != SceneShoutMissionState.Free)
        {
            return SceneShoutAvailabilityResult.WrongContext;
        }
        if (conversationActive)
        {
            return SceneShoutAvailabilityResult.ConversationActive;
        }
        if (blockingOverlayOpen)
        {
            return SceneShoutAvailabilityResult.BlockedByOverlay;
        }
        if (!hasSettlementContext && nearbyPeopleCount <= 0)
        {
            return SceneShoutAvailabilityResult.NoPeople;
        }
        return SceneShoutAvailabilityResult.Available;
    }
}
