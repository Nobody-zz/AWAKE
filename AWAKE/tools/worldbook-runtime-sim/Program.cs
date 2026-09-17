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
if (Environment.GetEnvironmentVariable("AWAKE_SIM_SEMANTIC") == "1")
{
    string simModelDir = Environment.GetEnvironmentVariable("AWAKE_SIM_MODEL_DIR")
        ?? @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\ONNX";
    string simVocabPath = Environment.GetEnvironmentVariable("AWAKE_SIM_VOCAB")
        ?? @"D:\AWAKE-Dev\AWAKE\tools\_af_tokenizer_20260916\vocab.txt";
    string simDbPath = Path.Combine(Path.GetTempPath(), "awake-runtime-sim", "sim-semantic.db");

    Console.WriteLine("=== 语义臂 ===");
    AwakeSemanticArmBootstrap.Schedule(service, snapshot);
    AwakeBackgroundTask.LastTask?.Wait(TimeSpan.FromMinutes(5));
    Console.WriteLine($"[semantic] 世界书先到（host 未就位）⇒ HasSemanticIndex={service.HasSemanticIndex}，问过 host {AwakeRuntime.ResolveCallCount} 次");

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
    Console.WriteLine($"[semantic] 会话就绪后重试 ⇒ HasSemanticIndex={service.HasSemanticIndex}（条目 {snapshot.Entries.Count} 条）");
}


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
