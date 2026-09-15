# AWAKE G3-B Persona 持久化与运行时恢复计划

- task_id: `AWAKE-G3-B-PERSONA-PERSISTENCE-20260911`
- batch_id: `AWAKE-G3-B-PLAN-20260911`
- status: `revision_4_1_implemented_approved_e2`
- risk_class: `high-risk`
- engineering_version: `v0.2.0`（不改版号）
- primary_artifact: AWAKE runtime source + focused tests
- user_signoff_required: `true`
- current_maximum_evidence: `E2`

## 1. 目标与完成边界

在已经完成的 G3-A `RuntimeBundle -> ContextSnapshot -> PersonaProjection` 之上，补齐
AWAKE 模组本体的 **目标 NPC** Persona 状态存储与恢复接缝：

`Storage namespace -> Persona state validation -> ContextSnapshot hydration -> Persona DSL`

本批次完成后，目标 NPC 的 Persona runtime state 可以在 Storage 中保存、读取、校验并参与下一次 NPC 对话投影。它不声称已经有新的游戏玩法 producer；如果没有可接受的 Persona 事件，`sequence` 与 watermarks 保持 0。

## 2. 已确认事实

- 当前生产 NPC 对话路径通过 `WorldbookRuntime.BuildPersonaProjection(ContextSnapshot)` 获取 Persona DSL。
- `ContextSnapshot.ToPersonaContext()` 当前把 `Continuity` 初始化为空、`PlayerOverride` 置空，这是 G3-A 的明确隔离结果。
- `PersonaContinuityState`、`PersonaOverrideSet`、`PersonaPersistenceEnvelope` 和校验器已经存在，但没有 Storage 生产者/消费者闭环。
- `AwakeStorageContract` 已登记 Persona 相关 schema；`awake.persona.state` 当前不在默认 namespace 打开列表中，而且 `WorldStateStore` 没有 Persona state construction 或 apply dispatch。
- `WorldStateStore` 已提供受框架权限、队列、背压和 final drain 约束的 KV 读写能力。
- `PersonaContinuitySync` 已负责**玩家身份锚点**；它不描述目标 NPC 状态，也不是本批次的 authority 或定位前置条件。

## 3. 架构决策（待独立审查确认）

### D-1：主体与权威分工

推荐方案：

- 当前 G3-B 的唯一可持久化资格谓词必须 fail-closed：`target != null && target.Hero != null && Hero.MainHero != null && !ReferenceEquals(target.Hero, Hero.MainHero) && !string.IsNullOrWhiteSpace(target.Hero.StringId) && stableId == "hero:" + target.Hero.StringId`；canonical `PersonaSubjectStableId` = `hero:<Hero.StringId>`；不读取、写入或投影玩家 Persona；
- `ContextSnapshot.CharacterId` 只用于 Persona definition/bundle 投影，永远不能作为 Storage key、subject guard 或 upsert identity；
- `ContextSnapshot.PersonaSubjectStableId` 是新增的、独立的 Storage read/upsert identity；
- generic NPC 的 `npc:<character>:a<agent>` 尚无跨读档稳定性保证，本批次不为它读取或写入 Persona Storage，继续 fixed identity-only fallback；
- `awake.persona.state` Storage namespace 是 Hero Persona 可变 runtime state 的唯一权威；
- Persona DSL 只由当前 `ContextSnapshot` 投影生成，不直接读取 SQLite 或存档 JSON。

拒绝方案：把 NPC runtime state 塞进现有玩家 `SyncData` anchor。它既主体错误，也会绕开现有 Storage 权限、队列、重试和 final drain 契约。

### D-2：记录契约与范围

新增一个窄的 `PersonaRuntimeStateDocument`（名称可在实现前保持内部），固定 schema、canonical `PersonaSubjectStableId` key、campaign/timeline/branch、active bundle id/revision/digest、payload digest、revision、sequence、watermarks 与 `PersonaContinuityState`。所有字段都进入 validator；不允许把未校验 `PayloadHash` 当作状态来源。

`PersonaOverrideSet` 暂不接入生产路径，因为当前没有已批准的游戏内编辑入口或稳定 producer；模型保留，后续单独立项。

### D-3：Storage read/upsert 与提交语义

必须为 `WorldStateKind.PersonaContinuity` 增加专用 `NewState`、apply dispatch 和 typed read/upsert contract；hydration 与 upsert 都必须调用同一个唯一 fail-closed eligibility helper。其 payload 必须在 `SetAsync` 前通过 schema、`PersonaSubjectStableId`、timeline、bundle、digest 与 watermark 校验。固定 key 从 `campaign/timeline/branch/hero stable id` 规范化构造；同 key+同 payload 是幂等，冲突 payload 必须拒绝。

Persona 生成本身不产生持久化写入。写入只能来自显式的已接受状态变更接缝；本批次仅提供可调用的内部 upsert contract 和离线 fake producer，不伪造游戏玩法事件。

### D-4：namespace 与生命周期

`PersonaStateNamespace` 纳入默认 required namespace 集合，不能以“只打开 Persona namespace”替换既有 store。session ready 后由既有 `AwakeRuntime` readiness path 一次性打开完整集合；若完整集合不能建立，不发布新的 Persona owner，保留旧 owner/LKG。

新增可 fake 的 `PersonaSessionHydrationAdapter`（可作为 `PersonaPersistenceService` 的薄组成部分）：它异步读取已打开的 store、构造 deep-copy 的 session snapshot，并以 session generation、deadline 与 CancellationToken 守卫发布。`NpcDialogueService` 必须通过该 adapter/ContextSnapshot factory 用 `PersonaSubjectStableId` 取得目标 Hero state；generic NPC、`Hero.MainHero`、空 Hero id 和任一资格条件不满足的 target 都不调用 adapter；不能直接调用 generator 绕过 hydration。

### D-5：失败、LKG 与可变对象策略

- namespace 不可用：不发布新的 session snapshot；Persona 继续走固定 identity-only fallback 或当前 LKG，不阻塞普通对话；
- schema、campaign、Hero subject、bundle、digest 或 watermark 不合法：整份拒绝，不做部分装载；
- 写入失败或 commit unknown：不覆盖上一份 committed state；commit unknown 必须重读并比较 canonical payload 后才决定 applied/unknown；
- reload、取消、旧 session completion 或 caller 对象后续修改：不能替换已发布 snapshot 或污染下一 session；
- Provider 不得原地修改调用者传入的 `ContextSnapshot`；bundle normalization 必须在副本上完成。

## 4. 最小写集

预期只允许修改：

- `AWAKE/src/PersonaPersistenceModels.cs`
- `AWAKE/src/AwakeStorageContract.cs`
- `AWAKE/src/AiTaskConstants.cs`
- `AWAKE/src/WorldStateStore.cs`（仅在缺少窄的读写接缝时）
- `AWAKE/src/AwakeRuntime.cs`
- `AWAKE/src/PersonaModels.cs`
- `AWAKE/src/PersonaDslGenerator.cs`
- `AWAKE/src/WorldbookRuntime.cs`（仅 hydration/缓存边界）
- `AWAKE/src/NpcDialogueService.cs`
- 必要时新增一个薄的 `AWAKE/src/PersonaPersistenceService.cs`
- `AWAKE.Tests/Program.cs`
- `AWAKE.Tests/AWAKE.Tests.csproj`（若新增源码需要显式加入）
- 对应 `AWAKE/docs/evidence/` 离线证据和本批次检查点

明确排除：

- 外部内容包、世界书正文、PersonaWorkbench；
- `ModuleData`、`dist`、游戏目录和发布包；
- 玩家 Persona、`PersonaOverride` 游戏入口；
- recovery 状态机、branch/fork 迁移、NPC Persona 批量生产；
- 云端 Provider、游戏启动和实机操作；
- 修改已冻结 G3-A 门控记录或旧候选。

## 5. 入口 → 调用 → 结算 → 观察

1. 入口：campaign session ready 后，Storage readiness 已成功打开包含 `awake.persona.state` 的完整 required namespace 集合。
2. 调用：`NpcDialogueService` 的真实 prompt-build path 同时传递 definition `CharacterId` 和独立的 `PersonaSubjectStableId`。只有 Hero subject 请求 hydration adapter；adapter 从 canonical key 读取 record，执行 schema/subject/timeline/bundle/digest/watermark 校验，并发布 immutable session snapshot。
3. 结算：同一份 `ContextSnapshot` 的深拷贝携带有效 Hero continuity；`PersonaDslGenerator` 继续以 `CharacterId` 选择 definition 并生成 DSL。显式 accepted-state producer 才能通过 typed upsert 写回 Storage。
4. 可观察结果：日志出现 `persona.persistence.loaded`、`persona.persistence.saved` 或明确拒绝/降级 reason；下一次投影 fingerprint 随有效 NPC state revision/digest 改变，失败不会替换 LKG。

## 6. 验收用例

| 编号 | 用例 | 通过标准 | 最小证据 |
|---|---|---|---|
| G3-B-001 | namespace readiness | 完整默认 namespace 集合加 Persona namespace 一次建立；原有 namespace 不因 Persona 接入消失 | focused test + 日志断言 |
| G3-B-002 | typed valid load | 合法 Hero state 被完整装载到 immutable snapshot，字段与 sequence/watermarks 保持一致 | focused test |
| G3-B-003 | production projection reachability | Hero fixture 必须使 `PersonaSubjectStableId=hero:<id>` 与 definition `CharacterId=<id>` 不同；真实 prompt-build entry 只用前者读取 state、只用后者投影 definition，StableCore/CurrentIdentity/Experiences 进入同一次 DSL，fingerprint 改变 | real caller test + projection trace |
| G3-B-004 | typed valid upsert | 显式 producer 的 state 通过固定 schema/key 写入；重复相同 payload 幂等，冲突 payload 拒绝 | fake Storage contract test |
| G3-B-005 | invalid state | schema、campaign、NPC subject、bundle、digest、watermark、JSON 任一无效时整份拒绝且不调用 `SetAsync` | negative tests |
| G3-B-006 | commit unknown | 写入结果不确定时重读 canonical payload；不能覆盖 LKG 或伪报 applied | failure-path test |
| G3-B-007 | session isolation | 新 session 不复用旧 session snapshot；取消/旧 completion/调用者对象修改均不能写入新 session | lifecycle test |
| G3-B-008 | legacy compatibility | 无 Persona Storage 记录的旧档仍可进入，SyncData 锚点行为保持不变 | compatibility test |
| G3-B-009 | no fake producer | 没有真实事件时 sequence/watermarks 不被伪增，不能把投影生成次数当 accepted sequence | invariant test |
| G3-B-010 | generic target fallback | `npc:<character>:a<agent>` generic fixture 不读取/写入 Persona Storage，仍输出 identity-only fallback，且不能污染 Hero record | negative caller test |
| G3-B-011 | player Hero exclusion | `Hero.MainHero` fixture 不发生 Persona Storage Get/Set、不构造可持久化 subject，且 DSL 不注入 persisted continuity | negative caller test |
| G3-B-012 | empty Hero id exclusion | 空/空白 `Hero.StringId` fixture 不发生 Persona Storage Get/Set、不产生 `hero:` subject，且不注入 persisted continuity | negative caller test |

## 7. 离线验证门

计划通过并实现后，依次执行：

1. G3-B focused tests 和负向用例；
2. `dotnet build AWAKE.Tests\AWAKE.Tests.csproj -c Release --nologo`，0 warning / 0 error；
3. `AWAKE\tools\build.ps1`，API `1.3.15`；
4. `Awake.SdkSmoke.exe`，`PASS ALL`；
5. G3-A/G3-B 分层静态门禁；
6. 适用的 MAF lint 与状态文档校验；
7. 生成新的 evidence、BuildId 和 hash 记录。

最高只报告 E2。E3 需要新的候选打包和明确同步授权；E4/E5 需要用户提供匹配 BuildId 的游戏日志。

## 8. 实现顺序与停止条件

1. 独立只读审查本计划；
2. 用户签收后取得唯一 G3-B 实现 lease；
3. 先做 typed model/schema/validator、canonical key 与 `WorldStateStore` Persona dispatch；
4. 再把 Persona namespace 合并进完整 readiness 集合，接入 session hydration 和 immutable Persona snapshot；
5. 最后接入真实 NPC prompt context factory、显式 producer contract、缓存失效和日志；
6. focused tests 红→绿后才跑全量离线门；
7. 任一存档契约、权限边界或状态权威发生未预见变化，停止实现并修订计划；
8. 离线通过后冻结候选，等待新的 E3/E4/E5 决策。

## 9. 后续批次（不在本批次内）

G3-B 完成后，仍需独立的 Persona producer/settlement 批次，才能让实际游戏事件推进 sequence/watermarks，并达到“事件接受 → 状态结算 → 持久化 → 读档恢复”的完整 E5。该批次不得在本计划中偷渡实现。

## 10.1 Round 1 修订记录

- R1-P1-主体：从“玩家 Persona”改为与真实生产 caller 一致的“目标 NPC Persona”。
- R1-P1-Storage：将 typed state construction、apply dispatch、read/upsert、idempotency 与 commit-unknown 重读列为必需写集和验收。
- R1-P1-namespace：改为完整 required namespace 集合一次性建立，并增加既有 namespace 保留回归。
- R1-P1-anchor：明确玩家 SyncData anchor 不属于 NPC G3-B authority；NPC record 自带 subject/timeline/bundle/digest 绑定。
- R1-P1-reachability：增加可 fake hydration adapter 与真实 NPC prompt-build entry 硬门。
- R1-P2：增加 immutable deep-copy、session generation、取消和 caller-object mutation 的 LKG 不变量。
- R2-P1：拆分 `PersonaSubjectStableId` 与 definition `CharacterId`；本批 Hero-only，generic NPC 持久化明确延后，新增两种身份差异和 generic fallback 用例。
- R3-P1：Hero-only 资格收紧为排除 `Hero.MainHero` 的唯一谓词；hydration/upsert 和玩家 Hero 无 I/O 夹具都必须使用它。
- R4-P1：资格谓词收紧为包含非空 target/Hero/MainHero、非玩家引用、非空白 Hero id 与严格 stable-id 相等的单一 fail-closed helper；增加空 Hero id 无 I/O 夹具。

## 10. 审查与签收

- review_state: `docs/review-state/AWAKE-G3-B-PERSONA-PERSISTENCE-20260911.review.json`
- review_rounds: 最多 3 轮；高风险批次
- review_status: `approved_user_signed`
- review_state: `docs/review-state/AWAKE-G3-B-PERSONA-PERSISTENCE-R4-20260911.review.json`

## 10.2 Revision 4 新审查批次

用户已在 predecessor 三轮预算耗尽后明确授权创建新的 revision review batch。本批仅审查
revision 4 已固定的 subject-eligibility 修正；predecessor 的三轮记录保持历史证据，不重置、
不覆盖，也不将其 `REVISE` 改写为 `APPROVED`。
- user_signoff: `confirmed by user-26811 at 2026-09-11T12:34:15.3470856Z`
- implementation_status: `implemented_and_independently_approved`
