using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchFactEvidenceInput(JsonObject Locator, string Quote);

internal sealed record BatchFactInput(
    string Text,
    string Kind,
    string Certainty,
    string RiskLevel,
    IReadOnlyList<BatchFactEvidenceInput> Evidence,
    string? Notes = null);

internal sealed record BatchFactCommitRequest(
    string BatchId,
    string ItemId,
    int ExpectedItemRevision,
    string AttemptId,
    string ProviderId,
    string ProviderFingerprint,
    IReadOnlyList<BatchFactInput> Facts,
    int LogicalAttemptNumber = 1,
    int LatencyMs = 0,
    JsonObject? TokenUsage = null,
    string AttemptKind = "provider",
    string? CacheMaterializationRef = null,
    string? PacketHash = null,
    JsonObject? Packet = null);

internal sealed record BatchReviewRequest(
    string BatchId,
    string ItemId,
    int ExpectedItemRevision,
    IReadOnlyList<string> FactIds,
    string RiskLevel,
    string ReviewStatus,
    string? ReviewerNote = null,
    string? RejectionReason = null,
    IReadOnlyList<JsonObject>? FactDecisions = null);

internal sealed record BatchFactCommitResult(JsonObject Item, JsonObject Result, JsonObject Attempt, IReadOnlyList<JsonObject> Evidence);

internal sealed class BatchFactReviewRepository
{
    private static readonly ConcurrentDictionary<string, object> ItemLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] FactKinds = ["fact", "definition", "relation", "chronology", "geography"];
    private static readonly string[] Certainties = ["confirmed", "probable", "uncertain", "contested"];
    private static readonly string[] RiskLevels = ["green", "yellow", "red"];
    private static readonly string[] ReviewStatuses = ["partially_reviewed", "accepted", "rejected", "needs_reconcile"];

    private readonly BatchWorkspaceLayout _layout;
    private readonly BatchAuthoringContractRegistry _contract;
    private readonly string _ownerId;
    private readonly string _ownerInstanceId;
    private readonly string _workspaceMarkerHash;

    public BatchFactReviewRepository(
        WorkspaceService workspace,
        BatchAuthoringContractRegistry contract,
        string ownerId,
        string ownerInstanceId,
        string workspaceMarkerHash)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _contract = contract;
        _ownerId = BatchPathValidator.RequireIdentifier(ownerId, "owner_id");
        _ownerInstanceId = BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        RequireHash(workspaceMarkerHash, "workspace_marker_hash");
        _workspaceMarkerHash = workspaceMarkerHash.ToUpperInvariant();
        _ = _contract.RequireSchema("item");
        _ = _contract.RequireSchema("fact_result");
        _ = _contract.RequireSchema("evidence");
        _ = _contract.RequireSchema("attempt");
        _ = _contract.RequireSchema("review");
    }

    public JsonObject ReadItem(string batchId, string itemId)
        => BatchWorkspaceLayout.ReadJson(_layout.BatchItem(
            BatchPathValidator.RequireIdentifier(batchId, "batch_id"),
            BatchPathValidator.RequireIdentifier(itemId, "item_id")));

    public JsonObject ReadFactResult(string batchId, string itemId)
        => BatchWorkspaceLayout.ReadJson(_layout.BatchItemResult(
            BatchPathValidator.RequireIdentifier(batchId, "batch_id"),
            BatchPathValidator.RequireIdentifier(itemId, "item_id")));

    public BatchFactCommitResult CommitFacts(BatchFactCommitRequest request, DateTimeOffset? now = null)
    {
        ValidateCommitRequest(request);
        var key = _layout.BatchItem(request.BatchId, request.ItemId);
        var gate = ItemLocks.GetOrAdd(key, _ => new object());
        lock (gate)
        {
            using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(request.BatchId));
            var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var item = ReadItem(request.BatchId, request.ItemId);
            RequireItemRevision(item, request.ExpectedItemRevision);
            if (item["status"]?.GetValue<string>() is not ("extracting" or "queued"))
                throw new InvalidOperationException("WB-BATCH-STAGE-409: 该项目当前不允许提交事实结果。");
            var manifest = BatchWorkspaceLayout.ReadJson(_layout.BatchManifest(request.BatchId));
            var lease = BatchLeaseGuard.Require(item, manifest, request.AttemptId, _ownerInstanceId, timestamp);
            var snapshot = BatchWorkspaceLayout.ReadJson(_layout.BatchSnapshotMeta(request.BatchId, item["source_snapshot_id"]!.GetValue<string>()));
            var sourceUnit = BatchWorkspaceLayout.ReadJson(_layout.BatchSourceUnit(request.BatchId, item["source_snapshot_id"]!.GetValue<string>(), item["source_unit_id"]!.GetValue<string>()));
            var attemptId = BatchPathValidator.RequireIdentifier(request.AttemptId, "attempt_id");
            var resultId = NewId("result.fact");
            var evidence = new List<JsonObject>();
            var facts = new JsonArray();
            for (var index = 0; index < request.Facts.Count; index++)
            {
                var fact = request.Facts[index];
                var factObject = BuildFact(request, item, snapshot, sourceUnit, resultId, attemptId, index, fact, timestamp, evidence);
                facts.Add(factObject);
            }

            var result = BuildFactResult(request, item, sourceUnit, resultId, attemptId, facts, timestamp);
            var attempt = BuildAttempt(request, item, resultId, lease, timestamp);
            _contract.ValidatePayload("fact_result", result);
            _contract.ValidatePayload("attempt", attempt);
            foreach (var sidecar in evidence)
                _contract.ValidatePayload("evidence", sidecar);
            foreach (var sidecar in evidence)
                BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchEvidence(request.BatchId, sidecar["evidence_id"]!.GetValue<string>()), sidecar);
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchItemResult(request.BatchId, request.ItemId), result);
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchItemAttempt(request.BatchId, request.ItemId, attemptId), attempt);

            var nextItem = (JsonObject)item.DeepClone();
            nextItem["status"] = request.Facts.Count == 0 ? "no_candidate" : "facts_review";
            nextItem["recovery_status"] = "clean";
            nextItem["status_reason"] = request.Facts.Count == 0 ? "no_candidate_returned" : "facts_succeeded";
            nextItem["revision"] = request.ExpectedItemRevision + 1;
            nextItem["attempt_count"] = item["attempt_count"]!.GetValue<int>() + 1;
            nextItem["review_status"] = request.Facts.Count == 0 ? "no_candidate" : "unreviewed";
            nextItem["facts_result_ref"] = resultId;
            nextItem["evidence_ref"] = new JsonArray(evidence.Select(x => JsonValue.Create(x["evidence_id"]!.GetValue<string>())).ToArray());
            nextItem["result_stage"] = "facts";
            nextItem["active_attempt_stage"] = "none";
            nextItem.Remove("lease");
            if (!string.IsNullOrWhiteSpace(request.CacheMaterializationRef)) nextItem["cache_materialization_ref"] = request.CacheMaterializationRef;
            nextItem["updated_at"] = timestamp.ToString("O");
            nextItem["correlation_id"] = attempt["correlation_id"]!.GetValue<string>();
            _contract.ValidatePayload("item", nextItem);
            BatchWorkspaceLayout.WriteJsonAtomic(key, nextItem);
            return new BatchFactCommitResult(nextItem, result, attempt, evidence);
        }
    }

    public JsonObject ReviewFacts(BatchReviewRequest request, DateTimeOffset? now = null)
        => ReviewFacts(request, false, now);

    public JsonObject ReviewFacts(BatchReviewRequest request, bool metadataConsentAvailable, DateTimeOffset? now = null)
    {
        ValidateReviewRequest(request);
        var key = _layout.BatchItem(request.BatchId, request.ItemId);
        var gate = ItemLocks.GetOrAdd(key, _ => new object());
        lock (gate)
        {
            var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var item = ReadItem(request.BatchId, request.ItemId);
            RequireItemRevision(item, request.ExpectedItemRevision);
            if (item["status"]?.GetValue<string>() != "facts_review" || item["result_stage"]?.GetValue<string>() != "facts")
                throw new InvalidOperationException("WB-BATCH-STAGE-409: 该项目当前不允许审核事实。");
            var result = ReadFactResult(request.BatchId, request.ItemId);
            var facts = result["facts"]?.AsArray() ?? throw new InvalidOperationException("WB-BATCH-RESULT-422: 事实结果缺少 facts。");
            var knownIds = facts.Select(x => x!["fact_id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            if (request.FactIds.Any(x => !knownIds.Contains(x)))
                throw new InvalidOperationException("WB-BATCH-REVIEW-422: 审核选择包含不存在的事实 ID。");
            if (request.ReviewStatus == "accepted" && request.FactIds.Count == 0)
                throw new InvalidOperationException("WB-BATCH-REVIEW-422: 接受事实时至少选择一条事实。");
            if (request.RiskLevel == "red" && string.IsNullOrWhiteSpace(request.ReviewerNote))
                throw new InvalidOperationException("WB-BATCH-REVIEW-422: 红色风险事实必须填写审核说明。");
            if (request.FactDecisions is not null)
            {
                var decisionIds = request.FactDecisions.Select(decision => decision["fact_id"]?.GetValue<string>() ?? string.Empty).ToArray();
                if (decisionIds.Distinct(StringComparer.Ordinal).Count() != request.FactDecisions.Count
                    || !decisionIds.ToHashSet(StringComparer.Ordinal).SetEquals(request.FactIds))
                    throw new InvalidOperationException("WB-BATCH-REVIEW-422: 逐条事实决定必须与 fact_ids 完全对应。");
                foreach (var factDecision in request.FactDecisions)
                {
                    if (factDecision["decision"]?.GetValue<string>() is not ("accept" or "reject" or "needs_review"))
                        throw new InvalidOperationException("WB-BATCH-REVIEW-422: 逐条事实决定无效。");
                }
            }

            var acceptedHash = ComputeAcceptedFactSetHash(result, request.FactIds);
            var decision = new JsonObject
            {
                ["schema_version"] = "awake.worldbook.batch-review-decision.v2",
                ["decision_id"] = NewId("decision"),
                ["batch_id"] = request.BatchId,
                ["item_id"] = request.ItemId,
                ["expected_item_revision"] = request.ExpectedItemRevision,
                ["fact_ids"] = new JsonArray(request.FactIds.Select(value => JsonValue.Create(value)).ToArray()),
                ["risk_level"] = request.RiskLevel,
                ["review_status"] = request.ReviewStatus,
                ["reviewer_owner_id"] = _ownerId,
                ["created_at"] = timestamp.ToString("O")
            };
            if (!string.IsNullOrWhiteSpace(request.ReviewerNote)) decision["reviewer_note"] = request.ReviewerNote;
            if (!string.IsNullOrWhiteSpace(request.RejectionReason)) decision["rejection_reason"] = request.RejectionReason;
            if (request.FactDecisions is not null) decision["fact_decisions"] = new JsonArray(request.FactDecisions.Select(value => value.DeepClone()).ToArray());
            decision["accepted_fact_set_hash"] = acceptedHash;

            var nextItem = (JsonObject)item.DeepClone();
            nextItem["revision"] = request.ExpectedItemRevision + 1;
            nextItem["review_status"] = request.ReviewStatus;
            nextItem["accepted_fact_ids"] = new JsonArray(request.FactIds.Select(value => JsonValue.Create(value)).ToArray());
            nextItem["accepted_fact_set_hash"] = acceptedHash;
            nextItem["updated_at"] = timestamp.ToString("O");
            var metadataQueued = request.ReviewStatus == "accepted" && metadataConsentAvailable;
            nextItem["status"] = request.ReviewStatus == "rejected"
                ? "skipped"
                : metadataQueued ? "metadata_pending" : "facts_review";
            nextItem["status_reason"] = request.ReviewStatus switch
            {
                "accepted" => metadataQueued ? "metadata_queued" : "facts_accepted_waiting_for_metadata_consent",
                "rejected" => "facts_rejected_by_reviewer",
                "needs_reconcile" => "review_needs_reconcile",
                _ => "facts_partially_reviewed"
            };
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchItemReview(request.BatchId, request.ItemId), decision);
            BatchWorkspaceLayout.WriteJsonAtomic(key, nextItem);
            return decision;
        }
    }

    public bool QueueAcceptedItemForMetadata(string batchId, string itemId, DateTimeOffset? now = null)
    {
        var key = _layout.BatchItem(
            BatchPathValidator.RequireIdentifier(batchId, "batch_id"),
            BatchPathValidator.RequireIdentifier(itemId, "item_id"));
        var gate = ItemLocks.GetOrAdd(key, _ => new object());
        lock (gate)
        {
            var item = ReadItem(batchId, itemId);
            if (item["status"]?.GetValue<string>() != "facts_review"
                || item["review_status"]?.GetValue<string>() != "accepted"
                || item["result_stage"]?.GetValue<string>() != "facts"
                || item["active_attempt_stage"]?.GetValue<string>() != "none"
                || item["accepted_fact_ids"] is not JsonArray acceptedFactIds
                || acceptedFactIds.Count == 0
                || string.IsNullOrWhiteSpace(item["accepted_fact_set_hash"]?.GetValue<string>()))
                return false;

            var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var nextItem = (JsonObject)item.DeepClone();
            nextItem["status"] = "metadata_pending";
            nextItem["status_reason"] = "metadata_queued";
            nextItem["revision"] = item["revision"]!.GetValue<int>() + 1;
            nextItem["updated_at"] = timestamp.ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(key, nextItem);
            return true;
        }
    }

    private JsonObject BuildFact(
        BatchFactCommitRequest request,
        JsonObject item,
        JsonObject snapshot,
        JsonObject sourceUnit,
        string resultId,
        string attemptId,
        int ordinal,
        BatchFactInput input,
        DateTimeOffset timestamp,
        List<JsonObject> evidence)
    {
        if (string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 12000) throw new InvalidOperationException("WB-BATCH-FACT-422: 事实文本不能为空且不能超过 12000 字符。");
        RequireEnum(input.Kind, FactKinds, "事实类型");
        RequireEnum(input.Certainty, Certainties, "事实确定程度");
        RequireEnum(input.RiskLevel, RiskLevels, "事实风险等级");
        if (input.Evidence is null || input.Evidence.Count == 0) throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: 每条事实必须绑定至少一条原文证据。");

        var sourceText = NormalizeText(sourceUnit["text"]!.GetValue<string>());
        var resolvedEvidence = new List<(JsonObject Locator, string Quote)>();
        var requiresManualReview = false;
        foreach (var evidenceInput in input.Evidence)
        {
            var locator = ValidateLocator(evidenceInput.Locator, sourceUnit, request.BatchId, request.ItemId);
            var quote = NormalizeText(evidenceInput.Quote);
            if (quote.Length == 0) throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: 证据原文不能为空。");
            var localStart = locator["start_utf16"]!.GetValue<int>() - sourceUnit["start_utf16"]!.GetValue<int>();
            var localEnd = locator["end_utf16"]!.GetValue<int>() - sourceUnit["start_utf16"]!.GetValue<int>();
            if (localStart < 0 || localEnd <= localStart || localEnd > sourceText.Length)
                throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: locator 超出当前资料原文范围。");

            var sourceQuote = sourceText[localStart..localEnd];
            if (!string.Equals(sourceQuote, quote, StringComparison.Ordinal))
            {
                if (!SourceEvidenceMatcher.AreEquivalent(sourceQuote, quote))
                    throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: quote 与 locator 指向的原文不一致。");
                requiresManualReview = true;
            }
            resolvedEvidence.Add((locator, sourceQuote));
        }

        var riskLevel = requiresManualReview && input.RiskLevel == "green" ? "yellow" : input.RiskLevel;
        var notes = input.Notes;
        if (requiresManualReview && string.IsNullOrWhiteSpace(notes))
            notes = "证据原文存在格式差异，已还原为资料原文，请人工核对。";
        var factId = "fact." + Hashing.Sha256Text(CanonicalJson.Serialize(new JsonObject
        {
            ["batch_id"] = request.BatchId,
            ["item_id"] = request.ItemId,
            ["attempt_id"] = attemptId,
            ["ordinal"] = ordinal,
            ["text"] = input.Text,
            ["kind"] = input.Kind,
            ["certainty"] = input.Certainty,
            ["risk_level"] = riskLevel
        }));
        var evidenceIds = new JsonArray();
        foreach (var resolved in resolvedEvidence)
        {
            var locator = resolved.Locator;
            var quote = resolved.Quote;
            var locatorHash = CanonicalJson.Hash(locator);
            var quoteHash = Hashing.Sha256Text(quote);
            var evidenceId = "evidence." + Hashing.Sha256Text(resultId + ":" + factId + ":" + locatorHash + ":" + quoteHash);
            var bindingHash = CanonicalJson.Hash(new JsonObject
            {
                ["batch_id"] = request.BatchId,
                ["item_id"] = request.ItemId,
                ["normalized_content_hash"] = item["normalized_content_hash"]!.GetValue<string>(),
                ["quote_hash"] = quoteHash,
                ["raw_content_hash"] = item["raw_content_hash"]!.GetValue<string>(),
                ["result_ref"] = resultId,
                ["source_snapshot_id"] = item["source_snapshot_id"]!.GetValue<string>(),
                ["locator_hash"] = locatorHash
            });
            evidence.Add(new JsonObject
            {
                ["schema_version"] = "awake.worldbook.batch-evidence.v2",
                ["evidence_id"] = evidenceId,
                ["batch_id"] = request.BatchId,
                ["item_id"] = request.ItemId,
                ["result_ref"] = resultId,
                ["source_snapshot_id"] = item["source_snapshot_id"]!.GetValue<string>(),
                ["raw_content_hash"] = snapshot["raw_content_hash"]!.GetValue<string>(),
                ["normalized_content_hash"] = item["normalized_content_hash"]!.GetValue<string>(),
                ["locator"] = locator,
                ["locator_hash"] = locatorHash,
                ["quote"] = quote,
                ["quote_hash"] = quoteHash,
                ["created_by_attempt_id"] = attemptId,
                ["binding_hash"] = bindingHash,
                ["fact_id"] = factId
            });
            evidenceIds.Add(evidenceId);
        }
        return new JsonObject
        {
            ["fact_id"] = factId,
            ["text"] = input.Text.Normalize(NormalizationForm.FormC),
            ["kind"] = input.Kind,
            ["certainty"] = input.Certainty,
            ["source_unit_id"] = sourceUnit["unit_id"]!.GetValue<string>(),
            ["evidence_ids"] = evidenceIds,
            ["risk_level"] = riskLevel,
            ["notes"] = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    private JsonObject BuildFactResult(BatchFactCommitRequest request, JsonObject item, JsonObject sourceUnit, string resultId, string attemptId, JsonArray facts, DateTimeOffset timestamp)
    {
        var result = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-fact-result.v2",
            ["result_id"] = resultId,
            ["batch_id"] = request.BatchId,
            ["item_id"] = request.ItemId,
            ["attempt_id"] = attemptId,
            ["source_snapshot_id"] = item["source_snapshot_id"]!.GetValue<string>(),
            ["source_unit_id"] = sourceUnit["unit_id"]!.GetValue<string>(),
            ["normalized_content_hash"] = item["normalized_content_hash"]!.GetValue<string>(),
            ["facts"] = facts,
            ["review_only"] = true,
            ["created_at"] = timestamp.ToString("O"),
            ["created_from_item_revision"] = request.ExpectedItemRevision
        };
        var hashInput = (JsonObject)result.DeepClone();
        hashInput.Remove("result_hash");
        hashInput.Remove("created_at");
        result["result_hash"] = CanonicalJson.Hash(hashInput);
        return result;
    }

    private JsonObject BuildAttempt(BatchFactCommitRequest request, JsonObject item, string resultId, BatchLeaseSnapshot lease, DateTimeOffset timestamp)
    {
        var attempt = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-attempt-result.v2",
            ["attempt_id"] = lease.AttemptId,
            ["provider_fingerprint"] = request.ProviderFingerprint,
            ["outcome"] = "succeeded",
            ["error_class"] = "none",
            ["started_at"] = timestamp.ToString("O"),
            ["finished_at"] = timestamp.ToString("O"),
            ["latency_ms"] = request.LatencyMs,
            ["token_usage"] = NormalizeTokenUsage(request.TokenUsage),
            ["correlation_id"] = NewId("correlation"),
            ["logical_attempt_number"] = request.LogicalAttemptNumber,
            ["exchange_count"] = 1,
            ["batch_id"] = request.BatchId,
            ["item_id"] = request.ItemId,
            ["provider_id"] = request.ProviderId,
            ["owner_instance_id"] = lease.OwnerInstanceId,
            ["fence_token"] = lease.FenceToken,
            ["deadline_at"] = lease.DeadlineAt.ToString("O"),
            ["lease"] = BatchLeaseGuard.Clone(lease),
            ["provider_request_id"] = "offline." + lease.AttemptId,
            ["item_revision"] = request.ExpectedItemRevision,
            ["claim_generation"] = lease.ClaimGeneration,
            ["attempt_kind"] = request.AttemptKind,
            ["token_usage_known"] = request.TokenUsage is not null,
            ["stage"] = "facts",
            ["result_schema_id"] = "awake.worldbook.batch-fact-result.v2",
            ["result_ref"] = resultId
        };
        if (!string.IsNullOrWhiteSpace(request.PacketHash)) attempt["packet_hash"] = request.PacketHash;
        if (request.Packet is not null) attempt["packet"] = request.Packet.DeepClone();
        if (!string.IsNullOrWhiteSpace(request.CacheMaterializationRef)) attempt["cache_materialization_ref"] = request.CacheMaterializationRef;
        return attempt;
    }

    private static JsonObject NormalizeTokenUsage(JsonObject? tokenUsage)
    {
        if (tokenUsage is null)
            return new JsonObject { ["known"] = false, ["input_tokens"] = null, ["output_tokens"] = null };
        var input = tokenUsage["input_tokens"]?.GetValue<int>() ?? tokenUsage["prompt_tokens"]?.GetValue<int>();
        var output = tokenUsage["output_tokens"]?.GetValue<int>() ?? tokenUsage["completion_tokens"]?.GetValue<int>();
        if (input is null || output is null || input < 0 || output < 0)
            throw new InvalidOperationException("WB-BATCH-ATTEMPT-422: token usage 格式无效。");
        return new JsonObject { ["known"] = true, ["input_tokens"] = input.Value, ["output_tokens"] = output.Value };
    }

    private static JsonObject ValidateLocator(JsonObject locator, JsonObject sourceUnit, string batchId, string itemId)
    {
        var sourceUnitId = locator["source_unit_id"]?.GetValue<string>();
        if (sourceUnitId != sourceUnit["unit_id"]?.GetValue<string>())
            throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: evidence locator 未绑定当前 source unit。");
        var start = locator["start_utf16"]?.GetValue<int>() ?? throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: locator 缺少 start_utf16。");
        var end = locator["end_utf16"]?.GetValue<int>() ?? throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: locator 缺少 end_utf16。");
        if (start < sourceUnit["start_utf16"]!.GetValue<int>() || end > sourceUnit["end_utf16"]!.GetValue<int>() || end <= start)
            throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: locator 超出 source unit 范围。");
        var result = new JsonObject
        {
            ["source_unit_id"] = sourceUnitId,
            ["start_utf16"] = start,
            ["end_utf16"] = end,
            ["line_start"] = locator["line_start"]?.GetValue<int>() ?? throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: locator 缺少 line_start。"),
            ["line_end"] = locator["line_end"]?.GetValue<int>() ?? throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: locator 缺少 line_end。"),
            ["heading_path"] = locator["heading_path"]?.DeepClone() as JsonArray ?? new JsonArray()
        };
        return result;
    }

    private static JsonObject CanonicalFactForSet(JsonNode fact)
        => new()
        {
            ["fact_id"] = fact["fact_id"]!.GetValue<string>(),
            ["text"] = fact["text"]!.GetValue<string>(),
            ["kind"] = fact["kind"]!.GetValue<string>(),
            ["certainty"] = fact["certainty"]!.GetValue<string>(),
            ["risk_level"] = fact["risk_level"]!.GetValue<string>(),
            ["source_unit_id"] = fact["source_unit_id"]!.GetValue<string>(),
            ["evidence_ids"] = fact["evidence_ids"]!.DeepClone()
        };

    internal static string ComputeAcceptedFactSetHash(JsonObject result, IEnumerable<string> acceptedFactIds)
    {
        var facts = result["facts"]?.AsArray() ?? throw new InvalidOperationException("WB-BATCH-RESULT-422: 事实结果缺少 facts。");
        var accepted = acceptedFactIds.ToHashSet(StringComparer.Ordinal);
        if (accepted.Count == 0) throw new InvalidOperationException("WB-BATCH-FACT-SET-409: 已接受事实集不能为空。");
        var knownIds = facts
            .Where(value => value is not null)
            .Select(value => value!["fact_id"]?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
        if (!accepted.IsSubsetOf(knownIds)) throw new InvalidOperationException("WB-BATCH-FACT-SET-409: 已接受事实集包含不存在的事实。");
        var canonicalFacts = facts
            .Where(value => value is not null && accepted.Contains(value!["fact_id"]!.GetValue<string>()))
            .Select(value => CanonicalFactForSet(value!))
            .OrderBy(value => CanonicalJson.Serialize(value), StringComparer.Ordinal)
            .ToArray();
        return CanonicalJson.Hash(new JsonArray(canonicalFacts));
    }

    private static void ValidateCommitRequest(BatchFactCommitRequest request)
    {
        BatchPathValidator.RequireIdentifier(request.BatchId, "batch_id");
        BatchPathValidator.RequireIdentifier(request.ItemId, "item_id");
        BatchPathValidator.RequireIdentifier(request.AttemptId, "attempt_id");
        RequireHash(request.ProviderFingerprint, "provider_fingerprint");
        if (request.ProviderId is not ("local" or "cloud")) throw new InvalidOperationException("WB-BATCH-PROVIDER-422: Provider 类型无效。");
        if (request.AttemptKind is not ("provider" or "cache_hit")) throw new InvalidOperationException("WB-BATCH-ATTEMPT-422: attempt_kind 无效。");
        if (request.AttemptKind == "cache_hit" && string.IsNullOrWhiteSpace(request.CacheMaterializationRef)) throw new InvalidOperationException("WB-BATCH-CACHE-422: cache_hit 缺少 materialization 引用。");
        if (request.ExpectedItemRevision < 0) throw new InvalidOperationException("WB-BATCH-REVISION-422: item revision 无效。");
        if (request.Facts is null) throw new InvalidOperationException("WB-BATCH-FACT-422: facts 不能为空。");
    }

    private static void ValidateReviewRequest(BatchReviewRequest request)
    {
        BatchPathValidator.RequireIdentifier(request.BatchId, "batch_id");
        BatchPathValidator.RequireIdentifier(request.ItemId, "item_id");
        if (request.ExpectedItemRevision < 0) throw new InvalidOperationException("WB-BATCH-REVISION-422: item revision 无效。");
        RequireEnum(request.RiskLevel, RiskLevels, "审核风险等级");
        RequireEnum(request.ReviewStatus, ReviewStatuses, "审核状态");
        if (request.FactIds is null || request.FactIds.Distinct(StringComparer.Ordinal).Count() != request.FactIds.Count)
            throw new InvalidOperationException("WB-BATCH-REVIEW-422: fact_ids 不能重复。");
        foreach (var factId in request.FactIds) BatchPathValidator.RequireIdentifier(factId, "fact_id");
    }

    private static void RequireItemRevision(JsonObject item, int expectedRevision)
    {
        var actual = item["revision"]?.GetValue<int>() ?? -1;
        if (actual != expectedRevision) throw new InvalidOperationException("WB-BATCH-REVISION-409: item revision 已变化，请刷新后重试。");
    }

    private static void RequireEnum(string value, IReadOnlyCollection<string> allowed, string label)
    {
        if (!allowed.Contains(value, StringComparer.Ordinal)) throw new InvalidOperationException($"WB-BATCH-REQUEST-422: {label} 不受支持。");
    }

    private static string NormalizeText(string value)
        => (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Normalize(NormalizationForm.FormC);

    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
    private static void RequireHash(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(x => !Uri.IsHexDigit(x)))
            throw new InvalidOperationException($"WB-BATCH-REQUEST-422: {field} 必须是 SHA-256。");
    }
}
