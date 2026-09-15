# AWAKE 运行时 hero 数据读取可行性备忘（v2 数值层前置核查）

> 时间：2026-09-11 · 范围：AWAKE 运行时能否实时读取 hero 的 skills / attributes / relation
> 结论先给：**运行时已走「实时端口」路线，且部分已投产**。v2 数值层应做**投影规则/引用**，而非静态快照。
> 关联：`PLAN-*` 系列尚未落地 v2 数值层；本备忘作为 v2 设计引用的前置依据。

## 1. 三方核查结论

| 数据 | 可实时读 | 现状 | 证据位置（运行时 `src`） |
|---|---|---|---|
| **relation** | ✅ 是，已在产 | 每遇对话/事件对 `Hero.MainHero` 与目标 hero 实时取值 | `BannerlordNativeSocialReader.cs` L51–55、L61–62 |
| **skills** | ✅ 是，机制在 | 目前仅接 4 项（trade/leadership/tactics/scouting + steward→management 别名） | `NpcDialogueService.cs` L740–751（`hero.GetSkillValue`） |
| **attributes** | ❌ 未接入 | 运行时核心无一处读 `Hero.Attributes`/`GetAttributeValue`；命中全在 workbench 工具侧 | `WorldbookIdentityEvaluator.cs` 仅消费 `query.Skills` |

## 2. 关键代码锚点

```csharp
// relation —— 实时，任意英雄可取
input.BaseRelation     = sourceHero.GetBaseHeroRelation(targetHero);   // B……SocialReader.L51
input.EffectiveRelation= sourceHero.GetRelation(targetHero);          // L52
input.NativeFriendState= ToFlag(sourceHero.IsFriend(targetHero));     // L53
input.NativeEnemyState = ToFlag(sourceHero.IsEnemy(targetHero));      // L54
input.EffectiveClanRelation = sourceClan.GetRelationWithClan(targetClan); // L61

// skills —— 实时，hero 对象直读
int value = hero?.GetSkillValue(skill) ?? 0;                          // NpcDialogueService.L744
_heroSkills[key] = value;
if (key == "steward") _heroSkills["management"] = value;              // L746 别名
```

## 3. 三个重要事实

1. **relation 与部分 skills 已是实时**，不是"理论可行性"——`Capture` 接受任意 `sourceHero/targetHero`，非限玩家。
2. **人格/身份文本仍是静态预设**（`PersonaDataLoader.Load` 从 `PersonaDefinition` JSON 读 core/identityFacts/relationStyle/currentStateHints 等），与实时数值层**并存**、职责分离。
3. **`currentStateHints` / `relationStyle` 字段已存在**，恰是运行时把实时 relation 数值接入人格文本的既定缝合位。

## ⚠️ 决策记录（2026-09-11）：全量数值层降级为文档参考

**结论：v2 不再做「全量 skills/attributes 精确镜像进人格轴」这一数值层。** 原因：AI 对话是定性叙事，精确数值（如"战术 187"）进提示词是噪声且与预设人格抢戏；复杂度高、语义增益趋近于零。**保留 relation 实时缝合 + 少数关键能力定性画像为优先方向。**「百科数值对应」仅作为工具型参考（查账用），不承担 AI 驱动价值。现有 `origins` + `docs/mappings/*-tsv` 已可覆盖查对。

原设计约束与补齐计划保留如下，仅作参考，不计入 v2 门禁。

## 4.（已降级为文档参考）原 v2 数值层设计约束

- 若未来确需数值，**禁止静态快照**（runtime 已实时化）；应存投影规则/引用，含值域换算（如 skill 0–300 → 轴 −3..3）。
- 规则示例（仅示意，不推荐作为 schema 功能）：
  - `statProject: { source: "hero.skill.tactics", target: "behaviorProfile.leadership", inputRange: [0,300], outputRange: [-3,3] }`
  - `statProject: { source: "hero.relation.player", target: "reactionProfile.warmth", ... }`

## 5.（仅作参考）若做百科数值查对，运行时需补的两点（非必需）

| 补齐项 | 做法 | 参照 |
|---|---|---|
| **skills 全量** | 把 AddHeroSkill 从 4 项扩到 DefaultSkills 全集 | `NpcDialogueService.AddHeroSkill` 模式 |
| **attributes 新增** | 新增 `hero.GetAttributeValue(...)` 读取点（注意 attributes 在 `hero.CharacterObject.Attributes`，语义偏"基准/声明"而非实时状态） | 照 `AddHeroSkill` 扩写 |

## 6. 结论

- 定位确定为**混合分层**：预设层（人格/身份/宣称概率静态）+ 实时层（relation 已产、skills 部分产、attributes 待补）。
- v2 数值层 = **投影规则层**（声明 runtime 字段→人格轴换算），不落地于卡内数值快照。
- 待设计时把 `statProject` 加入 `persona-workbench.character` 契约（v2 bump），并与现有 `origins` 锚点衔接。

## 7. 决策记录：移除 foodPreference（2026-09-11）

角色卡不再含 `foodPreference` 字段：契约 `contracts\persona-workbench.character.v1.schema.json` 已删除该属性，10 张 `characters\*.persona.json` 已逐张移除该行，schema lint **10/10 PASS**。原因：该字段本就不被 AWAKE 运行时加载消费（`src` 全库对 `.FoodPreference` 零命中），属无价值死内容。

后续清理已将该字段从工作台模型、Provider 草稿契约、联合校验脚本、运行时兼容模型、活动契约和测试夹具中移除。本文及旧计划中的相关文字保留为历史记录，不再代表当前有效字段集合。

## 8. 决策记录：处理运行时断裂字段（2026-09-11）

针对持久会话中核实的「已声明/已加载但从未消费」字段，做最小且有取舍的接线。逐项结论、理由与证据：

| 字段 | 处理 | 理由与代码 |
|---|---|---|
| `ContextSnapshot.KingdomName` / `ClanName` | **补填接线** | 数据早已在服务内（`NpcDialogueService._kingdomName/_clanName`，L44–45、L572–580），但从未写入 snapshot（L948–964），使 DSL `CURRENT_IDENTITY` 的 `KINGDOM_NAME/CLAN_NAME` 恒为缺省。已在 snapshot 初始化处补齐。 |
| `PersonaContext.Relation` | **维持空（有意休眠）** | 实时关系已随 `_npcState`（`FormatState` 的"信任 X；爱意 Y；敌意 Z"）进 `{{npc_state}}` 与 DSL `CURRENT_STATE`。再开独立 `CURRENT_RELATION` 会导致同一关系文本二次入 prompt 的重复。`CURRENT_RELATION` 现由**作者 `RelationStyle` 兜底**承载定性风格，与 `CURRENT_STATE` 的实时数值并存、不重复。 |
| `Definition.Summary` | **接入 `PERSONA_IDENTITY`** | 一句话身份概括是实时数据没有的纯增量，`SUMMARY_CN="..."` 非空才注入，高价值低噪声。 |
| `Definition.RelationStyle` / `CurrentStateHints` | **接入为「实时缺失时的回退」** | `CURRENT_RELATION`/`CURRENT_STATE` 先取实时 `context.Relation`/`context.CurrentState`，空时才用作者 `RelationStyle`/`CurrentStateHints` 兜底，杜绝静态预设与实时数值叠加成噪声。 |
| `origins` | **按设计不消费** | 仅创作/稽查侧元数据（`persona-workbench.character.v1` 契约），运行时 `awake.persona.definition.v1` 不定义，本就不该进 prompt。 |
| `PlayerOverride` | **按设计维持 null** | `ContextSnapshot.ToPersonaContext()`（L299）刻意置 null；运行时无 override 数据源，DSL 生成器已稳健判空（`PlayerOverride != null` 多处）。待未来有可持久化的 override 存储再做。 |

**落地文件**：`src\NpcDialogueService.cs`（snapshot 补 KingdomName/ClanName）；`src\PersonaDslGenerator.cs`（`BuildIdentityValues` 增 `Summary`，`BuildDynamicSections` 增 relation/state 回退）。**构建验证**：`tools\build.ps1` → `BUILD_OK api=1.3.15`。**提示**：因同时接入 `Summary` 与 `RelationStyle` 回退，已核准角色的 persona_dsl 会相应增段，需在游戏内复核一次对话投影效果。
