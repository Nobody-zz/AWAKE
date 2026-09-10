using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Awake;

internal interface IWorldKnowledgeQuery
{
    WorldKnowledgeQueryResult Query(WorldbookQuery query);
    List<WorldKnowledgeEntry> Search(string text, int limit);
    string BuildStatusText();
}

internal sealed class WorldKnowledgeSnapshot
{
    internal string SchemaVersion { get; set; } = string.Empty;
    internal string PackageId { get; set; } = string.Empty;
    internal string Version { get; set; } = string.Empty;
    internal string WorldId { get; set; } = string.Empty;
    internal string ContentTier { get; set; } = "base";
    internal int Revision { get; set; }
    internal Dictionary<string, WorldKnowledgeEntry> Entries { get; } = new Dictionary<string, WorldKnowledgeEntry>(StringComparer.Ordinal);
    internal Dictionary<string, WorldKnowledgeIdentity> Identities { get; } = new Dictionary<string, WorldKnowledgeIdentity>(StringComparer.Ordinal);
    internal Dictionary<string, WorldKnowledgeReferral> Referrals { get; } = new Dictionary<string, WorldKnowledgeReferral>(StringComparer.Ordinal);
    internal Dictionary<string, List<string>> KeywordIndex { get; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    internal List<string> Warnings { get; } = new List<string>();
}

internal sealed class WorldKnowledgeEntry
{
    internal string Id { get; set; } = string.Empty;
    internal string Domain { get; set; } = string.Empty;
    internal string Title { get; set; } = string.Empty;
    internal string Summary { get; set; } = string.Empty;
    internal string SourceKind { get; set; } = "static";
    internal string SourceId { get; set; } = string.Empty;
    internal string ReportId { get; set; } = string.Empty;
    internal List<string> SourceEventIds { get; } = new List<string>();
    internal List<string> Keywords { get; } = new List<string>();
    internal List<WorldKnowledgeExpression> Expressions { get; } = new List<WorldKnowledgeExpression>();
}

internal sealed class WorldKnowledgeExpression
{
    internal string Id { get; set; } = string.Empty;
    internal string Detail { get; set; } = "rumor";
    internal string Text { get; set; } = string.Empty;
    internal bool Enabled { get; set; } = true;
    internal List<WorldKnowledgeRule> Grants { get; } = new List<WorldKnowledgeRule>();
    internal List<WorldKnowledgeRule> Denies { get; } = new List<WorldKnowledgeRule>();
}

internal sealed class WorldKnowledgeRule
{
    internal string IdentityId { get; set; } = string.Empty;
    internal string Scope { get; set; } = "local";
    internal string MinDetail { get; set; } = "rumor";
    internal WorldKnowledgeCondition Conditions { get; set; }
    internal List<string> ReferralIds { get; } = new List<string>();
}

internal sealed class WorldKnowledgeCondition
{
    internal List<string> IdentityIds { get; } = new List<string>();
    internal List<string> CultureIds { get; } = new List<string>();
    internal List<string> KingdomIds { get; } = new List<string>();
    internal List<string> SettlementIds { get; } = new List<string>();
    internal List<string> RoleIds { get; } = new List<string>();
    internal bool? IsFemale { get; set; }
    internal bool? IsClanLeader { get; set; }
    internal int? MinAge { get; set; }
    internal int? MaxAge { get; set; }
    internal int? MinManagement { get; set; }
    internal Dictionary<string, int> MinSkills { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
}

internal sealed class WorldKnowledgeIdentity
{
    internal string Id { get; set; } = string.Empty;
    internal string DisplayName { get; set; } = string.Empty;
    internal int BasePriority { get; set; }
    internal List<string> Parents { get; } = new List<string>();
    internal List<string> ReferralTargetIds { get; } = new List<string>();
}

internal sealed class WorldKnowledgeReferral
{
    internal string Id { get; set; } = string.Empty;
    internal string DisplayName { get; set; } = string.Empty;
    internal string Reason { get; set; } = string.Empty;
    internal int Priority { get; set; }
    internal bool PubliclyAskable { get; set; }
}

internal sealed class WorldKnowledgeQueryResult
{
    internal string RetrievedText { get; set; } = string.Empty;
    internal string MatchMode { get; set; } = "none";
    internal string State { get; set; } = "not_found";
    internal int ByteBudget { get; set; }
    internal List<string> HitIds { get; } = new List<string>();
    internal List<string> SourceIds { get; } = new List<string>();
    internal List<string> ReportIds { get; } = new List<string>();
    internal List<string> MatchedKeywords { get; } = new List<string>();
    internal List<string> ReferralIds { get; } = new List<string>();
    internal List<string> Errors { get; } = new List<string>();
    internal string BlockedReason { get; set; } = string.Empty;
    internal string SourceVersion { get; set; } = string.Empty;
}

internal sealed class WorldKnowledgeOverlayState
{
    internal int Revision { get; set; }
    internal JArray Operations { get; } = new JArray();
}

internal sealed class WorldKnowledgeDecision
{
    internal string SchemaVersion { get; set; } = WorldKnowledgeDecisionPolicy.SchemaVersion;
    internal string State { get; set; } = WorldKnowledgeDecisionPolicy.NotFound;
    internal string Identity { get; set; } = string.Empty;
    internal string Scope { get; set; } = string.Empty;
    internal string Detail { get; set; } = string.Empty;
    internal string RetrievedText { get; set; } = string.Empty;
    internal string BlockedReason { get; set; } = string.Empty;
    internal string SourceVersion { get; set; } = string.Empty;
    internal string CorrelationId { get; set; } = string.Empty;
    internal string DirectReply { get; set; } = string.Empty;
    internal string Mood { get; set; } = string.Empty;
    internal List<string> ReferralIds { get; } = new List<string>();
    internal List<string> HitIds { get; } = new List<string>();
    internal List<string> SourceIds { get; } = new List<string>();
    internal List<string> ReportIds { get; } = new List<string>();
    internal List<string> Errors { get; } = new List<string>();

    internal bool AllowsAi
    {
        get
        {
            return (StringComparer.Ordinal.Equals(State, WorldKnowledgeDecisionPolicy.Known)
                || StringComparer.Ordinal.Equals(State, WorldKnowledgeDecisionPolicy.Partial))
                && !string.IsNullOrWhiteSpace(RetrievedText);
        }
    }
}

internal static class WorldKnowledgeDecisionPolicy
{
    internal const string SchemaVersion = "awake.knowledge.decision.v1";
    internal const string Known = "known";
    internal const string Partial = "partial";
    internal const string Referral = "referral";
    internal const string Blocked = "blocked";
    internal const string NotFound = "not_found";

    internal static WorldKnowledgeDecision Create(WorldbookQuery query, WorldKnowledgeQueryResult result, string correlationId)
    {
        WorldbookQuery effectiveQuery = query ?? new WorldbookQuery();
        WorldKnowledgeQueryResult safeResult = result ?? new WorldKnowledgeQueryResult
        {
            State = Blocked,
            BlockedReason = "worldbook_unavailable"
        };
        var decision = new WorldKnowledgeDecision
        {
            State = NormalizeState(safeResult.State),
            Identity = string.IsNullOrWhiteSpace(effectiveQuery.IdentityId) ? "unknown" : effectiveQuery.IdentityId,
            Scope = effectiveQuery.KnowledgeScopeAvailable && !string.IsNullOrWhiteSpace(effectiveQuery.KnowledgeScope)
                ? effectiveQuery.KnowledgeScope : "unknown",
            Detail = effectiveQuery.EffectiveDetailAvailable && !string.IsNullOrWhiteSpace(effectiveQuery.EffectiveDetail)
                ? effectiveQuery.EffectiveDetail : (effectiveQuery.RequestedDetail ?? "unknown"),
            SourceVersion = safeResult.SourceVersion ?? string.Empty,
            CorrelationId = correlationId ?? string.Empty,
            BlockedReason = safeResult.BlockedReason ?? string.Empty
        };
        foreach (string error in safeResult.Errors)
        {
            if (!string.IsNullOrWhiteSpace(error)) decision.Errors.Add(error);
        }

        if (StringComparer.Ordinal.Equals(decision.State, Known)
            || StringComparer.Ordinal.Equals(decision.State, Partial))
        {
            decision.RetrievedText = (safeResult.RetrievedText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(decision.RetrievedText))
            {
                decision.State = NotFound;
                decision.BlockedReason = string.Empty;
                decision.Errors.Add("WB2-EMPTY-KNOWLEDGE-TEXT");
            }
            else
            {
                foreach (string hitId in safeResult.HitIds)
                    if (!string.IsNullOrWhiteSpace(hitId)) decision.HitIds.Add(hitId);
                foreach (string sourceId in safeResult.SourceIds)
                    if (!string.IsNullOrWhiteSpace(sourceId)) decision.SourceIds.Add(sourceId);
                foreach (string reportId in safeResult.ReportIds)
                    if (!string.IsNullOrWhiteSpace(reportId)) decision.ReportIds.Add(reportId);
            }
        }
        else if (StringComparer.Ordinal.Equals(decision.State, Referral))
        {
            decision.RetrievedText = (safeResult.RetrievedText ?? string.Empty).Trim();
            foreach (string referralId in safeResult.ReferralIds)
                if (!string.IsNullOrWhiteSpace(referralId)) decision.ReferralIds.Add(referralId);
            if (decision.ReferralIds.Count == 0 && string.IsNullOrWhiteSpace(decision.RetrievedText))
            {
                decision.State = NotFound;
                decision.Errors.Add("WB2-EMPTY-REFERRAL");
            }
        }
        else if (!StringComparer.Ordinal.Equals(decision.State, Blocked)
            && !StringComparer.Ordinal.Equals(decision.State, NotFound))
        {
            decision.State = NotFound;
        }

        if (StringComparer.Ordinal.Equals(decision.State, Blocked))
        {
            if (string.IsNullOrWhiteSpace(decision.BlockedReason)) decision.BlockedReason = "unknown";
            decision.RetrievedText = string.Empty;
            decision.HitIds.Clear();
            decision.ReferralIds.Clear();
        }
        else if (StringComparer.Ordinal.Equals(decision.State, NotFound))
        {
            decision.RetrievedText = string.Empty;
            decision.HitIds.Clear();
            decision.ReferralIds.Clear();
            decision.BlockedReason = string.Empty;
        }

        decision.DirectReply = BuildDirectReply(decision);
        decision.Mood = BuildMood(decision.State);
        return decision;
    }

    internal static string BuildPromptBlock(WorldKnowledgeDecision decision)
    {
        if (decision == null || !decision.AllowsAi) return string.Empty;
        string hits = decision.HitIds.Count == 0 ? "none" : string.Join(",", decision.HitIds);
        string source = string.IsNullOrWhiteSpace(decision.SourceVersion) ? "unknown" : decision.SourceVersion;
        return "知识状态：" + decision.State
            + "\n知识来源：" + source
            + "\n身份：" + decision.Identity
            + "\n知识范围：" + decision.Scope
            + "\n允许详细度：" + decision.Detail
            + "\n命中档案：" + hits
            + "\n知识正文：\n" + decision.RetrievedText;
    }

    internal static string BuildDirectReply(WorldKnowledgeDecision decision)
    {
        if (decision == null) return "我没有可靠的说法。";
        if (StringComparer.Ordinal.Equals(decision.State, Referral))
            return string.IsNullOrWhiteSpace(decision.RetrievedText)
                ? "这方面我不清楚。你最好去问更懂行的人。"
                : decision.RetrievedText;
        if (StringComparer.Ordinal.Equals(decision.State, Blocked))
        {
            if (StringComparer.Ordinal.Equals(decision.BlockedReason, "permission"))
                return "这不是我该知道、也不是我该说的。";
            if (StringComparer.Ordinal.Equals(decision.BlockedReason, "content_gate"))
                return "这话题不便在这里谈。";
            return "我没有可靠的说法。";
        }
        if (StringComparer.Ordinal.Equals(decision.State, NotFound)) return "这件事我没听说过。";
        return string.Empty;
    }

    internal static string BuildMood(string state)
    {
        if (StringComparer.Ordinal.Equals(state, Referral)) return "指路";
        if (StringComparer.Ordinal.Equals(state, Blocked)) return "戒备";
        if (StringComparer.Ordinal.Equals(state, NotFound)) return "茫然";
        if (StringComparer.Ordinal.Equals(state, Partial)) return "谨慎";
        return "平静";
    }

    private static string NormalizeState(string value)
    {
        string state = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (StringComparer.Ordinal.Equals(state, Known)
            || StringComparer.Ordinal.Equals(state, Partial)
            || StringComparer.Ordinal.Equals(state, Referral)
            || StringComparer.Ordinal.Equals(state, Blocked)
            || StringComparer.Ordinal.Equals(state, NotFound)) return state;
        return NotFound;
    }
}

internal sealed class NpcKnowledgePromptBuildResult
{
    internal WorldKnowledgeDecision Knowledge { get; }
    internal string PromptText { get; }
    internal bool ShouldCallAi => Knowledge != null && Knowledge.AllowsAi && !string.IsNullOrWhiteSpace(PromptText);
    internal string DirectReply => Knowledge?.DirectReply ?? "我没有可靠的说法。";
    internal string Mood => Knowledge?.Mood ?? "茫然";

    internal NpcKnowledgePromptBuildResult(WorldKnowledgeDecision knowledge, string promptText)
    {
        Knowledge = knowledge;
        PromptText = promptText ?? string.Empty;
    }
}

