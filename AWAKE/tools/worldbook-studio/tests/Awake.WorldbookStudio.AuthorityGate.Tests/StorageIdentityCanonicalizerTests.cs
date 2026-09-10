using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.AuthorityGate.Tests;

internal static class StorageIdentityCanonicalizerTests
{
    public static void Run()
    {
        var identity = new Dictionary<string, object?>
        {
            ["compile_proof_id"] = "compile/proof..1",
            ["revision"] = 2
        };
        var bytes = StorageIdentityCanonicalizerV1.CanonicalBytes(StorageRecordFamily.CompileProof, identity);
        AuthorityGateTestFixture.Assert(System.Text.Encoding.UTF8.GetString(bytes) == "{\"kind\":\"compile_proof\",\"identity\":{\"compile_proof_id\":\"compile/proof..1\",\"revision\":2}}", "storage canonical bytes changed");
        AuthorityGateTestFixture.Assert(StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.CompileProof, identity) == "k1_cp_86aafb1afd70fdb89151eedec38ea3aa7d3648c88a19a2033ec31e1e5266d93e", "storage key golden changed");
        AuthorityGateTestFixture.Assert(StorageIdentityCanonicalizerV1.OldSafeIdV0("a/b\\c..d") == "a_b_c_d", "old SafeId v0 changed");
        var composed = StorageIdentityCanonicalizerV1.CanonicalBytes(StorageRecordFamily.Operation, new Dictionary<string, object?> { ["operation_id"] = "é" });
        var decomposed = StorageIdentityCanonicalizerV1.CanonicalBytes(StorageRecordFamily.Operation, new Dictionary<string, object?> { ["operation_id"] = "e\u0301" });
        AuthorityGateTestFixture.Assert(composed.SequenceEqual(decomposed), "storage canonicalizer did not normalize NFC");
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-SAFEID-422", () => StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.Operation, new Dictionary<string, object?> { ["operation_id"] = "bad\u0001" }));
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-SAFEID-422", () => StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.Operation, new Dictionary<string, object?> { ["operation_id"] = null }));

        using var legacyFixture = new AuthorityGateTestFixture();
        var issued = legacyFixture.IssueProof("legacy-proof");
        var strictPath = Path.Combine(legacyFixture.Root, "authoring-v1", "compile-proofs", StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.CompileProof, new Dictionary<string, object?> { ["compile_proof_id"] = issued.Compile.CompileProofId }) + ".json");
        var legacyPath = Path.Combine(legacyFixture.Root, "authoring-v1", "compile-proofs", StorageIdentityCanonicalizerV1.OldSafeIdV0(issued.Compile.CompileProofId) + ".json");
        File.Move(strictPath, legacyPath);
        var legacySettlement = legacyFixture.Authority.CompileApproved(issued.Compile.CompileProofId, outputRoot: Path.Combine(legacyFixture.Root, "compiled", "legacy-proof"));
        AuthorityGateTestFixture.Assert(Directory.Exists(legacySettlement.CompiledPath), "legacy CompileProof was not replayable");

        using var conflictFixture = new AuthorityGateTestFixture();
        var conflictIssued = conflictFixture.IssueProof("legacy-conflict");
        var conflictStrictPath = Path.Combine(conflictFixture.Root, "authoring-v1", "compile-proofs", StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.CompileProof, new Dictionary<string, object?> { ["compile_proof_id"] = conflictIssued.Compile.CompileProofId }) + ".json");
        var conflictLegacyPath = Path.Combine(conflictFixture.Root, "authoring-v1", "compile-proofs", StorageIdentityCanonicalizerV1.OldSafeIdV0(conflictIssued.Compile.CompileProofId) + ".json");
        File.Copy(conflictStrictPath, conflictLegacyPath);
        File.AppendAllText(conflictLegacyPath, " ");
        var beforeConflict = Directory.EnumerateFiles(conflictFixture.Root, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(conflictFixture.Root, path), Hashing.FileSha256, StringComparer.OrdinalIgnoreCase);
        AuthorityGateTestFixture.AssertThrows("WB-AUTHORITY-SAFEID-409", () => conflictFixture.Authority.CompileApproved(conflictIssued.Compile.CompileProofId));
        var afterConflict = Directory.EnumerateFiles(conflictFixture.Root, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(conflictFixture.Root, path), Hashing.FileSha256, StringComparer.OrdinalIgnoreCase);
        AuthorityGateTestFixture.Assert(beforeConflict.SequenceEqual(afterConflict), "strict/legacy conflict mutated workspace");
    }
}
