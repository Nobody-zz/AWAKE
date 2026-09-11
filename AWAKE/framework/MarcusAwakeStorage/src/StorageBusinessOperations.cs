using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Microsoft.Data.Sqlite;

namespace MarcusAwakeStorage;

public sealed partial class SqliteStorageAndRagBackend
{
    private const int BusinessMaximumIdBytes = 256;
    private const int BusinessMaximumValueBytes = 128 * 1024;
    private const int BusinessMaximumTimelinePayloadBytes = 128 * 1024;
    private const int BusinessMaximumDocumentBytes = 64 * 1024;
    private const int BusinessMaximumDocuments = 16;
    private const int BusinessMaximumQueryBytes = 8 * 1024;
    private const int BusinessMaximumTimelineRead = 128;
    private const int BusinessMaximumResponseBytes = 96 * 1024;

    public Task<OperationResult<StorageBusinessResult>> ExecuteBusinessAsync(
        StorageBusinessRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var validation = ValidateBusinessRequest(request, context, cancellationToken);
        if (validation != null) return Task.FromResult(OperationResult<StorageBusinessResult>.Failed(validation));

        switch (request.Operation)
        {
            case StorageBusinessOperation.KvGet:
                return ExecuteKvGetAsync(request, context, cancellationToken);
            case StorageBusinessOperation.KvSet:
                return ExecuteKvSetAsync(request, context, cancellationToken);
            case StorageBusinessOperation.KvDelete:
                return ExecuteKvDeleteAsync(request, context, cancellationToken);
            case StorageBusinessOperation.TimelineAppend:
                return ExecuteTimelineAppendAsync(request, context, cancellationToken);
            case StorageBusinessOperation.TimelineRead:
                return ExecuteTimelineReadAsync(request, context, cancellationToken);
            case StorageBusinessOperation.RagIngest:
                return ExecuteRagIngestAsync(request, context, cancellationToken);
            case StorageBusinessOperation.RagSearch:
                return ExecuteRagSearchAsync(request, context, cancellationToken);
            default:
                return Task.FromResult(Failure<StorageBusinessResult>(
                    "storage.operation_unsupported",
                    FrameworkErrorCategory.Unsupported,
                    "The storage operation is not supported.",
                    context));
        }
    }

    private FrameworkError ValidateBusinessRequest(
        StorageBusinessRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (request == null) return CreateError("storage.invalid_request", FrameworkErrorCategory.InvalidRequest, "A storage request is required.", context);
        if (context == null) return CreateError("storage.invalid_request", FrameworkErrorCategory.InvalidRequest, "A request context is required.", null);
        if (cancellationToken == CancellationToken.None) return CreateError("storage.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context);
        if (!Enum.IsDefined(typeof(StorageBusinessOperation), request.Operation)) return CreateError("storage.operation_unsupported", FrameworkErrorCategory.Unsupported, "The storage operation is not supported.", context);
        return ValidateBusinessFields(request, context);
    }

    private FrameworkError ValidateBusinessFields(StorageBusinessRequest request, RequestContext context)
    {
        if (request.Operation == StorageBusinessOperation.KvGet || request.Operation == StorageBusinessOperation.KvSet || request.Operation == StorageBusinessOperation.KvDelete)
        {
            if (request.Scope != StorageScopeKind.Campaign && request.Scope != StorageScopeKind.Session) return CreateError("storage.scope_invalid", FrameworkErrorCategory.InvalidRequest, "KV storage only supports campaign or session scope.", context);
            var error = ValidateBusinessId(request.NamespaceId, "storage.namespace_invalid", context) ?? ValidateBusinessId(request.Key, "storage.key_invalid", context);
            if (error != null) return error;
            if (request.Operation == StorageBusinessOperation.KvSet && ByteCount(request.Value) > BusinessMaximumValueBytes) return CreateError("storage.value_too_large", FrameworkErrorCategory.ResourceExhausted, "The storage value exceeds the business limit.", context);
        }

        if (request.Operation == StorageBusinessOperation.TimelineAppend)
        {
            var error = ValidateBusinessId(request.EventId, "storage.event_id_invalid", context) ?? ValidateBusinessId(request.EventType, "storage.event_type_invalid", context);
            if (error != null) return error;
            if (ByteCount(request.EventPayloadJson) > BusinessMaximumTimelinePayloadBytes) return CreateError("storage.timeline_payload_too_large", FrameworkErrorCategory.ResourceExhausted, "The timeline payload exceeds the business limit.", context);
            if (request.OccurredUnixMilliseconds < 1) return CreateError("storage.event_time_invalid", FrameworkErrorCategory.InvalidRequest, "A valid event timestamp is required.", context);
            if (ByteCount(request.EventCorrelationId) > BusinessMaximumIdBytes || ByteCount(request.EventCausationId) > BusinessMaximumIdBytes) return CreateError("storage.event_identity_too_large", FrameworkErrorCategory.ResourceExhausted, "The timeline event identity exceeds the business limit.", context);
        }

        if (request.Operation == StorageBusinessOperation.TimelineRead)
        {
            if (request.AfterSequence < 0) return CreateError("storage.cursor_invalid", FrameworkErrorCategory.InvalidRequest, "The timeline cursor cannot be negative.", context);
            if (request.MaximumResults < 1 || request.MaximumResults > BusinessMaximumTimelineRead) return CreateError("storage.read_limit_invalid", FrameworkErrorCategory.InvalidRequest, "The timeline read limit is outside the allowed range.", context);
        }

        if (request.Operation == StorageBusinessOperation.RagIngest || request.Operation == StorageBusinessOperation.RagSearch)
        {
            var error = ValidateBusinessId(request.CollectionId, "rag.collection_invalid", context) ?? ValidateBusinessId(request.CorpusFingerprint, "rag.corpus_fingerprint_invalid", context);
            if (error != null) return error;
        }

        return ValidateRagAndIdempotency(request, context);
    }

    private FrameworkError ValidateRagAndIdempotency(StorageBusinessRequest request, RequestContext context)
    {
        if (request.Operation == StorageBusinessOperation.RagIngest)
        {
            if (request.Documents == null || request.Documents.Count > BusinessMaximumDocuments) return CreateError("rag.batch_too_large", FrameworkErrorCategory.ResourceExhausted, "The RAG ingest batch exceeds the business limit.", context);
            foreach (var document in request.Documents)
            {
                if (document == null) return CreateError("rag.document_invalid", FrameworkErrorCategory.InvalidRequest, "The RAG ingest batch contains an invalid document.", context);
                if (ByteCount(document.DocumentId) > BusinessMaximumIdBytes || ByteCount(document.AccessScope) > BusinessMaximumIdBytes || ByteCount(document.SourceClass) > BusinessMaximumIdBytes) return CreateError("rag.document_identity_too_large", FrameworkErrorCategory.ResourceExhausted, "A RAG document identity exceeds the business limit.", context);
                if (ByteCount(document.Text) > BusinessMaximumDocumentBytes) return CreateError("rag.document_too_large", FrameworkErrorCategory.ResourceExhausted, "A RAG document exceeds the business limit.", context);
            }
        }

        if (request.Operation == StorageBusinessOperation.RagSearch)
        {
            if (ByteCount(request.Query) > BusinessMaximumQueryBytes) return CreateError("rag.query_too_large", FrameworkErrorCategory.ResourceExhausted, "The RAG query exceeds the business limit.", context);
            if (request.MaximumResults < 1 || request.MaximumResults > 64) return CreateError("rag.read_limit_invalid", FrameworkErrorCategory.InvalidRequest, "The RAG result limit is outside the allowed range.", context);
            if (request.AccessScopes == null || request.AccessScopes.Count > BusinessMaximumDocuments || request.AccessScopes.Any(scope => ByteCount(scope) > BusinessMaximumIdBytes)) return CreateError("rag.scope_invalid", FrameworkErrorCategory.ResourceExhausted, "The RAG access scope list exceeds the business limit.", context);
        }

        if (IsMutation(request.Operation))
        {
            var error = ValidateBusinessId(request.IdempotencyKey, "storage.idempotency_key_invalid", context);
            if (error != null) return error;
            if (!IsSha256(request.PayloadSha256)) return CreateError("storage.payload_hash_invalid", FrameworkErrorCategory.InvalidRequest, "A valid request payload hash is required.", context);
        }

        return null;
    }

    private FrameworkError ValidateBusinessId(string value, string code, RequestContext context)
    {
        if (string.IsNullOrWhiteSpace(value)) return CreateError(code, FrameworkErrorCategory.InvalidRequest, "A required storage identifier is missing.", context);
        if (ByteCount(value) > BusinessMaximumIdBytes) return CreateError(code, FrameworkErrorCategory.ResourceExhausted, "A storage identifier exceeds the business limit.", context);
        return null;
    }

    private static bool IsMutation(StorageBusinessOperation operation)
    {
        return operation == StorageBusinessOperation.KvSet
            || operation == StorageBusinessOperation.KvDelete
            || operation == StorageBusinessOperation.TimelineAppend
            || operation == StorageBusinessOperation.RagIngest;
    }

    private static bool IsSha256(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') || (character >= 'A' && character <= 'F'))) return false;
        }
        return true;
    }

    private static string ResponseSchema(StorageBusinessOperation operation)
    {
        switch (operation)
        {
            case StorageBusinessOperation.KvGet: return "marcus-awake.storage.kv_get.v1";
            case StorageBusinessOperation.KvSet: return "marcus-awake.storage.kv_set.v1";
            case StorageBusinessOperation.KvDelete: return "marcus-awake.storage.kv_delete.v1";
            case StorageBusinessOperation.TimelineAppend: return "marcus-awake.storage.timeline_append.v1";
            case StorageBusinessOperation.TimelineRead: return "marcus-awake.storage.timeline_read.v1";
            case StorageBusinessOperation.RagIngest: return "marcus-awake.rag.ingest.v1";
            case StorageBusinessOperation.RagSearch: return "marcus-awake.rag.search.v1";
            default: return "marcus-awake.storage.error.v1";
        }
    }

    private Task<OperationResult<StorageBusinessResult>> ExecuteKvGetAsync(StorageBusinessRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        return ExecuteAsync("storage", context, cancellationToken, (connection, token) =>
        {
            token.ThrowIfCancellationRequested();
            using var command = CreateCommand(connection, "SELECT value_json FROM kv_entries WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND session_id=@session AND scope_kind=@scope AND namespace_id=@namespace AND key=@key;", null);
            AddKeyParameters(command, request, context);
            var value = command.ExecuteScalar();
            var found = value != null && value != DBNull.Value;
            var payload = SerializePayload(new Dictionary<string, object>
            {
                ["scope"] = request.Scope.ToString(),
                ["namespace_id"] = request.NamespaceId,
                ["key"] = request.Key,
                ["found"] = found,
                ["value"] = found ? Convert.ToString(value) : null
            });
            return NonDurableResult(request, payload, 0, BuildKvResourceKey(request, context), context);
        });
    }

    private Task<OperationResult<StorageBusinessResult>> ExecuteTimelineReadAsync(StorageBusinessRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        return ExecuteAsync("storage", context, cancellationToken, (connection, token) =>
        {
            var records = ReadTimeline(connection, request.AfterSequence, request.MaximumResults, context, token);
            if (!records.IsSuccess) return Failure<StorageBusinessResult>(records.Error.Code, records.Error.Category, records.Error.SafeFallback, context, records.Error.Retryable);
            var items = records.Value.Select(record => new Dictionary<string, object>
            {
                ["sequence"] = record.Sequence,
                ["event_id"] = record.EventId,
                ["event_type"] = record.EventType,
                ["payload_json"] = record.PayloadJson,
                ["occurred_unix_ms"] = record.OccurredAt.ToUnixTimeMilliseconds(),
                ["session_id"] = record.SessionId,
                ["correlation_id"] = record.CorrelationId,
                ["causation_id"] = record.CausationId
            }).ToArray();
            var payload = SerializePayload(new Dictionary<string, object>
            {
                ["after_sequence"] = request.AfterSequence,
                ["records"] = items
            });
            return NonDurableResult(request, payload, 0, string.Empty, context);
        });
    }

    private Task<OperationResult<StorageBusinessResult>> ExecuteRagSearchAsync(StorageBusinessRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        return ExecuteAsync("rag", context, cancellationToken, (connection, token) =>
        {
            var searchRequest = new RagSearchRequest(
                request.CollectionId,
                request.CorpusFingerprint,
                request.Query,
                request.AccessScopes,
                request.MaximumResults,
                RetrievalMode.Keyword,
                context.Caller.Value,
                context.Session.CampaignGuid,
                context.Session.TimelineId,
                context.Session.SessionId,
                string.Empty,
                Array.Empty<string>());
            var result = Search(connection, searchRequest, context, token);
            if (!result.IsSuccess) return Failure<StorageBusinessResult>(result.Error.Code, result.Error.Category, result.Error.SafeFallback, context, result.Error.Retryable);
            var hits = result.Value.Select(hit => new Dictionary<string, object>
            {
                ["document_id"] = hit.DocumentId,
                ["text"] = hit.Text,
                ["source_locator"] = hit.SourceLocator,
                ["rank"] = hit.Rank,
                ["corpus_fingerprint"] = hit.CorpusFingerprint
            }).ToArray();
            var payload = SerializePayload(new Dictionary<string, object>
            {
                ["collection_id"] = request.CollectionId,
                ["corpus_fingerprint"] = request.CorpusFingerprint,
                ["query"] = request.Query,
                ["hits"] = hits
            });
            return NonDurableResult(request, payload, 0, request.CollectionId, context);
        });
    }

    private OperationResult<StorageBusinessResult> NonDurableResult(StorageBusinessRequest request, string payloadJson, long eventIndex, string resourceKey, RequestContext context)
    {
        var responseError = ValidateResponsePayload(payloadJson, context);
        if (responseError != null) return OperationResult<StorageBusinessResult>.Failed(responseError);
        return OperationResult<StorageBusinessResult>.Succeeded(new StorageBusinessResult(OperationName(request.Operation), payloadJson, false, "accepted", eventIndex, resourceKey));
    }

    private static string SerializePayload(object value)
    {
        return JsonSerializer.Serialize(value);
    }

    private FrameworkError ValidateResponsePayload(string payloadJson, RequestContext context)
    {
        if (ByteCount(payloadJson) > BusinessMaximumResponseBytes) return CreateError("storage.response_too_large", FrameworkErrorCategory.ResourceExhausted, "The storage response exceeds the business limit.", context);
        return null;
    }

    private static void AddKeyParameters(SqliteCommand command, StorageBusinessRequest request, RequestContext context)
    {
        AddParameter(command, "@owner", context.Caller.Value);
        AddParameter(command, "@campaign", context.Session.CampaignGuid);
        AddParameter(command, "@timeline", context.Session.TimelineId);
        AddParameter(command, "@session", request.Scope == StorageScopeKind.Campaign ? string.Empty : context.Session.SessionId);
        AddParameter(command, "@scope", request.Scope.ToString());
        AddParameter(command, "@namespace", request.NamespaceId);
        AddParameter(command, "@key", request.Key);
    }

    private static void AddTimelineScopeParameters(SqliteCommand command, RequestContext context)
    {
        AddParameter(command, "@owner", context.Caller.Value);
        AddParameter(command, "@campaign", context.Session.CampaignGuid);
        AddParameter(command, "@timeline", context.Session.TimelineId);
    }

    private static void AddTimelineIdentityParameters(SqliteCommand command, StorageBusinessRequest request, RequestContext context)
    {
        AddTimelineScopeParameters(command, context);
        AddParameter(command, "@event_id", request.EventId);
    }

    private static string BuildKvResourceKey(StorageBusinessRequest request, RequestContext context)
    {
        return JsonSerializer.Serialize(new object[]
        {
            request.Scope.ToString(),
            request.NamespaceId,
            request.Key,
            request.Scope == StorageScopeKind.Campaign ? string.Empty : context.Session.SessionId
        });
    }

    private static string ResourceKey(StorageBusinessRequest request, RequestContext context)
    {
        switch (request.Operation)
        {
            case StorageBusinessOperation.KvSet:
            case StorageBusinessOperation.KvDelete:
                return BuildKvResourceKey(request, context);
            case StorageBusinessOperation.TimelineAppend:
                return request.EventId;
            case StorageBusinessOperation.RagIngest:
                return request.CollectionId;
            default:
                return string.Empty;
        }
    }

    private static string OperationName(StorageBusinessOperation operation)
    {
        switch (operation)
        {
            case StorageBusinessOperation.KvGet: return "storage.kv_get";
            case StorageBusinessOperation.KvSet: return "storage.kv_set";
            case StorageBusinessOperation.KvDelete: return "storage.kv_delete";
            case StorageBusinessOperation.TimelineAppend: return "storage.timeline_append";
            case StorageBusinessOperation.TimelineRead: return "storage.timeline_read";
            case StorageBusinessOperation.RagIngest: return "rag.ingest";
            case StorageBusinessOperation.RagSearch: return "rag.search";
            default: return "storage.unknown";
        }
    }

    private Task<OperationResult<StorageBusinessResult>> ExecuteKvSetAsync(StorageBusinessRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        return ExecuteMutationWithReceiptAsync(request, context, cancellationToken, (connection, transaction, token) =>
        {
            token.ThrowIfCancellationRequested();
            using var command = CreateCommand(connection, "INSERT INTO kv_entries(owner_id,campaign_guid,timeline_id,session_id,scope_kind,namespace_id,key,value_json,updated_unix_ms) VALUES(@owner,@campaign,@timeline,@session,@scope,@namespace,@key,@value,@updated) ON CONFLICT(owner_id,campaign_guid,timeline_id,session_id,scope_kind,namespace_id,key) DO UPDATE SET value_json=excluded.value_json,updated_unix_ms=excluded.updated_unix_ms;", transaction);
            AddKeyParameters(command, request, context);
            AddParameter(command, "@value", request.Value ?? string.Empty);
            AddParameter(command, "@updated", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            command.ExecuteNonQuery();
            return MutationSuccess(request, new Dictionary<string, object>
            {
                ["scope"] = request.Scope.ToString(),
                ["namespace_id"] = request.NamespaceId,
                ["key"] = request.Key,
                ["changed"] = true
            }, 0, BuildKvResourceKey(request, context), context);
        });
    }

    private Task<OperationResult<StorageBusinessResult>> ExecuteKvDeleteAsync(StorageBusinessRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        return ExecuteMutationWithReceiptAsync(request, context, cancellationToken, (connection, transaction, token) =>
        {
            token.ThrowIfCancellationRequested();
            using var command = CreateCommand(connection, "DELETE FROM kv_entries WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND session_id=@session AND scope_kind=@scope AND namespace_id=@namespace AND key=@key;", transaction);
            AddKeyParameters(command, request, context);
            var changed = command.ExecuteNonQuery() > 0;
            return MutationSuccess(request, new Dictionary<string, object>
            {
                ["scope"] = request.Scope.ToString(),
                ["namespace_id"] = request.NamespaceId,
                ["key"] = request.Key,
                ["changed"] = changed
            }, 0, BuildKvResourceKey(request, context), context);
        });
    }

    private Task<OperationResult<StorageBusinessResult>> ExecuteTimelineAppendAsync(StorageBusinessRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        return ExecuteMutationWithReceiptAsync(request, context, cancellationToken, (connection, transaction, token) =>
        {
            token.ThrowIfCancellationRequested();
            using (var existingCommand = CreateCommand(connection, "SELECT sequence,event_type,payload_json,occurred_unix_ms,correlation_id,causation_id FROM timeline_ledger WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND event_id=@event_id;", transaction))
            {
                AddTimelineIdentityParameters(existingCommand, request, context);
                using var reader = existingCommand.ExecuteReader();
                if (reader.Read())
                {
                    var same = StringComparer.Ordinal.Equals(request.EventType, reader.GetString(1))
                        && StringComparer.Ordinal.Equals(request.EventPayloadJson ?? string.Empty, reader.GetString(2))
                        && request.OccurredUnixMilliseconds == reader.GetInt64(3)
                        && StringComparer.Ordinal.Equals(request.EventCorrelationId ?? string.Empty, reader.GetString(4))
                        && StringComparer.Ordinal.Equals(request.EventCausationId ?? string.Empty, reader.GetString(5));
                    if (!same) return Failure<StorageBusinessResult>("storage.ledger_event_conflict", FrameworkErrorCategory.Conflict, "The timeline event ID already contains different data.", context);
                    var sequence = reader.GetInt64(0);
                    return MutationSuccess(request, new Dictionary<string, object>
                    {
                        ["event_id"] = request.EventId,
                        ["sequence"] = sequence,
                        ["duplicate"] = true
                    }, sequence, request.EventId, context);
                }
            }

            token.ThrowIfCancellationRequested();
            long sequenceNumber;
            using (var sequenceCommand = CreateCommand(connection, "SELECT COALESCE(MAX(sequence),0)+1 FROM timeline_ledger WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline;", transaction))
            {
                AddTimelineScopeParameters(sequenceCommand, context);
                sequenceNumber = Convert.ToInt64(sequenceCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            using (var insertCommand = CreateCommand(connection, "INSERT INTO timeline_ledger(owner_id,campaign_guid,timeline_id,sequence,event_id,event_type,payload_json,occurred_unix_ms,session_id,correlation_id,causation_id) VALUES(@owner,@campaign,@timeline,@sequence,@event_id,@event_type,@payload,@occurred,@session,@correlation,@causation);", transaction))
            {
                AddTimelineScopeParameters(insertCommand, context);
                AddParameter(insertCommand, "@sequence", sequenceNumber);
                AddParameter(insertCommand, "@event_id", request.EventId);
                AddParameter(insertCommand, "@event_type", request.EventType);
                AddParameter(insertCommand, "@payload", request.EventPayloadJson ?? string.Empty);
                AddParameter(insertCommand, "@occurred", request.OccurredUnixMilliseconds);
                AddParameter(insertCommand, "@session", context.Session.SessionId);
                AddParameter(insertCommand, "@correlation", request.EventCorrelationId ?? string.Empty);
                AddParameter(insertCommand, "@causation", request.EventCausationId ?? string.Empty);
                insertCommand.ExecuteNonQuery();
            }

            return MutationSuccess(request, new Dictionary<string, object>
            {
                ["event_id"] = request.EventId,
                ["sequence"] = sequenceNumber,
                ["duplicate"] = false
            }, sequenceNumber, request.EventId, context);
        });
    }

    private Task<OperationResult<StorageBusinessResult>> ExecuteRagIngestAsync(StorageBusinessRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        return ExecuteMutationWithReceiptAsync(request, context, cancellationToken, (connection, transaction, token) =>
        {
            token.ThrowIfCancellationRequested();
            var storedFingerprint = GetCollectionFingerprint(connection, request.CollectionId, context, transaction);
            if (storedFingerprint != null && !StringComparer.Ordinal.Equals(storedFingerprint, request.CorpusFingerprint)) return Failure<StorageBusinessResult>("rag.corpus_fingerprint_conflict", FrameworkErrorCategory.Conflict, "The collection already contains a different corpus fingerprint.", context);

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
                token.ThrowIfCancellationRequested();
                var document = request.Documents[index];
                UpsertDocument(connection, transaction, ToRagIngestRequest(request, context), context, document);
                RefreshFtsDocument(connection, transaction, request.CollectionId, context, document);
            }

            return MutationSuccess(request, new Dictionary<string, object>
            {
                ["collection_id"] = request.CollectionId,
                ["corpus_fingerprint"] = request.CorpusFingerprint,
                ["ingested"] = request.Documents.Count
            }, 0, request.CollectionId, context);
        });
    }

    private Task<OperationResult<StorageBusinessResult>> ExecuteMutationWithReceiptAsync(
        StorageBusinessRequest request,
        RequestContext context,
        CancellationToken cancellationToken,
        Func<SqliteConnection, SqliteTransaction, CancellationToken, OperationResult<StorageBusinessResult>> mutation)
    {
        return ExecuteAsync("storage", context, cancellationToken, (connection, token) =>
        {
            using var transaction = connection.BeginTransaction();
            var operationName = OperationName(request.Operation);
            var resourceKey = ResourceKey(request, context);
            var existing = ReadReceipt(connection, transaction, request, context, operationName);
            if (existing != null)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(existing.PayloadSha256, request.PayloadSha256) || !StringComparer.Ordinal.Equals(existing.ResourceKey, resourceKey))
                {
                    transaction.Rollback();
                    return Failure<StorageBusinessResult>("storage.idempotency_conflict", FrameworkErrorCategory.Conflict, "The idempotency key was already used with a different request.", context);
                }

                transaction.Commit();
                return OperationResult<StorageBusinessResult>.Succeeded(new StorageBusinessResult(operationName, existing.ResponseJson, true, "terminal_replay", existing.EventIndex, existing.ResourceKey));
            }

            var mutationResult = mutation(connection, transaction, token);
            if (!mutationResult.IsSuccess)
            {
                transaction.Rollback();
                return mutationResult;
            }

            var responseError = ValidateResponsePayload(mutationResult.Value.PayloadJson, context);
            if (responseError != null)
            {
                transaction.Rollback();
                return OperationResult<StorageBusinessResult>.Failed(responseError);
            }

            InsertReceipt(connection, transaction, request, context, operationName, resourceKey, mutationResult.Value);
            token.ThrowIfCancellationRequested();
            transaction.Commit();
            return OperationResult<StorageBusinessResult>.Succeeded(new StorageBusinessResult(operationName, mutationResult.Value.PayloadJson, true, "accepted", mutationResult.Value.EventIndex, resourceKey));
        });
    }

    private OperationResult<StorageBusinessResult> MutationSuccess(StorageBusinessRequest request, object payload, long eventIndex, string resourceKey, RequestContext context)
    {
        return OperationResult<StorageBusinessResult>.Succeeded(new StorageBusinessResult(OperationName(request.Operation), SerializePayload(payload), true, "accepted", eventIndex, resourceKey));
    }

    private static RagIngestRequest ToRagIngestRequest(StorageBusinessRequest request, RequestContext context)
    {
        return new RagIngestRequest(request.CollectionId, request.CorpusFingerprint, request.Documents);
    }

    private ReceiptRecord ReadReceipt(SqliteConnection connection, SqliteTransaction transaction, StorageBusinessRequest request, RequestContext context, string operation)
    {
        using var command = CreateCommand(connection, "SELECT payload_sha256,resource_key,response_schema,response_json,outcome,event_index FROM business_receipts WHERE owner_id=@owner AND campaign_guid=@campaign AND timeline_id=@timeline AND effective_session_id=@session AND operation=@operation AND idempotency_key=@idempotency;", transaction);
        AddReceiptIdentityParameters(command, request, context, operation);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new ReceiptRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5));
    }

    private void InsertReceipt(SqliteConnection connection, SqliteTransaction transaction, StorageBusinessRequest request, RequestContext context, string operation, string resourceKey, StorageBusinessResult result)
    {
        using var command = CreateCommand(connection, "INSERT INTO business_receipts(owner_id,campaign_guid,timeline_id,effective_session_id,operation,idempotency_key,payload_sha256,resource_key,response_schema,response_json,outcome,event_index,created_unix_ms) VALUES(@owner,@campaign,@timeline,@session,@operation,@idempotency,@payload_hash,@resource_key,@response_schema,@response_json,@outcome,@event_index,@created);", transaction);
        AddReceiptIdentityParameters(command, request, context, operation);
        AddParameter(command, "@payload_hash", request.PayloadSha256);
        AddParameter(command, "@resource_key", resourceKey);
        AddParameter(command, "@response_schema", ResponseSchema(request.Operation));
        AddParameter(command, "@response_json", result.PayloadJson);
        AddParameter(command, "@outcome", result.Outcome);
        AddParameter(command, "@event_index", result.EventIndex);
        AddParameter(command, "@created", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    private static void AddReceiptIdentityParameters(SqliteCommand command, StorageBusinessRequest request, RequestContext context, string operation)
    {
        AddParameter(command, "@owner", context.Caller.Value);
        AddParameter(command, "@campaign", context.Session.CampaignGuid);
        AddParameter(command, "@timeline", context.Session.TimelineId);
        AddParameter(command, "@session", ReceiptSessionId(request, context));
        AddParameter(command, "@operation", operation);
        AddParameter(command, "@idempotency", request.IdempotencyKey);
    }

    private static string ReceiptSessionId(StorageBusinessRequest request, RequestContext context)
    {
        return request.Operation == StorageBusinessOperation.KvSet
            || request.Operation == StorageBusinessOperation.KvDelete
                ? request.Scope == StorageScopeKind.Session ? context.Session.SessionId : string.Empty
                : string.Empty;
    }

    private sealed class ReceiptRecord
    {
        internal ReceiptRecord(string payloadSha256, string resourceKey, string responseSchema, string responseJson, string outcome, long eventIndex)
        {
            PayloadSha256 = payloadSha256;
            ResourceKey = resourceKey;
            ResponseSchema = responseSchema;
            ResponseJson = responseJson;
            Outcome = outcome;
            EventIndex = eventIndex;
        }

        internal string PayloadSha256 { get; }
        internal string ResourceKey { get; }
        internal string ResponseSchema { get; }
        internal string ResponseJson { get; }
        internal string Outcome { get; }
        internal long EventIndex { get; }
    }
}
