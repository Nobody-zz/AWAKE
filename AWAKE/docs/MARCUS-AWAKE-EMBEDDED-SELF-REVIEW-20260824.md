# Marcus-Awake 内置迁移自审报告

- 日期：2026-08-24
- 任务：`MARCUS-AWAKE-EMBEDDED-DISCOVERY-20260824`
- 状态：`SELF_REVIEW_COMPLETE_NEEDS_EXTERNAL_REVIEW`
- 范围：功能拆分、玩家可视边界、游戏 Runtime、AI Service、DevTools、SDK、配置/存档/版本/发布
- 结论：方案方向正确，但有 7 项必须在实现前修正；本报告不授权代码迁移

## 1. 总体判断

### 通过项

- 已将原 Marcus 拆成游戏内框架核心、后台 AI Service、AWAKE MCM、DevTools、SDK/示例扩展。
- 已确认四个原 Marcus 扩展不是框架核心，不能直接并入 AWAKE。
- 已确认玩家看到的是游戏功能和必要配置，不应看到 IPC、Route、Capability、Prompt、数据库等技术细节。
- 已确认 AWAKE MCM 是玩家 API URL、API Key、模型拉取和联通测试的主要入口。
- 已确认 AI Service 不应提供玩家必经的配置界面，DevTools 不应成为玩家运行依赖。
- 已确认 AI 输出不能绕过代码权限、数值、事件和命令结算。

### 必须修正项

1. `MarcusAwakeFramework.dll` 是内置程序集，不应被当成第二个 Bannerlord 模组依赖。
2. MCM 是玩家配置界面，但 API Key 的持久化权威不能简单等同于 MCM 自动序列化。
3. AI Service 的启动/退出/多实例治理必须独立设计，不能含糊地称为“随包运行”。
4. 外部 `MarcusAIFramework` 与旧服务存在时，必须有冲突检测和单一运行策略。
5. 旧存档、旧 Storage、旧配置和旧 Companion 数据不能等到迁移后再补。
6. 框架 API、IPC 协议、Service 版本、Provider 配置和 Worldbook schema 必须分开版本化。
7. Phase 0 必须先建立真实源码清单；当前本地参考目录并不等同于公开仓库完整核心源码。

## 2. 关键修正

### 2.1 内置程序集不等于 Bannerlord 模组

推荐结构：

```text
Modules/AWAKE/
├─ SubModule.xml              # 只声明 AWAKE 及真实游戏前置
├─ bin/Win64_Shipping_Client/
│  ├─ Awake.dll
│  └─ MarcusAwakeFramework.dll
└─ RuntimeService/             # 独立后台进程及其依赖
```

`MarcusAwakeFramework.dll` 由 `Awake.dll` 引用，但不新增一个伪造的 `<DependedModule>`。否则会产生：

- 模组加载顺序与程序集加载顺序混淆；
- 玩家看到额外的伪模组；
- 两份 `SubModule.xml` 或重复初始化；
- 外部扩展误以为它是旧 Marcus 的兼容替代。

只有当框架确实需要独立 Bannerlord 生命周期入口时，才另设真实模块；当前规划不默认这样做。

### 2.2 MCM 界面与配置权威分开

玩家操作必须在 MCM，但这不意味着 MCM 的隐式序列化就是安全存储：

```text
MCM 输入层
  → AWAKE 配置服务
  → 本地安全存储/脱敏状态
  → AI Service 运行配置
```

MCM 负责：

- 输入、显示/隐藏、校验、按钮和状态；
- 调用模型拉取和测试连接；
- 展示脱敏结果。

AWAKE 配置服务负责：

- 规范化 URL；
- 去除危险换行和多余空格；
- 校验 Provider 类型；
- 保存版本化配置；
- 脱敏日志和导出；
- 向 AI Service 提交配置。

API Key 的存储方案仍需在实现前确定为受保护的本地存储机制；不能默认写入公开的 `Config.json`，也不能让 Service 反过来成为玩家配置界面。

### 2.3 AI Service 是后台运行服务，不是启动器和 DevTools

需要明确三个不同职责：

```text
AWAKE Launcher/Bootstrap
  └─ 启动、检查、停止 AI Service

AI Service
  └─ 处理 AI 请求、Provider、IPC、后台数据能力

DevTools
  └─ 调试、分析、导出、维护
```

AI Service 不应负责：

- 玩家配置向导；
- 玩家首次安装引导；
- 世界书编辑；
- 直接展示完整日志；
- 让玩家选择内部 Route；
- 让玩家编辑 JSON/数据库。

### 2.4 外部 Marcus 冲突策略

第一阶段不做旧 API 运行时兼容，也不做内置/外部双轨，但必须处理旧安装：

- 检测外部 `MarcusAIFramework` 模组是否启用；
- 检测旧 AI Service 是否已监听或运行；
- 不自动删除、不静默覆盖；
- AWAKE MCM 显示“检测到外部 Marcus，可能存在双服务冲突”；
- AWAKE 使用自己的 `MarcusAwakeFramework` 程序集和独立 IPC/配置标识；
- 若冲突会影响安全运行，则 AWAKE AI 功能 fail closed，而不是随机选择服务。

是否强制阻止需要后续实机验证，但“无检测直接运行”不通过。

### 2.5 存档与配置迁移必须前置

必须分别盘点：

- AWAKE 现有 SaveDefiner、Save key、Storage namespace；
- 旧 Marcus framework 的 timeline/campaign anchor；
- Service 的 profile/config 数据；
- Provider 配置和模型选择；
- 旧 Companion 数据库、RAG 索引和日志；
- `MarcusAIFramework.Api` owner/route/permission 字符串。

原则：

- AWAKE 现有存档业务状态不能因为框架程序集改名而丢失；
- 旧 Marcus 的外部存档数据不自动假设可迁移；
- 不能把配置迁移和战役存档迁移混成一件事；
- 每种迁移都要有版本、失败回退和可观察结果；
- 不迁移的数据必须明确告知，而不是静默忽略。

### 2.6 版本轴必须分开

至少保留：

```text
AWAKE Mod version
Marcus-Awake Framework API version
AI Service protocol version
AI Service implementation version
Provider profile schema version
Storage schema version
Worldbook schema version
Bannerlord API version
SDK version
```

兼容判断应由代码完成，不允许只比较一个总版本号。

### 2.7 源码来源必须冻结

本地 `MarcusAIFramework_Reference` 包含 SDK、文档、预览构建和 AuthorSource 扩展，但不能直接证明它就是公开仓库当前完整核心源码。

Phase 0 必须：

- 记录公开仓库 commit；
- 记录本地参考快照来源；
- 生成文件级清单和 SHA-256；
- 标记上游文件、AWAKE 修改文件、全新重写文件；
- 保留 Apache-2.0 LICENSE 和 NOTICE；
- 建立第三方依赖清单；
- 记录公开仓库与本地参考快照的差异。

## 3. 玩家可视边界自审

### 必须保留在游戏内

- AI 配置向导和配置字段；
- 模型拉取、选择、测试；
- AI 状态和可操作错误；
- NPC 对话和取消/超时反馈；
- 世界周报、事件档案、知识档案馆；
- 玩家传授/编辑世界知识；
- 必要的权限确认和高风险确认；
- 脱敏诊断包导出。

### 适合游戏内只读展示

- 当前 NPC 知道什么、不能知道什么；
- 事件的来源、时间、传播范围、可信度；
- 当前模型和脱敏连接状态；
- 玩家 Overlay 修改记录；
- AI 失败的自然语言解释。

### 必须移出游戏内

- Prompt 原文；
- Route/Capability/Permission ID；
- Token 和 Provider 原始请求；
- IPC/端口/队列/线程；
- 数据库/RAG/Timeline/CAS 细节；
- 原始堆栈和完整日志；
- 内部身份 ID 和世界书条目 ID；
- SDK、源码、测试 fixture。

## 4. 迁移顺序修正

原计划的阶段方向保留，但顺序增加两个前置：

```text
P0-Source Freeze
  ↓
P0.5-Existing Data/Config Inventory
  ↓
P1-Framework Core Extraction
  ↓
P1.5-API/Protocol/Assembly Contract
  ↓
P2-AI Service Extraction
  ↓
P3-AWAKE MCM Configuration
  ↓
P4-DevTools Separation
  ↓
P5-Conflict Detection and Migration UX
  ↓
P6-AWAKE Integration and Regression
```

不能先改命名空间，再去想存档；不能先做 MCM，再临时定义 Service 协议；不能先复制 DLL，再补许可证和来源。

## 5. 本阶段禁止事项

- 不直接修改 `AWAKE.csproj`；
- 不直接删除 `MarcusAIFramework` 依赖；
- 不复制公开仓库 main 分支到游戏目录；
- 不把四个示例玩法扩展并入框架核心；
- 不把 API Key 写入日志或普通诊断包；
- 不创建第二个伪 Bannerlord 模组来承载框架程序集；
- 不启动游戏、不同步 `dist`、不覆盖冻结候选；
- 不把旧 Companion 管理页继续当作玩家必经入口。

## 6. 自审结论

- 功能拆分：`PASS_WITH_CORRECTIONS`
- 玩家可视边界：`PASS`
- 进程边界：`REVISE_REQUIRED`
- 配置安全：`REVISE_REQUIRED`
- 程序集/模组加载：`REVISE_REQUIRED`
- 存档与配置迁移：`REVISE_REQUIRED`
- 版本与 SDK：`REVISE_REQUIRED`
- 许可证/来源：`REVISE_REQUIRED`

总体结论：

> 迁移方向成立，但当前不能直接进入代码搬运。必须先完成 P0 Source Freeze、P0.5 Data/Config Inventory 和 P1.5 API/Protocol/Assembly Contract。

下一步应是建立 Phase 0 只读清单和证据报告，而不是改代码。
