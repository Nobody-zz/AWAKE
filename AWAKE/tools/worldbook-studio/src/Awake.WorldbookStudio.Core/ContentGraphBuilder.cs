using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class ContentGraphBuilder
{
    internal static JsonObject Build(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents, Dictionary<(string Id, string Version), JsonObject> sources, RegistrySnapshot registries, ValidationReport report, out bool hasAdultClosure)
    {
        var nodes = new JsonArray();
        var edges = new JsonArray();
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        hasAdultClosure = false;
        void Node(string id, string kind, string tier, string origin)
        {
            if (!nodeIds.Add(id)) return;
            nodes.Add(new JsonObject { ["node_id"] = id, ["node_kind"] = kind, ["content_tier"] = tier, ["origin_pointer"] = origin });
        }
        void Edge(string type, string from, string to)
        {
            if (!edgeIds.Add($"{type}|{from}|{to}")) return;
            edges.Add(new JsonObject { ["edge_type"] = type, ["from"] = from, ["to"] = to });
        }

        Node("registry.profile", "profile_registry", "base", "profile-registry.v1.json");
        Node("registry.referral", "referral_registry", "base", "referral-registry.v1.json");
        Node("index.worldbook", "index", "base", "compiled/index.json");
        foreach (var item in documents)
        {
            var id = item.Document["id"]?.GetValue<string>() ?? item.Path;
            var tier = item.Document["content_tier"]?.GetValue<string>() ?? "unknown";
            if (tier == "adult_optional" || tier == "unknown") hasAdultClosure = true;
            if (tier == "unknown") report.Error("WB-TIER-001", "文档 content_tier 无法确定。", item.Path);
            Node(id, "document", tier, item.Path);
            Edge("index_entry", "index.worldbook", id);
            foreach (var sourceRef in item.Document["sources"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var sourceId = sourceRef["source_id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(sourceId)) continue;
                if (!nodes.Any(x => x?["node_id"]?.GetValue<string>() == sourceId)) Node(sourceId, "source", tier, $"{item.Path}#/sources");
                Edge("references_source", id, sourceId);
            }
            foreach (var assertion in item.Document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var assertionId = assertion["id"]?.GetValue<string>() ?? "assertion.unknown";
                Node(assertionId, "assertion", tier, $"{item.Path}#/assertions");
                Edge("contains", id, assertionId);
                foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
                {
                    var expressionId = expression["id"]?.GetValue<string>() ?? "expr.unknown";
                    var expressionTier = expression["content_tier"]?.GetValue<string>() ?? tier;
                    if (expressionTier == "adult_optional" || expressionTier == "unknown") hasAdultClosure = true;
                    Node(expressionId, "expression", expressionTier, $"{item.Path}#/assertions/{assertionId}/expressions");
                    Edge("contains", assertionId, expressionId);
                    foreach (var sourceRef in expression["sources"]?.AsArray().OfType<JsonObject>() ?? [])
                    {
                        var sourceId = sourceRef["source_id"]?.GetValue<string>();
                        if (sourceId is not null) Edge("references_source", expressionId, sourceId);
                    }
                    foreach (var referral in expression["fallback_referral_ids"]?.AsArray().Select(x => x?.GetValue<string>()).Where(x => x is not null) ?? [])
                    {
                        Node(referral!, "referral", tier, $"{item.Path}#/assertions/{assertionId}/expressions");
                        Edge("fallback", expressionId, referral!);
                    }
                }
            }
            foreach (var redirect in item.Document["redirects"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var redirectId = redirect["redirect_id"]?.GetValue<string>();
                var from = redirect["from"]?.GetValue<string>();
                var to = redirect["to"]?.GetValue<string>();
                if (redirectId is null || from is null || to is null) continue;
                Node(redirectId, "redirect", tier, $"{item.Path}#/redirects");
                Edge("redirect", redirectId, to);
                Edge("redirect", from, redirectId);
            }
        }
        return new JsonObject { ["graph_version"] = "awake.worldbook.content-graph.v1", ["nodes"] = nodes, ["edges"] = edges };
    }
}
