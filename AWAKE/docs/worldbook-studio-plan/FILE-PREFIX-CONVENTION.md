# 档名前缀：`underworld-` 的归入决定（2026-09-24）

> ⚠️ **本文件不是新规范。** 档名前缀的规范**早已存在**，见
> `TOPIC-WORLDBOOK.md` §「档名规范」（09-13 定、**09-14 21:5x 前缀全面复数化**）。
> 本文件先前误写「前缀此前从无成文规范」——**那句是错的**（我搜索范围只覆盖了本目录，
> 没搜记忆档就下了断言）。现改为**对该规范的一处补充**。
> 改名流程同理：照 `docs/worldbook-migration/corrections_20260914/CLASSIFICATION-V2-20260914.md` 走，
> 不另立一套。

---

## 一、既有规范说了什么（引用，不复述）

| 条款 | 出处 | 内容 |
|---|---|---|
| 形态 | 09-13 定 | 文件名＝id 尾＝`<类型前缀>-<词条名>` |
| 复数化 | 09-14 21:5x | 实体类加 `s`；抽象类（`military`／`throne`）保持单数；已复数者不动 |
| **范围＝B 案** | 09-14 用户裁定 | **文件名 ＋ `doc` id 首段**用新前缀；`assertion`／`expr` id 起始段**保留历史单数形态** |
| 现行前缀 | 09-14 | 21 个（13 复数 ＋ 8 不动），见 `TOPIC-WORLDBOOK.md` |
| ⚠️ 只改首段 | 09-14 | `castles-ab-comer-castle` 的词尾 `castle` 是地名的一部分，**不得跟着改**（67 档曾全中招） |

## 二、本次补充：`underworld-` 接进 politics 域

`politics` 域原有三个前缀：`throne`（王座/继承）· `territories`（领地掌故）· `clans`（氏族）。
本次按同一逻辑接入第四个：**`underworld-`**（城镇地下秩序）。

**归入 8 档**（原为 6 档裸名 ＋ 2 档 `alley-`／`town-` 混用）：

| 旧档名 | 新档名 | 条目 id |
|---|---|---|
| `town-alleys` | `underworld-alleys` | `politics.underworld-alleys` |
| `alley-gang-leaders` | `underworld-gang-leaders` | `politics.underworld-gang-leaders` |
| `alley-struggle` | `underworld-struggle` | `politics.underworld-struggle` |
| `town-gangs` | `underworld-gangs` | `politics.underworld-gangs` |
| `crime-rating` | `underworld-crime-rating` | `politics.underworld-crime-rating` |
| `blood-money` | `underworld-blood-money` | `politics.underworld-blood-money` |
| `bandits` | `underworld-bandits` | `politics.underworld-bandits` |
| `smuggling` | `underworld-smuggling` | `economy.underworld-smuggling` |

⚠️ `smuggling` 的**域段是 `economy`**（`domain: economy` / `subdomain: trade`），
只有 slug 段归 `underworld`。**前缀说「是什么货」，域段说「进哪个抽屉」**，两者不必对齐。

**不归入 3 档**：`notables`（要人——城市社会结构）· `serfs`（农奴——乡下的依附关系）·
`small-factions`（小阵营——跨域并集，含雇佣兵与宗教运动）。
判据：**「是不是只在城镇后街／法外地带成立」**。是则入，否则留。

## 三、本次暴露的一个真问题（给下次改名的人）

**`doc.<域>.<slug>` 的第三段就是条目 id `awake:entry:<域>.<slug>` 的 slug —— 同一个东西。**
⇒ 改 `doc` id 首段**必然**让条目 id 跟着变，**这是 B 案要求的形态，不是事故**。

我第一轮误以为「条目 id 已发布、不许动」，只改了文件名、留下 `doc` id 不动 ——
**那是我自造的规矩，与 B 案冲突**，结果是文件名与 id 分叉（违反「文件名＝id 尾」）。
第二轮已按 B 案补齐：文件名 ＋ `doc` id 首段同步换新，子 id 保留旧形态。

**两处必须同步改，否则会静默退回或分叉：**
1. 两个目录（WS `authoring/` ＋ 镜像 `authoring-out/`）
2. 🚨 **生成器 `_gen_dark_20260920.py` 的 `SLUG_MAP`** ——
   它同时管文件名与 `doc` id；不改，任何人重跑生成器都会把两目录退回旧名。
   （判别方法：把生成器复制到临时目录真跑，产出与现役档**逐字节比对**。
   这条判据在 `tools/_underworld_regen_equiv_20260924.py`，可复用。）

## 四、已知缺口（记账，未处置）

**分类表没有「法外／地下」这一格。** 这 8 档仍挤在 `politics/law`（7 档）与
`economy/trade`（1 档）。补 `politics/lawlessness` 或 `culture/underworld` 属专项——
分类表一改 Studio 包就过期（`scripts/package.ps1` 把整个 plan 目录拷进包 `schemas/`），
要另走一次定向刷新。**本次未动。**

另：`docs/worldbook-studio-plan/link-registry.v1.json` 与
`docs/mappings/worldbook-should-link/20260920/should-link.v3.json` 是**当时的一次性快照**
（1286 / 1394 条边），与当前 558 档不同步是既成事实，**不随档名走、也不手改**；
要更新得整条链重跑重签。
