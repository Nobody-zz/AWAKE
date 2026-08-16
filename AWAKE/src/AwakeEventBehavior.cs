using System;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal sealed class AwakeEventBehavior : CampaignBehaviorBase
{
    private readonly AwakeEventEngine _engine = new AwakeEventEngine();

    internal AwakeEventEngine Engine => _engine;

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnHourlyTick()
    {
        try
        {
            if (!AwakeSettings.Current.EnableEventEngine) return;
            if (NpcDialogueOverlay.IsOpen || AwakeMessengerOverlay.IsOpen) return;
            _ = _engine.OnHourlyTickAsync(CancellationToken.None);
            _ = NpcProactiveService.Current?.OnHourlyTickAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_behavior_tick_error error=" + ex.Message);
        }
    }
}
