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
    // ★★ 2026-09-17 口径改了：**从「排第 1」改成「进前 3」**（甲方拍板「可以」）。
    //    缘由：下游是把**前几条一起**拼给模型的，卡第 1 名会把"其实找得到"的判成找不到。
    //    实测（24 条，hit@3）：字面 A 10/13、B 9/11、合计 19/24；合并 A 13/13、合计 23/24。
    //    ⇒ 下面三个常量是**字面臂**的 hit@3 门槛；合并臂另有两条判据（见 `improved`）。
    //    ⚠️ 改这些数＝改验收标准，必须同时改文档（显式决策）。
    private const int GateAHit3 = 10;
    private const int GateBHit3 = 9;
    private const int GateAllHit3 = 19;
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
    // 流水线式对照要扫的「语义召回闸宽度」。产品那档是 3 —— 3 太窄，语义没召回到的字面命中会被整条掐死。
    private static readonly int[] PinpointRecallSweep = { 3, 5, 8, 12, 20 };
    private const int MergedOvermatchMax = 5 + SemanticArmLimit;

    // ── E「该空手时空手」（2026-09-17 加，甲方点题）───────────────────────────────
    // 上面 A~D 四条判据量的都是**候选/命中条数**，量不出「库里本就没这一条，却答得很自信」。
    // 09-17 那条（问「领主」吐马穆鲁克）就是从这条缝漏过去的：候选数没超上限，答案错了。
    // 这条判据换尺子：量的是**这一轮会不会把知识喂给模型** —— 直接读真策略
    // `WorldKnowledgeDecisionPolicy.BuildPromptBlock(...)` 是否为空（`NpcDialogueService` 就是照它
    // 决定往提示词里塞不塞世界书事实的），不在这里另写一套判据（平行实现必须同源）。
    // ⚠️ 2026-09-18 换的观测点：E 刚加时读的是 `Create(...).AllowsAi`；那天下午「知道」与「开口」
    //    被拆成了两根绳子，`not_found` 也会 AllowsAi=true ⇒ 继续读它会变成恒绿的假闸。
    // 题集：tools/_generic_question_cases_20260917.json（骨架在这，题面由内容侧填）。
    private const int NoAnswerMaxOffenders = 0;   // expect=empty 的题：一个都不许喂知识
    private const int ConceptMaxOffenders = 0;    // expect=concept 的题：必须喂，且排第一的是概念条
    // 已确认缺口（题集里 status=known_gap）：**仍然测量、仍然逐条打印**，只是不当场卡红。
    // 常量取实测值，**只许降不许升**（跟上面 GateBHit3「抬到实测值」同一条规矩）。
    // 2026-09-17 首次跑出来是 3 条，全部记在题集 JSON 的 basis 里：
    //   「领主」                        → commoner 之外的 soldier/noble/merchant 三种身份都答了马穆鲁克
    //   「这边的人怎么样」              → 答了 7 条，排第一是迪纳尔堡
    //   「附近有什么好东西」            → 答了 5 条，排第一是拉迈萨
    // ⚠️ 这三条的根因**不是余弦门槛定低了**（门槛是 0.45，见
    //    MarcusAwakeStorage/src/SqliteStorageAndRagBackend.RagSemantic.cs:44）：
    //    它们说的确实是这个世界的物事（城堡／村庄／人），余弦自然够高，跟真问题**叠在同一段分数里**
    //    ——「村民的一亩地归谁」0.530、「帝国的一座城」0.571（这两个是**过匹配探针**的数），
    //       而真问题**命中前 3** 的目标余弦是 **0.4708–0.7951**（rank≤3 的 sim 区间，
    //       见 tools/_semantic_floor_20260917.json；rank=1 是 0.4764–0.7951）。
    //       ⚠️ 这两批数**不是同一次跑**，别混着引用（红测抓到过）。
    //    没有空档可以放门槛：想挡住它们就得把真题一起砍（tools/_semantic_floor_20260917.json 的
    //    rows 表已量过——门槛要抬到 0.60 才不漏，真题只剩 12/26）。
    //    ⇒ 能不能分开，靠的不是分数，是**这句话有没有确定所指**。
    //      而这一步该落在**检索之前**（先把指代绑到当前聚落／把前文递进来），
    //      **不是**在检索层加规则，也不是"内容侧规则" —— 见
    //      docs/DESIGN-20260917-检索之前的判断层.md（09-17 追之三定的，本行按它更正）。
    private const int KnownGapMax = 3;

    // 诊断用对照（**不参与判据**，只在 AWAKE_NOANSWER_DIAG=1 时打印）：
    // 这几条是**真问题**里"同样整句、同样不带实体名"的那一类（24 题集的 B 组）。
    // 用途只有一个：看「跟命中条目共享几个 term、共享的是哪几个」这件事，
    // 能不能把病题（领主／这边的人怎么样／附近有什么好东西）和它们分开。
    // 分不开 ⇒ 就别再在检索层里找修法了，那不是信号的问题。
    private static readonly string[] NoAnswerContrastProbes =
    {
        "这一带有好马吗？",
        "山里有不让进的地方吗？",
        "什么人拿大圆盾扔飞斧？"
    };

    // 正面实验用（同样只在 AWAKE_NOANSWER_DIAG=1 时跑）：左＝原句，右＝把指代词换成"当前聚落"。
    // 聚落取 `castle_village_S1_2`＝哲米扬（它本身就是条目 geography.villages-zhemyan 的一个 keyword）。
    private static readonly string[][] DeixisResolveProbes =
    {
        new[] { "这边的人怎么样", "哲米扬的人怎么样" },
        new[] { "附近有什么好东西", "哲米扬附近有什么好东西" },
        new[] { "这边的人怎么样", "castle_village_S1_2 的人怎么样" }
    };

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
            // 分组 hit@3（09-17 加）：甲方 09-17 拍板把验收口径从「排第 1」改成「进前 3」，
            // 所以**门禁要按分组看 hit@3**，不能只看合计。
            int litA3 = 0, litB3 = 0, semA3 = 0, semB3 = 0, merA3 = 0, merB3 = 0;
            int lostByMerge3 = 0;
            // 对照用（**不是上线规则**）：把两条臂的输出按「谁优先」简单串起来，看这两个极端各值几条。
            // 上线的是 WorldKnowledgeRankFusion（对称 RRF）。这两行只是给决策留数，别当成实现。
            int semanticFirstHit1 = 0, literalFirstHit1 = 0, semanticFirstLost = 0, literalFirstGained = 0;
            // 流水线式（甲方 09-18 口径）：语义先召回、字面在集合内定位。同样**不是上线规则**，只留数。
            // 扫的旋钮＝**语义召回闸开多大**（产品那档只有 3）。
            int[] pinpointHit1 = new int[PinpointRecallSweep.Length];
            int[] pinpointHit3 = new int[PinpointRecallSweep.Length];
            int[] pinpointBypassHit1 = new int[PinpointRecallSweep.Length];
            int[] pinpointBypassHit3 = new int[PinpointRecallSweep.Length];
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
                    if (group == "A")
                    {
                        litA3 += litH3 ? 1 : 0; semA3 += semH3 ? 1 : 0; merA3 += merH3 ? 1 : 0;
                    }
                    else
                    {
                        litB3 += litH3 ? 1 : 0; semB3 += semH3 ? 1 : 0; merB3 += merH3 ? 1 : 0;
                    }
                    // 合并**在 hit@3 口径下**有没有把字面能拿的挤掉（门禁要的正是这一口径）。
                    if (!merH3 && litH3) lostByMerge3++;
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

                    // ── 流水线式（甲方 09-18 口径：「得先抓住大概的语义，再去按照字面的去找对应具体词条」）──
                    // 与上面两版**不同**：语义不是"排在前面"，而是**先当召回闸**（决定"大概在说哪一类"），
                    // 字面只在这个集合**内部**负责钉到具体条目。
                    // ⚠️ 定位顺序**直接借产品的字面名次**（`litHits`），不另算一套 —— 免得变成"另写一个引擎"。
                    // ★ 闸门开多大是关键旋钮：产品那档只有 3（`SemanticCandidateLimit`），
                    //   3 太窄 ⇒ 语义没召回到的字面命中会被整条掐死。所以这里**扫一遍宽度**。
                    for (int pinIndex = 0; pinIndex < PinpointRecallSweep.Length; pinIndex++)
                    {
                        int recallN = PinpointRecallSweep[pinIndex];
                        List<string> semRecall = semantic.Search(text, recallN).ToList();
                        List<string> orderPinpoint = semRecall
                            .OrderBy(x => { int i = litHits.IndexOf(x); return i < 0 ? int.MaxValue : i; })
                            .ThenBy(x => semRecall.IndexOf(x))
                            .ToList();
                        // 变体：**指名道姓时不让召回闸把入口关掉** —— 字面第 1 若不在语义集合里，前置。
                        List<string> orderPinpointBypass = new List<string>(orderPinpoint);
                        if (litHits.Count > 0 && !semRecall.Contains(litHits[0])) orderPinpointBypass.Insert(0, litHits[0]);

                        pinpointHit1[pinIndex] += (orderPinpoint.Count > 0 && orderPinpoint[0] == target) ? 1 : 0;
                        pinpointHit3[pinIndex] += orderPinpoint.Take(3).Any(x => x == target) ? 1 : 0;
                        pinpointBypassHit1[pinIndex] += (orderPinpointBypass.Count > 0 && orderPinpointBypass[0] == target) ? 1 : 0;
                        pinpointBypassHit3[pinIndex] += orderPinpointBypass.Take(3).Any(x => x == target) ? 1 : 0;
                    }
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
            Console.WriteLine("── ★ 验收口径（甲方 09-17 拍板）：**进前 3 就算过**；下面这行才是门禁看的那一行 ──");
            Console.WriteLine("                  hit@3 A组   hit@3 B组   hit@3 合计");
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  字面臂        {0,5}/{1,-4} {2,5}/{3,-4} {4,5}/{5,-4}", litA3, nA, litB3, nB, litA3 + litB3, total));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  语义臂        {0,5}/{1,-4} {2,5}/{3,-4} {4,5}/{5,-4}", semA3, nA, semB3, nB, semA3 + semB3, total));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  ★ 合并（上线）{0,5}/{1,-4} {2,5}/{3,-4} {4,5}/{5,-4}", merA3, nA, merB3, nB, merA3 + merB3, total));
            Console.WriteLine("  （hit@1 同口径供对照：字面 " + (litA + litB) + "/" + total
                + "、语义 " + (semA + semB) + "/" + total + "、合并 " + (merA + merB) + "/" + total
                + " —— **不再是门禁口径**，只留作退化预警）");
            Console.WriteLine("  合并 vs 字面 @hit@3：涨 " + ((merA3 + merB3) - (litA3 + litB3)) + " 条，跌 " + lostByMerge3 + " 条"
                + "（@hit@1 口径：涨 " + gainedByMerge.Count + " 跌 " + lostByMerge.Count + "）");

            Console.WriteLine();
            Console.WriteLine("── 对照：两个极端规则各值多少（**都不是上线规则**）──────────────");
            Console.WriteLine("字面优先（字面原序 + 语义补尾）        合计=" + literalFirstHit1 + "/" + total
                + "，较字面涨 " + literalFirstGained + " 跌 0");
            Console.WriteLine("语义优先（语义原序 + 字面补尾）        合计=" + semanticFirstHit1 + "/" + total
                + "，较字面涨 " + (semanticFirstHit1 - (litA + litB) + semanticFirstLost) + " 跌 " + semanticFirstLost
                + "（跌的就是那几条：" + Join(semanticFirstLostQueries) + "）");
            Console.WriteLine("★ 上线：对称 RRF（k=" + WorldKnowledgeRankFusion.RankConstant + "）          合计="
                + (merA + merB) + "/" + total + "，较字面涨 " + gainedByMerge.Count + " 跌 " + lostByMerge.Count);

            // 流水线式（甲方 09-18 口径）——这是**第三种形状**，不是"谁优先"。
            // 判据同门禁口径：@hit3 为准（@hit1 一并给）。旋钮＝语义召回闸开多大。
            Console.WriteLine();
            Console.WriteLine("── 流水线式（语义先召回 → 字面在集合内定位；甲方 09-18 口径，**都不是上线规则**）──");
            Console.WriteLine("   闸宽  语义召回闸+字面定位        指名道姓不被闸拦（变体）");
            Console.WriteLine("          @hit3   @hit1              @hit3   @hit1");
            for (int i = 0; i < PinpointRecallSweep.Length; i++)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "   {0,4}   {1,5}/{2,-4} {3,5}/{2,-4}      {4,5}/{2,-4} {5,5}/{2,-4}",
                    PinpointRecallSweep[i],
                    pinpointHit3[i], total, pinpointHit1[i],
                    pinpointBypassHit3[i], pinpointBypassHit1[i]));
            }
            Console.WriteLine("   ★ 对照：上线（对称 RRF）      @hit3=" + (merA3 + merB3) + "/" + total
                + "  @hit1=" + (merA + merB) + "/" + total
                + "；字面臂 @hit3=" + (litA3 + litB3) + " @hit1=" + (litA + litB)
                + "；语义臂 @hit3=" + (semA3 + semB3) + " @hit1=" + (semA + semB));
            Console.WriteLine("  （定位顺序直接借产品字面名次，不另算；闸宽 3 = 与产品同一档 `SemanticCandidateLimit`）");

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

            // ── E 该空手时空手（★ 09-17 新增，见上方常量注释）────────────────────────
            int noAnswerOffenders, conceptOffenders, knownGapHits;
            bool rulerCasesLoaded = RunNoAnswerRuler(merged, snapshot, repositoryRoot,
                out noAnswerOffenders, out conceptOffenders, out knownGapHits);
            if (!rulerCasesLoaded) failures++;
            bool noAnswerClean = noAnswerOffenders <= NoAnswerMaxOffenders;
            bool conceptClean = conceptOffenders <= ConceptMaxOffenders;
            bool knownGapsBounded = knownGapHits <= KnownGapMax;
            Console.WriteLine("NOANSWER_RULER offenders=" + noAnswerOffenders + "/" + NoAnswerMaxOffenders
                + " concept_offenders=" + conceptOffenders + "/" + ConceptMaxOffenders
                + " known_gaps=" + knownGapHits + "/" + KnownGapMax
                + " " + (noAnswerClean && conceptClean && knownGapsBounded ? "OK" : "HARD_ANSWER"));

            // ── 判据汇总（★ 09-17 起全部按 **hit@3** 口径，与甲方拍板的验收线一致）────────
            bool literalUnchanged = litA3 >= GateAHit3 && litB3 >= GateBHit3 && (litA3 + litB3) >= GateAllHit3;
            bool improved = merA3 >= GateAHit3 && (merA3 + merB3) > (litA3 + litB3) && lostByMerge3 == 0;
            Console.WriteLine();
            Console.WriteLine("JUDGE positive_control=" + (positiveControl ? "PASS" : "FAIL")
                + " literal_unchanged=" + (literalUnchanged ? "PASS" : "FAIL")
                + " merged_improves=" + (improved ? "PASS" : "FAIL")
                + " negative_clean=" + (negativeClean ? "PASS" : "FAIL")
                + " overmatch_bounded=" + (overmatchBounded ? "PASS" : "FAIL")
                + " no_answer_clean=" + (noAnswerClean ? "PASS" : "FAIL")
                + " concept_clean=" + (conceptClean ? "PASS" : "FAIL")
                + " known_gaps_bounded=" + (knownGapsBounded ? "PASS" : "FAIL"));
            bool gate = positiveControl && literalUnchanged && improved && negativeClean && overmatchBounded
                && noAnswerClean && conceptClean && knownGapsBounded && failures == 0;
            Console.WriteLine("MERGE_GATE " + (gate ? "PASS" : "FAIL")
                + " need **hit@3** literal A>=" + GateAHit3 + " B>=" + GateBHit3 + " ALL>=" + GateAllHit3
                + " merged A>=" + GateAHit3 + " ALL>(literal) losses@3=0"
                + " negative<=" + NegativeMaxCandidates + " overmatch<=" + MergedOvermatchMax
                + " 硬答<=" + NoAnswerMaxOffenders + " 概念错位<=" + ConceptMaxOffenders
                + " 已知缺口<=" + KnownGapMax);
            return gate ? 0 : 1;
        }
        finally
        {
            try { Directory.Delete(dataRoot, true); } catch (Exception) { }
        }
    }

    private static List<string> RunQuery(WorldKnowledgeQueryService service, string text, string identity)
    {
        return service.Query(BuildQuery(text, identity)).HitIds;
    }

    // 查询对象单点构造（09-17 抽出）：判据 E 要拿到**整个结果**（State / HitIds）去算决策，
    // 不能只要 HitIds —— 两份各写一遍迟早走样。
    private static WorldbookQuery BuildQuery(string text, string identity)
    {
        return new WorldbookQuery
        {
            IdentityId = identity,
            KnowledgeScope = "private",
            KnowledgeScopeAvailable = true,
            EffectiveDetail = "secret",
            EffectiveDetailAvailable = true,
            RequestedDetail = "secret",
            PlayerText = text,
            MaximumBytes = 1 << 20
        };
    }

    // ── 判据 E 的实现：该空手时空手（2026-09-17）──────────────────────────────────
    //
    // 判什么：把这句拿去查，看**这一轮会不会把知识喂给模型**（观测点＝`WorldKnowledgeDecisionPolicy.BuildPromptBlock` 是否为空）。
    //   · expect=empty   —— 库里本就没这一条（或问法没有确定所指）⇒ 正确就是空手，喂模型即错。
    //   · expect=concept —— 泛问词已升格成概念词条 ⇒ 该答，但**排第一的必须是概念条**；
    //                       被某条具体条目顶到第一，就是把泛问当成了专名。
    // ⚠️ 2026-09-18：观测点从 `AllowsAi` 换成了 `BuildPromptBlock`。拆开「知道」与「开口」之后，
    //   `AllowsAi` 只回答"让不让这一轮说话"，`not_found` 也会是 true —— 继续拿它当判据，
    //   这条闸会变成**恒绿（或恒红）的假闸**，看着全过、其实什么都没测。
    //   而「知识块非空」正好等于"这一轮真的把世界书事实拼进了提示词"。
    // 为什么身份要逐个查：同一句话，换个身份能看到的东西就不一样，「只有某个身份会答错」是真事
    //   —— 09-17 的「领主」就是 soldier/noble/merchant 三种身份答错、别的没中。
    // 返回 false ＝ 题集缺失或为空。一条从不执行的判据，"没报警"什么也证明不了，必须当场报红。
    internal static bool RunNoAnswerRuler(WorldKnowledgeQueryService merged, WorldKnowledgeSnapshot snapshot,
        string repositoryRoot, out int offenders, out int conceptOffenders, out int knownGaps)
    {
        offenders = 0;
        conceptOffenders = 0;
        knownGaps = 0;

        string casesPath = Path.Combine(repositoryRoot, "tools", "_generic_question_cases_20260917.json");
        Console.WriteLine();
        Console.WriteLine("── E 该空手时空手（判的是『会不会被拼给模型』，不是『捞回几条』）──");
        if (!File.Exists(casesPath))
        {
            Console.WriteLine("NOANSWER_CASES_MISSING " + casesPath + "  ⇒ 没题可跑，按失败计");
            return false;
        }

        JArray list = JObject.Parse(File.ReadAllText(casesPath))["cases"] as JArray;
        if (list == null || list.Count == 0)
        {
            Console.WriteLine("NOANSWER_CASES_EMPTY ⇒ 一条题都没有，判据是空转的，按失败计");
            return false;
        }

        foreach (JToken token in list)
        {
            string text = (string)token["query"];
            string expect = ((string)token["expect"] ?? "empty").Trim();
            bool isConcept = string.Equals(expect, "concept", StringComparison.Ordinal);
            bool knownGap = string.Equals((string)token["status"], "known_gap", StringComparison.Ordinal);
            List<string> allow = (token["allow"] as JArray)?.Select(x => (string)x).ToList() ?? new List<string>();
            List<string> identities = (token["identities"] as JArray)?.Select(x => (string)x).ToList() ?? new List<string>();
            if (identities.Count == 0) identities.Add("awake:identity:commoner");

            var wrong = new List<string>();
            var rows = new List<string>();
            foreach (string identity in identities)
            {
                WorldbookQuery query = BuildQuery(text, identity);
                WorldKnowledgeQueryResult result = merged.Query(query);
                // 真判据：同一个策略 —— `NpcDialogueService` 就是照它决定往提示词里塞不塞知识的。
                // 2026-09-18 换观测点：`AllowsAi` 已拆成"让不让说"，not_found 也是 true，拿它判会成假闸；
                // 知识块非空 == 这一轮真的把世界书事实拼给了模型。
                WorldKnowledgeDecision decision = WorldKnowledgeDecisionPolicy.Create(query, result, "no-answer-ruler");
                bool answered = !string.IsNullOrWhiteSpace(WorldKnowledgeDecisionPolicy.BuildPromptBlock(decision));
                string top1 = decision.HitIds.Count > 0 ? decision.HitIds[0].Split(':').Last() : "-";
                rows.Add("    " + identity.Split(':').Last() + "=" + decision.State
                    + (answered ? "/喂知识" : "/空手")
                    + " 开口=" + decision.AllowsAi
                    + " 通路=" + result.MatchMode
                    + " top1=" + top1 + " 条数=" + decision.HitIds.Count);
                // 把**答出来的那句话**印出来。只给条目名会让人以为"是不是只搭上了一点边"，
                // 给原话才看得出这就是拿它当答案（2026-09-17 甲方追问后加）。
                rows.Add("      它答的是：" + Shorten(decision.RetrievedText, 90));

                bool pass = isConcept
                    ? answered && allow.Contains(decision.HitIds.FirstOrDefault())
                    : !answered;
                if (!pass) wrong.Add(identity.Split(':').Last());
            }

            string verdict = wrong.Count == 0 ? "OK"
                : knownGap ? "KNOWN_GAP(已确认缺口，内容侧未修)"
                : "HARD_ANSWER(错)";
            Console.WriteLine("NOANSWER " + verdict
                + " expect=" + expect + (knownGap ? " status=known_gap" : "")
                + (wrong.Count == 0 ? "" : " 不对的身份[" + string.Join(",", wrong) + "]")
                + " q=" + text);
            foreach (string row in rows) Console.WriteLine(row);

            if (wrong.Count == 0) continue;
            if (knownGap) knownGaps++;
            else if (isConcept) conceptOffenders++;
            else offenders++;
        }

        RunNoAnswerDiagnostic(merged, snapshot, list, repositoryRoot);
        return true;
    }

    // 诊断（默认不打印，AWAKE_NOANSWER_DIAG=1 才打）：
    // ① 共享 term 能不能把病题和真问题分开（结论：分不开）；
    // ② 如果加一条出口规则「与问句没有任何**入口词**（keyword）共享的命中，只算相关、不算知道」，
    //    代价是多少 —— 逐个真问题量：它的目标条目有没有一个 keyword 出现在问句里。
    private static void RunNoAnswerDiagnostic(WorldKnowledgeQueryService merged, WorldKnowledgeSnapshot snapshot,
        JArray list, string repositoryRoot)
    {
        if (Environment.GetEnvironmentVariable("AWAKE_NOANSWER_DIAG") != "1") return;
        Console.WriteLine();
        Console.WriteLine("── 诊断：跟命中条目『共享几个 term』能不能把病题和真问题分开 ──");
        var queries = new List<string>();
        foreach (JToken token in list) queries.Add((string)token["query"]);
        queries.AddRange(NoAnswerContrastProbes);
        foreach (string q in queries)
        {
            bool contrast = Array.IndexOf(NoAnswerContrastProbes, q) >= 0;
            WorldKnowledgeQueryResult result = merged.Query(BuildQuery(q, "awake:identity:commoner"));
            HashSet<string> queryTerms = WorldbookTermIndex.TermSet(q);
            Console.WriteLine("  " + (contrast ? "[真问题]" : "[本题集]") + " " + q
                + " ⇒ 命中 " + result.HitIds.Count + " 条，问句切出 " + queryTerms.Count + " 个 term");
            foreach (string id in result.HitIds.Take(3))
            {
                WorldKnowledgeEntry entry;
                if (!snapshot.Entries.TryGetValue(id, out entry)) continue;
                var entryTerms = new HashSet<string>(WorldbookTermIndex.TermSet(entry.Title), StringComparer.OrdinalIgnoreCase);
                foreach (string term in WorldbookTermIndex.TermSet(entry.Summary)) entryTerms.Add(term);
                List<string> shared = queryTerms.Where(t => entryTerms.Contains(t))
                    .OrderByDescending(t => t.Length).ToList();
                Console.WriteLine("      " + id.Split(':').Last() + " 共享 " + shared.Count + " 个："
                    + (shared.Count == 0 ? "（无）" : string.Join("、", shared.Take(8))));
            }
        }

        // ② 出口规则的代价：24 题真问题里，目标条目有没有**入口词**（keyword）出现在问句里。
        string casesPath = Path.Combine(repositoryRoot, "tools", "_retrieval_cases_20260916.json");
        if (!File.Exists(casesPath)) return;
        JArray real = JObject.Parse(File.ReadAllText(casesPath))["cases"] as JArray;
        if (real == null) return;
        Console.WriteLine();
        Console.WriteLine("── 诊断：若加出口规则「命中条目与问句没有任何 keyword 共享 ⇒ 只算相关、不算知道」，代价多少 ──");
        int withEntryWord = 0, withoutEntryWord = 0, skipped = 0;
        var noEntryWord = new List<string>();
        foreach (JToken token in real)
        {
            if (token["countInGate"] != null && !(bool)token["countInGate"]) { skipped++; continue; }
            string target = (string)token["target"];
            string text = (string)token["query"];
            WorldKnowledgeEntry entry;
            if (!snapshot.Entries.TryGetValue(target, out entry)) { skipped++; continue; }
            List<string> hit = entry.Keywords
                .Where(k => !string.IsNullOrWhiteSpace(k)
                    && text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (hit.Count > 0) withEntryWord++;
            else { withoutEntryWord++; noEntryWord.Add(text); }
            Console.WriteLine("   " + (hit.Count > 0 ? "有入口词" : "★没有入口词")
                + " 共享[" + string.Join("、", hit.Take(4)) + "]  " + text);
        }
        Console.WriteLine("   小计：目标条目的入口词出现在问句里 " + withEntryWord + " 条，"
            + "**没有** " + withoutEntryWord + " 条（另有 " + skipped + " 条不计入）");
        Console.WriteLine("   ⇒ 「没有入口词」那 " + withoutEntryWord + " 条，一旦上了这条出口规则就会被判成『不算知道』；"
            + "它们现在是命中还是没命中，决定这条规则能不能上。");
        if (noEntryWord.Count > 0) Console.WriteLine("   ★没有入口词的题目：" + Join(noEntryWord));

        // ③ 正面实验：把指代词换成"当前聚落"，检索是不是就答得上了。
        //    这是"缺一个判断步骤"的证据 —— 上游**已经**把聚落放进查询了
        //    （`NpcDialogueService.cs:1051 SettlementId = _heroSettlementId`，取自
        //     `hero.CurrentSettlement ?? hero.StayingInSettlement`），只是检索层没用它：
        //    `SettlementId` 只在身份条件里被读（`WorldbookIdentityEvaluator.cs:71`），
        //    `SceneKeywords` 全仓无人读。
        Console.WriteLine();
        Console.WriteLine("── 诊断：把「这边／附近」换成当前聚落之后，检索是不是就答得上了 ──");
        Console.WriteLine("   （聚落 → 条目的机制：游戏 StringId 就在条目的 keywords 里，例："
            + "geography.villages-zhemyan 的 keywords 含 `castle_village_S1_2`）");
        foreach (string[] pair in DeixisResolveProbes)
        {
            WorldKnowledgeQueryResult before = merged.Query(BuildQuery(pair[0], "awake:identity:commoner"));
            WorldKnowledgeQueryResult after = merged.Query(BuildQuery(pair[1], "awake:identity:commoner"));
            Console.WriteLine("   「" + pair[0] + "」⇒ " + before.HitIds.Count + " 条，喂模型=" + before.State
                + "  top1=" + Top1(before));
            Console.WriteLine("   「" + pair[1] + "」⇒ " + after.HitIds.Count + " 条，喂模型=" + after.State
                + "  top1=" + Top1(after));
            Console.WriteLine();
        }
        foreach (string key in new[] { "castle_village_S1_2", "哲米扬" })
            Console.WriteLine("   关键词索引里有 `" + key + "` 吗：" + (snapshot.KeywordIndex.ContainsKey(key) ? "有" : "没有"));
    }

    private static string Top1(WorldKnowledgeQueryResult result)
    {
        return result.HitIds.Count > 0 ? result.HitIds[0].Split(':').Last() : "-";
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
