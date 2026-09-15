# 世界事实到周报：最小端到端切片独立静态复审

## 审查范围

- 计划：`PLAN-WORLD-FACT-REPORT-MINIMAL-E2-20260912.md`
- 对照实现：`WorldFactQuery.cs`、`WeeklyReportService.cs`、`WorldEventContracts.cs`
- 本轮只审查计划是否可实施、可验收；未编辑源码、未构建、未同步、未启动游戏。

## 结论

`VERDICT: APPROVED`

该计划已经成功收窄到一个可独立验收的 E2 离线切片，当前没有发现阻止实施的 P0/P1。它只证明“结构化事实经过 WeeklyDynamics 查询后能够生成确定性报告”，没有把尚未完成的存档、菜单、Native、知识投影或游戏内行为包装成已完成能力。

## 通过依据

1. **单一入口**：正式构建器只接受 typed `WeeklyDynamicsInput`，不再接受 Ledger 快照或独立 `currentDay`。
2. **窗口可判定**：窗口必须是 `[endDay-6,endDay]`，且 `endDay >= 7`、`endDay % 7 == 0`，明确排除未结算周。
3. **状态不混淆**：合法空窗口、成功窗口、missing、corrupt、unavailable 和 legacy fallback 有独立处理要求，不以空报告吞掉失败。
4. **来源闭包明确**：每条输入 fact 必须且只能进入一个 item；顶层和 item 级 `sourceFactIds` 去重并满足闭包。
5. **确定性明确**：事实、来源数组、栏目、item 和 JSON 属性均有固定顺序；同输入必须得到相同 JSON 和文本。
6. **范围受控**：不写存档、不接生命周期、不碰 Native、不改菜单和其他模组层，失败面已显著小于原计划。
7. **验收可执行**：已有 runtime smoke 入口可扩展，验收覆盖有效事实、重复构建、完整/未结算窗口、空窗口、失败状态、fallback 和闭包错误。

## 门禁状态

本审查只批准“按该计划进入实施准备”，不批准代码实现本身：

```text
计划 APPROVED
  → 等待用户签收
  → 才允许实现
  → E2 build/smoke
  → 实现结果独立审查
```

当前仍不得把 E2 计划通过描述为正式周报功能已完成。

