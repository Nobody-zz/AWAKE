# AWAKE 切割 · 内容/代码全量清点与去向表

> 日期：2026-08-15
> 范围：`SlaaneshsEmbrace/src` 78 个源文件、GUI、ModuleData、测试、发布包
> 目的：逐项标明每个机制/代码的用途与去向，作为后续切割的唯一依据；本表之后不再“边修边猜”。

## 去向标记

- **AWAKE 保留**：属于通用 AI 世界运行时，保留并做 ID/命名/去耦清理。
- **AWAKE 保留 + 修复**：机制属于运行时，但当前有 bug，需要先修复。
- **内容包迁入**：属于 Slanesh's Embrace 内容包，迁出运行时。
- **拆分**：机制属于运行时，但当前硬编码了内容数据/语义，需拆成“运行时机制 + 内容配置”。
- **资源拆分**：文件同时含运行时与内容，需按 key/文件拆分。
- **清理**：只做命名/标识/死代码清理，不改行为。

## 一、src 源码清点

| 文件 | 用途 | 现状耦合 | 去向 | 备注 |
| --- | --- | --- | --- | --- |
| `AiTaskConstants.cs` | 路由/命令/存储/上下文常量 | 全部 `slaanesh.*` | AWAKE 保留 + 清理 | 运行时 ID 收敛为 `awake.*`，内容 ID 另立 |
| `AiTaskGateway.cs` | AI 任务网关，路由单飞 | 无内容 | AWAKE 保留 | 通用 |
| `AltarOfferingService.cs` | 祭坛献祭，金币→愿力 | 内容 | 内容包迁入 | 含 `AltarConstants` 菜单 ID |
| `BodyDevelopmentActions.cs` | 身体分区开发动作 | 内容 | 内容包迁入 | 18+ 门 |
| `BodyDevelopmentEditService.cs` | 身体开发读写/结算 | 内容 | 内容包迁入 | 走 `body.develop` |
| `BodyEstrusStatusService.cs` | 身体/发情状态展示 | 内容 | 内容包迁入 | |
| `CampEventRules.cs` | 营地事件规则 | 内容 | 内容包迁入 | |
| `CaptiveEventBehavior.cs` | 主队俘虏事件行为 | 内容 | 内容包迁入 | |
| `CaptiveEventRules.cs` | 俘虏事件规则/条件 | 内容 | 内容包迁入 | |
| `CloudExportPolicy.cs` | 云外发分类策略 | 无内容 | AWAKE 保留 | 通用 |
| `CommandAdapters.cs` | 祝福/神谕/愿力命令适配 | 内容 | 内容包迁入 | 适配器框架留 AWAKE |
| `CommandRiskPolicy.cs` | 命令风险分级 | 内容命令表 | AWAKE 保留 + 拆分 | 分级机制 AWAKE；风险表由内容包注册 |
| `ContextProviders.cs` | player/hero/relationship 上下文 | relationship 轴内容语义 | AWAKE 保留 + 拆分 | player/hero 纯运行时；relationship 机制 AWAKE，轴语义内容包 |
| `EstrusCycleBehavior.cs` | 发情周期推进行为 | 内容 | 内容包迁入 | |
| `EstrusCycleRules.cs` | 发情周期规则 | 内容 | 内容包迁入 | |
| `EstrusHudBehavior.cs` | 发情 HUD 提示 | 内容 | 内容包迁入 | |
| `EstrusHudRules.cs` | HUD 文案/规则 | 内容 | 内容包迁入 | |
| `EventDialogueQueue.cs` | 事件→对话安全队列 | 无内容 | AWAKE 保留 | 通用事件/对话桥 |
| `GoddessAiGateway.cs` | 女神 AI 路由封装 | 内容 | 内容包迁入 | 可泛化为 persona gateway |
| `GoddessConstants.cs` | 女神常量 + 版本/日志名 | 混 | 拆分 | 版本/日志名归 AWAKE；女神常量归内容包 |
| `GoddessDialogueScreen.cs` | 女神 Gauntlet 覆盖层 | 内容 | 内容包迁入 | 未来可由 AWAKE 通用对话壳取代 |
| `GoddessDialogueService.cs` | 女神对话服务/状态机 | 内容 | 内容包迁入 | |
| `GoddessDialogueVM.cs` | 女神对话 VM | 内容 | 内容包迁入 | |
| `GoddessEffectBridge.cs` | 女神命令/候选桥 | 内容 | 内容包迁入 | |
| `GoddessMenuBehavior.cs` | 菜单注册（女神/祭坛 + NPC 深谈/开发者） | 混 | 拆分 | 内容菜单迁内容包；NPC 深谈/开发者菜单归 AWAKE |
| `GoddessMessageSerializer.cs` | 女神消息协议 | 内容 | 内容包迁入 | |
| `GoddessModels.cs` | 女神对话模型 | 内容 | 内容包迁入 | |
| `GoddessOutputValidator.cs` | 女神输出校验 | 内容 | 内容包迁入 + 修复 | 当前 `mood/effects` 必填导致 schema 拒收 |
| `GoddessPromptTemplate.cs` | 女神人格/世界规则提示词 | 内容 | 内容包迁入 + 修复 | 变量集合需与新框架对齐 |
| `KnowledgeModels.cs` | 知识语料模型 + 本地倒排索引 | 语料内容 | AWAKE 保留 + 拆分 | 检索机制 AWAKE；语料文件归内容包 |
| `KnowledgeRuntime.cs` | 知识运行时单例 | 无内容 | AWAKE 保留 | |
| `KnowledgeService.cs` | 知识检索服务 | 引用 `slaanesh_knowledge.json` | AWAKE 保留 + 拆分 | 路径由内容包注册 |
| `NarrativeReportBuilder.cs` | 周报/事件叙事生成 | 无内容 | AWAKE 保留 | 通用世界事件报告 |
| `NpcDialogueConstants.cs` | NPC 对话常量 | 无内容 | AWAKE 保留 | |
| `NpcDialogueContext.cs` | 对话上下文单槽 | 无内容 | AWAKE 保留 | |
| `NpcDialogueLauncher.cs` | NPC 对话启动器 | 无内容 | AWAKE 保留 + 修复 | native 回退会触发对话场景崩溃 |
| `NpcDialogueOutput.cs` | NPC 输出校验 + 状态格式化 | relationship/body/estrus 格式化 | AWAKE 保留 + 拆分 | 通用输出校验 AWAKE；内容状态块内容包 |
| `NpcDialogueOverlay.cs` | NPC 对话覆盖层 | 无内容 | AWAKE 保留 | 通用对话壳 |
| `NpcDialoguePromptPipeline.cs` | 提示词预算/截断 | 无内容 | AWAKE 保留 | |
| `NpcDialogueService.cs` | NPC 对话服务 | 直接读 body/estrus/favor | AWAKE 保留 + 拆分 | 只读通用状态；内容状态由内容包贡献 |
| `NpcDialogueStarter.cs` | 原版对话回退 | 无内容 | AWAKE 保留 + 修复 | 当前回退路径导致崩溃，需加安全门或移除 |
| `NpcDialogueVM.cs` | NPC 对话 VM | 无内容 | AWAKE 保留 | |
| `NpcEstrusRules.cs` | NPC 发情规则 | 内容 | 内容包迁入 | |
| `NpcLetterBehavior.cs` | NPC 写信/收件箱行为 | 内容 | 内容包迁入 | 若做通用消息系统再泛化 |
| `NpcLetterModels.cs` | 信件模板/收件箱 | 内容 | 内容包迁入 | |
| `NpcMemoryService.cs` | NPC 跨会话记忆 | 无内容 | AWAKE 保留 | 运行时机制 |
| `NpcProactiveBehavior.cs` | NPC 主动行为评估 | 内容驱动 | AWAKE 保留 + 拆分 | 动机/冷却机制 AWAKE；发情/关系驱动内容包 |
| `NpcProactiveRules.cs` | NPC 主动规则 | 内容驱动 | AWAKE 保留 + 拆分 | |
| `NpcProfileModels.cs` | 角色卡模型/格式化 | body/estrus/relationship 字段 | AWAKE 保留 + 拆分 | 基础档案 AWAKE；内容字段内容包 |
| `NpcProfileService.cs` | 角色卡读取 | 读 body/estrus/relationship | AWAKE 保留 + 拆分 | |
| `NpcPromptTemplate.cs` | NPC 提示词模板 | 斯拉涅斯世界铁律 + `relationship.delta` | AWAKE 保留 + 拆分 | 通用模板 AWAKE；世界观/命令白名单内容包 |
| `OracleFavorService.cs` | 愿力账本服务 | 内容 | 内容包迁入 | |
| `OracleProposalState.cs` | 女神赐福候选状态 | 内容 | 内容包迁入 | |
| `PermissionCatalog.cs` | 权限目录 | 内容权限与运行时权限混列 | AWAKE 保留 + 拆分 | manifest 权限按 owner 拆分 |
| `PermissionGate.cs` | 权限门（Evaluate/Ensure + 主线程 marshal） | 无内容 | AWAKE 保留 | 已含主线程修复 |
| `PlayerCaptivityBehavior.cs` | 玩家被俘互动 | 内容 | 内容包迁入 | |
| `PlayerCaptivityRules.cs` | 玩家被俘规则 | 内容 | 内容包迁入 | |
| `ProbeExtension.cs` | 扩展注册：capability/context/命令/路由 | 运行时 + 内容命令混注册 | AWAKE 保留 + 拆分 | 框架注册 AWAKE；内容命令/路由内容包 |
| `RelationshipLabels.cs` | 信任/爱意/敌意标签 | 内容轴语义 | 内容包迁入 | 机制可泛化 |
| `RelationshipLabelUiFormatter.cs` | 关系标签 UI 格式化 | 内容 | 内容包迁入 | |
| `RelationshipLabelUiService.cs` | 附近关系标签读取 | 内容 | 内容包迁入 | |
| `SlaaneshConfig.cs` | MCM 设置 | 运行时开关 + 内容开关混用 | AWAKE 保留 + 拆分 | `AwakeConfig` + 内容配置，带 copy-on-first-run 迁移 |
| `SlaaneshDeveloperReport.cs` | 开发者诊断 | 无内容但带“神谕”命名 | AWAKE 保留 + 清理 | 去掉神谕命名 |
| `SlaaneshEventEngine.cs` | 事件引擎 + 硬编码内容事件 | 机制 + 内容规则混 | AWAKE 保留 + 拆分 | 引擎机制 AWAKE；8 条内容事件迁内容包 |
| `SlaaneshEventEngineCore.cs` | 事件规则/链/权重核心 | 无内容 | AWAKE 保留 | 通用 |
| `SlaaneshEventModels.cs` | 事件模型/效果规则 | 效果含 relationship/body 语义 | AWAKE 保留 + 拆分 | 模型机制 AWAKE；效果语义内容包 |
| `SlaaneshEventPopupService.cs` | 事件弹窗服务 | 无内容 | AWAKE 保留 | 通用 UI 组件 |
| `SlaaneshLocalization.cs` | 本地化解析 | 无内容 | AWAKE 保留 | |
| `SlaaneshLog.cs` | 日志写入 | 无内容 | AWAKE 保留 | |
| `SlaaneshOverviewService.cs` | 状态总览 | 愿力/身体/发情/关系 | 内容包迁入 | |
| `SlaaneshPresetCatalog.cs` | MCM 预设 | 内容预设硬编码 | AWAKE 保留 + 拆分 | 预设机制 AWAKE；内容档位内容包 |
| `SlaaneshRuntime.cs` | 运行时宿主/绑定/世界状态准备 | 内容常量（bless/oracle/favor） | AWAKE 保留 + 拆分 | 运行时核心 AWAKE；内容命令常量内容包 |
| `SlaaneshUiDispatcher.cs` | 主线程 UI 派发 | 无内容 | AWAKE 保留 | 已含主线程 marshal |
| `SubModule.cs` | 生命周期：注册/行为/重置 | 直接注册全部内容行为 | AWAKE 保留 + 拆分 | 运行时只注册通用行为；内容行为由内容包注册 |
| `WorldCommandAdapters.cs` | 关系/身体/发情/献祭/赐福命令适配 | 内容 | 内容包迁入 | 命令适配器框架 AWAKE |
| `WorldCommandBridge.cs` | 世界命令桥/预演/幂等 | 无内容 | AWAKE 保留 | 通用 |
| `WorldEventLedger.cs` | 世界事件账本 | 无内容 | AWAKE 保留 | 通用 |
| `WorldStateStore.cs` | 存储引擎 + 各状态 schema | 内容 schema 混入 | AWAKE 保留 + 拆分 | 通用 KV/命令层 AWAKE；content schema 内容包 |

## 二、GUI / ModuleData / 配置

| 资源 | 用途 | 去向 |
| --- | --- | --- |
| `GUI/Prefabs/NpcDialogue.xml` | NPC 对话覆盖层 UI | AWAKE 保留 |
| `GUI/Prefabs/GoddessDialogue.xml` | 女神对话 UI | 内容包迁入 |
| `ModuleData/Knowledge/slaanesh_knowledge.json` | 世界观知识语料 | 内容包迁入 |
| `ModuleData/Languages/slaanesh_embrace_strings.xml` | 运行时 + 内容文案 | 资源拆分 |
| `ModuleData/Languages/CNs/slaanesh_embrace_strings-zh-HANS.xml` | 简体中文文案 | 资源拆分 |
| `ModuleData/Languages/language_data.xml` / `CNs/language_data.xml` | 语言清单 | 按模块拆分 |
| `SubModule.xml` | 模块身份/依赖 | AWAKE 改名 + 内容包子模块另立 |
| `README_CN.md` / `README_EN.txt` | 说明文档 | 拆分（AWAKE README + 内容包 README） |
| `BUILD_VERIFICATION.txt` | 构建验收记录 | 持续更新 |

## 三、测试 / 工具 / 文档

| 资源 | 用途 | 去向 |
| --- | --- | --- |
| `SlaaneshsEmbraceTests/Program.cs` | SdkSmoke 全部断言 | 拆分：运行时测试 + 内容包测试 |
| `SlaaneshsEmbraceTests/SlaaneshsEmbrace.SdkSmoke.csproj` | 测试工程 | 双工程化 |
| `tools/validate_localization.ps1` | 本地化校验 | AWAKE 保留 |
| `docs/` 现有文档 | 规划/验收/原理 | 按主题归档到 AWAKE 或内容包 |
| `dist/Modules/AWAKE` | 发布包 | 拆为 AWAKE 包 + 内容包 |

## 四、跨文件拆分点（先做这些再搬内容）

1. `SubModule.cs`：行为注册与生命周期钩子改为内容包注册。
2. `ProbeExtension.cs`：路由/命令/上下文按 owner 注册；内容命令迁出。
3. `WorldStateStore.cs`：先抽通用 KV/命令层与 schema 注册点，再迁内容 schema。
4. `NpcDialogueService` / `NpcDialogueOutput` / `NpcPromptTemplate`：对话壳只读通用状态，内容块由内容包贡献。
5. `SlaaneshConfig.cs` / `SlaaneshPresetCatalog.cs`：运行时与内容设置拆分，带迁移。
6. `SlaaneshRuntime.cs` / `GoddessConstants.cs`：版本/日志/宿主常量归 AWAKE，内容常量归内容包。
7. `GoddessMenuBehavior.cs`：内容菜单迁出，NPC 深谈/开发者菜单留 AWAKE。
8. `NpcDialogueStarter.cs`：native 回退加安全门或移除，防止对话场景崩溃。
9. `GoddessOutputValidator.cs` / `GoddessPromptTemplate.cs`：修正输出契约与变量集合，与新框架对齐。
10. `NpcProactiveBehavior` / `NpcProactiveRules`：通用动机/冷却机制与内容驱动条件分离。

## 五、去向汇总（源码 78 个文件）

- AWAKE 保留（含清理/修复）：约 26 个
- 内容包迁入：约 25 个
- 拆分（机制 AWAKE + 内容迁出）：约 27 个

> 注：计数为按文件主导去向的大致归类；实际实现以本表每行备注为准。

## 六、AWAKE 命名迁移清单

运行时主体代码名统一改为 AWAKE；内容包保留 `SlaaneshsEmbrace` / `slaanesh.*` 命名。旧 ID 作为兼容别名或一次性迁移。

| 类别 | 旧名（运行时侧） | AWAKE 目标 |
| --- | --- | --- |
| ModId / Module Name | `SlaaneshsEmbrace` | `AWAKE` |
| DLL | `SlaaneshsEmbrace.dll` | `Awake.dll` |
| SubModuleClassType | `SlaaneshsEmbrace.SubModule` | `Awake.SubModule` |
| namespace | `SlaaneshsEmbrace` | `Awake` |
| AssemblyTitle/Product | `SlaaneshsEmbrace` | `Awake` |
| 路由 | `SlaaneshsEmbrace.route.*` | `AWAKE.route.*`（与 ExtensionId 命名空间一致） |
| 扩展 owner | `SlaaneshsEmbrace` | `AWAKE` |
| Context Provider | `slaanesh.embrace.player/hero/relationship.context` | `awake.*` |
| 运行时存储 namespace | `slaanesh.embrace.npc.memories` / `event_meta` | `awake.npc.memories` / `awake.event_meta` |
| 运行时命令/schema/事件 | `slaanesh.embrace.*` | `awake.*` |
| 运行时菜单 ID | `slaanesh_embrace_npc_talk_*` 等 | `awake_npc_talk_*` 等 |
| 本地化 key | `slaanesh.embrace.*` / `SlaaneshsEmbrace.*` | `awake.*` / `Awake.*` |
| 日志文件 | `SlaaneshsEmbrace.log` / `SlaaneshsEmbraceProbe.log` | `Awake.log` / `AwakeProbe.log` |
| MCM 设置 | `SettingsId=FolderName=SlaaneshsEmbrace` | `Awake` |
| 测试工程 | `SlaaneshsEmbrace.SdkSmoke` | `Awake.SdkSmoke` |
| 项目/目录 | `SlaaneshsEmbrace` | `AWAKE`（最终） |

命名迁移属于破坏性改动，必须和内容包切割同批落地；旧存档/旧 Companion 配置按 `PLAN-AwakeSplit` Phase 3 的兼容层处理。
