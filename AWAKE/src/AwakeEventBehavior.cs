using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal sealed class AwakeEventBehavior : CampaignBehaviorBase
{
    private readonly AwakeEventEngine _engine = new AwakeEventEngine();
    private int _lastWeeklyReportDay = -1;
    private int _knowledgeRefreshScheduled;

    internal AwakeEventEngine Engine => _engine;

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("awake_last_weekly_report_day", ref _lastWeeklyReportDay);
    }

    private void OnHourlyTick()
    {
        try
        {
            if (!AwakeSettings.Current.EnableEventEngine) return;
            if (NpcDialogueOverlay.IsOpen || AwakeMessengerOverlay.IsOpen) return;
            int sessionGeneration = AwakeRuntime.SessionGeneration;
            CancellationToken sessionCancellationToken = AwakeRuntime.SessionCancellationToken;
            if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration)) return;
            _engine.EnsureRulesLoadedFromRegistry();
            _ = _engine.OnHourlyTickAsync(sessionGeneration, sessionCancellationToken);
            _ = NpcProactiveService.Current?.OnHourlyTickAsync(sessionCancellationToken);
            _ = NpcMemoryService.Current?.ConsolidateDailyForNearbyHeroesAsync(
                AwakeRuntime.CurrentGameDay(),
                sessionCancellationToken);
            ScheduleKnowledgeRefresh(sessionGeneration, sessionCancellationToken);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_behavior_tick_error error=" + ex.Message);
        }
    }

    private void ScheduleKnowledgeRefresh(int sessionGeneration, CancellationToken sessionCancellationToken)
    {
        int day = AwakeRuntime.CurrentGameDay();
        if (day <= 0 || Interlocked.Exchange(ref _knowledgeRefreshScheduled, 1) != 0) return;
        AwakeBackgroundTask.Run(
            async () =>
            {
                try
                {
                    if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration)) return;
                    await AwakeRuntime.EnsureKnowledgeReadyIfNativeReadyAsync(sessionCancellationToken).ConfigureAwait(false);
                    WorldStateStore expectedStore = AwakeRuntime.WorldStateStore;
                    if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore))
                    {
                        AwakeLog.Write("awake_knowledge_refresh_ignored reason=stale_session");
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _knowledgeRefreshScheduled, 0);
                }
            },
            "awake_knowledge_refresh");
    }
}
