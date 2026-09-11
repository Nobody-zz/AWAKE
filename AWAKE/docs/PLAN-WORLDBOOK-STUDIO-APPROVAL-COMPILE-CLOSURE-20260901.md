# Worldbook Studio Approval → Compile Closure 修复计划

- 批次：WORLDBOOK-STUDIO-APPROVAL-COMPILE-CLOSURE-20260901
- 日期：2026-09-01
- 风险：high-risk
- 前置背景：WORLDBOOK-STUDIO-AUTHORITY-CLOSURE-20260901 第 3 轮终审 REVISE，已拆分为可独立验收的首批
- 目标：让“显式批准 → CompileProof → 编译”成为唯一可用闭环，彻底消除客户入口隐式批准和 Core 无 proof 的编译/导出/写入绕过

## 为什么先做这一批

发布、恢复、跨进程锁和完整路由合同是后续批次，依赖本批先建立可信 CompileProof。若批准到编译仍可被客户 façade 或 Core 绕过，后续 publish 修复仍会建立在不可信输入上。

## 已确认事实

1. 当前客户 `/api/authoring/compile` 与 `/api/authoring/export-staging` 仍通过 `IssueCustomerCompileProof` 自动完成 register/selection/approve/proof。
2. 当前 `AuthorityGateService` 已有 `ApproveSelection`、`IssueCompileProof` 和 `Compile`，但客户入口没有要求调用者先提交显式 approval/proof。
3. `WorldbookApplicationService` 仍公开 `Compile`、`CompileExact`、`Export`、`WriteCompiled`；其中 `Export` 可直接进入候选发布器，四者都构成 AuthorityGate 外部绕过面。
4. 本批不处理 export operation 绑定 publish、恢复接管、跨进程 pointer CAS 或全量 registry 清理。

## 唯一通路

```text
register(document)
→ create/resolve selection
→ explicit approve(selection)
→ issue CompileProof(approval)
→ compile(CompileProof)
→ observable CompileResult
```

客户编译请求只能提交服务端签发的 `compile_proof_id` 和可选确认令牌；不得再提交一组路径/hash/revision 让服务端内部自动批准。审批、proof 和编译必须绑定同一 document revision、content hash、selection hash 及当前 registry/reference closure。

## 实施决策

- 删除或停用客户 compile/export façade 内部的 `IssueCustomerCompileProof` 自动批准路径；客户 compile 与 export-staging 均只保留 proof-backed 输入。export-staging 本批只负责由有效 CompileProof 生成隔离 staging，不处理 staging→publish 的 operation-only 授权。
- `/api/ai/authoring/selections/{selectionId}/approve` 与 `/api/ai/authoring/compile-proof` 继续作为显式 authority 控制面；compile-proof 请求必须引用已批准的 `approval_id`/`selection_id`，服务端重新验证文档版本和选择集合。
- `/api/authoring/compile` 与 `/api/authoring/export-staging` 只接受 `compile_proof_id`（及既有确认令牌/输出参数）；缺失或无效 proof 返回确定性错误，不产生编译输出、隔离 staging、Provider 调用或 journal 副作用。旧的 `path`、`source_hash`、`revision` 字段必须被拒绝为 `422`，不得触发任何 register/selection/approve。
- `ApprovalProof` 在本批至少记录并验证 `approved` 状态、document revision/content hash、selection hash 和 proof hash；文档或选择集合变化时 compile-proof/compile fail-closed。`superseded` 的跨批准历史和完整失效原因表留到后续 authority 状态批次，但不得允许旧 proof 继续编译。
- `WorldbookApplicationService.CompileExact`、`Compile`、`Export`、`WriteCompiled` 的可见性收窄为 AuthorityGate 内部实现；AuthorityGate 提供唯一的 `CompileApproved(compileProofId, confirmationToken, outputRoot)` 组合入口，内部按“验证 proof → `CompileExact` → `WriteCompiled`”结算并返回 CompileResult 与 compiled path。Web 不得直接调用 `WorldbookApplicationService.WriteCompiled`。无 proof 的公共编译/导出/写入入口必须不存在或在副作用前 fail-closed；本批不改变 AuthorityGate 对 publish 的后续绑定设计。
- CompileProof 必须持久保存并校验：`approval_id`、`selection_id`、`selection_hash`、每个文档的 `revision`/`content_hash`、`registry_snapshot_hash`、`reference_closure_hash`、`content_tier` 和 canonical request digest。`registry_snapshot_hash` 由参与编译的 profile/referral registry 版本与内容 canonical hash 计算；`reference_closure_hash` 由当前文档引用源集合及其 canonical source registry hash 计算。签发和消费时使用同一字段顺序与大小写不敏感 hash 比较。
- `IssueCompileProof` 的 `operation_id` 是服务端幂等键：operation 记录保存 canonical request digest（approval_id、selection_id、content_tier 的 canonical JSON hash）。相同 operation_id 且 digest 相同只返回原 proof；digest 不同固定返回 `409`，不得重放原 proof。客户 compile/export 使用返回的 `compile_proof_id`，不把 operation_id 当作可替代 proof。
- 本批固定错误映射：请求体缺失或 `compile_proof_id` 缺失=`400`；文档或 CompileProof 不存在=`404`；ApprovalProof 未批准、CompileProof 过期或任一绑定 hash（含 registry/reference closure）变化=`409`；字段类型、content tier 或分层不合法=`422`。同一错误不得在 Web 与 CLI 间漂移。

## 验收标准

- 未显式批准的客户 compile/export-staging：返回 `404`（缺失 proof）或 `409`（proof 失效），不写 compiled/staging/current pointer，不写 journal，不调用 Provider。
- 缺少 `compile_proof_id` 的客户 compile/export-staging：固定返回 `400`，且零副作用；携带旧 `path`/`source_hash`/`revision` 字段的请求固定返回 `422`，且零副作用。
- 显式批准且文档未变化：compile-proof 成功，随后客户 compile 返回有效 CompileResult。
- 批准后 document revision/content hash/selection hash/registry-reference closure hash 变化：compile-proof 或 compile/export-staging 返回 `409`，不生成新编译输出或 staging。
- 伪造、替换或篡改 CompileProof：返回 `404`（不存在）或 `409`（存在但校验失败），不产生输出。
- 同一 CompileProof operation ID 搭配不同 approval、selection 或 content tier 重用：固定返回 `409`，不得重放不相干 proof。
- registry snapshot 或 reference closure 变化：CompileProof 消费固定返回 `409`，不生成新编译输出或 staging。
- 直接调用无 proof 的 `Compile`、`CompileExact`、`Export`、`WriteCompiled`：无法从公共 API 绕过，或在任何输出/journal 副作用前明确拒绝；Core 绕过测试必须覆盖四个入口及 `CompileApproved` 的无效 proof。
- proof-backed 客户 compile 的完整闭环必须断言：approval route 返回 approval proof → compile-proof route 返回 compile proof → customer compile route 只提交 compile proof 并经 `AuthorityGate.CompileApproved` → 返回 CompileResult/compiled path；Web 不得直接写入 Core。
- 客户前端只在拥有当前有效 proof 时启用 compile/export-staging；不再以路径/hash/revision 参数触发隐式批准。
- 既有旧路线继续保持 `410` 且 `side_effect=none`；本批不得恢复旧路线。
- export-staging 只验证本批输入并生成隔离 staging；验收必须断言 current pointer、既有 `PublishStaging` 参数与语义不变。本批不宣称 publish/recovery/CAS/游戏内验证或真实 Provider 已完成。

## 文件范围

- 核心：`src/Awake.WorldbookStudio.Core/AuthorityGate.cs`、`src/Awake.WorldbookStudio.Core/Application.cs` 及必要 authority contract/model
- Web：`src/Awake.WorldbookStudio.Web/Program.cs`
- 前端：`src/Awake.WorldbookStudio.Web/wwwroot/studio-editor-safety.js` 与直接相关状态/测试
- 测试：`tests/Awake.WorldbookStudio.AuthorityGate.Tests`、`tests/frontend/customer-closure.test.js` 及必要 fixture
- 合同：补充本批批准/CompileProof/compile/export-staging 的 exact-set request/error fixture；不重写未涉及的 publish registry
- 文档：本计划、对应 review-state、完成后的 checkpoint；不覆盖旧批次审查状态

## 明确不做

- 不实现 `export_staging → publish` 的 operation-only 绑定
- 不实现恢复完整性接管、跨进程 lease/CAS
- 不重写全部 action registry，不迁移 R14 合同
- 不启动 Bannerlord，不同步游戏目录，不调用真实云 Provider，不读取真实 API Key
- 不修改冻结 AWAKE 候选 002，不提版本，不重新生成客户 ZIP

## 验证顺序

1. 终审确认唯一输入合同、Core 可见性边界和零副作用验收。
2. 用户明确签收本批后，先补失败回归，再实现 authority/Web/前端闭环。
3. 运行 AuthorityGate 专项、customer closure、HTTP smoke 和完整 Studio harness。
4. 做静态 route/legacy 检查与构建；只报告离线证据，不冒充游戏内验证。
5. 更新 AWAKE checkpoint，登记后续 publish/recovery/CAS 批次。

## 当前门槛

- `plan_status: ready_for_review`
- `review_status: active`
- `user_signoff_required: true`
- `primary_executor: Worldbook Studio authority owner`
- `minimum_evidence: explicit approval gate, CompileProof closure, Core bypass rejection, customer zero-side-effect regression, AuthorityGate tests, full Studio harness`
