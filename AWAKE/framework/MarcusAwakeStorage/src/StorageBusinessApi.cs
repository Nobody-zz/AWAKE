using System;
using System.Collections.Generic;
using MarcusAwakeFramework.Api;

namespace MarcusAwakeStorage;

public enum StorageBusinessOperation
{
    KvGet,
    KvSet,
    KvDelete,
    TimelineAppend,
    TimelineRead,
    RagIngest,
    RagSearch
}

public sealed class StorageBusinessRequest
{
    public StorageBusinessOperation Operation { get; set; }
    public string NamespaceId { get; set; } = string.Empty;
    public StorageScopeKind Scope { get; set; } = StorageScopeKind.Session;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventPayloadJson { get; set; } = string.Empty;
    public long OccurredUnixMilliseconds { get; set; }
    public string EventCorrelationId { get; set; } = string.Empty;
    public string EventCausationId { get; set; } = string.Empty;
    public long AfterSequence { get; set; }
    public int MaximumResults { get; set; }
    public string CollectionId { get; set; } = string.Empty;
    public string CorpusFingerprint { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public RetrievalMode Mode { get; set; } = RetrievalMode.Keyword;
    public IReadOnlyList<string> AccessScopes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<RagDocument> Documents { get; set; } = Array.Empty<RagDocument>();
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ResourceKey { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
}

public sealed class StorageBusinessResult
{
    public StorageBusinessResult(
        string operation,
        string payloadJson,
        bool durable,
        string outcome,
        long eventIndex,
        string resourceKey)
    {
        Operation = operation ?? string.Empty;
        PayloadJson = payloadJson ?? "{}";
        Durable = durable;
        Outcome = outcome ?? string.Empty;
        EventIndex = eventIndex;
        ResourceKey = resourceKey ?? string.Empty;
    }

    public string Operation { get; }
    public string PayloadJson { get; }
    public bool Durable { get; }
    public string Outcome { get; }
    public long EventIndex { get; }
    public string ResourceKey { get; }
}
