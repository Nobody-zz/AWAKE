# 正式周报 v2：生成与校验 E2 实现审查

## 审查范围

- 计划：`docs/PLAN-WORLD-REPORT-V2-BUILD-E2-20260912.md`
- 实现：`src/WeeklyReportService.cs`、`tools/worldbook-runtime-smoke/Program.cs`、`tools/worldbook-contract/v2/`
- 只核对本切片的生成、校验、canonical fingerprint、fixture 和 smoke 证据。
- 明确排除存储、生命周期、菜单、Native、事件、记忆、对话和 Worldbook 内容。

## 结果

`REVIEW_TARGET: AWAKE-WORLD-REPORT-V2-BUILD-E2-20260912`

`REVISION: implementation round 1`

`VERDICT: APPROVED`

## 证据

- `dotnet build AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-restore`：PASS，0 warning，0 error。
- `dotnet run --project AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-build`：PASS。
- smoke 已执行 `TestWorldReportV2Build`，覆盖有效事实窗口、空窗口、重复生成、schema/结构校验、来源闭包错误、窗口错误、fingerprint 错误、legacy fallback 和失败 Journal。
- golden fixture 与生成结果的 JSON、canonical JSON、fingerprint 均匹配；v2 schema 文件身份和严格字段边界已检查。
- `git diff --check`：PASS。
- 未同步游戏目录，未启动游戏。

## 实现核对

1. `WeeklyReportService.TryBuildV2FromFacts` 只消费 `WeeklyDynamicsInput`，不会重新读取 Ledger 或独立 currentDay。
2. v2 的版本、报告 ID、生成者、policy、Unix epoch period、section/domain 顺序、item ID、中文文本、visibility 和 extensions 与批准计划一致。
3. canonicalizer 递归按 ordinal 属性名排序，数组保持生成顺序，使用紧凑 JSON 和大写 SHA-256；`contentFingerprint` 不参与自身计算。
4. v2 validator 对未知字段、null、窗口、来源 ID、来源闭包、section/item 结构、visibility 和 fingerprint fail-closed。
5. v1 旧路径未被改接；`WorldStateStore`、`WorldEventContracts` 和 `SmokeStubs` 未纳入本次 v2 生成切片。

## 变更后债务检查

- 审查范围：本次改动文件及其一层调用/fixture 邻域。
- confirmed dead code：0。
- confirmed duplicate authority：0；v1 与 v2 入口保持分离。
- confirmed performance issue：0；本切片为离线/低频纯生成路径。
- suspected risk：golden fixture 仍需在后续修改时保持人工固定，不得改成运行时自生成；这是维护约束，不阻断当前实现。

## 当前门禁

```text
plan review = APPROVED
user sign-off = true
implementation = completed
implementation review = APPROVED
E2 = PASS
storage/lifecycle/game evidence = not claimed
```
