# AWAKE Native Knowledge B3：询问-only 首个纵向闭环计划

- `task_id`: `AWAKE-NATIVE-KNOWLEDGE-B3-20260826`
- `batch_id`: `awake-native-knowledge-b3-inquiry-only-20260826`
- `plan_status`: `approved_for_user_signoff`
- `review_status`: `APPROVED`
- `evidence_level`: `E0`（本文件是产品/架构计划，不代表功能已实现）
- `user_signoff`: 用户以“继续”确认沿用独立审查后的最小询问闭环；本文件仍需独立审查后才能进入代码实施。
- `implementation_contract`: `docs/PLAN-AWAKE-NativeKnowledge-B3-IMPLEMENTATION-CONTRACT-20260826.md`
- `implementation_priority`: 实施时以实施合同 v1 的固定符号、写集、状态机、存储字段、预算和停止规则为准；实施合同第 9 节关于“不持久化 expiresDay、从现有 day 推导”的决定，覆盖本计划第 3.4/3.5 节中要求持久化 `expiresDay` 的旧表述。
- `source_review`: `docs/AWAKE-BIG-DIRECTION-REVIEW-20260826.md` 与 B3 三轮独立只读审查，详见 `docs/PLAN-AWAKE-NativeKnowledge-B3-InquiryOnly-20260826-REVIEW-LOG.md`
- `source_checkpoint`: `docs/checkpoints/AWAKE-NATIVE-KNOWLEDGE-B2-20260824-checkpoint.md`

## 1. 目标

在不扩大 AWAKE 首期范围的前提下，建立第一条可验证的运行时纵向链路：

```text
玩家进入 NPC 的“知识询问”模式
→ 输入自然语言问题
→ 代码查询并按 NPC 身份筛选世界知识
→ 已知/部分已知时最多调用一次 AI 生成角色化回答
→ 未知、转介、权限阻断或 AI 不可用时由代码直接回复
→ 每个询问回合最多追加一条短期“曾被询问”记忆
→ 使用 AWAKE Storage 保存
→ 重新打开同一 NPC 后读取并表现有限连续性
```

长期产品方向仍是“知识驱动的 AI 社会沙盒”。本计划只锁定首个纵向切片，不把经济、政治、战争、周报、世界事件、NPC 自主行动或完整学习系统提前写成首期承诺。

## 2. 当前已确认的事实

### 2.1 已有能力

- B2 已提供统一的 `WorldbookRuntime.Knowledge.Query` 查询入口和结构化结果：`known`、`partial`、`referral`、`blocked`、`not_found`。
- B2 已规定只有带有已过滤文本的 `known`/`partial` 结果可以进入 AI；其他状态走代码分支。
- `NpcDialogueService` 已在现有通用对话回合中调用 B2 查询，并将过滤后的知识、NPC 身份、NPC 记忆和玩家回合送入提示词；这不等于已经存在可达的 `knowledge_inquiry` 入口。
- 当前已有一条可追踪的用户界面调用链：`AwakeMessengerOverlay.Open` → `AwakeMessengerVM` 联系人选择 → `AwakeMessengerVM.SelectContact` → 选中联系人卡片。现有 `SelectContact` 创建的是通用通讯对话，不能把它直接宣称为询问入口；B3 将在选中联系人卡片上增加一个明确的“询问知识”动作，并复用同一对话窗口。
- `WorldStateStore` 已有 `awake.npc.memories` 命名空间、稳定英雄键、异步写入、结果账本和幂等写入基础。
- `NpcMemoryService` 已能读取有限记忆块，并使用游戏内 `day` 参与记忆整理；它当前还不是 B3 的询问记忆写入契约。
- `NpcMemoryConsolidator` 已有低权重记忆的游戏日老化规则；现有低权重上限为 30 个游戏日。

### 2.2 已确认的缺口

- 当前 `NpcDialogueLauncher.TryOpenDialogue` 只有通用 `entrySource` 入口；它不是 B3 的入口。B3 的真实入口拟定为：`AwakeMessengerVM.SelectContact` 选中附近可交谈 NPC → 联系人卡片的新增“询问知识”命令 → `NpcDialogueLauncher.TryOpenKnowledgeInquiry` → `NpcDialogueService` 的 `KnowledgeInquiry` 模式 → `NpcDialogueOverlay.Open`。该链路在实现前必须以实际 UI 调用和离线测试证明可达。
- 当前 B2 没有把玩家询问结果接成独立的询问记忆写入契约。
- 当前通用 NPC 对话在关闭时会走 `CloseConversationAsync`，可能触发通用共享经历写入和额外 AI 总结；这不符合 B3 的“一次回答、无额外总结”预算，必须由代码级模式门禁隔离，而不是只换提示词。
- 当前通用对话允许关系、承诺、金币或世界效果命令；不能直接把它当作询问-only 会话。
- 当前没有 E3 同步、E4 游戏内入口证据或 E5 存档恢复证据。
- 当前讨论状态仍为 `E0` 方向阶段；B2 的离线通过不等于 B3 已实现。

## 3. 选定的最小方案

### 3.1 入口：复用通讯录中的现有 NPC 对话窗口

不新建第二套聊天系统，也不让通用自然语言分类器决定玩家是在询问、传授还是执行行动。B3 直接复用通讯录中已经存在的联系人选择和对话窗口，但不改变普通通讯的默认行为：选中联系人后，由联系人卡片上的新增“询问知识”命令显式启动 `KnowledgeInquiry` 模式。

实现上使用强类型 `NpcDialogueMode.KnowledgeInquiry`（或同等强类型模式值），不以 `entrySource == "knowledge_inquiry"` 作为安全边界。现有普通通讯构造函数继续映射到普通模式，场景喊话继续映射到场景模式；只有 `TryOpenKnowledgeInquiry` 能创建询问模式。

进入该模式后，通讯录/对话窗口必须显示“知识询问”状态，并明确提示当前不会执行关系、承诺、金币或世界效果行动。其他普通通讯、`NpcDialogueLauncher.TryOpenDialogue`、事件队列和场景入口保持原路径，不在 B3 中重写。只有从联系人卡片命令到 `TryOpenKnowledgeInquiry`、再到 `NpcDialogueService` 的真实调用方并能在离线测试中触发，才算入口完成。

### 3.2 查询：自然语言输入，代码先决策

- 玩家可以直接输入自然语言问题；B3 不要求玩家填写关键词或内部档案 ID。
- 代码构造现有 `WorldbookQuery`，填入 NPC 稳定 ID、文化、王国、聚落、身份、性别、年龄、技能和场景上下文。
- 查询只读取当前已装载且已过滤的世界知识快照，不读取完整世界书，也不让 AI 临时补造 NPC 原本不知道的客观知识。
- `known` 和 `partial` 才允许生成角色化回答；`partial` 必须保留“知道范围有限”的语气约束。
- `referral`、`blocked`、`not_found` 由代码直接生成固定或模板化回复，不能为了润色再次调用 AI。

### 3.3 AI：一次、无副作用、无命令

B3 复用现有 Provider、路由和权限编排基础，但必须使用**代码级询问模式硬门**、独立的询问提示词模板和明确的询问-only 输出契约；不能把通用 `NpcPromptTemplate.TemplateText` 仅靠文字约束当作隔离方案：

- 每个询问回合最多一次 Provider 生成调用。
- 不调用前处理、认知提案、后处理或记忆总结链；提示词编译不计作 Provider 生成，但仍受现有字节预算约束。
- AI 只能输出 NPC 的回答文本及必要的表现字段；不输出关系变化、承诺、金币、世界效果、知识写入或行动命令。
- `NpcDialogueService` 的询问模式不得把 AI 输出送入通用命令解析/结算队列；即便输出中出现伪造的命令字段，也必须在模式门禁处丢弃并记录。
- 该硬门必须覆盖 `HandleCompleted`、`ExecuteCommandAsync`、`Dispose`、`CloseConversationAfterCommandsAsync` 和 `AppendTranscriptTurn`；不能只在提示词层声明“不要执行命令”。
- 询问模式的输出处理必须使用无命令分支；即使防御性检查发现 `output.Command` 非空，也只能记录 `knowledge_inquiry_command_rejected`，不能启动 `WorldCommandBridge.ExecuteAsync`。
- AI 上下文只能包含代码筛选后的 `retrieved_knowledge`、有限 NPC 记忆、NPC 身份/状态、场景和玩家当前问题。
- AI 不得把未出现在过滤知识或有限记忆中的客观内容写成 NPC 已知事实；不确定时必须以角色化的不确定表达收束。
- Provider 未配置、权限失败、超时、解析失败或调用被取消时，回退到代码生成的简短回答，不改变任何关系或世界状态。

询问模式的 Prompt/Output 只允许回答和表现字段（例如 `reply`、`mood`），不包含通用 `effects`/`command` 可执行字段；代码仍保留防御性拒绝。普通对话继续使用现有通用模板和输出契约，不被 B3 改写。

询问模式允许进入 AI 的上下文必须逐项限定：

| 上下文 | B3 处理 |
| --- | --- |
| `retrieved_knowledge` | 允许；只用本回合 B2 已过滤、已限长的知识文本 |
| `npc_identity` | 允许；只用当前 NPC 的身份、文化、年龄、职业/角色等运行时信息 |
| `persona_dsl` | 允许；只用当前 NPC 的限长人格表达约束，不承担客观世界知识权威 |
| `npc_state` | 允许只读；用于语气和处境，不得转化为关系写入 |
| `player_known` | 允许只读；用于称呼和上下文，不改变玩家知识 |
| `scene` | 允许限长场景标签 |
| `history` | 允许当前会话内存中的最后若干回合，不持久化为 B3 Transcript |
| `npc_memory` | 只允许未过期的 `knowledge_inquiry` 记忆；不混入 promise、关系或其他事件记忆 |
| `opening_hint`、`scene_people` | B3 默认不注入，避免事件/场景路径绕过询问边界 |

这张表是 B3 的上下文白名单；实现和测试不得以“当前通用模板已经有这个字段”为理由自动加入额外上下文。

### 3.4 记忆：只记录“被询问”，不记录“观点改变”

B3 的记忆不是 NPC 学习系统，不代表 NPC 相信了玩家，也不代表 NPC 的世界知识发生变化。

每个完成的询问回合最多追加一条 `knowledge_inquiry` 记忆，内容由代码生成：

- `heroId`：当前 NPC 的稳定 Bannerlord 英雄 ID。
- `day`：询问发生时的 Bannerlord 战役日。
- `type`：`knowledge_inquiry`。
- `source`：`player_inquiry`。
- `weight`：低权重，复用现有低权重短期记忆策略。
- `topicKey`：优先由命中档案 ID/标题构成的稳定主题标识；无命中时只保留不可逆的短标识，不默认保存完整玩家原文。
- `outcome`：`known`、`partial`、`referral`、`blocked`、`not_found` 或 `ai_fallback`。
- `summary`：代码生成的短句，例如“玩家曾询问有关某主题的知识”；不保存 AI 自由总结。
- `expiresDay`：只作为 `recordedDay + 30` 的派生语义，不新增持久化字段；读取端必须使用现有 `day` 计算并执行 `age >= 30` 过滤。

约束：

- 不写入 NPC “相信”“学会”“接受”“被说服”等状态。
- 不写入原版个人关系、家族关系、族长关系或 AWAKE 信任值。
- 不保存完整世界书文本、完整提示词、Provider 原始响应或未截断的玩家输入到 NPC 记忆。
- 每个询问回合在 UI 接受输入时只生成一次稳定 `inquiryId`，并把它保存在该回合的内存状态中；B3 不自动重试 Provider，框架重复投递、Provider 回退、Storage 重试和 UI 完成回调都沿用同一个 ID。推荐组成是 `campaignSessionId|heroId|inquiry|turnSequence`，同一 NPC 的新回合使用新的序号，不承诺跨回合主题去重。
- Storage 写入必须把该 `inquiryId` 作为持久记录 ID/幂等依据；不能在 `RecordInquiryAsync` 内重新生成 GUID。
- 记忆写入失败不回滚已经显示给玩家的回答，但必须记录可诊断状态，并允许代码路径按现有存储策略重试；不得在 UI tick 中阻塞等待。

询问记忆在本回合回答已经确定后由代码立即发起写入（直接回复和 AI 回复都覆盖），不等待 `Dispose`，也不通过通用 `Reserve`/`CloseConversationAsync` 关闭流程生成。关闭窗口只负责取消活动请求、释放 UI 和结束会话。

### 3.5 读档与时间

- 读取记忆使用当前 NPC 稳定 ID和 `awake.npc.memories` 命名空间，只向 B3 提供 `type=knowledge_inquiry` 的记忆，不把通用 promise、关系或其他事件记忆混入询问上下文。
- 记忆年龄明确计算为 `age = max(0, currentGameDay - recordedDay)`；新记录 `expiresDay = recordedDay + 30`，当 `age >= 30` 时不再进入 AI 上下文。
- 回忆只是读取，不刷新 `day`、`expiresDay`、权重或可信度。
- 到期的 `knowledge_inquiry` 不进入 AI 上下文；读取过滤必须显式接收当前游戏日并以 `age >= 30` 与现有整理规则一致，不能只依赖后台整理“最终会删掉”。定期整理仍由已有批处理负责清理。
- 旧存档只要存在整数 `day`，就按 `recordedDay + 30` 计算派生期限；若记录日缺失或类型错误，则 B3 不把该记录放入询问上下文、不因 B3 强行删除，并记录一次兼容警告。不能用当前日倒推或使用现实时间。
- 读档后二次打开同一 NPC，最多观察到“此前曾询问过相关主题”的有限连续性；不能观察到 NPC 突然获得了完整知识、改变了信念或改变了关系。

## 4. 权威与副作用边界

| 对象 | B3 权威 | B3 是否可写 |
| --- | --- | --- |
| Bannerlord NPC、身份、文化、关系原值 | Bannerlord 当前运行时 | 只读 |
| 世界书客观知识 | `WorldbookRuntime.Knowledge.Query` 返回的已过滤快照 | 不写回 |
| NPC 询问记忆 | `WorldStateStore` 的 `awake.npc.memories` | 只允许 `knowledge_inquiry` 追加/读取 |
| NPC 关系、家族关系、族长关系 | 原版关系系统及现有 AWAKE 专用路径 | B3 禁止写入 |
| AI 回答 | Provider 输出，经 B3 合同解析 | 不得直接成为世界事实 |
| 世界事件、周报、玩家知识 | 各自既有或未来独立契约 | B3 不触碰 |
| 其他模组状态 | 其他模组自身 | B3 不读取、不修改、不假设兼容 |

B3 的新增持久副作用只有 AWAKE 自己的短期 NPC 询问记忆。询问模式默认不追加通用 Transcript；如果为了现有 UI 历史而保留本地回合历史，也必须明确它只存在于当前会话，不得走通用 Transcript 持久化路径。询问模式的 `Dispose` 不得调用通用 `CloseConversationAfterCommandsAsync`，也不得保留 `_settledFacts` 供通用关闭流程消费；任何关系命令、承诺命令、金币命令或世界效果命令在该模式下均不可达；即使 AI 返回了相关字段，代码也必须拒绝并记录，而不是执行。

## 5. 性能、Token 与运行顺畅要求

- 世界知识检索继续走现有索引/快照，不对全体 NPC 扫描，不在每个询问回合加载完整世界书。
- 复用现有上限：检索知识块不超过 `4096` 字节，NPC 记忆块不超过 `2500` 字节，最终提示词不超过 `32768` UTF-8 字节；若实际配置另有更低上限，以更低者为准。
- 单回合 Provider 生成调用上限为 `1`；直接分支为 `0`。初始化的提示词注册、Storage 打开和本地查询不计作 Provider 生成，但必须在测试中分别计数，防止关闭路径或后台任务偷偷增加生成调用。
- B3 禁止额外的 AI 记忆总结调用；询问记忆摘要必须由代码生成。
- Storage 读写必须异步，不得在 UI、campaign 或 mission tick 中执行阻塞网络/文件/数据库操作。
- 无 Provider 时仍能完成代码查询、直接回复和无 AI 的询问记忆写入；AI 是体验依赖，但不是让入口崩溃的单点故障。
- 每次回合记录查询状态、AI 是否调用、回退原因、记忆写入结果、Transcript 是否跳过、战役日、稳定 NPC ID和关联请求 ID，便于离线审查和游戏日志核验。

## 6. 接受标准

| 编号 | 场景 | 必须观察到的结果 | 最低证据 |
| --- | --- | --- | --- |
| B3-01 | `known` 询问 | 联系人卡片“询问知识”命令可达；查询命中；最多一次 AI；回答符合白名单上下文；无命令提交 | UI 调用链/fixture + 日志断言 + 构建 |
| B3-02 | `partial` 询问 | AI 获得有限文本并表达不确定/有限范围，不补齐被过滤细节 | fixture + 输出契约检查 |
| B3-03 | `referral` | 返回代码转介，不调用 Provider；显示可询问的知识面广 NPC | fixture + Provider 调用计数 |
| B3-04 | `blocked` | 返回代码阻断，不泄露被拒绝正文，不调用 Provider | fixture + 负向日志断言 |
| B3-05 | `not_found` | 返回代码生成的无可靠说法，不调用 Provider | fixture + Provider 调用计数 |
| B3-06 | Provider 不可用 | 有可读回退，不提交关系/世界命令；询问记忆仍按策略处理；不发生 Provider 重试 | fake Provider + 状态/调用计数断言 |
| B3-07 | 成功询问记忆 | 最多一条 `knowledge_inquiry`；包含游戏日、稳定 NPC ID、主题标识和结果；可幂等重试 | `WorldStateStore` fixture |
| B3-08 | 关闭询问会话 | `Dispose` 不触发通用共享经历写入/总结，不额外调用 AI，不追加通用 Transcript，不写关系/承诺/金币/世界效果 | Dispose/Close/AppendTranscript 负向测试 + 调用计数 |
| B3-09 | 读档/重开 | 同一 NPC 可读取短期询问记忆并表现有限连续性；不产生知识升级或关系变化 | 存储 round-trip fixture；真实 E5 仍需用户游戏日志 |
| B3-10 | 记忆过期 | `age = max(0, currentGameDay - recordedDay)`；`age >= 30` 的低权重询问记忆不进入上下文；回忆不刷新期限 | 时间边界 fixture + 旧数据兼容 fixture |
| B3-11 | 输入过长/空输入/重复提交 | 复用现有输入上限；空输入不调用查询；同一 `inquiryId` 的重复提交不重复写入 | focused tests + idempotency assertion |
| B3-12 | 其他对话模式 | 既有非询问模式行为不因 B3 改动而改变 | 回归测试 |

## 7. 实施边界与候选文件

只在正式审查和用户签收后修改以下最小范围，实际写集以实现前再次核对为准：

- `_houkai_merge/AWAKE/src/NpcDialogueService.cs`：增加明确的询问-only 会话分支，禁止通用命令、通用关闭总结和 Transcript 写入；硬门覆盖 `HandleCompleted`、`ExecuteCommandAsync`、`Dispose`、`CloseConversationAfterCommandsAsync`、`AppendTranscriptTurn`。
- `_houkai_merge/AWAKE/src/NpcDialogueLauncher.cs`、`AwakeMessengerVM.cs`、`AwakeContactCardVM.cs` 及对应 `AwakeMessenger.xml`：从联系人卡片新增“询问知识”动作，调用强类型 `TryOpenKnowledgeInquiry`；不能只增加未调用的重载，也不能把普通通讯默认改成询问模式。
- `_houkai_merge/AWAKE/src/NpcDialogueConstants.cs`、`NpcPromptTemplate.cs`：注册或配置无副作用询问提示词/输出契约。
- `_houkai_merge/AWAKE/src/NpcDialogueModels.cs` 或等价模型文件：增加强类型 `NpcDialogueMode`，让普通、场景和询问入口不能仅靠字符串区分。
- `_houkai_merge/AWAKE/src/NpcMemoryService.cs`：增加代码生成的 `knowledge_inquiry` 记录路径，避免调用 AI 总结。
- `_houkai_merge/AWAKE/src/WorldStateStore.cs`：仅在现有 memory append/read 无法满足幂等或到期过滤时做最小兼容扩展；不另造第二套存储。
- `_houkai_merge/AWAKE/src/NpcMemoryService.cs`：当前 `NpcMemorySelector` 类位于该文件，不存在独立的 `NpcMemorySelector.cs`；只补充基于现有 `day` 的询问记忆过滤逻辑，不新增 `expiresDay` 存储字段。
- `_houkai_merge/AWAKE.Tests/`：增加 B3 fixture、Provider 调用计数、命令负向断言、存储 round-trip 和时间边界测试。

禁止在 B3 中顺手修改：

- Worldbook Studio、世界书格式、内容包和四档文本。
- Marcus SDK 源码、Provider 配置层和云端密钥界面。
- 周报、世界事件、玩家知识、NPC 传授/学习或跨 NPC 传播。
- Bannerlord 原版关系、家族关系、族长关系、金币、战争或任务状态。
- `dist`、游戏 `Modules\\AWAKE`、PlayerExports、冻结候选和发布包。

## 8. 方案对比与放弃项

### 方案 A：在现有通用 NPC 对话中直接默认询问-only

放弃原因：会改变现有非询问路径的行为，可能误伤已经存在的关系/承诺/世界效果功能，回滚边界不清楚。

### 方案 B：新增独立聊天窗口和独立 Provider 框架

放弃原因：重复 UI、生命周期、权限和 Provider 接入，增加维护面；现有对话窗口已经具备输入、显示和异步生成基础。

### 方案 C：继续使用通用 AI 自由分类，自动判断询问/传授/行动

放弃原因：会把“自然语言理解”重新扩大成首期总路由，无法保证重大副作用前的确认和权限边界，也会增加 Token 和测试组合数量。

### 选定方案：B3 使用现有窗口的显式 `knowledge_inquiry` 模式，并由代码硬门隔离副作用

它只增加一个会话边界，不另造聊天系统，也不改变其他模式；查询和直接分支由代码主导，AI 只负责有限的角色化语言。它必须有真实调用方和可观察的 UI 状态，不能停留在字符串标签或未调用重载。

## 9. 实施顺序

1. 从 `AwakeMessengerVM.SelectContact` 接通联系人卡片“询问知识”命令；先证明真实调用链可达，再允许后续写入。
2. 增加 `KnowledgeInquiry` 模式硬门，覆盖 `HandleCompleted`、`ExecuteCommandAsync`、`Dispose`、`CloseConversationAfterCommandsAsync` 和 `AppendTranscriptTurn`；用负向测试证明通用副作用不可达。
3. 使用独立无命令询问提示词/输出契约和 Provider 计数；分别证明直接分支、AI答复、初始化和关闭流程不会产生额外 Provider 生成或记忆总结。
4. 在 UI 接受输入时生成一次稳定 `inquiryId`，贯穿回退、完成回调和 Storage 重试；验证存储不重新生成 GUID。
5. 固化代码生成的 `knowledge_inquiry` 记忆、现有 `day` 对应的 `recordedDay` 语义、派生 `expiresDay`、`age >= 30` 过滤和旧数据兼容，完成离线 round-trip 与兼容测试。
6. 通过项目级 Bannerlord 入口/生命周期审查后，等待用户运行游戏提供 E4/E5 证据；未经用户日志不得声称游戏内闭环完成。

## 10. 风险与未决事项

- 现有 `NpcDialogueService` 的通用命令结算、共享经历总结和 Transcript 写入与询问模式分支之间需要通过代码门禁和负向测试证明完全隔离。
- B3 不新增 `expiresDay` 保存字段；需要用当前测试 fake 和真实 Marcus Storage 合同验证现有 `day` 的读取过滤、幂等写入和旧记录兼容，不能只凭 Newtonsoft JSON 可写就视为兼容。
- 询问模式的 UI 入口位置尚未由现有 overlay 代码证明；实现前需查明最小可达入口，不得凭文件名假设。
- B3 不解决“玩家传授错误知识后 NPC 是否上当”；该能力必须作为后续独立闭环另行审查。
- B3 的离线存储 round-trip 不能替代 Bannerlord 真实读档验证；真实 E5 需要用户提供游戏日志。

## 11. 审查与实施门

```text
本计划草案
→ 独立只读审查
→ 记录 APPROVED/REVISE 及修订理由
→ 用户确认最终计划
→ 项目级 Bannerlord / AI / 存档技能复核
→ 才能创建实现租约和写代码
```

当前不得据此文件声称 B3 已实现、已同步游戏目录、已完成真实 Provider 验证或已完成游戏内存档验证。计划已通过独立审查，但仍需用户签收最终修订版，才可创建实现租约。
