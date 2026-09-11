# Worldbook Studio AI Review UX — Batch 6

- Batch: `WORLDBOOK-STUDIO-AI-REVIEW-UX-20260904`
- Parent: `AWAKE-WORLDBOOK-STUDIO-REPAIR-SEQUENCE-20260902`
- Risk: `high-risk`
- Status: `planned_for_review`

## Goal

Make AI segmentation explainable and reviewable from a customer perspective without changing worldbook authority, automatic publication rules, or the Batch 5 lifecycle.

## Scope

1. Extend candidate data with segmentation reason codes and source-span references.
2. Add a review projection that groups candidates by risk, missing evidence, conflict, and duplicate signals.
3. Add explicit keep-whole, merge, split, reorder, discard, and return-to-source decisions as review-only operations.
4. Deduplicate repeated evidence while retaining every candidate binding.
5. Persist review decisions and invalidate them when source, packet, provider, or candidate fingerprints change.
6. Keep low-risk batch actions separate from high-risk itemized review.

## Non-goals

- No automatic canon publication or runtime synchronization.
- No redesign of the three-workstation shell; Batch 7 owns that.
- No provider-specific prompt workaround in the authoritative Studio parser.
- No silent merge/split decision by AI; the editor must expose and confirm it.

## Acceptance

- A candidate displays why it was segmented and where it came from.
- A user can keep whole, merge, split, reorder, discard, and return to source.
- Every operation produces a new review projection and preserves the original candidate set.
- Duplicate evidence is visually collapsed but remains traceable to all bound candidates.
- Risk filters reduce review work without allowing red or unresolved candidates to bypass review.
- Any source/provider/candidate change marks prior review decisions stale.
- Focused UI/domain tests, HTTP contract tests, and full Worldbook Studio regression pass.

## Execution slices

1. Contract and projection model → validate JSON shape and compatibility.
2. Candidate reason/span normalization → test zero/one/many and missing evidence.
3. Review decision service → test merge/split/keep/discard/reorder and stale invalidation.
4. Draft and Batch UI → test visible reasons, risk filters, evidence grouping, and recovery.
5. Full regression and package smoke → record evidence before sign-off.
