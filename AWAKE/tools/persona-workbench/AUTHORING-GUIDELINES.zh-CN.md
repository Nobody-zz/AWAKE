# Persona Workbench · 角色卡创作与取证准则（强制）

- 版本：v4（2026-09-13，红队驱动「可靠版」：三层结构【硬门 / 观测 / 认证】；E5 由动词枚举改**结构判定**；E2b 由词表命中改**窗口共现**；M7 复读自检降为观测；M3 与 E2b 的自相矛盾消解；新增 M8 处境覆盖与 Q1/Q2 盲评协议）
- 性质：**强制执行规则**，非参考建议。任何 Agent / 协作者在本工作区创建或修改 `characters/*.persona.json` 前，必须先读本文件，并遵守全部硬规则。
- 缘起：2026-09-11 创作与校核中发生 5 次真实性误判（见 `docs/REVIEW-PERSONA-WORKBENCH-FORMAT-QUALITY-20260911.md`），根因均是**先凭记忆/印象下论断，未先对游戏数据取证**。本准则将其前移为写作动作本身的前置约束。

---

## 核心原则：先落依据，再断言

任何人名、地名、归属、事件、台词——**先锁定游戏数据依据（heroId / clanId / kingdomId / 官方 key / 官方译名表条目），再动笔写正文**。不得凭记忆先写、后补校核。

---

## W1 · 归属依据前置
- 写 `identityFacts` / `core` / `origins` 前，先从 `heroes.xml` 锁定该角色的 `hero id`，从 `spkingdoms.xml` / `spclans.xml` 锁定其 `kingdomId` / `clanId`。
- 卡内 `origins.heroId` / `kingdomId` / `clanId` 必须与实际 latgames 数据一致；保存/导出前跑 `tools/audit-character-affiliations.ps1`。

## W2 · 存在性判断必须双向穷举
- **写"游戏内存在 X"之前**：找到该角色/事件的官方文本（heroes 正文、对应语言文件 key），并在卡内 `origins.heroId` 或其 `sourceDescription` 注明依据。
- **写"游戏内没有 X"之前**（此方向更危险，阿庇斯·瓦罗斯误判即源于此）：必须穷举所有相关数据源，不得只查一个文件就下结论。数据源清单见下表；任一源未查，均不得声称"游戏内无此人/无此事"。
- 反过来说，查表未命中 ≠ 不存在，只意味着"当前未取证"：此时标记 `待核`，不得默认其存在，也不得默认其不存在。

## W3 · 译名一律查表
- 中文人名/地名/阵营名，先查 `docs/mappings/character-names-zh-en.tsv`、`persona-names-zh-en.tsv`、`settlement-names-zh-en.tsv`。
- 表内有 → 照用官方译名，禁止自造音译。
- 表内无 → 先加"待核"标记，并用正文级官方用法为准（如 heroes 正文内嵌名 `蒙楚格`/`墨速宜` 属这一类），不得直接编造。

## W4 · origins 必填
- `origins`（heroId + kingdomId，必要时的 clanId / parentHeroId / spouseHeroId / seatSettlementId）为 `schemaVersion=persona-workbench.character.v1` 的强制结构。
- 缺失、与 `heroes`/`spkingdoms` 不符，即属违规，`audit-character-affiliations.ps1` 会判 FAIL。

## W5 · 补写显式化
- 游戏内明文之外的一切合理延展、推断、创作人物，必须在 `sourceDescription` 中明示来源口径（基于游戏内 X 的创作 / 为叙事衔接而补写 Y）；禁止把创作内容伪装成游戏明文。
- 禁止在正文（`core`/`identityFacts`/`contradictionDescription` 等）引入无依据的专用名词却不标注；无官方依据的人物关系不得写为既定事实。

## W6 · 改动门禁（最少四道）
- 每次修改任一卡后，依次通过，全部通过才可宣称完成：
  1. `tools/audit-character-schema.ps1`（结构 + 数值轴 + origins + 枚举）
  2. `tools/audit-character-affiliations.ps1`（归属 + 译名 + origins 一致性）
  3. `tools/audit-character-text.ps1`（A/B 硬伤为零；C/D/E/F 提示项人工复核）
  4. `tools/compile-verify.ps1`（真实编译链全量通过，含 crosswalk + tag registry 哈希校验）
- 任何一道 FAIL 或任一 A/B 硬伤未清零，均视为未完成。

## W7 · 运行时交付（方向 A + 方向丙）
- 四门禁通过后，把卡交付给运行时：`tools/materialize-definitions.ps1` 生成 `definition.v1`（只含 文本 + tags + bundles，丢弃 axis/facet），随后 `tools/audit-definitions.ps1` 校验 47 个 definition（46 卡 + `hero_default`）可被 `PersonaDataLoader` 加载；`DEFINITIONS_VALID=1` 才算交付完成。
- **方向丙契约（重要）**：运行时人物 prompt 只展开**作者自选 tags**，`bundles` 仅作元数据（主题/档案引用），**绝不注入 prompt**——避免被别人词集越权激活（`PersonaDslGenerator.cs` 的 `TryExpand` 已传 null bundle）。因此作者要给人格加面向，必须写在卡片 `tags`，而不是靠 bundle 词集。
- 若改了 crosswalk（例如跑 `update-crosswalk-b3.ps1`），必须同步 `PersonaAuthoringV2.cs` 的 `CrosswalkSha256`，否则 `compile-verify.ps1` 会因"contract assets drifted"失败。
- **字段载荷契约（字段去留）**：真正交付给运行时的只有三件事——①叙述文本（`core/identityFacts/summary/public/private/contradiction`）②边界（`selfClaimRules/realSelfBehaviors/selfClaimExamples`）③`tags`。`facetStrengths`、`traitProfile/expressionProfile/behaviorProfile`、`reactionProfile/commitmentProfile` 均为「作者侧刻画草稿」：**可选、不进运行时（物化时丢弃）、不入门禁（仅软建议）**；schema 与 `audit-character-schema.ps1` 已把这些字段移出 required 并按规范键收口。要让某条人格生效，请写 `tags` 或叙述文本，不要依赖草稿层。

## W8 · 溯源双层边界（作者侧 vs 发布侧）

> 溯源功能**只服务作者，不交付玩家**。对外一切内容统一归入"模组官设（mod-canon）"一层，绝不向玩家暴露创作过程的分源。

- **作者侧（内部，仅你自己用）**：可保留细粒度来源区分（官方明文 `official` / 官方数据 `heroes_data` / 本模组世界书 `worldbook` / 第三方模组二创如 CK3 `mod_fanon` / 自创 `original`），用途是**审计自查、防写错与漂移**。这些分源**不入发布材质、不写进任何对外文档、不进入门禁的输出面**，只作内部取证凭据。
- **发布侧（玩家观感，唯一对外标尺）**：不存在"作者自创 / 二创"等细分。凡被本模组采纳发布的内容，对外即为**模组官设一层**，是自洽的、不可再拆的整套世界版本；即便同角色在原版有官方设定、在模组有改编，对外呈现的永远是**本模组这套**。
- **禁止为玩家逐卡标注来源**（如每条 `native` / `mod`）：预告"此处可能有冲突"只会制造困惑，把作者负担和玩家注意力引向错误方向。逐卡来源标签**一律不加**。
- **原生/模组的整体披露**：由模组在置顶简介一次性声明"本模组在原版基础上扩展、补充、改编"即可，不需要也不应给每张卡贴来源标签。
- **唯一的例外**：仅当某处改编**故意与原版显式设定正面冲突**（非"在空白处补写"）时，在**该卡正文**里用一句文字说明（如"本条为模组改编"）交代，作为对可能引发不满的诚实披露——这是**文字说明，不是溯源字段**。
- **对门禁的影响**：作者侧分源可辅助取证校验（如 W1/W2/W5），但均不新增运行时字段、不新增发布侧 schema 项。`sourceDescription` 保持作者侧定位，不进运行时（见 W7 字段载荷契约）。

---

## W9 · 完成定义（DoD · 一卡何时算完成）

一卡从 `draft` 到可交付，必须**依次全绿**，缺一不算完成：

| # | 门禁 | 入口 | 通过判据 |
|---|---|---|---|
| 1 | 结构 | `tools/audit-character-schema.ps1` | 0 FAIL（必填/id/origins/数值轴/枚举/多余键） |
| 2 | 归属 | `tools/audit-character-affiliations.ps1` | issues=0 |
| 3 | 文字 | `tools/audit-character-text.ps1` | A/B 硬伤为零 |
| 4 | **表达质量（第五门禁）** | `tools/audit-character-enhancement.ps1` | **E1 / E2 / E2b / E4 / E5 全绿**（v4 起 E5 为结构判定；O2–O5 为告警，不判死） |
| 5 | 编译 | `tools/compile-verify.ps1` | 真实编译链全量通过 |
| 6 | **复读自检（v4 降为观测）** | `tools/worldbook-runtime-sim/persona_scenario8_focused.ps1 -Cards <卡>` | 出**观测报告**并随卡归档；**不再作硬门**——它测的是模型输出，不是卡的素材 |
| 7 | **运行时物化** | `tools/materialize-definitions.ps1` + `tools/audit-definitions.ps1` | `DEFINITIONS_VALID=1` |
| 8 | **盲评（人工，非机械）** | 按 §Q1/Q2 执行，结论留档 | 记录齐全（三档结论 + 依据），**语义层唯一的认证手段** |

任何一道红，或任一硬伤未清零，该卡仍为 `draft`：不得标 `approved`，不得宣称完成。

> **v4 变更（红队驱动）**：第 6 道由**硬门降为观测**；新增第 8 道**盲评**。原因是红队证明了两件事——① 第 6 道判定的是「模型的输出」，换模型/温度即变，本质不该决定一张卡的生死（违宪章 R1）；② 反脸谱最核心的两个维度（**脱名可辨识、语义密度**）**在语义层面不可能机械化**，任何声称用门禁测它们的设计都是不可靠的（违宪章 R6）。故 v4 把机械层降为「下限拦截」，把语义判定交给盲评并留档。

> **缘起（2026-09-13 复核）**：此前只用旧四门禁（上表 1–3、5）宣称"全部验证绿门"，而第 4 道（表达质量）与第 6 道（8 场景自检）**根本没跑**——全量 76 卡实测仅有 2 卡（拉盖娅、那得娅）过 E1–E5，其余 74 卡里 64 卡是同一个「X 会如何…」问答框。**"字段合法"不等于"质量合格"**。W9 的作用就是把这两件事绑死：没跑表达质量门禁，不许说绿。

---

## 人格表达质量规范（决策样本 / 三张力轴 / 反金句 / 反模板 / 反脸谱）

> 背景：2026-09-12~13 用 qwen2.5 跑 8 场景实测，锁定三个病根——①角色卡里的「金句/口号」会被小模型在索取、质询、开价场景**整句复读**；②统一「如何回应…」问答框把几十张卡套成同一腔调（模板化）；③用文化符号堆砌（草原/雕弓/烈马…）让同王国角色撞脸（脸谱化）。
>
> **v4 结构（红队驱动，见 `docs/PERSONA-SPEC-RELIABILITY-CHARTER-20260913.md` 与 `docs/PERSONA-REDTEAM-3-20260913.md`）**：本规范分三层，**不得混用**——
>
> | 层 | 性质 | 判定 | 覆盖 |
> |---|---|---|---|
> | **硬门** | 机械，判生死 | 只判**结构与形式**的下限 | E1 / E2 / E2b / E4 / E5 |
> | **观测** | 机械，出报告不判死 | 记录事实，供人判断 | O1 复读自检 / O2 处境 / O3 符号 / O4 纯台词 / O5 跨卡印章 |
> | **认证** | 人工 / 盲评 | 语义层唯一手段 | Q1 脱名辨认 / Q2 语义密度 |
>
> 三层分立是红队逼出来的：**反脸谱（脱名可辨识）与语义密度在语义层面不可机械化**，任何"用门禁测它们"的写法都是不可靠的（宪章 R6）。机械层只做下限拦截，质量认证交盲评。

### 总纲：让模型吸收「判断」，而非背诵「台词」

`selfClaimExamples` 不是对白台词库，是**决策样本**——供运行时吸收角色的价值排序与决断路径，而非提供一句句可背的现成回话。判断写对 → 模型在任何场景都能即兴出「像本人」的回答；写成金句/模板 → 模型只会复读。

**边界（v4 明确）**：本规范约束的是**给模型的素材**，不是**模型怎么回**。「模型会不会复读」由模型决定，规范只能减少素材里的诱因，不能保证输出——凡以模型输出为判定对象的规则，一律降为观测（见 M7）。

---

### M1 · 决策样本 = 自由叙事片段（反金句）

- 每条 `selfClaimExamples` = **具体处境 + 具体利害**的第一人称散文/决断叙事，**3–8 条**（v4：上限由 6 放宽至 8，避免"卡着下限写"把信息量压薄；E1 审计）。
- **金句禁令（强制）**：禁止写「可整句背」的顺口溜、格言、对仗、座右铭式短句。例证宾语「名分不是布帛，不能论尺买」正是被 qwen2.5 在拉盖娅 2_利益交换 一字不落复读的那句（证据 `docs/evidence/lord_1_14-scenario8.json`）。
- **自查判据**：逐条读，若能抽出任意一句 ≥10 字、可独立成句、像「座右铭」的短句，即违规，改写为「处境 + 盘算 + 结果」的散文化叙述。
- 至少一条为纯散文/决断叙事（体内无「」收尾引号），进一步断开背诵回路。

### M2 · 三张力轴（**可选建模法，允许缺席**）

- 写 `selfClaimRules` / `realSelfBehaviors` / `selfClaimExamples` / `contradictionDescription` 之前，先填 `tensionAxes` 三轴：
  - `hardLine`：绝不退让的底线（命根子）。
  - `negotiable`：可以商量、可以绕的空间。
  - `breachSwitch`：什么现实压力会逼他破一次例，破完又如何收回。
- **允许缺席（v4 新增）**：任一轴可填 `none`——「无底线 / 不可谈判 / 无破例」是**合法人格**，不是缺陷。疯的、痴的、殉道的、铁板一块的角色不再被机器判死（红队 A1/R5 整改）。
- **但不得三轴全 `none`**（v4）：三轴全 none 等于"什么都不写"，会变成逃逸口（红队 B4 实测可全绿过关）。**至少一轴须有实质**，且须让人看出「他真正在意的是什么」。
- `tensionAxes` 是**作者侧草稿**，物化时丢弃，所以三轴内容必须**落实进运行时字段**（E2b）。

### M3 · 底线必须绑条件（去铁板）

- 禁止**无条件的**绝对断言；**底线词必须与条件/让步词在同一句或 70 字窗口内共现**（E2b）。
- 反例（无条件，判 FAIL）：`我绝不让出正统`
- 正例：`若有人拿钱财来掂先皇传下的名分次序，我绝不退；可他若只求在南边安身逐利，位子与情分都好商量。`
- **v4 修订说明**：旧版把「绝不/永远/总是」列为**禁词**（M3 旧文），同时又在 E2b 的**必填词表**里放「绝不」——两条条款对同一段文本给出相反要求，属**自相矛盾**（红队 A1，实测 6 张卡靠"绝不"过 E2b）。v4 取消词级禁令，改为**结构要求**：**不禁用词，只禁用"无条件"**。矛盾即消解。

### M4 · 禁可背诵的结论句（**v4 取代旧「阶梯表态」**）

- **旧版 M4 已废**：它要求角色必须「先认诚意 → 再谈可让 → 最后守底线」，并写明「绝不让角色一口抛结论」。红队判定此条**越界**——它规定的是**模型该怎么说话**（演出节奏），而非给模型什么素材；而且那套"先礼后兵"是**帝国宫廷话术**，强加给库赛特头人、诺德狂战士、阿塞莱商人就会撞脸（红队 RT-5 / A9）。
- **v4 规则（管素材，不管演出）**：样本内**不得出现可整句背诵的结论句/台词**（与 M1 金句禁令同源）；保证至少一条为**纯叙述**。至于角色这一次是"先礼后兵"还是"一口回绝"，**交给模型**。

### M5 · 异构破模板（**结构判定**）

- 样本**不得成片套用统一问答框结构**，两种典型结构信号：
  - (a)「问号 → 破折号 → 引号」：`拔该会如何劝解…？——“台词”`
  - (b)「会/要 + 如何/怎样/怎么」：`蒙楚格会如何回应…`
  - 占本卡样本 **≥50% 即 FAIL**。
- **v4 为何改结构判定**：旧版靠**有限动词表**（回应/应对/看待…）判定，实测有 5 张卡换动词（如"劝解"）即溜过，其中克洛托耳 2/2 全是框、动词命中 0，对旧尺子**完全隐形**（红队 A2/R3）。**枚举必然泄漏**，故改为结构信号。
- **模板不得搬家**：`selfClaimRules` 同样不得成片套用该结构（E5b）。
- 同卡内多条样本的**处境**应尽量不同（见 M8）；视角、语气的不同由盲评 Q1 把关。

### M6 · 脱名测试（反脸谱）——**分层落地**

- 文化底色（方言/礼数/称谓/意象）只是「怎么说」的共享引擎，**不解释个体是谁**。禁止用一整套族属符号（草原/雕弓/烈马、绿洲/商路/苏丹、冰原/斧盾）填充角色。
- **同王国的多个角色必须互不相同**：区分落在行为铁律 + 语言指纹 + 他在意的那一两件事上。
- **验收分层（v4）**：
  - **机械层**（告警，不判死）：O2 处境单一 / O3 族属符号堆砌 / O5 跨卡印章——只提示，供人复核。
  - **Q2 现已附机械预检**（v4.1）：`PersonaCorpusAudit` 把 Q2 的两个反问落成可比对的形式——跨卡整条复用、跨卡首分句骨架、零信息句、卡内重复、抄编辑器示例文字。**它只出提示，认证仍归人工 Q2**（见下表）。
  - **认证层**（人工，见 Q1）：遮名辨认，**这才是判定手段**。
- **明确边界**：脱名可辨识是**语义判断，机械层不可能判定**。旧版 M6 声称"验收"，却零自动化、也无人工程序，属**空条款**（红队 A4）。v4 不再假装机械能测它。

### M7 · 复读自检（**v4 降为观测，不再是硬门**）

- 每张重点卡改完后，仍跑 8 场景本地模型自检：`persona_scenario8_focused.ps1 -Cards <卡>`，输出 `docs/evidence/*-scenario8.json`。
- **但它只出观测报告，不判卡的生死**。理由：它判定的是**模型的输出**（`anyReplyRepeatedSlogan` / `maxOverlapRatio`），换模型、换温度结论即变——**卡的质量不该是模型行为的函数**（红队 A9，宪章 R1）。
- 更关键的一层：要求"模型回复与卡内样本重叠 < 30%"，会**反向迫使素材与角色语言脱钩**——写手为了过关，只能把卡写得"跟任何回复都不像"，也就是**故意写得不像这个角色会说的话**。**不复读 ≠ 像本人**；退回复读通用腔调，才是最大的脸谱。（此条为**待验证假设**，见红队报告 §未验证。）

### M8 · 处境覆盖（**观测 O2**）

- 同卡样本应尽量覆盖不同**处境类**（利益交换 / 索取求助 / 质询审问 / 战争武力 / 结盟投靠 / 亲族婚嫁 / 生死殉难 / 背叛告发 / 日常生计）。
- **但这是告警，不是硬门**：校准实测，把"处境 ≥2 类"设为 FAIL 会**误伤 2 张已知好卡**（埃隆、普林多尔），且与 M6「区分落在他在意的一两件事上」**自相矛盾**——只在乎一件事的角色是**好角色**，不该被判死（红队 R3 校准）。
- 故：机械层只提示，是否补足处境由作者与盲评判断。

### 盲评协议（Q1 / Q2 · v4 新增，语义层唯一认证手段）

> 执行人：作者以外的复核者，或换一个模型（**不得用写这张卡时用过的同一模型**）。结论留档于 `docs/review-state/` 或卡旁 `.review.md`。

- **Q1 脱名辨认（反脸谱验收）**
  1. 取同王国全部卡，遮去姓名、家族名、地名人名与一切族属符号词。
  2. 逐条读 `selfClaimExamples`，回答："这是谁？"
  3. 判据三档：**能唯一认出 = 通过**；**只能说出一类人（"某个库赛特头人"）= 撞脸**；**完全认不出 = 空洞**。撞脸或空洞 → 重写或并卡（并卡前须经用户裁决）。
- **Q2 语义密度（防空洞）**
  1. 去掉所有专有名词后重读样本。
  2. 问："这些句子换成另一个人，还成立吗？"若几乎都成立 → 语义空洞，重写。
  3. 反向问："有没有哪一句是**只有这个人**才会说/才会做的？"**至少要有两条**。
  4. **机械预检先行（v4.1）**：跑 `PersonaCorpusAudit`（工作台「全库一起看」面板，或 CLI `--corpus-audit`）先看两件事——① `selfClaimRules` / `realSelfBehaviors` 有没有跨卡共用的开头（O5 结构上扫不到这两个字段）；② 有没有"自称我／本名"这类**由卡内已有字段就能推出来**的空句。
     **但它判不了第 2 问**（"换成另一个人还成立吗"是语义判断，机械层不可能判，见 M6 边界）：预检零告警 ≠ Q2 通过。

### 门禁与工具映射

| 规范 | 自动化审计 | 入口 |
|---|---|---|
| M1 条数 / M2 三轴声明（含 none 规则）/ M3 底线绑条件 / M4 禁结论句 / M5 结构框 / M8 处境 | `audit-character-enhancement.ps1`（E1/E2/E2b/E4/E5 为硬门；O2–O5 为告警） | 第五门禁 |
| M6 机械层 | 同上（O2/O3/O5 告警） | 第五门禁 |
| M5 的 `selfClaimRules` 侧（E5b）**＋ Q2 的机械预检** | `PersonaCorpusAudit`（工作台面板「全库一起看」；CLI `PersonaWorkbench.Verify --corpus-audit <卡目录>`） | **编辑器内**（v4.1 新增） |
| M6 认证层 / M8 复核 | **人工盲评 Q1/Q2** | 无自动化（刻意；机械预检只做提示，不替代） |
| M7 复读自检 | `persona_scenario8_focused.ps1` | **观测**（不再硬门） |
| 规范可靠性的红队回归 | `redteam/run-attack-suite.py --spec v4` | 改规范后必跑 |

> **写作顺序（v4）**：写卡 → 填三轴（可为 none，不得全 none）→ 写自由叙事样本（禁金句、底线绑条件、禁结论句、破问答框、尽量多处境）→ 跑 `audit-character-enhancement.ps1` 全绿 → 跑四门禁 → 物化 → **盲评 Q1/Q2 留档** → 交付。8 场景自检随时可跑，但只作参考。

---

## 取证数据源清单与口径

| 数据 | 来源文件（自 `Modules\`） | 口径 |
|---|---|---|
| 角色 id / 归属 / 关系 | `SandBox/ModuleData/heroes.xml` | 唯一 ID 源；`faction` 即 clan |
| 王国 owner | `SandBox/ModuleData/spkingdoms.xml` | 统治者/kingdomId 权威 |
| 家族 | `SandBox/ModuleData/spclans.xml` | clan 归属 |
| 角色官方中文（固定名覆盖层） | `Native|SandBox/ModuleData/Languages/CNs/std_heroes|std_lords_*_xml-zho-CN.xml` | **名字层 305 条覆盖**；grep 须含此覆盖层 |
| 角色正文级中文名 | 上述 heroes 正文内嵌名 / `std_common_strings` | 如 `蒙楚格`、`墨速宜`、`兀儿浑` |
| 地名 | `*/Languages/CNs/std_common_strings*_xml-zho-CN.xml` | 注意 `Makeb→马凯布` 在 **Native** 而非 SandBox |
| 通用专名 | `Native/ModuleData/std_common_strings.xml` | 人物通用 `{=...}` key 的口径 |
| 时间线口径（硬规则） | 官方现行年表：**潘德拉克战役 = 1077 年**（涅雷采斯战死）；阿雷尼科斯 ≈ **1083 年**在都城吕卡隆（`town_ES4`）遇害，为帝国分裂直接导火索。**禁止采用旧版 1064 年**；凡卡中年表/年份表述一律以 1077 版为准，写前查证、写后跑 `audit-character-text.ps1`。 |

> 只因为"在 heroes.xml 没搜到"就判定"游戏内没有"，是明确违背 W2 的违规行为。

### BannerlordSage 本地索引（便捷取证入口，非权威）

| | |
|---|---|
| 工具 | `tools/bannersage-query.ps1 <kind> <arg>`（`hero`/`clan`/`kingdom`/`culture`/`settlement`/`localize`/`search`），把 BannerlordSage 建立的 `bannerlord.db` **只读**当查询源，秒查归属/亲属/生平正文，并自动翻译 `{=key}` token 为官方中文（简体 `CNs`）。查询输出里的 `owner`/`spouse`/`father`/`mother`/`initialHomeSettlement` 等**引用（`Hero.*`/`Faction.*`/`Kingdom.*`/`Settlement.*`/裸 id）自动解析为中文名**。`search` 通吃英文/中文：**中文关键词走反查**（CNs/CNt 文本 → `stringId` → 实体），如 `search 蒙楚格` 直达 `hero lord_6_1`、`search 马凯布` 直达 `settlement town_K3`，并附命中正文片段。 |
| 权威性 | `heroes.xml` / `spkingdoms.xml` / `spclans.xml` 仍是**唯一 ID 源权威**；此工具只是它们的便捷查询镜像，不替代原始文件。 |
| 依赖 | 库**不在本工作区**，位于 `BannerlordSage/dist/games/bannerlord/bannerlord.db`（外部路径）；可用环境变量 `BANNERSAGE_DB` 覆盖。 |
| 覆盖范围 | 当前索引 `dll_scope=core` + `xml_scope=official`。**某 ID 未命中 ≠ 不存在**——若确属 Native/SandBox 数据应回原始文件取证（遵守 W2），不得据此断言"游戏内没有"。 |
| 核验覆盖（2026-09-12 实测） | `hero`（六大王国统治者+配偶，如德泰尔 lord_4_1）、`kingdom`（empire_w/khuzait/aserai）、`clan`（兀儿浑乃特、俄斯提科斯）、`culture`（帝国）、`settlement`（town `town_K3` / castle `castle_A1` / village `castle_village_A1_1` 三类）、`search`（中文反查如瓦兰迪亚/雇佣兵团/蒙楚格）。引用 `owner`/`spouse`/祖地均解析为中文名；不存在 id（如 `castle_C11`）返回 `not found` 不崩溃。 |

---

## 工具链

| 脚本（`tools/`） | 作用 |
|---|---|
| `audit-character-schema.ps1` | 契约 lint：必填、id pattern、origins、数值轴 -3..3、枚举、多余顶层键 |
| `audit-character-affiliations.ps1` | 归属稽查：kingdomId 对齐 + 译名对照 + origins 一致性 |
| `audit-character-text.ps1` | 文字 lint：A/B 硬伤必须为零；C 超长句 / D 模板腔 / E 代词 / F 复用 为人工复核项 |
| `compile-verify.ps1` | **编译门禁**：走真实 PersonaAuthoringV2 编译链，校验 crosswalk 哈希、tag registry 哈希、文本合法性、标签/轴/面合法性，全量 46 卡通过才算 OK |
| `materialize-definitions.ps1` | **运行时物化（方向 A）**：把每张 `characters/*.persona.json` 转成 `definition.v1`，只保留 叙述文本 + `tags`(PersonaTagUse) + `bundles`(按 tags 与 4 bundle 词集 Jaccard 重合度自动最佳分配)；**丢弃 axis / facetStrengths**（运行时模型无 observations）。输出到 `ModuleData/persona_definitions/definitions/` |
| `audit-definitions.ps1` | **运行时定义审计**：镜像 `PersonaDataLoader` 加载约束——schemaVersion v1、id 唯一且非空、status 合法、character 级必须带 characterId/tags、tag/bundle 都能在 registry 解析。`DEFINITIONS_VALID=1` 才算 OK |
| `update-crosswalk-b3.ps1` | **方向 A（B-3）幂等迁移**：把 crosswalk 里 axis/facet 的全部非零取值降为 `preserve_only`（0 保持 omit），使运行时 selector 只由 tags 取材；执行后可重算并打印新 crosswalk SHA-256，须同步 `PersonaAuthoringV2.cs` 的 `CrosswalkSha256` 常量 |
| `build-character-names.ps1` | 从 lords/std_lords 重建角色名表（TSV 单一事实源） |
| `bannersage-query.ps1` / `.ts` | BannerlordSage 本地索引查询入口：`hero/clan/kingdom/culture/settlement/localize/search`，自动解析官方中文（见"取证数据源"节口径） |
| `audit-character-refs.ps1` / `.ts` | **库直连三方核验**：把 `characters/*.persona.json` 的 `origins.heroId` 对准 BannerlordSage 库，核对 hero 实际归属 clan / 王国 superFaction / 中文名与卡内 `kingdomId`/`clanId` 是否一致。退出码 0=一致、1=有缺失/不一致、2=依赖缺失。是 `audit-character-affiliations.ps1`（只读 XML）之外的多一层实时核验。 |

---

## 违反后果与校核入口

- 违反任一硬规则 → 该卡不得进入 `approved` 状态，不得宣称审核通过，不得标注为游戏内依据。
- 复核入口：`docs/AUDIT-CHARACTER-SCHEMA|AFFILIATIONS|TEXT-QUALITY-20260911.md` 与 `docs/AUDIT-CROSS-CARD-CONSISTENCY-20260911.md`。
- 后续若发现既有卡存在未依此准则核实的条目，应回到取证阶段修正并重跑四门禁，而不是依赖既有结论。

---

## 修订记录

| 版本 | 日期 | 改动 | 验证证据 |
|---|---|---|---|
| v4.1 | 2026-09-15 | ①Q2 增**机械预检**（`PersonaCorpusAudit`：跨卡整条复用／跨卡首分句骨架／零信息句／卡内重复／抄编辑器示例文字），并写明它**只做提示、不替代 Q1/Q2 认证**；②M6 机械层条目补上该预检；③门禁映射表新增编辑器内入口 | 真实 76 卡：**3 提示 / 0 错误**，报的是共用开头那一句（`称可汗为“蒙楚格”`×5、`自称“我”`×5、`自称“我”或“斯瓦娜”`×4）；引擎 8 条单测全绿；判据已做变异检验（改坏被测提示词 → 判据变红） |
| v4 | 2026-09-13 | **红队驱动「可靠版」**：①三层结构（硬门 E1/E2/E2b/E4/E5 · 观测 O1–O5 · 认证 Q1/Q2）；②E5 由动词枚举改**结构判定**；③E2b 由词表命中改**70 字窗口共现**，M3 禁词与 E2b 必填词的自相矛盾消解；④M2 允许 `none` 但**不得三轴全 none**；⑤M4「阶梯表态」废，改为「禁可背诵结论句」；⑥M7 复读自检**降为观测**；⑦新增 M8 处境覆盖（告警）与 Q1/Q2 盲评协议；W9 由 7 道改 **8 道**（第 6 道降观测、新增第 8 道盲评） | 红队套件 **`redteam/run-attack-suite.py`：v3 BREAK 7/9 → v4 BREAK 0/17**（连续两轮无 BREAK，宪章 R7 收敛）；E5 结构判定命中 **69** 张（旧动词判定漏 5 张，含克洛托耳）；E2b 69 张因"底线未绑条件"判 FAIL；好卡 拉盖娅/那得娅 仍 PASS；`E5b`/`E2allnone`/`O5` 在真实卡上零误伤；详见 `docs/PERSONA-REDTEAM-3-20260913.md` |
| v3 | 2026-09-13 | ①新增 W9 完成定义（七道门禁绑死，"字段合法≠质量合格"）；②`audit-character-enhancement.ps1` 的 E5 由「只认『如何回应』四字全等」升级为「问答框动词族 + 占比 ≥50% 判 FAIL」，堵住换动词绕过；③`persona_scenario8_focused.ps1` 复读判定由布尔升级为 10 字滑窗比例（mid-adapt 计入），并支持 `-Cards`；status 增 `repetition_detected` | E5 命中卡数 **26 → 64**（新抓 38 张）；拉盖娅/那得娅未被误伤；两脚本 `Parser::ParseFile` 0 错误、BOM `efbbbf` 完好；全量基线 `docs/persona-quality-baseline-20260913.json`（76 卡 / 过 2 / 败 74） |
| v2 | 2026-09-13 | 新增「人格表达质量规范」章节 M1–M7（反金句 / 三张力轴 / 条件化 / 阶梯表态 / 脱名测试） | 8 场景 qwen2.5 实测（拉盖娅「名分不是布帛」复读案例） |
| v1 | 2026-09-11 | 初版 W1–W8 取证与门禁规则 | 5 次真实性误判复盘（阿庇斯·瓦罗斯误判为"游戏内无此人"） |