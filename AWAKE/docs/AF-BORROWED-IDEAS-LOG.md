# AF 借鉴来源记录

> 用途：记录“从 AF 学到什么 → 斯拥怎么重写 → 和 AF 的差异”，用于原创性自查、代码审计和后续维护。
> 规则：AF 只作学习参考，不进入 `src`；每完成一个功能批次，在这里补一行。

## 登记模板

| 批次 | AF 来源概念 | AF 源码文件 | 斯拥实现 | 差异点 |
| --- | --- | --- | --- | --- |
| 示例 | 世界书规则 schema | `KnowledgeLibraryBehavior.cs` | `KnowledgeModels` + 自有 JSON | 字段重命名、接入 Marcus RAG、离线关键词回退 |

## 已登记

| 批次 | AF 来源概念 | AF 源码文件 | 斯拥实现 | 差异点 |
| --- | --- | --- | --- | --- |
| 待定 | 世界书检索管线（关键词/短句/语义原型/条件变体/注入上限/命中率） | `KnowledgeLibraryBehavior.cs` | 计划：`KnowledgeService` 升级 | 自有 schema、Marcus RAG、四档世界书为唯一内容源 |
| 待定 | NPC 主动聊天状态机（Pending/Opening、冷却、动机疲劳） | `CompanionProactiveChatBehavior.cs` | 计划：`NpcProactiveBehavior` 升级 | 状态存 `WorldStateStore`，动机按斯拥世界观重写 |
| 待定 | 对话覆盖层主线程动作队列与长等待解锁 | `AnimusForgeNativeConversationOverlay.cs` | 已有 `SlaaneshUiDispatcher` + 覆盖层 | 接口、类名、派发器全部自建 |
| 待定 | 记忆压缩（日结封存、记忆块、总览、重试队列） | `MyBehavior.cs` | 计划：`NpcMemoryService` 扩展 | 用 Marcus 存储与路由重写，不读 AF 存档 |
| 待定 | 热键开发者终端（前置拦截、根菜单） | `AnimusForgeTerminalBehavior.cs` | 计划：斯拥开发者终端 | 仅借鉴交互流程，菜单内容完全不同 |
| 待定 | 事件收件箱（大地图通知 + 弹窗） | `MyBehavior.cs` + `AnimusForgeWorldEventInboxPopup.xml` | 计划：`WorldEventLedger` UI | 自建 VM/prefab，不复制 XML |
| 2026-08-16 Batch 1 | NPC 主动聊天状态机（Pending/Opening/冷却/疲劳） | `CompanionProactiveChatBehavior.cs` | `NpcProactiveService` + `NpcProactiveModels` + `awake.npc.proactive` 存储 | 只保留动机/冷却/状态概念，候选池限定附近英雄，运行时依赖用钩子注入，不复制 AF 会话实现 |
| 2026-08-16 Batch 1 | 长等待 Esc 解锁 | `AnimusForgeNativeConversationOverlay.cs` | `NpcDialogueService.CanEscCancel` + `NpcDialogueOverlay` 60 秒门控 | 自己定义等待起始时间与取消语义，不再使用 AF 覆盖层代码 |

## 反模式登记（明确不学）

| AF 反模式 | 证据 | 斯拥规则 |
| --- | --- | --- |
| Mod 内嵌本地 NLP 推理 | `OnnxEmbeddingEngine.cs` 自写 BERT tokenizer、`OnnxCrossEncoderReranker.cs`、`RagWarmupCoordinator.cs`、native onnxruntime 依赖 | 不引入本地 ONNX/embedding/rerank，不手写 tokenizer；语义检索走 Marcus RAG，离线回退关键词 |
| 巨型单体文件 | `MyBehavior.cs` 约 57,600 行、`ShoutBehavior.cs` 约 37,400 行 | 按领域拆小模块，单文件可读可测 |
| SubModule 堆大量 Harmony patch | `SubModule.cs` 约 70 次 patch 操作、69 个 catch | 少量可解释 patch，优先 Marcus 注册机制 |
| 反射游戏私有 UI | `ScreenBase._layers` 非公开字段 | 只用公开 API，不行就降级 |
