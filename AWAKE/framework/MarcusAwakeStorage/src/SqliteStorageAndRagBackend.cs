using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Microsoft.Data.Sqlite;

namespace MarcusAwakeStorage;

public sealed class TimelineLedgerEvent
{
    public TimelineLedgerEvent(string eventId, string eventType, string payloadJson, DateTimeOffset occurredAt, string correlationId, string causationId)
    {
        EventId = RequireId(eventId, nameof(eventId));
        EventType = RequireId(eventType, nameof(eventType));
        PayloadJson = payloadJson ?? string.Empty;
        OccurredAt = occurredAt;
        CorrelationId = correlationId ?? string.Empty;
        CausationId = causationId ?? string.Empty;
    }

    public string EventId { get; }
    public string EventType { get; }
    public string PayloadJson { get; }
    public DateTimeOffset OccurredAt { get; }
    public string CorrelationId { get; }
    public string CausationId { get; }

    private static string RequireId(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A stable identifier is required.", name);
        return value.Trim();
    }
}

public sealed class TimelineLedgerRecord
{
    internal TimelineLedgerRecord(long sequence, string eventId, string eventType, string payloadJson, DateTimeOffset occurredAt, string sessionId, string correlationId, string causationId)
    {
        Sequence = sequence;
        EventId = eventId;
        EventType = eventType;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        SessionId = sessionId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }

    public long Sequence { get; }
    public string EventId { get; }
    public string EventType { get; }
    public string PayloadJson { get; }
    public DateTimeOffset OccurredAt { get; }
    public string SessionId { get; }
    public string CorrelationId { get; }
    public string CausationId { get; }
}

public sealed partial class SqliteStorageAndRagBackend : IStorageService, IRagService, IDisposable, IAsyncDisposable
{
    private const int SchemaVersion = 2;
    private const string ErrorOwner = "MarcusAwakeStorage";
    private static readonly object ProviderSync = new object();
    private static bool providerInitialized;

    private readonly string databasePath;
    private readonly SqliteStorageOptions options;
    private readonly string connectionString;
    private readonly SemaphoreSlim databaseGate = new SemaphoreSlim(1, 1);
    private readonly IRagEmbedder embedder;
    private int disposed;

    /// <summary>
    /// <paramref name="embedder"/> is optional on purpose. Without one this backend behaves exactly
    /// as it always has: keyword retrieval only, and a Semantic request is refused rather than
    /// silently downgraded. That keeps ONNX and a 90 MB model out of every host that merely needs
    /// the storage half. When one is supplied the backend owns it and disposes it on shutdown.
    /// </summary>
    public SqliteStorageAndRagBackend(string databasePath, SqliteStorageOptions options = null, IRagEmbedder embedder = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("A database path is required.", nameof(databasePath));
        this.databasePath = Path.GetFullPath(databasePath);
        this.options = options ?? new SqliteStorageOptions();
        this.options.Validate();
        this.embedder = embedder;
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = this.databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = Math.Max(1, (int)Math.Ceiling(this.options.BusyTimeoutMilliseconds / 1000d))
        }.ToString();
    }

    public string DatabasePath => databasePath;

    /// <summary>Identity of the embedding function backing Semantic retrieval, or null when this
    /// backend has none and only answers Keyword requests.</summary>
    public string EmbeddingModelId => embedder == null ? null : embedder.ModelId;

    public Task<OperationResult<IKeyValueStore>> OpenCampaignNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken)
    {
        return OpenNamespaceAsync(namespaceId, StorageScopeKind.Campaign, context, cancellationToken);
    }

    public Task<OperationResult<IKeyValueStore>> OpenSessionNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken)
    {
        return OpenNamespaceAsync(namespaceId, StorageScopeKind.Session, context, cancellationToken);
    }

    public Task<OperationResult<long>> AppendTimelineEventAsync(TimelineLedgerEvent entry, RequestContext context, CancellationToken cancellationToken)
    {
        if (entry == null || context == null)
        {
            return Task.FromResult(Failure<long>("storage.invalid_request", FrameworkErrorCategory.InvalidRequest, "A timeline event and request context are required.", context));
        }
        if (ByteCount(entry.PayloadJson) > options.MaxLedgerPayloadBytes)
        {
            return Task.FromResult(Failure<long>("storage.ledger_payload_too_large", FrameworkErrorCategory.ResourceExhausted, "The timeline event payload exceeds the configured limit.", context));
        }
        return ExecuteAsync("storage", context, cancellationToken, (connection, token) => AppendTimelineEvent(connection, entry, context, token));
    }

    public Task<OperationResult<IReadOnlyList<TimelineLedgerRecord>>> ReadTimelineAsync(long afterSequence, int maximumResults, RequestContext context, CancellationToken cancellationToken)
    {
        if (context == null || afterSequence < 0 || maximumResults < 1 || maximumResults > options.MaxLedgerRead)
        {
            return Task.FromResult(Failure<IReadOnlyList<TimelineLedgerRecord>>("storage.invalid_request", FrameworkErrorCategory.InvalidRequest, "A valid timeline cursor, limit and request context are required.", context));
        }
        return ExecuteAsync("storage", context, cancellationToken, (connection, token) => ReadTimeline(connection, afterSequence, maximumResults, context, token));
    }

    public Task<OperationResult<int>> IngestAsync(RagIngestRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        if (request == null || context == null)
        {
            return Task.FromResult(Failure<int>("rag.invalid_request", FrameworkErrorCategory.InvalidRequest, "A RAG ingest request and request context are required.", context));
        }
        for (var index = 0; index < request.Documents.Count; index++)
        {
            if (ByteCount(request.Documents[index].Text) > options.MaxDocumentBytes)
            {
                return Task.FromResult(Failure<int>("rag.document_too_large", FrameworkErrorCategory.ResourceExhausted, "A RAG document exceeds the configured limit.", context));
            }
        }

        // Embedding runs before the database gate is taken: it is the slow part (tens of
        // milliseconds per passage) and holding the storage gate across it would stall every other
        // storage caller for the whole ingest.
        float[][] vectors = null;
        if (embedder != null)
        {
            var texts = new string[request.Documents.Count];
            for (var index = 0; index < texts.Length; index++) texts[index] = request.Documents[index].Text;
            try
            {
                vectors = embedder.Encode(texts, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // The documents are still worth storing: keyword retrieval keeps working, and the
                // next semantic search reports the failure instead of returning an empty answer.
                vectors = null;
            }
        }

        return ExecuteAsync("rag", context, cancellationToken, (connection, token) => Ingest(connection, request, context, token, vectors));
    }

    public Task<OperationResult<IReadOnlyList<RagHit>>> SearchAsync(RagSearchRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        if (request == null || context == null)
        {
            return Task.FromResult(Failure<IReadOnlyList<RagHit>>("rag.invalid_request", FrameworkErrorCategory.InvalidRequest, "A RAG search request and request context are required.", context));
        }
        if (request.Mode == RetrievalMode.Semantic && embedder == null)
        {
            return Task.FromResult(Failure<IReadOnlyList<RagHit>>("rag.retrieval_mode_unsupported", FrameworkErrorCategory.Unsupported, "Semantic retrieval is unavailable because this backend was built without an embedding model.", context));
        }
        if (request.Mode == RetrievalMode.Hybrid)
        {
            return Task.FromResult(Failure<IReadOnlyList<RagHit>>("rag.retrieval_mode_unsupported", FrameworkErrorCategory.Unsupported, "Only keyword and semantic retrieval are available in this backend.", context));
        }
        var identityValidation = ValidateSearchIdentity(request, context);
        if (identityValidation != null) return Task.FromResult(OperationResult<IReadOnlyList<RagHit>>.Failed(identityValidation));
        if (ByteCount(request.Query) > options.MaxQueryBytes)
        {
            return Task.FromResult(Failure<IReadOnlyList<RagHit>>("rag.query_too_large", FrameworkErrorCategory.ResourceExhausted, "The RAG query exceeds the configured limit.", context));
        }
        return ExecuteAsync("rag", context, cancellationToken, (connection, token) => request.Mode == RetrievalMode.Semantic
            ? SearchSemantic(connection, request, context, token)
            : Search(connection, request, context, token));
    }
    private Task<OperationResult<IKeyValueStore>> OpenNamespaceAsync(string namespaceId, StorageScopeKind scope, RequestContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(namespaceId) || context == null)
        {
            return Task.FromResult(Failure<IKeyValueStore>("storage.invalid_request", FrameworkErrorCategory.InvalidRequest, "A namespace and request context are required.", context));
        }
        var reference = new StorageNamespaceRef(context.Caller.Value, context.Session, namespaceId, scope);
        return ExecuteAsync("storage", context, cancellationToken, (connection, token) =>
        {
            return OperationResult<IKeyValueStore>.Succeeded(new KeyValueStore(this, reference, context.SessionGeneration));
        });
    }

    private OperationResult<long> AppendTimelineEvent(SqliteConnection connection, TimelineLedgerEvent entry, RequestContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = connection.BeginTransaction();
        using (var existingCommand = CreateCommand(connection, "SELECT sequence,event_type,payload_json,occurred_unix_ms,correlation_id,causation_id FROM timeline_ledger WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND event_id=@event_id;", transaction))
        {
            AddParameter(existingCommand, "@owner", context.Caller.Value);
            AddParameter(existingCommand, "@campaign", context.Session.CampaignGuid);
            AddParameter(existingCommand, "@timeline", context.Session.TimelineId);
            AddParameter(existingCommand, "@event_id", entry.EventId);
            using var reader = existingCommand.ExecuteReader();
            if (reader.Read())
            {
                var same = StringComparer.Ordinal.Equals(entry.EventType, reader.GetString(1))
                    && StringComparer.Ordinal.Equals(entry.PayloadJson, reader.GetString(2))
                    && entry.OccurredAt.ToUnixTimeMilliseconds() == reader.GetInt64(3)
                    && StringComparer.Ordinal.Equals(entry.CorrelationId, reader.GetString(4))
                    && StringComparer.Ordinal.Equals(entry.CausationId, reader.GetString(5));
                if (same)
                {
                    transaction.Commit();
                    return OperationResult<long>.Succeeded(reader.GetInt64(0));
                }
                transaction.Rollback();
                return Failure<long>("storage.ledger_event_conflict", FrameworkErrorCategory.Conflict, "The timeline event ID already contains different data.", context);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        long sequence;
        using (var sequenceCommand = CreateCommand(connection, "SELECT COALESCE(MAX(sequence),0)+1 FROM timeline_ledger WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline;", transaction))
        {
            AddParameter(sequenceCommand, "@owner", context.Caller.Value);
            AddParameter(sequenceCommand, "@campaign", context.Session.CampaignGuid);
            AddParameter(sequenceCommand, "@timeline", context.Session.TimelineId);
            sequence = Convert.ToInt64(sequenceCommand.ExecuteScalar());
        }

        using (var insertCommand = CreateCommand(connection, "INSERT INTO timeline_ledger(owner_id,campaign_guid,timeline_id,sequence,event_id,event_type,payload_json,occurred_unix_ms,session_id,correlation_id,causation_id) VALUES(@owner,@campaign,@timeline,@sequence,@event_id,@event_type,@payload,@occurred,@session,@correlation,@causation);", transaction))
        {
            AddParameter(insertCommand, "@owner", context.Caller.Value);
            AddParameter(insertCommand, "@campaign", context.Session.CampaignGuid);
            AddParameter(insertCommand, "@timeline", context.Session.TimelineId);
            AddParameter(insertCommand, "@sequence", sequence);
            AddParameter(insertCommand, "@event_id", entry.EventId);
            AddParameter(insertCommand, "@event_type", entry.EventType);
            AddParameter(insertCommand, "@payload", entry.PayloadJson);
            AddParameter(insertCommand, "@occurred", entry.OccurredAt.ToUnixTimeMilliseconds());
            AddParameter(insertCommand, "@session", context.Session.SessionId);
            AddParameter(insertCommand, "@correlation", entry.CorrelationId);
            AddParameter(insertCommand, "@causation", entry.CausationId);
            insertCommand.ExecuteNonQuery();
        }
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return OperationResult<long>.Succeeded(sequence);
    }

    private OperationResult<IReadOnlyList<TimelineLedgerRecord>> ReadTimeline(SqliteConnection connection, long afterSequence, int maximumResults, RequestContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var records = new List<TimelineLedgerRecord>();
        using var command = CreateCommand(connection, "SELECT sequence,event_id,event_type,payload_json,occurred_unix_ms,session_id,correlation_id,causation_id FROM timeline_ledger WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND sequence>@after_sequence ORDER BY sequence ASC LIMIT @maximum_results;", null);
        AddParameter(command, "@owner", context.Caller.Value);
        AddParameter(command, "@campaign", context.Session.CampaignGuid);
        AddParameter(command, "@timeline", context.Session.TimelineId);
        AddParameter(command, "@after_sequence", afterSequence);
        AddParameter(command, "@maximum_results", maximumResults);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            records.Add(new TimelineLedgerRecord(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4)), reader.GetString(5), reader.GetString(6), reader.GetString(7)));
        }
        return OperationResult<IReadOnlyList<TimelineLedgerRecord>>.Succeeded(records.AsReadOnly());
    }

    private OperationResult<int> Ingest(SqliteConnection connection, RagIngestRequest request, RequestContext context, CancellationToken cancellationToken, float[][] vectors)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = connection.BeginTransaction();
        var storedFingerprint = GetCollectionFingerprint(connection, request.CollectionId, context, transaction);
        if (storedFingerprint != null && !StringComparer.Ordinal.Equals(storedFingerprint, request.CorpusFingerprint))
        {
            transaction.Rollback();
            return Failure<int>("rag.corpus_fingerprint_conflict", FrameworkErrorCategory.Conflict, "The collection already contains a different corpus fingerprint.", context);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (storedFingerprint == null)
        {
            using var createCollection = CreateCommand(connection, "INSERT INTO rag_collections(owner_id,campaign_guid,timeline_id,collection_id,corpus_fingerprint,created_unix_ms,updated_unix_ms) VALUES(@owner,@campaign,@timeline,@collection,@fingerprint,@now,@now);", transaction);
            AddCollectionParameters(createCollection, request.CollectionId, request.CorpusFingerprint, context);
            AddParameter(createCollection, "@now", now);
            createCollection.ExecuteNonQuery();
        }
        else
        {
            using var updateCollection = CreateCommand(connection, "UPDATE rag_collections SET updated_unix_ms=@now WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND collection_id=@collection;", transaction);
            AddParameter(updateCollection, "@now", now);
            AddCollectionIdentityParameters(updateCollection, request.CollectionId, context);
            updateCollection.ExecuteNonQuery();
        }

        for (var index = 0; index < request.Documents.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = request.Documents[index];
            UpsertDocument(connection, transaction, request, context, document);
            RefreshFtsDocument(connection, transaction, request.CollectionId, context, document);
        }

        WriteEmbeddings(connection, transaction, request, context, vectors, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return OperationResult<int>.Succeeded(request.Documents.Count);
    }

    private OperationResult<IReadOnlyList<RagHit>> Search(SqliteConnection connection, RagSearchRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = connection.BeginTransaction();
        var storedFingerprint = GetCollectionFingerprint(connection, request.CollectionId, context, transaction);
        if (storedFingerprint == null || !StringComparer.Ordinal.Equals(storedFingerprint, request.CorpusFingerprint))
        {
            transaction.Rollback();
            return Failure<IReadOnlyList<RagHit>>("rag.index_stale", FrameworkErrorCategory.Conflict, "The RAG corpus fingerprint is stale.", context, true);
        }

        var ftsQuery = BuildFtsQuery(request.Query);
        if (ftsQuery.Length == 0)
        {
            transaction.Commit();
            return OperationResult<IReadOnlyList<RagHit>>.Succeeded(Array.Empty<RagHit>());
        }

        var scopeClause = new StringBuilder();
        var scopeParameters = new List<KeyValuePair<string, string>>();
        if (request.AccessScopes.Count > 0)
        {
            scopeClause.Append(" AND d.access_scope IN (");
            for (var index = 0; index < request.AccessScopes.Count; index++)
            {
                if (index > 0) scopeClause.Append(",");
                var parameterName = "@scope" + index;
                scopeClause.Append(parameterName);
                scopeParameters.Add(new KeyValuePair<string, string>(parameterName, request.AccessScopes[index] ?? string.Empty));
            }
            scopeClause.Append(")");
        }

        var sql = "SELECT d.document_id,d.text,d.source_locator,bm25(rag_documents_fts) FROM rag_documents_fts JOIN rag_documents AS d ON d.owner_id=rag_documents_fts.owner_id AND d.campaign_guid=rag_documents_fts.campaign_guid AND d.timeline_id=rag_documents_fts.timeline_id AND d.collection_id=rag_documents_fts.collection_id AND d.document_id=rag_documents_fts.document_id WHERE rag_documents_fts.owner_id=@owner AND rag_documents_fts.campaign_guid=@campaign AND rag_documents_fts.timeline_id=@timeline AND rag_documents_fts.collection_id=@collection AND rag_documents_fts MATCH @fts_query" + scopeClause + " ORDER BY bm25(rag_documents_fts) ASC,d.document_id COLLATE BINARY ASC LIMIT @maximum_results;";
        var hits = new List<RagHit>();
        var totalBytes = 0;
        using var command = CreateCommand(connection, sql, transaction);
        AddParameter(command, "@owner", context.Caller.Value);
        AddParameter(command, "@campaign", context.Session.CampaignGuid);
        AddParameter(command, "@timeline", context.Session.TimelineId);
        AddParameter(command, "@collection", request.CollectionId);
        AddParameter(command, "@fts_query", ftsQuery);
        AddParameter(command, "@maximum_results", request.MaximumResults);
        for (var index = 0; index < scopeParameters.Count; index++) AddParameter(command, scopeParameters[index].Key, scopeParameters[index].Value);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = reader.GetString(1);
            var textBytes = ByteCount(text);
            if (totalBytes + textBytes > options.MaxResultBytes) break;
            totalBytes += textBytes;
            var score = reader.IsDBNull(3) ? 0d : reader.GetDouble(3);
            hits.Add(new RagHit(reader.GetString(0), text, reader.GetString(2), ScoreToRank(score), request.CorpusFingerprint, request.Mode));
        }
        transaction.Commit();
        return OperationResult<IReadOnlyList<RagHit>>.Succeeded(hits.AsReadOnly());
    }
    private string GetCollectionFingerprint(SqliteConnection connection, string collectionId, RequestContext context, SqliteTransaction transaction)
    {
        using var command = CreateCommand(connection, "SELECT corpus_fingerprint FROM rag_collections WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND collection_id=@collection;", transaction);
        AddCollectionIdentityParameters(command, collectionId, context);
        var value = command.ExecuteScalar();
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    private static void UpsertDocument(SqliteConnection connection, SqliteTransaction transaction, RagIngestRequest request, RequestContext context, RagDocument document)
    {
        using var command = CreateCommand(connection, "INSERT INTO rag_documents(owner_id,campaign_guid,timeline_id,collection_id,document_id,text,source_locator,access_scope,source_class,corpus_locator,observed_unix_ms) VALUES(@owner,@campaign,@timeline,@collection,@document,@text,@source_locator,@access_scope,@source_class,@corpus_locator,@observed) ON CONFLICT(owner_id,campaign_guid,timeline_id,collection_id,document_id) DO UPDATE SET text=excluded.text,source_locator=excluded.source_locator,access_scope=excluded.access_scope,source_class=excluded.source_class,corpus_locator=excluded.corpus_locator,observed_unix_ms=excluded.observed_unix_ms;", transaction);
        AddCollectionIdentityParameters(command, request.CollectionId, context);
        AddParameter(command, "@document", document.DocumentId);
        AddParameter(command, "@text", document.Text);
        AddParameter(command, "@source_locator", document.SourceLocator);
        AddParameter(command, "@access_scope", document.AccessScope);
        AddParameter(command, "@source_class", document.SourceClass);
        AddParameter(command, "@corpus_locator", document.CorpusLocator);
        AddParameter(command, "@observed", document.ObservedAt.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    private static void RefreshFtsDocument(SqliteConnection connection, SqliteTransaction transaction, string collectionId, RequestContext context, RagDocument document)
    {
        using (var deleteCommand = CreateCommand(connection, "DELETE FROM rag_documents_fts WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND collection_id=@collection AND document_id=@document;", transaction))
        {
            AddCollectionIdentityParameters(deleteCommand, collectionId, context);
            AddParameter(deleteCommand, "@document", document.DocumentId);
            deleteCommand.ExecuteNonQuery();
        }
        using (var insertCommand = CreateCommand(connection, "INSERT INTO rag_documents_fts(owner_id,campaign_guid,timeline_id,collection_id,document_id,text) VALUES(@owner,@campaign,@timeline,@collection,@document,@text);", transaction))
        {
            AddCollectionIdentityParameters(insertCommand, collectionId, context);
            AddParameter(insertCommand, "@document", document.DocumentId);
            AddParameter(insertCommand, "@text", document.Text);
            insertCommand.ExecuteNonQuery();
        }
    }

    private static void AddCollectionIdentityParameters(SqliteCommand command, string collectionId, RequestContext context)
    {
        AddParameter(command, "@owner", context.Caller.Value);
        AddParameter(command, "@campaign", context.Session.CampaignGuid);
        AddParameter(command, "@timeline", context.Session.TimelineId);
        AddParameter(command, "@collection", collectionId);
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, string sql, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    private static void AddCollectionParameters(SqliteCommand command, string collectionId, string fingerprint, RequestContext context)
    {
        AddCollectionIdentityParameters(command, collectionId, context);
        AddParameter(command, "@fingerprint", fingerprint);
    }

    private sealed class Fts5UnavailableException : Exception
    {
    }

    private sealed class SchemaIncompatibleException : Exception
    {
    }

    private static void AddParameter(SqliteCommand command, string name, object value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static FrameworkError ValidateSearchIdentity(RagSearchRequest request, RequestContext context)
    {
        if (HasValue(request.OwnerId) && !StringComparer.Ordinal.Equals(request.OwnerId, context.Caller.Value)) return CreateError("rag.scope_denied", FrameworkErrorCategory.Denied, "The search owner is outside the request scope.", context);
        if (HasValue(request.CampaignGuid) && !StringComparer.Ordinal.Equals(request.CampaignGuid, context.Session.CampaignGuid)) return CreateError("rag.scope_denied", FrameworkErrorCategory.Denied, "The search campaign is outside the request scope.", context);
        if (HasValue(request.TimelineId) && !StringComparer.Ordinal.Equals(request.TimelineId, context.Session.TimelineId)) return CreateError("rag.scope_denied", FrameworkErrorCategory.Denied, "The search timeline is outside the request scope.", context);
        if (HasValue(request.SessionId) && !StringComparer.Ordinal.Equals(request.SessionId, context.Session.SessionId)) return CreateError("rag.scope_denied", FrameworkErrorCategory.Denied, "The search session is outside the request scope.", context);
        return null;
    }

    private FrameworkError ValidateHandle(StorageNamespaceRef reference, long generation, string key, RequestContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || context == null) return CreateError("storage.invalid_request", FrameworkErrorCategory.InvalidRequest, "A storage key and request context are required.", context);
        if (cancellationToken == CancellationToken.None) return CreateError("storage.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context);
        if (!StringComparer.Ordinal.Equals(reference.OwnerId, context.Caller.Value) || !reference.Session.Equals(context.Session) || generation != context.SessionGeneration)
        {
            return CreateError("storage.scope_denied", FrameworkErrorCategory.Denied, "The storage namespace is outside the request scope.", context);
        }
        return null;
    }

    private async Task<OperationResult<T>> ExecuteAsync<T>(string area, RequestContext context, CancellationToken callerToken, Func<SqliteConnection, CancellationToken, OperationResult<T>> operation)
    {
        if (Volatile.Read(ref disposed) != 0) return Failure<T>(area + ".disposed", FrameworkErrorCategory.Unavailable, "The storage backend is disposed.", context);
        if (context == null) return Failure<T>(area + ".invalid_request", FrameworkErrorCategory.InvalidRequest, "A request context is required.", null);
        if (callerToken == CancellationToken.None) return Failure<T>(area + ".cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context);
        if (callerToken.IsCancellationRequested || context.CancellationToken.IsCancellationRequested) return Failure<T>("awake.cancelled", FrameworkErrorCategory.Cancelled, "The operation was cancelled.", context);
        if (context.IsExpiredAt(DateTimeOffset.UtcNow)) return Failure<T>(area + ".deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context);

        using var callerAndSession = CancellationTokenSource.CreateLinkedTokenSource(callerToken, context.CancellationToken);
        CancellationTokenSource deadlineSource = null;
        var remaining = context.Deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return Failure<T>(area + ".deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context);
        if (remaining < TimeSpan.FromMilliseconds(int.MaxValue))
        {
            deadlineSource = new CancellationTokenSource();
            deadlineSource.CancelAfter(remaining);
        }
        using (deadlineSource)
        using (var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(callerAndSession.Token, deadlineSource?.Token ?? CancellationToken.None))
        {
            var gateAcquired = false;
            try
            {
                await databaseGate.WaitAsync(operationCancellation.Token).ConfigureAwait(false);
                gateAcquired = true;
                if (Volatile.Read(ref disposed) != 0) return Failure<T>(area + ".disposed", FrameworkErrorCategory.Unavailable, "The storage backend is disposed.", context);
                operationCancellation.Token.ThrowIfCancellationRequested();
                var task = Task.Run(() => ExecuteDatabase(area, context, operationCancellation.Token, operation), CancellationToken.None);
                return await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (deadlineSource != null && deadlineSource.IsCancellationRequested && !callerToken.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested) return Failure<T>(area + ".deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context);
                return Failure<T>("awake.cancelled", FrameworkErrorCategory.Cancelled, "The operation was cancelled.", context);
            }
            catch (Fts5UnavailableException)
            {
                return Failure<T>(area + ".fts5_unavailable", FrameworkErrorCategory.Unavailable, "SQLite FTS5 is unavailable in the deployed runtime.", context);
            }
            catch (SchemaIncompatibleException)
            {
                return Failure<T>(area + ".schema_incompatible", FrameworkErrorCategory.Incompatible, "The database schema is newer than this backend.", context);
            }
            catch (SqliteException exception)
            {
                var busy = exception.SqliteErrorCode == 5 || exception.SqliteErrorCode == 6;
                return Failure<T>(area + (busy ? ".database_busy" : ".database_failure"), busy ? FrameworkErrorCategory.Timeout : FrameworkErrorCategory.InternalFailure, busy ? "The SQLite database remained busy past the configured timeout." : "The SQLite operation failed.", context, busy);
            }
            catch (UnauthorizedAccessException)
            {
                return Failure<T>(area + ".database_unavailable", FrameworkErrorCategory.Unavailable, "The SQLite database path is not accessible.", context);
            }
            catch (IOException)
            {
                return Failure<T>(area + ".database_unavailable", FrameworkErrorCategory.Unavailable, "The SQLite database path is not available.", context);
            }
            finally
            {
                if (gateAcquired) databaseGate.Release();
            }
        }
    }

    private OperationResult<T> ExecuteDatabase<T>(string area, RequestContext context, CancellationToken cancellationToken, Func<SqliteConnection, CancellationToken, OperationResult<T>> operation)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        InitializeSqliteProvider();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        ConfigureConnection(connection);
        EnsureSchema(connection);
        cancellationToken.ThrowIfCancellationRequested();
        return operation(connection, cancellationToken);
    }
    private static void InitializeSqliteProvider()
    {
        if (providerInitialized) return;
        lock (ProviderSync)
        {
            if (providerInitialized) return;
            SQLitePCL.Batteries_V2.Init();
            providerInitialized = true;
        }
    }

    private void ConfigureConnection(SqliteConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = " + options.BusyTimeoutMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";";
            command.ExecuteNonQuery();
        }
        using (var journalCommand = connection.CreateCommand())
        {
            journalCommand.CommandText = "PRAGMA journal_mode = WAL;";
            journalCommand.ExecuteScalar();
        }
        using (var synchronousCommand = connection.CreateCommand())
        {
            synchronousCommand.CommandText = "PRAGMA synchronous = NORMAL;";
            synchronousCommand.ExecuteNonQuery();
        }
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        long userVersion;
        using (var versionCommand = connection.CreateCommand())
        {
            versionCommand.CommandText = "PRAGMA user_version;";
            userVersion = Convert.ToInt64(versionCommand.ExecuteScalar());
        }
        if (userVersion > SchemaVersion) throw new SchemaIncompatibleException();

        ExecuteNonQuery(connection, "CREATE TABLE IF NOT EXISTS kv_entries(owner_id TEXT NOT NULL,campaign_guid TEXT NOT NULL,timeline_id TEXT NOT NULL,session_id TEXT NOT NULL,scope_kind TEXT NOT NULL,namespace_id TEXT NOT NULL,key TEXT NOT NULL,value_json TEXT NOT NULL,updated_unix_ms INTEGER NOT NULL,PRIMARY KEY(owner_id,campaign_guid,timeline_id,session_id,scope_kind,namespace_id,key));");
        ExecuteNonQuery(connection, "CREATE TABLE IF NOT EXISTS timeline_ledger(owner_id TEXT NOT NULL,campaign_guid TEXT NOT NULL,timeline_id TEXT NOT NULL,sequence INTEGER NOT NULL,event_id TEXT NOT NULL,event_type TEXT NOT NULL,payload_json TEXT NOT NULL,occurred_unix_ms INTEGER NOT NULL,session_id TEXT NOT NULL,correlation_id TEXT NOT NULL,causation_id TEXT NOT NULL,PRIMARY KEY(owner_id,campaign_guid,timeline_id,sequence),UNIQUE(owner_id,campaign_guid,timeline_id,event_id));");
        ExecuteNonQuery(connection, "CREATE TABLE IF NOT EXISTS rag_collections(owner_id TEXT NOT NULL,campaign_guid TEXT NOT NULL,timeline_id TEXT NOT NULL,collection_id TEXT NOT NULL,corpus_fingerprint TEXT NOT NULL,created_unix_ms INTEGER NOT NULL,updated_unix_ms INTEGER NOT NULL,PRIMARY KEY(owner_id,campaign_guid,timeline_id,collection_id));");
        ExecuteNonQuery(connection, "CREATE TABLE IF NOT EXISTS rag_documents(owner_id TEXT NOT NULL,campaign_guid TEXT NOT NULL,timeline_id TEXT NOT NULL,collection_id TEXT NOT NULL,document_id TEXT NOT NULL,text TEXT NOT NULL,source_locator TEXT NOT NULL,access_scope TEXT NOT NULL,source_class TEXT NOT NULL,corpus_locator TEXT NOT NULL,observed_unix_ms INTEGER NOT NULL,PRIMARY KEY(owner_id,campaign_guid,timeline_id,collection_id,document_id),FOREIGN KEY(owner_id,campaign_guid,timeline_id,collection_id) REFERENCES rag_collections(owner_id,campaign_guid,timeline_id,collection_id) ON DELETE CASCADE);");
        ExecuteNonQuery(connection, "CREATE TABLE IF NOT EXISTS business_receipts(owner_id TEXT NOT NULL,campaign_guid TEXT NOT NULL,timeline_id TEXT NOT NULL,effective_session_id TEXT NOT NULL,operation TEXT NOT NULL,idempotency_key TEXT NOT NULL,payload_sha256 TEXT NOT NULL,resource_key TEXT NOT NULL,response_schema TEXT NOT NULL,response_json TEXT NOT NULL,outcome TEXT NOT NULL,event_index INTEGER NOT NULL,created_unix_ms INTEGER NOT NULL,PRIMARY KEY(owner_id,campaign_guid,timeline_id,effective_session_id,operation,idempotency_key));");
        // Semantic retrieval adds a table but changes no existing one, so the schema version stays
        // put: a database written by this build still opens in a build that has no embeddings.
        CreateSemanticSchema(connection);
        try
        {
            ExecuteNonQuery(connection, "CREATE VIRTUAL TABLE IF NOT EXISTS rag_documents_fts USING fts5(owner_id UNINDEXED,campaign_guid UNINDEXED,timeline_id UNINDEXED,collection_id UNINDEXED,document_id UNINDEXED,text,tokenize='unicode61');");
        }
        catch (SqliteException exception) when (exception.Message.IndexOf("fts5", StringComparison.OrdinalIgnoreCase) >= 0 || exception.Message.IndexOf("no such module", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            throw new Fts5UnavailableException();
        }
        if (userVersion < SchemaVersion)
        {
            using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version = " + SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";";
            versionCommand.ExecuteNonQuery();
        }
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string BuildFtsQuery(string query)
    {
        var terms = new List<string>();
        var current = new StringBuilder();
        for (var index = 0; index < query.Length; index++)
        {
            var character = query[index];
            if (char.IsLetterOrDigit(character))
            {
                current.Append(character);
            }
            else if (current.Length > 0)
            {
                terms.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0) terms.Add(current.ToString());
        return string.Join(" OR ", terms.Select(term => "\"" + term.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
    }

    private static int ScoreToRank(double score)
    {
        var scaled = Math.Abs(score) * 1000000d;
        if (scaled >= int.MaxValue) return int.MaxValue;
        return Math.Max(1, (int)Math.Round(scaled, MidpointRounding.AwayFromZero));
    }

    private static int ByteCount(string value)
    {
        return Encoding.UTF8.GetByteCount(value ?? string.Empty);
    }

    private static bool HasValue(string value)
    {
        return !string.IsNullOrEmpty(value);
    }

    private static FrameworkError CreateError(string code, FrameworkErrorCategory category, string fallback, RequestContext context, bool retryable = false)
    {
        return FrameworkErrors.Create(code, category, fallback, context?.CorrelationId ?? "no-correlation", retryable, ErrorOwner);
    }

    private static OperationResult<T> Failure<T>(string code, FrameworkErrorCategory category, string fallback, RequestContext context, bool retryable = false)
    {
        return OperationResult<T>.Failed(CreateError(code, category, fallback, context, retryable));
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await databaseGate.WaitAsync().ConfigureAwait(false);
        databaseGate.Release();
        databaseGate.Dispose();
        // The embedder was handed over at construction and has no other owner: the ONNX session
        // behind it holds native memory that only a dispose gives back. OnnxSentenceEmbedder guards
        // against a double dispose, so a caller that kept its own reference stays safe.
        try { embedder?.Dispose(); } catch { }
        GC.SuppressFinalize(this);
    }

    private sealed class KeyValueStore : IKeyValueStore
    {
        private readonly SqliteStorageAndRagBackend backend;
        private readonly StorageNamespaceRef reference;
        private readonly long generation;

        internal KeyValueStore(SqliteStorageAndRagBackend backend, StorageNamespaceRef reference, long generation)
        {
            this.backend = backend;
            this.reference = reference;
            this.generation = generation;
        }

        public Task<OperationResult<string>> GetAsync(string key, RequestContext context, CancellationToken cancellationToken)
        {
            var validation = backend.ValidateHandle(reference, generation, key, context, cancellationToken);
            if (validation != null) return Task.FromResult(OperationResult<string>.Failed(validation));
            return backend.ExecuteAsync("storage", context, cancellationToken, (connection, token) =>
            {
                token.ThrowIfCancellationRequested();
                using var command = CreateCommand(connection, "SELECT value_json FROM kv_entries WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND session_id=@session AND scope_kind=@scope AND namespace_id=@namespace AND key=@key;", null);
                AddKeyParameters(command, reference, key);
                var value = command.ExecuteScalar();
                return OperationResult<string>.Succeeded(value == null || value == DBNull.Value ? null : Convert.ToString(value));
            });
        }

        public Task<OperationResult<bool>> SetAsync(string key, string value, RequestContext context, CancellationToken cancellationToken)
        {
            var validation = backend.ValidateHandle(reference, generation, key, context, cancellationToken);
            if (validation != null) return Task.FromResult(OperationResult<bool>.Failed(validation));
            value = value ?? string.Empty;
            if (ByteCount(value) > backend.options.MaxValueBytes) return Task.FromResult(Failure<bool>("storage.value_too_large", FrameworkErrorCategory.ResourceExhausted, "The storage value exceeds the configured limit.", context));
            return backend.ExecuteAsync("storage", context, cancellationToken, (connection, token) =>
            {
                token.ThrowIfCancellationRequested();
                using var command = CreateCommand(connection, "INSERT INTO kv_entries(owner_id,campaign_guid,timeline_id,session_id,scope_kind,namespace_id,key,value_json,updated_unix_ms) VALUES(@owner,@campaign,@timeline,@session,@scope,@namespace,@key,@value,@updated) ON CONFLICT(owner_id,campaign_guid,timeline_id,session_id,scope_kind,namespace_id,key) DO UPDATE SET value_json=excluded.value_json,updated_unix_ms=excluded.updated_unix_ms;", null);
                AddKeyParameters(command, reference, key);
                AddParameter(command, "@value", value);
                AddParameter(command, "@updated", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                command.ExecuteNonQuery();
                return OperationResult<bool>.Succeeded(true);
            });
        }

        public Task<OperationResult<bool>> DeleteAsync(string key, RequestContext context, CancellationToken cancellationToken)
        {
            var validation = backend.ValidateHandle(reference, generation, key, context, cancellationToken);
            if (validation != null) return Task.FromResult(OperationResult<bool>.Failed(validation));
            return backend.ExecuteAsync("storage", context, cancellationToken, (connection, token) =>
            {
                token.ThrowIfCancellationRequested();
                using var command = CreateCommand(connection, "DELETE FROM kv_entries WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND session_id=@session AND scope_kind=@scope AND namespace_id=@namespace AND key=@key;", null);
                AddKeyParameters(command, reference, key);
                command.ExecuteNonQuery();
                return OperationResult<bool>.Succeeded(true);
            });
        }

        private static void AddKeyParameters(SqliteCommand command, StorageNamespaceRef reference, string key)
        {
            AddParameter(command, "@owner", reference.OwnerId);
            AddParameter(command, "@campaign", reference.Session.CampaignGuid);
            AddParameter(command, "@timeline", reference.Session.TimelineId);
            AddParameter(command, "@session", reference.Scope == StorageScopeKind.Campaign ? string.Empty : reference.Session.SessionId);
            AddParameter(command, "@scope", reference.Scope.ToString());
            AddParameter(command, "@namespace", reference.NamespaceId);
            AddParameter(command, "@key", key);
        }
    }

}
