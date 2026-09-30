# tag 层筛查报告（第一轮）

> 生成：2026-10-01 ｜ 数据源＝正典 `AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring/`（**800 档**）
> 判据脚本：`tools/_tag_census.py`（可重跑）
> 上位件：`WORLDBOOK-NAME-INDEX.xlsx`（名录）｜`knowledge-taxonomy.v1.json`（分类目录）

---

## 〇、这一层是什么（先对齐口径）

按用户 2026-10-01 裁定：**tag ＝ 子域（subdomain）事实上的下属分类，但不注册、不进 schema、不进包**，
由档名（`document_id` 第三段）**自动派生**。

```
doc.<domain>.<tag>-<slug>
     ^^^^^^^^^  ^^^
      5 域封闭    tag：子域的下属分类（本报告的对象）
```

**关键结论：tag 不是分类层，是「同一子域下的自然聚类」。** 所以判据不是"划得对不对"，
而是**「同一子域内，这些 tag 是否真的把条目分成了有意义的几堆」**。

---

## 一、实况总览

**800 档 / 34 个 tag。**

| 规模 | tag | 所在子域 |
|---|---|---|
| 273 | `villages` | geography/settlements |
| 101 | `items` | economy/items |
| 74 | `clan` | politics/clans |
| 67 | `castles` | geography/settlements |
| 53 | `towns` | geography/settlements |
| 39 | `goods` | economy/goods |
| 39 | `weapons` | war/weapons |
| 28 | `concept` | **散落 16 个子域** ⚠️ |
| 27 | `hero` | politics/clans(19) + politics/kingdoms(8) |
| 15 | `polity` | politics/polity |
| 12 | `military` | war/military |
| 10 | `troops` | war/troops |
| 8 | `underworld` | politics/law |
| … | （另有 22 个 tag ≤ 8 档） | |

**分布判断：**
- **前 7 个 tag 覆盖 646/800 = 81%**，全部集中在 4 个子域（settlements / items / clans / goods / weapons）。tag 层在这几处是**成熟**的。
- **`concept`(28) + `tales`(5) = 33 档是"万能筐"**，占了非前 7 名的大头。
- **15 个 tag 只出现 1 次**，是"一次性命名"，不成体系。

---

## 二、三个待处置问题（按优先级）

### P1 ⚠️ `concept` 是最大的万能筐（28 档，散落 16 个子域）

`doc.<domain>.concept-*` 出现在：

```
culture/identity(7)  culture/customs(1)  culture/marriage(1)
economy/trade(1)     economy/currency(1) economy/goods(1)
geography/settlements(1)
politics/diplomacy(2) politics/offices(2) politics/law(2)
politics/clans(1)    politics/kingdoms(1) politics/succession(1)
war/military(1)      war/logistics(1)     war/troops(4)
```

**问题**：`concept-` 不是"一类东西"，而是**"我这条是讲一个抽象概念的"**——这是**体裁**，不是**下属分类**。
它和 `tales-` 一样是**万能 tag**，只是没人把它当回事。

**但要先问：这是病吗？**
- 28/800 = 3.5%，比重不大。
- 它**确实**标注了一个共通的体裁特征（"抽象概念条目"）。
- 拆开的好处是"在子域内能分堆"，坏处是"丢失了'这是一条概念条目'这个跨域共性"。

**建议（待用户拍板）：**
- **方案 A（保留为体裁标记）**：承认 `concept-` 与 `tales-` 是**跨域体裁层**（第二类 tag），
  不进"下属分类"体系。**代价 0**，但要写进口径，以后不许再往子域分类里算它。
- **方案 B（就地拆开）**：按**所在子域**改用该子域的候选 tag（见 §三），
  如 `war/concept-troop-archer` → `war/troops-archer`。**代价**：28 档要改 id（**发布事故风险**，需慎重）。
- **方案 C（暂缓）**：只在名录里标出，不动 id。下一批新增时不再用 `concept-`。

> ⚠️ **改 `document_id` ＝ 发布事故**（见 MEMORY）。所以方案 B 只能**在下一批新增时用新命名**，
> 已上线档**不改**——除非用户明确要重发。

---

### P2 ⚠️ `tales` 是第二个体裁筐（5 档）

```
culture/faith(2)     doc.culture.tales-charas-origins / tales-dawn-taboo
culture/customs(2)   doc.culture.tales-lakonis-red-water / tales-lycaron-vultures
culture/identity(1)  doc.culture.tales-kachar-rulership
```

**与 `concept-` 同性质**：`tales-` 是**"这是一则故事/传说"**的体裁标记，不是下属分类。
**并入 P1 一起裁定。**（建议同为方案 A：承认体裁层，不再当分类。）

---

### P3 ℹ️ 「错位」复核：**4 处里 3 处不是错位**

我上一轮的判断**过于武断**。逐条复核如下：

| 子域 | 实际用 tag | 上一轮判断 | 复核结论 |
|---|---|---|---|
| `politics/kingdoms` | `hero-`(8) | ❌ 错位 | ✅ **不是错位**：装的是**王国的统治者个人**（`hero-lord_1_1`…），子域名是"王国"、内容是"王国的领主"，tag `hero` 准确。**真正的问题是子域名太粗**（应叫 `kingdoms` 还是 `rulers`？——**这是分类层的事，不是 tag 层**）。 |
| `politics/law` | `underworld-`(7) | ❌ 错位 | ✅ **不是错位**：装的是**地下秩序**（`underworld-alleys`/`bandits`/`blood-money`/`crime-rating`/`gangs`…），子域 `law` 是"法"，内容是"法的对立面/边缘"，tag `underworld` 准确。**同上：子域名与实际内容有落差，属分类层。** |
| `culture/faith` | `tales-`(2) | ❌ 错位 | ⚠️ **半错位**：内容是"起源传说/禁忌传说"，**既是 faith 又是 tales**。建议**保留 `tales-` 作为体裁 tag**，同时子域 `faith` 准确。 |
| `culture/customs` | `tales-`(2) | ❌ 错位 | ⚠️ 同上。 |

**修正后的结论：tag 层「错位」实际只有 0~2 处，且与 `tales-` 体裁问题重叠。**
我上一轮的"4 处错位"是**把分类层的问题误记到 tag 层**了。

---

## 三、单例 tag 清单（15 个，观察项）

以下 tag 只出现 1 次，**不是错误**，但**下次新增同子域条目时会面临"用哪个 tag"的决策**：

| tag | 所在子域 | id | 备注 |
|---|---|---|---|
| `bays` | geography/rivers | `doc.geography.bays-charas` | ⚠️ 子域名 `rivers`（河流）装了"湾澳/海/湖"——**子域名偏窄**，实为"水系" |
| `seas` | geography/rivers | | 同上 |
| `lakes`(3) | geography/rivers | | 同上（有 3 条） |
| `rivers`(2) | geography/rivers | | 子域名本身也当 tag 用 |
| `deserts` | geography/terrain | | terrain 下已有 mountains(4)/plateaus/peninsulas/deserts，**这组是齐的**（四大地貌） |
| `plateaus` | geography/terrain | | 同上 |
| `peninsulas` | geography/terrain | | 同上 |
| `settlement`(3) | geography/settlements | `settlement-types-castle/-town/-village` | ✅ **不是问题**：是"聚落类型"系列三连（castle/town/village 各一条），tag 名准确 |
| `clans` | politics/clans | `doc.politics.clans-charas-cortain-secret` | ⚠️ 与 `clan`(74) **单复数不一致**（同子域内应统一） |
| `small` | politics/diplomacy | `doc.politics.small-factions` | ✅ **不是问题**：标题即"次要势力"，语义清楚 |
| `notables` | culture/identity | `doc.culture.notables` | ✅ **不是问题**：标题「要人」，tag＝整条 id 尾段（无 `-`），语义准确 |
| `serfs` | culture/identity | `doc.culture.serfs` | ✅ 同上：标题「农奴」 |
| `throne`(2) | politics/throne | | 子域名＝tag 名 |
| `politics`(2) | politics/offices, politics/succession | `politics-offices` / `politics-succession` | ⚠️ **用域名当 tag**，语义为空 |
| `economy`(7) | economy 下 7 个子域各 1 | `economy-currency` / `-debt` / `-food` / `-land` / `-taxation` / `-trade-routes` / `-workshops` | ⚠️ 同上，**用域名当 tag** |

**经复核，真正要处置的只有 3 项（其余是合理命名）：**

1. ⚠️ `clans`(1) → 应并入 `clan`（**同子域内单复数统一**，这是唯一确定的命名瑕疵）
2. ⚠️ `politics-*`(2) / `economy-*`(7) 共 **9 档"用域名当 tag"** —— tag 与 domain 同名，等于**没有下属分类**。
   这 9 档等于"该子域下唯一一条"，**不是错，是"还没到需要分类的规模"**。下次该子域新增时再定。
3. ⚠️ `geography/rivers` 的**子域名偏窄**（装了湾/海/湖/河）——**属分类层，不属 tag 层**。

---

## 四、结论与建议动作

| # | 事项 | 性质 | 是否动 id |
|---|---|---|---|
| 1 | `concept-`(28) / `tales-`(5) 定性为**跨域体裁层** | 口径 | 否 |
| 2 | `politics/kingdoms`、`politics/law` 的子域名 vs 内容落差 | **分类层**（不是 tag 层） | 否 |
| 3 | `geography/rivers` 子域名偏窄（实为"水系"） | **分类层** | 否 |
| 4 | `clans`(1) → `clan`（单复数） | tag 命名 | 否（登记后用） |
| 5 | 9 档"用域名当 tag"（politics-*／economy-*） | 观察项（规模未到） | 否 |

**一句话：tag 层整体健康（81% 集中在 7 个成熟 tag），真正需要处置的只有"两个体裁筐没定性"这一条；
上一轮报的"4 处错位"经复核全部不成立——其中 2 处是分类层问题（子域名偏粗），2 处与体裁筐重叠，
另有 2 个我误判的（`settlement-types-*`、`small-factions`）实际命名正确。**
