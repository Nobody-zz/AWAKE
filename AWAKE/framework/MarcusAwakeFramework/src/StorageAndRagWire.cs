using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;

namespace MarcusAwakeFramework.Api
{
    /// <summary>
    /// Wire helpers for the storage/RAG business messages. Field names and limits mirror the
    /// service-side <c>StorageBusinessFrameAdapter</c> and <c>StorageBusinessOperations</c>
    /// verbatim; a drift here shows up as <c>business_unknown_field</c> /
    /// <c>business_field_empty</c> rejections on the service side, so keep both sides in step.
    /// </summary>
    internal static class RagRuntimeWire
    {
        internal const string IngestSchema = "marcus-awake.rag.ingest.v1";
        internal const string SearchSchema = "marcus-awake.rag.search.v1";
        internal const string ResultSchema = "marcus-awake.rag.result.v1";

        // RAG tasks carry no Provider identity. The protocol still requires RouteId/ProviderId/
        // ProfileId to be non-empty for a complete task scope, so these are the stable placeholders.
        internal const string RouteId = "runtime.rag";
        internal const string ProviderId = "runtime.storage";
        internal const string ProfileId = "runtime.storage";

        internal const int MaximumCollectionIdBytes = 256;
        internal const int MaximumDocuments = 16;
        internal const int MaximumDocumentBytes = 64 * 1024;
        internal const int MaximumQueryBytes = 8 * 1024;
        internal const int MaximumAccessScopes = 16;
        internal const int MaximumAccessScopeBytes = 256;

        internal static FrameworkError ValidateIngest(RagIngestRequest request, string correlationId)
        {
            if (request == null) return Invalid("rag.invalid_request", "A RAG ingest request is required.", correlationId);
            var identity = ValidateCollectionIdentity(request.CollectionId, request.CorpusFingerprint, correlationId);
            if (identity != null) return identity;
            if (request.Documents == null) return Invalid("rag.invalid_request", "A RAG ingest request must carry a document list.", correlationId);
            if (request.Documents.Count > MaximumDocuments)
            {
                return Exhausted("rag.batch_too_large", "The RAG ingest batch exceeds the business limit.", correlationId);
            }

            for (var index = 0; index < request.Documents.Count; index++)
            {
                var document = request.Documents[index];
                if (document == null) return Invalid("rag.document_invalid", "The RAG ingest batch contains an invalid document.", correlationId);
                // The service frame adapter rejects empty text/source_locator/corpus_locator outright;
                // fail here so the caller gets a typed error instead of a bare "rejected".
                if (string.IsNullOrEmpty(document.DocumentId)) return Invalid("rag.document_invalid", "A RAG document requires a document id.", correlationId);
                if (string.IsNullOrEmpty(document.Text)) return Invalid("rag.document_invalid", "A RAG document requires text.", correlationId);
                if (string.IsNullOrEmpty(document.SourceLocator)) return Invalid("rag.document_invalid", "A RAG document requires a source locator.", correlationId);
                if (string.IsNullOrEmpty(document.CorpusLocator)) return Invalid("rag.document_invalid", "A RAG document requires a corpus locator.", correlationId);
                if (string.IsNullOrEmpty(document.AccessScope)) return Invalid("rag.document_invalid", "A RAG document requires an access scope.", correlationId);
                if (string.IsNullOrEmpty(document.SourceClass)) return Invalid("rag.document_invalid", "A RAG document requires a source class.", correlationId);
                if (ByteCount(document.DocumentId) > MaximumCollectionIdBytes
                    || ByteCount(document.AccessScope) > MaximumCollectionIdBytes
                    || ByteCount(document.SourceClass) > MaximumCollectionIdBytes)
                {
                    return Exhausted("rag.document_identity_too_large", "A RAG document identity exceeds the business limit.", correlationId);
                }

                if (ByteCount(document.Text) > MaximumDocumentBytes)
                {
                    return Exhausted("rag.document_too_large", "A RAG document exceeds the business limit.", correlationId);
                }

                if (document.ObservedAt.ToUnixTimeMilliseconds() < 1)
                {
                    return Invalid("rag.observed_time_invalid", "A RAG document requires a valid observed timestamp.", correlationId);
                }
            }

            return null;
        }

        internal static FrameworkError ValidateSearch(RagSearchRequest request, string correlationId)
        {
            if (request == null) return Invalid("rag.invalid_request", "A RAG search request is required.", correlationId);
            var identity = ValidateCollectionIdentity(request.CollectionId, request.CorpusFingerprint, correlationId);
            if (identity != null) return identity;
            // Hybrid is still refused: the backend implements keyword and semantic, and answering a
            // Hybrid request as either one would be a silent downgrade. Semantic is refused here only
            // for wave-1 hosts that have not been rebuilt — the wire carries the mode, so the service
            // decides whether it can serve it.
            if (request.Mode != RetrievalMode.Keyword && request.Mode != RetrievalMode.Semantic)
            {
                return FrameworkErrors.Create("rag.retrieval_mode_unsupported", FrameworkErrorCategory.Unsupported, "Only keyword and semantic retrieval are available in this runtime.", correlationId);
            }

            if (string.IsNullOrEmpty(request.Query)) return Invalid("rag.query_invalid", "A RAG search request requires a query.", correlationId);
            if (ByteCount(request.Query) > MaximumQueryBytes)
            {
                return Exhausted("rag.query_too_large", "The RAG query exceeds the business limit.", correlationId);
            }

            if (request.AccessScopes == null) return Invalid("rag.scope_invalid", "The RAG access scope list is required.", correlationId);
            if (request.AccessScopes.Count > MaximumAccessScopes)
            {
                return Exhausted("rag.scope_invalid", "The RAG access scope list exceeds the business limit.", correlationId);
            }

            for (var index = 0; index < request.AccessScopes.Count; index++)
            {
                var scope = request.AccessScopes[index];
                if (string.IsNullOrEmpty(scope) || ByteCount(scope) > MaximumAccessScopeBytes)
                {
                    return Exhausted("rag.scope_invalid", "The RAG access scope list exceeds the business limit.", correlationId);
                }
            }

            return null;
        }

        internal static string IngestPayload(RagIngestRequest request)
        {
            var builder = new StringBuilder("{\"collection_id\":");
            builder.Append(ProviderRuntimeWire.Quote(request.CollectionId));
            builder.Append(",\"corpus_fingerprint\":").Append(ProviderRuntimeWire.Quote(request.CorpusFingerprint));
            builder.Append(",\"documents\":[");
            for (var index = 0; index < request.Documents.Count; index++)
            {
                if (index > 0) builder.Append(',');
                var document = request.Documents[index];
                builder.Append("{\"access_scope\":").Append(ProviderRuntimeWire.Quote(document.AccessScope));
                builder.Append(",\"corpus_locator\":").Append(ProviderRuntimeWire.Quote(document.CorpusLocator));
                builder.Append(",\"document_id\":").Append(ProviderRuntimeWire.Quote(document.DocumentId));
                builder.Append(",\"observed_unix_ms\":").Append(document.ObservedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"source_class\":").Append(ProviderRuntimeWire.Quote(document.SourceClass));
                builder.Append(",\"source_locator\":").Append(ProviderRuntimeWire.Quote(document.SourceLocator));
                builder.Append(",\"text\":").Append(ProviderRuntimeWire.Quote(document.Text));
                builder.Append('}');
            }

            return builder.Append("]}").ToString();
        }

        internal static string SearchPayload(RagSearchRequest request)
        {
            var builder = new StringBuilder("{\"access_scopes\":[");
            for (var index = 0; index < request.AccessScopes.Count; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append(ProviderRuntimeWire.Quote(request.AccessScopes[index]));
            }

            builder.Append("],\"collection_id\":").Append(ProviderRuntimeWire.Quote(request.CollectionId));
            builder.Append(",\"corpus_fingerprint\":").Append(ProviderRuntimeWire.Quote(request.CorpusFingerprint));
            builder.Append(",\"maximum_results\":").Append(request.MaximumResults.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"mode\":").Append(ProviderRuntimeWire.Quote(ModeToken(request.Mode)));
            builder.Append(",\"query\":").Append(ProviderRuntimeWire.Quote(request.Query));
            return builder.Append('}').ToString();
        }

        internal static string ModeToken(RetrievalMode mode)
        {
            switch (mode)
            {
                case RetrievalMode.Semantic: return "semantic";
                case RetrievalMode.Hybrid: return "hybrid";
                default: return "keyword";
            }
        }

        internal static bool TryParseMode(string token, out RetrievalMode mode)
        {
            switch (token)
            {
                case "keyword": mode = RetrievalMode.Keyword; return true;
                case "semantic": mode = RetrievalMode.Semantic; return true;
                case "hybrid": mode = RetrievalMode.Hybrid; return true;
                default: mode = RetrievalMode.Keyword; return false;
            }
        }

        internal static bool TryReadIngestResult(string payload, RagIngestRequest request, out int ingested, out string error)
        {
            ingested = 0;
            var wire = ProviderRuntimeJson.Deserialize<RagIngestResultWire>(payload, out error);
            if (wire == null
                || !ProviderRuntimeJson.HasExactTopLevelProperties(
                    payload,
                    new[] { "collection_id", "corpus_fingerprint", "ingested" },
                    new[] { "collection_id", "corpus_fingerprint", "ingested" },
                    out error))
            {
                return false;
            }

            if (!StringComparer.Ordinal.Equals(wire.CollectionId, request.CollectionId)
                || !StringComparer.Ordinal.Equals(wire.CorpusFingerprint, request.CorpusFingerprint)
                || wire.Ingested < 0
                || wire.Ingested != request.Documents.Count)
            {
                error = "rag_result_mismatch";
                return false;
            }

            ingested = wire.Ingested;
            return true;
        }

        internal static bool TryReadSearchResult(string payload, RagSearchRequest request, out IReadOnlyList<RagHit> hits, out string error)
        {
            hits = null;
            var wire = ProviderRuntimeJson.Deserialize<RagSearchResultWire>(payload, out error);
            if (wire == null
                || !ProviderRuntimeJson.HasExactTopLevelProperties(
                    payload,
                    new[] { "collection_id", "corpus_fingerprint", "query", "hits" },
                    new[] { "collection_id", "corpus_fingerprint", "query", "hits" },
                    out error))
            {
                return false;
            }

            if (!StringComparer.Ordinal.Equals(wire.CollectionId, request.CollectionId)
                || !StringComparer.Ordinal.Equals(wire.CorpusFingerprint, request.CorpusFingerprint)
                || wire.Hits == null
                || wire.Hits.Count > request.MaximumResults)
            {
                error = "rag_result_mismatch";
                return false;
            }

            var result = new List<RagHit>(wire.Hits.Count);
            for (var index = 0; index < wire.Hits.Count; index++)
            {
                var hit = wire.Hits[index];
                if (hit == null
                    || string.IsNullOrEmpty(hit.DocumentId)
                    || string.IsNullOrEmpty(hit.Text)
                    || string.IsNullOrEmpty(hit.SourceLocator)
                    || string.IsNullOrEmpty(hit.CorpusFingerprint)
                    || hit.Rank < 0)
                {
                    error = "rag_result_mismatch";
                    return false;
                }

                result.Add(new RagHit(hit.DocumentId, hit.Text, hit.SourceLocator, hit.Rank, hit.CorpusFingerprint, request.Mode));
            }

            hits = result.AsReadOnly();
            return true;
        }

        /// <summary>Reads the generic error envelope the storage dispatcher emits on failure.</summary>
        internal static bool TryReadError(string payload, out string errorCode, out bool retryable)
        {
            errorCode = string.Empty;
            retryable = false;
            var wire = ProviderRuntimeJson.Deserialize<GenericErrorWire>(payload, out var error);
            if (wire == null || !string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(wire.ErrorCode)) return false;
            errorCode = wire.ErrorCode;
            retryable = wire.Retryable;
            return true;
        }

        private static FrameworkError ValidateCollectionIdentity(string collectionId, string corpusFingerprint, string correlationId)
        {
            if (string.IsNullOrEmpty(collectionId)) return Invalid("rag.collection_invalid", "A RAG collection id is required.", correlationId);
            if (string.IsNullOrEmpty(corpusFingerprint)) return Invalid("rag.corpus_fingerprint_invalid", "A RAG corpus fingerprint is required.", correlationId);
            if (ByteCount(collectionId) > MaximumCollectionIdBytes || ByteCount(corpusFingerprint) > MaximumCollectionIdBytes)
            {
                return Exhausted("rag.collection_invalid", "A RAG collection identity exceeds the business limit.", correlationId);
            }

            return null;
        }

        private static int ByteCount(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);
        }

        private static FrameworkError Invalid(string code, string fallback, string correlationId)
        {
            return FrameworkErrors.Create(code, FrameworkErrorCategory.InvalidRequest, fallback, correlationId);
        }

        private static FrameworkError Exhausted(string code, string fallback, string correlationId)
        {
            return FrameworkErrors.Create(code, FrameworkErrorCategory.ResourceExhausted, fallback, correlationId);
        }
    }

    [DataContract]
    internal sealed class RagIngestResultWire
    {
        [DataMember(Name = "collection_id")] public string CollectionId { get; set; }
        [DataMember(Name = "corpus_fingerprint")] public string CorpusFingerprint { get; set; }
        [DataMember(Name = "ingested")] public int Ingested { get; set; }
    }

    [DataContract]
    internal sealed class RagSearchResultWire
    {
        [DataMember(Name = "collection_id")] public string CollectionId { get; set; }
        [DataMember(Name = "corpus_fingerprint")] public string CorpusFingerprint { get; set; }
        [DataMember(Name = "query")] public string Query { get; set; }
        [DataMember(Name = "hits")] public List<RagHitWire> Hits { get; set; }
    }

    [DataContract]
    internal sealed class RagHitWire
    {
        [DataMember(Name = "document_id")] public string DocumentId { get; set; }
        [DataMember(Name = "text")] public string Text { get; set; }
        [DataMember(Name = "source_locator")] public string SourceLocator { get; set; }
        [DataMember(Name = "rank")] public int Rank { get; set; }
        [DataMember(Name = "corpus_fingerprint")] public string CorpusFingerprint { get; set; }
    }

    [DataContract]
    internal sealed class GenericErrorWire
    {
        [DataMember(Name = "schema")] public string Schema { get; set; }
        [DataMember(Name = "error_code")] public string ErrorCode { get; set; }
        [DataMember(Name = "category")] public string Category { get; set; }
        [DataMember(Name = "retryable")] public bool Retryable { get; set; }
        [DataMember(Name = "fallback_allowed")] public bool FallbackAllowed { get; set; }
        [DataMember(Name = "safe_message")] public string SafeMessage { get; set; }
    }
}
