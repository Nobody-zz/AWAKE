using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api
{
    public enum AiTaskEventKind
    {
        Accepted,
        Started,
        TextDelta,
        UsageUpdate,
        RouteChanged,
        Completed,
        Cancelled,
        Failed
    }

    public sealed class AiTaskRequest
    {
        public AiTaskRequest(string taskId, string routeId, string inputJson, string outputSchemaId, string cloudExportClassification, DateTimeOffset deadline, string messageId, bool requiresSettlement)
            : this(taskId, messageId, routeId, "default", "default", inputJson, new SchemaRef(outputSchemaId, new ApiVersion(1, 0)), cloudExportClassification, deadline, requiresSettlement, new RuntimeResourceBudget(32768, 65536, 2048, 256), taskId)
        {
        }

        public AiTaskRequest(string taskId, string messageId, string routeId, string providerId, string profileId, string inputJson, SchemaRef outputSchema, string cloudExportClassification, DateTimeOffset deadline, bool requiresSettlement, RuntimeResourceBudget budget)
            : this(taskId, messageId, routeId, providerId, profileId, inputJson, outputSchema, cloudExportClassification, deadline, requiresSettlement, budget, taskId)
        {
        }

        public AiTaskRequest(string taskId, string messageId, string routeId, string providerId, string profileId, string inputJson, SchemaRef outputSchema, string cloudExportClassification, DateTimeOffset deadline, bool requiresSettlement, RuntimeResourceBudget budget, string idempotencyKey)
        {
            TaskId = ContractGuard.Id(taskId, nameof(taskId));
            MessageId = ContractGuard.Id(messageId, nameof(messageId));
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            ProviderId = ContractGuard.Id(providerId, nameof(providerId));
            ProfileId = ContractGuard.Id(profileId, nameof(profileId));
            InputJson = inputJson ?? "null";
            OutputSchema = outputSchema ?? throw new ArgumentNullException(nameof(outputSchema));
            CloudExportClassification = ContractGuard.Id(cloudExportClassification, nameof(cloudExportClassification));
            Deadline = deadline;
            SettlementRequirement = requiresSettlement ? SettlementRequirement.Required : SettlementRequirement.NotApplicable;
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
            IdempotencyKey = ContractGuard.Id(idempotencyKey, nameof(idempotencyKey));
        }

        public string TaskId { get; }
        public string MessageId { get; }
        public string RouteId { get; }
        public string ProviderId { get; }
        public string ProfileId { get; }
        public string InputJson { get; }
        public SchemaRef OutputSchema { get; }
        public string OutputSchemaId => OutputSchema.SchemaId;
        public string CloudExportClassification { get; }
        public DateTimeOffset Deadline { get; }
        public SettlementRequirement SettlementRequirement { get; }
        public RuntimeResourceBudget Budget { get; }
        public string IdempotencyKey { get; }
    }

    public sealed class AiTaskEvent
    {
        public AiTaskEvent(string taskId, string messageId, AiTaskEventKind kind, long sequence, string text, FrameworkError error, string resolvedModel, int inputTokens, int outputTokens)
            : this(taskId, messageId, kind, sequence, text, error, resolvedModel, inputTokens, outputTokens, string.Empty, string.Empty, string.Empty, string.Empty)
        {
        }

        public AiTaskEvent(string taskId, string messageId, AiTaskEventKind kind, long sequence, string text, FrameworkError error, string resolvedModel, int inputTokens, int outputTokens, string structuredJson, string activeProviderId, string fromProviderId, string toProviderId)
        {
            TaskId = ContractGuard.Id(taskId, nameof(taskId));
            MessageId = ContractGuard.Id(messageId, nameof(messageId));
            Kind = kind;
            Sequence = sequence;
            Text = text ?? string.Empty;
            Error = error;
            ResolvedModel = resolvedModel ?? string.Empty;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
            StructuredJson = structuredJson ?? string.Empty;
            ActiveProviderId = activeProviderId ?? string.Empty;
            FromProviderId = fromProviderId ?? string.Empty;
            ToProviderId = toProviderId ?? string.Empty;
        }

        public string TaskId { get; }
        public string MessageId { get; }
        public AiTaskEventKind Kind { get; }
        public long Sequence { get; }
        public string Text { get; }
        public FrameworkError Error { get; }
        public string ResolvedModel { get; }
        public int InputTokens { get; }
        public int OutputTokens { get; }
        public string StructuredJson { get; }
        public string ActiveProviderId { get; }
        public string FromProviderId { get; }
        public string ToProviderId { get; }
    }

    public sealed class AiTaskReceipt
    {
        public AiTaskReceipt(string receiptId, AiTaskRequest request, AiTaskScope scope, string correlationId, string causationId, SessionRef session, long sessionGeneration, string terminalStatus, string settlementId)
        {
            ReceiptId = ContractGuard.Id(receiptId, nameof(receiptId));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            CorrelationId = ContractGuard.Id(correlationId, nameof(correlationId));
            CausationId = ContractGuard.Id(causationId, nameof(causationId));
            Session = session ?? throw new ArgumentNullException(nameof(session));
            SessionGeneration = sessionGeneration;
            TerminalStatus = ContractGuard.Id(terminalStatus, nameof(terminalStatus));
            SettlementId = settlementId ?? string.Empty;
        }

        public string ReceiptId { get; }
        public AiTaskRequest Request { get; }
        public AiTaskScope Scope { get; }
        public string TaskId => Request.TaskId;
        public string MessageId => Request.MessageId;
        public string IdempotencyKey => Scope.IdempotencyKey;
        public string RequestPayloadHash => Scope.RequestPayloadHash;
        public string ProviderId => Request.ProviderId;
        public string ProfileId => Request.ProfileId;
        public string CorrelationId { get; }
        public string CausationId { get; }
        public SessionRef Session { get; }
        public long SessionGeneration { get; }
        public string TerminalStatus { get; }
        public SettlementRequirement SettlementRequirement => Request.SettlementRequirement;
        public string SettlementId { get; }
    }

    public interface IAiTaskHandle : IDisposable
    {
        string TaskId { get; }
        IReadOnlyList<AiTaskEvent> Snapshot();
        IDisposable Subscribe(Action<AiTaskEvent> handler);
        Task<OperationResult<bool>> CancelAsync(CancellationToken cancellationToken);
    }

    public interface IAiGateway
    {
        Task<OperationResult<IAiTaskHandle>> SubmitAsync(AiTaskRequest request, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<AiTaskReceipt>> GetReceiptAsync(AiTaskScope scope, RequestContext context, CancellationToken cancellationToken);
    }
}
