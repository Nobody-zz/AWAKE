using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class AuthoringTemplateFactory
{
    internal static JsonObject Create(string documentId, string title, string domain, string contentTier, string authorId, string slug, RegistrySnapshot registries, string? subdomain = null, IReadOnlyList<string>? relatedDomains = null, string? eraKey = null, string? eraCertainty = null, IReadOnlyList<string>? entityIds = null)
    {
        JsonObject AuthorCreated(string reason) => new()
        {
            ["author_id"] = authorId,
            ["reason"] = new JsonObject { ["zh-CN"] = reason },
            ["review_status"] = "draft"
        };

        var expressionId = $"expr.{slug}";
        var assertionId = $"assertion.{slug}";
        var document = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring.v1",
            ["revision"] = 1,
            ["id"] = documentId,
            ["title"] = new JsonObject { ["zh-CN"] = title },
            ["status"] = "needs_review",
            ["domain"] = domain,
            ["universe"] = "awake_current",
            ["era"] = new JsonObject
            {
                ["key"] = string.IsNullOrWhiteSpace(eraKey) ? "current" : eraKey.Trim(),
                ["certainty"] = string.IsNullOrWhiteSpace(eraCertainty) ? "unknown" : eraCertainty.Trim()
            },
            ["content_tier"] = contentTier,
            ["summary"] = new JsonObject { ["zh-CN"] = "待补充：说明这份档案记录的世界知识范围。" },
            ["registry_bindings"] = new JsonObject
            {
                ["profile_registry_version"] = registries.ProfileVersion,
                ["profile_registry_hash"] = registries.ProfileHash,
                ["referral_registry_version"] = registries.ReferralVersion,
                ["referral_registry_hash"] = registries.ReferralHash
            },
            ["author_created"] = AuthorCreated("工作室新建草稿，等待作者补充并复核。"),
            ["authority"] = new JsonObject { ["owner"] = "awake_canon", ["conflict_policy"] = "canon_wins" },
            ["assertions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = assertionId,
                    ["revision"] = 1,
                    ["kind"] = "fact",
                    ["text"] = new JsonObject { ["zh-CN"] = "待补充：这条档案要记录的客观事实。" },
                    ["author_created"] = AuthorCreated("作者草拟的档案事实。"),
                    ["expressions"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = expressionId,
                            ["revision"] = 1,
                            ["layer"] = "summary",
                            ["text"] = new JsonObject { ["zh-CN"] = "待补充：NPC 能够说出的简要表达。" },
                            ["author_created"] = AuthorCreated("作者草拟的 NPC 表达。"),
                            ["grants"] = new JsonArray
                            {
                                new JsonObject { ["profile_id"] = "profile.commoner", ["scope"] = "local", ["min_detail"] = "summary" }
                            },
                            ["denies"] = new JsonArray()
                        }
                    }
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(subdomain)) document["subdomain"] = subdomain;
        if (relatedDomains is { Count: > 0 })
            document["related_domains"] = new JsonArray(relatedDomains.Select(value => JsonValue.Create(value) as JsonNode).ToArray()!);
        if (entityIds is { Count: > 0 })
        {
            var normalizedEntityIds = entityIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (normalizedEntityIds.Length > 0)
                document["entity_ids"] = new JsonArray(normalizedEntityIds.Select(value => JsonValue.Create(value) as JsonNode).ToArray()!);
        }
        return document;
    }
}
