# G3-A Native Persona Runtime 技术预审（2026-09-11）

> 性质：当前执行者的只读技术预审，不是独立审查，不产生 `APPROVED`、用户签收或执行 lease。
>
> 审查对象：`PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-20260824.md` 中的 G3-A；当前源码 BuildId 为 `awake-20260911-dialogue-chain-010`。

## 结论

**VERDICT: REVISE**

G3-S0 readiness 已有 E2 证据，但 G3-A 不能直接按当前写集实施。当前计划没有闭合 v2 Worldbook → Persona → Prompt 的唯一运行时所有权，并且一项静态验收会对现有常量别名产生假阴性。

## 已核实的调用链

当前 NPC 对话在 `NpcDialogueService.BuildPromptInputAsync` 中：

```text
WorldbookRuntime.Knowledge.Query
  -> WorldKnowledgeDecisionPolicy.BuildPromptBlock
  -> WorldbookRuntime.Current.BuildPersona
  -> rawVariables["persona_dsl"]
  -> NpcDialoguePromptPipeline.BuildBounded
  -> Prompts.CompileAsync
```

v2 包加载路径在 `WorldbookRuntime.EnsureCreated` 中创建 `WorldKnowledgeQueryService`，但同时将 `_current` 设为 `null`。因此生产 Persona DSL 唯一依赖 `WorldbookRuntime.Current.BuildPersona`，在正常 v2 初始化路径中没有来源。现有 Persona DSL 单元 smoke 只证明 `PersonaDslGenerator.Generate` 本身，不证明 NPC 生产 prompt 消费了 Persona block。

## 阻塞发现

### P0：v2 生产 Persona block 不可达

`NpcDialogueService` 仅在 `WorldbookRuntime.Current != null` 时调用 `BuildPersona`；`WorldbookRuntime.EnsureCreated` 的 v2 成功路径固定令 `Current` 为 `null`。G3-A 若只新增 facade 而没有指定它从已验证 v2 snapshot 构造、发布和读取 `RuntimeBundle`，仍可能编译通过但实际 prompt 的 `persona_dsl` 永远为空。

**修订要求：** `PersonaRuntimeBundleLoader` 必须从已验证的 v2 package/snapshot 构建候选 bundle；`PersonaRuntimeProvider.BuildProjection(ContextSnapshot)` 是 NPC 生产入口唯一 Persona 来源；旧 `WorldbookService.BuildPersona` 只能保留为明确的离线兼容边界，不能被生产 `NpcDialogueService` 调用。

**验收：** 用实际 `NpcDialogueService` 的 prompt 构建路径证明 approved bundle 的 Persona DSL 出现在传给 prompt pipeline 的变量中；无合法 bundle 时只出现固定 `RUNTIME_FALLBACK`，且不得调用旧路径。

### P1：reload 不满足 last-known-good

当前 `WorldbookRuntime.Reload()` 先 `ShutdownCurrent()`，再 `EnsureCreated()`。若后者验证或加载失败，已运行的 Knowledge、activation 与后续 Persona bundle 都会被清空。这与 G3-A 的“候选 side-by-side 验证后原子交换，失败保留旧 RuntimeBundle”的验收相冲突。

**修订要求：** 将加载拆为无副作用的候选构建与成功后的单次原子发布；`_knowledge`、activation 和 Persona runtime bundle 必须使用同一成功候选。失败只记录错误，保留上一份可用引用。

**验收：** 先成功加载 bundle，再提交无效 overlay/activation/package；验证 Knowledge、Persona DSL、activation metadata 和 fingerprint 均保持旧值。

### P1：G3-A 静态验收器当前有 schema 假阴性

`AwakeStorageContract.IsKnownSchema` 通过 `PersonaContinuitySchema`、`PersonaOverrideSchema`、`PersonaRecoverySchema` 常量注册 schema；`verify-runtime-bridge-static.ps1` 却在该方法体中搜索三个字面量，结果错误报告 `persona_storage_schema_registered=false`。这会诱导实现者把字面量重复写入注册方法以迎合测试，破坏当前的单点 schema 定义。

**修订要求：** 将 `verify-runtime-bridge-static.ps1` 纳入 G3-A 的测试工具写集，改为验证常量声明、`IsKnownSchema` 和 `ExpectedSchema(WorldStateKind.*)` 的三重映射；不得要求复制字面量。

**验收：** 在不复制 schema 字面量的前提下，静态检查能正确报告三条 Persona schema 映射已注册。

### P1：Storage 读取边界尚未定义

G3-S0 已注册 Persona schema/namespace/readiness，但 `WorldStateStore` 尚没有 PersonaContinuity / PersonaOverride / PersonaRecovery 的读写处理分支；`NewState` 与 apply switch 也未实现这三个 kind。G3-A 不应偷偷实现 G3-B persistence，但其 `ContextSnapshot` 是否、如何读取 continuity/override 目前未定义。

**修订要求：** G3-A 明确采用其中一个选择：

1. G3-A 的 `ContextSnapshot` 不读取 Persona Storage，所有 continuity/override/recovery 字段固定为“未提供”；业务读写完全留给 G3-B。
2. 将一个严格只读、无写入副作用的 Persona projection reader 列入 G3-A 精确写集，并说明其对空值、schema 不匹配和未就绪 Storage 的 fail-closed 回退。

未作此选择前，不得实现 facade。

### 已选择的边界（2026-09-11）

用户选择方案 1：G3-A 不读取 Persona Storage。G3-A 的 snapshot/projection 只使用已验证 v2 bundle 与当前运行时上下文；Storage 不可用、没有数据或 schema 不匹配均不触发读写，也不阻断身份 fallback。continuity、override、recovery 的读写、save/load、branch/restart 继续完全归属 G3-B。

该决定缩小 G3-A 的写集与失败面：G3-A 无需新增 Storage adapter，也不得通过直接读取 `WorldStateStore` 偷渡 G3-B 语义。独立复审应核对新 runtime 类型与 NPC caller 不包含 Persona Storage/Persistence 调用，并确认 fallback 不走 `WorldbookService.BuildPersona`。

### P2：验收需要绑定实际入口，而非字符串存在性

现有 `verify-runtime-bridge-static.ps1` 只能发现符号和部分字符串。G3-A 必须新增 focused test，捕获实际 prompt 变量或 `Prompts.CompileAsync` 请求，证明同一 `ContextSnapshot` 的 fingerprint、Knowledge block、Persona DSL 和 prompt request 彼此绑定；场景喊话路径保持不注入个人 Persona。

## 建议锁定的 G3-A 最小闭环

```text
已验证 v2 package/snapshot
  -> side-by-side RuntimeBundle candidate
  -> 成功后原子发布 Knowledge + activation + Persona bundle
  -> NPC 对话构造 ContextSnapshot
  -> PersonaRuntimeProvider.BuildProjection
  -> prompt 变量 persona_dsl
  -> Prompts.CompileAsync 请求可观察
```

非目标：Persona Storage 业务写入、save/load/branch/restart 恢复、游戏同步、游戏启动、发布，均保留给 G3-B / G4。

## 本次证据

- `verify-runtime-bridge-static.ps1`：`reject/10`，源码树摘要 `6A7488D4666DC7F51D511592A49B9266145D1D4FAA357D659ADB8B47C6319B54`。
- 静态审计确认：legacy Persona entry 1 个；`PersonaRuntimeProvider.BuildProjection`、`ContextSnapshot`、`RuntimeBundle`、ContextModes fingerprint、atomic reload 与 Overlay invalidation 均缺失。
- 不运行 Bannerlord，不同步游戏目录，不创建 G3-A approval 或 lease。

## 下一步

修订 G3-A 计划与精确写集以解决 P0/P1；随后由独立只读审查者评审该修订版。只有获得独立 `VERDICT: APPROVED`、用户签收及唯一 G3-A active lease 后，才可写入运行时代码。

## 独立审查第 1 轮回写（2026-09-11）

**VERDICT: REVISE**。独立审查确认本预审列出的生产 legacy caller、RuntimeBundle/ContextSnapshot/Provider 缺失、reload 不保留 last-known-good、legacy fallback、snapshot/fingerprint/invalidation 与静态门禁问题均为有效阻断项。

审查者另报“本预审文档缺失”。该项经主工作区核实为审查 worktree 可见性限制：本文件存在于权威路径 `D:\AWAKE-Dev\AWAKE\docs\`，但尚未提交，因而没有出现在新建审查 worktree；旧工作目录也没有可回填的同名文档。该项不构成迁移缺失，也不应通过从旧目录复制来修复。

本轮修订将静态验证器的修正拆为独立 G3-A0 工具批次，避免它与 runtime G3-A 写集冲突。第 2 轮独立审查必须检查 G3-A0/G3-A 的批次边界、fail-closed 语义、真实 caller 验收和非 Storage 边界；在新的 `VERDICT: APPROVED` 前，G3-A 继续 blocked。
