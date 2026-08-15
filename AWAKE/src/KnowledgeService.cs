using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using Newtonsoft.Json;

namespace Awake;

internal sealed class KnowledgeService : IDisposable
{
    internal static KnowledgeCorpus TestCorpusOverride;
    private readonly object _gate = new object();
    private readonly IMarcusAiFrameworkHost _host;
    private readonly IKeyValueStore _store;
    private readonly Func<string, string, Task<bool>> _requestPermissionAsync;
    private readonly Action<string> _pushStatus;

    private KnowledgeCorpus _corpus;
    private LocalKeywordIndex _localIndex;
    private string _corpusFingerprint = string.Empty;
    private string _persistedFingerprint = string.Empty;
    private string _lastIngestedFingerprint = string.Empty;
    private Task<bool> _ingestInFlight;
    private string _lastAttemptFingerprint = string.Empty;
    private bool _lastAttemptRetryable;
    private bool _corpusLoaded;

    internal KnowledgeService(
        IMarcusAiFrameworkHost host,
        IKeyValueStore store,
        Func<string, string, Task<bool>> requestPermissionAsync,
        Action<string> pushStatus)
        : this(host, store, requestPermissionAsync, pushStatus, null)
    {
    }

    internal KnowledgeService(
        IMarcusAiFrameworkHost host,
        IKeyValueStore store,
        Func<string, string, Task<bool>> requestPermissionAsync,
        Action<string> pushStatus,
        KnowledgeCorpus injectedCorpus)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _store = store;
        _requestPermissionAsync = requestPermissionAsync;
        _pushStatus = pushStatus ?? (_ => { });
        if (injectedCorpus != null)
        {
            _corpus = injectedCorpus;
            _corpusFingerprint = "awake.knowledge.v1:test-injected";
            _localIndex = new LocalKeywordIndex(injectedCorpus);
            _corpusLoaded = true;
        }
        else if (TestCorpusOverride != null)
        {
            _corpus = TestCorpusOverride;
            _corpusFingerprint = "awake.knowledge.v1:test-injected";
            _localIndex = new LocalKeywordIndex(TestCorpusOverride);
            _corpusLoaded = true;
        }
    }

    internal string LastIngestedFingerprint => _lastIngestedFingerprint;

    internal bool CorpusLoaded
    {
        get { lock (_gate) return _corpusLoaded; }
    }

    internal bool HasPersistedFingerprint => !string.IsNullOrWhiteSpace(_persistedFingerprint);

    internal void Initialize()
    {
        if (CorpusLoaded)
        {
            if (_store != null)
            {
                _ = LoadPersistedFingerprintAsync();
            }
            return;
        }
        byte[] bytes;
        try
        {
            bytes = KnowledgeCorpusLoader.ReadCorpusFile(KnowledgeConstants.CorpusRelativePath);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("knowledge_file_read_error error=" + ex.Message);
            bytes = null;
        }

        KnowledgeCorpus corpus = bytes == null ? null : KnowledgeCorpusLoader.ParseCorpus(bytes);
        lock (_gate)
        {
            _corpus = corpus;
            _corpusFingerprint = bytes == null ? string.Empty : KnowledgeCorpusLoader.ComputeFingerprint(bytes);
            _localIndex = corpus == null ? new LocalKeywordIndex(null) : new LocalKeywordIndex(corpus);
            _corpusLoaded = true;
        }

        if (bytes != null)
        {
            _ = LoadPersistedFingerprintAsync();
        }
        else
        {
            _pushStatus("AWAKE 知识：未找到知识库，仅靠模型本知回答。");
        }
    }

    internal Task<bool> IngestIfNeededAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_corpusLoaded || string.IsNullOrWhiteSpace(_corpusFingerprint))
            {
                return Task.FromResult(false);
            }
            if (StringComparer.Ordinal.Equals(_lastIngestedFingerprint, _corpusFingerprint)
                || StringComparer.Ordinal.Equals(_persistedFingerprint, _corpusFingerprint))
            {
                return Task.FromResult(true);
            }
            if (_ingestInFlight != null)
            {
                return _ingestInFlight;
            }
            if (string.Equals(_lastAttemptFingerprint, _corpusFingerprint, StringComparison.Ordinal)
                && !_lastAttemptRetryable)
            {
                return Task.FromResult(false);
            }
            Task<bool> started = IngestCoreAsync(cancellationToken);
            _ingestInFlight = started;
            return started;
        }
    }

    internal async Task<bool> ForceReingestAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _lastIngestedFingerprint = string.Empty;
            _persistedFingerprint = string.Empty;
            _lastAttemptFingerprint = string.Empty;
            _lastAttemptRetryable = false;
        }
        return await IngestIfNeededAsync(cancellationToken).ConfigureAwait(false);
    }

        private async Task<bool> IngestCoreAsync(CancellationToken cancellationToken)
    {
        bool attemptFailed = false;
        bool attemptRetryable = false;
        try
        {
            bool granted = await RequestPermissionAsync(KnowledgeConstants.PermissionRagWrite, "AWAKE 需要写入世界知识语料。", cancellationToken).ConfigureAwait(false);
            if (!granted)
            {
                AwakeLog.Write("knowledge_ingest_permission_denied");
                attemptFailed = true;
                return false;
            }

            List<RagDocument> documents = new List<RagDocument>();
            lock (_gate)
            {
                if (_corpus?.Documents == null)
                {
                    attemptFailed = true;
                    return false;
                }
                foreach (KnowledgeDocument doc in _corpus.Documents)
                {
                    if (string.IsNullOrWhiteSpace(doc.DocumentId) || string.IsNullOrWhiteSpace(doc.Content)) continue;
                    string searchableContent = doc.Content;
                    if (!string.IsNullOrWhiteSpace(doc.Title) || doc.Keywords != null)
                    {
                        searchableContent = (doc.Title ?? string.Empty);
                        if (doc.Keywords != null && doc.Keywords.Count > 0) searchableContent += " " + string.Join(" ", doc.Keywords);
                        searchableContent += " " + doc.Content;
                    }
                    documents.Add(new RagDocument(
                        doc.DocumentId,
                        searchableContent,
                        doc.SourceLocator ?? "awake_knowledge.json",
                        KnowledgeConstants.AccessScope,
                        SourceClass.ExtensionProvider.ToString(),
                        "awake_knowledge.json",
                        DateTimeOffset.UtcNow));
                }
            }

            RequestContext context = AwakeRuntime.CreateContext(_host, Guid.NewGuid().ToString("N"));
            OperationResult<int> ingested = await _host.Rag.IngestAsync(
                new RagIngestRequest(KnowledgeConstants.CollectionId, _corpusFingerprint, documents),
                context,
                cancellationToken).ConfigureAwait(false);
            if (!ingested.IsSuccess)
            {
                AwakeLog.Write("knowledge_ingest_failed code=" + (ingested.Error?.Code ?? "unknown")
                    + " category=" + (ingested.Error?.Category.ToString() ?? "unknown")
                    + " retryable=" + ingested.Error?.Retryable);
                attemptFailed = true;
                attemptRetryable = ingested.Error?.Retryable == true;
                return false;
            }

            _lastIngestedFingerprint = _corpusFingerprint;
            await PersistFingerprintAsync(_corpusFingerprint, cancellationToken).ConfigureAwait(false);
            _pushStatus("AWAKE 知识已写入记忆。");
            return true;
        }
        catch (OperationCanceledException)
        {
            attemptFailed = true;
            attemptRetryable = true;
            return false;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("knowledge_ingest_error error=" + ex.Message);
            attemptFailed = true;
            attemptRetryable = true;
            return false;
        }
        finally
        {
            lock (_gate)
            {
                if (attemptFailed)
                {
                    _lastAttemptFingerprint = _corpusFingerprint;
                    _lastAttemptRetryable = attemptRetryable;
                }
                else
                {
                    _lastAttemptFingerprint = string.Empty;
                    _lastAttemptRetryable = false;
                }
                _ingestInFlight = null;
            }
        }
    }
    internal async Task<string> RetrieveAsync(string query, CancellationToken cancellationToken)
    {
        return await RetrieveAsync(query, string.Empty, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<string> RetrieveLocalAsync(string query, string contextKeywords, int maximumBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CorpusLoaded || _localIndex == null || _localIndex.IsEmpty) return string.Empty;
        string searchQuery = BuildSearchQuery(query, contextKeywords);
        if (string.IsNullOrWhiteSpace(searchQuery)) return string.Empty;
        IReadOnlyList<KnowledgeHit> hits = _localIndex.Search(searchQuery, KnowledgeConstants.MaximumSearchResults);
        cancellationToken.ThrowIfCancellationRequested();
        return KnowledgeCorpusLoader.BuildRetrievedBlock(hits, maximumBytes);
    }

    internal async Task<string> RetrieveAsync(string query, string contextKeywords, CancellationToken cancellationToken)
    {
        if (!CorpusLoaded) return string.Empty;
        string searchQuery = BuildSearchQuery(query, contextKeywords);
        if (!string.IsNullOrWhiteSpace(contextKeywords))
        {
            AwakeLog.Write("knowledge_search_scene keywords=" + contextKeywords);
        }
        IReadOnlyList<KnowledgeHit> hits = null;
        string mode = "local";

        bool canRead = await RequestPermissionAsync(KnowledgeConstants.PermissionRagRead, "AWAKE 需要读取世界知识语料。", cancellationToken).ConfigureAwait(false);
        if (canRead)
        {
            await IngestIfNeededAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                RequestContext context = AwakeRuntime.CreateContext(_host, Guid.NewGuid().ToString("N"));
                OperationResult<IReadOnlyList<RagHit>> search = await _host.Rag.SearchAsync(
                    new RagSearchRequest(
                        KnowledgeConstants.CollectionId,
                        _corpusFingerprint,
                        searchQuery,
                        new[] { KnowledgeConstants.AccessScope },
                        KnowledgeConstants.MaximumSearchResults),
                    context,
                    cancellationToken).ConfigureAwait(false);

                if (search.IsSuccess && search.Value != null && search.Value.Count > 0)
                {
                    List<KnowledgeHit> converted = new List<KnowledgeHit>();
                    foreach (RagHit hit in search.Value)
                    {
                        converted.Add(new KnowledgeHit(hit.DocumentId, hit.Text));
                    }
                    hits = converted;
                    mode = "rag";
                    _pushStatus("AWAKE 知识：记忆正在回应你。");
                }
                else if (search.IsSuccess)
                {
                    if (_localIndex != null && !_localIndex.IsEmpty)
                    {
                        AwakeLog.Write("rag_fallback local_keyword_index");
                        hits = _localIndex.Search(searchQuery, KnowledgeConstants.MaximumSearchResults);
                        mode = "local";
                        AwakeLog.Write("knowledge_search_empty_fallback_local");
                        _pushStatus("AWAKE 知识：检索无直接命中，先用模型本知作答。");
                    }
                }
                else
                {
                    FrameworkError err = search.Error;
                    if (err != null && (err.Category == FrameworkErrorCategory.NotFound || err.Category == FrameworkErrorCategory.Conflict))
                    {
                        AwakeLog.Write("knowledge_search_missing reingest code=" + err.Code);
                        await ForceReingestAsync(cancellationToken).ConfigureAwait(false);
                        OperationResult<IReadOnlyList<RagHit>> retrySearch = await _host.Rag.SearchAsync(
                            new RagSearchRequest(
                                KnowledgeConstants.CollectionId,
                                _corpusFingerprint,
                                searchQuery,
                                new[] { KnowledgeConstants.AccessScope },
                                KnowledgeConstants.MaximumSearchResults),
                            context,
                            cancellationToken).ConfigureAwait(false);
                        if (retrySearch.IsSuccess && retrySearch.Value != null && retrySearch.Value.Count > 0)
                        {
                            List<KnowledgeHit> converted = new List<KnowledgeHit>();
                            foreach (RagHit hit in retrySearch.Value)
                            {
                                converted.Add(new KnowledgeHit(hit.DocumentId, hit.Text));
                            }
                            hits = converted;
                            mode = "rag";
                        }
                    }
                    if (hits == null)
                    {
                        AwakeLog.Write("knowledge_search_degraded code=" + (err?.Code ?? "unknown"));
                        if (_localIndex != null && !_localIndex.IsEmpty)
                        {
                            AwakeLog.Write("rag_fallback local_keyword_index");
                            hits = _localIndex.Search(searchQuery, KnowledgeConstants.MaximumSearchResults);
                            mode = "local";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AwakeLog.Write("knowledge_search_error error=" + ex.Message);
            }
        }

        if (hits == null && _localIndex != null && !_localIndex.IsEmpty)
        {
            AwakeLog.Write("rag_fallback local_keyword_index");
            hits = _localIndex.Search(searchQuery, KnowledgeConstants.MaximumSearchResults);
            mode = "local";
        }

        if (mode == "local" && hits != null && hits.Count > 0)
        {
            _pushStatus("AWAKE 知识：本地记忆已回应。");
        }
        return KnowledgeCorpusLoader.BuildRetrievedBlock(hits, KnowledgeConstants.MaximumRetrievedBlockBytes);
    }

    private static string BuildSearchQuery(string query, string contextKeywords)
    {
        string player = (query ?? string.Empty).Trim();
        string scene = (contextKeywords ?? string.Empty).Trim();
        if (player.Length == 0) return scene;
        if (scene.Length == 0) return player;
        return player + " " + scene;
    }

    private async Task<bool> RequestPermissionAsync(string permission, string purpose, CancellationToken cancellationToken)
    {
        if (_requestPermissionAsync != null)
        {
            try
            {
                return await _requestPermissionAsync(permission, purpose).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AwakeLog.Write("knowledge_permission_request_error permission=" + permission + " error=" + ex.Message);
                return false;
            }
        }
        return true;
    }

    private async Task LoadPersistedFingerprintAsync()
    {
        if (_store == null) return;
        try
        {
            RequestContext context = AwakeRuntime.CreateContext(_host, Guid.NewGuid().ToString("N"));
            OperationResult<string> result = await _store.GetAsync(KnowledgeConstants.FingerprintKey, context, CancellationToken.None).ConfigureAwait(false);
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
            {
                string raw = result.Value;
                if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                {
                    try
                    {
                        raw = JsonConvert.DeserializeObject<string>(raw) ?? string.Empty;
                    }
                    catch
                    {
                        raw = raw.Substring(1, raw.Length - 2);
                    }
                }
                lock (_gate)
                {
                    _persistedFingerprint = raw;
                }
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("knowledge_fingerprint_load_error error=" + ex.Message);
        }
    }

    private async Task PersistFingerprintAsync(string fingerprint, CancellationToken cancellationToken)
    {
        if (_store == null) return;
        try
        {
            RequestContext context = AwakeRuntime.CreateContext(_host, Guid.NewGuid().ToString("N"));
            OperationResult<bool> stored = await _store.SetAsync(KnowledgeConstants.FingerprintKey, "\"" + fingerprint.Replace("\"", "\\\"") + "\"", context, cancellationToken).ConfigureAwait(false);
            if (stored.IsSuccess && stored.Value)
            {
                lock (_gate)
                {
                    _persistedFingerprint = fingerprint;
                }
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("knowledge_fingerprint_persist_error error=" + ex.Message);
        }
    }

    public void Dispose()
    {
    }
}
