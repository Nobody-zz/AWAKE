using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldEventAppendResult
{
    internal const string Persisted = "persisted";
    internal const string DuplicateConfirmed = "duplicate_confirmed";
    internal const string KeyConflict = "key_conflict";
    internal const string PersistenceRetryable = "persistence_retryable";
    internal const string PersistenceFailed = "persistence_failed";
    internal const string PersistenceUnknown = "persistence_unknown";

    internal string Status { get; set; } = PersistenceFailed;
    internal string EventId { get; set; } = string.Empty;
    internal string EventKey { get; set; } = string.Empty;
    internal string Code { get; set; } = string.Empty;
    internal int Attempts { get; set; }
    internal WorldEventRecord Record { get; set; }

    internal bool Succeeded => StringComparer.Ordinal.Equals(Status, Persisted)
        || StringComparer.Ordinal.Equals(Status, DuplicateConfirmed);
}

internal sealed class WeeklyReportApplicationState
{
    internal string ReportId { get; set; } = string.Empty;
    internal string SchemaVersion { get; set; } = string.Empty;
    internal int WindowStartDay { get; set; }
    internal int WindowEndDay { get; set; }
    internal string Status { get; set; } = "retryable";
    internal int AttemptCount { get; set; }
    internal int LastAttemptDay { get; set; }
    internal string LastErrorCode { get; set; } = string.Empty;
    internal string ContentFingerprint { get; set; } = string.Empty;
    internal IReadOnlyList<string> SourceFactIds { get; set; } = Array.Empty<string>();
    internal bool Corrupt { get; set; }
    internal JObject Report { get; set; }
}

internal sealed class WeeklyReportStateWriteResult
{
    internal const string Applied = "applied";
    internal const string AlreadyApplied = "already_applied";
    internal const string Conflict = "conflict";
    internal const string Retryable = "retryable";
    internal const string Failed = "failed";

    internal string Status { get; set; } = Failed;
    internal string Code { get; set; } = string.Empty;
    internal int Attempts { get; set; }
    internal JObject Report { get; set; }
    internal bool Succeeded => StringComparer.Ordinal.Equals(Status, Applied)
        || StringComparer.Ordinal.Equals(Status, AlreadyApplied);
}

internal sealed class FormalWeeklyReportResult
{
    internal const string Applied = "applied";
    internal const string Preview = "preview";
    internal const string Unavailable = "unavailable";

    internal string Status { get; set; } = Unavailable;
    internal string Code { get; set; } = string.Empty;
    internal JObject Report { get; set; }
    internal bool HasFormalReport => StringComparer.Ordinal.Equals(Status, Applied) && Report != null;
}

internal sealed class WeeklyFactReportBuildResult
{
    internal JObject Report { get; set; }
    internal string ErrorCode { get; set; } = string.Empty;
    internal bool Succeeded => Report != null && string.IsNullOrWhiteSpace(ErrorCode);
}

internal interface IWorldEventRecorder
{
    int Count { get; }

    bool QueueRecord(int day, string eventType, string text, string eventKey = null);

    bool QueueRecord(int day, string eventType, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds);

    Task<WorldEventAppendResult> RecordAsync(int day, string eventType, string text, string eventKey = null, CancellationToken cancellationToken = default(CancellationToken));

    Task<WorldEventAppendResult> RecordAsync(int day, string eventType, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds, CancellationToken cancellationToken = default(CancellationToken));

    Task<WorldEventAppendResult> RecordAsyncForCampaign(int day, string eventType, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds, int campaignGeneration, CancellationToken cancellationToken = default(CancellationToken));

    Task LoadAsync(CancellationToken cancellationToken);

    IReadOnlyList<WorldEventRecord> SnapshotAll();

    IReadOnlyList<WorldEventRecord> SnapshotWindow(int startDay, int endDay);

    IReadOnlyList<WorldEventRecord> SnapshotWeek(int nowDay);

    void ResetForCampaign();
}

internal interface IWeeklyReportService
{
    JObject Build(IReadOnlyList<WorldEventRecord> records, int nowDay);

    JObject BuildWindow(IReadOnlyList<WorldEventRecord> records, int startDay, int endDay);

    string BuildText(IReadOnlyList<WorldEventRecord> records, int nowDay);
}

internal static class WorldEventServices
{
    private const int QueueRecordMaximumAttempts = 3;
    private static readonly object CampaignBoundaryGate = new object();
    private static readonly SemaphoreSlim FormalReportGate = new SemaphoreSlim(1, 1);

    internal static IWorldEventRecorder Recorder { get; } = new WorldEventLedgerRecorder();

    internal static IWeeklyReportService Reports { get; } = new WeeklyReportServiceFacade();

    internal static WorldKnowledgeProjectionService Projection { get; } = new WorldKnowledgeProjectionService();

    internal static void WithCampaignBoundary(Action action)
    {
        if (action == null) return;
        lock (CampaignBoundaryGate) action();
    }

    internal static T WithCampaignBoundary<T>(Func<T> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        lock (CampaignBoundaryGate) return action();
    }

    internal static WorldStateStore CaptureWorldStateStore(int campaignGeneration)
    {
        return WithCampaignBoundary(() =>
        {
            if (!IsCurrentCampaign(campaignGeneration, null)) return null;
            return AwakeRuntime.WorldStateStore;
        });
    }

    internal static bool IsCurrentWorldStateStore(int campaignGeneration, WorldStateStore store)
    {
        return WithCampaignBoundary(() => IsCurrentCampaign(campaignGeneration, store));
    }

    internal static bool TryProjectEventsIfReady(
        int campaignGeneration,
        WorldStateStore store,
        WorldEventLedgerSnapshot snapshot)
    {
        return WithCampaignBoundary(() =>
        {
            return CanProject(campaignGeneration, store, snapshot)
                && Projection.TryReplaceEvents(snapshot, null);
        });
    }

    internal static bool TryProjectSourcesIfReady(
        int campaignGeneration,
        WorldStateStore store,
        WorldEventLedgerSnapshot snapshot,
        IReadOnlyList<JObject> reports)
    {
        return WithCampaignBoundary(() =>
        {
            return CanProject(campaignGeneration, store, snapshot)
                && Projection.TryReplaceSources(snapshot, reports, null);
        });
    }

    internal static bool TryProjectFactSourcesIfReady(
        int campaignGeneration,
        WorldStateStore store,
        WorldEventLedgerSnapshot snapshot,
        WorldFactQueryResult facts,
        IReadOnlyList<JObject> reports)
    {
        return WithCampaignBoundary(() =>
        {
            return CanProject(campaignGeneration, store, snapshot)
                && Projection.TryReplaceFacts(facts, snapshot, reports, null);
        });
    }

    internal static void BindKnowledge(WorldKnowledgeSnapshot snapshot, WorldKnowledgeQueryService query)
    {
        WithCampaignBoundary(() => Projection.Bind(snapshot, query));
    }

    /// <summary>Single runtime entry point for all structured-fact consumers.</summary>
    internal static Task<WorldFactQueryResult> QueryFactsAsync(
        WorldFactQueryRequest request,
        CancellationToken cancellationToken)
    {
        return WorldEventContracts.QueryFactsAsync(request, cancellationToken);
    }

    internal static Task<WorldFactQueryResult> QueryRecentDynamicsAsync(int currentDay, CancellationToken cancellationToken)
    {
        return QueryFactsAsync(new WorldFactQueryRequest
        {
            Policy = WorldFactSelectionPolicy.RecentDynamics,
            CurrentDay = currentDay,
            MaximumResults = 20
        }, cancellationToken);
    }

    internal static Task<WorldFactQueryResult> QueryWeeklyDynamicsAsync(int currentDay, CancellationToken cancellationToken)
    {
        return QueryFactsAsync(new WorldFactQueryRequest
        {
            Policy = WorldFactSelectionPolicy.WeeklyDynamics,
            CurrentDay = currentDay,
            MaximumResults = 0
        }, cancellationToken);
    }

    internal static Task<WorldFactQueryResult> QueryCompletedWeeklyDynamicsAsync(int windowEndDay, CancellationToken cancellationToken)
    {
        return QueryFactsAsync(new WorldFactQueryRequest
        {
            Policy = WorldFactSelectionPolicy.WeeklyDynamics,
            StartDay = windowEndDay - 6,
            EndDay = windowEndDay,
            MaximumResults = 0
        }, cancellationToken);
    }

    internal static async Task<WeeklyFactReportBuildResult> BuildCompletedWeeklyReportFromFactsAsync(
        int windowEndDay,
        CancellationToken cancellationToken)
    {
        WorldFactQueryResult result = await QueryCompletedWeeklyDynamicsAsync(windowEndDay, cancellationToken).ConfigureAwait(false);
        return TryBuildWeeklyReportFromQueryResult(result);
    }

    internal static async Task<WeeklyReportStateWriteResult> PersistV2WeeklyReportAsync(
        JObject report,
        int currentDay,
        CancellationToken cancellationToken)
    {
        if (!WeeklyReportService.TryValidateV2Report(report, out _))
            return new WeeklyReportStateWriteResult
            {
                Status = WeeklyReportStateWriteResult.Failed,
                Code = "awake.world_report.v2.invalid_payload"
            };
        WorldStateStore store = CaptureWorldStateStore(WorldEventLedger.CampaignGeneration);
        if (store == null)
            return new WeeklyReportStateWriteResult
            {
                Status = WeeklyReportStateWriteResult.Retryable,
                Code = "awake.world_report.v2.storage_unavailable"
            };
        return await store.UpsertWeeklyReportStateAsync(
            (string)report["reportId"],
            (int)report["extensions"]["awake:windowStartDay"],
            (int)report["extensions"]["awake:windowEndDay"],
            "applied",
            currentDay,
            string.Empty,
            report,
            cancellationToken).ConfigureAwait(false);
    }

    internal static WeeklyFactReportBuildResult TryBuildWeeklyReportFromQueryResult(WorldFactQueryResult result)
    {
        if (!WeeklyDynamicsInput.TryCreate(result, out WeeklyDynamicsInput input, out string inputError))
            return new WeeklyFactReportBuildResult { ErrorCode = inputError };
        if (!WeeklyReportService.TryBuildFromFacts(input, out JObject report, out string reportError))
            return new WeeklyFactReportBuildResult { ErrorCode = reportError };
        return new WeeklyFactReportBuildResult { Report = report };
    }

    internal static async Task<WorldFactQueryResult> QueryFactsForCampaignAsync(
        WorldFactQuery query,
        WorldFactQueryRequest request,
        int campaignGeneration,
        WorldStateStore store,
        CancellationToken cancellationToken)
    {
        WorldFactQueryResult result = await query.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        if (!IsCurrentCampaign(campaignGeneration, store))
            return new WorldFactQueryResult(
                WorldFactQueryStatus.Unavailable,
                request == null ? default(WorldFactSelectionPolicy) : request.Policy,
                errorCode: "awake.world_fact.stale_session");
        AwakeLog.Write("awake_world_fact_query policy=" + result.Policy
            + " status=" + result.Status
            + " count=" + result.Facts.Count
            + " generation=" + campaignGeneration
            + " error=" + result.ErrorCode);
        return result;
    }

    internal static void ResetForCampaign()
    {
        WithCampaignBoundary(() =>
        {
            Recorder.ResetForCampaign();
            Projection.Clear();
            WorldEventContracts.ResetForCampaign();
        });
    }

    internal static void QueueRecord(int day, string eventType, string text, string eventKey = null)
    {
        QueueRecord(day, eventType, text, eventKey, null);
    }

    internal static void QueueRecord(
        int day,
        string eventType,
        string text,
        string eventKey,
        IReadOnlyList<string> visibilityIdentityIds)
    {
        int campaignGeneration = WorldEventLedger.CampaignGeneration;
        AwakeBackgroundTask.Run(
            async () =>
            {
                try
                {
                    WorldEventAppendResult result = null;
                    for (int attempt = 1; attempt <= QueueRecordMaximumAttempts; attempt++)
                    {
                        result = await Recorder.RecordAsyncForCampaign(
                            day,
                            eventType,
                            text,
                            eventKey,
                            visibilityIdentityIds,
                            campaignGeneration,
                            CancellationToken.None).ConfigureAwait(false);
                        if (!IsRetryable(result) || attempt == QueueRecordMaximumAttempts) break;
                        await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt)).ConfigureAwait(false);
                    }
                    AwakeLog.Write("world_event_record_status status=" + result.Status
                        + " event=" + result.EventId
                        + " code=" + result.Code);
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("world_event_record_task_error error=" + ex.Message);
                }
            },
            "world_event_record");
    }

    private static bool IsRetryable(WorldEventAppendResult result)
    {
        return result != null
            && (StringComparer.Ordinal.Equals(result.Status, WorldEventAppendResult.PersistenceRetryable)
                || StringComparer.Ordinal.Equals(result.Status, WorldEventAppendResult.PersistenceUnknown))
            && !StringComparer.Ordinal.Equals(result.Code, "awake.world_event.stale_session");
    }

    internal static async Task<FormalWeeklyReportResult> EnsureFormalReportsReadyAsync(int currentDay, CancellationToken cancellationToken)
    {
        if (currentDay <= 0) return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Preview };
        int campaignGeneration = WorldEventLedger.CampaignGeneration;
        WorldStateStore expectedStore = CaptureWorldStateStore(campaignGeneration);
        if (expectedStore == null || !IsCurrentCampaign(campaignGeneration, expectedStore))
            return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Unavailable, Code = "awake.weekly_report.storage_unavailable" };
        await FormalReportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsCurrentCampaign(campaignGeneration, expectedStore))
                return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Unavailable, Code = "awake.weekly_report.stale_session" };
            List<WeeklyReportApplicationState> states = await expectedStore.GetWeeklyReportStatesAsync(cancellationToken).ConfigureAwait(false);
            if (states == null || !IsCurrentCampaign(campaignGeneration, expectedStore))
                return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Unavailable, Code = "awake.weekly_report.read_failed" };
            List<int> completed = WeeklyReportService.CompletedWindowEnds(currentDay);
            if (completed.Count == 0) return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Preview };
            int endDay = completed[completed.Count - 1];
            int startDay = endDay - 6;
            WorldFactQueryResult facts = await QueryCompletedWeeklyDynamicsAsync(endDay, cancellationToken).ConfigureAwait(false);
            if (facts.Status != WorldFactQueryStatus.Success && facts.Status != WorldFactQueryStatus.Empty)
            {
                AwakeLog.Write("awake_weekly_report_fact_query_failed window=" + endDay
                    + " status=" + facts.Status + " error=" + facts.ErrorCode);
                return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Unavailable, Code = facts.ErrorCode ?? "awake.weekly_report.fact_query_failed" };
            }
            WeeklyDynamicsInput input;
            JObject report = null;
            string inputError = string.Empty;
            string reportError = string.Empty;
            bool validInput = WeeklyDynamicsInput.TryCreate(facts, out input, out inputError);
            bool built = validInput && WeeklyReportService.TryBuildV2FromFacts(input, out report, out reportError);
            bool validReport = built && WeeklyReportService.TryValidateV2Report(report, out reportError);
            if (!validInput || !built || !validReport)
            {
                AwakeLog.Write("awake_weekly_report_invalid window=" + endDay + " error=" + (reportError ?? inputError));
                return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Unavailable, Code = reportError ?? inputError ?? "awake.weekly_report.invalid" };
            }
            string reportId = (string)report["reportId"] ?? string.Empty;
            WeeklyReportApplicationState state = states.FirstOrDefault(value => StringComparer.Ordinal.Equals(value.ReportId, reportId));
            bool hasStableSnapshot = state?.Report != null
                && StringComparer.Ordinal.Equals((string)state.Report["reportId"], reportId)
                && !state.Corrupt
                && StringComparer.Ordinal.Equals(state.SchemaVersion, WeeklyReportService.V2SchemaVersion)
                && WeeklyReportService.TryValidateV2Report(state.Report, out _);
            if (state != null && StringComparer.Ordinal.Equals(state.Status, "applied") && hasStableSnapshot)
                return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Applied, Report = (JObject)state.Report.DeepClone() };
            if (state != null && StringComparer.Ordinal.Equals(state.Status, "applied"))
                AwakeLog.Write("awake_weekly_report_snapshot_repair report=" + reportId);
            WeeklyReportStateWriteResult write = await expectedStore.UpsertWeeklyReportStateAsync(
                reportId, startDay, endDay, "applied", currentDay, string.Empty, report, cancellationToken).ConfigureAwait(false);
            if (!IsCurrentCampaign(campaignGeneration, expectedStore))
                return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Unavailable, Code = "awake.weekly_report.stale_session" };
            if (!write.Succeeded)
            {
                AwakeLog.Write("awake_weekly_report_state_not_applied report=" + reportId + " status=" + write.Status + " code=" + write.Code);
                return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Unavailable, Code = write.Code };
            }
            JObject appliedReport = write.Report ?? report;
            string appliedReportError;
            bool validAppliedReport = WeeklyReportService.TryValidateV2Report(appliedReport, out appliedReportError);
            if (!StringComparer.Ordinal.Equals((string)appliedReport["reportId"], reportId) || !validAppliedReport)
            {
                AwakeLog.Write("awake_weekly_report_snapshot_invalid report=" + reportId + " error=" + appliedReportError);
                return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Unavailable, Code = "awake.weekly_report.invalid_snapshot" };
            }
            return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Applied, Report = (JObject)appliedReport.DeepClone() };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_weekly_report_ready_error error=" + ex.Message);
            return new FormalWeeklyReportResult { Status = FormalWeeklyReportResult.Unavailable, Code = "awake.weekly_report.exception" };
        }
        finally
        {
            FormalReportGate.Release();
        }
    }

    internal static async Task EnsureKnowledgeReadyAsync(int currentDay, CancellationToken cancellationToken)
    {
        if (currentDay <= 0) return;
        int campaignGeneration = WorldEventLedger.CampaignGeneration;
        WorldStateStore expectedStore = CaptureWorldStateStore(campaignGeneration);
        if (expectedStore == null || !CanProject(campaignGeneration, expectedStore)) return;
        JObject formal = await ReadLatestAppliedWeeklyReportAsync(currentDay, expectedStore, cancellationToken).ConfigureAwait(false);
        if (formal == null || !CanProject(campaignGeneration, expectedStore)) return;
        WorldEventLedgerSnapshot snapshot = WorldEventLedger.CaptureSnapshot();
        WorldFactQueryResult facts = await QueryFactsAsync(new WorldFactQueryRequest
        {
            Policy = WorldFactSelectionPolicy.WorldKnowledge,
            CurrentDay = currentDay,
            MaximumResults = 100
        }, cancellationToken).ConfigureAwait(false);
        if (facts.Status != WorldFactQueryStatus.Success && facts.Status != WorldFactQueryStatus.Empty)
        {
            AwakeLog.Write("awake_knowledge_fact_query_skipped generation=" + campaignGeneration
                + " status=" + facts.Status + " error=" + facts.ErrorCode);
            return;
        }
        if (!TryProjectFactSourcesIfReady(campaignGeneration, expectedStore, snapshot, facts, new[] { formal }))
            AwakeLog.Write("awake_knowledge_projection_skipped generation=" + campaignGeneration);
    }

    private static async Task<JObject> ReadLatestAppliedWeeklyReportAsync(
        int currentDay,
        WorldStateStore expectedStore,
        CancellationToken cancellationToken)
    {
        List<WeeklyReportApplicationState> states = await expectedStore.GetWeeklyReportStatesAsync(cancellationToken).ConfigureAwait(false);
        if (states == null) return null;
        List<int> completed = WeeklyReportService.CompletedWindowEnds(currentDay);
        if (completed.Count == 0) return null;
        int endDay = completed[completed.Count - 1];
        return states
            .Where(state => state != null
                && StringComparer.Ordinal.Equals(state.Status, "applied")
                && !state.Corrupt
                && state.WindowEndDay == endDay
                && state.Report != null
                && WorldEventContract.TryValidateWeeklyReport(state.Report, out _))
            .OrderByDescending(state => StringComparer.Ordinal.Equals(state.SchemaVersion, WeeklyReportService.V2SchemaVersion))
            .Select(state => (JObject)state.Report.DeepClone())
            .FirstOrDefault();
    }

    private static bool IsCurrentCampaign(int campaignGeneration, WorldStateStore store)
    {
        if (!WorldEventLedger.IsCurrentCampaignGeneration(campaignGeneration) || AwakeRuntime.SessionEnded)
            return false;
        return store == null || ReferenceEquals(AwakeRuntime.WorldStateStore, store);
    }

    private static bool CanProject(int campaignGeneration, WorldStateStore store, WorldEventLedgerSnapshot snapshot)
    {
        return store != null
            && snapshot != null
            && snapshot.CampaignGeneration == campaignGeneration
            && IsCurrentCampaign(campaignGeneration, store)
            && AwakeRuntime.IsNativeKnowledgeReady();
    }

    private static bool CanProject(int campaignGeneration, WorldStateStore store)
    {
        return store != null
            && IsCurrentCampaign(campaignGeneration, store)
            && AwakeRuntime.IsNativeKnowledgeReady();
    }
}

internal static class WorldEventContracts
{
    private sealed class RuntimeFactContextReader : IWorldFactContextReader
    {
        public Task<WorldFactQueryResult> QueryAsync(
            WorldFactQueryRequest request,
            CancellationToken cancellationToken)
        {
            return QueryFactsFromRuntimeAsync(request, cancellationToken);
        }
    }

    private static IWorldFactContextReader _factContextReader = new RuntimeFactContextReader();

    internal static void SetFactContextReaderForTesting(IWorldFactContextReader reader)
    {
        _factContextReader = reader ?? new RuntimeFactContextReader();
    }

    internal static void ResetForCampaign()
    {
        _factContextReader = new RuntimeFactContextReader();
    }

    /// <summary>Single runtime entry point for all structured-fact consumers.</summary>
    internal static Task<WorldFactQueryResult> QueryFactsAsync(
        WorldFactQueryRequest request,
        CancellationToken cancellationToken)
    {
        IWorldFactContextReader reader = _factContextReader ?? new RuntimeFactContextReader();
        return reader.QueryAsync(request, cancellationToken);
    }

    internal static async Task<WorldFactQueryResult> QueryEventTriggerCandidatesAsync(
        int currentDay,
        CancellationToken cancellationToken)
    {
        WorldFactQueryResult result;
        try
        {
            result = await QueryFactsAsync(new WorldFactQueryRequest
            {
                Policy = WorldFactSelectionPolicy.EventTriggerCandidate,
                CurrentDay = currentDay,
                MaximumResults = 0,
                AllowLegacyFallback = false
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new WorldFactQueryResult(
                WorldFactQueryStatus.Cancelled,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                errorCode: "awake.cancelled");
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_world_fact_event_query_error consumer=event policy=EventTriggerCandidate error_code=awake.world_fact.query_exception message=" + ex.Message);
            result = new WorldFactQueryResult(
                WorldFactQueryStatus.Unavailable,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                errorCode: "awake.world_fact.query_exception");
        }

        if (result == null)
        {
            result = new WorldFactQueryResult(
                WorldFactQueryStatus.Unavailable,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                errorCode: "awake.world_fact.query_result_null");
        }
        else if (result.LegacyFallbackState == LegacyFallbackState.Used || result.UsedLegacyFallback)
        {
            result = new WorldFactQueryResult(
                WorldFactQueryStatus.Unavailable,
                WorldFactSelectionPolicy.EventTriggerCandidate,
                errorCode: "awake.world_fact.event.legacy_fallback",
                usedLegacyFallback: true,
                legacyFallbackState: LegacyFallbackState.Used,
                windowStartDay: result.WindowStartDay,
                windowEndDay: result.WindowEndDay);
        }
        AwakeLog.Write("awake_world_fact_event_query consumer=event"
            + " policy=EventTriggerCandidate"
            + " status=" + result.Status
            + " journal_revision=" + (result.JournalRevision.HasValue ? result.JournalRevision.Value.ToString(CultureInfo.InvariantCulture) : "missing")
            + " fact_count=" + result.Facts.Count.ToString(CultureInfo.InvariantCulture)
            + " source_fact_count=" + result.SourceFactIds.Count.ToString(CultureInfo.InvariantCulture)
            + " used_legacy_fallback=" + result.UsedLegacyFallback
            + " legacy_fallback_state=" + result.LegacyFallbackState
            + " error_code=" + (result.ErrorCode ?? string.Empty)
            + " correlation_id=" + Guid.NewGuid().ToString("N"));
        return result;
    }

    private static Task<WorldFactQueryResult> QueryFactsFromRuntimeAsync(
        WorldFactQueryRequest request,
        CancellationToken cancellationToken)
    {
        int campaignGeneration = WorldEventLedger.CampaignGeneration;
        WorldStateStore store = WorldEventServices.CaptureWorldStateStore(campaignGeneration);
        IReadOnlyList<WorldEventRecord> legacySnapshot = WorldEventLedger.SnapshotAll();
        var query = new WorldFactQuery(async token =>
        {
            if (store == null)
                return new WorldFactJournalReadResult(
                    WorldFactJournalReadStatus.Unavailable,
                    errorCode: "awake.world_fact.storage_unavailable");
            return await store.GetWorldFactJournalAsync(null, token).ConfigureAwait(false);
        }, () => legacySnapshot);
        return WorldEventServices.QueryFactsForCampaignAsync(query, request, campaignGeneration, store, cancellationToken);
    }
}

internal sealed class WorldEventLedgerRecorder : IWorldEventRecorder
{
    public int Count => WorldEventLedger.Count;

    public bool QueueRecord(int day, string eventType, string text, string eventKey = null)
    {
        return WorldEventLedger.QueueRecord(day, eventType, text, eventKey);
    }

    public bool QueueRecord(int day, string eventType, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds)
    {
        return WorldEventLedger.QueueRecord(day, eventType, text, eventKey, visibilityIdentityIds);
    }

    public Task<WorldEventAppendResult> RecordAsync(int day, string eventType, string text, string eventKey = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return WorldEventLedger.RecordAsync(day, eventType, text, eventKey, cancellationToken);
    }

    public Task<WorldEventAppendResult> RecordAsync(int day, string eventType, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds, CancellationToken cancellationToken = default(CancellationToken))
    {
        return WorldEventLedger.RecordAsync(day, eventType, text, eventKey, visibilityIdentityIds, cancellationToken);
    }

    public Task<WorldEventAppendResult> RecordAsyncForCampaign(int day, string eventType, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds, int campaignGeneration, CancellationToken cancellationToken = default(CancellationToken))
    {
        return WorldEventLedger.RecordAsyncForCampaign(day, eventType, text, eventKey, visibilityIdentityIds, campaignGeneration, cancellationToken);
    }

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        return WorldEventLedger.LoadFromStoreAsync(cancellationToken);
    }

    public IReadOnlyList<WorldEventRecord> SnapshotWeek(int nowDay)
    {
        return WorldEventLedger.SnapshotWeek(nowDay);
    }

    public IReadOnlyList<WorldEventRecord> SnapshotAll()
    {
        return WorldEventLedger.SnapshotAll();
    }

    public IReadOnlyList<WorldEventRecord> SnapshotWindow(int startDay, int endDay)
    {
        return WorldEventLedger.SnapshotWindow(startDay, endDay);
    }

    public void ResetForCampaign()
    {
        WorldEventLedger.ResetForCampaign();
    }
}

internal sealed class WeeklyReportServiceFacade : IWeeklyReportService
{
    public JObject Build(IReadOnlyList<WorldEventRecord> records, int nowDay)
    {
        return WeeklyReportService.Build(records, nowDay);
    }

    public JObject BuildWindow(IReadOnlyList<WorldEventRecord> records, int startDay, int endDay)
    {
        return WeeklyReportService.BuildWindow(records, startDay, endDay);
    }

    public string BuildText(IReadOnlyList<WorldEventRecord> records, int nowDay)
    {
        return WeeklyReportService.BuildText(records, nowDay);
    }
}

internal static class WorldEventAudience
{
    private static readonly string[] DefaultIdentityIds =
    {
        "awake:identity:headman",
        "awake:identity:merchant",
        "awake:identity:tavernkeeper",
        "awake:identity:ransom_broker"
    };

    internal static IReadOnlyList<string> Default => DefaultIdentityIds;

    internal static IReadOnlyList<string> None => Array.Empty<string>();

    internal static List<string> Normalize(IEnumerable<string> identityIds)
    {
        var result = new List<string>();
        foreach (string identityId in identityIds ?? Enumerable.Empty<string>())
        {
            string normalized = WorldbookIdentityEvaluator.NormalizeIdentity(identityId);
            if (WorldEventContract.IsStableId(normalized) && !result.Contains(normalized, StringComparer.Ordinal))
                result.Add(normalized);
        }
        return result;
    }

    internal static IReadOnlyList<string> Resolve(IEnumerable<string> identityIds)
    {
        if (identityIds == null) return new List<string>(Default);
        return Normalize(identityIds);
    }

    internal static IReadOnlyList<string> Intersect(IReadOnlyList<WorldEventRecord> records)
    {
        List<string> result = null;
        foreach (WorldEventRecord record in records ?? new List<WorldEventRecord>())
        {
            List<string> audience = Normalize(record?.VisibilityIdentityIds);
            if (result == null)
            {
                result = audience;
                continue;
            }
            result = result.Where(audience.Contains).ToList();
        }
        return result ?? new List<string>(None);
    }
}

internal static class WorldEventContract
{
    private static readonly Regex StableIdPattern = new Regex(
        "^[a-z][a-z0-9_-]*:[a-z][a-z0-9_-]*:[a-z][a-z0-9_.-]*$",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Domains = new HashSet<string>(
        new[] { "politics", "economy", "culture", "war" },
        StringComparer.Ordinal);

    private static readonly HashSet<string> Scopes = new HashSet<string>(
        new[] { "local", "regional", "national", "faction", "elite", "private" },
        StringComparer.Ordinal);

    private static readonly HashSet<string> DetailLevels = new HashSet<string>(
        new[] { "rumor", "summary", "detail", "secret" },
        StringComparer.Ordinal);

    internal static bool IsStableId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && StableIdPattern.IsMatch(value);
    }

    internal static string NormalizeEventType(string value)
    {
        string source = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (source.Length == 0) return "event";

        StringBuilder builder = new StringBuilder(source.Length);
        foreach (char character in source)
        {
            bool isAsciiLetter = character >= 'a' && character <= 'z';
            bool isDigit = character >= '0' && character <= '9';
            if (isAsciiLetter || isDigit || character == '_' || character == '-' || character == '.')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('_');
            }
        }

        string normalized = builder.ToString().Trim('_', '.', '-');
        if (normalized.Length == 0) return "event";
        if (normalized[0] < 'a' || normalized[0] > 'z') normalized = "event_" + normalized;
        return normalized.Length <= 40 ? normalized : normalized.Substring(0, 40).TrimEnd('_', '.', '-');
    }

    internal static JObject ToEventRecord(WorldEventRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        if (!CanProject(record)) throw new InvalidOperationException("World event cannot satisfy event-record.v1.");

        return new JObject
        {
            ["schemaVersion"] = "awake.worldbook.event-record.v1",
            ["eventId"] = record.EventId,
            ["eventKey"] = record.EventKey,
            ["occurredAt"] = record.OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["eventType"] = record.Kind,
            ["domain"] = record.Domain,
            ["visibility"] = new JObject
            {
                ["scope"] = "local",
                ["min_detail"] = "summary",
                ["identity_ids"] = new JArray(record.VisibilityIdentityIds)
            },
            ["facts"] = new JArray
            {
                new JObject
                {
                    ["factId"] = BuildFactId(record.EventId),
                    ["text"] = new JObject { ["zh-CN"] = record.Text }
                }
            },
            ["extensions"] = new JObject
            {
                ["awake:gameDay"] = record.Day,
                ["awake:projection"] = "ledger-v1-conservative-defaults"
            }
        };
    }

    internal static bool CanProject(WorldEventRecord record)
    {
        return record != null
            && IsStableId(record.EventId)
            && !string.IsNullOrWhiteSpace(record.Kind)
            && Domains.Contains(record.Domain)
            && !string.IsNullOrWhiteSpace(record.Text)
            && record.OccurredAt != default(DateTimeOffset);
    }

    internal static bool TryValidateEventRecord(JObject record, out string error)
    {
        error = string.Empty;
        if (record == null) return Fail("event record is null", out error);
        if (!HasOnlyProperties(record, "schemaVersion", "eventId", "eventKey", "occurredAt", "eventType", "domain", "actorIds", "locationIds", "visibility", "facts", "causationId", "extensions"))
            return Fail("event record contains unknown properties", out error);
        if (!StringComparer.Ordinal.Equals((string)record["schemaVersion"], "awake.worldbook.event-record.v1"))
            return Fail("event record schemaVersion mismatch", out error);
        if (!IsStableId((string)record["eventId"])) return Fail("event record eventId is invalid", out error);
        if (record["eventKey"] != null && ((string)record["eventKey"] ?? string.Empty).Length == 0)
            return Fail("event record eventKey is empty", out error);
        if (!DateTimeOffset.TryParse((string)record["occurredAt"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
            return Fail("event record occurredAt is invalid", out error);
        if (!Regex.IsMatch((string)record["eventType"] ?? string.Empty, "^[a-z][a-z0-9_.-]*$", RegexOptions.CultureInvariant))
            return Fail("event record eventType is invalid", out error);
        if (!Domains.Contains((string)record["domain"])) return Fail("event record domain is invalid", out error);

        JObject visibility = record["visibility"] as JObject;
        if (visibility == null
            || !HasOnlyProperties(visibility, "scope", "min_detail", "identity_ids")
            || !Scopes.Contains((string)visibility["scope"])
            || !DetailLevels.Contains((string)visibility["min_detail"])
            || !TryValidateStableIdArray(visibility["identity_ids"], out error))
            return Fail("event record visibility is invalid", out error);

        if (!TryValidateStableIdArray(record["actorIds"], out error) || !TryValidateStableIdArray(record["locationIds"], out error))
            return Fail("event record actor/location IDs are invalid", out error);
        if (record["causationId"] != null && record["causationId"].Type != JTokenType.Null && record["causationId"].Type != JTokenType.String)
            return Fail("event record causationId is invalid", out error);

        JArray facts = record["facts"] as JArray;
        if (facts == null || facts.Count == 0) return Fail("event record facts are empty", out error);
        HashSet<string> factIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JToken token in facts)
        {
            JObject fact = token as JObject;
            if (fact == null || !HasOnlyProperties(fact, "factId", "text", "entryId", "keywords") || !IsStableId((string)fact["factId"]) || !factIds.Add((string)fact["factId"]))
                return Fail("event record factId is invalid or duplicated", out error);
            JObject text = fact["text"] as JObject;
            if (text == null || !text.Properties().Any(property => !string.IsNullOrWhiteSpace((string)property.Value)))
                return Fail("event record fact text is empty", out error);
            if (fact["entryId"] != null && !IsStableId((string)fact["entryId"]))
                return Fail("event record entryId is invalid", out error);
            if (!TryValidateStringArray(fact["keywords"], out error))
                return Fail("event record keywords are invalid", out error);
        }

        return true;
    }

    internal static bool TryValidateWeeklyReport(JObject report, out string error)
    {
        error = string.Empty;
        if (report == null) return Fail("weekly report is null", out error);
        if (StringComparer.Ordinal.Equals((string)report["schemaVersion"], WeeklyReportService.V2SchemaVersion))
            return WeeklyReportService.TryValidateV2Report(report, out error);
        if (!HasOnlyProperties(report, "schemaVersion", "reportId", "period", "generatedBy", "sourceEventIds", "sections", "visibility", "extensions"))
            return Fail("weekly report contains unknown properties", out error);
        if (!StringComparer.Ordinal.Equals((string)report["schemaVersion"], "awake.worldbook.weekly-report.v1"))
            return Fail("weekly report schemaVersion mismatch", out error);
        if (!IsStableId((string)report["reportId"])) return Fail("weekly report reportId is invalid", out error);
        if (!StringComparer.Ordinal.Equals((string)report["generatedBy"], "awake:system:weekly-report-generator"))
            return Fail("weekly report generatedBy is invalid", out error);

        if (report["visibility"] != null)
        {
            JObject visibility = report["visibility"] as JObject;
            if (visibility == null
                || !HasOnlyProperties(visibility, "scope", "min_detail", "identity_ids")
                || !Scopes.Contains((string)visibility["scope"])
                || !DetailLevels.Contains((string)visibility["min_detail"])
                || !TryValidateStableIdArray(visibility["identity_ids"], out error))
                return Fail("weekly report visibility is invalid", out error);
        }

        JObject period = report["period"] as JObject;
        if (period == null
            || !HasOnlyProperties(period, "start", "end")
            || !DateTimeOffset.TryParse((string)period["start"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset start)
            || !DateTimeOffset.TryParse((string)period["end"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset end)
            || end <= start)
            return Fail("weekly report period is invalid", out error);

        HashSet<string> sourceEventIds = new HashSet<string>(StringComparer.Ordinal);
        if (report["sourceEventIds"] is JArray sourceIds)
        {
            foreach (JToken token in sourceIds)
            {
                string sourceId = (string)token;
                if (!IsStableId(sourceId) || !sourceEventIds.Add(sourceId))
                    return Fail("weekly report sourceEventIds are invalid or duplicated", out error);
            }
        }

        JArray sections = report["sections"] as JArray;
        if (sections == null) return Fail("weekly report sections are missing", out error);
        HashSet<string> sectionIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> renderedEventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JToken sectionToken in sections)
        {
            JObject section = sectionToken as JObject;
            if (section == null || !HasOnlyProperties(section, "sectionId", "domain", "title", "items") || !IsStableId((string)section["sectionId"]) || !sectionIds.Add((string)section["sectionId"]))
                return Fail("weekly report sectionId is invalid or duplicated", out error);
            if (!Domains.Contains((string)section["domain"])) return Fail("weekly report section domain is invalid", out error);
            if (!HasLocalizedText(section["title"] as JObject)) return Fail("weekly report section title is empty", out error);
            if (!(section["items"] is JArray items)) return Fail("weekly report section items are missing", out error);
            foreach (JToken itemToken in items)
            {
                JObject item = itemToken as JObject;
                if (item == null || !HasOnlyProperties(item, "itemId", "text", "sourceEventIds", "entryId") || !IsStableId((string)item["itemId"]) || !itemIds.Add((string)item["itemId"]))
                return Fail("weekly report itemId is invalid or duplicated", out error);
                if (!HasLocalizedText(item["text"] as JObject)) return Fail("weekly report item text is empty", out error);
                if (!(item["sourceEventIds"] is JArray itemSources) || itemSources.Count == 0)
                    return Fail("weekly report item sourceEventIds are empty", out error);
                HashSet<string> itemSourceIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (JToken itemSourceToken in itemSources)
                {
                    string itemSourceId = (string)itemSourceToken;
                    if (!IsStableId(itemSourceId) || !itemSourceIds.Add(itemSourceId) || !sourceEventIds.Contains(itemSourceId))
                        return Fail("weekly report item source closure is invalid", out error);
                    renderedEventIds.Add(itemSourceId);
                }
                if (item["entryId"] != null && !IsStableId((string)item["entryId"]))
                    return Fail("weekly report entryId is invalid", out error);
            }
        }

        if (!sourceEventIds.SetEquals(renderedEventIds)) return Fail("weekly report source closure is incomplete", out error);
        return true;
    }

    private static string BuildFactId(string eventId)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(eventId ?? string.Empty));
            StringBuilder hex = new StringBuilder(12);
            for (int index = 0; index < 6; index++) hex.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            return "awake:fact:event-" + hex;
        }
    }

    private static bool HasLocalizedText(JObject value)
    {
        return value != null && value.Properties().Any(property => !string.IsNullOrWhiteSpace((string)property.Value));
    }

    private static bool HasOnlyProperties(JObject value, params string[] allowedNames)
    {
        if (value == null) return false;
        HashSet<string> allowed = new HashSet<string>(allowedNames ?? new string[0], StringComparer.Ordinal);
        return value.Properties().All(property => allowed.Contains(property.Name));
    }

    private static bool TryValidateStableIdArray(JToken value, out string error)
    {
        error = string.Empty;
        if (value == null) return true;
        if (!(value is JArray array)) return Fail("stable ID field is not an array", out error);
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JToken token in array)
        {
            string id = (string)token;
            if (!IsStableId(id) || !ids.Add(id)) return Fail("stable ID array contains an invalid or duplicate value", out error);
        }
        return true;
    }

    private static bool TryValidateStringArray(JToken value, out string error)
    {
        error = string.Empty;
        if (value == null) return true;
        if (!(value is JArray array)) return Fail("string field is not an array", out error);
        HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
        foreach (JToken token in array)
        {
            string text = (string)token;
            if (string.IsNullOrWhiteSpace(text) || !values.Add(text)) return Fail("string array contains an invalid or duplicate value", out error);
        }
        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
