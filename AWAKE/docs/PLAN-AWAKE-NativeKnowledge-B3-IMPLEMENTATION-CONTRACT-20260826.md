# AWAKE Native Knowledge B3：实施合同 v1

- `contract_id`: `AWAKE-NATIVE-KNOWLEDGE-B3-IMPLEMENTATION-20260826`
- `based_on_plan`: `docs/PLAN-AWAKE-NativeKnowledge-B3-InquiryOnly-20260826.md`
- `contract_status`: `approved_for_user_signoff`
- `review_status`: `APPROVED`
- `user_signoff_required`: `true`
- `user_signoff`: `false`
- `implementation_authorized`: `false`
- `evidence_level`: `E0`
- `code_changes_started`: `false`
- `build_started`: `false`
- `game_directory_changed`: `false`

> 本文件是 B3 的实施合同，不是代码实现报告。它把已经批准的产品范围转换成可逐项核对的源码、状态、数据和验收边界。独立审查和用户签收完成前，不得依据本文件写代码、构建、同步或启动游戏。

## 1. 合同目标

只实现第一条“知识询问”纵向闭环：

```text
联系人卡片的“询问知识”按钮
→ 明确创建 KnowledgeInquiry 会话
→ 玩家输入问题
→ 代码调用 B2 知识查询
→ known/partial 最多一次 Provider 生成
→ referral/blocked/not_found 或 Provider 失败走代码回答
→ 代码追加一条短期询问记忆
→ Storage 异步保存
→ 重开同一 NPC 时读取未过期询问记忆
```

### 1.1 完成定义

B3 只有同时满足以下条件才算完成：

```text
入口可达
→ 调用链真实执行
→ 知识状态正确分流
→ 回答可观察
→ 询问记忆按契约保存
→ 普通对话副作用未被触发
→ 离线验收通过
```

编译通过、类存在、重载存在、JSON 能解析，都不能单独证明 B3 完成。

## 2. 状态标记规则

实施合同中的每个结论必须使用以下标记之一：

| 标记 | 含义 | 是否可直接作为实现依据 |
| --- | --- | --- |
| `FACT` | 已从当前源码、现有测试或存储实现确认 | 可以 |
| `DECISION` | 本合同锁定的设计决定 | 用户签收后可以 |
| `TARGET` | 实施后必须存在并可观察的目标 | 不能当作当前已存在事实 |
| `EVIDENCE` | 已经获得的验证证据 | 只能证明对应范围 |
| `OPEN` | 尚未确认，禁止自行补全 | 不可以 |
| `DEFERRED` | 明确不属于 B3 | 不可以加入本批次 |

## 3. 当前源码事实

下列事实已在实施合同编写前核对；它们不是目标代码。

| 标记 | 文件与位置 | 已确认事实 |
| --- | --- | --- |
| `FACT` | `src/AwakeMessengerVM.cs:11-17, 44-45` | 通讯录 VM 已有联系人列表、聊天列表、历史列表和 `AwakeContactCardVM SelectedCard`。 |
| `FACT` | `src/AwakeMessengerVM.cs:169-187` | 联系人行目前绑定到 `AwakeMessengerVM.SelectContact`；首次打开会自动选择附近联系人。 |
| `FACT` | `src/AwakeMessengerVM.cs:529-594` | `SelectContact` 当前只显示卡片、刷新关系、加载历史并创建普通 `NpcDialogueService`；没有“询问知识”动作。 |
| `FACT` | `src/AwakeContactRowVM.cs:6-58` | 联系人列表行目前只有 `ExecuteSelect`，没有知识询问命令。 |
| `FACT` | `src/AwakeContactCardVM.cs:8-105` | 联系人卡片目前只有展示属性和 `Show`，没有询问按钮状态或命令回调。 |
| `FACT` | `GUI/Prefabs/AwakeMessenger.xml:22-31, 130-149` | 左侧联系人行和右侧卡片均有独立 UI 区域；卡片当前没有操作按钮。 |
| `FACT` | `src/AwakeMessengerOverlay.cs:16-41, 75-128` | 通讯录拥有独立会话锁 `messenger/contacts`、关闭和释放生命周期。 |
| `FACT` | `src/NpcDialogueLauncher.cs:29-98` | 现有入口是 `TryOpenDialogue(target, entrySource)`；它使用字符串来源并在 Overlay 失败后可能回退原生对话。没有 `TryOpenKnowledgeInquiry`。 |
| `FACT` | `src/NpcDialogueService.cs:14-83` | 对话服务当前用 `_isSceneShout` 和 `_entrySource` 区分路径，没有统一强类型模式。 |
| `FACT` | `src/NpcDialogueService.cs:171-299` | 普通发送会刷新玩家知识、构造通用 Prompt，并提交 `NpcDialogueConstants.RouteId`。 |
| `FACT` | `src/NpcDialogueService.cs:1194-1240` | 通用完成路径会验证通用输出、追加本地历史、追加 Transcript，并可能执行 `output.Command`。 |
| `FACT` | `src/NpcDialogueService.cs:1270-1312` | `AppendTranscriptTurn` 会异步持久化通用 Transcript。 |
| `FACT` | `src/NpcDialogueService.cs:1314-1380` | `ExecuteCommandAsync` 会把允许的关系/承诺/金币/世界命令送入 `WorldCommandBridge`。 |
| `FACT` | `src/NpcDialogueOverlay.cs:16-55, 132-165` | 现有对话 Overlay 负责创建 UI 层、处理输入焦点、关闭服务和释放会话。 |
| `FACT` | `src/WorldKnowledgeQueryService.cs:27-82` | B2 已实现代码查询和 `known`、`partial`、`referral`、`blocked`、`not_found` 分流。 |
| `FACT` | `src/WorldKnowledgeModels.cs:128-135` | 只有 `known` 或 `partial` 且存在过滤文本时，结果才允许进入 AI。 |
| `FACT` | `src/NpcMemoryService.cs:70-155` | 当前记忆选择按 `weight`、`day`、`id` 排序和限长，但没有询问记忆专用过期过滤。 |
| `FACT` | `src/NpcMemoryService.cs:323-342` | 当前 `LoadMemoryBlockAsync` 读取整个 NPC memory 文档后调用通用 `FormatTopK`。 |
| `FACT` | `src/WorldStateStore.cs:335-393` | 现有 memory append 通过 `FlushMemoryFactsAsync` 写入，使用 `conversationId` 和现有队列/幂等机制。 |
| `FACT` | `src/WorldStateStore.cs:1627-1661` | 现有 memory 命令持久化字段包含 `conversationId`、`day`、`type`、`facts`、`summary`、`weight`、`source`；当前没有 `expiresDay` 参数。 |
| `FACT` | `src/WorldStateStore.cs:2587-2692` | 现有 memory entry 会保存 `id`、`day`、`type`、`summary`、`facts`、`weight`、`source` 和 `conversationId`，并用 `idempotencyKey` 防止重复应用。 |

## 4. 权威边界

| 对象 | 权威来源 | B3 行为 |
| --- | --- | --- |
| NPC 稳定身份、文化、年龄、地点、原版关系 | Bannerlord 当前运行时 | 只读快照；不在 B3 写回 |
| 客观世界知识 | `WorldbookRuntime.Knowledge.Query` 的已过滤结果 | 只读；不由 AI 改写 |
| 查询状态 | B2 代码分流 | 代码决定是否能调用 AI |
| NPC 回答文本和表现字段 | B3 专用 Output Contract，经 Provider 生成 | 只作为本次表达，不直接成为世界事实 |
| 询问记忆 | `awake.npc.memories` Storage | 只允许追加 `knowledge_inquiry` |
| 关系、家族关系、族长关系、金币、承诺、世界效果 | 原版或既有 AWAKE 路径 | B3 禁止写入 |
| AI Provider | Marcus/AWAKE Provider 边界 | 只负责生成；不拥有世界状态 |

## 5. 固定入口与调用链

### 5.1 目标入口

`TARGET`：用户在联系人卡片点击“询问知识”，必须走以下唯一业务链路：

```text
AwakeMessengerOverlay
→ AwakeMessengerVM.SelectedCard
→ AwakeContactCardVM.ExecuteAskKnowledge
→ AwakeMessengerVM.StartKnowledgeInquiry
→ 关闭通讯录 Overlay 并释放 messenger/contacts 会话锁
→ AwakeUiDispatcher 下一次安全 UI 调度
→ NpcDialogueLauncher.TryOpenKnowledgeInquiry(AwakeNpcTarget)
→ NpcDialogueService(..., NpcDialogueMode.KnowledgeInquiry, ...)
→ NpcDialogueOverlay.Open(...)
```

入口必须满足：

- `AwakeMessengerVM.SelectContact` 的普通路径不改为询问模式。
- 点击按钮前保存当前选中的 `AwakeContactInfo`；关闭通讯录前从它取得稳定 `AwakeNpcTarget`，关闭后只传递该目标。
- 目标必须存在、处于附近、通过 `NpcDialogueLauncher.IsEligibleNpcTarget` 检查。
- 通讯录 Overlay 和 NPC 对话 Overlay 不得同时保持打开。
- 询问入口失败时不得回退成普通 AWAKE 对话或 Bannerlord 原生对话；应显示可读失败状态并记录日志。

### 5.2 固定新增/修改符号

`DECISION`：下列符号名是 B3 合同名称。不得通过改名、字符串替代或增加第二条业务路径绕过这些符号的语义。

| 文件 | 固定符号/字段 | 语义 |
| --- | --- | --- |
| `src/NpcDialogueModels.cs` | `NpcDialogueMode.Standard`、`SceneShout`、`KnowledgeInquiry` | 普通、场景、询问三种强类型模式。必须是 `internal enum`，不以字符串替代。 |
| `src/AwakeContactCardVM.cs` | `CanAskKnowledge`、`AskKnowledgeText`、`ExecuteAskKnowledge()` | 卡片按钮状态、中文显示文本和点击回调。 |
| `src/AwakeMessengerVM.cs` | `StartKnowledgeInquiry(AwakeContactInfo contact)` | 捕获目标、校验目标、关闭通讯录并调度询问入口。 |
| `src/NpcDialogueLauncher.cs` | `TryOpenKnowledgeInquiry(AwakeNpcTarget target)` | 唯一可创建 `KnowledgeInquiry` 的 Launcher 入口。 |
| `src/NpcDialogueService.cs` | `Mode` | 所有副作用硬门读取此强类型属性，不读取 `entrySource` 判断权限。 |
| `src/NpcDialogueVM.cs` | 询问模式下的 `inquiryId` 生成和传递 | UI 接受输入时生成一次询问回合 ID。 |
| `src/NpcMemoryService.cs` | `RecordKnowledgeInquiryAsync(...)`、`LoadKnowledgeInquiryMemoryBlockAsync(...)` | 代码生成询问记忆和按游戏日读取过滤。 |
| `src/NpcDialogueConstants.cs` / `src/NpcPromptTemplate.cs` | `KnowledgeInquiryPromptId`、`KnowledgeInquiryOutputContractId`、`CreateKnowledgeInquiryDefinition()` | 只包含回答和表现字段，不包含可执行命令。继续复用现有 `RouteId`。 |
| `src/NpcDialogueOutput.cs` | `TryValidateKnowledgeInquiry(...)` | 只验证 `reply`、`mood`；存在 `effects`、`command` 或其他属性时返回无效。 |

### 5.3 `entrySource` 的唯一用途

`DECISION`：`entrySource` 仅用于会话锁、日志和诊断显示，不得用于以下任何行为判断：

- 是否为询问模式；
- 是否允许命令；
- 是否追加 Transcript；
- 是否触发通用关闭总结；
- 是否写入询问记忆。

所有上述判断只能读取 `NpcDialogueMode.KnowledgeInquiry`。

## 6. 询问回合状态机

`DECISION`：询问回合必须实现以下状态语义；状态判断必须集中在 `NpcDialogueMode` 和回合状态中，不得由多个字符串分支分别解释。

```text
Idle
→ InputAccepted
→ Querying
→ DirectReply
或
→ ProviderWaiting
→ AiReply
或
→ AiFallback
→ ResponseSettled
→ MemoryWriteRequested
→ Completed
```

任意未完成状态都可以进入：

```text
Cancelled / Disposed
```

状态规则：

| 状态/事件 | Provider | 知识查询 | 记忆 | 通用副作用 |
| --- | ---: | ---: | --- | --- |
| 空输入 | 0 | 0 | 不写 | 不触发 |
| 有效问题已接受 | 按结果 0 或 1 | 1 | 回答确定后最多写 1 条 | 禁止 |
| `known` | 1 | 1 | 回答确定后最多写 1 条 | 禁止 |
| `partial` | 1 | 1 | 回答确定后最多写 1 条 | 禁止 |
| `referral` | 0 | 1 | 代码回答确定后最多写 1 条 | 禁止 |
| `blocked` | 0 | 1 | 代码回答确定后最多写 1 条 | 禁止 |
| `not_found` | 0 | 1 | 代码回答确定后最多写 1 条 | 禁止 |
| Provider 失败/超时/解析失败 | 不重试 | 1 | 代码回退回答确定后最多写 1 条 | 禁止 |
| 取消/关闭后尚未确定回答 | 0 或已发起的 1 | 已发起则不重放 | 不写 | 禁止 |
| 回答确定后窗口关闭 | 不增加 | 不增加 | Storage 写入继续使用同一 ID | 禁止 |

`DECISION`：Provider 失败后必须代码回退，且不得再次调用 Provider。Storage 的既有异步重试不属于 Provider 重试，必须复用同一 `inquiryId` 和幂等键。

## 7. `inquiryId`、CorrelationId 和 generation

三者职责必须完全分开：

| 标识 | 生成位置 | 用途 | 是否持久化为询问身份 |
| --- | --- | --- | --- |
| `inquiryId` | `NpcDialogueVM.ExecuteSend` 在询问模式接受输入时生成一次 | 本回合业务身份、幂等、回退、完成回调、Storage 重试 | 是，通过 `conversationId`/`id` 保留 |
| `CorrelationId` | AWAKE 请求上下文 | 日志、Provider/Storage 诊断链路 | 否，不得替代 `inquiryId` |
| `generation` | `AiTaskGateway` | 同一路由的异步事件代次 | 否，不得作为业务 ID |

固定格式：

```text
inquiryId = "inquiry|" + Guid.NewGuid().ToString("N")
conversationId = "inquiry|" + heroId + "|" + inquiryId
```

格式中的前缀用于诊断，不承担权限判断。任何回调、回退或 Storage 重试都必须继续使用同一个 `inquiryId`；禁止在 Storage 方法内部重新生成 GUID。

## 8. B2 查询和 AI 合同

### 8.1 代码查询

`DECISION`：玩家只输入自然语言；代码构造 `WorldbookQuery`，使用以下当前运行时信息，不额外注入未列出的运行时字段：

- NPC 稳定 ID；
- 身份、文化、王国、聚落和角色；
- 性别、年龄、族长状态和相关技能（可用时）；
- 当前场景的限长标签；
- 玩家当前问题；
- 当前内容包门控状态。

查询只使用已经装载的运行时快照和索引，不读取完整世界书来源文件。

### 8.2 状态分流

| B2 状态 | B3 行为 | AI 生成次数 |
| --- | --- | ---: |
| `known` | 把已过滤文本送入询问 Prompt | 1 |
| `partial` | 把有限文本送入询问 Prompt，要求表达信息不完整 | 1 |
| `referral` | 代码生成转介提示，显示公开可询问 NPC | 0 |
| `blocked` | 代码生成阻断提示，不泄露被阻断正文 | 0 |
| `not_found` | 代码生成“没有可靠说法”的短答 | 0 |
| 查询异常 | 按安全回退处理，不伪造 `known` | 0 |

### 8.3 询问 Output Contract

`DECISION`：询问输出只允许：

```json
{
  "reply": "NPC 可以说出的回答文本",
  "mood": "可选的短表现字段"
}
```

约束：

- 不包含 `command`、`effects`、`relationship`、`promise`、`gold`、`world_effect`、`knowledge_write` 或行动字段。
- `TryValidateKnowledgeInquiry(...)` 发现 `effects`、`command` 或任意额外属性时记录 `knowledge_inquiry_output_rejected`；不进入 `WorldCommandBridge`，该回答按 AI 无法安全成形处理，走代码回退。
- `HandleCompleted` 在询问模式使用专用 Contract，不使用通用 Contract。
- 普通对话继续使用原有通用 Contract，不被 B3 改写。
- Provider 不自动重试。

### 8.4 AI 上下文白名单

询问模式只允许以下内容进入 Prompt：

| 字段 | 允许内容 | 固定限制 |
| --- | --- | --- |
| `retrieved_knowledge` | 本回合 B2 已过滤文本 | 不超过 4096 UTF-8 字节 |
| `npc_identity` | 身份、文化、年龄、职业/角色等 | 只读、限长 |
| `persona_dsl` | 当前 NPC 的表达约束 | 不承担客观知识权威 |
| `npc_state` | 当前处境和状态 | 只读，不映射为关系写入 |
| `player_known` | 玩家已知的有限上下文 | 只读、限长 |
| `scene` | 当前场景短标签 | 限长 |
| `history` | 当前会话内最近几轮 | 不持久化为 B3 Transcript |
| `npc_memory` | 未过期的 `knowledge_inquiry` 记忆 | 不混入 promise、关系或其他事件记忆 |

默认禁止注入：`opening_hint`、`scene_people`、完整世界书、Provider 原始响应、通用 Transcript、未过滤的 NPC 记忆和隐藏调试状态。

最终 Prompt 不超过 32768 UTF-8 字节；实际 Provider 配置更低时使用更低上限。

## 9. 记忆和存储合同

### 9.1 持久字段

`DECISION`：复用现有 memory entry 结构，不新增 `expiresDay` 持久字段。原因是当前 `BuildMemoryCommand` 和 `ApplyMemory` 都只支持 `day`，强行增加字段会扩大 B3 存档迁移范围。

询问记忆语义如下：

| 业务语义 | 实际存储字段 | 固定值/规则 |
| --- | --- | --- |
| 询问回合 ID | `id`、`conversationId` | 使用固定 `conversationId` |
| NPC | `heroId`/存储键 | 使用稳定 Bannerlord Hero ID |
| 记录日 | `day` | 唯一持久化的记录日字段，语义名为 `recordedDay` |
| 类型 | `type` | `knowledge_inquiry` |
| 来源 | `source` | `player_inquiry` |
| 权重 | `weight` | 1，复用低权重短期记忆策略 |
| 主题 | `facts` 中的单条短标识 | 使用 `HitIds[0]`；没有命中时使用 `ReferralIds[0]`；两者都没有时使用 `unknown`；不得默认保存完整问题 |
| 结果 | `summary` | 代码生成的短句，包含结果状态，不使用 AI 总结 |
| 过期日 | 不持久化 | 由 `recordedDay + 30` 代码推导 |

Storage 幂等键固定为：

```text
memoryCommandId = "awake.memory.append"
idempotencyKey = conversationId + ":facts"
```

该键由现有 `WorldStateStore.BuildMemoryCommand(...)` 生成并交给 `WorldStateStore`，不新增第二套幂等算法。`inquiryId`、`conversationId` 和 `idempotencyKey` 的关系固定为：

```text
inquiryId       = "inquiry|" + randomN
conversationId  = "inquiry|" + heroId + "|" + inquiryId
id               = conversationId
idempotencyKey   = conversationId + ":facts"
```

Storage 重试、重复完成和回退只能复用上述 `conversationId` 与 `idempotencyKey`；不得把 `CorrelationId`、`generation` 或新的 GUID 用作幂等键。

示例语义，不要求固定中文措辞：

```text
id/conversationId: inquiry|hero.npc|inquiry|a1b2...
day: 120
type: knowledge_inquiry
source: player_inquiry
weight: 1
facts: ["主题：calradia:entry:grain_tax"]
summary: "玩家曾询问过一项知识；回答状态：known。"
```

### 9.2 记忆写入时机

- 空输入、无效输入、未接受输入：不写。
- 回答已经确定后，由 B3 代码立即发起一次异步写入。
- 直接回答、AI 回答和 AI 回退回答均适用。
- 不等待 `Dispose` 才写入。
- 不调用通用 `ReserveMemory`、`CloseConversationAsync` 或 AI 总结生成询问记忆。
- UI 关闭不能取消已确定回答的 Storage 写入；写入失败不回滚已显示回答，但必须记录失败并按既有 Storage 策略重试。
- 逻辑上每个 `inquiryId` 最多一条记录；物理重试必须使用同一幂等键。

### 9.3 时间过滤

`DECISION`：

```text
recordedDay = memory["day"]
age = max(0, currentGameDay - recordedDay)
expiresDay = recordedDay + 30
```

- `age < 30`：允许进入询问模式记忆上下文。
- `age >= 30`：不进入询问上下文。
- 回忆不刷新 `day`、期限、权重或可信度。
- `day` 字段不存在或不是整数：不进入询问上下文，不强制删除，记录一次兼容警告。
- 旧数据不存在 `expiresDay` 不构成错误，因为 B3 不要求该字段；只要 `day` 存在，就按上述公式计算。
- 该过滤只适用于 `type=knowledge_inquiry` 的 B3 上下文，不改变其他记忆类型的既有整理规则。

## 10. 模式硬门

以下硬门必须以 `Mode == NpcDialogueMode.KnowledgeInquiry` 为判断依据：

| 代码位置 | 询问模式允许行为 | 明确禁止行为 | 负向证据 |
| --- | --- | --- | --- |
| `SendAsync` | B2 查询、直接分流、一次询问 Provider | 通用意图路由、传授/行动分类 | `commandExecuteCount == 0` |
| `HandleCompleted` | 验证询问 Output、显示回答、写一次询问记忆 | 解析后调用通用命令、关系/金币/世界效果 | `WorldCommandBridge` 未调用 |
| `ExecuteCommandAsync` | 记录拒绝并返回 | 任何命令结算 | `knowledge_inquiry_command_rejected` |
| `AppendTranscriptTurn` | 直接返回 | 写入通用 Transcript | `transcriptAppendCount == 0` |
| `CloseConversationAfterCommandsAsync` | 不进入 | 共享经历写入、AI 总结、Promise 消费 | `genericSummaryCount == 0` |
| `Dispose` | 取消活动 Provider、释放资源 | 追加记忆、启动总结或补发回合 | 关闭路径计数断言 |
| `NpcDialogueOverlay.Close` | 关闭 UI 并释放会话 | 将询问降级成普通对话 | 入口/关闭 fixture |

询问模式只允许保留当前会话内 UI 聊天历史；该历史不得进入通用 Transcript 持久化路径。

## 11. 允许写集和禁止写集

### 11.1 允许写入的文件

实际实现前必须再次确认文件仍存在且符号未迁移；未列文件不能自行加入。

- `_houkai_merge/AWAKE/src/NpcDialogueModels.cs`
- `_houkai_merge/AWAKE/src/NpcDialogueConstants.cs`
- `_houkai_merge/AWAKE/src/NpcPromptTemplate.cs`
- `_houkai_merge/AWAKE/src/NpcDialogueOutput.cs`
- `_houkai_merge/AWAKE/src/NpcDialogueService.cs`
- `_houkai_merge/AWAKE/src/NpcDialogueVM.cs`
- `_houkai_merge/AWAKE/src/NpcDialogueLauncher.cs`
- `_houkai_merge/AWAKE/src/AwakeMessengerVM.cs`
- `_houkai_merge/AWAKE/src/AwakeContactCardVM.cs`
- `_houkai_merge/AWAKE/src/NpcMemoryService.cs`
- `_houkai_merge/AWAKE/GUI/Prefabs/AwakeMessenger.xml`
- `_houkai_merge/AWAKE.Tests/Program.cs`

### 11.2 默认禁止修改的文件/范围

- `_houkai_merge/AWAKE/src/WorldStateStore.cs`：本合同禁止修改；若离线测试证明现有 memory append 无法完成稳定幂等，必须停止并修订合同。
- `AwakeMessengerOverlay.cs`、`NpcDialogueOverlay.cs`：本合同禁止修改；若真实入口验证证明无法释放/创建会话，必须停止并修订合同。
- `WorldKnowledgeQueryService.cs`、世界书运行时和内容包。
- Marcus Framework、Provider 配置层、云端 API Key UI 和本机 Worker。
- `WorldCommandBridge.cs`、关系/金币/战争/任务状态路径。
- `NpcMemoryConsolidator.cs` 的通用整理规则；B3 只增加询问类型的读取过滤。
- `dist`、游戏 `Modules/AWAKE`、PlayerExports、冻结候选、发布包。

### 11.3 明确不做

- 玩家传授知识。
- NPC 学习、相信、纠错或观点改变。
- 跨 NPC 传播。
- 周报、季度报告和世界事件生成。
- NPC 自主行动。
- 关系、家族关系、族长关系或信任数值修改。
- 完整长期记忆系统。
- 世界书编辑器、内容包和 Marcus Framework 改造。

## 12. 预算和观测计数

每个 fixture 和真实日志都必须能够区分以下计数；不能只记录一个“AI 调用次数”：

| 计数 | `KnowledgeInquiry` 期望 |
| --- | ---: |
| `knowledgeQueryCount` | 有效问题 1；空输入 0 |
| `providerGenerationCount` | `known/partial` 为 1；其他状态为 0 |
| `providerRetryCount` | 始终 0 |
| `promptRegistrationCount` | 单独记录，不计入 Provider 生成 |
| `storageReadCount` | 记录实际读取次数 |
| `logicalMemoryAppendCount` | 每个 `inquiryId` 最多 1 |
| `physicalMemoryWriteAttemptCount` | 可因既有 Storage 重试大于 1，但必须同一幂等键 |
| `genericSummaryCount` | 0 |
| `transcriptAppendCount` | 0 |
| `commandExecuteCount` | 0 |
| `relationshipMutationCount` | 0 |
| `worldEffectCount` | 0 |
| `inquiryIdRegenerationCount` | 0 |

日志必须至少包含：`inquiryId`、`correlationId`、稳定 NPC ID、查询状态、Provider 是否调用、回退原因、记忆写入结果、当前战役日和模式。

## 13. 接受矩阵

| 编号 | 输入/场景 | 必须观察到 | 失败即停止 |
| --- | --- | --- | --- |
| `B3-C01` | 联系人卡片点击询问 | 卡片按钮可见且可达；通讯录关闭后只打开询问 Overlay | 入口不可达或打开普通对话 |
| `B3-C02` | 普通联系人对话 | 仍走原有普通模式和行为 | 普通路径被改为询问模式 |
| `B3-C03` | `known` | 查询 1 次、Provider 1 次、回答可见、无命令 | Provider 超过 1 次或命令执行 |
| `B3-C04` | `partial` | AI 只获得有限文本，回答不补齐被过滤细节 | 上下文包含完整世界书或被拒绝细节 |
| `B3-C05` | `referral` | 代码直接给出公开转介 NPC，Provider 为 0 | 为润色再次调用 AI |
| `B3-C06` | `blocked` | 代码阻断且不泄露正文，Provider 为 0 | 输出被阻断正文 |
| `B3-C07` | `not_found` | 代码给出无可靠说法，Provider 为 0 | AI 自行补造事实 |
| `B3-C08` | Provider 超时/拒绝/解析失败 | 无重试，显示可读代码回退，仍不执行命令 | Provider 重试或窗口崩溃 |
| `B3-C09` | AI 输出伪造 `command` | 记录拒绝并回退，不调用 `WorldCommandBridge` | 任何命令、关系或金币变化 |
| `B3-C10` | 成功回答后关闭窗口 | 不触发通用总结、Transcript 或命令；询问记忆仍使用同一 ID写入/重试 | 关闭触发通用副作用 |
| `B3-C11` | 同一 `inquiryId` 重复完成/重试 | Storage 最终只有一条逻辑记录 | 重复记录或重新生成 ID |
| `B3-C12` | 读取第 29/30/31 日记忆 | 29 日可用；30、31 日不进入询问上下文 | 边界反转或回忆刷新期限 |
| `B3-C13` | 旧记录缺 `expiresDay` | 只依据 `day` 推导；不因缺字段删除 | 使用现实时间或当前日倒推 |
| `B3-C14` | 旧记录缺 `day` | 不进入询问上下文，保留记录并给兼容警告 | 伪造记录日或强制删除 |
| `B3-C15` | 空输入/超长输入/重复点击 | 空输入不查库；超长复用既有上限；重复点击不重入 | 空输入触发 Provider或重复提交 |
| `B3-C16` | 错误内容包/无 Provider | 入口和代码查询仍安全失败，不伪造客观知识 | 把不可用状态伪装成已知 |

## 14. 实施前检查清单

代码写入前必须逐项打勾并留下证据：

- [ ] 当前工作区没有另一个任务占用上述文件的写集。
- [ ] `AwakeMessengerVM.SelectContact`、`AwakeContactCardVM.Show` 和 `AwakeMessengerOverlay.CloseActive` 的真实符号未迁移。
- [ ] `NpcDialogueLauncher.TryOpenDialogue`、`NpcDialogueOverlay.Open` 和 `NpcDialogueService` 当前调用方已全部列出。
- [ ] 新模式不会以 `entrySource` 字符串作为安全边界。
- [ ] `NpcDialogueOutputValidator` 的现有接口已核对，询问 Contract 的注册和验证入口已确定。
- [ ] `WorldStateStore` 现有 memory append 的幂等路径足以承载固定 `conversationId`；若不足，先暂停并修订合同。
- [ ] 询问记忆读取过滤不会改变普通记忆上下文。
- [ ] `AWAKE.Tests` 的真实测试入口和 fake Provider/Storage 已核对。
- [ ] Bannerlord 生命周期、Gauntlet UI 和存档兼容技能已完成实施前边界复核。
- [ ] 用户签收本合同后，才能创建实现租约。

## 15. 偏离处理协议

发现偏离时不得直接“按经验补上”。按以下顺序处理：

1. 立即停止当前写集，不继续扩大修改范围。
2. 在执行日志记录：合同条款、实际源码、偏离内容、影响范围和证据。
3. 只允许选择：
   - 将实现改回合同；或
   - 修订本合同新版本并重新独立审查。
4. 若涉及入口、公共接口、保存字段、权限、AI 调用预算或原版写回，必须重新取得用户签收。
5. 未完成修订前，不得用编译通过、局部测试通过或模拟日志替代缺失证据。

### 15.1 立即停止条件

- 真实联系人卡片入口不存在或需要改动普通通讯默认行为。
- 必须使用字符串 `entrySource` 才能实现模式隔离。
- 询问模式触达 `WorldCommandBridge`、关系、金币、承诺、世界效果、通用 Transcript 或通用总结。
- 同一 `inquiryId` 被重新生成、重复写入或被 `CorrelationId` 替代。
- `day` 无法可靠读取，或 29/30/31 日边界失败。
- Provider 生成超过 1 次或发生自动重试。
- 需要修改本合同未列出的文件。
- 需要修改 Worldbook Studio、Marcus、世界书内容或游戏目录。

## 16. 实施顺序与证据

```text
合同独立审查
→ 用户签收
→ 实施前 Bannerlord/AI/存档边界复核
→ 先接通联系人卡片真实入口
→ 接入强类型 KnowledgeInquiry 模式
→ 接入询问专用 Prompt/Output 和计数
→ 接入代码生成记忆及按日过滤
→ 完成 focused tests 和负向测试
→ Release build / E1
→ 离线闭环 / E2
→ 用户要求后再做 E3 同步
→ 用户运行游戏提供 E4
→ 用户保存、退出、重载提供 E5
```

当前最高可宣称证据仍为 `E0`。本合同不得宣称 B3 已实现、已接入真实游戏、已完成真实 Provider 或已验证存档。

## 17. 当前未决事项

- `OPEN`：联系人卡片按钮的具体布局位置，属于 UI 实现细节；不得改变入口和权限语义。
- `DECISION`：询问专用验证入口固定为 `NpcDialogueOutputValidator.TryValidateKnowledgeInquiry(...)`；不得复用允许 `effects`/`command` 的通用验证结果作为询问结果。
- `DECISION`：回答确定后的询问记忆写入使用不受 UI 关闭取消影响的异步路径；若现有 `NpcDialogueService` 关闭流程会取消该写入，必须在 `NpcDialogueService.cs` 内修正，不能修改 Overlay 生命周期文件。
- `DECISION`：B3 不持久化 `expiresDay`，统一从现有 `day` 推导；若实现发现必须新增保存字段，停止并修订合同。
- `DECISION`：B3 不等待 Marcus P3 的具体内部实现；只依赖 AWAKE 已锁定的 Provider/Route 合同，Marcus 由适配层接入。

## 18. 实施授权

本合同在以下条件全部满足前，保持不可实施：

```text
review_status = APPROVED
且
user_signoff = true
且
implementation_authorized = true
```

否则只能继续进行只读核对、合同修订和审查记录，不得修改运行时代码。
