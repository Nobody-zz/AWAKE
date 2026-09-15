// AWAKE 出图适配层 · 离线验证台
//
// 目的只有一个：把「地址/形状 → 请求 → 响应 → 真图字节」这条链在**不进游戏、不花钱、不要 Key**
// 的前提下证死或证伪。
//
// 为什么能这么测：本工程把 AWAKE/src/**/*.cs 整个编进自己的程序集（见 csproj 注释），
// 所以 internal 的形状适配器就近可见，测的就是仓库里那几行，没有替身、没有平行实现。
//
// 判据必须来自**独立生产者**：图片样本由 Pillow 产出并把真尺寸写进常量（SampleImages.cs），
// 另有仓库内自带的 212x360 立绘探测图做交叉核对。C# 侧读出来的尺寸要跟它们对上，
// 而不是自己跟自己对。
//
// 输出契约与 WorldbookRuntimeProductionSmoke 一致：PASS / FAIL <name> :: <reason> / RESULT，失败退 1。
//
// 怎么跑（不需要游戏、不需要 Key、不花钱）：
//   dotnet build AWAKE/tools/image-shape-harness/ImageShapeHarness.csproj -c Release
//   AWAKE/tools/image-shape-harness/artifacts/bin/Release/Awake.ImageShapeHarness.exe
//
// 判据本身也要能红：每次改这块代码后，随手做一次变异检验（把 StripDataUri 去掉、或让 SniffFormat
// 认不出时返回 "png"），确认验台会 FAIL 而不是照样 PASS。恒绿的判据等于没测。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Awake.ImageShapeHarness;

internal static class Program
{
    private static readonly object LogGate = new object();
    private static readonly List<string> CapturedLogs = new List<string>();
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        // 关掉落文件，只留内存捕获：避免在模块目录里留下 Logs/。
        // Recorder 在 Enabled=false 时依然会被调用（见 AwakeLog.Write）。
        AwakeLog.Enabled = false;
        AwakeLog.Recorder = line =>
        {
            lock (LogGate) CapturedLogs.Add(line ?? string.Empty);
        };

        try
        {
            Run("shape-endpoint-paths", TestShapeEndpointPaths);
            Run("base-url-normalization", TestBaseUrlNormalization);
            Run("config-endpoint-resolution", TestConfigEndpointResolution);
            Run("request-body-per-shape", TestRequestBodyPerShape);
            Run("data-uri-stripping", TestDataUriStripping);
            Run("reply-parsing-player2", TestReplyParsingPlayer2);
            Run("reply-parsing-openai", TestReplyParsingOpenAi);
            Run("reply-parsing-rejects-bad-payload", TestReplyParsingRejectsBadPayload);
            Run("format-and-dimension-sniffing", TestFormatAndDimensionSniffing);
            Run("declared-mime-is-not-trusted", TestDeclaredMimeIsNotTrusted);
            Run("live-generate-no-auth-player2", TestLiveGenerateNoAuthPlayer2);
            Run("live-generate-with-auth-openai", TestLiveGenerateWithAuthOpenAi);
            Run("live-generate-with-auth-player2", TestLiveGenerateWithAuthPlayer2);
            Run("live-generate-no-auth-openai", TestLiveGenerateNoAuthOpenAi);
            Run("live-http-failure-mapping", TestLiveHttpFailureMapping);
            Run("live-unreachable-service", TestLiveUnreachableService);
            Run("cancelled-request-reports-timeout", TestCancelledRequestReportsTimeout);
        }
        finally
        {
            AwakeLog.Recorder = null;
            AwakeLog.Enabled = true;
        }

        Console.WriteLine("RESULT passed=" + _passed + " failed=" + _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine("FAIL " + name + " :: " + Flatten(ex));
        }
    }

    // ── 判据 ────────────────────────────────────────────────────────────────

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal(string expected, string actual, string what)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException(what + "：期望 “" + expected + "”，实际 “" + (actual ?? "<null>") + "”");
    }

    private static void EqualInt(int expected, int actual, string what)
    {
        if (expected != actual)
            throw new InvalidOperationException(what + "：期望 " + expected + "，实际 " + actual);
    }

    private static void Contains(string haystack, string needle, string what)
    {
        if (haystack == null || haystack.IndexOf(needle, StringComparison.Ordinal) < 0)
            throw new InvalidOperationException(what + "：找不到 “" + needle + "”，实际 “" + (haystack ?? "<null>") + "”");
    }

    private static void BytesEqual(byte[] expected, byte[] actual, string what)
    {
        if (actual == null) throw new InvalidOperationException(what + "：实际为 null");
        if (expected.Length != actual.Length)
            throw new InvalidOperationException(what + "：长度 期望 " + expected.Length + "，实际 " + actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
                throw new InvalidOperationException(what + "：第 " + i + " 字节 期望 0x"
                    + expected[i].ToString("X2", CultureInfo.InvariantCulture) + "，实际 0x"
                    + actual[i].ToString("X2", CultureInfo.InvariantCulture));
        }
    }

    private static string Flatten(Exception exception)
    {
        var parts = new List<string>();
        for (Exception current = exception; current != null; current = current.InnerException)
            parts.Add(current.GetType().Name + ":" + current.Message);
        return string.Join(" -> ", parts);
    }

    private static string Leading(string value)
    {
        string text = (value ?? "<null>").Replace('\r', '·').Replace('\n', '·');
        return text.Length <= 32 ? text : text.Substring(0, 32) + "…";
    }

    // ── 1. 路径与形状 ───────────────────────────────────────────────────────

    private static void TestShapeEndpointPaths()
    {
        Equal("/image/generate", AwakeImageShapeAdapter.EndpointPath(AwakeImageShape.Player2, false), "Player2 无参考图");
        Equal("/image/edit", AwakeImageShapeAdapter.EndpointPath(AwakeImageShape.Player2, true), "Player2 有参考图");
        Equal("/images/generations", AwakeImageShapeAdapter.EndpointPath(AwakeImageShape.OpenAiCompatible, false), "OpenAI 无参考图");
        Equal("/images/generations", AwakeImageShapeAdapter.EndpointPath(AwakeImageShape.OpenAiCompatible, true), "OpenAI 有参考图");

        // 只有云端才带 Authorization —— 本机免认证服务带了反而可能被拒。
        Check(!AwakeImageShapeAdapter.RequiresAuthorization(AwakeImageShape.Player2, false), "Player2 本机不得带 Authorization");
        Check(AwakeImageShapeAdapter.RequiresAuthorization(AwakeImageShape.Player2, true), "Player2 云端要带 Authorization");
        Check(!AwakeImageShapeAdapter.RequiresAuthorization(AwakeImageShape.OpenAiCompatible, false), "OpenAI 本机不得带 Authorization");
        Check(AwakeImageShapeAdapter.RequiresAuthorization(AwakeImageShape.OpenAiCompatible, true), "OpenAI 云端要带 Authorization");

        Equal("Player2", AwakeImageEndpointResolver.ShapeLabel(AwakeImageShape.Player2), "形状标签");
        Equal("OpenAI 兼容", AwakeImageEndpointResolver.ShapeLabel(AwakeImageShape.OpenAiCompatible), "形状标签");
        Equal("player2", AwakeImageEndpointResolver.ShapeId(AwakeImageShape.Player2), "形状 id");
        Equal("openai_compatible", AwakeImageEndpointResolver.ShapeId(AwakeImageShape.OpenAiCompatible), "形状 id");
    }

    // ── 2. 地址归一化 ───────────────────────────────────────────────────────

    private static void TestBaseUrlNormalization()
    {
        // 玩家真会粘的几种：API root / 带版本 root / 完整接口地址。
        NormalizesTo("http://127.0.0.1:4315/v1", "http://127.0.0.1:4315/v1", "API root 原样通过");
        NormalizesTo("http://127.0.0.1:4315/v1/", "http://127.0.0.1:4315/v1", "尾部斜杠");
        NormalizesTo("http://127.0.0.1:4315/v1/image/generate", "http://127.0.0.1:4315/v1", "粘完整 Player2 文生图地址");
        NormalizesTo("http://127.0.0.1:4315/v1/image/edit", "http://127.0.0.1:4315/v1", "粘完整 Player2 图生图地址");
        NormalizesTo("https://api.example.com/v1/images/generations", "https://api.example.com/v1", "粘完整 OpenAI 出图地址");
        NormalizesTo("https://api.example.com/v1/images/edits", "https://api.example.com/v1", "粘完整 OpenAI 改图地址");
        NormalizesTo("https://api.example.com/chat/completions", "https://api.example.com", "粘 AI 链路对话地址（与生图共用同一张后缀表）");
        NormalizesTo("https://api.example.com/v1/models", "https://api.example.com/v1", "粘模型列表地址");
        NormalizesTo("https://ark.cn-beijing.volces.com/api/v3", "https://ark.cn-beijing.volces.com/api/v3", "火山方舟 root 原样通过");

        Rejects("", "空地址");
        Rejects("   ", "全空白");
        Rejects("not-a-url", "不是绝对地址");
        Rejects("127.0.0.1:4315/v1", "缺 scheme");
        Rejects("ftp://api.example.com/v1", "非 http(s)");
        Rejects("https://user:secret@api.example.com/v1", "地址里带账号");
        Rejects("https://api.example.com/v1?key=abc", "地址里带查询参数");
        Rejects("https://api.example.com/v1#frag", "地址里带片段");

        // 归一化真的发生了 —— 不是"看着对"，是确实走了那条分支。
        lock (LogGate)
        {
            Check(CapturedLogs.Exists(line => line != null && line.Contains("provider_base_url_normalized")),
                "归一化应当留一行 provider_base_url_normalized 日志");
        }
    }

    private static void NormalizesTo(string raw, string expectedRoot, string what)
    {
        string normalized;
        string error;
        Check(AwakeProviderConfiguration.TryNormalizeProviderBaseUrl(raw, out normalized, out error),
            what + "：应当归一化成功，实际失败：" + error);
        Check(Uri.IsWellFormedUriString(normalized, UriKind.Absolute), what + "：结果必须是合法绝对地址，实际 " + normalized);
        Equal(expectedRoot.TrimEnd('/'), normalized.TrimEnd('/'), what);
    }

    private static void Rejects(string raw, string what)
    {
        string normalized;
        string error;
        Check(!AwakeProviderConfiguration.TryNormalizeProviderBaseUrl(raw, out normalized, out error),
            what + "：应当被拒（“" + raw + "”）");
        Check(!string.IsNullOrWhiteSpace(error), what + "：被拒时必须给人话原因");
    }

    // ── 3. 从 MCM 两栏解出可用端点 ──────────────────────────────────────────

    private static void TestConfigEndpointResolution()
    {
        AwakeConfig config = new AwakeConfig();
        config.PortraitImageBaseUrl = "http://127.0.0.1:4315/v1/image/edit";
        config.PortraitImageIsCloud = false;

        AwakeImageEndpoint endpoint;
        string error;
        Check(AwakeImageEndpointResolver.TryResolve(config, out endpoint, out error), "应当解析成功，实际：" + error);
        Equal("http://127.0.0.1:4315/v1", endpoint.BaseUrl, "粘完整地址应被归一化成 root");
        Equal("awake.image.default", endpoint.CredentialReference, "生图凭据槽位名");
        Check(!endpoint.IsCloud, "本机应当不是云端");
        Equal("player2", AwakeImageEndpointResolver.ShapeId(endpoint.Shape), "默认形状应为 Player2");

        string described = endpoint.Describe();
        Contains(described, "credential=none", "非云端时日志不得声称有凭据");
        Check(described.IndexOf("secret", StringComparison.OrdinalIgnoreCase) < 0, "Describe 绝不能吐出疑似密钥");

        config.PortraitImageIsCloud = true;
        Check(AwakeImageEndpointResolver.TryResolve(config, out endpoint, out error), "云端也应解析成功：" + error);
        Contains(endpoint.Describe(), "credential=awake.image.default", "云端应写明凭据槽位");

        // 形状下拉第二项 → OpenAI 兼容
        AwakeConfig openAi = new AwakeConfig();
        openAi.PortraitImageBaseUrl = "https://api.example.com/v1";
        openAi.PortraitImageShape.SelectedIndex = 1;
        Equal("openai_compatible",
            AwakeImageEndpointResolver.ShapeId(AwakeImageEndpointResolver.ResolveShape(openAi)),
            "下拉第二项应解析成 OpenAI 兼容");

        // 地址为空
        AwakeConfig blank = new AwakeConfig();
        Check(!AwakeImageEndpointResolver.TryResolve(blank, out endpoint, out error), "空地址必须解析失败");
        Check(endpoint == null, "失败时不得给出端点");
        Contains(error, "出图服务地址为空", "空地址要给人话");

        // 配置还没加载
        Check(!AwakeImageEndpointResolver.TryResolve(null, out endpoint, out error), "null 配置必须失败");
        Contains(error, "尚未加载", "null 配置要给人话");
    }

    // ── 4. 请求体：两个形状方向相反 ─────────────────────────────────────────

    private static void TestRequestBodyPerShape()
    {
        byte[] reference = Convert.FromBase64String(AwakePortraitProbeFixture.PngBase64);
        AwakeImageRequest withReference = new AwakeImageRequest("一个骑士的半身像", reference, 320, 192);
        AwakeImageRequest plain = new AwakeImageRequest("一个骑士的半身像", null, 320, 192);

        Check(withReference.HasReference, "有参考图时 HasReference 应为真");
        Check(!plain.HasReference, "无参考图时 HasReference 应为假");

        // --- Player2 ---
        JObject player2 = JObject.Parse(AwakeImageShapeAdapter.BuildRequestBody(AwakeImageShape.Player2, withReference));
        Equal("一个骑士的半身像", player2["prompt"].ToString(), "Player2 prompt");
        EqualInt(320, player2["width"].Value<int>(), "Player2 width");
        EqualInt(192, player2["height"].Value<int>(), "Player2 height");
        Check(player2["image"] != null, "Player2 有参考图时必须带 image 字段");
        string player2Image = player2["image"].ToString();
        Check(!player2Image.StartsWith("data:", StringComparison.OrdinalIgnoreCase),
            "Player2 的 image 必须是裸 base64，不能带 data: 前缀，实际 " + Leading(player2Image));
        Check(!player2.ContainsKey("size"), "Player2 不应带 OpenAI 的 size 字段");
        BytesEqual(reference, Convert.FromBase64String(player2Image), "Player2 的 image 必须是参考图的裸 base64");

        JObject player2Plain = JObject.Parse(AwakeImageShapeAdapter.BuildRequestBody(AwakeImageShape.Player2, plain));
        Check(player2Plain["image"] == null, "Player2 无参考图时不得带 image 字段");

        // --- OpenAI 兼容 ---
        JObject openAi = JObject.Parse(AwakeImageShapeAdapter.BuildRequestBody(AwakeImageShape.OpenAiCompatible, withReference));
        Equal("一个骑士的半身像", openAi["prompt"].ToString(), "OpenAI prompt");
        Equal("320x192", openAi["size"].ToString(), "OpenAI size");
        EqualInt(1, openAi["n"].Value<int>(), "OpenAI n");
        Equal("b64_json", openAi["response_format"].ToString(), "OpenAI response_format");
        Check(openAi["image"] != null, "OpenAI 有参考图时必须带 image 字段");
        string openAiImage = openAi["image"].ToString();
        Check(openAiImage.StartsWith("data:image/png;base64,", StringComparison.Ordinal),
            "OpenAI 的 image 必须带 data URI 前缀，实际 " + Leading(openAiImage));
        Check(!openAi.ContainsKey("width") && !openAi.ContainsKey("height"), "OpenAI 形状不应带 width/height 数字字段");
        BytesEqual(reference, Convert.FromBase64String(AwakeImageShapeAdapter.StripDataUri(openAiImage)),
            "OpenAI 的 image 剥掉前缀后必须是参考图本体");

        // 两个方向相反 ⇒ 同一份参考图在两形状下必须长得不一样
        Check(!string.Equals(player2Image, openAiImage, StringComparison.Ordinal),
            "两种形状的 image 字段形态必须不同（一裸 base64、一带 data URI 前缀）");

        JObject openAiPlain = JObject.Parse(AwakeImageShapeAdapter.BuildRequestBody(AwakeImageShape.OpenAiCompatible, plain));
        Check(openAiPlain["image"] == null, "OpenAI 无参考图时不得带 image 字段");
    }

    // ── 5. data URI 剥离（反方向也走同一条路） ──────────────────────────────

    private static void TestDataUriStripping()
    {
        Equal("AAAA", AwakeImageShapeAdapter.StripDataUri("data:image/png;base64,AAAA"), "带前缀");
        Equal("AAAA", AwakeImageShapeAdapter.StripDataUri("AAAA"), "裸 base64 原样通过");
        Equal("AAAA", AwakeImageShapeAdapter.StripDataUri("data:image/jpeg;base64,AAAA"), "jpeg 前缀");
        Equal("AAAA", AwakeImageShapeAdapter.StripDataUri("DATA:IMAGE/PNG;BASE64,AAAA"), "前缀大小写不敏感");
        Equal("AAAA", AwakeImageShapeAdapter.StripDataUri("  data:image/png;base64,\r\nAAAA  "), "前缀 + 换行 + 两侧空白");
        Equal("AAAA", AwakeImageShapeAdapter.StripDataUri("data:image/png;base64, AAAA "), "逗号后有空格");
        Equal("data:image/png;base64", AwakeImageShapeAdapter.StripDataUri("data:image/png;base64"), "没有逗号时原样返回（已文档化的行为）");
        Equal("", AwakeImageShapeAdapter.StripDataUri(""), "空串");
        Equal("", AwakeImageShapeAdapter.StripDataUri(null), "null");

        byte[] three = new byte[] { 0x01, 0x02, 0x03 };
        Equal("data:image/png;base64," + Convert.ToBase64String(three), AwakeImageShapeAdapter.ToDataUri("png", three), "ToDataUri png");
        Equal("data:image/jpeg;base64," + Convert.ToBase64String(three), AwakeImageShapeAdapter.ToDataUri("jpg", three), "ToDataUri jpg 必须用 image/jpeg");
        Equal("", AwakeImageShapeAdapter.ToDataUri("png", null), "空字节 → 空串");
        Equal("", AwakeImageShapeAdapter.ToDataUri("png", new byte[0]), "零长 → 空串");
    }

    // ── 6. 响应解析（Player2） ──────────────────────────────────────────────

    private static void TestReplyParsingPlayer2()
    {
        byte[] png = SampleImages.Png();
        string pngB64 = Convert.ToBase64String(png);

        Parses(AwakeImageShape.Player2, "{\"image\":\"" + pngB64 + "\"}",
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "裸 base64");

        // 这一条是整块代码存在的理由：Player2 文档写"不带前缀"，实测带。
        string prefixed = "{\"image\":\"data:image/png;base64," + pngB64 + "\"}";
        AwakeImageReply reply = Parses(AwakeImageShape.Player2, prefixed,
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "带 data URI 前缀（文档说没有、实测有）");
        BytesEqual(png, reply.Bytes, "剥前缀后字节必须与样本逐字节相同（多 15 字节垃圾就会在这里露馅）");

        Parses(AwakeImageShape.Player2, "{\"image\":\"data:image/png;base64,\\r\\n" + pngB64 + "\"}",
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "前缀 + 夹换行");
        Parses(AwakeImageShape.Player2, "{\"images\":[\"" + pngB64 + "\"]}",
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "images 数组");
        Parses(AwakeImageShape.Player2, "{\"image_base64\":\"" + pngB64 + "\"}",
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "image_base64 字段");
        Parses(AwakeImageShape.Player2, "{\"mimetype\":\"image/png\",\"image\":\"" + pngB64 + "\"}",
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "带 mimetype");
        Parses(AwakeImageShape.Player2, "{\"image\":\"" + Convert.ToBase64String(SampleImages.Gif()) + "\"}",
            "gif", SampleImages.GifWidth, SampleImages.GifHeight, "gif 真尺寸");
        Parses(AwakeImageShape.Player2, "{\"image\":\"" + AwakePortraitProbeFixture.PngBase64 + "\"}",
            "png", AwakePortraitProbeFixture.Width, AwakePortraitProbeFixture.Height, "仓库自带立绘探测图（独立生产者交叉核对）");
    }

    // ── 7. 响应解析（OpenAI 兼容） ──────────────────────────────────────────

    private static void TestReplyParsingOpenAi()
    {
        byte[] png = SampleImages.Png();
        string pngB64 = Convert.ToBase64String(png);

        Parses(AwakeImageShape.OpenAiCompatible, "{\"data\":[{\"b64_json\":\"" + pngB64 + "\"}]}",
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "标准 b64_json");
        Parses(AwakeImageShape.OpenAiCompatible, "{\"data\":[{\"b64_json\":\"data:image/jpeg;base64,"
            + Convert.ToBase64String(SampleImages.Jpeg()) + "\"}]}",
            "jpg", SampleImages.JpegWidth, SampleImages.JpegHeight, "b64_json 里也带前缀");
        Parses(AwakeImageShape.OpenAiCompatible, "{\"data\":[{\"b64\":\"" + pngB64 + "\"}]}",
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "b64 别名");
        Parses(AwakeImageShape.OpenAiCompatible, "{\"data\":[{\"url\":\"data:image/png;base64," + pngB64 + "\"}]}",
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "url 字段（同样是 data URI，同样要剥）");
        Parses(AwakeImageShape.OpenAiCompatible, "{\"image\":\"" + pngB64 + "\"}",
            "png", SampleImages.PngWidth, SampleImages.PngHeight, "退化成 image 字段");
        Parses(AwakeImageShape.OpenAiCompatible, "{\"data\":[{\"b64_json\":\""
            + Convert.ToBase64String(SampleImages.Bmp()) + "\"}]}",
            "bmp", 0, 0, "bmp 只认格式（尺寸表里没有它，如实报 0）");
        Parses(AwakeImageShape.OpenAiCompatible, "{\"data\":[{\"b64_json\":\""
            + Convert.ToBase64String(SampleImages.Webp()) + "\"}]}",
            "webp", 0, 0, "webp 只认格式（同上）");
    }

    // ── 8. 响应解析：坏输入不许静默成功 ─────────────────────────────────────

    private static void TestReplyParsingRejectsBadPayload()
    {
        AwakeImageShape player2 = AwakeImageShape.Player2;
        AwakeImageShape openAi = AwakeImageShape.OpenAiCompatible;

        RejectsPayload(player2, null, "JSON", "null 响应体");
        RejectsPayload(player2, "", "JSON", "空响应体");
        RejectsPayload(player2, "<html><body>502 Bad Gateway</body></html>", "JSON", "HTML 错误页（网关回的）");
        RejectsPayload(player2, "{}", "没有图片字段", "空对象");
        RejectsPayload(player2, "{\"status\":\"ok\"}", "没有图片字段", "形似成功但没有图");
        RejectsPayload(player2, "{\"image\":\"\"}", "没有图片字段", "空图片字段");
        RejectsPayload(player2, "{\"image\":null}", "没有图片字段", "null 图片字段");
        RejectsPayload(player2, "{\"image\":\"not*valid*base64!\"}", "base64", "非法 base64");
        RejectsPayload(player2, "{\"image\":\"data:image/png;base64,\"}", "0 字节", "前缀剥完是 0 字节");

        // 形状选错的症状：Player2 形状不读 data[]，于是报"没有图片字段"而不是给出假图。
        // 这正是 MCM 提示里写的那句话，这里把它钉成判据。
        RejectsPayload(player2, "{\"data\":[{\"b64_json\":\"###\"}]}", "没有图片字段",
            "形状选错：Player2 形状收到 OpenAI 体 ⇒ 报没有图片字段");

        RejectsPayload(openAi, "{\"data\":[{\"b64_json\":\"###\"}]}", "base64", "OpenAI 侧非法 base64");
        RejectsPayload(openAi, "{\"data\":[{\"b64_json\":\"\"}]}", "没有图片字段", "OpenAI 空图片字段");
        RejectsPayload(openAi, "{\"data\":[]}", "没有图片字段", "OpenAI data 空数组");
        RejectsPayload(openAi, "{\"data\":\"not-an-array\"}", "没有图片字段", "OpenAI data 不是数组");
    }

    // ── 9. 认格式与读尺寸 ───────────────────────────────────────────────────

    private static void TestFormatAndDimensionSniffing()
    {
        // 格式：按文件头认，五个形状都要认得。
        Equal("png", AwakeImageShapeAdapter.SniffFormat(SampleImages.Png()), "png 签名");
        Equal("jpg", AwakeImageShapeAdapter.SniffFormat(SampleImages.Jpeg()), "jpg 签名");
        Equal("gif", AwakeImageShapeAdapter.SniffFormat(SampleImages.Gif()), "gif 签名");
        Equal("bmp", AwakeImageShapeAdapter.SniffFormat(SampleImages.Bmp()), "bmp 签名");
        Equal("webp", AwakeImageShapeAdapter.SniffFormat(SampleImages.Webp()), "webp 签名");
        Equal("png", AwakeImageShapeAdapter.SniffFormat(Convert.FromBase64String(AwakePortraitProbeFixture.PngBase64)),
            "仓库自带立绘探测图也是真 png");

        // 认不出来就是 unknown —— 不做任何假定。
        Equal("unknown", AwakeImageShapeAdapter.SniffFormat(new byte[] { 0x00, 0x01, 0x02, 0x03 }), "无签名");
        Equal("unknown", AwakeImageShapeAdapter.SniffFormat(new byte[] { 0x89, 0x50, 0x4E }), "只有 3 字节（png 截断）");
        Equal("unknown", AwakeImageShapeAdapter.SniffFormat(null), "null");
        Equal("unknown", AwakeImageShapeAdapter.SniffFormat(new byte[0]), "空数组");
        Equal("unknown", AwakeImageShapeAdapter.SniffFormat(Encoding.ASCII.GetBytes("XIF89")), "近似但不对的头");
        Equal("gif", AwakeImageShapeAdapter.SniffFormat(Encoding.ASCII.GetBytes("GIF89")),
            "只认 4 字节签名：GIF89 命中即判 gif（如实记录这个边界）");

        // 尺寸：与独立生产者（Pillow 自报常量）对齐。
        Dimensions(SampleImages.Png(), "png", SampleImages.PngWidth, SampleImages.PngHeight);
        Dimensions(SampleImages.Jpeg(), "jpg", SampleImages.JpegWidth, SampleImages.JpegHeight);
        Dimensions(SampleImages.Gif(), "gif", SampleImages.GifWidth, SampleImages.GifHeight);
        Dimensions(Convert.FromBase64String(AwakePortraitProbeFixture.PngBase64), "png",
            AwakePortraitProbeFixture.Width, AwakePortraitProbeFixture.Height);

        // 不在尺寸表里的形状：如实报 0，不许硬猜。
        Dimensions(SampleImages.Bmp(), "bmp", 0, 0);
        Dimensions(SampleImages.Webp(), "webp", 0, 0);
        Dimensions(new byte[] { 1, 2, 3 }, "unknown", 0, 0);
        Dimensions(Encoding.ASCII.GetBytes("GIF89"), "gif", 0, 0);
        Dimensions(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "jpg", 0, 0);
    }

    private static void Dimensions(byte[] bytes, string format, int expectedWidth, int expectedHeight)
    {
        int width;
        int height;
        bool ok = AwakeImageShapeAdapter.TryReadDimensions(bytes, format, out width, out height);
        if (expectedWidth == 0 && expectedHeight == 0)
        {
            Check(!ok, format + "：不在尺寸表里时应当返回 false，不该硬猜");
            EqualInt(0, width, format + " 宽");
            EqualInt(0, height, format + " 高");
            return;
        }

        Check(ok, format + "：应当读到尺寸");
        EqualInt(expectedWidth, width, format + " 宽（判据来自独立生产者）");
        EqualInt(expectedHeight, height, format + " 高（判据来自独立生产者）");
    }

    // ── 10. 自称 mime 不可信 ────────────────────────────────────────────────

    private static void TestDeclaredMimeIsNotTrusted()
    {
        AwakeImageReply reply;
        string error;

        // 自称 png，头是 JPEG。
        Check(AwakeImageShapeAdapter.TryParseReply(AwakeImageShape.Player2,
            "{\"mimetype\":\"image/png\",\"image\":\"" + Convert.ToBase64String(SampleImages.Jpeg()) + "\"}",
            out reply, out error), "解析应当成功：" + error);
        Equal("jpg", reply.Format, "自称 png 但头是 JPEG ⇒ 必须以文件头为准");
        Equal("image/png", reply.DeclaredMime, "自称要如实记下来，供日志/排查");
        Contains(reply.Describe(), "jpg", "Describe 应给出真实格式");

        // 反向：自称 jpeg，头是 PNG。
        Check(AwakeImageShapeAdapter.TryParseReply(AwakeImageShape.Player2,
            "{\"mime_type\":\"image/jpeg\",\"image\":\"" + Convert.ToBase64String(SampleImages.Png()) + "\"}",
            out reply, out error), "解析应当成功：" + error);
        Equal("png", reply.Format, "自称 jpeg 但头是 PNG ⇒ 以文件头为准");
        Equal("image/jpeg", reply.DeclaredMime, "自称要如实记下来");

        // 底线：认不出格式时不许拿 .png 硬装。
        byte[] junk = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC };
        Check(AwakeImageShapeAdapter.TryParseReply(AwakeImageShape.Player2,
            "{\"image\":\"" + Convert.ToBase64String(junk) + "\"}", out reply, out error),
            "unknown 也必须能解出来（字节本身合法）：" + error);
        Equal("unknown", reply.Format, "认不出就是 unknown，不许默认成 png");
        BytesEqual(junk, reply.Bytes, "unknown 时字节必须原样保留");
        EqualInt(junk.Length, reply.Bytes.Length, "unknown 时不截断、不补头");
    }

    // ── 11. 真打一发：Player2 形状 + 不开云端（无鉴权）─────────────────────

    private static void TestLiveGenerateNoAuthPlayer2()
    {
        byte[] png = SampleImages.Png();
        // 服务端回的是**带前缀**的那种（实测形状），不是文档写的裸 base64。
        string payload = "{\"image\":\"data:image/png;base64," + Convert.ToBase64String(png) + "\"}";

        using (FakeHttpServer server = new FakeHttpServer(request => FakeReply.Json(200, payload)))
        {
            AwakeConfig config = new AwakeConfig();
            // 故意粘**完整接口地址**，验证"玩家粘满也能用"。
            config.PortraitImageBaseUrl = server.BaseUrl + "/v1/image/generate";
            config.PortraitImageIsCloud = false;

            AwakeImageEndpoint endpoint;
            string error;
            Check(AwakeImageEndpointResolver.TryResolve(config, out endpoint, out error), "端点解析失败：" + error);
            Equal(server.BaseUrl + "/v1", endpoint.BaseUrl, "粘完整地址应归一化到 root");

            // 请求 256x256，但服务端给的是 320x192 ⇒ 落盘尺寸必须按文件头，不按请求。
            AwakeImageRequest request = new AwakeImageRequest("一个骑士的半身像", null, 256, 256);
            AwakeImageOutcome outcome = AwakeImageClient
                .GenerateAsync(endpoint, request, null, CancellationToken.None)
                .GetAwaiter().GetResult();

            Check(outcome.Ok, "出图应当成功，实际 " + outcome.ErrorCode + " / " + outcome.ErrorMessage);
            Check(outcome.Reply != null, "成功必须带回复");
            EqualInt(200, outcome.StatusCode, "状态码");
            Equal("/v1/image/generate", outcome.RequestUrl.Substring(server.BaseUrl.Length), "RequestUrl 的路径部分");
            Equal("png", outcome.Reply.Format, "产出格式");
            EqualInt(SampleImages.PngWidth, outcome.Reply.Width, "真实宽度必须来自文件头（请求填的是 256）");
            EqualInt(SampleImages.PngHeight, outcome.Reply.Height, "真实高度必须来自文件头（请求填的是 192/256 都不对）");
            BytesEqual(png, outcome.Reply.Bytes, "收到的字节必须与样本逐字节相同（前缀剥干净了）");

            CapturedRequest seen = server.LastRequest;
            Check(seen != null, "服务端应当收到请求");
            Equal("POST", seen.Method, "必须是 POST");
            Equal("/v1/image/generate", seen.Path, "无参考图必须打 /image/generate");
            Equal("application/json; charset=utf-8", seen.Headers["Content-Type"], "Content-Type");
            Check(!seen.Headers.ContainsKey("Authorization"), "本机免认证（未开云端）不得带 Authorization");

            JObject sent = JObject.Parse(seen.Body);
            EqualInt(256, sent["width"].Value<int>(), "请求体里应是我们填的 256");
            EqualInt(256, sent["height"].Value<int>(), "请求体里应是我们填的 256");
            Check(sent["image"] == null, "无参考图时请求体不得带 image");
            Equal("一个骑士的半身像", sent["prompt"].ToString(), "prompt 要按 UTF-8 原样过网（中文不许乱码）");
        }
    }

    // ── 12. 真打一发：OpenAI 兼容形状 + 开云端（带鉴权）────────────────────

    private static void TestLiveGenerateWithAuthOpenAi()
    {
        byte[] png = SampleImages.Png();
        string payload = "{\"data\":[{\"b64_json\":\"" + Convert.ToBase64String(png) + "\"}]}";

        using (FakeHttpServer server = new FakeHttpServer(request => FakeReply.Json(200, payload)))
        {
            AwakeConfig config = new AwakeConfig();
            config.PortraitImageBaseUrl = server.BaseUrl + "/v1/images/generations";
            config.PortraitImageIsCloud = true;
            config.PortraitImageShape.SelectedIndex = 1;

            AwakeImageEndpoint endpoint;
            string error;
            Check(AwakeImageEndpointResolver.TryResolve(config, out endpoint, out error), "端点解析失败：" + error);
            Equal("openai_compatible", AwakeImageEndpointResolver.ShapeId(endpoint.Shape), "形状");
            Equal(server.BaseUrl + "/v1", endpoint.BaseUrl, "粘完整地址应归一化到 root");

            byte[] reference = SampleImages.Jpeg();
            AwakeImageRequest request = new AwakeImageRequest("同一个人的侧脸", reference, 256, 256);
            AwakeImageOutcome outcome = AwakeImageClient
                .GenerateAsync(endpoint, request, "test-key-123", CancellationToken.None)
                .GetAwaiter().GetResult();

            Check(outcome.Ok, "出图应当成功，实际 " + outcome.ErrorCode + " / " + outcome.ErrorMessage);
            Equal("png", outcome.Reply.Format, "产出格式");
            EqualInt(SampleImages.PngWidth, outcome.Reply.Width, "真实宽度来自文件头");
            BytesEqual(png, outcome.Reply.Bytes, "字节逐一致");

            CapturedRequest seen = server.LastRequest;
            Equal("/v1/images/generations", seen.Path, "OpenAI 形状固定打 /images/generations（有参考图也一样）");
            Equal("Bearer test-key-123", seen.Headers["Authorization"], "云端必须带 Authorization");

            JObject sent = JObject.Parse(seen.Body);
            Equal("256x256", sent["size"].ToString(), "size 用 WxH 字符串");
            Check(sent["image"] != null, "有参考图必须带 image");
            Check(sent["image"].ToString().StartsWith("data:image/png;base64,", StringComparison.Ordinal),
                "OpenAI 形状的 image 必须带 data URI 前缀，实际 " + Leading(sent["image"].ToString()));
            BytesEqual(reference, Convert.FromBase64String(AwakeImageShapeAdapter.StripDataUri(sent["image"].ToString())),
                "带着前缀送出去的仍必须是参考图本体");
        }
    }

    // ── 13. 真打一发：Player2 形状 + 开云端（带鉴权）─────────────────────────
    // 关键对角：形状是 Player2（image 必须是裸 base64），但开了云端（必须带 Bearer）。
    // 证明"鉴权只看 IsCloud、与形状无关"——云端 Player2 不能因为"本地 App 习惯"就漏掉 Authorization。
    private static void TestLiveGenerateWithAuthPlayer2()
    {
        byte[] png = SampleImages.Png();
        // 服务端回带前缀的体（实测形状），顺便验证收侧剥前缀。
        string payload = "{\"image\":\"data:image/png;base64," + Convert.ToBase64String(png) + "\"}";

        using (FakeHttpServer server = new FakeHttpServer(request => FakeReply.Json(200, payload)))
        {
            AwakeConfig config = new AwakeConfig();
            config.PortraitImageBaseUrl = server.BaseUrl + "/v1/image/generate";
            config.PortraitImageIsCloud = true; // 云端

            AwakeImageEndpoint endpoint;
            string error;
            Check(AwakeImageEndpointResolver.TryResolve(config, out endpoint, out error), "端点解析失败：" + error);
            Equal("player2", AwakeImageEndpointResolver.ShapeId(endpoint.Shape), "形状仍是 Player2");
            Equal(server.BaseUrl + "/v1", endpoint.BaseUrl, "粘完整地址应归一化到 root");

            byte[] reference = SampleImages.Jpeg();
            AwakeImageRequest request = new AwakeImageRequest("同一个人的侧脸", reference, 256, 256);
            AwakeImageOutcome outcome = AwakeImageClient
                .GenerateAsync(endpoint, request, "test-key-123", CancellationToken.None)
                .GetAwaiter().GetResult();

            Check(outcome.Ok, "出图应当成功，实际 " + outcome.ErrorCode + " / " + outcome.ErrorMessage);
            Equal("png", outcome.Reply.Format, "产出格式");
            EqualInt(SampleImages.PngWidth, outcome.Reply.Width, "真实宽度来自文件头");
            BytesEqual(png, outcome.Reply.Bytes, "字节逐一致");

            CapturedRequest seen = server.LastRequest;
            Equal("/v1/image/edit", seen.Path, "Player2 形状有参考图打 /image/edit");
            Equal("Bearer test-key-123", seen.Headers["Authorization"], "云端（无论形状）必须带 Authorization");

            JObject sent = JObject.Parse(seen.Body);
            Check(sent["image"] != null, "有参考图必须带 image");
            string image = sent["image"].ToString();
            Check(!image.StartsWith("data:", StringComparison.OrdinalIgnoreCase),
                "Player2 形状的 image 必须是裸 base64，即使开云端也不带 data: 前缀，实际 " + Leading(image));
            BytesEqual(reference, Convert.FromBase64String(image),
                "云端 Player2 发出的仍是参考图本体（裸 base64）");
        }
    }

    // ── 14. 真打一发：OpenAI 兼容形状 + 不开云端（无鉴权）──────────────────
    // 另一个对角：形状是 OpenAI 兼容（image 必须带 data: 前缀），但本机跑（不得带 Bearer）。
    // 证明"本机服务即使走 OpenAI 形状也不该发 Authorization"——否则可能被本机免认证服务拒掉。
    private static void TestLiveGenerateNoAuthOpenAi()
    {
        byte[] png = SampleImages.Png();
        string payload = "{\"data\":[{\"b64_json\":\"" + Convert.ToBase64String(png) + "\"}]}";

        using (FakeHttpServer server = new FakeHttpServer(request => FakeReply.Json(200, payload)))
        {
            AwakeConfig config = new AwakeConfig();
            config.PortraitImageBaseUrl = server.BaseUrl + "/v1/images/generations";
            config.PortraitImageIsCloud = false; // 本机
            config.PortraitImageShape.SelectedIndex = 1;

            AwakeImageEndpoint endpoint;
            string error;
            Check(AwakeImageEndpointResolver.TryResolve(config, out endpoint, out error), "端点解析失败：" + error);
            Equal("openai_compatible", AwakeImageEndpointResolver.ShapeId(endpoint.Shape), "形状仍是 OpenAI 兼容");
            Equal(server.BaseUrl + "/v1", endpoint.BaseUrl, "粘完整地址应归一化到 root");

            byte[] reference = SampleImages.Jpeg();
            AwakeImageRequest request = new AwakeImageRequest("同一个人的侧脸", reference, 256, 256);
            AwakeImageOutcome outcome = AwakeImageClient
                .GenerateAsync(endpoint, request, null, CancellationToken.None)
                .GetAwaiter().GetResult();

            Check(outcome.Ok, "出图应当成功，实际 " + outcome.ErrorCode + " / " + outcome.ErrorMessage);
            Equal("png", outcome.Reply.Format, "产出格式");
            EqualInt(SampleImages.PngWidth, outcome.Reply.Width, "真实宽度来自文件头");
            BytesEqual(png, outcome.Reply.Bytes, "字节逐一致");

            CapturedRequest seen = server.LastRequest;
            Equal("/v1/images/generations", seen.Path, "OpenAI 形状固定打 /images/generations");
            Check(!seen.Headers.ContainsKey("Authorization"), "本机（无论形状）不得带 Authorization");

            JObject sent = JObject.Parse(seen.Body);
            Check(sent["image"] != null, "有参考图必须带 image");
            string image = sent["image"].ToString();
            Check(image.StartsWith("data:image/png;base64,", StringComparison.Ordinal),
                "OpenAI 兼容形状的 image 必须带 data URI 前缀，实际 " + Leading(image));
            BytesEqual(reference, Convert.FromBase64String(AwakeImageShapeAdapter.StripDataUri(image)),
                "带着前缀送出去的仍必须是参考图本体");
        }
    }

    // ── 15. HTTP 失败态映射成人话 ───────────────────────────────────────────

    private static void TestLiveHttpFailureMapping()
    {
        var cases = new[]
        {
            new { Status = 401, Body = "{\"error\":\"invalid api key\",\"error_code\":\"invalid_api_key\",\"request_id\":\"r1\"}", Code = "invalid_api_key", Must = "未认证", Alert = "invalid api key" },
            new { Status = 402, Body = "{\"error\":\"insufficient balance\"}", Code = "http_402", Must = "额度", Alert = "insufficient balance" },
            new { Status = 403, Body = "{\"message\":\"content blocked\"}", Code = "http_403", Must = "被拒", Alert = "content blocked" },
            new { Status = 404, Body = "{\"error\":\"not found\"}", Code = "http_404", Must = "接口不存在", Alert = "not found" },
            new { Status = 422, Body = "{\"detail\":\"bad size\"}", Code = "http_422", Must = "参数未被接受", Alert = "bad size" },
            new { Status = 429, Body = "{\"message\":\"rate limited\"}", Code = "http_429", Must = "过于频繁", Alert = "rate limited" },
            new { Status = 500, Body = "{\"detail\":\"boom\"}", Code = "http_500", Must = "内部错误", Alert = "boom" },
            new { Status = 502, Body = "<html>bad gateway</html>", Code = "http_502", Must = "内部错误", Alert = "bad gateway" },
        };

        foreach (var item in cases)
        {
            int status = item.Status;
            string body = item.Body;
            using (FakeHttpServer server = new FakeHttpServer(request => FakeReply.Json(status, body)))
            {
                AwakeConfig config = new AwakeConfig();
                config.PortraitImageBaseUrl = server.BaseUrl + "/v1";

                AwakeImageEndpoint endpoint;
                string error;
                Check(AwakeImageEndpointResolver.TryResolve(config, out endpoint, out error), "端点解析失败：" + error);

                AwakeImageOutcome outcome = AwakeImageClient
                    .GenerateAsync(endpoint, new AwakeImageRequest("x", null, 64, 64), null, CancellationToken.None)
                    .GetAwaiter().GetResult();

                Check(!outcome.Ok, "HTTP " + status + " 必须判失败");
                EqualInt(status, outcome.StatusCode, "HTTP " + status + " 状态码要如实带出");
                Equal(item.Code, outcome.ErrorCode, "HTTP " + status + " 的错误码");
                Contains(outcome.ErrorMessage, item.Must, "HTTP " + status + " 的人话");
                Contains(outcome.ErrorMessage, item.Alert, "HTTP " + status + " 要把服务端原文带上");
                Check(outcome.Reply == null, "失败时不得给出图片");
            }
        }
    }

    // ── 14. 连不上 ──────────────────────────────────────────────────────────

    private static void TestLiveUnreachableService()
    {
        int port = FindClosedPort();
        AwakeConfig config = new AwakeConfig();
        config.PortraitImageBaseUrl = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/v1";

        AwakeImageEndpoint endpoint;
        string error;
        Check(AwakeImageEndpointResolver.TryResolve(config, out endpoint, out error), "端点解析失败：" + error);

        AwakeImageOutcome outcome = AwakeImageClient
            .GenerateAsync(endpoint, new AwakeImageRequest("x", null, 64, 64), null, CancellationToken.None)
            .GetAwaiter().GetResult();

        Check(!outcome.Ok, "连不上的端口必须判失败");
        Equal("image_unreachable", outcome.ErrorCode, "连不上 → image_unreachable");
        Contains(outcome.ErrorMessage, "连不上出图服务", "要给人话而不是裸异常");
        Check(outcome.Reply == null, "失败时不得给出图片");
    }

    private static int FindClosedPort()
    {
        TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    // ── 15. 取消 / 超时归一到同一条错误 ─────────────────────────────────────

    private static void TestCancelledRequestReportsTimeout()
    {
        using (CancellationTokenSource scope = AwakeImageClient.CreateTimeoutScope())
        {
            Check(!scope.IsCancellationRequested, "刚建的超时窗口不应已取消");
            Check(scope.Token.CanBeCanceled, "超时窗口必须可取消");
        }

        using (FakeHttpServer server = new FakeHttpServer(request => FakeReply.Json(200, "{}")))
        {
            AwakeConfig config = new AwakeConfig();
            config.PortraitImageBaseUrl = server.BaseUrl + "/v1";

            AwakeImageEndpoint endpoint;
            string error;
            Check(AwakeImageEndpointResolver.TryResolve(config, out endpoint, out error), "端点解析失败：" + error);

            using (CancellationTokenSource cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                AwakeImageOutcome outcome = AwakeImageClient
                    .GenerateAsync(endpoint, new AwakeImageRequest("x", null, 64, 64), null, cancelled.Token)
                    .GetAwaiter().GetResult();

                Check(!outcome.Ok, "已取消的请求必须失败");
                Equal("image_timeout", outcome.ErrorCode, "取消/超时应归一到 image_timeout");
                Contains(outcome.ErrorMessage, "超时", "要给人话");
                Check(outcome.Reply == null, "失败时不得给出图片");
            }
        }
    }

    // ── 共用断言小工具 ──────────────────────────────────────────────────────

    private static AwakeImageReply Parses(AwakeImageShape shape, string body, string expectedFormat, int expectedWidth, int expectedHeight, string what)
    {
        AwakeImageReply reply;
        string error;
        Check(AwakeImageShapeAdapter.TryParseReply(shape, body, out reply, out error),
            what + "：解析应当成功，实际：" + error);
        Check(reply != null, what + "：成功必须带回复");
        Equal(expectedFormat, reply.Format, what + "（格式）");
        EqualInt(expectedWidth, reply.Width, what + "（宽）");
        EqualInt(expectedHeight, reply.Height, what + "（高）");
        return reply;
    }

    private static void RejectsPayload(AwakeImageShape shape, string body, string expectedFragment, string what)
    {
        AwakeImageReply reply;
        string error;
        Check(!AwakeImageShapeAdapter.TryParseReply(shape, body, out reply, out error),
            what + "：应当判失败（但解析成功了）");
        Check(reply == null, what + "：失败时不得给出回复");
        Contains(error, expectedFragment, what + "：错误原因");
    }
}

// ── 本机假端点：够用就好的 HTTP/1.1 服务 ────────────────────────────────────
//
// 刻意不用 HttpListener：Windows 上它要 URL ACL，非管理员跑会挂；
// 裸 TcpListener 没有这个门槛，而且能把原始请求（方法/路径/头/体）原封不动抓下来当证据。
//
// 必须处理 Expect: 100-continue —— .NET Framework 的 HttpClient 对 POST 默认会发它，
// 不回 100 的话客户端要空等一小会儿才发体。

internal sealed class CapturedRequest
{
    internal string Method { get; set; } = string.Empty;
    internal string Path { get; set; } = string.Empty;
    internal string Body { get; set; } = string.Empty;
    internal Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

internal sealed class FakeReply
{
    internal int Status { get; set; } = 200;
    internal string Body { get; set; } = "{}";

    internal static FakeReply Json(int status, string body)
    {
        return new FakeReply { Status = status, Body = body };
    }
}

internal sealed class FakeHttpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Thread _thread;
    private readonly Func<CapturedRequest, FakeReply> _handler;
    private volatile bool _stopping;
    private CapturedRequest _last;

    internal FakeHttpServer(Func<CapturedRequest, FakeReply> handler)
    {
        _handler = handler;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _thread = new Thread(Loop) { IsBackground = true, Name = "fake-image-server" };
        _thread.Start();
    }

    internal int Port { get; }

    internal string BaseUrl
    {
        get { return "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture); }
    }

    internal CapturedRequest LastRequest
    {
        get { return Volatile.Read(ref _last); }
    }

    public void Dispose()
    {
        _stopping = true;
        try { _listener.Stop(); } catch { }
        try { _thread.Join(2000); } catch { }
    }

    private void Loop()
    {
        while (!_stopping)
        {
            TcpClient client;
            try
            {
                client = _listener.AcceptTcpClient();
            }
            catch (SocketException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (InvalidOperationException) { return; }

            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    Handle(stream);
                }
            }
            catch
            {
                // 单条连接出错不该影响被测代码的判定：客户端会拿到连接被关，转成自己的错误分支。
            }
        }
    }

    private void Handle(NetworkStream stream)
    {
        byte[] buffer = new byte[8192];
        MemoryStream raw = new MemoryStream();
        int headerEnd = -1;
        while (headerEnd < 0)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) return;
            raw.Write(buffer, 0, read);
            headerEnd = IndexOfHeaderEnd(raw.GetBuffer(), (int)raw.Length);
        }

        string headerText = Encoding.ASCII.GetString(raw.GetBuffer(), 0, headerEnd);
        int bodyStart = headerEnd + 4;

        string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
        string[] requestLine = lines[0].Split(' ');
        CapturedRequest captured = new CapturedRequest
        {
            Method = requestLine.Length > 0 ? requestLine[0] : string.Empty,
            Path = requestLine.Length > 1 ? requestLine[1] : string.Empty
        };

        for (int i = 1; i < lines.Length; i++)
        {
            int colon = lines[i].IndexOf(':');
            if (colon <= 0) continue;
            captured.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
        }

        string expect;
        if (captured.Headers.TryGetValue("Expect", out expect)
            && expect.IndexOf("100-continue", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            byte[] interim = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
            stream.Write(interim, 0, interim.Length);
            stream.Flush();
        }

        int contentLength = 0;
        string lengthText;
        if (captured.Headers.TryGetValue("Content-Length", out lengthText))
            int.TryParse(lengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength);

        MemoryStream body = new MemoryStream();
        int alreadyHave = (int)raw.Length - bodyStart;
        if (alreadyHave > 0)
        {
            int take = Math.Min(alreadyHave, contentLength);
            body.Write(raw.GetBuffer(), bodyStart, take);
        }

        while (body.Length < contentLength)
        {
            int want = Math.Min(buffer.Length, contentLength - (int)body.Length);
            int read = stream.Read(buffer, 0, want);
            if (read <= 0) break;
            body.Write(buffer, 0, read);
        }

        captured.Body = Encoding.UTF8.GetString(body.ToArray());
        Volatile.Write(ref _last, captured);

        FakeReply reply = _handler(captured) ?? new FakeReply();
        byte[] payload = Encoding.UTF8.GetBytes(reply.Body ?? string.Empty);
        string head = "HTTP/1.1 " + reply.Status.ToString(CultureInfo.InvariantCulture) + " " + Reason(reply.Status) + "\r\n"
            + "Content-Type: application/json; charset=utf-8\r\n"
            + "Content-Length: " + payload.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
            + "Connection: close\r\n\r\n";
        byte[] headBytes = Encoding.ASCII.GetBytes(head);
        stream.Write(headBytes, 0, headBytes.Length);
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }

    private static int IndexOfHeaderEnd(byte[] data, int length)
    {
        for (int i = 0; i + 3 < length; i++)
        {
            if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10) return i;
        }
        return -1;
    }

    private static string Reason(int status)
    {
        switch (status)
        {
            case 200: return "OK";
            case 400: return "Bad Request";
            case 401: return "Unauthorized";
            case 402: return "Payment Required";
            case 403: return "Forbidden";
            case 404: return "Not Found";
            case 422: return "Unprocessable Entity";
            case 429: return "Too Many Requests";
            case 500: return "Internal Server Error";
            case 502: return "Bad Gateway";
            default: return "Status";
        }
    }
}
