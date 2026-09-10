using System.Text.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public enum PublishFaultPoint
{
    None,
    WriteInterruption,
    DiskFullSimulation,
    FileLock,
    LeaseConflict,
    PointerReplaceFailure
}

internal static class CompiledPackageGuard
{
    public static void Ensure(CompileResult compiled, string operation)
    {
        if (compiled.Snapshot is null) throw new InvalidOperationException($"{operation}-003: 编译结果缺少输入快照。");
        compiled.Snapshot.VerifyForOutput();

        var manifest = compiled.Manifest ?? throw new InvalidOperationException($"{operation}-003: 编译结果缺少 manifest。");
        if (!compiled.Files.TryGetValue("runtime.json", out var runtimeBytes) || runtimeBytes is null || runtimeBytes.Length == 0)
            throw new InvalidOperationException($"{operation}-004: 编译结果缺少 runtime.json。");
        if (!compiled.Files.TryGetValue("index.json", out var indexBytes) || indexBytes is null || indexBytes.Length == 0)
            throw new InvalidOperationException($"{operation}-004: 编译结果缺少 index.json。");

        var runtime = ParseJson(runtimeBytes, "runtime.json", operation);
        var index = ParseJson(indexBytes, "index.json", operation);
        var hashes = manifest["hashes"] as JsonObject;
        var declaredContentHash = StringValue(hashes?["contentHash"]);
        var declaredManifestHash = StringValue(hashes?["manifestHash"]);
        var declaredPackageHash = StringValue(hashes?["packageHash"]);
        var actualContentHash = ContractHashing.ContentHash(new[] { ("runtime.json", runtime), ("index.json", index) });
        if (!string.Equals(declaredContentHash, actualContentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{operation}-004: 编译结果 contentHash 与 runtime/index 内容不一致。");

        var actualManifestHash = ContractHashing.ManifestHash(manifest);
        if (!string.Equals(declaredManifestHash, actualManifestHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(compiled.ManifestHash, actualManifestHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{operation}-004: 编译结果 manifestHash 不一致。");

        var actualPackageHash = ContractHashing.PackageHash(actualManifestHash, actualContentHash);
        if (!string.Equals(declaredPackageHash, actualPackageHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{operation}-004: 编译结果 packageHash 不一致。");

        if (compiled.Files.TryGetValue("package-manifest.json", out var packageManifestBytes) && packageManifestBytes is not null && packageManifestBytes.Length > 0)
        {
            var packageManifest = ParseJson(packageManifestBytes, "package-manifest.json", operation);
            if (!string.Equals(CanonicalJson.Serialize(packageManifest), CanonicalJson.Serialize(manifest), StringComparison.Ordinal))
                throw new InvalidOperationException($"{operation}-004: package-manifest.json 与 manifest.json 不一致。");
        }
    }

    private static JsonNode ParseJson(byte[] bytes, string name, string operation)
    {
        try { return JsonNode.Parse(bytes) ?? throw new InvalidOperationException(); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException($"{operation}-004: 编译结果 {name} 无法解析。", ex);
        }
    }

    private static string? StringValue(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
public sealed class AtomicCandidatePublisher
{
    private readonly WorkspaceService _workspace;
    private readonly SchemaValidator _schema = new();
    public AtomicCandidatePublisher(WorkspaceService workspace) => _workspace = workspace;

    public static string ExpectedAdultConfirmationToken(CompileResult compiled)
        => compiled.ConfirmationToken ?? Hashing.Sha256Text($"{compiled.ManifestHash}|adult_optional");

    public string Publish(CompileResult compiled, string contentTier, string? confirmationToken = null, string? outputRoot = null, PublishFaultPoint faultPoint = PublishFaultPoint.None, bool updatePointer = true)
    {
        if (compiled is null || !compiled.Validation.Valid) throw new InvalidOperationException("WB-COMPILE-001: 校验失败，不能发布。");
        if (contentTier is not ("base" or "adult_optional")) throw new InvalidOperationException("WB-TIER-002: content tier 无效。");
        if (contentTier == "adult_optional" && confirmationToken != ExpectedAdultConfirmationToken(compiled)) throw new InvalidOperationException("WB-CONFIRM-001: adult_optional 确认 token 与当前输入不匹配。");
        if (compiled.Manifest is null) throw new InvalidOperationException("WB-PUBLISH-002: 编译结果缺少 manifest。");
        CompiledPackageGuard.Ensure(compiled, "WB-PUBLISH");

        var manifest = JsonNode.Parse(compiled.Manifest.ToJsonString())!.AsObject();
        manifest["content_tier"] = contentTier;
        manifest["confirmation_token_hash"] = contentTier == "adult_optional" ? Hashing.Sha256Text(confirmationToken!) : null;
        var contentHash = manifest["hashes"]?["contentHash"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(contentHash)) throw new InvalidOperationException("WB-PUBLISH-003: 编译结果缺少 contentHash。");
        var manifestHash = ContractHashing.ManifestHash(manifest);
        var packageHash = ContractHashing.PackageHash(manifestHash, contentHash);
        manifest["hashes"] = new JsonObject
        {
            ["manifestHash"] = manifestHash,
            ["contentHash"] = contentHash,
            ["packageHash"] = packageHash
        };
        var manifestBytes = Encoding.UTF8.GetBytes(CanonicalJson.Serialize(manifest));

        var exportRoot = _workspace.Policy.RequireExport(outputRoot ?? Path.Combine(_workspace.Root, "export", "WorldbookV2"));
        Directory.CreateDirectory(exportRoot);
        var lockPath = Path.Combine(exportRoot, ".publish.lock");
        FileStream? lease = null;
        var ownsLease = false;
        string? tempPath = null;
        string? pointerTemp = null;
        try
        {
            if (faultPoint == PublishFaultPoint.LeaseConflict) throw new InvalidOperationException("WB-PUBLISH-LOCK: 发布 lease 被占用。");
            lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            ownsLease = true;
            if (faultPoint == PublishFaultPoint.FileLock) throw new IOException("WB-PUBLISH-FILE-LOCK: 模拟候选文件占用。");

            var baseName = $"WorldbookV2-{manifestHash}";
            var candidateName = baseName;
            var suffix = 0;
            while (Directory.Exists(Path.Combine(exportRoot, candidateName))) candidateName = $"{baseName}-{++suffix:00}";
            var candidatePath = Path.Combine(exportRoot, candidateName);
            tempPath = Path.Combine(exportRoot, $".{candidateName}.{Guid.NewGuid():N}.tmp");
            Directory.CreateDirectory(tempPath);
            if (faultPoint == PublishFaultPoint.DiskFullSimulation) throw new IOException("WB-PUBLISH-DISK-FULL: 模拟磁盘空间不足。");
            foreach (var file in compiled.Files)
            {
                if (faultPoint == PublishFaultPoint.WriteInterruption && file.Key.Equals("content-graph.json", StringComparison.Ordinal)) throw new IOException("WB-PUBLISH-WRITE-INTERRUPTED: 模拟写入中断。");
                if (file.Key.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || file.Key.Equals("package-manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
                var target = _workspace.Policy.RequireExport(Path.Combine(tempPath, file.Key), "候选文件写入");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                WriteFlushed(target, file.Value);
            }

            WriteFlushed(_workspace.Policy.RequireExport(Path.Combine(tempPath, "manifest.json"), "候选清单写入"), manifestBytes);
            WriteFlushed(_workspace.Policy.RequireExport(Path.Combine(tempPath, "package-manifest.json"), "候选清单兼容副本写入"), manifestBytes);
            WriteFlushed(_workspace.Policy.RequireExport(Path.Combine(tempPath, "SHA256SUMS.txt"), "候选哈希写入"), BuildChecksums(tempPath));
            WriteFlushed(_workspace.Policy.RequireExport(Path.Combine(tempPath, "complete.marker"), "候选完成标记写入"), Encoding.UTF8.GetBytes("complete\n"));
            FlushDirectory(tempPath);

            var candidate = tempPath;
            Directory.Move(candidate, candidatePath);
            tempPath = null;
            if (!updatePointer) return candidatePath;
            var pointerPath = _workspace.Policy.RequireExport(Path.Combine(exportRoot, "current.json"), "发布指针写入");
            var previousManifestHash = ReadCurrentManifestHash(pointerPath);
            var pointer = new JsonObject
            {
                ["candidate_dir"] = Path.GetFileName(candidatePath),
                ["manifest_hash"] = Hashing.Sha256Bytes(manifestBytes),
                ["published_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["previous_manifest_hash"] = previousManifestHash
            };
            var pointerSchema = Path.Combine(_workspace.SchemaRoot, "current-pointer.v1.schema.json");
            var pointerValidation = new ValidationReport();
            _schema.Validate(pointer, pointerSchema, pointerValidation);
            if (!pointerValidation.Valid) throw new InvalidOperationException("WB-PUBLISH-POINTER-001: current pointer schema 校验失败。");
            if (faultPoint == PublishFaultPoint.PointerReplaceFailure) throw new IOException("WB-PUBLISH-POINTER-002: 模拟指针替换失败。");
            pointerTemp = _workspace.Policy.RequireExport(pointerPath + $".{Guid.NewGuid():N}.tmp", "临时发布指针写入");
            WriteFlushed(pointerTemp, Encoding.UTF8.GetBytes(CanonicalJson.Serialize(pointer)));
            File.Move(pointerTemp, pointerPath, true);
            pointerTemp = null;
            FlushDirectory(exportRoot);
            return candidatePath;
        }
        catch (Exception error)
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && Directory.Exists(tempPath))
            {
                try { Directory.Delete(tempPath, true); }
                catch (Exception cleanupError) { error.Data["worldbook.publish_cleanup_error"] = cleanupError.ToString(); }
            }
            if (!string.IsNullOrWhiteSpace(pointerTemp) && File.Exists(pointerTemp))
            {
                try { File.Delete(pointerTemp); }
                catch (Exception cleanupError) { error.Data["worldbook.pointer_cleanup_error"] = cleanupError.ToString(); }
            }
            throw;
        }
        finally
        {
            if (ownsLease)
            {
                try { lease?.Dispose(); }
                finally
                {
                    if (File.Exists(lockPath))
                    {
                        try { File.Delete(lockPath); } catch { }
                    }
                }
            }
        }
    }
    private static string? ReadCurrentManifestHash(string pointerPath)
    {
        if (!File.Exists(pointerPath)) return null;
        try { return JsonNode.Parse(File.ReadAllText(pointerPath))?["manifest_hash"]?.GetValue<string>(); }
        catch { return null; }
    }

    private static byte[] BuildChecksums(string root)
    {
        var lines = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(x => !x.EndsWith("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => Path.GetRelativePath(root, x).Replace('\\', '/'), StringComparer.Ordinal)
            .Select(path => $"{Hashing.FileSha256(path)}  {Path.GetRelativePath(root, path).Replace('\\', '/')}");
        return Encoding.ASCII.GetBytes(string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static void WriteFlushed(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    private static void FlushDirectory(string path)
    {
        try
        {
            using var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.WriteThrough);
            handle.Flush(true);
        }
        catch (UnauthorizedAccessException) { }
        catch (NotSupportedException) { }
    }
}
