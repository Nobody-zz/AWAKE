using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// 把世界书条目接到运行时服务的 RAG 数据面上 —— 语义召回这一路的**真件**。
///
/// 分工（与 `docs/PLAN-20260917-语义层接在哪怎么接.md` 一致）：
///   · 词条文本由**模组侧**按拼法 D 拼好（`WorldKnowledgePassage`），服务侧只管编码与检索；
///   · 权限、分级、拼块**一个字都不动** —— 这一路只负责"把候选捞回来"，捞回来的仍旧交给
///     `WorldKnowledgeQueryService` 原有的身份过滤去裁决。所以 RAG 的 accessScope 一律 `worldbook`、
///     检索时不带 scope 过滤：在两层里各判一次权限，只会多出一处不一致的机会。
///
/// 三件必须说清的事：
///   ① **模型不进游戏进程**。这里只发 IPC，ONNX 与那 90 MB 权重全在服务侧（`docs/CONFIG-20260916 §28`）。
///   ② **任何失败都退化成"这一路没说话"**（返回空表 / `false`），不抛、不重试、不留半截状态。
///      模型缺失、服务没起、超时，玩家应该只是少拿到语义那部分的召回，而不是对话崩掉。
///   ③ **同步等异步**用 `Task.Run` 把整段异步流程推到线程池线程上跑，再带超时等它 ——
///      这是唯一不会和游戏主线程互相等的写法（框架内部若有一处忘了 `ConfigureAwait(false)`，
///      直接在主线程上 `GetResult()` 就会死锁）。
/// </summary>
internal sealed class AwakeWorldKnowledgeSemanticIndex : IWorldKnowledgeSemanticIndex
{
    /// <summary>RAG 集合名。与旧代 `KnowledgeConstants.CollectionId`（`awake.knowledge`）**故意不同**：
    /// 那是被作废那条链的集合，共用会把两代语料混在一个库里（`docs/DECISION-20260916-旧代检索链路作废.md`）。</summary>
    internal const string CollectionId = "awake.worldbook";

    /// <summary>入库时每批几条。服务侧对单批条数有上限（超过会回 `rag.batch_too_large`），16 是安全值。</summary>
    internal const int IngestBatchSize = 16;

    /// <summary>一次召回最多等多久。宁可这一轮没有语义召回，也不能让玩家的对话卡在那儿等模型。</summary>
    internal const int SearchTimeoutMilliseconds = 1500;

    /// <summary>入库等待上限：448 条要跑 28 批、每批都要编码，给足余量。</summary>
    internal const int IngestTimeoutMilliseconds = 120000;

    private readonly IRagService _rag;
    private readonly RequestContext _context;
    private readonly string _corpusFingerprint;

    internal AwakeWorldKnowledgeSemanticIndex(IRagService rag, RequestContext context, string corpusFingerprint)
    {
        _rag = rag ?? throw new ArgumentNullException(nameof(rag));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _corpusFingerprint = string.IsNullOrWhiteSpace(corpusFingerprint) ? "worldbook.v1" : corpusFingerprint;
    }

    internal string CorpusFingerprint { get { return _corpusFingerprint; } }

    /// <summary>
    /// 世界书重载后的语料指纹。**必须把拼法的版本号带上**：只改拼法不改指纹，服务侧会认为语料没变、
    /// 沿用旧向量，而向量是按旧文本算的 —— 排序会静默变差，没有任何报错。
    /// </summary>
    internal static string ComposeFingerprint(string packageId, string version)
    {
        return (string.IsNullOrWhiteSpace(packageId) ? "worldbook" : packageId)
            + "@" + (string.IsNullOrWhiteSpace(version) ? "0" : version)
            + "#" + WorldKnowledgePassage.Revision;
    }

    public IReadOnlyList<string> Search(string playerText, int limit)
    {
        if (string.IsNullOrWhiteSpace(playerText) || limit < 1) return Array.Empty<string>();
        var request = new RagSearchRequest(
            CollectionId,
            _corpusFingerprint,
            playerText,
            Array.Empty<string>(),          // 不带 scope 过滤：权限在模组侧原有的那一层判
            limit > 64 ? 64 : limit,        // 框架侧的合法区间是 1..64
            RetrievalMode.Semantic,
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>());

        OperationResult<IReadOnlyList<RagHit>> result = SearchOnce(request);
        if (result == null || !result.IsSuccess || result.Value == null) return Array.Empty<string>();
        return result.Value.Select(hit => hit.DocumentId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
    }

    /// <summary>
    /// 把整本世界书按拼法 D 灌进 RAG 集合。**离线/加载期做的事**，不进对话热路径。
    /// 返回 false 表示这一路没建起来（调用方应当照常跑字面链，不要因此拦住世界书加载）。
    /// </summary>
    internal bool TryIngest(IReadOnlyList<WorldKnowledgeEntry> entries, out int ingested, out string error)
    {
        ingested = 0;
        error = string.Empty;
        if (entries == null || entries.Count == 0) { error = "no_entries"; return false; }

        for (int offset = 0; offset < entries.Count; offset += IngestBatchSize)
        {
            int count = Math.Min(IngestBatchSize, entries.Count - offset);
            var documents = new List<RagDocument>(count);
            for (int index = 0; index < count; index++)
            {
                WorldKnowledgeEntry entry = entries[offset + index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
                string text = WorldKnowledgePassage.Compose(entry);
                if (string.IsNullOrWhiteSpace(text)) continue;
                documents.Add(new RagDocument(entry.Id, text, "worldbook/" + entry.Id, "worldbook", "worldbook", _corpusFingerprint, DateTimeOffset.UtcNow));
            }
            if (documents.Count == 0) continue;

            OperationResult<int> result = IngestOnce(new RagIngestRequest(CollectionId, _corpusFingerprint, documents));
            if (result == null || !result.IsSuccess)
            {
                error = result == null ? "ingest_timeout" : (result.Error == null ? "ingest_failed" : result.Error.Code);
                return false;
            }
            ingested += result.Value;
        }
        return ingested > 0;
    }

    private OperationResult<IReadOnlyList<RagHit>> SearchOnce(RagSearchRequest request)
    {
        try
        {
            using (var cancellation = new CancellationTokenSource(SearchTimeoutMilliseconds))
            {
                // 整段异步流程在池线程上跑（见类注释第③条）。
                Task<OperationResult<IReadOnlyList<RagHit>>> task =
                    Task.Run(() => _rag.SearchAsync(request, _context, cancellation.Token), CancellationToken.None);
                if (!task.Wait(SearchTimeoutMilliseconds))
                {
                    cancellation.Cancel();
                    return null;
                }
                return task.Result;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private OperationResult<int> IngestOnce(RagIngestRequest request)
    {
        try
        {
            using (var cancellation = new CancellationTokenSource(IngestTimeoutMilliseconds))
            {
                Task<OperationResult<int>> task =
                    Task.Run(() => _rag.IngestAsync(request, _context, cancellation.Token), CancellationToken.None);
                if (!task.Wait(IngestTimeoutMilliseconds))
                {
                    cancellation.Cancel();
                    return null;
                }
                return task.Result;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
}
