# Worldbook Semantic Rewrite Pilot Review Log

## Review target

- Plan: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\PLAN-WORLDBOOK-SEMANTIC-REWRITE-20260903.md`
- State: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\PLAN-WORLDBOOK-SEMANTIC-REWRITE-20260903.review-state.json`
- Risk class: `high-risk`
- Review budget: 3 rounds

## Round 1 — 2026-09-03

- Reviewer: independent read-only subagent
- Verdict: `REVISE`
- Findings: P1 semantic rewrite proof, epistemic boundaries, source locator contract, ID bridge, content-tier provisional semantics, `When` permission mapping; P2 pilot coverage, worksheet completeness, snapshot reproducibility.
- Changed decision: Authoring v1 projection is deferred until source registry, content tier, universe/era and ID boundaries are confirmed. The first artifact is authoring-neutral claim-level semantic work.
- New evidence: Full-text non-equality alone cannot distinguish true semantic rewriting from deletion, reordering, synonym substitution, RAG copying, concatenation, or bias relabeling.
- Changed acceptance:
  - Require `claim_id`, `epistemic_kind`, subject/predicate/object, perspective, time scope, confidence, conflict group, legacy origins, rewrite rationale, loss and unresolved.
  - Require source registry-backed relative locator, normalized quote and quote hash.
  - Treat old `When` as perspective/audience evidence by default; do not auto-create grants/denies.
  - Separate Authoring provisional IDs from Runtime/entity/profile IDs.
  - Add sentence/segment/n-gram anti-mechanical-rewrite checks.
  - Add blocked-only fixtures.
  - Require exact source-unit set equality across worksheet, cluster plan, rewrite candidate and validation reports.

## Round 2 — 2026-09-03

- Reviewer: independent read-only subagent
- Verdict: `REVISE`
- Findings: the plan direction was corrected, but legacy origin/claim binding, provisional IDs, default-base negative gates, legacy `When` mapping evidence, snapshot reproducibility, and positive semantic preservation still lacked executable detail.
- Changed decision: promote claim-level provenance, provisional namespace, tier/default-base negative gates, legacy `When` boundary, deterministic snapshot algorithm, and positive semantic preservation to implementation prerequisites.
- New evidence: the existing Authoring/source/ledger schemas do not define semantic worksheets, `legacy_origins`, `claim_source_binding`, `mapping_basis`, or `target_span` coverage.

## Round 3 — 2026-09-03

- Reviewer: independent read-only subagent
- Verdict: `REVISE`
- Findings:
  - semantic worksheet, legacy origin and claim source binding still lack a formal schema and validator;
  - `authoring_provisional.*` lacks complete syntax, lifecycle and fail-closed validation, and the current ledger schema would accept the prefix syntactically;
  - snapshot hash still lacks a golden fixture and complete cross-implementation serialization rules;
  - positive semantic preservation still lacks a source-proposition inventory, complete source-to-claim coverage protocol, independent reviewer rule and repeatable pass/fail semantics;
  - `SemanticPrototypes` has no explicit origin kind or disposition;
  - the round-2 log had not yet been synchronized with review-state.
- Changed acceptance: no semantic worksheet generation until the worksheet/legacy-origin/claim-binding/provisional-ID/snapshot-golden/semantic-preservation machine contracts and validators exist; tier execution order must be `CLI parse → tier resolution → graph closure → pre-write block`.

## Current state

`revising`: the maximum three-round high-risk review budget is exhausted without an `APPROVED` verdict. No semantic worksheet or rewritten content may be generated under this review state.

## Blocker

The current review state is not approved and has `user_signoff=false`. Further changes require a separately authorized batch or a new review target; do not reopen this approved-budget state or treat the existing direct-mapping candidate as semantic migration evidence.
