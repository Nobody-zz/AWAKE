using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Awake;
using MarcusAwakeFramework.Api;
using MarcusAwakeStorage;
using Newtonsoft.Json.Linq;

// 「生产里那根线接上以后，语义臂有没有真的开始工作」—— 离实验台（2026-09-17）。
//
// 背景：语义那一整条链早就齐了（服务侧有语义档、游戏侧有可空委托、适配器也写好了），
// 但 `AttachSemanticIndex` / `TryIngest` **只有离线验台在调** ⇒ 游戏跑起来只有字面那条。
// 本轮补上挂载器 `AwakeSemanticArmBootstrap`，这个验台就是来证它**真的在干活**的。
//
// 形态：**零替身**。宿主 = 框架自己的 `FrameworkHost.CreateDefaultHost`；
//       会话 = 框架自己的 `SessionCoordinator`；RAG 后端 + 嵌入器 = 真件（真 SQLite + 真 ONNX）。
//       唯一片出去的是 `AwakeRuntime` 的 host 定位（要 TaleWorlds），见 `SemanticArmStubs.cs`。
//
// 判据（缺一不可）：
//   1 阳性对照    嵌入器自比 = 1、不同句不同向量（防"静默退化成常量向量"）
//   2 世界书先到  host 还没就位时 Schedule ⇒ **不许**挂上任何东西（那一次等不到 host 是正常的）
//   3 战役内接上  摆好宿主 + RetryCurrent ⇒ 真的挂上（`HasSemanticIndex` 为真）且只入库一次
//   4 端到端变好  用**被挂载器挂上的那个服务**跑 24 条题：hit@3 = 23/24（与合并验台同值）
//   5 幂等        同一会话反复 Schedule/Retry ⇒ 入库次数不涨（每次对话多一次 IPC 是浪费）
//   6 换战役重来  会话变了 ⇒ 必须重新入库（向量表按 campaign/timeline 分行，新档里那些行不存在）
//   7 失败回落    host 始终不来 ⇒ 不挂、不抛、不卡；同一个服务照常回答（只是回到字面那条）
//
// 运行：dotnet run -c Release --project AWAKE/tools/semantic-arm-bootstrap-smoke [模型目录] [词表路径]
internal static class Program
{
    private const string DefaultModelDirectory = @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\ONNX";
    private const string DefaultVocabularyPath = @"D:\AWAKE-Dev\AWAKE\tools\_af_tokenizer_20260916\vocab.txt";

    // 与合并验台 `worldbook-rag-merge` 同值的对照线（同一条字面臂、同一份题集）。
    private const int ExpectedLiteralHit3 = 19;
    private const int ExpectedMergedHit3 = 23;

    private static int _failures;

    internal static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 1 && args[0] == "--help")
        {
            Console.WriteLine("用法: semantic-arm-bootstrap-smoke [模型目录] [词表路径]");
            return 0;
        }

        string modelDirectory = args.Length > 0 ? args[0] : DefaultModelDirectory;
        string vocabularyPath = args.Length > 1 ? args[1] : DefaultVocabularyPath;
        string repositoryRoot = ResolveRepositoryRoot();
        string packageManifest = Path.Combine(repositoryRoot, "ModuleData", "Worldbook", "packages", "calradia", "manifest.json");
        string casesPath = Path.Combine(repositoryRoot, "tools", "_retrieval_cases_20260916.json");
        if (!File.Exists(packageManifest)) { Console.Error.WriteLine("FAIL package_manifest_missing:" + packageManifest); return 1; }
        if (!File.Exists(casesPath)) { Console.Error.WriteLine("FAIL cases_missing:" + casesPath); return 1; }

        WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.Load(packageManifest);
        Console.WriteLine("包 = " + snapshot.PackageId + "@" + snapshot.Version + "，条目 " + snapshot.Entries.Count + " 条");
        Console.WriteLine("模型目录 = " + modelDirectory);
        Console.WriteLine("词表     = " + vocabularyPath);
        Console.WriteLine();

        string dataRoot = Path.Combine(Path.GetTempPath(), "awake-semantic-arm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        try
        {
            var embedderOptions = new OnnxEmbedderOptions
            {
                ModelDirectory = modelDirectory,
                VocabularyPath = vocabularyPath,
                ModelId = "bge-small-zh-v1.5|" + WorldKnowledgePassage.Revision,
            };

            using var embedder = new OnnxSentenceEmbedder(embedderOptions);
            using var backend = new SqliteStorageAndRagBackend(Path.Combine(dataRoot, "semantic-arm.db"), null, embedder);
            var counting = new CountingRagService(backend);

            // ── 1 阳性对照：嵌入器自己得是活的 ────────────────────────────────────
            string[] probes = { "圆顶锅盔是什么", "圆顶锅盔多少钱", "斯特拉吉亚的军队怎么打仗", "怎么做面包", "今天天气不错" };
            float[][] vectors = embedder.Encode(probes, CancellationToken.None);
            int distinct = vectors.Select(v => string.Join(",", v.Select(x => x.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)))).Distinct().Count();
            bool degenerate = vectors.Any(v => v.All(x => x == 0f) || v.Any(float.IsNaN));
            double self = Dot(vectors[0], vectors[0]);
            Check("1 阳性对照（嵌入器活着）", distinct == probes.Length && !degenerate && Math.Abs(self - 1d) < 1e-4,
                "维度 " + embedder.Dimension + "，不同向量 " + distinct + "/" + probes.Length + "，自比 " + self.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));

            // 验台把等待窗口压到"读一次就走"，好让第 2 条判据在一瞬间得到答案。
            AwakeSemanticArmBootstrap.HostWaitAttemptsOverride = 1;
            AwakeSemanticArmBootstrap.HostWaitIntervalMillisecondsOverride = 0;

            var campaignOne = new SessionRef("campaign-arm-1", "timeline-arm-1", "session-arm-1");
            var campaignTwo = new SessionRef("campaign-arm-2", "timeline-arm-2", "session-arm-2");

            // ⚠️ 一次入库**不是一次调用**：挂载器按批灌（`IngestBatchSize` 条一批），
            // 448 条 ⇒ 28 次 `IngestAsync`。判据要按"批"算，否则量到的是批量而不是幂等。
            int callsPerPass = (snapshot.Entries.Count + AwakeWorldKnowledgeSemanticIndex.IngestBatchSize - 1)
                / AwakeWorldKnowledgeSemanticIndex.IngestBatchSize;
            Console.WriteLine("每轮入库 = " + callsPerPass + " 批（批量 " + AwakeWorldKnowledgeSemanticIndex.IngestBatchSize + " 条）");
            Console.WriteLine();

            // ── 2 世界书先到、host 还没就位 ──────────────────────────────────────
            AwakeSemanticArmBootstrap.ResetForTesting();
            AwakeRuntime.ResetStubHost();
            var service = new WorldKnowledgeQueryService(snapshot);
            AwakeSemanticArmBootstrap.Schedule(service, snapshot);
            WaitForBootstrap();
            bool notAttachedEarly = !service.HasSemanticIndex;
            int ingestsAfterEarly = counting.IngestCalls;
            Check("2 世界书先到（host 未就位不挂）", notAttachedEarly && ingestsAfterEarly == 0,
                "HasSemanticIndex=" + service.HasSemanticIndex + "，入库次数=" + ingestsAfterEarly
                + "，问过 host " + AwakeRuntime.ResolveCallCount + " 次");

            // ── 3 战役内接上（这一步就是本轮补的那根线）───────────────────────────
            IMarcusAiFrameworkHost host = BuildHost(counting, campaignOne);
            AwakeRuntime.StubHost = host;
            AwakeSemanticArmBootstrap.RetryCurrent();
            WaitForBootstrap();
            bool attached = service.HasSemanticIndex;
            Check("3 战役内接上（挂载器真的挂了）", attached && counting.IngestCalls == callsPerPass,
                "HasSemanticIndex=" + attached + "，入库调用=" + counting.IngestCalls + "（一轮 " + callsPerPass + " 批）"
                + "，attach 键=" + AwakeSemanticArmBootstrap.AttachedKey);

            // ── 4 端到端：被挂载器挂上的那个服务，跑题集 ──────────────────────────
            var literalOnly = new WorldKnowledgeQueryService(snapshot);
            int literalHits = CountHit3(literalOnly, snapshot, casesPath);
            int mergedHits = CountHit3(service, snapshot, casesPath);
            Check("4 端到端变好（挂上以后 hit@3）", literalHits == ExpectedLiteralHit3 && mergedHits == ExpectedMergedHit3,
                "字面 " + literalHits + "/24 → 挂上 " + mergedHits + "/24（对照线 " + ExpectedLiteralHit3 + " → " + ExpectedMergedHit3 + "）");

            // ── 5 幂等：同一会话再催一次，不许重复入库 ────────────────────────────
            AwakeSemanticArmBootstrap.RetryCurrent();
            WaitForBootstrap();
            Check("5 同一会话不重复入库", counting.IngestCalls == callsPerPass,
                "入库调用仍为 " + counting.IngestCalls + "（第二次只是 already_attached，一批都没重灌）");

            // ── 6 换战役会话 ⇒ 必须重新入库（向量按 campaign/timeline 分行）────────
            AwakeRuntime.StubHost = BuildHost(counting, campaignTwo);
            AwakeSemanticArmBootstrap.RetryCurrent();
            WaitForBootstrap();
            string keyAfterSecond = AwakeSemanticArmBootstrap.AttachedKey;
            Check("6 换战役重新入库", service.HasSemanticIndex && counting.IngestCalls == callsPerPass * 2 && keyAfterSecond.Contains("campaign-arm-2", StringComparison.Ordinal),
                "HasSemanticIndex=" + service.HasSemanticIndex + "，入库调用=" + counting.IngestCalls
                + "（应为两轮 " + (callsPerPass * 2) + "），attach 键=" + keyAfterSecond);

            // ── 7 失败回落：host 始终不来 ⇒ 不挂、不抛、照常回答 ─────────────────
            AwakeSemanticArmBootstrap.ResetForTesting();
            AwakeRuntime.ResetStubHost();
            var fallbackService = new WorldKnowledgeQueryService(snapshot);
            AwakeSemanticArmBootstrap.Schedule(fallbackService, snapshot);
            WaitForBootstrap();
            int fallbackHits = CountHit3(fallbackService, snapshot, casesPath);
            Check("7 失败回落（不挂也不炸）",
                !fallbackService.HasSemanticIndex && fallbackHits == ExpectedLiteralHit3,
                "HasSemanticIndex=" + fallbackService.HasSemanticIndex + "，同一服务仍能回答 " + fallbackHits + "/24（字面值）");

            // ── 收尾 ────────────────────────────────────────────────────────────
            // 还原测试开关，避免"验台上改过的全局"被人误当成生产默认值。
            AwakeSemanticArmBootstrap.HostWaitAttemptsOverride = 0;
            AwakeSemanticArmBootstrap.HostWaitIntervalMillisecondsOverride = -1;
            AwakeSemanticArmBootstrap.ResetForTesting();
            AwakeRuntime.ResetStubHost();

            Console.WriteLine();
            Console.WriteLine("RESULT " + (_failures == 0 ? "PASS" : "FAIL") + " failed=" + _failures);
            return _failures == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL exception: " + ex);
            return 1;
        }
        finally
        {
            try { Directory.Delete(dataRoot, true); } catch (Exception) { }
        }
    }

    /// <summary>摆一个**真**宿主：框架自己的 `CreateDefaultHost` + 框架自己的会话协调器，
    /// RAG 那一档换成真的（接在本机 SQLite 上的那个后端）。</summary>
    private static IMarcusAiFrameworkHost BuildHost(IRagService rag, SessionRef session)
    {
        var overrides = new FrameworkServiceOverrides { Rag = rag };
        FrameworkHost host = FrameworkHost.CreateDefaultHost(null, overrides, "1.3.15");
        OperationResult<SessionLease> lease = host.Sessions.BeginSession(session);
        if (!lease.IsSuccess) throw new InvalidOperationException("session_begin_failed:" + lease.Error?.Code);
        AwakeRuntime.StubFullHost = host;
        return host;
    }

    /// <summary>后台任务是被 `AwakeBackgroundTask` 起的；验台里它是同步 invoke，等最后那个 Task 就行。</summary>
    private static void WaitForBootstrap()
    {
        Task task = AwakeBackgroundTask.LastTask;
        if (task == null) return;
        if (!task.Wait(TimeSpan.FromMinutes(5)))
        {
            throw new TimeoutException("挂载器后台任务超时未结束");
        }
    }

    private static int CountHit3(WorldKnowledgeQueryService service, WorldKnowledgeSnapshot snapshot, string casesPath)
    {
        JArray cases = (JObject.Parse(File.ReadAllText(casesPath))["cases"] as JArray)
            ?? throw new InvalidOperationException("题集 JSON 里没有 cases 数组");
        int hits = 0;
        foreach (JToken token in cases)
        {
            bool countInGate = token["countInGate"] == null || (bool)token["countInGate"];
            if (!countInGate) continue;
            string target = (string)token["target"];
            string text = (string)token["query"];
            string identity = PickIdentity(snapshot, target);
            List<string> ids = RunQuery(service, text, identity);
            if (ids.Take(3).Any(x => string.Equals(x, target, StringComparison.Ordinal))) hits++;
        }
        return hits;
    }

    private static List<string> RunQuery(WorldKnowledgeQueryService service, string text, string identity)
    {
        return service.Query(new WorldbookQuery
        {
            IdentityId = identity,
            KnowledgeScope = "private",
            KnowledgeScopeAvailable = true,
            EffectiveDetail = "secret",
            EffectiveDetailAvailable = true,
            RequestedDetail = "secret",
            PlayerText = text,
            MaximumBytes = 1 << 20
        }).HitIds;
    }

    // 与另两个验台**同源**：身份取目标条目自己授权里的第一个，保证"目标可被吐出来"，
    // 否则测到的是权限而不是检索。
    private static string PickIdentity(WorldKnowledgeSnapshot snapshot, string targetId)
    {
        WorldKnowledgeEntry entry;
        if (!snapshot.Entries.TryGetValue(targetId, out entry)) throw new InvalidOperationException("题集里的目标条目不在包里: " + targetId);
        List<string> identities = entry.Expressions
            .Where(x => x.Enabled)
            .SelectMany(x => x.Grants)
            .Select(g => g.IdentityId)
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.EndsWith(":public", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        if (identities.Count == 0) throw new InvalidOperationException("目标条目没有任何可用身份授权: " + targetId);
        return identities[0];
    }

    private static double Dot(float[] left, float[] right)
    {
        double sum = 0d;
        for (int index = 0; index < left.Length; index++) sum += left[index] * right[index];
        return sum;
    }

    private static void Check(string name, bool ok, string detail)
    {
        Console.WriteLine((ok ? "CHECK PASS " : "CHECK FAIL ") + name + " :: " + detail);
        if (!ok) _failures++;
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "ModuleData")) && Directory.Exists(Path.Combine(directory.FullName, "framework")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("找不到仓库根（应有 ModuleData/ 与 framework/）");
    }

    /// <summary>数一数"入库被调了几次"。幂等与换战役两条判据靠它，
    /// 不能靠日志 —— 日志是给人看的，不是判据。</summary>
    private sealed class CountingRagService : IRagService
    {
        private readonly IRagService _inner;
        internal int IngestCalls;

        internal CountingRagService(IRagService inner) { _inner = inner; }

        public Task<OperationResult<int>> IngestAsync(RagIngestRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref IngestCalls);
            return _inner.IngestAsync(request, context, cancellationToken);
        }

        public Task<OperationResult<IReadOnlyList<RagHit>>> SearchAsync(RagSearchRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            return _inner.SearchAsync(request, context, cancellationToken);
        }
    }
}
