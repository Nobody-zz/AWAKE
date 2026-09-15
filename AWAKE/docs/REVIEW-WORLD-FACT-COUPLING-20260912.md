# 世界事实耦合链路审查

- 范围：事实 Journal、`WorldFactQuery`、正式周报、近期预览、世界知识投影之间的依赖与来源闭包。
- 审查方式：源码与计划只读核对；未编辑、未构建、未同步、未启动游戏。
- 依据：`PLAN-WORLD-FACT-FOUNDATION-20260912.md`、`PLAN-WEEKLY-DYNAMICS-20260912.md`、`PLAN-WORLD-REPORT-FOUNDATION-20260912.md` 及当前 AWAKE 源码。

## 结论

`REVISE`。分层方向正确，但正式周报还没有真正接入查询层，因此当前不能宣称“事实 → 查询 → 周报/知识”的统一闭环已经完成。

## P0

### P0-1：正式周报仍直接读取旧事件账本

证据：`WorldEventContracts.cs` 的 `EnsureFormalReportsReadyAsync` 仍调用 `Recorder.LoadAsync`、`WorldEventLedger.CaptureSnapshot`，再把 records 交给 `Reports.BuildWindow`；`WeeklyReportService.BuildWindow` 仍以 `WorldEventRecord` 为输入。

影响：`WeeklyDynamics` 不是正式周报的唯一入口，Journal 中的结构化事实可能不进入正式周报；这违反事实计划关于消费者不得直接枚举旧账本的约束。

最小修正：正式周报生成器只接受 `WorldFactQueryResult`，并且调用 `QueryWeeklyDynamicsAsync`；旧 v1 只保留为读取兼容旁路。正式报告必须在写入前保存并校验选中的 `sourceFactIds`。

## P1

### P1-1：`sourceFactIds` 与 `sourceEventIds` 的契约没有统一

事实查询结果提供 `SourceFactIds`，但当前 v1/v2 周报计划把 `sourceEventIds` 作为必选来源字段；当前 `WorldFact` 的 `factId` 不是旧事件 ID 格式，且正式报告尚未定义两者的确定性映射。

最小修正：在周报契约中明确 `sourceFactIds` 为事实闭包，`sourceEventIds` 仅为兼容字段；没有真实事件 ID 时不得伪造。若 v2 必须同时要求两者，应补齐 schema、canonical JSON 和 fixture。

### P1-2：世界知识投影的 seam 没有强制校验查询策略

`WorldKnowledgeProjectionService.TryReplaceFacts` 接受任意 `WorldFactQueryResult`，但没有断言 `result.Policy == WorldKnowledge`。调用方若误传 Recent、Character 或 EventTrigger 结果，投影层仍可能接受。

最小修正：投影入口拒绝非 `WorldKnowledge` 结果，并拒绝错误状态；把 policy 作为类型或显式契约字段固定下来。

### P1-3：宽查询的固定上限会丢失合法事实

`WorldFactQueryRequest.MaximumResults` 上限为 100；`EnsureKnowledgeReadyAsync` 以 100 查询世界知识。事实超过 100 条时，后续事实不会进入知识投影，违反“全部合法事实可通过宽查询找到”的验收语义。

最小修正：为 WorldKnowledge 提供分页/游标，或提供不截断的内部宽读取；报告和人物候选仍可保留独立上限。

### P1-4：legacy fallback 的正式消费规则未被代码强制

Journal `missing` 时查询层会返回 `UsedLegacyFallback=true`。当前未来周报调用方尚不存在，因此还没有代码保证 legacy fallback 只能用于预览/兼容读取，不能生成正式 v2 周报。

最小修正：正式报告入口拒绝 `UsedLegacyFallback`，返回明确的 `storage_missing`/preview 状态；知识投影可按计划保留 `legacy_import`，人物候选必须继续拒绝。

## 已确认正确的部分

- Journal 是事实的保存者，查询层是叙事消费者的统一读取入口。
- 近期预览已经使用 `QueryRecentDynamicsAsync`。
- 知识投影已经具备消费查询结果的接缝。
- 旧记录兼容路径保留事件 ID 与身份受众，且没有实体关系时不会进入人物候选。
- 查询层本身不执行写存储操作。

## 下一步

先修订并锁定周报来源闭包，再实现正式 v2 生成器与持久化调用；完成后重新审查，之后再考虑人物记忆候选或对话消费。

## 第二轮复核

复核时间：2026-09-12。上一轮审查后，相关实现没有实质性修订；P0-1、P1-1、P1-2、P1-3、P1-4 全部仍然成立。

新增治理问题：周报 review state `AWAKE-WEEKLY-DYNAMICS-MVP-20260912.review.json` 标记为 `implemented_e2_pending_game`，但 `PLAN-WEEKLY-DYNAMICS-20260912.md` 仍标记 `blocked_by_world_window_storage`，同时 `PLAN-WORLD-REPORT-FOUNDATION-20260912.md` 仍要求另一套存储前置。三者没有统一的当前权威状态，不能据此宣称周报已经完成或允许同步。

第二轮结论仍为：`REVISE`。

## 局部修订复核

已关闭两项实现缺口：

- `WorldKnowledge` 使用 `MaximumResults = 0` 表示不截断的宽查询，避免超过 100 条后静默丢失事实。
- `WorldKnowledgeProjectionService.TryReplaceFacts` 强制要求结果策略为 `WorldKnowledge`，拒绝其它策略误接入知识投影。

Runtime Smoke 已覆盖宽查询结果；正式周报直读旧账本、来源 ID 闭包、legacy 正式消费限制和周报计划状态矛盾仍未解决，因此总体结论保持 `REVISE`。
