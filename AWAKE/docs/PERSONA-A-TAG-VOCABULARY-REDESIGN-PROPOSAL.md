# A 类修复 · 角色词库重设计提案与改动清单

> 状态：**`superseded`（2026-09-30 标记）。已被 `PERSONA-A-TAG-VOCABULARY-REDESIGN-PROPOSAL-v3.md` 取代（v3 已实施）。**
>
> ⚠️ 本文件原写「提案待审」—— **但它的方向已被否**（v2 指出 v1「规模偏保守、有把角色压扁平的风险」）。
> **不要据本文件实施任何修改。** 现行形态见 v3。

---

## 1. 设计原则

1. **三层分离**：全局约束（所有人都守的规矩）/ 角色文字描述（这个人是谁）/ 角色标签词库（性格硬锚点），各司其职，互不越界。
2. **标签只做开关，不做描述**：能在 `core` / `description` 里用中文说清楚的，不要硬挤成标签。标签的职责是「给模型一个明确的、可条件触发的维度锚点」。
3. **适度规模**：目标 **22–25 个标签**。太少区分不开人，太多则提示词预算爆炸、个性文字被挤掉。
4. **数据驱动扩选**：优先从「编译器 19 键 ∪ 运行时 8 键 ∪ 卡片自造高频词」里筛选，不凭空发明。
5. **冲突少而准**：只标记真正互斥的对（如 direct↔indirect），其余允许共存。

---

## 2. 第一层 · 全局约束（所有人都守，从词库移出）

### 2.1 新增/确认的全局约束令牌

| 令牌 | 对应旧标签 | 作用 | 代码位置 |
|---|---|---|---|
| `CONSTRAINT_NO_INSTANT_SUBMISSION` | `boundary.no_instant_submission` | 不得仅因玩家发话就归顺/爱慕/原谅/信任 | `PersonaDslGenerator.BuildCanonicalConstraintTokens` |
| `CONSTRAINT_NO_MODERN_PSYCHOLOGY` | `boundary.no_modern_psychology` | 不得用现代心理学术语或现代价值观替代角色反应 | 同上 |

### 2.2 与既有约束的关系

- 既有 `CONSTRAINT_NO_OBEDIENCE_AUTO_ESCALATION`（不自动服从）与 `NO_INSTANT_SUBMISSION` 语义接近但侧重不同：前者管「服从/听命」，后者管「立场/情感转变」。两者**并存**，不合并。
- 全局约束总数从 7 → **9**。

---

## 3. 第三层 · 词库重设计（23 标签，5 类）

> 图例：★ = 运行时 8 键原有；● = 编译器 19 键原有；▲ = 从自造词升格；✦ = 近名漂移修正（如 pride→proud）

### 3.1 trait · 性格底色（7）

| id | 中文名 | 含义 | promptText | 来源 |
|---|---|---|---|---|
| `trait.pragmatic` | 务实 | 优先考虑可执行利益 | 先算代价与收益 | ★● |
| `trait.status_conscious` | 重视身份 | 对身份、礼法和地位敏感 | 在意身份、礼法与地位 | ★ |
| `trait.cautious` | 谨慎 | 不轻易冒险，谋定而后动 | 不冒无把握的险 | ● |
| `trait.ambitious` | 野心勃勃 | 追求更高的权力与地位 | 盘算向上的路径 | ● |
| `trait.proud` | 自尊强烈 | 对名誉和脸面看得极重 | 不容轻侮与冒犯 | ● |
| `trait.guardian` | 护卫自己人 | 把家人/族人/亲信的安危放在首位 | 先护自己人，再谈别的 | ● |
| `trait.traditional` | 重视传统 | 信奉旧制、礼法与祖宗规矩 | 凡事循旧例、讲名分 | ● |

### 3.2 expression · 说话方式（6）

| id | 中文名 | 含义 | promptText | 来源 |
|---|---|---|---|---|
| `expression.measured` | 克制 | 表达不轻易失控 | 措辞克制，不轻易暴露底牌 | ★● |
| `expression.indirect` | 含蓄 | 常用暗示、试探和迂回表达 | 多用暗示、试探和迂回 | ★ |
| `expression.direct` | 直白明确 | 有话直说，不绕弯子 | 开门见山，不绕圈子 | ● |
| `expression.formal` | 礼貌正式 | 讲究称谓、措辞和礼数 | 措辞得体，讲究礼节 | ● |
| `expression.warm` | 温和亲近 | 语气柔和，容易拉近关系 | 语气温和，待人亲近 | ● |
| `expression.teasing` | 喜欢戏谑试探 | 爱用玩笑、调侃来试探对方 | 用玩笑和调侃探底 | ● |

**对立极**：`indirect` ↔ `direct`（互为冲突，见 §4）。

### 3.3 behavior · 做事方式（7）

| id | 中文名 | 含义 | promptText | 来源 |
|---|---|---|---|---|
| `behavior.conditional_cooperation` | 有条件合作 | 合作需要理由、利益或承诺 | 合作前提出条件或索取代价 | ★ |
| `behavior.bargains` | 先谈条件 | 凡事先讲价、找筹码 | 先谈条件再做事 | ● |
| `behavior.observes_before_acting` | 先观察再行动 | 不急于出手，先摸清楚局势 | 先看清楚再动 | ● |
| `behavior.tests_loyalty` | 习惯试探忠诚 | 对身边人的可靠性保持怀疑 | 不轻信，反复试探 | ● |
| `behavior.keeps_leverage` | 习惯留后手 | 总给自己留退路或把柄 | 凡事留一手 | ● |
| `behavior.protects_inner_circle` | 优先保护自己人 | 决策时向亲信倾斜 | 自己人优先 | ● |
| `behavior.takes_command` | 倾向直接主导 | 喜欢掌控局面、发号施令 | 不由分说，先把局面拿住 | ● |

### 3.4 trigger · 触发点（3）

| id | 中文名 | 含义 | promptText | 来源 |
|---|---|---|---|---|
| `trigger.threat_or_leverage` | 威胁与筹码 | 危险、把柄和利益会改变表达强度 | 遇到威胁、把柄或明确筹码时提高警惕 | ★ |
| `trigger.public_humiliation` | 公开羞辱 | 被当众羞辱时会强烈反击 | 当众受辱必反击 | ● |
| `trigger.family_safety` | 家人安危 | 家人/族人受到威胁时态度剧变 | 家人有危险时不惜一切 | ▲（8 卡高频） |

### 3.5 boundary · 红线（0）

> 原 2 个 boundary 标签全部升为第一层全局约束（§2）。
> boundary 分类机制保留，未来若有**角色级**红线（如某贵族绝不下跪）可再加入。

### 3.6 合计

7 trait + 6 expression + 7 behavior + 3 trigger + 0 boundary = **23 个标签**。

与现状对比：
- 运行时 8 键 → 保留 6 个（2 个升全局）+ 从编译器 19 键恢复 16 个 + 从自造词升格 1 个 = 23
- 编译器 19 键 → 16 个保留（2 个 trigger/boundary 原就有；3 个「自造键但近名漂移」已修正为正确形式：`pride→proud`、`caution→cautious`、`tradition→traditional`）

---

## 4. 冲突对（conflicts）

只标记真正意义上互斥的对，其余允许共存（一个人可以既务实又有野心）。

| 标签 A | 标签 B | 理由 |
|---|---|---|
| `expression.indirect` | `expression.direct` | 说话不可能既含蓄绕弯又开门见山 |
| `trait.cautious` | `behavior.takes_command` | （可选，讨论项）谨慎谋定与不由分说主导存在张力 |

**默认只采纳第 1 对**。第 2 对留待观察——很多人物（如蒙楚格）既谨慎又强势，硬冲突可能不合理。

---

## 5. bundle · 人物原型包（3 个）

> bundle 是「一组常一起出现的标签」，方便作者快速给角色打基础，再加减个别标签。

| bundle id | 中文名 | 包含标签 | 典型人物 |
|---|---|---|---|
| `bundle.courtier` | 宫廷官僚 | pragmatic, status_conscious, measured, indirect, formal, conditional_cooperation, bargains, keeps_leverage, observes_before_acting, tests_loyalty, threat_or_leverage | 德泰尔、拉盖娅、加里奥斯 |
| `bundle.warrior` | 沙场武将 | proud, guardian, direct, warm, takes_command, protects_inner_circle, public_humiliation, family_safety | 阿隆·德拉古、埃隆、梅利迪尔 |
| `bundle.patriarch` | 家族族长 | guardian, traditional, status_conscious, measured, formal, protects_inner_circle, family_safety, threat_or_leverage | 卢钱、蒙楚格、温金迪 |

---

## 6. 改动清单（文件级，按实施顺序）

### Phase 0 · 准备（不动产出，只加工具）

| # | 文件 | 改动 | 目的 |
|---|---|---|---|
| 0.1 | `tools/persona-workbench/tools/compile-verify.ps1`（新增） | 封装 `dotnet run` 式编译验证，逐卡返回错误码 | 作为改动后的门禁，防止再出现「schema 过了但编译器拒了」 |

### Phase 1 · 第一层：全局约束（运行时）

| # | 文件 | 改动 |
|---|---|---|
| 1.1 | `src/PersonaDslGenerator.cs` `BuildCanonicalConstraintTokens` | 新增 `CONSTRAINT_NO_INSTANT_SUBMISSION`、`CONSTRAINT_NO_MODERN_PSYCHOLOGY` 两个令牌 |
| 1.2 | `src/NpcPromptTemplate.cs` 或提示词侧 | （如果令牌需要对应中文解释）补充令牌释义映射 |

> 注：令牌本身是大写常量，模型靠的是提示词里的语义。如果当前提示词只塞代号、不附中文释义，需要同步加释义段，否则模型看不懂新令牌。

### Phase 2 · 第三层：词库（运行时 registry）

| # | 文件 | 改动 |
|---|---|---|
| 2.1 | `ModuleData/Worldbook/persona_definitions/tag_registry.json` | 从 8 键重写为 23 键（§3），含 conflicts、bundles |
| 2.2 | `docs/persona-contract/awake.persona.tags.v1.schema.json` | 确认 schema 已匹配（category 枚举、字段结构） |

### Phase 3 · 编译器侧对齐

| # | 文件 | 改动 |
|---|---|---|
| 3.1 | `tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs` `PersonaTagRegistry.CreateDefault()` | **删除 19 键硬编码**，改为从嵌入的 `tag_registry.json` 加载，与运行时同源 |
| 3.2 | `PersonaValidator.Validate` | 轴值范围 `-2..2` 与 schema `-3..3` 对齐 → **统一为 `-3..3`**（改 validator，而不是改 schema，因为 schema 更宽且作者已按宽的写） |
| 3.3 | `facet.strength` 范围 | schema `0..5` 与 validator `1..4` → **统一为 `1..5`**（0 表示未设置，不写进 facetStrengths） |
| 3.4 | `PersonaDocumentCodec` `AllowedProperties` 白名单 | **移除 `foodPreference`**（schema 已删），**不加 `origins`**（见 §7 决策项） |
| 3.5 | `PersonaWorkbench.Core.csproj` 嵌入资源 | 确认 `tag_registry.json` 嵌入路径与实际一致 |

### Phase 4 · schema 收口

| # | 文件 | 改动 |
|---|---|---|
| 4.1 | `tools/persona-workbench/contracts/persona-workbench.character.v1.schema.json` | `tags.items` 从自由 string 改为 **enum（23 个合法值）** |
| 4.2 | 同上 | `facetStrengths.additionalProperties` 保留数值约束，但 key 的枚举校验通过 `allOf` + `propertyNames` 实现（或保留开放，依赖 validator 拦截——倾向加 enum 收口） |
| 4.3 | 同上 | `traitProfile`/`expressionProfile`/`behaviorProfile`/`reactionProfile`/`commitmentProfile` 从 `additionalProperties` 改为**显式 properties**，轴名枚举化 |
| 4.4 | 同上 | 轴值 `minimum/maximum` 从 `-3/3` → 确认与 Phase 3.2 一致 |
| 4.5 | 同上 | `facetStrengths` 值范围与 Phase 3.3 一致 |
| 4.6 | 同上 | 移除 `foodPreference`（已在之前的 A 类修复中移除，确认残留） |
| 4.7 | 同上 | **移除 `origins`**（旁挂为 sidecar，不进入角色卡主体） |

### Phase 5 · crosswalk 同步

| # | 文件 | 改动 |
|---|---|---|
| 5.1 | `docs/persona-contract/persona-workbench-to-awake.crosswalk.v1.json` | `sourceVocabulary.tagIds` 从 19 → 23（与新 registry 对齐） |
| 5.2 | 同上 | `sourceVocabulary.facetStrengthIds` 同步（排除 trigger/boundary 类） |
| 5.3 | 同上 | 新增 16 个标签对应的 row（`sourceKind=tag` / `sourceKind=facet_strength`），lossPolicy 暂置 `preserve_only`（先让编译通过，emit 映射后续再定） |
| 5.4 | 同上 | 移除 2 个 boundary 标签的 row（已升为全局约束） |
| 5.5 | 同上 | `targetRegistry.sha256` 更新为新 registry 的哈希 |

### Phase 6 · 卡片批量修正

| # | 工作 | 说明 |
|---|---|---|
| 6.1 | 移除 `origins` 字段 | 46 张卡都有，整体删除 |
| 6.2 | 修正近名漂移 | `trait.caution→cautious`、`trait.pride→proud`、`trait.tradition→traditional` |
| 6.3 | 自造键 → 保留/翻译/删除 | 逐键决策：在新词库中的保留；近义的映射；其余删除（语义已在文字描述里） |
| 6.4 | 轴值范围校准 | 36 张卡 ±3 越界 → 如果统一为 -3..3 则无需改；如果统一为 -2..2 则需裁剪 |
| 6.5 | facet 值范围校准 | 按新统一范围修正 |
| 6.6 | 移除两个 boundary 标签 | `no_instant_submission`、`no_modern_psychology`（已升全局） |
| 6.7 | 过编译验证 | `compile-verify.ps1` 全绿 = 过关 |

### Phase 7 · 门禁与文档

| # | 文件 | 改动 |
|---|---|---|
| 7.1 | `tools/persona-workbench/tools/audit-character-schema.ps1` | 同步新 schema 路径 |
| 7.2 | 门禁链 | 加入 `compile-verify.ps1` 一步，编译失败即拦截 |
| 7.3 | `docs/PERSONA-A-CONSTRAINT-MISMATCH-COMPILE-VERIFY.md` | 标记为已修复，附修复前后对比 |
| 7.4 | `AGENTS.md` 或工作区规范 | 补充词库变更流程（改 registry → 同步 schema/crosswalk/编译器 → 全量门禁） |

---

## 7. 待决策项（实施前需拍板）

1. **轴值范围**：统一为 `-3..3`（schema 现状，作者已按此写）还是 `-2..2`（validator 现状，更严格）？倾向 `-3..3`（作者已写大量 ±3，且更细粒度没坏处）。
2. **facet 值范围**：统一为 `1..5`（schema 现状）还是 `1..4`（validator 现状）？倾向 `1..5`。
3. **`origins` 去向**：
   - 方案 A（推荐）：从角色卡 JSON 移除，旁挂为 `*.origins.json` sidecar 文件，仅供门禁脚本校验使用，不进入编译链。
   - 方案 B：保留在角色卡 JSON 里，编译器 `PersonaDocument` 增 `Origins` 属性并加入白名单，但迁移时 `omit` 不进运行时。
4. **冲突对数量**：只采纳 `indirect↔direct` 1 对，还是多加几对？倾向从简。
5. **bundle 数量与命名**：3 个原型包够不够？需不需要增加「学者型」「商人型」等？
6. **promptText 中文释义注入**：当前生成器只把标签转成大写代号（如 `TRAIT_PRAGMATIC`）塞进提示词，**registry 里的中文 `promptText` 并未使用**。扩词库后是否同步让生成器把 `promptText` 也注入提示词？这影响标签的实际效果。

---

## 8. 实施顺序建议

**先做 0 → 1 → 2 → 3**（工具链对齐，不动卡片），这四步做完后编译器和运行时共享同一份 23 键词库。再做 4 → 5（schema 和 crosswalk 收口），最后做 6（卡片批量修正）+ 7（门禁）。

原因：先把「权威真相源」统一了，再改卡片才有明确的对错标准，不然改到一半又变口径。