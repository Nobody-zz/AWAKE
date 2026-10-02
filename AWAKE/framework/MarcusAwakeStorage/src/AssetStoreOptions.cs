using System;

namespace MarcusAwakeStorage;

/// <summary>
/// 资产库（内容寻址 CAS）的限额。
///
/// <para>与 <see cref="SqliteStorageOptions"/> <b>刻意分开</b>：资产落在文件系统上，不是 SQLite 行，
/// 两者的物理上限不是一回事。把 8 MB 的图片上限塞进「键值 / RAG 文档」的限额表里，会让
/// 「存储值多大」和「一张图多大」这两件无关的事互相牵制。</para>
/// </summary>
public sealed class AssetStoreOptions
{
    /// <summary>
    /// 单个资产字节上限。默认 8 MB，与 Provider 侧 <c>ProviderImageMedia.DefaultMaxAssetBytes</c> 同值 ——
    /// 两边都卡在同一个数上，超限在**进库之前**就被拦住，而不是先收下再发现存不了。
    /// </summary>
    public int MaxAssetBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>一次列出（<c>ListAsync</c>）最多返回多少条。</summary>
    public int MaxListResults { get; set; } = 256;

    /// <summary>一次清理（<c>CleanupAsync</c>）最多处理多少条。</summary>
    public int MaxCleanupItems { get; set; } = 1024;

    internal void Validate()
    {
        ValidatePositive(MaxAssetBytes, nameof(MaxAssetBytes), 64 * 1024 * 1024);
        ValidatePositive(MaxListResults, nameof(MaxListResults), 4096);
        ValidatePositive(MaxCleanupItems, nameof(MaxCleanupItems), 65536);
    }

    private static void ValidatePositive(int value, string name, int maximum)
    {
        if (value < 1 || value > maximum) throw new ArgumentOutOfRangeException(name);
    }
}
