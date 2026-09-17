# 世界书 · 头盔形制批（war/weapons）收官报告

> 批次：`hd1d20260916` ｜ 产出包：`workspace/full-geo1/compiled/geo1-v9/`
> 完成：2026-09-16 12:18（编译）／12:2x（全链验收）
> 前置文档：`WORLDBOOK-ENTRY-CHARTER-20260913.md`、`HELMET-FORM-SCOPE-20260916.md`（本批口径）

---

## 一、这批做的是什么（口径）

**做「盔的类别与形制」，不做「某顶具体头盔的清单」。**

上一轮的设计是"256 件单人头盔逐件建卡"，被否掉（太多、没必要）。改为按**形制类别 + 特色**归类，
10 张类卡落 `war` / `weapons`（对齐既有 `weapons-crossbow` 的形态：war/weapons、多断言、
`culture_ids` 限定 grant、文化 rumor 只挂 villager）。**每件物品只归一张卡**（生成器硬断言，72 个引文全局唯一）。

| 卡（slug） | 中文名 | 引证件数 | 文化限定层 |
|---|---|---|---|
| `head-cloth-coif` | 布制围帽与头巾 | 10 | 1 |
| `head-fur-cap` | 毛皮帽 | 8 | 3 |
| `head-kettle` | 锅盔 | 8 | 2 |
| `head-nasal` | 护鼻盔 | 6 | 2 |
| `head-closed` | 全覆面盔 | 6 | 2 |
| `head-mail-coif` | 链甲围帽 | 5 | 1 |
| `head-cheekguard` | 护颊盔 | 6 | 1 |
| `head-crown` | 王冠与仪式盔 | 7 | 2 |
| `head-oddity` | 兽首与面具盔 | 6 | 0 |
| `head-layered` | 叠穿：盔下的那一层 | 10 | 2 |
| **合计** | | **72** | **16** |

---

## 二、素材通道

- **A 级引证＝官方物品属性本身**（宪章"物品正文走属性成文"）。
  编年史池与官方 lore 里**都没有盔甲内容** ⇒ 盔类卡的锚只能落在 `head_armors.xml` 的属性行上。
- 快照：`workspace/full-geo1/sources/game-items-head.txt`
  - **256 行 / 56,846 B / sha256 `45887F4A4963E921A9C498B871009749C5B0BD65E1FE8D7B3885E660FDDE0A7F`**
  - 每行 `head.<id> => 中文名 | HeadArmor | 护头 N | 重 W | 材质 X | 属 culture | 遮发 … | 遮须 … | 潜行 … | 认队色 | 分男女 | 叠穿 | 出处 head_armors.xml#<id> | 译名token <tok>`
- 来源档：`source.calradia.game.items-head`（`bannerlord-1.3.15.110062` / `game_snapshot` / `base` / `permitted`）。
  **独立 source_id，不碰 poc2 快照 hash**；既有 13 张器物档一字未动。

**层级勘误（本轮踩到）**：`weight` / `difficulty` / `appearance` / `culture` / `is_merchandise` 挂在 **`<Item>` 上**，
不在 `<Armor>` 上；`head_armor` / `material_type` / `modifier_group` / `hair_cover_type` / `beard_cover_type` /
`stealth_factor` / `has_gender_variations` 在 `<Armor>`；`UseTeamColor` / `Stealth` 在 `<Flags>`。

---

## 三、盘点事实（`head_armors.xml`，256 件单人头盔）

- 文化分布：瓦兰迪亚 70 / 帝国 55 / 阿塞莱 47 / 斯特吉亚 33 / 巴旦尼亚 26 / 库赛特 21 / 劫匪 2 / 中立 1 / 诺德 1
- 材质：Plate 175 / Cloth 46 / Leather 20 / Chainmail 15
- 护头：min 1 ／ p25 13 ／ **median 33** ／ p75 45 ／ max 54
- **真特色 = 叠穿**：**89/256 的名字是 `over X`**（盔下垫围帽／填充帽／链甲）
- `hair_cover_type` = all 197 件；`beard_cover_type` 分 7 档；12 件带 `stealth_factor`（潜行加值）
- **护颊盔 9 件全属巴旦尼亚**（卡拉迪亚只此一家 —— 这是本批最"有脸"的一条特色）

DB 侧 447 条 `HeadArmor` 中 **191 条是 `mp_` 前缀（多人模式）**，XML 里不存在 ⇒ 单人 256 件对得上；缺中文名 0 条。

---

## 四、最重要的机制查证：文化限定 rumor 只能挂 `villager`

**症状**：`IWH7`（巴旦尼亚贵族问护颊盔）拿到"村民口吻的 rumor"，层级判定掉到 `partial`。

**三层真因（逐层排掉才到位）**：

1. **打分公式**（`WorldKnowledgeQueryService.SelectExpression:286`）：
   `score = 规则条件分 × 10 + 表达层号`。
2. **文化匹配 +20**（`WorldbookIdentityEvaluator:82-91`）⇒ **×10 后 = +200**，
   而"层号"最多差 3 ⇒ **文化匹配完全压过层级**。
3. **身份父链**（`AddIdentity` 沿 `Parents` 传播；编译包 `runtime.json` 实测）：
   `noble→notable→commoner`｜`merchant→townsfolk→commoner`｜`headman→notable`｜`soldier→commoner`。
   ⇒ 文化 rumor 发给 `notable` / `commoner` / `townsfolk` **等于没收窄**（贵族沿父链照样被命中）。

**结论**：`villager` 不是任何 detail 身份的先祖 ⇒ **唯一安全落点**。
（既有 `weapons-crossbow` 把文化 rumor 只挂 villager，正是这个原因 —— 本轮才算说明白。）
文化 **detail** 照旧挂 `headman/merchant/soldier/noble`：同层对同层，文化版胜出，结果仍是 `known`。

**修法**：`_rollout_head_gen_20260916.py` 的 `CULTURE_RUMOR_GRANTS = [("profile.villager","local")]`。
副作用：全库 grants 6423 → **6369**（文化 rumor 由 4 身份收到 1 身份）。

> ⚠️ 这条坑与 `weapons-crossbow` 的写法**同源**，但直到本轮才被"贵族 × 特定文化"这个组合撞出来。
> 教训：**探针覆盖度本身就是可靠性的一部分** —— 上一版矩阵只测了一个身份×文化组合，144/145 差点把它盖过去。

---

## 五、验收结果（全部实测）

| 关 | 结果 |
|---|---|
| 六步链（validate→register→select→approve→proof→compile） | **OK**，总用时 1.5 分钟；批次 `hd1d20260916` |
| compile 身份 | `manifest_hash=1597f2e3baf7103598ad6f5a3863f01eaf017288bbfa42a7d1dd60d0d7ec39d7`<br>`result_hash=EA19AA5D4600A9D7CC582303CA144E4EA9740F5A648461AB13EC001F6E351616` |
| validation | `total=21 error=0` |
| 条目回读（runtime.json） | `entries=458`，带锚点 **402**，本批入包 **10** |
| **矩阵探针** | **145 / 145 PASS，FAIL 0**（`IWH1–IWH12` 全过；`IWH7` 由 partial 转 **known**） |
| 授权/表达自检 | `docs=458 / exprs=1032 / grants=6369 / denies=3 / violations=0 / passed=true`；18 条警告**全为旧档**，本批 0 |
| 文本质检（awake-prose-qc） | 458 档全扫，**硬 0 / 软 0** |
| 跨档引文查重 | 6 组重复，**全为旧档**（clans / territories / mountains / villages），本批 **0 新增** |
| 名录 xlsx | `rows=458`，前缀 21（新增 `head`），本批 10 行落 **war / weapons** |

**双写一致**：`authoring-out/weapons-head-*.yaml` 与 `workspace/full-geo1/authoring/` **10/10 逐字节一致**（14.7–28.2 KB）。

**矩阵新增行**（`IWH1–IWH12`，前缀 `IWH` = Head，与 `IT/IW/IS/IV/CS` 并列）：
每张类卡一行 + 村民求 detail 的"门外照"（`IWH11`，正确落 partial）+ 架空词 not_found 对照（`IWH12-角冠铁胄`）。

---

## 六、本批产出文件

- 生成器：`docs/worldbook-migration/projection/authoring-out/_rollout_head_gen_20260916.py`
- 文案素材：`_l2_head_a_20260916.json` / `_l2_head_b_20260916.json`
- 盘点/探针/快照：`_head_inventory_20260916.py`、`_probe_headxml_20260916.py`、`_head_pick_20260916.py`、
  `_head_types_20260916.py`、`_head_snapshot_20260916.py`
- 链脚本：`tools/worldbook-studio/workspace/full-geo1/_head1_chain_20260916.py`（日志 `_head1_chain_log.txt`）
- 探针：`_head1_probe_spec_20260916.py`（追加 `IWH1–IWH12`）、`_verify_matrix2_20260913.py`（加判定预期）
- 包：`workspace/full-geo1/compiled/geo1-v9/`（**保留 v7/v8 作对照**）

---

## 七、遗留 / 待裁决

1. **两个待裁产品问题**（仍在 `TOPIC-WORLDBOOK.md`）：
   - 两棵 authoring 树哪棵权威（`docs/.../authoring-out` vs `tools/.../full-geo1/authoring`）——本批已双写保持一致。
   - 锚点类型是否扩契约。
2. **包未投送、未真机复验**：`geo1-v9` 目前只在 workspace；**离线全绿不算过版**，须 assemble→deploy→启动游戏看 `Awake.log`。
3. **共享工具未提交**：`Program.cs` 的 `authoring-register-batch` 分支（其他线也用，提交前须确认未混编，**绝不 `git add -A`**）。
