# 角色卡契约断点 · 甲／丙 两案具体改法

日期：2026-09-13 夜 ｜ 线：角色卡 ｜ 性质：**决策备忘录**（只写方案与落点，未改任何代码）
前情：`STAGE-REPORT-8.md` §7 只留了"甲（改字段为复数）／丙（加名册 + 运行时选一张）"两个代号，未写具体改法。本文补上。
证据：`docs/AUDIT-COMPILE-VERIFY-latest.json`（本轮复跑 76/76）、以下全部 `文件:行` 均为本机实读。

---

## 0. 先纠正一个口径

之前说"**76 张装不进一个包**"。查完代码要改成更重的说法：**现在连一张都进不去，而且不报错。**

链路是「源卡 → 物化 definitions → 编译包 → 运行时」，中间有两处断点：

| 断点 | 位置 | 事实 |
|---|---|---|
| 一 · 没人写 | 写侧 `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs:76` | 写 `extensions` 时只有 `{contentTier, source}`，**没有 persona 节点**。全仓 `awake.persona.runtime-bundle.v1` 只出现在读侧 `src/WorldbookRuntime.cs:230` 和一个测试 fixture（`AWAKE.Tests/Program.cs:1613`）⇒ **无生产写入方**。 |
| 二 · 悄悄降级 | 读侧 `src/WorldbookRuntime.cs:229` | `persona == null` 时**直接 `return true`**（不抛错、bundle=null）⇒ `:152` 造出 `PersonaRuntimeProvider(null)` ⇒ `:295` 判 `_bundle == null` ⇒ 走 `PersonaDslGenerator.cs:40` 的 `BuildRuntimeFallback`（**只有 ID 与 NAME**）。 |

两个连带事实：

- **生成时不看是谁**：`WorldbookRuntime.cs:306` → `PersonaDslGenerator.cs:23` 直接用 `bundle.Definition`，**完全忽略 `snapshot.CharacterId`** ⇒ 谁来对话都拿同一张卡。
- **模拟器是旁路**：`tools/worldbook-runtime-sim/PersonaDialogueSim.cs:57` 用 `FindDefinition(dir, heroId)` **直读 `definitions/` 目录** ＋ `--force-approved`，绕开了"包 → bundle"这一层。⇒ 一直以来跑出的 76/76 是**模拟器口径，不等于真机装载通**。

另有一条**待与 codex 线确认**：`AWAKE/ModuleData/Worldbook/` 下只有 `persona_definitions/`，**无 `manifest.json`、无 `packages/`** ⇒ `LocateManifest()`（`WorldbookRuntime.cs:201`）返 null。是否由 `sync_module.ps1` 在部署时补上，本环境未跑该步，不作结论。

**⇒ 两案共同的第一步：先补 persona 的写入方。这一环空着，选甲选丙都跑不起来。**

---

## 1. 甲 · 契约改成复数

**思路**：让"一个包带全套卡"成为契约。`definition`（单对象）→ `definitions`（数组／字典）。

| # | 文件:行 | 改什么 |
|---|---|---|
| 1 | `src/PersonaModels.cs:365` | `PersonaDefinition Definition` → `IReadOnlyDictionary<string, PersonaDefinition> Definitions`（key = `characterId`）；`:368-377` `IsApproved` 里的 `Definition != null` 跟着改成 `Definitions.Count > 0` |
| 2 | `src/WorldbookRuntime.cs:235` | `persona["definition"] as JObject` → `persona["definitions"] as JArray`；循环调 `PersonaDataLoader.TryParseDefinition`（现有方法，已能逐条解析），按 `characterId` 建字典；`:261` 的 `Definition = definition` 跟着改 |
| 3 | `src/PersonaDslGenerator.cs:14-28` | 加一步"按 `snapshot.CharacterId` 取卡"，取不到走**现有** fallback（语义不变） |
| 4 | `src/PersonaPersistenceService.cs:57-60` | `PersonaSessionHydrationAdapter.HydrateAsync(..., RuntimeBundle bundle, ...)` 签名要跟着改 |
| 5 | `.../RuntimePackageCompiler.cs:76` | **新建写入方**：`extensions` 里加 persona 节点（`schemaVersion` / `definitions[]` / `registry`），把 `persona_definitions/` 逐条塞进包 |
| 6 | `docs/persona-contract/` | **新建 bundle schema**：`awake.persona.runtime-bundle.v1` 现在只有读侧、无 schema 文件 |

**改动面**：5 个源文件 ＋ 1 套 schema ＋ 1 段新写入方（第 5 项是新代码，不是改一行）。
**代价／风险**：旧包**不兼容**，既有产物须重生成；包体变大（76 张 × 2–8 KB ≈ 0.5 MB）要过完整性 hash（`WorldbookPackageIntegrity`）与 studio 编译门。
**全部落在模组主干（codex 地盘）。**

---

## 2. 丙 · 加名册，运行时挑一张

**思路**：契约不动。运行时另持一份名册，按 `CharacterId` 查表。

| # | 文件:行 | 改什么 |
|---|---|---|
| 1 | **新建** `src/PersonaRoster.cs` | 启动扫 `ModuleData/Worldbook/persona_definitions/definitions/*.json`（**现成 77 个文件**）→ `Dictionary<CharacterId, PersonaDefinition>`，只收 `status == approved` |
| 2 | `src/WorldbookRuntime.cs:108+` | `TryLoadAndPublish` 里把 roster 一并注入 |
| 3 | `src/WorldbookRuntime.cs:274` | `PersonaRuntimeProvider` 持有 roster |
| 4 | `src/WorldbookRuntime.cs:292` | `BuildProjection` 按 `snapshot.CharacterId` 查表，不命中走现有 fallback |
| 5 | `src/WorldbookService.cs:237-269` | **复用现成选择器**：`SelectPersonaDefinition` ＋ `:271+ PersonaMatchScore` 已实现"按 CharacterId 打分、同分报 `persona.definition_conflict`"，而且**全仓零调用方**（`BuildPersona` 没人调）⇒ 抽成公用即可，不用新写 |

**名册从哪来**（这一项要你挑）：

- **丙-a**：直扫 `definitions/` 目录 —— 零新增产物，但**绕开包的 hash 校验**（目录被人改了不会被发现）。
- **丙-b**：编译器写 `extensions.persona.roster` 进包 —— 产物仍走完整性校验，但**第 5 点写侧还是得补**。

**改动面**：1 个新类 ＋ 2 处改动 ＋ 1 段复用（相对甲约 1/4）。schema 不动、既有产物不动。
**已知坑（两案共有）**：`PersonaRuntimeProvider._cache`（`:278`）是按 fingerprint 缓存的，多卡多人会持续涨 ⇒ 需按 `characterId` 分桶或加 LRU。

---

## 3. 我的建议

**选丙，且不着急先做丙-b。**

理由：甲的收益是"契约干净"，但目前**没有场景支撑**——运行时一次对话只有一个人，不需要把 76 张卡全冻进包；而甲要动 schema，就得重生成全部既有产物 ＋ 动 studio 编译链，是四倍的活。
丙复用的是**已经写好、已经测过、只是没接线的选择器**，这是眼下最便宜的走法。

但这条线在模组主干上（`src/`），按分工该 codex 动手或至少你拍板——**我不擅自改**。

---

## 4. 拍板后必须复验的两件事

1. **预算 6144 是否仍是零丢失线**（契约变了，装载形态就变了）——跑 `budget_audit.py 6144,16000`。
2. **`AWAKE.Tests` 的 golden fixture 必须一并重生成**。日记里那 2 条跨线失败，早前推断是"fixture 未随预算／段序同步"；段序＋预算一落库，这个推断要么被证实、要么被排除。

---

## 5. 本轮顺带核实的其他挂账项

| 项 | 状态 |
|---|---|
| `tensionAxes` 编译必红 | **已闭环**。白名单三处齐（`PersonaAuthoringV2.cs:26`、`PersonaDocumentStorage.cs:40`、`Verify/Program.cs:40`），`PersonaCore.cs:51` 有 `TensionAxes` 属性（注释写明"编译期进 `preservedLegacyData`、物化时丢弃、不进运行时"）。**本轮复跑 `compile-verify.ps1`：`TOTAL_CARDS=76 / BUILD_PASS=76 / BUILD_FAIL=0`，`unknownRoot` 与 `loadError` 全空。** |
| 段序 ＋ 预算 6144 | **仍未提交**。`git diff HEAD` 显示 `src/PersonaDslGenerator.cs`（+30/−11）、`src/WorldbookRuntime.cs`（+4/−2）在工作区；`HEAD` 版本里**没有** `DefaultMaximumDslBytes`。与 `STAGE-REPORT-8.md` 的"待签收"一致。 |
| 蒙楚格（8640）／拉盖娅（6691） | 底座自身超预算，**重排救不了**。要这两张也零丢外壳，预算需 ≥ 8704——就是把 `DefaultMaximumDslBytes` 再抬一次（一个常量），或单独削这两张的 examples。**未做。** |
| examples 重新送达模型带来的照抄风险 | 预算放宽后 74/76 张的外壳重新进 prompt。批 1 静态判据 `risk=0 / R1=0 / R3=0`。**具体做法**＝下一批铺量时，对这 74 张里 `R1≥3` 的卡**按卡定制探针**（禁用通用探针，已证零区分度）。**未做。** |
| 11 张高危点测名单 | 伊拉、卡拉蒂尔德、弥娜、文得利娅、斯瓦娜、毛蕾阿斯、泽洛西卡、科林、金达、阿丝塔、阿尔瓦。**具体做法**＝每张从旧样本取"最可能被追问的场景"造 2–3 个问题，同卡多问若逐字相同（LCS > 60%）判不过。**未做。** |
| 全库 29 张带风险 | 静态扫描结果，**是指标不是实测**；批 1 已实测证明"含短引文 ≠ 会照抄"（old 2%）。铺量时随批处理。 |
