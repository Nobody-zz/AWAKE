using System;
using System.Diagnostics;

namespace Awake;

internal static class AwakePerfProbe
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object Gate = new object();
    private static int _recordCount;
    private static int _slowCount;
    private static string _lastSlowOperation = string.Empty;

    internal static long StartMilliseconds()
    {
        return Clock.ElapsedMilliseconds;
    }

    internal static void Record(string operation, long startedAtMilliseconds)
    {
        long elapsed = Clock.ElapsedMilliseconds - startedAtMilliseconds;
        lock (Gate)
        {
            _recordCount++;
            if (elapsed >= 100)
            {
                _slowCount++;
                _lastSlowOperation = operation ?? "unknown";
            }
        }
        AwakeLog.Write("awake_perf operation=" + (operation ?? "unknown") + " ms=" + Math.Max(0, elapsed));
    }

    internal static PerfProbeSnapshot Snapshot()
    {
        lock (Gate)
        {
            return new PerfProbeSnapshot(
                _recordCount,
                _slowCount,
                _lastSlowOperation,
                Clock.ElapsedMilliseconds);
        }
    }

    internal static void Reset()
    {
        lock (Gate)
        {
            _recordCount = 0;
            _slowCount = 0;
            _lastSlowOperation = string.Empty;
            Clock.Restart();
        }
    }
}

internal readonly struct PerfProbeSnapshot
{
    internal int RecordCount { get; }
    internal int SlowCount { get; }
    internal string LastSlowOperation { get; }
    internal long UptimeMilliseconds { get; }

    internal PerfProbeSnapshot(int recordCount, int slowCount, string lastSlowOperation, long uptimeMilliseconds)
    {
        RecordCount = recordCount;
        SlowCount = slowCount;
        LastSlowOperation = lastSlowOperation ?? string.Empty;
        UptimeMilliseconds = uptimeMilliseconds;
    }
}
