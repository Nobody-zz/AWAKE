# 角色卡运行时装载 · 丙案执行清单（2026-09-14）

> 日期：2026-09-14 ｜ 线：模组主干（**codex 地盘**）｜ 性质：**执行清单**（承接 `PERSONA-CONTRACT-BREAK-OPTIONS-20260913.md` §2「丙」）
> 本人（世界书侧）**未改任何 `src/` 代码**，只出清单。以下 `文件:行` 全部为本机实读（2026-09-14）。
> 目的：把 09-13 那份备忘录的「改什么」变成**可逐条执行、可逐条验收**的指令。

> ## ⚠ 本文档已被执行，且 §0/§1 的前提部分被推翻 —— 看 §5
>
> 2026-09-14 当天已按 **丙-a + 试行名单** 落地并验收（见 §5）。执行中实测出**本文档没预料到的东西**：
> **挡在卡与玩家之间的不是 1 道门，是 4 道**，丙-a 只解最后一道。§0 的「断点 0（`LocateManifest` 返 null）」
> 只在**仓库**成立，**游戏里 manifest 找得到**；§1.2 原写「只收 `status == approved`」在本机实测下**名册会为空**
> （76 张卡全是 draft）。§1.4「抽出选择器复用」**本轮未做**（理由见 §5.4）。先读 §5 再回看 §0/§1。

---

## 0. 现场复核：比 09-13 记的那条断点更靠前

| # | 断点 | 位置 | 事实（本机实读） |
|---|---|---|---|
| **0** | **根本不启动** | `src/WorldbookRuntime.cs:113 LocateManifest()` | `AWAKE/ModuleData/Worldbook/` 目录下**只有 `persona_definitions/`**，**无 `manifest.json`** ⇒ `LocateManifest()` 返 null ⇒ `:117 return false`。**persona 那条分支压根走不到。** |
| 1 | 没人写 | `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs:76` | 写 `extensions` 时只有 `{contentTier, source}`。实例佐证 `release/awake-worldbook-pilot/runtime.json` → `extensions = {"contentTier":"base","source":"awake.dialogue-chain-010.pilot"}`，**无 persona 节点**。全仓 `awake.persona.runtime-bundle.v1` 只出现在读侧 `src/WorldbookRuntime.cs:230` 与一个测试 fixture（`AWAKE.Tests/Program.cs:1613`）⇒ **无生产写入方**。 |
| 2 | 悄悄降级 | `src/WorldbookRuntime.cs:229` | `if (persona == null) return true;` —— 不抛错、不告警，`bundle` 保持 null ⇒ `:152` 造出 `PersonaRuntimeProvider(null)` ⇒ `:295 _bundle == null` ⇒ 走 `PersonaDslGenerator.cs:36-54 BuildRuntimeFallback`（**只有 `ID` 与 `NAME`**）。 |
| 3 | 不看是谁 | `src/PersonaDslGenerator.cs:22-28` | 直接用 `bundle.Definition`（**单数**），**完全忽略 `snapshot.CharacterId`** ⇒ 谁来对话都拿同一张卡。`RuntimeBundle`（`src/PersonaModels.cs:359-379`）字段就是 `Definition` 单数 + `Registry`。 |

**⇒ 结论比 09-13 更硬：当前 76 张卡在真机上一张都不会生效，而且没有任何报错。**

> **断点 0 的边界**：上面说的是**本工作区仓库里的** `AWAKE/ModuleData/Worldbook/`。`sync_module.ps1` 的 `$managedWorldbookFiles` 列了 `ModuleData\Worldbook\manifest.json`，但仓里没有该文件；脚本注释写着"试点包投放期间不把工程的 `ModuleData\Worldbook\**` 当受管文件，避免用后备版 v1 覆盖游戏内的 v2 试点包"⇒ **真机那份包在游戏目录、不在仓库**，它到底有没有 persona 节点，**从仓库无法判定**。所以断点 1/2/3 是"仓库与代码层面确定成立"，断点 0 需在**部署现场**验证。

### 对 09-13 备忘录的两处更正

- **`SelectPersonaDefinition` 不是"全仓零调用方"**：它在 `src/WorldbookService.cs:68` 被 `BuildPersona`（`:64`）调用；但 **`BuildPersona` 全仓无调用方**（只有定义，无一处调用）⇒ 整条**作者侧选择路径是死代码**，可安全抽出复用。备忘录的**结论**（"复用现成选择器、不用新写"）成立，只是理由要写成"死码可搬"而不是"零调用"。
- **断点 0 是新证**：09-13 只写了"待与 codex 线确认 `LocateManifest` 是否返 null"。本轮实证**返 null**。这一条是**最靠前、最致命**的：不解决它，丙-a/丙-b 都白做。

---

## 1. 丙案落点（逐条可执行）

### 1.1 新建 `src/PersonaRoster.cs`

```
internal sealed class PersonaRoster
{
    static PersonaRoster Load(IEnumerable<PersonaDefinition> definitions);
    PersonaDefinition Find(string characterId);   // Ordinal 精确查表；未命中返 null
}
```

- `Load`：只收 `StringComparer.Ordinal.Equals(d.Status, PersonaSchemaConstants.StatusApproved)`；按 `CharacterId` 建 `Dictionary<string, PersonaDefinition>(StringComparer.Ordinal)`；`CharacterId` 为空的跳过并记 warning。
- **roster 里不做打分**。打分归 §1.4 的选择器（职责分开，roster 只管"有哪些卡"）。

### 1.2 名册从哪来（**二选一，需拍板**）

- **丙-a**：直扫 `ModuleData/Worldbook/persona_definitions/definitions/*.json`（**现 77 个文件**）。零新增产物；代价是**绕开包的 hash 校验**（目录被人改了不会被发现）。
- **丙-b**：`RuntimePackageCompiler` 写 `extensions.persona.roster` 进包，产物仍过 `WorldbookPackageIntegrity`。代价是 §1.3 的写侧也得补。

**建议：先丙-a 打通链路，再迁丙-b。**（与 09-13 建议一致；丙-a 当天可验，丙-b 要动 studio 编译链 + 包 hash。）

### 1.3 读侧接线（`src/WorldbookRuntime.cs`）

| # | 行 | 改动 |
|---|---|---|
| 1 | `:111 TryLoadAndPublish` | 在 `:151` 附近构造 `PersonaRoster`（丙-a：扫目录；丙-b：从 `verified.Runtime["extensions"]["persona"]["roster"]` 解析） |
| 2 | `:152` | `new PersonaRuntimeProvider(personaBundle)` → `new PersonaRuntimeProvider(personaBundle, roster)` |
| 3 | `:274-283 PersonaRuntimeProvider` | 加 `private readonly PersonaRoster _roster`；构造加第二参；**保留单参重载**（传 null，供作者侧/测试） |
| 4 | `:292-310 BuildProjection` | 早退条件 `:295` 从 `_bundle == null` **放宽**为"roster 也没命中且 bundle 为 null"——有 roster 时不必强依赖 bundle |
| 5 | `:306` | 传"选出的 definition"给生成器（见 §1.5） |

### 1.4 选择器抽出复用（**不新写**）

- 把 `WorldbookService.SelectPersonaDefinition`（`src/WorldbookService.cs:236-265`）＋ `PersonaMatchScore`（`:267+`）抽到新的 `internal static class PersonaDefinitionSelector`。
- **语义原样保留**：`characterId` 精确匹配 score 1000；同分且同 `Priority` 多于 1 张 → 记 `persona.definition_conflict` 并返 null。**这个冲突护栏必须保留**（它是"两张卡抢一个人"的唯一拦网）。
- `WorldbookService:68` 改调静态版，行为不变。
- 优先级链（复用现有）：`characterId` 1000 → `identityId` → `role` → 其余按 `Priority` + `Id` Ordinal 兜底。

### 1.5 生成侧（`src/PersonaDslGenerator.cs`）

- `Generate(RuntimeBundle, ContextSnapshot, int)`（`:14`）：把 `:23` 的 `bundle.Definition` 换成"按 `snapshot.CharacterId` 选出的 `PersonaDefinition`"。
- 参数形态二选一（倾向后者）：新增 `PersonaDefinition definition` 参数，或新增 `PersonaRoster roster` 参数。**倾向前者**（生成器不该知道 roster 的存在，只管"这张卡"）。
- `BuildRuntimeFallback`（`:36-54`）**不动**：未命中时语义（仅 `ID`+`NAME`）保持不变，它仍是最后一道兜底。

### 1.6 缓存（两案共有坑）

- `PersonaRuntimeProvider._cache`（`:278`）按 `snapshot.ComputeFingerprint()` 缓存。
- 已核实 `ContextSnapshot.ComputeFingerprint()`（`src/PersonaModels.cs:248+`）**含 `characterId`** ⇒ **多人不会串卡**；但**条数会随对话人增长**。
- 加 LRU（上限如 256）或按 `characterId` 分桶。**不做也不会错，只是内存单调涨。**

---

## 2. 验收（缺一不可）

1. **断点 0**：部署目录须有 `ModuleData/Worldbook/manifest.json`（或确认 `sync_module.ps1` 部署时会补）。**先确认这条，否则 1–4 全部无从谈起。**
2. **断点 1**：重编译 pilot 后 `runtime.json.extensions.persona` 存在（丙-b），或 roster 非空（丙-a）。
3. **断点 2**：`PersonaRuntimeBundleLoader.TryLoad` **不再**走 `persona == null` 分支；`PersonaRuntimeProvider.Bundle` 非 null。
4. **断点 3（最关键的功能验收）**：同一 provider 传不同 `CharacterId`（如 `lord_5_1` 卡拉多格 vs 帝国某领主）→ **产出的 DSL 不同、`DefinitionId` 不同**；传未知 id → `IsRuntimeFallback == true`。**这一条不过，等于没修。**
5. **预算**：复跑 `budget_audit.py 6144,16000`，不得因名册化回升丢失数（当前基线 74/76 零丢外壳）。
6. **`AWAKE.Tests` golden fixture 一并重生成**（09-13 挂账的 2 条跨线失败，段序＋预算落库后应闭环）。
7. **真机**：`LocateManifest` 命中后 `AwakeLog` 应见 `worldbook_runtime_initialized`；且 `NpcDialogueService` 拿到的 `persona_dsl` 不再是 `[PERSONA_RUNTIME] RUNTIME_FALLBACK IDENTITY_ONLY`。**本工作区无游戏，此项只能由 codex 在真机跑。**

---

## 3. 与行尾雷的耦合（提醒）

丙-b 会往 `runtime.json` 增 `persona` 节点 ⇒ 改变 `contentHash` / `packageHash`（`WorldbookPackageIntegrity`）。
而 persona 的四个内嵌契约 sha 常量**被行尾钉死**（见 `docs/PERSONA-CONTRACT-FIX-20260913.md` §四遗留 + 本文档姊妹篇的状态块）。
**两件事一起动之前，先把行尾规范化定下来**，否则"包 hash 重算"与"契约常量失配"会互相打脸。

---

## 4. 不做什么

- **不改** `docs/persona-contract/` 既有 schema——丙的前提就是契约不动。
- **不动** `BuildRuntimeFallback` 的语义。
- **不 `git add -A`**（工作区多人共享脏树；只 `git commit -- <精确路径>`）。

---

# 5. 执行记录（2026-09-14 当天落地）

## 5.1 两个已拍板的决策

| 问题 | Max 的决定 |
|---|---|
| 76 张卡全是 `draft`，运行时硬卡 `approved` ⇒ 丙-a 修完也一张不生效。**审批门怎么开？** | **试行名单制**：运行时仍只认 `approved`，另加一个名单文件；名单里点名的草稿卡才放行。可控、可撤、不造假章。 |
| 这一轮做到哪一步？ | **只修选卡**：改运行时装载/选卡与生成入口；**不碰同步脚本、不动游戏目录**。 |

## 5.2 执行中实测出的「四道门」（本文档原本只预料到 1 道）

| # | 门 | 现状（本机实测） | 证据 |
|---|---|---|---|
| 1 | **审批 status** | **76 / 76 全是 `draft`**。运行时硬卡 `approved`。 | 真源 `tools/persona-workbench/characters/*.persona.json` 76 张全 `draft`；`tools/materialize-definitions.ps1:106` 是 `status = $card.status` **原样复制** ⇒ **不是物化写坏的，是源头带来的，从没人点过工作台里的「批准」** |
| 2 | **送进游戏** | 仓库 77 个 definition，**游戏目录只有 1 个**（`hero_default.json`） | `sync_module.ps1:53` 把 `persona_definitions\definitions` 列为受管目录，但 `-SkipWorldbook`（试点包投放期）当前禁用它；且仓库无 `ModuleData\Worldbook\manifest.json` ⇒ `Assert-SourceManifest:456` 会直接 throw |
| 3 | **标签表** | 卡引用 **39 tags / 4 bundles**；游戏侧 `tag_registry.json` 只有 **8 tags / 1 bundle** ⇒ `TryExpand` 失败照样退兜底 | 两侧实测对比（见 §5.5 对照 B） |
| 4 | **选卡逻辑（本轮修的）** | 运行时只认包内**一张**单数 `definition`；真实游戏包里 `extensions.persona` 缺节点 ⇒ `bundle == null` ⇒ **每个 NPC 都走 IDENTITY_ONLY 兜底** | `PersonaRuntimeBundleLoader.TryLoad`（旧 `return true` 静默）、`RuntimeBundle.Definition` 单数、`PersonaDslGenerator` 忽略 `CharacterId` |

> **更正 §0 断点 0**：`LocateManifest()` 返 null 是**仓库**现象（仓库无 `manifest.json`）。**游戏目录有** `Modules/AWAKE/ModuleData/Worldbook/manifest.json`（`awake.worldbook.v2`，试点包）⇒ 真机上 manifest 找得到，真正的失败点是门 4 的 `extensions.persona` 缺节点。

## 5.3 本轮实现落点

| 文件 | 落点 | 内容 |
|---|---|---|
| **新建** `src/PersonaRoster.cs` | `:29` 类 · `:87 Create` · `:98 TrySelect` · `:131 Load` · `:238 TryAdmit` · `:268 ReadAllowlist` · `:336 ComputeDigest` | 扫目录建名册；**复用 `PersonaDataLoader.Load`**（把 manifest 的 persona 路径投影成最小 `WorldbookDocument` 再喂进去，**不写第二套扫描**）；状态门＝approved 全收／draft 需在名单／disabled 一律拒 |
| `src/WorldbookRuntime.cs` | `:154` 构造名册 · `:155` 注入 · `:168-175` 日志 · `:253` 缺节点不再静默 · `:316` 双参构造 · `:325 Roster` · `:332-381 BuildProjection` | 选卡三级：**名册 → 包内单卡 → 运行时兜底**；缓存改 512 条 LRU（`LinkedList` 记序，`:303`） |
| `src/PersonaDslGenerator.cs` | `:14` 旧重载委派 · `:31` **新增** `(definition, registry, ContextSnapshot, int)` | 新重载**不再看 `Status`**——状态门由名册统一把关，生成器只管「这张卡」；6 参重载与 `BuildRuntimeFallback` **未动** |
| **新建** `ModuleData/Worldbook/persona_definitions/pilot-allowlist.json` | — | schema `awake.persona.pilot-allowlist.v1`；条目按 `definitionId` 或 `characterId` 任一匹配；本轮放 5 张（选取依据与理由写在文件里） |
| `AWAKE.Tests/AWAKE.Tests.csproj` + `Program.cs` | csproj 增 `PersonaRoster.cs`；`Program.cs` 增 `RunPersonaRosterSmoke` | 名册回归用例（9 组断言，含「放行不改源状态」「撤名单即撤销放行」） |
| `tools/worldbook-runtime-sim/` | csproj 增 `WorldbookModels.cs` + `PersonaDataLoader.cs` + `PersonaRoster.cs`；`SmokeStubs.cs` 删 2 个重名替身；新增 `PersonaRosterSim.cs`；`Program.cs` 挂 `persona-roster` 子命令 | 离线探针，跑**同一份** `src/PersonaRoster.cs` |

**放行的 5 张**（依据：`docs/PERSONA-SPOTCHECK-11-20260914.md` 的 11 张点测通过卡里，两两 LCS 覆盖最低的前 5）：

| characterId | 卡 | 点测 LCS |
|---|---|---|
| `lord_2_12` | 斯瓦娜 | 20.8% |
| `lord_4_6` | 卡拉蒂尔德 | 21.2% |
| `lord_1_47_1` | 弥娜 | 21.2% |
| `lord_2_2` | 阿丝塔 | 21.4% |
| `lord_3_2` | 金达 | 23.8% |

## 5.4 与原文的偏离（都要理由）

| 原清单 | 本轮做法 | 理由 |
|---|---|---|
| §1.1「只收 `status == approved`」 | 收 approved ＋ **名单内 draft** | 实测 76/76 全 draft ⇒ 原写法名册恒为空 |
| §1.1「`CharacterId` 为空的跳过并记 warning」 | 空 `CharacterId` 的卡进**兜底池**（scope=fallback/role/global），不记 warning | `hero_default`（scope=role）本就是"任意英雄"的兜底卡；按原文会把它丢掉、导致每个无卡 NPC 掉进 IDENTITY_ONLY（实测：有兜底池时 850B，没有时 80B） |
| §1.4「抽 `PersonaDefinitionSelector` 复用」 | **未做** | 该选择器打分输入是 `WorldbookQuery`（identity/role/scope 打分），而名册路径的键是 `characterId` **精确匹配**，两者不同构。硬搬会为了复用而复用，且要动共享文件 `WorldbookService.cs`。**`persona.definition_conflict` 护栏改由 `persona.character_duplicate` 在名册侧承担**（同一 `characterId` 两张卡 → 忽略后者并记 warning） |
| §1.6「加 LRU（上限如 256）」 | 已加，上限 512 | — |
| §2-7「真机验证」 | **未做，本轮不可能做** | 只修选卡、不部署 ⇒ 游戏目录里仍是那 1 张卡。且门 2/3 未解决 |

## 5.5 验收结果（装置：`tools/worldbook-runtime-sim`，跑真实 `src/PersonaRoster.cs`）

原始输出：`docs/evidence/persona-roster-20260914/roster-probe.json`（＋ `dsl/` 落盘的 DSL 全文）。

**A）基准（仓库目录，名单在，注册表 39 tags）**

```
characters=5  fallbacks=1  registry_tags=39  allowlist=10  drafts_admitted=5
lord_2_12 -> exact  source=draft runtime=approved  dsl=5031B fallback=False
lord_4_6  -> exact  source=draft runtime=approved  dsl=4921B fallback=False
lord_1_47_1->exact  source=draft runtime=approved  dsl=5021B fallback=False
lord_2_2  -> exact  source=draft runtime=approved  dsl=5280B fallback=False
lord_3_2  -> exact  source=draft runtime=approved  dsl=4648B fallback=False
lord_1_5  -> fallback def=hero_default            dsl=850B     ← 未列名单的草稿卡，拿不到自己的卡
lord_5_1  -> fallback def=hero_default            dsl=850B     ← 同上
```

⇒ **门 4 打通**：精确命中、源状态保持 `draft`、运行时状态 `approved`、产出非兜底 DSL。
⇒ **门 1 的名单有效**：未列名单的草稿卡拿不到本人卡（只剩兜底卡）。

**B）对照 A——把名单文件拿掉**：`characters=0`、5 张全退兜底池、DSL 由 4648–5280B **掉到 850–856B**。
**对照 B——把注册表换成游戏里那份 8 tags**：选卡仍 `exact`（名册不受影响），但 DSL **80B / `RUNTIME_FALLBACK IDENTITY_ONLY`**，警告 `persona.tag_unregistered:*`。
⇒ **门 3 是硬的**：卡选到了、标签表不搬过去，一样白搭。

**C）`AWAKE.Tests`**：`60 例 / 2 失败`（改动前为 `59 例 / 2 失败`）。新增 `RunPersonaRosterSmoke` **通过**；那 2 条是 09-13 就挂账的跨线红（`docs/AUDIT-SDKSMOKE-FAULT-TOLERANT-20260913.md:130-131` 已判定「必然失败、与代码线改动无关」）。
**主构建**：`tools/build.ps1` → `BUILD_OK api=1.3.15` ＋ `TESTS_OK`。

## 5.6 本轮**没有**解决（下一步）

1. **门 2（部署）**：把 77 个 definition 与完整注册表送进游戏目录。牵扯 `sync_module.ps1` 的 `-SkipWorldbook`、仓库缺 `manifest.json`（`Assert-SourceManifest:456` 会 throw）、以及与游戏内 v2 试点包谁覆盖谁。**下一轮的第一个动作。**
2. **门 3（注册表）**：与门 2 同批解决（同一目录、同一次同步）。
3. **门 1 收口**：试行名单现放 5 张，逐步扩到 76 张的过程＝角色卡验收线（13 道门禁／红队）的实际节奏。
4. **`AWAKE.Tests` 2 条跨线红**（golden fixture 与 template 断言）：属角色卡线，fixture 需随段序/预算重生成。**修绿之后 `AWAKE.Tests` 的运行期断言才是活的**（`docs/AUDIT-REDTEST-GATES-20260913.md:126`）。
5. **真机验证**（须有游戏）：`AwakeLog` 应见 `worldbook_runtime_persona_roster ...`；`persona_dsl` 不再是 `IDENTITY_ONLY`。
6. 迁 **丙-b**（把名册写进包 `extensions.persona`，过 `WorldbookPackageIntegrity`）——须先解决 §3 的行尾耦合（**该耦合已在 `a75fd58` 修掉：契约摘要改为行尾无关**）。

---

# 6. 丙-a2 · 摘耦合：角色卡不再依赖世界书（2026-09-14）

## 6.1 为什么改

Max 一问「你要 worldbook 干什么」逼出的定位错误。查实后的结论分两层：

- **语义上，角色卡不需要世界书。** 角色卡生成要的只是「一个卡目录 ＋ 一份标签表」。世界书那套（包、hash 校验、词条、身份、转介）是另一件事（NPC 记得什么），不是「NPC 是谁」。src 里**不存在**世界书之外的 persona 通路——v2 的 `persona_definitions/`、v1 的 `personality_background`／`unnamed_persona`／`voice_mapping`（`WorldbookModels.cs:86-90`）全在 `ModuleData/Worldbook/` 下。
- **但装载被世界书链挟持。** `WorldbookRuntime.TryLoadAndPublish()` 找不到 manifest 就 `return false`（`:113-118`），**整个运行时（含角色卡）不装载**；而 **丙-a 又把名册的根目录取成了 manifest 所在目录**（旧 `:154`）⇒ 「要一份 manifest」这件事，一半是既有耦合，**一半是丙-a 自己加的**。

⇒ 于是原 §5.6-1 的定位**作废**：那等于**替世界书线做他们的包**（`ModuleData/Worldbook/manifest.json` 属世界书线，非本线），顺着耦合走＝把别人的问题接成自己的门槛。

## 6.2 改了什么（本线自己那段接线）

| 文件 | 改动 |
|---|---|
| `src/PersonaRootLocator.cs`（**新建**，63 行） | 从给定起点逐级向上找含 `persona_definitions/` 的目录。**刻意不读 manifest、不引用 TaleWorlds / AwakeLog** ⇒ ① 装载不依赖世界书；② 测试工程能编同一份源码。候选形状＝`ModuleData/Worldbook` 与 `Worldbook`（沿用既有约定；将来搬自有目录只需追加形状，调用点不动） |
| `src/WorldbookRuntime.cs` | `EnsureCreated()` 首句改调 `EnsurePersonaCreated()`；新增 `EnsurePersonaCreated()`（只依赖磁盘上有一个含 `persona_definitions/` 的目录）；`LocatePersonaRoot()` 薄封装 `PersonaRootLocator`；`TryLoadAndPublish()` 不再自己建名册（改为 `EnsurePersonaCreated()` + 把包内 bundle 接到同一个 provider 上）；新增日志 `worldbook_runtime_persona_bundle` |
| `src/PersonaRoster.cs` | 仅注释：根目录「由调用方给定、刻意不从 manifest 推导」；manifest 只影响两个相对路径的覆盖，缺失走默认值 |
| `AWAKE.Tests/AWAKE.Tests.csproj` | 补 `PersonaRootLocator.cs` 的 `<Compile Include Link>` |
| `AWAKE.Tests/Program.cs` | 新增 `RunPersonaRootLocatorSmoke`（65 行） |

## 6.3 验收（同装置对比）

- **新增用例 `RunPersonaRootLocatorSmoke` 通过**。它直接断言三件事：
  1. **fixture 里刻意不写 `manifest.json`**，定位仍能解出 `ModuleData/Worldbook`，且该根**能建出可用名册并精确选中卡**（＝「没有 manifest 也能装载」）；
  2. 另一种形状 `Worldbook/persona_definitions` 同样可解析；
  3. 反向：`persona_definitions` 挂在约定形状之外**不算**角色卡根；起点为空/空白返回 null。
- **`AWAKE.Tests`：`60 例 / 2 失败` → `61 例 / 2 失败`**（改前基线见 §5.5-C）。那 2 条仍是 09-13 挂账跨线红，**与本次改动无关**。
- **主构建**：`tools/build.ps1` → `BUILD_OK api=1.3.15` ＋ `TESTS_OK`，0 警告 0 错误。

## 6.4 本轮**没有**解决（必须说清）

1. **只解了「装载」，没解「投送」。** 角色卡现在**不需要** manifest 就能被装载；但把 77 个 definition 送进游戏目录这一步，仍然只有 `sync_module.ps1` 的世界书受管块在做，而那一块被 `-SkipWorldbook` 关着、且开着就要 `manifest.json`。**⇒ 游戏里仍然看不到 76 张卡。**
2. **真机未验**（须有游戏）：`EnsureCreated` 的调用顺序、`worldbook_runtime_persona_roster` / `worldbook_runtime_persona_bundle` 两条日志、以及「世界书加载失败时角色卡照常出卡」这个断言，都还只在离线层面成立。
3. **地点仍在世界书目录下。** `ModuleData/Worldbook/persona_definitions/` 是**世界书线声明的接口**（`sync_module.ps1:458` 把该路径钉为 canonical），所以**没有擅自搬家**。要真正给角色卡自有目录，属跨线改动（要改世界书侧的 manifest 断言与受管清单），需两线商定；`PersonaRootLocator.CandidateShapes` 已为那一天留好追加点。
4. **`WorldbookRuntime.cs` 是主干侧共享文件**：本次改动虽属本线接线，提交前仍应与主干线（workbuddy 会话）打个招呼。

