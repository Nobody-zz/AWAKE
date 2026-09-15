using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Awake;

/// <summary>一次出图的结局。成功带图，失败带人话原因与原始状态码。</summary>
internal sealed class AwakeImageOutcome
{
    internal bool Ok { get; set; }
    internal string ErrorCode { get; set; } = string.Empty;
    internal string ErrorMessage { get; set; } = string.Empty;
    internal AwakeImageReply Reply { get; set; }
    internal long ElapsedMs { get; set; }
    internal int StatusCode { get; set; }
    internal string RequestUrl { get; set; } = string.Empty;
}

/// <summary>
/// 模组进程里的出网口。**这是 AWAKE 第一次从游戏进程直接发 HTTP**（此前全走 Runtime 进程），
/// 所以三件事必须守：
///   · TLS 显式抬到 1.2（net472 的默认不保证）；
///   · 只在后台线程跑，**绝不在 tick / VM 命令里同步等**；
///   · 日志只记 url / 形状 / 状态码 / 耗时 / 产出格式，**绝不记请求体与钥匙**。
/// </summary>
internal static class AwakeImageClient
{
    private const int TimeoutSeconds = 120;

    private static readonly object Gate = new object();
    private static HttpClient client;

    private static HttpClient Client
    {
        get
        {
            lock (Gate)
            {
                if (client == null) client = CreateClient();
                return client;
            }
        }
    }

    /// <summary>把进程的 TLS 抬到 1.2。幂等，代价可忽略。</summary>
    internal static void EnsureModernTls()
    {
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("image_tls_setup_failed error=" + ex.Message);
        }
    }

    private static HttpClient CreateClient()
    {
        EnsureModernTls();
        HttpClientHandler handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        // 超时交给 CancellationToken 管，这样"连接超时"和"读体超时"是同一个可取消的窗口。
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    internal static async Task<AwakeImageOutcome> GenerateAsync(
        AwakeImageEndpoint endpoint,
        AwakeImageRequest request,
        string apiKey,
        CancellationToken cancellationToken)
    {
        if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

        string url = endpoint.BaseUrl.TrimEnd('/')
            + AwakeImageShapeAdapter.EndpointPath(endpoint.Shape, request.HasReference);

        string body = AwakeImageShapeAdapter.BuildRequestBody(endpoint.Shape, request);
        bool needsAuth = AwakeImageShapeAdapter.RequiresAuthorization(endpoint.Shape, endpoint.IsCloud);

        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            using (HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, url))
            {
                message.Content = new StringContent(body, Encoding.UTF8, "application/json");
                if (needsAuth)
                {
                    // 钥匙只在这一行出现，且不进任何日志分支。
                    message.Headers.TryAddWithoutValidation("Authorization", "Bearer " + (apiKey ?? string.Empty));
                }

                using (HttpResponseMessage response = await Client
                    .SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken)
                    .ConfigureAwait(false))
                {
                    string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    watch.Stop();

                    if (!response.IsSuccessStatusCode)
                    {
                        return new AwakeImageOutcome
                        {
                            Ok = false,
                            StatusCode = (int)response.StatusCode,
                            ElapsedMs = watch.ElapsedMilliseconds,
                            RequestUrl = url,
                            ErrorCode = ExtractErrorCode(text) ?? ("http_" + (int)response.StatusCode),
                            ErrorMessage = DescribeHttpFailure((int)response.StatusCode, text)
                        };
                    }

                    AwakeImageReply reply;
                    string parseError;
                    if (!AwakeImageShapeAdapter.TryParseReply(endpoint.Shape, text, out reply, out parseError))
                    {
                        return new AwakeImageOutcome
                        {
                            Ok = false,
                            StatusCode = (int)response.StatusCode,
                            ElapsedMs = watch.ElapsedMilliseconds,
                            RequestUrl = url,
                            ErrorCode = "image_payload_invalid",
                            ErrorMessage = parseError
                        };
                    }

                    return new AwakeImageOutcome
                    {
                        Ok = true,
                        StatusCode = (int)response.StatusCode,
                        ElapsedMs = watch.ElapsedMilliseconds,
                        RequestUrl = url,
                        Reply = reply
                    };
                }
            }
        }
        catch (OperationCanceledException)
        {
            watch.Stop();
            return new AwakeImageOutcome
            {
                Ok = false,
                ElapsedMs = watch.ElapsedMilliseconds,
                RequestUrl = url,
                ErrorCode = "image_timeout",
                ErrorMessage = "出图请求超时（" + TimeoutSeconds + " 秒）。"
                    + "若是云端服务，先确认网络能通；若是本机服务，确认 App 正在运行。"
            };
        }
        catch (HttpRequestException ex)
        {
            watch.Stop();
            return new AwakeImageOutcome
            {
                Ok = false,
                ElapsedMs = watch.ElapsedMilliseconds,
                RequestUrl = url,
                ErrorCode = "image_unreachable",
                ErrorMessage = "连不上出图服务：" + ex.Message
                    + "。请确认地址、端口与服务是否已启动。"
            };
        }
        catch (Exception ex)
        {
            watch.Stop();
            return new AwakeImageOutcome
            {
                Ok = false,
                ElapsedMs = watch.ElapsedMilliseconds,
                RequestUrl = url,
                ErrorCode = "image_unexpected",
                ErrorMessage = "出图请求失败：" + ex.Message
            };
        }
    }

    internal static CancellationTokenSource CreateTimeoutScope()
    {
        return new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
    }

    /// <summary>
    /// 状态码 → 人话。分类取自实测见过的错误体形状
    /// （<c>{ error, error_code, request_id, … }</c>）。
    /// </summary>
    internal static string DescribeHttpFailure(int statusCode, string body)
    {
        string detail = ExtractErrorMessage(body);
        string suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : "：" + detail;

        switch (statusCode)
        {
            case 401:
                return "出图服务未认证（401）。若是云端服务，请填写出图 API Key；本机服务请把“走云端”关掉。" + suffix;
            case 402:
                return "出图服务余额或额度不足（402）。" + suffix;
            case 403:
                return "出图请求被拒（403），可能是内容策略或区域限制。" + suffix;
            case 404:
                return "出图接口不存在（404）。请核对地址是否写成了完整接口路径，或形状选错了。" + suffix;
            case 422:
                return "出图参数未被接受（422），可能是提示词或尺寸不合规。" + suffix;
            case 429:
                return "出图请求过于频繁（429），请稍后再试。" + suffix;
            default:
                if (statusCode >= 500)
                {
                    return "出图服务内部错误（" + statusCode + "），稍后重试即可。" + suffix;
                }

                return "出图请求被拒绝（" + statusCode + "）。" + suffix;
        }
    }

    private static string ExtractErrorCode(string body)
    {
        try
        {
            Newtonsoft.Json.Linq.JObject json = Newtonsoft.Json.Linq.JObject.Parse(body ?? string.Empty);
            string code = json["error_code"]?.ToString();
            return string.IsNullOrWhiteSpace(code) ? null : code;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractErrorMessage(string body)
    {
        string text = (body ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (text.Length == 0) return string.Empty;

        try
        {
            Newtonsoft.Json.Linq.JObject json = Newtonsoft.Json.Linq.JObject.Parse(text);
            string message = json["error"]?.ToString()
                ?? json["message"]?.ToString()
                ?? json["detail"]?.ToString();
            if (!string.IsNullOrWhiteSpace(message)) text = message;
        }
        catch
        {
            // 不是 JSON 就原样截断。
        }

        return text.Length <= 240 ? text : text.Substring(0, 240) + "…";
    }
}
