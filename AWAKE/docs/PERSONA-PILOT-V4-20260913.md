# 样板卡 V4 验收 + M7 假设验证（2026-09-13）

> 目的：①拿一张真卡按 v4 规范重写、走八道门禁，验证"规范能不能落到卡上"；②用本机 Ollama 小样本，把红队留下的那条**未验证假设**（M7 是否反向迫使素材与角色语言脱钩）验掉。
> 结论一句话：**卡本身能过（表达质量全绿、物化通过）；卡过不去的那两道，问题都不在卡上——一道是别人未完成的契约改动把编译链弄红了，一道是 v4 自己要求填一个工具链不认的字段。**

---

## 一、任务一：蒙楚格卡按 v4 重写

选它是因为它是最烂的样板：13 条样本、**13/13 全是**「蒙楚格会如何…？——『台词』」问答框、无三轴、status 停在 `draft`。

### 改了什么

| 项 | 改前 | 改后 |
|---|---|---|
| `selfClaimExamples` | **13 条**（超 E1 上限） | **5 条**（3–8 内） |
| 样本结构 | 13/13 问答框 + 引号台词 | **0** 结构框，自由叙事（处境 + 盘算 + 结果） |
| `tensionAxes` | **缺失** | 三轴齐备（硬线 = 征伐不回头；可让 = 分配与名分；破例 = 兵心将散时先抬公正，事后不认） |
| `selfClaimRules` / `realSelfBehaviors` | 含"踩着不变的阶梯…""先接住…再给答复"等**演出指令**（旧 M4 残留） | 改为**素材性判断标准**（他看什么、认什么、不认什么），不管模型怎么说话 |

样本重写覆盖处境：军功分配 / 质疑征伐 / 黄金买名分 / 守约者 / 错怪了自己人。底线词与条件词在 70 字窗口内共现（如"他绝不回头，除非谁能把一条更稳的活路摆到他面前来"）。

### 八道门禁逐道结果

| # | 门禁 | 结果 | 说明 |
|---|---|---|---|
| 1 | 结构 `audit-character-schema` | **PASS** | |
| 2 | 归属 `audit-character-affiliations` | **PASS** | |
| 3 | 文字 `audit-character-text` | **PASS** | |
| 4 | **表达质量** `audit-character-enhancement` | **PASS** | E1 5 条 · E2 三轴齐 · E2b 窗口共现 · E4 有 · E5 结构框 0%；O2/O3/O4 零告警 |
| 5 | 编译 `compile-verify` | **阻塞** | 全量 0/76，非本卡问题（见 §三） |
| 6 | 观测 8 场景 `persona_scenario8_focused` | 已跑，`CLEAN` | v4 起为观测，不再判生死 |
| 7 | **物化** `materialize-definitions` + `audit-definitions` | **PASS** | `DEFINITIONS_VALID=1`（77 文件 = 76 卡 + hero_default） |
| 8 | 盲评 Q1/Q2 | 已做 Q1，**负面** | 见 §五 |

全库基线随之更新：**76 卡 / 过 3（拉盖娅、蒙楚格、那得娅）/ 败 73**（`docs/persona-quality-baseline-20260913.json`）。

---

## 二、任务二：M7 假设验证（本机 Ollama 实测）

**假设原文**：要求"模型回复与卡内样本 overlap < 30%"，会反向迫使写手把素材写得"跟任何回复都不像"——即**故意写得不像这个角色会说的话**。

**实验设计**：同一个角色（蒙楚格）的三版素材，各跑同一套 8 场景，同一模型（qwen2.5:latest, temp 0.7）。

| 版 | 素材形态 | maxOverlap | 均长 | 角色特有趣命中 |
|---|---|---|---|---|
| **A** | 旧版台词库（13 条问答框 + 引号台词） | 0.19 | 45 | 15 |
| **B** | **v4 血肉版**（自由叙事，具体处境/具体利害） | **0.00** | 43 | 10 |
| **C** | **脱钩抽象版**（同语义、抽干具体性，为压低 overlap 而写） | **0.00** | 34 | 2 |

证据：`docs/evidence/MONCHUG-M7-{A,B,C}-20260913.json`

### 结论

1. **假设的因果不成立。** 血肉版（B）overlap = **0.00**，与抽象脱钩版（C）**完全相同**。"写得像本人会说的话"根本不会推高 overlap——因为模型复读的对象是**可背诵的台词式句子**，不是叙事。改成叙事体后，复读诱因自然消失，**不需要故意脱钩**。
2. **但 M7 的问题更大，也更简单：它没有区分度。** 三版**全部判 pass**——包括最差的 A 版（台词库）。A 版场景 5 明显在改写卡里的句子（"军功册上记着你们的血与汗…下一仗战利品头一份，三成归你部族"），10 字滑窗 overlap 却只有 0.04，因为它是"改写式复读"（mid-adapt），连续 10 字对不上。**M7 连它本该拦的东西都没拦住。**
3. **旁证**：素材越具体，模型输出越有角色内容（特有趣 A15 / B10 vs C2；均长 A45/B43 vs C34）。**抽象化确实让输出更空**——但这是"写手自愿脱钩"的代价，不是 M7 逼出来的。

**对 v4 的影响**：M7 降为观测的处置**不变**，但理由要改——不是"逼写手脱钩"（未证实/已证伪），而是"**它测的是模型输出，且无区分度**"（违宪章 R1）。

---

## 三、顺带发现 1：编译门禁全红（非本轮引入）

- 现状：`compile-verify` 全量 **76/76** 报 `persona.migration_required`。
- 根因：**契约摘要漂移**。`docs/persona-contract/` 下的 schema 与 crosswalk 已被改（git 状态 `M` 未提交），但 `PersonaAuthoringV2.cs` 的 sha 常量没跟上：

| 契约资源 | 磁盘实际 sha256（前 16） | 代码常量（前 16） | |
|---|---|---|---|
| `awake.persona.authoring.v2.schema.json` | `2361F7F8E1575D74` | `5B5E704329C83838` | ✗ |
| `persona-workbench-to-awake.crosswalk.v1.json` | `D02A9A6F0411ABED` | `D2F2AF9CBBCA3CAA` | ✗ |
| `persona-canonical-json.v1.json` | `B9BE5523DAF6723E` | 同 | ✓ |
| `tag_registry.json` | `0E66E0344BA64CCF` | 同 | ✓ |

- 对照历史：`docs/AUDIT-COMPILE-VERIFY-latest.json`（9-12 21:19）显示当时 **70/70 全绿** → 这是 9-12 之后引入的。
- **隔离验证**（临时同步常量，验完已回滚，未留在工作区）：编译 **75/76**。
  - 剩 **1 张**：雅那 `Crosswalk has no action for source value: facetStrengths.trait.pragmatic`（crosswalk 缺行）
  - 剩 **3 张**：拉盖娅 / 蒙楚格 / 那得娅 = **恰好是所有填了三轴的卡**（见 §四）
- 证据：`docs/evidence/COMPILE-VERIFY-ISOLATED-FIX-20260913.json`

> 结论：**这不是卡的问题，是契约/工具链的半成品状态。** 属别人在途的改动，本轮未触碰、未收尾。

---

## 四、顺带发现 2：v4 的 `tensionAxes` 与工具链冲突

v4 把三轴（E2）定为**硬门**，要求每张卡在 `persona.json` 里填 `tensionAxes`。**但工具链不接受这个字段**，至少三道拦截：

1. `PersonaDocumentStorage.AllowedProperties` 白名单不含它 → `persona.document_unknown_property`
2. `PersonaDocument` 类无该属性，且 `UnmappedMemberHandling.Disallow` → `persona.document_invalid`
3. `PersonaAuthoringV2` 的 RootProperties 数组不含它

**即：照 v4 填三轴 = 第 5 道编译必红。** 这是 v4 的**第二例自我矛盾**（第一例是红队抓的 A1：M3 禁词 vs E2b 必填词），而且它解释了一个长期现象——**76 张里只有 2 张填了三轴**，且那两张一直编译不过。规则的执行被工具链的硬约束挡住了。

**待裁的两个方案**：

- **方案甲（推荐）**：把 `tensionAxes` 定性为「作者侧草稿」，与 `facetStrengths` 同待遇——补进工具链（上述 3 处）+ 契约，物化时丢弃。
- **方案乙**：`tensionAxes` 移出 `persona.json`，改为卡旁 sidecar（如 `X.tensionAxes.json`），E2 改读外部文件。改动面小，不动编译链。

---

## 五、Q1 脱名辨认（负面结果）

- **方法**：12 张库赛特卡的 35 条样本，遮去专名与族属符号后混排，交**另一模型**（qwen2.5，非写卡模型）分堆。
- **结果**：蒙楚格的 3 条（S11/S14/S35）**未被识别为一组**；S35 还被误配到别人组里。落入判据三档中的"撞脸/无法区分"。
- **注意**：这是**换模型盲评**，不是人工；样本每卡只取前 3 条，任务本身极难。**小模型的盲评能力不足以支撑结论**，须人工复核方可采信。
- **它证明的事**：用同级别小模型做 Q1 复核，**可靠度低**。Q1 协议需要更强的复核者（人 / 强模型）或更多上下文——否则"认证"这一层会变成摆设。

证据：`docs/evidence/MONCHUG-Q1-20260913.json`

---

## 六、本轮产出与待办

**产出**
- 重写卡：`tools/persona-workbench/characters/蒙楚格_monchug_urkhunait_khuzait.persona.json`（v4 版）
- 物化产物：`ModuleData/Worldbook/persona_definitions/definitions/`（77 文件，`DEFINITIONS_VALID=1`）
- 基线：`docs/persona-quality-baseline-20260913.json`（过 3 / 败 73）
- 证据：`docs/evidence/MONCHUG-M7-{A,B,C}-*.json`、`MONCHUG-Q1-*.json`、`COMPILE-VERIFY-ISOLATED-FIX-*.json`

**待办 / 待裁**
1. **契约漂移**（阻塞第 5 道）：谁来收尾——同步 sha 常量 + 补 crosswalk 缺行。属主线级，牵动 76 张卡。
2. **tensionAxes 落地**：方案甲 or 乙（§四）。
3. **Q1 协议加严**：复核者换成人工或强模型（§五）。
4. 蒙楚格卡 status 仍为 `draft`：第 5 道未通前不升 `approved`（守 W9）。
