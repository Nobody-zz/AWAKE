using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using System.Net.Http;
using Awake.WorldbookStudio.Core;
using Awake.WorldbookStudio.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

var schemaRoot = FindSchemaRoot(AppContext.BaseDirectory);
var contractRoot = FindContractRoot(AppContext.BaseDirectory);
var passed = 0;
var total = 0;
Run("HTTP authority error matrix is explicit and safe", () =>
{
    var cases = new[]
    {
        ("WB-AUTHORITY-400", 400),
        ("WB-AUTHORITY-404", 404),
        ("WB-AUTHORITY-SELECTION-404", 404),
        ("WB-AUTHORITY-STAGING-404", 404),
        ("WB-AUTHORITY-CAS-409", 409),
        ("WB-AUTHORITY-STAGING-409", 409),
        ("WB-AUTHORITY-OPERATION-409", 409),
        ("WB-AUTHORITY-APPROVAL-409", 409),
        ("WB-AUTHORITY-PROOF-409", 409),
        ("WB-AUTHORITY-CLOSURE-409", 409),
         ("WB-AUTHORITY-COMPILE-409", 409),
         ("WB-AUTHORITY-POINTER-409", 409),
         ("WB-AUTHORITY-SAFEID-409", 409),
        ("WB-AUTHORITY-422", 422),
        ("WB-AUTHORITY-DOCUMENT-422", 422),
        ("WB-AUTHORITY-SELECTION-422", 422),
        ("WB-AUTHORITY-TIER-422", 422),
        ("WB-AUTHORITY-COMPILE-422", 422),
        ("WB-AUTHORITY-PROOF-403", 403),
        ("WB-AUTHORITY-MUTATION-UNKNOWN", 503),
        ("WB-AUTHORITY-LEGACY-410", 410)
    };
    foreach (var (code, status) in cases)
    {
        var projection = AuthorityHttpErrorProjection.Project(new InvalidOperationException(code + ": /internal/workspace/path"), AuthorityFailureContext.Preflight);
        Assert(projection.Error == code, $"stable code changed for {code}");
        Assert(projection.StatusCode == status, $"status changed for {code}");
        Assert(projection.SideEffect == "none", $"preflight side effect changed for {code}");
        Assert(!projection.Message.Contains("workspace", StringComparison.OrdinalIgnoreCase), $"unsafe message for {code}");
    }
    var workspaceRoot = Path.Combine(Path.GetTempPath(), "awake-public-projection");
    var inside = Path.Combine(workspaceRoot, "authoring", "entry.yaml");
    var outside = Path.Combine(Path.GetTempPath(), "private", "entry.yaml");
    var document = AuthorityPublicProjection.Document(new AuthorityDocumentRevision("doc-1", inside, 2, "content-hash", "authoring-v1/document-revisions/internal.json"), workspaceRoot);
    var outsideDocument = AuthorityPublicProjection.Document(new AuthorityDocumentRevision("doc-2", outside, 1, "content-hash-2", "private-record"), workspaceRoot);
    Assert(document["path"]?.GetValue<string>() == "authoring/entry.yaml", "workspace path must be relative");
    Assert(!document.ContainsKey("record_path"), "record path must not be projected");
    Assert(outsideDocument["path"]?.GetValue<string>()?.StartsWith("opaque:document:", StringComparison.Ordinal) == true, "outside path must become opaque");
    var studioRoot = FindStudioRoot(AppContext.BaseDirectory);
    var registry = JsonNode.Parse(File.ReadAllText(Path.Combine(studioRoot, "contracts", "authoring-action-route-registry.v1.json")))!.AsObject();
    Assert(registry["registry_status"]?.GetValue<string>() == "design_catalog", "route registry must not claim full runtime binding");
    var surface = registry["runtime_surface"]!.AsObject();
    Assert(surface["status"]?.GetValue<string>() == "partial", "runtime surface status must be explicit");
    var advertised = surface["advertised_action_ids"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
    Assert(advertised.SequenceEqual(new[] { "query_operation", "materialize_selection" }), "advertised action surface changed unexpectedly");
});
Run("HTTP authority errors distinguish mutation uncertainty", () =>
{
    var projection = AuthorityHttpErrorProjection.Project(new InvalidOperationException("WB-AUTHORITY-COMPILE-409: internal path"), AuthorityFailureContext.MutationUnknown);
    Assert(projection.SideEffect == "unknown", "mutation failure must not claim no side effect");
});
Run("HTTP unknown exceptions use safe 500 projection", () =>
{
    var projection = AuthorityHttpErrorProjection.Project(new IOException("C:\\secret\\workspace"), AuthorityFailureContext.MutationUnknown);
    Assert(projection.Error == "WB-AUTHORITY-UNKNOWN-500", "unknown exception was not normalized");
    Assert(projection.StatusCode == 500, "unknown exception did not map to 500");
    Assert(!projection.Message.Contains("secret", StringComparison.OrdinalIgnoreCase), "unknown exception leaked raw details");
    Assert(projection.SideEffect == "unknown", "unknown mutation exception must be uncertain");
});
Run("HTTP error response exposes matching correlation header and body", () =>
{
    var context = new DefaultHttpContext();
    using var body = new MemoryStream();
    context.Response.Body = body;
    AuthorityHttpErrorProjection.WriteAsync(context, new InvalidOperationException("WB-AUTHORITY-STAGING-404: C:\\secret\\workspace"), AuthorityFailureContext.Preflight).GetAwaiter().GetResult();
    body.Position = 0;
    using var document = JsonDocument.Parse(body);
    var bodyCorrelation = document.RootElement.GetProperty("correlation_id").GetString();
    var headerCorrelation = context.Response.Headers["X-AWAKE-Correlation-Id"].ToString();
    Assert(!string.IsNullOrWhiteSpace(bodyCorrelation), "body correlation id is missing");
    Assert(bodyCorrelation == headerCorrelation, "correlation header and body differ");
    Assert(document.RootElement.GetProperty("side_effect").GetString() == "none", "preflight response side effect changed");
    Assert(!body.ToArray().AsSpan().Contains((byte)'C'), "raw internal path leaked into response");
});
Run("HTTP response-started boundary does not append a second error body", () =>
{
    using var body = new MemoryStream();
    var responseFeature = new StartedResponseFeature(body);
    var features = new FeatureCollection();
    features.Set<IHttpResponseFeature>(responseFeature);
    var context = new DefaultHttpContext(features);
    AuthorityHttpErrorProjection.WriteAsync(context, new InvalidOperationException("WB-AUTHORITY-404: second"), AuthorityFailureContext.Preflight).GetAwaiter().GetResult();
    Assert(body.Length == 0, "response-started boundary appended an error body");
});
Run("F01 valid compile and stable hash", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var service = fixture.Service;
    var first = service.Compile();
    var second = service.Compile();
    Assert(first.Validation.Valid, "valid fixture should pass");
    Assert(first.ManifestHash == second.ManifestHash, "manifest hash should be stable");
    Assert(first.Files.ContainsKey("content-graph.json") && first.Files.ContainsKey("validation.json"), "reports should exist");
});
Run("F71 A2 snapshot exposes complete input closure", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var snapshot = fixture.Service.Compile().Snapshot ?? throw new InvalidOperationException("compile snapshot missing");
    var fingerprint = RequiredProperty<string>(snapshot, "InputFingerprint");
    Assert(fingerprint.Length == 64, "snapshot fingerprint must be SHA-256");
    var closure = RequiredEnumerable(snapshot, "InputClosure");
    Assert(closure.Count > 0, "snapshot input closure must not be empty");
    foreach (var input in closure)
    {
        var bytes = RequiredProperty<byte[]>(input, "Bytes");
        var hash = RequiredProperty<string>(input, "Sha256");
        Assert(bytes.Length > 0, "snapshot input bytes must be retained");
        Assert(hash.Length == 64, "snapshot input hash must be SHA-256");
    }
});
Run("F72 A2 parses and hashes the same bytes", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var snapshot = fixture.Service.Compile().Snapshot ?? throw new InvalidOperationException("compile snapshot missing");
    foreach (var input in RequiredEnumerable(snapshot, "InputClosure"))
    {
        var parsed = RequiredProperty<object>(input, "Parsed");
        Assert(parsed is not null, "snapshot input must retain parsed value from the same bytes");
        var sourceHash = RequiredProperty<string>(input, "Sha256");
        Assert(sourceHash == Hashing.Sha256Bytes(RequiredProperty<byte[]>(input, "Bytes")), "parsed input hash must match retained bytes");
    }
});
Run("F73 A2 records declared read inventory and zero downstream reads", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var snapshot = fixture.Service.Compile().Snapshot ?? throw new InvalidOperationException("compile snapshot missing");
    var inventory = RequiredEnumerable(snapshot, "ReadInventory");
    Assert(inventory.Count > 0, "read inventory must not be empty");
    Assert(RequiredProperty<int>(snapshot, "DownstreamReadCount") == 0, "downstream snapshot consumption must not read disk");
    Assert(inventory.Any(item => RequiredProperty<string>(item, "Path").EndsWith("demo.yaml", StringComparison.OrdinalIgnoreCase)), "authoring read must be recorded");
    var closurePaths = RequiredEnumerable(snapshot, "InputClosure").Select(item => RequiredProperty<string>(item, "Path")).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var inventoryPaths = inventory.Select(item => RequiredProperty<string>(item, "Path")).ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(closurePaths.SetEquals(inventoryPaths), "declared input closure and actual read inventory must match");
    var countDetails = string.Join(", ", inventory.Select(item =>
    {
        var path = RequiredProperty<string>(item, "Path");
        var count = RequiredProperty<int>(item, "Count");
        return $"{path}: {count}";
    }));
    Assert(inventory.All(item => RequiredProperty<int>(item, "Count") == 2), "compile snapshot must read each input once and verify once: " + countDetails);
});
Run("F74 A1 protection matrix is fixed at 72 rows", () =>
{
    var path = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "fixtures", "a1-protection-matrix.v1.json");
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var rows = document.RootElement.GetProperty("rows").EnumerateArray().ToArray();
    Assert(rows.Length == 72, $"A1 protection matrix must have 72 rows, got {rows.Length}");
    var ids = rows.Select(row => row.GetProperty("id").GetString()).ToArray();
    Assert(ids.All(id => !string.IsNullOrWhiteSpace(id)) && ids.Distinct(StringComparer.Ordinal).Count() == 72, "A1 protection matrix IDs must be unique");
});
Run("A2 TOCTOU during initial read rebuilds once", () =>
{
    var probe = new WorkspaceReadProbe();
    var mutated = false;
    probe.OnRead = read =>
    {
        if (mutated || read.Stage != WorkspaceReadStage.InitialRead || !read.Path.EndsWith("demo.yaml", StringComparison.OrdinalIgnoreCase)) return;
        mutated = true;
        File.AppendAllText(read.Path, "\n# changed during initial read\n");
    };
    using var fixture = MakeWorkspace(schemaRoot, readProbe: probe);
    var result = fixture.Service.Compile();
    Assert(mutated && result.Validation.Valid, "initial-read mutation should cause one clean rebuild");
});
Run("A2 TOCTOU after snapshot assembly rebuilds once", () =>
{
    var probe = new WorkspaceReadProbe();
    var mutated = false;
    string? probeWorkspaceRoot = null;
    probe.BeforeStage = stage =>
    {
        if (mutated || stage != WorkspaceReadStage.AssemblyVerification) return;
        mutated = true;
        File.AppendAllText(Path.Combine(probeWorkspaceRoot!, "authoring", "demo.yaml"), "\n# changed after assembly\n");
    };
    using var fixture = MakeWorkspace(schemaRoot, readProbe: probe);
    probeWorkspaceRoot = fixture.Root;
    var result = fixture.Service.Compile();
    Assert(mutated && result.Validation.Valid, "assembly mutation should cause one clean rebuild");
});
Run("A2 TOCTOU at output boundary keeps export unpublished", () =>
{
    var probe = new WorkspaceReadProbe();
    var mutated = false;
    string? workspaceRoot = null;
    probe.BeforeStage = stage =>
    {
        if (mutated || stage != WorkspaceReadStage.OutputBoundary || workspaceRoot is null) return;
        mutated = true;
        File.AppendAllText(Path.Combine(workspaceRoot, "authoring", "demo.yaml"), "\n# changed before output commit\n");
    };
    using var fixture = MakeWorkspace(schemaRoot, readProbe: probe);
    workspaceRoot = fixture.Root;
    var exportRoot = Path.Combine(fixture.Root, "export", "WorldbookV2");
    AssertThrowsCode(() => fixture.Service.Export(outputRoot: exportRoot), "WB-CAS-409");
    Assert(mutated && !Directory.EnumerateDirectories(exportRoot).Any(x => Path.GetFileName(x).StartsWith("WorldbookV2-", StringComparison.Ordinal)), "changed output must not publish a candidate");
});
Run("A2 compiled staging preserves old output on boundary change", () =>
{
    var probe = new WorkspaceReadProbe();
    var mutated = false;
    string? workspaceRoot = null;
    probe.BeforeStage = stage =>
    {
        if (mutated || stage != WorkspaceReadStage.OutputBoundary || workspaceRoot is null) return;
        mutated = true;
        File.AppendAllText(Path.Combine(workspaceRoot, "authoring", "demo.yaml"), "\n# changed before compiled commit\n");
    };
    using var fixture = MakeWorkspace(schemaRoot, readProbe: probe);
    workspaceRoot = fixture.Root;
    var targetRoot = Path.Combine(fixture.Root, "compiled", "current");
    Directory.CreateDirectory(targetRoot);
    File.WriteAllText(Path.Combine(targetRoot, "sentinel.txt"), "old-output");
    var compiled = fixture.Service.Compile();
    AssertThrowsCode(() => fixture.Service.WriteCompiled(compiled, targetRoot), "WB-CAS-409");
    Assert(File.ReadAllText(Path.Combine(targetRoot, "sentinel.txt")) == "old-output", "failed compiled publish must preserve old output");
});
Run("A2 preview and export stay within three input reads", () =>
{
    var previewProbe = new WorkspaceReadProbe();
    using (var fixture = MakeWorkspace(schemaRoot, readProbe: previewProbe))
    {
        fixture.Service.Preview("profile.commoner", "A2-preview");
        Assert(previewProbe.ReadInventory.All(item => item.Count == 3), "preview input reads must be initial, assembly, output only");
    }

    var exportProbe = new WorkspaceReadProbe();
    using (var fixture = MakeWorkspace(schemaRoot, readProbe: exportProbe))
    {
        fixture.Service.Export(outputRoot: Path.Combine(fixture.Root, "export", "WorldbookV2"));
        Assert(exportProbe.ReadInventory.All(item => item.Count == 3), "export input reads must be initial, assembly, output only");
    }
});
Run("A2 source schema audit ledger and file-set changes rebuild once", () =>
{
    var cases = new[] { "source", "schema", "audit", "ledger", "file-set" };
    foreach (var kind in cases)
    {
        var copiedSchemaRoot = kind == "schema" ? CopySchemaRoot(schemaRoot) : null;
        try
        {
            var probe = new WorkspaceReadProbe();
            var mutated = false;
            string? workspaceRoot = null;
            probe.BeforeStage = stage =>
            {
                if (mutated || stage != WorkspaceReadStage.AssemblyVerification || workspaceRoot is null) return;
                mutated = true;
                var path = kind switch
                {
                    "source" => Path.Combine(workspaceRoot, "authoring", "sources", "source-demo.yaml"),
                    "schema" => Path.Combine(copiedSchemaRoot!, "content-graph.v1.schema.json"),
                    "audit" => Path.Combine(workspaceRoot, "authoring", "audit", "events", "events.jsonl"),
                    "ledger" => Path.Combine(workspaceRoot, "authoring", "identity", "ledger.jsonl"),
                    _ => Path.Combine(workspaceRoot, "authoring", "sources", "extra.txt")
                };
                if (kind == "file-set") File.WriteAllText(path, "extra source file\n");
                else File.AppendAllText(path, "\n");
            };
            using var fixture = MakeWorkspace(schemaRoot: copiedSchemaRoot ?? schemaRoot, readProbe: probe);
            workspaceRoot = fixture.Root;
            if (kind == "audit") File.WriteAllText(Path.Combine(workspaceRoot, "authoring", "audit", "events", "events.jsonl"), string.Empty);
            if (kind == "ledger") File.WriteAllText(Path.Combine(workspaceRoot, "authoring", "identity", "ledger.jsonl"), string.Empty);
            var result = fixture.Service.Compile();
            Assert(mutated && result.Validation.Valid, $"{kind} mutation should rebuild without mixed output");
        }
        finally
        {
            if (copiedSchemaRoot is not null) try { Directory.Delete(copiedSchemaRoot, true); } catch { }
        }
    }
});
Run("A2 revision rollback returns CAS conflict", () =>
{
    var probe = new WorkspaceReadProbe();
    var mutated = false;
    string? workspaceRoot = null;
    probe.BeforeStage = stage =>
    {
        if (mutated || stage != WorkspaceReadStage.AssemblyVerification || workspaceRoot is null) return;
        mutated = true;
        var path = Path.Combine(workspaceRoot, "authoring", "demo.yaml");
        File.WriteAllText(path, File.ReadAllText(path).Replace("revision: 2", "revision: 1", StringComparison.Ordinal));
    };
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace("revision: 1", "revision: 2", StringComparison.Ordinal), probe);
    workspaceRoot = fixture.Root;
    AssertThrowsCode(() => fixture.Service.Compile(), "WB-CAS-409");
    Assert(mutated, "revision rollback seam did not run");
});
Run("F54 bootstrap frame roundtrip", () =>
{
    var bootstrap = new StudioBootstrap(1, "instance", new string('a', 64), Path.Combine(Path.GetTempPath(), "workspace"), Path.Combine(Path.GetTempPath(), "package", "schemas"), Path.Combine(Path.GetTempPath(), "package"));
    using var stream = new MemoryStream(StudioBootstrapCodec.EncodeFrame(bootstrap));
    var decoded = StudioBootstrapCodec.ReadFrameAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
    Assert(decoded == bootstrap, "bootstrap frame did not round-trip");
});
Run("F55 bootstrap environment binding", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "awake-runtime-contract-" + Guid.NewGuid().ToString("N"));
    var package = Path.Combine(root, "package");
    var schema = Path.Combine(package, "schemas");
    var workspace = Path.Combine(root, "workspace");
    Directory.CreateDirectory(schema);
    Directory.CreateDirectory(workspace);
    var bootstrap = new StudioBootstrap(1, "instance", new string('a', 64), workspace, schema, package);
    var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        [StudioRuntimeConstants.WorkspaceEnvironment] = workspace,
        [StudioRuntimeConstants.SchemaEnvironment] = schema,
        [StudioRuntimeConstants.PackageEnvironment] = package,
        [StudioRuntimeConstants.InstanceEnvironment] = "instance"
    };
    StudioBootstrapValidation.ValidateAgainstEnvironment(bootstrap, environment, package);
    environment[StudioRuntimeConstants.InstanceEnvironment] = "other";
    AssertThrowsCode(() => StudioBootstrapValidation.ValidateAgainstEnvironment(bootstrap, environment, package), "WB-BOOTSTRAP-003");
    try { Directory.Delete(root, true); } catch { }
});
Run("F56 workspace rejects alternate game tree", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "Mount & Blade II Bannerlord", "Modules", "OtherWorldbook");
    Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(root)!, "Native"));
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(root)!, "Native", "SubModule.xml"), "<Module />");
    try { AssertThrowsCode(() => WorkspacePathPolicy.ValidateWorkspaceRoot(root), "WB-ROOT-006"); }
    finally { try { Directory.Delete(Path.Combine(Path.GetTempPath(), "Mount & Blade II Bannerlord"), true); } catch { } }
});
Run("F57 malformed bootstrap frame", () =>
{
    using var stream = new MemoryStream([0, 0, 0, 0]);
    AssertThrowsCode(() => StudioBootstrapCodec.ReadFrameAsync(stream, CancellationToken.None).GetAwaiter().GetResult(), "WB-BOOTSTRAP-002");
});
Run("F02 missing source hashes", () => AssertCode(MakeWorkspace(schemaRoot, "fixture-invalid-source-version.yaml").Service.Validate(), "WB-SOURCE-001"));
Run("F03 future timeline conflict", () => AssertCode(MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace("universe: awake_current", "universe: warband_future", StringComparison.Ordinal)).Service.Validate(), "WB-TIME-001"));
Run("F04 deny wins over grant", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace("denies: []", "denies: [{profile_id: profile.commoner, scope: local, min_detail: summary}]", StringComparison.Ordinal));
    var preview = fixture.Service.Preview("profile.commoner", "F04");
    Assert(preview.Envelope["npc_preview"]?["results"]?[0]?["status"]?.GetValue<string>() == "redacted", "deny must win");
});
Run("F05 unknown profile", () => AssertCode(MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace("profile.commoner", "profile.unknown", StringComparison.Ordinal)).Service.Validate(), "WB-PROFILE-001"));
Run("F06 redirect cycle", () =>
{
    var redirects = "\nredirects:\n  - {redirect_id: redirect.one, from: entity.one, to: entity.two, reason: {zh-CN: 迁移}, event_id: event.one, revision: 1}\n  - {redirect_id: redirect.two, from: entity.two, to: entity.one, reason: {zh-CN: 迁移}, event_id: event.two, revision: 1}\n";
    AssertCode(MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text + redirects).Service.Validate(), "WB-ID-001");
});
Run("F07 profile preview differs", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace("denies: []", "denies: [{profile_id: profile.noble, scope: local, min_detail: summary}]", StringComparison.Ordinal));
    var commoner = fixture.Service.Preview("profile.commoner", "F07");
    var noble = fixture.Service.Preview("profile.noble", "F07");
    Assert(commoner.Items.Any(x => x.Visibility == "visible"), "commoner should see expression");
    Assert(noble.Items.Any(x => x.Visibility == "redacted"), "noble should be denied");
});
Run("F08 NPC preview isolates diagnostics", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var envelope = fixture.Service.Preview("profile.commoner", "F08").Envelope;
    var npcText = envelope["npc_preview"]!.ToJsonString();
    Assert(!npcText.Contains("source_ids", StringComparison.Ordinal) && !npcText.Contains("rule_ids", StringComparison.Ordinal) && !npcText.Contains("explanation_chain", StringComparison.Ordinal), "NPC DTO leaked author diagnostics");
});
Run("F09 adult tier closure", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace("content_tier: base", "content_tier: adult_optional", StringComparison.Ordinal));
    var result = fixture.Service.Compile("base");
    AssertCode(result.Validation, "WB-TIER-001");
});
Run("F10 suggestions isolated", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    Directory.CreateDirectory(Path.Combine(fixture.Root, "authoring", "suggestions"));
    File.WriteAllText(Path.Combine(fixture.Root, "authoring", "suggestions", "ai.yaml"), "not: valid");
    Assert(fixture.Service.Validate().Valid, "suggestions must not enter compile");
});
Run("F11 confirmation invalidates after source change", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace("content_tier: base", "content_tier: adult_optional", StringComparison.Ordinal));
    var token = fixture.Service.GenerateConfirmationToken("adult_optional");
    File.AppendAllText(Path.Combine(fixture.Root, "authoring", "sources", "source-demo.txt"), "变化");
    var result = fixture.Service.Compile("adult_optional", token);
    AssertCode(result.Validation, "WB-CONFIRM-001");
});
Run("F12 atomic pointer failure keeps current", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var compiled = fixture.Service.Compile();
    var publisher = new AtomicCandidatePublisher(fixture.Workspace);
    var first = publisher.Publish(compiled, "base");
    var pointer = Path.Combine(fixture.Root, "export", "WorldbookV2", "current.json");
    var before = Hashing.FileSha256(pointer);
    try { publisher.Publish(compiled, "base", faultPoint: PublishFaultPoint.PointerReplaceFailure); } catch (IOException) { }
    Assert(Hashing.FileSha256(pointer) == before, "pointer changed after injected failure");
    Assert(File.Exists(Path.Combine(first, "complete.marker")), "old candidate disappeared");
});
Run("F13 deterministic reorder boundary", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var one = fixture.Service.Compile();
    var two = fixture.Service.Compile();
    Assert(one.ManifestHash == two.ManifestHash, "same input must hash identically");
});
Run("F14 localized preview", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    Assert(fixture.Service.Preview("profile.commoner").Items.Any(x => x.Text == "示例表达"), "Chinese text fallback failed");
});
Run("F15 v1 protected boundary", () =>
{
    var protectedRoot = @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\ModuleData\StudioWorkspace";
    try { _ = new WorkspaceService(new WorkspaceOptions(protectedRoot, schemaRoot)); throw new InvalidOperationException("protected root accepted"); } catch (InvalidOperationException ex) { Assert(ex.Message.Contains("WB-ROOT-001", StringComparison.Ordinal), "wrong root error"); }
});
Run("F16 unapproved canon", () => AssertCode(MakeWorkspace(schemaRoot, "fixture-invalid-canon-unapproved.yaml").Service.Validate(), "WB-CANON-001"));
Run("F17-D approved event missing", () =>
{
    var report = MakeWorkspace(schemaRoot, "fixture-invalid-canon-unapproved.yaml", text => text.Replace("review_status: draft", "review_status: approved", StringComparison.Ordinal)).Service.Validate();
    Assert(report.Diagnostics.Any(x => x.Code is "WB-CANON-001" or "WB-SCHEMA-001"), "missing approval event not blocked");
});
Run("F18 valid schema", () => Assert(MakeWorkspace(schemaRoot).Service.Validate().Valid, "minimal fixture invalid"));
Run("F19 accepted variant", () => Assert(MakeWorkspace(schemaRoot, "fixture-valid-accepted-variant.yaml").Service.Validate().Valid, "accepted variant invalid"));
Run("F20 missing source version", () => AssertCode(MakeWorkspace(schemaRoot, "fixture-invalid-source-version.yaml").Service.Validate(), "WB-SOURCE-001"));
Run("F21 canon approval gate", () => AssertCode(MakeWorkspace(schemaRoot, "fixture-invalid-canon-unapproved.yaml").Service.Validate(), "WB-CANON-001"));
Run("F22 v2 runtime contract schema", () =>
{
    var document = JsonNode.Parse(File.ReadAllText(Path.Combine(contractRoot, "golden-content-input.json")))!.AsObject()["runtime.json"]!.AsObject();
    var report = new ValidationReport();
    new SchemaValidator().Validate(document, Path.Combine(contractRoot, "runtime.schema.json"), report);
    Assert(report.Valid, string.Join("; ", report.Diagnostics.Select(x => x.Message + " " + x.Detail)));
});
Run("F23 cross-tool package hashes", () =>
{
    var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(contractRoot, "golden-manifest-input.json")))!.AsObject();
    var content = JsonNode.Parse(File.ReadAllText(Path.Combine(contractRoot, "golden-content-input.json")))!.AsObject();
    var files = content.Select(x => (x.Key, x.Value!));
    var manifestHash = ContractHashing.ManifestHash(manifest);
    var contentHash = ContractHashing.ContentHash(files);
    var packageHash = ContractHashing.PackageHash(manifestHash, contentHash);
    var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(contractRoot, "golden-hashes.json")))!.AsObject();
    Assert(manifestHash == golden["manifestHash"]!.GetValue<string>(), "manifest golden hash mismatch");
    Assert(contentHash == golden["contentHash"]!.GetValue<string>(), "content golden hash mismatch");
    Assert(packageHash == golden["packageHash"]!.GetValue<string>(), "package golden hash mismatch");
});
Run("A1 canonical authority golden vectors", () =>
{
    var studioRoot = FindStudioRoot(AppContext.BaseDirectory);
    var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(studioRoot, "tests", "fixtures", "a1-authority-golden.v1.json")))!.AsObject();
    foreach (var item in golden["canonical_cases"]!.AsArray().OfType<JsonObject>())
    {
        var input = JsonNode.Parse(item["input"]!.ToJsonString())!;
        Assert(CanonicalJson.Serialize(input) == item["canonical"]!.GetValue<string>(), $"canonical vector changed: {item["name"]}");
        Assert(CanonicalJson.Hash(input) == item["hash"]!.GetValue<string>(), $"canonical hash changed: {item["name"]}");
    }

    var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(contractRoot, "golden-manifest-input.json")))!.AsObject();
    var content = JsonNode.Parse(File.ReadAllText(Path.Combine(contractRoot, "golden-content-input.json")))!.AsObject();
    var files = content.Select(item => (item.Key, item.Value!));
    var manifestHash = ContractHashing.ManifestHash(manifest);
    var contentHash = ContractHashing.ContentHash(files);
    var packageHash = ContractHashing.PackageHash(manifestHash, contentHash);
    var expectedContracts = golden["contract_hashes"]!.AsObject();
    Assert(manifestHash == expectedContracts["manifest_hash"]!.GetValue<string>(), "A1 manifest hash changed");
    Assert(contentHash == expectedContracts["content_hash"]!.GetValue<string>(), "A1 content hash changed");
    Assert(packageHash == expectedContracts["package_hash"]!.GetValue<string>(), "A1 package hash changed");

    var objectInput = JsonNode.Parse(golden["canonical_cases"]!.AsArray()[0]!.AsObject()["input"]!.ToJsonString())!;
    Assert(CanonicalJson.Hash(objectInput) == golden["object_hash"]!.GetValue<string>(), "A1 object hash changed");
    var ledger = JsonNode.Parse("{\"id\":\"doc.sample\",\"revision\":1,\"last_state\":\"active\",\"record_hash\":\"ignored\"}")!.AsObject();
    ledger.Remove("record_hash");
    Assert(CanonicalJson.Hash(ledger) == golden["ledger_record_hash"]!.GetValue<string>(), "A1 ledger hash changed");

    var forward = new[] { ("A.yaml", (JsonNode)JsonValue.Create("one")!), ("a.yaml", (JsonNode)JsonValue.Create("two")!) };
    var reverse = forward.Reverse();
    var collision = golden["path_collision"]!.AsObject();
    Assert(ContractHashing.ContentHash(forward) == collision["forward_hash"]!.GetValue<string>(), "A1 forward path collision changed");
    Assert(ContractHashing.ContentHash(reverse) == collision["reverse_hash"]!.GetValue<string>(), "A1 reverse path collision changed");
});
Run("A1 shared provider and revision boundaries", () =>
{
    Assert(WorldbookInputNormalization.NormalizeProvider(" Cloud ") == "cloud", "provider normalization changed");
    Assert(WorldbookInputNormalization.NormalizeProvider("LOCAL") == "local", "provider normalization changed");
    AssertThrowsCode(() => WorldbookInputNormalization.NormalizeProvider("remote"), "WB-AI-PROVIDER-400");
    Assert(WorldbookInputNormalization.ReadRevision(new JsonObject()) == 1, "missing revision default changed");
    Assert(WorldbookInputNormalization.ReadRevision(new JsonObject { ["revision"] = 7L }) == 7, "revision read changed");
    Assert(WorldbookInputNormalization.ReadRevision(new JsonObject { ["revision"] = -3L }) == -3, "negative revision behavior changed");
    try
    {
        _ = WorldbookInputNormalization.ReadRevision(new JsonObject { ["revision"] = "invalid" });
        throw new InvalidOperationException("invalid revision type should fail");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Int64", StringComparison.OrdinalIgnoreCase)) { }
});
Run("F24 compiled runtime package contract", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace(
        "grants: [{profile_id: profile.commoner, scope: local, min_detail: summary}]",
        "grants: [{profile_id: profile.commoner, scope: local, min_detail: summary, culture_ids: [entity.culture.vlandia], kingdom_ids: [entity.kingdom.vlandia], settlement_ids: [entity.settlement.pravend], role_ids: [entity.role.headman], min_age: 45, max_age: 70, min_steward: 80, min_management: 90}]",
        StringComparison.Ordinal));
    var compiled = fixture.Service.Compile();
    var runtime = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(compiled.Files["runtime.json"]))!.AsObject();
    var manifest = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(compiled.Files["package-manifest.json"]))!.AsObject();
    var validator = new SchemaValidator();
    var runtimeReport = new ValidationReport();
    validator.Validate(runtime, Path.Combine(contractRoot, "runtime.schema.json"), runtimeReport);
    var manifestReport = new ValidationReport();
    validator.Validate(manifest, Path.Combine(contractRoot, "package-manifest.schema.json"), manifestReport);
    Assert(runtimeReport.Valid, "runtime contract invalid: " + string.Join("; ", runtimeReport.Diagnostics.Select(x => x.Message + " " + x.Detail)));
    Assert(manifestReport.Valid, "manifest contract invalid: " + string.Join("; ", manifestReport.Diagnostics.Select(x => x.Message + " " + x.Detail)));
    var conditions = runtime["entries"]?[0]?["expressions"]?[0]?["grants"]?[0]?["conditions"]?.AsObject();
    Assert(conditions?["culture_ids"] is JsonArray && conditions?["kingdom_ids"] is JsonArray && conditions?["settlement_ids"] is JsonArray && conditions?["role_ids"] is JsonArray, "runtime compiler dropped identity conditions");
    Assert(conditions?["culture_ids"]?[0]?.GetValue<string>() == "awake:culture:vlandia", "culture condition lost its type");
    Assert(conditions?["kingdom_ids"]?[0]?.GetValue<string>() == "awake:kingdom:vlandia", "kingdom condition lost its type");
    Assert(conditions?["settlement_ids"]?[0]?.GetValue<string>() == "awake:settlement:pravend", "settlement condition lost its type");
    Assert(conditions?["role_ids"]?[0]?.GetValue<string>() == "awake:role:headman", "role condition lost its type");
    Assert(conditions?["min_age"]?.GetValue<int>() == 45 && conditions?["min_management"]?.GetValue<int>() == 90, "runtime compiler dropped numeric identity conditions");
});
Run("F25 every contract schema resolves", () =>
{
    foreach (var path in Directory.EnumerateFiles(contractRoot, "*.schema.json", SearchOption.TopDirectoryOnly))
    {
        var report = new ValidationReport();
        new SchemaValidator().Validate(new JsonObject(), path, report);
        Assert(!report.Diagnostics.Any(x => x.Code == "WB-SCHEMA-000"), Path.GetFileName(path) + " could not load: " + string.Join("; ", report.Diagnostics.Select(x => x.Detail)));
    }
});
Run("F26 public referral compiles into runtime grant", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace(
        "        denies: []",
        "        fallback_referral_ids: [referral.notary_merchant]\n        denies: []",
        StringComparison.Ordinal));
    var compiled = fixture.Service.Compile();
    var runtime = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(compiled.Files["runtime.json"]))!.AsObject();
    var grant = runtime["entries"]?[0]?["expressions"]?[0]?["grants"]?[0]?.AsObject();
    Assert(grant?["referral_ids"]?[0]?.GetValue<string>() == "awake:referral:notary_merchant", "fallback referral was not mapped to runtime grant");
    Assert(runtime["referrals"]?.AsArray().Any(x => x?["id"]?.GetValue<string>() == "awake:referral:notary_merchant" && x?["publiclyAskable"]?.GetValue<bool>() == true) == true, "runtime referral lost public marker");
});
Run("F27 unknown referral is rejected", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace(
        "        denies: []",
        "        fallback_referral_ids: [referral.missing_target]\n        denies: []",
        StringComparison.Ordinal));
    AssertCode(fixture.Service.Validate(), "WB-REFERRAL-001");
});
Run("F28 disabled or denied preview emits no referral", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace(
        "        denies: []",
        "        fallback_referral_ids: [referral.notary_merchant]\n        denies: [{profile_id: profile.commoner, scope: local, min_detail: summary}]",
        StringComparison.Ordinal));
    var preview = fixture.Service.Preview("profile.commoner", "F28");
    Assert(preview.Items.All(x => x.ReferralIds is null || x.ReferralIds.Length == 0), "denied preview leaked referral");
});
Run("F29 unknown preview emits public referral", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace(
        "grants: [{profile_id: profile.commoner, scope: local, min_detail: summary}]",
        "fallback_referral_ids: [referral.notary_merchant]\n        grants: [{profile_id: profile.noble, scope: elite, min_detail: secret}]",
        StringComparison.Ordinal));
    var preview = fixture.Service.Preview("profile.commoner", "F29");
    Assert(preview.Items.Any(x => x.ReferralIds?.Contains("referral.notary_merchant", StringComparer.Ordinal) == true), "unknown preview did not expose public referral");
});
Run("F30 direct preview suppresses fallback referral", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace(
        "        denies: []",
        "        fallback_referral_ids: [referral.notary_merchant]\n        denies: []",
        StringComparison.Ordinal));
    var preview = fixture.Service.Preview("profile.commoner", "F30");
    Assert(preview.Items.All(x => x.ReferralIds is null || x.ReferralIds.Length == 0), "directly visible preview exposed fallback referral");
});
Run("F75 default compiled output commits inside compiled root", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var output = fixture.Service.WriteCompiled(fixture.Service.Compile());
    var expected = Path.GetFullPath(Path.Combine(fixture.Root, "compiled"));
    Assert(string.Equals(Path.GetFullPath(output), expected, StringComparison.OrdinalIgnoreCase), "default compiled output should commit to workspace compiled root");
    Assert(File.Exists(Path.Combine(output, "manifest.json")), "default compiled output should contain manifest.json");
});
Run("F76 published manifest preserves runtime hash contract", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var compiled = fixture.Service.Compile();
    var candidate = new AtomicCandidatePublisher(fixture.Workspace).Publish(compiled, "base");
    var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(candidate, "manifest.json")))!.AsObject();
    var packageManifest = JsonNode.Parse(File.ReadAllText(Path.Combine(candidate, "package-manifest.json")))!.AsObject();
    var runtime = JsonNode.Parse(File.ReadAllText(Path.Combine(candidate, "runtime.json")))!;
    var index = JsonNode.Parse(File.ReadAllText(Path.Combine(candidate, "index.json")))!;
    var hashes = manifest["hashes"]!.AsObject();
    Assert(CanonicalJson.Serialize(manifest) == CanonicalJson.Serialize(packageManifest), "published manifest and package-manifest must have one authoritative representation");
    Assert(ContractHashing.ManifestHash(manifest) == hashes["manifestHash"]!.GetValue<string>(), "published manifest hash must verify");
    Assert(ContractHashing.ContentHash(new[] { ("runtime.json", runtime), ("index.json", index) }) == hashes["contentHash"]!.GetValue<string>(), "published content hash must verify");
    Assert(ContractHashing.PackageHash(hashes["manifestHash"]!.GetValue<string>(), hashes["contentHash"]!.GetValue<string>()) == hashes["packageHash"]!.GetValue<string>(), "published package hash must verify");
});
Run("F77 published candidate passes AWAKE package integrity", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var compiled = fixture.Service.Compile();
    var candidate = new AtomicCandidatePublisher(fixture.Workspace).Publish(compiled, "base");
    var verified = Awake.WorldbookPackageIntegrity.ReadAndVerify(Path.Combine(candidate, "manifest.json"));
    Assert(verified.ManifestHash == JsonNode.Parse(File.ReadAllText(Path.Combine(candidate, "manifest.json")))!["hashes"]!["manifestHash"]!.GetValue<string>(), "AWAKE verifier returned a different manifest hash");
    Assert(verified.ContentHash.Length == 64 && verified.PackageHash.Length == 64, "AWAKE verifier did not retain package hashes");
});
Run("F78 published candidate loads and answers through AWAKE query", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var compiled = fixture.Service.Compile();
    var candidate = new AtomicCandidatePublisher(fixture.Workspace).Publish(compiled, "base");
    var verified = Awake.WorldbookPackageIntegrity.ReadAndVerify(Path.Combine(candidate, "manifest.json"));
    var snapshot = Awake.WorldKnowledgeLoader.LoadVerified(verified);
    var service = new Awake.WorldKnowledgeQueryService(snapshot);
    var result = service.Query(new Awake.WorldbookQuery
    {
        IdentityId = "profile.commoner",
        ContentTier = "base",
        KnowledgeScope = "local",
        KnowledgeScopeAvailable = true,
        EffectiveDetail = "summary",
        EffectiveDetailAvailable = true,
        RequestedDetail = "summary",
        PlayerText = "示例政治档案",
        MaximumBytes = 4096
    });
    Assert(result.State is "known" or "partial", "AWAKE query did not retrieve a Studio entry");
    Assert(result.RetrievedText.Contains("示例政治档案", StringComparison.Ordinal), "AWAKE query result lost the localized entry title");
});
Run("F79 output rejects missing compile snapshot", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var original = fixture.Service.Compile();
    var incomplete = new CompileResult(original.Validation)
    {
        Manifest = original.Manifest,
        ManifestHash = original.ManifestHash,
        ConfirmationToken = original.ConfirmationToken
    };
    foreach (var item in original.Files) incomplete.Files[item.Key] = item.Value;
    var publisher = new AtomicCandidatePublisher(fixture.Workspace);
    var exportRoot = Path.Combine(fixture.Root, "export", "WorldbookV2");
    AssertThrowsCode(() => publisher.Publish(incomplete, "base", outputRoot: exportRoot), "WB-PUBLISH-003");
    Assert(!Directory.Exists(exportRoot) || !Directory.EnumerateDirectories(exportRoot).Any(x => Path.GetFileName(x).StartsWith("WorldbookV2-", StringComparison.Ordinal)), "incomplete compile result must not publish");
    AssertThrowsCode(() => fixture.Service.WriteCompiled(incomplete, Path.Combine(fixture.Root, "compiled", "current")), "WB-COMPILE-003");
});
Run("F80 output rechecks runtime content hash", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var compiled = fixture.Service.Compile();
    compiled.Files["runtime.json"] = Encoding.UTF8.GetBytes("{\"schemaVersion\":\"awake.worldbook.v2\",\"tampered\":true}");
    var exportRoot = Path.Combine(fixture.Root, "export", "WorldbookV2");
    AssertThrowsCode(() => new AtomicCandidatePublisher(fixture.Workspace).Publish(compiled, "base", outputRoot: exportRoot), "WB-PUBLISH-004");
    Assert(!Directory.Exists(exportRoot) || !Directory.EnumerateDirectories(exportRoot).Any(x => Path.GetFileName(x).StartsWith("WorldbookV2-", StringComparison.Ordinal)), "tampered compile result must not publish");
});
Run("F81 successful publish releases lock file", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var exportRoot = Path.Combine(fixture.Root, "export", "WorldbookV2");
    _ = new AtomicCandidatePublisher(fixture.Workspace).Publish(fixture.Service.Compile(), "base", outputRoot: exportRoot);
    Assert(!File.Exists(Path.Combine(exportRoot, ".publish.lock")), "successful publish must release lock file");
});
Run("F31 editor lists and reads authoring documents", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var documents = fixture.Service.ListDocuments();
    Assert(documents.Count == 1, "editor should list one authoring document");
    Assert(documents[0].Path == "authoring/demo.yaml", "editor path should be workspace-relative");
    Assert(documents[0].Title == "示例政治档案", "editor should expose localized title");
    var document = fixture.Service.ReadDocument("authoring/demo.yaml");
    Assert(document.Content.Contains("schema_version: awake.worldbook.authoring.v1", StringComparison.Ordinal), "editor should return raw authoring content");
    Assert(document.Report.Valid, "read document should include its validation report");
});
Run("F32 new authoring document is immediately editable", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var created = fixture.Service.CreateDocument("authoring/new-record.yaml", "doc.politics.new_record", "新政治档案", "politics", "base");
    Assert(created.Path == "authoring/new-record.yaml", "new document path should be normalized");
    Assert(created.Content.Contains("doc.politics.new_record", StringComparison.Ordinal), "new document should contain requested id");
    Assert(created.Report.Valid, $"new document template should validate: {string.Join("; ", created.Report.Diagnostics.Select(x => $"{x.Code}@{x.Path}:{x.Message}:{x.Detail}"))}");
    var before = Hashing.FileSha256(Path.Combine(fixture.Root, "authoring", "demo.yaml"));
    var first = fixture.Service.CreateGeneratedDocument("自动生成档案", "politics");
    var second = fixture.Service.CreateGeneratedDocument("自动生成档案", "politics");
    var firstSlug = Path.GetFileNameWithoutExtension(first.Path)["entry-".Length..];
    var secondSlug = Path.GetFileNameWithoutExtension(second.Path)["entry-".Length..];
    var firstId = first.Document["id"]?.GetValue<string>() ?? string.Empty;
    var secondId = second.Document["id"]?.GetValue<string>() ?? string.Empty;
    Assert(first.Path.StartsWith("authoring/politics/entry-", StringComparison.Ordinal) && first.Path.EndsWith(".yaml", StringComparison.Ordinal), "generated path should stay inside the selected domain");
    Assert(Guid.TryParseExact(firstSlug, "N", out _) && Guid.TryParseExact(secondSlug, "N", out _), "generated slug should use a stable machine-safe suffix");
    Assert(first.Path != second.Path && firstId != secondId, "repeated creation must not overwrite or reuse internal identity");
    Assert(firstId == first.Document["id"]?.GetValue<string>(), "generated identity must remain stable after creation");
    Assert(first.Report.Valid && second.Report.Valid, "generated documents must pass the existing v1 schema");
    Assert(Hashing.FileSha256(Path.Combine(fixture.Root, "authoring", "demo.yaml")) == before, "creating a document must not modify an existing v1 document");

    var studioRoot = FindStudioRoot(AppContext.BaseDirectory);
    var html = File.ReadAllText(Path.Combine(studioRoot, "src", "Awake.WorldbookStudio.Web", "wwwroot", "index.html"));
    Assert(!html.Contains("id=\"newPath\"", StringComparison.Ordinal), "new-document dialog must not expose a path input");
    Assert(!html.Contains("id=\"newId\"", StringComparison.Ordinal), "new-document dialog must not expose an internal ID input");
    Assert(html.Contains("不用填写文件位置或英文 ID", StringComparison.Ordinal), "new-document dialog must explain automatic technical fields");
    var createStart = html.IndexOf("async function createDocument", StringComparison.Ordinal);
    var createEnd = html.IndexOf("function bootstrap", createStart, StringComparison.Ordinal);
    Assert(createStart >= 0 && createEnd > createStart, "new-document client flow should remain discoverable");
    var createFunction = html[createStart..createEnd];
    Assert(!createFunction.Contains("path:", StringComparison.Ordinal) && !createFunction.Contains("documentId:", StringComparison.Ordinal), "new-document client request must send author fields only");
    var ux = File.ReadAllText(Path.Combine(studioRoot, "src", "Awake.WorldbookStudio.Web", "wwwroot", "studio-authoring-ux.js"));
    var ai = File.ReadAllText(Path.Combine(studioRoot, "src", "Awake.WorldbookStudio.Web", "wwwroot", "studio-ai.js"));
    Assert(html.Contains("studio-authoring-ux.js", StringComparison.Ordinal), "author UX layer is not loaded by the Web entry page");
    Assert(html.Contains("function focusTarget", StringComparison.Ordinal) && html.Contains("button.dataset.target", StringComparison.Ordinal), "diagnostic navigation does not expose a concrete field target");
    Assert(ux.Contains("data-target", StringComparison.Ordinal) && ux.Contains("data-empty-action", StringComparison.Ordinal) && ux.Contains("clear-filter", StringComparison.Ordinal), "author navigation or empty workspace actions are not wired");
    Assert(ux.Contains("只发送已保存内容，不包含内部编号、版本号或注册表信息。", StringComparison.Ordinal), "AI scope still exposes technical identity fields");
    Assert(ux.Contains("WB-AI-PROVIDER-409", StringComparison.Ordinal) && ux.Contains("providerLoadPromise", StringComparison.Ordinal), "AI errors or provider loading are not normalized for the author UI");
    Assert(ai.Contains("friendlyPatchPath", StringComparison.Ordinal) && ai.Contains("可能影响：", StringComparison.Ordinal), "AI suggestion field paths are not translated for authors");
});
Run("A3.1 authoring template matches independent golden", () =>
{
    var assembly = typeof(WorldbookApplicationService).Assembly;
    var factoryType = assembly.GetType("Awake.WorldbookStudio.Core.AuthoringTemplateFactory", throwOnError: false);
    Assert(factoryType is not null && !factoryType.IsPublic, "A3.1 factory must be an internal type");
    Assert(factoryType!.GetMethod("Create", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is not null, "A3.1 factory Create seam is missing");
    var factorySource = File.ReadAllText(Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "src", "Awake.WorldbookStudio.Core", "AuthoringTemplateFactory.cs"));
    foreach (var forbidden in new[] { "WorkspaceService", "File.", "Directory.", "FileInfo", "DirectoryInfo" })
        Assert(!factorySource.Contains(forbidden, StringComparison.Ordinal), "A3.1 factory contains forbidden I/O dependency: " + forbidden);
    AssertA31RunManifest();

    using var fixture = MakeWorkspace(schemaRoot);
    var golden = LoadA31Golden();
    Assert(string.Equals(Hashing.FileSha256(Path.Combine(schemaRoot, "profile-registry.v1.json")), golden["registry"]!["profile_hash"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase), "profile registry golden hash drifted");
    Assert(string.Equals(Hashing.FileSha256(Path.Combine(schemaRoot, "referral-registry.v1.json")), golden["registry"]!["referral_hash"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase), "referral registry golden hash drifted");
    var created = fixture.Service.CreateDocument("authoring/a3-1-golden.yaml", golden["input"]!["document_id"]!.GetValue<string>(), golden["input"]!["title"]!.GetValue<string>(), "politics", "base", "author.developer");
    Assert(created.Report.Valid, "golden template must validate");
    AssertA31Golden(created.Document, golden);
    AssertSerializationOrder(created.Content, golden["expected"]!.AsObject()["yaml_key_sequence"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray(), yaml: true);
});
Run("A3.1 authoring YAML and JSON preserve normalized shape", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var golden = LoadA31Golden();
    var args = ("doc.politics.a3_1_golden", "  A3.1 模板基线  ", "politics", "base", "author.developer");
    var yaml = fixture.Service.CreateDocument("authoring/a3-1-format.yaml", args.Item1, args.Item2, args.Item3, args.Item4, args.Item5);
    var json = fixture.Service.CreateDocument("authoring/a3-1-format.json", args.Item1, args.Item2, args.Item3, args.Item4, args.Item5);
    var upperJson = fixture.Service.CreateDocument("authoring/a3-1-format.JSON", args.Item1, args.Item2, args.Item3, args.Item4, args.Item5);
    Assert(yaml.Report.Valid && json.Report.Valid && upperJson.Report.Valid, "all authoring formats must validate");
    Assert(CanonicalJson.Hash(yaml.Document) == CanonicalJson.Hash(json.Document), "YAML and JSON canonical documents differ");
    Assert(CanonicalJson.Hash(json.Document) == CanonicalJson.Hash(upperJson.Document), "uppercase JSON extension changed the document");
    AssertDiagnosticsEqual(yaml.Report, json.Report);
    AssertDiagnosticsEqual(json.Report, upperJson.Report);
    AssertSerializationOrder(yaml.Content, golden["expected"]!.AsObject()["yaml_key_sequence"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray(), yaml: true);
    AssertSerializationOrder(json.Content, golden["expected"]!.AsObject()["json_key_sequence"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray(), yaml: false);
    var defaultTitle = fixture.Service.CreateDocument("authoring/a3-1-default.yaml", "doc.politics.a3_1_default", "   ", "politics", "base", "author.developer");
    Assert(defaultTitle.Document["title"]?["zh-CN"]?.GetValue<string>() == "未命名世界书档案", "blank title default changed");
});
Run("A3.1 CreateDocument preserves validation failure boundaries", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    AssertCreateDocumentFailure(fixture, "authoring/a3-1-invalid-id.yaml", "bad", "politics", "base", "author.developer", "WB-DOC-001");
    AssertCreateDocumentFailure(fixture, "authoring/a3-1-invalid-domain.yaml", "doc.politics.a3_1_invalid_domain", "invalid", "base", "author.developer", "WB-DOC-002");
    AssertCreateDocumentFailure(fixture, "authoring/a3-1-invalid-tier.yaml", "doc.politics.a3_1_invalid_tier", "politics", "invalid", "author.developer", "WB-DOC-003");
    AssertCreateDocumentFailure(fixture, "authoring/a3-1-invalid-author.yaml", "doc.politics.a3_1_invalid_author", "politics", "base", "bad", "WB-DOC-004");
    AssertCreateDocumentFailure(fixture, "authoring/a3-1-invalid-precedence.yaml", "bad", "invalid", "invalid", "bad", "WB-DOC-001");
});
Run("A3.1 authoring path and registry boundaries", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    AssertCreateDocumentFailure(fixture, "", "doc.politics.a3_1_empty_path", "politics", "base", "author.developer", "WB-PATH-001");
    AssertCreateDocumentFailure(fixture, "../a3-1-outside.yaml", "doc.politics.a3_1_outside", "politics", "base", "author.developer", "WB-PATH-003");
    var blocked = Path.Combine(fixture.Root, "authoring", "Modules", "blocked.yaml");
    Directory.CreateDirectory(Path.GetDirectoryName(blocked)!);
    File.WriteAllText(blocked, "old-content");
    AssertCreateDocumentFailure(fixture, "authoring/Modules/blocked.yaml", "doc.politics.a3_1_blocked", "politics", "base", "author.developer", "WB-PATH-004");
    Assert(File.ReadAllText(blocked) == "old-content", "path rejection changed existing content");
    AssertCreateDocumentFailure(fixture, "authoring/a3-1-unknown.txt", "doc.politics.a3_1_unknown", "politics", "base", "author.developer", "WB-SAVE-001");
    var nested = fixture.Service.CreateDocument("nested/a3-1-nested.yaml", "doc.politics.a3_1_nested", "嵌套档案", "politics", "base", "author.developer");
    Assert(nested.Path == "authoring/nested/a3-1-nested.yaml", "nested authoring path normalization changed");
    fixture.Service.CreateDocument("authoring/a3-1-overwrite.yaml", "doc.politics.a3_1_overwrite", "旧标题", "politics", "base", "author.developer");
    var overwrite = fixture.Service.CreateDocument("authoring/a3-1-overwrite.yaml", "doc.politics.a3_1_overwrite", "新标题", "politics", "base", "author.developer");
    Assert(overwrite.Document["title"]?["zh-CN"]?.GetValue<string>() == "新标题", "existing authoring file was not atomically replaced");

    var copiedSchema = CopySchemaRoot(schemaRoot);
    try
    {
        File.Delete(Path.Combine(copiedSchema, "profile-registry.v1.json"));
        using var brokenRegistry = MakeWorkspace(copiedSchema);
        AssertCreateDocumentFailure(brokenRegistry, "authoring/a3-1-registry-missing.yaml", "doc.politics.a3_1_registry_missing", "politics", "base", "author.developer", "WB-DOC-005");
        Assert(!File.Exists(Path.Combine(brokenRegistry.Root, "authoring", "a3-1-registry-missing.yaml")), "registry failure wrote a target document");
    }
    finally { try { Directory.Delete(copiedSchema, true); } catch { } }
});
Run("F33 save returns document validation", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var saved = fixture.Service.SaveAndValidate("authoring/demo.yaml", "schema_version: broken");
    Assert(!saved.Report.Valid, "invalid saved content should be reported");
    Assert(saved.Report.Diagnostics.Any(x => x.Code == "WB-SCHEMA-001"), "invalid saved content should expose schema diagnostic");
});
Run("F58 author editor projection preserves v1 nesting", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var projection = fixture.Service.ReadEditorDocument("authoring/demo.yaml");
    var model = projection["model"]!.AsObject();
    Assert(model["title"]?.GetValue<string>() == "示例政治档案", "editor projection lost localized title");
    Assert(model["domain"]?.GetValue<string>() == "politics", "editor projection lost domain");
    Assert(model["era"]?["label"]?.GetValue<string>() == "当前世界", "editor projection did not explain current era");
    Assert(model["sourceMode"]?.GetValue<string>() == "source", "source-backed document should be read-only");
    var assertion = model["assertions"]!.AsArray()[0]!.AsObject();
    Assert(assertion["sourceMode"]?.GetValue<string>() == "source", "assertion source mode was not projected");
    var expression = assertion["expressions"]!.AsArray()[0]!.AsObject();
    Assert(expression["text"]?.GetValue<string>() == "示例表达", "nested expression text was not projected");
    Assert(expression["grants"]!.AsArray()[0]?["profileId"]?.GetValue<string>() == "profile.commoner", "grant profile was not projected at expression level");
    Assert(expression["grants"]!.AsArray()[0]?["profileLabel"]?.GetValue<string>() == "普通平民", "grant profile label was not localized");
    Assert(model["advanced"]?["fields"]?.AsArray().Any(x => x?.GetValue<string>() == "authority") == true, "advanced authority summary missing");
    Assert(model["advanced"]?["fields"]?.AsArray().Any(x => x?.GetValue<string>() == "registry_bindings") == true, "advanced registry summary missing");
});
Run("F59 editor catalog exposes Chinese author choices", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var catalog = fixture.Service.GetEditorCatalog();
    var profile = catalog["profiles"]!.AsArray().First(x => x?["id"]?.GetValue<string>() == "profile.commoner")!.AsObject();
    Assert(profile["label"]?.GetValue<string>() == "普通平民", "profile label should be Chinese");
    Assert(!string.IsNullOrWhiteSpace(profile["help"]?.GetValue<string>()), "profile help should explain the identity");
    var domains = catalog["domains"]!.AsArray();
    Assert(domains.Select(x => x?["value"]?.GetValue<string>()).SequenceEqual(new[] { "politics", "economy", "culture", "war", "geography" }), "catalog should expose the five domains in stable order");
    Assert(domains.All(x => !string.IsNullOrWhiteSpace(x?["label"]?.GetValue<string>())), "all domain labels should be localized");
    var geography = domains.First(x => x?["value"]?.GetValue<string>() == "geography")!.AsObject();
    Assert(geography["label"]?.GetValue<string>() == "地理", "geography domain should be localized");
    Assert(geography["subdomains"]!.AsArray().Any(x => x?["value"]?.GetValue<string>() == "directions" && x?["label"]?.GetValue<string>() == "方位"), "geography should expose the directions subdomain");
    var geographyDocument = fixture.Service.CreateGeneratedDocument("地理测试档案", "geography", "base", "author.developer", "terrain", new[] { "war" });
    Assert(geographyDocument.Path.StartsWith("authoring/geography/", StringComparison.Ordinal), "geography documents should use the geography authoring path");
    Assert(geographyDocument.Document["subdomain"]?.GetValue<string>() == "terrain", "geography document should preserve its subdomain");
    AssertThrowsCode(() => fixture.Service.CreateGeneratedDocument("错误分类", "geography", "base", "author.developer", "throne"), "WB-TAXONOMY-422");
    Assert(catalog["eraPresets"]!.AsArray().Any(x => x?["value"]?.GetValue<string>() == "current" && x?["label"]?.GetValue<string>() == "当前世界"), "era catalog should use author-facing language");
    Assert(catalog["scopes"]!.AsArray().Any(x => x?["value"]?.GetValue<string>() == "elite" && x?["label"]?.GetValue<string>() == "贵族圈"), "scope catalog should explain elite scope");
    Assert(catalog["profile_registry_hash"]?.GetValue<string>()?.Length == 64, "catalog should expose registry hash for binding");
});
Run("B1 entity catalog exposes base game and uninstalled DLC", () =>
{
    var schemaCopy = CopySchemaRoot(schemaRoot);
    try
    {
        var studioRoot = FindStudioRoot(AppContext.BaseDirectory);
        CopyDirectory(
            Path.Combine(studioRoot, "..", "..", "docs", "mappings", "persona-entity"),
            Path.Combine(schemaCopy, "mappings", "persona-entity"));
        using var fixture = MakeWorkspace(schemaCopy);
        var catalog = fixture.Service.GetEditorCatalog();
        Assert(catalog["entityCatalogAvailable"]?.GetValue<bool>() == true, "entity catalog should be available when its package mapping exists");
        var entityCatalog = catalog["entityCatalog"]!.AsObject();
        var counts = entityCatalog["counts"]!.AsObject();
        Assert(counts["hero"]?.GetValue<int>() == 415, "entity catalog should retain all mapped heroes");
        Assert(counts["hero_base_game"]?.GetValue<int>() == 362, "base game hero count should be 362");
        Assert(counts["hero_official_dlc_not_installed"]?.GetValue<int>() == 53, "uninstalled official DLC hero count should be 53");
        Assert(counts["clan_base_game"]?.GetValue<int>() == 73, "base game clan count should be 73");
        Assert(counts["clan_official_dlc_not_installed"]?.GetValue<int>() == 9, "uninstalled official DLC clan count should be 9");
        var entities = entityCatalog["entities"]!.AsArray();
        Assert(entities.Count(x => x?["type"]?.GetValue<string>() == "hero") == 415, "all mapped hero names should be author-visible");
        Assert(entities.Any(x => x?["type"]?.GetValue<string>() == "hero" && x?["worldSource"]?.GetValue<string>() == "official_dlc" && x?["availability"]?.GetValue<string>() == "not_installed"), "DLC heroes should be labeled as official DLC and currently uninstalled");
        var wire = catalog.ToJsonString();
        Assert(!wire.Contains("entity.hero.", StringComparison.Ordinal), "ordinary catalog must not expose entity IDs");
        Assert(!wire.Contains("hero_code", StringComparison.Ordinal), "ordinary catalog must not expose raw hero codes");
    }
    finally
    {
        try { Directory.Delete(schemaCopy, true); } catch { }
    }
});
Run("B1 entity catalog degrades without mapping package", () =>
{
    var schemaCopy = CopySchemaRoot(schemaRoot);
    try
    {
        using var fixture = MakeWorkspace(schemaCopy);
        var catalog = fixture.Service.GetEditorCatalog();
        Assert(catalog["entityCatalogAvailable"]?.GetValue<bool>() == false, "entity catalog should be unavailable without its mapping package");
        Assert(catalog["profiles"]!.AsArray().Count > 0, "profile catalog must remain available during entity catalog fallback");
    }
    finally
    {
        try { Directory.Delete(schemaCopy, true); } catch { }
    }
});
Run("K1 runtime keywords carry titles document aliases and entity anchor names", () =>
{
    var schemaCopy = CopySchemaRoot(schemaRoot);
    try
    {
        var studioRoot = FindStudioRoot(AppContext.BaseDirectory);
        CopyDirectory(
            Path.Combine(studioRoot, "..", "..", "docs", "mappings", "persona-entity"),
            Path.Combine(schemaCopy, "mappings", "persona-entity"));
        using var fixture = MakeWorkspace(schemaCopy, "fixture-valid-minimal.yaml", text => text.Replace(
            "authority:",
            "entity_ids:\n  - entity.settlement.town_v3\naliases:\n  zh-CN:\n    - 帕拉汶德\n  en:\n    - Demo Alias\nauthority:",
            StringComparison.Ordinal));
        var compiled = fixture.Service.Compile();
        Assert(compiled.Validation.Valid, "compile must stay valid with aliases and entity anchors");
        var runtime = JsonNode.Parse(Encoding.UTF8.GetString(compiled.Files["runtime.json"]))!.AsObject();
        var keywords = runtime["entries"]![0]!["keywords"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Assert(keywords.Contains("示例政治档案"), "document title must stay a runtime keyword");
        Assert(keywords.Contains("Demo Alias"), "document aliases must become runtime keywords");
        Assert(keywords.Contains("帕拉汶德"), "entity anchor display name must become a runtime keyword");
        Assert(keywords.Contains("Pravend"), "entity anchor english name must become a runtime keyword");
        Assert(keywords.Contains("Paravenos"), "entity anchor aliases must become runtime keywords");
        Assert(keywords[^1] == "doc.politics.demo", "internal document id must be kept as the last fallback keyword");
        Assert(keywords.Length > 2 && keywords.Count(x => x == "doc.politics.demo") == 1, "internal document id must no longer be the only keyword");
        var keywordIndex = runtime["indexes"]!["keywordToEntryIds"]!.AsObject();
        Assert(keywordIndex["帕拉汶德"]?[0]?.GetValue<string>() == "awake:entry:politics.demo", "keyword index must resolve the anchor name to its entry");
        Assert(keywordIndex["Demo Alias"]?[0]?.GetValue<string>() == "awake:entry:politics.demo", "keyword index must resolve document aliases to their entry");
    }
    finally
    {
        try { Directory.Delete(schemaCopy, true); } catch { }
    }
});
Run("K1 runtime keywords degrade safely without the entity mapping package", () =>
{
    var schemaCopy = CopySchemaRoot(schemaRoot);
    try
    {
        using var fixture = MakeWorkspace(schemaCopy, "fixture-valid-minimal.yaml", text => text.Replace(
            "authority:",
            "entity_ids:\n  - entity.settlement.town_v3\naliases:\n  zh-CN:\n    - 帕拉汶德\nauthority:",
            StringComparison.Ordinal));
        var compiled = fixture.Service.Compile();
        Assert(compiled.Validation.Valid, "missing entity mapping must not block compilation");
        var runtime = JsonNode.Parse(Encoding.UTF8.GetString(compiled.Files["runtime.json"]))!.AsObject();
        var keywords = runtime["entries"]![0]!["keywords"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Assert(keywords.Contains("帕拉汶德") && keywords[^1] == "doc.politics.demo", "keywords must fall back to title, aliases and the internal id");
        Assert(!keywords.Contains("Pravend"), "anchor english name requires the mapping package and must be absent without it");
    }
    finally
    {
        try { Directory.Delete(schemaCopy, true); } catch { }
    }
});
Run("D1-2 ambiguous keywords are reported without blocking compilation", () =>
{
    using var fixture = MakeWorkspace(schemaRoot, "fixture-valid-minimal.yaml", text => text.Replace(
        "authority:",
        "aliases:\n  zh-CN:\n    - 同名检索词\nauthority:",
        StringComparison.Ordinal));
    var demo = File.ReadAllText(Path.Combine(fixture.Root, "authoring", "demo.yaml"));
    File.WriteAllText(
        Path.Combine(fixture.Root, "authoring", "second.yaml"),
        demo.Replace("id: doc.politics.demo", "id: doc.politics.demo_second", StringComparison.Ordinal)
            .Replace("title: {zh-CN: 示例政治档案}", "title: {zh-CN: 示例政治档案二}", StringComparison.Ordinal));
    var compiled = fixture.Service.Compile();
    Assert(compiled.Validation.Valid, "ambiguity diagnostics must not block compilation");
    var runtime = JsonNode.Parse(Encoding.UTF8.GetString(compiled.Files["runtime.json"]))!.AsObject();
    var hits = runtime["indexes"]!["keywordToEntryIds"]!["同名检索词"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
    Assert(hits.Length == 2, "both documents must be indexed under the shared keyword");
    var diagnostic = compiled.Validation.Diagnostics.FirstOrDefault(x => x.Code == "WB-INDEX-AMBIGUOUS" && x.Message.Contains("同名检索词", StringComparison.Ordinal));
    Assert(diagnostic is not null, "ambiguous keyword must produce a diagnostic");
    Assert(diagnostic!.Severity == "warning", "ambiguity must be a warning, not an error");
    Assert(diagnostic.Message.Contains("2", StringComparison.Ordinal), "ambiguity diagnostic must state how many entries matched");
    Assert(diagnostic.Detail is not null
        && diagnostic.Detail.Contains("awake:entry:politics.demo_second", StringComparison.Ordinal)
        && diagnostic.Detail.Contains("awake:entry:politics.demo", StringComparison.Ordinal), "ambiguity diagnostic must list the matched entry ids");
});
Run("compile settlement public projection tolerates null diagnostic fields", () =>
{
    var envelope = new JsonObject
    {
        ["schema_version"] = "awake.worldbook.authority.compile-settlement.v1",
        ["validation"] = new JsonObject
        {
            ["valid"] = true,
            ["diagnostics"] = new JsonArray
            {
                new JsonObject
                {
                    ["code"] = "WB-INDEX-AMBIGUOUS",
                    ["severity"] = "warning",
                    ["message"] = "关键词“同名检索词”同时命中 2 条词条。",
                    ["path"] = null,
                    ["detail"] = null
                }
            }
        }
    };
    var projection = CompileSettlementSupport.PublicProjection(envelope);
    var diagnostic = projection["validation"]!["diagnostics"]![0]!.AsObject();
    Assert(diagnostic["code"]!.GetValue<string>() == "WB-INDEX-AMBIGUOUS", "projection must keep the diagnostic code");
    Assert(diagnostic["path"] is null && diagnostic["detail"] is null, "null diagnostic fields must survive projection as JSON null");
});
Run("F60 source-backed author save rejects edits", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var projection = fixture.Service.ReadEditorDocument("authoring/demo.yaml");
    var model = projection["model"]!.AsObject();
    model["summary"] = "作者模式不应改写来源摘要";
    AssertThrowsCode(() => fixture.Service.SaveEditorDocument(
        "authoring/demo.yaml",
        model,
        projection["sourceHash"]!.GetValue<string>(),
        projection["revision"]!.GetValue<int>(),
        projection["registry"]!.AsObject()), "WB-EDITOR-SOURCE-403");
});
Run("F61 author save increments layered revisions", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    fixture.Service.CreateDocument("authoring/author-save.yaml", "doc.politics.author_save", "作者保存测试", "politics");
    var projection = fixture.Service.ReadEditorDocument("authoring/author-save.yaml");
    var model = projection["model"]!.AsObject();
    model["title"] = "更新后的档案标题";
    model["summary"] = "更新后的档案摘要";
    var assertion = model["assertions"]!.AsArray()[0]!.AsObject();
    assertion["text"] = "更新后的客观事实";
    var expression = assertion["expressions"]!.AsArray()[0]!.AsObject();
    expression["text"] = "更新后的 NPC 表达";
    var saved = fixture.Service.SaveEditorDocument(
        "authoring/author-save.yaml",
        model,
        projection["sourceHash"]!.GetValue<string>(),
        projection["revision"]!.GetValue<int>(),
        projection["registry"]!.AsObject());
    var document = fixture.Service.ReadDocument("authoring/author-save.yaml").Document;
    var savedAssertion = document["assertions"]!.AsArray()[0]!.AsObject();
    var savedExpression = savedAssertion["expressions"]!.AsArray()[0]!.AsObject();
    Assert(saved["model"]?["title"]?.GetValue<string>() == "更新后的档案标题", "author save should return refreshed projection");
    Assert(document["revision"]?.GetValue<long>() == 2, "document revision should increment once");
    Assert(savedAssertion["revision"]?.GetValue<long>() == 2, "assertion revision should increment once");
    Assert(savedExpression["revision"]?.GetValue<long>() == 2, "expression revision should increment once");
    Assert(document["author_created"]?["review_status"]?.GetValue<string>() == "draft", "author edit must remain draft");
    Assert(savedAssertion["text"]?["zh-CN"]?.GetValue<string>() == "更新后的客观事实", "assertion text should be saved");
    Assert(savedExpression["text"]?["zh-CN"]?.GetValue<string>() == "更新后的 NPC 表达", "expression text should be saved");

    fixture.Service.CreateDocument("authoring/advanced-save.yaml", "doc.politics.advanced_save", "高级保存测试", "politics");
    var advancedInitial = fixture.Service.ReadDocument("authoring/advanced-save.yaml");
    var advancedContent = advancedInitial.Content.Replace("高级保存测试", "高级保存已更新", StringComparison.Ordinal);
    var advancedDirectReport = new ValidationReport();
    var advancedDirect = new SafeYamlLoader().Load("authoring/advanced-save.yaml", Encoding.UTF8.GetBytes(advancedContent), advancedDirectReport);
    Assert(advancedDirect["title"]?["zh-CN"]?.GetValue<string>() == "高级保存已更新", "YAML loader should retain edited localized text: " + advancedDirect["title"]?.ToJsonString());
    var advancedValidated = fixture.Workspace.ValidateAuthoringContent("authoring/advanced-save.yaml", advancedContent);
    Assert(advancedValidated.Document["title"]?["zh-CN"]?.GetValue<string>() == "高级保存已更新", "advanced validation should retain edited localized text");
    Assert(fixture.Service.FormatAuthoringDocument(advancedValidated.Document, "authoring/advanced-save.yaml").Contains("高级保存已更新", StringComparison.Ordinal), "advanced formatting should retain edited localized text");
    var advancedSaved = fixture.Service.SaveAdvancedDocumentWithEvidence("authoring/advanced-save.yaml", advancedContent, advancedInitial.Report.InputHash!, WorldbookInputNormalization.ReadRevision(advancedInitial.Document));
    Assert(advancedSaved.Document.Document["revision"]?.GetValue<long>() == 2, "advanced save should increment document revision");
    Assert(advancedSaved.Document.Document["title"]?["zh-CN"]?.GetValue<string>() == "高级保存已更新", "advanced save should persist edited localized text");
    var expectedAdvanced = JsonNode.Parse(advancedValidated.Document.ToJsonString())!.AsObject();
    expectedAdvanced["revision"] = 2;
    Assert(advancedSaved.ExpectedContentHash == Hashing.Sha256Text(fixture.Service.FormatAuthoringDocument(expectedAdvanced, "authoring/advanced-save.yaml")), "advanced save should expose the exact expected content hash");
    Assert(advancedSaved.Document.Content == fixture.Service.FormatAuthoringDocument(expectedAdvanced, "authoring/advanced-save.yaml"), "advanced save should return the exact bytes that were written");
    Assert(advancedSaved.Document.Report.InputHash == advancedSaved.ExpectedContentHash, "advanced save readback hash should match the expected content hash");
});
Run("F62 author save rejects stale source hash", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    fixture.Service.CreateDocument("authoring/stale.yaml", "doc.politics.stale", "过期保存测试", "politics");
    var projection = fixture.Service.ReadEditorDocument("authoring/stale.yaml");
    var model = projection["model"]!.AsObject();
    model["summary"] = "第一次保存";
    fixture.Service.SaveEditorDocument("authoring/stale.yaml", model, projection["sourceHash"]!.GetValue<string>(), projection["revision"]!.GetValue<int>(), projection["registry"]!.AsObject());
    AssertThrowsCode(() => fixture.Service.SaveEditorDocument("authoring/stale.yaml", model, projection["sourceHash"]!.GetValue<string>(), projection["revision"]!.GetValue<int>(), projection["registry"]!.AsObject()), "WB-CAS-409");
});
Run("F34 AI request projection excludes protected fields", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var request = new AssistanceService(fixture.Workspace).CreateRequest("authoring/demo.yaml", "cloud", AssistanceAnalysisKind.Consistency);
    Assert(request.DocumentProjection["sources"] is null, "AI projection must not include source references");
    Assert(request.DocumentProjection["authority"] is null, "AI projection must not include authority controls");
    Assert(request.DocumentProjection["registry_bindings"] is null, "AI projection must not include registry hashes");
    Assert(request.RequestHash.Length == 64, "AI request hash must be SHA-256");
});
Run("F35 restricted patch changes only localized text", () =>
{
    var source = JsonNode.Parse("{\"summary\":{\"zh-CN\":\"旧摘要\"},\"id\":\"doc.politics.demo\"}")!;
    var patch = new KnowledgePatch("knowledge-patch.v1", [new KnowledgePatchOperation("replace", "/summary/zh-CN", JsonValue.Create("新摘要"))]);
    var result = JsonPatchEngine.Apply(source, patch);
    Assert(result["summary"]?["zh-CN"]?.GetValue<string>() == "新摘要", "allowed localized path should change");
    Assert(result["id"]?.GetValue<string>() == "doc.politics.demo", "control field must remain unchanged");
});
Run("F36 restricted patch rejects authority and root changes", () =>
{
    var source = JsonNode.Parse("{\"authority\":{\"owner\":\"awake_canon\"},\"summary\":{\"zh-CN\":\"旧摘要\"}}")!;
    AssertThrowsCode(() => JsonPatchEngine.Apply(source, new KnowledgePatch("knowledge-patch.v1", [new KnowledgePatchOperation("replace", "/authority/owner", JsonValue.Create("other"))])), "WB-AI-PATCH-403");
    AssertThrowsCode(() => JsonPatchEngine.Apply(source, new KnowledgePatch("knowledge-patch.v1", [new KnowledgePatchOperation("replace", "", JsonNode.Parse("{}"))])), "WB-AI-PATCH-403");
});
Run("F37 suggestions stay outside authoring document enumeration", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var suggestions = fixture.Workspace.Policy.RequireSuggestions(Path.Combine(fixture.Root, "authoring", "suggestions", "suggestion.json"));
    Directory.CreateDirectory(Path.GetDirectoryName(suggestions)!);
    File.WriteAllText(suggestions, "{}");
    Assert(fixture.Workspace.LoadAuthoringDocuments().Count == 1, "suggestions must not be treated as authoring documents");
});
Run("F38 provider status redacts endpoint and secret", () =>
{
    var settings = ProviderConfiguration.FromEnvironment(new Dictionary<string, string?>
    {
        ["WORLD_BOOK_CLOUD_BASE_URL"] = "https://secret.example.invalid/v1",
        ["WORLD_BOOK_CLOUD_MODEL"] = "model-private",
        ["WORLD_BOOK_CLOUD_API_KEY_ENV"] = "MY_SECRET_KEY",
        ["MY_SECRET_KEY"] = "actual-secret"
    });
    var status = settings.GetStatus("cloud");
    var text = JsonSerializer.Serialize(status);
    Assert(status.State == "configured", "cloud configuration should be recognized");
    Assert(!text.Contains("secret.example", StringComparison.OrdinalIgnoreCase) && !text.Contains("actual-secret", StringComparison.Ordinal), "provider status leaked secret configuration");
});
Run("F39 provider endpoint policy rejects insecure and private cloud targets", () =>
{
    AssertThrowsCode(() => ProviderEndpointPolicy.ValidateCloudBaseUri(new Uri("http://example.com/v1")), "WB-AI-ENDPOINT-403");
    AssertThrowsCode(() => ProviderEndpointPolicy.ValidateCloudBaseUri(new Uri("https://127.0.0.1/v1")), "WB-AI-ENDPOINT-403");
    ProviderEndpointPolicy.ValidateCloudBaseUri(new Uri("https://example.com/v1"));
});
Run("F40 assistance result parser rejects unknown fields", () =>
{
    var raw = JsonNode.Parse("{\"schema_version\":\"assistance.result.v1\",\"request_hash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"source_document_hash\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"suggestions\":[],\"unexpected\":true}")!;
    AssertThrowsCode(() => AssistanceResultParser.Parse(raw), "WB-AI-FORMAT-JSON");
});
Run("F41 OpenAI-compatible response extracts structured content", () =>
{
    var raw = JsonNode.Parse("{\"choices\":[{\"message\":{\"content\":\"{\\\"schema_version\\\":\\\"assistance.result.v1\\\",\\\"request_hash\\\":\\\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\\\",\\\"source_document_hash\\\":\\\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\\\",\\\"suggestions\\\":[]}\"}}]}")!;
    var result = AssistanceResultParser.Parse(OpenAICompatibleResponseParser.ExtractJson(raw));
    Assert(result.Suggestions.Count == 0, "structured provider response should parse");
});
Run("F42 OpenAI-compatible response rejects malformed content", () =>
{
    var raw = JsonNode.Parse("{\"choices\":[{\"message\":{\"content\":\"not-json\"}}]}")!;
    AssertThrowsCode(() => OpenAICompatibleResponseParser.ExtractJson(raw), "WB-AI-FORMAT-JSON");
});
Run("F43 cloud provider sends bearer and parses result", () =>
{
    const string environmentName = "AWAKE_TEST_CLOUD_KEY_F43";
    const string secret = "test-cloud-secret-f43";
    var previous = Environment.GetEnvironmentVariable(environmentName);
    Environment.SetEnvironmentVariable(environmentName, secret);
    try
    {
        var request = MakeAssistanceRequest("cloud-f43");
        var handler = new RecordingHandler(httpRequest =>
        {
            Assert(httpRequest.Headers.Authorization?.Scheme == "Bearer", "cloud request must use bearer authorization");
            Assert(httpRequest.Headers.Authorization?.Parameter == secret, "cloud request bearer value mismatch");
            Assert(httpRequest.RequestUri?.AbsolutePath.EndsWith("/v1/chat/completions", StringComparison.Ordinal) == true, "cloud endpoint path mismatch");
            return JsonResponse(200, CloudResponseJson(request.RequestHash, request.SourceDocumentHash));
        });
        var configuration = new ProviderConfiguration(
            "https://cloud.example.com/v1",
            "model-f43",
            environmentName,
            5,
            128,
            null,
            null);
        var provider = OpenAICompatibleCloudProvider.ForTesting(configuration, () => handler);
        var result = provider.AnalyzeAsync(request).GetAwaiter().GetResult();
        Assert(result.RequestHash == request.RequestHash, "cloud result request hash mismatch");
        Assert(handler.RequestCount == 1, "cloud provider should make one request");
    }
    finally
    {
        Environment.SetEnvironmentVariable(environmentName, previous);
    }
});
Run("F44 cloud 429 maps to stable redacted error", () =>
{
    const string environmentName = "AWAKE_TEST_CLOUD_KEY_F44";
    const string secret = "test-cloud-secret-f44";
    var previous = Environment.GetEnvironmentVariable(environmentName);
    Environment.SetEnvironmentVariable(environmentName, secret);
    try
    {
        var handler = new RecordingHandler(_ => JsonResponse(429, "provider key should never appear in the thrown error"));
        var configuration = new ProviderConfiguration(
            "https://cloud.example.com/v1",
            "model-f44",
            environmentName,
            5,
            128,
            null,
            null);
        var provider = OpenAICompatibleCloudProvider.ForTesting(configuration, () => handler);
        try
        {
            provider.AnalyzeAsync(MakeAssistanceRequest("cloud-f44")).GetAwaiter().GetResult();
            throw new InvalidOperationException("expected cloud rate-limit failure");
        }
        catch (InvalidOperationException error)
        {
            Assert(error.Message.StartsWith("WB-AI-RATE-429", StringComparison.Ordinal), "429 error code mismatch");
            Assert(!error.Message.Contains(secret, StringComparison.Ordinal), "API key leaked in error message");
        }
    }
    finally
    {
        Environment.SetEnvironmentVariable(environmentName, previous);
    }
});
Run("F45 local worker valid handshake permits analysis", () =>
{
    const string environmentName = "AWAKE_TEST_WORKER_SECRET_F45";
    const string secret = "test-worker-secret-f45";
    var previous = Environment.GetEnvironmentVariable(environmentName);
    Environment.SetEnvironmentVariable(environmentName, secret);
    try
    {
        var request = MakeAssistanceRequest("worker-f45");
        var handler = new RecordingHandler(httpRequest =>
        {
            if (httpRequest.RequestUri?.AbsolutePath == "/awake/handshake")
            {
                var payload = ReadJson(httpRequest);
                var clientNonce = payload["client_nonce"]!.GetValue<string>();
                var timestamp = payload["timestamp"]!.GetValue<long>();
                var workerId = "worker.test-f45";
                var signature = WorkerHandshake.CreateSignature(secret, WorkerHandshake.Protocol, clientNonce, workerId, timestamp, request.RequestHash);
                return JsonResponse(200, new JsonObject
                {
                    ["protocol"] = WorkerHandshake.Protocol,
                    ["client_nonce"] = clientNonce,
                    ["worker_id"] = workerId,
                    ["timestamp"] = timestamp,
                    ["signature"] = signature
                }.ToJsonString());
            }

            Assert(httpRequest.RequestUri?.AbsolutePath == "/awake/analyze", "worker analysis endpoint mismatch");
            return JsonResponse(200, AssistanceJson(request.RequestHash, request.SourceDocumentHash));
        });
        var configuration = new ProviderConfiguration(
            null,
            null,
            null,
            5,
            128,
            "http://127.0.0.1:5444",
            environmentName);
        var provider = LocalWorkerProvider.ForTesting(configuration, () => handler);
        var result = provider.AnalyzeAsync(request).GetAwaiter().GetResult();
        Assert(result.RequestHash == request.RequestHash, "worker result request hash mismatch");
        Assert(handler.RequestCount == 2, "worker provider must handshake before analysis");
    }
    finally
    {
        Environment.SetEnvironmentVariable(environmentName, previous);
    }
});
Run("F46 local worker forged signature is rejected", () =>
{
    const string environmentName = "AWAKE_TEST_WORKER_SECRET_F46";
    const string secret = "test-worker-secret-f46";
    var previous = Environment.GetEnvironmentVariable(environmentName);
    Environment.SetEnvironmentVariable(environmentName, secret);
    try
    {
        var request = MakeAssistanceRequest("worker-f46");
        var handler = new RecordingHandler(httpRequest =>
        {
            Assert(httpRequest.RequestUri?.AbsolutePath == "/awake/handshake", "forged worker should fail during handshake");
            var payload = ReadJson(httpRequest);
            return JsonResponse(200, new JsonObject
            {
                ["protocol"] = WorkerHandshake.Protocol,
                ["client_nonce"] = payload["client_nonce"]!.GetValue<string>(),
                ["worker_id"] = "worker.forged-f46",
                ["timestamp"] = payload["timestamp"]!.GetValue<long>(),
                ["signature"] = "invalid-signature"
            }.ToJsonString());
        });
        var configuration = new ProviderConfiguration(
            null,
            null,
            null,
            5,
            128,
            "http://127.0.0.1:5445",
            environmentName);
        var provider = LocalWorkerProvider.ForTesting(configuration, () => handler);
        try
        {
            provider.AnalyzeAsync(request).GetAwaiter().GetResult();
            throw new InvalidOperationException("expected forged worker rejection");
        }
        catch (InvalidOperationException error)
        {
            Assert(error.Message.StartsWith("WB-AI-WORKER-HANDSHAKE-403", StringComparison.Ordinal), "forged worker error code mismatch");
            Assert(handler.RequestCount == 1, "forged worker must not receive analysis request");
        }
    }
    finally
    {
        Environment.SetEnvironmentVariable(environmentName, previous);
    }
});
Run("F47 suggestion store deduplicates and stays isolated", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var request = MakeAssistanceRequest("store-f47");
    var result = MakeAssistanceResult(request, "suggestion.f47");
    var store = new SuggestionStore(fixture.Workspace);
    var first = store.Save(request, result, "web-session-f47", "buffer-f47");
    var second = store.Save(request, result, "web-session-f47", "buffer-f47");
    Assert(first.Count == 1 && second.Count == 1, "suggestion store should save one suggestion");
    Assert(first[0].SuggestionHash == second[0].SuggestionHash, "duplicate request should return the existing suggestion");
    Assert(Directory.EnumerateFiles(Path.Combine(fixture.Root, "authoring", "suggestions"), "*.json").Count() == 1, "duplicate request created multiple files");
    Assert(fixture.Workspace.LoadAuthoringDocuments().Count == 1, "suggestion file leaked into authoring document enumeration");
    var suggestionPath = Directory.EnumerateFiles(Path.Combine(fixture.Root, "authoring", "suggestions"), "*.json").Single();
    var persisted = File.ReadAllText(suggestionPath);
    Assert(!persisted.Contains("web-session-f47", StringComparison.Ordinal), "raw session binding was persisted");
    Assert(!persisted.Contains(request.DocumentPath, StringComparison.Ordinal), "request path should not be persisted in suggestion envelope");
});
Run("F48 suggestion read requires document and session binding", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var request = MakeAssistanceRequest("store-f48");
    var store = new SuggestionStore(fixture.Workspace);
    var saved = store.Save(request, MakeAssistanceResult(request, "suggestion.f48"), "session-f48", "buffer-f48")[0];
    var found = store.Read(saved.SuggestionId, request.DocumentId, request.SourceDocumentHash, request.SavedRevision, "session-f48", "buffer-f48");
    Assert(found.SuggestionHash == saved.SuggestionHash, "stored suggestion could not be read");
    AssertThrowsCode(() => store.Read(saved.SuggestionId, request.DocumentId, request.SourceDocumentHash, request.SavedRevision, "wrong-session", "buffer-f48"), "WB-AI-SUGGESTION-404");
});
Run("F49 document CAS applies patch and consumes nonce once", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var request = MakeAssistanceRequest("cas-f49");
    var store = new SuggestionStore(fixture.Workspace);
    var saved = store.Save(request, MakeAssistanceResult(request, "suggestion.f49"), "session-f49", "buffer-f49")[0];
    var document = new JsonObject
    {
        ["id"] = request.DocumentId,
        ["title"] = new JsonObject { ["zh-CN"] = "旧标题" }
    };
    var buffer = DocumentCas.Open(request.DocumentPath, request.DocumentId, request.SourceDocumentHash, request.SavedRevision, "buffer-f49", document);
    var updated = DocumentCas.Apply(buffer, saved, saved.ApplyNonce!);
    Assert(updated.Revision == 2, "CAS revision did not advance");
    Assert(updated.Document["title"]?["zh-CN"]?.GetValue<string>() == "AI 建议标题", "CAS patch was not applied");
    store.ConsumeApplyNonce(saved.SuggestionId, request.DocumentId, request.SourceDocumentHash, request.SavedRevision, "session-f49", "buffer-f49", saved.ApplyNonce!);
    AssertThrowsCode(() => store.ConsumeApplyNonce(saved.SuggestionId, request.DocumentId, request.SourceDocumentHash, request.SavedRevision, "session-f49", "buffer-f49", saved.ApplyNonce!), "WB-AI-SUGGESTION-404");
});
Run("F50 document CAS rejects stale source and revision", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var request = MakeAssistanceRequest("cas-f50");
    var store = new SuggestionStore(fixture.Workspace);
    var saved = store.Save(request, MakeAssistanceResult(request, "suggestion.f50"), "session-f50", "buffer-f50")[0];
    var staleDocument = new JsonObject { ["id"] = request.DocumentId, ["title"] = new JsonObject { ["zh-CN"] = "旧标题" } };
    var staleBuffer = DocumentCas.Open(request.DocumentPath, request.DocumentId, new string('c', 64), request.SavedRevision, "buffer-f50", staleDocument);
    AssertThrowsCode(() => DocumentCas.Apply(staleBuffer, saved, saved.ApplyNonce!), "WB-AI-CAS-409");
    var staleRevision = DocumentCas.Open(request.DocumentPath, request.DocumentId, request.SourceDocumentHash, request.SavedRevision + 1, "buffer-f50", staleDocument);
    AssertThrowsCode(() => DocumentCas.Apply(staleRevision, saved, saved.ApplyNonce!), "WB-AI-CAS-409");
});
Run("F51 CAS increments document revision", () =>
{
    using var fixture = MakeWorkspace(schemaRoot);
    var request = MakeAssistanceRequest("cas-f51");
    var store = new SuggestionStore(fixture.Workspace);
    var saved = store.Save(request, MakeAssistanceResult(request, "suggestion.f51"), "session-f51", "buffer-f51")[0];
    var document = new JsonObject
    {
        ["id"] = request.DocumentId,
        ["revision"] = 7,
        ["title"] = new JsonObject { ["zh-CN"] = "旧标题" }
    };
    var buffer = DocumentCas.Open(request.DocumentPath, request.DocumentId, request.SourceDocumentHash, request.SavedRevision, "buffer-f51", document);
    var updated = DocumentCas.Apply(buffer, saved, saved.ApplyNonce!);
    Assert(updated.Revision == 2, "CAS buffer revision did not advance from saved revision");
    Assert(updated.Document["revision"]?.GetValue<int>() == 2, "CAS did not write the next revision into the document");
});
Run("F52 malformed provider content returns stable format error", () =>
{
    var raw = JsonNode.Parse("{\"choices\":[{\"message\":{\"content\":42}}]}")!;
    AssertThrowsCode(() => OpenAICompatibleResponseParser.ExtractJson(raw), "WB-AI-FORMAT-JSON");
});
Run("F53 read-only AI provider status accepts browser GET without Origin", () =>
{
    var store = new WebAiSessionStore();
    var bootstrap = new DefaultHttpContext();
    bootstrap.Request.Headers.Origin = WebAiSessionStore.ExactOrigin;
    var session = store.Bootstrap(bootstrap);

    var read = new DefaultHttpContext();
    read.Request.Headers.Cookie = $"{WebAiSessionStore.SessionCookieName}={session.SessionId}";
    read.Request.Headers[WebAiSessionStore.CsrfHeaderName] = session.CsrfToken;
    Assert(store.RequireReadSession(read) == session.SessionId, "read-only session should accept browser GET without Origin");

    var mutation = new DefaultHttpContext();
    mutation.Request.Headers.Cookie = $"{WebAiSessionStore.SessionCookieName}={session.SessionId}";
    mutation.Request.Headers[WebAiSessionStore.CsrfHeaderName] = session.CsrfToken;
    AssertThrowsCode(() => store.RequireSession(mutation), "WB-AI-CSRF-403");
});
Run("F63 AI focus maps and serializes", () =>
{
    Assert(AssistanceFocusMapping.ToAnalysis(AssistanceFocusKind.NpcVoice) == AssistanceAnalysisKind.Prose, "NPC voice focus should use prose analysis");
    Assert(AssistanceFocusMapping.ToAnalysis(AssistanceFocusKind.Hierarchy) == AssistanceAnalysisKind.Permissions, "hierarchy focus should use permissions analysis");
    var request = MakeAssistanceRequest("focus-f63") with { Focus = AssistanceFocusKind.FactCorrection };
    var wire = AssistanceRequestSerializer.ToWire(request);
    Assert(wire["focus"]?.GetValue<string>() == "fact_correction", "AI focus was not serialized");
});
Run("F64 local provider settings use DPAPI without plaintext key", () =>
{
    var path = Path.Combine(Path.GetTempPath(), "awake-provider-settings-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        ProviderSettingsStore.Save("https://api.example.com", "model-test", "secret-key-f64", path);
        var raw = File.ReadAllText(path);
        Assert(!raw.Contains("secret-key-f64", StringComparison.Ordinal), "provider settings leaked the API key");
        Assert(ProviderSettingsStore.TryLoad(out var loaded, path), "provider settings did not load");
        Assert(loaded.BaseUrl == "https://api.example.com/" && loaded.Model == "model-test" && loaded.ApiKey == "secret-key-f64", "provider settings roundtrip failed");
    }
    finally { ProviderSettingsStore.Delete(path); }
});
Run("F65 local provider settings override environment configuration", () =>
{
    var path = Path.Combine(Path.GetTempPath(), "awake-provider-settings-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        ProviderSettingsStore.Save("https://stored.example.com", "stored-model", "stored-key-f65", path);
        var environment = new Dictionary<string, string?>
        {
            ["WORLD_BOOK_CLOUD_BASE_URL"] = "https://env.example.com",
            ["WORLD_BOOK_CLOUD_MODEL"] = "env-model",
            ["WORLD_BOOK_CLOUD_API_KEY_ENV"] = "MISSING_ENV_KEY",
            ["AWAKE_WB_PROVIDER_SETTINGS_PATH"] = path
        };
        var configuration = ProviderConfiguration.FromEnvironment(environment);
        Assert(configuration.HasLocalCloudSettings, "local provider settings source was not detected");
        Assert(configuration.CloudBaseUrl == "https://stored.example.com/" && configuration.CloudModel == "stored-model", "local settings did not override environment");
        Assert(configuration.ResolveCloudApiKey() == "stored-key-f65", "local API key was not selected");
    }
    finally { ProviderSettingsStore.Delete(path); }
});
Run("F66 deleting local provider settings falls back to environment", () =>
{
    var path = Path.Combine(Path.GetTempPath(), "awake-provider-settings-" + Guid.NewGuid().ToString("N") + ".json");
    var environment = new Dictionary<string, string?>
    {
        ["WORLD_BOOK_CLOUD_BASE_URL"] = "https://env.example.com",
        ["WORLD_BOOK_CLOUD_MODEL"] = "env-model",
        ["WORLD_BOOK_CLOUD_API_KEY_ENV"] = "AWAKE_TEST_PROVIDER_KEY",
        ["AWAKE_WB_PROVIDER_SETTINGS_PATH"] = path
    };
    var previous = Environment.GetEnvironmentVariable("AWAKE_TEST_PROVIDER_KEY");
    try
    {
        Environment.SetEnvironmentVariable("AWAKE_TEST_PROVIDER_KEY", "env-key-f66");
        ProviderSettingsStore.Save("https://stored.example.com", "stored-model", "stored-key-f66", path);
        ProviderSettingsStore.Delete(path);
        var configuration = ProviderConfiguration.FromEnvironment(environment);
        Assert(!configuration.HasLocalCloudSettings && configuration.ResolveCloudApiKey() == "env-key-f66", "environment fallback did not work after local delete");
    }
    finally
    {
        Environment.SetEnvironmentVariable("AWAKE_TEST_PROVIDER_KEY", previous);
        ProviderSettingsStore.Delete(path);
    }
});
Run("X1 local worker settings stay encrypted and survive a cloud save", () =>
{
    var path = Path.Combine(Path.GetTempPath(), "awake-provider-settings-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        ProviderSettingsStore.SaveLocalWorker("http://127.0.0.1:11434", "worker-secret-x1", path);
        var raw = File.ReadAllText(path);
        Assert(!raw.Contains("worker-secret-x1", StringComparison.Ordinal), "local worker settings leaked the secret");
        Assert(ProviderSettingsStore.TryLoadLocalWorker(out var url, out var secret, path), "local worker settings did not load");
        Assert(url == "http://127.0.0.1:11434/" && secret == "worker-secret-x1", "local worker settings roundtrip failed");

        ProviderSettingsStore.Save("https://api.example.com", "model-x1", "cloud-key-x1", path);
        Assert(ProviderSettingsStore.TryLoadLocalWorker(out _, out var keptSecret, path) && keptSecret == "worker-secret-x1", "saving cloud settings must not wipe the local worker settings");
        Assert(ProviderSettingsStore.TryLoad(out var cloud, path) && cloud.ApiKey == "cloud-key-x1", "cloud settings must survive a local worker save");

        ProviderSettingsStore.ClearLocalWorker(path);
        Assert(!ProviderSettingsStore.TryLoadLocalWorker(out _, out _, path), "clearing local worker settings must remove them");
        Assert(ProviderSettingsStore.TryLoad(out var keptCloud, path) && keptCloud.ApiKey == "cloud-key-x1", "clearing local worker settings must keep the cloud settings");
    }
    finally { ProviderSettingsStore.Delete(path); }
});
Run("X1 local worker settings override environment and reject non-loopback hosts", () =>
{
    var path = Path.Combine(Path.GetTempPath(), "awake-provider-settings-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        ProviderSettingsStore.SaveLocalWorker("http://127.0.0.1:11434", "worker-secret-x1b", path);
        var environment = new Dictionary<string, string?>
        {
            ["WORLD_BOOK_LOCAL_WORKER_URL"] = "http://127.0.0.1:9999",
            ["WORLD_BOOK_LOCAL_WORKER_SECRET_ENV"] = "AWAKE_TEST_WORKER_SECRET",
            ["AWAKE_WB_PROVIDER_SETTINGS_PATH"] = path
        };
        var configuration = ProviderConfiguration.FromEnvironment(environment);
        Assert(configuration.HasLocalWorkerSettings, "stored local worker settings were not detected");
        Assert(configuration.LocalWorkerUrl == "http://127.0.0.1:11434/", "stored local worker url must override the environment");
        Assert(configuration.ResolveLocalWorkerSecret() == "worker-secret-x1b", "stored local worker secret was not selected");
        Assert(configuration.GetStatus("local").State == "configured", "stored local worker settings must count as configured");
        AssertThrowsCode(() => ProviderSettingsStore.SaveLocalWorker("http://10.0.0.5:11434", "worker-secret-x1b", path), "WB-AI-ENDPOINT-403");
    }
    finally { ProviderSettingsStore.Delete(path); }
});
Run("F67 provider settings UI exposes safe Chinese entry and CSRF path", () =>
{
    var root = FindStudioRoot(AppContext.BaseDirectory);
    var index = File.ReadAllText(Path.Combine(root, "src", "Awake.WorldbookStudio.Web", "wwwroot", "index.html"));
    var script = File.ReadAllText(Path.Combine(root, "src", "Awake.WorldbookStudio.Web", "wwwroot", "studio-ai.js"));
    Assert(index.Contains("providerSettingsButton", StringComparison.Ordinal) && index.Contains("providerApiKey", StringComparison.Ordinal), "provider settings dialog is missing");
    Assert(script.Contains("/api/ai/provider-settings", StringComparison.Ordinal) && script.Contains("X-AWAKE-CSRF", StringComparison.Ordinal), "provider settings or CSRF integration is missing");
    Assert(script.Contains("世界观文风", StringComparison.Ordinal) && script.Contains("NPC 语气", StringComparison.Ordinal), "AI language/style assistance is missing");
    Assert(script.Contains("focusAnalysis", StringComparison.Ordinal) && script.Contains("analysisDefaultFocus", StringComparison.Ordinal), "AI analysis and focus controls are not synchronized");
});
Run("X1 provider settings dialog exposes the local worker entry", () =>
{
    var root = FindStudioRoot(AppContext.BaseDirectory);
    var index = File.ReadAllText(Path.Combine(root, "src", "Awake.WorldbookStudio.Web", "wwwroot", "index.html"));
    var script = File.ReadAllText(Path.Combine(root, "src", "Awake.WorldbookStudio.Web", "wwwroot", "studio-ai.js"));
    Assert(index.Contains("localWorkerUrl", StringComparison.Ordinal) && index.Contains("localWorkerSecret", StringComparison.Ordinal), "local worker entry fields are missing from the settings dialog");
    Assert(index.Contains("clearLocalWorkerButton", StringComparison.Ordinal), "the local worker settings cannot be cleared from the dialog");
    Assert(script.Contains("localWorkerUrl", StringComparison.Ordinal) && script.Contains("clearSavedLocalWorkerConfiguration", StringComparison.Ordinal), "the local worker fields are not wired to the settings endpoint");
    Assert(script.Contains("本机 AI Worker 未配置", StringComparison.Ordinal), "a missing local worker must be reported as a blocking, actionable state");
});
Run("F68 provider status requires actual environment secrets", () =>
{
    var missing = ProviderConfiguration.FromEnvironment(new Dictionary<string, string?>
    {
        ["WORLD_BOOK_CLOUD_BASE_URL"] = "https://cloud.example.com/v1",
        ["WORLD_BOOK_CLOUD_MODEL"] = "model-f68",
        ["WORLD_BOOK_CLOUD_API_KEY_ENV"] = "MISSING_CLOUD_KEY_F68",
        ["WORLD_BOOK_LOCAL_WORKER_URL"] = "http://127.0.0.1:5078",
        ["WORLD_BOOK_LOCAL_WORKER_SECRET_ENV"] = "MISSING_WORKER_SECRET_F68"
    });
    Assert(missing.GetStatus("cloud").State == "missing", "cloud status must require an actual API key");
    Assert(missing.GetStatus("local").State == "missing", "local status must require an actual Worker secret");

    var configured = ProviderConfiguration.FromEnvironment(new Dictionary<string, string?>
    {
        ["WORLD_BOOK_CLOUD_BASE_URL"] = "https://cloud.example.com/v1",
        ["WORLD_BOOK_CLOUD_MODEL"] = "model-f68",
        ["WORLD_BOOK_CLOUD_API_KEY_ENV"] = "CLOUD_KEY_F68",
        ["CLOUD_KEY_F68"] = "cloud-secret-f68",
        ["WORLD_BOOK_LOCAL_WORKER_URL"] = "http://127.0.0.1:5078",
        ["WORLD_BOOK_LOCAL_WORKER_SECRET_ENV"] = "WORKER_SECRET_F68",
        ["WORKER_SECRET_F68"] = "worker-secret-f68"
    });
    Assert(configured.GetStatus("cloud").State == "configured", "cloud status should accept a present API key");
    Assert(configured.GetStatus("local").State == "configured", "local status should accept a present Worker secret");
});
Run("F69 result parser enforces review-only suggestions", () =>
{
    var baseSuggestion = new JsonObject
    {
        ["id"] = "suggestion-f69",
        ["kind"] = "prose",
        ["severity"] = "info",
        ["confidence"] = 0.5,
        ["title"] = "测试建议",
        ["reason"] = "仅用于测试。",
        ["review_only"] = true
    };
    var missing = new JsonObject
    {
        ["schema_version"] = "assistance.result.v1",
        ["request_hash"] = new string('a', 64),
        ["source_document_hash"] = new string('b', 64),
        ["suggestions"] = new JsonArray(JsonNode.Parse(baseSuggestion.ToJsonString())!)
    };
    missing["suggestions"]![0]!.AsObject().Remove("review_only");
    AssertThrowsCode(() => AssistanceResultParser.Parse(missing), "WB-AI-FORMAT-JSON");
    baseSuggestion["review_only"] = false;
    var falseValue = new JsonObject
    {
        ["schema_version"] = "assistance.result.v1",
        ["request_hash"] = new string('a', 64),
        ["source_document_hash"] = new string('b', 64),
        ["suggestions"] = new JsonArray(JsonNode.Parse(baseSuggestion.ToJsonString())!)
    };
    AssertThrowsCode(() => AssistanceResultParser.Parse(falseValue), "WB-AI-FORMAT-JSON");
});
Run("F70 serialized request matches strict schema", () =>
{
    var request = MakeAssistanceRequest("schema-f70") with { ProviderId = "cloud", Focus = AssistanceFocusKind.DomainStyle };
    var report = new ValidationReport();
    new SchemaValidator().Validate(AssistanceRequestSerializer.ToWire(request), Path.Combine(schemaRoot, "assistance.request.v1.schema.json"), report);
    Assert(report.Valid, string.Join("; ", report.Diagnostics.Select(item => $"{item.Path}:{item.Detail}")));
});
Run("A3.2 content graph matches pre-extraction golden", () =>
{
    var golden = LoadA32Golden();
    AssertA32BuilderSeam();
    foreach (var caseName in new[] { "baseline", "branch" })
    {
        using var fixture = MakeA32Workspace(schemaRoot, caseName);
        var result = fixture.Service.Compile();
        AssertA32CompileCase(result, golden["cases"]!.AsObject()[caseName]!.AsObject(), caseName);
    }
});
Run("A3.2 content graph tier closure and diagnostics", () =>
{
    var golden = LoadA32Golden();
    using (var adult = MakeA32Workspace(schemaRoot, "adult_optional"))
    {
        var expected = golden["cases"]!.AsObject()["adult_optional"]!.AsObject();
        AssertA32Diagnostics(adult.Service.Validate(), expected["validation_diagnostics"]!.AsArray(), adult.Root);
        var token = adult.Service.GenerateConfirmationToken("adult_optional");
        var compiled = adult.Service.Compile("adult_optional", token);
        AssertA32CompileCase(compiled, expected, "adult_optional");
        var baseResult = adult.Service.Compile("base");
        var tierDiagnostics = baseResult.Validation.Diagnostics.Where(x => x.Code == "WB-TIER-001").ToArray();
        Assert(tierDiagnostics.Length == 1 && tierDiagnostics[0].Path == "content-graph", "adult_optional base gate changed");
        Assert(!baseResult.Validation.Valid && baseResult.Files.Count == 0, "base compile must reject adult closure");
    }
    foreach (var caseName in new[] { "document_unknown", "expression_unknown" })
    {
        using var fixture = MakeA32Workspace(schemaRoot, caseName);
        var expected = golden["cases"]!.AsObject()[caseName]!.AsObject();
        AssertA32Diagnostics(fixture.Service.Validate(), expected["validation_diagnostics"]!.AsArray(), fixture.Root);
        var result = fixture.Service.Compile();
        AssertA32SnapshotGraph(result, expected, caseName);
        AssertA32Diagnostics(result.Validation, expected["compile_diagnostics"]!.AsArray(), fixture.Root);
        Assert(!result.Validation.Valid && result.Files.Count == 0, caseName + " must remain snapshot-only");
    }
});
Run("A3.2 content graph compile wiring and determinism", () =>
{
    var golden = LoadA32Golden();
    foreach (var caseName in new[] { "baseline", "branch" })
    {
        using var fixture = MakeA32Workspace(schemaRoot, caseName);
        var first = fixture.Service.Compile();
        var second = fixture.Service.Compile();
        AssertA32CompileCase(first, golden["cases"]!.AsObject()[caseName]!.AsObject(), caseName);
        Assert(first.ManifestHash == second.ManifestHash, caseName + " manifest hash changed between compiles");
        Assert(first.Files.Keys.OrderBy(x => x).SequenceEqual(second.Files.Keys.OrderBy(x => x)), caseName + " file set changed between compiles");
        foreach (var name in first.Files.Keys)
            Assert(first.Files[name].SequenceEqual(second.Files[name]), caseName + " output bytes changed for " + name);
        AssertA32SnapshotGraphMatchesOutput(first, fixture.Root, caseName);
    }
    using (var adult = MakeA32Workspace(schemaRoot, "adult_optional"))
    {
        var token = adult.Service.GenerateConfirmationToken("adult_optional");
        var first = adult.Service.Compile("adult_optional", token);
        var second = adult.Service.Compile("adult_optional", token);
        Assert(first.ManifestHash == second.ManifestHash, "adult_optional manifest hash changed between compiles");
        Assert(first.Files.Keys.OrderBy(x => x).SequenceEqual(second.Files.Keys.OrderBy(x => x)), "adult_optional file set changed between compiles");
        foreach (var name in first.Files.Keys)
            Assert(first.Files[name].SequenceEqual(second.Files[name]), "adult_optional output bytes changed for " + name);
        AssertA32SnapshotGraphMatchesOutput(first, adult.Root, "adult_optional");
    }
});
Run("A3.3 preview golden", () =>
{
    var golden = LoadA33Golden();
    AssertA33BuilderSeam();
    AssertA33RunManifest();
    foreach (var caseName in A33CaseNames())
    {
        using var fixture = MakeA33Workspace(schemaRoot, caseName);
        var scenario = A33Scenario(caseName);
        var expected = golden["cases"]!.AsObject()[caseName]!.AsObject();
        var snapshotReport = fixture.Service.Validate();
        var result = fixture.Service.Preview(scenario.ProfileId, $"a33.{caseName}", scenario.Identity);
        AssertA33GoldenCase(fixture, result, snapshotReport, expected, caseName);
    }
});
Run("A3.3 permission matrix", () =>
{
    foreach (var caseName in A33CaseNames())
    {
        using var fixture = MakeA33Workspace(schemaRoot, caseName);
        var scenario = A33Scenario(caseName);
        var result = fixture.Service.Preview(scenario.ProfileId, $"a33.{caseName}", scenario.Identity);
        AssertA33PermissionCase(result, caseName);
    }
});
Run("A3.3 preview wiring and determinism", () =>
{
    var probe = new WorkspaceReadProbe();
    using var fixture = MakeA33Workspace(schemaRoot, "baseline_commoner", probe);
    var scenario = A33Scenario("baseline_commoner");
    var first = fixture.Service.Preview(scenario.ProfileId, "a33.baseline_commoner", scenario.Identity);
    var second = fixture.Service.Preview(scenario.ProfileId, "a33.baseline_commoner", scenario.Identity);
    Assert(first.Envelope.ToJsonString() == second.Envelope.ToJsonString(), "preview envelope raw bytes changed between identical calls");
    Assert(CanonicalJson.Hash(first.Envelope) == CanonicalJson.Hash(second.Envelope), "preview envelope canonical hash changed between identical calls");
    Assert(JsonSerializer.SerializeToNode(first.Items)!.ToJsonString() == JsonSerializer.SerializeToNode(second.Items)!.ToJsonString(), "preview items changed between identical calls");
    Assert(A33DiagnosticArray(first.Diagnostics).ToJsonString() == A33DiagnosticArray(second.Diagnostics).ToJsonString(), "preview diagnostics changed between identical calls");
    var expectedInputCount = fixture.Workspace.EnumerateSnapshotInputPaths().Count;
    Assert(probe.ReadInventory.Count == expectedInputCount && probe.ReadInventory.All(item => item.Count == 6 && item.InitialReads == 2 && item.AssemblyReads == 2 && item.OutputReads == 2), "two previews must preserve the A2 three-stage read boundary per operation");
});
Run("A3.4 registry snapshot golden", () =>
{
    AssertA34BuilderSeam();
    AssertA34RunManifest();
    var golden = LoadA34Golden();
    foreach (var caseName in A34CaseNames())
    {
        using var fixture = MakeA34Workspace(schemaRoot, caseName);
        var report = new ValidationReport();
        var snapshot = new RegistryService(fixture.Workspace, new SchemaValidator()).LoadAndValidate(report);
        AssertA34GoldenCase(snapshot, report, golden["cases"]!.AsObject()[caseName]!.AsObject(), caseName);
    }
});
Run("A3.4 registry diagnostics and empty boundary", () =>
{
    var golden = LoadA34Golden();
    using (var unknown = MakeA34Workspace(schemaRoot, "unknown_parent"))
    {
        var report = new ValidationReport();
        _ = new RegistryService(unknown.Workspace, new SchemaValidator()).LoadAndValidate(report);
        var diagnostic = report.Diagnostics.Single();
        Assert(diagnostic.Code == "WB-PROFILE-001" && diagnostic.Path == "profile.test_unknown" && diagnostic.Detail == "profile.missing", "unknown parent diagnostic changed");
    }
    using (var cycle = MakeA34Workspace(schemaRoot, "parent_cycle"))
    {
        var report = new ValidationReport();
        _ = new RegistryService(cycle.Workspace, new SchemaValidator()).LoadAndValidate(report);
        var sequence = report.Diagnostics.Select(item => $"{item.Code}|{item.Path}|{item.Detail}").ToArray();
        Assert(sequence.SequenceEqual(new[]
        {
            "WB-PROFILE-001|profile.cycle_a|profile.cycle_a -> profile.cycle_b",
            "WB-PROFILE-001|profile.cycle_b|profile.cycle_b -> profile.cycle_a"
        }), "parent cycle diagnostic order/detail changed");
    }
    using (var schemaInvalid = MakeA34Workspace(schemaRoot, "schema_invalid"))
    {
        var report = new ValidationReport();
        _ = new RegistryService(schemaInvalid.Workspace, new SchemaValidator()).LoadAndValidate(report);
        Assert(report.Diagnostics.Count == 5, "schema-invalid registry diagnostic count changed");
        Assert(report.Diagnostics.Take(4).All(item => item.Code == "WB-SCHEMA-001"), "schema diagnostics must remain before parent diagnostics");
        Assert(report.Diagnostics[^1].Code == "WB-PROFILE-001" && report.Diagnostics[^1].Path == "profile.schema_invalid", "schema-invalid parent diagnostic boundary changed");
    }
    using (var missing = MakeA34Workspace(schemaRoot, "missing_registry"))
    {
        var report = new ValidationReport();
        var snapshot = new RegistryService(missing.Workspace, new SchemaValidator()).LoadAndValidate(report);
        Assert(report.Diagnostics.Count == 1 && report.Diagnostics[0].Code == "WB-REGISTRY-000", "missing registry must produce only WB-REGISTRY-000");
        Assert(snapshot.ProfileVersion == "" && snapshot.ReferralVersion == "" && snapshot.Profiles.Count == 0 && snapshot.Referrals.Count == 0, "missing registry must return an empty snapshot");
        Assert(snapshot.ProfileHash == Hashing.Sha256Bytes(Array.Empty<byte>()) && snapshot.ReferralHash == Hashing.Sha256Bytes(Array.Empty<byte>()), "missing registry empty hashes changed");
        Assert(A34DiagnosticArray(report.Diagnostics).ToJsonString() != golden["cases"]!["baseline"]!["diagnostics"]!.ToJsonString(), "missing registry must not inherit baseline diagnostics");
    }
});
Run("A3.4 registry wiring and determinism", () =>
{
    var sourceRoot = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "src", "Awake.WorldbookStudio.Core");
    var validationSource = File.ReadAllText(Path.Combine(sourceRoot, "ValidationServices.cs"));
    Assert(CountOccurrences(validationSource, "RegistrySnapshotBuilder.Build(") == 1, "ValidationServices.cs must call the A3.4 builder exactly once");

    var probe = new WorkspaceReadProbe();
    using var fixture = MakeA34Workspace(schemaRoot, "baseline", probe);
    var service = new RegistryService(fixture.Workspace, new SchemaValidator());
    var firstReport = new ValidationReport();
    var first = service.LoadAndValidate(firstReport);
    var secondReport = new ValidationReport();
    var second = service.LoadAndValidate(secondReport);
    Assert(A34Projection(first).ToJsonString() == A34Projection(second).ToJsonString(), "registry projection changed between identical calls");
    Assert(A34DiagnosticArray(firstReport.Diagnostics).ToJsonString() == A34DiagnosticArray(secondReport.Diagnostics).ToJsonString(), "registry diagnostics changed between identical calls");
    Assert(first.ProfileHash == second.ProfileHash && first.ReferralHash == second.ReferralHash, "registry hashes changed between identical calls");

    var preview = fixture.Service.Preview("profile.commoner", "a34.baseline", new IdentitySnapshot("a34.baseline", "profile.commoner", null, null, null, null, null, null));
    Assert(preview.Envelope is not null, "full application registry path did not return a preview envelope");
    Assert(probe.ReadInventory.Count > 0 && probe.ReadInventory.All(item => item.Count == 3 && item.InitialReads == 1 && item.AssemblyReads == 1 && item.OutputReads == 1), "full application registry path must preserve the A2 three-stage read boundary");
});
Run("A4 semantic suggestion projection golden", () =>
{
    var golden = LoadA4Golden();
    var envelope = A4Envelope(golden["independent_envelope"]!.AsObject());
    var projected = SuggestionSemanticProjection.Project(envelope);
    var expected = golden["expected_projection"]!.AsObject();
    Assert(projected.Id == expected["id"]!.GetValue<string>(), "A4 projection id changed");
    Assert(projected.Kind == expected["kind"]!.GetValue<string>(), "A4 projection kind changed");
    Assert(projected.Severity == expected["severity"]!.GetValue<string>(), "A4 projection severity changed");
    Assert(Math.Abs(projected.Confidence - expected["confidence"]!.GetValue<double>()) < 0.0000001, "A4 projection confidence changed");
    Assert(projected.Title == expected["title"]!.GetValue<string>(), "A4 projection title changed");
    Assert(projected.Reason == expected["reason"]!.GetValue<string>(), "A4 projection reason changed");
    Assert(projected.CandidateText == expected["candidate_text"]!.GetValue<string>(), "A4 projection candidate text changed");
    Assert(projected.ReviewOnly == expected["review_only"]!.GetValue<bool>(), "A4 projection review_only changed");
    Assert(projected.ApplyNonce == expected["apply_nonce"]!.GetValue<string>(), "A4 projection apply nonce changed");
    Assert(projected.SuggestionHash == expected["suggestion_hash"]!.GetValue<string>(), "A4 projection suggestion hash changed");
    var expectedPatch = expected["patch"]!.AsObject();
    Assert(projected.Patch is not null && projected.Patch.SchemaVersion == expectedPatch["schema_version"]!.GetValue<string>(), "A4 projection patch schema changed");
    var expectedOperations = expectedPatch["operations"]!.AsArray();
    Assert(projected.Patch!.Operations.Count == expectedOperations.Count, "A4 projection patch operation count changed");
    for (var index = 0; index < projected.Patch.Operations.Count; index++)
    {
        var actual = projected.Patch.Operations[index];
        var wanted = expectedOperations[index]!.AsObject();
        Assert(actual.Op == wanted["op"]!.GetValue<string>(), $"A4 projection patch op changed at {index}");
        Assert(actual.Path == wanted["path"]!.GetValue<string>(), $"A4 projection patch path changed at {index}");
        Assert(actual.Value?.ToJsonString() == wanted["value"]!.ToJsonString(), $"A4 projection patch value changed at {index}");
    }
    var sourceRoot = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "src", "Awake.WorldbookStudio.Core");
    var projectionSource = File.ReadAllText(Path.Combine(sourceRoot, "SuggestionSemanticProjection.cs"));
    Assert(!projectionSource.Contains("File", StringComparison.Ordinal)
        && !projectionSource.Contains("Directory", StringComparison.Ordinal)
        && !projectionSource.Contains("HttpClient", StringComparison.Ordinal)
        && !projectionSource.Contains("Process", StringComparison.Ordinal)
        && !projectionSource.Contains("Environment", StringComparison.Ordinal)
        && !projectionSource.Contains("DateTime", StringComparison.Ordinal), "A4 projection gained an external or time dependency");
    Assert(!projectionSource.Contains("candidate_text", StringComparison.Ordinal)
        && !projectionSource.Contains("candidateText", StringComparison.Ordinal)
        && !projectionSource.Contains("apply_nonce", StringComparison.Ordinal)
        && !projectionSource.Contains("applyNonce", StringComparison.Ordinal), "A4 projection must not own wire casing");
    var projectionType = typeof(SuggestionSemanticProjection);
    Assert(!projectionType.IsPublic && projectionType.IsAbstract && projectionType.IsSealed, "A4 projection must remain an internal static type");
    var projectMethod = projectionType.GetMethod("Project", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    Assert(projectMethod is not null && projectMethod.IsStatic, "A4 projection method is missing or not static");
});
Run("A4 CLI/Web wire casing and ownership", () =>
{
    var golden = LoadA4Golden();
    var a1 = JsonNode.Parse(File.ReadAllText(Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "fixtures", "a1-wire-golden.v1.json")))!.AsObject();
    Assert(golden["cli_fields"]!.ToJsonString() == new JsonArray(golden["cli_fields"]!.AsArray().Select(item => item!.DeepClone()).ToArray()).ToJsonString(), "A4 CLI field fixture is not an array");
    Assert(golden["cli_fields"]!.ToJsonString() == a1["cli_fields"]!.ToJsonString(), "A1 CLI wire golden changed while capturing A4");
    Assert(golden["web_fields"]!.ToJsonString() == a1["web_fields"]!.ToJsonString(), "A1 Web wire golden changed while capturing A4");
    var studioRoot = FindStudioRoot(AppContext.BaseDirectory);
    var cliSourcePath = Path.Combine(studioRoot, "src", "Awake.WorldbookStudio.Cli", "Program.cs");
    var webSourcePath = Path.Combine(studioRoot, "src", "Awake.WorldbookStudio.Web", "Program.cs");
    var cliSource = File.ReadAllText(cliSourcePath);
    var webSource = File.ReadAllText(webSourcePath);
    Assert(CountOccurrences(cliSource, "SuggestionSemanticProjection.Project(") == 1, "CLI must call A4 projection exactly once");
    Assert(CountOccurrences(webSource, "SuggestionSemanticProjection.Project(") == 1, "Web must call A4 projection exactly once");
    AssertOrderedLiterals(cliSource, new[] { "id = semantic.Id", "kind = semantic.Kind", "severity = semantic.Severity", "confidence = semantic.Confidence", "title = semantic.Title", "reason = semantic.Reason", "candidate_text = semantic.CandidateText", "patch = semantic.Patch", "review_only = semantic.ReviewOnly", "apply_nonce = semantic.ApplyNonce", "suggestion_hash = semantic.SuggestionHash" }, "CLI semantic wire order");
    AssertOrderedLiterals(webSource, new[] { "id = semantic.Id", "kind = semantic.Kind", "severity = semantic.Severity", "confidence = semantic.Confidence", "title = semantic.Title", "reason = semantic.Reason", "candidateText = semantic.CandidateText", "patch = semantic.Patch", "reviewOnly = semantic.ReviewOnly", "applyNonce = semantic.ApplyNonce", "suggestionHash = semantic.SuggestionHash" }, "Web semantic wire order");
    Assert(!File.ReadAllText(Path.Combine(studioRoot, "src", "Awake.WorldbookStudio.Core", "SuggestionSemanticProjection.cs")).Contains("candidate_text", StringComparison.Ordinal), "Core projection must not become a wire DTO");
    var assemblyInfo = File.ReadAllText(Path.Combine(studioRoot, "src", "Awake.WorldbookStudio.Core", "Properties", "AssemblyInfo.cs"));
    foreach (var friend in golden["friend_assemblies"]!.AsArray().Select(item => item!.GetValue<string>()))
        Assert(assemblyInfo.Contains($"InternalsVisibleTo(\"{friend}\")", StringComparison.Ordinal), $"friend assembly missing: {friend}");
    var coreTypes = typeof(SuggestionEnvelope).Assembly.GetExportedTypes().Where(type => type.IsPublic && type.DeclaringType is null).Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
    Assert(coreTypes.SequenceEqual(golden["public_core_types"]!.AsArray().Select(item => item!.GetValue<string>())), "public Core type baseline changed");
    var commands = System.Text.RegularExpressions.Regex.Matches(cliSource, "case \\\"([^\\\"]+)\\\":").Select(match => match.Groups[1].Value).ToArray();
    Assert(commands.SequenceEqual(golden["cli_commands"]!.AsArray().Select(item => item!.GetValue<string>())), "CLI command baseline changed");
    var routes = System.Text.RegularExpressions.Regex.Matches(webSource, "app\\.Map(?<method>Get|Post|Put|Delete)\\(\\\"(?<path>[^\\\"]+)\\\"").Select(match => $"{match.Groups["method"].Value.ToUpperInvariant()} {match.Groups["path"].Value}").ToArray();
    Assert(routes.SequenceEqual(golden["web_routes"]!.AsArray().Select(item => item!.GetValue<string>())), "Web route baseline changed");
    AssertA4SchemaGolden(golden, studioRoot);
    AssertA4NamedCaseManifest(golden, studioRoot);
});
Run("A4 CLI/Web smoke coverage", () =>
{
    var studioRoot = FindStudioRoot(AppContext.BaseDirectory);
    var scriptPath = Path.Combine(studioRoot, "scripts", "a1-authority-smoke.ps1");
    var script = File.ReadAllText(scriptPath);
    Assert(script.Contains("pwsh -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\a1-authority-smoke.ps1 -Extended -EvidencePath <TEMP>", StringComparison.Ordinal), "A4 smoke invocation contract is not documented in the script");
    foreach (var caseId in new[] { "cli-invalid-provider", "cli-consent-success", "cli-analyze-success", "cli-apply-success", "cli-reject-success", "cli-wrong-nonce", "cli-wrong-cas", "web-session-bootstrap", "web-invalid-csrf", "web-invalid-session", "web-invalid-consent", "web-invalid-provider", "web-consent-success", "web-analyze-success", "web-apply-success", "web-reject-success", "web-wrong-nonce", "web-wrong-cas" })
        Assert(script.Contains(caseId, StringComparison.Ordinal), $"A4 smoke case is missing: {caseId}");
    var evidenceDirectory = Path.Combine(Path.GetTempPath(), "awake-worldbook-studio-a4-evidence", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(evidenceDirectory);
    var evidencePath = Path.Combine(evidenceDirectory, "a4-cli-web-smoke.json");
    try
    {
        var result = RunPowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-Extended", "-EvidencePath", evidencePath);
        Assert(result.ExitCode == 0, $"A4 extended smoke failed: {result.Stdout} {result.Stderr}");
        var archived = Path.GetFullPath(Path.Combine(studioRoot, "..", "..", "docs", "evidence", "WORLDBOOK-STUDIO-A4-CLI-WEB-20260823-smoke.json"));
        Assert(File.Exists(evidencePath) && File.Exists(archived), "A4 smoke evidence was not written to both paths");
        var evidence = JsonNode.Parse(File.ReadAllText(archived))!.AsObject();
        foreach (var key in new[] { "schema_version", "batch_id", "started_at_utc", "finished_at_utc", "working_directory", "command", "release_artifacts", "fake_worker_artifact_path", "fake_worker_artifact_sha256", "fake_worker_dll_sha256", "cases", "cleanup", "game_directory_touched", "real_provider", "real_worker" })
            Assert(evidence.ContainsKey(key), $"A4 smoke evidence key missing: {key}");
        Assert(evidence["game_directory_touched"]!.GetValue<bool>() == false && evidence["real_provider"]!.GetValue<bool>() == false && evidence["real_worker"]!.GetValue<bool>() == false, "A4 smoke evidence claimed an external boundary");
        Assert(evidence["fake_worker_dll_sha256"] is null, "A4 fake Worker DLL hash must remain null");
        var cases = evidence["cases"]!.AsArray().Select(item => item!.AsObject()).ToArray();
        Assert(cases.Length == 18 && cases.All(item => item["passed"]!.GetValue<bool>()), "A4 smoke case evidence is incomplete");
        Assert(evidence["cleanup"]!["web_process_stopped"]!.GetValue<bool>() && evidence["cleanup"]!["worker_process_stopped"]!.GetValue<bool>() && evidence["cleanup"]!["temp_workspace_removed"]!.GetValue<bool>(), "A4 smoke cleanup evidence is incomplete");
    }
    finally
    {
        try { if (Directory.Exists(evidenceDirectory)) Directory.Delete(evidenceDirectory, true); } catch { }
    }
});
Console.WriteLine($"PASS: Worldbook Studio harness ({passed}/{total})");

void Run(string name, Action action)
{
    total++;
    try { action(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { throw new InvalidOperationException($"FAIL {name}: {ex.Message}", ex); }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AssertCode(ValidationReport report, string code)
{
    Assert(report.Diagnostics.Any(x => x.Code == code), $"expected {code}; got {string.Join(',', report.Diagnostics.Select(x => x.Code))}");
}

static void AssertThrowsCode(Action action, string code)
{
    try { action(); throw new InvalidOperationException($"expected {code} but no exception was thrown"); }
    catch (InvalidOperationException ex) { Assert(ex.Message.StartsWith(code, StringComparison.Ordinal), $"expected {code}; got {ex.Message}"); }
}

static string[] A34CaseNames() => new[] { "baseline", "unknown_parent", "parent_cycle", "schema_invalid", "missing_registry" };

static JsonObject LoadA34Golden()
{
    var path = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "fixtures", "a3-4-registry-snapshot-golden.v1.json");
    return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
}

static JsonObject LoadA4Golden()
{
    var path = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "fixtures", "a4-cli-web-contract-golden.v1.json");
    return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
}

static SuggestionEnvelope A4Envelope(JsonObject root)
{
    var suggestionRoot = root["suggestion"]!.AsObject();
    var patchRoot = suggestionRoot["patch"]!.AsObject();
    var operations = patchRoot["operations"]!.AsArray().Select(item =>
    {
        var operation = item!.AsObject();
        return new KnowledgePatchOperation(
            operation["op"]!.GetValue<string>(),
            operation["path"]!.GetValue<string>(),
            operation["value"]?.DeepClone());
    }).ToArray();
    var patch = new KnowledgePatch(patchRoot["schema_version"]!.GetValue<string>(), operations);
    var suggestion = new AssistanceSuggestion(
        suggestionRoot["id"]!.GetValue<string>(),
        suggestionRoot["kind"]!.GetValue<string>(),
        suggestionRoot["severity"]!.GetValue<string>(),
        suggestionRoot["confidence"]!.GetValue<double>(),
        suggestionRoot["title"]!.GetValue<string>(),
        suggestionRoot["reason"]!.GetValue<string>(),
        suggestionRoot["candidate_text"]!.GetValue<string>(),
        patch,
        suggestionRoot["review_only"]!.GetValue<bool>());
    return new SuggestionEnvelope(
        root["schema_version"]!.GetValue<string>(),
        root["suggestion_id"]!.GetValue<string>(),
        root["document_id"]!.GetValue<string>(),
        root["provider_id"]!.GetValue<string>(),
        root["request_hash"]!.GetValue<string>(),
        root["request_nonce_hash"]!.GetValue<string>(),
        root["source_document_hash"]!.GetValue<string>(),
        root["saved_revision"]!.GetValue<int>(),
        root["session_binding_hash"]!.GetValue<string>(),
        root["buffer_id"]!.GetValue<string>(),
        root["suggestion_hash"]!.GetValue<string>(),
        root["apply_nonce"]!.GetValue<string>(),
        DateTimeOffset.Parse(root["created_at"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture),
        suggestion,
        root["applied"]!.GetValue<bool>());
}

static void AssertOrderedLiterals(string content, string[] expected, string label)
{
    var cursor = -1;
    foreach (var literal in expected)
    {
        var next = content.IndexOf(literal, cursor + 1, StringComparison.Ordinal);
        Assert(next > cursor, $"{label} missing or out of order: {literal}");
        cursor = next;
    }
}

static void AssertA4SchemaGolden(JsonObject golden, string studioRoot)
{
    var schemaRoot = Path.Combine(studioRoot, "..", "..", "docs", "worldbook-studio-plan");
    schemaRoot = Path.GetFullPath(schemaRoot);
    var expected = golden["schema_files"]!.AsArray().Select(item => item!.AsObject()).ToArray();
    var b1SchemaNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "entity-registry.v1.schema.json",
        "entity-registry-manifest.v1.schema.json",
        "entity-registry-diagnostics.v1.schema.json",
        "entity-registry-pointer.v1.schema.json"
    };
    var actual = Directory.EnumerateFiles(schemaRoot, "*.json", SearchOption.TopDirectoryOnly)
        .Where(path => !b1SchemaNames.Contains(Path.GetFileName(path)))
        .Select(path => new
    {
        Path = Path.GetFileName(path),
        Hash = Hashing.Sha256Bytes(File.ReadAllBytes(path)).ToLowerInvariant()
    }).OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
    Assert(actual.Length == expected.Length, $"A4 schema file count changed: {actual.Length}");
    for (var index = 0; index < actual.Length; index++)
    {
        Assert(actual[index].Path == expected[index]["path"]!.GetValue<string>(), $"A4 schema path changed at {index}");
        Assert(actual[index].Hash == expected[index]["sha256"]!.GetValue<string>(), $"A4 schema hash changed at {actual[index].Path}");
    }
}

static void AssertA4NamedCaseManifest(JsonObject golden, string studioRoot)
{
    var path = Path.Combine(studioRoot, "tests", "Awake.WorldbookStudio.Tests", "Program.cs");
    var lines = File.ReadAllLines(path).Where(line => line.StartsWith("Run(\"", StringComparison.Ordinal)).ToArray();
    var b1 = new[]
    {
        "Run(\"B1 entity catalog exposes base game and uninstalled DLC\", () =>",
        "Run(\"B1 entity catalog degrades without mapping package\", () =>"
    };
    var v2Package = new[]
    {
        "Run(\"F75 default compiled output commits inside compiled root\", () =>",
        "Run(\"F76 published manifest preserves runtime hash contract\", () =>",
        "Run(\"F77 published candidate passes AWAKE package integrity\", () =>",
        "Run(\"F78 published candidate loads and answers through AWAKE query\", () =>",
        "Run(\"F79 output rejects missing compile snapshot\", () =>",
        "Run(\"F80 output rechecks runtime content hash\", () =>",
        "Run(\"F81 successful publish releases lock file\", () =>"
    };
    var boundary = new[]
    {
        "Run(\"HTTP authority error matrix is explicit and safe\", () =>",
        "Run(\"HTTP authority errors distinguish mutation uncertainty\", () =>",
        "Run(\"HTTP unknown exceptions use safe 500 projection\", () =>",
        "Run(\"HTTP error response exposes matching correlation header and body\", () =>",
        "Run(\"HTTP response-started boundary does not append a second error body\", () =>"
    };
    var k1 = new[]
    {
        "Run(\"K1 runtime keywords carry titles document aliases and entity anchor names\", () =>",
        "Run(\"K1 runtime keywords degrade safely without the entity mapping package\", () =>",
        "Run(\"D1-2 ambiguous keywords are reported without blocking compilation\", () =>",
        "Run(\"compile settlement public projection tolerates null diagnostic fields\", () =>",
        "Run(\"X1 local worker settings stay encrypted and survive a cloud save\", () =>",
        "Run(\"X1 local worker settings override environment and reject non-loopback hosts\", () =>",
        "Run(\"X1 provider settings dialog exposes the local worker entry\", () =>"
    };
    lines = lines.Where(line => !b1.Contains(line, StringComparer.Ordinal) && !v2Package.Contains(line, StringComparer.Ordinal) && !boundary.Contains(line, StringComparer.Ordinal) && !k1.Contains(line, StringComparer.Ordinal)).ToArray();
    var manifest = golden["named_case_manifest"]!.AsObject();
    var added = manifest["added_lines"]!.AsArray().Select(item => item!.GetValue<string>()).ToArray();
    Assert(lines.Length == manifest["target_count"]!.GetValue<int>(), "A4 named-case count changed");
    Assert(lines[^added.Length..].SequenceEqual(added), "A4 named cases must be appended after A3.4");
    Assert(added.All(line => lines.Count(candidate => candidate == line) == 1), "A4 named case is missing or duplicated");
    var baseline = lines.Where(line => !added.Contains(line, StringComparer.Ordinal)).ToArray();
    Assert(baseline.Length == manifest["baseline_count"]!.GetValue<int>(), "A4 baseline named-case count changed");
    Assert(Hashing.Sha256Text(string.Join("\n", baseline)).Equals(manifest["baseline_sha256"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase), "A4 baseline named-case hash changed");
    Assert(Hashing.Sha256Text(string.Join("\n", lines)).Equals(manifest["target_sha256"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase), "A4 target named-case hash changed");
}

static ProcessResult RunPowerShell(params string[] arguments)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "pwsh",
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("A4 PowerShell process could not start");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    Task.WaitAll(stdout, stderr);
    return new ProcessResult(process.ExitCode, stdout.Result, stderr.Result);
}

static JsonObject A34Projection(RegistrySnapshot snapshot)
{
    var profilesInInputOrder = snapshot.ProfileRegistry["profiles"]?.AsArray().OfType<JsonObject>().Select(profile => profile["id"]?.GetValue<string>()).Where(id => id is not null).Select(id => id!).ToArray() ?? Array.Empty<string>();
    var referralsInInputOrder = snapshot.ReferralRegistry["referrals"]?.AsArray().OfType<JsonObject>().Select(referral => referral["id"]?.GetValue<string>()).Where(id => id is not null).Select(id => id!).ToArray() ?? Array.Empty<string>();
    return new JsonObject
    {
        ["profile_version"] = snapshot.ProfileVersion,
        ["referral_version"] = snapshot.ReferralVersion,
        ["profile_hash"] = snapshot.ProfileHash,
        ["referral_hash"] = snapshot.ReferralHash,
        ["profiles_in_input_order"] = new JsonArray(profilesInInputOrder.Select(id => JsonValue.Create(id)).ToArray()),
        ["referrals_in_input_order"] = new JsonArray(referralsInInputOrder.Select(id => JsonValue.Create(id)).ToArray()),
        ["profile_parents"] = new JsonObject(snapshot.ProfileParents.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new KeyValuePair<string, JsonNode?>(item.Key, item.Value))),
        ["publicly_askable_referrals"] = new JsonArray(snapshot.PubliclyAskableReferrals.OrderBy(id => id, StringComparer.Ordinal).Select(id => JsonValue.Create(id)).ToArray()),
        ["profile_object_hashes"] = new JsonObject(snapshot.ProfileObjects.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new KeyValuePair<string, JsonNode?>(item.Key, JsonValue.Create(CanonicalJson.Hash(item.Value))))),
    };
}

static JsonArray A34DiagnosticArray(IEnumerable<Diagnostic> diagnostics)
    => new(diagnostics.Select(item => new JsonObject
    {
        ["code"] = item.Code,
        ["severity"] = item.Severity,
        ["message"] = item.Message,
        ["path"] = NormalizeA34DiagnosticPath(item.Path),
        ["detail"] = item.Detail
    }).ToArray());

static JsonArray NormalizeA34GoldenDiagnostics(JsonArray diagnostics)
    => new(diagnostics.OfType<JsonObject>().Select(item => new JsonObject
    {
        ["code"] = item["code"]?.DeepClone(),
        ["severity"] = item["severity"]?.DeepClone(),
        ["message"] = item["message"]?.DeepClone(),
        ["path"] = NormalizeA34DiagnosticPath(item["path"]?.GetValue<string>()),
        ["detail"] = item["detail"]?.DeepClone()
    }).ToArray());

static string? NormalizeA34DiagnosticPath(string? path)
    => string.IsNullOrWhiteSpace(path) ? path : Path.IsPathRooted(path) ? "<ABSOLUTE_PATH>" : path;

static void AssertA34GoldenCase(RegistrySnapshot snapshot, ValidationReport report, JsonObject expected, string caseName)
{
    var projection = A34Projection(snapshot);
    Assert(projection.ToJsonString() == expected["projection"]!.ToJsonString(), caseName + " registry projection changed");
    var raw = projection.ToJsonString();
    var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
    Assert(Hashing.Sha256Bytes(bytes) == expected["raw_projection_utf8_sha256"]!.GetValue<string>(), caseName + " raw projection hash changed");
    Assert(Convert.ToBase64String(bytes) == expected["raw_projection_base64"]!.GetValue<string>(), caseName + " raw projection bytes changed");
    Assert(CanonicalJson.Hash(projection) == expected["canonical_hash"]!.GetValue<string>(), caseName + " canonical registry projection hash changed");
    Assert(A34DiagnosticArray(report.Diagnostics).ToJsonString() == NormalizeA34GoldenDiagnostics(expected["diagnostics"]!.AsArray()).ToJsonString(), caseName + " registry diagnostic sequence changed");
}

static void AssertA34BuilderSeam()
{
    var assembly = typeof(WorldbookApplicationService).Assembly;
    var builderType = assembly.GetType("Awake.WorldbookStudio.Core.RegistrySnapshotBuilder", throwOnError: false);
    Assert(builderType is not null && !builderType.IsPublic, "A3.4 builder must be an internal type");
    var buildMethods = builderType?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(method => method.Name == "Build").ToArray() ?? [];
    Assert(buildMethods.Length == 1 && buildMethods[0].IsStatic, "A3.4 builder must expose exactly one static Build seam");
    var sourceRoot = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "src", "Awake.WorldbookStudio.Core");
    var builderPath = Path.Combine(sourceRoot, "RegistrySnapshotBuilder.cs");
    Assert(File.Exists(builderPath), "A3.4 builder source file is missing");
    var builderSource = File.ReadAllText(builderPath);
    foreach (var forbidden in new[] { "WorkspaceService", "SchemaValidator", "File.", "Directory.", "FileInfo", "DirectoryInfo", "HttpClient", "HttpRequestMessage", "NetworkStream" })
        Assert(!builderSource.Contains(forbidden, StringComparison.Ordinal), "A3.4 builder contains forbidden dependency: " + forbidden);
    Assert(builderSource.Split("Build(", StringSplitOptions.None).Length - 1 == 1, "A3.4 builder must contain one Build method declaration");

    var validationSource = File.ReadAllText(Path.Combine(sourceRoot, "ValidationServices.cs"));
    Assert(CountOccurrences(validationSource, "RegistrySnapshotBuilder.Build(") == 1, "ValidationServices.cs must call the A3.4 builder exactly once");
    foreach (var forbidden in new[] { "new RegistrySnapshot", "Profiles.Add(", "Referrals.Add(", "PubliclyAskableReferrals.Add(", "ProfileParents[", "ProfileObjects[", "while (!string.IsNullOrWhiteSpace(parent))", "foreach (var profile in profiles", "foreach (var referral in referrals" })
        Assert(!validationSource.Contains(forbidden, StringComparison.Ordinal), "old registry projection authority remains in ValidationServices.cs: " + forbidden);
    foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.TopDirectoryOnly).Where(path => !path.EndsWith("RegistrySnapshotBuilder.cs", StringComparison.OrdinalIgnoreCase)))
    {
        var source = File.ReadAllText(file);
        Assert(!source.Contains("new RegistrySnapshot", StringComparison.Ordinal), "second RegistrySnapshot construction remains in " + Path.GetFileName(file));
    }
}

static void AssertA34RunManifest()
{
    var path = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "Awake.WorldbookStudio.Tests", "Program.cs");
    var lines = File.ReadAllLines(path).Where(line => line.StartsWith("Run(\"", StringComparison.Ordinal)).ToArray();
    var added = new[]
    {
        "Run(\"A3.4 registry snapshot golden\", () =>",
        "Run(\"A3.4 registry diagnostics and empty boundary\", () =>",
        "Run(\"A3.4 registry wiring and determinism\", () =>"
    };
    var a4 = new[]
    {
        "Run(\"A4 semantic suggestion projection golden\", () =>",
        "Run(\"A4 CLI/Web wire casing and ownership\", () =>",
        "Run(\"A4 CLI/Web smoke coverage\", () =>"
    };
    var b1 = new[]
    {
        "Run(\"B1 entity catalog exposes base game and uninstalled DLC\", () =>",
        "Run(\"B1 entity catalog degrades without mapping package\", () =>"
    };
    var v2Package = new[]
    {
        "Run(\"F75 default compiled output commits inside compiled root\", () =>",
        "Run(\"F76 published manifest preserves runtime hash contract\", () =>",
        "Run(\"F77 published candidate passes AWAKE package integrity\", () =>",
        "Run(\"F78 published candidate loads and answers through AWAKE query\", () =>",
        "Run(\"F79 output rejects missing compile snapshot\", () =>",
        "Run(\"F80 output rechecks runtime content hash\", () =>",
        "Run(\"F81 successful publish releases lock file\", () =>"
    };
    var boundary = new[]
    {
        "Run(\"HTTP authority error matrix is explicit and safe\", () =>",
        "Run(\"HTTP authority errors distinguish mutation uncertainty\", () =>",
        "Run(\"HTTP unknown exceptions use safe 500 projection\", () =>",
        "Run(\"HTTP error response exposes matching correlation header and body\", () =>",
        "Run(\"HTTP response-started boundary does not append a second error body\", () =>"
    };
    var k1 = new[]
    {
        "Run(\"K1 runtime keywords carry titles document aliases and entity anchor names\", () =>",
        "Run(\"K1 runtime keywords degrade safely without the entity mapping package\", () =>",
        "Run(\"D1-2 ambiguous keywords are reported without blocking compilation\", () =>",
        "Run(\"compile settlement public projection tolerates null diagnostic fields\", () =>",
        "Run(\"X1 local worker settings stay encrypted and survive a cloud save\", () =>",
        "Run(\"X1 local worker settings override environment and reject non-loopback hosts\", () =>",
        "Run(\"X1 provider settings dialog exposes the local worker entry\", () =>"
    };
    var withoutA4 = lines.Where(line => !a4.Contains(line, StringComparer.Ordinal) && !b1.Contains(line, StringComparer.Ordinal) && !v2Package.Contains(line, StringComparer.Ordinal) && !boundary.Contains(line, StringComparer.Ordinal) && !k1.Contains(line, StringComparer.Ordinal)).ToArray();
    Assert(withoutA4.Length == 96, "A3.4 harness named-case count must be 96, got " + withoutA4.Length);
    Assert(added.All(expected => withoutA4.Count(line => line == expected) == 1), "A3.4 named cases are missing or duplicated");
    var baseline = withoutA4.Where(line => !added.Contains(line, StringComparer.Ordinal)).ToArray();
    Assert(baseline.Length == 93, "A3.4 existing named-case count changed: " + baseline.Length);
    var baselineHash = Hashing.Sha256Text(string.Join("\n", baseline));
    Assert(string.Equals(baselineHash, "eb6e76fe216bddb066797e6bb77823387b72e14044face9bf865e352f889802c", StringComparison.OrdinalIgnoreCase), "A3.4 existing harness named-case manifest changed");
}

static RegistryWorkspaceFixture MakeA34Workspace(string schemaRoot, string caseName, WorkspaceReadProbe? readProbe = null)
{
    var copiedSchemaRoot = CopySchemaRoot(schemaRoot);
    try
    {
        ApplyA34CaseMutation(caseName, copiedSchemaRoot);
        return new RegistryWorkspaceFixture(copiedSchemaRoot, MakeWorkspace(copiedSchemaRoot, readProbe: readProbe));
    }
    catch
    {
        try { Directory.Delete(copiedSchemaRoot, true); } catch { }
        throw;
    }
}

static void ApplyA34CaseMutation(string caseName, string schemaRoot)
{
    var profilePath = Path.Combine(schemaRoot, "profile-registry.v1.json");
    var referralPath = Path.Combine(schemaRoot, "referral-registry.v1.json");
    if (caseName == "missing_registry")
    {
        File.Delete(referralPath);
        return;
    }
    var profiles = JsonNode.Parse(File.ReadAllText(profilePath))!.AsObject();
    var profileArray = profiles["profiles"]!.AsArray();
    if (caseName == "unknown_parent") profileArray.Add(new JsonObject { ["id"] = "profile.test_unknown", ["display"] = new JsonObject { ["zh-CN"] = "未知继承测试" }, ["inherits"] = "profile.missing" });
    if (caseName == "parent_cycle")
    {
        profileArray.Add(new JsonObject { ["id"] = "profile.cycle_a", ["display"] = new JsonObject { ["zh-CN"] = "循环甲" }, ["inherits"] = "profile.cycle_b" });
        profileArray.Add(new JsonObject { ["id"] = "profile.cycle_b", ["display"] = new JsonObject { ["zh-CN"] = "循环乙" }, ["inherits"] = "profile.cycle_a" });
    }
    if (caseName == "schema_invalid") profileArray.Add(new JsonObject { ["id"] = "profile.schema_invalid", ["inherits"] = "profile.missing" });
    File.WriteAllText(profilePath, profiles.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
}

static JsonObject LoadA32Golden()
{
    var path = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "fixtures", "a3-2-content-graph-golden.v1.json");
    return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
}

static void AssertA32BuilderSeam()
{
    var assembly = typeof(WorldbookApplicationService).Assembly;
    var builderType = assembly.GetType("Awake.WorldbookStudio.Core.ContentGraphBuilder", throwOnError: false);
    Assert(builderType is not null && !builderType.IsPublic, "A3.2 builder must be an internal type");
    var buildMethod = builderType!.GetMethod("Build", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    Assert(buildMethod is not null && buildMethod.IsStatic, "A3.2 builder Build seam is missing");
    var sourcePath = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "src", "Awake.WorldbookStudio.Core", "ContentGraphBuilder.cs");
    Assert(File.Exists(sourcePath), "A3.2 builder source file is missing");
    var source = File.ReadAllText(sourcePath);
    foreach (var forbidden in new[] { "WorkspaceService", "File.", "Directory.", "FileInfo", "DirectoryInfo", "HttpClient", "WebRequest" })
        Assert(!source.Contains(forbidden, StringComparison.Ordinal), "A3.2 builder contains forbidden dependency: " + forbidden);
}

static WorkspaceFixture MakeA32Workspace(string schemaRoot, string caseName)
    => MakeWorkspace(schemaRoot, mutate: text => BuildA32CaseText(text, caseName));

static string BuildA32CaseText(string baseText, string caseName)
    => caseName switch
    {
        "baseline" => baseText,
        "branch" => BuildA32Branch(baseText),
        "adult_optional" => baseText.Replace("content_tier: base", "content_tier: adult_optional", StringComparison.Ordinal),
        "document_unknown" => baseText.Replace("content_tier: base", "content_tier: unknown", StringComparison.Ordinal),
        "expression_unknown" => baseText.Replace("        layer: summary", "        layer: summary\n        content_tier: unknown", StringComparison.Ordinal),
        _ => throw new InvalidOperationException("unknown A3.2 case: " + caseName)
    };

static string BuildA32Branch(string baseText)
{
    var withRedirect = baseText.Replace(
        "authority:",
        "redirects:\n  - redirect_id: redirect.demo\n    from: doc.politics.demo\n    to: entity.kingdom.vlandia\n    reason: {zh-CN: 示例重定向}\n    event_id: event.demo\n    revision: 1\nauthority:",
        StringComparison.Ordinal);
    var withFallback = withRedirect.Replace(
        "        grants:",
        "        fallback_referral_ids:\n          - referral.notary_merchant\n        grants:",
        StringComparison.Ordinal);
    return withFallback + "\n      - id: expr.demo\n        revision: 1\n        layer: summary\n        text: {zh-CN: 示例表达}\n        sources:\n          - {source_id: source.demo, source_version: v1, source_content_hash: 0D53F08678D8101B0C7E5053B30224FC671C55051770BA18207435EAD7AD37BD, locator: demo:1, quote_hash: 0D53F08678D8101B0C7E5053B30224FC671C55051770BA18207435EAD7AD37BD, quote: 示例}\n        fallback_referral_ids:\n          - referral.notary_merchant\n        grants: [{profile_id: profile.commoner, scope: local, min_detail: summary}]\n        denies: []\n";
}

static void AssertA32CompileCase(CompileResult result, JsonObject expected, string caseName)
{
    Assert(result.Validation.Valid == expected["compile_valid"]!.GetValue<bool>(), caseName + " compile validity changed");
    AssertA32SnapshotGraph(result, expected, caseName);
    var expectedFiles = expected["compiled_file_names"]!.AsArray().Select(x => x!.GetValue<string>()).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    Assert(result.Files.Keys.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expectedFiles), caseName + " compiled file set changed");
    if (expected["content_graph_sha256"] is JsonValue expectedHash && expectedHash.TryGetValue<string>(out var normalizedHash))
    {
        Assert(result.Files.TryGetValue("content-graph.json", out var bytes), caseName + " content graph file missing");
        var outputGraph = NormalizeA32Graph(JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes!))!.AsObject(), A32WorkspaceRoot(result.Snapshot!));
        Assert(CanonicalJson.Hash(outputGraph).Equals(normalizedHash, StringComparison.OrdinalIgnoreCase), caseName + " normalized content graph hash changed");
        var snapshotGraph = result.Snapshot!.ContentGraph;
        Assert(CanonicalJson.Hash(outputGraph) == CanonicalJson.Hash(NormalizeA32Graph(snapshotGraph, A32WorkspaceRoot(result.Snapshot!))), caseName + " snapshot/output graph mismatch");
    }
}

static void AssertA32SnapshotGraph(CompileResult result, JsonObject expected, string caseName)
{
    var snapshot = result.Snapshot ?? throw new InvalidOperationException(caseName + " snapshot missing");
    var actual = NormalizeA32Graph(snapshot.ContentGraph, A32WorkspaceRoot(snapshot));
    Assert(snapshot.HasAdultClosure == expected["has_adult_closure"]!.GetValue<bool>(), caseName + " adult closure changed");
    Assert(CanonicalJson.Hash(actual).Equals(expected["graph_canonical_hash"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase), caseName + " graph canonical hash changed");
    Assert(actual.ToJsonString() == expected["graph"]!.ToJsonString(), caseName + " graph object/order changed");
    var expectedKeys = expected["graph_key_sequence"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
    Assert(FlattenJsonKeySequence(actual).SequenceEqual(expectedKeys), caseName + " graph key sequence changed");
}

static void AssertA32SnapshotGraphMatchesOutput(CompileResult result, string workspaceRoot, string caseName)
{
    var snapshot = result.Snapshot ?? throw new InvalidOperationException(caseName + " snapshot missing");
    Assert(result.Files.TryGetValue("content-graph.json", out var bytes), caseName + " content graph output missing");
    var snapshotGraph = NormalizeA32Graph(snapshot.ContentGraph, workspaceRoot);
    var outputGraph = NormalizeA32Graph(JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes!))!.AsObject(), workspaceRoot);
    Assert(CanonicalJson.Hash(snapshotGraph) == CanonicalJson.Hash(outputGraph), caseName + " snapshot graph differs from output graph");
}

static void AssertA32Diagnostics(ValidationReport actual, JsonArray expected, string workspaceRoot)
{
    var actualSequence = actual.Diagnostics.Select(item => $"{item.Code}|{item.Severity}|{NormalizeA32Path(item.Path, workspaceRoot)}").ToArray();
    var expectedSequence = expected.OfType<JsonObject>().Select(item => $"{item["code"]?.GetValue<string>()}|{item["severity"]?.GetValue<string>()}|{item["path"]?.GetValue<string>()}").ToArray();
    Assert(actualSequence.SequenceEqual(expectedSequence), "A3.2 diagnostic code/path/order changed");
}

static string A32WorkspaceRoot(ValidatedSnapshot snapshot)
{
    var documentPath = snapshot.Documents.FirstOrDefault().Path;
    return Path.GetDirectoryName(Path.GetDirectoryName(documentPath)!)!;
}

static JsonObject NormalizeA32Graph(JsonObject graph, string workspaceRoot)
{
    var copy = JsonNode.Parse(graph.ToJsonString())!.AsObject();
    foreach (var node in copy["nodes"]?.AsArray().OfType<JsonObject>() ?? [])
    {
        if (node["origin_pointer"] is JsonValue value && value.TryGetValue<string>(out var origin))
            node["origin_pointer"] = NormalizeA32Path(origin, workspaceRoot);
    }
    return copy;
}

static string? NormalizeA32Path(string? value, string workspaceRoot)
{
    if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value)) return value;
    var candidate = Path.GetFullPath(value);
    var root = Path.GetFullPath(workspaceRoot);
    var rootName = Path.GetPathRoot(root)!;
    var comparableRoot = root.Equals(rootName, StringComparison.OrdinalIgnoreCase) ? root : root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var comparableCandidate = candidate.Equals(Path.GetPathRoot(candidate), StringComparison.OrdinalIgnoreCase) ? candidate : candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    if (comparableCandidate.Equals(comparableRoot, StringComparison.OrdinalIgnoreCase)) return "<WORKSPACE_ROOT>";
    var childWithDirectorySeparator = comparableRoot + Path.DirectorySeparatorChar;
    var childWithAltSeparator = comparableRoot + Path.AltDirectorySeparatorChar;
    if (!comparableCandidate.StartsWith(childWithDirectorySeparator, StringComparison.OrdinalIgnoreCase)
        && !comparableCandidate.StartsWith(childWithAltSeparator, StringComparison.OrdinalIgnoreCase)) return value;
    return "<WORKSPACE_ROOT>/" + Path.GetRelativePath(comparableRoot, comparableCandidate).Replace('\\', '/');
}

static JsonObject LoadA33Golden()
{
    var path = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "fixtures", "a3-3-preview-golden.v1.json");
    return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
}

static string[] A33CaseNames() => new[]
{
    "baseline_commoner",
    "denied_noble",
    "unknown_public_referral",
    "rumor_commoner",
    "empty_rules",
    "identity_match",
    "identity_miss",
    "invalid_profile",
    "missing_text_raw"
};

static (string ProfileId, IdentitySnapshot Identity) A33Scenario(string caseName)
    => caseName switch
    {
        "baseline_commoner" => ("profile.commoner", new("a33.baseline", "profile.commoner", null, null, null, null, null, null)),
        "denied_noble" => ("profile.noble", new("a33.denied", "profile.noble", null, null, null, 40, 20, null)),
        "unknown_public_referral" => ("profile.commoner", new("a33.unknown", "profile.commoner", null, null, null, null, null, null)),
        "rumor_commoner" => ("profile.commoner", new("a33.rumor", "profile.commoner", null, null, null, null, null, null)),
        "empty_rules" => ("profile.commoner", new("a33.empty", "profile.commoner", null, null, null, null, null, null)),
        "identity_match" => ("profile.commoner", new("a33.identity-match", "profile.commoner", "entity.culture.test", "entity.kingdom.test", "entity.settlement.test", 35, 7, null)),
        "identity_miss" => ("profile.commoner", new("a33.identity-miss", "profile.commoner", "entity.culture.other", "entity.kingdom.other", "entity.settlement.other", 20, 2, null)),
        "invalid_profile" => ("profile.missing", new("a33.invalid-profile", "profile.missing", null, null, null, null, null, null)),
        "missing_text_raw" => ("profile.commoner", new("a33.missing-text", "profile.commoner", null, null, null, null, null, null)),
        _ => throw new InvalidOperationException("unknown A3.3 case: " + caseName)
    };

static void AssertA33BuilderSeam()
{
    var assembly = typeof(WorldbookApplicationService).Assembly;
    var builderType = assembly.GetType("Awake.WorldbookStudio.Core.PreviewProjectionBuilder", throwOnError: false);
    Assert(builderType is not null && !builderType.IsPublic, "A3.3 builder must be an internal type");
    var buildMethods = builderType?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(method => method.Name == "Build").ToArray() ?? [];
    Assert(buildMethods.Length == 1, "A3.3 builder must expose exactly one static Build seam");
    var sourceRoot = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "src", "Awake.WorldbookStudio.Core");
    var builderPath = Path.Combine(sourceRoot, "PreviewProjectionBuilder.cs");
    Assert(File.Exists(builderPath), "A3.3 builder source file is missing");
    var builderSource = File.ReadAllText(builderPath);
    foreach (var forbidden in new[] { "WorkspaceService", "File.", "Directory.", "HttpClient", "HttpRequestMessage", "NetworkStream" })
        Assert(!builderSource.Contains(forbidden, StringComparison.Ordinal), "A3.3 builder contains forbidden I/O or network dependency: " + forbidden);
    Assert(builderSource.Split("Build(", StringSplitOptions.None).Length - 1 == 1, "A3.3 builder must contain one Build method declaration");

    var applicationPath = Path.Combine(sourceRoot, "Application.cs");
    var applicationSource = File.ReadAllText(applicationPath);
    Assert(CountOccurrences(applicationSource, "PreviewProjectionBuilder.Build(") == 1, "Application.cs must call the A3.3 builder exactly once");
    foreach (var forbidden in new[] { "foreach (var item in snapshot.Documents)", "MatchingRules(", "MatchesDimension(", "LayerRank(", "SourceIds(" })
        Assert(!applicationSource.Contains(forbidden, StringComparison.Ordinal), "old Preview projection authority remains in Application.cs: " + forbidden);
    foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.TopDirectoryOnly).Where(path => !path.EndsWith("PreviewProjectionBuilder.cs", StringComparison.OrdinalIgnoreCase)))
    {
        var source = File.ReadAllText(file);
        foreach (var forbidden in new[] { "MatchingRules(", "MatchesDimension(", "LayerRank(", "SourceIds(" })
            Assert(!source.Contains(forbidden, StringComparison.Ordinal), "old Preview helper remains outside the builder: " + forbidden + " in " + Path.GetFileName(file));
    }
}

static void AssertA33RunManifest()
{
    var path = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "Awake.WorldbookStudio.Tests", "Program.cs");
    var lines = File.ReadAllLines(path).Where(line => line.StartsWith("Run(\"", StringComparison.Ordinal)).ToArray();
    var added = new[]
    {
        "Run(\"A3.3 preview golden\", () =>",
        "Run(\"A3.3 permission matrix\", () =>",
        "Run(\"A3.3 preview wiring and determinism\", () =>"
    };
    var a34 = new[]
    {
        "Run(\"A3.4 registry snapshot golden\", () =>",
        "Run(\"A3.4 registry diagnostics and empty boundary\", () =>",
        "Run(\"A3.4 registry wiring and determinism\", () =>"
    };
    var a4 = new[]
    {
        "Run(\"A4 semantic suggestion projection golden\", () =>",
        "Run(\"A4 CLI/Web wire casing and ownership\", () =>",
        "Run(\"A4 CLI/Web smoke coverage\", () =>"
    };
    var b1 = new[]
    {
        "Run(\"B1 entity catalog exposes base game and uninstalled DLC\", () =>",
        "Run(\"B1 entity catalog degrades without mapping package\", () =>"
    };
    var v2Package = new[]
    {
        "Run(\"F75 default compiled output commits inside compiled root\", () =>",
        "Run(\"F76 published manifest preserves runtime hash contract\", () =>",
        "Run(\"F77 published candidate passes AWAKE package integrity\", () =>",
        "Run(\"F78 published candidate loads and answers through AWAKE query\", () =>",
        "Run(\"F79 output rejects missing compile snapshot\", () =>",
        "Run(\"F80 output rechecks runtime content hash\", () =>",
        "Run(\"F81 successful publish releases lock file\", () =>"
    };
    var boundary = new[]
    {
        "Run(\"HTTP authority error matrix is explicit and safe\", () =>",
        "Run(\"HTTP authority errors distinguish mutation uncertainty\", () =>",
        "Run(\"HTTP unknown exceptions use safe 500 projection\", () =>",
        "Run(\"HTTP error response exposes matching correlation header and body\", () =>",
        "Run(\"HTTP response-started boundary does not append a second error body\", () =>"
    };
    var k1 = new[]
    {
        "Run(\"K1 runtime keywords carry titles document aliases and entity anchor names\", () =>",
        "Run(\"K1 runtime keywords degrade safely without the entity mapping package\", () =>",
        "Run(\"D1-2 ambiguous keywords are reported without blocking compilation\", () =>",
        "Run(\"compile settlement public projection tolerates null diagnostic fields\", () =>",
        "Run(\"X1 local worker settings stay encrypted and survive a cloud save\", () =>",
        "Run(\"X1 local worker settings override environment and reject non-loopback hosts\", () =>",
        "Run(\"X1 provider settings dialog exposes the local worker entry\", () =>"
    };
    var withoutA34 = lines.Where(line => !a34.Contains(line, StringComparer.Ordinal) && !a4.Contains(line, StringComparer.Ordinal) && !b1.Contains(line, StringComparer.Ordinal) && !v2Package.Contains(line, StringComparer.Ordinal) && !boundary.Contains(line, StringComparer.Ordinal) && !k1.Contains(line, StringComparer.Ordinal)).ToArray();
    Assert(withoutA34.Length == 93, "A3.3 harness named-case count must be 93, got " + withoutA34.Length);
    Assert(added.All(expected => lines.Count(line => line == expected) == 1), "A3.3 named cases are missing or duplicated");
    var baseline = withoutA34.Where(line => !added.Contains(line, StringComparer.Ordinal)).ToArray();
    Assert(baseline.Length == 90, "A3.3 existing named-case count changed: " + baseline.Length);
    var baselineHash = Hashing.Sha256Text(string.Join("\n", baseline));
    Assert(string.Equals(baselineHash, "35468699f85151a83729e828bc792210e69c3489677deffe115f0160f01a089f", StringComparison.OrdinalIgnoreCase), "A3.3 existing harness named-case manifest changed");
}

static void AssertA33GoldenCase(WorkspaceFixture fixture, PreviewResult result, ValidationReport snapshotReport, JsonObject expected, string caseName)
{
    Assert(result.Envelope.ToJsonString() == expected["envelope"]!.ToJsonString(), caseName + " envelope object/order changed");
    var envelopeJson = result.Envelope.ToJsonString();
    var envelopeBytes = System.Text.Encoding.UTF8.GetBytes(envelopeJson);
    Assert(envelopeJson == expected["envelope_json"]!.GetValue<string>(), caseName + " raw envelope JSON changed");
    Assert(Hashing.Sha256Bytes(envelopeBytes) == expected["envelope_utf8_sha256"]!.GetValue<string>(), caseName + " raw envelope SHA-256 changed");
    Assert(Convert.ToBase64String(envelopeBytes) == expected["envelope_utf8_base64"]!.GetValue<string>(), caseName + " raw envelope base64 changed");
    Assert(CanonicalJson.Hash(result.Envelope) == expected["envelope_canonical_hash"]!.GetValue<string>(), caseName + " canonical envelope hash changed");
    Assert(JsonSerializer.SerializeToNode(result.Items)!.ToJsonString() == expected["items"]!.ToJsonString(), caseName + " PreviewItem projection changed");
    Assert(A33DiagnosticArray(snapshotReport.Diagnostics).ToJsonString() == expected["snapshot_report_diagnostics"]!.ToJsonString(), caseName + " snapshot report diagnostics changed");
    Assert(A33DiagnosticArray(result.Diagnostics).ToJsonString() == expected["result_diagnostics"]!.ToJsonString(), caseName + " result diagnostics changed");
    Assert(result.Items.Count == (expected["status"]!.GetValue<string>() == "empty" ? 0 : 1), caseName + " item count/status changed");
    AssertA33InputFiles(fixture, expected, caseName);
}

static void AssertA33InputFiles(WorkspaceFixture fixture, JsonObject expected, string caseName)
{
    var actual = new JsonArray();
    foreach (var path in fixture.Workspace.EnumerateSnapshotInputPaths())
    {
        actual.Add(new JsonObject
        {
            ["path"] = Path.GetRelativePath(fixture.Root, path).Replace('\\', '/'),
            ["sha256"] = Hashing.FileSha256(path)
        });
    }
    Assert(actual.ToJsonString() == expected["input_files"]!.ToJsonString(), caseName + " input file closure/hash changed");
}

static void AssertA33PermissionCase(PreviewResult result, string caseName)
{
    var results = result.Envelope["npc_preview"]!.AsObject()["results"]!.AsArray();
    if (caseName == "invalid_profile")
    {
        Assert(results.Count == 0, "invalid profile must return an empty NPC projection");
        Assert(result.Diagnostics.Any(item => item.Code == "WB-PROFILE-001"), "invalid profile must produce WB-PROFILE-001");
        return;
    }
    Assert(results.Count == 1, caseName + " should produce one NPC projection");
    var npc = results[0]!.AsObject();
    switch (caseName)
    {
        case "baseline_commoner":
            Assert(npc["status"]?.GetValue<string>() == "visible" && npc["layer"]?.GetValue<string>() == "summary", "baseline commoner visibility changed");
            break;
        case "denied_noble":
            Assert(npc["status"]?.GetValue<string>() == "redacted", "deny must take precedence over grant");
            Assert(npc["text"] is null, "redacted knowledge must not expose text");
            Assert(npc["fallback_referral_ids"] is null, "denied knowledge must not expose referrals");
            var deniedDiagnostic = result.Envelope["author_diagnostics"]!.AsObject()["results"]!.AsArray()[0]!.AsObject();
            Assert(deniedDiagnostic["rule_ids"]!.AsArray().Select(item => item!.GetValue<string>()).SequenceEqual(new[] { "expr.demo.deny.0", "expr.demo.deny.1", "expr.demo.grant.0", "expr.demo.grant.1" }), "deny/grant rule order changed");
            Assert(deniedDiagnostic["rejection_code"]?.GetValue<string>() == "WB-PERMISSION-002", "deny rejection code changed");
            break;
        case "unknown_public_referral":
            Assert(npc["status"]?.GetValue<string>() == "unknown", "unmatched knowledge must remain unknown");
            Assert(npc["fallback_referral_ids"]!.AsArray().Select(item => item!.GetValue<string>()).SequenceEqual(new[] { "referral.notary_merchant", "referral.tavernkeeper" }), "public referral order/filter changed");
            var unknownDiagnostic = result.Envelope["author_diagnostics"]!.AsObject()["results"]!.AsArray()[0]!.AsObject();
            Assert(unknownDiagnostic["source_ids"]!.AsArray().Select(item => item!.GetValue<string>()).SequenceEqual(new[] { "source.demo" }), "source IDs must be distinct while preserving first occurrence");
            break;
        case "rumor_commoner":
            Assert(npc["status"]?.GetValue<string>() == "unknown" && npc["layer"]?.GetValue<string>() == "rumor", "rumor layer gating/projection changed");
            break;
        case "empty_rules":
            Assert(npc["status"]?.GetValue<string>() == "unknown" && npc["fallback_referral_ids"] is null, "empty permission rules must not create referrals");
            break;
        case "identity_match":
            Assert(npc["status"]?.GetValue<string>() == "visible", "matching identity dimensions/thresholds must grant knowledge");
            break;
        case "identity_miss":
            Assert(npc["status"]?.GetValue<string>() == "unknown", "identity dimension or threshold miss must deny the grant");
            break;
        case "missing_text_raw":
            Assert(npc["status"]?.GetValue<string>() == "visible" && npc["text"] is JsonObject text && text.Count == 0, "missing-text raw case must project an empty localized object");
            Assert(!result.Envelope["author_diagnostics"]!.AsObject()["results"]!.AsArray().Any(item => item!.AsObject().ContainsKey("schema_diagnostic")), "preview envelope must not gain a schema diagnostic field");
            break;
    }
}

static JsonArray A33DiagnosticArray(IEnumerable<Diagnostic> diagnostics)
    => new(diagnostics.Select(item => new JsonObject
    {
        ["code"] = item.Code,
        ["severity"] = item.Severity,
        ["message"] = item.Message,
        ["path"] = item.Path,
        ["detail"] = item.Detail
    }).ToArray());

static int CountOccurrences(string source, string token)
    => source.Split(token, StringSplitOptions.None).Length - 1;

static WorkspaceFixture MakeA33Workspace(string schemaRoot, string caseName, WorkspaceReadProbe? readProbe = null)
{
    var fixture = MakeWorkspace(schemaRoot, readProbe: readProbe);
    var seed = fixture.Service.ReadDocument("authoring/demo.yaml").Document;
    var document = JsonNode.Parse(seed.ToJsonString())!.AsObject();
    var expression = document["assertions"]!.AsArray()[0]!["expressions"]!.AsArray()[0]!.AsObject();
    switch (caseName)
    {
        case "denied_noble":
            expression["denies"] = new JsonArray(A33Rule("profile.noble", "summary"), A33Rule("profile.noble", "summary"));
            expression["grants"] = new JsonArray(A33Rule("profile.noble", "summary"), A33Rule("profile.noble", "summary"));
            break;
        case "unknown_public_referral":
            expression["grants"] = new JsonArray();
            expression["denies"] = new JsonArray();
            expression["fallback_referral_ids"] = new JsonArray("referral.notary_merchant", "referral.tavernkeeper");
            expression["sources"] = new JsonArray(expression["sources"]!.AsArray()[0]!.DeepClone(), expression["sources"]!.AsArray()[0]!.DeepClone());
            break;
        case "rumor_commoner":
            expression["layer"] = "rumor";
            break;
        case "empty_rules":
            expression["grants"] = new JsonArray();
            expression["denies"] = new JsonArray();
            expression.Remove("fallback_referral_ids");
            break;
        case "identity_match":
        case "identity_miss":
            expression["grants"] = new JsonArray(new JsonObject
            {
                ["profile_id"] = "profile.commoner",
                ["scope"] = "local",
                ["min_detail"] = "summary",
                ["culture_ids"] = new JsonArray("entity.culture.test"),
                ["kingdom_ids"] = new JsonArray("entity.kingdom.test"),
                ["settlement_ids"] = new JsonArray("entity.settlement.test"),
                ["min_age"] = 30,
                ["min_steward"] = 5
            });
            break;
        case "missing_text_raw":
            expression.Remove("text");
            break;
    }
    var yamlPath = Path.Combine(fixture.Root, "authoring", "demo.yaml");
    File.Delete(yamlPath);
    File.WriteAllText(Path.Combine(fixture.Root, "authoring", "demo.json"), document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
    return fixture;
}

static JsonObject A33Rule(string profileId, string minDetail)
    => new() { ["profile_id"] = profileId, ["scope"] = "local", ["min_detail"] = minDetail };
static JsonObject LoadA31Golden()
{
    var path = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "fixtures", "a3-1-authoring-template-golden.v1.json");
    return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
}

static void AssertA31Golden(JsonObject document, JsonObject golden)
{
    var expected = golden["expected"]!.AsObject();
    Assert(string.Equals(CanonicalJson.Hash(document), expected["canonical_hash"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase), "A3.1 canonical template hash changed");
    AssertOrderedKeys(document, expected["ordered_keys"]!.AsObject());
    foreach (var property in expected["values"]!.AsObject())
    {
        var actual = GetJsonPath(document, property.Key);
        var actualText = actual?.ToJsonString() ?? "null";
        var expectedText = property.Value?.ToJsonString() ?? "null";
        Assert(actualText == expectedText, $"A3.1 golden value changed at {property.Key}: {actualText}");
    }
    var jsonSequence = expected["json_key_sequence"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
    var yamlSequence = expected["yaml_key_sequence"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
    Assert(FlattenJsonKeySequence(document).SequenceEqual(jsonSequence), "A3.1 in-memory key sequence changed");
    Assert(document["assertions"]!.AsArray().Count == 1, "A3.1 assertion count changed");
    Assert(document["assertions"]!.AsArray()[0]!["expressions"]!.AsArray().Count == 1, "A3.1 expression count changed");
    Assert(expected["diagnostics"]!.AsArray().Count == 0, "A3.1 golden diagnostics must be empty");
}

static void AssertOrderedKeys(JsonObject root, JsonObject expected)
{
    foreach (var property in expected)
    {
        var node = GetJsonPath(root, property.Key);
        Assert(node is JsonObject, $"A3.1 ordered object missing at {property.Key}");
        var actual = node!.AsObject().Select(x => x.Key).ToArray();
        var wanted = property.Value!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Assert(actual.SequenceEqual(wanted), $"A3.1 property order changed at {property.Key}");
    }
}

static JsonNode? GetJsonPath(JsonNode root, string path)
{
    if (path == "$") return root;
    JsonNode? current = root;
    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(path, @"(?:\.([A-Za-z0-9_-]+)|\[(\d+)\])"))
    {
        if (current is null) return null;
        if (match.Groups[1].Success)
        {
            current = current.AsObject()[match.Groups[1].Value];
        }
        else
        {
            current = current.AsArray()[int.Parse(match.Groups[2].Value)];
        }
    }
    return current;
}

static string[] FlattenJsonKeySequence(JsonNode node)
{
    var result = new List<string>();
    Visit(node, result);
    return result.ToArray();

    static void Visit(JsonNode? current, List<string> result)
    {
        if (current is JsonObject obj)
        {
            foreach (var property in obj)
            {
                result.Add(property.Key);
                Visit(property.Value, result);
            }
        }
        else if (current is JsonArray array)
        {
            foreach (var item in array) Visit(item, result);
        }
    }
}

static void AssertSerializationOrder(string content, string[] expected, bool yaml)
{
    var actual = yaml
        ? System.Text.RegularExpressions.Regex.Matches(content, @"^\s*(?:-\s+)?(?<key>[A-Za-z_][A-Za-z0-9_-]*):(?:\s|$)", System.Text.RegularExpressions.RegexOptions.Multiline).Select(x => x.Groups["key"].Value).ToArray()
        : System.Text.RegularExpressions.Regex.Matches(content, "\\\"(?<key>(?:[^\\\"\\\\]|\\\\.)+)\\\"\\s*:").Select(x => x.Groups["key"].Value).ToArray();
    Assert(actual.SequenceEqual(expected), $"{(yaml ? "YAML" : "JSON")} serialized key order changed");
}

static void AssertA31RunManifest()
{
    var path = Path.Combine(FindStudioRoot(AppContext.BaseDirectory), "tests", "Awake.WorldbookStudio.Tests", "Program.cs");
    var lines = File.ReadAllLines(path).Where(line => line.StartsWith("Run(\"", StringComparison.Ordinal)).ToArray();
    var a33 = new[]
    {
        "Run(\"A3.3 preview golden\", () =>",
        "Run(\"A3.3 permission matrix\", () =>",
        "Run(\"A3.3 preview wiring and determinism\", () =>"
    };
    var a32 = new[]
    {
        "Run(\"A3.2 content graph matches pre-extraction golden\", () =>",
        "Run(\"A3.2 content graph tier closure and diagnostics\", () =>",
        "Run(\"A3.2 content graph compile wiring and determinism\", () =>"
    };
    var a34 = new[]
    {
        "Run(\"A3.4 registry snapshot golden\", () =>",
        "Run(\"A3.4 registry diagnostics and empty boundary\", () =>",
        "Run(\"A3.4 registry wiring and determinism\", () =>"
    };
    var a4 = new[]
    {
        "Run(\"A4 semantic suggestion projection golden\", () =>",
        "Run(\"A4 CLI/Web wire casing and ownership\", () =>",
        "Run(\"A4 CLI/Web smoke coverage\", () =>"
    };
    var b1 = new[]
    {
        "Run(\"B1 entity catalog exposes base game and uninstalled DLC\", () =>",
        "Run(\"B1 entity catalog degrades without mapping package\", () =>"
    };
    var v2Package = new[]
    {
        "Run(\"F75 default compiled output commits inside compiled root\", () =>",
        "Run(\"F76 published manifest preserves runtime hash contract\", () =>",
        "Run(\"F77 published candidate passes AWAKE package integrity\", () =>",
        "Run(\"F78 published candidate loads and answers through AWAKE query\", () =>",
        "Run(\"F79 output rejects missing compile snapshot\", () =>",
        "Run(\"F80 output rechecks runtime content hash\", () =>",
        "Run(\"F81 successful publish releases lock file\", () =>"
    };
    var k1 = new[]
    {
        "Run(\"K1 runtime keywords carry titles document aliases and entity anchor names\", () =>",
        "Run(\"K1 runtime keywords degrade safely without the entity mapping package\", () =>",
        "Run(\"D1-2 ambiguous keywords are reported without blocking compilation\", () =>",
        "Run(\"compile settlement public projection tolerates null diagnostic fields\", () =>",
        "Run(\"X1 local worker settings stay encrypted and survive a cloud save\", () =>",
        "Run(\"X1 local worker settings override environment and reject non-loopback hosts\", () =>",
        "Run(\"X1 provider settings dialog exposes the local worker entry\", () =>"
    };
    var withoutA32 = lines.Where(line => !a33.Contains(line, StringComparer.Ordinal) && !a32.Contains(line, StringComparer.Ordinal) && !a34.Contains(line, StringComparer.Ordinal) && !a4.Contains(line, StringComparer.Ordinal) && !b1.Contains(line, StringComparer.Ordinal) && !v2Package.Contains(line, StringComparer.Ordinal) && !k1.Contains(line, StringComparer.Ordinal)).ToArray();
    Assert(withoutA32.Length == 92, $"A3.1 harness case count changed: {withoutA32.Length}");
    var added = new[]
    {
        "Run(\"HTTP authority error matrix is explicit and safe\", () =>",
        "Run(\"HTTP authority errors distinguish mutation uncertainty\", () =>",
        "Run(\"HTTP unknown exceptions use safe 500 projection\", () =>",
        "Run(\"HTTP error response exposes matching correlation header and body\", () =>",
        "Run(\"HTTP response-started boundary does not append a second error body\", () =>",
        "Run(\"A3.1 authoring template matches independent golden\", () =>",
        "Run(\"A3.1 authoring YAML and JSON preserve normalized shape\", () =>",
        "Run(\"A3.1 CreateDocument preserves validation failure boundaries\", () =>",
        "Run(\"A3.1 authoring path and registry boundaries\", () =>"
    };
    Assert(added.All(expected => withoutA32.Count(line => line == expected) == 1), "A3.1 named cases are missing or duplicated");
    var baseline = withoutA32.Where(line => !added.Contains(line, StringComparer.Ordinal)).ToArray();
    var baselineHash = Hashing.Sha256Text(string.Join("\n", baseline));
    Assert(string.Equals(baselineHash, "d8035a0fd6e13027589e6a0e84a39cf8474967d77ef11a6bef65d4ec1595729b", StringComparison.OrdinalIgnoreCase), "existing harness named-case manifest changed");
}

static void AssertDiagnosticsEqual(ValidationReport left, ValidationReport right)
{
    static string[] Sequence(ValidationReport report) => report.Diagnostics.Select(x => $"{x.Code}|{x.Severity}|{x.Message}|{x.Path}|{x.Detail}").ToArray();
    Assert(Sequence(left).SequenceEqual(Sequence(right)), "diagnostic sequence changed between authoring formats");
}

static void AssertCreateDocumentFailure(WorkspaceFixture fixture, string path, string documentId, string domain, string contentTier, string authorId, string code)
{
    AssertThrowsCode(() => fixture.Service.CreateDocument(path, documentId, "失败边界", domain, contentTier, authorId), code);
}

static T RequiredProperty<T>(object target, string name)
{
    var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"required property missing: {target.GetType().Name}.{name}");
    var value = property.GetValue(target);
    if (value is T typed) return typed;
    throw new InvalidOperationException($"required property type mismatch: {target.GetType().Name}.{name}");
}

static List<object> RequiredEnumerable(object target, string name)
{
    var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"required collection missing: {target.GetType().Name}.{name}");
    if (property.GetValue(target) is not IEnumerable values) throw new InvalidOperationException($"required collection invalid: {target.GetType().Name}.{name}");
    return values.Cast<object>().ToList();
}

static AssistanceRequest MakeAssistanceRequest(string requestId)
{
    var document = new JsonObject
    {
        ["schema_version"] = "awake.worldbook.authoring.v1",
        ["id"] = $"doc.war.{requestId}",
        ["title"] = new JsonObject { ["zh-CN"] = "测试档案" },
        ["assertions"] = new JsonArray()
    };
    var registry = new JsonObject { ["profiles"] = new JsonArray("profile.commoner") };
    return new AssistanceRequest(
        "assistance.request.v1",
        "test",
        AssistanceAnalysisKind.Consistency,
        $"authoring/{requestId}.yaml",
        $"doc.war.{requestId}",
        new string('b', 64),
        1,
        $"nonce-{requestId}",
        document,
        registry,
        new string('a', 64));
}

static string CloudResponseJson(string requestHash, string sourceHash)
    => new JsonObject
    {
        ["choices"] = new JsonArray
        {
            new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["content"] = AssistanceJson(requestHash, sourceHash)
                }
            }
        }
    }.ToJsonString();
static AssistanceResult MakeAssistanceResult(AssistanceRequest request, string suggestionId)
{
    var patch = new KnowledgePatch("knowledge-patch.v1", new[]
    {
        new KnowledgePatchOperation("replace", "/title/zh-CN", JsonValue.Create("AI 建议标题"))
    });
    var suggestion = new AssistanceSuggestion(
        suggestionId,
        "prose",
        "info",
        0.75,
        "标题建议",
        "仅用于测试的候选文本建议。",
        "AI 建议标题",
        patch,
        true);
    return new AssistanceResult("assistance.result.v1", request.RequestHash, request.SourceDocumentHash, new[] { suggestion });
}
static string AssistanceJson(string requestHash, string sourceHash)
    => new JsonObject
    {
        ["schema_version"] = "assistance.result.v1",
        ["request_hash"] = requestHash,
        ["source_document_hash"] = sourceHash,
        ["suggestions"] = new JsonArray()
    }.ToJsonString();

static JsonObject ReadJson(HttpRequestMessage request)
    => JsonNode.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())!.AsObject();

static HttpResponseMessage JsonResponse(int statusCode, string content)
    => new((HttpStatusCode)statusCode)
    {
        Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
    };


static WorkspaceFixture MakeWorkspace(string schemaRoot, string fixtureName = "fixture-valid-minimal.yaml", Func<string, string>? mutate = null, WorkspaceReadProbe? readProbe = null)
{
    var root = Path.Combine(Path.GetTempPath(), "awake-worldbook-studio-tests", Guid.NewGuid().ToString("N"));
    var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot) { ReadProbe = readProbe });
    workspace.Initialize();
    var text = File.ReadAllText(Path.Combine(schemaRoot, fixtureName));
    if (mutate is not null) text = mutate(text);
    File.WriteAllText(Path.Combine(root, "authoring", "demo.yaml"), text);
    File.Copy(Path.Combine(schemaRoot, "source-fixture-demo.yaml"), Path.Combine(root, "authoring", "sources", "source-demo.yaml"));
    File.Copy(Path.Combine(schemaRoot, "source-demo.txt"), Path.Combine(root, "authoring", "sources", "source-demo.txt"));
    return new WorkspaceFixture(root, workspace, new WorldbookApplicationService(workspace));
}

static string FindSchemaRoot(string start)
{
    var current = Path.GetFullPath(start);
    while (!string.IsNullOrEmpty(current))
    {
        var candidate = Path.Combine(current, "docs", "worldbook-studio-plan");
        if (File.Exists(Path.Combine(candidate, "awake.worldbook.authoring.v1.schema.json"))) return candidate;
        current = Directory.GetParent(current)?.FullName ?? string.Empty;
    }
    throw new DirectoryNotFoundException("worldbook studio schema root not found");
}

static string CopySchemaRoot(string source)
{
    var target = Path.Combine(Path.GetTempPath(), "awake-worldbook-schema-copy", Guid.NewGuid().ToString("N"));
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(source, file);
        var destination = Path.Combine(target, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(file, destination);
    }
    return target;
}

static void CopyDirectory(string source, string target)
{
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(source, file);
        var destination = Path.Combine(target, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(file, destination, true);
    }
}

static string FindContractRoot(string start)
{
    var current = Path.GetFullPath(start);
    while (!string.IsNullOrEmpty(current))
    {
        var candidate = Path.Combine(current, "tools", "worldbook-contract", "v1");
        if (File.Exists(Path.Combine(candidate, "runtime.schema.json"))) return candidate;
        current = Directory.GetParent(current)?.FullName ?? string.Empty;
    }
    throw new DirectoryNotFoundException("worldbook contract root not found");
}

static string FindStudioRoot(string start)
{
    var current = Path.GetFullPath(start);
    while (!string.IsNullOrEmpty(current))
    {
        var candidate = Path.Combine(current, "src", "Awake.WorldbookStudio.Web", "wwwroot", "index.html");
        if (File.Exists(candidate)) return current;
        current = Directory.GetParent(current)?.FullName ?? string.Empty;
    }
    throw new DirectoryNotFoundException("worldbook studio root not found");
}

sealed class WorkspaceFixture : IDisposable
{
    public string Root { get; }
    public WorkspaceService Workspace { get; }
    public WorldbookApplicationService Service { get; }
    public WorkspaceFixture(string root, WorkspaceService workspace, WorldbookApplicationService service) { Root = root; Workspace = workspace; Service = service; }
    public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
}
sealed class RegistryWorkspaceFixture : IDisposable
{
    private readonly WorkspaceFixture _fixture;
    public string SchemaRoot { get; }
    public string Root => _fixture.Root;
    public WorkspaceService Workspace => _fixture.Workspace;
    public WorldbookApplicationService Service => _fixture.Service;
    public RegistryWorkspaceFixture(string schemaRoot, WorkspaceFixture fixture) { SchemaRoot = schemaRoot; _fixture = fixture; }
    public void Dispose()
    {
        _fixture.Dispose();
        try { Directory.Delete(SchemaRoot, true); } catch { }
    }
}
sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
    public int RequestCount { get; private set; }
    public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        return Task.FromResult(_handler(request));
    }
}

sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);

sealed class StartedResponseFeature : IHttpResponseFeature
{
    public StartedResponseFeature(Stream body) { Body = body; }
    public int StatusCode { get; set; }
    public string? ReasonPhrase { get; set; }
    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
    public Stream Body { get; set; }
    public bool HasStarted => true;
    public void OnStarting(Func<object, Task> callback, object state) { }
    public void OnCompleted(Func<object, Task> callback, object state) { }
}
