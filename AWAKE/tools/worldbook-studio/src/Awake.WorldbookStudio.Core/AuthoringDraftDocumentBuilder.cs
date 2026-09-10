using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal static class AuthoringDraftDocumentBuilder
{
    private static readonly HashSet<string> ExpressionLayers = new(StringComparer.Ordinal)
    {
        "unknown", "rumor", "summary", "detail", "secret"
    };

    public static JsonObject Build(
        JsonObject template,
        string draftId,
        string title,
        string? summary,
        string domain,
        IReadOnlyList<AuthoringDraftFact> facts,
        IReadOnlyList<AuthoringDraftExpression> expressions,
        RegistrySnapshot registries,
        AuthoringCandidateSet? candidateSet = null,
        AuthoringDraftMetadataReview? metadataReview = null)
    {
        var document = JsonNode.Parse(template.ToJsonString())!.AsObject();
        var normalizedSummary = summary?.Trim() ?? string.Empty;
        // 摘要为空时不写占位文字冒充作者内容；档案 schema 要求摘要非空，因此这里直接阻断建档。
        if (normalizedSummary.Length == 0)
            throw new InvalidOperationException("WB-AI-DRAFT-422: 档案摘要为空，不能写入档案；请先在作者表单中填写或生成摘要（不会用占位文字代替作者内容）。");
        SetLocalized(document, "title", title);
        SetLocalized(document, "summary", normalizedSummary);

        var factIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var assertions = new JsonArray();
        foreach (var fact in facts)
        {
            var assertionId = StableId("assertion", draftId, fact.Id);
            if (!factIds.TryAdd(fact.Id, assertionId))
                throw new InvalidOperationException($"WB-AI-DRAFT-422: 客观事实编号重复：{fact.Id}。" );
            var authoringKind = AuthoringProjection.MapFactKind(fact.Kind);
            assertions.Add(new JsonObject
            {
                ["id"] = assertionId,
                ["revision"] = 1,
                ["kind"] = authoringKind,
                ["text"] = new JsonObject { ["zh-CN"] = fact.Text.Trim() },
                ["author_created"] = AuthorCreated(
                    "AI 根据参考资料生成，作者已采纳，等待最终复核。",
                    candidateSet,
                    EvidenceFor(fact),
                    [fact],
                    []),
                ["expressions"] = new JsonArray()
            });
        }

        foreach (var expression in expressions)
        {
            if (expression.FactIds.Count != 1)
                throw new InvalidOperationException("WB-AI-DRAFT-422: 每条身份表达必须绑定一条客观事实；请拆分表达或重新生成。" );
            var targetFactId = expression.FactIds[0];
            if (!factIds.TryGetValue(targetFactId, out var assertionId))
                throw new InvalidOperationException($"WB-AI-DRAFT-422: 身份表达引用了不存在的客观事实：{targetFactId}。" );
            if (!ExpressionLayers.Contains(expression.Layer))
                throw new InvalidOperationException($"WB-AI-DRAFT-422: NPC 表达层级无效：{expression.Layer}。请重新生成或在作者表单中选择表达层级。" );
            var assertion = assertions.OfType<JsonObject>().First(item => string.Equals(item["id"]?.GetValue<string>(), assertionId, StringComparison.Ordinal));
            var expressionId = StableId("expr", draftId, expression.Id);
            var expressionObject = new JsonObject
            {
                ["id"] = expressionId,
                ["revision"] = 1,
                ["layer"] = expression.Layer,
                ["text"] = new JsonObject { ["zh-CN"] = expression.Text.Trim() },
                ["author_created"] = AuthorCreated(
                    "AI 根据已采纳事实生成，作者已采纳，等待最终复核。",
                    candidateSet,
                    expression.Evidence is null ? [] : [expression.Evidence],
                    [],
                    [expression]),
                ["grants"] = BuildGrants(expression, registries),
                ["denies"] = new JsonArray()
            };
            ((JsonArray)assertion["expressions"]!).Add(expressionObject);
        }

        document["domain"] = domain;
        document["status"] = "needs_review";
        document["revision"] = 1;
        document["assertions"] = assertions;
        document["author_created"] = AuthorCreated(
            "工作室 AI 草稿已写入，必须由作者继续复核后保存。",
            candidateSet,
            facts.SelectMany(EvidenceFor)
                .Concat(expressions.Select(expression => expression.Evidence))
                .Where(evidence => evidence is not null)
                .Cast<AuthoringDraftEvidence>()
                .ToArray(),
            facts,
            expressions,
            metadataReview);
        return document;
    }

    private static JsonArray BuildGrants(AuthoringDraftExpression expression, RegistrySnapshot registries)
    {
        if (expression.ProfileIds.Count == 0)
            throw new InvalidOperationException("WB-AI-DRAFT-422: NPC 表达还没有指定适用身份，请至少选择一个身份。" );
        var grants = new JsonArray();
        foreach (var profileId in expression.ProfileIds.Distinct(StringComparer.Ordinal))
        {
            if (!registries.Profiles.Contains(profileId))
                throw new InvalidOperationException($"WB-AI-DRAFT-422: 身份表达包含未登记身份：{profileId}。" );
            grants.Add(new JsonObject
            {
                ["profile_id"] = profileId,
                ["scope"] = "local",
                ["min_detail"] = ExpressionLayers.Contains(expression.Layer) && expression.Layer is not "unknown" ? expression.Layer : "summary"
            });
        }
        return grants;
    }

    private static JsonObject AuthorCreated(
        string reason,
        AuthoringCandidateSet? candidateSet,
        IReadOnlyList<AuthoringDraftEvidence> evidence,
        IReadOnlyList<AuthoringDraftFact> facts,
        IReadOnlyList<AuthoringDraftExpression> expressions,
        AuthoringDraftMetadataReview? metadataReview = null)
    {
        var result = new JsonObject
        {
            ["author_id"] = "author.developer",
            ["reason"] = new JsonObject { ["zh-CN"] = reason },
            ["review_status"] = "draft"
        };
        if (candidateSet is not null)
        {
            var candidate = candidateSet.Candidates.FirstOrDefault();
            result["provenance"] = new JsonObject
            {
                ["source_content_hash"] = candidateSet.SourceContentHash,
                ["packet_hash"] = candidateSet.PacketHash,
                ["provider_fingerprint"] = candidateSet.ProviderFingerprint,
                ["candidate_fingerprint"] = candidate?.Fingerprint,
                ["fact_annotations"] = new JsonArray(facts.Select(fact => new JsonObject
                {
                    ["id"] = fact.Id,
                    ["kind"] = fact.Kind,
                    ["certainty"] = fact.Certainty,
                    ["inferred"] = fact.Inferred,
                    ["review_status"] = fact.ReviewStatus
                }).ToArray()!),
                ["expression_annotations"] = new JsonArray(expressions.Select(expression => new JsonObject
                {
                    ["id"] = expression.Id,
                    ["perspective"] = expression.Perspective,
                    ["layer"] = expression.Layer,
                    ["fact_ids"] = new JsonArray(expression.FactIds.Select(value => JsonValue.Create(value)).ToArray()!),
                    ["inferred"] = expression.Inferred,
                    ["review_status"] = expression.ReviewStatus
                }).ToArray()!),
                ["propositions"] = new JsonArray((candidate?.Propositions ?? []).Select(AuthoringDraftRequestFactory.SerializeProposition).ToArray()!),
                ["claims"] = new JsonArray((candidate?.Claims ?? []).Select(AuthoringDraftRequestFactory.SerializeClaim).ToArray()!),
                ["target_spans"] = new JsonArray((candidate?.TargetSpans ?? []).Select(AuthoringDraftRequestFactory.SerializeTargetSpan).ToArray()!),
                ["unresolved"] = new JsonArray((candidate?.Unresolved ?? []).Select(AuthoringDraftRequestFactory.SerializeUnresolved).ToArray()!),
                ["coverage"] = candidate?.Coverage?.DeepClone(),
                ["evidence"] = new JsonArray(evidence
                    .Where(item => item is not null)
                    .GroupBy(item => $"{item.ReferenceId}\u001f{item.Locator}\u001f{item.QuoteHash}", StringComparer.Ordinal)
                    .Select(group => group.First())
                    .Select(item =>
                    {
                        var evidenceObject = new JsonObject
                        {
                            ["reference_id"] = item.ReferenceId,
                            ["locator"] = item.Locator,
                            ["quote"] = item.Quote,
                            ["quote_hash"] = item.QuoteHash
                        };
                        if (item.LocatorObject is not null)
                            evidenceObject["locator_object"] = item.LocatorObject.DeepClone();
                        return evidenceObject;
                    })
                    .ToArray()!)
            };
            if (metadataReview is not null)
            {
                result["provenance"]!["metadata_review"] = new JsonObject
                {
                    ["status"] = metadataReview.Status,
                    ["revalidation_status"] = metadataReview.RevalidationStatus,
                    ["modified_fields"] = new JsonArray(metadataReview.ModifiedFields.Select(value => JsonValue.Create(value)).ToArray()!)
                };
            }
        }
        return result;
    }

    private static IReadOnlyList<AuthoringDraftEvidence> EvidenceFor(AuthoringDraftFact fact)
        => fact.EvidenceGroup is { Count: > 0 }
            ? fact.EvidenceGroup
            : fact.Evidence is null ? [] : [fact.Evidence];

    private static void SetLocalized(JsonObject document, string key, string value)
    {
        var localized = document[key] is JsonObject current
            ? JsonNode.Parse(current.ToJsonString())!.AsObject()
            : new JsonObject();
        localized["zh-CN"] = value;
        document[key] = localized;
    }

    private static string StableId(string kind, string draftId, string sourceId)
        => $"{kind}.ai.{Hashing.Sha256Text($"{kind}|{draftId}|{sourceId}").ToLowerInvariant()[..24]}";
}
