# 角色卡线 4 项红收口（2026-09-19）

> 本文只写**已实测**的读数与**可复核**的出处。凡未经验证的一律标「推断」。
> 前置：`docs/REPORT-OFFLINE-GATE-FULLCHAIN-20260915.md` §1–§6 已把 6 项红逐条列过；
> 本文**取代**其 §2–§5 的结论（那 4 条的根因当时只到「疑似同源，待验证」），并按
> 项目约定另立新档而不改历史产物。

## 0. 一句话结论

`AWAKE.Tests` 那 4 项 persona 红**全部是判据／金标陈旧，不是运行时坏了**；根因汇聚到
同一次收口提交 `de2b9a0`（2026-09-15「收口(1/3)」），它把 09-10 至 09-15 之间**「已暂存、
未提交」的行为改动一次纳入历史**，却没有同步测试数据与金标样本。改判据一侧，不动运行时。

读数（`AWAKE.Tests/bin/Debug/net472/Awake.SdkSmoke.exe`，实测）：

| 时点 | RESULT | 失败项 |
|---|---|---|
| 修前 | `total=64 passed=58 failed=6` | g3-s0-focused-readiness、persona-template、shared-persona-golden-fixture、persona-persistence、persona-anchor、dialogue-chain-redtest |
| 修后 | `total=64 passed=62 failed=2` | g3-s0-focused-readiness、dialogue-chain-redtest（**均非本线**） |

`AWAKE/src/**` 在修前修后**均为工作区干净**（`git status --short -- AWAKE/src` 空）⇒
运行时代码一行未改。

## 1. 根因：`de2b9a0` 一次纳入了 5 处改变行为、未同步判据的改动

`git log -- <file>` 显示三处相关实现文件的**最新改动提交均为 `de2b9a0`**：

- `AWAKE/src/PersonaDslGenerator.cs`（上一次 `1120ef3` 09-10）
- `AWAKE/src/PersonaPersistenceModels.cs`（`git log -S'"hero:"' -- 该文件` 只命中 `de2b9a0`）
- `AWAKE/src/AiTaskConstants.cs`（上一次 `0630a6d` 09-11）

`de2b9a0` 提交信息自述：「索引里长期积压一批"已暂存、未提交"的改动……本笔为多条线累计
产出的暂存成果，按目录边界归档，**不表示由本线撰写**。」同笔自述的验证只到「可编译、
可进入执行，停在第 19 条」。

该笔对上述三文件的改动中，**5 处直接落在本轮 4 项红上**：

| # | 改动 | 落在哪条红 |
|---|---|---|
| 1 | `TryExpand(directTagIds, definition.Bundles, …)` → `TryExpand(directTagIds, null, …)`（方向丙：bundle 只作元数据） | `persona-template` |
| 2 | `optional.Add(BuildPublicSection(…))`：`[PERSONALITY_PUBLIC]` 由必选段第 5 位挪到可选段末尾 | `shared-persona-golden-fixture` |
| 3 | `BuildCanonicalConstraintTokens()` 新增 `CONSTRAINT_NO_INSTANT_SUBMISSION`、`CONSTRAINT_NO_MODERN_PSYCHOLOGY`（7 → 9 条） | `shared-persona-golden-fixture` |
| 4 | `PersonaStorageKey.TryBuild` 增 `hero:` 前缀守卫 | `persona-persistence` |
| 5 | `PersonaStateNamespace` 加入 `AiTaskConstants.StorageNamespaceIds`（同时删掉原「不进入默认打开列表」注释） | `persona-anchor` |

另有一处同笔改动未落在本轮红上，但同属行为变更：`maximumBytes` 默认值 `4096` →
`DefaultMaximumDslBytes`(6144)、fingerprint 增 `ContextModes`。

**第 1、3 项是有文档依据的**：`docs/PERSONA-A-TAG-VOCABULARY-REDESIGN-PROPOSAL-v3.md`
（标题含「已实施」，实施日 2026-09-12）§「新增」明写那两条令牌为新增，实施表 1.1 指名
`src/PersonaDslGenerator.cs` 的 `BuildCanonicalConstraintTokens`；方向丙在代码内亦有注释。
⇒ **是判据侧陈旧，不是实现侧回退。**

## 2. 逐项裁决与修法

### 2.1 `persona-template` — 判据数据陈旧

- 测试卡只给了 `"bundles":["bundle.test"]`，**没有 `tags`**；`PersonaDataLoader` 把
  `bundles` 映射到 `definition.Bundles`（`PersonaDataLoader.cs:142`），与 `Tags` 无关。
- 于是 `directTagIds` 为空 ⇒ `TryExpand` 的 `valid` 保持 `true`（`PersonaTagRegistry.cs:52`）
  ⇒ `expanded == true`，**不走 legacy fallback**，DSL 正常生成但**没有任何标签令牌**；
  判据缺的正是 `TRAIT_TEST` / `BOUNDARY_TEST`。
- 修法：卡上补 `"tags":[{"id":"trait.test"},{"id":"boundary.test"}]`，`bundles` 保留以标记
  「bundle 只作元数据」。
- 顺带修掉本条的**诊断粒度**（`REPORT-OFFLINE-GATE-FULLCHAIN-20260915.md` 第 2 条点名的
  「约 22 项 `||` 链，失败不告诉你是哪一项」）：改为逐项记录，异常消息直接列出未满足项，
  并打印真实 DSL。另收掉同处重复写了两遍的同一 `if`。

### 2.2 `shared-persona-golden-fixture` — 金标陈旧

- 判据是 `expectedDsl` 与生成结果**全串相等**（`Program.cs` 该用例）。
- 实测真实输出与金标有**两处**分歧，与 §1 的第 2、3 项一一对应：
  - 约束段少 2 条令牌；
  - `[PERSONALITY_PUBLIC]` 位置：金标在第 5 段（CORE 之后），真实输出在**末段**
    （CONTRADICTION 之后，38 行 / 共 42 行）。
- ⚠️ 既有诊断只提到「段序」一处；**只改段序，这条仍红**——两处必须同时改。
- 修法：按真实输出重写 `expectedDsl`（42 行 / 975 字符）。改动为**单行** diff。

### 2.3 `persona-persistence` — 测试数据陈旧

- `PersonaStorageKey.TryBuild`（`AWAKE/src/PersonaPersistenceModels.cs:184`）要求
  `personaSubjectStableId` 以 `hero:` 开头且其后非空；测试传的是 `"hero_test"`（下划线）
  ⇒ 守卫直接返回 `false`，`storageKey` 未赋值。
- 生产侧全部是 `"hero:" + hero.StringId`（`AwakeNpcTarget.cs:31/79`、
  `PersonaPersistenceService.cs:37`、`NpcDialogueDispatcher` 路径等）⇒ 守卫与生产同源，
  **是测试写法过时**。
- 修法：`CharacterId = "hero_test"` → `"hero:test"`；并**新增两条否定断言**，把守卫
  两个分支都变成可 FAIL 的判据（缺前缀、前缀后为空）。

### 2.4 `persona-anchor` — 判据方向反了（本次唯一一处「反转断言」）

- 原断言：`persona state namespace must not be in the default storage open list.`
- 文档里存在**两版互相冲突的裁定**，按时间取后者：

| 版本 | 出处 | 要求 |
|---|---|---|
| 09-10 | `docs/PLAN-AWAKE-PERSONA-PERSISTENCE-20260910.md:179` | 从默认打开列表**移除**；并明写「同批必须更新既有证据与期望：`docs/persona-awake-joint-g3-s0-*.json`、`T/Program.cs:3268-3269`」 |
| 09-11 | `docs/PLAN-AWAKE-G3-B-PERSONA-PERSISTENCE-20260911.md` D-4 | 「`PersonaStateNamespace` **纳入默认 required namespace 集合**，不能以"只打开 Persona namespace"替换既有 store」 |

- 09-11 那一版**已获批并已实施**：
  - 计划头部 `status: revision_4_1_implemented_approved_e2`、`user_signoff_required: true`；
  - `docs/evidence/AWAKE-G3-B-IMPLEMENTATION-E2-20260911.md`：「已加入默认 required namespace 集合」；
  - `docs/review-state/AWAKE-G3-B-PERSONA-PERSISTENCE-IMPLEMENTATION-20260911.review.json`：
    `verdict=APPROVED`、`findings: []`、独立评审人、`reviewed_at_utc=2026-09-11T13:12:28Z`，
    并以 `plan_sha256=3E8DCCB6…856F` 钉住含 D-4 的那份计划。
- 代码与 09-11 一致（`AiTaskConstants.cs:131` 在列表内）；运行期读口也**依赖**它在列表内：
  `WorldStateStore.cs:2017` 在命名空间未开时 `return null`（静默当「无存档人格」），
  而全仓**没有任何调用方**把 persona 命名空间当 `requiredNamespaces` 显式传入
  （`PersonaStateNamespace` 在 `AWAKE/src` 仅 4 处引用：常量声明、列表元素、读口、命令构造）
  ⇒ 「由调用方显式传入」这条退路**并不存在**。
- 修法：把断言**反转**为「必须在默认打开列表内」，并在注释里落 09-11 依据与
  「此为 09-10 旧设计遗留的陈旧不变量记录」。PASS 标记同步改名
  `persona.anchor.namespace_not_default` → `persona.anchor.namespace_default_required`
  （全仓 grep：该标记仅此一处引用，无外部校验器依赖）。

## 3. 变异检验（证明改后的判据仍能 FAIL）

只变绿不算验过。对**每一处新增/反转判据**都做了变异，逐条实测：

| 变异 | 结果 |
|---|---|
| 金标里 1 条令牌改成哨兵值 | 红：`divergences=1 \| line 17 fixture=[TOKEN=CONSTRAINT_MUTATED_SENTINEL] actual=[TOKEN=CONSTRAINT_NO_MODERN_PSYCHOLOGY]` |
| 摘掉 `TryBuild` 的 `hero:` 守卫 | 红：`persona storage key must reject a subject id without the hero: prefix` |
| 只摘掉守卫的「前缀后为空」分支 | 红：`persona storage key must reject an empty subject id after the hero: prefix` |
| 测试卡删掉刚补的 `tags` | 红：`failed=contains:TRAIT_TEST, contains:BOUNDARY_TEST`（逐项标签映射正确） |
| 从 `StorageNamespaceIds` 删掉 `PersonaStateNamespace` | 红：`persona state namespace must be in the default storage open list.` |

每次变异后均**按字节还原**并复核（`restored exact=True`、`git status --short -- AWAKE/src` 为空），
最终复核跑 `total=64 passed=62 failed=2`。

## 4. 未修 2 项及归属（均非本线，本文不动）

- `g3-s0-focused-readiness`：`AwakeRuntime.cs:872-880` 打开失败分支只回收候选、不动旧持有者。
  判据权威 `AWAKE/tools/persona-awake-joint/verify-g3-s0-focused-evidence.ps1:138,157-158`。
  `docs/persona-awake-joint-g3-s0-scope.v1.json` 的 `writeSet` 明列 `AwakeRuntime.cs` /
  `AwakeTestFakes.cs` / `Program.cs` ⇒ **cross-line 协作件，待裁决**（见 09-15 报告第 1 条）。
- `dialogue-chain-redtest`：判据要 `awake.worldbook.v2`（单包形态），部署件实为
  `awake.worldbook.registry.v1`（注册表形态）。09-15 报告已判为**判据对象错配**，
  修法是改判据而非改部署件；属世界书线。

## 5. 本文新增了什么（相对既有文档）

1. **补齐金标分歧的第二处**：既有诊断只说了「`PUBLIC` 段已后置」；实测还有**少 2 条约束令牌**，
   只改段序仍红。
2. **坐实 09-15 报告第 5 条的「疑似同源」**：`persona-anchor` 与 `persona-persistence`
   并非同源（前者是设计翻转、后者是测试数据陈旧），且前者可由 09-11 已批准的 G3-B 决策判死。
3. **给出 #1 `persona-template` 的确定性根因**（`bundles` 不注标签 ⇒ 无标签令牌、且不走兜底），
   并把那条 22 项 `||` 链改成逐项可读。
4. **把 4 项红归到同一次收口提交** `de2b9a0`，解释了「为何 09-15 就红、一直没修」。
