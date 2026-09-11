using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace MarcusAwakeFramework.Tests.TestDoubles
{
    internal sealed class FixtureTaskHandle : IAiTaskHandle
    {
        private readonly object sync = new object();
        private readonly List<AiTaskEvent> events = new List<AiTaskEvent>();
        private readonly List<Action<AiTaskEvent>> subscribers = new List<Action<AiTaskEvent>>();
        private long nextSequence;
        private bool terminal;
        private bool disposed;

        internal FixtureTaskHandle(string taskId, string messageId)
        {
            TaskId = taskId;
            MessageId = messageId;
        }

        public string TaskId { get; }
        private string MessageId { get; }

        public IReadOnlyList<AiTaskEvent> Snapshot()
        {
            lock (sync) return events.ToList().AsReadOnly();
        }

        public IDisposable Subscribe(Action<AiTaskEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (sync)
            {
                if (disposed) return new ActionSubscription(null);
                subscribers.Add(handler);
                return new ActionSubscription(() =>
                {
                    lock (sync) subscribers.Remove(handler);
                });
            }
        }

        public Task<OperationResult<bool>> CancelAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken == CancellationToken.None) return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create("fixture.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A cancellation token is required.", "fixture-cancel")));
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create("fixture.cancelled", FrameworkErrorCategory.Cancelled, "The cancellation was cancelled.", "fixture-cancel")));
            return Task.FromResult(Emit(AiTaskEventKind.Cancelled, string.Empty, null, string.Empty, 0, 0)
                ? OperationResult<bool>.Succeeded(true)
                : OperationResult<bool>.Failed(FrameworkErrors.Create("fixture.duplicate_terminal", FrameworkErrorCategory.Conflict, "The task already has a terminal event.", "fixture-cancel")));
        }

        internal bool Emit(AiTaskEventKind kind, string text, FrameworkError error, string model, int inputTokens, int outputTokens)
        {
            List<Action<AiTaskEvent>> callbacks;
            AiTaskEvent item;
            lock (sync)
            {
                if (disposed || terminal) return false;
                if (kind == AiTaskEventKind.Completed || kind == AiTaskEventKind.Cancelled || kind == AiTaskEventKind.Failed) terminal = true;
                item = new AiTaskEvent(TaskId, MessageId, kind, ++nextSequence, text, error, model, inputTokens, outputTokens);
                events.Add(item);
                callbacks = subscribers.ToList();
            }
            for (var index = 0; index < callbacks.Count; index++) callbacks[index](item);
            return true;
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                subscribers.Clear();
            }
        }

        private sealed class ActionSubscription : IDisposable
        {
            private readonly Action dispose;
            internal ActionSubscription(Action dispose) { this.dispose = dispose; }
            public void Dispose() { dispose?.Invoke(); }
        }
    }
}
