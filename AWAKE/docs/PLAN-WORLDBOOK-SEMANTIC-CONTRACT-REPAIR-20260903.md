# Worldbook Semantic Contract Repair and Rewrite Batch

## Authorization

- Authorized by user on 2026-09-03.
- This is a new bounded batch; it does not reopen the exhausted review state for `PLAN-WORLDBOOK-SEMANTIC-REWRITE-20260903.md`.
- The previous direct-mapping candidate remains `superseded` and is not reused as content.

## Goal

补齐语义迁移的最小机器契约，生成 30 个 source unit 的语义工作表，并对 5 个代表文件完成真实的理解、提炼和重新撰写；所有输出保持 `needs_review`，不自动 canon、发布、同步或启动游戏。

## Scope

### Read-only authority

`C:\Users\26811\Downloads\卡拉迪亚编年史\卡拉迪亚编年史`

### Five rewrite files

- `knowledge/rules/rule_拉科尼斯湖__拉科尼斯湖.json`
- `knowledge/rules/rule_沙拉斯湾__沙拉斯湾.json`
- `knowledge/rules/rule_黎明山脉__黎明山脉.json`
- `knowledge/rules/rule_卡恰尔半岛__卡恰尔半岛.json`
- `knowledge/rules/rule_德里亚特__德里亚特.json`

### New machine contracts

- `docs/worldbook-migration/semantic-worksheet.v1.schema.json`
- `docs/worldbook-migration/semantic-provisional-id.v1.schema.json`
- `docs/worldbook-migration/snapshot-manifest.v1.schema.json`
- `docs/worldbook-migration/semantic-rewrite-review.v1.schema.json`

### New validators and artifacts

- `tools/worldbook-migration/validate_semantic_batch_20260903.py`
- `docs/worldbook-migration/SNAPSHOT-MANIFEST-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/SEMANTIC-WORKSHEETS-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/KNOWLEDGE-CLUSTER-PLAN-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/REWRITE-CANDIDATE-5-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/BLOCKED-ONLY-FIXTURES-DOWNLOAD-20260903.json`
- `docs/worldbook-migration/SEMANTIC-REWRITE-VALIDATION-DOWNLOAD-20260903.json`

## Hard rules

- Source files are evidence, not target prose.
- `Variants`, `RagShortTexts`, `Keywords`, `When`, `TextMappings` and `SemanticPrototypes` must be decomposed and assigned a disposition.
- No target prose may be copied from a legacy variant; necessary proper nouns are explicit overlap exceptions only.
- A source claim is not a canon fact until separately reviewed.
- `When` is perspective/audience evidence by default, never an automatic grant or deny.
- Unknown source license, use status, content tier, universe, era, identity or permission mapping remains blocked/unresolved.
- Provisional IDs never enter formal runtime, save, registry, ledger or redirect namespaces.
- All generated documents, claims, expressions and review records remain `needs_review`.
- No source, Studio source, AWAKE source, dist, game directory or published package may be modified.
- No real Provider, API key, token or Bannerlord process may be used.

## Acceptance

- 30 source units are represented exactly once in the worksheet set.
- Every legacy source atom has exactly one disposition: `preserve`, `merge`, `split`, `rephrase`, `drop`, or `unresolved`; `drop` and `unresolved` require rationale/loss.
- Every claim has a source binding with source ID/version/hash, relative locator, normalized quote and matching quote hash.
- Five rewritten documents contain claim coverage and semantic review records.
- Target prose is not a legacy variant surface rewrite; sentence/segment/n-gram overlaps are either absent or explicitly justified by proper noun/necessary terminology exceptions.
- Subject, predicate, object, polarity, epistemic kind, perspective and time scope are not changed without recorded rationale.
- The five documents remain `needs_review`; no grants or denies are emitted without registry-backed mapping.
- Schema/validator checks pass for the new migration artifacts.
- Source hashes are unchanged before and after the batch.
