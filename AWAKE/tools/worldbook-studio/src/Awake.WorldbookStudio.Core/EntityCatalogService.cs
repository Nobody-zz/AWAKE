using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed class EntityCatalogService
{
    private const string PointerFileName = "current-pointer.v1.json";
    private const string RegistryFileName = "entity-registry.v1.json";
    private const string ManifestFileName = "entity-registry-manifest.v1.json";
    private const string DiagnosticsFileName = "entity-registry-diagnostics.v1.json";

    private readonly WorkspaceService _workspace;
    private readonly SchemaValidator _schema;
    private readonly object _cacheGate = new();
    private string? _cacheKey;
    private EntityCatalogSnapshot? _cachedSnapshot;

    public EntityCatalogService(WorkspaceService workspace, SchemaValidator schema)
    {
        _workspace = workspace;
        _schema = schema;
    }

    public EntityCatalogSnapshot LoadAndValidate()
    {
        // 目录文件 600 KB 以上，逐次重读 + schema 校验 + 跨文件 hash 约 200-600 ms；仅在文件戳变化时重算。
        var cacheKey = TryComputeCacheKey();
        if (cacheKey is not null)
        {
            lock (_cacheGate)
            {
                if (_cachedSnapshot is not null && string.Equals(_cacheKey, cacheKey, StringComparison.Ordinal))
                    return _cachedSnapshot;
            }
        }

        var snapshot = LoadAndValidateFresh();
        if (cacheKey is not null && snapshot.Available)
        {
            lock (_cacheGate)
            {
                _cacheKey = cacheKey;
                _cachedSnapshot = snapshot;
            }
        }
        return snapshot;
    }

    private string? TryComputeCacheKey()
    {
        try
        {
            var catalogRoot = Path.GetFullPath(Path.Combine(_workspace.SchemaRoot, "mappings", "persona-entity"));
            var pointerPath = Path.Combine(catalogRoot, PointerFileName);
            if (!File.Exists(pointerPath)) return null;
            var relativeGeneration = Text(ReadObject(pointerPath)["generation_relative_path"]);
            if (string.IsNullOrWhiteSpace(relativeGeneration)) return null;
            var generationPath = Path.GetFullPath(Path.Combine(catalogRoot, relativeGeneration.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsInside(catalogRoot, generationPath)) return null;
            var paths = new[]
            {
                pointerPath,
                Path.Combine(generationPath, RegistryFileName),
                Path.Combine(generationPath, ManifestFileName),
                Path.Combine(generationPath, DiagnosticsFileName),
                Path.Combine(_workspace.SchemaRoot, "entity-registry-pointer.v1.schema.json"),
                Path.Combine(_workspace.SchemaRoot, "entity-registry.v1.schema.json"),
                Path.Combine(_workspace.SchemaRoot, "entity-registry-manifest.v1.schema.json"),
                Path.Combine(_workspace.SchemaRoot, "entity-registry-diagnostics.v1.schema.json")
            };
            var key = new StringBuilder();
            foreach (var path in paths)
            {
                if (!File.Exists(path)) return null;
                var info = new FileInfo(path);
                key.Append(info.FullName).Append('@').Append(info.LastWriteTimeUtc.Ticks).Append('#').Append(info.Length).Append(';');
            }
            return key.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private EntityCatalogSnapshot LoadAndValidateFresh()
    {
        var diagnostics = new List<Diagnostic>();
        var catalogRoot = Path.GetFullPath(Path.Combine(_workspace.SchemaRoot, "mappings", "persona-entity"));
        var pointerPath = Path.Combine(catalogRoot, PointerFileName);
        if (!File.Exists(pointerPath))
        {
            diagnostics.Add(new("WB-ENTITY-CATALOG-000", "warning", "人物和家族目录当前不可用，现有身份目录仍可正常使用。"));
            return EntityCatalogSnapshot.Unavailable(diagnostics);
        }

        JsonObject pointer;
        try
        {
            pointer = ReadObject(pointerPath);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new("WB-ENTITY-CATALOG-001", "warning", "人物和家族目录文件无法读取，现有身份目录仍可正常使用。", null, ex.Message));
            return EntityCatalogSnapshot.Unavailable(diagnostics);
        }

        var pointerReport = new ValidationReport();
        var pointerSchema = Path.Combine(_workspace.SchemaRoot, "entity-registry-pointer.v1.schema.json");
        if (File.Exists(pointerSchema)) _schema.Validate(pointer, pointerSchema, pointerReport);
        if (!pointerReport.Valid)
        {
            diagnostics.Add(new("WB-ENTITY-CATALOG-002", "warning", "人物和家族目录版本指针无效，现有身份目录仍可正常使用。"));
            return EntityCatalogSnapshot.Unavailable(diagnostics);
        }

        var buildId = Text(pointer["catalog_build_id"]);
        var relativeGeneration = Text(pointer["generation_relative_path"]);
        if (string.IsNullOrWhiteSpace(buildId) || string.IsNullOrWhiteSpace(relativeGeneration))
            return Unavailable(diagnostics, "WB-ENTITY-CATALOG-003", "人物和家族目录缺少版本信息，现有身份目录仍可正常使用。");

        var generationPath = Path.GetFullPath(Path.Combine(catalogRoot, relativeGeneration.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsInside(catalogRoot, generationPath))
            return Unavailable(diagnostics, "WB-ENTITY-CATALOG-004", "人物和家族目录路径无效，现有身份目录仍可正常使用。");

        var registryPath = Path.Combine(generationPath, RegistryFileName);
        var manifestPath = Path.Combine(generationPath, ManifestFileName);
        var diagnosticsPath = Path.Combine(generationPath, DiagnosticsFileName);
        if (!File.Exists(registryPath) || !File.Exists(manifestPath) || !File.Exists(diagnosticsPath))
            return Unavailable(diagnostics, "WB-ENTITY-CATALOG-005", "人物和家族目录缺少完整文件，现有身份目录仍可正常使用。");

        try
        {
            var registry = ReadObject(registryPath);
            var manifest = ReadObject(manifestPath);
            var diagnosticDocument = ReadObject(diagnosticsPath);
            var report = new ValidationReport();
            ValidateSchema(registry, Path.Combine(_workspace.SchemaRoot, "entity-registry.v1.schema.json"), report);
            ValidateSchema(manifest, Path.Combine(_workspace.SchemaRoot, "entity-registry-manifest.v1.schema.json"), report);
            ValidateSchema(diagnosticDocument, Path.Combine(_workspace.SchemaRoot, "entity-registry-diagnostics.v1.schema.json"), report);
            if (!report.Valid) return Unavailable(diagnostics, "WB-ENTITY-CATALOG-006", "人物和家族目录结构无效，现有身份目录仍可正常使用。");

            ValidateCrossFileHashes(pointer, registry, manifest, diagnosticDocument, registryPath, manifestPath, diagnosticsPath, buildId, report);
            if (!report.Valid) return Unavailable(diagnostics, "WB-ENTITY-CATALOG-007", "人物和家族目录校验失败，现有身份目录仍可正常使用。");

            if (diagnosticDocument["valid"]?.GetValue<bool>() == false)
                return Unavailable(diagnostics, "WB-ENTITY-CATALOG-008", "人物和家族目录包含无法隔离的结构错误，现有身份目录仍可正常使用。");

            var publicEntities = BuildPublicEntities(registry["entities"]?.AsArray() ?? []);
            var counts = registry["counts"]?.AsObject() is { } countObject ? countObject.DeepClone().AsObject() : new JsonObject();
            return new EntityCatalogSnapshot(true, buildId, publicEntities, counts, diagnostics);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new("WB-ENTITY-CATALOG-009", "warning", "人物和家族目录无法解析，现有身份目录仍可正常使用。", null, ex.Message));
            return EntityCatalogSnapshot.Unavailable(diagnostics);
        }
    }

    private void ValidateSchema(JsonObject document, string schemaPath, ValidationReport report)
    {
        if (File.Exists(schemaPath)) _schema.Validate(document, schemaPath, report);
        else report.Error("WB-ENTITY-CATALOG-SCHEMA", "人物和家族目录所需的契约文件缺失。", schemaPath);
    }

    private static void ValidateCrossFileHashes(
        JsonObject pointer,
        JsonObject registry,
        JsonObject manifest,
        JsonObject diagnosticDocument,
        string registryPath,
        string manifestPath,
        string diagnosticsPath,
        string buildId,
        ValidationReport report)
    {
        var registryHash = Hashing.FileSha256(registryPath);
        var manifestHash = Hashing.FileSha256(manifestPath);
        var diagnosticsHash = Hashing.FileSha256(diagnosticsPath);
        var expectedRegistryHash = Text(pointer["registry_sha256"]);
        var expectedManifestHash = Text(pointer["manifest_sha256"]);
        var expectedDiagnosticsHash = Text(pointer["diagnostics_sha256"]);
        if (!HashEquals(expectedRegistryHash, registryHash) || !HashEquals(expectedManifestHash, manifestHash) || !HashEquals(expectedDiagnosticsHash, diagnosticsHash))
            report.Error("WB-ENTITY-CATALOG-HASH", "人物和家族目录文件 hash 不一致。", buildId);
        if (!HashEquals(Text(manifest["registry_sha256"]), registryHash) || !HashEquals(Text(manifest["diagnostics_sha256"]), diagnosticsHash))
            report.Error("WB-ENTITY-CATALOG-MANIFEST", "人物和家族目录清单中的 hash 不一致。", buildId);
        if (!string.Equals(Text(pointer["catalog_build_id"]), Text(registry["catalog_build_id"]), StringComparison.Ordinal)
            || !string.Equals(buildId, Text(manifest["catalog_build_id"]), StringComparison.Ordinal)
            || !string.Equals(buildId, Text(diagnosticDocument["catalog_build_id"]), StringComparison.Ordinal))
            report.Error("WB-ENTITY-CATALOG-BUILD", "人物和家族目录版本号不一致。", buildId);
    }

    private static JsonArray BuildPublicEntities(JsonArray rawEntities)
    {
        var rawById = rawEntities
            .OfType<JsonObject>()
            .Where(x => !string.IsNullOrWhiteSpace(Text(x["entity_id"])))
            .ToDictionary(x => Text(x["entity_id"])!, StringComparer.Ordinal);
        var settlementNamesByCode = rawEntities
            .OfType<JsonObject>()
            .Where(x => string.Equals(Text(x["kind"]), "settlement", StringComparison.Ordinal))
            .Select(x => (Code: Text(x["settlement_code"]), Name: Text(x["display_name_zh"])))
            .Where(x => !string.IsNullOrWhiteSpace(x.Code) && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Code!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Name!, StringComparer.Ordinal);
        var duplicateNames = rawEntities
            .OfType<JsonObject>()
            .Select(x => Text(x["display_name_zh"]))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
        var result = new JsonArray();
        foreach (var raw in rawById.Values.OrderBy(x => Text(x["kind"]), StringComparer.Ordinal).ThenBy(x => Text(x["display_name_zh"]), StringComparer.Ordinal))
        {
            var kind = Text(raw["kind"]);
            var displayName = Text(raw["display_name_zh"]);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = Text(raw["clan_code"]) ?? Text(raw["hero_code"]) ?? Text(raw["settlement_code"]);
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(displayName)) continue;
            var item = new JsonObject
            {
                ["type"] = kind is "hero" or "clan" or "settlement" ? kind : "clan",
                ["displayName"] = displayName,
                ["worldSource"] = Text(raw["world_source"]) ?? "unknown",
                ["availability"] = Text(raw["runtime_availability"]) ?? "unknown",
                ["mappingStatus"] = Text(raw["mapping_status"]) ?? "needs_review"
            };
            if (duplicateNames.Contains(displayName)) item["duplicateName"] = true;
            if (kind == "hero")
            {
                var familyName = Text(raw["family_name_zh"]);
                if (!string.IsNullOrWhiteSpace(familyName)) item["familyName"] = familyName;
            }
            else if (kind == "settlement")
            {
                var settlementType = Text(raw["settlement_type"]);
                if (!string.IsNullOrWhiteSpace(settlementType)) item["settlementType"] = settlementType;
                var culture = Text(raw["culture_code"]);
                if (!string.IsNullOrWhiteSpace(culture)) item["culture"] = culture;
                var ownerClan = Text(raw["owner_clan_code"]);
                if (!string.IsNullOrWhiteSpace(ownerClan)) item["ownerClan"] = ownerClan;
                var kingdom = Text(raw["kingdom_code"]);
                if (!string.IsNullOrWhiteSpace(kingdom)) item["kingdom"] = kingdom;
                if (raw["pos_x"] is not null && raw["pos_y"] is not null)
                    item["position"] = new JsonObject { ["x"] = raw["pos_x"]!.DeepClone(), ["y"] = raw["pos_y"]!.DeepClone() };
                var boundCode = Text(raw["bound_settlement_code"]);
                if (!string.IsNullOrWhiteSpace(boundCode)) item["boundSettlement"] = settlementNamesByCode.TryGetValue(boundCode, out var boundName) ? boundName : boundCode;
                var villageType = Text(raw["village_type"]);
                if (!string.IsNullOrWhiteSpace(villageType)) item["villageType"] = villageType;
                if (raw["aliases"] is JsonArray aliases && aliases.Count > 0) item["aliases"] = aliases.DeepClone();
            }
            else
            {
                var members = new JsonArray();
                foreach (var memberId in raw["member_entity_ids"]?.AsArray().Select(Text).Where(x => !string.IsNullOrWhiteSpace(x)) ?? [])
                    if (rawById.TryGetValue(memberId!, out var member) && !string.IsNullOrWhiteSpace(Text(member["display_name_zh"]))) members.Add(Text(member["display_name_zh"]));
                item["members"] = members;
            }
            result.Add(item);
        }
        return result;
    }

    private static EntityCatalogSnapshot Unavailable(List<Diagnostic> diagnostics, string code, string message)
    {
        diagnostics.Add(new(code, "warning", message));
        return EntityCatalogSnapshot.Unavailable(diagnostics);
    }

    private static JsonObject ReadObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException("JSON 根节点不是对象。");

    private static string? Text(JsonNode? node) => node?.GetValue<string>();

    private static bool HashEquals(string? expected, string actual)
        => !string.IsNullOrWhiteSpace(expected) && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

    private static bool IsInside(string root, string candidate)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
