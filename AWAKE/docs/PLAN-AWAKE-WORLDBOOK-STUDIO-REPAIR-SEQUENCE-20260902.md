# AWAKE Worldbook Studio Repair Sequence

- Batch: AWAKE-WORLDBOOK-STUDIO-REPAIR-SEQUENCE-20260902
- Approval date: 2026-09-02
- Status: approved_sequential_execution
- Objective: close high-risk defects across Worldbook Studio, UI Workstation, and Persona Workbench by root cause, including structural inefficiency and redundant authoring steps.

## Fixed order

1. WORLDBOOK-STUDIO-BOUNDARY-HARDENING-20260902: HTTP security boundary and safe error projection.
2. WORLDBOOK-STUDIO-COMPILE-SETTLEMENT-20260902: compile settlement, idempotency, crash recovery, and cross-process contention.
3. WORLDBOOK-STUDIO-SAFEID-A-20260902: SafeId and historical-record compatibility; signed off, later changes require a new post-signoff revision.
4. WORLDBOOK-STUDIO-PUBLIC-CONTRACT-20260902: align Web, CLI, route registry, and public projections.
5. WORLDBOOK-STUDIO-AI-AUTHORING-20260902: unify Draft and Batch into one authoritative AI author workflow.
6. WORLDBOOK-STUDIO-AI-REVIEW-UX-20260902: fix unexplained splitting, lack of merge, excessive review, stale evidence, and repeated manual work.
7. AWAKE-THREE-WORKSTATIONS-20260902: close the entry, call, settlement, and observable-result loop across all three workstations.
8. AWAKE-CUSTOMER-DELIVERY-20260902: package, hashes, documentation, offline trial, and customer acceptance; in-game validation remains user-run.

## Hard gates

- High-risk work gets one unique review state; independent review and user signoff precede code changes.
- Completion means entry -> call -> settlement -> observable result; an existing but unwired mechanism is a P0 defect.
- Each batch provides focused tests, broader regression, and an explicit evidence boundary. Do not start Bannerlord, use a real Provider, or read a real API key unless separately approved.
- Do not present future AI-workflow routes as implemented. Keep design registry, current supported surface, and later batches separate.
- Do not reopen signed-off batch state. Cross-boundary changes use a new post-signoff revision and review state.

## AI authoring acceptance focus

- A user sees a complete candidate knowledge unit instead of unexplained fragments.
- AI output is a candidate set that supports merge, split, reorder, discard, and return-to-source, with reasons and evidence for each decision.
- Facts, metadata, expressions, and publication state are separate layers; users do not confirm the same information repeatedly.
- Review supports filtering and collapsing by risk, conflict, missing evidence, and duplication; low-risk items can be batched while high-risk items remain itemized.
- Regeneration, retry, recovery, or target changes explicitly invalidate stale evidence and never silently reuse it.

## Final delivery standard

- Release build has zero warnings and zero errors.
- JSON, XML, and contracts parse; public responses omit owner, fence, token, absolute path, and internal storage identity.
- Root, dist, game, and test-package DLL, event, manifest, version, and hash state is either consistent or explicitly marked source-only.
- All four Worldbook tiers pass keyword coverage, duplication, truncation, structure, and style-difference checks.
- Handoff separates build, sync hashes, file checks, in-game validation, unverified items, and residual risks.