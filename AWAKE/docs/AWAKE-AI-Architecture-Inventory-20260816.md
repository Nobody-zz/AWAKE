# AWAKE AI 架构清单

> 日期：2026-08-16
> 状态：按当前 `src/` 代码核对，非历史文档。

## 1. 分层

| 层 | 职责 | 当前状态 |
| --- | --- | --- |
| 运行时壳 | Mod 生命周期、扩展注册、UI、主线程派发 | 已实现 |
| AI 编排 | Route、任务提交、流式事件、取消、结构化输出 | 已实现 |
| 上下文 | 玩家/英雄快照、场景关键词、Prompt 变量 | 已实现 |
| 记忆 | 事实账、top-k、AI 摘要、存储写入 | 代码完成，真机待验 |
| 知识 | 本地关键词索引、Marcus RAG 入口 | 代码在，语料缺失，已暂停 |
| 状态存储 | memory / event_meta / relationships | 已实现，离线验证完成 |
| 命令 | 白名单、风险、权限、preflight、幂等、drain | 关系命令已接通 |
| 事件 | 规则注册、每小时评估、弹窗、参与话题、效果结算 | 已实现 |
| 内容包 | 世界书、事件、信件、NPC 主动 | 未接入，公开 API 待做 |

## 2. AI 编排组件

| 组件 | 文件 | 职责 |
| --- | --- | --- |
| `AiTaskGateway` | `AiTaskGateway.cs` | Route 权限、云外发、玩家绑定、提交/订阅/取消 |
| `NpcDialogueService` | `NpcDialogueService.cs` | 对话会话、历史、记忆加载、命令执行 |
| `NpcDialoguePromptPipeline` | `NpcDialoguePromptPipeline.cs` | Prompt 预算、截断、直接文本回退 |
| `NpcDialogueOutputValidator` | `NpcDialogueOutput.cs` | 结构化输出校验 |
| `NpcPromptTemplate` | `NpcPromptTemplate.cs` | NPC 内置提示词与输出 schema |
| `NpcMemorySummaryTemplate` | `NpcMemoryService.cs` | 记忆摘要提示词与解析 |

## 3. 路由

| Route | 用途 | 状态 |
| --- | --- | --- |
| `awake.route.npc.dialogue` | NPC 深谈 | 使用中 |
| `awake.route.memory.daily` | 记忆摘要 | 使用中 |
| `awake.route.preprocess` | 预留 | 未使用 |
| `awake.route.postprocess` | 预留 | 未使用 |

## 4. 上下文 Provider

| Provider | 数据 |
| --- | --- |
| `awake.player.context` | 玩家名、家族、王国、快照 token |
| `awake.hero.context` | 当前绑定英雄 |

## 5. 记忆

- `NpcMemoryService`：reserve、facts flush、summary、event facts。
- `WorldStateStore`：`awake.npc.memories`，上限 100 条、pinned 20、top-k 8、摘要 240 字。
- 当前状态：SdkSmoke 已覆盖；读档回读待真机。

## 6. 知识

- `KnowledgeService`：本地关键词索引、RAG 写入/检索、fingerprint。
- `KnowledgeRuntime`：战役级单例。
- 当前状态：代码在，运行时语料缺失，用户已暂停背景知识接入。

## 7. 状态存储

| namespace | 内容 |
| --- | --- |
| `awake.npc.memories` | NPC 跨会话记忆 |
| `awake.event_meta` | 事件冷却与每日计数 |
| `awake.relationships` | 信任/爱意/敌意 |

- 已实现 pending / outbox / idempotency / final drain。
- 离线 SdkSmoke 已覆盖；Companion 真机待验。

## 8. 命令

- `awake.relationship.delta.v1`：NPC 对话和事件选项均可触发。
- `CommandRiskPolicy`：R2Gameplay。
- `PermissionCatalog` / `PermissionGate`：统一权限。
- `WorldCommandBridge`：preflight / submit / drain。

## 9. 事件

- `AwakeEventEngine`：每小时评估、条件、权重、冷却、每日上限。
- `AwakeEventPopupService`：A/B 与可选“参与话题”。
- `AwakeEventEffectRules`：事件选项触发关系命令。
- 事件类型枚举：source / context / subject / content / resolution / choice shape / persistence。

## 10. UI 与入口

| 入口 | UI |
| --- | --- |
| 场景 T/Y | `NpcDialogueOverlay` |
| 遭遇面谈 | `NpcDialogueOverlay` |
| 通讯录 | `AwakeMessengerOverlay` |
| 命令台 | `AwakeTerminalBehavior` |

原版对话 UI 永远保留，不替换。

## 11. 治理与安全

- `PermissionCatalog` / `PermissionGate`
- `CloudExportPolicy`
- `CommandRiskPolicy`
- `AwakeUiDispatcher`
- `AwakeDeveloperReport`

## 12. 未完成/暂停

- 内容包公开 API：未实现。
- 知识语料与 RAG 接入：暂停。
- Messenger 持久化/写信/来信：未做。
- 周报/世界事件：未接线。
- 开发者检查面板：仍是文本。
- Preprocess/Postprocess：空契约。
- Companion 真机验收：待游戏内执行。
- 世界书接口与格式规范：设计草案已出，代码未实现。
