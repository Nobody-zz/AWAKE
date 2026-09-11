using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeTransport;

namespace MarcusAwakeFramework.Api
{
    public sealed class AiTaskHandle : IAiTaskHandle
    {
        public const int MaximumSnapshotEvents = 128;

        private readonly object sync = new object();
        private readonly List<AiTaskEvent> events = new List<AiTaskEvent>();
        private readonly List<Action<AiTaskEvent>> subscribers = new List<Action<AiTaskEvent>>();
        private readonly int snapshotLimit;
        private readonly Func<CancellationToken, Task<OperationResult<bool>>> cancellationDelegate;
        private readonly Action cancellationSignal;
        private bool terminal;
        private bool disposed;
        private bool cancellationInFlight;
        private bool visibleText;
        private long nextSequence;

        public AiTaskHandle(string taskId, string messageId, int snapshotLimit = MaximumSnapshotEvents)
            : this(taskId, messageId, snapshotLimit, null)
        {
        }

        internal AiTaskHandle(string taskId, string messageId, int snapshotLimit, Func<CancellationToken, Task<OperationResult<bool>>> cancellationDelegate)
            : this(taskId, messageId, snapshotLimit, cancellationDelegate, null)
        {
        }

        internal AiTaskHandle(string taskId, string messageId, int snapshotLimit, Func<CancellationToken, Task<OperationResult<bool>>> cancellationDelegate, Action cancellationSignal)
        {
            TaskId = ContractGuard.Id(taskId, nameof(taskId));
            MessageId = ContractGuard.Id(messageId, nameof(messageId));
            if (snapshotLimit < 2 || snapshotLimit > MaximumSnapshotEvents) throw new ArgumentOutOfRangeException(nameof(snapshotLimit));
            this.snapshotLimit = snapshotLimit;
            this.cancellationDelegate = cancellationDelegate;
            this.cancellationSignal = cancellationSignal;
        }

        public string TaskId { get; }
        public string MessageId { get; }

        public IReadOnlyList<AiTaskEvent> Snapshot()
        {
            lock (sync) return events.ToList().AsReadOnly();
        }

        public IDisposable Subscribe(Action<AiTaskEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (sync)
            {
                if (disposed) return NoopSubscription.Instance;
                subscribers.Add(handler);
                return new Subscription(() =>
                {
                    lock (sync) subscribers.Remove(handler);
                });
            }
        }

        public OperationResult<bool> Publish(AiTaskEvent taskEvent)
        {
            if (taskEvent == null) return Failure("runtime.task_event_missing", FrameworkErrorCategory.InvalidRequest, "The AI task event is required.");
            List<Action<AiTaskEvent>> callbacks;
            lock (sync)
            {
                if (disposed) return Failure("runtime.task_disposed", FrameworkErrorCategory.Expired, "The AI task handle has been disposed.");
                if (terminal) return Failure("runtime.terminal_event_duplicate", FrameworkErrorCategory.Conflict, "The AI task already has a terminal event.");
                if (!StringComparer.Ordinal.Equals(TaskId, taskEvent.TaskId) || !StringComparer.Ordinal.Equals(MessageId, taskEvent.MessageId)) return Failure("runtime.task_event_identity_mismatch", FrameworkErrorCategory.Conflict, "The AI task event does not belong to this task.");
                if (taskEvent.Sequence != nextSequence + 1) return Failure("runtime.task_event_sequence_invalid", FrameworkErrorCategory.InvalidRequest, "The AI task event sequence is invalid.");
                if (!ValidateTransition(taskEvent, out var transitionError)) return Failure(transitionError, FrameworkErrorCategory.InvalidRequest, "The AI task event is not valid for the current state.");
                if (!StructuredJsonContract.TryValidateObject(taskEvent.StructuredJson, out var structuredError)) return Failure(structuredError, FrameworkErrorCategory.InvalidRequest, "The structured AI result is invalid.");
                if (events.Count >= snapshotLimit && !IsTerminal(taskEvent.Kind)) return Failure("runtime.task_snapshot_exhausted", FrameworkErrorCategory.ResourceExhausted, "The AI task event snapshot is full.");

                events.Add(taskEvent);
                nextSequence = taskEvent.Sequence;
                if (taskEvent.Kind == AiTaskEventKind.TextDelta) visibleText = true;
                if (IsTerminal(taskEvent.Kind)) terminal = true;
                callbacks = subscribers.ToList();
            }

            for (var index = 0; index < callbacks.Count; index++) callbacks[index](taskEvent);
            return OperationResult<bool>.Succeeded(true);
        }

        public async Task<OperationResult<bool>> CancelAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken == CancellationToken.None) return Failure("runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A cancellation token is required.");
            if (cancellationToken.IsCancellationRequested) return Failure("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The cancellation was cancelled.");

            lock (sync)
            {
                if (disposed || terminal) return OperationResult<bool>.Succeeded(false);
                if (events.Count < 2) return Failure("runtime.task_not_started", FrameworkErrorCategory.Conflict, "The AI task has not reached the started state.");
                if (cancellationDelegate != null)
                {
                    if (cancellationInFlight) return Failure("runtime.cancel_in_progress", FrameworkErrorCategory.Conflict, "A cancellation request is already in progress.");
                    cancellationInFlight = true;
                }
            }

            if (cancellationDelegate == null) return PublishLocalCancellation();

            try
            {
                var remoteResult = await cancellationDelegate(cancellationToken).ConfigureAwait(false);
                if (!remoteResult.IsSuccess || !remoteResult.Value) return remoteResult;
                var published = PublishConfirmedCancellation();
                if (published.IsSuccess && published.Value) SignalCancellation();
                return published;
            }
            catch (OperationCanceledException)
            {
                return Failure("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The cancellation was cancelled.");
            }
            catch (Exception)
            {
                return Failure("runtime.cancel_failed", FrameworkErrorCategory.Unavailable, "The runtime service could not process the cancellation request.");
            }
            finally
            {
                lock (sync) cancellationInFlight = false;
            }
        }

    private OperationResult<bool> PublishLocalCancellation()
        {
            try
            {
                var published = PublishConfirmedCancellation();
                if (published.IsSuccess && published.Value) SignalCancellation();
                return published;
            }
            finally
            {
                lock (sync) cancellationInFlight = false;
            }
        }

        private OperationResult<bool> PublishConfirmedCancellation()
        {
            return PublishTerminal(AiTaskEventKind.Cancelled, string.Empty, null, string.Empty, 0, 0);
        }

        internal OperationResult<bool> PublishTerminal(
            AiTaskEventKind kind,
            string text,
            FrameworkError error,
            string resolvedModel,
            int inputTokens,
            int outputTokens,
            string structuredJson = "",
            string activeProviderId = "",
            string fromProviderId = "",
            string toProviderId = "")
        {
            if (!IsTerminal(kind)) return Failure("runtime.terminal_kind_invalid", FrameworkErrorCategory.InvalidRequest, "The AI task event is not terminal.");

            AiTaskEvent taskEvent;
            List<Action<AiTaskEvent>> callbacks;
            lock (sync)
            {
                if (disposed) return Failure("runtime.task_disposed", FrameworkErrorCategory.Expired, "The AI task handle has been disposed.");
                if (terminal) return OperationResult<bool>.Succeeded(false);

                taskEvent = new AiTaskEvent(TaskId, MessageId, kind, nextSequence + 1, text, error, resolvedModel, inputTokens, outputTokens, structuredJson, activeProviderId, fromProviderId, toProviderId);
                if (!ValidateTransition(taskEvent, out var transitionError)) return Failure(transitionError, FrameworkErrorCategory.InvalidRequest, "The AI task event is not valid for the current state.");
                if (!StructuredJsonContract.TryValidateObject(taskEvent.StructuredJson, out var structuredError)) return Failure(structuredError, FrameworkErrorCategory.InvalidRequest, "The structured AI result is invalid.");

                events.Add(taskEvent);
                nextSequence = taskEvent.Sequence;
                terminal = true;
                callbacks = subscribers.ToList();
            }

            for (var index = 0; index < callbacks.Count; index++) callbacks[index](taskEvent);
            return OperationResult<bool>.Succeeded(true);
        }

        private void SignalCancellation()
        {
            try { if (cancellationSignal != null) cancellationSignal(); } catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                subscribers.Clear();
            }
        }

        private bool ValidateTransition(AiTaskEvent taskEvent, out string error)
        {
            error = string.Empty;
            if (events.Count == 0)
            {
                if (taskEvent.Kind != AiTaskEventKind.Accepted) error = "runtime.accepted_event_required";
                return string.IsNullOrEmpty(error);
            }

            if (events.Count == 1)
            {
                if (taskEvent.Kind != AiTaskEventKind.Started) error = "runtime.started_event_required";
                return string.IsNullOrEmpty(error);
            }

            if (taskEvent.Kind == AiTaskEventKind.Accepted || taskEvent.Kind == AiTaskEventKind.Started)
            {
                error = "runtime.lifecycle_event_duplicate";
                return false;
            }

            if (taskEvent.Kind == AiTaskEventKind.TextDelta && string.IsNullOrEmpty(taskEvent.Text))
            {
                error = "runtime.text_delta_empty";
                return false;
            }

            if (taskEvent.Kind == AiTaskEventKind.Completed && !string.IsNullOrEmpty(taskEvent.Text))
            {
                error = "runtime.completed_text_invalid";
                return false;
            }

            if (!string.IsNullOrEmpty(taskEvent.StructuredJson) && taskEvent.Kind != AiTaskEventKind.Completed)
            {
                error = "runtime.structured_json_terminal_only";
                return false;
            }

            if (!ProviderProtocolContract.IsValidUsage(taskEvent.InputTokens, taskEvent.OutputTokens))
            {
                error = "runtime.usage_invalid";
                return false;
            }

            if (taskEvent.Kind == AiTaskEventKind.RouteChanged)
            {
                if (visibleText) error = "runtime.route_change_after_visible_text";
                else if (string.IsNullOrWhiteSpace(taskEvent.FromProviderId) || string.IsNullOrWhiteSpace(taskEvent.ToProviderId) || StringComparer.Ordinal.Equals(taskEvent.FromProviderId, taskEvent.ToProviderId)) error = "runtime.route_change_invalid";
            }

            if (taskEvent.Kind == AiTaskEventKind.Failed && taskEvent.Error == null) error = "runtime.failed_event_error_missing";
            if (IsTerminal(taskEvent.Kind) && taskEvent.Kind != AiTaskEventKind.Failed && taskEvent.Error != null) error = "runtime.terminal_error_invalid";
            return string.IsNullOrEmpty(error);
        }

        private static bool IsTerminal(AiTaskEventKind kind)
        {
            return kind == AiTaskEventKind.Completed || kind == AiTaskEventKind.Cancelled || kind == AiTaskEventKind.Failed;
        }

        private OperationResult<bool> Failure(string code, FrameworkErrorCategory category, string fallback)
        {
            return OperationResult<bool>.Failed(FrameworkErrors.Create(code, category, fallback, MessageId));
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action dispose;
            private int disposed;

            internal Subscription(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0) dispose();
            }
        }

        private sealed class NoopSubscription : IDisposable
        {
            internal static readonly NoopSubscription Instance = new NoopSubscription();
            public void Dispose()
            {
            }
        }
    }
}
