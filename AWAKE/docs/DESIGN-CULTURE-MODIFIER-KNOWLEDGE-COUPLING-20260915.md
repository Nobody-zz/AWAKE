# 设计记录 · 文化专属玩法＝数值修正 ＋ 知识库贯穿

- 日期：2026-09-15
- 出具：全局主控线（**定机制与边界，不写实现**）
- 缘起：甲方 Max 09-15 18:5x 原话 ——
  > **「文化专属玩法我想做成对游戏数值的影响，类似游戏内政策的百分比数值影响，并且联动知识库体系贯穿游戏」**
- 前情：功能谱系 §3.1 文化、末节「文化专属玩法能不能挂上游戏内机制」原记作 **待验证** —— 本文把它**验掉了**。

---

## 一、结论先说

**这条路游戏自己就铺好了。** 结论分三句：

1. **"文化 → 百分比数值修正"不是我们发明的东西，原版就有 18 条在跑**（六大文化各 3 条），而且用的正是带理由文本的因子运算。
2. **"像政策那样"这个类比很准，但要注意**：政策的数值效果**写死在 C# 里**，不是 XML 数值表；所以"加一条政策就带一个百分比"**做不到**，得走**模型覆盖**。
3. **真正的注入点是 `CampaignGameStarter.AddModel<T>()`** —— 官方留的正式口子，**不需要 Harmony**；而 AWAKE 的 `SubModule.OnGameStart` **已经拿着 `campaignStarter` 了**，口子是开的。

---

## 二、一手源取证（三条机制，逐条落到文件与行）

### 2.1 文化本来就带数值属性（XML 层）

`Modules/SandBoxCore/ModuleData/spcultures.xml` 的 `<Culture>` 元素上直接有：

| 属性 | 读进哪 | 谁在用 |
|---|---|---|
| `militia_bonus` | `CultureObject.MilitiaBonus`（int） | 民兵相关模型 |
| `prosperity_bonus` | `CultureObject.ProsperityBonus`（int） | 繁荣度相关模型 |
| `naval_factor` | `CultureObject.NavalFactor`（float） | 本版 XML **0 命中**（未使用，留作观察） |

出处：`TaleWorlds.CampaignSystem/CultureObject.cs:218-220`（属性）、`:265-267`（`Deserialize` 读属性）。

⇒ **文化在数据层就是"带数值的"**，这是既有事实，不是我们加的概念。

### 2.2 文化专长（Feat）＝官方的"百分比修正"机制 ★关键

**这是整件事的核心证据。**

| 事实 | 出处 |
|---|---|
| `FeatObject` 带 `EffectBonus`（float）＋ `IncrementType`（`Add` 平加 / **`AddFactor` 因子**）＋ `IsPositive` | `CharacterDevelopment/FeatObject.cs:9-35` |
| `CultureObject` 持有 `MBList<FeatObject> _cultureFeats`，由 XML `<cultural_feats>` 段填充 | `CultureObject.cs:34, 198, 395-407` |
| 原版实有 **18 条**，六大文化各 3 条，`InitializeAll()` 里逐条给数值 | `CharacterDevelopment/DefaultCulturalFeats.cs:88-134` |
| 消费方＝**17 个 CampaignModel**（`GameComponents/Default*Model.cs`） | `HasFeat(` 全仓命中 17 文件 |

原版 18 条的真实数值（`DefaultCulturalFeats.cs:116-133`，节选）：

| 文化 | 专长 | 数值 | 类型 |
|---|---|---|---|
| 巴旦尼亚 | 林地速度惩罚 | **−50%** | `AddFactor` |
| 巴旦尼亚 | 民兵成老兵几率 | +20% | `Add` |
| 巴旦尼亚 | 城镇工程建造 | −10% | `AddFactor` |
| 帝国 | 驻军工资 | −20% | `AddFactor` |
| 帝国 | 军团影响力 | +25% | `AddFactor` |
| 帝国 | 村庄户数增长 | −20% | `AddFactor` |
| 库赛特 | 马牛羊产量 | +25% | `AddFactor` |
| 库赛特 | 城镇税收 | −20% | `AddFactor` |
| 斯特吉亚 | 集结军团影响力 | −50% | `AddFactor` |
| 斯特吉亚 | 村庄粮食产量 | +10% | `AddFactor` |
| 瓦兰迪亚 | 雇佣兵收入 | +15% | `AddFactor` |
| 阿塞莱 | 商队建造 | −30% | `AddFactor` |
| 阿塞莱 | 部队日薪 | +5% | `AddFactor` |

⇒ **甲方要的"百分比数值影响"，原版已经在用同一套写法跑了六年。** 这是最有力的一条：**不是新设计，是既有机制的直接延伸。**

### 2.3 政策 = 同一套数值面，但数值写死

- **数据层**：`bannerlord_policies` **32 条**，列只有 `displayName / descriptionText / proposalText / **effectsText** / rulerSupport / lordsSupport / commonsSupport / **defaultCultureIdsJson**` —— **没有任何数值列**。原文即文字，例如 `policy_crown_duty`：`"5% tax on tariffs is paid to the ruler clan / Higher trade penalty / Town prosperity is decreased by 1 per day"`。
- **政策的效果数值写在模型里**，例如 `GameComponents/DefaultSettlementProsperityModel.cs:180-198`：
  `ActivePolicies.Contains(DefaultPolicies.RoadTolls)` → `explainedNumber.Add(-0.2f, RoadTolls.Name)`。
- **政策↔文化本来就有绑定**（既成事实）：`policy_cantons→battania`、`policy_castle_charters→vlandia`、`policy_council_of_the_commons→aserai`、`policy_sacred_majesty→khuzait`…；反向在 `bannerlord_cultures.defaultPolicyIdsJson`（帝国→senate、斯特吉亚→lawspeakers＋precarial_land_tenure、瓦兰迪亚→feudal_inheritance＋castle_charters、库赛特→grazing_rights＋sacred_majesty）。

⇒ **"像政策那样的百分比"这个类比要修正一句**：政策**呈现**得像百分比表，**实现**上却是硬编码常量。**能照搬的是它的呈现，不是它的写法。**

### 2.4 注入点与运算原语（可行性收口）

| 事实 | 出处 |
|---|---|
| `CampaignGameStarter.AddModel(GameModel)` / `AddModel<T>(MBGameModel<T>)` —— 官方换模型口子 | `CampaignGameStarter.cs:71, 76` |
| **AWAKE 已经在这个口子旁边**：`SubModule.OnGameStart` 已拿 `campaignStarter` 并 `AddBehavior(...)` | `AWAKE/src/SubModule.cs:47-63` |
| AWAKE 目前 **未覆盖任何 CampaignModel**（全仓 `AddModel` 0 命中）⇒ 这是新增能力，不是改造 | `AWAKE/src` grep |

`ExplainedNumber` 的运算原语（`TaleWorlds.CampaignSystem/ExplainedNumber.cs`）：

| 方法 | 行 | 语义 |
|---|---|---|
| `Add(float value, TextObject description = null, TextObject variable = null)` | `:221` | 平加（**带理由文本**） |
| **`AddFactor(float value, TextObject description = null)`** | **`:238`** | **因子/百分比乘算（带理由文本）** ★ |
| `LimitMin(float)` / `LimitMax(float, TextObject)` | `:250, :259` | **夹紧上下限**（防爆） |
| `StatExplainer` / `ExplanationLine` | `:64, :93-97` | 每条修正**逐行留痕**，可进游戏内明细 |

⇒ **两个关键发现**：
- **`AddFactor` 的第二参数就是"理由文本"** —— 它会进 `StatExplainer`，**显示在游戏内的数值明细里**（如繁荣度 tooltip 的逐条来源）。
- ⇒ **这就是"知识库贯穿游戏"的天然接口**（见 §四）。

---

## 三、机制形态（建议口径）

三层，从"最贴官方"到"最可动"：

| 层 | 形态 | 动的是谁 | 适合 |
|---|---|---|---|
| **L1 文化静态属性** | 复用 `militia_bonus` / `prosperity_bonus` | 游戏 XML | 只想要"这个民族天生偏什么" |
| **L2 文化专长（feat 式）** | `FeatObject` + `AddFactor` | AWAKE 自注册 | **主力**：一条文化特性＝一个带理由的百分比 |
| **L3 世界书驱动修正** | AWAKE 自持 `文化 → 修正值` 表，覆盖模型时按 `culture.StringId` 查表 | 世界书 | **甲方的"联动知识库"** |

**两条实现路线（都要，但主次要分清）**：

- **路 A · 借 feat**：往文化 XML 挂 `<cultural_feats><feat id="awake_xxx"/></cultural_feats>` ＋ 代码里注册同名 `FeatObject`。
  ⚠️ 得动 `SandBoxCore/spcultures.xml`（走 XSLT 补丁或自有模块覆盖）⇒ **与别的模组抢加载顺序**，且数值被冻在 XML。
- **路 B · 自持表（建议主力）**：**不碰游戏 XML**。AWAKE 在覆盖的模型里，按 `culture.StringId` 查自己维护的修正表；表由世界书生成。
  ⇒ **世界书改一条，游戏内的数字立刻跟着变** —— 这才是"贯穿"，也是唯一能让知识库真正驱动玩法的形态。

---

## 四、知识库怎么"贯穿"（这一节是甲方那句话的正面回答）

**一条世界书条目 → 两个出口。** 这是"贯穿"的实际含义：

| 出口 | 形态 | 落到哪 |
|---|---|---|
| **出口 1 · 数值** | `AddFactor(bonus, 理由TextObject)` | 游戏内数值明细（tooltip 逐条来源） |
| **出口 2 · 台词** | 同一条目的摘要进 NPC 上下文 | NPC 对话／喊话／周报里**能说出这个数字的来历** |

⇒ 玩家的体验是：**"我这个民族的林地传统让我少受一半减速"这句话，NPC 会讲，结算里也看得到，用的是同一条目。**
⇒ 这才是"贯穿游戏"：**不是把知识库塞进对话框，是让同一条知识同时是规则和说法。**

⚠️ 反面：如果只做出口 1，那就只是一个**数值表**；只做出口 2，那就只是一段**背景设定**。**两个出口共用一条目，才是这件事的全部价值。**

---

## 五、硬约束与风险（做之前必须写进契约的）

1. **必须带理由文本。** `AddFactor(x, reason)` 第二参数不许省 —— 省了玩家看到"繁荣度莫名 −20%"，这是**静默修正**，比不生效更坏。
2. **必须夹紧。** 每条修正套 `LimitMin/LimitMax`，且**总因子有上限**。世界书里一条夸张的数字能把经济打崩。
3. **乘算顺序要定死。** 官方已有 feat ＋ perk ＋ policy ＋ building 都在往同一个 `ExplainedNumber` 上加因子。**叠加顺序不改，但我们的项插在哪一步必须写清**，否则同一套数值在不同存档表现不一致。
4. **存档绑定。** 修正值若来自世界书（活文档），**改条目＝改历史存档的数值**。二选一，必须甲方定：
   - **冻结**：进存档时快照一份，之后世界书再改不影响该存档（可复现，但"活文档"变死）；
   - **活**：每次读档重算（世界书改了就变，但两个存档同一段历史数值不同）。
   ⇒ 我倾向**冻结**（与本项目「记忆绑定存档」的既有方向一致），但**这是甲方的选择，不默认**。
5. **数值要可审计。** 每个修正必须能回答"这条从哪来" —— 至少 `Awake.log` 里可 grep 到"条目 id → 文化 → 修正值"。
6. **不许与游戏既有 feat 撞 id。** 自注册的 `FeatObject` 一律 `awake_` 前缀。
7. **不许做成"文化优越表"。** 六大文化各有得有失（原版就是这样：帝国驻军便宜但村庄户数慢）。**只给加成不给代价＝平衡崩**，且与项目「不造优越种族」的取向冲突。
8. **这是"新机制"，按甲方 09-15 口径排在第三阶段最靠后**（见 `docs/DECISION-20260915-PHASE3-BOUNDARY.md`）——**先验证，别先铺量**。

---

## 六、这条改了功能谱系的哪个状态

| 项 | 原状态 | 现状态 |
|---|---|---|
| 谱系 §3.1「文化专属玩法（森林战、骑射传统）」 | **待验证**（我没查） | **机制已验证，可做**——见本文 §二；接口、原语、注入点均有出处 |
| 谱系末节「仍待验证」 | 文化专属玩法一项 | **清空**（本项转入 §3.1 正文，机制层面不再挂"待验证"） |

---

## 七、我**没有**验的（别当成已定）

- AWAKE 自注册的 `FeatObject` 是否随存档正确往返（`DefaultCulturalFeats` 是开局注册的，模组注册时机与读档顺序未验）。
- 用自有模块覆盖 `SandBoxCore/spcultures.xml` 时，**与其它模组的加载顺序冲突**实际长什么样（路 A 才涉及）。
- `naval_factor` 在本版为何 0 命中（属性存在、XML 未用）。
- 具体挂哪几个模型、每条文化给多少数值 —— **这是设计活，不是本文范围**（且属第三阶段，排在后面）。

---

## 八、一手源清单（可复查）

**反编译源码**（根：`…/BannerlordSage-main/dist/games/bannerlord/assets/Source/bin/Win64_Shipping_Client/`）：
- `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CultureObject.cs`（`:218-220, :265-267, :395-407`）
- `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterDevelopment/FeatObject.cs`（`:9-35`）
- `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterDevelopment/DefaultCulturalFeats.cs`（`:88-134`）
- `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/PolicyObject.cs`（全文：无数值字段）
- `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/ExplainedNumber.cs`（`:221, :238, :250, :259`）
- `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultSettlementProsperityModel.cs`（`:51-54, :65-68, :180-198`）
- `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignGameStarter.cs`（`:71, :76`）

**官方 XML**：`Modules/SandBoxCore/ModuleData/spcultures.xml`（`<Culture>` 数值属性 ＋ `<cultural_feats>` 段）。

**官方索引库**：`bannerlord_policies`（32 行，**无数值列**）、`bannerlord_cultures`（`defaultPolicyIdsJson`）。

**AWAKE 侧**：`AWAKE/src/SubModule.cs:47-63`（`OnGameStart` 已持有 `campaignStarter`）。

**探针**（入库留档，可重跑）：`docs/_probe_culture_policy_20260915.py`（政策/文化表结构与取值）、`docs/_probe_culture_xml_20260915.py`（spcultures.xml 数值属性与 `<cultural_feats>` 写法）。

---

## 九、留痕落点

| # | 落点 | 形态 |
|---|---|---|
| 1 | `docs/FUNCTION-GENEALOGY-3PHASE-20260915.md` §3.1 与末节 | 文化专属玩法由「待验证」改为「机制已验证」，指向本文件 |
| 2 | 本文件 | 甲方方向 ＋ 主控线可行性取证的合订记录 |
