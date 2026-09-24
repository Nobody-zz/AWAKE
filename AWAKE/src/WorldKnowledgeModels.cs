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
    // 兜底 term 索引（2026-09-16）：标题/综述切成的 term → 条目 id。
    // **与 KeywordIndex 并列、互不混**：关键词那条路径仍走「整串包含」，只有它一无所获时才查这张表。
    // 起因：中文没有空格 ⇒ 查询「斯特吉亚的军队怎么打仗」永远不含关键词整串 ⇒ 零命中。
    internal Dictionary<string, List<string>> FallbackTermIndex { get; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
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
    internal List<string> SourceFactIds { get; } = new List<string>();
    internal List<string> Keywords { get; } = new List<string>();
    internal List<WorldKnowledgeExpression> Expressions { get; } = new List<WorldKnowledgeExpression>();
    /// 互引边（2026-09-20）：本档正文**点名过谁**。编译期由边表写进 `extensions.links`。
    /// 顺序即权重（强→弱、专名→枢纽），因为编译器已经排好序（见 Studio 的 LinkRegistryService）。
    internal List<WorldKnowledgeLink> Links { get; } = new List<WorldKnowledgeLink>();
}

/// 一条互引边。语义是「本档正文里出现过 `ViaName`，而 `ViaName` 是 `To` 那档的名字」
/// —— 保的是**提到**，不是**该一起答**。所以消费它时只能当低权重线索（见 QueryService 的 LinkExpand*）。
internal sealed class WorldKnowledgeLink
{
    internal string To { get; set; } = string.Empty;
    internal string ViaName { get; set; } = string.Empty;
    internal string Strength { get; set; } = "weak";
    internal string Bucket { get; set; } = "proper";
    internal List<string> UsableAs { get; } = new List<string>();
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
    // 家族归属（2026-09-24）：这条说法只送给「属于某个家族」的人。
    // 与 IsClanLeader 的分工：那个问「他是不是族长」，这个问「他是哪家的人」。
    internal List<string> ClanIds { get; } = new List<string>();
    // 具体某人（2026-09-25）：这条说法只送给「这一个人」。
    // 与 ClanIds 的分工：那个问「他是不是哪家的人」（一族都得给），这个问「他是不是他本人」。
    internal List<string> HeroIds { get; } = new List<string>();
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
    /// 这次答案里**由互引边扩进来**（不是直接命中）的条目 id（2026-09-20）。
    /// 与 `HitIds` 的关系：`LinkIds ⊆ HitIds`，回答「送的这几条里哪几条是捎带的」。
    internal List<string> LinkIds { get; } = new List<string>();
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

    /// <summary>
    /// 这一轮要不要把玩家的话交给模型（2026-09-18 改语义）。
    /// **旧语义是「知道才开口」**：只在 known/partial 且有正文时为 true。
    /// **新语义是「只有故障与合规才闭嘴」**：blocked（世界书不可用／权限／内容门）拦住，
    /// referral 本版保持现状，**not_found 放行**——它只是拿不到知识，不该因此不开口。
    /// 依据：<c>docs/DECISION-20260916-无命中仍须正常对话.md</c>（甲方 09-17 23:40 批「可以，那就做」）
    /// 与 <c>docs/AWAKE-KNOWLEDGE-GATE-20260916.md</c> §七。
    /// ⚠️ 这里只管「**能不能开口**」。「**给不给知识**」是另一根绳子，在 <see cref="WorldKnowledgeDecisionPolicy.BuildPromptBlock"/> ——
    /// 两者曾经共用这一个开关，正是那次裁决要拆开的东西。**不要再把它们合并回去。**
    /// </summary>
    internal bool AllowsAi
    {
        get
        {
            if (StringComparer.Ordinal.Equals(State, WorldKnowledgeDecisionPolicy.Blocked)) return false;
            if (StringComparer.Ordinal.Equals(State, WorldKnowledgeDecisionPolicy.Referral)) return false;
            if (StringComparer.Ordinal.Equals(State, WorldKnowledgeDecisionPolicy.NotFound)) return true;
            return !string.IsNullOrWhiteSpace(RetrievedText);
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

    /// <summary>
    /// 送给模型的「知识」那一格（2026-09-18 改）。
    /// **只管给不给知识，不管开不开口**：not_found／referral／blocked 一律返回空串。
    /// 模型要理解"这一段为什么是空的"，靠的是模板里那句就地说明 ——
    /// ⚠️ **那句目前还没写**（模板 <c>src/Prompts/NpcPromptTemplate.cs</c> 的【检索到的知识】段今天仍是裸格子，
    /// 渲染出来是字面的 <c>""</c>），与"内置提示词怎么完善"一起规划。别把这条注释读成"已经写了"。
    /// ⚠️ **不要在这里塞一段"我不清楚"的文案**：那样 <c>NpcDialogueContextDiagnostics</c> 的
    /// <c>dialogue_context.knowledge</c> 会一律变成 present，"这一轮到底有没有世界书事实"就没法读了。
    /// 🚩 **09-18 离线实测（27 次采样）——"在这一格上做文章"整条路走不通，别再试第七种写法。**
    /// 空串／告知句／禁令句／放开字数下限／结构化状态行／状态行＋短指令，**六种写法全部 27/27 编造**；
    /// 同一探针的阳性对照（知识格填真知识）6/6 精确照说 ⇒ 不是探针太钝。详见
    /// <c>tools/_ollama_knowledge_gap_probe_20260918.py</c> 与 <c>tools/_probe_result_20260918.txt</c>。
    /// **病灶在产出侧**：模板逼它给"具体、有画面感、80-180 字"的答案，而"我不知道"八个字无处安放。
    /// </summary>
    internal static string BuildPromptBlock(WorldKnowledgeDecision decision)
    {
        if (decision == null) return string.Empty;
        if (string.IsNullOrWhiteSpace(decision.RetrievedText)) return string.Empty;
        if (!StringComparer.Ordinal.Equals(decision.State, Known)
            && !StringComparer.Ordinal.Equals(decision.State, Partial)) return string.Empty;
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

