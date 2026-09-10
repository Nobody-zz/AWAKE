using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Awake.WorldbookStudio.Core;

public enum AssistanceAnalysisKind
{
    Permissions,
    Consistency,
    Metadata,
    Prose
}

public enum AssistanceFocusKind
{
    General,
    Completion,
    FactCorrection,
    KnowledgePermissions,
    Hierarchy,
    ExpressionLevel,
    Metadata,
    WorldStyle,
    NpcVoice,
    DomainStyle
}

public sealed record AssistanceRequest(
    string SchemaVersion,
    string ProviderId,
    AssistanceAnalysisKind Analysis,
    string DocumentPath,
    string DocumentId,
    string SourceDocumentHash,
    int SavedRevision,
    string Nonce,
    JsonObject DocumentProjection,
    JsonObject RegistrySummary,
    string RequestHash,
    AssistanceFocusKind Focus = AssistanceFocusKind.General);

public sealed record KnowledgePatch(string SchemaVersion, IReadOnlyList<KnowledgePatchOperation> Operations);

public sealed record KnowledgePatchOperation(string Op, string Path, JsonNode? Value = null);

public static class AssistanceAnalysisNames
{
    public static string ToWire(AssistanceAnalysisKind value) => value switch
    {
        AssistanceAnalysisKind.Permissions => "permissions",
        AssistanceAnalysisKind.Consistency => "consistency",
        AssistanceAnalysisKind.Metadata => "metadata",
        AssistanceAnalysisKind.Prose => "prose",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToFocusWire(AssistanceFocusKind value) => value switch
    {
        AssistanceFocusKind.General => "general",
        AssistanceFocusKind.Completion => "completion",
        AssistanceFocusKind.FactCorrection => "fact_correction",
        AssistanceFocusKind.KnowledgePermissions => "knowledge_permissions",
        AssistanceFocusKind.Hierarchy => "hierarchy",
        AssistanceFocusKind.ExpressionLevel => "expression_level",
        AssistanceFocusKind.Metadata => "metadata",
        AssistanceFocusKind.WorldStyle => "world_style",
        AssistanceFocusKind.NpcVoice => "npc_voice",
        AssistanceFocusKind.DomainStyle => "domain_style",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

public static class AssistanceFocusMapping
{
    public static AssistanceAnalysisKind ToAnalysis(AssistanceFocusKind focus) => focus switch
    {
        AssistanceFocusKind.KnowledgePermissions or AssistanceFocusKind.Hierarchy or AssistanceFocusKind.ExpressionLevel
            => AssistanceAnalysisKind.Permissions,
        AssistanceFocusKind.Metadata => AssistanceAnalysisKind.Metadata,
        AssistanceFocusKind.WorldStyle or AssistanceFocusKind.NpcVoice or AssistanceFocusKind.DomainStyle
            => AssistanceAnalysisKind.Prose,
        _ => AssistanceAnalysisKind.Consistency
    };
}

public sealed class AssistanceService
{
    private static readonly string[] TopLevelFields =
    [
        "schema_version", "revision", "id", "title", "status", "domain", "subdomain", "related_domains", "universe", "era",
        "content_tier", "summary", "entity_ids", "aliases"
    ];

    private readonly WorkspaceService _workspace;

    public AssistanceService(WorkspaceService workspace)
    {
        _workspace = workspace;
    }

    public AssistanceRequest CreateRequest(string documentPath, string providerId, AssistanceAnalysisKind analysis, AssistanceFocusKind focus = AssistanceFocusKind.General)
    {
        var document = _workspace.ReadAuthoring(documentPath);
        if (!document.Report.Valid)
            throw new InvalidOperationException("WB-AI-REQUEST-422: 当前档案未通过结构校验，不能发送给 AI。");
        if (string.IsNullOrWhiteSpace(providerId))
            throw new InvalidOperationException("WB-AI-REQUEST-400: provider 不能为空。");

        var projection = ProjectDocument(document.Document);
        var registry = GetRegistrySummary();
        var requestBody = new JsonObject
        {
            ["schema_version"] = "assistance.request.v1",
            ["provider_id"] = providerId,
            ["analysis"] = AssistanceAnalysisNames.ToWire(analysis),
            ["focus"] = AssistanceAnalysisNames.ToFocusWire(focus),
            ["document"] = projection,
            ["registry_summary"] = registry
        };

        return new AssistanceRequest(
            "assistance.request.v1",
            providerId,
            analysis,
            document.Path,
            document.Document["id"]?.GetValue<string>() ?? "unknown",
            document.Report.InputHash ?? Hashing.FileSha256(document.AbsolutePath),
            document.Document["revision"] is null ? 1 : checked((int)document.Document["revision"]!.GetValue<long>()),
            NewNonce(),
            projection,
            registry,
            CanonicalJson.Hash(requestBody),
            focus);
    }

    public JsonObject GetRegistrySummary()
    {
        var result = new JsonObject();
        foreach (var name in new[] { "profile-registry.v1.json", "referral-registry.v1.json" })
        {
            var path = Path.Combine(_workspace.SchemaRoot, name);
            if (!File.Exists(path)) continue;
            var source = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
            if (source is null) continue;
            var key = name.StartsWith("profile", StringComparison.Ordinal) ? "profiles" : "referrals";
            var ids = new JsonArray();
            var records = source[key]?.AsArray() ?? [];
            foreach (var item in records.OfType<JsonObject>())
            {
                var id = item["id"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }
            result[key] = ids;
        }
        return result;
    }

    private static JsonObject ProjectDocument(JsonObject document)
    {
        var result = new JsonObject();
        foreach (var field in TopLevelFields)
        {
            if (document[field] is not null) result[field] = Clone(document[field]!);
        }

        var assertions = new JsonArray();
        foreach (var assertion in document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var projectedAssertion = new JsonObject();
            foreach (var field in new[] { "id", "revision", "kind", "text" })
                if (assertion[field] is not null) projectedAssertion[field] = Clone(assertion[field]!);
            var expressions = new JsonArray();
            foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var projectedExpression = new JsonObject();
                foreach (var field in new[] { "id", "revision", "layer", "text", "grants", "denies", "fallback_referral_ids" })
                    if (expression[field] is not null) projectedExpression[field] = Clone(expression[field]!);
                expressions.Add(projectedExpression);
            }
            projectedAssertion["expressions"] = expressions;
            assertions.Add(projectedAssertion);
        }
        result["assertions"] = assertions;
        return result;
    }

    private static JsonNode Clone(JsonNode node) => JsonNode.Parse(node.ToJsonString())!;

    private static string NewNonce()
        => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

public static class JsonPatchEngine
{
    private static readonly Regex Locale = new("^[a-z]{2}(?:-[A-Z]{2})?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static JsonNode Apply(JsonNode source, KnowledgePatch patch)
    {
        if (!string.Equals(patch.SchemaVersion, "knowledge-patch.v1", StringComparison.Ordinal))
            throw new InvalidOperationException("WB-AI-PATCH-422: patch schema_version 无效。");

        var result = JsonNode.Parse(source.ToJsonString())!;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in patch.Operations)
        {
            if (!seen.Add($"{operation.Op}:{operation.Path}"))
                throw new InvalidOperationException("WB-AI-PATCH-422: patch 存在重复路径操作。");
            if (operation.Op is not ("add" or "replace" or "remove"))
                throw new InvalidOperationException("WB-AI-PATCH-422: 只允许 add、replace、remove。");
            var segments = ParsePath(operation.Path);
            if (!IsAllowedPath(segments))
                throw new InvalidOperationException("WB-AI-PATCH-403: patch 路径不在可应用的本地化文本白名单内。");
            ApplyOne(result, operation, segments);
        }
        return result;
    }

    private static string[] ParsePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("/", StringComparison.Ordinal)) return [];
        return path[1..].Split('/').Select(x => x.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)).ToArray();
    }

    private static bool IsAllowedPath(string[] segments)
    {
        if (segments.Length == 2 && segments[0] is "title" or "summary" && Locale.IsMatch(segments[1])) return true;
        if (segments.Length == 4 && segments[0] == "assertions" && int.TryParse(segments[1], out _) && segments[2] == "text" && Locale.IsMatch(segments[3])) return true;
        if (segments.Length == 6 && segments[0] == "assertions" && int.TryParse(segments[1], out _) && segments[2] == "expressions" && int.TryParse(segments[3], out _) && segments[4] == "text" && Locale.IsMatch(segments[5])) return true;
        return false;
    }

    private static void ApplyOne(JsonNode root, KnowledgePatchOperation operation, string[] segments)
    {
        var parent = root;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            parent = parent switch
            {
                JsonObject obj when obj[segments[index]] is not null => obj[segments[index]]!,
                JsonArray array when int.TryParse(segments[index], out var arrayIndex) && arrayIndex >= 0 && arrayIndex < array.Count => array[arrayIndex]!,
                _ => throw new InvalidOperationException("WB-AI-PATCH-422: patch 路径不存在。")
            };
        }

        var leaf = segments[^1];
        if (parent is not JsonObject parentObject)
            throw new InvalidOperationException("WB-AI-PATCH-422: patch 只能修改对象属性。");
        if (operation.Op == "remove")
        {
            if (!parentObject.Remove(leaf)) throw new InvalidOperationException("WB-AI-PATCH-422: remove 目标不存在。");
            return;
        }
        if (operation.Value is null) throw new InvalidOperationException("WB-AI-PATCH-422: add/replace 必须提供 value。");
        if (operation.Op == "replace" && parentObject[leaf] is null) throw new InvalidOperationException("WB-AI-PATCH-422: replace 目标不存在。");
        parentObject[leaf] = JsonNode.Parse(operation.Value.ToJsonString())!;
    }
}
