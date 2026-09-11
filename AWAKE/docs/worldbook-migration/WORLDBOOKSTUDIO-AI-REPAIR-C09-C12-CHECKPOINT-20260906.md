# Worldbook Studio AI 修复批次 C09–C12 Checkpoint

> 日期：2026-09-06
> 状态：已实现并通过离线回归；总 Goal 仍在继续
> 范围：语义边界、候选级诊断、本地 Draft 状态和 Quick Authoring HTTP 闭环

## 本批完成

- candidate 级 `unresolved`、`coverage` 已进入 Parser、Normalizer、CandidateSet、公开投影、审查投影和 merge/split 派生。
- candidate-local blocking 项会阻断 `create-document`，并在 UI 中按所选候选显示。
- `propositions` 的 `epistemic_kind`、`time_scope`、`polarity` 使用冻结枚举并在 Parser/Normalizer 双重 fail closed。
- proposition、claim、target span 的来源 ID 必须非空；claim 和 target span 的来源不能超出上游来源集合。
- coverage 不足、claim 超出可定位 quote、target span 超出可定位 quote 会产生 blocking unresolved。
- `must_not_invent` 对来源外人物主体、年份、战争标记和未登记正式 ID 执行确定性阻断。
- rumor source → fact、historical source → current 的结构性升级会被阻断。
- 建档 provenance 继续保留候选级诊断与语义链路。
- 本地 Draft payload 升级为 v2；旧 v1 payload 可迁移，未知版本安全丢弃。
- fake Worker smoke 增加 Quick Authoring：意图 → complete candidate → 语义校验 → content tier → 建档 → provenance。
- 修复 Draft smoke 的 web/worker 端口选择竞态和测试 fixture 的显式 tier。

## 验证结果

- Release solution build：0 warnings / 0 errors。
- DraftTests：`49/49 PASS`。
- BatchTests：`21/21 PASS`。
- Worldbook Studio harness：`113/113 PASS`。
- 前端 editor/session/safety/draft/batch/customer harness：全部通过。
- Draft HTTP smoke：staged、幂等、伪造 evidence、候选切换、Quick Authoring 语义闭环均通过。
- Authoring save、public contract、workstation handoff smoke：通过。
- 全量 `scripts/test.ps1`：通过。

## 仍待处理

- 多文化 requested perspectives 的覆盖率与“视角抹平”检测仍需专门语义 fixture。
- candidate-specific warnings 与 coverage 的 UI 细粒度展示仍可继续增强。
- Draft 服务端多任务续接、状态迁移版本化和定时恢复仍需独立批次。
- Batch/cache 仍主要验证结构与 source binding，尚未接入 proposition-level 语义覆盖。
- Semantic Migration 继续保持显式拒绝，不在 Quick Authoring 中冒充实现。

## 硬边界声明

- 未启动 Bannerlord。
- 未同步游戏目录。
- 未访问真实 Cloud Provider、API Key、Token、Worker 或网络服务。
- 未修改 AWAKE 模组本体、ModuleData、dist 或世界书迁移候选。
- 未将任何候选标记为 `approved`、`canon`、`compiled`、`published` 或 `runtime-ready`。
