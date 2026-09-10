using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class PreviewProjectionBuilder
{
    internal static (JsonArray NpcResults, JsonArray DiagnosticResults) Build(
        IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents,
        RegistrySnapshot registries,
        string profileId,
        IdentitySnapshot identity)
    {
        var chain = RegistryService.GetProfileChain(registries, profileId).ToHashSet(StringComparer.Ordinal);
        var npcResults = new JsonArray();
        var diagnosticResults = new JsonArray();
        foreach (var item in documents)
        {
            foreach (var assertion in item.Document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
                {
                    var knowledgeId = expression["id"]?.GetValue<string>() ?? assertion["id"]?.GetValue<string>() ?? item.Document["id"]!.GetValue<string>();
                    var layer = expression["layer"]?.GetValue<string>() ?? "unknown";
                    var matchingDenies = MatchingRules(expression["denies"]?.AsArray(), layer).ToList();
                    var matchingGrants = MatchingRules(expression["grants"]?.AsArray(), layer).ToList();
                    var denied = matchingDenies.Count > 0;
                    var granted = !denied && matchingGrants.Count > 0;
                    var status = denied ? "redacted" : granted ? (layer == "rumor" ? "rumor" : "visible") : "unknown";
                    var referrals = !denied && !granted
                        ? expression["fallback_referral_ids"]?.AsArray().Select(node => node?.GetValue<string>()).Where(referral => referral is not null && registries.PubliclyAskableReferrals.Contains(referral)).Select(referral => referral!).Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>()
                        : Array.Empty<string>();
                    var npc = new JsonObject { ["knowledge_id"] = knowledgeId, ["status"] = status };
                    if (granted)
                    {
                        npc["text"] = expression["text"] is JsonObject text ? CanonicalJson.Canonicalize(text) : new JsonObject();
                        npc["layer"] = layer;
                    }
                    else if (layer is "rumor" or "summary" or "detail" or "secret") npc["layer"] = layer;
                    if (referrals.Length > 0) npc["fallback_referral_ids"] = new JsonArray(referrals.Select(referral => JsonValue.Create(referral)).ToArray());
                    npcResults.Add(npc);

                    var sourceIds = SourceIds(expression);
                    var diagnostic = new JsonObject
                    {
                        ["document_id"] = item.Document["id"]!.GetValue<string>(),
                        ["knowledge_id"] = knowledgeId,
                        ["status"] = status,
                        ["rule_ids"] = new JsonArray((matchingDenies.Select((_, index) => $"{knowledgeId}.deny.{index}").Concat(matchingGrants.Select((_, index) => $"{knowledgeId}.grant.{index}")).Distinct()).Select(ruleId => JsonValue.Create(ruleId)).ToArray()),
                        ["source_ids"] = new JsonArray(sourceIds.Select(sourceId => JsonValue.Create(sourceId)).ToArray()),
                        ["final_layer"] = granted ? layer : "unknown",
                        ["explanation_chain"] = new JsonArray((denied ? new[] { "deny 优先" } : granted ? new[] { "命中显式 grant" } : new[] { "无匹配权限" }).Select(explanation => JsonValue.Create(explanation)).ToArray()),
                        ["rejection_code"] = denied ? "WB-PERMISSION-002" : null
                    };
                    diagnosticResults.Add(diagnostic);
                }
            }
        }
        return (npcResults, diagnosticResults);

        IEnumerable<JsonObject> MatchingRules(JsonArray? rules, string currentLayer)
        {
            foreach (var rule in rules?.OfType<JsonObject>() ?? [])
            {
                var ruleProfileId = rule["profile_id"]?.GetValue<string>();
                if (ruleProfileId is null || !chain.Contains(ruleProfileId)) continue;
                if (!MatchesDimension(rule["culture_ids"], identity.CultureId) || !MatchesDimension(rule["kingdom_ids"], identity.KingdomId) || !MatchesDimension(rule["settlement_ids"], identity.SettlementId)) continue;
                int? minAge = rule["min_age"] is null ? null : (int)rule["min_age"]!.GetValue<long>();
                int? minSteward = rule["min_steward"] is null ? null : (int)rule["min_steward"]!.GetValue<long>();
                if (minAge is not null && (identity.Age is null || identity.Age < minAge)) continue;
                if (minSteward is not null && (identity.Steward is null || identity.Steward < minSteward)) continue;
                var minimumLayer = rule["min_detail"]?.GetValue<string>() ?? "rumor";
                if (LayerRank(currentLayer) < LayerRank(minimumLayer)) continue;
                yield return rule;
            }
        }

        static bool MatchesDimension(JsonNode? values, string? actual)
        {
            if (values is not JsonArray array || array.Count == 0) return true;
            return actual is not null && array.Any(value => string.Equals(value?.GetValue<string>(), actual, StringComparison.Ordinal));
        }

        static int LayerRank(string value) => value switch { "unknown" => 0, "rumor" => 1, "summary" => 2, "detail" => 3, "secret" => 4, _ => 0 };

        static string[] SourceIds(JsonObject expression)
            => expression["sources"]?.AsArray().Select(node => node?["source_id"]?.GetValue<string>()).Where(sourceId => sourceId is not null).Select(sourceId => sourceId!).Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
    }
}
