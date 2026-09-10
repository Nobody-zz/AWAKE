namespace Awake.WorldbookStudio.Core;

internal sealed record BatchFailureDetails(
    string Code,
    string Message,
    string StatusReason,
    string Outcome,
    string ErrorClass,
    int? RetryAfterSeconds)
{
    public static BatchFailureDetails FromCancellation(string stage, string? detail = null)
    {
        var message = string.IsNullOrWhiteSpace(detail) ? "AI 请求已取消。" : detail.Trim();
        return From(stage, "WB-AI-CANCELLED: " + message);
    }

    public static BatchFailureDetails From(string stage, string? error)
    {
        var raw = string.IsNullOrWhiteSpace(error) ? "AI 处理未完成。" : error.Trim();
        var (rawCode, rawMessage) = SplitCode(raw);
        var errorClass = Classify(rawCode, rawMessage);
        var outcome = errorClass switch
        {
            "cancelled" => "cancelled",
            "transport" or "timeout" => "unknown_result",
            "stale" => "stale_result",
            "format" or "rate_limit" or "upstream" => "transient_error",
            "unknown" => "transient_error",
            _ => "permanent_error"
        };
        var statusReason = outcome switch
        {
            "unknown_result" => stage == "metadata" ? "metadata_network_unknown" : "network_unknown",
            "stale_result" => "stale_result",
            "cancelled" => "cancelled",
            "transient_error" => stage == "metadata" ? "metadata_provider_transient_error" : "provider_transient_error",
            _ => stage == "metadata" ? "metadata_provider_permanent_error" : "provider_permanent_error"
        };
        var retryAfterSeconds = outcome == "transient_error" || errorClass == "rate_limit" ? (int?)0 : null;
        return new BatchFailureDetails(
            ToBatchCode(rawCode, stage),
            BuildMessage(errorClass, rawMessage),
            statusReason,
            outcome,
            errorClass,
            retryAfterSeconds);
    }

    private static (string Code, string Message) SplitCode(string raw)
    {
        var separator = raw.IndexOf(':');
        if (separator <= 0 || !raw[..separator].StartsWith("WB-", StringComparison.Ordinal))
            return (string.Empty, ProviderErrorMapping.SanitizeDetail(raw));
        return (raw[..separator], ProviderErrorMapping.SanitizeDetail(raw[(separator + 1)..]));
    }

    private static string ToBatchCode(string rawCode, string stage)
    {
        if (rawCode.StartsWith("WB-BATCH-", StringComparison.Ordinal)) return rawCode;
        if (rawCode.Equals("WB-AI-CANCELLED", StringComparison.Ordinal))
            return stage == "metadata" ? "WB-BATCH-METADATA-CANCELLED" : "WB-BATCH-PROVIDER-CANCELLED";
        if (rawCode.StartsWith("WB-AI-", StringComparison.Ordinal))
            return "WB-BATCH-PROVIDER-" + NormalizeCode(rawCode[6..]);
        if (!string.IsNullOrWhiteSpace(rawCode))
            return "WB-BATCH-PROVIDER-" + NormalizeCode(rawCode);
        return stage == "metadata" ? "WB-BATCH-METADATA-500" : "WB-BATCH-PROVIDER-500";
    }

    private static string NormalizeCode(string value)
    {
        var normalized = new string(value.ToUpperInvariant().Select(character => char.IsLetterOrDigit(character) || character == '-' ? character : '-').ToArray()).Trim('-');
        return normalized.Length <= 80 ? normalized : normalized[..80];
    }

    private static string Classify(string code, string message)
    {
        var codeValue = code.ToUpperInvariant();
        var messageValue = message.ToUpperInvariant();
        var value = codeValue + " " + messageValue;
        if (codeValue.Contains("CANCEL", StringComparison.Ordinal)
            || messageValue.Contains("CANCEL", StringComparison.Ordinal)) return "cancelled";
        if (codeValue.Contains("AUTH", StringComparison.Ordinal)
            || codeValue.Contains("SECRET", StringComparison.Ordinal)
            || codeValue.Contains("HANDSHAKE", StringComparison.Ordinal)
            || messageValue.Contains("凭据", StringComparison.Ordinal)) return "auth";
        if (codeValue.Contains("ENDPOINT", StringComparison.Ordinal)
            || codeValue.Contains("POLICY", StringComparison.Ordinal)
            || codeValue.Contains("CSRF", StringComparison.Ordinal)) return "policy";
        if (codeValue.Contains("BIND", StringComparison.Ordinal)
            || codeValue.Contains("STALE", StringComparison.Ordinal)
            || codeValue.Contains("CLAIM", StringComparison.Ordinal)
            || codeValue.Contains("REVISION", StringComparison.Ordinal)) return "stale";
        if (codeValue.Contains("MODEL", StringComparison.Ordinal)
            || codeValue.Contains("PARAMETER", StringComparison.Ordinal)
            || codeValue.Contains("REQUEST-413", StringComparison.Ordinal)
            || codeValue.Contains("HTTP-400", StringComparison.Ordinal)
            || codeValue.Contains("HTTP-404", StringComparison.Ordinal)
            || codeValue.Contains("HTTP-422", StringComparison.Ordinal)) return "parameter";
        if (codeValue.Contains("FORMAT", StringComparison.Ordinal)
            || codeValue.Contains("OUTPUT", StringComparison.Ordinal)
            || codeValue.Contains("RESPONSE", StringComparison.Ordinal)) return "format";
        if (codeValue.Contains("RATE", StringComparison.Ordinal)
            || codeValue.Contains("429", StringComparison.Ordinal)) return "rate_limit";
        if (codeValue.Contains("TIMEOUT", StringComparison.Ordinal)
            || codeValue.Contains("408", StringComparison.Ordinal)) return "timeout";
        if (codeValue.Contains("TRANSPORT", StringComparison.Ordinal)
            || codeValue.Contains("502", StringComparison.Ordinal)) return "transport";
        if (codeValue.Contains("UPSTREAM", StringComparison.Ordinal)
            || codeValue.Contains("5XX", StringComparison.Ordinal)) return "upstream";
        if (value.Contains("UNSUPPORTED PARAMETER", StringComparison.Ordinal)
            || value.Contains("UNKNOWN PARAMETER", StringComparison.Ordinal)) return "parameter";
        return "unknown";
    }

    private static string BuildMessage(string errorClass, string rawMessage)
    {
        var prefix = errorClass switch
        {
            "auth" => "认证失败：",
            "parameter" => "模型或请求参数不被 Provider 支持：",
            "policy" => "请求被本机安全策略阻止：",
            "stale" => "返回结果与当前资料不匹配：",
            "format" => "Provider 返回内容格式不正确：",
            "rate_limit" => "Provider 当前限流：",
            "timeout" => "请求超时，结果暂时无法确认：",
            "transport" => "网络连接失败，结果暂时无法确认：",
            "upstream" => "Provider 服务暂时异常：",
            "cancelled" => "操作已取消：",
            _ => "AI 处理失败："
        };
        var detail = string.IsNullOrWhiteSpace(rawMessage) ? "请查看 Provider 配置后重试。" : rawMessage;
        var message = prefix + detail;
        return message.Length <= 1000 ? message : message[..1000];
    }
}
