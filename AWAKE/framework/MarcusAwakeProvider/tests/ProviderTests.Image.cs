using MarcusAwakeProvider;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider.Tests;

/// <summary>
/// 生图（片 1）的离线判据。**全部走假 HTTP，不发真的网络请求。**
///
/// <para>两条判据是刻意做成「改坏就会红」的：
/// ① 剥前缀 —— 断言解出的字节数与真图**逐一相等**（多 15 字节就是没剥干净）；
/// ② 媒体类型 —— 断言按字节认，而不是采信服务端自称的 mimetype。</para>
/// </summary>
internal static partial class ProviderTests
{
    private static DateTimeOffset Deadline() => DateTimeOffset.UtcNow.AddMinutes(1);

    private static ProviderConnectionProfile Player2Profile(bool cloud)
    {
        return cloud
            ? new ProviderConnectionProfile("player2-cloud", ProviderKind.Player2, new Uri("https://api.player2.game/v1/"), "player2", "cred-player2", isCloud: true)
            : new ProviderConnectionProfile("player2-local", ProviderKind.Player2, new Uri("http://127.0.0.1:4315/v1/"), "player2", isCloud: false);
    }

    /// <summary>一个小而形状合法的 PNG：签名 + IHDR 里带真实宽高。只做鉴定用，不是能打开的图。</summary>
    private static byte[] PngBytes(int width, int height, int length = 40)
    {
        var bytes = new byte[Math.Max(24, length)];
        byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Array.Copy(signature, bytes, 8);
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';
        WriteBigEndian(bytes, 16, width);
        WriteBigEndian(bytes, 20, height);
        for (var i = 24; i < bytes.Length; i++) bytes[i] = (byte)(i & 0x7F);
        return bytes;
    }

    private static byte[] JpegBytes(int length = 32)
    {
        var bytes = new byte[length];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xE0;
        for (var i = 4; i < length; i++) bytes[i] = (byte)(i & 0x7F);
        return bytes;
    }

    private static void WriteBigEndian(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }

    private static FakeHttpMessageHandler ImageHandler(string json)
    {
        return new FakeHttpMessageHandler(_ => TestSupport.JsonResponse(json));
    }

    private static string Player2ImageJson(byte[] image)
    {
        return "{\"image\":\"" + Convert.ToBase64String(image) + "\"}";
    }

    // ---------- 形状与鉴权 ----------

    internal static async Task Player2ImageGenerateAndEditPathsAsync()
    {
        var handler = ImageHandler(Player2ImageJson(PngBytes(64, 64)));
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var adapter = new Player2Provider(Player2Profile(false), invoker);

        var plain = await adapter.GenerateImageAsync(new ProviderImageRequest("a knight at dusk", 512, 512), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertSuccess(plain);
        TestSupport.Assert(handler.Requests[0].Path == "/v1/image/generate", "无参考图必须走 /v1/image/generate，实到 " + handler.Requests[0].Path);
        TestSupport.Assert(handler.Requests[0].Body.IndexOf("\"prompt\":\"a knight at dusk\"", StringComparison.Ordinal) >= 0, "请求体没带 prompt");
        TestSupport.Assert(handler.Requests[0].Body.IndexOf("\"width\":512", StringComparison.Ordinal) >= 0, "请求体没带 width");
        TestSupport.Assert(handler.Requests[0].Body.IndexOf("\"image\"", StringComparison.Ordinal) < 0, "纯文生图不该带参考图字段");

        var reference = PngBytes(212, 360);
        var edited = await adapter.GenerateImageAsync(new ProviderImageRequest("same knight", 512, 512, referenceImage: reference), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertSuccess(edited);
        TestSupport.Assert(handler.Requests[1].Path == "/v1/image/edit", "带参考图必须走 /v1/image/edit，实到 " + handler.Requests[1].Path);
        TestSupport.Assert(
            handler.Requests[1].Body.IndexOf("\"image\":\"" + Convert.ToBase64String(reference) + "\"", StringComparison.Ordinal) >= 0,
            "参考图必须是裸 base64（Player2 这侧不吃 data URI 前缀）");
    }

    internal static async Task Player2ImageIdempotencyKeyIsSentWhenProvidedAsync()
    {
        var handler = ImageHandler(Player2ImageJson(PngBytes(16, 16)));
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var adapter = new Player2Provider(Player2Profile(false), invoker);

        await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256, idempotencyKey: "key-abc"), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.Assert(handler.Requests[0].IdempotencyKey == "key-abc", "提供了幂等键就必须上头，实到 " + (handler.Requests[0].IdempotencyKey ?? "null"));

        await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.Assert(handler.Requests[1].IdempotencyKey == null, "没提供幂等键就不该编一个出来");
    }

    internal static async Task Player2ImageAuthorizationFollowsCloudNotShapeAsync()
    {
        var localHandler = ImageHandler(Player2ImageJson(PngBytes(16, 16)));
        using (var invoker = new System.Net.Http.HttpMessageInvoker(localHandler))
        {
            var adapter = new Player2Provider(Player2Profile(false), invoker);
            await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
            TestSupport.Assert(localHandler.Requests[0].Authorization == null, "本机免认证服务不该收到 Authorization（带了反而可能被拒）");
        }

        var cloudHandler = ImageHandler(Player2ImageJson(PngBytes(16, 16)));
        using (var invoker = new System.Net.Http.HttpMessageInvoker(cloudHandler))
        {
            var adapter = new Player2Provider(Player2Profile(true), invoker);
            using var credential = new ApiKeyCredential("cred-player2", "secret-value");
            await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), credential, Deadline(), CancellationToken.None).ConfigureAwait(false);
            TestSupport.Assert(cloudHandler.Requests[0].Authorization == "Bearer secret-value", "云端必须带 Bearer，实到 " + (cloudHandler.Requests[0].Authorization ?? "null"));
        }
    }

    internal static async Task Player2ChatOperationsReportUnsupportedAsync()
    {
        var handler = ImageHandler("{}");
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var adapter = new Player2Provider(Player2Profile(false), invoker);

        var completion = await adapter.CompleteAsync(null!, null, Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertFailure(completion, ProviderErrorCategory.Unsupported);

        var events = await TestSupport.CollectAsync(adapter.StreamAsync(null!, null, Deadline(), CancellationToken.None)).ConfigureAwait(false);
        TestSupport.Assert(events.Count == 1 && events[0].Error != null, "流式这一侧必须回一条终止失败，而不是静默空流");
        TestSupport.Assert(events[0].Error!.Category == ProviderErrorCategory.Unsupported, "流式失败类别应为 Unsupported");

        TestSupport.Assert(handler.RequestCount == 0, "报「不支持」不该发任何请求出去");
        var models = TestSupport.AssertSuccess(await adapter.ListModelsAsync(null, Deadline(), CancellationToken.None).ConfigureAwait(false));
        TestSupport.Assert(models.Count == 1, "Player2 应当只有一条合成模型名");
        TestSupport.Assert(handler.RequestCount == 0, "合成模型名不该去猜一个 /models 端点");
    }

    /// <summary>
    /// 连接检查的能力表必须如实：Player2 不提供文字，就不能继承基类那句「文字能力未验证」。
    /// </summary>
    internal static async Task Player2ConnectionReportDoesNotClaimTextCapabilityAsync()
    {
        var profile = Player2Profile(false);
        var handler = ImageHandler("{}");
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var adapter = new Player2Provider(profile, invoker);

        var report = TestSupport.AssertSuccess(await adapter.TestConnectionAsync(null, Deadline(), CancellationToken.None).ConfigureAwait(false));
        TestSupport.Assert(report.ProviderId == profile.ProviderId, "能力表要报自己的 provider");
        TestSupport.Assert(
            report.Capabilities[ProviderCapabilityId.TextGeneration] == ProviderCapabilityState.Unsupported,
            "Player2 不提供文字 ⇒ 必须报 Unsupported 而不是基类那句 Unverified；实得 " + report.Capabilities[ProviderCapabilityId.TextGeneration]);
        TestSupport.Assert(
            report.Capabilities[ProviderCapabilityId.Streaming] == ProviderCapabilityState.Unsupported,
            "流式同理；实得 " + report.Capabilities[ProviderCapabilityId.Streaming]);
        TestSupport.Assert(
            report.Capabilities[ProviderCapabilityId.StructuredOutput] == ProviderCapabilityState.Unsupported,
            "结构化输出同理；实得 " + report.Capabilities[ProviderCapabilityId.StructuredOutput]);
        TestSupport.Assert(
            report.Capabilities[ProviderCapabilityId.ImageGeneration] == ProviderCapabilityState.Unverified,
            "生图能力存在，但这次连通性检查并没有真去出一张图 ⇒ 只能报 Unverified，不许宣称已验证；实得 " + report.Capabilities[ProviderCapabilityId.ImageGeneration]);
        TestSupport.Assert(
            report.Capabilities[ProviderCapabilityId.ModelDiscovery] == ProviderCapabilityState.Available,
            "合成模型名是实际给出的 ⇒ Available；实得 " + report.Capabilities[ProviderCapabilityId.ModelDiscovery]);
        TestSupport.Assert(handler.RequestCount == 0, "Player2 的连通性检查不该发请求");
    }

    // ---------- 剥前缀（判据 ①）----------

    internal static async Task ImagePayloadDataUriPrefixIsStrippedExactlyAsync()
    {
        var png = PngBytes(1024, 1024, 4096);
        var jpeg = JpegBytes(2048);

        var handler = new FakeHttpMessageHandler(request =>
        {
            var payload = request.RequestUri!.AbsolutePath.EndsWith("/image/edit", StringComparison.Ordinal)
                ? "data:image/jpeg;base64," + Convert.ToBase64String(jpeg)
                : "data:image/png;base64," + Convert.ToBase64String(png);
            return TestSupport.JsonResponse("{\"image\":\"" + payload + "\"}");
        });
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var adapter = new Player2Provider(Player2Profile(false), invoker);

        var pngResult = TestSupport.AssertSuccess(await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), null, Deadline(), CancellationToken.None).ConfigureAwait(false));
        TestSupport.Assert(pngResult.Content.Length == png.Length, "剥前缀后字节数必须与真图一致；多出来的字节数 = " + (pngResult.Content.Length - png.Length));
        TestSupport.Assert(Convert.ToBase64String(pngResult.Content) == Convert.ToBase64String(png), "剥前缀不能动到真数据");
        TestSupport.Assert(pngResult.MediaType == "image/png", "实到 " + pngResult.MediaType);

        var jpegResult = TestSupport.AssertSuccess(await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256, referenceImage: PngBytes(8, 8)), null, Deadline(), CancellationToken.None).ConfigureAwait(false));
        TestSupport.Assert(jpegResult.Content.Length == jpeg.Length, "jpeg 那侧同样必须逐字节相等；多出来的字节数 = " + (jpegResult.Content.Length - jpeg.Length));
        TestSupport.Assert(jpegResult.MediaType == "image/jpeg", "实到 " + jpegResult.MediaType);
    }

    // ---------- 按字节认类型（判据 ②）----------

    internal static async Task ImageMediaTypeFollowsBytesNotDeclaredMimeAsync()
    {
        // 服务端自称 png，实际字节是 jpeg ⇒ 必须按 jpeg 记。
        var jpeg = JpegBytes(64);
        var handler = ImageHandler("{\"mimetype\":\"image/png\",\"mime_type\":\"image/png\",\"image\":\"" + Convert.ToBase64String(jpeg) + "\"}");
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var adapter = new Player2Provider(Player2Profile(false), invoker);

        var result = TestSupport.AssertSuccess(await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), null, Deadline(), CancellationToken.None).ConfigureAwait(false));
        TestSupport.Assert(result.MediaType == "image/jpeg", "媒体类型必须来自字节，不能采信服务端自称的 mimetype；实到 " + result.MediaType);
    }

    internal static Task ImageMediaSniffingAndDimensionsAsync()
    {
        var png = PngBytes(1024, 1536);
        TestSupport.Assert(ProviderImageMedia.SniffFormat(png) == "png", "PNG 签名没认出来");
        TestSupport.Assert(ProviderImageMedia.TryReadDimensions(png, "png", out var width, out var height), "PNG 宽高没读出来");
        TestSupport.Assert(width == 1024 && height == 1536, "PNG 宽高读错：" + width + "x" + height);

        TestSupport.Assert(ProviderImageMedia.SniffFormat(JpegBytes()) == "jpg", "JPEG 签名没认出来");
        TestSupport.Assert(ProviderImageMedia.MediaTypeFor("jpg") == "image/jpeg", "jpg 必须映到 image/jpeg，不是 image/jpg");
        TestSupport.Assert(ProviderImageMedia.SniffFormat(new byte[] { 0x01, 0x02, 0x03, 0x04 }) == "unknown", "认不出来就该是 unknown，不能假定");
        TestSupport.Assert(ProviderImageMedia.MediaTypeFor("unknown") == ProviderImageMedia.UnknownMediaType, "unknown 应落到通用二进制类型");

        TestSupport.Assert(ProviderImageMedia.StripDataUri("data:image/png;base64,AAAA") == "AAAA", "剥前缀结果不对");
        TestSupport.Assert(ProviderImageMedia.StripDataUri("AAAA") == "AAAA", "没有前缀时不该动内容");
        return Task.CompletedTask;
    }

    // ---------- 限额与坏载荷 ----------

    internal static async Task ImageAssetLimitIsEnforcedAsync()
    {
        var handler = ImageHandler(Player2ImageJson(PngBytes(16, 16, 40)));
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var adapter = new Player2Provider(Player2Profile(false), invoker, null, null, null, maxImageAssetBytes: 32);

        var result = await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertFailure(result, ProviderErrorCategory.ResourceExhausted);
        TestSupport.Assert(result.Error!.Code == "media.provider_payload_too_large", "实到 " + result.Error.Code);
    }

    internal static async Task ImageRequestLimitIsEnforcedBeforeHttpAsync()
    {
        var handler = ImageHandler(Player2ImageJson(PngBytes(16, 16)));
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var adapter = new Player2Provider(Player2Profile(false), invoker, null, null, null, maxImageAssetBytes: 32);

        var result = await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256, referenceImage: PngBytes(64, 64)), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertFailure(result, ProviderErrorCategory.ResourceExhausted);
        TestSupport.Assert(result.Error!.Code == "media.image_request_too_large", "实到 " + result.Error.Code);
        TestSupport.Assert(handler.RequestCount == 0, "超限的请求绝不该发出去");
    }

    internal static async Task ImageBadPayloadsAreTypedFailuresAsync()
    {
        using (var invoker = new System.Net.Http.HttpMessageInvoker(ImageHandler("{\"note\":\"no image here\"}")))
        {
            var adapter = new Player2Provider(Player2Profile(false), invoker);
            var result = await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
            TestSupport.AssertFailure(result, ProviderErrorCategory.MalformedResponse);
        }

        using (var invoker = new System.Net.Http.HttpMessageInvoker(ImageHandler("{\"image\":\"!!!! not base64 !!!!\"}")))
        {
            var adapter = new Player2Provider(Player2Profile(false), invoker);
            var result = await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
            TestSupport.AssertFailure(result, ProviderErrorCategory.MalformedResponse);
            TestSupport.Assert(result.Error!.Code == "media.image_payload_invalid", "实到 " + result.Error.Code);
        }

        using (var invoker = new System.Net.Http.HttpMessageInvoker(new FakeHttpMessageHandler(_ => TestSupport.JsonResponse("not json at all"))))
        {
            var adapter = new Player2Provider(Player2Profile(false), invoker);
            var result = await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
            TestSupport.AssertFailure(result, ProviderErrorCategory.MalformedResponse);
        }
    }

    internal static async Task ImageHttpFailureIsMappedNotSwallowedAsync()
    {
        using var invoker = new System.Net.Http.HttpMessageInvoker(new FakeHttpMessageHandler(_ => new System.Net.Http.HttpResponseMessage(HttpStatusCode.NotFound)));
        var adapter = new Player2Provider(Player2Profile(false), invoker);

        var result = await adapter.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), null, Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertFailure(result, ProviderErrorCategory.NotFound);
        TestSupport.Assert(result.Error!.StatusCode == 404, "状态码该被带出来");
    }

    // ---------- OpenAI 兼容那条路 ----------

    internal static async Task OpenAiImageUsesGenerationsPathAndB64Async()
    {
        var png = PngBytes(512, 768);
        var handler = ImageHandler("{\"data\":[{\"b64_json\":\"" + Convert.ToBase64String(png) + "\"}]}");
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var profile = new ProviderConnectionProfile("openai-img", ProviderKind.OpenAiCompatible, new Uri("https://example.test/v1/"), "gpt-image-1", "cred-openai", isCloud: true);
        var adapter = ProviderAdapterFactory.CreateImage(profile, invoker);
        TestSupport.Assert(adapter != null, "OpenAI 兼容应当支持生图");

        using var credential = new ApiKeyCredential("cred-openai", "sk-test");
        var result = TestSupport.AssertSuccess(await adapter!.GenerateImageAsync(new ProviderImageRequest("a knight", 512, 768), credential, Deadline(), CancellationToken.None).ConfigureAwait(false));
        TestSupport.Assert(handler.Requests[0].Path == "/v1/images/generations", "实到 " + handler.Requests[0].Path);
        TestSupport.Assert(handler.Requests[0].Body.IndexOf("\"size\":\"512x768\"", StringComparison.Ordinal) >= 0, "size 没写对");
        TestSupport.Assert(handler.Requests[0].Body.IndexOf("\"response_format\":\"b64_json\"", StringComparison.Ordinal) >= 0, "必须显式要 b64_json");
        TestSupport.Assert(handler.Requests[0].Body.IndexOf("\"model\":\"gpt-image-1\"", StringComparison.Ordinal) >= 0, "model 没写对");
        TestSupport.Assert(handler.Requests[0].Authorization == "Bearer sk-test", "云端必须带 Bearer");
        TestSupport.Assert(result.MediaType == "image/png", "实到 " + result.MediaType);
        TestSupport.Assert(result.Content.Length == png.Length, "字节数应当与真图一致");
    }

    internal static async Task OpenAiImageUrlOnlyIsRejectedAndNeverFollowedAsync()
    {
        var handler = ImageHandler("{\"data\":[{\"url\":\"https://cdn.example.test/a.png\"}]}");
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var profile = new ProviderConnectionProfile("openai-img", ProviderKind.OpenAiCompatible, new Uri("https://example.test/v1/"), "gpt-image-1", "cred-openai", isCloud: true);
        var adapter = ProviderAdapterFactory.CreateImage(profile, invoker)!;

        using var credential = new ApiKeyCredential("cred-openai", "sk-test");
        var result = await adapter.GenerateImageAsync(new ProviderImageRequest("a knight", 512, 512), credential, Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertFailure(result, ProviderErrorCategory.Unsupported);
        TestSupport.Assert(result.Error!.Code == "media.image_url_unsupported", "实到 " + result.Error.Code);
        TestSupport.Assert(handler.RequestCount == 1, "绝不该跟着响应里的 url 再去取一次；实到请求数 " + handler.RequestCount);
    }

    // ---------- 路由 ----------

    internal static async Task RouterSkipsNonImageAdaptersAsync()
    {
        var handler = ImageHandler(Player2ImageJson(PngBytes(32, 32)));
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var textOnly = new ProviderConnectionProfile("text-only", ProviderKind.Anthropic, new Uri("http://127.0.0.1:4316/v1/"), "claude", isCloud: false);
        var router = new ProviderRouter(new List<ProviderRouteCandidate>
        {
            new ProviderRouteCandidate(ProviderAdapterFactory.Create(textOnly, invoker)),
            new ProviderRouteCandidate(ProviderAdapterFactory.Create(Player2Profile(false), invoker))
        });

        TestSupport.Assert(router.CanGenerateImages, "路由里明明有一条能生图的候选");
        var result = TestSupport.AssertSuccess(await router.GenerateImageAsync(new ProviderImageRequest("a knight", 256, 256), Deadline(), CancellationToken.None).ConfigureAwait(false));
        TestSupport.Assert(handler.RequestCount == 1, "只有能生图的那条候选该被请求；实到 " + handler.RequestCount);
        TestSupport.Assert(handler.Requests[0].Path == "/v1/image/generate", "实到 " + handler.Requests[0].Path);
        TestSupport.Assert(result.MediaType == "image/png", "实到 " + result.MediaType);
    }

    internal static async Task RouterReportsUnsupportedWhenNothingCanGenerateAsync()
    {
        var handler = ImageHandler("{}");
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var textOnly = new ProviderConnectionProfile("text-only", ProviderKind.Ollama, new Uri("http://127.0.0.1:11434/"), "llama", isCloud: false);
        var router = new ProviderRouter(new List<ProviderRouteCandidate>
        {
            new ProviderRouteCandidate(ProviderAdapterFactory.Create(textOnly, invoker))
        });

        TestSupport.Assert(!router.CanGenerateImages, "纯文字候选不该被当成能生图");
        var result = await router.GenerateImageAsync(new ProviderImageRequest("x", 256, 256), Deadline(), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertFailure(result, ProviderErrorCategory.Unsupported);
        TestSupport.Assert(result.Error!.Code == "media.image_adapter_unsupported", "实到 " + result.Error.Code);
        TestSupport.Assert(handler.RequestCount == 0, "没有能生图的候选就不该有请求出去");
    }

    internal static Task Player2KindCodecAndFactoryAsync()
    {
        TestSupport.Assert(ProviderKindCodec.TryParseWireName("player2", out var parsed) && parsed == ProviderKind.Player2, "player2 的线名没解析出来");
        TestSupport.Assert(ProviderKindCodec.ToWireName(ProviderKind.Player2) == "player2", "player2 的线名没序列化出来");

        var handler = ImageHandler("{}");
        using var invoker = new System.Net.Http.HttpMessageInvoker(handler);
        var profile = Player2Profile(false);
        TestSupport.Assert(ProviderAdapterFactory.Create(profile, invoker) is Player2Provider, "工厂没造出 Player2Provider");
        TestSupport.Assert(ProviderAdapterFactory.CreateImage(profile, invoker) is Player2Provider, "生图工厂没造出 Player2Provider");

        var textOnly = new ProviderConnectionProfile("text-only", ProviderKind.Anthropic, new Uri("http://127.0.0.1:4317/v1/"), "claude", isCloud: false);
        TestSupport.Assert(ProviderAdapterFactory.CreateImage(textOnly, invoker) == null, "Anthropic 不该被当成能生图");

        TestSupport.AssertThrows<ArgumentException>(() => new Player2Provider(
            new ProviderConnectionProfile("wrong-kind", ProviderKind.Ollama, new Uri("http://127.0.0.1:4318/"), "llama", isCloud: false),
            invoker));
        return Task.CompletedTask;
    }
}
