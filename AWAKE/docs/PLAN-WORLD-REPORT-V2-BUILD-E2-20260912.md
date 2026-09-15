# 正式周报 v2：生成与校验 E2 切片

状态：`proposed_pending_independent_review`

这是当前唯一的周报开发计划。它从已通过的 `WeeklyDynamicsInput` 生成稳定的 v2 报告 JSON，并验证 schema、来源闭包和 fingerprint。它不负责保存报告，也不改变现有 v1 报告路径。

## 1. 目标

```text
WeeklyDynamicsInput
  → deterministic v2 report JSON
  → schema validation
  → canonical JSON + uppercase SHA-256 fingerprint
```

完成标准是：相同输入在不同进程、重复运行时产生相同 JSON 结构和 fingerprint；无效输入只能失败，不生成正式报告。

## 2. 明确不做

本切片不修改或调用：

- `WorldStateStore`、任何 storage adapter、报告状态 envelope；
- idempotency、conflict、repair、unknown-write、重开读取；
- `ProbeExtension.cs`、`AwakeRuntime.cs`、`AwakeEventBehavior.cs`；
- 菜单、Native Knowledge、事件触发、人物记忆、人物对话和 Worldbook 内容。

旧的持久化计划 `PLAN-WORLD-REPORT-V2-PERSISTENCE-20260912.md` 暂缓，待本切片通过后另行重写。

## 3. 输入门槛

只接受已验证的 `WeeklyDynamicsInput`：

- `Policy = WeeklyDynamics`；
- `Status = Success` 或 `Empty`；
- `UsedLegacyFallback` 不得为 true；
- `WindowStartDay >= 1`；
- `WindowEndDay >= 7`、`WindowEndDay % 7 == 0`；
- `WindowStartDay = WindowEndDay - 6`；
- 每条 fact 必须有稳定的 64 位小写十六进制 `factId`；
- facts 与 `SourceFactIds` 一一对应；空输入只能生成空报告。

不从 `WorldEventLedger`、`WorldEventRecord` 或独立 `currentDay` 重新取数。

## 4. v2 输出契约

schema 固定为：`tools/worldbook-contract/v2/weekly-report.schema.json`。

报告顶层字段严格为以下 10 个，全部必需：

```text
schemaVersion, reportId, period, generatedBy, policyVersion,
contentFingerprint, sourceFactIds, sections, visibility, extensions
```

固定值和生成规则：

- `schemaVersion = awake.worldbook.weekly-report.v2`；
- `reportId = awake:report:weekly-v2-{windowEndDay}`，数字使用 `InvariantCulture` 十进制、不补零；
- `generatedBy = awake:system:weekly-report-generator-v2`；
- `policyVersion = awake.weekly-report.policy.v2`；
- day 到 UTC 时间使用 Unix epoch `1970-01-01T00:00:00Z`；`period.start` 是 `windowStartDay` 当日 00:00:00Z，`period.end` 是 `windowEndDay + 1` 当日 00:00:00Z；字符串使用 `DateTimeOffset.ToString("O", CultureInfo.InvariantCulture)`；
- `sourceFactIds` 与 facts 使用同一固定顺序：先 `occurred.campaignDay` 升序，再 `occurred.timeSlot` 升序，最后 `factId` ordinal 升序；
- sections 固定按 `politics`、`war`、`people`、`local` 顺序，仅输出有事实的 section；
- 每条输入 fact 恰好生成一个 item，不合并、不丢弃；item 按上述事实顺序在所属 section 内排列；
- `sectionId = awake:section:weekly-v2-{windowEndDay}-{domain}`；
- `itemId = awake:item:weekly-v2-{windowEndDay}-{domain}-{index}`，index 从 0 开始，仅在该 section 内计数；
- section title 使用现有固定中文标题；item text 为 `{ "zh-CN": "第 {campaignDay} 天：{presentation.summary}" }`；
- `visibility = { "scope": "local", "min_detail": "summary", "identity_ids": [] }`；
- `extensions` 严格为整数 `awake:windowStartDay` 和 `awake:windowEndDay`；
- v2 不包含 `sourceEventIds` 或 `entryId`；
- 空报告的 `sourceFactIds` 和 `sections` 都是空数组，不生成虚构 item。

section、item、text、visibility、period、extensions 的 required、类型、枚举和 `additionalProperties=false` 必须完整写入 schema。

## 5. fingerprint

生成报告时先构造不含 `contentFingerprint` 的 payload，再计算 fingerprint：

- 递归按对象属性名的 ordinal 顺序排序；
- 数组保持生成顺序，不重新排序；
- 输出为 UTF-8、无 BOM、无空白的紧凑 JSON；
- 数字使用 JSON invariant 表示；本契约中的数字只允许整数；
- 字符串使用标准 JSON 转义；
- 不允许 null；
- canonical 输入包含除 `contentFingerprint` 外的全部 v2 字段；
- 使用 SHA-256，结果为大写十六进制 64 字符。

schema、生成器、canonicalizer 和 fixture 必须共用同一字段定义。必须落盘一份完整的 expected canonical JSON 与对应 expected fingerprint，作为固定 golden fixture。

## 6. 写集

允许修改：

- `src/WeeklyReportService.cs`：新增从 `WeeklyDynamicsInput` 生成 v2 的纯函数入口、canonicalizer 和 fingerprint；
- `tools/worldbook-contract/v2/weekly-report.schema.json`：新增 v2 schema；
- `tools/worldbook-contract/v2/fixtures/valid-report.json`；
- `tools/worldbook-contract/v2/fixtures/valid-empty-report.json`；
- `tools/worldbook-contract/v2/fixtures/invalid-source-closure.json`；
- `tools/worldbook-contract/v2/fixtures/invalid-window.json`；
- `tools/worldbook-contract/v2/fixtures/invalid-fingerprint.json`；
- `tools/worldbook-contract/v2/fixtures/expected-canonical.json`；
- `tools/worldbook-runtime-smoke/Program.cs`：新增固定测试入口 `TestWorldReportV2Build`。

不修改 `WorldStateStore.cs`、`WorldEventContracts.cs` 和 `SmokeStubs.cs`；本切片不接生产存储 seam。

## 7. E2 验收

| 场景 | 必须观察到 |
| --- | --- |
| 有效事实窗口 | v2 schema 通过，来源闭包完整 |
| 有效空窗口 | schema 通过，sections/sourceFactIds 为空 |
| 同一输入重复生成 | JSON 与 fingerprint 字节级一致 |
| 来源闭包错误 | 失败，错误码稳定，不输出正式报告 |
| 窗口错误 | 失败，错误码稳定，不输出正式报告 |
| fingerprint 被篡改 | 校验失败 |
| legacy fallback / 失败 Journal | 不生成 v2 |
| golden fixture | canonical JSON 与大写 SHA-256 完全匹配 |

测试入口固定为 `TestWorldReportV2Build`，位于 `tools/worldbook-runtime-smoke/Program.cs`。验证命令：

```text
dotnet build AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-restore
dotnet run --project AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-build
```

只要求离线 E2 build/smoke；不声称持久化、E3、E4 或 E5。

## 8. 门禁

```text
本计划独立审查 APPROVED
  → 用户签收
  → 实施 v2 生成/校验
  → E2 build/smoke
  → 独立实现审查
```

当前未签收，`implementation_allowed = false`。
