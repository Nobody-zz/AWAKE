using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Awake.WorldbookStudio.Core;

internal static class ProviderErrorMapping
{
    private const int MaximumDetailLength = 480;

    public static InvalidOperationException MapCloud(HttpStatusCode statusCode, string? body)
        => Map(statusCode, body, "云端 Provider");

    public static InvalidOperationException MapLocalWorker(HttpStatusCode statusCode, string? body, string operation)
        => Map(statusCode, body, $"本机 Worker {operation}");

    public static string SanitizeDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = Regex.Replace(value, @"\s+", " ").Trim();
        text = Regex.Replace(text, "(?i)(api[-_ ]?key|authorization|bearer|token|secret|password)\\s*[:=]\\s*[^,\\s;]+", "$1=[已隐藏]");
        text = Regex.Replace(text, @"(?i)\b(?:sk|key|token|secret)[-_][A-Za-z0-9._-]{8,}\b", "[凭据已隐藏]");
        text = Regex.Replace(text, "(?i)\\b[A-Z]:\\\\[^\\r\\n\\s]+", "[路径已隐藏]");
        text = Regex.Replace(text, @"https?://\S+", "[地址已隐藏]");
        text = new string(text.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return text.Length <= MaximumDetailLength ? text : text[..MaximumDetailLength] + "…";
    }

    private static InvalidOperationException Map(HttpStatusCode statusCode, string? body, string providerLabel)
    {
        var detail = ExtractDetail(body);
        var (code, message) = Classify(statusCode, body);
        var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" Provider 返回：{detail}";
        return new InvalidOperationException($"{code}: {providerLabel}：{message}{suffix}");
    }

    private static (string Code, string Message) Classify(HttpStatusCode statusCode, string? body)
    {
        var value = (body ?? string.Empty).ToLowerInvariant();
        var status = (int)statusCode;
        if (status is 401 or 403 || ContainsAny(value, "api key", "apikey", "authentication", "unauthorized", "invalid credential"))
            return ("WB-AI-AUTH-401", "Provider 拒绝了凭据");
        if (status == 408) return ("WB-AI-TIMEOUT-408", "Provider 请求超时");
        if (status == 429) return ("WB-AI-RATE-429", "Provider 当前限流");
        if (status is 413) return ("WB-AI-REQUEST-413", "发送给 Provider 的请求超过大小限制");
        if (status is 400 or 422)
        {
            if (ContainsAny(value,
                "model_not_found",
                "model not found",
                "unknown model",
                "invalid model",
                "model does not exist",
                "no such model",
                "model is not available",
                "deployment not found"))
                return ("WB-AI-MODEL-422", "Provider 不接受当前模型名称");
            if (ContainsAny(value,
                "unsupported parameter",
                "unknown parameter",
                "unrecognized parameter",
                "additional propert",
                "invalid parameter",
                "parameter is not allowed",
                "not allowed"))
                return ("WB-AI-PARAMETER-422", "Provider 不接受当前请求参数");
            return ($"WB-AI-HTTP-{status}", "Provider 拒绝了请求");
        }
        if (status == 404)
            return (ContainsAny(value, "model not found", "model_not_found", "deployment not found", "deployment does not exist") ? "WB-AI-MODEL-404" : "WB-AI-ENDPOINT-404", "Provider 找不到请求的资源");
        if (status is >= 500 and <= 599) return ("WB-AI-UPSTREAM-5XX", "Provider 返回了上游错误");
        return ($"WB-AI-HTTP-{status}", "Provider 返回了不可接受的 HTTP 状态");
    }

    private static string ExtractDetail(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        try
        {
            var root = JsonNode.Parse(body);
            var parts = new List<string>();
            if (root is JsonObject objectRoot)
            {
                if (objectRoot["error"] is JsonObject error)
                {
                    AddValue(parts, error, "code");
                    AddValue(parts, error, "type");
                    AddValue(parts, error, "message");
                    AddValue(parts, error, "detail");
                }
                AddValue(parts, objectRoot, "code");
                AddValue(parts, objectRoot, "message");
                AddValue(parts, objectRoot, "detail");
                AddValue(parts, objectRoot, "error_description");
            }
            var parsed = SanitizeDetail(string.Join("；", parts.Distinct(StringComparer.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(parsed)) return parsed;
        }
        catch (JsonException)
        {
        }
        return SanitizeDetail(body);
    }

    private static void AddValue(List<string> values, JsonObject root, string key)
    {
        var value = root[key]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(value)) values.Add(value.Trim());
    }

    private static bool ContainsAny(string value, params string[] candidates)
        => candidates.Any(value.Contains);
}
