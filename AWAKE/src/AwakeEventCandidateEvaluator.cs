using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class AwakeEventFactTrigger
{
    internal IReadOnlyList<string> AllowedKinds { get; }
    internal int MinimumMatches { get; }
    internal int MaximumAgeDays { get; }

    internal AwakeEventFactTrigger(IEnumerable<string> allowedKinds, int minimumMatches, int maximumAgeDays)
    {
        AllowedKinds = (allowedKinds ?? Enumerable.Empty<string>())
            .Select(value => value == null ? string.Empty : value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        MinimumMatches = minimumMatches;
        MaximumAgeDays = maximumAgeDays;
    }
}

internal sealed class AwakeEventCandidateEvaluation
{
    internal bool Eligible { get; }
    internal string ReasonCode { get; }
    internal IReadOnlyList<string> MatchedFactIds { get; }
    internal WorldFactQueryStatus Status { get; }
    internal int? JournalRevision { get; }

    internal AwakeEventCandidateEvaluation(
        bool eligible,
        string reasonCode,
        WorldFactQueryStatus status,
        int? journalRevision,
        IEnumerable<string> matchedFactIds)
    {
        Eligible = eligible;
        ReasonCode = reasonCode ?? string.Empty;
        Status = status;
        JournalRevision = journalRevision;
        MatchedFactIds = (matchedFactIds ?? Enumerable.Empty<string>()).ToArray();
    }
}

internal static class AwakeEventCandidateEvaluator
{
    internal static bool Validate(AwakeEventFactTrigger trigger, out string error)
    {
        error = string.Empty;
        if (trigger == null) return true;
        if (trigger.AllowedKinds == null || trigger.AllowedKinds.Count == 0 || trigger.AllowedKinds.Count > 20)
        {
            error = "factTrigger.allowedKinds";
            return false;
        }
        if (trigger.AllowedKinds.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 80))
        {
            error = "factTrigger.allowedKinds";
            return false;
        }
        if (trigger.MinimumMatches < 1 || trigger.MinimumMatches > 20)
        {
            error = "factTrigger.minimumMatches";
            return false;
        }
        if (trigger.MaximumAgeDays < 1 || trigger.MaximumAgeDays > 7)
        {
            error = "factTrigger.maximumAgeDays";
            return false;
        }
        return true;
    }

    internal static AwakeEventCandidateEvaluation Evaluate(
        AwakeEventRule rule,
        WorldFactQueryResult result,
        int currentDay)
    {
        AwakeEventFactTrigger trigger = rule?.FactTrigger;
        if (trigger == null)
        {
            return new AwakeEventCandidateEvaluation(
                true,
                "fact_trigger_not_required",
                result == null ? WorldFactQueryStatus.Unavailable : result.Status,
                result?.JournalRevision,
                Array.Empty<string>());
        }

        if (!Validate(trigger, out string validationError))
        {
            return Failed(result, validationError);
        }
        if (result == null)
        {
            return Failed(null, "fact_trigger_query_unavailable");
        }
        if (result.LegacyFallbackState == LegacyFallbackState.Used || result.UsedLegacyFallback)
        {
            return Failed(result, "fact_trigger_legacy_fallback");
        }
        if (result.Status == WorldFactQueryStatus.Cancelled)
        {
            return Failed(result, "awake.cancelled");
        }
        if (result.Status == WorldFactQueryStatus.Empty || result.Status == WorldFactQueryStatus.Missing)
        {
            return Failed(result, "fact_trigger_no_match");
        }
        if (result.Status != WorldFactQueryStatus.Success)
        {
            return Failed(result, string.IsNullOrWhiteSpace(result.ErrorCode)
                ? "fact_trigger_query_" + result.Status.ToString().ToLowerInvariant()
                : result.ErrorCode);
        }
        if (currentDay <= 0)
        {
            return Failed(result, "fact_trigger_current_day_invalid");
        }

        HashSet<string> allowedKinds = new HashSet<string>(trigger.AllowedKinds, StringComparer.Ordinal);
        Dictionary<string, JObject> matched = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (JObject fact in result.Facts ?? Array.Empty<JObject>())
        {
            string factId = (string)fact?["factId"] ?? string.Empty;
            string kind = (string)fact?["kind"] ?? string.Empty;
            int factDay = (int?)fact?["occurred"]?["campaignDay"] ?? 0;
            int age = currentDay - factDay;
            if (string.IsNullOrWhiteSpace(factId)
                || !allowedKinds.Contains(kind)
                || factDay <= 0
                || age < 0
                || age > trigger.MaximumAgeDays)
            {
                continue;
            }
            if (!matched.ContainsKey(factId)) matched.Add(factId, fact);
        }

        string reason = matched.Count >= trigger.MinimumMatches
            ? "fact_trigger_matched"
            : "fact_trigger_no_match";
        return new AwakeEventCandidateEvaluation(
            matched.Count >= trigger.MinimumMatches,
            reason,
            result.Status,
            result.JournalRevision,
            matched
                .OrderBy(value => (int?)value.Value["occurred"]?["campaignDay"] ?? 0)
                .ThenBy(value => (long?)value.Value["occurred"]?["timeSlot"] ?? 0L)
                .ThenBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => value.Key));
    }

    private static AwakeEventCandidateEvaluation Failed(WorldFactQueryResult result, string reason)
    {
        return new AwakeEventCandidateEvaluation(
            false,
            reason,
            result == null ? WorldFactQueryStatus.Unavailable : result.Status,
            result?.JournalRevision,
            Array.Empty<string>());
    }
}
