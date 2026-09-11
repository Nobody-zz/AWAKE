using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    public enum IpcAckStatus
    {
        Accepted,
        DurablyRecorded
    }

    public sealed class IpcEnvelope
    {
        public IpcEnvelope(string sessionId, long connectionEpoch, string directionNonce, long sequence, string messageId, string payloadHash)
        {
            SessionId = ContractGuard.Id(sessionId, nameof(sessionId));
            ConnectionEpoch = connectionEpoch;
            DirectionNonce = ContractGuard.Id(directionNonce, nameof(directionNonce));
            Sequence = sequence;
            MessageId = ContractGuard.Id(messageId, nameof(messageId));
            PayloadHash = ContractGuard.Id(payloadHash, nameof(payloadHash));
        }
        public string SessionId { get; }
        public long ConnectionEpoch { get; }
        public string DirectionNonce { get; }
        public long Sequence { get; }
        public string MessageId { get; }
        public string PayloadHash { get; }
    }

    public enum IpcFrameDecisionKind
    {
        Accepted,
        SafeRetry,
        SequenceGap,
        SequenceOutOfOrder,
        ReplayRejected,
        NonceMismatch,
        Backpressure
    }

    public sealed class IpcFrameDecision
    {
        internal IpcFrameDecision(IpcFrameDecisionKind kind, IpcAckStatus? ackStatus = null) { Kind = kind; AckStatus = ackStatus; }
        public IpcFrameDecisionKind Kind { get; }
        public IpcAckStatus? AckStatus { get; }
    }

    public sealed class IpcSequenceWindow
    {
        private readonly object sync = new object();
        private readonly string sessionId;
        private readonly long connectionEpoch;
        private readonly string directionNonce;
        private readonly int windowSize;
        private readonly Dictionary<long, IpcEnvelope> received = new Dictionary<long, IpcEnvelope>();
        private long lastAccepted;

        public IpcSequenceWindow(string sessionId, long connectionEpoch, string directionNonce, int windowSize = 32)
        {
            this.sessionId = ContractGuard.Id(sessionId, nameof(sessionId));
            this.connectionEpoch = connectionEpoch;
            this.directionNonce = ContractGuard.Id(directionNonce, nameof(directionNonce));
            if (windowSize < 1 || windowSize > 1024) throw new ArgumentOutOfRangeException(nameof(windowSize));
            this.windowSize = windowSize;
        }

        public long LastAccepted { get { lock (sync) return lastAccepted; } }
        public IpcFrameDecision Evaluate(IpcEnvelope frame)
        {
            lock (sync)
            {
                if (frame == null) return new IpcFrameDecision(IpcFrameDecisionKind.NonceMismatch);
                if (!StringComparer.Ordinal.Equals(frame.SessionId, sessionId) || frame.ConnectionEpoch != connectionEpoch || !StringComparer.Ordinal.Equals(frame.DirectionNonce, directionNonce)) return new IpcFrameDecision(IpcFrameDecisionKind.NonceMismatch);
                IpcEnvelope previous;
                if (received.TryGetValue(frame.Sequence, out previous))
                {
                    if (!StringComparer.Ordinal.Equals(previous.MessageId, frame.MessageId) || !StringComparer.Ordinal.Equals(previous.PayloadHash, frame.PayloadHash)) return new IpcFrameDecision(IpcFrameDecisionKind.ReplayRejected);
                    return frame.Sequence <= lastAccepted
                        ? new IpcFrameDecision(IpcFrameDecisionKind.SafeRetry, IpcAckStatus.DurablyRecorded)
                        : new IpcFrameDecision(IpcFrameDecisionKind.SequenceOutOfOrder);
                }
                if (frame.Sequence <= lastAccepted) return new IpcFrameDecision(IpcFrameDecisionKind.ReplayRejected);
                if (frame.Sequence - lastAccepted > windowSize) return new IpcFrameDecision(IpcFrameDecisionKind.SequenceGap);
                received[frame.Sequence] = frame;
                if (frame.Sequence > lastAccepted + 1)
                {
                    Trim();
                    return new IpcFrameDecision(IpcFrameDecisionKind.SequenceOutOfOrder);
                }
                while (received.ContainsKey(lastAccepted + 1)) lastAccepted++;
                Trim();
                return new IpcFrameDecision(IpcFrameDecisionKind.Accepted, IpcAckStatus.Accepted);
            }
        }

        private void Trim()
        {
            var floor = lastAccepted - windowSize;
            var remove = new List<long>();
            foreach (var pair in received) if (pair.Key < floor) remove.Add(pair.Key);
            for (var index = 0; index < remove.Count; index++) received.Remove(remove[index]);
        }
    }

    public enum FrameworkHealthState
    {
        Ready,
        Degraded,
        RecoveryRequired,
        Unavailable
    }

    public sealed class FrameworkProbeReceipt
    {
        public FrameworkProbeReceipt(FrameworkIdentity identity, FrameworkHealthState health, string correlationId, long sessionGeneration, DateTimeOffset observedAt)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Health = health;
            CorrelationId = ContractGuard.Id(correlationId, nameof(correlationId));
            SessionGeneration = sessionGeneration;
            ObservedAt = observedAt;
        }
        public FrameworkIdentity Identity { get; }
        public FrameworkHealthState Health { get; }
        public string CorrelationId { get; }
        public long SessionGeneration { get; }
        public DateTimeOffset ObservedAt { get; }
    }

    public interface IDiagnosticsService
    {
        OperationResult<FrameworkProbeReceipt> Probe(RequestContext context, FrameworkHealthState health);
    }

    public sealed class DiagnosticsService : IDiagnosticsService
    {
        private readonly FrameworkIdentity identity;
        private readonly ISessionCoordinator sessions;
        private readonly Func<DateTimeOffset> clock;
        public DiagnosticsService(FrameworkIdentity identity, ISessionCoordinator sessions, Func<DateTimeOffset> clock = null)
        {
            this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.clock = clock ?? (() => DateTimeOffset.MinValue);
        }
        public OperationResult<FrameworkProbeReceipt> Probe(RequestContext context, FrameworkHealthState health)
        {
            if (context == null) return OperationResult<FrameworkProbeReceipt>.Failed(FrameworkErrors.Create("session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", "diagnostics-probe"));
            var validation = sessions.ValidateLease(context);
            if (!validation.IsSuccess) return OperationResult<FrameworkProbeReceipt>.Failed(validation.Error);
            return OperationResult<FrameworkProbeReceipt>.Succeeded(new FrameworkProbeReceipt(identity, health, context.CorrelationId, validation.Value.Generation, clock()));
        }
    }

    public sealed class BackpressureGate
    {
        private readonly int capacity;
        private int active;
        public BackpressureGate(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }
        public bool TryEnter()
        {
            while (true)
            {
                var observed = System.Threading.Volatile.Read(ref active);
                if (observed >= capacity) return false;
                if (System.Threading.Interlocked.CompareExchange(ref active, observed + 1, observed) == observed) return true;
            }
        }
        public void Exit()
        {
            while (true)
            {
                var observed = System.Threading.Volatile.Read(ref active);
                if (observed == 0) return;
                if (System.Threading.Interlocked.CompareExchange(ref active, observed - 1, observed) == observed) return;
            }
        }
        public int Active => System.Threading.Volatile.Read(ref active);
        public int Capacity => capacity;
    }
}
