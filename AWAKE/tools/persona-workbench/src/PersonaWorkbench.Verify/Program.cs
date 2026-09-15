using System.Text.Json;
using PersonaWorkbench.Core;

/*
 * PersonaWorkbench.Verify —— 编译门禁验证工具
 *
 * 用途：逐张加载 characters/*.persona.json，走真实编译链
 *       (PersonaDocumentCodec.Deserialize → PersonaValidator.Validate → PersonaAuthoringV2Adapter.Build)
 *       输出 JSON 格式的逐卡错误清单和汇总。
 *
 * 用法：dotnet run --project PersonaWorkbench.Verify.csproj -- <charactersDir> <outputJson>
 *   - charactersDir：包含 *.persona.json 的目录
 *   - outputJson：输出报告路径
 *
 * 另一种模式（全库产出体检，2026-09-15 加）：
 *   dotnet run --project PersonaWorkbench.Verify.csproj -- --corpus-audit <charactersDir> [reportJson]
 *   管的是**一批卡之间**的内容一致性（跨卡重复 / 槽位骨架 / 零信息句 / 卡内重复 / 抄了界面示例文字），
 *   与上面的编译门禁互补：那个管「单卡结构合不合法」，这个管「卡与卡之间有没有互相抄」。
 *   有 error 级发现时退出码 1。
 *
 * 退出码：0 = 无问题；1 = 有失败；2 = 参数错误
 */

// 全库产出体检模式
if (args.Length >= 2 && args[0] == "--corpus-audit")
{
    var auditDir = args[1];
    if (!Directory.Exists(auditDir))
    {
        Console.Error.WriteLine($"Characters directory not found: {auditDir}");
        return 2;
    }

    var auditRegistry = PersonaTagRegistry.CreateDefault();
    var loadedCards = new List<PersonaDocument>();
    var unreadableCards = new List<string>();
    foreach (var file in Directory.GetFiles(auditDir, "*.persona.json").OrderBy(f => f, StringComparer.Ordinal))
    {
        try { loadedCards.Add(PersonaDocumentStore.Read(file, auditRegistry)); }
        catch (Exception) { unreadableCards.Add(Path.GetFileName(file)); }
    }

    var auditReport = PersonaCorpusAudit.Analyze(loadedCards);
    Console.WriteLine($"CARDS={auditReport.CardCount}");
    Console.WriteLine($"FINDINGS={auditReport.Findings.Count}  ERRORS={auditReport.ErrorCount}  WARNINGS={auditReport.WarningCount}");
    if (unreadableCards.Count > 0)
    {
        Console.WriteLine($"UNREADABLE={unreadableCards.Count}  [{string.Join(",", unreadableCards)}]");
    }

    foreach (var finding in auditReport.Findings)
    {
        Console.WriteLine();
        Console.WriteLine($"[{finding.Severity}] {finding.Rule}  ({finding.Field})");
        Console.WriteLine($"  cards: {string.Join("、", finding.Cards)}");
        Console.WriteLine($"  text : {Describe(finding.Text, 60)}");
        Console.WriteLine($"  why  : {finding.Message}");
    }

    if (args.Length >= 3)
    {
        File.WriteAllText(args[2], JsonSerializer.Serialize(new
        {
            schemaVersion = "awake.persona.corpus-audit.v1",
            cardCount = auditReport.CardCount,
            errorCount = auditReport.ErrorCount,
            warningCount = auditReport.WarningCount,
            unreadableFiles = unreadableCards,
            findings = auditReport.Findings
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"RESULT_FILE={args[2]}");
    }

    return auditReport.ErrorCount > 0 ? 1 : 0;
}

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: PersonaWorkbench.Verify <charactersDir> <outputJson>");
    Console.Error.WriteLine("       PersonaWorkbench.Verify --corpus-audit <charactersDir> [reportJson]");
    return 2;
}

var charsDir = args[0];
var outPath = args[1];

if (!Directory.Exists(charsDir))
{
    Console.Error.WriteLine($"Characters directory not found: {charsDir}");
    return 2;
}

var registry = PersonaTagRegistry.CreateDefault();
var looseOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

var allowedRoot = new HashSet<string>(StringComparer.Ordinal)
{
    "schemaVersion","id","displayName","core","identityFacts","summary","sourcePackId","templateVersion",
    "status","sourceDescription","publicDescription","privateDescription","contradictionDescription",
    "selfClaimRules","realSelfBehaviors","selfClaimExamples","tensionAxes","tags","facetStrengths",
    "traitProfile","expressionProfile","behaviorProfile","reactionProfile","commitmentProfile"
};

var files = Directory.GetFiles(charsDir, "*.persona.json")
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToArray();

var codeCards = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
var outOfRegistryTags = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
var outOfRegistryFacets = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
var cards = new List<object>();

void Add(SortedDictionary<string, SortedSet<string>> map, string key, string card)
{
    if (!map.TryGetValue(key, out var set)) { set = new SortedSet<string>(StringComparer.Ordinal); map[key] = set; }
    set.Add(card);
}

static string Describe(string text, int limit) => text.Length <= limit ? text : text[..limit] + "…";

var printedBuildErrors = new HashSet<string>(StringComparer.Ordinal);
foreach (var file in files)
{
    var baseName = Path.GetFileNameWithoutExtension(file);
    var name = baseName.EndsWith(".persona", StringComparison.Ordinal)
        ? baseName[..^".persona".Length]
        : baseName;

    var json = File.ReadAllText(file);

    // Layer 1: root whitelist (mirrors PersonaDocumentCodec.ValidateRootShape)
    var unknownRoot = new List<string>();
    using (var parsed = JsonDocument.Parse(json))
    {
        foreach (var p in parsed.RootElement.EnumerateObject())
        {
            if (!allowedRoot.Contains(p.Name))
            {
                unknownRoot.Add(p.Name);
                Add(codeCards, "root." + p.Name, name);
            }
        }
    }

    // Layer 1b: authoritative load path
    string loadError = "";
    try { PersonaDocumentCodec.Deserialize(json, registry); }
    catch (PersonaDocumentFormatException ex)
    {
        loadError = ex.Code;
        Add(codeCards, "load." + ex.Code, name);
    }
    catch (Exception ex)
    {
        loadError = "EX:" + ex.GetType().Name;
        Add(codeCards, "load.EXCEPTION", name);
    }

    // Layer 2: loose deserialize + full validator error list
    var validatorCodes = new List<string>();
    bool buildOk = false;
    string buildErrorCode = "";
    var buildDiagCodes = new List<string>();

    try
    {
        var doc = JsonSerializer.Deserialize<PersonaDocument>(json, looseOptions);
        if (doc == null)
        {
            validatorCodes.Add("document_null");
            Add(codeCards, "document_null", name);
        }
        else
        {
            var vr = PersonaValidator.Validate(doc, registry);
            foreach (var e in vr.Errors)
            {
                validatorCodes.Add(e.Code);
                Add(codeCards, e.Code, name);
            }

            // Collect out-of-registry keys for reporting
            foreach (var t in doc.Tags ?? new List<string>())
                if (!registry.TryGet(t, out _)) Add(outOfRegistryTags, t, name);
            foreach (var k in (doc.FacetStrengths ?? new()).Keys)
                if (!registry.TryGet(k, out _)) Add(outOfRegistryFacets, k, name);

            // Layer 3: migration build
            var build = PersonaAuthoringV2Adapter.Build(doc, string.Empty, "none");
            buildOk = build.IsSuccess;
            buildErrorCode = build.ErrorCode ?? "";
            foreach (var d in build.Diagnostics)
            {
                buildDiagCodes.Add(d.Code);
                Add(codeCards, "build." + d.Code, name);
            }
            if (!buildOk && build.Diagnostics.Count > 0)
            {
                var firstCode = build.Diagnostics[0].Code;
                if (!printedBuildErrors.Contains(firstCode))
                {
                    printedBuildErrors.Add(firstCode);
                    Console.Error.WriteLine($"=== Build failure detail ({firstCode}) ===");
                    Console.Error.WriteLine($"Card: {name}");
                    foreach (var d in build.Diagnostics)
                    {
                        Console.Error.WriteLine($"  DIAG {d.Code}: {d.Message}");
                    }
                    foreach (var w in build.Warnings.Take(10))
                    {
                        Console.Error.WriteLine($"  WARN: {w}");
                    }
                    Console.Error.WriteLine("=== end ===");
                }
            }
            if (buildOk) Add(codeCards, "build.ok", name);
        }
    }
    catch (Exception ex)
    {
        var msg = "EX:" + ex.GetType().Name + ":" + ex.Message.Split('\n')[0];
        validatorCodes.Add(msg);
        Add(codeCards, "deserialize.EXCEPTION", name);
    }

    cards.Add(new
    {
        file = baseName,
        unknownRoot = unknownRoot,
        loadError = loadError,
        validatorCodes = validatorCodes,
        buildOk = buildOk,
        buildErrorCode = buildErrorCode,
        buildDiagCodes = buildDiagCodes
    });
}

var summary = codeCards.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);

var result = new
{
    schemaVersion = "awake.persona.compile-verify.v1",
    totalCards = files.Length,
    registryKeyCount = registry.Ids.Count,
    registryKeys = registry.Ids,
    summary = summary,
    outOfRegistryTags = outOfRegistryTags.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal),
    outOfRegistryFacets = outOfRegistryFacets.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal),
    cards = cards
};

var dir = Path.GetDirectoryName(outPath);
if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
File.WriteAllText(outPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

// Console summary
Console.WriteLine($"TOTAL_CARDS={files.Length}");
Console.WriteLine($"REGISTRY_KEYS={registry.Ids.Count}");

var passCount = codeCards.TryGetValue("build.ok", out var okSet) ? okSet.Count : 0;
Console.WriteLine($"BUILD_PASS={passCount}");
Console.WriteLine($"BUILD_FAIL={files.Length - passCount}");

Console.WriteLine("--- error summary (code -> card count) ---");
foreach (var kv in codeCards.OrderBy(k => k.Key, StringComparer.Ordinal))
{
    Console.WriteLine($"{kv.Key}  {kv.Value.Count}  [{string.Join(",", kv.Value)}]");
}

Console.WriteLine($"RESULT_FILE={outPath}");

return passCount == files.Length ? 0 : 1;
