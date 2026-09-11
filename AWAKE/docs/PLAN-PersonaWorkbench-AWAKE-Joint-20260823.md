# Plan: PersonaWorkbench × AWAKE Joint Persona Contract and Runtime Bridge
_Locked via grill; user continuation accepts the recommended isolated E0–E2 path. Shared runtime implementation remains gated by exact Native approval plus a unique lease._

## Goal
建立一条可追溯、可回退、可被《骑砍 2》实际消费的角色卡链路：PersonaWorkbench 负责作者侧编辑、审核和候选导出；显式迁移/适配器将 Workbench 文档转换为 AWAKE 运行时定义；AWAKE 只选择合法的 `approved` 定义，并在 NPC 对话入口把静态角色卡与当前游戏身份、关系、记忆、场景和世界知识投影为非空 Persona DSL。Workbench 草稿、Workbench approved、导出候选、AWAKE approved 和 runtime selected 分层，失败不得覆盖上一份有效快照或伪造 approved。

## Required Annexes
- `docs/PLAN-PersonaWorkbench-AWAKE-Joint-CONTRACT-LOCK-20260823.md`：schema、状态机、crosswalk、运行时边界、夹具和证据命令。
- `docs/PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-20260824.md`：G3-S0/G3-A/G3-B 的串行门禁、精确写集与验收条件。
- `docs/PLAN-PersonaWorkbench-AWAKE-Joint-REQUIREMENT-EVIDENCE-MATRIX-20260824.md`：联合目标、权威证据、当前状态和下一放行条件的单页索引。
- `docs/persona-awake-candidate-ledger.v1.json`：唯一候选账本，当前 `needs_reconcile`，证据上限 E2。
- `docs/persona-awake-candidate-ledger.v1.draft.json`：非权威 staging copy，必须与 canonical ledger 字节一致才可使用。

## Approach

### Phase 0 — Authority and ownership
1. 以 candidate ledger 核对 `AWAKE-CURRENT.md`、`AWAKE-VALIDATION.md`、checkpoint、`BUILD_VERIFICATION.txt` 与 source/dist/game 实际文件；冲突只记录，不改写历史状态。
2. 与 Native Knowledge Boundary、Workbench K1、Worldbook Contract/Platform 对齐 owner/write-set；本批先只拥有 contract、fixture、isolated adapter 和 verifier 文件，禁止并行修改 `NpcDialogueService`、Storage、Worldbook manifest 或冻结候选。
3. 锁定 `awake.worldbook.v2` 为唯一新 runtime worldbook；现有 `awake.worldbook.v1` 在新 runtime 中稳定拒绝，不形成自动 v1→v2 运行时迁移链。`PWB-AWAKE-009-v1-runtime-rejection` 只验证拒绝语义。

### Phase 1 — Contract and state
4. 锁定 `awake.persona.authoring.v2` 为唯一作者中间层；Workbench `persona-workbench.character.v1` 只作迁移输入。K1 仍为 `REVISE`，本批只冻结 Contract-Lock annex 中的 K1A-compatible minimum，不把旧 K1 审查结果冒充实现批准。
5. 定义五层状态：Workbench draft → Workbench approved → export candidate → AWAKE approved → runtime selected；每条转换绑定 actor、revision、approval evidence、hash tuple、拒绝、撤销、幂等和回退语义。
6. Workbench v1 没有稳定游戏选择字段；导出必须提供显式 `awake.persona.selection.v1` sidecar/request，包含 `characterId`、`identityId`、`role`、`scope`、`priority`、`sourceRevision` 和 `selectionRevision`，不得从显示名、文件名、mtime 或普通 `Id` 推断。
7. 完成 Workbench tags、25 axes、2 flags、facet strengths、reaction/commitment、legacy trigger/boundary 与 provenance 的逐项 crosswalk；迁移可 `preserve_only`，export/runtime 对运行时语义损失一律 reject。

### Phase 2 — Isolated offline implementation
8. 在 `tools/persona-awake-joint` 实现离线迁移/适配器：Workbench v1 → authoring-v2 → export-v1 → definition-v1；输出 source/selection/authoring hash、registry digest、mapping report、adapter error 和确定性结果。
9. 在 `docs/persona-contract` 提供 JSON Schema、crosswalk、mapping/error/report contract；未知字段、未知 tag、未审核 observation、不支持 rule、stale revision、路径越界和 digest mismatch 必须 fail closed。
10. 在 `docs/fixtures/persona-awake-joint` 提供固定正例/负例；在 `tools/persona-awake-joint` 提供 runner、contract verifier、old-entry scan、ledger verifier、Native prerequisite verifier 和报告生成器。工具不能修改 runtime、ModuleData、dist、game 或 frozen roots。

### Phase 3 — Runtime integration, serial and gated
11. Native 计划必须取得精确范围 `APPROVED` 和唯一 active lease；`approved_for_b1_only` + `execution_lease=none` 不得进入共享运行时写集。
12. 串行实现唯一 `PersonaRuntimeProvider.BuildProjection(ContextSnapshot)`；禁止生产 caller 直接调用 `WorldbookRuntime.Current.BuildPersona`、legacy fallback、`KnowledgeRuntime.Current` 或通过 Knowledge 旁路选择 Persona。
13. 使用不可变 `RuntimeBundle`：worldbook activation、knowledge snapshot、persona snapshot、revision、BuildId、所有 digests 一起 side-by-side 校验，成功后单引用原子发布；失败保留 last-known-good。
14. 每轮 prompt 只生成一份 `ContextSnapshot`，统一提供给 knowledge、Persona 和 prompt；区分 `sourceHeroId`、`targetHeroId`、`characterId`、`personaIdentityId`，并把 session generation、capture token、bundle/build/digest 信息纳入 fingerprint。
15. 首次无合法 bundle 时只返回固定 identity-only `RUNTIME_FALLBACK` DSL，不包含 Workbench 文本、legacy prose、未批准 definition 或 unmapped tag。Persona continuity/override 持久化必须由 Storage owner 接线后再做 E5。

### Phase 4 — Evidence gates
16. 固定夹具：
    - `PWB-AWAKE-001-valid-approved`
    - `PWB-AWAKE-002-workbench-draft-rejected`
    - `PWB-AWAKE-003-missing-selection-rejected`
    - `PWB-AWAKE-004-unknown-tag-rejected`
    - `PWB-AWAKE-005-loss-report-rejected`
    - `PWB-AWAKE-006-schema-skew-rejected`
    - `PWB-AWAKE-007-duplicate-and-path-rejected`
    - `PWB-AWAKE-008-last-known-good`
    - `PWB-AWAKE-009-v1-runtime-rejection`
    - `PWB-AWAKE-010-runtime-caller` (Native lease required)
    - `PWB-AWAKE-011-dynamic-invalidation` (Native lease required)
    - `PWB-AWAKE-012-persistence` (Storage owner/lease required)
    - `PWB-AWAKE-013-frozen-candidate-isolation`
17. E0–E2 报告必须包含 command line、cwd、输入/输出 hash、exit code、status、断言和 cleanup；状态/退出码固定为 `pass=0`、`reject=10`、`blocked=20`、`not_attempted=30`、`error=40`。
18. E3 需要 reconciled ledger、新 BuildId、release-prep 和明确同步授权；E4/E5 需要匹配 BuildId 的用户游戏日志与存读档序列。当前 E3–E5 保持 blocked/not_attempted，不复用或改写 `awake-20260820-syncpack-001`。

## Key Decisions
- Workbench authoring 与 AWAKE runtime definition 分离，不做跨 Target Framework ProjectReference。
- `awake.worldbook.v2` 是新 runtime canonical；v1 只做拒绝验证，不自动迁移进 runtime。
- Workbench approved 不等于 AWAKE approved；runtime selected 只来自合法 approved candidate。
- Legacy fallback 不得把未批准作者文本带入生产 DSL；首次失败使用 identity-only fallback。
- Native 共享运行时要求“精确范围 approval + 唯一 active lease”，不是只看计划状态。
- DSL 是可重建投影；最终验收以游戏实际消费角色卡为准。

## Out of Scope
- 不重做 Workbench Provider、Ollama 预测试、提示词压缩、token usage 或已有 DSL 质量优化。
- 不直接把 Workbench 并入 AWAKE，不修改游戏目录、dist、ModuleData、冻结候选或正式内容包。
- 不在本批独立改动关系/记忆业务逻辑、存档 key、事件引擎或成人内容包。
- 不启动 Bannerlord、不切换启动器、不强制结束游戏进程、不宣称 E3–E5。
