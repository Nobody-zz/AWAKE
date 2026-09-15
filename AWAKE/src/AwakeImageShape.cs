using System;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake;

/// <summary>调用方递给形状适配器的东西。**只讲要什么，不讲往哪发。**</summary>
internal sealed class AwakeImageRequest
{
    internal AwakeImageRequest(string prompt, byte[] referencePng, int width, int height)
    {
        Prompt = prompt ?? string.Empty;
        ReferencePng = referencePng;
        Width = width;
        Height = height;
    }

    internal string Prompt { get; }

    /// <summary>可选的角色参考图（png 字节）。为 null ⇒ 纯文生图。</summary>
    internal byte[] ReferencePng { get; }

    internal int Width { get; }
    internal int Height { get; }

    internal bool HasReference
    {
        get { return ReferencePng != null && ReferencePng.Length > 0; }
    }
}

/// <summary>适配器解出来的东西：已经**剥掉 data URI 前缀、按 magic 认过格式**的真图字节。</summary>
internal sealed class AwakeImageReply
{
    internal AwakeImageReply(byte[] bytes, string format, int width, int height, string declaredMime)
    {
        Bytes = bytes ?? new byte[0];
        Format = format ?? "unknown";
        Width = width;
        Height = height;
        DeclaredMime = declaredMime ?? string.Empty;
    }

    internal byte[] Bytes { get; }

    /// <summary>按**文件头**认出来的真实格式：png / jpg / gif / webp / bmp / unknown。</summary>
    internal string Format { get; }

    /// <summary>从文件头读出来的真实像素。**不要拿请求里填的尺寸代替它** —— 服务端有权不遵守。</summary>
    internal int Width { get; }
    internal int Height { get; }

    /// <summary>服务端自称的 mime。**仅供参考，别信** —— 实测与真实格式不符过。</summary>
    internal string DeclaredMime { get; }

    internal string Describe()
    {
        return Format + " " + Width.ToString(CultureInfo.InvariantCulture)
            + "x" + Height.ToString(CultureInfo.InvariantCulture)
            + " " + Bytes.Length.ToString(CultureInfo.InvariantCulture) + "B";
    }
}

/// <summary>
/// 形状适配：URL 子路径、请求体、响应体三件事，按 <see cref="AwakeImageShape"/> 分派。
///
/// **两个方向都要防前缀，而且方向相反：**
///   出（带参考图）：火山方舟这类接口的 <c>image</c> 字段要**带** <c>data:image/…;base64,</c> 前缀。
///   进（收结果）：Player2 的 <c>image</c> 字段**也带**前缀，但它的文档写的是"不带"。
///   ⇒ 收的那侧一律先剥、再按 magic 认格式。**别按文档假定格式。**
/// </summary>
internal static class AwakeImageShapeAdapter
{
    internal static string EndpointPath(AwakeImageShape shape, bool hasReference)
    {
        if (shape == AwakeImageShape.Player2)
        {
            return hasReference ? "/image/edit" : "/image/generate";
        }

        return "/images/generations";
    }

    /// <summary>云端才带 Authorization。本机免认证服务带了反而可能被拒。</summary>
    internal static bool RequiresAuthorization(AwakeImageShape shape, bool isCloud)
    {
        return isCloud;
    }

    internal static string BuildRequestBody(AwakeImageShape shape, AwakeImageRequest request)
    {
        JObject body = new JObject();

        if (shape == AwakeImageShape.Player2)
        {
            body["prompt"] = request.Prompt;
            body["width"] = request.Width;
            body["height"] = request.Height;
            if (request.HasReference)
            {
                // Player2 的 /image/edit：单张走 image，多张走 images。
                body["image"] = Convert.ToBase64String(request.ReferencePng);
            }

            return body.ToString(Formatting.None);
        }

        // OpenAI 形状。参考图沿用火山方舟/百炼那一套：generations 里直接带 image 字段，
        // 值可以是 data URI、公网 URL 或裸 base64 —— 这里给 data URI，兼容面最宽。
        body["prompt"] = request.Prompt;
        body["size"] = request.Width.ToString(CultureInfo.InvariantCulture)
            + "x" + request.Height.ToString(CultureInfo.InvariantCulture);
        body["n"] = 1;
        body["response_format"] = "b64_json";
        if (request.HasReference)
        {
            body["image"] = ToDataUri("png", request.ReferencePng);
        }

        return body.ToString(Formatting.None);
    }

    /// <summary>
    /// 从响应体里挖出图片字节。挖不到 ⇒ 失败并带出服务端的人话错误（截断后）。
    /// </summary>
    internal static bool TryParseReply(
        AwakeImageShape shape,
        string body,
        out AwakeImageReply reply,
        out string error)
    {
        reply = null;
        error = string.Empty;

        JObject json;
        try
        {
            json = JObject.Parse(body ?? string.Empty);
        }
        catch (Exception ex)
        {
            error = "响应不是合法 JSON（" + ex.Message + "）：" + Truncate(body, 200);
            return false;
        }

        string declaredMime = ReadString(json, "mimetype") ?? ReadString(json, "mime_type") ?? string.Empty;
        string payload = shape == AwakeImageShape.Player2
            ? ReadString(json, "image") ?? ReadString(json, "images") ?? ReadString(json, "image_base64")
            : ReadDataB64(json) ?? ReadString(json, "image");

        if (string.IsNullOrWhiteSpace(payload))
        {
            error = "响应里没有图片字段：" + Truncate(body, 300);
            return false;
        }

        byte[] raw;
        try
        {
            string bare = StripDataUri(payload);
            raw = Convert.FromBase64String(bare);
        }
        catch (FormatException ex)
        {
            error = "图片字段不是合法 base64（" + ex.Message + "）。";
            return false;
        }

        if (raw.Length == 0)
        {
            error = "图片字段解出来是 0 字节。";
            return false;
        }

        string format = SniffFormat(raw);
        int width;
        int height;
        if (!TryReadDimensions(raw, format, out width, out height))
        {
            width = 0;
            height = 0;
        }

        reply = new AwakeImageReply(raw, format, width, height, declaredMime);
        return true;
    }

    /// <summary>
    /// 剥掉 <c>data:…;base64,</c> 前缀。
    ///
    /// **为什么非剥不可**：前缀里 <c>dataimage/jpegbase64</c> 这 20 个字符**全是 base64 合法字符**，
    /// 会被当数据解出 15 字节垃圾顶在文件头；又刚好 20 是 4 的倍数，
    /// 真数据落在 4 字符边界上 ⇒ 后面其实没坏，只是前面多了 15 字节。
    /// 症状是"头既不是 89504E47 也不是 FFD8FF、PIL 打不开、任何解压器都解不了"—— 最难查的那种。
    /// </summary>
    internal static string StripDataUri(string value)
    {
        string text = (value ?? string.Empty).Trim();
        if (!text.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return text;
        int comma = text.IndexOf(',');
        return comma < 0 ? text : text.Substring(comma + 1).Trim();
    }

    internal static string ToDataUri(string format, byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;
        string mime = StringComparer.OrdinalIgnoreCase.Equals(format, "jpg") ? "image/jpeg" : "image/" + format;
        return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
    }

    /// <summary>按文件头认格式。**认不出来就是 unknown，不做任何假定。**</summary>
    internal static string SniffFormat(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4) return "unknown";
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
        return "unknown";
    }

    /// <summary>
    /// 从文件头读真实像素。**§4.4 的落点**——服务端有权忽略请求里的尺寸
    /// （实测 Player2 的 /image/edit 传 256 照样出 1024×1024），所以落盘与布局都按这个值走。
    /// </summary>
    internal static bool TryReadDimensions(byte[] bytes, string format, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes == null) return false;

        if (StringComparer.Ordinal.Equals(format, "png"))
        {
            // 8 字节签名 + 4 字节长度 + 4 字节 "IHDR" ⇒ 宽在 16，高在 20，均大端。
            if (bytes.Length < 24) return false;
            width = ReadBigEndianInt32(bytes, 16);
            height = ReadBigEndianInt32(bytes, 20);
            return width > 0 && height > 0;
        }

        if (StringComparer.Ordinal.Equals(format, "jpg"))
        {
            return TryReadJpegDimensions(bytes, out width, out height);
        }

        if (StringComparer.Ordinal.Equals(format, "gif") && bytes.Length >= 10)
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
        int offset = 2;
        while (offset + 9 < bytes.Length)
        {
            if (bytes[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            byte marker = bytes[offset + 1];
            if (marker == 0xD8 || marker == 0xD9 || (marker >= 0xD0 && marker <= 0xD7) || marker == 0xFF)
            {
                offset += 2;
                continue;
            }

            int segmentLength = (bytes[offset + 2] << 8) | bytes[offset + 3];
            if (segmentLength < 2) return false;

            bool isStartOfFrame = (marker >= 0xC0 && marker <= 0xC3)
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

    private static string ReadString(JObject json, string property)
    {
        JToken token = json?[property];
        if (token == null || token.Type == JTokenType.Null) return null;
        if (token.Type == JTokenType.Array)
        {
            JArray array = (JArray)token;
            return array.Count == 0 ? null : array[0]?.ToString();
        }

        return token.ToString();
    }

    private static string ReadDataB64(JObject json)
    {
        JToken data = json?["data"];
        if (data == null || data.Type != JTokenType.Array) return null;
        JArray array = (JArray)data;
        if (array.Count == 0) return null;
        JToken first = array[0];
        return first?["b64_json"]?.ToString() ?? first?["b64"]?.ToString() ?? first?["url"]?.ToString();
    }

    private static string Truncate(string value, int limit)
    {
        string text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
        if (text.Length <= limit) return text;
        return text.Substring(0, limit) + "…";
    }
}
