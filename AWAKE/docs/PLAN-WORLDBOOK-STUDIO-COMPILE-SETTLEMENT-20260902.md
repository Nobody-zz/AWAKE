# Worldbook Studio compile settlement plan

- Batch: `WORLDBOOK-STUDIO-COMPILE-SETTLEMENT-20260902`
- Date: `2026-09-02`
- Risk: `high-risk`
- Parent: `PLAN-WORLDBOOK-STUDIO-REPAIR-ROADMAP-20260902.md`
- Prerequisite: boundary hardening checkpoint completed.
- Scope: make customer compile deterministic across duplicate requests, response loss, restart, partial output, and cross-process contention.

## Entry and current behavior

The customer entry is `/api/authoring/compile`, which calls `AuthorityGateService.CompileApproved`. The current path validates a proof, compiles, writes output, and returns; it does not persist a dedicated compile result for replay.

## Required design decisions before implementation

- Define one server-owned compile operation identity derived from proof identity and a canonical request digest.
- Define the exact canonical digest input, UTF-8 encoding, field ordering, normalization, workspace binding, and output-root identity.
- Define atomic cross-process reservation, owner/fence semantics, reservation crash recovery, and winner/loser behavior.
- Define the persisted operation/result schema and all legal state transitions.
- Define marker/result integrity with a trusted verification root, not a self-hash that can be rewritten together with tampered content.
- Define unique recovery outcomes for prepared records, `.tmp`, `.previous`, backup directories, missing targets, manifest mismatch, and orphan compiled output.
- Define replay response contents without re-running compilation and deterministic conflict behavior for changed output/token/digest.
- Define exact compatibility behavior for existing operation/proof files before changing any path mapping.

## Acceptance path

`/api/authoring/compile` → server-owned operation reservation → proof/digest validation → prepared persistence → compile/write → verified committed result → replayable public result or deterministic conflict.

## Required evidence

- First-call compile creates one operation and one committed result.
- Same request replay returns the persisted result without a second compile or journal entry.
- Changed request conflicts without filesystem changes.
- Two processes produce one winner and one deterministic loser.
- Restart recovers every defined crash window to one unique state.
- Tampered/missing marker, result, manifest, and orphan output each produce the specified unique outcome.
- Web and CLI replay the same result and expose only the approved public projection.
- Existing AuthorityGate, Web, CLI, package, and customer closure suites remain green.

## Explicit non-goals

- No SafeId migration or legacy lookup changes in this batch.
- No Draft/Batch unification or segmentation UX.
- No route registry changes.
- No game directory sync, version bump, or real Provider access.

## Gate

This plan requires a fresh independent review and explicit user sign-off before code changes. The previous settlement review state reached its round limit and is historical evidence only. `PLAN-WORLDBOOK-STUDIO-COMPILE-SETTLEMENT-20260902-REVIEW-REVISION-1.md` is the authoritative implementation contract for this batch and supersedes any unresolved checklist wording in this file.
