using System;
using System.Collections.Generic;

namespace MarcusAwakeTransport
{
    public static class ProtocolValidation
    {
        public static ProtocolDecision ValidateProtocolIdentity(string protocolId, int protocolMajor, int protocolMinor, int frameworkApiMajor, string serviceId)
        {
            if (string.IsNullOrWhiteSpace(protocolId)) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "protocol_id");
            if (!StringComparer.Ordinal.Equals(protocolId, ProtocolConstants.ProtocolId))
            {
                var legacy = protocolId.IndexOf("marcus", StringComparison.OrdinalIgnoreCase) >= 0 || protocolId.IndexOf("framework", StringComparison.OrdinalIgnoreCase) >= 0 || protocolId.IndexOf("companion", StringComparison.OrdinalIgnoreCase) >= 0;
                return ProtocolDecision.Rejected(legacy ? ProtocolErrorCode.LegacyProtocolRejected : ProtocolErrorCode.ProtocolIdMismatch, "protocol_id");
            }
            if (protocolMajor != ProtocolConstants.ProtocolMajor) return ProtocolDecision.Rejected(ProtocolErrorCode.ProtocolMajorMismatch, "protocol_major");
            if (protocolMinor != ProtocolConstants.ProtocolMinor) return ProtocolDecision.Rejected(ProtocolErrorCode.ProtocolMinorMismatch, "protocol_minor");
            if (frameworkApiMajor != ProtocolConstants.FrameworkApiMajor) return ProtocolDecision.Rejected(ProtocolErrorCode.FrameworkApiMajorMismatch, "framework_api_major");
            if (!StringComparer.Ordinal.Equals(serviceId, ProtocolConstants.ServiceId)) return ProtocolDecision.Rejected(ProtocolErrorCode.ServiceIdMismatch, "service_id");
            return ProtocolDecision.Accepted();
        }

        public static ProtocolDecision ValidatePipeName(string pipeName)
        {
            if (string.IsNullOrWhiteSpace(pipeName)) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "pipe_name");
            if (!pipeName.StartsWith(ProtocolConstants.PipePrefix, StringComparison.Ordinal))
            {
                var legacy = pipeName.IndexOf("marcus", StringComparison.OrdinalIgnoreCase) >= 0 || pipeName.IndexOf("companion", StringComparison.OrdinalIgnoreCase) >= 0;
                return ProtocolDecision.Rejected(legacy ? ProtocolErrorCode.LegacyPipeRejected : ProtocolErrorCode.PipeNameInvalid, "pipe_name");
            }
            var fingerprint = pipeName.Substring(ProtocolConstants.PipePrefix.Length);
            if (fingerprint.Length < 1 || fingerprint.Length > 128) return ProtocolDecision.Rejected(ProtocolErrorCode.PipeNameInvalid, "pipe_name");
            for (var index = 0; index < fingerprint.Length; index++)
            {
                var character = fingerprint[index];
                if (character <= 0x20 || character == '\\' || character == '/' || character == ':' || character == '"') return ProtocolDecision.Rejected(ProtocolErrorCode.PipeNameInvalid, "pipe_name");
            }
            return ProtocolDecision.Accepted();
        }

        public static ProtocolDecision ValidatePipeName(string pipeName, string expectedSidFingerprint)
        {
            var decision = ValidatePipeName(pipeName);
            if (!decision.IsAccepted) return decision;
            if (string.IsNullOrWhiteSpace(expectedSidFingerprint) || !StringComparer.Ordinal.Equals(pipeName, ProtocolConstants.PipePrefix + expectedSidFingerprint)) return ProtocolDecision.Rejected(ProtocolErrorCode.PipeNameInvalid, "pipe_name");
            return ProtocolDecision.Accepted();
        }

        public static string BuildPipeName(string userSidFingerprint)
        {
            if (string.IsNullOrWhiteSpace(userSidFingerprint)) throw new ArgumentException("user_sid_fingerprint_required", nameof(userSidFingerprint));
            var pipeName = ProtocolConstants.PipePrefix + userSidFingerprint;
            var decision = ValidatePipeName(pipeName);
            if (!decision.IsAccepted) throw new ArgumentException(decision.ErrorCode, nameof(userSidFingerprint));
            return pipeName;
        }

        public static ProtocolDecision ValidateBootstrap(BootstrapDescriptor value)
        {
            if (value == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "bootstrap");
            var identity = ValidateProtocolIdentity(value.ProtocolId, value.ProtocolMajor, value.ProtocolMinor, value.FrameworkApiMajor, value.ServiceId);
            if (!identity.IsAccepted) return identity;
            var pipe = ValidatePipeName(value.PipeName, value.UserSidFingerprint);
            if (!pipe.IsAccepted) return pipe;
            if (!Required(value.BannerlordApiVersion)) return Missing("bannerlord_api_version");
            if (!Required(value.ClientInstanceId)) return Missing("client_instance_id");
            if (!Required(value.LaunchTransactionId)) return Missing("launch_transaction_id");
            if (!Required(value.LaunchNonce)) return Missing("launch_nonce");
            if (!Required(value.ChallengeId)) return Missing("challenge_id");
            if (!Required(value.SessionNonce)) return Missing("session_nonce");
            if (!Required(value.UserSidFingerprint)) return Missing("user_sid_fingerprint");
            if (value.ParentProcessId < 1 || value.ParentStartUnixMilliseconds < 1 || value.InstanceEpoch < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidEpoch, "bootstrap_epoch");
            if (!Required(value.AuthKeyId)) return Missing("auth_key_id");
            if (!Required(value.ExpectedServiceArtifactSha256)) return Missing("expected_service_artifact_sha256");
            if (!Required(value.ClientBootstrapProof)) return Missing("client_bootstrap_proof");
            return ValidateCapabilities(value.RequestedCapabilities, "requested_capabilities");
        }

        public static ProtocolDecision ValidateServiceReady(ServiceReadyDescriptor value)
        {
            if (value == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "service_ready");
            var identity = ValidateProtocolIdentity(value.ProtocolId, value.ProtocolMajor, value.ProtocolMinor, value.FrameworkApiMajor, value.ServiceId);
            if (!identity.IsAccepted) return identity;
            var pipe = ValidatePipeName(value.PipeName, value.UserSidFingerprint);
            if (!pipe.IsAccepted) return pipe;
            if (!Required(value.ServiceInstanceId)) return Missing("service_instance_id");
            if (value.ServiceProcessId < 1 || value.ServiceStartUnixMilliseconds < 1 || value.InstanceEpoch < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidEpoch, "service_epoch");
            if (!Required(value.ServiceArtifactSha256)) return Missing("service_artifact_sha256");
            if (!Required(value.ParentStartProof)) return Missing("parent_start_proof");
            if (!Required(value.ServiceBootstrapProof)) return Missing("service_bootstrap_proof");
            return ProtocolDecision.Accepted();
        }

        public static ProtocolDecision ValidateHandshakeRequest(HandshakeRequest value)
        {
            if (value == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "handshake_request");
            var identity = ValidateProtocolIdentity(value.ProtocolId, value.ProtocolMajor, value.ProtocolMinor, value.FrameworkApiMajor, value.ServiceId);
            if (!identity.IsAccepted) return identity;
            if (!Required(value.ClientInstanceId)) return Missing("client_instance_id");
            if (!Required(value.ServiceInstanceId)) return Missing("service_instance_id");
            if (!Required(value.BannerlordApiVersion)) return Missing("bannerlord_api_version");
            if (!Required(value.SessionNonce)) return Missing("session_nonce");
            if (!Required(value.UserSidFingerprint)) return Missing("user_sid_fingerprint");
            if (!Required(value.LaunchTransactionId)) return Missing("launch_transaction_id");
            if (!Required(value.LaunchNonce)) return Missing("launch_nonce");
            if (!Required(value.ChallengeId)) return Missing("challenge_id");
            if (!Required(value.AuthKeyId)) return Missing("auth_key_id");
            if (!Required(value.PipeName)) return Missing("pipe_name");
            var pipe = ValidatePipeName(value.PipeName, value.UserSidFingerprint);
            if (!pipe.IsAccepted) return pipe;
            if (!Required(value.ClientBootstrapProof)) return Missing("client_bootstrap_proof");
            if (!Required(value.ServiceBootstrapProof)) return Missing("service_bootstrap_proof");
            if (!Required(value.ChallengeResponse)) return Missing("challenge_response");
            if (!Required(value.SessionId)) return Missing("session_id");
            if (value.ParentProcessId < 1 || value.ParentStartUnixMilliseconds < 1 || value.InstanceEpoch < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidEpoch, "handshake_epoch");
            var frameBound = ValidateFrameBound(value.MaxFrameBytes);
            if (!frameBound.IsAccepted) return frameBound;
            var checksum = ValidateChecksumAlgorithm(value.ChecksumAlgorithm);
            if (!checksum.IsAccepted) return checksum;
            if (!Required(value.CorrelationId)) return Missing("correlation_id");
            return ValidateCapabilities(value.RequestedCapabilities, "requested_capabilities");
        }

        public static ProtocolDecision ValidateHandshakeResponse(HandshakeResponse value)
        {
            if (value == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "handshake_response");
            var identity = ValidateProtocolIdentity(value.ProtocolId, value.ProtocolMajor, value.ProtocolMinor, value.FrameworkApiMajor, value.ServiceId);
            if (!identity.IsAccepted) return identity;
            if (!Required(value.ServiceInstanceId)) return Missing("service_instance_id");
            if (!Required(value.UserSidFingerprint)) return Missing("user_sid_fingerprint");
            if (value.ParentProcessId < 1 || value.InstanceEpoch < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidEpoch, "handshake_epoch");
            var frameBound = ValidateFrameBound(value.MaxFrameBytes);
            if (!frameBound.IsAccepted) return frameBound;
            var checksum = ValidateChecksumAlgorithm(value.ChecksumAlgorithm);
            if (!checksum.IsAccepted) return checksum;
            if (!Required(value.CorrelationId)) return Missing("correlation_id");
            var ack = ValidateAckStatus(value.AckStatus, false);
            if (!ack.IsAccepted) return ack;
            var outcome = ValidateOutcome(value.OutcomeKind, value.NonDurable, value.AckStatus);
            if (!outcome.IsAccepted) return outcome;
            if (!value.Accepted)
            {
                if (!Required(value.ErrorCode)) return Missing("error_code");
                return ValidateCapabilities(value.Capabilities, "capabilities");
            }
            if (value.ConnectionEpoch < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidEpoch, "connection_epoch");
            if (!Required(value.ClientDirectionNonce)) return Missing("client_direction_nonce");
            if (!Required(value.ServiceDirectionNonce)) return Missing("service_direction_nonce");
            if (!Required(value.ServiceBootstrapProof)) return Missing("service_bootstrap_proof");
            if (!Required(value.ParentStartProof)) return Missing("parent_start_proof");
            var streamBudget = ValidateStreamBudget(value.StreamBudget);
            if (!streamBudget.IsAccepted) return streamBudget;
            return ValidateCapabilities(value.Capabilities, "capabilities");
        }

        private static ProtocolDecision ValidateStreamBudget(ProviderStreamBudgetDescriptor value)
        {
            if (value == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "stream_budget");
            if (value.FrameCount < 2 || value.FrameCount > ProtocolConstants.MaxProviderStreamEvents) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidField, "stream_budget.frame_count");
            if (value.OutputBytes < 1 || value.OutputBytes > ProtocolConstants.MaxProviderStreamOutputBytes) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidField, "stream_budget.output_bytes");
            if (value.Tokens < 1 || value.Tokens > ProtocolConstants.MaxProviderStreamTokens) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidField, "stream_budget.tokens");
            if (value.TextDeltas < 1 || value.TextDeltas > ProtocolConstants.MaxProviderStreamTextDeltas) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidField, "stream_budget.text_deltas");
            return ProtocolDecision.Accepted();
        }

        public static ProtocolDecision ValidateSessionFence(SessionFence value)
        {
            if (value == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingSessionFence, "session_fence");
            if (!Required(value.CampaignGuid) || !Required(value.TimelineId) || !Required(value.SessionId)) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidSessionFence, "session_fence");
            if (value.SessionGeneration < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidSessionFence, "session_generation");
            return ProtocolDecision.Accepted();
        }

        public static ProtocolDecision ValidateTaskScope(TaskScopeEnvelope value)
        {
            if (value == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingTaskScope, "task_scope");
            if (!value.IsComplete) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidTaskScope, "task_scope");
            return ProtocolDecision.Accepted();
        }

        public static ProtocolDecision ValidateEnvelope(PipeEnvelope value)
        {
            if (value == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "envelope");
            string payload;
            try
            {
                payload = StrictJson.RenderPayload(StrictJson.ParsePayload(string.IsNullOrWhiteSpace(value.PayloadJson) ? "{}" : value.PayloadJson));
            }
            catch (StrictJsonException)
            {
                return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidPayload, "payload");
            }
            var payloadBytes = StrictJson.Utf8.GetBytes(payload);
            return ValidateEnvelopeCore(value, payloadBytes.Length, TransportSecurity.Sha256Hex(payloadBytes), false);
        }

        public static ProtocolDecision ValidateEnvelopeHeaderOnly(PipeEnvelope value)
        {
            if (value == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "envelope");
            var identity = ValidateProtocolIdentity(value.ProtocolId, value.ProtocolMajor, value.ProtocolMinor, ProtocolConstants.FrameworkApiMajor, ProtocolConstants.ServiceId);
            if (!identity.IsAccepted) return identity;
            var checksumAlgorithm = ValidateChecksumAlgorithm(value.ChecksumAlgorithm);
            if (!checksumAlgorithm.IsAccepted) return checksumAlgorithm;
            if (!Required(value.MessageType) || !Required(value.MessageId) || !Required(value.CorrelationId) || !Required(value.DirectionNonce) || !Required(value.PayloadSchema)) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "envelope");
            if (value.InstanceEpoch < 1 || value.ConnectionEpoch < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidEpoch, "envelope_epoch");
            if (value.Sequence < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidSequence, "sequence");
            if (value.DeadlineUnixMilliseconds < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidDeadline, "deadline_unix_milliseconds");
            var messageType = ValidateMessageType(value.MessageType);
            if (!messageType.IsAccepted) return messageType;
            var ack = ValidateAckStatus(value.AckStatus, IsResponseMessageType(value.MessageType));
            if (!ack.IsAccepted) return ack;
            var outcome = ValidateOutcome(value.OutcomeKind, value.NonDurable, value.AckStatus);
            if (!outcome.IsAccepted) return outcome;
            var session = new SessionFence
            {
                CampaignGuid = value.CampaignGuid,
                TimelineId = value.TimelineId,
                SessionId = value.SessionId,
                SessionGeneration = value.SessionGeneration
            };
            var hasSessionFields = !string.IsNullOrWhiteSpace(value.CampaignGuid) || !string.IsNullOrWhiteSpace(value.TimelineId) || !string.IsNullOrWhiteSpace(value.SessionId) || value.SessionGeneration != 0;
            if (!IsHealthMessageType(value.MessageType))
            {
                if (!hasSessionFields) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingSessionFence, "session_fence");
                var sessionDecision = ValidateSessionFence(session);
                if (!sessionDecision.IsAccepted) return sessionDecision;
                if (!Required(value.OwnerId)) return Missing("owner_id");
            }
            else if (hasSessionFields)
            {
                var sessionDecision = ValidateSessionFence(session);
                if (!sessionDecision.IsAccepted) return sessionDecision;
            }

            var taskRequired = IsTaskMessageType(value.MessageType);
            if (taskRequired && value.TaskScope == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingTaskScope, "task_scope");
            if (value.TaskScope != null)
            {
                var taskDecision = ValidateTaskScope(value.TaskScope);
                if (!taskDecision.IsAccepted) return taskDecision;
                if (!StringComparer.Ordinal.Equals(value.TaskScope.MessageId, value.MessageId)) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidTaskScope, "task_scope.message_id");
            }
            if (IsHealthMessageType(value.MessageType) && value.TaskScope != null) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidTaskScope, "task_scope");
            return ProtocolDecision.Accepted();
        }

        internal static ProtocolDecision ValidateEnvelopeForSerialization(PipeEnvelope value, int payloadLength, string payloadHash)
        {
            return ValidateEnvelopeCore(value, payloadLength, payloadHash, true);
        }

        private static ProtocolDecision ValidateEnvelopeCore(PipeEnvelope value, int computedPayloadLength, string computedPayloadHash, bool allowUncomputedIntegrity)
        {
            var identity = ValidateProtocolIdentity(value.ProtocolId, value.ProtocolMajor, value.ProtocolMinor, ProtocolConstants.FrameworkApiMajor, ProtocolConstants.ServiceId);
            if (!identity.IsAccepted) return identity;
            var checksumAlgorithm = ValidateChecksumAlgorithm(value.ChecksumAlgorithm);
            if (!checksumAlgorithm.IsAccepted) return checksumAlgorithm;
            if (!Required(value.MessageType) || !Required(value.MessageId) || !Required(value.CorrelationId) || !Required(value.DirectionNonce) || !Required(value.PayloadSchema)) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, "envelope");
            if (value.InstanceEpoch < 1 || value.ConnectionEpoch < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidEpoch, "envelope_epoch");
            if (value.Sequence < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidSequence, "sequence");
            if (value.DeadlineUnixMilliseconds < 1) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidDeadline, "deadline_unix_milliseconds");
            var messageType = ValidateMessageType(value.MessageType);
            if (!messageType.IsAccepted) return messageType;
            var ack = ValidateAckStatus(value.AckStatus, IsResponseMessageType(value.MessageType));
            if (!ack.IsAccepted) return ack;
            var outcome = ValidateOutcome(value.OutcomeKind, value.NonDurable, value.AckStatus);
            if (!outcome.IsAccepted) return outcome;
            if (computedPayloadLength < 1 || computedPayloadLength > ProtocolConstants.MaxPayloadBytes) return ProtocolDecision.Rejected(ProtocolErrorCode.PayloadLengthInvalid, "payload_length");
            if (!allowUncomputedIntegrity || value.PayloadLength != 0)
            {
                if (value.PayloadLength != computedPayloadLength) return ProtocolDecision.Rejected(ProtocolErrorCode.PayloadLengthInvalid, "payload_length");
            }
            if (!allowUncomputedIntegrity || !string.IsNullOrWhiteSpace(value.PayloadSha256))
            {
                if (!StringComparer.Ordinal.Equals(value.PayloadSha256, computedPayloadHash)) return ProtocolDecision.Rejected(ProtocolErrorCode.PayloadHashMismatch, "payload_sha256");
            }
            if (!allowUncomputedIntegrity || !string.IsNullOrWhiteSpace(value.Checksum))
            {
                if (!StringComparer.Ordinal.Equals(value.Checksum, computedPayloadHash)) return ProtocolDecision.Rejected(ProtocolErrorCode.ChecksumMismatch, "checksum");
            }

            var session = new SessionFence
            {
                CampaignGuid = value.CampaignGuid,
                TimelineId = value.TimelineId,
                SessionId = value.SessionId,
                SessionGeneration = value.SessionGeneration
            };
            var hasSessionFields = !string.IsNullOrWhiteSpace(value.CampaignGuid) || !string.IsNullOrWhiteSpace(value.TimelineId) || !string.IsNullOrWhiteSpace(value.SessionId) || value.SessionGeneration != 0;
            if (!IsHealthMessageType(value.MessageType))
            {
                if (!hasSessionFields) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingSessionFence, "session_fence");
                var sessionDecision = ValidateSessionFence(session);
                if (!sessionDecision.IsAccepted) return sessionDecision;
                if (!Required(value.OwnerId)) return Missing("owner_id");
            }
            else if (hasSessionFields)
            {
                var sessionDecision = ValidateSessionFence(session);
                if (!sessionDecision.IsAccepted) return sessionDecision;
            }

            var taskRequired = IsTaskMessageType(value.MessageType);
            if (taskRequired && value.TaskScope == null) return ProtocolDecision.Rejected(ProtocolErrorCode.MissingTaskScope, "task_scope");
            if (value.TaskScope != null)
            {
                var taskDecision = ValidateTaskScope(value.TaskScope);
                if (!taskDecision.IsAccepted) return taskDecision;
                if (!StringComparer.Ordinal.Equals(value.TaskScope.MessageId, value.MessageId)) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidTaskScope, "task_scope.message_id");
            }
            if (IsHealthMessageType(value.MessageType) && value.TaskScope != null) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidTaskScope, "task_scope");
            return ProtocolDecision.Accepted();
        }

        private static bool IsHealthMessageType(string value)
        {
            return StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeHealth) || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeHealthAck);
        }

        private static bool IsTaskMessageType(string value)
        {
            return StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeEcho)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeCancel)
                || ProviderProtocolContract.IsProviderRequest(value)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageKvGet)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageKvSet)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageKvDelete)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageTimelineAppend)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageTimelineRead)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeRagIngest)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeRagSearch);
        }

        private static bool IsResponseMessageType(string value)
        {
            return ProviderProtocolContract.IsProviderResponse(value)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeHealthAck)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeDiagnosticAck)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeEchoResult)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeCancelAck)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeShutdownAck)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageResult)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeRagResult)
                || StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeError);
        }

        private static ProtocolDecision ValidateCapabilities(string[] values, string field)
        {
            if (values == null || values.Length > ProtocolConstants.MaxJsonArrayItems) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidField, field);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < values.Length; index++)
            {
                if (!Required(values[index]) || !seen.Add(values[index])) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidField, field);
            }
            return ProtocolDecision.Accepted();
        }

        private static ProtocolDecision ValidateFrameBound(int value)
        {
            if (value < 1 || value > ProtocolConstants.MaxFrameBytes) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidField, "max_frame_bytes");
            return ProtocolDecision.Accepted();
        }

        private static ProtocolDecision ValidateChecksumAlgorithm(string value)
        {
            if (!StringComparer.Ordinal.Equals(value, ProtocolConstants.ChecksumAlgorithm)) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidChecksumAlgorithm, "checksum_algorithm");
            return ProtocolDecision.Accepted();
        }

        private static ProtocolDecision ValidateMessageType(string value)
        {
            if (ProviderProtocolContract.IsProviderRequest(value)) return ProtocolDecision.Accepted();
            if (ProviderProtocolContract.IsProviderResponse(value)) return ProtocolDecision.Accepted();
            if (StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeHealth) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeEcho) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeCancel) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeDiagnostic) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeShutdown) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeHealthAck) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeDiagnosticAck) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeEchoResult) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeCancelAck) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeShutdownAck) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageKvGet) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageKvSet) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageKvDelete) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageTimelineAppend) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageTimelineRead) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeRagIngest) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeRagSearch) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeStorageResult) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeRagResult) ||
                StringComparer.Ordinal.Equals(value, ProtocolConstants.MessageTypeError)) return ProtocolDecision.Accepted();
            return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidMessageType, "message_type");
        }

        private static ProtocolDecision ValidateAckStatus(string value, bool responseMessage)
        {
            if (string.IsNullOrWhiteSpace(value)) return ProtocolDecision.Accepted();
            if (StringComparer.Ordinal.Equals(value, ProtocolConstants.AckDurablyRecorded))
            {
                return responseMessage ? ProtocolDecision.Accepted() : ProtocolDecision.Rejected(ProtocolErrorCode.DurableAckForbidden, "ack_status");
            }
            if (!StringComparer.Ordinal.Equals(value, ProtocolConstants.AckAccepted)) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidAckStatus, "ack_status");
            return ProtocolDecision.Accepted();
        }

        private static ProtocolDecision ValidateOutcome(string value, bool nonDurable, string ackStatus)
        {
            if (string.IsNullOrWhiteSpace(value)) return ProtocolDecision.Accepted();
            if (!StringComparer.Ordinal.Equals(value, ProtocolConstants.OutcomeAccepted) && !StringComparer.Ordinal.Equals(value, ProtocolConstants.OutcomeDuplicate) && !StringComparer.Ordinal.Equals(value, ProtocolConstants.OutcomeRetryableReject) && !StringComparer.Ordinal.Equals(value, ProtocolConstants.OutcomeTerminalReplay) && !StringComparer.Ordinal.Equals(value, ProtocolConstants.OutcomeRejected)) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidOutcome, "outcome_kind");
            var durable = StringComparer.Ordinal.Equals(ackStatus, ProtocolConstants.AckDurablyRecorded);
            if ((StringComparer.Ordinal.Equals(value, ProtocolConstants.OutcomeDuplicate) || StringComparer.Ordinal.Equals(value, ProtocolConstants.OutcomeTerminalReplay)) && !nonDurable && !durable) return ProtocolDecision.Rejected(ProtocolErrorCode.NonDurableRequired, "non_durable");
            if (durable && nonDurable) return ProtocolDecision.Rejected(ProtocolErrorCode.InvalidAckStatus, "non_durable");
            return ProtocolDecision.Accepted();
        }

        private static bool Required(string value) => !string.IsNullOrWhiteSpace(value);
        private static ProtocolDecision Missing(string field) => ProtocolDecision.Rejected(ProtocolErrorCode.MissingField, field);
    }
}
