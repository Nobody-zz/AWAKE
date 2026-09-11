# Marcus-Awake 核心能力继承矩阵 v2

> 状态：P1.5 全功能基线，供能力边界锁定、owner 映射与对抗审查使用。
> 日期：2026-08-24
> 核心目标：不是把 Marcus 改名后塞进 AWAKE，而是继承其核心能力和突出优势，再按 AWAKE 的 NPC、世界知识、事件、玩家配置和开发者工具需求重构。
> 当前阶段：只做架构与契约盘点，不修改运行时代码。
> 全功能逐项清单：`MARCUS-AWAKE-FULL-CAPABILITY-INVENTORY-v1-20260824.md`。

## 1. 总原则

### 1.1 能力继承，不是代码搬运

Marcus 的先进部分主要是边界、治理、异步、持久化、路由、权限和可观测性。AWAKE 要继承这些**能力契约与不变量**，不要求保留旧模块名、旧玩家 UI、旧文件布局或旧示例玩法。

### 1.2 玩家简单，内部复杂

玩家只需要在 AWAKE MCM 中配置 URL、API Key、模型、测试连接和保存应用；复杂能力仍由 Framework/Service 在内部完成：

```text
AWAKE MCM
  -> MarcusAwakeFramework.Api
  -> RequestContext / Permission / ConfigTransaction
  -> 私有 IPC Session
  -> Marcus-Awake AI Runtime Service
  -> Provider Adapter / Credential Store / Storage / RAG
```

### 1.3 AI 不拥有游戏事实权

- Bannerlord 与 AWAKE 代码负责事实、身份、权限、数值、状态、命令和存档。
- AI 负责语言、有限判断、NPC 表达、叙述和候选建议。
- AI 输出永远是非信任输入；不能直接改变关系、经济、战争、知识权限或世界状态。
- 所有效果必须经过代码侧 allowlist、Preflight、当前快照复核、幂等结算和可观察 receipt。

### 1.4 性能与 Token 原则

- 不把完整世界书、完整 NPC 记忆或完整事件历史注入 Prompt。
- NPC 知识按身份、阶层、地域、时代、可信度和表达层级做代码侧筛选。
- 重大且持久的知识变化通过事件/周报批次统一更新，不对每个 NPC 单独调用 Provider。
- 高频 tick 只做轻量判断；网络、IPC、文件、数据库和 Provider 工作异步完成。

### 1.5 “全部考虑”与“第一版全部开放”的区别

- Marcus 的框架能力全部进入设计、协议、版本、测试和迁移清单。
- `A0/A1/A2` 只表示实现顺序，不表示是否重要；`A2` 能力必须保留契约和后续接入口。
- 四个 Marcus 参考扩展的全部业务行为进入 AWAKE 适配验收场景，但不原样并入 `MarcusAwakeFramework.dll`。
- 旧模块身份、旧 API 兼容、旧 Companion 玩家入口和旧内容包不作为运行时能力继承；这是边界决策，不是功能遗漏。

## 2. 能力继承分级

| 等级 | 含义 |
| --- | --- |
| A0 必须继承 | 第一版内置框架不可缺失；缺失即不再是 Marcus-Awake |
| A1 先继承契约 | 先实现稳定 API/协议/测试，再逐步接入具体玩法 |
| A2 后续继承 | 能力属于 Marcus 优势，但可在 Service/生态阶段加入 |
| N 不继承 | 不是框架核心，或与 AWAKE 的边界/玩家体验冲突 |

## 3. 核心运行时能力

| Marcus 能力 | 原框架优势 | AWAKE 的具体用途 | 继承级别 | 适配要求 |
| --- | --- | --- | --- | --- |
| Host / Extension 生命周期 | 扩展注册、CampaignSessionReady、SessionEnding、卸载隔离 | AWAKE 在战役安全生命周期初始化 NPC、知识、事件和存储 | A0 | 改为 `MarcusAwakeFramework` 身份；不在轻量注册阶段访问 `Campaign.Current` |
| `RequestContext` | correlation、session、deadline、取消贯穿调用链 | NPC 对话、周报生成、模型拉取、连接测试、知识检索统一追踪 | A0 | 所有 AWAKE 请求必须绑定当前 campaign/timeline/session |
| Typed Error | 通过 `FrameworkError.Code/Category` 分支，不解析文本 | MCM、NPC 对话、世界事件和 DevTools 得到稳定可本地化错误 | A0 | 保留 code/category/owner/correlation，中文仅做展示 |
| PermissionGate | manifest 不等于授权，调用点受控，未知权限 fail closed | 云端外发、玩家状态读取、RAG、Storage、命令执行 | A0 | 玩家主动 UI 才能请求授权；后台只能 Evaluate |
| AI Gateway | 逻辑 Route 与 Provider 解耦 | NPC 对话、预处理、后处理、记忆压缩、周报总结 | A0 | AWAKE 只调用逻辑 Route，不携带 URL、Key 或厂商 DTO |
| Provider routing | 多 Provider、能力探测、fallback、pinning | 云端/本地 Provider 切换、模型降级、按任务选模型 | A0 | 模型不可用时返回 typed degraded；不能静默改变任务语义 |
| Streaming / cancellation | TextDelta、Completed、取消、RouteChanged、ResolvedModel | NPC 对话流式显示、取消生成、连接测试、模型拉取 | A0 | UI 只处理状态机，不把半截文本当最终结果 |
| Prompt Registry | 版本化 Prompt、占位符、Schema、工具 allowlist | NPC 人格、世界知识表达、周报、事件叙述 | A0 | Prompt 属于 AWAKE/内容包 owner；玩家不在游戏内编辑原始 Prompt |
| Structured Output | Schema 校验、版本和字段边界 | NPC reply/effects、周报结构、知识摘要、事件候选 | A0 | 解析失败不得执行效果；保留原文供 DevTools 诊断 |
| Context Provider / Planner | PlayerKnown DTO、来源、scope、过期、token 预算和排除原因 | 玩家、NPC、场景、关系、知识、事件上下文 | A0 | 不长期持有 TaleWorlds 实时对象；上下文贡献必须可解释、可限额 |
| ToolCandidate 验证 | 模型工具调用不可信，冻结 allowlist、轮次、owner 和风险 | NPC 可能提出关系/承诺/世界事件候选 | A0 | 代码重新校验当前快照和权限；候选数量有上限 |
| Command Governance | CommandDescriptor、风险等级、Preflight、Execute、receipt、幂等 | 关系变化、承诺、金钱、世界效果、知识传播 | A0 | AI 只能产生候选；实际结算由 AWAKE command adapter 完成 |
| R1/R2/R3 风险 | 普通界面、Gameplay、战略命令分级 | 玩家确认、重大关系/经济/战争效果 | A1 | 成人内容或高张力内容也不能绕过命令风险和玩家选择 |

## 4. 持久化、事件与时间线能力

| Marcus 能力 | 原框架优势 | AWAKE 的具体用途 | 继承级别 | 适配要求 |
| --- | --- | --- | --- | --- |
| Campaign namespace | 按 extension/campaign/timeline 隔离 KV | NPC 记忆、关系、事件、周报、对话队列、联系人 | A0 | 逻辑 namespace/key/schema 原样接管；物理根改为 `%LOCALAPPDATA%\\MarcusAwakeFramework` |
| 幂等写入 | command idempotency、重复 receipt、重试安全 | 防止 AI 重试导致关系/金钱/承诺重复结算 | A0 | 每个持久化命令必须有稳定幂等键 |
| Async write queue | 游戏线程不阻塞，批量 drain，SessionEnding 最终排空 | 对话记忆、事件、周报、关系和结算写入 | A0 | 不在 tick/UI/Execute 中做网络或 DB I/O |
| Durable event / spool | 事件先持久化、可重放、有限重试、失败可观察 | 世界事件、知识传播、周报批次、NPC 反应 | A1 | 高价值事件保留；低价值 UI 流不进入持久 spool |
| Save anchor | 小型存档锚点绑定外部 campaign/timeline 数据 | Bannerlord 存档与 Service DB 对应 | A0 | 保留 AWAKE 现有三个 Native SyncData key；Framework 使用独立 anchor key |
| Timeline isolation | campaign lineage、timeline branch、防止未来事件倒灌 | 旧档回载、新分支、玩家知识 Overlay 导出 | A1 | 不自动合并兄弟时间线；旧 Marcus DB 不自动接管 |
| Migration registry | Schema epoch、版本迁移、失败可观察 | AWAKE namespace/schema 升级 | A1 | 每个 schema 明确 current/legacy/unsupported；迁移前备份 |
| Read-only views | 不暴露共享 DB，按 owner/scope/行数/字节限制 | 玩家简化状态、DevTools 脱敏诊断、作者工具 | A1 | 玩家不可直接看到表、路径、SQL 或完整存储正文 |

## 5. 世界知识与 RAG 能力

| Marcus 能力 | 原框架优势 | AWAKE 的具体用途 | 继承级别 | 适配要求 |
| --- | --- | --- | --- | --- |
| SQLite FTS5 RAG | bounded chunks、scope、corpus fingerprint、atomic replacement | 世界知识基础检索、历史/政治/经济/文化/战争档案 | A0 | Runtime Service 拥有 SQLite/FTS5、索引和物理 DB；Framework 只提供 API/IPC/权限/预算；AWAKE 负责世界书语义和权限；Worldbook 查询必须先带 `WorldbookQueryScope` |
| Embedding / rerank capability | 可由 Service/Provider 提供语义检索，不污染游戏侧 | 后续提高复杂问题检索质量 | A2 | Runtime Service 拥有 Adapter 和缓存；第一阶段保留关键词回退，不在游戏进程加入本地 embedding |
| Corpus fingerprint | 识别索引与当前语料是否一致 | 世界书包切换、内容扩展包、Overlay 导出 | A0 | 包 hash、manifest hash、语料 fingerprint 分开记录 |
| Access scope | 检索结果不越过 owner/collection/权限范围 | NPC 只检索自己能知道的内容 | A0 | 先代码侧筛选身份/阶层/地域/时代，再进入 Prompt |
| Bounded retrieval | 限制命中数、字节数和 token 预算 | 节省 CPU、Provider token，避免完整世界书灌入 | A0 | 每次检索记录 included/excluded 原因，供诊断分析 |
| Worldbook semantic layer | 不是 Marcus 通用框架本身，而是 AWAKE 内容能力 | 档案、表达、授予/拒绝、推荐询问对象、Overlay | A0 | 不把 worldbook 权限规则下放给 AI；AI 只读已经筛选的结果 |
| NPC knowledge state | 将长期、重要、可表达知识投影为 NPC 状态 | 普民、头人、士兵、贵族、广闻 NPC 的差异 | A1 | 周报/事件统一产生 KnowledgePatch，不逐 NPC 学习 |

RAG 所有权不可混用：Framework Core 不实现或持有 SQLite、FTS5、Embedding、Rerank、向量缓存和物理 RAG 路径；Runtime Service 是这些能力的唯一实现与存储 owner；AWAKE 只负责世界书语义、身份/阶层筛选、事实权限和用途边界。检索结果不能覆盖 Bannerlord 事实或 AWAKE 权限结算。

Worldbook v2 的检索顺序固定为：AWAKE 生成不可变 `WorldbookQueryScope` → Runtime Service 在 FTS5/Embedding/Rerank 前按 package/collection/archive/entry/grant/content revision/overlay revision 过滤 → 返回带 `source_entry_ids`、`grant_rule_ids`、classification、manifest hash、corpus fingerprint 和 policy epoch 的 chunk → Framework PermissionGate → ExportPolicy/Egress。没有 scope、索引指纹不匹配或来源无法追溯时 fail closed，不得让未授权条目进入候选、排序、缓存或诊断。

## 6. IPC、Provider 与安全能力

| Marcus 能力 | 原框架优势 | AWAKE 的具体用途 | 继承级别 | 适配要求 |
| --- | --- | --- | --- | --- |
| Private Named Pipe | 本机双进程，无需开放端口 | Framework 与 AI Runtime Service 通信 | A0 | 使用新服务身份与新 pipe 名，不与旧 Marcus 共用 |
| Protocol handshake | major/minor、Bannerlord API、service version、features | 防止错误 Service/错误游戏版本接入 | A0 | 不匹配时 typed unavailable；不静默降级到旧服务 |
| Message envelope | request/correlation/causation、session、sequence、deadline、checksum | 配置、Provider、AI 任务、Storage 和事件请求 | A0 | 有界 frame、解析深度、字段数和 payload 大小 |
| Session invalidation | 战役切换后旧消息不能进入新战役 | 防止 NPC 对话、事件和写入串档 | A0 | generation/session token 双重校验 |
| Retry / reconnect | 管道断线后有限重连和有界重试 | 服务启动慢、Provider 暂时不可用 | A1 | 命令依靠 receipt 对账；不重复执行不确定命令 |
| Credential reference | API Key 不暴露给扩展和游戏 UI | MCM 配置云端 Provider | A0 | Key 只进入 OS protected store；MCM 可见编辑仅存在于当前输入控件 |
| Cloud export policy | Route、分类、MCM、Connection 云端门求交集 | 玩家状态、NPC 状态和世界书外发控制 | A0 | 默认交集为空；玩家明确开启后才允许云端 |
| Management API | DevTools 配置、诊断和批量操作 | 开发者工具和导出，不作为玩家主入口 | A1 | loopback/鉴权/Origin/CSRF/no-store；玩家 MCM 走受控 API |
| Observability | owner、correlation、route、resolved model、health | 玩家得到简单状态，开发者得到完整诊断 | A0 | 日志分级、脱敏、可导出，但不把原始日志放进游戏 UI |

## 7. 资产、媒体和生态能力

| Marcus 能力 | AWAKE 适配 | 继承级别 | 说明 |
| --- | --- | --- | --- |
| AssetHandle / CAS | NPC 头像、信件附件、内容包资源、未来语音/图片 | A2 | 先保留 opaque handle 和所有权边界，不急于开放全部媒体 |
| TTS / VoiceProfile | NPC 语音、周报朗读、内容包音色 | A2 | 受 Route、语言、许可和 fallback 治理 |
| Image generation | 世界书插图、事件图片、NPC portrait | A2 | 不是第一版核心；不得把文件路径暴露给游戏 UI |
| SDK / capability URI | 第三方世界观、内容包、其他大型 Mod 接入 | A1 | 稳定 API、Schema、最小权限和版本化 capability |
| DevTools | 内容作者、模组开发者、故障排查 | A0 | 与玩家配置工具分离，但共享同一服务契约 |

### 3.1 Canonical A2 capability IDs

The canonical A2 set is: `F-016`, `F-024`, `F-026`, `F-049`, `F-050`, `F-051`, `F-065`. The inventory, ownership map, contract, phase fields and release gates must use this exact set; a phase label containing `A2` is invalid unless the F-ID is in this set.

## 8. 不进入 Framework Core、但仍纳入整体设计的内容

| 不继承对象 | 原因 |
| --- | --- |
| 旧 `MarcusAIFramework` 模组身份、ModId、SubModule 入口 | AWAKE 将完全内置，不支持玩家同时启用旧模块 |
| 旧 `MarcusAIFramework.Api` 运行时兼容 | 避免双程序集、双 Host、双 IPC 和双 Storage 权威 |
| 四个 Marcus 示例玩法扩展原样代码 | `MarcusAINpc`、`MarcusAIRelationships`、`MarcusAIDiplomacy`、`MarcusAIWorldEvents` 的全部行为纳入 AWAKE 适配与验收，但不把玩法代码塞进 Framework Core |
| 旧 Companion 管理页作为玩家必经入口 | 玩家配置统一收回 AWAKE MCM |
| Provider HTTP、SQLite、API Key 直接暴露给 AWAKE 玩法代码 | 会破坏权限、凭据和服务边界 |
| 旧世界书、旧成人内容和旧 NPC 设定 | 内容属于 AWAKE/内容包，不应污染通用 Framework Core |
| 原始 Prompt、Token、数据库表、IPC 细节进入玩家 UI | 属于 DevTools/诊断边界 |
| 每个 NPC 主动 Provider 学习 | CPU/Token 成本高，且不符合 AWAKE 的周报/事件统一更新策略 |

> 以上“不进入 Framework Core”不等于不考虑。媒体、六类 Provider、CAS、SDK、FakeHost、Analyzer、管理 API、时间线、RAG、四个参考扩展等能力的逐项归属、阶段和证据见全功能清单。

## 9. AWAKE 专属适配层

Marcus 提供通用基础设施，但以下逻辑必须由 AWAKE 自己拥有：

1. **NPC 知识权限**：按身份、阶层、专业、地域、亲历性、时代和关系判断可知范围。
2. **世界书档案语义**：政治、经济、文化、战争四类客观知识及其摘要、表达、来源和可信度。
3. **知识传播结算**：玩家传授、NPC 间转述、周报/事件批量更新。
4. **NPC 对话编排**：先代码决定候选知识和行为边界，再由 AI 生成中世纪语气表达。
5. **关系与家族语义**：原版关系数值作为输入或变动依据，但不让 AI 直接替代原版数值结算。
6. **成人内容与内容包门控**：Framework 不绑定内容取向，内容包决定启用的世界书、事件和 Prompt。
7. **玩家 Overlay**：玩家可在游戏内修改既有知识、导出并供后续存档复用，但权限授予规则仍由内容/程序维护。

## 10. 分阶段验收口径

“Framework 首版不可缩水”与“AWAKE 玩法仍有 deferred 能力”不是同一个验收口径，项目固定区分以下三层：

### 10.1 Framework Core Migration Baseline

首批内置迁移只验收 Framework Core、IPC、Session/Save anchor、Permission、Context、Command、typed result 和 Service 基础边界。最小链路固定为：

```text
SubModule → Bootstrap/Host → SessionReady → SessionLease
  → RequestContext → typed result → receipt/诊断
```

该首版不宣称 NPC 对话、关系写回、外交提案或世界事件 AI 叙述已经可用；这些能力必须在 AWAKE Gameplay Baseline 单独验收。

### 10.2 AWAKE Gameplay Baseline

游戏侧功能必须达到“入口 → 调用 → 结算/持久化 → 可观察结果”，但只允许使用契约中明确的 `deferred`/`partial`：

- MCM 可配置 URL、可见 API Key、模型拉取/选择、连接测试和保存应用。
- 世界书和 NPC 上下文先经过代码侧身份、阶层、地域、时代和 PlayerKnown 筛选，再进入有界 RAG/Prompt。
- AI 返回结构化 reply/effects/command 时，代码验证 Schema、allowlist、权限、风险和 snapshot 后才结算。
- 原版关系、事实、事件和存档仍由代码/Native 权威；F-064 的只读关系投影和 F-066 的事实观察可作为 `partial`，AI 写回、叙述和传播必须显示 deferred。
- 玩家界面只显示自然语言状态；DevTools 能导出脱敏诊断；外部旧 Marcus 不作为兼容路径。

### 10.3 Full Capability Completion

完整继承验收要求 F-001–F-066 都在 `MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md` 中拥有唯一 owner、artifact、fixture 和 phase；除明确标记为 A2 的后续能力外，均须达到 `available` 或有匹配的实机/Provider 门证据。`deferred`/`partial` 不能被包装成完整功能，也不能因首批 Framework Core 已通过而自动转为 available。

每一批必须引用具体 F-ID，并记录未实现、未实机验证、外部 Provider 未验证和降级状态；编译通过或类/路由存在都不构成闭环证据。

### 10.4 三层权限顺序

知识调用固定遵循：

```text
AWAKE WorldbookKnowledgePolicy
  → Framework PermissionGate
  → ExportPolicyEvaluator / EgressBroker
```

- 世界书策略按 NPC 身份、阶层、地域、时代、专业和亲历性筛选，只能缩小候选知识，不能授予隐藏知识。
- Framework `PermissionGate` 是框架权限唯一权威，未知权限 fail closed；它不能恢复世界书已经排除的内容。
- Export/Egress 是最终外发门，只能 `allow`、`trim` 或 `deny` 当前已获准的字段；不能扩大世界书或 Framework 的范围。
- 统一 fixture：`PermissionOrderAndEgressFixture`；必须覆盖未知权限、分类未知、裁剪后 hash 重算、旧 decision 重放和 Adapter 直连拒绝。

### 10.5 阶段退出门

| 阶段 | 退出证据 | 失败时 |
| --- | --- | --- |
| P0/P0.5 | 来源、许可证、配置/存档/Storage 盘点 | 停止，不改代码入口 |
| P1.5 | 契约、ownership map、统一 fixtures 和独立审查 `VERDICT: APPROVED` | 禁止进入 P2 |
| P1 | Framework Core 独立项目骨架、API surface diff、Release build 和 P1 allowlist | 不修改 AWAKE 现有入口 |
| P2 | `FrameworkCoreVerticalSmokeFixture` E1 和 Core 契约报告 | 禁止进入 P3 |
| P3 | Service lifecycle、RAG、Credential/Egress、IPC/Storage fixtures，以及 Vertical Smoke E2 后置证据 | 保持离线降级 |
| P4 | MCM 事务、玩家配置脱敏和连接测试 | 不宣称玩家配置完成 |
| P5 | DevTools/SDK、Analyzer、脱敏导出和包检查 | 不宣称作者工具完成 |
| P6 | AWAKE 适配逐项 fixture、partial/deferred 状态和原版事实回归 | 不宣称完整游戏首版 |
| P7 | 构建、同步、哈希和用户提供的 E4/E5 证据 | 只能保持离线验证状态 |

fixture、artifact、owner 或 evidence 任一缺失时，阶段保持 `needs_review`/`blocked`，不能用下一阶段的静态文件反向补齐上一阶段退出门。

## 11. 迁移顺序

```text
P0  来源与许可证冻结
  ↓
P0.5  配置、存档、Storage、Companion 盘点
  ↓
P1.5  能力继承矩阵 + API/程序集/IPC/Storage 契约
  ↓
P1  Framework Core 独立项目/API surface
  ↓
P2  Framework Core vertical integration
  ↓
P3  AI Runtime Service 重构
  ↓
P4  AWAKE MCM 玩家配置接入
  ↓
P5  DevTools 与诊断导出
  ↓
P6  AWAKE NPC/世界书/事件集成
  ↓
P7  离线契约、构建、部署、游戏内与存读档验证
```

任何阶段都不能用“DLL 能编译”代替实际能力闭环验证。

## 12. F-ID 覆盖索引

以下索引把全功能清单的每一个连续 ID 范围绑定到本矩阵章节，避免“矩阵有能力描述但无法证明覆盖 F-001–F-066”的歧义。具体能力名称和阶段仍以 `MARCUS-AWAKE-FULL-CAPABILITY-INVENTORY-v1-20260824.md` 为准。

| 能力 ID | 本矩阵权威章节 | 说明 |
| --- | --- | --- |
| F-001–F-008 | §3、§7 | 身份/生命周期/SDK/测试工具；SDK、FakeHost、Analyzer 另在生态层保留边界 |
| F-009–F-017 | §3、§5 | GameData、稳定身份、可见性、快照、Context、RAG、Prompt Registry |
| F-018–F-031 | §3、§6、§7 | Gateway、Route、六类 Provider、流式取消、预算、Structured Output、Tool Candidate |
| F-032–F-041 | §4、§5 | 事件、spool、platform/campaign DB、sidecar、Save anchor、Timeline、Command、Receipt |
| F-042–F-048 | §6 | IPC、Session invalidation、Service、Management API、Credential、Cloud Policy、离线降级 |
| F-049–F-053 | §7、§8 | Asset/CAS、TTS、图片、Workflow、Retention/Quota；不把媒体塞进 Framework Core 的边界 |
| F-054–F-062 | §7、§8、§11 | MCM、诊断、DevTools、观测、性能、本地化、许可证、双版本、迁移 |
| F-063–F-066 | §8、§9、§10 | 四个参考扩展的 AWAKE 适配，不原样进入 Framework Core；F-063/F-065 为 deferred，F-064/F-066 为 partial |

覆盖检查规则：全功能清单的连续范围 `F-001` 至 `F-066` 必须在本索引中完整覆盖且不得有空档；本索引用范围表达避免重复复制 66 行能力名称，具体单项、owner、artifact、fixture 和 phase 以 `MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md` 为准。新增能力必须先进入全功能清单，再补本索引、owner map 和对应验收闭环。
