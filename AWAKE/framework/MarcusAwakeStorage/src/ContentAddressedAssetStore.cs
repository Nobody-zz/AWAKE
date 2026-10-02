using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace MarcusAwakeStorage;

/// <summary>
/// 内容寻址资产库（CAS）—— <see cref="IAssetService"/> 的落地实现。
///
/// <para><b>为什么是文件，不是 SQLite 行。</b> 设计大纲 §6 定的形态就是文件树：
/// <c>objects/&lt;hash 前 2 位&gt;/&lt;sha256&gt;</c>、<c>metadata/&lt;asset-id&gt;.json</c>、
/// <c>temp/</c>、<c>quarantine/</c>。这么定的实质好处是 <b>对象文件名即内容哈希</b>：去重不是一段逻辑，
/// 是「文件在不在」这一个事实。同时它让资产库<b>不依赖 SQLite</b> —— 可以单独构造、单独测，
/// 也不必为它推一次 <c>SchemaVersion</c>。</para>
///
/// <para><b>identity 与引用是两件事</b>（设计大纲 §7、§11）。<c>asset_id</c> 每次导入都新生成，
/// <c>content_hash</c> 由字节决定。于是「同一张图导两次」得到两条元数据、共享一个对象文件；
/// 删掉其中一条时，只有当<b>再没有别的元数据指向那个哈希</b>，对象文件才真的消失。
/// 这就是 §11 那条验收判据（「删除一条 metadata 不会误删仍被其他引用使用的 CAS 对象」）的实现方式。</para>
///
/// <para><b>所有操作按 <c>context.Caller</c> 划范围。</b> 元数据里记着
/// <c>owner_extension_id</c>，读 / 写 / 列 / 删 / 导出都要求它等于调用者，否则 <c>asset.scope_denied</c>。
/// 这是设计大纲 §13「所有者范围列表」的直接后果：别人家的资产不该出现在你的列表里，
/// 更不该被你读到或删掉。</para>
///
/// <para><b>调用者不依赖真实文件路径</b>（§6）：对外只有 <see cref="AssetHandle"/> 与字节，
/// <see cref="RootPath"/> 只是给宿主做诊断与测试用的。</para>
/// </summary>
public sealed class ContentAddressedAssetStore : IAssetService, IDisposable
{
    private const string ErrorOwner = "MarcusAwakeStorage";
    private const string ObjectFolderName = "objects";
    private const string MetadataFolderName = "metadata";
    private const string TempFolderName = "temp";
    private const string QuarantineFolderName = "quarantine";
    private const string ExportFolderName = "exports";
    private const string MetadataExtension = ".json";

    /// <summary>
    /// 保留等级。设计大纲 §9 定的五个值，<b>不在表里的一律拒收</b>而不是归一到默认 ——
    /// 清理策略按这个字段做判断，写错一个拼写就等于让这条资产永远不会被清掉。
    /// </summary>
    private static readonly string[] KnownRetentionClasses = { "ephemeral", "session", "campaign", "persistent", "pinned" };

    private const string DefaultRetentionClass = "campaign";

    private readonly string root;
    private readonly AssetStoreOptions options;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private int disposed;

    public ContentAddressedAssetStore(string assetRootPath, AssetStoreOptions options = null)
    {
        if (string.IsNullOrWhiteSpace(assetRootPath)) throw new ArgumentException("An asset root path is required.", nameof(assetRootPath));
        root = Path.GetFullPath(assetRootPath);
        this.options = options ?? new AssetStoreOptions();
        this.options.Validate();
    }

    /// <summary>资产库根目录。给宿主做诊断用；<b>调用者不该拿它拼路径</b>（设计大纲 §6）。</summary>
    public string RootPath => root;

    private string ObjectRootPath => Path.Combine(root, ObjectFolderName);

    private string MetadataRootPath => Path.Combine(root, MetadataFolderName);

    private string TempRootPath => Path.Combine(root, TempFolderName);

    private string QuarantineRootPath => Path.Combine(root, QuarantineFolderName);

    private string ExportRootPath => Path.Combine(root, ExportFolderName);

    // ------------------------------------------------------------------ 导入

    public Task<OperationResult<AssetHandle>> ImportAsync(AssetImportRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        if (request == null || context == null)
        {
            return Task.FromResult(Invalid<AssetHandle>("asset.invalid_request", FrameworkErrorCategory.InvalidRequest, "An asset import request and request context are required.", context));
        }
        if (request.ByteLength < 1)
        {
            return Task.FromResult(Invalid<AssetHandle>("asset.empty_content", FrameworkErrorCategory.InvalidRequest, "Imported content must not be empty.", context));
        }
        if (request.ByteLength > options.MaxAssetBytes)
        {
            return Task.FromResult(Invalid<AssetHandle>("asset.content_too_large", FrameworkErrorCategory.ResourceExhausted, "The imported content exceeds the configured asset byte limit.", context));
        }
        var retention = NormalizeRetentionClass(request.RetentionClass);
        if (retention == null)
        {
            return Task.FromResult(Invalid<AssetHandle>("asset.retention_unknown", FrameworkErrorCategory.InvalidRequest, "The retention class must be one of ephemeral, session, campaign, persistent or pinned.", context));
        }

        var content = request.GetContentCopy();
        return ExecuteAsync("asset", context, cancellationToken, token => Import(content, request, retention, context, token));
    }

    private OperationResult<AssetHandle> Import(byte[] content, AssetImportRequest request, string retention, RequestContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureFolders();

        var detected = AssetContentInspector.Detect(content);
        if (detected.Format == AssetContentFormat.Unknown)
        {
            Quarantine(content, detected.ContentHash);
            return Invalid<AssetHandle>("asset.content_format_unknown", FrameworkErrorCategory.Unsupported, "The content is not a recognized image payload.", context);
        }

        var declared = AssetContentInspector.NormalizeDeclaredMediaType(request.MediaType);
        if (declared.Length > 0 && !StringComparer.Ordinal.Equals(declared, detected.MediaType))
        {
            Quarantine(content, detected.ContentHash);
            return Invalid<AssetHandle>("asset.content_type_mismatch", FrameworkErrorCategory.InvalidRequest, "The declared media type does not match the payload's real format.", context);
        }

        var objectPath = ObjectPathFor(detected.ContentHash);
        if (!File.Exists(objectPath))
        {
            // 暂存再落位：读的人要么看到完整对象，要么什么都看不到，不会看到半个文件。
            WriteAtomically(objectPath, content, cancellationToken);
        }

        var record = new AssetMetadataRecord
        {
            AssetId = NewAssetId(),
            ContentHash = detected.ContentHash,
            MediaType = detected.MediaType,
            ByteLength = detected.ByteLength,
            LogicalKind = request.LogicalKind ?? string.Empty,
            CreatedByTask = request.CreatedByTask ?? string.Empty,
            OwnerExtensionId = context.Caller.Value,
            CampaignId = context.Session.CampaignGuid,
            TimelineId = context.Session.TimelineId,
            Provenance = request.Provenance ?? string.Empty,
            RetentionClass = retention,
            CreatedUtc = DateTimeOffset.UtcNow,
            Pinned = false
        };
        WriteMetadata(record, cancellationToken);
        return OperationResult<AssetHandle>.Succeeded(record.ToHandle());
    }

    // ------------------------------------------------------------------ 读取

    public Task<OperationResult<AssetMetadata>> GetMetadataAsync(string assetId, RequestContext context, CancellationToken cancellationToken)
    {
        if (!IsAssetId(assetId) || context == null)
        {
            return Task.FromResult(Invalid<AssetMetadata>("asset.invalid_request", FrameworkErrorCategory.InvalidRequest, "A valid asset ID and request context are required.", context));
        }
        return ExecuteAsync("asset", context, cancellationToken, token => GetMetadata(assetId, context, token));
    }

    private OperationResult<AssetMetadata> GetMetadata(string assetId, RequestContext context, CancellationToken cancellationToken)
    {
        var loaded = LoadMetadata(assetId);
        if (loaded.Error != null) return OperationResult<AssetMetadata>.Failed(loaded.Error);
        var scopeError = CheckScope(loaded.Record, context);
        if (scopeError != null) return OperationResult<AssetMetadata>.Failed(scopeError);
        cancellationToken.ThrowIfCancellationRequested();
        return OperationResult<AssetMetadata>.Succeeded(new AssetMetadata(loaded.Record.ToHandle(), loaded.Record.CreatedUtc, loaded.Record.Pinned));
    }

    public Task<OperationResult<AssetContent>> ReadAsync(string assetId, RequestContext context, CancellationToken cancellationToken)
    {
        if (!IsAssetId(assetId) || context == null)
        {
            return Task.FromResult(Invalid<AssetContent>("asset.invalid_request", FrameworkErrorCategory.InvalidRequest, "A valid asset ID and request context are required.", context));
        }
        return ExecuteAsync("asset", context, cancellationToken, token => Read(assetId, context, token));
    }

    private OperationResult<AssetContent> Read(string assetId, RequestContext context, CancellationToken cancellationToken)
    {
        var loaded = LoadMetadata(assetId);
        if (loaded.Error != null) return OperationResult<AssetContent>.Failed(loaded.Error);
        var record = loaded.Record;
        var scopeError = CheckScope(record, context);
        if (scopeError != null) return OperationResult<AssetContent>.Failed(scopeError);

        var objectPath = ObjectPathFor(record.ContentHash);
        if (!File.Exists(objectPath))
        {
            return Invalid<AssetContent>("asset.object_missing", FrameworkErrorCategory.RecoveryRequired, "The asset object is missing from the content store.", context);
        }
        var bytes = File.ReadAllBytes(objectPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.LongLength != record.ByteLength || !StringComparer.Ordinal.Equals(AssetContentInspector.Sha256Hex(bytes), record.ContentHash))
        {
            // 对象文件名就是它的哈希。对不上只有一种解释：文件被外部动过。
            // 这是**数据完整性问题**，不是「暂时读不到」—— 所以 RecoveryRequired，不是 Unavailable。
            return Invalid<AssetContent>("asset.object_corrupt", FrameworkErrorCategory.RecoveryRequired, "The asset object does not match its recorded content hash.", context);
        }
        return OperationResult<AssetContent>.Succeeded(new AssetContent(record.ToHandle(), bytes));
    }

    // ------------------------------------------------------------------ 固定 / 列表

    public Task<OperationResult<bool>> SetPinnedAsync(string assetId, bool pinned, RequestContext context, CancellationToken cancellationToken)
    {
        if (!IsAssetId(assetId) || context == null)
        {
            return Task.FromResult(Invalid<bool>("asset.invalid_request", FrameworkErrorCategory.InvalidRequest, "A valid asset ID and request context are required.", context));
        }
        return ExecuteAsync("asset", context, cancellationToken, token => SetPinned(assetId, pinned, context, token));
    }

    private OperationResult<bool> SetPinned(string assetId, bool pinned, RequestContext context, CancellationToken cancellationToken)
    {
        var loaded = LoadMetadata(assetId);
        if (loaded.Error != null) return OperationResult<bool>.Failed(loaded.Error);
        var scopeError = CheckScope(loaded.Record, context);
        if (scopeError != null) return OperationResult<bool>.Failed(scopeError);
        if (loaded.Record.Pinned != pinned)
        {
            loaded.Record.Pinned = pinned;
            WriteMetadata(loaded.Record, cancellationToken);
        }
        return OperationResult<bool>.Succeeded(true);
    }

    public Task<OperationResult<AssetListPage>> ListAsync(int maximumResults, string cursor, RequestContext context, CancellationToken cancellationToken)
    {
        if (context == null || maximumResults < 1 || maximumResults > options.MaxListResults)
        {
            return Task.FromResult(Invalid<AssetListPage>("asset.invalid_request", FrameworkErrorCategory.InvalidRequest, "A page size within the configured limit and a request context are required.", context));
        }
        var normalizedCursor = cursor ?? string.Empty;
        if (normalizedCursor.Length > 0 && !IsAssetId(normalizedCursor))
        {
            return Task.FromResult(Invalid<AssetListPage>("asset.invalid_request", FrameworkErrorCategory.InvalidRequest, "The list cursor is not a valid asset ID.", context));
        }
        return ExecuteAsync("asset", context, cancellationToken, token => List(maximumResults, normalizedCursor, context, token));
    }

    private OperationResult<AssetListPage> List(int maximumResults, string cursor, RequestContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owned = ScanMetadata().Records
            .Where(item => StringComparer.Ordinal.Equals(item.OwnerExtensionId, context.Caller.Value))
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .ToList();

        var page = new List<AssetMetadata>();
        foreach (var record in owned)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cursor.Length > 0 && StringComparer.Ordinal.Compare(record.AssetId, cursor) <= 0) continue;
            page.Add(new AssetMetadata(record.ToHandle(), record.CreatedUtc, record.Pinned));
            if (page.Count == maximumResults) break;
        }

        // 只有「后面还有」时才给游标：空游标就是「到底了」，调用方不必再猜。
        var nextCursor = string.Empty;
        if (page.Count == maximumResults)
        {
            var last = page[page.Count - 1].Handle.AssetId;
            nextCursor = owned.Any(item => StringComparer.Ordinal.Compare(item.AssetId, last) > 0) ? last : string.Empty;
        }
        return OperationResult<AssetListPage>.Succeeded(new AssetListPage(page, nextCursor));
    }

    // ------------------------------------------------------------------ 导出 / 删除 / 清理

    public Task<OperationResult<AssetExportReceipt>> ExportAsync(string assetId, RequestContext context, CancellationToken cancellationToken)
    {
        if (!IsAssetId(assetId) || context == null)
        {
            return Task.FromResult(Invalid<AssetExportReceipt>("asset.invalid_request", FrameworkErrorCategory.InvalidRequest, "A valid asset ID and request context are required.", context));
        }
        return ExecuteAsync("asset", context, cancellationToken, token => Export(assetId, context, token));
    }

    private OperationResult<AssetExportReceipt> Export(string assetId, RequestContext context, CancellationToken cancellationToken)
    {
        var read = Read(assetId, context, cancellationToken);
        if (!read.IsSuccess) return OperationResult<AssetExportReceipt>.Failed(read.Error);

        var bytes = read.Value.GetContentCopy();
        var exportId = NewAssetId();
        WriteAtomically(Path.Combine(ExportRootPath, exportId + ".pkg"), bytes, cancellationToken);
        // 单件资产的便携包就是它自己的字节，所以包的哈希等于内容哈希。收据本身是**不透明**的
        // （设计大纲 §13）：调用方拿到的是「导出过了」这个事实，不是一个它该去读的路径。
        var receipt = new AssetExportReceipt(exportId, assetId, AssetContentInspector.Sha256Hex(bytes), bytes.LongLength);
        return OperationResult<AssetExportReceipt>.Succeeded(receipt);
    }

    public Task<OperationResult<bool>> DeleteAsync(string assetId, RequestContext context, CancellationToken cancellationToken)
    {
        if (!IsAssetId(assetId) || context == null)
        {
            return Task.FromResult(Invalid<bool>("asset.invalid_request", FrameworkErrorCategory.InvalidRequest, "A valid asset ID and request context are required.", context));
        }
        return ExecuteAsync("asset", context, cancellationToken, token => Delete(assetId, context, token));
    }

    private OperationResult<bool> Delete(string assetId, RequestContext context, CancellationToken cancellationToken)
    {
        var loaded = LoadMetadata(assetId);
        if (loaded.Error != null) return OperationResult<bool>.Failed(loaded.Error);
        var scopeError = CheckScope(loaded.Record, context);
        if (scopeError != null) return OperationResult<bool>.Failed(scopeError);
        if (loaded.Record.Pinned)
        {
            // 设计大纲 §6 的「固定资产拒删」：钉住是玩家/宿主表达的意图，删除请求不该悄悄覆盖它。
            return Invalid<bool>("asset.pinned", FrameworkErrorCategory.Denied, "A pinned asset cannot be deleted.", context);
        }
        DeleteRecord(loaded.Record, cancellationToken);
        return OperationResult<bool>.Succeeded(true);
    }

    public Task<OperationResult<AssetCleanupSummary>> CleanupAsync(AssetCleanupRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        if (request == null || context == null || request.MaximumItems < 1 || request.MaximumItems > options.MaxCleanupItems)
        {
            return Task.FromResult(Invalid<AssetCleanupSummary>("asset.invalid_request", FrameworkErrorCategory.InvalidRequest, "A cleanup request within the configured limit and a request context are required.", context));
        }
        return ExecuteAsync("asset", context, cancellationToken, token => Cleanup(request, context, token));
    }

    private OperationResult<AssetCleanupSummary> Cleanup(AssetCleanupRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var retentionFilter = request.RetentionClasses ?? Array.Empty<string>();
        var eligible = ScanMetadata().Records
            .Where(item => StringComparer.Ordinal.Equals(item.OwnerExtensionId, context.Caller.Value))
            .Where(item => !item.Pinned)
            .Where(item => retentionFilter.Count == 0 || retentionFilter.Any(value => StringComparer.Ordinal.Equals(value, item.RetentionClass)))
            .Where(item => item.CreatedUtc < request.OlderThanUtc)
            .OrderBy(item => item.CreatedUtc)
            .ThenBy(item => item.AssetId, StringComparer.Ordinal)
            .Take(request.MaximumItems)
            .ToList();

        if (request.DryRun)
        {
            // 干跑只回答「有多少条符合条件」。它**不写任何东西** —— 这是它能被安全调用的全部理由。
            return OperationResult<AssetCleanupSummary>.Succeeded(new AssetCleanupSummary(eligible.Count, 0, 0, 0L));
        }

        var reclaimedObjects = 0;
        var reclaimedBytes = 0L;
        foreach (var record in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var objectPath = ObjectPathFor(record.ContentHash);
            var objectBytes = File.Exists(objectPath) ? new FileInfo(objectPath).Length : 0L;
            if (DeleteRecord(record, cancellationToken))
            {
                reclaimedObjects++;
                reclaimedBytes += objectBytes;
            }
        }
        return OperationResult<AssetCleanupSummary>.Succeeded(new AssetCleanupSummary(eligible.Count, eligible.Count, reclaimedObjects, reclaimedBytes));
    }

    // ------------------------------------------------------------------ 内部：落盘

    /// <summary>
    /// 删掉一条元数据；只有在**再没有别的元数据指向同一个内容哈希**时才回收对象文件。
    /// 返回是否真的回收了对象。
    /// </summary>
    private bool DeleteRecord(AssetMetadataRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryDelete(MetadataPathFor(record.AssetId));

        var scan = ScanMetadata();
        if (scan.Records.Any(item => StringComparer.Ordinal.Equals(item.ContentHash, record.ContentHash))) return false;
        // 有一条元数据读不出来 ⇒ 无法证明没人还引用这个对象 ⇒ **宁可留着**。
        // 泄漏一个对象文件是可以接受的；删掉别人还要用的字节不行。
        if (scan.UnreadableCount > 0) return false;

        var objectPath = ObjectPathFor(record.ContentHash);
        if (!File.Exists(objectPath)) return false;
        TryDelete(objectPath);
        return true;
    }

    private void WriteMetadata(AssetMetadataRecord record, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(record, MetadataJsonOptions);
        WriteAtomically(MetadataPathFor(record.AssetId), System.Text.Encoding.UTF8.GetBytes(json), cancellationToken);
    }

    private void WriteAtomically(string target, byte[] content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        Directory.CreateDirectory(TempRootPath);
        var staging = Path.Combine(TempRootPath, Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllBytes(staging, content);
        cancellationToken.ThrowIfCancellationRequested();
        // **必须带 overwrite。** `File.Move(src, dst)` 在 dst 已存在时抛 IOException，
        // 而「目标已存在」在元数据改写（钉住/取消钉住）这条路上是**常态**，不是异常 ——
        // 早先这里靠 catch IOException 吞掉，结果是每一次改写都被静默丢弃，读回来还是旧值。
        File.Move(staging, target, true);
    }

    private void Quarantine(byte[] content, string contentHash)
    {
        try
        {
            EnsureFolders();
            var target = Path.Combine(QuarantineRootPath, contentHash);
            if (File.Exists(target)) return;
            WriteAtomically(target, content, CancellationToken.None);
        }
        catch (IOException)
        {
            // 隔离是**尽力而为**的取证动作。它失败了不该把「内容不合规」这个判决变成「存储故障」——
            // 拒收才是结论，留档是附带的。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // ------------------------------------------------------------------ 内部：读盘

    private sealed class MetadataLoad
    {
        internal MetadataLoad(AssetMetadataRecord record, FrameworkError error)
        {
            Record = record;
            Error = error;
        }

        internal AssetMetadataRecord Record { get; }

        internal FrameworkError Error { get; }
    }

    private sealed class MetadataScan
    {
        internal MetadataScan(List<AssetMetadataRecord> records, int unreadableCount)
        {
            Records = records;
            UnreadableCount = unreadableCount;
        }

        internal List<AssetMetadataRecord> Records { get; }

        internal int UnreadableCount { get; }
    }

    private MetadataLoad LoadMetadata(string assetId)
    {
        var path = MetadataPathFor(assetId);
        if (!File.Exists(path))
        {
            return new MetadataLoad(null, FrameworkErrors.Create("asset.not_found", FrameworkErrorCategory.NotFound, "No asset with that ID exists in this store.", null, false, ErrorOwner));
        }
        AssetMetadataRecord record;
        try
        {
            record = JsonSerializer.Deserialize<AssetMetadataRecord>(File.ReadAllText(path), MetadataJsonOptions);
        }
        catch (JsonException)
        {
            return new MetadataLoad(null, FrameworkErrors.Create("asset.metadata_corrupt", FrameworkErrorCategory.RecoveryRequired, "The asset metadata file is not valid JSON.", null, false, ErrorOwner));
        }
        catch (IOException)
        {
            return new MetadataLoad(null, FrameworkErrors.Create("asset.store_unavailable", FrameworkErrorCategory.Unavailable, "The asset metadata could not be read.", null, true, ErrorOwner));
        }
        if (!IsValidRecord(record))
        {
            return new MetadataLoad(null, FrameworkErrors.Create("asset.metadata_corrupt", FrameworkErrorCategory.RecoveryRequired, "The asset metadata file is missing required fields.", null, false, ErrorOwner));
        }
        return new MetadataLoad(record, null);
    }

    /// <summary>
    /// 读全部元数据。<paramref name="MetadataScan.UnreadableCount"/> 是**故意保留**的：
    /// 读不出来的条目在引用扫描里必须被当成「可能还引用着」，否则一次 JSON 损坏就会连带删掉别人还要用的对象。
    /// </summary>
    private MetadataScan ScanMetadata()
    {
        var records = new List<AssetMetadataRecord>();
        var unreadable = 0;
        if (!Directory.Exists(MetadataRootPath)) return new MetadataScan(records, unreadable);
        foreach (var path in Directory.EnumerateFiles(MetadataRootPath, "*" + MetadataExtension))
        {
            try
            {
                var record = JsonSerializer.Deserialize<AssetMetadataRecord>(File.ReadAllText(path), MetadataJsonOptions);
                if (!IsValidRecord(record))
                {
                    unreadable++;
                    continue;
                }
                records.Add(record);
            }
            catch (JsonException)
            {
                unreadable++;
            }
            catch (IOException)
            {
                unreadable++;
            }
        }
        return new MetadataScan(records, unreadable);
    }

    private static bool IsValidRecord(AssetMetadataRecord record)
    {
        return record != null
            && IsAssetId(record.AssetId)
            && !string.IsNullOrWhiteSpace(record.ContentHash)
            && !string.IsNullOrWhiteSpace(record.OwnerExtensionId)
            && record.ByteLength >= 1;
    }

    private FrameworkError CheckScope(AssetMetadataRecord record, RequestContext context)
    {
        if (StringComparer.Ordinal.Equals(record.OwnerExtensionId, context.Caller.Value)) return null;
        return FrameworkErrors.Create("asset.scope_denied", FrameworkErrorCategory.Denied, "The asset belongs to a different extension.", context.CorrelationId, false, ErrorOwner);
    }

    // ------------------------------------------------------------------ 内部：杂项

    private void EnsureFolders()
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(ObjectRootPath);
        Directory.CreateDirectory(MetadataRootPath);
        Directory.CreateDirectory(TempRootPath);
        Directory.CreateDirectory(QuarantineRootPath);
        Directory.CreateDirectory(ExportRootPath);
    }

    private string ObjectPathFor(string contentHash)
    {
        var prefix = contentHash.Length >= 2 ? contentHash.Substring(0, 2) : "00";
        return Path.Combine(ObjectRootPath, prefix, contentHash);
    }

    private string MetadataPathFor(string assetId) => Path.Combine(MetadataRootPath, assetId + MetadataExtension);

    private static bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string NewAssetId() => Guid.NewGuid().ToString("N");

    private static bool IsAssetId(string value)
    {
        if (value == null || value.Length != 32) return false;
        for (var index = 0; index < value.Length; index++)
        {
            var c = value[index];
            var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!isHex) return false;
        }
        return true;
    }

    /// <summary>空串归一到默认；不在表里的返回 <c>null</c>（调用方据此拒收）。</summary>
    private static string NormalizeRetentionClass(string retentionClass)
    {
        if (string.IsNullOrWhiteSpace(retentionClass)) return DefaultRetentionClass;
        var trimmed = retentionClass.Trim();
        for (var index = 0; index < KnownRetentionClasses.Length; index++)
        {
            if (StringComparer.Ordinal.Equals(KnownRetentionClasses[index], trimmed)) return KnownRetentionClasses[index];
        }
        return null;
    }

    private static OperationResult<T> Invalid<T>(string code, FrameworkErrorCategory category, string fallback, RequestContext context, bool retryable = false)
    {
        return OperationResult<T>.Failed(FrameworkErrors.Create(code, category, fallback, context == null ? null : context.CorrelationId, retryable, ErrorOwner));
    }

    private async Task<OperationResult<T>> ExecuteAsync<T>(string area, RequestContext context, CancellationToken callerToken, Func<CancellationToken, OperationResult<T>> operation)
    {
        if (Volatile.Read(ref disposed) != 0) return Invalid<T>(area + ".disposed", FrameworkErrorCategory.Unavailable, "The asset store is disposed.", context);
        if (context == null) return Invalid<T>(area + ".invalid_request", FrameworkErrorCategory.InvalidRequest, "A request context is required.", null);
        if (callerToken == CancellationToken.None) return Invalid<T>(area + ".cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context);
        if (callerToken.IsCancellationRequested || context.CancellationToken.IsCancellationRequested) return Invalid<T>("awake.cancelled", FrameworkErrorCategory.Cancelled, "The operation was cancelled.", context);
        if (context.IsExpiredAt(DateTimeOffset.UtcNow)) return Invalid<T>(area + ".deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context);

        using var callerAndSession = CancellationTokenSource.CreateLinkedTokenSource(callerToken, context.CancellationToken);
        var remaining = context.Deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return Invalid<T>(area + ".deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context);
        CancellationTokenSource deadlineSource = null;
        if (remaining < TimeSpan.FromMilliseconds(int.MaxValue))
        {
            deadlineSource = new CancellationTokenSource();
            deadlineSource.CancelAfter(remaining);
        }

        using (deadlineSource)
        using (var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(callerAndSession.Token, deadlineSource?.Token ?? CancellationToken.None))
        {
            var gateAcquired = false;
            try
            {
                await gate.WaitAsync(operationCancellation.Token).ConfigureAwait(false);
                gateAcquired = true;
                if (Volatile.Read(ref disposed) != 0) return Invalid<T>(area + ".disposed", FrameworkErrorCategory.Unavailable, "The asset store is disposed.", context);
                operationCancellation.Token.ThrowIfCancellationRequested();
                // 文件 IO 是同步的：扔到线程池上做，门由这个后台线程持有，调用方的线程不被 IO 占住。
                var task = Task.Run(() => operation(operationCancellation.Token), CancellationToken.None);
                return await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (deadlineSource != null && deadlineSource.IsCancellationRequested && !callerToken.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
                {
                    return Invalid<T>(area + ".deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context);
                }
                return Invalid<T>("awake.cancelled", FrameworkErrorCategory.Cancelled, "The operation was cancelled.", context);
            }
            catch (UnauthorizedAccessException)
            {
                return Invalid<T>(area + ".store_unavailable", FrameworkErrorCategory.Unavailable, "The asset store path is not accessible.", context, true);
            }
            catch (IOException)
            {
                return Invalid<T>(area + ".store_unavailable", FrameworkErrorCategory.Unavailable, "The asset store path is not available.", context, true);
            }
            finally
            {
                if (gateAcquired) gate.Release();
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly JsonSerializerOptions MetadataJsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>元数据落盘形态。字段名用下划线，与设计大纲 §6 列的那组概念名逐字对应。</summary>
    private sealed class AssetMetadataRecord
    {
        [JsonPropertyName("asset_id")] public string AssetId { get; set; }

        [JsonPropertyName("content_hash")] public string ContentHash { get; set; }

        [JsonPropertyName("media_type")] public string MediaType { get; set; }

        [JsonPropertyName("byte_length")] public long ByteLength { get; set; }

        [JsonPropertyName("logical_kind")] public string LogicalKind { get; set; }

        [JsonPropertyName("created_by_task")] public string CreatedByTask { get; set; }

        [JsonPropertyName("owner_extension_id")] public string OwnerExtensionId { get; set; }

        [JsonPropertyName("campaign_id")] public string CampaignId { get; set; }

        [JsonPropertyName("timeline_id")] public string TimelineId { get; set; }

        [JsonPropertyName("provenance")] public string Provenance { get; set; }

        [JsonPropertyName("retention_class")] public string RetentionClass { get; set; }

        [JsonPropertyName("created_utc")] public DateTimeOffset CreatedUtc { get; set; }

        [JsonPropertyName("pinned")] public bool Pinned { get; set; }

        internal AssetHandle ToHandle()
        {
            return new AssetHandle(
                AssetId,
                ContentHash,
                MediaType,
                ByteLength,
                LogicalKind,
                CreatedByTask,
                new ExtensionId(OwnerExtensionId),
                CampaignId,
                TimelineId,
                Provenance,
                RetentionClass);
        }
    }
}
