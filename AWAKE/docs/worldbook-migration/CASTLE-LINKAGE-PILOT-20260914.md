# 城堡档 + 聚落隶属联系 · PILOT 验证与批量计划

**日期**：2026-09-14　**线**：世界书　**状态**：**PILOT 已验通；批量待令**

---

## 一、问题（Max 问）

「村庄和其所属的城堡、城镇的知识词条有联系吧？」—— **实查结论：游戏数据里有，知识词条里没有。**

| 层 | 实况 |
|---|---|
| 数据层 | **有**：`bannerlord_settlements.boundSettlement`（形态 `Settlement.castle_EN1`／`Settlement.town_EN1`，join 须去 `Settlement.` 前缀）。273 村归属：**132 → 城堡（67 座）／141 → 城镇（53 座）** |
| 词条层 | **没有**：273 村档 **0** 写上级；53 镇档 **0** 提下辖村；**城堡档 0 个** |
| 设计层 | **有机制**：`ENTRY-LINKAGE-MAP-20260912.md` 轴二＝`entity_ids` 实体锚点；但仅 **14 档**在用（老批 town-varcheg／village-deriat 等），且只锚自身 |
| 运行时 | **没有**：`content-graph.json` 仅 `contains`／`references_source`／`index_entry` 三类边，**无聚落边**，不能从父聚落跳到子村 |

**关键硬约束（实测 schema）**：`awake.worldbook.authoring.v1.schema.json` 的 **`additionalProperties: false`** ⇒ **不能加自定义字段**（如 `parent_settlement`）。`id` 允许 `doc.geography.castle-*`；`entity_ids` 只收**小写**（`^entity\.[a-z0-9]+(?:[._-][a-z0-9]+)*$`）。

⇒ **联系只能落在既有字段上**：`aliases`（进检索面）＋ `entity_ids`（实体锚点）。

## 二、素材面（城堡档的引文锚从哪来）

- **城堡无任何官方描述文**：DB 实查 castle 67 座、有 `descriptionText` **0**；localization 里 `Settlement.text.castle*` 92 条**全是** `castle_village_*`，**纯城堡文本 0 条**。
- B 级编年史（AnimusForge）**基本无城堡内容**（4 个 chronicle 文件里"堡/Castle"仅 1 命中）。
- ⇒ 城堡档可用 A 级素材＝**官方城堡名**（`Settlements.Settlement.name.castle_EN1` → 瓦拉戈斯堡）＋ DB 事实（culture／owner／下辖村／prosperity／攻城场景 sceneName）＋ **下辖村的官方描述文**（内容主体由村庄描述聚合而来）。
- 引文锚：`quote` ＝ **官方城堡名**，`quote_hash = sha256(quote)`，快照文件含该名（register 校验：`quote` 必须在快照里能定位）。

## 三、PILOT 设计（已落 3 档）

档型 `doc.geography.castle-<slug>`（slug＝英文名小写连字符），关键字段：

```yaml
aliases:
  zh-CN: [德鲁伊莫尔堡, 城堡, 德鲁伊莫尔, 托·梅利纳]   # 自身 + 类别 + 下辖村名（＝联系入口）
  en: [Druimmor Castle, castle_B3, Druimmor, Tor Melina]
entity_ids: [entity.settlement.castle_b3]              # 自身聚落锚（小写 sid）
sources: [{source_id: source.calradia.game.castles, locator: bannerlord.castles#castle_b3,
           quote: 德鲁伊莫尔堡, quote_hash: sha256(德鲁伊莫尔堡)}]
```

- 新来源登记 `source.calradia.game.castles`（快照 `game-castles-desc.txt`，每堡一行含官方 CN 名）。
- L2 三层手写：assert＝中立转写（文化＋下辖村＋产业）；rumor＝村民口吻；detail＝门道/行情。
- **文化口径**：巴旦尼亚用 氏族/部众；斯特吉亚用 波耶/领主；帝国用 元老院/行省；**禁帝制词混用**。

### PILOT 3 档
| 档 | 城 | 文化 | 下辖 |
|---|---|---|---|
| `castle-druimmor-castle` | 德鲁伊莫尔堡 | battania | 德鲁伊莫尔（银）・托·梅利纳（陶土） |
| `castle-varagos-castle` | 瓦拉戈斯堡 | empire | 瓦拉戈斯（羊）・艾俄里亚（粮） |
| `castle-ustokol-castle` | 乌斯托科堡 | sturgia | 乌斯托科（牛）・哲米扬（马） |

## 四、PILOT 端到端结果（全过）

```
当前档数: 384  需注册: 3
[1/5] register 3/3
[2/5] select -> ... items: 384
[3/5] approve -> approval.20f9b8803b8d40bb8d0b47d711680b26
[4/5] proof  -> compile.f5e25a6b71934a9da692bb4cb1624b46
[5/5] compile -> compiled/geo1-castlepilot  manifest: f8e40b9e5189e40c  validation: 21  errors: 0
```
⇒ 新档型 + 新来源登记 + `entity_ids` + quote=官方名 **全链通过 schema 与来源校验**。

### 联系探针（`_castlepilot_spec_20260914.json`）
| 行 | 查询 | 身份 | 结果 |
|---|---|---|---|
| CP1 | 德鲁伊莫尔堡 | 商人 detail | known ✅ |
| CP2 | **德鲁伊莫尔** | 商人 detail | 命中**堡档** ✅（问村名→堡档亦召回） |
| CP3 | **托·梅利纳** | 商人 detail | 命中**堡档** ✅（用下辖村名反查到堡） |
| CP4 | 乌斯托科堡 | 村民 rumor | **堡档 rumor ＋ 村档 rumor 同返** ✅（双向联系） |
| CP5 | 瓦拉戈斯 | 贵族 detail | 命中**堡档** ✅ |
| CP6 | 子虚堡 | 商人 detail | not_found ✅（对照） |

**机制**：检索＝`title+aliases+锚点` 上的**双向子串**命中，故「堡档带村名」＋「村档带堡名」互为入口。

## 五、批量计划（待令执行）

| 阶段 | 内容 | 规模 |
|---|---|---|
| P1 | 城堡档 **67 座**（含 pilot 3；补 L2 64 条手写） | 67 档（净新增） |
| P2 | 村庄档补丁：加 `entity_ids`（自身，小写 sid）＋ `aliases` 增上级聚落名 | **273 档**改 |
| P3 | 城镇档补丁：加 `entity_ids` ＋ `aliases` 增下辖村名 | **53 档**改 |
| P4 | 全量重编译（register ~390 档改＋增，约 **60–70 分钟**，后台）＋矩阵扩 CP 段＋自检＋名录＋prose-qc＋报告 | 一次 |
| P5 | 命名规范补 `castle` 类目（现 geography 词表无 castle） | 1 处 |

**风险/注意**
1. P2/P3 改 326 档 ⇒ 全量重注册（register 因 journal 累积已近二次方，是本批最耗时项）。
2. `castle_A7`（乌格巴堡）下辖村 `乌格巴` 的官方 CNs **错挂他文**（已知）——该堡 L2 不用该错挂文。
3. 城堡名与首村名常有 **CN 不一致**（如 castle_S3＝涅维扬斯克堡 vs 村＝涅夫扬斯克，官方数据如此），一律**照官方串写**，不得"修正"。
4. cross_dup 会因堡档引用村庄名而**无新增 hash**（堡档不引村档 quote，只把村名放 aliases）——设计上已规避。

## 六、本 PILOT 产物（未提交）

- 档：`castle-druimmor-castle.yaml`／`castle-varagos-castle.yaml`／`castle-ustokol-castle.yaml`（double-write 到 WS）
- 生成器：`_rollout_castles1_gen_20260914.py`；取数：`_castle_fetch_20260914.py`＋`_castle_inventory_20260914.json`
- 六步：`_castlepilot_sixsteps_20260914.py`；探针 spec/result：`_castlepilot_spec_20260914.json`／`_castlepilot_result.json`
- 包：`compiled/geo1-castlepilot`（384 档）
