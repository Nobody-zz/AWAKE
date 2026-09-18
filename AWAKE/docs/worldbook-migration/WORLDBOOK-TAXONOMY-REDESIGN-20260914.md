# 世界书分类体系重构设计（2026-09-14）

> 设计稿，待甲方裁。落地前不改 schema／不重建包。
> 上一版"统一前缀＝子域"的中间产物（taxonomy 48 子域＋448 档改写）**保留未撤**，本稿通过后再定接回还是推倒。

---

## 0 · 一句话

分类的病根不是"分得不够细"，是**三个不同的问题被挤进了同一棵树**。
本设计把三件事各归其位：**类型**（单值，进 taxonomy）、**侧面**（多值，新字段）、**立场与时间**（已有字段，不进 taxonomy）。

## 1 · 三个问题，三种机制

一条目要回答三件事，各有各的表达方式：

| 问题 | 性质 | 落点 | 说明 |
|---|---|---|---|
| **它是什么** | 单值、封闭、互斥 | `domain`（族）＋ `subdomain`（类型） | 这是 taxonomy 该干的事，也是现在做混了的事 |
| **从哪些面看** | 多值、正交 | **新字段 `facets`**（多值） | 政治／经济／军事／文化／地理——旧"五域"降级为多值侧面 |
| **它算几成真** | 立场／可信度／时间 | `content_tier` / `authority` / `era` | **已有字段**，taxonomy 不管 |

为什么这样拆——现状第二层混装三种东西：`castle`（类型）、`taxation`（侧面）、`tale`（文体）。
- `castle` 和 `tactics` 成兄弟，是错的。
- `tale` 那 6 档本质是"传说／多口径并置"（**立场**），不是题材，该用 `content_tier` 表达。
- 一座城堡同时是地理、政治、军事对象 ⇒ 侧面**天生多值**。设计里留了 `related_domains`（多值，最多 4 项）但**全库 0 档启用** ⇒ "多面"只写在纸上。

## 2 · 新的类型树（`domain` = 族 → `subdomain` = 类型）

**命名原则：按"它是什么"命名，不按"它属于哪个领域"命名。** 互斥、封闭、够用。
7 族 24 类型：

| 族（`domain`） | 类型（`subdomain`） | 各是什么 |
|---|---|---|
| **地方** places | `settlement` 聚落 · `landform` 地貌 · `waters` 水域 · `route` 通路 · `site` 场所 | 有位置的东西。聚落的城堡/村庄/城镇是**属性**，不再各占一格 |
| **人群** people | `person` 人物 · `lineage` 家族 · `folk` 族群 · `group` 群体 | 人和人的集合。家族＝血缘；族群＝民族；群体＝行会/军团/结社 |
| **势力** powers | `realm` 王国 · `fief` 领地 · `office` 官职 · `law` 法度 | 权力结构。王国＝主权实体；领地＝其下辖土 |
| **器物** things | `gear` 装备 · `commodity` 物产 · `relic` 珍宝 | 物件。装备＝兵器甲胄畜力；物产＝原料商品；珍宝＝孤品 |
| **事件** events | `war` 战事 · `event` 变故 | 发生过的事。战事单列（Bannerlord 战争占大头） |
| **制度** institutions | `military` 军制 · `livelihood` 营生 · `custom` 习俗 · `language` 语言 | 做法与系统。不是地方、不是单次事件 |
| **心念** beliefs | `faith` 信仰 · `lore` 传说 | 人心里想的。信仰＝神祇教义；传说＝怪谈缘起 |

**关键取舍——聚落只留一格 `settlement`**：城堡/村庄/城镇从"三个类型"降为 `settlement` 的一个**属性**。
理由：**档名前缀本来就是"类型＋属性"的写法**（`castle-akiser-castle`＝类型 `castle`＋名 `akiser-castle`）。
让分类表跟档名同构，一条信息只在一个地方表达。

## 3 · `facets`（多值侧面）＝旧五域的降级

旧"五域"（politics／economy／culture／war／geography）**不删**，降为 `facets`（多值）：

| 值 | 含义 | 例 |
|---|---|---|
| `politics` 政治 | 权力、主权、统治 | 城堡的政治面 |
| `economy` 经济 | 生产、交易、财富 | 城堡的经济面 |
| `culture` 文化 | 信仰、习俗、日常 | 传说的文化面 |
| `war` 军事 | 战事、军队、防御 | 城堡的军事面 |
| `geography` 地理 | 方位、地形、通行 | 城堡的地理面 |

例：`阿契赛尔堡`＝`domain:地方`＋`subdomain:settlement`＋`facets:[geography, politics, war]`。
`steppe_war_bow`（兵器）＝`domain:器物`＋`subdomain:gear`＋`facets:[war]`——**兵器回到器物这一个家，军事是它的面**，不再被劈到 war 和 economy 两边。

## 4 · 与现有字段的分工（不再重复表达）

| 已有字段 | 管什么 | 为什么不进 taxonomy |
|---|---|---|
| `universe` | 哪个宇宙 | 世界观坐标，不是题材 |
| `era.key` / `era.certainty` | 何时、有多确定 | 时间轴 |
| `content_tier` | 素材层级（A/B 级、立场） | **传说／多口径在这里** |
| `authority` | 正典优先策略 | 冲突消解规则 |
| `grants`（profile×scope×min_detail） | 谁知道、问多远、问多深 | **检索面**，与题材正交 |

## 5 · 448 档怎么落（现状映射，示例）

| 现档 | 新 domain | 新 subdomain | 新 facets |
|---|---|---|---|
| castle／village／town（393） | 地方 | `settlement`（属性记 castle/town/village） | geography, politics, war |
| lake/river/bay/sea（7） | 地方 | `waters` | geography |
| mount/mountains/plateau/peninsula/desert（7） | 地方 | `landform` | geography |
| item 甲胄兵器（15） | 器物 | `gear` | war, economy |
| item 物产＋furs（6） | 器物 | `commodity` | economy |
| territory（4） | 势力 | `fief` | politics, geography |
| throne（2） | 势力 | `realm` | politics |
| clan（1） | 人群 | `lineage` | politics |
| tale（6） | 心念 | `lore`（立场由 content_tier 标） | culture |
| military（3） | 制度 | `military` | war, culture |
| troops（2） | 人群 | `group` | war |
| weapons（1） | 器物 | `gear` | war |
| mine（1） | 地方 | `site` | economy, geography |

## 6 · 要动的工程面与成本

| 面 | 动什么 | 成本 |
|---|---|---|
| taxonomy json | 新 shape（族→类型＋facets 枚举） | 低（一份文件，CAS 流程） |
| schema | `domain` enum 5→7；新增 `facets` 字段 | 中（`awake.worldbook.authoring.v1.schema.json`＋`common.schema.json` 的 `knowledge_domain`/`event_domain`＋`enums.json`） |
| Studio UI | 族/类型下拉重排；`facets` 多选控件 | 中 |
| 448 档 | 重分类 `domain`/`subdomain`＋补 `facets` | 高（同上一版：≈90 分钟重登记） |
| 编译/包 | 重编译＋重建包＋golden 钉值 | 高 |

## 7 · 待裁

1. **族/类型这套**（7 族 24 类型）行不行？
2. **`facets` 这个多值字段**要不要加？（加＝正解但要动 schema＋UI；不加＝只修类型树，丢掉"按面过滤"）
3. **字段名**：`domain`/`subdomain` 改名 `family`/`kind` 更准，但要改全部 448 档与代码；**保留现字段名只改语义更便宜**。我建议保留现名。
4. **`throne`／`troops` 的落点**：王位归 `realm` 还是 `office`？兵种归 `group` 还是 `military`？
