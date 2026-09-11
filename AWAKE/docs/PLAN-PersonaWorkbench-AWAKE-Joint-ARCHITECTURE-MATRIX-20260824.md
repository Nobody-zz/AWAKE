# PersonaWorkbench × AWAKE 联合架构矩阵与任务图

> 状态：`DESIGN_ONLY / G3_BLOCKED`
>
> 本文件是 `PLAN-PersonaWorkbench-AWAKE-Joint-20260823.md` 与 Contract-Lock 的架构补充，不改变既有 schema、状态机、门禁或租约规则，也不授权 `AWAKE/src`、Storage、`dist`、游戏目录或冻结候选写入。

## 1. 目标与当前证据

最终链路必须闭合为：

```text
Workbench source
  -> authoring-v2
  -> export-v1
  -> definition-v1
  -> approved RuntimeBundle
  -> ContextSnapshot projection
  -> Persona DSL
  -> game prompt
```

当前已证明到 E2 的部分：

- Workbench approved source、显式 selection、authoring/export/definition 和 digest handoff 可以离线闭合。
- 无效输入、缺审批、旧 schema、路径风险、映射损失和 stale revision 会 fail closed。
- `RuntimeBundle`、`ContextSnapshot`、生产 Persona facade、动态失效、Storage 连续性和游戏内 prompt 消费尚未实现或未验证。
- 当前 `AWAKE/src` 保护树摘要为 `46F174553F39FC283912C93ADA63DFAE8EA29442B19A958B3EA2CDDD07E82013`；此摘要属于只读观察证据，不是新候选 BuildId。

## 2. 五层边界

| 层 | 唯一职责 | 输入 | 输出 | 不得承担 |
|---|---|---|---|---|
| 共享契约核心 | 稳定 schema、canonical JSON、digest、revision、selection、crosswalk、错误/状态和 golden fixtures | 版本化 JSON 与测试向量 | 可被两端独立实现并互相校验的契约 | UI、AI 调用、战役状态、存档写入 |
| PersonaWorkbench 作者层 | 文本编辑、AI 扩写草稿、人工确认、Workbench approval、作者侧选择 | 用户文本、作者字段、Provider 草稿 | Workbench source、approved evidence、selection sidecar、候选导出请求 | 直接访问 AWAKE runtime、读取 Bannerlord 对象、伪造 runtime approved |
| 离线迁移/适配器层 | 确定性迁移、映射、校验、报告和隔离回归 | Workbench v1、selection-v1、registry/crosswalk | authoring-v2、export-v1、definition-v1、mapping/error/report | 修改游戏目录、修改运行时、自动替换冻结候选 |
| AWAKE 运行时适配层 | 选择 approved definition，构造 immutable RuntimeBundle，生成一次 ContextSnapshot，投影 Persona | definition/registry/worldbook、游戏身份、关系、记忆、场景、Overlay、Storage | 非空 Persona DSL、fallback、诊断和 cache/revision 结果 | 调用 Workbench Provider、读取 `WorldbookRuntime.Current` 旧 Persona 路径、把 Knowledge 当 Persona 旁路 |
| 游戏状态与 Storage 层 | 提供当前硬事实、可变关系/记忆/continuity/override，负责 save/load/restart | Bannerlord 生命周期、Campaign 状态、Storage | typed snapshot、revision、持久记录和恢复结果 | 保存作者源文本、把内容包当战役状态、在 tick 中阻塞 I/O |

### 2.1 共享核心的具体形态

共享核心不是跨 Target Framework 的二进制 ProjectReference，而是以下版本化证据集合：

1. `docs/persona-contract/*.schema.json`：对象结构和未知字段策略。
2. `persona-canonical-json.v1`：NFC、LF、无 BOM、属性序、数组规则和 digest 规则。
3. `persona-workbench-to-awake.crosswalk.v1.json`：Workbench tag/axis/flag/facet/rule 到 definition-v1 的唯一映射。
4. `selectionRevision`、`sourceRevision`、`authoringRevision`、approval evidence 和 BuildId 的匹配元组。
5. `awake.persona.adapter-error.v1`、`pass/reject/blocked/not_attempted/error` 退出语义。
6. `docs/fixtures/persona-awake-joint`：两端实现必须通过的正负 golden vectors。

Workbench 与 AWAKE 可以分别实现读取/写入，但不得分别发明上述字段、哈希或状态含义。

## 3. 状态与数据流

```text
Workbench draft
  -- human review + Workbench approval --> Workbench approved
  -- explicit selection + adapter validation --> export candidate
  -- distinct AWAKE approval --> AWAKE approved definition
  -- runtime bundle selection --> runtime selected
  -- one ContextSnapshot --> Knowledge + Persona + Prompt
```

每次提升必须同时携带：

- 稳定 `documentId/characterId/identityId`；
- source、authoring、selection、registry、mapping、export、definition digest；
- 对应 revision、actor、时间和 evidence ID；
- 失败原因、是否可重试、是否保持 last-known-good；
- 撤销或 supersedes 关系。

任何失败都不得：

- 把 `draft` 当成 `approved`；
- 把 Workbench approval 当成 AWAKE approval；
- 把 `RUNTIME_FALLBACK` 当成 Persona approved；
- 覆盖上一份可用 RuntimeBundle；
- 将作者文本或 legacy fallback 旁路注入生产 prompt。

## 4. 静态与动态边界

| 变化类型 | 权威来源 | 触发时机 | 必须变化 | 不允许的做法 |
|---|---|---|---|---|
| 作者文本、标签、规则、选择改变 | Workbench source + approved export | 显式批准后 reload/promote | source/authoring/selection/revision、bundle digest | 监听 mtime 后直接覆盖运行时 |
| Worldbook 包或 registry 改变 | v2 package/registry | side-by-side validate 后原子 reload | bundle/package/manifest/worldbook revision | 先清空旧 bundle 再尝试加载 |
| 关系、当前状态、记忆、场景、context modes 改变 | 游戏快照/Storage/Overlay | 每次 prompt build 或明确 invalidation | ContextSnapshot fingerprint；若字段渲染则生成新 DSL | 每个 tick 读文件、网络或全量重建所有角色 |
| continuity/override 改变 | Storage | 玩家操作、事件结算、读档/重启 | persistence revision、snapshot fingerprint | 写入内容包、仅存内存、跨角色串用 |
| identity/role/硬事实改变 | identity resolver + Bannerlord 当前状态 | lifecycle/对话上下文刷新 | snapshot identity/role 和投影结果 | 用显示名、临时 AgentIndex 或旧缓存冒充稳定身份 |

运行时必须遵守：每轮 prompt 只生成一份 `ContextSnapshot`；Knowledge、Persona 和 prompt 使用同一份快照。`ContextSnapshot` 至少区分 `sourceHeroId`、`targetHeroId`、`characterId`、`personaIdentityId`，并纳入 `sessionGeneration`、`captureToken`、`activeBundleId`、`buildId` 和全部选中 digest。

## 5. G3 任务图

### G3-S0：Storage readiness contract（当前 pending）

G3-S0 是 G3-A 的 readiness 前置，不是完整 Storage persistence。它只负责让 `awake.persona.state` 的 schema、namespace、owner 和 typed boundary 可被 runtime 检查；不得在此批宣称 save/load、branch、restart、recovery 或 Persona continuity 已完成。

G3-S0 完成后必须关闭自己的 lease，才能进入 G3-A；它与 G3-A/G3-B 不并行写共享文件。

### G3-A：Native Persona runtime integration（当前 blocked）

**前置门禁**

- Native exact-scope `APPROVED`；
- 唯一 active lease、owner、lease ID；
- write set 不与 `tools/persona-awake-joint/*` 重叠；
- 新 BuildId，不修改 frozen row；
- G3-S0 readiness evidence 已通过；不得把 readiness 伪造成完整 persistence。

**唯一写集应细化到**

- 新增 `ContextSnapshot` 与规范化/fingerprint 代码；
- 新增 immutable `RuntimeBundle` 与 side-by-side loader/reload；
- 新增唯一 `PersonaRuntimeProvider.BuildProjection(ContextSnapshot)`；
- 修改生产 `NpcDialogueService` caller，使其只消费 facade；
- 迁移或删除生产 legacy Persona 入口前，保留可验证的 identity-only fallback；
- 相关离线 fixture、静态扫描和 focused tests。

**最小验收**

- `PWB-AWAKE-010-runtime-caller`：entry → caller → projection → prompt block 可观察，禁止旧入口。
- `PWB-AWAKE-011-dynamic-invalidation`：只改变一个关系/状态/scene/context field 时，fingerprint、invalidation 和下一次投影结果符合契约。
- invalid candidate reload 保留旧 RuntimeBundle、旧 activation metadata 和旧 DSL。

### G3-B：Storage persistence（当前 blocked）

**当前源码事实**

- `src/PersonaPersistenceModels.cs` 已提供 continuity/override/recovery 的 DTO、branch-aware key builder 和 validator，但当前搜索未发现其在该文件之外的运行时调用。
- `src/AwakeStorageContract.cs` 当前未注册 `awake.persona.continuity.v1`、`awake.persona.override.v1` 或 `awake.persona.recovery.v1`；这些模型只能视为契约基础，不是已接线的持久化实现。
- `WorldbookRuntime` 的 Overlay/activation 当前由 `AwakeTerminalBehavior.SyncData` 直接保存到 `awake_worldbook_overlay_v1` 与 `awake_worldbook_activation_v1`，不经过 `WorldStateStore`；G3-B 必须明确保留该兼容路径还是建立 typed Storage adapter，不能把它与 Persona continuity 静默合并。
- `AwakeStorageContract.WorldbookOverlaySchema` 当前是声明但未被 `IsKnownSchema` 或 `ExpectedSchema` 使用的孤立常量；这是待确认的历史债务，不在本批直接删除或改写。
- 因此 G3-B 不能从“模型存在”直接提升为 Storage ready，必须补齐 owner、namespace 注册、生命周期接线和 save/load/restart 证据。

**唯一写集应细化到**

- Persona continuity/override/revision/bundle selection 的 Storage DTO 和 namespace；
- save/load、读档后引用重建、重启恢复和旧数据兼容；
- readiness、幂等、队列/背压和关闭 drain 证据；
- `PWB-AWAKE-012-persistence` 及对应 focused tests。

**硬边界**

- 作者源文本、Workbench approval、内容包正文不进入 Campaign Storage；
- Storage 只保存战役可变状态和投影控制数据；
- 失败写入不覆盖上一份 committed state；
- continuity/override 的 identity key 必须使用稳定 `personaIdentityId/characterId`，不能使用显示名。

### G3-C：Offline integrated contract（依赖 G3-A/G3-B）

- 重新运行 `001–017`，并增加 G3 runtime/storage 结果到统一 report；
- 对同一 `ContextSnapshot` 验证 Knowledge、Persona、Prompt 使用同一 bundle/revision/fingerprint；
- 覆盖首次加载、reload 失败、overlay mutation、save/load/restart、旧数据和禁用功能；
- 最高只宣称 E2，除非有匹配候选的同步/游戏证据。

机器可校验任务图：`G3-S0 -> G3-A -> G3-B -> G3-C -> G4`。

### G4：Game evidence（用户运行后）

必须由用户运行匹配 BuildId 的候选并提供日志，验证：

1. NPC 对话入口确实调用 Persona facade；
2. Persona DSL 实际进入 prompt，而不是只在诊断面板出现；
3. Knowledge、关系、记忆、场景和 Persona 使用同一轮状态；
4. 修改角色卡/Overlay 后下一轮可观察变化；
5. 读档、重启后 continuity/override 和 bundle selection 保持正确；
6. 失败 reload 保持 last-known-good；
7. 无合法 bundle 时只出现 identity-only fallback。

## 6. 证据矩阵

| 阶段 | 证据等级 | 证明内容 | 当前状态 |
|---|---|---|---|
| Contract/schema/crosswalk | E0–E1 | schema 可解析、canonical/digest/vector 契约存在 | 已完成 |
| Workbench → definition | E2 | 正负 fixture、审批/selection/revision/digest handoff | 已完成，K1 full implementation 仍 `REVISE` |
| Runtime static bridge | E0–E2 | 旧入口、ContextSnapshot、RuntimeBundle、fingerprint、invalidation，以及 Persona persistence model/wiring/schema 缺口 | 已完成，结果 `reject/10`；最新报告逐项写出 blocking errors |
| Native runtime caller | E2 | 010、生产 caller、fallback、last-known-good | blocked |
| Storage continuity | E2 | 012、save/load/restart/旧数据兼容 | blocked |
| Candidate package/sync | E3 | 新 BuildId、ledger、hash、release-prep、明确同步授权 | 未开始 |
| Game prompt consumption | E4 | 匹配 BuildId 的日志和 prompt 证据 | 未开始 |
| Save/restart regression | E5 | 存档分支、重启、长期连续性 | 未开始 |

## 7. 被否决的替代方案

### 7.1 共享二进制库

否决：Workbench 与 Bannerlord/Marcus Target Framework、发布节奏和依赖边界不同；ProjectReference 会把作者工具与游戏运行时耦合。采用版本化 schema + crosswalk + golden vectors。

### 7.2 每个服务分别捕获快照

否决：Knowledge、Persona、Prompt 可能看到不同关系/Overlay/revision，导致同一轮自相矛盾。采用单一 `ContextSnapshot`。

### 7.3 原地 reload

否决：先销毁当前对象会丢失 last-known-good。采用 immutable bundle、side-by-side validation 和原子引用交换。

### 7.4 legacy prose fallback

否决：错误或缺失 approval 时会把未审核作者文本带入生产 prompt。采用固定 identity-only `RUNTIME_FALLBACK`，并让其在日志中可区分、在 DSL 中不可冒充 approved。

## 8. 当前决策与下一步

本文件不提升任何证据等级，也不打开 G3。当前唯一允许的动作是：

1. 保持 G3-S0/G3-A/G3-B/G4 阻塞；
2. 用本文件作为 Native/Storage exact-scope approval 的准备材料；
3. 取得 approval 与 lease 后再按 G3-S0 → G3-A → G3-B → G3-C 串行实施；
4. 每批只接受与 declared write set 对应的证据；
5. 若源码树摘要再次漂移，先回到 G2-B，不改写 `PWB-AWAKE-013`。

## 9. 权威参考

- `docs/PLAN-PersonaWorkbench-AWAKE-Joint-20260823.md`
- `docs/PLAN-PersonaWorkbench-AWAKE-Joint-CONTRACT-LOCK-20260823.md`
- `docs/PLAN-PersonaWorkbench-AWAKE-Joint-NEXT-GATE-20260824.md`
- `docs/persona-awake-candidate-ledger.v1.json`
- `tools/persona-awake-joint/artifacts/final-g2c-20260824-contract.json`
- `tools/persona-awake-joint/artifacts/final-g2c-20260824-runtime-static.json`
- `tools/persona-awake-joint/artifacts/final-g2c-20260824-native.json`
