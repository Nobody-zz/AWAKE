# PersonaWorkbench × AWAKE Joint G2 Gate Checkpoint

- `task_id`: `PERSONA-AWAKE-JOINT-G2-GATE-20260824`
- `batch_id`: `persona-awake-joint-g2-e2-g3-gate-20260824`
- `status`: `offline_verified_g2`
- `execution_lease`: `none`
- `scope`: 联合契约、G3-S0/G3-A/G3-B 门禁准备、隔离计划验证器、E2 fixture 矩阵聚合；不含 AWAKE runtime/Storage 源码实现。

## files_changed

- `_houkai_merge/AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-20260823.md`
- `_houkai_merge/AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-20260823-REVIEW-LOG.md`
- `_houkai_merge/AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-CONTRACT-LOCK-20260823.md`
- `_houkai_merge/AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-NEXT-GATE-20260824.md`
- `_houkai_merge/AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-ARCHITECTURE-MATRIX-20260824.md`
- `_houkai_merge/AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-20260824.md`
- `_houkai_merge/AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-REVIEW-LOG-20260824.md`
- `_houkai_merge/AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-REQUIREMENT-EVIDENCE-MATRIX-20260824.md`
- `_houkai_merge/AWAKE/docs/fixtures/persona-awake-joint/PWB-AWAKE-012-persistence/expected-error.txt`
- `_houkai_merge/AWAKE/docs/fixtures/persona-awake-joint/PWB-AWAKE-012-persistence/manifest.sha256.txt`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/verify-g3-plan.ps1`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/verify-e2-matrix.ps1`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/verify-g3-s0-scope.ps1`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/verify-native-prerequisite.ps1`
- `_houkai_merge/AWAKE/docs/persona-awake-joint-g3-s0-scope.v1.json`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/verify-contract.ps1`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/continuation-*.json`

## verification

- `verify-contract.ps1`: `pass/0`。
- `verify-g3-plan.ps1`: `pass/0`；任务图为 `G3-S0 -> G3-A -> G3-B -> G3-C -> G4`。
- `verify-e2-matrix.ps1`: `pass/0`；20/20 fixture outcome 按策略匹配，`PWB-AWAKE-013` 单独记录 `protected_baseline_drift` warning。
- `PWB-AWAKE-001-valid-approved`: `pass/0`。
- `PWB-AWAKE-012-persistence`: 预期 `blocked/20`，断言全通过，未授予 lease。
- Runtime static: `reject/10`；runtime-static evidence consumer: `pass/0`。
- Candidate ledger: `pass/0`，业务状态仍 `needs_reconcile`，active candidate 为空，证据上限 `E2`。
- Old-entry scan: 预期 `reject/10`。
- Native prerequisite: `blocked/20`；当前 checkpoint 为 `offline_verified_b1`，`executionLease=none`，`storageReady=false`。
- 当前 runtime source tree observation: `46F174553F39FC283912C93ADA63DFAE8EA29442B19A958B3EA2CDDD07E82013`；未修改 `AWAKE/src`。
- 继续轮次 `continuation-20260824-1750-*`：契约、fixture、ledger、G3 plan、evidence consumer 均按预期通过；old-entry/runtime-static/native 均保持预期阻断。
- G2 收口轮次 `g2-close-20260824-1900-*`：Contract `pass/0`、G3 plan `pass/0`、E2 matrix `pass/0`；scope verifier `blocked/20`、Native prerequisite `blocked/20` 均为预期。
- Scope manifest 状态为 `pending_approval`；write set 六项精确、与排除范围不重叠，副作用开关全部关闭；没有授予 owner、lease ID 或源码写权限。
- 最终回归 `g2-close-final-20260824-*`：Contract/G3 plan/E2 matrix/ledger/evidence 均 `pass/0`；scope/Native `blocked/20`；old-entry/runtime static `reject/10`；受保护源码摘要未变。

## known_limitations

- G3 execution plan 仍 `REVIEW_PENDING`；一次独立只读 reviewer 未返回 verdict，不能计为 approval。
- 本轮另一架构只读审查代理返回 `429 Too Many Requests`，不计入 verdict；不得自动重试或伪造审查结论。
- G3-S0 scope verifier 现在是 Native prerequisite 的唯一 scope/lease 输入；旧 B1 `LOCKED_AFTER_GRILL` 不再提升 Persona exact approval。
- `PWB-AWAKE-013` 的冻结 source baseline 为旧摘要 `1E08A7A86916BD3C499123CD846A2C62537C4D618E4FF63302E103415C827F52`，与当前观察摘要不同；冻结 fixture/expected 未改。
- 当前只证明隔离契约和门禁行为，不证明 Persona runtime caller、实时更新、Storage persistence、游戏 prompt 消费或存档连续性。
- 未执行编译、模块同步、Bannerlord 启动、E3/E4/E5 验证。
- G3-S0 尚未实现；当前只完成 exact-scope proposal 和阻塞验证器，不能宣称 Storage readiness 已具备。

## protected_boundary

本批没有修改：

- `_houkai_merge/AWAKE/src`
- `_houkai_merge/AWAKE/ModuleData`
- `_houkai_merge/AWAKE/dist`
- 游戏 `Modules\\AWAKE`
- `PlayerExports`
- `persona-awake-candidate-ledger.v1.json`
- frozen candidate roots

## next_action

为 G3-S0 取得独立 exact-scope approval、owner、lease ID 和不重叠 write set；在该条件和独立只读复核通过前，不修改 `AWAKE/src`。取得并关闭 G3-S0 lease 后，按六文件 write set 实现 readiness 并做 E2 focused smoke；完成后才可进入 G3-A Native Persona runtime projection。

## last_error

`none_for_isolated_g2; independent_g3_review_incomplete; g3_s0_scope_pending; native_persona_scope_and_storage_readiness_not_authorized`
