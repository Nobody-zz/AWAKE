using System;
using System.Threading;

namespace MarcusAwakeFramework.Api
{
    public sealed class RequestContext
    {
        public RequestContext(ExtensionId caller, SessionRef session, string correlationId, DateTimeOffset deadlineUtc)
            : this(caller, CreateCompatibilityLease(session), correlationId, deadlineUtc, null)
        {
        }

        public RequestContext(ExtensionId caller, SessionRef session, long sessionGeneration, string correlationId, DateTimeOffset deadlineUtc)
            : this(caller, CreateCompatibilityLease(session, sessionGeneration), correlationId, deadlineUtc, null)
        {
        }

        public RequestContext(ExtensionId caller, SessionLease sessionLease, string correlationId, DateTimeOffset deadline)
            : this(caller, sessionLease, correlationId, deadline, null)
        {
        }

        public RequestContext(ExtensionId caller, SessionLease sessionLease, string correlationId, DateTimeOffset deadline, AiTaskScope aiTaskScope)
        {
            Caller = caller ?? throw new ArgumentNullException(nameof(caller));
            SessionLease = sessionLease ?? throw new ArgumentNullException(nameof(sessionLease));
            Session = sessionLease.Reference;
            SessionGeneration = sessionLease.Generation;
            CorrelationId = ContractGuard.Id(correlationId, nameof(correlationId));
            Deadline = deadline;
            if (aiTaskScope != null && !aiTaskScope.BelongsTo(Caller, Session, SessionGeneration, CorrelationId))
            {
                throw new ArgumentException("The AI task scope does not belong to this request context.", nameof(aiTaskScope));
            }
            AiTaskScope = aiTaskScope;
        }

        public ExtensionId Caller { get; }
        public SessionRef Session { get; }
        public long SessionGeneration { get; }
        public string CorrelationId { get; }
        public DateTimeOffset Deadline { get; }
        public DateTimeOffset DeadlineUtc => Deadline;
        public bool IsExpired => DateTimeOffset.UtcNow >= Deadline;
        public AiTaskScope AiTaskScope { get; }
        public CancellationToken CancellationToken => SessionLease.CancellationToken;
        internal SessionLease SessionLease { get; }
        public bool IsExpiredAt(DateTimeOffset now) => now >= Deadline;

        private static SessionLease CreateCompatibilityLease(SessionRef session)
        {
            SessionLease lease = new SessionLease(session ?? new SessionRef(string.Empty, string.Empty, string.Empty), 0);
            lease.MarkReady();
            return lease;
        }

        private static SessionLease CreateCompatibilityLease(SessionRef session, long sessionGeneration)
        {
            if (sessionGeneration < 1) throw new ArgumentOutOfRangeException(nameof(sessionGeneration));
            SessionLease lease = new SessionLease(session ?? new SessionRef(string.Empty, string.Empty, string.Empty), sessionGeneration);
            lease.MarkReady();
            return lease;
        }

        public RequestContext WithAiTaskScope(AiTaskScope aiTaskScope)
        {
            return new RequestContext(Caller, SessionLease, CorrelationId, Deadline, aiTaskScope);
        }
    }

    public sealed class AiTaskScope
    {
        public AiTaskScope(string taskId, string messageId, string ownerId, string campaignGuid, string timelineId, string sessionId, long sessionGeneration, string correlationId, string causationId, string routeId, string providerId, string profileId, string idempotencyKey, string requestPayloadHash, SchemaRef outputSchema, SettlementRequirement settlementRequirement)
        {
            TaskId = ContractGuard.Id(taskId, nameof(taskId));
            MessageId = ContractGuard.Id(messageId, nameof(messageId));
            OwnerId = ContractGuard.Id(ownerId, nameof(ownerId));
            CampaignGuid = ContractGuard.Id(campaignGuid, nameof(campaignGuid));
            TimelineId = ContractGuard.Id(timelineId, nameof(timelineId));
            SessionId = ContractGuard.Id(sessionId, nameof(sessionId));
            if (sessionGeneration < 1) throw new ArgumentOutOfRangeException(nameof(sessionGeneration));
            SessionGeneration = sessionGeneration;
            CorrelationId = ContractGuard.Id(correlationId, nameof(correlationId));
            CausationId = ContractGuard.Id(causationId, nameof(causationId));
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            IdempotencyKey = ContractGuard.Id(idempotencyKey, nameof(idempotencyKey));
            RequestPayloadHash = ContractGuard.Id(requestPayloadHash, nameof(requestPayloadHash));
            OutputSchema = outputSchema ?? throw new ArgumentNullException(nameof(outputSchema));
            SettlementRequirement = settlementRequirement;
            Session = new SessionRef(CampaignGuid, TimelineId, SessionId);
        }

        public AiTaskScope(string taskId, string messageId, string ownerId, SessionRef session, long sessionGeneration, string correlationId, string causationId, string routeId, string providerId, string profileId, string idempotencyKey, string requestPayloadHash, SchemaRef outputSchema, SettlementRequirement settlementRequirement)
            : this(taskId, messageId, ownerId, session == null ? null : session.CampaignGuid, session == null ? null : session.TimelineId, session == null ? null : session.SessionId, sessionGeneration, correlationId, causationId, routeId, providerId, profileId, idempotencyKey, requestPayloadHash, outputSchema, settlementRequirement)
        {
        }

        public string TaskId { get; }
        public string MessageId { get; }
        public string OwnerId { get; }
        public string CampaignGuid { get; }
        public string TimelineId { get; }
        public string SessionId { get; }
        public SessionRef Session { get; }
        public long SessionGeneration { get; }
        public string CorrelationId { get; }
        public string CausationId { get; }
        public string RouteId { get; }
        public string ProviderId { get; }
        public string ProfileId { get; }
        public string IdempotencyKey { get; }
        public string RequestPayloadHash { get; }
        public SchemaRef OutputSchema { get; }
        public SettlementRequirement SettlementRequirement { get; }

        public string IdempotencyScopeKey => string.Join("|", new[]
        {
            OwnerId,
            CampaignGuid,
            TimelineId,
            SessionId,
            RouteId,
            ProviderId,
            ProfileId,
            MessageId,
            IdempotencyKey,
            OutputSchema.ToString()
        });

        public string DeduplicationKey => IdempotencyScopeKey + "|" + RequestPayloadHash;

        public bool BelongsTo(ExtensionId caller, SessionRef session, long generation, string correlationId)
        {
            return caller != null
                && session != null
                && StringComparer.Ordinal.Equals(OwnerId, caller.Value)
                && Session.Equals(session)
                && SessionGeneration == generation
                && StringComparer.Ordinal.Equals(CorrelationId, correlationId);
        }
    }

    public sealed class IdempotencyScope
    {
        public IdempotencyScope(string ownerId, SessionRef session, string commandId, ApiVersion commandSchemaVersion, string idempotencyKey, string payloadHash)
        {
            OwnerId = ContractGuard.Id(ownerId, nameof(ownerId));
            Session = session ?? throw new ArgumentNullException(nameof(session));
            CommandId = ContractGuard.Id(commandId, nameof(commandId));
            CommandSchemaVersion = commandSchemaVersion ?? throw new ArgumentNullException(nameof(commandSchemaVersion));
            IdempotencyKey = ContractGuard.Id(idempotencyKey, nameof(idempotencyKey));
            PayloadHash = ContractGuard.Id(payloadHash, nameof(payloadHash));
        }

        public string OwnerId { get; }
        public SessionRef Session { get; }
        public string CommandId { get; }
        public ApiVersion CommandSchemaVersion { get; }
        public string IdempotencyKey { get; }
        public string PayloadHash { get; }
        public string DeduplicationKey => OwnerId + "|" + Session + "|" + CommandId + "|" + CommandSchemaVersion + "|" + IdempotencyKey;
        public string FullKey => DeduplicationKey + "|" + PayloadHash;
    }

}
