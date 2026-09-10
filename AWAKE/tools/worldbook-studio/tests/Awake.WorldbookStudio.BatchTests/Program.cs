using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

var passed = 0;
var total = 0;
Run("contract registry loads approved revision", () =>
{
    var path = FindContractPath(AppContext.BaseDirectory);
    var registry = BatchAuthoringContractRegistry.Load(path);
    Assert(registry.Revision == BatchAuthoringContractConstants.Revision, "contract revision mismatch");
    Assert(registry.Sha256 == BatchAuthoringContractConstants.ApprovedSha256, "contract hash mismatch");
    Assert(registry.HasSchema("manifest"), "manifest schema missing");
    Assert(registry.HasRoute("create"), "create route missing");
    Assert(registry.RequireRoute("start")["request_ref"]?.GetValue<string>() == "api.start.request", "start request binding missing");
    foreach (var route in new[] { "scan", "create", "get", "consent", "start", "pause", "cancel", "claim", "retry-item", "review", "create-documents", "report", "item-detail", "source-unit", "prebatch-source-unit" })
        Assert(registry.HasRoute(route), $"route binding missing: {route}");
});
Run("contract registry rejects changed hash", () =>
{
    var source = FindContractPath(AppContext.BaseDirectory);
    var path = Path.Combine(Path.GetTempPath(), "awake-batch-contract-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var document = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        document["revision"] = 12;
        File.WriteAllText(path, document.ToJsonString());
        AssertThrowsCode(() => BatchAuthoringContractRegistry.Load(path), "WB-BATCH-CONTRACT-409");
    }
    finally { TryDelete(path); }
});
Run("batch path validator accepts safe source file", () =>
{
    var root = MakeRoot();
    try
    {
        var path = BatchPathValidator.RequireRelativePath(root, "source/a.txt", "source", [".txt", ".md"]);
        Assert(path.EndsWith(Path.Combine("source", "a.txt"), StringComparison.OrdinalIgnoreCase), "safe path was not resolved under root");
    }
    finally { TryDelete(root); }
});
Run("batch path validator rejects unsafe segments and absolute paths", () =>
{
    var root = MakeRoot();
    try
    {
        foreach (var value in new[] { "../outside.txt", "source//file.txt", "source/./file.txt", "source/../file.txt", Path.Combine(root, "file.txt"), "source\\file.txt" })
            AssertThrowsCode(() => BatchPathValidator.RequireRelativePath(root, value, "source"), "WB-BATCH-PATH-422");
    }
    finally { TryDelete(root); }
});
Run("batch path validator enforces extension and collision rules", () =>
{
    var root = MakeRoot();
    try
    {
        AssertThrowsCode(() => BatchPathValidator.RequireRelativePath(root, "source/file.exe", "source", [".txt", ".md"]), "WB-BATCH-PATH-422");
        AssertThrowsCode(() => BatchPathValidator.EnsureNoCollisions(["A.txt", "a.txt"], "upload"), "WB-BATCH-PATH-409");
        var composed = "\u00E9.txt";
        var decomposed = "e\u0301.txt";
        var composedKey = BatchPathValidator.CollisionKey(composed);
        var decomposedKey = BatchPathValidator.CollisionKey(decomposed);
        Assert(composedKey == decomposedKey, $"NFC collision keys should match: {composedKey} != {decomposedKey}");
        AssertThrowsCode(() => BatchPathValidator.EnsureNoCollisions([composed, decomposed], "upload"), "WB-BATCH-PATH-409");
    }
    finally { TryDelete(root); }
});
Run("scan repository persists normalized prebatch and reopens", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var repository = new BatchScanRepository(workspace, registry);
        var result = repository.CreateScan(
            [new BatchSourceInput("资料/帝国.md", "帝国资料", System.Text.Encoding.UTF8.GetBytes("\uFEFF# 西部\r\n\r\n第一段\r\n第二行"))],
            "owner.developer",
            "instance.test",
            new string('A', 64),
            new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        Assert(result.Scan["status"]?.GetValue<string>() == "ready", "scan should be ready");
        Assert(result.Snapshots.Count == 1 && result.SourceUnits.Count == 2, "scan should produce one snapshot and two paragraph units");
        var snapshot = result.Snapshots[0];
        var unit = result.SourceUnits[0];
        Assert(snapshot["scope_kind"]?.GetValue<string>() == "prebatch", "snapshot scope must be prebatch");
        Assert(unit["scope_kind"]?.GetValue<string>() == "prebatch" && unit["batch_id"] is null, "prebatch unit must not expose batch id");
        Assert(result.SourceUnits.Any(x => x["text"]?.GetValue<string>() == "第一段\n第二行"), "source text normalization changed");
        var reopened = new BatchScanRepository(new WorkspaceService(new WorkspaceOptions(root, schemaRoot)), registry);
        var scan = reopened.ReadScan(result.ScanId);
        var reopenedUnit = reopened.ReadSourceUnit(result.ScanId, snapshot["snapshot_id"]!.GetValue<string>(), unit["unit_id"]!.GetValue<string>());
        Assert(scan["scan_hash"]?.GetValue<string>() == result.Scan["scan_hash"]?.GetValue<string>(), "scan hash changed after reopen");
        Assert(reopenedUnit["unit_hash"]?.GetValue<string>() == unit["unit_hash"]?.GetValue<string>(), "unit hash changed after reopen");
    }
    finally { TryDelete(root); }
});
Run("scan repository rejects duplicate paths and invalid UTF-8", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var repository = new BatchScanRepository(workspace, BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory)));
        AssertThrowsCode(() => repository.CreateScan(
            [
                new BatchSourceInput("a.txt", "a", [1]),
                new BatchSourceInput("A.txt", "A", [2])
            ], "owner.developer", "instance.test", new string('A', 64)), "WB-BATCH-PATH-409");
        AssertThrowsCode(() => repository.CreateScan(
            [new BatchSourceInput("bad.txt", "bad", [0xFF, 0xFE])],
            "owner.developer", "instance.test", new string('A', 64)), "WB-BATCH-UPLOAD-422");
    }
    finally { TryDelete(root); }
});
Run("batch create promotes prebatch and redacts public manifest", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var scanRepository = new BatchScanRepository(workspace, registry);
        var scan = scanRepository.CreateScan(
            [new BatchSourceInput("资料/帝国.md", "帝国资料", System.Text.Encoding.UTF8.GetBytes("第一段\n\n第二段"))],
            "owner.developer", "instance.test", new string('A', 64),
            new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        var request = CreateRequest(scan, new string('B', 64));
        var repository = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64));
        var result = repository.Create(request, new string('C', 64), new DateTimeOffset(2026, 8, 25, 12, 1, 0, TimeSpan.Zero));
        var reservation = repository.ReadReservation(request.IdempotencyKey);
        var journal = repository.ReadPromotionJournal(scan.ScanId, reservation["operation_id"]!.GetValue<string>());
        var manifest = repository.ReadManifest(reservation["batch_id"]!.GetValue<string>());
        Assert(reservation["state"]?.GetValue<string>() == "committed", "reservation should be committed");
        Assert(journal["state"]?.GetValue<string>() == "committed" && journal["phase_seq"]?.GetValue<int>() == 4, "promotion journal should be committed at phase 4");
        Assert(manifest["status"]?.GetValue<string>() == "planned", "promoted batch should be planned");
        Assert(manifest["item_ids"]?.AsArray().Count == 2, "each source unit should become one queued item");
        Assert(result.PublicManifest["schema_version"]?.GetValue<string>() == "awake.worldbook.batch-manifest-public.v2", "public schema missing");
        Assert(result.PublicManifest["owner_id"] is null && result.PublicManifest["workspace_marker_hash"] is null, "public manifest leaked internal ownership");
        Assert(result.PublicManifest["provider_selection"]?["provider_fingerprint"] is null, "public manifest leaked provider fingerprint");
        var promotedScan = BatchWorkspaceLayout.ReadJson(Path.Combine(root, "prebatches", scan.ScanId, "scan.json"));
        Assert(promotedScan["promotion"]?["state"]?.GetValue<string>() == "promoted", "scan promotion projection should be updated last");
    }
    finally { TryDelete(root); }
});
Run("batch create is idempotent and rejects changed input", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var scan = new BatchScanRepository(workspace, registry).CreateScan(
            [new BatchSourceInput("facts.txt", "facts", System.Text.Encoding.UTF8.GetBytes("事实"))],
            "owner.developer", "instance.test", new string('A', 64));
        var request = CreateRequest(scan, new string('D', 64));
        var first = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64)).Create(request, new string('C', 64));
        var second = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64)).Create(request, new string('C', 64));
        Assert(first.PublicManifest["batch_id"]?.GetValue<string>() == second.PublicManifest["batch_id"]?.GetValue<string>(), "same idempotency key should return the same batch");
        var changed = request with { PipelineRevision = "batch-authoring.v2" };
        AssertThrowsCode(() => new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64)).Create(changed, new string('C', 64)), "WB-BATCH-IDEMPOTENCY-409");
    }
    finally { TryDelete(root); }
});
Run("batch create resumes after copying interruption", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var scan = new BatchScanRepository(workspace, registry).CreateScan(
            [new BatchSourceInput("facts.txt", "facts", System.Text.Encoding.UTF8.GetBytes("事实"))],
            "owner.developer", "instance.test", new string('A', 64));
        var request = CreateRequest(scan, new string('E', 64));
        var interrupted = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64), phase =>
        {
            if (phase == "copying") throw new InvalidOperationException("TEST-INTERRUPTION");
        });
        AssertThrowsCode(() => interrupted.Create(request, new string('C', 64)), "TEST-INTERRUPTION");
        var resumed = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64));
        var result = resumed.Create(request, new string('C', 64));
        var reservation = resumed.ReadReservation(request.IdempotencyKey);
        Assert(result.PublicManifest["batch_id"]?.GetValue<string>() == reservation["batch_id"]?.GetValue<string>(), "recovery must retain batch id");
        Assert(reservation["state"]?.GetValue<string>() == "committed", "recovery should commit reservation");
    }
    finally { TryDelete(root); }
});
Run("batch facts-only draft is valid without expressions", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var documents = new WorldbookApplicationService(workspace);
        var fact = new AuthoringDraftFact("fact.test", "geography", "西帝国西部有葱郁温暖的橡树林地。", "confirmed", false, null, "accepted");
        var candidateSet = new AuthoringCandidateSet(
            "generation.batch-test",
            "snapshot.batch-test",
            new string('a', 64),
            new string('b', 64),
            new string('c', 64),
            "succeeded",
            [
                new AuthoringCandidate(
                    "candidate.batch-test",
                    new string('d', 64),
                    "snapshot.batch-test",
                    new string('a', 64),
                    new string('b', 64),
                    new string('c', 64),
                    [fact],
                    new AuthoringDraftMetadata("西帝国西部地貌", "西帝国西部以葱郁温暖的橡树林地为主要地貌。", "geography", null, [], null),
                    [],
                    Coverage: new JsonObject
                    {
                        ["mode"] = "batch_facts_only",
                        ["status"] = "not_evaluated",
                        ["heuristic"] = true,
                        ["not_semantic_migration_proof"] = true
                    })
            ]);
        var document = documents.CreateGeneratedDocumentFromDraft(
            "西帝国西部地貌",
            "西帝国西部以葱郁温暖的橡树林地为主要地貌。",
            "geography",
            "batch.item.test",
            [fact],
            [],
            "base",
            "author.developer",
            candidateSet);
        Assert(document.Document["status"]?.GetValue<string>() == "needs_review", "batch draft must remain needs_review");
        Assert(document.Document["assertions"]?.AsArray().Count == 1, "batch draft should contain one assertion");
        Assert(document.Document["assertions"]![0]!["expressions"]?.AsArray().Count == 0, "batch V1 draft must not generate expressions");
        Assert(document.Document["assertions"]![0]!["kind"]?.GetValue<string>() == "fact", "geography fact kind must project to authoring fact");
        Assert(AuthoringProjection.MapDomain("military") == "war", "military domain must project to authoring war");
        Assert(document.Document["author_created"]!["provenance"]!["coverage"]!["mode"]?.GetValue<string>() == "batch_facts_only", "batch draft must declare facts-only semantic coverage");
        Assert(document.Document["author_created"]!["provenance"]!["coverage"]!["not_semantic_migration_proof"]?.GetValue<bool>() == true, "batch draft must not claim Semantic Migration proof");
        Assert(File.Exists(document.AbsolutePath), "batch draft should be persisted");
    }
    finally { TryDelete(root); }
});

Run("batch translation preserves unlocatable facts for manual review", () =>
{
    var sourceText = "西帝国位于帝国西部。";
    var sourceUnit = new JsonObject
    {
        ["unit_id"] = "unit.manual-evidence",
        ["start_utf16"] = 0,
        ["end_utf16"] = sourceText.Length,
        ["line_start"] = 1,
        ["line_end"] = 1,
        ["heading_path"] = new JsonArray(),
        ["text"] = sourceText
    };
    var fact = new AuthoringDraftFact(
        "fact.manual-evidence",
        "fact",
        "西帝国拥有悠久军镇传统。",
        "uncertain",
        true,
        new AuthoringDraftEvidence("provider", "段落 1", "AI 对原文的概括", Hashing.Sha256Text("AI 对原文的概括")));
    var translate = typeof(BatchProviderService).GetMethod(
        "TranslateFacts",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("TranslateFacts method is missing");

    var translated = (IReadOnlyList<BatchFactInput>)translate.Invoke(null, [new[] { fact }, sourceUnit])!;

    Assert(translated.Count == 1, "unlocatable fact should remain in the translated result");
    Assert(translated[0].RiskLevel == "yellow", "unlocatable fact should remain a yellow-risk candidate");
    Assert(translated[0].Notes?.Contains("无法定位", StringComparison.Ordinal) == true, "unlocatable fact should explain the manual review requirement");
    Assert(translated[0].Evidence.Count == 1 && translated[0].Evidence[0].Quote == sourceText, "manual review evidence should point to the source unit context");
});

Run("batch empty facts settle as no_candidate with zero evidence", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var scan = new BatchScanRepository(workspace, registry).CreateScan(
            [new BatchSourceInput("empty.txt", "empty", System.Text.Encoding.UTF8.GetBytes("只有无法形成事实的材料。"))],
            "owner.developer",
            "instance.test",
            new string('A', 64));
        var batch = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64))
            .Create(CreateRequest(scan, new string('E', 64)), new string('C', 64));
        var batchId = batch.PublicManifest["batch_id"]!.GetValue<string>();
        var itemId = batch.PublicManifest["item_ids"]![0]!.GetValue<string>();
        var itemPath = Path.Combine(root, "batches", batchId, "items", itemId + ".json");
        var item = BatchWorkspaceLayout.ReadJson(itemPath);
        var timestamp = new DateTimeOffset(2026, 8, 25, 12, 5, 0, TimeSpan.Zero);
        item["status"] = "extracting";
        item["active_attempt_stage"] = "facts";
        item["lease"] = BatchLeaseGuard.Create(itemId, "instance.test", 0, timestamp);
        BatchWorkspaceLayout.WriteJsonAtomic(itemPath, item);
        var lease = item["lease"]!.AsObject();
        var commit = new BatchFactReviewRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64))
            .CommitFacts(
                new BatchFactCommitRequest(
                    batchId,
                    itemId,
                    0,
                    lease["attempt_id"]!.GetValue<string>(),
                    "local",
                    new string('C', 64),
                    []),
                timestamp);
        Assert(commit.Item["status"]?.GetValue<string>() == "no_candidate", "empty facts must settle the item as no_candidate");
        Assert(commit.Item["review_status"]?.GetValue<string>() == "no_candidate", "empty facts must not enter human fact review");
        Assert(commit.Evidence.Count == 0, "empty facts must produce zero evidence sidecars");
        Assert(commit.Result["facts"]?.AsArray().Count == 0, "empty facts result must retain an empty facts array");
        Assert(commit.Result["review_only"]?.GetValue<bool>() == true, "empty facts result must remain review-only");
        var report = new BatchWorkflowService(workspace, registry, "owner.developer", "instance.test", new string('A', 64))
            .GetPublicReport(batchId);
        Assert(report["counts"]!["no_candidate"]?.GetValue<int>() == 1, "public report must count no_candidate items");
    }
    finally { TryDelete(root); }
});

Run("batch cache keys isolate source hashes and empty payloads remain readable", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var cache = new BatchCacheRepository(workspace, registry);
        var key = new JsonObject
        {
            ["normalized_content_hash"] = new string('a', 64),
            ["unit_hash"] = new string('b', 64),
            ["stage"] = "facts"
        };
        var firstKey = cache.ComputeKey(key);
        key["normalized_content_hash"] = new string('c', 64);
        var secondKey = cache.ComputeKey(key);
        Assert(firstKey != secondKey, "cache key must change when source content hash changes");
        key["normalized_content_hash"] = new string('a', 64);
        var payload = new JsonObject { ["facts"] = new JsonArray() };
        var published = cache.Publish(key, "facts", "awake.worldbook.batch-cache-facts-payload.v2", payload);
        var hit = cache.TryRead(published.CacheKey);
        Assert(hit is not null && hit.Payload["facts"]?.AsArray().Count == 0, "published empty facts cache must be readable as an empty result");
    }
    finally { TryDelete(root); }
});

Run("cached evidence reuses its exact source-unit offset", () =>
{
    var sourceText = "前置说明\n第一条事实\n后置说明";
    var sourceUnit = new JsonObject
    {
        ["unit_id"] = "unit.cached-evidence",
        ["start_utf16"] = 0,
        ["end_utf16"] = sourceText.Length,
        ["line_start"] = 1,
        ["line_end"] = 3,
        ["heading_path"] = new JsonArray(),
        ["text"] = sourceText
    };
    var payload = new JsonObject
    {
        ["facts"] = new JsonArray
        {
            new JsonObject
            {
                ["text"] = "第一条事实",
                ["kind"] = "fact",
                ["certainty"] = "confirmed",
                ["risk_level"] = "green",
                ["evidence_candidates"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["start_utf16"] = sourceText.IndexOf("第一条事实", StringComparison.Ordinal),
                        ["end_utf16"] = sourceText.IndexOf("第一条事实", StringComparison.Ordinal) + "第一条事实".Length,
                        ["line_start"] = 2,
                        ["line_end"] = 2,
                        ["heading_path"] = new JsonArray(),
                        ["quote"] = "第一条事实"
                    }
                }
            }
        }
    };
    var parse = typeof(BatchProviderService).GetMethod(
        "ParseCachedFacts",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ParseCachedFacts method is missing");

    var parsed = (IReadOnlyList<BatchFactInput>)parse.Invoke(null, [payload, sourceUnit])!;
    var evidence = parsed.Single().Evidence.Single();
    Assert(evidence.Quote == "第一条事实", "cached evidence should preserve the source quote");
    Assert(evidence.Locator["start_utf16"]?.GetValue<int>() == sourceText.IndexOf("第一条事实", StringComparison.Ordinal), "cached evidence should use the current source offset");
    Assert(evidence.Locator["line_start"]?.GetValue<int>() == 2, "cached evidence should use the current source line");
});

Run("cached evidence preserves the selected duplicate quote occurrence", () =>
{
    var sourceText = "第一条事实\n中间说明\n第一条事实";
    var secondStart = sourceText.LastIndexOf("第一条事实", StringComparison.Ordinal);
    var sourceUnit = new JsonObject
    {
        ["unit_id"] = "unit.cached-duplicate",
        ["start_utf16"] = 0,
        ["end_utf16"] = sourceText.Length,
        ["line_start"] = 1,
        ["line_end"] = 3,
        ["heading_path"] = new JsonArray(),
        ["text"] = sourceText
    };
    var payload = new JsonObject
    {
        ["facts"] = new JsonArray
        {
            new JsonObject
            {
                ["text"] = "第一条事实",
                ["kind"] = "fact",
                ["certainty"] = "confirmed",
                ["risk_level"] = "green",
                ["evidence_candidates"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["start_utf16"] = secondStart,
                        ["end_utf16"] = secondStart + "第一条事实".Length,
                        ["line_start"] = 3,
                        ["line_end"] = 3,
                        ["heading_path"] = new JsonArray(),
                        ["quote"] = "第一条事实"
                    }
                }
            }
        }
    };
    var parse = typeof(BatchProviderService).GetMethod(
        "ParseCachedFacts",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ParseCachedFacts method is missing");

    var parsed = (IReadOnlyList<BatchFactInput>)parse.Invoke(null, [payload, sourceUnit])!;
    var evidence = parsed.Single().Evidence.Single();
    Assert(evidence.Locator["start_utf16"]?.GetValue<int>() == secondStart, "cached evidence must preserve the selected duplicate occurrence");
    Assert(evidence.Locator["line_start"]?.GetValue<int>() == 3, "cached evidence must preserve the selected duplicate line");
});

Run("cached evidence rejects stale offsets instead of silently rebinding", () =>
{
    var sourceUnit = new JsonObject
    {
        ["unit_id"] = "unit.cached-stale",
        ["start_utf16"] = 0,
        ["end_utf16"] = "新资料".Length,
        ["line_start"] = 1,
        ["line_end"] = 1,
        ["heading_path"] = new JsonArray(),
        ["text"] = "新资料"
    };
    var payload = new JsonObject
    {
        ["facts"] = new JsonArray
        {
            new JsonObject
            {
                ["text"] = "旧事实",
                ["kind"] = "fact",
                ["certainty"] = "confirmed",
                ["risk_level"] = "green",
                ["evidence_candidates"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["start_utf16"] = 0,
                        ["end_utf16"] = "旧事实".Length,
                        ["line_start"] = 1,
                        ["line_end"] = 1,
                        ["heading_path"] = new JsonArray(),
                        ["quote"] = "旧事实"
                    }
                }
            }
        }
    };
    var parse = typeof(BatchProviderService).GetMethod(
        "ParseCachedFacts",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ParseCachedFacts method is missing");

    try
    {
        parse.Invoke(null, [payload, sourceUnit]);
        throw new InvalidOperationException("expected stale cache rejection");
    }
    catch (System.Reflection.TargetInvocationException error) when (error.InnerException is InvalidOperationException inner && inner.Message.StartsWith("WB-BATCH-CACHE-422", StringComparison.Ordinal))
    {
    }
});

Run("commit accepts evidence with harmless formatting differences", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var sourceText = "第一条事实";
        var scan = new BatchScanRepository(workspace, registry).CreateScan(
            [new BatchSourceInput("facts.txt", "facts", System.Text.Encoding.UTF8.GetBytes(sourceText))],
            "owner.developer", "instance.test", new string('A', 64));
        var batch = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64)).Create(CreateRequest(scan, new string('9', 64)), new string('C', 64));
        var batchId = batch.PublicManifest["batch_id"]!.GetValue<string>();
        var itemId = batch.PublicManifest["item_ids"]!.AsArray()[0]!.GetValue<string>();
        var itemPath = Path.Combine(root, "batches", batchId, "items", itemId + ".json");
        var timestamp = new DateTimeOffset(2026, 8, 25, 12, 4, 0, TimeSpan.Zero);
        var item = BatchWorkspaceLayout.ReadJson(itemPath);
        item["status"] = "extracting";
        item["active_attempt_stage"] = "facts";
        var lease = BatchLeaseGuard.Create(itemId, "instance.test", 0, timestamp);
        item["lease"] = lease;
        BatchWorkspaceLayout.WriteJsonAtomic(itemPath, item);
        var locator = new JsonObject
        {
            ["source_unit_id"] = item["source_unit_id"]!.GetValue<string>(),
            ["start_utf16"] = 0,
            ["end_utf16"] = sourceText.Length,
            ["line_start"] = 1,
            ["line_end"] = 1,
            ["heading_path"] = new JsonArray()
        };
        var result = new BatchFactReviewRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64)).CommitFacts(
            new BatchFactCommitRequest(
                batchId,
                itemId,
                0,
                lease["attempt_id"]!.GetValue<string>(),
                "local",
                new string('C', 64),
                [new BatchFactInput("第一条事实", "fact", "confirmed", "yellow", [new BatchFactEvidenceInput(locator, "第一 条事实")])]),
            timestamp);
        Assert(result.Item["status"]?.GetValue<string>() == "facts_review", "formatting differences should not fail fact commit");
        Assert(result.Evidence.Single()["quote"]?.GetValue<string>() == sourceText, "committed evidence should use the source text");
    }
    finally { TryDelete(root); }
});

Run("fact result evidence and review are committed with CAS", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var scan = new BatchScanRepository(workspace, registry).CreateScan(
            [new BatchSourceInput("facts.txt", "facts", System.Text.Encoding.UTF8.GetBytes("第一条事实\n\n第二段"))],
            "owner.developer", "instance.test", new string('A', 64));
        var createRequest = CreateRequest(scan, new string('F', 64));
        var batchRepository = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64));
        var batch = batchRepository.Create(createRequest, new string('C', 64));
        var batchId = batch.PublicManifest["batch_id"]!.GetValue<string>();
        var itemId = batch.PublicManifest["item_ids"]!.AsArray()[0]!.GetValue<string>();
        var unitEntry = Directory.EnumerateFiles(Path.Combine(root, "batches", batchId, "sources"), "*.json", SearchOption.AllDirectories)
            .Where(path => path.Contains(".units", StringComparison.Ordinal))
            .Select(path => new { Path = path, Document = BatchWorkspaceLayout.ReadJson(path) })
            .Single(entry => entry.Document["unit_id"]?.GetValue<string>() ==
                BatchWorkspaceLayout.ReadJson(Path.Combine(root, "batches", batchId, "items", itemId + ".json"))["source_unit_id"]?.GetValue<string>());
        var sourceUnit = unitEntry.Document;
        var itemPath = Path.Combine(root, "batches", batchId, "items", itemId + ".json");
        var runningItem = BatchWorkspaceLayout.ReadJson(itemPath);
        var factTimestamp = new DateTimeOffset(2026, 8, 25, 12, 2, 0, TimeSpan.Zero);
        runningItem["status"] = "extracting";
        runningItem["active_attempt_stage"] = "facts";
        var factLease = BatchLeaseGuard.Create(itemId, "instance.test", 0, factTimestamp);
        runningItem["lease"] = factLease;
        BatchWorkspaceLayout.WriteJsonAtomic(itemPath, runningItem);
        var locator = new JsonObject
        {
            ["source_unit_id"] = sourceUnit["unit_id"]!.GetValue<string>(),
            ["start_utf16"] = sourceUnit["start_utf16"]!.GetValue<int>(),
            ["end_utf16"] = sourceUnit["end_utf16"]!.GetValue<int>(),
            ["line_start"] = sourceUnit["line_start"]!.GetValue<int>(),
            ["line_end"] = sourceUnit["line_end"]!.GetValue<int>(),
            ["heading_path"] = sourceUnit["heading_path"]!.DeepClone()
        };
        var factRepository = new BatchFactReviewRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64));
        var facts = factRepository.CommitFacts(new BatchFactCommitRequest(
            batchId,
            itemId,
            0,
            factLease["attempt_id"]!.GetValue<string>(),
            "local",
            new string('C', 64),
            [new BatchFactInput("帝国西部存在一条已记录的事实。", "fact", "confirmed", "green", [new BatchFactEvidenceInput(locator, "第一条事实")])]),
            factTimestamp);
        Assert(facts.Item["status"]?.GetValue<string>() == "facts_review" && facts.Item["revision"]?.GetValue<int>() == 1, "fact commit should enter facts_review");
        Assert(facts.Evidence.Count == 1 && facts.Result["review_only"]?.GetValue<bool>() == true, "fact result evidence should be review-only");
        var factId = facts.Result["facts"]!.AsArray()[0]! ["fact_id"]!.GetValue<string>();
        var decision = factRepository.ReviewFacts(new BatchReviewRequest(batchId, itemId, 1, [factId], "green", "accepted"));
        var reviewed = factRepository.ReadItem(batchId, itemId);
        Assert(decision["review_status"]?.GetValue<string>() == "accepted", "review decision should be accepted");
        Assert(reviewed["accepted_fact_set_hash"]?.GetValue<string>()?.Length == 64, "accepted fact set hash should be server computed");
        Assert(reviewed["status_reason"]?.GetValue<string>() == "facts_accepted_waiting_for_metadata_consent", "facts-only review should wait for metadata consent");
        var recomputedHash = BatchFactReviewRepository.ComputeAcceptedFactSetHash(facts.Result, [factId]);
        Assert(recomputedHash == reviewed["accepted_fact_set_hash"]?.GetValue<string>(), "accepted fact hash formula must be stable across review and metadata stages");

        var resultPath = Path.Combine(root, "batches", batchId, "items", itemId + ".result.json");
        var changedResult = BatchWorkspaceLayout.ReadJson(resultPath);
        changedResult["facts"]![0]!["text"] = "被篡改的事实";
        BatchWorkspaceLayout.WriteJsonAtomic(resultPath, changedResult);
        runningItem = BatchWorkspaceLayout.ReadJson(itemPath);
        runningItem["status"] = "metadata_running";
        runningItem["active_attempt_stage"] = "metadata";
        var metadataTimestamp = new DateTimeOffset(2026, 8, 25, 12, 3, 0, TimeSpan.Zero);
        var metadataLease = BatchLeaseGuard.Create(itemId, "instance.test", 0, metadataTimestamp);
        runningItem["lease"] = metadataLease;
        BatchWorkspaceLayout.WriteJsonAtomic(itemPath, runningItem);
        var metadataRepository = new BatchMetadataRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64));
        AssertThrowsCode(() => metadataRepository.Commit(new BatchMetadataCommitRequest(
            batchId,
            itemId,
            runningItem["revision"]!.GetValue<int>(),
            metadataLease["attempt_id"]!.GetValue<string>(),
            "local",
            new string('C', 64),
            reviewed["accepted_fact_set_hash"]!.GetValue<string>(),
            "标题",
            "摘要",
            "politics",
            "国家"), metadataTimestamp), "WB-BATCH-FACT-SET-409");
    }
    finally { TryDelete(root); }
});

Run("batch recovery settles interrupted in-flight items", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var scan = new BatchScanRepository(workspace, registry).CreateScan(
            [new BatchSourceInput("recovery.txt", "recovery", System.Text.Encoding.UTF8.GetBytes("待恢复的事实"))],
            "owner.developer", "instance.test", new string('A', 64));
        var batch = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64))
            .Create(CreateRequest(scan, new string('F', 64)), new string('C', 64));
        var batchId = batch.PublicManifest["batch_id"]!.GetValue<string>();
        var itemId = batch.PublicManifest["item_ids"]!.AsArray()[0]!.GetValue<string>();
        var itemPath = Path.Combine(root, "batches", batchId, "items", itemId + ".json");
        var item = BatchWorkspaceLayout.ReadJson(itemPath);
        var timestamp = new DateTimeOffset(2026, 9, 4, 1, 10, 0, TimeSpan.Zero);
        var lease = BatchLeaseGuard.Create(itemId, "instance.crashed", 0, timestamp);
        item["status"] = "extracting";
        item["active_attempt_stage"] = "facts";
        item["lease"] = lease;
        BatchWorkspaceLayout.WriteJsonAtomic(itemPath, item);

        var recovered = new BatchRecoveryService(workspace).RecoverInFlight(timestamp.AddMinutes(1));
        Assert(recovered == 1, "recovery should settle one interrupted item");
        var recoveredItem = BatchWorkspaceLayout.ReadJson(itemPath);
        var recoveredManifest = BatchWorkspaceLayout.ReadJson(Path.Combine(root, "batches", batchId, "manifest.json"));
        Assert(recoveredItem["status"]?.GetValue<string>() == "unknown_result", "interrupted item must become unknown_result");
        Assert(recoveredItem["recovery_status"]?.GetValue<string>() == "needs_reconcile", "interrupted item must require reconciliation");
        Assert(recoveredItem["lease"] is null && recoveredItem["active_attempt_stage"]?.GetValue<string>() == "none", "recovery must clear the stale lease");
        Assert(recoveredManifest["status"]?.GetValue<string>() == "needs_reconcile", "batch must expose recovery requirement");
        var attemptPath = Path.Combine(root, "batches", batchId, "items", itemId + ".attempts", lease["attempt_id"]!.GetValue<string>() + ".json");
        Assert(File.Exists(attemptPath), "recovery must persist an unknown attempt record");
        var attempt = BatchWorkspaceLayout.ReadJson(attemptPath);
        Assert(attempt["outcome"]?.GetValue<string>() == "transient_error", "recovery attempt must remain non-success");
    }
    finally { TryDelete(root); }
});
Run("batch cancel, claim, retry, report and public projection boundaries", () =>
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var scan = new BatchScanRepository(workspace, registry).CreateScan(
            [new BatchSourceInput("facts.txt", "facts", System.Text.Encoding.UTF8.GetBytes("事实"))],
            "owner.developer", "instance.test", new string('A', 64));
        var batch = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64)).Create(CreateRequest(scan, new string('7', 64)), new string('C', 64));
        var batchId = batch.PublicManifest["batch_id"]!.GetValue<string>();
        var itemId = batch.PublicManifest["item_ids"]!.AsArray()[0]!.GetValue<string>();
        var controls = new BatchControlService(workspace, registry, "owner.developer", "instance.test", new string('A', 64));
        var cancelled = controls.ChangeState(batchId, 0, "cancelled");
        var cancelledItem = BatchWorkspaceLayout.ReadJson(Path.Combine(root, "batches", batchId, "items", itemId + ".json"));
        Assert(cancelledItem["status"]?.GetValue<string>() == "skipped", "cancelled batch items must use the contract-supported skipped state");
        Assert(cancelled.Data["status"]?.GetValue<string>() == "cancelled", "batch manifest should remain cancelled");

        var workflow = new BatchWorkflowService(workspace, registry, "owner.developer", "instance.test", new string('A', 64));
        var reviewProjection = workflow.GetPublicReviewProjection(batchId, itemId);
        registry.ValidatePayload("public_review_projection", reviewProjection);
        Assert(reviewProjection["item"]?["status"]?.GetValue<string>() == "skipped", "review projection should expose the current item state");
        var report = workflow.GetPublicReport(batchId);
        registry.ValidatePayload("public_report", report);
        foreach (var field in new[] { "total", "queued", "running", "review_pending", "ready_to_create", "created", "failed", "unknown_result" })
            Assert(report["counts"]?[field] is not null, $"report count missing: {field}");
        Assert(report["counts"]?["skipped"] is null && report["counts"]?["cancelled"] is null, "report counts must not expose internal item states");

        var secondScan = new BatchScanRepository(workspace, registry).CreateScan(
            [new BatchSourceInput("second.txt", "second", System.Text.Encoding.UTF8.GetBytes("第二事实"))],
            "owner.developer", "instance.test", new string('A', 64));
        var second = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", new string('A', 64)).Create(CreateRequest(secondScan, new string('8', 64)), new string('C', 64));
        var secondBatchId = second.PublicManifest["batch_id"]!.GetValue<string>();
        var secondManifestPath = Path.Combine(root, "batches", secondBatchId, "manifest.json");
        var secondManifest = BatchWorkspaceLayout.ReadJson(secondManifestPath);
        secondManifest["active_owner_instance_id"] = "instance.original";
        secondManifest["lease_expires_at"] = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O");
        BatchWorkspaceLayout.WriteJsonAtomic(secondManifestPath, secondManifest);
        var otherControls = new BatchControlService(workspace, registry, "owner.developer", "instance.other", new string('A', 64));
        AssertThrowsCode(() => otherControls.Claim(secondBatchId, 0, "manual_reclaim"), "WB-BATCH-CLAIM-409");
        secondManifest["lease_expires_at"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O");
        BatchWorkspaceLayout.WriteJsonAtomic(secondManifestPath, secondManifest);
        var claimed = otherControls.Claim(secondBatchId, 0, "manual_reclaim");
        Assert(claimed.Data["claim_generation"]?.GetValue<int>() == 1, "expired lease claim must increment generation");

        var retryItemId = second.PublicManifest["item_ids"]!.AsArray()[0]!.GetValue<string>();
        var retryItemPath = Path.Combine(root, "batches", secondBatchId, "items", retryItemId + ".json");
        var retryItem = BatchWorkspaceLayout.ReadJson(retryItemPath);
        retryItem["status"] = "failed";
        retryItem["last_attempt_stage"] = "metadata";
        retryItem["review_status"] = "accepted";
        BatchWorkspaceLayout.WriteJsonAtomic(retryItemPath, retryItem);
        var retry = otherControls.RetryItem(secondBatchId, retryItemId, retryItem["revision"]!.GetValue<int>(), true, "metadata");
        Assert(retry.Data["item"]?["status"]?.GetValue<string>() == "metadata_pending", "metadata retry must requeue metadata_pending");
        Assert(retry.Data["facts"] is JsonArray && retry.Data["evidence"] is JsonArray && retry.Data["source_units"] is JsonArray, "metadata retry must preserve full review projection");
    }
    finally { TryDelete(root); }
});
RunAsync("batch cancellation settles the active attempt and skips pending items", () => RunCancellationSettlementScenarioAsync(true));
RunAsync("provider OperationCanceledException settles the active attempt and skips pending items", () => RunCancellationSettlementScenarioAsync(false));
Console.WriteLine($"PASS: Worldbook Studio BatchTests ({passed}/{total})");

void Run(string name, Action action)
{
    total++;
    try { action(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { throw new InvalidOperationException($"FAIL {name}: {ex.Message}", ex); }
}

void RunAsync(string name, Func<Task> action)
{
    total++;
    try { action().GetAwaiter().GetResult(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { throw new InvalidOperationException($"FAIL {name}: {ex.Message}", ex); }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AssertThrowsCode(Action action, string code)
{
    try { action(); throw new InvalidOperationException($"expected {code} but no exception was thrown"); }
    catch (InvalidOperationException ex) { Assert(ex.Message.StartsWith(code, StringComparison.Ordinal), $"expected {code}; got {ex.Message}"); }
}

static async Task RunCancellationSettlementScenarioAsync(bool cancelBatch)
{
    var root = MakeRoot();
    var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
    var timestamp = new DateTimeOffset(2026, 8, 25, 12, 5, 0, TimeSpan.Zero);
    try
    {
        var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
        workspace.Initialize();
        var registry = BatchAuthoringContractRegistry.Load(FindContractPath(AppContext.BaseDirectory));
        var workspaceMarkerHash = new string('A', 64);
        var scan = new BatchScanRepository(workspace, registry).CreateScan(
            [new BatchSourceInput("facts.txt", "facts", System.Text.Encoding.UTF8.GetBytes("第一事实\n\n第二事实"))],
            "owner.developer", "instance.test", workspaceMarkerHash, timestamp);
        var createRequest = CreateRequest(scan, new string(cancelBatch ? '1' : '2', 64));
        var providerFingerprint = new string('C', 64);
        var batch = new BatchCreateRepository(workspace, registry, "owner.developer", "instance.test", workspaceMarkerHash)
            .Create(createRequest, providerFingerprint, timestamp);
        var batchId = batch.PublicManifest["batch_id"]!.GetValue<string>();
        var itemIds = batch.PublicManifest["item_ids"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
        Assert(itemIds.Length == 2, "cancellation scenario requires two items");

        var manifestPath = Path.Combine(root, "batches", batchId, "manifest.json");
        var manifest = BatchWorkspaceLayout.ReadJson(manifestPath);
        var consent = new BatchConsentService(workspace, registry, "owner.developer", "instance.test", workspaceMarkerHash)
            .Issue(new BatchConsentRequest(
                batchId,
                manifest["revision"]!.GetValue<int>(),
                createRequest.ProviderId,
                "facts",
                createRequest.ModelParameters,
                "all_snapshots",
                itemIds), timestamp);

        using var cancellation = new CancellationTokenSource();
        var executedItemIds = new List<string>();
        Func<BatchStartRequest, JsonObject, CancellationToken, Task> targetExecutor = (_, item, cancellationToken) =>
        {
            executedItemIds.Add(item["item_id"]!.GetValue<string>());
            if (cancelBatch) cancellation.Cancel();
            var error = cancelBatch
                ? new OperationCanceledException("批次被取消。", cancellationToken)
                : new OperationCanceledException("Provider 请求取消。");
            return Task.FromException(error);
        };
        var execution = new BatchExecutionService(
            workspace,
            registry,
            new BatchConsentService(workspace, registry, "owner.developer", "instance.test", workspaceMarkerHash),
            "owner.developer",
            "instance.test",
            workspaceMarkerHash,
            new ProviderConfiguration(null, null, null, 5, 300, null, null),
            targetExecutor);

        var result = await execution.StartAsync(
            new BatchStartRequest(
                batchId,
                manifest["revision"]!.GetValue<int>(),
                consent.ConsentToken,
                manifest["claim_generation"]!.GetValue<int>(),
                0,
                "facts",
                itemIds),
            cancelBatch ? cancellation.Token : CancellationToken.None,
            timestamp);

        var activeItem = BatchWorkspaceLayout.ReadJson(Path.Combine(root, "batches", batchId, "items", itemIds[0] + ".json"));
        var pendingItem = BatchWorkspaceLayout.ReadJson(Path.Combine(root, "batches", batchId, "items", itemIds[1] + ".json"));
        Assert(executedItemIds.Count == 1 && executedItemIds[0] == itemIds[0], "cancellation must stop before the next provider request");
        Assert(activeItem["status"]?.GetValue<string>() == "unknown_result", "cancelled active item must use unknown_result");
        Assert(activeItem["status_reason"]?.GetValue<string>() == "cancelled", "cancelled active item must retain cancellation reason");
        Assert(activeItem["attempt_count"]?.GetValue<int>() == 1, "cancelled active item must record one attempt");
        Assert(activeItem["lease"] is null && activeItem["active_attempt_stage"]?.GetValue<string>() == "none", "cancelled active item must release its lease");
        Assert(activeItem["last_error_code"]?.GetValue<string>() == "WB-BATCH-PROVIDER-CANCELLED", "cancelled active item must use the canonical cancellation code");
        var attemptDirectory = Path.Combine(root, "batches", batchId, "items", itemIds[0] + ".attempts");
        var attemptPaths = Directory.Exists(attemptDirectory) ? Directory.GetFiles(attemptDirectory, "*.json") : [];
        Assert(attemptPaths.Length == 1, "cancelled active item must persist one attempt result");
        var attempt = BatchWorkspaceLayout.ReadJson(attemptPaths[0]);
        Assert(attempt["outcome"]?.GetValue<string>() == "unknown_result" && attempt["error_class"]?.GetValue<string>() == "cancelled", "cancelled attempt must be traceable as unknown_result");
        Assert(pendingItem["status"]?.GetValue<string>() == "skipped", "unstarted item must be skipped");
        Assert(pendingItem["attempt_count"]?.GetValue<int>() == 0 && pendingItem["lease"] is null, "unstarted item must not record an attempt or retain a lease");
        Assert(result.PublicManifest["status"]?.GetValue<string>() == "failed", "final batch status must be persisted after cancellation");
        var finalManifest = BatchWorkspaceLayout.ReadJson(manifestPath);
        Assert(finalManifest["status"]?.GetValue<string>() == "failed", "final manifest must not remain running after cancellation");
    }
    finally { TryDelete(root); }
}

static BatchCreateRequest CreateRequest(BatchScanResult scan, string idempotencyKey)
    => new(
        scan.ScanId,
        scan.Scan["scan_hash"]!.GetValue<string>(),
        "batch-authoring.v1",
        "local",
        new JsonObject
        {
            ["model"] = "fixture-model",
            ["temperature"] = 0.2,
            ["max_output_tokens"] = 512,
            ["reasoning_effort"] = "low"
        },
        idempotencyKey);

static string MakeRoot()
{
    var root = Path.Combine(Path.GetTempPath(), "awake-batch-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    return root;
}

static void TryDelete(string path)
{
    try
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
        else if (File.Exists(path)) File.Delete(path);
    }
    catch { }
}

static string FindContractPath(string start)
{
    var current = Path.GetFullPath(start);
    while (!string.IsNullOrWhiteSpace(current))
    {
        var candidate = Path.Combine(current, "docs", "superpowers", "specs", "2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json");
        if (File.Exists(candidate)) return candidate;
        current = Directory.GetParent(current)?.FullName ?? string.Empty;
    }
    throw new DirectoryNotFoundException("batch contract not found");
}

static string FindSchemaRoot(string start)
{
    var current = Path.GetFullPath(start);
    while (!string.IsNullOrWhiteSpace(current))
    {
        var candidate = Path.Combine(current, "docs", "worldbook-studio-plan");
        if (File.Exists(Path.Combine(candidate, "awake.worldbook.authoring.v1.schema.json"))) return candidate;
        current = Directory.GetParent(current)?.FullName ?? string.Empty;
    }
    throw new DirectoryNotFoundException("worldbook schema root not found");
}
