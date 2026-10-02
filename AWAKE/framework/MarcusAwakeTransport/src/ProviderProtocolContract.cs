using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MarcusAwakeTransport
{
    public enum ProviderStreamState
    {
        NotStarted,
        Started,
        VisibleText,
        Terminal
    }

    public sealed class ProviderStreamDecision
    {
        internal ProviderStreamDecision(bool accepted, string errorCode)
        {
            IsAccepted = accepted;
            ErrorCode = errorCode ?? string.Empty;
        }

        public bool IsAccepted { get; }
        public string ErrorCode { get; }
    }

    public sealed class ProviderStreamStateMachine
    {
        private readonly string streamId;
        private readonly string profileId;
        private long nextSequence;
        private string activeProviderId;
        private ProviderStreamState state;

        public ProviderStreamStateMachine(string streamId, string providerId, string profileId)
        {
            if (string.IsNullOrWhiteSpace(streamId)) throw new ArgumentException("stream_id_required", nameof(streamId));
            if (string.IsNullOrWhiteSpace(providerId)) throw new ArgumentException("provider_id_required", nameof(providerId));
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("profile_id_required", nameof(profileId));
            this.streamId = streamId;
            activeProviderId = providerId;
            this.profileId = profileId;
            state = ProviderStreamState.NotStarted;
        }

        public ProviderStreamState State => state;
        public long NextSequence => nextSequence + 1;
        public string ActiveProviderId => activeProviderId;
        public string StreamId => streamId;
        public string ProfileId => profileId;

        public ProviderStreamDecision Accept(
            string eventKind,
            long streamSequence,
            string providerId,
            string fromProviderId = "",
            string toProviderId = "",
            bool terminal = false,
            bool hasText = false,
            bool hasUsage = false,
            bool hasError = false,
            bool hasStructured = false)
        {
            if (state == ProviderStreamState.Terminal) return Reject("stream_terminal_already_emitted");
            if (!ProviderProtocolContract.IsKnownStreamEventKind(eventKind)) return Reject("stream_event_kind_invalid");
            if (streamSequence != nextSequence + 1) return Reject("stream_sequence_invalid");
            if (string.IsNullOrWhiteSpace(providerId)) return Reject("stream_provider_missing");

            if (eventKind == "started")
            {
                if (state != ProviderStreamState.NotStarted || streamSequence != 1 || terminal || hasText || hasUsage || hasError || hasStructured || !StringComparer.Ordinal.Equals(providerId, activeProviderId)) return Reject("stream_started_invalid");
                state = ProviderStreamState.Started;
            }
            else
            {
                if (state == ProviderStreamState.NotStarted) return Reject("stream_started_required");
                if (eventKind == "route_changed")
                {
                    if (state == ProviderStreamState.VisibleText) return Reject("route_change_after_visible_text");
                    if (terminal || hasText || hasUsage || hasError || hasStructured || string.IsNullOrWhiteSpace(fromProviderId) || string.IsNullOrWhiteSpace(toProviderId) || !StringComparer.Ordinal.Equals(providerId, fromProviderId) || !StringComparer.Ordinal.Equals(providerId, activeProviderId) || StringComparer.Ordinal.Equals(fromProviderId, toProviderId)) return Reject("stream_route_change_invalid");
                    activeProviderId = toProviderId;
                }
                else
                {
                    if (!StringComparer.Ordinal.Equals(providerId, activeProviderId)) return Reject("stream_provider_mismatch");
                    if (eventKind == "text_delta")
                    {
                        if (terminal || hasUsage || hasError || hasStructured || !hasText) return Reject("stream_text_delta_invalid");
                        state = ProviderStreamState.VisibleText;
                    }
                    else if (eventKind == "usage_update")
                    {
                        if (terminal || hasText || hasError || hasStructured || !hasUsage) return Reject("stream_usage_update_invalid");
                    }
                    else if (eventKind == "completed")
                    {
                        if (!terminal || hasError || hasText) return Reject("stream_completed_invalid");
                        state = ProviderStreamState.Terminal;
                    }
                    else if (eventKind == "cancelled" || eventKind == "failed")
                    {
                        if (!terminal || !hasError || hasText || hasUsage || hasStructured) return Reject("stream_terminal_invalid");
                        state = ProviderStreamState.Terminal;
                    }
                }
            }

            nextSequence = streamSequence;
            return new ProviderStreamDecision(true, string.Empty);
        }

        public ProviderStreamDecision CompleteAtEndOfStream()
        {
            return state == ProviderStreamState.Terminal ? new ProviderStreamDecision(true, string.Empty) : Reject("stream_incomplete");
        }

        private static ProviderStreamDecision Reject(string errorCode) => new ProviderStreamDecision(false, errorCode);
    }

    public sealed class ProviderRequestContract
    {
        internal ProviderRequestContract(string messageType, string requiredCapability, string requestSchema, string outputSchema, int outputSchemaMajor, int outputSchemaMinor, bool requiresTaskScope)
        {
            MessageType = messageType;
            RequiredCapability = requiredCapability;
            RequestSchema = requestSchema;
            OutputSchema = outputSchema;
            OutputSchemaMajor = outputSchemaMajor;
            OutputSchemaMinor = outputSchemaMinor;
            RequiresTaskScope = requiresTaskScope;
        }

        public string MessageType { get; }
        public string RequiredCapability { get; }
        public string RequestSchema { get; }
        public string OutputSchema { get; }
        public int OutputSchemaMajor { get; }
        public int OutputSchemaMinor { get; }
        public bool RequiresTaskScope { get; }
    }

    public static class ProviderProtocolContract
    {
        private static readonly IReadOnlyDictionary<string, ProviderRequestContract> Requests;
        private static readonly IReadOnlyList<string> WireErrorCategoryValues;

        static ProviderProtocolContract()
        {
            var requests = new Dictionary<string, ProviderRequestContract>(StringComparer.Ordinal)
            {
                [ProtocolConstants.MessageTypeProviderProfileUpsertV1] = new ProviderRequestContract(ProtocolConstants.MessageTypeProviderProfileUpsertV1, ProtocolConstants.CapabilityProviderConfigureV1, ProtocolConstants.ProviderProfileUpsertSchemaV1, ProtocolConstants.ProviderProfileResultSchemaV1, 1, 0, true),
                [ProtocolConstants.MessageTypeProviderCredentialUpsertV1] = new ProviderRequestContract(ProtocolConstants.MessageTypeProviderCredentialUpsertV1, ProtocolConstants.CapabilityProviderCredentialsV1, ProtocolConstants.ProviderCredentialUpsertSchemaV1, ProtocolConstants.ProviderCredentialResultSchemaV1, 1, 0, true),
                [ProtocolConstants.MessageTypeProviderProfileRemoveV1] = new ProviderRequestContract(ProtocolConstants.MessageTypeProviderProfileRemoveV1, ProtocolConstants.CapabilityProviderConfigureV1, ProtocolConstants.ProviderProfileRemoveSchemaV1, ProtocolConstants.ProviderProfileResultSchemaV1, 1, 0, true),
                [ProtocolConstants.MessageTypeProviderModelsV1] = new ProviderRequestContract(ProtocolConstants.MessageTypeProviderModelsV1, ProtocolConstants.CapabilityProviderModelsV1, ProtocolConstants.ProviderModelsSchemaV1, ProtocolConstants.ProviderModelsResultSchemaV1, 1, 0, true),
                [ProtocolConstants.MessageTypeProviderCompleteV1] = new ProviderRequestContract(ProtocolConstants.MessageTypeProviderCompleteV1, ProtocolConstants.CapabilityProviderCompleteV1, ProtocolConstants.ProviderCompleteSchemaV1, ProtocolConstants.ProviderResultSchemaV1, 1, 0, true),
                [ProtocolConstants.MessageTypeProviderStreamV1] = new ProviderRequestContract(ProtocolConstants.MessageTypeProviderStreamV1, ProtocolConstants.CapabilityProviderStreamV1, ProtocolConstants.ProviderStreamSchemaV1, ProtocolConstants.ProviderStreamEventSchemaV1, 1, 0, true),
                [ProtocolConstants.MessageTypeProviderImageV1] = new ProviderRequestContract(ProtocolConstants.MessageTypeProviderImageV1, ProtocolConstants.CapabilityProviderImageV1, ProtocolConstants.ProviderImageSchemaV1, ProtocolConstants.ProviderImageResultSchemaV1, 1, 0, true)
            };
            Requests = new ReadOnlyDictionary<string, ProviderRequestContract>(requests);
            WireErrorCategoryValues = new ReadOnlyCollection<string>(new[]
            {
                "invalid_request",
                "authentication",
                "forbidden",
                "not_found",
                "conflict",
                "rate_limited",
                "timeout",
                "unavailable",
                "server_unavailable",
                "transport_unavailable",
                "redirect_rejected",
                "policy_denied",
                "malformed_response",
                "incomplete_stream",
                "cancelled",
                "unsupported",
                "resource_exhausted",
                "corrupt_credential",
                "internal_failure"
            });
        }

        public static IReadOnlyList<string> WireErrorCategories => WireErrorCategoryValues;

        public static bool TryGetRequest(string messageType, out ProviderRequestContract contract)
        {
            if (messageType != null && Requests.TryGetValue(messageType, out contract)) return true;
            contract = null;
            return false;
        }

        public static bool IsProviderRequest(string messageType)
        {
            return TryGetRequest(messageType, out _);
        }

        public static bool IsProviderResponse(string messageType)
        {
            return StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeProviderProfileResult)
                || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeProviderCredentialResult)
                || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeProviderModelsResult)
                || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeProviderResult)
                || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeProviderStreamEvent)
                || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeProviderImageResult);
        }

        public static bool IsKnownProviderErrorCategory(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (var index = 0; index < WireErrorCategoryValues.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(WireErrorCategoryValues[index], value)) return true;
            }

            return false;
        }

        public static bool IsKnownStreamEventKind(string value)
        {
            return StringComparer.Ordinal.Equals(value, "started")
                || StringComparer.Ordinal.Equals(value, "text_delta")
                || StringComparer.Ordinal.Equals(value, "usage_update")
                || StringComparer.Ordinal.Equals(value, "route_changed")
                || StringComparer.Ordinal.Equals(value, "completed")
                || StringComparer.Ordinal.Equals(value, "cancelled")
                || StringComparer.Ordinal.Equals(value, "failed");
        }

        public static bool IsKnownCoreErrorCategory(string value)
        {
            return StringComparer.Ordinal.Equals(value, "invalid_request")
                || StringComparer.Ordinal.Equals(value, "incompatible")
                || StringComparer.Ordinal.Equals(value, "unsupported")
                || StringComparer.Ordinal.Equals(value, "unavailable")
                || StringComparer.Ordinal.Equals(value, "denied")
                || StringComparer.Ordinal.Equals(value, "not_found")
                || StringComparer.Ordinal.Equals(value, "conflict")
                || StringComparer.Ordinal.Equals(value, "expired")
                || StringComparer.Ordinal.Equals(value, "rate_limited")
                || StringComparer.Ordinal.Equals(value, "provider_failure")
                || StringComparer.Ordinal.Equals(value, "timeout")
                || StringComparer.Ordinal.Equals(value, "cancelled")
                || StringComparer.Ordinal.Equals(value, "resource_exhausted")
                || StringComparer.Ordinal.Equals(value, "recovery_required")
                || StringComparer.Ordinal.Equals(value, "internal_failure");
        }

        public static bool IsValidUsage(int inputTokens, int outputTokens)
        {
            return inputTokens >= 0 && inputTokens <= 16777216
                && outputTokens >= 0 && outputTokens <= 16777216
                && (long)inputTokens + outputTokens <= 33554432;
        }
    }
}
