using MarcusAwakeRuntimeService;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService.Tests;

internal static class ProviderStreamModelsTests
{
    internal static async Task<int> RunAsync()
    {
        var passed = 0;
        var failed = 0;
        await RunCaseAsync("cancelled_cleanup_does_not_hide_non_cancellation_failure", async () =>
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var session = CreateSession(
                new TestEnumerator(() => Task.FromException(new InvalidOperationException("cleanup-bug"))),
                cancellation.Token);
            var error = await session.DisposeSafeAsync().ConfigureAwait(false);
            Require(error != null && error.ErrorCode == "provider_stream_dispose_failed", "non_cancellation_cleanup_failure_was_hidden");
            passed++;
        }).ConfigureAwait(false);

        await RunCaseAsync("cancelled_cleanup_ignores_operation_cancellation", async () =>
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var session = CreateSession(
                new TestEnumerator(() => Task.FromCanceled(new CancellationToken(true))),
                cancellation.Token);
            var error = await session.DisposeSafeAsync().ConfigureAwait(false);
            Require(error == null, "operation_cancellation_was_reported_as_cleanup_failure");
            passed++;
        }).ConfigureAwait(false);

        await RunCaseAsync("dispose_waits_for_inflight_move_next", async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var enumerator = new PendingEnumerator();
            var session = CreateSession(enumerator, cancellation.Token);
            var moveTask = session.MoveNextAsync(cancellation.Token);
            await enumerator.Started.Task.ConfigureAwait(false);
            cancellation.Cancel();
            var moveStep = await moveTask.ConfigureAwait(false);
            Require(moveStep.Error != null && moveStep.Error.ErrorCode == "request.cancelled", "cancelled_move_did_not_return_typed_error");
            var disposeTask = session.DisposeSafeAsync();
            await Task.Delay(25).ConfigureAwait(false);
            Require(!enumerator.DisposeCalled, "enumerator_disposed_while_move_was_in_flight");
            enumerator.Completion.TrySetResult(false);
            var disposeError = await disposeTask.ConfigureAwait(false);
            Require(disposeError == null && enumerator.DisposeCalled, "inflight_move_cleanup_did_not_complete");
            passed++;
        }).ConfigureAwait(false);

        Console.WriteLine("PROVIDER_STREAM_MODELS " + passed.ToString() + "/" + (passed + failed).ToString() + (failed == 0 ? " PASS" : " FAIL"));
        return failed == 0 ? 0 : 1;

        async Task RunCaseAsync(string name, Func<Task> action)
        {
            Console.WriteLine("CASE " + name + " START");
            try
            {
                await action().ConfigureAwait(false);
                Console.WriteLine("CASE " + name + " PASS");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine("CASE " + name + " FAIL " + exception.GetType().Name + ":" + exception.Message);
            }
        }
    }

    private static ProviderStreamSession CreateSession(object enumerator, CancellationToken cancellationToken)
    {
        var type = enumerator.GetType();
        return new ProviderStreamSession(
            enumerator,
            type.GetProperty(nameof(TestEnumerator.Current), BindingFlags.Public | BindingFlags.Instance)!,
            type.GetMethod(nameof(TestEnumerator.MoveNextAsync), BindingFlags.Public | BindingFlags.Instance)!,
            type.GetMethod(nameof(TestEnumerator.DisposeAsync), BindingFlags.Public | BindingFlags.Instance)!,
            Array.Empty<ProviderCredentialLease>(),
            DateTimeOffset.UtcNow.AddMinutes(1),
            cancellationToken,
            () => new ProviderWireError("request.cancelled", "cancelled", false, false, "cancelled"));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TestEnumerator
    {
        private readonly Func<Task> dispose;

        internal TestEnumerator(Func<Task> dispose)
        {
            this.dispose = dispose;
        }

        public object Current => new object();

        public Task<bool> MoveNextAsync() => Task.FromResult(false);

        public Task DisposeAsync() => dispose();
    }

    private sealed class PendingEnumerator
    {
        internal readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool DisposeCalled { get; private set; }

        public object Current => new object();

        public Task<bool> MoveNextAsync()
        {
            Started.TrySetResult(true);
            return Completion.Task;
        }

        public Task DisposeAsync()
        {
            DisposeCalled = true;
            return Task.CompletedTask;
        }
    }
}
