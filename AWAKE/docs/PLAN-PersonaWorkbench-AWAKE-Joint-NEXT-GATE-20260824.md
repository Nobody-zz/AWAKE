# PersonaWorkbench × AWAKE 联合目标：下一门禁执行计划

> 状态：`REVISE`。本文件承接 `PLAN-PersonaWorkbench-AWAKE-Joint-20260823.md` 与 Contract-Lock，不改变既有契约，不授权 Native、Storage、游戏目录或冻结候选写入。`docs/PLAN-PersonaWorkbench-AWAKE-Joint-ARCHITECTURE-MATRIX-20260824.md` 是本轮新增的设计补充，用于明确共享契约核心、Workbench/适配器/运行时/Storage 分层和 G3 任务依赖；它不改变门禁。
>
> 当前决策：继续 E0–E2 隔离工作；不以当前离线证据宣称运行时接线、实时更新、存档连续性或游戏内角色卡已完成。

## 1. 目标与边界

目标是把 PersonaWorkbench 的作者结果安全地带入 AWAKE，并最终由骑砍 2 实际消费：

`Workbench source → authoring-v2 → export-v1 → definition-v1 → approved runtime bundle → ContextSnapshot projection → game prompt`

本阶段只允许：

- 读取当前 Workbench、AWAKE 源码、契约、夹具和现有报告；
- 修改 `_houkai_merge/AWAKE/tools/persona-awake-joint/*`；
- 增加或修订隔离 fixture、契约报告、计划和审查记录；
- 生成新的离线 `recheck` 报告。

本阶段禁止：

- 修改 `_houkai_merge/AWAKE/src`、`ModuleData`、`dist`、游戏目录或正式内容包；
- 修改 `persona-awake-candidate-ledger.v1.json` 或旧冻结候选；
- 复用 `awake-20260820-syncpack-001` 作为新运行时桥接 BuildId；
- 进入 `010`、`011`、`012` 的真实 Native/Storage 测试；
- 启动 Bannerlord、同步模块或宣称 E3–E5。

## 2. 当前重测结论

重测时间：2026-08-24 13:00（本机时间），使用当前脚本、当前源码和新建 `recheck` 报告；历史 `smoke-final*` 报告不作为本轮权威结果。

### 2.1 E2 夹具结果

| 范围 | 当前结果 | 解释 |
|---|---|---|
| `001` | `pass/0` | approved Workbench source、显式 selection、crosswalk、authoring/export/definition 和 digest 链闭合 |
| `002–006` | 预期 `reject/10` | draft、缺 selection、未知 tag、映射损失、schema skew 均 fail closed |
| `007` | 预期 `reject/10` | partial-first 首错为 `persona.partial_export`，后续重复 ID/路径分支由 companion fixture 覆盖 |
| `007A–007C` | 预期 `reject/10` | duplicate ID、protected path、reparse path 分别独立拒绝 |
| `008` | `pass/0` | 无效 reload 不覆盖 last-known-good |
| `009` | 预期 `reject/10` | `awake.worldbook.v1` 不进入新 v2 runtime |
| `010–011` | `blocked/20` | 无 Native 精确范围 active lease，不伪造 runtime caller 或 dynamic invalidation 通过 |
| `012` | `blocked/20` | 无 Storage owner/lease，不伪造 persistence 通过 |
| `013` | `error/40` | 旧冻结 fixture 声明的 `AWAKE/src` 摘要已与当前源码不一致；这是基线漂移证据，不是修改旧 hash 的理由 |
| `014` | 预期 `reject/10` | approved source 缺少 Workbench/AV视频双层 approval evidence |
| `015` | 预期 `reject/10` | selection 的 `sourceRevision` 落后于 source |
| `016` | 预期 `reject/10` | enabled rule 无法表示为 definition-v1 正式字段 |
| `017` | 预期 `reject/10` | selection 缺少 `awake.persona.selection.v1` schemaVersion |

### 2.2 独立门禁

- Contract：`pass/0`。
- Candidate ledger：`pass/0`，但 ledger 业务状态仍为 `needs_reconcile`，无 active candidate，证据上限仍为 E2。
- Old-entry scan：`reject/10`，当前脚本已将 `WorldbookRuntime.Knowledge` 列为允许的当前入口；实际拒绝命中为 `WorldbookRuntime.Current@NpcDialogueService.cs:1014` 与 `BuildLegacyFallback@PersonaDslGenerator.cs:28/36/65/140`。这表示 Persona DSL 仍走旧路径，不表示 B2 世界知识入口未完成。
- Runtime bridge static：`reject/10`，当前未发现 `PersonaRuntimeProvider.BuildProjection`、`ContextSnapshot`、`RuntimeBundle`、ContextModes fingerprint 或 Overlay→Persona cache invalidation 闭环；`WorldbookRuntime.Reload()` 仍未证明 last-known-good 原子切换。
- Native prerequisite：`blocked/20`，这是 Persona runtime 共享写集的独立前置条件；B2 checkpoint 已为 `offline_verified`、`execution_lease=completed`，但 Persona Native 仍无唯一 active lease、Storage readiness 或精确范围授权。

权威重测报告目录：

`_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/recheck-20260824-1300-recheck-*`

### 2.3 2026-08-24 13:46 门禁重跑

本轮只读重跑生成以下新报告，作为当前 G2-D 观察证据：

- `gate-20260824-134634-contract.json`：`pass/0`。
- `gate-20260824-134634-ledger.json`：`pass/0`。
- `gate-20260824-134634-old-entry.json`：`reject/10`。
- `gate-20260824-134634-runtime-static.json`：`reject/10`。
- `gate-20260824-runtime-static-evidence-v6.json`：`pass/0`，证明 9 个 failed blocking check 与 9 个 `observedErrors` 一一对应；目标 runtime-static 报告仍为 `reject/10`。
- `gate-20260824-runtime-static-pass-evidence-v4.json`：`pass/0`，证明仅 contract 级诊断失败不会把报告升级为 `reject`。
- `gate-20260824-134634-native.json`：`blocked/20`。
### 2.4 当前完整 E2 矩阵

最新 `final-g2c-PWB-AWAKE-*` 报告已覆盖 `001–017`：001/008 为 `pass/0`；002–009、014–017 为预期 `reject/10`；010–012 为前置条件不足的 `blocked/20`；013 为冻结根摘要漂移触发保护性 `error/40`。Contract 与 ledger 为 `pass/0`，old-entry 与 runtime-static 为 `reject/10`，Native prerequisite 为 `blocked/20`。直接将每个 fixture 的 `expected.json` 与报告比对时，唯一不匹配是 013；008 的 `pass/0` 已由报告自身的断言确认。

### 2.5 2026-08-24 14:29 最终只读门禁重跑

本轮最新报告为：

- `artifacts/final-g2c-20260824-contract.json`：`pass/0`。
- `artifacts/final-g2c-20260824-ledger.json`：`pass/0`。
- `artifacts/final-g2c-20260824-old-entry.json`：`reject/10`，只由 `WorldbookRuntime.Current` 与 `BuildLegacyFallback` 造成；`WorldbookRuntime.Knowledge` 仍是允许的 B2 知识入口。
- `artifacts/final-g2c-20260824-runtime-static.json`：`reject/10`，Persona runtime facade、ContextSnapshot、RuntimeBundle、ContextModes fingerprint 和 Overlay→Persona cache invalidation 均未闭合。
- `artifacts/storage-check-20260824-runtime-static-observed-errors.json`：`reject/10`，13 项静态检查中 9 项 blocking failure 已进入 `observedErrors`；新增确认 Persona persistence 外部接线和 Storage schema 注册尚未建立。
- `artifacts/final-g2c-20260824-native.json`：`blocked/20`，无唯一 active lease，且 Storage 尚未就绪。

完整矩阵的直接 artifact 比对确认 `PWB-AWAKE-008-last-known-good` 为 `pass/0`；上一轮终端摘要中的 `UNEXPECTED_COUNT=1` 不对应 008 的权威报告，当前以各 fixture 的 `expected.json`、报告状态和退出码为准。`PWB-AWAKE-013` 不更新冻结期望或旧摘要。

本轮 `AWAKE/src` 保护树摘要为 `46F174553F39FC283912C93ADA63DFAE8EA29442B19A958B3EA2CDDD07E82013`；该值是 `Get-JointProtectedPathSnapshot` 对源码树中相对路径与文件 SHA-256 的确定性树摘要，不是单个 `.cs` 文件摘要。

### 2.6 2026-08-24 16:20 证据回归与独立门禁重跑

本轮新增并实际执行了 runtime-static evidence verifier：

- `gate-20260824-contract-final.json`：`pass/0`；`verify-contract` 已实际执行 synthetic pass fixture。
- `gate-20260824-ledger-final.json`：`pass/0`。
- `gate-20260824-old-entry-final.json`：`reject/10`，保持既有旧 Persona 入口发现。
- `gate-20260824-runtime-static-final.json`：`reject/10`，9 个 failed blocking check、9 个结构化 `observedErrors`。
- `gate-20260824-runtime-static-evidence-final.json`：`pass/0`，证明报告内部映射、固定 13 项 check 集和状态/退出码契约一致。
- `gate-20260824-native-final.json`：`blocked/20`，仍缺精确 Native scope、唯一 active lease 与 Storage readiness。

独立负向回归 `gate-20260824-runtime-static-negative-summary.json` 通过：删除关键 check、注入未知 check、将 blocking check 降级为 contract、把状态改成 unsupported，分别得到预期 `reject/10`、`reject/10`、`reject/10`、`not_attempted/30`。源码树摘要在两次 runtime-static 生成中均为 `46F174553F39FC283912C93ADA63DFAE8EA29442B19A958B3EA2CDDD07E82013`。

E2 fixture 全矩阵 `gate-20260824-e2-matrix-summary.json` 中 `001`、`008` 为 `pass/0`，其余 002–012、014–017 与既定预期一致；`013` 仍因冻结源码摘要漂移返回保护性 `error/40`，未修改冻结期望、冻结候选或源码。

## 3. 已确认的运行时缺口

这些缺口来自当前源码只读审计，不在本阶段直接修复。

### 3.1 入口仍有双权威路径

- `NpcDialogueService.cs:918` 使用 v2 `WorldbookRuntime.Knowledge` 查询世界知识。
- 当前源码已不再命中 `KnowledgeRuntime.Current` fallback，但开发测试入口 `AwakeDeveloperTestActions.cs:60` 仍直接依赖 `WorldbookRuntime.Knowledge`。
- `NpcDialogueService.cs:1013` 仍从 `WorldbookRuntime.Current` 调用 `WorldbookService.BuildPersona`，尚未统一到计划中的 `PersonaRuntimeProvider.BuildProjection(ContextSnapshot)`。

### 3.2 Persona 缓存失效不完整

- `PersonaDslGenerator.ComputeFingerprint` 已纳入关系、当前状态、记忆和部分 continuity，但没有纳入 `ContextModes`；生成逻辑却会使用 `ContextModes` 参与 tag 匹配。
- `WorldbookService` 的 `_personaCache` 由生成 fingerprint 索引，但当前 fingerprint 没有明确绑定 Persona registry revision/digest 与 Worldbook bundle revision。
- Worldbook Overlay 通过 `WorldKnowledgeQueryService.TryApplyOverlay` 改变 v2 Knowledge snapshot，却没有同步使 legacy Persona cache 失效。

### 3.3 Reload 不是原子 last-known-good 切换

`WorldbookRuntime.Reload()` 当前先执行 `ShutdownCurrent()`，再执行 `EnsureCreated()`。若新包验证或加载失败，旧 bundle 可能已经被清空；这与契约要求的 immutable `RuntimeBundle`、并行验证、成功后单引用原子发布不一致。

### 3.4 游戏内实时更新尚未闭环

当前能证明的是会话 generation、手动 reload、Overlay CAS 和每轮世界知识查询；尚不能证明：

- 文件/包 revision 自动发现；
- 一份 `ContextSnapshot` 同时驱动 Knowledge、Persona 和 Prompt；
- 关系、记忆、当前状态变化必然生成新 fingerprint；
- 失败 reload 保留旧有效 bundle；
- Persona continuity/override 能经 Storage 保存、读档、重启后恢复。

## 4. 后续任务图

### 4.0 门禁顺序修正（2026-08-24）

旧图把完整 G3-B Storage persistence 放在 G3-A 后面，但 G3-A 的通用前置检查又要求 Storage readiness，形成不可满足的循环。后续唯一推荐顺序改为：

```text
G3-S0 Storage readiness contract
  -> G3-A Native Persona runtime integration
  -> G3-B Persona persistence
  -> G3-C offline integrated contract
  -> G4 game evidence
```

机器可校验任务图：`G3-S0 -> G3-A -> G3-B -> G3-C -> G4`。

G3-S0 只建立 `awake.persona.state` 的 schema/namespace/owner/readiness 边界，不声称 save/load、branch、restart 或 recovery 完成；完整 persistence 仍属于 G3-B。

### G3-S0：Storage readiness contract（新增前置批次）

- **状态**：`pending / blocked_before_exact_scope`
- **范围**：Persona schema 注册、`awake.persona.state` namespace、现有 Storage owner/queue/final-drain 的 readiness 接口和 focused negative tests。
- **不包含**：Persona save/load 业务、`ProbeExtension` 生命周期接线、`NpcDialogueService`、`WorldbookRuntime`、dist、游戏目录和冻结候选。
- **最低验收**：namespace 可被唯一 owner 打开并报告 ready；readiness 失败不写 Persona 记录；现有 Worldbook Overlay/activation 兼容路径不变。
- **门禁**：需要自己的 exact-scope approval 和唯一 active lease；不能复用 B1 Knowledge lease。

### G2-A：E2 证据重测与基线隔离（已完成）

- **写集**：仅 `tools/persona-awake-joint/artifacts`。
- **结果**：当前脚本重测可复现；正例、负例、blocked 语义均与契约一致。
- **唯一未闭合项**：`013` 的旧 frozen source hash 与当前源码漂移。
- **验收**：新 `recheck` 报告存在、报告 hash 可读、退出码遵循 `0/10/20/30/40`。

### G2-B：冻结候选漂移归档（本轮完成）

- **写集**：本计划/审查记录和非权威报告；不得更新 `013` 的旧 hash。
- **动作**：把当前只读重测的源码树摘要 `46F174553F39FC283912C93ADA63DFAE8EA29442B19A958B3EA2CDDD07E82013` 作为观察事实记录；`PWB-AWAKE-013` 中的旧摘要 `1E08A7A86916BD3C499123CD846A2C62537C4D618E4FF63302E103415C827F52` 保留为冻结候选历史基线，不回写修正。
- **验收**：已明确区分“历史冻结候选”和“未来新候选”；任何新 runtime candidate 必须申请新 BuildId、独立 ledger row 和新的 source/dist/game 证据。

### G2-C：Workbench handoff 证据补强（本轮完成）

- **写集**：仅联合 fixture、contract report 和工具报告。
- **输入**：固定的 Workbench `approved` source bytes、显式 `awake.persona.selection.v1` sidecar、source revision、authoring revision、registry/crosswalk digest。
- **验收**：
  - source 原始字节与 `source.json` 一致；
  - selection 不从显示名、文件名或 mtime 推断；
  - Workbench `draft`、缺 approval evidence、stale revision 和 unsupported rule 均由独立夹具拒绝；
  - 失败不写 candidate、不改变 active selection、不覆盖 last-known-good。
- **新增夹具**：`PWB-AWAKE-014-missing-approval-rejected`、`PWB-AWAKE-015-stale-selection-rejected`、`PWB-AWAKE-016-unsupported-rule-rejected`、`PWB-AWAKE-017-selection-schema-rejected`。
- **不做**：不调用真实云端 Provider，不把 Workbench 接入 AWAKE runtime。

### G2-D：运行时静态接线与 fingerprint 规格（只读/报告）

- **写集**：仅联合工具报告或 docs；不改 `AWAKE/src`。
- **验收**：列出 `entry → caller → settlement → observable result`，并逐项证明或标记缺失：`ContextSnapshot`、`RuntimeBundle`、bundle/build/digest fingerprint、dynamic invalidation、fallback、continuity/override。
- **输出**：本轮已生成精确缺口矩阵；后续 Native 批次仍必须以 `PersonaRuntimeProvider.BuildProjection(ContextSnapshot)`、生产 caller、RuntimeBundle 原子 reload、fingerprint 和 cache invalidation 的具体文件/符号作为 write set，不允许用泛化“修运行时”作为 lease 范围。
- **新增证据回归**：`verify-runtime-static-evidence.ps1` 锁定 13 个 check ID 及 severity，独立验证 failed blocking → `observedErrors`、`status/exitCode` 和 pass/reject 对称性；`verify-contract.ps1` 会实际执行 synthetic pass fixture。该回归只提升 E2 报告可信度，不改变 runtime-static 的 `reject/10` 结论。

### G3-A：Native runtime integration（blocked）

只有同时满足以下条件才能开门：

1. Native 取得精确范围 `APPROVED`；
2. 存在唯一 active lease、owner、lease ID；
3. Native write set 与联合工具 write set 不重叠；
4. 新候选使用新 BuildId，不修改 frozen row；
5. G3-S0 readiness evidence 已通过；
6. 先实现唯一 `PersonaRuntimeProvider.BuildProjection(ContextSnapshot)`，再迁移生产 caller。

最低验收：`010` runtime caller、`011` 单字段 dynamic mutation、atomic RuntimeBundle reload 和 last-known-good 均有可复现离线/集成证据。

### G3-B：Storage persistence（blocked）

Storage owner/lease 到位前不接线。G3-S0 只完成 readiness 后，仍需等待 G3-A 通过并关闭其 lease；开门后必须验证 continuity、override、revision、bundle selection 的 save/load、分支、重启和旧数据兼容。

### G4：游戏内角色卡验收（not attempted）

由用户运行匹配 BuildId 的游戏候选后提供日志。验收必须覆盖：角色进入对话、Persona DSL 实际进入 prompt、世界知识与当前状态同步、角色卡更新后下一轮可观察变化，以及存档重启后的连续性。静态、编译、JSON、哈希不能替代该证据。

## 5. 推荐门禁决策

本轮已完成：

- G2-B：冻结候选漂移归档；
- G2-C：Workbench handoff 隔离 fixture；
- G2-D：运行时静态接线与 fingerprint 报告。

仍可继续的范围仅限于新的、精确授权的 G2 隔离审计或 Native/Storage 计划准备。

当前必须保持阻塞：

- G3-S0 Storage readiness contract；
- G3-A Native runtime integration；
- G3-B Storage persistence；
- G4 游戏内验收、同步和发布。

本文件不把“计划已批准”解释成“代码已授权”。只有 exact-scope approval、唯一 active lease 和不重叠 write set 同时满足，才能从 G2 进入共享运行时。

## 6. 复核命令

```powershell
pwsh -NoProfile -File .\_houkai_merge\AWAKE\tools\persona-awake-joint\run-fixtures.ps1 -FixtureId PWB-AWAKE-001-valid-approved -ReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-PWB-AWAKE-001.json
pwsh -NoProfile -File .\_houkai_merge\AWAKE\tools\persona-awake-joint\verify-contract.ps1 -ReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-contract.json
pwsh -NoProfile -File .\_houkai_merge\AWAKE\tools\persona-awake-joint\verify-candidate-ledger.ps1 -ReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-ledger.json
pwsh -NoProfile -File .\_houkai_merge\AWAKE\tools\persona-awake-joint\verify-old-entry-scan.ps1 -ReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-old-entry.json
pwsh -NoProfile -File .\_houkai_merge\AWAKE\tools\persona-awake-joint\verify-runtime-bridge-static.ps1 -ReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-runtime-static.json
pwsh -NoProfile -File .\_houkai_merge\AWAKE\tools\persona-awake-joint\verify-runtime-static-evidence.ps1 -InputReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-runtime-static.json -ReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-runtime-static-evidence.json -Case reject
pwsh -NoProfile -File .\_houkai_merge\AWAKE\tools\persona-awake-joint\verify-native-prerequisite.ps1 -ReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-native.json
pwsh -NoProfile -File .\_houkai_merge\AWAKE\tools\persona-awake-joint\verify-g3-plan.ps1 -ReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-g3-plan.json
pwsh -NoProfile -File .\_houkai_merge\AWAKE\tools\persona-awake-joint\verify-e2-matrix.ps1 -ReportPath .\_houkai_merge\AWAKE\tools\persona-awake-joint\artifacts\recheck-e2-matrix.json
```

复核时若源码摘要变化，必须先停在 `G2-B`，记录新摘要和变更来源；不得直接重写 `PWB-AWAKE-013` 或提升证据等级。

## 7. 13:24 后源码漂移与 B2 状态归属

本轮执行期间观察到 `_houkai_merge/AWAKE/src` 出现外部变更：`NpcDialogueService.cs` 在 13:00 重测后仍有更新。短窗口连续三秒哈希检查随后稳定，但变更来源、授权批次和对应 checkpoint 尚未在本任务中得到归属确认，因此按协调协议标记为 `conflict/in_doubt`，不把它当成本任务修改。

当前只读重测使用的源码树摘要：`46F174553F39FC283912C93ADA63DFAE8EA29442B19A958B3EA2CDDD07E82013`。摘要范围为 `_houkai_merge/AWAKE/src` 的受保护树快照，算法由 `Get-JointProtectedPathSnapshot` 固定；不得将其与 `NpcDialogueService.cs` 单文件 SHA-256 混用。

当前静态重测：

- `KnowledgeRuntime.Current` fallback 已不再被当前源码扫描命中；`WorldbookRuntime.Knowledge` 是 B2 已关闭的当前知识入口；
- `NpcDialogueService.cs:1014` 仍保留 `WorldbookRuntime.Current` → legacy `WorldbookService.BuildPersona` Persona 路径；
- `verify-runtime-bridge-static.ps1` 当前报告 `final-g2c-20260824-runtime-static.json` 为 `reject/10`，未发现 `PersonaRuntimeProvider.BuildProjection`、`ContextSnapshot`、`RuntimeBundle` 或 Persona cache invalidation 闭环；
- B2 checkpoint `AWAKE-NATIVE-KNOWLEDGE-B2-20260824-checkpoint.md` 当前为 `status=offline_verified`、`execution_lease=completed`，且明确只负责 Knowledge 查询，不授权 Persona runtime；`AWAKE-CURRENT.md` 当前焦点已转为 Marcus 迁移，不再作为 B2 状态来源。

在 Persona runtime 的源码归属、精确 approval、唯一 lease 和新 BuildId 对齐前，G3-A、G3-B 与游戏内验证继续保持阻塞；当前只允许继续 G2 隔离审计与离线 handoff 证据维护，不得编译、同步、更新 frozen candidate 或宣称 Native runtime 已完成。
