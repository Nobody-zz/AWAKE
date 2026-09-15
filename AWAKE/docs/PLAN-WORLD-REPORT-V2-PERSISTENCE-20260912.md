# 正式周报 v2：契约与快照接线切片

状态：`deferred_by_scope_reduction`

> 本计划因存储适配器证明和生产 unknown-write 语义尚未就绪而暂缓，不再是当前实施计划。当前先执行 `PLAN-WORLD-REPORT-V2-BUILD-E2-20260912.md`，只完成 v2 报告生成、schema 校验和 fingerprint；持久化另行重写。

这是最小 E2 事实→周报切片之后的下一步唯一权威计划。本切片只负责把已验证的 `WeeklyDynamicsInput` 变成正式 v2 报告并通过现有 `WorldStateStore` 报告状态 seam 读写；不接生命周期、菜单、Native 或游戏验证。

## 1. 目标

```text
WeeklyDynamicsInput
  → v2 formal report JSON
  → schema/fingerprint 校验
  → 读取同 reportId 的快照
  → 幂等复用 / 安全写入 / 冲突拒绝
```

本切片完成后，代码可以在离线或后台服务调用点生成、校验和保存正式 v2 快照；不代表菜单已经显示，也不代表 CampaignSessionReady 已接入。

## 2. 输入边界

唯一输入是上一切片已通过的 `WeeklyDynamicsInput`：

- policy 必须是 `WeeklyDynamics`；
- status 只能是 `Success` 或 `Empty`；
- 不允许 legacy fallback；
- 窗口必须是 `endDay >= 7` 且 `endDay % 7 == 0` 的完整七日窗口；
- facts 与 sourceFactIds 必须一一对应，且每条 fact 只允许进入一个 item。

不得从 `WorldEventLedger`、`WorldEventRecord` 或独立 `currentDay` 重新取数。

## 3. v2 报告契约

契约文件固定为 `tools/worldbook-contract/v2/weekly-report.schema.json`；fixture 固定为 `tools/worldbook-contract/v2/fixtures/valid-report.json`、`valid-empty-report.json`、`invalid-source-closure.json`、`invalid-window.json` 和 `invalid-fingerprint.json`。

v2 顶层字段固定为以下 10 个，全部必需：

```text
schemaVersion, reportId, period, generatedBy, policyVersion,
contentFingerprint, sourceFactIds, sections, visibility, extensions
```

规则：

- `schemaVersion = awake.worldbook.weekly-report.v2`；
- `reportId = awake:report:weekly-v2-{windowEndDay}`；
- `policyVersion = awake.weekly-report.policy.v2`；
- `sourceFactIds` 是唯一权威来源闭包，必须是 64 位小写十六进制 factId，去重后按 ordinal 升序排列；
- `sections` 是数组；每个 section 固定包含 `sectionId`、`domain`、`title`、`items`，domain 只能是 `politics`、`war`、`people`、`local`；
- section 的 `title` 和 item 的 `text` 都是必需的 `{ "zh-CN": 非空字符串 }` 对象；
- item 固定包含 `itemId`、`text`、`sourceFactIds`，不包含 `entryId`；item 的 `sourceFactIds` 必须非空、去重、按 ordinal 升序，且是顶层集合的子集；每条输入 fact 必须且只能被一个 item 引用；
- `sourceEventIds` 不属于 v2，不伪造旧事件 ID；v1 继续只读兼容；
- `visibility` 固定为 `{ "scope": "local", "min_detail": "summary", "identity_ids": [] }`；
- `extensions` 只允许整数 `awake:windowStartDay` 和 `awake:windowEndDay`；
- `period` 固定为 UTC Round-trip 字符串，其中 start 是窗口起始日 00:00:00Z，end 是窗口结束日次日 00:00:00Z；
- 空报告的 `sections` 和 `sourceFactIds` 都必须为空数组，不生成填充 item。

顶层和嵌套对象不得出现未声明字段；所有数组元素类型、唯一性、required 字段和 `additionalProperties=false` 都写入上述 schema。

## 4. 指纹与状态语义

- `contentFingerprint` 是排除自身后的 canonical UTF-8 JSON 的大写 SHA-256；
- canonicalizer 递归按对象属性名 ordinal 排序；数组不重排，来源数组在生成阶段按 ordinal 排序；数字使用 invariant culture 的 JSON 数字表示；字符串使用 JSON 标准转义；不允许 null；
- canonical 输入包含 v2 除 `contentFingerprint` 外的全部字段，包含 `visibility`、`extensions`、section/item 文本和来源闭包；
- schema、canonicalizer、正例、反例和固定 hash fixture 必须共用同一字段定义；
- 同 `reportId`、同窗口、同 `sourceFactIds`、同 fingerprint：`already_applied`；
- 同 `reportId` 但任一上述内容不同：`conflict`，保留原快照；
- 原快照缺失：写入 `applied`；
- 原快照存在但 schema/fingerprint 校验失败：仅当新输入窗口和来源闭包仍完整时允许 repair；
- 写入结果不确定：重读同 `reportId`，只有完整匹配才确认 applied，否则返回 retryable。

## 5. 状态 envelope 和存储边界

报告状态沿用现有 `weeklyReports` 集合，但 v2 条目字段固定为：

```text
reportId, schemaVersion, windowStartDay, windowEndDay, status,
attemptCount, lastAttemptDay, lastErrorCode, contentFingerprint,
sourceFactIds, report
```

`report` 必须是完整 v2 payload；`contentFingerprint` 和 `sourceFactIds` 必须与 payload 重算一致。v1 状态不补字段、不改名、不被 v2 覆盖。

本批只支持同一 campaign 内的单写者。报告状态写入必须经过现有命令队列；不声称支持多进程/外部 writer。目标 storage adapter 必须证明单次 Set 对 reader 呈现 old-or-new，不接受半写值。若写入结果未知，统一返回 `retryable`，随后重读同 reportId；只有 envelope 与 payload 的 reportId、schema、窗口、sourceFactIds、fingerprint 全部一致才返回 `already_applied`。

故障注入 fixture 固定为 `WeeklyReportStateStoreFaultFixture`，覆盖 `read_missing`、`read_corrupt`、`write_success`、`write_rejected`、`write_unknown_before_replace`、`write_unknown_after_replace`、`read_after_write_unavailable`。

## 6. 实现写集

允许修改：

- `src/WeeklyReportService.cs`：从 `WeeklyDynamicsInput` 生成 v2 JSON、canonical JSON 和 fingerprint；
- `src/WorldEventContracts.cs`：增加正式 v2 构建/状态编排入口，复用已有报告状态 seam；
- `src/WorldStateStore.cs`：补齐 v2 schema 分派、状态指纹比较和 conflict/repair 结果；
- `tools/worldbook-runtime-smoke/Program.cs`：增加 v2 schema/fingerprint/idempotency/conflict/repair fixture。

本切片不修改 `ProbeExtension.cs`、`AwakeRuntime.cs`、`AwakeEventBehavior.cs`、`AwakeTerminalBehavior.cs`、`WeeklyReportBrowserVM.cs`、语言文件、Native Knowledge、事件触发、人物记忆、对话或 Worldbook 内容。

## 7. E2 验收

| 场景 | 必须观察到 |
| --- | --- |
| 有效事实窗口 | 生成可验证 v2 报告，来源闭包完整 |
| 有效空窗口 | 生成合法空 v2 报告，无 item、无虚构内容 |
| 同输入重复 | 同 fingerprint，返回 already_applied，不重复追加 |
| 同 ID 异内容 | 返回 conflict，原快照保持不变 |
| 损坏快照 | 仅在新来源完整时 repair，事实不被删除 |
| 写入不确定 | 重读确认；无法完全匹配则 retryable |
| legacy fallback / 失败 Journal | 拒绝生成正式 v2 |
| v1 fixture | 原 v1 读取行为不变 |

测试入口固定为 `TestWorldReportV2Persistence`，故障替身固定为 `WeeklyReportStateStoreFaultFixture`，均位于 `tools/worldbook-runtime-smoke/Program.cs`；fixture 目录固定为 `tools/worldbook-contract/v2/fixtures/`。验证命令为：

```text
dotnet build AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-restore
dotnet run --project AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-build
```

测试必须在每个场景后重开 fixture store，验证状态 envelope 和 payload 的持久化读取结果，而不是只比较当前进程内对象。验证只要求离线 build/smoke；不启动游戏、不同步游戏目录、不声称 E3/E4/E5。

## 8. 门禁

```text
本计划独立审查 APPROVED
  → 用户签收
  → 实施 v2 契约与快照接线
  → E2 build/smoke
  → 独立实现审查
```

当前未签收，`implementation_allowed = false`。
