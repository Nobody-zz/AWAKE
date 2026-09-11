# AWAKE Legacy Worldbook Report Pilot — 2026-09-03

## Purpose

对旧版 `ModuleData\Worldbook` 做只读、report-only 的小批整理，为后续按 Worldbook Studio authoring v1 改写提供证据；不生成可编译候选，不修改旧文件。

## Scope

- 固定一个主题域。
- 初始只选择 1–5 个旧 JSON 文件。
- 记录文件相对路径、SHA-256、解析状态、旧 ID、主题候选、实体候选和敏感内容层级。
- 生成重复、冲突、实体、损失和 needs-review 报告。
- 旧 `Id/Keywords/RagShortTexts/Variants/When/TextMappings` 只作为迁移线索。

## Explicit non-goals

- 不把旧格式认定为最终格式。
- 不生成 Studio authoring 文档、compiled package 或 Runtime v2 payload。
- 不创建权威 `event_id`、redirect、ID registration 或 migration event。
- 不修改 Worldbook Studio、Persona Workbench、UI Workstation、AWAKE `ModuleData`、`src`、`dist` 或游戏目录。
- 不做最终 canon 决策。
- 不处理全量世界书，不扩大到 20–50 文件，除非另立并批准扩展批次。

## Inputs

- Legacy root: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\ModuleData\Worldbook`
- Selected files must be listed explicitly in the batch report.
- Input snapshot and file hashes are authoritative for this pilot.

## Required report format

Each report must include:

- `batch_id`
- `source_snapshot_id`
- `input_files`
- `input_hashes`
- `source_units`
- `duplicate_findings`
- `conflict_findings`
- `entity_candidates`
- `migration_loss`
- `needs_review`
- `status`
- `output_hash`

`status` is report status only:

- `scanned`
- `analysis_complete`
- `needs_review`
- `failed`

It must not use `canon`, `compiled`, `candidate`, or `published`.

## Acceptance

- All selected files are hash-bound.
- All selected JSON files parse or have explicit parse errors.
- No source file is modified.
- Every proposed merge/split has source paths and reasons.
- Every uncertain fact enters `needs_review`.
- Adult/optional material is reported separately from base material.
- No authoritative registry or migration event is written.

## Deliverables

All new outputs stay under:

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\worldbook-migration\`

- `SOURCE-SNAPSHOT-20260903.json`
- `SOURCE-INVENTORY-20260903.json`
- `DUPLICATE-REPORT-20260903.json`
- `CONFLICT-REPORT-20260903.json`
- `ENTITY-CANDIDATE-REPORT-20260903.json`
- `MIGRATION-LOSS-REPORT-20260903.json`
- `NEEDS-REVIEW-20260903.json`

## Evidence

- E0: plan and explicit input list.
- E1: JSON parsing and hash checks.
- E2: report-only analysis with reproducible input/output hashes.
- No E3/E4/E5 claim.

