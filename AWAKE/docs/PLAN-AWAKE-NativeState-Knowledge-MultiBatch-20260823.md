# Plan: AWAKE 原版状态与 NPC 世界知识多批次边界

_Locked via grill — by Claude + user authorization on 2026-08-23_

> 状态：`LOCKED_AFTER_GRILL`
>
> 说明：用户已明确批准按当前推荐方案推进全部相关批次的边界设计。本次授权被记录为对 D1–D6 推荐答案的整体签收，但不等于批准立即修改运行时代码；代码仍须通过独立只读对抗审查、逐批实现租约和对应验收门禁。
>
> 编制日期：2026-08-23
>
> 当前检查点：`docs/checkpoints/AWAKE-NATIVE-KNOWLEDGE-BOUNDARY-20260823-checkpoint.md`

## 1. 目标

建立一条代码主导、可持久化、可验证、低 Token/CPU 消耗的 AWAKE 游戏内社会与世界知识链路：读取 Bannerlord 原版状态，生成 AWAKE 语义投影，依据世界书身份权限决定 NPC 能知道和能说什么，处理玩家传授知识时的相信/怀疑/拒绝，使用结构化世界事件和机械周报传播长期知识，并将 NPC 私人认知与世界客观事实严格分层。原版关系写回保持为最后的独立可选批次，不与只读读取、知识权限和记忆系统混做一批。

## 2. 计划状态与证据

### 2.1 当前状态

- 计划类型：跨运行时、世界书、存储、AI 上下文和游戏验证的架构边界方案。
- 当前最高证据等级：`E0`。
- Act 1 决策状态：D1–D6 已按推荐答案整体签收；不存在需要继续阻塞本边界计划的产品分歧。
- Act 2 状态：独立只读对抗审查已在三轮内收敛为 `VERDICT: APPROVED`；批准范围仅解除 B1 单批次实现前置门禁，不代表代码、构建、同步、游戏或存档验证完成。
- 当前无执行租约。
- 当前冻结运行时候选、`dist`、游戏目录、`PlayerExports` 不得因本计划改变。
- 本计划只写设计和验收边界，不包含代码、构建、同步或 Bannerlord 启动。

### 2.2 权威来源

1. `_houkai_merge\AWAKE\AGENTS.md`。
2. `docs\AWAKE-CURRENT.md` 和本计划检查点。
3. `docs\AWAKE-NativeState-Adapter-Spec-v1.md`。
4. `docs\AWAKE-NativeState-NumericRules-v1.md`。
5. `docs\AWAKE-Worldbook-Contract-v1.md`。
6. `docs\WORLDBOOK-EVENT-REPORT-CONTRACT-v1.md`。
7. `docs\mappings\bannerlord-native\关系-家族-家庭-外交原生映射表-v1.md`。
8. 当前 AWAKE `src` 中实际调用链和存储实现。

历史 AF 世界书、旧四版运行时内容、旧 RAG 回退实现、Studio AI 建议和 NPC 私人记忆都不是客观世界知识权威。

## 3. 已确认的现状

### 3.1 原版关系尚未接入 AWAKE

当前 AWAKE 静态搜索未发现以下原版 API 的运行时接线：

- `Hero.GetRelation`；
- `Hero.GetBaseHeroRelation`；
- `Hero.IsFriend`；
- `Hero.IsEnemy`；
- `ChangeRelationAction`；
- `CharacterRelationManager`；
- `Clan.GetRelationWithClan`；
- `FactionManager.GetRelationBetweenClans`。

当前 NPC 对话和主动行为主要读取 AWAKE 自定义 `trust/love/hostility`，主动行为仍使用既有 `affinity = trust + love - hostility`。

### 3.2 世界书 v2 已有唯一权限查询路径

当前世界书 Runtime 已有以下权威链路：

```text
WorldbookRuntime
  -> WorldKnowledgeSnapshot
  -> WorldKnowledgeQueryService.Query
  -> blocked
  -> identity
  -> conditions
  -> denies
  -> grants
  -> detail
  -> referral
  -> known / partial / referral / not_found
```

不得重新制作第二个 NPC 世界书读取器。旧 `KnowledgeService` 只能保留为显式、可观测、受权限限制的离线回退；不能在 v2 查询失败时静默变成无身份过滤的 NPC 运行时路径。

### 3.3 事件与周报已有唯一生产路径

```text
生产者
  -> WorldEventServices.Recorder
  -> WorldEventLedger
  -> Storage

查询/展示
  -> WeeklyReportService
```

周报是事件事实的机械投影，不是事实源，也不能通过“遍历全部 NPC 调 AI”实现学习。NPC 学习必须订阅结构化事件或明确的周报窗口投影，并接受身份、地点、可见性和持久性门控。

### 3.4 现有运行时风险

- `CampaignSessionReady` 的服务初始化与 `WorldStateStore` 就绪存在首开时序风险。
- `AwakeEventBehavior.OnHourlyTick` 并发启动事件、主动 NPC 和记忆整合，主动 NPC 缺少对应单飞闸门。
- `_pendingWrites` 没有明确容量/背压。
- 主动 NPC 保存候选集合时使用随机幂等键，存在并发最后写入覆盖风险。
- `ContextProviders` 在部分 GameData 失败路径直接回退 `Hero.MainHero`，与统一 GameData 边界不完全一致。
- 关闭生命周期中存在同步等待后台排空的风险。
- v2 查询结果的 `state/blockedReason/referralIds/hitIds` 尚未完整形成 AI 上下文硬边界。
- NPC 记忆目前没有清晰的 `epistemicStatus/confidence/visibility` 字段，玩家说法可能进入私人记忆但缺少知识性质标记。

## 4. 不可破坏的架构边界

### 4.1 五层知识对象

```text
世界书客观知识
  └─ 激活包/Manifest/Runtime Snapshot，内容权威，不由 NPC 对话改写

战役世界事实
  └─ WorldEventRecord，代码生成，带 eventId/eventKey/domain/occurredAt

机械周报
  └─ WeeklyReportService 的展示/批量投影，不反向成为事实源

NPC 个体认知
  └─ heard claim / tentative belief / confirmed-by-event / rejected claim

对话文本
  └─ AI 或代码生成的表达，不具有自动写入客观世界的权限
```

### 4.2 原版与 AWAKE 数值边界

- 原版基础关系、有效关系、敌友判断、家族拓扑、家族聚合和派系姿态保持不同语义。
- 原版值可直接用于原版语义，也可作为 AWAKE 临时投影输入。
- 原版关系不自动覆盖 `trust/love/hostility`。
- `sameClan`、家庭关系和族长关系不自动转成私人信任加分。
- 原版关系数字变化但原因未知时只刷新快照，不改变 AWAKE 认知状态。
- 原版关系写回必须通过 `ChangeRelationAction` 等原版入口，永远不直接改关系管理器内部字典。

### 4.3 AI 权限边界

AI 可以识别有限枚举、生成表达和给出语言建议；AI 不可以：

- 计算关系、信任、披露、请求或可信分；
- 授予世界书身份权限；
- 将玩家说法直接写入世界书客观事实；
- 绕过 `blocked/referral` 结果补答秘密；
- 把周报文本当作新事实；
- 直接调用原版关系写回。

## 5. 推荐的多批次拆分

### B0：合约、数值和边界冻结

**目标**：将适配规范、数值规则、世界书 Contract、事件/周报 Contract、主张结算和 typed memory Contract 统一为实施前基线。

- 入口：无运行时入口，属于设计审查。
- 产物：本计划、适配规范、数值规则、`KnowledgeDecision`、`ClaimDecision`、`EventPropagation`、typed memory、AI/Token/CPU budget 和失败状态契约、测试向量、决策记录、独立只读审查日志。
- 结算：锁定 source/target 语义、基础/有效关系用途、`unknown` 回退、`blocked/referral` 硬门、知识分层、旧回退策略、玩家传授存储方案、事件可见性/目标/水位/幂等和周报传播规则。
- 旧路径证明：正式 `NpcDialogueService` 运行路径中旧 `KnowledgeService` 不可达；缺少 v2 package、加载失败或查询异常统一返回结构化 `unknown/blocked`，不能静默切换到无身份过滤的本地索引。旧服务若保留，只能由离线测试显式注入。
- 规范 schema：B0 以版本化契约冻结字段所有权、枚举、范围、空值和非法输入处理；“至少包含”不作为实现接口，未列入契约的字段不得进入正式运行时。
- `KnowledgeDecision`：固定 `schemaVersion/state/identity/scope/detail/referralIds/hitIds/blockedReason/sourceVersion/correlationId`；`state` 仅允许 `known/partial/referral/blocked/not_found`。`blocked` 和全部 `referral` 分支 `AI=0`；`partial` 只能携带已过滤正文。
- `ClaimDecision`：固定 `schemaVersion/claimId/claimKey/subject/relation/object/timeScope/evidence/coherence/interestBias/knownState/status/source`; 枚举和分值复用数值规范，未知枚举按安全默认处理，任何数值由代码计算。
- `EventPropagation`：固定 `eventId/eventKey/occurredAt/domain/sourceActor/sourceFaction/location/visibility/scope/eligibleIdentityIds/explicitTargetHeroIds/detailTier/propagationWatermark/knowledgeRevision`; 缺失可见性、范围或目标字段时不得广播。
- `TypedKnowledgeEntry`：固定 `schemaVersion/knowledgeId/claimKey/kind/epistemicStatus/confidence/sourceType/sourceId/visibility/scope/firstSeenDay/lastSeenDay/lastVerifiedDay/causationId/idempotencyKey/retentionClass/expiresAt/revision`; schema 版本未知、字段非法或迁移失败时 fail closed。
- 主张到记忆映射：`accept_unverified → accepted_unverified/persist_if_important`；`tentative → tentative/persist_if_important`；`skeptical → skeptical/transient_or_short_retention`；`reject → rejected/transient_anti_repeat`；`tentative_without_overwrite → tentative/temporary_only`，并保留原有 `confirmed/witnessed` 条目，不执行覆盖。映射由代码固定，AI 不选择落盘策略。
- B9 幂等契约：主动评估键固定为 `campaignId + evaluationHour + sourceHeroId + targetHeroId + candidateKey + knowledgeRevision`；同键同载荷为 no-op，同键不同载荷执行 CAS/版本冲突并拒绝后写，不允许随机键或最后写入覆盖。
- 观察结果：`E0` 计划可恢复，所有批次共享的枚举/字段/失败分支已冻结，负向测试能证明越权回退和 AI 补答不可达。
- 不做：代码、存档迁移、世界书内容修改、构建、同步。
- 前置：用户批准本批次的边界方案；随后进入 grill 和独立审查。

### B1：运行时就绪与原版/身份统一只读快照

**目标**：建立一次业务结算可复用的稳定快照，不长期持有 Bannerlord 对象。

- 入口：安全战役生命周期和一次对话/知识结算开始处。
- Caller：建议由 `NpcDialogueService` 通过统一上下文边界取得，不在业务各处直接访问 `Campaign.Current`。
- 唯一所有权：`AwakeRuntime` 持有唯一 `CampaignReadinessCoordinator`/readiness future；B1 与 B1-S 只报告或消费该 future，不各自创建、重置或伪造 ready 状态；`WorldStateStore` 是 Storage 状态提供者，不是第二个 readiness owner。
- 读取：`sourceHeroId`、`targetHeroId`、基础关系、有效关系、敌友状态、家族/族长、家庭拓扑、文化、国家、聚落、年龄、技能、职业/身份所需字段。
- 结算：分别生成短生命周期的 `NativeSocialSnapshot` 与 `IdentitySnapshot`；不可用字段为 `unknown`，不能用 `0` 伪装中立；快照不持有 `Hero`、`Clan`、`Settlement` 等原生对象引用，不跨 `await`、tick 或存档生命周期；快照本身不写 Storage。
- 就绪门：建立唯一共享 readiness future，明确 `ready/failed/cancelled/skipped` 状态、重试策略、超时和 correlation 日志；onboarding、事件队列恢复和对话/知识结算必须依赖同一 readiness 结果，不能在 Store 未就绪时静默跳过。
- 旧服务负向门：B1 的正式组合根不得调用或初始化 `KnowledgeRuntime.EnsureCreated`；旧服务只允许在离线测试组合根显式注入，B1 验收必须包含正式调用图不可达证明。
- 可观察结果：离线夹具能区分基础关系/有效关系/敌友/家族/家庭/身份；首开、重入和失败重试只产生一次就绪结算；上下文来源、快照版本和未知字段可追踪。
- 不做：写回原版关系、关系迁移、NPC-NPC 方向性关系、AI 调用。
- 依赖：B0。
- 最低证据：E1/E2；真实只读对话入口需要后续 E4-R。

### B1-S：Storage 安全基础门

**目标**：在任何对话、事件、玩家传授或 NPC 主动路径产生持久写入前，先锁定 Storage 的就绪、队列、排空和幂等边界。

- 入口：`WorldStateStore` 初始化、`CampaignSessionReady`、所有持久写入调用方。
- 结算：共享 readiness future、bounded write queue、single-flight 写入、关闭 drain 超时、队列满载策略和稳定幂等键；高价值持久知识不得静默丢弃，队列满时返回 `deferred/rejected` 并由上层决定延迟或代码降级；低价值短期记忆允许合并或丢弃，但必须计数。
- B9 写入冲突：按 B0 冻结的 `campaign/hour/source/target/candidate/revision` 键执行 CAS；重复同载荷幂等成功，版本不同返回 `conflict` 并保留先到版本，禁止无条件最后写入覆盖。
- 失败矩阵：取消、超时、Store 未就绪、队列满、版本冲突、重复提交分别返回结构化状态，不把“已入队”冒充“已持久化”。
- 可观察结果：队列深度、拒绝/延迟/合并/丢弃、排空剩余量、排空超时和写入完成状态有日志与离线夹具；B4–B10 不得绕过该门直接写 Storage。
- 不做：修改 Bannerlord 原版 SaveDefiner、改变现有存档命名空间、在 tick 中同步等待文件/网络/数据库。
- 依赖：B0；必须在 B4–B10 产生持久写入前完成。
- 最低证据：E2；读档/关闭回归属于 B11-R 的 E5-R；不得引用 B11-F 的 E5-F 作为 B1-S 的证据。

### B2：统一世界书查询权威和结构化拒绝结果

**目标**：让 NPC 运行时只有一条权限查询路径，拒绝和转介可被代码/AI 上下文观察。

- 入口：`NpcDialogueService.BuildPromptInputAsync` 的知识上下文构建。
- Caller：`WorldbookRuntime.Knowledge.Query` / `WorldKnowledgeQueryService.Query`。
- 结算：统一输出 B0 冻结字段的不可丢失 `KnowledgeDecision`；查询顺序固定为 `blocked → identity → conditions → denies → grants → detail → referral → not_found`。
- AI 硬门状态矩阵：`state=blocked`、`state=referral`、无权限 `state=not_found` 和内容/敌对硬门均 `AI=0`；代码直接生成固定拒绝、转介或安全未知。只有 `known` 或带已过滤正文的 `partial` 才能进入 AI，且不得从空文本推测被清除的秘密。
- 旧路径：旧 `KnowledgeService` 不得从正式 NPC 运行时可达；`CampaignSessionReady` 的正式组合根不得初始化 `KnowledgeRuntime.EnsureCreated`，v2 缺失、加载失败或查询异常统一 fail closed；旧服务只能由离线测试组合根显式注入并标记 `legacy_fallback`。
- 可观察结果：`known/partial/referral/blocked/not_found` 分支可离线测试；每个分支的 AI 调用次数、Prompt 命中和回退原因可验证。
- 不做：重新设计世界书文件格式、修改 Studio 编辑器、把玩家 Overlay 当成 NPC 私人记忆。
- 依赖：B0；可与 B1 的纯设计部分并行，运行时接线需 B1。
- 最低证据：E2；真实只读游戏入口需要后续 E4-R。

### B3：原版社会状态的纯语义投影

**目标**：把原版快照转换为行为用途标签和少量临时数值，不把原始数字直接塞给 AI。

- 入口：对话、知识披露、玩家传授和请求结算前。
- 函数：`NativeConflict`、`SocialModifier`、`TrustModifier`、`HostilityPenalty` 及数值规则文档中的 Belief/Disclosure/Request policy。
- 结算：输出 `nativeConflict`、`socialBaseline`、`familyContext`、`clanContext`、`politicalContext` 和业务专用投影；相同输入必须稳定输出。
- 原则：知识权限由世界书身份决定；原版关系只能影响合作/披露/相信的社会倾向，不能提升权限。
- 可观察结果：N-01 至 N-15 测试通过；原始关系数字默认不进 Prompt。
- 不做：改写 `trust/love/hostility` 存档结构；把三轴重新合并成全局 `affinity`；原版写回。
- 依赖：B1、B2。
- 最低证据：E1/E2。

### B4：对话只读接线与 AI 上下文硬边界

**目标**：在真实 NPC 对话入口接入 B2/B3 结果，且不改变现有关系结算路径。

- 入口：`NpcDialogueService` 的对话准备和 Prompt 编译。
- 结算：一轮对话只读取一份快照，先执行权限/敌对/内容门，再决定是否调用 AI；AI 只收到标签、允许详细度、命中 ID、拒绝/转介状态和表达约束。
- 失败：快照不可用时返回 `unknown` 并保持原版/现有安全回退；权限拒绝不交给 AI自由补答。
- 可观察结果：日志包含 `correlationId`、命中条目、权限状态、快照来源、回退原因和 AI 是否调用。
- 不做：玩家教学持久化、世界事实写入、原版关系写回。
- 依赖：B1、B2、B3。
- 最低证据：E2；只读对话闭环需要 E4-R。

### B5：玩家传授与 NPC 相信结算

**目标**：让玩家可以向 NPC 传授说法，但由代码决定接受、暂信、怀疑、拒绝和转介，不污染世界书客观事实。

- 入口：玩家一轮输入被识别为知识主张后、AI 回复生成前。
- AI 作用：只输出有限枚举，如主题、证据类型、是否与已知事实冲突、叙述一致性、利益偏向和时间范围；输出必须进入 `ClaimDecision`，不提交任意数值。
- 主张契约：`ClaimDecision` 使用 B0 冻结的 `claimId/claimKey`、subject/relation/object/timeScope、evidence、coherence、interestBias、status、source 字段；最终 `BeliefScore/ResolveBelief` 只由代码计算。
- 结算：使用 `accept_unverified`、`tentative`、`skeptical`、`reject`、`tentative_without_overwrite`；亲历/confirmed 事实不能被一次对话覆盖。
- 记忆：玩家说法只能形成带来源、知识状态、可信度、可见性和时间的 NPC 私人认知；不能修改世界书 Runtime Snapshot、基础包或客观事件事实。
- 可观察结果：N-07 至 N-12 通过；同一主张重试幂等；高信任最多使亲历冲突进入暂信，不得直接改写。
- 不做：自动把玩家说法传播给其他 NPC；自动写入世界书；让 AI决定最终分数。
- 依赖：B0、B1-S、B2、B3、B4；B7 只负责把已冻结的 typed knowledge entry 落入 Storage，不反向决定主张语义。
- 最低证据：E2；只读持久化需 E5-R。

### B6：事件事实、机械周报与受控知识传播

**目标**：让 NPC 的长期世界知识主要通过结构化事件和周期性机制更新，而不是逐个 NPC 调 AI 学习。

- 事实入口：`WorldEventServices.Recorder.Record`。
- 周报入口：`WeeklyReportService.Build`，只作展示和批量投影，不反向成为事实源。
- 传播入口：新增或扩展代码主导的 `KnowledgePropagation` 订阅/批处理边界，读取事件记录而不是读取周报文本。
- 事件契约：B0 必须冻结 `eventId/eventKey/occurredAt/domain/sourceActor/sourceFaction/location/visibility/scope/eligibleIdentityIds/explicitTargetHeroIds/detailTier/propagationWatermark` 等可传播字段；缺失字段按保守不可传播处理。
- 传播门：事件可见性、地点/范围、身份、职业、战争/政治领域、是否长期重要、是否已经接收；未知可见性按保守策略处理。
- 结算：可见且值得持久化的事件写入指定 NPC 或指定范围的知识记忆；未指定目标的事件不广播给所有 NPC；重复键使用稳定 `eventId + targetHeroId + knowledgeRevision` 去重，不使用随机会话 ID。
- 可观察结果：事件 → 受限 NPC 知识条目；机械周报内容与事件来源闭合；不存在“周报文本污染事实库”。
- 不做：逐 NPC AI 学习循环；把所有每日事件写成长久知识；把周报当作客观事实权威。
- 依赖：B0、B1-S、B2；B0 已冻结全部共享知识状态、事件可见性和传播目标契约后，B6 可与 B5 并行实现；B7 再合并两者结果接入 Storage。
- 最低证据：E2；跨周、读档和重复传播需 E5-R。

### B7：NPC 知识分层、容量和存档恢复

**目标**：把 Transcript、玩家主张、暂信、确认事实、关系状态、世界事件记忆严格分层，并确保读档/换战役/重试安全。

- 入口：对话结束、玩家主张结算、事件传播、每日整理、读档重建。
- 推荐 v1 存储：复用现有 `awake.npc.memories` Storage 路径，新增明确的 typed knowledge entry 字段，而不是立刻再造第二套 AI 记忆系统；若容量/查询 profiling 不足，再单独立 `awake.npc.knowledge` namespace 迁移批次。
- 最少字段：`schemaVersion`、`knowledgeId`、`claimKey`、`kind`、`epistemicStatus`、`confidence`、`sourceType`、`sourceId`、`visibility`、`scope`、`firstSeenDay`、`lastSeenDay`、`lastVerifiedDay`、`causationId`、`idempotencyKey`、`retentionClass`，以及必要的 `expiresAt`/`revision`。
- 兼容契约：旧 `awake.npc.memory.v1` 条目必须可读；缺失字段映射为保守的 `legacy_memory/unverified`，不得默认升级为 `confirmed`；重复键、版本未知、部分写入和迁移失败必须可检测、可重试，不能静默覆盖。新 schema 的写入与旧条目迁移必须有 round-trip fixture。
- 结算：只有长期重要、可追溯、满足容量预算的条目进入持久知识；单日闲聊和未核实细节保留为短期记忆或不持久化；高价值条目在队列满载时只能延迟/拒绝，不能静默丢弃。
- 可观察结果：保存/读档、重复提交、战役切换、容量淘汰、失败重试、跨 NPC 隔离、旧条目兼容和版本冲突均可验证；私人记忆永不自动升级为世界书事实。
- 不做：修改 Bannerlord 原版 SaveDefiner；把基础世界书复制进每个 NPC 存档；无上限积累知识。
- 依赖：B1-S、B5、B6；Storage readiness、幂等、背压和 schema 兼容门必须先完成。
- 最低证据：E2；只读持久化完整目标为 E5-R。

### B8：AI、Token、CPU 和污染门

**目标**：确保 AI 只处理不可机械化的语言任务，且任何知识/记忆写入都有代码门。

- 入口：知识查询、Prompt 编译、记忆摘要、对话输出、周报/事件投影。
- 代码门顺序：`content gate → identity/access gate → native conflict gate → memory/claim gate → token/byte budget → AI route`。
- 纯代码路径：权限拒绝、转介、固定拒绝、重复事件、无新信息和低价值日常事件不调用 AI。
- AI 路径：只提供有限输入/输出 Schema；不接收未过滤世界书全文、原始关系数字或无界历史。
- 预算：固定 top-k、字节上限、记忆上限、超时、重试、摘要长度和每结算 AI 次数；具体值必须引用 B0 锁定的数值/测试向量，不得由 AI 或 Provider 动态放宽；日志记录命中 ID、耗时、fallback、token/byte 预算。
- 污染门：Studio 建议、旧 RAG、历史包、玩家主张、NPC 私人记忆、周报文本不能直接成为客观世界知识。
- 可观察结果：离线预算测试、拒绝不调用 AI、回退显式可见、无 UI/Campaign tick 阻塞 IO。
- 不做：本地 embedding/tokenizer/模型；逐 NPC 周报 AI 学习；让 AI自由写数值或世界事实。
- 依赖：B0、B4、B5、B6、B7；可与 B9 的主动 NPC 设计和 B10 的原版写回设计分开。
- 最低证据：E2；真实 Provider/只读游戏运行需要 E4-R。

### B9：主动 NPC 受控接线

**目标**：在原有主动 NPC 机制不被破坏的前提下加入原版语义投影和知识动机。

- 入口：`NpcProactiveService.OnHourlyTickAsync`。
- 结算：原版投影只能作为额外上下文和硬门控，不能直接加进现有 `affinity`；每小时使用单飞闸门、候选上限、冷却和稳定排序。v1 只允许使用“玩家 → 当前 NPC”的 native snapshot；主动 NPC 面向其他 NPC 时不得暗中创建 NPC-NPC 原版关系快照，必须使用不依赖原版方向性关系的 AWAKE 语义或延后该候选。
- 生命周期：不得在 tick 中阻塞网络、文件或 Storage；异步任务必须有取消/过期边界；后台任务只接收不可变标量快照和稳定 Hero ID，不持有 Bannerlord 原生对象；禁止并发保存覆盖。
- 可观察结果：每小时最多一次受控评估；候选理由、快照 token、知识状态、是否因 native scope 被跳过以及是否调用 AI 可追踪；不重复弹出相同候选。
- 不做：一次性重写主动 NPC 关系模型；NPC-NPC 全量方向性关系迁移；强制学习世界全部知识。
- 依赖：B1、B1-S、B3、B7、B8；B9 前必须解决 hourly in-flight、候选快照范围、稳定幂等键和写入合并风险。
- 最低证据：E2；主动行为只读游戏入口需要 E4-R。

### B10：原版关系写回（可选独立批次）

**目标**：只在明确的 AWAKE 事件确实应影响 Bannerlord 原版玩法时，把代码结算结果通过原版合法入口写回。

- 入口：明确的玩家动作或 AWAKE 事件结算，不允许由 AI 文本直接触发。
- 结算：先用与 Bannerlord `v1.3.15` 版本匹配的反编译/API 证据锁定 `ChangeRelationAction` 的具体 overload、调用时机、钳制和失败行为，再执行 `Preflight → 权限 → 快照 token → 幂等 → 代码计算 delta → 原版合法入口 → 原版钳制/事件/存档 → AWAKE 快照失效`。
- 双写：必须使用同一 `eventId`、`correlationId`、`causationId`、`idempotencyKey` 和前置快照记录结果；任一侧失败不得伪造成功。
- 失败矩阵：原版写回成功/AWAKE 写入失败、AWAKE 预写失败、原版调用异常、重复调用、快照过期、族长更换和读档恢复都必须有明确补偿或人工可诊断状态；在没有原子双写能力时，不得宣称两侧一致。
- 可观察结果：原版关系、AWAKE 状态、事件账本和日志可相互追溯；族长更换不重复传播；关系写回能读档恢复；重复调用和失败补偿结果可离线重放。
- 不做：直接修改 `CharacterRelationManager` 内部字典；把 AWAKE 三轴持续覆盖原版关系；把家族关系复制到成员。
- 依赖：B11-R、B1、B1-S、B3、B7、B8；必须单独 grill、独立审查、用户签收和版本匹配 API 证据。
- 最低证据：E4-F/E5-F；没有用户运行证据不得宣布 B10 完成。

### B11-R：只读整合、性能和存档回归

**目标**：在不启用原版关系写回的前提下，验证入口→调用→结算→可观察结果的完整只读知识链路，并覆盖存档、时间线和性能边界；这是 B10 的前置门。

- E2：纯函数、查询、事件/周报、拒绝/转介、幂等、预算和静态边界。
- E3：仅在新运行时批次完成后，按 BuildId 重新构建/同步/哈希；不触碰当前冻结候选。
- E4-R：用户运行匹配 BuildId，验证对话、身份权限、知识传授、周报传播和主动 NPC 入口；明确验证原版关系未被写回。
- E5-R：退出/读档/新进程后验证 NPC 知识、AWAKE 关系状态、事件重复、族长更换、战争状态、Overlay 和时间线隔离；不把原版写回作为前置假设。
- 性能：统计每次结算快照次数、查询候选数、Prompt 字节数、AI 次数、Storage 写入次数、队列长度、小时批处理耗时。
- Storage：验证 readiness single-flight、队列满载的 `deferred/rejected/coalesced/dropped` 分支、关闭 drain 超时和重复写入结果；高价值知识不得静默丢失。
- Scope：验证 v1 不产生 NPC-NPC native relation snapshot；主动 NPC 对超出 scope 的候选必须可解释跳过或使用明确的 AWAKE-only 路径。
- 不做：用编译通过、文件存在、旧 BuildId 日志或静态测试冒充 E4-R/E5-R。

### B11-F：可选写回最终验证

**目标**：仅在 B10 获得独立签收并启用时，验证原版关系写回与 AWAKE 状态、事件账本和存档之间的最终可追溯性。

- 入口：匹配 B10 BuildId 的用户运行和存档回归。
- 结算：重复写回、原版 API 异常、快照过期、双写失败、补偿、读档和族长更换均按 B10 失败矩阵验证。
- 可观察结果：原版关系变化、AWAKE 状态变化、事件/日志和存档结果能由同一 event/correlation/idempotency 链追溯；没有原子双写时明确显示不一致状态。
- 不做：把 B11-F 的证据倒推为 B1–B9 已完成，或把可选写回当作 AWAKE 基础运行时必需功能。
- 依赖：B10、B11-R；最低证据 E4-F/E5-F。

## 6. 依赖图与可并行范围

```text
B0 合约/边界冻结
 ├─ B1 运行时就绪 + 原版/身份快照
 ├─ B1-S Storage 安全基础门
 └─ B2 唯一世界书查询 + 结构化拒绝
       └──────────────┐
B1 + B1-S ────────────┴─ B3 纯语义投影
                               └─ B4 对话只读接线
                                    ├─ B5 玩家传授/相信
                                    ├─ B6 事件/周报传播
                                    └─ B8 AI/Token/污染门
B1-S + B5 + B6 ── B7 NPC知识分层/存档恢复
B7 + B8 ── B9 主动 NPC 接线
B4 + B5 + B6 + B7 + B8 + B9 ── B11-R 只读 E4-R/E5-R
B11-R + B1 + B1-S + B3 + B7 + B8 ── B10 原版写回（可选）
B10 + B11-R ── B11-F 写回 E4-F/E5-F（仅启用 B10 时）
```

### 6.1 可并行

- B1 的静态接口设计、B1-S 的 Storage 失败矩阵和 B2 的世界书查询契约审查可以并行，但 B4 以前必须合并为唯一运行时边界。
- B5 和 B6 在纯契约设计层可以并行；涉及同一 Storage schema 时必须串行合并。
- B8 的预算/污染审计可以和 B10 的只读设计并行，但任何代码写入必须按批次排队。

### 6.2 必须串行

- B0 之后才能实施任何批次。
- B1/B1-S/B2 之后才能实施 B3。
- B3/B4 之后才能实施玩家教学。
- B5/B6 的状态枚举和来源字段确定、且 B1-S 通过后才能锁 B7 存储。
- B7 的实现不等待 B11-R；只有 B11-R 的 E5-R 通过后，才能宣称玩家知识或事件传播持久化完成。
- B11-R 完成前不能进入 B10 的写回实现；B10 永远不能与 B1–B9 的首次只读接线混在同一个实现批次。
- B11-F 只验证 B10，不替代 B11-R，也不把可选写回变成基础依赖。

## 7. 存储和对象分层计划

| 对象 | 权威来源 | 是否写存档 | 允许谁修改 |
|---|---|---:|---|
| Worldbook Runtime Snapshot | 激活包 + manifest/index hash | 否，按激活重建 | Runtime loader |
| Campaign Activation | 当前存档选择的主世界观/扩展 | 是 | 玩家开局确认/代码 |
| Campaign Overlay | 玩家对既有知识的允许范围修改 | 是，可 CAS 导出复用 | 玩家 Overlay 命令 |
| NativeSocialSnapshot | Bannerlord 当前运行时 | 否，短生命周期缓存 | Native reader |
| AWAKE relationship state | AWAKE `trust/love/hostility` | 是 | AWAKE 代码命令 |
| WorldEventRecord | `WorldEventServices.Recorder` | 是/可重建 | 结构化事件生产者 |
| WeeklyReport | `WeeklyReportService` | 可保存或重建 | 代码生成器 |
| NPC knowledge memory | AWAKE typed memory entry | 是 | 玩家主张/事件传播/记忆整理代码 |
| AI Transcript | 对话记录 | 按现有契约 | 对话服务 |
| AI 建议 | Provider/Studio 临时结果 | 否，不是世界事实 | 编辑工具/开发者 |

### 7.1 推荐的玩家传授存储

v1 推荐复用 `awake.npc.memories` Storage 路径，但把知识主张作为明确类型的条目，不让它混入普通摘要：

```text
kind = knowledge_claim
epistemicStatus = heard_claim | skeptical | tentative | accepted_unverified | rejected | confirmed_by_event
confidence
sourceType = player | event | event_window_projection | native_observation
sourceId
visibility
firstSeenDay
lastVerifiedDay
causationId
idempotencyKey
retentionClass
```

如果离线容量/查询测试证明通用 memories 不适合，再单独立 `awake.npc.knowledge` namespace；不得在没有迁移计划时静默增加第二套存储。

### 7.2 Overlay 位置

推荐继续把玩家编辑既有世界知识的结果放在 Campaign Overlay/Storage 中，保持 CAS、导入、导出和版本冲突保护。Overlay 不能修改：

- 世界观身份；
- 世界书身份授予；
- 否定规则；
- 来源权威；
- NPC 私人记忆。

## 8. 已签收的六项决策

以下六项采用草案中的推荐方案，并由用户“把这些全部批次相关边界推进完”的授权整体签收。它们是后续实现的硬边界；如果实现证据推翻其中任一项，必须停批并重新审查，不得静默改义。

### D1：旧 `KnowledgeService` 的命运

**推荐**：NPC 正式运行时彻底不走旧路径；旧服务仅允许由离线测试组合根显式注入，禁止任何游戏正式组合根、开发者运行模式或调试运行时回退；测试注入必须标记 `legacy_fallback` 并默认 fail closed。

**理由**：当前旧权限回调恒为允许，存在绕过身份知识边界的污染风险。

### D2：玩家传授存储

**推荐**：v1 复用 `awake.npc.memories`，使用 typed knowledge entry；只有 profiling 证明容量/查询不足时，才创建独立 namespace。

**理由**：减少存档 schema 和启动 namespace 变化，同时保持来源、可信度和淘汰字段清晰。

### D3：周报是否传播给 NPC

**推荐**：周报文本只展示；NPC 知识传播直接读取结构化事件记录/周窗口，不读取渲染后的周报文本；只有明确可见性、地点、身份和长期价值的事件才写入目标 NPC。

**理由**：避免周报文本反向污染事实，也避免遍历全部 NPC 调 AI。

### D4：Overlay 正式持久化位置

**推荐**：Campaign Storage/WorldStateStore 为运行时权威，保留现有 CAS、导出/导入；不把 Overlay 复制进世界书包或 NPC 记忆。

**理由**：符合 AWAKE“战役可变状态走 Storage、内容包是只读权威”的边界。

### D5：首批关系适配范围

**推荐**：v1 的 native snapshot 固定为“玩家 → 当前对话 NPC”，DTO 保留 source/target 字段；NPC-NPC 方向性关系另立批次。

**理由**：当前 AWAKE 关系存档只有单个 `heroId`，直接扩大到 NPC-NPC 会引入新的存档迁移和规模问题。

### D6：原版写回时间

**推荐**：只读接入和知识机制稳定并完成 B11-R 的 `E4-R/E5-R` 后，再单独评估 B10；不纳入第一批实现。B10 若获批，使用专属 `E4-F/E5-F`，不与只读证据混称。

**理由**：写回会同时涉及原版关系、AWAKE 状态、事件、幂等、存档和回滚，必须独立审查。

## 9. 统一验收定义

每个批次必须用以下链路证明完成：

```text
入口
  → 真实 Caller
  → 代码结算/权限/持久化
  → 用户可观察结果
  → 失败/禁用/缺失数据回退
```

不接受以下作为功能完成证明：

- 类或方法存在；
- JSON/YAML 能解析；
- 编译通过但没有调用方；
- Prompt 中出现字段但代码没有门控；
- 周报文件生成但 NPC 没有受控传播；
- NPC 记忆有文本但没有来源/可信度/可见性；
- 旧 BuildId 的游戏日志；
- 没有读档/新进程验证的持久化声明。

## 10. 实施前门禁

在任何 B1–B10 代码修改前必须完成：

1. 使用独立只读 Codex 对本锁定计划进行对抗审查，最多 5 轮，记录 `VERDICT: APPROVED/REVISE`。
2. 若审查为 `REVISE`，只修正有证据的边界缺口，并在审查日志中记录采纳或拒绝理由。
3. 取得 `VERDICT: APPROVED` 后，首个实现租约只允许覆盖 `B1`；`B1-S`、`B2` 及后续批次必须分别排队并各自建立检查点。
4. 实现租约不得跨批次、跨版本、跨游戏目录同步或跨原版关系写回边界。
5. 每个批次都必须完成“入口 → 真实 Caller → 代码结算/持久化 → 可观察结果 → 失败回退”验证，才能进入下一个批次。

## 11. 非目标

- 不修改 Studio 世界书编辑器 A4 已完成批次。
- 不重写旧四版世界书内容。
- 不把旧 AF 世界书作为 Runtime 权威。
- 不在游戏运行时读取完整 YAML 或作者工作区全文。
- 不引入本地 embedding、tokenizer、ONNX 或游戏内模型。
- 不让本地 Worker 决定架构、权限、公式或发布。
- 不自动启动 Bannerlord、不自动同步游戏目录。
- 不在本计划内完成原版关系写回。
- 不把 NPC 私人记忆自动升级为世界书客观事实。

## 12. 推荐执行顺序

```text
当前：B0 共同合约、边界审查与独立只读复核
  ↓ `VERDICT: APPROVED`
B1 + B1-S + B2 只读基础、就绪/Storage 门与唯一查询
  ↓
B3 纯投影
  ↓
B4 对话只读接线
  ↓
B5 玩家传授       B6 事件/周报传播
       \           /
        B7 记忆分层/持久化
               ↓
         B8 AI/Token/污染门
                ↓
         B9 主动 NPC
                ↓
         B11-R 只读 E4-R/E5-R
                ↓
         B10 原版写回（可选、另行签收）
                 ↓
         B11-F 写回 E4-F/E5-F（仅启用 B10）
```

本文件被压缩或中断后，恢复顺序是：

1. 读取 `docs/checkpoints/AWAKE-NATIVE-KNOWLEDGE-BOUNDARY-20260823-checkpoint.md`。
2. 读取本文件。
3. 读取 `docs/PLAN-AWAKE-NativeState-Knowledge-MultiBatch-20260823-DISCOVERY-LOG.md`。
4. 不读取完整历史任务队列。
5. 只执行检查点中的唯一 `next_action`。
