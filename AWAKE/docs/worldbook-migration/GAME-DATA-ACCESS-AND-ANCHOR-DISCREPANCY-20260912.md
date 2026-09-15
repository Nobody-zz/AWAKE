# 游戏数据接入口径 + 锚点核验（BannerlordSage）— 2026-09-12

> 起因：Max 提供了本机工具 `C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main`（"开发工具，可以参考用一下"），
> 并定下规矩「**新增身份必须从游戏数据提取**」。
> 本文记录：这个工具能给世界书线什么、怎么用、以及核验时发现的一处**不一致**（**未实施任何修正**）。
>
> **（2026-09-12 下午追记）** 上文"未实施"的状态**已改变**：Max 指示"修"，修正已实施，见**第七节**。
> 第七节同时补上了判定所需的决定性书证，并记下一个顺带查出的编译阻断项（C16）。

---

## 一、工具是什么 / 索引现状

`BannerlordSage` 是一个 MCP 服务器：读本机 Bannerlord 安装目录 → 导入 XML → 反编译官方 DLL → 建 SQLite 索引 → 通过 MCP 工具暴露给 AI agent。37 个工具（默认集 35）。

**本机索引已建好，可直接用：**

| 项 | 值 |
|---|---|
| 索引根 | `<repo>\dist\games\bannerlord\` |
| 主库 | `bannerlord.db`（**528 MB**，SQLite，43 张表） |
| 索引生成时间 | `2026-09-11T15:14Z`（本地 09-11 23:14） |
| 游戏版本 | **`v1.3.15.110062`**（`bin/Win64_Shipping_Client/Version.xml`）—— **与本模组目标版本一致** |
| 游戏目录 | `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` |
| XML | 3784 文件，**解析失败 0** |
| 反编译源码 | `csharp_types` 4656 / `csharp_methods` 40337 |

**关键数据表：** `bannerlord_cultures` 23 · `bannerlord_kingdoms` 8 · `bannerlord_clans` 95 · `bannerlord_heroes` 397 · `bannerlord_settlements` 494 · `bannerlord_troops` 1696 · `bannerlord_items` 3052 · `bannerlord_perks` 374 · `bannerlord_policies` 32 · `localization_entries` **272,223** · `xml_entities` 300,565。

> **注**：BannerlordSage 是 MCP 服务，**当前未接入本会话**（只接了 agent-mail）。但它的索引就是一个 SQLite 文件，**本侧用只读方式直查即可**（`file:bannerlord.db?mode=ro`），功能等价、无需起服务。

---

## 二、对本线的四项用途

### 1. 满足「新增身份必须从游戏数据提取」

`bannerlord_troops.occupation` 是游戏原生的**职业枚举**，与 `profile-registry.v1.json` 高度对应：

| 游戏 occupation | 次数 | 对应 profile |
|---|---|---|
| Villager | 57 | `profile.villager` ✅在用 |
| Townsfolk | 138 | `profile.townsfolk` ✅在用 |
| Merchant | 55 | `profile.merchant` ✅在用 |
| Lord | 523 | `profile.noble` / `noble_high_steward` ✅在用 |
| **Headman** | 18 | `profile.headman` ⛔未用（登记表已有） |
| **Tavernkeeper** | 6 | `profile.tavernkeeper` ⛔未用 |
| **RansomBroker** | 6 | `profile.ransom_broker` ⛔未用 |
| **Soldier** | 343 | `profile.soldier` ⛔未用 |
| Artisan / Preacher / GangLeader / Gangster / Mercenary / CaravanGuard / Weaponsmith / Blacksmith / Armorer / HorseTrader / ArenaMaster / Musician / ShopWorker / PrisonGuard / TavernWench / TavernGameHost / GoodsTrader / RuralNotable / Wanderer / Bandit / Special … | — | **登记表尚无对应 profile** |

**结论**：登记表里 6 个没用过的身份，**全部能在游戏 occupation 里找到出处**（除 `anonymous`，那是系统级"身份未知"）。而游戏里还有一批 occupation 我们**没有对应身份**——新增身份的候选池就在这里。

### 2. 给权限契约那三个闲置维度提供**真实取值**

`PERMISSION-CONTRACT.md` 的 6 个匹配维度里，`culture_ids` / `kingdom_ids` / `settlement_ids` 内容**一条都没用**（实测全 0）。而游戏数据直接提供真实 ID：

- **文化 23 个**（主文化 6：empire / sturgia / aserai / vlandia / battania / khuzait；另有 darshi / nord / vakken / 各 bandit 等 17 个）
- **王国 8 个**（empire / empire_w / empire_s / sturgia / aserai / vlandia / battania / khuzait）
- **聚落 494 个**（type: town / village / castle / hideout / custom）

即：批次 1「授权结构做真」不必自造取值，**可直接引用游戏本体 ID**。

### 3. 内容素材池 —— 比原先估计大得多

原先认为在册素材只有 5 份编年史（约 28KB）。实际另有：

- **官方本地化文本 272,223 条**（含 CNs 简体中文），里面有大量**聚落描述文**（`Settlements.Settlement.text.*`）；
- **XML 3784 文件**（`xml_documents_fts` 可全文检索）；
- `xml_entities` 300,565 条。

**最关键**：官方聚落描述里**直接写到了我们那几个地点**（见下节）。这是"从游戏数据提取"的现成素材，且天然带 ID 与出处。

### 4. 核对世界书锚点（见下节，这是本次主要产出）

---

## 三、核验结果：5 个地点全部在游戏原生文本里有出处

| 地点 | 游戏文本命中 | 官方原文举例 |
|---|---|---|
| 拉科尼斯湖 | **28 处** | 「阿耳戈隆城位于冰冷缓慢的涅维斯河汇入**拉科尼斯湖**的河口处……」 |
| 沙拉斯湾 | **8 处** | 「突比力斯位于**沙拉斯湾**，阿塞莱人亦称此地为沙瑞兹……」 |
| 卡恰尔半岛 | **5 处** | 「乌斯托科坐落于瓦尔切格湾与比亚里海之间当风的**卡恰尔半岛**……」 |
| 黎明山脉 | **3 处** | 「喀拉罕位于**柯希·罗希尼——黎明山脉**山麓的丘陵地带……」 |
| 德里亚特 | **2 处** | `Settlements.Settlement.name.castle_village_V6_2` = 德里亚特 |

**已确认正确的既有绑定：**

- `Settlements.Settlement.name.town_V7` = **沙拉斯** ✅ —— 与 `LORE-ENTITY-REGISTER` 的 `entity.settlement.town_V7` 交叉引用一致。
- `Settlements.Settlement.name.town_S1` = **瓦尔切格** ✅ —— 其官方描述「曾经是诺德人殖民地的瓦尔切格位于风雨交加的海崖上，此地正是昔日巴旦尼亚领土的**卡恰尔半岛**深处」与登记表 note **逐字对应**。
- **别名得到官方印证**：登记表记「黎明山脉 = 柯希·罗希尼（全称）」——官方文本正是「柯希·罗希尼——黎明山脉」✅

---

## 四、⚠ 发现一处不一致（候选修正，**未实施**）

### 事实

`projection/LORE-ENTITY-REGISTER-20260912.json` 的 `entity.lore.deriat_village`（德里亚特）写着：

> `"game_cross_refs": []`
> `"source_basis": "编年史 rule_德里亚特 4 variants；所引**卡琉斯堡经 BannerlordSage XML 查证非游戏实体**，交叉引用悬空（与 ddfb30ad 注册元数据口径一致）"`

**但游戏数据里两者都是真实存在的实体：**

| 游戏 ID | 中文名 | 类型 | 文化 | 出处文件 |
|---|---|---|---|---|
| `castle_V6` | **卡琉斯堡** | castle | `Culture.vlandia` | `Modules/SandBox/ModuleData/settlements.xml` |
| `castle_village_V6_1` | **卡琉斯** | village | `Culture.vlandia` | 同上 |
| `castle_village_V6_2` | **德里亚特** | village（bound → `castle_V6`） | `Culture.vlandia` | 同上 |

中文名来自 `Modules/Native/ModuleData/Languages/CNs/std_common_strings_xml-zho-CN.xml`（stringId `Settlements.Settlement.name.castle_V6` / `.castle_village_V6_2`）。

### 影响

1. **"卡琉斯堡非游戏实体"这一结论不成立** —— 它是 SandBox 官方模块定义的正规城堡。
2. **德里亚特的实体分类可能错了**：登记表把它归为**语义实体**（`entity.lore.deriat_village`，游戏无对应对象）。但游戏里有同名村庄 `castle_village_V6_2`。若内容所指确为该村，则它应是**游戏锚定实体**（`entity.settlement.*`），按 B9 + ID-MIGRATION-CONTRACT 不得走 `entity.lore.*` 命名空间。
3. 连带影响 `der-vill` / `der-furs` 两档的 `entity_ids` 绑定。

### 可能原因（**未定论**）

`xml_entities` 表里**查不到** `castle_V6`（它 index 的是 XML 的 string/entity 节点，settlement 定义在 `bannerlord_settlements` 表）。若前次核验只走了 XML 实体/名称检索，就会漏掉——**但这是猜测，需复核**。

### 处置建议

- **不擅自改**：该登记表是投影批产物、已经过独立审查并由 Max 签收（`review-state` 在案）。按本线约定，**改动历史产物走「追加修正记录」**，并需**独立复审 + 签收**。
- 建议动作：① 先复核德里亚特/卡琉斯堡与内容所指是否同一对象（人工判定，禁自动猜测）；② 若确认，开一条**候选修正记录**（不改写原值），并把 `entity_ids` 重绑纳入批次 3（结构收口）。

---

## 五、对本线的直接价值（汇总）

| 用途 | 结论 |
|---|---|
| 新增身份 | 有据可依：游戏 `occupation` 枚举即来源（满足 Max 第 4 条规矩） |
| 权限三维度 | 有真实取值：23 文化 / 8 王国 / 494 聚落 |
| 内容素材 | 远超原估计：27 万条本地化 + 3784 XML，且**官方文本直接覆盖我们 5 个地点** |
| 锚点核验 | 可在离线、可复现、带出处的前提下做；本次已查出 1 处不一致 |
| 版本一致性 | 索引为 `v1.3.15.110062`，与模组目标 API `v1.3.15` 一致 |

---

## 六、使用方式（本侧）

只读直查，不改动索引：

```python
import sqlite3
con = sqlite3.connect('file:<repo>/dist/games/bannerlord/bannerlord.db?mode=ro', uri=True)
con.execute("SELECT settlementId, culture, settlementType FROM bannerlord_settlements LIMIT 5")
```

常用核验片段：

```sql
-- 中文名解析
SELECT text FROM localization_entries
WHERE stringId = 'Settlements.Settlement.name.town_V7' AND language = 'CNs';
-- 某地点的官方描述
SELECT text FROM localization_entries
WHERE stringId = 'Settlements.Settlement.text.castle_village_V6_1' AND language = 'CNs';
-- 职业枚举（新增身份候选池）
SELECT occupation, COUNT(*) FROM bannerlord_troops GROUP BY occupation ORDER BY 2 DESC;
```

**边界**：只读。不改游戏文件、不改索引、不反编译新东西（属主干/工具侧）。如需把 BannerlordSage 接成 MCP 供本会话调用，需另行配置。

---

**状态**：核验已完成；第 4 节的不一致**待 Max 定处置**（是否开修正批次）。

> **2026-09-12 下午更新**：Max 指示"修"，处置已定并实施 —— **见第七节**。原第 4 节"未实施"表述就此作废（保留不改写，以第七节为准）。

---

## 七、修正实施记录（2026-09-12 下午追记，Max 指示"修"）

### 7.1 判定：不是"疑似"，是**确证同一对象**

第 4 节写的是"若内容所指确为该村，则应是游戏锚定实体"（当时留了人工判定余地）。复核后**余地没有了**，书证如下：

| 面 | 原文 |
|---|---|
| 游戏官方村庄描述 `Settlements.Settlement.text.castle_Village_V6_2`（CNs） | 「德里亚特位于瓦尔切格湾与埃博半岛的山脊之间。当地村民会在山上捕获河狸与水貂，有时会在海湾水域捉海豹。」 |
| 本案编年史源文（der-vill 断言正文） | 「德里亚特是卡琉斯堡附近的一座村庄，夹在瓦尔切格湾和埃博半岛之间的山脊上。」 |
| 本案编年史源文（der-furs 引文） | 「当地村民在山上捕获河狸与水貂，有时也会在海湾水域捉海豹，毛皮和油脂是他们的主要产出来源」 |

**几乎逐字相同**——编年史该段本身就是从游戏官方村庄描述派生的。再加两条旁证：

- persona-entity 登记表：`entity.settlement.castle_village_v6_2` = 德里亚特，`village_type = trapper`（**捕户村**），与"毛皮和油脂是主要产出"互证，`mapping_status = exact_base`。
- 同表：`entity.settlement.castle_v6` = **卡琉斯堡**（English: Caleus Castle），确实存在 —— 原判"非游戏实体"直接推翻了。

### 7.2 原判为何会错（第 4.4 节当时只是猜测，现已证实）

城堡**没有官方描述文本**：`Settlements.Settlement.text.castle_V6` 在本地化表中 **0 条**（游戏只给村庄/城镇写描述文，城堡只有名称条目）。所以按"文本/XML 实体"检索，`castle_V6` 必然查不到，于是被误判成"非游戏实体"。**属检索面选择问题，不是数据缺失。**

### 7.3 实施内容

| 文件 | 改动 |
|---|---|
| `projection/LORE-ENTITY-REGISTER-20260912.json` | 德里亚特条目由 `entity.lore.deriat_village`（语义）改为 `entity.settlement.castle_village_v6_2`（游戏锚定），补 `game_cross_refs`；两处 `town_V7 / town_S1` → `town_v7 / town_s1`；加 `corrections_20260912`（原值逐字保留） |
| `projection/ANCHOR-BINDING-PLAN-20260912.json` | der-vill / der-furs 绑定改为 game_anchored + 交叉引用 `castle_v6`；五处大小写修正；加 `corrections_20260912` |
| `workspace/authoring/der-vill.yaml`、`der-furs.yaml`（落库 rev2） | `entity_ids` 重绑 |
| `projection/authoring-out/der-vill.yaml`、`der-furs.yaml`（rev1 草稿） | `entity_ids` 重绑 |
| `tools-r3/validate-authoring-closure.ps1` | C12 放宽为 lore + game_anchored 双通道（game_anchored 须在 persona-entity 登记表；实测核过 890 实体库）；新增 C16 |
| `STATUS-20260912.md` | 新增第八节 |

**注意**：`workspace/authoring/` 是 gitignore 的落库产物，本次为**手工改**，未走官方保存链重存（故 revision 未 bump、`registry_bindings` 未重写）。是否补正式重存待定。

### 7.4 顺带查出的编译阻断（C16）

核验过程中顺带发现：12 档里 **10 档**的锚点是 `entity.lore.*`，而编译器 `RuntimePackageCompiler.CanonicalEntityRef`（`RuntimePackageCompiler.cs:253`）**只认 `hero/clan/settlement` 三种 kind**，其余直接抛 `WB-DOC-003`；`BuildEntry` 又对每档每个 `entity_id` 无条件调用（同文件 147）。**这 10 档目前编译必失败**，而此前没人发现，是因为 W6 只跑了结构校验、从未执行 `Compile()`。

这条与 `ENTRY-LINKAGE-MAP` 第 26 行"无需改 C#"矛盾（该结论对锚点**名称查找**成立，对**引用规范化**不成立）。详见 `LORE-ENTITY-REGISTER-20260912.json` 的 `corrections_20260912.compile_blocker_lore_kind` 与 `STATUS-20260912.md` 第 8.2 节。**归属 Studio 侧，本侧不实施。**

### 7.5 当前状态

- 闭合复验：**PASS 14 / FAIL 0 / OPEN 0 / BLOCKED 2**（C13 + C16）。
- 本次修正属**对已过审产物的改动**，按规矩走「追加修正记录 + 独立复审 + Max 签收」；**复审前不得 approve/compile/export/publish**。
- 待 Max 定：① 签收；② 是否补正式重存；③ C16 归属与排期。
