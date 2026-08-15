using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Awake;

internal static class AwakeUiDispatcher
{
    private static readonly ConcurrentQueue<Action> Actions = new ConcurrentQueue<Action>();
    private static int _draining;
    private static int _gameThreadId;

    internal static void InitializeGameThread()
    {
        int current = Thread.CurrentThread.ManagedThreadId;
        Interlocked.CompareExchange(ref _gameThreadId, current, 0);
    }

    internal static void ResetGameThreadForTesting()
    {
        Interlocked.Exchange(ref _gameThreadId, 0);
    }

    internal static Task<T> RunOnGameThreadAsync<T>(Func<Task<T>> factory, CancellationToken cancellationToken)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        // No observed game thread yet (tests/early init) or already on game thread: run directly,
        // otherwise waiting for a tick here would deadlock or never complete.
        if (_gameThreadId == 0 || Thread.CurrentThread.ManagedThreadId == _gameThreadId)
        {
            return factory();
        }

        TaskCompletionSource<T> completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Actions.Enqueue(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }
            try
            {
                Task<T> task = factory();
                if (task == null)
                {
                    completion.TrySetException(new InvalidOperationException("Main-thread factory returned null task."));
                    return;
                }
                task.ContinueWith(
                    completed =>
                    {
                        if (completed.IsCanceled)
                        {
                            completion.TrySetCanceled();
                        }
                        else if (completed.IsFaulted)
                        {
                            completion.TrySetException(completed.Exception ?? new Exception("Permission request failed."));
                        }
                        else
                        {
                            completion.TrySetResult(completed.Result);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken), useSynchronizationContext: false);
        }
        return completion.Task;
    }

    internal static void Enqueue(Action action)
    {
        if (action == null) return;
        Actions.Enqueue(action);
    }

    internal static void Drain()
    {
        if (Interlocked.CompareExchange(ref _draining, 1, 0) != 0) return;
        try
        {
            while (Actions.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("ui_dispatch_action_error error=" + ex.Message);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _draining, 0);
        }
    }
}
