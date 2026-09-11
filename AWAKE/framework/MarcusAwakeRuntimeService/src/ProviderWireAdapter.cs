using MarcusAwakeTransport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MarcusAwakeRuntimeService;

internal enum ProviderWireOperation
{
    ProfileUpsert,
    CredentialUpsert,
    ProfileRemove,
    Models,
    Complete,
    Stream
}

internal sealed class ProviderWireRequest
{
    internal ProviderWireRequest(
        ProviderWireOperation operation,
        string profileId,
        string providerId,
        string routeId,
        string providerKind = "",
        string baseUrl = "",
        string defaultModel = "",
        string? credentialReference = null,
        bool isCloud = false,
        IReadOnlyList<ProviderWireMessage>? messages = null,
        string? model = null,
        int? maxOutputTokens = null,
        double? temperature = null,
        string? responseSchemaJson = null,
        ProviderStreamBudget? resourceBudget = null,
        string? credentialSecret = null)
    {
        Operation = operation;
        ProfileId = profileId;
        ProviderId = providerId;
        RouteId = routeId;
        ProviderKind = providerKind;
        BaseUrl = baseUrl;
        DefaultModel = defaultModel;
        CredentialReference = credentialReference;
        CredentialSecret = credentialSecret;
        IsCloud = isCloud;
        Messages = messages ?? Array.Empty<ProviderWireMessage>();
        Model = model;
        MaxOutputTokens = maxOutputTokens;
        Temperature = temperature;
        ResponseSchemaJson = responseSchemaJson;
        ResourceBudget = resourceBudget;
    }

    internal ProviderWireOperation Operation { get; }
    internal string ProfileId { get; }
    internal string ProviderId { get; }
    internal string RouteId { get; }
    internal string ProviderKind { get; }
    internal string BaseUrl { get; }
    internal string DefaultModel { get; }
    internal string? CredentialReference { get; }
    internal string? CredentialSecret { get; }
    internal bool IsCloud { get; }
    internal IReadOnlyList<ProviderWireMessage> Messages { get; }
    internal string? Model { get; }
    internal int? MaxOutputTokens { get; }
    internal double? Temperature { get; }
    internal string? ResponseSchemaJson { get; }
    internal ProviderStreamBudget? ResourceBudget { get; }
}

internal sealed class ProviderWireMessage
{
    internal ProviderWireMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    internal string Role { get; }
    internal string Content { get; }
}

internal sealed class ProviderModelProjection
{
    internal ProviderModelProjection(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    internal string Id { get; }
    internal string DisplayName { get; }
}

internal sealed class ProviderCompletionProjection
{
    internal ProviderCompletionProjection(string modelId, string content, string? structuredJson, int? inputTokens, int? outputTokens)
    {
        ModelId = modelId;
        Content = content;
        StructuredJson = structuredJson;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    internal string ModelId { get; }
    internal string Content { get; }
    internal string? StructuredJson { get; }
    internal int? InputTokens { get; }
    internal int? OutputTokens { get; }
}

internal sealed class ProviderWireError
{
    internal ProviderWireError(string errorCode, string category, bool retryable, bool fallbackAllowed, string safeMessage, int? statusCode = null)
    {
        ErrorCode = errorCode;
        Category = category;
        Retryable = retryable;
        FallbackAllowed = fallbackAllowed;
        SafeMessage = safeMessage;
        StatusCode = statusCode;
    }

    internal string ErrorCode { get; }
    internal string Category { get; }
    internal bool Retryable { get; }
    internal bool FallbackAllowed { get; }
    internal string SafeMessage { get; }
    internal int? StatusCode { get; }
}

internal sealed class ProviderWireResult
{
    private ProviderWireResult(string? payloadJson, ProviderWireError? error)
    {
        PayloadJson = payloadJson;
        Error = error;
    }

    internal bool IsSuccess => Error == null;
    internal string? PayloadJson { get; }
    internal ProviderWireError? Error { get; }

    internal static ProviderWireResult Success(string payloadJson)
    {
        return new ProviderWireResult(payloadJson ?? throw new ArgumentNullException(nameof(payloadJson)), null);
    }

    internal static ProviderWireResult Failure(ProviderWireError error)
    {
        return new ProviderWireResult(null, error ?? throw new ArgumentNullException(nameof(error)));
    }
}

internal static class ProviderWireAdapter
{
    private const int MaximumIdentifierBytes = 160;
    private const int MaximumModelBytes = 256;
    private const int MaximumUrlBytes = 2048;
    private const int MaximumMessageBytes = 65536;
    private const int MaximumStructuredJsonBytes = 65536;
    private const int MaximumMessages = 64;

    internal static bool TryParse(PipeEnvelope envelope, out ProviderWireRequest? request, out string error)
    {
        request = null;
        error = string.Empty;
        if (envelope == null || envelope.TaskScope == null)
        {
            error = "provider_schema_mismatch";
            return false;
        }

        if (!TryMapOperation(envelope.MessageType, out var operation, out var schema)
            || !StringComparer.Ordinal.Equals(envelope.PayloadSchema, schema))
        {
            error = "provider_schema_mismatch";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(envelope.PayloadJson ?? string.Empty, new JsonDocumentOptions
            {
                MaxDepth = ProtocolConstants.MaxJsonDepth,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "provider_schema_mismatch";
                return false;
            }

            var root = document.RootElement;
            if (operation == ProviderWireOperation.ProfileUpsert)
            {
                return TryParseProfileUpsert(root, envelope, out request, out error);
            }

            if (operation == ProviderWireOperation.CredentialUpsert)
            {
                return TryParseCredentialUpsert(root, envelope, out request, out error);
            }

            if (!TryReadScope(root, envelope, out error)) return false;
            if (operation == ProviderWireOperation.ProfileRemove || operation == ProviderWireOperation.Models)
            {
                if (!HasOnlyProperties(root, "schema", "profile_id", "provider_id", "route_id"))
                {
                    error = "provider_schema_mismatch";
                    return false;
                }

                request = new ProviderWireRequest(operation, envelope.TaskScope.ProfileId, envelope.TaskScope.ProviderId, envelope.TaskScope.RouteId);
                return true;
            }

            return TryParseCompletion(root, envelope, operation, out request, out error);
        }
        catch (JsonException)
        {
            error = "provider_schema_mismatch";
            return false;
        }
    }

    internal static string BuildProfileResult(ProviderWireRequest request, string status)
    {
        return SerializeBounded(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = ProtocolConstants.ProviderProfileResultSchemaV1,
            ["operation"] = request.Operation == ProviderWireOperation.ProfileUpsert ? "upsert" : "remove",
            ["profile_id"] = request.ProfileId,
            ["provider_id"] = request.ProviderId,
            ["route_id"] = request.RouteId,
            ["status"] = status
        });
    }

    internal static string BuildCredentialResult(ProviderWireRequest request, string status)
    {
        return SerializeBounded(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = ProtocolConstants.ProviderCredentialResultSchemaV1,
            ["operation"] = "upsert",
            ["profile_id"] = request.ProfileId,
            ["provider_id"] = request.ProviderId,
            ["route_id"] = request.RouteId,
            ["credential_reference"] = request.CredentialReference,
            ["status"] = status
        });
    }

    internal static string BuildModelsResult(ProviderWireRequest request, IReadOnlyList<ProviderModelProjection> models)
    {
        var modelValues = models.Select(model => (object)new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = model.Id,
            ["display_name"] = model.DisplayName
        }).ToArray();
        var capabilities = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["model_discovery"] = "available",
            ["text_generation"] = "unverified",
            ["streaming"] = "unverified",
            ["usage"] = "unverified",
            ["structured_output"] = "unverified"
        };

        return SerializeBounded(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = ProtocolConstants.ProviderModelsResultSchemaV1,
            ["profile_id"] = request.ProfileId,
            ["provider_id"] = request.ProviderId,
            ["route_id"] = request.RouteId,
            ["models"] = modelValues,
            ["capabilities"] = capabilities
        });
    }

    internal static string BuildCompletionResult(ProviderWireRequest request, ProviderCompletionProjection completion)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = ProtocolConstants.ProviderResultSchemaV1,
            ["profile_id"] = request.ProfileId,
            ["provider_id"] = request.ProviderId,
            ["route_id"] = request.RouteId,
            ["model_id"] = completion.ModelId,
            ["content"] = completion.Content
        };
        if (completion.StructuredJson != null) payload["structured_json"] = completion.StructuredJson;
        if (completion.InputTokens.HasValue || completion.OutputTokens.HasValue)
        {
            var usage = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (completion.InputTokens.HasValue) usage["input_tokens"] = completion.InputTokens.Value;
            if (completion.OutputTokens.HasValue) usage["output_tokens"] = completion.OutputTokens.Value;
            payload["usage"] = usage;
        }

        return SerializeBounded(payload);
    }

    internal static string BuildStreamEventPayload(ProviderWireRequest request, ProviderStreamEventProjection streamEvent)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = ProtocolConstants.ProviderStreamEventSchemaV1,
            ["profile_id"] = request.ProfileId,
            ["route_id"] = request.RouteId,
            ["provider_id"] = streamEvent.ProviderId,
            ["model_id"] = streamEvent.ModelId,
            ["event_kind"] = streamEvent.EventKind,
            ["stream_sequence"] = streamEvent.Sequence
        };
        if (!string.IsNullOrEmpty(streamEvent.Text)) payload["text"] = streamEvent.Text;
        if (!string.IsNullOrEmpty(streamEvent.StructuredJson)) payload["structured_json"] = streamEvent.StructuredJson;
        if (streamEvent.InputTokens.HasValue || streamEvent.OutputTokens.HasValue)
        {
            var usage = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (streamEvent.InputTokens.HasValue) usage["input_tokens"] = streamEvent.InputTokens.Value;
            if (streamEvent.OutputTokens.HasValue) usage["output_tokens"] = streamEvent.OutputTokens.Value;
            payload["usage"] = usage;
        }
        if (streamEvent.Error != null)
        {
            var error = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["error_code"] = streamEvent.Error.ErrorCode,
                ["category"] = streamEvent.Error.Category,
                ["retryable"] = streamEvent.Error.Retryable,
                ["safe_message"] = streamEvent.Error.SafeMessage
            };
            if (streamEvent.Error.StatusCode.HasValue) error["status_code"] = streamEvent.Error.StatusCode.Value;
            payload["error"] = error;
        }
        if (!string.IsNullOrEmpty(streamEvent.FromProviderId)) payload["from_provider_id"] = streamEvent.FromProviderId;
        if (!string.IsNullOrEmpty(streamEvent.ToProviderId)) payload["to_provider_id"] = streamEvent.ToProviderId;
        return SerializeBounded(payload);
    }

    internal static ProviderWireError SchemaError(string code = "provider_schema_mismatch")
    {
        return new ProviderWireError(code, "invalid_request", false, false, "Provider request did not satisfy the fixed runtime contract.");
    }

    internal static ProviderWireError RuntimeUnavailable(string message = "Provider runtime is unavailable.")
    {
        return new ProviderWireError("provider.runtime_unavailable", "unavailable", true, false, message);
    }

    internal static ProviderWireError ProfileNotFound()
    {
        return new ProviderWireError("profile.not_found", "not_found", false, false, "Provider profile was not found in the current runtime scope.");
    }

    internal static ProviderWireError IdempotencyConflict()
    {
        return new ProviderWireError("provider.idempotency_conflict", "conflict", false, false, "The provider request conflicts with an existing idempotency record.");
    }

    internal static ProviderWireError TaskInProgress()
    {
        return new ProviderWireError("provider.task_in_progress", "unavailable", true, false, "An equivalent provider request is already in progress.");
    }

    internal static ProviderWireError LedgerCapacity()
    {
        return new ProviderWireError("runtime.ledger_capacity", "unavailable", true, false, "The runtime stream replay ledger is temporarily full.");
    }

    internal static ProviderWireError Cancelled()
    {
        return new ProviderWireError("request.cancelled", "cancelled", false, false, "Provider request was cancelled.");
    }

    internal static ProviderWireError DeadlineExpired()
    {
        return new ProviderWireError("request.deadline_expired", "timeout", false, false, "Provider request deadline expired.");
    }

    internal static ProviderWireError RouteAnchorUnavailable()
    {
        return new ProviderWireError("route_anchor_unavailable", "unavailable", false, false, "The requested provider route anchor is unavailable.");
    }

    internal static ProviderWireError RouteNoCandidate()
    {
        return new ProviderWireError("route_no_candidate", "unavailable", false, false, "No provider candidate is available for this route.");
    }

    internal static ProviderWireError ResourceExhausted()
    {
        return new ProviderWireError("resource_exhausted", "resource_exhausted", false, false, "The provider stream resource budget was exhausted.");
    }

    internal static ProviderWireError StreamIncomplete()
    {
        return new ProviderWireError("stream_incomplete", "incomplete_stream", false, false, "Provider stream ended without a terminal event.");
    }

    internal static ProviderWireError StreamTerminalAlreadyEmitted()
    {
        return new ProviderWireError("stream_terminal_already_emitted", "conflict", false, false, "Provider emitted an event after its terminal event.");
    }

    internal static ProviderWireError ProfileChanged()
    {
        return new ProviderWireError("provider_profile_changed", "conflict", true, false, "Provider profile changed while the stream was starting.");
    }

    internal static string BuildErrorPayload(ProviderWireRequest? request, ProviderWireError error)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = ProtocolConstants.ProviderErrorSchemaV1,
            ["error_code"] = error.ErrorCode,
            ["category"] = error.Category,
            ["retryable"] = error.Retryable,
            ["fallback_allowed"] = error.FallbackAllowed,
            ["safe_message"] = error.SafeMessage,
            ["provider_id"] = request?.ProviderId ?? string.Empty,
            ["profile_id"] = request?.ProfileId ?? string.Empty,
            ["route_id"] = request?.RouteId ?? string.Empty
        };
        if (error.StatusCode.HasValue) payload["status_code"] = error.StatusCode.Value;
        return SerializeBounded(payload);
    }

    private static bool TryParseProfileUpsert(JsonElement root, PipeEnvelope envelope, out ProviderWireRequest? request, out string error)
    {
        request = null;
        error = string.Empty;
        if (!HasOnlyProperties(root, "schema", "profile_id", "provider_id", "route_id", "provider_kind", "base_url", "default_model", "credential_reference", "is_cloud")
            || !TryReadScope(root, envelope, out error)
            || !TryReadRequiredString(root, "provider_kind", out var providerKind, out error)
            || !TryReadRequiredString(root, "base_url", out var baseUrl, out error)
            || !TryReadRequiredString(root, "default_model", out var defaultModel, out error)
            || !TryReadRequiredBool(root, "is_cloud", out var isCloud, out error))
        {
            error = string.IsNullOrWhiteSpace(error) ? "provider_schema_mismatch" : error;
            return false;
        }

        if (providerKind is not ("openai_compatible" or "anthropic" or "ollama")
            || !IsBoundedUtf8(baseUrl, MaximumUrlBytes)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !IsBoundedUtf8(defaultModel, MaximumModelBytes))
        {
            error = "provider_schema_mismatch";
            return false;
        }

        string? credentialReference = null;
        if (root.TryGetProperty("credential_reference", out var credentialProperty))
        {
            if (credentialProperty.ValueKind != JsonValueKind.String || !IsBoundedIdentifier(credentialProperty.GetString(), MaximumIdentifierBytes))
            {
                error = "provider_schema_mismatch";
                return false;
            }

            credentialReference = credentialProperty.GetString();
        }

        if (isCloud && string.IsNullOrWhiteSpace(credentialReference))
        {
            error = "provider_schema_mismatch";
            return false;
        }

        request = new ProviderWireRequest(
            ProviderWireOperation.ProfileUpsert,
            envelope.TaskScope!.ProfileId,
            envelope.TaskScope.ProviderId,
            envelope.TaskScope.RouteId,
            providerKind,
            baseUrl,
            defaultModel,
            credentialReference,
            isCloud);
        return true;
    }

    private static bool TryParseCredentialUpsert(JsonElement root, PipeEnvelope envelope, out ProviderWireRequest? request, out string error)
    {
        request = null;
        error = string.Empty;
        if (!HasOnlyProperties(root, "schema", "profile_id", "provider_id", "route_id", "credential_reference", "secret")
            || !TryReadScope(root, envelope, out error)
            || !TryReadRequiredString(root, "credential_reference", out var credentialReference, out error)
            || !TryReadRequiredString(root, "secret", out var secret, out error))
        {
            error = string.IsNullOrWhiteSpace(error) ? "provider_schema_mismatch" : error;
            return false;
        }

        if (!IsBoundedIdentifier(credentialReference, MaximumIdentifierBytes)
            || !IsBoundedUtf8(secret, 4096))
        {
            error = "provider_schema_mismatch";
            return false;
        }

        request = new ProviderWireRequest(
            ProviderWireOperation.CredentialUpsert,
            envelope.TaskScope!.ProfileId,
            envelope.TaskScope.ProviderId,
            envelope.TaskScope.RouteId,
            credentialReference: credentialReference,
            credentialSecret: secret);
        return true;
    }

    private static bool TryParseCompletion(JsonElement root, PipeEnvelope envelope, ProviderWireOperation operation, out ProviderWireRequest? request, out string error)
    {
        request = null;
        error = string.Empty;
        var allowed = new List<string> { "schema", "profile_id", "provider_id", "route_id", "model", "messages", "max_output_tokens", "temperature", "response_schema_json" };
        if (operation == ProviderWireOperation.Stream) allowed.Add("resource_budget");
        if (!HasOnlyProperties(root, allowed.ToArray())
            || !root.TryGetProperty("messages", out var messagesProperty)
            || !TryReadMessages(messagesProperty, out var messages, out error))
        {
            error = string.IsNullOrWhiteSpace(error) ? "provider_schema_mismatch" : error;
            return false;
        }

        string? model = null;
        if (root.TryGetProperty("model", out var modelProperty))
        {
            if (modelProperty.ValueKind != JsonValueKind.String || !IsBoundedUtf8(modelProperty.GetString(), MaximumModelBytes))
            {
                error = "provider_schema_mismatch";
                return false;
            }

            model = modelProperty.GetString();
        }

        int? maxOutputTokens = null;
        if (root.TryGetProperty("max_output_tokens", out var maxOutputProperty))
        {
            if (maxOutputProperty.ValueKind != JsonValueKind.Number || !maxOutputProperty.TryGetInt32(out var value) || value < 1 || value > 65536)
            {
                error = "provider_schema_mismatch";
                return false;
            }

            maxOutputTokens = value;
        }

        double? temperature = null;
        if (root.TryGetProperty("temperature", out var temperatureProperty))
        {
            if (temperatureProperty.ValueKind != JsonValueKind.Number || !temperatureProperty.TryGetDouble(out var value) || double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 2)
            {
                error = "provider_schema_mismatch";
                return false;
            }

            temperature = value;
        }

        string? responseSchemaJson = null;
        if (root.TryGetProperty("response_schema_json", out var responseSchemaProperty))
        {
            if (responseSchemaProperty.ValueKind != JsonValueKind.String || !IsBoundedUtf8(responseSchemaProperty.GetString(), MaximumStructuredJsonBytes) || !IsJsonObject(responseSchemaProperty.GetString()))
            {
                error = "provider_schema_mismatch";
                return false;
            }

            responseSchemaJson = responseSchemaProperty.GetString();
        }

        ProviderStreamBudget? resourceBudget = null;
        if (operation == ProviderWireOperation.Stream)
        {
            if (!root.TryGetProperty("resource_budget", out var budgetProperty) || !TryReadResourceBudget(budgetProperty, out resourceBudget, out error)) return false;
        }

        request = new ProviderWireRequest(
            operation,
            envelope.TaskScope!.ProfileId,
            envelope.TaskScope.ProviderId,
            envelope.TaskScope.RouteId,
            messages: messages,
            model: model,
            maxOutputTokens: maxOutputTokens,
            temperature: temperature,
            responseSchemaJson: responseSchemaJson,
            resourceBudget: resourceBudget);
        return true;
    }

    private static bool TryReadResourceBudget(JsonElement property, out ProviderStreamBudget? budget, out string error)
    {
        budget = null;
        error = string.Empty;
        if (property.ValueKind != JsonValueKind.Object || !HasOnlyProperties(property, "frame_count", "output_bytes", "tokens", "text_deltas") || !TryReadPositiveInt(property, "frame_count", out var frameCount) || !TryReadPositiveInt(property, "output_bytes", out var outputBytes) || !TryReadPositiveInt(property, "tokens", out var tokens) || !TryReadPositiveInt(property, "text_deltas", out var textDeltas))
        {
            error = "stream_budget_invalid";
            return false;
        }

        if (frameCount < 2 || frameCount > 125 || outputBytes < 1 || tokens < 1 || textDeltas < 1)
        {
            error = "stream_budget_invalid";
            return false;
        }

        budget = new ProviderStreamBudget(frameCount, outputBytes, tokens, textDeltas);
        return true;
    }

    private static bool TryReadPositiveInt(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value) && value > 0;
    }

    private static bool TryMapOperation(string messageType, out ProviderWireOperation operation, out string schema)
    {
        switch (messageType)
        {
            case ProtocolConstants.MessageTypeProviderProfileUpsertV1:
                operation = ProviderWireOperation.ProfileUpsert;
                schema = ProtocolConstants.ProviderProfileUpsertSchemaV1;
                return true;
            case ProtocolConstants.MessageTypeProviderCredentialUpsertV1:
                operation = ProviderWireOperation.CredentialUpsert;
                schema = ProtocolConstants.ProviderCredentialUpsertSchemaV1;
                return true;
            case ProtocolConstants.MessageTypeProviderProfileRemoveV1:
                operation = ProviderWireOperation.ProfileRemove;
                schema = ProtocolConstants.ProviderProfileRemoveSchemaV1;
                return true;
            case ProtocolConstants.MessageTypeProviderModelsV1:
                operation = ProviderWireOperation.Models;
                schema = ProtocolConstants.ProviderModelsSchemaV1;
                return true;
            case ProtocolConstants.MessageTypeProviderCompleteV1:
                operation = ProviderWireOperation.Complete;
                schema = ProtocolConstants.ProviderCompleteSchemaV1;
                return true;
            case ProtocolConstants.MessageTypeProviderStreamV1:
                operation = ProviderWireOperation.Stream;
                schema = ProtocolConstants.ProviderStreamSchemaV1;
                return true;
            default:
                operation = default;
                schema = string.Empty;
                return false;
        }
    }

    private static bool TryReadScope(JsonElement root, PipeEnvelope envelope, out string error)
    {
        error = string.Empty;
        var scope = envelope.TaskScope!;
        foreach (var name in new[] { "schema", "profile_id", "provider_id", "route_id" })
        {
            if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            {
                error = "provider_schema_mismatch";
                return false;
            }
        }

        if (!StringComparer.Ordinal.Equals(root.GetProperty("profile_id").GetString(), scope.ProfileId)
            || !StringComparer.Ordinal.Equals(root.GetProperty("provider_id").GetString(), scope.ProviderId)
            || !StringComparer.Ordinal.Equals(root.GetProperty("route_id").GetString(), scope.RouteId))
        {
            error = "provider_schema_mismatch";
            return false;
        }

        return true;
    }

    private static bool TryReadMessages(JsonElement property, out IReadOnlyList<ProviderWireMessage> messages, out string error)
    {
        messages = Array.Empty<ProviderWireMessage>();
        error = string.Empty;
        if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() < 1 || property.GetArrayLength() > MaximumMessages)
        {
            error = "provider_schema_mismatch";
            return false;
        }

        var values = new List<ProviderWireMessage>(property.GetArrayLength());
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !HasOnlyProperties(item, "role", "content")
                || !TryReadRequiredString(item, "role", out var role, out error)
                || !TryReadRequiredString(item, "content", out var content, out error)
                || role is not ("system" or "user" or "assistant")
                || !IsBoundedUtf8(content, MaximumMessageBytes))
            {
                error = "provider_schema_mismatch";
                return false;
            }

            values.Add(new ProviderWireMessage(role, content));
        }

        messages = values.AsReadOnly();
        return true;
    }

    private static bool HasOnlyProperties(JsonElement root, params string[] allowed)
    {
        var set = new HashSet<string>(allowed, StringComparer.Ordinal);
        return root.EnumerateObject().All(property => set.Contains(property.Name));
    }

    private static bool TryReadRequiredString(JsonElement root, string name, out string value, out string error)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            error = "provider_schema_mismatch";
            return false;
        }

        value = property.GetString() ?? string.Empty;
        error = string.Empty;
        return true;
    }

    private static bool TryReadRequiredBool(JsonElement root, string name, out bool value, out string error)
    {
        value = false;
        if (!root.TryGetProperty(name, out var property) || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            error = "provider_schema_mismatch";
            return false;
        }

        value = property.GetBoolean();
        error = string.Empty;
        return true;
    }

    private static bool IsBoundedIdentifier(string? value, int maximumBytes)
    {
        return !string.IsNullOrWhiteSpace(value)
            && IsBoundedUtf8(value, maximumBytes)
            && value.All(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' or ':' or '/');
    }

    private static bool IsBoundedUtf8(string? value, int maximumBytes)
    {
        return value != null && Encoding.UTF8.GetByteCount(value) <= maximumBytes && !value.Any(char.IsControl);
    }

    private static bool IsJsonObject(string? value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            using var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = ProtocolConstants.MaxJsonDepth, AllowTrailingCommas = false });
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string SerializeBounded(object value)
    {
        var json = JsonSerializer.Serialize(value);
        if (Encoding.UTF8.GetByteCount(json) > ProtocolConstants.MaxPayloadBytes) throw new InvalidOperationException("provider_response_too_large");
        return json;
    }
}
