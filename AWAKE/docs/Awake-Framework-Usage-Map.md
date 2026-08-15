# AWAKE · Marcus 框架利用地图

> 日期：2026-08-16
> 状态：与当前运行时对齐。旧版 Slaanesh 时代的地图归档为 `docs/archive/Awake-Framework-Usage-Map-20260813.md`。

## 1. 身份边界

- 运行时：`AWAKE`，ModId `AWAKE`，DLL `Awake.dll`，namespace `Awake`，owner `AWAKE`，存储与路由前缀 `awake.*`。
- 内容包：`SlaaneshsEmbraceContent`，DLL `SlaneshsEmbrace.dll`，ModId `SlaaneshsEmbrace`，内容数据前缀 `slaanesh.*`。
- 运行时不依赖 AF / 爱与恨，也不反向引用内容包类型。
- 当前完整架构清单见 `docs/AWAKE-AI-Architecture-Inventory-20260816.md`，本文件只保留能力映射。

## 2. 已注册的 Marcus 能力

| 能力 | 使用方 | 当前用途 |
| --- | --- | --- |
| Extension registration | `AwakeExtension` / `ProbeExtension` | 注册探针 capability、上下文 Provider、权限与路由 |
| AiGateway | `AiTaskGateway` + `NpcDialogueService` | NPC 深谈与四条逻辑路由 |
| Prompts | `NpcPromptTemplate` + `NpcDialogueService` | NPC 对话提示词注册/编译 |
| Storage | `WorldStateStore` | `awake.npc.memories`、`awake.event_meta`、`awake.relationships` |
| Rag | `KnowledgeService` / `KnowledgeRuntime` | 世界知识检索与本地关键词回退 |
| Commands | `WorldCommandBridge` | 风险门 + 权限 + preflight/submit + drain；`awake.relationship.delta.v1` 已接入 |
| Events | `AwakeEventEngine` / `WorldEventLedger` | 事件评估、弹窗、参与话题、效果结算；账本最终持久化待游戏内验证 |
| GameData | `PlayerContextProvider` / `HeroContextProvider` | 玩家与当前英雄快照，贡献到 `PlayerKnown` |
| Diagnostics | `AwakeDeveloperReport` | 只读开发者报告 |

## 3. 逻辑路由

| Route | 用途 | 状态 |
| --- | --- | --- |
| `awake.route.npc.dialogue` | NPC 深谈 | 已实现 |
| `awake.route.preprocess` | 话题/关键词/意图分类 | 空契约 |
| `awake.route.postprocess` | 回复标签抽取 | 空契约 |
| `awake.route.memory.daily` | 日记忆压缩 | NPC 记忆摘要使用 |

## 4. 上下文 Provider

- `awake.player.context`：玩家名、家族、王国、快照 token。
- `awake.hero.context`：当前绑定英雄。

## 5. 运行时 UI 与入口

- `AwakeTerminalBehavior`：MCM 可配置快捷键呼出命令台。
- `NpcDialogueOverlay`：NPC 深谈 Gauntlet 覆盖层，失败回退原版对话。
- `AwakeMessengerOverlay`：通讯录与会话面板。

## 6. 当前未完成

- 内容包公开 API 仍是草案，注册表未落地为代码。
- `awake.npc.memories` / `awake.event_meta` / `awake.relationships` 的真实 Companion 存储管道待游戏内验证。
- 运行时命令只有关系命令；世界效果与内容语义由内容包后续注册。
