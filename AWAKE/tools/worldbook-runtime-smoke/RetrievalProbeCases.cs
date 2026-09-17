using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Awake;
using Newtonsoft.Json.Linq;

// 检索层对照探针（离线验台用，2026-09-16）。
//
// 干什么：拿真包（ModuleData/Worldbook/packages/calradia）跑一批「玩家问法」，看检索能不能把
//   目标条目排进前 3。用来量「补 summary 进索引」这一改的收益，并守住「不许回退」。
//
// 题集**只有一份**：tools/_retrieval_cases_20260916.json —— Python 探针
//   (tools/_bge_probe_20260916.py) 与本验台都读它，避免两边各写一套（平行实现必须同源）。
//
// 方案与判据出处：docs/PLAN-SUMMARY-INTO-INDEX-20260916.md §九。
//
// ⚠️ 已知边界（读的人别当前提之外的东西用）：
//   测的是**公有 API `Query()` 的输出 `HitIds`**，不是私有的 `FindCandidates`。
//   `Query()` 在检索之后还有一层身份/权限过滤（`SelectExpression`），拿不到授权的条目会从
//   `HitIds` 里消失。为了不把「权限」误读成「检索」，每个用例的**身份取自目标条目自己的授权**，
//   这样目标一定能被吐出来；而检索排序发生在身份过滤**之前**，所以对两臂是对称的。
//   每条用例的身份都会打印出来，便于复核。
//   ⚠️ 原以为「身份过滤会让 C# 的绝对数字无法与 Python 那一臂逐个比较」——2026-09-16 实测否掉了这个
//   顾虑：两套实现（本验台 vs `tools/_bge_probe_20260916.py` 的 LexicalIndex 臂）逐条 26/26 完全一致
//   （A 9/13、B 0/13）。原因是本批身份多为 commoner/headman/notable，条目普遍没有 restrict 规则，
//   身份过滤实际是空操作。⇒ 不再打折，两边数字可直接对照。
internal static class RetrievalProbeCases
{
    // 门禁（改前基线见题集 JSON 的 baselineLexical）：
    //   A 组 9/13 —— 现网就能命中的，改了不许掉（这条靠**结构**保证：关键词路径一行没改）；
    //   B 组 / 合计 —— 这两个是**实测到的水平**（2026-09-16：B 7/13、合计 16/26），
    //   **不是当初的愿望值**（当初只写「B≥5、合计≥13」）。故意抬到实现值：以后任何改动若把它们
    //   悄悄拉低，门禁要红。想拉低必须同时改这里 + 文档，等于强制一次显式决策。
    //   （B_hit3 实测 9/13、A_hit3 10/13、合计 hit3 19/26 —— 未入门禁，仅备查。）
    //
    // ⚠️ 2026-09-17 分母变了（26 → 24），看数字前先读这段：
    //   · 删掉一条**题不可判**的废题（`哪座城堡底下管着两个村子？`：全库 67 座城堡都写「两村」，
    //     无唯一答案）—— 甲方裁定「废题别留着」；
    //   · `这一带有好马吗？` 标了 `countInGate=false`（多个村都产好马，把拉迈萨定成唯一目标是任意的，
    //     单目标命中率判不出通道好坏）—— 它**仍跑、仍打印**，只是不进分子分母。
    //   ⇒ 门槛值（9 / 7 / 16）**一个没动**：删掉的那条在两臂都是 0，分子不动、分母缩小。
    //     所以现在的 B 是「7/11」、合计「16/24」；**别拿 7/13 或 9/26 跟它比**。
    private const int GateAHit1 = 9;
    private const int GateBHit1 = 7;
    private const int GateAllHit1 = 16;

    // ── 三条对照（2026-09-16 加；每条都必须是「能判」的，否则就是摆设）────────────────────
    // ① 阴性对照：「无关话」应与世界书零交集。
    //    实测：这组在阈值 1 / 2 下**都给 0** ⇒ **它们判不出阈值好坏**（别当事后诸葛，别拿它当门禁）。
    private const int NegativeMaxCandidates = 3;
    // 必须与 `WorldKnowledgeQueryService.FallbackMaxCandidates` 相等；故意分开写，方便它被改动时门禁变红。
    private const int ExpectedFallbackMaxCandidates = 5;
    private static readonly string[] NegativeQueries =
    {
        "今天午饭吃什么",
        "帮我写一段排序代码",
        "火车时刻表在哪儿查"
    };

    // ② 兜底通道的过匹配门禁：「笼统话」没有关键词命中，会整句落进兜底通道，
    //    所以它的候选数**只受 `FallbackMaxCandidates` 约束**。
    //    实测（开/关兜底对照）：`村民的一亩地归谁` 0 → 2、`帝国的一座城` 0 → 5（恰好卡住上限）
    //    ⇒ 这条**真能判**「兜底上限调没调对」。
    private static readonly string[] FallbackOvermatchProbes =
    {
        "村民的一亩地归谁",
        "帝国的一座城"
    };

    // ③ 关键词路径的过匹配（**只报数、不设门禁**）：`说说这座城堡坐落于哪儿` 实测 67 条，
    //    且**开关兜底都是 67** ⇒ 这是关键词层「整串包含」的既有性质（短/泛关键词如 `城堡` 一撞就中一片），
    //    **不是兜底通道引入的**，本轮不动它。留在这里是为了下次有人看到 67 时知道它老早就在。
    private static readonly string[] KeywordPathOvermatchProbes =
    {
        "说说这座城堡坐落于哪儿"
    };

    internal static void Run(string worldbookRoot, string repositoryRoot)
    {
        string casesPath = Path.Combine(repositoryRoot, "tools", "_retrieval_cases_20260916.json");
        if (!File.Exists(casesPath)) throw new FileNotFoundException("检索题集不存在: " + casesPath);

        string packageManifest = Path.Combine(worldbookRoot, "packages", "calradia", "manifest.json");
        WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.Load(packageManifest);
        var service = new WorldKnowledgeQueryService(snapshot);

        JArray cases = (JObject.Parse(File.ReadAllText(casesPath))["cases"] as JArray)
            ?? throw new InvalidOperationException("题集 JSON 里没有 cases 数组: " + casesPath);

        int aHit1 = 0, aHit3 = 0, bHit1 = 0, bHit3 = 0, nA = 0, nB = 0;
        int observed = 0;
        var failures = new List<string>();

        foreach (JToken token in cases)
        {
            string group = (string)token["group"];
            string target = (string)token["target"];
            string text = (string)token["query"];
            // countInGate=false 的题：仍跑、仍打印，但不进分子分母（见上方注释与题集 JSON 的 note）。
            bool countInGate = token["countInGate"] == null || (bool)token["countInGate"];
            string identity = PickIdentity(snapshot, target);

            WorldKnowledgeQueryResult result = service.Query(new WorldbookQuery
            {
                IdentityId = identity,
                KnowledgeScope = "private",
                KnowledgeScopeAvailable = true,
                EffectiveDetail = "secret",
                EffectiveDetailAvailable = true,
                RequestedDetail = "secret",
                PlayerText = text,
                MaximumBytes = 1 << 20
            });

            List<string> hits = result.HitIds;
            bool hit1 = hits.Count > 0 && string.Equals(hits[0], target, StringComparison.Ordinal);
            bool hit3 = hits.Take(3).Any(x => string.Equals(x, target, StringComparison.Ordinal));

            if (!countInGate)
            {
                observed++;
                Console.WriteLine("RETRIEVAL_OBSERVE " + (hit3 ? "HIT" : "MISS")
                    + " group=" + group
                    + " h1=" + (hit1 ? "1" : "0")
                    + " hits=" + hits.Count
                    + " top3=[" + string.Join(",", hits.Take(3).Select(x => x.Split(':').Last())) + "]"
                    + " q=" + text);
                continue;
            }

            if (string.Equals(group, "A", StringComparison.Ordinal)) { nA++; aHit1 += hit1 ? 1 : 0; aHit3 += hit3 ? 1 : 0; }
            else { nB++; bHit1 += hit1 ? 1 : 0; bHit3 += hit3 ? 1 : 0; }

            if (!hit3) failures.Add(group + " " + target + " :: " + text);

            Console.WriteLine("RETRIEVAL_CASE " + (hit3 ? "HIT" : "MISS")
                + " group=" + group
                + " h1=" + (hit1 ? "1" : "0")
                + " hits=" + hits.Count
                + " top3=[" + string.Join(",", hits.Take(3).Select(x => x.Split(':').Last())) + "]"
                + " identity=" + identity
                + " q=" + text);
        }

        int allHit1 = aHit1 + bHit1;
        int allHit3 = aHit3 + bHit3;
        int total = nA + nB;

        // ① 阴性对照：跟世界书无关的话，不该被检索接住。
        // ⚠️ 它拦的是「把诚实的『没听说过』换成自信的错答」。但实测它判不出阈值好坏（见上方常量注释）。
        int negativeWorst = 0;
        foreach (string text in NegativeQueries)
        {
            List<string> hits = RunQuery(service, text);
            negativeWorst = Math.Max(negativeWorst, hits.Count);
            Console.WriteLine("RETRIEVAL_NEGATIVE candidates=" + hits.Count + " q=" + text);
        }
        bool negativeClean = negativeWorst <= NegativeMaxCandidates;
        Console.WriteLine("RETRIEVAL_NEGATIVE_WORST candidates=" + negativeWorst
            + " max=" + NegativeMaxCandidates + " " + (negativeClean ? "OK" : "OVERMATCH"));

        // ② 兜底通道过匹配门禁：笼统话的候选数不得超过兜底上限。
        //    这里**故意写死独立数字**而不是引用源码常量 —— 常量被放宽时门禁要能红，否则门禁等于摆设。
        int fallbackOvermatchWorst = 0;
        foreach (string text in FallbackOvermatchProbes)
        {
            List<string> hits = RunQuery(service, text);
            fallbackOvermatchWorst = Math.Max(fallbackOvermatchWorst, hits.Count);
            Console.WriteLine("RETRIEVAL_OVERMATCH candidates=" + hits.Count + " q=" + text);
        }
        bool fallbackBounded = fallbackOvermatchWorst <= ExpectedFallbackMaxCandidates;
        Console.WriteLine("RETRIEVAL_OVERMATCH_WORST candidates=" + fallbackOvermatchWorst
            + " max=" + ExpectedFallbackMaxCandidates + " " + (fallbackBounded ? "OK" : "FLOOD"));

        // ③ 关键词路径过匹配：只报数（见上方注释，属既有行为，本轮不动）。
        foreach (string text in KeywordPathOvermatchProbes)
            Console.WriteLine("RETRIEVAL_OVERMATCH_REPORT candidates=" + RunQuery(service, text).Count + " q=" + text);

        Console.WriteLine("RETRIEVAL_SUMMARY A_hit1=" + aHit1 + "/" + nA
            + " A_hit3=" + aHit3 + "/" + nA
            + " B_hit1=" + bHit1 + "/" + nB
            + " B_hit3=" + bHit3 + "/" + nB
            + " ALL_hit1=" + allHit1 + "/" + total
            + " ALL_hit3=" + allHit3 + "/" + total
            + " observed_only=" + observed + "（不进分子分母，见题集 JSON 的 countInGate）");

        foreach (string failure in failures) Console.WriteLine("RETRIEVAL_MISS " + failure);

        bool gate = aHit1 >= GateAHit1 && bHit1 >= GateBHit1 && allHit1 >= GateAllHit1 && negativeClean && fallbackBounded;
        Console.WriteLine("RETRIEVAL_GATE " + (gate ? "PASS" : "FAIL")
            + " need A_hit1>=" + GateAHit1 + " B_hit1>=" + GateBHit1 + " ALL_hit1>=" + GateAllHit1
            + " negative<=" + NegativeMaxCandidates + " overmatch<=" + ExpectedFallbackMaxCandidates);

        // 量数模式：只看数、不当门禁（做变异检验/取基线时用）。
        // ⚠️ 这个开关本身也是判据，必须两边都验过（见文档 §九 第 2 步）。
        if (string.Equals(Environment.GetEnvironmentVariable("AWAKE_RETRIEVAL_PROBE_MEASURE_ONLY"), "1", StringComparison.Ordinal))
        {
            Console.WriteLine("RETRIEVAL_GATE SKIPPED (AWAKE_RETRIEVAL_PROBE_MEASURE_ONLY=1)");
            return;
        }

        if (!gate)
        {
            throw new InvalidOperationException("检索门禁未过: A_hit1=" + aHit1 + "/" + nA
                + " B_hit1=" + bHit1 + "/" + nB + " ALL_hit1=" + allHit1 + "/" + total
                + " negativeWorst=" + negativeWorst + "/" + NegativeMaxCandidates
                + " overmatchWorst=" + fallbackOvermatchWorst + "/" + ExpectedFallbackMaxCandidates
                + " —— 明细见上面的 RETRIEVAL_MISS / RETRIEVAL_NEGATIVE / RETRIEVAL_OVERMATCH 行");
        }
    }

    // 对照用的一次查询：固定低权限身份 + 放开细节上限，只看「检索接没接住」。
    private static List<string> RunQuery(WorldKnowledgeQueryService service, string text)
    {
        return service.Query(new WorldbookQuery
        {
            IdentityId = "awake:identity:commoner",
            KnowledgeScope = "private",
            KnowledgeScopeAvailable = true,
            EffectiveDetail = "secret",
            EffectiveDetailAvailable = true,
            RequestedDetail = "secret",
            PlayerText = text,
            MaximumBytes = 1 << 20
        }).HitIds;
    }

    // 身份取目标条目自己授权里的一个（稳定排序取第一个）：保证「目标可被吐出来」，
    // 否则测到的是权限而不是检索。
    private static string PickIdentity(WorldKnowledgeSnapshot snapshot, string targetId)
    {
        WorldKnowledgeEntry entry;
        if (!snapshot.Entries.TryGetValue(targetId, out entry))
        {
            throw new InvalidOperationException("题集里的目标条目不在包里: " + targetId);
        }
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
}
