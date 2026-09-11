# Worldbook Studio repair roadmap

- Owner: AWAKE / Worldbook Studio
- Started: `2026-09-02`
- Objective: make Worldbook Studio safe to deliver, recoverable after failure, compatible with existing records, and efficient for AI-assisted worldbook authoring.
- Rule: complete each batch as `入口 → 调用 → 结算/持久化 → 可观察结果`, then record focused and broader evidence before opening the next batch.

## Ordered batches

1. **HTTP boundary hardening** — approved and in implementation. Hide internal exception details, classify side effects, add correlation ID, unify authority status mapping, contain unknown exceptions, and cover retired routes.
2. **Compile settlement and crash recovery** — define one idempotent compile operation, atomic reservation, persisted result replay, marker integrity, `.tmp`/`.previous` recovery, orphan-output quarantine, and cross-process winner/loser behavior.
3. **SafeId and legacy compatibility** — preserve exact lookup for existing records, define new-ID validation, collision handling, read-only migration/indexing, and old-record regression fixtures.
4. **Public wire projection and route reconciliation** — remove internal paths and owner data from operation/success responses, align Web, CLI, and route registry, and define stable status/exit-code contracts.
5. **Draft/Batch authoring unification** — converge source, source unit, candidate claim, evidence, review decision, and draft creation into one domain path while retaining paste/file ingestion adapters.
6. **Explainable AI segmentation and review UX** — add source spans, reason codes, merge/split/keep-whole controls, risk-tiered review, resumable scans, and deduplicated evidence decisions.
7. **UI Workstation, Worldbook Studio, Persona Workbench integration pass** — verify shared contracts, loading/error/retry behavior, empty states, long-list performance, and customer workflows without changing domain authority.
8. **Release and customer acceptance** — build, package, hash, static harness, real HTTP smoke, and user-run application verification; do not claim runtime evidence that was not observed.

## Current boundary

Only batch 1 is authorized for code changes. Batches 2–8 remain planned and must not be mixed into the boundary patch. Each later high-risk batch gets its own plan, review state, acceptance matrix, and user sign-off before implementation.

## Completion target

The roadmap is complete only when every batch has a passing focused test set, dependent regression tests, documented limitations, and an observable user-facing path. Compilation alone is not completion.
