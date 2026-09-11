using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService;

internal sealed class ProviderStreamBudget
{
    internal ProviderStreamBudget(int frameCount, int outputBytes, int tokens, int textDeltas)
    {
        FrameCount = frameCount;
        OutputBytes = outputBytes;
        Tokens = tokens;
        TextDeltas = textDeltas;
    }

    internal int FrameCount { get; }
    internal int OutputBytes { get; }
    internal int Tokens { get; }
    internal int TextDeltas { get; }
}

internal sealed class ProviderStreamEventProjection
{
    internal ProviderStreamEventProjection(string eventKind, long sequence, string providerId, string modelId, string text, int? inputTokens, int? outputTokens, ProviderWireError? error, string fromProviderId, string toProviderId, string? structuredJson)
    {
        EventKind = eventKind;
        Sequence = sequence;
        ProviderId = providerId;
        ModelId = modelId;
        Text = text;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        Error = error;
        FromProviderId = fromProviderId;
        ToProviderId = toProviderId;
        StructuredJson = structuredJson;
    }

    internal string EventKind { get; }
    internal long Sequence { get; }
    internal string ProviderId { get; }
    internal string ModelId { get; }
    internal string Text { get; }
    internal int? InputTokens { get; }
    internal int? OutputTokens { get; }
    internal ProviderWireError? Error { get; }
    internal string FromProviderId { get; }
    internal string ToProviderId { get; }
    internal string? StructuredJson { get; }
}

internal sealed class ProviderStreamStep
{
    private ProviderStreamStep(bool end, ProviderStreamEventProjection? value, ProviderWireError? error)
    {
        End = end;
        Value = value;
        Error = error;
    }

    internal bool End { get; }
    internal ProviderStreamEventProjection? Value { get; }
    internal ProviderWireError? Error { get; }

    internal static ProviderStreamStep EndOfStream() => new ProviderStreamStep(true, null, null);
    internal static ProviderStreamStep Success(ProviderStreamEventProjection value) => new ProviderStreamStep(false, value, null);
    internal static ProviderStreamStep Failure(ProviderWireError error) => new ProviderStreamStep(false, null, error);
}

internal sealed class ProviderStreamBeginResult
{
    private ProviderStreamBeginResult(ProviderStreamSession? session, ProviderWireError? error)
    {
        Session = session;
        Error = error;
    }

    internal ProviderStreamSession? Session { get; }
    internal ProviderWireError? Error { get; }
    internal bool IsSuccess => Session != null && Error == null;

    internal static ProviderStreamBeginResult Success(ProviderStreamSession session) => new ProviderStreamBeginResult(session, null);
    internal static ProviderStreamBeginResult Failure(ProviderWireError error) => new ProviderStreamBeginResult(null, error);
}

internal sealed class ProviderCredentialLoadResult
{
    private ProviderCredentialLoadResult(object? credential, ProviderWireError? error)
    {
        Credential = credential;
        Error = error;
    }

    internal object? Credential { get; }
    internal ProviderWireError? Error { get; }

    internal static ProviderCredentialLoadResult Success(object? credential) => new ProviderCredentialLoadResult(credential, null);
    internal static ProviderCredentialLoadResult Failure(ProviderWireError error) => new ProviderCredentialLoadResult(null, error);
}

internal sealed class ProviderCredentialLease
{
    private readonly object? credential;
    private int disposed;

    internal ProviderCredentialLease(object? credential)
    {
        this.credential = credential;
    }

    internal object? Credential => credential;

    internal ProviderWireError? DisposeSafe()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0 || credential == null) return null;
        try
        {
            if (credential is IDisposable disposable)
            {
                disposable.Dispose();
                return null;
            }

            var dispose = credential.GetType().GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            dispose?.Invoke(credential, null);
            return null;
        }
        catch (TargetInvocationException exception)
        {
            return ProviderStreamErrors.DisposeFailure(exception.InnerException);
        }
        catch (Exception exception)
        {
            return ProviderStreamErrors.DisposeFailure(exception);
        }
    }
}

internal sealed class ProviderStreamSession : IAsyncDisposable
{
    private readonly object enumerator;
    private readonly PropertyInfo currentProperty;
    private readonly MethodInfo moveNextMethod;
    private readonly MethodInfo disposeMethod;
    private readonly IReadOnlyList<ProviderCredentialLease> credentialLeases;
    private readonly Func<ProviderWireError> cancellationErrorFactory;
    private readonly DateTimeOffset deadline;
    private readonly CancellationToken cancellationToken;
    private readonly object lifecycleGate = new object();
    private Task<bool>? inFlightMoveNext;
    private int disposed;

    internal ProviderStreamSession(object enumerator, PropertyInfo currentProperty, MethodInfo moveNextMethod, MethodInfo disposeMethod, IReadOnlyList<ProviderCredentialLease> credentialLeases, DateTimeOffset deadline, CancellationToken cancellationToken, Func<ProviderWireError> cancellationErrorFactory)
    {
        this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        this.currentProperty = currentProperty ?? throw new ArgumentNullException(nameof(currentProperty));
        this.moveNextMethod = moveNextMethod ?? throw new ArgumentNullException(nameof(moveNextMethod));
        this.disposeMethod = disposeMethod ?? throw new ArgumentNullException(nameof(disposeMethod));
        this.credentialLeases = credentialLeases ?? throw new ArgumentNullException(nameof(credentialLeases));
        this.deadline = deadline;
        this.cancellationToken = cancellationToken;
        this.cancellationErrorFactory = cancellationErrorFactory ?? throw new ArgumentNullException(nameof(cancellationErrorFactory));
    }

    internal async Task<ProviderStreamStep> MoveNextAsync(CancellationToken cancellationToken)
    {
        Task<bool> operation;
        try
        {
            lock (lifecycleGate)
            {
                if (Volatile.Read(ref disposed) != 0) return ProviderStreamStep.Failure(ProviderStreamErrors.Disposed());
                var result = moveNextMethod.Invoke(enumerator, null);
                operation = ProviderReflectionAsync.AwaitBooleanAsync(result);
                inFlightMoveNext = operation;
                _ = ClearInFlightMoveNextAsync(operation);
            }

            var wait = await ProviderReflectionAsync.AwaitUntilDeadlineAsync(operation, deadline, cancellationToken, cancellationErrorFactory).ConfigureAwait(false);
            if (!wait.Completed) return ProviderStreamStep.Failure(wait.Error!);
            var hasNext = wait.Value;
            if (!hasNext) return ProviderStreamStep.EndOfStream();
            var current = currentProperty.GetValue(enumerator);
            if (!ProviderStreamEventProjectionReader.TryRead(current, out var value, out var error)) return ProviderStreamStep.Failure(error!);
            return ProviderStreamStep.Success(value!);
        }
        catch (TargetInvocationException exception)
        {
            return ProviderStreamStep.Failure(ProviderStreamErrors.MapException(exception.InnerException, cancellationErrorFactory));
        }
        catch (OperationCanceledException)
        {
            return ProviderStreamStep.Failure(cancellationErrorFactory());
        }
        catch (Exception exception)
        {
            return ProviderStreamStep.Failure(ProviderStreamErrors.MapException(exception, cancellationErrorFactory));
        }
    }

    internal async Task<ProviderWireError?> DisposeSafeAsync()
    {
        Task<bool>? pendingMoveNext;
        lock (lifecycleGate)
        {
            if (Volatile.Read(ref disposed) != 0) return null;
            Volatile.Write(ref disposed, 1);
            pendingMoveNext = inFlightMoveNext;
        }

        if (pendingMoveNext != null)
        {
            var moveNextCompleted = false;
            try
            {
                moveNextCompleted = await ProviderReflectionAsync.AwaitWithGraceAsync(pendingMoveNext, 250).ConfigureAwait(false);
            }
            catch
            {
                moveNextCompleted = true;
            }

            if (!moveNextCompleted)
            {
                _ = FinishDeferredMoveNextCleanupAsync(pendingMoveNext);
                return ProviderStreamErrors.CleanupDeferred();
            }
        }

        return await DisposeEnumeratorAndLeasesAsync().ConfigureAwait(false);
    }

    private async Task<ProviderWireError?> DisposeEnumeratorAndLeasesAsync()
    {
        ProviderWireError? firstError = null;
        try
        {
            var result = disposeMethod.Invoke(enumerator, null);
            var operation = ProviderReflectionAsync.AwaitCompletionAsync(result);
            var completed = await ProviderReflectionAsync.AwaitWithGraceAsync(operation, 250).ConfigureAwait(false);
            if (!completed)
            {
                _ = FinishDeferredDisposeAsync(operation);
                return ProviderStreamErrors.CleanupDeferred();
            }
        }
        catch (TargetInvocationException exception)
        {
            firstError = IsCancellationCleanup(exception.InnerException) ? null : ProviderStreamErrors.DisposeFailure(exception.InnerException);
        }
        catch (OperationCanceledException)
        {
            firstError = null;
        }
        catch (Exception exception)
        {
            firstError = IsCancellationCleanup(exception) ? null : ProviderStreamErrors.DisposeFailure(exception);
        }

        for (var index = 0; index < credentialLeases.Count; index++)
        {
            var error = credentialLeases[index].DisposeSafe();
            if (firstError == null && error != null) firstError = error;
        }

        return firstError;
    }

    private async Task ClearInFlightMoveNextAsync(Task<bool> operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
        }

        lock (lifecycleGate)
        {
            if (ReferenceEquals(inFlightMoveNext, operation)) inFlightMoveNext = null;
        }
    }

    private bool IsCancellationCleanup(Exception? exception)
    {
        return cancellationToken.IsCancellationRequested && exception is OperationCanceledException;
    }

    private async Task FinishDeferredDisposeAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
        }

        for (var index = 0; index < credentialLeases.Count; index++) credentialLeases[index].DisposeSafe();
    }

    private async Task FinishDeferredMoveNextCleanupAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            await DisposeEnumeratorAndLeasesAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    public ValueTask DisposeAsync() => new ValueTask(DisposeSafeAsync());
}

internal static class ProviderStreamEventProjectionReader
{
    internal static bool TryRead(object? value, out ProviderStreamEventProjection? projection, out ProviderWireError? error)
    {
        projection = null;
        error = null;
        if (value == null)
        {
            error = ProviderStreamErrors.BridgeInvalid();
            return false;
        }

        try
        {
            var type = value.GetType();
            var kind = type.GetProperty("Kind", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value)?.ToString();
            var providerId = type.GetProperty("ProviderId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) as string;
            var modelId = type.GetProperty("ModelId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) as string;
            var text = type.GetProperty("Text", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) as string;
            var sequence = ReadLong(type.GetProperty("Sequence", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value));
            var usage = type.GetProperty("Usage", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);
            var providerError = type.GetProperty("Error", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);
            var fromProviderId = type.GetProperty("FromProviderId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) as string;
            var toProviderId = type.GetProperty("ToProviderId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) as string;
            var structuredJson = type.GetProperty("StructuredJson", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) as string;
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(providerId) || sequence < 1)
            {
                error = ProviderStreamErrors.BridgeInvalid();
                return false;
            }

            projection = new ProviderStreamEventProjection(
                ToWireKind(kind),
                sequence,
                providerId,
                modelId ?? string.Empty,
                text ?? string.Empty,
                ReadNullableInt(usage, "InputTokens"),
                ReadNullableInt(usage, "OutputTokens"),
                providerError == null ? null : ProviderRegistry.MapProviderError(providerError),
                fromProviderId ?? string.Empty,
                toProviderId ?? string.Empty,
                structuredJson);
            if (string.IsNullOrWhiteSpace(projection.EventKind))
            {
                error = ProviderStreamErrors.BridgeInvalid();
                projection = null;
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = ProviderStreamErrors.MapException(exception, () => ProviderWireAdapter.Cancelled());
            return false;
        }
    }

    private static string ToWireKind(string value)
    {
        return value switch
        {
            "Started" => "started",
            "TextDelta" => "text_delta",
            "UsageUpdate" => "usage_update",
            "RouteChanged" => "route_changed",
            "Completed" => "completed",
            "Cancelled" => "cancelled",
            "Failed" => "failed",
            _ => string.Empty
        };
    }

    private static long ReadLong(object? value)
    {
        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            _ => 0
        };
    }

    private static int? ReadNullableInt(object? value, string propertyName)
    {
        if (value == null) return null;
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        var raw = property?.GetValue(value);
        if (raw is int intValue) return intValue;
        var nullableValue = raw?.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.GetValue(raw);
        return nullableValue is int nullableInt ? nullableInt : null;
    }
}

internal static class ProviderReflectionAsync
{
    internal static async Task<bool> AwaitBooleanAsync(object? value)
    {
        if (value is Task<bool> task) return await task.ConfigureAwait(false);
        if (value is ValueTask<bool> valueTask) return await valueTask.ConfigureAwait(false);
        if (value != null && value.GetType().IsValueType && value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTask = value.GetType().GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance)?.Invoke(value, null) as Task<bool>;
            if (asTask != null) return await asTask.ConfigureAwait(false);
        }

        throw new InvalidOperationException("provider_stream_bridge_invalid");
    }

    internal static async Task AwaitCompletionAsync(object? value)
    {
        if (value is Task task)
        {
            await task.ConfigureAwait(false);
            return;
        }

        if (value != null && value.GetType().IsValueType && value.GetType().FullName == "System.Threading.Tasks.ValueTask")
        {
            var asTask = value.GetType().GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance)?.Invoke(value, null) as Task;
            if (asTask != null)
            {
                await asTask.ConfigureAwait(false);
                return;
            }
        }

        throw new InvalidOperationException("provider_stream_bridge_invalid");
    }

    internal static async Task<ProviderWaitResult<bool>> AwaitUntilDeadlineAsync(Task<bool> operation, DateTimeOffset deadline, CancellationToken cancellationToken, Func<ProviderWireError> cancellationErrorFactory)
    {
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return ProviderWaitResult<bool>.TimedOut(cancellationErrorFactory());
        var deadlineTask = Task.Delay(remaining);
        var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
        var completed = await Task.WhenAny(operation, deadlineTask, cancellationTask).ConfigureAwait(false);
        if (completed == operation) return ProviderWaitResult<bool>.CompletedValue(await operation.ConfigureAwait(false));
        _ = ObserveAsync(operation);
        return ProviderWaitResult<bool>.TimedOut(cancellationErrorFactory());
    }

    internal static async Task<bool> AwaitWithGraceAsync(Task operation, int graceMilliseconds)
    {
        var completed = await Task.WhenAny(operation, Task.Delay(graceMilliseconds)).ConfigureAwait(false);
        if (completed != operation)
        {
            _ = ObserveAsync(operation);
            return false;
        }

        await operation.ConfigureAwait(false);
        return true;
    }

    private static async Task ObserveAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
        }
    }
}

internal sealed class ProviderWaitResult<T>
{
    private ProviderWaitResult(bool completed, T value, ProviderWireError? error)
    {
        Completed = completed;
        Value = value;
        Error = error;
    }

    internal bool Completed { get; }
    internal T Value { get; }
    internal ProviderWireError? Error { get; }

    internal static ProviderWaitResult<T> CompletedValue(T value) => new ProviderWaitResult<T>(true, value, null);
    internal static ProviderWaitResult<T> TimedOut(ProviderWireError error) => new ProviderWaitResult<T>(false, default!, error);
}

internal static class ProviderStreamErrors
{
    internal static ProviderWireError BridgeInvalid()
    {
        return new ProviderWireError("provider_stream_bridge_invalid", "internal_failure", false, false, "Provider stream bridge is unavailable.");
    }

    internal static ProviderWireError Disposed()
    {
        return new ProviderWireError("provider_stream_session_disposed", "internal_failure", false, false, "Provider stream session is unavailable.");
    }

    internal static ProviderWireError DisposeFailure(Exception? exception)
    {
        return new ProviderWireError("provider_stream_dispose_failed", "internal_failure", false, false, "Provider stream cleanup failed.");
    }

    internal static ProviderWireError CleanupDeferred()
    {
        return new ProviderWireError("provider_stream_cleanup_deferred", "timeout", false, false, "Provider stream cleanup continues in the background.");
    }

    internal static ProviderWireError MapException(Exception? exception, Func<ProviderWireError> cancellationErrorFactory)
    {
        if (exception is OperationCanceledException) return cancellationErrorFactory();
        if (exception is InvalidOperationException invalid && StringComparer.Ordinal.Equals(invalid.Message, "provider_stream_bridge_invalid")) return BridgeInvalid();
        return new ProviderWireError("provider_stream_runtime_failure", "internal_failure", false, false, "Provider stream failed.");
    }
}
