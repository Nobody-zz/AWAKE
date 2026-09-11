# Marcus-Awake 内置迁移：P0.5 配置与数据盘点

> 盘点日期：2026-08-24
> 任务：`MARCUS-AWAKE-EMBEDDED-P0.5-20260824`
> 范围：只读核对 AWAKE 配置、MCM 落盘、战役存档、框架 Storage、Companion 与 Provider 入口。
> 本批次未修改 `AWAKE.csproj`、`SubModule.xml`、`src`、`dist` 或游戏目录。

## 1. 已确认的部署事实

| 项目 | 当前事实 | 证据 |
| --- | --- | --- |
| 游戏版本 | Bannerlord `v1.3.15` | 工作区项目规则、游戏目录 DLL 与已有构建基线 |
| AWAKE 游戏模块 | `Modules\AWAKE\bin\Win64_Shipping_Client\Awake.dll`，当前文件版本 `0.2.0.0` | 游戏目录文件属性与 SHA-256 |
| AWAKE 当前 DLL | SHA-256 `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7` | 游戏目录静态核对 |
| 外部框架模块 | `Modules\MarcusAIFramework\` 仍存在，`SubModule.xml` 为 `v0.1.0` | 游戏目录实际文件 |
| 外部框架 DLL | `MarcusAIFramework.dll`，文件版本 `0.1.0.0` | 游戏目录文件属性 |
| Companion | `MarcusAIFramework.Companion.exe`、DLL、`.NET 8.0` runtimeconfig 与本地 Vulkan/CPU native runtime 均存在 | 游戏目录实际文件 |
| 当前进程 | 盘点瞬间没有 `MarcusAIFramework.Companion.exe` 进程 | 只读进程快照；不代表历史上没有启动 |
| AWAKE 依赖 | `SubModule.xml` 仍声明 `MarcusAIFramework`，项目仍引用旧 reference DLL | `_houkai_merge\AWAKE\SubModule.xml`、`AWAKE.csproj` |

结论：**内置迁移尚未开始运行时替换**。当前游戏安装仍是“外部 Marcus 模块 + AWAKE 扩展”的旧拓扑，不能把现有游戏目录当作内置迁移候选。

## 2. AWAKE MCM 配置

### 2.1 实际落盘位置

MCM 全局设置实际写入：

```text
C:\Users\26811\OneDrive\文档\Mount and Blade II Bannerlord\Configs\ModSettings\Global\AWAKE\AWAKE.json
```

这是 MCM 的玩家全局配置，不是战役存档状态，也不是 Provider 数据库。

当前实测字段包括：

| 配置域 | 字段 | 当前值/语义 | 迁移判断 |
| --- | --- | --- | --- |
| 云外发 | `EnableCloudExport` | `true` | 保留为 AWAKE 玩家级总开关 |
| 云外发 | `AllowCloudExportPlayerState` | `true` | 保留，仍须与 Route/Connection 云端门求交集 |
| 开发者 | `EnableDeveloperMenu` | `true` | 迁移为开发者入口开关；不等于开放全部诊断 |
| 命令台 | `TerminalKey` | `U` | 与 AI Provider 配置无关 |
| NPC 主动 | `EnableNpcProactive` | `true` | AWAKE 玩法设置，不能放入 Framework Core |
| NPC 主动 | `NpcProactiveChance` | `35` | AWAKE 玩法数值，不能由框架迁移逻辑解释 |
| 事件 | `EnableEventEngine` | `true` | AWAKE 事件引擎设置 |
| 引导 | `EnableInGameGuide` | `true` | AWAKE 玩家体验设置 |
| 场景 | `SceneMaxRangeMeters` | `60` | AWAKE 场景设置 |
| 场景 | `EnableSceneVisualSelection` | `true` | AWAKE UI 设置 |
| 场景 | `SceneCycleNearToFarKey` / `SceneCycleFarToNearKey` | `[` / `]` | AWAKE UI 设置 |
| 场景 | `SceneShoutKey` | `V` | AWAKE UI 设置 |
| 状态 | `AiRuntimeStatus` | `Not checked yet` | 代码标记为 `JsonIgnore`，但实测 JSON 含该字段；迁移时不能假定 MCM 序列化行为与 Newtonsoft 属性完全一致 |

### 2.2 当前代码行为

- `AwakeConfig` 继承 `AttributeGlobalSettings<AwakeConfig>`，设置 ID 为 `AWAKE`、目录名为 `AWAKE`、格式声明为 `json`。
- `AwakeSettings.Current` 通过 `BaseSettingsProvider.Instance.GetSettings("AWAKE")` 取设置；取不到时创建内存回退实例。
- 代码显式调用 `BaseSettingsProvider.Instance.SaveSettings(config)` 的位置目前只有场景喊话键归一化和“一键开启云端对话”路径。
- `SyncRoutes`、`OpenAiSetup`、`OpenDiagnostics` 目前只是调用旧框架控制台；它们不是 AWAKE 自己的 Provider 配置实现。

### 2.3 迁移契约

1. AWAKE MCM 继续保存玩家级行为开关；不能把战役状态或 API Key 放入这里。
2. Provider 配置必须迁移为 AWAKE MCM → Framework/Service 配置协议，而不是继续依赖旧 `Marcus AI Framework` MCM 页面。
3. API Key 只允许传给受控配置服务并落入 OS 保护凭据存储；不得进入 `AWAKE.json`、存档、日志、导出包或 Framework API DTO。
4. “保存并应用”必须有独立的异步配置事务；MCM 属性变更不能被误认为 Provider 已应用。
5. 迁移必须兼容旧 `AWAKE.json` 中的玩法字段，但不自动把旧 Marcus 配置解释为新 Framework 配置，除非有明确版本化转换器。

## 3. Provider 与 API Key 现状

### 3.1 AWAKE 当前没有 Provider 字段

当前 AWAKE MCM JSON 不含 URL、模型、Provider、Authorization、API Key 或 profile 内容。当前 AWAKE 源码也只调用逻辑 Route，不直接访问 HTTP、Provider DTO 或 API Key。

### 3.2 外部 Marcus 当前提供的配置面

- 游戏目录存在独立 `MarcusAIFramework` 模块和 Companion。
- Companion runtimeconfig 明确为 `.NET 8.0`。
- AWAKE 文档把玩家配置描述为旧框架的 AI 设置台/MCM 页面；这是待迁移入口，不是新目标架构。
- `Modules\AWAKE\docs\PROVIDER_SETUP.md` 与两个 profiles 示例文件属于配置说明/开发者模板，不能作为玩家运行时权威状态。
- 实测 `Configs\ModSettings\Global\MarcusAIFramework\MarcusAIFramework.json` 只包含 `DetailedLogging`、`PauseAiTasks`、`StopHighRiskCommands`、`CloudExportEnabled`、`Enabled` 等框架开关；未发现 URL、Provider、模型或 API Key 字段。

### 3.3 凭据和服务存储

只读核对确认以下路径存在；未读取任何秘密内容：

```text
C:\Users\26811\AppData\Local\MarcusAIFramework\credentials.dpapi
C:\Users\26811\AppData\Local\MarcusAIFramework\platform.db
C:\Users\26811\AppData\Local\MarcusAIFramework\campaigns\
```

因此迁移时必须把“凭据引用”和“Provider profile 元数据”分开处理：

- `credentials.dpapi` 是敏感凭据资产，不能复制或解析进 AWAKE。
- `platform.db` 是跨战役框架平台状态，不能被 AWAKE 玩法代码直接读写。
- 新 Service 应提供版本化、脱敏的配置结果 DTO；AWAKE 只接收状态、模型列表、连接测试结果和错误码。

## 4. AWAKE 战役存档状态

### 4.1 Native `SyncData` 键

当前 AWAKE 自己通过 `CampaignBehaviorBase.SyncData` 保存的键只有：

| Save key | 类型/用途 | 权威 |
| --- | --- | --- |
| `awake_last_weekly_report_day` | `int`，周报生成去重日 | AWAKE `AwakeEventBehavior` |
| `awake_worldbook_overlay_v1` | `string`，玩家对世界书 Overlay 的存档投影 | AWAKE `AwakeTerminalBehavior` / `WorldbookRuntime` |
| `awake_worldbook_activation_v1` | `string`，当前世界书包激活状态的存档投影 | AWAKE `AwakeTerminalBehavior` / `WorldbookRuntime` |

这些字段随 Bannerlord 存档走，不应因程序集拆分而丢失。它们也不应被迁移为 Framework Core 的通用存档字段；Framework 只应拥有自己的小型 anchor 契约。

### 4.2 外部 Framework Storage 现状

AWAKE 通过 `host.Storage.OpenCampaignNamespaceAsync` 打开以下 12 个命名空间：

```text
awake.npc.memories
awake.event_meta
awake.relationships
awake.npc.proactive
awake.world.events
awake.messenger
awake.transcripts
awake.contacts
awake.history.audit
awake.onboarding
awake.dialogue.queue
awake.interactions
```

主要稳定 key/schema：

| 域 | key 形式 | schema |
| --- | --- | --- |
| NPC 记忆 | `hero.<heroId>.v1` | `awake.npc.memory.v1` |
| 关系 | `hero.<heroId>.v1` | `awake.relationship.state.v1` |
| 事件元数据 | `campaign.event_meta.v1` | `awake.event_meta.v1` |
| 主动行为 | `NpcProactiveConstants.Key` | `awake.npc.proactive.v1` |
| 世界事件 | `campaign.world_events.v1` | `awake.world_events.v1` |
| Messenger | `campaign.messenger.v1` | `awake.messenger.v1` |
| Transcript | 服务内固定 key | `awake.transcripts...` 系列 schema |
| 联系人 | 服务内固定 key | `awake.contacts...` 系列 schema |
| 审计 | 服务内固定 key | `awake.history.audit...` 系列 schema |
| 引导 | `campaign.onboarding.v1` | `awake.onboarding.v1` |
| 对话队列 | `campaign.dialogue.queue.v1` | `awake.dialogue.queue.v1` |
| 互动/承诺/结算 | `campaign.interactions.v1.<contactKey>` 与恢复索引 | `awake.interactions.v1` / `awake.interactions.recovery-index.v1` |

共同约束：单值上限 `512 KiB`、命令幂等键、Schema 检查、异步写入队列和 SessionEnding 最终 drain。

### 4.3 世界知识的两条现存路径

当前代码同时存在两种“知识”概念，迁移时必须分开：

1. **旧/通用 RAG 语料路径**：`Knowledge/awake_knowledge.json` → collection `awake.knowledge`，本地关键词回退，RAG 指纹 key `knowledge.fingerprint.v1`，access scope `ExtensionProvider`。
2. **当前 Worldbook v2 档案路径**：`awake.worldbook.v2` manifest/runtime，身份、表达、授予/拒绝、Overlay、激活包和本地查询服务。

这两条路径都存在代码和文档证据，不能在内置迁移中简单把它们合并为“一个数据库表”。需要在 P1.5 的 API/程序集契约中明确：新 Framework 提供通用检索/存储能力，AWAKE 保留世界书语义、身份权限和内容包选择。

## 5. AI Service / Companion 入口

### 5.1 当前实际入口

- 外部模块：`Modules\MarcusAIFramework\SubModule.xml` → `MarcusAIFramework.dll`。
- Companion：`Modules\MarcusAIFramework\Companion\MarcusAIFramework.Companion.exe`。
- Companion 启动参数在实际日志中显示支持 `serve [--parent-process-id <pid>]`；另有 `profiles apply`、`profiles list` 管理命令。
- 游戏侧通过 Framework Host/Bridge 连接 Companion；框架日志出现 `Companion bridge start requested`、连接 session、协议版本 `0.1.0-preview.1` 与 Named Pipe 断开/重连记录。
- Companion 日志记录 Storage root 为 `%LOCALAPPDATA%\MarcusAIFramework`。

### 5.2 已观察到的生命周期与故障

历史日志显示：

- Companion 可被游戏侧启动并报告 `Companion started`。
- 存在 `EndOfStreamException`、`Pipe is broken` 和配置请求在管道未连接时发出的退化记录。
- 这些日志证明了实际进程/管道边界存在，但不证明当前内置迁移后的服务契约已经可用。

迁移要求：

1. 不允许 AWAKE 与旧 Marcus 同时作为两个 Provider/Storage/IPC 权威运行。
2. 新服务必须有独立服务身份、协议版本、存储根和日志根；旧服务检测到时要给出明确冲突状态，而不是静默双轨。
3. 游戏退出、战役切换、服务重启和管道断开必须保留 typed degraded 状态；不能把“Companion connected”当成 Provider 已健康。
4. 启动入口、停止入口、父进程关系和异常退出原因必须在 P1.5/Phase 2 形成可测试契约。

## 6. 迁移边界结论

### 必须保留

- AWAKE 的三项 Native 存档键。
- AWAKE 的 MCM 玩家行为设置与旧值读取能力。
- 现有 12 个 Storage namespace 的逻辑 owner、key、schema 和幂等语义。
- `campaign_id` / `timeline_id` / `session_id` 的隔离语义。
- 世界书 Overlay/Activation 与 Framework Storage 的分工。

### 必须重做或迁移

- `AWAKE.csproj` 对旧 reference DLL 的引用。
- `SubModule.xml` 对外部 `MarcusAIFramework` 的依赖和加载顺序。
- `MarcusAIFramework.Api` 的公共命名空间与程序集身份。
- Companion/IPC/Provider 配置入口，使玩家只接触 AWAKE MCM。
- Framework/Service 的安装、版本、检测、冲突和降级机制。

### 禁止直接迁移

- 不复制 `credentials.dpapi`、API Key、Authorization header 或数据库秘密内容。
- 不把旧 profiles 示例当作实际玩家配置导入。
- 不把四个 Marcus 玩法扩展并入 Framework Core。
- 不把旧 Companion 管理页继续当作玩家必经入口。
- 不在本阶段修改冻结 AWAKE 候选、dist 或游戏目录。

## 7. P1.5 前置问题

P1.5 必须在代码迁移前锁定以下契约：

1. `MarcusAwakeFramework.dll` 是否作为 AWAKE 单模块中的非独立 Bannerlord SubModule 程序集加载。
2. AWAKE 与 `MarcusAwakeFramework` 的宿主注册、公共 API 和版本协商边界。
3. 新 Service 的进程身份、配置协议、Provider profile DTO、凭据引用和脱敏错误模型。
4. 旧 SaveData/Storage 的迁移策略：原 namespace 是否原样接管、是否增加 owner/schema epoch、如何处理旧数据库缺失。
5. Companion 旧进程/旧模块检测、双轨冲突处理和回滚方案。
6. `KnowledgeService` 旧 RAG 路径与 Worldbook v2 路径的最终分层。

## 8. 证据等级与限制

- 当前盘点达到 `E0`：静态文件、配置、目录、版本、日志和调用链核对。
- 未构建、未修改运行时代码、未同步 dist/游戏目录、未启动游戏。
- 未读取 API Key、DPAPI 内容、数据库正文或完整 Provider 请求。
- 当前游戏目录是旧外部框架部署，不是新内置迁移验证环境。
