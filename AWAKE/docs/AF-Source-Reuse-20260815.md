# 斯拉涅斯之拥 · AF 源码复用清单

> 日期：2026-08-15
> 来源：`_houkai_merge\AF_Source`（官方源码仓库 `daughenbaughedouard-sketch/Mount-Blade-Bannerlord-AnimusForge-mod` 的本地精选镜像，已下载核心 `.cs`/docs/prompts/prefabs）、`_af_decompiled_1.4\AnimusForge.decompiled.cs`（反编译快照，作交叉核对）、游戏目录 `Modules\AnimusForge` 的 GUI/ModuleData/PlayerExports
> 原则：只做源码级借鉴，不产生任何 AF 运行时兼容；不引用 AF DLL，不复制 Harmony/反射签名，不检测 AF，不做桥接/增强层。架构决定见：`docs\AF-Bridge-Architecture-20260815.md`（桥接方案已否决）。
> 借鉴前先读批判性评估：`docs\AF-CRITICAL-EVALUATION-20260815.md`（AF 哪些值得学、哪些不学）。

## 0. 借用策略与原创性边界

目标：**全面学习、自主重写**。把 AF 的功能、代码和架构“能用的用起来、能学的学过来”，但避免直接套用，降低抄袭争议，同时保持零运行时兼容。

工作方式：

1. 先读 AF 源码，提炼出：需求、数据模型、算法伪码、模块边界、生命周期、UI 交互、失败处理。
2. 合上 AF 源码，用我们自己的命名、文件结构、接口和注释重写；不逐行翻译，不保留 AF 的类名/字段名/注释。
3. 斯拥实现必须落到 Marcus API：路由、权限、存储、命令、RAG、主线程派发器；不能用 AF 的 AI 客户端、存档 key 或 Harmony 表。
4. 每个借鉴点记录“来源概念 → 斯拥实现 → 差异点”，统一维护 `docs\AF-BORROWED-IDEAS-LOG.md`，方便自查原创性和后续审计。
5. AF 源码目录只作外部参考，绝不复制进 `SlaaneshsEmbrace\src`；引用时写文档链接，不粘贴整段代码。
6. 世界书内容仍以斯拥四档世界书为唯一权威；只借检索算法和规则结构，不搬 AF 的正文/文案。
7. 仓库无 LICENSE：对代码概念的“学习后重写”风险较低，但若抽取明显有创作性的内容（提示词长文、美术、世界书正文）仍需先与作者确认。

原创性自查口径：交付代码中不应出现 AF 的命名痕迹、逐段相同结构、相同注释；新实现能用一句话说清“我为什么这么设计”，而不是“因为 AF 这么写”。

## 1. 可用源码资产

| 资产 | 位置 | 用途 |
| --- | --- | --- |
| 官方源码镜像 | `_houkai_merge\AF_Source` | 精选核心 `.cs`、docs、提示词、GUI prefab，可直接阅读和检索 |
| AF 反编译源码 | `_af_decompiled_1.4\AnimusForge.decompiled.cs` | 反编译快照，与官方源码交叉核对、补全整库视图 |
| AF 游戏模块 | `Modules\AnimusForge` | 对话覆盖层、世界书、MCM、GUI prefab 的运行时形态参考 |
| 爱与恨插件源码 | `AnimusForgeLoveHateHoukaiPlugin` 与 `AnimusForgeLoveHateHoukai` | 已有整合经验与事件包，作内容迁移来源，不作为新代码来源 |

本地没有 AF 的官方 GitHub 源码，搜索也未发现公开仓库。借用时以“算法级借鉴 + 数据结构复刻”为主，避免整段逐字复制；确认 AF 授权条款后再决定能否直接引用较大代码段。

## 2. 借用地图

| AF 模块 | 官方源码文件 | 核心可借资产 | 斯拥现状 | 借用方式 | 优先级 |
| --- | --- | --- | --- | --- | --- |
| 世界书/规则检索 | `KnowledgeLibraryBehavior.cs` | `LoreRule`（Keywords/RagShortTexts/SemanticPrototypes/Variants/When/TextMappings）、`BuildLoreContext`、实体提及查询、加权候选、rerank、注入上限、命中率日志 | `KnowledgeService` 只有本地关键词检索 | 引入 AF 规则 schema 与检索管线，向量部分接 Marcus RAG，离线回退本地关键词 | P0 |
| NPC 主动聊天 | `CompanionProactiveChatBehavior.cs` | `CompanionChatSession/Candidate/Motive/Storage`：Pending/Opening 状态机、按英雄冷却、动机疲劳、队伍快照动机、事件动机、对话结束跟进、大地图通知 | `NpcProactiveBehavior` 是单次随机候选 | 升级为可持久化会话状态机，动机与冷却入库 | P0 |
| 对话覆盖层 | `AnimusForgeNativeConversationOverlay.cs` + VM | `ConcurrentQueue<Action>` 主线程队列、长等待后 Esc 解锁、临时系统 UI 隐藏/恢复、等待动画 | 已有 `SlaaneshUiDispatcher` 和 `NpcDialogueOverlay` | 保留现有派发器，补齐长等待解锁与系统 UI 避让 | P0 |
| 记忆压缩 | `MyBehavior.cs`（DailyMemoryDraft/CompressedMemoryBlock/MemoryOverviewJob 等） | 日结封存、重试队列、记忆块、总览压缩、失败弹窗 | `NpcMemoryService` 已有记忆块和摘要 | 增加日结封存、总览任务与重试语义 | P1 |
| 开发者终端 | `AnimusForgeTerminalBehavior.cs` | 热键轮询、开菜单前置拦截、被拦截原因日志、根菜单 | 只有 MCM 与游戏菜单选项 | 补一个热键开发者终端，统一入口 | P1 |
| 世界事件收件箱 | `MyBehavior.cs` + `AnimusForge/GUI/Prefabs/AnimusForgeWorldEventInboxPopup.xml` | 大地图通知 + 收件箱弹窗 | `WorldEventLedger` 只写日志/周报 | 增加事件收件箱 UI | P1 |
| 稳定性基础设施 | `FreezeWatchdog.cs`、`ConversationExceptionGuard.cs`、`MissionViewExceptionGuard.cs`、`CampaignSaveChunkHelper.cs`、`BannerlordApiCompat.cs` | 卡死看门狗、对话/任务视图异常护栏、大 JSON 分块存档 | `SlaaneshLog` + try/catch | 抽取轻量看门狗和分块保存 | P1 |
| 守卫/提示词增强 | `GuardrailConfigModel.cs`、`LlmRetryPrompt.cs`、`LlmVisibleReplyNormalizer.cs` | 关键词守卫、失败重试、可见回复清洗 | 暂无 | 后续提示词批次吸收 | P2 |
| 周报 | `WeeklyReportTextHelper.cs` + `AnimusForge/CustomPrompts/WeeklyReportWritingRequirements.json` | 批次生成、失败重试、输出模式 | `NarrativeReportBuilder` 已有文本周报 | 参考生成/重试模型，UI 后置 | P2 |
| AI 配置 | `AIConfigHandler.cs`、`LlmApiCompat.cs` | 配置校验、API 诊断 | Marcus 已提供 MCM 配置 | 不搬运，只参考诊断思路 | 参考 |

## 1.1 官方仓库确认

- 仓库地址：<https://github.com/daughenbaughedouard-sketch/Mount-Blade-Bannerlord-AnimusForge-mod>
- 仓库无 LICENSE 文件，GitHub API 也返回空 license；逐字复制前需先与作者确认授权。
- 仓库 README 说明：当前主聊天链路是“场景喊话链路”（`ShoutBehavior` + `MyBehavior.BuildShoutPromptContextForExternalInternal`），原版直接对话只作兼容/回退。
- 仓库 AGENTS.md 提供了多个高价值案例：百科按钮注入、自定义 UI 窗口、指令标签三段式输出、场景 Agent 命令移动、大字符串存档溢出、双版本构建，均已收录进 `AF_Source\docs`。

## 3. 第一批落地方案

### P0-1 世界书检索升级

- 把 `LoreRule` 的字段结构吸收进 `KnowledgeModels`：`Keywords`、`RagShortTexts`、`SemanticPrototypes`、`Variants(Priority/When/Content)`、`TextMappings`。
- 检索管线改为：玩家/对话文本 → 提取实体提及与关键词 → 按实体加权查询 → 候选规则排序 → 按 `InjectLimit` 注入 → 记录命中率。
- 向量召回优先走 Marcus RAG；Companion 不可用时回退本地关键词，保持离线可用。
- 四档世界书仍是唯一权威内容源，只换检索器，不换内容格式。

### P0-2 NPC 主动聊天状态机

- `NpcProactiveBehavior` 增加 `PendingSession`：`HeroId/HeroName/MotiveType/Urgency/Affinity/ChancePercent/State/ExpiresAtDays`。
- 冷却语义：全局冷却 + 英雄冷却 + 动机疲劳，全部持久化到 `WorldStateStore`。
- 动机来源：关系情绪、队伍状态（伤兵/士气/俘虏/负重）、事件、对话跟进。
- 保留现有“NPC 主动发起 → 弹窗 → 应允 → 深谈”链路，只把随机触发升级为状态机。

### P0-3 覆盖层稳健性

- `NpcDialogueOverlay` / `GoddessDialogueOverlay` 增加：长等待超过 1 分钟后允许 Esc 关闭；临时系统 UI（Esc 菜单、选项、设置）打开时隐藏覆盖层，关闭后恢复。
- 继续使用 `SlaaneshUiDispatcher`，所有覆盖层生命周期只在主线程执行。

### P0-4 开发者终端骨架

- 参照 AF 的 `AnimusForgeTerminalBehavior`：默认热键（如 F9）+ 前置拦截（非战役地图、输入框/弹窗打开、冷却中均拦截）+ 根菜单。
- 根菜单项：神谕、祭坛、深谈、开发者检查、事件测试、事件收件箱、状态总览。
- 只在 `EnableDeveloperMenu` 开启时注册，避免普通玩家误触。

## 4. 不借与改造原则

- 不引用 `AnimusForge.dll`，不抄 Harmony patch 表，不抄 AF 对爱与恨/原版的反射签名。
- 不直接复制 AF 的 LLM 客户端、配置文件和存档 key；AI 链路全部走 Marcus API。
- 世界书只借检索算法，不借 AF 的文件格式；`slaanesh_knowledge.json` 继续由本模组维护。
- 被借用的算法必须重写为：主线程 UI、`SlaaneshUiDispatcher`、Marcus 存储/路由/命令边界。
- 授权不确定前，不逐字复制长段 AF 代码；优先按接口、数据模型和算法伪码重写。

## 5. 每批验收

- 构建：`dotnet build -c Release -p:BannerlordApi=1.3.15` 0 警告 0 错误。
- 离线：SdkSmoke 全绿、本地化校验通过、DLL 四地哈希一致。
- 游戏内：对应入口 → 调用 → 结算 → 可观察结果；列表见各功能 PLAN。
- 专项检查：世界书命中率日志、主动聊天冷却跨存档、覆盖层长等待 Esc、终端只在战役地图热键打开。
