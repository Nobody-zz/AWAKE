# 暗面批（城镇地下秩序）· 档型清单与开工约束

> 立档：2026-09-20 · 承接 `ECONOMY-SYSTEM-SCOPE-20260920.md` §七「建议先做 ④ 暗面」
> 范围：**巷子 / 帮派 / 犯罪 / 走私 / 农奴 / 强盗 / 小阵营** —— 卡拉迪亚的「法外那一半」。
> 甲方已裁：数值可进正文但须挂游戏数据出处、行话表述不写公式；百科等官方本地化文案算可引文本。

---

## 〇、一句话口径

**这一批真正要做的不是「补一批词条」，是补两根空轴。** 实测：现役包（482 档）与当前编译包（558 档）
`era`/`lifecycle`/`status`/`certainty` **全为 0 次出现**（「何时知道」无载体）、`secret` 层表达**全库仅 2 条**、
**444/558 = 80% 的档只有 2 条表达**（「信不信」没料）。
> ⚠️ 更正：本节此前的「547 档」是**当时编译包的总数**，不是现役包（现役 482）。两处的空轴读数一致。
而钱与权的事天生多说法 ⇒ **同一件事，不同的人嘴里是两回事**。本批据此把「说法分层」做成可验收的形态。

---

## 一、档型清单（11 档）

| # | slug | 标题 | 域/子域 | 断言性质 |
|---|---|---|---|---|
| 1 | `underworld-alleys` | 巷子 | politics/law | fact |
| 2 | `underworld-gang-leaders` | 巷子头目 | politics/law | **interpretation**（官叫帮派头目／居民叫守护者） |
| 3 | `underworld-struggle` | 巷子争夺 | politics/law | fact |
| 4 | `underworld-gangs` | 帮派 | politics/law | fact |
| 5 | `underworld-crime-rating` | 犯罪等级 | politics/law | fact（100 / 30 两个数在官方正文里，非我们算的） |
| 6 | `underworld-blood-money` | 赎罪金 | politics/law | fact |
| 7 | `underworld-smuggling` | 走私货 | economy/trade | fact |
| 8 | `notables` | 要人 | culture/identity | fact |
| 9 | `serfs` | 农奴 | culture/identity | **interpretation**（★ 三层说法） |
| 10 | `underworld-bandits` | 强盗 | politics/law | **interpretation**（★ 三层说法） |
| 11 | `small-factions` | 小阵营 | politics/diplomacy | fact |

合计 **11 断言 / 24 表达**。

## 二、★ 分类表的缺口（本批暴露，未擅自改）

现存五个域（politics / economy / culture / war / geography）**没有「法外／地下」这一格**。
⇒ 本批 11 档只能挤：**politics/law ×7**、culture/identity ×2、economy/trade ×1、politics/diplomacy ×1。
⇒ **建议以后补** `politics/lawlessness` 或 `culture/underworld`。**本次不动分类表** —— 按项目记录，分类表一改，Studio 包就过期（`scripts/package.ps1` 把整个 plan 目录拷进包 `schemas/`），要另走一次定向刷新。

## 三、★「信不信」的落地形态（本批的主要产出）

现役 **435/547 = 80% 条目只有 2 条表达**，`secret` 全库 2 条 ⇒「同一个知识的不同说法」这个维度基本没展开。
本批的做法：**同一档，按受众给不同说法**。三条硬约束：

1. **受众必须互斥**。`SelectExpression` 每档只送一条，`分数 = 规则分×10 ＋ 层号`，同分取先
   ⇒ 同一身份被两层都挂上，**层号低的那条永远送不到人耳**。
   本批 `serfs` / `underworld-bandits` 用三层：`rumor`→(commoner, villager)、`summary`→(townsfolk)、`detail`→(notable, merchant, tavernkeeper, ransom_broker, headman, soldier, noble)。
   生成器里对「同时有 rumor 与 summary 的档」自动把 `townsfolk` 从 rumor 层摘掉。
2. **能力上限不许越**（一手 `WorldbookIdentityCapabilityRules.cs`）：
   `villager/commoner=(local,rumor)`｜`townsfolk=(regional,summary)`｜`headman/soldier=(national,detail)`｜
   `notable=(regional,detail)`｜`merchant/tavernkeeper/ransom_broker=(faction,detail)`｜`noble=(elite,detail)`。
   序：scope `local<regional<national<faction<elite<private`；detail `rumor<summary<detail<secret`。
3. **说法必须出自原文**，不许我们自己编排对立。`serfs` 的三条直接对应编年史 `rule_农奴` 的变体
   （`[1]` 农奴自述「我们过的都挺好的」／`[0]` 外人叹「生生世世都是贵族的奴隶」＋`[3]`「君君臣臣，都是有契约的」／`[2]` 体面人口中的「屁民、群氓、交税工具、后备兵源」）。

## 四、开工约束（四条）

1. **数值只写官方文本里有的**。`underworld-crime-rating` 的 100 与 30 出自官方正文（`FP4KyHBK`）；其余机制数值
   （巷子收益是「城越兴旺、进项越多」、赃物「半价」出自文案）一律**定性表述，不写成公式**。
   ⇒ 这不违反甲方裁定：裁定的落点是「数值可进正文但须挂得上游戏数据出处」，本批把它收紧成「**数值必须在官方文本里能指出来**」，更安全。
2. **B 级编年史只给「世界内的说法」**，不据以改官方名。`rule_农奴` 里的旧译不得回灌官方条目。
3. **别名不许放类别词、不许与本档 title 相等或互为子串**（09-20 K1 口径，生成器已加硬断言）。
   本批 11 档 aliases 全空 —— 因为这些词的官方中文名就是本档 title，加同义词只会造死条。
4. **不进成人向内容**。编年史有 `rule_阿塞莱阶级与奴隶制`（含后宫、女奴等露骨段落）⇒ **本批不用它**。

## 五、来源登记（本批新建两条）

| source_id | 性质 | locator 根 |
|---|---|---|
| `source.calradia.game.urban-dark` | game_snapshot | `game-urban-dark.txt`（本批用到的官方 CN 原文快照） |
| `source.calradia.chronicle.animusforge.dark` | chronicle | `chronicle-animusforge-dark.txt` |

## 六、不做 / 搁置

- **`profile.bandit` 这一处缺口不补**：能力表里 `bandit` 角色已预留（`= (local, rumor)`），但包里的身份清单
  （12 条）**没有 `awake:identity:bandit`** ⇒ 强盗拿不到知识，是「空手身份」。
  补它要动身份登记表 ⇒ **全身 547 档的 `registry_bindings.profile_registry_hash` 全部作废**，属专项，单独立项。**本批只记账。**
- **巷子在田野里的实际占有情况**（哪几座城归哪个帮派）未取 —— 若要写「某城现在归谁」，得现查。
- 分类表不加格（见 §二）。
- 不上线、不提交。
