using System;

namespace MarcusAwakeFramework.Api
{
    public sealed class ToolDescriptor
    {
        public ToolDescriptor(string toolId, string version, ExtensionId owner, SchemaRef inputSchema, string commandId, CommandRiskTier commandRisk)
        {
            ToolId = toolId ?? string.Empty;
            Version = version ?? string.Empty;
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InputSchema = inputSchema;
            CommandId = commandId ?? string.Empty;
            CommandRisk = commandRisk;
        }

        public string ToolId { get; }
        public string Version { get; }
        public ExtensionId Owner { get; }
        public SchemaRef InputSchema { get; }
        public string CommandId { get; }
        public CommandRiskTier CommandRisk { get; }
        public string QualifiedId => ToolId + "@" + Version;
    }

    public sealed class ToolCandidate
    {
        public ToolCandidate(string candidateId, string toolId, string argumentsJson, int sourceTurn, string modelRef, int candidateOrdinal = 0)
        {
            CandidateId = candidateId ?? string.Empty;
            ToolId = toolId ?? string.Empty;
            ArgumentsJson = argumentsJson ?? string.Empty;
            SourceTurn = sourceTurn;
            ModelRef = modelRef ?? string.Empty;
            CandidateOrdinal = candidateOrdinal;
        }

        public string CandidateId { get; }
        public string ToolId { get; }
        public string ArgumentsJson { get; }
        public int SourceTurn { get; }
        public string ModelRef { get; }
        public int CandidateOrdinal { get; }
    }

    public sealed class ToolCandidateValidationResult
    {
        public ToolCandidateValidationResult(bool accepted, string errorCode, ToolDescriptor descriptor, bool requiresCommandApproval)
        {
            Accepted = accepted;
            ErrorCode = errorCode ?? string.Empty;
            Descriptor = descriptor;
            RequiresCommandApproval = requiresCommandApproval;
        }

        public bool Accepted { get; }
        public string ErrorCode { get; }
        public ToolDescriptor Descriptor { get; }
        public bool RequiresCommandApproval { get; }
    }

    public enum DataAccessScope
    {
        PublicCatalog,
        PlayerKnown,
        ObservedHistory,
        FullSimulation,
        SensitiveExtension
    }

    public enum EpistemicStatus
    {
        Fact,
        Metric,
        Inference,
        Generated
    }

    public enum EventDelivery
    {
        Runtime,
        Durable
    }

    public sealed class EventEnvelope
    {
        public EventEnvelope(string eventId, SchemaRef schema, SessionRef session, long sequence, ExtensionId source, string eventKind, string correlationId, string causationId, DataAccessScope accessScope, SourceClass sourceClass, EpistemicStatus epistemicStatus, DateTimeOffset observedAtUtc, string payloadJson)
        {
            EventId = eventId ?? Guid.NewGuid().ToString("N");
            Schema = schema;
            Session = session;
            Sequence = sequence;
            Source = source;
            EventKind = eventKind ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            CausationId = causationId ?? string.Empty;
            AccessScope = accessScope;
            SourceClass = sourceClass;
            EpistemicStatus = epistemicStatus;
            ObservedAtUtc = observedAtUtc;
            PayloadJson = payloadJson ?? "{}";
        }

        public string EventId { get; }
        public SchemaRef Schema { get; }
        public SessionRef Session { get; }
        public long Sequence { get; }
        public ExtensionId Source { get; }
        public string EventKind { get; }
        public string CorrelationId { get; }
        public string CausationId { get; }
        public DataAccessScope AccessScope { get; }
        public SourceClass SourceClass { get; }
        public EpistemicStatus EpistemicStatus { get; }
        public DateTimeOffset ObservedAtUtc { get; }
        public string PayloadJson { get; }
    }

    public enum FrameworkLogLevel
    {
        Info,
        Warn,
        Error
    }
}