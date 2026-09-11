# Worldbook Studio Authority Closure 修复计划

- 批次：WORLDBOOK-STUDIO-AUTHORITY-CLOSURE-20260901
- 日期：2026-09-01
- 风险：high-risk
- 审查状态：docs/review-state/WORLDBOOK-STUDIO-AUTHORITY-CLOSURE-20260901.json
- 目标：修复“隐式批准即可编译”和“未绑定 staging 可发布”两条 Authority Closure P1 缺口。

## 已确认事实

1. 客户编译/导出入口会先注册当前文档、自动生成 selection、自动生成 approval，再签发 CompileProof。
2. 合同状态机要求 approval_ready=true 后仍需 approve_document，编译只消费明确批准的选择集合。
3. PublishStaging 当前校验 staging manifest 和 current pointer CAS，但未强制 staging 对应本次已提交的 export_staging operation 与 CompileProof。
4. 旧路线已 fail-closed；本批不得恢复旧路线、修改冻结 AWAKE 候选、启动 Bannerlord、同步游戏目录或访问真实 Provider。

## 第 1 轮审查结论

- VERDICT: REVISE。
- P1-01：客户 compile/export 仍在入口内部自动批准，显式 approval 尚未成为真实前置条件。
- P1-02：PublishStaging 可接受任意工作区 staging，未强制绑定已提交 export operation。
- P1-03：Application.Compile/Export/WriteCompiled 仍存在 Core 层 AuthorityGate 绕过面。
- P1-04：发布恢复只验证局部目录或 manifest，不能重建完整 authority 链。
- P1-05：进程内 _gate 不能保护 Web 与 CLI 跨进程的 pointer CAS。
- P1-06：绑定 action registry 与实际 Web 路由发生漂移，错误状态映射也不完整。

## 最小闭环

打开/编辑文档 → 保存并递增 revision → 显式批准文档/选择集合 → 签发 CompileProof → 精确编译 → 提交已绑定 export_staging → 校验 staging 身份 → 显式 publish pointer → 可观察 release/journal 结果

## 修复决策

- 客户 UI 的 compile/export 不再隐式创建 ApprovalProof；必须先经过显式 approval action，且 approval 绑定文档 ID、revision、content hash、selection hash。
- CompileProof 必须引用且重新验证 ApprovalProof；ApprovalProof 必须来自显式批准操作，不接受客户 compile helper 自动批准。
- export_staging operation 记录 compile proof id、proof hash、manifest hash、staging 完成标记和 artifact identity。
- publish-staging 只接受已提交且可验证的 export operation；传入 staging path 不能单独构成发布授权。
- publish proof 绑定 export operation、CompileProof、staging manifest 和 current pointer CAS；任何绑定缺失或 hash 不一致都 fail-closed。
- 唯一接口通路固定为：`register → selection → explicit approve → CompileProof → export(export_operation_id) → publish(export_operation_id)`。客户 façade 只提交 `approval_id`/`selection_id` 和 proof 引用，不得在 compile/export 内部创建 approval。
- `ApprovalProof` 只允许三态：`approved`、`superseded`、`invalid`；文档 hash/revision、selection hash、registry/reference closure 任一变化即 `invalid`，compile/export 返回 409。
- publish 请求唯一授权字段为 `export_operation_id`；服务端从已提交 operation 重建并核对 `CompileProofId`、CompileProof hash、staging path、manifest hash、`complete.marker`、`SHA256SUMS.txt` 和 artifact identity。
- Core 选择唯一方案：无 proof 的 `WorldbookApplicationService.Compile`、`Export`、`WriteCompiled` 以及无 proof 的发布器入口收为 `internal`；CLI compile/export 只能通过 AuthorityGate。
- 路由权威表采用“真实 Web/CLI 可调用清单”：保留 `/api/authoring/*` 客户 façade 与 `/api/ai/authoring/*` 控制面，逐项登记真实 route、method、CLI command、request proof fields 和 response/error contract；未实现的旧 release action 不标记为 bound。
- 最小错误码矩阵固定为：未批准/批准失效=`409`，文档或 proof 不存在=`404`，请求字段/分层无效=`400/422`，staging 或绑定 hash 不一致=`409`，pointer/lease 竞争=`409`，旧路线=`410` 且 `side_effect=none`。
- Core 的编译、导出和写入入口改为内部实现，或必须携带 server-issued proof；不存在不带 proof 的可发布公共入口。
- Core 的 `CompileExact` 一并纳入收窄范围；除 AuthorityGate 持有的 server-issued proof 外，`Compile`、`CompileExact`、`Export`、`WriteCompiled` 和发布器入口均不得作为可调用公共绕过面，且无 proof 直调用必须在任何输出、staging 或 journal 副作用前 fail-closed。
- 保留现有 /api/authoring/* 客户 façade 与 /api/ai/authoring/* authority 控制面的分层，但将 action registry 明确登记为真实可调用路由；废弃未实现或漂移的声明，不制造第二条权威路径。
- 发布使用跨进程 lease/独占锁，并在锁内重新读取 current pointer 后执行 expected-current CAS；冲突统一返回 409。

### 接口生命周期与权威合同

- `export_operation_id` 由服务端在成功提交 `export_staging` 时创建并返回；客户端不得预先伪造或自行决定其权威内容。重复提交使用请求幂等键恢复同一已提交 operation，不能重复批准、编译或写入 staging。
- Export 请求必须携带已批准选择集合与 CompileProof 引用；成功响应至少返回 `export_operation_id`、staging identity、manifest hash、artifact identity 和可观察 operation 状态。Publish 请求只接受 `export_operation_id` 与 expected-current pointer 条件，拒绝并忽略 `staging_path`/`manifest_hash` 作为授权字段。
- canonical route/error exact-set 由 `contracts/authoring-action-route-registry.v1.json` 与对应 Web/CLI contract fixture 共同锁定；每个已登记 action 必须逐项匹配真实 method、route、CLI command、request proof fields、成功响应和错误码，未实现 action 不得登记为 bound，漂移测试必须 fail。
- Authority 状态与失效原因必须可断言：ApprovalProof 持久记录 `approved`；文档 revision/content hash、selection hash 或 registry/reference closure hash 任一变化时原 proof 转为 `invalid`，同一文档的新显式批准使旧 proof 转为 `superseded`。失效原因和触发时点写入 journal，compile/export 统一返回 409。
- 恢复按链条判定：`export_operation → CompileProof → staging → manifest → complete.marker → SHA256SUMS.txt → publish proof → current pointer`。任一记录缺失、篡改、身份或绑定 hash 不一致时只产生 `failed_recovery` 结果，不接管 pointer、不创建 committed publish operation。

## 验收标准

- 未显式批准的文档：compile/export 返回明确阻断，磁盘、Provider、journal 无副作用。
- 显式批准后文档未变化：CompileProof 可签发，compile 成功。
- 批准后文档 revision/hash 变化：CompileProof、compile、export 均拒绝，不生成新 staging。
- staging 成功：只创建完整 staging，不修改 current pointer。
- 伪造 staging path、伪造 manifest hash、缺失 export operation、替换 CompileProof：publish 全部拒绝且旧 pointer 不变。
- 重复 operation：只返回原结果，不重复生成批准、编译或发布副作用。
- 旧路线：继续返回 410、side_effect=none。
- 客户 UI：只能调用显式批准、authority-backed compile/export/publish 路由；按钮状态必须反映批准和版本前置条件。
- Core 直接调用编译/导出：无 proof 必须 fail-closed，且不创建 staging、不写 current pointer。
- 恢复：缺失 complete.marker、SHA256SUMS、export operation、CompileProof 或任一绑定 hash 时只能标记 failed_recovery，不得自动接管。
- 路由合同：批准、编译、导出、发布的 registry route、Web route、CLI command 和错误码一一对应；不存在的 action 不得标记为 bound。
- 跨进程竞争测试：两个独立 AuthorityGate 实例使用同一 `expected_current_manifest_hash` 并发发布，必须恰有一个成功、另一个返回 409；失败方不得写 committed publish operation、publish proof 或 pointer。
- Core 绕过测试：直接调用无 proof 的 Compile/Export/WriteCompiled 必须在任何输出和 journal 副作用前拒绝。
- Core 绕过测试：直接调用无 proof 的 Compile/CompileExact/Export/WriteCompiled 必须在任何输出、staging 和 journal 副作用前拒绝。
- 恢复判定逐项断言：缺失或篡改 `complete.marker`、`SHA256SUMS.txt`、export operation、CompileProof、publish marker 或任一绑定 hash 时，只能 `failed_recovery`，不得自动发布或接管。

## 实施范围

- 核心：AuthorityGate.cs、必要的 authority contracts/model。
- Web：Program.cs、客户 UI 的审批/编译/导出状态接线。
- CLI：仅同步同一权威路径和错误边界。
- 测试：AuthorityGate HTTP/compile/recovery fixture、customer closure 静态回归、相关 smoke；新增跨进程并发发布和 Core 绕过回归。
- 文档：客户流程和当前未验证边界。

## 明确不做

- 不修改 R14 历史合同，不迁移真实 v2 世界知识内容。
- 不实现 Bannerlord 游戏内验证、游戏目录同步、真实云 Provider 或 Worker 部署。
- 不扩展发布到游戏目录；publish 仍只处理 Studio 工作区 current pointer。
- 不顺手重构索引、根路径校验或旧批量工作台。

## 验证顺序

1. 独立只读审查本计划、相关 authority/Web/CLI/合同/测试。
2. 用户签收审查后的计划后，再实施核心 authority 修复。
3. 先跑新增的未批准/绑定缺失回归，再跑 AuthorityGate 专项。
4. 跑完整 scripts\test.ps1、重新打包并执行 release-check.ps1。
5. 更新 checkpoint/CURRENT，明确离线证据与未验证边界。

## 当前门槛

- plan_status: ready_for_review
- review_status: active
- user_signoff_required: true
- primary_executor: Worldbook Studio authority owner
- minimum_evidence: AuthorityGate 旧路由零副作用、批准门、CompileProof closure、export-staging binding、publish CAS/recovery、完整 Studio harness、release-check
- review_findings_disposition: 第 1 轮全部 P1 纳入本批；P2 测试扩展作为对应验收证据，不另开审查批次。
- review_round_2_disposition: 第 2 轮要求已落实；本批进入第 3 轮终审前不得改实现代码。
