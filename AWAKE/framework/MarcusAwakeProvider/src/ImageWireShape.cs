using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MarcusAwakeProvider;

/// <summary>
/// 出图这条线上的「形状」：请求体怎么写、响应体怎么挖、挖出来的东西怎么鉴定。
///
/// <para>两家（Player2 / OpenAI 兼容）只在 **URL 子路径** 和 **响应字段名** 上不同，
/// 剥前缀、认 magic、验字节上限这套是共用的，所以放一处，别抄两遍。</para>
/// </summary>
internal static class ImageWireShape
{
    /// <summary>Player2：无参考图走 generate，有参考图走 edit。**这个分叉是实测出来的，不是文档写的。**</summary>
    internal const string Player2GeneratePath = "image/generate";
    internal const string Player2EditPath = "image/edit";
    internal const string OpenAiImagesPath = "images/generations";

    internal static byte[] BuildPlayer2Body(ProviderImageRequest request)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("prompt", request.Prompt);
            writer.WriteNumber("width", request.Width);
            writer.WriteNumber("height", request.Height);
            if (request.NegativePrompt.Length > 0) writer.WriteString("negative_prompt", request.NegativePrompt);
            if (request.HasReference)
            {
                // Player2 的 /image/edit：单张走 image，多张走 images。我们一次只要一张。
                writer.WriteString("image", Convert.ToBase64String(request.ReferenceImage!));
            }

            writer.WriteEndObject();
        }

        return output.ToArray();
    }

    internal static byte[] BuildOpenAiBody(ProviderImageRequest request, string model)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            writer.WriteString("prompt", request.Prompt);
            writer.WriteString("size", FormatSize(request));
            writer.WriteNumber("n", 1);

            // 显式要 base64：不要 url，省掉「按响应里的地址再去取一次」那条岔路。
            writer.WriteString("response_format", "b64_json");
            if (request.HasReference) writer.WriteString("image", ProviderImageMedia.ToDataUri(request.ReferenceMediaType, request.ReferenceImage));
            writer.WriteEndObject();
        }

        return output.ToArray();
    }

    internal static string FormatSize(ProviderImageRequest request)
    {
        return request.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "x" + request.Height.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>按名字依次找第一个非空字符串。找不到返回 null，不猜。</summary>
    internal static string? ReadFirstString(JsonElement container, params string[] names)
    {
        foreach (var name in names)
        {
            if (!container.TryGetProperty(name, out var element)) continue;
            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
                continue;
            }

            // Player2 的 images 可能是数组；取第一个元素，不再往下猜。
            if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
            {
                var first = element[0];
                if (first.ValueKind == JsonValueKind.String)
                {
                    var text = first.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
        }

        return null;
    }

    internal static ProviderResult<string> ReadPlayer2Payload(JsonElement root, string providerId, string model)
    {
        var payload = ReadFirstString(root, "image", "images", "image_base64");
        return payload == null
            ? ProviderResult<string>.Failed(Malformed("media.image_payload_invalid", "Player2 omitted base64 image data.", providerId, model))
            : ProviderResult<string>.Succeeded(payload);
    }

    internal static ProviderResult<string> ReadOpenAiPayload(JsonElement root, string providerId, string model)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
        {
            return ProviderResult<string>.Failed(Malformed("media.image_payload_invalid", "The provider returned no image data.", providerId, model));
        }

        var first = data[0];
        var payload = ReadFirstString(first, "b64_json", "b64", "image");
        if (payload != null) return ProviderResult<string>.Succeeded(payload);

        // 只有 url、没有内联字节。**不跟着这个 url 去取** —— 见 media.image_url_unsupported 的说明。
        if (ReadFirstString(first, "url") != null)
        {
            return ProviderResult<string>.Failed(new ProviderError(
                "media.image_url_unsupported",
                ProviderErrorCategory.Unsupported,
                "The provider returned an image URL instead of inline base64 data; downloading is not enabled.",
                false,
                providerId,
                model));
        }

        return ProviderResult<string>.Failed(Malformed("media.image_payload_invalid", "The provider omitted base64 image data.", providerId, model));
    }

    /// <summary>
    /// 把 base64 串变成货：**先剥 data URI 前缀，再解码，再按 magic 定 MediaType。**
    /// 这三步的顺序不能换，也不能省其中任何一步。
    /// </summary>
    internal static ProviderResult<ProviderBinaryResult> DecodeAsset(string? payload, string providerId, string model, int maxAssetBytes)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return ProviderResult<ProviderBinaryResult>.Failed(Malformed("media.image_payload_invalid", "The provider returned empty image data.", providerId, model));
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(ProviderImageMedia.StripDataUri(payload));
        }
        catch (FormatException)
        {
            return ProviderResult<ProviderBinaryResult>.Failed(Malformed("media.image_payload_invalid", "The provider returned invalid base64 image data.", providerId, model));
        }

        if (bytes.Length == 0)
        {
            return ProviderResult<ProviderBinaryResult>.Failed(Malformed("media.image_payload_invalid", "The provider returned zero-length image data.", providerId, model));
        }

        if (bytes.Length > maxAssetBytes)
        {
            return ProviderResult<ProviderBinaryResult>.Failed(new ProviderError(
                "media.provider_payload_too_large",
                ProviderErrorCategory.ResourceExhausted,
                "The generated media exceeds the configured limit.",
                false,
                providerId,
                model,
                details: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["byte_length"] = bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["max_asset_bytes"] = maxAssetBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
        }

        // 不采信服务端自称的 mime：以字节为准。
        var format = ProviderImageMedia.SniffFormat(bytes);
        return ProviderResult<ProviderBinaryResult>.Succeeded(new ProviderBinaryResult(bytes, ProviderImageMedia.MediaTypeFor(format), model));
    }

    private static ProviderError Malformed(string code, string message, string providerId, string model)
    {
        return new ProviderError(code, ProviderErrorCategory.MalformedResponse, message, false, providerId, model);
    }
}
