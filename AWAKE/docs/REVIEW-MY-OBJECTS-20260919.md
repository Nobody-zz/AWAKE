---
title: AWAKE 我的四个任务对象 · 实测梳理
date: 2026-09-19
scope: 知识库格式 / 知识条目内容 / 知识识别回路 / 世界书编辑器
nature: 读数，不是裁决。凡结论都给了复现命令。
---

# AWAKE 我的四个任务对象 · 实测梳理（2026-09-19）

> **口径**：本档所有数字都是 **2026-09-19 22:0x–22:3x 当场实测**（重编后跑）。
> 凡引用旧档的地方都标了出处与日期；**不要拿旧数当现状**。
> 复现命令见文末 §6。

---

## 0. 一页话

**格式三层齐、回路五路全绿、编辑器能编能跑。**
真正卡住的不是机制，是三件事：

1. **内容的形状偏了** —— 82% 是聚落地名，三层知识实际上只用了两层，表达数普遍 2 条。
2. **上一批成果没接回回路** —— 09-18 判出的「978 条词条应当互引」至今零消费者，躺在 docs 里。
3. **回路只测了一根轴** —— 五路绿的全是「谁知道」；「何时知道」有管道没门禁、「信不信」连内容都没料（§3.4）。

---

## 1. 知识库格式

### 1.1 现存 schema 清单（实测）

| 层 | schema 串 | 出现在哪 | 数量 |
|---|---|---|---|
| **内容层**（作者写） | `awake.worldbook.authoring.v1` | `authoring-out/*.yaml` 每份一个 | 482 |
| **包层**（运行时读） | `awake.worldbook.registry.v1` | `ModuleData/Worldbook/manifest.json` | 1 |
| | `awake.worldbook.v2` | `packages/calradia/manifest.json`、`runtime.json` | 2 |
| | `awake.worldbook.index.v1` | `packages/calradia/index.json` | 1 |
| **编辑器层**（studio 自有契约） | `awake.worldbook.studio.authoring-wire.v1` | `contracts/authoring-wire-contract.v1.json` | 3 |
| | `awake.worldbook.studio.authoring-action-contract-catalog.v1` | 同上目录 | 2 |
| | `awake.worldbook.studio.action-route-registry.v1` | 同上目录 | 1 |
| | `awake.worldbook.studio.legacy-route-matrix.v1` | 同上目录 | 1 |
| | `awake.worldbook.state-predicate.v1` | 同 `action-route-registry` | 1 |
| | `awake.worldbook.studio` | 编辑器工作区产物 | 1 |

规范 schema 文件（带 JSON Schema 校验的那份）：
`docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json`。

### 1.2 现行包的实际形状（词条字段）

```
entry := { id, domain, title{zh-CN,en}, summary{zh-CN}, keywords[], expressions[], extensions{} }
expression := { id, text{zh-CN}, detail(rumor|detail|secret), grants[], denies[], extensions{} }
```

⚠️ **两个常被找错的字段都埋在 `extensions` 里**（不在顶层）：
- `extensions.subdomain` —— 子域
- `extensions.entityRefs` —— 游戏实体锚点
- 另有 `extensions.sourceAssertionId`（表达层）/ `extensions.sourceDocumentId`（词条层），是回溯到研究稿的线。

包顶层还有 `identities`（12 个）、`referrals`（4 条，可公开询问的人物）、
`indexes`（`domainToEntryIds` 5 组 / `keywordToEntryIds` **1795** 键）。

### 1.3 判断

- **没有单一真源**：三层各有各的版本号（内容层 authoring.v1、包层 v2/registry.v1/index.v1、
  编辑器层 studio.*）。这是**分层解耦的正常代价**，但要清楚：**改一层不等于改三层**。
- **1 yaml → 1 entry 的映射是干净的**（482 ↔ 482，无多对一、无丢件）—— 编译器这一环没漏。

---

## 2. 知识条目内容（现行包 482 条）

### 2.1 域分布

| domain | 条数 | 占比 |
|---|---|---|
| geography | 411 | 85% |
| war | 37 | 7.7% |
| economy | 21 | 4.4% |
| politics | 7 | 1.5% |
| culture | 6 | 1.2% |

子域前几名：**`geography.settlements` 396**、`war.weapons` 32、`economy.items` 15、
`geography.rivers` 7、`geography.terrain` 7、`economy.goods` 6、`politics.territories` 4。

### 2.2 空缺与覆盖

| 指标 | 读数 |
|---|---|
| 无 expressions | 0 |
| 无 keywords | 0 |
| 无中文 summary | 0 |
| **有游戏实体锚点**（`extensions.entityRefs`） | **402 / 482 = 83%** |
| 含 rumor 层 | 476 |
| 含 detail 层 | 473 |
| **含 secret 层** | **1** |
| expressions 只有 2 条的词条 | 415 / 482 = 86% |

### 2.3 判断（三条）

1. **包基本是一部「地名百科」**。82% 的条目是聚落。设计初衷是「**谁知道**×**何时知道**×**信不信**」，
   可现在的内容重心压在「哪儿有什么」。**机制长出来了，内容还是地图册。**
2. **三层知识实际上只用了两层**。secret 层全库只有 1 条 ⇒
   「个人观感按身份分层」这条设计**在内容侧没兑现**（机制那侧是好的，见 §3 身份闸）。
3. **表达数太薄**。86% 的条目只有 2 条表达 ⇒ 「同一个知识的不同说法」这个维度基本没展开，
   而它恰恰是「信不信」的载体。

---

## 3. 知识识别回路

### 3.1 五路实测（本次重编后跑，读数档 `tools/_gates_now_20260919.txt`）

| 闸 | 挂哪个工程 | 判定 | 关键读数 |
|---|---|---|---|
| RETRIEVAL_GATE | worldbook-runtime-smoke | **PASS** | A_hit3 **10/13**、B_hit3 **9/10**、ALL_hit3 **19/23**、observed_only 2 |
| MERGE_GATE | worldbook-rag-merge | **PASS** | literal 与 merged 两路都过线，losses@3 = 0 |
| IDENTITY_GATE | worldbook-runtime-sim | **PASS** | 被排除行 71（**泄漏 0**）；该知道行 205（真拿到 180）；**点名题失败 0** |
| IDENTITY_GATE[变异] | 同上 + `AWAKE_GATE_MUTATE_ASSUME_ALLOWED=1` | **FAIL（应然）** | 泄漏 **60**、点名题失败 **5** |
| PRODUCTION_SMOKE | worldbook-runtime-production-smoke | **PASS** | 31 项（`RESULT passed=31 failed=0`）运行时管道全绿 |

★ **变异检验这一路是本次最要紧的读数**：把「被排除身份的查询」冒充成够得着的身份，
身份闸立刻从「泄漏 0」翻成「泄漏 60」⇒ **这道闸确实在测，不是全绿空转。**

### 3.2 接线状态（grep 实测）

| 通道 | 接没接 | 证据 |
|---|---|---|
| **语义通道** | ✅ 已接 | `src/` 有 `AwakeSemanticArmBootstrap.cs`、`AwakeWorldKnowledgeSemanticIndex.cs`、`WorldKnowledgeSemantic.cs`，被 `WorldKnowledgeQueryService.cs` 用；rag-merge 闸与 runtime-sim 也消费它 |
| **词条互引（边表 v2 / 联系判断规范）** | ❌ **零消费者** | 在 `AWAKE/src` 与 `AWAKE/tools` 的 `.cs/.py/.ps1` 里 grep `should-link`/`LinkSpec`，**只命中 09-18 我自己写的构建与审计脚本** |

### 3.3 判断

**回路本身是绿的，但它是「两路合流」的绿（字面 + 语义）。第三路 —— 词条之间互相引用 —— 根本没接。**
09-18 那份「978 条应当互相引用、其中 382 条是互提」的成果，至今躺在 `docs/mappings/` 里当文件看。
**这是我这四个对象里最明确的一处「做完没接上」。**

### 3.4 按设计初衷的三根轴，把覆盖重算一遍（09-19 22:3x 补测）

设计初衷的判据是「**谁知道** × **何时知道** × **信不信**」。把五路闸按这三轴归类：

| 轴 | 门禁 | 证据（均为本次 grep／实读） |
|---|---|---|
| **谁知道** | ✅ **有，且做得最好** | 身份闸：阴性（`grants` 点不到 ⇒ 该 id 不得出现在 HitIds）＋点名题带**阳性对照**＋变异检验证明分辨力 |
| **何时知道** | ⚠️ **当日 22:4x 补了一道（只覆盖事实时点）** | 原判「有管道、无门禁」**已于当晚被补上**：新增 `time-gate`（事实 × 当下），① 未来不泄漏＋② 阳性对照＋③ 老料不回流＋④ 周窗形状，**变异检验判红（泄漏 0→3）** ⇒ 见 `docs/AUDIT-20260919-何时知道闸.md`。⚠️ **但它只管"动态事实的时点"**：条目侧的 `era`／`lifecycle`／`status=future` **编译时就被丢掉、进不了包**（三份产物 0 命中），仍是空的 |
| **信不信** | ❌ **只有"够不够格拿细档"，没有"信不信这个说法"** | 内容侧用 `detail(rumor \| detail \| secret)` 表达说法分层，运行时按身份解析出**能拿到哪一档**（`ProfileOfIdentity → (Scope, Detail)`）；代码侧 grep `Believ`／`Conviction`／`Credib` 在 `src/` 内 **0 命中** |

★ 注意后一条的证据边界：`TrustDelta`（±100）确实存在，但它是**人际关系**的信任度，与 `LoveDelta`／`HostilityDelta` 并列，
**不是"这个知识我信不信"**。别把这两个当成一回事。

⇒ **重估读数**：原"五路全绿"测的是**一根轴**（谁知道 ＋ 查得到）；第三根轴（信不信）**连内容都没料**（secret 层全库仅 1 条）。
「何时知道」当晚补了一道闸，**但闸只能测"已经进包的东西"** —— 而这一轴的内容（`era`／`lifecycle`／`status`）**根本进不了包**
（见 `docs/AUDIT-20260919-何时知道闸.md` §3.1）⇒ **这一轴的空处在内容侧，不在闸侧**。

---

## 4. 世界书编辑器（studio）

### 4.1 实测

| 项 | 读数 |
|---|---|
| `Awake.WorldbookStudio.slnx` 编译 | **0 错 0 警**，9.1 秒（本次实编） |
| 测试工程数 | 7（AuthorityGate / Batch / Draft / EditorContent / Launcher / Tests / Workstation） |
| CLI `doctor` | `ok: true`；sdk `10.0.301`；offline true；`v1_runtime_write: false`；ai true |
| CLI `compile` | **400** `WB-AUTHORITY-400: compile proof is required.`（authority gate 挡住，设计如此） |

### 4.2 两个坑（记下来，别再踩）

1. **CLI 必须从 `AWAKE/` 目录里跑**。从仓库根跑会 `WB-SCHEMA-404: 找不到 worldbook-studio 规范目录`——
   它按 `<cwd>/docs/worldbook-studio-plan` 找规范，而这个目录在 `AWAKE/` 下。
   也可用 `--schema-root` 显式指定。
2. **程序集名与工程目录同名**：CLI 的程序集叫 `worldbook-studio`（不是 `Awake.WorldbookStudio.Cli`），
   产物是 `worldbook-studio.exe`。按工程名找产物会找不到。

### 4.3 判断

**编辑器这一环能用**（能编、能诊断、能校验 schema），且「编译」这条路**故意带闸**。
⇒ 它不是瓶颈。**瓶颈在它要编的内容长什么样（§2），和编完的成果接不接回回路（§3.2）。**

---

## 5. 四个对象之间的关系：卡点在哪一环

```
知识库格式 ──(编译器)──▶ 知识条目内容 ──(检索/身份/语义)──▶ 知识识别回路
     ▲                                                              ▲
     └──────────────── 世界书编辑器（写内容、校验格式）──────────────┘
                          ▲
                          └── 09-18 的「互引边表」本该从这头接进去，现在没接
```

- **格式**：齐。没有单一真源是分层解耦的正常代价。
- **内容**：**偏**（82% 聚落、secret 层闲置、表达数薄）。**这是当前最实的一处缺口，且它在内容侧、不在机制侧。**
- **回路**：绿（五路）。缺第三路（互引）。
- **编辑器**：能用。编译带闸是设计。

**一句话结论**：我这四个对象里，**机制都长好了，缺的是「内容该长什么样」和「成果接回去」**。

---

## 6. 怎么复现本档的读数

```bash
# ① 知识库格式 + ② 内容（一次跑出两节读数）
python AWAKE/tools/_my_objects_readings_20260919.py

# ③ 识别回路：重编 + 跑五路闸（含身份闸变异检验），读数落 _gates_now_20260919.txt
python AWAKE/tools/_run_gates_20260919.py

# ④ 编辑器：编 + 诊断（注意 cwd 必须是 AWAKE/）
cd AWAKE && dotnet build tools/worldbook-studio/Awake.WorldbookStudio.slnx -c Debug
cd AWAKE && tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Debug/net10.0/worldbook-studio.exe doctor

# ③ 接线状态：互引边表的消费者
grep -rn 'should-link\|LinkSpec' AWAKE/src AWAKE/tools --include=*.cs --include=*.py --include=*.ps1
```

⚠️ **本次实测纠了两个我自己犯的字段错**：第一版脚本把 `subdomain` 与 `entityRefs`
当成词条顶层字段去读，读出一片 None/0 —— 它们其实埋在 `extensions` 里。
**读数为 0 和「字段不存在」是两件事，先 dump 一条真实记录再说。**
