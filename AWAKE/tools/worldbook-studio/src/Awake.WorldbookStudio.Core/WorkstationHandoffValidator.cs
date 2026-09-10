using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using System.Diagnostics.CodeAnalysis;

namespace Awake.WorldbookStudio.Core;

internal sealed class WorkstationHandoffException : InvalidOperationException
{
    public WorkstationHandoffException(string code, string message, int statusCode, string? path = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Path = path;
    }

    public string Code { get; }
    public int StatusCode { get; }
    public string? Path { get; }
}

internal sealed record WorkstationHandoffEnvelope(
    string SchemaVersion,
    string HandoffId,
    string WorkspaceId,
    string DocumentId,
    int Revision,
    string ContentSha256,
    string Producer,
    JsonObject Provenance,
    bool ReviewOnly,
    string ReviewStatus,
    string IssuedAtUtc,
    string ExpiresAtUtc,
    string Payload,
    string RequestFingerprint)
{
    public JsonObject ToJson()
    {
        return new JsonObject
        {
            ["schema_version"] = SchemaVersion,
            ["handoff_id"] = HandoffId,
            ["workspace_id"] = WorkspaceId,
            ["document_id"] = DocumentId,
            ["revision"] = Revision,
            ["content_sha256"] = ContentSha256,
            ["producer"] = Producer,
            ["provenance"] = Provenance.DeepClone(),
            ["review_only"] = ReviewOnly,
            ["review_status"] = ReviewStatus,
            ["issued_at_utc"] = IssuedAtUtc,
            ["expires_at_utc"] = ExpiresAtUtc,
            ["payload"] = Payload,
            ["request_fingerprint"] = RequestFingerprint
        };
    }
}

internal static class WorkstationHandoffValidator
{
    public const string EnvelopeSchemaVersion = "awake.workstation.handoff-envelope.v1";
    public const string ReceiptSchemaVersion = "awake.workstation.handoff-receipt.v1";
    private static readonly HashSet<string> EnvelopeFields = new(StringComparer.Ordinal)
    {
        "schema_version", "handoff_id", "workspace_id", "document_id", "revision",
        "content_sha256", "producer", "provenance", "review_only", "review_status",
        "issued_at_utc", "expires_at_utc", "payload", "request_fingerprint"
    };
    private static readonly HashSet<string> ProvenanceFields = new(StringComparer.Ordinal)
    {
        "source_schema", "source_id", "source_revision", "source_sha256", "issuer_id"
    };

    public static WorkstationHandoffEnvelope Validate(JsonObject envelope, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        RequireExactFields(envelope, EnvelopeFields, "envelope");

        var schemaVersion = RequiredString(envelope, "schema_version", "envelope");
        if (schemaVersion != EnvelopeSchemaVersion)
            Malformed("schema_version", "schema_version 无效。");

        var handoffId = RequiredString(envelope, "handoff_id", "envelope");
        if (!System.Text.RegularExpressions.Regex.IsMatch(handoffId, "^(pwb|wbs|ui)-handoff-[a-f0-9]{32}$"))
            Malformed("handoff_id", "handoff_id 格式无效。");

        var workspaceId = RequiredString(envelope, "workspace_id", "envelope");
        if (!System.Text.RegularExpressions.Regex.IsMatch(workspaceId, "^[a-z0-9][a-z0-9._-]{2,127}$"))
            Malformed("workspace_id", "workspace_id 格式无效。");

        var documentId = RequiredString(envelope, "document_id", "envelope");
        if (documentId.Length > 128 || documentId.Any(char.IsControl))
            Malformed("document_id", "document_id 格式无效。");

        var revision = RequiredInt(envelope, "revision", "envelope");
        if (revision < 1) Malformed("revision", "revision 必须大于等于 1。");

        var contentSha256 = RequiredString(envelope, "content_sha256", "envelope");
        RequireHashShape(contentSha256, "content_sha256");

        var producer = RequiredString(envelope, "producer", "envelope");
        if (producer is not ("persona_workbench" or "worldbook_studio" or "ui_workstation"))
            Malformed("producer", "producer 不在冻结枚举中。");
        var expectedPrefix = producer switch
        {
            "persona_workbench" => "pwb-",
            "worldbook_studio" => "wbs-",
            _ => "ui-"
        };
        if (!handoffId.StartsWith(expectedPrefix, StringComparison.Ordinal))
            Semantic("handoff_id", "handoff_id 前缀必须与 producer 一致。", 422);

        var provenance = envelope["provenance"] as JsonObject
            ?? throw new WorkstationHandoffException("WB-HANDOFF-400", "provenance 必须是对象。", 400, "provenance");
        RequireExactFields(provenance, ProvenanceFields, "provenance");
        var sourceSchema = RequiredString(provenance, "source_schema", "provenance");
        var sourceId = RequiredString(provenance, "source_id", "provenance");
        var sourceRevision = RequiredInt(provenance, "source_revision", "provenance");
        var sourceSha256 = RequiredString(provenance, "source_sha256", "provenance");
        var issuerId = RequiredString(provenance, "issuer_id", "provenance");
        if (sourceRevision < 1) Semantic("provenance.source_revision", "source_revision 必须大于等于 1。", 422);
        RequireHashShape(sourceSha256, "provenance.source_sha256", 422);
        if (!string.Equals(sourceId, documentId, StringComparison.Ordinal) || sourceRevision != revision)
            Semantic("provenance", "provenance 与 document_id/revision 不一致。", 409);
        var reviewOnly = false;
        if (envelope["review_only"] is not JsonValue reviewOnlyValue
            || !reviewOnlyValue.TryGetValue<bool>(out reviewOnly)
            || !reviewOnly)
            Malformed("review_only", "review_only 必须为 true。");

        var reviewStatus = RequiredString(envelope, "review_status", "envelope");
        if (reviewStatus is not ("approved_local" or "needs_review" or "rejected"))
            Malformed("review_status", "review_status 不在冻结枚举中。");
        if (producer == "persona_workbench" && reviewStatus != "approved_local")
            Semantic("review_status", "Persona handoff 必须来自 approved_local。", 422);

        var issuedAt = RequiredUtcTimestamp(envelope, "issued_at_utc");
        var expiresAt = RequiredUtcTimestamp(envelope, "expires_at_utc");
        if (expiresAt <= issuedAt) Semantic("expires_at_utc", "expires_at_utc 必须严格晚于 issued_at_utc。", 422);

        var payload = RequiredString(envelope, "payload", "envelope");
        if (payload.Length < 2 || HasDuplicateObjectKeys(payload))
            Malformed("payload", "payload 不是可接受的 JSON。");
        JsonNode parsedPayload;
        try
        {
            parsedPayload = JsonNode.Parse(payload) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            Malformed("payload", "payload 不是有效 JSON。");
            throw;
        }
        if (!string.Equals(SerializeSharedCanonical(parsedPayload), payload, StringComparison.Ordinal))
            Semantic("payload", "payload 不是冻结的 canonical JSON。", 422);
        if (!string.Equals(Hashing.Sha256Text(payload).ToLowerInvariant(), contentSha256, StringComparison.Ordinal))
            Semantic("content_sha256", "content_sha256 与 payload 不匹配。", 422);

        var requestFingerprint = RequiredString(envelope, "request_fingerprint", "envelope");
        RequireHashShape(requestFingerprint, "request_fingerprint");
        var expectedFingerprint = ComputeFingerprint(envelope);
        if (!string.Equals(requestFingerprint, expectedFingerprint, StringComparison.Ordinal))
            Semantic("request_fingerprint", "request_fingerprint 与 envelope 不匹配。", 422);

        var clock = now ?? DateTimeOffset.UtcNow;
        if (clock >= expiresAt)
            throw new WorkstationHandoffException("WB-HANDOFF-410", "handoff 已过期，请由 Persona Workbench 重新发起。", 410, "expires_at_utc");

        return new WorkstationHandoffEnvelope(
            schemaVersion,
            handoffId,
            workspaceId,
            documentId,
            revision,
            contentSha256,
            producer,
            provenance,
            reviewOnly,
            reviewStatus,
            envelope["issued_at_utc"]!.GetValue<string>(),
            envelope["expires_at_utc"]!.GetValue<string>(),
            payload,
            requestFingerprint);
    }

    public static string ComputeFingerprint(JsonObject envelope)
    {
        var values = new[]
        {
            ValueForFingerprint(envelope, "schema_version"),
            ValueForFingerprint(envelope, "handoff_id"),
            ValueForFingerprint(envelope, "workspace_id"),
            ValueForFingerprint(envelope, "document_id"),
            ValueForFingerprint(envelope, "revision"),
            ValueForFingerprint(envelope, "content_sha256"),
            ValueForFingerprint(envelope, "producer"),
            "true",
            ValueForFingerprint(envelope, "review_status"),
            ValueForFingerprint(envelope, "issued_at_utc"),
            ValueForFingerprint(envelope, "expires_at_utc"),
            ValueForFingerprint(envelope, "payload")
        };
        return Hashing.Sha256Text(string.Join('\n', values)).ToLowerInvariant();
    }

    private static string ValueForFingerprint(JsonObject envelope, string property)
    {
        if (property == "revision")
            return envelope[property]?.GetValue<int>().ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        return envelope[property]?.GetValue<string>() ?? string.Empty;
    }

    private static readonly JsonSerializerOptions SharedPayloadOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private static string SerializeSharedCanonical(JsonNode node)
        => CanonicalizeShared(node, string.Empty).ToJsonString(SharedPayloadOptions);

    private static JsonNode CanonicalizeShared(JsonNode node, string path)
    {
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var property in obj.OrderBy(item => item.Key, StringComparer.Ordinal))
                result[property.Key] = property.Value is null ? null : CanonicalizeShared(property.Value, path.Length == 0 ? property.Key : path + "." + property.Key);
            return result;
        }

        if (node is JsonArray array)
        {
            IEnumerable<JsonNode?> values = array.Select(item => item is null ? null : CanonicalizeShared(item, path));
            var stableKey = path switch
            {
                "facts" => "factId",
                "observations" => "selectorId",
                "rules" => "ruleId",
                _ => null
            };
            if (stableKey is not null)
                values = values.OrderBy(item => item?[stableKey]?.GetValue<string>() ?? string.Empty, StringComparer.Ordinal);
            var result = new JsonArray();
            foreach (var value in values) result.Add(value);
            return result;
        }

        return JsonNode.Parse(node.ToJsonString())!;
    }

    private static void RequireExactFields(JsonObject value, HashSet<string> allowed, string path)
    {
        foreach (var property in value)
            if (!allowed.Contains(property.Key))
                Malformed($"{path}.{property.Key}", "包含未冻结字段。");
    }

    private static string RequiredString(JsonObject value, string property, string path)
    {
        string? result = null;
        if (value[property] is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out result) || string.IsNullOrWhiteSpace(result))
            Malformed($"{path}.{property}", "字段必须为非空字符串。");
        return result!;
    }

    private static int RequiredInt(JsonObject value, string property, string path)
    {
        var result = 0;
        if (value[property] is not JsonValue jsonValue || !jsonValue.TryGetValue<int>(out result))
            Malformed($"{path}.{property}", "字段必须为整数。");
        return result;
    }

    private static DateTimeOffset RequiredUtcTimestamp(JsonObject value, string property)
    {
        var text = RequiredString(value, property, "envelope");
        var result = default(DateTimeOffset);
        if (!text.EndsWith('Z')
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result))
            Malformed(property, "时间戳必须为 RFC 3339 UTC（Z）。");
        return result.ToUniversalTime();
    }

    private static void RequireHashShape(string value, string path, int statusCode = 400)
    {
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character) || char.IsUpper(character)))
        {
            if (statusCode == 400) Malformed(path, "必须为小写 64 位十六进制 SHA-256。");
            Semantic(path, "必须为小写 64 位十六进制 SHA-256。", statusCode);
        }
    }

    private static bool HasDuplicateObjectKeys(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), true, default);
        var scopes = new Stack<HashSet<string>>();
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject) scopes.Push(new HashSet<string>(StringComparer.Ordinal));
                else if (reader.TokenType == JsonTokenType.EndObject) scopes.Pop();
                else if (reader.TokenType == JsonTokenType.PropertyName && scopes.Count > 0 && !scopes.Peek().Add(reader.GetString()!))
                    return true;
            }
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    [DoesNotReturn]
    private static void Malformed(string path, string message)
        => throw new WorkstationHandoffException("WB-HANDOFF-400", message, 400, path);

    [DoesNotReturn]
    private static void Semantic(string path, string message, int statusCode)
        => throw new WorkstationHandoffException(statusCode == 409 ? "WB-HANDOFF-409" : "WB-HANDOFF-422", message, statusCode, path);
}
