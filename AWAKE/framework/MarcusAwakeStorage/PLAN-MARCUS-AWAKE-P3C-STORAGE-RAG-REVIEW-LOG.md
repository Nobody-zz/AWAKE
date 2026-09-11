# P3C Storage/RAG Read-Only Challenge Log

- Date: 2026-08-27
- Scope: `MarcusAwakeStorage` only
- Review inputs: AWAKE `AGENTS.md`, full migration plan dated 2026-08-26, P3C storage/RAG plan, current `MarcusAwakeFramework.Api` storage/RAG source and build, old SDK v1.3.15 metadata, and the old SDK Storage/RAG DTOs.
- Review mode: controller-side bounded read-only challenge; no shared framework or AWAKE files were edited.

## Findings

### F1 — Contract drift between old SDK and current AWAKE API

- Severity: P1
- Observation: the old SDK exposes only campaign KV plus sidecar/timeline export methods; its RAG DTOs use `Content`, `Provenance`, `UpdatedUtc`, `ExpectedCorpusFingerprint`, `AllowedAccessScopes`, and a 50-result limit. The current AWAKE Core contract adds session KV, retrieval modes, request identity fields, and the `Text`/`CorpusLocator`/`ObservedAt` DTO shape with a 64-result limit.
- Correction: compile the backend against the current read-only AWAKE Core DLL because the locked P3C acceptance matrix explicitly requires session namespaces and `Hybrid`/`Semantic` unsupported results. Treat the old SDK as migration evidence, not as a second runtime contract.
- Status: accepted

### F2 — Timeline ledger is not present in the current public Core API

- Severity: P1
- Observation: the current shared API exposes KV and RAG interfaces but no event-ledger interface.
- Correction: keep the shared API untouched and expose a small backend-owned timeline ledger DTO/API backed by the same database. This is an implementation seam for the future Runtime Service handler, not a claim that AWAKE Core already wires it.
- Status: accepted

### F3 — Permission and Runtime Service wiring are outside this write grant

- Severity: P1
- Observation: permission evaluation, IPC handlers, and AWAKE registration live outside `MarcusAwakeStorage`.
- Correction: enforce owner/session/namespace identity and request identity matching locally; leave permission broker, transport, and Runtime Service integration explicitly unimplemented in this slice.
- Status: accepted

### F4 — Cancellation must not create partial database state

- Severity: P1
- Observation: SQLite calls are synchronous and must not run on a caller or game thread; cancellation can arrive while work is queued or while an ingest transaction is active.
- Correction: serialize all database work behind an async gate, execute it on a thread-pool worker, check cancellation/deadline before and during transactions, and commit only after the final check. Queued cancellation/deadline exits before the worker starts.
- Status: accepted

### F5 — Semantic behavior must not be inferred locally

- Severity: P1
- Observation: no embedding or rerank provider is available in this directory and AWAKE forbids local model inference.
- Correction: implement only FTS5 keyword retrieval; return `rag.retrieval_mode_unsupported` for `Hybrid` and `Semantic`, and document that no fallback pretends to be semantic.
- Status: accepted

### F6 — SQLite/native package availability is an environment gate

- Severity: P1
- Observation: the target directory has no existing project or restored SQLite package assets.
- Correction: pin `Microsoft.Data.Sqlite`, restore into a target-local package cache, then audit the actual publish assets. If restore or native assets fail, report the exact failure and do not call a non-FTS fallback FTS5.
- Status: pending build evidence

## Decision

The smallest implementation that satisfies the locked P3C matrix is approved under the corrections above. No shared contract shim, AWAKE integration, game launch, or external directory write is authorized.

VERDICT: APPROVED
