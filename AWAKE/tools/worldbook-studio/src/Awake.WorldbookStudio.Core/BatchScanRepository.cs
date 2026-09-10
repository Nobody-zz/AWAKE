using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchSourceInput(string RelativePath, string DisplayName, byte[] Bytes);

internal sealed record BatchScanResult(
    string ScanId,
    JsonObject Scan,
    IReadOnlyList<JsonObject> Snapshots,
    IReadOnlyList<JsonObject> SourceUnits);

internal sealed class BatchWorkspaceLayout
{
    private static readonly string[] AllowedSourceExtensions = [".txt", ".md", ".yaml", ".yml", ".json"];

    public BatchWorkspaceLayout(WorkspaceWritePolicy policy)
    {
        Policy = policy;
        Initialize();
    }

    public WorkspaceWritePolicy Policy { get; }
    public string Root => Policy.Root;
    public string PrebatchesRoot => Policy.RequirePrebatches(Path.Combine(Root, "prebatches"), "预批次根目录");
    public string CreateReservationsRoot => Policy.RequirePrebatches(Path.Combine(Root, "prebatches", "create-reservations"), "创建 reservation 根目录");
    public string BatchesRoot => Policy.RequireBatches(Path.Combine(Root, "batches"), "批次根目录");
    public IReadOnlyCollection<string> SourceExtensions => AllowedSourceExtensions;

    public string ScanRoot(string scanId)
        => Policy.RequirePrebatches(Path.Combine(PrebatchesRoot, BatchPathValidator.RequireIdentifier(scanId, "scan_id")), "预批次目录");

    public string ScanManifest(string scanId) => Path.Combine(ScanRoot(scanId), "scan.json");
    public string SnapshotText(string scanId, string snapshotId) => Path.Combine(ScanRoot(scanId), "sources", BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id") + ".txt");
    public string SnapshotMeta(string scanId, string snapshotId) => Path.Combine(ScanRoot(scanId), "sources", BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id") + ".meta.json");
    public string SourceUnit(string scanId, string snapshotId, string sourceUnitId) => Path.Combine(ScanRoot(scanId), "sources", BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id") + ".units", BatchPathValidator.RequireIdentifier(sourceUnitId, "source_unit_id") + ".json");
    public string PromotionJournal(string scanId, string operationId) => Path.Combine(ScanRoot(scanId), "promotion", BatchPathValidator.RequireIdentifier(operationId, "operation_id") + ".json");
    public string Reservation(string idempotencyKey) => Path.Combine(CreateReservationsRoot, BatchPathValidator.RequireIdentifier(idempotencyKey, "idempotency_key") + ".json");
    public string BatchRoot(string batchId) => Policy.RequireBatches(Path.Combine(BatchesRoot, BatchPathValidator.RequireIdentifier(batchId, "batch_id")), "批次目录");
    public string BatchTempRoot(string batchId) => Policy.RequireBatches(Path.Combine(Root, "batches", BatchPathValidator.RequireIdentifier(batchId, "batch_id") + ".tmp"), "批次临时目录");
    public string BatchManifest(string batchId) => Path.Combine(BatchRoot(batchId), "manifest.json");
    public string BatchTempManifest(string batchId) => Path.Combine(BatchTempRoot(batchId), "manifest.json");
    public string BatchSnapshotText(string batchId, string snapshotId) => Path.Combine(BatchRoot(batchId), "sources", BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id") + ".txt");
    public string BatchSnapshotMeta(string batchId, string snapshotId) => Path.Combine(BatchRoot(batchId), "sources", BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id") + ".meta.json");
    public string BatchSourceUnit(string batchId, string snapshotId, string sourceUnitId) => Path.Combine(BatchRoot(batchId), "sources", BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id") + ".units", BatchPathValidator.RequireIdentifier(sourceUnitId, "source_unit_id") + ".json");
    public string BatchTempSnapshotText(string batchId, string snapshotId) => Path.Combine(BatchTempRoot(batchId), "sources", BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id") + ".txt");
    public string BatchTempSnapshotMeta(string batchId, string snapshotId) => Path.Combine(BatchTempRoot(batchId), "sources", BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id") + ".meta.json");
    public string BatchTempSourceUnit(string batchId, string snapshotId, string sourceUnitId) => Path.Combine(BatchTempRoot(batchId), "sources", BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id") + ".units", BatchPathValidator.RequireIdentifier(sourceUnitId, "source_unit_id") + ".json");
    public string BatchItem(string batchId, string itemId) => Path.Combine(BatchRoot(batchId), "items", BatchPathValidator.RequireIdentifier(itemId, "item_id") + ".json");
    public string BatchTempItem(string batchId, string itemId) => Path.Combine(BatchTempRoot(batchId), "items", BatchPathValidator.RequireIdentifier(itemId, "item_id") + ".json");
    public string BatchItemResult(string batchId, string itemId) => Path.Combine(BatchRoot(batchId), "items", BatchPathValidator.RequireIdentifier(itemId, "item_id") + ".result.json");
    public string BatchItemReview(string batchId, string itemId) => Path.Combine(BatchRoot(batchId), "items", BatchPathValidator.RequireIdentifier(itemId, "item_id") + ".review.json");
    public string BatchItemMetadata(string batchId, string itemId) => Path.Combine(BatchRoot(batchId), "items", BatchPathValidator.RequireIdentifier(itemId, "item_id") + ".metadata.json");
    public string BatchItemDocument(string batchId, string itemId) => Path.Combine(BatchRoot(batchId), "items", BatchPathValidator.RequireIdentifier(itemId, "item_id") + ".document.json");
    public string BatchItemAttempt(string batchId, string itemId, string attemptId) => Path.Combine(BatchRoot(batchId), "items", BatchPathValidator.RequireIdentifier(itemId, "item_id") + ".attempts", BatchPathValidator.RequireIdentifier(attemptId, "attempt_id") + ".json");
    public string BatchEvidence(string batchId, string evidenceId) => Path.Combine(BatchRoot(batchId), "evidence", "v1", BatchPathValidator.RequireIdentifier(evidenceId, "evidence_id") + ".json");
    public string BatchConsent(string batchId, string consentId) => Path.Combine(BatchRoot(batchId), "consents", BatchPathValidator.RequireIdentifier(consentId, "consent_id") + ".json");
    public string BatchJournal(string batchId, string operationId) => Path.Combine(BatchRoot(batchId), "journal", BatchPathValidator.RequireIdentifier(operationId, "operation_id") + ".json");
    public string CacheEntry(string cacheKey) => Policy.RequireCache(Path.Combine(Root, "cache", "v1", BatchPathValidator.RequireIdentifier(cacheKey, "cache_key") + ".json"), "cache entry");
    public string CachePayload(string cacheKey) => Policy.RequireCache(Path.Combine(Root, "cache", "v1", BatchPathValidator.RequireIdentifier(cacheKey, "cache_key") + ".payload.json"), "cache payload");
    public string CacheCommit(string cacheKey) => Policy.RequireCache(Path.Combine(Root, "cache", "v1", BatchPathValidator.RequireIdentifier(cacheKey, "cache_key") + ".commit.json"), "cache commit marker");
    public string BatchCacheMaterialization(string batchId, string materializationId) => Path.Combine(BatchRoot(batchId), "cache-materializations", BatchPathValidator.RequireIdentifier(materializationId, "materialization_id") + ".json");

    public static void WriteJsonAtomic(string path, JsonObject value)
        => BatchAtomicFile.Write(path, Encoding.UTF8.GetBytes(CanonicalJson.Serialize(value) + "\n"));

    public static JsonObject ReadJson(string path)
        => JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))?.AsObject()
            ?? throw new InvalidOperationException($"WB-BATCH-STORE-422: JSON 文件根节点必须是对象。{path}");

    private void Initialize()
    {
        Directory.CreateDirectory(PrebatchesRoot);
        Directory.CreateDirectory(CreateReservationsRoot);
        Directory.CreateDirectory(BatchesRoot);
        Directory.CreateDirectory(Path.Combine(Root, "cache", "v1"));
    }
}

internal sealed class BatchScanRepository
{
    private const int MaxFiles = 200;
    private const long MaxTotalBytes = 64L * 1024 * 1024;
    private const long MaxFileBytes = 16L * 1024 * 1024;
    private const int MaxUnitCharacters = 80_000;
    private const string NormalizationRevision = "normalization.v1";
    private const string SplitterRevision = "splitter.v1";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex MarkdownHeading = new("^\\s{0,3}(#{1,6})\\s+(.+?)\\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly BatchWorkspaceLayout _layout;
    private readonly BatchAuthoringContractRegistry _contract;

    public BatchScanRepository(WorkspaceService workspace, BatchAuthoringContractRegistry contract)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _contract = contract;
        _ = _contract.RequireSchema("scan");
        _ = _contract.RequireSchema("snapshot");
        _ = _contract.RequireSchema("source_unit");
    }

    public BatchScanResult CreateScan(IEnumerable<BatchSourceInput> inputs, string ownerId, string ownerInstanceId, string workspaceMarkerHash, DateTimeOffset? now = null)
    {
        var sourceInputs = inputs?.ToList() ?? throw new InvalidOperationException("WB-BATCH-UPLOAD-400: 上传文件集合不能为空。");
        if (sourceInputs.Count is < 1 or > MaxFiles) throw new InvalidOperationException("WB-BATCH-UPLOAD-413: 上传文件数量超出上限。");
        BatchPathValidator.RequireIdentifier(ownerId, "owner_id");
        BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        RequireHash(workspaceMarkerHash, "workspace_marker_hash");

        var normalizedPaths = sourceInputs.Select(x => BatchPathValidator.CollisionKey(x.RelativePath)).ToArray();
        BatchPathValidator.EnsureNoCollisions(normalizedPaths, "上传路径");
        if (sourceInputs.Any(x => x.Bytes is null || x.Bytes.LongLength > MaxFileBytes)) throw new InvalidOperationException("WB-BATCH-UPLOAD-413: 单个上传文件超过 16 MiB。");
        if (sourceInputs.Sum(x => x.Bytes.LongLength) > MaxTotalBytes) throw new InvalidOperationException("WB-BATCH-UPLOAD-413: 上传总大小超过 64 MiB。");

        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var scanId = NewId("scan");
        var operationId = NewId("promotion");
        var scanRoot = _layout.ScanRoot(scanId);
        var snapshots = new List<JsonObject>();
        var units = new List<JsonObject>();
        var snapshotBindings = new JsonArray();

        try
        {
            foreach (var input in sourceInputs.OrderBy(x => BatchPathValidator.CollisionKey(x.RelativePath), StringComparer.Ordinal))
            {
                var relativePath = ValidateUploadPath(input.RelativePath);
                var displayName = string.IsNullOrWhiteSpace(input.DisplayName) ? Path.GetFileName(relativePath) : input.DisplayName.Trim();
                if (displayName.Length is < 1 or > 260) throw new InvalidOperationException("WB-BATCH-UPLOAD-422: 文件显示名长度无效。");
                var rawHash = Hashing.Sha256Bytes(input.Bytes);
                var normalizedText = NormalizeText(input.Bytes);
                var normalizedHash = Hashing.Sha256Text(normalizedText);
                var snapshotId = NewId("snapshot");
                var sourceTextPath = $"prebatches/{scanId}/sources/{snapshotId}.txt";
                var sourceTextFile = _layout.SnapshotText(scanId, snapshotId);
                BatchAtomicFile.Write(sourceTextFile, Encoding.UTF8.GetBytes(normalizedText));

                var sourceUnits = SplitUnits(normalizedText, scanId, snapshotId, ownerId, workspaceMarkerHash, timestamp, normalizedHash);
                foreach (var unit in sourceUnits)
                {
                    var unitId = unit["unit_id"]!.GetValue<string>();
                    BatchWorkspaceLayout.WriteJsonAtomic(_layout.SourceUnit(scanId, snapshotId, unitId), unit);
                    units.Add(unit);
                }

                var snapshot = new JsonObject
                {
                    ["schema_version"] = "awake.worldbook.batch-snapshot.v2",
                    ["snapshot_id"] = snapshotId,
                    ["scan_id"] = scanId,
                    ["owner_id"] = ownerId,
                    ["display_name"] = displayName,
                    ["relative_path"] = relativePath,
                    ["raw_content_hash"] = rawHash,
                    ["normalized_content_hash"] = normalizedHash,
                    ["byte_length"] = input.Bytes.LongLength,
                    ["character_count"] = normalizedText.Length,
                    ["created_at"] = timestamp.ToString("O"),
                    ["workspace_marker_hash"] = workspaceMarkerHash,
                    ["normalization_revision"] = NormalizationRevision,
                    ["splitter_revision"] = SplitterRevision,
                    ["normalized_text_ref"] = sourceTextPath,
                    ["unit_ids"] = new JsonArray(sourceUnits.Select(x => JsonValue.Create(x["unit_id"]!.GetValue<string>())).ToArray()),
                    ["scope_id"] = scanId,
                    ["scope_kind"] = "prebatch",
                    ["source_text_path"] = sourceTextPath
                };
                BatchWorkspaceLayout.WriteJsonAtomic(_layout.SnapshotMeta(scanId, snapshotId), snapshot);
                snapshots.Add(snapshot);
                snapshotBindings.Add(new JsonObject { ["snapshot_id"] = snapshotId, ["snapshot_hash"] = normalizedHash });
            }

            var scanHash = ContractHashing.HashCanonical(new JsonObject
            {
                ["normalization_revision"] = NormalizationRevision,
                ["snapshot_bindings"] = new JsonArray(snapshotBindings
                    .OrderBy(x => x!["snapshot_id"]!.GetValue<string>(), StringComparer.Ordinal)
                    .Select(x => x!.DeepClone())
                    .ToArray()),
                ["splitter_revision"] = SplitterRevision
            });
            var scan = new JsonObject
            {
                ["schema_version"] = "awake.worldbook.batch-scan.v2",
                ["scan_id"] = scanId,
                ["owner_id"] = ownerId,
                ["status"] = "ready",
                ["snapshot_ids"] = new JsonArray(snapshots.Select(x => JsonValue.Create(x["snapshot_id"]!.GetValue<string>())).ToArray()),
                ["scan_hash"] = scanHash,
                ["created_at"] = timestamp.ToString("O"),
                ["expires_at"] = timestamp.AddHours(24).ToString("O"),
                ["warnings"] = new JsonArray(),
                ["workspace_marker_hash"] = workspaceMarkerHash,
                ["created_owner_instance_id"] = ownerInstanceId,
                ["prebatch_path"] = $"prebatches/{scanId}",
                ["promotion"] = new JsonObject
                {
                    ["operation_id"] = operationId,
                    ["state"] = "staged",
                    ["source_scan_hash"] = scanHash,
                    ["journal_ref"] = $"prebatches/{scanId}/promotion/{operationId}.json"
                }
            };
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.ScanManifest(scanId), scan);
            return new BatchScanResult(scanId, scan, snapshots, units);
        }
        catch
        {
            try { Directory.CreateDirectory(Path.Combine(scanRoot, "quarantine")); } catch { }
            throw;
        }
    }

    public JsonObject ReadScan(string scanId)
        => BatchWorkspaceLayout.ReadJson(_layout.ScanManifest(BatchPathValidator.RequireIdentifier(scanId, "scan_id")));

    public JsonObject ReadSourceUnit(string scanId, string snapshotId, string sourceUnitId)
        => BatchWorkspaceLayout.ReadJson(_layout.SourceUnit(
            BatchPathValidator.RequireIdentifier(scanId, "scan_id"),
            BatchPathValidator.RequireIdentifier(snapshotId, "snapshot_id"),
            BatchPathValidator.RequireIdentifier(sourceUnitId, "source_unit_id")));

    private static string ValidateUploadPath(string relativePath)
    {
        if (relativePath.Length > 260) throw new InvalidOperationException("WB-BATCH-UPLOAD-422: 上传相对路径过长。");
        var segments = relativePath.Split('/', StringSplitOptions.None);
        if (segments.Length == 0 || segments.Any(x => x.Length == 0 || x is "." or "..")) throw new InvalidOperationException("WB-BATCH-UPLOAD-422: 上传路径段无效。");
        return string.Join('/', segments.Select(x => x.Normalize(NormalizationForm.FormC)));
    }

    private static string NormalizeText(byte[] bytes)
    {
        string text;
        try { text = StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException ex) { throw new InvalidOperationException("WB-BATCH-UPLOAD-422: 文件不是有效 UTF-8 文本。", ex); }
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Normalize(NormalizationForm.FormC);
    }

    private static List<JsonObject> SplitUnits(string text, string scanId, string snapshotId, string ownerId, string workspaceMarkerHash, DateTimeOffset timestamp, string snapshotHash)
    {
        var ranges = new List<(int Start, int End)>();
        var cursor = 0;
        while (cursor < text.Length)
        {
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor])) cursor++;
            if (cursor >= text.Length) break;
            var separator = text.IndexOf("\n\n", cursor, StringComparison.Ordinal);
            var end = separator < 0 ? text.Length : separator;
            if (end > cursor) ranges.Add((cursor, end));
            cursor = separator < 0 ? text.Length : separator + 2;
        }
        if (ranges.Count == 0 && text.Length > 0) ranges.Add((0, text.Length));

        var units = new List<JsonObject>();
        foreach (var range in ranges)
        {
            var start = range.Start;
            var end = range.End;
            while (start < end && char.IsWhiteSpace(text[start])) start++;
            while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
            for (var partStart = start; partStart < end;)
            {
                var partEnd = Math.Min(end, partStart + MaxUnitCharacters);
                if (partEnd < end && char.IsHighSurrogate(text[partEnd - 1])) partEnd--;
                if (partEnd <= partStart) partEnd = Math.Min(end, partStart + MaxUnitCharacters);
                var unitText = text[partStart..partEnd];
                var lineStart = 1 + text[..partStart].Count(x => x == '\n');
                var lineEnd = lineStart + unitText.Count(x => x == '\n');
                var headingPath = new JsonArray();
                foreach (var line in unitText.Split('\n'))
                {
                    var heading = MarkdownHeading.Match(line);
                    if (heading.Success) headingPath.Add(heading.Groups[2].Value.Trim());
                }
                var unitContentHash = Hashing.Sha256Text(unitText);
                var unitHash = ContractHashing.HashCanonical(new JsonObject
                {
                    ["end_utf16"] = partEnd,
                    ["line_end"] = lineEnd,
                    ["line_start"] = lineStart,
                    ["normalized_content_hash"] = unitContentHash,
                    ["start_utf16"] = partStart,
                    ["text"] = unitText
                });
                var unitId = NewId("unit");
                units.Add(new JsonObject
                {
                    ["schema_version"] = "awake.worldbook.batch-source-unit.v2",
                    ["unit_id"] = unitId,
                    ["snapshot_id"] = snapshotId,
                    ["owner_id"] = ownerId,
                    ["unit_index"] = units.Count,
                    ["heading_path"] = headingPath,
                    ["start_utf16"] = partStart,
                    ["end_utf16"] = partEnd,
                    ["line_start"] = lineStart,
                    ["line_end"] = lineEnd,
                    ["text"] = unitText,
                    ["normalized_content_hash"] = unitContentHash,
                    ["unit_hash"] = unitHash,
                    ["splitter_revision"] = SplitterRevision,
                    ["created_at"] = timestamp.ToString("O"),
                    ["scope_id"] = scanId,
                    ["scope_kind"] = "prebatch",
                    ["scan_id"] = scanId
                });
                partStart = partEnd;
            }
        }
        return units;
    }

    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";

    private static void RequireHash(string value, string field)
    {
        if (!Regex.IsMatch(value ?? string.Empty, "^[A-Fa-f0-9]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException($"WB-BATCH-UPLOAD-422: {field} 必须是 SHA-256。");
    }
}

internal static class BatchAtomicFile
{
    public static void WriteCreateOnly(string target, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("WB-BATCH-STORE-422: 目标目录缺失。");
        Directory.CreateDirectory(directory);
        WorkspacePathPolicy.EnsureNoReparsePoint(directory);
        var temporary = target + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(temporary, target, false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static void Write(string target, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("WB-BATCH-STORE-422: 目标目录缺失。");
        Directory.CreateDirectory(directory);
        WorkspacePathPolicy.EnsureNoReparsePoint(directory);
        var temporary = target + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(temporary, target, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
