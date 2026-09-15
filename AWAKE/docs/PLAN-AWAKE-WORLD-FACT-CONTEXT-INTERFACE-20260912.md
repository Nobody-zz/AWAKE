# AWAKE 世界事实只读接口批次

状态：`approved_e2_complete`

本批是从“大世界上下文桥接”收窄出的本体基础批次。世界书、角色卡、对话、记忆和随机事件继续独立推进，本批不读取、不修改、不冻结它们。

## 1. 目标与唯一完成边界

把现有事实查询能力放到一个可替换、可测试的本体接口后面，并让事件引擎通过唯一 facade 观察事件候选：

```text
WorldFactQuery
    → WorldEventContracts.QueryEventTriggerCandidatesAsync
    → AwakeEventEngine（记录候选，继续既有规则链）
```

本批只做事实只读通路，不建立综合 `WorldContextSnapshot`，不合并 Worldbook、Persona、Dialogue 和 Memory 状态。

完成标准：真实 `AwakeEventEngine` hourly 路径每小时调用一次候选 facade，得到可观察的事实结果；失败和空结果不伪造、不写状态，且既有规则/Command/效果行为保持不变。

## 2. 现有权威模型与边界

- `WorldFactQueryRequest` 是事实查询请求的唯一模型。
- `WorldFactQueryResult` 是事实查询结果的唯一模型。
- `WorldFactSelectionPolicy.EventTriggerCandidate` 表示事件候选查询。
- `WorldEventContracts.QueryFactsAsync` 是当前战役、存储和 generation 边界。
- `WorldFactJournal` 的 revision 是事实结果的唯一 revision 来源。

新增的 `IWorldFactContextReader` 只作为 `WorldEventContracts` 内部的可替换测试 seam。`WorldEventContracts` 只持有一个 reader 字段和一个默认构造路径；测试只能替换该字段，不能另造 reader。生产调用方不得直接实例化它，也不得另造 `ExecuteAsync`/`QueryFactsAsync` 的平行入口。生产唯一入口是 `WorldEventContracts.QueryEventTriggerCandidatesAsync`，Engine 只能调用该入口。

## 3. 接口与结果不变量

```csharp
internal interface IWorldFactContextReader
{
    Task<WorldFactQueryResult> QueryAsync(
        WorldFactQueryRequest request,
        CancellationToken cancellationToken);
}
```

`WorldFactQuery` 实现该接口。`WorldEventContracts` 持有默认实现，并提供仅测试可替换的 reader seam；所有生产调用仍经 `QueryEventTriggerCandidatesAsync`。

为现有 `WorldFactQueryResult` 增加：

- `JournalRevision`：真实 Journal 成功或合法空结果的 revision，类型必须能表达缺失（实现可用 `int?` 或等价的 presence 标记）；无 Journal revision 不得伪造 0 或 `-1`；
- 现有 `Status`、`ErrorCode`、`Policy`、`Facts`、`SourceFactIds`、`Decisions` 保持兼容；
- `SourceFactIds` 与 `Facts[*].factId` 一一对应、按稳定 ID 去重；
- fallback 必须使用独立状态 `LegacyFallbackState`：`NotApplicable`、`NotUsed`、`Used`、`Rejected`；`Used` 只表示实际使用了 legacy，`Rejected` 不得写入 `UsedLegacyFallback=true`；
- `UsedLegacyFallback` 与 `AllowLegacyFallback` 组合语义固定，避免非 Journal 事实冒充正式 Event candidate。

## 4. Legacy fallback 与状态语义

`WorldFactQueryRequest` 增加 `AllowLegacyFallback`，默认保持现有兼容行为；Event facade 固定设置为 `false`。

- Journal `Success` / `Empty`：必须有真实 `JournalRevision`，可以成为 Event candidate 输入。
- Journal `Missing`、`Unavailable`、`Corrupt`：保持原状态和错误码，不转成 Empty。
- 当存在 legacy 快照但 Event facade 禁止 fallback 时，返回 `Unavailable`，`LegacyFallbackState=Rejected`，`UsedLegacyFallback=false`，错误码为稳定的 `awake.world_fact.event.legacy_fallback`，`JournalRevision` 缺失。
- 当允许 fallback 且实际使用 legacy 时，返回 `Unavailable` 或专用非正式状态，`LegacyFallbackState=Used`，`JournalRevision` 缺失；该结果不得成为正式 Event candidate。
- 当允许 fallback 但 legacy 没有匹配事实时，仍保留 `LegacyFallbackState=Used` 和缺失 revision，不得误报为 Journal `Empty`。
- `InvalidRequest`：不读取、不写入。
- `Cancelled`：新增稳定查询状态或等价结果状态，错误码固定为 `awake.cancelled`。

取消优先级高于请求校验：预取消必须先返回 `awake.cancelled`；Journal 读取中取消也由 query/facade 捕获并转换为同一稳定结果，不向 Engine 泄漏未分类的 `OperationCanceledException`。Engine 收到 `Cancelled` 后必须立即返回，不得进入 eligible rule、weighted random、ShowRule、RecordTrigger 或 Command。

## 5. Event 唯一调用点与行为保持

唯一接入位置为 `AwakeEventEngine.OnHourlyTickCoreAsync` 的既有 eligible rule 选择前，通过 `WorldEventContracts.QueryEventTriggerCandidatesAsync` 调用一次：

```text
hourly tick
  → QueryEventTriggerCandidatesAsync（只读观察）
  → 记录 candidate/status/revision
  → 既有 rule eligibility
  → 既有 weighted random selection
  → 既有 ShowRule / RecordTrigger / Command
```

重要行为约束：

- `Success`、`Empty`、`Missing`、`Unavailable`、`Corrupt` 都不会改变既有规则的选择或效果；本批只增加事实观察和日志。
- `Cancelled` 只停止当前已取消的 Engine 调用，不进入任何规则或效果路径；正常未取消调用的既有行为保持不变。
- `Empty` 不等于“阻止事件”，也不触发任何新效果。
- `AwakeEventBehavior` 仍只负责调度 Engine，不直接调用事实接口。
- 本批不把事实结果写回 Journal、报告、Persona Storage 或事件状态。

## 6. 可观察日志

Event facade 每次调用记录：

- `consumer=event`
- `policy=EventTriggerCandidate`
- `status`
- `journal_revision`
- `fact_count`
- `source_fact_count`
- `used_legacy_fallback`
- `legacy_fallback_state`
- `error_code`
- `correlation_id`

日志不包含 Provider 请求正文或敏感数据。

## 7. 允许修改范围

- `src/WorldFactQuery.cs`：接口实现、revision、fallback 和取消结果不变量；
- `src/WorldEventContracts.cs`：唯一 reader 字段、默认 reader 构造路径、唯一 Event facade 和结果映射；
- `src/AwakeEventEngine.cs`：唯一真实调用点、单次调用和失败安全日志；
- 必要的 `src/WorldFactJournal.cs`：只做 revision 元数据适配；
- `tools/worldbook-runtime-smoke/Program.cs`：reader/facade 的状态矩阵和取消测试；
- `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsBoundary.cs` 与 `Program.cs`：Engine caller fixture 和注册项。

不修改：Worldbook、Persona、NpcDialogueService、NpcMemoryService、Marcus 公共 API、菜单、同步脚本和游戏目录。

## 8. 验收与具体测试入口

在 `tools/worldbook-runtime-smoke/Program.cs` 增加并注册：

- `RunWorldFactContextInterfaceStateMatrix`：Success/Empty/Missing/Unavailable/Corrupt/InvalidRequest/Cancelled；
- `RunWorldFactContextInterfaceProvenance`：JournalRevision、factId 闭包、稳定去重；
- `RunWorldFactContextInterfaceLegacyFallback`：覆盖 NotUsed/Used/Rejected，Event facade 拒绝 fallback；
- `RunWorldFactContextInterfaceCancellation`：预取消和读取中取消均返回 `awake.cancelled`。

在 `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsBoundary.cs` 增加 `RunWorldFactEventCallerTests`，并在 `Program.cs` 的生产 smoke runner 中明确注册。使用 reader spy/Engine fixture 断言：

- hourly 路径调用 facade 一次；
- Engine 不直接创建第二个 reader；
- Event facade 失败或 Empty 时，既有 rule selection、ShowRule、RecordTrigger 和 Command 调用次数不因本批改变；
- 日志带 consumer/status/revision/error_code。

验收矩阵：

| 场景 | 必须观察到 | 证据 |
|---|---|---|
| 两条合法事实 | Success、真实 revision、两个 sourceFactIds | focused smoke |
| 无匹配事实 | Empty，不伪造事实，不改变旧事件行为 | focused smoke |
| 重复事实 | sourceFactIds 稳定去重 | deterministic fixture |
| Journal 缺失/不可用/损坏 | 保留原失败状态，不转 Empty | failure fixture |
| legacy fallback | Event facade 拒绝且不产生正式 candidate | compatibility fixture |
| 非法请求 | InvalidRequest，不读取/写入 | focused test |
| 预取消/读取中取消 | `awake.cancelled`，Engine 立即返回且无规则/效果/写入 | cancellation test |
| Engine 真实入口 | 每小时只调用一次 facade | production smoke |
| 既有事件行为 | 规则、Command、效果路径保持原样 | regression smoke |

## 9. 门禁

```text
本计划独立只读审查 APPROVED
  → 用户签收
  → 实现接口、revision 传播和唯一 Engine caller
  → focused/production smoke
  → Release build 与独立实现复审
```

最低证据为 E2。没有当前 BuildId 的用户游戏日志，不声明 E4；本批不声明 E5。

## 10. 后续但不属于本批

- Dialogue 读取事实和世界书知识；
- Worldbook 可见性和知识内容聚合；
- Persona 投影；
- Memory candidate 到 Persona Storage；
- Event candidate 转 Command、随机事件和效果扩展。

## 11. 审查记录

### Round 1

- 结论：`VERDICT: REVISE`
- P0：0；P1：5；P2：1
- 修订：唯一 wiring、legacy/revision 组合语义、取消优先级、保持既有事件行为、具体 production smoke 注册位置。
- 审查任务：独立只读任务 Darwin；未编辑、未构建、未同步、未启动游戏。

### Round 2

- 结论：`VERDICT: REVISE`
- P0：0；P1：2；P2：1
- 修订：可缺失的 JournalRevision、独立 fallback 状态、取消后的 Engine 立即返回、单 reader/单 facade 硬约束。
- 说明：本次修订发生在 Round 2 之后，尚未重新复审；在新的独立复审和用户签收前不得实现。

## 12. 本次实现与 E2 证据

- 用户已签收本计划后实施；未修改 Worldbook、Persona、Dialogue、Memory、Marcus 公共 API、菜单、同步脚本或游戏目录。
- 已实现 `IWorldFactContextReader` seam、`JournalRevision`、`LegacyFallbackState`、`AllowLegacyFallback`、`Cancelled/awake.cancelled`，并由 `WorldEventContracts.QueryEventTriggerCandidatesAsync` 统一承接事件候选查询。
- `AwakeEventEngine.OnHourlyTickCoreAsync` 在既有规则筛选前观察一次事实候选；`Cancelled` 立即退出，其他失败或空结果只记录观察日志，不进入新的规则或效果分支。
- focused runtime smoke：通过；覆盖状态矩阵、revision/sourceFactIds 闭包与去重、fallback 的 NotUsed/Used/Rejected、预取消和读取取消。
- production runtime smoke：`19/19` 通过；包含真实 `AwakeEventEngine` hourly caller fixture 和事件观察日志字段断言。
- AWAKE Release build：通过，输出 `AWAKE/_build_out/1.3.15/Release/Awake.dll`。
- 全量 `AWAKE.Tests`：除已知的 Persona golden fixture `approved persona should generate canonical authored DSL` 外通过；该失败不在本批写集内，未处理。
- 证据上限：离线 `E2`；未同步游戏目录、未启动游戏，未声明 E3/E4/E5。

## 13. 复审意见裁决

- 本批 Event facade 是旁观输入，不是既有事件规则的决策门。`Success`、`Empty`、`Missing`、`Unavailable`、`Corrupt` 均只记录观察结果；它们不得阻断既有 eligibility、weighted selection、ShowRule、RecordTrigger 或 Command。
- `LegacyFallbackState=Used/Rejected` 已在 query/facade 层阻止 legacy 事实成为正式 Event candidate；Engine 不消费失败结果中的事实，因此不需要再把 `Unavailable` 解释成事件 fail-closed。
- `Cancelled` 是唯一例外，因为它表示当前 Engine 调用已被取消；它必须在进入既有规则链前返回。
- 独立复审曾建议所有失败状态阻断事件。该建议与本节及第 5 节行为保持约束不一致，需由新的独立复审确认本裁决后，才能将本批标记为最终闭环。

### Adjudication review

- 独立只读复审：P0 `0`、P1 `0`、P2 `0`，`VERDICT: APPROVED`。
- 结论：`AwakeEventEngine` 仅在 `Cancelled` 或实际取消令牌时提前返回；其他事实状态继续既有 eligibility/selection 链；Event facade 固定关闭 legacy fallback，legacy 结果不成为正式 candidate。
