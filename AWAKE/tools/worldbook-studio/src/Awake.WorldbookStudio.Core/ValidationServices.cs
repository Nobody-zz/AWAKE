using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public sealed class RegistrySnapshot
{
    public required JsonObject ProfileRegistry { get; init; }
    public required JsonObject ReferralRegistry { get; init; }
    public required string ProfileVersion { get; init; }
    public required string ReferralVersion { get; init; }
    public required string ProfileHash { get; init; }
    public required string ReferralHash { get; init; }
    public HashSet<string> Profiles { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string?> ProfileParents { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Referrals { get; } = new(StringComparer.Ordinal);
    public HashSet<string> PubliclyAskableReferrals { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, JsonObject> ProfileObjects { get; } = new(StringComparer.Ordinal);
}

public sealed class RegistryService
{
    private readonly WorkspaceService _workspace;
    private readonly SchemaValidator _schema;
    public RegistryService(WorkspaceService workspace, SchemaValidator schema) { _workspace = workspace; _schema = schema; }

    public RegistrySnapshot LoadAndValidate(ValidationReport report, SnapshotInputStore? snapshot = null)
    {
        var profilePath = Path.Combine(_workspace.SchemaRoot, "profile-registry.v1.json");
        var referralPath = Path.Combine(_workspace.SchemaRoot, "referral-registry.v1.json");
        var profileBytes = Array.Empty<byte>();
        var referralBytes = Array.Empty<byte>();
        var profiles = new JsonObject();
        var referrals = new JsonObject();
        if (!File.Exists(profilePath) || !File.Exists(referralPath))
        {
            report.Error("WB-REGISTRY-000", "profile/referral registry 缺失。", _workspace.SchemaRoot);
        }
        else
        {
            profileBytes = snapshot?.GetBytes(profilePath) ?? File.ReadAllBytes(profilePath);
            referralBytes = snapshot?.GetBytes(referralPath) ?? File.ReadAllBytes(referralPath);
            profiles = JsonNode.Parse(profileBytes)?.AsObject() ?? new JsonObject();
            referrals = JsonNode.Parse(referralBytes)?.AsObject() ?? new JsonObject();
            _schema.Validate(profiles, Path.Combine(_workspace.SchemaRoot, "profile-registry.v1.schema.json"), report, snapshot);
            _schema.Validate(referrals, Path.Combine(_workspace.SchemaRoot, "referral-registry.v1.schema.json"), report, snapshot);
        }
        return RegistrySnapshotBuilder.Build(profiles, referrals, profileBytes, referralBytes, report);
    }

    public static IEnumerable<string> GetProfileChain(RegistrySnapshot registry, string profileId)
    {
        var current = profileId;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrWhiteSpace(current) && seen.Add(current))
        {
            yield return current;
            if (!registry.ProfileParents.TryGetValue(current, out var parent)) yield break;
            current = parent;
        }
    }

}

public sealed class SourceRegistryService
{
    private readonly WorkspaceService _workspace;
    private readonly SchemaValidator _schema;
    public SourceRegistryService(WorkspaceService workspace, SchemaValidator schema) { _workspace = workspace; _schema = schema; }

    public Dictionary<(string Id, string Version), JsonObject> LoadAndValidate(ValidationReport report, SnapshotInputStore? snapshot = null)
    {
        var entries = new Dictionary<(string, string), JsonObject>();
        foreach (var item in _workspace.LoadSourceRegistries(snapshot))
        {
            report.Diagnostics.AddRange(item.Report.Diagnostics);
            var id = item.Document["source_id"]?.GetValue<string>() ?? "";
            var version = item.Document["source_version"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(version)) entries[(id, version)] = item.Document;
            ValidateSourceFile(item.Path, item.Document, report, snapshot);
        }
        return entries;
    }

    public void ValidateDocumentSources(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents, Dictionary<(string Id, string Version), JsonObject> registry, ValidationReport report, SnapshotInputStore? snapshot = null)
    {
        foreach (var item in documents)
        {
            ValidateRefs(item.Document["sources"]?.AsArray(), item.Path, registry, report, snapshot);
            foreach (var assertion in item.Document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                ValidateRefs(assertion["sources"]?.AsArray(), $"{item.Path}#/assertions/{assertion["id"]}", registry, report, snapshot);
                foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
                    ValidateRefs(expression["sources"]?.AsArray(), $"{item.Path}#/expressions/{expression["id"]}", registry, report, snapshot);
            }
        }
    }

    private void ValidateRefs(JsonArray? refs, string path, Dictionary<(string Id, string Version), JsonObject> registry, ValidationReport report, SnapshotInputStore? snapshot)
    {
        foreach (var sourceRef in refs?.OfType<JsonObject>() ?? [])
        {
            var id = sourceRef["source_id"]?.GetValue<string>() ?? "";
            var version = sourceRef["source_version"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(version) || !registry.TryGetValue((id, version), out var registered))
            {
                report.Error("WB-SOURCE-001", "正典 source_ref 未命中有效来源登记或缺少 source_version。", path, $"{id}@{version}");
                continue;
            }

            var expectedHash = registered["source_content_hash"]?.GetValue<string>();
            var actualHash = sourceRef["source_content_hash"]?.GetValue<string>();
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                report.Error("WB-SOURCE-001", "source_content_hash 与来源登记不一致。", path, id);

            var quote = sourceRef["quote"]?.GetValue<string>();
            var quoteHash = sourceRef["quote_hash"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(quote) && !string.Equals(Hashing.Sha256Text(quote), quoteHash, StringComparison.OrdinalIgnoreCase))
                report.Error("WB-SOURCE-001", "quote_hash 与 quote 不一致。", path, id);

            var locatorRoot = registered["locator_root"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(locatorRoot))
            {
                var sourcePath = Path.Combine(_workspace.Root, "authoring", "sources", locatorRoot);
                if (!File.Exists(sourcePath))
                {
                    report.Error("WB-SOURCE-001", "来源登记 locator_root 文件不存在。", sourcePath, id);
                }
                else
                {
                    var bytes = snapshot?.GetBytes(sourcePath) ?? File.ReadAllBytes(sourcePath);
                    if (!string.Equals(Hashing.Sha256Bytes(bytes), expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        report.Error("WB-SOURCE-001", "来源文件内容 hash 与登记不一致。", sourcePath, id);
                    }
                    else if (!string.IsNullOrEmpty(quote)
                        && !SourceEvidenceMatcher.TryFind(System.Text.Encoding.UTF8.GetString(bytes), quote, out _))
                    {
                        report.Error("WB-SOURCE-001", "引文 quote 未能在来源文件中定位。", path, id);
                    }
                }
            }

            if (registered["license_status"]?.GetValue<string>() is "blocked" or "unknown" || registered["use_status"]?.GetValue<string>() is "blocked" or "expired" or "unknown")
                report.Error("WB-SOURCE-001", "来源登记状态不允许进入正典。", path, id);
        }
    }

    private void ValidateSourceFile(string path, JsonObject source, ValidationReport report, SnapshotInputStore? snapshot)
    {
        var locatorRoot = source["locator_root"]?.GetValue<string>();
        var expected = source["source_content_hash"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(locatorRoot) || string.IsNullOrWhiteSpace(expected)) return;
        var sourcePath = Path.Combine(_workspace.Root, "authoring", "sources", locatorRoot);
        if (File.Exists(sourcePath) && !string.Equals(Hashing.Sha256Bytes(snapshot?.GetBytes(sourcePath) ?? File.ReadAllBytes(sourcePath)), expected, StringComparison.OrdinalIgnoreCase))
            report.Error("WB-SOURCE-001", "来源文件 hash 与 source registry 不一致。", path, locatorRoot);
    }
}

public sealed class AuditLedgerService
{
    private readonly WorkspaceService _workspace;
    private readonly SchemaValidator _schema;
    public AuditLedgerService(WorkspaceService workspace, SchemaValidator schema) { _workspace = workspace; _schema = schema; }

    public Dictionary<string, JsonObject> LoadAndValidate(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents, ValidationReport report, SnapshotInputStore? snapshot = null)
    {
        var events = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var streams = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);
        foreach (var file in _workspace.EnumerateAuditFiles())
        {
            var sequence = 0;
            foreach (var line in snapshot?.GetLines(file) ?? File.ReadLines(file))
            {
                sequence++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                JsonObject item;
                try { item = JsonNode.Parse(line)?.AsObject() ?? new JsonObject(); }
                catch (Exception ex) { report.Error("WB-AUDIT-001", "审计事件 JSONL 无法解析。", $"{file}:{sequence}", ex.Message); continue; }
                _schema.Validate(item, Path.Combine(_workspace.SchemaRoot, "audit-event.v1.schema.json"), report, snapshot);
                var id = item["event_id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!events.TryAdd(id, item)) report.Error("WB-AUDIT-001", "审计 event_id 重复。", file, id);
                var stream = item["stream_id"]?.GetValue<string>() ?? "";
                if (!streams.TryGetValue(stream, out var list)) streams[stream] = list = [];
                list.Add(item);
                var expectedHash = HashWithout(item, "event_hash");
                if (!string.Equals(item["event_hash"]?.GetValue<string>(), expectedHash, StringComparison.OrdinalIgnoreCase))
                    report.Error("WB-AUDIT-001", "event_hash 不匹配。", id, expectedHash);
            }
        }

        foreach (var (streamId, list) in streams)
        {
            var ordered = list.OrderBy(x => x["sequence"]?.GetValue<long>() ?? 0).ToList();
            string? previous = null;
            for (var index = 0; index < ordered.Count; index++)
            {
                var item = ordered[index];
                var actualSequence = item["sequence"]?.GetValue<long>() ?? 0;
                if (actualSequence != index + 1) report.Error("WB-AUDIT-001", "审计 stream sequence 不连续。", streamId, $"expected={index + 1}, actual={actualSequence}");
                var actualPrevious = item["previous_event_hash"]?.GetValue<string>();
                if (!string.Equals(actualPrevious, previous, StringComparison.OrdinalIgnoreCase)) report.Error("WB-AUDIT-001", "审计 previous_event_hash 链断裂。", streamId, item["event_id"]?.GetValue<string>());
                previous = item["event_hash"]?.GetValue<string>();
            }
        }

        var objects = BuildObjectMap(documents);
        foreach (var authorCreated in EnumerateAuthorCreated(documents))
        {
            if (!string.Equals(authorCreated.Value["review_status"]?.GetValue<string>(), "approved", StringComparison.OrdinalIgnoreCase)) continue;
            var eventId = authorCreated.Value["review_event_id"]?.GetValue<string>();
            var objectId = authorCreated.Key.Id;
            if (string.IsNullOrWhiteSpace(eventId) || !events.TryGetValue(eventId, out var audit) || audit["event_type"]?.GetValue<string>() != "approval" || audit["decision"]?.GetValue<string>() != "approved")
            {
                report.Error("WB-CANON-001", "原创正典批准事件不存在或不是有效 approval。", objectId, eventId);
                continue;
            }
            var actualHash = objects.TryGetValue(objectId, out var obj) ? CanonicalJson.Hash(obj) : "";
            if (!string.Equals(audit["object_id"]?.GetValue<string>(), objectId, StringComparison.Ordinal) || audit["object_revision"]?.GetValue<long>() != authorCreated.Key.Revision || !string.Equals(audit["object_hash"]?.GetValue<string>(), actualHash, StringComparison.OrdinalIgnoreCase))
                report.Error("WB-CANON-001", "批准审计事件的对象或 hash 与原创内容不匹配。", objectId, $"event={eventId}, expected={actualHash}");
        }
        return events;
    }

    public void ValidateLedger(ValidationReport report, SnapshotInputStore? snapshot = null)
    {
        var records = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var file in _workspace.EnumerateLedgerFiles())
        {
            var lineNumber = 0;
            foreach (var line in snapshot?.GetLines(file) ?? File.ReadLines(file))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                JsonObject record;
                try { record = JsonNode.Parse(line)?.AsObject() ?? new JsonObject(); }
                catch (Exception ex) { report.Error("WB-LEDGER-001", "ID ledger JSONL 无法解析。", $"{file}:{lineNumber}", ex.Message); continue; }
                _schema.Validate(record, Path.Combine(_workspace.SchemaRoot, "id-ledger.v1.schema.json"), report, snapshot);
                var id = record["id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!records.TryAdd(id, record)) report.Error("WB-LEDGER-001", "ID ledger 存在重复稳定 ID。", file, id);
                if (!string.Equals(record["record_hash"]?.GetValue<string>(), HashWithout(record, "record_hash"), StringComparison.OrdinalIgnoreCase))
                    report.Error("WB-LEDGER-001", "ID ledger record_hash 不匹配。", id);
                if (record["last_state"]?.GetValue<string>() == "tombstoned" && records.TryGetValue(id, out var previous) && previous != record && previous["last_state"]?.GetValue<string>() == "active")
                    report.Error("WB-LEDGER-001", "tombstoned ID 不应重新登记为 active。", id);
            }
        }
    }

    private static Dictionary<string, JsonObject> BuildObjectMap(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var item in documents)
        {
            var document = item.Document;
            Add(document);
            foreach (var assertion in document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                Add(assertion);
                foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? []) Add(expression);
            }
        }
        return result;
        void Add(JsonObject obj)
        {
            var id = obj["id"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id)) result[id] = obj;
        }
    }

    private static IEnumerable<KeyValuePair<(string Id, int Revision), JsonObject>> EnumerateAuthorCreated(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents)
    {
        foreach (var item in documents)
        {
            if (item.Document["author_created"] is JsonObject documentCreated) yield return new(new(item.Document["id"]!.GetValue<string>(), (int)item.Document["revision"]!.GetValue<long>()), documentCreated);
            foreach (var assertion in item.Document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                if (assertion["author_created"] is JsonObject assertionCreated) yield return new(new(assertion["id"]!.GetValue<string>(), (int)assertion["revision"]!.GetValue<long>()), assertionCreated);
                foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
                    if (expression["author_created"] is JsonObject expressionCreated) yield return new(new(expression["id"]!.GetValue<string>(), (int)expression["revision"]!.GetValue<long>()), expressionCreated);
            }
        }
    }

    private static string HashWithout(JsonObject value, string property)
    {
        var copy = JsonNode.Parse(value.ToJsonString())!.AsObject();
        copy.Remove(property);
        return CanonicalJson.Hash(copy);
    }
}

