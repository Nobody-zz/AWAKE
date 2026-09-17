using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Awake;
using MarcusAwakeStorage;
using Newtonsoft.Json.Linq;

// ============================================================
// 世界书运行时门控模拟器（只读 / 离线）
//   用 AWAKE 真实运行时代码加载一个运行时包，
//   按「身份 × 问题 × 范围 × 详细度」查询，打印门控结果并导出 JSON。
//   不启动游戏、不联网、不写回仓库（包会先复制到临时目录再加载）。
//
// 用法：
//   dotnet run -c Release -- [manifest.json路径] [导出json路径] [问题,逗号分隔] [身份,逗号分隔]
// 例：
//   dotnet run -c Release -- "D:\...\awake-worldbook-pilot\manifest.json" out.json "收成,劫匪" "profile.commoner,profile.noble"
// ============================================================

// 从输出目录向上找仓库根（同时含 src 与 release 的那一层）
string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "src"))
            && Directory.Exists(Path.Combine(dir.FullName, "release")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return Directory.GetCurrentDirectory();
}

string repoRoot = FindRepoRoot();

// persona 子命令：生成 persona DSL 落盘 / 列举 heroes（P 层）
if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "persona"))
{
    string[] rest = args.Skip(1).ToArray();
    return PersonaDialogueSim.Run(rest);
}
if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "persona-list"))
{
    string[] defsDir = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
        ? new[] { args[1] }
        : new[] { Path.Combine(repoRoot, "ModuleData", "Worldbook", "persona_definitions", "definitions") };
    return PersonaDialogueSim.ListHeroes(defsDir[0]);
}

// persona-roster：角色卡名册离线探针（方向丙-a）——扫目录、按 characterId 选卡、跑真实生成器。
// 用法: dotnet run -c Release -- persona-roster <worldbookRoot> [out.json] [charIds,逗号分隔] [dslOutDir]
if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "persona-roster"))
{
    return PersonaRosterSim.Run(args.Skip(1).ToArray());
}

// probe 模式：JSON 规格驱动的门控探针（身份/年龄/文化/王国/聚落/能力全可指定）
// 用法: dotnet run -c Release -- probe <manifest.json> <spec.json> <out.json>
if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "probe"))
{
    return RunProbeMode(args.Skip(1).ToArray());
}

// identity-gate：「谁知道」这条维度的门禁（**身份 × 题集全扫**）—— 设计初衷的第一层。
// 用法: dotnet run -c Release -- identity-gate [manifest.json] [cases.json] [out.json]
//   （默认取 ModuleData/Worldbook/packages/calradia/manifest.json 与 tools/_retrieval_cases_20260916.json）
// 判据：
//   ① 阴性（硬）：目标条目的 grants **点不到**某身份（按 parents 上溯闭包）⇒ 该条目 id 不得出现在 HitIds 里；
//   ② 点名题（硬）：`deniedCases` 每条都带**阳性对照** —— 同一个问句换一个 grants 点到、能力够的身份必须拿到，
//      否则这条题根本没打在条目上，不构成有效探针；
//   ③ 空转护栏（硬）：扫描行数与"该知道且真拿到"的行数都不许为 0 ——「恒 True＝没测」。
// ⚠️ ① 只用包里的 grants 与 parents **两份清单**算，不含 scope/detail 数学 ⇒ 与运行时实现不重叠，不是自证。
// 变异检验：置 AWAKE_GATE_MUTATE_ASSUME_ALLOWED=1 会把被排除身份的查询"冒充成够得着的身份"⇒ ① 必须判红。
if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "identity-gate"))
{
    return RunIdentityGate(args.Skip(1).ToArray());
}

// full-context 模式：用真实事实捕获/周报代码生成一个可供组合链路消费的上下文快照。
// 用法: dotnet run -c Release -- full-context <out.json>
if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "full-context"))
{
    return RunFullContextMode(args.Skip(1).ToArray());
}

string manifestPath = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? args[0]
    : Path.Combine(repoRoot, "release", "awake-worldbook-pilot", "manifest.json");
string dumpPath = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
    ? args[1]
    : Path.Combine(Path.GetTempPath(), "awake-sim-retrieval.json");
string[] probes = args.Length > 2 && !string.IsNullOrWhiteSpace(args[2])
    ? args[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : new[] { "收成", "劫匪", "领主" };
string[] identities = args.Length > 3 && !string.IsNullOrWhiteSpace(args[3])
    ? args[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : new[] { "profile.commoner", "profile.soldier", "profile.noble", "profile.merchant", "profile.headman", "profile.notable", "profile.anonymous" };

if (!File.Exists(manifestPath))
{
    Console.WriteLine("找不到 manifest: " + manifestPath);
    return 2;
}

// 复制到临时目录再加载，确保零写回
string workDir = Path.Combine(Path.GetTempPath(), "awake-runtime-sim", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workDir);
foreach (string name in new[] { "manifest.json", "runtime.json", "index.json" })
{
    string src = Path.Combine(Path.GetDirectoryName(manifestPath)!, name);
    if (File.Exists(src)) File.Copy(src, Path.Combine(workDir, name), true);
}

var snapshot = WorldKnowledgeLoader.Load(Path.Combine(workDir, "manifest.json"));
var service = new WorldKnowledgeQueryService(snapshot);

// ── 语义臂（可选）：置 AWAKE_SIM_SEMANTIC=1 才挂，默认不挂 ⇒ 不动原有行为。
//    次序照游戏来：**世界书先到**（此时没有 host，应当挂不上）→ 战役会话就绪 → RetryCurrent 补一次。
//    挂上以后，下面同一套门控矩阵就是"带语义"的结果，可与不置开关那一跑直接对照。
if (Environment.GetEnvironmentVariable("AWAKE_SIM_SEMANTIC") == "1") AttachSemanticArm(service, snapshot);


Console.WriteLine("=== 包概览 ===");
Console.WriteLine($"packageId={snapshot.PackageId}  version={snapshot.Version}  revision={snapshot.Revision}");
Console.WriteLine($"entries={snapshot.Entries.Count}  identities={snapshot.Identities.Count}  referrals={snapshot.Referrals.Count}");
foreach (var kv in snapshot.Entries)
{
    var e = kv.Value;
    Console.WriteLine($"  - {e.Id}  [{e.Domain}] {e.Title}  表达数={e.Expressions.Count}  关键词={string.Join("/", e.Keywords)}");
}
Console.WriteLine("  identities: " + string.Join(", ", snapshot.Identities.Keys));

// 身份能力表：取自 src/WorldbookIdentityCapabilityRules.cs（Resolve）
// scope = 该身份的圈层上限（不是地理范围），detail = 该身份的详细度上限。
// 作者写 grant 时，scope / min_detail 不能超过对应身份的上限，否则该身份永远拿不到。
var CAP = new Dictionary<string, (string Scope, string Detail)>(StringComparer.OrdinalIgnoreCase)
{
    ["profile.commoner"] = ("local", "rumor"),
    ["profile.villager"] = ("local", "rumor"),
    ["profile.townsfolk"] = ("regional", "summary"),
    ["profile.notable"] = ("regional", "detail"),
    ["profile.headman"] = ("national", "detail"),
    ["profile.merchant"] = ("faction", "detail"),
    ["profile.tavernkeeper"] = ("faction", "detail"),
    ["profile.ransom_broker"] = ("faction", "detail"),
    ["profile.soldier"] = ("national", "detail"),
    ["profile.noble"] = ("elite", "detail"),
    ["profile.noble_high_steward"] = ("elite", "detail"),
    ["profile.anonymous"] = ("", ""),
};

(string Scope, string Detail) CapOf(string identity) =>
    CAP.TryGetValue(identity, out var c) ? c : ("local", "rumor");

// requestedDetail = 询问者索要的详细度上限；默认给到 secret（最宽）
WorldKnowledgeQueryResult Ask(string identity, string requestedDetail, string playerText)
{
    var cap = CapOf(identity);
    return service.Query(new WorldbookQuery
    {
        IdentityId = identity,
        KnowledgeScope = cap.Scope,
        KnowledgeScopeAvailable = !string.IsNullOrEmpty(cap.Scope),
        EffectiveDetail = cap.Detail,
        EffectiveDetailAvailable = !string.IsNullOrEmpty(cap.Detail),
        RequestedDetail = requestedDetail,
        PlayerText = playerText,
        MaximumBytes = 4096
    });
}

Console.WriteLine();
Console.WriteLine("=== 身份能力上限（取自 WorldbookIdentityCapabilityRules）===");
Console.WriteLine("  作者给某身份写 grant 时，scope 与 min_detail 不得越过该身份上限，否则永远 blocked。");
foreach (string id in identities)
{
    var cap = CapOf(id);
    Console.WriteLine($"  {id,-24} scope={cap.Scope,-10} detail={cap.Detail}");
}

Console.WriteLine();
Console.WriteLine("=== 门控矩阵：谁问什么能拿到什么（按各身份真实能力查询）===");
foreach (string probe in probes)
{
    Console.WriteLine();
    Console.WriteLine($"--- 提问：{probe} ---");
    foreach (string id in identities)
    {
        var r = Ask(id, "secret", probe);
        string text = (r.RetrievedText ?? string.Empty).Trim().Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 60) text = text.Substring(0, 60) + "...";
        Console.WriteLine($"  {id,-24} state={r.State,-16} referrals={r.ReferralIds.Count}  {text}");
    }
}

// 导出（供 e2e_ollama.py 接本地模型用）
var dump = new JArray();
foreach (string probe in probes)
    foreach (string id in identities)
    {
        var r = Ask(id, "secret", probe);
        dump.Add(new JObject
        {
            ["question"] = probe,
            ["identity"] = id,
            ["state"] = r.State,
            ["text"] = (r.RetrievedText ?? string.Empty).Trim(),
            ["referrals"] = r.ReferralIds.Count
        });
    }
File.WriteAllText(dumpPath, dump.ToString(Newtonsoft.Json.Formatting.Indented));
Console.WriteLine();
Console.WriteLine("检索结果已导出: " + dumpPath);
Console.WriteLine("SIM-OK");
return 0;

// ---------- identity-gate 实现 ----------

// 身份 → 能力。**走真件**：游戏侧同一个 `WorldbookIdentityCapabilityRules.Resolve`。
(string Scope, string Detail) ProfileOfIdentity(string identityId)
{
    string tail = identityId.Substring(identityId.LastIndexOf(':') + 1);
    bool noble = StringComparer.Ordinal.Equals(tail, "noble")
              || StringComparer.Ordinal.Equals(tail, "noble_high_steward")
              || StringComparer.Ordinal.Equals(tail, "noble_mature");
    var profile = WorldbookIdentityCapabilityRules.Resolve(tail, noble, noble ? 50 : 30, 0);
    return (profile.KnowledgeScope, profile.EffectiveDetail);
}

// 语义臂：默认矩阵与 identity-gate **共用这一个函数**（平行实现必须同源）。
void AttachSemanticArm(WorldKnowledgeQueryService svc, WorldKnowledgeSnapshot snap)
{
    string simModelDir = Environment.GetEnvironmentVariable("AWAKE_SIM_MODEL_DIR")
        ?? @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\ONNX";
    string simVocabPath = Environment.GetEnvironmentVariable("AWAKE_SIM_VOCAB")
        ?? @"D:\AWAKE-Dev\AWAKE\tools\_af_tokenizer_20260916\vocab.txt";
    string simDbPath = Path.Combine(Path.GetTempPath(), "awake-runtime-sim", "sim-semantic.db");

    Console.WriteLine("=== 语义臂 ===");
    AwakeSemanticArmBootstrap.Schedule(svc, snap);
    AwakeBackgroundTask.LastTask?.Wait(TimeSpan.FromMinutes(5));
    Console.WriteLine($"[semantic] 世界书先到（host 未就位）⇒ HasSemanticIndex={svc.HasSemanticIndex}，问过 host {AwakeRuntime.ResolveCallCount} 次");

    var simEmbedderOptions = new OnnxEmbedderOptions
    {
        ModelDirectory = simModelDir,
        VocabularyPath = simVocabPath,
        ModelId = "bge-small-zh-v1.5|" + WorldKnowledgePassage.Revision,
    };
    var simEmbedder = new OnnxSentenceEmbedder(simEmbedderOptions);
    var simBackend = new SqliteStorageAndRagBackend(simDbPath, null, simEmbedder);
    AwakeRuntime.BootHostForSim(simBackend, "sim-campaign", "sim-timeline", "sim-session");
    AwakeSemanticArmBootstrap.RetryCurrent();
    AwakeBackgroundTask.LastTask?.Wait(TimeSpan.FromMinutes(5));
    Console.WriteLine($"[semantic] 会话就绪后重试 ⇒ HasSemanticIndex={svc.HasSemanticIndex}（条目 {snap.Entries.Count} 条）");
}

int RunIdentityGate(string[] a)
{
    string manifest = a.Length > 0 && !string.IsNullOrWhiteSpace(a[0])
        ? a[0]
        : Path.Combine(repoRoot, "ModuleData", "Worldbook", "packages", "calradia", "manifest.json");
    string casesPath = a.Length > 1 && !string.IsNullOrWhiteSpace(a[1])
        ? a[1]
        : Path.Combine(repoRoot, "tools", "_retrieval_cases_20260916.json");
    string outPath = a.Length > 2 && !string.IsNullOrWhiteSpace(a[2])
        ? a[2]
        : Path.Combine(Path.GetTempPath(), "awake-sim-identity-gate.json");
    if (!File.Exists(manifest)) { Console.WriteLine("找不到 manifest: " + manifest); return 2; }
    if (!File.Exists(casesPath)) { Console.WriteLine("找不到题集: " + casesPath); return 2; }

    string workDir = Path.Combine(Path.GetTempPath(), "awake-runtime-sim", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(workDir);
    foreach (string name in new[] { "manifest.json", "runtime.json", "index.json" })
    {
        string src = Path.Combine(Path.GetDirectoryName(manifest), name);
        if (File.Exists(src)) File.Copy(src, Path.Combine(workDir, name), true);
    }

    var gateSnapshot = WorldKnowledgeLoader.Load(Path.Combine(workDir, "manifest.json"));
    var gateService = new WorldKnowledgeQueryService(gateSnapshot);
    bool wantSemantic = Environment.GetEnvironmentVariable("AWAKE_SIM_SEMANTIC") == "1";
    Console.WriteLine($"identity-gate: package={gateSnapshot.PackageId} entries={gateSnapshot.Entries.Count} semantic={wantSemantic}");
    if (wantSemantic)
    {
        AttachSemanticArm(gateService, gateSnapshot);
        // 要了却没生效（静默退化）＝本项目反复吃亏的那一类失败 ⇒ 直接判红，别让它悄悄变成"纯字面也 PASS"
        if (!gateService.HasSemanticIndex) { Console.WriteLine("IDENTITY_GATE FAIL 语义臂没挂上（要了却没生效）"); return 1; }
    }

    bool mutate = Environment.GetEnvironmentVariable("AWAKE_GATE_MUTATE_ASSUME_ALLOWED") == "1";
    if (mutate) Console.WriteLine("⚠️ 变异检验：被排除身份的查询会冒充成够得着的身份 —— 阴性判据必须判红");

    string[] gateIdentities = gateSnapshot.Identities.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();

    // 沿 parents **上溯**（与 `WorldbookIdentityEvaluator.AddIdentity` 同向）：
    // 包里的父链是 noble→notable→commoner，所以"给 commoner 的授权"人人可拿，反过来不成立。
    HashSet<string> AncestorsOf(string identityId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(identityId);
        while (stack.Count > 0)
        {
            string current = stack.Pop();
            if (string.IsNullOrWhiteSpace(current) || !seen.Add(current)) continue;
            WorldKnowledgeIdentity identity;
            if (gateSnapshot.Identities.TryGetValue(current, out identity))
                foreach (string parent in identity.Parents) stack.Push(parent);
        }
        return seen;
    }

    bool IsUnconditional(WorldKnowledgeCondition condition)
    {
        if (condition == null) return true;
        return condition.IdentityIds.Count == 0 && condition.CultureIds.Count == 0
            && condition.KingdomIds.Count == 0 && condition.SettlementIds.Count == 0
            && condition.RoleIds.Count == 0 && !condition.IsFemale.HasValue
            && !condition.IsClanLeader.HasValue && !condition.MinAge.HasValue
            && !condition.MaxAge.HasValue && !condition.MinManagement.HasValue
            && condition.MinSkills.Count == 0;
    }

    // 该条目"点名过的身份"：只读 grants 里的 identity_id（**不碰** scope/detail）。
    // 返回 null ⇒ 有 public 授权，人人可拿，这一条不做阴性断言。
    HashSet<string> NamedByAuthoring(WorldKnowledgeEntry entry)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorldKnowledgeExpression expression in entry.Expressions)
        {
            if (!expression.Enabled) continue;
            foreach (WorldKnowledgeRule grant in expression.Grants)
            {
                if (!IsUnconditional(grant.Conditions)) continue;
                string id = grant.IdentityId ?? string.Empty;
                if (id.EndsWith(":public", StringComparison.Ordinal)) return null;
                if (!string.IsNullOrWhiteSpace(id)) named.Add(id);
            }
        }
        return named;
    }

    WorldKnowledgeQueryResult AskAs(string identity, string text)
    {
        var profile = ProfileOfIdentity(identity);
        return gateService.Query(new WorldbookQuery
        {
            IdentityId = identity,
            KnowledgeScope = profile.Scope,
            KnowledgeScopeAvailable = !string.IsNullOrEmpty(profile.Scope),
            EffectiveDetail = profile.Detail,
            EffectiveDetailAvailable = !string.IsNullOrEmpty(profile.Detail),
            RequestedDetail = "secret",
            PlayerText = text,
            MaximumBytes = 4096
        });
    }

    var spec = JObject.Parse(File.ReadAllText(casesPath));
    var counted = (spec["cases"] as JArray ?? new JArray())
        .Where(x => x["countInGate"] == null || x.Value<bool>("countInGate"))
        .ToList();
    // `deniedCases` 是对象（带 `_note`），题在它的 `cases` 里。读不到就判红 —— 静默读到 0 条＝这条门禁形同不存在。
    JToken deniedNode = spec["deniedCases"];
    var deniedCases = (deniedNode is JArray ? (JArray)deniedNode : deniedNode?["cases"] as JArray) ?? new JArray();
    if (counted.Count == 0) { Console.WriteLine("IDENTITY_GATE FAIL 题集里一条计入的题都没读到"); return 1; }
    if (deniedCases.Count == 0) { Console.WriteLine("IDENTITY_GATE FAIL 点名题一条都没读到（deniedCases 形状不对？）"); return 1; }

    int sweepRows = 0, leakedRows = 0, shouldKnowRows = 0, shouldKnowHit = 0;
    var caseDetails = new JArray();
    var shouldKnowMisses = new JArray();

    Console.WriteLine();
    Console.WriteLine($"=== 阴性扫描：题集 {counted.Count} 条（不含只观察的）× 包内 {gateIdentities.Length} 个身份 ===");
    foreach (JToken token in counted)
    {
        string target = token.Value<string>("target") ?? string.Empty;
        string text = token.Value<string>("query") ?? string.Empty;
        WorldKnowledgeEntry entry;
        if (!gateSnapshot.Entries.TryGetValue(target, out entry))
        {
            Console.WriteLine("  [无效探针] 目标条目不在包里: " + target);
            return 1;
        }
        HashSet<string> named = NamedByAuthoring(entry);
        if (named == null) { Console.WriteLine("  [跳过·有 public 授权] " + target); continue; }
        if (named.Count == 0)
        {
            Console.WriteLine("  [无效探针] " + target + " 没有任何**无条件**的身份授权 —— 阴性断言对它无意义");
            return 1;
        }
        string highest = named
            .OrderByDescending(x => WorldbookIdentityEvaluator.ScopeRank(ProfileOfIdentity(x).Scope))
            .ThenByDescending(x => WorldbookIdentityEvaluator.DetailRank(ProfileOfIdentity(x).Detail))
            .First();

        var leaks = new List<string>();
        int deniedRows = 0;
        foreach (string identity in gateIdentities)
        {
            HashSet<string> ancestors = AncestorsOf(identity);
            bool allowed = false;
            foreach (string item in named) if (ancestors.Contains(item)) { allowed = true; break; }
            if (allowed)
            {
                shouldKnowRows++;
                WorldKnowledgeQueryResult shouldKnow = AskAs(identity, text);
                if (shouldKnow.HitIds.Contains(target)) shouldKnowHit++;
                else shouldKnowMisses.Add(new JObject
                {
                    ["query"] = text,
                    ["target"] = target,
                    ["identity"] = identity,
                    ["state"] = shouldKnow.State
                });
                continue;
            }
            deniedRows++;
            sweepRows++;
            bool leaked = AskAs(mutate ? highest : identity, text).HitIds.Contains(target);
            if (leaked) { leakedRows++; if (!mutate) leaks.Add(identity); }
        }
        caseDetails.Add(new JObject
        {
            ["target"] = target,
            ["query"] = text,
            ["namedByAuthoring"] = new JArray(named.OrderBy(x => x, StringComparer.Ordinal)),
            ["deniedRows"] = deniedRows,
            ["leaks"] = new JArray(leaks)
        });
        Console.WriteLine($"  {text,-26} {target.Substring(target.LastIndexOf('.') + 1),-32} 排除 {deniedRows,2} 个身份，泄漏 {leaks.Count}"
            + (leaks.Count > 0 ? " ⇒ " + string.Join(",", leaks) : string.Empty));
    }

    Console.WriteLine();
    Console.WriteLine($"=== 点名题：他不该知道（{deniedCases.Count} 条，每条都带阳性对照）===");
    int deniedFail = 0;
    var deniedDetails = new JArray();
    foreach (JToken token in deniedCases)
    {
        string text = token.Value<string>("query") ?? string.Empty;
        string target = token.Value<string>("target") ?? string.Empty;
        string allowedId = token.Value<string>("allowedIdentity") ?? string.Empty;
        string deniedId = token.Value<string>("deniedIdentity") ?? string.Empty;
        string forbidden = token.Value<string>("forbiddenText") ?? string.Empty;
        string required = token.Value<string>("requiredText") ?? string.Empty;
        // 缺字段 ⇒ 直接判红：静默当成"这一条不用查"正是本项目反复吃亏的那类失败
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(target)
            || string.IsNullOrWhiteSpace(allowedId) || string.IsNullOrWhiteSpace(deniedId))
        {
            Console.WriteLine("  [无效点名题] query/target/allowedIdentity/deniedIdentity 四个字段缺一不可");
            return 1;
        }

        WorldKnowledgeQueryResult allowedResult = AskAs(allowedId, text);
        bool allowedHit = allowedResult.HitIds.Contains(target);
        // 阳性对照：被禁的那一句，必须**在够格的身份那里真能出现** —— 否则"他没漏"是空转（恒 True＝没测）。
        // 分层题的"够细那一档"常常另有其人（例：村民只配 rumor、镇上人才配 summary），所以对照身份可另指。
        string controlId = token.Value<string>("forbiddenControlIdentity") ?? allowedId;
        string controlText = StringComparer.Ordinal.Equals(controlId, allowedId)
            ? (allowedResult.RetrievedText ?? string.Empty)
            : (AskAs(controlId, text).RetrievedText ?? string.Empty);
        bool forbiddenReachable = string.IsNullOrEmpty(forbidden) || controlText.Contains(forbidden);

        // 受限那一方**到底该不该看见这条**：由授权数据判（与阴性扫描同一个 oracle），**不由题面写死**。
        //   看得见 ⇒ 他该拿到的是**低一档**的版本：assert 拿到 + 有糙版本 + 无细档；
        //   看不见 ⇒ 整条都不该出现：assert 不得命中，且**不许**给 requiredText（给了＝题写反了）。
        WorldKnowledgeEntry deniedEntry;
        bool expectVisible = false;
        if (gateSnapshot.Entries.TryGetValue(target, out deniedEntry))
        {
            HashSet<string> namedForDenied = NamedByAuthoring(deniedEntry);
            if (namedForDenied == null) expectVisible = true;   // 有 public 授权 ⇒ 人人可见
            else
            {
                HashSet<string> deniedAncestors = AncestorsOf(deniedId);
                foreach (string item in namedForDenied)
                    if (deniedAncestors.Contains(item)) { expectVisible = true; break; }
            }
        }
        if (!expectVisible && !string.IsNullOrEmpty(required))
        {
            Console.WriteLine("  [无效点名题] " + deniedId + " 按授权根本看不见 " + target + "，却又要求它必须拿到「糙版本」——题写反了");
            return 1;
        }

        WorldKnowledgeQueryResult deniedResult = AskAs(mutate ? allowedId : deniedId, text);
        string deniedText = deniedResult.RetrievedText ?? string.Empty;
        bool deniedHit = deniedResult.HitIds.Contains(target);
        bool forbiddenLeaked = !string.IsNullOrEmpty(forbidden) && deniedText.Contains(forbidden);
        bool requiredMissing = !string.IsNullOrEmpty(required) && !deniedText.Contains(required);
        bool visibilityOk = expectVisible ? deniedHit : !deniedHit;

        bool ok = allowedHit && forbiddenReachable && visibilityOk && !forbiddenLeaked && !requiredMissing;
        if (!ok) deniedFail++;
        Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {text}   → {target.Substring(target.LastIndexOf('.') + 1)}");
        Console.WriteLine($"      阳性对照 {allowedId,-30} 拿到={allowedHit} 该细档真能出现={forbiddenReachable} state={allowedResult.State}");
        Console.WriteLine($"      受限一方 {deniedId,-30} 应可见={expectVisible} 可见性符合={visibilityOk} 漏细档={forbiddenLeaked}"
            + (string.IsNullOrEmpty(required) ? string.Empty : " 糙版本到位=" + !requiredMissing)
            + " state=" + deniedResult.State + (mutate ? "（变异中：冒充 " + allowedId + "）" : string.Empty));
        deniedDetails.Add(new JObject
        {
            ["query"] = text,
            ["target"] = target,
            ["allowedIdentity"] = allowedId,
            ["deniedIdentity"] = deniedId,
            ["expectVisible"] = expectVisible,
            ["allowedHit"] = allowedHit,
            ["forbiddenReachable"] = forbiddenReachable,
            ["deniedHit"] = deniedHit,
            ["visibilityOk"] = visibilityOk,
            ["forbiddenLeaked"] = forbiddenLeaked,
            ["requiredMissing"] = requiredMissing,
            ["note"] = token.Value<string>("note") ?? string.Empty
        });
    }

    bool vacuous = sweepRows == 0 || shouldKnowRows == 0 || shouldKnowHit == 0;
    bool pass = leakedRows == 0 && deniedFail == 0 && !vacuous;

    var report = new JObject
    {
        ["package"] = gateSnapshot.PackageId,
        ["semantic"] = wantSemantic,
        ["mutate"] = mutate,
        ["identityCount"] = gateIdentities.Length,
        ["sweepDeniedRows"] = sweepRows,
        ["sweepLeaks"] = leakedRows,
        ["shouldKnowRows"] = shouldKnowRows,
        ["shouldKnowHit"] = shouldKnowHit,
        ["deniedCasesFail"] = deniedFail,
        ["cases"] = caseDetails,
        ["shouldKnowMisses"] = shouldKnowMisses,
        ["deniedCases"] = deniedDetails
    };
    File.WriteAllText(outPath, report.ToString(Newtonsoft.Json.Formatting.Indented));

    Console.WriteLine();
    Console.WriteLine($"扫描：被排除行 {sweepRows}（泄漏 {leakedRows}）；该知道行 {shouldKnowRows}（真拿到 {shouldKnowHit}）；点名题失败 {deniedFail}");
    if (shouldKnowMisses.Count > 0)
    {
        // **只报不判**：这一列混着"召回没找到"与"能力不够"，要分诊（见 docs/AUDIT-20260917… 与 identity-gate 报告）。
        Console.WriteLine($"⚠️ 该知道却没拿到 {shouldKnowMisses.Count} 行（只报不判，待分诊：召回 or 能力）——前 8 行：");
        foreach (JObject miss in shouldKnowMisses.Take(8))
            Console.WriteLine("    " + miss["query"] + " × " + miss["identity"] + "  state=" + miss["state"]);
    }
    if (vacuous) Console.WriteLine("⚠️ 空转：没有产生任何有效行 ⇒ 恒 PASS 不算证据（「恒 True＝没测」）");
    Console.WriteLine($"IDENTITY_GATE {(pass ? "PASS" : "FAIL")} need leaks=0 deniedFail=0 且非空转（deniedRows>0 且 shouldKnowHit>0）");
    Console.WriteLine("报告已导出: " + outPath);
    return pass ? 0 : 1;
}

// ---------- probe 模式实现 ----------

int RunProbeMode(string[] a)
{
    if (a.Length < 3)
    {
        Console.WriteLine("用法: probe <manifest.json> <spec.json> <out.json>");
        return 2;
    }
    string manifest = a[0];
    if (!File.Exists(manifest))
    {
        Console.WriteLine("找不到 manifest: " + manifest);
        return 2;
    }
    var spec = JObject.Parse(File.ReadAllText(a[1]));
    string workDir = Path.Combine(Path.GetTempPath(), "awake-runtime-sim", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(workDir);
    foreach (string name in new[] { "manifest.json", "runtime.json", "index.json" })
    {
        string src = Path.Combine(Path.GetDirectoryName(manifest)!, name);
        if (File.Exists(src)) File.Copy(src, Path.Combine(workDir, name), true);
    }
    var snapshot = WorldKnowledgeLoader.Load(Path.Combine(workDir, "manifest.json"));
    var service = new WorldKnowledgeQueryService(snapshot);
    Console.WriteLine($"probe: package={snapshot.PackageId} entries={snapshot.Entries.Count}");

    var results = new JArray();
    foreach (var q in spec["queries"] ?? new JArray())
    {
        string identity = q.Value<string>("identity") ?? "";
        string role = q.Value<string>("role") ?? "";
        (string scope, string detail) = CapabilityOf(identity);
        if (q["scope"] != null) scope = q.Value<string>("scope");
        if (q["detail"] != null) detail = q.Value<string>("detail");
        var wbq = new WorldbookQuery
        {
            IdentityId = identity,
            Role = role,
            CultureId = q.Value<string>("culture") ?? "",
            KingdomId = q.Value<string>("kingdom") ?? "",
            SettlementId = q.Value<string>("settlement") ?? "",
            Age = q.Value<int?>("age") ?? 0,
            IsClanLeader = q.Value<bool?>("is_clan_leader") ?? false,
            KnowledgeScope = scope,
            KnowledgeScopeAvailable = !string.IsNullOrEmpty(scope),
            EffectiveDetail = detail,
            EffectiveDetailAvailable = !string.IsNullOrEmpty(detail),
            RequestedDetail = q.Value<string>("requested_detail") ?? "secret",
            PlayerText = q.Value<string>("text") ?? "",
            MaximumBytes = 4096
        };
        var mgmt = q["management"];
        if (mgmt != null) wbq.Skills["management"] = mgmt.Value<int>();
        var r = service.Query(wbq);
        string text = (r.RetrievedText ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
        Console.WriteLine($"  [{q.Value<string>("name")}] identity={identity} role={role} age={wbq.Age} scope={scope} detail={detail} -> state={r.State} text={Trunc(text, 70)}");
        results.Add(new JObject
        {
            ["name"] = q.Value<string>("name"),
            ["identity"] = identity,
            ["role"] = role,
            ["culture"] = wbq.CultureId,
            ["kingdom"] = wbq.KingdomId,
            ["settlement"] = wbq.SettlementId,
            ["age"] = wbq.Age,
            ["scope"] = scope,
            ["detail"] = detail,
            ["requested_detail"] = wbq.RequestedDetail,
            ["state"] = r.State,
            ["hits"] = new JArray(r.HitIds),
            ["text"] = text
        });
    }
    File.WriteAllText(a[2], results.ToString(Newtonsoft.Json.Formatting.Indented));
    Console.WriteLine("probe 结果已导出: " + a[2]);
    Console.WriteLine("PROBE-OK");
    return 0;
}

static (string Scope, string Detail) CapabilityOf(string identity)
{
    var cap = identity.Trim().ToLowerInvariant() switch
    {
        "profile.commoner" => ("local", "rumor"),
        "profile.villager" => ("local", "rumor"),
        "profile.townsfolk" => ("regional", "summary"),
        "profile.notable" => ("regional", "detail"),
        "profile.headman" => ("national", "detail"),
        "profile.merchant" => ("faction", "detail"),
        "profile.tavernkeeper" => ("faction", "detail"),
        "profile.ransom_broker" => ("faction", "detail"),
        "profile.soldier" => ("national", "detail"),
        "profile.noble" => ("elite", "detail"),
        "profile.noble_high_steward" => ("elite", "detail"),
        _ => ("", "")
    };
    return cap;
}

static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "...";

int RunFullContextMode(string[] a)
{
    if (a.Length < 1 || string.IsNullOrWhiteSpace(a[0]))
    {
        Console.WriteLine("用法: full-context <out.json>");
        return 2;
    }

    const int startDay = 8;
    const int endDay = 14;
    if (!WorldFactCapture.TryCreateWar(9, 9 * 144L + 12L, "faction:empire", "西帝国", "faction:battania", "巴旦尼亚", out WorldFactCapture war)
        || !WorldFactCapture.TryCreateSettlementOwnerChanged(11, 11 * 144L + 31L, "settlement:maranath", "马拉纳斯", "hero:old", "hero:new", "新领主", out WorldFactCapture ownerChanged)
        || !WorldFactCapture.TryCreateHeroKilled(13, 13 * 144L + 7L, "hero:fallen", "一名边境贵族", "hero:new", out WorldFactCapture killed))
    {
        Console.WriteLine("无法创建 full-context 事实 fixture");
        return 1;
    }

    JObject[] facts = { war.Fact.ToJson(), ownerChanged.Fact.ToJson(), killed.Fact.ToJson() };
    WorldFactQueryResult result = new WorldFactQueryResult(
        WorldFactQueryStatus.Success,
        WorldFactSelectionPolicy.WeeklyDynamics,
        facts,
        windowStartDay: startDay,
        windowEndDay: endDay,
        journalRevision: 1);
    WeeklyDynamicsInput input;
    string inputError = string.Empty;
    JObject report = null;
    string reportError = string.Empty;
    string validationError = string.Empty;
    bool validInput = WeeklyDynamicsInput.TryCreate(result, out input, out inputError);
    bool builtReport = validInput && WeeklyReportService.TryBuildV2FromFacts(input, out report, out reportError);
    bool validReport = builtReport && WeeklyReportService.TryValidateV2Report(report, out validationError);
    if (!validInput || !builtReport || !validReport)
    {
        Console.WriteLine("full-context 生成失败: " + (validationError ?? reportError ?? inputError));
        return 1;
    }

    JObject output = new JObject
    {
        ["chainVersion"] = "awake.ai.simulation-context.v1",
        ["source"] = "WorldFactCapture + WorldFactQueryResult + WeeklyReportService.TryBuildV2FromFacts",
        ["windowStartDay"] = startDay,
        ["windowEndDay"] = endDay,
        ["facts"] = new JArray(facts),
        ["report"] = report,
        ["reportText"] = WeeklyReportService.BuildText(report, "本周动态", "本周没有记录。"),
        ["validation"] = new JObject
        {
            ["reportV2"] = true,
            ["sourceFactCount"] = facts.Length,
            ["sourceFactIds"] = new JArray(facts.Select(value => (string)value["factId"]))
        }
    };
    string outputPath = Path.GetFullPath(a[0]);
    string parent = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
    File.WriteAllText(outputPath, output.ToString(Newtonsoft.Json.Formatting.Indented));
    Console.WriteLine("full-context 结果已导出: " + outputPath);
    Console.WriteLine("FULL-CONTEXT-OK");
    return 0;
}
