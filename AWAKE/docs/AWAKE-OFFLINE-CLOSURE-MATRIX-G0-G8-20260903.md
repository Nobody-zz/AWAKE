# AWAKE Offline Closure Matrix G0–G8 — 2026-09-03

## Status vocabulary

- `planned`: contract or plan exists.
- `verified`: current command produced the required result.
- `partial`: some evidence exists but the requested scope is broader.
- `unverified`: no sufficient current evidence.
- `blocked`: a required dependency or approval is unmet.
- `deferred`: intentionally outside the current batch.

`not_attempted` and `not_available` never count as `verified`.

## Matrix

| Gate | Objective | Current status | Required evidence |
|---|---|---|---|
| G0 | scope, authority, evidence and write boundaries | partial | approved independent plan and current state; combined G3-A plan superseded after review budget |
| G1 | legacy Worldbook inventory and report-only analysis | partial | Downloads parent/child snapshot and 30-file semantic batch are structurally reconciled; content remains `needs_review`, source registry/publish gates remain open |
| G2 | Persona Workbench fixture baseline | verified | current-run five-fixture baseline: 1 positive, 1 fallback, 3 expected rejects; persistence remains not_attempted |
| G3 | Persona runtime projection/context contract | deferred | separate reviewed contract and implementation |
| G4 | Worldbook Studio v2 compile/query/permission | partial/blocked | semantic batch validation `passed` with 7/7 structural checks; candidate remains `needs_review`, human semantic review and source registry gate remain open |
| G5 | NPC dialogue + Persona prompt consumption | unverified/deferred | current BuildId entry→call→structured output |
| G6 | governed memory/relationship/event settlement | partial | current production Smoke `18/18 PASS`; selected fail-closed case does not prove all settlement types |
| G7 | persistence, restart and recovery | unverified | supported persistence runner or new approved handler |
| G8 | full offline closure and candidate evidence | blocked by G3/G7 and Worldbook pilot | current-run E1/E2 evidence baseline exists; full feature closure is not reached |

## Current facts

- AWAKE Runtime `004` offline core Smoke and production Smoke are historical/current source evidence for runtime behavior, not Persona closure.
- Persona G3-S0 proves Storage readiness only.
- Current Persona runner reports persistence as `not_attempted`.
- Current old Worldbook is transitional and will be replaced.
- The latest migration session selected `C:\Users\26811\Downloads\卡拉迪亚编年史\卡拉迪亚编年史` as an audit source, replacing AWAKE `ModuleData\Worldbook` for that audit only; this does not modify or promote either source.
- Latest migration artifact index reports `759` source files, `0` parse errors, `464` conflict candidates, `1320` entity candidates, `93` sensitive-signal files, and `1` adult-signal file. These are report-only findings pending source/hash reconciliation.
- The isolated Downloads artifact set reports `757` files, `1` parse error, `2` duplicate legacy ID groups, `471` conflict candidates, `1320` entity candidates, and `pilot_candidates=30`; its snapshot/index/parse hashes now agree.
- The 30-file pilot is orange/review-only: 22 split candidates, 6 TextMappings reviews, 9 domain reviews, 3 intra-file duplicate cases, 1 placeholder signal, 1 possible truncation signal, and unknown source registry status. It is not compilable or publishable.
- The current P1 repair population is six review subjects: 2 confirmed P1 and 4 P2; the complete source gate for `拉盖娅` remains separate from the 30-file geography pilot.
- New semantic worksheet/rewrite materials remain review-only: `SNAPSHOT-MANIFEST-DOWNLOAD-20260903.json` is a 30-file derived child snapshot, while worksheet/candidate files bind to the 757-file parent snapshot; parent binding is explicitly recorded in `SEMANTIC-PILOT-PARENT-BINDING-20260903.json`, and the declared normalized manifest hash mode was reproduced for 30/30 files.
- `semantic-rewrite-batch-download-20260903/reports/SEMANTIC-REWRITE-VALIDATION-DOWNLOAD-20260903.json` reports `status=passed`; source hash recheck, exact source-unit set, legacy-origin coverage, claim bindings, semantic shape, target-span coverage, and blocked fixtures all passed. This is structural candidate evidence, not human semantic approval or Studio publish evidence.
- Five P1 source files were independently read without modification: `rule_攻城塔` is parseable with distinct variants and its truncation signal is unconfirmed; `rule_吕卡隆` needs assertion split and three TextMappings reviews; `rule_塞堤斯河`, `rule_贝恩兰岛`, and `rule_车尔特格山` contain confirmed duplicate expressions with differing conditions.
- Studio v2 must not be injected into current AWAKE `ModuleData` without a separately reviewed adapter.

## Next order

1. Use only the isolated Downloads artifact set (`*-DOWNLOAD-*`) as current migration evidence; do not combine it with the earlier 5-file AWAKE report set.
2. Reconcile the semantic pilot manifest, worksheets, rewrite candidates, and source snapshot ID/hash before using any semantic candidate.
3. Use the passed semantic-batch validation as structural evidence only; next perform human semantic review of the five rewrite candidates.
4. Resolve the Downloads source registry gate and the full-source `拉盖娅` parse gate before package promotion.
5. Preserve the Persona fixture baseline as E2; do not infer Persona runtime closure from it.
6. Preserve the `18/18` production Smoke as runtime boundary evidence; do not expand its scope.
7. Decide whether a new Persona persistence/Projection contract is warranted; the current runner still reports persistence as `not_attempted`.
8. Do not label G8 closed until G3 and G7 have sufficient evidence.
