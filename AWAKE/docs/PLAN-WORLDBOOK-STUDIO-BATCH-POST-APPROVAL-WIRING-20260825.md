# Plan: Worldbook Studio 批量作者发布接线与合同升级

- date: 2026-08-25
- status: release_ready
- parent: `docs/PLAN-WORLDBOOK-STUDIO-BATCH-IMPLEMENTATION-REPAIR-20260825.md`
- current_contract: Revision 13, SHA-256 `217E7538DC7C356D092ABC029A79E7504EE36BC1000B0B3FD3F7FF261FDCB4A9`
- proposed_contract: Revision 14, new hash to be computed only after approval
- proposed_contract_source: `docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json`
- proposed_contract_package_path: `schemas/batch/awake.worldbook.batch-authoring-contracts-r14.json`
- proposed_design_addendum: `docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-r14-design-addendum.md`
- user_goal: 完成批量作者阶段封闭实现并通过当前源码打包与发布检查
- code_gate: 未取得当前-primary 独立只读审查 `VERDICT: APPROVED` 和用户签收前，不修改合同、加载器、发布脚本或代码
- review_log: `docs/PLAN-WORLDBOOK-STUDIO-BATCH-POST-APPROVAL-WIRING-20260825-REVIEW-LOG.md`

## Confirmed facts

- 批量作者闭环已经可达：导入 → 扫描 → 事实提取 → 人工审核 → 元数据 → `needs_review` 建档。
- 当前本机证据已经通过：Studio `101/101`、BatchTests `13/13`、Draft `9/9`、Draft/Batch HTTP Smoke、Release build `0 warnings / 0 errors`。
- Revision 13 的 `test_entrypoints.wired_now=false` 是有意的发布阻断；当前 `release-check.ps1` 必须返回 `WB-RELEASE-033`。
- Revision 13 原始文件、hash、评审结论和内部候选包必须保留，不得原地翻转字段或覆盖历史证据。
- 当前候选包不是正式发布版，也不代表云端 Provider、Worker、Bannerlord 或存档验证通过。

## Decision frame

### Option A — 原地把 Revision 13 的 `wired_now` 改为 true

- 优点：改动最少。
- 放弃：破坏已批准合同的 hash、评审边界和历史候选可复现性。
- 结论：拒绝。

### Option B — 通过 release override 绕过 `wired_now=false`

- 优点：无需升级合同。
- 放弃：直接违反“未接线必须阻断发布”的合同，形成不可审计的假绿。
- 结论：拒绝。

### Option C — 保留 Revision 13，新增当前发布合同 Revision 14

- 优点：R13 可复现、R14 明确记录接线完成，发布门禁有新的可验证权威。
- 成本：需要新 hash、加载器常量、合同检查、打包脚本、发布脚本和相关 golden/测试同步，并重新审查。
- 选择：推荐。

## Recommended design

1. 原样保留 Revision 13 文件和 `217E7538...FDCB4A9` hash。
2. 从 R13 生成 R14 当前合同副本，并用 R14 设计附录声明新的当前权威；只改变发布接线所需的合同版本/哈希字段：
   - `revision=14`；
   - `test_entrypoints.wired_now=true`；
   - 保留 R13 的状态机、API、缓存、权限、公开投影和 no-canon 约束；
   - 不新增状态、不改变批量业务语义。
3. Studio Dev/Package 运行时、`batch-contract-check.ps1`、`package.ps1`、`release-check.ps1` 和测试 golden 统一指向 R14 canonical source/package path；R13 只作为历史/兼容审计材料，不作为正式包的当前合同。
4. 正式包仍只创建 `needs_review` 草稿，不生成 expressions，不绑定身份、人物或家族，不发布正典。
5. 正式包生成前继续执行 `test.ps1`、完整 HTTP Smoke、合同检查、manifest/SHA256SUMS 和 ZIP sidecar；失败时不得生成或保留名为正式版的 ZIP。

## Behavior loop

`R14 contract load → test.ps1 → BatchTests/Draft/HTTP Smoke → package.ps1 → release-check.ps1 → manifest/SHA256SUMS/ZIP → internal candidate artifact`

可观察结果：`release-check.ps1` 返回 0，当前合同 hash 与包内合同、manifest、SHA256SUMS 一致；档案仍为 `needs_review`，无 expressions/identity/person/family binding。

## Acceptance cases

- R13 原始文件 hash 仍为 `217E7538DC7C356D092ABC029A79E7504EE36BC1000B0B3FD3F7FF261FDCB4A9`。
- R14 合同 authority、R14 设计附录和实现计划三者指向同一当前合同；R13 设计不会继续声称自己是 R14 的唯一权威。
- R14 在 PowerShell 7 和 Windows PowerShell 5.1 下通过机械合同检查，所有 `$ref`、route、required/conditional 字段可解析。
- R14 `wired_now=true`，且发布检查不再返回 `WB-RELEASE-033`；R13 单独运行仍保持阻断语义。
- `test.ps1` 返回 0，并保留 Studio `101/101`、Batch `13/13`、Draft `9/9`、Draft/Batch HTTP Smoke 证据。
- `package.ps1` 只生成当前源码产物；manifest、SHA256SUMS、ZIP sidecar 互相一致，正式发布检查返回 0。
- 15 条 batch route 的 request/response/error registry binding 保持通过；public projection 不泄露 lease、owner、provider fingerprint、内部路径或原始错误。
- 所有自动生成档案仍是 `needs_review`，`expressions` 为空，人物/家族/身份绑定为空。
- 不启动 Bannerlord、不修改游戏目录、不修改冻结 AWAKE 候选、不进行真实云端 Provider 声称。

## Non-goals

- 不实现新的批量业务阶段、UI、游戏内读取器或周报系统。
- 不把 R13 改造成 R14，不删除历史合同、候选包或评审日志。
- 不把离线 Smoke 当成游戏内验证或云端语义验证。
- 不借升级合同顺便实现 metadata cache 深化或 Provider unknown-result 分类；它们另立后续硬化批次。

## Write set after approval

- `docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json`
- `docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-r14-design-addendum.md`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/BatchAuthoringContractRegistry.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/BatchEndpoints.cs`
- `tools/worldbook-studio/scripts/batch-contract-check.ps1`
- `tools/worldbook-studio/scripts/package.ps1`
- `tools/worldbook-studio/scripts/release-check.ps1`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.BatchTests/Program.cs`
- affected golden/review/checkpoint files only

## Evidence minimum

- Independent read-only review of this plan and current source with exact terminal verdict.
- R13 preservation hash check and R14 mechanical contract check under both PowerShell engines.
- Full current-source build, all existing tests and both HTTP Smoke paths.
- Current-source package, manifest/SHA256SUMS/ZIP verification and release-check exit code `0`.
- Final bounded code-debt audit; no game startup, game-directory sync or canonical publication.

## Open decision

- This plan needs explicit user signoff before the R14 contract and code write set may be changed, because it introduces a new public contract revision and changes release eligibility.

## Completion snapshot

- Independent read-only review returned exact `VERDICT: APPROVED`.
- R13 source hash remains `217E7538DC7C356D092ABC029A79E7504EE36BC1000B0B3FD3F7FF261FDCB4A9`.
- R14 source/package hash is `7D07B08DA4CE5AB27DFE70F2F81BB098597A04FD170160612E09C32DA4540808`.
- `batch-contract-check.ps1`: `1132/1132 PASS`.
- Full test chain, Draft/Batch HTTP Smoke, Launcher tests `14`, package and standalone `release-check`: PASS.
- Current package: `tools/worldbook-studio/artifacts/WorldbookStudio-win-x64.zip`; ZIP SHA-256 `a2b775e76e63298999b44e73fa1cb56552f99584fb8dd25db83b6caefd9e2ee9`.

## Completion snapshot

- Independent read-only review returned exact `VERDICT: APPROVED`.
- R13 source hash remains `217E7538DC7C356D092ABC029A79E7504EE36BC1000B0B3FD3F7FF261FDCB4A9`.
- R14 source/package hash is `7D07B08DA4CE5AB27DFE70F2F81BB098597A04FD170160612E09C32DA4540808`.
- `batch-contract-check.ps1`: `1132/1132 PASS`.
- Full test chain, Draft/Batch HTTP Smoke, Launcher tests `14`, package and standalone `release-check`: PASS.
- Current package: `tools/worldbook-studio/artifacts/WorldbookStudio-win-x64.zip`; ZIP SHA-256 `a2b775e76e63298999b44e73fa1cb56552f99584fb8dd25db83b6caefd9e2ee9`.
