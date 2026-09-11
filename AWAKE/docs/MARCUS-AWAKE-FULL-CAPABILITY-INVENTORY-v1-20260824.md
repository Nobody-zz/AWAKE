# Marcus-Awake 全功能继承清单 v1

- 日期：2026-08-24
- 状态：`P1.5_FULL_CAPABILITY_BASELINE`
- 任务：把 Marcus 的全部框架能力纳入 AWAKE 的设计考虑，并为每项能力指定归属、继承方式、阶段和证据要求。
- 依据：本地 `MarcusAIFramework_Reference` 的设计大纲 00–18、`IMPLEMENTATION_STATUS.md`、SDK 预览、AuthorSource 四个参考扩展，以及 AWAKE 当前工程。

## 1. 这份清单解决什么问题

“全部加入考虑”不等于第一版把所有功能同时开放给玩家，也不等于把四个参考扩展原样塞进框架 DLL。这里的定义是：

1. 每个 Marcus 能力都有明确的 AWAKE 归属。
2. 每个能力都明确是原样继承、契约继承、AWAKE 重构、后续能力，还是仅作为参考扩展行为。
3. 每个能力都有对应的版本轴、失败降级和验证证据。
4. 任何暂不开放的能力都不能因为“以后再说”而从架构、协议和存储中消失。

因此，`A2` 表示“后续实现”，不是“放弃”；“不进入 Framework Core”表示“放到 AWAKE 适配层、Runtime Service、DevTools 或独立内容包”，不是“不考虑”。

## 2. 证据等级

| 证据 | 含义 |
| --- | --- |
| `source` | AuthorSource 或参考实现中存在可检查的源代码。 |
| `contract-tested` | 参考框架已有可重复的协议/契约测试，但不代表 Bannerlord 实机已通过。 |
| `static` | 文档、API、Schema、构建或包检查证据。 |
| `game-pending` | 需要在目标 Bannerlord 版本中启动、开局、读档或 UI 实测。 |
| `provider-pending` | 需要真实 Provider、ComfyUI、Player2、TTS 或 GGUF 环境验证。 |

## 2.1 参考文档覆盖

以下 Marcus 设计文档全部属于本次盘点的证据范围；表格中的能力条目负责把它们落实到 AWAKE 的归属和阶段，而不是只保留文档链接。

| 参考文档 | 覆盖主题 |
| --- | --- |
| `00_product_contract.md` | 产品目标、玩家/开发者动作、平台边界、假设账本和最小纵向验证。 |
| `01_identity_dependencies_versions.md` | 稳定身份、依赖、四条版本轴和发布身份。 |
| `02_runtime_architecture.md` | 两进程边界、线程模型、状态权威和故障收缩。 |
| `03_extension_sdk_capability_broker.md` | Extension、Manifest、Capability、权限交集和 SDK 发现。 |
| `04_game_data_query_encyclopedia.md` | 游戏数据、稳定实体、可见性、快照和百科索引。 |
| `05_events_storage_timeline.md` | Runtime/Durable Event、Storage、Save Anchor、Timeline、导入导出。 |
| `06_command_authority.md` | R0–R3 命令、预演、授权、复核、幂等和收据。 |
| `07_ai_gateway_provider_routing.md` | Gateway、Connection/Model/Route、六类 Adapter、路由和配额。 |
| `08_context_rag_tools_outputs.md` | Context、预算、Prompt、RAG、工具候选、事实引用和结构化输出。 |
| `09_media_comfyui_tts_assets.md` | ComfyUI、生图、TTS、VoiceProfile、CAS、资产验证和保留。 |
| `10_ipc_companion_security.md` | Named Pipe、Service、Loopback、凭据、插件边界和协议安全。 |
| `11_ui_configuration_localization.md` | MCM、Gauntlet、管理页、配置权威、首次使用和本地化。 |
| `12_dual_version_compatibility.md` | v1.4.8/v1.3.15 构建、引用、包、存档和验证隔离。 |
| `13_api_contract_catalog.md` | 公共 API、Schema、错误、事件、命令、媒体和生命周期契约目录。 |
| `14_reference_extension_developer_experience.md` | 参考扩展、SDK、FakeHost、测试工具、Analyzer 和开发文档。 |
| `15_security_performance_observability.md` | 威胁、权限、主线程预算、背压、日志、指标、隐私和熔断。 |
| `16_roadmap_scope_validation.md` | 分阶段路线、停止条件、纵向切片和证据门。 |
| `17_reference_evidence_register.md` | 参考实现、构建、协议和未验证项的证据登记。 |
| `18_adr_open_questions.md` | 已接受 ADR、开放问题、证据决定和变更规则。 |

## 3. 全功能归属矩阵

### 3.1 身份、生命周期与开发者扩展平台

| ID | Marcus 能力 | 原框架功能范围 | AWAKE 处理 | 阶段 | 原参考证据 |
| --- | --- | --- | --- | --- | --- |
| F-001 | 稳定身份与版本轴 | ModId、程序集、API、协议、Schema、Bannerlord 版本隔离 | 迁移为 `MarcusAwakeFramework` 身份；保留 Mod、API、Protocol、Provider、Storage、Worldbook、SDK 分轴 | P1.5 | `01_identity_dependencies_versions.md` `IMPLEMENTATION_STATUS.md` |
| F-002 | Host / Extension 生命周期 | 注册、发现、`CampaignSessionReady`、`SessionEnding`、卸载隔离 | 原样继承契约；AWAKE 玩法只在安全生命周期接入 | P2 | `02_runtime_architecture.md`、SDK、ReferenceExtension |
| F-003 | Extension Manifest | ExtensionId、API 范围、Bannerlord API、权限、Route、Capability | 保留；所有 AWAKE 内容包、未来第三方世界观都必须声明清单 | P2 | `03_extension_sdk_capability_broker.md` |
| F-004 | Capability Broker | query、event、command、context、ai-task、tool、storage-view、ui-panel、provider、asset-transform | 原样继承能力分类；AWAKE 只公开最小权限和稳定 Schema | P2 | `03_extension_sdk_capability_broker.md` |
| F-005 | 依赖/循环/能力发现 | capability URI、版本协商、缺失/冲突/循环依赖处理 | 保留；缺失时得到可解释的 unavailable，不静默降级成错误语义 | P2 | `03_extension_sdk_capability_broker.md` |
| F-006 | SDK 开发体验 | reference DLL、XML 文档、Schemas、模板、双语资源 | 迁移为独立 `Marcus-Awake SDK`，不进入玩家包 | P5 | `14_reference_extension_developer_experience.md` |
| F-007 | FakeHost / Test Kit | DTO 快照、事件回放、权限矩阵、流、存储、命令、虚拟时钟、取消 | 保留并增加 AWAKE 世界知识/Persona/事件 fixture | P5 | `14_reference_extension_developer_experience.md` |
| F-008 | Analyzer / Linter | 阻塞调用、裸权限、无命名空间、泄漏内部类型、缺幂等策略等检查 | 保留；加入 AWAKE 世界书包、Provider 外发和 NPC 知识权限检查 | P5 | `14_reference_extension_developer_experience.md` |

### 3.2 游戏数据、上下文与知识检索

| ID | Marcus 能力 | 原框架功能范围 | AWAKE 处理 | 阶段 | 原参考证据 |
| --- | --- | --- | --- | --- | --- |
| F-009 | GameDataQuery | PublicCatalog、CampaignSnapshot、ExtensionDataProvider | 原样继承 DTO/分页/快照边界；不让 AI 或 Service 持有 TaleWorlds 实时对象 | P2 | `04_game_data_query_encyclopedia.md` |
| F-010 | EntityRef 与稳定身份 | Hero、Clan、Kingdom、Settlement、Party 等稳定引用 | 作为 NPC、家族、世界知识和事件关联的唯一基础；不使用显示名、AgentIndex 或裸坐标 | P2 | `04_game_data_query_encyclopedia.md` |
| F-011 | 数据可见性 Scope | PublicCatalog、PlayerKnown、ObservedHistory、FullSimulation、SensitiveExtension | AWAKE 默认只允许代码生成的 PlayerKnown/ObservedHistory；隐藏事实必须显式授权 | P2 | `04_game_data_query_encyclopedia.md` |
| F-012 | Snapshot / 一致性 | 快照令牌、过期、分页、主线程读取与后台 DTO | 作为每次 NPC 对话、命令、周报和知识更新的事实快照 | P2 | `04_game_data_query_encyclopedia.md` |
| F-013 | Context Provider / Planner | 来源、scope、TTL、排除原因、token/字节预算、贡献排序 | 增加 NPC 身份、阶层、地域、专业、关系、世界书权限、当前场景和事件来源 | P2 | `08_context_rag_tools_outputs.md` |
| F-014 | Prompt Context 解释 | 让调用者知道哪些上下文进入/被排除以及原因 | DevTools 可见；玩家只看到“NPC 为什么知道/不知道”的自然语言解释 | P2 | `08_context_rag_tools_outputs.md` |
| F-015 | SQLite FTS5 RAG | collection、scope、fingerprint、bounded chunks、原子替换 | 用于世界书档案、周报、事件、对话摘要和玩家 Overlay；绝不整本世界书注入 Prompt | P3 | `08_context_rag_tools_outputs.md`、`IMPLEMENTATION_STATUS.md` |
| F-016 | Embedding / Rerank | 向量/关键词混合检索与重排能力 | 作为可选检索层；代码先筛身份权限，再做有界检索 | P3/A2 | `IMPLEMENTATION_STATUS.md`、`08_context_rag_tools_outputs.md` |
| F-017 | Prompt Registry | 版本化模板、占位符、Schema、工具 allowlist、owner | AWAKE/内容包维护 Prompt；玩家不编辑原始 Prompt | P3 | `07_ai_gateway_provider_routing.md`、`08_context_rag_tools_outputs.md` |

### 3.3 AI Gateway、Provider 与任务治理

| ID | Marcus 能力 | 原框架功能范围 | AWAKE 处理 | 阶段 | 原参考证据 |
| --- | --- | --- | --- | --- | --- |
| F-018 | AI Gateway | 统一逻辑任务入口、Route 与 Provider 解耦 | NPC 对话、周报、事件叙述、世界书开发辅助统一走 Gateway | P3 | `07_ai_gateway_provider_routing.md` |
| F-019 | Connection / Model / Route | 连接、模型能力、逻辑用途三层分离 | MCM 只呈现易懂的连接/模型配置；Route 留在内部策略 | P3/P4 | `07_ai_gateway_provider_routing.md`、`11_ui_configuration_localization.md` |
| F-020 | Provider Capability Report | 模态、工具、流式、结构化输出、Embedding、Rerank、图像、TTS 等能力探测 | Route 只在能力等价时 fallback；禁止把“能响应”当成“能力相同” | P3 | `07_ai_gateway_provider_routing.md` |
| F-021 | OpenAI-compatible Adapter | 文本、工具、Embedding、兼容图像/TTS 端点 | 保留为默认云端/兼容 Provider 适配器 | P3 | `07_ai_gateway_provider_routing.md` |
| F-022 | Anthropic Adapter | Messages、流式、工具转换 | 保留；请求差异隔离在 Adapter | P3 | `07_ai_gateway_provider_routing.md` |
| F-023 | Ollama Adapter | 本地/局域模型、模型列表与文本任务 | 保留；视为本地服务，不等同 Managed GGUF | P3 | `07_ai_gateway_provider_routing.md` |
| F-024 | Player2 Adapter | 本地 AI、图像、语音能力 | 纳入适配规划；真实协议未核验前只返回 `unavailable` | P3/A2 | `07_ai_gateway_provider_routing.md` |
| F-025 | ComfyUI Adapter | Workflow Descriptor、输入/输出白名单、进度、取消、输出校验 | 纳入媒体路线；不把任意 Workflow JSON 暴露给游戏侧 | P6 | `09_media_comfyui_tts_assets.md` |
| F-026 | Managed GGUF Adapter | Companion 管理的模型文件、runtime、内存和许可 | 纳入本地 Provider 规划；不把推理 runtime 塞回游戏进程 | P3/A2 | `07_ai_gateway_provider_routing.md` |
| F-027 | 流式与取消 | TextDelta、Completed、RouteChanged、ResolvedModel、deadline、取消 | NPC 对话、模型拉取、连接测试统一使用状态机；半截文本不算最终结果 | P3 | `07_ai_gateway_provider_routing.md` |
| F-028 | 重试、fallback、pinning | 有界重试、模型固定、路由切换与幂等 | 错误时保留任务语义；降级必须可观察、不能静默换任务类型 | P3 | `07_ai_gateway_provider_routing.md` |
| F-029 | 配额与公平性 | 扩展/Route 并发、Token、媒体、磁盘和队列配额 | 为 NPC、周报、世界书 AI 辅助设预算；高频 NPC 不主动学习 | P3 | `07_ai_gateway_provider_routing.md`、`15_security_performance_observability.md` |
| F-030 | Structured Output | Schema、版本、字段边界、原文留存 | NPC reply/effects、KnowledgePatch、WeeklyReport、事件候选必须先验证再结算 | P3 | `08_context_rag_tools_outputs.md` |
| F-031 | Tool Candidate 循环 | 模型提出工具候选，框架验证 owner、轮次、权限、风险和参数 | AI 只能提出候选；AWAKE 代码决定是否执行 | P3 | `08_context_rag_tools_outputs.md`、`06_command_authority.md` |

### 3.4 事件、存储、时间线与命令权威

| ID | Marcus 能力 | 原框架功能范围 | AWAKE 处理 | 阶段 | 原参考证据 |
| --- | --- | --- | --- | --- | --- |
| F-032 | Runtime Event Bus | 即时事件、订阅、生命周期和低延迟通知 | 驱动 NPC 当前上下文、UI 状态和非持久提示 | P2 | `05_events_storage_timeline.md` |
| F-033 | Durable Event Stream | 持久事件、cursor、重放、幂等、背压和 gap | 驱动周报、世界事件和知识批量更新；事件缺口必须可见 | P3 | `05_events_storage_timeline.md` |
| F-034 | Durable Spool / Backpressure | 高价值事件落盘，低价值事件采样/聚合/丢弃并计数 | 不让事件高峰拖垮游戏主线程或无限增长 | P3 | `05_events_storage_timeline.md`、`15_security_performance_observability.md` |
| F-035 | platform.db | 全局 Provider、Route、权限、扩展注册和公开视图 | 迁移为 Marcus-Awake 物理根；不复用旧 Marcus DB | P3 | `05_events_storage_timeline.md` |
| F-036 | campaign.db | 战役级 Storage、审计、事件与 timeline | 与 Bannerlord 存档锚点绑定；旧 AWAKE namespace/key/schema 保持语义 | P3 | `05_events_storage_timeline.md` |
| F-037 | Managed KV / Sidecar / Read View | 托管键值、扩展私有 sidecar、共享只读 SQL 视图 | AWAKE 玩法数据与世界书索引分开；禁止扩展写共享库 | P3 | `05_events_storage_timeline.md` |
| F-038 | Save Anchor | 战役/时间线/会话隔离、读档恢复、非序列化引用重建 | 接入现有 AWAKE SaveDefiner 和 `awake_*` 存档键 | P2/P3 | `05_events_storage_timeline.md` |
| F-039 | Timeline Export/Import/Fork | Snapshot ZIP、hash、资产重映射、血缘、未来序列隔离 | 支持开发者诊断与玩家 Overlay/知识包复用；禁止旧未来数据污染新分支 | P3/P5 | `05_events_storage_timeline.md` |
| F-040 | R0-R3 Command Authority | 查询、低风险准备、中风险有界修改、战略/不可逆操作 | 保留完整风险门；AI 永不直接写关系、战争、财产或知识权限 | P2/P3 | `06_command_authority.md` |
| F-041 | Preflight / Revalidation / Receipt | 预演、快照复核、幂等键、过期、取消、执行收据和 emergency stop | 每个 AWAKE 效果必须有可追踪 receipt | P2/P3 | `06_command_authority.md` |

### 3.5 IPC、服务管理、凭据与安全边界

| ID | Marcus 能力 | 原框架功能范围 | AWAKE 处理 | 阶段 | 原参考证据 |
| --- | --- | --- | --- | --- | --- |
| F-042 | Named Pipe 协议 | 握手、协议协商、消息 envelope、checksum、请求/流/取消、断线 | 迁移为 AWAKE 独立 pipe/service 标识；单服务、单权威 | P2/P3 | `10_ipc_companion_security.md`、`IMPLEMENTATION_STATUS.md` |
| F-043 | Session Invalidation | 战役切换、服务重启、断线和晚到结果隔离 | 旧 session 的结果只能进入诊断，不能更新当前 NPC/UI/存档 | P2/P3 | `10_ipc_companion_security.md` |
| F-044 | Companion/Service Process Management | 启动、停止、健康检查、多实例治理、优雅退出 | 重命名为 Marcus-Awake AI Runtime Service；Launcher/Bootstrap 与 DevTools 分离 | P3/P5 | `10_ipc_companion_security.md` |
| F-045 | Management HTTP | loopback、Bearer、Host/Origin、CSRF、CSP、no-store、事务性 Profile Apply | 保留给 DevTools/维护，不成为玩家必经入口 | P5 | `IMPLEMENTATION_STATUS.md`、`10_ipc_companion_security.md` |
| F-046 | Credential Reference | Key 不进子 Mod、DB、存档、日志或导出；OS 保护存储 | MCM 输入可见，保存后只显示脱敏状态；Service 只接收引用 | P3/P4 | `10_ipc_companion_security.md` |
| F-047 | Cloud Export Policy | 字段分类、Route policy、玩家策略、最终请求门 | 默认拒绝；玩家明确开启后仍取最小权限交集 | P3/P4 | `07_ai_gateway_provider_routing.md`、`15_security_performance_observability.md` |
| F-048 | 离线与故障收缩 | Service 缺失、Provider 失败、DB 缺失、spool 满、协议不兼容 | 本地查询/原版游戏继续；AI/媒体/高风险自动化按能力关闭并说明原因 | P2/P3 | `02_runtime_architecture.md` |

### 3.6 媒体、资产与内容生态

| ID | Marcus 能力 | 原框架功能范围 | AWAKE 处理 | 阶段 | 原参考证据 |
| --- | --- | --- | --- | --- | --- |
| F-049 | AssetHandle / CAS | 内容寻址、去重、metadata、所有权、引用、保留、导出、清理 | 继承 opaque handle；先服务 NPC 头像、世界事件附件和内容包资源 | P3/A2 | `09_media_comfyui_tts_assets.md` |
| F-050 | Image Generation | OpenAI-compatible、Player2、ComfyUI 和未来 Provider 生图/编辑 | 纳入路线但不是首批玩家必需；输出必须验证后才能给游戏消费 | P6/A2 | `09_media_comfyui_tts_assets.md` |
| F-051 | TTS / VoiceProfile | 语言、音色、速度、分块流、字幕、fallback、许可 | 纳入路线；NPC/周报使用时由 AWAKE 决定时机，框架不自动播放 | P6/A2 | `09_media_comfyui_tts_assets.md` |
| F-052 | Workflow / Asset Transform | ComfyUI WorkflowDescriptor、图像转换、验证与格式约束 | 作为受 Schema 约束的 Service 能力；不传任意路径、脚本或节点参数 | P6 | `03_extension_sdk_capability_broker.md`、`09_media_comfyui_tts_assets.md` |
| F-053 | Retention / Quarantine / Quota | ephemeral/session/campaign/persistent/pinned、损坏隔离、配额清理 | 按战役/内容包/玩家导出策略管理，不让媒体污染存档事实 | P6 | `09_media_comfyui_tts_assets.md` |

### 3.7 UI、配置、本地化、观测与发布

| ID | Marcus 能力 | 原框架功能范围 | AWAKE 处理 | 阶段 | 原参考证据 |
| --- | --- | --- | --- | --- | --- |
| F-054 | MCM 快速配置 | Provider、URL、Key、模型拉取/选择、连接测试、保存应用、总开关 | 玩家唯一主配置入口；中文优先、API Key 输入可见、网络异步 | P4 | `11_ui_configuration_localization.md` |
| F-055 | Gauntlet 诊断/控制台 | 扩展、权限、任务、命令、数据、资产、健康、审计面板 | 只保留必要的玩家状态/高风险确认；技术细节移 DevTools | P4/P5 | `IMPLEMENTATION_STATUS.md`、`11_ui_configuration_localization.md` |
| F-056 | DevTools 管理与导出 | 原始日志、Prompt、Route、Token、Provider、RAG、Storage、Timeline、世界书检查 | 独立作者工具；导出脱敏诊断包，不成为游戏运行依赖 | P5 | `14_reference_extension_developer_experience.md` |
| F-057 | Observability | owner、correlation、route、model、latency、retry、health、metrics、trace | 玩家看到自然语言状态，作者看到分层诊断；关键链路可关联 | P2/P5 | `15_security_performance_observability.md` |
| F-058 | 性能/背压/熔断 | 主线程预算、并发、队列、Token、媒体磁盘、熔断和恢复 | 高频 tick 代码主导；AI 仅处理有限任务；断线/限流不拖死游戏 | P2/P3 | `15_security_performance_observability.md` |
| F-059 | CN/EN 本地化 | 双语 UI、错误码、按钮、文档和占位符一致性 | 玩家面中文优先，内部 ID 不直接暴露；DevTools 保留英文稳定标识 | P4/P5 | `11_ui_configuration_localization.md` |
| F-060 | 包、许可证与来源 | Apache-2.0、NOTICE、第三方清单、包 allowlist、哈希与保护检查 | 内置迁移保留来源和修改说明；玩家包不含 SDK、测试 fixture、无关 DLL | P0/P7 | `IMPLEMENTATION_STATUS.md`、`17_reference_evidence_register.md` |
| F-061 | 双版本兼容 | v1.4.8/v1.3.15 独立构建、引用、包和 smoke | 以当前可验证游戏根为准；不能以文档或单一总版本号假设兼容 | P0/P7 | `12_dual_version_compatibility.md` |
| F-062 | 配置/存档/索引迁移 | 版本门、失败回退、旧配置提示、timeline/campaign 隔离 | 外部旧 Marcus 不自动迁移；AWAKE 现有 key/namespace 必须保留语义 | P0.5/P1.5/P7 | `18_adr_open_questions.md`、AWAKE P0.5 inventory |

### 3.8 四个 Marcus 参考扩展的全部功能：纳入考虑，但不并入 Framework Core

这四个项目不是“被忽略”，而是作为 AWAKE 适配层和纵向验收场景处理。它们的业务行为不能未经重构直接进入通用框架。

| ID | 参考扩展能力 | 原参考功能 | AWAKE 继承方式 | 阶段 | 主要证据 |
| --- | --- | --- | --- | --- | --- |
| F-063 | NPC 对话与档案 | 对话发送/取消/流式等待、Profile、Memory、Evidence、WorldBook、Archive、压缩校验、版本差异 | 重构为 AWAKE NPC/世界知识适配；代码先筛权限和上下文，AI 只负责表达 | P6 | `AuthorSource/.../MarcusAINpc` |
| F-064 | 关系系统 | 原版关系投影、Proposal、Receipt、Events、Encyclopedia、配偶观察、成人门控 | 使用 AWAKE 原版关系适配器；不把关系与家族好感混为单一 AI 数值 | P6 | `AuthorSource/.../MarcusAIRelationships` |
| F-065 | 外交分析 | Analyze、Review、Reports、Proposals、Outbox、Audit、会话/版本/冲突校验 | 作为 AWAKE 事件/政治系统的候选能力；命令仍由 R0-R3 治理 | P6/A2 | `AuthorSource/.../MarcusAIDiplomacy` |
| F-066 | 世界事件 | 观察战争/和平/死亡/俘虏/释放/领地变更/玩家战斗、叙述、传播、档案、去重 | 重构为 AWAKE 事件引擎；AI 只能在事实快照范围内生成叙述，KnowledgePatch 由代码结算 | P6 | `AuthorSource/.../MarcusAIWorldEvents` |

## 4. AWAKE 的最终分层

```text
Awake.dll
  └─ AWAKE 玩法、世界书、NPC、关系、事件、MCM、存档入口

MarcusAwakeFramework.dll
  └─ Host、API、Capability、Permission、Context、Command、Event、Save Anchor、IPC Client

Marcus-Awake AI Runtime Service
  └─ Gateway、六类 Adapter、Prompt、Structured Output、RAG、Storage、Timeline、CAS、Media、IPC Server

Marcus-Awake DevTools
  └─ 日志、Token、Prompt、Route、Provider、RAG、Storage、Timeline、世界书诊断与导出

Marcus-Awake SDK
  └─ Reference DLL、Schema、XML docs、模板、FakeHost、Test Kit、Analyzer/Linter
```

## 5. 继承原则

- 框架能力全部纳入设计，不因“第一版暂不开放”而删除协议、失败状态或存储边界。
- 能力契约继承优先于旧命名、旧文件布局和旧 UI；不复制旧 Companion 作为黑盒。
- Provider 适配器必须独立，不能把 OpenAI-compatible 的假设扩散到 Anthropic、Ollama、Player2、ComfyUI 或 GGUF。
- 游戏事实、权限、关系、事件、知识和命令由代码掌权；AI 输出始终经过 Schema、权限、快照、风险和幂等结算。
- 游戏内玩家界面只暴露“配置、操作、结果和失败原因”；开发者诊断才显示 Prompt、Route、Token、RAG、Storage、IPC 和原始日志。
- 所有网络、IPC、文件、数据库、媒体和模型工作都必须异步、有界、可取消，不能进入高频游戏 tick。

## 6. 全功能验收门

后续每一批迁移必须同时填写：

1. 本清单中的能力 ID。
2. 入口、调用、结算/持久化、可观察结果。
3. API、Protocol、Schema、Storage、Provider 和 Bannerlord 版本证据。
4. 正常、禁用、离线、超时、重复提交、版本不兼容和旧数据边界。
5. 尚未实机验证的项目及其剩余风险。

“类存在”“DLL 能编译”“JSON 能解析”均不能单独证明对应能力已完成。
