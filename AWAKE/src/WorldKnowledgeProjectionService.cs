using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldKnowledgeProjectionService
{
    private readonly object _gate = new object();
    private readonly List<WorldEventRecord> _events = new List<WorldEventRecord>();
    private readonly List<JObject> _reports = new List<JObject>();
    private WorldKnowledgeSnapshot _snapshot;
    private WorldKnowledgeQueryService _query;
    private int _revision;
    private int _appliedCampaignGeneration = -1;
    private long _appliedLedgerRevision = -1;
    private long _appliedSnapshotRevision = -1;

    internal int Revision
    {
        get { lock (_gate) return _revision; }
    }

    internal void Bind(WorldKnowledgeSnapshot snapshot, WorldKnowledgeQueryService query)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
            _query = query;
            ApplyLocked();
        }
    }

    internal bool TryReplaceSources(
        WorldEventLedgerSnapshot snapshot,
        IReadOnlyList<JObject> reports,
        Func<bool> canApply)
    {
        lock (_gate)
        {
            if (canApply != null && !canApply()) return false;
            if (!CanApplySnapshotLocked(snapshot)) return false;
            ReplaceEventsLocked(snapshot.Records);
            ReplaceReportsLocked(reports);
            MarkSnapshotAppliedLocked(snapshot);
            ApplyLocked();
            return true;
        }
    }

    internal bool TryReplaceEvents(WorldEventLedgerSnapshot snapshot, Func<bool> canApply)
    {
        lock (_gate)
        {
            if (canApply != null && !canApply()) return false;
            if (!CanApplySnapshotLocked(snapshot)) return false;
            ReplaceEventsLocked(snapshot.Records);
            MarkSnapshotAppliedLocked(snapshot);
            ApplyLocked();
            return true;
        }
    }

    internal void Clear()
    {
        lock (_gate)
        {
        _events.Clear();
        _reports.Clear();
        _appliedCampaignGeneration = -1;
        _appliedLedgerRevision = -1;
        _appliedSnapshotRevision = -1;
            ApplyLocked();
        }
    }

    private bool CanApplySnapshotLocked(WorldEventLedgerSnapshot snapshot)
    {
        if (snapshot == null) return false;
        if (snapshot.CampaignGeneration < _appliedCampaignGeneration) return false;
        if (snapshot.CampaignGeneration != _appliedCampaignGeneration) return true;
        return snapshot.LedgerRevision >= _appliedLedgerRevision
            && snapshot.SnapshotRevision >= _appliedSnapshotRevision;
    }

    private void MarkSnapshotAppliedLocked(WorldEventLedgerSnapshot snapshot)
    {
        _appliedCampaignGeneration = snapshot.CampaignGeneration;
        _appliedLedgerRevision = snapshot.LedgerRevision;
        _appliedSnapshotRevision = snapshot.SnapshotRevision;
    }

    private void ApplyLocked()
    {
        _revision++;
        if (_query == null || _snapshot == null) return;
        _query.ReplaceDynamicEntries(BuildEntriesLocked());
    }

    private void ReplaceEventsLocked(IReadOnlyList<WorldEventRecord> events)
    {
        _events.Clear();
        foreach (WorldEventRecord record in events ?? new List<WorldEventRecord>())
            if (record != null) _events.Add(record);
    }

    private void ReplaceReportsLocked(IReadOnlyList<JObject> reports)
    {
        _reports.Clear();
        foreach (JObject report in reports ?? new List<JObject>())
            if (report != null) _reports.Add((JObject)report.DeepClone());
    }

    private List<WorldKnowledgeEntry> BuildEntriesLocked()
    {
        var entries = new List<WorldKnowledgeEntry>();
        foreach (WorldEventRecord record in _events.OrderBy(x => x.Day).ThenBy(x => x.EventId, StringComparer.Ordinal))
        {
            if (!WorldEventContract.CanProject(record)) continue;
            entries.Add(BuildEventEntry(record));
        }
        foreach (JObject report in _reports.OrderBy(x => (string)x["reportId"], StringComparer.Ordinal))
        {
            if (!WorldEventContract.TryValidateWeeklyReport(report, out _)) continue;
            entries.Add(BuildReportEntry(report));
        }
        return entries;
    }

    private WorldKnowledgeEntry BuildEventEntry(WorldEventRecord record)
    {
        string suffix = StableSuffix(record.EventId);
        var entry = new WorldKnowledgeEntry
        {
            Id = "awake:knowledge:event-" + suffix,
            Domain = record.Domain,
            Title = "事件：" + record.Kind,
            Summary = "第 " + record.Day + " 天发生的消息。",
            SourceKind = "event",
            SourceId = record.EventId
        };
        entry.Keywords.Add("事件");
        entry.Keywords.Add(record.Kind);
        entry.Keywords.Add(record.Domain);
        entry.Keywords.Add(record.Text);
        var expression = new WorldKnowledgeExpression
        {
            Id = "awake:expression:event-" + suffix,
            Detail = "summary",
            Text = "第 " + record.Day + " 天：" + record.Text
        };
        AddGrants(expression, "local", "summary", record.VisibilityIdentityIds);
        entry.Expressions.Add(expression);
        return entry;
    }

    private WorldKnowledgeEntry BuildReportEntry(JObject report)
    {
        string reportId = (string)report["reportId"] ?? string.Empty;
        string suffix = StableSuffix(reportId);
        int endDay = (int?)report["extensions"]?["awake:windowEndDay"] ?? 0;
        var entry = new WorldKnowledgeEntry
        {
            Id = "awake:knowledge:report-" + suffix,
            Domain = "politics",
            Title = endDay > 0 ? "世界周报（第 " + endDay + " 天）" : "世界周报",
            Summary = "已结束战役周的事件汇总。",
            SourceKind = "weekly_report",
            SourceId = reportId,
            ReportId = reportId
        };
        foreach (JToken sourceId in (JArray)report["sourceEventIds"] ?? new JArray())
            if (!string.IsNullOrWhiteSpace((string)sourceId)) entry.SourceEventIds.Add((string)sourceId);
        entry.Keywords.Add("周报");
        if (endDay > 0) entry.Keywords.Add("第 " + endDay + " 天");
        var expression = new WorldKnowledgeExpression
        {
            Id = "awake:expression:report-" + suffix,
            Detail = "summary",
            Text = RenderReportText(report)
        };
        AddGrants(expression, "local", "summary", ReadReportAudience(report));
        entry.Expressions.Add(expression);
        return entry;
    }

    private void AddGrants(WorldKnowledgeExpression expression, string scope, string minDetail, IEnumerable<string> audienceIds)
    {
        foreach (string identityId in WorldEventAudience.Resolve(audienceIds).OrderBy(x => x, StringComparer.Ordinal))
        {
            string normalized = WorldbookIdentityEvaluator.NormalizeIdentity(identityId);
            if (!_snapshot.Identities.ContainsKey(normalized)) continue;
            expression.Grants.Add(new WorldKnowledgeRule
            {
                IdentityId = normalized,
                Scope = scope,
                MinDetail = minDetail
            });
        }
    }

    private static IReadOnlyList<string> ReadReportAudience(JObject report)
    {
        var audience = new List<string>();
        JArray identityIds = report?["visibility"]?["identity_ids"] as JArray;
        if (identityIds == null) return WorldEventAudience.Default;
        foreach (JToken token in identityIds)
        {
            string identityId = (string)token;
            if (!string.IsNullOrWhiteSpace(identityId)) audience.Add(identityId);
        }
        return audience;
    }

    private static string RenderReportText(JObject report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("世界周报");
        foreach (JObject section in (JArray)report["sections"] ?? new JArray())
        {
            JArray items = section["items"] as JArray;
            if (items == null || items.Count == 0) continue;
            builder.AppendLine((string)section["title"]?["zh-CN"] ?? (string)section["domain"] ?? string.Empty);
            foreach (JObject item in items)
                builder.AppendLine("· " + ((string)item["text"]?["zh-CN"] ?? string.Empty));
        }
        return builder.ToString().TrimEnd();
    }

    private static string StableSuffix(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            var builder = new StringBuilder(12);
            for (int index = 0; index < 6; index++)
                builder.Append(digest[index].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
