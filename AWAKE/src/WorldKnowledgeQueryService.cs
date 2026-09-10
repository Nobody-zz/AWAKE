using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldKnowledgeQueryService : IWorldKnowledgeQuery
{
    private sealed class EntrySelection
    {
        internal WorldKnowledgeExpression Expression { get; set; }
        internal bool PermissionLimited { get; set; }
        internal List<string> ReferralIds { get; } = new List<string>();
    }

    private readonly WorldKnowledgeSnapshot _snapshot;
    private readonly WorldKnowledgeOverlayState _overlay = new WorldKnowledgeOverlayState();
    private readonly object _gate = new object();
    private readonly HashSet<string> _dynamicEntryIds = new HashSet<string>(StringComparer.Ordinal);
    private int _dynamicRevision;
    internal WorldKnowledgeQueryService(WorldKnowledgeSnapshot snapshot) { _snapshot = snapshot; }
    internal int EntryCount { get { lock (_gate) return _snapshot.Entries.Count; } }
    internal int IdentityCount { get { lock (_gate) return _snapshot.Identities.Count; } }
    internal int ReferralCount { get { lock (_gate) return _snapshot.Referrals.Count; } }
    internal int OverlayRevision { get { lock (_gate) return _overlay.Revision; } }
    internal int DynamicRevision { get { lock (_gate) return _dynamicRevision; } }
    internal IReadOnlyList<string> Warnings { get { lock (_gate) return _snapshot.Warnings.ToArray(); } }

    public WorldKnowledgeQueryResult Query(WorldbookQuery query)
    {
        query = query ?? new WorldbookQuery();
        lock (_gate)
        {
            var result = new WorldKnowledgeQueryResult
            {
                ByteBudget = query.MaximumBytes > 0 ? query.MaximumBytes : 4096,
                SourceVersion = _snapshot.PackageId + "@" + _snapshot.Version + "+dynamic=" + _dynamicRevision
            };
            if (!ContentGateAllows(query, result)) return result;
            var candidates = FindCandidates(query.PlayerText);
            result.MatchMode = candidates.Count == 0 ? "identity" : "keyword";
            WorldKnowledgeIdentityEvaluation evaluation = WorldbookIdentityEvaluator.Evaluate(query, _snapshot);
            var builder = new StringBuilder();
            bool sawKnown = false;
            bool sawPartial = false;
            bool sawHardBlocked = false;
            bool sawPermissionLimited = false;
            foreach (WorldKnowledgeEntry entry in candidates)
            {
                if (HasMatchingDeny(entry, query, evaluation))
                {
                    sawHardBlocked = true;
                    continue;
                }
                EntrySelection selection = SelectExpression(entry, query, evaluation);
                if (selection.Expression == null)
                {
                    if (selection.PermissionLimited) sawPermissionLimited = true;
                    foreach (string referralId in selection.ReferralIds) AddReferral(result, referralId);
                    continue;
                }
                bool partial = WorldbookIdentityEvaluator.DetailRank(selection.Expression.Detail) < WorldbookIdentityEvaluator.DetailRank(query.RequestedDetail);
                string text = FormatEntry(entry, selection.Expression, !partial);
                if (Encoding.UTF8.GetByteCount(builder.ToString() + text + Environment.NewLine) > result.ByteBudget) break;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(text);
                result.HitIds.Add(entry.Id);
                if (!string.IsNullOrWhiteSpace(entry.SourceId) && !result.SourceIds.Contains(entry.SourceId, StringComparer.Ordinal)) result.SourceIds.Add(entry.SourceId);
                if (!string.IsNullOrWhiteSpace(entry.ReportId) && !result.ReportIds.Contains(entry.ReportId, StringComparer.Ordinal)) result.ReportIds.Add(entry.ReportId);
                sawKnown |= !partial;
                sawPartial |= partial;
            }
            if (builder.Length > 0)
            {
                result.State = sawKnown ? "known" : "partial";
            }
            else if (result.ReferralIds.Count > 0 && !sawHardBlocked)
            {
                result.State = "referral";
                builder.Append("这方面我不清楚。你可以去问：");
                builder.Append(string.Join("、", result.ReferralIds.Select(id => _snapshot.Referrals.TryGetValue(id, out WorldKnowledgeReferral referral) ? referral.DisplayName : id)));
            }
            else if (sawHardBlocked || sawPermissionLimited)
            {
                SetBlocked(result, "permission");
            }
            result.RetrievedText = builder.ToString().Trim();
            return result;
        }
    }

    public List<WorldKnowledgeEntry> Search(string text, int limit)
    {
        if (string.IsNullOrWhiteSpace(text) || limit <= 0) return new List<WorldKnowledgeEntry>();
        lock (_gate)
        {
            return _snapshot.Entries.Values.Where(x => x.Id.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || x.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || x.Keywords.Any(k => k.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(x => x.Id, StringComparer.Ordinal).Take(limit).ToList();
        }
    }

    public string BuildStatusText()
    {
        lock (_gate)
        {
            return "v2 package=" + _snapshot.PackageId + " entries=" + _snapshot.Entries.Count + " dynamic=" + _dynamicEntryIds.Count + " identities=" + _snapshot.Identities.Count + " referrals=" + _snapshot.Referrals.Count + " revision=" + _snapshot.Revision + " dynamic_revision=" + _dynamicRevision;
        }
    }

    internal void ReplaceDynamicEntries(IEnumerable<WorldKnowledgeEntry> entries)
    {
        lock (_gate)
        {
            foreach (string entryId in _dynamicEntryIds)
                _snapshot.Entries.Remove(entryId);
            _dynamicEntryIds.Clear();
            foreach (WorldKnowledgeEntry entry in entries ?? Enumerable.Empty<WorldKnowledgeEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || _snapshot.Entries.ContainsKey(entry.Id)) continue;
                _snapshot.Entries[entry.Id] = entry;
                _dynamicEntryIds.Add(entry.Id);
            }
            RebuildKeywordIndex();
            _dynamicRevision++;
        }
    }

    internal bool TryApplyOverlay(string kind, string targetId, string value, int baseRevision, string reason, out string error)
    {
        lock (_gate)
        {
            error = string.Empty;
            if (baseRevision != _overlay.Revision) { error = "WB2-OVERLAY-CAS"; return false; }
            bool changed = false;
            if ((kind == "replace_title" || kind == "replace_summary" || kind == "add_keyword" || kind == "remove_keyword") && _snapshot.Entries.TryGetValue(targetId, out WorldKnowledgeEntry entry))
            {
                if (kind == "replace_title") { entry.Title = value ?? string.Empty; changed = true; }
                else if (kind == "replace_summary") { entry.Summary = value ?? string.Empty; changed = true; }
                else if (kind == "add_keyword" && !entry.Keywords.Contains(value ?? string.Empty)) { entry.Keywords.Add(value ?? string.Empty); changed = true; }
                else if (kind == "remove_keyword") { changed = entry.Keywords.Remove(value ?? string.Empty); }
                if (changed) RebuildKeywordIndex();
            }
            else if (kind == "replace_text" || kind == "disable_expression" || kind == "enable_expression")
            {
                WorldKnowledgeExpression expression = FindExpression(targetId);
                if (expression != null)
                {
                    if (kind == "replace_text") { expression.Text = value ?? string.Empty; changed = true; }
                    else { expression.Enabled = kind == "enable_expression"; changed = true; }
                }
            }
            else { error = "WB2-OVERLAY-FORBIDDEN"; return false; }
            if (!changed) { error = "WB2-REFERENCE-MISSING"; return false; }
            _overlay.Revision++;
            _overlay.Operations.Add(new JObject
            {
                ["operationId"] = "awake:operation:" + _overlay.Revision,
                ["targetId"] = targetId,
                ["kind"] = kind,
                ["baseRevision"] = baseRevision,
                ["value"] = value,
                ["reason"] = new JObject { ["zh-CN"] = reason ?? "玩家编辑" }
            });
            return true;
        }
    }

    internal JObject ExportOverlay()
    {
        lock (_gate)
        {
            return new JObject
            {
                ["schemaVersion"] = "awake.worldbook.overlay.v1",
                ["overlayId"] = "awake:overlay:campaign",
                ["baseActivationId"] = "awake:activation:current",
                ["revision"] = _overlay.Revision,
                ["operations"] = _overlay.Operations.DeepClone()
            };
        }
    }

    internal bool TryImportOverlay(JObject overlay, out string error)
    {
        lock (_gate)
        {
            error = string.Empty;
            if (overlay == null) { error = "WB2-OVERLAY-IMPORT"; return false; }
            int targetRevision = overlay["revision"]?.Value<int>() ?? 0;
            if (targetRevision <= _overlay.Revision) return true;
            JArray operations = overlay["operations"] as JArray;
            if (operations == null) { error = "WB2-OVERLAY-IMPORT"; return false; }
            foreach (JObject operation in operations)
            {
                string kind = operation["kind"]?.Value<string>() ?? string.Empty;
                string targetId = operation["targetId"]?.Value<string>() ?? string.Empty;
                string value = operation["value"]?.Value<string>() ?? string.Empty;
                int baseRevision = operation["baseRevision"]?.Value<int>() ?? -1;
                if (!TryApplyOverlay(kind, targetId, value, baseRevision, "导入战役 Overlay", out error)) return false;
            }
            return _overlay.Revision == targetRevision;
        }
    }

    private List<WorldKnowledgeEntry> FindCandidates(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return _snapshot.Entries.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in _snapshot.KeywordIndex)
        {
            if (text.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) < 0 && pair.Key.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) continue;
            foreach (string id in pair.Value) ids.Add(id);
        }
        return ids.Select(id => _snapshot.Entries.TryGetValue(id, out WorldKnowledgeEntry entry) ? entry : null).Where(x => x != null).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
    }

    private bool HasMatchingDeny(WorldKnowledgeEntry entry, WorldbookQuery query, WorldKnowledgeIdentityEvaluation evaluation)
    {
        foreach (WorldKnowledgeExpression expression in entry.Expressions)
        {
            if (!expression.Enabled) continue;
            if (expression.Denies.Any(rule => WorldbookIdentityEvaluator.MatchesIdentityAndConditions(rule, query, _snapshot, evaluation, out _))) return true;
        }
        return false;
    }

    private EntrySelection SelectExpression(WorldKnowledgeEntry entry, WorldbookQuery query, WorldKnowledgeIdentityEvaluation evaluation)
    {
        var selection = new EntrySelection();
        WorldKnowledgeExpression best = null;
        int bestScore = -1;
        int requestedDetail = WorldbookIdentityEvaluator.DetailRank(query.RequestedDetail);
        foreach (WorldKnowledgeExpression expression in entry.Expressions)
        {
            if (!expression.Enabled) continue;
            foreach (WorldKnowledgeRule grant in expression.Grants)
            {
                if (!WorldbookIdentityEvaluator.MatchesIdentityAndConditions(grant, query, _snapshot, evaluation, out int ruleScore)) continue;
                bool publicGrant = StringComparer.Ordinal.Equals(WorldbookIdentityEvaluator.NormalizeIdentity(grant.IdentityId), "awake:identity:public");
                if (!publicGrant && !evaluation.CapabilitiesAvailable)
                {
                    selection.PermissionLimited = true;
                    continue;
                }
                if (!WorldbookIdentityEvaluator.CapabilityMatches(grant, evaluation))
                {
                    selection.PermissionLimited = true;
                    if (evaluation.CapabilitiesAvailable) AddReferralIds(selection.ReferralIds, grant.ReferralIds);
                    continue;
                }
                int expressionDetail = WorldbookIdentityEvaluator.DetailRank(expression.Detail);
                if (expressionDetail < 0 || requestedDetail < 0 || expressionDetail > requestedDetail)
                {
                    selection.PermissionLimited = true;
                    continue;
                }
                int score = ruleScore * 10 + expressionDetail;
                if (score > bestScore)
                {
                    best = expression;
                    bestScore = score;
                }
            }
        }
        selection.Expression = best;
        return selection;
    }

    private void AddReferralIds(List<string> target, IEnumerable<string> ids)
    {
        foreach (string id in ids ?? Enumerable.Empty<string>())
            if (!string.IsNullOrWhiteSpace(id) && _snapshot.Referrals.TryGetValue(id, out WorldKnowledgeReferral referral) && referral.PubliclyAskable && !target.Contains(id, StringComparer.Ordinal)) target.Add(id);
    }

    private void AddReferral(WorldKnowledgeQueryResult result, string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && _snapshot.Referrals.TryGetValue(id, out WorldKnowledgeReferral referral) && referral.PubliclyAskable && !result.ReferralIds.Contains(id, StringComparer.Ordinal)) result.ReferralIds.Add(id);
    }

    private static string FormatEntry(WorldKnowledgeEntry entry, WorldKnowledgeExpression expression, bool includeHeader)
    {
        if (!includeHeader) return expression.Text;
        string summary = string.IsNullOrWhiteSpace(entry.Summary) ? string.Empty : entry.Summary + Environment.NewLine;
        return "【" + entry.Title + "】" + Environment.NewLine + summary + expression.Text;
    }

    private bool ContentGateAllows(WorldbookQuery query, WorldKnowledgeQueryResult result)
    {
        string packageTier = WorldbookIdentityEvaluator.NormalizeCapability(_snapshot.ContentTier);
        string requestedTier = WorldbookIdentityEvaluator.NormalizeCapability(query.ContentTier);
        if (packageTier != "base" && packageTier != "adult_optional")
        {
            SetBlocked(result, "content_gate");
            return false;
        }
        if (packageTier == "adult_optional" && requestedTier != "adult_optional")
        {
            SetBlocked(result, "content_gate");
            return false;
        }
        return true;
    }

    private static void SetBlocked(WorldKnowledgeQueryResult result, string reason)
    {
        result.State = "blocked";
        result.BlockedReason = reason ?? "unknown";
        result.Errors.Add("WB2-QUERY-BLOCKED");
        result.RetrievedText = string.Empty;
        result.HitIds.Clear();
        result.ReferralIds.Clear();
    }

    private WorldKnowledgeExpression FindExpression(string id)
    {
        foreach (WorldKnowledgeEntry entry in _snapshot.Entries.Values)
        {
            WorldKnowledgeExpression expression = entry.Expressions.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.Id, id));
            if (expression != null) return expression;
        }
        return null;
    }

    private void RebuildKeywordIndex()
    {
        _snapshot.KeywordIndex.Clear();
        foreach (WorldKnowledgeEntry entry in _snapshot.Entries.Values)
            foreach (string keyword in entry.Keywords)
            {
                if (!_snapshot.KeywordIndex.TryGetValue(keyword, out List<string> ids)) _snapshot.KeywordIndex[keyword] = ids = new List<string>();
                if (!ids.Contains(entry.Id)) ids.Add(entry.Id);
            }
    }
}

