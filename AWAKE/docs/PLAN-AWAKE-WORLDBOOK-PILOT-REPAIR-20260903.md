# AWAKE Worldbook Pilot Repair — 2026-09-03

## Current state

- Authority source: `C:\Users\26811\Downloads\卡拉迪亚编年史\卡拉迪亚编年史`
- Source snapshot: `source.awake.worldbook.download-20260903`
- Source snapshot SHA-256: `629a24fc633c035aa83d369b54d35cd3a893eb429d5fcf501e2e2ff0c717f129`
- Pilot: `pilot.awake.worldbook.download-20260903.geo-rules-30`
- Pilot status: `review_only`
- Pilot rating: orange; not compilable or publishable

## Objective

Resolve the pilot's source and classification blockers without changing the authority source, creating canon, mutating Studio registries, or publishing content.

## Fixed pilot findings

- 30 files, 248 non-empty legacy variants, 223 conditioned variants.
- 22 files require split or manual reorganization.
- 6 files contain TextMappings requiring explicit entity review.
- 9 files have a non-geography primary-domain suggestion.
- 3 files contain intra-file duplicate variants.
- 1 file has a placeholder signal.
- 1 file has a possible truncation signal.
- 2 duplicate legacy knowledge-ID groups exist in the full source audit.
- Source registry status is unknown and blocks Studio package compilation.
- All pilot authoring candidates remain `needs_review`; no grants or denies were emitted automatically.

## Priority order

### P1 source/content blockers

1. `personality_background/lord_1_14__拉盖娅.json`
   - Full-source gate: resolve the confirmed invalid CRLF inside the JSON string before treating the complete source package as parseable. This file is outside the current 30-file geography pilot.
2. `knowledge/rules/rule_吕卡隆__吕卡隆.json`
   - Resolve three TextMappings and split mixed settlement/history/political/economic assertions before any rewrite.
3. `knowledge/rules/rule_塞堤斯河__塞堤斯河.json`
   - Decide whether the duplicate expression is semantically shared or must be rewritten; do not delete automatically.
4. `knowledge/rules/rule_贝恩兰岛__贝恩兰岛.json`
   - Decide whether the duplicate expression is semantically shared or must be rewritten; do not delete automatically.
5. `knowledge/rules/rule_车尔特格山__车尔特格山.json`
   - Decide whether the scouting-gated duplicate expression changes access detail; do not delete automatically.

The complete review population is six source/content review subjects: two confirmed P1 items and four P2 items.

### P2 heuristic findings

- `knowledge/rules/rule_攻城塔__攻城塔.json`: parseable with three distinct normalized texts; truncation/placeholder is not confirmed.
- `rule_吕卡隆` placeholder-letter signal: no reproducible token/locator yet; keep as a review hint only.
- Full-source heuristic counts (placeholder, truncation, repeated text) require file-level confirmation before severity promotion.

The pilot has six source/content review subjects: two confirmed P1 items (`拉盖娅` full-source parse gate and `吕卡隆` TextMappings/assertion split) and four P2 semantic/heuristic items (duplicate expressions in `塞堤斯河`, `贝恩兰岛`, and `车尔特格山`, plus the separate `攻城塔` integrity review).

### P2 model-shaping work

- Test the split model on the 22 split candidates.
- Build a manual mapping table for the 6 TextMappings files.
- Reclassify the 9 cross-domain files.
- Decide universe and era for the 30-file pilot.
- Resolve the source registry's `content_tier`, `license_status`, and `use_status`.

### P3 later review

- Revisit keyword and RAG responsibilities after assertion structure is stable.
- Review expression layers and grants/denies only after profile rules are authoritative.
- Compare four-tier differences only after each source unit has a stable identity.

## Execution rules

- First pass is read-only diagnosis and proposed decisions.
- Do not edit the Downloads authority source in this batch.
- Do not edit AWAKE `ModuleData\Worldbook`.
- Do not create Studio authoring candidates or compiled packages in the repair pass.
- Do not create authoritative IDs, redirects, migration events, or registry entries.
- Every proposed decision must reference source snapshot ID, source file hash, locator, and reason.
- Unresolved items remain `needs_review`.
- Existing `PILOT-AUTHORING-CANDIDATE-DOWNLOAD-20260903.json` and its candidate directory are superseded outputs from a prior pass; they are not inputs or outputs of this repair pass.
- The confirmed `lord_1_14__拉盖娅.json` parse error is a package-level source gate. It blocks full-package promotion, but does not invalidate a pilot explicitly limited to the 30 geography files that excludes it.

## Acceptance

- Each of the two confirmed P1 items and four P2 items has a source diagnosis and a human decision request.
- Duplicate variants are classified as `same_expression`, `distinct_expression`, or `unresolved`.
- TextMappings have explicit target-entity candidates or are marked unresolved.
- Domain changes include old domain, proposed domain, reason, and confidence.
- Possible truncation/placeholder findings include source locator and exact evidence.
- Full-source parse gate is explicit: `parse_error_count=0` is required before full-package promotion; pilot-only analysis must prove the error file is outside its selected input set.
- No source bytes, AWAKE runtime bytes, Studio files, registry files, or package pointers change.

## Deliverables

All reports remain under:

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\worldbook-migration\`

- `PILOT-P1-SOURCE-REPAIR-REVIEW-20260903.json`
- `PILOT-DUPLICATE-DECISIONS-20260903.json`
- `PILOT-TEXTMAPPING-REVIEW-20260903.json`
- `PILOT-DOMAIN-RECLASSIFICATION-20260903.json`
- `PILOT-REPAIR-NEEDS-REVIEW-20260903.json`
- `PILOT-AUTHORING-CANDIDATE-DOWNLOAD-20260903.json` remains excluded as superseded prior output.

## Next gate

Only after all six source/content review subjects are human-reviewed may one file be transformed into a Studio authoring v1 candidate. The transformed file remains `needs_review` and cannot be compiled or published until source registry and permission gates pass.
