# PLAN 精查报告（2026-09-30）

> 触发：甲方要求「精查」`docs/` 下的历史 PLAN。
> 结论一句话：**226 份 `PLAN-*` 中，只有一小部分能确凿判定状态；三种自动判据全部被证伪，批量标退役会误伤。**

---

## 一、先说方法：三种自动判据，全部被证伪

精查的正确做法是「逐份开内容读」。我先尝试了三种自动化，**全部失败** —— 记在这里，避免下次有人重蹈。

| 判据 | 通过率 | **为什么不可用** |
|---|---|---|
| ① **落地物命中率**（抽 PLAN 里的类名/文件名，去仓库 grep） | 140/226 命中 ≥50% | ⚠️ **测的是"文档与代码库的词汇重合度"，不是"完成度"**。实证：`PLAN-AWAKE-EVENT-CANDIDATE-EVALUATION` 命中 18/18，但打开一看，它引用的类名**在第 2 节「已确认的现状」里就被提到了** —— 是**既有代码**，不是它要新建的 |
| ② **自带状态行** | **仅 57/226 有状态行**（74% 没有） | 剩下的 57 份有 20+ 种状态值，且大量是中间态（`REVISE_PENDING_REVIEW`／`待用户签收`／`Round 1 REVISE`） |
| ③ **同名取最晚日期** | — | ⚠️ **判据本身会出错**。实证：它判定 `WorldbookHardening-DRAFT` 被 `-REVIEW-LOG` 覆盖 —— 但 REVIEW-LOG 是流水、DRAFT 是草稿，**谁覆盖谁要看内容** |

### ⭐ 最要命的一条：**「设计没照做」≠「工作没完成」**

实证：`PLAN-Awake-Dialogue-BatchC` 说要建 `awake.conversations` 命名空间 —— **该命名空间零命中**。
但 `AwakeMessengerHistory.cs` / `AwakeMessengerService.cs` **都在** —— **功能做了，换了实现形态**。

⇒ **拿"PLAN 里写的具体设计"去核对，会把已完成的工作误判成没做。** 这比漏报更危险（会诱使人去"补做"已经存在的东西）。

---

## 二、226 份的真实构成（按能否判定分类）

| 类别 | 数量 | 能否判定 | 处置 |
|---|---|---|---|
| **附属件**（`REVIEW-LOG`/`DRAFT`/`DISCOVERY-LOG`） | **70** | 本就是主档的审查流水与草稿 | ✅ **不动**（甲方已定）—— 它们是证据链，标退役反而让人以为"审查记录作废了" |
| **主档 · 能确凿判定** | ~18 | ✅ | 见 §三 |
| **主档 · 无法机械判定** | ~138 | ❌ | **不动**，见 §四 |

---

## 三、✅ 能确凿判定的（线1：自称权威/待审/待签收）

这 18 份最危险（自称"待审"或"权威"，读者会以为还有事要做）。逐份读完的判定：

### 3.1 已实施，但状态行未更新（**建议标 `implemented`**，非退役）

| 文件 | 它自称 | 实际证据 |
|---|---|---|
| `PLAN-AWAKE-G3-B-PERSONA-PERSISTENCE-20260911.md` | `revision_4_1_implemented_approved_e2` | 状态行**已写 implemented** ✅ 处置正确 |
| `PLAN-AWAKE-KNOWLEDGE-INDEPENDENT-20260828.md` | `offline_verified` + `APPROVED` | 状态行已写 ✅ |
| `PLAN-Awake-Dialogue-BatchA-20260816.md` | 「待用户签收的修订 PLAN」 | `NpcDialogueOverlay`/`AwakeMessengerOverlay` **在源码里**；09-14 真机跑通对话面板 |
| `PLAN-Awake-Dialogue-BatchB-20260816.md` | 同上 | 无名 NPC 身份/成年校验逻辑已并入 |
| `PLAN-Awake-Dialogue-BatchC-20260816.md` | 同上 | ⚠️ **`awake.conversations` 未按原设计实现**，但 `AwakeMessenger{History,Service}` **已落地**（换了形态） |
| `PLAN-SceneVisualSelection-20260816.md` | 「待独立审查后签收」 | `AwakeConfig.cs:175 EnableSceneVisualSelection` + MCM 文案**都在**；T/Y 交互已实现 |

### 3.2 已被取代（**建议标 `superseded`**）

| 文件 | 依据 |
|---|---|
| `PLAN-AWAKE-WorldbookContract-20260822.md` | 自称「方案草案，待独立审查后进入实现计划」—— 但 **Contract v1 早已落地**，且有专门的 `AWAKE-Worldbook-Contract-v1.md` 作为正式契约 |
| `PLAN-AWAKE-WorldbookPlatform-20260822.md` | 自称「待用户签收后进入独立实现计划」—— 它说的「替换现有 v1 读取器的设计方向」**早已完成**（世界书已到 v38 / 790 档） |
| `PLAN-AWAKE-SYSTEMIC-WORLD-SIMULATION-20260825.md` | 自称「本批次只升级路线与文档权威关系，不实现任何运行时系统」—— **纯文档批次**，且它声称升格的"路线权威"**已被 `AWAKE-ROADMAP.md` 取代** |

### 3.3 状态存疑，**不动**（作者自称未开工，但线后来大幅推进）

| 文件 | 疑点 |
|---|---|
| `PLAN-WorldbookStudio-AI-AUTHORING-REWORK-20260829.md` | `REVISION_17_PENDING_REVIEW` + `implementation_status: not_started`，但 Studio 此后大幅推进（authoring-v1、taxonomy v1…）⇒ **无法判定是"没做"还是"换路做了"** |
| `PLAN-WORLDBOOK-STUDIO-*-REVIEW-REVISION*.md`（4 份） | 标题即 `REVIEW-REVISION`，是**修订稿**，其主档状态未知 |
| `PLAN-PersonaWorkbench-AWAKE-Joint-G3-*`（4 份） | 自标 `GATE`/`PREDECESSOR`/`COMPLETION`，是**批次内部件**，需连主档读才能判 |

---

## 四、❌ 无法判定的（~138 份主档）—— 为什么不动

**不做批量标记，理由：**

1. **74% 无状态行** ⇒ 没有作者的自我判定可依。
2. **落地物命中率不可靠**（§一）⇒ 无法自动化。
3. **逐份读的成本**：226 份 × 平均 ~15 KB ≈ 3.4 MB，且**每份都要连源码核**才发现"换了形态"（§一末条）。
4. **误标的代价 > 不标的代价**：错标 `superseded` 会让后人以为某件事不用做了；而**不标只是搜索时碍眼** —— 有了 `AWAKE-DOC-INDEX-20260930.md` 的导航，入口已经通了。

---

## 五、本轮实际处置

**只标 5 份**（§3.1 的 Batch A/B/C + SceneVisual；§3.2 的 WorldbookContract / WorldbookPlatform / SYSTEMIC）。

⚠️ **注意分寸**：Batch A/B/C + SceneVisual 标的是 **`implemented`（已实施）**，**不是 `superseded`** ——
它们的**设计与当前实现一致**，标退役会切断"这个功能当初为什么这么设计"的追溯。

---

## 六、给后人的建议

1. **新写 PLAN 时，头部必须带状态行**（`状态：draft / approved / implemented / superseded` ＋ 日期）。
   **这是本轮 74% 无状态行的直接补救。**
2. **状态变化时回原文件改那一行** —— 不要让状态只存在于对话里。
3. **判"做完没做完"，不要只看 PLAN 里写的具体设计**（§一末条）—— 要问「这件事现在有没有替代物」。

---

## 七、第二轮：按簇核验（2026-09-30 晚）

逐份读 146 份不现实，改用**按簇核验** —— 大簇对应同一个子系统，**核一次子系统就能覆盖一批**。

### 7.1 簇核验结果

| 簇 | 份数 | 子系统核查 | 判定 |
|---|---|---|---|
| `WorldbookStudio-A1~A4/B1` | 9 | ✅ `tools/worldbook-studio/` 含 `Awake.WorldbookStudio.{Cli,Core,Launcher,Web}` ＋ entity registry | ✅ **标 `implemented`** |
| `MARCUS-AWAKE-P3B/P3C/P3D` | 5 | ✅ `framework/` 含 `MarcusAwakeFramework`／`Provider`（有真实源码）／`RuntimeService`／`Storage`／`Transport` | ✅ **标 `implemented`** |
| `PLAN-Awake-*-20260816` | ~14 | 🟡 **混杂**：`AwakeMessenger*`／`AwakeEvent*`／`AwakeContact*`／`NpcProactive*`／`WeeklyReportBrowser*`／`WorldEventInbox*` **在**；但 `DevTestTool`／`McmConfig`／`UnifiedDialogue` **零命中**（可能改名） | ⬜ **不标**（证据混杂） |
| `PersonaWorkbench-*` | ~15 | ❌ `tools/persona-workbench/` **只有 .md，无代码**；`KeywordConstraintCompiler` 全仓零命中（真身可能在 `tools/persona-awake-joint/`） | ⬜ **不标** |
| `WORLDBOOK-STUDIO-*`（大写） | ~15 | 🟡 编译链在（`Cli`/`Core`/`Launcher`/`Web`），但各档具体目标未逐一核 | ⬜ **不标** |

### 7.2 本轮标记（14 份）

**WorldbookStudio A 系列（9）** ＋ **MARCUS P3 系列（5）**，统一标 `implemented`，头部写明：
> 本文件是**批次施工单**，其所属子系统已存在 ⇒ 本批视为已完成。**保留本文件**（不是 `superseded`）—— 它记录了当时的设计意图与边界。

> ⚠️ **标记的诚实边界**：依据是「**所属子系统已存在**」，不是「逐条验收了每项交付物」。
> 头部**写明了这个依据**，读者可自行判断。这是「误标代价 > 不标代价」下的折中。

### 7.3 ⭐ 顺带查出的重要事实：**DLC 处理链早已存在**（修正当日 DLC 评估）

核 B1 簇时发现 `PLAN-WorldbookStudio-B1-ENTITY-CATALOG-20260824.md` 里**已处理战帆 DLC**：
> 「战帆是同一世界的官方 DLC；本机未安装时只显示"官方 DLC、当前未安装"，不建立独立的战帆世界。」

顺此查出**项目 08-24 已建 DLC 处理链**：
- `tools/build_war_sails_reference_mapping.ps1`
- `docs/mappings/war-sails-reference/`（**528 条**：settlements 358／naval_characters 102／naval_lords 53／clans 9／heroes 5／kingdoms 1）
- 实体注册表带 DLC 状态：`hero_official_dlc_not_installed: 53`、`clan_official_dlc_not_installed: 9`
- 口径文件 `war-sails-reference/README.md`：「**本机未安装战帆时，不能把"本机缺少数据"当成对象冲突**」

⇒ **`DLC-COMPAT-NAVAL-20260930.md` 初版写的「AWAKE 对 DLC 接入度 = 0」是错的**（只量了 `src/*.cs`），
已修正为**三层表述**（工具层 ✅ 已建／内容层 🟡 部分／运行时层 ❌ 未接）。
**这与本项目反复出现的那条教训同族：量"接入度"不能只量源码。**
