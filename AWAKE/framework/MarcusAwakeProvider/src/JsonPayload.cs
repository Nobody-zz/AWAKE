using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MarcusAwakeProvider;

internal static class JsonPayload
{
    internal static byte[] BuildOpenAiChat(ProviderChatRequest request, string model, bool stream)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            WriteMessages(writer, request);
            writer.WriteBoolean("stream", stream);
            if (request.MaxOutputTokens.HasValue) writer.WriteNumber("max_tokens", request.MaxOutputTokens.Value);
            if (request.Temperature.HasValue) writer.WriteNumber("temperature", request.Temperature.Value);
            if (request.ResponseSchemaJson != null)
            {
                writer.WritePropertyName("response_format");
                writer.WriteRawValue(request.ResponseSchemaJson, false);
            }

            writer.WriteEndObject();
        }

        return output.ToArray();
    }

    internal static byte[] BuildAnthropicChat(ProviderChatRequest request, string model, bool stream)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            writer.WriteNumber("max_tokens", request.MaxOutputTokens ?? 1024);
            var system = string.Join("\n", request.Messages.WhereSystem().SelectContent());
            if (system.Length > 0) writer.WriteString("system", system);
            WriteNonSystemMessages(writer, request);
            writer.WriteBoolean("stream", stream);
            if (request.Temperature.HasValue) writer.WriteNumber("temperature", request.Temperature.Value);
            if (request.ResponseSchemaJson != null)
            {
                writer.WritePropertyName("response_format");
                writer.WriteRawValue(request.ResponseSchemaJson, false);
            }

            writer.WriteEndObject();
        }

        return output.ToArray();
    }

    internal static byte[] BuildOllamaChat(ProviderChatRequest request, string model, bool stream)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            WriteMessages(writer, request);
            writer.WriteBoolean("stream", stream);
            if (request.Temperature.HasValue)
            {
                writer.WriteStartObject("options");
                writer.WriteNumber("temperature", request.Temperature.Value);
                writer.WriteEndObject();
            }

            if (request.ResponseSchemaJson != null)
            {
                writer.WritePropertyName("format");
                writer.WriteRawValue(request.ResponseSchemaJson, false);
            }

            writer.WriteEndObject();
        }

        return output.ToArray();
    }

    internal static string? ReadString(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    internal static int? ReadNonNegativeInt(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value) || value < 0) return null;
        return value;
    }

    private static void WriteMessages(Utf8JsonWriter writer, ProviderChatRequest request)
    {
        writer.WriteStartArray("messages");
        foreach (var message in request.Messages)
        {
            writer.WriteStartObject();
            writer.WriteString("role", message.Role);
            writer.WriteString("content", message.Content);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteNonSystemMessages(Utf8JsonWriter writer, ProviderChatRequest request)
    {
        writer.WriteStartArray("messages");
        foreach (var message in request.Messages)
        {
            if (message.Role == "system") continue;
            writer.WriteStartObject();
            writer.WriteString("role", message.Role);
            writer.WriteString("content", message.Content);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}

internal static class ProviderMessageExtensions
{
    internal static System.Collections.Generic.IEnumerable<ProviderMessage> WhereSystem(this System.Collections.Generic.IEnumerable<ProviderMessage> messages)
    {
        return messages.Where(message => message.Role == "system");
    }

    internal static System.Collections.Generic.IEnumerable<string> SelectContent(this System.Collections.Generic.IEnumerable<ProviderMessage> messages)
    {
        return messages.Select(message => message.Content);
    }
}
