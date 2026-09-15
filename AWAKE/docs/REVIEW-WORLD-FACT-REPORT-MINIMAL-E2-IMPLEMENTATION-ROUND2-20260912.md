# 世界事实到周报：最小 E2 实施结果复审 Round 2

## 审查范围

- 计划：`PLAN-WORLD-FACT-REPORT-MINIMAL-E2-20260912.md`
- 实现：`WorldFactQuery.cs`、`WeeklyReportService.cs`、`WorldEventContracts.cs`
- 测试：`tools/worldbook-runtime-smoke/Program.cs`
- 复核内容：上一轮 P0/P1 修复、调用链、失败边界和 E2 证据。
- 本轮只读复审；未编辑源码、未同步、未启动游戏。

## 结论

`VERDICT: APPROVED`

在本计划限定的“库层 + 生产-neutral seam”范围内，上一轮问题已关闭，E2 实施结果通过。这个结论不扩大到现有正式报告的存档/生命周期入口；那些入口仍属于后续切片。

## 修复确认

1. `BuildCompletedWeeklyReportFromFactsAsync`（`WorldEventContracts.cs:246–252`）现在是生产可调用的无存档副作用入口，顺序固定为 `QueryCompletedWeeklyDynamicsAsync → WeeklyDynamicsInput.TryCreate → TryBuildFromFacts`。
2. `TryBuildWeeklyReportFromQueryResult`（`WorldEventContracts.cs:254–260`）提供唯一 typed 转换 seam；smoke 已通过该 seam 调用，而不是只直接调用报告服务。
3. `WeeklyReportService.FactDomain` 对未知 domain 返回空值并由输入校验返回 `awake.world_fact.report.fact_invalid`；smoke 已覆盖未知 domain，不再静默归入政治栏目。
4. 完整窗口、空窗口、Journal failure、legacy fallback、重复 factId、确定性 JSON/文本均已有断言。

## 通过证据

- `dotnet build AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-restore`：通过；
- runtime smoke：通过；
- `dotnet build AWAKE.Tests/AWAKE.Tests.csproj --no-restore`：通过；
- `Awake.SdkSmoke.exe --world-fact-journal`：通过；
- `AWAKE/tools/build.ps1 -Configuration Release`：通过；
- `git diff --check`：通过。

## 范围边界

当前 `EnsureFormalReportsReadyAsync` 仍保留旧 Ledger/v1 路径，这是有意保留的后续存档/正式报告切片，不作为本轮 E2 通过证据。当前没有 v0 报告存档、菜单接线、CampaignSessionReady 调度、Native 投影或游戏内验证。

## 门禁结果

```text
minimal E2 library/seam = APPROVED
user plan sign-off = recorded
implementation result = APPROVED for this scope
save/menu/lifecycle/game = not claimed
```

下一步应另开正式报告接线切片，不能直接把本轮 v0 内存结果当作已持久化的“本周动态”。

