# AWAKE 卡拉迪亚世界知识建设索引 v2

> **已并入唯一权威文档**：`WORLDBOOKSTUDIO-AUTHOR-HANDBOOK-v3-20260910.md`（2026-09-10）。
> 本文保留为历史记录；内容以 v3 手册 B 部分为准，与 v3 冲突时以 v3 为准。

> 日期：2026-09-09  
> 前身：`WORLDBOOK-CALRADIA-KNOWLEDGE-INDEX-20260823.md`  
> 性质：作者生产目录 + 与当前 Studio/Runtime 契约对齐方案  
> 当前阶段：`plan_only`，未改 Runtime、Studio 代码、世界书内容或迁移候选

## 1. 修订原因

旧索引是作者工作指南，方向正确，但没有与当前机器契约完全对齐：

- 旧索引使用“政治、经济、文化、战争”四类；
- 当前 Studio taxonomy 已固定为五类：政治、经济、文化、战争、地理；
- 旧索引的“时期/范围/身份可见性”没有落到 schema 字段；
- 旧索引把“建设优先级”与“内容状态”混在一起；
- 旧索引没有规定“一个索引条目应该拆成几条 authoring document”；
- 旧索引没有把官方中文本地 XML 映射到 source registry。

## 2. 统一世界基础

### 2.1 当前工作层

- 默认宇宙：`bannerlord_1084`
- 默认时代：约 1084 年霸主时代书签
- 非当前知识必须标记：
  - `era.key=historical`
  - 或 `era.key=current` 但 `start_year/end_year` 明确限位

### 2.2 知识领域

固定五类：

```text
politics
economy
culture
war
geography
```

### 2.3 资料优先级

| 优先级 | 来源 | 用途 | 当前登记建议 |
|---|---|---|---|
| A | Bannerlord 游戏数据与官方本地化文本 | 对象存在、名称、文化、身份、聚落、王国、兵种 | `game_snapshot` |
| B | 卡拉迪亚编年史 | 历史、人物背景、政治和文化叙述 | `chronicle` |
| C | 卡拉迪亚之王资料提取 | 国家、文化、宗教、制度、事件、地点、军事资料 | `mod_extract` |
| D | 开发者原创裁定 | 解决冲突、填补空白、项目独有逻辑 | `developer_original` |
| E | 战团未来、CK3、成人拓展、未核实素材 | 可选、未来或扩展层 | `future_era` / `adult_extension` / `under_review` |

任何进入正典的 source 必须登记到 source registry，且 `license_status`、`use_status`、`source_content_hash` 可核验。

## 3. 一条知识的形态

### 3.1 领域

每条知识必须先选定唯一 `domain`：

- 自然地形、聚落位置、方位、河流、道路走向 → `geography`
- 主权边界、聚落行政、国家、官职、法律、继承、外交、家族 → `politics`
- 土地生产、粮食、交易、税收、物价、商路经营 → `economy`
- 习俗、语言、信仰、荣誉、婚姻、葬礼、节庆、偏见 → `culture`
- 军制、战争、兵种、战役、防御、征召 → `war`

### 3.2 时间维度

必须落到 `era`：

```text
current
historical
pre_war
during_war
post_war
long_term
unknown
```

`current_and_historical` 只用于叙述性过渡，不应作为正式存档值。

### 3.3 范围维度

旧索引的“本地、本国、跨国、贵族圈、军队、私人、秘密”应作为：

- 表达层 `layer`；
- `knowledge_rule.scope`；
- 或文档检索 metadata。

不新增一个含义模糊的 `scope` 字符串，避免与 `expression` 的 `scope` 冲突。

### 3.4 内容状态

与建设优先级分开：

| 内容状态 | 含义 | 是否进入 Runtime |
|---|---|---|
| `canon` | 已批准当前正典 | 可 |
| `needs_review` | 待人工复核 | 否 |
| `reference_only` | 参考层，不进入 Runtime 正典 | 否 |
| `rumor` | 传闻层 | 仅受控表达 |
| `future` | 未来/扩展层 | 否 |

### 3.5 建设优先级

```text
p0
p1
p2
```

P0/P1/P2 只表示建设顺序，不表示内容正确或正典。

## 4. 知识条目边界规则

这是 v2 最重要的补充：**一个作者主题不必然等于一个 document。**

### 4.1 必须拆开的主题

以下内容不能写进同一条简介：

```text
地点简介
地点历史沿革
地点当前政治归属
相关家族关系
相关战争事件
宗教或文化解释
```

### 4.2 帕拉汶德最小条目拆分

推荐至少拆成：

```text
doc.geography.pravend
  - 当前名称
  - 历史名称/别名
  - 聚落类型
  - 北部平原关系

doc.politics.pravend-succession
  - 奥斯里克入侵
  - 与元老协商投降
  - 戴·提尔家族旁支传承

doc.politics.pravend-historical-name
  - 巴拉维诺斯旧名
  - 建立与首都关系
  - 帝国重心东移后的西部经济重镇地位
```

若正式 Entity Registry 中已有稳定 `entity.settlement.pravend`，再单独建立 alias/redirect：

```text
aliases:
  zh-CN:
    - 巴拉维诺斯
redirects:
  - from: doc.geography.balavinos
    to: doc.geography.pravend
```

## 5. Assertion 类型边界

当前 schema 已有五种 `assertion.kind`：

```text
fact
interpretation
rumor
relation
state
```

帕拉汶德映射：

| 内容 | 推荐 kind |
|---|---|
| 巴拉维诺斯由卡拉狄乌斯大帝建立 | `fact` |
| 后取代沙拉斯成为首都 | `fact` |
| 帝国重心东移后仍是西部经济重镇 | `state` |
| 奥斯里克入侵后与元老协商投降 | `fact` |
| 传承到戴·提尔家族旁支 | `relation` |
| 现名是帕拉汶德 | `fact` |
| 弗雷吉昂位于帕拉汶德北部平原 | `fact` |

不得把“瓦兰迪亚入侵是正义的”写成 `fact`；若必须保留，应标为 `interpretation` 或 `rumor`。

## 6. 来源登记

### 6.1 官方中文 XML 的定位

`SandBox/ModuleData/Languages/CNs` 和 `SandBoxCore/ModuleData/Languages/CNs` 属于：

- 官方游戏本地化文本；
- 可用于确认对象存在、官方名称、文化/地点/历史叙述；
- 不是自动可导入的 Studio authoring 文档；
- 必须摘录成带 locator 的 source 后，才进入 source registry。

### 6.2 官方中文与项目裁定的关系

```text
官方中文译名优先用于显示；
官方叙述冲突时，由项目主编裁定；
开发者裁定必须标 source_nature=developer_original；
同一对象的名称、别名和 redirect 必须保持稳定。
```

## 7. 身份表达与知识可见性

每条 expression 必须明确：

```text
layer: rumor|summary|detail|secret
grants:
  - profile_id
  - scope
  - min_detail
denies:
  - profile_id
  - scope
fallback_referral_ids
```

不允许只写“三种身份表达”而不指定 profile 和 layer。

## 8. 生命周期

世界知识必须考虑：

```text
revision
supersedes
split_from
merged_into
event_id
valid_from/valid_until
```

旧名称应通过 `aliases` 或 `redirects` 保留，不直接删除或改写同一条 `id`。

## 9. 帕拉汶德首批建设清单

### P0

1. `doc.geography.pravend`：帕拉汶德是瓦兰迪亚相关区域的一座城镇/聚落。
2. `doc.politics.pravend-historical-name`：巴拉维诺斯旧名及其沿革。
3. `doc.politics.pravend-succession`：奥斯里克入侵后的投降与戴·提尔家族旁支传承。

### P1

4. 帕拉汶德与弗雷吉昂的北部平原空间关系。
5. 帕拉汶德当前政治归属的公开层表达。

### P2 / 暂不进入

6. 戴·提尔家族完整谱系。
7. 奥斯里克人物独立条目。
8. 瓦兰迪亚入侵战争全事件。

## 10. 不写入当前正史

- 帕拉汶德的动态游戏所有权；
- 未经游戏数据支持的正式实体 ID；
- 精确建立年份；
- 戴·提尔家族未在官方文本中出现的私人关系；
- 当前战争结局；
- 任何未登记的 AI 生成事实；
- 旧四版成人化世界书内容；
- 战团未来、CK3 扩展和未核实素材。

## 11. 最终工作原则

```text
先按五领域归类，再拆条目边界；
先登记官方来源，再生成作者档案；
先写 fact/state/relation，再写 interpretation/rumor；
先做地理聚落简介，再做政治沿革和家族关系；
先完成 P0 骨架，再补 NPC 本地生活表达。
```

## 12. 本轮状态

- 旧框架已重新梳理；
- 尚未修改 Runtime、Studio 代码或真实世界书；
- 尚未把官方中文 XML 批量导入 source registry；
- 尚未生成帕拉汶德 AI 候选；
- 下一步应先建立“帕拉汶德官方摘录 source registry + 三条文档骨架”作为离线参考，不做任何正典或 Runtime 写入。

### 12.1 帕拉汶德离线参考簇（已落地，2026-09-09）

- 参考夹具：`tools/worldbook-studio/tests/fixtures/official-reference/pravend-cluster/`（三 authoring 文档 + 两条 source registry + 官方中文摘录 txt）。
- 通过 CLI `validate`：`Valid=true`，`Diagnostics=[]`。
- 说明文档：`pravend-codex-reference.md`；本地校验脚本：`tools/worldbook-studio/_tmp/validate-pravend-reference.ps1`。
- 仍为 `reference_only`；未生成 AI 候选；未改迁移候选、世界书正文、Runtime 或游戏目录。
