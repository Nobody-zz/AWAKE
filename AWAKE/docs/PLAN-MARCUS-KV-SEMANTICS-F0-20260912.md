# Plan: Marcus KV 基础语义契约（F0）

> Status: `revised_after_review_round_3`  
> Owner: embedded Marcus Framework storage contract.  
> Baseline: BuildId `awake-20260911-g3b-persona-storage-001`, Git HEAD `b7eacaa4f679dfcdeb8a45b16337a35f60636a1f`; pre-existing dirty files must be fingerprinted before implementation.

## Goal

Make two existing `IKeyValueStore` behaviors explicit, testable and consistent across the default SQLite path and the injected/file path, without widening the interface or adding AWAKE-specific services:

1. A successful `SetAsync(key, value)` is an atomic replacement visible to readers as either the entire old value or the entire new value.
2. A successfully read absent key is represented as `OperationResult<string>.Succeeded(null)`, distinct from a present empty string.

## Contract boundary

Public surface owner: `framework/MarcusAwakeFramework/src/StorageAndRagApi.cs`, members `IKeyValueStore.GetAsync` and `IKeyValueStore.SetAsync`.

Existing callers include `WorldStateStore` and framework storage consumers. The three distinct paths are: FrameworkHost default composition (`UnavailableStorageService`, which must remain unavailable), Runtime Service composition (SQLite backend), and AWAKE's explicit injected file-backed `JsonFileKeyValueStore`. F0 neither makes the default host acquire storage nor treats SQLite as the AWAKE default. SQLite and the injected file path must each have observable tests; the unavailable default path must prove it remains unavailable.

No new interface member, IPC message, capability, permission or automatic host service is introduced.

## Exact semantics

### Atomic replacement

For a valid, authorized key, `SetAsync` is linearizable per key within an adapter process: after it returns success, a completed `GetAsync` returns the complete new string; a concurrent reader sees the complete old string or the complete new string, never partial/truncated/corrupt data. A failed or cancelled Set makes no success claim. This does not promise cross-process compare-and-swap or multi-key transactions.

### Missing versus empty

- Absent key: successful `GetAsync` with `Value == null`.
- Present empty value: successful `GetAsync` with `Value == string.Empty`.
- Read failure: unsuccessful result with its existing error code.

This is a behavioral compatibility change for the file adapter's current missing-key empty-string response. It is required by S0's `missing/empty/corrupt/unavailable` distinction and must be covered by a migration/regression fixture. The guarantee is at the adapter boundary; legacy `WorldStateStore` callers that intentionally collapse null/empty/whitespace remain unchanged. S0 must consume a dedicated typed raw-read path rather than claim every existing caller preserves this distinction.

## Implementation scope

Expected write set:

- `framework/MarcusAwakeFramework/src/StorageAndRagApi.cs` (semantic documentation only unless review proves a public declaration change is necessary);
- `framework/MarcusAwakeStorage/src/SqliteStorageAndRagBackend.cs`, Runtime Service composition fixture, and storage tests;
- `src/AwakeFileStorageService.cs` and focused tests, including an explicit owner/campaign/timeline/session/generation binding captured when a namespace opens;
- `docs/contracts/MARCUS-KV-SEMANTICS-v1.json`;
- `tools/verify-framework-contract-change.ps1` because the required verifier is absent from this checkout.

The verifier must validate the versioned record fields, changed public members, unavailable-default/SQLite/injected-file path test names and verdicts, callers and deterministic evidence paths. It must fail closed for `pending` or `not_wired`, and reject missing/nonexistent test entrypoints or paths. It must not claim a runtime feature exists.

### Injected file-store requirements

The file store captures a namespace lease (owner, campaign, timeline, session and generation) when it opens. Every Get/Set/Delete validates the caller context against that lease, rejects generation zero/mismatch with `storage.scope_denied`, and honors a pre-cancelled cancellation token before reading/writing. Existing `CancellationToken.None` callers remain allowed; this does not create a new cancellation requirement for them.

The file store persists an envelope `{schemaVersion, lease, values}` and caches by `(path, leaseFingerprint)`, not path alone. Reopening under a different owner/campaign/timeline/session/generation must return `storage.scope_denied`; an unbound legacy file is `storage.legacy_unbound` and is never silently attributed to a new lease. The subsequent migration/retention choice for such legacy files is explicitly out of F0 scope.

File writes operate on a copied candidate dictionary and an atomic temporary-file replacement. The in-memory dictionary changes only after persistent replacement succeeds. A failed/cancelled Save before replacement leaves current-process Get and a newly reopened store reading the prior persisted value. A post-replacement exception returns `storage.commit_unknown`: the caller must reopen and compare the complete intended envelope before treating the write as committed; F0 never claims the old value remains in that case. Tests cover both outcomes, cancellation, concurrent readers, process reopen, and complete-old/complete-new visibility.

Cancellation precedence is fixed: a pre-cancelled caller token returns `awake.cancelled` before I/O; a stale/mismatched session lease returns `storage.scope_denied`; an expired request deadline returns `storage.deadline_exceeded`; otherwise `CancellationToken.None` is valid and relies on the request deadline/session lifecycle. SQLite and file paths must implement these same externally visible outcomes.

## Acceptance

| Case | Observable result | Evidence |
| --- | --- | --- |
| Unavailable default | FrameworkHost default storage remains unavailable; F0 does not silently inject a store | framework fixture |
| SQLite replace | Concurrent reader fixture observes only complete old/new values through repeated Set/Get | framework storage test |
| File replace | Equivalent injected/file fixture observes only complete old/new values, including after reopen | focused AWAKE storage test |
| SQLite missing | Missing key returns successful null; stored empty returns successful empty | framework storage test |
| File missing | Missing key returns successful null; stored empty returns successful empty | focused AWAKE storage test |
| Identity preservation | Wrong owner/campaign/timeline/session/generation is rejected in SQLite and file paths; no broadening of access | negative fixture |
| File failure | Failed/cancelled Save retains the old value in memory and after a new store opens | fault-injection fixture |
| Contract record | Versioned record and verifier pass with default and injected verdicts | verifier command |
| Commit unknown | Post-replacement failure returns `storage.commit_unknown`; exact reopen-and-compare confirmation is required before success | fault-injection fixture |
| Runtime composition | Runtime Service business adapter reaches SQLite with the same KV semantics/result mapping | composition fixture |

## Non-goals

- No CAS, transactions, multi-key commit, list-keys API or storage capacity change.
- No world facts, manifest/chunk implementation, weekly report, UI, worldbook, Persona, Native Knowledge or dialogue work.
- No game sync, Bannerlord launch, provider/cloud call, release or version bump.

## Gate

`plan_status = revised_after_review_round_3`  
`review_status = revision_required; review_cap_exhausted`  
`user_signoff_required = yes, after APPROVED`  
`minimum_evidence = E2; F0 does not claim E4/E5`  

Next action: user decides whether to split F0 into adapter semantics and verifier/composition batches, or authorize one exceptional extra review round. No implementation is authorized.

## Round 3 lock changes (supersedes conflicting earlier wording)

### Exact cancellation and deadline matrix

For SQLite and file adapters, operation outcome priority is fixed as follows: a pre-cancelled caller token or cancelled session-lifecycle token returns `awake.cancelled` before I/O; then invalid owner/campaign/timeline/session/generation returns `storage.scope_denied`; then an expired request deadline returns `storage.deadline_exceeded`; otherwise `CancellationToken.None` is valid. SQLite must no longer reject `None` solely because it is non-cancellable, and its legacy `storage.deadline_expired` outcome is normalized to `storage.deadline_exceeded`. File/UI-dispatch cancellation must be caught and returned as `awake.cancelled`, not leaked as a faulted task. Fixtures cover every pairwise competing condition and both adapters.

### File envelope, cache and legacy rule

The only new file format is canonical JSON:

```text
schemaVersion = awake.file-kv-envelope.v1
lease = { ownerId, campaignId, timelineId, sessionId, generation }
values = object<string,string>
```

`generation` is a positive integer. Lease fingerprint is uppercase SHA-256 of UTF-8 `ownerId\0campaignId\0timelineId\0sessionId\0generation`; cache key is normalized absolute path plus this fingerprint. Envelope lease must exactly match the opening lease. The old `{namespace,updatedUtc,values}` format is read only for classification and returns `storage.legacy_unbound`; it is neither attributed, rewritten nor deleted by F0. A mandatory fixture proves legacy classification, distinct lease cache keys, mismatch rejection and new-envelope reopen.

### F0/S0 boundary and Runtime composition

F0 proves adapter semantics and the Runtime Service SQLite composition only. It does not claim an S0 caller exists: remove S0 handoff from the F0 approval condition. After F0 E2, S0 must implement its own generation-bound typed raw-read caller and prove it in its new review/acceptance batch. The S0 plan may depend on the verified F0 contract ID, but this dependency alone is not a F0 feature claim.

F0's Runtime composition fixture starts the Runtime Service storage business adapter, opens a campaign namespace through the same production business path, and checks missing/empty, atomic replacement, identity error and cancellation/deadline result mapping. Direct `SqliteStorageAndRagBackend` tests remain unit evidence, not a substitute for this fixture.

### Verifier protocol

`tools/verify-framework-contract-change.ps1` is a required F0 implementation artifact. Its contract input has a frozen JSON schema with `additionalProperties=false`; it validates contract ID, status transition, three independent path objects, real source/test paths, declared entrypoints, changed-member statuses and verification fields. It emits one JSON result matching `marcus.framework.contract.verify.v1` with `{contractId,status,checks,errors}`; `status` is exactly `passed` or `failed`; every error has `{code,path,message}`. Exit code 0 means passed, 2 means contract/entrypoint/path validation failure, and 3 means verifier execution failure. It rejects unknown fields and any `draft`, `pending`, `planned` or `not_wired` status when asked to verify `verified_e2`. S0 dependency validation belongs to S0's later verifier, not F0's.
