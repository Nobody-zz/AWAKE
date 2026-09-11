# Worldbook Studio SafeId-A plan

- Batch: `WORLDBOOK-STUDIO-SAFEID-A-20260902`
- Risk: `high-risk`
- Review state: `docs/review-state/WORLDBOOK-STUDIO-SAFEID-COMPAT-REVISION-4-20260902.json`
- Parent batch: `WORLDBOOK-STUDIO-COMPILE-SETTLEMENT-20260902` (`APPROVED`)

## Scope

This batch changes only five compile-settlement record families: `compile_proof`, `operation`, `compile_result`, `commit_marker`, and `compile_reservation`. It does not change document revisions, selections, approvals, publish proofs, export staging, current pointer, workspace head, operation journal, public wire fields, routes, or compile settlement state transitions. Those families remain on their current paths and are future batches.

## Contract

1. Logical IDs remain unchanged in JSON. Storage keys are internal filename components and are never returned as logical IDs.
2. `StorageIdentityCanonicalizerV1` is a new private/versioned routine, independent of global `CanonicalJson`. It NFC-normalizes every string, rejects control characters and null identity values, emits fixed object keys in ordinal order, emits integers as JSON integers, emits UTF-8 without BOM, and hashes the exact bytes of `{"kind":<record-family>,"identity":<closed-object>}`. The key is `k1_{kind_code}_{lowercase_sha256_hex}`. Fixed codes are `cp=compile_proof`, `op=operation`, `res=compile_result`, `mk=commit_marker`, `rv=compile_reservation`.
3. Closed identity objects are: compile proof `{compile_proof_id}`; operation `{operation_id}`; compile result `{operation_id}`; marker `{operation_id}`; reservation `{operation_id}`. The JSON `kind` field remains the record-family discriminator; the runtime business value `compile_runtime` is validated separately and is never part of the storage key.
4. Strict paths are exactly `compile-proofs/{key}.json`, `operations/{key}.json`, `operations/results/{key}.json`, `commit-markers/{key}.json`, and `operations/reservations/{key}.json`. No alternate suffix or directory form is valid.
5. `oldSafeIdV0` is frozen to the current implementation exactly: replace `/` with `_` using ordinal replacement, then `\` with `_`, then every literal `..` with `_`; do no Unicode normalization, case folding, trimming, reserved-name rewriting, or byte conversion before these operations. Legacy paths are exactly the five current path formulas using `oldSafeIdV0`; no wildcard or recursive search is allowed.
6. If a strict path exists, parse and validate it; parse/schema/identity/family/hash failure is terminal and never falls back. If strict is valid and the exact legacy path exists, compare raw file SHA-256; equal hashes resolve to strict, different hashes return `WB-AUTHORITY-SAFEID-409` with no mutation. If strict is absent, validate the one exact legacy path; missing is `404`, invalid/mismatched is `WB-AUTHORITY-SAFEID-409`.
7. Immutable proof/result/marker records are created with `FileMode.CreateNew` under the compile workspace lease. If `CreateNew` reports an existing path, read it while holding the lease: equal family, identity, and raw bytes replay the record; any difference returns `WB-AUTHORITY-SAFEID-409`. Mutable operation/reservation records use `CreateNew` only for their initial record and use atomic replacement thereafter, still under the lease and after exact identity, owner, fence, and legal-state validation.
8. The lease is the existing `authoring-v1/.compile-workspace-lease`, held from compile preflight through all five record writes, target replacement, result/marker settlement, journal append, and cleanup. Recovery takes the same lease or skips without mutation.
9. Cross-record replay is self-contained: operation `operation_id` equals the operation's explicit `proof_id` relationship to proof; operation `proof_id == proof.compile_proof_id`; operation `proof_hash == proof.proof_hash`; result `operation_id`, `request_digest`, and `manifest_hash` equal operation; marker `operation_id`, `request_digest`, and `result_hash` equal operation/result; reservation `operation_id`, `request_digest`, and `fence_token` equal operation. Any missing, extra, mismatched, or tampered binding is a stable conflict; no partial record constructs a replay result.
10. No automatic migration, tombstone, redirect, or audit index is part of SafeId-A. Legacy replay is read-only and never recompiles, rewrites, or migrates a record.

## Acceptance

- Canonicalizer and `oldSafeIdV0` golden vectors reproduce exact bytes and keys across process restarts.
- Unicode, separators, control characters, trailing dots/spaces, reserved names, and old replacement-collision IDs have deterministic strict-key or validation outcomes without overwrites.
- Valid legacy records resolve by one exact path; strict corruption never falls back; strict/legacy equal raw hashes replay, mismatches fail closed with zero mutation.
- Independent processes racing immutable creation produce one exact winner; same-identity different bytes and different-identity collisions both return stable conflict.
- Mutable operation/reservation updates preserve owner/fence/state rules and cannot overwrite a different identity.
- Each of the five cross-record binding tamper cases prevents replay without recompilation or partial output.
- Existing compile settlement tests, Web/CLI public projections, and route behavior remain unchanged.

## Evidence and non-goals

- Evidence: canonicalizer/legacy golden vectors, strict/legacy fixtures, immutable CreateNew race, mutable state replacement tests, independent-process competition, cross-record tamper matrix, replay/no-recompile counters, JSON parsing, and solution Release build.
- Non-goals: document/selection/approval/publish/export/pointer/head/journal SafeId changes, audit indexing, automatic migration, public API changes, Draft/Batch unification, AI segmentation UX, UI Workstation, Persona Workbench, game synchronization, and real Provider access.
