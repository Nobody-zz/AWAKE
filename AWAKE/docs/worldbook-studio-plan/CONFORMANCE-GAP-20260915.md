# 知识卡编辑器 · 「先行标准」对账与整改提案（2026-09-15）

> **性质**：提案，**未实施**。
> **口径来源**：Max 09-15 指令「知识卡编辑器应该按照先行标准来做」。
> **「先行标准」＝规范在先**：契约不动，实现去合契约。

---

## 0. 一句话结论

目录里给知识体系立了**三根轴**（领域五类 / 知识形态三级 / 素材四级）。
编辑器**只落了领域这一根**；知识形态**一根没落**，素材分级**落在来源侧、到不了卡上**。

| 轴 | 规范出处 | 编辑器落点 | 实测状态 |
|---|---|---|---|
| **领域**（固定五类） | 手册 B1，CATALOG:22 | `authoring.v1` 的 `domain`（五值 enum）＋ `subdomain` | ✅ 已合规 |
| **知识形态**（Ⅰ锚定/Ⅱ体系/Ⅲ常识） | CATALOG:23–27 | —— | ❌ **无字段** |
| **素材四级**（A/B/C/D） | 手册 B9，CATALOG:11 | 只在 `source.registry.v1` 侧 | ⚠️ **半落**，卡上读不到 |

---

## 1. 已合规：领域五类

- 规范：CATALOG:22「领域（固定五类）政治 / 经济 / 文化 / 战争 / 地理 —— 手册 B1 钉死，**不许自创**」。
- 落点：`docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json` 的 `domain` 为五值 enum，
  且另有 `subdomain`（`^[a-z][a-z0-9_]*$`）与 `related_domains`（1–4 个）。
- 实测：448 篇逐篇带 `domain`；`subdomain` 448/448。
- **结论：无需改动。**

---

## 2. 缺口① —— 知识形态三级，编辑器里完全没有

### 2.1 规范说什么

CATALOG:25–27：

- **Ⅰ 锚定**：挂在一个游戏对象上（某王国、某家族、某英雄）。
- **Ⅱ 体系**：不属于任何单一对象（律法、商路、军制、习俗、史事）。
- **Ⅲ 常识**：这个世界每个人天然就知道的（城堡是干什么的、集市怎么开）
  ——「**最容易被忽略，却最影响"像不像一个世界"**」。

CATALOG:29 自己写着：**v1 目录只覆盖了「Ⅰ 锚定」中最窄的一角。Ⅱ 和 Ⅲ 整块漏掉了。**
CATALOG:119 给的铺法是「**Ⅰ→Ⅱ→Ⅲ 三级同铺**，不要只做 Ⅰ」。

### 2.2 编辑器里什么样（取证）

| 取证对象 | 结果 |
|---|---|
| `awake.worldbook.authoring.v1.schema.json` 允许字段 | **21 个**，`additionalProperties: false` |
| 其中有没有「形态」类字段 | **没有**（唯一名字沾边的是 `content_tier`，值是 `base`/`adult_optional`，属**内容分级**不是知识形态） |
| 448 篇里有没有任何一篇写过形态类字段 | **零篇**（`knowledge_form`/`form`/`shape`/`knowledge_kind` 全 0 命中） |
| `tools/worldbook-contract/v1/common.schema.json#/$defs/knowledge_entry` | `additionalProperties: false`，允许项里同样没有形态 |
| 现在能间接区分 Ⅰ 与其余的东西 | 只有 `entity_ids` 的**有无**（81 有 / 367 无） |

### 2.3 这造成什么

**「无锚点」是一个桶，Ⅱ 和 Ⅲ 混在里面分不开。**

- 448 张里 **81 张锚定**（锚点类型 100% 是 `entity.settlement`）；
- **367 张无锚点** —— 这一桶里既有「体系」（律法、商路、军制），也有「常识」（城堡是干什么的）。
- ⇒ 现状**无法回答**「常识层建了没有、建了多少」。CATALOG 把 Ⅲ 单列为一级，编辑器却量不出来。

> ⚠️ 注意分寸：`entity_ids` **不是**形态字段。CATALOG:33–44 明确写过「锚点**可有可无**、
> 是**加分项不是准入项**」——概念类知识直接不写 `entity_ids` 即可。所以**不能**把
> 「有 `entity_ids`」直接等同于「Ⅰ 锚定」，那是我自己的加码。（当前 81/367 这个切法只是**近似**。）

### 2.4 整改方案

**方案 A（推荐）· 新增可选字段 `knowledge_form`**

- `authoring.v1` schema 增一个**可选**属性 `knowledge_form`，enum `anchored|system|common`（即 Ⅰ/Ⅱ/Ⅲ）。
- **对 448 篇零破坏**：`additionalProperties: false` 只禁止*未声明*的字段，声明后不填不报错。
- 编辑器 UI 加一个下拉（三选一），位置放在 `domain` / `subdomain` 旁。
- 编译投影**可先不做**（形态是 authoring 侧元数据，runtime 不需要它）。
- **唯一的人工成本**：448 张要人工标 Ⅱ/Ⅲ（Ⅰ 可由 `entity_ids` 预填）。这属编辑判断，必须过目。

**方案 B · 不改 schema，只在评审投影里派生**

- 编译期派生一个 `form` 写进产物报告：`entity_ids` 非空 ⇒ Ⅰ，空 ⇒ Ⅱ∪Ⅲ。
- 优点：零契约改动。缺点：**Ⅱ/Ⅲ 永远分不开**，等于放弃 CATALOG 的一整根轴。**不推荐。**

**方案 C · 先只做「显示」，不改数据**

- 编辑器评审界面把「锚定 / 无锚点」标出来，但**不新增字段**。
- 这能马上让编辑者看见缺口，但同样分不开 Ⅱ/Ⅲ。可作为方案 A 的**前置一步**。

### 2.5 影响面（方案 A）

| 受影响物 | 改动 |
|---|---|
| `docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json` | ＋1 个可选属性（＋同步 `artifacts/current-test/WorldbookStudio/schemas/` 那份副本） |
| `AuthoringTemplateFactory.cs` | 新档默认值 |
| Studio 前端 ＋ Prefab | 一个下拉控件（**属 UI 线**） |
| 448 篇 YAML | **不必改**（可选）；若要补齐，是**人工分档**，不是机械改写 |
| 测试 | 增一条负向用例（非法 enum 值应红） |

> ⚠️ 这动的是「**知识卡编辑器长什么样**」，属 `ui-design-gate` 管辖范围
> （「游戏 UI 新建、大改及相关资产规划时使用：先线框，按需交互原型，再视觉确认；**用户批准后实施**」）。
> ⇒ 本提案**先不动手**，等确认。这是**项目的规矩压过"尽快落地"的冲动**，特此写明取舍。

---

## 3. 缺口② —— 素材四级：落在来源侧，到不了卡上

### 3.1 规范说什么

CATALOG:11（手册 B9 来源分级）：

> **A** 官方文本/游戏数据 · **B** 编年史 · **C** 模组资料提取 · **D** 开发者原创（填补空白）

CATALOG:6 红线③：**D 类必须登记来源**。
CATALOG:13：**D 不等于编造** —— 它是「官方没写、但这个世界必须成立」的部分（例：货币叫什么、神庙怎么运作）。**须登记、可复核。**

### 3.2 编辑器里什么样（取证）

**（a）来源侧：分级是齐的。**
`source.registry.v1.schema.json` 的 `source_nature` **在必填清单里**，enum 9 值：
`game_snapshot`/`chronicle`/`king_of_calradia_extract`/`mod_extract`/`adult_extension`/`fan_mod`/`future_era`/`under_review`/`developer_original`。

**（b）但实际只有 A、B 两类有人用。**
26 号在册来源的 `source_nature` 分布：

| 值 | 数量 | 对应级别 |
|---|---|---|
| `game_snapshot` | **16** | A 官方文本/游戏数据 |
| `chronicle` | **10** | B 编年史 |
| 其余 7 个枚举值 | **0** | C / D 从未出现 |

**（c）卡这一侧读不到分级。**
`tools/worldbook-contract/v1/common.schema.json#/$defs/source_ref` —— 卡里的 `sources[]` 每一项只允许：

```
required: [source_id, source_hash]
properties: source_id / source_hash / locator      additionalProperties: false
```

⇒ **没有任何位置写「这条引证是 A 还是 D」**。要判断一张卡依据什么，只能拿 `source_id`
去来源登记表**反查**。编译产物里也不带。

- 现状实测（按卡的引用来源反推）：**A 446 张 / B 2 张**（按「引用到的最高优先来源」取）。

### 3.3 这造成什么

1. **"这张卡依据什么"在卡上不可见** —— 编辑器里打开一张卡，看不出它是官方文本撑的，还是我们补的。
2. **更根本的问题不在编辑器，在素材池**：C/D 两级**一条来源都没登记**。
   ⇒ 也就是说「官方没写、但这个世界必须成立」的那一层**还没进入来源登记表**，
   红线③「D 类必须登记来源」目前**无物可登记**。
   ⚠️ 这是**内容生产**的洞，不是字段的洞。**加字段不会让 D 类出现。**

### 3.4 整改方案（按风险从低到高）

| 方案 | 做什么 | 风险 |
|---|---|---|
| **a（推荐先做）** | 编辑器评审界面**显示**每条引证的级别（读来源登记表反查，不改任何契约） | 零 |
| **b** | 编译产物每条 entry 附一个「最高来源级别」字段 | 动 runtime 契约 ⇒ **三边** |
| **c** | `source_ref` 增可选 `source_nature`，把级别**随卡存** | 动 common 契约 ＋ 448 篇引用块 |

**建议**：先做 a（可见即可谈），b/c 等 a 用过一轮再定。
**同时**把「C/D 素材料从哪来」作为**内容侧**的独立议题单列 —— 它与编辑器字段无关。

---

## 4. 附：本次取数中的一个推断，标"待验证"

可视化脚本把 `source_nature` 的 9 个枚举值映到 B9 四级时，用了这张表：

| source_nature | 我映到 |
|---|---|
| `game_snapshot` | A |
| `chronicle` | B |
| `king_of_calradia_extract` / `mod_extract` / `fan_mod` / `adult_extension` | C |
| `developer_original` / `future_era` | D |
| `under_review` | ？ |

- **A / B 两行是直接出自手册**（CATALOG:11 原文），可靠。
- **C / D 那几行是我的推断**（按值名语义归类），**项目里我找不到成文的对照表**。
- ⇒ 标**待验证**。但**不影响本次任何数字**：这 5 个值当前**一条都没有被使用**，
  无论怎么归类，C 与 D 都是 0。

---

## 5. 本轮不做的事（说清楚）

- 不动 `authoring.v1` schema（属 UI 设计门，待批准）。
- 不重写 448 篇 YAML。
- 不新增/修改 runtime 契约。
- 以上三项都在本提案里给出**可执行的改法**，等令。

---

## 6. 相关取证物

| 物 | 位置 |
|---|---|
| 成果看板（可视化） | `tools/worldbook-studio/artifacts/knowledge-system-view-20260915/index.html` |
| 中间数据 | 同目录 `data.json` |
| 取数脚本 | `tools/worldbook-studio/artifacts/_kb_view_data_20260915.py` |
| 字段缺口探针 | `tools/worldbook-studio/artifacts/_kb_gap_probe_20260915.py` |
| 渲染脚本 | `tools/worldbook-studio/artifacts/_kb_view_render_20260915.py` |
