using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.AuthorityGate.Tests;

internal static class AuthorityGateCompileProofTests
{
    public static void Run()
    {
        using var fixture = new AuthorityGateTestFixture();
        var issued = fixture.IssueProof("compile");
        var duplicateProof = fixture.Authority.IssueCompileProof("compile.compile", issued.Approval.ApprovalId, "base");
        AuthorityGateTestFixture.Assert(duplicateProof.CompileProofId == issued.Compile.CompileProofId, "duplicate compile operation did not replay the original proof");
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-OPERATION-409", () => fixture.Authority.IssueCompileProof("compile.compile", issued.Approval.ApprovalId, "adult_optional"));
        var settlement = fixture.Authority.CompileApproved(issued.Compile.CompileProofId, outputRoot: Path.Combine(fixture.Root, "compiled", "approved"));
        AuthorityGateTestFixture.Assert(settlement.Result.Validation.Valid, "server-issued compile proof did not compile");
        AuthorityGateTestFixture.Assert(Directory.Exists(settlement.CompiledPath), "approved compile did not write the compiled package");
        var exportRoot = Path.Combine(fixture.Root, "export", "WorldbookV2");
        var staging = fixture.Authority.ExportStaging("compile.export", issued.Compile.CompileProofId, null, exportRoot);
        var duplicateStaging = fixture.Authority.ExportStaging("compile.export", issued.Compile.CompileProofId, null, exportRoot);
        AuthorityGateTestFixture.Assert(duplicateStaging == staging, "duplicate export operation did not replay the original staging path");
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-OPERATION-409", () => fixture.Authority.ExportStaging("compile.export", issued.Compile.CompileProofId, null, Path.Combine(fixture.Root, "export", "other")));
        AuthorityGateTestFixture.Assert(Directory.Exists(staging), "staging artifact was not created");
        AuthorityGateTestFixture.Assert(!File.Exists(Path.Combine(exportRoot, "current.json")), "staging export changed current pointer");
        var stagingManifestHash = Hashing.FileSha256(Path.Combine(staging, "manifest.json"));
        var pointerPath = fixture.Authority.PublishStaging("compile.publish", staging, stagingManifestHash, null);
        var duplicatePointerPath = fixture.Authority.PublishStaging("compile.publish", staging, stagingManifestHash, null);
        AuthorityGateTestFixture.Assert(pointerPath == duplicatePointerPath && File.Exists(pointerPath), "duplicate publish operation did not replay the committed pointer");
        AuthorityGateTestFixture.Assert(File.Exists(Path.Combine(fixture.Root, "authoring-v1", "publish-proofs", "compile.publish.json")), "publish proof was not persisted");

        var operationPath = Path.Combine(fixture.Root, "authoring-v1", "operations", StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.Operation, new Dictionary<string, object?> { ["operation_id"] = CompileSettlementSupport.OperationId(issued.Compile.CompileProofId) }) + ".json");
        var journalPath = Path.Combine(fixture.Root, "authoring-v1", "operation-journal.jsonl");
        var operationCountBeforeReplay = Directory.EnumerateFiles(Path.Combine(fixture.Root, "authoring-v1", "operations"), "*.json", SearchOption.TopDirectoryOnly).Count();
        var journalLengthBeforeReplay = File.ReadAllLines(journalPath).Length;
        File.AppendAllText(Path.Combine(fixture.Root, "authoring", "demo.yaml"), "\n");
        var replay = fixture.Authority.CompileApproved(issued.Compile.CompileProofId, outputRoot: Path.Combine(fixture.Root, "compiled", "approved"));
        AuthorityGateTestFixture.Assert(replay.PersistedEnvelope is not null, "compile replay did not load persisted result envelope");
        AuthorityGateTestFixture.Assert(replay.CompiledPath == settlement.CompiledPath, "compile replay changed the target path");
        AuthorityGateTestFixture.Assert(Directory.EnumerateFiles(Path.Combine(fixture.Root, "authoring-v1", "operations"), "*.json", SearchOption.TopDirectoryOnly).Count() == operationCountBeforeReplay, "compile replay created another operation");
        AuthorityGateTestFixture.Assert(File.ReadAllLines(journalPath).Length == journalLengthBeforeReplay, "compile replay appended another journal entry");
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-OPERATION-409", () => fixture.Authority.CompileApproved(issued.Compile.CompileProofId, outputRoot: Path.Combine(fixture.Root, "compiled", "other")));
        AuthorityGateTestFixture.Assert(File.Exists(operationPath), "compile operation record was not persisted");

        using var casFixture = new AuthorityGateTestFixture();
        var casIssued = casFixture.IssueProof("cas");
        File.AppendAllText(Path.Combine(casFixture.Root, "authoring", "demo.yaml"), "\n");
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-CAS-409", () => casFixture.Authority.CompileApproved(casIssued.Compile.CompileProofId));

        using var proofFixture = new AuthorityGateTestFixture();
        var proofIssued = proofFixture.IssueProof("tamper");
        var proofPath = Path.Combine(proofFixture.Root, "authoring-v1", "compile-proofs", StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.CompileProof, new Dictionary<string, object?> { ["compile_proof_id"] = proofIssued.Compile.CompileProofId }) + ".json");
        var tampered = AuthorityGateTestFixture.ReadJson(proofPath);
        tampered["content_tier"] = "adult_optional";
        AuthorityGateTestFixture.WriteJson(proofPath, tampered);
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-PROOF-409", () => proofFixture.Authority.CompileApproved(proofIssued.Compile.CompileProofId));

        using var approvalFixture = new AuthorityGateTestFixture();
        var approvalIssued = approvalFixture.IssueProof("approval-tamper");
        var approvalPath = Path.Combine(approvalFixture.Root, "authoring-v1", "approval-proofs", approvalIssued.Approval.ApprovalId + ".json");
        var approvalTampered = AuthorityGateTestFixture.ReadJson(approvalPath);
        approvalTampered["approved"] = false;
        AuthorityGateTestFixture.WriteJson(approvalPath, approvalTampered);
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-PROOF-409", () => approvalFixture.Authority.CompileApproved(approvalIssued.Compile.CompileProofId));

        var nonPublic = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        AuthorityGateTestFixture.Assert(!typeof(WorldbookApplicationService).GetMethod("Compile", nonPublic)!.IsPublic, "Compile must not remain public");
        AuthorityGateTestFixture.Assert(!typeof(WorldbookApplicationService).GetMethod("CompileExact", nonPublic)!.IsPublic, "CompileExact must not remain public");
        AuthorityGateTestFixture.Assert(!typeof(WorldbookApplicationService).GetMethod("Export", nonPublic)!.IsPublic, "Export must not remain public");
        AuthorityGateTestFixture.Assert(!typeof(WorldbookApplicationService).GetMethod("WriteCompiled", nonPublic)!.IsPublic, "WriteCompiled must not remain public");

        using var invalidFixture = new AuthorityGateTestFixture();
        var beforeInvalid = Directory.EnumerateFiles(invalidFixture.Root, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(invalidFixture.Root, path), Hashing.FileSha256, StringComparer.OrdinalIgnoreCase);
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-404", () => invalidFixture.Authority.CompileApproved("compile.missing"));
        var afterInvalid = Directory.EnumerateFiles(invalidFixture.Root, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(invalidFixture.Root, path), Hashing.FileSha256, StringComparer.OrdinalIgnoreCase);
        AuthorityGateTestFixture.Assert(beforeInvalid.SequenceEqual(afterInvalid), "invalid proof changed workspace state");

        using var pointerFixture = new AuthorityGateTestFixture();
        var pointerIssued = pointerFixture.IssueProof("pointer");
        var pointerExportRoot = Path.Combine(pointerFixture.Root, "export", "WorldbookV2");
        var pointerStaging = pointerFixture.Authority.ExportStaging("pointer.export", pointerIssued.Compile.CompileProofId, null, pointerExportRoot);
        var manifestHash = Hashing.FileSha256(Path.Combine(pointerStaging, "manifest.json"));
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-POINTER-409", () => pointerFixture.Authority.PublishStaging("pointer.publish", pointerStaging, manifestHash, "stale-pointer"));
    }
}
