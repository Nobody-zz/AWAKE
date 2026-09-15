using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Awake;

internal enum WorldFactSelectionPolicy
{
    RecentDynamics,
    WeeklyDynamics,
    WorldKnowledge,
    CharacterMemoryCandidate,
    EventTriggerCandidate
}

internal enum WorldFactQueryStatus
{
    Missing,
    Empty,
    Success,
    Corrupt,
    Unavailable,
    InvalidRequest,
    Cancelled
}

internal enum LegacyFallbackState
{
    NotApplicable,
    NotUsed,
    Used,
    Rejected
}

internal interface IWorldFactContextReader
{
    Task<WorldFactQueryResult> QueryAsync(
        WorldFactQueryRequest request,
        CancellationToken cancellationToken);
}

internal sealed class WorldFactQueryRequest
{
    internal WorldFactSelectionPolicy Policy { get; set; }
    internal int CurrentDay { get; set; }
    internal int StartDay { get; set; }
    internal int EndDay { get; set; }
    internal int MaximumResults { get; set; } = 20;
    internal string HeroId { get; set; } = string.Empty;
    internal string KingdomId { get; set; } = string.Empty;
    internal string ClanId { get; set; } = string.Empty;
    internal string SettlementId { get; set; } = string.Empty;
    internal string PartyId { get; set; } = string.Empty;
    internal string FactionId { get; set; } = string.Empty;
    internal IReadOnlyList<string> AllowedKinds { get; set; } = Array.Empty<string>();
    internal bool AllowLegacyFallback { get; set; } = true;
}

internal sealed class WorldFactSelectionDecision
{
    internal string FactId { get; }
    internal bool Included { get; }
    internal string ReasonCode { get; }

    internal WorldFactSelectionDecision(string factId, bool included, string reasonCode)
    {
        FactId = factId ?? string.Empty;
        Included = included;
        ReasonCode = reasonCode ?? string.Empty;
    }
}

internal sealed class WorldFactQueryResult
{
    internal WorldFactQueryStatus Status { get; }
    internal WorldFactSelectionPolicy Policy { get; }
    internal IReadOnlyList<JObject> Facts { get; }
    internal IReadOnlyList<string> SourceFactIds { get; }
    internal IReadOnlyList<WorldFactSelectionDecision> Decisions { get; }
    internal string ErrorCode { get; }
    internal bool UsedLegacyFallback { get; }
    internal LegacyFallbackState LegacyFallbackState { get; }
    internal int? JournalRevision { get; }
    internal int WindowStartDay { get; }
    internal int WindowEndDay { get; }

    internal WorldFactQueryResult(
        WorldFactQueryStatus status,
        WorldFactSelectionPolicy policy,
        IEnumerable<JObject> facts = null,
        IEnumerable<WorldFactSelectionDecision> decisions = null,
        string errorCode = null,
        bool usedLegacyFallback = false,
        LegacyFallbackState legacyFallbackState = LegacyFallbackState.NotApplicable,
        int? journalRevision = null,
        int windowStartDay = 0,
        int windowEndDay = 0)
    {
        Status = status;
        Policy = policy;
        Facts = (facts ?? Enumerable.Empty<JObject>())
            .Where(value => value != null)
            .Select(value => (JObject)value.DeepClone())
            .ToArray();
        SourceFactIds = Facts.Select(value => (string)value["factId"] ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        Decisions = (decisions ?? Enumerable.Empty<WorldFactSelectionDecision>()).ToArray();
        ErrorCode = errorCode ?? string.Empty;
        UsedLegacyFallback = usedLegacyFallback;
        LegacyFallbackState = legacyFallbackState;
        JournalRevision = journalRevision;
        WindowStartDay = windowStartDay;
        WindowEndDay = windowEndDay;
    }
}

internal sealed class WeeklyDynamicsInput
{
    internal WorldFactQueryStatus Status { get; }
    internal WorldFactSelectionPolicy Policy { get; }
    internal IReadOnlyList<JObject> Facts { get; }
    internal IReadOnlyList<string> SourceFactIds { get; }
    internal int WindowStartDay { get; }
    internal int WindowEndDay { get; }

    private WeeklyDynamicsInput(WorldFactQueryResult result)
    {
        Status = result.Status;
        Policy = result.Policy;
        Facts = result.Facts.Select(value => (JObject)value.DeepClone()).ToArray();
        SourceFactIds = result.SourceFactIds.ToArray();
        WindowStartDay = result.WindowStartDay;
        WindowEndDay = result.WindowEndDay;
    }

    internal static bool TryCreate(WorldFactQueryResult result, out WeeklyDynamicsInput input, out string errorCode)
    {
        input = null;
        errorCode = string.Empty;
        if (result == null || result.Policy != WorldFactSelectionPolicy.WeeklyDynamics)
        {
            errorCode = "awake.world_fact.report.query_failed";
            return false;
        }
        if (result.Status != WorldFactQueryStatus.Success && result.Status != WorldFactQueryStatus.Empty)
        {
            errorCode = result.UsedLegacyFallback
                ? "awake.world_fact.report.legacy_fallback"
                : "awake.world_fact.report.query_failed";
            return false;
        }
        if (result.UsedLegacyFallback)
        {
            errorCode = "awake.world_fact.report.legacy_fallback";
            return false;
        }
        if (result.WindowStartDay < 1
            || result.WindowEndDay < 7
            || result.WindowEndDay % 7 != 0
            || result.WindowStartDay != result.WindowEndDay - 6)
        {
            errorCode = "awake.world_fact.report.window_invalid";
            return false;
        }
        if (result.Facts.Count != result.SourceFactIds.Count)
        {
            errorCode = "awake.world_fact.report.source_closure_invalid";
            return false;
        }
        HashSet<string> factIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < result.Facts.Count; index++)
        {
            JObject fact = result.Facts[index];
            string factId = (string)fact["factId"] ?? string.Empty;
            string eventKey = (string)fact["eventKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factId)
                || !WorldFact.IsStableFactId(eventKey, factId)
                || !factIds.Add(factId)
                || !StringComparer.Ordinal.Equals(factId, result.SourceFactIds[index]))
            {
                errorCode = "awake.world_fact.report.fact_invalid";
                return false;
            }
        }
        input = new WeeklyDynamicsInput(result);
        return true;
    }
}

/// <summary>
/// Pure read policy layer for structured world facts. It never writes the journal and never
/// exposes journal keys to consumers.
/// </summary>
internal sealed class WorldFactQuery : IWorldFactContextReader
{
    private readonly Func<CancellationToken, Task<WorldFactJournalReadResult>> _journalReader;
    private readonly Func<IReadOnlyList<WorldEventRecord>> _legacyReader;

    internal WorldFactQuery(
        Func<CancellationToken, Task<WorldFactJournalReadResult>> journalReader,
        Func<IReadOnlyList<WorldEventRecord>> legacyReader = null)
    {
        _journalReader = journalReader ?? throw new ArgumentNullException(nameof(journalReader));
        _legacyReader = legacyReader;
    }

    Task<WorldFactQueryResult> IWorldFactContextReader.QueryAsync(
        WorldFactQueryRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(request, cancellationToken);
    }

    internal async Task<WorldFactQueryResult> ExecuteAsync(WorldFactQueryRequest request, CancellationToken cancellationToken)
    {
        WorldFactSelectionPolicy policy;
        if (cancellationToken.IsCancellationRequested)
            return CancelledResult(request == null ? default(WorldFactSelectionPolicy) : request.Policy);
        if (!TryValidateRequest(request, out policy, out string requestError))
            return new WorldFactQueryResult(WorldFactQueryStatus.InvalidRequest, policy, errorCode: requestError);

        int windowStartDay;
        int windowEndDay;
        ResolveWindow(request, policy, out windowStartDay, out windowEndDay);

        WorldFactJournalReadResult journal;
        try
        {
            journal = await _journalReader(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return CancelledResult(policy, windowStartDay, windowEndDay);
        }
        if (cancellationToken.IsCancellationRequested)
            return CancelledResult(policy, windowStartDay, windowEndDay);
        if (journal == null)
            return new WorldFactQueryResult(WorldFactQueryStatus.Unavailable, policy, errorCode: "awake.world_fact.query_reader_null", windowStartDay: windowStartDay, windowEndDay: windowEndDay);

        IReadOnlyList<JObject> sourceFacts;
        bool usedLegacyFallback = false;
        WorldFactJournalReadStatus sourceStatus = journal.Status;
        string sourceError = journal.ErrorCode;
        if (journal.Status == WorldFactJournalReadStatus.Success)
        {
            if (!journal.Revision.HasValue || journal.Revision.Value < 1)
                return new WorldFactQueryResult(WorldFactQueryStatus.Corrupt, policy, errorCode: "awake.world_fact.journal_revision_missing", legacyFallbackState: LegacyFallbackState.NotUsed, windowStartDay: windowStartDay, windowEndDay: windowEndDay);
            sourceFacts = journal.Facts;
        }
        else if (journal.Status == WorldFactJournalReadStatus.Empty)
        {
            if (!journal.Revision.HasValue || journal.Revision.Value < 1)
                return new WorldFactQueryResult(WorldFactQueryStatus.Corrupt, policy, errorCode: "awake.world_fact.journal_revision_missing", legacyFallbackState: LegacyFallbackState.NotUsed, windowStartDay: windowStartDay, windowEndDay: windowEndDay);
            return new WorldFactQueryResult(WorldFactQueryStatus.Empty, policy, legacyFallbackState: _legacyReader == null ? LegacyFallbackState.NotApplicable : LegacyFallbackState.NotUsed, journalRevision: journal.Revision, windowStartDay: windowStartDay, windowEndDay: windowEndDay);
        }
        else if (journal.Status == WorldFactJournalReadStatus.Missing && _legacyReader != null)
        {
            IReadOnlyList<WorldEventRecord> legacyRecords = _legacyReader() ?? Array.Empty<WorldEventRecord>();
            LegacyFallbackState fallbackState = legacyRecords.Count == 0
                ? LegacyFallbackState.NotUsed
                : LegacyFallbackState.Used;
            if (legacyRecords.Count == 0)
                return new WorldFactQueryResult(WorldFactQueryStatus.Missing, policy, legacyFallbackState: fallbackState, windowStartDay: windowStartDay, windowEndDay: windowEndDay);
            if (!request.AllowLegacyFallback)
                return new WorldFactQueryResult(WorldFactQueryStatus.Unavailable, policy, errorCode: "awake.world_fact.event.legacy_fallback", legacyFallbackState: LegacyFallbackState.Rejected, windowStartDay: windowStartDay, windowEndDay: windowEndDay);
            sourceFacts = BuildLegacyFacts(legacyRecords);
            usedLegacyFallback = true;
            if (sourceFacts.Count == 0)
                return new WorldFactQueryResult(WorldFactQueryStatus.Unavailable, policy, errorCode: "awake.world_fact.legacy_fallback", usedLegacyFallback: true, legacyFallbackState: fallbackState, windowStartDay: windowStartDay, windowEndDay: windowEndDay);
            return Select(sourceFacts, request, policy, usedLegacyFallback, fallbackState, null, windowStartDay, windowEndDay);
        }
        else
        {
            return new WorldFactQueryResult(MapStatus(sourceStatus), policy, errorCode: sourceError, legacyFallbackState: _legacyReader == null ? LegacyFallbackState.NotApplicable : LegacyFallbackState.NotUsed, windowStartDay: windowStartDay, windowEndDay: windowEndDay);
        }

        return Select(sourceFacts, request, policy, usedLegacyFallback, _legacyReader == null ? LegacyFallbackState.NotApplicable : LegacyFallbackState.NotUsed, journal.Revision, windowStartDay, windowEndDay);
    }

    private static WorldFactQueryResult Select(
        IReadOnlyList<JObject> sourceFacts,
        WorldFactQueryRequest request,
        WorldFactSelectionPolicy policy,
        bool usedLegacyFallback,
        LegacyFallbackState legacyFallbackState,
        int? journalRevision,
        int windowStartDay,
        int windowEndDay)
    {
        int startDay;
        int endDay;
        ResolveWindow(request, policy, out startDay, out endDay);
        HashSet<string> seenFactIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> allowedKinds = new HashSet<string>(
            (request.AllowedKinds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()),
            StringComparer.Ordinal);
        HashSet<string> relatedIds = RelatedIds(request);
        var candidates = new List<SelectionCandidate>();

        foreach (JObject fact in sourceFacts ?? Array.Empty<JObject>())
        {
            string factId = (string)fact?["factId"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factId))
            {
                candidates.Add(new SelectionCandidate(factId, false, "excluded_invalid_fact_id", fact, 0, 0));
                continue;
            }
            if (!seenFactIds.Add(factId))
            {
                candidates.Add(new SelectionCandidate(factId, false, "excluded_duplicate_fact_id", fact, 0, 0));
                continue;
            }

            int day = (int?)fact["occurred"]?["campaignDay"] ?? 0;
            long timeSlot = (long?)fact["occurred"]?["timeSlot"] ?? 0L;
            string reason = SelectReason(fact, policy, startDay, endDay, allowedKinds, relatedIds);
            candidates.Add(new SelectionCandidate(
                factId,
                reason.StartsWith("selected_", StringComparison.Ordinal),
                reason,
                fact,
                day,
                timeSlot));
        }

        IEnumerable<SelectionCandidate> ordered = candidates
            .Where(value => value.Included)
            .OrderBy(value => SortDay(value, policy))
            .ThenBy(value => SortTime(value, policy))
            .ThenBy(value => value.FactId, StringComparer.Ordinal)
            .ToList();
        int maximum = request.MaximumResults == 0
            ? int.MaxValue
            : Math.Min(request.MaximumResults, 100);
        var selected = new List<SelectionCandidate>();
        foreach (SelectionCandidate candidate in ordered)
        {
            if (selected.Count < maximum)
            {
                selected.Add(candidate);
                continue;
            }
        }

        HashSet<SelectionCandidate> selectedSet = new HashSet<SelectionCandidate>(selected);
        var decisions = new List<WorldFactSelectionDecision>();
        foreach (SelectionCandidate candidate in candidates)
        {
            bool included = selectedSet.Contains(candidate);
            string reason = candidate.Included && !included ? "excluded_result_limit" : candidate.ReasonCode;
            decisions.Add(new WorldFactSelectionDecision(candidate.FactId, included, reason));
        }

        return new WorldFactQueryResult(
            legacyFallbackState == LegacyFallbackState.Used
                ? WorldFactQueryStatus.Unavailable
                : selected.Count == 0 ? WorldFactQueryStatus.Empty : WorldFactQueryStatus.Success,
            policy,
            selected.Select(value => value.Fact),
            decisions,
            usedLegacyFallback ? "awake.world_fact.legacy_fallback" : string.Empty,
            usedLegacyFallback,
            legacyFallbackState,
            journalRevision,
            windowStartDay,
            windowEndDay);
    }

    private static WorldFactQueryResult CancelledResult(
        WorldFactSelectionPolicy policy,
        int windowStartDay = 0,
        int windowEndDay = 0)
    {
        return new WorldFactQueryResult(
            WorldFactQueryStatus.Cancelled,
            policy,
            errorCode: "awake.cancelled",
            windowStartDay: windowStartDay,
            windowEndDay: windowEndDay);
    }

    private static string SelectReason(
        JObject fact,
        WorldFactSelectionPolicy policy,
        int startDay,
        int endDay,
        HashSet<string> allowedKinds,
        HashSet<string> relatedIds)
    {
        int day = (int?)fact["occurred"]?["campaignDay"] ?? 0;
        if (day < startDay || day > endDay)
            return "excluded_outside_window";

        string origin = (string)fact["origin"] ?? string.Empty;
        string authority = (string)fact["authority"] ?? string.Empty;
        string scope = (string)fact["initialKnowledgeScope"] ?? string.Empty;
        bool nativeConfirmedPublic = StringComparer.Ordinal.Equals(origin, "native_game")
            && StringComparer.Ordinal.Equals(authority, "game_confirmed")
            && StringComparer.Ordinal.Equals(scope, "public");
        bool legacy = StringComparer.Ordinal.Equals(origin, "legacy_import");

        if (policy == WorldFactSelectionPolicy.CharacterMemoryCandidate)
        {
            if (legacy) return "excluded_legacy_has_no_entities";
            if (!nativeConfirmedPublic) return "excluded_not_game_confirmed_public";
            if (relatedIds.Count == 0) return "excluded_character_context_missing";
            if (!HasRelatedEntity(fact, relatedIds)) return "excluded_no_related_entity";
            return "selected_character_related_fact";
        }

        if (policy == WorldFactSelectionPolicy.EventTriggerCandidate)
        {
            if (!nativeConfirmedPublic) return "excluded_not_game_confirmed_public";
            string kind = (string)fact["kind"] ?? string.Empty;
            if (allowedKinds.Count > 0 && !allowedKinds.Contains(kind)) return "excluded_kind_not_subscribed";
            return "selected_subscribed_structured_fact";
        }

        if (legacy)
        {
            return policy == WorldFactSelectionPolicy.WorldKnowledge
                ? "selected_legacy_compatibility_fact"
                : "selected_legacy_compatibility_fact";
        }
        if (!nativeConfirmedPublic) return "excluded_not_game_confirmed_public";
        return policy == WorldFactSelectionPolicy.WeeklyDynamics
            ? "selected_weekly_public_game_fact"
            : policy == WorldFactSelectionPolicy.WorldKnowledge
                ? "selected_public_world_knowledge_fact"
                : "selected_recent_public_game_fact";
    }

    private static bool HasRelatedEntity(JObject fact, HashSet<string> relatedIds)
    {
        JArray entities = fact["entities"] as JArray;
        return entities != null && entities.OfType<JObject>().Any(entity => relatedIds.Contains((string)entity["id"] ?? string.Empty));
    }

    private static HashSet<string> RelatedIds(WorldFactQueryRequest request)
    {
        return new HashSet<string>(new[]
        {
            request.HeroId, request.KingdomId, request.ClanId,
            request.SettlementId, request.PartyId, request.FactionId
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.Ordinal);
    }

    private static void ResolveWindow(WorldFactQueryRequest request, WorldFactSelectionPolicy policy, out int startDay, out int endDay)
    {
        if (request.StartDay > 0 || request.EndDay > 0)
        {
            startDay = request.StartDay;
            endDay = request.EndDay;
            return;
        }
        if (policy == WorldFactSelectionPolicy.RecentDynamics || policy == WorldFactSelectionPolicy.WeeklyDynamics)
        {
            endDay = request.CurrentDay;
            startDay = endDay - 6;
            return;
        }
        startDay = 1;
        endDay = int.MaxValue;
    }

    private static bool TryValidateRequest(WorldFactQueryRequest request, out WorldFactSelectionPolicy policy, out string error)
    {
        policy = request == null ? default(WorldFactSelectionPolicy) : request.Policy;
        error = string.Empty;
        if (request == null) { error = "awake.world_fact.query_request_missing"; return false; }
        if (request.MaximumResults < 0) { error = "awake.world_fact.query_limit_invalid"; return false; }
        if ((request.Policy == WorldFactSelectionPolicy.RecentDynamics || request.Policy == WorldFactSelectionPolicy.WeeklyDynamics)
            && request.CurrentDay <= 0 && request.EndDay <= 0)
        { error = "awake.world_fact.query_day_missing"; return false; }
        if ((request.StartDay > 0) != (request.EndDay > 0))
        { error = "awake.world_fact.query_window_incomplete"; return false; }
        if (request.StartDay > 0 && request.EndDay > 0
            && (request.StartDay < 1 || request.EndDay < request.StartDay))
        { error = "awake.world_fact.query_window_invalid"; return false; }
        if (request.Policy == WorldFactSelectionPolicy.WeeklyDynamics
            && request.StartDay > 0 && request.EndDay > 0
            && (request.EndDay - request.StartDay != 6 || request.EndDay < 7 || request.EndDay % 7 != 0))
        { error = "awake.world_fact.query_week_invalid"; return false; }
        return true;
    }

    private static int SortDay(SelectionCandidate value, WorldFactSelectionPolicy policy)
        => policy == WorldFactSelectionPolicy.RecentDynamics || policy == WorldFactSelectionPolicy.CharacterMemoryCandidate
            ? -value.Day : value.Day;

    private static long SortTime(SelectionCandidate value, WorldFactSelectionPolicy policy)
        => policy == WorldFactSelectionPolicy.RecentDynamics || policy == WorldFactSelectionPolicy.CharacterMemoryCandidate
            ? -value.TimeSlot : value.TimeSlot;

    private static WorldFactQueryStatus MapStatus(WorldFactJournalReadStatus status)
    {
        switch (status)
        {
            case WorldFactJournalReadStatus.Missing: return WorldFactQueryStatus.Missing;
            case WorldFactJournalReadStatus.Empty: return WorldFactQueryStatus.Empty;
            case WorldFactJournalReadStatus.Success: return WorldFactQueryStatus.Success;
            case WorldFactJournalReadStatus.Corrupt: return WorldFactQueryStatus.Corrupt;
            default: return WorldFactQueryStatus.Unavailable;
        }
    }

    private static IReadOnlyList<JObject> BuildLegacyFacts(IReadOnlyList<WorldEventRecord> records)
    {
        var result = new List<JObject>();
        foreach (WorldEventRecord record in records ?? Array.Empty<WorldEventRecord>())
        {
            if (record == null || !WorldEventContract.CanProject(record) || record.StructuredFact != null) continue;
            string eventId = record.EventId;
            string suffix = ShortHash("awake.world_fact.legacy\n" + eventId);
            long timeSlot = record.Day * 144L;
            if (record.OccurredAt != default(DateTimeOffset))
            {
                double days = (record.OccurredAt.ToUniversalTime() - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalDays;
                if (!double.IsNaN(days) && !double.IsInfinity(days) && days >= record.Day) timeSlot = (long)Math.Floor(days * 144d);
            }
            result.Add(new JObject
            {
                ["schema"] = WorldFact.Schema,
                ["factId"] = "awake:fact:legacy-" + suffix,
                ["legacyEventId"] = eventId,
                ["eventKey"] = string.IsNullOrWhiteSpace(record.EventKey) ? eventId : record.EventKey,
                ["occurred"] = new JObject { ["campaignDay"] = record.Day, ["timeSlot"] = timeSlot },
                ["kind"] = record.Kind,
                ["origin"] = "legacy_import",
                ["authority"] = "legacy_unstructured",
                ["initialKnowledgeScope"] = "public",
                ["presentation"] = new JObject { ["summary"] = record.Text, ["domain"] = record.Domain },
                ["legacyVisibilityIdentityIds"] = new JArray(record.VisibilityIdentityIds ?? Array.Empty<string>()),
                ["entities"] = new JArray()
            });
        }
        return result;
    }

    private static string ShortHash(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return string.Concat(digest.Take(8).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }

    private sealed class SelectionCandidate
    {
        internal string FactId { get; }
        internal bool Included { get; }
        internal string ReasonCode { get; }
        internal JObject Fact { get; }
        internal int Day { get; }
        internal long TimeSlot { get; }

        internal SelectionCandidate(string factId, bool included, string reasonCode, JObject fact, int day, long timeSlot)
        {
            FactId = factId;
            Included = included;
            ReasonCode = reasonCode;
            Fact = fact;
            Day = day;
            TimeSlot = timeSlot;
        }
    }
}
