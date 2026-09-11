# AWAKE 知识系统—Bannerlord 游戏数据映射表

> 文档用途：为 AWAKE 的世界书、NPC 知识调取和周报知识分发设计提供本地证据索引。
>
> 审计日期：2026-08-21
>
> 审计对象：Mount & Blade II: Bannerlord `v1.3.15`，本地游戏目录 `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`
>
> 文档性质：只读审计与设计参考。本次不修改运行时代码、游戏目录或冻结候选。

## 1. 结论摘要

当前提议可以落地，但必须区分三层：

1. **原版事实层**：从 `Occupation`、`Hero`、`Clan`、`Settlement`、文化 XML、NPC 模板和战役事件读取的真实游戏数据。
2. **AWAKE 适配层**：把原版对象转换为稳定的角色、阶层、文化、地点和事件上下文，不把 TaleWorlds 实时对象长期保存到知识状态中。
3. **AWAKE 知识规则层**：知识深度、阶层上限、公开范围、传闻版本、推荐询问对象、贵族年龄曲线和周报分发，均属于 AWAKE 自定义设计，不是 Bannerlord 原生字段。

核心判断：

- “农民只知道传说、不了解完整历史”可以作为 AWAKE 知识规则，但原版没有 `PeasantKnowledge` 或“传说等级”字段。
- “公证商人、赎金经纪人、酒馆老板、头人是知识中介”有原版角色和运行时生成依据，但“他们掌握哪些知识、如何主动推荐”需要 AWAKE 增加派生画像。
- “贵族按年龄和 Steward 提升知识”有 `Hero.Age`、`Clan.IsNoble` 和 `DefaultSkills.Steward` 作为输入，但“知道更多政治/经济秘密”是设计映射，不是原版技能效果。
- “士兵熟悉战争、贵族兵种按比例缩减贵族信息”只有前半部分能由 `Occupation.Soldier`、文化和兵种模板支撑；后半部分没有原版 `NobleTroop` 社会类别，必须由 AWAKE 根据兵种模板、升级路径或 ID 规则推导。
- 原版存在 `CampaignEvents.WeeklyTickEvent`，AWAKE 当前也有周报入口；但当前 `WorldEventRecord` 只保存 `Day`、`Kind`、`Text`，还不能直接承担结构化知识分发。

## 2. 版本与证据来源

| 项目 | 本地证据 | 结论 | 状态 |
|---|---|---|---|
| 游戏版本 | `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SubModule.xml` | `v1.3.15` | 已核验 |
| 游戏版本交叉核验 | `Modules\SandBoxCore\SubModule.xml`、`Modules\Sandbox\SubModule.xml` | 均为 `v1.3.15` | 已核验 |
| 旧元数据 | `package_info.txt` 中的 `PC@v1.3.4` | 过时元数据，不用于版本判定 | 已排除 |
| 原版 Campaign API | `C:\Users\26811\OneDrive\文档\New project\.csys_full_decomp\TaleWorlds.CampaignSystem.decompiled.cs` | 用于核对枚举、属性和战役事件 | 已核验 |
| 原版 Core API | `C:\Users\26811\OneDrive\文档\New project\.core_full_decomp\TaleWorlds.Core.decompiled.cs` | 用于核对技能定义 | 已核验 |
| 原版 SandBox 逻辑 | `C:\Users\26811\OneDrive\文档\New project\.sandbox_full_decomp\SandBox.decompiled.cs` | 用于核对酒馆、赎金经纪人和周事件监听 | 已核验 |
| 原版文化配置 | `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml` | 用于核对文化角色模板 | 已核验 |
| 原版 NPC 配置 | `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spnpccharacters.xml` | 用于核对具体 NPC 模板与 Occupation | 已核验 |
| AWAKE 当前事件账本 | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\WorldEventLedger.cs` | 当前周事件结构 | 已核验 |
| AWAKE 当前周报入口 | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeEventBehavior.cs` | 当前通过小时 tick 检查每 7 天生成报告 | 已核验 |

## 3. 总体映射规则

| 提议概念 | 原版游戏证据 | 精确字段/数据 | AWAKE 映射 | 状态 | 注意事项 |
|---|---|---|---|---|---|
| NPC 身份 | `CharacterObject`、`Hero` | `Occupation`、`StringId`、`Culture` | `NativeIdentitySnapshot` 或等价的短生命周期上下文 | 可实现 | 不要长期保存 TaleWorlds 实时对象 |
| 人物文化 | `CharacterObject`、`Hero`、`Clan`、`Kingdom` | `Culture`、`Clan.Kingdom.Culture` | `culture_id`、`kingdom_id` | 可实现 | 必须明确使用人物文化还是所属王国文化 |
| 所属阶层 | `Clan`、`Hero`、Occupation、地点关系 | `Clan.IsNoble`、`Hero.IsNotable`、`Hero.Occupation` | `social_class` 派生值 | 需 AWAKE 规则 | 原版没有统一社会阶层枚举 |
| 地方性知识 | `Settlement` | `IsTown`、`IsCastle`、`IsVillage`、`Notables`、`BoundVillages` | `locality_scope`、`settlement_scope` | 可实现 | “了解本国”不能只靠地点类型，需要文化/王国规则 |
| 知识领域 | 原版没有四领域枚举 | 无直接字段 | `politics`、`economy`、`culture`、`war` | AWAKE 自定义 | 作为世界书和事件的一级索引 |
| 知识深度 | 原版没有知识记忆字段 | 无直接字段 | `D0-D6` 或简化等级 | AWAKE 自定义 | 不应伪装成原版技能等级 |
| 公开范围 | 原版事件、地点、阵营关系可作为事实来源 | 需组合读取 | `public`、`local`、`regional`、`faction`、`elite`、`private` | AWAKE 自定义 | 详细秘密只在允许的阶层/网络中可见 |
| 玩家传授 | 原版无通用“玩家教学”知识接口 | 对话、任务、事件可作为入口 | `player_taught` 知识声明 | 需实现 | 这是唯一建议保留高精度即时写入的通道 |
| NPC 批量学习 | 原版有每日/每周事件 | `DailyTick*`、`WeeklyTickEvent` | 周报/事件生成 `KnowledgePatch` | 需实现 | 不对每个 NPC 逐个调用 Provider |

## 4. Occupation 映射

原版 `Occupation` 枚举位于：

`C:\Users\26811\OneDrive\文档\New project\.csys_full_decomp\TaleWorlds.CampaignSystem.decompiled.cs:41258`

| 原版 Occupation | 设计角色 | 建议知识画像 | 原版依据 | AWAKE 处理 |
|---|---|---|---|---|
| `Villager` | 村民/农民 | 本地生计、地方传闻、传统叙事；政治/经济/战争多为片段 | 原版枚举 | 设为低或中低知识上限，不得自动读取完整贵族档案 |
| `Townsfolk` | 城镇普通居民 | 城镇传闻、市场信息、公共礼俗；对高层政治只知结果不知细节 | 原版枚举；公证人模板也使用此 Occupation | 必须结合 `CharacterObject.StringId` 做二级角色识别 |
| `RuralNotable` | 乡村重要人物 | 本地资源、村庄关系、领主征收和治安；比普通村民更完整 | 原版枚举 | 可作为地方知识节点，但不等于全国知识专家 |
| `Headman` | 村庄/城镇头人 | 本地和所属国家的行政、征收、治安、战争影响 | 原版枚举 | 建议作为 `local_authority`，国家知识范围由王国关系派生 |
| `Merchant` / `GoodsTrader` | 商人/贸易者 | 价格、路线、货物流动、税费和跨地区传闻 | 原版枚举；`merchant_vlandia` 为 `GoodsTrader` | 商贸知识高；政治秘密需额外权限，不因“商人”自动获得 |
| `Tavernkeeper` | 酒馆老板 | 多来源传闻、旅客消息、佣兵/俘虏/地方流言 | 原版枚举；原版 SandBox 创建酒馆角色 | 建议作为高覆盖、低可信度的知识中介 |
| `RansomBroker` | 赎金经纪人 | 俘虏、赎金、贵族动向、战争后果和跨阵营消息 | 原版枚举；原版 SandBox 创建角色 | 建议作为高覆盖、较高战争/政治线索的知识中介 |
| `Lord` | 贵族/领主 | 政治、战争、领地经济和贵族网络；深度随年龄、职位和 Steward 派生 | 原版枚举；`Clan.IsNoble` 是更可靠贵族信号 | 不只按 Occupation 判断贵族，优先组合 `Hero.Clan.IsNoble` |
| `Soldier` | 普通士兵/兵员 | 本国战争、军制、行军、战斗常识；政治高层细节有限 | 原版枚举；`vlandian_recruit` 示例 | 设 `war` 域高，其他域按文化/经历削减 |
| `Guard` / `CaravanGuard` | 守卫/商队护卫 | 治安、道路、边境、商路和局部战争 | 原版枚举 | 作为战争/道路信息的区域节点 |
| `Artisan` | 工匠 | 工坊、原料、价格、行业和城镇生活 | 原版枚举 | 经济/文化局部知识高，政治深层信息低 |
| `Preacher` | 传教者 | 宗教文化、习俗、教义和本地社会规范 | 原版枚举 | 文化域高；具体宗教内容须由世界书定义 |
| `GangLeader` / `Gangster` | 地下势力 | 黑市、犯罪、地痞网络和局部权力消息 | 原版枚举 | 作为非官方经济/治安信息源，可信度和动机分开建模 |
| `Wanderer` | 流浪者/游荡者 | 个人经历、跨地区见闻，真实性不稳定 | 原版枚举 | 适合低可信度跨区线索，不应自动当作权威档案 |

使用原则：

- `Occupation` 是角色职业标签，不是完整知识权限。
- `merchant_notary` 和 `rural_notable_notary` 虽然名称体现特殊功能，原版仍标为 `Townsfolk`；必须用模板 ID 或 AWAKE 角色标签二次区分。
- `Lord` 也不等同于“知道所有秘密”。贵族知识仍应受年龄、职位、所属国家、亲历事件和交往网络限制。
- 兵员的 `Soldier` 标签只说明职业/模板用途，不自动证明其知道某场具体战争。

## 5. 文化角色模板映射

原版文化角色字段位于：

`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spcultures.xml`

在 Empire、Aserai、Sturgia、Vlandia、Battania、Khuzait 等主要文化配置中，已核验到以下字段：

| 原版文化字段 | 典型值形式 | AWAKE 角色 | 设计用途 | 状态 |
|---|---|---|---|---|
| `merchant` | `NPCCharacter.merchant_vlandia` | 普通商人 | 经济知识节点 | 已核验 |
| `merchant_notary` | `NPCCharacter.merchant_notary_vlandia` | 公证商人 | 商贸、契约、债务和跨地区消息节点 | 已核验 |
| `ransom_broker` | `NPCCharacter.ransom_broker_vlandia` | 赎金经纪人 | 俘虏、赎金和战争消息节点 | 已核验 |
| `rural_notable_notary` | `NPCCharacter.rural_notable_notary_vlandia` | 乡村重要人物公证人 | 村镇关系、土地/债务/地方秩序消息节点 | 已核验 |
| `tavernkeeper` | `NPCCharacter.tavernkeeper_vlandia` | 酒馆老板 | 旅客消息、流言和多来源拼接节点 | 已核验 |
| `headman` / `notable_templates` 相关模板 | 文化下的 notable 模板配置 | 头人/地方权威 | 本地行政、征收和治安知识 | 已核验 |

限制：

- 这些字段是“文化默认模板映射”，不是保证每个地图阶段、每个聚落都存在一个实例。
- 当前 XML 中 `nord`、`vakken`、`darshi` 和部分 bandit 文化的对应字段出现空值或未配置；不得假设所有文化都有完整的中介角色。
- AWAKE 读取时应允许角色缺失，并通过附近聚落、其他文化中介或普通对话降级，而不是创建虚假的原版 NPC。

## 6. 具体 NPC 模板对照

以下以 Vlandia 为可复核样本，位置均来自：

`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\spnpccharacters.xml`

| NPC 模板 ID | XML 证据 | 原版 Occupation | AWAKE 角色标签 | 结论 |
|---|---:|---|---|---|
| `ransom_broker_vlandia` | `:27784-27790` | `RansomBroker` | `knowledge_broker.ransom` | 原版有独立职业，可直接识别 |
| `merchant_notary_vlandia` | `:29398-29403` | `Townsfolk` | `knowledge_broker.merchant_notary` | 不能只靠 Occupation 识别，必须保留模板 ID |
| `rural_notable_notary_vlandia` | `:33193-33198` | `Townsfolk` | `knowledge_broker.rural_notable_notary` | 同上，名称/模板是关键证据 |
| `tavernkeeper_vlandia` | `:36722-36728` | `Tavernkeeper` | `knowledge_broker.tavernkeeper` | 原版有独立职业 |
| `merchant_vlandia` | `:38893-38899` | `GoodsTrader` | `economic_node.merchant` | 商品商人不是 `Merchant` 枚举，而是 `GoodsTrader` 模板 |
| `vlandian_recruit` | `:14896-14902` | `Soldier` | `military_profile.common_soldier` | 可作为普通士兵画像样本，不是贵族兵种证明 |

## 7. 贵族、年龄和 Steward 映射

### 7.1 贵族判定

已核验的原版字段：

- `Clan.IsNoble`：
  `C:\Users\26811\OneDrive\文档\New project\.csys_full_decomp\TaleWorlds.CampaignSystem.decompiled.cs:31430-31432`
- `Hero.Clan`、`Hero.Occupation`、`Hero.Age`、`Hero.IsNotable` 等成员存在于 CampaignSystem 反编译代码中。

建议优先级：

1. `Hero.Clan != null && Hero.Clan.IsNoble == true` → `noble_clan_member`
2. `Hero.IsLord` 或符合领主/贵族模板 → `noble_office_holder`
3. 仅有 `Occupation.Lord` 但缺少有效 Clan 时 → 只作为职业线索，不直接授予全部贵族知识
4. 普通 `Townsfolk`、`Villager`、`Soldier` → 不授予贵族圈详细知识

### 7.2 年龄

`Hero.Age` / `CharacterObject.Age` 可以作为知识成熟度的输入，但原版没有“年龄带来知识”的自动机制。

| 年龄/经历因素 | 影响方向 | 不应直接推出的结论 |
|---|---|---|
| 年轻贵族 | 公开礼法、家族基础、近年事件 | 不应自动知道全部旧战争和宫廷秘密 |
| 成年贵族 | 家族、领地、战争和政治常识增加 | 不应自动知道其他国家的深层机密 |
| 年长贵族 | 历史、旧盟约、旧战争、家族网络增加 | 年龄不等于亲历所有事件 |
| 长期任职/管理领地 | 政治和经济细节增加 | 必须有职位/管理证据或 AWAKE 规则支持 |

### 7.3 Steward

`DefaultSkills.Steward` 已在：

`C:\Users\26811\OneDrive\文档\New project\.core_full_decomp\TaleWorlds.Core.decompiled.cs`

相关原版描述和使用语境涉及组织队伍、后勤、经营领地、管理城镇和军需官职责。CampaignSystem/SandBox 代码也多处通过 `GetSkillValue(DefaultSkills.Steward)` 读取该技能。

因此可以使用：

```text
政治/经济知识权重 = 基础阶层权重
                 + 年龄/经历修正
                 + Steward 分段修正
                 + 职位/领地管理修正
                 + 亲历事件修正
```

必须标注：

- `高 Steward -> 知道更多政治经济细节` 是 AWAKE 设计映射，不是原版效果描述。
- Steward 不应单独突破阶层边界；一个高 Steward 的平民可以更懂账目和物流，但不应因此读取贵族私密档案。

## 8. 士兵与贵族兵种映射

### 8.1 普通士兵

原版 `Occupation.Soldier` 位于：

`C:\Users\26811\OneDrive\文档\New project\.csys_full_decomp\TaleWorlds.CampaignSystem.decompiled.cs:41267`

`vlandian_recruit` 在 XML 中标为 `occupation="Soldier"`，且属于 `Culture.vlandia`。

建议 AWAKE 画像：

- `war` 域：高于普通平民；对本国军制、战斗常识、行军、补给和近期战事有较高可见度。
- `politics` 域：知道征召、领主、敌我阵营等公共层信息；不自动知道宫廷细节。
- `economy` 域：知道军需、粮食、装备和军饷的实用层面；不等于商人价格网络。
- `culture` 域：按所属文化和军旅经历读取。

### 8.2 贵族兵种

本次核验没有发现原版通用 `NobleTroop` Occupation 或“贵族兵种知识比例”字段。原版存在贵族兵种 ID、文化 `elite_basic_troop`、升级路径和装备/模板信息，但这些只能作为派生输入。

建议 AWAKE：

1. 通过文化兵种树、升级路径或已核验的精英兵种 ID 建立 `elite_troop` 注册表。
2. 保留原版 `Occupation.Soldier` 作为职业基础。
3. 叠加 `military_profile.elite`，而不是伪造原版 Occupation。
4. “贵族信息缩减比例”只作为 AWAKE 参数，不能写成 Bannerlord 原生规则。

## 9. 聚落与头人映射

可用原版对象/成员包括：

- `Settlement.IsTown`
- `Settlement.IsCastle`
- `Settlement.IsVillage`
- `Settlement.Notables`
- `Settlement.BoundVillages`
- `Hero.HomeSettlement`
- `Hero.IsHeadman` / `Occupation.Headman`

| 原版对象/字段 | AWAKE 派生用途 | 知识范围 |
|---|---|---|
| `Settlement.IsVillage` | 村庄节点 | 本村、本地领主、征收、治安、附近村庄传闻 |
| `Settlement.IsTown` | 城镇节点 | 市场、商路、行政、驻军、领主和区域传闻 |
| `Settlement.IsCastle` | 军事节点 | 驻军、边境、攻守和领主军事动向 |
| `Settlement.Notables` | 本地信息网络 | 将头人、商人、工匠、农村重要人物组合成知识源 |
| `Settlement.BoundVillages` | 城镇—村庄依附关系 | 生成地方经济、征收和供应链范围 |
| `Occupation.Headman` | 地方权威角色 | 本地行政和本国公共知识较高，但不自动知道贵族私密信息 |

“头人十分了解本国知识”应实现为**本国公共知识覆盖率高**，而不是“拥有全部政治秘密”：

- `war.public_national`：高
- `economy.local_regional`：高
- `politics.public_administration`：高
- `politics.elite_private`：低或受关系门槛控制
- `culture.local_customs`：高

## 10. 知识中介与主动推荐

### 10.1 原版基础

SandBox 反编译代码已核验：

- `TavernEmployeesCampaignBehavior` 注册 `CampaignEvents.WeeklyTickEvent`。
- 原版存在创建酒馆老板的 `CreateTavernkeeper`。
- 原版存在创建赎金经纪人的 `CreateRansomBroker`。
- 角色创建时从 `culture.Tavernkeeper`、`culture.RansomBroker` 读取文化模板。

这证明酒馆老板和赎金经纪人作为真实运行时角色有原版依据。

### 10.2 AWAKE 中介画像

| 中介 | 可提供的优先知识 | 可信度模型 | 推荐逻辑 |
|---|---|---|---|
| 公证商人 | 契约、债务、货物、税费、交易关系、跨地消息 | 账目事实高；传闻中等 | 问到陌生的政治/经济细节时，推荐询问公证商人 |
| 赎金经纪人 | 俘虏、贵族动向、战争结果、赎金和战场后果 | 俘虏/赎金事实高；政治判断中等 | 问到战争、贵族俘虏或战后处置时优先推荐 |
| 酒馆老板 | 旅客消息、佣兵、流言、失踪和地方传闻 | 覆盖面高；真实性分级 | 对无法确认的跨地区问题提供线索，不直接当作权威 |
| 村庄/城镇头人 | 本地行政、征收、治安、领主政策、国家公共知识 | 本地事实高；秘密信息受限 | 对地方和本国公共知识问题优先推荐 |

必须避免：

- 不因一个 NPC 名字含有“公证人”就假定它有独立原版职业；当前两个公证人模板的 Occupation 是 `Townsfolk`。
- 不因酒馆老板“听得多”就让其知道所有国家机密；应保留来源范围和可信度。
- 不因头人是地方权威就让其知道贵族圈内部秘密。
- 推荐目标必须先通过原版实例、文化模板或聚落名册确认存在；不能生成一个只存在于知识系统里的虚构角色并当作可对话对象。

## 11. 周报与批量知识更新映射

### 11.1 原版周事件

CampaignSystem 的 `CampaignEvents.WeeklyTickEvent`：

- 事件入口定义：
  `C:\Users\26811\OneDrive\文档\New project\.csys_full_decomp\TaleWorlds.CampaignSystem.decompiled.cs:19621`
- 事件派发：同文件 `:20783-20786`
- 原版行为注册示例：同文件 `:198968-198975`

这支持用周批次统一推进知识变化，而不对每个 NPC 进行 Provider 调用。

### 11.2 AWAKE 当前周报入口

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeEventBehavior.cs` 当前：

1. 监听 `HourlyTickEvent`。
2. 通过 `AwakeRuntime.CurrentGameDay()` 检查 `day % 7 == 0`。
3. 从 `WorldEventLedger.SnapshotWeek(day)` 读取本周事件。
4. 调用 `NarrativeReportBuilder.Build`。
5. 记录 `weekly_report` 事件。

这是一条可复用的周报入口，但目前主要生成报告文本，不等于已完成 NPC 知识分发。

### 11.3 当前 WorldEventRecord 的能力边界

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\WorldEventLedger.cs` 当前 `WorldEventRecord` 已包含：

- `EventId`：稳定幂等键
- `EventKey`：可选的来源事件键；相同键视为同一事件重试
- `Day`
- `Kind`
- `Domain`
- `Text`
- `OccurredAt`

能够：

- 保存事件摘要
- 按天筛选最近 7 天事件
- 生成世界周报

仍不能可靠表达：

- `domain`：政治/经济/文化/战争
- `actors`：涉及人物、军队、家族、王国
- `location`：村庄、城镇、城堡、区域
- `scope`：公开、地方、区域、国家、贵族圈、私人
- `reliability`：确定事实、官方公告、传闻、错误情报
- `detail_level`：摘要、常识、细节、秘密
- `eligible_profiles`：哪些知识画像可以收到
- `causation_id` / `source_event_id`：事件链追踪

当前事件层已保证内存、存储加载和周报生成按稳定 `EventId` 去重；同文本但不同 `EventKey` 的事件仍可作为两次合法事件。未来 `Worldbook v2` 或 `KnowledgePatch` 仍不应直接把 `WorldEventRecord.Text` 当作完整知识条目。应继续增加独立结构化事件层，或由 `WorldEventRecord` 映射到结构化事件，而不是把现有字段强行扩展成所有业务的总表。

## 12. 明确属于 AWAKE 的自定义概念

以下概念在本次原版审计中没有找到同名原生字段，必须在代码和文档中标注为 AWAKE 设计：

- `KnowledgeClaim`
- `KnowledgeLedger`
- `KnowledgeFrontier`
- `KnowledgeBroker`
- `ReferralContext`
- `WeeklyKnowledgePatch`
- `D0-D6` 知识深度
- 贵族圈/平民知识上限
- 公共、地方、区域、阵营、贵族圈、私人知识范围
- 传闻版本、可信度和来源追踪
- 公证商人/赎金经纪人/酒馆老板的知识覆盖画像
- 贵族年龄—Steward 知识曲线
- 贵族兵种的“贵族信息缩减比例”
- 周报对 NPC 的批量知识更新

这些不是问题；问题在于不能把它们写成 Bannerlord 原版已有系统。

## 13. 面向 Worldbook v2 的推荐实现边界

### 13.1 原版适配输入

建议只读取并转换为短期快照：

```text
character_id
occupation
template_id
culture_id
kingdom_id
clan_id
is_noble_clan
is_lord / is_notable / is_headman
age
steward_skill
home_settlement_id
settlement_type
bound_village_ids
troop_profile_id
recent_event_ids
```

### 13.2 世界书条目最小字段

未来条目建议至少包含：

```json
{
  "id": "war.vlandia.border_raid.001",
  "domain": "war",
  "subject_ids": ["kingdom.vlandia", "settlement.example"],
  "fact_level": "public",
  "scope": "regional",
  "detail_level": 2,
  "source_kind": "event",
  "keywords": ["边境", "劫掠", "瓦兰迪亚"],
  "eligible_profiles": ["soldier", "headman", "tavernkeeper", "lord"],
  "blocked_profiles": ["ordinary_villager"],
  "summary": "……",
  "detail": "……",
  "rumor_variants": [],
  "source_event_id": "event.example"
}
```

字段命名是设计建议，不代表当前运行时已经支持。实现前仍需经过 AWAKE 的 `grill-me-codex`、锁定计划、独立只读审查和用户签收。

### 13.3 查询顺序

建议 Worldbook v2 使用以下顺序，避免把整本世界书直接塞给 NPC：

1. 先根据 `domain + subject + settlement/kingdom + scope` 做硬过滤。
2. 再根据 NPC 的 `knowledge_profile` 做允许/禁止判断。
3. 再按 `detail_level` 和可信度选择摘要或细节。
4. 最后使用关键词/本地索引或 Marcus RAG 做窄范围检索。
5. 若没有可见条目，返回“未知/只知传闻/推荐询问某类中介”，不得补写超出权限的事实。

## 14. 推荐验证清单

未来实现前，至少要分别验证：

- 版本：读取的游戏目录确为 `v1.3.15`。
- 角色：公证商人、赎金经纪人、酒馆老板和头人是否能由模板/实例正确识别。
- 负例：普通 `Townsfolk` 不会被误判为公证商人。
- 阶层：`Clan.IsNoble=false` 的人物不会因为高 Steward 读取贵族私密知识。
- 战争：普通士兵能读取战争公共知识，但不能自动读取未经历的宫廷秘密。
- 文化：缺少角色模板的文化不会被系统伪造出对应 NPC。
- 周报：同一周批次只处理一次，不对每个 NPC 逐个调用 Provider。
- 事件：没有结构化 scope/domain 的旧 `WorldEventRecord` 不会被误当成完整知识声明。
- 回退：无法匹配知识条目时，NPC 保持未知或传闻状态，并可推荐真实存在的中介。

## 15. 最终边界声明

本表确认：此前提出的世界书档案馆、阶层知识差异、玩家传授、周报批量更新和知识中介方向是**可行的 AWAKE 架构设计**，但不能直接宣称为 Bannerlord 原版机制。

最稳妥的落地方式是：

```text
原版对象/XML/事件
    -> AWAKE GameData 快照
    -> knowledge_profile 派生
    -> 世界书硬过滤
    -> 本地窄检索或 Marcus RAG
    -> NPC 可见摘要/未知/推荐中介
```

当前阶段只完成了证据映射和边界确认；没有修改 `src`、没有生成新 DLL、没有同步游戏目录，也没有改变冻结的 AWAKE 候选。
