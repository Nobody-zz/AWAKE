# 角色卡「B 类」消费端契约核对报告

> 日期：2026-09-12
> 范围：`tools/persona-workbench/characters/` 全部 46 张角色卡 × 三层性格字段，对照三层消费契约
> 性质：只核对、不改文件（本报告为落地前的决策依据）

## 一、一句话结论

**角色卡里的「三套性格字段」不但互相重复，而且已和「编译器内置源 registry」「运行时 tag 注册表」漂移成三套互不相通的词汇；46 张卡当前在编译链路下会整批校验不通过，运行时真正认的只有 8 个标签。**

## 二、契约链（谁是真正的消费端）

| 层 | 文件 | schema | 是否被游戏运行 |
|---|---|---|---|
| 卡片源 | `characters/*.persona.json` | `persona-workbench.character.v1` | 否（工作台输入） |
| 编译中间层 | `PersonaAuthoringV2.cs` + `crosswalk.v1.json` | `awake.persona.authoring.v2` | 否（迁移产物） |
| **运行时** | `ModuleData/Worldbook/persona_definitions/tag_registry.json` + `definitions/*.json` | `awake.persona.tags.v1` / `awake.persona.definition.v1` | **是** |

运行时通过 `src/PersonaDataLoader.cs` 读取 `tag_registry.json`（**8 个标签 + 1 个 bundle**）与 `definitions/` 下的 `definition.v1`。`definitions/hero_default.json` 的实机样本就是**一段叙述文字 + `"bundles": ["bundle.noble_pragmatist"]`**——没有轴、没有强度、没有 26/17/19 那些字段。

## 三、三层词汇漂移（evidence A）

`tag_registry.json` 的 8 个标签是唯一权威：`trait.pragmatic`、`trait.status_conscious`、`expression.measured`、`expression.indirect`、`behavior.conditional_cooperation`、`trigger.threat_or_leverage`、`boundary.no_instant_submission`、`boundary.no_modern_psychology`。

编译器内置 `PersonaTagRegistry.CreateDefault()`（`PersonaCore.cs:134`）另有一套 19 键：`trait.cautious`、`trait.ambitious`、`trait.proud`、`trait.pragmatic`、`trait.guardian`、`trait.traditional`、`expression.measured`、`expression.direct`、`expression.formal`、`expression.teasing`、`expression.warm`、`behavior.bargains`、`behavior.observes_before_acting`、`behavior.tests_loyalty`、`behavior.keeps_leverage`、`behavior.protects_inner_circle`、`behavior.takes_command`、`trigger.public_humiliation`、`boundary.no_empty_promises`。

**46 张卡实际用到的 `tags` 共有 69 个不同键**，分四类：

| 类别 | 数量 | 说明 |
|---|---|---|
| 运行时 + 编译器共有 | 2 | `trait.pragmatic`、`expression.measured` |
| 仅编译器（不在运行时） | 14 | `behavior.bargains`、`expression.direct`、`trait.ambitious`… 等 |
| 仅运行时（不在编译器） | 6 | `expression.indirect`、`behavior.conditional_cooperation`、`trait.status_conscious`、`trigger.threat_or_leverage`、`boundary.no_instant_submission`、`boundary.no_modern_psychology` |
| **双方都没有（自造键）** | **47** | 见下方清单 |

### 自造键清单（47 个，既不在编译器也不在运行时）

- behavior：`acts_before_reasoning`、`administers_fairly`、`charges_first`、`guards_home`、`misrepresents`、`retaliates`、`seeks_power`、`tests_commitment`（8）
- boundary：`no_direct_confrontation`、`no_grand_ambition`、`no_intrigue`、`no_servility`（4）
- expression：`blunt`、`evasive`、`fierce`、`flamboyant`、`frank`、`high_energy`、`informal`、`ingratiating`、`reserved`、`stoic`、`understated`、`warm_to_allies`（12）
- trait：`caution`、`caution_for_family`、`courage`、`deceitful`、`direct`、`hedonistic`、`kind`、`loyal`、`pride`、`private`、`tradition`、`wild`、`will`（13）
- trigger：`being_put_on_pedestal`、`clan_safety`、`duty_or_responsibility`、`family_safety`、`fear_of_decline`、`loyalty_or_betrayal`、`reputation_challenge`、`social_slight`、`threat_to_home`、`threat_to_house_honor`（10）

**三处「近名漂移」**特别值得警惕——作者心里有对应概念，但键名拼写对不上权威：
- `trait.caution`（卡片用）↔ 编译器的 `trait.cautious`
- `trait.pride`（卡片用）↔ 编译器的 `trait.proud`
- `trait.tradition`（卡片用）↔ 编译器的 `trait.traditional`

## 四、facetStrengths 键漂移（evidence B）

crosswalk 的 `facetStrengthIds` 只认 17 个键（`behavior.bargains`、`expression.measured`、`trait.pragmatic`… 等迁移名）。卡片却把 `expression.indirect`、`behavior.conditional_cooperation`、`trait.kind` 等当作 facet 键。**46 张卡里 facet 未注册键实例合计 93 处。**

## 五、取值范围三处打架（evidence C）

| 字段 | schema 允许 | 编译器/跨库要求 | 卡片实测 |
|---|---|---|---|
| axis（profile × 26 轴） | −3..3 | −2..2 | **22/26 轴出现 ±3 越界；越界值实例合计 126 处** |
| facetStrengths | 0..5 | 1..4 | 几乎全在 1..4，仅 1 张卡的 `trait.ambitious=5` 越界 |

26 个轴全部 46 张卡都填了；其中只有 4 个轴（`playfulness`、`resentment`、`supportSeeking`、`valueTradeability`）全程不越 −2..2。

## 六、三套系统冗余（evidence D）

编译端（`PersonaAuthoringV2.cs` 的 `MergeObservation`，优先级 axis=3 > facet=2 > tag=1）把七套字段收敛后，真正 emit 进观察的只有 4 个 selector，且前三个各自被三套字段同时表达：

| 运行时 selector | 来自 axis | 来自 facetStrengths | 来自 tags |
|---|---|---|---|
| `trait.pragmatic` | `traitProfile.pragmatism` | `trait.pragmatic` | `trait.pragmatic` |
| `expression.measured` | `expressionProfile.restraint` | `expression.measured` | `expression.measured` |
| `behavior.conditional_cooperation` | `behaviorProfile.conditionality` | `behavior.bargains` | `behavior.bargains` |
| `expression.indirect` | `expressionProfile.directness`（负值） | — | — |

其余约 40 个字段（全部 reaction/commitment、5/6 trait、3/5 expression、5/6 behavior、14/17 facet、16/19 tag）走 `preserve_only` → 进 `migration.preservedLegacyData`，**运行时读不到**。

## 七、逐卡问题统计

栏义：`tag未过编译器`=该卡 tags 里不在编译器 19 键的数量；`tag未过运行时`=不在运行时 8 键的数量；`自造tag`=两层都没有的数量；`facet未注册`=不在 crosswalk 17 键的数量；`facet越界`=1..4 之外的数量；`轴越界`=−2..2 之外的轴数量。

| 卡片 | tag未过编译器 | tag未过运行时 | 自造tag | facet未注册 | facet越界 | 轴越界 |
|---|---|---|---|---|---|---|
| caladog_battania | 5 | 1 | 0 | 0 | 0 | 0 |
| derthert_vlandia | 6 | 0 | 0 | 0 | 0 | 0 |
| garios_empire_west | 4 | 2 | 0 | 0 | 0 | 0 |
| rhagaea_empire_south | 6 | 0 | 0 | 1 | 0 | 0 |
| lukos_empire_north | 6 | 0 | 0 | 1 | 0 | 0 |
| mesui_khuzait | 4 | 2 | 0 | 0 | 0 | 0 |
| unqin_aserai | 6 | 0 | 0 | 2 | 0 | 0 |
| raganvad_sturgia | 3 | 3 | 0 | 0 | 0 | 0 |
| olek_sturgia | 4 | 3 | 0 | 0 | 0 | 0 |
| adram_aserai | 4 | 3 | 0 | 1 | 0 | 1 |
| aldric_vlandia | 4 | 3 | 0 | 1 | 0 | 1 |
| godun_sturgia | 4 | 3 | 0 | 1 | 0 | 1 |
| ingalther_vlandia | 3 | 6 | 0 | 0 | 0 | 1 |
| pharon_empire_south | 3 | 4 | 0 | 0 | 0 | 1 |
| tovir_sturgia | 3 | 2 | 0 | 0 | 0 | 1 |
| turiados_empire_south | 5 | 2 | 0 | 1 | 0 | 1 |
| jinda_aserai | 4 | 2 | 0 | 1 | 0 | 1 |
| ergeon_battania | 5 | 3 | 0 | 2 | 0 | 3 |
| berican_vlandia | 5 | 4 | 1 | 2 | 0 | 3 |
| yorig_sturgia | 4 | 3 | 1 | 2 | 0 | 3 |
| crotor_empire_west | 7 | 4 | 3 | 4 | 0 | 4 |
| fafen_sturgia | 5 | 5 | 2 | 3 | 0 | 5 |
| vashorki_sturgia | 2 | 3 | 0 | 1 | 0 | 4 |
| vyldur_sturgia | 3 | 3 | 0 | 0 | 0 | 5 |
| vendelia_empire_west | 4 | 4 | 2 | 2 | 0 | 4 |
| zerosica_empire_north | 5 | 3 | 1 | 3 | 0 | 3 |
| asta_sturgia | 5 | 5 | 2 | 3 | 0 | 4 |
| hashan_aserai | 4 | 5 | 2 | 2 | 0 | 3 |
| monchug_khuzait | 5 | 4 | 0 | 1 | 1 | 2 |
| tulag_khuzait | 4 | 4 | 2 | 2 | 0 | 4 |
| ira_empire_south | 7 | 6 | 5 | 4 | 0 | 2 |
| mina_empire_south | 5 | 4 | 3 | 3 | 0 | 5 |
| patyr_empire_south | 6 | 4 | 3 | 4 | 0 | 4 |
| ulbos_empire_south | 3 | 4 | 1 | 1 | 0 | 4 |
| thephilos_empire_west | 6 | 6 | 4 | 3 | 0 | 0 |
| penton_neretzes_empire_north | 5 | 3 | 0 | 2 | 0 | 1 |
| tais_aserai | 3 | 4 | 0 | 0 | 0 | 4 |
| apys_varros_empire_west | 6 | 5 | 2 | 3 | 0 | 1 |
| calatild_vlandia | 4 | 5 | 0 | 1 | 0 | 4 |
| manteos_empire_north | 4 | 4 | 1 | 2 | 0 | 4 |
| aradwyr_battania | 6 | 7 | 5 | 5 | 0 | 9 |
| aeron_battania | 8 | 9 | 8 | 7 | 0 | 6 |
| luichan_battania | 7 | 10 | 7 | 4 | 0 | 2 |
| maireas_battania | 8 | 11 | 8 | 5 | 0 | 6 |
| melidir_battania | 10 | 9 | 8 | 7 | 0 | 8 |
| pryndor_battania | 8 | 10 | 8 | 6 | 0 | 4 |

合计（46 张卡）：`tag未过运行时`实例 **187** 处、`facet未注册`实例 **93** 处、`轴越界`实例 **126** 处。仅 4 张卡（derthert、lukos、rhagaea、unqin）的 tags 全部落在运行时 8 键内。

## 八、根因推断

1. **早批次卡（中央统治者）**严格对齐运行时 8 标签 + 轴 −2..2，是「干净」基线：caladog、derthert、garios、rhagaea、mesui、unqin 等轴越界=0、自造 tag=0。
2. **后批次卡（尤其巴旦尼亚 6 个族长、帝国旁支）**漂移到自由发挥：自造 tag 5–8 个、轴越界 6–9 处。
3. 编译链路（19 键迁移词汇 + 跨库 17 键 facet）是一套**从未与运行时 8 标签收口**的中间层，导致「卡片—编译器—运行时」三方各行其是。

## 九、建议处理方向（供决策）

- **方向 A｜彻底收敛到运行时 8 标签（推荐）**：删 5 个 profile + `facetStrengths` + 冗余标签，只留 `tags`（8 选）+ 叙述文字；改 schema、编译器内置 registry、crosswalk、46 张卡。最干净、与游戏真正对齐，改动面最大。
- **方向 B｜只修漂移不动结构**：保留三套字段，把键名统一到运行时 8 标签内核、范围统一到轴 −2..2 / facet 1..4，让 46 张卡过校验；「同一件事写三遍」仍在。
- **方向 C｜先不落地**：以本报告为基线，逐层决定后再动。

在方向未定前，建议暂停新增剩余贵族卡，避免继续在漂移词汇上叠加。