# 世界事实到周报：最小端到端切片（E2）

状态：`implemented_approved_e2`

本文件是当前唯一权威的“事实 Journal → 周报生成”实现计划。此前的耦合计划、周报计划和综合报告计划只作历史追溯，不得作为当前实现依据。

## 1. 目标

先证明一条真实、可重复、可验收的最小链路：

```text
WorldFactJournal
  → WorldFactQuery(WeeklyDynamics)
  → WeeklyReportService.BuildFromFacts
  → 确定性的报告 JSON 和文本
```

本切片只证明“查询结果能正确变成周报内容”。它不声称已经完成存档、生命周期、菜单或游戏内最终功能。

## 2. 唯一输入规则

正式生成器只接受一个 `WeeklyDynamicsInput`（名称可按现有代码风格调整），不得直接接受 `WorldEventRecord`、Ledger 快照或 `currentDay`。

输入必须包含：

- `Policy = WeeklyDynamics`；
- `Status = Success` 或 `Empty`；
- `UsedLegacyFallback = false`；
- `WindowStartDay`、`WindowEndDay`，且 `WindowStartDay = WindowEndDay - 6`；
- `WindowEndDay >= 7` 且 `WindowEndDay % 7 == 0`；
- `Facts` 与 `SourceFactIds` 一一对应、无重复、顺序稳定。

状态映射固定为：Journal 合法且本窗口选中事实数为零时为 `Empty`；至少一条时为 `Success`；Journal 缺失、损坏或不可用不得改写为空结果。

正式生成器拒绝 `Missing`、`Corrupt`、`Unavailable`、legacy fallback 和未结算周。拒绝必须返回稳定错误码，不生成报告 JSON。

## 3. 最小报告输出

本切片输出内部 v0 报告对象，不写入存档。输出只包含能够由事实直接得到的内容：

- `schemaVersion = awake.worldbook.weekly-report.v0-preview`；
- `windowStartDay`、`windowEndDay`；
- 按固定顺序排列的 `sections`；
- 每个 item 的 `text` 和非空 `sourceFactIds`；
- `sourceFactIds` 顶层闭包；
- 空窗口不生成 item，不填充虚构文字。

每条输入 fact 必须且只能出现在一个 item 的 `sourceFactIds` 中；不得静默丢弃、合并或重复使用 fact。输入为空时才允许没有 item。

栏目顺序固定为 `politics`、`war`、`people`、`local`。栏目标题沿用当前朴素用词：政治与外交、战争与领地、人物近况、地方情况。事实没有对应栏目时不生成该栏目内容。

来源规则只有一条：v0 使用 `sourceFactIds` 作为唯一来源闭包，不带 `sourceEventIds`，不做 v1/v2 兼容。

## 4. 确定性规则

- 查询窗口由显式 `WindowStartDay/WindowEndDay` 决定，不从报告生成器重新推导。
- facts 先按 `campaignDay` 升序、`timeSlot` 升序、`factId` ordinal 升序排序。
- 顶层 `sourceFactIds` 和 item 级 `sourceFactIds` 均按 `factId` ordinal 升序去重。
- sections 按固定栏目顺序；items 按上述事实顺序。
- JSON 对象属性按输出字段声明顺序写入；相同输入重复构建得到结构化 JSON、规范化 JSON 文本和显示文本完全一致；不同窗口或不同事实集合不得复用旧输出。

稳定错误码固定为：`awake.world_fact.report.window_invalid`、`awake.world_fact.report.query_failed`、`awake.world_fact.report.legacy_fallback`、`awake.world_fact.report.fact_invalid`、`awake.world_fact.report.source_closure_invalid`。错误码以外的异常文本只能写日志，不进入报告输出。

## 5. 实现写集

允许修改：

- `src/WorldFactQuery.cs`：补齐显式七日窗口、已结算周校验和结果元数据；
- `src/WeeklyReportService.cs`：增加只接受 `WeeklyDynamicsInput` 的纯报告构建入口；
- `src/WorldEventContracts.cs`：提供查询结果到 typed input 的唯一转换 seam；
- `tools/worldbook-runtime-smoke/Program.cs`：增加最小端到端 fixture 和失败矩阵。

本切片不修改：报告持久化、`WorldStateStore` 状态协议、Native readiness、CampaignSessionReady、菜单/BrowserVM、语言资源、知识投影、人物记忆、事件触发、Worldbook、Persona、对话和 Marcus 公共 API。

## 6. E2 验收

| 场景 | 必须观察到 |
| --- | --- |
| 两条有效事实 | 生成固定栏目、固定排序和完整 sourceFactIds 闭包 |
| 同事实重复构建 | JSON 与文本完全一致 |
| 第 10 天未结算窗口 | 拒绝，不生成报告 |
| 第 7/14 天完整窗口 | 接受并生成对应窗口报告 |
| 有效空窗口 | 合法空报告，无 item、无虚构填充 |
| Journal missing/corrupt/unavailable | 分别保留失败状态，不转为空报告 |
| legacy fallback | 正式生成器拒绝 |
| 缺失或重复 factId | 拒绝并返回稳定错误码 |
| 输入 fact 未完整映射 | 拒绝并返回 `source_closure_invalid` |

验证命令：

```text
dotnet build AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-restore
Awake.SdkSmoke.exe --world-fact-report-minimal
```

只要求 E2 离线证据；不运行游戏，不同步游戏目录，不产生 E3/E4/E5 声称。

## 7. 门禁

```text
本计划独立静态审查 APPROVED
  → 用户签收
  → 实施最小切片
  → E2 build/smoke
  → 独立实现审查
```

用户已签收本最小切片并授权实施；实现结果仍需独立实现审查。E2 build/smoke 通过也不等于正式周报、存档或游戏内功能通过。

## 8. 后续扩展顺序

只有本切片实现审查通过后，才分别评估：

1. 报告 v1/v2 和持久化；
2. 生命周期与 Native-independent 调度；
3. 菜单正式/预览/不可用状态；
4. 知识投影；
5. 事件触发、人物记忆和对话消费。

这些不是本切片的隐含承诺。
