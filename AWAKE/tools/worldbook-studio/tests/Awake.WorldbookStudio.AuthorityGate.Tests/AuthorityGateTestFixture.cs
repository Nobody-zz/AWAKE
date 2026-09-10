using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.AuthorityGate.Tests;

internal sealed class AuthorityGateTestFixture : IDisposable
{
    public AuthorityGateTestFixture(Action<CompileSettlementFaultPoint>? compileFaultInjector = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "awake-authority-gate-tests", Guid.NewGuid().ToString("N"));
        SchemaRoot = FindSchemaRoot();
        Workspace = new WorkspaceService(new WorkspaceOptions(Root, SchemaRoot));
        Service = new WorldbookApplicationService(Workspace);
        Authority = new AuthorityGateService(Workspace, Service, compileFaultInjector);
        Service.Initialize();
        Workspace.SeedSample(SchemaRoot);
        Authority.Initialize();
    }

    public string Root { get; }
    public string SchemaRoot { get; }
    public WorkspaceService Workspace { get; }
    public WorldbookApplicationService Service { get; }
    public AuthorityGateService Authority { get; }
    public string DocumentId => "doc.politics.demo";
    public string DocumentPath => "authoring/demo.yaml";

    public (AuthorityDocumentRevision Document, AuthoritySelectionSnapshot Selection, AuthorityApprovalProof Approval, AuthorityCompileProof Compile) IssueProof(string prefix = "test")
    {
        var document = Authority.RegisterDocument($"{prefix}.register", DocumentPath);
        var selection = Authority.MaterializeSelection($"{prefix}.selection", [document.DocumentId]);
        var approval = Authority.ApproveSelection($"{prefix}.approval", selection.SelectionId);
        var compile = Authority.IssueCompileProof($"{prefix}.compile", approval.ApprovalId, "base");
        return (document, selection, approval, compile);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch { }
    }

    internal static string FindSchemaRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "docs", "worldbook-studio-plan");
            if (File.Exists(Path.Combine(candidate, "awake.worldbook.authoring.v1.schema.json"))) return candidate;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("authority test schema root not found");
    }

    internal static JsonObject ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    internal static void WriteJson(string path, JsonObject value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, CanonicalJson.Serialize(value));
    }
    internal static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    internal static void AssertThrows(string code, Action action)
    {
        try { action(); }
        catch (InvalidOperationException error)
        {
            Assert(error.Message.StartsWith(code + ":", StringComparison.Ordinal), $"expected {code}, got {error.Message}");
            return;
        }
        throw new InvalidOperationException($"expected {code}");
    }
}
