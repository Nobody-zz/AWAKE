using System;

namespace Awake;

internal static class AwakePromiseStateMachine
{
    internal const string Pending = "pending";
    internal const string Accepted = "accepted";
    internal const string Kept = "kept";
    internal const string Broken = "broken";
    internal const string Rejected = "rejected";

    internal static bool IsValidStatus(string status)
    {
        return StringComparer.Ordinal.Equals(status, Pending)
            || StringComparer.Ordinal.Equals(status, Accepted)
            || StringComparer.Ordinal.Equals(status, Kept)
            || StringComparer.Ordinal.Equals(status, Broken)
            || StringComparer.Ordinal.Equals(status, Rejected);
    }

    internal static bool CanTransition(string current, string next)
    {
        if (!IsValidStatus(current) || !IsValidStatus(next)) return false;
        if (StringComparer.Ordinal.Equals(current, Pending))
        {
            return StringComparer.Ordinal.Equals(next, Accepted)
                || StringComparer.Ordinal.Equals(next, Kept)
                || StringComparer.Ordinal.Equals(next, Broken)
                || StringComparer.Ordinal.Equals(next, Rejected);
        }
        if (StringComparer.Ordinal.Equals(current, Accepted))
        {
            return StringComparer.Ordinal.Equals(next, Kept)
                || StringComparer.Ordinal.Equals(next, Broken);
        }
        return false;
    }

    internal static string NewPromiseId()
    {
        return "promise|" + Guid.NewGuid().ToString("N");
    }
}
