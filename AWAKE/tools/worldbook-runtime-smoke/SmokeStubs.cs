using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldbookQuery
{
    internal string HeroId { get; set; } = string.Empty;
    internal string CharacterId { get; set; } = string.Empty;
    internal string IdentityId { get; set; } = string.Empty;
    internal string CultureId { get; set; } = string.Empty;
    internal string KingdomId { get; set; } = string.Empty;
    internal string SettlementId { get; set; } = string.Empty;
    internal string Role { get; set; } = string.Empty;
    internal bool? IsFemale { get; set; }
    internal int Age { get; set; }
    internal bool IsClanLeader { get; set; }
    internal Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
    internal string ContentTier { get; set; } = "pure";
    internal string KnowledgeScope { get; set; } = string.Empty;
    internal bool KnowledgeScopeAvailable { get; set; }
    internal string EffectiveDetail { get; set; } = string.Empty;
    internal bool EffectiveDetailAvailable { get; set; }
    internal string RequestedDetail { get; set; } = "secret";
    internal string PlayerText { get; set; } = string.Empty;
    internal int MaximumBytes { get; set; } = 4096;
}

internal sealed class WorldbookMappingContext
{
}

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

    internal WorldStateStore Reopen()
    {
        using (JsonTextReader reader = new JsonTextReader(new StringReader(Document.ToString(Newtonsoft.Json.Formatting.None))))
        {
            reader.DateParseHandling = DateParseHandling.None;
            WorldStateStore reopened = new WorldStateStore((JObject)JToken.ReadFrom(reader));
            reopened.ReportWriteAttempts = ReportWriteAttempts;
            return reopened;
        }
    }

    internal WorldStateStore()
    {
    }

    private WorldStateStore(JObject document)
    {
        Document = document ?? NewState();
    }

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
            string schemaVersion = (string)value["schemaVersion"] ?? (string)report?["schemaVersion"] ?? string.Empty;
            bool corrupt = false;
            if (schemaVersion == "awake.worldbook.weekly-report.v2")
            {
                if (!IsValidV2Entry(value, report)) { report = null; corrupt = true; }
            }
            else if (report != null && !StringComparer.Ordinal.Equals((string)report["reportId"], (string)value["reportId"]))
                report = null;
            result.Add(new WeeklyReportApplicationState
            {
                ReportId = (string)value["reportId"] ?? string.Empty,
                SchemaVersion = schemaVersion,
                WindowStartDay = (int?)value["windowStartDay"] ?? 0,
                WindowEndDay = (int?)value["windowEndDay"] ?? 0,
                Status = (string)value["status"] ?? "retryable",
                AttemptCount = (int?)value["attemptCount"] ?? 0,
                LastAttemptDay = (int?)value["lastAttemptDay"] ?? 0,
                LastErrorCode = (string)value["lastErrorCode"] ?? string.Empty,
                ContentFingerprint = (string)value["contentFingerprint"] ?? string.Empty,
                SourceFactIds = ((JArray)value["sourceFactIds"] ?? new JArray()).Values<string>().ToArray(),
                Corrupt = corrupt,
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
        if (report != null && StringComparer.Ordinal.Equals((string)report["schemaVersion"], "awake.worldbook.weekly-report.v2"))
            return UpsertV2WeeklyReportStateAsync(report, reportId, windowStartDay, windowEndDay, lastAttemptDay, lastErrorCode);
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

    private Task<WeeklyReportStateWriteResult> UpsertV2WeeklyReportStateAsync(JObject report, string reportId, int windowStartDay, int windowEndDay, int lastAttemptDay, string lastErrorCode)
    {
        if (FailReportWrites)
            return Task.FromResult(new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Retryable, Code = "smoke.report.write_failed" });
        if (!StringComparer.Ordinal.Equals((string)report["reportId"], reportId) || !WeeklyReportService.TryValidateV2Report(report, out _))
            return Task.FromResult(new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Failed, Code = "awake.world_report.v2.invalid_payload" });
        EnsureShape();
        JArray reports = (JArray)Document["weeklyReports"];
        JObject current = reports.Children<JObject>().FirstOrDefault(value => StringComparer.Ordinal.Equals((string)value["reportId"], reportId));
        if (current != null)
        {
            string schemaVersion = (string)current["schemaVersion"] ?? (string)current["report"]?["schemaVersion"] ?? string.Empty;
            if (schemaVersion == "awake.worldbook.weekly-report.v1")
                return Task.FromResult(new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Conflict, Code = "awake.world_report.v2.v1_conflict" });
            if (schemaVersion == "awake.worldbook.weekly-report.v2" && IsValidV2Entry(current, current["report"] as JObject))
            {
                if (StringComparer.Ordinal.Equals((string)current["contentFingerprint"], (string)report["contentFingerprint"]))
                    return Task.FromResult(new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.AlreadyApplied, Attempts = (int?)current["attemptCount"] ?? 0, Report = (JObject)current["report"].DeepClone() });
                return Task.FromResult(new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Conflict, Code = "awake.world_report.v2.conflict" });
            }
        }
        int attempt = ((int?)current?["attemptCount"] ?? 0) + 1;
        if (current == null) { current = new JObject(); reports.Add(current); }
        current.RemoveAll();
        current["reportId"] = reportId;
        current["schemaVersion"] = "awake.worldbook.weekly-report.v2";
        current["windowStartDay"] = windowStartDay;
        current["windowEndDay"] = windowEndDay;
        current["status"] = "applied";
        current["attemptCount"] = attempt;
        current["lastAttemptDay"] = lastAttemptDay;
        current["lastErrorCode"] = lastErrorCode ?? string.Empty;
        current["contentFingerprint"] = report["contentFingerprint"];
        current["sourceFactIds"] = report["sourceFactIds"].DeepClone();
        current["report"] = report.DeepClone();
        ReportWriteAttempts++;
        return Task.FromResult(new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Applied, Attempts = attempt, Report = (JObject)report.DeepClone() });
    }

    private static bool IsEmptyWeeklyReport(JObject report)
    {
        return report != null
            && report["sourceEventIds"] is JArray sourceEventIds
            && sourceEventIds.Count == 0;
    }

    private static bool IsValidV2Entry(JObject entry, JObject report)
    {
        return entry != null && report != null
            && WeeklyReportService.TryValidateV2Report(report, out _)
            && StringComparer.Ordinal.Equals((string)entry["reportId"], (string)report["reportId"])
            && StringComparer.Ordinal.Equals((string)entry["schemaVersion"], (string)report["schemaVersion"])
            && (int?)entry["windowStartDay"] == (int?)report["extensions"]?["awake:windowStartDay"]
            && (int?)entry["windowEndDay"] == (int?)report["extensions"]?["awake:windowEndDay"]
            && StringComparer.Ordinal.Equals((string)entry["contentFingerprint"], (string)report["contentFingerprint"])
            && JToken.DeepEquals(entry["sourceFactIds"], report["sourceFactIds"]);
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
