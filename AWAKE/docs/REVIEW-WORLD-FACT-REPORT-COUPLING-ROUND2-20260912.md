# 世界事实到正式周报耦合契约：独立静态复审 Round 2

## 审查范围

- 目标：`PLAN-WORLD-FACT-REPORT-COUPLING-20260912.md`
- 对照：`PLAN-WEEKLY-DYNAMICS-20260912.md`、`PLAN-WORLD-REPORT-FOUNDATION-20260912.md`
- 指定源码：`WorldFactQuery.cs`、`WorldEventContracts.cs`、`WeeklyReportService.cs`、`WorldKnowledgeProjectionService.cs`
- 本轮仅做静态核对；未编辑源码、未构建、未同步、未启动游戏。

## 结论

`VERDICT: REVISE`

目前不能签收，也不能把这份耦合契约标为 `APPROVED`。原因不是事实 Journal 或查询层没有价值，而是“正式周报的唯一输入、v2 来源字段、验证器和实际调用链”尚未形成同一套可执行契约。

## 未解决问题

### P0-1：v2 来源字段与现有报告契约互相冲突

耦合契约第 39–50 行要求新增必选 `sourceFactIds`，并允许没有真实旧事件 ID 时将 `sourceEventIds` 置空。但：

- `PLAN-WEEKLY-DYNAMICS-20260912.md` 第 25–27 行仍声明 v2 顶层字段只有 `sourceEventIds`，没有 `sourceFactIds`；
- `PLAN-WORLD-REPORT-FOUNDATION-20260912.md` 第 275–284 行把 v2 顶层字段和指纹输入固定为仅包含 `sourceEventIds`；
- 当前 `WorldEventContract.TryValidateWeeklyReport`（`WorldEventContracts.cs:700–777`）只接受 `awake.worldbook.weekly-report.v1`，并要求每个非空 item 的 `sourceEventIds` 非空且闭包完整。

因此实施者无法同时满足三份文档：是增加 `sourceFactIds`，还是保持“exact top-level fields”；事实只有 `factId` 而没有旧事件 ID 时，item 如何通过现有来源闭包校验，也没有确定答案。

最小修正：指定一份唯一权威的 v2 契约；同步修订另一份周报计划、schema、C# 版本分派、item 来源闭包和指纹 fixture。若保留双来源字段，必须明确 item 级字段、空 `sourceEventIds` 的合法条件以及 v1 投影规则。

### P0-2：正式周报的计划入口与当前实现仍不一致

耦合契约第 25–33 行要求正式周报只能消费 `Policy == WeeklyDynamics`、`Success/Empty` 且不得使用 legacy fallback；但当前 `WorldEventContracts.EnsureFormalReportsReadyAsync`（`WorldEventContracts.cs:310–368`）仍然：

- 调用 `Recorder.LoadAsync`；
- 读取 `WorldEventLedger.CaptureSnapshot()`；
- 通过 `Reports.BuildWindow(IReadOnlyList<WorldEventRecord>, ...)` 生成报告；
- 走当前 v1 validator。

虽然 `QueryWeeklyDynamicsAsync` 已存在（`WorldEventContracts.cs:218–225`），但正式报告调用链没有调用它。于是 Journal 中的结构化事实可能不会进入正式报告，且 legacy 记录仍可能成为正式报告来源。

最小修正：为正式报告定义唯一的 typed 输入 seam（至少包含策略、状态、七日边界、事实集合、来源 ID 和 legacy 标记），由 `EnsureFormalReportsReadyAsync` 调用该 seam；旧 `BuildWindow` 只能保留给 v1 兼容读取/离线适配，不得继续作为正式 v2 生成入口。接线后再做独立复审。

## P1 问题

### P1-1：`sourceEventIds` 允许为空，但现有校验规则不允许事实闭包只有 factId

耦合契约第 47–49 行允许没有真实旧事件 ID 时 `sourceEventIds` 为空；然而当前 validator 第 761–768 行要求每个报告 item 必须有非空 `sourceEventIds`，并且必须出现在报告顶层集合中。该规则会拒绝“只由结构化 WorldFact 生成、没有 legacy event ID”的合法报告。

最小修正：在 v2 schema 中明确使用 `sourceFactIds` 作为 item 和顶层闭包，`sourceEventIds` 仅作为可选兼容索引；或者继续要求每个事实都带可验证的旧事件映射。两种方案只能选一种，并补充有/无旧事件映射的正反 fixture。

### P1-2：查询结果没有携带正式报告所需的窗口和来源快照元数据

`WorldFactQueryResult`（`WorldFactQuery.cs:62–92`）只有 `Status`、`Policy`、`Facts`、`SourceFactIds`、`ErrorCode` 和 fallback 标记，没有明确的 `WindowStartDay`、`WindowEndDay`、Journal revision 或读取快照身份。正式报告目前只能从外部 `currentDay` 重新推导窗口。

这会让“查询到的事实集合”和“报告写入的 period”出现错配，也无法把重试时使用的事实版本绑定到指纹/报告状态。

最小修正：让查询结果携带已解析的窗口边界、来源 revision/digest 和结果是否完整；报告生成器只接受该结果，不接受另一个独立的日期或事实集合。

### P1-3：正式报告禁止 fallback，但没有机器可检查的强制边界

耦合契约第 28–33 行以约定方式要求 `UsedLegacyFallback == false`，但没有定义正式报告输入类型、统一 guard 或 verifier 规则。当前 `WorldFactQueryResult` 是普通可构造对象，调用方理论上可以传入任意 `Success + false` 组合；正式报告仍直接走旧 ledger 路径。

最小修正：增加 `FormalReportInput`/等价不可绕过的工厂或集中 guard；验证器必须检查 policy、status、fallback、窗口和来源闭包，并对违规输入返回稳定错误码。

### P1-4：报告持久化和版本化 ID 没有在耦合契约中闭合

耦合契约第 54–80 行描述了“幂等复用、冲突、保留旧快照”，但没有固定：

- v2 reportId 的精确格式与 v1 同窗口共存规则；
- 报告状态键/字段和 fingerprint 的持久化位置；
- 写入不确定后的确认范围；
- 晚到事实、重复生成和已存在损坏快照的处理矩阵。

现有 `EnsureFormalReportsReadyAsync` 仍使用旧 v1 reportId 和 `UpsertWeeklyReportStateAsync`，不能作为 v2 契约证明。

最小修正：引用唯一的已验证报告状态契约，锁定 v1/v2 ID、canonical JSON、fingerprint、幂等/冲突/repair 结果和持久化读写入口；如果复用既有报告存储计划，必须明确该计划的当前状态和依赖，不得只写文字引用。

### P1-5：整体状态仍存在“计划 blocked、代码已 implementing”的放行歧义

`PLAN-WEEKLY-DYNAMICS-20260912.md` 顶部仍是 `blocked_by_world_window_storage`，而事实基础 review state 已记录查询实现的 E2 build/smoke；耦合契约自身则是 `proposed_pending_user_signoff`。这些状态分别描述不同切片，但没有一份 machine-readable handoff 明确说明：事实查询已通过什么范围、正式周报仍被什么门禁阻止、何时允许进入 v2 实现。

最小修正：为本耦合切片建立独立 review state，列出依赖、允许写集、当前 verdict 和 signoff 状态；在 `REVISE` 时禁止 v2 正式报告接线，不能用事实查询层的 build/smoke 结果替代周报审查。

## 已确认可复用的部分

- `WorldFactJournal` 与 `WorldFactQuery` 的单向职责划分是合理的；查询层不写 Journal。
- 近期预览已走 `QueryRecentDynamicsAsync`，与正式报告路径应当分开。
- `WorldKnowledgeProjectionService.TryReplaceFacts` 已检查 `WorldKnowledge` policy 和 Success/Empty 状态；这部分不构成正式报告通过证明。
- 本轮没有发现新的事实 Journal 存储 P0；问题集中在报告契约冲突和正式调用链未接线。

## 下一步门禁

1. 先统一 v2 来源字段、schema、validator 和 fingerprint fixture。
2. 再把正式报告调用链改为唯一消费 `QueryWeeklyDynamicsAsync` 的 typed 输入。
3. 补齐报告状态/版本化 ID/幂等冲突和失败矩阵。
4. 独立静态复审达到 `APPROVED` 后，才向用户请求签收；在此之前用户无需签收，也不应开始 v2 正式报告实现。

