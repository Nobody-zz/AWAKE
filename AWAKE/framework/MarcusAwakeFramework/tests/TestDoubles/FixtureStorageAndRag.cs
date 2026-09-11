using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace MarcusAwakeFramework.Tests.TestDoubles
{
    internal sealed class FixtureKeyValueStore : IKeyValueStore
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly StorageNamespaceRef reference;

        internal FixtureKeyValueStore(StorageNamespaceRef reference) { this.reference = reference; }

        public Task<OperationResult<string>> GetAsync(string key, RequestContext context, CancellationToken cancellationToken)
        {
            var validation = Validate(key, context, cancellationToken);
            if (!validation.IsSuccess) return Task.FromResult(OperationResult<string>.Failed(validation.Error));
            lock (values)
            {
                string value;
                return Task.FromResult(OperationResult<string>.Succeeded(values.TryGetValue(key, out value) ? value : null));
            }
        }

        public Task<OperationResult<bool>> SetAsync(string key, string value, RequestContext context, CancellationToken cancellationToken)
        {
            var validation = Validate(key, context, cancellationToken);
            if (!validation.IsSuccess) return Task.FromResult(validation);
            lock (values) values[key] = value ?? string.Empty;
            return Task.FromResult(OperationResult<bool>.Succeeded(true));
        }

        public Task<OperationResult<bool>> DeleteAsync(string key, RequestContext context, CancellationToken cancellationToken)
        {
            var validation = Validate(key, context, cancellationToken);
            if (!validation.IsSuccess) return Task.FromResult(validation);
            lock (values) values.Remove(key);
            return Task.FromResult(OperationResult<bool>.Succeeded(true));
        }

        private OperationResult<bool> Validate(string key, RequestContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key) || context == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("storage.invalid_request", FrameworkErrorCategory.InvalidRequest, "A storage key and context are required.", context?.CorrelationId ?? "fixture-storage"));
            if (cancellationToken == CancellationToken.None) return OperationResult<bool>.Failed(FrameworkErrors.Create("storage.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId));
            if (!reference.Session.Equals(context.Session) || !StringComparer.Ordinal.Equals(reference.OwnerId, context.Caller.Value)) return OperationResult<bool>.Failed(FrameworkErrors.Create("storage.scope_denied", FrameworkErrorCategory.Denied, "The storage namespace is outside the request scope.", context.CorrelationId));
            return OperationResult<bool>.Succeeded(true);
        }
    }

    internal sealed class FixtureStorageService : IStorageService
    {
        private readonly Dictionary<string, FixtureKeyValueStore> stores = new Dictionary<string, FixtureKeyValueStore>(StringComparer.Ordinal);

        public Task<OperationResult<IKeyValueStore>> OpenCampaignNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken)
        {
            return Open(namespaceId, StorageScopeKind.Campaign, context, cancellationToken);
        }

        public Task<OperationResult<IKeyValueStore>> OpenSessionNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken)
        {
            return Open(namespaceId, StorageScopeKind.Session, context, cancellationToken);
        }

        private Task<OperationResult<IKeyValueStore>> Open(string namespaceId, StorageScopeKind scope, RequestContext context, CancellationToken cancellationToken)
        {
            if (context == null || string.IsNullOrWhiteSpace(namespaceId)) return Task.FromResult(OperationResult<IKeyValueStore>.Failed(FrameworkErrors.Create("storage.invalid_request", FrameworkErrorCategory.InvalidRequest, "A namespace and context are required.", context?.CorrelationId ?? "fixture-storage")));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(OperationResult<IKeyValueStore>.Failed(FrameworkErrors.Create("storage.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId)));
            var reference = new StorageNamespaceRef(context.Caller.Value, context.Session, namespaceId, scope);
            var key = reference.OwnerId + "|" + reference.Session + "|" + reference.NamespaceId + "|" + reference.Scope;
            lock (stores)
            {
                FixtureKeyValueStore store;
                if (!stores.TryGetValue(key, out store)) stores[key] = store = new FixtureKeyValueStore(reference);
                return Task.FromResult(OperationResult<IKeyValueStore>.Succeeded((IKeyValueStore)store));
            }
        }
    }

    internal sealed class FixtureRagService : IRagService
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, RagCorpus> corpora = new Dictionary<string, RagCorpus>(StringComparer.Ordinal);

        public Task<OperationResult<int>> IngestAsync(RagIngestRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            if (request == null || context == null) return Task.FromResult(OperationResult<int>.Failed(FrameworkErrors.Create("rag.invalid_request", FrameworkErrorCategory.InvalidRequest, "A RAG ingest request and context are required.", context?.CorrelationId ?? "fixture-rag")));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(OperationResult<int>.Failed(FrameworkErrors.Create("rag.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId)));
            var key = CorpusKey(context.Caller.Value, context.Session, request.CollectionId, request.CorpusFingerprint, string.Empty);
            lock (sync) corpora[key] = new RagCorpus(context.Caller.Value, context.Session, request.CollectionId, request.CorpusFingerprint, request.Documents.ToList());
            return Task.FromResult(OperationResult<int>.Succeeded(request.Documents.Count));
        }

        public Task<OperationResult<IReadOnlyList<RagHit>>> SearchAsync(RagSearchRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            if (request == null || context == null) return Task.FromResult(OperationResult<IReadOnlyList<RagHit>>.Failed(FrameworkErrors.Create("rag.invalid_request", FrameworkErrorCategory.InvalidRequest, "A RAG search request and context are required.", context?.CorrelationId ?? "fixture-rag")));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(OperationResult<IReadOnlyList<RagHit>>.Failed(FrameworkErrors.Create("rag.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId)));
            if (request.Mode != RetrievalMode.Keyword) return Task.FromResult(OperationResult<IReadOnlyList<RagHit>>.Failed(FrameworkErrors.Create("rag.unverified", FrameworkErrorCategory.Unsupported, "The requested retrieval mode is not available in the fixture.", context.CorrelationId)));
            if (!StringComparer.Ordinal.Equals(request.OwnerId, context.Caller.Value) || !StringComparer.Ordinal.Equals(request.CampaignGuid, context.Session.CampaignGuid) || !StringComparer.Ordinal.Equals(request.TimelineId, context.Session.TimelineId) || !StringComparer.Ordinal.Equals(request.SessionId, context.Session.SessionId)) return Task.FromResult(OperationResult<IReadOnlyList<RagHit>>.Failed(FrameworkErrors.Create("rag.scope_denied", FrameworkErrorCategory.Denied, "The RAG request is outside the session scope.", context.CorrelationId)));
            RagCorpus corpus;
            lock (sync) corpus = corpora.Values.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.OwnerId, request.OwnerId) && item.Session.Equals(context.Session) && StringComparer.Ordinal.Equals(item.CollectionId, request.CollectionId));
            if (corpus == null || !StringComparer.Ordinal.Equals(corpus.CorpusFingerprint, request.CorpusFingerprint)) return Task.FromResult(OperationResult<IReadOnlyList<RagHit>>.Failed(FrameworkErrors.Create("rag_index_stale", FrameworkErrorCategory.Conflict, "The RAG corpus fingerprint is stale.", context.CorrelationId, retryable: true)));
            var queryTerms = (request.Query ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n', ',', '，', '。' }, StringSplitOptions.RemoveEmptyEntries);
            var matches = new List<RagHit>();
            for (var index = 0; index < corpus.Documents.Count && matches.Count < Math.Min(request.MaximumResults, 8); index++)
            {
                var document = corpus.Documents[index];
                if (request.AccessScopes.Count > 0 && !request.AccessScopes.Contains(document.AccessScope)) continue;
                var score = 0;
                for (var termIndex = 0; termIndex < queryTerms.Length; termIndex++) if (document.Text.IndexOf(queryTerms[termIndex], StringComparison.OrdinalIgnoreCase) >= 0) score++;
                if (score == 0) continue;
                matches.Add(new RagHit(document.DocumentId, document.Text, document.SourceLocator, score, corpus.CorpusFingerprint, request.Mode));
            }
            var ordered = matches.OrderByDescending(item => item.Rank).ThenBy(item => item.DocumentId, StringComparer.Ordinal).ToList();
            var bytes = 0;
            var bounded = new List<RagHit>();
            for (var index = 0; index < ordered.Count; index++)
            {
                var size = Encoding.UTF8.GetByteCount(ordered[index].Text);
                if (bytes + size > 16384) break;
                bounded.Add(ordered[index]);
                bytes += size;
            }
            return Task.FromResult(OperationResult<IReadOnlyList<RagHit>>.Succeeded(bounded.AsReadOnly()));
        }

        private static string CorpusKey(string ownerId, SessionRef session, string collectionId, string corpusFingerprint, string identityProfileHash) => ownerId + "|" + session + "|" + collectionId + "|" + corpusFingerprint + "|" + identityProfileHash;

        private sealed class RagCorpus
        {
            internal RagCorpus(string ownerId, SessionRef session, string collectionId, string corpusFingerprint, IReadOnlyList<RagDocument> documents)
            {
                OwnerId = ownerId;
                Session = session;
                CollectionId = collectionId;
                CorpusFingerprint = corpusFingerprint;
                Documents = documents;
            }
            internal string OwnerId { get; }
            internal SessionRef Session { get; }
            internal string CollectionId { get; }
            internal string CorpusFingerprint { get; }
            internal IReadOnlyList<RagDocument> Documents { get; }
        }
    }

    internal sealed class FixtureEgressPolicy : IEgressPolicy
    {
        public OperationResult<EgressPolicyDecision> Evaluate(EgressPolicyRequest request, RequestContext context)
        {
            if (request == null || context == null) return OperationResult<EgressPolicyDecision>.Failed(FrameworkErrors.Create("egress.invalid_request", FrameworkErrorCategory.InvalidRequest, "An egress request and context are required.", context?.CorrelationId ?? "fixture-egress"));
            if (StringComparer.Ordinal.Equals(request.Classification, "secret")) return OperationResult<EgressPolicyDecision>.Succeeded(new EgressPolicyDecision(EgressDecisionKind.Denied, "policy-denied", "classification_denied", request.RequestHash));
            return OperationResult<EgressPolicyDecision>.Succeeded(new EgressPolicyDecision(EgressDecisionKind.Allowed, "policy-allowed", "fixture_allowed", request.RequestHash));
        }

        public OperationResult<bool> Consume(EgressPolicyDecision decision, AiTaskScope scope, RequestContext context)
        {
            if (decision == null || scope == null || context == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("egress.invalid_request", FrameworkErrorCategory.InvalidRequest, "An egress decision, scope and context are required.", context?.CorrelationId ?? "fixture-egress"));
            if (!decision.Allowed) return OperationResult<bool>.Failed(FrameworkErrors.Create("egress.denied", FrameworkErrorCategory.Denied, "The egress policy denied the request.", context.CorrelationId));
            if (!scope.BelongsTo(context.Caller, context.Session, context.SessionGeneration, context.CorrelationId)) return OperationResult<bool>.Failed(FrameworkErrors.Create("egress.scope_denied", FrameworkErrorCategory.Denied, "The egress scope does not belong to the request.", context.CorrelationId));
            return OperationResult<bool>.Succeeded(true);
        }
    }
}
