using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using MarcusAwakeFramework.Api;
using MarcusAwakeStorage;
using MarcusAwakeTransport;

namespace MarcusAwakeRuntimeService;

internal static class StorageBusinessFrameAdapter
{
    private const int MaximumPayloadBytes = 96 * 1024;
    private const int MaximumJsonDepth = 12;
    private const int MaximumObjectProperties = 32;
    private const int MaximumArrayItems = 16;

    internal static bool TryParse(PipeEnvelope envelope, out StorageBusinessRequest? request, out string error)
    {
        request = null;
        error = string.Empty;
        if (envelope == null)
        {
            error = "business_envelope_missing";
            return false;
        }

        if (!TryMapOperation(envelope.MessageType, out var operation, out var expectedSchema))
        {
            error = "business_message_unsupported";
            return false;
        }

        if (!StringComparer.Ordinal.Equals(envelope.PayloadSchema, expectedSchema))
        {
            error = "business_schema_mismatch";
            return false;
        }

        var payloadBytes = Encoding.UTF8.GetByteCount(envelope.PayloadJson ?? string.Empty);
        if (payloadBytes > MaximumPayloadBytes)
        {
            error = "business_payload_too_large";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(envelope.PayloadJson ?? string.Empty, new JsonDocumentOptions
            {
                MaxDepth = MaximumJsonDepth,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "business_payload_object_required";
                return false;
            }

            if (!ValidateShape(document.RootElement, 0, out error)) return false;
            if (!TryBuildRequest(operation, document.RootElement, envelope, out request, out error)) return false;
            return true;
        }
        catch (JsonException)
        {
            error = "business_payload_json_invalid";
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            request = null;
            error = "business_payload_value_invalid";
            return false;
        }
    }

    private static bool TryMapOperation(string messageType, out StorageBusinessOperation operation, out string expectedSchema)
    {
        operation = default(StorageBusinessOperation);
        expectedSchema = string.Empty;
        switch (messageType)
        {
            case ProtocolConstants.MessageTypeStorageKvGet:
                operation = StorageBusinessOperation.KvGet;
                expectedSchema = "marcus-awake.storage.kv_get.v1";
                return true;
            case ProtocolConstants.MessageTypeStorageKvSet:
                operation = StorageBusinessOperation.KvSet;
                expectedSchema = "marcus-awake.storage.kv_set.v1";
                return true;
            case ProtocolConstants.MessageTypeStorageKvDelete:
                operation = StorageBusinessOperation.KvDelete;
                expectedSchema = "marcus-awake.storage.kv_delete.v1";
                return true;
            case ProtocolConstants.MessageTypeStorageTimelineAppend:
                operation = StorageBusinessOperation.TimelineAppend;
                expectedSchema = "marcus-awake.storage.timeline_append.v1";
                return true;
            case ProtocolConstants.MessageTypeStorageTimelineRead:
                operation = StorageBusinessOperation.TimelineRead;
                expectedSchema = "marcus-awake.storage.timeline_read.v1";
                return true;
            case ProtocolConstants.MessageTypeRagIngest:
                operation = StorageBusinessOperation.RagIngest;
                expectedSchema = "marcus-awake.rag.ingest.v1";
                return true;
            case ProtocolConstants.MessageTypeRagSearch:
                operation = StorageBusinessOperation.RagSearch;
                expectedSchema = "marcus-awake.rag.search.v1";
                return true;
            default:
                return false;
        }
    }

    private static bool ValidateShape(JsonElement element, int depth, out string error)
    {
        error = string.Empty;
        if (depth > MaximumJsonDepth)
        {
            error = "business_payload_depth_exceeded";
            return false;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject().ToArray();
            if (properties.Length > MaximumObjectProperties)
            {
                error = "business_payload_property_limit";
                return false;
            }
            for (var index = 0; index < properties.Length; index++)
            {
                if (!ValidateShape(properties[index].Value, depth + 1, out error)) return false;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var items = element.EnumerateArray().ToArray();
            if (items.Length > MaximumArrayItems)
            {
                error = "business_payload_array_limit";
                return false;
            }
            for (var index = 0; index < items.Length; index++)
            {
                if (!ValidateShape(items[index], depth + 1, out error)) return false;
            }
        }
        return true;
    }

    private static bool TryBuildRequest(StorageBusinessOperation operation, JsonElement root, PipeEnvelope envelope, out StorageBusinessRequest? request, out string error)
    {
        request = new StorageBusinessRequest
        {
            Operation = operation,
            PayloadSha256 = envelope.PayloadSha256,
            IdempotencyKey = envelope.TaskScope?.IdempotencyKey ?? string.Empty
        };
        error = string.Empty;

        switch (operation)
        {
            case StorageBusinessOperation.KvGet:
            case StorageBusinessOperation.KvSet:
            case StorageBusinessOperation.KvDelete:
                if (!HasOnly(root, operation == StorageBusinessOperation.KvSet ? new[] { "scope", "namespace_id", "key", "value" } : new[] { "scope", "namespace_id", "key" }, out error)) return false;
                if (!TryReadScope(root, "scope", out var scope, out error)) return false;
                if (!TryReadRequiredString(root, "namespace_id", out var namespaceId, out error)) return false;
                if (!TryReadRequiredString(root, "key", out var key, out error)) return false;
                request.Scope = scope;
                request.NamespaceId = namespaceId;
                request.Key = key;
                if (operation == StorageBusinessOperation.KvSet)
                {
                    if (!TryReadRequiredString(root, "value", out var value, out error)) return false;
                    request.Value = value;
                }
                return true;
            case StorageBusinessOperation.TimelineAppend:
                if (!HasOnly(root, new[] { "event_id", "event_type", "payload_json", "occurred_unix_ms", "correlation_id", "causation_id" }, out error)) return false;
                if (!TryReadRequiredString(root, "event_id", out var eventId, out error)) return false;
                if (!TryReadRequiredString(root, "event_type", out var eventType, out error)) return false;
                if (!TryReadRequiredString(root, "payload_json", out var eventPayloadJson, out error)) return false;
                if (!TryReadRequiredInt64(root, "occurred_unix_ms", out var occurredUnixMilliseconds, out error) || occurredUnixMilliseconds < 1) { error = "business_event_time_invalid"; return false; }
                if (!TryReadOptionalString(root, "correlation_id", out var eventCorrelationId, out error)) return false;
                if (!TryReadOptionalString(root, "causation_id", out var eventCausationId, out error)) return false;
                request.EventId = eventId;
                request.EventType = eventType;
                request.EventPayloadJson = eventPayloadJson;
                request.OccurredUnixMilliseconds = occurredUnixMilliseconds;
                request.EventCorrelationId = eventCorrelationId;
                request.EventCausationId = eventCausationId;
                return true;
            case StorageBusinessOperation.TimelineRead:
                if (!HasOnly(root, new[] { "after_sequence", "maximum_results" }, out error)) return false;
                if (!TryReadRequiredInt64(root, "after_sequence", out var afterSequence, out error) || afterSequence < 0) { error = "business_cursor_invalid"; return false; }
                if (!TryReadRequiredInt32(root, "maximum_results", out var maximumTimelineResults, out error) || maximumTimelineResults < 1 || maximumTimelineResults > 128) { error = "business_read_limit_invalid"; return false; }
                request.AfterSequence = afterSequence;
                request.MaximumResults = maximumTimelineResults;
                return true;
            case StorageBusinessOperation.RagIngest:
                if (!HasOnly(root, new[] { "collection_id", "corpus_fingerprint", "documents" }, out error)) return false;
                if (!TryReadRequiredString(root, "collection_id", out var ingestCollectionId, out error)) return false;
                if (!TryReadRequiredString(root, "corpus_fingerprint", out var ingestCorpusFingerprint, out error)) return false;
                if (!TryReadDocuments(root, out var ingestDocuments, out error)) return false;
                request.CollectionId = ingestCollectionId;
                request.CorpusFingerprint = ingestCorpusFingerprint;
                request.Documents = ingestDocuments;
                return true;
            case StorageBusinessOperation.RagSearch:
                if (!HasOnly(root, new[] { "collection_id", "corpus_fingerprint", "query", "access_scopes", "maximum_results", "mode" }, out error)) return false;
                if (!TryReadRequiredString(root, "collection_id", out var searchCollectionId, out error)) return false;
                if (!TryReadRequiredString(root, "corpus_fingerprint", out var searchCorpusFingerprint, out error)) return false;
                if (!TryReadRequiredString(root, "query", out var query, out error)) return false;
                if (!TryReadStringArray(root, "access_scopes", out var accessScopes, out error)) return false;
                if (!TryReadRequiredInt32(root, "maximum_results", out var maximumSearchResults, out error) || maximumSearchResults < 1 || maximumSearchResults > 64) { error = "business_read_limit_invalid"; return false; }
                // Absent mode means an older client that only ever asked for keyword retrieval.
                if (!TryReadOptionalString(root, "mode", out var searchModeToken, out error)) return false;
                var searchMode = RetrievalMode.Keyword;
                if (!string.IsNullOrEmpty(searchModeToken) && !TryParseRetrievalMode(searchModeToken, out searchMode))
                {
                    error = "business_retrieval_mode_invalid";
                    return false;
                }
                if (searchMode == RetrievalMode.Hybrid)
                {
                    // The backend implements keyword and semantic; answering Hybrid as either one
                    // would be a silent downgrade, so it stays refused here too.
                    error = "business_retrieval_mode_unsupported";
                    return false;
                }
                request.CollectionId = searchCollectionId;
                request.CorpusFingerprint = searchCorpusFingerprint;
                request.Query = query;
                request.AccessScopes = accessScopes;
                request.MaximumResults = maximumSearchResults;
                if (!string.IsNullOrEmpty(searchModeToken)) request.Mode = searchMode;
                return true;
            default:
                error = "business_operation_unsupported";
                return false;
        }
    }

    private static bool TryParseRetrievalMode(string token, out RetrievalMode mode)
    {
        // Kept local: the framework's wire helper is internal to its assembly. The tokens here must
        // stay in step with RagRuntimeWire.ModeToken — that pairing is asserted by the RAG probe.
        switch (token)
        {
            case "keyword": mode = RetrievalMode.Keyword; return true;
            case "semantic": mode = RetrievalMode.Semantic; return true;
            case "hybrid": mode = RetrievalMode.Hybrid; return true;
            default: mode = RetrievalMode.Keyword; return false;
        }
    }

    private static bool HasOnly(JsonElement root, IReadOnlyCollection<string> allowed, out string error)
    {
        error = string.Empty;
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!allowedSet.Contains(property.Name))
            {
                error = "business_unknown_field:" + property.Name;
                return false;
            }
        }
        return true;
    }

    private static bool TryReadScope(JsonElement root, string name, out StorageScopeKind scope, out string error)
    {
        scope = StorageScopeKind.Session;
        if (!TryReadRequiredString(root, name, out var value, out error)) return false;
        if (StringComparer.OrdinalIgnoreCase.Equals(value, "campaign")) { scope = StorageScopeKind.Campaign; return true; }
        if (StringComparer.OrdinalIgnoreCase.Equals(value, "session")) { scope = StorageScopeKind.Session; return true; }
        error = "business_scope_invalid";
        return false;
    }

    private static bool TryReadRequiredString(JsonElement root, string name, out string value, out string error)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            error = "business_field_required:" + name;
            return false;
        }
        value = property.GetString() ?? string.Empty;
        if (value.Length == 0)
        {
            error = "business_field_empty:" + name;
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool TryReadOptionalString(JsonElement root, string name, out string value, out string error)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var property)) { error = string.Empty; return true; }
        if (property.ValueKind != JsonValueKind.String) { error = "business_field_invalid:" + name; return false; }
        value = property.GetString() ?? string.Empty;
        error = string.Empty;
        return true;
    }

    private static bool TryReadRequiredInt64(JsonElement root, string name, out long value, out string error)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out value))
        {
            error = "business_integer_required:" + name;
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool TryReadRequiredInt32(JsonElement root, string name, out int value, out string error)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out value))
        {
            error = "business_integer_required:" + name;
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool TryReadStringArray(JsonElement root, string name, out IReadOnlyList<string> values, out string error)
    {
        values = Array.Empty<string>();
        if (!root.TryGetProperty(name, out var property))
        {
            error = string.Empty;
            return true;
        }
        if (property.ValueKind != JsonValueKind.Array)
        {
            error = "business_array_required:" + name;
            return false;
        }
        var result = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                error = "business_array_item_invalid:" + name;
                return false;
            }
            result.Add(item.GetString() ?? string.Empty);
        }
        values = result.AsReadOnly();
        error = string.Empty;
        return true;
    }

    private static bool TryReadDocuments(JsonElement root, out IReadOnlyList<RagDocument> documents, out string error)
    {
        documents = Array.Empty<RagDocument>();
        error = string.Empty;
        if (!root.TryGetProperty("documents", out var property) || property.ValueKind != JsonValueKind.Array)
        {
            error = "business_array_required:documents";
            return false;
        }

        var result = new List<RagDocument>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !HasOnly(item, new[] { "document_id", "text", "source_locator", "access_scope", "source_class", "corpus_locator", "observed_unix_ms" }, out error)) return false;
            if (!TryReadRequiredString(item, "document_id", out var documentId, out error)) return false;
            if (!TryReadRequiredString(item, "text", out var text, out error)) return false;
            if (!TryReadRequiredString(item, "source_locator", out var sourceLocator, out error)) return false;
            if (!TryReadRequiredString(item, "access_scope", out var accessScope, out error)) return false;
            if (!TryReadRequiredString(item, "source_class", out var sourceClass, out error)) return false;
            if (!TryReadRequiredString(item, "corpus_locator", out var corpusLocator, out error)) return false;
            if (!TryReadRequiredInt64(item, "observed_unix_ms", out var observedUnixMilliseconds, out error) || observedUnixMilliseconds < 1)
            {
                error = "business_observed_time_invalid";
                return false;
            }
            result.Add(new RagDocument(documentId, text, sourceLocator, accessScope, sourceClass, corpusLocator, DateTimeOffset.FromUnixTimeMilliseconds(observedUnixMilliseconds)));
        }
        documents = result.AsReadOnly();
        error = string.Empty;
        return true;
    }
}
