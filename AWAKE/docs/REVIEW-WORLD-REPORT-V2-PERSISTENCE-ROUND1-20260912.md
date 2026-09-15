# 正式周报 v2：契约与快照接线计划独立静态复审

## 审查范围

- 计划：`PLAN-WORLD-REPORT-V2-PERSISTENCE-20260912.md`
- 对照：`WeeklyReportService.cs`、`WorldEventContracts.cs`、`WorldStateStore.cs`
- 本轮仅审查计划是否可实施、可验收；未编辑源码、未构建、未同步、未启动游戏。

## 结论

`VERDICT: REVISE`

当前没有用户签收，也不允许实施。计划方向是合理的，但还不能作为机械化实施说明，因为 v2 的可验证 schema 和报告状态写入语义仍依赖实施者自行补全。

## P0-1：v2 schema 与现有 validator/存储入口没有形成可执行分派

计划第 31–50 行给出了 v2 顶层字段，但没有给出 v2 schema 文件路径、完整 nested schema、版本分派入口和 fixture 内容。当前 `WorldEventContract.TryValidateWeeklyReport`（`WorldEventContracts.cs:735–777`）只接受 v1；`WorldStateStore` 的读取/写入路径（`WorldStateStore.cs:949–952,987–990,3617–3621,3681–3683`）也直接调用同一 v1 validator。

因此只按计划新增 v2 JSON 会在现有存储入口被拒绝；如果实施者放宽现有 validator，又可能改变 v1 行为。

最小修正：固定 `tools/worldbook-contract/v2/weekly-report.schema.json`、v2 正反 fixture、`TryValidateWeeklyReport` 的 schemaVersion dispatch 和存储入口的 v1/v2 验证规则。v1 必须保持原行为，v2 必须有独立 validator，未知版本 fail-closed。

## P0-2：幂等/冲突承诺超出了当前存储 API 可证明的范围

计划第 52–60 行要求同 ID 幂等、异内容冲突、写入不确定后重读确认；但当前 `WorldStateStore` 的报告写入仍是读—修改—整体写入，现有 API 没有报告级 CAS/版本条件，也没有本计划指定的故障注入 seam。仅靠单进程队列不能证明读者不会看到半成状态，也不能区分替换已发生后的 unknown write。

最小修正：在本切片中明确单写者边界和单 key Set 的 old-or-new 语义；若宿主不提供 CAS，则把“写入不确定”收窄为 `retryable`，并定义重读确认的完整比较字段、故障注入点和失败矩阵。E2 必须有真实的 fake store/adapter fixture，而不能只比较内存 JObject。

## P1-1：v2 item、section、visibility 的 required/类型规则不完整

计划第 33–50 行没有锁定：section 的字段、item 的文本结构、itemId/sectionId 格式、`visibility` 的字段和默认值、空数组是否合法、`entryId` 是否可选。第 48 行仅写“采用现有报告可读的安全默认值”，无法生成唯一 schema 或固定 fixture。

最小修正：列出 v2 顶层、section、item、text、visibility、extensions 的完整字段与 required 集合；固定空报告结构和安全默认 visibility 的具体 JSON。

## P1-2：fingerprint canonical JSON 仍不能复现

计划第 54–55 行只说明属性顺序、sections/items 顺序和来源集合排序，没有锁定 canonical JSON 的具体字段顺序、数字格式、字符串转义、空数组/null 处理和是否包含 `visibility` 默认值。不同 JSON 库可能得到不同 hash。

最小修正：固定 canonicalizer 规则和一份完整 expected canonical JSON + uppercase SHA-256 fixture；schema、生成器、状态比较必须复用同一实现。

## P1-3：状态记录字段与同 ID 比较字段没有映射

计划第 56–60 行要求比较“窗口、sourceFactIds、fingerprint”，但没有说明这些值存在哪个 `WeeklyReportApplicationState` 字段、是否从 payload 重算、状态 envelope 是否纳入 fingerprint，以及 `status=retryable/conflict/already_applied` 的精确转换。当前状态类型（`WorldEventContracts.cs:34–44`）没有 fingerprint 或 sourceFactIds 字段。

最小修正：固定状态 envelope schema 和字段映射；明确比较 payload 字段还是 envelope 字段；定义每种读/写结果的稳定错误码和保留原快照规则。

## P1-4：repair 的安全边界和损坏类型未覆盖完整

计划第 59 行允许 repair，但没有区分缺失 payload、v1 payload、v2 schema 错误、fingerprint 错误、窗口不匹配、source closure 不完整等情况。若直接按“校验失败”统一 repair，可能用错误窗口覆盖合法旧快照。

最小修正：固定 repair 矩阵：只有目标 v2 reportId、窗口、来源闭包和新 fingerprint 全部验证通过才可修复；v1 状态不得被 v2 repair；不可修复状态只能返回 corrupt/retryable 并保留原值。

## P1-5：E2 测试入口和 fake storage 尚未命名

计划第 69 行只写“runtime smoke fixture”，没有指定 fixture 文件、fake storage 类型、故障注入方式和命令行入口。当前 smoke 仍主要验证纯对象/已有 v1 路径，不能证明 v2 的真实读写分派和 unknown-write 行为。

最小修正：列出确切测试方法、fake store/adapter、fixture 路径、每个故障点和命令；验收必须覆盖 v1/v2 dispatch、重复写、冲突、损坏 repair、unknown write 和重开读取。

## 当前门禁

```text
plan status = proposed_pending_independent_review
user sign-off = false
implementation allowed = false
verdict = REVISE
```

下一步应先补齐 v2 schema/fixture、状态 envelope 和存储故障语义，再进行第二轮计划复审；暂不修改 `WorldStateStore`。

