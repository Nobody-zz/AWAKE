using System;
using System.Security.Cryptography;
using System.Text;

namespace MarcusAwakeStorage;

/// <summary>资产货物按 magic 认出来的真格式。</summary>
internal enum AssetContentFormat
{
    Unknown,
    Png,
    Jpeg,
    Gif,
    Webp,
    Bmp
}

/// <summary>一次鉴定的结果：真格式、与之对应的规范媒体类型、内容哈希、字节数。</summary>
internal sealed class AssetContentInfo
{
    internal AssetContentInfo(AssetContentFormat format, string mediaType, string contentHash, long byteLength)
    {
        Format = format;
        MediaType = mediaType;
        ContentHash = contentHash;
        ByteLength = byteLength;
    }

    internal AssetContentFormat Format { get; }

    /// <summary>规范媒体类型；<see cref="AssetContentFormat.Unknown"/> 时为空串。</summary>
    internal string MediaType { get; }

    internal string ContentHash { get; }

    internal long ByteLength { get; }
}

/// <summary>
/// 资产货物的鉴定工具：按 <b>magic / 文件头</b> 认真格式，并算内容哈希。
///
/// <para><b>为什么不采信调用方自称的媒体类型。</b> 设计大纲 §7 的原话是「通过 magic/header 与实际解码
/// 验证格式，不只信扩展名或 MIME」。这不是洁癖：Provider 侧实测过服务端自称的 mime 与真内容不符
/// （见 <c>ProviderImageMedia</c> 的注释）。所以库里另立一道 —— <b>存储不该信任它的调用方</b>，
/// 哪怕那个调用方是自家的运行时。</para>
///
/// <para><b>为什么这里不直接复用 <c>ProviderImageMedia.SniffFormat</c>。</b> 那个在
/// <c>MarcusAwakeProvider</c>（net8.0）里，而资产库住在 <c>MarcusAwakeStorage</c>，两者不互相引用
/// —— Provider 只引用 Transport，Storage 只引用 Framework。跨过依赖图去复用一个 30 行的嗅探器，
/// 代价比这 30 行本身大得多。这一份是**第二道防线**，不是第一道的替代品。</para>
///
/// <para><b>认不出来的东西一律拒收并进 quarantine</b>（设计大纲 §7）。SVG / HTML / 脚本 / 可执行内容
/// 没有图片 magic，因此天然落进「unknown」这一格 —— 伪装成图片的脚本在<b>入库之前</b>就被挡下，
/// 而不是等到游戏侧加载资源时才炸。</para>
///
/// <para><b>已知范围</b>：只认图片格式。音频（TTS）本次未 port（迁移计划 §7 明列不做），
/// 所以没有 wav/mp3 的 magic 表；真做 TTS 时在这里补一格即可。</para>
/// </summary>
internal static class AssetContentInspector
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    internal static AssetContentInfo Detect(byte[] content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        var hash = Sha256Hex(content);
        var format = DetectFormat(content);
        return new AssetContentInfo(format, MediaTypeFor(format), hash, content.LongLength);
    }

    internal static AssetContentFormat DetectFormat(byte[] content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        if (StartsWith(content, PngSignature)) return AssetContentFormat.Png;
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF) return AssetContentFormat.Jpeg;
        if (content.Length >= 6 && content[0] == (byte)'G' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'8')
        {
            // "GIF8" —— 第 5 个字符是 '7' 或 '9'（87a / 89a）。只认前四个字节。
            return AssetContentFormat.Gif;
        }
        if (content.Length >= 12
            && content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
            && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P')
        {
            return AssetContentFormat.Webp;
        }
        if (content.Length >= 2 && content[0] == (byte)'B' && content[1] == (byte)'M') return AssetContentFormat.Bmp;
        return AssetContentFormat.Unknown;
    }

    internal static string MediaTypeFor(AssetContentFormat format)
    {
        switch (format)
        {
            case AssetContentFormat.Png: return "image/png";
            case AssetContentFormat.Jpeg: return "image/jpeg";
            case AssetContentFormat.Gif: return "image/gif";
            case AssetContentFormat.Webp: return "image/webp";
            case AssetContentFormat.Bmp: return "image/bmp";
            default: return string.Empty;
        }
    }

    /// <summary>
    /// 把调用方声明的媒体类型规范到可比较的形态。
    ///
    /// <para>空串与 <c>application/octet-stream</c> 一律归为 <b>「未声明」</b>（返回空串）—— 后者是
    /// Provider 侧 <c>ProviderImageMedia.UnknownMediaType</c> 的取值，含义正是「我不知道这是什么」，
    /// 把它当成一个需要匹配的声明会让「未知」变成「错误」。<c>image/jpg</c> 归一到
    /// <c>image/jpeg</c>：同一个格式的两个写法，不该被判成不匹配。</para>
    /// </summary>
    internal static string NormalizeDeclaredMediaType(string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType)) return string.Empty;
        var trimmed = mediaType.Trim();
        if (StringComparer.OrdinalIgnoreCase.Equals(trimmed, "application/octet-stream")) return string.Empty;
        if (StringComparer.OrdinalIgnoreCase.Equals(trimmed, "image/jpg")) return "image/jpeg";
        return trimmed.ToLowerInvariant();
    }

    internal static string Sha256Hex(byte[] content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(content));
    }

    internal static string ToHex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        for (var index = 0; index < bytes.Length; index++) builder.Append(bytes[index].ToString("x2"));
        return builder.ToString();
    }

    private static bool StartsWith(byte[] content, byte[] prefix)
    {
        if (content.Length < prefix.Length) return false;
        for (var index = 0; index < prefix.Length; index++)
        {
            if (content[index] != prefix[index]) return false;
        }
        return true;
    }
}
