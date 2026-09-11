# Marcus-Awake Phase 0 源码与依赖清单

- 日期：2026-08-24
- 任务：`MARCUS-AWAKE-EMBEDDED-DISCOVERY-20260824`
- 状态：`offline_verified`
- 性质：只读盘点；未修改工程代码、项目引用、发布包或游戏目录

## 1. 上游公开来源

| 项目 | 值 |
|---|---|
| 仓库 | `https://github.com/Alexander-Dieros/MarcusAIFramework` |
| 分支 | `main` |
| 盘点时 main commit | `c3992fae4f0876fe3b40a3ee7835c9e25fc1aa2d` |
| commit 时间 | `2026-08-24 04:57:23 UTC` |
| commit message | `Add README for Marcus AI Framework v0.1.0` |
| LICENSE | Apache-2.0；GitHub license API 识别为 `apache-2.0` |
| LICENSE blob SHA | `261eeb9e9f8b2b4b0d119366dda99c6fd7d35c64` |
| NOTICE blob SHA | `0e3ace61fd2e97bc88b930236c07e95d37c26528` |
| SubModule.xml blob SHA | `0b8bd1b660523fe471af40926e1cb450b40ee1c4` |
| README.md blob SHA | `513aeca1b1a538ea8d8f47bb24d41be94fe63fe` |

> 上游 main 是会变化的移动目标。本清单只冻结本次发现的 commit；真正迁移时必须再次核对 commit 是否仍为团队批准来源。

## 2. 公开仓库顶层内容

已确认顶层包含：

```text
Directory.Build.props
GUI/
LICENSE
ModuleData/
NOTICE
README.md
README_CN.md
README_EN.txt
SubModule.xml
config/
docs/
sdk/
src/
tests/
```

初步归类：

- `src/`：框架运行时代码候选；需进一步按游戏内 Runtime 与后台 Service 分离。
- `sdk/`：reference、schema、FakeHost、test-kit、模板、分析器等开发资源。
- `tests/`：框架和协议测试，不进入玩家游戏包。
- `GUI/`、`ModuleData/`：需按玩家界面、框架诊断界面和服务资源逐项归类，不能整目录复制。
- `config/`：需单独审查是否含默认配置、开发配置或敏感字段。
- `LICENSE`、`NOTICE`：必须随内置发布包保留。

## 3. 本地参考快照

### AuthorSource

路径：

`_houkai_merge/MarcusAIFramework_Reference/AuthorSource/src_20260813`

排除 `obj/bin/_build_out/artifacts` 后：

- 50 个 `.cs`；
- 4 个 `.csproj`；
- 25 个 `.xml`；
- 4 个 `.md`；
- 4 个 `.txt`；
- 共 87 个纳入盘点的源/契约文件。

四个工程：

| 工程 | 程序集 | 性质 |
|---|---|---|
| `MarcusAIDiplomacy` | `MarcusAIDiplomacy.dll` | 外交分析与候选审阅玩法扩展 |
| `MarcusAINpc` | `MarcusAINpc.dll` | NPC 对话、记忆、人格玩法扩展 |
| `MarcusAIRelationships` | `MarcusAIRelationships.dll` | 关系投影与亲密关系玩法扩展 |
| `MarcusAIWorldEvents` | `MarcusAIWorldEvents.dll` | 世界事实、传播与叙述玩法扩展 |

结论：AuthorSource 四个工程不能当作框架核心源码整体内置。

### SDK_20260815

路径：

`_houkai_merge/MarcusAIFramework_Reference/SDK_20260815`

排除构建目录后：

- 5 个 `.cs`；
- 2 个 `.csproj`；
- 8 个 `.xml`；
- 3 个 `.json`；
- 3 个 `.md`；
- 3 个 `.txt`；
- 共 24 个纳入盘点的 SDK 文件。

结论：SDK 是开发资源，不能进入玩家运行包；迁移后应产出 `MarcusAwakeFramework.Api` 对应 SDK。

## 4. 当前 AWAKE 对旧 API 的依赖面

当前 `AWAKE` 工程中共有 29 个文件直接命中 `MarcusAIFramework` 或旧 Companion 语义，包含：

- `AWAKE.csproj`：旧 reference DLL 和验证目标；
- `SubModule.xml`：外部 `MarcusAIFramework` 依赖及加载顺序；
- `SubModule.cs`：框架入口；
- `AiTaskGateway.cs`：AI Route/Permission 网关；
- `AwakeRuntime.cs`：框架 Host、Session、Storage/生命周期；
- `ProbeExtension.cs`：扩展注册、Context Provider；
- `PermissionCatalog.cs`、`PermissionGate.cs`：权限；
- `NpcDialogueService.cs`、`NpcPromptTemplate.cs`、`PromptRegistrationCoordinator.cs`：NPC AI；
- `WorldStateStore.cs`、`WorldCommandBridge.cs`：Storage/Command；
- `AwakeConfig.cs`、`AwakeMcmActions.cs`：玩家配置和旧设置台入口；
- `AwakeDeveloperReport.cs`、`DeveloperCheckOverlay.cs`：开发诊断入口；
- `AwakeMarcusLinkService.cs`：旧框架连接语义；
- 以及事件、记忆、信件和关系相关调用方。

这些文件不能直接做机械命名空间替换；必须先建立 API/程序集/协议映射表。

## 5. 当前明确的迁移边界

### 进入 Marcus-Awake Framework Runtime

- Host、Extension Registry、Manifest；
- Permission/Capability；
- RequestContext、FrameworkError；
- GameData/Context 边界；
- Event/Command 治理；
- Storage anchor/Timeline 接线；
- IPC Client；
- 运行时健康和诊断状态 DTO。

### 进入 Marcus-Awake AI Runtime Service

- Provider/Connection/Model/Route；
- Prompt/Schema/Output validation；
- IPC Server；
- 请求生命周期、流式、超时、取消、重试；
- RAG/SQLite/Timeline/Assets/Media。

### 进入 AWAKE 玩家界面

- URL、API Key、模型拉取、模型选择、连接测试、保存应用；
- NPC 对话、世界事件、周报、知识档案；
- 简化状态和诊断包导出。

### 进入 DevTools/SDK

- 原始日志、Prompt、Route、Token、Provider 诊断；
- Worker、RAG、Storage、Timeline 调试；
- reference、schema、FakeHost、test-kit、模板和分析器。

### 不进入框架核心

- 四个 AuthorSource 玩法扩展；
- AWAKE 世界书和成人内容；
- AWAKE NPC/关系/事件玩法语义；
- 旧外部模块的兼容双轨。

## 6. Phase 0 尚未完成的只读项

- 公开仓库 `src/` 文件级源码清单尚未导入本地归档；
- 公开仓库与 `MarcusAIFramework_Reference` 的文件级差异尚未生成；
- 旧 Storage/Save/Timeline/Provider 配置数据迁移清单尚未完成；
- AI Service 的实际启动入口、配置协议和进程退出契约尚未完全取证；
- 旧框架运行程序集与公开源码 commit 的逐文件对应尚未证明。

这些是下一阶段继续盘点的内容，不可用“已有本地 SDK”代替。
