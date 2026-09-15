using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Awake;

internal sealed class AwakeEventBehavior : CampaignBehaviorBase
{
    private readonly AwakeEventEngine _engine = new AwakeEventEngine();
    private int _lastWeeklyReportDay = -1;
    private int _knowledgeRefreshScheduled;
    private int _formalReportRefreshScheduled;
    private string _personaAnchorJson = string.Empty;
    private PersonaPersistenceEnvelope _personaAnchor;

    internal AwakeEventEngine Engine => _engine;

    /// <summary>本 session 装载的 persona 锚点；内容生产者属下一批次。</summary>
    internal PersonaPersistenceEnvelope PersonaAnchor => _personaAnchor;

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(this, OnPlayerCharacterChanged);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("awake_last_weekly_report_day", ref _lastWeeklyReportDay);
        PersonaContinuitySync.Sync(dataStore, ref _personaAnchorJson, BuildPersonaAnchorSnapshot);
        if (dataStore.IsSaving && !string.IsNullOrWhiteSpace(_personaAnchorJson))
        {
            AwakeLog.Write("persona_continuity_saved key=" + PersonaContinuitySync.SaveKey
                + " bytes=" + Encoding.UTF8.GetByteCount(_personaAnchorJson)
                + " campaign=" + CurrentCampaignId());
        }
    }

    private static string CurrentCampaignId()
    {
        Campaign campaign = Campaign.Current;
        return campaign == null ? string.Empty : (campaign.UniqueGameId ?? string.Empty);
    }

    private static string CurrentCharacterId()
    {
        Hero mainHero = Hero.MainHero;
        return mainHero == null ? string.Empty : mainHero.StringId;
    }

    private string BuildPersonaAnchorSnapshot()
    {
        try
        {
            string json;
            string reason;
            if (!PersonaContinuitySync.TryBuildAnchorSnapshot(
                    CurrentCampaignId(),
                    CurrentCharacterId(),
                    out json,
                    out reason))
            {
                AwakeLog.Write("persona.persistence.skipped reason=" + reason);
                return string.Empty;
            }
            return json;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("persona.persistence.snapshot_error error=" + ex.Message);
            return string.Empty;
        }
    }

    private void OnGameLoaded(CampaignGameStarter campaignGameStarter)
    {
        try
        {
            PersonaPersistenceEnvelope envelope;
            string reason;
            PersonaLoadOutcome outcome = PersonaContinuitySync.Adopt(
                _personaAnchorJson,
                CurrentCampaignId(),
                CurrentCharacterId(),
                out envelope,
                out reason);
            if (outcome == PersonaLoadOutcome.Loaded)
            {
                _personaAnchor = envelope;
                AwakeLog.Write("persona_continuity_loaded campaign=" + _personaAnchor.Timeline.CampaignId
                    + " character=" + _personaAnchor.CharacterId
                    + " schema=" + _personaAnchor.Schema
                    + " sequence=" + _personaAnchor.Sequence);
                return;
            }
            _personaAnchor = null;
            // 旧档缺键/空值不告警；只有真正拒绝才记录。
            if (outcome == PersonaLoadOutcome.Rejected)
            {
                AwakeLog.Write("persona.persistence.rejected reason=" + reason);
            }
        }
        catch (Exception ex)
        {
            _personaAnchor = null;
            AwakeLog.Write("persona_continuity_load_error error=" + ex.Message);
        }
    }

    private void OnNewGameCreated(CampaignGameStarter campaignGameStarter)
    {
        try
        {
            // 新档：此刻 UniqueGameId 已生成；首存前允许没有 payload。
            _personaAnchor = null;
            _personaAnchorJson = string.Empty;
            AwakeLog.Write("persona_continuity_new_campaign campaign=" + CurrentCampaignId());
        }
        catch (Exception ex)
        {
            AwakeLog.Write("persona_continuity_new_campaign_error error=" + ex.Message);
        }
    }

    private void OnPlayerCharacterChanged(Hero oldPlayer, Hero newPlayer, MobileParty newMainParty, bool isMainPartyChanged)
    {
        try
        {
            // 换主角：作废本 session 锚点；旧数据不迁移也不删除。
            _personaAnchor = null;
            _personaAnchorJson = string.Empty;
            AwakeLog.Write("persona.persistence.character_changed old=" + (oldPlayer == null ? string.Empty : oldPlayer.StringId)
                + " new=" + (newPlayer == null ? string.Empty : newPlayer.StringId));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("persona_continuity_character_changed_error error=" + ex.Message);
        }
    }

    private void OnHourlyTick()
    {
        try
        {
            int sessionGeneration = AwakeRuntime.SessionGeneration;
            CancellationToken sessionCancellationToken = AwakeRuntime.SessionCancellationToken;
            if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration)) return;
            ScheduleFormalReportRefresh(sessionGeneration, sessionCancellationToken);
            int currentHour = AwakeRuntime.CurrentGameAbsoluteHour();
            _ = AwakeLetterService.AdvanceAsync(currentHour, sessionCancellationToken);
            // 主动来信只落未读、不打断玩家，故不受面板开关影响（面板一开就不来信是错的）；
            // 是否寄由 NpcLetterInitiator 自己按开关、动机、冷却与"上一封未读"决定。
            _ = NpcLetterInitiator.TryProduceAsync(currentHour, sessionCancellationToken);
            if (!AwakeSettings.Current.EnableEventEngine) return;
            if (NpcDialogueOverlay.IsOpen || AwakeMessengerOverlay.IsOpen) return;
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

    private void ScheduleFormalReportRefresh(int sessionGeneration, CancellationToken sessionCancellationToken)
    {
        int day = AwakeRuntime.CurrentGameDay();
        if (day <= 0 || Volatile.Read(ref _lastWeeklyReportDay) == day || Interlocked.Exchange(ref _formalReportRefreshScheduled, 1) != 0) return;
        AwakeBackgroundTask.Run(
            async () =>
            {
                try
                {
                    WorldStateStore expectedStore = AwakeRuntime.WorldStateStore;
                    if (expectedStore == null || !AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
                    FormalWeeklyReportResult result = await WorldEventServices.EnsureFormalReportsReadyAsync(day, sessionCancellationToken).ConfigureAwait(false);
                    if (!StringComparer.Ordinal.Equals(result.Status, FormalWeeklyReportResult.Unavailable))
                        Volatile.Write(ref _lastWeeklyReportDay, day);
                    AwakeLog.Write("awake_weekly_report_refresh status=" + result.Status + " code=" + result.Code);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("awake_weekly_report_refresh_error error=" + ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref _formalReportRefreshScheduled, 0);
                }
            },
            "awake_weekly_report_refresh");
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
