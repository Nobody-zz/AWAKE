# PersonaWorkbench × AWAKE G3 执行门禁与精确写集

> 状态：`PREPARED / BLOCKED_PENDING_SCOPE_AND_LEASE`
>
> 本文件只固定 G3 的执行顺序、前置条件、精确写集和验收证据；不授权当前回合修改 `AWAKE/src`、`ModuleData`、`dist`、游戏目录或冻结候选。用户的“按推荐门禁继续”被解释为继续门禁准备和隔离验证，不伪造 Native approval、Storage readiness 或 active lease。

## 1. 目的

把 Persona runtime 从当前“静态缺口已知、共享运行时未接线”的状态推进到可审查的串行执行批次，同时避免把 B1 Knowledge 旧批次的批准范围误用于 Persona runtime。

最终链路仍为：

```text
Workbench source
  -> authoring-v2
  -> export-v1
  -> definition-v1
  -> approved RuntimeBundle
  -> ContextSnapshot
  -> Persona DSL
  -> game prompt
```

当前最高证据等级保持 `E2`。

## 2. 发现并修正的顺序矛盾

旧任务图把完整 G3-B Storage persistence 放在 G3-A Native runtime integration 之后，但 G3-A 的通用前置检查又要求 Storage readiness。若把“完整 Persistence 已完成”当作 readiness，门禁会形成不可满足的循环前置关系。

本批将两者拆开：

```text
G3-S0 Storage readiness contract
  -> G3-A Native Persona runtime projection
  -> G3-B Persona continuity/override persistence
  -> G3-C Offline integrated contract
  -> G4 Game evidence
```

机器可校验任务图：`G3-S0 -> G3-G0 -> G3-A0 -> G3-A -> G3-B -> G3-C -> G4`。

`G3-S0` 只证明 Persona namespace、schema、owner/readiness 和 typed Storage 边界已经可被 runtime 使用；它不声称 save/load、branch、restart 或 recovery 已完成。完整存档连续性仍只属于 G3-B。

## 3. 共同门禁

每个 G3 批次都必须独立满足：

1. 当前批次有精确范围 `APPROVED`，不能用泛化的 B1 approval 代替；
2. 有唯一 active lease、owner 和 lease ID；
3. runtime implementation lease 的 write set 不与 `tools/persona-awake-joint/*` 重叠；验证器修订必须作为独立的工具批次完成、验证并关闭 lease 后，runtime implementation 才能开始；
4. 同一文件只能有一个 active writer；批次结束后先关闭 lease，再进入下一批；
5. 报告必须带 command line、cwd、输入/输出 hash、status、exit code 和 observed errors；
6. 失败不得覆盖 last-known-good、冻结候选或把 draft/legacy fallback 当成 approved。

当前 `AWAKE-NATIVE-KNOWLEDGE-B2-20260824-checkpoint.md` 的 `execution_lease=completed` 只关闭 B2 Knowledge 批次，不满足本文件任何 G3 lease 条件。

## 4. G3-S0：Storage readiness contract

### 4.1 目标

建立一个可检查的 `awake.persona.state` readiness 边界，使 G3-A 可以读取 typed Storage 投影，而不提前实现完整 Persona persistence。

### 4.2 最小写集（取得 S0 专属 lease 后）

- `_houkai_merge/AWAKE/src/AwakeStorageContract.cs`
  - 注册三个 Persona schema/type 映射；
- `_houkai_merge/AWAKE/src/AiTaskConstants.cs`
  - 注册 `awake.persona.state` namespace；
- `_houkai_merge/AWAKE/src/WorldStateStore.cs`
  - 让 namespace readiness 和 typed boundary 可被现有 owner/queue/final-drain 使用；不实现 save/load 业务；
- `_houkai_merge/AWAKE/src/AwakeRuntime.cs`
  - 将 Persona namespace 纳入 readiness 检查，不改变现有 Worldbook Overlay 保存路径；
- `_houkai_merge/AWAKE.Tests/Program.cs`
- `_houkai_merge/AWAKE.Tests/AwakeTestFakes.cs`
  - 仅增加 S0 focused readiness/negative tests。

### 4.3 不在 S0 写入

`PersonaPersistenceModels.cs` 的业务语义修订、`ProbeExtension.cs` 生命周期接线、Persona continuity/override/recovery 写入、`NpcDialogueService.cs`、`WorldbookRuntime.cs`、`ModuleData`、`dist`、游戏目录和冻结候选均不属于 S0。

### 4.3.1 修订后的 readiness 不变量

- G3-S0 的批准不再由 scope manifest 内字段授权；`docs/persona-awake-joint-g3-s0-approval.v1.json` 与 `docs/persona-awake-joint-g3-s0-lease.v1.json` 是 detached authority records，均须绑定 scope 原始字节 SHA-256、任务身份和精确六文件写集；
- 三个 typed schema 固定为 `awake.persona.continuity.v1`、`awake.persona.override.v1`、`awake.persona.recovery.v1`，分别映射到 `PersonaPersistenceEnvelope`、`PersonaPersistenceEnvelope`、`PersonaRecoveryRecord`，共享 `awake.persona.state` / `PersonaStorageOwner`，但 S0 只注册/读取边界，业务写入仍属于 G3-B；
- readiness 建立采用 side-effect-free staging：required namespace 全部打开且 typed contract 全部满足后，才发布唯一 ready owner；任一 namespace 缺失、部分打开、取消或异常都必须清理临时 owner，公开 `NotReady`，不得留下可复用的半就绪 `_worldStateStore`；
- readiness 失败后必须可重试：第一次失败不改变已有状态，第二次在依赖恢复后能建立完整 owner；测试必须覆盖“部分打开→失败无 owner→重试成功”和“已有 owner 遇 required namespace 缺失→不继续使用旧 owner”；
- `awake_worldbook_overlay_v1` / `awake_worldbook_activation_v1` 是 `AwakeTerminalBehavior.SyncData` 的兼容 key，不是 Persona Storage schema；S0 只做 key→schema→owner→import/export path characterization，禁止把它们并入 `awake.persona.state`。

### 4.4 S0 验收

- `awake.persona.state` 被唯一 Storage owner 打开并报告 ready；
- 三个 Persona schema 可被 `AwakeStorageContract` 识别和规范化；
- `G3-S0-003`：部分 namespace/required namespace 失败后不创建 Persona persistence 记录、不覆盖现有状态、不留下可调用 owner；
- `G3-S0-004`：失败后依赖恢复可重试成功，且失败前后的 owner 状态可观察；
- `G3-S0-005`：逐项验证两个 Worldbook SyncData key 的 schema、owner、import/export path 与现有命名不变；
- `G3-S0-006`：现有 `awake_worldbook_overlay_v1` 与 `awake_worldbook_activation_v1` 兼容路径保持不变，且不与 Persona schema 混淆；
- focused 正向证据必须同时提交 `g3-s0-readiness-focused.json` 与 `g3-s0-readiness-trace.json`；报告、trace、当前 scope raw hash 和相关源码 SHA-256 必须互相绑定，验证器从 raw trace 与源码内容独立重算，不接受实现侧自报 `passed=true` 作为唯一证据；
- 最高证据等级为 E2，不产生 E3+ 声明。

## 5. G3-A：Native Persona runtime projection

### 5.1 依赖

G3-A 分为不接触 runtime 的 G3-A0 工具批次与 runtime projection 批次。只有 G3-S0 E2 readiness 证据、G3-G0 已完成、G3-A0 已完成、Native exact-scope approval 和 G3-A 专属 active lease 同时通过后才可写入 runtime。

### 5.1.2 G3-A0：静态门禁工具校正

G3-A0 是独立、只改验证器的精确小批次；它不改 `AWAKE/src`，不建立或模拟 Persona runtime，不读取/写入 Persona Storage。

- 唯一写集：`_houkai_merge/AWAKE/tools/persona-awake-joint/verify-runtime-bridge-static.ps1` 及其 focused test/fixture；
- 把检查拆为 S0、A、B 三层：G3-A 不把 Persona Storage 业务 wiring 当成 blocking 条件；
- Persona schema 注册验证必须检查常量声明、`IsKnownSchema` 与 `ExpectedSchema(WorldStateKind.*)` 的映射，不要求在方法体重复 schema 字面量；
- `Reload`、生产 caller 或其它必需锚点缺失时必须 fail-closed；生产 caller 检查必须定位真实调用点，不能以注释/字符串命中代替；
- 验收：既有 S0 常量实现报告 schema registered，缺失 `Reload`/caller 锚点报告 reject，且 G3-A 仍因 runtime projection 未实现而 reject；最高证据等级 E2。

G3-A0 必须拥有自己的 approval 与 lease，并在 runtime G3-A 开始前关闭。它不能以修改 expected 结果或弱化检测来制造通过。其 approval/lease、check matrix 与 A 的 predecessor/evidence contract 必须先由已关闭的 G3-G0 通用 verifier 验证。

G3-A0 的精确范围由 `docs/persona-awake-joint-g3-a0-scope.v1.json` 固定；G3-A 的精确范围由 `docs/persona-awake-joint-g3-a-scope.v1.json` 固定。两份 manifest 的规范化 write set 必须为零交集，approval/lease 必须各自绑定其 raw UTF-8 SHA-256；任何未列入的 fixture、test、工具或源码文件均不授权写入。G3-A0 scope verifier 必须验证 owner、lease 状态、前序 lease 已关闭及两集合零交集。

G3-G0 只提供 scope/authority trust root 与 A0 check matrix；它不授权也不实现 G3-A runtime evidence。G3-A 的 `PWB-AWAKE-010/011/019/020` evidence contract、report/trace schema 与实际 `Prompts.CompileAsync` 捕获须在 G3-G0 关闭后、G3-A execution approval 前，以独立预实施门禁冻结并审查。该预实施审查只批准精确实施义务与可执行验收，绝不把当前 legacy caller、缺失的 runtime 类型或未运行的测试表述为已经满足；这些事实必须在 G3-A lease 内被实现并由 focused runtime tests 实证。

G3-A 发放 runtime lease 前还必须运行独立的 `verify-g3-a0-predecessor.ps1`：G3-A scope 固定 A0 scope、approval、released lease 与该 verifier 的 raw SHA-256；验证器必须同时核验 A0 task/batch/gate/scopePath、user signoff、released lease ID 与精确 write set。该校验器不能替代 G3-A runtime evidence，也不得修改 G0C 所固定的通用 verifier。

### 5.1.1 Storage 边界决定（2026-09-11）

G3-A 的 `ContextSnapshot` **不读取 Persona Storage**。它只由当前 NPC/玩家身份、场景、已验证 Worldbook package/snapshot、现有对话可用的非 Persona 状态和本轮输入构成；不得读取或写入 `PersonaContinuity`、`PersonaOverride`、`PersonaRecovery`，不得调用 `PersonaPersistence*` 或将未持久化数据伪装成已恢复状态。

这使 G3-A 仅负责“已验证 bundle → 运行时 Persona projection → prompt block”的可达链路。continuity、override、recovery 的业务读写、save/load、branch/restart 与旧存档兼容全部留在 G3-B；Storage 不可用或没有合法 bundle 时，G3-A 只能返回固定 identity-only `RUNTIME_FALLBACK`，不得回退到 `WorldbookService.BuildPersona`。

### 5.2 最小写集

- `_houkai_merge/AWAKE/src/NpcDialogueService.cs`
  - 将生产 Persona caller 收敛到唯一 facade；移除对 `WorldbookRuntime.Current.BuildPersona` 的生产旁路；
- `_houkai_merge/AWAKE/src/WorldbookRuntime.cs`
  - 以 side-by-side validation 和成功后原子引用交换替代先清空再加载；
- `_houkai_merge/AWAKE/src/WorldbookService.cs`
  - 不再作为生产 Persona 权威路径；保留必要的离线兼容边界；
- `_houkai_merge/AWAKE/src/PersonaDslGenerator.cs`
  - 只消费 approved RuntimeBundle/ContextSnapshot 投影，保留固定 identity-only fallback；
- 新增以下三个 runtime 类型文件：`_houkai_merge/AWAKE/src/PersonaRuntimeModels.cs`（`ContextSnapshot`、`RuntimeBundle`）、`_houkai_merge/AWAKE/src/PersonaRuntimeProvider.cs`（`BuildProjection` facade）和 `_houkai_merge/AWAKE/src/PersonaRuntimeBundleLoader.cs`（side-by-side validation/reload）；
- 只覆盖上述调用链的 focused tests 和离线 fixture 适配；测试必须从实际 `NpcDialogueService` prompt 构建入口捕获传入 `Prompts.CompileAsync` 的变量，不接受仅直接调用 Generator 的证明。

### 5.3 A 验收

- `PWB-AWAKE-010-runtime-caller`：真实 NPC 对话入口 → facade → projection → prompt block 可观察；
- `PWB-AWAKE-011-dynamic-invalidation`：只改变一项关系/状态/scene/context field 时，snapshot fingerprint、invalidation 和下一次投影同步变化；
- `PWB-AWAKE-019-reload-last-known-good`：先成功加载，再提交无效 bundle、overlay 或 activation；bundle、activation metadata、Knowledge projection、Persona DSL 与 fingerprint 均保持旧值；
- `PWB-AWAKE-020-runtime-fallback-identity-only`：无 bundle、无效 bundle 或 runtime context 缺失时，只有固定 identity-only `RUNTIME_FALLBACK`，并证明生产调用未触及 `WorldbookService.BuildPersona`；
- 对每个 `PWB-AWAKE-010/011/019/020`，`AWAKE.Tests/Program.cs` 必须从真实 `NpcDialogueService` prompt-build 路径捕获传给 `Prompts.CompileAsync` 的 `rawVariables`，输出 case id、输入 fixture digest、bundle/revision/digest、ContextSnapshot fingerprint、旧/新投影 digest、`BuildPersona` invocation count、exit code 与源码 SHA-256；直接调用 generator 的测试不能单独通过；
- reload 失败保留旧 RuntimeBundle、activation metadata 和 DSL；
- 一份 ContextSnapshot 同时驱动 Knowledge、Persona 和 Prompt；
- ContextSnapshot fingerprint 必须包含 bundle id/revision/digest、scene、关系/状态、ContextModes 与本轮输入中实际参与投影的字段；任一 overlay/activation 变更必须使下一次 Persona projection 缓存失效；
- 无合法 bundle 时只有固定 identity-only `RUNTIME_FALLBACK` DSL。
- G3-A 的 runtime provider、bundle loader 和 NPC 生产 caller 不读取/写入 Persona Storage；Persona persistence 的可观察行为只能由后续 G3-B 验收。
- G3-A0 的静态验证仅验证结构和真实调用点；LKG、overlay invalidation、canonical fingerprint 与 fixed fallback 的通过结论必须由 G3-A focused runtime tests 给出，不能由符号或字符串存在性替代。

## 6. G3-B：Persona persistence

### 6.1 依赖

G3-B 必须在 G3-S0 已完成、G3-A 已通过 focused runtime 验收且 G3-A lease 已关闭后串行执行。G3-B 不与 G3-A 并行写共享文件。

### 6.2 只读审计已确认的最小写集

- `_houkai_merge/AWAKE/src/PersonaPersistenceModels.cs`
- `_houkai_merge/AWAKE/src/AwakeStorageContract.cs`
- `_houkai_merge/AWAKE/src/AiTaskConstants.cs`
- `_houkai_merge/AWAKE/src/WorldStateStore.cs`
- `_houkai_merge/AWAKE/src/AwakeRuntime.cs`
- `_houkai_merge/AWAKE/src/ProbeExtension.cs`
- 必要时新增单一 `PersonaPersistenceService.cs` 薄适配器
- 对应 focused tests 与 `PWB-AWAKE-012-persistence` 正向夹具

### 6.3 B 验收

- continuity、override、revision、active bundle selection 的 save/load 可复现；
- branch 使用稳定 `campaign/timeline/branch/character` 身份，不能用显示名；
- restart 后恢复引用和 last-known-good；
- recovery/commit 纳入现有 Storage owner、队列、背压和 final drain；
- 失败写入不覆盖上一份 committed state；
- `PWB-AWAKE-012-persistence` 从当前 `blocked/20` 提升前，必须有正向 evidence，不得只改 expected 文件。

## 7. G3-C：Offline integrated contract

### 7.1 依赖与写集

G3-C 只能在 G3-A 和 G3-B 各自完成、报告通过并关闭 lease 后执行。它只允许写入联合工具、fixture、报告和文档，不修改 `AWAKE/src`、`ModuleData`、`dist`、游戏目录或冻结候选。

### 7.2 验收

- 重新运行 `PWB-AWAKE-001–017`，保留负向 reject/blocked 语义；
- 对同一份 `ContextSnapshot` 检查 Knowledge、Persona 和 Prompt 使用相同的 bundle/revision/fingerprint；
- 覆盖首次加载、无效 reload、Overlay mutation、save/load/restart、旧数据兼容和禁用功能；
- `PWB-AWAKE-010/011/012` 必须分别拥有正向 evidence，不能用静态存在性替代真实入口/结算；
- G3-C 最高只提升到 E2；E3 需要新的 BuildId、ledger 和同步授权，E4/E5 需要匹配 BuildId 的用户游戏证据。

## 8. 当前阻塞事实

- G3-S0 Storage readiness 已完成 E2 验证并已关闭 lease；它不授予 G3-A runtime 写入权限；
- 没有 Persona typed Storage adapter、save/load/branch/restart caller 或读档恢复接线；
- `NpcDialogueService.cs:1014-1019` 仍直接走 `WorldbookRuntime.Current` → `WorldbookService.BuildPersona`；
- `WorldbookRuntime.Reload()` 仍先 `ShutdownCurrent()` 再 `EnsureCreated()`；
- G3-A0 与 G3-A 当前均无 exact-scope approval 或 active lease；
- 当前 `PWB-AWAKE-012` 只有“缺 lease 时正确阻塞”的负向证据。

因此本回合继续保持 `E2 / G3-S0 completed / G3-A0 blocked / G3-A blocked / G3-B blocked / G4 not_attempted`。

## 9. 非目标

- 不启动 Bannerlord，不同步 `dist`、游戏目录或 PlayerExports；
- 不修改 Workbench Provider、Ollama 预测试、提示词预算或作者 UI；
- 不修改 frozen candidate、candidate ledger canonical row 或旧 BuildId；
- 不把 B2 Knowledge approval 当作 Persona runtime approval；
- 不用编译、JSON 解析或静态存在性替代真实 caller、Storage 和游戏证据。
