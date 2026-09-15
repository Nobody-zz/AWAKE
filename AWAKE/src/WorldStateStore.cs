using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal enum WorldStateKind
{
    Memory,
    Relationship,
    EventMeta,
    Proactive,
    WorldEvents,
    Messenger,
    Transcript,
    Contacts,
    Audit,
    Onboarding,
    PendingDialogue,
    Interaction,
    InteractionIndex,
    PersonaContinuity,
    PersonaOverride,
    PersonaRecovery,
    Letters
}

internal enum WorldStateStoreLifecycle
{
    Active,
    Ending,
    Ended
}

internal enum MemoryReservationState
{
    Reserved,
    Enqueued
}

internal sealed class MemoryReservation
{
    internal string HeroId { get; }
    internal string EntrySource { get; }
    internal string ConversationId { get; }
    internal int Sequence { get; }
    internal int Day { get; }
    internal MemoryReservationState State { get; set; }
    internal string Type { get; set; }
    internal JArray Facts { get; set; }
    internal string Summary { get; set; }
    internal int Weight { get; set; }
    internal string Source { get; set; }
    internal bool HasPayload { get; private set; }

    internal MemoryReservation(string heroId, string entrySource, string conversationId, int sequence, int day)
    {
        HeroId = heroId ?? string.Empty;
        EntrySource = entrySource ?? string.Empty;
        ConversationId = conversationId ?? string.Empty;
        Sequence = sequence;
        Day = day;
        State = MemoryReservationState.Reserved;
        Type = "shared_experience";
        Facts = new JArray();
        Summary = string.Empty;
        Weight = 1;
        Source = entrySource ?? "npc_dialogue";
        HasPayload = false;
    }

    internal void SetPayload(
        string type,
        JArray facts,
        string summary,
        int weight,
        string source)
    {
        Type = string.IsNullOrWhiteSpace(type) ? "shared_experience" : type;
        Facts = facts == null ? new JArray() : (JArray)facts.DeepClone();
        Summary = summary ?? string.Empty;
        Weight = weight;
        Source = string.IsNullOrWhiteSpace(source) ? EntrySource : source;
        HasPayload = true;
    }

    internal WorldStateCommand BuildCommand()
    {
        return WorldStateStore.BuildMemoryCommand(
            HeroId,
            ConversationId,
            "append",
            Day,
            Type,
            Facts,
            Summary,
            Weight,
            Source,
            Sequence);
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
    internal DataAccessScope AccessScope { get; }
    internal SourceClass SourceClass { get; }
    internal EpistemicStatus EpistemicStatus { get; }
    internal int Attempts { get; set; }

    internal WorldPendingEvent(
        string eventId,
        string commandId,
        string heroId,
        JObject payload,
        string correlationId,
        string eventKind = null,
        string eventSchema = null,
        DataAccessScope accessScope = DataAccessScope.SensitiveExtension,
        SourceClass sourceClass = SourceClass.ExtensionProvider,
        EpistemicStatus epistemicStatus = EpistemicStatus.Fact)
    {
        EventId = eventId ?? Guid.NewGuid().ToString("N");
        CommandId = commandId ?? string.Empty;
        HeroId = heroId ?? string.Empty;
        Payload = payload ?? new JObject();
        CorrelationId = correlationId ?? string.Empty;
        EventKind = eventKind ?? string.Empty;
        EventSchema = eventSchema ?? string.Empty;
        AccessScope = accessScope;
        SourceClass = sourceClass;
        EpistemicStatus = epistemicStatus;
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
    internal bool Duplicate { get; set; }
    internal bool Retryable { get; set; }
    internal bool CommitUnknown { get; set; }
    internal int Attempts { get; set; }
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
    internal bool OwnerApplied { get; set; }
    internal bool OwnerDuplicate { get; set; }
    internal bool OwnerRetryable { get; set; }
    internal bool OwnerCommitUnknown { get; set; }
    internal int OwnerAttempts { get; set; }
    internal string OwnerCode { get; set; }
}

internal sealed class WorldFinalDrainResult
{
    internal WorldFinalDrainResult(
        bool succeeded,
        int pendingWrites,
        int pendingEvents,
        int droppedItems,
        string errorCode)
    {
        Succeeded = succeeded;
        PendingWrites = pendingWrites;
        PendingEvents = pendingEvents;
        DroppedItems = droppedItems;
        ErrorCode = errorCode ?? string.Empty;
    }

    internal bool Succeeded { get; }
    internal int PendingWrites { get; }
    internal int PendingEvents { get; }
    internal int DroppedItems { get; }
    internal string ErrorCode { get; }

    internal static WorldFinalDrainResult NotStarted()
    {
        return new WorldFinalDrainResult(true, 0, 0, 0, string.Empty);
    }
}

internal sealed class WorldCommandResultRecord
{
    internal string CommandId { get; set; }
    internal string IdempotencyKey { get; set; }
    internal bool Applied { get; set; }
    internal bool Duplicate { get; set; }
    internal bool Retryable { get; set; }
    internal bool CommitUnknown { get; set; }
    internal int Attempts { get; set; }
    internal string Code { get; set; }
    internal int EventPublishFailureCount { get; set; }
}

internal sealed class WorldStateStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> LogicalKeyGates =
        new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

    /// <summary>
    /// 判断一次读取是不是"这个 key 不存在"。
    ///
    /// ⚠️ 2026-09-15 定案：**真后端不会产出 `storage.key_not_found`**。AWAKE 的文件后端
    /// （AwakeFileStorageService.cs:142-143）对缺 key 返回的是 **Succeeded("")**（成功＋空串），
    /// 全仓唯一产出该错误码的地方在 tools/ 下的**离线替身**里。故本判断在真后端上**恒为 False**。
    /// 逐处核过：15 个调用点全部嵌在 `if (!loaded.IsSuccess)` 之内，它实际只决定"要不要打一条失败日志"，
    /// **没有一处因此发生功能故障** —— 所以这里**刻意不改行为**（改了要动 15 处语义，收益近零）。
    ///
    /// ⇒ 真正的规矩是：**凡"缺 key 就用默认值"的逻辑，必须另有 `IsNullOrWhiteSpace(值)` 兜底。**
    ///   缺了那半行就会出永久死锁（世界事实日志 2026-09-15 就是这么坏的：
    ///   读空值判成 Corrupt ⇒ 写侧放弃写入 ⇒ 永远空 ⇒ 永远读坏）。
    /// </summary>
    private static bool IsStorageKeyNotFound(OperationResult<string> result)
    {
        return result != null
            && !result.IsSuccess
            && StringComparer.Ordinal.Equals(result.Error?.Code, "storage.key_not_found");
    }

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
    private readonly SemaphoreSlim _namespaceOpenGate = new SemaphoreSlim(1, 1);
    private static readonly SemaphoreSlim WorldFactJournalWriterGate = new SemaphoreSlim(1, 1);
    private bool _sessionEnded;
    private bool _memoryReservationsQueuedForFinalDrain;
    private WorldStateStoreLifecycle _lifecycleState = WorldStateStoreLifecycle.Active;
    private Task<WorldFinalDrainResult> _finalDrainTask;
    private int _activeDirectMemoryWrites;
    private TaskCompletionSource<bool> _directMemoryWritesCompletion;
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

    internal SessionRef Session => _sessionRef;

    internal PersonaTimelineIdentity BuildPersonaTimeline()
    {
        return new PersonaTimelineIdentity
        {
            CampaignId = _sessionRef.CampaignId ?? string.Empty,
            TimelineId = _sessionRef.TimelineId ?? string.Empty,
            BranchId = "root",
            ParentBranchId = string.Empty,
            ForkSequence = 0
        };
    }

    internal WorldStateStoreLifecycle LifecycleState
    {
        get { lock (_gate) return _lifecycleState; }
    }

    internal bool HasNamespaces(IReadOnlyCollection<string> namespaceIds)
    {
        if (namespaceIds == null || namespaceIds.Count == 0) return true;
        lock (_gate)
        {
            foreach (string namespaceId in namespaceIds)
            {
                if (string.IsNullOrWhiteSpace(namespaceId) || !_stores.ContainsKey(namespaceId)) return false;
            }
            return true;
        }
    }

    internal Task<bool> OpenNamespacesAsync(CancellationToken cancellationToken)
    {
        return OpenNamespacesAsync(cancellationToken, null);
    }

    internal async Task<bool> OpenNamespacesAsync(
        CancellationToken cancellationToken,
        IReadOnlyCollection<string> requiredNamespaces)
    {
        HashSet<string> targetNamespaceSet = new HashSet<string>(AiTaskConstants.StorageNamespaceIds, StringComparer.Ordinal);
        if (requiredNamespaces != null)
        {
            foreach (string namespaceId in requiredNamespaces)
                if (!string.IsNullOrWhiteSpace(namespaceId)) targetNamespaceSet.Add(namespaceId);
        }
        string[] targetNamespaces = targetNamespaceSet.ToArray();
        if (targetNamespaces.Length == 0) return false;

        await _namespaceOpenGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, IKeyValueStore> stagedStores = new Dictionary<string, IKeyValueStore>(StringComparer.Ordinal);
            lock (_gate)
            {
                foreach (string namespaceId in targetNamespaces)
                {
                    IKeyValueStore existing;
                    if (!string.IsNullOrWhiteSpace(namespaceId)
                        && _stores.TryGetValue(namespaceId, out existing)
                        && existing != null)
                    {
                        stagedStores[namespaceId] = existing;
                    }
                }
            }

            foreach (string namespaceId in targetNamespaces)
            {
                if (string.IsNullOrWhiteSpace(namespaceId)) return false;
                if (stagedStores.ContainsKey(namespaceId)) continue;
                if (_host == null || _host.Storage == null)
                {
                    AwakeLog.Write("world_state_namespace_open_unavailable namespace=" + namespaceId);
                    return false;
                }

                try
                {
                    RequestContext context = CreateContext();
                    OperationResult<IKeyValueStore> result = await _host.Storage.OpenCampaignNamespaceAsync(
                        namespaceId,
                        context,
                        cancellationToken).ConfigureAwait(false);
                    if (!result.IsSuccess || result.Value == null)
                    {
                        AwakeLog.Write("world_state_namespace_open_failed namespace=" + namespaceId
                            + " code=" + (result.Error?.Code ?? "unknown"));
                        return false;
                    }
                    stagedStores[namespaceId] = result.Value;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("world_state_namespace_open_error namespace=" + namespaceId + " error=" + ex.Message);
                    return false;
                }
            }

            lock (_gate)
            {
                if (_sessionEnded) return false;
                foreach (KeyValuePair<string, IKeyValueStore> pair in stagedStores)
                {
                    _stores[pair.Key] = pair.Value;
                }
            }
            return HasNamespaces(targetNamespaces);
        }
        finally
        {
            _namespaceOpenGate.Release();
        }
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
    internal bool BeginSessionEnd()
    {
        lock (_gate)
        {
            if (_lifecycleState != WorldStateStoreLifecycle.Active) return false;
            _lifecycleState = WorldStateStoreLifecycle.Ending;
            _sessionEnded = true;
            return true;
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
        MemoryReservation reservation = null;
        lock (_gate)
        {
            if (_memoryReservations.TryGetValue(conversationId, out reservation))
            {
                sequence = reservation.Sequence;
                reservation.SetPayload(
                    type,
                    facts,
                    summary,
                    weight,
                    string.IsNullOrWhiteSpace(source) ? "npc_dialogue" : source);
                reservation.State = MemoryReservationState.Enqueued;
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
        if (!TryEnqueue(command))
        {
            if (reservation != null)
            {
                lock (_gate) reservation.State = MemoryReservationState.Reserved;
            }
            return false;
        }
        WorldDrainSummary drainSummary = await DrainAsync(
            command.CommandId,
            command.IdempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (!IsPersisted(drainSummary))
        {
            AwakeLog.Write("world_state_memory_flush_unsettled conversation=" + conversationId
                + " retryable=" + drainSummary.OwnerRetryable
                + " unknown=" + drainSummary.OwnerCommitUnknown
                + " code=" + (drainSummary.OwnerCode ?? "none"));
            return false;
        }
        if (reservation != null && IsPersisted(drainSummary))
        {
            lock (_gate) _memoryReservations.Remove(conversationId);
        }
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
        WorldDrainSummary drainSummary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(drainSummary);
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
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
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
            if (!IsStorageKeyNotFound(loaded))
            {
                AwakeLog.Write("world_state_memory_load_failed hero=" + heroId + " code=" + (loaded.Error?.Code ?? "unknown"));
            }
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
            if (!IsStorageKeyNotFound(loaded))
            {
                AwakeLog.Write("world_state_relationship_load_failed hero=" + heroId + " code=" + (loaded.Error?.Code ?? "unknown"));
            }
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
            if (!IsStorageKeyNotFound(loaded))
            {
                AwakeLog.Write("world_state_event_meta_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            }
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
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<bool> ConsolidateMemoryAsync(
        string heroId,
        JArray memories,
        JArray promises,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(heroId) || memories == null || promises == null) return false;
        JObject arguments = new JObject
        {
            ["mode"] = "consolidate",
            ["memories"] = (JArray)memories.DeepClone(),
            ["promises"] = (JArray)promises.DeepClone()
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.NpcMemoriesNamespace,
            HeroKey(heroId),
            "awake.memory.consolidate",
            string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey,
            heroId,
            WorldStateKind.Memory,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
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
            if (!IsStorageKeyNotFound(loaded))
            {
                AwakeLog.Write("world_state_proactive_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            }
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
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<JObject> GetWorldEventsAsync(RequestContext context, CancellationToken cancellationToken)
    {
        if (!IsBusinessOperationOpen()) return null;
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.WorldEventsNamespace, out store);
        if (store == null) return null;

        OperationResult<string> loaded = await store.GetAsync(
            AiTaskConstants.WorldEventsKey,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!IsBusinessOperationOpen()) return null;
        if (!loaded.IsSuccess)
        {
            if (IsStorageKeyNotFound(loaded)) return NewWorldEventsState();
            AwakeLog.Write("world_state_world_events_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return NewWorldEventsState();
        try
        {
            JObject doc = ParseJsonObjectPreservingDateStrings(loaded.Value);
            if (doc.Type != JTokenType.Object) throw new InvalidOperationException("world events root is not object");
            EnsureWorldEventsShape(doc);
            return doc;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_world_events_corrupt error=" + ex.Message);
            return null;
        }
    }

    internal async Task<WorldFactJournalReadResult> GetWorldFactJournalAsync(RequestContext context, CancellationToken cancellationToken)
    {
        if (!IsBusinessOperationOpen())
            return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Unavailable, errorCode: "awake.world_state.stale_store_session");
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.WorldFactJournalNamespace, out store);
        if (store == null)
            return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Unavailable, errorCode: "awake.world_fact.storage_unavailable");

        RequestContext readContext = context ?? CreateContext();
        OperationResult<string> rootValue = await store.GetAsync(
            AiTaskConstants.WorldFactJournalRootKey, readContext, cancellationToken).ConfigureAwait(false);
        if (!rootValue.IsSuccess)
        {
            if (IsStorageKeyNotFound(rootValue)) return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Missing);
            return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Unavailable,
                errorCode: rootValue.Error?.Code ?? "awake.world_fact.root_read_failed");
        }

        JObject root;
        WorldFactJournalReadStatus rootStatus = WorldFactJournalCodec.ReadRoot(rootValue.Value, out root);
        if (rootStatus == WorldFactJournalReadStatus.Missing || rootStatus == WorldFactJournalReadStatus.Empty)
            return new WorldFactJournalReadResult(rootStatus, revision: root == null ? 0 : IntValue(root["revision"]));
        if (rootStatus != WorldFactJournalReadStatus.Success)
            return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.root_corrupt");

        int rootStartDay = IntValue(root["startDay"]);
        int rootEndDay = IntValue(root["endDay"]);
        int expectedOrdinal = 0;
        var facts = new List<JObject>();
        foreach (string chunkKey in ((JArray)root["chunkKeys"]).Values<string>())
        {
            OperationResult<string> chunkValue = await store.GetAsync(chunkKey, readContext, cancellationToken).ConfigureAwait(false);
            if (!chunkValue.IsSuccess)
            {
                if (IsStorageKeyNotFound(chunkValue))
                    return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.chunk_missing");
                return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Unavailable,
                    errorCode: chunkValue.Error?.Code ?? "awake.world_fact.chunk_read_failed");
            }
            int chunkStart;
            int chunkEnd;
            int chunkRevision;
            int chunkOrdinal;
            string chunkHash;
            if (!WorldFactJournalCodec.TryParseChunkKey(chunkKey, out chunkStart, out chunkEnd, out chunkRevision, out chunkOrdinal, out chunkHash)
                || chunkOrdinal != expectedOrdinal++
                || chunkStart < rootStartDay
                || chunkEnd > rootEndDay
                || !StringComparer.OrdinalIgnoreCase.Equals(chunkHash, WorldFactJournalCodec.Sha256Hex(chunkValue.Value)))
                return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.chunk_key_mismatch");
            JObject chunkDocument;
            try { chunkDocument = JObject.Parse(chunkValue.Value); }
            catch (Exception) { return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.chunk_json_invalid"); }
            if (IntValue(chunkDocument["startDay"]) != chunkStart
                || IntValue(chunkDocument["endDay"]) != chunkEnd
                || IntValue(chunkDocument["revision"]) != chunkRevision
                || chunkRevision != IntValue(root["revision"]))
                return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: "awake.world_fact.chunk_bounds_mismatch");
            WorldFactJournalReadResult chunk = WorldFactJournalCodec.ReadChunk(chunkValue.Value);
            if (chunk.Status != WorldFactJournalReadStatus.Success)
                return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt,
                    errorCode: string.IsNullOrWhiteSpace(chunk.ErrorCode) ? "awake.world_fact.chunk_corrupt" : chunk.ErrorCode);
            facts.AddRange(chunk.Facts.Select(value => (JObject)value.DeepClone()));
        }
        return facts.Count == 0
            ? new WorldFactJournalReadResult(WorldFactJournalReadStatus.Empty, revision: IntValue(root["revision"]))
            : new WorldFactJournalReadResult(WorldFactJournalReadStatus.Success, facts, revision: IntValue(root["revision"]));
    }

    internal async Task<List<WeeklyReportApplicationState>> GetWeeklyReportStatesAsync(CancellationToken cancellationToken)
    {
        JObject state = await GetWorldEventsAsync(null, cancellationToken).ConfigureAwait(false);
        if (state == null) return null;
        var result = new List<WeeklyReportApplicationState>();
        foreach (JObject value in ((JArray)state["weeklyReports"] ?? new JArray()).Children<JObject>())
        {
            string reportId = (string)value["reportId"] ?? string.Empty;
            string schemaVersion = (string)value["schemaVersion"] ?? (string)value["report"]?["schemaVersion"] ?? string.Empty;
            JObject report = value["report"] as JObject;
            bool corrupt = !StringComparer.Ordinal.Equals(schemaVersion, string.Empty)
                && !StringComparer.Ordinal.Equals(schemaVersion, "awake.worldbook.weekly-report.v1")
                && !StringComparer.Ordinal.Equals(schemaVersion, "awake.worldbook.weekly-report.v2");
            if (!WorldEventContract.IsStableId(reportId) && !StringComparer.Ordinal.Equals(schemaVersion, "awake.worldbook.weekly-report.v2")) continue;
            if (StringComparer.Ordinal.Equals(schemaVersion, "awake.worldbook.weekly-report.v2"))
            {
                if (!IsValidV2WeeklyReportEntry(value, out report))
                {
                    report = null;
                    corrupt = true;
                }
            }
            else if (report != null
                && (!StringComparer.Ordinal.Equals((string)report["reportId"], reportId)
                    || !WorldEventContract.TryValidateWeeklyReport(report, out _)))
                report = null;
            result.Add(new WeeklyReportApplicationState
            {
                ReportId = reportId,
                SchemaVersion = schemaVersion,
                WindowStartDay = IntValue(value["windowStartDay"]),
                WindowEndDay = IntValue(value["windowEndDay"]),
                Status = (string)value["status"] ?? "retryable",
                AttemptCount = IntValue(value["attemptCount"]),
                LastAttemptDay = IntValue(value["lastAttemptDay"]),
                LastErrorCode = (string)value["lastErrorCode"] ?? string.Empty,
                ContentFingerprint = (string)value["contentFingerprint"] ?? string.Empty,
                SourceFactIds = ((JArray)value["sourceFactIds"] ?? new JArray()).Values<string>().ToArray(),
                Corrupt = corrupt,
                Report = report == null ? null : (JObject)report.DeepClone()
            });
        }
        return result;
    }

    internal async Task<WeeklyReportStateWriteResult> UpsertWeeklyReportStateAsync(
        string reportId,
        int windowStartDay,
        int windowEndDay,
        string status,
        int lastAttemptDay,
        string lastErrorCode,
        JObject report,
        CancellationToken cancellationToken)
    {
        if (!IsBusinessOperationOpen())
        {
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Retryable, Code = "awake.world_state.stale_store_session" };
        }
        if (report != null && StringComparer.Ordinal.Equals((string)report["schemaVersion"], "awake.worldbook.weekly-report.v2"))
            return await UpsertV2WeeklyReportStateAsync(report, reportId, windowStartDay, windowEndDay, status, lastAttemptDay, lastErrorCode, cancellationToken).ConfigureAwait(false);
        if (!WorldEventContract.IsStableId(reportId) || (status != "applied" && status != "retryable"))
        {
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Failed, Code = "awake.world_state.weekly_report.invalid" };
        }
        if (report != null
            && (!WorldEventContract.TryValidateWeeklyReport(report, out _)
                || !StringComparer.Ordinal.Equals((string)report["reportId"], reportId)))
        {
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Failed, Code = "awake.world_state.weekly_report.invalid_payload" };
        }
        JObject currentState = await GetWorldEventsAsync(null, cancellationToken).ConfigureAwait(false);
        if (currentState == null)
        {
            return new WeeklyReportStateWriteResult
            {
                Status = WeeklyReportStateWriteResult.Retryable,
                Code = SessionEnded ? "awake.world_state.stale_store_session" : "awake.world_state.weekly_report.read_failed"
            };
        }
        JObject current = ((JArray)currentState["weeklyReports"] ?? new JArray()).Children<JObject>()
            .FirstOrDefault(value => StringComparer.Ordinal.Equals((string)value["reportId"], reportId));
        bool currentApplied = StringComparer.Ordinal.Equals((string)current?["status"], "applied");
        bool currentSnapshotValid = currentApplied && IsValidWeeklyReportSnapshot(current["report"], reportId);
        if (currentApplied && (report == null || currentSnapshotValid))
        {
            return new WeeklyReportStateWriteResult
            {
                Status = WeeklyReportStateWriteResult.AlreadyApplied,
                Attempts = IntValue(current["attemptCount"]),
                Code = string.Empty,
                Report = current["report"] is JObject appliedReport ? (JObject)appliedReport.DeepClone() : null
            };
        }
        if (currentApplied && !currentSnapshotValid && status == "applied" && IsEmptyWeeklyReport(report))
        {
            return new WeeklyReportStateWriteResult
            {
                Status = WeeklyReportStateWriteResult.Retryable,
                Attempts = IntValue(current["attemptCount"]),
                Code = "awake.world_state.weekly_report.snapshot_unrecoverable"
            };
        }
        int attemptCount = IntValue(current?["attemptCount"]) + 1;
        string idempotencyKey = "awake:weekly-report-state:" + reportId + ":" + attemptCount;
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.WorldEventsNamespace,
            AiTaskConstants.WorldEventsKey,
            "awake.world.weekly_report_state",
            idempotencyKey,
            string.Empty,
            WorldStateKind.WorldEvents,
            new JObject
            {
                ["operation"] = "weekly_report_state",
                ["reportId"] = reportId,
                ["windowStartDay"] = windowStartDay,
                ["windowEndDay"] = windowEndDay,
                ["status"] = status,
                ["attemptCount"] = attemptCount,
                ["lastAttemptDay"] = lastAttemptDay,
                ["lastErrorCode"] = lastErrorCode ?? string.Empty,
                ["report"] = report == null ? null : (JObject)report.DeepClone()
            },
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command))
        {
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Retryable, Attempts = attemptCount, Code = "awake.world_state.enqueue_failed" };
        }
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (!IsBusinessOperationOpen())
        {
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Retryable, Attempts = attemptCount, Code = "awake.world_state.stale_store_session" };
        }
        if (summary.OwnerApplied)
        {
            return new WeeklyReportStateWriteResult
            {
                Status = status == "applied" ? WeeklyReportStateWriteResult.Applied : WeeklyReportStateWriteResult.Retryable,
                Attempts = attemptCount,
                Code = summary.OwnerCode,
                Report = report == null ? null : (JObject)report.DeepClone()
            };
        }
        if (summary.OwnerDuplicate && status == "applied")
        {
            return new WeeklyReportStateWriteResult
            {
                Status = WeeklyReportStateWriteResult.AlreadyApplied,
                Attempts = attemptCount,
                Code = summary.OwnerCode,
                Report = report == null ? null : (JObject)report.DeepClone()
            };
        }
        if (summary.OwnerRetryable || summary.OwnerCommitUnknown)
        {
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Retryable, Attempts = attemptCount, Code = summary.OwnerCode };
        }
        return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Failed, Attempts = attemptCount, Code = summary.HardFailureCode ?? summary.OwnerCode ?? "awake.world_state.weekly_report.write_failed" };
    }

    private async Task<WeeklyReportStateWriteResult> UpsertV2WeeklyReportStateAsync(
        JObject report,
        string reportId,
        int windowStartDay,
        int windowEndDay,
        string status,
        int lastAttemptDay,
        string lastErrorCode,
        CancellationToken cancellationToken)
    {
        if (status != "applied")
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Failed, Code = "awake.world_report.v2.invalid_status" };
        if (!WorldEventContract.IsStableId(reportId)
            || !WeeklyReportService.TryValidateV2Report(report, out _)
            || !StringComparer.Ordinal.Equals((string)report["reportId"], reportId))
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Failed, Code = "awake.world_report.v2.invalid_payload" };

        JObject currentState = await GetWorldEventsAsync(null, cancellationToken).ConfigureAwait(false);
        if (currentState == null)
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Retryable, Code = SessionEnded ? "awake.world_state.stale_store_session" : "awake.world_report.v2.read_failed" };
        JObject current = ((JArray)currentState["weeklyReports"] ?? new JArray()).Children<JObject>()
            .FirstOrDefault(value => StringComparer.Ordinal.Equals((string)value["reportId"], reportId));
        if (current != null)
        {
            string currentSchema = (string)current["schemaVersion"] ?? (string)current["report"]?["schemaVersion"] ?? string.Empty;
            if (StringComparer.Ordinal.Equals(currentSchema, "awake.worldbook.weekly-report.v1"))
                return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Conflict, Code = "awake.world_report.v2.v1_conflict" };
            if (StringComparer.Ordinal.Equals(currentSchema, "awake.worldbook.weekly-report.v2")
                && IsValidV2WeeklyReportEntry(current, out JObject currentReport))
            {
                if (SameV2WeeklyReport(current, currentReport, report, windowStartDay, windowEndDay))
                    return new WeeklyReportStateWriteResult
                    {
                        Status = WeeklyReportStateWriteResult.AlreadyApplied,
                        Attempts = IntValue(current["attemptCount"]),
                        Report = (JObject)currentReport.DeepClone()
                    };
                return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Conflict, Code = "awake.world_report.v2.conflict" };
            }
        }

        int attemptCount = IntValue(current?["attemptCount"]) + 1;
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.WorldEventsNamespace,
            AiTaskConstants.WorldEventsKey,
            "awake.world.weekly_report_state",
            "awake:weekly-report-v2-state:" + reportId + ":" + attemptCount,
            string.Empty,
            WorldStateKind.WorldEvents,
            new JObject
            {
                ["operation"] = "weekly_report_state",
                ["reportId"] = reportId,
                ["schemaVersion"] = "awake.worldbook.weekly-report.v2",
                ["windowStartDay"] = windowStartDay,
                ["windowEndDay"] = windowEndDay,
                ["status"] = "applied",
                ["attemptCount"] = attemptCount,
                ["lastAttemptDay"] = lastAttemptDay,
                ["lastErrorCode"] = lastErrorCode ?? string.Empty,
                ["contentFingerprint"] = (string)report["contentFingerprint"],
                ["sourceFactIds"] = report["sourceFactIds"].DeepClone(),
                ["report"] = (JObject)report.DeepClone()
            },
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command))
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.Retryable, Attempts = attemptCount, Code = "awake.world_report.v2.enqueue_failed" };
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (summary.OwnerApplied)
        {
            List<WeeklyReportApplicationState> readBack = await GetWeeklyReportStatesAsync(cancellationToken).ConfigureAwait(false);
            WeeklyReportApplicationState confirmed = readBack?.FirstOrDefault(value =>
                StringComparer.Ordinal.Equals(value.ReportId, reportId)
                && StringComparer.Ordinal.Equals(value.SchemaVersion, WeeklyReportService.V2SchemaVersion)
                && StringComparer.Ordinal.Equals(value.Status, "applied")
                && !value.Corrupt
                && value.Report != null
                && value.WindowStartDay == windowStartDay
                && value.WindowEndDay == windowEndDay
                && StringComparer.Ordinal.Equals(value.ContentFingerprint, (string)report["contentFingerprint"])
                && StringComparer.Ordinal.Equals(WeeklyReportService.CanonicalizeV2(value.Report), WeeklyReportService.CanonicalizeV2(report)));
            if (confirmed == null)
                return new WeeklyReportStateWriteResult
                {
                    Status = WeeklyReportStateWriteResult.Retryable,
                    Attempts = attemptCount,
                    Code = "awake.world_report.v2.readback_mismatch"
                };
            return new WeeklyReportStateWriteResult
            {
                Status = WeeklyReportStateWriteResult.Applied,
                Attempts = attemptCount,
                Report = (JObject)confirmed.Report.DeepClone()
            };
        }
        if (summary.OwnerDuplicate)
            return new WeeklyReportStateWriteResult { Status = WeeklyReportStateWriteResult.AlreadyApplied, Attempts = attemptCount, Code = summary.OwnerCode };
        return new WeeklyReportStateWriteResult
        {
            Status = WeeklyReportStateWriteResult.Retryable,
            Attempts = attemptCount,
            Code = summary.OwnerCode ?? summary.HardFailureCode ?? "awake.world_report.v2.write_failed"
        };
    }

    internal Task<WeeklyReportStateWriteResult> UpsertWeeklyReportStateAsync(
        string reportId,
        int windowStartDay,
        int windowEndDay,
        string status,
        int lastAttemptDay,
        string lastErrorCode,
        CancellationToken cancellationToken)
    {
        return UpsertWeeklyReportStateAsync(
            reportId,
            windowStartDay,
            windowEndDay,
            status,
            lastAttemptDay,
            lastErrorCode,
            null,
            cancellationToken);
    }

    internal async Task<WorldEventAppendResult> AppendWorldEventAsync(
        int day,
        string kind,
        string text,
        string idempotencyKey,
        string eventKey,
        string domain,
        DateTimeOffset occurredAt,
        IReadOnlyList<string> visibilityIdentityIds,
        CancellationToken cancellationToken,
        JObject structuredFact = null)
    {
        if (!IsBusinessOperationOpen())
        {
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.PersistenceRetryable,
                EventId = idempotencyKey ?? string.Empty,
                EventKey = eventKey ?? idempotencyKey ?? string.Empty,
                Code = "awake.world_state.stale_store_session"
            };
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.PersistenceFailed,
                Code = "awake.world_event.invalid_idempotency_key"
            };
        }
        JObject arguments = new JObject
        {
            ["operation"] = structuredFact == null ? null : "world_fact_append",
            ["day"] = day,
            ["kind"] = kind ?? "event",
            ["text"] = text ?? string.Empty,
            ["eventKey"] = eventKey ?? idempotencyKey,
            ["domain"] = domain ?? WeeklyReportService.InferDomain(kind),
            ["occurredAt"] = occurredAt.ToUniversalTime().ToString("O"),
            ["visibilityIdentityIds"] = new JArray(WorldEventAudience.Resolve(visibilityIdentityIds).Select(value => (object)value))
        };
        if (structuredFact != null) arguments["fact"] = structuredFact.DeepClone();
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.WorldEventsNamespace,
            AiTaskConstants.WorldEventsKey,
            "awake.world.events.append",
            idempotencyKey,
            string.Empty,
            WorldStateKind.WorldEvents,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command))
        {
            return new WorldEventAppendResult
            {
                Status = SessionEnded ? WorldEventAppendResult.PersistenceRetryable : WorldEventAppendResult.PersistenceFailed,
                EventId = idempotencyKey,
                EventKey = eventKey ?? idempotencyKey,
                Code = SessionEnded ? "awake.world_state.stale_store_session" : "awake.world_state.enqueue_failed"
            };
        }
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (!IsBusinessOperationOpen())
        {
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.PersistenceUnknown,
                EventId = idempotencyKey,
                EventKey = eventKey ?? idempotencyKey,
                Attempts = summary.OwnerAttempts,
                Code = "awake.world_state.stale_store_session"
            };
        }
        if (summary.OwnerDuplicate)
        {
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.DuplicateConfirmed,
                EventId = idempotencyKey,
                EventKey = eventKey ?? idempotencyKey,
                Attempts = summary.OwnerAttempts,
                Code = summary.OwnerCode
            };
        }
        if (summary.OwnerApplied)
        {
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.Persisted,
                EventId = idempotencyKey,
                EventKey = eventKey ?? idempotencyKey,
                Attempts = summary.OwnerAttempts,
                Code = summary.OwnerCode
            };
        }
        if (summary.OwnerCommitUnknown)
        {
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.PersistenceUnknown,
                EventId = idempotencyKey,
                EventKey = eventKey ?? idempotencyKey,
                Attempts = summary.OwnerAttempts,
                Code = summary.OwnerCode
            };
        }
        if (summary.OwnerRetryable)
        {
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.PersistenceRetryable,
                EventId = idempotencyKey,
                EventKey = eventKey ?? idempotencyKey,
                Attempts = summary.OwnerAttempts,
                Code = summary.OwnerCode
            };
        }
        if (StringComparer.Ordinal.Equals(summary.HardFailureCode, "awake.world_state.key_conflict"))
        {
            return new WorldEventAppendResult
            {
                Status = WorldEventAppendResult.KeyConflict,
                EventId = idempotencyKey,
                EventKey = eventKey ?? idempotencyKey,
                Attempts = summary.OwnerAttempts,
                Code = summary.HardFailureCode
            };
        }
        return new WorldEventAppendResult
        {
            Status = WorldEventAppendResult.PersistenceFailed,
            EventId = idempotencyKey,
            EventKey = eventKey ?? idempotencyKey,
            Attempts = summary.OwnerAttempts,
            Code = summary.HardFailureCode ?? summary.OwnerCode ?? "awake.world_state.append_failed"
        };
    }

    internal Task<WorldEventAppendResult> AppendWorldEventAsync(
        int day,
        string kind,
        string text,
        string idempotencyKey,
        string eventKey,
        string domain,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        return AppendWorldEventAsync(
            day,
            kind,
            text,
            idempotencyKey,
            eventKey,
            domain,
            occurredAt,
            null,
            cancellationToken);
    }

    internal async Task<JObject> GetMessengerAsync(RequestContext context, CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.MessengerNamespace, out store);
        if (store == null) return null;

        OperationResult<string> loaded = await store.GetAsync(
            AiTaskConstants.MessengerKey,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            if (!IsStorageKeyNotFound(loaded))
            {
                AwakeLog.Write("world_state_messenger_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            }
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return NewMessengerState();
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            if (doc.Type != JTokenType.Object) throw new InvalidOperationException("messenger root is not object");
            return doc;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_messenger_corrupt error=" + ex.Message);
            return null;
        }
    }

    internal async Task<bool> AppendMessengerMessageAsync(
        string targetId,
        string speaker,
        string text,
        int day,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        JObject arguments = new JObject
        {
            ["targetId"] = targetId,
            ["speaker"] = speaker ?? string.Empty,
            ["text"] = text ?? string.Empty,
            ["day"] = day
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.MessengerNamespace,
            AiTaskConstants.MessengerKey,
            "awake.messenger.append",
            idempotencyKey,
            string.Empty,
            WorldStateKind.Messenger,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<JObject> GetOnboardingAsync(RequestContext context, CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.OnboardingNamespace, out store);
        if (store == null) return null;
        OperationResult<string> loaded = await store.GetAsync(
            AiTaskConstants.OnboardingKey,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            if (!IsStorageKeyNotFound(loaded))
            {
                AwakeLog.Write("world_state_onboarding_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            }
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return NewOnboardingState();
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            return doc.Type == JTokenType.Object ? doc : null;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_onboarding_corrupt error=" + ex.Message);
            return null;
        }
    }

    internal async Task<bool> UpdateOnboardingAsync(
        IReadOnlyList<string> completedSteps,
        bool skippedThisCampaign,
        bool permanentlySkipped,
        int lastReminderDay,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        JObject arguments = new JObject
        {
            ["completedSteps"] = completedSteps == null
                ? new JArray()
                : new JArray(completedSteps),
            ["skippedThisCampaign"] = skippedThisCampaign,
            ["permanentlySkipped"] = permanentlySkipped,
            ["lastReminderDay"] = lastReminderDay
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.OnboardingNamespace,
            AiTaskConstants.OnboardingKey,
            AiTaskConstants.OnboardingUpsertCommandId,
            idempotencyKey,
            string.Empty,
            WorldStateKind.Onboarding,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<JObject> GetLettersAsync(RequestContext context, CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.LettersNamespace, out store);
        if (store == null) return null;
        OperationResult<string> loaded = await store.GetAsync(
            AiTaskConstants.LettersKey,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            if (!IsStorageKeyNotFound(loaded))
            {
                AwakeLog.Write("world_state_letters_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            }
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return LetterLedgerDocument.NewState();
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            return doc.Type == JTokenType.Object ? doc : null;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_letters_corrupt error=" + ex.Message);
            return null;
        }
    }

    internal async Task<bool> UpdateLettersAsync(
        JObject arguments,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.LettersNamespace,
            AiTaskConstants.LettersKey,
            AiTaskConstants.LettersUpsertCommandId,
            idempotencyKey,
            string.Empty,
            WorldStateKind.Letters,
            arguments ?? new JObject(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<JObject> GetDialogueQueueAsync(RequestContext context, CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.DialogueQueueNamespace, out store);
        if (store == null) return null;
        OperationResult<string> loaded = await store.GetAsync(
            AiTaskConstants.DialogueQueueKey,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            if (!IsStorageKeyNotFound(loaded))
            {
                AwakeLog.Write("world_state_dialogue_queue_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            }
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return NewDialogueQueueState();
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            return doc.Type == JTokenType.Object ? doc : null;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_dialogue_queue_corrupt error=" + ex.Message);
            return null;
        }
    }

    internal async Task<bool> EnqueueDialogueAsync(
        string id,
        string source,
        string targetId,
        string canonicalContactKey,
        string openingHint,
        string motive,
        int day,
        int expiryDay,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }
        JObject arguments = new JObject
        {
            ["id"] = id,
            ["source"] = source ?? "unknown",
            ["targetId"] = targetId,
            ["canonicalContactKey"] = canonicalContactKey ?? targetId,
            ["openingHint"] = openingHint ?? string.Empty,
            ["motive"] = motive ?? string.Empty,
            ["day"] = day,
            ["expiryDay"] = expiryDay,
            ["state"] = "pending"
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.DialogueQueueNamespace,
            AiTaskConstants.DialogueQueueKey,
            AiTaskConstants.DialogueQueueEnqueueCommandId,
            idempotencyKey,
            string.Empty,
            WorldStateKind.PendingDialogue,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<bool> ConsumeDialogueAsync(
        string id,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        JObject arguments = new JObject
        {
            ["id"] = id,
            ["state"] = "consumed"
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.DialogueQueueNamespace,
            AiTaskConstants.DialogueQueueKey,
            AiTaskConstants.DialogueQueueConsumeCommandId,
            idempotencyKey,
            string.Empty,
            WorldStateKind.PendingDialogue,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<JObject> GetInteractionsAsync(
        string canonicalContactKey,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(canonicalContactKey)) return null;
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.InteractionsNamespace, out store);
        if (store == null) return null;
        string key = BuildInteractionKey(canonicalContactKey);
        OperationResult<string> loaded = await store.GetAsync(
            key,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            if (!IsStorageKeyNotFound(loaded))
            {
                AwakeLog.Write("world_state_interactions_load_failed key=" + key + " code=" + (loaded.Error?.Code ?? "unknown"));
            }
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return NewInteractionState(canonicalContactKey);
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            return doc.Type == JTokenType.Object ? doc : null;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_interactions_corrupt key=" + key + " error=" + ex.Message);
            return null;
        }
    }

    internal async Task<JObject> GetInteractionRecoveryIndexAsync(
        RequestContext context,
        CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.InteractionsNamespace, out store);
        if (store == null) return null;
        OperationResult<string> loaded = await store.GetAsync(
            AiTaskConstants.InteractionsRecoveryIndexKey,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            if (IsStorageKeyNotFound(loaded)) return NewInteractionRecoveryIndexState();
            AwakeLog.Write("world_state_interactions_index_load_failed code=" + (loaded.Error?.Code ?? "unknown"));
            return null;
        }
        if (string.IsNullOrWhiteSpace(loaded.Value)) return NewInteractionRecoveryIndexState();
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            return doc.Type == JTokenType.Object ? doc : null;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_interactions_index_corrupt error=" + ex.Message);
            return null;
        }
    }

    internal async Task<bool> UpdateInteractionRecoveryIndexAsync(
        string canonicalContactKey,
        string interactionId,
        bool pending,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        JObject arguments = new JObject
        {
            ["canonicalContactKey"] = canonicalContactKey ?? string.Empty,
            ["interactionId"] = interactionId ?? string.Empty,
            ["pending"] = pending,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O")
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.InteractionsNamespace,
            AiTaskConstants.InteractionsRecoveryIndexKey,
            AiTaskConstants.InteractionsIndexUpdateCommandId,
            idempotencyKey,
            string.Empty,
            WorldStateKind.InteractionIndex,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<bool> UpsertPromiseAsync(
        string canonicalContactKey,
        JObject promise,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(canonicalContactKey) || promise == null || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }
        JObject arguments = new JObject
        {
            ["mode"] = "promise_upsert",
            ["promise"] = (JObject)promise.DeepClone()
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.InteractionsNamespace,
            BuildInteractionKey(canonicalContactKey),
            AiTaskConstants.PromiseRequestCommandId,
            idempotencyKey,
            canonicalContactKey,
            WorldStateKind.Interaction,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<bool> UpdatePromiseStatusAsync(
        string canonicalContactKey,
        string promiseId,
        string newStatus,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(canonicalContactKey)
            || string.IsNullOrWhiteSpace(promiseId)
            || string.IsNullOrWhiteSpace(newStatus)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }
        JObject arguments = new JObject
        {
            ["mode"] = "promise_update",
            ["promiseId"] = promiseId,
            ["newStatus"] = newStatus,
            ["reason"] = reason ?? string.Empty
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.InteractionsNamespace,
            BuildInteractionKey(canonicalContactKey),
            AiTaskConstants.PromiseUpdateCommandId,
            idempotencyKey,
            canonicalContactKey,
            WorldStateKind.Interaction,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<JObject> GetTranscriptChunkAsync(
        string contactKey,
        int chunkIndex,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.TranscriptNamespace, out store);
        if (store == null) return null;
        string key = AwakeTranscriptKeys.TranscriptChunkKey(contactKey, chunkIndex);
        OperationResult<string> loaded = await store.GetAsync(key, context ?? CreateContext(), cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess) return null;
        if (string.IsNullOrWhiteSpace(loaded.Value)) return null;
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            return doc.Type == JTokenType.Object ? doc : null;
        }
        catch
        {
            return null;
        }
    }

    internal async Task<bool> AppendTranscriptAsync(
        string contactKey,
        int chunkIndex,
        AwakeTranscriptLine line,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await AppendTranscriptLinesAsync(
            contactKey,
            chunkIndex,
            new[] { line },
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<bool> AppendTranscriptLinesAsync(
        string contactKey,
        int chunkIndex,
        IReadOnlyList<AwakeTranscriptLine> lines,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contactKey) || lines == null || lines.Count == 0 || string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        JArray lineArray = new JArray();
        foreach (AwakeTranscriptLine line in lines)
        {
            if (line == null) return false;
            lineArray.Add(line.ToJson());
        }
        JObject arguments = new JObject
        {
            ["mode"] = "append",
            ["contactKey"] = contactKey,
            ["chunkIndex"] = chunkIndex,
            ["lines"] = lineArray
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.TranscriptNamespace,
            AwakeTranscriptKeys.TranscriptChunkKey(contactKey, chunkIndex),
            AiTaskConstants.TranscriptAppendCommandId,
            idempotencyKey,
            string.Empty,
            WorldStateKind.Transcript,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        bool persisted = IsPersisted(summary);
        if (!persisted)
        {
            AwakeLog.Write("transcript_write_not_persisted key=" + contactKey + " code=" + (summary.HardFailureCode ?? "deferred"));
        }
        return persisted;
    }

    internal async Task<bool> PinTranscriptAsync(
        string contactKey,
        int chunkIndex,
        string lineId,
        bool pin,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contactKey) || string.IsNullOrWhiteSpace(lineId) || string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        JObject arguments = new JObject
        {
            ["mode"] = "pin",
            ["contactKey"] = contactKey,
            ["chunkIndex"] = chunkIndex,
            ["lineId"] = lineId,
            ["pin"] = pin
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.TranscriptNamespace,
            AwakeTranscriptKeys.TranscriptChunkKey(contactKey, chunkIndex),
            AiTaskConstants.TranscriptPinCommandId,
            idempotencyKey,
            string.Empty,
            WorldStateKind.Transcript,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return IsPersisted(summary);
    }

    internal async Task<bool> EnsureContactAsync(
        string contactKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await EnsureContactAsync(contactKey, string.Empty, idempotencyKey, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<bool> EnsureContactAsync(
        string contactKey,
        string displayName,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contactKey) || string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        JObject arguments = new JObject { ["contactKey"] = contactKey };
        if (!string.IsNullOrWhiteSpace(displayName)) arguments["displayName"] = displayName;
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.ContactsNamespace,
            AwakeTranscriptKeys.ContactsKey,
            AiTaskConstants.ContactsUpsertCommandId,
            idempotencyKey,
            string.Empty,
            WorldStateKind.Contacts,
            arguments,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return false;
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        bool persisted = IsPersisted(summary);
        if (!persisted)
        {
            AwakeLog.Write("contact_write_not_persisted key=" + contactKey + " code=" + (summary.HardFailureCode ?? "deferred"));
        }
        return persisted;
    }

    internal async Task<JObject> GetContactsAsync(RequestContext context, CancellationToken cancellationToken)
    {
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.ContactsNamespace, out store);
        if (store == null) return null;
        OperationResult<string> loaded = await store.GetAsync(
            AwakeTranscriptKeys.ContactsKey,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess || string.IsNullOrWhiteSpace(loaded.Value)) return null;
        try
        {
            JObject doc = JObject.Parse(loaded.Value);
            return doc.Type == JTokenType.Object ? doc : null;
        }
        catch
        {
            return null;
        }
    }

    internal async Task<PersonaRuntimeStateDocument> GetPersonaStateAsync(
        string key,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.PersonaStateNamespace, out store);
        if (store == null) return null;
        OperationResult<string> loaded = await store.GetAsync(
            key,
            context ?? CreateContext(),
            cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            if (!IsStorageKeyNotFound(loaded))
                AwakeLog.Write("persona.persistence.load_failed key=" + key + " code=" + (loaded.Error?.Code ?? "unknown"));
            return null;
        }
        PersonaRuntimeStateDocument document = PersonaRuntimeStateDocument.FromJson(loaded.Value);
        if (document == null)
        {
            AwakeLog.Write("persona.persistence.rejected key=" + key + " reason=persona.runtime_state.json_invalid");
        }
        return document;
    }

    private async Task<PersonaStateWriteResult> UpsertPersonaStateDocumentAsync(
        PersonaRuntimeStateDocument document,
        string idempotencyKey,
        CancellationToken cancellationToken,
        RuntimeBundle expectedBundle)
    {
        string validationError;
        RuntimeBundle activeBundle = WorldbookRuntime.Persona?.Bundle;
        if (!PersonaPersistenceValidator.TryValidateRuntimeState(
            document,
            BuildPersonaTimeline(),
            null,
            expectedBundle ?? activeBundle,
            out validationError))
            return new PersonaStateWriteResult { Code = validationError };
        string key;
        if (!PersonaStorageKey.TryBuild(document.Timeline, document.PersonaSubjectStableId, out key, out validationError))
            return new PersonaStateWriteResult { Code = validationError };
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return new PersonaStateWriteResult { Code = "persona.persistence.idempotency_missing" };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.PersonaStateNamespace,
            key,
            "awake.persona.continuity.upsert",
            idempotencyKey,
            document.PersonaSubjectStableId,
            WorldStateKind.PersonaContinuity,
            new JObject { ["document"] = document.ToJsonObject() },
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
        if (!TryEnqueue(command)) return new PersonaStateWriteResult { Code = "persona.persistence.session_unavailable" };
        WorldDrainSummary summary = await DrainAsync(command.CommandId, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return new PersonaStateWriteResult
        {
            Applied = summary.OwnerApplied,
            Duplicate = summary.OwnerDuplicate,
            CommitUnknown = summary.OwnerCommitUnknown,
            Code = summary.OwnerCode ?? string.Empty
        };
    }

    internal Task<PersonaStateWriteResult> UpsertPersonaStateAsync(
        AwakeNpcTarget target,
        PersonaRuntimeStateDocument document,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Hero mainHero;
        try { mainHero = Hero.MainHero; }
        catch { mainHero = null; }
        return UpsertPersonaStateWithEligibilityAsync(target, mainHero, document, idempotencyKey, cancellationToken);
    }

#if AWAKE_TESTS
    internal Task<PersonaStateWriteResult> UpsertPersonaStateAsync(
        AwakeNpcTarget target,
        Hero mainHero,
        PersonaRuntimeStateDocument document,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return UpsertPersonaStateWithEligibilityAsync(target, mainHero, document, idempotencyKey, cancellationToken);
    }
#endif

    private Task<PersonaStateWriteResult> UpsertPersonaStateWithEligibilityAsync(
        AwakeNpcTarget target,
        Hero mainHero,
        PersonaRuntimeStateDocument document,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Hero hero;
        string subjectStableId;
        if (!PersonaSubjectEligibility.TryGetEligibleHeroSubject(target, mainHero, out hero, out subjectStableId))
        {
            return Task.FromResult(new PersonaStateWriteResult { Code = "persona.persistence.subject_ineligible" });
        }
        if (document == null || !StringComparer.Ordinal.Equals(document.PersonaSubjectStableId, subjectStableId))
        {
            return Task.FromResult(new PersonaStateWriteResult { Code = "persona.persistence.subject_mismatch" });
        }
        RuntimeBundle activeBundle = WorldbookRuntime.Persona?.Bundle;
        if (activeBundle == null)
            return Task.FromResult(new PersonaStateWriteResult { Code = "persona.persistence.bundle_unavailable" });
        return UpsertPersonaStateDocumentAsync(document, idempotencyKey, cancellationToken, activeBundle);
    }

#if AWAKE_TESTS
    internal Task<PersonaStateWriteResult> UpsertPersonaStateForTestingAsync(
        PersonaRuntimeStateDocument document,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return UpsertPersonaStateDocumentAsync(document, idempotencyKey, cancellationToken, null);
    }
#endif

    internal Task<WorldFinalDrainResult> BeginFinalDrainAsync()
    {
        TaskCompletionSource<WorldFinalDrainResult> completion = null;
        Task<WorldFinalDrainResult> existingTask;
        Task directMemoryWritesTask = Task.CompletedTask;
        lock (_gate)
        {
            if (_lifecycleState == WorldStateStoreLifecycle.Active)
            {
                _lifecycleState = WorldStateStoreLifecycle.Ending;
                _sessionEnded = true;
            }
            if (_lifecycleState == WorldStateStoreLifecycle.Ending && !_memoryReservationsQueuedForFinalDrain)
            {
                foreach (MemoryReservation reservation in _memoryReservations.Values)
                {
                    _pendingWrites.Enqueue(reservation.BuildCommand());
                }
                _memoryReservationsQueuedForFinalDrain = true;
            }
            if (_activeDirectMemoryWrites > 0)
            {
                if (_directMemoryWritesCompletion == null)
                {
                    _directMemoryWritesCompletion = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }
                directMemoryWritesTask = _directMemoryWritesCompletion.Task;
            }
            existingTask = _finalDrainTask;
            if (existingTask == null)
            {
                completion = new TaskCompletionSource<WorldFinalDrainResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _finalDrainTask = completion.Task;
                existingTask = completion.Task;
            }
        }

        if (completion == null) return existingTask;
        _ = CompleteFinalDrainAsync(completion, directMemoryWritesTask);
        return existingTask;
    }

    private async Task CompleteFinalDrainAsync(
        TaskCompletionSource<WorldFinalDrainResult> completion,
        Task directMemoryWritesTask)
    {
        await Task.Yield();
        WorldFinalDrainResult result = null;
        try
        {
            await directMemoryWritesTask.ConfigureAwait(false);
            await DrainAsync(CancellationToken.None).ConfigureAwait(false);
            int pendingWrites;
            int pendingEvents;
            int droppedItems;
            lock (_gate)
            {
                pendingWrites = _pendingWrites.Count;
                pendingEvents = _pendingEvents.Count;
                droppedItems = _droppedItems;
            }
            bool succeeded = pendingWrites == 0 && pendingEvents == 0 && droppedItems == 0;
            string errorCode = succeeded
                ? string.Empty
                : BuildFinalDrainErrorCode(pendingWrites, pendingEvents, droppedItems);
            result = new WorldFinalDrainResult(
                succeeded,
                pendingWrites,
                pendingEvents,
                droppedItems,
                errorCode);
            if (!succeeded)
            {
                AwakeLog.Write("world_state_final_drain_failed pending_writes=" + pendingWrites
                    + " pending_events=" + pendingEvents
                    + " dropped=" + droppedItems
                    + " code=" + errorCode);
            }
            else
            {
                AwakeLog.Write("world_state_final_drain_complete");
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_state_final_drain_error code=awake.world_state.final_drain_error error=" + ex.Message);
            result = new WorldFinalDrainResult(
                false,
                _pendingWrites.Count,
                _pendingEvents.Count,
                Volatile.Read(ref _droppedItems),
                "awake.world_state.final_drain_error");
        }
        finally
        {
            lock (_gate)
            {
                _lifecycleState = WorldStateStoreLifecycle.Ended;
                _sessionEnded = true;
                _memoryReservations.Clear();
            }
            completion.TrySetResult(result ?? new WorldFinalDrainResult(
                false,
                _pendingWrites.Count,
                _pendingEvents.Count,
                Volatile.Read(ref _droppedItems),
                "awake.world_state.final_drain_error"));
        }
    }

    private static string BuildFinalDrainErrorCode(int pendingWrites, int pendingEvents, int droppedItems)
    {
        if (droppedItems > 0) return "awake.world_state.final_drain_dropped";
        if (pendingWrites > 0 || pendingEvents > 0) return "awake.world_state.final_drain_pending";
        return "awake.world_state.final_drain_incomplete";
    }

    private bool IsBusinessOperationOpen()
    {
        lock (_gate) return _lifecycleState == WorldStateStoreLifecycle.Active && !_sessionEnded;
    }

    private bool TryBeginDirectMemoryWrite(out IKeyValueStore store)
    {
        store = null;
        if (!ReferenceEquals(AwakeRuntime.WorldStateStore, this)) return false;
        lock (_gate)
        {
            if (_lifecycleState != WorldStateStoreLifecycle.Active || _sessionEnded) return false;
            if (!_stores.TryGetValue(AiTaskConstants.NpcMemoriesNamespace, out store) || store == null)
            {
                store = null;
                return false;
            }
            _activeDirectMemoryWrites++;
            return true;
        }
    }

    private void CompleteDirectMemoryWrite()
    {
        TaskCompletionSource<bool> completion = null;
        lock (_gate)
        {
            if (_activeDirectMemoryWrites <= 0) return;
            _activeDirectMemoryWrites--;
            if (_activeDirectMemoryWrites == 0)
            {
                completion = _directMemoryWritesCompletion;
            }
        }
        if (completion != null) completion.TrySetResult(true);
    }

    private bool IsCurrentWorldStateStoreOpen()
    {
        return ReferenceEquals(AwakeRuntime.WorldStateStore, this) && IsBusinessOperationOpen();
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
            OwnerApplied = record != null && record.Applied,
            OwnerDuplicate = record != null && record.Duplicate,
            OwnerRetryable = record != null && record.Retryable,
            OwnerCommitUnknown = record != null && record.CommitUnknown,
            OwnerAttempts = record?.Attempts ?? 0,
            OwnerCode = record?.Code ?? string.Empty,
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

    private static bool IsPersisted(WorldDrainSummary summary)
    {
        return summary != null
            && summary.OwnerCommandObserved
            && (summary.OwnerApplied || summary.OwnerDuplicate)
            && summary.HardFailureCount == 0
            && !summary.OwnerRetryable
            && !summary.OwnerCommitUnknown
            && !summary.DeferredRetry;
    }

    internal async Task<bool> WriteEmptyMemoryAsync(string heroId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return false;
        IKeyValueStore store;
        if (!TryBeginDirectMemoryWrite(out store)) return false;
        try
        {
            JObject doc = new JObject
            {
                ["schema"] = "awake.npc.memory.v1",
                ["heroId"] = heroId,
                ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["memories"] = new JArray()
            };
            string key = HeroKey(heroId);
            return await WithLogicalKeyGateAsync(
                AiTaskConstants.NpcMemoriesNamespace,
                key,
                async () =>
                {
                    if (!IsCurrentWorldStateStoreOpen()) return false;
                    OperationResult<bool> stored = await store.SetAsync(
                        key,
                        doc.ToString(Formatting.None),
                        CreateContext(),
                        cancellationToken).ConfigureAwait(false);
                    return stored.IsSuccess && stored.Value;
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CompleteDirectMemoryWrite();
        }
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
            if (result.Retryable && command.Attempts >= AiTaskConstants.DrainMaximumRetries)
                result.Retryable = false;
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

    private Task<bool> DrainEventsOnceAsync(CancellationToken cancellationToken)
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
                pending.AccessScope,
                pending.SourceClass,
                pending.EpistemicStatus,
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
        return Task.FromResult(any);
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
                CommitUnknown = result.CommitUnknown,
                Attempts = command.Attempts,
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

    internal static WorldStateCommand BuildMemoryCommand(
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

    private static SemaphoreSlim GetLogicalKeyGate(string namespaceId, string key)
    {
        string gateKey = (namespaceId ?? string.Empty) + "\u001f" + (key ?? string.Empty);
        return LogicalKeyGates.GetOrAdd(gateKey, _ => new SemaphoreSlim(1, 1));
    }

    private static async Task<T> WithLogicalKeyGateAsync<T>(
        string namespaceId,
        string key,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = GetLogicalKeyGate(namespaceId, key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<WorldApplyResult> TryApplyAsync(WorldStateCommand command, RequestContext context, CancellationToken cancellationToken)
    {
        return WithLogicalKeyGateAsync(
            command.NamespaceId,
            command.Key,
            () => TryApplyCoreAsync(command, context, cancellationToken),
            cancellationToken);
    }

    private async Task<WorldApplyResult> TryApplyCoreAsync(WorldStateCommand command, RequestContext context, CancellationToken cancellationToken)
    {
        bool hasStructuredFact = command.Kind == WorldStateKind.WorldEvents && command.Arguments["fact"] is JObject;
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(command.NamespaceId, out store);
        if (hasStructuredFact)
        {
            WorldApplyResult journalResult = await AppendWorldFactJournalAsync((JObject)command.Arguments["fact"], context, cancellationToken).ConfigureAwait(false);
            if (journalResult.Duplicate)
                return new WorldApplyResult { Applied = true, Duplicate = true, Code = "awake.world_state.duplicate" };
            if (!journalResult.Applied) return journalResult;
            return new WorldApplyResult { Applied = true, Code = "awake.world_fact.persisted" };
        }
        if (store == null)
        {
            return new WorldApplyResult { Retryable = true, Code = "awake.world_state.storage_unavailable" };
        }

        OperationResult<string> loaded = await store.GetAsync(command.Key, context, cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess
            && !StringComparer.Ordinal.Equals(loaded.Error?.Code, "storage.key_not_found"))
        {
            return new WorldApplyResult { Retryable = true, Code = loaded.Error?.Code ?? "awake.world_state.read_failed" };
        }

        JObject state;
        if (!loaded.IsSuccess || string.IsNullOrWhiteSpace(loaded.Value))
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
        string expectedSchema = AwakeStorageContract.ExpectedSchema(command.Kind);
        if (!AwakeStorageContract.TryNormalizeSchema(state, expectedSchema))
        {
            AwakeLog.Write("world_state_schema_mismatch key=" + command.Key
                + " current=" + (string)state["schema"]
                + " expected=" + expectedSchema);
        }

        if (command.Kind != WorldStateKind.EventMeta && !IsWeeklyReportStateCommand(command))
        {
            // 安全取值：未知 Kind 由状态工厂落成空文档时这里没有 appliedKeys，
            // 强制转换会得到 null 并让下面的迭代抛空引用（触发可重试的错误码，掩盖真正的接线问题）。
            JArray appliedKeys = state["appliedKeys"] as JArray;
            foreach (JToken keyToken in appliedKeys ?? Enumerable.Empty<JToken>())
            {
                if (keyToken.Type == JTokenType.String && StringComparer.Ordinal.Equals((string)keyToken, command.IdempotencyKey))
                {
                    if (command.Kind == WorldStateKind.PersonaContinuity)
                    {
                        PersonaRuntimeStateDocument existingPersona = ParsePersonaStateForComparison(state);
                        PersonaRuntimeStateDocument incomingPersona = PersonaRuntimeStateDocument.FromJson(
                            (command.Arguments["document"] as JObject)?.ToString(Formatting.None));
                        if (!PersonaPersistenceValidator.AreSameRuntimeState(existingPersona, incomingPersona))
                            return new WorldApplyResult { Applied = false, Retryable = false, Code = "awake.world_state.key_conflict" };
                    }
                    if (command.Kind == WorldStateKind.WorldEvents && !IsWeeklyReportStateCommand(command))
                    {
                        JArray records = state["records"] as JArray;
                        JObject existing = records?.Children<JObject>().FirstOrDefault(value =>
                            StringComparer.Ordinal.Equals((string)value["id"], command.IdempotencyKey)
                            || StringComparer.Ordinal.Equals((string)value["eventKey"], (string)command.Arguments["eventKey"]));
                        if (existing != null && !SameWorldEventPayload(existing, command))
                            return new WorldApplyResult { Applied = false, Retryable = false, Code = "awake.world_state.key_conflict" };
                    }
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
            case WorldStateKind.WorldEvents:
                applyError = ApplyWorldEvents(state, command);
                break;
            case WorldStateKind.Messenger:
                applyError = ApplyMessenger(state, command);
                break;
            case WorldStateKind.Transcript:
                applyError = ApplyTranscript(state, command);
                break;
            case WorldStateKind.Contacts:
                applyError = ApplyContacts(state, command);
                break;
            case WorldStateKind.Audit:
                applyError = ApplyAudit(state, command);
                break;
            case WorldStateKind.Onboarding:
                applyError = ApplyOnboarding(state, command);
                break;
            case WorldStateKind.PendingDialogue:
                applyError = ApplyDialogueQueue(state, command);
                break;
            case WorldStateKind.Interaction:
                applyError = ApplyInteraction(state, command);
                break;
            case WorldStateKind.InteractionIndex:
                applyError = ApplyInteractionRecoveryIndex(state, command);
                break;
            case WorldStateKind.PersonaContinuity:
                applyError = ApplyPersonaContinuity(state, command);
                break;
            case WorldStateKind.Letters:
                applyError = LetterLedgerDocument.Apply(state, command);
                break;
            default:
                // 枚举已声明但尚未接入分发的 Kind 必须在这里被拒。
                // 缺这一支时 applyError 会保持空 ⇒ 被判"成功" ⇒ 把未修改的文档连同新的 updatedUtc 写回存储：
                // 不抛错、不告警，属静默失败。非重试——这是接线错误，不是暂态故障。
                AwakeLog.Write("world_state_unknown_kind key=" + command.Key + " kind=" + command.Kind);
                applyError = "awake.world_state.unknown_kind";
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
            if (command.Kind == WorldStateKind.PersonaContinuity && !stored.IsSuccess)
            {
                OperationResult<string> confirmed = await store.GetAsync(command.Key, context, cancellationToken).ConfigureAwait(false);
                PersonaRuntimeStateDocument confirmedDocument = PersonaRuntimeStateDocument.FromJson(confirmed.Value);
                PersonaRuntimeStateDocument intendedDocument = ParsePersonaStateForComparison(state);
                if (confirmed.IsSuccess
                    && PersonaPersistenceValidator.AreSameRuntimeState(confirmedDocument, intendedDocument))
                {
                    AwakeLog.Write("persona.persistence.commit_unknown_confirmed key=" + command.Key);
                    return new WorldApplyResult { Applied = true, Code = "awake.world_state.commit_unknown_confirmed" };
                }
                return new WorldApplyResult
                {
                    Retryable = false,
                    CommitUnknown = true,
                    Code = stored.Error?.Code ?? "awake.world_state.commit_unknown"
                };
            }
            return new WorldApplyResult
            {
                Retryable = true,
                CommitUnknown = !stored.IsSuccess,
                Code = stored.Error?.Code ?? "awake.world_state.write_failed"
            };
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

    private static bool IsWeeklyReportStateCommand(WorldStateCommand command)
    {
        return command != null
            && command.Kind == WorldStateKind.WorldEvents
            && StringComparer.Ordinal.Equals((string)command.Arguments["operation"], "weekly_report_state");
    }

    private static JObject NewState(WorldStateKind kind, string heroId)
    {
        switch (kind)
        {
            case WorldStateKind.Memory: return NewMemoryState(heroId);
            case WorldStateKind.Relationship: return NewRelationshipState(heroId);
            case WorldStateKind.EventMeta: return NewEventMetaState();
            case WorldStateKind.Proactive: return NewProactiveState();
            case WorldStateKind.WorldEvents: return NewWorldEventsState();
            case WorldStateKind.Messenger: return NewMessengerState();
            case WorldStateKind.Transcript: return NewTranscriptChunkState();
            case WorldStateKind.Contacts: return NewContactsState();
            case WorldStateKind.Audit: return NewAuditState();
            case WorldStateKind.Onboarding: return NewOnboardingState();
            case WorldStateKind.PendingDialogue: return NewDialogueQueueState();
            case WorldStateKind.Interaction: return NewInteractionState(heroId);
            case WorldStateKind.InteractionIndex: return NewInteractionRecoveryIndexState();
            case WorldStateKind.PersonaContinuity: return NewPersonaContinuityState();
            case WorldStateKind.Letters: return LetterLedgerDocument.NewState();
            default: return new JObject();
        }
    }

    private static JObject NewPersonaContinuityState()
    {
        return new JObject
        {
            ["schema"] = PersonaPersistenceConstants.ContinuitySchema,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["appliedKeys"] = new JArray(),
            ["initialized"] = false
        };
    }

    private static PersonaRuntimeStateDocument ParsePersonaStateForComparison(JObject state)
    {
        if (state == null) return null;
        JObject copy = (JObject)state.DeepClone();
        if (copy["initialized"] != null && !copy["initialized"].Value<bool>()) return null;
        copy.Remove("appliedKeys");
        copy.Remove("updatedUtc");
        PersonaRuntimeStateDocument document = PersonaRuntimeStateDocument.FromJson(copy.ToString(Formatting.None));
        if (document == null || string.IsNullOrWhiteSpace(document.PersonaSubjectStableId)) return null;
        return document;
    }

    private static string ApplyPersonaContinuity(JObject state, WorldStateCommand command)
    {
        JObject incomingJson = command.Arguments["document"] as JObject;
        PersonaRuntimeStateDocument incoming = PersonaRuntimeStateDocument.FromJson(incomingJson?.ToString(Formatting.None));
        string error;
        if (!PersonaPersistenceValidator.TryValidateRuntimeState(incoming, null, command.HeroId, null, out error)) return error;
        PersonaRuntimeStateDocument current = ParsePersonaStateForComparison(state);
        if (current != null && current.Sequence > incoming.Sequence)
            return "persona.persistence.sequence_regression";
        if (current != null && current.Sequence == incoming.Sequence
            && !PersonaPersistenceValidator.AreSameRuntimeState(current, incoming))
            return "awake.world_state.key_conflict";

        JArray appliedKeys = state["appliedKeys"] as JArray ?? new JArray();
        state.RemoveAll();
        state.Merge(incoming.ToJsonObject());
        appliedKeys.Add(command.IdempotencyKey);
        state["appliedKeys"] = appliedKeys;
        state["initialized"] = true;
        return string.Empty;
    }

    private static JObject NewMemoryState(string heroId)
    {
        return new JObject
        {
            ["schema"] = "awake.npc.memory.v1",
            ["heroId"] = heroId ?? string.Empty,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["memories"] = new JArray(),
            ["promises"] = new JArray(),
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

    private static JObject NewWorldEventsState()
    {
        return new JObject
        {
            ["schema"] = "awake.world_events.v1",
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["records"] = new JArray(),
            ["appliedKeys"] = new JArray(),
            ["weeklyReports"] = new JArray()
        };
    }

    private static JObject NewMessengerState()
    {
        return new JObject
        {
            ["schema"] = "awake.messenger.v1",
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["chats"] = new JObject(),
            ["appliedKeys"] = new JArray()
        };
    }

    private static JObject NewTranscriptChunkState()
    {
        return new JObject
        {
            ["schema"] = AwakeTranscriptConstants.Schema,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["chunkIndex"] = -1,
            ["contactKey"] = string.Empty,
            ["entries"] = new JArray(),
            ["pinnedIds"] = new JArray(),
            ["appliedKeys"] = new JArray()
        };
    }

    private static JObject NewContactsState()
    {
        return new JObject
        {
            ["schema"] = AwakeTranscriptConstants.ContactsSchema,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["contacts"] = new JArray(),
            ["contactNames"] = new JObject(),
            ["appliedKeys"] = new JArray()
        };
    }

    private static JObject NewAuditState()
    {
        return new JObject
        {
            ["schema"] = AwakeTranscriptConstants.AuditSchema,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["entries"] = new JArray(),
            ["appliedKeys"] = new JArray()
        };
    }

    private static JObject NewOnboardingState()
    {
        return new JObject
        {
            ["schema"] = AwakeStorageContract.OnboardingSchema,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["completedSteps"] = new JArray(),
            ["skippedThisCampaign"] = false,
            ["permanentlySkipped"] = false,
            ["lastReminderDay"] = -1,
            ["appliedKeys"] = new JArray()
        };
    }

    private static JObject NewDialogueQueueState()
    {
        return new JObject
        {
            ["schema"] = AwakeStorageContract.DialogueQueueSchema,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["entries"] = new JArray(),
            ["appliedKeys"] = new JArray()
        };
    }

    private static JObject NewInteractionRecoveryIndexState()
    {
        return new JObject
        {
            ["schema"] = AwakeStorageContract.InteractionRecoveryIndexSchema,
            ["entries"] = new JArray(),
            ["appliedKeys"] = new JArray()
        };
    }

    private static JObject NewInteractionState(string canonicalContactKey)
    {
        return new JObject
        {
            ["schema"] = AwakeStorageContract.InteractionSchema,
            ["canonicalContactKey"] = canonicalContactKey ?? string.Empty,
            ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["promises"] = new JArray(),
            ["interactions"] = new JArray(),
            ["appliedKeys"] = new JArray()
        };
    }

    private static void EnsureTranscriptChunkShape(JObject state, string contactKey, int chunkIndex)
    {
        state["schema"] = AwakeTranscriptConstants.Schema;
        state["chunkIndex"] = chunkIndex;
        state["contactKey"] = contactKey ?? string.Empty;
        if (!(state["entries"] is JArray)) state["entries"] = new JArray();
        if (!(state["pinnedIds"] is JArray)) state["pinnedIds"] = new JArray();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ApplyTranscript(JObject state, WorldStateCommand command)
    {
        string mode = (string)command.Arguments["mode"] ?? "append";
        string contactKey = (string)command.Arguments["contactKey"] ?? string.Empty;
        int chunkIndex = IntValue(command.Arguments["chunkIndex"]);
        EnsureTranscriptChunkShape(state, contactKey, chunkIndex);
        JArray entries = (JArray)state["entries"];
        JArray appliedKeys = (JArray)state["appliedKeys"];

        if (StringComparer.Ordinal.Equals(mode, "append"))
        {
            List<AwakeTranscriptLine> newLines = new List<AwakeTranscriptLine>();
            if (command.Arguments["lines"] is JArray lineArray)
            {
                foreach (JToken token in lineArray)
                {
                    AwakeTranscriptLine line = AwakeTranscriptLine.FromJson(token);
                    if (line == null) return "awake.world_state.transcript.invalid_line";
                    newLines.Add(line);
                }
            }
            else
            {
                AwakeTranscriptLine line = AwakeTranscriptLine.FromJson(command.Arguments["line"]);
                if (line != null) newLines.Add(line);
            }
            if (newLines.Count == 0) return "awake.world_state.transcript.invalid_line";
            foreach (AwakeTranscriptLine line in newLines)
            {
                string error;
                if (!AwakeTranscriptValidator.ValidateLine(line, out error))
                {
                    return "awake.world_state.transcript.invalid_line." + error;
                }
                entries.Add(line.ToJson());
            }
            if (entries.Count > AwakeTranscriptConstants.MaximumLinesPerContact)
            {
                return "awake.world_state.transcript.too_many_lines";
            }
            if (Encoding.UTF8.GetByteCount(state.ToString(Formatting.None)) > AwakeTranscriptConstants.MaximumChunkUtf8Bytes)
            {
                return "awake.world_state.transcript.chunk_full";
            }
            appliedKeys.Add(command.IdempotencyKey);
            Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
            return string.Empty;
        }

        if (StringComparer.Ordinal.Equals(mode, "pin"))
        {
            string lineId = (string)command.Arguments["lineId"] ?? string.Empty;
            bool pin = BoolValue(command.Arguments["pin"]);
            if (string.IsNullOrWhiteSpace(lineId)) return "awake.world_state.transcript.invalid_lineId";
            bool found = false;
            foreach (JToken token in entries)
            {
                if (token is JObject obj && StringComparer.Ordinal.Equals((string)obj["id"], lineId))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return "awake.world_state.transcript.line_not_found";
            JArray pinned = (JArray)state["pinnedIds"];
            if (pinned == null)
            {
                pinned = new JArray();
                state["pinnedIds"] = pinned;
            }
            if (pin)
            {
                if (pinned.Count >= AwakeTranscriptConstants.MaximumPinnedLines)
                {
                    return "awake.world_state.transcript.pin_limit";
                }
                if (!ContainsString(pinned, lineId)) pinned.Add(lineId);
            }
            else
            {
                for (int i = pinned.Count - 1; i >= 0; i--)
                {
                    if (StringComparer.Ordinal.Equals((string)pinned[i], lineId)) pinned.RemoveAt(i);
                }
            }
            appliedKeys.Add(command.IdempotencyKey);
            Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
            return string.Empty;
        }

        if (StringComparer.Ordinal.Equals(mode, "roll"))
        {
            int cutoffDay = IntValue(command.Arguments["cutoffDay"]);
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] is not JObject obj) continue;
                string id = (string)obj["id"] ?? string.Empty;
                JArray pinned = state["pinnedIds"] as JArray;
                bool pinnedLine = pinned != null && ContainsString(pinned, id);
                if (!pinnedLine && IntValue(obj["day"]) < cutoffDay)
                {
                    entries.RemoveAt(i);
                }
            }
            appliedKeys.Add(command.IdempotencyKey);
            Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
            return string.Empty;
        }

        return "awake.world_state.transcript.invalid_mode";
    }

    private static void EnsureContactsShape(JObject state)
    {
        state["schema"] = AwakeTranscriptConstants.ContactsSchema;
        if (!(state["contacts"] is JArray)) state["contacts"] = new JArray();
        if (!(state["contactNames"] is JObject)) state["contactNames"] = new JObject();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ApplyContacts(JObject state, WorldStateCommand command)
    {
        EnsureContactsShape(state);
        string contactKey = (string)command.Arguments["contactKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(contactKey)) return "awake.world_state.contacts.invalid_key";
        JArray contacts = (JArray)state["contacts"];
        if (!ContainsString(contacts, contactKey))
        {
            contacts.Add(contactKey);
            Trim(contacts, 1000);
        }
        string displayName = (string)command.Arguments["displayName"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            ((JObject)state["contactNames"])[contactKey] = displayName;
        }
        JArray appliedKeys = (JArray)state["appliedKeys"];
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        return string.Empty;
    }

    private static void EnsureAuditShape(JObject state)
    {
        state["schema"] = AwakeTranscriptConstants.AuditSchema;
        if (!(state["entries"] is JArray)) state["entries"] = new JArray();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ApplyAudit(JObject state, WorldStateCommand command)
    {
        EnsureAuditShape(state);
        JArray entries = (JArray)state["entries"];
        entries.Insert(0, new JObject
        {
            ["rollId"] = (string)command.Arguments["rollId"] ?? string.Empty,
            ["lineId"] = (string)command.Arguments["lineId"] ?? string.Empty,
            ["action"] = (string)command.Arguments["action"] ?? string.Empty,
            ["day"] = IntValue(command.Arguments["day"]),
            ["correlation"] = command.CorrelationId,
            ["memoryWriteId"] = (string)command.Arguments["memoryWriteId"] ?? string.Empty,
            ["conversationId"] = (string)command.Arguments["conversationId"] ?? string.Empty,
            ["phase"] = (string)command.Arguments["phase"] ?? "intent",
            ["chunkIndexes"] = command.Arguments["chunkIndexes"] ?? new JArray(),
            ["status"] = (string)command.Arguments["status"] ?? "pending"
        });
        Trim(entries, AwakeTranscriptConstants.MaximumAuditEntries);
        JArray appliedKeys = (JArray)state["appliedKeys"];
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        return string.Empty;
    }

    private static void EnsureOnboardingShape(JObject state)
    {
        state["schema"] = AwakeStorageContract.OnboardingSchema;
        if (!(state["completedSteps"] is JArray)) state["completedSteps"] = new JArray();
        if (state["skippedThisCampaign"] == null) state["skippedThisCampaign"] = false;
        if (state["permanentlySkipped"] == null) state["permanentlySkipped"] = false;
        if (state["lastReminderDay"] == null) state["lastReminderDay"] = -1;
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ApplyOnboarding(JObject state, WorldStateCommand command)
    {
        EnsureOnboardingShape(state);
        JArray completedSteps = (JArray)state["completedSteps"];
        if (command.Arguments["completedSteps"] is JArray newSteps)
        {
            foreach (JToken token in newSteps)
            {
                string step = token?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(step) && !ContainsString(completedSteps, step))
                {
                    completedSteps.Add(step);
                }
            }
        }
        Trim(completedSteps, 32);
        state["skippedThisCampaign"] = BoolValue(command.Arguments["skippedThisCampaign"]) || (bool)state["skippedThisCampaign"];
        state["permanentlySkipped"] = BoolValue(command.Arguments["permanentlySkipped"]) || (bool)state["permanentlySkipped"];
        int reminderDay = IntValue(command.Arguments["lastReminderDay"]);
        if (reminderDay >= 0) state["lastReminderDay"] = reminderDay;
        JArray appliedKeys = (JArray)state["appliedKeys"];
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        return string.Empty;
    }

    private static void EnsureDialogueQueueShape(JObject state)
    {
        state["schema"] = AwakeStorageContract.DialogueQueueSchema;
        if (!(state["entries"] is JArray)) state["entries"] = new JArray();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ApplyDialogueQueue(JObject state, WorldStateCommand command)
    {
        EnsureDialogueQueueShape(state);
        JArray entries = (JArray)state["entries"];
        JArray appliedKeys = (JArray)state["appliedKeys"];
        if (StringComparer.Ordinal.Equals(command.CommandId, AiTaskConstants.DialogueQueueEnqueueCommandId))
        {
            string id = (string)command.Arguments["id"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)) return "awake.world_state.dialogue_queue.invalid_id";
            if (FindQueueEntry(entries, id) != null) return "awake.world_state.dialogue_queue.duplicate";
            entries.Add(new JObject
            {
                ["id"] = id,
                ["source"] = (string)command.Arguments["source"] ?? "unknown",
                ["targetId"] = (string)command.Arguments["targetId"] ?? string.Empty,
                ["canonicalContactKey"] = (string)command.Arguments["canonicalContactKey"] ?? string.Empty,
                ["openingHint"] = ClampTextElements((string)command.Arguments["openingHint"] ?? string.Empty, 240),
                ["motive"] = ClampTextElements((string)command.Arguments["motive"] ?? string.Empty, 120),
                ["day"] = IntValue(command.Arguments["day"]),
                ["expiryDay"] = IntValue(command.Arguments["expiryDay"]),
                ["state"] = (string)command.Arguments["state"] ?? "pending"
            });
            Trim(entries, 64);
        }
        else if (StringComparer.Ordinal.Equals(command.CommandId, AiTaskConstants.DialogueQueueConsumeCommandId))
        {
            string id = (string)command.Arguments["id"] ?? string.Empty;
            JObject entry = FindQueueEntry(entries, id);
            if (entry == null) return "awake.world_state.dialogue_queue.not_found";
            entry["state"] = (string)command.Arguments["state"] ?? "consumed";
        }
        else
        {
            return "awake.world_state.dialogue_queue.invalid_command";
        }
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        return string.Empty;
    }

    private static JObject FindQueueEntry(JArray entries, string id)
    {
        if (entries == null) return null;
        foreach (JToken token in entries)
        {
            if (token is JObject obj && StringComparer.Ordinal.Equals((string)obj["id"], id))
            {
                return obj;
            }
        }
        return null;
    }

    private static void EnsureInteractionShape(JObject state, string canonicalContactKey)
    {
        state["schema"] = AwakeStorageContract.InteractionSchema;
        state["canonicalContactKey"] = canonicalContactKey ?? string.Empty;
        if (!(state["promises"] is JArray)) state["promises"] = new JArray();
        if (!(state["interactions"] is JArray)) state["interactions"] = new JArray();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ApplyInteraction(JObject state, WorldStateCommand command)
    {
        EnsureInteractionShape(state, command.HeroId);
        JArray promises = (JArray)state["promises"];
        JArray interactions = (JArray)state["interactions"];
        JArray appliedKeys = (JArray)state["appliedKeys"];
        string mode = (string)command.Arguments["mode"] ?? string.Empty;
        if (StringComparer.Ordinal.Equals(mode, "promise_upsert"))
        {
            if (!(command.Arguments["promise"] is JObject promise))
            {
                return "awake.world_state.interactions.invalid_promise";
            }
            string promiseId = (string)promise["promiseId"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(promiseId)) return "awake.world_state.interactions.invalid_promise_id";
            JObject existing = FindPromise(promises, promiseId);
            if (existing != null)
            {
                promise["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
                existing.Replace(promise);
            }
            else
            {
                promise["createdUtc"] = DateTimeOffset.UtcNow.ToString("O");
                promises.Add(promise);
            }
            interactions.Add(new JObject
            {
                ["kind"] = "promise_request",
                ["promiseId"] = promiseId,
                ["day"] = IntValue(promise["day"]),
                ["correlation"] = command.CorrelationId
            });
        }
        else if (StringComparer.Ordinal.Equals(mode, "promise_update"))
        {
            string promiseId = (string)command.Arguments["promiseId"] ?? string.Empty;
            string newStatus = (string)command.Arguments["newStatus"] ?? string.Empty;
            JObject existing = FindPromise(promises, promiseId);
            if (existing == null) return "awake.world_state.interactions.promise_not_found";
            if (!AwakePromiseStateMachine.CanTransition((string)existing["status"], newStatus))
            {
                return "awake.world_state.interactions.invalid_transition";
            }
            existing["status"] = newStatus;
            existing["reason"] = ClampTextElements((string)command.Arguments["reason"] ?? string.Empty, 240);
            existing["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
            interactions.Add(new JObject
            {
                ["kind"] = "promise_update",
                ["promiseId"] = promiseId,
                ["status"] = newStatus,
                ["correlation"] = command.CorrelationId
            });
        }
        else if (StringComparer.Ordinal.Equals(mode, "give_gold_pending"))
        {
            string interactionId = (string)command.Arguments["interactionId"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(interactionId)) return "awake.world_state.interactions.invalid_interaction_id";
            if (FindInteraction(interactions, interactionId) == null)
            {
                interactions.Add(new JObject
                {
                    ["interactionId"] = interactionId,
                    ["kind"] = "give_gold",
                    ["phase"] = "pending",
                    ["amount"] = IntValue(command.Arguments["amount"]),
                    ["targetHeroId"] = (string)command.Arguments["targetHeroId"] ?? string.Empty,
                    ["expectedBalanceBefore"] = IntValue(command.Arguments["expectedBalanceBefore"]),
                    ["expectedBalanceAfter"] = IntValue(command.Arguments["expectedBalanceAfter"]),
                    ["sessionId"] = (string)command.Arguments["sessionId"] ?? string.Empty,
                    ["generation"] = IntValue(command.Arguments["generation"]),
                    ["snapshotToken"] = (string)command.Arguments["snapshotToken"] ?? string.Empty,
                    ["day"] = IntValue(command.Arguments["day"]),
                    ["correlation"] = command.CorrelationId,
                    ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O")
                });
            }
        }
        else if (StringComparer.Ordinal.Equals(mode, "give_gold_complete")
            || StringComparer.Ordinal.Equals(mode, "give_gold_compensated"))
        {
            string interactionId = (string)command.Arguments["interactionId"] ?? string.Empty;
            JObject existing = FindInteraction(interactions, interactionId);
            if (existing == null) return "awake.world_state.interactions.interaction_not_found";
            existing["phase"] = StringComparer.Ordinal.Equals(mode, "give_gold_complete") ? "complete" : "compensated";
            existing["reason"] = ClampTextElements((string)command.Arguments["reason"] ?? string.Empty, 240);
            existing["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
        }
        else
        {
            return "awake.world_state.interactions.invalid_mode";
        }
        Trim(promises, 100);
        Trim(interactions, 200);
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        return string.Empty;
    }

    private static JObject FindPromise(JArray promises, string promiseId)
    {
        if (promises == null) return null;
        foreach (JToken token in promises)
        {
            if (token is JObject obj && StringComparer.Ordinal.Equals((string)obj["promiseId"], promiseId))
            {
                return obj;
            }
        }
        return null;
    }

    private static JObject FindInteraction(JArray interactions, string interactionId)
    {
        if (interactions == null) return null;
        foreach (JToken token in interactions)
        {
            if (token is JObject obj && StringComparer.Ordinal.Equals((string)obj["interactionId"], interactionId))
            {
                return obj;
            }
        }
        return null;
    }

    private static string ApplyInteractionRecoveryIndex(JObject state, WorldStateCommand command)
    {
        state["schema"] = AwakeStorageContract.InteractionRecoveryIndexSchema;
        if (!(state["entries"] is JArray entries)) state["entries"] = entries = new JArray();
        if (!(state["appliedKeys"] is JArray appliedKeys)) state["appliedKeys"] = appliedKeys = new JArray();
        string contactKey = (string)command.Arguments["canonicalContactKey"] ?? string.Empty;
        string interactionId = (string)command.Arguments["interactionId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(contactKey) || string.IsNullOrWhiteSpace(interactionId))
        {
            return "awake.world_state.interactions.invalid_recovery_index_entry";
        }
        JObject existing = null;
        foreach (JToken token in entries)
        {
            if (token is JObject obj && StringComparer.Ordinal.Equals((string)obj["interactionId"], interactionId))
            {
                existing = obj;
                break;
            }
        }
        if (BoolValue(command.Arguments["pending"]))
        {
            if (existing == null)
            {
                entries.Add(new JObject
                {
                    ["canonicalContactKey"] = contactKey,
                    ["interactionId"] = interactionId,
                    ["updatedUtc"] = (string)command.Arguments["updatedUtc"] ?? DateTimeOffset.UtcNow.ToString("O")
                });
            }
            else
            {
                existing["canonicalContactKey"] = contactKey;
                existing["updatedUtc"] = (string)command.Arguments["updatedUtc"] ?? DateTimeOffset.UtcNow.ToString("O");
            }
        }
        else if (existing != null)
        {
            existing.Remove();
        }
        Trim(entries, 200);
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        return string.Empty;
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

    private static void EnsureWorldEventsShape(JObject state)
    {
        state["schema"] = "awake.world_events.v1";
        if (!(state["records"] is JArray)) state["records"] = new JArray();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
        if (!(state["weeklyReports"] is JArray)) state["weeklyReports"] = new JArray();
        JArray records = (JArray)state["records"];
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < records.Count; index++)
        {
            if (!(records[index] is JObject record)) continue;
            string id = (string)record["id"] ?? string.Empty;
            string eventKey = (string)record["eventKey"] ?? id;
            if (string.IsNullOrWhiteSpace(id) || (seen.Add(id) && seenKeys.Add(eventKey))) continue;
            records.RemoveAt(index--);
        }
    }

    private static string ApplyWorldEvents(JObject state, WorldStateCommand command)
    {
        EnsureWorldEventsShape(state);
        if (IsWeeklyReportStateCommand(command)) return ApplyWeeklyReportState(state, command);
        JArray records = (JArray)state["records"];
        string eventId = command.IdempotencyKey;
        string eventKey = (string)command.Arguments["eventKey"] ?? eventId;
        foreach (JToken token in records)
        {
            if (token is JObject existing
                && (StringComparer.Ordinal.Equals((string)existing["id"], eventId)
                    || StringComparer.Ordinal.Equals((string)existing["eventKey"] ?? (string)existing["id"], eventKey)))
            {
                return SameWorldEventPayload(existing, command) ? "awake.world_state.duplicate" : "awake.world_state.key_conflict";
            }
        }
        string kind = WorldEventContract.NormalizeEventType((string)command.Arguments["kind"] ?? "event");
        JArray visibilityIdentityIds = NormalizeIdentityIds(command.Arguments["visibilityIdentityIds"]);
        records.Insert(0, new JObject
        {
            ["id"] = eventId,
            ["day"] = IntValue(command.Arguments["day"]),
            ["kind"] = ClampTextElements(kind, 40),
            ["text"] = ClampTextElements((string)command.Arguments["text"] ?? string.Empty, 500),
            ["eventKey"] = ClampTextElements(eventKey, 200),
            ["domain"] = WeeklyReportService.NormalizeDomain((string)command.Arguments["domain"], kind),
            ["occurredAt"] = (string)command.Arguments["occurredAt"] ?? DateTimeOffset.UtcNow.ToString("O"),
            ["visibilityIdentityIds"] = visibilityIdentityIds,
            ["fact"] = command.Arguments["fact"] == null ? null : command.Arguments["fact"].DeepClone()
        });
        Trim(records, 200);
        state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
        JArray appliedKeys = (JArray)state["appliedKeys"];
        appliedKeys.Add(command.IdempotencyKey);
        Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
        return string.Empty;
    }

    private async Task<WorldApplyResult> AppendWorldFactJournalAsync(JObject fact, RequestContext context, CancellationToken cancellationToken)
    {
        if (fact == null) return new WorldApplyResult { Code = "awake.world_fact.missing_payload" };
        IKeyValueStore store;
        lock (_gate) _stores.TryGetValue(AiTaskConstants.WorldFactJournalNamespace, out store);
        if (store == null) return new WorldApplyResult { Retryable = true, Code = "awake.world_fact.storage_unavailable" };

        await WorldFactJournalWriterGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WorldFactJournalReadResult existing = await GetWorldFactJournalAsync(context, cancellationToken).ConfigureAwait(false);
            if (existing.Status == WorldFactJournalReadStatus.Corrupt)
            {
                // 坏账本不能让这本账永久写不进去（读坏 ⇒ 放弃写入 ⇒ 永远坏，是同一个死锁的另一半）。
                // 先把坏值隔离到旁路 key（只复制、不删原件，数据不丢），再按空账本继续 ——
                // 写入流程末尾会用新 root 覆盖掉坏值。2026-09-15 定案。
                if (!await QuarantineCorruptJournalAsync(store, context, cancellationToken).ConfigureAwait(false))
                    return new WorldApplyResult { Retryable = true, Code = "awake.world_fact.journal_quarantine_failed" };
                existing = new WorldFactJournalReadResult(WorldFactJournalReadStatus.Missing);
            }
            else if (existing.Status == WorldFactJournalReadStatus.Unavailable)
            {
                // 存储不可用是环境问题，重试即可，不能靠隔离解决。
                return new WorldApplyResult
                {
                    Retryable = true,
                    Code = string.IsNullOrWhiteSpace(existing.ErrorCode) ? "awake.world_fact.journal_unavailable" : existing.ErrorCode
                };
            }

            List<JObject> facts = existing.Facts.Select(value => (JObject)value.DeepClone()).ToList();
            string factId = (string)fact["factId"] ?? string.Empty;
            string eventKey = (string)fact["eventKey"] ?? string.Empty;
            JObject same = facts.FirstOrDefault(value => StringComparer.Ordinal.Equals((string)value["factId"], factId)
                || StringComparer.Ordinal.Equals((string)value["eventKey"], eventKey));
            if (same != null)
            {
                bool equal = JToken.DeepEquals(same, fact);
                return new WorldApplyResult { Duplicate = equal, Code = equal ? "awake.world_state.duplicate" : "awake.world_state.key_conflict" };
            }

            WorldFactJournalReadResult candidate = ValidateFactForJournal(fact);
            if (candidate.Status != WorldFactJournalReadStatus.Success)
                return new WorldApplyResult { Code = string.IsNullOrWhiteSpace(candidate.ErrorCode) ? "awake.world_fact.fact_invalid" : candidate.ErrorCode };
            facts.Add((JObject)fact.DeepClone());

            int revision = (existing.Revision ?? 0) + 1;
            List<JournalChunkWrite> chunks = BuildJournalChunks(facts, revision);
            if (chunks == null || chunks.Count == 0)
                return new WorldApplyResult { Code = "awake.world_fact.too_large" };
            var keys = new List<string>();
            foreach (JournalChunkWrite chunk in chunks)
            {
                OperationResult<bool> stored = await store.SetAsync(chunk.Key, chunk.Json, context, cancellationToken).ConfigureAwait(false);
                if (!stored.IsSuccess || !stored.Value)
                {
                    OperationResult<string> confirmed = await store.GetAsync(chunk.Key, context, cancellationToken).ConfigureAwait(false);
                    if (!confirmed.IsSuccess || !StringComparer.Ordinal.Equals(confirmed.Value, chunk.Json))
                        return new WorldApplyResult { Retryable = true, CommitUnknown = !confirmed.IsSuccess, Code = stored.Error?.Code ?? "awake.world_fact.chunk_write_failed" };
                }
                OperationResult<string> readBack = await store.GetAsync(chunk.Key, context, cancellationToken).ConfigureAwait(false);
                if (!readBack.IsSuccess || !StringComparer.Ordinal.Equals(readBack.Value, chunk.Json))
                    return new WorldApplyResult { Retryable = true, CommitUnknown = !readBack.IsSuccess, Code = "awake.world_fact.chunk_verify_failed" };
                if (WorldFactJournalCodec.ReadChunk(readBack.Value).Status != WorldFactJournalReadStatus.Success)
                    return new WorldApplyResult { Code = "awake.world_fact.chunk_verify_failed" };
                keys.Add(chunk.Key);
            }

            int startDay = facts.Min(value => IntValue(value["occurred"]?["campaignDay"]));
            startDay = ((startDay - 1) / 7) * 7 + 1;
            int endDay = facts.Max(value => IntValue(value["occurred"]?["campaignDay"]));
            endDay = ((endDay - 1) / 7) * 7 + 7;
            JObject root = WorldFactJournalCodec.BuildRoot(startDay, endDay, revision, keys);
            string rootJson = root.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(rootJson) > AiTaskConstants.StorageValueMaximumBytes)
                return new WorldApplyResult { Code = "awake.world_fact.root_too_large" };

            OperationResult<bool> rootStored = await store.SetAsync(AiTaskConstants.WorldFactJournalRootKey, rootJson, context, cancellationToken).ConfigureAwait(false);
            OperationResult<string> rootReadBack = await store.GetAsync(AiTaskConstants.WorldFactJournalRootKey, context, cancellationToken).ConfigureAwait(false);
            if (rootReadBack.IsSuccess && StringComparer.Ordinal.Equals(rootReadBack.Value, rootJson))
                return new WorldApplyResult { Applied = true, Code = "awake.world_fact.persisted" };
            if (!rootReadBack.IsSuccess)
                return new WorldApplyResult { Retryable = true, CommitUnknown = true, Code = rootStored.Error?.Code ?? "awake.world_fact.root_commit_unknown" };

            JObject observedRoot;
            WorldFactJournalReadStatus observedStatus = WorldFactJournalCodec.ReadRoot(rootReadBack.Value, out observedRoot);
            if (observedStatus == WorldFactJournalReadStatus.Success && IntValue(observedRoot["revision"]) == existing.Revision)
                return new WorldApplyResult { Retryable = true, Code = "awake.world_fact.root_replace_retryable" };
            return new WorldApplyResult { Code = "awake.world_fact.root_conflict" };
        }
        finally
        {
            WorldFactJournalWriterGate.Release();
        }
    }

    /// <summary>
    /// 把坏掉的世界事实日志 root 隔离到旁路 key。只复制、不删原件 —— 随后由新写入覆盖它；
    /// 若本次写入最终失败，坏值仍在原位，下次重试会再隔离一次（同名覆盖，不会膨胀）。
    /// 返回 false 表示隔离没做成，调用方应放弃本次写入。
    /// </summary>
    private static async Task<bool> QuarantineCorruptJournalAsync(
        IKeyValueStore store,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        OperationResult<string> current = await store.GetAsync(
            AiTaskConstants.WorldFactJournalRootKey, context, cancellationToken).ConfigureAwait(false);
        if (!current.IsSuccess) return false;
        if (string.IsNullOrWhiteSpace(current.Value)) return true;
        OperationResult<bool> stored = await store.SetAsync(
            AiTaskConstants.WorldFactJournalRootKey + ".quarantine",
            current.Value,
            context,
            cancellationToken).ConfigureAwait(false);
        if (!stored.IsSuccess || !stored.Value) return false;
        AwakeLog.Write("world_fact_journal_quarantined bytes=" + Encoding.UTF8.GetByteCount(current.Value));
        return true;
    }

    private sealed class JournalChunkWrite
    {
        internal string Key { get; set; }
        internal string Json { get; set; }
    }

    private static WorldFactJournalReadResult ValidateFactForJournal(JObject fact)
    {
        int day = IntValue(fact?["occurred"]?["campaignDay"]);
        try
        {
            JObject probe = WorldFactJournalCodec.BuildChunk(day, day, 1, new[] { fact });
            return WorldFactJournalCodec.ReadChunk(probe.ToString(Formatting.None));
        }
        catch (Exception ex)
        {
            return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt, errorCode: ex.Message);
        }
    }

    private static List<JournalChunkWrite> BuildJournalChunks(List<JObject> facts, int revision)
    {
        var result = new List<JournalChunkWrite>();
        foreach (IGrouping<int, JObject> group in facts
            .OrderBy(value => IntValue(value["occurred"]?["campaignDay"]))
            .GroupBy(value => ((IntValue(value["occurred"]?["campaignDay"]) - 1) / 7) * 7 + 1)
            .OrderBy(value => value.Key))
        {
            int startDay = group.Key;
            int endDay = startDay + 6;
            var buffer = new List<JObject>();
            foreach (JObject value in group.OrderBy(item => IntValue(item["occurred"]?["campaignDay"]))
                .ThenBy(item => (long?)item["occurred"]?["timeSlot"] ?? 0L)
                .ThenBy(item => (string)item["factId"], StringComparer.Ordinal))
            {
                buffer.Add(value);
                if (buffer.Count > WorldFactJournalCodec.MaximumFactsPerChunk)
                {
                    buffer.RemoveAt(buffer.Count - 1);
                    AddJournalChunk(result, startDay, endDay, revision, result.Count, WorldFactJournalCodec.BuildChunk(startDay, endDay, revision, buffer).ToString(Formatting.None));
                    buffer.Clear();
                    buffer.Add(value);
                }
                JObject candidate = WorldFactJournalCodec.BuildChunk(startDay, endDay, revision, buffer);
                string candidateJson = candidate.ToString(Formatting.None);
                if (Encoding.UTF8.GetByteCount(candidateJson) <= AiTaskConstants.StorageValueMaximumBytes
                    && buffer.Count <= WorldFactJournalCodec.MaximumFactsPerChunk) continue;
                buffer.RemoveAt(buffer.Count - 1);
                if (buffer.Count == 0) return null;
                AddJournalChunk(result, startDay, endDay, revision, result.Count, WorldFactJournalCodec.BuildChunk(startDay, endDay, revision, buffer).ToString(Formatting.None));
                buffer.Clear();
                buffer.Add(value);
                JObject single = WorldFactJournalCodec.BuildChunk(startDay, endDay, revision, buffer);
                if (Encoding.UTF8.GetByteCount(single.ToString(Formatting.None)) > AiTaskConstants.StorageValueMaximumBytes) return null;
            }
            if (buffer.Count > 0)
                AddJournalChunk(result, startDay, endDay, revision, result.Count, WorldFactJournalCodec.BuildChunk(startDay, endDay, revision, buffer).ToString(Formatting.None));
        }
        return result;
    }

    private static void AddJournalChunk(List<JournalChunkWrite> chunks, int startDay, int endDay, int revision, int ordinal, string json)
    {
        chunks.Add(new JournalChunkWrite
        {
            Key = "facts-" + startDay.ToString("D8") + "-" + endDay.ToString("D8") + "-r" + revision.ToString("D8") + "-" + ordinal.ToString("D4") + "-" + WorldFactJournalCodec.Sha256Hex(json),
            Json = json
        });
    }

    private static string ApplyWeeklyReportState(JObject state, WorldStateCommand command)
    {
        string reportId = (string)command.Arguments["reportId"] ?? string.Empty;
        string status = (string)command.Arguments["status"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reportId) || (status != "applied" && status != "retryable"))
            return "awake.world_state.weekly_report.invalid";
        JObject incomingReport = command.Arguments["report"] as JObject;
        if (StringComparer.Ordinal.Equals((string)command.Arguments["schemaVersion"], "awake.worldbook.weekly-report.v2")
            || StringComparer.Ordinal.Equals((string)incomingReport?["schemaVersion"], "awake.worldbook.weekly-report.v2"))
            return ApplyV2WeeklyReportState(state, command, incomingReport);
        if (incomingReport != null
            && (!WorldEventContract.TryValidateWeeklyReport(incomingReport, out _)
                || !StringComparer.Ordinal.Equals((string)incomingReport["reportId"], reportId)))
            return "awake.world_state.weekly_report.invalid_payload";
        int windowStartDay = IntValue(command.Arguments["windowStartDay"]);
        int windowEndDay = IntValue(command.Arguments["windowEndDay"]);
        int attemptCount = IntValue(command.Arguments["attemptCount"]);
        int lastAttemptDay = IntValue(command.Arguments["lastAttemptDay"]);
        string lastErrorCode = (string)command.Arguments["lastErrorCode"] ?? string.Empty;
        JArray reports = (JArray)state["weeklyReports"];
        JObject current = reports.Children<JObject>().FirstOrDefault(value => StringComparer.Ordinal.Equals((string)value["reportId"], reportId));
        if (current != null)
        {
            bool currentApplied = StringComparer.Ordinal.Equals((string)current["status"], "applied");
            bool currentSnapshotValid = currentApplied && IsValidWeeklyReportSnapshot(current["report"], reportId);
            if (currentApplied)
            {
                if (currentSnapshotValid) return "awake.world_state.duplicate";
                if (status == "applied" && IsEmptyWeeklyReport(incomingReport))
                    return "awake.world_state.weekly_report.snapshot_unrecoverable";
                if (status == "applied" && incomingReport != null)
                {
                    current["report"] = incomingReport.DeepClone();
                    current["lastAttemptDay"] = lastAttemptDay;
                    current["lastErrorCode"] = lastErrorCode;
                    current["attemptCount"] = Math.Max(IntValue(current["attemptCount"]), attemptCount);
                    state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
                    JArray repairKeys = (JArray)state["appliedKeys"];
                    repairKeys.Add(command.IdempotencyKey);
                    Trim(repairKeys, AiTaskConstants.AppliedKeysMaximum);
                    return string.Empty;
                }
                return "awake.world_state.duplicate";
            }
            if ((current["attemptCount"]?.Value<int>() ?? 0) >= attemptCount) return "awake.world_state.duplicate";
            current["windowStartDay"] = windowStartDay;
            current["windowEndDay"] = windowEndDay;
            current["status"] = status;
            current["attemptCount"] = attemptCount;
            current["lastAttemptDay"] = lastAttemptDay;
            current["lastErrorCode"] = lastErrorCode;
            if (incomingReport != null) current["report"] = (JObject)incomingReport.DeepClone();
        }
        else
        {
            reports.Add(new JObject
            {
                ["reportId"] = reportId,
                ["windowStartDay"] = windowStartDay,
                ["windowEndDay"] = windowEndDay,
                ["status"] = status,
                ["attemptCount"] = attemptCount,
                ["lastAttemptDay"] = lastAttemptDay,
                ["lastErrorCode"] = lastErrorCode,
                ["report"] = incomingReport == null ? null : (JObject)incomingReport.DeepClone()
            });
        }
        state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
        return string.Empty;
    }

    private static string ApplyV2WeeklyReportState(JObject state, WorldStateCommand command, JObject incomingReport)
    {
        if (incomingReport == null || !WeeklyReportService.TryValidateV2Report(incomingReport, out _))
            return "awake.world_report.v2.invalid_payload";
        string reportId = (string)command.Arguments["reportId"] ?? string.Empty;
        if (!StringComparer.Ordinal.Equals(reportId, (string)incomingReport["reportId"]))
            return "awake.world_report.v2.invalid_payload";
        JArray reports = (JArray)state["weeklyReports"];
        JObject current = reports.Children<JObject>().FirstOrDefault(value => StringComparer.Ordinal.Equals((string)value["reportId"], reportId));
        if (current != null)
        {
            string currentSchema = (string)current["schemaVersion"] ?? (string)current["report"]?["schemaVersion"] ?? string.Empty;
            if (StringComparer.Ordinal.Equals(currentSchema, "awake.worldbook.weekly-report.v1"))
                return "awake.world_report.v2.v1_conflict";
            if (StringComparer.Ordinal.Equals(currentSchema, "awake.worldbook.weekly-report.v2")
                && IsValidV2WeeklyReportEntry(current, out JObject currentReport))
            {
                if (SameV2WeeklyReport(current, currentReport, incomingReport,
                    (int)incomingReport["extensions"]["awake:windowStartDay"],
                    (int)incomingReport["extensions"]["awake:windowEndDay"]))
                    return "awake.world_state.duplicate";
                return "awake.world_report.v2.conflict";
            }
            current.RemoveAll();
            WriteV2WeeklyReportEntry(current, command, incomingReport);
        }
        else
        {
            current = new JObject();
            WriteV2WeeklyReportEntry(current, command, incomingReport);
            reports.Add(current);
        }
        state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
        return string.Empty;
    }

    private static void WriteV2WeeklyReportEntry(JObject entry, WorldStateCommand command, JObject report)
    {
        entry["reportId"] = (string)report["reportId"];
        entry["schemaVersion"] = (string)report["schemaVersion"];
        entry["windowStartDay"] = (int)report["extensions"]["awake:windowStartDay"];
        entry["windowEndDay"] = (int)report["extensions"]["awake:windowEndDay"];
        entry["status"] = "applied";
        entry["attemptCount"] = IntValue(command.Arguments["attemptCount"]);
        entry["lastAttemptDay"] = IntValue(command.Arguments["lastAttemptDay"]);
        entry["lastErrorCode"] = (string)command.Arguments["lastErrorCode"] ?? string.Empty;
        entry["contentFingerprint"] = (string)report["contentFingerprint"];
        entry["sourceFactIds"] = report["sourceFactIds"].DeepClone();
        entry["report"] = report.DeepClone();
    }

    private static bool IsValidWeeklyReportSnapshot(JToken value, string reportId)
    {
        return value is JObject report
            && StringComparer.Ordinal.Equals((string)report["reportId"], reportId)
            && WorldEventContract.TryValidateWeeklyReport(report, out _);
    }

    private static bool IsValidV2WeeklyReportEntry(JObject entry, out JObject report)
    {
        report = entry?["report"] as JObject;
        if (entry == null || report == null || !WeeklyReportService.TryValidateV2Report(report, out _)) return false;
        return StringComparer.Ordinal.Equals((string)entry["reportId"], (string)report["reportId"])
            && StringComparer.Ordinal.Equals((string)entry["schemaVersion"], (string)report["schemaVersion"])
            && IntValue(entry["windowStartDay"]) == (int)report["extensions"]["awake:windowStartDay"]
            && IntValue(entry["windowEndDay"]) == (int)report["extensions"]["awake:windowEndDay"]
            && StringComparer.Ordinal.Equals((string)entry["contentFingerprint"], (string)report["contentFingerprint"])
            && JToken.DeepEquals(entry["sourceFactIds"], report["sourceFactIds"]);
    }

    private static bool SameV2WeeklyReport(JObject entry, JObject currentReport, JObject incomingReport, int windowStartDay, int windowEndDay)
    {
        return currentReport != null
            && StringComparer.Ordinal.Equals((string)entry["reportId"], (string)incomingReport["reportId"])
            && IntValue(entry["windowStartDay"]) == windowStartDay
            && IntValue(entry["windowEndDay"]) == windowEndDay
            && StringComparer.Ordinal.Equals((string)entry["contentFingerprint"], (string)incomingReport["contentFingerprint"])
            && JToken.DeepEquals(entry["sourceFactIds"], incomingReport["sourceFactIds"])
            && StringComparer.Ordinal.Equals(WeeklyReportService.CanonicalizeV2(currentReport), WeeklyReportService.CanonicalizeV2(incomingReport));
    }

    private static bool IsEmptyWeeklyReport(JObject report)
    {
        return report != null
            && report["sourceEventIds"] is JArray sourceEventIds
            && sourceEventIds.Count == 0;
    }

    private static bool SameWorldEventPayload(JObject existing, WorldStateCommand command)
    {
        string kind = WorldEventContract.NormalizeEventType((string)command.Arguments["kind"] ?? "event");
        string text = ClampTextElements((string)command.Arguments["text"] ?? string.Empty, 500);
        string eventKey = ClampTextElements((string)command.Arguments["eventKey"] ?? command.IdempotencyKey, 200);
        string domain = WeeklyReportService.NormalizeDomain((string)command.Arguments["domain"], kind);
        string occurredAt = (string)command.Arguments["occurredAt"] ?? string.Empty;
        return IntValue(existing["day"]) == IntValue(command.Arguments["day"])
            && StringComparer.Ordinal.Equals((string)existing["kind"], ClampTextElements(kind, 40))
            && StringComparer.Ordinal.Equals((string)existing["text"], text)
            && StringComparer.Ordinal.Equals((string)existing["eventKey"] ?? (string)existing["id"], eventKey)
            && StringComparer.Ordinal.Equals((string)existing["domain"], domain)
            && StringComparer.Ordinal.Equals((string)existing["occurredAt"], occurredAt)
            && SameIdentityIds(existing["visibilityIdentityIds"], command.Arguments["visibilityIdentityIds"])
            && SameOptionalToken(existing["fact"], command.Arguments["fact"]);
    }

    private static bool SameOptionalToken(JToken left, JToken right)
    {
        bool leftEmpty = left == null || left.Type == JTokenType.Null;
        bool rightEmpty = right == null || right.Type == JTokenType.Null;
        return leftEmpty && rightEmpty || (!leftEmpty && !rightEmpty && JToken.DeepEquals(left, right));
    }

    private static bool SameIdentityIds(JToken left, JToken right)
    {
        return new HashSet<string>(NormalizeIdentityIds(left).Values<string>(), StringComparer.Ordinal)
            .SetEquals(NormalizeIdentityIds(right).Values<string>());
    }

    private static void EnsureMessengerShape(JObject state)
    {
        state["schema"] = "awake.messenger.v1";
        if (!(state["chats"] is JObject)) state["chats"] = new JObject();
        if (!(state["appliedKeys"] is JArray)) state["appliedKeys"] = new JArray();
    }

    private static string ApplyMessenger(JObject state, WorldStateCommand command)
    {
        EnsureMessengerShape(state);
        string targetId = (string)command.Arguments["targetId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetId)) return "awake.world_state.messenger.invalid_target";
        JObject chats = (JObject)state["chats"];
        JArray lines = chats[targetId] as JArray;
        if (lines == null)
        {
            lines = new JArray();
            chats[targetId] = lines;
        }
        lines.Add(new JObject
        {
            ["id"] = command.IdempotencyKey,
            ["speaker"] = ClampTextElements((string)command.Arguments["speaker"] ?? string.Empty, 40),
            ["text"] = ClampTextElements((string)command.Arguments["text"] ?? string.Empty, 4000),
            ["day"] = IntValue(command.Arguments["day"])
        });
        while (lines.Count > 200) lines.RemoveAt(0);
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
        if (!(state["promises"] is JArray)) state["promises"] = new JArray();
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

        if (StringComparer.Ordinal.Equals(mode, "consolidate"))
        {
            JArray consolidatedMemories = command.Arguments["memories"] as JArray;
            JArray consolidatedPromises = command.Arguments["promises"] as JArray;
            if (consolidatedMemories == null || consolidatedPromises == null)
            {
                return "awake.world_state.memory.consolidate_invalid";
            }
            Trim(consolidatedMemories, AiTaskConstants.MemoryEntriesMaximum);
            Trim(consolidatedPromises, AiTaskConstants.MemoryEntriesMaximum);
            state["memories"] = (JArray)consolidatedMemories.DeepClone();
            state["promises"] = (JArray)consolidatedPromises.DeepClone();
            appliedKeys.Add(command.IdempotencyKey);
            Trim(appliedKeys, AiTaskConstants.AppliedKeysMaximum);
            state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
            return string.Empty;
        }

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
            ["eventType"] = (string)command.Arguments["eventType"] ?? string.Empty,
            ["location"] = (string)command.Arguments["location"] ?? string.Empty,
            ["entityId"] = (string)command.Arguments["entityId"] ?? string.Empty,
            ["result"] = (string)command.Arguments["result"] ?? string.Empty,
            ["promise"] = BoolValue(command.Arguments["promise"]),
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

    private static JArray NormalizeIdentityIds(JToken value)
    {
        if (value == null || value.Type == JTokenType.Null)
            return new JArray(WorldEventAudience.Default.Select(identityId => (object)identityId));
        if (!(value is JArray array)) return new JArray();
        var identities = new List<string>();
        foreach (JToken token in array)
        {
            string normalized = WorldbookIdentityEvaluator.NormalizeIdentity((string)token);
            if (WorldEventContract.IsStableId(normalized) && !identities.Contains(normalized, StringComparer.Ordinal))
                identities.Add(normalized);
        }
        return new JArray(identities.Select(identityId => (object)identityId));
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

    private static bool ContainsString(JArray items, string value)
    {
        if (items == null) return false;
        foreach (JToken token in items)
        {
            if (StringComparer.Ordinal.Equals((string)token, value)) return true;
        }
        return false;
    }

    private static double DoubleValue(JToken token)
    {
        if (token == null) return 0d;
        try { return Convert.ToDouble(token, System.Globalization.CultureInfo.InvariantCulture); }
        catch { return 0d; }
    }

    private static bool BoolValue(JToken token)
    {
        return token != null && token.Type == JTokenType.Boolean && (bool)token;
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

    internal static string BuildInteractionKey(string canonicalContactKey)
    {
        return "campaign.interactions.v1." + (canonicalContactKey ?? string.Empty);
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

    private static JObject ParseJsonObjectPreservingDateStrings(string json)
    {
        using (StringReader reader = new StringReader(json ?? string.Empty))
        using (JsonTextReader jsonReader = new JsonTextReader(reader) { DateParseHandling = DateParseHandling.None })
        {
            return JObject.Load(jsonReader);
        }
    }
}
