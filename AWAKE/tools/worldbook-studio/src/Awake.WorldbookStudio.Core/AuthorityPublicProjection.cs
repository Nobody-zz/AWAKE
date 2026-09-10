using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class AuthorityPublicProjection
{
    public static JsonObject Document(AuthorityDocumentRevision value, string workspaceRoot)
        => new()
        {
            ["documentId"] = value.DocumentId,
            ["path"] = SafePathOrOpaqueReference(workspaceRoot, value.Path, "document"),
            ["revision"] = value.Revision,
            ["contentHash"] = value.ContentHash
        };

    public static JsonObject Selection(AuthoritySelectionSnapshot value)
        => new()
        {
            ["selectionId"] = value.SelectionId,
            ["selectionHash"] = value.SelectionHash,
            ["itemCount"] = value.ItemCount
        };

    public static JsonObject Approval(AuthorityApprovalProof value)
        => new()
        {
            ["approvalId"] = value.ApprovalId,
            ["proofHash"] = value.ProofHash,
            ["selectionId"] = value.SelectionId
        };

    public static JsonObject CompileProof(AuthorityCompileProof value)
        => new()
        {
            ["compileProofId"] = value.CompileProofId,
            ["proofHash"] = value.ProofHash,
            ["contentTier"] = value.ContentTier,
            ["itemCount"] = value.ItemCount
        };

    public static JsonObject Artifact(string propertyName, string path, string workspaceRoot)
        => new()
        {
            [propertyName] = SafePathOrOpaqueReference(workspaceRoot, path, propertyName)
        };

    private static string SafePathOrOpaqueReference(string workspaceRoot, string path, string purpose)
    {
        var fullRoot = Path.GetFullPath(workspaceRoot);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (!Path.IsPathRooted(relative)
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            return relative.Replace('\\', '/');

        var normalized = fullPath.Replace('\\', '/').Normalize(NormalizationForm.FormC);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return $"opaque:{purpose}:{digest}";
    }
}