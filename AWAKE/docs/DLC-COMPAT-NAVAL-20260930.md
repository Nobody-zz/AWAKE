# NavalDLC（战帆）兼容性评估

> 评估日期：2026-09-30
> 评估对象：官方 DLC `NavalDLC`（中文名「战帆」），`v1.2.8`
> 结论一句话：**DLC 不是硬依赖 —— 不带 DLC 必须能玩；但内容侧已经在写 Nord，运行时却完全没接。这个缺口是本次评估的真问题。**

---

## 一、事实基线（全部实测，非推断）

### 1.1 基础游戏已全线升到 v1.4.8

| 模块 | Version | ModuleType | DefaultModule | 依赖 |
|---|---|---|---|---|
| Native | **v1.4.8** | Official | — | — |
| SandBoxCore | **v1.4.8** | Official | true | Native |
| SandBox | **v1.4.8** | Official | true | Native, SandBoxCore |
| StoryMode | **v1.4.8** | Official | false | Native, SandBoxCore, Sandbox |
| **NavalDLC** | **v1.2.8** | **OfficialOptional** | **true** | Native, SandBoxCore, Sandbox, StoryMode |
| CustomBattle | v1.4.8 | Official | true | Native, SandBoxCore |
| Multiplayer | v1.4.8 | Official | true | Native |

> 取值方式：`xml.etree` 解析各 `SubModule.xml`（避开 grep 对含中文 XML 的读取阻断）。

**三个硬结论**：

1. **基础游戏已升 v1.4.8**。AWAKE 现行 `SubModule.xml` 无 `RequiredBaseVersion` 声明，`AWAKE.csproj:8` 默认 `BannerlordApi=1.3.15` —— **默认构建目标已落后于现行游戏**。
2. **`OfficialOptional` ⇒ DLC 不是硬依赖**，与本轮甲方口径一致。玩家勾掉 `NavalDLC` 也能进游戏。
3. **但 `DefaultModule=true`** —— 默认加载。⇒ 绝大多数玩家**实际会带 DLC**，AWAKE 不能假设它不存在。

### 1.2 AWAKE 工具链早就备好了 1.4.8

```
AWAKE.csproj:51  <Error Condition="'$(BannerlordApi)' != '1.4.8' and '$(BannerlordApi)' != '1.3.15'" ... />
AWAKE/tools/build.ps1:3  [ValidateSet('1.3.15', '1.4.8')]
AWAKE/tools/build.ps1:28  if ($detectedVersion -ne "v$BannerlordApi") { throw ... }
```

⇒ **不是「不支持 1.4.8」，是「默认没切过去」**。`build.ps1` 自带版本校验，传 `-BannerlordApi 1.4.8` 即可。
⚠️ 注意 `build.ps1:28` 的 `throw`：构建目标必须与 `GamePath` 的 Native 版本**严格相等**，不能混。

### 1.3 AWAKE 对 DLC 的当前接入度 = 0

| 检查项 | 结果 |
|---|---|
| `src/**/*.cs` 中 `NavalDLC` 命中 | **0** |
| `src/**/*.cs` 中 `naval`/`Naval`/`Ship` 命中 | **0**（grep 到的 `ship` 全部是 `relationship` 的误匹配） |
| `SubModule.xml` 依赖 NavalDLC | **无** |
| 运行时模块探测（`ModuleInfo`/`GetModule`） | 未用于 DLC |

⇒ **`Awake.dll` 完全不知道战帆 DLC 存在。**

---

## 二、⭐ 真问题：内容侧已写 Nord，运行时零感知

### 2.1 世界书里已有 4 份引用 DLC 实体的正典档

| 档 | 域 | 摘要 |
|---|---|---|
| `clan-clan_nord_1.yaml` | politics | 许尔夫家族；11 名在册成员 |
| `clan-clan_nord_2.yaml` | politics | 肖尔德家族；6 名在册成员 |
| `clan-clan_nord_3.yaml` | politics | 奥特尔家族；5 名在册成员 |
| `military-nord.yaml` | war | 诺德军事力量：盾墙核心、铩戟兵、投斧手、**长船**、无骑兵 |

`military-nord.yaml` 的引文来自 `source.calradia.chronicle.animusforge.war3`，逐字引到「马在船上站不稳」「重装步兵是盾墙的骨头」—— **正文假定 Nord 是一个真实存在的、有海权的势力**。

另有 11 份档（`troops-*`、`weapons-armor-*`、`weapons-head-*`）在正文或锚点里提到 nord 相关物项。

### 2.2 矛盾

```
内容层：假设 Nord 存在（有家族、有军制、有长船、有海权）
          ↑
运行时层：零感知（不探测 DLC、不识别 nord 势力、不认长船/港口实体）
```

**表现预测（游戏内）**：
- 带 DLC：世界书能检索到「许尔夫家」「诺德军事力量」，但**运行时不知道 nord 是什么** ⇒ 条件判定（`kingdom_ids`/`clan_ids`/`settlement_ids`）对 nord 实体无法命中。
- 不带 DLC：这 4 份档成了**指向不存在实体的孤儿档**，检索命中后给出的信息玩家在游戏里找不到对应物。

### 2.3 DLC 规模（供内容侧估算工作量）

| 文件 | 根元素 | 条数 | 说明 |
|---|---|---|---|
| `ship_hulls.xml` | `<ShipHulls>` | 48 | 船体；含 `total_crew_capacity`/`max_hitpoints`/`sea_worthiness`/`inventory_capacity`/`has_hold`/`base_speed` |
| `ship_slots.xml` | `<ShipSlots>` | 10 | fore/aft/bow/hull/side/deck/oars/sail/roof/medium_fore |
| `mission_ships.xml` | `<MissionShips>` | 48 | 任务用船 |
| `kingdoms.xml` | `<Kingdoms>` | **1** | `nord`，都城 `castle_EN2`，君主 `lord_7_1`，**与 vlandia 交战**，政策 `policy_feudal_inheritance` |
| `clans.xml` | `<Factions>` | 12 | `northern_pirates`/`southern_pirates`/`skolderbrotva` + `clan_nord_1..9` |
| `naval_cultures.xml` | `<SPCultures>` | 1 | `nord`（`southern_pirates` 归 bandit） |
| `settlements.xml` | `<Settlements>` | 540 | ⚠️ **是对原版聚落的增补（加港口/海路属性），不是 540 个新聚落** |
| `heroes.xml` | `<Heroes>` | 56 | |
| `naval_lords.xml` | `<NPCCharacters>` | 56 | |
| `items.xml` | `<Items>` | 131 | |
| `Languages/CNs/*.xml` | — | 21 文件 | 完整官方简中本地化（UTF-16） |

> ⚠️ 子元素名常与文件名不同：`naval_partyTemplates.xml`→`<MBPartyTemplate>`、`naval_equipment_sets.xml`→`<EquipmentRoster>`、`naval_custom_battle_scenes.xml`→`<Scene>`。按文件名猜标签会得到 0。

---

## 三、处置口径（按「软依赖 + 运行时探测」）

### 3.1 硬纪律

1. **不得把 NavalDLC 写进 `DependedModules`**。一旦写进去就变成硬依赖，不带 DLC 的玩家直接进不去游戏 —— 违背甲方口径。
2. **一切 DLC 相关代码走运行时探测**，探测不到就静默降级（不报错、不空转、不留半成品状态）。
3. **探测结果要落日志**（`Awake.log` 一行 `naval_dlc_present=True/False`），便于真机核对。

### 3.2 三条待办（按依赖顺序）

| # | 事项 | 判据 |
|---|---|---|
| **N0** | 切构建目标到 `1.4.8`（`build.ps1 -BannerlordApi 1.4.8`），确认 `Awake.dll` 在 v1.4.8 下编译通过 | 构建 0 错；`build.ps1` 版本校验通过 |
| **N1** | 加**DLC 探测**（软探测，`SubModule.xml` 声明不动） | 离线探针：带/不带 DLC 两条路径都能跑；日志两态可辨 |
| **N2** | 内容侧：给 4 份 nord 档补**DLC 条件**（或缺 DLC 时的降级说明） | 不带 DLC 时这些档不进检索索引 |

### 3.3 需要甲方裁定的问题

1. **不带 DLC 时，4 份 nord 档怎么办？** 三个选项：
   - (a) 完全隐藏（连检索都进不去）
   - (b) 保留但改写成「传闻/异域传说」语体（玩家知道有这个地方，但游戏里到不了）
   - (c) 照发，由玩家自己理解
2. **`military-nord.yaml` 的正文把「长船」写成军制核心** —— 不带 DLC 的玩家永远见不到船。是否需要一个「无 DLC 版本」的表述？

> 这两条**不定不动手**。本轮只做评估，不改内容。

---

## 四、附：为什么这不是「升个版本号就完事」

`SubModule.xml` 的 `<Version value="v0.2.0" />` **与游戏 API 版本无关** —— 那是 AWAKE 自己的版本。
真正决定「能不能加载」的是：

1. `Awake.dll` 编译时引用的 TaleWorlds 程序集版本（由 `BannerlordApi` 决定）；
2. `SubModule.xml` 里 `<DependedModules>` 声明的模块是否存在。

⇒ 现状是 **① 落后（编在 1.3.15）、② 缺 DLC 声明（但也不该补成硬依赖）**。两件事要分开处置。
