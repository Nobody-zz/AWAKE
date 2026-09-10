using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class CompileSettlementSupport
{
    public const string SchemaVersion = "awake.worldbook.compile-operation.v1";
    public const string Kind = "compile_runtime";
    public const string ResultSchemaVersion = "awake.worldbook.compile-result.v1";
    public const string IntegrityAlgorithm = "HMAC-SHA256";

    public static string OperationId(string proofId)
    {
        var value = Encoding.UTF8.GetBytes("awake.worldbook.compile.operation.v1\0" + proofId.Normalize(NormalizationForm.FormC));
        return "customer.compile.v1." + Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    }

    public static string RequestDigest(string workspaceIdentity, string proofId, JsonObject proof, string outputRoot, string? confirmationToken)
        => CanonicalJson.Hash(new JsonObject
        {
            ["schema_version"] = "awake.worldbook.compile-request.v1",
            ["workspace_identity"] = Normalize(workspaceIdentity),
            ["proof_id"] = Normalize(proofId),
            ["proof_hash"] = NormalizeHash(proof["proof_hash"]?.GetValue<string>()),
            ["selection_id"] = Normalize(proof["selection_id"]?.GetValue<string>()),
            ["selection_hash"] = NormalizeHash(proof["selection_hash"]?.GetValue<string>()),
            ["content_tier"] = Normalize(proof["content_tier"]?.GetValue<string>()),
            ["output_root"] = Normalize(outputRoot).ToLowerInvariant(),
            ["confirmation_token_hash"] = Hashing.Sha256Text(Normalize(confirmationToken)).ToLowerInvariant()
        });

    public static JsonObject ResultEnvelope(AuthorityCompileSettlement settlement, string operationId, string requestDigest, string workspaceIdentity, string proofId, string proofHash, string contentTier, string outputRoot, string fenceToken)
    {
        var manifestPath = Path.Combine(settlement.CompiledPath, "manifest.json");
        if (!File.Exists(manifestPath)) throw new InvalidOperationException("WB-AUTHORITY-COMPILE-409: compiled manifest 缺失。");
        var manifestHash = Hashing.FileSha256(manifestPath).ToLowerInvariant();
        var validation = new JsonArray(settlement.Result.Validation.Diagnostics.Select(item => new JsonObject
        {
            ["code"] = item.Code,
            ["severity"] = item.Severity,
            ["message"] = item.Message,
            ["path"] = item.Path,
            ["detail"] = item.Detail
        }).ToArray());
        var fileHashes = new JsonObject();
        foreach (var fileName in settlement.Result.Files.Keys.OrderBy(x => x, StringComparer.Ordinal))
            fileHashes[fileName] = Hashing.Sha256Bytes(settlement.Result.Files[fileName]).ToLowerInvariant();
        var artifactHashes = new JsonObject();
        foreach (var path in Directory.EnumerateFiles(settlement.CompiledPath, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetRelativePath(settlement.CompiledPath, path).Replace((char)92, '/');
            artifactHashes[fileName] = Hashing.FileSha256(path).ToLowerInvariant();
        }
        var result = new JsonObject
        {
            ["schema_version"] = ResultSchemaVersion,
            ["operation_id"] = operationId,
            ["request_digest"] = requestDigest,
            ["workspace_identity"] = workspaceIdentity,
            ["proof_id"] = proofId,
            ["proof_hash"] = proofHash,
            ["content_tier"] = contentTier,
            ["output_root"] = outputRoot,
            ["fence_token"] = fenceToken,
            ["compiled_path"] = ".",
            ["manifest_hash"] = manifestHash,
            ["manifest"] = settlement.Result.Manifest?.DeepClone(),
            ["confirmation_token"] = settlement.Result.ConfirmationToken,
            ["validation"] = validation,
            ["files"] = new JsonArray(settlement.Result.Files.Keys.OrderBy(x => x, StringComparer.Ordinal).Select(fileName => JsonValue.Create(fileName)).ToArray()),
            ["file_hashes"] = fileHashes,
            ["artifact_hashes"] = artifactHashes
        };
        result["result_hash"] = CanonicalJson.Hash(result.DeepClone().AsObject());
        return result;
    }

    public static CompileResult LoadResult(JsonObject result, string compiledPath)
    {
        var report = new ValidationReport();
        if (result["validation"] is JsonArray diagnostics)
        {
            foreach (var item in diagnostics.OfType<JsonObject>())
            {
                var diagnostic = new Diagnostic(
                    item["code"]?.GetValue<string>() ?? "WB-COMPILE-RESULT-422",
                    item["severity"]?.GetValue<string>() ?? "error",
                    item["message"]?.GetValue<string>() ?? "编译结果无效。",
                    item["path"]?.GetValue<string>(),
                    item["detail"]?.GetValue<string>());
                report.Diagnostics.Add(diagnostic);
            }
        }
        if (result["schema_version"]?.GetValue<string>() != ResultSchemaVersion) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result schema 无效。");
        var resultHash = result["result_hash"]?.GetValue<string>();
        var resultHashInput = result.DeepClone().AsObject();
        resultHashInput.Remove("result_hash");
        if (string.IsNullOrWhiteSpace(resultHash) || CanonicalJson.Hash(resultHashInput) != resultHash)
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result integrity 无效。");
        report.ManifestHash = result["manifest_hash"]?.GetValue<string>();
        var compiled = new CompileResult(report)
        {
            ManifestHash = result["manifest_hash"]?.GetValue<string>(),
            Manifest = result["manifest"]?.DeepClone()?.AsObject(),
            ConfirmationToken = result["confirmation_token"]?.GetValue<string>()
        };
        var fileNames = result["files"]?.AsArray().Select(x => x?.GetValue<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray() ?? [];
        var fileHashes = result["file_hashes"]?.AsObject() ?? throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result 缺少文件哈希。");
        var artifactHashes = result["artifact_hashes"]?.AsObject() ?? throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result 缺少产物哈希。");
        if (fileNames.Distinct(StringComparer.Ordinal).Count() != fileNames.Length || fileHashes.Count != fileNames.Length)
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result 文件集合无效。");
        foreach (var fileName in fileNames)
        {
            if (Path.IsPathRooted(fileName) || fileName.Contains("..", StringComparison.Ordinal) || fileName.Contains('\\'))
                throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result 文件路径无效。");
            var path = Path.GetFullPath(Path.Combine(compiledPath, fileName.Replace('/', Path.DirectorySeparatorChar)));
            if (!WorkspaceRootGuard.IsSameOrInside(path, compiledPath)) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result 文件路径越界。");
            if (!File.Exists(path)) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: committed compile artifact is incomplete.");
            var bytes = File.ReadAllBytes(path);
            if (fileHashes[fileName]?.GetValue<string>() is not { } expectedHash || !string.Equals(Hashing.Sha256Bytes(bytes), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: committed compile artifact hash mismatch.");
            compiled.Files[fileName] = bytes;
        }
        var actualArtifactPaths = Directory.EnumerateFiles(compiledPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(compiledPath, path).Replace((char)92, '/'))
            .ToHashSet(StringComparer.Ordinal);
        if (!actualArtifactPaths.SetEquals(artifactHashes.Select(item => item.Key)))
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compiled artifact 文件集合不匹配。");
        foreach (var item in artifactHashes)
        {
            var fileName = item.Key;
            if (Path.IsPathRooted(fileName) || fileName.Contains("..", StringComparison.Ordinal) || fileName.Contains((char)92))
                throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compiled artifact 文件路径无效。");
            var path = Path.Combine(compiledPath, fileName.Replace('/', Path.DirectorySeparatorChar));
            if (item.Value?.GetValue<string>() is not { } expectedHash || !string.Equals(Hashing.FileSha256(path), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compiled artifact hash mismatch。");
        }
        return compiled;
    }

    public static JsonObject PublicProjection(JsonObject envelope)
    {
        var projection = new JsonObject();
        foreach (var key in new[] { "schema_version", "operation_id", "request_digest", "workspace_identity", "proof_id", "proof_hash", "content_tier", "output_root", "compiled_path", "manifest_hash", "manifest", "validation", "files", "file_hashes", "artifact_hashes", "result_hash" })
        {
            if (envelope[key] is JsonNode value) projection[key] = PublicNode(value);
        }
        return projection;
    }

    public static JsonObject PublicOperationProjection(JsonObject operation)
    {
        var projection = new JsonObject();
        foreach (var key in new[] { "schema_version", "operation_id", "kind", "state", "request_digest", "proof_id", "proof_hash", "content_tier", "output_root", "manifest_hash", "result_hash", "failure_code", "created_at", "prepared_at", "committed_at" })
        {
            if (operation[key] is JsonNode value) projection[key] = PublicNode(value);
        }
        return projection;
    }

    private static JsonNode PublicNode(JsonNode value)
    {
        // 校验诊断的 path/detail 允许为空，序列化后就是 JSON null；
        // 直接下探会在投影阶段抛 NullReferenceException，把「编译其实已提交」变成不透明的 500。
        if (value is null) return null!;
        if (value is JsonObject obj)
        {
            var projected = new JsonObject();
            foreach (var property in obj)
            {
                if (IsSensitiveName(property.Key)) continue;
                projected[property.Key] = PublicNode(property.Value!);
            }
            return projected;
        }
        if (value is JsonArray array)
        {
            var projected = new JsonArray();
            foreach (var item in array)
                if (item is not null) projected.Add(PublicNode(item));
            return projected;
        }
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text) && Path.IsPathRooted(text))
            return JsonValue.Create(".")!;
        return value.DeepClone();
    }

    private static bool IsSensitiveName(string name)
        => name.Contains("token", StringComparison.OrdinalIgnoreCase)
            || name.Contains("fence", StringComparison.OrdinalIgnoreCase)
            || name.Equals("owner_instance_id", StringComparison.OrdinalIgnoreCase)
            || name.Contains("absolute_path", StringComparison.OrdinalIgnoreCase)
            || name.Contains("internal_path", StringComparison.OrdinalIgnoreCase);

    public static string GetOrCreateIntegrityKey()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local)) throw new InvalidOperationException("WB-AUTHORITY-RECOVERY-409: 本机完整性密钥不可用。");
        var path = Path.Combine(local, "AWAKE", "WorldbookStudio", "authority-v1.key.dpapi");
        if (File.Exists(path))
        {
            var protectedValue = File.ReadAllText(path, Encoding.UTF8).Trim();
            if (!WindowsDpapiSecretProtector.TryUnprotect(protectedValue, out var key) || !IsValidIntegrityKey(key))
                throw new InvalidOperationException("WB-AUTHORITY-RECOVERY-409: 本机完整性密钥无法解封。");
            return key;
        }
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        if (!WindowsDpapiSecretProtector.TryProtect(raw, out var protectedKey))
            throw new InvalidOperationException("WB-AUTHORITY-RECOVERY-409: 本机完整性密钥无法保护。");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(protectedKey);
            writer.Flush();
            stream.Flush(true);
        }
        catch (IOException) when (File.Exists(path))
        {
            var existing = File.ReadAllText(path, Encoding.UTF8).Trim();
            if (!WindowsDpapiSecretProtector.TryUnprotect(existing, out var existingKey) || !IsValidIntegrityKey(existingKey))
                throw new InvalidOperationException("WB-AUTHORITY-RECOVERY-409: 本机完整性密钥无法解封。");
            return existingKey;
        }
        return raw;
    }

    public static string IntegrityTag(JsonObject marker)
    {
        var key = Convert.FromBase64String(GetOrCreateIntegrityKey());
        var payload = Encoding.UTF8.GetBytes(CanonicalJson.Serialize(marker));
        return Convert.ToHexString(HMACSHA256.HashData(key, payload)).ToLowerInvariant();
    }

    public static bool VerifyIntegrityTag(JsonObject marker)
    {
        try
        {
            var supplied = marker["integrity_tag"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(supplied)) return false;
            var clone = marker.DeepClone().AsObject();
            clone.Remove("integrity_tag");
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local)) return false;
            var path = Path.Combine(local, "AWAKE", "WorldbookStudio", "authority-v1.key.dpapi");
            if (!File.Exists(path)) return false;
            var protectedValue = File.ReadAllText(path, Encoding.UTF8).Trim();
            if (!WindowsDpapiSecretProtector.TryUnprotect(protectedValue, out var rawKey) || !IsValidIntegrityKey(rawKey)) return false;
            var expected = Convert.ToHexString(HMACSHA256.HashData(Convert.FromBase64String(rawKey), Encoding.UTF8.GetBytes(CanonicalJson.Serialize(clone)))).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(supplied), Convert.FromHexString(expected));
        }
        catch (FormatException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (JsonException) { return false; }
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Normalize(NormalizationForm.FormC);
    private static string NormalizeHash(string? value) => Normalize(value).ToLowerInvariant();
    private static bool IsValidIntegrityKey(string? value)
    {
        try { return !string.IsNullOrWhiteSpace(value) && Convert.FromBase64String(value).Length == 32; }
        catch (FormatException) { return false; }
    }
}
