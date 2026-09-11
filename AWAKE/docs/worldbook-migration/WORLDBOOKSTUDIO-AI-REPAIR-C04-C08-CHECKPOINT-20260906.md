# Worldbook Studio AI 修复批次 C04–C08 Checkpoint

> 日期：2026-09-06
> 状态：已实现并通过离线回归；总 Goal 仍在继续
> 范围：Worldbook Studio AI 生成链路、离线测试与契约 schema

## 本批完成

- `warnings`、`unresolved`、`coverage` 已进入结果 Parser、Store 持久化和跨阶段合并。
- `blocking=true` 的未解决项会阻断 `create-document`。
- `unknown`、缺失或非法 `content tier` 不再静默降级为 `base`；UI 要求作者明确选择。
- 新增一等 `target_spans`，并贯通 Normalizer、Parser、CandidateSet、review projection、merge/split、公开投影和 provenance。
- 新增 `propositions`、`claims`，校验 proposition/claim/target span 的 ID 与 source origin 绑定。
- Quick Authoring complete 结果缺少语义图时进入 blocking unresolved。
- coverage 声明的 source proposition 数不足以覆盖正文时进入 blocking unresolved。
- 建档 provenance 保留 `propositions`、`claims`、`target_spans`、`certainty`、`inferred` 和 `perspective`。
- Quick Authoring UI 增加模式、目标、简单提示词、受众、文风、必须保留、禁止新增和 content tier 输入。
- API generate 响应返回诊断与语义图字段。
- Draft smoke 修复 web/worker 端口竞态，并增加 Quick Authoring HTTP 闭环。

## 离线证据

- Release solution build：0 warnings / 0 errors。
- DraftTests：`47/47 PASS`。
- BatchTests：`21/21 PASS`。
- Worldbook Studio harness：`113/113 PASS`。
- Frontend editor/session/safety/draft/batch/customer harness：全部通过。
- Draft HTTP smoke：通过 staged、幂等、伪造 evidence、候选切换和 Quick Authoring semantic closure。
- Authoring save、public contract、workstation handoff smoke：通过。
- 所有 smoke 仅使用 loopback 与 fake/local test harness。

## 主要改动文件

- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringDraftContracts.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringLifecycleContracts.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringReviewProjection.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringReviewDecisions.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringDraftDocumentBuilder.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/AuthoringDraftStore.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/AuthoringDraftEndpoints.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/studio-draft.js`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/studio-draft.css`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.Draft.Tests/Program.cs`
- `tools/worldbook-studio/tests/frontend/editor-content.test.js`
- `tools/worldbook-studio/scripts/draft-workflow-smoke.ps1`
- `docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json`
- `docs/worldbook-studio-plan/awake.worldbook.authoring-review-projection.v1.schema.json`
- 对应 A3.3/A4 contract golden hash

## 尚未完成

- 语义 red-team 仍需覆盖 rumor/history/perspective/polarity 的来源冲突检测。
- `must_not_invent` 还需要更细的确定性审查与回归 fixture。
- candidate-specific `unresolved/coverage` 还未单独投影。
- Draft state schema/migration 与多任务服务端续接仍待下一批。
- Batch/cache semantic coverage 仍未闭合。
- Semantic Migration 路径继续保持显式拒绝，不在本批实现。

## 硬边界声明

- 未启动 Bannerlord。
- 未同步游戏目录。
- 未访问真实 Cloud Provider、API Key、Token、Worker 或网络服务。
- 未修改 AWAKE 模组本体、ModuleData、dist 或世界书迁移候选。
- 未将任何结果标记为 `approved`、`canon`、`compiled`、`published` 或 `runtime-ready`。
