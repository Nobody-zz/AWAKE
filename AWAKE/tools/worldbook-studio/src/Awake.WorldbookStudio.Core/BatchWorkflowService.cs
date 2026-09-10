using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed class BatchWorkflowService
{
    private readonly WorkspaceService _workspace;
    private readonly BatchAuthoringContractRegistry _contract;
    private readonly BatchWorkspaceLayout _layout;
    private readonly BatchScanRepository _scanRepository;
    private readonly BatchCreateRepository _createRepository;
    private readonly BatchFactReviewRepository _factRepository;
    private readonly BatchConsentService? _consents;
    private readonly string _ownerId;
    private readonly string _ownerInstanceId;
    private readonly string _workspaceMarkerHash;

    public BatchWorkflowService(
        WorkspaceService workspace,
        BatchAuthoringContractRegistry contract,
        string ownerId,
        string ownerInstanceId,
        string workspaceMarkerHash,
        BatchConsentService? consents = null)
    {
        _workspace = workspace;
        _contract = contract;
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _ownerId = BatchPathValidator.RequireIdentifier(ownerId, "owner_id");
        _ownerInstanceId = BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        if (workspaceMarkerHash.Length != 64 || workspaceMarkerHash.Any(value => !Uri.IsHexDigit(value)))
            throw new InvalidOperationException("WB-BATCH-OWNER-422: workspace_marker_hash 无效。");
        _workspaceMarkerHash = workspaceMarkerHash.ToUpperInvariant();
        _scanRepository = new BatchScanRepository(workspace, contract);
        _createRepository = new BatchCreateRepository(workspace, contract, ownerId, ownerInstanceId, workspaceMarkerHash);
        _factRepository = new BatchFactReviewRepository(workspace, contract, ownerId, ownerInstanceId, workspaceMarkerHash);
        _consents = consents;
    }

    public BatchScanResult Scan(IEnumerable<BatchSourceInput> inputs, string ownerId, string ownerInstanceId, string workspaceMarkerHash, DateTimeOffset? now = null)
        => _scanRepository.CreateScan(inputs, ownerId, ownerInstanceId, workspaceMarkerHash, now);

    public BatchCreateResult Create(BatchCreateRequest request, string providerFingerprint, DateTimeOffset? now = null)
        => _createRepository.Create(request, providerFingerprint, now);

    public BatchFactCommitResult CommitFacts(BatchFactCommitRequest request, DateTimeOffset? now = null)
        => _factRepository.CommitFacts(request, now);

    public JsonObject ReviewFacts(BatchReviewRequest request, DateTimeOffset? now = null)
        => _factRepository.ReviewFacts(
            request,
            request.ReviewStatus == "accepted" && (_consents?.HasIssuedMetadataAuthorization(request.BatchId, request.ItemId, now) ?? false),
            now);

    public JsonObject GetPublicManifest(string batchId)
        => BatchPublicProjection.ProjectManifest(RequireOwnedBatch(batchId));

    public JsonObject GetPublicBatch(string batchId)
    {
        var manifest = RequireOwnedBatch(batchId);
        var items = manifest["item_ids"]?.AsArray()
            .Select(item => GetPublicItem(batchId, item!.GetValue<string>()))
            .ToArray() ?? [];
        return new JsonObject
        {
            ["manifest"] = BatchPublicProjection.ProjectManifest(manifest),
            ["items"] = new JsonArray(items.Select(item => item.DeepClone()).ToArray())
        };
    }

    public JsonObject GetPublicScan(string scanId)
        => BatchPublicProjection.ProjectScan(RequireOwnedScan(scanId));

    public JsonObject GetPublicSourceUnit(string batchId, string snapshotId, string sourceUnitId)
    {
        _ = RequireOwnedBatch(batchId);
        return BatchPublicProjection.ProjectSourceUnit(BatchWorkspaceLayout.ReadJson(_layout.BatchSourceUnit(batchId, snapshotId, sourceUnitId)), "batch");
    }

    public JsonObject GetPublicPrebatchSourceUnit(string scanId, string snapshotId, string sourceUnitId)
    {
        _ = RequireOwnedScan(scanId);
        return BatchPublicProjection.ProjectSourceUnit(BatchWorkspaceLayout.ReadJson(_layout.SourceUnit(scanId, snapshotId, sourceUnitId)), "prebatch");
    }

    public JsonObject GetPublicReport(string batchId)
    {
        var manifest = RequireOwnedBatch(batchId);
        var itemIds = manifest["item_ids"]?.AsArray().Select(item => item!.GetValue<string>()).ToArray() ?? [];
        var reportItems = new JsonArray();
        var attemptOutcomes = new JsonArray();
        var unknownResults = new JsonArray();
        var createdDocuments = new JsonArray();
        var counts = new JsonObject
        {
            ["total"] = itemIds.Length,
            ["queued"] = 0,
            ["running"] = 0,
            ["review_pending"] = 0,
            ["ready_to_create"] = 0,
            ["created"] = 0,
            ["failed"] = 0,
            ["unknown_result"] = 0,
            ["no_candidate"] = 0
        };
        var cacheHits = 0;
        foreach (var itemId in itemIds)
        {
            var item = _factRepository.ReadItem(batchId, itemId);
            var status = item["status"]?.GetValue<string>() ?? "unknown";
            var reviewStatus = item["review_status"]?.GetValue<string>() ?? "unreviewed";
            var countBucket = status switch
            {
                "queued" or "metadata_pending" => "queued",
                "extracting" or "metadata_running" => "running",
                "facts_review" => "review_pending",
                "ready_to_create" => "ready_to_create",
                "created" => "created",
                "failed" => "failed",
                "unknown_result" => "unknown_result",
                "no_candidate" => "no_candidate",
                _ => null
            };
            if (countBucket is not null) counts[countBucket] = counts[countBucket]!.GetValue<int>() + 1;
            var risk = "unknown";
            var resultPath = _layout.BatchItemResult(batchId, itemId);
            if (File.Exists(resultPath))
            {
                var result = BatchWorkspaceLayout.ReadJson(resultPath);
                risk = result["facts"]?.AsArray()
                    .Select(fact => fact?["risk_level"]?.GetValue<string>())
                    .Where(value => value is not null)
                    .OrderByDescending(value => value == "red" ? 3 : value == "yellow" ? 2 : 1)
                    .FirstOrDefault() ?? "unknown";
                if (result["attempt_kind"]?.GetValue<string>() == "cache_hit") cacheHits++;
            }
            var reportItem = new JsonObject
            {
                ["item_id"] = itemId,
                ["status"] = status,
                ["review_status"] = reviewStatus,
                ["risk_level"] = risk
            };
            if (item["document_id"] is not null) reportItem["document_id"] = item["document_id"]!.DeepClone();
            if (status is "failed" or "unknown_result")
            {
                reportItem["error_code"] = item["last_error_code"]?.DeepClone() ?? "WB-BATCH-PROVIDER-500";
                reportItem["message"] = item["last_error_message"]?.DeepClone() ?? "项目处理失败，请查看项目详情后重试。";
            }
            reportItems.Add(reportItem);
            if (status == "unknown_result") unknownResults.Add(itemId);
            var attemptDirectory = Path.Combine(_layout.BatchRoot(batchId), "items", itemId + ".attempts");
            if (Directory.Exists(attemptDirectory))
            {
                foreach (var attemptPath in Directory.EnumerateFiles(attemptDirectory, "*.json"))
                {
                    var attempt = BatchWorkspaceLayout.ReadJson(attemptPath);
                    var publicAttempt = new JsonObject
                    {
                        ["item_id"] = itemId,
                        ["attempt_id"] = attempt["attempt_id"]!.DeepClone(),
                        ["outcome"] = attempt["outcome"]?.DeepClone() ?? "unknown",
                        ["provider_id"] = attempt["provider_id"]?.DeepClone() ?? "local"
                    };
                    if (attempt["latency_ms"] is not null) publicAttempt["latency_ms"] = attempt["latency_ms"]!.DeepClone();
                    attemptOutcomes.Add(publicAttempt);
                }
            }
            var documentPath = _layout.BatchItemDocument(batchId, itemId);
            if (File.Exists(documentPath))
            {
                var document = BatchWorkspaceLayout.ReadJson(documentPath);
                createdDocuments.Add(new JsonObject
                {
                    ["item_id"] = itemId,
                    ["document_id"] = document["document_id"]!.DeepClone(),
                    ["status"] = document["status"]?.DeepClone() ?? "created"
                });
            }
        }
        return new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-report-public.v2",
            ["batch_id"] = batchId,
            ["manifest_revision"] = manifest["revision"]!.DeepClone(),
            ["status"] = manifest["status"]!.DeepClone(),
            ["counts"] = counts,
            ["items"] = reportItems,
            ["cache_hits"] = cacheHits,
            ["attempt_outcomes"] = attemptOutcomes,
            ["unknown_results"] = unknownResults,
            ["created_documents"] = createdDocuments,
            ["created_at"] = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    public JsonObject GetPublicItem(string batchId, string itemId)
    {
        _ = RequireOwnedBatch(batchId);
        var item = _factRepository.ReadItem(batchId, itemId);
        JsonObject? result = null;
        if (item["facts_result_ref"] is not null)
        {
            var path = _layout.BatchItemResult(batchId, itemId);
            if (File.Exists(path)) result = BatchWorkspaceLayout.ReadJson(path);
        }
        JsonObject? metadata = null;
        var metadataPath = _layout.BatchItemMetadata(batchId, itemId);
        if (File.Exists(metadataPath)) metadata = BatchWorkspaceLayout.ReadJson(metadataPath);
        var sourceUnitPath = Directory.EnumerateFiles(
                Path.Combine(_layout.BatchRoot(batchId), "sources"),
                "*.json",
                SearchOption.AllDirectories)
            .FirstOrDefault(path => path.Contains(".units", StringComparison.Ordinal)
                && BatchWorkspaceLayout.ReadJson(path)["unit_id"]?.GetValue<string>() == item["source_unit_id"]?.GetValue<string>());
        var sourceUnit = sourceUnitPath is null ? null : BatchWorkspaceLayout.ReadJson(sourceUnitPath);
        var evidence = new List<JsonObject>();
        if (item["evidence_ref"] is JsonArray references)
        {
            foreach (var reference in references)
            {
                var evidenceId = reference?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(evidenceId)) continue;
                var evidencePath = _layout.BatchEvidence(batchId, evidenceId);
                if (File.Exists(evidencePath)) evidence.Add(BatchWorkspaceLayout.ReadJson(evidencePath));
            }
        }
        return BatchPublicProjection.ProjectItem(item, result, metadata, sourceUnit, evidence);
    }

    public JsonObject GetPublicReviewProjection(string batchId, string itemId)
    {
        var item = GetPublicItem(batchId, itemId);
        var facts = item["facts"]?.DeepClone() as JsonArray ?? new JsonArray();
        var evidence = item["evidence"]?.DeepClone() as JsonArray ?? new JsonArray();
        var sourceUnits = new JsonArray();
        if (item["source_unit"] is JsonObject sourceUnit) sourceUnits.Add(sourceUnit.DeepClone());
        var metadataResults = new JsonArray();
        if (item["metadata"] is JsonObject metadata) metadataResults.Add(metadata.DeepClone());
        item.Remove("facts");
        item.Remove("evidence");
        item.Remove("source_unit");
        item.Remove("metadata");
        return new JsonObject
        {
            ["item"] = item,
            ["facts"] = facts,
            ["evidence"] = evidence,
            ["source_units"] = sourceUnits,
            ["metadata_results"] = metadataResults
        };
    }

    private JsonObject RequireOwnedBatch(string batchId)
    {
        var manifest = _createRepository.ReadManifest(batchId);
        RequireOwnership(manifest, "batch");
        return manifest;
    }

    private JsonObject RequireOwnedScan(string scanId)
    {
        var scan = BatchWorkspaceLayout.ReadJson(_layout.ScanManifest(BatchPathValidator.RequireIdentifier(scanId, "scan_id")));
        RequireOwnership(scan, "scan");
        return scan;
    }

    private void RequireOwnership(JsonObject document, string scope)
    {
        if (!string.Equals(document["owner_id"]?.GetValue<string>(), _ownerId, StringComparison.Ordinal)
            || !string.Equals(document["workspace_marker_hash"]?.GetValue<string>(), _workspaceMarkerHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"WB-BATCH-OWNER-403: 当前工作区无权读取该 {scope}。");
    }
}

internal static class BatchPublicProjection
{
    public static JsonObject ProjectScan(JsonObject scan)
    {
        var result = (JsonObject)scan.DeepClone();
        result["schema_version"] = "awake.worldbook.batch-scan-public.v2";
        result.Remove("owner_id");
        result.Remove("workspace_marker_hash");
        result.Remove("created_owner_instance_id");
        result.Remove("prebatch_path");
        if (result["promotion"] is JsonObject promotion)
        {
            promotion.Remove("operation_id");
            promotion.Remove("source_scan_hash");
            promotion.Remove("journal_ref");
            promotion.Remove("temp_path");
            promotion.Remove("target_manifest_hash");
            promotion.Remove("request_idempotency_key");
        }
        return result;
    }

    public static JsonObject ProjectSourceUnit(JsonObject sourceUnit, string scope)
    {
        var result = (JsonObject)sourceUnit.DeepClone();
        result["schema_version"] = "awake.worldbook.batch-source-unit-public.v2";
        result["source_scope"] = scope;
        result.Remove("owner_id");
        result.Remove("scope_id");
        result.Remove("scope_kind");
        result.Remove("workspace_marker_hash");
        if (scope == "prebatch") result.Remove("batch_id");
        return result;
    }

    public static JsonObject ProjectReviewDecision(JsonObject decision)
    {
        var result = (JsonObject)decision.DeepClone();
        result["schema_version"] = "awake.worldbook.batch-review-decision-public.v2";
        result.Remove("reviewer_owner_id");
        result.Remove("accepted_fact_set_hash");
        return result;
    }

    public static JsonObject ProjectManifest(JsonObject manifest)
    {
        var result = (JsonObject)manifest.DeepClone();
        result["schema_version"] = "awake.worldbook.batch-manifest-public.v2";
        result.Remove("owner_id");
        result.Remove("workspace_marker_hash");
        result.Remove("active_owner_instance_id");
        result.Remove("lease_issued_at");
        result.Remove("lease_expires_at");
        result.Remove("status_reason");
        result.Remove("limits");
        if (result["report_summary"] is JsonObject reportSummary && reportSummary.Count == 0)
            result.Remove("report_summary");
        if (result["provider_selection"] is JsonObject provider) provider.Remove("provider_fingerprint");
        return result;
    }

    public static JsonObject ProjectItem(JsonObject item, JsonObject? result, JsonObject? metadata, JsonObject? sourceUnit, IReadOnlyList<JsonObject> evidence)
    {
        var projected = (JsonObject)item.DeepClone();
        projected["schema_version"] = "awake.worldbook.batch-item-public.v2";
        projected.Remove("owner_id");
        projected.Remove("document_path");
        projected.Remove("last_error");
        projected.Remove("lease");
        projected.Remove("owner_instance_id");
        projected.Remove("fence_token");
        projected.Remove("provider_fingerprint");
        projected.Remove("workspace_marker_hash");
        projected.Remove("raw_content_hash");
        projected.Remove("normalized_content_hash");
        projected.Remove("unit_hash");
        projected.Remove("idempotency_key");
        projected.Remove("reservation_id");
        projected.Remove("active_attempt_stage");
        projected.Remove("last_attempt_stage");
        projected.Remove("accepted_fact_set_hash");
        if (result is not null)
        {
            var publicResult = (JsonObject)result.DeepClone();
            publicResult.Remove("attempt_id");
            publicResult.Remove("created_from_item_revision");
            projected["facts"] = publicResult["facts"]?.DeepClone();
            projected["result_hash"] = publicResult["result_hash"]?.DeepClone();
        }
        if (metadata is not null) projected["metadata"] = ProjectMetadataResult(metadata);
        if (sourceUnit is not null) projected["source_unit"] = ProjectSourceUnit(sourceUnit, "batch");
        projected["evidence"] = new JsonArray(evidence.Select(ProjectEvidence).ToArray());
        return projected;
    }

    internal static JsonObject ProjectMetadataResult(JsonObject metadata)
    {
        var result = (JsonObject)metadata.DeepClone();
        result["schema_version"] = "awake.worldbook.batch-metadata-result-public.v2";
        result.Remove("attempt_id");
        result.Remove("result_hash");
        result.Remove("created_from_item_revision");
        result.Remove("provider_fingerprint");
        return result;
    }

    private static JsonNode ProjectEvidence(JsonObject evidence)
    {
        var result = (JsonObject)evidence.DeepClone();
        result["schema_version"] = "awake.worldbook.batch-evidence-public.v2";
        result.Remove("raw_content_hash");
        result.Remove("normalized_content_hash");
        result.Remove("binding_hash");
        result.Remove("created_by_attempt_id");
        result.Remove("owner_id");
        result.Remove("workspace_marker_hash");
        return result;
    }
}
