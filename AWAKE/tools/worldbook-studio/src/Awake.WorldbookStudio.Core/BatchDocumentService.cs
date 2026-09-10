using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchDocumentCreateInput(
    string ItemId,
    int ExpectedItemRevision,
    IReadOnlyList<string> FactIds,
    JsonObject MetadataSelection);

internal sealed record BatchCreateDocumentsResult(JsonObject Data);

internal sealed class BatchDocumentService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly BatchWorkspaceLayout _layout;
    private readonly WorldbookApplicationService _documents;
    private readonly string _ownerId;

    public BatchDocumentService(WorkspaceService workspace, WorldbookApplicationService documents, string ownerId)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _documents = documents;
        _ownerId = BatchPathValidator.RequireIdentifier(ownerId, "owner_id");
    }

    public BatchCreateDocumentsResult Create(
        string batchId,
        int expectedManifestRevision,
        string createMode,
        IReadOnlyList<BatchDocumentCreateInput> inputs,
        DateTimeOffset? now = null)
    {
        BatchPathValidator.RequireIdentifier(batchId, "batch_id");
        if (createMode != "needs_review") throw new InvalidOperationException("WB-BATCH-STAGE-422: 只能创建 needs_review 作者草稿。");
        if (inputs is null || inputs.Count == 0) throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: 至少选择一条可建档项目。");
        var gate = Locks.GetOrAdd(_layout.BatchRoot(batchId), _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        using var writeLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(batchId));
        try
        {
            var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var manifestPath = _layout.BatchManifest(batchId);
            var manifest = BatchWorkspaceLayout.ReadJson(manifestPath);
            var actualManifestRevision = manifest["revision"]?.GetValue<int>() ?? -1;
            if (actualManifestRevision != expectedManifestRevision)
                throw new InvalidOperationException("WB-BATCH-REVISION-409: 批次已更新，请刷新后重试。");

            var results = new JsonArray();
            var createdCount = 0;
            var failedCount = 0;
            foreach (var input in inputs)
            {
                var result = CreateOne(batchId, input, timestamp);
                results.Add(result);
                if (result["status"]?.GetValue<string>() is "created" or "already_exists") createdCount++;
                if (result["status"]?.GetValue<string>() == "failed") failedCount++;
            }
            manifest["revision"] = expectedManifestRevision + 1;
            manifest["commit_seq"] = (manifest["commit_seq"]?.GetValue<int>() ?? 0) + 1;
            manifest["updated_at"] = timestamp.ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(manifestPath, manifest);
            var data = new JsonObject
            {
                ["schema_version"] = "awake.worldbook.batch-create-result.v2",
                ["batch_id"] = batchId,
                ["manifest_revision"] = manifest["revision"]!.DeepClone(),
                ["created_count"] = createdCount,
                ["failed_count"] = failedCount,
                ["items"] = results,
                ["correlation_id"] = NewId("correlation")
            };
            return new BatchCreateDocumentsResult(data);
        }
        finally
        {
            gate.Release();
        }
    }

    private JsonObject CreateOne(string batchId, BatchDocumentCreateInput input, DateTimeOffset timestamp)
    {
        BatchPathValidator.RequireIdentifier(input.ItemId, "item_id");
        var itemPath = _layout.BatchItem(batchId, input.ItemId);
        var item = BatchWorkspaceLayout.ReadJson(itemPath);
        var actualRevision = item["revision"]?.GetValue<int>() ?? -1;
        if (actualRevision != input.ExpectedItemRevision)
            throw new InvalidOperationException("WB-BATCH-REVISION-409: item revision 已变化，请刷新后重试。");
        var currentSelection = item["metadata_selection"]?.AsObject() ?? throw new InvalidOperationException("WB-BATCH-METADATA-422: 当前项目缺少元数据选择。");
        var selection = NormalizeSelection(input.MetadataSelection);
        if (CanonicalJson.Serialize(currentSelection) != CanonicalJson.Serialize(selection))
            return Failed(input.ItemId, "WB-BATCH-EVIDENCE-422", "提交的元数据选择与当前项目不一致。");

        var resultPath = _layout.BatchItemResult(batchId, input.ItemId);
        var result = BatchWorkspaceLayout.ReadJson(resultPath);
        var facts = result["facts"]?.AsArray().OfType<JsonObject>().ToArray() ?? throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: 当前项目缺少事实结果。");
        var factIds = input.FactIds.Distinct(StringComparer.Ordinal).ToArray();
        if (factIds.Length == 0 || factIds.Any(id => !facts.Any(fact => fact?["fact_id"]?.GetValue<string>() == id)))
            return Failed(input.ItemId, "WB-BATCH-EVIDENCE-422", "提交的事实选择不属于当前项目。");
        var accepted = item["accepted_fact_ids"]?.AsArray().Select(id => id!.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
        if (factIds.Any(id => !accepted.Contains(id)))
            return Failed(input.ItemId, "WB-BATCH-EVIDENCE-422", "只能把已人工接受的事实写入作者草稿。");
        var acceptedHash = BatchFactReviewRepository.ComputeAcceptedFactSetHash(result, accepted);
        if (item["accepted_fact_set_hash"]?.GetValue<string>() != acceptedHash)
            return Failed(input.ItemId, "WB-BATCH-EVIDENCE-422", "事实结果与当前审核事实集不一致。");
        if (result["source_fact_set_hash"] is not null && result["source_fact_set_hash"]!.GetValue<string>() != acceptedHash)
            return Failed(input.ItemId, "WB-BATCH-EVIDENCE-422", "事实结果与当前审核事实集不一致。");
        var metadataPath = _layout.BatchItemMetadata(batchId, input.ItemId);
        if (!File.Exists(metadataPath)) throw new InvalidOperationException("WB-BATCH-METADATA-422: 当前项目缺少元数据结果。");
        var metadata = BatchWorkspaceLayout.ReadJson(metadataPath);
        if (metadata["source_fact_set_hash"]?.GetValue<string>() != acceptedHash)
            return Failed(input.ItemId, "WB-BATCH-EVIDENCE-422", "元数据结果与当前审核事实集不一致。");

        var inputHash = CanonicalJson.Hash(new JsonObject
        {
            ["batch_id"] = batchId,
            ["item_id"] = input.ItemId,
            ["fact_ids"] = new JsonArray(factIds.OrderBy(id => id, StringComparer.Ordinal).Select(id => JsonValue.Create(id)).ToArray()),
            ["metadata_selection"] = selection.DeepClone()
        });
        var bindingPath = _layout.BatchItemDocument(batchId, input.ItemId);
        if (File.Exists(bindingPath))
        {
            var existing = BatchWorkspaceLayout.ReadJson(bindingPath);
            if (existing["input_hash"]?.GetValue<string>() != inputHash)
                throw new InvalidOperationException("WB-BATCH-IDEMPOTENCY-409: 该项目已经使用不同内容建档。");
            var existingStatus = existing["status"]?.GetValue<string>();
            if (existingStatus == "created") return new JsonObject { ["item_id"] = input.ItemId, ["status"] = "already_exists", ["document_id"] = existing["document_id"]!.DeepClone() };
            if (existingStatus == "creating")
            {
                if (existing["document_id"] is not null)
                {
                    existing["status"] = "created";
                    existing["recovery_status"] = "recovered";
                    BatchWorkspaceLayout.WriteJsonAtomic(bindingPath, existing);
                    return new JsonObject { ["item_id"] = input.ItemId, ["status"] = "already_exists", ["document_id"] = existing["document_id"]!.DeepClone() };
                }
                existing["status"] = "quarantined";
                existing["recovery_status"] = "manual_reconciliation_required";
                existing["updated_at"] = timestamp.ToString("O");
                BatchWorkspaceLayout.WriteJsonAtomic(bindingPath, existing);
                return Failed(input.ItemId, "WB-BATCH-DOCUMENT-UNKNOWN-409", "上次建档结果无法确认，已隔离；请检查作者档案后再处理。" );
            }
            if (existingStatus == "quarantined")
                return Failed(input.ItemId, "WB-BATCH-DOCUMENT-UNKNOWN-409", "该项目处于建档结果待人工核对状态。" );
        }
        if (item["status"]?.GetValue<string>() != "ready_to_create" || item["review_status"]?.GetValue<string>() != "accepted")
            return Failed(input.ItemId, "WB-BATCH-STAGE-422", "项目尚未完成事实审核和元数据确认。");
        var binding = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-document-binding.v1",
            ["batch_id"] = batchId,
            ["item_id"] = input.ItemId,
            ["input_hash"] = inputHash,
            ["status"] = "creating",
            ["created_at"] = timestamp.ToString("O"),
            ["owner_id"] = _ownerId
        };
        BatchWorkspaceLayout.WriteJsonAtomic(bindingPath, binding);
        try
        {
            var attempt = BatchWorkspaceLayout.ReadJson(_layout.BatchItemAttempt(batchId, input.ItemId, result["attempt_id"]!.GetValue<string>()));
            var draftFacts = facts
                .Where(fact => fact is not null && factIds.Contains(fact["fact_id"]!.GetValue<string>()))
                .Select(fact => BuildDraftFact(batchId, item, result, fact!))
                .ToArray();
            var candidateBody = new JsonObject
            {
                ["facts"] = new JsonArray(draftFacts.Select(AuthoringDraftRequestFactory.SerializeFact).ToArray()!),
                ["source_content_hash"] = item["normalized_content_hash"]!.DeepClone(),
                ["result_hash"] = result["result_hash"]!.DeepClone()
            };
            var candidateFingerprint = CanonicalJson.Hash(candidateBody);
            var packetHash = attempt["packet_hash"]?.GetValue<string>()
                ?? throw new InvalidOperationException("WB-BATCH-DOCUMENT-422: 当前 attempt 缺少 canonical packet hash。");
            if (packetHash.Length != 64 || packetHash.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidOperationException("WB-BATCH-DOCUMENT-422: 当前 attempt 的 packet hash 无效。");
            var packet = attempt["packet"] as JsonObject
                ?? throw new InvalidOperationException("WB-BATCH-DOCUMENT-422: 当前 attempt 缺少 packet 输入快照。");
            if (!string.Equals(packet["packet_hash"]?.GetValue<string>(), packetHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(AuthoringLifecycleFactory.RecomputePacketHash(packet), packetHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(packet["source_snapshot_id"]?.GetValue<string>(), item["source_snapshot_id"]?.GetValue<string>(), StringComparison.Ordinal)
                || !string.Equals(packet["source_content_hash"]?.GetValue<string>(), item["normalized_content_hash"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-BATCH-DOCUMENT-409: canonical packet 与当前项目输入不一致，请重新执行本项目。");
            var candidateSet = new AuthoringCandidateSet(
                "batch." + input.ItemId,
                item["source_snapshot_id"]!.GetValue<string>(),
                item["normalized_content_hash"]!.GetValue<string>(),
                packetHash,
                attempt["provider_fingerprint"]!.GetValue<string>(),
                "succeeded",
                [new AuthoringCandidate(
                    "candidate." + candidateFingerprint[..24],
                    candidateFingerprint,
                    item["source_snapshot_id"]!.GetValue<string>(),
                    item["normalized_content_hash"]!.GetValue<string>(),
                     packetHash,
                    attempt["provider_fingerprint"]!.GetValue<string>(),
                    draftFacts,
                    new AuthoringDraftMetadata(
                        selection["title"]!.GetValue<string>(),
                        selection["summary"]!.GetValue<string>(),
                        selection["domain"]!.GetValue<string>(),
                        selection["subdomain"]!.GetValue<string>(),
                        [],
                        null),
                     [],
                     "accepted",
                     Coverage: new JsonObject
                     {
                         ["mode"] = "batch_facts_only",
                         ["status"] = "not_evaluated",
                         ["heuristic"] = true,
                         ["not_semantic_migration_proof"] = true
                     })]);
            var created = _documents.CreateGeneratedDocumentFromDraft(
                selection["title"]!.GetValue<string>(),
                selection["summary"]!.GetValue<string>(),
                AuthoringProjection.MapDomain(selection["domain"]!.GetValue<string>()),
                "batch." + input.ItemId,
                draftFacts,
                [],
                "base",
                "author.developer",
                candidateSet);
            var documentId = created.Document["id"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-BATCH-DOCUMENT-500: 作者草稿缺少 document_id。");
            binding["status"] = "created";
            binding["document_id"] = documentId;
            binding["updated_at"] = timestamp.ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(bindingPath, binding);
            item["status"] = "created";
            item["status_reason"] = "needs_review_document_created";
            item["document_id"] = documentId;
            item["revision"] = input.ExpectedItemRevision + 1;
            item["updated_at"] = timestamp.ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(itemPath, item);
            return new JsonObject { ["item_id"] = input.ItemId, ["status"] = "created", ["document_id"] = documentId };
        }
        catch (InvalidOperationException error)
        {
            binding["status"] = "failed";
            binding["error_code"] = "WB-BATCH-DOCUMENT-500";
            binding["failure_detail"] = error.Message.Split(':', 2)[0].Trim();
            binding["message"] = "作者草稿创建失败，请检查工作区后重试。";
            binding["updated_at"] = timestamp.ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(bindingPath, binding);
            return Failed(input.ItemId, "WB-BATCH-DOCUMENT-500", "作者草稿创建失败，请检查工作区后重试。");
        }
    }

    private AuthoringDraftFact BuildDraftFact(string batchId, JsonObject item, JsonObject result, JsonObject fact)
    {
        var evidenceGroup = new List<AuthoringDraftEvidence>();
        var evidenceIds = fact["evidence_ids"]?.AsArray()
            .Select(value => value?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray() ?? [];
        foreach (var evidenceId in evidenceIds)
        {
            var sidecarPath = _layout.BatchEvidence(batchId, evidenceId);
            if (!File.Exists(sidecarPath))
                throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: 建档所需 evidence sidecar 缺失。");
            var sidecar = BatchWorkspaceLayout.ReadJson(sidecarPath);
            ValidateEvidenceSidecar(batchId, item, result, fact, evidenceId, sidecar);
                evidenceGroup.Add(new AuthoringDraftEvidence(
                    evidenceId,
                    CanonicalJson.Serialize(sidecar["locator"]!),
                    sidecar["quote"]!.GetValue<string>(),
                    sidecar["quote_hash"]!.GetValue<string>(),
                    sidecar["locator"]!.AsObject().DeepClone() as JsonObject));
        }
        if (evidenceIds.Length > 0 && evidenceGroup.Count != evidenceIds.Length)
            throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: 建档 evidence group 不完整。");
        return new AuthoringDraftFact(
            fact["fact_id"]!.GetValue<string>(),
            fact["kind"]!.GetValue<string>(),
            fact["text"]!.GetValue<string>(),
            fact["certainty"]!.GetValue<string>(),
            false,
            evidenceGroup.FirstOrDefault(),
            "accepted",
            evidenceGroup);
    }

    private static void ValidateEvidenceSidecar(
        string batchId,
        JsonObject item,
        JsonObject result,
        JsonObject fact,
        string evidenceId,
        JsonObject sidecar)
    {
        var locator = sidecar["locator"]?.AsObject()
            ?? throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: evidence locator 缺失。");
        var quote = sidecar["quote"]?.GetValue<string>()
            ?? throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: evidence quote 缺失。");
        var quoteHash = sidecar["quote_hash"]?.GetValue<string>()
            ?? throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: evidence quote_hash 缺失。");
        var locatorHash = sidecar["locator_hash"]?.GetValue<string>()
            ?? throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: evidence locator_hash 缺失。");
        if (sidecar["evidence_id"]?.GetValue<string>() != evidenceId
            || sidecar["batch_id"]?.GetValue<string>() != batchId
            || sidecar["item_id"]?.GetValue<string>() != item["item_id"]?.GetValue<string>()
            || sidecar["result_ref"]?.GetValue<string>() != result["result_id"]?.GetValue<string>()
            || sidecar["fact_id"]?.GetValue<string>() != fact["fact_id"]?.GetValue<string>()
            || sidecar["source_snapshot_id"]?.GetValue<string>() != item["source_snapshot_id"]?.GetValue<string>()
            || sidecar["normalized_content_hash"]?.GetValue<string>() != item["normalized_content_hash"]?.GetValue<string>()
            || !string.Equals(Hashing.Sha256Text(quote), quoteHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(CanonicalJson.Hash(locator), locatorHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: evidence sidecar 与当前建档结果不匹配。");
        var expectedBindingHash = CanonicalJson.Hash(new JsonObject
        {
            ["batch_id"] = batchId,
            ["item_id"] = item["item_id"]!.GetValue<string>(),
            ["normalized_content_hash"] = item["normalized_content_hash"]!.GetValue<string>(),
            ["quote_hash"] = quoteHash,
            ["raw_content_hash"] = item["raw_content_hash"]!.GetValue<string>(),
            ["result_ref"] = result["result_id"]!.GetValue<string>(),
            ["source_snapshot_id"] = item["source_snapshot_id"]!.GetValue<string>(),
            ["locator_hash"] = locatorHash
        });
        if (!string.Equals(sidecar["binding_hash"]?.GetValue<string>(), expectedBindingHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: evidence binding_hash 不匹配。");
    }
    private static JsonObject NormalizeSelection(JsonObject input)
    {
        var title = input["title"]?.GetValue<string>()?.Trim() ?? string.Empty;
        var summary = input["summary"]?.GetValue<string>()?.Trim() ?? string.Empty;
        var domain = input["domain"]?.GetValue<string>()?.Trim().ToLowerInvariant() ?? string.Empty;
        var subdomain = input["subdomain"]?.GetValue<string>()?.Trim() ?? string.Empty;
        if (title.Length == 0 || summary.Length == 0 || subdomain.Length == 0 || domain.Length == 0)
            throw new InvalidOperationException("WB-BATCH-METADATA-422: 元数据选择不完整。");
        return new JsonObject
        {
            ["title"] = title,
            ["summary"] = summary,
            ["domain"] = domain,
            ["subdomain"] = subdomain,
            ["metadata_selection_hash"] = input["metadata_selection_hash"]?.DeepClone() ?? CanonicalJson.Hash(new JsonObject { ["domain"] = domain, ["subdomain"] = subdomain, ["summary"] = summary, ["title"] = title })
        };
    }

    private static JsonObject Failed(string itemId, string code, string message)
        => new() { ["item_id"] = itemId, ["status"] = "failed", ["error_code"] = code, ["message"] = message };

    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
}
