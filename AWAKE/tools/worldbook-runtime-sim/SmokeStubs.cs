using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

// 注意：WorldbookQuery / WorldbookMappingContext 已改由真实 src\WorldbookModels.cs 提供
// （本工程 2026-09-14 起编译该文件，以支撑 PersonaDataLoader / PersonaRoster 的目录扫描）。
// 此前的精简替身已删除，避免与真实类型重名。

internal static class AwakeRuntime
{
    internal static WorldStateStore WorldStateStore { get; set; }
    internal static bool SessionEnded { get; set; }
    internal static bool NativeKnowledgeReady { get; set; } = true;

    internal static bool IsNativeKnowledgeReady()
    {
        return !SessionEnded && NativeKnowledgeReady;
    }

    internal static void ResetSessionStateForCampaignCore()
    {
        WorldStateStore = null;
        SessionEnded = false;
        NativeKnowledgeReady = true;
    }

    internal static string TruncateTextElements(string value, int maximumElements)
    {
        if (string.IsNullOrEmpty(value) || maximumElements <= 0) return string.Empty;
        return value.Length <= maximumElements ? value : value.Substring(0, maximumElements);
    }
}

internal sealed class WorldStateStore
{
    internal JObject Document { get; set; } = NewState();
    internal string LastDomain { get; private set; }
    internal DateTimeOffset LastOccurredAt { get; private set; }
    internal bool FailEventWrites { get; set; }
    internal bool ThrowEventWrites { get; set; }
    internal TaskCompletionSource<WorldEventAppendResult> PendingEventWrite { get; set; }
    internal TaskCompletionSource<bool> EventWriteStarted { get; set; }
    internal TaskCompletionSource<JObject> PendingWorldEventsRead { get; set; }
    internal TaskCompletionSource<bool> WorldEventsReadStarted { get; set; }
    internal bool FailReportWrites { get; set; }
    internal bool FailReads { get; set; }
    internal WorldFactJournalReadResult Journal { get; set; }
    internal int EventWriteAttempts { get; private set; }
    internal int ReportWriteAttempts { get; private set; }

    internal Task<JObject> GetWorldEventsAsync(object context, CancellationToken cancellationToken)
    {
        WorldEventsReadStarted?.TrySetResult(true);
        if (PendingWorldEventsRead != null) return PendingWorldEventsRead.Task;
        return Task.FromResult(FailReads ? null : Document ?? NewState());
    }

    internal Task<WorldFactJournalReadResult> GetWorldFactJournalAsync(object context, CancellationToken cancellationToken)
    {
        return Task.FromResult(Journal ?? new WorldFactJournalReadResult(WorldFactJournalReadStatus.Missing));
    }

    internal Task<List<WeeklyReportApplicationState>> GetWeeklyReportStatesAsync(CancellationToken cancellationToken)
    {
        if (FailReads) return Task.FromResult<List<WeeklyReportApplicationState>>(null);
        var result = new List<WeeklyReportApplicationState>();
        foreach (JObject value in ((JArray)Document["weeklyReports"] ?? new JArray()).Children<JObject>())
        {
            JObject report = value["report"] as JObject;
            if (report != null && !StringComparer.Ordinal.Equals((string)report["reportId"], (string)value["reportId"]))
                report = null;
            result.Add(new WeeklyReportApplicationState
            {
                ReportId = (string)value["reportId"] ?? string.Empty,
                WindowStartDay = (int?)value["windowStartDay"] ?? 0,
                WindowEndDay = (int?)value["windowEndDay"] ?? 0,
                Status = (string)value["status"] ?? "retryable",
                AttemptCount = (int?)value["attemptCount"] ?? 0,
                LastAttemptDay = (int?)value["lastAttemptDay"] ?? 0,
                LastErrorCode = (string)value["lastErrorCode"] ?? string.Empty,
                Report = report == null ? null : (JObject)report.DeepClone()
            });
        }
        return Task.FromResult(result);
    }

    internal Task<WorldEventAppendResult> AppendWorldEventAsync(int day, string kind, string text, string idempotencyKey, string eventKey, string domain, DateTimeOffset occurredAt, IReadOnlyList<string> visibilityIdentityIds, CancellationToken cancellationToken, JObject structuredFact = null)
    {
        EventWriteAttempts++;
        LastDomain = domain;
        LastOccurredAt = occurredAt;
        EventWriteStarted?.TrySetResult(true);
        if (PendingEventWrite != null) return PendingEventWrite.Task;
        if (ThrowEventWrites) throw new InvalidOperationException("smoke event write exception");
        if (FailEventWrites)
        {
            return Task.FromResult(new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.PersistenceRetryable,
                EventId = idempotencyKey,
                EventKey = eventKey,
                Code = "smoke.event.write_failed"
            });
        }
        EnsureShape();
        JArray records = (JArray)Document["records"];
        JObject existing = records.Children<JObject>().FirstOrDefault(value => StringComparer.Ordinal.Equals((string)value["id"], idempotencyKey) || StringComparer.Ordinal.Equals((string)value["eventKey"], eventKey));
        if (existing != null)
        {
            bool same = (int?)existing["day"] == day
                && StringComparer.Ordinal.Equals((string)existing["kind"], kind)
                && StringComparer.Ordinal.Equals((string)existing["text"], text)
                && StringComparer.Ordinal.Equals((string)existing["eventKey"], eventKey)
                && StringComparer.Ordinal.Equals((string)existing["domain"], domain)
                && StringComparer.Ordinal.Equals((string)existing["occurredAt"], occurredAt.ToUniversalTime().ToString("O"))
                && new HashSet<string>(((JArray)existing["visibilityIdentityIds"] ?? new JArray()).Values<string>(), StringComparer.Ordinal)
                    .SetEquals(WorldEventAudience.Resolve(visibilityIdentityIds))
                && ((existing["fact"] == null || existing["fact"].Type == JTokenType.Null) == (structuredFact == null)
                    && (structuredFact == null || JToken.DeepEquals(existing["fact"], structuredFact)));
            return Task.FromResult(new WorldEventAppendResult
            {
                Status = same ? WorldEventAppendResult.DuplicateConfirmed : WorldEventAppendResult.KeyConflict,
                EventId = idempotencyKey,
                EventKey = eventKey,
                Code = same ? string.Empty : "smoke.event.key_conflict",
                Record = same ? new WorldEventRecord(idempotencyKey, day, kind, domain, text, occurredAt, eventKey, visibilityIdentityIds, structuredFact) : null
            });
        }
        records.Insert(0, new JObject
        {
            ["id"] = idempotencyKey,
            ["day"] = day,
            ["kind"] = kind,
            ["text"] = text,
            ["eventKey"] = eventKey,
            ["domain"] = domain,
            ["occurredAt"] = occurredAt.ToUniversalTime().ToString("O"),
            ["visibilityIdentityIds"] = new JArray(WorldEventAudience.Resolve(visibilityIdentityIds).Select(value => (object)value)),
            ["fact"] = structuredFact == null ? null : structuredFact.DeepClone()
        });
        return Task.FromResult(new WorldEventAppendResult { Status = WorldEventAppendResult.Persisted, EventId = idempotencyKey, EventKey = eventKey });
    }

    internal Task<WorldEventAppendResult> AppendWorldEventAsync(int day, string kind, string text, string idempotencyKey, string eventKey, string domain, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        return AppendWorldEventAsync(day, kind, text, idempotencyKey, eventKey, domain, occurredAt, null, cancellationToken);
    }

    internal Task<WeeklyReportStateWriteResult> UpsertWeeklyReportStateAsync(string reportId, int windowStartDay, int windowEndDay, string status, int lastAttemptDay, string lastErrorCode, JObject report, CancellationToken cancellationToken)
    {
        ReportWriteAttempts++;
        if (FailReportWrites)
        {
            return Task.FromResult(new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Retryable, Code = "smoke.report.write_failed" });
        }
        EnsureShape();
        JArray reports = (JArray)Document["weeklyReports"];
        JObject current = reports.Children<JObject>().FirstOrDefault(value => StringComparer.Ordinal.Equals((string)value["reportId"], reportId));
        bool currentApplied = StringComparer.Ordinal.Equals((string)current?["status"], "applied");
        bool currentSnapshotValid = currentApplied
            && current["report"] is JObject existingSnapshot
            && StringComparer.Ordinal.Equals((string)existingSnapshot["reportId"], reportId)
            && WorldEventContract.TryValidateWeeklyReport(existingSnapshot, out _);
        if (currentApplied)
        {
            if (currentSnapshotValid)
                return Task.FromResult(new WeeklyReportStateWriteResult
                {
                    Status = WeeklyReportStateWriteResult.AlreadyApplied,
                    Attempts = (int?)current["attemptCount"] ?? 0,
                    Report = (JObject)current["report"].DeepClone()
                });
            if (IsEmptyWeeklyReport(report))
                return Task.FromResult(new WeeklyReportStateWriteResult
                {
                    Status = WeeklyReportStateWriteResult.Retryable,
                    Attempts = (int?)current["attemptCount"] ?? 0,
                    Code = "awake.world_state.weekly_report.snapshot_unrecoverable"
                });
            if (report != null && StringComparer.Ordinal.Equals((string)report["reportId"], reportId))
            {
                current["report"] = (JObject)report.DeepClone();
                return Task.FromResult(new WeeklyReportStateWriteResult
                {
                    Status = WeeklyReportStateWriteResult.Applied,
                    Attempts = (int?)current["attemptCount"] ?? 0,
                    Report = (JObject)report.DeepClone()
                });
            }
            return Task.FromResult(new WeeklyReportStateWriteResult
            {
                Status = WeeklyReportStateWriteResult.AlreadyApplied,
                Attempts = (int?)current["attemptCount"] ?? 0,
                Report = null
            });
        }
        int attempt = ((int?)current?["attemptCount"] ?? 0) + 1;
        if (current == null)
        {
            current = new JObject { ["reportId"] = reportId };
            reports.Add(current);
        }
        current["windowStartDay"] = windowStartDay;
        current["windowEndDay"] = windowEndDay;
        current["status"] = status;
        current["attemptCount"] = attempt;
        current["lastAttemptDay"] = lastAttemptDay;
        current["lastErrorCode"] = lastErrorCode ?? string.Empty;
        current["report"] = report == null ? null : (JObject)report.DeepClone();
        return Task.FromResult(new WeeklyReportStateWriteResult { Status = status == "applied" ? WeeklyReportStateWriteResult.Applied : WeeklyReportStateWriteResult.Retryable, Attempts = attempt });
    }

    private static bool IsEmptyWeeklyReport(JObject report)
    {
        return report != null
            && report["sourceEventIds"] is JArray sourceEventIds
            && sourceEventIds.Count == 0;
    }

    internal Task<WeeklyReportStateWriteResult> UpsertWeeklyReportStateAsync(string reportId, int windowStartDay, int windowEndDay, string status, int lastAttemptDay, string lastErrorCode, CancellationToken cancellationToken)
    {
        return UpsertWeeklyReportStateAsync(reportId, windowStartDay, windowEndDay, status, lastAttemptDay, lastErrorCode, null, cancellationToken);
    }

    private static JObject NewState() => new JObject
    {
        ["schema"] = "awake.world_events.v1",
        ["records"] = new JArray(),
        ["appliedKeys"] = new JArray(),
        ["weeklyReports"] = new JArray()
    };

    private void EnsureShape()
    {
        if (!(Document["records"] is JArray)) Document["records"] = new JArray();
        if (!(Document["weeklyReports"] is JArray)) Document["weeklyReports"] = new JArray();
        if (!(Document["appliedKeys"] is JArray)) Document["appliedKeys"] = new JArray();
    }
}

internal static class AwakeBackgroundTask
{
    internal static Task LastTask { get; private set; }

    internal static void Run(Func<Task> taskFactory, string label)
    {
        LastTask = taskFactory?.Invoke();
    }
}

internal static class AwakeLog
{
    internal static void Write(string message)
    {
    }
}
