using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api
{
    public enum RetrievalMode
    {
        Keyword,
        Hybrid,
        Semantic
    }

    public enum StorageScopeKind
    {
        Campaign,
        Session,
        Extension,
        Sidecar
    }

    public enum SourceClass
    {
        GameRuntime,
        ExtensionProvider,
        PlayerOverlay,
        WorldEvent,
        Conversation
    }

    public sealed class StorageNamespaceRef
    {
        public StorageNamespaceRef(string ownerId, SessionRef session, string namespaceId, StorageScopeKind scope)
        {
            OwnerId = ContractGuard.Id(ownerId, nameof(ownerId));
            Session = session ?? throw new ArgumentNullException(nameof(session));
            NamespaceId = ContractGuard.Id(namespaceId, nameof(namespaceId));
            Scope = scope;
        }

        public string OwnerId { get; }
        public SessionRef Session { get; }
        public string NamespaceId { get; }
        public StorageScopeKind Scope { get; }
    }

    public interface IKeyValueStore
    {
        Task<OperationResult<string>> GetAsync(string key, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<bool>> SetAsync(string key, string value, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<bool>> DeleteAsync(string key, RequestContext context, CancellationToken cancellationToken);
    }

    public interface IStorageService
    {
        Task<OperationResult<IKeyValueStore>> OpenCampaignNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<IKeyValueStore>> OpenSessionNamespaceAsync(string namespaceId, RequestContext context, CancellationToken cancellationToken);
    }

    public sealed class RagDocument
    {
        public RagDocument(string documentId, string text, string sourceLocator, string accessScope, string sourceClass, string corpusLocator, DateTimeOffset observedAt)
        {
            DocumentId = ContractGuard.Id(documentId, nameof(documentId));
            Text = text ?? string.Empty;
            SourceLocator = sourceLocator ?? string.Empty;
            AccessScope = ContractGuard.Id(accessScope, nameof(accessScope));
            SourceClass = ContractGuard.Id(sourceClass, nameof(sourceClass));
            CorpusLocator = corpusLocator ?? string.Empty;
            ObservedAt = observedAt;
        }

        public string DocumentId { get; }
        public string Text { get; }
        public string SourceLocator { get; }
        public string AccessScope { get; }
        public string SourceClass { get; }
        public string CorpusLocator { get; }
        public DateTimeOffset ObservedAt { get; }
    }

    public sealed class RagIngestRequest
    {
        public RagIngestRequest(string collectionId, string corpusFingerprint, IReadOnlyList<RagDocument> documents)
        {
            CollectionId = ContractGuard.Id(collectionId, nameof(collectionId));
            CorpusFingerprint = ContractGuard.Id(corpusFingerprint, nameof(corpusFingerprint));
            Documents = documents ?? new RagDocument[0];
        }

        public string CollectionId { get; }
        public string CorpusFingerprint { get; }
        public IReadOnlyList<RagDocument> Documents { get; }
    }

    public sealed class RagSearchRequest
    {
        public RagSearchRequest(string collectionId, string corpusFingerprint, string query, IReadOnlyList<string> accessScopes, int maximumResults)
            : this(collectionId, corpusFingerprint, query, accessScopes, maximumResults, RetrievalMode.Keyword, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, new string[0])
        {
        }

        public RagSearchRequest(string collectionId, string corpusFingerprint, string query, IReadOnlyList<string> accessScopes, int maximumResults, RetrievalMode mode, string ownerId, string campaignGuid, string timelineId, string sessionId, string identityProfileHash, IReadOnlyList<string> grantRuleIds)
        {
            CollectionId = ContractGuard.Id(collectionId, nameof(collectionId));
            CorpusFingerprint = ContractGuard.Id(corpusFingerprint, nameof(corpusFingerprint));
            Query = query ?? string.Empty;
            AccessScopes = accessScopes ?? new string[0];
            if (maximumResults < 1 || maximumResults > 64) throw new ArgumentOutOfRangeException(nameof(maximumResults));
            MaximumResults = maximumResults;
            Mode = mode;
            OwnerId = ownerId ?? string.Empty;
            CampaignGuid = campaignGuid ?? string.Empty;
            TimelineId = timelineId ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            IdentityProfileHash = identityProfileHash ?? string.Empty;
            GrantRuleIds = grantRuleIds ?? new string[0];
        }

        public string CollectionId { get; }
        public string CorpusFingerprint { get; }
        public string Query { get; }
        public IReadOnlyList<string> AccessScopes { get; }
        public int MaximumResults { get; }
        public RetrievalMode Mode { get; }
        public string OwnerId { get; }
        public string CampaignGuid { get; }
        public string TimelineId { get; }
        public string SessionId { get; }
        public string IdentityProfileHash { get; }
        public IReadOnlyList<string> GrantRuleIds { get; }
    }

    public sealed class RagHit
    {
        public RagHit(string documentId, string text, string sourceLocator, int rank, string corpusFingerprint, RetrievalMode mode)
        {
            DocumentId = ContractGuard.Id(documentId, nameof(documentId));
            Text = text ?? string.Empty;
            SourceLocator = sourceLocator ?? string.Empty;
            Rank = rank;
            CorpusFingerprint = ContractGuard.Id(corpusFingerprint, nameof(corpusFingerprint));
            Mode = mode;
        }

        public string DocumentId { get; }
        public string Text { get; }
        public string SourceLocator { get; }
        public int Rank { get; }
        public string CorpusFingerprint { get; }
        public RetrievalMode Mode { get; }
    }

    public interface IRagService
    {
        Task<OperationResult<int>> IngestAsync(RagIngestRequest request, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<IReadOnlyList<RagHit>>> SearchAsync(RagSearchRequest request, RequestContext context, CancellationToken cancellationToken);
    }
}
