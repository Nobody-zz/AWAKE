# AWAKE world fact context interface checkpoint — 2026-09-12

- `batch`: `AWAKE-WORLD-FACT-CONTEXT-INTERFACE`
- `plan`: `docs/PLAN-AWAKE-WORLD-FACT-CONTEXT-INTERFACE-20260912.md`
- `status`: `approved_e2_complete`
- `scope`: 事实只读查询 seam、事件候选 facade、事件 hourly 观察调用；世界书、角色卡、对话、记忆保持独立且可继续开发
- `evidence_ceiling`: `E2`

## 已完成

- `WorldFactQuery` 实现 `IWorldFactContextReader`，结果增加可缺失的 `JournalRevision` 和独立 `LegacyFallbackState`。
- 取消优先于请求校验；预取消和读取取消统一为 `Cancelled / awake.cancelled`。
- `WorldEventContracts.QueryEventTriggerCandidatesAsync` 是事件候选唯一 facade，固定关闭 legacy fallback，并记录状态、revision、数量、错误码和 correlation id。
- `AwakeEventEngine` 每个 hourly tick 在既有规则链前观察一次候选；空、失败和非正式 fallback 不改变原有规则/Command/效果链，取消直接返回。

## 验证

- focused runtime smoke：通过。
- production runtime smoke：`19/19` 通过，包含真实 hourly caller fixture。
- AWAKE Release build：通过，`AWAKE/_build_out/1.3.15/Release/Awake.dll`。
- 本轮复跑：focused smoke 通过、production smoke `19/19` 通过、Release build 通过。
- 全量 `AWAKE.Tests`：除已知 Persona golden fixture `approved persona should generate canonical authored DSL` 外通过；该失败属于并行 Persona 轨道，未在本批处理。

## 边界与后续

- 未同步游戏目录、未启动游戏；没有 E3/E4/E5 证据。
- 独立实现复审尚未完成前，不把本 checkpoint 标记为最终闭环。
- 下一步是完成独立实现复审；若无新增 P0/P1，再决定是否将本批收口并进入后续事件/对话消费层规划。

## 独立复审记录

- 独立复审返回 `REVISE`，报告 0 个 P0、1 个 P1：建议 `Unavailable/Corrupt/Missing` 阻断既有事件规则链。
- 该建议与已签收计划第 5 节的明确约束冲突：这些状态必须只观察、不改变既有规则选择；只有 `Cancelled` 立即停止。因此未按该建议改变生产行为。
- 为增加可见性曾尝试补充 `Unavailable + Always` 的真实规则夹具，但无游戏 UI 的 smoke 宿主会在弹窗队列上等待，夹具已撤回；没有留下代码或测试进程。
- 撤回后 production smoke 恢复为 `19/19`，Release build 通过。当前仍不把独立复审标记为 APPROVED，后续如需正式收口应由复审方针对该计划冲突重新裁决。

## 裁决请求

- Event facade 在本批是旁观输入，不是事件规则门；非取消状态必须保持既有规则链，只有 `Cancelled` 阻断。
- legacy fallback 已在 query/facade 层拒绝为正式 Event candidate，Engine 不消费失败结果中的事实。
- 请独立复审确认上述计划语义与实现是否一致。

## 最终独立复审

- 结论：P0 `0`、P1 `0`、P2 `0`，`VERDICT: APPROVED`。
- 复审确认：Engine 只在 `Cancelled` 时阻断；其他事实状态保持既有事件链；Event facade 固定拒绝 legacy fallback；revision 缺失 fail-closed。
- 本批以离线 `E2` 收口；无游戏同步、无游戏启动、无 E3/E4/E5 声明。
