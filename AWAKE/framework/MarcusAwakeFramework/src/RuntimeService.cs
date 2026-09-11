using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api
{
    public sealed class RuntimeService : IRuntimeServicePort, IAiGateway
    {
        public const int MaximumConcurrentTasksPerOwner = 4;
        public const int MaximumQueuedTasksPerOwner = 8;
        public const int MaximumInputBytes = 32768;
        public const int MaximumOutputBytes = 65536;
        public const int MaximumTokens = 2048;
        public const int MaximumTextDeltas = 256;
        public const int MaximumDrainSteps = 32;

        private readonly object sync = new object();
        private readonly string serviceId;
        private readonly ApiVersion defaultProtocolVersion;
        private readonly IReadOnlyList<string> capabilities;
        private readonly IAiGateway gateway;
        private readonly Func<DateTimeOffset> clock;
        private readonly int maximumDrainSteps;
        private readonly Dictionary<string, string> scopeKeysByIdempotency = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, IAiTaskHandle> trackedHandles = new Dictionary<string, IAiTaskHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> activeTasksByOwner = new Dictionary<string, int>(StringComparer.Ordinal);
        private RuntimeServiceState state;
        private string instanceId;
        private long connectionEpoch;
        private ApiVersion protocolVersion;
        private int maximumFrameBytes;
        private int activeTaskCount;
        private int drainAttempts;

        public RuntimeService(string serviceId, ApiVersion protocolVersion, IReadOnlyList<string> capabilities, IAiGateway gateway, Func<DateTimeOffset> clock = null, int maximumDrainSteps = MaximumDrainSteps)
        {
            this.serviceId = ContractGuard.Id(serviceId, nameof(serviceId));
            defaultProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
            this.capabilities = capabilities ?? new string[0];
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.clock = clock ?? (() => DateTimeOffset.MinValue);
            if (maximumDrainSteps < 1 || maximumDrainSteps > MaximumDrainSteps) throw new ArgumentOutOfRangeException(nameof(maximumDrainSteps));
            this.maximumDrainSteps = maximumDrainSteps;
            state = RuntimeServiceState.Created;
            this.protocolVersion = defaultProtocolVersion;
        }

        public RuntimeServiceStatus Status
        {
            get
            {
                lock (sync) return CreateStatusLocked();
            }
        }

        public int ActiveTaskCount
        {
            get
            {
                lock (sync) return activeTaskCount;
            }
        }

        public OperationResult<RuntimeServiceStatus> Start(RuntimeServiceStartRequest request, RequestContext context)
        {
            var validation = ValidateContext(context, "runtime-start");
            if (!validation.IsSuccess) return OperationResult<RuntimeServiceStatus>.Failed(validation.Error);
            if (request == null) return Failure<RuntimeServiceStatus>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A runtime service start request is required.", context.CorrelationId);
            if (!StringComparer.Ordinal.Equals(serviceId, request.ServiceId)) return Failure<RuntimeServiceStatus>("runtime.service_mismatch", FrameworkErrorCategory.Incompatible, "The runtime service identifier does not match.", context.CorrelationId);
            if (!request.ProtocolVersion.Equals(defaultProtocolVersion)) return Failure<RuntimeServiceStatus>("runtime.protocol_mismatch", FrameworkErrorCategory.Incompatible, "The runtime service protocol is incompatible.", context.CorrelationId);
            if (context.CancellationToken.IsCancellationRequested) return Failure<RuntimeServiceStatus>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The runtime service start was cancelled.", context.CorrelationId);

            lock (sync)
            {
                if (state == RuntimeServiceState.Ready) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                if (state == RuntimeServiceState.Starting) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                if (state == RuntimeServiceState.Draining) return Failure<RuntimeServiceStatus>("runtime.draining", FrameworkErrorCategory.Conflict, "The runtime service is draining.", context.CorrelationId);
                if (state == RuntimeServiceState.RecoveryRequired) return Failure<RuntimeServiceStatus>("runtime.recovery_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service requires recovery.", context.CorrelationId);
                state = RuntimeServiceState.Starting;
                connectionEpoch++;
                instanceId = serviceId + "-instance-" + connectionEpoch.ToString();
                protocolVersion = request.ProtocolVersion;
                maximumFrameBytes = request.MaximumFrameBytes;
                drainAttempts = 0;
                state = RuntimeServiceState.Ready;
                return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
            }
        }

        public OperationResult<RuntimeServiceStatus> BeginDrain(RequestContext context)
        {
            var validation = ValidateDrainContext(context, "runtime-drain");
            if (!validation.IsSuccess) return OperationResult<RuntimeServiceStatus>.Failed(validation.Error);
            lock (sync)
            {
                if (state == RuntimeServiceState.Stopped || state == RuntimeServiceState.Created) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                if (state == RuntimeServiceState.Draining) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                if (state == RuntimeServiceState.RecoveryRequired) return Failure<RuntimeServiceStatus>("runtime.recovery_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service requires recovery.", context.CorrelationId);
                if (state != RuntimeServiceState.Ready) return Failure<RuntimeServiceStatus>("runtime.invalid_drain", FrameworkErrorCategory.Conflict, "The runtime service is not ready to drain.", context.CorrelationId);
                state = RuntimeServiceState.Draining;
                drainAttempts = 0;
                return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
            }
        }

        public OperationResult<RuntimeServiceStatus> CompleteDrain(RequestContext context)
        {
            var validation = ValidateDrainContext(context, "runtime-complete-drain");
            if (!validation.IsSuccess) return OperationResult<RuntimeServiceStatus>.Failed(validation.Error);
            lock (sync)
            {
                if (state == RuntimeServiceState.Stopped || state == RuntimeServiceState.Created) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                if (state == RuntimeServiceState.RecoveryRequired) return Failure<RuntimeServiceStatus>("runtime.recovery_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service requires recovery.", context.CorrelationId);
                if (state != RuntimeServiceState.Draining) return Failure<RuntimeServiceStatus>("runtime.invalid_drain", FrameworkErrorCategory.Conflict, "The runtime service is not draining.", context.CorrelationId);
                if (activeTaskCount > 0)
                {
                    drainAttempts++;
                    if (drainAttempts >= maximumDrainSteps)
                    {
                        state = RuntimeServiceState.RecoveryRequired;
                        return Failure<RuntimeServiceStatus>("runtime.drain_recovery_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service could not drain within the bounded limit.", context.CorrelationId);
                    }
                    return Failure<RuntimeServiceStatus>("runtime.drain_incomplete", FrameworkErrorCategory.Conflict, "The runtime service still has active tasks.", context.CorrelationId);
                }
                state = RuntimeServiceState.Stopped;
                return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
            }
        }

        public Task<OperationResult<IAiTaskHandle>> SubmitAsync(AiTaskRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var validation = ValidateSubmission(request, context, cancellationToken);
            if (!validation.IsSuccess) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(validation.Error));

            var canonical = TaskRequestCanonicalizer.ForTask(new TaskRequestCanonicalInput(
                request.RouteId,
                request.ProviderId,
                request.ProfileId,
                request.MessageId,
                request.InputJson,
                request.OutputSchema,
                SettlementName(request.SettlementRequirement),
                request.Budget.InputBytes,
                request.Budget.OutputBytes,
                request.Budget.Tokens,
                request.Budget.TextDeltas), context.CorrelationId);
            if (!canonical.IsSuccess) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(canonical.Error));

            var scope = new AiTaskScope(
                request.TaskId,
                request.MessageId,
                context.Caller.Value,
                context.Session,
                context.SessionGeneration,
                context.CorrelationId,
                context.CorrelationId,
                request.RouteId,
                request.ProviderId,
                request.ProfileId,
                request.IdempotencyKey,
                canonical.Value.Hash,
                request.OutputSchema,
                request.SettlementRequirement);
            if (context.AiTaskScope != null && !StringComparer.Ordinal.Equals(context.AiTaskScope.DeduplicationKey, scope.DeduplicationKey))
            {
                return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(FrameworkErrors.Create("runtime.scope_conflict", FrameworkErrorCategory.Conflict, "The request context already carries a different AI task scope.", context.CorrelationId)));
            }

            lock (sync)
            {
                string existingScope;
                if (scopeKeysByIdempotency.TryGetValue(scope.IdempotencyScopeKey, out existingScope) && !StringComparer.Ordinal.Equals(existingScope, scope.DeduplicationKey))
                {
                    return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(FrameworkErrors.Create("runtime.idempotency_conflict", FrameworkErrorCategory.Conflict, "The idempotency key was reused with a different request payload.", context.CorrelationId)));
                }
                IAiTaskHandle existingHandle;
                if (trackedHandles.TryGetValue(scope.DeduplicationKey, out existingHandle)) return Task.FromResult(OperationResult<IAiTaskHandle>.Succeeded(existingHandle));
                int ownerTaskCount;
                activeTasksByOwner.TryGetValue(context.Caller.Value, out ownerTaskCount);
                if (ownerTaskCount >= MaximumConcurrentTasksPerOwner) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(FrameworkErrors.Create("runtime.quota_exhausted", FrameworkErrorCategory.ResourceExhausted, "The owner task quota has been exhausted.", context.CorrelationId, retryable: true)));
                scopeKeysByIdempotency[scope.IdempotencyScopeKey] = scope.DeduplicationKey;
            }

            var scopedContext = context.WithAiTaskScope(scope);
            return SubmitToGatewayAsync(request, scopedContext, cancellationToken, context.Caller.Value, scope.DeduplicationKey);
        }

        public Task<OperationResult<AiTaskReceipt>> GetReceiptAsync(AiTaskScope scope, RequestContext context, CancellationToken cancellationToken)
        {
            if (scope == null || context == null) return Task.FromResult(OperationResult<AiTaskReceipt>.Failed(FrameworkErrors.Create("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A task scope and request context are required.", context?.CorrelationId ?? "runtime-receipt")));
            var validation = ValidateContext(context, "runtime-receipt");
            if (!validation.IsSuccess) return Task.FromResult(OperationResult<AiTaskReceipt>.Failed(validation.Error));
            if (!scope.BelongsTo(context.Caller, context.Session, context.SessionGeneration, context.CorrelationId)) return Task.FromResult(OperationResult<AiTaskReceipt>.Failed(FrameworkErrors.Create("runtime.scope_owner_mismatch", FrameworkErrorCategory.Denied, "The task scope does not belong to the request context.", context.CorrelationId)));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(OperationResult<AiTaskReceipt>.Failed(FrameworkErrors.Create("runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId)));
            return gateway.GetReceiptAsync(scope, context.WithAiTaskScope(scope), cancellationToken);
        }

        private async Task<OperationResult<IAiTaskHandle>> SubmitToGatewayAsync(AiTaskRequest request, RequestContext context, CancellationToken cancellationToken, string ownerId, string scopeKey)
        {
            OperationResult<IAiTaskHandle> result;
            try
            {
                result = await gateway.SubmitAsync(request, context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return OperationResult<IAiTaskHandle>.Failed(FrameworkErrors.Create("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The AI task was cancelled.", context.CorrelationId));
            }
            if (!result.IsSuccess || result.Value == null) return result;
            IAiTaskHandle tracked;
            lock (sync)
            {
                if (trackedHandles.TryGetValue(scopeKey, out tracked)) return OperationResult<IAiTaskHandle>.Succeeded(tracked);
                activeTaskCount++;
                int ownerCount;
                activeTasksByOwner.TryGetValue(ownerId, out ownerCount);
                activeTasksByOwner[ownerId] = ownerCount + 1;
            }
            tracked = new TrackingTaskHandle(result.Value, () => ReleaseTask(ownerId));
            lock (sync) trackedHandles[scopeKey] = tracked;
            return OperationResult<IAiTaskHandle>.Succeeded(tracked);
        }

        private OperationResult<bool> ValidateSubmission(AiTaskRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            if (request == null || context == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "An AI task request and context are required.", context?.CorrelationId ?? "runtime-submit"));
            var validation = ValidateContext(context, "runtime-submit");
            if (!validation.IsSuccess) return validation;
            if (cancellationToken == CancellationToken.None) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId));
            if (request.Deadline == DateTimeOffset.MinValue || request.Deadline == DateTimeOffset.MaxValue || request.Deadline > context.Deadline) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.deadline_invalid", FrameworkErrorCategory.InvalidRequest, "The task deadline must be finite and bounded by the request context.", context.CorrelationId));
            if (request.Deadline <= clock()) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.deadline_expired", FrameworkErrorCategory.Expired, "The AI task deadline has expired.", context.CorrelationId));
            if (context.CancellationToken.IsCancellationRequested || cancellationToken.IsCancellationRequested) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The AI task was cancelled.", context.CorrelationId));
            if (request.Budget.InputBytes > MaximumInputBytes) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.input_budget_exceeded", FrameworkErrorCategory.ResourceExhausted, "The AI task input budget was exceeded.", context.CorrelationId));
            if (request.Budget.OutputBytes > MaximumOutputBytes) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.output_budget_exceeded", FrameworkErrorCategory.ResourceExhausted, "The AI task output budget was exceeded.", context.CorrelationId));
            if (request.Budget.Tokens > MaximumTokens) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.token_budget_exceeded", FrameworkErrorCategory.ResourceExhausted, "The AI task token budget was exceeded.", context.CorrelationId));
            if (request.Budget.TextDeltas > MaximumTextDeltas) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.delta_budget_exceeded", FrameworkErrorCategory.ResourceExhausted, "The AI task delta budget was exceeded.", context.CorrelationId));
            return OperationResult<bool>.Succeeded(true);
        }

        private OperationResult<bool> ValidateContext(RequestContext context, string correlationId)
        {
            if (context == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.context_missing", FrameworkErrorCategory.InvalidRequest, "A request context is required.", correlationId));
            if (context.CancellationToken == CancellationToken.None) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.session_token_missing", FrameworkErrorCategory.InvalidRequest, "A session-owned cancellation token is required.", context.CorrelationId));
            if (!context.SessionLease.MatchesReady(context)) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", context.CorrelationId));
            if (context.IsExpiredAt(clock())) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context.CorrelationId));
            return OperationResult<bool>.Succeeded(true);
        }

        private OperationResult<bool> ValidateDrainContext(RequestContext context, string correlationId)
        {
            if (context == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.context_missing", FrameworkErrorCategory.InvalidRequest, "A request context is required.", correlationId));
            if (context.CancellationToken == CancellationToken.None) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.session_token_missing", FrameworkErrorCategory.InvalidRequest, "A session-owned cancellation token is required.", context.CorrelationId));
            if (!context.SessionLease.MatchesClosing(context) && !context.SessionLease.MatchesReady(context)) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", context.CorrelationId));
            if (context.IsExpiredAt(clock())) return OperationResult<bool>.Failed(FrameworkErrors.Create("runtime.deadline_expired", FrameworkErrorCategory.Expired, "The runtime service request deadline has expired.", context.CorrelationId));
            return OperationResult<bool>.Succeeded(true);
        }

        private RuntimeServiceStatus CreateStatusLocked()
        {
            return new RuntimeServiceStatus(state, serviceId, instanceId ?? string.Empty, connectionEpoch, protocolVersion, capabilities);
        }

        private void ReleaseTask(string ownerId)
        {
            lock (sync)
            {
                if (activeTaskCount > 0) activeTaskCount--;
                int ownerCount;
                if (activeTasksByOwner.TryGetValue(ownerId, out ownerCount))
                {
                    if (ownerCount <= 1) activeTasksByOwner.Remove(ownerId);
                    else activeTasksByOwner[ownerId] = ownerCount - 1;
                }
            }
        }

        private static string SettlementName(SettlementRequirement requirement) => requirement == SettlementRequirement.Required ? "required" : "not_applicable";

        private static OperationResult<T> Failure<T>(string code, FrameworkErrorCategory category, string fallback, string correlationId)
        {
            return OperationResult<T>.Failed(FrameworkErrors.Create(code, category, fallback, correlationId));
        }

        private sealed class TrackingTaskHandle : IAiTaskHandle
        {
            private readonly IAiTaskHandle inner;
            private readonly Action release;
            private readonly IDisposable terminalSubscription;
            private int released;

            internal TrackingTaskHandle(IAiTaskHandle inner, Action release)
            {
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
                this.release = release ?? throw new ArgumentNullException(nameof(release));
                terminalSubscription = inner.Subscribe(Observe);
            }

            public string TaskId => inner.TaskId;
            public IReadOnlyList<AiTaskEvent> Snapshot() => inner.Snapshot();
            public IDisposable Subscribe(Action<AiTaskEvent> handler) => inner.Subscribe(handler);
            public Task<OperationResult<bool>> CancelAsync(CancellationToken cancellationToken) => inner.CancelAsync(cancellationToken);

            public void Dispose()
            {
                Release();
                terminalSubscription?.Dispose();
                inner.Dispose();
            }

            private void Observe(AiTaskEvent item)
            {
                if (item != null && (item.Kind == AiTaskEventKind.Completed || item.Kind == AiTaskEventKind.Cancelled || item.Kind == AiTaskEventKind.Failed)) Release();
            }

            private void Release()
            {
                if (Interlocked.Exchange(ref released, 1) == 0) release();
            }
        }
    }
}
