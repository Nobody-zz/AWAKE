using System.Text.Json;
using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.AuthorityGate.Tests;

internal static class AuthorityGateHttpTests
{
    public static void Run()
    {
        var root = FindStudioRoot();
        var matrix = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "contracts", "authoring-v1-legacy-route-matrix.json")))!.AsObject();
        var rows = matrix["rows"]!.AsArray().OfType<JsonObject>().ToArray();
        AuthorityGateTestFixture.Assert(rows.Length >= 27, "legacy route matrix is incomplete");
        AuthorityGateTestFixture.Assert(rows.Any(row => row["route"]?.GetValue<string>() == "/api/ai/batch/{batchId}/items/{itemId}/review"), "batch review route is missing");
        AuthorityGateTestFixture.Assert(!rows.Any(row => row["route"]?.GetValue<string>() == "/api/ai/batch/*"), "batch wildcard must not replace concrete routes");
        AuthorityGateTestFixture.Assert(AuthorityGateRoutes.IsRetired("POST", "/api/ai/batch/abc/start"), "batch route must be retired");
        AuthorityGateTestFixture.Assert(AuthorityGateRoutes.IsRetired("POST", "/api/ai/apply"), "AI apply route must be retired");
        AuthorityGateTestFixture.Assert(!AuthorityGateRoutes.IsRetired("POST", "/api/ai/authoring/selections"), "authoring-v1 route must remain available");

        using var fixture = new AuthorityGateTestFixture();
        var before = Snapshot(fixture.Root);
        AuthorityGateTestFixture.Assert(AuthorityGateRoutes.IsRetired("POST", "/api/ai/draft/generate"), "draft route must be retired before provider dispatch");
        var after = Snapshot(fixture.Root);
        AuthorityGateTestFixture.Assert(before.SequenceEqual(after), "retired route gate changed the workspace");
        var issued = fixture.IssueProof("http");
        AuthorityGateTestFixture.Assert(File.Exists(Path.Combine(fixture.Root, issued.Document.RecordPath.Replace('/', Path.DirectorySeparatorChar))), "document revision was not persisted");
        AuthorityGateTestFixture.Assert(File.Exists(Path.Combine(fixture.Root, "authoring-v1", "operation-journal.jsonl")), "operation journal was not persisted");
    }

    private static Dictionary<string, string> Snapshot(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(path => Path.GetRelativePath(root, path), Hashing.FileSha256, StringComparer.OrdinalIgnoreCase);

    private static string FindStudioRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "contracts", "authoring-v1-legacy-route-matrix.json"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("studio root not found");
    }
}
