# Worldbook Studio AI 批量作者修正方案审查日志

## Round 1 — 2026-08-29

- `plan_revision`: `1`
- `verdict`: `REVISE`
- `review_mode`: 两名独立只读代理；未改文件、未启动游戏、未调用真实 Provider

### 架构审查结果

- `P0`：未定义新生产对象的持久化布局、唯一读写权威和旧 revision 14 的禁止写入边界；新旧路径并存会造成重复生成或丢候选。
- `P0`：候选物化到普通文档的两步写入不可恢复，可能产生孤儿文档或永久 `creating`。
- `P0`：多 source unit 证据没有统一坐标系、跨 unit 证据集合和 snapshot hash 校验规则。
- `P1`：动态分包缺少确定性排序、预算估算版本、拆分原因、Provider 能力协商、队列领取和 backpressure。
- `P1`：candidate fingerprint、跨 packet 去重、候选 lineage 和重排/重试规则未定义。
- `P1`：新协议 request/cache hash 未覆盖用户要求、补全确认、planner、注册表和既有索引；旧 facts cache 可能错误复用。
- `P1`：unknown result、迟到响应、lease 接管 CAS 和旧 owner 写入拒绝不完整。
- `P1`：候选级失败与 packet 级失败边界未定义，缺少 quarantine 规则。
- `P1`：新协议与 revision 14 的双注册表、双 reader、route/schema 分流和迁移 fixture 未落地。
- `P2`：support level 与 certainty/inferred/risk 的映射、零候选终态和 publication state transition 未明确。

### 产品/运行审查结果

- `P0`：现有用户流程仍是扫描→建批次→授权→事实审核→元数据→建档，未切换到计划要求的一键批量生成。
- `P0`：现有 `/start` 等待整个批次完成，且执行器按 item 串行。
- `P0`：候选没有直接打开普通编辑器、保存、导出/编译的可验证调用链。
- `P1`：暂停只修改 manifest，执行器未检查暂停状态。
- `P1`：关闭只停止前端轮询，未通知服务端/Worker；重开后的授权、owner 和 lease 可能失配。
- `P1`：取消、unknown result、全部跳过、部分成功和零候选缺少完整下一步动作。
- `P1`：现有授权请求的 `authorized_item_ids` 与 `send_scope=all_snapshots` 语义冲突。
- `P1`：验收矩阵缺少每个终态的下一步按钮、异步受理、暂停阻止、重开恢复、候选打开编辑器和退出进程断言。

### 已纳入 Revision 2 的修正

- 增加参考资料/作者方向/AI 建议/作者接受/运行时发布的权威边界。
- 增加世界观、内容包、成人扩展和未安装 DLC 的隔离规则。
- 覆盖评估改为可选，不再是所有任务的强制 AI 前置请求。
- 增加 evidence 全局 UTF-16 坐标和 snapshot/unit hash 约束。
- 增加确定性 planner、预算/拆分记录、packet 队列和 backpressure。
- 增加 `authoring-v1` 独立存储命名空间、journal 权威和 r14 只读边界。
- 增加 candidate fingerprint/cache/lineage、unknown result 对账和文档 reconcile。
- 增加服务端 `next_action`、部分成功、零候选和 no-provider 状态。
- 增加包身份、启动器、Web/Worker 退出握手和旧版本隔离要求。
- 增加作者草稿导出与运行时编译发布的状态门。

## Round 2 — 2026-08-29

- `plan_revision`: `2`
- `verdict`: `REVISE`
- `review_mode`: 独立架构代理 + 独立产品/运行代理，只读；未改文件、未启动游戏、未调用真实 Provider

### 架构审查结果

- `P0`：没有明确 documents/binding/compile manifest 的唯一权威，旧 authoring 目录可能被编译器扫描；候选存在不等于可运行的边界不够硬。
- `P0`：发布状态机前后矛盾，`approved_for_compile` 未绑定 document revision/content hash，编辑后也未规定自动撤销批准。
- `P1`：新旧 namespace 只有约定，没有 Store 层拒绝矩阵和双向隔离测试。
- `P1`：planner.json 未明确不可变提交物、PlannerCommitted 事件和 capability snapshot。
- `P1`：evidence 缺少半开区间、normalization/hash 公式和跨 unit group 完整性。
- `P1`：cache 复用可能携带旧 job-local source ID，缺少当前 job 唯一重绑定规则。
- `P1`：lineage、fingerprint 和编辑 revision 没有分层；部分成功没有 valid/invalid index commit set。
- `P1`：unknown result 没有新 fence reconciliation attempt 和崩溃断点恢复矩阵。
- `P1`：next_action 只有动作集合，没有多问题时的确定性优先级。

### 产品/运行审查结果

- `P0`：新 route 尚未定义 open-editor、编辑器 CAS 保存、作者导出和运行时编译的正式契约。
- `P0`：现有 `/start` 仍是同步 `200`，没有持久化 job host、fake Provider latch 或“请求取消不影响任务”的证明。
- `P0`：授权字段语义冲突，且现有契约可能向父目录回溯旧 r14 文件/路径。
- `P0`：验收矩阵没有强制一条真实连续 E2E 从候选到保存、导出、批准、编译和重开。
- `P1`：前端尚未穷举新状态与 next_action，unknown result 没有独立对账入口。
- `P1`：Provider/模型未冻结在 job 快照，创建与启动之间配置变化可能静默换 Provider。
- `P1`：关闭、Web 崩溃、Worker 退出和重开缺少统一恢复证明。
- `P1`：content/evidence fingerprint 混为一谈，证据变化可能阻断同义候选去重。

### 已纳入 Revision 3 的修正

- 明确 `authoring-v1/documents`、binding、document journal 和显式 compile manifest；编译器不得扫描普通 authoring/job/旧 r14 目录。
- 统一唯一发布状态机，批准绑定文档 revision、内容/诊断/注册表/策略 hash，编辑自动撤销批准。
- 增加新旧 route/store 强制拒绝矩阵和双向隔离要求。
- 将 planner 设为 create-only 提交物，增加 PlannerCommitted、planner hash 和 Provider capability snapshot。
- 固定 evidence `[start,end)`、UTF-8/NFC/LF/SHA-256 规则和跨 unit evidence group。
- 区分 lineage、content fingerprint、evidence fingerprint 和 candidate/document revision。
- 增加 unknown reconciliation attempt、迟到响应、六个崩溃断点和部分成功 commit set。
- 增加 next_action 固定优先级和真实连续 E2E 门。

## Round 3 — 2026-08-29

- `plan_revision`: `3`
- `verdict`: `REVISE`
- `review_mode`: 独立架构代理 + 独立产品/运行代理，只读；未改文件、未启动游戏、未调用真实 Provider

### 架构审查结果

- `P0`：仍未封死现有 `/api/compile`、`/api/export`、`Application.CompileCore` 和 `AtomicCandidatePublisher` 的编译/发布旁路；未批准内容仍可能进入运行包。
- `P1`：content/evidence fingerprint、文档 revision 和 release 状态仍有歧义。
- `P1`：发布失败、重试、回滚、superseded 状态与 next_action 不完整。
- `P1`：Provider request ID/query/unknown reconciliation 尚未形成统一 adapter 契约。

### 产品/运行审查结果

- `P0`：unknown result 没有完整正式操作入口，用户可能停在待确认状态。
- `P0`：open-editor→CAS 保存→approve→compile→publish 缺少完整 route/request/response/磁盘证据契约。
- `P1`：状态枚举没有统一覆盖 `quarantined`、`succeeded_zero_output`。
- `P1`：无 Provider 的手动降级没有强制可达的服务端动作。
- `P1`：一键制作的 API 编排边界不清晰，可能重新实现成旧多阶段 UI。

### 已纳入 Revision 4 的修正

- 把 `open-manual-editor`、`reconcile-unknown`、`launch`、document GET/PUT/approve、export、compile、release/publish 全部列为正式 route。
- 固定 route 的成功状态、持久化证据、失败动作和 `202` 启动语义。
- 明确编译器只能接受显式 compile manifest 和 compile proof，旧编译/导出入口必须委托或对新文档返回 410。
- 明确 `RuntimePackageCompiler` 为内部引擎、`AtomicCandidatePublisher` 必须接受同一批准证明或只服务旧 r14 只读迁移。
- 将作者文档状态和 release 状态分离，补充 compile/publish 失败、回滚、superseded 状态。
- 补充 Provider request ID、idempotency key、query capability 和 unknown mode 契约。
- 统一状态列表、next_action 优先级和无 Provider/unknown/部分成功/旧旁路 E2E。

## Round 4 — pending

- `plan_revision`: `4`
- `verdict`: `PENDING`
- `review_mode`: 独立架构审查 + 独立产品/运行审查；只读
- `required_focus`: 是否还存在编译旁路、路由缺口、状态表冲突、无法执行的 Provider 对账承诺或用户仍会卡死的终态。

## Round 4 — 2026-08-29（收束结果）

- `plan_revision`: `4`
- `verdict`: `REVISE`
- `review_mode`: 两名独立只读代理；未改文件、未启动游戏、未调用真实或 fake Provider

### 架构复审结果

- `P1`：`/launch`、`/start` 和兼容层描述造成实际启动入口不唯一；需要明确只有新 `launch` 能入队，旧 `/start` 对新对象 `410`。
- `P1`：Provider 快照冻结时机在“创建 job”和“launch”之间矛盾；需要统一为 launch/方向确认事务原子冻结。
- `P1`：旧 route、`Application.CompileCore` 和 `AtomicCandidatePublisher` 仍可能形成编译/发布旁路；需要统一服务端 `CompileProof`/发布证明门，禁止 path-based compile/export。
- `P1`：release 缺少完整持久化布局、release ID 返回、编译重试新 release、发布重试复用和 rollback 指针 CAS 契约。
- `P1`：Provider request ID、Studio idempotency key、重复点击、查询、`keep_pending` 和新 attempt 的作用域不够确定。
- `P1`：planner 分组算法、tie-break、重叠证据归属和 lineage/指纹关系仍允许实现分歧。

### 产品/运行复审结果

- `P0`（针对当前实现，不是本方案状态）：当前 Web 仍是旧 `/api/ai/batch/*` 三阶段链路，尚未落地新 authoring route；本轮不把它误判为方案缺陷，但要求实现阶段以新连续 E2E 为硬门。
- `P1`：缺少正式 `resume`、`review_gaps`/gaps 和一次性方向确认的完整入口契约。
- `P1`：无 Provider 手动降级、关闭重开和旧进程隔离必须有可达动作与断言。
- `P1`：旧编译/导出入口必须明确拒绝或委托到显式批准证明，而不是只靠 UI 隐藏。
- `P2`：状态词、release 失败/回滚动作和 E2E 仍需在最终契约中统一，避免“成功但无下一步”。

### 纳入 Revision 5 的修正

- 唯一新启动入口为 `POST /api/ai/authoring/jobs/{jobId}/launch`；旧 `/start` 对新对象返回 `410` 且无副作用；`confirm-direction` 只委托同一 `LaunchService`。
- Provider/模型/参数/能力/授权在 launch 事务原子冻结；创建阶段只保存可修改的 request draft。
- 增加 `resume`、`gaps`、`confirm-direction`、release GET、compile retry 和 rollback route 及成功/失败语义。
- 定义 `authoring-v1` 的 jobs/documents/releases 持久化布局、release journal、发布指针和 `CompileProof → PublishProof` 证明链。
- 固定 planner v1 的排序、分组、预算、owner packet、context-only 规则和重规划边界。
- 固定 `candidate_fingerprint`、`evidence_fingerprint`、lineage、cache、operation id 和 Provider idempotency/query/keep-pending 规则。
- 统一候选、Job、Packet、Document、Release 状态与对象级 `next_action`，补齐资料不足、零候选、隔离、关闭恢复和 release 失败 E2E。

### 本轮限制

- 以上结论区分“当前代码尚未实现”和“方案逻辑缺陷”；当前仍未修改代码、未启动 Studio/游戏、未调用 Provider、未同步 AWAKE 或触碰 Marcus。

## Round 5 — pending

- `plan_revision`: `5`
- `verdict`: `PENDING`
- `review_mode`: 两名独立只读代理；只审查 Revision 5 方案逻辑，不以当前实现未改判定方案失败
- `required_focus`: 唯一启动/冻结事务、证明链与 release 持久化、幂等/对账恢复、状态动作闭封、planner/证据指纹和连续验收是否仍有 P0/P1。

## Round 5 — 2026-08-30（Rawls 只读复审结果）

- `plan_revision`: `14`
- `verdict`: `REVISE`
- `review_mode`: 独立只读；未修改代码、未启动 Studio/游戏、未调用 Provider

### 复审发现

- `P0`（实现阶段阻塞，不将其误判为方案已完成）：当前 Studio 仍是旧 `/api/ai/batch/*` 流程，新 authoring route 尚未接线；旧 compile/export 旁路仍需在实现阶段统一拒绝或委托。
- `P1`：Candidate `needs_review` 没有自然可达的 `accept_candidate`；候选编辑后的 Candidate/Document revision、打开时 hash 与当前正文 hash 约束不完整。
- `P1`：72 个响应别名主要继承公共壳，`result.payload` 没有动作级载荷；job gaps、zero output、quarantine 可能只返回计数或自然语言。
- `P1`：`action_reference` 缺少 selection、路由参数和 payload 绑定；关闭重开仍依赖进程内字典和旧 batch ID。
- `P1`：资料不足的 Analysis/报告/方向选项没有在创建或阻塞响应中完整物化；Packet 零候选同时存在 `succeeded + candidate_count=0` 与 `succeeded_zero_output`。
- `P1`：隔离、重复和 unknown 选项缺少统一中文标题、后果、前置条件、终态和下一步；助手详情未严格返回建议正文、证据、风险、diff 与目标 revision。
- `P1`：`bulk_approve_documents` 错误声明创建 `DocumentSet`，实际应返回逐文档更新结果。

### 纳入 Revision 15 的修正

- 三份契约权威指向方案第 34 节；路由状态规则加入 response payload contract 和 action-reference request binding 门。
- Candidate `needs_review` 增加接受动作；接受请求加入 Candidate revision 和可空/完整 `editor_session` CAS，空会话在同一 marker 创建 Document，非空会话保护编辑正文。
- wire 契约增加通用 `action_payload`、路由/payload binding、严格任务/缺口/零结果/隔离/候选/编辑器/批准/导出/编译/助手 payload；响应目录为所有 schema 登记默认或专属 payload definition。
- 修正批量接受确认字段、批量批准虚构 `DocumentSet`、资料导入对账误用确认字段、重复 resolution 条件目标和 Release 双重比较条件。
- Packet `succeeded + candidate_count=0` 删除；Job 零候选优先进入 `review_gaps`；状态条件加入字段白名单并把 Packet dispatch 与 Release 非缺失分支改为可解析表达式。

### 本轮验证限制

- 仅完成静态契约与方案修改；尚未启动真实 Studio、Provider 或游戏，未验证实现代码、关闭重开、实际编译发布和用户点击链路。

## Round 6 — pending

- `plan_revision`: `15`
- `verdict`: `PENDING`
- `review_mode`: 两名独立只读代理；只审 Revision 15 方案与三份契约，不以当前实现尚未接线判定方案失败
- `required_focus`: 重点检查 action payload 递归引用、所有 response alias 的默认/专属载荷、状态条件字段白名单、候选编辑接受 CAS、批量结果、恢复持久化和连续离线验收是否仍有明显 P0/P1。

## Revision 16 修订记录 — 2026-08-30

- `plan_revision`: `16`
- `status`: `PENDING_NEW_READ_ONLY_REVIEW`
- `review_mode`: 尚未启动本轮代理；本轮仍未修改 Studio 实现、未调用 Provider、未启动 Studio/游戏、未同步 AWAKE 或触碰 Marcus

### 已纳入修正

- 72 个响应目录项全部绑定到可解析的专属载荷或 `operation_result_payload_v1`；不再允许未登记响应隐式使用 generic 载荷。
- 72 个真实 `response_*_v1` Schema 全部在 `result.payload` 引用与目录相同的载荷定义，消除“目录有字段、实际 Schema 仍为空壳”的断点。
- Wire 契约将响应载荷默认回退改为显式禁止；generic 仅保留为内部组合基础。
- 路由状态条件把非法的 `failure_class_missing_reference=false` 改为 `failure_class=other`。
- `start_publish_recovery_case` 明确为幂等 create-or-reopen，允许新建 `201` 和复用未关闭案件的 `200`，不写运行时文件、不切换指针。
- 方案新增第 35 节，固定上述规则、机械验收项和 Revision 16 停审门。

### 下一步

先执行三份契约的 JSON、`$ref`、响应目录/真实 Schema exact-set、载荷一致性、状态条件和路由状态机械检查；检查通过后启动两名新的独立只读代理。任一代理发现 P0/P1 或机械检查失败，继续追加修订，不宣称方案通过。

### Revision 16 机械检查结果 — 2026-08-30

- 三份契约 JSON 可解析；Wire 契约本地 `$ref` 461 个，未发现未解析引用。
- 响应目录 72 项、真实 `response_*_v1` 定义 72 项，集合一致。
- 72 项目录载荷与真实响应 `result.payload.$ref` 全部一致；最终载荷 generic 使用数为 0。
- Action catalog 响应覆盖 72 项，路由动作 77 项，动作 ID exact-set 和响应别名引用通过。
- 状态条件字段/枚举检查通过；未发现旧 `failure_class_missing_reference`，`start_publish_recovery_case` 状态为 `[200, 201]`。
- 过程中发现并修复批量生成响应定义时误写的字面量 `` `$ref`` 键；修复后重新执行全量检查通过。
- 本结果仍是离线契约证据，不证明 Studio 实现、真实 Provider、关闭重开、编译发布或游戏内运行链路已完成。

## Revision 17 修订记录 — 2026-08-30

- `REVIEW_TARGET`: `PLAN-WorldbookStudio-AI-AUTHORING-REWORK-20260829.md` Revision 17 与三份 `authoring-*` 机器契约。
- `REVISION`: `17`。
- `behavior_slice`: operation/retry 身份、Analysis freshness、Job 控制 marker、lease 过期恢复、typed `next_action`、选择快照、作者导出与响应载荷绑定。
- `review_mode`: 按 `bounded-review-convergence` 执行一次基线检查、一次修正后定向检查；项目门仍要求一名主审查代理和一名挑战代理分别返回 `VERDICT: APPROVED`。
- `internal DECISION`: `REVISE_CURRENT_SLICE`（基线发现旧 execution 语义、operation 状态别名、Analysis 列表 freshness 缺失和控制 marker 未显式分支）。
- `evidence_boundary`: 仅检查方案文本、三份契约 JSON、现有状态/路由/载荷注册；不启动 Studio、Provider、Bannerlord，不修改 AWAKE 运行时、游戏目录、dist 或冻结候选。
- `corrections_applied`: retry 使用新 operation 与 `retry_of_operation_id`；operation policy 统一为 `terminal_rejected`/`retryable_failure`；Analysis list 物化 `freshness`；Job control marker 补齐 `paused_requested`、`resume_requested`、`cancel_requested` 的 pending/committed/unknown 分支；dispatch enum 统一为 `not_started|submitted|started_unknown|completed|failed` 并移除 `started` 别名；恢复矩阵拆除未注册复合动作；补齐 Release publish failure 的 pointer `unknown|conflict|unknown_archived|verified` 分支；创建型动作加入 `reference_target`；修正 `target_display` 绑定；响应 success/error 加入 `correlation_id`；明确顶层 `response.next_action` 的唯一对象级权威；raw-output→manual-draft 强制 `needs_review`、`compile_eligible=false` 与 `document_provenance` 来源链；统一未提交意图为 `discarded`/`discard_uncommitted`；新增方案第 36 节作为本轮权威解释层。
- `mechanical_check`: `13/13 PASS`；三份契约 JSON 可解析；79 个 action exact-set；状态动作均指向已注册动作或保留 `none`；恢复矩阵无复合动作；Registry/Wire dispatch enum 一致；6 个创建型动作均有 `reference_target`；`target_display` 路径正确；success/error 与顶层 required fields 均含 `correlation_id`；operation 状态集合一致；raw-output 人工草稿门禁存在；74 个 response catalog 与真实 schema/payload `$ref` 一致且 generic 最终使用为 0；旧恢复术语清理通过；方案第 36 节权威字段覆盖通过。
- `unverified_claims`: 真实关闭重开、Worker/Provider unknown、实现端 marker/CAS、真实编译发布和用户点击链路仍未验证。
- `next_action`: 机械检查已通过；现在由 Dewey（事务/恢复主审）和 Euclid（普通编辑者/响应契约挑战）只审本轮修正 diff 与受影响不变量，不重跑完整历史审查。

### Revision 17 定向复审边界 — 2026-08-30

- `review_target`: 仅本轮新增或修改的 `resume_requested` 恢复分支、canonical dispatch enum、恢复矩阵动作、Release pointer failure 分支、创建型 `reference_target`、顶层 `next_action`/`correlation_id` 响应约束、raw-output 人工草稿门禁与 provenance。
- `evidence_boundary`: 仅静态方案、三份契约 JSON 和上述 13 项机械检查；不启动 Studio、Provider、Worker 或 Bannerlord，不修改实现、不同步 AWAKE、不触碰 Marcus。
- `stop_rule`: 两名指定代理各返回精确 `VERDICT: APPROVED` 即停止本 Revision 17 审查；若发现新的阻塞性产品边界、持久化、并发、外部副作用或不可逆语义问题，只允许记录该新 blocker 并按 bounded-review 规则处理，不扩展为全文循环。
