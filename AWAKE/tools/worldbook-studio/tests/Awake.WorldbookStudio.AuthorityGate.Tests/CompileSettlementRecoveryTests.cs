using System.Text;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.AuthorityGate.Tests;

internal static class CompileSettlementRecoveryTests
{
    public static void Run()
    {
        foreach (var point in Enum.GetValues<CompileSettlementFaultPoint>())
            RunFaultPoint(point);
        RunTargetReplacementRecovery();
        RunCrossInstanceTargetCompetition();
        RunSameProofCompetition();
        RunWorkspaceWriterLeaseBoundary();
        RunIndependentProcessCompetitionAndRecovery();
    }

    private static void RunCrossInstanceTargetCompetition()
    {
        using var fixture = new AuthorityGateTestFixture();
        var first = fixture.IssueProof("competition-a");
        var second = fixture.IssueProof("competition-b");
        var targetRoot = Path.Combine(fixture.Root, "compiled", "competition");
        var firstAuthority = new AuthorityGateService(fixture.Workspace, fixture.Service);
        var secondAuthority = new AuthorityGateService(fixture.Workspace, fixture.Service);
        var results = Task.WhenAll(
            Task.Run(() => CaptureCompile(() => firstAuthority.CompileApproved(first.Compile.CompileProofId, outputRoot: targetRoot))),
            Task.Run(() => CaptureCompile(() => secondAuthority.CompileApproved(second.Compile.CompileProofId, outputRoot: targetRoot)))).GetAwaiter().GetResult();
        AuthorityGateTestFixture.Assert(results.Count(result => result.Success) == 1, "same target competition must have one winner");
        AuthorityGateTestFixture.Assert(results.Count(result => result.ErrorCode == "WB-AUTHORITY-OPERATION-409") == 1, "same target competition loser must be a deterministic conflict");
        var operationRoot = Path.Combine(fixture.Root, "authoring-v1", "operations");
        AuthorityGateTestFixture.Assert(CountCompileRuntimeOperations(operationRoot) == 1, "same target competition created more than one compile operation");
        var journalPath = Path.Combine(fixture.Root, "authoring-v1", "operation-journal.jsonl");
        AuthorityGateTestFixture.Assert(File.ReadAllLines(journalPath).Count(line => line.Contains("compile_runtime", StringComparison.Ordinal)) == 1, "same target competition appended more than one compile journal entry");
    }

    private static void RunSameProofCompetition()
    {
        using var fixture = new AuthorityGateTestFixture();
        var issued = fixture.IssueProof("same-proof");
        var targetRoot = Path.Combine(fixture.Root, "compiled", "same-proof");
        var firstAuthority = new AuthorityGateService(fixture.Workspace, fixture.Service);
        var secondAuthority = new AuthorityGateService(fixture.Workspace, fixture.Service);
        var results = Task.WhenAll(
            Task.Run(() => CaptureCompile(() => firstAuthority.CompileApproved(issued.Compile.CompileProofId, outputRoot: targetRoot))),
            Task.Run(() => CaptureCompile(() => secondAuthority.CompileApproved(issued.Compile.CompileProofId, outputRoot: targetRoot)))).GetAwaiter().GetResult();
        AuthorityGateTestFixture.Assert(results.All(result => result.Success || result.ErrorCode == "WB-AUTHORITY-OPERATION-409"), "same proof competition returned an unstable error");
        var operationRoot = Path.Combine(fixture.Root, "authoring-v1", "operations");
        AuthorityGateTestFixture.Assert(CountCompileRuntimeOperations(operationRoot) == 1, "same proof competition created more than one compile operation");
        var journalPath = Path.Combine(fixture.Root, "authoring-v1", "operation-journal.jsonl");
        AuthorityGateTestFixture.Assert(File.ReadAllLines(journalPath).Count(line => line.Contains("compile_runtime", StringComparison.Ordinal)) == 1, "same proof competition appended more than one compile journal entry");
    }

    private static void RunWorkspaceWriterLeaseBoundary()
    {
        using var fixture = new AuthorityGateTestFixture();
        var issued = fixture.IssueProof("workspace-lease");
        var operationsRoot = Path.Combine(fixture.Root, "authoring-v1", "operations");
        var leasePath = Path.Combine(operationsRoot, ".compile-workspace-lease");
        Directory.CreateDirectory(operationsRoot);
        using (var externalLease = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-OPERATION-409", () => fixture.Authority.CompileApproved(issued.Compile.CompileProofId, outputRoot: Path.Combine(fixture.Root, "compiled", "lease-blocked")));
            AuthorityGateTestFixture.Assert(CountCompileRuntimeOperations(operationsRoot) == 0, "workspace lease conflict created an operation record");
            var reservationRoot = Path.Combine(operationsRoot, "reservations");
            AuthorityGateTestFixture.Assert(!Directory.Exists(reservationRoot) || !Directory.EnumerateFiles(reservationRoot, "*.json").Any(), "workspace lease conflict created a reservation");
        }
    }

    private static CompileAttempt CaptureCompile(Func<AuthorityCompileSettlement> action)
    {
        try { action(); return new CompileAttempt(true, null); }
        catch (InvalidOperationException error) { return new CompileAttempt(false, error.Message.Split(':', 2)[0]); }
    }

    private sealed record CompileAttempt(bool Success, string? ErrorCode);

    private static void RunFaultPoint(CompileSettlementFaultPoint point)
    {
        using var fixture = new AuthorityGateTestFixture(current =>
        {
            if (current == point) throw new InvalidOperationException("injected compile settlement fault: test");
        });
        var issued = fixture.IssueProof("crash-" + point);
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-MUTATION-UNKNOWN", () => fixture.Authority.CompileApproved(issued.Compile.CompileProofId));

        var resumed = new AuthorityGateService(fixture.Workspace, fixture.Service);
        resumed.Initialize();
        var operationId = CompileSettlementSupport.OperationId(issued.Compile.CompileProofId);
        var operation = resumed.ReadOperation(operationId);
        var state = operation["state"]?.GetValue<string>();
        AuthorityGateTestFixture.Assert(state is "committed" or "failed_recovery" or "quarantined", $"fault point {point} did not reach a terminal state: {state}");
        AuthorityGateTestFixture.Assert(state is not "reserved" and not "prepared" and not "executing", $"fault point {point} left a non-terminal state");

        var replay = new AuthorityGateService(fixture.Workspace, fixture.Service);
        replay.Initialize();
        var replayed = replay.ReadOperation(operationId);
        AuthorityGateTestFixture.Assert(replayed["state"]?.GetValue<string>() == state, $"fault point {point} changed terminal state on second recovery");
    }

    private static void RunTargetReplacementRecovery()
    {
        using var fixture = new AuthorityGateTestFixture();
        var first = fixture.IssueProof("previous");
        var targetRoot = Path.Combine(fixture.Root, "compiled", "customer");
        fixture.Authority.CompileApproved(first.Compile.CompileProofId, outputRoot: targetRoot);
        var previousManifestHash = Hashing.FileSha256(Path.Combine(targetRoot, "manifest.json"));

        var second = fixture.IssueProof("replacement");
        var crashing = new AuthorityGateService(fixture.Workspace, fixture.Service, current =>
        {
            if (current == CompileSettlementFaultPoint.AfterTargetReplace) throw new InvalidOperationException("injected target replacement crash");
        });
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-MUTATION-UNKNOWN", () => crashing.CompileApproved(second.Compile.CompileProofId, outputRoot: targetRoot));

        var operationId = CompileSettlementSupport.OperationId(second.Compile.CompileProofId);
        var operationKey = StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.Operation, new Dictionary<string, object?> { ["operation_id"] = operationId });
        var operationBeforeRecovery = AuthorityGateTestFixture.ReadJson(Path.Combine(fixture.Root, "authoring-v1", "operations", operationKey + ".json"));
        var previousBeforeRecovery = Path.Combine(fixture.Root, operationBeforeRecovery["previous_relative"]!.GetValue<string>()!.Replace('/', Path.DirectorySeparatorChar));
        AuthorityGateTestFixture.Assert(Directory.Exists(previousBeforeRecovery), "replacement previous target was not durable before recovery");
        AuthorityGateTestFixture.Assert(Hashing.FileSha256(Path.Combine(previousBeforeRecovery, "manifest.json")) == previousManifestHash, "replacement previous manifest changed before recovery");
        var verifyPrevious = typeof(AuthorityGateService).GetMethod("VerifyPreviousCompileTarget", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        AuthorityGateTestFixture.Assert((bool)verifyPrevious.Invoke(null, [previousBeforeRecovery, previousManifestHash])!, "replacement previous target failed full integrity verification before recovery");
        var resumed = new AuthorityGateService(fixture.Workspace, fixture.Service);
        resumed.Initialize();
        var operation = resumed.ReadOperation(operationId);
        var previousPath = string.IsNullOrWhiteSpace(operation["previous_relative"]?.GetValue<string>()) ? string.Empty : Path.Combine(fixture.Root, operation["previous_relative"]!.GetValue<string>()!.Replace('/', Path.DirectorySeparatorChar));
        var quarantinePath = Path.Combine(fixture.Root, "compiled", "quarantine", operationId);
        var evidence = $"state={operation["state"]?.GetValue<string>()}, phase={operation["replacement_phase"]?.GetValue<string>()}, previous={operation["previous_relative"]?.GetValue<string>()}, previousExists={Directory.Exists(previousPath)}, targetExists={Directory.Exists(targetRoot)}, quarantine={Directory.Exists(quarantinePath)}";
        AuthorityGateTestFixture.Assert(operation["state"]?.GetValue<string>() == "failed_recovery", $"target replacement recovery should fail closed after restoring previous: {evidence}");
        AuthorityGateTestFixture.Assert(Hashing.FileSha256(Path.Combine(targetRoot, "manifest.json")) == previousManifestHash, "previous compiled target was not restored");
        AuthorityGateTestFixture.Assert(Directory.Exists(Path.Combine(fixture.Root, "compiled", "quarantine", operationId, "candidate")), "replacement candidate was not quarantined");

        var replay = new AuthorityGateService(fixture.Workspace, fixture.Service);
        replay.Initialize();
        AuthorityGateTestFixture.Assert(replay.ReadOperation(operationId)["state"]?.GetValue<string>() == "failed_recovery", "target replacement recovery was not idempotent");
    }

    private static void RunIndependentProcessCompetitionAndRecovery()
    {
        using var fixture = new AuthorityGateTestFixture();
        var first = fixture.IssueProof("process-a");
        var second = fixture.IssueProof("process-b");
        var targetRoot = Path.Combine(fixture.Root, "compiled", "process-competition");
        using var firstProcess = CrossProcessCompileHarness.Start("compile-hold", fixture.Root, first.Compile.CompileProofId, targetRoot, Path.Combine(fixture.Root, "first.ready"), Path.Combine(fixture.Root, "first.release"));
        CrossProcessCompileHarness.WaitForFile(Path.Combine(fixture.Root, "first.ready"), firstProcess);
        using var secondProcess = CrossProcessCompileHarness.Start("compile", fixture.Root, second.Compile.CompileProofId, targetRoot);
        var secondOutput = CrossProcessCompileHarness.ReadOutput(secondProcess);
        AuthorityGateTestFixture.Assert(secondProcess.ExitCode == 2 && secondOutput.Contains("ERROR|WB-AUTHORITY-OPERATION-409", StringComparison.Ordinal), "independent process loser did not fail with compile conflict");
        File.WriteAllText(Path.Combine(fixture.Root, "first.release"), "release", Encoding.UTF8);
        var firstOutput = CrossProcessCompileHarness.ReadOutput(firstProcess);
        AuthorityGateTestFixture.Assert(firstProcess.ExitCode == 0 && firstOutput.Contains("SUCCESS", StringComparison.Ordinal), "independent process winner did not commit");

        var operationRoot = Path.Combine(fixture.Root, "authoring-v1", "operations");
        AuthorityGateTestFixture.Assert(CountCompileRuntimeOperations(operationRoot) == 1, "independent process competition created more than one operation");

        var crashProof = fixture.IssueProof("process-crash");
        var readyPath = Path.Combine(fixture.Root, "crash.ready");
        var releasePath = Path.Combine(fixture.Root, "crash.release");
        var crashProcess = CrossProcessCompileHarness.Start("compile-hold", fixture.Root, crashProof.Compile.CompileProofId, Path.Combine(fixture.Root, "compiled", "process-crash"), readyPath, releasePath);
        try
        {
            CrossProcessCompileHarness.WaitForFile(readyPath, crashProcess);
            crashProcess.Kill(entireProcessTree: true);
            crashProcess.WaitForExit();
        }
        finally
        {
            if (!crashProcess.HasExited) crashProcess.Kill(entireProcessTree: true);
            crashProcess.Dispose();
        }

        var recovery = CrossProcessCompileHarness.Start("initialize", fixture.Root, crashProof.Compile.CompileProofId, Path.Combine(fixture.Root, "compiled", "process-crash"));
        var recoveryOutput = CrossProcessCompileHarness.ReadOutput(recovery);
        var recoveryExitCode = recovery.ExitCode;
        recovery.Dispose();
        AuthorityGateTestFixture.Assert(recoveryExitCode == 0 && recoveryOutput.Contains("INITIALIZED", StringComparison.Ordinal), "post-crash recovery child failed to initialize");
        var crashOperation = new AuthorityGateService(fixture.Workspace, fixture.Service);
        crashOperation.Initialize();
        var recovered = crashOperation.ReadOperation(CompileSettlementSupport.OperationId(crashProof.Compile.CompileProofId));
        AuthorityGateTestFixture.Assert(recovered["state"]?.GetValue<string>() is "failed_recovery" or "quarantined", "post-crash recovery did not reach a terminal state");
    }

    private static int CountCompileRuntimeOperations(string operationRoot)
        => Directory.EnumerateFiles(operationRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(AuthorityGateTestFixture.ReadJson)
            .Count(operation => operation["kind"]?.GetValue<string>() == CompileSettlementSupport.Kind);
}
