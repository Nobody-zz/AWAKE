using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Awake;
using MarcusAwakeFramework.Api;
using MarcusAwakeStorage;
using Newtonsoft.Json.Linq;

// 「把模型接上去」到底多拿到几条 —— 真代码验台（2026-09-17）。
//
// 与门禁验台 `worldbook-runtime-smoke` 的关系：
//   · 那个跑的是**字面臂**（不加载模型），数字 16/26 是它的责任，本验台会复算一遍做对照；
//   · 本验台跑的是**合并后**的结果，用的是真件：真 ONNX 嵌入器（`OnnxSentenceEmbedder`）
//     ＋ 真 SQLite/RAG 后端（`SqliteStorageAndRagBackend`）＋ 真融合（`WorldKnowledgeRankFusion`）
//     ＋ 真接服务那一层（`AwakeWorldKnowledgeSemanticIndex`，同步等异步、带超时、失败即空表）。
//   · 两条臂的**题集只有一份**：tools/_retrieval_cases_20260916.json。
//
// 判据（缺一不可）：
//   A 阳性对照  自比余弦 = 1、不同句向量不同（防「静默退化成常量向量」）
//   B 字面臂不掉  未挂语义时的 hit@1 必须与门禁基线一致（A 9 / B 7 / 合计 16）
//   C 合并有涨   A 组合并后 >= 9，合计 > 16
//   D 阴性干净   挂上语义后，「今天午饭吃什么」这类候选数 <= 3（门槛 0.45 是否生效）
//
// 运行：
//   dotnet run -c Release --project AWAKE/tools/worldbook-rag-merge [模型目录] [词表路径]
internal static class Program
{
    private const string DefaultModelDirectory = @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\ONNX";
    private const string DefaultVocabularyPath = @"D:\AWAKE-Dev\AWAKE\tools\_af_tokenizer_20260916\vocab.txt";

    // 门禁基线（与 `RetrievalProbeCases.cs` 里的 Gate* 同值；对不上就是字面臂被改动了）
    // ⚠️ 2026-09-17：分母 26 → 24（删掉一条题不可判的废题「哪座城堡底下管着两个村子？」，
    //    全库 67 座城堡都写「两村」无唯一答案；另有「这一带有好马吗？」标了 countInGate=false）。
    //    **门槛值一个没动** —— 删掉的那条在两臂、合并三处都是 0，分子不动、分母缩小。
    //    ⇒ 现在的 B 是 7/11、合计 16/24，**别拿 7/13 / 9/26 跟它比**。
    private const int GateAHit1 = 9;
    private const int GateBHit1 = 7;
    private const int GateAllHit1 = 16;
    private const int NegativeMaxCandidates = 3;

    // 与 `RetrievalProbeCases.cs` **逐字一致**（改一处要两处都改）
    private static readonly string[] NegativeQueries =
    {
        "今天午饭吃什么",
        "帮我写一段排序代码",
        "火车时刻表在哪儿查"
    };
    private static readonly string[] FallbackOvermatchProbes =
    {
        "村民的一亩地归谁",
        "帝国的一座城"
    };

    private const int SemanticArmLimit = 3;      // = WorldKnowledgeQueryService.SemanticCandidateLimit
    private const int MergedOvermatchMax = 5 + SemanticArmLimit;

    internal static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 1 && args[0] == "--help")
        {
            Console.WriteLine("用法: worldbook-rag-merge [模型目录] [词表路径]");
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

        string dataRoot = Path.Combine(Path.GetTempPath(), "awake-rag-merge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        int failures = 0;
        try
        {
            var embedderOptions = new OnnxEmbedderOptions
            {
                ModelDirectory = modelDirectory,
                VocabularyPath = vocabularyPath,
                ModelId = "bge-small-zh-v1.5|" + WorldKnowledgePassage.Revision,
            };

            using var embedder = new OnnxSentenceEmbedder(embedderOptions);
            using var backend = new SqliteStorageAndRagBackend(Path.Combine(dataRoot, "rag-merge.db"), null, embedder);

            // ── A 阳性对照：嵌入器自己得是活的 ────────────────────────────────────
            string[] probes = { "圆顶锅盔是什么", "圆顶锅盔多少钱", "斯特拉吉亚的军队怎么打仗", "怎么做面包", "今天天气不错" };
            float[][] vectors = embedder.Encode(probes, System.Threading.CancellationToken.None);
            int distinct = vectors.Select(v => string.Join(",", v.Select(x => x.ToString("F6", CultureInfo.InvariantCulture)))).Distinct().Count();
            bool degenerate = vectors.Any(v => v.All(x => x == 0f) || v.Any(float.IsNaN));
            double self = Dot(vectors[0], vectors[0]);
            bool positiveControl = distinct == probes.Length && !degenerate && Math.Abs(self - 1d) < 1e-4;
            Console.WriteLine("[A] 维度 " + embedder.Dimension + "，不同向量 " + distinct + "/" + probes.Length
                + "，自比 " + self.ToString("F6", CultureInfo.InvariantCulture) + "  => " + (positiveControl ? "PASS" : "FAIL"));
            if (!positiveControl) failures++;

            // ── 入库：448 条按拼法 D ──────────────────────────────────────────────
            var entries = snapshot.Entries.Values.ToList();
            foreach (WorldKnowledgeEntry entry in entries)
            {
                if (!snapshot.Entries.ContainsKey(entry.Id)) throw new InvalidOperationException("entry id mismatch");
            }
            var ingestSample = entries.Take(1).Select(WorldKnowledgePassage.Compose).First();
            Console.WriteLine("[入库] 拼法 " + WorldKnowledgePassage.Revision + "，示例：" + Shorten(ingestSample, 60));

            SessionRef session = new SessionRef("campaign-rag-merge", "timeline-rag-merge", "session-rag-merge");
            var coordinator = new SessionCoordinator();
            var lease = coordinator.BeginSession(session);
            if (!lease.IsSuccess) { Console.Error.WriteLine("FAIL session_start_failed"); return 1; }
            var context = new RequestContext(new ExtensionId("rag-merge-harness"), lease.Value, "rag-merge-correlation", DateTimeOffset.UtcNow.AddHours(1));

            var semantic = new AwakeWorldKnowledgeSemanticIndex(
                backend, context, AwakeWorldKnowledgeSemanticIndex.ComposeFingerprint(snapshot.PackageId, snapshot.Version));
            Console.WriteLine("[入库] 语料指纹 = " + semantic.CorpusFingerprint);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool ingested = semantic.TryIngest(entries, out int ingestedCount, out string ingestError);
            stopwatch.Stop();
            Console.WriteLine("[入库] " + (ingested ? "OK" : "FAIL " + ingestError) + "，写入 " + ingestedCount + " 条，用时 "
                + stopwatch.ElapsedMilliseconds + " ms");
            if (!ingested) failures++;

            var literalOnly = new WorldKnowledgeQueryService(snapshot);
            var merged = new WorldKnowledgeQueryService(snapshot);
            merged.AttachSemanticIndex(semantic);

            // ── 题目 ──────────────────────────────────────────────────────────────
            JArray cases = (JObject.Parse(File.ReadAllText(casesPath))["cases"] as JArray)
                ?? throw new InvalidOperationException("题集 JSON 里没有 cases 数组");
            Console.WriteLine();
            Console.WriteLine("──────────────────────────────────────────────────────────────────────────────");
            Console.WriteLine("组  问法                            目标                                  字面  语义  合并  上限  池  涨");

            int litA = 0, litB = 0, semA = 0, semB = 0, merA = 0, merB = 0, oracle = 0, total = 0, grew = 0;
            int nA = 0, nB = 0;          // 分组分母（09-17 加：以前这两个数在打印里写死成 /13）
            int observed = 0;            // countInGate=false 的题：仍跑、仍打印，不进分子分母
            // hit@3 / hit@5（09-17 深夜加）：下游是把**前几条一起**拼给模型的（`MaximumRetrievedBlockBytes`），
            // 不是只取第 1 条 ⇒ **hit@1 会夸大问题**，必须同时看"进没进前几条"。
            int lit3 = 0, sem3 = 0, mer3 = 0, lit5 = 0, sem5 = 0, mer5 = 0;
            // 对照用（**不是上线规则**）：把两条臂的输出按「谁优先」简单串起来，看这两个极端各值几条。
            // 上线的是 WorldKnowledgeRankFusion（对称 RRF）。这两行只是给决策留数，别当成实现。
            int semanticFirstHit1 = 0, literalFirstHit1 = 0, semanticFirstLost = 0, literalFirstGained = 0;
            var lostByMerge = new List<string>();
            var gainedByMerge = new List<string>();
            var semanticOnly = new List<string>();
            var literalOnlySaved = new List<string>();
            // 观察项（countInGate=false）：进不了分子分母，但要在报告里留一行，不能凭空消失。
            var observationRows = new List<string>();
            var stillMissed = new List<string>();
            var semanticFirstLostQueries = new List<string>();
            // 诊断（仅 AWAKE_MERGE_DIAG=1 时打印，2026-09-17 加）：
            // 合并把「语义臂本来能排第 1」的条目挤掉了 —— 那挤上来的是谁？
            // 是**同主题的兄弟条目**（≈答得偏，还能看）还是**不相干条目**（＝真答错）？
            // 这一步决定「合并丢的那几条」算不算真短板，不能只看总数。
            var semanticWinsLostToMerge = new List<string>();
            // 诊断之二（同一开关）：**合并也拿不到**的那几条，语义臂到底排到哪儿了？
            // 用来分开"根本找不到"和"排在后面挤不上来"——两者的修法完全不同。
            var missedRankDiag = new List<string>();

            foreach (JToken token in cases)
            {
                string group = (string)token["group"];
                string target = (string)token["target"];
                string text = (string)token["query"];
                // 题集里可以标 countInGate=false（09-17 加，见题集 JSON 的 note）：判不出通道好坏的题
                // （靶子任意／多个答案都对）不进分子分母，但仍跑、仍打印，留着当记录。
                bool countInGate = token["countInGate"] == null || (bool)token["countInGate"];
                string identity = PickIdentity(snapshot, target);

                List<string> litHits = RunQuery(literalOnly, text, identity);
                List<string> merHits = RunQuery(merged, text, identity);
                IReadOnlyList<string> semTop = semantic.Search(text, SemanticArmLimit);

                bool litH1 = litHits.Count > 0 && litHits[0] == target;
                bool semH1 = semTop.Count > 0 && semTop[0] == target;
                bool merH1 = merHits.Count > 0 && merHits[0] == target;
                bool merH3 = merHits.Take(3).Any(x => x == target);
                IReadOnlyList<string> semTop5 = semantic.Search(text, 5);
                bool litH3 = litHits.Take(3).Any(x => x == target);
                bool merH5 = merHits.Take(5).Any(x => x == target);
                bool semH3 = semTop5.Take(3).Any(x => x == target);
                bool semH5 = semTop5.Any(x => x == target);
                if (countInGate)
                {
                    lit3 += litH3 ? 1 : 0; sem3 += semH3 ? 1 : 0; mer3 += merH3 ? 1 : 0;
                    lit5 += litHits.Take(5).Any(x => x == target) ? 1 : 0;
                    sem5 += semH5 ? 1 : 0;
                    mer5 += merH5 ? 1 : 0;

                    total++;
                    if (group == "A") { nA++; litA += litH1 ? 1 : 0; semA += semH1 ? 1 : 0; merA += merH1 ? 1 : 0; }
                    else { nB++; litB += litH1 ? 1 : 0; semB += semH1 ? 1 : 0; merB += merH1 ? 1 : 0; }
                    oracle += (litH1 || semH1) ? 1 : 0;
                    if (merH1 && !litH1) { grew++; gainedByMerge.Add(text); }
                    if (!merH1 && litH1) lostByMerge.Add(text);
                    if (semH1 && !litH1) semanticOnly.Add(text);
                    if (litH1 && !semH1) literalOnlySaved.Add(text);
                    if (!merH3 && !semTop.Contains(target)) stillMissed.Add(text);
                    if (!merH3 && !semTop.Contains(target))
                    {
                        IReadOnlyList<string> wide = semantic.Search(text, 8);
                        missedRankDiag.Add(text
                            + " ⇒ 语义前 8：" + Join(wide.Select(x => Describe(snapshot, x)).ToList())
                            + " ｜ 字面前 8：" + Join(litHits.Take(8).Select(x => Describe(snapshot, x)).ToList())
                            + " ｜ 目标=" + Describe(snapshot, target));
                    }
                    if (semH1 && !merH1)
                    {
                        semanticWinsLostToMerge.Add(group + " " + text
                            + " ⇒ 合并#1=" + Describe(snapshot, merHits.Count > 0 ? merHits[0] : null)
                            + "；字面#1=" + Describe(snapshot, litHits.Count > 0 ? litHits[0] : null)
                            + "；目标=" + Describe(snapshot, target)
                            + "\n      字面前 8：" + Join(litHits.Take(8).Select(x => Describe(snapshot, x)).ToList())
                            + "\n      语义前 8：" + Join(semantic.Search(text, 8).Select(x => Describe(snapshot, x)).ToList()));
                    }

                    List<string> orderSemanticFirst = semTop.Concat(litHits.Where(x => !semTop.Contains(x))).ToList();
                    List<string> orderLiteralFirst = litHits.Concat(semTop.Where(x => !litHits.Contains(x))).ToList();
                    bool sfH1 = orderSemanticFirst.Count > 0 && orderSemanticFirst[0] == target;
                    bool lfH1 = orderLiteralFirst.Count > 0 && orderLiteralFirst[0] == target;
                    semanticFirstHit1 += sfH1 ? 1 : 0;
                    literalFirstHit1 += lfH1 ? 1 : 0;
                    semanticFirstLost += (litH1 && !sfH1) ? 1 : 0;
                    if (litH1 && !sfH1) semanticFirstLostQueries.Add(text);
                    literalFirstGained += (lfH1 && !litH1) ? 1 : 0;
                }
                else
                {
                    observed++;
                    observationRows.Add("  " + group + " " + text + "（目标 " + target.Split(':').Last() + "）"
                        + " ⇒ 字面" + (litH1 ? "1" : "0") + " 语义" + (semH1 ? "1" : "0") + " 合并" + (merH1 ? "1" : "0")
                        + "（三臂都拿不到第 1；这条不计入门禁，原因见题集 JSON 的 note）");
                }

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-3} {1,-30} {2,-40} {3,-6} {4,-6} {5,-6} {6,-6} {7,-6} {8,-4}",
                    group, text, target.Split(':').Last(),
                    litH1 ? "1" : "0", semH1 ? "1" : "0", merH1 ? "1" : "0",
                    (litH1 || semH1) ? "1" : "0", merHits.Count, (merH1 && !litH1) ? "+" : (litH1 && !merH1 ? "-" : "")));
            }

            Console.WriteLine();
            Console.WriteLine("字面臂（未挂语义）  A_hit1=" + litA + "/" + nA + "  B_hit1=" + litB + "/" + nB + "  合计=" + (litA + litB) + "/" + total);
            Console.WriteLine("语义臂（单独）      A_hit1=" + semA + "/" + nA + "  B_hit1=" + semB + "/" + nB + "  合计=" + (semA + semB) + "/" + total);
            Console.WriteLine("合并后（真融合）    A_hit1=" + merA + "/" + nA + "  B_hit1=" + merB + "/" + nB + "  合计=" + (merA + merB) + "/" + total);
            Console.WriteLine("上限（任一臂即算）  合计=" + oracle + "/" + total + "  ← 两条臂能力的上界，融合只能无限接近、不能超过");
            if (observed > 0)
            {
                Console.WriteLine();
                Console.WriteLine("⚠️ 分母是 " + total + " 条，不是 26 条（09-17 删掉一条题不可判的废题；另有 " + observed + " 条标了不计入门禁）");
                foreach (string row in observationRows) Console.WriteLine(row);
            }
            Console.WriteLine();
            Console.WriteLine("── 换一个口径：进没进「前 3 / 前 5」（下游是把前几条一起拼给模型的，不是只取第 1 条）──");
            Console.WriteLine("                 hit@1   hit@3   hit@5");
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  字面臂        {0,5}   {1,5}   {2,5}", litA + litB, lit3, lit5));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  语义臂        {0,5}   {1,5}   {2,5}", semA + semB, sem3, sem5));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  ★ 合并（上线）{0,5}   {1,5}   {2,5}", merA + merB, mer3, mer5));

            Console.WriteLine();
            Console.WriteLine("── 对照：两个极端规则各值多少（**都不是上线规则**）──────────────");
            Console.WriteLine("字面优先（字面原序 + 语义补尾）        合计=" + literalFirstHit1 + "/" + total
                + "，较字面涨 " + literalFirstGained + " 跌 0");
            Console.WriteLine("语义优先（语义原序 + 字面补尾）        合计=" + semanticFirstHit1 + "/" + total
                + "，较字面涨 " + (semanticFirstHit1 - (litA + litB) + semanticFirstLost) + " 跌 " + semanticFirstLost
                + "（跌的就是那几条：" + Join(semanticFirstLostQueries) + "）");
            Console.WriteLine("★ 上线：对称 RRF（k=" + WorldKnowledgeRankFusion.RankConstant + "）          合计="
                + (merA + merB) + "/" + total + "，较字面涨 " + gainedByMerge.Count + " 跌 " + lostByMerge.Count);

            Console.WriteLine();
            Console.WriteLine("只有语义能拿的 " + semanticOnly.Count + " 条：" + Join(semanticOnly));
            Console.WriteLine("只有字面能拿的 " + literalOnlySaved.Count + " 条：" + Join(literalOnlySaved));
            Console.WriteLine("合并仍拿不到的 " + stillMissed.Count + " 条：" + Join(stillMissed));
            Console.WriteLine("合并比字面多拿 " + gainedByMerge.Count + " 条：" + Join(gainedByMerge));
            Console.WriteLine("合并比字面少拿 " + lostByMerge.Count + " 条：" + (lostByMerge.Count == 0 ? "（无）" : Join(lostByMerge)));

            // ── 诊断（默认不打印，AWAKE_MERGE_DIAG=1 才打）──────────────────────────
            if (Environment.GetEnvironmentVariable("AWAKE_MERGE_DIAG") == "1")
            {
                Console.WriteLine();
                Console.WriteLine("── 诊断：语义臂能排第 1、合并后被挤掉的（" + semanticWinsLostToMerge.Count + " 条）"
                    + " —— 看挤上来的是同主题兄弟条目还是不相干条目");
                foreach (string line in semanticWinsLostToMerge) Console.WriteLine("  " + line);
                Console.WriteLine();
                Console.WriteLine("── 诊断：合并也拿不到的那几条，两条臂各自排到哪儿了（" + missedRankDiag.Count + " 条）");
                foreach (string line in missedRankDiag) Console.WriteLine("  " + line);

                // 诊断之三：**问句改写探针**（09-17 深夜加）。
                // 目的：分清语义臂"拿不到"是 (a) 词面对不上，还是 (b) 高频实体词把结果拉走。
                // 每组三种问法：原句 / 去掉高频实体词 / 换成语料里实际用的词。
                // 判读：换语料用词就命中 ⇒ 词面问题是主因；去实体词就命中 ⇒ 高频实体词是主因。
                Console.WriteLine();
                Console.WriteLine("── 诊断：问句改写探针（看语义臂前 5 名怎么变）");
                // ⚠️ 09-17：原来那三行「城堡」探针随废题一起删了（题都删了，留探针等于留尸检报告）。
                //    那一轮的结果已记在 docs/DECISION-20260917-两条通道怎么合.md §7.7 的表里。
                string[][] rewriteProbes =
                {
                    new[] { "好马·原句", "这一带有好马吗？" },
                    new[] { "好马·去实体词", "听说这里出产最好的马？" },
                    new[] { "好马·换语料词", "这一带放养着阿塞莱最好的马匹吗？" },
                    new[] { "禁地·原句", "山里有不让进的地方吗？" },
                    new[] { "禁地·去实体词", "有不让进的地方吗？" },
                    new[] { "禁地·换语料词", "有被当作禁地的地方吗？" },
                };
                foreach (string[] probe in rewriteProbes)
                {
                    IReadOnlyList<string> top = semantic.Search(probe[1], 5);
                    Console.WriteLine("  [" + probe[0] + "] " + probe[1] + " ⇒ "
                        + Join(top.Select(x => Describe(snapshot, x)).ToList()));
                }

                // 诊断之四：**分类（域／类型）能不能当筛选器**（09-17 深夜加，甲方点题）。
                // 域 ＝ id 第 3 段里 '.' 之前（geography／economy／politics／culture／war）
                // 类型 ＝ '.' 之后、'-' 之前（villages／castles／items／goods／military／tales／…）
                // 两路都测：oracle（直接给定目标所在域，只看上限）与自动选域（不许偷看答案）。
                Console.WriteLine();
                var domainCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                var subtypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string entryId in snapshot.Entries.Keys)
                {
                    string d = DomainOf(entryId), s = SubtypeOf(entryId);
                    domainCounts[d] = domainCounts.TryGetValue(d, out int dc) ? dc + 1 : 1;
                    subtypeCounts[s] = subtypeCounts.TryGetValue(s, out int sc) ? sc + 1 : 1;
                }
                Console.WriteLine("  全库分类：域 " + domainCounts.Count + " 个（"
                    + string.Join(" / ", domainCounts.OrderByDescending(x => x.Value).Select(x => x.Key + " " + x.Value))
                    + "），类型 " + subtypeCounts.Count + " 个");
                int dOracle1 = 0, dOracle3 = 0, sOracle1 = 0, dAuto1 = 0, dCases = 0, dInPool = 0;
                var dNotInPool = new List<string>();
                foreach (JToken token in cases)
                {
                    // 与门禁同分母（09-17）：countInGate=false 的题（靶子任意）不进这张表，
                    // 否则报告里会出现两个分母，读的人无从对照。
                    if (token["countInGate"] != null && !(bool)token["countInGate"]) continue;
                    string target = (string)token["target"];
                    string text = (string)token["query"];
                    IReadOnlyList<string> wide = semantic.Search(text, 20);
                    string tDom = DomainOf(target), tSub = SubtypeOf(target);
                    dCases++;
                    if (wide.Contains(target)) dInPool++; else dNotInPool.Add(text);
                    string inDomain = wide.FirstOrDefault(x => DomainOf(x) == tDom);
                    string inSubtype = wide.FirstOrDefault(x => SubtypeOf(x) == tSub);
                    if (inDomain == target) dOracle1++;
                    if (wide.Take(20).Where(x => DomainOf(x) == tDom).Take(3).Contains(target)) dOracle3++;
                    if (inSubtype == target) sOracle1++;
                    // 自动选域：取前 20 里成员最多的域；并列则取"该域名次最靠前"的那个
                    string autoDom = wide
                        .Select((x, i) => new { Id = x, Rank = i, Dom = DomainOf(x) })
                        .GroupBy(x => x.Dom)
                        .OrderByDescending(g => g.Count())
                        .ThenBy(g => g.Min(x => x.Rank))
                        .Select(g => g.Key)
                        .FirstOrDefault();
                    if (!string.IsNullOrEmpty(autoDom) && wide.FirstOrDefault(x => DomainOf(x) == autoDom) == target) dAuto1++;
                }
                Console.WriteLine("  分类当筛选器（" + dCases + " 条，语义臂前 20 为池）：");
                Console.WriteLine("    ★ 目标『在语义前 20 里』     ⇒ " + dInPool + "/" + dCases
                    + "  ← **任何「重排／筛选」做法的天花板**（不在池里，重排也救不回来）");
                Console.WriteLine("    oracle 限定到目标的『域』     ⇒ 命中 " + dOracle1 + "/" + dCases
                    + "（域内前 3：" + dOracle3 + "/" + dCases + "）");
                Console.WriteLine("    oracle 限定到目标的『类型』   ⇒ 命中 " + sOracle1 + "/" + dCases);
                Console.WriteLine("    自动选域（不许偷看答案）      ⇒ 命中 " + dAuto1 + "/" + dCases);
                Console.WriteLine("    ⚠️ 目标连前 20 都没进的（重排救不了，只能靠换说法／换编码）："
                    + Join(dNotInPool));
            }

            // ── D 阴性对照 / 过匹配（**挂上语义之后**才有意义）─────────────────────
            Console.WriteLine();
            int negativeWorst = 0;
            foreach (string text in NegativeQueries)
            {
                int candidateCount = RunQuery(merged, text, "awake:identity:commoner").Count;
                IReadOnlyList<string> semTop = semantic.Search(text, SemanticArmLimit);
                negativeWorst = Math.Max(negativeWorst, candidateCount);
                Console.WriteLine("阴性 候选=" + candidateCount + " 语义臂=" + semTop.Count + " q=" + text);
            }
            bool negativeClean = negativeWorst <= NegativeMaxCandidates;
            Console.WriteLine("NEGATIVE_WORST candidates=" + negativeWorst + " max=" + NegativeMaxCandidates + " "
                + (negativeClean ? "OK" : "OVERMATCH"));

            int overmatchWorst = 0;
            foreach (string text in FallbackOvermatchProbes)
            {
                int candidateCount = RunQuery(merged, text, "awake:identity:commoner").Count;
                IReadOnlyList<string> semTop = semantic.Search(text, SemanticArmLimit);
                overmatchWorst = Math.Max(overmatchWorst, candidateCount);
                Console.WriteLine("笼统 候选=" + candidateCount + " 语义臂=" + semTop.Count + " q=" + text);
            }
            bool overmatchBounded = overmatchWorst <= MergedOvermatchMax;
            Console.WriteLine("OVERMATCH_WORST candidates=" + overmatchWorst + " max=" + MergedOvermatchMax + " "
                + (overmatchBounded ? "OK" : "FLOOD"));

            // ── 判据汇总 ─────────────────────────────────────────────────────────
            bool literalUnchanged = litA >= GateAHit1 && litB >= GateBHit1 && (litA + litB) >= GateAllHit1;
            bool improved = merA >= GateAHit1 && (merA + merB) > (litA + litB) && lostByMerge.Count == 0;
            Console.WriteLine();
            Console.WriteLine("JUDGE positive_control=" + (positiveControl ? "PASS" : "FAIL")
                + " literal_unchanged=" + (literalUnchanged ? "PASS" : "FAIL")
                + " merged_improves=" + (improved ? "PASS" : "FAIL")
                + " negative_clean=" + (negativeClean ? "PASS" : "FAIL")
                + " overmatch_bounded=" + (overmatchBounded ? "PASS" : "FAIL"));
            bool gate = positiveControl && literalUnchanged && improved && negativeClean && overmatchBounded && failures == 0;
            Console.WriteLine("MERGE_GATE " + (gate ? "PASS" : "FAIL")
                + " need literal A>=" + GateAHit1 + " B>=" + GateBHit1 + " ALL>=" + GateAllHit1
                + " merged A>=" + GateAHit1 + " ALL>" + GateAllHit1 + " losses=0"
                + " negative<=" + NegativeMaxCandidates + " overmatch<=" + MergedOvermatchMax);
            return gate ? 0 : 1;
        }
        finally
        {
            try { Directory.Delete(dataRoot, true); } catch (Exception) { }
        }
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

    // 与门禁验台 `RetrievalProbeCases.PickIdentity` 同源：身份取目标条目自己授权里的第一个，
    // 保证「目标可被吐出来」，否则测到的是权限而不是检索。
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

    // 分类取自条目 id 的形状：`awake:entry:<域>.<类型>-<名字>`
    // （例：awake:entry:geography.villages-lamesa ⇒ 域 geography、类型 villages）
    private static string DomainOf(string id)
    {
        string[] parts = id.Split(':');
        if (parts.Length < 3) return "(?)";
        int dot = parts[2].IndexOf('.');
        return dot < 0 ? parts[2] : parts[2].Substring(0, dot);
    }

    private static string SubtypeOf(string id)
    {
        string[] parts = id.Split(':');
        if (parts.Length < 3) return "(?)";
        int dot = parts[2].IndexOf('.');
        if (dot < 0) return "(?)";
        string rest = parts[2].Substring(dot + 1);
        int dash = rest.IndexOf('-');
        return dash < 0 ? rest : rest.Substring(0, dash);
    }

    // 诊断用：把条目 id 打成人能读的「标题(id 尾段)」；找不到就只打 id。
    private static string Describe(WorldKnowledgeSnapshot snapshot, string id)
    {
        if (string.IsNullOrEmpty(id)) return "(空)";
        WorldKnowledgeEntry entry;
        if (!snapshot.Entries.TryGetValue(id, out entry)) return id;
        return (string.IsNullOrEmpty(entry.Title) ? "(无标题)" : entry.Title) + "(" + id.Split(':').Last() + ")";
    }

    private static double Dot(float[] left, float[] right)
    {
        double sum = 0;
        for (int index = 0; index < left.Length; index++) sum += (double)left[index] * right[index];
        return sum;
    }

    private static string Join(List<string> values)
    {
        return values.Count == 0 ? "（无）" : string.Join(" / ", values);
    }

    private static string Shorten(string value, int maximum)
    {
        if (string.IsNullOrEmpty(value)) return "(空)";
        return value.Length <= maximum ? value : value.Substring(0, maximum) + "…";
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
}
