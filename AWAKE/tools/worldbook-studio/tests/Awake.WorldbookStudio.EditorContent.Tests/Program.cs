using System.Linq;
using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

var passed = 0;
var total = 0;

Run("title path targets basic information", () =>
{
    var target = AuthoringCandidateProjection.Project(new KnowledgePatch("knowledge-patch.v1", [
        new KnowledgePatchOperation("replace", "/title/zh-CN", JsonValue.Create("标题"))]));
    Assert(target is not null && target.Step == 0 && target.StepTitle == "基本信息" && target.Label == "档案标题" && target.FocusId == "doc-title" && target.OperationCount == 1, "title path should target the basic information field");
});

Run("fact path targets the corresponding fact card", () =>
{
    var target = AuthoringCandidateProjection.Project(new KnowledgePatch("knowledge-patch.v1", [
        new KnowledgePatchOperation("replace", "/assertions/3/text/zh-CN", JsonValue.Create("事实"))]));
    Assert(target is not null && target.Step == 1 && target.StepTitle == "客观事实" && target.Label == "客观事实" && target.FocusId == "fact-3-text", "fact path should target the corresponding fact card");
});

Run("expression path targets the corresponding expression card", () =>
{
    var target = AuthoringCandidateProjection.Project(new KnowledgePatch("knowledge-patch.v1", [
        new KnowledgePatchOperation("replace", "/assertions/2/expressions/4/text/zh-CN", JsonValue.Create("表达"))]));
    Assert(target is not null && target.Step == 2 && target.StepTitle == "NPC 表达" && target.Label == "NPC 表达" && target.FocusId == "expression-2-4-text", "expression path should target the corresponding expression card");
});

Run("multi-operation patches stay out of author mode", () =>
{
    var target = AuthoringCandidateProjection.Project(new KnowledgePatch("knowledge-patch.v1", [
        new KnowledgePatchOperation("replace", "/title/zh-CN", JsonValue.Create("标题")),
        new KnowledgePatchOperation("replace", "/assertions/0/text/zh-CN", JsonValue.Create("事实"))]));
    Assert(target is null, "multi-operation patches should not pretend that the first author target is sufficient");
});

Run("source-backed documents reject AI candidate application", () =>
{
    var sourceDocument = new JsonObject
    {
        ["id"] = "doc.source",
        ["sources"] = new JsonArray()
    };
    AssertThrowsCode(() => AiCandidateGuards.EnsureApplyAllowed(sourceDocument), "WB-EDITOR-SOURCE-403");
});

Run("applying one AI candidate invalidates the rest of its batch", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-editor-content-tests", Guid.NewGuid().ToString("N"));
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, FindSchemaRoot()));
        workspace.Initialize();
        var request = MakeAssistanceRequest();
        var firstPatch = new KnowledgePatch("knowledge-patch.v1", [
            new KnowledgePatchOperation("replace", "/title/zh-CN", JsonValue.Create("第一个候选"))]);
        var secondPatch = new KnowledgePatch("knowledge-patch.v1", [
            new KnowledgePatchOperation("replace", "/summary/zh-CN", JsonValue.Create("第二个候选"))]);
        var result = new AssistanceResult("assistance.result.v1", request.RequestHash, request.SourceDocumentHash, [
            new AssistanceSuggestion("suggestion.editor.first", "prose", "info", 0.7, "第一个", "测试候选", "第一个候选", firstPatch, true),
            new AssistanceSuggestion("suggestion.editor.second", "prose", "info", 0.7, "第二个", "测试候选", "第二个候选", secondPatch, true)]);
        var store = new SuggestionStore(workspace);
        var saved = store.Save(request, result, "session-editor-content", "buffer-editor-content");
        store.ConsumeApplyNonce(saved[0].SuggestionId, request.DocumentId, request.SourceDocumentHash, request.SavedRevision, "session-editor-content", "buffer-editor-content", saved[0].ApplyNonce!);
        store.InvalidatePendingForBuffer(request.DocumentId, request.SourceDocumentHash, request.SavedRevision, "session-editor-content", "buffer-editor-content");
        AssertThrowsCode(() => store.Read(saved[1].SuggestionId, request.DocumentId, request.SourceDocumentHash, request.SavedRevision, "session-editor-content", "buffer-editor-content"), "WB-AI-SUGGESTION-404");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
});

Run("missing and protected patches stay out of author mode", () =>
{
    Assert(AuthoringCandidateProjection.Project(null) is null, "missing patches should not be author-mode candidates");
    Assert(AuthoringCandidateProjection.Project(new KnowledgePatch("knowledge-patch.v1", [
        new KnowledgePatchOperation("replace", "/authority/owner", JsonValue.Create("other"))])) is null, "protected paths should not be author-mode candidates");
});

Run("runtime package carries settlement anchors", () =>
{
    var schemaRoot = FindSchemaRoot();
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-editor-content-tests", Guid.NewGuid().ToString("N"));
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var service = new WorldbookApplicationService(workspace);
        service.CreateGeneratedDocument("加伦锚点档案", "geography", "base", "author.developer", "settlements", new[] { "politics" }, null, null, new[] { "entity.settlement.town_v5" });
        var compiled = service.Compile();
        var runtime = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Encoding.UTF8.GetString(compiled.Files["runtime.json"]))!.AsObject();
        var refs = runtime["entries"]?[0]?["extensions"]?["entityRefs"]?.AsArray().Select(x => x!.GetValue<string>()).ToArray() ?? [];
        Assert(refs.Contains("awake:settlement:town_v5"), "runtime entry must carry the canonical settlement anchor");

        service.CreateGeneratedDocument("非法锚点档案", "geography", "base", "author.developer", "settlements", new[] { "politics" }, null, null, new[] { "entity.bogus.thing" });
        try
        {
            service.Compile();
            throw new InvalidOperationException("expected WB-DOC-003");
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("WB-DOC-003", StringComparison.Ordinal))
        {
        }
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
});

Run("empty YAML scalar loads as null", () =>
{
    var loader = new SafeYamlLoader();
    var report = new ValidationReport();
    var document = loader.Load("memory.yaml", System.Text.Encoding.UTF8.GetBytes("a:\nb: ''\nc: null\n"), report);
    Assert(document["a"] is null, "bare empty scalar must load as null");
    Assert(document["b"] is not null && document["b"]!.GetValue<string>() == "", "quoted empty string must stay a string");
    Assert(document["c"] is null, "explicit null must load as null");
});

Run("era flows into created documents", () =>
{
    var schemaRoot = FindSchemaRoot();
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-editor-content-tests", Guid.NewGuid().ToString("N"));
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var service = new WorldbookApplicationService(workspace);
        var historical = service.CreateGeneratedDocument("历史测试档案", "politics", "base", "author.developer", "territories", new[] { "geography" }, "historical");
        Assert(historical.Document["era"]?["key"]?.GetValue<string>() == "historical", "explicit era must reach the created document");
        var current = service.CreateGeneratedDocument("现状测试档案", "geography", "base", "author.developer", "settlements", new[] { "politics" });
        Assert(current.Document["era"]?["key"]?.GetValue<string>() == "current", "default era must remain current");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
});

Run("R1 quote must be locatable inside source file", () =>
{
    var schemaRoot = FindSchemaRoot();
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-editor-content-tests", Guid.NewGuid().ToString("N"));
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var sources = Path.Combine(root, "authoring", "sources");
        Directory.CreateDirectory(sources);
        var text = File.ReadAllText(Path.Combine(schemaRoot, "fixture-valid-minimal.yaml"));
        var fabricated = "不存在的引文";
        var fabricatedHash = Hashing.Sha256Text(fabricated);
        text = text
            .Replace("quote: 示例", "quote: " + fabricated, StringComparison.Ordinal)
            .Replace("quote_hash: 0D53F08678D8101B0C7E5053B30224FC671C55051770BA18207435EAD7AD37BD", "quote_hash: " + fabricatedHash, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(root, "authoring", "demo.yaml"), text);
        File.Copy(Path.Combine(schemaRoot, "source-fixture-demo.yaml"), Path.Combine(sources, "source-demo.yaml"), true);
        File.Copy(Path.Combine(schemaRoot, "source-demo.txt"), Path.Combine(sources, "source-demo.txt"), true);
        var report = new WorldbookApplicationService(workspace).Validate();
        Assert(report.Diagnostics.Any(x => x.Code == "WB-SOURCE-001"), "quote containment must be rejected; got " + string.Join(",", report.Diagnostics.Select(x => x.Code)));
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
});

Console.WriteLine($"PASS: editor content core checks ({passed}/{total})");

void Run(string name, Action action)
{
    total++;
    action();
    passed++;
    Console.WriteLine($"PASS {name}");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAIL " + message);
}

static void AssertThrowsCode(Action action, string code)
{
    try
    {
        action();
    }
    catch (InvalidOperationException error) when (error.Message.StartsWith(code + ":", StringComparison.Ordinal))
    {
        return;
    }

    throw new InvalidOperationException($"FAIL expected {code}");
}

static AssistanceRequest MakeAssistanceRequest()
{
    var document = new JsonObject
    {
        ["schema_version"] = "awake.worldbook.authoring.v1",
        ["id"] = "doc.editor-content",
        ["title"] = new JsonObject { ["zh-CN"] = "测试档案" },
        ["assertions"] = new JsonArray()
    };
    return new AssistanceRequest(
        "assistance.request.v1",
        "test",
        AssistanceAnalysisKind.Consistency,
        "authoring/editor-content.yaml",
        "doc.editor-content",
        new string('b', 64),
        1,
        "nonce-editor-content",
        document,
        new JsonObject(),
        new string('a', 64));
}

static string FindSchemaRoot()
{
    var current = Path.GetFullPath(AppContext.BaseDirectory);
    while (!string.IsNullOrEmpty(current))
    {
        var candidate = Path.Combine(current, "docs", "worldbook-studio-plan");
        if (File.Exists(Path.Combine(candidate, "awake.worldbook.authoring.v1.schema.json"))) return candidate;
        current = Directory.GetParent(current)?.FullName ?? string.Empty;
    }
    throw new DirectoryNotFoundException("worldbook studio schema root not found");
}
