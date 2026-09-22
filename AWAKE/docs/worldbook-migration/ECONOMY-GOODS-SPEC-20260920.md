# 经济批（货品与物产）· 档型清单与开工约束

> 立档：2026-09-20 · 承接甲方两份机制普查（`物价体系与购买力.md` / `金钱机制普查.md`）
> 本批范围：**只做「看得见的东西」** —— 缺的贸易品、牲畜与驮畜，外加「谁产什么」。
> **经济体系层（价格机制/点差/行情/工坊/商队/汇率/财富五档）不在本批**，另立一批。

---

## 〇、一句话口径

**普查给的是「东西值多少」，本批要写的是「谁在产、谁在卖、谁知道它值钱」。**
机制数值可以进 `detail` 层，但**表述必须换成行话，不许出现公式**；
「谁知道」这层一律用**官方文案原话**（行情传闻、工匠抱怨、商人转述）当来源。

---

## 一、现状读数（v19 产物实测，507 档）

| 位置 | 有 | 缺 |
|---|---|---|
| `economy/goods` | 6 档（`deriat` 村庄生计 + 盐/毛皮/天鹅绒/香料/银矿石） | 真·货物档只 5 件；游戏共 **23 种贸易品** |
| `economy/items` | 15 档（10 装备 + 牛/羊/骡/旅行马/驮运骆驼） | 牲畜缺 5（猫/狗/鹅/鸡/肉猪）；驮畜缺 3（驮马/役马/骆驼） |
| `geography/resources` | 1 档（`mines-lycaron` 吕卡隆银矿） | — |
| 经济体系层 | **0 档**（全 507 档里「物价」0、「金币」0） | 另立批 |

---

## 二、档型清单（本批 25 档新建）

### A. 贸易货物 17 档 —— `economy` / `goods`

**来源**：`bannerlord.db` → `bannerlord_items WHERE itemType='Goods'`（**官方中文名 + `value` 列**）

| # | slug | 官方中文名 | 标价 | type |
|---|---|---|---|---|
| 1 | `goods-fish` | 鱼 | 12 | Goods |
| 2 | `goods-flax` | 亚麻 | 15 | Goods |
| 3 | `goods-clay` | 黏土 | 18 | Goods |
| 4 | `goods-grape` | 葡萄 | 20 | Goods |
| 5 | `goods-wool` | 羊毛 | 22 | Goods |
| 6 | `goods-butter` | 黄油 | 25 | Goods |
| 7 | `goods-olives` | 橄榄 | 30 | Goods |
| 8 | `goods-cheese` | 奶酪 | 40 | Goods |
| 9 | `goods-date-fruit` | 枣 | 50 | Goods |
| 10 | `goods-beer` | 啤酒 | 50 | Goods |
| 11 | `goods-cotton` | 生丝 | 80 | Goods |
| 12 | `goods-wine` | 葡萄酒 | 90 | Goods |
| 13 | `goods-pottery` | 陶器 | 210 | Goods |
| 14 | `goods-leather` | 皮革 | 230 | Goods |
| 15 | `goods-linen` | 亚麻布 | 245 | Goods |
| 16 | `goods-oil` | 油 | 290 | Goods |
| 17 | `goods-jewelry` | 贵重品 | 675 | Goods |

### B. 牲畜与驮畜 8 档 —— `economy` / `items`

| # | slug | 官方中文名 | 标价 | type | 来源 |
|---|---|---|---|---|---|
| 18 | `items-cat` | 猫 | 20 | Animal | `bannerlord_items` |
| 19 | `items-dog` | 狗 | 20 | Animal | 同上 |
| 20 | `items-goose` | 鹅 | 50 | Animal | 同上 |
| 21 | `items-chicken` | 鸡 | 50 | Animal | 同上 |
| 22 | `items-hog` | 肉猪 | 60 | Animal | 同上 |
| 23 | `items-sumpter-horse` | 驮马 | 110 | Horse | 同上 |
| 24 | `items-old-horse` | 役马 | 130 | Horse | 同上 |
| 25 | `items-camel` | 骆驼 | **未配** | Horse | 同上（`value` 列为空，quote 注明） |

> ⚠️ `camel` 的 `value` 在一手数据里是空的（源码未设），**不硬凑数字**，照实写「未配基准价」。
> ⚠️ 已被覆盖、不重复做：`salt` / `fur` / `velvet` / `spice` / `silver`（货）、`cow` / `sheep` / `mule` / `saddle_horse` / `pack_camel`（畜）。

---

## 三、「谁产什么」的落点（本批不进独立档，写进 detail 表达）

**一手依据**：`DefaultVillageTypes.cs:139-274` —— 22 种村庄类型 / 全图 274 村（`settlements.xml` 的 `village_type`）。

| 村庄类型（官方中文名） | 村数 | 产出（`AddProductions`，括号内为产量权重） |
|---|---|---|
| 麦田 wheat_farm | 49 | 谷物 50 ＋ 牛 0.2 / 羊 0.4 / 肉猪 0.8 |
| 渔民 fisherman | 22 | 鱼 28 |
| 牧羊场 sheep_farm | 17 | 羊 4 / 羊毛 10 / 黄油 2 / 奶酪 2 |
| 森民 lumberjack | 16 | 硬木 18 |
| 葡萄园 vineyard | 16 | 葡萄 11 |
| 黏土矿坑 clay_mine | 16 | 黏土 10 |
| 畜牧场 cattle_farm | 15 | 牛 2 / 黄油 4 / 奶酪 4 |
| 亚麻种植园 flax_plant | 13 | 亚麻 18 |
| 盐矿 salt_mine | 12 | 盐 15 |
| 橄榄树 olive_trees | 12 | 橄榄 12 |
| 铁矿 iron_mine | 11 | 铁矿石 10 |
| 马场 ×6（欧/草原/沙漠/巴旦尼亚/斯特吉亚/瓦兰迪亚） | 33 | 各文化马＋驮马/骡子/旅行马/役马（沙漠场另有骆驼 0.3 / 战骆驼 0.08 / 驮运骆驼 0.3） |
| 椰枣园 date_farm | 9 | 枣 8 |
| 猪场 swine_farm | 9 | 肉猪 8 / 黄油 2 / 奶酪 2 |
| 猎户 trapper | 9 | 毛皮 1.4 |
| 蚕饲养场 silk_plant | 8 | 生丝 8 |
| 银矿 silver_mine | 7 | 银矿石 3 |

⇒ **22 件贸易品 正好对应 22 种村庄产出**；「谁产出它」是每件货物档 detail 层的天然内容。

---

## 四、开工约束（四条硬断言）

1. **译名一律用官方**。普查与官方有出入的四处已核出，**不得沿用普查写法**：
   | 普查写法 | 官方 | 依据 |
   |---|---|---|
   | 珠宝 675 | **贵重品** 675 | `localization_entries` `jewelry` |
   | 棉 80 | **生丝** 80 | `cotton` |
   | 老马 130 | **役马** 130 | `old_horse` |
   | 椰枣（物品） | **枣**（物品）；「椰枣园」是村庄类型名 | `date_fruit` / `VillageTypeDateFarm` |
   ⚠️ B 级编年史里若出现旧译，**不得据以改官方名**（编年史是「世界内的说法」，不是权威名）。

2. **不许把公式写进正文**。`价格 = 比值^0.6`、`标价×系数`、`0.14×inStoreValue+2` 这类，**一个字都不进断言**。要写的只有「行话级」结论，如「同一件货，城里买进再卖出要亏一成」——而且这类要么有官方文案支撑，要么归到 `detail` 层给商人/贵族。

3. **「谁知道」用官方文案当来源**。本批已定位的现成原话（`localization_entries`，CNs）：
   - `T3vJp4PG`「谁能掌握价格的运作方式，谁就能凭空生财。」（商人视角）
   - `evuR5kwf` 行情传闻原文「有传闻说这附近的{ITEM_NAME}比其他城镇卖得便宜……」（**这就是 TradeRumor 的官方文案**）
   - `7fjOwwVH`「我和一些商人交谈时了解到，他们刚从{SETTLEMENT}以{BUY_COST}的价格买来了一些{ITEM_NAME}」
   - `b778BvfW` / `gTlW1qOB`「这个东西比平均价格贵了/便宜了{PERCENTAGE}%。」
   - `3KpgHPlo` / `FKtkmwtb` / `Vg7Ftrdl` 工匠抱怨「法律规定我只能按固定价卖给本地商人，他们却统一口径抬价」
   - `RYkPTHv1`「从村庄购买物品的价格惩罚{VALUE}%。」（村庄买卖的价差）

4. **alias 不放类别词**。旧档里 `Goods` / `Animal` / `Horse` 这类类别词是**过匹配源**，新档一律不写；
   `zh-CN` 别名只放同一事物的真别称，`en` 别名放英文名（供中英混问）。

---

## 五、生成与验收流程（与军事批同款）

1. L2 数据 json：`docs/worldbook-migration/projection/authoring-out/_eco_{A,B}_20260920.json`
2. 生成器：`_gen_eco_20260920.py` —— **双写**（现役 `workspace/full-geo1/authoring/` + 镜像 `docs/worldbook-migration/projection/authoring-out/`），引文校验 + 写盘前硬断言
3. `--check` 干跑 → 实跑 → `diff -rq` 逐字节复验
4. 链式编译（六步，`OPBASE=eco20260920`）→ 产物 `compiled/geo1-v20-goods`
5. 可达性探针（`--cap` / `--pure` 两模式）—— 逐 grant 驱动，修「写而不用」
6. 名录刷新 + 报告 + 记忆

---

## 六、不做的（明确划出）

- 经济体系层条目（价格机制/点差/行情/工坊/商队/汇率/财富五档/巷子/债务豁免）—— **另立一批**
- 回补已上线的 5 件货品档与 5 档牲畜档 —— **列为第二段**，待本批新档验收通过后再定是否动
- 装备类 `economy/items` 10 档的历史遗留（与 `war/weapons` 39 档重叠）—— 不在本批

---

## 七、定稿补记（09-20 收工后回填，**规格正文之上以此节为准**）

上面二节的清单是**开工前**写的（25 档）。实际落地 **40 档**，差在三处：

1. **`camel`（骆驼）撤掉**。实测它的 `value` 在一手数据里是空的——普查把它与 `pack_camel`「驮运骆驼」混了。不硬凑数字 ⇒ 本批牲畜驮畜只 7 档（去掉骆驼）。
2. **补上普查 10.1 那批 9 件**（谷物／肉／兽皮／硬木／木炭／铁／板材／毛毡／工具）。上一版因它们在 `bannerlord_items` 里**没有 entityId** 而排除；实际那是**品类 id 不是物品 id**（一手：`DefaultItems.cs:117-135` 的 `InitializeTradeGood`）⇒ 改用 C# 组 token 反查官方中文名，按物品真身补齐。
3. **补上六种铁锭**（粗铁／熟铁／铁／钢／优质钢／精炼钢）：一手 `ironIngot1–6` 是**独立物品档**。
4. **另加 1 件**：`stolen-goods` 被盗的货物。

⇒ 定稿 40 档 ＝ A1 组 18 ＋ A2 组 15 ＋ B 组 7。
最终读数与逐档清单见 `ECONOMY-GOODS-REPORT-20260920.md`。
