using System;
using System.Collections.Generic;
using System.Threading;

namespace MarcusAwakeFramework.Api
{
    public enum SessionState
    {
        Created,
        Ready,
        Closing,
        Drained,
        RecoveryRequired
    }

    public sealed class SessionLease
    {
        private readonly object sync = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private long generation;
        private SessionState state;
        private int pendingOperationCount;
        private string drainTaskId;

        internal SessionLease(SessionRef reference, long generation)
        {
            Reference = reference ?? throw new ArgumentNullException(nameof(reference));
            this.generation = generation;
            state = SessionState.Created;
        }

        public SessionRef Reference { get; }
        public long Generation { get { lock (sync) return generation; } }
        public SessionState State { get { lock (sync) return state; } }
        public string DrainTaskId { get { lock (sync) return drainTaskId; } }
        public int PendingOperationCount { get { lock (sync) return pendingOperationCount; } }
        public CancellationToken CancellationToken => cancellation.Token;

        internal bool MarkReady()
        {
            lock (sync)
            {
                if (state != SessionState.Created) return false;
                state = SessionState.Ready;
                return true;
            }
        }

        internal bool MatchesReady(RequestContext context)
        {
            lock (sync) return context != null && Reference.Equals(context.Session) && (context.SessionGeneration == 0 || context.SessionGeneration == generation) && state == SessionState.Ready;
        }

        internal bool MatchesClosing(RequestContext context)
        {
            lock (sync) return context != null && Reference.Equals(context.Session) && context.SessionGeneration == generation && state == SessionState.Closing;
        }

        internal SessionCloseTransition BeginClosing()
        {
            lock (sync)
            {
                if (state != SessionState.Ready) return new SessionCloseTransition(false, null);
                state = SessionState.Closing;
                generation++;
                drainTaskId = "drain-" + Reference.SessionId + "-" + generation;
                return new SessionCloseTransition(true, cancellation);
            }
        }

        internal bool CompleteDrain()
        {
            lock (sync)
            {
                if (state != SessionState.Closing || pendingOperationCount != 0) return false;
                state = SessionState.Drained;
                return true;
            }
        }

        internal SessionCloseTransition MarkRecovery()
        {
            lock (sync)
            {
                if (state == SessionState.RecoveryRequired) return new SessionCloseTransition(false, null);
                if (state != SessionState.Closing) return new SessionCloseTransition(false, null);
                state = SessionState.RecoveryRequired;
                return new SessionCloseTransition(true, cancellation);
            }
        }

        internal bool TryBeginOperation(out long operationGeneration)
        {
            lock (sync)
            {
                operationGeneration = generation;
                if (state != SessionState.Ready) return false;
                pendingOperationCount++;
                return true;
            }
        }

        internal void ReleaseOperation()
        {
            lock (sync) if (pendingOperationCount > 0) pendingOperationCount--;
        }
    }

    internal sealed class SessionCloseTransition
    {
        internal SessionCloseTransition(bool changed, CancellationTokenSource cancellation)
        {
            Changed = changed;
            Cancellation = cancellation;
        }

        internal bool Changed { get; }
        internal CancellationTokenSource Cancellation { get; }
    }

    public sealed class SessionOperation
    {
        private readonly SessionCoordinator owner;
        private readonly SessionLease lease;
        private bool completed;

        internal SessionOperation(SessionCoordinator owner, SessionLease lease, string operationId, long generation, string correlationId)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
            OperationId = ContractGuard.Id(operationId, nameof(operationId));
            Generation = generation;
            CorrelationId = ContractGuard.Id(correlationId, nameof(correlationId));
        }

        public string OperationId { get; }
        public SessionRef Session => lease.Reference;
        public long Generation { get; }
        internal string CorrelationId { get; }
        internal SessionLease Lease => lease;
        internal SessionCoordinator Owner => owner;
        internal bool IsCompleted => completed;
        internal void MarkCompleted() { completed = true; }
        public OperationResult<bool> Complete() => owner.CompleteOperation(this);
    }

    public interface ISessionCoordinator
    {
        OperationResult<SessionLease> BeginSession(SessionRef reference);
        OperationResult<SessionLease> BeginClosing(SessionRef reference);
        OperationResult<bool> CompleteDrain(SessionRef reference, long generation);
        OperationResult<SessionLease> ValidateLease(RequestContext context);
        OperationResult<SessionOperation> BeginOperation(RequestContext context, string operationId);
        SessionLease Current { get; }
    }

    public sealed class SessionCoordinator : ISessionCoordinator
    {
        private readonly object sync = new object();
        private readonly Func<DateTimeOffset> clock;
        private long nextGeneration;
        private SessionLease current;
        private readonly HashSet<SessionOperation> operations = new HashSet<SessionOperation>();

        public SessionCoordinator(Func<DateTimeOffset> clock = null)
        {
            this.clock = clock ?? (() => DateTimeOffset.MinValue);
        }

        public SessionLease Current { get { lock (sync) return current; } }

        public OperationResult<SessionLease> BeginSession(SessionRef reference)
        {
            if (reference == null) return OperationResult<SessionLease>.Failed(FrameworkErrors.Create("session.reference_missing", FrameworkErrorCategory.InvalidRequest, "A session reference is required.", "session-start"));

            lock (sync)
            {
                if (current != null)
                {
                    var state = current.State;
                    if (state == SessionState.RecoveryRequired) return Failure<SessionLease>("session_recovery_required", FrameworkErrorCategory.RecoveryRequired, "The session requires recovery before a new session can begin.", "session-start");
                    if (state == SessionState.Closing) return Failure<SessionLease>("session_closing", FrameworkErrorCategory.Conflict, "The session is closing.", "session-start");
                    if (state == SessionState.Ready && current.Reference.Equals(reference)) return OperationResult<SessionLease>.Succeeded(current);
                    if (state != SessionState.Drained) return Failure<SessionLease>("session.active", FrameworkErrorCategory.Conflict, "Another session is still active.", "session-start");
                }

                var lease = new SessionLease(reference, ++nextGeneration);
                lease.MarkReady();
                current = lease;
                return OperationResult<SessionLease>.Succeeded(lease);
            }
        }

        public OperationResult<SessionLease> BeginClosing(SessionRef reference)
        {
            CancellationTokenSource cancellation = null;
            SessionLease lease;
            lock (sync)
            {
                lease = current;
                if (lease == null || reference == null || !lease.Reference.Equals(reference)) return Failure<SessionLease>("session.not_found", FrameworkErrorCategory.NotFound, "The session is not active.", "session-close");
                var transition = lease.BeginClosing();
                cancellation = transition.Cancellation;
            }

            cancellation?.Cancel();
            return OperationResult<SessionLease>.Succeeded(lease);
        }

        public OperationResult<bool> CompleteDrain(SessionRef reference, long generation)
        {
            lock (sync)
            {
                if (current == null || reference == null || !current.Reference.Equals(reference) || current.Generation != generation) return Failure<bool>("session_generation_conflict", FrameworkErrorCategory.Conflict, "The session generation is stale.", "session-drain");
                if (current.State == SessionState.RecoveryRequired) return Failure<bool>("session_recovery_required", FrameworkErrorCategory.RecoveryRequired, "The session requires recovery before drain can complete.", "session-drain");
                if (current.State != SessionState.Closing) return Failure<bool>("session_generation_conflict", FrameworkErrorCategory.Conflict, "The session is not in the closing state.", "session-drain");
                if (current.PendingOperationCount != 0) return Failure<bool>("session_drain_incomplete", FrameworkErrorCategory.Conflict, "The session still has pending operations.", "session-drain");
                return OperationResult<bool>.Succeeded(current.CompleteDrain());
            }
        }

        public OperationResult<SessionLease> ValidateLease(RequestContext context)
        {
            if (context == null) return Failure<SessionLease>("session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", "session-validate");
            if (context.IsExpiredAt(clock())) return Failure<SessionLease>("request_deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context.CorrelationId);

            lock (sync) return ValidateLeaseLocked(context);
        }

        public OperationResult<SessionOperation> BeginOperation(RequestContext context, string operationId)
        {
            if (context == null) return Failure<SessionOperation>("session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", "session-operation");
            if (string.IsNullOrWhiteSpace(operationId)) return Failure<SessionOperation>("session.operation_id_missing", FrameworkErrorCategory.InvalidRequest, "An operation identifier is required.", context.CorrelationId);
            if (context.IsExpiredAt(clock())) return Failure<SessionOperation>("request_deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context.CorrelationId);

            lock (sync)
            {
                var validation = ValidateLeaseLocked(context);
                if (!validation.IsSuccess) return OperationResult<SessionOperation>.Failed(validation.Error);
                long generation;
                if (!validation.Value.TryBeginOperation(out generation)) return Failure<SessionOperation>("session_closing", FrameworkErrorCategory.Conflict, "The session is closing.", context.CorrelationId);
                var operation = new SessionOperation(this, validation.Value, operationId, generation, context.CorrelationId);
                operations.Add(operation);
                return OperationResult<SessionOperation>.Succeeded(operation);
            }
        }

        internal OperationResult<bool> CompleteOperation(SessionOperation operation)
        {
            if (operation == null) return Failure<bool>("session.operation_missing", FrameworkErrorCategory.InvalidRequest, "A session operation is required.", "session-operation");
            lock (sync)
            {
                if (!ReferenceEquals(operation.Owner, this)) return Failure<bool>("session.operation_owner_conflict", FrameworkErrorCategory.Conflict, "The operation belongs to another session coordinator.", operation.CorrelationId);
                if (operation.IsCompleted) return Failure<bool>("session_operation_already_completed", FrameworkErrorCategory.Conflict, "The session operation was already completed.", operation.CorrelationId);
                operation.MarkCompleted();
                operations.Remove(operation);
                operation.Lease.ReleaseOperation();
                if (current == null || !ReferenceEquals(current, operation.Lease) || current.Generation != operation.Generation || current.State != SessionState.Ready) return Failure<bool>("session_stale_result", FrameworkErrorCategory.Expired, "The operation completed after its session fence changed.", operation.CorrelationId);
                return OperationResult<bool>.Succeeded(true);
            }
        }

        internal OperationResult<bool> EnterRecoveryRequired(SessionRef reference)
        {
            CancellationTokenSource cancellation = null;
            lock (sync)
            {
                if (current == null || reference == null || !current.Reference.Equals(reference)) return Failure<bool>("session.not_found", FrameworkErrorCategory.NotFound, "The session is not active.", "session-recovery");
                if (current.State == SessionState.RecoveryRequired) return OperationResult<bool>.Succeeded(true);
                if (current.State != SessionState.Closing) return Failure<bool>("session.recovery_transition_invalid", FrameworkErrorCategory.Conflict, "Recovery is only valid while the session is closing.", "session-recovery");
                cancellation = current.MarkRecovery().Cancellation;
            }
            cancellation?.Cancel();
            return OperationResult<bool>.Succeeded(true);
        }

        private OperationResult<SessionLease> ValidateLeaseLocked(RequestContext context)
        {
            if (current == null) return Failure<SessionLease>("session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", context.CorrelationId);
            if (current.State == SessionState.RecoveryRequired) return Failure<SessionLease>("session_recovery_required", FrameworkErrorCategory.RecoveryRequired, "The session requires recovery.", context.CorrelationId);
            if (current.State == SessionState.Closing) return Failure<SessionLease>("session_closing", FrameworkErrorCategory.Conflict, "The session is closing.", context.CorrelationId);
            if (current.State != SessionState.Ready) return Failure<SessionLease>("session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", context.CorrelationId);
            if (!current.MatchesReady(context)) return Failure<SessionLease>("session_generation_conflict", FrameworkErrorCategory.Conflict, "The request belongs to a stale session generation.", context.CorrelationId);
            return OperationResult<SessionLease>.Succeeded(current);
        }

        private static OperationResult<T> Failure<T>(string code, FrameworkErrorCategory category, string fallback, string correlationId)
        {
            return OperationResult<T>.Failed(FrameworkErrors.Create(code, category, fallback, correlationId));
        }
    }
}
