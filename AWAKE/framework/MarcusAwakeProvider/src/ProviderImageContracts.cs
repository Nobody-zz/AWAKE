using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider;

/// <summary>
/// 要一张图。**只讲要什么，不讲往哪发** —— 端点、鉴权由 <see cref="ProviderConnectionProfile"/> 决定。
/// </summary>
public sealed class ProviderImageRequest
{
    public ProviderImageRequest(
        string prompt,
        int width = 0,
        int height = 0,
        string? negativePrompt = null,
        byte[]? referenceImage = null,
        string referenceMediaType = "png",
        string? model = null,
        string idempotencyKey = "")
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Image prompt is required.", nameof(prompt));
        Prompt = prompt;
        if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        NegativePrompt = negativePrompt ?? string.Empty;
        ReferenceImage = referenceImage != null && referenceImage.Length > 0 ? referenceImage : null;
        ReferenceMediaType = string.IsNullOrWhiteSpace(referenceMediaType) ? "png" : referenceMediaType;
        Model = string.IsNullOrWhiteSpace(model) ? null : model;
        IdempotencyKey = idempotencyKey ?? string.Empty;
    }

    public string Prompt { get; }
    public string NegativePrompt { get; }

    /// <summary>0 表示「用服务端默认」，不是「要 0 像素」。</summary>
    public int Width { get; }
    public int Height { get; }

    /// <summary>可选的角色参考图。null / 空 ⇒ 纯文生图。</summary>
    public byte[]? ReferenceImage { get; }
    public string ReferenceMediaType { get; }

    public string? Model { get; }

    /// <summary>重复的键由调用方负责稳定；空串表示不带这个头。</summary>
    public string IdempotencyKey { get; }

    public bool HasReference => ReferenceImage != null && ReferenceImage.Length > 0;
}

/// <summary>
/// 生成出来的二进制货物。
///
/// <para><b>MediaType 按 <em>magic 认出的真格式</em> 给，不采信服务端自称的 mime。</b>
/// 服务端自称的字段实测与真实内容不符过 ⇒ 不采信不是洁癖，是有案底。</para>
/// </summary>
public sealed class ProviderBinaryResult
{
    public ProviderBinaryResult(byte[] content, string mediaType, string resolvedModel)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        if (content.Length == 0) throw new ArgumentException("Binary result cannot be empty.", nameof(content));
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? ProviderImageMedia.UnknownMediaType : mediaType;
        ResolvedModel = resolvedModel ?? string.Empty;
    }

    public byte[] Content { get; }
    public string MediaType { get; }
    public string ResolvedModel { get; }
}

/// <summary>
/// 媒体能力接口。
///
/// <para><b>刻意不扩 <see cref="IProviderAdapter"/>。</b> 文字适配器不该被迫实现生图
/// （原版是「按 adapter 名分派 + 兜底抛错」，那会让每个文字适配器都长出一个假实现）。
/// 谁真会生图，谁实现这个接口；路由按「候选里有没有能生图的」过滤。</para>
/// </summary>
public interface IProviderImageAdapter
{
    Task<ProviderResult<ProviderBinaryResult>> GenerateImageAsync(
        ProviderImageRequest request,
        ApiKeyCredential? credential,
        DateTimeOffset deadline,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 图片字节的鉴定工具：剥 data URI 前缀、按 magic 认格式、从文件头读真实像素。
///
/// <para><b>剥前缀为什么非剥不可</b>（数字已核）：<c>data:image/jpeg;base64,</c> 去掉
/// <c>:</c> <c>;</c> <c>,</c> 之后剩 <c>dataimage/jpegbase64</c> —— 正好 <b>20 个字符、
/// 全是 base64 合法字符、且整除 4</b>。所以只要解码器对这些标点宽容（不少语言的解码器会
/// 跳过非法字符），它们就会被当数据处理，在文件头前多解出 <b>15 字节</b>垃圾；真数据落在
/// 4 字符边界上，后面其实没坏，只是前面多了一截。症状是「头既不是 89504E47 也不是 FFD8FF、
/// 任何看图工具都打不开」—— 最难查的那一种。</para>
///
/// <para>.NET 的 <see cref="Convert.FromBase64String(string)"/> 是严格的：碰到
/// <c>:</c> 会直接抛 <see cref="FormatException"/>，所以本模块的失败症状是「报 base64 非法」
/// 而不是「多 15 字节」。<b>两种症状，同一个根因：前缀没剥。</b></para>
/// </summary>
public static class ProviderImageMedia
{
    /// <summary>单次生成货物的字节上限。8 MB，与原版一致。</summary>
    public const int DefaultMaxAssetBytes = 8 * 1024 * 1024;

    public const string UnknownMediaType = "application/octet-stream";

    internal const string UnknownFormat = "unknown";

    public static string StripDataUri(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (!text.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return text;
        var comma = text.IndexOf(',');
        return comma < 0 ? text : text.Substring(comma + 1).Trim();
    }

    public static string ToDataUri(string format, byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;
        return "data:" + MediaTypeFor(format) + ";base64," + Convert.ToBase64String(bytes);
    }

    /// <summary>按文件头认格式。**认不出来就是 unknown，不做任何假定。**</summary>
    public static string SniffFormat(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < 4) return UnknownFormat;
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "png";
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "jpg";
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38) return "gif";
        if (bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "webp";
        }

        if (bytes[0] == 0x42 && bytes[1] == 0x4D) return "bmp";
        return UnknownFormat;
    }

    public static string MediaTypeFor(string? format)
    {
        if (format == null) return UnknownMediaType;
        if (StringComparer.OrdinalIgnoreCase.Equals(format, "png")) return "image/png";
        if (StringComparer.OrdinalIgnoreCase.Equals(format, "jpg")) return "image/jpeg";
        if (StringComparer.OrdinalIgnoreCase.Equals(format, "gif")) return "image/gif";
        if (StringComparer.OrdinalIgnoreCase.Equals(format, "webp")) return "image/webp";
        if (StringComparer.OrdinalIgnoreCase.Equals(format, "bmp")) return "image/bmp";
        return UnknownMediaType;
    }

    /// <summary>
    /// 从文件头读真实像素。
    ///
    /// <para><b>不要拿请求里填的尺寸代替它</b> —— 服务端有权不遵守
    /// （实测 Player2 的 <c>/image/edit</c> 传 256 照样出 1024×1024），
    /// 所以落盘与布局都得以这个值为准。</para>
    /// </summary>
    public static bool TryReadDimensions(byte[]? bytes, string? format, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes == null || format == null) return false;

        if (StringComparer.OrdinalIgnoreCase.Equals(format, "png"))
        {
            // 8 字节签名 + 4 字节长度 + 4 字节 "IHDR" ⇒ 宽在 16、高在 20，均大端。
            if (bytes.Length < 24) return false;
            width = ReadBigEndianInt32(bytes, 16);
            height = ReadBigEndianInt32(bytes, 20);
            return width > 0 && height > 0;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(format, "jpg")) return TryReadJpegDimensions(bytes, out width, out height);

        if (StringComparer.OrdinalIgnoreCase.Equals(format, "gif") && bytes.Length >= 10)
        {
            width = bytes[6] | (bytes[7] << 8);
            height = bytes[8] | (bytes[9] << 8);
            return width > 0 && height > 0;
        }

        return false;
    }

    private static bool TryReadJpegDimensions(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        var offset = 2;
        while (offset + 9 < bytes.Length)
        {
            if (bytes[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            var marker = bytes[offset + 1];
            if (marker == 0xD8 || marker == 0xD9 || (marker >= 0xD0 && marker <= 0xD7) || marker == 0xFF)
            {
                offset += 2;
                continue;
            }

            var segmentLength = (bytes[offset + 2] << 8) | bytes[offset + 3];
            if (segmentLength < 2) return false;

            var isStartOfFrame = (marker >= 0xC0 && marker <= 0xC3)
                || (marker >= 0xC5 && marker <= 0xC7)
                || (marker >= 0xC9 && marker <= 0xCB)
                || (marker >= 0xCD && marker <= 0xCF);
            if (isStartOfFrame)
            {
                if (offset + 9 >= bytes.Length) return false;
                height = (bytes[offset + 5] << 8) | bytes[offset + 6];
                width = (bytes[offset + 7] << 8) | bytes[offset + 8];
                return width > 0 && height > 0;
            }

            offset += 2 + segmentLength;
        }

        return false;
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }
}
