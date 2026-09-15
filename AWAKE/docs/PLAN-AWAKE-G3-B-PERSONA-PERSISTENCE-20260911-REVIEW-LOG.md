# AWAKE G3-B Persona 持久化审查日志

## Round 1 — 2026-09-11

- 审查方式：独立只读审查。
- 读集：G3-B 计划、控制面、Persona/Storage/Runtime/Dialogue/Event/Test 真实调用链。
- 写入：无。
- 结论：`VERDICT: REVISE`。

### 阻断性发现

1. **P1 / 主体错配**：生产 `NpcDialogueService` 用目标 NPC 的 stable id 构造
   `ContextSnapshot`，而计划限定恢复玩家 Persona；不能把玩家状态注入 NPC DSL。
2. **P1 / Storage 写入缺口**：`WorldStateKind.Persona*` 只有枚举/schema，当前
   `WorldStateStore` 没有 Persona state construction 或写入分派。
3. **P1 / namespace 替换风险**：只请求 Persona namespace 会触发现有 store retire，
   新 store 不再包含既有 required namespaces。
4. **P1 / 锚点与 Storage 未绑定**：SyncData anchor 没有 canonical storage key、已提交
   revision、bundle identity 或 payload digest，无法安全定位/验证 Storage record。
5. **P1 / 生命周期测试不可达**：测试工程未编译真实 `AwakeEventBehavior`；计划没有
   可 fake 的 session-hydration adapter，却要求真实 prompt 到 DSL 的可达性证明。

### 非阻断性发现

- **P2 / snapshot 与 LKG**：当前 `ContextSnapshot` 可变，Provider 会补 bundle 字段；计划
  没有 deep-copy/跨 session 隔离的不变式。

### 下一动作

修订计划，分别锁定 NPC/玩家主体、typed Storage read/upsert、namespace 扩展策略、
anchor-record 绑定、可测试 hydration adapter 与 immutable snapshot 契约；随后进入 round 2
独立只读审查。

VERDICT: REVISE

## Revision 4 新批次 / Round 2 — 2026-09-11

- 审查方式：第二位独立只读审查。
- 审查对象：revision 4.1，SHA-256 `E6511A2A01F6394A557271D2224370AA125D58A3BDDEA1422DA872869103074E`。
- 结论：`VERDICT: APPROVED`。
- 结果：未发现 P0/P1；资格谓词已 fail-closed，且既有 Storage、namespace、身份分离、LKG 与真实 prompt-path 验收均未回退。

### 限制

本结论只批准计划门禁。当前源码尚未实现 eligibility helper、hydration/upsert 或
G3-B-011/012；实现后仍须完成 E2 离线验证。

VERDICT: APPROVED

## Revision 4 新批次 / Round 1 — 2026-09-11

- 审查方式：用户授权后的新独立只读审查批次。
- 结论：`VERDICT: REVISE`。
- P1：原资格谓词允许空 `Hero.StringId`，而 `AwakeNpcTarget.FromHero` 会产生共同的
  `hero:` stable id，可能使不同未识别目标共享 Storage key。

### 修订方向

资格必须 fail-closed：target/Hero/MainHero 均非空、目标不是 MainHero、Hero.StringId 非空白，
且 stable id 与 `hero:` + 原始 StringId 严格相等。hydration/upsert 都必须调用同一个 helper；
新增空 StringId 的 no-Get/no-Set 负向夹具。

VERDICT: REVISE

## Round 3 — 2026-09-11

- 审查方式：第三位独立只读审查；高风险批次的 3 轮预算至此耗尽。
- 结论：`VERDICT: REVISE`。
- 新 P1：Hero-only 资格遗漏 `Hero.MainHero` 排除；现有 `AwakeNpcTarget.FromHero` 可为任何
  Hero 生成 `hero:<id>`，因此仅靠调用场景不足以阻止玩家 Persona Storage I/O。

### 已写入的最小修正

计划锁定唯一资格谓词：`IsHero && Hero != Hero.MainHero && stableId == "hero:" + Hero.StringId`。
hydration 与 typed upsert 都必须强制使用该谓词；新增玩家 Hero 负向夹具，断言没有 Storage
Get/Set 且不会把 continuity 投影到 DSL。

### 门禁状态

没有第 4 轮审查授权。计划已吸收该最小修正，但未获得 `APPROVED` 终态；实现前必须等待用户
决定后续门径。

VERDICT: REVISE

## Round 2 — 2026-09-11

- 审查方式：第二位独立只读审查。
- 结论：`VERDICT: REVISE`。
- R1 关闭情况：Storage typed dispatch、namespace 扩展、玩家 SyncData authority 分离、hydration 可测性与 snapshot/LKG 均在计划层关闭。
- 新 P1：持久化 subject stable id 与 Persona 定义投影 `CharacterId` 仍被混用；对 generic NPC，
  `npc:<character>:a<agent>` 与 definition character id 不是同一身份。

### 下一动作

revision 3 必须显式拆分 `PersonaSubjectStableId` 与 `CharacterId`，并锁定本批次只持久化
英雄目标（generic NPC 保持 identity-only fallback）；hero fixture 必须证明两个 id 不同仍按 subject key
读取，generic fixture 必须证明不会读取/写入 Storage。

VERDICT: REVISE
