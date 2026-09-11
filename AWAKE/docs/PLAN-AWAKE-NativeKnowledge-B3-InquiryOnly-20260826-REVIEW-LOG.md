# B3 询问-only 计划独立审查日志

- `plan`: `docs/PLAN-AWAKE-NativeKnowledge-B3-InquiryOnly-20260826.md`
- `review_date`: 2026-08-26
- `review_mode`: 独立只读子智能体；未修改文件、未启动游戏、未调用 Provider
- `review_status`: `REVISE`
- `reviewer_submission`: `01a03f01-4322-74e1-a1f5-c108cf17949e`

## 关键发现

### P0

1. 当前 `NpcDialogueLauncher.TryOpenDialogue` 只有通用 `entrySource`，没有真实 `knowledge_inquiry` 调用方或 UI 入口证据。
2. 现有 `NpcDialogueService` 关闭时仍可能走通用共享经历写入和 AI 总结，AI 输出也可能进入通用命令结算；仅改变提示词/输出契约不足，必须增加代码级询问模式硬门。

### P1

1. 现有 `NpcMemorySelector` 不接收当前游戏日，也不会过滤 `expiresDay`；现有 `NpcMemoryConsolidator` 使用 `age >= 30`，计划必须与之对齐。
2. `expiresDay` 当前没有进入 `WorldStateStore.BuildMemoryCommand` 和 `ApplyMemory` 写入链路。
3. 现有 Reservation 主要按会话序列生成 ID，不能直接证明主题级幂等；修订版改为每回合稳定 `inquiryId`，不承诺跨回合主题去重。
4. 计划误列了不存在的 `NpcMemorySelector.cs`；`NpcMemorySelector` 类位于 `NpcMemoryService.cs`。
5. 现有 Persona、记忆和 Transcript 上下文必须明确允许范围，并证明询问模式不走通用 Transcript/总结副作用。
6. AI 字节上限和一次生成限制可以作为预算，但初始化、关闭总结和后台路径必须分别计数验证。

## 修订动作

- 将计划状态改为 `revised_for_user_signoff`，记录本次 `REVISE`。
- 明确真实入口是实施前置条件，不再把 `entrySource` 当作入口完成证据。
- 增加代码级询问模式硬门：不执行命令、不走共享经历总结、不追加通用 Transcript。
- 将 `expiresDay` 的保存和读取过滤列为必须验证的存储契约；旧数据采用兼容回退。
- 将记忆幂等改为每回合稳定 `inquiryId`，删除未经实现支持的“同主题去重”表述。
- 修正源码候选文件，将 `NpcMemorySelector` 归入 `NpcMemoryService.cs`。
- 增加初始化/Storage/提示词编译/Provider 生成/关闭后台路径的分项计数要求。

## 终结论

```text
VERDICT: REVISE
```

本审查不批准代码实施。修订版需用户签收，并在实施前由项目级 Bannerlord、AI 和存档相关技能再次确认实际写集与入口。

## 第二轮复核（2026-08-26）

### 发现

1. `REVISE`：计划虽已描述联系人卡片入口，但没有锁定具体可达调用点、模式传递方式和入口验收证据。
2. `REVISE`：通用命令、共享记忆总结和 Transcript 的隔离没有覆盖所有现有无条件路径；需要明确覆盖 `HandleCompleted`、`ExecuteCommandAsync`、`Dispose`、`CloseConversationAfterCommandsAsync` 和 `AppendTranscriptTurn`。
3. `REVISE`：`expiresDay` 的旧数据回退、明确年龄公式和存储写入链路仍不完整。
4. `REVISE`：`inquiryId` 必须从 UI 接受输入开始，贯穿框架重复投递、Provider 回退、Storage 重试和完成回调；不能在存储层重新生成 GUID。
5. `REVISE`：询问上下文中的 Persona、NPC 状态、玩家知识、历史和记忆字段必须逐项白名单化。
6. `REVISE`：Provider 生成、提示词编译、初始化、关闭和后台总结需要分项计数，不能只写一个“最多一次 AI”。

### 本轮修订

- 将入口从“复用 `entrySource`”改为联系人卡片新增“询问知识”命令，明确候选调用链：`AwakeMessengerVM.SelectContact` → 联系人卡片命令 → `TryOpenKnowledgeInquiry` → `KnowledgeInquiry` 模式 → `NpcDialogueOverlay.Open`。
- 明确普通通讯、事件队列和场景入口不改为询问模式。
- 将代码级硬门扩展到五个现有方法，并明确 `output.Command` 永不进入 `WorldCommandBridge.ExecuteAsync`。
- 明确 B3 不自动重试 Provider；同一询问回合的 `inquiryId` 贯穿框架重复投递、回退、存储重试和 UI 回调。
- 明确 `age = max(0, currentGameDay - recordedDay)`、`expiresDay = recordedDay + 30`、`age >= 30` 过滤以及旧记录缺字段时的固定兼容行为。
- 扩充 B3 接受标准、实施写集和实施顺序，加入 UI、Dispose、Transcript、旧数据和幂等负向验证。

### 第二轮结论

```text
VERDICT: REVISE
```

第二轮仍不批准代码实施；计划需再次复核入口调用链、模式硬门的实际覆盖和询问上下文/计数的落地方式。

## 第三轮复核（2026-08-26）

### 检查结果

- 真实入口已明确为联系人卡片新增“询问知识”动作；普通通讯不改为询问模式。
- 已锁定强类型 `KnowledgeInquiry` 模式，并要求独立询问 Prompt/Output，不依赖通用模板文字约束。
- 已明确代码硬门覆盖 `HandleCompleted`、`ExecuteCommandAsync`、`Dispose`、`CloseConversationAfterCommandsAsync` 和 `AppendTranscriptTurn`。
- 已明确回答完成时由代码写入一次询问记忆，关闭窗口不走通用总结或 Transcript 路径。
- 已明确 `inquiryId` 从 UI 接受输入开始贯穿框架重复投递、回退、Storage 重试和完成回调；B3 不自动重试 Provider。
- 已明确 `recordedDay`、`expiresDay`、`age >= 30`、旧数据固定回退和无法计算年龄时的保守兼容行为。
- 未发现新的 P0/P1；未发现计划把未实现能力写成已实现事实或明显过度设计。

### 第三轮结论

```text
VERDICT: APPROVED
```

本结论只批准计划进入用户签收阶段，不等于代码、构建、同步、Provider、游戏入口或存档验证已经完成。
