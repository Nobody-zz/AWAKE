using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MarcusAwakeProvider;

internal sealed class CompletionParts
{
    internal CompletionParts(string modelId, string content, ProviderUsage? usage)
    {
        ModelId = modelId;
        Content = content;
        Usage = usage;
    }

    internal string ModelId { get; }
    internal string Content { get; }
    internal ProviderUsage? Usage { get; }
}

internal sealed class StreamFrameParts
{
    internal StreamFrameParts(string? text, ProviderUsage? usage, bool isTerminal)
    {
        Text = text;
        Usage = usage;
        IsTerminal = isTerminal;
    }

    internal string? Text { get; }
    internal ProviderUsage? Usage { get; }
    internal bool IsTerminal { get; }
}

internal static class ProviderResponseSupport
{
    internal static ProviderResult<IReadOnlyList<ProviderModel>> ParseModels(
        JsonDocument document,
        string propertyName,
        string providerId,
        int maximumModels,
        bool deduplicate = true)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
        {
            return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(Malformed(providerId, "response.models_invalid", "Provider model list is malformed."));
        }

        var models = new List<ProviderModel>();
        var modelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var modelElement in modelsElement.EnumerateArray())
        {
            if (modelElement.ValueKind != JsonValueKind.Object)
            {
                return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(Malformed(providerId, "response.models_invalid", "Provider model list is malformed."));
            }

            var id = JsonPayload.ReadString(modelElement, "id") ?? JsonPayload.ReadString(modelElement, "name");
            if (string.IsNullOrWhiteSpace(id))
            {
                return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(Malformed(providerId, "response.model_id_missing", "Provider returned a model without an identifier."));
            }

            try
            {
                ProviderContract.RequireModel(id, nameof(id));
            }
            catch (ArgumentException)
            {
                return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(Malformed(providerId, "response.model_id_invalid", "Provider returned an invalid model identifier."));
            }

            if (models.Count >= maximumModels)
            {
                return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(new ProviderError("response.models_too_many", ProviderErrorCategory.ResourceExhausted, "Provider returned too many models.", false, providerId));
            }

            if (deduplicate && !modelIds.Add(id)) continue;
            var displayName = JsonPayload.ReadString(modelElement, "display_name") ?? JsonPayload.ReadString(modelElement, "name") ?? string.Empty;
            models.Add(new ProviderModel(id, displayName));
        }

        if (deduplicate) models.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        return ProviderResult<IReadOnlyList<ProviderModel>>.Succeeded(models);
    }

    internal static ProviderResult<CompletionParts> ParseOpenAiCompletion(JsonDocument document, string providerId, string requestedModel)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return ProviderResult<CompletionParts>.Failed(Malformed(providerId, "response.completion_invalid", "Provider completion response is malformed.", requestedModel));
        }

        var firstChoice = choices[0];
        if (firstChoice.ValueKind != JsonValueKind.Object)
        {
            return ProviderResult<CompletionParts>.Failed(Malformed(providerId, "response.completion_invalid", "Provider completion response is malformed.", requestedModel));
        }

        if (!firstChoice.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return ProviderResult<CompletionParts>.Failed(Malformed(providerId, "response.message_missing", "Provider completion message is missing.", requestedModel));
        }

        var content = JsonPayload.ReadString(message, "content");
        if (content == null)
        {
            return ProviderResult<CompletionParts>.Failed(Malformed(providerId, "response.content_missing", "Provider completion content is missing.", requestedModel));
        }

        var modelResult = ResolveResponseModel(root, requestedModel, providerId);
        if (!modelResult.IsSuccess) return ProviderResult<CompletionParts>.Failed(modelResult.Error!);
        return ProviderResult<CompletionParts>.Succeeded(new CompletionParts(modelResult.Value!, content, ReadNestedUsage(root, "prompt_tokens", "completion_tokens")));
    }

    internal static ProviderResult<CompletionParts> ParseAnthropicCompletion(JsonDocument document, string providerId, string requestedModel)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("content", out var contentElement))
        {
            return ProviderResult<CompletionParts>.Failed(Malformed(providerId, "response.content_missing", "Provider completion content is missing.", requestedModel));
        }

        string? content;
        if (contentElement.ValueKind == JsonValueKind.String)
        {
            content = contentElement.GetString();
        }
        else if (contentElement.ValueKind == JsonValueKind.Array)
        {
            var text = new StringBuilder();
            foreach (var block in contentElement.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object) continue;
                if (!StringComparer.Ordinal.Equals(JsonPayload.ReadString(block, "type"), "text")) continue;
                var blockText = JsonPayload.ReadString(block, "text");
                if (blockText != null) text.Append(blockText);
            }

            content = text.Length == 0 ? null : text.ToString();
        }
        else
        {
            content = null;
        }

        if (content == null)
        {
            return ProviderResult<CompletionParts>.Failed(Malformed(providerId, "response.content_missing", "Provider completion content is missing.", requestedModel));
        }

        var modelResult = ResolveResponseModel(root, requestedModel, providerId);
        if (!modelResult.IsSuccess) return ProviderResult<CompletionParts>.Failed(modelResult.Error!);
        return ProviderResult<CompletionParts>.Succeeded(new CompletionParts(modelResult.Value!, content, ReadNestedUsage(root, "input_tokens", "output_tokens")));
    }

    internal static ProviderResult<CompletionParts> ParseOllamaCompletion(JsonDocument document, string providerId, string requestedModel)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return ProviderResult<CompletionParts>.Failed(Malformed(providerId, "response.message_missing", "Provider completion message is missing.", requestedModel));
        }

        var content = JsonPayload.ReadString(message, "content");
        if (content == null)
        {
            return ProviderResult<CompletionParts>.Failed(Malformed(providerId, "response.content_missing", "Provider completion content is missing.", requestedModel));
        }

        var modelResult = ResolveResponseModel(root, requestedModel, providerId);
        if (!modelResult.IsSuccess) return ProviderResult<CompletionParts>.Failed(modelResult.Error!);
        return ProviderResult<CompletionParts>.Succeeded(new CompletionParts(modelResult.Value!, content, ReadUsage(root, "prompt_eval_count", "eval_count")));
    }

    internal static ProviderResult<StreamFrameParts> ParseOpenAiStreamFrame(JsonDocument document, string providerId, string model)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ProviderResult<StreamFrameParts>.Failed(Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model));
        }

        if (root.TryGetProperty("error", out var errorElement))
        {
            return ProviderResult<StreamFrameParts>.Failed(StreamProviderError(providerId, model, errorElement));
        }

        string? text = null;
        if (root.TryGetProperty("choices", out var choices))
        {
            if (choices.ValueKind != JsonValueKind.Array)
            {
                return ProviderResult<StreamFrameParts>.Failed(Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model));
            }

            if (choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.ValueKind != JsonValueKind.Object)
                {
                    return ProviderResult<StreamFrameParts>.Failed(Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model));
                }

                if (firstChoice.TryGetProperty("delta", out var delta))
                {
                    if (delta.ValueKind != JsonValueKind.Object)
                    {
                        return ProviderResult<StreamFrameParts>.Failed(Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model));
                    }

                    text = JsonPayload.ReadString(delta, "content");
                }
                else if (firstChoice.TryGetProperty("message", out var message))
                {
                    if (message.ValueKind != JsonValueKind.Object)
                    {
                        return ProviderResult<StreamFrameParts>.Failed(Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model));
                    }

                    text = JsonPayload.ReadString(message, "content");
                }
            }
        }

        return ProviderResult<StreamFrameParts>.Succeeded(new StreamFrameParts(text, ReadNestedUsage(root, "prompt_tokens", "completion_tokens"), false));
    }

    internal static ProviderResult<StreamFrameParts> ParseAnthropicStreamFrame(JsonDocument document, string eventName, string providerId, string model)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ProviderResult<StreamFrameParts>.Failed(Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model));
        }

        if (root.TryGetProperty("error", out var errorElement))
        {
            return ProviderResult<StreamFrameParts>.Failed(StreamProviderError(providerId, model, errorElement));
        }

        var type = JsonPayload.ReadString(root, "type") ?? eventName;
        if (StringComparer.Ordinal.Equals(type, "message_stop"))
        {
            return ProviderResult<StreamFrameParts>.Succeeded(new StreamFrameParts(null, null, true));
        }

        string? text = null;
        if (StringComparer.Ordinal.Equals(type, "content_block_delta") && root.TryGetProperty("delta", out var delta))
        {
            if (delta.ValueKind != JsonValueKind.Object)
            {
                return ProviderResult<StreamFrameParts>.Failed(Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model));
            }

            text = JsonPayload.ReadString(delta, "text");
        }

        ProviderUsage? usage = null;
        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object && message.TryGetProperty("usage", out var messageUsage) && messageUsage.ValueKind == JsonValueKind.Object)
        {
            usage = ReadUsage(messageUsage, "input_tokens", "output_tokens");
        }

        if (root.TryGetProperty("usage", out var rootUsage) && rootUsage.ValueKind == JsonValueKind.Object)
        {
            usage = MergeUsage(usage, ReadUsage(rootUsage, "input_tokens", "output_tokens"));
        }

        return ProviderResult<StreamFrameParts>.Succeeded(new StreamFrameParts(text, usage, false));
    }

    internal static ProviderResult<StreamFrameParts> ParseOllamaStreamLine(JsonDocument document, string providerId, string model)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ProviderResult<StreamFrameParts>.Failed(Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model));
        }

        if (root.TryGetProperty("error", out var errorElement))
        {
            return ProviderResult<StreamFrameParts>.Failed(StreamProviderError(providerId, model, errorElement));
        }

        string? text = null;
        if (root.TryGetProperty("message", out var message))
        {
            if (message.ValueKind != JsonValueKind.Object)
            {
                return ProviderResult<StreamFrameParts>.Failed(Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model));
            }

            text = JsonPayload.ReadString(message, "content");
        }

        var done = root.TryGetProperty("done", out var doneElement) && doneElement.ValueKind == JsonValueKind.True;
        var usage = new ProviderUsage(
            JsonPayload.ReadNonNegativeInt(root, "prompt_eval_count"),
            JsonPayload.ReadNonNegativeInt(root, "eval_count"));
        if (!usage.InputTokens.HasValue && !usage.OutputTokens.HasValue) usage = null;
        return ProviderResult<StreamFrameParts>.Succeeded(new StreamFrameParts(text, usage, done));
    }

    internal static ProviderError Malformed(string providerId, string code, string message, string? model = null)
    {
        return new ProviderError(code, ProviderErrorCategory.MalformedResponse, message, false, providerId, model);
    }

    internal static ProviderError Incomplete(string providerId, string model)
    {
        return new ProviderError("response.stream_incomplete", ProviderErrorCategory.IncompleteStream, "Provider stream ended before its terminal marker.", false, providerId, model);
    }

    internal static ProviderError TooLarge(string providerId, string model, string code)
    {
        return new ProviderError("response." + code, ProviderErrorCategory.ResourceExhausted, "Provider stream exceeds the configured limit.", false, providerId, model);
    }

    internal static ProviderError InvalidUtf8(string providerId, string model)
    {
        return new ProviderError("response.utf8_invalid", ProviderErrorCategory.MalformedResponse, "Provider returned invalid UTF-8.", false, providerId, model);
    }

    internal static ProviderError Transport(string providerId, string model)
    {
        return new ProviderError("transport.unavailable", ProviderErrorCategory.TransportUnavailable, "Provider transport is unavailable.", true, providerId, model);
    }

    private static ProviderResult<string> ResolveResponseModel(JsonElement root, string requestedModel, string providerId)
    {
        var responseModel = JsonPayload.ReadString(root, "model");
        var model = string.IsNullOrWhiteSpace(responseModel) ? requestedModel : responseModel;
        try
        {
            return ProviderResult<string>.Succeeded(ProviderContract.RequireModel(model, nameof(model)));
        }
        catch (ArgumentException)
        {
            return ProviderResult<string>.Failed(Malformed(providerId, "response.model_invalid", "Provider returned an invalid model identifier.", requestedModel));
        }
    }

    private static ProviderUsage? ReadNestedUsage(JsonElement parent, string inputName, string outputName)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;
        return ReadUsage(usage, inputName, outputName);
    }

    private static ProviderUsage? ReadUsage(JsonElement usage, string inputName, string outputName)
    {
        var inputTokens = JsonPayload.ReadNonNegativeInt(usage, inputName);
        var outputTokens = JsonPayload.ReadNonNegativeInt(usage, outputName);
        return inputTokens.HasValue || outputTokens.HasValue ? new ProviderUsage(inputTokens, outputTokens) : null;
    }

    private static ProviderUsage? MergeUsage(ProviderUsage? first, ProviderUsage? second)
    {
        if (first == null) return second;
        if (second == null) return first;
        return new ProviderUsage(second.InputTokens ?? first.InputTokens, second.OutputTokens ?? first.OutputTokens);
    }

    private static ProviderError StreamProviderError(string providerId, string model, JsonElement errorElement)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal);
        if (errorElement.ValueKind == JsonValueKind.Object)
        {
            var type = JsonPayload.ReadString(errorElement, "type");
            if (!string.IsNullOrWhiteSpace(type) && type.Length <= 160) details["type"] = type;
            var code = JsonPayload.ReadString(errorElement, "code");
            if (!string.IsNullOrWhiteSpace(code) && code.Length <= 160) details["code"] = code;
        }

        return new ProviderError("stream.provider_error", ProviderErrorCategory.Unavailable, "Provider returned a stream error.", true, providerId, model, details: details);
    }
}