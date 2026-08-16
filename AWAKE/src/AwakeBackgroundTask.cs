using System;
using System.Threading.Tasks;

namespace Awake;

internal static class AwakeBackgroundTask
{
    internal static void Run(Func<Task> taskFactory, string label)
    {
        if (taskFactory == null) return;
        try
        {
            Task task = taskFactory();
            if (task == null) return;
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
        catch (Exception ex)
        {
            AwakeLog.Write("background_task_start_failed label=" + label + " error=" + ex.Message);
        }
    }
}
