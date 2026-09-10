using System.Text;
using System.Text.Json;

namespace PersonaWorkbench.Web;

internal enum ProviderChatEnvelopeKind
{
    Unknown,
    OllamaNative,
    OpenAiCompatible
}

internal enum ProviderChatResponseReadStatus
{
    Success,
    ResponseSizeInvalid,
    ResponseShapeInvalid,
    Truncated,
    Empty,
    JsonInvalid
}

internal sealed record ProviderChatResponseReadResult(
    ProviderChatResponseReadStatus Status,
    string Content,
    ProviderUsage? Usage,
    ProviderChatEnvelopeKind EnvelopeKind,
    string? CompletionReason)
{
    public bool IsSuccess => Status == ProviderChatResponseReadStatus.Success;
}

internal static class ProviderChatResponseReader
{
    public static ProviderChatResponseReadResult Read(string responseText, int maximumResponseBytes)
    {
        if (string.IsNullOrWhiteSpace(responseText) || Encoding.UTF8.GetByteCount(responseText) > maximumResponseBytes)
        {
            return Failure(ProviderChatResponseReadStatus.ResponseSizeInvalid);
        }

        try
        {
            using JsonDocument outer = JsonDocument.Parse(responseText, new JsonDocumentOptions { MaxDepth = 16 });
            ProviderUsage? usage = ProviderUsage.TryParse(outer.RootElement);
            string contentText;
            string? completionReason;
            ProviderChatEnvelopeKind envelopeKind;
            if (outer.RootElement.TryGetProperty("message", out JsonElement nativeMessage)
                && nativeMessage.ValueKind == JsonValueKind.Object
                && nativeMessage.TryGetProperty("content", out JsonElement nativeContent)
                && nativeContent.ValueKind == JsonValueKind.String)
            {
                contentText = nativeContent.GetString() ?? string.Empty;
                completionReason = nativeMessage.TryGetProperty("done_reason", out JsonElement nestedDoneReason)
                    && nestedDoneReason.ValueKind == JsonValueKind.String
                    ? nestedDoneReason.GetString()
                    : outer.RootElement.TryGetProperty("done_reason", out JsonElement doneReason)
                        && doneReason.ValueKind == JsonValueKind.String
                        ? doneReason.GetString()
                        : null;
                envelopeKind = ProviderChatEnvelopeKind.OllamaNative;
            }
            else if (outer.RootElement.TryGetProperty("choices", out JsonElement choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() == 1
                && choices[0].TryGetProperty("message", out JsonElement message)
                && message.TryGetProperty("content", out JsonElement content)
                && content.ValueKind == JsonValueKind.String)
            {
                contentText = content.GetString() ?? string.Empty;
                completionReason = choices[0].TryGetProperty("finish_reason", out JsonElement finishReason)
                    && finishReason.ValueKind == JsonValueKind.String
                    ? finishReason.GetString()
                    : null;
                envelopeKind = ProviderChatEnvelopeKind.OpenAiCompatible;
            }
            else
            {
                return new ProviderChatResponseReadResult(ProviderChatResponseReadStatus.ResponseShapeInvalid, string.Empty, usage, ProviderChatEnvelopeKind.Unknown, null);
            }

            if (string.Equals(completionReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                return new ProviderChatResponseReadResult(ProviderChatResponseReadStatus.Truncated, string.Empty, usage, envelopeKind, completionReason);
            }

            if (string.IsNullOrWhiteSpace(contentText))
            {
                return new ProviderChatResponseReadResult(ProviderChatResponseReadStatus.Empty, string.Empty, usage, envelopeKind, completionReason);
            }

            return new ProviderChatResponseReadResult(ProviderChatResponseReadStatus.Success, contentText, usage, envelopeKind, completionReason);
        }
        catch (JsonException)
        {
            return Failure(ProviderChatResponseReadStatus.JsonInvalid);
        }
    }

    private static ProviderChatResponseReadResult Failure(ProviderChatResponseReadStatus status)
    {
        return new ProviderChatResponseReadResult(status, string.Empty, null, ProviderChatEnvelopeKind.Unknown, null);
    }
}
