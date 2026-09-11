# AWAKE Offline Evidence Baseline — 2026-09-03

## Current position

- AWAKE Runtime source candidate: `awake-20260903-awake-runtime-repair-004`
- Engineering version: `v0.2.0`
- Evidence level: current offline E1/E2 only
- No Bannerlord launch, game-directory synchronization, real Provider/API key, or E4/E5 evidence

## Persona fixture evidence

- Runner: `tools/persona-awake-joint/run-fixtures.ps1`
- Current reports:
  - `PWB-AWAKE-001-valid-approved`: `pass/0`
  - `PWB-AWAKE-008-last-known-good`: `pass/0`
  - `PWB-AWAKE-003-missing-selection-rejected`: expected `reject/10`, `persona.schema_required_field`
  - `PWB-AWAKE-004-unknown-tag-rejected`: expected `reject/10`, `persona.unknown_tag`
  - `PWB-AWAKE-015-stale-selection-rejected`: expected `reject/10`, `persona.stale_revision`
- Persona persistence/recovery remains `not_attempted`; these reports do not prove Runtime Projection, ContextSnapshot, RuntimeBundle prompt payload, or in-game Persona behavior.
- Reports are under `tools/persona-awake-joint/artifacts/` and include input/output hashes.

## Legacy Worldbook evidence

- Latest external migration session selected source:
  `C:\Users\26811\Downloads\卡拉迪亚编年史\卡拉迪亚编年史`
- Selection is recorded in `docs/worldbook-migration/AUTHORITY-SOURCE-DECISION-20260903.json`.
- Artifact index:
  `docs/worldbook-migration/ARTIFACT-INDEX-20260903.json`
- Reported scope in the isolated Downloads batch: `757` files, `337` knowledge rules, `415` personality/background files, `3` event-data files, `1` parse error, `471` conflict candidates, `1320` entity candidates, `93` sensitive-signal files, and `1` adult-signal file.
- Source was reported unchanged and no authoring v1, canon, or published status was generated.
- Reconciliation warning: the artifact index reports `parse_errors=0`, while the source decision's embedded observation history records an earlier `parse_error_count=1` and `duplicate_knowledge_id_group_count=2`. The current snapshot/parse report must be treated as the load-bearing pair and rechecked before promotion.
- Additional reconciliation finding: `SOURCE-SNAPSHOT-20260903.json` and `SOURCE-INVENTORY-20260903.json` still identify the earlier 5-file AWAKE pilot, while `ARTIFACT-INDEX-20260903.json` and `SOURCE-PARSE-REPORT-20260903.json` identify the user-selected Downloads source. The mixed artifact set is blocked and must not be used as one batch.
- Isolated `*-DOWNLOAD-*` artifacts now agree on `source.awake.worldbook.download-20260903` and snapshot SHA-256 `629a24fc633c035aa83d369b54d35cd3a893eb429d5fcf501e2e2ff0c717f129`; the pilot remains review-only and blocked for compilation by unknown source registry and unresolved content decisions.
- P1 source diagnosis report: `docs/worldbook-migration/PILOT-P1-SOURCE-REPAIR-REVIEW-20260903.json`. It confirms `rule_攻城塔` is parseable with distinct variants, `rule_吕卡隆` requires split/three mapping reviews, and duplicate expressions in `rule_塞堤斯河`, `rule_贝恩兰岛`, and `rule_车尔特格山`. No source file was modified.
- Follow-up report-only decisions are present and JSON-parseable:
  - `docs/worldbook-migration/PILOT-DUPLICATE-DECISIONS-20260903.json`
  - `docs/worldbook-migration/PILOT-TEXTMAPPING-REVIEW-20260903.json`
  - `docs/worldbook-migration/PILOT-DOMAIN-RECLASSIFICATION-20260903.json`
  - `docs/worldbook-migration/PILOT-REPAIR-NEEDS-REVIEW-20260903.json`
- Current source gate: the Downloads package still has one confirmed JSON parse error in `personality_background/lord_1_14__拉盖娅.json`; it is outside the 30-file geography pilot but blocks full-package promotion until resolved in a separately authorized source repair.
- Final report-only repair classification: `2` confirmed P1 + `4` P2 = `6` review subjects. All six decision records now carry the source snapshot ID/hash, source-file SHA-256, JSON locator, evidence kind, and reason.
- The Worldbook pilot repair plan review state reached its three-round high-risk budget with terminal `REVISE`; no source-edit authorization was inferred. Source edits, canon decisions, registry changes, and Studio authoring remain pending human decision and a new bounded implementation/review batch.
- Semantic worksheet/rewrite artifacts are derived-child review materials: `SNAPSHOT-MANIFEST-DOWNLOAD-20260903.json` identifies the 30-file child `semantic-pilot30.download-20260903`, while worksheets/candidate bind to the 757-file parent `source.awake.worldbook.download-20260903`. Parent binding is explicitly cross-referenced and the declared `utf8-lf-no-bom-v1` content hashes reproduce 30/30; no semantic candidate is promoted as standalone or publishable.
- `semantic-rewrite-batch-download-20260903/reports/SEMANTIC-REWRITE-VALIDATION-DOWNLOAD-20260903.json` passed 7/7 structural checks with zero warnings. The report's next action is human semantic review; it does not prove canon, permission approval, Studio compile, export, or publication.
- This is report-only organization evidence. No final Studio authoring document, compiled v2 package, Runtime adapter, or authoritative registry was produced.

## Runtime boundary evidence

- Worldbook Runtime Production Smoke: `18/18 PASS`.
- Selected case: `failed-drain-fail-closed`.
- It proves fail-closed drain/readiness/replacement behavior only; it does not prove Persona recovery, generic save/load, full session isolation, or Persona-to-NPC Prompt projection.
- The captured stdout was saved under `docs/offline-closure/G3C-SETTLEMENT-SMOKE-20260903.txt` with executable and stdout hashes.

## Next action

Reconcile the current migration snapshot and parse report, then create a separate approved batch for either:

1. Persona persistence/Runtime Projection contract work; or
2. Worldbook migration pilot refinement against the user-selected source.

Do not modify old Worldbook files, AWAKE `ModuleData`, Studio source, Persona Workbench source, UI Workstation, `dist`, PlayerExports, or the game directory.
