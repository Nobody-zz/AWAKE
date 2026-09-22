# 军事批 · 作业规格（2026-09-20）

> 起因：甲方 `下一步把兵种和装备推完`。素材已取（`_mil_batch_dump_20260920.txt`，24 个题材词）。
> 本文件定三件事：**档型清单** / **撞车裁定** / **四条开工约束**。

## 一、档型清单（23 档待写 + 1 档样板已写）

### A 组 · 兵种与军团（10 档，其中具装骑兵已写）

| doc_id | subdomain | title | 说明 |
|---|---|---|---|
| `doc.war.troops-banner-knight` | troops | 方旗骑士 | 8 变体＋本体 1 条 |
| `doc.war.troops-khans-guard` | troops | 可汗亲卫 | 10 变体（含英/瓦/巴/阿/诺五族对手视角） |
| `doc.war.troops-ghulam` | troops | 古拉姆 | 10 变体＋本体 7 条；有 `KingdomIds:["ghilman"]` 自述 |
| `doc.war.troops-vaegir-guard` | troops | 维基亚卫队 | 15 变体；跨诺德/斯特吉亚/帝国三族 |
| `doc.war.troops-druzhinnik-cavalry` | troops | 斯特吉亚亲卫骑兵 | 8 变体＋本体 1 条 |
| `doc.war.troops-golden-boar` | troops | 黄金野猪兵团 | 3 变体＋本体 2 条；雇佣兵 |
| `doc.war.troops-legion-of-the-betrayed` | troops | 被弃者军团 | 7 变体＋本体 2 条；雇佣兵 |
| `doc.war.military-legion-old` | military | 旧式军团 | 11 变体；**原 PILOT 与「现代军团」并档，本轮拆开** |
| `doc.war.military-legion-modern` | military | 现代军团 | 14 变体；同上拆开 |
| `doc.war.war_history-kuyug` | war_history | 库由格 | 16 变体；**人物**，四族叙述完全对立 |

**拆档理由**：旧式／现代是两支不同的军队（一支废除、一支取代），各有 11／14 变体，合成一档会顶到篇幅上限、且「谁说的」会串。
PILOT 原写「帝国军团演变」并档，本轮改判。

### B 组 · 军制与军力（7 档）

| doc_id | subdomain | title |
|---|---|---|
| `doc.war.military-empire-system` | military | 帝国军事制度 |
| `doc.war.military-battania` | military | 巴旦尼亚军事制度 |
| `doc.war.military-empire-north` | military | 北帝国的军事实力 |
| `doc.war.military-empire-south` | military | 南帝国的军事实力 |
| `doc.war.military-empire-west` | military | 西帝国的军事实力 |
| `doc.war.military-nord` | military | 诺德军事力量 |
| `doc.war.military-aserai` | military | 阿塞莱军事力量 |

### C 组 · 弓弩与攻城器械（7 档）

| doc_id | subdomain | title |
|---|---|---|
| `doc.war.weapons-crossbow-ironbound` | weapons | 包铁弩 |
| `doc.war.weapons-ballista` | weapons | 弩砲 |
| `doc.war.weapons-ballista-fire` | weapons | 火焰弩砲 |
| `doc.war.weapons-mangonel` | weapons | 投石车 |
| `doc.war.weapons-mangonel-fire` | weapons | 火焰投石车 |
| `doc.war.weapons-siege-tower` | weapons | 攻城塔 |
| `doc.war.weapons-siege-ram` | weapons | 攻城锤 |

**子域一律复用已登记值**（`troops`/`military`/`weapons`/`war_history`），不动 taxonomy。

## 二、撞车裁定（写新批前）

检索面＝`Id + Title + Keywords`，子串匹配。逐词实测后，真正撞 title/alias 的只有两处：

1. **`military-vlandia` 的 alias「方旗骑士」** —— 撤。
2. **`military-khuzait` 的 alias「怯薛」「可汗卫士」** —— 撤（两条都给新档「可汗亲卫」）。

**判据**：这两条不是本档的别称，是本档正文里提到的**另一个事物**的名字。按 09-20 别名口径（别名只许放同一事物的别称），它们本来就该撤；
而「本尊档出现后，挂名让位」是同一口径的延伸 —— 与「B 类跨档挂名保留」不冲突：**B 类留的前提是它指向的那个东西没有自己的档**。

**实测的「不撞」清单**（只在正文/引文出现，不进检索面，无需处理）：
`troops-royal-guard` 正文提方旗骑士／`towns-lycaron` 正文提维基亚卫队／`throne-saneopa` 引文提攻城锤／
`weapons-crossbow` 正文提弩砲／`weapons-armor-shield-kite` 引文提亲卫骑兵／`weapons-armor-cape-shoulders` 正文提瓦兰吉／
`towns-varnovapol` 正文提阿契特。

**别名避让**（新档写法）：
- 斯特吉亚亲卫骑兵：**不写「瓦连格」「瓦良格」**（`military-sturgia` 已收），只写「亲卫骑兵」「斯特吉亚骑兵」。
- 维基亚卫队：可写「瓦兰吉卫队」（同一事物的别称，与「瓦良格」不是同一个词）。
- 包铁弩：**不写「弩」**（`weapons-crossbow` 的 title）。
- 帝国三兄弟：**不互写「帝国军力」**这种共用串。

## 三、四条开工约束

1. **别名**：只写别称，不得复写本档 title（09-20 口径）。
2. **B 级料讲机制必须先与一手游戏数据对表**（09-20 甲方当场纠偏；具装骑兵「无法举盾」那条已作废）。
   本批会碰到机制的：方旗骑士／可汗亲卫／亲卫骑兵的装备与骑枪能力。**凡涉及装备、能不能架枪、能不能骑射，一律先查 `spnpccharacters.xml`。**
3. **判层（`assertions[].kind`）**：`fact`＝来源平铺直叙；`rumor`＝来源自认无凭据／远方传闻；`interpretation`＝某方判断／名声／评价。
   每档至少覆盖两层，能到三层就到三层。
4. **一档只送一条表达**（`SelectExpression`）：同层内多条表达必须**受众互斥**（文化门／身份门），否则非首条送不到人耳。
