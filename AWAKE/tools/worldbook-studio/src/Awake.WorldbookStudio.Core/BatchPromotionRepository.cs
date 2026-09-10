using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed record BatchCreateRequest(
    string ScanId,
    string ExpectedScanHash,
    string PipelineRevision,
    string ProviderId,
    JsonObject ModelParameters,
    string IdempotencyKey);

internal sealed record BatchCreateResult(
    JsonObject PublicManifest,
    JsonObject Reservation,
    JsonObject PromotionJournal);

internal sealed class BatchCreateRepository
{
    private const string PipelineRevision = "batch-authoring.v1";
    private const string SplitterRevision = "splitter.v1";
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, object> ReservationLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly BatchWorkspaceLayout _layout;
    private readonly BatchAuthoringContractRegistry _contract;
    private readonly string _ownerId;
    private readonly string _ownerInstanceId;
    private readonly string _workspaceMarkerHash;
    private readonly Action<string>? _afterPhase;

    public BatchCreateRepository(
        WorkspaceService workspace,
        BatchAuthoringContractRegistry contract,
        string ownerId,
        string ownerInstanceId,
        string workspaceMarkerHash,
        Action<string>? afterPhase = null)
    {
        _layout = new BatchWorkspaceLayout(workspace.Policy);
        _contract = contract;
        _ownerId = BatchPathValidator.RequireIdentifier(ownerId, "owner_id");
        _ownerInstanceId = BatchPathValidator.RequireIdentifier(ownerInstanceId, "owner_instance_id");
        RequireHash(workspaceMarkerHash, "workspace_marker_hash");
        _workspaceMarkerHash = workspaceMarkerHash.ToUpperInvariant();
        _afterPhase = afterPhase;
        _ = _contract.RequireSchema("create_reservation");
        _ = _contract.RequireSchema("promotion_journal");
        _ = _contract.RequireSchema("manifest");
        _ = _contract.RequireSchema("item");
    }

    public BatchCreateResult Create(BatchCreateRequest request, string providerFingerprint, DateTimeOffset? now = null)
    {
        ValidateRequest(request, providerFingerprint);
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var inputHash = ComputeInputHash(request, providerFingerprint);
        var lockKey = _layout.Reservation(request.IdempotencyKey);
        var gate = ReservationLocks.GetOrAdd(lockKey, _ => new object());
        lock (gate)
        {
            using var writeLease = BatchWorkspaceWriteLease.Acquire(lockKey);
            var reservation = LoadOrCreateReservation(request, providerFingerprint, inputHash, timestamp);
            if (reservation["canonical_input_hash"]?.GetValue<string>() != inputHash)
                throw new InvalidOperationException("WB-BATCH-IDEMPOTENCY-409: 相同幂等键对应了不同的创建参数。");
            return ContinuePromotion(reservation, request, providerFingerprint, timestamp);
        }
    }

    public JsonObject ReadReservation(string idempotencyKey)
        => BatchWorkspaceLayout.ReadJson(_layout.Reservation(BatchPathValidator.RequireIdentifier(idempotencyKey, "idempotency_key")));

    public JsonObject ReadPromotionJournal(string scanId, string operationId)
        => BatchWorkspaceLayout.ReadJson(_layout.PromotionJournal(
            BatchPathValidator.RequireIdentifier(scanId, "scan_id"),
            BatchPathValidator.RequireIdentifier(operationId, "operation_id")));

    public JsonObject ReadManifest(string batchId)
        => BatchWorkspaceLayout.ReadJson(_layout.BatchManifest(BatchPathValidator.RequireIdentifier(batchId, "batch_id")));

    private JsonObject LoadOrCreateReservation(BatchCreateRequest request, string providerFingerprint, string inputHash, DateTimeOffset timestamp)
    {
        var path = _layout.Reservation(request.IdempotencyKey);
        if (File.Exists(path))
        {
            var existing = BatchWorkspaceLayout.ReadJson(path);
            ValidateExistingReservation(existing, request, inputHash, timestamp);
            return existing;
        }

        var scan = BatchWorkspaceLayout.ReadJson(_layout.ScanManifest(request.ScanId));
        if (scan["status"]?.GetValue<string>() != "ready")
            throw new InvalidOperationException("WB-BATCH-SCAN-409: 预批次尚未处于可创建状态。");
        if (scan["scan_hash"]?.GetValue<string>() != request.ExpectedScanHash)
            throw new InvalidOperationException("WB-BATCH-SCAN-409: 预批次扫描哈希已变化。");
        var promotion = scan["promotion"]?.AsObject() ?? throw new InvalidOperationException("WB-BATCH-SCAN-409: 预批次缺少 promotion 信息。");
        var operationId = promotion["operation_id"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-BATCH-SCAN-409: 预批次缺少 operation_id。");

        var reservation = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.batch-create-reservation.v2",
            ["idempotency_key"] = request.IdempotencyKey,
            ["reservation_id"] = NewId("reservation"),
            ["canonical_input_hash"] = inputHash,
            ["scan_id"] = request.ScanId,
            ["expected_scan_hash"] = request.ExpectedScanHash,
            ["pipeline_revision"] = request.PipelineRevision,
            ["provider_selection"] = ProviderSelection(request.ProviderId, providerFingerprint, request.ModelParameters),
            ["owner_id"] = _ownerId,
            ["owner_instance_id"] = _ownerInstanceId,
            ["operation_id"] = operationId,
            ["fence_token"] = NewHash(),
            ["claim_expires_at"] = timestamp.Add(ClaimLifetime).ToString("O"),
            ["state"] = "reserved",
            ["reservation_revision"] = 0,
            ["created_at"] = timestamp.ToString("O"),
            ["updated_at"] = timestamp.ToString("O"),
            ["allocation_id"] = NewId("allocation"),
            ["claim_generation"] = 0
        };
        try
        {
            BatchAtomicFile.WriteCreateOnly(path, Encoding.UTF8.GetBytes(CanonicalJson.Serialize(reservation) + "\n"));
            return reservation;
        }
        catch (IOException) when (File.Exists(path))
        {
            var existing = BatchWorkspaceLayout.ReadJson(path);
            ValidateExistingReservation(existing, request, inputHash, timestamp);
            return existing;
        }
    }

    private void ValidateExistingReservation(JsonObject reservation, BatchCreateRequest request, string inputHash, DateTimeOffset timestamp)
    {
        if (reservation["canonical_input_hash"]?.GetValue<string>() != inputHash)
            throw new InvalidOperationException("WB-BATCH-IDEMPOTENCY-409: 相同幂等键对应了不同的创建参数。");
        var state = reservation["state"]?.GetValue<string>() ?? string.Empty;
        if (state is "committed") return;
        if (state is "failed") throw new InvalidOperationException("WB-BATCH-PROMOTION-409: 该创建 reservation 已失败，不能自动重放。");
        if (state is "needs_reconcile") throw new InvalidOperationException("WB-BATCH-PROMOTION-RECONCILE-409: 该创建 reservation 需要先完成恢复协调。");

        var owner = reservation["owner_instance_id"]?.GetValue<string>();
        var expiresAt = ParseTimestamp(reservation["claim_expires_at"], "claim_expires_at");
        if (!string.Equals(owner, _ownerInstanceId, StringComparison.Ordinal) || expiresAt <= timestamp)
            throw new InvalidOperationException("WB-BATCH-IDEMPOTENCY-INFLIGHT-409: 该创建任务仍由其他租约持有，请稍后重试或先接管租约。");
    }

    private BatchCreateResult ContinuePromotion(JsonObject reservation, BatchCreateRequest request, string providerFingerprint, DateTimeOffset timestamp)
    {
        var batchId = reservation["batch_id"]?.GetValue<string>();
        if (reservation["state"]?.GetValue<string>() == "committed")
        {
            if (string.IsNullOrWhiteSpace(batchId)) throw new InvalidOperationException("WB-BATCH-PROMOTION-409: committed reservation 缺少 batch_id。");
            return ResultFromCommitted(reservation, batchId);
        }

        if (string.IsNullOrWhiteSpace(batchId))
        {
            reservation = CasReservation(reservation, "reserved", "allocation_pending", timestamp, null);
            batchId = "batch." + Hashing.Sha256Text(reservation["allocation_id"]!.GetValue<string>()).Substring(0, 32).ToLowerInvariant();
            reservation["batch_id"] = batchId;
            reservation["reservation_revision"] = reservation["reservation_revision"]!.GetValue<int>() + 1;
            reservation["updated_at"] = timestamp.ToString("O");
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.Reservation(request.IdempotencyKey), reservation);
            InvokePhase("allocation_persisted");
        }
        using var batchLease = BatchWorkspaceWriteLease.Acquire(_layout.BatchRoot(batchId!));

        var operationId = reservation["operation_id"]!.GetValue<string>();
        var journalPath = _layout.PromotionJournal(request.ScanId, operationId);
        var journal = File.Exists(journalPath)
            ? BatchWorkspaceLayout.ReadJson(journalPath)
            : CreatePreparedJournal(reservation, request, batchId!, timestamp);
        if (!File.Exists(journalPath))
        {
            BatchWorkspaceLayout.WriteJsonAtomic(journalPath, journal);
            InvokePhase("prepared");
        }

        if (reservation["state"]?.GetValue<string>() == "allocation_pending")
        {
            reservation = CasReservation(reservation, "allocation_pending", "promoting", timestamp, batchId);
            InvokePhase("promoting");
        }

        journal = AdvancePromotion(journal, reservation, request, batchId!, timestamp);
        return ResultFromCommitted(reservation, batchId!);
    }

    private JsonObject AdvancePromotion(JsonObject journal, JsonObject reservation, BatchCreateRequest request, string batchId, DateTimeOffset timestamp)
    {
        var state = journal["state"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-BATCH-PROMOTION-409: promotion journal 缺少 state。");
        var journalPath = _layout.PromotionJournal(request.ScanId, journal["operation_id"]!.GetValue<string>());
        if (state == "prepared")
        {
            journal = AdvanceJournal(journal, "copying", timestamp);
            BatchWorkspaceLayout.WriteJsonAtomic(journalPath, journal);
            InvokePhase("copying");
            state = "copying";
        }

        if (state == "copying")
        {
            EnsureTempBatch(reservation, request, batchId, timestamp);
            var manifest = BatchWorkspaceLayout.ReadJson(_layout.BatchTempManifest(batchId));
            var manifestHash = ContractHashing.ManifestHash(manifest);
            journal = AdvanceJournal(journal, "verified", timestamp, manifestHash);
            BatchWorkspaceLayout.WriteJsonAtomic(journalPath, journal);
            InvokePhase("verified");
            state = "verified";
        }

        var targetManifestHash = journal["target_manifest_hash"]?.GetValue<string>()
            ?? throw new InvalidOperationException("WB-BATCH-PROMOTION-409: promotion journal 缺少 target_manifest_hash。");
        if (state == "verified")
        {
            VerifyTempBatch(batchId, targetManifestHash);
            MoveTempBatch(batchId, targetManifestHash);
            journal = AdvanceJournal(journal, "renamed", timestamp, targetManifestHash);
            BatchWorkspaceLayout.WriteJsonAtomic(journalPath, journal);
            InvokePhase("renamed");
            state = "renamed";
        }

        if (state == "renamed")
        {
            VerifyTargetBatch(batchId, targetManifestHash);
            journal = AdvanceJournal(journal, "committed", timestamp, targetManifestHash);
            BatchWorkspaceLayout.WriteJsonAtomic(journalPath, journal);
            InvokePhase("committed");
            state = "committed";
        }

        if (state != "committed") throw new InvalidOperationException("WB-BATCH-PROMOTION-409: promotion journal 未进入 committed。");
        var currentReservation = BatchWorkspaceLayout.ReadJson(_layout.Reservation(request.IdempotencyKey));
        if (currentReservation["state"]?.GetValue<string>() != "committed")
        {
            currentReservation = CasReservation(currentReservation, "promoting", "committed", timestamp, batchId);
            InvokePhase("reservation_committed");
        }
        UpdateScanProjection(request, batchId, targetManifestHash, journal["operation_id"]!.GetValue<string>(), timestamp);
        return journal;
    }

    private JsonObject CreatePreparedJournal(JsonObject reservation, BatchCreateRequest request, string batchId, DateTimeOffset timestamp)
        => new()
        {
            ["schema_version"] = "awake.worldbook.batch-promotion-journal.v2",
            ["operation_id"] = reservation["operation_id"]!.GetValue<string>(),
            ["scan_id"] = request.ScanId,
            ["batch_id"] = batchId,
            ["idempotency_key"] = request.IdempotencyKey,
            ["reservation_revision"] = reservation["reservation_revision"]!.GetValue<int>(),
            ["fence_token"] = reservation["fence_token"]!.GetValue<string>(),
            ["owner_instance_id"] = reservation["owner_instance_id"]!.GetValue<string>(),
            ["reservation_id"] = reservation["reservation_id"]!.GetValue<string>(),
            ["phase_seq"] = 0,
            ["state"] = "prepared",
            ["prebatch_path"] = $"prebatches/{request.ScanId}",
            ["temp_path"] = $"batches/{batchId}.tmp",
            ["source_scan_hash"] = request.ExpectedScanHash,
            ["created_at"] = timestamp.ToString("O"),
            ["updated_at"] = timestamp.ToString("O"),
            ["claim_generation"] = reservation["claim_generation"]!.GetValue<int>()
        };

    private JsonObject CasReservation(JsonObject expected, string fromState, string toState, DateTimeOffset timestamp, string? batchId)
    {
        var path = _layout.Reservation(expected["idempotency_key"]!.GetValue<string>());
        var current = BatchWorkspaceLayout.ReadJson(path);
        RequireReservationMatch(current, expected);
        if (current["state"]?.GetValue<string>() != fromState)
            throw new InvalidOperationException("WB-BATCH-CLAIM-409: reservation 状态已被其他操作改变。");
        current["state"] = toState;
        if (!string.IsNullOrWhiteSpace(batchId)) current["batch_id"] = batchId;
        current["reservation_revision"] = current["reservation_revision"]!.GetValue<int>() + 1;
        current["updated_at"] = timestamp.ToString("O");
        BatchWorkspaceLayout.WriteJsonAtomic(path, current);
        return current;
    }

    private static void RequireReservationMatch(JsonObject current, JsonObject expected)
    {
        foreach (var field in new[] { "reservation_id", "operation_id", "owner_instance_id", "fence_token", "claim_generation", "reservation_revision" })
        {
            if (!JsonEquals(current[field], expected[field]))
                throw new InvalidOperationException("WB-BATCH-CLAIM-409: reservation fence 或 revision 不匹配。");
        }
    }

    private static bool JsonEquals(JsonNode? left, JsonNode? right)
        => left is null && right is null
            || left is not null && right is not null && CanonicalJson.Serialize(left) == CanonicalJson.Serialize(right);

    private void EnsureTempBatch(JsonObject reservation, BatchCreateRequest request, string batchId, DateTimeOffset timestamp)
    {
        var tempRoot = _layout.BatchTempRoot(batchId);
        if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        Directory.CreateDirectory(tempRoot);
        WorkspacePathPolicy.EnsureNoReparsePoint(tempRoot);

        var scan = BatchWorkspaceLayout.ReadJson(_layout.ScanManifest(request.ScanId));
        if (scan["scan_hash"]?.GetValue<string>() != request.ExpectedScanHash)
            throw new InvalidOperationException("WB-BATCH-SCAN-409: 预批次扫描内容已变化，不能晋升。");
        var itemIds = new JsonArray();
        foreach (var snapshotIdNode in scan["snapshot_ids"]!.AsArray())
        {
            var snapshotId = snapshotIdNode!.GetValue<string>();
            var sourceMeta = BatchWorkspaceLayout.ReadJson(_layout.SnapshotMeta(request.ScanId, snapshotId));
            var sourceText = File.ReadAllBytes(_layout.SnapshotText(request.ScanId, snapshotId));
            var snapshot = (JsonObject)sourceMeta.DeepClone();
            snapshot["scope_id"] = batchId;
            snapshot["scope_kind"] = "batch";
            snapshot["batch_id"] = batchId;
            snapshot["normalized_text_ref"] = $"batches/{batchId}/sources/{snapshotId}.txt";
            snapshot["source_text_path"] = $"batches/{batchId}/sources/{snapshotId}.txt";
            BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchTempSnapshotMeta(batchId, snapshotId), snapshot);
            BatchAtomicFile.Write(_layout.BatchTempSnapshotText(batchId, snapshotId), sourceText);

            foreach (var unitIdNode in sourceMeta["unit_ids"]!.AsArray())
            {
                var unitId = unitIdNode!.GetValue<string>();
                var sourceUnit = BatchWorkspaceLayout.ReadJson(_layout.SourceUnit(request.ScanId, snapshotId, unitId));
                var unit = (JsonObject)sourceUnit.DeepClone();
                unit["scope_id"] = batchId;
                unit["scope_kind"] = "batch";
                unit["batch_id"] = batchId;
                BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchTempSourceUnit(batchId, snapshotId, unitId), unit);

                var itemId = "item." + Hashing.Sha256Text(batchId + ":" + unitId).Substring(0, 32).ToLowerInvariant();
                itemIds.Add(itemId);
                var item = CreateQueuedItem(batchId, itemId, request, sourceMeta, unit, timestamp);
                BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchTempItem(batchId, itemId), item);
            }
        }

        var manifest = CreateManifest(batchId, request, reservation, itemIds, timestamp);
        BatchWorkspaceLayout.WriteJsonAtomic(_layout.BatchTempManifest(batchId), manifest);
    }

    private JsonObject CreateQueuedItem(string batchId, string itemId, BatchCreateRequest request, JsonObject snapshot, JsonObject unit, DateTimeOffset timestamp)
        => new()
        {
            ["schema_version"] = "awake.worldbook.batch-item.v2",
            ["batch_id"] = batchId,
            ["item_id"] = itemId,
            ["owner_id"] = _ownerId,
            ["revision"] = 0,
            ["status"] = "queued",
            ["recovery_status"] = "clean",
            ["status_reason"] = "created",
            ["source_snapshot_id"] = snapshot["snapshot_id"]!.GetValue<string>(),
            ["source_unit_id"] = unit["unit_id"]!.GetValue<string>(),
            ["raw_content_hash"] = snapshot["raw_content_hash"]!.GetValue<string>(),
            ["normalized_content_hash"] = unit["normalized_content_hash"]!.GetValue<string>(),
            ["unit_hash"] = unit["unit_hash"]!.GetValue<string>(),
            ["attempt_count"] = 0,
            ["idempotency_key"] = Hashing.Sha256Text(batchId + ":item:" + unit["unit_id"]!.GetValue<string>()),
            ["review_status"] = "unreviewed",
            ["created_at"] = timestamp.ToString("O"),
            ["updated_at"] = timestamp.ToString("O"),
            ["correlation_id"] = NewId("correlation"),
            ["result_stage"] = "none",
            ["active_attempt_stage"] = "none"
        };

    private JsonObject CreateManifest(string batchId, BatchCreateRequest request, JsonObject reservation, JsonArray itemIds, DateTimeOffset timestamp)
        => new()
        {
            ["schema_version"] = "awake.worldbook.batch-manifest.v2",
            ["batch_id"] = batchId,
            ["owner_id"] = _ownerId,
            ["revision"] = 0,
            ["commit_seq"] = 0,
            ["status"] = "planned",
            ["recovery_status"] = "clean",
            ["pipeline_revision"] = request.PipelineRevision,
            ["splitter_revision"] = SplitterRevision,
            ["item_ids"] = itemIds,
            ["limits"] = Limits(),
            ["provider_selection"] = reservation["provider_selection"]!.DeepClone(),
            ["created_at"] = timestamp.ToString("O"),
            ["updated_at"] = timestamp.ToString("O"),
            ["workspace_marker_hash"] = _workspaceMarkerHash,
            ["claim_generation"] = reservation["claim_generation"]!.GetValue<int>(),
            ["active_owner_instance_id"] = _ownerInstanceId,
            ["source_scan_id"] = request.ScanId,
            ["report_summary"] = new JsonObject()
        };

    private static JsonObject Limits()
        => new()
        {
            ["max_files"] = 200,
            ["max_total_bytes"] = 67_108_864,
            ["max_file_bytes"] = 16_777_216,
            ["max_unit_characters"] = 80_000,
            ["multipart_request_bytes"] = 73_400_320,
            ["local_concurrency"] = 1,
            ["cloud_default_concurrency"] = 2,
            ["cloud_max_concurrency"] = 4,
            ["logical_attempt_deadline_seconds"] = 120,
            ["max_total_attempts"] = 3,
            ["scan_ttl_hours"] = 24
        };

    private void VerifyTempBatch(string batchId, string expectedManifestHash)
    {
        var manifestPath = _layout.BatchTempManifest(batchId);
        if (!File.Exists(manifestPath)) throw new InvalidOperationException("WB-BATCH-PROMOTION-409: 临时批次缺少 manifest。");
        var manifest = BatchWorkspaceLayout.ReadJson(manifestPath);
        if (ContractHashing.ManifestHash(manifest) != expectedManifestHash)
            throw new InvalidOperationException("WB-BATCH-PROMOTION-RECONCILE-409: 临时 manifest 哈希不匹配。");
        if (manifest["batch_id"]?.GetValue<string>() != batchId)
            throw new InvalidOperationException("WB-BATCH-PROMOTION-RECONCILE-409: 临时 manifest 的 batch_id 不匹配。");
    }

    private void MoveTempBatch(string batchId, string expectedManifestHash)
    {
        var tempRoot = _layout.BatchTempRoot(batchId);
        var targetRoot = _layout.BatchRoot(batchId);
        if (Directory.Exists(targetRoot))
        {
            var targetManifest = _layout.BatchManifest(batchId);
            if (File.Exists(targetManifest) && ContractHashing.ManifestHash(BatchWorkspaceLayout.ReadJson(targetManifest)) == expectedManifestHash)
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                return;
            }
            throw new InvalidOperationException("WB-BATCH-PROMOTION-RECONCILE-409: 目标批次已存在但内容不匹配。");
        }
        if (!Directory.Exists(tempRoot)) throw new InvalidOperationException("WB-BATCH-PROMOTION-RECONCILE-409: 记录的临时批次目录不存在。");
        Directory.Move(tempRoot, targetRoot);
    }

    private void VerifyTargetBatch(string batchId, string expectedManifestHash)
    {
        var manifestPath = _layout.BatchManifest(batchId);
        if (!File.Exists(manifestPath)) throw new InvalidOperationException("WB-BATCH-PROMOTION-RECONCILE-409: 目标批次缺少 manifest。");
        var manifest = BatchWorkspaceLayout.ReadJson(manifestPath);
        if (ContractHashing.ManifestHash(manifest) != expectedManifestHash)
            throw new InvalidOperationException("WB-BATCH-PROMOTION-RECONCILE-409: 目标 manifest 哈希不匹配。");
    }

    private JsonObject AdvanceJournal(JsonObject journal, string state, DateTimeOffset timestamp, string? targetManifestHash = null)
    {
        var result = (JsonObject)journal.DeepClone();
        result["state"] = state;
        result["phase_seq"] = result["phase_seq"]!.GetValue<int>() + 1;
        result["updated_at"] = timestamp.ToString("O");
        if (!string.IsNullOrWhiteSpace(targetManifestHash)) result["target_manifest_hash"] = targetManifestHash;
        return result;
    }

    private void UpdateScanProjection(BatchCreateRequest request, string batchId, string targetManifestHash, string operationId, DateTimeOffset timestamp)
    {
        var scanPath = _layout.ScanManifest(request.ScanId);
        var scan = BatchWorkspaceLayout.ReadJson(scanPath);
        var promotion = scan["promotion"]?.AsObject() ?? throw new InvalidOperationException("WB-BATCH-PROMOTION-409: scan 缺少 promotion projection。");
        if (promotion["operation_id"]?.GetValue<string>() != operationId || promotion["source_scan_hash"]?.GetValue<string>() != request.ExpectedScanHash)
            throw new InvalidOperationException("WB-BATCH-PROMOTION-RECONCILE-409: scan projection 与 promotion journal 不匹配。");
        promotion["state"] = "promoted";
        promotion["batch_id"] = batchId;
        promotion["target_manifest_hash"] = targetManifestHash;
        promotion["updated_at"] = timestamp.ToString("O");
        promotion["request_idempotency_key"] = request.IdempotencyKey;
        BatchWorkspaceLayout.WriteJsonAtomic(scanPath, scan);
    }

    private BatchCreateResult ResultFromCommitted(JsonObject reservation, string batchId)
    {
        var currentReservation = BatchWorkspaceLayout.ReadJson(_layout.Reservation(reservation["idempotency_key"]!.GetValue<string>()));
        if (currentReservation["state"]?.GetValue<string>() != "committed")
            throw new InvalidOperationException("WB-BATCH-PROMOTION-409: reservation 尚未 committed。");
        var manifest = ReadManifest(batchId);
        var journal = ReadPromotionJournal(currentReservation["scan_id"]!.GetValue<string>(), currentReservation["operation_id"]!.GetValue<string>());
        if (journal["state"]?.GetValue<string>() != "committed")
            throw new InvalidOperationException("WB-BATCH-PROMOTION-409: promotion journal 尚未 committed。");
        return new BatchCreateResult(ProjectPublicManifest(manifest), currentReservation, journal);
    }

    private static JsonObject ProjectPublicManifest(JsonObject manifest)
        => BatchPublicProjection.ProjectManifest(manifest);

    private static JsonObject ProviderSelection(string providerId, string providerFingerprint, JsonObject modelParameters)
        => new()
        {
            ["provider_id"] = providerId,
            ["provider_fingerprint"] = providerFingerprint,
            ["model_parameters"] = CanonicalJson.Canonicalize(modelParameters)
        };

    private static string ComputeInputHash(BatchCreateRequest request, string providerFingerprint)
        => CanonicalJson.Hash(new JsonObject
        {
            ["scan_id"] = request.ScanId,
            ["expected_scan_hash"] = request.ExpectedScanHash,
            ["pipeline_revision"] = request.PipelineRevision,
            ["provider_id"] = request.ProviderId,
            ["provider_fingerprint"] = providerFingerprint,
            ["model_parameters"] = CanonicalJson.Canonicalize(request.ModelParameters)
        });

    private static void ValidateRequest(BatchCreateRequest request, string providerFingerprint)
    {
        BatchPathValidator.RequireIdentifier(request.ScanId, "scan_id");
        BatchPathValidator.RequireIdentifier(request.PipelineRevision, "pipeline_revision");
        RequireHash(request.ExpectedScanHash, "expected_scan_hash");
        RequireHash(request.IdempotencyKey, "idempotency_key");
        RequireHash(providerFingerprint, "provider_fingerprint");
        if (request.ProviderId is not ("local" or "cloud")) throw new InvalidOperationException("WB-BATCH-PROVIDER-422: Provider 类型无效。");
        if (request.ModelParameters is null) throw new InvalidOperationException("WB-BATCH-PROVIDER-422: model_parameters 不能为空。");
        foreach (var key in new[] { "model", "temperature", "max_output_tokens", "reasoning_effort" })
            if (request.ModelParameters[key] is null) throw new InvalidOperationException($"WB-BATCH-PROVIDER-422: model_parameters 缺少 {key}。");
    }

    private static DateTimeOffset ParseTimestamp(JsonNode? node, string field)
        => node is not null && DateTimeOffset.TryParse(node.GetValue<string>(), out var value)
            ? value.ToUniversalTime()
            : throw new InvalidOperationException($"WB-BATCH-STORE-422: {field} 时间格式无效。");

    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
    private static string NewHash() => Hashing.Sha256Text(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
    private static void RequireHash(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(x => !Uri.IsHexDigit(x)))
            throw new InvalidOperationException($"WB-BATCH-REQUEST-422: {field} 必须是 SHA-256。");
    }
    private void InvokePhase(string phase) => _afterPhase?.Invoke(phase);
}
