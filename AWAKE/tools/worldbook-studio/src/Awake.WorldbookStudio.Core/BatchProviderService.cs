using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchProviderRunResult(BatchFactCommitResult Commit, string? CacheKey, JsonObject? CacheMaterialization);

internal sealed class BatchProviderService
{
    private const string PromptRevision = AuthoringDraftRequestFactory.PromptRevision;
    private const string OutputSchemaRevision = "awake.worldbook.batch-fact-result.v2";
    private const string CacheNormalizationRevision = "normalization.v2";
    private const string EmptyFactSetHash = "4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";

    private readonly ProviderConfiguration _configuration;
    private readonly BatchCacheRepository _cache;
    private readonly BatchFactReviewRepository _facts;

    public BatchProviderService(
        ProviderConfiguration configuration,
        BatchCacheRepository cache,
        BatchFactReviewRepository facts)
    {
        _configuration = configuration;
        _cache = cache;
        _facts = facts;
    }

    public string ResolveFingerprint(string providerId, JsonObject modelParameters)
    {
        var status = _configuration.GetStatus(providerId);
        if (status.State != "configured") throw new InvalidOperationException($"WB-BATCH-PROVIDER-422: {status.Label}。");
        var endpoint = providerId == "cloud" ? _configuration.CloudBaseUrl : _configuration.LocalWorkerUrl;
        var model = providerId == "cloud" ? _configuration.CloudModel : modelParameters["model"]?.GetValue<string>();
        return Hashing.Sha256Text(CanonicalJson.Serialize(new JsonObject
        {
            ["provider_id"] = providerId,
            ["endpoint"] = endpoint ?? string.Empty,
            ["model"] = model ?? string.Empty,
            ["model_parameters"] = CanonicalJson.Canonicalize(modelParameters)
        }));
    }

    public async Task<BatchProviderRunResult> ExtractFactsAsync(
        string batchId,
        string itemId,
        int expectedItemRevision,
        string providerId,
        JsonObject modelParameters,
        string providerFingerprint,
        JsonObject item,
        JsonObject sourceUnit,
        string registrySnapshotHash,
        CancellationToken cancellationToken = default,
        DateTimeOffset? now = null)
    {
        var cacheKeyPayload = BuildCacheKeyPayload(item, providerId, providerFingerprint, modelParameters, registrySnapshotHash);
        var cacheKey = _cache.ComputeKey(cacheKeyPayload);
        var authoringRequest = AuthoringDraftRequestFactory.Create(
            providerId,
            "batch." + itemId,
            AuthoringDraftStage.Facts,
            sourceUnit["unit_id"]?.GetValue<string>() ?? itemId,
            "reference_material",
            sourceUnit["text"]?.GetValue<string>() ?? string.Empty,
            [],
            null,
            [],
            new JsonObject());
        var snapshot = AuthoringLifecycleFactory.CreateSnapshot(
            item["source_snapshot_id"]?.GetValue<string>() ?? authoringRequest.DraftId,
            authoringRequest.SourceName,
            authoringRequest.SourceNature,
            authoringRequest.SourceText);
        var packet = AuthoringLifecycleFactory.CreatePacket(
            snapshot,
            authoringRequest.DraftId,
            providerId,
            providerFingerprint,
            ["facts"],
            [],
            modelParameters,
            registrySnapshotHash);
        BatchCacheHit? cacheHit = _cache.TryRead(cacheKey);
        IReadOnlyList<BatchFactInput> facts = [];
        var attemptKind = "provider";
        string? materializationId = null;
        if (cacheHit is not null)
        {
            try
            {
                facts = ParseCachedFacts(cacheHit.Payload, sourceUnit);
                attemptKind = "cache_hit";
                materializationId = _cache.MaterializationId(cacheKey, batchId, itemId, "attempt.pending");
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("WB-BATCH-CACHE-", StringComparison.Ordinal))
            {
                cacheHit = null;
                facts = [];
            }
        }
        if (cacheHit is null)
        {
            var provider = AuthoringDraftProviderFactory.Create(providerId, _configuration.WithModelParameters(modelParameters));
            var result = await provider.GenerateAsync(authoringRequest, cancellationToken).ConfigureAwait(false);
            facts = TranslateFacts(result.Facts, sourceUnit);
            var payload = new JsonObject { ["facts"] = SerializeFacts(facts, sourceUnit) };
            _cache.Publish(cacheKeyPayload, "facts", "awake.worldbook.batch-cache-facts-payload.v2", payload, now);
        }

        var attemptId = item["lease"]?["attempt_id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("WB-BATCH-CLAIM-409: 当前项目缺少执行租约。");
        if (cacheHit is not null) materializationId = _cache.MaterializationId(cacheKey, batchId, itemId, attemptId);
        var commit = _facts.CommitFacts(new BatchFactCommitRequest(
            batchId,
            itemId,
            expectedItemRevision,
            attemptId,
            providerId,
            providerFingerprint,
            facts,
            AttemptKind: attemptKind,
            CacheMaterializationRef: materializationId,
            PacketHash: packet.PacketHash,
            Packet: AuthoringLifecycleFactory.ProjectPacket(packet)),
            now);
        JsonObject? materialization = null;
        if (cacheHit is not null && materializationId is not null)
        {
            var evidenceIds = commit.Evidence.Select(x => x["evidence_id"]!.GetValue<string>()).ToArray();
            materialization = _cache.WriteMaterialization(batchId, itemId, materializationId, cacheHit, attemptId, commit.Result["result_id"]!.GetValue<string>(), evidenceIds, "facts", now);
        }
        return new BatchProviderRunResult(commit, cacheHit?.CacheKey, materialization);
    }

    private static JsonObject BuildCacheKeyPayload(JsonObject item, string providerId, string providerFingerprint, JsonObject modelParameters, string registrySnapshotHash)
        => new()
        {
            ["normalized_content_hash"] = item["normalized_content_hash"]!.GetValue<string>(),
            ["unit_hash"] = item["unit_hash"]!.GetValue<string>(),
            ["normalization_revision"] = CacheNormalizationRevision,
            ["splitter_revision"] = "splitter.v1",
            ["stage"] = "facts",
            ["prompt_revision"] = PromptRevision,
            ["provider_id"] = providerId,
            ["provider_fingerprint"] = providerFingerprint,
            ["model_parameters"] = CanonicalJson.Canonicalize(modelParameters),
            ["output_schema_revision"] = OutputSchemaRevision,
            ["registry_snapshot_hash"] = registrySnapshotHash,
            ["accepted_fact_set_hash"] = EmptyFactSetHash,
            ["metadata_selection_hash"] = EmptyFactSetHash,
            ["identity_selection_hash"] = EmptyFactSetHash
        };

    private static IReadOnlyList<BatchFactInput> TranslateFacts(IReadOnlyList<AuthoringDraftFact> sourceFacts, JsonObject sourceUnit)
    {
        var result = new List<BatchFactInput>();
        foreach (var sourceFact in sourceFacts)
        {
            var kind = sourceFact.Kind.Trim().ToLowerInvariant() switch
            {
                "definition" => "definition",
                "relation" => "relation",
                "chronology" => "chronology",
                "geography" => "geography",
                _ => "fact"
            };
            var certainty = sourceFact.Certainty.Trim().ToLowerInvariant() switch
            {
                "confirmed" or "certain" => "confirmed",
                "probable" or "likely" => "probable",
                "contested" => "contested",
                _ => "uncertain"
            };
            var risk = sourceFact.Inferred || certainty is "uncertain" or "contested" ? "yellow" : "green";
            var notes = string.Empty;
            EvidenceResolution resolution;
            if (sourceFact.Evidence is null)
            {
                resolution = new EvidenceResolution(
                    CreateFallbackEvidence(sourceUnit),
                    "AI 未提供可验证的原文片段，已保留资料单元作为核对范围，请人工确认。");
            }
            else
            {
                resolution = CreateEvidence(sourceFact.Evidence, sourceFact.Text, sourceUnit);
            }

            if (resolution.RequiresManualReview)
            {
                risk = "yellow";
                notes = resolution.Note;
            }

            result.Add(new BatchFactInput(sourceFact.Text, kind, certainty, risk, [resolution.Evidence], notes));
        }
        return result;
    }

    private sealed record EvidenceResolution(BatchFactEvidenceInput Evidence, string? Note)
    {
        public bool RequiresManualReview => !string.IsNullOrWhiteSpace(Note);
    }

    private static EvidenceResolution CreateEvidence(AuthoringDraftEvidence sourceEvidence, string factText, JsonObject sourceUnit)
    {
        var sourceText = NormalizeText(sourceUnit["text"]?.GetValue<string>() ?? string.Empty);
        var quote = NormalizeText(sourceEvidence.Quote);
        if (quote.Length > 0 && SourceEvidenceMatcher.TryFind(sourceText, quote, out var quoteMatch))
        {
            return new EvidenceResolution(BuildEvidenceLocator(sourceUnit, quoteMatch), null);
        }

        if (SourceEvidenceMatcher.TryFind(sourceText, factText, out var factMatch))
        {
            return new EvidenceResolution(
                BuildEvidenceLocator(sourceUnit, factMatch),
                "AI 提供的原文片段未直接定位，已按事实文本找到资料原文，请人工核对。");
        }

        return new EvidenceResolution(
            CreateFallbackEvidence(sourceUnit),
            "AI 返回的原文片段无法定位到当前资料，已保留为待人工核对候选。");
    }

    private static BatchFactEvidenceInput CreateFallbackEvidence(JsonObject sourceUnit)
    {
        var sourceText = NormalizeText(sourceUnit["text"]?.GetValue<string>() ?? string.Empty);
        if (sourceText.Length == 0) throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: 当前资料单元为空，无法建立人工核对范围。");
        var locator = new JsonObject
        {
            ["source_unit_id"] = sourceUnit["unit_id"]!.GetValue<string>(),
            ["start_utf16"] = sourceUnit["start_utf16"]!.GetValue<int>(),
            ["end_utf16"] = sourceUnit["end_utf16"]!.GetValue<int>(),
            ["line_start"] = sourceUnit["line_start"]!.GetValue<int>(),
            ["line_end"] = sourceUnit["line_end"]!.GetValue<int>(),
            ["heading_path"] = sourceUnit["heading_path"]?.DeepClone() as JsonArray ?? new JsonArray()
        };
        return new BatchFactEvidenceInput(locator, sourceText);
    }

    private static BatchFactEvidenceInput BuildEvidenceLocator(JsonObject sourceUnit, SourceEvidenceMatch match)
    {
        var sourceText = NormalizeText(sourceUnit["text"]?.GetValue<string>() ?? string.Empty);
        var lineStart = sourceUnit["line_start"]!.GetValue<int>() + sourceText[..match.StartUtf16].Count(x => x == '\n');
        var lineEnd = lineStart + match.Quote.Count(x => x == '\n');
        var locator = new JsonObject
        {
            ["source_unit_id"] = sourceUnit["unit_id"]!.GetValue<string>(),
            ["start_utf16"] = sourceUnit["start_utf16"]!.GetValue<int>() + match.StartUtf16,
            ["end_utf16"] = sourceUnit["start_utf16"]!.GetValue<int>() + match.EndUtf16,
            ["line_start"] = lineStart,
            ["line_end"] = lineEnd,
            ["heading_path"] = sourceUnit["heading_path"]?.DeepClone() as JsonArray ?? new JsonArray()
        };
        return new BatchFactEvidenceInput(locator, match.Quote);
    }

    private static IReadOnlyList<BatchFactInput> ParseCachedFacts(JsonObject payload, JsonObject sourceUnit)
    {
        var facts = payload["facts"]?.AsArray() ?? throw new InvalidOperationException("WB-BATCH-CACHE-422: facts cache payload 缺少 facts。");
        return facts.Select(item =>
        {
            var root = item?.AsObject() ?? throw new InvalidOperationException("WB-BATCH-CACHE-422: facts cache 项目格式无效。");
            var candidates = root["evidence_candidates"]?.AsArray() ?? throw new InvalidOperationException("WB-BATCH-CACHE-422: facts cache evidence_candidates 缺失。");
            var sourceText = NormalizeText(sourceUnit["text"]?.GetValue<string>() ?? string.Empty);
            var manualReviewNotes = new List<string>();
            var evidence = candidates.Select(entry =>
            {
                var evidenceObject = entry?.AsObject() ?? throw new InvalidOperationException("WB-BATCH-CACHE-422: facts cache evidence 格式无效。");
                var quote = evidenceObject["quote"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-BATCH-CACHE-422: facts cache quote 缺失。");
                var start = evidenceObject["start_utf16"]?.GetValue<int>() ?? throw new InvalidOperationException("WB-BATCH-CACHE-422: facts cache start_utf16 缺失。");
                var end = evidenceObject["end_utf16"]?.GetValue<int>() ?? throw new InvalidOperationException("WB-BATCH-CACHE-422: facts cache end_utf16 缺失。");
                if (start < 0 || end <= start || end > sourceText.Length)
                    throw new InvalidOperationException("WB-BATCH-CACHE-422: facts cache evidence 范围无效。");
                var storedQuote = sourceText[start..end];
                if (!string.Equals(storedQuote, quote, StringComparison.Ordinal))
                    throw new InvalidOperationException("WB-BATCH-CACHE-422: facts cache evidence 原文已变化。");
                return BuildEvidenceLocator(sourceUnit, new SourceEvidenceMatch(start, end, storedQuote, true));
            }).ToArray() ?? [];
            var notes = root["notes"]?.GetValue<string>();
            if (manualReviewNotes.Count > 0)
                notes = string.Join(" ", new[] { notes, string.Join(" ", manualReviewNotes) }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
            return new BatchFactInput(
                root["text"]?.GetValue<string>() ?? string.Empty,
                root["kind"]?.GetValue<string>() ?? "fact",
                root["certainty"]?.GetValue<string>() ?? "uncertain",
                manualReviewNotes.Count > 0 ? "yellow" : root["risk_level"]?.GetValue<string>() ?? "yellow",
                evidence,
                notes);
        }).ToArray();
    }

    private static JsonArray SerializeFacts(IReadOnlyList<BatchFactInput> facts, JsonObject sourceUnit)
        => new(facts.Select(fact => new JsonObject
        {
            ["text"] = fact.Text,
            ["kind"] = fact.Kind,
            ["certainty"] = fact.Certainty,
            ["risk_level"] = fact.RiskLevel,
            ["notes"] = string.IsNullOrWhiteSpace(fact.Notes) ? null : fact.Notes.Trim(),
            ["evidence_candidates"] = new JsonArray(fact.Evidence.Select(evidence => SerializeEvidenceCandidate(evidence, sourceUnit)).ToArray())
        }).ToArray());

    private static JsonObject SerializeEvidenceCandidate(BatchFactEvidenceInput evidence, JsonObject sourceUnit)
    {
        var locator = evidence.Locator;
        var sourceStart = sourceUnit["start_utf16"]!.GetValue<int>();
        var sourceLineStart = sourceUnit["line_start"]!.GetValue<int>();
        var start = locator["start_utf16"]!.GetValue<int>() - sourceStart;
        var end = locator["end_utf16"]!.GetValue<int>() - sourceStart;
        var lineStart = locator["line_start"]!.GetValue<int>() - sourceLineStart + 1;
        var lineEnd = locator["line_end"]!.GetValue<int>() - sourceLineStart + 1;
        if (start < 0 || end <= start || lineStart < 1 || lineEnd < lineStart)
            throw new InvalidOperationException("WB-BATCH-CACHE-422: evidence locator 无法转换为资料单元范围。");
        return new JsonObject
        {
            ["start_utf16"] = start,
            ["end_utf16"] = end,
            ["line_start"] = lineStart,
            ["line_end"] = lineEnd,
            ["heading_path"] = locator["heading_path"]?.DeepClone() as JsonArray ?? new JsonArray(),
            ["quote"] = evidence.Quote
        };
    }

    private static string NormalizeText(string value)
        => (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Normalize(System.Text.NormalizationForm.FormC);
}
