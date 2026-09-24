# 暗面批「补传言」· 机制落点（2026-09-24）

> **背景**：甲方两问 —— ①「身份对应的代码搞清楚了吗？怎么做搞清楚了吗？」②「你别告诉我没用过的你就不会用。」
> 本章是把「怎么做」当场查死的结果。**凡是查过的，给读数；凡是查不到的，标缺口。不许再拿"没先例"当挡箭牌。**
>
> **结论一句话**：**身份只有 12 个（按社会地位分），游戏侧的 `KingdomIds` 挂不上身份，只能当条件；
> 而 `kingdom_ids` 写 clan（火焰余烬/秘密之手）是死条件。**
>
> **★ 2026-09-24 追记**：本文原写「系统里没有 `clan_ids`」——**该论断已过时。**
> 同日已补实现全链（schema→模型→编译器→加载器→判定器→查询侧），
> 一条说法现在可以写 `clan_ids: [entity.clan.<code>]` 只送给某个家族的人。
> 见本文 §六「追记」与 `docs/DECISION-20260924-CLAN-BINDING.md`。
> **注意：这一步越过了 `docs/superpowers/specs/2026-08-24-worldbook-entity-catalog-design.md:31`
> 那句「B1 不实现 `hero_ids`/`clan_ids` 运行时权限条件」——是否接受由甲方定。**

## 一、身份清单：**只有 12 个，没有"教徒"**

出处：`docs/worldbook-studio-plan/profile-registry.v1.json`（一手，当场读）

| id | 中文 | 继承 |
|---|---|---|
| `profile.commoner` | 普通平民 | —（根）|
| `profile.villager` | 普通村民 | commoner |
| `profile.townsfolk` | 城镇居民 | commoner |
| `profile.soldier` | 士兵 | commoner |
| `profile.notable` | 地方重要人物 | commoner |
| `profile.headman` | 头人 | notable |
| `profile.noble` | 贵族 | notable |
| `profile.noble_high_steward` | 高管理贵族 | noble |
| `profile.merchant` | 商人 | townsfolk |
| `profile.tavernkeeper` | 酒馆老板 | townsfolk |
| `profile.ransom_broker` | 赎金经纪人 | merchant |
| `profile.anonymous` | 身份未知 | — |

**★ 三层要点**

1. **全是"社会地位"，没有"信仰/组织"维度。** 没有「教徒」「秘密之手」「火焰余烬」。
2. **`inherits` 是继承链** ⇒ 记过的坑「文化限定 rumor 只能挂 villager，挂 notable/commoner 等于没收窄」，
   根因就在这里：**挂父节点会连带命中整棵子树**。
3. **给低能力身份挂高级别 grant ＝ 死表达**（能力上限见 §三）。

## 二、条件字段：原 7 个，**2026-09-24 起补上 `clan_ids`**

> **★ 本节是当日快照（写于 clan 落地之前）。** 现行为 **8 个**，多了 `clan_ids`，
> 见 §六「追记」。下表保留原样，便于对照"改之前长什么样"。

出处：`AuthoringEditorModel.cs:485` ＋ `RuntimePackageCompiler.cs:232-243`（一手）

```
culture_ids / kingdom_ids / settlement_ids / role_ids
is_female / is_clan_leader
min_age / max_age / min_management / min_steward / min_skill
```

**⚠️ 三处必须记住的差异**

| 授权侧（AWAKE）| 游戏侧 `When` | 说明 |
|---|---|---|
| 有 `culture_ids` | 有 `Cultures` | 对应 |
| 有 `kingdom_ids` | 有 `KingdomIds` | 对应 |
| 有 `role_ids` | 有 `Roles` | 对应 |
| 有 `settlement_ids` | 有 `SettlementIds` | 对应 |
| **无** | 有 `HeroIds` | **缺** |
| **无** | 有 `IdentityIds` | **缺** |
| ~~**无 `clan_ids`**~~ | — | ~~★ 关键缺口~~ **★ 已于 09-24 补上（§六）** |

**值域**（`awake.worldbook.authoring.v1.schema.json`）：
- 档里写**裸名**（`empire` / `embers_of_flame`），
- 编译时 `CanonicalConditionId` **自动补前缀**成 `awake:culture:empire` 等，
- **编译不查存在性**（`MapConditionId` 只加前缀）⇒ **写错值不报警，运行时才算**。

## 三、身份能力上限（决定"这句话送不送得到"）

| 身份 | scope | detail |
|---|---|---|
| `villager` | local | rumor |
| `commoner` | local | rumor |
| `townsfolk` | regional | summary |
| `notable` | regional | detail |
| `merchant` / `tavernkeeper` / `ransom_broker` | faction | detail |
| `headman` / `soldier` | national | detail |
| `noble` / `noble_high_steward` | elite | detail |

⇒ **给 `villager` 挂 `detail` ＝ 那条表达永远送不到人耳。**

## 四、★ 火焰余烬 / 秘密之手 = **clan（家族），不是 kingdom**

出处：`bannerlord.db` → `bannerlord_clans`（一手，当场查）

| clanId | 官方中文 | culture | isMinorFaction | isOutlaw | **isMafia** | isMercenary | tier |
|---|---|---|---|---|---|---|---|
| `embers_of_flame` | **火焰余烬** | empire | 1 | 1 | **0** | 0 | 3 |
| `hidden_hand` | **秘密之手** | empire | 1 | 1 | **1** | 0 | 3 |
| `lakepike` | **湖鼠帮** | sturgia | 1 | 1 | 1 | 0 | 4 |

**★ `isMinorFaction = 1` 就是「小派系」这个词的出处 —— 它在游戏数据里是一个字段。**

**★ 官方的 `Embers of the Flame` 背景原文**（`spclans.xml`）：
> "The Embers of the Flame are the descendants of a rebel movement that rose up nearly a century ago,
> after the saintly but ineffective teenaged Emperor Darusos was toppled by one of his generals.
> They claim that they are preparing the way for Heaven to bring back Darusos and usher in a golden age,
> but like so many other rebel movements in Calradia they have been forced to turn to extortion to survive."

⇒ **"变成敲诈来活命"是官方原文**，我先前读到的"收保护费"不是错觉，**但它只是这条的后半句**。

**★ `isMafia` 这一个字段，就是"教派"与"黑帮"的分界**：
`hidden_hand` = `isMafia:1`（黑帮），`embers_of_flame` = `isMafia:0`（**不是黑帮**）。
⇒ **游戏数据自己把两类分开了** —— 甲方最初的质疑（"人家是教徒，怎么搞得像黑社会"）**被数据坐实**。

### 全部 15 个小派系（`isMinorFaction=1`，当场读数）

| 类 | clanId | culture | 官方中文 |
|---|---|---|---|
| 雇佣兵 | `ghilman` | darshi | **古拉姆** |
| 雇佣兵 | `legion_of_the_betrayed` | empire | **被弃者军团** |
| 雇佣兵 | `skolderbrotva` | nord | **护盾兄弟会** |
| 雇佣兵 | `company_of_the_boar` | vlandia | **黄金野猪兵团** |
| 法外·黑帮 | `beni_zilal` | aserai | **暗影之子** |
| 法外·黑帮 | `wolfskins` | battania | **狼皮部落** |
| 法外·黑帮 | `brotherhood_of_woods` | vlandia | **绿林兄弟会** |
| 法外·黑帮 | `hidden_hand` | empire | **秘密之手** |
| 法外·黑帮 | `lakepike` | sturgia | **湖鼠帮** |
| 法外·非黑帮 | `embers_of_flame` | empire | **火焰余烬** |
| 法外·非黑帮 | `jawwal` | aserai | **贾沃勒** |
| 法外·非黑帮 | `karakhuzaits` | khuzait | **喀拉库吉特** |
| 法外·非黑帮 | `forest_people` | vakken | **森民** |
| 法外·非黑帮 | `eleftheroi` | empire | **自由民** |

（`wolfskins`→狼皮部落 与 `forest_people`→森民 两条取自 `SandBox` 原版模块本地化；
其余同源 `bannerlord_clans` → `localization_entries` 两步查。）

## 五、★ 结论：怎么落（把"怎么做"定死）

### ① `kingdom_ids` 写 `embers_of_flame` = **死条件，禁止**

理由：它是 `bannerlord_clans` 的 clanId，**不是 kingdom**。写进去编译不报错（只加前缀），
**但运行时匹配不上任何 NPC**。⇒ **绝对不写。**

### ② 系统里**没有** `clan_ids` ⇒ **"说给火焰余烬的人听"表达不了**

⇒ 变体 2（他们自己的自述）**不能按"只有他们自己听得到"来挂**。这是**机制缺口，记在案**。

### ③ 那实际怎么挂（**能做的**）

变体 2 的可表达近似：**`culture_ids: [entity.culture.empire]` ＋ 低能力身份层（villager/commoner/townsfolk）**。
即：**说给"帝国地界上的街面人"听**，靠**内容本身**（第一人称"我们"）让读者知道是他们在说话，
而**不是靠条件筛人**。

⇒ **这正好与《巷子》样板同构**：**口吻承担"谁在说"，条件只管"谁能听到"。**

> ⚠️ **2026-09-24 补正（重要）**：上面这段原先只是**纸上的方案**，`small-factions.yaml` 里
> **根本没有 `culture_ids`**（rumor 层三个 grant 只有 `profile_id`/`scope`/`min_detail`）。
> 跑真探针时暴露：**帝国与瓦兰迪亚的平民收到的是同一条** ⇒ 当时 culture 条件是"空转"。
>
> **至于 culture 条件到底管不管事**，已用**阳性对照档** `military-empire-north`
> （它真挂了 `culture_ids:[entity.culture.empire]` ＋ `kingdom_ids:[entity.kingdom.empire]`）跑真探针验死：

| 探针 | 条件 | 目标条目命中 | 说明 |
|---|---|---|---|
| P1 | 帝国 + townsfolk | **True** | 阳性成立 |
| N1 | 瓦兰迪亚 + townsfolk | **False** | ★ **culture 闸真在筛** |
| N2 | 帝国 + commoner（能力不足） | **False** | ★ **能力闸真在筛** |
| P2 | 帝国 + soldier | **True** | 阳性成立 |

⇒ **两道闸（culture / 能力）都确认管事**；`culture_ids` 是**可用的真条件**，不是死条件。
⇒ 所以 §五-③ 的方向是对的，**但必须真的把 `culture_ids` 写进 grant** —— 光写在文档里不算。

### ④ 七变体的分层方案（**按 `When` ＋ 能力上限**）

| 变体 | `When` | 可表达为 | 层 | 挂谁 |
|---|---|---|---|---|
| 0 | `Cultures: ["empire"]` | `culture_ids: [empire]` | rumor | villager / commoner（+ townsfolk 若不同时有 summary）|
| 1 | `+ Roles: ["lord"]` | `+ role_ids: [lord]` | detail | notable（**且 role=lord**）|
| 2 | `+ KingdomIds: [embers_of_flame]` | **不可表达** | — | **见 ③：用 culture + 低层近似** |
| 3 | `Roles: ["lord"]` | `role_ids: [lord]` | detail | notable |
| 4 | 空 | 无条件 | rumor | 任何低能力身份（★最宽，注意别顶掉别人）|
| 5 | `+ KingdomIds: [hidden_hand]` | **不可表达** | — | **同 ③，或改挂 role 近似** |

**⚠️ 分层铁律（既有坑，逐条守）**：
- **同档只送一条**（`SelectExpression`，同分取先）⇒ **层与层受众必须互斥**。
- **`rumor` 层只挂低能力身份**；**`detail` 层给高能力身份**。**禁止把 rumor 挂到能拿 detail 的身份上。**
- **`townsfolk` 在同时有 summary 层时才摘出 rumor 层**（否则被吃）。

## 六、待查 → **已验（2026-09-24 全部销账）**

> 四件都跑完了。下面每条给读数与出处，**不留"待查"**。

### ✅ 1. 15 个 clanId 的官方中文名 —— **已全部查明**（见 §四 表）

两步查法（`bannerlord_clans.clanId` → 本地化 → `CNs`）：
`ghilman`→古拉姆｜`legion_of_the_betrayed`→被弃者军团｜`skolderbrotva`→护盾兄弟会｜
`company_of_the_boar`→黄金野猪兵团｜`beni_zilal`→暗影之子｜`wolfskins`→狼皮部落｜
`brotherhood_of_woods`→绿林兄弟会｜`jawwal`→贾沃勒｜`karakhuzaits`→喀拉库吉特｜
`forest_people`→森民｜`eleftheroi`→自由民（＋ 火焰余烬／秘密之手／湖鼠帮 已在上轮查明）。

### ✅ 2. `role_ids` 的合法值域 —— **不是登记表，是一张写死在代码里的映射**

出处：`src/WorldbookIdentityCapabilityRules.cs:17-79`（**决定 scope/detail 的那一步**）
＋ `src/WorldbookIdentityEvaluator.cs:147-192`（`IdentitiesForRole`）
＋ `src/BannerlordWorldbookIdentityAdapter.cs:128-159`（角色名从哪来）。

**实测**（把 role 名灌给真件 `Resolve`，看 scope 是否为空＝落 anonymous）：
- **认得出（26 个）**：`villager` `farmer` `commoner` `headman` `village_headman` `town_headman`
  `merchant` `tavernkeeper` `ransom_broker` `soldier` `noble` `lord` `rural_notable` `wanderer`
  `gang_leader` `notable` `arena_master` `gangster` `bandit` `preacher` `musician` `goods_trader`
  `weaponsmith` `guard` `caravan_guard` `mercenary`
- **认不出（落 anonymous）**：`tavern_keeper` `trooper` `clan_leader` `embers_of_flame`
  `hidden_hand` `cultist` …

**★ 游戏真实产出的 occupation 只有 30 种，全是驼峰**（`bannerlord_troops.occupation` 当场读数）：
`Lord`/`Soldier`/`Townsfolk`/`Wanderer`/`Villager`/`Merchant`/`Bandit`/`Mercenary`/`CaravanGuard`/
`GangLeader`/`Headman`/`Artisan`/`Preacher`/`RuralNotable`/`GoodsTrader`/`Gangster`/`ShopWorker`/
`Weaponsmith`/`Tavernkeeper`/`TavernWench`/`TavernGameHost`/`RansomBroker`/`PrisonGuard`/`Musician`/
`HorseTrader`/`Blacksmith`/`Armorer`/`ArenaMaster`/`Special`/`NotAssigned`。

⇒ **`NormalizeRole` 拆驼峰后小写，不删下划线** ⇒ `Tavernkeeper`→`tavernkeeper` **命中** ✓；
**游戏里根本没有 `Trooper` / `ClanLeader` 两个写法**（30 种里没有）
⇒ 代码里那几个下划线 case 是**死代码，但走不到**（不是活缺陷）。
⇒ **`Lord`→`noble`**（不是 `clan_leader`）；**"贵族"这个身份是靠 `Occupation=Lord` 认的**。

### ✅ 3. `is_clan_leader` / `min_skill` 能否表达"这人在某小派系" —— **不能**

- `is_clan_leader` 是**布尔一位**，只做两件事：① `IdentitiesForRole` 里**默认分支**用它
  兜底成 `noble`（`:189`）；② 条件匹配时**要求 NPC 的族长位与条件一致**（`WorldbookIdentityEvaluator.cs:75`）。
  它**只知道"是不是族长"，不知道"是哪一家"** ⇒ **与 clanId 无关，表达不了派系归属**。
- 实测（`role=noble` + `is_clan_leader` 真/假）：两者 scope/detail **完全相同**（`elite`/`secret`）
  ⇒ 这一位**不改变能听到什么**，只改"是/不是族长"这一条筛选。
- `min_skill` 只在 `query.Skills` 里比数值（`:79-82`），**与派系无关**。

⇒ **原结论：「系统里没有 `clan_ids`，属于火焰余烬这件事在条件层表达不了。」**
⇒ **★ 2026-09-24 追记（当天补实现，结论作废）**：见下。
⇒ 原替代方案（靠 §五-③ 的 `culture_ids` ＋ 口吻近似）**不再需要**。

### ★ 追记（2026-09-24）：`clan_ids` 已补实现，全链打通并验通

「一条说法只送给某个家族的人」现在**能表达了**。改了六处：

| # | 部位 | 文件 |
|---|---|---|
| ① | 授权 schema | `awake.worldbook.authoring.v1.schema.json`（`knowledge_rule.clan_ids`，值域 `^entity\.clan\.[a-z0-9]+…$`）|
| ② | 条件模型 | `src/WorldKnowledgeModels.cs`（`WorldKnowledgeCondition.ClanIds`）|
| ③ | 编译器 | `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs`（`CopyStringArray(..., "clan_ids")` ＋ `MapConditionId("clan_ids") => "clan"`）|
| ④ | 加载器 | `src/WorldKnowledgeLoader.cs:169`（`AddStrings(condition.ClanIds, value["clan_ids"])`）|
| ⑤ | 判定器 | `src/WorldbookIdentityEvaluator.cs`（`condition.ClanIds.Count > 0 && !ContainsValue(...)` ⇒ false；权重 30/条）|
| ⑥ | 查询侧 | `src/NpcDialogueService.cs`（`_heroClanId = hero.Clan?.StringId`）＋ `src/WorldbookModels.cs`（`WorldbookQuery.ClanId`）|

**另有一处编辑侧白名单**：`tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringEditorModel.cs:485`（`RuleConditionKeys`）——不加，编辑器里填了留不住。

**证据**（零替身探针 `tools/_uw_clan_gate_probe_20260924.py`，两轮全链）：
- 编译产物真落下条件：`"conditions": {"clan_ids": ["awake:clan:clan_empire_south_1"]}`（值归一成 `awake:clan:`）。
- 主轮（条件=TARGET）：TARGET 查 → 拿到 / OTHER 查 → 0 / 无 clan 查 → 0。
- **变异轮**（同一份档，只把条件换成 OTHER）：TARGET 查 → **翻成 0** / OTHER 查 → **翻成 >0** / 无 clan 查 → 0。
  两轮读数**真的跟着 clan 翻转** ⇒ 判定器确实在按 clan 分流（不是恒真/恒空）。

**★ 越界声明**：`docs/superpowers/specs/2026-08-24-worldbook-entity-catalog-design.md:31` 明写
「**B1 不实现 `hero_ids`/`clan_ids` 运行时权限条件**；作者选择结果先作为可预览的实体绑定数据，
运行时契约另立后续批次。」本次**直接实现了 clan 的运行时条件**，越过了这条批次边界。
= **这是甲方 09-24 明确裁定要的（`我就是要知识条目的某一个说法绑定一个 clan`）**，
设计稿 B1 条款视为被本次裁定覆盖；是否要回写设计稿、以及 `hero_ids` 是否同样跟进，待甲方定。

### ✅ 4. 变体 2 近似挂法 —— **已跑真探针，结论见 §五-③ 的 2026-09-24 补正**

零替身验台：`tools/worldbook-runtime-sim/`（把 `src/*.cs` 整个编进自己的程序集，`internal` 就近可见），
`probe` 模式内部**直接调真件** `WorldbookIdentityCapabilityRules.Resolve` / `WorldbookIdentityEvaluator`
⇒ 是读数，不是"另写一个引擎"。脚本：`tools/_uw_ident_probe_20260924.py`（值域）、
`tools/_uw_ident_report_20260924.py`（读数表）、`tools/_uw_pc_probe_20260924.py`（culture 闸阳性对照）、
`tools/_uw_occupation_20260924.py`（游戏 occupation 真写法）。

**★ 本轮的自我纠错**：我上一轮在 §五-③ 写的"用 `culture_ids:[empire]` 近似"**从没落进档里**；
探针跑出来"帝国与瓦兰迪亚平民拿到同一条"才发现。**教训与甲方那句同源：写进文档不算数，
要真落到产物上、并且跑得起来才算。**

## 七、记账

- 一手来源：`profile-registry.v1.json` / `awake.worldbook.authoring.v1.schema.json` /
  `AuthoringEditorModel.cs:485` / `RuntimePackageCompiler.cs:229-300` / `bannerlord_clans` 表 /
  `bannerlord_troops.occupation`（30 种驼峰职业）/ `WorldbookIdentityCapabilityRules.cs` /
  `WorldbookIdentityEvaluator.cs` / `BannerlordWorldbookIdentityAdapter.cs`。
- 真探针读数（零替身验台 `worldbook-runtime-sim`，包 `geo1-v26-uw-rumor`）：
  `tools/_uw_ident_probe_20260924.py` ＋ `_uw_ident_report_20260924.py` ＋
  `_uw_pc_probe_20260924.py` ＋ `_uw_occupation_20260924.py`（读数落
  `tools/worldbook-studio/workspace/full-geo1/_uw_*_20260924.json`）。
- **§六 四条已全部销账**；本档「做法定死」的部分可以用了。
