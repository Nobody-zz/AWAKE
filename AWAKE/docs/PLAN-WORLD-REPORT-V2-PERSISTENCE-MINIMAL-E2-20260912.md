# 正式周报 v2：最小快照持久化 E2 切片

状态：`implemented_approved_e2`

这是 v2 生成与校验 E2 通过后的下一步计划。它只把已经生成并验证的 v2 报告作为一个完整快照写入现有 `WorldStateStore` 报告状态集合，并支持同 ID 的幂等读取和冲突拒绝。

## 1. 本切片目标

```text
validated v2 report
  → existing WorldStateStore weeklyReports entry
  → reopen/read
  → same fingerprint = already_applied
  → different fingerprint = conflict
```

只支持同一进程、同一 campaign、单写者。完成后可以证明“v2 快照能保存并重新读回”，不代表生命周期、菜单或 Native 已接入。

## 2. 明确不做与保证边界

不做：

- storage adapter、Framework API、CAS、事务或多进程协调；
- atomic old-or-new 的生产 adapter 证明；
- unknown-write 的自动恢复或已提交判定；
- `ProbeExtension.cs`、`AwakeRuntime.cs`、菜单、Native、事件、记忆、对话和 Worldbook 内容。

本切片对写入结果的保证仅为：

- 业务队列明确报告成功：返回 `applied`；
- 已有相同 v2 快照：返回 `already_applied`，不重复写入；
- 同 reportId 但 fingerprint 不同：返回 `conflict`，保留旧快照；
- 读取、入队、执行或提交结果不确定：返回 `retryable`，不声称已保存。

不把 adapter 返回的未知结果解释成成功，也不在本切片内补偿未知写入。

## 3. 持久化条目契约

仍使用现有 `weeklyReports` 集合；v2 条目严格为：

```text
reportId, schemaVersion, windowStartDay, windowEndDay, status,
attemptCount, lastAttemptDay, lastErrorCode, contentFingerprint,
sourceFactIds, report
```

类型和规则：

- `reportId`、`schemaVersion`、`status`、`lastErrorCode`、`contentFingerprint` 为字符串；day/attempt 字段为整数；`sourceFactIds` 为字符串数组；`report` 为完整 v2 JSON 对象；
- `status` 仅允许 `applied` 或 `retryable`；`conflict` 只作为调用结果，不写入状态；
- `report` 必须通过 `TryValidateV2Report`；entry 的 `reportId`、schemaVersion、窗口、fingerprint、sourceFactIds 必须与 payload 完全一致；
- v1 条目原样保留，v2 不覆盖 v1，也不向 v1 补字段；
- 缺失 `weeklyReports` 仍按现有世界事件状态的空集合处理；已有但非法的 v2 条目按损坏快照处理，不伪装成合法空集合。

## 4. 读写语义

- 读状态时按 `reportId` 查找并分派 v1/v2；未知 schemaVersion 或非法 v2 envelope 返回 `corrupt`/`retryable`，不得返回合法 v2；
- 新报告不存在时，写入一条 `applied` v2 entry；
- 已有 v2 entry 且完整 payload/fingerprint 相同，返回 `already_applied`；
- 已有 v2 entry 但 fingerprint、窗口或 sourceFactIds 任一不同，返回 `conflict`，原 entry 不变；
- 已有 v2 entry 损坏时，只有新报告本身完整且 reportId/窗口/sourceFactIds 合法，才允许用新完整 entry 修复；v1 entry 不进入该修复分支；
- `WorldStateStore` 继续使用现有命令队列和单 key 状态写入；本切片不把该队列扩大解释为跨进程事务。

## 5. 写集

允许修改：

- `src/WorldStateStore.cs`：增加 v1/v2 报告分派、v2 entry 读取、幂等/冲突/有限 repair；
- `src/WorldEventContracts.cs`：补充 v2 持久化结果类型和调用 seam；
- `tools/worldbook-runtime-smoke/SmokeStubs.cs`：提供可重开的 v2 fixture store；
- `tools/worldbook-runtime-smoke/Program.cs`：增加 `TestWorldReportV2PersistenceMinimal`。

不修改 storage adapter、Framework、生命周期和 UI。

## 6. E2 验收

| 场景 | 必须观察到 |
| --- | --- |
| 新 v2 快照 | 写入成功，重开后完整读回 |
| 相同输入重复 | `already_applied`，写入次数不增加 |
| 同 ID 不同 fingerprint | `conflict`，旧快照不变 |
| 损坏 v2 entry | 完整新报告可 repair，v1 不被覆盖 |
| 非法 v2 payload | 拒绝写入 |
| 读取/执行失败 | `retryable`，不返回已保存 |
| v1 fixture | 原字段和读取行为不变 |

测试必须使用 `TestWorldReportV2PersistenceMinimal` 和可重开的 `WeeklyReportStateStoreFixture`；每个场景后重新加载 fixture backing JSON，不能只比较当前 JObject。

验证命令：

```text
dotnet build AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-restore
dotnet run --project AWAKE/tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --no-build
```

只声称离线 E2；不声称生产 adapter 原子性、E3、E4 或 E5。

## 7. 门禁

```text
本计划独立审查 APPROVED
  → 用户签收
  → 实施最小 v2 快照持久化
  → E2 build/smoke
  → 独立实现审查
```

当前未签收，`implementation_allowed = false`。
