# G3 执行门禁计划审查记录

## 当前状态

- 计划：`PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-20260824.md`
- 状态：`PREPARED / REVIEW_PENDING`
- 最高证据等级：`E2`
- 本文件不授权 `AWAKE/src` 写入；只有 exact-scope approval、唯一 active lease 和独立验收通过后，才可建立对应实现 checkpoint。

## 本轮采用的门禁修正

1. 将 Storage readiness 与完整 persistence 分成 `G3-S0` 和 `G3-B`，解除旧任务图中的循环前置关系；
2. 让 `G3-S0 → G3-A → G3-B → G3-C → G4` 成为唯一推荐顺序；
3. 明确 B1 Knowledge lease 已关闭，不能授权 Persona runtime；
4. 把 `PWB-AWAKE-012` 的当前 blocked 结果定义为负向证据，不把它包装成正向存档能力；
5. 保持所有 runtime、Storage、dist、game 和 frozen roots 在本回合只读。

## 待独立只读复核

- G3-S0 是否确实只覆盖 readiness，而没有偷偷承载 persistence；
- G3-A 与 G3-B 的 write set 是否有重叠但仍保持串行 lease；
- `ContextSnapshot`、`RuntimeBundle`、Storage revision 和 fallback 的依赖是否闭合；
- 当前顺序是否会让 G3-A 在 Storage 没有 typed read boundary 时提前写入生产 caller；
- G4 的 BuildId、prompt 消费、Overlay 更新和 save/restart 证据是否仍被正确挡在后面。

## 当前决议

在独立只读复核返回 `VERDICT: APPROVED`、并由用户按推荐路径确认后，下一步只建立 G3-S0 的精确 scope/lease 准备，不直接开始 G3-A 或 G3-B。

## 独立复核尝试

- 已启动只读 G3-A scope reviewer，要求对照本计划、NEXT-GATE 和 ARCHITECTURE-MATRIX 检查顺序、写集与阻塞条件；
- 代理在返回 verdict 前无正文并被安全关闭；
- 本次没有 `VERDICT: APPROVED` 或 `VERDICT: REVISE`，因此不把该尝试计为审查证据，也不提升任何门禁状态；
- 现状继续保持 `REVIEW_PENDING`。

## 隔离计划验证 — 2026-08-24

- `verify-g3-plan.ps1`：`pass/0`，确认 `G3-S0 -> G3-A -> G3-B -> G3-C -> G4` 顺序、G3-S0/G3-B 分离、具体源码写集、跨文档引用和当前阻塞状态一致；
- `verify-e2-matrix.ps1`：`pass/0`，20/20 fixture outcome 按策略闭合；`PWB-AWAKE-013` 作为 `protected_baseline_drift` warning 记录，未改冻结期望；
- `verify-contract.ps1`：`pass/0`，新验证器已纳入 Contract-Lock 工具契约；
- `PWB-AWAKE-001-valid-approved`：`pass/0`；
- `PWB-AWAKE-012-persistence`：预期 `blocked/20`，无失败断言，未授予 lease；
- runtime-static：`reject/10`，evidence consumer：`pass/0`；
- Native prerequisite：`blocked/20`，当前仍为 `offline_verified_b1`、无 Persona active lease、Storage readiness=false。

## 变更后债务审计 — 2026-08-24

- 审计范围：新增/修改的三个隔离 PowerShell 工具及其一层 Contract-Lock/fixture 绑定；不扫描整个仓库，不触碰历史运行时代码。
- 非空非注释逻辑行约 `391` 行；三个脚本 PowerShell AST syntax errors 均为 `0`。
- 新工具均已被 Contract-Lock、`verify-contract.ps1`、checkpoint 或恢复命令引用，没有发现“新增但未接线”的隔离工具。
- 未发现可确认的重复权威路径；`verify-g3-plan.ps1` 负责计划一致性，`verify-e2-matrix.ps1` 负责 fixture 结果聚合，职责不同，不合并。
- `PSScriptAnalyzer` 本机不可用，因此未宣称 lint 通过；确定性命令回归仍全部按预期通过。
- 未删除历史死代码或重构生产运行时；本批没有需要清理的孤儿文件。

## 继续轮次隔离重测 — 2026-08-24 17:50

- `run-fixtures.ps1 -FixtureId PWB-AWAKE-001-valid-approved`：`pass/0`。
- `verify-contract.ps1`：`pass/0`。
- `verify-candidate-ledger.ps1`：`pass/0`；active candidate 仍为空，业务状态仍为 `needs_reconcile`。
- `verify-old-entry-scan.ps1`：预期 `reject/10`；旧 Persona 入口仍存在，未把拒绝结果误判为通过。
- `verify-runtime-bridge-static.ps1`：预期 `reject/10`；`PersonaRuntimeProvider.BuildProjection`、`ContextSnapshot`、`RuntimeBundle`、动态 fingerprint、atomic last-known-good、Persona persistence wiring 和 Storage schema registration 仍缺失。
- `verify-runtime-static-evidence.ps1 -Case reject`：`pass/0`；证明静态拒绝报告与当前未授权状态一致，不证明 runtime 已实现。
- `verify-native-prerequisite.ps1`：预期 `blocked/20`；`executionLease=none`、`storageReady=false`，且当前 checkpoint 仍属于 B1 Knowledge 边界。
- `verify-g3-plan.ps1`：`pass/0`；任务图仍为 `G3-S0 -> G3-A -> G3-B -> G3-C -> G4`。
- `verify-e2-matrix.ps1`：`pass/0`；20/20 fixture 按策略匹配，`PWB-AWAKE-013` 仍仅作为 `protected_baseline_drift` warning。

## 独立审查调度状态 — 2026-08-24 17:50

- 状态复核、架构闭合性、证据矩阵三路只读审查已按不重叠范围发起；子代理没有源码写权限、游戏副作用权限或 checkpoint/lock 修改权限。
- 架构审查代理触发 `429 Too Many Requests`，按协调协议不重复重试，也不把未完成审查计为 `VERDICT: APPROVED` 或 `VERDICT: REVISE`。
- 在获得有效独立 verdict 前，计划状态继续保持 `PREPARED / REVIEW_PENDING`，G3-S0 不建立源码 lease。

## G2 门禁收口批次 — 2026-08-24 19:00

- 新增 `docs/persona-awake-joint-g3-s0-scope.v1.json`：只记录 G3-S0 的六文件 proposal、排除范围、禁止副作用和 E2 验收项；当前状态为 `pending_approval`，没有 owner、lease ID 或 user signoff，不具备自授权能力。
- 新增 `tools/persona-awake-joint/verify-g3-s0-scope.ps1`：当前按预期返回 `blocked/20`；八项结构/边界断言全部通过，阻塞原因仅为 exact approval/active lease 缺失。
- 修正 `verify-native-prerequisite.ps1`：不再把旧 B1 Knowledge `LOCKED_AFTER_GRILL` 当作 Persona exact approval；现在消费 G3-S0 scope 结果，当前返回 `blocked/20`，`ExactApprovedScope=false`、`UniqueActiveLease=false`、`StorageReady=false`。
- `verify-contract.ps1`：`pass/0`，新 scope manifest 和 verifier 已纳入 Contract-Lock command/tool/artifact 检查。
- `verify-g3-plan.ps1`：`pass/0`，37 项断言通过，并校验 G3-S0 manifest schema、身份和六文件 write set。
- `verify-candidate-ledger.ps1`：`pass/0`；active candidate 为空，业务状态仍为 `needs_reconcile`。
- `verify-old-entry-scan.ps1`：预期 `reject/10`；当前源码树摘要仍为 `46F174553F39FC283912C93ADA63DFAE8EA29442B19A958B3EA2CDDD07E82013`。
- `verify-runtime-bridge-static.ps1`：预期 `reject/10`；证据仍准确暴露旧 Persona caller、缺失 RuntimeBundle/ContextSnapshot/fingerprint/invalidation/persistence wiring 等缺口。
- `verify-runtime-static-evidence.ps1 -Case reject`：`pass/0`；静态拒绝报告内部映射一致。
- `verify-e2-matrix.ps1`：`pass/0`，20/20 fixture 按声明策略匹配；`PWB-AWAKE-013` 继续仅记录 `protected_baseline_drift` warning。

### G2 变更后债务审计

- 审计范围限定为四个 PowerShell verifier、一个 G3-S0 scope manifest、Contract-Lock 和本批 checkpoint/log；未扫描整个仓库。
- 四个脚本 AST syntax errors 均为 `0`；manifest JSON 可解析。
- 新 scope verifier 已被 Contract-Lock、G3 plan verifier 和 Native prerequisite consumer 同时引用，没有发现新增孤儿入口。
- 本批没有确认的重复权威路径：scope verifier 负责 proposal/approval/lease，Native prerequisite 负责与 Storage readiness 联合放行；职责边界不同。
- 未执行 PSScriptAnalyzer（本机不可用）；未修改 C#、构建配置、dist、游戏目录或候选文件。

## G2 最终回归 — 2026-08-24

- `verify-g3-s0-scope.ps1`：`blocked/20`，当前 `pending_approval`，结构断言通过。
- `verify-contract.ps1`：`pass/0`；Contract-Lock hash 已刷新，scope manifest/verifier 均被纳入检查。
- `verify-g3-plan.ps1`：`pass/0`；37 项断言通过。
- `verify-native-prerequisite.ps1`：`blocked/20`；未误用旧 B1 approval，`ExactApprovedScope=false`、`UniqueActiveLease=false`、`StorageReady=false`。
- `verify-candidate-ledger.ps1`：`pass/0`；无 active candidate，仍为 `needs_reconcile`。
- `verify-old-entry-scan.ps1`：预期 `reject/10`；源码摘要保持 `46F174553F39FC283912C93ADA63DFAE8EA29442B19A958B3EA2CDDD07E82013`。
- `verify-runtime-bridge-static.ps1`：预期 `reject/10`；`verify-runtime-static-evidence.ps1 -Case reject`：`pass/0`。
- `verify-e2-matrix.ps1`：`pass/0`，20/20 fixture matched；冻结基线漂移仍只作 warning。

本批 G2 门禁收口完成。下一批只处理 G3-S0 的独立批准、owner、lease 和 readiness 实现；在这些条件成立前不修改 `AWAKE/src`。

## 独立只读复审 Round 1 — 2026-08-24

- reviewer：`Anscombe`；只读，无文件修改、无 lease、无游戏副作用；
- verdict：`REVISE`；
- P0：scope manifest 同时承载批准、用户签收和 lease，存在自授权风险；
- P1：三个 Persona schema/type/WorldStateKind/owner 映射未锁定；
- P1：required namespace 失败可能留下半就绪 `_worldStateStore`，缺少失败清理、重试和部分打开测试；
- P2：Worldbook SyncData key 与 Storage schema 概念未分开，缺少可执行 characterization；
- P2：现有测试没有 G3-S0 专项正负 evidence。

### Claude response / 修订动作

1. 接受 P0：新增 detached `persona-awake-joint-g3-s0-approval.v1.json` 与 `persona-awake-joint-g3-s0-lease.v1.json`；scope manifest 只保留 authority 路径和 raw scope hash 规则，verifier 不再读取 manifest 内 approval/lease 字段；
2. 接受 P1：scope manifest 锁定 `awake.persona.continuity.v1`、`awake.persona.override.v1`、`awake.persona.recovery.v1` 的精确类型、`WorldStateKind`、namespace、owner 与 G3-B-only 写边界；
3. 接受 P1：将 staging、失败无 owner、已有 owner 失效、部分打开和失败后重试成功写入 G3-S0 不变量与验收 ID `G3-S0-003/004`；本轮不提前改 C#；
4. 接受 P2：明确 `awake_worldbook_overlay_v1` / `awake_worldbook_activation_v1` 与 Persona Storage 分离，锁定各自 schema、owner 和 import/export path，并增加 `G3-S0-005/006` characterization 验收；
5. 接受 P2：scope verifier 已增加 typed schema、Worldbook 双边界、authority shape、scope hash 与 detached record 断言；focused runtime test 仍留在取得批准后的六文件写集内。

### 修订后离线证据

- `verify-g3-s0-scope.ps1` PowerShell parse：`pass`；
- G3-S0 scope verifier：`blocked/20`；13 项结构/边界断言通过，阻塞仅因 detached review 尚为 `PENDING`、user signoff 为 false、lease 为 `unleased`；
- 当前未修改 `_houkai_merge/AWAKE/src`、`ModuleData`、`dist`、游戏目录、PlayerExports、candidate ledger 或 frozen roots；
- 下一步：重新取得独立只读 verdict；只有 `VERDICT: APPROVED`、用户签收、精确 active lease 与 scope hash 全部通过后，才可写六文件 S0 实现集。

## 独立只读复审 Round 2 — 2026-08-24

- reviewer：`Russell`；只读，无文件修改、无 lease、无游戏副作用；
- verdict：`REVISE`；
- P1：detached approval/lease 只绑定 scope hash，未严格交叉校验 task/batch/gate/path/revision/actor/time，也未证明全局只有一个 active lease；
- P1：`verify-native-prerequisite.ps1` 仍依赖旧 Storage 文档与 checkpoint 文本正则，缺少 scope-bound focused positive evidence；
- P1：G3-S0-001..006 主要是声明，没有可执行报告证明 partial-open、no-owner、existing-owner、失败清理、重试成功和兼容路径；
- P1：Worldbook import/export path 与 Persona Storage namespace/owner/schema 集合不相交的校验不完整。

### Claude response / 第二轮修订动作

1. 接受记录绑定缺口：scope revision 固定为 `2`；approval/lease records 现在强制匹配 task、batch、gate、scope revision、scope path、review log path、record schema 和 scope raw hash；approval 只有 reviewer/user actor 与时间戳齐全才可放行；lease verifier 扫描同目录同批次 lease records，active 数量必须恰为 `1`；
2. 接受旧证据误放行风险：新增 `awake.persona.g3-s0-focused-report.v1.schema.json` 与 `verify-g3-s0-focused-evidence.ps1`；Native prerequisite 只接受 scope 声明的 focused report、当前 scope hash、`pass/0` 和全部正向断言，不再使用旧 Storage 文档/checkpoint 正则作为 readiness 证据；
3. 接受正向证据缺口：focused report 固定要求六个 acceptance ID，以及 `namespace_owner_unique`、`typed_schema_registry`、`partial_open_no_owner`、`existing_owner_required_missing_no_use`、`retry_after_failure`、`worldbook_syncdata_characterization`、`worldbook_schema_separation` 七项正向断言；
4. 接受边界校验缺口：scope verifier 现在逐项校验两个 Worldbook SyncData 的 import/export path，并校验 `personaStorage` 的 namespace、owner、schema 集合与 Worldbook schema 不相交；
5. 修复验证器异常语义：将当前门禁链路中的未限定 `FileNotFoundException` 改为 `System.IO.FileNotFoundException`；focused report 缺失现在稳定返回 `blocked/20`，不再冒泡为 adapter internal error。

### 第二轮修订后离线证据

- 五个 PowerShell verifier + focused verifier：AST parse 全部 `pass`；focused schema JSON parse `pass`；
- `verify-g3-s0-scope.ps1`：`blocked/20`；16 项结构/边界断言通过，`identityBound=true`、`activeLeaseRecordCount=0`、Persona/Worldbook 边界均通过；
- `verify-g3-plan.ps1`：`pass/0`，43 项断言通过；
- `verify-contract.ps1`：`pass/0`，43 项断言通过，focused tool/schema/command 已接线；
- `verify-g3-s0-focused-evidence.ps1`：预期 `blocked/20`，原因是实现尚未产生 `g3-s0-readiness-focused.json`；
- `verify-native-prerequisite.ps1`：预期 `blocked/20`，`exactApprovedScope=false`、`uniqueActiveLease=false`、`focusedEvidenceStatus=blocked`、`storageReady=false`；
- 当前仍未修改 `_houkai_merge/AWAKE/src`、`ModuleData`、`dist`、游戏目录、PlayerExports、candidate ledger 或 frozen roots。

## 独立只读复审 Round 3 — 2026-08-24

- reviewer：`Aristotle`；只读，无文件修改、无 lease、无游戏副作用；
- verdict：`REVISE`；
- P1：focused report/trace 仍可由实现侧自报布尔结果，且 acceptance/assertion item 约束不足；
- P1：Native prerequisite 对旧 Native 文档存在硬依赖，并未把子验证器退出码同时纳入放行；
- P1：approval/lease 缺少专用 schema、ISO UTC 时间、review event/log hash、active/released 生命周期约束；
- P1：Worldbook 实际 SyncData/import/export 映射仍只有 manifest/trace 声明，缺少源码绑定 characterization；
- P2：lease 扫描未区分 foreign active lease，focused IDs 允许未知或矛盾重复项。

### Claude response / 第三轮修订动作

1. focused evidence 改为三件套：报告声明 trace 路径/trace hash/源码 bindings；raw trace 必须包含七个 observed events、严格 task/scope/process identity 和当前源码 SHA-256；verifier 从 trace 数值与事件结构重新推导 namespace owner、typed schema、partial-open no-owner、existing-owner 不复用、retry、Worldbook mapping/separation，完全不信实现侧 `passed=true`；
2. Native prerequisite 将旧 Native plan/checkpoint/Storage 文档改为可选诊断；放行同时要求 scope 子进程 `exit=0 + report pass/0`、focused 子进程 `exit=0 + report pass/0`，并以当前 scope-bound focused trace 作为唯一 readiness evidence；
3. 新增 `awake.persona.g3-s0-approval.v1.schema.json` 与 `awake.persona.g3-s0-lease.v1.schema.json`，scope verifier 采用 exact-field 校验、ISO UTC 校验、review event ID、review-log SHA-256、`reviewSource`、active/released 生命周期和 foreign active lease 分离；
4. focused verifier 直接读取 `AwakeTerminalBehavior.cs` 与 `WorldbookRuntime.cs`，校验真实 SyncData key、import/export 方法和 activation schema；同时校验 Persona Storage 源码绑定；
5. 修复 focused evidence 相对路径解析与未限定 `FileNotFoundException` 工具 bug；缺失正向报告稳定返回 `blocked/20`。

### 第三轮修订后离线证据

- PowerShell AST parse：scope、focused、Native、plan、contract 全部 `pass`；7 个 G3-S0 JSON/契约文件全部可解析；
- `verify-g3-s0-scope.ps1`：`blocked/20`；20 项结构/边界断言通过，`identityBound=true`、`activeLeaseRecordCount=0`、`foreignActiveLeaseRecordCount=0`；
- `verify-g3-plan.ps1`：`pass/0`，43 项断言通过；
- `verify-contract.ps1`：`pass/0`，46 项断言通过；
- `verify-g3-s0-focused-evidence.ps1`：预期 `blocked/20`，当前没有实现阶段 raw trace/report；
- `verify-native-prerequisite.ps1`：预期 `blocked/20`，scope/focused 子进程均为 `20`，无误放行；
- 当前仍未修改 `_houkai_merge/AWAKE/src`、`ModuleData`、`dist`、游戏目录、PlayerExports、candidate ledger 或 frozen roots。

### 当前下一步

重新进行独立只读复审。只有新的 `VERDICT: APPROVED`、用户签收、ISO 时间戳、review-log hash、唯一 active lease 和后续 focused raw trace 全部闭合后，才进入六文件 S0 源码实现。

## 独立只读复审 Round 4 — 2026-08-24

- reviewer：`Pasteur`；只读，无文件修改、无 lease、无游戏副作用；
- verdict：`APPROVED`；
- 结论：未发现 P0/P1 级设计阻塞；scope revision、六文件写集、raw trace/source hash、approval/lease 身份绑定、子验证器退出码和 Worldbook 源码 characterization 已形成可执行门禁；
- 保留 P2：JSON Schema 对部分状态转换约束仍由 verifier 补足；raw trace 需在实现阶段由 focused runner 生成；源码 characterization 当前为精确字符串/路径断言，不替代 AST；这些不阻塞进入 G3-S0 实现，且不授权 G3-A/G3-B。

### Final review evidence

- 最终 verdict：`VERDICT: APPROVED`；
- 批准范围：仅 `G3-S0 Storage readiness`，不包含 runtime caller、完整 persistence、WorldbookRuntime reload、游戏同步或发布；
- 操作前置：先写入本记录、用户本轮“做”签收和唯一 active lease，再运行 scope verifier；scope verifier 未 `pass/0` 前不得改六文件。
- reviewEventId：`g3-s0-review-r4-20260824`
VERDICT: APPROVED

### 当前下一步

再次进行独立只读复审；若获得 `VERDICT: APPROVED`，再记录用户签收并建立唯一 active lease。此后才允许进入六文件 G3-S0 readiness 实现，且实现必须先产出 scope-bound focused report，再考虑 G3-A。

- reviewEventId：`g3-s0-review-r3-20260824`
