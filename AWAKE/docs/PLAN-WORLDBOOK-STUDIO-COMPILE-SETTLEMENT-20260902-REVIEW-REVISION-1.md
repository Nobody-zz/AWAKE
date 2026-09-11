# Worldbook Studio compile settlement review revision 1

- Parent plan: `PLAN-WORLDBOOK-STUDIO-COMPILE-SETTLEMENT-20260902.md`
- Review target: `WORLDBOOK-STUDIO-COMPILE-SETTLEMENT-20260902`
- Revision: `1`
- Status: `REVISE_PENDING_REVIEW`

This revision is authoritative over the parent plan and converts the settlement batch from a design checklist into an implementable contract. It does not authorize SafeId migration, route-registry edits, Draft/Batch unification, AI segmentation changes, game-directory synchronization, version changes, or real Provider access.

## 1. Canonical inputs and operation identity

The customer compile request is the tuple:

```text
proof_id
confirmation_token
output_root
```

The server reads the persisted proof before deriving the request digest. The proof contributes `proof_id`, `proof_hash`, `selection_id`, `selection_hash`, `content_tier`, and the ordered proof item paths. All string values are Unicode NFC normalized; no trimming or case folding is applied to identifiers, hashes, tiers, or paths beyond the existing output-root policy. A missing confirmation token is represented by the empty string.

`output_root` is normalized by the existing workspace policy first, then represented for hashing as a lower-case workspace-relative identity. An output outside the allowed workspace remains rejected before reservation.

The canonical digest object has exactly these fields in ordinal key order:

```json
{
  "schema_version": "awake.worldbook.compile-request.v1",
  "workspace_identity": "workspace:<normalized-workspace-hash>",
  "proof_id": "<NFC proof id>",
  "proof_hash": "<lowercase sha256 hex>",
  "selection_id": "<NFC selection id>",
  "selection_hash": "<lowercase sha256 hex>",
  "content_tier": "<NFC content tier>",
  "output_root": "<lowercase workspace-relative identity>",
  "confirmation_token_hash": "<lowercase sha256 of UTF-8 token or empty string>"
}
```

The object is serialized as UTF-8 without BOM using the repository canonical JSON implementation, then hashed with SHA-256 and emitted as 64 lower-case hexadecimal characters. No timestamps, process IDs, random values, or absolute paths enter the digest.

The logical operation ID is proof-derived and stable across output/token retries:

```text
customer.compile.v1.<hex-sha256>
```

where the suffix is SHA-256 of UTF-8 bytes for `awake.worldbook.compile.operation.v1\0` followed by the NFC `proof_id`, emitted as 64 lower-case hexadecimal characters. Therefore the same proof with the same request replays, while the same proof with a changed output, token, proof hash, or workspace binding addresses the same operation and deterministically conflicts.

The operation ID is composed only of ASCII characters and may be used with the existing filename mapping without changing SafeId semantics in this batch.

## 2. Persisted schema and state machine

The operation record is `authoring-v1/operations/<existing-safe-id(operation_id)>.json`. Required fields are:

```text
schema_version = awake.worldbook.compile-operation.v1
operation_id
kind = compile_runtime
state
request_digest
workspace_identity
proof_id
proof_hash
content_tier
output_root
confirmation_token_hash
owner_instance_id
fence_token
reservation_created_at
prepared_at
candidate_relative
target_relative
manifest_hash
result_relative
result_hash
failure_code (only for failed_recovery/quarantined)
created_at
committed_at (only for committed)
```

The full result is persisted separately at `authoring-v1/operations/results/<operation-safe-id>.json` with `schema_version = awake.worldbook.compile-result.v1`, the operation binding fields, the complete public compile result needed by Web and CLI replay, and a `result_hash` over its canonical UTF-8 JSON.

Legal states and transitions are:

```text
reserved         -> prepared | failed_recovery
prepared         -> executing | failed_recovery
executing        -> candidate_written | failed_recovery | quarantined
candidate_written-> committed | failed_recovery | quarantined
committed        -> committed            (read-only replay)
failed_recovery  -> failed_recovery      (read-only deterministic 409)
quarantined      -> quarantined          (read-only deterministic 409)
```

`reserved`, `prepared`, `executing`, and `candidate_written` are non-terminal. `committed`, `failed_recovery`, and `quarantined` are terminal. There is no automatic retry or takeover after a crash in this batch; a future explicit retry contract may create a new operation only after a separate review.

External behavior:

- `committed` with matching digest returns the persisted result without compiling, writing a candidate, or appending a second journal entry.
- Any existing state with a different digest returns `WB-AUTHORITY-OPERATION-409` and leaves all files unchanged.
- `reserved`, `prepared`, `executing`, or `candidate_written` after restart are reconciled once to `failed_recovery` or `quarantined` by the recovery rules below, then return deterministic `409`.
- `failed_recovery` and `quarantined` always return deterministic `409`; they are never silently re-executed.

## 3. Cross-process reservation and fencing

Reservation uses one exclusive filesystem creation as the authority: `FileMode.CreateNew` for the operation record in a dedicated `operations/reservations` path. The losing process receives `WB-AUTHORITY-OPERATION-409` after reading and comparing the existing `request_digest`; it never enters compilation.

The reservation contains a cryptographically random `owner_instance_id` and `fence_token`, the current process ID plus process start timestamp for diagnostics, and `reservation_created_at`. The fence token is copied into every later operation, candidate, result, and marker record. A writer may advance the operation only when its fence token matches the persisted reservation; a mismatched writer fails closed with `WB-AUTHORITY-OPERATION-409`.

There is no lease-based takeover. On startup, a reservation without a valid committed operation is converted to `failed_recovery` with `failure_code = WB-AUTHORITY-RECOVERY-409`; this prevents PID reuse or stale-owner guesses from taking over a partially executed compile. The reserved file is retained as the durable tombstone.

## 4. Marker, result, and manifest integrity

The marker is `authoring-v1/commit-markers/<existing-safe-id(operation_id)>.json`. Its canonical signed fields are exactly:

```text
schema_version, operation_id, kind, state, request_digest,
workspace_identity, proof_id, proof_hash, content_tier,
output_root, owner_instance_id, fence_token,
candidate_relative, target_relative, manifest_hash,
result_relative, result_hash, committed_at
```

The marker stores `integrity_algorithm = HMAC-SHA256` and `integrity_key_version`, plus `integrity_tag`. The tag is computed over canonical UTF-8 JSON of the fields above, excluding only `integrity_tag` itself. The key is an OS-protected per-user key stored outside the workspace at `%LOCALAPPDATA%\AWAKE\WorldbookStudio\authority-v1.key.dpapi`; it is created once with restrictive ACLs and protected with Windows DPAPI. Missing, unreadable, or version-mismatched key material causes fail-closed recovery; the system must not generate a replacement key while claiming an old operation is committed.

Recovery trusts a committed compile only when all of these verify together: marker HMAC, operation ID/kind/state, request digest, proof hash, workspace/output identity, fence token, target/candidate relative paths, result hash, result contents, manifest hash, and the required compiled file hashes. A marker self-hash without the external key is insufficient and is not accepted.

## 5. Persistence ordering and crash windows

Every JSON write uses UTF-8 without BOM, writes to a same-directory temporary file, calls `Flush(true)`, then atomically replaces/renames the destination. Directory creation and rename boundaries are recorded in the fault-injection seam.

The only valid write order is:

```text
reserve
→ operation=reserved
→ operation=prepared + request fields
→ operation=executing
→ candidate files + candidate manifest
→ operation=candidate_written
→ result file
→ authenticated marker
→ operation=committed
→ one journal append
→ HTTP/CLI response
```

The recovery result for each crash point is unique:

| Crash point | Restart action | Final state |
|---|---|---|
| before reservation | no record exists | no-op; next request may reserve |
| after reservation before operation prepared | retain reservation tombstone | `failed_recovery` |
| after prepared before executing | no candidate may be adopted | `failed_recovery` |
| during executing before candidate is complete | quarantine any candidate/temp files | `quarantined` |
| after `.tmp` creation | never adopt `.tmp`; move it under `compiled/quarantine/<operation-id>/` | `quarantined` |
| after candidate complete before `candidate_written` | verify all candidate hashes; adopt only through the recorded fence and exact target identity, otherwise quarantine | `committed` or `quarantined` |
| after target replacement before result/marker | if target manifest and all files match the request, write result then marker and commit; otherwise restore valid `.previous` or quarantine | `committed`, `failed_recovery`, or `quarantined` per verification result |
| after result before marker | verify result and candidate; write authenticated marker, otherwise quarantine | `committed` or `quarantined` |
| after marker before operation committed | verify marker/result/target, then write committed operation and one journal entry | `committed` |
| after journal append before response | replay committed persisted result | `committed` |
| `.previous` without a valid current target | restore only when its manifest matches the last committed operation; otherwise retain as quarantine input | `failed_recovery` or `quarantined` |
| orphan compiled output with no matching operation/marker | never publish or replay it; move once to quarantine | no consumer-visible artifact |

The implementation must not use “adopt or block” as an unresolved choice: the table above is the only allowed outcome policy.

## 6. Replay and conflict matrix

| Condition | Result | Side effect |
|---|---|---|
| no operation, valid proof/digest | execute one compile | committed result |
| committed operation + identical digest | return persisted result | no compile/write/journal |
| any existing operation + different digest | `WB-AUTHORITY-OPERATION-409` | no mutation |
| non-terminal record while same process is active | `WB-AUTHORITY-OPERATION-409` | no second execution |
| non-terminal record after restart | recovery result above, then `409` | no blind retry |
| failed result/marker/manifest verification | `WB-AUTHORITY-OPERATION-409` | no replay; quarantine if needed |
| invalid proof or output before reservation | mapped preflight error | `side_effect=none` |
| exception after reservation | mapped mutation-unknown error | `side_effect=unknown` |

Web and CLI consume the same persisted result envelope and map the same stable operation codes. Public path redaction is handled by the later public-wire-contract batch; this batch must not invent a second result shape.

## 7. Executable fault-injection fixtures

The Core test seam exposes named points matching the crash table: `AfterReservation`, `AfterPrepared`, `AfterExecuting`, `AfterTempCandidate`, `AfterCandidateWritten`, `AfterTargetReplace`, `AfterResultWrite`, `AfterMarkerWrite`, and `AfterJournalAppend`. Each point throws a test-only exception after the named durable boundary and before the next one.

Required tests have fixed intent:

- `CompileSettlement_SameRequest_ReplaysPersistedResultWithoutSecondCompile`
- `CompileSettlement_SameProofChangedOutput_ReturnsConflictWithoutMutation`
- `CompileSettlement_TwoProcesses_OnlyOneReservationWinner`
- `CompileSettlement_Restart_ReconcilesEveryCrashPointToUniqueState`
- `CompileSettlement_TamperedMarkerResultManifest_IsNotAdopted`
- `CompileSettlement_TmpPreviousAndOrphanOutputs_FollowQuarantinePolicy`
- `CompileSettlement_WebAndCli_ReplaySamePersistedResult`
- `CompileSettlement_InvalidPreflight_CreatesNoReservationOrJournal`

Assertions must include operation count, journal line count, compile invocation count, exact terminal state, candidate/quarantine paths, manifest/file hashes, replay equality, and response status/code. No test may use a real Provider or Bannerlord directory.

## 8. Acceptance and non-goals

The batch is accepted only when the entry path `/api/authoring/compile` reaches reservation, compile, persisted settlement, replay/conflict projection, and observable Web/CLI result under all cases above. Build success alone is insufficient.

SafeId compatibility, route registry reconciliation, successful public path projection, Draft/Batch unification, AI segmentation UX, game sync, package versioning, and real Provider access remain explicit non-goals.

Implementation remains blocked until this revision receives terminal review approval and explicit user sign-off.
