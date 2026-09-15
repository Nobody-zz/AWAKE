# Review Log: Marcus KV 基础语义契约（F0）

## Round 1 — 2026-09-12

Scope: framework KV public contract, SQLite/runtime path, explicit AWAKE file injection, callers and contract verifier requirements. Read-only; no build, sync or game launch.

Result: `VERDICT: REVISE`

| Finding | Severity | Revision made |
| --- | --- | --- |
| SQLite was incorrectly described as FrameworkHost default | P1 | Separated unavailable FrameworkHost default, Runtime Service SQLite and explicit AWAKE file injection. |
| File store lacked lease/cancellation checks | P1 | F0 requires captured owner/campaign/timeline/session/generation lease and cancellation behavior. |
| File Save failure could expose unpersisted in-memory state | P1 | Requires copy-on-write/atomic replacement and failure/reopen fixtures. |
| Legacy WorldStateStore collapses null/empty/whitespace | P1 | Scoped guarantee to adapter boundary; S0 must use typed raw-read rather than claim all callers preserve it. |
| S0 used old contract identity and retained framework write ownership | P1 | S0 now depends only on F0's `marcus.kv.semantics.v1` record and is blocked until F0 E2. |
| Verifier could be superficial | P1 | Requires fail-closed rejection of pending/not_wired and real path/entrypoint checks. |

Revision evidence: plan SHA-256 `926C3615728D2B07F34AFED97912ABC930D33BED1CC82E62FD6B095A72310DA6`.

Next action: independent read-only round 2. No implementation is authorized before `APPROVED` and user sign-off.

## Round 2 — 2026-09-12

Scope: revised F0 plan, contract record, default/runtime/injected storage paths, and S0 handoff. Read-only; no build, sync or game launch.

Result: `VERDICT: REVISE`

| Finding | Severity | Revision made |
| --- | --- | --- |
| Contract record merged default unavailable and runtime SQLite paths | P1 | Record now has separate default, runtime-SQLite and injected-file path objects, each with source/test/entrypoint/verdict fields. |
| File lease was neither persisted nor isolated in cache | P1 | Requires persisted lease envelope and `(path, leaseFingerprint)` cache key; cross-lease reopen denies access. |
| Cancellation and unknown-write claims were ambiguous | P1 | Locks cancellation precedence and `storage.commit_unknown` reopen-and-compare confirmation. |
| Adapter guarantee was incorrectly attributed to all WorldStateStore callers | P1 | S0 must use typed raw-read; legacy callers remain explicitly out of that guarantee. |
| F0-to-S0 dependency and verifier inputs were non-machine-readable | P1 | Adds consumer handoff, status requirement, exact paths/entrypoints and fail-closed reject statuses. |

Revision evidence: plan SHA-256 `661661809E08830828471B6615EAAB382D0BD7A22404BC898C9C211798E43A68`.

Next action: independent read-only round 3, the high-risk final review round. No implementation is authorized before `APPROVED` and user sign-off.

## Round 3 — 2026-09-12

Scope: final F0 review of cancellation, file envelope, runtime composition, S0 boundary and verifier protocol. Read-only; no build, sync or game launch.

Result: `VERDICT: REVISE`

| Finding | Severity | Revision made |
| --- | --- | --- |
| SQLite/file cancellation, deadline and `None` behavior differed | P1 | Locks combined priority and one external error-code matrix. |
| File envelope/legacy behavior was not verifiable | P1 | Locks schema, lease fields/fingerprint/cache key and `storage.legacy_unbound` classification fixture. |
| F0 claimed an S0 caller handoff that does not yet exist | P1 | F0 now proves adapter/runtime semantics only; S0 owns its future typed raw-read implementation and handoff proof. |
| Runtime SQLite lacked production composition evidence | P1 | Adds a Runtime Service business-adapter composition fixture. |
| Future verifier was not itself a frozen contract | P1 | Locks input/output schema, statuses, errors, exit codes, path/entrypoint checks and unknown-field rejection. |

Revision evidence: plan SHA-256 `3811F001A4B8E696194C3D2772F81F434259D5D92BA82934C249178D8E325897`.

Review budget: the three high-risk rounds are exhausted. F0 is not approved and implementation remains blocked. The user must authorize an exceptional extra review round, or direct a split between adapter semantics and verifier/runtime-composition work.
