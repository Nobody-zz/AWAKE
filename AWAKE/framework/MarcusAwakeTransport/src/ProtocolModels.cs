using System;

namespace MarcusAwakeTransport
{
    public static class ProtocolConstants
    {
        public const string ProtocolId = "marcus-awake-runtime";
        public const int ProtocolMajor = 2;
        public const int ProtocolMinor = 0;
        public const int FrameworkApiMajor = 2;
        public const int MaxFrameBytes = 262144;
        public const int MaxPayloadBytes = 131072;
        public const int MaxJsonDepth = 16;
        public const int MaxJsonProperties = 256;
        public const int MaxJsonArrayItems = 256;
        public const int MaxProviderStreamEvents = 125;
        public const int MaxProviderStreamOutputBytes = 4194304;
        public const int MaxProviderStreamTokens = 32768;
        public const int MaxProviderStreamTextDeltas = 4096;
        public const int MaxSequenceWindow = 32;
        public const int FramePrefixBytes = 4;
        public const string ServiceId = "marcus-awake.runtime-service";
        public const string PipePrefix = "MarcusAwake.Runtime.v2.";
        public const string ChecksumAlgorithm = "sha256";
        public const string AckAccepted = "accepted";
        public const string AckDurablyRecorded = "durably_recorded";
        public const string OutcomeAccepted = "accepted";
        public const string OutcomeDuplicate = "duplicate";
        public const string OutcomeRetryableReject = "retryable_reject";
        public const string OutcomeTerminalReplay = "terminal_replay";
        public const string OutcomeRejected = "rejected";
        public const string MessageTypeHealth = "health";
        public const string MessageTypeEcho = "echo";
        public const string MessageTypeCancel = "cancel";
        public const string MessageTypeDiagnostic = "diagnostic";
        public const string MessageTypeShutdown = "shutdown";
        public const string MessageTypeHealthAck = "health_ack";
        public const string MessageTypeDiagnosticAck = "diagnostic_ack";
        public const string MessageTypeEchoResult = "echo_result";
        public const string MessageTypeCancelAck = "cancel_ack";
        public const string MessageTypeShutdownAck = "shutdown_ack";
        public const string MessageTypeError = "error";
        public const string CapabilityStorageRead = "storage.read";
        public const string CapabilityStorageWrite = "storage.write";
        public const string CapabilityRagRead = "rag.read";
        public const string CapabilityRagWrite = "rag.write";
        public const string CapabilityProviderConfigureV1 = "provider.configure.v1";
        public const string CapabilityProviderCredentialsV1 = "provider.credentials.v1";
        public const string CapabilityProviderModelsV1 = "provider.models.v1";
        public const string CapabilityProviderCompleteV1 = "provider.complete.v1";
        public const string CapabilityProviderStreamV1 = "provider.stream.v1";
        public const string MessageTypeProviderProfileUpsertV1 = "provider.profile_upsert.v1";
        public const string MessageTypeProviderCredentialUpsertV1 = "provider.credential_upsert.v1";
        public const string MessageTypeProviderProfileRemoveV1 = "provider.profile_remove.v1";
        public const string MessageTypeProviderModelsV1 = "provider.models.v1";
        public const string MessageTypeProviderCompleteV1 = "provider.complete.v1";
        public const string MessageTypeProviderStreamV1 = "provider.stream.v1";
        public const string MessageTypeProviderProfileResult = "provider_profile_result";
        public const string MessageTypeProviderCredentialResult = "provider_credential_result";
        public const string MessageTypeProviderModelsResult = "provider_models_result";
        public const string MessageTypeProviderResult = "provider_result";
        public const string MessageTypeProviderStreamEvent = "provider_stream_event";
        public const string ProviderProfileUpsertSchemaV1 = "marcus-awake.provider.profile_upsert.v1";
        public const string ProviderCredentialUpsertSchemaV1 = "marcus-awake.provider.credential_upsert.v1";
        public const string ProviderProfileRemoveSchemaV1 = "marcus-awake.provider.profile_remove.v1";
        public const string ProviderModelsSchemaV1 = "marcus-awake.provider.models.v1";
        public const string ProviderCompleteSchemaV1 = "marcus-awake.provider.complete.v1";
        public const string ProviderStreamSchemaV1 = "marcus-awake.provider.stream.v1";
        public const string ProviderResultSchemaV1 = "marcus-awake.provider.result.v1";
        public const string ProviderProfileResultSchemaV1 = "marcus-awake.provider.profile_result.v1";
        public const string ProviderCredentialResultSchemaV1 = "marcus-awake.provider.credential_result.v1";
        public const string ProviderModelsResultSchemaV1 = "marcus-awake.provider.models_result.v1";
        public const string ProviderStreamEventSchemaV1 = "marcus-awake.provider.stream_event.v1";
        public const string ProviderErrorSchemaV1 = "marcus-awake.provider.error.v1";
        public const string GenericErrorSchemaV1 = "marcus-awake.error.v1";
        public const string MessageTypeStorageKvGet = "storage.kv_get";
        public const string MessageTypeStorageKvSet = "storage.kv_set";
        public const string MessageTypeStorageKvDelete = "storage.kv_delete";
        public const string MessageTypeStorageTimelineAppend = "storage.timeline_append";
        public const string MessageTypeStorageTimelineRead = "storage.timeline_read";
        public const string MessageTypeRagIngest = "rag.ingest";
        public const string MessageTypeRagSearch = "rag.search";
        public const string MessageTypeStorageResult = "storage_result";
        public const string MessageTypeRagResult = "rag_result";
    }

    public sealed class BootstrapDescriptor
    {
        public string ProtocolId { get; set; } = ProtocolConstants.ProtocolId;
        public int ProtocolMajor { get; set; } = ProtocolConstants.ProtocolMajor;
        public int ProtocolMinor { get; set; } = ProtocolConstants.ProtocolMinor;
        public int FrameworkApiMajor { get; set; } = ProtocolConstants.FrameworkApiMajor;
        public string BannerlordApiVersion { get; set; } = string.Empty;
        public string ServiceId { get; set; } = ProtocolConstants.ServiceId;
        public string ClientInstanceId { get; set; } = string.Empty;
        public string LaunchTransactionId { get; set; } = string.Empty;
        public string LaunchNonce { get; set; } = string.Empty;
        public string ChallengeId { get; set; } = string.Empty;
        public string SessionNonce { get; set; } = string.Empty;
        public string UserSidFingerprint { get; set; } = string.Empty;
        public int ParentProcessId { get; set; }
        public long ParentStartUnixMilliseconds { get; set; }
        public long InstanceEpoch { get; set; }
        public string AuthKeyId { get; set; } = "bootstrap-v1";
        public string ExpectedServiceArtifactSha256 { get; set; } = string.Empty;
        public string PipeName { get; set; } = string.Empty;
        public string ClientBootstrapProof { get; set; } = string.Empty;
        public string[] RequestedCapabilities { get; set; } = Array.Empty<string>();
    }

    public sealed class ServiceReadyDescriptor
    {
        public string ProtocolId { get; set; } = ProtocolConstants.ProtocolId;
        public int ProtocolMajor { get; set; } = ProtocolConstants.ProtocolMajor;
        public int ProtocolMinor { get; set; } = ProtocolConstants.ProtocolMinor;
        public int FrameworkApiMajor { get; set; } = ProtocolConstants.FrameworkApiMajor;
        public string ServiceId { get; set; } = ProtocolConstants.ServiceId;
        public string ServiceInstanceId { get; set; } = string.Empty;
        public int ServiceProcessId { get; set; }
        public long ServiceStartUnixMilliseconds { get; set; }
        public string ServiceArtifactSha256 { get; set; } = string.Empty;
        public string ParentStartProof { get; set; } = string.Empty;
        public string ServiceBootstrapProof { get; set; } = string.Empty;
        public string PipeName { get; set; } = string.Empty;
        public long InstanceEpoch { get; set; }
        public string UserSidFingerprint { get; set; } = string.Empty;
    }

    public sealed class HandshakeRequest
    {
        public string ProtocolId { get; set; } = ProtocolConstants.ProtocolId;
        public int ProtocolMajor { get; set; } = ProtocolConstants.ProtocolMajor;
        public int ProtocolMinor { get; set; } = ProtocolConstants.ProtocolMinor;
        public string ClientInstanceId { get; set; } = string.Empty;
        public string ServiceInstanceId { get; set; } = string.Empty;
        public string ServiceId { get; set; } = ProtocolConstants.ServiceId;
        public int FrameworkApiMajor { get; set; } = ProtocolConstants.FrameworkApiMajor;
        public string BannerlordApiVersion { get; set; } = string.Empty;
        public string[] RequestedCapabilities { get; set; } = Array.Empty<string>();
        public string SessionNonce { get; set; } = string.Empty;
        public string UserSidFingerprint { get; set; } = string.Empty;
        public int ParentProcessId { get; set; }
        public long ParentStartUnixMilliseconds { get; set; }
        public long InstanceEpoch { get; set; }
        public string LaunchTransactionId { get; set; } = string.Empty;
        public string LaunchNonce { get; set; } = string.Empty;
        public string ChallengeId { get; set; } = string.Empty;
        public string AuthKeyId { get; set; } = string.Empty;
        public string PipeName { get; set; } = string.Empty;
        public string ClientBootstrapProof { get; set; } = string.Empty;
        public string ServiceBootstrapProof { get; set; } = string.Empty;
        public string ChallengeResponse { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public int MaxFrameBytes { get; set; } = ProtocolConstants.MaxFrameBytes;
        public string ChecksumAlgorithm { get; set; } = ProtocolConstants.ChecksumAlgorithm;
        public string CorrelationId { get; set; } = "hello";
    }

    public sealed class HandshakeResponse
    {
        public bool Accepted { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ProtocolId { get; set; } = ProtocolConstants.ProtocolId;
        public int ProtocolMajor { get; set; } = ProtocolConstants.ProtocolMajor;
        public int ProtocolMinor { get; set; } = ProtocolConstants.ProtocolMinor;
        public int FrameworkApiMajor { get; set; } = ProtocolConstants.FrameworkApiMajor;
        public string ServiceId { get; set; } = ProtocolConstants.ServiceId;
        public string ServiceInstanceId { get; set; } = string.Empty;
        public string UserSidFingerprint { get; set; } = string.Empty;
        public int ParentProcessId { get; set; }
        public long InstanceEpoch { get; set; }
        public long ConnectionEpoch { get; set; }
        public string ClientDirectionNonce { get; set; } = string.Empty;
        public string ServiceDirectionNonce { get; set; } = string.Empty;
        public string ServiceBootstrapProof { get; set; } = string.Empty;
        public string ParentStartProof { get; set; } = string.Empty;
        public string[] Capabilities { get; set; } = Array.Empty<string>();
        public int MaxFrameBytes { get; set; } = ProtocolConstants.MaxFrameBytes;
        public string ChecksumAlgorithm { get; set; } = ProtocolConstants.ChecksumAlgorithm;
        public string CorrelationId { get; set; } = "hello";
        public string AckStatus { get; set; } = string.Empty;
        public string OutcomeKind { get; set; } = string.Empty;
        public bool NonDurable { get; set; }
        public ProviderStreamBudgetDescriptor StreamBudget { get; set; } = ProviderStreamBudgetDescriptor.Default();
    }

    public sealed class ProviderStreamBudgetDescriptor
    {
        public int FrameCount { get; set; } = ProtocolConstants.MaxProviderStreamEvents;
        public int OutputBytes { get; set; } = ProtocolConstants.MaxProviderStreamOutputBytes;
        public int Tokens { get; set; } = ProtocolConstants.MaxProviderStreamTokens;
        public int TextDeltas { get; set; } = ProtocolConstants.MaxProviderStreamTextDeltas;

        public static ProviderStreamBudgetDescriptor Default()
        {
            return new ProviderStreamBudgetDescriptor();
        }
    }

    public sealed class SessionFence
    {
        public string CampaignGuid { get; set; } = string.Empty;
        public string TimelineId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public long SessionGeneration { get; set; }
        public bool IsComplete => !string.IsNullOrWhiteSpace(CampaignGuid) && !string.IsNullOrWhiteSpace(TimelineId) && !string.IsNullOrWhiteSpace(SessionId) && SessionGeneration > 0;
    }

    public sealed class TaskScopeEnvelope
    {
        public string TaskId { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string IdempotencyKey { get; set; } = string.Empty;
        public string RequestPayloadHash { get; set; } = string.Empty;
        public string OutputSchemaId { get; set; } = string.Empty;
        public int OutputSchemaMajor { get; set; }
        public int OutputSchemaMinor { get; set; }
        public string SettlementRequirement { get; set; } = "not_applicable";
        public bool IsComplete => !string.IsNullOrWhiteSpace(TaskId) && !string.IsNullOrWhiteSpace(MessageId) && !string.IsNullOrWhiteSpace(OwnerId) && !string.IsNullOrWhiteSpace(RouteId) && !string.IsNullOrWhiteSpace(ProviderId) && !string.IsNullOrWhiteSpace(ProfileId) && !string.IsNullOrWhiteSpace(IdempotencyKey) && !string.IsNullOrWhiteSpace(RequestPayloadHash) && !string.IsNullOrWhiteSpace(OutputSchemaId) && OutputSchemaMajor >= 0 && OutputSchemaMinor >= 0 && (SettlementRequirement == "required" || SettlementRequirement == "not_applicable");
    }

    public sealed class PipeEnvelope
    {
        public string ProtocolId { get; set; } = ProtocolConstants.ProtocolId;
        public int ProtocolMajor { get; set; } = ProtocolConstants.ProtocolMajor;
        public int ProtocolMinor { get; set; } = ProtocolConstants.ProtocolMinor;
        public string MessageType { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string CausationId { get; set; } = string.Empty;
        public string CampaignGuid { get; set; } = string.Empty;
        public string TimelineId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public long SessionGeneration { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public long DeadlineUnixMilliseconds { get; set; }
        public long InstanceEpoch { get; set; }
        public long ConnectionEpoch { get; set; }
        public string DirectionNonce { get; set; } = string.Empty;
        public long Sequence { get; set; }
        public string FenceProof { get; set; } = string.Empty;
        public string PayloadSchema { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = "{}";
        public int PayloadLength { get; set; }
        public string PayloadSha256 { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
        public string ChecksumAlgorithm { get; set; } = ProtocolConstants.ChecksumAlgorithm;
        public string AckStatus { get; set; } = string.Empty;
        public string OutcomeKind { get; set; } = string.Empty;
        public bool NonDurable { get; set; }
        public long EventIndex { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public TaskScopeEnvelope TaskScope { get; set; }
    }

    public enum ProtocolDecisionKind
    {
        Accepted,
        Rejected
    }

    public enum ProtocolErrorCode
    {
        None,
        MissingField,
        InvalidField,
        UnknownField,
        ProtocolIdMismatch,
        ProtocolMajorMismatch,
        ProtocolMinorMismatch,
        FrameworkApiMajorMismatch,
        ServiceIdMismatch,
        LegacyProtocolRejected,
        LegacyPipeRejected,
        PipeNameInvalid,
        InvalidChecksumAlgorithm,
        InvalidMessageType,
        InvalidPayload,
        PayloadLengthInvalid,
        PayloadHashMismatch,
        ChecksumMismatch,
        InvalidSessionFence,
        MissingSessionFence,
        InvalidTaskScope,
        MissingTaskScope,
        InvalidAckStatus,
        DurableAckForbidden,
        InvalidOutcome,
        NonDurableRequired,
        InvalidEpoch,
        InvalidNonce,
        InvalidSequence,
        InvalidDeadline,
        SequenceDuplicate,
        SequenceOutOfOrder,
        SequenceGap,
        ReplayRejected,
        NonceMismatch
    }

    public sealed class ProtocolDecision
    {
        private ProtocolDecision(ProtocolDecisionKind kind, ProtocolErrorCode code, string field)
        {
            Kind = kind;
            Code = code;
            Field = field ?? string.Empty;
        }

        public ProtocolDecisionKind Kind { get; }
        public ProtocolErrorCode Code { get; }
        public string Field { get; }
        public bool IsAccepted => Kind == ProtocolDecisionKind.Accepted;
        public string ErrorCode => ProtocolErrorCodes.ToWireCode(Code);

        public static ProtocolDecision Accepted() => new ProtocolDecision(ProtocolDecisionKind.Accepted, ProtocolErrorCode.None, string.Empty);
        public static ProtocolDecision Rejected(ProtocolErrorCode code, string field = null) => new ProtocolDecision(ProtocolDecisionKind.Rejected, code, field);
    }

    public static class ProtocolErrorCodes
    {
        public static string ToWireCode(ProtocolErrorCode code)
        {
            switch (code)
            {
                case ProtocolErrorCode.None: return string.Empty;
                case ProtocolErrorCode.MissingField: return "missing_field";
                case ProtocolErrorCode.InvalidField: return "invalid_field";
                case ProtocolErrorCode.UnknownField: return "unknown_field";
                case ProtocolErrorCode.ProtocolIdMismatch: return "protocol_id_mismatch";
                case ProtocolErrorCode.ProtocolMajorMismatch: return "protocol_major_mismatch";
                case ProtocolErrorCode.ProtocolMinorMismatch: return "protocol_minor_mismatch";
                case ProtocolErrorCode.FrameworkApiMajorMismatch: return "framework_api_major_mismatch";
                case ProtocolErrorCode.ServiceIdMismatch: return "service_id_mismatch";
                case ProtocolErrorCode.LegacyProtocolRejected: return "legacy_protocol_rejected";
                case ProtocolErrorCode.LegacyPipeRejected: return "legacy_pipe_rejected";
                case ProtocolErrorCode.PipeNameInvalid: return "pipe_name_invalid";
                case ProtocolErrorCode.InvalidChecksumAlgorithm: return "invalid_checksum_algorithm";
                case ProtocolErrorCode.InvalidMessageType: return "invalid_message_type";
                case ProtocolErrorCode.InvalidPayload: return "invalid_payload";
                case ProtocolErrorCode.PayloadLengthInvalid: return "payload_length_invalid";
                case ProtocolErrorCode.PayloadHashMismatch: return "payload_hash_mismatch";
                case ProtocolErrorCode.ChecksumMismatch: return "checksum_mismatch";
                case ProtocolErrorCode.InvalidSessionFence: return "invalid_session_fence";
                case ProtocolErrorCode.MissingSessionFence: return "missing_session_fence";
                case ProtocolErrorCode.InvalidTaskScope: return "invalid_task_scope";
                case ProtocolErrorCode.MissingTaskScope: return "missing_task_scope";
                case ProtocolErrorCode.InvalidAckStatus: return "invalid_ack_status";
                case ProtocolErrorCode.DurableAckForbidden: return "durable_ack_forbidden";
                case ProtocolErrorCode.InvalidOutcome: return "invalid_outcome";
                case ProtocolErrorCode.NonDurableRequired: return "non_durable_required";
                case ProtocolErrorCode.InvalidEpoch: return "invalid_epoch";
                case ProtocolErrorCode.InvalidNonce: return "invalid_nonce";
                case ProtocolErrorCode.InvalidSequence: return "invalid_sequence";
                case ProtocolErrorCode.InvalidDeadline: return "invalid_deadline";
                case ProtocolErrorCode.SequenceDuplicate: return "sequence_duplicate";
                case ProtocolErrorCode.SequenceOutOfOrder: return "sequence_out_of_order";
                case ProtocolErrorCode.SequenceGap: return "sequence_gap";
                case ProtocolErrorCode.ReplayRejected: return "replay_rejected";
                case ProtocolErrorCode.NonceMismatch: return "nonce_mismatch";
                default: return "protocol_rejected";
            }
        }
    }

    public enum SequenceDecisionKind
    {
        Accepted,
        Duplicate,
        SequenceOutOfOrder,
        SequenceGap,
        ReplayRejected,
        NonceMismatch
    }

    public sealed class SequenceDecision
    {
        public SequenceDecision(SequenceDecisionKind kind, long sequence, string messageId, string payloadHash)
        {
            Kind = kind;
            Sequence = sequence;
            MessageId = messageId ?? string.Empty;
            PayloadHash = payloadHash ?? string.Empty;
        }

        public SequenceDecisionKind Kind { get; }
        public long Sequence { get; }
        public string MessageId { get; }
        public string PayloadHash { get; }
        public bool IsAccepted => Kind == SequenceDecisionKind.Accepted;

        public ProtocolErrorCode ErrorCode
        {
            get
            {
                switch (Kind)
                {
                    case SequenceDecisionKind.Accepted: return ProtocolErrorCode.None;
                    case SequenceDecisionKind.Duplicate: return ProtocolErrorCode.SequenceDuplicate;
                    case SequenceDecisionKind.SequenceOutOfOrder: return ProtocolErrorCode.SequenceOutOfOrder;
                    case SequenceDecisionKind.SequenceGap: return ProtocolErrorCode.SequenceGap;
                    case SequenceDecisionKind.ReplayRejected: return ProtocolErrorCode.ReplayRejected;
                    case SequenceDecisionKind.NonceMismatch: return ProtocolErrorCode.NonceMismatch;
                    default: return ProtocolErrorCode.InvalidSequence;
                }
            }
        }
    }
}
