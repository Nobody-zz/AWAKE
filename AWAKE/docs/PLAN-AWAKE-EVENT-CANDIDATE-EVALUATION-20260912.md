# AWAKE 事实驱动事件候选判定批次

状态：`approved_e2_complete`

本批承接已完成的事实只读接口批次，目标是让“明确声明事实触发条件的事件规则”能够使用已查询到的世界事实参与候选判定。世界书、角色卡、人物对话、记忆和事件内容继续独立开发，不在本批冻结或改写。

## 1. 目标与完成边界

当前链路已经是：

```text
WorldFactQuery
  → WorldEventContracts.QueryEventTriggerCandidatesAsync
  → AwakeEventEngine（只观察）
  → 既有规则链
```

本批改为：

```text
事实查询
  → 事实触发条件判定
  → 仅筛选声明了事实条件的事件规则
  → 既有 Condition / cooldown / weighted selection
  → 既有 ShowRule / RecordTrigger / Command / effect
```

完成标准：

- 规则可以通过数据声明允许的事实种类和最低匹配数；
- 声明了事实条件的规则只有在当前事实结果满足条件时进入 eligible；
- 未声明事实条件的旧规则行为保持不变；
- 事实查询失败、空结果和取消的行为有明确边界；
- 判定结果可通过日志和 focused/production smoke 观察；
- 不新增事实写入、事件内容、对话调用、世界书查询、角色卡查询或效果类型。

## 2. 已确认的现状

- `WorldEventContracts.QueryEventTriggerCandidatesAsync` 已是事件事实的唯一 facade，并固定关闭 legacy fallback。
- `AwakeEventEngine.OnHourlyTickCoreAsync` 已在旧规则筛选前调用该 facade；目前结果只用于观察。
- `AwakeEventRule` 目前只有游戏上下文 `Condition`、权重、冷却和每日上限，没有事实触发条件。
- `AwakeEventDataLoader.TryParseRule` 从规则 payload 生成 `AwakeEventRule`，适合承载可选的结构化触发字段。
- `WorldFactQueryResult` 已提供 `Status`、`Facts`、`SourceFactIds`、`JournalRevision` 和 fallback 状态，可作为判定输入。

## 3. 事实触发契约

在 `AwakeEventRule` 增加可选 `FactTrigger`；缺省值表示旧规则，不要求事实。

```json
{
  "factTrigger": {
    "allowedKinds": ["war_declared", "settlement_owner_changed"],
    "minimumMatches": 1,
    "maximumAgeDays": 7
  }
}
```

字段不变量：

- `allowedKinds` 必须非空、去重、按 ordinal 排序，单项长度和总数受现有事件数据限制；
- `minimumMatches` 必须为 `1..20`；
- `maximumAgeDays` 必须为 `1..7`；
- 触发条件不携带文本、Prompt、角色名、世界书 key 或 Command 参数；
- 没有 `factTrigger` 与显式空触发条件等价，保持旧行为；
- 未知字段继续由现有事件数据验证策略处理，不静默猜测触发语义。

外部内容包通过现有 `IAwakeContentRegistry.RegisterEvent(AwakeContentEvent)` 声明该字段：`AwakeContentEvent` 增加可选的结构化 `FactTrigger` 属性，注册器必须把它映射为事件 payload 的 `factTrigger` 对象，再由 `AwakeEventDataLoader` 使用同一解析路径。外部内容包不得直接调用内部 `AwakeRuleRegistry` 或构造内部规则对象。

`AwakeEventCandidateEvaluation` 是纯内存结果，至少包含：

- `Eligible`；
- `ReasonCode`；
- `MatchedFactIds`；
- `Status`；
- `JournalRevision`。

它不持有 Bannerlord 对象，不写存档，不替换 `WorldFactQueryResult`。

## 4. 判定语义

对每一条初始 hourly 规则，在既有 `IsHourlySource`、cooldown 和 `ConditionMet` 之外增加事实条件检查。Engine 必须在 hourly tick 开始处通过 `AwakeRuntime.CurrentGameDay()` 获取游戏战役日，并将该值显式传给纯判定器；判定器不读取现实时间，也不接受调用方自填的替代日期。年龄固定为 `age = currentDay - factDay`，仅当 `0 <= age <= maximumAgeDays` 时有效，未来事实和超过窗口的事实均不匹配：

| 规则 | 查询结果 | 判定 |
|---|---|---|
| 无 `FactTrigger` | 任意非取消结果 | 保持旧规则行为 |
| 有 `FactTrigger` | `Success` | 统计窗口内 allowedKinds 且年龄不超过 maximumAgeDays 的事实；达到 minimumMatches 才 eligible |
| 有 `FactTrigger` | `Empty` 或 `Missing` | 不 eligible，记录 `fact_trigger_no_match` |
| 有 `FactTrigger` | `Unavailable` 或 `Corrupt` | 不 eligible，记录对应失败原因；不伪造匹配 |
| 任意规则 | `Cancelled` 或调用令牌已取消 | 当前 Engine 立即返回，不进入任何规则/效果路径 |
| 任意规则 | legacy fallback | facade 已拒绝；不得作为正式匹配事实 |

事实触发判定只筛选规则，不改变：

- 既有 `ConditionMet`；
- cooldown / max-per-day；
- weighted random；
- popup、dialogue queue、RecordTrigger、Command 和 effect；
- 无事实条件旧规则的可触发性。

同一条规则的 `MatchedFactIds` 按稳定 `factId` 去重并按事实时间、factId 的既有稳定顺序输出。判定不依赖随机数，不产生新事实，不把事实内容写入事件状态。

链式事件边界固定为：`factTrigger` 只作用于 hourly 入口的初始规则；`RunChainAsync` 由玩家上一节点的选择显式推进，不重新查询或继承事实触发条件。若某个链节点必须受事实约束，应作为新的 hourly 入口规则提供。无论是否满足条件，UI 展示前都必须再次检查取消令牌和 session 边界。

## 5. 失败与生命周期边界

- 事实查询仍由 `WorldEventContracts.QueryEventTriggerCandidatesAsync` 负责 generation/store/cancellation 边界；Engine 不创建第二个 reader。
- `Cancelled` 仍是唯一让当前 hourly Engine 调用提前返回的查询状态。
- `Unavailable/Missing/Corrupt/Empty` 对无 `FactTrigger` 规则不改变旧行为；对有 `FactTrigger` 规则只导致该规则不 eligible。
- 读档、Native readiness、Worldbook、Persona、Dialogue 和 Memory 生命周期不在本批改动。
- 本批不把事件候选判定持久化；重启或读档后由当前事实窗口重新计算。

## 6. 允许修改范围

- `src/AwakeEventModels.cs` 或独立事件模型文件：事实触发条件和判定结果模型；
- `src/AwakeEventDataLoader.cs`：解析可选 `factTrigger`；
- `src/AwakeContentApi.cs`：将公开 `AwakeContentEvent.FactTrigger` 映射为 payload 字段；
- `src/AwakeEventEngineCore.cs` 或新增 `src/AwakeEventCandidateEvaluator.cs`：纯函数判定；
- `src/AwakeEventEngine.cs`：把查询结果传入既有 eligible 构建，并记录判定摘要；
- `tools/worldbook-runtime-smoke/Program.cs`：触发条件、窗口、状态和去重 focused tests；
- `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsBoundary.cs` 与 `Program.cs`：真实 Engine caller、旧规则兼容和事实规则筛选 fixture；
- 本计划、审查记录和 checkpoint。

不修改：Worldbook、Persona、NpcDialogueService、NpcMemoryService、Marcus 公共 API、存档 schema、菜单、同步脚本、游戏目录和既有事件效果实现。

## 7. 验收矩阵

| 场景 | 必须观察到 | 证据 |
|---|---|---|
| 旧规则无 factTrigger | 事实 Success/Empty/Unavailable 均不改变旧 eligibility | production smoke |
| 匹配事实达到 minimumMatches | 事实规则进入 eligible，判定含稳定 MatchedFactIds | focused + production smoke |
| 匹配事实不足 | 事实规则不进入 eligible，ReasonCode 稳定 | focused smoke |
| kind 不匹配/事实过旧/未来事实 | 不 eligible，不伪造来源；年龄只按 `AwakeRuntime.CurrentGameDay()` 计算 | focused smoke |
| 重复 factId | 只计一次，来源 ID 稳定去重 | focused smoke |
| Empty/Missing | factTrigger 规则不 eligible，旧规则保持 | focused + production smoke |
| Unavailable/Corrupt | factTrigger 规则不 eligible且保留失败状态 | focused smoke |
| legacy fallback | 不进入正式事实匹配 | focused smoke |
| Cancelled | Engine 立即返回，无规则/ShowRule/RecordTrigger/Command | focused + production smoke |
| 实际 hourly caller | 每小时只调用一次 facade，判定在旧筛选链前发生 | production smoke |

最低证据为 E2：focused smoke、production smoke、AWAKE Release build。未授权前不做游戏目录同步，不启动游戏，不声明 E3/E4/E5。

focused smoke 工程必须显式编译并执行 `AwakeEventModels.cs`、`AwakeEventDataLoader.cs`、`AwakeEventEngineCore.cs` 和本批事实判定器；不以只编译 `WorldEventContracts.cs`/`WorldFactQuery.cs` 作为事实触发判定证据。依赖 Bannerlord UI/运行时的真实 `AwakeEventEngine` caller 仍由 production smoke 验证。

## 8. 设计选择

- 选择“可选规则条件 + 纯判定器”：兼容旧规则，事实判断可单测，事件 Engine 不承担文本或知识解释。
- 不把事实直接拼接到事件文本或 Prompt：避免事实层越权，也为后续对话/世界书消费保留边界。
- 不把失败统一成全局 fail-closed：事实层失败只阻断明确依赖事实的规则；否则会改变旧事件系统的既有行为。
- 不让链式事件隐式继承事实门：链式推进由上一节点的玩家选择负责，事实触发条件只约束 hourly 初始候选。
- 不在本批增加复杂关系表达式、随机事件生成器、玩家行动采集器或事件持久化：这些属于后续独立批次。

## 9. 门禁与下一动作

```text
本计划独立只读审查 APPROVED
  → 用户签收
  → 实现事实触发模型、纯判定器和唯一 Engine caller
  → focused/production smoke
  → Release build
  → 独立实现复审
```

实现已完成并通过 E2，且独立实现复审已 `APPROVED`；本批已收口。

## 10. Round 1 复审修订记录

- 结论：`VERDICT: REVISE`；P0 `0`、P1 `3`、P2 `0`。
- 已修订：明确 focused smoke 必须编译事件模型/Loader/Core/判定器；补齐公开 `RegisterEvent` 的 `FactTrigger` 映射；固定年龄公式 `0 <= currentDay - factDay <= maximumAgeDays`；明确事实触发只作用于 hourly 初始规则，链式推进不继承事实门；补充 UI 展示前的取消/session 检查要求。
- Round 2 新增修订：固定 `currentDay` 唯一来自 `AwakeRuntime.CurrentGameDay()`，禁止现实时间和调用方自填日期进入年龄判定。
- 下一步：对修订后的计划重新进行独立只读复审；在新的 `APPROVED` 和用户签收前不实现。

## 11. 实现与 E2 记录

- 用户已签收本计划后实施；未修改世界书、角色卡、人物对话、记忆、存档 schema、Marcus 公共 API、同步脚本或游戏目录。
- 已实现：`AwakeEventFactTrigger`、`AwakeEventCandidateEvaluation`、`AwakeEventCandidateEvaluator`；规则 Loader 解析与校验；`AwakeContentEvent.FactTrigger` 到 payload 的公开注册映射；小时入口事实筛选；UI 展示前取消检查。
- 判定器覆盖：allowedKinds、稳定去重与排序、`0 <= currentDay - factDay <= maximumAgeDays`、Empty/Missing、Unavailable/Corrupt、Cancelled、legacy fallback 和无事实条件旧规则兼容。
- focused smoke：通过；明确编译并执行事件模型、Loader、EngineCore 和事实判定器。
- production smoke：通过，`passed=22 failed=0`，包含真实 `AwakeEventEngine` caller 的匹配事实、内容包映射、失败状态和取消用例。
- AWAKE Release build：通过；产物为 `AWAKE/_build_out/1.3.15/Release/Awake.dll`。
- 证据等级：E2；未同步游戏目录，未启动游戏，不声明 E3/E4/E5。

## 12. 独立实现复审结论

- 结论：`APPROVED`；P0 `0`、P1 `0`、P2 `0`。
- 复审确认：实现范围、公开内容 API、真实 Engine 调用链、失败状态、取消边界及 focused/production smoke 满足本计划。
- 文档修订：将 production smoke 证据从早期记录的 `20/0` 对齐为最终 `22/22`。
