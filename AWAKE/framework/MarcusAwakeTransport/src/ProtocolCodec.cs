using System;
using System.Collections.Generic;
using System.Text;

namespace MarcusAwakeTransport
{
    public static class ProtocolCodec
    {
        private static readonly string[] BootstrapFields =
        {
            "auth_key_id", "bannerlord_api_version", "challenge_id", "client_bootstrap_proof", "client_instance_id", "expected_service_artifact_sha256", "framework_api_major", "instance_epoch", "launch_nonce", "launch_transaction_id", "parent_process_id", "parent_start_unix_milliseconds", "pipe_name", "protocol_id", "protocol_major", "protocol_minor", "requested_capabilities", "service_id", "session_nonce", "user_sid_fingerprint"
        };

        private static readonly string[] ServiceReadyFields =
        {
            "framework_api_major", "instance_epoch", "parent_start_proof", "pipe_name", "protocol_id", "protocol_major", "protocol_minor", "service_artifact_sha256", "service_bootstrap_proof", "service_id", "service_instance_id", "service_process_id", "service_start_unix_milliseconds", "user_sid_fingerprint"
        };

        private static readonly string[] HandshakeRequestFields =
        {
            "auth_key_id", "bannerlord_api_version", "challenge_id", "challenge_response", "checksum_algorithm", "client_bootstrap_proof", "client_instance_id", "correlation_id", "framework_api_major", "instance_epoch", "launch_nonce", "launch_transaction_id", "max_frame_bytes", "parent_process_id", "parent_start_unix_milliseconds", "pipe_name", "protocol_id", "protocol_major", "protocol_minor", "requested_capabilities", "service_bootstrap_proof", "service_id", "service_instance_id", "session_id", "session_nonce", "user_sid_fingerprint"
        };

        private static readonly string[] HandshakeResponseFields =
        {
            "accepted", "ack_status", "capabilities", "checksum_algorithm", "client_direction_nonce", "connection_epoch", "correlation_id", "error_code", "framework_api_major", "instance_epoch", "max_frame_bytes", "non_durable", "outcome_kind", "parent_process_id", "parent_start_proof", "protocol_id", "protocol_major", "protocol_minor", "service_bootstrap_proof", "service_direction_nonce", "service_id", "service_instance_id", "stream_budget", "user_sid_fingerprint"
        };

        private static readonly string[] EnvelopeFields =
        {
            "ack_status", "campaign_guid", "causation_id", "checksum", "checksum_algorithm", "connection_epoch", "correlation_id", "deadline_unix_milliseconds", "direction_nonce", "error_code", "event_index", "fence_proof", "instance_epoch", "message_id", "message_type", "non_durable", "outcome_kind", "owner_id", "payload", "payload_length", "payload_schema", "payload_sha256", "protocol_id", "protocol_major", "protocol_minor", "request_id", "sequence", "session_generation", "session_id", "task_scope", "timeline_id"
        };

        private static readonly string[] TaskScopeFields =
        {
            "idempotency_key", "message_id", "output_schema_id", "output_schema_major", "output_schema_minor", "owner_id", "profile_id", "provider_id", "request_payload_hash", "route_id", "settlement_requirement", "task_id"
        };

        public static string SerializeBootstrap(BootstrapDescriptor value)
        {
            Ensure(ProtocolValidation.ValidateBootstrap(value));
            return StrictJson.Render(EncodeBootstrap(value));
        }

        public static bool TryParseBootstrap(string json, out BootstrapDescriptor value, out string error)
        {
            return TryParse(json, DecodeBootstrap, ProtocolValidation.ValidateBootstrap, out value, out error);
        }

        public static string SerializeServiceReady(ServiceReadyDescriptor value)
        {
            Ensure(ProtocolValidation.ValidateServiceReady(value));
            return StrictJson.Render(EncodeServiceReady(value));
        }

        public static bool TryParseServiceReady(string json, out ServiceReadyDescriptor value, out string error)
        {
            return TryParse(json, DecodeServiceReady, ProtocolValidation.ValidateServiceReady, out value, out error);
        }

        public static string SerializeHandshakeRequest(HandshakeRequest value)
        {
            Ensure(ProtocolValidation.ValidateHandshakeRequest(value));
            return StrictJson.Render(EncodeHandshakeRequest(value));
        }

        public static bool TryParseHandshakeRequest(string json, out HandshakeRequest value, out string error)
        {
            return TryParse(json, DecodeHandshakeRequest, ProtocolValidation.ValidateHandshakeRequest, out value, out error);
        }

        public static string SerializeHandshakeResponse(HandshakeResponse value)
        {
            Ensure(ProtocolValidation.ValidateHandshakeResponse(value));
            return StrictJson.Render(EncodeHandshakeResponse(value));
        }

        public static bool TryParseHandshakeResponse(string json, out HandshakeResponse value, out string error)
        {
            return TryParse(json, DecodeHandshakeResponse, ProtocolValidation.ValidateHandshakeResponse, out value, out error);
        }

        public static string SerializeEnvelope(PipeEnvelope value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var payloadJson = CanonicalPayload(value.PayloadJson);
            var payloadBytes = StrictJson.Utf8.GetBytes(payloadJson);
            var payloadHash = TransportSecurity.Sha256Hex(payloadBytes);
            Ensure(ProtocolValidation.ValidateEnvelopeForSerialization(value, payloadBytes.Length, payloadHash));
            return StrictJson.Render(EncodeEnvelope(value, payloadJson, payloadBytes.Length, payloadHash));
        }

        public static bool TryParseEnvelope(string json, out PipeEnvelope value, out string error)
        {
            return TryParse(json, DecodeEnvelope, ProtocolValidation.ValidateEnvelope, out value, out error);
        }

        public static bool TryParseEnvelopeHeaderOnly(string json, out PipeEnvelope value, out string error)
        {
            value = null;
            if (!HeaderOnlyParser.TryParse(json, out var parsed, out error)) return false;
            value = parsed.Envelope;
            return true;
        }

        private static bool TryParse<T>(string json, Func<JsonNode, T> decoder, Func<T, ProtocolDecision> validator, out T value, out string error)
        {
            value = default(T);
            try
            {
                value = decoder(StrictJson.Parse(json));
                var decision = validator(value);
                if (!decision.IsAccepted)
                {
                    value = default(T);
                    error = decision.ErrorCode;
                    return false;
                }
                error = string.Empty;
                return true;
            }
            catch (StrictJsonException exception)
            {
                value = default(T);
                error = exception.Message;
                return false;
            }
            catch (Exception)
            {
                value = default(T);
                error = "protocol_decode_failed";
                return false;
            }
        }

        private static JsonNode EncodeBootstrap(BootstrapDescriptor value)
        {
            return StrictJson.Object(new[]
            {
                StrictJson.Pair("auth_key_id", StrictJson.String(value.AuthKeyId)),
                StrictJson.Pair("bannerlord_api_version", StrictJson.String(value.BannerlordApiVersion)),
                StrictJson.Pair("challenge_id", StrictJson.String(value.ChallengeId)),
                StrictJson.Pair("client_bootstrap_proof", StrictJson.String(value.ClientBootstrapProof)),
                StrictJson.Pair("client_instance_id", StrictJson.String(value.ClientInstanceId)),
                StrictJson.Pair("expected_service_artifact_sha256", StrictJson.String(value.ExpectedServiceArtifactSha256)),
                StrictJson.Pair("framework_api_major", StrictJson.Number(value.FrameworkApiMajor)),
                StrictJson.Pair("instance_epoch", StrictJson.Number(value.InstanceEpoch)),
                StrictJson.Pair("launch_nonce", StrictJson.String(value.LaunchNonce)),
                StrictJson.Pair("launch_transaction_id", StrictJson.String(value.LaunchTransactionId)),
                StrictJson.Pair("parent_process_id", StrictJson.Number(value.ParentProcessId)),
                StrictJson.Pair("parent_start_unix_milliseconds", StrictJson.Number(value.ParentStartUnixMilliseconds)),
                StrictJson.Pair("pipe_name", StrictJson.String(value.PipeName)),
                StrictJson.Pair("protocol_id", StrictJson.String(value.ProtocolId)),
                StrictJson.Pair("protocol_major", StrictJson.Number(value.ProtocolMajor)),
                StrictJson.Pair("protocol_minor", StrictJson.Number(value.ProtocolMinor)),
                StrictJson.Pair("requested_capabilities", StringArray(value.RequestedCapabilities)),
                StrictJson.Pair("service_id", StrictJson.String(value.ServiceId)),
                StrictJson.Pair("session_nonce", StrictJson.String(value.SessionNonce)),
                StrictJson.Pair("user_sid_fingerprint", StrictJson.String(value.UserSidFingerprint))
            });
        }

        private static BootstrapDescriptor DecodeBootstrap(JsonNode node)
        {
            StrictJson.EnsureAllowedProperties(node, BootstrapFields);
            return new BootstrapDescriptor
            {
                AuthKeyId = StrictJson.RequiredString(node, "auth_key_id"),
                BannerlordApiVersion = StrictJson.RequiredString(node, "bannerlord_api_version"),
                ChallengeId = StrictJson.RequiredString(node, "challenge_id"),
                ClientBootstrapProof = StrictJson.RequiredString(node, "client_bootstrap_proof"),
                ClientInstanceId = StrictJson.RequiredString(node, "client_instance_id"),
                ExpectedServiceArtifactSha256 = StrictJson.RequiredString(node, "expected_service_artifact_sha256"),
                FrameworkApiMajor = StrictJson.RequiredInt(node, "framework_api_major"),
                InstanceEpoch = StrictJson.RequiredLong(node, "instance_epoch"),
                LaunchNonce = StrictJson.RequiredString(node, "launch_nonce"),
                LaunchTransactionId = StrictJson.RequiredString(node, "launch_transaction_id"),
                ParentProcessId = StrictJson.RequiredInt(node, "parent_process_id"),
                ParentStartUnixMilliseconds = StrictJson.RequiredLong(node, "parent_start_unix_milliseconds"),
                PipeName = StrictJson.RequiredString(node, "pipe_name"),
                ProtocolId = StrictJson.RequiredString(node, "protocol_id"),
                ProtocolMajor = StrictJson.RequiredInt(node, "protocol_major"),
                ProtocolMinor = StrictJson.RequiredInt(node, "protocol_minor"),
                RequestedCapabilities = StrictJson.StringArray(node, "requested_capabilities", true),
                ServiceId = StrictJson.RequiredString(node, "service_id"),
                SessionNonce = StrictJson.RequiredString(node, "session_nonce"),
                UserSidFingerprint = StrictJson.RequiredString(node, "user_sid_fingerprint")
            };
        }

        private static JsonNode EncodeServiceReady(ServiceReadyDescriptor value)
        {
            return StrictJson.Object(new[]
            {
                StrictJson.Pair("framework_api_major", StrictJson.Number(value.FrameworkApiMajor)),
                StrictJson.Pair("instance_epoch", StrictJson.Number(value.InstanceEpoch)),
                StrictJson.Pair("parent_start_proof", StrictJson.String(value.ParentStartProof)),
                StrictJson.Pair("pipe_name", StrictJson.String(value.PipeName)),
                StrictJson.Pair("protocol_id", StrictJson.String(value.ProtocolId)),
                StrictJson.Pair("protocol_major", StrictJson.Number(value.ProtocolMajor)),
                StrictJson.Pair("protocol_minor", StrictJson.Number(value.ProtocolMinor)),
                StrictJson.Pair("service_artifact_sha256", StrictJson.String(value.ServiceArtifactSha256)),
                StrictJson.Pair("service_bootstrap_proof", StrictJson.String(value.ServiceBootstrapProof)),
                StrictJson.Pair("service_id", StrictJson.String(value.ServiceId)),
                StrictJson.Pair("service_instance_id", StrictJson.String(value.ServiceInstanceId)),
                StrictJson.Pair("service_process_id", StrictJson.Number(value.ServiceProcessId)),
                StrictJson.Pair("service_start_unix_milliseconds", StrictJson.Number(value.ServiceStartUnixMilliseconds)),
                StrictJson.Pair("user_sid_fingerprint", StrictJson.String(value.UserSidFingerprint))
            });
        }

        private static ServiceReadyDescriptor DecodeServiceReady(JsonNode node)
        {
            StrictJson.EnsureAllowedProperties(node, ServiceReadyFields);
            return new ServiceReadyDescriptor
            {
                FrameworkApiMajor = StrictJson.RequiredInt(node, "framework_api_major"),
                InstanceEpoch = StrictJson.RequiredLong(node, "instance_epoch"),
                ParentStartProof = StrictJson.RequiredString(node, "parent_start_proof"),
                PipeName = StrictJson.RequiredString(node, "pipe_name"),
                ProtocolId = StrictJson.RequiredString(node, "protocol_id"),
                ProtocolMajor = StrictJson.RequiredInt(node, "protocol_major"),
                ProtocolMinor = StrictJson.RequiredInt(node, "protocol_minor"),
                ServiceArtifactSha256 = StrictJson.RequiredString(node, "service_artifact_sha256"),
                ServiceBootstrapProof = StrictJson.RequiredString(node, "service_bootstrap_proof"),
                ServiceId = StrictJson.RequiredString(node, "service_id"),
                ServiceInstanceId = StrictJson.RequiredString(node, "service_instance_id"),
                ServiceProcessId = StrictJson.RequiredInt(node, "service_process_id"),
                ServiceStartUnixMilliseconds = StrictJson.RequiredLong(node, "service_start_unix_milliseconds"),
                UserSidFingerprint = StrictJson.RequiredString(node, "user_sid_fingerprint")
            };
        }

        private static JsonNode EncodeHandshakeRequest(HandshakeRequest value)
        {
            return StrictJson.Object(new[]
            {
                StrictJson.Pair("auth_key_id", StrictJson.String(value.AuthKeyId)),
                StrictJson.Pair("bannerlord_api_version", StrictJson.String(value.BannerlordApiVersion)),
                StrictJson.Pair("challenge_id", StrictJson.String(value.ChallengeId)),
                StrictJson.Pair("challenge_response", StrictJson.String(value.ChallengeResponse)),
                StrictJson.Pair("checksum_algorithm", StrictJson.String(value.ChecksumAlgorithm)),
                StrictJson.Pair("client_bootstrap_proof", StrictJson.String(value.ClientBootstrapProof)),
                StrictJson.Pair("client_instance_id", StrictJson.String(value.ClientInstanceId)),
                StrictJson.Pair("correlation_id", StrictJson.String(value.CorrelationId)),
                StrictJson.Pair("framework_api_major", StrictJson.Number(value.FrameworkApiMajor)),
                StrictJson.Pair("instance_epoch", StrictJson.Number(value.InstanceEpoch)),
                StrictJson.Pair("launch_nonce", StrictJson.String(value.LaunchNonce)),
                StrictJson.Pair("launch_transaction_id", StrictJson.String(value.LaunchTransactionId)),
                StrictJson.Pair("max_frame_bytes", StrictJson.Number(value.MaxFrameBytes)),
                StrictJson.Pair("parent_process_id", StrictJson.Number(value.ParentProcessId)),
                StrictJson.Pair("parent_start_unix_milliseconds", StrictJson.Number(value.ParentStartUnixMilliseconds)),
                StrictJson.Pair("pipe_name", StrictJson.String(value.PipeName)),
                StrictJson.Pair("protocol_id", StrictJson.String(value.ProtocolId)),
                StrictJson.Pair("protocol_major", StrictJson.Number(value.ProtocolMajor)),
                StrictJson.Pair("protocol_minor", StrictJson.Number(value.ProtocolMinor)),
                StrictJson.Pair("requested_capabilities", StringArray(value.RequestedCapabilities)),
                StrictJson.Pair("service_bootstrap_proof", StrictJson.String(value.ServiceBootstrapProof)),
                StrictJson.Pair("service_id", StrictJson.String(value.ServiceId)),
                StrictJson.Pair("service_instance_id", StrictJson.String(value.ServiceInstanceId)),
                StrictJson.Pair("session_id", StrictJson.String(value.SessionId)),
                StrictJson.Pair("session_nonce", StrictJson.String(value.SessionNonce)),
                StrictJson.Pair("user_sid_fingerprint", StrictJson.String(value.UserSidFingerprint))
            });
        }

        private static HandshakeRequest DecodeHandshakeRequest(JsonNode node)
        {
            StrictJson.EnsureAllowedProperties(node, HandshakeRequestFields);
            return new HandshakeRequest
            {
                AuthKeyId = StrictJson.RequiredString(node, "auth_key_id"),
                BannerlordApiVersion = StrictJson.RequiredString(node, "bannerlord_api_version"),
                ChallengeId = StrictJson.RequiredString(node, "challenge_id"),
                ChallengeResponse = StrictJson.RequiredString(node, "challenge_response"),
                ChecksumAlgorithm = StrictJson.RequiredString(node, "checksum_algorithm"),
                ClientBootstrapProof = StrictJson.RequiredString(node, "client_bootstrap_proof"),
                ClientInstanceId = StrictJson.RequiredString(node, "client_instance_id"),
                CorrelationId = StrictJson.RequiredString(node, "correlation_id"),
                FrameworkApiMajor = StrictJson.RequiredInt(node, "framework_api_major"),
                InstanceEpoch = StrictJson.RequiredLong(node, "instance_epoch"),
                LaunchNonce = StrictJson.RequiredString(node, "launch_nonce"),
                LaunchTransactionId = StrictJson.RequiredString(node, "launch_transaction_id"),
                MaxFrameBytes = StrictJson.RequiredInt(node, "max_frame_bytes"),
                ParentProcessId = StrictJson.RequiredInt(node, "parent_process_id"),
                ParentStartUnixMilliseconds = StrictJson.RequiredLong(node, "parent_start_unix_milliseconds"),
                PipeName = StrictJson.RequiredString(node, "pipe_name"),
                ProtocolId = StrictJson.RequiredString(node, "protocol_id"),
                ProtocolMajor = StrictJson.RequiredInt(node, "protocol_major"),
                ProtocolMinor = StrictJson.RequiredInt(node, "protocol_minor"),
                RequestedCapabilities = StrictJson.StringArray(node, "requested_capabilities", true),
                ServiceBootstrapProof = StrictJson.RequiredString(node, "service_bootstrap_proof"),
                ServiceId = StrictJson.RequiredString(node, "service_id"),
                ServiceInstanceId = StrictJson.RequiredString(node, "service_instance_id"),
                SessionId = StrictJson.RequiredString(node, "session_id"),
                SessionNonce = StrictJson.RequiredString(node, "session_nonce"),
                UserSidFingerprint = StrictJson.RequiredString(node, "user_sid_fingerprint")
            };
        }

        private static JsonNode EncodeHandshakeResponse(HandshakeResponse value)
        {
            return StrictJson.Object(new[]
            {
                StrictJson.Pair("accepted", StrictJson.Boolean(value.Accepted)),
                StrictJson.Pair("ack_status", StrictJson.String(value.AckStatus)),
                StrictJson.Pair("capabilities", StringArray(value.Capabilities)),
                StrictJson.Pair("checksum_algorithm", StrictJson.String(value.ChecksumAlgorithm)),
                StrictJson.Pair("client_direction_nonce", StrictJson.String(value.ClientDirectionNonce)),
                StrictJson.Pair("connection_epoch", StrictJson.Number(value.ConnectionEpoch)),
                StrictJson.Pair("correlation_id", StrictJson.String(value.CorrelationId)),
                StrictJson.Pair("error_code", StrictJson.String(value.ErrorCode)),
                StrictJson.Pair("framework_api_major", StrictJson.Number(value.FrameworkApiMajor)),
                StrictJson.Pair("instance_epoch", StrictJson.Number(value.InstanceEpoch)),
                StrictJson.Pair("max_frame_bytes", StrictJson.Number(value.MaxFrameBytes)),
                StrictJson.Pair("non_durable", StrictJson.Boolean(value.NonDurable)),
                StrictJson.Pair("outcome_kind", StrictJson.String(value.OutcomeKind)),
                StrictJson.Pair("parent_process_id", StrictJson.Number(value.ParentProcessId)),
                StrictJson.Pair("parent_start_proof", StrictJson.String(value.ParentStartProof)),
                StrictJson.Pair("protocol_id", StrictJson.String(value.ProtocolId)),
                StrictJson.Pair("protocol_major", StrictJson.Number(value.ProtocolMajor)),
                StrictJson.Pair("protocol_minor", StrictJson.Number(value.ProtocolMinor)),
                StrictJson.Pair("service_bootstrap_proof", StrictJson.String(value.ServiceBootstrapProof)),
                StrictJson.Pair("service_direction_nonce", StrictJson.String(value.ServiceDirectionNonce)),
                StrictJson.Pair("service_id", StrictJson.String(value.ServiceId)),
                StrictJson.Pair("service_instance_id", StrictJson.String(value.ServiceInstanceId)),
                StrictJson.Pair("stream_budget", EncodeStreamBudget(value.StreamBudget)),
                StrictJson.Pair("user_sid_fingerprint", StrictJson.String(value.UserSidFingerprint))
            });
        }

        private static JsonNode EncodeStreamBudget(ProviderStreamBudgetDescriptor value)
        {
            value = value ?? ProviderStreamBudgetDescriptor.Default();
            return StrictJson.Object(new[]
            {
                StrictJson.Pair("frame_count", StrictJson.Number(value.FrameCount)),
                StrictJson.Pair("output_bytes", StrictJson.Number(value.OutputBytes)),
                StrictJson.Pair("tokens", StrictJson.Number(value.Tokens)),
                StrictJson.Pair("text_deltas", StrictJson.Number(value.TextDeltas))
            });
        }

        private static HandshakeResponse DecodeHandshakeResponse(JsonNode node)
        {
            StrictJson.EnsureAllowedProperties(node, HandshakeResponseFields);
            return new HandshakeResponse
            {
                Accepted = StrictJson.RequiredBoolean(node, "accepted"),
                AckStatus = StrictJson.OptionalString(node, "ack_status"),
                Capabilities = StrictJson.StringArray(node, "capabilities", true),
                ChecksumAlgorithm = StrictJson.RequiredString(node, "checksum_algorithm"),
                ClientDirectionNonce = StrictJson.OptionalString(node, "client_direction_nonce"),
                ConnectionEpoch = StrictJson.OptionalLong(node, "connection_epoch"),
                CorrelationId = StrictJson.RequiredString(node, "correlation_id"),
                ErrorCode = StrictJson.OptionalString(node, "error_code"),
                FrameworkApiMajor = StrictJson.RequiredInt(node, "framework_api_major"),
                InstanceEpoch = StrictJson.RequiredLong(node, "instance_epoch"),
                MaxFrameBytes = StrictJson.RequiredInt(node, "max_frame_bytes"),
                NonDurable = StrictJson.OptionalBoolean(node, "non_durable"),
                OutcomeKind = StrictJson.OptionalString(node, "outcome_kind"),
                ParentProcessId = StrictJson.RequiredInt(node, "parent_process_id"),
                ParentStartProof = StrictJson.OptionalString(node, "parent_start_proof"),
                ProtocolId = StrictJson.RequiredString(node, "protocol_id"),
                ProtocolMajor = StrictJson.RequiredInt(node, "protocol_major"),
                ProtocolMinor = StrictJson.RequiredInt(node, "protocol_minor"),
                ServiceBootstrapProof = StrictJson.OptionalString(node, "service_bootstrap_proof"),
                ServiceDirectionNonce = StrictJson.OptionalString(node, "service_direction_nonce"),
                ServiceId = StrictJson.RequiredString(node, "service_id"),
                ServiceInstanceId = StrictJson.RequiredString(node, "service_instance_id"),
                StreamBudget = DecodeStreamBudget(StrictJson.RequiredNode(node, "stream_budget")),
                UserSidFingerprint = StrictJson.RequiredString(node, "user_sid_fingerprint")
            };
        }

        private static ProviderStreamBudgetDescriptor DecodeStreamBudget(JsonNode node)
        {
            StrictJson.EnsureAllowedProperties(node, new[] { "frame_count", "output_bytes", "tokens", "text_deltas" });
            return new ProviderStreamBudgetDescriptor
            {
                FrameCount = StrictJson.RequiredInt(node, "frame_count"),
                OutputBytes = StrictJson.RequiredInt(node, "output_bytes"),
                Tokens = StrictJson.RequiredInt(node, "tokens"),
                TextDeltas = StrictJson.RequiredInt(node, "text_deltas")
            };
        }

        private static JsonNode EncodeEnvelope(PipeEnvelope value, string payloadJson, int payloadLength, string payloadHash)
        {
            var pairs = new List<KeyValuePair<string, JsonNode>>
            {
                StrictJson.Pair("ack_status", StrictJson.String(value.AckStatus)),
                StrictJson.Pair("causation_id", StrictJson.String(value.CausationId)),
                StrictJson.Pair("checksum", StrictJson.String(payloadHash)),
                StrictJson.Pair("checksum_algorithm", StrictJson.String(value.ChecksumAlgorithm)),
                StrictJson.Pair("connection_epoch", StrictJson.Number(value.ConnectionEpoch)),
                StrictJson.Pair("correlation_id", StrictJson.String(value.CorrelationId)),
                StrictJson.Pair("deadline_unix_milliseconds", StrictJson.Number(value.DeadlineUnixMilliseconds)),
                StrictJson.Pair("direction_nonce", StrictJson.String(value.DirectionNonce)),
                StrictJson.Pair("error_code", StrictJson.String(value.ErrorCode)),
                StrictJson.Pair("event_index", StrictJson.Number(value.EventIndex)),
                StrictJson.Pair("fence_proof", StrictJson.String(value.FenceProof)),
                StrictJson.Pair("instance_epoch", StrictJson.Number(value.InstanceEpoch)),
                StrictJson.Pair("message_id", StrictJson.String(value.MessageId)),
                StrictJson.Pair("message_type", StrictJson.String(value.MessageType)),
                StrictJson.Pair("non_durable", StrictJson.Boolean(value.NonDurable)),
                StrictJson.Pair("outcome_kind", StrictJson.String(value.OutcomeKind)),
                StrictJson.Pair("payload", StrictJson.ParsePayload(payloadJson)),
                StrictJson.Pair("payload_length", StrictJson.Number(payloadLength)),
                StrictJson.Pair("payload_schema", StrictJson.String(value.PayloadSchema)),
                StrictJson.Pair("payload_sha256", StrictJson.String(payloadHash)),
                StrictJson.Pair("protocol_id", StrictJson.String(value.ProtocolId)),
                StrictJson.Pair("protocol_major", StrictJson.Number(value.ProtocolMajor)),
                StrictJson.Pair("protocol_minor", StrictJson.Number(value.ProtocolMinor)),
                StrictJson.Pair("sequence", StrictJson.Number(value.Sequence)),
                StrictJson.Pair("session_generation", StrictJson.Number(value.SessionGeneration)),
                StrictJson.Pair("session_id", StrictJson.String(value.SessionId)),
                StrictJson.Pair("timeline_id", StrictJson.String(value.TimelineId))
            };

            if (!string.IsNullOrWhiteSpace(value.CampaignGuid)) pairs.Add(StrictJson.Pair("campaign_guid", StrictJson.String(value.CampaignGuid)));
            if (!string.IsNullOrWhiteSpace(value.OwnerId)) pairs.Add(StrictJson.Pair("owner_id", StrictJson.String(value.OwnerId)));
            if (!string.IsNullOrWhiteSpace(value.RequestId)) pairs.Add(StrictJson.Pair("request_id", StrictJson.String(value.RequestId)));
            if (value.TaskScope != null) pairs.Add(StrictJson.Pair("task_scope", EncodeTaskScope(value.TaskScope)));
            return StrictJson.Object(pairs);
        }

        private static PipeEnvelope DecodeEnvelope(JsonNode node)
        {
            StrictJson.EnsureAllowedProperties(node, EnvelopeFields);
            var payload = StrictJson.RequiredNode(node, "payload");
            var payloadJson = StrictJson.RenderPayload(payload);
            var value = new PipeEnvelope
            {
                AckStatus = StrictJson.OptionalString(node, "ack_status"),
                CampaignGuid = StrictJson.OptionalString(node, "campaign_guid"),
                CausationId = StrictJson.OptionalString(node, "causation_id"),
                Checksum = StrictJson.RequiredString(node, "checksum"),
                ChecksumAlgorithm = StrictJson.RequiredString(node, "checksum_algorithm"),
                ConnectionEpoch = StrictJson.RequiredLong(node, "connection_epoch"),
                CorrelationId = StrictJson.RequiredString(node, "correlation_id"),
                DeadlineUnixMilliseconds = StrictJson.RequiredLong(node, "deadline_unix_milliseconds"),
                DirectionNonce = StrictJson.RequiredString(node, "direction_nonce"),
                ErrorCode = StrictJson.OptionalString(node, "error_code"),
                EventIndex = StrictJson.OptionalLong(node, "event_index"),
                FenceProof = StrictJson.OptionalString(node, "fence_proof"),
                InstanceEpoch = StrictJson.RequiredLong(node, "instance_epoch"),
                MessageId = StrictJson.RequiredString(node, "message_id"),
                MessageType = StrictJson.RequiredString(node, "message_type"),
                NonDurable = StrictJson.OptionalBoolean(node, "non_durable"),
                OutcomeKind = StrictJson.OptionalString(node, "outcome_kind"),
                OwnerId = StrictJson.OptionalString(node, "owner_id"),
                PayloadJson = payloadJson,
                PayloadLength = StrictJson.RequiredInt(node, "payload_length"),
                PayloadSchema = StrictJson.RequiredString(node, "payload_schema"),
                PayloadSha256 = StrictJson.RequiredString(node, "payload_sha256"),
                ProtocolId = StrictJson.RequiredString(node, "protocol_id"),
                ProtocolMajor = StrictJson.RequiredInt(node, "protocol_major"),
                ProtocolMinor = StrictJson.RequiredInt(node, "protocol_minor"),
                RequestId = StrictJson.OptionalString(node, "request_id"),
                Sequence = StrictJson.RequiredLong(node, "sequence"),
                SessionGeneration = StrictJson.OptionalLong(node, "session_generation"),
                SessionId = StrictJson.OptionalString(node, "session_id"),
                TimelineId = StrictJson.OptionalString(node, "timeline_id")
            };
            JsonNode taskScope;
            if (StrictJson.TryGet(node, "task_scope", out taskScope)) value.TaskScope = DecodeTaskScope(taskScope);
            return value;
        }

        private static JsonNode EncodeTaskScope(TaskScopeEnvelope value)
        {
            return StrictJson.Object(new[]
            {
                StrictJson.Pair("idempotency_key", StrictJson.String(value.IdempotencyKey)),
                StrictJson.Pair("message_id", StrictJson.String(value.MessageId)),
                StrictJson.Pair("output_schema_id", StrictJson.String(value.OutputSchemaId)),
                StrictJson.Pair("output_schema_major", StrictJson.Number(value.OutputSchemaMajor)),
                StrictJson.Pair("output_schema_minor", StrictJson.Number(value.OutputSchemaMinor)),
                StrictJson.Pair("owner_id", StrictJson.String(value.OwnerId)),
                StrictJson.Pair("profile_id", StrictJson.String(value.ProfileId)),
                StrictJson.Pair("provider_id", StrictJson.String(value.ProviderId)),
                StrictJson.Pair("request_payload_hash", StrictJson.String(value.RequestPayloadHash)),
                StrictJson.Pair("route_id", StrictJson.String(value.RouteId)),
                StrictJson.Pair("settlement_requirement", StrictJson.String(value.SettlementRequirement)),
                StrictJson.Pair("task_id", StrictJson.String(value.TaskId))
            });
        }

        private static TaskScopeEnvelope DecodeTaskScope(JsonNode node)
        {
            StrictJson.EnsureAllowedProperties(node, TaskScopeFields);
            return new TaskScopeEnvelope
            {
                IdempotencyKey = StrictJson.RequiredString(node, "idempotency_key"),
                MessageId = StrictJson.RequiredString(node, "message_id"),
                OutputSchemaId = StrictJson.RequiredString(node, "output_schema_id"),
                OutputSchemaMajor = StrictJson.RequiredInt(node, "output_schema_major"),
                OutputSchemaMinor = StrictJson.RequiredInt(node, "output_schema_minor"),
                OwnerId = StrictJson.RequiredString(node, "owner_id"),
                ProfileId = StrictJson.RequiredString(node, "profile_id"),
                ProviderId = StrictJson.RequiredString(node, "provider_id"),
                RequestPayloadHash = StrictJson.RequiredString(node, "request_payload_hash"),
                RouteId = StrictJson.RequiredString(node, "route_id"),
                SettlementRequirement = StrictJson.RequiredString(node, "settlement_requirement"),
                TaskId = StrictJson.RequiredString(node, "task_id")
            };
        }

        private static JsonNode StringArray(IEnumerable<string> values)
        {
            var items = new List<JsonNode>();
            if (values != null)
            {
                foreach (var value in values) items.Add(StrictJson.String(value));
            }
            return StrictJson.Array(items);
        }

        private static string CanonicalPayload(string payloadJson)
        {
            try
            {
                return StrictJson.RenderPayload(StrictJson.ParsePayload(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson));
            }
            catch (StrictJsonException exception)
            {
                throw new ArgumentException(exception.Message, nameof(payloadJson));
            }
        }

        private static void Ensure(ProtocolDecision decision)
        {
            if (!decision.IsAccepted) throw new ArgumentException(decision.ErrorCode + (string.IsNullOrWhiteSpace(decision.Field) ? string.Empty : ":" + decision.Field));
        }
    }
}
