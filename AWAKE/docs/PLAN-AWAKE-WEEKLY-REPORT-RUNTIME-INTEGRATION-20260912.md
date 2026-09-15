# AWAKE 周报运行时接入最小切片

状态：`implemented_approved_e2`
范围：把已通过的事实查询、v2 周报生成和最小持久化接到 AWAKE 本体运行时。

## 目标

形成一条可追踪的运行时链路：

```text
CampaignSessionReady
  → storage-only readiness（只 Evaluate，不 Request）
  → 已安装 WorldStateStore
  → WeeklyDynamics 查询
  → v2 周报生成/复用
  → weeklyReports 持久化
```

Native Knowledge 只作为之后的投影通路，不得决定正式周报是否生成。

## 固定边界

- 存储恢复和正式周报任务由 `CampaignSessionReady` 独立启动，不等待 Native readiness。
- 后台 storage-only readiness 只调用 `PermissionGate.Evaluate`；不得调用 `EnsureAsync`、`RequestAsync` 或 UI dispatcher。
- 恢复函数只消费已经安装且仍属于当前 session 的 `WorldStateStore`，不得再次确保存储就绪。
- 正式周报只消费 `QueryCompletedWeeklyDynamicsAsync` 返回的 `WeeklyDynamicsInput`，拒绝 legacy fallback、旧 Ledger 快照和 v1 生成路径。
- 只生成或复用最近一个已完成窗口；不补造更早缺失窗口。
- Native continuation 只调用 Native-gated 知识投影；投影失败不回滚或删除已保存周报。
- 不修改存储适配器契约、事件触发、人物记忆、对话、Worldbook 内容、菜单布局、同步或游戏目录。

## 写集

- `src/AwakeRuntime.cs`
- `src/ProbeExtension.cs`
- `src/WorldEventContracts.cs`
- `src/WeeklyReportService.cs`
- `src/WorldKnowledgeProjectionService.cs`
- `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsBoundary.cs`
- `tools/worldbook-runtime-production-smoke/ProductionSmokeAdapters.cs`
- 必要时更新生产冒烟入口的最小辅助字段；不改框架工程。

## 实施规则

1. `AwakeRuntime` 增加 storage-only readiness，并抽取与现有交互式 readiness 共用的 store 打开/安装逻辑。
2. `ProbeExtension` 在 `CampaignSessionReady` 同时启动 storage task 和 Native task：前者负责恢复与正式周报，后者只负责知识投影。
3. `EnsureFormalReportsReadyAsync` 改为读取已结算事实查询结果并构建/持久化 v2；已有合法 v2 直接复用。v2 顶层来源按契约的 factId ordinal 顺序输出，不能与 item 的时间排序混用。
4. 周报契约验证入口同时支持既有 v1 读取和 v2 运行时投影；v2 投影读取 `sourceFactIds`，v1 数据不被改写。
5. Native continuation 只读取已保存的正式报告并执行投影，不再负责生成或持久化报告。
6. 生产冒烟覆盖：storage-only 路径不调用权限请求、Native 未就绪仍能生成 v2、重复调用幂等、Native 就绪后仅读取并投影 v2。

## 验收

- `CampaignSessionReady` 在 Native false 时仍能完成 storage task；正式 v2 report 可读回。
- 权限未预授权时后台路径返回不可用，`RequestAsync` 调用次数为 0。
- 恢复任务不调用 `EnsureWorldStateReadyAsync`。
- 同一窗口重复运行不新增报告写入。
- v2 报告可被知识投影验证并显示，v1 报告仍可读取。
- Native continuation 不触发周报写入；权限适配器的 `RequestAsync` 调用次数保持为 0。
- Release build、生产 runtime smoke、普通 runtime smoke 和 `git diff --check` 通过。

## 证据边界

本切片最高只声明 E2（构建与离线/生产冒烟）。不声明游戏内 E4/E5；不执行同步和游戏启动。

## 实施结果

- CampaignSessionReady 已独立启动 storage-only readiness；后台权限只走 `Evaluate`，恢复路径复用已安装 store，不再触发 `EnsureAsync`/`RequestAsync`。
- 正式报告已改为 `WeeklyDynamics → WeeklyDynamicsInput → TryBuildV2FromFacts → v2 persistence`；Native continuation 只读取已保存报告并执行知识投影。
- v2 写入成功后增加真实存储读回确认，校验 reportId、schema、窗口、状态、指纹和 canonical JSON；不一致返回 `awake.world_report.v2.readback_mismatch`。
- v2 的空 `visibility.identity_ids` 固定解释为公开摘要；v1 报告保持可读取、可投影且不被 v2 覆盖。
- 候选存储替换改为候选先打开，候选失败时保留现有 LKG owner；成功后仍等待旧 store drain 再交换。

## E2 验证结果（2026-09-12）

- `WorldbookRuntimeProductionSmoke`：`passed=18 failed=0`。
- `WorldbookRuntimeSmoke`：通过。
- `AWAKE/tools/build.ps1 -Configuration Release`：`BUILD_OK api=1.3.15`。
- `git diff --check`：通过（仅有工作树换行格式提示，无 diff check 错误）。
- 独立实施终审：`APPROVED`，P0/P1 均为零；剩余 P2 仅为旧 preview helper 的命名歧义，不在本切片生产调用链内。

全量 `AWAKE.Tests` 的 SDK smoke 已成功编译并运行，但在既有人物 DSL 断言处失败；该失败不作为本切片通过依据，也未在本切片中扩展修复。
