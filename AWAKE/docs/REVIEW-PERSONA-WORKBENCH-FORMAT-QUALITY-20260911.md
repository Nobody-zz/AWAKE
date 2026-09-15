# REVIEW · Persona Workbench 格式与质量反思

- 日期：2026-09-11
- 范围：`persona-workbench.character.v1` 卡格式；`AWAKE\tools\persona-workbench\characters\` 下 10 张角色卡
- 依据：本轮创作 + 归属校核 + 官方简中（CNs）译名核查中暴露的实际问题
- 关联契约：`persona-contract\persona-workbench-to-awake.crosswalk.v1.schema.json`、`awake.persona.definition.v1.schema.json`、`awake.persona.authoring.v2.schema.json`

---

## 一、格式层缺陷（schema）

### 高优先级

**1. 缺少机器可校验的归属位 —— 本轮 3 次错误的共同根因**

- `hero id / kingdom id / clan id` 只以自由文本形式出现在 `identityFacts`，或作为括号备注藏在 `sourceDescription`。
- 后果：加里俄斯 北/西 混淆、奥列克 父子混淆、墨速宜 与可汗混淆，全部只能靠人肉对照 `heroes.xml` / `spkingdoms.xml` 事后拦截，schema 本身无力阻止。
- 现有 crosswalk 已有 `evidence_quote`、`provenanceFields`，但那是"导出到 AWAKE"时的追溯；**源头创建阶段缺少结构化的 `origins` 引用位**，校验无法前置。

### 中优先级

**2. 同一人格用三套词汇表表达且不对齐**

`tags`（`trait.ambitious`）→ `facetStrengths`（键 `trait.ambitious`）→ `traitProfile`（裸词 `ambition`）。
同一维度三种写法，schema 未绑定为同一轴，编辑时会漂移。三者应由单一轴定义表派生。

**3. 数值轴无 min/max/语义约束**

`traitProfile`（6 项）、`expressionProfile`（5 项）、`behaviorProfile`（6 项）轴数不一，取值有负数（`caution: -1`），与"27 轴人格体系"的对应关系无 schema 描述，越界时无法拦截。

**4. 版本概念双轨**

`schemaVersion`（persona-workbench.character.v1）与 `templateVersion`（persona-load.v2）一个是卡格式、一个是运行时 DSL，区别未文档化，误用风险高。

**5. `status` 单一取值**

只有 `draft`。无法表达"已按官方译名校核""待实机验证""已批准"等真实需要的状态机。

### 低优先级

**6. 中长文本内嵌 `\n` 转义** —— `core` 中 `\n\n` 手工换行，不可读、易手改坏。
**7. 卡内全中文，不引用官方 i18n key** —— "马凯布"需人肉 grep 语言文件才能对上；卡片若持有 `{=...}` key，译名核查可自动化。
**8. 重复表达** —— `facetStrengths` 与 `tags` 大体重叠；`identityFacts` 与 `core` 首段高度重复，无"事实 vs 展开"的边界契约。

---

## 二、质量层缺陷（内容）

| # | 问题 | 说明 |
|---|---|---|
| Q1 | 模板感趋同 | 四段式骨架 + `priorityOrder` 一律"X > Y > 个人地位 > 一切"，10 张连读疲劳 |
| Q2 | 矛盾点符号化 | 普遍"声称…却…"反讽腔，缺具体事件与人际张力快照 |
| Q3 | 缺"铁证 vs 补写"标注 | 哪些是游戏内明文、哪些是合理延展，无法区分——对"不得胡编"的要求是硬伤 |
| Q4 | 跨卡一致性未全局校验 | Olek–朗瓦德、蒙楚格–墨速宜、加里俄斯–阿庇斯·瓦罗斯等关系，单卡自洽但双卡互证未跑 |
| Q5 | 成文小瑕疵 | 长句偏西化、个别词复用（如"打抱不平"），未跑文字清单 |

---

## 三、流程层缺陷

- **创作顺序倒置**：先凭素材/记忆起草、后补校核 —— 加里俄斯即因此出错。应反过来：先锁定 `origins`，再写身份文本。
- **译名核查是事后人工 grep**：官方译名散在 6 个模块的 CN 文件，应固化为查表（见下）。
- **缺门禁**：无"归属校验 / 译名校核 / 跨卡一致性"三道自动门禁卡在保存/导出前。

---

## 四、`origins` 校验位设计规格（自 rev01）

在 `persona-workbench.character.v1` 顶层新增（与 crosswalk 的 `evidence_quote`/`provenance` 语义衔接）：

```json
"origins": {
  "type": "object",
  "required": ["heroId", "kingdomId"],
  "properties": {
    "heroId":    { "type": "string", "pattern": "^(lord|dead_lord)_[0-9A-Za-z_]+$" },
    "kingdomId": { "type": "string", "enum": ["empire", "empire_w", "empire_s",
                  "sturgia", "aserai", "vlandia", "battania", "khuzait"] },
    "clanId":    { "type": "string" },
    "seatSettlementId": { "type": "string" },
    "parentHeroId":     { "type": ["string", "null"] },
    "spouseHeroId":     { "type": ["string", "null"] },
    "knownAliases":     { "type": "array", "items": { "type": "string" } }
  }
}
```

**校验规则（前置到保存/导出）**
1. `heroId` 必须存在于 `heroes.xml`；
2. `kingdomId` 必须等于 `spkingdoms.xml` 中该 hero 所在王国 owner（统治者为该王国时）；
3. 若 `clanId` 提供，必须与 `heroes.xml` 中 hero 的 `faction` 一致；
4. 命名空间 `id` 的阵营段（如 `calradia.monchug.khuzait` 的 `khuzait`）应与 `kingdomId` 对齐；
5. 通过校验才能进入 `authoring`/`definition` 导出（纳入 crosswalk `rows` 的 `provenance`）。

> 落地方式：编辑器的"保存前校验"与 `tools` 下新增的归属稽查脚本共用一份映射表（heroes/spkingdoms/spclans/settlements 解析结果）。

---

## 五、官方中文译名对照表（官网 CNs 固化）

来源：`Modules\*\ModuleData\Languages\CNs\std_*_xml-zho-CN.xml`（SandBox / Native）。

**机器可读单一事实源**：`docs\mappings\persona-names-zh-en.tsv`（27 行，8 列，制表符分隔，列结构对齐 `settlement-names-zh-en.tsv`）。本表此前内嵌的版本已作废，后续扩展与稽查一律以该 TSV 为准。

固化过程中修正的两点：

- **先王之英文原拼**：巴旦尼亚前任至高王英文名为 `Aeril`（埃里尔），非旧稿"Erran"；卡拉多格家族英文为 `fen Gruffendoc`（芬·格鲁芬多克），已有卡正确。
- **阿庇斯·瓦罗斯为官方英雄（已确证）**：`lord_1_9`，属西帝国民粹派家族 `clan_empire_west_2`。官方文本（key `xL95EfpQ`）明载其为"旧帝国元老院中最臭名昭著、放荡无礼而又最富有的成员之一"，与加里俄斯早年结盟（"加里俄斯提供声望，阿庇斯提供资金"）。garios 卡"早年与之结为同盟"一句**有官方依据，予以保留**；TSV 此前"补写人物/建议移除"的标注系误判，已更正为准确条目。

> 说明：`Makeb` 官方译名位于 **Native** 模块 `std_common_strings`，不在 SandBox；此前卡内误写"梅可布"已按此更正。

---

## 六、推荐执行顺序

1. **schema 升级**：加 `origins`（第 4 节规格）+ 数值轴 min/max + `status` 枚举 —— 收益最高、风险最低。
2. **归属稽查脚本**：解析 heroes/spkingdoms/spclans/settlements，对 10 张卡跑一遍，导出对照表供编辑器复用。
3. **译名查表固化**：以上表为初始数据，接入稽查脚本 / 编辑器，杜绝自造音译。
4. **跨卡一致性第二轮**：Olek–朗瓦德、蒙楚格–墨速宜、加里俄斯–阿庇斯·瓦罗斯 双卡互证。
5. **内容去模板化**（可选，非阻塞）：priorityOrder 去占位感、矛盾点补具体事件、补"铁证/补写"标注。
6. **文字清单**：西化长句与用词复用过一遍。

---

## 状态

- [x] origins 校验位落地（10 张卡内嵌；契约 `persona-workbench.character.v1.schema.json` 已建）
- [x] 归属稽查脚本（`tools/audit-character-affiliations.ps1`，读卡内 origins，10/10 通过）
- [x] 译名查表固化（`docs/mappings/character-names-zh-en.tsv` 397 条 + kingdom + persona + settlement）
- [x] schema 数值轴 min/max 全量校验通过（`tools/audit-character-schema.ps1`，10/10 PASS）
- [x] 跨卡一致性第二轮（Olek–朗瓦德、蒙楚格–墨速宜 互证一致；加里俄斯–阿庇斯·瓦罗斯确证为官方英雄 `lord_1_9`，含一处误判更正，见 `docs/AUDIT-CROSS-CARD-CONSISTENCY-20260911.md`）
- [x] 内容去模板化（priorityOrder 10/10 去除"…> 一切"占位、改为角色特化取舍；contradictionDescription 9/10 去除"他声称…却…他声称…但…"三连模板、绑定具体事件与人物张力；monchug 保留其高密度转折 signature，不以"声称"模板计。文字 lint 复测：D 模板腔"声称"计数整体从 ×2~4 降至 0~1，A/B 硬伤保持清零，schema 10/10 PASS）
  - 附注（Q3 铁证/补写标注未落地、留待后续）：本轮有意未在卡内新增 attribution 字段——`schema.additionalProperties=false` 下加字段会连锁改动 schema + lint + 编辑器，属 schema 级演进而非文本级。现状覆盖：人物/地名隶属已由 `origins` + `persona-names-zh-en.tsv` 做到可追溯；未覆盖的是"叙述性观点/事件"级的铁证-延展区分。推荐后续字段 `attributions: [{claim, basis:"game"|"extend", key?}]`，随 version bump 一并设计。
- [x] 文字清单（`tools/audit-character-text.ps1` + `docs/AUDIT-TEXT-QUALITY-20260911.md`；A/B 硬伤已清零，C/D/E/F 留待人工复核）