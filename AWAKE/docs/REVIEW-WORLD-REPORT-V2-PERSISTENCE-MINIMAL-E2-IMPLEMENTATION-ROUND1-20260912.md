# 正式周报 v2：最小快照持久化 E2 实现审查

## 审查范围

- 计划：`docs/PLAN-WORLD-REPORT-V2-PERSISTENCE-MINIMAL-E2-20260912.md`
- 实现：`src/WorldEventContracts.cs`、`src/WorldStateStore.cs`、`tools/worldbook-runtime-smoke/SmokeStubs.cs`、`tools/worldbook-runtime-smoke/Program.cs`
- 只核对最小 v2 快照保存、重开读取、幂等、冲突、有限 repair 和 v1 保留。
- 不把 storage adapter、CAS、事务、unknown-write、生命周期、菜单或游戏运行证据纳入本结论。

## 结果

`REVIEW_TARGET: AWAKE-WORLD-REPORT-V2-PERSISTENCE-MINIMAL-E2-20260912`

`REVISION: implementation round 1`

`VERDICT: APPROVED`

## 证据

- `& AWAKE/tools/build.ps1 -Configuration Release`：PASS，生成 `Awake.dll`。
- `dotnet build AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-restore`：PASS，0 warning，0 error。
- `dotnet run --project AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-build`：PASS。
- `TestWorldReportV2PersistenceMinimal` 已覆盖新写入、重开读取、相同 fingerprint 幂等、不同 fingerprint 冲突、损坏 v2 repair、非法 payload、读取/写入失败和 v1 不覆盖。
- fixture 使用 JSON backing 重开，重新验证 v2 envelope、payload 和 fingerprint。
- `git diff --check`：PASS。
- 未同步游戏目录，未启动游戏。

## 实现核对

1. v2 报告先通过 `WeeklyReportService.TryValidateV2Report`，非法 payload 不进入写队列。
2. v2 entry 额外保存 schemaVersion、窗口、contentFingerprint 和 sourceFactIds，并在读取时与 payload 逐项比对。
3. 同 reportId 且相同完整内容返回 `already_applied`；不同 fingerprint 返回 `conflict`，不改旧快照。
4. 损坏 v2 entry 可由完整的新 v2 报告修复；同 reportId 的 v1 entry 返回冲突并保持原样。
5. 读取、入队或执行失败返回 `retryable`；本切片没有把不确定结果伪装成成功。
6. 新增 seam 尚未接入 CampaignSessionReady、菜单或 Native Knowledge，符合当前切片边界。

## 非本轮结论

- 未证明生产 storage adapter 的 atomic old-or-new 语义。
- 未实现 unknown-write 自动确认/补偿。
- 未接入周报生命周期、菜单展示或游戏运行时。

## 当前状态

```text
plan review = APPROVED
user sign-off = true
implementation = completed
implementation review = APPROVED
E2 = PASS
```
