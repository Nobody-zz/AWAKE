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

机器可校验任务图：`G3-S0 -> G3-A -> G3-B -> G3-C -> G4`。

`G3-S0` 只证明 Persona namespace、schema、owner/readiness 和 typed Storage 边界已经可被 runtime 使用；它不声称 save/load、branch、restart 或 recovery 已完成。完整存档连续性仍只属于 G3-B。

## 3. 共同门禁

每个 G3 批次都必须独立满足：

1. 当前批次有精确范围 `APPROVED`，不能用泛化的 B1 approval 代替；
2. 有唯一 active lease、owner 和 lease ID；
3. active lease 的 write set 不与 `tools/persona-awake-joint/*` 重叠；
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

只有 G3-S0 readiness 证据、Native exact-scope approval 和 G3-A 专属 active lease 同时通过后才可写入。

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
- 只覆盖上述调用链的 focused tests 和离线 fixture 适配。

### 5.3 A 验收

- `PWB-AWAKE-010-runtime-caller`：真实 NPC 对话入口 → facade → projection → prompt block 可观察；
- `PWB-AWAKE-011-dynamic-invalidation`：只改变一项关系/状态/scene/context field 时，snapshot fingerprint、invalidation 和下一次投影同步变化；
- reload 失败保留旧 RuntimeBundle、activation metadata 和 DSL；
- 一份 ContextSnapshot 同时驱动 Knowledge、Persona 和 Prompt；
- 无合法 bundle 时只有固定 identity-only `RUNTIME_FALLBACK` DSL。

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

- Persona DTO 已存在，但 `awake.persona.state` 尚未进入 `StorageNamespaceIds`；
- 三个 Persona schema 尚未进入 `AwakeStorageContract` 注册；
- 没有 Persona typed Storage adapter、save/load/branch/restart caller 或读档恢复接线；
- `NpcDialogueService.cs:1014-1019` 仍直接走 `WorldbookRuntime.Current` → `WorldbookService.BuildPersona`；
- `WorldbookRuntime.Reload()` 仍先 `ShutdownCurrent()` 再 `EnsureCreated()`；
- 当前无 Persona exact-scope active lease；
- 当前 `PWB-AWAKE-012` 只有“缺 lease 时正确阻塞”的负向证据。

因此本回合继续保持 `E2 / G3-S0 pending / G3-A blocked / G3-B blocked / G4 not_attempted`。

## 9. 非目标

- 不启动 Bannerlord，不同步 `dist`、游戏目录或 PlayerExports；
- 不修改 Workbench Provider、Ollama 预测试、提示词预算或作者 UI；
- 不修改 frozen candidate、candidate ledger canonical row 或旧 BuildId；
- 不把 B2 Knowledge approval 当作 Persona runtime approval；
- 不用编译、JSON 解析或静态存在性替代真实 caller、Storage 和游戏证据。
