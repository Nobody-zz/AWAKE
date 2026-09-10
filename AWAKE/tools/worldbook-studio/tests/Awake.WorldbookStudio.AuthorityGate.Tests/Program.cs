using System.Text.Json;
using Awake.WorldbookStudio.AuthorityGate.Tests;

if (args.Length > 0 && args[0] == "--compile-child")
    return CrossProcessCompileHarness.RunChild(args[1..]);

var cases = new List<string>();
try
{
    AuthorityGateHttpTests.Run(); cases.Add("http-route-matrix-zero-side-effect");
    AuthorityGateRecoveryTests.Run(); cases.Add("commit-marker-restart-recovery");
    AuthorityGateCompileProofTests.Run(); cases.Add("compile-proof-staging-pointer-cas");
    StorageIdentityCanonicalizerTests.Run(); cases.Add("storage-identity-canonicalizer-v1");
    CompileSettlementRecoveryTests.Run(); cases.Add("compile-settlement-crash-recovery");
    var evidencePath = Environment.GetEnvironmentVariable("AWAKE_WB_AUTHORITY_EVIDENCE_PATH");
    if (string.IsNullOrWhiteSpace(evidencePath)) evidencePath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "current-test", "evidence", "authority-gate.test.json");
    Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
    File.WriteAllText(evidencePath, JsonSerializer.Serialize(new { schema_version = "awake.worldbook.authority-gate-evidence.v1", authority_gate_passed = true, cases = cases.Select(id => new { id, passed = true }).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"PASS: AuthorityGate ({cases.Count}/{cases.Count})");
}
catch (Exception error)
{
    Console.Error.WriteLine($"FAIL: AuthorityGate: {error}");
    return 1;
}
return 0;
