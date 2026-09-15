# 世界事实到正式周报耦合契约：独立静态复审 Round 3

## 审查范围

- 目标计划：`PLAN-WORLD-FACT-REPORT-COUPLING-20260912.md`
- 联动计划：`PLAN-WEEKLY-DYNAMICS-20260912.md`
- 追溯计划：`PLAN-WORLD-REPORT-FOUNDATION-20260912.md`
- 对照源码：`WorldFactQuery.cs`、`WorldEventContracts.cs`、`WeeklyReportService.cs`
- 本轮仅审查文档与当前源码状态；未编辑源码、未构建、未同步、未启动游戏。

## 结论

`VERDICT: REVISE`

本轮未发现新的 P0，但仍有 P1 未闭合，因此不能标记 `APPROVED`，也不能请用户签收。上一轮发现的两个核心方向已经写入计划：正式周报应消费查询层、v2 应以 `sourceFactIds` 建立事实闭包；但下面的边界仍可能让不同实施者写出不同结果。

## 未解决 P1

### P1-1：完整周窗口没有锁定“已结算周”的日历条件

耦合计划第 78 行只要求 `windowEndDay >= 7`，没有要求 `windowEndDay` 是 7 的倍数。当前 `QueryWeeklyDynamicsAsync`（`WorldEventContracts.cs:218–225`）把 `currentDay` 直接传入查询，而 `WorldFactQuery.ResolveWindow`（`WorldFactQuery.cs:304–308`）会生成 `[currentDay-6,currentDay]`。这允许在第 10 天生成 `[4,10]` 的“正式周报”，与周报计划所说的已完成七日窗口不一致。

最小修正：固定 `windowEndDay >= 7 && windowEndDay % 7 == 0`，`windowStartDay = windowEndDay - 6`；正式入口接收已结算的 `windowEndDay`，不得使用当前日直接推导。加入第 6、7、10、14 天的正反 fixture。

### P1-2：来源数组的排序规则自相矛盾

耦合计划第 61 行要求 `sourceFactIds`“按报告生成顺序排列”，第 82 行又要求“所有来源集合按 ordinal 排序”。这两种顺序在事实生成顺序与字典序不一致时会产生不同 JSON、不同 fingerprint 和不同重试结果。

最小修正：明确顶层与 item 级 `sourceFactIds/sourceEventIds` 全部使用 ordinal 排序；sections/items 的数组顺序才保留生成顺序。若确实需要生成顺序，必须把它定义为生成器排序规则并删除“集合按 ordinal 排序”的冲突表述。补充顺序相反的事实 fixture。

### P1-3：`Success`、`Empty` 与“筛选后无事实”的映射未锁定

计划第 27–32 行允许正式输入为 `Success` 或 `Empty`，第 109–111 行又要求只有合法空窗口才生成空报告。但当前查询实现中，Journal `Success` 后即使所有事实被策略排除，`Select` 仍返回 `WorldFactQueryStatus.Success`（`WorldFactQuery.cs:221–227`）；Journal `Empty` 才直接返回 `Empty`（`WorldFactQuery.cs:130–133`）。因此“有效 Journal 但该周无可报告事实”和“Journal 有事实但全部不满足策略”目前没有明确相同或不同的语义。

最小修正：固定状态矩阵：合法 Journal 且窗口内选中事实数为零返回 `Empty`；至少一条返回 `Success`；Journal 缺失、损坏、不可用保持独立状态。明确被策略排除的事实是否计入 `Decisions` 但不进入来源闭包，并加入对应 fixture。

### P1-4：`JournalDigest` 的来源和算法仍是占位字段

计划第 30–32 行要求 typed 输入携带 `JournalRevision` 与 `JournalDigest`，但没有定义 digest 覆盖的 canonical 内容、编码、哈希算法、大小写和空窗口值。没有该定义，报告 fingerprint 只能绑定一个未定义字符串，重读确认也无法证明使用的是同一事实快照。

最小修正：固定 digest 为“本次查询实际读取并通过 schema 校验的事实集合”的 canonical UTF-8 JSON SHA-256，定义事实排序、对象字段排序、空集合 digest 和大写/小写格式；把预期值加入 fixture。若不需要该绑定，则从 typed 输入和计划中删除该字段。

### P1-5：追溯计划仍保留可被误读的旧 v2 契约

耦合计划第 139 行要求 superseded 的 `PLAN-WORLD-REPORT-FOUNDATION-20260912.md` 指向当前契约，但该文件当前仍在第 275–284 行把 v2 顶层字段固定为不含 `sourceFactIds` 的旧集合。它虽然标记为 `split_superseded_not_implementable`，但机械化实施者若按文件名检索 v2 规则，仍可能选择错误定义。

最小修正：在旧计划文件顶部增加醒目的 `SUPERSEDED / DO NOT IMPLEMENT` 及当前权威文件路径；删除或包裹旧 v2 字段段落，明确其仅为历史记录。不能只依靠正文后部的 superseded 说明。

### P1-6：v2 item 字段的必选/可选和空数组约束不完整

计划第 65 行称 item 字段“固定”为五项，但没有区分 JSON 必选字段与允许省略的字段，也没有明确 `sourceEventIds` 顶层和 item 级必须始终存在为空数组，还是允许缺失。schema 实施者可能得到不同的 `additionalProperties`、`required` 和空报告行为。

最小修正：明确 v2 顶层 11 个字段全部 required；每个 item 的 `itemId/text/sourceFactIds/sourceEventIds` required，`entryId` optional；两个来源数组都必须是字符串数组、允许为空，且分别满足顶层闭包关系。补充无 legacy 映射与有 legacy 映射的 schema fixture。

## 已确认没有回退的问题

- 活动周报计划已改为引用耦合契约，不再把旧 Window Storage 计划写成当前实现依赖。
- 当前状态文件已明确 `user_signoff=false`、`implementation_allowed=false`，不会把“计划修订完成”误报为授权。
- 当前正式报告源码仍走旧 Ledger/v1 路径，这与耦合计划中的“待实现”记录一致；它仍然不是通过证据。

## 下一步门禁

1. 修订上述 6 个 P1，尤其先锁定已结算周窗口和来源排序。
2. 给 superseded 追溯计划加顶部阻断标记。
3. 更新 plan hash 和 review state。
4. 再做一次独立静态复审；只有达到 `APPROVED` 后，才进入用户签收，不提前实施 v2。

