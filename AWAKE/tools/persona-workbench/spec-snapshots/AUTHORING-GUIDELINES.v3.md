# Persona Workbench · 角色卡创作与取证准则（强制）

- 版本：v3（2026-09-13，新增 W9 完成定义；E5 升级为动词族+占比判定，堵换动词绕过）
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
| 4 | **表达质量（第五门禁）** | `tools/audit-character-enhancement.ps1` | **E1–E5 全绿**（V3 起为硬门） |
| 5 | 编译 | `tools/compile-verify.ps1` | 真实编译链全量通过 |
| 6 | **复读自检** | `tools/worldbook-runtime-sim/persona_scenario8_focused.ps1 -Cards <卡>` | `anyReplyRepeatedSlogan=false` 且 `maxOverlapRatio < 0.30` |
| 7 | **运行时物化** | `tools/materialize-definitions.ps1` + `tools/audit-definitions.ps1` | `DEFINITIONS_VALID=1` |

任何一道红，或任一硬伤未清零，该卡仍为 `draft`：不得标 `approved`，不得宣称完成。

> **缘起（2026-09-13 复核）**：此前只用旧四门禁（上表 1–3、5）宣称"全部验证绿门"，而第 4 道（表达质量）与第 6 道（8 场景自检）**根本没跑**——全量 76 卡实测仅有 2 卡（拉盖娅、那得娅）过 E1–E5，其余 74 卡里 64 卡是同一个「X 会如何…」问答框。**"字段合法"不等于"质量合格"**。W9 的作用就是把这两件事绑死：没跑表达质量门禁，不许说绿。

---

## 人格表达质量规范（决策样本 / 三张力轴 / 反金句 / 反模板 / 反脸谱）

> 背景：2026-09-12~13 用 qwen2.5 跑 8 场景实测，锁定三个病根——①角色卡里的「金句/口号」会被小模型在索取、质询、开价场景**整句复读**；②统一「如何回应…」问答框把几十张卡套成同一腔调（模板化）；③用文化符号堆砌（草原/雕弓/烈马…）让同王国角色撞脸（脸谱化）。本章是治疗这三点的**表达质量硬规则**，与 W1–W8（取证/归属/门禁）并列，同属强制。

### 总纲：让模型吸收「判断」，而非背诵「台词」

`selfClaimExamples` 不是对白台词库，是**决策样本**——供运行时吸收角色的价值排序与决断路径，而非提供一句句可背的现成回话。判断写对 → 模型在任何场景都能即兴出「像本人」的回答；写成金句/模板 → 模型只会复读。

---

### M1 · 决策样本 = 自由叙事片段（反金句）

- 每条 `selfClaimExamples` = **具体处境 + 具体利害**的第一人称散文/决断叙事，3–6 条（写精不凑，E1 审计）。
- **金句禁令（强制）**：禁止写「可整句背」的顺口溜、格言、对仗、座右铭式短句。例证宾语「名分不是布帛，不能论尺买」正是被 qwen2.5 在拉盖娅 2_利益交换 一字不落复读的那句（证据 `docs/evidence/lord_1_14-scenario8.json`）。
- **自查判据**：逐条读，若能抽出任意一句 ≥10 字、可独立成句、像「座右铭」的短句，即违规，改写为「处境 + 盘算 + 结果」的散文化叙述。
- 至少一条为纯散文/决断叙事（体内无「」收尾引号），进一步断开背诵回路（E5 strict）。

### M2 · 先三轴，后正文（三张力轴）

- 写 `selfClaimRules` / `realSelfBehaviors` / `selfClaimExamples` / `contradictionDescription` 之前，先填 `tensionAxes` 三轴：
  - `hardLine`：绝不退让的底线（命根子）。
  - `negotiable`：可以商量、可以绕的空间。
  - `breachSwitch`：什么现实压力会逼他破一次例，破完又如何收回。
- `tensionAxes` 是**作者侧草稿**，物化时丢弃，所以三轴内容必须**落实进运行时字段**（E2b 审计：运行时文本须同时命中底线词 + 条件/可让词）。

### M3 · 条件化去铁板（禁绝对词）

- 禁止「永远/绝不/总是」式绝对断言；底线要用「若/但凡/除非/一旦…」裹住，让规则可被现实压力改写（E3 审计）。
- 例：不说「我绝不让出正统」，说「若有人拿钱财来掂先皇传下的名分次序，我一步不让；可他若只求在南边安身逐利，位子与情分都好商量」。

### M4 · 阶梯表态（压复读的第一手段）

- 在**索取/开价/质询/利益交换**场景（最易触发复读），让角色**先承接对方此刻的诉求与苦处，再按阶梯表态**：先认诚意/本事 → 再谈可商量处 → 最后才守住底线。绝不让角色一口抛结论。
- 这既是真人说话的自然节律，也从机制上断开「模型直接抄卡里金句」的回路。

### M5 · 异构破模板

- 多条 `selfClaimExamples` 不得共用统一问答框（如全部「如何回应…」）或统一开头（E5 审计：开头 8 字雷同即告警）。
- 同一角色的多条样本，处境、视角、语气要**互不相同**。

### M6 · 脱名测试（反脸谱）

- 文化底色（方言/礼数/称谓/意象）只是「怎么说」的共享引擎，**不解释个体是谁**。禁止用一整套族属符号（草原/雕弓/烈马、绿洲/商路/苏丹、冰原/斧盾）填充角色。
- **同王国的多个角色必须互不相同**：区分落在行为铁律 + 语言指纹 + 他在意的那一两件事上。
- **验收**：遮住姓名，仅凭口吻能否认出是谁（脱名测试）。认不出 → 撞脸，重写。

### M7 · 复读自检（内容验收）

- 每张重点卡改完后，跑 8 场景本地模型自检：`persona_scenario8_focused.ps1`，核对 `checks.anyReplyRepeatedSlogan` 必须为 `false`（输出 `docs/evidence/*-scenario8.json`）。
- 这是把「金句败坏」挡在交付前的最终手段：静态 lint 拦不住的复读，靠这一步兜底。

### 门禁与工具映射

| 规范 | 自动化审计 | 入口 |
|---|---|---|
| M1 条数窗口 / M2 三轴齐备 / E2b 张力入运行时 / M3 条件化 / E4 破例落点 / M5 异构 | `audit-character-enhancement.ps1`（E1–E5） | 第五门禁（W6 四门禁之外） |
| M7 复读自检 | `persona_scenario8_focused.ps1` | 8 场景本地模型验收 |

> 写作顺序：写卡 → 填三轴 → 写自由叙事样本（禁金句、禁绝对词、阶梯表态、脱名测试）→ 跑 `audit-character-enhancement.ps1` 全绿 → 跑 8 场景复读自检 → 现四门禁 → 交付。

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
| v3 | 2026-09-13 | ①新增 W9 完成定义（七道门禁绑死，"字段合法≠质量合格"）；②`audit-character-enhancement.ps1` 的 E5 由「只认『如何回应』四字全等」升级为「问答框动词族 + 占比 ≥50% 判 FAIL」，堵住换动词绕过；③`persona_scenario8_focused.ps1` 复读判定由布尔升级为 10 字滑窗比例（mid-adapt 计入），并支持 `-Cards`；status 增 `repetition_detected` | E5 命中卡数 **26 → 64**（新抓 38 张）；拉盖娅/那得娅未被误伤；两脚本 `Parser::ParseFile` 0 错误、BOM `efbbbf` 完好；全量基线 `docs/persona-quality-baseline-20260913.json`（76 卡 / 过 2 / 败 74） |
| v2 | 2026-09-13 | 新增「人格表达质量规范」章节 M1–M7（反金句 / 三张力轴 / 条件化 / 阶梯表态 / 脱名测试） | 8 场景 qwen2.5 实测（拉盖娅「名分不是布帛」复读案例） |
| v1 | 2026-09-11 | 初版 W1–W8 取证与门禁规则 | 5 次真实性误判复盘（阿庇斯·瓦罗斯误判为"游戏内无此人"） |