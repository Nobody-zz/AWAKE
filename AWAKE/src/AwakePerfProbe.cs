using System;
using System.Diagnostics;

namespace Awake;

internal static class AwakePerfProbe
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    internal static long StartMilliseconds()
    {
        return Clock.ElapsedMilliseconds;
    }

    internal static void Record(string operation, long startedAtMilliseconds)
    {
        long elapsed = Clock.ElapsedMilliseconds - startedAtMilliseconds;
        AwakeLog.Write("awake_perf operation=" + (operation ?? "unknown") + " ms=" + Math.Max(0, elapsed));
    }
}
