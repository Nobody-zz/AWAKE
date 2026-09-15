# 正式周报 v2：契约与快照接线计划独立静态复审 Round 2

## 审查范围

- 计划：`docs/PLAN-WORLD-REPORT-V2-PERSISTENCE-20260912.md`
- 对照：`src/WeeklyReportService.cs`、`src/WorldEventContracts.cs`、`src/WorldStateStore.cs`、`tools/worldbook-runtime-smoke/Program.cs`、`tools/worldbook-runtime-smoke/SmokeStubs.cs`
- 本轮是对 Round 1 修订内容的定向复审；只读核对计划与当前调用/测试 seam。
- 未编辑源码，未构建，未同步，未启动游戏；本轮证据上限为 E2 计划可实施性。

## 结论

`REVIEW_TARGET: AWAKE-WORLD-REPORT-V2-PERSISTENCE-20260912`

`REVISION: Round 2`

`DECISION: REVISE_CURRENT_SLICE`

上一轮的 v2 顶层字段、来源闭包、空报告结构、canonicalizer 方向、状态 envelope、repair 矩阵和故障场景名称已经补齐，以下只记录仍会阻止机械化实施的根问题。

## P0-1：不确定写入与原子可见性没有落到可执行的 storage proof

证据：

- 计划第 83–85 行要求“目标 storage adapter”证明单次 Set 的 old-or-new 可见性，并要求 `write_unknown_before_replace` / `write_unknown_after_replace` 等故障场景；但计划第 89–94 行的唯一写集只有 AWAKE 三个源码文件和 `Program.cs`，没有指定实际 adapter、接口契约文件或生产 adapter 测试入口。
- 当前 `WorldStateStore.UpsertWeeklyReportStateAsync`（`src/WorldStateStore.cs:992–1051`）仍是读—生成命令—队列 drain—整体写入的 seam；当前的 `WeeklyReportStateStoreFaultFixture` 还不存在。现有 smoke 替身在 `tools/worldbook-runtime-smoke/SmokeStubs.cs:79–112,177–238` 直接操作进程内 `Document`，并沿用 v1 validator，不能证明真实 adapter 的 old-or-new 或“写入已发生后异常”的重读语义。
- 计划第 118 行要求每个场景“重开 fixture store”，但没有定义重开的对象、持久化介质、注入点和重开后读取入口。

影响：

实施者可以写出一个内存 fake 让所有测试通过，却仍无法证明生产 `WorldStateStore` 的 unknown-write 语义；也可能误把 `write_unknown_after_replace` 当成失败并重复写入，或把同一次写入误报为 `already_applied`。

最小修正，二选一并写死：

1. 将本切片的保证收窄为“WorldStateStore 状态机在注入式 old-or-new fake 上通过 E2”；明确不宣称生产 adapter 的原子性/unknown-write 证明，并把生产 adapter 证明列为后续切片；或
2. 把实际生产 adapter 的契约、源码路径、测试项目/入口、故障注入方式和重开读取路径加入写集与验收，明确 `SetAsync` 的 old-or-new 语义由谁保证。

在二选一落定前，P0-1 阻断实施。

## P1-1：v2 生成结果仍不能由计划唯一复现

证据：

- 第 45 行只规定 `reportId` 的模板，未规定 `{windowEndDay}` 的十进制格式；第 48–50 行未规定 section 的固定顺序、事实如何分组为 item、item/section ID 的完整生成公式，也未规定 `generatedBy` 的固定值。
- 第 54 行规定 UTC Round-trip period，但没有明确复用现有 `WeeklyReportService` 的 Unix epoch 映射（当前 `WeeklyReportService.cs:27–29`），也没有锁定字符串的精确输出格式。
- 第 61–64 行说明了 canonicalizer 的原则，但没有规定 canonical JSON 必须是无空白紧凑 JSON，也没有给出 expected canonical JSON 文件名与 expected SHA-256 值；第 33 行列出的 fixture 中没有固定 hash fixture 文件。

影响：

同一 `WeeklyDynamicsInput` 可能得到不同 section/item 排列、时间字符串、`generatedBy` 或 canonical bytes，进而得到不同 fingerprint；这会直接影响重复提交、冲突判断和 fixture 结果。

最小修正：固定 `generatedBy`、section/domain 顺序、每个 item 的分组规则和 ID 公式；明确 day→UTC epoch、`ToString("O")`（或替代格式）和无空白 JSON；增加一个包含完整 canonical JSON 与期望大写 SHA-256 的 fixture，并把该 fixture 的路径写入计划。

## P1-2：状态 envelope 与结果状态的可验证枚举仍未闭合

证据：

- 第 73–81 行列出了 envelope 字段，但没有规定这些字段的 JSON 类型、required 集合、`additionalProperties`、`status` 枚举或 `lastErrorCode` 的稳定枚举/格式。
- 第 65–69 行要求返回 `already_applied`、`conflict`、`applied`、`retryable`，但当前 `WeeklyReportStateWriteResult`（`src/WorldEventContracts.cs:46–55`）只有 `applied`、`already_applied`、`retryable`、`failed`，没有 `conflict`；计划没有说明新增状态如何映射到现有结果类型、持久化 status 是否允许 `conflict`，以及冲突是否写入状态。
- 当前读取/应用路径仍直接使用 v1 validator（`src/WorldStateStore.cs:949–952,986–990,3617–3621,3681–3683`）；计划虽然写了“v1 只读兼容”，但未定义 v1/v2/未知 `schemaVersion` 在同一 `weeklyReports` 集合中的逐条分派和失败结果。

影响：

不同实现可能把冲突写成 `failed`、`retryable` 或 `conflict`，或在遇到未知版本时跳过、整批失败、当作损坏；这些结果都会破坏调用方和验收脚本的稳定判断。

最小修正：为 envelope 单独给出 schema/类型/required/枚举；固定结果状态、错误码和是否持久化冲突；写出 v1、v2、未知版本、非法 envelope 的分派矩阵，要求未知版本 fail-closed 且 v1 条目原样保留。

## P1-3：故障 fixture 的实现文件和重开语义未进入写集

证据：

- 第 85、111 行把 `WeeklyReportStateStoreFaultFixture` 固定在 `Program.cs`，但现有可注入状态行为位于 `SmokeStubs.cs`；第 94 行又只允许修改 `Program.cs`，没有说明替身是否可以内嵌、是否要修改 `SmokeStubs.cs`，以及生产 `WorldStateStore` 如何接到该替身。
- 第 118 行的“每个场景后重开”没有定义 `Document` 如何序列化、由谁重新加载、故障状态是否在重开后清除、以及 `read_after_write_unavailable` 在重开前还是重开后注入。

影响：

验收名称虽然明确，但机械化执行者仍需自行选择测试对象和重开方式，无法保证测试实际覆盖持久化读取而非内存对象。

最小修正：固定一个确切的 fixture 实现文件/类型、注入接口、持久化 backing store 和 `Reopen()` 行为；把需要修改的 `SmokeStubs.cs`（或明确“全部内嵌 Program.cs”）列入写集，并为七种 fault case 写出注入时点和重开后的预期状态。

## 已闭合项

- Round 1 的 v2 顶层字段和嵌套对象范围已明确；`sourceFactIds` 闭包、`entryId` 排除、空报告规则已写入当前计划。
- repair 只允许完整新输入、v1 不被 v2 覆盖、unknown write 必须重读的方向已写入当前计划。
- 当前切片明确排除生命周期、菜单、Native、事件、记忆、对话和 Worldbook 内容，范围没有再次扩张。

## 当前门禁

```text
independent review = REVISE_CURRENT_SLICE
user sign-off = false
implementation_allowed = false
E2/E3/E4/E5 evidence = not produced in this review
```

本轮已经达到当前 revision 的审查上限；不再把上述同一因果链拆成第三轮。下一步只有一个：先对 P0-1 选择“收窄 E2 保证”或“补齐真实 adapter proof”，并同时补齐 P1-1 至 P1-3 的确定性字段，然后以新 revision 重新进入计划审查。

`VERDICT: REVISE`
