using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal sealed class AwakeEventBehavior : CampaignBehaviorBase
{
    private readonly AwakeEventEngine _engine = new AwakeEventEngine();
    private int _lastWeeklyReportDay = -1;

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
            _ = NpcMemoryService.Current?.ConsolidateDailyForNearbyHeroesAsync(
                AwakeRuntime.CurrentGameDay(),
                CancellationToken.None);
            MaybeGenerateWeeklyReport();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_event_behavior_tick_error error=" + ex.Message);
        }
    }

    private void MaybeGenerateWeeklyReport()
    {
        try
        {
            int day = AwakeRuntime.CurrentGameDay();
            if (day <= 0 || day % 7 != 0 || day == _lastWeeklyReportDay) return;
            _lastWeeklyReportDay = day;
            List<WorldEventRecord> week = WorldEventLedger.SnapshotWeek(day);
            string report = NarrativeReportBuilder.Build(week, day);
            WorldEventLedger.Record(day, "weekly_report", "世界周报已生成。");
            AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve(
                "awake.feedback.weekly_report",
                "世界周报已生成，可到命令台查看。"));
            AwakeLog.Write("awake_weekly_report_generated day=" + day);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_weekly_report_generate_error error=" + ex.Message);
        }
    }
}
