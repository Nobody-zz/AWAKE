using System;
using System.Collections.Generic;

namespace MarcusAwakeTransport
{
    public sealed class SequenceWindow
    {
        private readonly object sync = new object();
        private readonly string sessionId;
        private readonly long connectionEpoch;
        private readonly string directionNonce;
        private readonly int windowSize;
        private readonly Dictionary<long, SequenceRecord> records = new Dictionary<long, SequenceRecord>();
        private long lastAccepted;

        public SequenceWindow(string sessionId, long connectionEpoch, string directionNonce, int windowSize = ProtocolConstants.MaxSequenceWindow)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("session_id_required", nameof(sessionId));
            if (string.IsNullOrWhiteSpace(directionNonce)) throw new ArgumentException("direction_nonce_required", nameof(directionNonce));
            if (connectionEpoch < 1) throw new ArgumentOutOfRangeException(nameof(connectionEpoch));
            if (windowSize < 1 || windowSize > ProtocolConstants.MaxSequenceWindow) throw new ArgumentOutOfRangeException(nameof(windowSize));
            this.sessionId = sessionId;
            this.connectionEpoch = connectionEpoch;
            this.directionNonce = directionNonce;
            this.windowSize = windowSize;
        }

        public long LastAccepted
        {
            get { lock (sync) return lastAccepted; }
        }

        public long ExpectedNext
        {
            get { lock (sync) return lastAccepted == long.MaxValue ? long.MaxValue : lastAccepted + 1; }
        }

        public SequenceDecision Evaluate(PipeEnvelope envelope)
        {
            if (envelope == null) return new SequenceDecision(SequenceDecisionKind.ReplayRejected, 0, string.Empty, string.Empty);
            return Evaluate(envelope.SessionId, envelope.ConnectionEpoch, envelope.DirectionNonce, envelope.Sequence, envelope.MessageId, envelope.PayloadSha256);
        }

        public SequenceDecision Evaluate(string frameSessionId, long frameConnectionEpoch, string frameDirectionNonce, long sequence, string messageId, string payloadHash)
        {
            lock (sync)
            {
                if (!StringComparer.Ordinal.Equals(sessionId, frameSessionId) || frameConnectionEpoch != connectionEpoch || !StringComparer.Ordinal.Equals(directionNonce, frameDirectionNonce)) return new SequenceDecision(SequenceDecisionKind.NonceMismatch, sequence, messageId, payloadHash);
                if (sequence < 1) return new SequenceDecision(SequenceDecisionKind.ReplayRejected, sequence, messageId, payloadHash);
                SequenceRecord previous;
                if (records.TryGetValue(sequence, out previous))
                {
                    if (!StringComparer.Ordinal.Equals(previous.MessageId, messageId) || !StringComparer.Ordinal.Equals(previous.PayloadHash, payloadHash)) return new SequenceDecision(SequenceDecisionKind.ReplayRejected, sequence, messageId, payloadHash);
                    return new SequenceDecision(SequenceDecisionKind.Duplicate, sequence, messageId, payloadHash);
                }
                if (sequence <= lastAccepted) return new SequenceDecision(SequenceDecisionKind.ReplayRejected, sequence, messageId, payloadHash);
                if (sequence - lastAccepted > windowSize) return new SequenceDecision(SequenceDecisionKind.SequenceGap, sequence, messageId, payloadHash);
                if (sequence != lastAccepted + 1)
                {
                    return new SequenceDecision(SequenceDecisionKind.SequenceOutOfOrder, sequence, messageId, payloadHash);
                }
                records[sequence] = new SequenceRecord(messageId, payloadHash);
                AdvanceWatermark();
                Trim();
                return new SequenceDecision(SequenceDecisionKind.Accepted, sequence, messageId, payloadHash);
            }
        }

        private void AdvanceWatermark()
        {
            while (lastAccepted < long.MaxValue && records.ContainsKey(lastAccepted + 1)) lastAccepted++;
        }

        private void Trim()
        {
            var floor = lastAccepted <= windowSize ? 0 : lastAccepted - windowSize;
            var remove = new List<long>();
            foreach (var record in records)
            {
                if (record.Key < floor) remove.Add(record.Key);
            }
            for (var index = 0; index < remove.Count; index++) records.Remove(remove[index]);
        }

        private sealed class SequenceRecord
        {
            internal SequenceRecord(string messageId, string payloadHash)
            {
                MessageId = messageId ?? string.Empty;
                PayloadHash = payloadHash ?? string.Empty;
            }

            internal string MessageId { get; }
            internal string PayloadHash { get; }
        }
    }

    public sealed class DuplexSequenceWindows
    {
        public DuplexSequenceWindows(string sessionId, long connectionEpoch, string clientToServiceNonce, string serviceToClientNonce, int windowSize = ProtocolConstants.MaxSequenceWindow)
        {
            ClientToService = new SequenceWindow(sessionId, connectionEpoch, clientToServiceNonce, windowSize);
            ServiceToClient = new SequenceWindow(sessionId, connectionEpoch, serviceToClientNonce, windowSize);
        }

        public SequenceWindow ClientToService { get; }
        public SequenceWindow ServiceToClient { get; }
    }
}
