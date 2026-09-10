using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldEventRecord
{
    internal string EventId { get; }
    internal string EventKey { get; }
    internal int Day { get; }
    internal string Kind { get; }
    internal string Domain { get; }
    internal string Text { get; }
    internal DateTimeOffset OccurredAt { get; }
    internal IReadOnlyList<string> VisibilityIdentityIds { get; }

    internal WorldEventRecord(int day, string kind, string text)
        : this(day, kind, text, null, null)
    {
    }

    internal WorldEventRecord(int day, string kind, string text, string eventKey)
        : this(day, kind, text, eventKey, null)
    {
    }

    internal WorldEventRecord(int day, string kind, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds)
        : this(BuildEventId(day, kind, text, eventKey), day, kind, WeeklyReportService.InferDomain(kind), text, new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day), eventKey, visibilityIdentityIds)
    {
    }

    internal WorldEventRecord(string eventId, int day, string kind, string domain, string text)
        : this(eventId, day, kind, domain, text, new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day), null, null)
    {
    }

    internal WorldEventRecord(string eventId, int day, string kind, string domain, string text, DateTimeOffset occurredAt, string eventKey = null)
        : this(eventId, day, kind, domain, text, occurredAt, eventKey, null)
    {
    }

    internal WorldEventRecord(string eventId, int day, string kind, string domain, string text, DateTimeOffset occurredAt, string eventKey, IReadOnlyList<string> visibilityIdentityIds)
    {
        string normalizedEventId = string.IsNullOrWhiteSpace(eventId) || !WorldEventContract.IsStableId(eventId.Trim())
            ? BuildEventId(day, kind, text, eventKey)
            : eventId.Trim();
        EventId = normalizedEventId;
        EventKey = string.IsNullOrWhiteSpace(eventKey) ? EventId : eventKey.Trim();
        Day = day;
        Kind = WorldEventContract.NormalizeEventType(kind);
        Domain = WeeklyReportService.NormalizeDomain(domain, Kind);
        Text = text ?? string.Empty;
        OccurredAt = occurredAt;
        VisibilityIdentityIds = WorldEventAudience.Resolve(visibilityIdentityIds);
    }

    private static string BuildEventId(int day, string kind, string text, string eventKey)
    {
        using (SHA256 sha = SHA256.Create())
        {
            bool explicitKey = !string.IsNullOrWhiteSpace(eventKey);
            string identity = explicitKey
                ? "key|" + eventKey.Trim()
                : "payload|" + day + "|" + (kind ?? string.Empty) + "|" + (text ?? string.Empty);
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
            StringBuilder hex = new StringBuilder(12);
            for (int i = 0; i < 6; i++) hex.Append(bytes[i].ToString("x2"));
            string safeKind = WorldEventContract.NormalizeEventType(kind);
            return explicitKey ? "awake:event:key-" + hex : "awake:event:day-" + day + "-" + safeKind + "-" + hex;
        }
    }
}

internal sealed class WorldEventLedgerSnapshot
{
    internal int CampaignGeneration { get; }
    internal long LedgerRevision { get; }
    internal long SnapshotRevision { get; }
    internal IReadOnlyList<WorldEventRecord> Records { get; }

    internal WorldEventLedgerSnapshot(int campaignGeneration, long ledgerRevision, IReadOnlyList<WorldEventRecord> records)
        : this(campaignGeneration, ledgerRevision, ledgerRevision, records)
    {
    }

    internal WorldEventLedgerSnapshot(int campaignGeneration, long ledgerRevision, long snapshotRevision, IReadOnlyList<WorldEventRecord> records)
    {
        CampaignGeneration = campaignGeneration;
        LedgerRevision = ledgerRevision;
        SnapshotRevision = snapshotRevision;
        Records = records ?? new List<WorldEventRecord>();
    }
}

internal static class WorldEventLedger
{
    private const int Capacity = 50;
    private const int SeenEventCapacity = 200;
    private const long LoadRetryIntervalMilliseconds = 10000;
    private static readonly Queue<WorldEventRecord> Records = new Queue<WorldEventRecord>();
    private static readonly Queue<string> SeenEventOrder = new Queue<string>();
    private static readonly Queue<string> SeenEventKeyOrder = new Queue<string>();
    private static readonly HashSet<string> SeenEventIds = new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> SeenEventKeys = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> PendingEventIds = new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> PendingEventKeys = new Dictionary<string, int>(StringComparer.Ordinal);
    private static bool _loaded;
    private static WorldStateStore _loadedStore;
    private static long _lastLoadAttemptUtcTicks;
    private static int _campaignGeneration;
    private static long _ledgerRevision;
    private static long _snapshotRevision;
    internal static Func<long> UtcTicksProviderForTesting { get; set; }

    internal static int CampaignGeneration
    {
        get { return Volatile.Read(ref _campaignGeneration); }
    }

    internal static bool IsCurrentCampaignGeneration(int generation)
    {
        return Volatile.Read(ref _campaignGeneration) == generation;
    }

    internal static long LedgerRevision
    {
        get { return Interlocked.Read(ref _ledgerRevision); }
    }

    internal static WorldEventLedgerSnapshot CaptureSnapshot()
    {
        lock (Records)
        {
            return CreateSnapshotLocked();
        }
    }

    internal static int Count
    {
        get { lock (Records) return Records.Count; }
    }

    internal static bool QueueRecord(int day, string kind, string text, string eventKey = null)
    {
        return QueueRecord(day, kind, text, eventKey, null);
    }

    internal static bool QueueRecord(int day, string kind, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds)
    {
        WorldEventRecord record = CreateRecord(day, kind, text, eventKey, visibilityIdentityIds);
        if (record == null) return false;
        int sessionGeneration = CampaignGeneration;
        AwakeBackgroundTask.Run(
            () => RecordInBackgroundAsync(day, kind, text, eventKey, visibilityIdentityIds, sessionGeneration),
            "world_event_record");
        return true;
    }

    private static async Task RecordInBackgroundAsync(int day, string kind, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds, int sessionGeneration)
    {
        WorldEventAppendResult result = await RecordAsync(day, kind, text, eventKey, visibilityIdentityIds, sessionGeneration, CancellationToken.None).ConfigureAwait(false);
        AwakeLog.Write("world_event_record_status status=" + result.Status + " event=" + result.EventId + " code=" + result.Code);
    }

    internal static Task<WorldEventAppendResult> RecordAsync(
        int day,
        string kind,
        string text,
        string eventKey,
        CancellationToken cancellationToken)
    {
        return RecordAsync(day, kind, text, eventKey, null, CampaignGeneration, cancellationToken);
    }

    internal static Task<WorldEventAppendResult> RecordAsync(
        int day,
        string kind,
        string text,
        string eventKey,
        IReadOnlyList<string> visibilityIdentityIds,
        CancellationToken cancellationToken)
    {
        return RecordAsync(day, kind, text, eventKey, visibilityIdentityIds, CampaignGeneration, cancellationToken);
    }

    internal static Task<WorldEventAppendResult> RecordAsyncForCampaign(
        int day,
        string kind,
        string text,
        string eventKey,
        IReadOnlyList<string> visibilityIdentityIds,
        int sessionGeneration,
        CancellationToken cancellationToken)
    {
        return RecordAsync(day, kind, text, eventKey, visibilityIdentityIds, sessionGeneration, cancellationToken);
    }

    private static async Task<WorldEventAppendResult> RecordAsync(
        int day,
        string kind,
        string text,
        string eventKey,
        IReadOnlyList<string> visibilityIdentityIds,
        int sessionGeneration,
        CancellationToken cancellationToken)
    {
        WorldEventRecord record = CreateRecord(day, kind, text, eventKey, visibilityIdentityIds);
        if (record == null)
        {
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.PersistenceFailed,
                Code = "awake.world_event.invalid"
            };
        }
        if (!IsCurrentCampaignGeneration(sessionGeneration))
            return StaleSessionResult(record);

        lock (Records)
        {
            if (_campaignGeneration != sessionGeneration) return StaleSessionResult(record);
            WorldEventRecord existing = FindExisting(record);
            if (existing != null)
            {
                return new WorldEventAppendResult
                {
                    Status = SameContent(existing, record) ? WorldEventAppendResult.DuplicateConfirmed : WorldEventAppendResult.KeyConflict,
                    EventId = existing.EventId,
                    EventKey = existing.EventKey,
                    Record = existing,
                    Code = SameContent(existing, record) ? string.Empty : "awake.world_event.key_conflict"
                };
            }
            if (PendingEventIds.ContainsKey(record.EventId) || PendingEventKeys.ContainsKey(record.EventKey))
            {
                return new WorldEventAppendResult
                {
                    Status = WorldEventAppendResult.PersistenceUnknown,
                    EventId = record.EventId,
                    EventKey = record.EventKey,
                    Code = "awake.world_event.pending"
                };
            }
            PendingEventIds[record.EventId] = sessionGeneration;
            PendingEventKeys[record.EventKey] = sessionGeneration;
        }

        WorldEventAppendResult result;
        WorldStateStore store = WorldEventServices.CaptureWorldStateStore(sessionGeneration);
        if (store == null)
        {
            lock (Records)
            {
                RemovePendingIfOwned(record, sessionGeneration);
                if (_campaignGeneration != sessionGeneration)
                    return StaleSessionResult(record);
            }
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.PersistenceRetryable,
                EventId = record.EventId,
                EventKey = record.EventKey,
                Code = "awake.world_event.storage_unavailable"
            };
        }
        else
        {
            try
            {
                result = await store.AppendWorldEventAsync(
                    day,
                    record.Kind,
                    record.Text,
                    record.EventId,
                    record.EventKey,
                    record.Domain,
                    record.OccurredAt,
                    record.VisibilityIdentityIds,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (Records)
                {
                    RemovePendingIfOwned(record, sessionGeneration);
                }
                throw;
            }
            catch (Exception ex)
            {
                AwakeLog.Write("world_event_persist_error error=" + ex.Message);
                result = new WorldEventAppendResult
                {
                    Status = WorldEventAppendResult.PersistenceUnknown,
                    EventId = record.EventId,
                    EventKey = record.EventKey,
                    Code = "awake.world_event.persistence_exception"
                };
            }
        }

        WorldEventLedgerSnapshot projectionSnapshot = null;
        bool currentSession = WorldEventServices.WithCampaignBoundary(() =>
        {
            if (!WorldEventServices.IsCurrentWorldStateStore(sessionGeneration, store)) return false;
            lock (Records)
            {
                if (_campaignGeneration != sessionGeneration) return false;
                RemovePendingIfOwned(record, sessionGeneration);
                if (result == null)
                {
                    result = new WorldEventAppendResult
                    {
                        Status = WorldEventAppendResult.PersistenceUnknown,
                        EventId = record.EventId,
                        EventKey = record.EventKey,
                        Code = "awake.world_event.empty_result"
                    };
                }
                if (result.Succeeded)
                {
                    WorldEventRecord accepted = result.Record ?? record;
                    WorldEventRecord existing = FindExisting(accepted);
                    if (existing == null && TryRemember(accepted))
                    {
                        Records.Enqueue(accepted);
                        while (Records.Count > Capacity) Records.Dequeue();
                        result.Record = accepted;
                        _ledgerRevision++;
                    }
                    else if (existing != null)
                    {
                        result.Record = existing;
                        result.Status = WorldEventAppendResult.DuplicateConfirmed;
                    }
                }
                projectionSnapshot = CreateSnapshotLocked();
                return true;
            }
        });
        if (!currentSession)
        {
            lock (Records)
            {
                RemovePendingIfOwned(record, sessionGeneration);
            }
            return StaleSessionResult(record);
        }
        if (result.Succeeded)
            WorldEventServices.TryProjectEventsIfReady(sessionGeneration, store, projectionSnapshot);
        return result;
    }

    internal static async Task LoadFromStoreAsync(CancellationToken cancellationToken)
    {
        int campaignGeneration;
        WorldStateStore store;
        lock (Records)
        {
            campaignGeneration = _campaignGeneration;
        }
        store = WorldEventServices.CaptureWorldStateStore(campaignGeneration);
        if (store == null)
        {
            return;
        }
        long loadStartRevision;
        lock (Records)
        {
            if (_campaignGeneration != campaignGeneration) return;
            if (_loaded && ReferenceEquals(_loadedStore, store)) return;
            long now = UtcTicksProviderForTesting == null ? DateTimeOffset.UtcNow.UtcTicks : UtcTicksProviderForTesting();
            if (now - _lastLoadAttemptUtcTicks < LoadRetryIntervalMilliseconds * TimeSpan.TicksPerMillisecond)
            {
                return;
            }
            _lastLoadAttemptUtcTicks = now;
            loadStartRevision = _ledgerRevision;
        }
        JObject doc = null;
        try
        {
            doc = await store.GetWorldEventsAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_event_load_error error=" + ex.Message);
            return;
        }
        if (doc == null) return;
        WorldEventLedgerSnapshot projectionSnapshot = null;
        bool currentSession = WorldEventServices.WithCampaignBoundary(() =>
        {
            if (!WorldEventServices.IsCurrentWorldStateStore(campaignGeneration, store)) return false;
            lock (Records)
            {
                if (_campaignGeneration != campaignGeneration) return false;
                bool revisionChangedWhileLoading = _ledgerRevision != loadStartRevision;
                if (revisionChangedWhileLoading) return false;
                Records.Clear();
                SeenEventOrder.Clear();
                SeenEventKeyOrder.Clear();
                SeenEventIds.Clear();
                SeenEventKeys.Clear();
                var loadedRecords = new List<WorldEventRecord>();
                if (doc?["records"] is JArray records)
                {
                    foreach (JToken token in records)
                    {
                        if (token is not JObject record) continue;
                        string eventId = (string)record["id"] ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(eventId)) continue;
                        string kind = (string)record["kind"] ?? "event";
                        string text = (string)record["text"] ?? string.Empty;
                        string domain = (string)record["domain"] ?? WeeklyReportService.InferDomain(kind);
                        string eventKey = (string)record["eventKey"] ?? eventId;
                        DateTimeOffset occurredAt = ParseOccurredAt((string)record["occurredAt"], IntValue(record["day"]));
                        WorldEventRecord loadedRecord = new WorldEventRecord(
                            eventId,
                            IntValue(record["day"]),
                            kind,
                            domain,
                            text,
                            occurredAt,
                            eventKey,
                            ReadVisibilityIdentityIds(record["visibilityIdentityIds"]));
                        if (!TryRemember(loadedRecord)) continue;
                        loadedRecords.Add(loadedRecord);
                    }
                }
                foreach (WorldEventRecord loadedRecord in loadedRecords
                    .OrderBy(record => record.Day)
                    .ThenBy(record => record.OccurredAt)
                    .ThenBy(record => record.EventId, StringComparer.Ordinal))
                {
                    Records.Enqueue(loadedRecord);
                    while (Records.Count > Capacity) Records.Dequeue();
                }
                _loaded = true;
                _loadedStore = store;
                _ledgerRevision++;
                projectionSnapshot = CreateSnapshotLocked();
                return true;
            }
        });
        if (currentSession)
            WorldEventServices.TryProjectEventsIfReady(campaignGeneration, store, projectionSnapshot);
    }

    internal static string FormatWeek(int nowDay)
    {
        return WorldEventServices.Reports.BuildText(SnapshotWeek(nowDay), nowDay);
    }

    internal static List<WorldEventRecord> SnapshotWeek(int nowDay)
    {
        return SnapshotWindow(nowDay - 6, nowDay);
    }

    internal static List<WorldEventRecord> SnapshotWindow(int startDay, int endDay)
    {
        lock (Records)
        {
            return Records.Where(record => record.Day >= startDay && record.Day <= endDay).ToList();
        }
    }

    internal static List<WorldEventRecord> SnapshotAll()
    {
        return CaptureSnapshot().Records.ToList();
    }

    internal static void ClearForTesting()
    {
        WorldEventServices.ResetForCampaign();
    }

    internal static void ResetForCampaign()
    {
        WorldEventServices.WithCampaignBoundary(() =>
        {
            lock (Records)
            {
                Records.Clear();
                SeenEventOrder.Clear();
                SeenEventKeyOrder.Clear();
                SeenEventIds.Clear();
                SeenEventKeys.Clear();
                PendingEventIds.Clear();
                PendingEventKeys.Clear();
                _loaded = false;
                _loadedStore = null;
                _lastLoadAttemptUtcTicks = 0;
                Interlocked.Increment(ref _campaignGeneration);
                _ledgerRevision++;
                UtcTicksProviderForTesting = null;
            }
        });
    }

    private static WorldEventLedgerSnapshot CreateSnapshotLocked()
    {
        return new WorldEventLedgerSnapshot(_campaignGeneration, _ledgerRevision, ++_snapshotRevision, Records.ToList());
    }

    private static WorldEventRecord CreateRecord(int day, string kind, string text, string eventKey, IReadOnlyList<string> visibilityIdentityIds)
    {
        string safeKind = WorldEventContract.NormalizeEventType(AwakeRuntime.TruncateTextElements(kind ?? "event", 40));
        string safeText = AwakeRuntime.TruncateTextElements(text ?? string.Empty, 500);
        if (string.IsNullOrWhiteSpace(safeText)) return null;
        string safeEventKey = string.IsNullOrWhiteSpace(eventKey) ? null : AwakeRuntime.TruncateTextElements(eventKey.Trim(), 200);
        return new WorldEventRecord(day, safeKind, safeText, safeEventKey, visibilityIdentityIds);
    }

    private static WorldEventAppendResult StaleSessionResult(WorldEventRecord record)
    {
        return new WorldEventAppendResult
        {
            Status = WorldEventAppendResult.PersistenceUnknown,
            EventId = record?.EventId ?? string.Empty,
            EventKey = record?.EventKey ?? string.Empty,
            Code = "awake.world_event.stale_session"
        };
    }

    private static WorldEventRecord FindExisting(WorldEventRecord record)
    {
        return Records.FirstOrDefault(existing => StringComparer.Ordinal.Equals(existing.EventId, record.EventId)
            || StringComparer.Ordinal.Equals(existing.EventKey, record.EventKey));
    }

    private static void RemovePendingIfOwned(WorldEventRecord record, int campaignGeneration)
    {
        if (record == null) return;
        if (PendingEventIds.TryGetValue(record.EventId, out int idGeneration) && idGeneration == campaignGeneration)
            PendingEventIds.Remove(record.EventId);
        if (PendingEventKeys.TryGetValue(record.EventKey, out int keyGeneration) && keyGeneration == campaignGeneration)
            PendingEventKeys.Remove(record.EventKey);
    }

    private static bool SameContent(WorldEventRecord left, WorldEventRecord right)
    {
        return left != null && right != null
            && left.Day == right.Day
            && StringComparer.Ordinal.Equals(left.Kind, right.Kind)
            && StringComparer.Ordinal.Equals(left.Domain, right.Domain)
            && StringComparer.Ordinal.Equals(left.Text, right.Text)
            && left.OccurredAt == right.OccurredAt
            && new HashSet<string>(left.VisibilityIdentityIds ?? Array.Empty<string>(), StringComparer.Ordinal)
                .SetEquals(right.VisibilityIdentityIds ?? Array.Empty<string>());
    }

    private static bool TryRemember(WorldEventRecord record)
    {
        if (record == null || SeenEventIds.Contains(record.EventId) || SeenEventKeys.Contains(record.EventKey)) return false;
        SeenEventIds.Add(record.EventId);
        SeenEventKeys.Add(record.EventKey);
        SeenEventOrder.Enqueue(record.EventId);
        SeenEventKeyOrder.Enqueue(record.EventKey);
        while (SeenEventOrder.Count > SeenEventCapacity)
            SeenEventIds.Remove(SeenEventOrder.Dequeue());
        while (SeenEventKeyOrder.Count > SeenEventCapacity)
            SeenEventKeys.Remove(SeenEventKeyOrder.Dequeue());
        return true;
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }

    private static DateTimeOffset ParseOccurredAt(string value, int day)
    {
        if (DateTimeOffset.TryParse(value, out DateTimeOffset parsed)) return parsed;
        return new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day);
    }

    private static IReadOnlyList<string> ReadVisibilityIdentityIds(JToken value)
    {
        var result = new List<string>();
        if (value is JArray array)
        {
            foreach (JToken token in array)
            {
                string identityId = (string)token;
                if (!string.IsNullOrWhiteSpace(identityId)) result.Add(identityId);
            }
        }
        return result;
    }
}
