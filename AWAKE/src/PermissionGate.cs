using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;

namespace Awake;

internal sealed class PermissionGateResult
{
    internal bool Granted { get; }
    internal string PermissionId { get; }
    internal string Purpose { get; }
    internal bool CanDegrade { get; }
    internal bool WasRequested { get; }
    internal FrameworkError Error { get; }

    internal PermissionGateResult(
        bool granted,
        string permissionId,
        string purpose,
        bool canDegrade,
        bool wasRequested,
        FrameworkError error)
    {
        Granted = granted;
        PermissionId = permissionId ?? string.Empty;
        Purpose = purpose ?? string.Empty;
        CanDegrade = canDegrade;
        WasRequested = wasRequested;
        Error = error;
    }
}

internal sealed class PermissionGate
{
    private const string Owner = AwakeConstants.OwnerValue;

    private readonly IMarcusAiFrameworkHost _host;
    private readonly object _gate = new object();
    private readonly Dictionary<string, SharedPermissionRequest> _active = new Dictionary<string, SharedPermissionRequest>(StringComparer.Ordinal);

    internal PermissionGate(IMarcusAiFrameworkHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    internal PermissionGateResult Evaluate(PermissionDefinition definition, RequestContext context)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
        {
            return Failure(definition, context, UnknownError(context?.CorrelationId));
        }
        if (!PermissionCatalog.TryGet(definition.Id, out _))
        {
            return Failure(definition, context, UnknownError(context?.CorrelationId));
        }
        if (context == null)
        {
            return Failure(definition, context, FrameworkErrors.Create(
                "awake.context_missing",
                FrameworkErrorCategory.InvalidRequest,
                "A request context is required.",
                null,
                owner: Owner));
        }
        if (context.IsExpired)
        {
            return Failure(definition, context, FrameworkErrors.Create(
                "awake.context_expired",
                FrameworkErrorCategory.Expired,
                "The request context expired.",
                context.CorrelationId,
                owner: Owner));
        }

        try
        {
            PermissionEvaluation evaluation = _host.Permissions.Evaluate(definition.Id, context);
            bool granted = evaluation != null && evaluation.Decision == PermissionDecision.Granted;
            AwakeLog.Write("permission_gate_evaluate permission=" + definition.Id + " decision=" + (granted ? "Granted" : "Denied"));
            if (granted)
            {
                return new PermissionGateResult(true, definition.Id, definition.Purpose, definition.CanDegrade, false, null);
            }
            return new PermissionGateResult(false, definition.Id, definition.Purpose, definition.CanDegrade, false, DeniedError(definition.Id, context.CorrelationId));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("permission_gate_evaluate_error permission=" + definition.Id + " error=" + ex.Message);
            return Failure(definition, context, FrameworkErrors.Create(
                "awake.permission_evaluate_error",
                FrameworkErrorCategory.InternalFailure,
                "Permission evaluation failed.",
                context.CorrelationId,
                owner: Owner));
        }
    }

    internal async Task<PermissionGateResult> EnsureAsync(
        PermissionDefinition definition,
        RequestContext context,
        CancellationToken cancellationToken,
        string purpose = null)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
        {
            return Failure(definition, context, UnknownError(context?.CorrelationId));
        }
        if (!PermissionCatalog.TryGet(definition.Id, out _))
        {
            return Failure(definition, context, UnknownError(context?.CorrelationId));
        }
        if (context == null)
        {
            return Failure(definition, context, FrameworkErrors.Create(
                "awake.context_missing",
                FrameworkErrorCategory.InvalidRequest,
                "A request context is required.",
                null,
                owner: Owner));
        }
        if (context.IsExpired)
        {
            return Failure(definition, context, FrameworkErrors.Create(
                "awake.context_expired",
                FrameworkErrorCategory.Expired,
                "The request context expired.",
                context.CorrelationId,
                owner: Owner));
        }
        if (cancellationToken.IsCancellationRequested)
        {
            return Failure(definition, context, CancelledError(context.CorrelationId));
        }

        PermissionGateResult evaluated = Evaluate(definition, context);
        if (evaluated.Granted) return evaluated;
        if (evaluated.Error == null || evaluated.Error.Category != FrameworkErrorCategory.Denied) return evaluated;

        string effectivePurpose = purpose ?? definition.Purpose;
        SharedPermissionRequest shared = null;
        bool creator = false;
        lock (_gate)
        {
            if (!_active.TryGetValue(definition.Id, out shared))
            {
                shared = new SharedPermissionRequest
                {
                    FirstContext = context,
                    Purpose = effectivePurpose,
                    SharedCts = new CancellationTokenSource()
                };
                _active[definition.Id] = shared;
                creator = true;
            }
            shared.ActiveWaiters++;
        }

        if (creator)
        {
            shared.Task = RequestCoreAsync(definition, shared, shared.FirstContext, shared.SharedCts.Token);
            AttachCleanup(definition.Id, shared);
        }

        try
        {
            TaskCompletionSource<bool> cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (CancellationTokenRegistration registration = cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(shared.Task, cancelled.Task).ConfigureAwait(false);
                if (ReferenceEquals(completed, cancelled.Task))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            OperationResult<PermissionEvaluation> result = await shared.Task.ConfigureAwait(false);
            ReleaseWaiter(definition.Id, shared, cancelIfLast: false);

            if (result == null || !result.IsSuccess || result.Value == null || result.Value.Decision != PermissionDecision.Granted)
            {
                FrameworkError error = result?.Error;
                if (error == null)
                {
                    error = DeniedError(definition.Id, context.CorrelationId);
                }
                else if (!StringComparer.Ordinal.Equals(error.CorrelationId, context.CorrelationId))
                {
                    error = RebindCorrelation(error, context.CorrelationId);
                }
                AwakeLog.Write("permission_gate_request permission=" + definition.Id + " granted=false code=" + error.Code + " correlation=" + context.CorrelationId);
                return new PermissionGateResult(false, definition.Id, effectivePurpose, definition.CanDegrade, true, error);
            }

            AwakeLog.Write("permission_gate_request permission=" + definition.Id + " granted=true code=none correlation=" + context.CorrelationId);
            return new PermissionGateResult(true, definition.Id, effectivePurpose, definition.CanDegrade, true, null);
        }
        catch (OperationCanceledException)
        {
            ReleaseWaiter(definition.Id, shared, cancelIfLast: true);
            return Failure(definition, context, CancelledError(context.CorrelationId));
        }
    }

    private async Task<OperationResult<PermissionEvaluation>> RequestCoreAsync(
        PermissionDefinition definition,
        SharedPermissionRequest shared,
        RequestContext context,
        CancellationToken sharedToken)
    {
        try
        {
            OperationResult<PermissionEvaluation> requested = await AwakeUiDispatcher.RunOnGameThreadAsync(
                () => _host.Permissions.RequestAsync(
                    definition.Id,
                    shared.Purpose,
                    context,
                    sharedToken),
                sharedToken).ConfigureAwait(false);
            return requested ?? OperationResult<PermissionEvaluation>.Failed(FrameworkErrors.Create(
                "awake.permission_request_failed",
                FrameworkErrorCategory.InternalFailure,
                "The permission request returned no result.",
                context.CorrelationId,
                owner: Owner));
        }
        catch (OperationCanceledException)
        {
            return OperationResult<PermissionEvaluation>.Failed(FrameworkErrors.Create(
                "awake.cancelled",
                FrameworkErrorCategory.Cancelled,
                "The permission request was cancelled.",
                context.CorrelationId,
                owner: Owner));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("permission_gate_request_error permission=" + definition.Id + " error=" + ex.Message);
            return OperationResult<PermissionEvaluation>.Failed(FrameworkErrors.Create(
                "awake.permission_request_error",
                FrameworkErrorCategory.InternalFailure,
                "The permission request failed.",
                context.CorrelationId,
                owner: Owner));
        }
    }

    private void AttachCleanup(string permissionId, SharedPermissionRequest shared)
    {
        _ = shared.Task.ContinueWith(
            _ =>
            {
                bool dispose = false;
                lock (_gate)
                {
                    if (shared.ActiveWaiters <= 0)
                    {
                        SharedPermissionRequest current;
                        if (_active.TryGetValue(permissionId, out current) && ReferenceEquals(current, shared))
                        {
                            _active.Remove(permissionId);
                            dispose = true;
                        }
                    }
                }
                if (dispose)
                {
                    try { shared.SharedCts?.Dispose(); } catch { }
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void ReleaseWaiter(string permissionId, SharedPermissionRequest shared, bool cancelIfLast)
    {
        bool cancel = false;
        bool dispose = false;
        lock (_gate)
        {
            shared.ActiveWaiters--;
            if (shared.ActiveWaiters <= 0)
            {
                bool taskCompleted = shared.Task != null && shared.Task.IsCompleted;
                if (cancelIfLast && !taskCompleted)
                {
                    cancel = true;
                }
                else
                {
                    SharedPermissionRequest current;
                    if (_active.TryGetValue(permissionId, out current) && ReferenceEquals(current, shared))
                    {
                        _active.Remove(permissionId);
                        dispose = true;
                    }
                }
            }
        }
        if (cancel)
        {
            try { shared.SharedCts?.Cancel(); } catch { }
        }
        if (dispose)
        {
            try { shared.SharedCts?.Dispose(); } catch { }
        }
    }

    private static FrameworkError DeniedError(string permissionId, string correlationId)
    {
        return FrameworkErrors.Create(
            "awake.permission_denied",
            FrameworkErrorCategory.Denied,
            "The permission was not granted: " + permissionId,
            correlationId,
            owner: Owner);
    }

    private static FrameworkError CancelledError(string correlationId)
    {
        return FrameworkErrors.Create(
            "awake.cancelled",
            FrameworkErrorCategory.Cancelled,
            "The permission request was cancelled.",
            correlationId,
            owner: Owner);
    }

    private static FrameworkError UnknownError(string correlationId)
    {
        return FrameworkErrors.Create(
            "awake.permission_unknown",
            FrameworkErrorCategory.InvalidRequest,
            "The permission is not defined in the catalog.",
            correlationId,
            owner: Owner);
    }

    private static PermissionGateResult Failure(PermissionDefinition definition, RequestContext context, FrameworkError error)
    {
        return new PermissionGateResult(
            false,
            definition?.Id ?? string.Empty,
            definition?.Purpose ?? string.Empty,
            definition?.CanDegrade ?? false,
            false,
            error);
    }

    private static FrameworkError RebindCorrelation(FrameworkError source, string correlationId)
    {
        return new FrameworkError(
            source.Code,
            source.Category,
            source.MessageTextId,
            source.SafeFallback,
            source.Retryable,
            source.Owner,
            correlationId,
            source.Details);
    }

    private sealed class SharedPermissionRequest
    {
        internal Task<OperationResult<PermissionEvaluation>> Task;
        internal CancellationTokenSource SharedCts;
        internal RequestContext FirstContext;
        internal string Purpose;
        internal int ActiveWaiters;
    }
}
