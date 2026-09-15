using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

internal sealed class AuthorityGateService
{
    private const string ServerIssuer = "awake.worldbook-studio.authoring-v1";
    private readonly WorkspaceService _workspace;
    private readonly WorldbookApplicationService _application;
    private readonly object _gate = new();
    private readonly string _ownerInstanceId = Environment.ProcessId + ":" + Guid.NewGuid().ToString("N");
    private readonly Action<CompileSettlementFaultPoint>? _compileFaultInjector;
    private string Root => Path.Combine(_workspace.Root, "authoring-v1");
    private string DocumentsRoot => Path.Combine(Root, "document-revisions");
    private string SelectionsRoot => Path.Combine(Root, "selections");
    private string ApprovalsRoot => Path.Combine(Root, "approval-proofs");
    private string CompileProofsRoot => Path.Combine(Root, "compile-proofs");
    private string PublishProofsRoot => Path.Combine(Root, "publish-proofs");
    private string OperationsRoot => Path.Combine(Root, "operations");
    private string CompileReservationsRoot => Path.Combine(OperationsRoot, "reservations");
    private string CompileResultsRoot => Path.Combine(OperationsRoot, "results");
    private string MarkersRoot => Path.Combine(Root, "commit-markers");
    private string JournalPath => Path.Combine(Root, "operation-journal.jsonl");
    private string HeadPath => Path.Combine(Root, "workspace-head.json");
    private string CompileWorkspaceLeasePath => Path.Combine(OperationsRoot, ".compile-workspace-lease");

    public AuthorityGateService(WorkspaceService workspace, WorldbookApplicationService application, Action<CompileSettlementFaultPoint>? compileFaultInjector = null)
    {
        _workspace = workspace;
        _application = application;
        _compileFaultInjector = compileFaultInjector;
    }

    public void Initialize()
    {
        lock (_gate)
        {
            foreach (var path in new[] { Root, DocumentsRoot, SelectionsRoot, ApprovalsRoot, CompileProofsRoot, PublishProofsRoot, OperationsRoot, CompileReservationsRoot, CompileResultsRoot, MarkersRoot })
                Directory.CreateDirectory(path);
            if (!File.Exists(HeadPath))
                WriteAtomic(HeadPath, new JsonObject
                {
                    ["schema_version"] = "awake.worldbook.authoring-v1.head",
                    ["head_revision"] = 0,
                    ["documents"] = new JsonObject()
                });
            using var compileRecoveryLease = TryAcquireCompileWorkspaceLease();
            if (compileRecoveryLease is null) return;
            Recover();
        }
    }

    public AuthorityDocumentRevision RegisterDocument(string operationId, string path)
    {
        RequireId(operationId, "operation_id");
        if (TryReadCommittedOperation(operationId, "document_revision", out var existing))
            return new AuthorityDocumentRevision(existing["document_id"]!.GetValue<string>(), existing["path"]!.GetValue<string>(), existing["revision"]!.GetValue<int>(), existing["content_hash"]!.GetValue<string>(), existing["target"]!.GetValue<string>());
        var document = _workspace.ReadAuthoring(path);
        var documentId = document.Document["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(documentId)) throw new InvalidOperationException("WB-AUTHORITY-DOCUMENT-422: 文档缺少稳定 id。");
        var revision = WorldbookInputNormalization.ReadRevision(document.Document);
        var contentHash = document.Report.InputHash ?? Hashing.Sha256Text(document.Content);
        var record = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring-v1.document-revision",
            ["document_id"] = documentId,
            ["path"] = document.Path,
            ["revision"] = revision,
            ["content_hash"] = contentHash,
            ["content"] = document.Content,
            ["registered_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["immutable"] = true
        };
        var recordPath = Path.Combine(DocumentsRoot, SafeId(documentId), $"{revision}.json");
        var headEntry = new JsonObject
        {
            ["document_id"] = documentId,
            ["path"] = document.Path,
            ["revision"] = revision,
            ["content_hash"] = contentHash,
            ["record_path"] = Relative(recordPath)
        };
        record = Commit(operationId, "document_revision", recordPath, record, headEntry);
        return new AuthorityDocumentRevision(documentId, document.Path, revision, contentHash, Relative(recordPath));
    }

    public AuthoritySelectionSnapshot MaterializeSelection(string operationId, IReadOnlyList<string> documentIds)
    {
        RequireId(operationId, "operation_id");
        if (TryReadCommittedOperation(operationId, "selection_snapshot", out var existing))
            return new AuthoritySelectionSnapshot(existing["selection_id"]!.GetValue<string>(), existing["selection_hash"]!.GetValue<string>(), existing["items"]!.AsArray().Count);
        if (documentIds.Count == 0) throw new InvalidOperationException("WB-AUTHORITY-SELECTION-422: selection 不得为空。");
        var head = ReadJson(HeadPath);
        var items = new JsonArray();
        foreach (var documentId in documentIds.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            var item = head["documents"]?[documentId] as JsonObject ?? throw new InvalidOperationException($"WB-AUTHORITY-SELECTION-404: 未登记文档 {documentId}。");
            items.Add(item.DeepClone());
        }
        var selectionId = $"selection.{Guid.NewGuid():N}";
        var selection = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring-v1.selection-snapshot",
            ["selection_id"] = selectionId,
            ["items"] = items,
            ["selection_hash"] = Hashing.Sha256Text(CanonicalJson.Serialize(items)),
            ["immutable"] = true,
            ["created_at"] = DateTimeOffset.UtcNow.ToString("O")
        };
        selection = Commit(operationId, "selection_snapshot", Path.Combine(SelectionsRoot, SafeId(selectionId) + ".json"), selection, null);
        selectionId = selection["selection_id"]!.GetValue<string>();
        return new AuthoritySelectionSnapshot(selectionId, selection["selection_hash"]!.GetValue<string>(), selection["items"]!.AsArray().Count);
    }

    public AuthorityApprovalProof ApproveSelection(string operationId, string selectionId)
    {
        RequireId(operationId, "operation_id");
        if (TryReadCommittedOperation(operationId, "approval_proof", out var existing))
            return new AuthorityApprovalProof(existing["approval_id"]!.GetValue<string>(), existing["proof_hash"]!.GetValue<string>(), existing["selection_id"]!.GetValue<string>());
        var selection = ReadJson(Path.Combine(SelectionsRoot, SafeId(selectionId) + ".json"));
        var proofId = $"approval.{Guid.NewGuid():N}";
        var proof = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring-v1.approval-proof",
            ["approval_id"] = proofId,
            ["selection_id"] = selection["selection_id"]!.GetValue<string>(),
            ["selection_hash"] = selection["selection_hash"]!.GetValue<string>(),
            ["items"] = selection["items"]!.DeepClone(),
            ["approved"] = true,
            ["issuer"] = ServerIssuer,
            ["approved_at"] = DateTimeOffset.UtcNow.ToString("O")
        };
        proof["proof_hash"] = HashWithout(proof, "proof_hash");
        proof = Commit(operationId, "approval_proof", Path.Combine(ApprovalsRoot, SafeId(proofId) + ".json"), proof, null);
        proofId = proof["approval_id"]!.GetValue<string>();
        return new AuthorityApprovalProof(proofId, proof["proof_hash"]!.GetValue<string>(), proof["selection_id"]!.GetValue<string>());
    }

    public AuthorityCompileProof IssueCompileProof(string operationId, string approvalId, string contentTier)
    {
        RequireId(operationId, "operation_id");
        if (contentTier is not ("base" or "adult_optional")) throw new InvalidOperationException("WB-AUTHORITY-TIER-422: content tier 无效。");
        var approval = ReadJson(Path.Combine(ApprovalsRoot, SafeId(approvalId) + ".json"));
        if (approval["approved"]?.GetValue<bool>() != true) throw new InvalidOperationException("WB-AUTHORITY-APPROVAL-409: approval proof 未批准。");
        var selectionId = approval["selection_id"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-AUTHORITY-PROOF-409: approval proof 缺少 selection_id。");
        var selection = ReadJson(Path.Combine(SelectionsRoot, SafeId(selectionId) + ".json"));
        if (selection["selection_id"]?.GetValue<string>() != selectionId
            || Hashing.Sha256Text(CanonicalJson.Serialize(selection["items"] ?? new JsonArray())) != selection["selection_hash"]?.GetValue<string>()
            || selection["selection_hash"]?.GetValue<string>() != approval["selection_hash"]?.GetValue<string>()
            || CanonicalJson.Serialize(selection["items"] ?? new JsonArray()) != CanonicalJson.Serialize(approval["items"] ?? new JsonArray()))
            throw new InvalidOperationException("WB-AUTHORITY-PROOF-409: approval 与 selection 快照不一致。");
        var paths = approval["items"]!.AsArray().OfType<JsonObject>().Select(x => x["path"]!.GetValue<string>()).ToArray();
        var snapshot = _application.CompileExact(paths, contentTier).Snapshot ?? throw new InvalidOperationException("WB-AUTHORITY-COMPILE-409: 无法建立 CompileProof 输入快照。");
        var registrySnapshotHash = RegistrySnapshotHash(snapshot.Registries);
        var referenceClosureHash = snapshot.SourceRegistryHash;
        var requestDigest = CanonicalJson.Hash(new JsonObject
        {
            ["approval_id"] = approval["approval_id"]!.GetValue<string>(),
            ["selection_id"] = selectionId,
            ["selection_hash"] = approval["selection_hash"]!.GetValue<string>(),
            ["content_tier"] = contentTier
        });
        using var compileWorkspaceLease = AcquireCompileWorkspaceLease();
        var proofOperationPath = Path.Combine(OperationsRoot, SafeId(operationId) + ".json");
        if (File.Exists(proofOperationPath))
        {
            var operation = ReadOperation(operationId);
            if (operation["kind"]?.GetValue<string>() != "compile_proof") throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation_id 已用于其他操作。");
            if (operation["state"]?.GetValue<string>() != "committed" || operation["request_digest"]?.GetValue<string>() != requestDigest)
                throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation_id 请求参数不一致。");
            var existing = ReadCommittedOperationTarget(operationId, "compile_proof");
            return new AuthorityCompileProof(existing["compile_proof_id"]!.GetValue<string>(), existing["proof_hash"]!.GetValue<string>(), existing["content_tier"]!.GetValue<string>(), existing["items"]!.AsArray().Count);
        }
        var proofId = $"compile.{Guid.NewGuid():N}";
        var proof = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring-v1.compile-proof",
            ["compile_proof_id"] = proofId,
            ["approval_id"] = approval["approval_id"]!.GetValue<string>(),
            ["approval_proof_hash"] = approval["proof_hash"]!.GetValue<string>(),
            ["selection_id"] = selectionId,
            ["selection_hash"] = approval["selection_hash"]!.GetValue<string>(),
            ["content_tier"] = contentTier,
            ["items"] = approval["items"]!.DeepClone(),
            ["registry_snapshot_hash"] = registrySnapshotHash,
            ["reference_closure_hash"] = referenceClosureHash,
            ["request_digest"] = requestDigest,
            ["issuer"] = ServerIssuer,
            ["issued_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["immutable"] = true
        };
        proof["proof_hash"] = HashWithout(proof, "proof_hash");
        proof = Commit(operationId, "compile_proof", CompileProofStrictPath(proofId), proof, null, new JsonObject { ["request_digest"] = requestDigest });
        proofId = proof["compile_proof_id"]!.GetValue<string>();
        return new AuthorityCompileProof(proofId, proof["proof_hash"]!.GetValue<string>(), proof["content_tier"]!.GetValue<string>(), proof["items"]!.AsArray().Count);
    }

    public AuthorityCompileSettlement CompileApproved(string proofId, string? confirmationToken = null, string? outputRoot = null)
    {
        var targetRoot = NormalizeCompileOutputRoot(outputRoot);
        var operationId = CompileSettlementSupport.OperationId(proofId);
        var operationPath = ExistingOrStrictCompileOperationPath(operationId);
        var reservationPath = File.Exists(operationPath) ? ExistingOrStrictCompileReservationPath(operationId) : CompileReservationStrictPath(operationId);
        JsonObject proof;
        string requestDigest;
        var workspaceIdentity = "workspace:" + StudioRuntimeHashing.WorkspaceHash(_workspace.Root);
        if (File.Exists(operationPath))
        {
            var existing = ReadJson(operationPath);
            proof = ReadPersistedCompileProof(proofId, existing["proof_hash"]?.GetValue<string>());
        }
        else
        {
            proof = ValidateCompileProof(proofId);
        }
        requestDigest = CompileSettlementSupport.RequestDigest(workspaceIdentity, proofId, proof, Relative(targetRoot).ToLowerInvariant(), confirmationToken);
        FileStream? workspaceLease = null;
        FileStream? targetLease = null;
        var mutationStarted = false;

        try
        {
            workspaceLease = AcquireCompileWorkspaceLease();
            lock (_gate)
            {
                operationPath = ExistingOrStrictCompileOperationPath(operationId);
                reservationPath = File.Exists(operationPath) ? ExistingOrStrictCompileReservationPath(operationId) : CompileReservationStrictPath(operationId);
                if (File.Exists(operationPath))
                {
                    var existing = ReadJson(operationPath);
                    proof = ReadPersistedCompileProof(proofId, existing["proof_hash"]?.GetValue<string>());
                    requestDigest = CompileSettlementSupport.RequestDigest(workspaceIdentity, proofId, proof, Relative(targetRoot).ToLowerInvariant(), confirmationToken);
                    RequireCompileOperationMatch(existing, operationId, requestDigest);
                    if (existing["state"]?.GetValue<string>() == "committed") return ReadCommittedCompileSettlement(existing, proofId, requestDigest);
                    throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile operation 尚未提交或已 fail-closed。");
                }

                proof = ValidateCompileProof(proofId);
                requestDigest = CompileSettlementSupport.RequestDigest(workspaceIdentity, proofId, proof, Relative(targetRoot).ToLowerInvariant(), confirmationToken);
                targetLease = AcquireCompileTargetLease(targetRoot);
                var reserved = CompileOperation(operationId, requestDigest, proofId, proof, targetRoot, confirmationToken);
                var previousManifestPath = Path.Combine(targetRoot, "manifest.json");
                if (File.Exists(previousManifestPath))
                    reserved["previous_manifest_hash"] = Hashing.FileSha256(previousManifestPath).ToLowerInvariant();
                reserved["reservation_relative"] = Relative(reservationPath);
                try
                {
                    WriteExclusiveJson(operationPath, reserved);
                }
                catch (IOException) when (File.Exists(operationPath))
                {
                    var existing = ReadJson(operationPath);
                    RequireCompileOperationMatch(existing, operationId, requestDigest);
                    if (existing["state"]?.GetValue<string>() == "committed") return ReadCommittedCompileSettlement(existing, proofId, requestDigest);
                    throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile operation 已被其他进程占用。");
                }
                mutationStarted = true;
                try
                {
                    WriteExclusiveJson(reservationPath, reserved);
                }
                catch (IOException) when (File.Exists(reservationPath))
                {
                    var existing = ReadJson(reservationPath);
                    RequireCompileOperationMatch(existing, operationId, requestDigest);
                    throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile reservation 已被其他进程占用。");
                }
                InvokeCompileFault(CompileSettlementFaultPoint.AfterReservation);
                RequireCompileState(reserved, "reserved");
                reserved["state"] = "prepared";
                reserved["prepared_at"] = DateTimeOffset.UtcNow.ToString("O");
                WriteAtomic(operationPath, reserved);
                WriteAtomic(reservationPath, reserved);
                InvokeCompileFault(CompileSettlementFaultPoint.AfterPrepared);
                RequireCompileState(reserved, "prepared");
                reserved["state"] = "executing";
                WriteAtomic(operationPath, reserved);
                WriteAtomic(reservationPath, reserved);
                InvokeCompileFault(CompileSettlementFaultPoint.AfterExecuting);
                RequireCompileOwner(reserved, reservationPath);
            }
            var paths = proof["items"]!.AsArray().OfType<JsonObject>().Select(x => x["path"]!.GetValue<string>()).ToArray();
            var compiled = _application.CompileExact(paths, proof["content_tier"]!.GetValue<string>(), confirmationToken);
            if (!compiled.Validation.Valid) throw new InvalidOperationException("WB-AUTHORITY-COMPILE-422: CompileProof 对应输入未通过验证。");
            var compiledPath = _application.WriteCompiled(
                compiled,
                targetRoot,
                () => InvokeCompileFault(CompileSettlementFaultPoint.AfterTempCandidate),
                () =>
                {
                    PersistCompileReplacementPhase(operationPath, reservationPath, operationId, requestDigest, "target_replaced");
                    InvokeCompileFault(CompileSettlementFaultPoint.AfterTargetReplace);
                },
                (stagingRoot, previousRoot) => PersistCompileReplacementPaths(operationPath, reservationPath, operationId, requestDigest, stagingRoot, previousRoot));
            var manifestHash = Hashing.FileSha256(Path.Combine(compiledPath, "manifest.json"));
            var resultPath = CompileResultStrictPath(operationId);
            var result = new AuthorityCompileSettlement(compiled, compiledPath);
            var operation = ReadJson(operationPath);
            RequireCompileOperationMatch(operation, operationId, requestDigest);
            RequireCompileState(operation, "executing");
            operation["state"] = "candidate_written";
            operation["candidate_relative"] = Relative(compiledPath);
            operation["target_relative"] = Relative(compiledPath);
            operation["manifest_hash"] = manifestHash;
            operation["result_relative"] = Relative(resultPath);
            RequireCompileOwner(operation, reservationPath);
            WriteAtomic(operationPath, operation);
            InvokeCompileFault(CompileSettlementFaultPoint.AfterCandidateWritten);

            var resultEnvelope = CompileSettlementSupport.ResultEnvelope(result, operationId, requestDigest, workspaceIdentity, proofId, operation["proof_hash"]!.GetValue<string>(), operation["content_tier"]!.GetValue<string>(), Relative(targetRoot).ToLowerInvariant(), operation["fence_token"]!.GetValue<string>());
            operation["result_hash"] = resultEnvelope["result_hash"]!.DeepClone();
            WriteImmutableJson(resultPath, resultEnvelope);
            InvokeCompileFault(CompileSettlementFaultPoint.AfterResultWrite);

            var marker = CompileMarker(operation, resultEnvelope);
            marker["integrity_tag"] = CompileSettlementSupport.IntegrityTag(marker);
            WriteImmutableJson(CompileMarkerStrictPath(operationId), marker);
            InvokeCompileFault(CompileSettlementFaultPoint.AfterMarkerWrite);

            RequireCompileState(operation, "candidate_written");
            operation["state"] = "committed";
            operation["committed_at"] = DateTimeOffset.UtcNow.ToString("O");
            WriteAtomic(operationPath, operation);
            AppendJournal(new JsonObject { ["operation_id"] = operationId, ["kind"] = CompileSettlementSupport.Kind, ["state"] = "committed", ["request_digest"] = requestDigest, ["manifest_hash"] = manifestHash });
            InvokeCompileFault(CompileSettlementFaultPoint.AfterJournalAppend);
            CleanupCompiledPrevious(targetRoot);
            DeleteIfExists(reservationPath);
            return new AuthorityCompileSettlement(compiled, compiledPath, resultEnvelope);
        }
        catch (AuthorityMutationUnknownException) { throw; }
        catch (InvalidOperationException error) when (IsStableAuthorityConflict(error)) { throw; }
        catch (Exception) when (!mutationStarted) { throw; }
        catch (Exception error) { throw new AuthorityMutationUnknownException(error); }
        finally
        {
            targetLease?.Dispose();
            workspaceLease?.Dispose();
        }
    }

    public string ExportStaging(string operationId, string proofId, string? confirmationToken, string? outputRoot = null)
    {
        RequireId(operationId, "operation_id");
        var proof = ValidateCompileProof(proofId);
        var requestDigest = ExportRequestDigest(proofId, proof, outputRoot, confirmationToken);
        if (File.Exists(Path.Combine(OperationsRoot, SafeId(operationId) + ".json")))
        {
            var operation = ReadOperation(operationId);
            if (operation["kind"]?.GetValue<string>() != "export_staging") throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation_id 已用于其他操作。");
            if (operation["request_digest"]?.GetValue<string>() != requestDigest) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation_id 请求参数不一致。");
            if (TryReadCommittedOperationPath(operationId, "export_staging", out var existingPath)) return existingPath;
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation 尚未提交或已 fail-closed。");
        }
        var compiled = _application.CompileExact(
            proof["items"]!.AsArray().OfType<JsonObject>().Select(x => x["path"]!.GetValue<string>()).ToArray(),
            proof["content_tier"]!.GetValue<string>(),
            confirmationToken);
        if (!compiled.Validation.Valid) throw new InvalidOperationException("WB-AUTHORITY-COMPILE-422: CompileProof 对应输入未通过验证。");
        var staging = new AtomicCandidatePublisher(_workspace).Publish(compiled, proof["content_tier"]!.GetValue<string>(), confirmationToken, outputRoot, updatePointer: false);
        return CommitArtifactOperation(operationId, "export_staging", staging, new JsonObject { ["proof_id"] = proofId, ["request_digest"] = requestDigest, ["manifest_hash"] = Hashing.FileSha256(Path.Combine(staging, "manifest.json")) });
    }

    public string ExportStagingApproved(string proofId, string? confirmationToken, string? outputRoot = null)
    {
        var normalizedOutput = NormalizeOutputRoot(outputRoot);
        var operationId = $"customer.export.{SafeId(proofId)}";
        return ExportStaging(operationId, proofId, confirmationToken, normalizedOutput);
    }

    public string PublishStaging(string operationId, string stagingPath, string expectedManifestHash, string? expectedCurrentManifestHash = null)
    {
        RequireId(operationId, "operation_id");
        if (TryReadCommittedOperationPath(operationId, "publish_pointer", out var existingPath)) return existingPath;
        var full = _workspace.Policy.RequireExport(stagingPath, "staging 发布");
        var manifestPath = Path.Combine(full, "manifest.json");
        if (!File.Exists(manifestPath)) throw new InvalidOperationException("WB-AUTHORITY-STAGING-404: staging manifest 不存在。");
        var manifestHash = Hashing.FileSha256(manifestPath);
        if (!string.Equals(manifestHash, expectedManifestHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("WB-AUTHORITY-STAGING-409: staging manifest hash 不匹配。");
        var pointerPath = Path.Combine(_workspace.Root, "export", "WorldbookV2", "current.json");
        var previous = File.Exists(pointerPath) ? ReadJson(pointerPath)["manifest_hash"]?.GetValue<string>() : null;
        if (!string.Equals(expectedCurrentManifestHash, previous, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AUTHORITY-POINTER-409: current pointer 已变化。");
        var pointer = new JsonObject
        {
            ["candidate_dir"] = Path.GetFileName(full),
            ["manifest_hash"] = manifestHash,
            ["published_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["previous_manifest_hash"] = previous
        };
        var publishProofPath = Path.Combine(PublishProofsRoot, SafeId(operationId) + ".json");
        var publishProof = new JsonObject
        {
            ["schema_version"] = "awake.worldbook.authoring-v1.publish-proof",
            ["operation_id"] = operationId,
            ["staging_path"] = Relative(full),
            ["manifest_hash"] = manifestHash,
            ["expected_current_manifest_hash"] = expectedCurrentManifestHash,
            ["pointer"] = pointer.DeepClone(),
            ["issued_at"] = DateTimeOffset.UtcNow.ToString("O")
        };
        publishProof["proof_hash"] = HashWithout(publishProof, "proof_hash");
        lock (_gate)
        {
            var operationPath = Path.Combine(OperationsRoot, SafeId(operationId) + ".json");
            WriteAtomic(operationPath, new JsonObject { ["operation_id"] = operationId, ["kind"] = "publish_pointer", ["state"] = "prepared", ["created_at"] = DateTimeOffset.UtcNow.ToString("O"), ["target"] = Relative(pointerPath), ["manifest_hash"] = manifestHash, ["publish_proof"] = Relative(publishProofPath) });
            WriteAtomic(publishProofPath, publishProof);
            WriteAtomic(Path.Combine(MarkersRoot, SafeId(operationId) + ".json"), new JsonObject { ["operation_id"] = operationId, ["kind"] = "publish_pointer", ["state"] = "committed", ["target"] = Relative(pointerPath), ["pointer"] = pointer.DeepClone(), ["publish_proof"] = Relative(publishProofPath) });
            WriteAtomic(pointerPath, pointer);
            var operation = new JsonObject { ["operation_id"] = operationId, ["kind"] = "publish_pointer", ["state"] = "committed", ["target"] = Relative(pointerPath), ["manifest_hash"] = manifestHash, ["publish_proof"] = Relative(publishProofPath), ["committed_at"] = DateTimeOffset.UtcNow.ToString("O") };
            WriteAtomic(operationPath, operation);
            AppendJournal(operation);
        }
        return pointerPath;
    }

    public JsonObject ReadOperation(string operationId)
        => ReadJson(ResolveOperationPath(operationId));

    private JsonObject ValidateCompileProof(string proofId)
    {
        var proof = ReadJson(ResolveCompileProofPath(proofId));
        if (proof["compile_proof_id"]?.GetValue<string>() != proofId) throw new InvalidOperationException("WB-AUTHORITY-PROOF-409: CompileProof ID 不一致。");
        if (proof["issuer"]?.GetValue<string>() != ServerIssuer || proof["immutable"]?.GetValue<bool>() != true) throw new InvalidOperationException("WB-AUTHORITY-PROOF-409: CompileProof 不是 server-issued immutable proof。");
        if (HashWithout(proof, "proof_hash") != proof["proof_hash"]?.GetValue<string>()) throw new InvalidOperationException("WB-AUTHORITY-PROOF-409: CompileProof hash 无效。");
        if (proof["content_tier"]?.GetValue<string>() is not ("base" or "adult_optional")) throw new InvalidOperationException("WB-AUTHORITY-TIER-422: content tier 无效。");
        foreach (var item in proof["items"]!.AsArray().OfType<JsonObject>())
        {
            var current = _workspace.ReadAuthoring(item["path"]!.GetValue<string>());
            var hash = current.Report.InputHash ?? Hashing.Sha256Text(current.Content);
            var revision = WorldbookInputNormalization.ReadRevision(current.Document);
            if (hash != item["content_hash"]?.GetValue<string>() || revision != item["revision"]?.GetValue<int>()) throw new InvalidOperationException("WB-AUTHORITY-CAS-409: CompileProof 输入已变化。");
        }
        var approvalId = proof["approval_id"]!.GetValue<string>();
        var approval = ReadJson(Path.Combine(ApprovalsRoot, SafeId(approvalId) + ".json"));
        var selectionId = proof["selection_id"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-AUTHORITY-PROOF-409: CompileProof 缺少 selection_id。");
        var selection = ReadJson(Path.Combine(SelectionsRoot, SafeId(selectionId) + ".json"));
        if (approval["approved"]?.GetValue<bool>() != true
            || HashWithout(approval, "proof_hash") != approval["proof_hash"]?.GetValue<string>()
            || approval["proof_hash"]?.GetValue<string>() != proof["approval_proof_hash"]?.GetValue<string>()
            || approval["selection_id"]?.GetValue<string>() != selectionId
            || approval["selection_hash"]?.GetValue<string>() != proof["selection_hash"]?.GetValue<string>()
            || Hashing.Sha256Text(CanonicalJson.Serialize(selection["items"] ?? new JsonArray())) != selection["selection_hash"]?.GetValue<string>()
            || selection["selection_hash"]?.GetValue<string>() != proof["selection_hash"]?.GetValue<string>()
            || CanonicalJson.Serialize(selection["items"] ?? new JsonArray()) != CanonicalJson.Serialize(proof["items"] ?? new JsonArray()))
            throw new InvalidOperationException("WB-AUTHORITY-PROOF-409: ApprovalProof 或 selection 已变化。");
        var paths = proof["items"]!.AsArray().OfType<JsonObject>().Select(x => x["path"]!.GetValue<string>()).ToArray();
        var currentSnapshot = _application.CompileExact(paths, proof["content_tier"]!.GetValue<string>()).Snapshot ?? throw new InvalidOperationException("WB-AUTHORITY-COMPILE-409: 无法重建 CompileProof 输入快照。");
        if (RegistrySnapshotHash(currentSnapshot.Registries) != proof["registry_snapshot_hash"]?.GetValue<string>()
            || currentSnapshot.SourceRegistryHash != proof["reference_closure_hash"]?.GetValue<string>())
            throw new InvalidOperationException("WB-AUTHORITY-CLOSURE-409: registry 或 reference closure 已变化。");
        return proof;
    }

    private static string RegistrySnapshotHash(RegistrySnapshot snapshot)
        => CanonicalJson.Hash(new JsonObject
        {
            ["profile_version"] = snapshot.ProfileVersion,
            ["profile_hash"] = snapshot.ProfileHash,
            ["referral_version"] = snapshot.ReferralVersion,
            ["referral_hash"] = snapshot.ReferralHash
        });

    private string NormalizeOutputRoot(string? outputRoot)
        => Path.GetFullPath(outputRoot ?? Path.Combine(_workspace.Root, "export", "WorldbookV2"));

    private string NormalizeCompileOutputRoot(string? outputRoot)
        => _workspace.Policy.RequireCompiled(outputRoot ?? Path.Combine(_workspace.Root, "compiled", "customer"), "compiled 输出");

    private JsonObject ReadPersistedCompileProof(string proofId, string? expectedProofHash)
    {
        var proof = ReadJson(ResolveCompileProofPath(proofId));
        if (proof["compile_proof_id"]?.GetValue<string>() != proofId
            || proof["issuer"]?.GetValue<string>() != ServerIssuer
            || proof["immutable"]?.GetValue<bool>() != true
            || HashWithout(proof, "proof_hash") != proof["proof_hash"]?.GetValue<string>()
            || (!string.IsNullOrWhiteSpace(expectedProofHash) && !string.Equals(expectedProofHash, proof["proof_hash"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("WB-AUTHORITY-PROOF-409: CompileProof 持久化绑定无效。");
        return proof;
    }

    private string CompileProofStrictPath(string proofId)
        => Path.Combine(CompileProofsRoot, StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.CompileProof, new Dictionary<string, object?> { ["compile_proof_id"] = proofId }) + ".json");

    private string CompileProofLegacyPath(string proofId)
        => Path.Combine(CompileProofsRoot, StorageIdentityCanonicalizerV1.OldSafeIdV0(proofId) + ".json");

    private string ResolveCompileProofPath(string proofId)
    {
        return ResolveStorageRecordPath(
            CompileProofStrictPath(proofId),
            CompileProofLegacyPath(proofId),
            proof => ValidateCompileProofIdentity(proof, proofId));
    }

    private static void ValidateCompileProofIdentity(JsonObject proof, string proofId)
    {
        if (proof["compile_proof_id"]?.GetValue<string>() != proofId
            || proof["schema_version"]?.GetValue<string>() != "awake.worldbook.authoring-v1.compile-proof")
            throw new InvalidOperationException("WB-AUTHORITY-SAFEID-409: CompileProof 身份或 schema 不一致。");
    }

    private string CompileResultStrictPath(string operationId)
        => Path.Combine(CompileResultsRoot, StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.CompileResult, new Dictionary<string, object?> { ["operation_id"] = operationId }) + ".json");

    private string CompileResultLegacyPath(string operationId)
        => Path.Combine(CompileResultsRoot, StorageIdentityCanonicalizerV1.OldSafeIdV0(operationId) + ".json");

    private string CompileMarkerStrictPath(string operationId)
        => Path.Combine(MarkersRoot, StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.CommitMarker, new Dictionary<string, object?> { ["operation_id"] = operationId }) + ".json");

    private string CompileMarkerLegacyPath(string operationId)
        => Path.Combine(MarkersRoot, StorageIdentityCanonicalizerV1.OldSafeIdV0(operationId) + ".json");

    private string ResolveCompileMarkerPath(string operationId)
        => ResolveStorageRecordPath(
            CompileMarkerStrictPath(operationId),
            CompileMarkerLegacyPath(operationId),
            marker => ValidateCompileRecord(marker, operationId, "marker"));

    private string ExistingOrStrictCompileMarkerPath(string operationId)
    {
        var strictPath = CompileMarkerStrictPath(operationId);
        var legacyPath = CompileMarkerLegacyPath(operationId);
        return File.Exists(strictPath) || File.Exists(legacyPath)
            ? ResolveCompileMarkerPath(operationId)
            : strictPath;
    }

    private string CompileOperationStrictPath(string operationId)
        => Path.Combine(OperationsRoot, StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.Operation, new Dictionary<string, object?> { ["operation_id"] = operationId }) + ".json");

    private string CompileOperationLegacyPath(string operationId)
        => Path.Combine(OperationsRoot, StorageIdentityCanonicalizerV1.OldSafeIdV0(operationId) + ".json");

    private string ResolveOperationPath(string operationId)
    {
        var strictPath = CompileOperationStrictPath(operationId);
        var legacyPath = CompileOperationLegacyPath(operationId);
        if (File.Exists(strictPath))
            return ResolveCompileOperationPath(operationId);
        if (File.Exists(legacyPath) && ReadJson(legacyPath)["kind"]?.GetValue<string>() == CompileSettlementSupport.Kind)
            return ResolveCompileOperationPath(operationId);
        return ResolveStorageRecordPath(
            strictPath,
            legacyPath,
            operation => ValidateStorageIdentity(operation, "operation_id", operationId, "operation"));
    }

    private string CompileReservationStrictPath(string operationId)
        => Path.Combine(CompileReservationsRoot, StorageIdentityCanonicalizerV1.Key(StorageRecordFamily.CompileReservation, new Dictionary<string, object?> { ["operation_id"] = operationId }) + ".json");

    private string CompileReservationLegacyPath(string operationId)
        => Path.Combine(CompileReservationsRoot, StorageIdentityCanonicalizerV1.OldSafeIdV0(operationId) + ".json");

    private string ResolveCompileReservationPath(string operationId)
        => ResolveStorageRecordPath(
            CompileReservationStrictPath(operationId),
            CompileReservationLegacyPath(operationId),
            reservation => ValidateCompileRecord(reservation, operationId, "reservation"));

    private string ResolveCompileResultPath(string operationId)
        => ResolveStorageRecordPath(
            CompileResultStrictPath(operationId),
            CompileResultLegacyPath(operationId),
            result => ValidateCompileRecord(result, operationId, "result"));

    private string ResolveCompileOperationPath(string operationId)
        => ResolveStorageRecordPath(
            CompileOperationStrictPath(operationId),
            CompileOperationLegacyPath(operationId),
            operation => ValidateCompileRecord(operation, operationId, "operation"));

    private string ExistingOrStrictCompileOperationPath(string operationId)
    {
        var strictPath = CompileOperationStrictPath(operationId);
        var legacyPath = CompileOperationLegacyPath(operationId);
        return File.Exists(strictPath) || File.Exists(legacyPath)
            ? ResolveCompileOperationPath(operationId)
            : strictPath;
    }

    private string ExistingOrStrictCompileReservationPath(string operationId)
    {
        var strictPath = CompileReservationStrictPath(operationId);
        var legacyPath = CompileReservationLegacyPath(operationId);
        return File.Exists(strictPath) || File.Exists(legacyPath)
            ? ResolveCompileReservationPath(operationId)
            : strictPath;
    }

    private string ResolveStorageRecordPath(string strictPath, string legacyPath, Action<JsonObject> validate)
    {
        if (File.Exists(strictPath))
        {
            var strict = ReadJson(strictPath);
            validate(strict);
            if (File.Exists(legacyPath)
                && !string.Equals(Hashing.FileSha256(strictPath), Hashing.FileSha256(legacyPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-AUTHORITY-SAFEID-409: strict 与 legacy 记录内容冲突。");
            return strictPath;
        }

        if (File.Exists(legacyPath))
        {
            var legacy = ReadJson(legacyPath);
            validate(legacy);
            return legacyPath;
        }

        throw new InvalidOperationException($"WB-AUTHORITY-404: authority record 不存在。{Relative(legacyPath)}");
    }

    private static void ValidateStorageIdentity(JsonObject record, string field, string expected, string recordName)
    {
        if (!string.Equals(record[field]?.GetValue<string>(), expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"WB-AUTHORITY-SAFEID-409: {recordName} 身份不一致。");
    }

    private static void ValidateCompileRecord(JsonObject record, string operationId, string recordName)
    {
        ValidateStorageIdentity(record, "operation_id", operationId, recordName);
        var expectedSchema = recordName == "result"
            ? CompileSettlementSupport.ResultSchemaVersion
            : CompileSettlementSupport.SchemaVersion;
        if (record["schema_version"]?.GetValue<string>() != expectedSchema)
            throw new InvalidOperationException($"WB-AUTHORITY-SAFEID-409: compile {recordName} schema 不一致。");
        if (recordName != "result" && record["kind"]?.GetValue<string>() != CompileSettlementSupport.Kind)
            throw new InvalidOperationException($"WB-AUTHORITY-SAFEID-409: compile {recordName} family 不一致。");
        if (recordName == "result")
        {
            var resultHash = record["result_hash"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(resultHash) || HashWithout(record, "result_hash") != resultHash)
                throw new InvalidOperationException("WB-AUTHORITY-SAFEID-409: compile result hash 不一致。");
        }
        if (recordName == "marker" && !CompileSettlementSupport.VerifyIntegrityTag(record))
            throw new InvalidOperationException("WB-AUTHORITY-SAFEID-409: compile marker integrity 无效。");
    }

    private static void RequireCompileState(JsonObject operation, params string[] allowedStates)
    {
        var state = operation["state"]?.GetValue<string>();
        if (state is null || !allowedStates.Contains(state, StringComparer.Ordinal))
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile operation state transition 不合法。");
    }

    private JsonObject CompileOperation(string operationId, string requestDigest, string proofId, JsonObject proof, string targetRoot, string? confirmationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        return new JsonObject
        {
            ["schema_version"] = CompileSettlementSupport.SchemaVersion,
            ["operation_id"] = operationId,
            ["kind"] = CompileSettlementSupport.Kind,
            ["state"] = "reserved",
            ["request_digest"] = requestDigest,
            ["workspace_identity"] = "workspace:" + StudioRuntimeHashing.WorkspaceHash(_workspace.Root),
            ["proof_id"] = proofId,
            ["proof_hash"] = proof["proof_hash"]?.DeepClone(),
            ["content_tier"] = proof["content_tier"]?.DeepClone(),
            ["output_root"] = Relative(targetRoot).ToLowerInvariant(),
            ["target_relative"] = Relative(targetRoot),
            ["confirmation_token_hash"] = Hashing.Sha256Text(confirmationToken ?? string.Empty).ToLowerInvariant(),
            ["owner_instance_id"] = _ownerInstanceId,
            ["fence_token"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            ["reservation_relative"] = Relative(CompileReservationStrictPath(operationId)),
            ["reservation_created_at"] = now,
            ["created_at"] = now
        };
    }

    private void RequireCompileOperationMatch(JsonObject operation, string operationId, string requestDigest)
    {
        if (operation["schema_version"]?.GetValue<string>() != CompileSettlementSupport.SchemaVersion
            || operation["operation_id"]?.GetValue<string>() != operationId
            || operation["kind"]?.GetValue<string>() != CompileSettlementSupport.Kind
            || operation["request_digest"]?.GetValue<string>() != requestDigest
            || operation["proof_id"]?.GetValue<string>() is not { } proofId
            || CompileSettlementSupport.OperationId(proofId) != operationId
            || operation["workspace_identity"]?.GetValue<string>() != "workspace:" + StudioRuntimeHashing.WorkspaceHash(_workspace.Root)
            || operation["proof_hash"]?.GetValue<string>() is not { } proofHash
            || !IsHex(proofHash, 64)
            || operation["content_tier"]?.GetValue<string>() is not ("base" or "adult_optional")
            || operation["output_root"]?.GetValue<string>() is not { } outputRoot
            || !IsCompiledRoot(outputRoot)
            || operation["confirmation_token_hash"]?.GetValue<string>() is not { } tokenHash
            || !IsHex(tokenHash, 64)
            || operation["fence_token"]?.GetValue<string>() is not { } fenceToken
            || !IsHex(fenceToken, 64)
             || string.IsNullOrWhiteSpace(operation["owner_instance_id"]?.GetValue<string>()))
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile operation binding 不一致。");
        var proof = ReadPersistedCompileProof(proofId, proofHash);
        if (proof["compile_proof_id"]?.GetValue<string>() != proofId
            || proof["proof_hash"]?.GetValue<string>() != proofHash)
            throw new InvalidOperationException("WB-AUTHORITY-SAFEID-409: compile operation 与 CompileProof 绑定不一致。");
    }

    private void RequireCompileOwner(JsonObject operation, string reservationPath)
    {
        if (!string.Equals(operation["owner_instance_id"]?.GetValue<string>(), _ownerInstanceId, StringComparison.Ordinal))
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile reservation owner 不匹配。");
        var reservation = ReadJson(reservationPath);
        if (reservation["operation_id"]?.GetValue<string>() != operation["operation_id"]?.GetValue<string>()
            || reservation["request_digest"]?.GetValue<string>() != operation["request_digest"]?.GetValue<string>()
            || reservation["owner_instance_id"]?.GetValue<string>() != _ownerInstanceId
            || reservation["fence_token"]?.GetValue<string>() != operation["fence_token"]?.GetValue<string>())
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile reservation fence 不匹配。");
    }

    private void InvokeCompileFault(CompileSettlementFaultPoint point) => _compileFaultInjector?.Invoke(point);

    private static void DeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private bool IsCompiledRoot(string relative)
    {
        try { return _workspace.Policy.RequireCompiled(ResolveWorkspacePath(relative), "compiled settlement") == ResolveWorkspacePath(relative); }
        catch (InvalidOperationException) { return false; }
    }

    private static bool IsHex(string value, int length)
        => value.Length == length && value.All(Uri.IsHexDigit);

    private static bool IsStableAuthorityConflict(InvalidOperationException error)
        => error.Message.StartsWith("WB-AUTHORITY-OPERATION-409:", StringComparison.Ordinal)
            || error.Message.StartsWith("WB-AUTHORITY-CAS-409:", StringComparison.Ordinal)
            || error.Message.StartsWith("WB-AUTHORITY-PROOF-409:", StringComparison.Ordinal)
            || error.Message.StartsWith("WB-AUTHORITY-CLOSURE-409:", StringComparison.Ordinal)
            || error.Message.StartsWith("WB-AUTHORITY-SAFEID-409:", StringComparison.Ordinal);

    private AuthorityCompileSettlement ReadCommittedCompileSettlement(JsonObject operation, string proofId, string requestDigest)
    {
        var operationId = CompileSettlementSupport.OperationId(proofId);
        RequireCompileOperationMatch(operation, operationId, requestDigest);
        var proof = ReadPersistedCompileProof(proofId, operation["proof_hash"]?.GetValue<string>());
        if (operation["proof_id"]?.GetValue<string>() != proof["compile_proof_id"]?.GetValue<string>()
            || operation["proof_hash"]?.GetValue<string>() != proof["proof_hash"]?.GetValue<string>())
            throw new InvalidOperationException("WB-AUTHORITY-SAFEID-409: compile operation 与 CompileProof 绑定不一致。");
        if (operation["state"]?.GetValue<string>() != "committed") throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile operation 尚未提交或已 fail-closed。");
        var markerPath = ResolveCompileMarkerPath(operationId);
        var marker = ReadJson(markerPath);
        if (!CompileSettlementSupport.VerifyIntegrityTag(marker)) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile commit marker 完整性校验失败。");
        if (marker["integrity_algorithm"]?.GetValue<string>() != CompileSettlementSupport.IntegrityAlgorithm || marker["integrity_key_version"]?.GetValue<string>() != "authority-v1")
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile commit marker integrity version 无效。");
        foreach (var key in new[] { "schema_version", "operation_id", "kind", "state", "request_digest", "workspace_identity", "proof_id", "proof_hash", "content_tier", "output_root", "owner_instance_id", "fence_token", "candidate_relative", "target_relative", "manifest_hash", "result_relative", "result_hash" })
        {
            var expected = key == "schema_version" ? CompileSettlementSupport.SchemaVersion : key == "kind" ? CompileSettlementSupport.Kind : key == "state" ? "committed" : operation[key]?.GetValue<string>();
            if (marker[key]?.GetValue<string>() != expected) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile commit marker binding 不一致。");
        }
        var resultPath = ResolveCompileResultPath(operationId);
        var resultEnvelope = ReadJson(resultPath);
        var resultHash = resultEnvelope["result_hash"]?.GetValue<string>();
        var resultHashInput = resultEnvelope.DeepClone().AsObject();
        resultHashInput.Remove("result_hash");
        if (string.IsNullOrWhiteSpace(resultHash) || CanonicalJson.Hash(resultHashInput) != resultHash || resultHash != operation["result_hash"]?.GetValue<string>() || resultHash != marker["result_hash"]?.GetValue<string>())
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result 完整性校验失败。");
        foreach (var key in new[] { "operation_id", "request_digest", "workspace_identity", "proof_id", "proof_hash", "content_tier", "output_root", "fence_token" })
            if (resultEnvelope[key]?.GetValue<string>() != operation[key]?.GetValue<string>()) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result binding 不一致。");
        var targetPath = ResolveWorkspacePath(operation["target_relative"]?.GetValue<string>());
        var expectedCompiledPath = Path.GetRelativePath(ResolveWorkspacePath(operation["output_root"]?.GetValue<string>()), targetPath).Replace('\\', '/');
        if (!string.Equals(resultEnvelope["compiled_path"]?.GetValue<string>(), expectedCompiledPath, StringComparison.Ordinal))
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile result target binding 不一致。");
        var manifestPath = Path.Combine(targetPath, "manifest.json");
        if (!Directory.Exists(targetPath) || !File.Exists(manifestPath) || !string.Equals(Hashing.FileSha256(manifestPath), operation["manifest_hash"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase) || !string.Equals(resultEnvelope["manifest_hash"]?.GetValue<string>(), operation["manifest_hash"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compiled artifact 清单不匹配。");
        var loaded = CompileSettlementSupport.LoadResult(resultEnvelope, targetPath);
        return new AuthorityCompileSettlement(loaded, targetPath, resultEnvelope);
    }

    private JsonObject CompileMarker(JsonObject operation, JsonObject resultEnvelope)
        => new()
        {
            ["schema_version"] = CompileSettlementSupport.SchemaVersion,
            ["operation_id"] = operation["operation_id"]?.DeepClone(),
            ["kind"] = CompileSettlementSupport.Kind,
            ["state"] = "committed",
            ["request_digest"] = operation["request_digest"]?.DeepClone(),
            ["workspace_identity"] = operation["workspace_identity"]?.DeepClone(),
            ["proof_id"] = operation["proof_id"]?.DeepClone(),
            ["proof_hash"] = operation["proof_hash"]?.DeepClone(),
            ["content_tier"] = operation["content_tier"]?.DeepClone(),
            ["output_root"] = operation["output_root"]?.DeepClone(),
            ["owner_instance_id"] = operation["owner_instance_id"]?.DeepClone(),
            ["fence_token"] = operation["fence_token"]?.DeepClone(),
            ["candidate_relative"] = operation["candidate_relative"]?.DeepClone(),
            ["target_relative"] = operation["target_relative"]?.DeepClone(),
            ["manifest_hash"] = operation["manifest_hash"]?.DeepClone(),
            ["result_relative"] = operation["result_relative"]?.DeepClone(),
            ["result_hash"] = resultEnvelope["result_hash"]?.DeepClone(),
            ["committed_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["integrity_algorithm"] = CompileSettlementSupport.IntegrityAlgorithm,
            ["integrity_key_version"] = "authority-v1"
        };

    private static void WriteExclusiveJson(string path, JsonNode value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = Encoding.UTF8.GetBytes(CanonicalJson.Serialize(value));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    private static void WriteImmutableJson(string path, JsonNode value)
    {
        var bytes = Encoding.UTF8.GetBytes(CanonicalJson.Serialize(value));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        catch (IOException) when (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (CryptographicOperations.FixedTimeEquals(existing, bytes)) return;
            throw new InvalidOperationException("WB-AUTHORITY-SAFEID-409: immutable record 内容冲突。");
        }
    }

    private FileStream AcquireCompileTargetLease(string targetRoot)
    {
        var parent = Directory.GetParent(targetRoot)?.FullName ?? throw new InvalidOperationException("WB-AUTHORITY-COMPILE-409: compiled 输出目录无效。");
        var leasePath = Path.Combine(parent, "." + Path.GetFileName(targetRoot) + ".compile-lease");
        try
        {
            var stream = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
            stream.SetLength(0);
            var bytes = Encoding.UTF8.GetBytes(_ownerInstanceId);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
            return stream;
        }
        catch (IOException)
        {
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compiled 输出当前由其他操作占用。");
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compiled 输出当前不可用。");
        }
    }

    private FileStream AcquireCompileWorkspaceLease()
    {
        try
        {
            var stream = new FileStream(CompileWorkspaceLeasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
            stream.SetLength(0);
            var bytes = Encoding.UTF8.GetBytes(_ownerInstanceId);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
            return stream;
        }
        catch (IOException)
        {
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: authoring 工作区当前由其他编译操作占用。");
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: authoring 工作区编译锁不可用。");
        }
    }

    private FileStream? TryAcquireCompileWorkspaceLease()
    {
        try
        {
            var stream = new FileStream(CompileWorkspaceLeasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
            stream.SetLength(0);
            var bytes = Encoding.UTF8.GetBytes("recovery:" + _ownerInstanceId);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
            return stream;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("WB-AUTHORITY-RECOVERY-409: authoring 工作区编译恢复锁不可用。");
        }
    }

    private FileStream? TryAcquireCompileRecoveryLease(JsonObject operation)
    {
        var outputRoot = ResolveWorkspacePath(operation["output_root"]?.GetValue<string>());
        var parent = Directory.GetParent(outputRoot)?.FullName ?? throw new InvalidOperationException("WB-AUTHORITY-RECOVERY-409: compiled 输出目录无效。");
        var leasePath = Path.Combine(parent, "." + Path.GetFileName(outputRoot) + ".compile-lease");
        try
        {
            var stream = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
            stream.SetLength(0);
            var bytes = Encoding.UTF8.GetBytes("recovery:" + (_ownerInstanceId));
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
            return stream;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("WB-AUTHORITY-RECOVERY-409: compiled 输出恢复锁不可用。");
        }
    }

    private void PersistCompileReplacementPaths(string operationPath, string reservationPath, string operationId, string requestDigest, string stagingRoot, string previousRoot)
    {
        lock (_gate)
        {
            var operation = ReadJson(operationPath);
            RequireCompileOperationMatch(operation, operationId, requestDigest);
            RequireCompileState(operation, "reserved", "prepared", "executing", "candidate_written");
            RequireCompileOwner(operation, reservationPath);
            operation["staging_relative"] = Relative(stagingRoot);
            operation["previous_relative"] = Relative(previousRoot);
            operation["replacement_phase"] = "ready_to_replace";
            WriteAtomic(operationPath, operation);
            WriteAtomic(reservationPath, operation);
        }
    }

    private void PersistCompileReplacementPhase(string operationPath, string reservationPath, string operationId, string requestDigest, string phase)
    {
        lock (_gate)
        {
            var operation = ReadJson(operationPath);
            RequireCompileOperationMatch(operation, operationId, requestDigest);
            RequireCompileState(operation, "reserved", "prepared", "executing", "candidate_written");
            RequireCompileOwner(operation, reservationPath);
            operation["replacement_phase"] = phase;
            if (phase == "target_replaced")
                operation["target_relative"] = operation["output_root"]?.DeepClone();
            WriteAtomic(operationPath, operation);
            WriteAtomic(reservationPath, operation);
        }
    }

    private string ExportRequestDigest(string proofId, JsonObject proof, string? outputRoot, string? confirmationToken)
        => CanonicalJson.Hash(new JsonObject
        {
            ["compile_proof_id"] = proofId,
            ["proof_hash"] = proof["proof_hash"]!.GetValue<string>(),
            ["selection_id"] = proof["selection_id"]!.GetValue<string>(),
            ["selection_hash"] = proof["selection_hash"]!.GetValue<string>(),
            ["output_root"] = NormalizeOutputRoot(outputRoot),
            ["confirmation_token_hash"] = Hashing.Sha256Text(confirmationToken ?? string.Empty)
        });

    private JsonObject Commit(string operationId, string kind, string targetPath, JsonObject payload, JsonObject? headEntry, JsonObject? operationMetadata = null)
    {
        lock (_gate)
        {
            var compileProofCommit = kind == "compile_proof";
            var operationPath = Path.Combine(OperationsRoot, SafeId(operationId) + ".json");
            if (File.Exists(operationPath)) return ReadCommittedOperationTarget(operationId, kind);
            var preparedOperation = new JsonObject { ["operation_id"] = operationId, ["kind"] = kind, ["state"] = "prepared", ["created_at"] = DateTimeOffset.UtcNow.ToString("O") };
            if (operationMetadata is not null) foreach (var property in operationMetadata) preparedOperation[property.Key] = property.Value?.DeepClone();
            WriteAtomic(operationPath, preparedOperation);
            if (compileProofCommit) WriteImmutableJson(targetPath, payload);
            else WriteAtomic(targetPath, payload);
            var markerPath = Path.Combine(MarkersRoot, SafeId(operationId) + ".json");
            var marker = new JsonObject { ["operation_id"] = operationId, ["kind"] = kind, ["state"] = "committed", ["target"] = Relative(targetPath), ["head_entry"] = headEntry };
            WriteAtomic(markerPath, marker);
            if (headEntry is not null) ApplyHeadEntry(headEntry);
            var operation = new JsonObject { ["operation_id"] = operationId, ["kind"] = kind, ["state"] = "committed", ["target"] = Relative(targetPath), ["committed_at"] = DateTimeOffset.UtcNow.ToString("O") };
            if (operationMetadata is not null) foreach (var property in operationMetadata) operation[property.Key] = property.Value?.DeepClone();
            WriteAtomic(operationPath, operation);
            AppendJournal(operation);
            return payload;
        }
    }

    private bool TryReadCommittedOperation(string operationId, string kind, out JsonObject payload)
    {
        var operationPath = Path.Combine(OperationsRoot, SafeId(operationId) + ".json");
        if (!File.Exists(operationPath))
        {
            payload = null!;
            return false;
        }
        payload = ReadCommittedOperationTarget(operationId, kind);
        return true;
    }

    private JsonObject ReadCommittedOperationTarget(string operationId, string kind)
    {
        var operation = ReadOperation(operationId);
        if (operation["kind"]?.GetValue<string>() != kind) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation_id 已用于其他操作。");
        if (operation["state"]?.GetValue<string>() != "committed") throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation 尚未提交或已 fail-closed。");
        var target = operation["target"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation 缺少提交目标。");
        var targetPath = ResolveWorkspacePath(target);
        var payload = ReadJson(targetPath);
        payload["target"] = target;
        return payload;
    }

    private bool TryReadCommittedOperationPath(string operationId, string kind, out string path)
    {
        var operationPath = Path.Combine(OperationsRoot, SafeId(operationId) + ".json");
        if (!File.Exists(operationPath))
        {
            path = string.Empty;
            return false;
        }
        var operation = ReadOperation(operationId);
        if (operation["kind"]?.GetValue<string>() != kind) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation_id 已用于其他操作。");
        if (operation["state"]?.GetValue<string>() != "committed") throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation 尚未提交或已 fail-closed。");
        path = ResolveWorkspacePath(operation["target"]?.GetValue<string>());
        if (kind == "export_staging" && !Directory.Exists(path)) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: staging artifact 缺失。");
        if (kind == "publish_pointer" && !File.Exists(path)) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: current pointer 缺失。");
        return true;
    }

    private string CommitArtifactOperation(string operationId, string kind, string artifactPath, JsonObject metadata)
    {
        lock (_gate)
        {
            var operationPath = Path.Combine(OperationsRoot, SafeId(operationId) + ".json");
            if (File.Exists(operationPath))
            {
                if (TryReadCommittedOperationPath(operationId, kind, out var existingPath)) return existingPath;
                throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation 尚未提交或已 fail-closed。");
            }
            var operation = new JsonObject { ["operation_id"] = operationId, ["kind"] = kind, ["state"] = "prepared", ["target"] = Relative(artifactPath), ["created_at"] = DateTimeOffset.UtcNow.ToString("O") };
            foreach (var property in metadata) operation[property.Key] = property.Value?.DeepClone();
            WriteAtomic(operationPath, operation);
            WriteAtomic(Path.Combine(MarkersRoot, SafeId(operationId) + ".json"), new JsonObject { ["operation_id"] = operationId, ["kind"] = kind, ["state"] = "committed", ["target"] = Relative(artifactPath) });
            operation["state"] = "committed";
            operation["committed_at"] = DateTimeOffset.UtcNow.ToString("O");
            WriteAtomic(operationPath, operation);
            AppendJournal(operation);
            return artifactPath;
        }
    }

    private string ResolveWorkspacePath(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation 缺少提交目标。");
        var path = Path.GetFullPath(Path.Combine(_workspace.Root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.GetFullPath(_workspace.Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: operation 目标越界。");
        return path;
    }

    private void Recover()
    {
        string[] operationPaths;
        try { operationPaths = Directory.EnumerateFiles(OperationsRoot, "*.json", SearchOption.TopDirectoryOnly).ToArray(); }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        foreach (var operationPath in operationPaths)
        {
            try
            {
                var operation = ReadJson(operationPath);
                if (operation["kind"]?.GetValue<string>() == CompileSettlementSupport.Kind)
                {
                    try
                    {
                        RecoverCompileOperation(operationPath, operation);
                    }
                    catch (Exception error) when (IsRecoveryBoundaryFailure(error))
                    {
                        operation["state"] = "failed_recovery";
                        operation["failure_code"] = "WB-AUTHORITY-RECOVERY-409";
                        WriteAtomic(operationPath, operation);
                    }
                    continue;
                }
                if (operation["state"]?.GetValue<string>() == "committed") continue;
                var id = operation["operation_id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id))
                {
                    operation["state"] = "failed_recovery";
                    operation["failure_code"] = "WB-AUTHORITY-RECOVERY-409";
                    WriteAtomic(operationPath, operation);
                    continue;
                }
                var markerPath = Path.Combine(MarkersRoot, SafeId(id) + ".json");
                if (File.Exists(markerPath))
                {
                    var marker = ReadJson(markerPath);
                    var kind = operation["kind"]?.GetValue<string>();
                    var recovered = true;
                    if (kind == "publish_pointer")
                    {
                        var pointer = marker["pointer"] as JsonObject;
                        var pointerPath = ResolveWorkspacePath(marker["target"]?.GetValue<string>());
                        var proofPath = ResolveWorkspacePath(marker["publish_proof"]?.GetValue<string>());
                        recovered = pointer is not null && File.Exists(proofPath) && ValidatePublishProof(proofPath, pointer) && WriteRecoveredPointer(pointerPath, pointer);
                    }
                    else if (kind == "export_staging")
                    {
                        recovered = Directory.Exists(ResolveWorkspacePath(marker["target"]?.GetValue<string>()));
                    }
                    else
                    {
                        recovered = File.Exists(ResolveWorkspacePath(marker["target"]?.GetValue<string>()));
                        if (recovered && marker["head_entry"] is JsonObject headEntry) ApplyHeadEntry(headEntry);
                    }
                    operation["state"] = recovered ? "committed" : "failed_recovery";
                }
                else operation["state"] = "failed_recovery";
                WriteAtomic(operationPath, operation);
                AppendJournal(new JsonObject { ["operation_id"] = id, ["kind"] = operation["kind"]?.DeepClone(), ["state"] = operation["state"]?.DeepClone() });
            }
            catch (Exception error) when (IsRecoveryBoundaryFailure(error) && !IsStrictLegacyConflict(error))
            {
                QuarantineOperationRecord(operationPath);
            }
        }
        try { ReconcileCompileReservations(); } catch (Exception error) when (IsRecoveryBoundaryFailure(error)) { }
        try { QuarantineCompileOrphans(); } catch (Exception error) when (IsRecoveryBoundaryFailure(error)) { }
    }

    private void RecoverCompileOperation(string operationPath, JsonObject operation)
    {
        var operationId = operation["operation_id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(operationId)) return;
        var reservationPath = ExistingOrStrictCompileReservationPath(operationId);
        FileStream? targetLease = null;
        var state = operation["state"]?.GetValue<string>();
        if (state is "failed_recovery" or "quarantined")
        {
            DeleteIfExists(reservationPath);
            return;
        }
        if (state == "committed")
        {
            try
            {
                targetLease = TryAcquireCompileRecoveryLease(operation);
                if (targetLease is null) return;
                ReadCommittedCompileSettlement(operation, operation["proof_id"]?.GetValue<string>() ?? string.Empty, operation["request_digest"]?.GetValue<string>() ?? string.Empty);
                CleanupCompiledPrevious(ResolveWorkspacePath(operation["output_root"]?.GetValue<string>()));
                DeleteIfExists(reservationPath);
                return;
            }
            catch (Exception error) when (IsRecoverableFailure(error) && !IsStrictLegacyConflict(error))
            {
                QuarantineCompileArtifacts(operation);
                operation["state"] = "quarantined";
                operation["failure_code"] = "WB-AUTHORITY-RECOVERY-409";
                operation["failure_detail"] = error.Message;
                WriteAtomic(operationPath, operation);
                DeleteIfExists(reservationPath);
                return;
            }
            finally { targetLease?.Dispose(); }
        }

        try
        {
            targetLease = TryAcquireCompileRecoveryLease(operation);
            if (targetLease is null) return;
            var markerPath = ExistingOrStrictCompileMarkerPath(operationId);
            if (File.Exists(markerPath))
            {
                try
                {
                    var candidate = operation.DeepClone().AsObject();
                    candidate["state"] = "committed";
                    ReadCommittedCompileSettlement(candidate, operation["proof_id"]?.GetValue<string>() ?? string.Empty, operation["request_digest"]?.GetValue<string>() ?? string.Empty);
                    operation["state"] = "committed";
                    operation["committed_at"] = DateTimeOffset.UtcNow.ToString("O");
                    WriteAtomic(operationPath, operation);
                    AppendCompileJournalOnce(operation);
                    CleanupCompiledPrevious(ResolveWorkspacePath(operation["output_root"]?.GetValue<string>()));
                    DeleteIfExists(reservationPath);
                    return;
                }
                catch (Exception error) when (IsRecoverableFailure(error) && !IsStrictLegacyConflict(error))
                {
                    QuarantineCompileArtifacts(operation);
                    operation["state"] = "quarantined";
                    operation["failure_code"] = "WB-AUTHORITY-RECOVERY-409";
                    operation["failure_detail"] = error.Message;
                    WriteAtomic(operationPath, operation);
                    DeleteIfExists(reservationPath);
                    return;
                }
            }

            if (TryRestorePreviousCompileTarget(operation))
            {
                QuarantineCompileTransientArtifacts(operation);
                operation["state"] = "failed_recovery";
                operation["failure_code"] = "WB-AUTHORITY-RECOVERY-409";
                WriteAtomic(operationPath, operation);
                DeleteIfExists(reservationPath);
                return;
            }

            if (HasCompileArtifacts(operation))
            {
                QuarantineCompileArtifacts(operation);
                operation["state"] = "quarantined";
            }
            else operation["state"] = "failed_recovery";
            operation["failure_code"] = "WB-AUTHORITY-RECOVERY-409";
            WriteAtomic(operationPath, operation);
            DeleteIfExists(reservationPath);
        }
        finally { targetLease?.Dispose(); }
    }

    private bool HasCompileArtifacts(JsonObject operation)
    {
        var replacementPhase = operation["replacement_phase"]?.GetValue<string>();
        var stagingRelative = operation["staging_relative"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(stagingRelative))
        {
            try { if (Directory.Exists(ResolveWorkspacePath(stagingRelative))) return true; } catch (InvalidOperationException) { return true; }
        }
        var targetRelative = operation["target_relative"]?.GetValue<string>();
        if (replacementPhase is "target_replaced" or "candidate_written" && !string.IsNullOrWhiteSpace(targetRelative))
        {
            try { if (Directory.Exists(ResolveWorkspacePath(targetRelative))) return true; } catch (InvalidOperationException) { return true; }
        }
        var outputRelative = operation["output_root"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(outputRelative)) return false;
        try
        {
            var outputRoot = ResolveWorkspacePath(outputRelative);
            if (replacementPhase is "target_replaced" or "candidate_written" && Directory.Exists(outputRoot)) return true;
            var parent = Directory.GetParent(outputRoot)?.FullName;
            var name = Path.GetFileName(outputRoot);
            return parent is not null && Directory.Exists(parent) && Directory.EnumerateFileSystemEntries(parent, $".{name}.*").Any();
        }
        catch (InvalidOperationException) { return true; }
    }

    private static bool IsRecoverableFailure(Exception error)
        => error is InvalidOperationException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException;

    private static bool IsRecoveryBoundaryFailure(Exception error)
        => error is InvalidOperationException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException;

    private static bool IsSafeIdConflict(Exception error)
        => error is InvalidOperationException invalid
            && invalid.Message.StartsWith("WB-AUTHORITY-SAFEID-409:", StringComparison.Ordinal);

    private static bool IsStrictLegacyConflict(Exception error)
        => IsSafeIdConflict(error)
            && error.Message.Contains("strict 与 legacy", StringComparison.Ordinal);

    private void QuarantineCompileArtifacts(JsonObject operation)
    {
        var operationId = operation["operation_id"]?.GetValue<string>() ?? "unknown";
        var quarantineRoot = Path.Combine(_workspace.Root, "compiled", "quarantine", SafeId(operationId));
        Directory.CreateDirectory(quarantineRoot);
        var targetRelative = operation["target_relative"]?.GetValue<string>();
        string? outputRootPath = null;
        try { outputRootPath = ResolveWorkspacePath(operation["output_root"]?.GetValue<string>()); } catch (InvalidOperationException) { }
        if (!string.IsNullOrWhiteSpace(targetRelative))
        {
            try
            {
                var target = ResolveWorkspacePath(targetRelative);
                if (Directory.Exists(target) || File.Exists(target))
                {
                    if (outputRootPath is not null && string.Equals(target, outputRootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var artifactRoot = Path.Combine(quarantineRoot, "artifact");
                        foreach (var child in Directory.EnumerateFileSystemEntries(target).Where(path => !string.Equals(Path.GetFileName(path), "quarantine", StringComparison.OrdinalIgnoreCase)))
                            MoveToQuarantine(child, artifactRoot);
                        if (!Directory.EnumerateFileSystemEntries(target).Any()) Directory.Delete(target);
                    }
                    else MoveToQuarantine(target, quarantineRoot);
                }
            }
            catch (InvalidOperationException) { }
        }
        var outputRelative = operation["output_root"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(outputRelative)) return;
        try
        {
            var outputRoot = ResolveWorkspacePath(outputRelative);
            var parent = Directory.GetParent(outputRoot)?.FullName;
            var name = Path.GetFileName(outputRoot);
            if (parent is null || !Directory.Exists(parent)) return;
            foreach (var path in Directory.EnumerateFileSystemEntries(parent, $".{name}.*"))
            {
                if (Path.GetFileName(path).EndsWith(".compile-lease", StringComparison.OrdinalIgnoreCase)) continue;
                MoveToQuarantine(path, quarantineRoot);
            }
        }
        catch (InvalidOperationException) { }
        var stagingRelative = operation["staging_relative"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(stagingRelative))
        {
            try
            {
                var staging = ResolveWorkspacePath(stagingRelative);
                if (Directory.Exists(staging) || File.Exists(staging)) MoveToQuarantine(staging, Path.Combine(quarantineRoot, "candidate"));
            }
            catch (InvalidOperationException) { }
        }
        foreach (var relative in new[] { operation["result_relative"]?.GetValue<string>(), Relative(CompileMarkerStrictPath(operationId)), Relative(CompileMarkerLegacyPath(operationId)) })
        {
            if (string.IsNullOrWhiteSpace(relative)) continue;
            try
            {
                var path = ResolveWorkspacePath(relative);
                if (File.Exists(path)) MoveToQuarantine(path, Path.Combine(quarantineRoot, "records"));
            }
            catch (InvalidOperationException) { }
        }
    }

    private static void MoveToQuarantine(string source, string quarantineRoot)
    {
        Directory.CreateDirectory(quarantineRoot);
        var destination = Path.Combine(quarantineRoot, Path.GetFileName(source));
        if (Directory.Exists(destination) || File.Exists(destination)) destination = Path.Combine(quarantineRoot, Path.GetFileName(source) + "." + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(source)) Directory.Move(source, destination);
        else if (File.Exists(source)) File.Move(source, destination);
    }

    private void QuarantineCompileOrphans()
    {
        var compiledRoot = Path.Combine(_workspace.Root, "compiled");
        if (!Directory.Exists(compiledRoot)) return;
        var tracked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var operationPath in Directory.EnumerateFiles(OperationsRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var operation = ReadJson(operationPath);
                if (operation["kind"]?.GetValue<string>() != CompileSettlementSupport.Kind
                    || operation["state"]?.GetValue<string>() == "quarantined") continue;
                var target = operation["target_relative"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(target) && operation["state"]?.GetValue<string>() is "reserved" or "prepared" or "executing")
                    target = operation["output_root"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(target))
                    tracked.Add(Path.GetFullPath(ResolveWorkspacePath(target)));
            }
            catch (InvalidOperationException) { }
        }
        var orphanRoot = Path.Combine(compiledRoot, "quarantine", "orphans");
        foreach (var path in Directory.EnumerateFileSystemEntries(compiledRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(path);
            if (name.Equals("quarantine", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".", StringComparison.Ordinal)) continue;
            if (!tracked.Contains(Path.GetFullPath(path))) MoveToQuarantine(path, orphanRoot);
        }
    }

    private void ReconcileCompileReservations()
    {
        if (!Directory.Exists(CompileReservationsRoot)) return;
        foreach (var reservationPath in Directory.EnumerateFiles(CompileReservationsRoot, "*.json", SearchOption.TopDirectoryOnly).ToArray())
        {
            try
            {
                var reservation = ReadJson(reservationPath);
                var operationId = reservation["operation_id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(operationId)) throw new InvalidOperationException("invalid reservation");
                var operationPath = ResolveOperationPath(operationId);
                if (!File.Exists(operationPath))
                {
                    MoveToQuarantine(reservationPath, Path.Combine(Root, "quarantine", "reservations"));
                    continue;
                }
                var operation = ReadJson(operationPath);
                if (operation["kind"]?.GetValue<string>() != CompileSettlementSupport.Kind
                    || operation["operation_id"]?.GetValue<string>() != operationId
                    || operation["request_digest"]?.GetValue<string>() != reservation["request_digest"]?.GetValue<string>()
                    || operation["fence_token"]?.GetValue<string>() != reservation["fence_token"]?.GetValue<string>())
                    throw new InvalidOperationException("reservation binding mismatch");
                if (operation["state"]?.GetValue<string>() is "committed" or "failed_recovery" or "quarantined")
                    DeleteIfExists(reservationPath);
            }
            catch (Exception error) when (IsRecoveryBoundaryFailure(error) && !IsStrictLegacyConflict(error))
            {
                try { MoveToQuarantine(reservationPath, Path.Combine(Root, "quarantine", "reservations")); } catch (Exception moveError) when (IsRecoveryBoundaryFailure(moveError)) { }
            }
        }
    }

    private void QuarantineOperationRecord(string operationPath)
    {
        try { MoveToQuarantine(operationPath, Path.Combine(Root, "quarantine", "operations")); }
        catch (Exception error) when (IsRecoveryBoundaryFailure(error)) { }
    }

    private bool TryRestorePreviousCompileTarget(JsonObject operation)
    {
        var outputRelative = operation["output_root"]?.GetValue<string>();
        var expectedHash = operation["previous_manifest_hash"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(outputRelative) || !IsHex(expectedHash ?? string.Empty, 64)) return false;
        var expectedManifestHash = expectedHash!;
        var outputRoot = ResolveWorkspacePath(outputRelative);
        var parent = Directory.GetParent(outputRoot)?.FullName;
        var name = Path.GetFileName(outputRoot);
        if (parent is null || !Directory.Exists(parent)) return false;
        var previousRelative = operation["previous_relative"]?.GetValue<string>();
        var previous = !string.IsNullOrWhiteSpace(previousRelative)
            ? new[] { ResolveWorkspacePath(previousRelative) }
            : Directory.EnumerateFileSystemEntries(parent, $".{name}.*.previous").ToArray();
        if (previous.Length != 1 || !Directory.Exists(previous[0]) || !VerifyPreviousCompileTarget(previous[0], expectedManifestHash)) return false;
        if (Directory.Exists(outputRoot))
            MoveToQuarantine(outputRoot, Path.Combine(_workspace.Root, "compiled", "quarantine", SafeId(operation["operation_id"]?.GetValue<string>() ?? "unknown"), "candidate"));
        Directory.Move(previous[0], outputRoot);
        return true;
    }

    private static bool VerifyPreviousCompileTarget(string root, string expectedManifestHash)
    {
        var manifestPath = Path.Combine(root, "manifest.json");
        var checksumsPath = Path.Combine(root, "SHA256SUMS.txt");
        if (!File.Exists(manifestPath) || !File.Exists(checksumsPath) || !string.Equals(Hashing.FileSha256(manifestPath), expectedManifestHash, StringComparison.OrdinalIgnoreCase)) return false;
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(checksumsPath, Encoding.ASCII))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var separator = line.IndexOf("  ", StringComparison.Ordinal);
            if (separator <= 0) return false;
            var hash = line[..separator];
            var fileName = line[(separator + 2)..].Trim();
            if (!IsHex(hash, 64) || string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName) || fileName.Contains("..", StringComparison.Ordinal) || fileName.Contains('\\') || !expected.TryAdd(fileName, hash)) return false;
        }
        var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !string.Equals(path, "SHA256SUMS.txt", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected.Keys)) return false;
        foreach (var fileName in expected.Keys)
        {
            var path = Path.Combine(root, fileName.Replace('/', Path.DirectorySeparatorChar));
            if (!string.Equals(Hashing.FileSha256(path), expected[fileName], StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private void QuarantineCompileTransientArtifacts(JsonObject operation)
    {
        var outputRelative = operation["output_root"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(outputRelative)) return;
        var outputRoot = ResolveWorkspacePath(outputRelative);
        var parent = Directory.GetParent(outputRoot)?.FullName;
        var name = Path.GetFileName(outputRoot);
        if (parent is null || !Directory.Exists(parent)) return;
        var quarantineRoot = Path.Combine(_workspace.Root, "compiled", "quarantine", SafeId(operation["operation_id"]?.GetValue<string>() ?? "unknown"), "transient");
        foreach (var path in Directory.EnumerateFileSystemEntries(parent, $".{name}.*").ToArray())
            MoveToQuarantine(path, quarantineRoot);
    }

    private void AppendCompileJournalOnce(JsonObject operation)
    {
        var operationId = operation["operation_id"]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(operationId)) throw new InvalidOperationException("WB-AUTHORITY-OPERATION-422: operation_id 缺失。");
        if (File.Exists(JournalPath) && File.ReadLines(JournalPath).Any(line => IsCompileJournalEntryForOperation(line, operationId))) return;
        AppendJournal(new JsonObject { ["operation_id"] = operationId, ["kind"] = CompileSettlementSupport.Kind, ["state"] = "committed", ["request_digest"] = operation["request_digest"]?.DeepClone(), ["manifest_hash"] = operation["manifest_hash"]?.DeepClone() });
    }

    private static bool IsCompileJournalEntryForOperation(string line, string operationId)
    {
        try
        {
            var entry = JsonNode.Parse(line)?.AsObject();
            return entry?["kind"]?.GetValue<string>() == CompileSettlementSupport.Kind
                && entry["operation_id"]?.GetValue<string>() == operationId;
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private void CleanupCompiledPrevious(string outputRoot)
    {
        var parent = Directory.GetParent(outputRoot)?.FullName;
        var name = Path.GetFileName(outputRoot);
        if (parent is null || !Directory.Exists(parent)) return;
        var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var operationPath in Directory.EnumerateFiles(OperationsRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var operation = ReadJson(operationPath);
                if (operation["kind"]?.GetValue<string>() != CompileSettlementSupport.Kind
                    || operation["state"]?.GetValue<string>() is not ("reserved" or "prepared" or "executing" or "candidate_written")) continue;
                var previousRelative = operation["previous_relative"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(previousRelative))
                    protectedPaths.Add(Path.GetFullPath(ResolveWorkspacePath(previousRelative)));
            }
            catch (InvalidOperationException) { }
        }
        foreach (var path in Directory.EnumerateFileSystemEntries(parent, $".{name}.*.previous"))
        {
            try
            {
                if (protectedPaths.Contains(Path.GetFullPath(path))) continue;
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private bool ValidatePublishProof(string proofPath, JsonObject pointer)
    {
        try
        {
            var proof = ReadJson(proofPath);
            if (HashWithout(proof, "proof_hash") != proof["proof_hash"]?.GetValue<string>()) return false;
            if (proof["pointer"] is not JsonObject expectedPointer || CanonicalJson.Serialize(expectedPointer) != CanonicalJson.Serialize(pointer)) return false;
            var stagingPath = ResolveWorkspacePath(proof["staging_path"]?.GetValue<string>());
            var manifestPath = Path.Combine(stagingPath, "manifest.json");
            return File.Exists(manifestPath) && string.Equals(Hashing.FileSha256(manifestPath), proof["manifest_hash"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException) { return false; }
    }

    private bool WriteRecoveredPointer(string pointerPath, JsonObject pointer)
    {
        WriteAtomic(pointerPath, pointer);
        return true;
    }

    private void ApplyHeadEntry(JsonObject entry)
    {
        var head = ReadJson(HeadPath);
        var documents = head["documents"] as JsonObject ?? new JsonObject();
        documents[entry["document_id"]!.GetValue<string>()] = entry.DeepClone();
        head["documents"] = documents;
        head["head_revision"] = (head["head_revision"]?.GetValue<int>() ?? 0) + 1;
        WriteAtomic(HeadPath, head);
    }

    private JsonObject ReadJson(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException($"WB-AUTHORITY-404: authority record 不存在。{Relative(path)}");
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidOperationException("WB-AUTHORITY-422: authority record 无效。");
    }

    private void AppendJournal(JsonObject entry)
    {
        Directory.CreateDirectory(Root);
        File.AppendAllText(JournalPath, CanonicalJson.Serialize(entry) + Environment.NewLine, Encoding.UTF8);
    }

    private void WriteAtomic(string path, JsonNode value) => WriteAtomic(path, Encoding.UTF8.GetBytes(CanonicalJson.Serialize(value)));
    private static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        File.Move(temp, path, true);
    }

    private string Relative(string path) => Path.GetRelativePath(_workspace.Root, path).Replace('\\', '/');
    private static string SafeId(string value) => value.Replace("/", "_", StringComparison.Ordinal).Replace("\\", "_", StringComparison.Ordinal).Replace("..", "_", StringComparison.Ordinal);
    private static void RequireId(string value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"WB-AUTHORITY-400: {name} 不能为空。"); }
    private static string HashWithout(JsonObject value, string property)
    {
        var clone = value.DeepClone().AsObject();
        clone.Remove(property);
        return CanonicalJson.Hash(clone);
    }
}

internal sealed record AuthorityDocumentRevision(string DocumentId, string Path, int Revision, string ContentHash, string RecordPath);
internal sealed record AuthoritySelectionSnapshot(string SelectionId, string SelectionHash, int ItemCount);
internal sealed record AuthorityApprovalProof(string ApprovalId, string ProofHash, string SelectionId);
internal sealed record AuthorityCompileProof(string CompileProofId, string ProofHash, string ContentTier, int ItemCount);
internal sealed record AuthorityCompileSettlement(CompileResult Result, string CompiledPath, JsonObject? PersistedEnvelope = null);
internal sealed class AuthorityMutationUnknownException : InvalidOperationException
{
    public AuthorityMutationUnknownException(Exception? cause = null)
        : base("WB-AUTHORITY-MUTATION-UNKNOWN: 编译已进入持久化流程，但最终结算状态未知。"
               + (cause is null ? "" : $" 原因: {cause.GetType().Name}: {cause.Message}"), cause) { }
}

internal enum CompileSettlementFaultPoint
{
    AfterReservation,
    AfterPrepared,
    AfterExecuting,
    AfterTempCandidate,
    AfterCandidateWritten,
    AfterTargetReplace,
    AfterResultWrite,
    AfterMarkerWrite,
    AfterJournalAppend
}

internal static class AuthorityGateRoutes
{
    private static readonly string[] RetiredPrefixes = ["/api/ai/batch/", "/api/ai/draft/"];
    private static readonly string[] RetiredPaths =
    [
        "/api/save-authoring",
        "/api/save-authoring/check",
        "/api/save-editor-document",
        "/api/document/new",
        "/api/compile",
        "/api/export",
        "/api/ai/apply",
        "/api/ai/reject"
    ];

    public static bool IsRetired(string method, string? path)
    {
        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)) return false;
        var value = path ?? string.Empty;
        return RetiredPaths.Contains(value, StringComparer.OrdinalIgnoreCase)
            || RetiredPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}

