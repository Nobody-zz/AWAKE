# AWAKE 世界知识独立闭环计划

- `task_id`: `AWAKE-KNOWLEDGE-INDEPENDENT-20260828`
- `goal_ref`: `01a02f11-7552-77c1-be7d-d847f72f1a77`
- `status`: `offline_verified`
- `plan_revision`: `10`
- `review_status`: `completed_revision_9_read_only_review`
- `review_verdict`: `APPROVED`（Revision 9 修复范围已完成；当前批最高证据为 E2）
- `revision_reason`: `2026-08-28` 完成 Revision 9 的 SessionEnding 异步观察、失败闭锁、single-flight drain、跨实例逻辑键互斥和 production smoke 清理；新增限定范围代码债务审查，未发现阻断本批的 P0/P1 债务。
- `completion_date`: `2026-08-28`
- `user_direction`: 用户已明确要求在 Marcus 内置期间继续独立推进世界知识系统；本计划只覆盖内容无关的底层闭环。
- `created_at`: `2026-08-28`

## 1. 本批目标

在不依赖具体世界观内容、不调用云端 AI、不启动 Bannerlord、不修改游戏目录的前提下，完成以下可观察闭环：

```text
事件结算
→ 稳定事件台账
→ 统一事件事实投影
→ 7 日周报确定性生成
→ 周报事实投影（同一 reportId 幂等）
→ NPC 知识查询可见
→ AI 上下文只消费已筛选知识
```

玩家对既有知识的 Overlay 编辑、导入和导出保持现有行为；本批不把玩家每次编辑变成 AI 调用，也不为每个 NPC 单独执行学习请求。

## 2. 已确认事实

1. Studio v2 编译、发布、AWAKE 完整性校验、加载和查询已有离线 E2；当前候选没有同步进游戏目录。
2. AWAKE 当前运行时已经有 `WorldKnowledgeLoader`、`WorldKnowledgeQueryService`、身份权限评估、Overlay、事件台账和 7 日周报。
3. 当前 runtime smoke 与 production boundary smoke 均通过；后者编译并调用 AWAKE 生产源代码，覆盖真实 `SubModule`/`ProbeExtension` 生命周期边界、事件台账、周报快照和动态知识投影。
4. 现行入口由 `AwakeEventBehavior` 的非阻塞知识刷新触发 `AwakeRuntime.EnsureKnowledgeReadyIfNativeReadyAsync`，再由 `WorldEventServices` 负责台账加载、逐周补报、存档状态更新和统一动态投影；不再把周报结果丢弃。
5. Marcus P3D-A0 已释放本批 `AWAKE\src` 世界知识生产写集；本批没有修改 Marcus、`AWAKE.csproj`、游戏目录、dist 或冻结候选。

## 3. 设计选择

### 3.1 动态知识采用“可重建事实源”

- 事件台账是动态事实的唯一来源。
- 周报是事件台账的确定性投影，不另建一份不可重建的长文本权威存储。
- 事件只在持久化确认后进入权威台账；`reportId` 的应用状态由战役存档状态统一持有，动态知识文本和投影缓存均可从台账重建。
- 事件事实和周报事实进入同一个知识投影接口，查询端不区分“静态包事实”和“事件产生事实”的读取方式，只按来源和权限筛选。

### 3.2 权威边界

| 内容 | 唯一权威 |
|---|---|
| 原版人物、家族、王国、城镇、身份投影 | Bannerlord 快照适配层 |
| 静态世界知识 | 已验证的 v2 内容包 |
| 事件发生记录 | `WorldEventLedger` |
| 周报文本和来源闭包 | `WeeklyReportService` |
| 动态知识可见性 | `WorldKnowledgeProjectionService` + 查询权限评估 |
| AI 请求与 Provider | Marcus；本批不接线 |
| 战役存档状态 | Marcus Storage / AWAKE 现有存储边界；本批不发明第二套存档系统 |

### 3.3 事件时间边界

- `day` 仍表示 Bannerlord 战役日，不把它当作现实年份。
- 周报按战役日的完整 7 日窗口生成：第 `n` 周覆盖 `[7n-6, 7n]`，例如第 1 周为 `[1,7]`、第 2 周为 `[8,14]`。
- `reportId` 必须由稳定的报告作用域和周序号确定性生成，不得使用时间戳、随机数或本次运行进程 ID。
- 生成器必须枚举所有已结束但尚未确认应用的周；不能只看“最近一周”或用单个游标跳过中间周。
- 同一个 `reportId` 的重复触发、重试或读档恢复只能产生一个逻辑投影。

### 3.4 `reportId` 应用状态契约

`reportId` 的“是否已应用”是战役存档状态，不是周报文本字段、内容包字段或进程内缓存标志。它必须只有一个权威持久化边界：

- 唯一存储边界是现有 AWAKE/Marcus Storage 的战役存档状态；不得把应用状态写入 `ModuleData`、世界书内容包、独立报告 JSON、日志或进程级缓存并将其当作权威。
- `WeeklyReportService` 只负责根据事件台账构造确定性报告；`WorldKnowledgeProjectionService` 只负责按稳定键幂等投影；两者都不拥有第二份 `reportId` 应用表。
- 最小逻辑结构如下，具体序列化形式服从现有 Storage 契约：

```json
{
  "schemaVersion": 1,
  "reports": [
    {
      "reportId": "<stable-scope>:weekly:<week-index>",
      "windowStartDay": 1,
      "windowEndDay": 7,
      "status": "applied|retryable",
      "attemptCount": 1,
      "lastAttemptDay": 7,
      "lastErrorCode": null
    }
  ]
}
```

- `reportId` 在该记录中必须唯一；`windowStartDay`、`windowEndDay` 用于审计和恢复，不得靠显示文本推断窗口。
- `applied` 只表示“报告投影已经完成且应用状态已经持久化确认”；`retryable` 或缺少记录都不表示成功，下一次安全入口必须仍可重试。
- `attemptCount`、`lastAttemptDay`、`lastErrorCode` 只用于诊断和调度，不能阻止必要重试；错误字段使用稳定机器码，不解析本地化文本。
- 报告正文、来源事件闭包和动态知识条目不作为这张状态表的第二份权威副本；它们必须从 `WorldEventLedger` 确定性重建，投影层使用 `reportId` 做替换/去重而不是追加。
- 读取时先恢复并校验该状态记录，再进行周报对账。存储读取失败、结构无效或出现同一 `reportId` 的冲突记录时，不得把 `_loaded` 标记为成功，也不得把未确认报告标为 `applied`；应保留可重试结果并在下一次安全生命周期重试。

#### 提交顺序与跨重载幂等

- 报告应用必须有一个明确的提交边界：只有投影结果和 `applied` 状态都被确认后，入口才能返回成功。
- 在投影完成但 `applied` 状态尚未持久化确认时崩溃，重载后按“未确认”处理，用同一个 `reportId` 重建并幂等替换，不生成新报告 ID，也不追加第二份知识。
- 在 `applied` 状态持久化确认后崩溃，重载后的正常对账应跳过重复应用；如果动态投影缓存已经丢失，只能用同一个 `reportId` 重建/替换，不能把“缓存重建”算成第二次报告。
- 持久化提交结果不明确时不得返回成功；应返回可观测的未知/可重试结果，下一次重试先按同一 `reportId` 对账，再决定补做或确认已完成。

### 3.5 事件持久化入口契约

事件只有在 `WorldEventLedger` 持久化确认后，才算进入动态知识闭环。入口必须返回稳定机器结果，不能把“已写入内存”伪装成“已持久化”：

| 入口结果 | 权威台账 | 动态投影/周报 | 后续动作 |
|---|---|---|---|
| `persisted` | 已确认写入 | 允许投影和纳入后续周报 | 正常继续 |
| `duplicate_confirmed` | 已存在且内容指纹一致 | 不重复投影 | 视为幂等成功 |
| `key_conflict` | 不接受新内容 | 不投影 | 修正冲突后使用新稳定键 |
| `persistence_failed` | 不得声称已写入 | 不得投影、不得纳入周报 | 在下一安全入口用同一 `eventKey` 重试 |
| `persistence_unknown` | 结果未确认 | 不得投影、不得纳入周报 | 先重读核对；不存在时用同一键重试 |

- 同一 `eventKey` 重试时必须携带相同的内容指纹；已存在且指纹一致只返回 `duplicate_confirmed`，指纹不同必须返回 `key_conflict`。
- 存储失败时可以记录诊断信息，但不能把事件加入权威台账、不能让查询看到该事件、不能让周报把它当作已发生事实。
- 事件持久化失败不得阻塞战役 tick；重试必须是有边界、可观察、非阻塞的安全入口行为。事件写入成功但后续投影失败时，台账仍是权威事实，投影可以独立按稳定键重试。
- 只有成功读取事件台账后才能判定某个周窗口“没有事件”；台账读取失败不能生成并确认一个伪造的空周报。

## 4. 最小实现切片

### A. 动态知识投影

新增一个薄接口和实现，职责仅限：

- 接收稳定事件事实和周报事实；
- 依据 `eventId` / `reportId` 幂等；
- 为查询服务提供有界、可排序的动态条目；
- 提供测试用清空和快照入口；
- 不执行 HTTP、AI、Embedding、数据库或游戏 tick 阻塞操作。

查询服务只保留一条动态事实读取路径，不在 `NpcDialogueService` 另写事件特殊分支。

### B. 事件事实可靠性

- 事件生产端传递稳定 `eventKey`、明确 `domain`、事件发生日和结算后的事实文本。
- 台账接受事件后只在唯一入口投影；重复键不再次投影。
- 存储失败不能伪装成持久化成功；内存已接受和持久化结果必须能区分。
- 读档失败保持可重试状态，不把失败标成已加载。
- 从存储重载时保留最近事件，而不是因数组方向错误丢弃最新记录。

### C. 周报投影和补报

- `AwakeEventBehavior` 只负责非阻塞触发知识刷新；周报生成、持久化和投影由 `WorldEventServices.EnsureKnowledgeReadyAsync` 统一负责。
- 周报完成后进入唯一动态投影入口，并保留报告来源事件闭包。
- 生成逻辑按战役日枚举所有已结束但尚未应用的完整 7 日窗口，支持跨过多个边界后的逐周补报。
- 报告投影失败记录稳定错误和下一次重试条件，不阻塞战役 tick。

### D. 固定离线验收

在现有 `tools/worldbook-runtime-smoke` 增加固定测试资料和断言，不引入真实卡拉迪亚内容：

1. 静态 v2 条目可加载并按身份/详细度筛选。
2. 事件结算进入台账后，查询可看到事件事实。
3. 同一 `eventKey` 重试不产生第二条事实。
4. 7 日周报生成后，查询可看到周报事实及来源 `reportId`。
5. 同一 `reportId` 重复投影不重复输出。
6. 错过第 7 天后在第 8 天补算完整周，不漏事件。
7. 存储加载失败不会永久锁定 `_loaded`，下一次可重试。
8. 超过内存容量后，重载仍保留最近窗口内的最新事件。
9. 无权限身份只能得到阻断或既有 referral，不会看到动态事实。
10. AI prompt builder 只接收查询服务筛选后的结果；动态事实不会绕过权限进入上下文。

> **测试证据限制：** `worldbook-runtime-smoke` 证明固定夹具下的核心查询、台账和周报契约；`worldbook-runtime-production-smoke` 进一步编译并调用当前 AWAKE 生产源代码，覆盖 `SubModule`/`ProbeExtension` 生命周期、Store drain、事件持久化边界和动态投影。两者都不能证明真实 Bannerlord 可达、真实 Marcus Storage 存读档、当前候选已同步或 E3/E4/E5。

## 5. 实施前置门

### 执行结果

- Marcus P3D-A0 checkpoint 已释放本批 `AWAKE\src` 世界知识生产写集。
- 用户已明确要求在 Marcus 内置期间独立推进世界知识系统；本批没有修改 Marcus。
- 生产代码、focused smoke、production boundary smoke 和代码债务审查均已完成；本计划现在进入 `offline_verified`，不再保留“仅能修改测试工具”的当前限制。

### 本批实际写入边界

- 生产路径：`AWAKE\src` 中的世界事件台账、周报/投影、Store 生命周期、Probe 收尾和后台任务调度相关文件。
- 验证路径：`AWAKE\tools\worldbook-runtime-smoke` 与 `AWAKE\tools\worldbook-runtime-production-smoke`。
- 文档路径：本计划、验收矩阵、代码债务审查和 checkpoint。

### 持续禁止

- 不修改 `AWAKE.csproj`、`SubModule.xml`、`ModuleData`、`dist`、Marcus `framework`、游戏目录、冻结候选和同步发布目标。
- 不启动 Bannerlord，不结束已有进程，不把离线证据包装成 E3/E4/E5。

## 6. 明确排除项

- 不制作真实卡拉迪亚知识内容，不迁移旧四版成人世界书。
- 不把 Warsails/DLC 数据写入默认核心；可选 DLC 仍由内容包/适配层决定。
- 不实现 NPC 主动逐个学习，不为 NPC 批量调用云端 AI。
- 不实现季度报告、长期记忆传播、语义向量检索或本地模型。
- 不修改 Marcus `framework`、Provider、IPC、Runtime Service、Storage 核心或 API Key 流程。
- 不修改 `AWAKE.csproj`、`SubModule.xml`、`ModuleData`、`dist`、冻结候选、游戏目录和同步脚本。
- 不启动 Bannerlord，不结束已有进程，不宣称 E3/E4/E5。

## 7. 完成定义

本批只有同时满足以下条件，才能标为 `offline_verified`：

- 事件、周报、知识投影和查询存在唯一生产路径；
- 静态事实与动态事实都经过同一身份/权限筛选；
- 重复事件、重复周报、错过边界、存储失败和重载方向均有确定性测试；
- 测试 harness 的通过结果与生产入口证据分开记录；不得用测试夹具通过替代生产接线、游戏内验证或存档验证；
- `tools/worldbook-runtime-smoke` 全部通过，且 Release 构建无 warning/error；
- 未修改 Marcus 当前锁定写集、游戏目录和冻结候选；
- checkpoint 明确记录未进行游戏内 E4、存读档 E5 和真实 Provider/Worker 验证。

## 8. 当前状态

- `plan_status`: `approved_ready_for_implementation`
- `review_status`: `completed_approved`
- `user_signoff_required`: `satisfied_by_explicit_autonomous_continuation_direction`
- `primary_executor`: 当前主线程；只读审查由独立子代理执行
- `minimum_evidence`: smoke 全部通过 + Release build 0 warning/error + fixed fixture assertions
- `open_decisions`: Marcus P3D-A0 释放 `AWAKE\src` 写集后，是否需要改 `WorldStateStore.cs` 由失败证据决定
- `blocked_by`: `none`；Marcus P3D-A0 已完成并释放本批 AWAKE 生产写集

## 12. Marcus 写集释放记录

- `release_checkpoint`: `docs/checkpoints/MARCUS-AWAKE-P3D-A0-20260827-checkpoint.md`
- `release_status`: `offline_verified`
- `release_lease`: `released`
- `release_evidence`: A0 Transport `6/6`、Framework Core `6/6`、Provider mapping `19/19`、Runtime Service `8/8`；独立 verifier 和七个固定 Release 构建均为 `0 warnings / 0 errors`。
- `released_scope`: 允许进入本计划列明的 `AWAKE\src` 世界知识生产实现；不改变 Marcus A0 已验证文件，不进入 P3D-A1 Provider bridge。
- `remaining_boundary`: 本批仍不修改 `AWAKE.csproj`、`SubModule.xml`、`ModuleData`、`dist`、游戏目录或冻结候选；不启动 Bannerlord，不宣称 E3/E4/E5。

## 9. 独立反审记录

- `reviewer_node`: `R5`
- `record_status`: `superseded_by_plan_revision_3`
- `verdict`: `APPROVED`（仅适用于 `plan_revision: 1`）
- `findings`: 修订前无必须修订项；`reportId` 持久化/崩溃语义以及测试 harness 的阶段例外均未包含在该审查结论内。
- `accepted_scope`: 修订前的离线设计、事件台账→周报→知识投影→查询闭环、固定 smoke 验收；不含真实世界书内容或 AI 接线。
- `blocked_scope`: Marcus P3D-A0 锁定写集、AWAKE 生产代码直改、Provider/Storage 核心、游戏目录、同步发布、Bannerlord E4/E5。
- `evidence`: 修订前计划已明确唯一权威路径、实施前置门、排除项和确定性验收；当前周报结果尚未进入知识查询的缺口被正确列为后续生产实现。

## 10. 修订复审门

- 本次 `plan_revision: 3` 改变了战役存档状态的所有权边界、事件入口的失败结果语义、周报补报算法、崩溃恢复契约以及 Marcus 写集期间允许修改的测试工具范围，属于需要重新独立只读审查的实质性契约变更。
- 需要重新独立审查；原 `R5 APPROVED` 不再覆盖本版本。测试 harness 例外与持久化契约应纳入同一次复审，不需要另造第二套审查流程。
- 在新的独立审查给出 `VERDICT: APPROVED` 前，不得据本修订版修改 `AWAKE\src`、生产接线、`AWAKE.csproj`、游戏目录、`dist` 或冻结候选。若项目既有批准门允许测试准备，则只可修改本计划明确列出的 smoke harness/固定夹具，且不得将其结果包装为生产入口证据。
- 重新审查至少应反驳或确认：单一 `reportId` 状态边界、`applied/retryable` 状态转换、事件持久化失败不入台账、day 7→21 逐周补报、三种崩溃时序、未知提交结果的重试行为，以及测试通过与生产接线证据的明确分离。

## 11. 修订 3 复审记录

- `reviewer_node`: `Russell`
- `review_status`: `completed`
- `verdict`: `APPROVED`
- `review_scope`: 只读核对 `reportId` 跨重载/崩溃语义、day 7→21 逐周补报、测试 harness 例外边界，以及测试证据和生产入口证据的分离。
- `findings`: 无 P0/P1/P2 必须修订项。
- `evidence`: 计划已明确唯一战役存储边界、`applied/retryable` 状态、三种崩溃恢复语义、逐周完整窗口 `[1,7]`/`[8,14]`/`[15,21]`，并限制测试 harness 只能作为 E2 离线证据；未将其包装为生产入口、游戏内或存档证据。
- `implementation_gate`: 仅开放计划列明的 `tools\\worldbook-runtime-smoke` 固定夹具和测试 harness；`AWAKE\\src`、生产接线、Marcus framework、游戏目录、`dist`、冻结候选仍被禁止。

## 13. Revision 4 — 实现后 P1 修复边界

- `plan_revision`: `4`
- `status`: `implementation_in_progress`
- `trigger`: 实现后独立只读审查返回 `VERDICT: REVISE`，发现跨战役泄漏、动态权限过宽、生产入口仍使用模糊同步包装、报告内容不可稳定重建等问题。
- `scope`: 只修复本批世界知识闭环的状态隔离、可见性、持久化结果语义和周报快照；不新增内容、不改 Marcus、不改 Studio、不改发布或游戏目录。

### 13.1 必须修复

1. 战役切换时通过唯一 `WorldEventServices.ResetForCampaign()` 清空事件台账、动态投影和报告源。
2. 为台账和知识刷新增加独立战役代际；异步存储完成后若代际已变化，只返回 `stale_session`，不得写入当前台账或投影。
3. 事件记录携带显式身份可见性；未提供细粒度配置时使用固定、可审计的默认身份白名单，不把未来新增身份自动授予知识。
4. 生产同步回调改为调用明确命名的异步排队入口 `QueueRecord()`，由其观察并记录 `RecordAsync()` 的持久化结果；不再以 `bool Record()` 暗示已经保存。
5. 周报应用状态保存生成后的报告快照；重载时优先恢复已应用快照，避免台账容量淘汰后同一 `reportId` 静默变空或改写。
6. `OwnerRetryable` 对外保留为可重试状态；未知提交结果不进入权威台账，使用同一幂等键等待调用方或后台入口重试。
7. 只有 `NativeReadinessStatus.Ready` 且代际仍匹配时，才启动知识恢复任务。

### 13.2 验收补充

- 重置战役后，旧事件和旧周报不能出现在新查询结果中。
- 旧代际异步写入完成后，不能改变新代际台账、投影或查询 revision。
- 明确身份白名单外的身份不能读取动态事件；指定身份仍可读取。
- 同一 `reportId` 重复恢复不产生第二条报告，且已应用快照内容保持不变。
- 生产调用链静态扫描不再出现 `Recorder.Record(...)` 或 `WorldEventLedger.Record(...)`。
- Release build、runtime smoke、唯一入口扫描和变更范围代码债务审查全部通过。

### 13.3 明确不做

- 不在本 revision 推导每个事件的完整角色/家族/地点传播模型；缺少来源信息时采用保守白名单。
- 不实现迟到事件的 correction report；已应用 `reportId` 内容固定，未来修正使用新的报告 ID。
- 不把 smoke 假存储包装为真实 Marcus Storage、Bannerlord 存读档或 E4/E5 证据。
- 不修改 `AWAKE.csproj`、`SubModule.xml`、`ModuleData`、`dist`、游戏目录、冻结候选和 Marcus framework。

### 13.4 当前执行门

- `user_signoff_required`: `satisfied_by_explicit_autonomous_continuation_direction`
- `review_status`: `pending_revision_4_read_only_review`
- `primary_executor`: 当前主线程
- `minimum_evidence`: 上述补充验收全部通过 + AWAKE Release `0 warnings / 0 errors` + runtime smoke `exit 0`

## 14. Revision 5 — 并发快照与存储实例失效修复

- `plan_revision`: `5`
- `status`: `implementation_in_progress`
- `trigger`: revision 4 实现后独立只读复审返回 `VERDICT: REVISE`，确认存在同代际旧快照覆盖新快照、旧 `WorldStateStore` 未显式失效，以及 smoke 使用替身无法覆盖生产边界的问题。
- `scope`: 只修复本批世界知识闭环的快照单调性、存储实例生命周期和生产边界离线验证；不新增世界知识内容，不改 Marcus framework、Worldbook Studio、发布候选或游戏目录。

### 14.1 必须修复

1. 台账在记录、加载、战役重置时维护单调 `ledger revision`；快照必须与 revision 原子捕获，动态投影只接受不小于当前已应用快照 revision 的提交。
2. `AwakeRuntime` 的战役重置、会话结束、存储替换和释放路径，在断开旧实例前调用幂等的 `WorldStateStore.BeginSessionEnd()`；旧实例的异步读写在关键 await 前后都必须识别已结束状态，不能继续提交为当前结果。
3. 旧实例需要保留既有正常结束的 drain 入口，但不得被新战役或新实例重新引用；重复失效不得重复写入收尾命令。
4. 增加使用生产 `WorldStateStore` 生命周期代码的离线边界 harness，至少覆盖旧实例失效、异步写入完成后的拒绝、实例替换和快照倒退；原有 stub smoke 继续保留为快速逻辑回归，不得冒充生产边界证据。

### 14.2 验收补充

- 同一代际中，revision 较小的加载或周报恢复完成后，不能覆盖 revision 较大的动态投影。
- 新事件先投影后，迟到的旧快照提交不能使该事件从查询结果消失。
- 战役重置、正常会话结束、存储替换和释放后，旧实例 `SessionEnded` 为真且新的写入返回明确的结束/重试结果。
- 旧实例异步操作即使在 `BeginSessionEnd()` 前已经越过首次检查，也不能在结束后把结果当作当前实例成功提交。
- 生产边界 harness 使用实际 `WorldStateStore` 生命周期实现；stub smoke 与生产 harness 的证据分开记录。
- AWAKE Release 构建、两个 smoke/harness、唯一投影入口扫描和变更范围代码债务审查全部通过；若 Marcus Framework 并行写集使完整构建暂不可复现，必须单独标记环境阻塞，不得用旧 DLL 冒充本批构建证据。

### 14.3 明确不做

- 不修改 Marcus framework 的源代码、项目文件或构建产物；其并行写集只作为外部构建前置条件。
- 不把生产边界 harness 包装为 Bannerlord 实机、真实存档或 E3/E4/E5 证据。
- 不改变事件容量、周报窗口、身份白名单或内容生成策略，除非修复上述竞态必须带来的最小契约调整。

### 14.4 当前执行门

- `user_signoff_required`: `satisfied_by_explicit_autonomous_continuation_direction`
- `review_status`: `pending_revision_5_read_only_review_after_clarification`
- `primary_executor`: 当前主线程；独立复审代理只读审查
- `minimum_evidence`: revision 5 验收补充全部通过 + AWAKE Release 可复现或明确记录外部构建阻塞 + production boundary harness `exit 0`

### 14.5 复审补充契约

1. **版本令牌。** `WorldEventLedgerSnapshot` 同时携带 `campaignGeneration`、数据 `ledgerRevision` 和投影 `snapshotRevision`；前两者用于判断快照是否仍属于同一战役及是否可提交，后者是跨异步完成顺序的单调投影令牌。三者在 `Records` 锁内原子捕获。`WorldKnowledgeProjectionService` 只接受同一代际且 `snapshotRevision >= appliedSnapshotRevision` 的提交；战役重置清除投影并提升代际，旧代际永远拒绝。周报状态变化也必须沿用该快照令牌，不用单独的未受保护时间戳。
2. **投影比较点。** `TryProjectEventsIfReady` 与 `TryProjectSourcesIfReady` 在 `CampaignBoundaryGate` 内先验证代际、存储实例、Native readiness，再把快照令牌交给 Projection；Projection 在自己的锁内再次比较已应用令牌，拒绝旧快照且不递增查询 revision。事件记录、加载和周报恢复都必须从同一个原子快照对象提交，禁止重新用裸事件列表拼装版本。
3. **存储结束态机。** `WorldStateStore` 的结束状态分为 `Active → Ending → Ended`：`BeginSessionEnd()` 幂等地从 `Active` 转为 `Ending`，只在首次转换时封存新的业务写入口并把已有内存预留转为允许收尾的 pending writes；`BeginFinalDrainAsync()` 是唯一允许继续处理这些已接纳命令的收尾入口，完成后转为 `Ended`。`TryEnqueue`、事件追加、周报状态更新和读取在 `Ending/Ended` 拒绝新业务操作；已经进入 drain 的底层请求可以完成，但返回给调用方必须带“结束期间结果不可作为当前战役成功”的明确状态，不能把旧实例结果装入当前台账/投影。`SetAsync` 已发出后的物理提交无法撤回，只能由代际/实例校验丢弃其业务结果。
4. **旧实例替换。** `AwakeRuntime` 在战役重置、会话结束、设置新存储、释放旧存储和 `EnsureWorldStateReadyAsync` 替换实例时，先在边界锁内摘除旧引用，再在锁外调用旧实例的幂等 `BeginSessionEnd()`；新实例安装前不复用旧引用。正常会话结束仍由 `ProbeExtension` 启动唯一 final drain；重复调用只返回已有结束状态，不重复追加收尾命令。
5. **真实边界 harness。** 新增 `tools/worldbook-runtime-production-smoke/` 独立项目，编译实际 `AWAKE/src/AwakeRuntime.cs`、`AWAKE/src/WorldStateStore.cs` 及其最小必要世界知识调用链，使用只替代 Marcus 外部宿主/存储网络的测试适配器；不得重新定义 `AwakeRuntime` 或 `WorldStateStore`。入口必须实际调用 `AwakeRuntime.SetWorldStateStore`、`EnsureWorldStateReadyAsync` 的替换分支、`ResetSessionStateForCampaign`、`BeginSessionEnd` 和 `ReleaseWorldStateStore`，并观察真实实例的 `SessionEnded`、持久化结果和投影令牌。
6. **生产边界测试矩阵。** harness 至少覆盖：旧实例在 await 前结束、await 后结束、设置新实例、战役重置、重复结束、final drain 收尾；旧事件写入不得进入新台账，旧周报写入不得覆盖新报告，旧加载不得清空新事件，旧投影不得回退查询结果。所有测试记录为 E2 离线生产代码边界证据，不宣称 Bannerlord、真实 Marcus 存储或 E3/E4/E5。

### 14.6 复审记录

- `reviewer_node`: `Halley`
- `review_status`: `completed_revise`
- `verdict`: `REVISE`
- `findings`: 要求明确版本令牌与比较锁、`Active/Ending/Ended` 结束态机及 final drain 例外、真实 `AwakeRuntime + WorldStateStore` harness 的入口和测试矩阵。
- `next_review`: 仅审查本节补充后的 revision 5 计划；通过前不修改生产代码或新增 harness。

### 14.7 第二轮复审补充

1. **失效顺序。** 所有旧实例替换点都在同一次 `CampaignBoundaryGate` 临界区内执行：先调用旧实例幂等的 `BeginSessionEnd()`，使其从 `Active` 原子转入 `Ending`，再摘除 `AwakeRuntime._worldStateStore` 引用；若旧实例与新实例相同则不重复处理。`BeginSessionEnd()` 内部的 `_gate` 与业务入队共用同一状态检查，因此异步任务要么在失效前被接纳并交给 final drain，要么在失效后被拒绝，不允许出现“已摘除但仍 Active”的窗口。
2. **早期重置统一入口。** `SubModule.ResetCampaignState()` 不再直接调用 `WorldEventServices.ResetForCampaign()`，改为调用 `AwakeRuntime.ResetSessionStateForCampaign()`；该唯一入口在同一边界内先失效旧 Runtime store，再重置 Runtime 会话代际，最后重置事件台账和动态投影。`ProbeExtension.CampaignSessionReady` 继续调用同一幂等入口以覆盖框架会话就绪时序；harness 必须分别调用并验证两条真实生产入口，不得只测后者。
3. **生产入口矩阵新增。** 真实边界 harness 增加 `SubModule.ResetCampaignState` 的可测试包装/等价生产调用路径，以及 `ProbeExtension.CampaignSessionReady` 使用的统一 Runtime reset；验证任一入口执行期间旧实例先成为 `Ending/Ended`，事件代际与 Runtime 代际同步推进，且不会留下只清 ledger 不清 Runtime store 的中间状态。

## 15. Revision 6 — 实现级复审修复边界

- `plan_revision`: `6`
- `status`: `needs_review`
- `trigger`: Revision 5 实现完成后，独立只读复审返回 `VERDICT: REVISE`，确认存在真实生产生命周期重复重置、旧实例共享存储写入顺序和收尾入口覆盖不足；同时确认 production smoke 的回调清理会使后续测试退化为空操作。
- `scope`: 只修复世界知识闭环依赖的 Runtime/Store 生命周期边界和对应离线证据；不新增知识内容、不改变权限策略、周报窗口、容量、Marcus、Studio、发布候选或游戏目录。

### 15.1 必须修复

1. **CampaignSessionReady 只重置一次。** `ProbeExtension` 不再先直接 reset 再调用 `CampaignResetLifecycle.Reset`；当真实 `SubModule` 回调已安装时只走该回调，未安装时才使用 Runtime fallback。回调默认值改为可检测的未安装状态；production smoke 必须安装真实 `SubModule` 回调并断言 Runtime/ledger 代际只推进一次。
2. **旧/新 Store 的读改写顺序。** `WorldStateStore` 的命令状态读改写使用进程内共享异步互斥，覆盖 `GetAsync → 变换 → SetAsync` 的完整区间，使同一进程内旧 Store 的迟到写入不能越过新 Store 的写入顺序；命令已在 Ending 前接纳的收尾写仍可完成，但不得把结果提交为当前台账/投影成功。production smoke 增加两个 Store 共享同一底层键值空间的迟到 `SetAsync` 场景。
3. **替换/重置完成收尾。** `EnsureWorldStateReadyAsync` 替换旧 Store 前等待旧实例的 final drain；同步的 `SetWorldStateStore`、`ClaimWorldStateStore` 和战役 reset 路径在失效旧实例后安排幂等 final drain，不在游戏 tick 中同步等待外部存储。旧实例仍先失效再摘除，新的 Store 不复用旧引用。
4. **测试入口与清理。** production smoke 的每个测试从干净的 `CampaignResetLifecycle` 状态开始；`TestProbeCampaignReadyBoundaryAsync` 必须通过真实 `SubModule.OnSubModuleLoad` 安装回调，并验证单次 reset、旧 Store 生命周期和 readiness 代际；增加直接 `SetWorldStateStore` 替换和真实 SessionEnding final drain 的断言。

### 15.2 验收标准

- `CampaignSessionReady` 在真实回调已安装时 Runtime/ledger generation 各只增加一次；没有回调时 fallback 仍能完成一次 reset。
- 共享底层键值空间中，旧 Store 在 `GetAsync` 阶段被替换后，不得在新 Store 已写入之后再覆盖其状态；所有迟到结果均可观测且不进入当前动态知识台账。
- 替换、战役 reset、release 和真实 SessionEnding 都能观察到旧 Store 最终进入 `Ended`，重复 final drain 不重复执行收尾命令。
- 两个 smoke 与 production boundary harness 均通过；测试必须调用真实 `AwakeRuntime`、`WorldStateStore`、`SubModule` 和 `ProbeExtension` 类型，不得用同名替身取代生产实现。
- AWAKE 隔离 Release 构建通过；正式完整构建若仍因 Marcus `netstandard 2.0` 引用阻塞，单独记录外部阻塞，不修改 Marcus 文件。

### 15.3 明确不做

- 不把共享异步互斥扩展成跨进程或跨存档分布式锁；真实 Marcus Storage 的跨进程并发语义留给框架契约。
- 不在同步生命周期回调中阻塞等待网络、文件或数据库；reset/release 只安排 final drain，异步替换入口才等待其完成。
- 不删除历史 `awake_last_weekly_report_day` 存档 key；其是否作为 legacy 兼容字段另立存档迁移批次，本批不改变保存契约。

### 15.4 Revision 6 执行门

- `user_signoff_required`: `satisfied_by_explicit_autonomous_continuation_direction`
- `review_status`: `pending_revision_6_read_only_review`
- `primary_executor`: 当前主线程；Halley 只读复审
- `minimum_evidence`: Revision 6 复审 `APPROVED` + 两个 smoke exit 0 + 真实 production boundary harness exit 0 + AWAKE 隔离 Release 构建通过 + 变更范围代码债务审查

## 16. Revision 7 — 完成屏障与跨实例写入栅栏

- `plan_revision`: `7`
- `status`: `needs_review`
- `trigger`: Revision 6 设计复审确认“共享互斥 + 后台安排 drain”仍不能证明旧实例在新 Store 写入前完成收尾，也不能证明旧实例迟到的读改写不会覆盖共享底层状态。
- `scope`: 只补齐 Revision 6 的可等待语义和跨实例写入顺序；不改变世界知识内容、查询权限、周报算法、Marcus、Studio、发布候选或游戏目录。

### 16.1 必须修复

1. **final drain 单例任务。** `WorldStateStore.BeginFinalDrainAsync()` 改为 single-flight：首次调用创建并保存实际 drain `Task`，后续调用都返回同一个任务并等待其结束；只有该任务的 finally 将状态置为 `Ended`。测试必须验证第二次调用在第一次未完成时不会提前返回。
2. **跨实例逻辑存储键栅栏。** 新增进程内 `ConcurrentDictionary<string, SemaphoreSlim>`，键只由逻辑 `namespaceId + key` 组成，所有 `WorldStateStore` 实例共享同一把锁；`TryApplyAsync` 必须持锁覆盖完整 `GetAsync → 状态变换 → SetAsync`。这样旧 Store 若先取得锁，则新 Store 只能在旧写入结束后重新读取；新 Store 若先取得锁，旧 Store 的迟到操作只能在锁内读取新状态后再按幂等规则处理，不能用旧读快照覆盖新写入。该锁不承诺跨进程/跨实例外部写者一致性。
3. **reset 的 drain 屏障。** `AwakeRuntime` 保存最近一次旧 Store 的 final-drain task；`ResetSessionStateForCampaign` 先让旧 Store 进入 Ending 并创建/保存该 task，再摘除旧引用。后续 `EnsureWorldStateReadyAsync` 在创建或安装新 Store 前必须等待该屏障；若 reset 后又发生新一轮 reset，只保留并等待当前代际对应的最新屏障。
4. **同步 setter 不再伪装成即时替换。** `AwakeRuntime.SetWorldStateStore` 和 `ClaimWorldStateStore` 改为可等待的异步入口（保留方法名，返回 `Task`/`Task<WorldStateStore>`）；它们在替换旧实例后等待同一个 final-drain task，再安装新实例。所有现有调用点必须 `await`，不允许通过 `.Result` 或 `GetAwaiter().GetResult()` 绕过屏障。当前 production harness 的 `Install` 改为异步并等待真实入口。
5. **Probe 单次 reset 和真实清理。** `CampaignResetLifecycle.Reset` 默认设为未安装状态；`CampaignSessionReady` 先选择并执行唯一 reset 回调，再创建 readiness task。若没有回调才直接调用 Runtime reset。harness 的每个测试 `finally` 恢复回调、等待已启动的 drain/continuation，并在 Probe 测试中实际安装 `SubModule.OnSubModuleLoad` 回调。

### 16.2 验收标准

- 重复 `BeginFinalDrainAsync()` 返回同一个任务对象；第二个 await 只有在 Store `Ended` 后才完成。
- 共享同一逻辑键的两个 Store 的所有读改写不会交叉；旧 Store 的 blocked `GetAsync` 释放后不能覆盖新 Store 已写入的字段，旧结果不会进入新代际台账或投影。
- reset、Ensure 替换和直接异步 setter 都能观察到：旧实例 `Ending → Ended`，屏障完成后新实例才可接受业务写入。
- `CampaignSessionReady` 在真实 SubModule 回调已安装时仅发生一次 Runtime/ledger reset；测试之间不存在空操作回调污染或未等待后台任务。
- 两个 smoke、生产 boundary harness、AWAKE 隔离 Release 构建和限定范围代码债务审查全部通过；完整构建若仍被 Marcus `netstandard 2.0` 外部引用阻塞，保持单独标记。

### 16.3 明确不做

- 不在同步 lifecycle 回调中阻塞等待外部存储；需要等待的替换 API 统一为 async，reset 通过下一次异步 ready 入口等待屏障。
- 不新增第二份事件台账、报告状态表或运行时知识缓存；本修复只改变写入顺序和生命周期完成语义。
- 不删除 `awake_last_weekly_report_day` 等历史保存 key；其存档兼容清理另立批次。

### 16.4 Revision 7 执行门

- `user_signoff_required`: `satisfied_by_explicit_autonomous_continuation_direction`
- `review_status`: `pending_revision_7_read_only_review`
- `primary_executor`: 当前主线程；Halley 只读复审
- `minimum_evidence`: Revision 7 复审 `APPROVED` + 两个 smoke exit 0 + production boundary harness exit 0 + AWAKE 隔离 Release 构建通过 + 代码债务审查

## 17. Revision 8 — 多实例收尾屏障与可观察失败语义

- `plan_revision`: `8`
- `status`: `needs_review`
- `trigger`: Revision 7 设计复审确认只保存“最新 drain”会丢失早期 reset 的未完成收尾；同步 release 未纳入屏障；重复 final drain 的失败/未清空语义不可观察；harness 也没有真实追踪和等待后台 continuation 的机制。
- `scope`: 只闭合 Revision 7 的完成语义和测试隔离；不新增世界知识内容、不改变事件/周报/权限算法、不修改 Marcus、Studio、发布候选或游戏目录。

### 17.1 必须修复

1. **累积所有退役 Store 的屏障。** `AwakeRuntime` 不再用单个“最近 drain”覆盖旧任务，而是维护当前进程内所有未完成退役 Store 的 `Task<WorldFinalDrainResult>` 集合；每次 reset、替换或 release 都加入对应任务，`EnsureWorldStateReadyAsync`、异步 setter 和异步 claim 在安装/返回新 Store 前等待集合中全部未完成任务。已完成任务可以在边界锁内清理，但不得丢弃仍未完成的早期代际屏障。
2. **final drain 返回可复用结果。** `WorldStateStore.BeginFinalDrainAsync()` 改为 single-flight `Task<WorldFinalDrainResult>`：后续调用返回同一个任务对象；结果至少包含 `Succeeded`、`PendingWrites`、`PendingEvents`、`DroppedItems` 和稳定 `ErrorCode`。底层异常、重试耗尽或仍有 pending 项时任务仍完成但 `Succeeded=false`，Store 最终进入 `Ended`；调用方必须记录失败，不能把“任务结束”当成“数据已成功收尾”。
3. **所有替换与 release 纳入等待。** `SetWorldStateStore`、`ClaimWorldStateStore` 和 `ReleaseWorldStateStore` 改为 async 入口，并等待被替换/释放实例的同一 final-drain task；`ResetSessionStateForCampaign` 在同步生命周期中只启动并登记 drain，后续 `EnsureWorldStateReadyAsync` 必须等待全部登记屏障后才能打开新 Store。正常 `BeginSessionEnd` 也登记其 Store 的 drain，`ProbeExtension.SessionEnding` 复用并等待同一任务，不另起第二条收尾路径。
4. **跨实例读改写栅栏保持不变但明确范围。** 所有 `WorldStateStore` 实例按逻辑 `namespaceId + key` 共享进程内异步互斥，完整覆盖 `GetAsync → 状态变换 → SetAsync`；旧实例允许完成在 Ending 前已接纳的命令，但新实例只有在旧实例 drain 完成后才接受业务写入，且旧代际结果仍不能进入当前台账/投影。
5. **Probe 与 harness 可验证。** `CampaignSessionReady` 必须先执行唯一真实 reset 回调，再创建 readiness task；默认未安装状态可检测。production smoke 的 `AwakeBackgroundTask` 增加仅测试用的任务观察钩子；每个测试在 `finally` 恢复回调、等待所有被观察的后台任务和所有退役 Store drain，失败路径也必须释放 barrier，不允许把遗留后台任务带入下一项测试。

### 17.2 验收标准

- 连续两次 reset 产生的 D1、D2 都被下一次 ready 等待；D1 未完成时不能只等待 D2。
- 重复 final drain 返回同一 `Task<WorldFinalDrainResult>`；第二个 await 只有在 Store `Ended` 后完成，并能区分成功、丢弃和仍 pending。
- 直接异步 setter/claim/release 和 Ensure 替换都在旧实例 drain 任务完成后才允许新 Store 写入；reset 后的下一次 Ensure 等待所有退役任务。
- 共享逻辑键的两个 Store 不发生 stale `Get→Set` 覆盖；旧实例迟到结果可观察为 stale/失败，不进入新代际动态知识。
- `CampaignSessionReady` 的真实 SubModule 回调只执行一次 reset；每个 harness 测试结束时后台任务集合为空或全部已完成，回调恢复为未安装状态。
- 两个 smoke、production boundary harness、AWAKE 隔离 Release 构建和限定范围代码债务审查全部通过；完整构建若仍被 Marcus `netstandard 2.0` 外部引用阻塞，单独记录。

### 17.3 明确不做

- 不把屏障扩展为跨进程分布式锁；外部进程并发由 Marcus Storage 契约负责。
- 不在同步 lifecycle 回调中等待网络/文件/数据库；同步路径只登记任务，所有等待发生在 async ready/setter/release 入口或测试清理。
- 不删除历史 `awake_last_weekly_report_day` 保存 key，不改变存档迁移契约。

### 17.4 Revision 8 执行门

- `user_signoff_required`: `satisfied_by_explicit_autonomous_continuation_direction`
- `review_status`: `pending_revision_8_read_only_review`
- `primary_executor`: 当前主线程；Halley 只读复审
- `minimum_evidence`: Revision 8 复审 `APPROVED` + 两个 smoke exit 0 + production boundary harness exit 0 + AWAKE 隔离 Release 构建通过 + 代码债务审查

## 18. Revision 9 — SessionEnding、失败闭锁与完整写入清单

- `plan_revision`: `9`
- `status`: `needs_review`
- `trigger`: Revision 8 设计复审确认 SessionEnding 的同步/异步要求冲突，drain 失败后是否允许新 Store 激活未定义，且不能只凭 `TryApplyAsync` 覆盖 `WorldStateStore` 内全部直接存储写入。
- `scope`: 只补齐生命周期失败语义、收尾任务观察和存储写入审计；不新增知识内容、不改变权限/周报算法、不修改 Marcus、Studio、发布候选或游戏目录。

### 18.1 必须修复

1. **SessionEnding 采用异步观察，不阻塞生命周期回调。** `ProbeExtension.OnLifecycle(SessionEnding)` 只在同步入口内完成 `BeginSessionEnd`、取得/登记唯一 final-drain task 并启动后台观察；不调用同步等待，不把 `SessionEnding` 标记为“已完成收尾”。观察任务必须记录 `WorldFinalDrainResult`，成功、丢弃、仍 pending 和异常分别使用稳定日志码。`ReleaseWorldStateStore` 的真正等待发生在该异步观察 continuation 中或专用 async 入口中。
2. **失败屏障 fail closed。** `WorldFinalDrainResult.Succeeded=false` 或存在 `PendingWrites/PendingEvents/DroppedItems` 时，退役 Store 仍进入 `Ended` 但其 drain task 标记失败；`AwakeRuntime` 的 drain barrier 聚合结果必须可查询。`EnsureWorldStateReadyAsync`、异步 setter 和 async claim 等待全部屏障后，若任一失败则记录稳定错误并拒绝安装/返回新 Store；只有全部屏障成功或没有退役 Store 才允许激活新 Store。下一次安全入口可重试同一已失败任务，但不得把失败转换成成功。
3. **single-flight 结果任务。** `BeginFinalDrainAsync()` 保存并返回同一 `Task<WorldFinalDrainResult>`；第二次调用不得提前返回。任务不抛出未观察异常，所有异常转为失败结果并写入 `ErrorCode`；`Ended` 只代表收尾尝试结束，不代表成功。
4. **跨实例逻辑键互斥覆盖清单。** 建立 `WorldStateStore` 直接存储调用清单：`TryApplyAsync` 的命令读改写和 `WriteEmptyMemoryAsync` 的直接写入都必须经过按 `namespaceId + key` 获取的共享异步互斥；其余 `GetAsync` 调用逐一标记为只读、前置诊断读取或由命令再次 RMW，不得存在未分类的直接 `SetAsync/DeleteAsync`。新增或修改写入时必须同时更新该清单/测试断言。
5. **多退役任务与 harness 清理。** Runtime 保存所有未完成退役 Store 的 drain task，reset 连续发生时 D1、D2 都必须等待。`AwakeBackgroundTask` 提供仅测试用的任务观察钩子；harness 每项测试的 `finally` 恢复 `CampaignResetLifecycle`、释放所有 storage barrier、等待观察到的后台任务和退役 drain 完成，再开始下一项。

### 18.2 验收标准

- `SessionEnding` 返回后可以观察到后台 drain 仍在运行；观察完成后才能调用 release，且不会阻塞同步生命周期线程。
- 任一退役 drain 失败时，下一次 `EnsureWorldStateReadyAsync`/async setter/claim 都返回失败且不安装新 Store；所有旧 drain 都完成后才允许重试。
- 重复 final drain 得到同一结果任务；结果字段能区分成功、pending、drop 和错误。
- 代码扫描能列出 `WorldStateStore.cs` 全部 `GetAsync/SetAsync/DeleteAsync` 调用，并证明唯一直接写入与所有命令 RMW 都走逻辑键互斥。
- 连续 reset 不丢失任何早期 drain 屏障；production smoke 的真实 Probe/SubModule 路径和后台任务清理在失败路径也可重复执行。
- 两个 smoke、production boundary harness、AWAKE 隔离 Release 构建和限定范围代码债务审查全部通过；完整构建如仍被 Marcus `netstandard 2.0` 阻塞，单独记录。

### 18.3 明确不做

- 不在 `SessionEnding` 同步回调中调用 `.Wait()`、`.Result` 或 `GetAwaiter().GetResult()` 等待外部存储。
- 不把 drain 失败吞掉后继续激活新 Store，不修改旧存档 key，不引入第二个持久化状态来源。
- 不把只读查询强行串行化；仅对完整读改写和直接写入使用共享逻辑键互斥。

### 18.4 Revision 9 执行门

- `user_signoff_required`: `satisfied_by_explicit_autonomous_continuation_direction`
- `review_status`: `completed_revision_9_read_only_review`
- `primary_executor`: 当前主线程；独立子代理只读复审返回 `VERDICT: APPROVED`
- `minimum_evidence`: Revision 9 复审 `APPROVED` + 两个 smoke exit 0 + production boundary harness exit 0 + 隔离生产源代码构建通过 + 代码债务审查

## 19. Revision 10 — 离线闭环完成归档

- `plan_revision`: `10`
- `status`: `offline_verified`
- `review_status`: `completed`
- `review_verdict`: `APPROVED`
- `scope_result`: 事件结算 → 持久化事件台账 → 逐周周报 → 动态知识投影 → 身份权限筛选的离线生产源代码闭环已完成。

### 19.1 已完成

1. 事件台账、周报状态和动态知识投影共用唯一生产路径；重复 `eventKey`/`reportId` 不重复产生知识。
2. 周报按完整 7 日窗口逐周补报，跨越多个边界时不跳过中间窗口；已应用报告快照可重载和修复。
3. 动态事件和周报进入 `WorldKnowledgeQueryService`，沿用静态知识的身份、范围、详细度和内容闸门。
4. 会话结束采用异步 drain 观察；旧 Store 的迟到读写、直接写入、连续 reset 和失败 drain 均有明确隔离/闭锁语义。
5. `KnowledgeService` 的旧指纹 `SetAsync` 已完成边界审查：当前没有生产调用者，不纳入现行战役存储清单；未来若重新接线必须单独建迁移批次。

### 19.2 当前证据

- `WorldbookRuntimeSmoke`：`Release-r16` 构建 `0 warnings / 0 errors`，运行通过，exit `0`。
- `WorldbookRuntimeProductionSmoke`：`Release-r16` 构建 `0 warnings / 0 errors`，`15/15 PASS`。
- 限定范围代码债务审查：`docs/evidence/AWAKE-KNOWLEDGE-INDEPENDENT-CODE-DEBT-20260828.md`，未发现阻断本批的 P0/P1 债务。
- 正式 `tools/build.ps1`：未通过，阻塞点位于 Marcus Framework 编译缺少 `netstandard, Version=2.0.0.0` 引用；该错误属于外部构建环境/框架项目，不修改 Marcus、不复制旧 DLL，也不影响上述隔离生产源代码 smoke 结果。

### 19.3 明确未完成

- 未生成或同步新的真实世界观内容包；当前 `ModuleData`、`dist` 和游戏目录未改动。
- 未启动 Bannerlord，未取得 E3 游戏入口、E4 实机或 E5 存读档/长时证据。
- 未验证真实 Marcus Storage、云端 Provider、Worker、NPC 对话完整 AI 请求出口。

### 19.4 下一动作

本计划到此完成。下一批应单独建立“真实 v2 世界知识内容迁移/包候选”计划；解决 Marcus Framework 的 `netstandard` 构建环境后，再分别取得匹配 BuildId 的 E3/E4/E5 证据。不得把本批离线结果直接同步进游戏目录或宣称发布版完成。
