using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeTransport;

namespace MarcusAwakeFramework.Api
{
    public sealed class ProviderProfileRequest
    {
        public ProviderProfileRequest(string profileId, string providerId, string routeId, string providerKind, string baseUrl, string defaultModel, string credentialReference, bool isCloud)
        {
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            ProviderKind = ContractGuard.Id(providerKind, nameof(providerKind));
            // 与 ProviderKindCodec.TryParseWireName、RuntimeServiceHost 的准入名单三处同源；
            // 少一处就在那一层把整条形状拒掉。player2 只生图，但它是合法形状。
            if (ProviderKind != "openai_compatible" && ProviderKind != "anthropic" && ProviderKind != "ollama" && ProviderKind != "player2") throw new ArgumentException("Unsupported provider kind.", nameof(providerKind));
            BaseUrl = ContractGuard.Id(baseUrl, nameof(baseUrl));
            DefaultModel = ContractGuard.Id(defaultModel, nameof(defaultModel));
            CredentialReference = string.IsNullOrWhiteSpace(credentialReference) ? string.Empty : ContractGuard.Id(credentialReference, nameof(credentialReference));
            if (isCloud && CredentialReference.Length == 0) throw new ArgumentException("Cloud profiles require a credential reference.", nameof(credentialReference));
            IsCloud = isCloud;
        }

        public string ProfileId { get; }
        public string ProviderId { get; }
        public string RouteId { get; }
        public string ProviderKind { get; }
        public string BaseUrl { get; }
        public string DefaultModel { get; }
        public string CredentialReference { get; }
        public bool IsCloud { get; }
    }

    public sealed class ProviderCredentialRequest
    {
        public ProviderCredentialRequest(string profileId, string providerId, string routeId, string credentialReference, string secret)
        {
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            CredentialReference = ContractGuard.Id(credentialReference, nameof(credentialReference));
            if (string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("A credential secret is required.", nameof(secret));
            if (Encoding.UTF8.GetByteCount(secret) > 4096) throw new ArgumentException("The credential secret exceeds the supported limit.", nameof(secret));
            Secret = secret;
        }

        public string ProfileId { get; }
        public string ProviderId { get; }
        public string RouteId { get; }
        public string CredentialReference { get; }
        internal string Secret { get; }
    }

    public sealed class ProviderScopeRequest
    {
        public ProviderScopeRequest(string profileId, string providerId, string routeId)
        {
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
        }

        public string ProfileId { get; }
        public string ProviderId { get; }
        public string RouteId { get; }
    }

    public sealed class ProviderProfileResult
    {
        public ProviderProfileResult(string operation, string profileId, string providerId, string routeId, string status)
        {
            Operation = ContractGuard.Id(operation, nameof(operation));
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            Status = ContractGuard.Id(status, nameof(status));
        }

        public string Operation { get; }
        public string ProfileId { get; }
        public string ProviderId { get; }
        public string RouteId { get; }
        public string Status { get; }
    }

    public sealed class ProviderCredentialResult
    {
        public ProviderCredentialResult(string operation, string profileId, string providerId, string routeId, string credentialReference, string status)
        {
            Operation = ContractGuard.Id(operation, nameof(operation));
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            CredentialReference = ContractGuard.Id(credentialReference, nameof(credentialReference));
            Status = ContractGuard.Id(status, nameof(status));
        }

        public string Operation { get; }
        public string ProfileId { get; }
        public string ProviderId { get; }
        public string RouteId { get; }
        public string CredentialReference { get; }
        public string Status { get; }
    }

    public sealed class ProviderModelInfo
    {
        public ProviderModelInfo(string id, string displayName)
        {
            Id = ContractGuard.Id(id, nameof(id));
            DisplayName = displayName ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayName { get; }
    }

    public sealed class ProviderModelsResult
    {
        public ProviderModelsResult(string profileId, string providerId, string routeId, IReadOnlyList<ProviderModelInfo> models, IReadOnlyDictionary<string, string> capabilities)
        {
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            Models = new ReadOnlyCollection<ProviderModelInfo>((models ?? throw new ArgumentNullException(nameof(models))).ToArray());
            if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));
            var capabilityCopy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in capabilities) capabilityCopy.Add(pair.Key, pair.Value);
            Capabilities = new ReadOnlyDictionary<string, string>(capabilityCopy);
        }

        public string ProfileId { get; }
        public string ProviderId { get; }
        public string RouteId { get; }
        public IReadOnlyList<ProviderModelInfo> Models { get; }
        public IReadOnlyDictionary<string, string> Capabilities { get; }
    }

    public interface IProviderRuntimePort
    {
        Task<OperationResult<ProviderProfileResult>> UpsertProfileAsync(ProviderProfileRequest request, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<ProviderCredentialResult>> UpsertCredentialAsync(ProviderCredentialRequest request, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<ProviderProfileResult>> RemoveProfileAsync(ProviderScopeRequest request, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<ProviderModelsResult>> ListModelsAsync(ProviderScopeRequest request, RequestContext context, CancellationToken cancellationToken);
    }

    internal sealed class ProviderWireError
    {
        internal string Code { get; set; }
        internal string Category { get; set; }
        internal bool Retryable { get; set; }
        internal bool FallbackAllowed { get; set; }
        internal string SafeMessage { get; set; }
        internal string ProviderId { get; set; }
        internal string ProfileId { get; set; }
        internal string RouteId { get; set; }
        internal int? StatusCode { get; set; }
    }

    internal static class ProviderRuntimeSchema
    {
        internal const string ProfileUpsert = "marcus-awake.provider.profile_upsert.v1";
        internal const string CredentialUpsert = "marcus-awake.provider.credential_upsert.v1";
        internal const string ProfileRemove = "marcus-awake.provider.profile_remove.v1";
        internal const string Models = "marcus-awake.provider.models.v1";
        internal const string Complete = "marcus-awake.provider.complete.v1";
        internal const string Stream = "marcus-awake.provider.stream.v1";
        internal const string StreamEvent = "marcus-awake.provider.stream_event.v1";
        internal const string ProfileResult = "marcus-awake.provider.profile_result.v1";
        internal const string CredentialResult = "marcus-awake.provider.credential_result.v1";
        internal const string ModelsResult = "marcus-awake.provider.models_result.v1";
        internal const string Result = "marcus-awake.provider.result.v1";
        internal const string Error = "marcus-awake.provider.error.v1";
    }

    [DataContract]
    internal sealed class ProviderInputWire
    {
        [DataMember(Name = "model", EmitDefaultValue = false)] public string Model { get; set; }
        [DataMember(Name = "messages")] public List<ProviderMessageWire> Messages { get; set; }
        [DataMember(Name = "max_output_tokens", EmitDefaultValue = false)] public int? MaxOutputTokens { get; set; }
        [DataMember(Name = "temperature", EmitDefaultValue = false)] public double? Temperature { get; set; }
        [DataMember(Name = "response_schema_json", EmitDefaultValue = false)] public string ResponseSchemaJson { get; set; }
    }

    [DataContract]
    internal sealed class ProviderMessageWire
    {
        [DataMember(Name = "role")] public string Role { get; set; }
        [DataMember(Name = "content")] public string Content { get; set; }
    }

    [DataContract]
    internal sealed class ProfileResultWire
    {
        [DataMember(Name = "schema")] public string Schema { get; set; }
        [DataMember(Name = "operation")] public string Operation { get; set; }
        [DataMember(Name = "profile_id")] public string ProfileId { get; set; }
        [DataMember(Name = "provider_id")] public string ProviderId { get; set; }
        [DataMember(Name = "route_id")] public string RouteId { get; set; }
        [DataMember(Name = "status")] public string Status { get; set; }
    }

    [DataContract]
    internal sealed class CredentialResultWire
    {
        [DataMember(Name = "schema")] public string Schema { get; set; }
        [DataMember(Name = "operation")] public string Operation { get; set; }
        [DataMember(Name = "profile_id")] public string ProfileId { get; set; }
        [DataMember(Name = "provider_id")] public string ProviderId { get; set; }
        [DataMember(Name = "route_id")] public string RouteId { get; set; }
        [DataMember(Name = "credential_reference")] public string CredentialReference { get; set; }
        [DataMember(Name = "status")] public string Status { get; set; }
    }

    [DataContract]
    internal sealed class ModelsResultWire
    {
        [DataMember(Name = "schema")] public string Schema { get; set; }
        [DataMember(Name = "profile_id")] public string ProfileId { get; set; }
        [DataMember(Name = "provider_id")] public string ProviderId { get; set; }
        [DataMember(Name = "route_id")] public string RouteId { get; set; }
        [DataMember(Name = "models")] public List<ModelWire> Models { get; set; }
        [DataMember(Name = "capabilities")] public Dictionary<string, string> Capabilities { get; set; }
    }

    [DataContract]
    internal sealed class ModelWire
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "display_name")] public string DisplayName { get; set; }
    }

    [DataContract]
    internal sealed class CompletionResultWire
    {
        [DataMember(Name = "schema")] public string Schema { get; set; }
        [DataMember(Name = "profile_id")] public string ProfileId { get; set; }
        [DataMember(Name = "provider_id")] public string ProviderId { get; set; }
        [DataMember(Name = "route_id")] public string RouteId { get; set; }
        [DataMember(Name = "model_id")] public string ModelId { get; set; }
        [DataMember(Name = "content")] public string Content { get; set; }
        [DataMember(Name = "structured_json", EmitDefaultValue = false)] public string StructuredJson { get; set; }
        [DataMember(Name = "usage", EmitDefaultValue = false)] public UsageWire Usage { get; set; }
    }

    [DataContract]
    internal sealed class ProviderStreamEventWire
    {
        [DataMember(Name = "schema")] public string Schema { get; set; }
        [DataMember(Name = "profile_id")] public string ProfileId { get; set; }
        [DataMember(Name = "provider_id")] public string ProviderId { get; set; }
        [DataMember(Name = "route_id")] public string RouteId { get; set; }
        [DataMember(Name = "model_id")] public string ModelId { get; set; }
        [DataMember(Name = "event_kind")] public string EventKind { get; set; }
        [DataMember(Name = "stream_sequence")] public long StreamSequence { get; set; }
        [DataMember(Name = "terminal", EmitDefaultValue = false)] public bool? Terminal { get; set; }
        [DataMember(Name = "text", EmitDefaultValue = false)] public string Text { get; set; }
        [DataMember(Name = "structured_json", EmitDefaultValue = false)] public string StructuredJson { get; set; }
        [DataMember(Name = "usage", EmitDefaultValue = false)] public ProviderStreamUsageWire Usage { get; set; }
        [DataMember(Name = "error", EmitDefaultValue = false)] public ProviderStreamErrorWire Error { get; set; }
        [DataMember(Name = "from_provider_id", EmitDefaultValue = false)] public string FromProviderId { get; set; }
        [DataMember(Name = "to_provider_id", EmitDefaultValue = false)] public string ToProviderId { get; set; }
    }

    [DataContract]
    internal sealed class ProviderStreamUsageWire
    {
        [DataMember(Name = "input_tokens", EmitDefaultValue = false)] public int? InputTokens { get; set; }
        [DataMember(Name = "output_tokens", EmitDefaultValue = false)] public int? OutputTokens { get; set; }
    }

    [DataContract]
    internal sealed class ProviderStreamErrorWire
    {
        [DataMember(Name = "error_code")] public string ErrorCode { get; set; }
        [DataMember(Name = "category")] public string Category { get; set; }
        [DataMember(Name = "retryable")] public bool Retryable { get; set; }
        [DataMember(Name = "safe_message")] public string SafeMessage { get; set; }
        [DataMember(Name = "status_code", EmitDefaultValue = false)] public int? StatusCode { get; set; }
    }

    [DataContract]
    internal sealed class UsageWire
    {
        [DataMember(Name = "input_tokens")] public int InputTokens { get; set; }
        [DataMember(Name = "output_tokens")] public int OutputTokens { get; set; }
    }

    [DataContract]
    internal sealed class ErrorWire
    {
        [DataMember(Name = "schema")] public string Schema { get; set; }
        [DataMember(Name = "error_code")] public string ErrorCode { get; set; }
        [DataMember(Name = "category")] public string Category { get; set; }
        [DataMember(Name = "retryable")] public bool Retryable { get; set; }
        [DataMember(Name = "fallback_allowed")] public bool FallbackAllowed { get; set; }
        [DataMember(Name = "safe_message")] public string SafeMessage { get; set; }
        [DataMember(Name = "provider_id")] public string ProviderId { get; set; }
        [DataMember(Name = "profile_id")] public string ProfileId { get; set; }
        [DataMember(Name = "route_id")] public string RouteId { get; set; }
        [DataMember(Name = "status_code", EmitDefaultValue = false)] public int? StatusCode { get; set; }
    }

    internal static class ProviderRuntimeWire
    {
        internal const string ProfileUpsertSchema = "marcus-awake.provider.profile_upsert.v1";
        internal const string CredentialUpsertSchema = "marcus-awake.provider.credential_upsert.v1";
        internal const string ProfileRemoveSchema = "marcus-awake.provider.profile_remove.v1";
        internal const string ModelsSchema = "marcus-awake.provider.models.v1";
        internal const string CompleteSchema = "marcus-awake.provider.complete.v1";
        internal const string ProfileResultSchema = "marcus-awake.provider.profile_result.v1";
        internal const string CredentialResultSchema = "marcus-awake.provider.credential_result.v1";
        internal const string ModelsResultSchema = "marcus-awake.provider.models_result.v1";
        internal const string ResultSchema = "marcus-awake.provider.result.v1";
        internal const string StreamSchema = "marcus-awake.provider.stream.v1";
        internal const string StreamEventSchema = "marcus-awake.provider.stream_event.v1";
        internal const string ErrorSchema = "marcus-awake.provider.error.v1";

        internal static string Quote(string value)
        {
            var builder = new StringBuilder("\"");
            foreach (var character in (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n'))
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20) builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(character);
                        break;
                }
            }
            return builder.Append('"').ToString();
        }

        internal static string ProfileUpsertPayload(ProviderProfileRequest request)
        {
            return "{\"base_url\":" + Quote(request.BaseUrl) + ",\"credential_reference\":" + Quote(request.CredentialReference) + ",\"default_model\":" + Quote(request.DefaultModel) + ",\"is_cloud\":" + (request.IsCloud ? "true" : "false") + ",\"profile_id\":" + Quote(request.ProfileId) + ",\"provider_id\":" + Quote(request.ProviderId) + ",\"provider_kind\":" + Quote(request.ProviderKind) + ",\"route_id\":" + Quote(request.RouteId) + ",\"schema\":" + Quote(ProfileUpsertSchema) + "}";
        }

        internal static string CredentialUpsertPayload(ProviderCredentialRequest request)
        {
            return "{\"credential_reference\":" + Quote(request.CredentialReference) + ",\"profile_id\":" + Quote(request.ProfileId) + ",\"provider_id\":" + Quote(request.ProviderId) + ",\"route_id\":" + Quote(request.RouteId) + ",\"schema\":" + Quote(CredentialUpsertSchema) + ",\"secret\":" + Quote(request.Secret) + "}";
        }

        internal static string ScopePayload(string schema, ProviderScopeRequest request)
        {
            return "{\"profile_id\":" + Quote(request.ProfileId) + ",\"provider_id\":" + Quote(request.ProviderId) + ",\"route_id\":" + Quote(request.RouteId) + ",\"schema\":" + Quote(schema) + "}";
        }

        internal static OperationResult<string> CompletionPayload(AiTaskRequest request, string correlationId)
        {
            if (request == null) return Failure<string>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "An AI task request is required.", correlationId);
            if (!StringComparer.Ordinal.Equals(request.OutputSchema.SchemaId, ResultSchema) || request.OutputSchema.Major != 1 || request.OutputSchema.Minor != 0)
            {
                return Failure<string>("runtime.output_schema_mismatch", FrameworkErrorCategory.InvalidRequest, "The requested output schema is not supported by the unary Provider bridge.", correlationId);
            }

            var input = ProviderRuntimeJson.Deserialize<ProviderInputWire>(request.InputJson, out var error);
            if (input == null || !ProviderRuntimeJson.HasExactTopLevelProperties(request.InputJson, new[] { "model", "messages", "max_output_tokens", "temperature", "response_schema_json" }, new[] { "messages" }, out error))
            {
                return Failure<string>("runtime.input_schema_mismatch", FrameworkErrorCategory.InvalidRequest, "The AI task input must contain only the bounded Provider fields.", correlationId);
            }

            if (input.Messages == null || input.Messages.Count < 1 || input.Messages.Count > 64 || input.Messages.Any(message => message == null || (message.Role != "system" && message.Role != "user" && message.Role != "assistant") || message.Content == null || Encoding.UTF8.GetByteCount(message.Content) > 65536))
            {
                return Failure<string>("runtime.input_schema_mismatch", FrameworkErrorCategory.InvalidRequest, "The messages array is invalid or exceeds its limit.", correlationId);
            }
            if (input.Model != null && Encoding.UTF8.GetByteCount(input.Model) > 256) return Failure<string>("runtime.input_schema_mismatch", FrameworkErrorCategory.InvalidRequest, "The requested model is too large.", correlationId);
            if (input.MaxOutputTokens.HasValue && (input.MaxOutputTokens.Value < 1 || input.MaxOutputTokens.Value > 65536)) return Failure<string>("runtime.input_schema_mismatch", FrameworkErrorCategory.InvalidRequest, "The output token limit is outside the supported range.", correlationId);
            if (input.Temperature.HasValue && (double.IsNaN(input.Temperature.Value) || double.IsInfinity(input.Temperature.Value) || input.Temperature.Value < 0 || input.Temperature.Value > 2)) return Failure<string>("runtime.input_schema_mismatch", FrameworkErrorCategory.InvalidRequest, "The temperature is outside the supported range.", correlationId);
            if (input.ResponseSchemaJson != null && (Encoding.UTF8.GetByteCount(input.ResponseSchemaJson) > 65536 || !ProviderRuntimeJson.IsObject(input.ResponseSchemaJson))) return Failure<string>("runtime.input_schema_mismatch", FrameworkErrorCategory.InvalidRequest, "The response schema must be a bounded JSON object.", correlationId);

            var builder = new StringBuilder("{");
            if (input.MaxOutputTokens.HasValue) builder.Append("\"max_output_tokens\":").Append(input.MaxOutputTokens.Value.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"messages\":").Append(SerializeMessages(input.Messages));
            if (input.Model != null) builder.Append(",\"model\":").Append(Quote(input.Model));
            builder.Append(",\"profile_id\":").Append(Quote(request.ProfileId));
            builder.Append(",\"provider_id\":").Append(Quote(request.ProviderId));
            if (input.ResponseSchemaJson != null) builder.Append(",\"response_schema_json\":").Append(Quote(input.ResponseSchemaJson));
            builder.Append(",\"route_id\":").Append(Quote(request.RouteId));
            builder.Append(",\"schema\":").Append(Quote(CompleteSchema));
            if (input.Temperature.HasValue) builder.Append(",\"temperature\":").Append(input.Temperature.Value.ToString("R", CultureInfo.InvariantCulture));
            return OperationResult<string>.Succeeded(builder.Append('}').ToString());
        }

        internal static OperationResult<string> StreamPayload(AiTaskRequest request, RuntimeResourceBudget budget, string correlationId)
        {
            if (request == null) return Failure<string>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "An AI task request is required.", correlationId);
            if (budget == null) return Failure<string>("runtime.stream_budget_invalid", FrameworkErrorCategory.InvalidRequest, "The stream resource budget is invalid.", correlationId);
            if (!TryReadProviderInput(request, out var input, out _)) return Failure<string>("runtime.input_schema_mismatch", FrameworkErrorCategory.InvalidRequest, "The AI task input must contain only the bounded Provider fields.", correlationId);

            var builder = new StringBuilder("{");
            if (input.MaxOutputTokens.HasValue) builder.Append("\"max_output_tokens\":").Append(input.MaxOutputTokens.Value.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"messages\":").Append(SerializeMessages(input.Messages));
            if (input.Model != null) builder.Append(",\"model\":").Append(Quote(input.Model));
            builder.Append(",\"profile_id\":").Append(Quote(request.ProfileId));
            builder.Append(",\"provider_id\":").Append(Quote(request.ProviderId));
            if (input.ResponseSchemaJson != null) builder.Append(",\"response_schema_json\":").Append(Quote(input.ResponseSchemaJson));
            builder.Append(",\"resource_budget\":{\"frame_count\":").Append(budget.FrameCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"output_bytes\":").Append(budget.OutputBytes.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"tokens\":").Append(budget.Tokens.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"text_deltas\":").Append(budget.TextDeltas.ToString(CultureInfo.InvariantCulture)).Append('}');
            builder.Append(",\"route_id\":").Append(Quote(request.RouteId));
            builder.Append(",\"schema\":").Append(Quote(StreamSchema));
            if (input.Temperature.HasValue) builder.Append(",\"temperature\":").Append(input.Temperature.Value.ToString("R", CultureInfo.InvariantCulture));
            return OperationResult<string>.Succeeded(builder.Append('}').ToString());
        }

        internal static bool HasResponseSchemaJson(AiTaskRequest request)
        {
            return request != null && TryReadProviderInput(request, out var input, out _) && input.ResponseSchemaJson != null;
        }

        internal static bool TryReadStreamEvent(string payload, ProviderScopeRequest scope, out ProviderStreamEventWire result, out string error)
        {
            result = null;
            if (scope == null)
            {
                error = "provider_scope_missing";
                return false;
            }
            var wire = ProviderRuntimeJson.Deserialize<ProviderStreamEventWire>(payload, out error);
            if (wire == null || !ProviderRuntimeJson.HasExactTopLevelProperties(payload,
                new[] { "schema", "profile_id", "provider_id", "route_id", "model_id", "event_kind", "stream_sequence", "terminal", "text", "structured_json", "usage", "error", "from_provider_id", "to_provider_id" },
                new[] { "schema", "profile_id", "provider_id", "route_id", "model_id", "event_kind", "stream_sequence" }, out error)) return false;

            if (wire.Schema != StreamEventSchema || wire.ProfileId != scope.ProfileId || string.IsNullOrWhiteSpace(wire.ProviderId) || Encoding.UTF8.GetByteCount(wire.ProviderId) > 160 || wire.RouteId != scope.RouteId || string.IsNullOrWhiteSpace(wire.ModelId) || Encoding.UTF8.GetByteCount(wire.ModelId) > 256 || wire.StreamSequence < 1 || !ProviderProtocolContract.IsKnownStreamEventKind(wire.EventKind))
            {
                error = "provider_schema_mismatch";
                return false;
            }

            var terminalKind = wire.EventKind == "completed" || wire.EventKind == "cancelled" || wire.EventKind == "failed";
            if (wire.Terminal.HasValue && wire.Terminal.Value != terminalKind)
            {
                error = "stream_terminal_invalid";
                return false;
            }

            if (wire.StructuredJson != null && (wire.EventKind != "completed" || !StructuredJsonContract.TryValidateObject(wire.StructuredJson, out error)))
            {
                error = string.IsNullOrWhiteSpace(error) ? "structured_json_invalid" : error;
                return false;
            }

            if (wire.Usage != null)
            {
                if (wire.EventKind != "usage_update" && wire.EventKind != "completed" || (wire.Usage.InputTokens.HasValue && (wire.Usage.InputTokens.Value < 0 || wire.Usage.InputTokens.Value > 16777216)) || (wire.Usage.OutputTokens.HasValue && (wire.Usage.OutputTokens.Value < 0 || wire.Usage.OutputTokens.Value > 16777216)) || (!wire.Usage.InputTokens.HasValue && !wire.Usage.OutputTokens.HasValue))
                {
                    error = "stream_usage_invalid";
                    return false;
                }
                if (wire.Usage.InputTokens.HasValue && wire.Usage.OutputTokens.HasValue && (long)wire.Usage.InputTokens.Value + wire.Usage.OutputTokens.Value > 33554432)
                {
                    error = "stream_usage_invalid";
                    return false;
                }
            }

            if (wire.Error != null)
            {
                if (wire.EventKind != "failed" && wire.EventKind != "cancelled" || string.IsNullOrWhiteSpace(wire.Error.ErrorCode) || string.IsNullOrWhiteSpace(wire.Error.Category) || !ProviderProtocolContract.IsKnownProviderErrorCategory(wire.Error.Category) || Encoding.UTF8.GetByteCount(wire.Error.ErrorCode) > 128 || wire.Error.SafeMessage == null || Encoding.UTF8.GetByteCount(wire.Error.SafeMessage) > 1024)
                {
                    error = "stream_error_invalid";
                    return false;
                }
            }

            switch (wire.EventKind)
            {
                case "started":
                    if (!StringComparer.Ordinal.Equals(wire.ProviderId, scope.ProviderId)) error = "provider_identity";
                    else if (terminalKind || wire.Text != null || wire.StructuredJson != null || wire.Usage != null || wire.Error != null || wire.FromProviderId != null || wire.ToProviderId != null) error = "stream_started_invalid";
                    break;
                case "text_delta":
                    if (terminalKind || string.IsNullOrEmpty(wire.Text) || Encoding.UTF8.GetByteCount(wire.Text) > 65536 || wire.StructuredJson != null || wire.Usage != null || wire.Error != null || wire.FromProviderId != null || wire.ToProviderId != null) error = "stream_text_delta_invalid";
                    break;
                case "usage_update":
                    if (terminalKind || wire.Text != null || wire.StructuredJson != null || wire.Error != null || wire.FromProviderId != null || wire.ToProviderId != null || wire.Usage == null) error = "stream_usage_update_invalid";
                    break;
                case "route_changed":
                    if (terminalKind || wire.Text != null || wire.StructuredJson != null || wire.Usage != null || wire.Error != null || string.IsNullOrWhiteSpace(wire.FromProviderId) || string.IsNullOrWhiteSpace(wire.ToProviderId) || Encoding.UTF8.GetByteCount(wire.FromProviderId) > 160 || Encoding.UTF8.GetByteCount(wire.ToProviderId) > 160 || StringComparer.Ordinal.Equals(wire.FromProviderId, wire.ToProviderId)) error = "stream_route_change_invalid";
                    break;
                case "completed":
                    if (!terminalKind || wire.Text != null || wire.Error != null || wire.FromProviderId != null || wire.ToProviderId != null) error = "stream_completed_invalid";
                    break;
                case "cancelled":
                case "failed":
                    if (!terminalKind || wire.Text != null || wire.StructuredJson != null || wire.Usage != null || wire.Error == null || wire.FromProviderId != null || wire.ToProviderId != null) error = "stream_terminal_invalid";
                    break;
            }

            if (!string.IsNullOrEmpty(error)) return false;
            result = wire;
            return true;
        }

        internal static bool TryReadProfileResult(string payload, ProviderScopeRequest scope, string operation, out ProviderProfileResult result, out string error)
        {
            result = null;
            var wire = ProviderRuntimeJson.Deserialize<ProfileResultWire>(payload, out error);
            if (wire == null || !ProviderRuntimeJson.HasExactTopLevelProperties(payload, new[] { "schema", "operation", "profile_id", "provider_id", "route_id", "status" }, new[] { "schema", "operation", "profile_id", "provider_id", "route_id", "status" }, out error)) return false;
            if (wire.Schema != ProfileResultSchema || wire.Operation != operation || wire.ProfileId != scope.ProfileId || wire.ProviderId != scope.ProviderId || wire.RouteId != scope.RouteId || (operation == "upsert" && wire.Status != "ready") || (operation == "remove" && wire.Status != "removed"))
            {
                error = "provider_schema_mismatch";
                return false;
            }
            result = new ProviderProfileResult(wire.Operation, wire.ProfileId, wire.ProviderId, wire.RouteId, wire.Status);
            return true;
        }

        internal static bool TryReadCredentialResult(string payload, ProviderCredentialRequest request, out ProviderCredentialResult result, out string error)
        {
            result = null;
            var wire = ProviderRuntimeJson.Deserialize<CredentialResultWire>(payload, out error);
            if (wire == null || !ProviderRuntimeJson.HasExactTopLevelProperties(payload, new[] { "schema", "operation", "profile_id", "provider_id", "route_id", "credential_reference", "status" }, new[] { "schema", "operation", "profile_id", "provider_id", "route_id", "credential_reference", "status" }, out error)) return false;
            if (wire.Schema != CredentialResultSchema
                || wire.Operation != "upsert"
                || wire.ProfileId != request.ProfileId
                || wire.ProviderId != request.ProviderId
                || wire.RouteId != request.RouteId
                || wire.CredentialReference != request.CredentialReference
                || wire.Status != "saved")
            {
                error = "provider_schema_mismatch";
                return false;
            }
            result = new ProviderCredentialResult(wire.Operation, wire.ProfileId, wire.ProviderId, wire.RouteId, wire.CredentialReference, wire.Status);
            return true;
        }

        internal static bool TryReadModelsResult(string payload, ProviderScopeRequest scope, out ProviderModelsResult result, out string error)
        {
            result = null;
            var wire = ProviderRuntimeJson.Deserialize<ModelsResultWire>(payload, out error);
            if (wire == null || !ProviderRuntimeJson.HasExactTopLevelProperties(payload, new[] { "schema", "profile_id", "provider_id", "route_id", "models", "capabilities" }, new[] { "schema", "profile_id", "provider_id", "route_id", "models", "capabilities" }, out error)) return false;
            if (wire.Schema != ModelsResultSchema || wire.ProfileId != scope.ProfileId || wire.ProviderId != scope.ProviderId || wire.RouteId != scope.RouteId || wire.Models == null || wire.Models.Count > 256 || wire.Capabilities == null || wire.Capabilities.Count > 32)
            {
                error = "provider_schema_mismatch";
                return false;
            }
            var models = new List<ProviderModelInfo>();
            foreach (var model in wire.Models)
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Id) || Encoding.UTF8.GetByteCount(model.Id) > 256 || model.DisplayName == null || Encoding.UTF8.GetByteCount(model.DisplayName) > 512)
                {
                    error = "provider_schema_mismatch";
                    return false;
                }
                models.Add(new ProviderModelInfo(model.Id, model.DisplayName));
            }
            result = new ProviderModelsResult(scope.ProfileId, scope.ProviderId, scope.RouteId, models, wire.Capabilities);
            return true;
        }

        internal static bool TryReadCompletionResult(string payload, AiTaskRequest request, out CompletionResultWire result, out string error)
        {
            result = null;
            if (request == null)
            {
                error = "request_missing";
                return false;
            }
            var wire = ProviderRuntimeJson.Deserialize<CompletionResultWire>(payload, out error);
            if (wire == null || !ProviderRuntimeJson.HasExactTopLevelProperties(payload, new[] { "schema", "profile_id", "provider_id", "route_id", "model_id", "content", "structured_json", "usage" }, new[] { "schema", "profile_id", "provider_id", "route_id", "model_id", "content" }, out error)) return false;
            if (wire.Schema != ResultSchema || wire.ProfileId != request.ProfileId || wire.ProviderId != request.ProviderId || wire.RouteId != request.RouteId || string.IsNullOrWhiteSpace(wire.ModelId) || Encoding.UTF8.GetByteCount(wire.ModelId) > 256 || wire.Content == null || Encoding.UTF8.GetByteCount(wire.Content) > 65536)
            {
                error = "provider_schema_mismatch";
                return false;
            }
            if (wire.StructuredJson != null && (Encoding.UTF8.GetByteCount(wire.StructuredJson) > 65536 || !ProviderRuntimeJson.IsObject(wire.StructuredJson)))
            {
                error = "provider_schema_mismatch";
                return false;
            }
            if (wire.Usage != null && (wire.Usage.InputTokens < 0 || wire.Usage.OutputTokens < 0 || wire.Usage.InputTokens > 16777216 || wire.Usage.OutputTokens > 16777216 || (long)wire.Usage.InputTokens + wire.Usage.OutputTokens > 33554432))
            {
                error = "provider_schema_mismatch";
                return false;
            }
            result = wire;
            return true;
        }

        internal static ProviderWireError ToProviderWireError(ProviderStreamErrorWire error)
        {
            if (error == null) return null;
            return new ProviderWireError
            {
                Code = error.ErrorCode,
                Category = error.Category,
                Retryable = error.Retryable,
                FallbackAllowed = false,
                SafeMessage = error.SafeMessage ?? string.Empty,
                ProviderId = string.Empty,
                ProfileId = string.Empty,
                RouteId = string.Empty,
                StatusCode = error.StatusCode
            };
        }

        private static bool TryReadProviderInput(AiTaskRequest request, out ProviderInputWire input, out string error)
        {
            input = ProviderRuntimeJson.Deserialize<ProviderInputWire>(request == null ? null : request.InputJson, out error);
            if (input == null || !ProviderRuntimeJson.HasExactTopLevelProperties(request.InputJson, new[] { "model", "messages", "max_output_tokens", "temperature", "response_schema_json" }, new[] { "messages" }, out error)) return false;
            if (input.Messages == null || input.Messages.Count < 1 || input.Messages.Count > 64 || input.Messages.Any(message => message == null || (message.Role != "system" && message.Role != "user" && message.Role != "assistant") || message.Content == null || Encoding.UTF8.GetByteCount(message.Content) > 65536)) return false;
            if (input.Model != null && Encoding.UTF8.GetByteCount(input.Model) > 256) return false;
            if (input.MaxOutputTokens.HasValue && (input.MaxOutputTokens.Value < 1 || input.MaxOutputTokens.Value > 65536)) return false;
            if (input.Temperature.HasValue && (double.IsNaN(input.Temperature.Value) || double.IsInfinity(input.Temperature.Value) || input.Temperature.Value < 0 || input.Temperature.Value > 2)) return false;
            if (input.ResponseSchemaJson != null && (Encoding.UTF8.GetByteCount(input.ResponseSchemaJson) > 65536 || !ProviderRuntimeJson.IsObject(input.ResponseSchemaJson))) return false;
            return true;
        }

        internal static bool TryReadError(string payload, out ProviderWireError error)
        {
            error = null;
            var wire = ProviderRuntimeJson.Deserialize<ErrorWire>(payload, out var parseError);
            if (wire == null || !ProviderRuntimeJson.HasExactTopLevelProperties(payload, new[] { "schema", "error_code", "category", "retryable", "fallback_allowed", "safe_message", "provider_id", "profile_id", "route_id", "status_code" }, new[] { "schema", "error_code", "category", "retryable", "fallback_allowed", "safe_message", "provider_id", "profile_id", "route_id" }, out parseError) || wire.Schema != ErrorSchema || string.IsNullOrWhiteSpace(wire.ErrorCode) || string.IsNullOrWhiteSpace(wire.Category)) return false;
            error = new ProviderWireError { Code = wire.ErrorCode, Category = wire.Category, Retryable = wire.Retryable, FallbackAllowed = wire.FallbackAllowed, SafeMessage = wire.SafeMessage ?? string.Empty, ProviderId = wire.ProviderId ?? string.Empty, ProfileId = wire.ProfileId ?? string.Empty, RouteId = wire.RouteId ?? string.Empty, StatusCode = wire.StatusCode };
            return true;
        }

        private static string SerializeMessages(IReadOnlyList<ProviderMessageWire> messages)
        {
            var builder = new StringBuilder("[");
            for (var index = 0; index < messages.Count; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append("{\"content\":").Append(Quote(messages[index].Content)).Append(",\"role\":").Append(Quote(messages[index].Role)).Append('}');
            }
            return builder.Append(']').ToString();
        }

        private static OperationResult<T> Failure<T>(string code, FrameworkErrorCategory category, string fallback, string correlationId)
        {
            return OperationResult<T>.Failed(FrameworkErrors.Create(code, category, fallback, correlationId));
        }
    }

    internal static class ProviderRuntimeJson
    {
        internal static T Deserialize<T>(string json, out string error) where T : class
        {
            error = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("json_missing");
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(T));
                    var value = serializer.ReadObject(stream) as T;
                    if (value == null) throw new InvalidDataException("json_object_required");
                    return value;
                }
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is SerializationException || exception is ArgumentException || exception is FormatException)
            {
                error = "provider_json_invalid";
                return null;
            }
        }

        internal static bool IsObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            var index = 0;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '{') return false;
            return TrySkipValue(json, ref index, 0) && ConsumeEnd(json, ref index);
        }

        internal static bool HasExactTopLevelProperties(string json, IReadOnlyList<string> allowed, IReadOnlyList<string> required, out string error)
        {
            error = string.Empty;
            if (!TryReadTopLevelNames(json, out var names, out error)) return false;
            if (names.Any(name => !allowed.Contains(name)) || names.Count != names.Distinct(StringComparer.Ordinal).Count() || required.Any(name => !names.Contains(name)))
            {
                error = "provider_schema_mismatch";
                return false;
            }
            return true;
        }

        private static bool TryReadTopLevelNames(string json, out List<string> names, out string error)
        {
            names = new List<string>();
            error = string.Empty;
            try
            {
                var index = 0;
                SkipWhitespace(json, ref index);
                Require(json, ref index, '{');
                SkipWhitespace(json, ref index);
                if (Consume(json, ref index, '}')) return true;
                while (true)
                {
                    SkipWhitespace(json, ref index);
                    var key = ReadString(json, ref index);
                    if (names.Contains(key, StringComparer.Ordinal)) throw new FormatException("duplicate_property");
                    names.Add(key);
                    SkipWhitespace(json, ref index);
                    Require(json, ref index, ':');
                    SkipWhitespace(json, ref index);
                    if (!TrySkipValue(json, ref index, 0)) throw new FormatException("value_invalid");
                    SkipWhitespace(json, ref index);
                    if (Consume(json, ref index, '}')) break;
                    Require(json, ref index, ',');
                }
                SkipWhitespace(json, ref index);
                if (index != json.Length) throw new FormatException("trailing_data");
                return true;
            }
            catch (FormatException)
            {
                names.Clear();
                error = "provider_json_invalid";
                return false;
            }
        }

        private static bool TrySkipValue(string json, ref int index, int depth)
        {
            if (depth > 16 || index >= json.Length) return false;
            if (json[index] == '"') { ReadString(json, ref index); return true; }
            if (json[index] == '{') return TrySkipComposite(json, ref index, '{', '}', depth + 1);
            if (json[index] == '[') return TrySkipComposite(json, ref index, '[', ']', depth + 1);
            var start = index;
            while (index < json.Length && ",}] \t\r\n".IndexOf(json[index]) < 0) index++;
            return index > start;
        }

        private static bool TrySkipComposite(string json, ref int index, char open, char close, int depth)
        {
            if (depth > 16 || !Consume(json, ref index, open)) return false;
            var inString = false;
            var escaped = false;
            var nested = 1;
            while (index < json.Length)
            {
                var character = json[index++];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') inString = false;
                    continue;
                }
                if (character == '"') inString = true;
                else if (character == open) nested++;
                else if (character == close && --nested == 0) return true;
            }
            return false;
        }

        private static string ReadString(string json, ref int index)
        {
            if (!Consume(json, ref index, '"')) throw new FormatException("string_required");
            var builder = new StringBuilder();
            var escaped = false;
            while (index < json.Length)
            {
                var character = json[index++];
                if (escaped) { builder.Append(character); escaped = false; continue; }
                if (character == '\\') { builder.Append(character); escaped = true; continue; }
                if (character == '"') return builder.ToString();
                if (character < 0x20) throw new FormatException("control_character");
                builder.Append(character);
            }
            throw new FormatException("string_unterminated");
        }

        private static bool ConsumeEnd(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            return index == json.Length;
        }

        private static void SkipWhitespace(string json, ref int index) { while (index < json.Length && " \t\r\n".IndexOf(json[index]) >= 0) index++; }
        private static bool Consume(string json, ref int index, char expected) { if (index < json.Length && json[index] == expected) { index++; return true; } return false; }
        private static void Require(string json, ref int index, char expected) { if (!Consume(json, ref index, expected)) throw new FormatException("expected_character"); }
    }
}
