# AWAKE 世界上下文桥接接口批次

状态：`revised_after_review_round_2_needs_final_review`

风险级别：`high-risk`（跨事实查询、世界知识、事件、对话和记忆调用边界）

本计划只定义并实现模组本体的“事实/知识 → 可消费上下文”边界。世界书和角色卡继续作为可变化的外部输入，不在本批冻结、改写或发布。

## 1. 目标与完成边界

让事件系统、人物对话和未来记忆系统能够从同一份带来源的上下文结果取数：

```text
现有 Fact/Knowledge 查询
        → 薄桥接适配器
        → 分来源结果 + 总体结果
        → Event / Dialogue / Memory consumer
```

完成标准是：Event、Dialogue、Memory 三个真实调用点均能通过桥接入口取得可观察结果，并由 focused/integration smoke 验证成功、空、部分可用、失败、取消和预算截断。只注册一个类、只让测试 fake 调用桥接器，不算完成。

## 2. 设计决策：薄适配器，不造第二套查询系统

本批选择“薄适配器”方案：

- `WorldFactQuery` 继续负责事实过滤、窗口和事实选择理由；
- `IWorldKnowledgeQuery` / `WorldKnowledgeQueryService` 继续负责世界书匹配和可见性；
- 桥接器只负责请求归一化、调用顺序、分来源状态、来源闭包汇总、最终预算裁剪和消费者输出；
- 不复制 `WorldFactQueryRequest`、`WorldKnowledgeQueryResult` 或 Persona `ContextSnapshot` 的全部字段；
- 不把 Persona 或 Worldbook 设为本体强制依赖。

字段责任映射固定为：

| 内容 | 权威模型/责任方 | 桥接器行为 |
|---|---|---|
| 事实窗口、过滤、选择理由 | `WorldFactQueryRequest/Result` | 转换为事实 outcome，不重算 |
| 世界书匹配、身份门控 | `WorldbookQuery`、`IWorldKnowledgeQuery` | 提供归一化 visibility，接收结果 |
| Persona 叙述和 tags | Persona definition / `ContextSnapshot` | 不复制、不持有 |
| 事实/知识跨源状态 | 本批 `WorldContextSnapshot` | 仅在此处汇总 |
| Event/Dialogue/Memory 消费 | 各自现有 caller | 只通过桥接入口取候选 |

拒绝以下方案：

1. 新建包含所有事实、知识、人格、记忆字段的万能上下文模型：会形成第二套权威模型。
2. 只新增接口不接真实 caller：无法证明玩家对话、事件和记忆路径真的消费了它。

## 3. `WorldContextRequest`

只保留桥接层确实需要的字段：

- `Consumer`：`Dialogue`、`Event`、`MemoryCandidate`；
- `CurrentDay`、可选 `WindowStartDay` / `WindowEndDay`；
- 稳定人物、王国、家族、聚落、队伍和阵营 ID；
- 已归一化的 `VisibilityContext`；
- `SceneKeywords`、`ContextModes` 和可选主题关键词；
- `MaximumFactItems`、`MaximumKnowledgeItems`、`MaximumUtf8Bytes`；
- 现有 `RequestContext` 的 correlation/deadline 信息；
- caller cancellation token。

请求不包含 TaleWorlds 实时对象、Persona JSON、原始世界书文档、Provider 请求正文或存档写入句柄。

### VisibilityContext 责任

唯一生成责任归 caller 侧的 `BannerlordWorldbookIdentityAdapter`：它从当前目标对象产生归一化值，桥接器只接收值对象，不接收实时对象。

`VisibilityContext` 必须带 presence/availability 标记，并承载现有门控所需的：身份 ID、角色、文化、王国、聚落、性别、年龄、管理等级、技能、内容层级、scope/detail、`IsClanLeader`。

- Dialogue caller：目标对象无法生成完整 visibility 时，知识分支为 `Unavailable`，但事实分支仍可继续。
- Event caller：若没有人物身份，不请求知识门控，只请求 Event candidate。
- Memory caller：只请求事实候选；不得因为缺少知识身份而默认放行。

## 4. `WorldContextSnapshot` 与状态契约

snapshot 包含：

- `FactsOutcome`：状态、事实 items、真实 `JournalRevision`、fact IDs、错误码；
- `KnowledgeOutcome`：状态、知识 items、package/version/revision/fingerprint、每个 item 的完整 source closure、错误码；
- `AggregateStatus`：`Success`、`Empty`、`Partial`、`Unavailable`、`Corrupt`；
- `BudgetLimited`、被裁掉的 item IDs 和裁剪理由；
- consumer、当前日、窗口、correlation ID。

分来源状态使用 `Success`、`Empty`、`Unavailable`、`Corrupt`。总体状态按以下算法确定，覆盖全部组合：

1. 两个来源都是 `Success` 或 `Empty`：至少一个 `Success` 时为 `Success`，否则为 `Empty`。
2. 只有一个来源是 `Success` 或 `Empty`：为 `Partial`，保留可用来源。
3. 两个来源都不可安全消费：任一来源为 `Corrupt` 时为 `Corrupt`，否则为 `Unavailable`。

因此，`Facts=empty + Knowledge=success` 明确为 `Success`；`Facts=success + Knowledge=unavailable` 明确为 `Partial`；两边均失败时 `Corrupt` 优先于 `Unavailable`。

## 5. revision 与来源闭包

- `WorldFactQueryResult` 必须传出 Journal 的真实 revision；没有 revision 时 facts outcome 为 `Unavailable`，不得伪造 0。
- `WorldKnowledgeQueryResult` 必须传出 package ID、package version、package revision、overlay revision 和静态 fingerprint。
- 静态 fingerprint 使用 validated `WorldKnowledgeSnapshot` 的 canonical JSON：固定属性顺序，排除运行时 `KeywordIndex` 和 `Warnings`，列表按稳定 ID 排序，UTF-8 SHA-256。
- 每个 knowledge hit 必须传出命中的 `entryId`、`expressionId`，以及该表达对应的 `SourceFactIds`、`SourceEventIds`、`SourceReportIds`；缺失闭包时该 hit 不进入 snapshot。
- snapshot 只汇总 source stamp 和闭包，不复制原始查询模型的全部字段。

## 6. 排序、预算与选择理由

- 底层事实查询负责事实选择与事实顺序；桥接器不重新解释事实优先级。
- 知识查询先返回完整候选及其稳定 ID；最终字节预算只由桥接器负责，避免底层先静默截断。
- 跨源合并排序固定为 `sourceKind ordinal → sourceId ordinal`；知识内部为 `entryId ordinal → expressionId ordinal`。
- 事实 item 的计量文本为其 canonical JSON；知识 item 的计量文本为其 canonical expression JSON；item 之间以单个 `\n` 计入总 UTF-8 预算。
- 先应用每类 item 数量上限，再按上述顺序应用总字节预算。
- 超预算设置 `BudgetLimited=true`，记录所有 dropped IDs 和 `context.budget_limited`；不得静默丢失。
- 每个 item 带稳定 `SelectionReason`，例如 `fact.policy.selected`、`knowledge.visibility.granted`、`context.budget_limited`。

## 7. 取消、deadline 与后台权限

- 复用现有 `RequestContext` 的 correlation/deadline，不新增平行 deadline 模型。
- 所有异步事实查询共享 caller 的 linked token。
- 同步知识查询前后检查取消和 deadline；若底层无法中断，则在调用前拒绝过期请求，在返回后拒绝已过期结果。
- 预取消返回 `awake.cancelled`；deadline 过期使用项目既有稳定 deadline 错误码。
- 取消和 deadline 不能写入 Journal、报告、Persona Storage 或记忆。
- 后台 caller 只能使用已安装服务和 `PermissionGate.Evaluate` 结果；桥接器不得调用 `EnsureAsync`、`RequestAsync` 或 UI dispatcher。

## 8. 唯一真实接入点

为避免双路径，固定三个入口：

1. Event：只在 `AwakeEventEngine` 的规则候选生成前调用一次；`AwakeEventBehavior` 不直接调用桥接器。桥接器只返回 `EventTriggerCandidate`，事件引擎继续负责规则选择、效果和 Command。
2. Dialogue：`NpcDialogueService` 在 Persona 投影和 Prompt 组装前调用一次；替换当前 direct `WorldbookRuntime.Knowledge.Query` 作为事实/知识上下文来源。事实成功、知识不可用时，事实仍可进入 prompt，知识状态进入结构化日志。
3. Memory：在 `NpcMemoryService` 的日常整理入口调用一次 candidate-only seam；本批不让桥接器调用 Persona Storage，也不替换现有记忆持久化流程。候选结果交给现有整理逻辑时，必须由测试证明 Get/Set 次数为 0。

## 9. 允许修改范围

- 新增内部世界上下文契约与薄装配器；
- 必要时扩展事实/知识结果的 revision、per-item source closure 和预算元数据；
- 在 `WorldEventContracts` 暴露唯一本体调用 seam；
- 修改上述三个真实 caller 的最小调用点；
- 为状态矩阵、来源闭包、预算、取消和后台权限增加 focused/integration fixture；
- 增加结构化日志：consumer、aggregate status、各来源状态、item 数、dropped 数、revision/fingerprint、correlation ID。

## 10. 明确不做

- 不修改世界书正文、Studio 产物或角色卡；
- 不实现随机事件；
- 不执行事件效果、外交结算或 Command；
- 不把所有历史事实自动注入对话；
- 不实现记忆自动写入、删除或存档迁移；
- 不修改 Marcus 公共 API、存储适配器或权限契约；
- 不改菜单布局，不同步游戏目录，不启动游戏。

## 11. 验收与测试入口

新增并在 `AWAKE/tools/worldbook-runtime-smoke/Program.cs` 主入口注册：

- `RunWorldContextBridgeStateMatrix`：16 种 facts/knowledge 状态组合；
- `RunWorldContextBridgeProvenance`：Journal revision、package fingerprint、expression/source closure；
- `RunWorldContextBridgeBudget`：UTF-8 计量、稳定排序、dropped IDs；
- `RunWorldContextBridgeCancellation`：预取消、过期 deadline、无写入；

在 `AWAKE/tools/worldbook-runtime-production-smoke/` 增加真实 caller fixture：

- Event：断言 `AwakeEventEngine` 单次调用桥接，返回 candidate，不执行 Command；
- Dialogue：断言 `NpcDialogueService` 不再直接查询知识，事实可用/知识不可用时仍形成部分上下文；
- Memory：断言 `NpcMemoryService` 取得 candidate 且 Persona Storage Get/Set 为 0。

验收场景：

| 场景 | 必须观察到 | 证据 |
|---|---|---|
| 事实和知识均成功 | 两个分支状态、revision、fingerprint 和闭包可读 | focused smoke |
| facts empty + knowledge success | 总体为 `Success` | state matrix |
| 事实成功、知识不可用 | 总体为 `Partial`，事实保留 | integration fixture |
| 两边均失败 | `Corrupt` 优先于 `Unavailable` | state matrix |
| Journal 损坏/不可用 | 不转为空，不生成虚构上下文 | focused smoke |
| 知识门控字段缺失 | 知识 fail-closed | contract test |
| 预算不足 | 稳定 dropped IDs、理由和 UTF-8 字节结果 | budget fixture |
| Event 真实入口 | 只得到候选，不执行 Command | production smoke |
| Dialogue 真实入口 | 通过桥接取得选择性事实/知识上下文 | production smoke |
| Memory 真实入口 | 只得到候选，Persona Storage Get/Set 为 0 | production smoke |
| 预取消/过期 deadline | 返回稳定错误，不写状态 | focused test |
| 无世界书/无角色卡 | 本体仍安全返回事实或 empty/partial | regression smoke |

## 12. 门禁与审查记录

```text
本修订计划独立只读审查 APPROVED
  → 用户签收
  → 实现契约、薄装配器和真实 caller
  → focused/integration smoke
  → Release build 与独立实现复审
```

最低证据为 E2。没有当前 BuildId 的游戏日志，不声明 E4；没有存档重启证据，不声明 E5。

### Round 1

- 结论：`VERDICT: REVISE`
- P0：0；P1：8；P2：1
- 修订：分来源状态、revision/source closure、visibility context、预算和排序、Dialogue/Event/Memory 真实 caller、取消/deadline/backend 权限、薄适配器边界。

### Round 2

- 结论：`VERDICT: REVISE`
- P0：0；P1：9；P2：1
- 修订：完整状态算法、底层 revision/closure 产出、唯一 visibility 生成责任、单层预算裁剪、三个唯一真实入口、同步知识查询的 deadline 语义、具体测试入口和字段责任映射。
- 审查任务：独立只读任务 Darwin，未编辑、未构建、未同步、未启动游戏。

