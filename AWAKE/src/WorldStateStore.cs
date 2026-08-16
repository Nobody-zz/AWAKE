using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake;

internal enum WorldStateKind
{
    Memory,
    Relationship,
    EventMeta,
    Proactive
}

internal sealed class MemoryReservation
{
    internal string HeroId { get; }
    internal string EntrySource { get; }
    internal string ConversationId { get; }
    internal int Sequence { get; }
    internal int Day { get; }

    internal MemoryReservation(string heroId, string entrySource, string conversationId, int sequence, int day)
    {
        HeroId = heroId ?? string.Empty;
        EntrySource = entrySource ?? string.Empty;
        ConversationId = conversationId ?? string.Empty;
        Sequence = sequence;
        Day = day;
    }
}

internal sealed class WorldStateCommand
{
    internal string NamespaceId { get; }
    internal string Key { get; }
    internal string CommandId { get; }
    internal string IdempotencyKey { get; }
    internal string HeroId { get; }
    internal WorldStateKind Kind { get; }
    internal JObject Arguments { get; }
    internal DateTimeOffset RequestedUtc { get; }
    internal string CorrelationId { get; }
    internal int Attempts { get; set; }

    internal WorldStateCommand(
        string namespaceId,
        string key,
        string commandId,
        string idempotencyKey,
        string heroId,
        WorldStateKind kind,
        JObject arguments,
        DateTimeOffset requestedUtc,
        string correlationId)
    {
        NamespaceId = namespaceId ?? string.Empty;
        Key = key ?? string.Empty;
        CommandId = commandId ?? string.Empty;
        IdempotencyKey = idempotencyKey ?? string.Empty;
        HeroId = heroId ?? string.Empty;
        Kind = kind;
        Arguments = arguments ?? new JObject();
        RequestedUtc = requestedUtc;
        CorrelationId = correlationId ?? string.Empty;
    }
}

internal sealed class WorldPendingEvent
{
    internal string EventId { get; }
    internal string CommandId { get; }
    internal string HeroId { get; }
    internal JObject Payload { get; }
    internal string CorrelationId { get; }
    internal string EventKind { get; }
    internal string EventSchema { get; }
    internal int Attempts { get; set; }

    internal WorldPendingEvent(
        string eventId,
        string commandId,
        string heroId,
        JObject payload,
        string correlationId,
        string eventKind = null,
        string eventSchema = null)
    {
        EventId = eventId ?? Guid.NewGuid().ToString("N");
        CommandId = commandId ?? string.Empty;
        HeroId = heroId ?? string.Empty;
        Payload = payload ?? new JObject();
        CorrelationId = correlationId ?? string.Empty;
        EventKind = eventKind ?? string.Empty;
        EventSchema = eventSchema ?? string.Empty;
    }
}

internal sealed class DrainWritePass
{
    internal bool Any { get; set; }
    internal bool DeferredRetry { get; set; }
}
internal sealed class WorldApplyResult
{
    internal bool Applied { get; set; }
    internal bool Retryable { get; set; }
    internal string Code { get; set; }
    internal WorldPendingEvent Event { get; set; }
}

internal sealed class WorldDrainSummary
{
    internal int StateWriteCount { get; set; }
    internal int DuplicateCount { get; set; }
    internal int HardFailureCount { get; set; }
    internal string HardFailureCode { get; set; }
    internal int EventPublishFailureCount { get; set; }
    internal bool DeferredRetry { get; set; }
    internal bool OwnerCommandObserved { get; set; }
}

internal sealed class WorldCommandResultRecord
{
    internal string CommandId { get; set; }
    internal string IdempotencyKey { get; set; }
    internal bool Applied { get; set; }
    internal bool Duplicate { get; set; }
    internal bool Retryable { get; set; }
    internal string Code { get; set; }
    internal int EventPublishFailureCount { get; set; }
}

internal sealed class WorldStateStore
{
    private readonly IMarcusAiFrameworkHost _host;
    private readonly SessionRef _sessionRef;
    private readonly object _gate = new object();
    private readonly Dictionary<string, IKeyValueStore> _stores = new Dictionary<string, IKeyValueStore>(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldCommandResultRecord> _resultLedger = new Dictionary<string, WorldCommandResultRecord>(StringComparer.Ordinal);
    private readonly List<string> _resultLedgerOrder = new List<string>();
    private readonly ConcurrentQueue<WorldStateCommand> _pendingWrites = new ConcurrentQueue<WorldStateCommand>();
    private readonly ConcurrentQueue<WorldPendingEvent> _pendingEvents = new ConcurrentQueue<WorldPendingEvent>();
    private readonly Dictionary<string, MemoryReservation> _memoryReservations = new Dictionary<string, MemoryReservation>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _memorySequence = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly SemaphoreSlim _drainGate = new SemaphoreSlim(1, 1);
    private bool _sessionEnded;
    private bool _finalDrainStarted;
    private int _droppedItems;

    internal WorldStateStore(IMarcusAiFrameworkHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _sessionRef = host.CurrentSession ?? new SessionRef(string.Empty, string.Empty, string.Empty);
    }

    internal WorldStateStore(SessionRef sessionRef)
    {
        _host = null;
        _sessionRef = sessionRef ?? new SessionRef("test-campaign", "test-timeline", "test-session");
    }

    internal void InjectStoreForTesting(string namespaceId, IKeyValueStore store)
    {
        if (string.IsNullOrWhiteSpace(namespaceId) || store == null) return;
        lock (_gate) _stores[namespaceId] = store;
    }

    internal bool SessionEnded
    {
        get { lock (_gate) return _sessionEnded; }
    }

    internal async Task<bool> OpenNamespacesAsync(CancellationToken cancellationToken)
    {
        bool any = false;
        foreach (string namespaceId in AiTaskConstants.StorageNamespaceIds)
        {
            try
            {
                RequestContext context = CreateContext();
                OperationResult<IKeyValueStore> result = await _host.Storage.OpenCampaignNamespaceAsync(namespaceId, context, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess && result.Value != null)
                {
                    lock (_gate) _stores[namespaceId] = result.Value;
                    any = true;
                }
                else
                {
                    AwakeLog.Write("world_state_namespace_open_degraded namespace=" + namespaceId + " code=" + (result.Error?.Code ?? "unknown"));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AwakeLog.Write("world_state_namespace_open_error namespace=" + namespaceId + " error=" + ex.Message);
            }
        }
        return any;
    }

    internal bool TryEnqueue(WorldStateCommand command)
    {
        if (command == null) return false;
        lock (_gate)
        {
            if (_sessionEnded) return false;
            _pendingWrites.Enqueue(command);
            return true;
        }
    }

    internal void EnqueuePendingEventForTesting(WorldPendingEvent pending)
    {
        if (pending == null) return;
        _pendingEvents.Enqueue(pending);
    }
    internal void BeginSessionEnd()
    {
        lock (_gate)
        {
            foreach (MemoryReservation reservation in _memoryReservations.Values)
            {
                _pendingWrites.Enqueue(BuildMemoryCommand(
                    reservation.HeroId,
                    reservation.ConversationId,
                    "append",
                    reservation.Day,
                    "shared_experience",
                    new JArray(),
                    string.Empty,
                    1,
                    reservation.EntrySource,
                    reservation.Sequence));
            }
            _memoryReservations.Clear();
            _sessionEnded = true;
        }
    }

    internal bool ReserveMemory(string heroId, string entrySource, int day, out string conversationId, out int sequence)
    {
        conversationId = string.Empty;
        sequence = 0;
        if (string.IsNullOrWhiteSpace(heroId)) return false;
        lock (_gate)
        {
            if (_sessionEnded) return false;
            int current;
            _memorySequence.TryGetValue(heroId, out current);
            sequence = current + 1;
            _memorySequence[heroId] = sequence;
            conversationId = (_sessionRef.SessionId ?? "campaign") + "|" + heroId + "|" + entrySource + "|" + sequence;
            _memoryReservations[conversationId] = new MemoryReservation(heroId, entrySource, conversationId, sequence, day);
            return true;
        }
    }

    internal async Task<bool> FlushMemoryFactsAsync(
        string heroId,
        string conversationId,
        int day,
        string type,
        JArray facts,
        string summary,
        int weight,
        string source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(heroId) || string.IsNullOrWhiteSpace(conversationId)) return false;
        int sequence = 0;
        lock (_gate)
        {
            MemoryReservation reservation;
            if (_memoryReservations.TryGetValue(conversationId, out reservation))
            {
                sequence = reservation.Sequence;
                _memoryReservations.Remove(conversationId);
            }
            else
            {
                _memorySequence.TryGetValue(heroId, out sequence);
            }
        }
        WorldStateCommand command = BuildMemoryCommand(
            heroId,
            conversationId,
            "append",
            day,
            string.IsNullOrWhiteSpace(type) ? "shared_experience" : type,
            facts ?? new JArray(),
            summary ?? string.Empty,
            weight,
            string.IsNullOrWhiteSpace(source) ? "npc_dialogue" : source,
            sequence);
        if (!TryEnqueue(command)) return false;
        await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<bool> PatchMemorySummaryAsync(
        string heroId,
        string conversationId,
        string summary,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(heroId) || string.IsNullOrWhiteSpace(conversationId)) return false;
        JObject arguments = new JObject
        {
            ["mode"] = "patch",
            ["conversationId"] = conversationId,
            ["summary"] = summary ?? string.Empty
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.NpcMemoriesNamespace,
            HeroKey(heroId),
            "awake.memory.patch",
            conversationId + ":summary",
            heroId,
            WorldStateKind.Memory,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<bool> AppendEventMemoryAsync(
        string heroId,
        int day,
        string type,
        JArray facts,
        int weight,
        string source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return false;
        string conversationId = "event|" + heroId + "|" + source + "|" + Guid.NewGuid().ToString("N");
        int sequence;
        lock (_gate)
        {
            if (_sessionEnded) return false;
            int current;
            _memorySequence.TryGetValue(heroId, out current);
            sequence = current + 1;
            _memorySequence[heroId] = sequence;
        }
        WorldStateCommand command = BuildMemoryCommand(
            heroId,
            conversationId,
            "append",
            day,
            string.IsNullOrWhiteSpace(type) ? "shared_experience" : type,
            facts ?? new JArray(),
            string.Empty,
            weight,
            string.IsNullOrWhiteSpace(source) ? "event" : source,
            sequence);
        if (!TryEnqueue(command)) return false;
        await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<JObject> GetMemoriesAsync(string heroId, RequestContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return null;
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.NpcMemoriesNamespace, out store);
        if (store == null) return null;

        OperationResult<string> loaded = await store.GetAsync(HeroKey(heroId), context ?? CreateContext(), cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            AwakeLog.Write("world_state_memory_load_failed hero=" + heroId + " code=" + (loaded.Error?.Code ?? "unknown"));
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return null;

        JObject doc;
        try
        {
            doc = JObject.Parse(loaded.Value);
            if (doc.Type != JTokenType.Object) throw new InvalidOperationException("memory root is not object");
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_memory_corrupt hero=" + heroId + " error=" + ex.Message);
            return null;
        }

        lock (_gate)
        {
            int persisted;
            if (int.TryParse(doc["nextConversationSequence"]?.ToString(), out persisted))
            {
                int current;
                _memorySequence.TryGetValue(heroId, out current);
                if (persisted > current) _memorySequence[heroId] = persisted;
            }
        }
        return doc;
    }

    internal async Task<JObject> GetRelationshipAsync(string heroId, RequestContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return null;
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.RelationshipsNamespace, out store);
        if (store == null) return null;

        string key = BuildHeroKey(heroId);
        OperationResult<string> loaded = await store.GetAsync(key, context ?? CreateContext(), cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            AwakeLog.Write("world_state_relationship_load_failed hero=" + heroId + " code=" + (loaded.Error?.Code ?? "unknown"));
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return NewRelationshipState(heroId);

        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            if (doc.Type != JTokenType.Object) throw new InvalidOperationException("relationship root is not object");
            return doc;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_relationship_corrupt hero=" + heroId + " error=" + ex.Message);
            return null;
        }
    }

    internal async Task<JObject> GetEventMetaAsync(RequestContext context, CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.EventMetaNamespace, out store);
        if (store == null) return null;

        OperationResult<string> loaded = await store.GetAsync(AiTaskConstants.EventMetaKey, context ?? CreateContext(), cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            AwakeLog.Write("world_state_event_meta_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return NewEventMetaState();
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            if (doc.Type != JTokenType.Object) throw new InvalidOperationException("event meta root is not object");
            return doc;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_event_meta_corrupt error=" + ex.Message);
            return null;
        }
    }

    internal async Task<bool> UpdateEventMetaAsync(
        string eventId,
        int version,
        double lastTriggerHour,
        int day,
        int count,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        JObject arguments = new JObject
        {
            ["eventId"] = eventId,
            ["version"] = version,
            ["lastTriggerHour"] = lastTriggerHour,
            ["day"] = day,
            ["count"] = count
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.EventMetaNamespace,
            AiTaskConstants.EventMetaKey,
            "awake.event_meta.upsert",
            idempotencyKey,
            string.Empty,
            WorldStateKind.EventMeta,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<JObject> GetProactiveAsync(RequestContext context, CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.ProactiveNamespace, out store);
        if (store == null) return null;

        OperationResult<string> loaded = await store.GetAsync(
            NpcProactiveConstants.Key,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            AwakeLog.Write("world_state_proactive_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return NewProactiveState();
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            if (doc.Type != JTokenType.Object) throw new InvalidOperationException("proactive root is not object");
            return doc;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_proactive_corrupt error=" + ex.Message);
            return null;
        }
    }

    internal async Task<bool> UpdateProactiveAsync(
        JArray candidates,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (candidates == null) return false;
        JObject arguments = new JObject
        {
            ["candidates"] = (JArray)candidates.DeepClone()
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.ProactiveNamespace,
            NpcProactiveConstants.Key,
            "awake.npc.proactive.upsert",
            idempotencyKey,
            string.Empty,
            WorldStateKind.Proactive,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task BeginFinalDrainAsync()
    {
        bool started;
        lock (_gate)
        {
            if (_finalDrainStarted)
            {
                started = false;
            }
            else
            {
                _finalDrainStarted = true;
                started = true;
            }
        }
        if (!started) return;

        try
        {
            await DrainAsync(CancellationToken.None).ConfigureAwait(false);
            bool empty;
            lock (_gate)
            {
                empty = _pendingWrites.IsEmpty && _pendingEvents.IsEmpty;
            }
            if (_droppedItems > 0 || !empty)
            {
                AwakeLog.Write("world_state_final_drain_failed pending_writes=" + _pendingWrites.Count + " pending_events=" + _pendingEvents.Count + " dropped=" + _droppedItems);
            }
            else
            {
                AwakeLog.Write("world_state_final_drain_complete");
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_final_drain_error error=" + ex.Message);
        }
    }

    internal async Task<WorldDrainSummary> DrainAsync(CancellationToken cancellationToken)
    {
        await DrainCoreAsync(null, null, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            WorldDrainSummary summary = new WorldDrainSummary();
            foreach (WorldCommandResultRecord record in _resultLedger.Values)
            {
                if (record.Applied) summary.StateWriteCount++;
                if (record.Duplicate) summary.DuplicateCount++;
                if (!record.Applied && !record.Duplicate && !record.Retryable) summary.HardFailureCount++;
                summary.EventPublishFailureCount += record.EventPublishFailureCount;
            }
            return summary;
        }
    }

    internal async Task<WorldDrainSummary> DrainAsync(string ownerCommandId, string ownerIdempotencyKey, CancellationToken cancellationToken)
    {
        await DrainCoreAsync(ownerCommandId, ownerIdempotencyKey, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            WorldCommandResultRecord record;
            if (!string.IsNullOrWhiteSpace(ownerIdempotencyKey)
                && TryGetResultRecord(OwnerKey(ownerCommandId, ownerIdempotencyKey), out record))
            {
                return SummaryFromRecord(record);
            }
            return new WorldDrainSummary();
        }
    }

    private async Task DrainCoreAsync(string ownerCommandId, string ownerIdempotencyKey, CancellationToken cancellationToken)
    {
        await _drainGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int guard = 0;
            while (guard++ < 32)
            {
                DrainWritePass writePass = await DrainWritesOnceAsync(cancellationToken).ConfigureAwait(false);
                bool evented = await DrainEventsOnceAsync(cancellationToken).ConfigureAwait(false);
                if (writePass.DeferredRetry || (!writePass.Any && !evented)) break;
            }
        }
        finally
        {
            _drainGate.Release();
        }
    }

    private static WorldDrainSummary SummaryFromRecord(WorldCommandResultRecord record)
    {
        WorldDrainSummary summary = new WorldDrainSummary
        {
            OwnerCommandObserved = record != null,
            StateWriteCount = record != null && record.Applied ? 1 : 0,
            DuplicateCount = record != null && record.Duplicate ? 1 : 0,
            EventPublishFailureCount = record?.EventPublishFailureCount ?? 0,
            DeferredRetry = record != null && record.Retryable
        };
        if (record != null && !record.Applied && !record.Duplicate && !record.Retryable)
        {
            summary.HardFailureCount = 1;
            summary.HardFailureCode = record.Code;
        }
        return summary;
    }

    internal async Task<bool> WriteEmptyMemoryAsync(string heroId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return false;
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.NpcMemoriesNamespace, out store);
        if (store == null) return false;

        JObject doc = new JObject
        {
            ["schema"] = "awake.npc.memory.v1",
            ["heroId"] = heroId,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["memories"] = new JArray()
        };
        OperationResult<bool> stored = await store.SetAsync(HeroKey(heroId), doc.ToString(Formatting.None), CreateContext(), cancellationToken).ConfigureAwait(false);
        return stored.IsSuccess && stored.Value;
    }

    private async Task<DrainWritePass> DrainWritesOnceAsync(CancellationToken cancellationToken)
    {
        bool any = false;
        bool deferredRetry = false;
        WorldStateCommand command;
        while (_pendingWrites.TryDequeue(out command))
        {
            any = true;
            RequestContext context = CreateContext();
            WorldApplyResult result;
            try
            {
                result = await TryApplyAsync(command, context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AwakeLog.Write("world_state_apply_error command=" + command.CommandId
                    + " key=" + command.Key
                    + " error=" + ex.Message);
                result = new WorldApplyResult { Retryable = true, Code = "awake.world_state.apply_error" };
            }
            RecordResult(command, result);
            if (result.Applied)
            {
                if (result.Event != null)
                {
                    _pendingEvents.Enqueue(result.Event);
                }
            }
            else if (result.Retryable && command.Attempts < AiTaskConstants.DrainMaximumRetries)
            {
                command.Attempts++;
                _pendingWrites.Enqueue(command);
                deferredRetry = true;
                break;
            }
            else
            {
                if (!StringComparer.Ordinal.Equals(result.Code, "awake.world_state.duplicate")
                    && !StringComparer.Ordinal.Equals(result.Code, "awake.world_state.favor.insufficient_balance"))
                {
                    System.Threading.Interlocked.Increment(ref _droppedItems);
                    AwakeLog.Write("world_state_write_failed_dropped command=" + command.CommandId
                        + " key=" + command.Key
                        + " code=" + (string.IsNullOrWhiteSpace(result.Code) ? "unknown" : result.Code)
                        + " attempts=" + command.Attempts);
                }
            }
        }
        return new DrainWritePass { Any = any, DeferredRetry = deferredRetry };
    }

    private async Task<bool> DrainEventsOnceAsync(CancellationToken cancellationToken)
    {
        bool any = false;
        WorldPendingEvent pending;
        while (_pendingEvents.TryDequeue(out pending))
        {
            any = true;
            RequestContext context = CreateContext();
            EventEnvelope envelope = new EventEnvelope(
                pending.EventId,
                new SchemaRef(pending.EventSchema, 1, 0),
                _sessionRef,
                0,
                new ExtensionId(AwakeConstants.OwnerValue),
                pending.EventKind,
                pending.CorrelationId,
                pending.CorrelationId,
                DataAccessScope.PlayerKnown,
                SourceClass.ExtensionProvider,
                EpistemicStatus.Fact,
                DateTimeOffset.UtcNow,
                pending.Payload.ToString(Formatting.None));

            OperationResult<bool> published;
            try
            {
                published = _host.Events.Publish(envelope, EventDelivery.Durable, context);
            }
            catch (Exception ex)
            {
                AwakeLog.Write("world_state_event_publish_error event=" + pending.EventId + " error=" + ex.Message);
                published = OperationResult<bool>.Failed(FrameworkErrors.Create("awake.event_publish_error", FrameworkErrorCategory.InternalFailure, "Event publish failed.", context.CorrelationId, owner: AwakeConstants.OwnerValue));
            }

            if (published.IsSuccess && published.Value)
            {
                RecordEventSuccess(pending);
                AwakeLog.Write("world_state_event_published event=" + pending.EventId);
            }
            else if (pending.Attempts < AiTaskConstants.DrainMaximumRetries)
            {
                pending.Attempts++;
                _pendingEvents.Enqueue(pending);
            }
            else
            {
                System.Threading.Interlocked.Increment(ref _droppedItems);
                RecordEventFailure(pending);
                AwakeLog.Write("world_state_event_publish_failed_dropped event=" + pending.EventId
                    + " code=" + (published.Error?.Code ?? "unknown")
                    + " attempts=" + pending.Attempts);
            }
        }
        return any;
    }

    private void RecordResult(WorldStateCommand command, WorldApplyResult result)
    {
        if (command == null || result == null) return;
        lock (_gate)
        {
            WriteResultRecord(OwnerKey(command.CommandId, command.IdempotencyKey), new WorldCommandResultRecord
            {
                CommandId = command.CommandId,
                IdempotencyKey = command.IdempotencyKey,
                Applied = result.Applied,
                Duplicate = StringComparer.Ordinal.Equals(result.Code, "awake.world_state.duplicate"),
                Retryable = result.Retryable,
                Code = result.Code ?? string.Empty
            });
        }
    }

    private void RecordEventFailure(WorldPendingEvent pending)
    {
        if (pending == null) return;
        lock (_gate)
        {
            string key = OwnerKey(pending.CommandId, pending.EventId);
            WorldCommandResultRecord record;
            if (!TryGetResultRecord(key, out record))
            {
                record = new WorldCommandResultRecord
                {
                    CommandId = pending.CommandId,
                    IdempotencyKey = pending.EventId
                };
                WriteResultRecord(key, record);
            }
            record.EventPublishFailureCount++;
        }
    }

    private void RecordEventSuccess(WorldPendingEvent pending)
    {
        if (pending == null) return;
        lock (_gate)
        {
            string key = OwnerKey(pending.CommandId, pending.EventId);
            WorldCommandResultRecord record;
            if (TryGetResultRecord(key, out record))
            {
                record.EventPublishFailureCount = 0;
            }
        }
    }

    private void WriteResultRecord(string key, WorldCommandResultRecord record)
    {
        if (_resultLedger.ContainsKey(key))
        {
            _resultLedgerOrder.Remove(key);
        }
        _resultLedger[key] = record;
        _resultLedgerOrder.Add(key);
        while (_resultLedgerOrder.Count > AiTaskConstants.AppliedKeysMaximum)
        {
            string oldest = _resultLedgerOrder[0];
            _resultLedgerOrder.RemoveAt(0);
            _resultLedger.Remove(oldest);
        }
    }

    private bool TryGetResultRecord(string key, out WorldCommandResultRecord record)
    {
        return _resultLedger.TryGetValue(key, out record);
    }

    private static string OwnerKey(string commandId, string idempotencyKey)
    {
        return (commandId ?? string.Empty) + "|" + (idempotencyKey ?? string.Empty);
    }

    private static WorldStateCommand BuildMemoryCommand(
        string heroId,
        string conversationId,
        string mode,
        int day,
        string type,
        JArray facts,
        string summary,
        int weight,
        string source,
        int sequence)
    {
        bool patch = StringComparer.Ordinal.Equals(mode, "patch");
        JObject arguments = new JObject
        {
            ["mode"] = mode,
            ["conversationId"] = conversationId ?? string.Empty,
            ["day"] = day,
            ["type"] = type ?? "shared_experience",
            ["facts"] = facts ?? new JArray(),
            ["summary"] = summary ?? string.Empty,
            ["weight"] = weight,
            ["source"] = source ?? "npc_dialogue",
            ["sequence"] = sequence
        };
        return new WorldStateCommand(
            AiTaskConstants.NpcMemoriesNamespace,
            HeroKey(heroId),
            patch ? "awake.memory.patch" : "awake.memory.append",
            conversationId + (patch ? ":summary" : ":facts"),
            heroId,
            WorldStateKind.Memory,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
    }

    private async Task<WorldApplyResult> TryApplyAsync(WorldStateCommand command, RequestContext context, CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(command.NamespaceId, out store);
        if (store == null)
        {
            return new WorldApplyResult { Retryable = true, Code = "awake.world_state.storage_unavailable" };
        }

        OperationResult<string> loaded = await store.GetAsync(command.Key, context, cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            return new WorldApplyResult { Retryable = true, Code = loaded.Error?.Code ?? "awake.world_state.read_failed" };
        }

        JObject state;
        if (string.IsNullOrWhiteSpace(loaded.Value))
        {
            state = NewState(command.Kind, command.HeroId);
        }
        else
        {
            try
            {
                state = JObject.Parse(loaded.Value);
                if (state.Type != JTokenType.Object) throw new InvalidOperationException("root is not object");
            }
            catch (Exception ex)
            {
                AwakeLog.Write("world_state_state_corrupt key=" + command.Key + " error=" + ex.Message);
                return new WorldApplyResult { Retryable = false, Code = "awake.world_state.corrupt" };
            }
        }

        if (command.Kind != WorldStateKind.EventMeta)
        {
            JArray appliedKeys = (JArray)state["appliedKeys"];
            foreach (JToken keyToken in appliedKeys)
            {
                if (keyToken.Type == JTokenType.String && StringComparer.Ordinal.Equals((string)keyToken, command.IdempotencyKey))
                {
                    return new WorldApplyResult { Applied = false, Retryable = false, Code = "awake.world_state.duplicate" };
                }
            }
        }

        JObject eventPayload = null;
        string applyError = string.Empty;
        switch (command.Kind)
        {
            case WorldStateKind.Memory:
                applyError = ApplyMemory(state, command, out eventPayload);
                break;
            case WorldStateKind.Relationship:
                applyError = ApplyRelationship(state, command);
                break;
            case WorldStateKind.EventMeta:
                applyError = ApplyEventMeta(state, command);
                break;
            case WorldStateKind.Proactive:
                applyError = ApplyProactive(state, command);
                break;
        }
        if (!string.IsNullOrWhiteSpace(applyError))
        {
            return new WorldApplyResult { Applied = false, Retryable = false, Code = applyError };
        }

        state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
        string json = state.ToString(Formatting.None);
        if (Encoding.UTF8.GetByteCount(json) > AiTaskConstants.StorageValueMaximumBytes)
        {
            return new WorldApplyResult { Retryable = false, Code = "awake.world_state.too_large" };
        }

        OperationResult<bool> stored = await store.SetAsync(command.Key, json, context, cancellationToken).ConfigureAwait(false);
        if (!stored.IsSuccess || !stored.Value)
        {
            return new WorldApplyResult { Retryable = true, Code = stored.Error?.Code ?? "awake.world_state.write_failed" };
        }

        if (eventPayload != null)
        {
            return new WorldApplyResult
            {
                Applied = true,
                Event = new WorldPendingEvent(
                    command.IdempotencyKey,
                    command.CommandId,
                    command.HeroId,
                    eventPayload,
                    command.CorrelationId,
                    command.CommandId,
                    command.CommandId + ".event.v1")
            };
        }
        return new WorldApplyResult { Applied = true };
    }

    private static JObject NewState(WorldStateKind kind, string heroId)
    {
        switch (kind)
        {
            case WorldStateKind.Memory: return NewMemoryState(heroId);
            case WorldStateKind.Relationship: return NewRelationshipState(heroId);
            case WorldStateKind.EventMeta: return NewEventMetaState();
            case WorldStateKind.Proactive: return NewProactiveState();
            default: return new JObject();
        }
    }

    private static JObject NewMemoryState(string heroId)
    {
        return new JObject
        {
            ["schema"] = "awake.npc.memory.v1",
            ["heroId"] = heroId ?? string.Empty,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["memories"] = new JArray(),
            ["nextConversationSequence"] = 0,
            ["appliedKeys"] = new JArray()
        };
    }

    private static JObject NewEventMetaState()
    {
        return new JObject
        {
            ["schema"] = "awake.event_meta.v1",
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["versions"] = new JObject(),
            ["cooldowns"] = new JObject(),
            ["daily"] = new JObject()
        };
    }

    private static JObject NewProactiveState()
    {
        return new JObject
        {
            ["schema"] = NpcProactiveConstants.Schema,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["candidates"] = new JArray(),
            ["appliedKeys"] = new JArray()
        };
    }

    private static JObject NewRelationshipState(string heroId)
    {
        return new JObject
        {
            ["schema"] = "awake.relationship.state.v1",
            ["heroId"] = heroId ?? string.Empty,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["trust"] = 0,
            ["love"] = 0,
            ["hostility"] = 0,
            ["entries"] = new JArray(),
            ["appliedKeys"] = new JArray()
        };
    }

    private static void EnsureEventMetaShape(JObject state)
    {
        state["schema"] = "awake.event_meta.v1";
        if (!(state["versions"] is JObject)) state["versions"] = new JObject();
        if (!(state["cooldowns"] is JObject)) state["cooldowns"] = new JObject();
        if (!(state["daily"] is JObject)) state["daily"] = new JObject();
    }

    private static void EnsureProactiveShape(JObject state)
    {
        state["schema"] = NpcProactiveConstants.Schema;
        if (!(state["candidates"] is JArray)) state["candidates"] = new JArray();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ApplyProactive(JObject state, WorldStateCommand command)
    {
        EnsureProactiveShape(state);
        JArray candidates = command.Arguments["candidates"] as JArray;
        if (candidates == null) return "awake.world_state.proactive.invalid_candidates";
        state["candidates"] = (JArray)candidates.DeepClone();
        state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
        JArray appliedKeys = (JArray)state["appliedKeys"];
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        return string.Empty;
    }

    private static string ApplyEventMeta(JObject state, WorldStateCommand command)
    {
        EnsureEventMetaShape(state);
        string eventId = (string)command.Arguments["eventId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(eventId)) return "awake.world_state.event_meta.invalid_event";
        int version = IntValue(command.Arguments["version"]);
        int storedVersion = IntValue(((JObject)state["versions"])[eventId]);
        if (storedVersion >= version) return "awake.world_state.duplicate";

        ((JObject)state["versions"])[eventId] = version;
        ((JObject)state["cooldowns"])[eventId] = DoubleValue(command.Arguments["lastTriggerHour"]);
        JObject dailyEntry = ((JObject)state["daily"])[eventId] as JObject;
        if (dailyEntry == null)
        {
            dailyEntry = new JObject { ["day"] = 0, ["count"] = 0 };
            ((JObject)state["daily"])[eventId] = dailyEntry;
        }
        dailyEntry["day"] = IntValue(command.Arguments["day"]);
        dailyEntry["count"] = IntValue(command.Arguments["count"]);
        state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
        return string.Empty;
    }

    private static void EnsureMemoryShape(JObject state, string heroId)
    {
        state["schema"] = "awake.npc.memory.v1";
        if (state["heroId"] == null) state["heroId"] = heroId ?? string.Empty;
        if (!(state["memories"] is JArray)) state["memories"] = new JArray();
        if (!(state["nextConversationSequence"] is JValue) || state["nextConversationSequence"].Type != JTokenType.Integer)
        {
            state["nextConversationSequence"] = 0;
        }
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ApplyMemory(JObject state, WorldStateCommand command, out JObject eventPayload)
    {
        eventPayload = null;
        EnsureMemoryShape(state, command.HeroId);
        JArray memories = (JArray)state["memories"];
        JArray appliedKeys = (JArray)state["appliedKeys"];
        string mode = (string)command.Arguments["mode"] ?? "append";

        if (StringComparer.Ordinal.Equals(mode, "patch"))
        {
            string conversationId = (string)command.Arguments["conversationId"] ?? string.Empty;
            JObject target = null;
            foreach (JToken token in memories)
            {
                if (token is JObject candidate && StringComparer.Ordinal.Equals((string)candidate["conversationId"], conversationId))
                {
                    target = candidate;
                    break;
                }
            }
            if (target == null) return "awake.world_state.memory.not_found";
            target["summary"] = ClampTextElements((string)command.Arguments["summary"] ?? string.Empty, AiTaskConstants.MemorySummaryMaximumChars);
            appliedKeys.Add(command.IdempotencyKey);
            Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
            return string.Empty;
        }

        int sequence = IntValue(command.Arguments["sequence"]);
        int weight = Clamp(IntValue(command.Arguments["weight"]), 1, 3);
        if (weight == 3)
        {
            int pinned = 0;
            foreach (JToken token in memories)
            {
                if (token is JObject candidate && IntValue(candidate["weight"]) == 3) pinned++;
            }
            if (pinned >= AiTaskConstants.MemoryPinnedMaximum) weight = 2;
        }

        JObject entry = new JObject
        {
            ["id"] = (string)command.Arguments["conversationId"] ?? string.Empty,
            ["day"] = IntValue(command.Arguments["day"]),
            ["type"] = (string)command.Arguments["type"] ?? "shared_experience",
            ["summary"] = ClampTextElements((string)command.Arguments["summary"] ?? string.Empty, AiTaskConstants.MemorySummaryMaximumChars),
            ["facts"] = ClampFacts(command.Arguments["facts"] as JArray),
            ["weight"] = weight,
            ["source"] = (string)command.Arguments["source"] ?? "npc_dialogue",
            ["conversationId"] = (string)command.Arguments["conversationId"] ?? string.Empty
        };
        if (Encoding.UTF8.GetByteCount(entry.ToString(Formatting.None)) > AiTaskConstants.MemoryEntryMaximumBytes)
        {
            return "awake.world_state.memory.too_large";
        }

        if (memories.Count >= AiTaskConstants.MemoryEntriesMaximum)
        {
            int candidateIndex = -1;
            int candidateWeight = 4;
            for (int i = memories.Count - 1; i >= 0; i--)
            {
                JToken token = memories[i];
                int currentWeight = token is JObject candidate ? IntValue(candidate["weight"]) : 3;
                if (currentWeight < candidateWeight)
                {
                    candidateWeight = currentWeight;
                    candidateIndex = i;
                    if (currentWeight == 1) break;
                }
            }
            if (candidateIndex < 0 || (candidateWeight == 3 && weight == 3))
            {
                return "awake.world_state.memory.full";
            }
            memories.RemoveAt(candidateIndex);
        }

        memories.Insert(0, entry);
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        int nextSequence = IntValue(state["nextConversationSequence"]);
        if (sequence > nextSequence) state["nextConversationSequence"] = sequence;
        return string.Empty;
    }

    private static string ApplyRelationship(JObject state, WorldStateCommand command)
    {
        EnsureRelationshipShape(state, command.HeroId);
        int trust = IntValue(state["trust"]);
        int love = IntValue(state["love"]);
        int hostility = IntValue(state["hostility"]);
        int trustDelta = IntValue(command.Arguments["trustDelta"]);
        int loveDelta = IntValue(command.Arguments["loveDelta"]);
        int hostilityDelta = IntValue(command.Arguments["hostilityDelta"]);
        state["trust"] = Clamp(trust + trustDelta, -100, 100);
        state["love"] = Clamp(love + loveDelta, -100, 100);
        state["hostility"] = Clamp(hostility + hostilityDelta, -100, 100);

        JArray entries = (JArray)state["entries"];
        entries.Insert(0, new JObject
        {
            ["commandId"] = command.CommandId,
            ["idempotencyKey"] = command.IdempotencyKey,
            ["trustDelta"] = trustDelta,
            ["loveDelta"] = loveDelta,
            ["hostilityDelta"] = hostilityDelta,
            ["reason"] = (string)command.Arguments["reason"] ?? string.Empty,
            ["requestedUtc"] = command.RequestedUtc.ToString("O")
        });
        Trim(entries, AiTaskConstants.StateEntriesMaximum);

        JArray appliedKeys = (JArray)state["appliedKeys"];
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
        return string.Empty;
    }

    private static void EnsureRelationshipShape(JObject state, string heroId)
    {
        state["schema"] = "awake.relationship.state.v1";
        if (state["heroId"] == null) state["heroId"] = heroId ?? string.Empty;
        if (!(state["trust"] is JValue) || state["trust"].Type != JTokenType.Integer) state["trust"] = 0;
        if (!(state["love"] is JValue) || state["love"].Type != JTokenType.Integer) state["love"] = 0;
        if (!(state["hostility"] is JValue) || state["hostility"].Type != JTokenType.Integer) state["hostility"] = 0;
        if (!(state["entries"] is JArray)) state["entries"] = new JArray();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ClampTextElements(string value, int maximumElements)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return AwakeRuntime.TruncateTextElements(value, maximumElements);
    }

    private static JArray ClampFacts(JArray facts)
    {
        JArray result = new JArray();
        if (facts == null) return result;
        int count = 0;
        foreach (JToken token in facts)
        {
            if (count >= AiTaskConstants.MemoryFactsMaximum) break;
            string text = token is JValue value ? Convert.ToString(value) : token?.ToString();
            if (string.IsNullOrWhiteSpace(text)) continue;
            result.Add(AwakeRuntime.TruncateTextElements(text, 120));
            count++;
        }
        return result;
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }

    private static void Trim(JArray items, int maximum)
    {
        if (items == null) return;
        while (items.Count > maximum) items.RemoveAt(items.Count - 1);
    }

    private static double DoubleValue(JToken token)
    {
        if (token == null) return 0d;
        try { return Convert.ToDouble(token, System.Globalization.CultureInfo.InvariantCulture); }
        catch { return 0d; }
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        if (value < minimum) return minimum;
        if (value > maximum) return maximum;
        return value;
    }

    private static JObject Clone(JObject source)
    {
        return source == null ? null : (JObject)source.DeepClone();
    }

    private static bool TryReadCache(Dictionary<string, JObject> cache, List<string> order, string key, out JObject value)
    {
        if (cache.TryGetValue(key, out value)) return true;
        value = null;
        return false;
    }

    private static void WriteCache(Dictionary<string, JObject> cache, List<string> order, string key, JObject value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (cache.ContainsKey(key))
        {
            order.Remove(key);
        }
        cache[key] = value;
        order.Add(key);
        while (order.Count > AiTaskConstants.CacheMaximumEntries)
        {
            string oldest = order[0];
            order.RemoveAt(0);
            cache.Remove(oldest);
        }
    }

    internal static string BuildHeroKey(string heroId)
    {
        return "hero." + heroId + ".v1";
    }

    private static string HeroKey(string heroId)
    {
        return BuildHeroKey(heroId);
    }

    private RequestContext CreateContext()
    {
        return new RequestContext(
            new ExtensionId(AwakeConstants.OwnerValue),
            _sessionRef,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow + AwakeConstants.RequestTimeout);
    }
}
