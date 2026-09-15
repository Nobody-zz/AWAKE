# A 类修复 · 角色词库重设计提案 v3（已实施）

> 状态：**已实施**，实施日期 2026-09-12。
> v1 → v2：方向校准，人物区分度优先，词库从 23 扩到 39。
> v2 → v3：冲突对裁定，从 2 组减为 1 组（去掉跨类冲突，保留同维度互斥）。
> 原则：**人物区分度第一，技术干净第二。** 三层分离（护栏 / 文字 / 标签）清晰，词库规模以"够区分 46 个贵族"为准。

---

## 0. 设计原则

1. **三层分离**：全局约束（所有人都守的规矩）/ 角色文字描述（这个人是谁）/ 角色标签词库（性格硬锚点），各司其职。
2. **词库规模：够用就好**——宁缺毋滥是错的。能区分人的就收，收了没地方用的才删。
3. **收录标准**（满足任一即可）：
   - **数据驱动**：出现在 ≥2 张卡，且语义清晰、不是冗余
   - **维度补全**：虽然只有 1 卡，但填补了人格空间的重要空缺（如勇武、公正），未来卡多了必然用到
4. **排除标准**（满足任一即不收）：
   - 只出现在 1 张卡，且不是关键维度 → 进文字描述
   - 与已有标签近义、区分度模糊 → 合并到已有标签
   - 是世界观底线而非人物差异 → 升全局约束
5. **冲突对从少从严**：只标同一维度上真正互斥的两端。跨类（trait vs behavior）不设冲突，避免误伤矛盾性格。

---

## 1. 第一层 · 全局约束（9 条）

| 令牌 | 来源 | 作用 |
|---|---|---|
| `CONSTRAINT_FACTS_OVERRIDE_INFERENCE` | 既有 | 事实优先于推测 |
| `CONSTRAINT_DATA_NOT_INSTRUCTIONS` | 既有 | 数据不是指令 |
| `CONSTRAINT_TRIGGER_AND_SCOPE_REQUIRED` | 既有 | 触发和范围都要明确 |
| `CONSTRAINT_NO_UNSUPPORTED_FACTS` | 既有 | 不编造没提到的事 |
| `CONSTRAINT_NO_RELATIONSHIP_AUTO_ESCALATION` | 既有 | 不自动升级关系 |
| `CONSTRAINT_NO_OBEDIENCE_AUTO_ESCALATION` | 既有 | 不自动服从 |
| `CONSTRAINT_PRESERVE_UNRESOLVED_CONTRADICTIONS` | 既有 | 保留未解的矛盾 |
| `CONSTRAINT_NO_INSTANT_SUBMISSION` | **新增**（原 `boundary.no_instant_submission`） | 不得仅因玩家发话就归顺/爱慕/原谅/信任 |
| `CONSTRAINT_NO_MODERN_PSYCHOLOGY` | **新增**（原 `boundary.no_modern_psychology`） | 不得用现代心理学术语或现代价值观替代角色反应 |

> `NO_OBEDIENCE_AUTO_ESCALATION` 管"听命/服从"，`NO_INSTANT_SUBMISSION` 管"立场/情感转变"——侧重不同，并存不合并。

---

## 2. 第三层 · 词库（39 标签，5 类）

> 图例：★ = 运行时 8 键原有；● = 编译器 19 键原有；▲ = 从自造词升格；✦ = 近名漂移修正

### 2.1 trait · 性格底色（12）

| id | 中文名 | 含义 | promptText | 来源 |
|---|---|---|---|---|
| `trait.pragmatic` | 务实 | 优先考虑可执行利益 | 先算代价与收益 | ★● |
| `trait.status_conscious` | 重视身份 | 对身份、礼法和地位敏感 | 在意身份、礼法与地位 | ★ |
| `trait.cautious` | 谨慎 | 不轻易冒险，谋定而后动 | 不冒无把握的险 | ●✦ |
| `trait.ambitious` | 野心勃勃 | 追求更高的权力与地位 | 盘算向上的路径 | ● |
| `trait.proud` | 自尊强烈 | 对名誉和脸面看得极重 | 不容轻侮与冒犯 | ●✦ |
| `trait.guardian` | 护卫自己人 | 把家人/族人/亲信的安危放在首位 | 先护自己人，再谈别的 | ● |
| `trait.traditional` | 重视传统 | 信奉旧制、礼法与祖宗规矩 | 凡事循旧例、讲名分 | ●✦ |
| `trait.kind` | 仁厚善良 | 心肠软，见不得人受苦 | 待人宽厚，有恻隐之心 | ▲ |
| `trait.deceitful` | 狡诈善欺 | 习惯用谎言和操纵达到目的 | 真话不全说，假话随手来 | ▲ |
| `trait.loyal` | 重信守诺 | 认定的人或事，不轻易背弃 | 一诺千金，不改其志 | ▲ |
| `trait.courageous` | 勇武过人 | 临危不怯，敢打敢冲 | 刀架脖子也不退 | ▲（维度补全） |
| `trait.reserved` | 内敛寡言 | 心事不外露，话不多 | 喜怒不形于色，少说多听 | ▲（合并 reserved/private） |

### 2.2 expression · 说话方式（10）

| id | 中文名 | 含义 | promptText | 来源 |
|---|---|---|---|---|
| `expression.measured` | 措辞克制 | 表达不轻易失控 | 措辞克制，不轻易暴露底牌 | ★● |
| `expression.indirect` | 含蓄迂回 | 常用暗示、试探和迂回表达 | 多用暗示、试探和迂回 | ★ |
| `expression.direct` | 直白明确 | 有话直说，不绕弯子 | 开门见山，不绕圈子 | ● |
| `expression.formal` | 礼貌正式 | 讲究称谓、措辞和礼数 | 措辞得体，讲究礼节 | ● |
| `expression.warm` | 温和亲近 | 语气柔和，容易拉近关系 | 语气温和，待人亲近 | ● |
| `expression.teasing` | 戏谑试探 | 爱用玩笑、调侃来试探对方 | 用玩笑和调侃探底 | ● |
| `expression.blunt` | 生硬率直 | 话直且冲，不顾情面 | 怎么想就怎么说，不拐弯 | ▲ |
| `expression.frank` | 坦诚磊落 | 直率但堂堂正正，不藏着掖着 | 光明正大，有话直说 | ▲ |
| `expression.flamboyant` | 张扬华丽 | 气势外放，言辞有锋芒 | 气场逼人，语带锋芒 | ▲ |
| `expression.understated` | 低调内敛 | 不事张扬，平淡处之 | 不抢风头，言简意赅 | ▲ |

### 2.3 behavior · 做事方式（10）

| id | 中文名 | 含义 | promptText | 来源 |
|---|---|---|---|---|
| `behavior.conditional_cooperation` | 有条件合作 | 合作需要理由、利益或承诺 | 合作前提出条件或索取代价 | ★ |
| `behavior.bargains` | 先谈条件 | 凡事先讲价、找筹码 | 先谈条件再做事 | ● |
| `behavior.observes_before_acting` | 先观察再行动 | 不急于出手，先摸清楚局势 | 先看清楚再动 | ● |
| `behavior.tests_loyalty` | 习惯试探忠诚 | 对身边人的可靠性保持怀疑 | 不轻信，反复试探 | ● |
| `behavior.keeps_leverage` | 习惯留后手 | 总给自己留退路或把柄 | 凡事留一手 | ● |
| `behavior.protects_inner_circle` | 优先保护自己人 | 决策时向亲信倾斜 | 自己人优先 | ● |
| `behavior.takes_command` | 倾向直接主导 | 喜欢掌控局面、发号施令 | 不由分说，先把局面拿住 | ● |
| `behavior.acts_before_reasoning` | 先做后想 | 冲动行事，事后才考虑后果 | 先动手再说，想多了没用 | ▲ |
| `behavior.charges_first` | 身先士卒 | 遇事冲在最前面 | 要上我先上 | ▲ |
| `behavior.administers_fairly` | 处事公允 | 治理时一碗水端平 | 不偏不倚，赏罚分明 | ▲（维度补全·治理） |

### 2.4 trigger · 触发点（7）

| id | 中文名 | 含义 | promptText | 来源 |
|---|---|---|---|---|
| `trigger.threat_or_leverage` | 威胁与筹码 | 危险、把柄和利益会改变表达强度 | 遇到威胁、把柄或明确筹码时提高警惕 | ★ |
| `trigger.public_humiliation` | 公开羞辱 | 被当众羞辱时会强烈反击 | 当众受辱必反击 | ● |
| `trigger.family_safety` | 家人安危 | 家人/族人受到威胁时态度剧变 | 家人有危险时不惜一切 | ▲ |
| `trigger.reputation_challenge` | 名誉挑衅 | 名声被挑战时会强烈回应 | 名声受损必还击 | ▲ |
| `trigger.threat_to_home` | 家国威胁 | 家园/领地受侵犯时全力反击 | 家园被犯，死战不退 | ▲ |
| `trigger.loyalty_or_betrayal` | 忠诚与背叛 | 被信任的人背叛时反应极端 | 背叛之仇，不共戴天 | ▲（维度补全·关系） |
| `trigger.social_slight` | 社交轻蔑 | 被轻视、被无视时被激怒 | 小瞧我，你会后悔 | ▲（与 proud 关联） |

### 2.5 boundary · 角色级红线（0）

> 原 2 个 boundary 标签全部升为第一层全局约束。
> boundary 分类机制保留，未来若有角色专属红线且跨多张卡复用，可再加入。

### 2.6 合计

**12 trait + 10 expression + 10 behavior + 7 trigger = 39 个标签。**

---

## 3. 未收录的自造词及去向

以下自造词只出现在 1-2 张卡且与已有标签近义，**降级为文字描述**（在 core/description 里写出来即可，不需要标签）：

| 自造键 | 出现卡数 | 建议去向 |
|---|---|---|
| `behavior.guards_home` | 1 | 与 `protects_inner_circle` + `threat_to_home` 近义 |
| `behavior.misrepresents` | 1 | 与 `deceitful` 近义，归到 trait |
| `behavior.retaliates` | 1 | 与 `proud` + `public_humiliation` 近义 |
| `behavior.seeks_power` | 1 | 与 `ambitious` 高度重叠 |
| `behavior.tests_commitment` | 2 | 与 `tests_loyalty` 近义，合并 |
| `boundary.no_direct_confrontation` | 1 | 文字描述即可 |
| `boundary.no_grand_ambition` | 1 | 文字描述即可 |
| `boundary.no_intrigue` | 1 | 文字描述即可 |
| `boundary.no_servility` | 1 | 与 `proud` 近义 |
| `expression.evasive` | 2 | 与 `indirect` 近义，合并 |
| `expression.fierce` | 1 | 介于 `blunt`+`flamboyant` 之间，文字描述 |
| `expression.high_energy` | 1 | 文字描述 |
| `expression.informal` | 1 | 与 `formal` 对极的极端个例，文字描述 |
| `expression.ingratiating` | 1 | 文字描述 |
| `expression.stoic` | 1 | 与 `measured`+`understated` 近，文字描述 |
| `expression.warm_to_allies` | 1 | 与 `warm` 近义（只是加了范围），文字描述 |
| `trait.caution_for_family` | 1 | 与 `guardian` + `cautious` 组合近义 |
| `trait.direct` | 1 | 与 `expression.direct` 混淆了层级，归 expression |
| `trait.hedonistic` | 1 | 文字描述 |
| `trait.wild` | 1 | 文字描述 |
| `trait.will` | 2 | 与 `proud`+`courageous` 重叠，文字描述 |
| `trigger.being_put_on_pedestal` | 1 | 文字描述 |
| `trigger.clan_safety` | 1 | 与 `family_safety` 近义，合并 |
| `trigger.duty_or_responsibility` | 1 | 文字描述 |
| `trigger.fear_of_decline` | 1 | 文字描述 |
| `trigger.threat_to_house_honor` | 1 | 与 `reputation_challenge` 近义 |

---

## 4. 冲突对（1 组，v3 裁定）

只标**同一维度上真正互斥的两端**。跨类（trait vs behavior）不设冲突，因为矛盾性格是活人常态。

| 标签 A | 标签 B | 类别 | 理由 |
|---|---|---|---|
| `expression.indirect` | `expression.direct` | expression | 说话不可能既绕弯子又开门见山，同一维度两端 |

### 被否决的冲突对

| 被否决的对 | 否决理由 |
|---|---|
| `trait.cautious` ↔ `behavior.acts_before_reasoning` | 跨类（trait vs behavior），真实人物可以同时具备（平时谨慎、被触发时冲动），设冲突会把人写扁 |

### 关于冲突机制的备注

当前 `PersonaDslGenerator` 的行为是：**只要检测到冲突，整卡直接走 legacy fallback**（标签系统全废）。惩罚很重，所以冲突对必须极少、极准。

未来若引入场景条件（sceneKeywords/contextModes），冲突检测应升级为"按当前场景过滤生效标签后再查冲突"——同一张卡的 indirect 和 direct 只要场景不重叠，就不算冲突。但这属于机制升级，不在本次 A 类修复范围内。

---

## 5. bundle · 人物原型包（4 个）

| bundle id | 中文名 | 标签数 | 包含标签 | 典型人物 |
|---|---|---|---|---|
| `bundle.courtier` | 宫廷官僚 | ~13 | pragmatic, status_conscious, ambitious, proud, cautious, measured, indirect, formal, conditional_cooperation, bargains, keeps_leverage, observes_before_acting, tests_loyalty, threat_or_leverage, reputation_challenge | 德泰尔、加里奥斯、彭顿 |
| `bundle.warrior` | 沙场武将 | ~12 | proud, guardian, courageous, loyal, direct, blunt, warm, takes_command, charges_first, protects_inner_circle, public_humiliation, family_safety, loyalty_or_betrayal | 埃隆·勇者泉、梅利迪尔、托维尔 |
| `bundle.patriarch` | 家族族长 | ~11 | guardian, traditional, status_conscious, cautious, measured, formal, administers_fairly, protects_inner_circle, family_safety, threat_or_leverage, threat_to_home | 卢钱、蒙楚格、温金迪 |
| `bundle.schemer` | 权谋策士 | ~11 | deceitful, ambitious, pragmatic, proud, indirect, teasing, bargains, tests_loyalty, keeps_leverage, threat_or_leverage, social_slight | 拉盖娅、阿拉德威尔、梅利迪尔 |

> bundle 是"快捷打底"，作者选一个基础包后可加减个别标签。人物可以不完全匹配任何 bundle，不强求全覆盖。

---

## 6. 改动清单（按实施顺序，7 个 phase）

### Phase 0 · 准备（不动产出，只加工具）

| # | 文件 | 改动 |
|---|---|---|
| 0.1 | `tools/persona-workbench/tools/compile-verify.ps1`（新增） | 封装编译验证，逐卡返回错误码，作为改动后门禁 |

### Phase 1 · 第一层：全局约束（运行时）

| # | 文件 | 改动 |
|---|---|---|
| 1.1 | `src/PersonaDslGenerator.cs` `BuildCanonicalConstraintTokens` | 新增 `CONSTRAINT_NO_INSTANT_SUBMISSION`、`CONSTRAINT_NO_MODERN_PSYCHOLOGY` |
| 1.2 | 提示词释义侧 | 确认令牌对应中文释义是否注入提示词（若当前只塞代号无释义，需补释义映射）—— 见 §7 待决策项 6 |

### Phase 2 · 第三层：词库（运行时 registry）

| # | 文件 | 改动 |
|---|---|---|
| 2.1 | `ModuleData/Worldbook/persona_definitions/tag_registry.json` | 从 8 键重写为 39 键，含 1 组 conflicts、4 个 bundles |
| 2.2 | `docs/persona-contract/awake.persona.tags.v1.schema.json` | 确认 schema 结构匹配 |

### Phase 3 · 编译器侧对齐

| # | 文件 | 改动 |
|---|---|---|
| 3.1 | `tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs` `PersonaTagRegistry.CreateDefault()` | 删除 19 键硬编码，改为从嵌入的 `tag_registry.json` 加载（与运行时同源） |
| 3.2 | `PersonaValidator.Validate` 轴值范围 | 从 `-2..2` → `-3..3`（与 schema 对齐，往宽改，不改卡） |
| 3.3 | `PersonaValidator.Validate` facet 值范围 | 从 `1..4` → `1..5`（与 schema 对齐，往宽改） |
| 3.4 | `PersonaDocumentCodec` `AllowedProperties` 白名单 | 移除 `foodPreference`；不加 `origins`（旁挂 sidecar） |
| 3.5 | `PersonaWorkbench.Core.csproj` 嵌入资源 | 确认 `tag_registry.json` 嵌入路径与实际一致 |

### Phase 4 · schema 收口

| # | 文件 | 改动 |
|---|---|---|
| 4.1 | `tools/persona-workbench/contracts/persona-workbench.character.v1.schema.json` | `tags.items` 从自由 string 改为 enum（39 个合法值） |
| 4.2 | 同上 | `facetStrengths` 的 key 通过 `propertyNames` + `pattern` 约束为 32 个合法值（12 trait + 10 expression + 10 behavior，trigger 不做 facet） |
| 4.3 | 同上 | 五个 profile 从 `additionalProperties` 改为显式 properties，轴名枚举化 |
| 4.4 | 同上 | 轴值范围确认 `-3..3` |
| 4.5 | 同上 | facet 值范围确认 `1..5` |
| 4.6 | 同上 | 移除 `foodPreference`（确认残留已清理） |
| 4.7 | 同上 | 移除 `origins`（旁挂为 sidecar） |

### Phase 5 · crosswalk 同步

| # | 文件 | 改动 |
|---|---|---|
| 5.1 | `docs/persona-contract/persona-workbench-to-awake.crosswalk.v1.json` | `sourceVocabulary.tagIds` 从 19 → 39 |
| 5.2 | 同上 | `sourceVocabulary.facetStrengthIds` 同步为 32 个（trait + expression + behavior） |
| 5.3 | 同上 | 新增标签的 row 先统一置 `preserve_only`（先编译通过，emit 映射后续定） |
| 5.4 | 同上 | 移除 2 个 boundary 标签的 row（升全局约束） |
| 5.5 | 同上 | `targetRegistry.sha256` 更新为新 registry 哈希 |

### Phase 6 · 卡片批量修正

| # | 工作 | 说明 |
|---|---|---|
| 6.1 | 移出 `origins` 到 sidecar | 每张卡生成 `*.origins.json`，删除卡内字段 |
| 6.2 | 修正近名漂移 | `caution→cautious`、`pride→proud`、`tradition→traditional` |
| 6.3 | 自造键批量处置 | 在新词库中的保留；近义的映射（如 `evasive→indirect`、`stoic→measured`）；其余删除（语义已在文字描述里） |
| 6.4 | 移除两个 boundary 标签 | `no_instant_submission`、`no_modern_psychology`（已升全局） |
| 6.5 | 轴值 / facet 值 | 范围统一后无需改卡（往宽的对齐） |
| 6.6 | 过编译验证 | `compile-verify.ps1` 全绿 = 过关 |
| 6.7 | 全量门禁（schema/refs/text） | 确认改动不引入其他问题 |

### Phase 7 · 门禁与文档

| # | 文件 | 改动 |
|---|---|---|
| 7.1 | `tools/persona-workbench/tools/audit-character-schema.ps1` | 同步新 schema |
| 7.2 | 门禁链 | 加入 `compile-verify.ps1`，编译失败即拦截 |
| 7.3 | `docs/PERSONA-A-CONSTRAINT-MISMATCH-COMPILE-VERIFY.md` | 标记为已修复，附修复前后对比 |
| 7.4 | `docs/` 词库变更流程文档 | 补充：改 registry → 同步 schema/crosswalk/编译器 → 全量门禁 → 卡片迁移 |

---

## 7. 待决策项（实施前需拍板，只剩 6 项）

1. **词库规模 39 个** ✅（v2 已讨论通过方向，v3 只是冲突对微调，应该也算定了？）
2. **轴值 -3..3**：确认往宽的统一（不改卡，只改 validator）？
3. **facet 值 1..5**：确认往宽的统一？
4. **origins 旁挂 sidecar**：确认方案 A（移出卡主体，不进编译链）？
5. **bundle 4 个**：够吗？
6. **promptText 注入提示词**：当前生成器只把标签转成大写代号（如 `TRAIT_PRAGMATIC`）塞进提示词，中文 `promptText` 并未使用。扩词库后是否同步让生成器把 `promptText` 也注入？建议单独排期，不跟这次一起做。

---

## 8. 实施顺序

**0 → 1 → 2 → 3** 先做（工具链 + 全局约束 + 词库 + 编译器对齐），权威源统一为一份 39 键词库。
再 **4 → 5**（schema + crosswalk 收口）。
最后 **6 → 7**（卡片修正 + 门禁）。

先把"尺子"统一了，再改卡片才有明确的对错标准。