using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace MarcusAwakeFramework.Tests.TestDoubles
{
    internal sealed class FixtureRouteProfileResolver : IRouteProfileResolver
    {
        private readonly Dictionary<string, RouteProfile> profiles = new Dictionary<string, RouteProfile>(StringComparer.Ordinal);

        internal FixtureRouteProfileResolver()
        {
            Add(new RouteProfile("dialogue", "openai-compatible", "default", "fixture-model", true, new[] { "text", "structured_output" }));
            Add(new RouteProfile("dialogue", "anthropic", "default", "fixture-model", true, new[] { "text", "structured_output" }));
            Add(new RouteProfile("dialogue", "ollama", "default", "fixture-model", false, new[] { "text", "structured_output" }));
        }

        internal void Add(RouteProfile profile) { profiles[Key(profile.RouteId, profile.ProviderId, profile.ProfileId)] = profile; }

        public OperationResult<RouteProfile> Resolve(string routeId, string providerId, string profileId, RequestContext context)
        {
            RouteProfile profile;
            if (!profiles.TryGetValue(Key(routeId, providerId, profileId), out profile)) return OperationResult<RouteProfile>.Failed(FrameworkErrors.Create("route.profile_unavailable", FrameworkErrorCategory.Unavailable, "The route profile is unavailable.", context?.CorrelationId ?? "fixture-route", retryable: true));
            return OperationResult<RouteProfile>.Succeeded(profile);
        }

        private static string Key(string routeId, string providerId, string profileId) => routeId + "|" + providerId + "|" + profileId;
    }

    internal sealed class FixtureAiGateway : IAiGateway
    {
        private readonly object sync = new object();
        private readonly FixtureRouteProfileResolver routes;
        private readonly Dictionary<string, FixtureTaskHandle> handles = new Dictionary<string, FixtureTaskHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, AiTaskReceipt> receipts = new Dictionary<string, AiTaskReceipt>(StringComparer.Ordinal);

        internal FixtureAiGateway(FixtureRouteProfileResolver routes) { this.routes = routes ?? throw new ArgumentNullException(nameof(routes)); }

        internal RequestContext LastContext { get; private set; }
        internal AiTaskRequest LastRequest { get; private set; }
        internal AiTaskScope LastScope { get; private set; }

        public Task<OperationResult<IAiTaskHandle>> SubmitAsync(AiTaskRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            if (request == null || context == null) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(FrameworkErrors.Create("fixture.invalid_request", FrameworkErrorCategory.InvalidRequest, "The fixture request is invalid.", context?.CorrelationId ?? "fixture-submit")));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(FrameworkErrors.Create("fixture.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId)));
            if (context.AiTaskScope == null) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(FrameworkErrors.Create("fixture.scope_missing", FrameworkErrorCategory.InvalidRequest, "The AI task scope is missing.", context.CorrelationId)));
            var route = routes.Resolve(request.RouteId, request.ProviderId, request.ProfileId, context);
            if (!route.IsSuccess) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(route.Error));
            if (!StringComparer.Ordinal.Equals(context.AiTaskScope.TaskId, request.TaskId)) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(FrameworkErrors.Create("fixture.scope_task_mismatch", FrameworkErrorCategory.Conflict, "The AI task scope does not match the request.", context.CorrelationId)));

            lock (sync)
            {
                LastContext = context;
                LastRequest = request;
                LastScope = context.AiTaskScope;
                FixtureTaskHandle existing;
                if (handles.TryGetValue(context.AiTaskScope.DeduplicationKey, out existing)) return Task.FromResult(OperationResult<IAiTaskHandle>.Succeeded(existing));
                var handle = new FixtureTaskHandle(request.TaskId, request.MessageId);
                handles.Add(context.AiTaskScope.DeduplicationKey, handle);
                handle.Subscribe(item => RecordTerminal(context.AiTaskScope, request, item));
                handle.Emit(AiTaskEventKind.Accepted, string.Empty, null, route.Value.ModelId, 0, 0);
                handle.Emit(AiTaskEventKind.Started, string.Empty, null, route.Value.ModelId, 0, 0);
                return Task.FromResult(OperationResult<IAiTaskHandle>.Succeeded(handle));
            }
        }

        public Task<OperationResult<AiTaskReceipt>> GetReceiptAsync(AiTaskScope scope, RequestContext context, CancellationToken cancellationToken)
        {
            if (scope == null || context == null) return Task.FromResult(OperationResult<AiTaskReceipt>.Failed(FrameworkErrors.Create("fixture.invalid_request", FrameworkErrorCategory.InvalidRequest, "A task scope and context are required.", context?.CorrelationId ?? "fixture-receipt")));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(OperationResult<AiTaskReceipt>.Failed(FrameworkErrors.Create("fixture.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId)));
            lock (sync)
            {
                AiTaskReceipt receipt;
                if (!receipts.TryGetValue(scope.DeduplicationKey, out receipt)) return Task.FromResult(OperationResult<AiTaskReceipt>.Failed(FrameworkErrors.Create("fixture.receipt_not_ready", FrameworkErrorCategory.Unavailable, "The task receipt is not ready.", context.CorrelationId, retryable: true)));
                return Task.FromResult(OperationResult<AiTaskReceipt>.Succeeded(receipt));
            }
        }

        internal bool Complete(string taskId, string text, int inputTokens, int outputTokens)
        {
            FixtureTaskHandle handle;
            lock (sync) handle = handles.Values.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.TaskId, taskId));
            if (handle == null) return false;
            if (!handle.Emit(AiTaskEventKind.TextDelta, text, null, "fixture-model", inputTokens, outputTokens)) return false;
            if (!handle.Emit(AiTaskEventKind.UsageUpdate, string.Empty, null, "fixture-model", inputTokens, outputTokens)) return false;
            return handle.Emit(AiTaskEventKind.Completed, string.Empty, null, "fixture-model", inputTokens, outputTokens);
        }

        internal Task<OperationResult<bool>> Cancel(string taskId, CancellationToken cancellationToken)
        {
            FixtureTaskHandle handle;
            lock (sync) handle = handles.Values.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.TaskId, taskId));
            if (handle == null) return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create("fixture.task_not_found", FrameworkErrorCategory.NotFound, "The fixture task was not found.", "fixture-cancel")));
            return handle.CancelAsync(cancellationToken);
        }

        internal FixtureTaskHandle Find(string taskId)
        {
            lock (sync) return handles.Values.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.TaskId, taskId));
        }

        private void RecordTerminal(AiTaskScope scope, AiTaskRequest request, AiTaskEvent item)
        {
            if (item == null || (item.Kind != AiTaskEventKind.Completed && item.Kind != AiTaskEventKind.Cancelled && item.Kind != AiTaskEventKind.Failed)) return;
            lock (sync)
            {
                if (receipts.ContainsKey(scope.DeduplicationKey)) return;
                var status = item.Kind == AiTaskEventKind.Completed ? "completed" : item.Kind == AiTaskEventKind.Cancelled ? "cancelled" : "failed";
                receipts[scope.DeduplicationKey] = new AiTaskReceipt("receipt-" + request.TaskId, request, scope, scope.CorrelationId, scope.CausationId, scope.Session, scope.SessionGeneration, status, string.Empty);
            }
        }
    }
}
