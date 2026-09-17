using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Awake;
using Newtonsoft.Json.Linq;

// AWAKE 对话链路 010 —— 世界书最小 v2 试点包出包器。
// 用与游戏完全相同的 WorldbookPackageIntegrity 计算哈希，产出 manifest.json / runtime.json / index.json，
// 并用 WorldKnowledgeQueryService + WorldKnowledgeDecisionPolicy 断言「目标 NPC 命中条目 → 会把知识喂给模型」。
// 2026-09-18：断言观测点从 AllowsAi 换成 BuildPromptBlock —— 「知道」与「开口」拆开之后，
// AllowsAi 只回答"让不让这一轮说话"，命中和没命中都是 true，区分不出正负路径。
// 不依赖 worldbookstudio。

string toolRoot = FindToolRoot();
string repoRoot = FindRepoRoot();
string sourcePath = Path.Combine(toolRoot, "pilot-runtime.source.json");
string outputRoot = Path.Combine(repoRoot, "release", "awake-worldbook-pilot");

if (!File.Exists(sourcePath)) throw new FileNotFoundException("pilot source not found: " + sourcePath);

string sourceText = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(sourcePath));
JObject runtime = (JObject)JObject.Parse(sourceText);
JArray entries = runtime["entries"] as JArray ?? throw new InvalidOperationException("source entries missing");

var keywordIndex = new JObject();
var domainIndex = new JObject();
foreach (JObject entry in entries.Children<JObject>())
{
    string entryId = (string)entry["id"] ?? string.Empty;
    if (string.IsNullOrWhiteSpace(entryId)) throw new InvalidOperationException("entry id missing");
    foreach (string keyword in (entry["keywords"] as JArray ?? new JArray()).Select(x => (string)x).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        AddIndex(keywordIndex, keyword, entryId);
    AddIndex(domainIndex, (string)entry["domain"] ?? string.Empty, entryId);
}
runtime["indexes"] = new JObject { ["keywordToEntryIds"] = keywordIndex, ["domainToEntryIds"] = domainIndex };

var index = new JObject
{
    ["schemaVersion"] = "awake.worldbook.index.v1",
    ["entryIds"] = new JArray(entries.Children<JObject>().Select(x => (string)x["id"] ?? string.Empty)),
    ["keywordToEntryIds"] = keywordIndex.DeepClone(),
    ["domainToEntryIds"] = domainIndex.DeepClone()
};

string packageId = (string)runtime["packageId"] ?? string.Empty;
string version = (string)runtime["version"] ?? string.Empty;
string worldId = (string)runtime["worldId"] ?? string.Empty;
var manifest = new JObject
{
    ["schemaVersion"] = "awake.worldbook.v2",
    ["packageId"] = packageId,
    ["version"] = version,
    ["kind"] = "universe",
    ["displayName"] = new JObject { ["zh-CN"] = "AWAKE 对话链路验收小样" },
    ["worldId"] = worldId,
    ["entrypoints"] = new JObject { ["runtime"] = "runtime.json", ["index"] = "index.json" },
    ["hashes"] = new JObject()
};
string manifestHash = WorldbookPackageIntegrity.ComputeManifestHash(manifest);
string contentHash = WorldbookPackageIntegrity.ComputeContentHash("runtime.json", runtime, "index.json", index);
string packageHash = WorldbookPackageIntegrity.ComputePackageHash(manifestHash, contentHash);
manifest["hashes"] = new JObject
{
    ["manifestHash"] = manifestHash,
    ["contentHash"] = contentHash,
    ["packageHash"] = packageHash
};

Directory.CreateDirectory(outputRoot);
var utf8 = new UTF8Encoding(false);
File.WriteAllText(Path.Combine(outputRoot, "runtime.json"), runtime.ToString(Newtonsoft.Json.Formatting.None), utf8);
File.WriteAllText(Path.Combine(outputRoot, "index.json"), index.ToString(Newtonsoft.Json.Formatting.None), utf8);
File.WriteAllText(Path.Combine(outputRoot, "manifest.json"), manifest.ToString(Newtonsoft.Json.Formatting.None), utf8);

WorldbookVerifiedPackage verified = WorldbookPackageIntegrity.ReadAndVerify(
    Path.Combine(outputRoot, "manifest.json"), packageId, version, "universe", manifestHash, contentHash, packageHash);

WorldKnowledgeSnapshot snapshot = WorldKnowledgeLoader.LoadVerified(verified);
WorldKnowledgeQueryService service = new WorldKnowledgeQueryService(snapshot);

Console.WriteLine("pilot package  : " + outputRoot);
Console.WriteLine("packageId      : " + packageId);
Console.WriteLine("entries        : " + snapshot.Entries.Count);
Console.WriteLine("identities     : " + snapshot.Identities.Count);
Console.WriteLine("manifestHash   : " + manifestHash);
Console.WriteLine("contentHash    : " + contentHash);
Console.WriteLine("packageHash    : " + packageHash);
Console.WriteLine();

var probes = new (string Label, string Role, bool IsNoble, int Age, string PlayerText)[]
{
    ("村民", "villager", false, 34, "今年收成怎么样？"),
    ("士兵", "soldier", false, 28, "路上有劫匪吗？"),
    ("贵族", "noble", true, 47, "你的封地对谁效忠？"),
    ("商人", "merchant", false, 41, "商队走哪条路安全？")
};

foreach (var probe in probes)
{
    WorldbookIdentityCapabilityProfile profile = WorldbookIdentityCapabilityRules.Resolve(probe.Role, probe.IsNoble, probe.Age, 0);
    WorldbookQuery query = new WorldbookQuery
    {
        IdentityId = profile.ProfileId,
        Role = WorldbookEntityId.Canonical("role", profile.Role),
        KnowledgeScope = profile.KnowledgeScope,
        KnowledgeScopeAvailable = profile.KnowledgeScopeAvailable,
        EffectiveDetail = profile.EffectiveDetail,
        EffectiveDetailAvailable = profile.EffectiveDetailAvailable,
        ContentTier = "pure",
        RequestedDetail = "secret",
        PlayerText = probe.PlayerText,
        MaximumBytes = 4096,
        Age = probe.Age
    };
    WorldKnowledgeQueryResult result = service.Query(query);
    WorldKnowledgeDecision decision = WorldKnowledgeDecisionPolicy.Create(query, result, "pilot");
    // 2026-09-18：这里要验的是「命中了条目就该把知识喂给模型」，所以看知识块。
    // 原先看的是 AllowsAi —— 它已拆成"让不让这一轮说话"，not_found 也是 true，区分不出正负路径。
    Require(!string.IsNullOrWhiteSpace(WorldKnowledgeDecisionPolicy.BuildPromptBlock(decision)),
        "probe failed (no knowledge block): " + probe.Label + " state=" + decision.State + " reason=" + decision.BlockedReason);
    Console.WriteLine("[OK] " + probe.Label + "  id=" + profile.ProfileId
        + "  role=" + profile.Role
        + "  state=" + decision.State
        + "  hits=" + string.Join(",", decision.HitIds)
        + "  text=" + Shorten(decision.RetrievedText));
}

WorldbookQuery negativeQuery = new WorldbookQuery
{
    IdentityId = "profile.villager",
    Role = WorldbookEntityId.Canonical("role", "villager"),
    KnowledgeScope = "local",
    KnowledgeScopeAvailable = true,
    EffectiveDetail = "rumor",
    EffectiveDetailAvailable = true,
    ContentTier = "pure",
    RequestedDetail = "secret",
    PlayerText = "今天天气不错",
    MaximumBytes = 4096
};
WorldKnowledgeDecision negativeDecision = WorldKnowledgeDecisionPolicy.Create(negativeQuery, service.Query(negativeQuery), "pilot");
// 2026-09-18：负路径原来断言 !AllowsAi —— 那与「拆开知道与开口」直接相反，拆开后必然红。
// 现在分开验两件事：**不喂知识**（该空手时空手），且**仍然可以开口**（这正是本次改动的行为）。
Require(string.IsNullOrWhiteSpace(WorldKnowledgeDecisionPolicy.BuildPromptBlock(negativeDecision)),
    "negative probe should not feed knowledge");
Require(negativeDecision.AllowsAi,
    "negative probe (not_found) should still be allowed to speak");
Console.WriteLine("[OK] 负路径（无关键词命中）不喂知识 · 仍可开口 state=" + negativeDecision.State);
Console.WriteLine();
Console.WriteLine("PILOT-PACKAGE-OK");

static void AddIndex(JObject index, string key, string entryId)
{
    if (string.IsNullOrWhiteSpace(key)) return;
    if (index[key] is not JArray values)
    {
        values = new JArray();
        index[key] = values;
    }
    if (!values.Any(x => StringComparer.Ordinal.Equals((string)x, entryId))) values.Add(entryId);
}

static string Shorten(string value)
{
    string flat = (value ?? string.Empty).Replace(Environment.NewLine, " / ");
    return flat.Length <= 48 ? flat : flat.Substring(0, 48) + "…";
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string FindToolRoot()
{
    DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 10 && current != null; i++, current = current.Parent)
    {
        if (File.Exists(Path.Combine(current.FullName, "pilot-runtime.source.json"))) return current.FullName;
    }
    throw new DirectoryNotFoundException("pilot tool root not found (pilot-runtime.source.json)");
}

static string FindRepoRoot()
{
    DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 10 && current != null; i++, current = current.Parent)
    {
        if (File.Exists(Path.Combine(current.FullName, "src", "AwakeRuntime.cs"))) return current.FullName;
    }
    throw new DirectoryNotFoundException("AWAKE repo root not found");
}
