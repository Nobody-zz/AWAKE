# Marcus-Awake 功能拆分与归属矩阵 v1

- 编制日期：2026-08-24
- 状态：`DISCOVERY_BASELINE`
- 适用项目：`AWAKE`
- 依据：公开仓库 `Alexander-Dieros/MarcusAIFramework`、本地 `MarcusAIFramework_Reference`、AuthorSource 四个扩展和当前 AWAKE 工程
- 本文只做功能归类，不批准代码迁移

## 1. 先纠正一个名称问题

原 Marcus 体系中并不存在一个单独、边界清晰的“Companion 功能整体”。实际是多个层次的组合：

```text
Bannerlord 游戏进程
  ├─ Framework Core：游戏内框架核心
  ├─ MCM / Gauntlet：玩家设置与游戏内界面
  └─ Extension：NPC、关系、外交、世界事件等玩法扩展

独立进程
  ├─ AI Runtime Service：Provider、路由、模型、IPC、请求执行
  ├─ Storage/RAG/Timeline/Asset：后台数据能力
  └─ Management/Diagnostics：开发者管理、诊断与导出

开发资源
  ├─ Public API / SDK reference
  ├─ FakeHost / test-kit / schemas
  ├─ templates / analyzers
  └─ 示例扩展
```

因此，`Companion` 只能作为历史统称，不应继续作为整个系统的产品边界名称。

## 2. 五类功能归属

### A. Marcus-Awake Game Runtime

必须随 AWAKE 一起运行，但以独立程序集存在：

- Extension manifest、注册和生命周期；
- Capability Broker；
- Permission Catalog / Permission Gate；
- RequestContext、错误、相关性 ID、deadline、取消；
- GameData / PlayerKnown / Context Provider 边界；
- Event Service 的游戏内入口与订阅边界；
- Command Descriptor、Preflight、Execute、Receipt、幂等和风险门；
- 游戏内 Storage anchor / SaveDefiner 接线；
- UI 注册与 MCM/Gauntlet 生命周期桥；
- Timeline/campaign 身份绑定的游戏侧适配；
- 与后台 AI Runtime 的 IPC 客户端；
- AWAKE 自己需要的诊断状态投影。

**结论：**这是“框架内核”，不是玩家工具，也不是开发者工具。

### B. Marcus-Awake AI Runtime Service

这是独立后台进程，不向玩家暴露技术操作界面：

- AI Gateway；
- Connection / Model / Route 管理；
- OpenAI-compatible、Anthropic、Ollama 等 Provider 适配；
- 请求排队、流式、超时、重试、取消、fallback/pinning；
- Prompt registry 与结构化输出校验；
- Embedding / Rerank；
- 云端外发分类与最终请求门；
- IPC 服务端、握手、认证、消息帧、校验和；
- RAG / SQLite / FTS5；
- campaign storage、sidecar、timeline、fork/import/export；
- 资产存储、图片、TTS、VoiceProfile；
- 后台服务日志、健康状态和安全脱敏。

**结论：**它是“AI 运行服务”，不叫玩家配置工具，也不叫开发者工具。

### C. AWAKE MCM：玩家 AI 环境配置

这些功能不能藏在后台管理页，必须直接整合到 AWAKE MCM，并以新手可理解的方式呈现：

- Provider 类型/预设选择；
- API URL；
- API Key 明文显示/隐藏切换；
- 拉取模型列表；
- 模型选择；
- 手动输入模型名的回退；
- 测试连接；
- 保存并应用；
- 当前连接状态；
- AI 功能开关和必要的玩家体验预设；
- 简化的错误说明；
- 脱敏诊断包导出入口。

**配置权威：**玩家通过 MCM 操作；配置由 AWAKE 的配置边界提交给 AI Runtime Service 使用。不能让玩家编辑服务端文件，也不能要求玩家理解 IPC、Route、Companion 等术语。

**安全要求：**输入时可见；保存后默认掩码；日志、Prompt、导出包不含 API Key；网络操作异步，不阻塞 Bannerlord 主线程。

### D. Marcus-Awake DevTools：开发者工具

独立于游戏内 MCM，面向作者和维护者：

- 原始服务日志查看与筛选；
- Provider 请求/响应诊断（凭据脱敏）；
- Prompt、Route、Schema 调试；
- Token、延迟、重试、fallback 统计；
- Companion/AI Runtime 健康检查；
- 本地 Worker 管理；
- RAG/索引、Storage、Timeline 诊断；
- 世界书 Studio/Runtime 包检查；
- 日志和诊断包导出；
- 迁移、版本、协议、API 差异检查；
- 开发测试 fixture、回归 smoke、契约测试启动器。

**不放入玩家 MCM：**原始 Prompt、完整请求体、数据库路径、IPC 端口、内部 Route、线程/队列细节、模型 Provider 原始错误、完整 Token 明细。

### E. SDK / 示例扩展 / 参考代码

不属于 AWAKE 玩家运行包：

- `MarcusAwakeFramework.Api` reference assembly；
- XML 文档、Schema、manifest contract；
- FakeHost、test-kit、analyzer；
- 扩展模板和迁移指南；
- 示例扩展源码；
- `MarcusAIDiplomacy`；
- `MarcusAINpc`；
- `MarcusAIRelationships`；
- `MarcusAIWorldEvents`。

这些内容进入独立开发 SDK 或源码仓库，不应因为“内置框架”而全部塞进 AWAKE 游戏模块。

## 3. 四个原有扩展的正确定位

| 原模块 | 实际性质 | 是否属于框架核心 | AWAKE 第一阶段处理 |
|---|---|---:|---|
| `MarcusAINpc` | NPC 对话、记忆、人格玩法扩展 | 否 | AWAKE 自己已有 NPC 路径；只借鉴契约，不作为框架核心迁移 |
| `MarcusAIRelationships` | 关系投影和成人/亲密关系玩法扩展 | 否 | 不并入通用核心；与 AWAKE 关系系统分批对照 |
| `MarcusAIDiplomacy` | 外交分析与候选审阅玩法扩展 | 否 | 作为示例/可选内容，不进入核心运行时 |
| `MarcusAIWorldEvents` | 世界事实、传播和叙述玩法扩展 | 否 | 与 AWAKE 世界事件/周报机制分别核对，不能直接复制 |

原扩展共同依赖框架 API，但它们不是框架本体。迁移时必须避免“把四个玩法 Mod 一并编进 AWAKE”的错误。

## 4. 当前 AWAKE 的特殊修正

原 Marcus 文档把 Companion 管理页视为重要入口，并曾规定 MCM 不保存 API Key。当前 AWAKE 产品决策已改变这一点：

- 玩家 API URL、API Key、拉取模型和联通测试必须在 AWAKE MCM 内完成；
- MCM 是玩家配置界面；
- AI Runtime Service 是后台执行服务；
- DevTools 是开发者调试工具；
- 原“Companion 管理页”不能继续作为玩家必经入口；
- 旧文档中的“API Key 不由 MCM 保存”属于待迁移的历史设计，不是当前 AWAKE 玩家体验权威。

## 5. 推荐最终目录

```text
AWAKE/
├─ src/                         # AWAKE 玩法与世界运行逻辑
├─ framework/
│  └─ MarcusAwakeRuntime/       # 游戏内框架核心，独立程序集源码
├─ service/
│  └─ MarcusAwakeAiRuntime/     # 后台 AI Runtime Service 源码
├─ devtools/
│  └─ MarcusAwakeDevTools/      # 开发者工具，不随玩家核心 UI 暴露
├─ sdk/
│  └─ MarcusAwakeFramework.Sdk/ # reference、schema、FakeHost、test-kit
├─ docs/
├─ tools/
└─ dist/
    └─ Modules/AWAKE/
        ├─ Awake.dll
        ├─ MarcusAwakeFramework.dll
        └─ AI Runtime Service/  # 随包发布的后台服务，不直接面向玩家操作
```

目录名可以在实现阶段按现有仓库约束微调，但职责不能重新混合。

## 6. 不应迁移的内容

- 原作者旧模块名和旧外部依赖声明；
- 四个示例扩展的玩法行为；
- 旧 Companion 管理页作为玩家必经配置入口；
- SDK 示例和测试 fixture 进入游戏玩家包；
- 原始 Provider/API Key/数据库路径暴露给游戏 UI；
- 把后台 AI 服务逻辑塞入 `Awake.dll`；
- 把 AWAKE 世界书、NPC、事件逻辑反向塞进框架核心。
## 7. 游戏内可视层级

核心原则：玩家应看到“我能做什么、发生了什么、为什么失败”，不应看到“框架是怎样实现的”。

### P0：必须让玩家直接接触

这些是 AWAKE 的实际游戏功能或首次运行必需配置：

- AI 首次配置向导；
- API URL、API Key、模型拉取、模型选择、联通测试、保存应用；
- AI 当前状态：未配置、连接中、已连接、失败及下一步建议；
- NPC 对话入口和对话界面；
- AI 生成中的状态、取消、超时和失败提示；
- 世界知识/周报/事件档案的玩家阅读入口；
- 玩家可触发的世界知识编辑、传授或导出入口（按对应功能是否启用）；
- 必须由玩家确认的高风险行为或权限请求；
- 简化的 AI 功能开关和强度/频率预设。

### P1：玩家可选查看，但不应成为日常操作中心

这些内容对理解游戏有价值，但应以“档案/状态/帮助”形式展示：

- 当前 NPC 的已知/未知/转介状态；
- NPC 对某条知识的表达范围，而不是内部 `profile_id`；
- 世界事件的来源、时间、传播范围和可信度；
- 周报的事实摘要、未确认传闻和已知来源；
- AI 配置的脱敏状态、当前模型和最后测试时间；
- 简化诊断包导出；
- 存档/世界知识 Overlay 的导入导出结果；
- “为什么 NPC 不知道/不回答”的玩家可理解解释。

### P2：默认不显示，只在开发者模式或外部 DevTools 查看

- Route ID、Capability URI、Permission ID；
- Prompt 原文、模板版本、结构化 Schema；
- Token 数、Provider DTO、原始 HTTP 错误；
- IPC、Named Pipe、端口、握手、队列、重试和线程状态；
- SQLite 表、RAG 索引、Timeline sequence、CAS hash；
- 真实文件路径、数据库路径、服务进程参数；
- 原始框架日志、完整 AI 请求/响应、调试堆栈；
- 具体 `profile_id`、`identity_id`、内部世界书条目 ID；
- 权限矩阵和框架内部失败码。

### P3：绝不作为玩家可编辑内容

- API Key 日志、Prompt、世界事实源数据；
- 世界书身份授予和否定规则；
- 框架权限策略、风险等级上限、命令 allowlist；
- 存档命名空间、事件幂等键、内部版本和迁移标记；
- Provider 原始凭据、服务端安全配置；
- 框架程序集、源码、SDK 和开发者测试 fixture。

## 8. 推荐游戏内入口结构

```text
AWAKE
├─ AI 设置
│  ├─ 首次配置向导
│  ├─ API 地址
│  ├─ API Key（显示/隐藏）
│  ├─ 拉取模型
│  ├─ 选择模型
│  ├─ 测试连接
│  └─ 保存并应用
├─ AI 功能
│  ├─ NPC 对话
│  ├─ 世界事件/周报
│  ├─ 世界知识档案
│  └─ 玩家传授/编辑（启用时）
├─ 当前世界
│  ├─ 周报
│  ├─ 事件档案
│  ├─ 知识档案馆
│  └─ NPC 知识状态
└─ 帮助与诊断
   ├─ 当前状态说明
   ├─ 常见错误处理
   └─ 导出诊断包
```

其中“AI 设置”是玩家最频繁使用的配置入口；“当前世界”是游戏内容入口；“帮助与诊断”只提供简化解释和导出，不展示技术后台。

## 9. 可视性判断标准

一个功能只有同时满足以下条件，才值得放进游戏内：

1. 玩家需要主动操作它，或它直接改变玩家可感知的游戏结果；
2. 玩家能用自然语言理解它，不需要框架/编程知识；
3. 它有明确的成功、失败或当前状态；
4. 它不会暴露凭据、内部权限或客观知识源的不可编辑边界；
5. 游戏内展示比外部工具更方便，并且不会引入长时间阻塞操作。

否则应进入外部 DevTools、日志导出或开发者文档。
