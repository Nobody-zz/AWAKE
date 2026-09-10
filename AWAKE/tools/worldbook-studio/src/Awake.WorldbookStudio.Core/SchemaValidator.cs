using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Awake.WorldbookStudio.Core;

public sealed class SchemaValidator
{
    private static readonly ConcurrentDictionary<string, JsonSchema> Cache = new(StringComparer.OrdinalIgnoreCase);

    public void Validate(JsonObject document, string schemaPath, ValidationReport report, SnapshotInputStore? snapshot = null)
    {
        try
        {
            var fullPath = Path.GetFullPath(schemaPath);
            var schema = snapshot is null ? Cache.GetOrAdd(fullPath, LoadSchema) : LoadSchema(fullPath, snapshot);
            using var parsed = JsonDocument.Parse(document.ToJsonString());
            var result = schema.Evaluate(parsed.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (result.IsValid) return;
            report.Error("WB-SCHEMA-001", "Schema 校验失败。", result.InstanceLocation.ToString(), "Schema evaluation failed.");
            foreach (var detail in result.Details ?? [])
            {
                if (detail.IsValid) continue;
                report.Error("WB-SCHEMA-001", "Schema 子项校验失败。", detail.InstanceLocation.ToString(), detail.Errors is null ? detail.InstanceLocation.ToString() : string.Join("; ", detail.Errors.Select(x => $"{x.Key}:{x.Value}")));
            }
        }
        catch (Exception ex)
        {
            report.Error("WB-SCHEMA-000", "Schema 加载或执行失败。", schemaPath, ex.Message);
        }
    }

    private static JsonSchema LoadSchema(string path)
        => LoadSchema(path, null);

    private static JsonSchema LoadSchema(string path, SnapshotInputStore? snapshot)
    {
        var schemaText = snapshot?.GetText(path) ?? File.ReadAllText(path);
        var registry = new SchemaRegistry();
        var options = BuildOptions.Default;
        options.SchemaRegistry = registry;
        registry.Fetch = (uri, _) => FetchLocalSchema(uri, options, Path.GetDirectoryName(path)!, snapshot);
        return JsonSchema.FromText(schemaText, options, new Uri(path));
    }

    private static IBaseDocument? FetchLocalSchema(Uri uri, BuildOptions options, string schemaDirectory, SnapshotInputStore? snapshot)
    {
        var candidateUri = uri;
        var text = uri.ToString();
        var encodedFragment = text.IndexOf("%23", StringComparison.OrdinalIgnoreCase);
        if (encodedFragment >= 0) candidateUri = new Uri(text[..encodedFragment]);
        var candidate = candidateUri.IsFile ? candidateUri.LocalPath : Path.Combine(schemaDirectory, Uri.UnescapeDataString(candidateUri.OriginalString));
        if (!File.Exists(candidate)) return null;
        return JsonSchema.FromText(snapshot?.GetText(candidate) ?? File.ReadAllText(candidate), options, new Uri(candidate));
    }
}


