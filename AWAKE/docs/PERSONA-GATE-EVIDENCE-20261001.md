# 角色卡质量门 · 红绿证据（2026-10-01）

门：`AWAKE/tools/persona-card-gate.py`（16 条判据：R1–R9 与 R13–R15 是本门自有的硬判据，R10–R12 委派给外部判据脚本见 §10，R16 是**告警**、不判死）
证据等级：**E2（离线测试）** —— 未涉及游戏内验证，本门也不声称游戏内生效。
配套规格：[`PERSONA-CARD-SPEC-v1-20261001.md`](PERSONA-CARD-SPEC-v1-20261001.md)（巡检会话，另写；关系见 §7）

---

## 一、十六条判据

| # | 规则码 | 判据 | 类型 | 老卡 76 | 新卡 279 |
|---|---|---|---|---|---|
| R1 | `PERSONA_CORE_TOO_SHORT` | `len(core) ≥ 150` | 阈值 | 0 | **279** |
| R2 | `PERSONA_CORE_UNTERMINATED` | `core` 末尾须是终止标点 | 不变量 | 0 | **262** |
| R3 | `PERSONA_TEMPLATE_LOAD` | 边界字段句子被 ≥5 张卡共用者占比 ≤5% | 阈值 | 0 | **262** |
| R4 | `PERSONA_TAG_STAMP` | 最大同标签集合占比 ≤10% | 阈值 | 0 | **1**（235/279 = 84%） |
| R5 | `PERSONA_KIN_NOT_A_NAME` | 「已知亲属：」条目剥角色词后须是 2–5 字纯名字 | 不变量 | 0（该字段不存在） | **117** |
| R6 | `PERSONA_ID_PATTERN` | `id` 匹配 `^calradia\.[a-z0-9_]+(\.[a-z0-9_]+){1,2}$` | 不变量 | 0 | **279** |
| R7 | `PERSONA_TAG_UNREGISTERED` | 每个 tag 须在 `tag_registry.json`（39 个）中 | 不变量 | 0 | **276** |
| R8 | `PERSONA_FIELD_SHAPE` | `realSelfBehaviors` **存在时**须是数组 | 不变量 | 0 | 0 |
| R9 | `PERSONA_TAG_COUNT_STAMP` | 每卡 tag 数须有 ≥4 种取值且单值占比 ≤50% | 阈值 | 0 | **1**（1 种取值，100%） |
| R10 | `PERSONA_CROSS_CARD_DUPLICATE` | 边界条目（三轴 / `selfClaimRules` / `realSelfBehaviors`）出现在 >2 张卡即违规 | 阈值 | 0 | **262** |
| R11 | `PERSONA_SUMMARY_COPIED_INTO_EXAMPLE` | 整条 `summary` 不得是自身某条例句的子串 | 不变量 | 0 | **262** |
| R12 | `PERSONA_SPEAKER_LABEL_PREFIX` | 例句不得以「本名（…家）」开头 | 不变量 | 0 | **262** |
| R13 | `PERSONA_PLACEHOLDER_TEXT` | 卡内**任何**字符串不得含未填模板标记（`（…heroId）`/`{…}`/`${…}`/`<…>`/TODO/TBD/FIXME/XXX/待填/待补/占位） | 不变量 | 0 | **8** |
| R14 | `PERSONA_SUMMARY_DERIVED_FROM_DESCRIPTION` | `summary` 不得是自身 `publicDescription`/`privateDescription` 的**前缀延长**（原文与剥掉「对外，/私下里，」标签两种形态都比） | 不变量 | 0 | **17** |
| R15 | `PERSONA_PROFILE_KEY_NONCONFORMANT` | 存在的 profile 字典（`traitProfile`/`expressionProfile`/`behaviorProfile`/`reactionProfile`/`commitmentProfile`）键名须**精确等于**规范键集 | 不变量 | 0 | **68**（17 卡 × 4 套） |
| R16 | `PERSONA_FACET_MIRRORS_TAGS` | `facetStrengths` 键集不得**等于** `tags`（好卡是严格超集） | **告警** | 2 | 17 |
| — | `PERSONA_CRITERIA_UNAVAILABLE` | 判据脚本加载失败（**门级**失败，不挂在某张卡上） | 不变量 | 0 | 0 |

R10–R12 的规则逻辑**不在本门里**：本门只应用阈值，规则实现在
`AWAKE/tools/persona-workbench/tools/measure-three-criteria.py`（巡检会话产出，详见 §10）。

R13–R15 是**本门自有的局部判据**：纯单卡结构检查、无跨卡状态、与语料规模无关，因此**不受
`--min-population` 影响**（20 张以下也照样判）。R16 只打印 `WARN` 与 `PERSONA_GATE_WARNINGS`，
**从不改变退出码**，除非显式传 `--fail-on-facet-mirror` 把它提成硬判据。

红批次合计 **2356 条违规 / 279 张卡**（1477 + 262×3 + 8 + 17 + 68），另有 **17 条告警**。

### R6 与 R7 为什么最重

这两条**不是口味问题**：

- **R6** 是作者侧 schema 的硬 pattern。老卡写 `calradia.ulbos.empire.south`（全小写、3 段），新卡写 `calradia.Ulman.khuzait`（**首字母大写**）——279/279 全中。
- **R7** 有运行时后果。`AWAKE/src/PersonaDslGenerator.cs:123` 起：

  ```
  123   bool expanded = registry.TryExpand(directTagIds, null, out tags, out warnings);
  127   if (!expanded || hasConflict)
  131       result.UsedLegacyFallback = true;
  132       result.Dsl = BuildLegacyFallback(context, legacyPersonality, legacyBackground, maximumBytes);
  ```

  tag 没注册 ⇒ `TryExpand` 返回 false ⇒ **整张卡丢掉人格 DSL，退回 legacy 兜底**。即 276/279 张卡在游戏里根本不会用自己的性格。

---

## 二、阈值来源：校准，不是猜

标定线 = **git 是否跟踪**（76 张已入库＝人工过审；279 张未跟踪＝批量生成）。

| 判据 | 好（76） | 坏（279） | 阈值 | 判别力 |
|---|---|---|---|---|
| `core` 长度 | min **187** / 中位 285 / 均 304 | min 26 / 中位 48 / 均 54 / **max 129** | **150** | ★★★ 分布**零重叠** |
| `core` 终止标点 | 违规 0% | 违规 94% | 不变量 | ★★★ |
| 跨卡样板句负载 | 均 0.000，零样板 76/76 | 均 0.600，零样板 17/279 | 5% | ★★★ |
| 最大同标签集合 | 4/76 = 5.3% | 235/279 = 84% | 10% | ★★★ |
| 每卡 tag 数分布 | 8 种取值，最大 29/76 = 38% | 1 种取值，100% | ≥4 种且 ≤50% | ★★★ |
| `identityFacts` 亲属条目 | 该字段不存在 | 117/409 条不是名字 | 不变量 | ★★ |

> `core` 长度那条**可证明安全**：好卡最小 187 与坏卡最大 129 之间有一道 58 字空档，阈值落在空档内任何位置都 100% 分对两侧。**没有猜。**

---

## 三、红 → 绿一对（实测）

### 绿：76 张已入库

```
PERSONA_GATE_REGISTRY tags=39
PERSONA_GATE_SUMMARY total=76 failed_cards=0 violations=0
PERSONA_GATE_GREEN
exit=0
```

### 红：279 张未跟踪

```
FAIL PERSONA_CORE_TOO_SHORT     count=279
FAIL PERSONA_CORE_UNTERMINATED  count=262
FAIL PERSONA_KIN_NOT_A_NAME     count=117
FAIL PERSONA_ID_PATTERN         count=279
FAIL PERSONA_TEMPLATE_LOAD      count=262
FAIL PERSONA_TAG_STAMP          count=1
     largest tag set 235/279 (84%)
     tags=behavior.calculating,expression.cold,trait.pragmatic,trait.ruthless,trigger.family_interest
FAIL PERSONA_TAG_UNREGISTERED   count=276
FAIL PERSONA_TAG_COUNT_STAMP    count=1
     distinct=1 (min=4) largest=5 tag(s) x279 (100% max=50%)
FAIL PERSONA_CROSS_CARD_DUPLICATE            count=262
FAIL PERSONA_SUMMARY_COPIED_INTO_EXAMPLE     count=262
FAIL PERSONA_SPEAKER_LABEL_PREFIX            count=262
FAIL PERSONA_PLACEHOLDER_TEXT                count=8
     ..._Tynops_elaches_empire_w.persona.json :: $.identityFacts=…… heroId）
FAIL PERSONA_SUMMARY_DERIVED_FROM_DESCRIPTION count=17
     ..._Tynops_elaches_empire_w.persona.json :: summary starts with publicDescription (60 chars)
FAIL PERSONA_PROFILE_KEY_NONCONFORMANT       count=68
     ..._Tynops_elaches_empire_w.persona.json :: behaviorProfile keys=6 expected=6
          extra=cond,delib,ing,lead,lev,trust
          missing=conditionality,deliberation,inGroupPriority,leadership,leverage,trustTesting
WARN PERSONA_FACET_MIRRORS_TAGS count=17
PERSONA_GATE_WARNINGS total=17
PERSONA_GATE_SUMMARY total=279 failed_cards=279 violations=2356
PERSONA_GATE_RED
exit=1
```

（`FAIL` 行的中文原文见 `--report` 输出的 UTF-8 文件；stdout 里的 `?` 是 Windows 控制台按 GBK 解码所致，非门的问题。）

**三条新硬判据只咬漏检的那 17 张**：R13 命中 8 张、R14 命中 17 张、R15 命中 17 张×4 套 = 68 条，
**262 张坏卡一张没误伤**，76 张好卡零误报。

**红在对的理由上**：每条判据各自报出错误码与计数，不是靠「进程非 0」这种无关信号。

---

## 四、变异检验 ＋ 阴性对照矩阵（技能 §4）

取一张已入库好卡 `乌尔玻斯_ulbos_pethros_empire_s.persona.json`，基线 sha256 = `F91DFAF2C4830DBB4B14D9A3CE754A492A48276496FC6B8A3B71878B6A1EE28D`。
每次变异后**按字节还原**并复校 sha256，最终逐字节相同。

| 用例 | 动作 | 期望 | 实测 exit | 触发的判据 | 判定 |
|---|---|---|---|---|---|
| baseline | 无 | GREEN | 0 | — | ✅ |
| core-trunc | `core` 截到 50 字 | RED | 1 | `CORE_TOO_SHORT`, `CORE_UNTERMINATED` | ✅ |
| id-caps | 名段首字母大写 | RED | 1 | `ID_PATTERN` | ✅ |
| tag-unreg | 追加未注册 tag | RED | 1 | `TAG_UNREGISTERED` | ✅ |
| shape-dict | `realSelfBehaviors = {}` | RED | 1 | `FIELD_SHAPE` | ✅ |
| **key-absent** | **删掉该键** | **GREEN** | **0** | — | ✅ **阴性对照** |
| placeholder | `identityFacts` 追加 `（游戏内配偶 heroId）` | RED | 1 | `PLACEHOLDER_TEXT` | ✅ |
| derived | `summary := publicDescription + "。补充说明。"` | RED | 1 | `SUMMARY_DERIVED_FROM_DESCRIPTION` | ✅ |
| profilekey | `behaviorProfile.conditionality` 改名 `cond` | RED | 1 | `PROFILE_KEY_NONCONFORMANT` | ✅ |
| **facetmirror** | **`facetStrengths := {tag:1}`** | **GREEN＋告警** | **0** | 仅 `WARN FACET_MIRRORS_TAGS` | ✅ **阴性对照** |
| facetmirror-strict | 同上 ＋ `--fail-on-facet-mirror` | RED | 1 | `FACET_MIRRORS_TAGS` | ✅ |
| restored | 还原 | GREEN | 0 | — | ✅ |

**8/8 ALL_PASS**，源卡 sha256 前后完全相同（`f91dfaf2c4830dbb…`）。

**变异抓到了判据自己的洞（值得记一笔）**：R14 第一版只拿「剥掉 `对外，` 标签后」的描述去比前缀，
而 `乌尔玻斯` 的 `publicDescription` 恰好以 `对外，` 开头（83 字）。变异把**带标签的原文**拼进
`summary` 时，判据**没有红** —— 同一个病、两种拼法，我只堵了一种。改成**原文与剥标签两种形态都比**
之后才红。这正是「先红在对的理由上」这条纪律的价值：红测（279 张坏卡）当时是**通过**的，
洞只有变异才照得出来。

**`facetmirror → GREEN＋告警` 是 R16 的阴性对照**：证明「告警」这个设计是真的不判死，
不是嘴上说说。

**`key-absent → GREEN` 是关键的阴性对照**：它证明 R8 对「键缺失」的豁免是**有据的**（8/76 张好卡本来就缺该键），不是门上的窟窿。
**每条新判据（R6/R7/R8）都至少被变异打红过一次**——满足「一道从未红过的门不是门」。

---

## 五、判据体检（技能 §3：不可失败门的四种形态）

| 形态 | 本门是否中招 | 依据 |
|---|---|---|
| 恒定退出码 | **否** | 实测 `exit=1`；`main()` 按 `failures` 是否为空返回 1/0，无 `exit 0` 兜底分支 |
| 只比字符串 | **否** | 全部判据**重算**：长度、句级出现卡数、标签集合频次、正则匹配、注册表成员、形状 |
| 依赖被 gitignore 的产物 | **否** | 只读 `characters/*.persona.json` 与 `tag_registry.json`（均已入库） |
| 判据用错入口 | **否** | 直接读卡 JSON，不经过任何「验证器」包装 |

---

## 六、本次新发现的缺陷（老批次，规格稿标反了）

规格稿 §7 矩阵把 B4（`realSelfBehaviors` 形状）标成「老卡 ✅ / 新卡 ❌ 8」。**实测批次标反了。**

| 层 | 实测 |
|---|---|
| 角色卡侧（355 张） | **8 张已入库好卡**省略该键（合法：角色卡 schema 的 `required` 里没有它） |
| 物化产物侧（78 个 `.definition.json`） | **同样这 8 个角色**的产物是 `realSelfBehaviors = {}`，**违反 definition schema** |

涉事 8 人：`berican_dey_rothad_vlandia`、`luichan_fen_penraic_battania`、`ingalther_dey_cortain_vlandia`、`aeron_fen_giall_battania`、`pryndor_fen_morcar_battania`、`melidir_fen_uvain_battania`、`aldric_dey_tihr_vlandia`、`aradwyr_fen_eingal_battania`。

依据：
- 角色卡 schema `AWAKE/tools/persona-workbench/contracts/persona-workbench.character.v1.schema.json:6` 的 `required` **不含** `realSelfBehaviors`。
- 定义 schema `AWAKE/docs/persona-contract/awake.persona.definition.v1.schema.json:7` 的 `required` **含** `realSelfBehaviors`（第 27 行），类型 `$ref: #/$defs/textList`（第 53 行）。

**根因**：物化器在源卡缺该字段时输出空对象。**这是老批次的既有缺陷，不是新批次引入的。**
修复方向二选一：① 给这 8 张源卡补上 `realSelfBehaviors` 并重跑物化；② 物化器改为输出 `[]`。

---

## 七、与 `PERSONA-CARD-SPEC-v1-20261001.md` 的关系

两份东西**同源不同形**：同一批 76/279 基线，独立推出。规格稿是**可计算规范**（17 条，T/B/N/I/F/S 系列，不含可运行脚本，判定脚本列指向既有 `.ps1`）；本门是**已跑通红绿的可执行门**（9 条）。

**重叠且一致**（双方独立测得同一数字）：跨卡样板句负载（其 B1 ≡ 本门 R3，阈值同为 5%）、最大同标签集合 235 张（其 T1 ≡ 本门 R4）、`id` pattern（其 I1 ≡ 本门 R6，同为 279）、未注册 tag（其 T5 ≡ 本门 R7，同为 276）、叙述逐字重复 1 对、显示名重复 1 组。

**规格稿有、本门暂无**：T2（已由 R9 覆盖）、T4 单 tag 占比 ≤70%、T6 同义跨类目 5 组、B2 单句跨卡 ≤5 张、B3 骨架句 ≤3 张、N1 正文字数、N2 样本条数分布、N3 逐字唯一、I2 `characterId` pattern（76/76 违规，**老卡也有**）、F1 物化新鲜度（73/76 过期，**运行时用的是早 4 天的快照**）。

**本门有、规格稿无**：R2（`core` 未终止标点，262 张，规格稿完全没这条）、R5（`identityFacts` 亲属条目不是名字，117 条，**这正是用户抱怨的「家族信息对不上」**）、以及 R1 的**可证明阈值**（规格稿的 N1 明确标注「待标定」，因为它没测老卡字数下界；本门测了：好卡 min=187）。

**规格稿有一处阈值缺陷**：T1 硬门写作「≤4 张 **且** ≤5%」。老卡实测 4/76 = **5.26% > 5%** ⇒ **按字面执行会把好批次判红**。本门 R4 取 10%，对老卡留 4.7 个百分点的余量。

**未做**：未改动规格稿一个字节；两者的合并（把本门接进其 §7 矩阵的「判定脚本」列）应由甲方裁定。

---

## 八、已知局限

1. **R3 需要 ≥6 张卡**（`--share-threshold + 1`）才生效；**R4/R9 需要 ≥20 张**。小样本下自动跳过并打印 `PERSONA_GATE_NOTE`。
2. **R5 只覆盖「已知亲属：」这一种表述**。其他写法（「其父为X」「X之子」）不在判据内。
3. **R8 在当前样本上零判别力**（0/355 违规），只是不变量，不承担判别。
4. **阈值标定于 2026-10-01 的 76/279 样本**，样本构成变化后须重标。
5. **本门判不了「亲属关系是否写错」**（实测只有 8 张卡能被句式解析出亲属）与**「事实是否有据」**。
6. **本门不覆盖物化产物**（`definition.json` 的形状与新鲜度）——§6 的 8 个 `{}` 与规格稿 F1 的 73/76 过期都在门外。
7. **R10–R12 的规则实现在门外**（`measure-three-criteria.py`）。该脚本已随本门一起入库（提交 `21d5597`）；缺失时本门**直接变红**（`PERSONA_CRITERIA_UNAVAILABLE`），这是有意为之，不是缺陷。
8. **R10–R12 在这批语料上完全相关**：三条命中同一 262 张（并集也是 262），**不能当成三重独立证据**（详见 §10）。
9. **R11 没有摘要长度下限**，它现在零误报只因为最短摘要也有 14 字（详见 §10）。
10. **R13 的占位符正则只认「有界」的标记**：`（…heroId）`、`{{…}}`、`${…}`、`<…>`、`{…}` 五种形态。
    纯中文的「待填/待补/占位」必须**带括号**才命中（这是有意的：正文里裸写「待补」是正常词）。
11. **R14 只比前缀，不比同义改写**。把 `publicDescription` 换个说法再当 `summary`，判据看不出来 ——
    它抓的是「机械拼接」，不是「语义重复」。
12. **R15 只看键集合、不看取值**；且 `traitProfile` 在好/坏/漏检三组上**全部合规**
    ⇒ 该套在本样本上零判别力（它靠变异证明会红，不靠样本）。
13. **R16 是告警**，默认不改退出码；要它判死必须显式 `--fail-on-facet-mirror`。
    它也只认「键集恰好等于 tags」这一种形态（`facetStrengths` 为 tags 真子集或含 tags 之外键时不响）。

---

## 九、本次未做的事

- 未修改任何一张角色卡（变异在临时目录副本上做，已按字节还原）
- 未修改任何 schema、`tag_registry.json`、物化器
- 未把门挂进 `AWAKE/tools/build.ps1`
- 未提交任何文件（本节写作时的状态；实际提交见 §十 末尾，提交 `21d5597`）
- 未做游戏内验证（本门与游戏内无关）
- 未把 C1/C2/C3 的规则逻辑抄进本门（改为委派，见 §10）
- 未给 R11 加摘要长度下限
- ~~未查清帝国三系那 17 张坏卡「另一型病」的具体形态~~ → **已查清，见 §十一**
- 未实跑 `persona-awake-joint` 的 fixture（§十一 的「联合契约拒掉 355 张」因此仍是【审计】，不是【实测】）
- 未裁定 `tensionAxes` 究竟该不该出现在角色卡里（见 §十一 的未决项）
- 未修 279 张卡中的任何一张，也未重做

---

## 十、R10–R12：委派给外部判据脚本（2026-10-01 增补）

### 为什么委派而不是抄进来

巡检会话已把三条判据实现为 `AWAKE/tools/persona-workbench/tools/measure-three-criteria.py`（122 行，`py -3.11` 跑）。用户裁定**不重复实现**：本门用 `importlib` 把它当模块加载，只调它的 `units()` 与 `measure()` 两个函数，阈值留在本门。同一规则只有一份实现，不会两处漂移。

三点接线细节：

1. **不跑它的 `main()`。** 该脚本 `main()` 里有冻结断言 `if (len(good), len(bad)) != (76, 279): raise ValueError(...)`，它是**标定工具**、不是通用门。模块加载不触发 `main()`。
2. **跨卡计数一律在整目录上算**，绝不在 `--include-list` 子集上算。在子集里算，每张卡的 `support` 都会塌成 1，判据永远不可能红——这正是「不可失败门」的第四种形态（判据用错入口）。因此本门**先读全目录（355 张）算读数，再只对受检子集报违规**。
3. **判据脚本加载失败 = 门变红**（`PERSONA_CRITERIA_UNAVAILABLE`），不是静默跳过。要主动放弃这三条必须显式传 `--skip-external-criteria`。

### 独立复跑（复现巡检会话的每一个数字）

`py -3.11 AWAKE/tools/persona-workbench/tools/measure-three-criteria.py` → 退出 0，`head=ab5a3bb`，`corpus_sha256=2369a7d0b79b1841…`。

| 判据 | 好卡 76（min/中位/max，直方图） | 坏卡 279（min/中位/max，直方图） | 命中 |
|---|---|---|---|
| C1 跨卡整条重复 | 1 / 1 / 2，{1:70, 2:6} | 1 / 259 / 259，{1:17, 247:3, 259:259} | 262/279 |
| C2 摘要搬入样本 | 0 / 0 / 0，{0:76} | 0 / 1 / 1，{0:17, 1:262} | 262/279 |
| C3 说话人标签前缀 | 0 / 0 / 0，{0:76} | 0 / 5 / 5，{0:17, 5:262} | 262/279 |

**报告里的每个数字都对上了。**

### 复跑时发现、报告里没说的两件事

**① 三条判据在这批语料上不是三个独立信号。** 三条各自命中 262 张，且**并集也正好是 262** ⇒ 三个命中集合**完全相同**。它们是同一个生成缺陷的三种形状，不是三重独立证据。对未来的批次可能分化，但**用这批数据无法证明它们彼此独立**。

**② 漏检的 17 张全部是帝国系。** 逐张列名后按文化后缀统计：

| 文化后缀 | 坏卡总数 | 其中漏检 |
|---|---|---|
| `empire_n` | 45 | **6** |
| `empire_s` | 34 | **5** |
| `empire_w` | 33 | **6** |
| aserai / battania / khuzait / sturgia / vlandia | 167 | **0** |

⇒ 非帝国的 167 张坏卡**一张不漏**；帝国三系 112 张里漏 17 张（15%）。这 17 张 C1=1、C2=0、C3=0，说明它们的规则/行为/三轴都唯一、摘要没被搬、例句没有标签前缀——**它们的病是另一型**。这 17 张本门的 R1/R2/R6/R7 已全部拦住（红批次 `failed_cards=279`）。

### C2 的标定边界（复跑时实测）

全库 355 张 `summary` 均非空，归一化长度 **min 14 / 中位 33 / max 108**。C2 用的是**无长度下限的连续子串**判据，它现在零误报只是因为最短的摘要也有 14 字。**将来出现 3–5 字的摘要，C2 会误报**，而脚本里没有长度下限。这是一条**未加护栏的标定假设**。

### 本门自己的红绿与变异

- **绿**（76 张已入库）：`PERSONA_GATE_CRITERIA script=… corpus=355` + `total=76 failed_cards=0 violations=0` + `PERSONA_GATE_GREEN`，退出 0。
- **红**（279 张未跟踪）：新增三行 `FAIL PERSONA_CROSS_CARD_DUPLICATE count=262`、`FAIL PERSONA_SUMMARY_COPIED_INTO_EXAMPLE count=262`、`FAIL PERSONA_SPEAKER_LABEL_PREFIX count=262`；`violations` 由 **1477 → 2263**（+786 = 262×3），`PERSONA_GATE_RED`，退出 1。
- **阴性对照**：取好卡 `乌尔玻斯_ulbos_pethros_empire_s.persona.json`，真实语料、受检集合只有它一张 → `total=1 failed_cards=0`，绿，退出 0。
- **变异**：把整目录复制到临时目录（355 张），只在该副本里把这张好卡注入三种缺陷——① 追加一条两张好卡共有的 `selfClaimRules` 条目（`support` 由 1→3）；② `selfClaimExamples[0] := summary`；③ `selfClaimExamples[1]` 前缀加 `乌尔玻斯（测试家）`。跑门 → 退出 1，`violations=3`，且**恰好**是：

  ```
  FAIL PERSONA_CROSS_CARD_DUPLICATE count=1
       … :: shared with 3 cards (max=2)
  FAIL PERSONA_SUMMARY_COPIED_INTO_EXAMPLE count=1
       … :: 1 example(s) carry the whole summary (max=0)
  FAIL PERSONA_SPEAKER_LABEL_PREFIX count=1
       … :: 1 example(s) start with a speaker label (max=0)
  ```

  ⇒ 三条委派判据在本门里**确实会红，且红在对的理由上**。仓库里的角色卡一个字节未动。
- **不可失败门检验**：`--criteria-script <不存在的路径>` → `FAIL PERSONA_CRITERIA_UNAVAILABLE count=1` + `PERSONA_GATE_RED`，退出 1（**不会静默变绿**）；`--skip-external-criteria` → `PERSONA_GATE_NOTE` + 绿，退出 0（主动放弃有明确痕迹）。

### 依赖风险（已知，未消除）

1. **脚本与门必须同一次提交。** `measure-three-criteria.py` 已随本门一起入库（提交 `21d5597`）。本门默认路径指向它，且加载失败即变红 ⇒ 只提交门而不提交脚本，任何一次干净检出上这道门都会红。
2. **`--cards` 本身就定义了「语料」。** R10 的 `support` 是相对「本门看到的那个目录」而不是「全部角色卡」计算的——同一张卡，单独放一个 20 张的目录里跑，和混在 355 张里跑，读数不同。**门外存在重复卡时本门看不出来。** 这是设计取舍（门无法知道真正的语料全集），但调用方必须清楚：**要判跨卡重复，`--cards` 必须指向完整语料目录。**
3. **阈值有两处，不会自动同步。** 规则脚本 `main()` 里硬编码 `limits = (2, 0, 0)`，本门有 `--max-cross-card-support` / `--max-summary-copies` / `--max-speaker-prefix`（默认值与之一致）。本门只调 `units()` / `measure()`，**读不到** `main()` 里的 `limits` ⇒ 巡检会话若改了脚本里的 `limits`，本门不会跟着变。**改阈值时两处都要动。**
4. **一张畸形卡会让三条判据对全部卡失效。** 规则脚本的 `units()` 在 `selfClaimRules` / `realSelfBehaviors` 不是数组时 `raise`，`measure()` 因此对**整个语料**抛异常；本门捕获后报 `PERSONA_CRITERIA_ERROR` 并变红（不是静默），但**其余 354 张卡的 R10–R12 结果一并丢失**。这是「单卡 DoS 三条判据」，目前靠 R8 兜住（同一张卡也会触发 `PERSONA_FIELD_SHAPE`）。

### 提交记录

- `21d5597 feat(tooling): 角色卡门扩到 12 条判据（R10–R12 委派外部判据脚本）` —— 3 files changed, 318 insertions(+), 4 deletions(-)。含本门（472 行）、本文档、`AWAKE/tools/persona-workbench/tools/measure-three-criteria.py`。
- 提交后烟测（已提交状态）：绿 76 张 `violations=0` 退出 0；红 279 张 `violations=2263` 退出 1。

---

## 十一、帝国三系那 17 张的「另一型病」＋ 联合契约审计（2026-10-01 增补）

### 怎么定位的（不是靠打字猜）

- 判据：`AWAKE/tools/persona-workbench/tools/measure-three-criteria.py` 的 `measure()` 返回 `(support, copies, prefixes)`；筛「`s<=2 and c<=0 and pr<=0` 且不在已入库 76 张名单里」⇒ 恰好 17 张，`CORPUS=355 GOOD=76 BAD=279 LEAKED=17`。
- 单独拿这 17 张跑门：只命中 `CORE_TOO_SHORT`、`ID_PATTERN`、`TAG_UNREGISTERED` 三条，`violations=51` ⇒ **旧门对它几乎无感**。

### 决定性证据：文件时间线

| 组 | mtime 范围 | 张数 |
|---|---|---|
| GOOD76 | 2026-09-20 00:24:20 .. 2026-09-24 22:06:30 | 76 |
| **LEAK17** | **2026-09-24 23:03:16 .. 23:17:13** | 17 |
| BAD262 | 2026-09-25 12:30:28 .. 20:54:38 | 262 |

好卡 → 隔 57 分钟 → 17 张（14 分钟内批量出）→ 隔 13 小时 → 262 张。**是三批，不是两批。**

### 三组指纹对照（实测）

| 指标 | GOOD76 | LEAK17 | BAD262 |
|---|---|---|---|
| `core` 长度 | 187–546（均 304） | **91–129（均 107）** | 26–92（均 50） |
| `core` 有终止标点 | 76/76 | **17/17** | 0/262 |
| `id` 合 pattern | 76/76 | **0/17** | 0/262 |
| 未注册 tag | 0/76 | **17/17** | 259/262 |
| tag 个数 | 4–11 | **恒 5** | 恒 5 |
| 例句条数 | 4–6 | 5–6 | 恒 5 |
| 四套 profile 键名合规 | 52/52 | **0/17** | 262/262 |
| `identityFacts` 带占位符 | 0/76 | **8/17** | 0/262 |
| `facetStrengths` 键集 == `tags` | 2/76 | **17/17** | 0/262 |
| `summary` 是 `publicDescription` 前缀延长 | 0/76 | **17/17** | 0/262 |

⇒ **LEAK17 是「修了一半」的批**：core 截断（无句号）修好了、长度也翻了一倍，但 id 命名法、tag 词汇表、恒 5 tag 一个没改，另外多了三条只属于它的新病。

### 四条只属于它的签名

1. **四套 profile 键名全缩写**（`traitProfile` 除外）：`behaviorProfile{cond,delib,ing,lead,lev,trust}`、`commitmentProfile{ascope,br,ec,pc,po,pp,pv,vt}`、`expressionProfile{direct,form,play,restraint,warm}`、`reactionProfile{condresp,conf,expr,res,sens,supp,tim}`。17 张**逐张、四套、完全一致**。规范键名的权威在源码：`AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs:307-310`（四套）＋`:738-743`（`traitProfile`），同一批名字硬编码在 `AWAKE/tools/persona-awake-joint/persona-awake-joint.ps1:583-586` 与 `:1057-1060`。
2. **`identityFacts` 里留着未填的模板变量 `（游戏内配偶 heroId）`**（8/17）。
3. **`facetStrengths` 的键集合恰好等于 `tags`**（17/17）。好卡是严格超集（如 `乌尔玻斯` 7 键 vs 6 tag）。
4. **`summary` 是 `publicDescription` 的机械拼接**（17/17；`summary` 81 字 / `pub` 63 字 / `priv` 43 字）。

### 关于「草稿层」的一次反转与再反转（记下来免得后人再绕）

`AWAKE/tools/persona-workbench/AUTHORING-GUIDELINES.zh-CN.md:61`（v4.3）称 profile 是「作者侧刻画草稿：可选、物化时丢弃、不入门禁（仅软建议）」，`AWAKE/tools/persona-workbench/AI-FEEL-BASELINE.md:248` 更直接写着「❌ 不要量草稿层」。

**但这句话在联合契约这条链路上不成立**：`AWAKE/tools/persona-awake-joint/persona-awake-joint.ps1:1092-1093` 明确从 `reactionProfile.sensitiveConditions` / `conditionalResponses` / `commitmentProfile.priorityOrder|protectedValues|applicableScope|exceptionCost|breachResponse` **取文本拼进运行时的 `privateDescription` / `contradictionDescription`**，且 `:591` 要求键名精确相等。

⇒ 结论：**profile 键名走 workbench 物化链路可能是被丢弃的，走 `persona-awake-joint` 导出链路是真损坏。** 文档那句「物化时丢弃」至少对第二条链路是**过时或不准确**的。R15 因此按**硬判据**做（改成告警会更符合文档、更不符合代码）。

### 联合契约审计：0/355 张卡能进（【审计】，未实跑）

- `AWAKE/tools/persona-awake-joint/persona-awake-joint.ps1:456-466` `Assert-JointExactFields`：**未知字段直接 `Throw-JointReject 'persona.schema_unknown_field'`**，不是告警。
- `:559-601` `Assert-JointWorkbenchDocument`：`:560` 的 `$allowed` 只有 **23 个顶层字段**，**不含 `tensionAxes`、也不含 `evidence`**；`:591` 每个存在的 profile 必须字段名精确相等；`:577-578` facetStrengths 值须为整数 1..4；`:596` 数值轴 **-2..2**（而 `AWAKE/tools/persona-workbench/tools/audit-character-schema.ps1:90-91` 用 **-3..3** —— 两处不一致）。
- `:968-970`：导出还要求 `status == 'approved'`，否则 `persona.workbench_not_approved` ⇒ **76 张「好卡」全是 `draft`，在这一关也会被拒。**
- `:363-402` `Get-JointWorkbenchSource` **不做任何字段剥离/转换** ⇒ 上面这些检查看到的就是原始卡。
- 对照全库 **323/355 张卡有 `tensionAxes`** ⇒ 全部触发未知字段；LEAK17 另外在四套 profile 上触发键名不符。**0/355 能过。**

**契约认可的卡长什么样**（`AWAKE/docs/fixtures/persona-awake-joint/PWB-AWAKE-001-valid-approved/source.json`）：**只有 18 个顶层字段、没有 `tensionAxes`**，四套 profile 全用规范长键名，`status:"approved"`，且 `tags` 里允许 `boundary.no_empty_promises` 这种不在 `tag_registry.json` 里的 id（该 fixture 自带另一份 `registry.json`）。

### 由此产生的三个未决项

1. **`tensionAxes` 到底该不该在角色卡里？** 角色卡 schema（`AWAKE/tools/persona-workbench/contracts/persona-workbench.character.v1.schema.json:29`）与 `AWAKE/tools/persona-workbench/src/PersonaWorkbench.Verify/Program.cs:107` 都认它合法，联合契约不认。**是契约漏了这个字段，还是卡不该带它？** 待裁。
2. **`status` 从 draft 到 approved 的路径**：联合契约要求 `approved`，全库 355 张一张都不是。批准动作由谁做、在哪个入口做，尚未找到。
3. **R4 的语料相对性**：R4（标签戳）按「占**本次语料**的比例 ≤10%」算。LEAK17 在批内是 5/17 = 29%（很明显），混进 355 张里只有 5/355 = 1.4%（**永不触发**）。⇒ **按批次重做时，戳记会藏进多数派里。** 这条对「重做 279 张」是致命的，必须处理（候选：按批次分组，或加「批内」戳记检测）。另：R4 还需 ≥20 张才生效。

本文档与门的本次改动（R13–R16）见下一次提交。
