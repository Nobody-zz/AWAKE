# AWAKE 拆分方案（落地版）

> 日期：2026-08-15
> 依据：`PLAN-AwakeSplit-20260815.md`（APPROVED）、`AwakeSplit-ContentClassification-20260815.md`
> 原则：先改名、再抽机制、后搬内容；每阶段可独立编译和验证；不再边修边猜。

## 1. 目标目录结构

过渡期（未最终改名目录前）：

```text
_houkai_merge/
  SlaaneshsEmbrace/              # 过渡期运行时工程，代码名已是 AWAKE
    src/                         # namespace Awake，ModId=AWAKE，DLL=Awake.dll
    GUI/Prefabs/NpcDialogue.xml
    ModuleData/Languages/awake_*.xml
    SubModule.xml                # Id=AWAKE
  SlaneshsEmbraceContent/        # 内容包工程（现有占位目录）
    src/                         # namespace SlaneshsEmbrace.Content
    GUI/Prefabs/GoddessDialogue.xml
    ModuleData/Knowledge/slaanesh_knowledge.json
    ModuleData/Languages/slaanesh_*.xml
    SubModule.xml                # Id=SlaaneshsEmbrace（旧档兼容锚点）
  AWAKE/                         # 最终运行时目录（验证通过后整体迁入）
```

游戏目录最终布局：

```text
Modules/AWAKE/                   # 运行时，先加载
Modules/AWAKE/        # 内容包，依赖 AWAKE + MarcusAIFramework
```

内容包复用旧 `SlaaneshsEmbrace` ModId 作为 Bannerlord 存档模块引用锚点；`awake.*` 运行时数据与 `slaanesh.*` 内容数据分离。

## 2. 运行时公开接口（AWAKE 提供给内容包）

- `IPersonaDialogueShell`：通用 AI 人格对话壳（覆盖层/VM/输入输出）。
- `IMenuEntryRegistry`：按游戏菜单 ID 注册菜单入口。
- `ICommandAdapterRegistry`：按 owner 注册命令适配器。
- `IStateSchemaRegistry`：注册存储 namespace 的 schema 与应用器。
- `ILifecycleHook`：`OnCampaignStart / OnReset / OnOverlayClose / OnFinalDrain`。
- `IContentPolicyGate`：内容档位门，`Pure` 默认排除未启用内容。
- `IKnowledgeCorpusRegistry`：内容包注册世界书语料。
- `IRouteRegistry`：内容包声明自己的 route ID；运行时不再硬编码内容 route。

内容包只通过上述接口接入，不反射、不引用 AWAKE 私有类型。

## 3. 文件去向（按分类表执行）

### 3.1 留在 AWAKE 运行时

`AiTaskConstants`、`AiTaskGateway`、`CloudExportPolicy`、`CommandRiskPolicy`（机制）、`EventDialogueQueue`、`Knowledge*`（机制）、`NarrativeReportBuilder`、`NpcDialogueConstants/Context/Launcher/Overlay/PromptPipeline/Service/Starter/VM`（通用壳，内容块抽出）、`NpcMemoryService`、`PermissionCatalog/Gate`、`SlaaneshDeveloperReport`、`SlaaneshLocalization/Log/UiDispatcher`、`SubModule`、`WorldCommandBridge`、`WorldEventLedger`、`WorldStateStore`（通用层）、`SlaaneshEventEngine*`（机制）、`SlaaneshConfig`（运行时部分）、`SlaaneshPresetCatalog`（机制）、`ProbeExtension`（注册框架）。

### 3.2 迁入内容包

`AltarOfferingService`、`BodyDevelopment*`、`BodyEstrusStatusService`、`CampEventRules`、`CaptiveEventBehavior/Rules`、`EstrusCycleBehavior/Rules`、`EstrusHudBehavior/Rules`、`NpcEstrusRules`、`Goddess*`（全部）、`NpcLetterBehavior/Models`、`OracleFavorService`、`OracleProposalState`、`PlayerCaptivityBehavior/Rules`、`RelationshipLabels` 及 UI、`SlaaneshOverviewService`、`WorldCommandAdapters`（内容命令）、`CommandAdapters`（内容命令）。

### 3.3 先拆分再分家

- `NpcPromptTemplate` / `NpcDialogueOutput` / `NpcDialogueService`：通用模板/状态壳留 AWAKE；世界观铁律、命令白名单、身体/发情状态块迁内容包。
- `NpcProfile*`：基础档案机制留 AWAKE；身体/发情/关系字段由内容包注册。
- `NpcProactiveBehavior/Rules`：动机/冷却机制留 AWAKE；发情/关系驱动条件迁内容包。
- `SlaaneshRuntime`：宿主/绑定/世界状态准备留 AWAKE；bless/oracle/favor 常量迁内容包。
- `SlaaneshConfig` / `SlaaneshPresetCatalog`：运行时设置与内容设置拆分，带 copy-on-first-run 迁移。
- `WorldStateStore`：通用 KV/命令/幂等层留 AWAKE；relationship/body/estrus/favor schema 由内容包注册。
- `ProbeExtension`：扩展/上下文/通用命令注册留 AWAKE；内容 route/命令迁内容包。
- `SubModule`：只注册通用行为；内容行为由内容包 `ILifecycleHook` 注册。
- `GoddessMenuBehavior`：女神/祭坛菜单迁内容包；NPC 深谈/开发者菜单留 AWAKE。
- `SlaaneshEventEngine`：引擎机制留 AWAKE；8 条硬编码内容事件迁内容包。

## 4. 命名迁移

按 `AwakeSplit-ContentClassification-20260815.md` 第六节执行：

- ModId `AWAKE`、DLL `Awake.dll`、namespace `Awake`、路由 `AWAKE.route.*`、owner `AWAKE`。
- 运行时存储 `awake.*`；运行时菜单/本地化/日志/MCM/测试工程全部 AWAKE。
- 内容包保留 `SlaaneshsEmbrace` / `slaanesh.*`。
- 旧 `slaanesh.embrace.npc.memories` / `event_meta` 走兼容迁移（marker + 惰性迁移 + 重启对账）。

## 5. 加载顺序与依赖

1. `MarcusAIFramework`
2. `AWAKE`（运行时，依赖框架与四个前置）
3. `SlaaneshsEmbrace`（内容包，`DependedModule Id="AWAKE"`）

缺少内容包时 AWAKE 独立运行，不出现女神/祭坛入口，不崩溃。

## 6. 执行顺序

1. **AWAKE 改名**：在当前工程内把运行时标识改为 AWAKE，构建/SdkSmoke 保持绿。
2. **内容包骨架**：新建 `SlaneshsEmbraceContent` 工程，先做空模块 + `SubModule.xml` + 注册接口引用。
3. **抽运行时机制**：按 3.3 拆分点先抽通用层，内容暂以“未接入”标记留在内容包工程。
4. **迁内容**：按 3.2 把内容文件/语料/UI/事件迁入内容包；运行时不再引用内容类型。
5. **ContentPolicy 基线**：默认 Pure，内容包事件/提示词/语料带档位元数据。
6. **P0 修复同批落地**：native 回退安全门、女神输出 schema、提示词变量集合。
7. **双路径验证**：无内容包 + 有内容包；构建/SdkSmoke/本地化/哈希。
8. **目录收尾**：验证通过后把运行时目录迁入 `AWAKE/`，更新文档与发布包。

## 7. 验证门

- AWAKE 单独编译：0 警告 0 错误；SdkSmoke PASS。
- AWAKE 单独进游戏：无女神/祭坛菜单，NPC 深谈不崩，记忆/事件机制可用（存储管道修复后）。
- 内容包接入：女神对话完成、祭坛/愿力/身体/发情可用、NPC 深谈走 AWAKE 覆盖层。
- 旧档：`SlaaneshsEmbrace` 内容包存在时旧 `slaanesh.*` 数据可读；`awake.*` 运行时数据可迁移。
- 哈希：`Awake.dll`、`SlaneshsEmbrace.dll` 在 `_build_out` / `dist` / 游戏目录三处一致。

## 8. 不做的事

- 不重做整个项目。
- 不实现完整六层 ContentPolicy，只做最小纯净基线。
- 不在本批开发新玩法。
- 不把内容包再拆成多个子包。
