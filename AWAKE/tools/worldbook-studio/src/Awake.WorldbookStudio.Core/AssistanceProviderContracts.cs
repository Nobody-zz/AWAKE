using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public sealed record ProviderConfiguration(
    string? CloudBaseUrl,
    string? CloudModel,
    string? CloudApiKeyEnvironmentVariable,
    int CloudTimeoutSeconds,
    int CloudMaxTokens,
    string? LocalWorkerUrl,
    string? LocalWorkerSecretEnvironmentVariable)
{
    public string? CloudApiKey { get; init; }
    public bool HasLocalCloudSettings { get; init; }
    public string? LocalWorkerSecret { get; init; }
    public bool HasLocalWorkerSettings { get; init; }
    public double CloudTemperature { get; init; } = 0;
    public string CloudReasoningEffort { get; init; } = "low";
    public int? WorkerTimeoutSeconds { get; init; }
    internal string? CloudApiKeyEnvironmentValue { get; init; }
    internal string? LocalWorkerSecretEnvironmentValue { get; init; }
    internal bool UsesEnvironmentSnapshot { get; init; }

    public static ProviderConfiguration FromEnvironment(IReadOnlyDictionary<string, string?>? environment = null)
    {
        string? Get(string name) => environment is null ? Environment.GetEnvironmentVariable(name) : environment.GetValueOrDefault(name);
        var cloudApiKeyEnvironmentVariable = Get("WORLD_BOOK_CLOUD_API_KEY_ENV");
        var localWorkerSecretEnvironmentVariable = Get("WORLD_BOOK_LOCAL_WORKER_SECRET_ENV");
        string? GetNamed(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (environment is not null && environment.TryGetValue(name, out var value)) return value;
            return Environment.GetEnvironmentVariable(name);
        }

        var cloudTimeoutSeconds = ParseBoundedInt(Get("WORLD_BOOK_CLOUD_TIMEOUT_SECONDS"), 60, 5, 600);
        var configuration = new ProviderConfiguration(
            Get("WORLD_BOOK_CLOUD_BASE_URL"),
            Get("WORLD_BOOK_CLOUD_MODEL"),
            cloudApiKeyEnvironmentVariable,
            cloudTimeoutSeconds,
            // 默认值与参考资料输入上限匹配，避免长资料被静默截断。
            ParseBoundedInt(Get("WORLD_BOOK_CLOUD_MAX_TOKENS"), 8000, 128, 8000),
            Get("WORLD_BOOK_LOCAL_WORKER_URL"),
            localWorkerSecretEnvironmentVariable)
        {
            WorkerTimeoutSeconds = ParseBoundedInt(Get("WORLD_BOOK_WORKER_TIMEOUT_SECONDS"), cloudTimeoutSeconds, 5, 600),
            CloudReasoningEffort = ParseReasoningEffort(Get("WORLD_BOOK_CLOUD_REASONING_EFFORT")),
            CloudApiKeyEnvironmentValue = environment is null ? null : GetNamed(cloudApiKeyEnvironmentVariable),
            LocalWorkerSecretEnvironmentValue = environment is null ? null : GetNamed(localWorkerSecretEnvironmentVariable),
            UsesEnvironmentSnapshot = environment is not null
        };

        var settingsPath = Get("AWAKE_WB_PROVIDER_SETTINGS_PATH");
        var canReadSettingsFile = environment is null || !string.IsNullOrWhiteSpace(settingsPath);
        if (canReadSettingsFile && ProviderSettingsStore.TryLoad(out var stored, settingsPath))
        {
            configuration = configuration with
            {
                CloudBaseUrl = stored.BaseUrl,
                CloudModel = stored.Model,
                CloudApiKey = stored.ApiKey,
                HasLocalCloudSettings = true
            };
        }

        // X1：本机 Worker 的地址与凭据同样支持本机加密保存（界面可填），并优先于环境变量。
        if (canReadSettingsFile && ProviderSettingsStore.TryLoadLocalWorker(out var workerUrl, out var workerSecret, settingsPath))
        {
            configuration = configuration with
            {
                LocalWorkerUrl = workerUrl,
                LocalWorkerSecret = workerSecret,
                HasLocalWorkerSettings = true
            };
        }

        return configuration;
    }

    public ProviderConfiguration WithModelParameters(JsonObject? modelParameters)
    {
        if (modelParameters is null) return this;
        var result = this;
        if (modelParameters["model"] is JsonValue modelValue && modelValue.TryGetValue<string>(out var model) && !string.IsNullOrWhiteSpace(model))
            result = result with { CloudModel = model.Trim() };
        if (modelParameters["max_output_tokens"] is JsonValue tokenValue && tokenValue.TryGetValue<int>(out var maxTokens))
            result = result with { CloudMaxTokens = Math.Clamp(maxTokens, 128, 8000) };
        if (modelParameters["temperature"] is JsonValue temperatureValue && temperatureValue.TryGetValue<double>(out var temperature))
            result = result with { CloudTemperature = Math.Clamp(temperature, 0, 2) };
        if (modelParameters["reasoning_effort"] is JsonValue reasoningValue && reasoningValue.TryGetValue<string>(out var reasoning))
            result = result with { CloudReasoningEffort = ParseReasoningEffort(reasoning) };
        return result;
    }

    public ProviderStatus GetStatus(string providerId)
    {
        if (providerId.Equals("cloud", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(CloudBaseUrl) || string.IsNullOrWhiteSpace(CloudModel) || !HasCredentialSource())
                return new ProviderStatus("cloud", "missing", "云端配置不完整");
            if (!Uri.TryCreate(CloudBaseUrl, UriKind.Absolute, out var uri))
                return new ProviderStatus("cloud", "invalid", "云端地址无效");
            try
            {
                ProviderEndpointPolicy.ValidateCloudBaseUri(uri);
                return new ProviderStatus("cloud", "configured", "云端 Provider 已配置");
            }
            catch (InvalidOperationException)
            {
                return new ProviderStatus("cloud", "blocked", "云端地址被安全策略阻止");
            }
        }

        if (providerId.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(LocalWorkerUrl) || string.IsNullOrWhiteSpace(ResolveLocalWorkerSecret()))
                return new ProviderStatus("local", "missing", "本机 AI Worker 未配置：请在「AI 来源设置」里填写本机 Worker 地址和凭据。");
            if (!Uri.TryCreate(LocalWorkerUrl, UriKind.Absolute, out var uri))
                return new ProviderStatus("local", "invalid", "本机 Worker 地址无效");
            try
            {
                ProviderEndpointPolicy.ValidateLocalWorkerUri(uri);
                return new ProviderStatus("local", "configured", "本机 Worker 已配置");
            }
            catch (InvalidOperationException)
            {
                return new ProviderStatus("local", "blocked", "本机 Worker 地址被安全策略阻止");
            }
        }

        return new ProviderStatus(providerId, "missing", "未知 Provider");
    }

    public string? ResolveCloudApiKey()
        => HasLocalCloudSettings
            ? CloudApiKey
            : UsesEnvironmentSnapshot
                ? CloudApiKeyEnvironmentValue
                : string.IsNullOrWhiteSpace(CloudApiKeyEnvironmentVariable) ? null : Environment.GetEnvironmentVariable(CloudApiKeyEnvironmentVariable);

    public string? ResolveLocalWorkerSecret()
        => HasLocalWorkerSettings
            ? LocalWorkerSecret
            : UsesEnvironmentSnapshot
                ? LocalWorkerSecretEnvironmentValue
                : string.IsNullOrWhiteSpace(LocalWorkerSecretEnvironmentVariable) ? null : Environment.GetEnvironmentVariable(LocalWorkerSecretEnvironmentVariable);

    private static int ParseBoundedInt(string? value, int fallback, int minimum, int maximum)
        => int.TryParse(value, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;

    private static string ParseReasoningEffort(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "none" or "low" or "medium" or "high" => value.Trim().ToLowerInvariant(),
            _ => "low"
        };

    private bool HasCredentialSource()
        => HasLocalCloudSettings
            ? !string.IsNullOrWhiteSpace(CloudApiKey)
            : !string.IsNullOrWhiteSpace(ResolveCloudApiKey());
}

public sealed record ProviderStatus(string ProviderId, string State, string Label);

public static class ProviderEndpointPolicy
{
    public static void ValidateCloudBaseUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("WB-AI-ENDPOINT-403: 云端 Provider 必须使用无认证信息的 HTTPS 地址。");
        if (IsForbiddenHost(uri.Host))
            throw new InvalidOperationException("WB-AI-ENDPOINT-403: 云端 Provider 目标属于禁止的本机或私有地址。");
    }

    public static void ValidateLocalWorkerUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AI-ENDPOINT-403: 本机 Worker 必须使用 HTTP/HTTPS 地址。");
        if (!IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address))
            throw new InvalidOperationException("WB-AI-ENDPOINT-403: 本机 Worker 只能绑定 loopback 地址。");
        if (uri.Port is < 1024 or > 65535)
            throw new InvalidOperationException("WB-AI-ENDPOINT-403: 本机 Worker 端口不在允许范围内。");
    }

    public static void ValidateResolvedAddress(IPAddress address)
    {
        if (IsForbiddenAddress(address))
            throw new InvalidOperationException("WB-AI-ENDPOINT-403: 解析结果属于禁止的本机或私有地址。");
    }

    private static bool IsForbiddenHost(string host)
    {
        if (IPAddress.TryParse(host, out var address)) return IsForbiddenAddress(address);
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase)
            || host.Equals("instance-data", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 0 || bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 169 && bytes[1] == 254
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168;
        }
        return bytes.Length == 16 && (bytes[0] & 0xFE) == 0xFC;
    }
}

public sealed record AssistanceResult(
    string SchemaVersion,
    string RequestHash,
    string SourceDocumentHash,
    IReadOnlyList<AssistanceSuggestion> Suggestions);

public sealed record AssistanceSuggestion(
    string Id,
    string Kind,
    string Severity,
    double Confidence,
    string Title,
    string Reason,
    string? CandidateText,
    KnowledgePatch? Patch,
    bool ReviewOnly);

public static class AssistanceResultParser
{
    public static AssistanceResult Parse(JsonNode node)
    {
        var root = RequireObject(node);
        EnsureKeys(root, "schema_version", "request_hash", "source_document_hash", "suggestions");
        var schema = RequiredString(root, "schema_version");
        if (schema != "assistance.result.v1") throw Format("结果 schema_version 无效。");
        var requestHash = RequiredHash(root, "request_hash");
        var sourceHash = RequiredHash(root, "source_document_hash");
        var suggestions = new List<AssistanceSuggestion>();
        var array = root["suggestions"] as JsonArray ?? throw Format("suggestions 必须是数组。");
        if (array.Count > 64) throw Format("suggestions 数量超限。");
        foreach (var item in array)
        {
            var suggestion = RequireObject(item);
            EnsureKeys(suggestion, "id", "kind", "severity", "confidence", "title", "reason", "candidate_text", "patch", "review_only");
            var confidence = suggestion["confidence"]?.GetValue<double>() ?? throw Format("confidence 缺失。");
            if (confidence is < 0 or > 1) throw Format("confidence 超出范围。");
            if (suggestion["review_only"] is not JsonValue reviewOnlyValue
                || !reviewOnlyValue.TryGetValue<bool>(out var reviewOnly)
                || !reviewOnly)
                throw Format("review_only 必须为 true。");
            var patch = suggestion["patch"] is null ? null : ParsePatch(suggestion["patch"]!);
            suggestions.Add(new AssistanceSuggestion(
                BoundedString(suggestion, "id", 128),
                BoundedString(suggestion, "kind", 64),
                BoundedString(suggestion, "severity", 32),
                confidence,
                BoundedString(suggestion, "title", 512),
                BoundedString(suggestion, "reason", 4000),
                OptionalString(suggestion, "candidate_text", 12000),
                patch,
                reviewOnly));
        }
        return new AssistanceResult(schema, requestHash, sourceHash, suggestions);
    }

    private static KnowledgePatch ParsePatch(JsonNode node)
    {
        var patch = RequireObject(node);
        EnsureKeys(patch, "schema_version", "operations");
        if (RequiredString(patch, "schema_version") != "knowledge-patch.v1") throw Format("patch schema_version 无效。");
        var operations = patch["operations"] as JsonArray ?? throw Format("patch operations 必须是数组。");
        if (operations.Count > 64) throw Format("patch operations 数量超限。");
        var list = new List<KnowledgePatchOperation>();
        foreach (var item in operations)
        {
            var operation = RequireObject(item);
            EnsureKeys(operation, "op", "path", "value");
            list.Add(new KnowledgePatchOperation(BoundedString(operation, "op", 16), BoundedString(operation, "path", 512), operation["value"] is null ? null : JsonNode.Parse(operation["value"]!.ToJsonString())));
        }
        return new KnowledgePatch("knowledge-patch.v1", list);
    }

    private static JsonObject RequireObject(JsonNode? node) => node as JsonObject ?? throw Format("对象结构无效。");
    private static string RequiredString(JsonObject root, string key) => root[key]?.GetValue<string>() ?? throw Format($"字段 {key} 缺失。");
    private static string? OptionalString(JsonObject root, string key, int maximum)
    {
        if (root[key] is null) return null;
        var value = root[key]!.GetValue<string>();
        if (value.Length > maximum) throw Format($"字段 {key} 超长。");
        return value;
    }
    private static string BoundedString(JsonObject root, string key, int maximum)
    {
        var value = RequiredString(root, key);
        if (value.Length == 0 || value.Length > maximum) throw Format($"字段 {key} 长度无效。");
        return value;
    }
    private static string RequiredHash(JsonObject root, string key)
    {
        var value = BoundedString(root, key, 64);
        if (value.Length != 64 || value.Any(x => !Uri.IsHexDigit(x))) throw Format($"字段 {key} 不是 SHA-256。");
        return value;
    }
    private static void EnsureKeys(JsonObject root, params string[] allowed)
    {
        var set = allowed.ToHashSet(StringComparer.Ordinal);
        if (root.Any(x => !set.Contains(x.Key))) throw Format("结果包含未声明字段。");
    }
    private static InvalidOperationException Format(string message) => new($"WB-AI-FORMAT-JSON: {message}");
}

public static class OpenAICompatibleResponseParser
{
    public static JsonNode ExtractJson(JsonNode response)
    {
        var root = response as JsonObject ?? throw new InvalidOperationException("WB-AI-FORMAT-JSON: Provider 响应不是对象。");
        if (root["choices"] is not JsonArray choices || choices.Count == 0 || choices[0] is not JsonObject choice || choice["message"] is not JsonObject message)
            throw new InvalidOperationException("WB-AI-FORMAT-JSON: Provider choices/message 结构无效。");
        var finishReason = OptionalString(choice["finish_reason"]);
        var content = message["content"];
        string text;
        if (content is JsonValue value && value.TryGetValue<string>(out var valueText)) text = valueText;
        else if (content is JsonArray parts) text = ExtractTextParts(parts);
        else if (content is JsonObject contentObject && contentObject["text"] is JsonValue textValue && textValue.TryGetValue<string>(out var objectText)) text = objectText;
        else
        {
            if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"WB-AI-OUTPUT-LENGTH-422: Provider 把输出额度耗尽在推理阶段，未返回可用草稿。（{DescribeEnvelope(choice, message, root)}）请降低推理强度或提高最大输出长度。");
            if (message["refusal"] is JsonValue refusalValue && refusalValue.TryGetValue<string>(out var refusal) && !string.IsNullOrWhiteSpace(refusal))
                throw new InvalidOperationException($"WB-AI-REFUSAL-422: Provider 拒绝生成结构化草稿。（{DescribeEnvelope(choice, message, root)}）");
            throw new InvalidOperationException($"WB-AI-FORMAT-JSON: Provider content 结构无效。（{DescribeEnvelope(choice, message, root)}）");
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"WB-AI-OUTPUT-LENGTH-422: Provider 把输出额度耗尽在推理阶段，未返回可用草稿。（{DescribeEnvelope(choice, message, root)}）请降低推理强度或提高最大输出长度。");
            throw new InvalidOperationException($"WB-AI-FORMAT-EMPTY: Provider 没有返回文本内容。（{DescribeEnvelope(choice, message, root)}）");
        }
        return ParseJsonText(text, DescribeEnvelope(choice, message, root));
    }

    private static string ExtractTextParts(JsonArray? parts)
    {
        if (parts is null) throw new InvalidOperationException("WB-AI-FORMAT-JSON: Provider content 结构无效。");
        var texts = new List<string>();
        foreach (var part in parts)
        {
            var item = part as JsonObject;
            if (item?["text"] is not JsonValue textValue || !textValue.TryGetValue<string>(out var text))
                throw new InvalidOperationException("WB-AI-FORMAT-JSON: Provider content 部分缺少文本。");
            texts.Add(text);
        }
        return string.Concat(texts);
    }

    private static string StripOneMarkdownFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal) || !trimmed.EndsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstLine = trimmed.IndexOf('\n');
        if (firstLine < 0) return trimmed;
        return trimmed[(firstLine + 1)..^3].Trim();
    }

    private static JsonNode ParseJsonText(string text, string envelope)
    {
        var normalized = StripOneMarkdownFence(text);
        if (TryParseJson(normalized, out var direct)) return direct;
        foreach (var candidate in EnumerateJsonCandidates(normalized))
            if (TryParseJson(candidate, out var extracted)) return extracted;
        throw new InvalidOperationException($"WB-AI-FORMAT-JSON: Provider 返回内容不是合法 JSON。（{envelope}）");
    }

    private static bool TryParseJson(string text, out JsonNode node)
    {
        try
        {
            node = JsonNode.Parse(text) ?? throw new JsonException();
            return true;
        }
        catch (JsonException)
        {
            node = null!;
            return false;
        }
    }

    private static IEnumerable<string> EnumerateJsonCandidates(string text)
    {
        for (var start = 0; start < text.Length; start++)
        {
            if (text[start] is not ('{' or '[')) continue;
            var stack = new Stack<char>();
            var inString = false;
            var escaped = false;
            for (var index = start; index < text.Length; index++)
            {
                var character = text[index];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') inString = false;
                    continue;
                }
                if (character == '"')
                {
                    inString = true;
                    continue;
                }
                if (character is '{' or '[')
                {
                    stack.Push(character);
                    continue;
                }
                if (character is not ('}' or ']')) continue;
                if (stack.Count == 0 || !IsMatchingPair(stack.Peek(), character)) break;
                stack.Pop();
                if (stack.Count == 0)
                {
                    yield return text[start..(index + 1)];
                    break;
                }
            }
        }
    }

    private static bool IsMatchingPair(char opening, char closing)
        => opening == '{' && closing == '}' || opening == '[' && closing == ']';

    private static string? OptionalString(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string DescribeEnvelope(JsonObject choice, JsonObject message, JsonObject root)
    {
        var content = message["content"];
        var contentType = content switch
        {
            null => "null",
            JsonValue stringValue when stringValue.TryGetValue<string>(out _) => "string",
            JsonValue => "value",
            JsonArray array => $"array[{array.Count}]",
            JsonObject => "object",
            _ => "unknown"
        };
        var contentLength = content is JsonValue contentValueForLength && contentValueForLength.TryGetValue<string>(out var text) ? text.Length : 0;
        var reasoningPresent = message["reasoning_content"] is not null || message["reasoning"] is not null;
        var refusalPresent = message["refusal"] is not null;
        var usage = root["usage"] as JsonObject;
        var usageSummary = usage is null
            ? "unknown"
            : string.Join(",", new[] { "prompt_tokens", "completion_tokens", "total_tokens" }
                .Where(key => usage[key] is not null)
                .Select(key => $"{key}={DescribeScalar(usage[key])}"));
        return $"finish_reason={OptionalString(choice["finish_reason"]) ?? "unknown"}, content_type={contentType}, content_length={contentLength}, reasoning_present={reasoningPresent.ToString().ToLowerInvariant()}, refusal_present={refusalPresent.ToString().ToLowerInvariant()}, usage={usageSummary}";
    }

    private static string DescribeScalar(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<int>(out var integer) ? integer.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : node is JsonValue decimalValue && decimalValue.TryGetValue<double>(out var number) ? number.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "unknown";

}
