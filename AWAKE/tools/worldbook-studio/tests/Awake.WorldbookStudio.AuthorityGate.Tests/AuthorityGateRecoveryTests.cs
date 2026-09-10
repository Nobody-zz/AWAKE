using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.AuthorityGate.Tests;

internal static class AuthorityGateRecoveryTests
{
    public static void Run()
    {
        using var fixture = new AuthorityGateTestFixture();
        var issued = fixture.IssueProof("recovery");
        var operations = Path.Combine(fixture.Root, "authoring-v1", "operations");
        foreach (var operationId in new[] { "recovery.register", "recovery.selection", "recovery.approval", "recovery.compile" })
        {
            var operationPath = Path.Combine(operations, operationId + ".json");
            var operation = AuthorityGateTestFixture.ReadJson(operationPath);
            operation["state"] = "prepared";
            AuthorityGateTestFixture.WriteJson(operationPath, operation);
        }
        var headPath = Path.Combine(fixture.Root, "authoring-v1", "workspace-head.json");
        var head = AuthorityGateTestFixture.ReadJson(headPath);
        head["documents"]!.AsObject().Remove(issued.Document.DocumentId);
        AuthorityGateTestFixture.WriteJson(headPath, head);
        new AuthorityGateService(fixture.Workspace, fixture.Service).Initialize();
        foreach (var operationId in new[] { "recovery.register", "recovery.selection", "recovery.approval", "recovery.compile" })
        {
            var recoveredOperation = fixture.Authority.ReadOperation(operationId);
            AuthorityGateTestFixture.Assert(recoveredOperation["state"]?.GetValue<string>() == "committed", $"prepared operation was not recovered: {operationId}; state={recoveredOperation["state"]}; detail={recoveredOperation["failure_detail"]}");
        }
        head = AuthorityGateTestFixture.ReadJson(headPath);
        AuthorityGateTestFixture.Assert(head["documents"]?[issued.Document.DocumentId] is not null, "workspace head did not recover commit marker");

        var exportRoot = Path.Combine(fixture.Root, "export", "WorldbookV2");
        var staging = fixture.Authority.ExportStaging("recovery.export", issued.Compile.CompileProofId, null, exportRoot);
        var manifestHash = Hashing.FileSha256(Path.Combine(staging, "manifest.json"));
        var pointerPath = fixture.Authority.PublishStaging("recovery.publish", staging, manifestHash, null);
        File.Delete(pointerPath);
        var publishOperationPath = Path.Combine(operations, "recovery.publish.json");
        var publishOperation = AuthorityGateTestFixture.ReadJson(publishOperationPath);
        publishOperation["state"] = "prepared";
        AuthorityGateTestFixture.WriteJson(publishOperationPath, publishOperation);
        new AuthorityGateService(fixture.Workspace, fixture.Service).Initialize();
        AuthorityGateTestFixture.Assert(File.Exists(pointerPath), "publish pointer was not recovered from durable proof and marker");
        AuthorityGateTestFixture.Assert(fixture.Authority.ReadOperation("recovery.publish")["state"]?.GetValue<string>() == "committed", "publish operation was not recovered");

        var failedId = "recovery.uncommitted";
        AuthorityGateTestFixture.WriteJson(Path.Combine(operations, failedId + ".json"), new JsonObject { ["operation_id"] = failedId, ["kind"] = "selection_snapshot", ["state"] = "prepared" });
        var pointerBeforeFailedRecovery = Hashing.FileSha256(pointerPath);
        new AuthorityGateService(fixture.Workspace, fixture.Service).Initialize();
        var failed = fixture.Authority.ReadOperation(failedId);
        AuthorityGateTestFixture.Assert(failed["state"]?.GetValue<string>() == "failed_recovery", "unmarked operation was not fail-closed");
        AuthorityGateTestFixture.Assert(Hashing.FileSha256(pointerPath) == pointerBeforeFailedRecovery, "failed recovery changed the current pointer");

        using var tamperFixture = new AuthorityGateTestFixture();
        var tamperIssued = tamperFixture.IssueProof("recovery-tamper");
        var tamperOutput = Path.Combine(tamperFixture.Root, "compiled", "tamper");
        tamperFixture.Authority.CompileApproved(tamperIssued.Compile.CompileProofId, outputRoot: tamperOutput);
        var tamperOperationId = CompileSettlementSupport.OperationId(tamperIssued.Compile.CompileProofId);
        var tamperMarkerPath = Path.Combine(tamperFixture.Root, "authoring-v1", "commit-markers", StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.CommitMarker, new Dictionary<string, object?> { ["operation_id"] = tamperOperationId }) + ".json");
        var tamperMarker = AuthorityGateTestFixture.ReadJson(tamperMarkerPath);
        tamperMarker["manifest_hash"] = "tampered";
        AuthorityGateTestFixture.WriteJson(tamperMarkerPath, tamperMarker);
        new AuthorityGateService(tamperFixture.Workspace, tamperFixture.Service).Initialize();
        var tamperedOperation = tamperFixture.Authority.ReadOperation(tamperOperationId);
        AuthorityGateTestFixture.Assert(tamperedOperation["state"]?.GetValue<string>() == "quarantined", $"tampered compile marker was adopted: state={tamperedOperation["state"]?.GetValue<string>()}");
        AuthorityGateTestFixture.Assert(!Directory.Exists(tamperOutput) || !Directory.EnumerateFileSystemEntries(tamperOutput).Any(), "tampered compile output remained consumer-visible");
        AuthorityGateTestFixture.Assert(Directory.Exists(Path.Combine(tamperFixture.Root, "compiled", "quarantine", tamperOperationId)), "tampered compile output was not quarantined");
    }
}
