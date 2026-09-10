using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public sealed record Diagnostic(string Code, string Severity, string Message, string? Path = null, string? Detail = null);

public sealed class ValidationReport
{
    public bool Valid => Diagnostics.All(x => x.Severity != "error");
    public List<Diagnostic> Diagnostics { get; } = [];
    public string? InputHash { get; set; }
    public string? ReportHash { get; set; }
    public string? ManifestHash { get; set; }

    public void Error(string code, string message, string? path = null, string? detail = null) => Diagnostics.Add(new(code, "error", message, path, detail));
    public void Warning(string code, string message, string? path = null, string? detail = null) => Diagnostics.Add(new(code, "warning", message, path, detail));
    public void Info(string code, string message, string? path = null, string? detail = null) => Diagnostics.Add(new(code, "info", message, path, detail));
}

public sealed record WorkspaceOptions(string Root, string SchemaRoot)
{
    public WorkspaceReadProbe? ReadProbe { get; init; }
}

public static class Hashing
{
    public static string Sha256Bytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string Sha256Text(string text) => Sha256Bytes(Encoding.UTF8.GetBytes(text));
    public static string FileSha256(string path) => Sha256Bytes(File.ReadAllBytes(path));
}

public static class CanonicalJson
{
    public static string Serialize(JsonNode node)
    {
        var canonical = Canonicalize(node);
        return canonical.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static JsonNode Canonicalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var property in obj.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                result[property.Key] = property.Value is null ? null : Canonicalize(property.Value);
            }
            return result;
        }

        if (node is JsonArray array)
        {
            var result = new JsonArray();
            foreach (var child in array) result.Add(child is null ? null : Canonicalize(child));
            return result;
        }

        return JsonNode.Parse(node.ToJsonString())!;
    }
    public static string Hash(JsonNode node) => Hashing.Sha256Text(Serialize(node));
}

