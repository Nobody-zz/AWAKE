using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class BatchAuthoringContractConstants
{
    public const string Format = "awake.worldbook.batch-authoring.contract-registry.v2";
    public const int Revision = 14;
    public const string SchemaVersion = "awake.worldbook.batch-authoring.contracts.v2";
    public const string ApprovedSha256 = "DF19CC670DB082BED8702C2CD95C60C22C91B5340A27D841D915514810BD8FFB";
}

internal sealed class BatchAuthoringContractRegistry
{
    private readonly JsonObject _document;
    private readonly Dictionary<string, JsonObject> _schemas;
    private readonly Dictionary<string, JsonObject> _routes;

    private BatchAuthoringContractRegistry(string sourcePath, string sha256, JsonObject document, Dictionary<string, JsonObject> schemas, Dictionary<string, JsonObject> routes)
    {
        SourcePath = sourcePath;
        Sha256 = sha256;
        _document = document;
        _schemas = schemas;
        _routes = routes;
    }

    public string SourcePath { get; }
    public string Sha256 { get; }
    public int Revision => _document["revision"]?.GetValue<int>() ?? 0;
    public string SchemaVersion => _document["schema_version"]?.GetValue<string>() ?? string.Empty;
    public JsonObject Document => (JsonObject)_document.DeepClone();

    public static BatchAuthoringContractRegistry Load(string path, string? expectedSha256 = BatchAuthoringContractConstants.ApprovedSha256)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("WB-BATCH-CONTRACT-400: contract 路径不能为空。");
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new InvalidOperationException($"WB-BATCH-CONTRACT-404: contract 文件不存在。{fullPath}");

        JsonObject document;
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
            document = JsonNode.Parse(bytes)?.AsObject() ?? throw new JsonException("root object required");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException($"WB-BATCH-CONTRACT-422: contract 无法读取或解析。{fullPath}", ex);
        }

        var hash = Hashing.Sha256Bytes(bytes);
        if (!string.IsNullOrWhiteSpace(expectedSha256) && !hash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"WB-BATCH-CONTRACT-409: contract hash 不匹配。{hash}");

        var schemas = ReadNamedObjects(document, "schemas", "WB-BATCH-CONTRACT-422");
        var routes = ReadNamedObjects(document["api"]?.AsObject(), "routes", "WB-BATCH-CONTRACT-422");
        ValidateHeader(document);
        ValidateSchemaIds(schemas);
        ValidateRoutes(routes, schemas, document["error_definitions"]?.AsObject());
        return new BatchAuthoringContractRegistry(fullPath, hash, document, schemas, routes);
    }

    public JsonObject RequireSchema(string name)
        => _schemas.TryGetValue(name, out var schema)
            ? (JsonObject)schema.DeepClone()
            : throw new InvalidOperationException($"WB-BATCH-CONTRACT-422: schema 不存在。{name}");

    public JsonObject RequireRoute(string name)
        => _routes.TryGetValue(name, out var route)
            ? (JsonObject)route.DeepClone()
            : throw new InvalidOperationException($"WB-BATCH-CONTRACT-422: route 不存在。{name}");

    public JsonObject RequireSchemaByReference(string reference)
        => _schemas.TryGetValue(reference, out var schema)
            ? (JsonObject)schema.DeepClone()
            : _schemas.Values.FirstOrDefault(value => value["schema_id"]?.GetValue<string>() == reference) is { } byId
                ? (JsonObject)byId.DeepClone()
                : throw new InvalidOperationException($"WB-BATCH-CONTRACT-422: schema 引用无法解析。{reference}");

    public void ValidatePayload(string schemaReference, JsonNode payload)
        => ValidatePayload(RequireSchemaByReference(schemaReference), payload, schemaReference);

    public bool HasSchema(string name) => _schemas.ContainsKey(name);
    public bool HasRoute(string name) => _routes.ContainsKey(name);

    private void ValidatePayload(JsonObject schema, JsonNode payload, string schemaReference)
    {
        if (schema["forbidden_fields"] is JsonArray forbidden && payload is JsonObject payloadObject)
        {
            foreach (var field in forbidden.Select(value => value?.GetValue<string>()).Where(value => !string.IsNullOrWhiteSpace(value)))
                if (payloadObject[field!] is not null)
                    throw new InvalidOperationException($"WB-BATCH-RESPONSE-500: {schemaReference} 暴露了禁止字段 {field}。");
        }

        if (payload is not JsonObject objectPayload) return;
        foreach (var required in schema["required"]?.AsArray()?.Select(value => value?.GetValue<string>()).Where(value => !string.IsNullOrWhiteSpace(value)) ?? [])
            if (!objectPayload.ContainsKey(required!))
                throw new InvalidOperationException($"WB-BATCH-RESPONSE-500: {schemaReference} 缺少必需字段 {required}。");

        if (schema["fields"] is not JsonObject fields) return;
        foreach (var field in fields)
        {
            var value = objectPayload[field.Key];
            if (value is null || field.Value is not JsonObject definition) continue;
            ValidateField(field.Key, definition, value, schemaReference);
        }
    }

    private void ValidateField(string fieldName, JsonObject definition, JsonNode value, string schemaReference)
    {
        if (definition["type"]?.GetValue<string>() is "const" && definition["value"] is not null
            && !JsonNode.DeepEquals(value, definition["value"]))
            throw new InvalidOperationException($"WB-BATCH-RESPONSE-500: {schemaReference}.{fieldName} 常量值不匹配。");

        if (definition["type"]?.GetValue<string>() is "enum" && definition["values"] is JsonArray values
            && !values.Any(candidate => JsonNode.DeepEquals(candidate, value)))
            throw new InvalidOperationException($"WB-BATCH-RESPONSE-500: {schemaReference}.{fieldName} 枚举值无效。");

        if (definition["$ref"]?.GetValue<string>() is { } reference && TryGetSchema(reference, out var referenced))
        {
            ValidatePayload(referenced, value, reference);
            return;
        }

        switch (definition["type"]?.GetValue<string>())
        {
            case "array" when value is JsonArray array && definition["items"] is JsonObject items:
                foreach (var child in array)
                    if (child is not null) ValidateField(fieldName + "[]", items, child, schemaReference);
                break;
            case "object" when value is JsonObject childObject:
                ValidatePayload(new JsonObject
                {
                    ["required"] = definition["required"]?.DeepClone() ?? new JsonArray(),
                    ["fields"] = definition["fields"]?.DeepClone() ?? new JsonObject(),
                    ["forbidden_fields"] = definition["forbidden_fields"]?.DeepClone() ?? new JsonArray()
                }, childObject, schemaReference + "." + fieldName);
                break;
        }
    }

    private bool TryGetSchema(string reference, out JsonObject schema)
    {
        if (_schemas.TryGetValue(reference, out schema!))
        {
            schema = (JsonObject)schema.DeepClone();
            return true;
        }
        var byId = _schemas.Values.FirstOrDefault(value => value["schema_id"]?.GetValue<string>() == reference);
        if (byId is null)
        {
            schema = null!;
            return false;
        }
        schema = (JsonObject)byId.DeepClone();
        return true;
    }

    private static void ValidateHeader(JsonObject document)
    {
        if (document["format"]?.GetValue<string>() != BatchAuthoringContractConstants.Format
            || document["revision"]?.GetValue<int>() != BatchAuthoringContractConstants.Revision
            || document["schema_version"]?.GetValue<string>() != BatchAuthoringContractConstants.SchemaVersion)
            throw new InvalidOperationException("WB-BATCH-CONTRACT-409: contract revision 或 schema version 不受支持。");
    }

    private static Dictionary<string, JsonObject> ReadNamedObjects(JsonNode? parent, string property, string code)
    {
        var value = parent?[property]?.AsObject() ?? throw new InvalidOperationException($"{code}: 缺少 {property}。");
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var entry in value)
        {
            if (entry.Value is not JsonObject obj) throw new InvalidOperationException($"{code}: {property}.{entry.Key} 必须是对象。");
            result.Add(entry.Key, obj);
        }
        return result;
    }

    private static void ValidateSchemaIds(Dictionary<string, JsonObject> schemas)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in schemas)
        {
            var id = entry.Value["schema_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                throw new InvalidOperationException($"WB-BATCH-CONTRACT-422: schema_id 缺失或重复。{entry.Key}");
            if (entry.Value["fields"] is not JsonObject || entry.Value["required"] is not JsonArray)
                throw new InvalidOperationException($"WB-BATCH-CONTRACT-422: schema 字段定义不完整。{entry.Key}");
        }
    }

    private static void ValidateRoutes(Dictionary<string, JsonObject> routes, Dictionary<string, JsonObject> schemas, JsonObject? errors)
    {
        if (errors is null) throw new InvalidOperationException("WB-BATCH-CONTRACT-422: 缺少 error_definitions。");
        var schemaIds = schemas.Values
            .Select(x => x["schema_id"]?.GetValue<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var route in routes)
        {
            var request = route.Value["request_ref"]?.GetValue<string>();
            var response = route.Value["response_ref"]?.GetValue<string>();
            if (!Resolves(request, schemas, schemaIds) || !Resolves(response, schemas, schemaIds))
                throw new InvalidOperationException($"WB-BATCH-CONTRACT-422: route schema 引用无法解析。{route.Key}");
            foreach (var error in route.Value["errors"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var code = error["code"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(code) || errors[code] is null)
                    throw new InvalidOperationException($"WB-BATCH-CONTRACT-422: route error 引用无法解析。{route.Key}");
            }
        }
    }

    private static bool Resolves(string? reference, Dictionary<string, JsonObject> schemas, HashSet<string> schemaIds)
        => !string.IsNullOrWhiteSpace(reference) && (schemas.ContainsKey(reference) || schemaIds.Contains(reference));
}
