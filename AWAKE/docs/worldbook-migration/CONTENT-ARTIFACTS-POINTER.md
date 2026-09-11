# 内容产物出仓指针（2026-09-11）

本目录从旧工作区 `_houkai_merge\AWAKE\docs\worldbook-migration\` 整体迁回（265 文件），随后按工作区边界规则（`AGENTS.md`：世界书正文不进本工作区）拆分：**内嵌编年史正文的批次产物移至 `D:\AWAKE-Archive\worldbook-migration-content\`**，本目录仅保留契约、报告索引、方法论与红测文档（133 文件）。

## 已移出产物清单

| 产物 | 文件数 | 说明 |
|---|---|---|
| `semantic-rewrite-batch-download-20260903/` | 89 | 语义重写候选正文 + 审查报告 |
| `pilot-authoring-v1-candidate-download-20260903/` | 38 | 试点 Authoring v1 候选工作区（含 sources/suggestions） |
| `INDEPENDENT-SEMANTIC-CANDIDATE-REVIEW-20260904.json` | — | 内嵌 `normalized_quote` 引文 |
| `SEMANTIC-DECISION-PACK-R2-20260906.json` | — | 内嵌引文与长段中文正文 |
| `SEMANTIC-REVISION-EVIDENCE-PACK-R3-20260908.json` | — | 内嵌 `normalized_quote` 引文 |
| `SEMANTIC-WORKSHEET-R3-REVISION-DRAFT-20260908.json` | — | 内嵌引文与长段中文正文 |
| `SEMANTIC-WORKSHEETS-DOWNLOAD-20260903.json` | — | 内嵌引文与长段中文正文 |

## 出仓文件 SHA-256（顶层 JSON）

```
a62623968c72b60cd157e27e3c4bcae74e49de1af3e172cb4042bace7fffecd2  INDEPENDENT-SEMANTIC-CANDIDATE-REVIEW-20260904.json
dc9a2df559f2757828c9abc99ebac522860e191fd772fd8f8f37402ee86689b3  SEMANTIC-DECISION-PACK-R2-20260906.json
4c0a402c11ef1dad14d8d2c486bf3f6db6590652087f9ae04c57272ae018bb20  SEMANTIC-REVISION-EVIDENCE-PACK-R3-20260908.json
0b4d9618e1d59e6cc5034baa45774752ad1ead204404ae86a2487d36b022b8c3  SEMANTIC-WORKSHEET-R3-REVISION-DRAFT-20260908.json
736f5d786a63b553aa538fbdf3894e8909c8fb77078542f964a59317c2951e56  SEMANTIC-WORKSHEETS-DOWNLOAD-20260903.json
```

## 甄别口径

- 移出标准：`normalized_quote` / `quote` 字段实值、≥80 连续中文字符（含标点）的正文段、重写候选正文文件。
- 保留标准：仅含定位符（`#/Variants/...`）、哈希、路径引用、占位示例文本的文件；schema 契约文件（如 `semantic-worksheet.v1.schema.json`）；纯过程文档。
- 注：`WORLDBOOKSTUDIO-AI-PIPELINE-REDTEAM-*` 中的 `quote` 命中均为占位示例，非真实正文，已保留。

## 关联决策

- 旧工作区 v1 世界书本体（`ModuleData\Worldbook`，约 4.8 MB）的归宿决策与归档记录见 `AWAKE\docs\artifact-retention\CLEANUP-CANDIDATES-20260911.md`。
