using System;
using System.Threading.Tasks;

namespace Awake;

internal static class AwakeBackgroundTask
{
    internal static Action<Task, string> ObserverForTesting { get; set; }

    internal static Task Run(Func<Task> taskFactory, string label)
    {
        if (taskFactory == null) return Task.CompletedTask;
        try
        {
            Task task = Task.Run(taskFactory);
            Observe(task, label);
            return task;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("background_task_start_failed label=" + label + " error=" + ex.Message);
            return Task.CompletedTask;
        }
    }

    internal static Task<T> Run<T>(Func<Task<T>> taskFactory, string label)
    {
        if (taskFactory == null) return Task.FromResult(default(T));
        try
        {
            Task<T> task = Task.Run(taskFactory);
            Observe(task, label);
            return task;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("background_task_start_failed label=" + label + " error=" + ex.Message);
            return Task.FromResult(default(T));
        }
    }

    private static void Observe(Task task, string label)
    {
        try
        {
            ObserverForTesting?.Invoke(task, label ?? string.Empty);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("background_task_observer_error label=" + label + " error=" + ex.Message);
        }
        _ = task.ContinueWith(
            completed =>
            {
                if (completed.IsFaulted)
                {
                    AwakeLog.Write(
                        "background_task_failed label=" + label
                        + " error=" + (completed.Exception?.GetBaseException()?.Message ?? "unknown"));
                }
                else if (completed.IsCanceled)
                {
                    AwakeLog.Write("background_task_cancelled label=" + label);
                }
            },
            TaskScheduler.Default);
    }
}
