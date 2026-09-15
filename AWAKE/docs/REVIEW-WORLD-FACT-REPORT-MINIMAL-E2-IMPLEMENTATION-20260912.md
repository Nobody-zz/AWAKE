# 世界事实到周报：最小 E2 实施结果审查

## 审查范围

- 计划：`PLAN-WORLD-FACT-REPORT-MINIMAL-E2-20260912.md`
- 实现：`WorldFactQuery.cs`、`WeeklyReportService.cs`、`WorldEventContracts.cs`
- 测试：`tools/worldbook-runtime-smoke/Program.cs`
- 本轮只读审查现有实现和已生成证据；未编辑源码、未同步、未启动游戏。

## 结论

`VERDICT: REVISE`

E2 离线库层测试和构建均已通过，但实现结果还不能签收为“最小端到端链路完成”。核心缺口是：新链路目前只在 smoke 中被直接调用，生产正式报告入口仍然使用旧 Ledger/v1 路径。

## P0-1：新链路没有接入生产编排入口

证据：

- 新增 `QueryCompletedWeeklyDynamicsAsync`（`WorldEventContracts.cs:228–237`）没有生产调用点；
- `TryBuildFromFacts`（`WeeklyReportService.cs:76–128`）当前只在 smoke（`Program.cs:273–380`）中调用；
- `EnsureFormalReportsReadyAsync`（`WorldEventContracts.cs:325–346`）仍调用 `Recorder.LoadAsync`、`WorldEventLedger.CaptureSnapshot()` 和 `Reports.BuildWindow(IReadOnlyList<WorldEventRecord>, ...)`；
- 旧 `BuildWindow` 仍通过 `IWorldEventServices` 暴露（`WorldEventContracts.cs:100–104,507–510`）。

影响：本轮证明了“测试代码可以从 Journal 查询并生成 v0 报告”，但模组正式报告服务仍不会消费 Journal/WeeklyDynamics 输入。运行时可能继续读取容量受限的 Ledger 快照，无法宣称生产链路已切换。

最小修正：增加一个生产可调用的、无存档副作用的 `BuildCompletedWeeklyReportFromFactsAsync`（或同等唯一入口），内部只能调用显式已结算周查询、`WeeklyDynamicsInput.TryCreate` 和 `TryBuildFromFacts`；为该入口增加调用点测试。旧 Ledger/v1 入口只能作为兼容路径，必须明确标记为非本轮正式入口。

如果本轮确实只想交付库层而不接生产编排，则应把计划目标从“最小端到端链路”改成“纯函数库层 proof”，并将本项从后续计划中明确列出，而不能同时使用两种说法。

## P1-1：未知 domain 会被静默归入 politics

证据：`WeeklyReportService.FactDomain` 对未知 domain 和非战争 kind 最终返回 `politics`。计划要求没有对应栏目的事实不生成该栏目内容；当前实现却会把未知数据误报为政治与外交。

最小修正：未知 domain 返回明确的 `fact_invalid`/`source_closure_invalid`，或按计划明确规定安全丢弃并在 decisions/log 中记录；增加未知 domain fixture。不得静默改写栏目归属。

## P1-2：实施证据没有独立验证生产入口的不可达性/可达性

当前 smoke 直接实例化 `WorldFactQuery`，没有验证 `WorldEventContracts` 的新入口能被调用，也没有验证正式报告入口不会误用 `BuildWindow`。因此测试通过只证明类级组合，不证明实际 AWAKE 调用链。

最小修正：至少增加一个 service seam 测试：给 Journal reader 注入固定事实，调用生产可调用的新入口，断言得到 v0 输出；同时对旧 formal caller 做静态 fail-closed 检查或明确其仅为 legacy compatibility。

## 已通过项目

- 显式七日窗口的边界校验已实现；未结算周 fixture 被拒绝。
- `Success/Empty`、Journal failure、legacy fallback 和重复 factId 已有聚焦断言。
- 报告来源使用 `sourceFactIds`，每条输入 fact 在 item 来源闭包中出现一次。
- 相同输入的结构化 JSON 和文本重复构建一致。
- runtime smoke、AWAKE.Tests 编译、WorldFactJournal smoke、Release build 均通过。

## 当前门禁

```text
E2 library proof = PASS
production report wiring = NOT_PROVEN
overall implementation = REVISE
user signoff for this implementation result = not requested
```

本轮不能标记实现通过。下一步应先决定是补齐生产-neutral 调用 seam，还是把交付名称收窄为“库层 proof”；在决定前不应接入存档、菜单或游戏生命周期。

