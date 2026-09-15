# 角色卡「B 类」修复方案试点报告

> 日期：2026-09-12
> 目的：在改动 46 张卡之前，先拿 3 张代表性卡片验证「收敛到运行时 8 标签」这条修复路线是否成立
> 试点卡：德泰尔（干净基线）、阿丝塔（中等漂移）、埃隆（最严重漂移，巴旦尼亚族长）

## 一、试验前提

上一份核对报告里，我给出的「方向 A」是：把三套性格字段收敛到运行时 `tag_registry.json` 的 8 个标签。本报告验证这条路线是否真的能保住角色的个性。

## 二、关键发现：运行时只有一种人格原型

`tag_registry.json` 的 8 个标签，语义如下：

| 标签 | 含义 |
|---|---|
| trait.pragmatic | 务实，优先可执行利益 |
| trait.status_conscious | 重视身份、礼法、地位 |
| expression.measured | 克制，不失控 |
| expression.indirect | 含蓄、暗示、迂回 |
| behavior.conditional_cooperation | 有条件合作 |
| trigger.threat_or_leverage | 对威胁/把柄/筹码敏感 |
| boundary.no_instant_submission | 不即时臣服 |
| boundary.no_modern_psychology | 不用现代心理话术 |

这 8 个标签 = 同一个 bundle「贵族务实底座」（`bundle.noble_pragmatist`）。**它只描述一种人：一个克制、含蓄、重身份、务实算计的老练贵族。** 它没有「温和／冲动／勇猛／豪爽／报复／护内／传统」这些维度。

## 三、三张卡的映射结论

### 1. 德泰尔（干净基线）——完全命中
他的本质就是「克制的务实仲裁者」：温和、克制、含蓄、重身份、讲条件。8 标签 100% 覆盖，`facetStrengths` 里唯一的额外键 `trait.traditional` 也只是「传统=2」的轻标记。

**结论：干净卡恰好就是 8 标签围着的那个原型，这不是巧合，是 8 标签按「贵族统治者」这个原型设计的。**

### 2. 阿丝塔（中等漂移）——丢掉本质
她的本质是「传统、温和、护内、谨慎、织补氏族的部族主母」。映射到 8 标签后：

| 她的核心 | 8 标签能表达吗 |
|---|---|
| 传统（tradition=3、traditional=4） | ✗ 无对应标签 |
| 温和（warmth=3、trait.kind） | ✗ 无对应标签 |
| 护内（inGroupLoyalty=3、保护子弟） | ✗ 无对应标签 |
| 含蓄（expression.indirect） | ✓ 命中 |
| 克制（restraint=2） | ✓ 命中 |
| 重身份/礼法/谱系 | ✓ 勉强命中 status_conscious |
| 谨慎（caution） | ✗ 无对应标签 |

**结论：阿丝塔会被裁成一个只剩「克制、含蓄」的空壳，「传统、温和、护内」这三个她最硬的特质全部丢失。**

### 3. 埃隆（最严重漂移）——人格被磨空
他的本质是「冲动、勇猛、直率、高能量、身先士卒、护内、报复、恩怨分明的疯虎」。8 标签逐一对照：

| 他的核心 | 8 标签能表达吗 |
|---|---|
| 冲动（caution=−2、deliberation=−2） | ✗ 完全相反 |
| 直率（directness=3、expression.blunt） | ✗ 与「含蓄」相反 |
| 高能量/豪爽（high_energy、warm_to_allies） | ✗ 无对应 |
| 身先士卒（charges_first、leadership=2） | ✗ 无对应 |
| 报复（retaliates、resentment=2） | ✗ 无对应 |
| 恩怨分明（loyalty_or_betrayal、护内） | ✗ 无对应 |
| 蔑视体面（formality=−2） | ✗ 与「重视身份」相反 |
| 不即时臣服 / 不用现代话术 | ✓ 仅这 2 个边界命中 |

**结论：8 标签只能给埃隆留下 2 个「边界」，他作为「疯虎战神」的全部人格——勇猛、冲动、直率、高能、报复、护内——在 8 标签模型里一个都放不下。**

## 四、试点结论：方向 A 被证伪

**「收敛到运行时 8 标签」不可行。** 它会把约 80% 的角色磨平成同一种「老练务实贵族」的空壳，只有第一代「中央统治者」卡（德泰尔、卢科斯、拉盖娅…）因为本来就是照着这个原型写的，才能安全收敛。

逆向看，这也解释了漂移的根因：后批次作者（尤其巴旦尼亚族长、帝国旁支）遇到 8 标签表达不了的角色，只能逃进自由发挥的自造键（`trait.will`、`expression.high_energy`、`behavior.charges_first` 等 47 个），这些键写得对、但没注册，于是和编译器、运行时三方失联。

## 五、修正方向（替代方向 A）

问题不在「字段太多」，而在「**三套重复 + 词汇没注册 + 范围不一致**」。修正应该是：

1. **三套并成一套**：`traitProfile`(26 轴) + `facetStrengths`(17) + `tags`(19+47 自造) → 合并成**一套结构化的「人格码」**，按 trait / expression / behavior / trigger / boundary 五类，每条目 = 「键 + 一个强度值」。
2. **注册成一份完整分类法**：把 47 自造键 + 19 编译器键 + 8 运行时键，去重、统一拼写（`trait.caution`↔`trait.cautious`、`trait.pride`↔`trait.proud`、`trait.tradition`↔`trait.traditional` 这类近名错位全部归一），产出一份权威 catalog，并在 `tag_registry.json` 注册。
3. **取值范围统一到一把尺**（如 −2..2 一轴到底，或 1..4 一档到底，二选一）。
4. **8 运行时标签降级为「运行时底座」**：能从人格码映射的映射（务实/克制/含蓄/有条件合作…），映射不了的（温和/勇猛/报复/护内…）由叙述文字兜底——叙述本就被运行时逐字消费。
5. **叙述文字原样保留**——它是角色的真正载体，运行时全量读取，不需要动。

## 六、样例：埃隆的单系统重构（目标形态）

下面是埃隆从「7 字段 × 约 30 个散值」合并成「**一套人格码**」后的样子（键为待注册 catalog，强度统一 −2..2）：

```json
"personality": {
  "trait": { "impulsive": 2, "ambitious": 2, "proud": 2, "in_group_loyalty": 3, "daring": 2, "willful": 2 },
  "expression": { "blunt": 3, "high_energy": 2, "warm_to_allies": 2, "playful": 2 },
  "behavior": { "charges_first": 3, "protects_inner_circle": 2, "retaliates": 2, "commands": 2 },
  "trigger": { "loyalty_or_betrayal": 2, "reputation_challenge": 2 },
  "boundary": { "no_instant_submission": true, "no_modern_psychology": true }
}
```

同一份信息，从 7 套字段、约 30 个散值、三套词汇，压成一套 5 类 18 条的注册人格码；叙述（core/identityFacts/…）一字未动。

## 七、下一步

- 若要落地修正方向，需先定：① 人格码用统一 −2..2 还是 1..4；② catalog 的权威清单先由我把 47 自造键归一后产出草案给你审。
- 在方向未定前，仍建议暂停新增剩余贵族卡。

（本报告只做观察与建议，未改动任何卡片、schema 或编译器文件。）