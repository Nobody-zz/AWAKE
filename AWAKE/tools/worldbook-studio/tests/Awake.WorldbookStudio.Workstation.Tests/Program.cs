using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Awake.WorldbookStudio.Core;
using Awake.WorldbookStudio.Web;

var passed = 0;
var total = 0;

void Run(string name, Action test)
{
    total++;
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

Run("validator accepts frozen envelope and recomputes fingerprint", () =>
{
    var envelope = TestEnvelope();
    var result = WorkstationHandoffValidator.Validate(envelope, TestClock());
    Assert(result.HandoffId == "pwb-handoff-0123456789abcdef0123456789abcdef", "handoff id should survive validation");
    Assert(result.ContentSha256 == Hashing.Sha256Text(result.Payload).ToLowerInvariant(), "content hash should match payload bytes");
});

Run("validator rejects changed payload hash with 422", () =>
{
    var envelope = TestEnvelope();
    envelope["content_sha256"] = new string('a', 64);
    ExpectCode("WB-HANDOFF-422", () => WorkstationHandoffValidator.Validate(envelope, TestClock()));
});

Run("validator rejects unknown lifecycle field with 400", () =>
{
    var envelope = TestEnvelope();
    envelope["lifecycle_status"] = "issued";
    ExpectCode("WB-HANDOFF-400", () => WorkstationHandoffValidator.Validate(envelope, TestClock()));
});

Run("import accept consume are idempotent and review-only", () =>
{
    var root = NewTempDirectory();
    var drafts = new AuthoringDraftStore();
    var store = new WorkstationHandoffStore(root, drafts, clock: TestClock);
    var first = store.Import(TestEnvelope());
    var duplicate = store.Import(TestEnvelope());
    Assert(first.Receipt["receipt_id"]?.GetValue<string>() == duplicate.Receipt["receipt_id"]?.GetValue<string>(), "duplicate import must reuse receipt");
    var accepted = store.Accept(first.HandoffId, first.Receipt["receipt_id"]!.GetValue<string>(), "issued");
    var consumed = store.Consume(first.HandoffId, accepted.Receipt["receipt_id"]!.GetValue<string>(), "accepted");
    var duplicateConsumed = store.Consume(first.HandoffId, consumed.Receipt["receipt_id"]!.GetValue<string>(), "accepted");
    Assert(consumed.Receipt["lifecycle_status"]?.GetValue<string>() == "consumed", "consume must settle consumed");
    Assert(consumed.DraftId is not null, "consume must return a draft id");
    Assert(consumed.DraftStatus == "needs_review" && consumed.ReviewOnly, "consumed draft must remain review-only");
    Assert(duplicateConsumed.DraftId == consumed.DraftId, "duplicate consume must preserve draft id");
    Assert(!Directory.EnumerateFiles(root, "*.canon.json", SearchOption.AllDirectories).Any(), "handoff consume must not write canon");
});

Run("changed duplicate fingerprint is a stable conflict", () =>
{
    var root = NewTempDirectory();
    var store = new WorkstationHandoffStore(root, new AuthoringDraftStore(), clock: TestClock);
    var first = TestEnvelope();
    store.Import(first);
    var changed = TestEnvelope();
    changed["document_id"] = "persona-2";
    changed["request_fingerprint"] = WorkstationHandoffValidator.ComputeFingerprint(changed);
    ExpectCode("WB-HANDOFF-409", () => store.Import(changed));
});

Run("recover marks an interrupted consume unknown and preserves reason", () =>
{
    var root = NewTempDirectory();
    var writerCalls = 0;
    var store = new WorkstationHandoffStore(
        root,
        new AuthoringDraftStore(),
        (_, _) =>
        {
            writerCalls++;
            throw new IOException("injected draft write failure");
        },
        clock: TestClock);
    var imported = store.Import(TestEnvelope());
    var accepted = store.Accept(imported.HandoffId, imported.Receipt["receipt_id"]!.GetValue<string>(), "issued");
    ExpectCode("WB-HANDOFF-503", () => store.Consume(imported.HandoffId, accepted.Receipt["receipt_id"]!.GetValue<string>(), "accepted"));
    var unknown = store.Recover(imported.HandoffId, accepted.Receipt["receipt_id"]!.GetValue<string>(), "mark_unknown", "人工确认未能读取草稿结果");
    Assert(writerCalls == 1, "failed consume should call writer once");
    Assert(unknown.Receipt["lifecycle_status"]?.GetValue<string>() == "unknown", "recovery should be terminal unknown");
    Assert(unknown.Receipt["recovery_reason"]?.GetValue<string>() == "人工确认未能读取草稿结果", "recovery reason must persist");
    ExpectCode("WB-HANDOFF-503", () => store.Recover(imported.HandoffId, accepted.Receipt["receipt_id"]!.GetValue<string>(), "retry_consume", null));
});

Run("recover retry consumes the same handoff after an interrupted write", () =>
{
    var root = NewTempDirectory();
    var drafts = new AuthoringDraftStore();
    var failing = new WorkstationHandoffStore(
        root,
        drafts,
        (_, _) => throw new IOException("injected draft write failure"),
        clock: TestClock);
    var imported = failing.Import(TestEnvelope());
    var accepted = failing.Accept(imported.HandoffId, imported.Receipt["receipt_id"]!.GetValue<string>(), "issued");
    ExpectCode("WB-HANDOFF-503", () => failing.Consume(imported.HandoffId, accepted.Receipt["receipt_id"]!.GetValue<string>(), "accepted"));

    var recovered = new WorkstationHandoffStore(root, drafts, clock: TestClock)
        .Recover(imported.HandoffId, accepted.Receipt["receipt_id"]!.GetValue<string>(), "retry_consume", null);
    var reloadedStore = new WorkstationHandoffStore(root, drafts, clock: TestClock);
    var reloaded = reloadedStore.Get(imported.HandoffId);
    Assert(recovered.Receipt["lifecycle_status"]?.GetValue<string>() == "consumed", "retry_consume must settle consumed");
    Assert(reloaded.DraftId == recovered.DraftId, "retry_consume must preserve the deterministic draft id");
});

Run("deterministic consume fault persists in_doubt across restart", () =>
{
    var root = NewTempDirectory();
    var drafts = new AuthoringDraftStore();
    var store = new WorkstationHandoffStore(root, drafts, clock: TestClock);
    var imported = store.Import(TestEnvelope());
    var accepted = store.Accept(imported.HandoffId, imported.Receipt["receipt_id"]!.GetValue<string>(), "issued");
    ExpectCode(
        "WB-HANDOFF-503",
        () => store.Consume(
            imported.HandoffId,
            accepted.Receipt["receipt_id"]!.GetValue<string>(),
            "accepted",
            injectInDoubtFailure: true));

    var reloadedStore = new WorkstationHandoffStore(root, drafts, clock: TestClock);
    var reloaded = reloadedStore.Get(imported.HandoffId);
    Assert(reloaded.Receipt["lifecycle_status"]?.GetValue<string>() == "in_doubt", "injected failure must persist in_doubt");
    Assert(reloaded.Receipt["recovery_required"]?.GetValue<bool>() == true, "in_doubt must require recovery");
    Assert(reloaded.Receipt["accepted_at_utc"] is not null, "in_doubt must retain accepted timestamp");
    Assert(reloaded.Receipt["consumed_at_utc"] is null, "in_doubt must not claim consumed timestamp");
    Assert(reloaded.Receipt["draft_id"] is null, "in_doubt must not claim draft id");
    Assert(reloaded.Receipt["recovery_reason"] is null, "in_doubt must not invent a recovery reason");

    var unknown = reloadedStore.Recover(
        imported.HandoffId,
        accepted.Receipt["receipt_id"]!.GetValue<string>(),
        "mark_unknown",
        "重启后无法确认草稿写入结果");
    var unknownReloaded = reloadedStore.Get(imported.HandoffId);
    Assert(unknown.Receipt["lifecycle_status"]?.GetValue<string>() == "unknown", "mark_unknown must settle unknown");
    Assert(unknownReloaded.Receipt["recovery_reason"]?.GetValue<string>() == "重启后无法确认草稿写入结果", "unknown reason must survive restart");
    Assert(unknownReloaded.Receipt["consumed_at_utc"] is null, "unknown must not claim consumed timestamp");
});

Run("invalid in_doubt receipt state is rejected on restart", () =>
{
    var root = NewTempDirectory();
    var store = new WorkstationHandoffStore(root, new AuthoringDraftStore(), clock: TestClock);
    var imported = store.Import(TestEnvelope());
    var accepted = store.Accept(imported.HandoffId, imported.Receipt["receipt_id"]!.GetValue<string>(), "issued");
    ExpectCode(
        "WB-HANDOFF-503",
        () => store.Consume(
            imported.HandoffId,
            accepted.Receipt["receipt_id"]!.GetValue<string>(),
            "accepted",
            injectInDoubtFailure: true));

    var ledgerPath = Path.Combine(root, "handoff-inbox.v1.ndjson");
    var lines = File.ReadAllLines(ledgerPath);
    var last = JsonNode.Parse(lines[^1])!.AsObject();
    last["receipt"]!["recovery_required"] = false;
    lines[^1] = CanonicalJson.Serialize(last);
    File.WriteAllLines(ledgerPath, lines);

    ExpectException<InvalidOperationException>(() => new WorkstationHandoffStore(root, new AuthoringDraftStore(), clock: TestClock));
});

Run("expiry transitions issued receipt to expired and blocks accept", () =>
{
    var now = TestClock();
    var root = NewTempDirectory();
    var store = new WorkstationHandoffStore(root, new AuthoringDraftStore(), clock: () => now);
    var imported = store.Import(TestEnvelope());
    now = new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero);
    ExpectCode("WB-HANDOFF-410", () => store.Accept(imported.HandoffId, imported.Receipt["receipt_id"]!.GetValue<string>(), "issued"));
    Assert(store.Get(imported.HandoffId).Receipt["lifecycle_status"]?.GetValue<string>() == "expired", "expired receipt must be persisted");
});

Run("receipt contains only frozen fields and review-only state", () =>
{
    var root = NewTempDirectory();
    var result = new WorkstationHandoffStore(root, new AuthoringDraftStore(), clock: TestClock).Import(TestEnvelope());
    var allowed = new HashSet<string>(
        ["schema_version", "receipt_id", "handoff_id", "consumer", "lifecycle_status",
            "consumer_review_status", "recovery_required", "accepted_at_utc",
            "consumed_at_utc", "draft_id", "recovery_reason", "request_fingerprint"],
        StringComparer.Ordinal);
    Assert(result.Receipt.All(item => allowed.Contains(item.Key)), "receipt must not contain non-schema fields");
    Assert(result.Receipt["consumer"]?.GetValue<string>() == "worldbook_studio", "receipt consumer must be WBS");
    Assert(result.Receipt["consumer_review_status"]?.GetValue<string>() == "needs_review", "consumer review must start needs_review");
});

Run("fault injection header is ignored outside Development and Test", () =>
{
    var production = WorkstationHandoffFaultInjection.ForEnvironment(new TestHostEnvironment("Production"));
    var request = new DefaultHttpContext().Request;
    request.Headers[WorkstationHandoffFaultInjection.HeaderName] = "consume_in_doubt";
    Assert(!production.ShouldInjectConsumeInDoubt(request), "production must not activate fault injection");
});

Console.WriteLine($"WORKSTATION TESTS: {passed}/{total} PASS");
if (passed != total) Environment.ExitCode = 1;

static JsonObject TestEnvelope()
{
    var payload = CanonicalJson.Serialize(new JsonObject
    {
        ["id"] = "persona-1",
        ["name"] = "Nordvig"
    });
    var envelope = new JsonObject
    {
        ["schema_version"] = "awake.workstation.handoff-envelope.v1",
        ["handoff_id"] = "pwb-handoff-0123456789abcdef0123456789abcdef",
        ["workspace_id"] = "awake-test",
        ["document_id"] = "persona-1",
        ["revision"] = 3,
        ["content_sha256"] = Hashing.Sha256Text(payload).ToLowerInvariant(),
        ["producer"] = "persona_workbench",
        ["provenance"] = new JsonObject
        {
            ["source_schema"] = "persona.workbench.persona.v1",
            ["source_id"] = "persona-1",
            ["source_revision"] = 3,
            ["source_sha256"] = new string('b', 64),
            ["issuer_id"] = "persona-workbench"
        },
        ["review_only"] = true,
        ["review_status"] = "approved_local",
        ["issued_at_utc"] = "2026-09-04T00:00:00Z",
        ["expires_at_utc"] = "2026-09-05T00:00:00Z",
        ["payload"] = payload
    };
    envelope["request_fingerprint"] = WorkstationHandoffValidator.ComputeFingerprint(envelope);
    return envelope;
}

DateTimeOffset TestClock() => new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

static string NewTempDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "awake-wbs-handoff-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void ExpectCode(string code, Action action)
{
    try
    {
        action();
    }
    catch (WorkstationHandoffException ex) when (ex.Code == code)
    {
        return;
    }
    throw new InvalidOperationException($"expected {code}");
}

static void ExpectException<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException($"expected {typeof(TException).Name}");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "Awake.WorldbookStudio.Workstation.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
