using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using MarcusAwakeStorage;

namespace MarcusAwakeStorage.Tests;

/// <summary>
/// 内容寻址资产库（<see cref="ContentAddressedAssetStore"/>）的判据。
///
/// <para>这组用例对着设计大纲 §7（输入输出验证）、§9（保留与清理）、§11（验收）写。
/// 其中三条是**必须响**的：内容不合规不得进入可用路径、声明类型不得覆盖实到字节、
/// 删一条元数据不得误删仍被别的记录引用的对象。</para>
/// </summary>
internal static class AssetStoreTests
{
    internal static async Task RunImportReadDedupAsync()
    {
        var root = NewAssetRoot();
        try
        {
            using var store = new ContentAddressedAssetStore(root);
            using var session = new AssetSession("session-import", "fixture.asset.owner.a");
            var png = PngBytes(64, 7);

            var first = await store.ImportAsync(new AssetImportRequest(png, "image/png", "portrait", "task-portrait", "player2", "campaign"), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(first);
            var second = await store.ImportAsync(new AssetImportRequest(png, "image/png", "portrait", "task-portrait", "player2", "campaign"), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(second);

            AssertEqual(Sha256Hex(png), first.Value.ContentHash, "the handle must carry the content hash");
            AssertEqual("image/png", first.Value.MediaType, "the handle must carry the detected media type");
            AssertEqual((long)png.Length, first.Value.ByteLength, "the handle must carry the byte length");
            AssertEqual("campaign", first.Value.RetentionClass, "an unspecified retention class must default to campaign");
            AssertEqual(session.Caller.Value, first.Value.OwnerExtensionId.Value, "the handle must be owned by the caller");
            if (StringComparer.Ordinal.Equals(first.Value.AssetId, second.Value.AssetId)) throw new InvalidOperationException("two imports must receive distinct asset IDs");
            AssertEqual(first.Value.ContentHash, second.Value.ContentHash, "identical bytes must share a content hash");

            AssertEqual(1, CountFiles(Path.Combine(root, "objects")), "identical bytes must be stored once");
            AssertEqual(2, CountFiles(Path.Combine(root, "metadata")), "each import must keep its own metadata record");

            var read = await store.ReadAsync(first.Value.AssetId, session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(read);
            AssertBytes(png, read.Value.GetContentCopy(), "the read bytes must equal the imported bytes");

            // 引用安全删除：还有一条元数据指向同一个哈希 ⇒ 对象必须留下。
            var deleted = await store.DeleteAsync(first.Value.AssetId, session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(deleted);
            AssertEqual(1, CountFiles(Path.Combine(root, "objects")), "the object must survive while another record still references it");
            AssertSuccess(await store.ReadAsync(second.Value.AssetId, session.Context, session.Token).ConfigureAwait(false));

            AssertSuccess(await store.DeleteAsync(second.Value.AssetId, session.Context, session.Token).ConfigureAwait(false));
            AssertEqual(0, CountFiles(Path.Combine(root, "objects")), "the object must be reclaimed once the last reference is gone");
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    internal static async Task RunRejectsAndQuarantineAsync()
    {
        var root = NewAssetRoot();
        try
        {
            using var store = new ContentAddressedAssetStore(root);
            using var session = new AssetSession("session-reject", "fixture.asset.owner.a");
            var text = Encoding.UTF8.GetBytes("this is not an image at all");

            AssertFailure(await store.ImportAsync(new AssetImportRequest(text, "image/png", "portrait", "task", "test", "campaign"), session.Context, session.Token).ConfigureAwait(false), "asset.content_format_unknown");
            AssertEqual(1, CountFiles(Path.Combine(root, "quarantine")), "rejected content must be quarantined for forensics");

            // 声明与实到字节不符 ⇒ 拒收。只信 magic，不信声明（设计大纲 §7）。
            AssertFailure(await store.ImportAsync(new AssetImportRequest(PngBytes(32, 1), "image/jpeg", "portrait", "task", "test", "campaign"), session.Context, session.Token).ConfigureAwait(false), "asset.content_type_mismatch");
            AssertFailure(await store.ImportAsync(new AssetImportRequest(JpegBytes(32, 1), "image/png", "portrait", "task", "test", "campaign"), session.Context, session.Token).ConfigureAwait(false), "asset.content_type_mismatch");
            AssertEqual(3, CountFiles(Path.Combine(root, "quarantine")), "every rejected payload must be quarantined");

            // 未声明类型时按实到字节判定 —— 这不是「未知」，是「让它自己说」。
            var undeclared = await store.ImportAsync(new AssetImportRequest(JpegBytes(32, 1), string.Empty, "portrait", "task", "test", "campaign"), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(undeclared);
            AssertEqual("image/jpeg", undeclared.Value.MediaType, "an undeclared media type must be resolved from the payload");

            AssertFailure(await store.ImportAsync(new AssetImportRequest(Array.Empty<byte>(), "image/png", "portrait", "task", "test", "campaign"), session.Context, session.Token).ConfigureAwait(false), "asset.empty_content");
            AssertFailure(await store.ImportAsync(new AssetImportRequest(PngBytes(32, 1), "image/png", "portrait", "task", "test", "forever"), session.Context, session.Token).ConfigureAwait(false), "asset.retention_unknown");

            using var tiny = new ContentAddressedAssetStore(root, new AssetStoreOptions { MaxAssetBytes = 64 });
            AssertFailure(await tiny.ImportAsync(new AssetImportRequest(PngBytes(256, 1), "image/png", "portrait", "task", "test", "campaign"), session.Context, session.Token).ConfigureAwait(false), "asset.content_too_large");
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    internal static async Task RunScopeOwnershipAndPinAsync()
    {
        var root = NewAssetRoot();
        try
        {
            using var store = new ContentAddressedAssetStore(root);
            using var ownerA = new AssetSession("session-scope-a", "fixture.asset.owner.a");
            using var ownerB = new AssetSession("session-scope-b", "fixture.asset.owner.b");
            var imported = await store.ImportAsync(new AssetImportRequest(PngBytes(48, 3), "image/png", "portrait", "task", "test", "campaign"), ownerA.Context, ownerA.Token).ConfigureAwait(false);
            AssertSuccess(imported);
            var assetId = imported.Value.AssetId;

            AssertFailure(await store.GetMetadataAsync(assetId, ownerB.Context, ownerB.Token).ConfigureAwait(false), "asset.scope_denied");
            AssertFailure(await store.ReadAsync(assetId, ownerB.Context, ownerB.Token).ConfigureAwait(false), "asset.scope_denied");
            AssertFailure(await store.DeleteAsync(assetId, ownerB.Context, ownerB.Token).ConfigureAwait(false), "asset.scope_denied");
            AssertFailure(await store.SetPinnedAsync(assetId, true, ownerB.Context, ownerB.Token).ConfigureAwait(false), "asset.scope_denied");

            var foreignList = await store.ListAsync(16, string.Empty, ownerB.Context, ownerB.Token).ConfigureAwait(false);
            AssertSuccess(foreignList);
            AssertEqual(0, foreignList.Value.Items.Count, "a list must never include another owner's assets");

            AssertSuccess(await store.SetPinnedAsync(assetId, true, ownerA.Context, ownerA.Token).ConfigureAwait(false));
            var metadata = await store.GetMetadataAsync(assetId, ownerA.Context, ownerA.Token).ConfigureAwait(false);
            AssertSuccess(metadata);
            AssertEqual(true, metadata.Value.Pinned, "pinning must be observable through the metadata");
            AssertFailure(await store.DeleteAsync(assetId, ownerA.Context, ownerA.Token).ConfigureAwait(false), "asset.pinned");

            AssertSuccess(await store.SetPinnedAsync(assetId, false, ownerA.Context, ownerA.Token).ConfigureAwait(false));
            AssertSuccess(await store.DeleteAsync(assetId, ownerA.Context, ownerA.Token).ConfigureAwait(false));
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    internal static async Task RunListPagingAndCleanupAsync()
    {
        var root = NewAssetRoot();
        try
        {
            using var store = new ContentAddressedAssetStore(root);
            using var session = new AssetSession("session-list", "fixture.asset.owner.a");
            var ids = new List<string>();
            for (var index = 0; index < 3; index++)
            {
                var imported = await store.ImportAsync(new AssetImportRequest(PngBytes(24 + index, (byte)(index + 1)), "image/png", "portrait", "task", "test", "campaign"), session.Context, session.Token).ConfigureAwait(false);
                AssertSuccess(imported);
                ids.Add(imported.Value.AssetId);
            }
            ids.Sort(StringComparer.Ordinal);

            var firstPage = await store.ListAsync(2, string.Empty, session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(firstPage);
            AssertEqual(2, firstPage.Value.Items.Count, "the first page must honour the page size");
            if (firstPage.Value.NextCursor.Length == 0) throw new InvalidOperationException("a page that is not the last must return a cursor");
            var secondPage = await store.ListAsync(2, firstPage.Value.NextCursor, session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(secondPage);
            AssertEqual(1, secondPage.Value.Items.Count, "the second page must return the remainder");
            AssertEqual(string.Empty, secondPage.Value.NextCursor, "the last page must not return a cursor");
            var walked = firstPage.Value.Items.Concat(secondPage.Value.Items).Select(item => item.Handle.AssetId).ToList();
            AssertEqual(string.Join(",", ids), string.Join(",", walked), "paging must walk every asset exactly once, in order");

            // 干跑只回答「有多少条符合条件」，不动数据。
            var dryRun = await store.CleanupAsync(new AssetCleanupRequest(DateTimeOffset.UtcNow.AddMinutes(1), new[] { "campaign" }, 16, true), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(dryRun);
            AssertEqual(3, dryRun.Value.EligibleItems, "a dry run must report every eligible item");
            AssertEqual(0, dryRun.Value.DeletedItems, "a dry run must not delete anything");
            var afterDryRun = await store.ListAsync(16, string.Empty, session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(afterDryRun);
            AssertEqual(3, afterDryRun.Value.Items.Count, "a dry run must leave the store untouched");

            // 保留等级不匹配 ⇒ 一条都不该被选中。
            var mismatched = await store.CleanupAsync(new AssetCleanupRequest(DateTimeOffset.UtcNow.AddMinutes(1), new[] { "ephemeral" }, 16, true), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(mismatched);
            AssertEqual(0, mismatched.Value.EligibleItems, "cleanup must not touch retention classes that were not requested");

            // 年龄阈值：比阈值新的不许动。
            var tooNew = await store.CleanupAsync(new AssetCleanupRequest(DateTimeOffset.UtcNow.AddMinutes(-1), new[] { "campaign" }, 16, false), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(tooNew);
            AssertEqual(0, tooNew.Value.DeletedItems, "cleanup must respect the age threshold");

            var cleanup = await store.CleanupAsync(new AssetCleanupRequest(DateTimeOffset.UtcNow.AddMinutes(1), new[] { "campaign" }, 16, false), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(cleanup);
            AssertEqual(3, cleanup.Value.DeletedItems, "cleanup must delete every eligible item");
            AssertEqual(3, cleanup.Value.ReclaimedObjects, "cleanup must reclaim the orphaned objects");
            AssertEqual(0, CountFiles(Path.Combine(root, "objects")), "cleanup must not leave orphaned objects behind");
            var empty = await store.ListAsync(16, string.Empty, session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(empty);
            AssertEqual(0, empty.Value.Items.Count, "cleanup must remove the metadata records too");
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    internal static async Task RunExportAndGuardsAsync()
    {
        var root = NewAssetRoot();
        try
        {
            using var store = new ContentAddressedAssetStore(root);
            using var session = new AssetSession("session-export", "fixture.asset.owner.a");
            var png = PngBytes(40, 5);
            var imported = await store.ImportAsync(new AssetImportRequest(png, "image/png", "portrait", "task", "player2", "persistent"), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(imported);

            var receipt = await store.ExportAsync(imported.Value.AssetId, session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(receipt);
            AssertEqual(imported.Value.AssetId, receipt.Value.AssetId, "the receipt must name the exported asset");
            AssertEqual((long)png.Length, receipt.Value.PackageByteLength, "the receipt must report the package size");
            AssertEqual(Sha256Hex(png), receipt.Value.PackageSha256, "a single-asset package is the asset's own bytes");
            var packagePath = Path.Combine(root, "exports", receipt.Value.ExportId + ".pkg");
            if (!File.Exists(packagePath)) throw new InvalidOperationException("the export must leave a package file behind");
            AssertBytes(png, File.ReadAllBytes(packagePath), "the exported package must contain the asset bytes");

            AssertFailure(await store.ReadAsync("not-an-asset-id", session.Context, session.Token).ConfigureAwait(false), "asset.invalid_request");
            AssertFailure(await store.ReadAsync(new string('0', 32), session.Context, session.Token).ConfigureAwait(false), "asset.not_found");
            // 房规：没给取消令牌要报出来，不是默认放行。
            AssertFailure(await store.ReadAsync(imported.Value.AssetId, session.Context, CancellationToken.None).ConfigureAwait(false), "asset.cancellation_token_missing");
            AssertFailure(await store.ListAsync(0, string.Empty, session.Context, session.Token).ConfigureAwait(false), "asset.invalid_request");
            AssertFailure(await store.ListAsync(16, "not-an-asset-id", session.Context, session.Token).ConfigureAwait(false), "asset.invalid_request");

            using (var expired = new AssetSession("session-expired", "fixture.asset.owner.a", DateTimeOffset.UtcNow.AddSeconds(-1)))
            {
                AssertFailure(await store.ReadAsync(imported.Value.AssetId, expired.Context, expired.Token).ConfigureAwait(false), "asset.deadline_expired");
            }
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                AssertFailure(await store.ReadAsync(imported.Value.AssetId, session.Context, cancelled.Token).ConfigureAwait(false), "awake.cancelled");
            }
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    internal static async Task RunCorruptionBoundariesAsync()
    {
        var root = NewAssetRoot();
        try
        {
            using var store = new ContentAddressedAssetStore(root);
            using var session = new AssetSession("session-corrupt", "fixture.asset.owner.a");
            var png = PngBytes(56, 9);
            var imported = await store.ImportAsync(new AssetImportRequest(png, "image/png", "portrait", "task", "test", "campaign"), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(imported);
            var assetId = imported.Value.AssetId;
            var objectPath = ObjectPath(root, imported.Value.ContentHash);

            // 对象被外部动过 ⇒ 数据完整性问题，不是「暂时读不到」。
            File.WriteAllBytes(objectPath, PngBytes(8, 9));
            AssertFailure(await store.ReadAsync(assetId, session.Context, session.Token).ConfigureAwait(false), "asset.object_corrupt");

            // 对象整个丢了 ⇒ 元数据还在，但字节没了。
            File.Delete(objectPath);
            AssertFailure(await store.ReadAsync(assetId, session.Context, session.Token).ConfigureAwait(false), "asset.object_missing");

            // 元数据读不出来 ⇒ 明确报 corrupt，不假装「这条不存在」。
            File.WriteAllText(Path.Combine(root, "metadata", assetId + ".json"), "{ this is not json");
            AssertFailure(await store.GetMetadataAsync(assetId, session.Context, session.Token).ConfigureAwait(false), "asset.metadata_corrupt");
            AssertFailure(await store.ReadAsync(assetId, session.Context, session.Token).ConfigureAwait(false), "asset.metadata_corrupt");

            // 有一条元数据读不出来时，引用扫描不能假装「没人引用」——
            // 宁可留一个对象，也不能删掉可能还有人要的字节。
            var second = await store.ImportAsync(new AssetImportRequest(PngBytes(64, 11), "image/png", "portrait", "task", "test", "campaign"), session.Context, session.Token).ConfigureAwait(false);
            AssertSuccess(second);
            var secondObjectPath = ObjectPath(root, second.Value.ContentHash);
            AssertSuccess(await store.DeleteAsync(second.Value.AssetId, session.Context, session.Token).ConfigureAwait(false));
            if (!File.Exists(secondObjectPath)) throw new InvalidOperationException("an unreadable metadata record must block object reclamation");
        }
        finally
        {
            CleanupRoot(root);
        }
    }

    // ------------------------------------------------------------------ 夹具

    private sealed class AssetSession : IDisposable
    {
        private readonly SessionCoordinator coordinator;
        private readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

        internal AssetSession(string sessionId, string callerId, DateTimeOffset? deadline = null)
        {
            Caller = new ExtensionId(callerId);
            Reference = new SessionRef("campaign-assets", "timeline-assets", sessionId);
            coordinator = new SessionCoordinator();
            var begun = coordinator.BeginSession(Reference);
            if (begun == null || !begun.IsSuccess) throw new InvalidOperationException("Expected a session lease but received " + begun?.Error?.Code);
            Lease = begun.Value;
            Context = new RequestContext(Caller, Lease, "corr-" + sessionId, deadline ?? DateTimeOffset.UtcNow.AddMinutes(5));
        }

        internal ExtensionId Caller { get; }

        internal SessionRef Reference { get; }

        internal SessionLease Lease { get; }

        internal RequestContext Context { get; }

        /// <summary>调用方取消令牌。<b>不能传 <c>CancellationToken.None</c></b> —— 资产库沿用存储后端的房规：
        /// 「没给取消令牌」是一个要报出来的调用错误（<c>asset.cancellation_token_missing</c>），不是默认放行。</summary>
        internal CancellationToken Token => tokenSource.Token;

        public void Dispose()
        {
            if (Lease.State == SessionState.Ready)
            {
                var closing = coordinator.BeginClosing(Reference);
                if (closing.IsSuccess) coordinator.CompleteDrain(Reference, closing.Value.Generation);
            }
            tokenSource.Dispose();
        }
    }

    private static byte[] PngBytes(int payloadLength, byte seed)
    {
        var bytes = new byte[8 + payloadLength];
        Array.Copy(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes, 8);
        for (var index = 0; index < payloadLength; index++) bytes[8 + index] = (byte)(seed + index);
        return bytes;
    }

    private static byte[] JpegBytes(int payloadLength, byte seed)
    {
        var bytes = new byte[3 + payloadLength];
        Array.Copy(new byte[] { 0xFF, 0xD8, 0xFF }, bytes, 3);
        for (var index = 0; index < payloadLength; index++) bytes[3 + index] = (byte)(seed + index);
        return bytes;
    }

    private static string NewAssetRoot()
    {
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures"));
        Directory.CreateDirectory(directory);
        var root = Path.Combine(directory, "assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupRoot(string root)
    {
        if (!Directory.Exists(root)) return;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(root, true);
                return;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static string ObjectPath(string root, string contentHash)
    {
        return Path.Combine(root, "objects", contentHash.Substring(0, 2), contentHash);
    }

    private static int CountFiles(string directory)
    {
        return Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count() : 0;
    }

    private static string Sha256Hex(byte[] content)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(content);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash) builder.Append(value.ToString("x2"));
        return builder.ToString();
    }

    private static void AssertSuccess<T>(OperationResult<T> result)
    {
        if (result == null || !result.IsSuccess) throw new InvalidOperationException("Expected success but received " + result?.Error?.Code);
    }

    private static void AssertFailure<T>(OperationResult<T> result, string code)
    {
        if (result == null || result.IsSuccess || !StringComparer.Ordinal.Equals(code, result.Error.Code)) throw new InvalidOperationException("Expected failure " + code + " but received " + result?.Error?.Code);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
    }

    private static void AssertBytes(byte[] expected, byte[] actual, string message)
    {
        if (actual == null || !expected.AsSpan().SequenceEqual(actual)) throw new InvalidOperationException(message + ": expected " + expected.Length + " bytes, actual " + (actual?.Length ?? -1));
    }
}
