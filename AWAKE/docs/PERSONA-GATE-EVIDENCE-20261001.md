# 角色卡质量门 · 红绿证据（2026-10-01）

门：`AWAKE/tools/persona-card-gate.py`（9 条判据）
证据等级：**E2（离线测试）** —— 未涉及游戏内验证，本门也不声称游戏内生效。
配套规格：[`PERSONA-CARD-SPEC-v1-20261001.md`](PERSONA-CARD-SPEC-v1-20261001.md)（巡检会话，另写；关系见 §7）

---

## 一、九条判据

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

红批次合计 **1477 条违规 / 279 张卡**。

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
PERSONA_GATE_SUMMARY total=279 failed_cards=279 violations=1477
PERSONA_GATE_RED
exit=1
```

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
| restored | 还原 | GREEN | 0 | — | ✅ |

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

---

## 九、本次未做的事

- 未修改任何一张角色卡（变异在临时目录副本上做，已按字节还原）
- 未修改任何 schema、`tag_registry.json`、物化器
- 未把门挂进 `AWAKE/tools/build.ps1`
- 未提交任何文件
- 未做游戏内验证（本门与游戏内无关）
