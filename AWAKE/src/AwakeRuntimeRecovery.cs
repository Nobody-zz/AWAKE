using System;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// Decides whether a stopped Runtime Service may be relaunched, and carries the single
/// thread-safe seam the MCM gates use to ask the host composition to relaunch it.
///
/// Background: the framework can tear the runtime down mid-session (a failed business exchange runs
/// FailConnectionAsync -> state = Stopped) and never restarts it, so one fault used to end AI for the
/// rest of the campaign session. The framework does allow Start() again from Stopped; AWAKE just
/// never asked. Since the 009 contract/probe fix a failed health probe no longer tears the runtime
/// down, so this path is the backstop for a runtime that really died.
/// </summary>
internal static class AwakeRuntimeRecovery
{
    /// <summary>Relaunch attempts allowed per campaign session.</summary>
    internal const int MaxAttemptsPerSession = 3;

    /// <summary>Minimum gap between relaunch attempts.</summary>
    internal const int CooldownMilliseconds = 20000;

    /// <summary>
    /// Installed by the host composition (which owns the runtime lifecycle) and cleared on dispose.
    /// Returns true when a relaunch was actually requested.
    /// </summary>
    internal static Func<string, bool> RelaunchHook;

    /// <summary>
    /// Pure policy: may we relaunch right now? <paramref name="reason"/> carries the decision token
    /// so callers can log why nothing happened.
    /// </summary>
    internal static bool ShouldRelaunch(
        RuntimeServiceState? state,
        int attemptsThisSession,
        long lastAttemptUtcTicks,
        long nowUtcTicks,
        out string reason)
    {
        if (state != RuntimeServiceState.Stopped)
        {
            reason = "state_not_stopped";
            return false;
        }

        if (attemptsThisSession < 0 || attemptsThisSession >= MaxAttemptsPerSession)
        {
            reason = "attempt_limit";
            return false;
        }

        if (lastAttemptUtcTicks > 0 && nowUtcTicks > lastAttemptUtcTicks)
        {
            long elapsedTicks = nowUtcTicks - lastAttemptUtcTicks;
            if (elapsedTicks < CooldownMilliseconds * TimeSpan.TicksPerMillisecond)
            {
                reason = "cooldown";
                return false;
            }
        }

        reason = "allowed";
        return true;
    }

    /// <summary>
    /// Asks the host composition to relaunch a stopped runtime. Never throws: a missing hook or a
    /// failing hook simply reports "not requested".
    /// </summary>
    internal static bool TryRequestRelaunch(string reason)
    {
        Func<string, bool> hook = RelaunchHook;
        if (hook == null) return false;
        try
        {
            return hook(reason ?? string.Empty);
        }
        catch
        {
            return false;
        }
    }
}
