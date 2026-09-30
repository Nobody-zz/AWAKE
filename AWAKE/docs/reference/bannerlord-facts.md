# Bannerlord 游戏事实 · 发现全集

> 由 `AWAKE/tools/gen_skill_refs.py` 从既有 skill 的发现提取生成。**不要手改**——改了会被下次重跑覆盖。
>
> **这是跨 agent 的共享知识**（见 `AWAKE/AGENTS.md`〈跨 agent 共享知识〉）。任何 harness 的 agent 都应读这里，不要各自维护私有副本。
>
> 共 **366** 条发现，其中 **160** 条标了 `needs_reverify`（测量于旧版本，引用前复核）。

## 头部规则

1. **`valid_for` 是测量版本，不是当前版本。** 本机游戏已从 v1.3.15 升到 **v1.4.8**；凡 `valid_for=v1.3.15` 的，引用前先复核。
2. **`未标版本` 的条目 = 提取时未记录版本**，请**按 v1.3.15 对待**（即同样需要复核）。
3. **否定式断言（`negative-claim`）风险最高**——最容易因升级变成假话。先看文末〈待重验队列〉。
4. **`出处` 只到「旧 skill 名 # 小节名」一级。** 旧 skill 在各 harness 的私有目录里（`~/.workbuddy/skills` 等），**不在本仓库**，故不保留行号——留着是假的可点性。
5. 本文件是**世界里的事实**，不是程序。怎么做任务看对应 skill 或 `AWAKE/AGENTS.md`。

## 按 kind 索引

| kind | 条数 | 待重验 |
|---|---|---|
| `engine-behavior` | 72 | 20 |
| `byte-layout` | 20 | 5 |
| `db-schema` | 13 | 6 |
| `measured-number` | 103 | 34 |
| `silent-failure` | 24 | 18 |
| `api-quirk` | 45 | 14 |
| `negative-claim` | 47 | 47 |
| `version-fact` | 8 | 4 |
| `path-fact` | 34 | 12 |
| **合计** | **366** | **160** |

---

# 引擎行为（engine-behavior）

> 共 72 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-046 | 政策的数值效果写死在 C# 模型里，不是 XML 数值表。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-050 | FeatObject 有 EffectBonus(float)、IncrementType（Add 平加 / AddFactor 因子）、IsPositive。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-052 | 文化专长数值写在 C#，XML 只引用 id。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-058 | AddFactor/Add 的 description 参数会进 StatExplainer，显示在游戏内数值明细 tooltip 的逐条来源里。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-073 | MCP 服务不需要 cwd：索引位置由 src/utils/env.ts:6 root=join(import.meta.dir,'../../') 按脚本自身路径推导。 | 未标版本 |  | bannerlord-game-data-access#MCP 直连 |
| bgda-080 | DefaultPartySpeedCalculatingModel.BaseSpeed => 4f，是每游戏小时推进的地图单位数。 | v1.3.15 |  | bannerlord-game-data-access#MCP 直连 |
| bgda-082 | GetDistance 内部用 Position.Distance(GatePosition)，故 settlements.xml 的 posX/posY 与运行时光标同坐标系。 | v1.3.15 |  | bannerlord-game-data-access#MCP 直连 |
| bgda-100 | 地图 Y 轴增大＝向北（西比尔 posY=586 最北斯特吉亚，胡比亚 posY=107 最南阿塞莱）；屏幕 y 向下，画图必须翻转 Y。 | v1.3.15 |  | bannerlord-game-data-access#解析 settlements.xml 坐标 |
| bgda-104 | 村庄产出是『基础＋追加』两段：Initialize 垫基础产出，AddProductions 追加特色产出。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-107 | IsFood 设置路径①：代码 InitializeTradeGood(...,isFood)，全原版仅 DefaultItems.cs:119/120 传 true（谷物、肉）。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-108 | IsFood 设置路径②：XML 的 Item IsFood 属性，全原版仅 SandBoxCore/ModuleData/items/horses_and_others.xml 带它。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-112 | 村庄绑的可能是城镇也可能是城堡（Settlement.town_X / castle_X）。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-116 | 村庄日产量 = 基值 × (炉灶等级+1) × 0.5（DefaultVillageProductionCalculatorModel.cs:31）。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-117 | 炉灶等级 0/1/2 的分界在 200 与 600（Village.cs:320），倍率只可能 0.5/1.0/1.5。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-119 | CalculateDailyFoodProductionAmount = 炉灶等级+1，同一个倍率也是城的口粮。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-120 | 『城的粮』与『谷物这个货物』在引擎里是两套数，没有换算关系。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-121 | 需求 := 0.85×昨日 + 0.15×[BaseDemand×(繁荣+extra) + LuxuryDemand×max(0,繁荣−3000)]。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-122 | 供给 := 0.85×昨日 + 0.15×[该类别在城货架上的货值 InStoreValue]。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-123 | 价格因子 := (需求/(0.1×供给+0.04×货值+2))^0.6，货物钳 [0.1,10]。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-124 | 引擎从不问货是哪来的：谁的货进了城就算谁的供给。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-128 | 村民卖货进城的上限是城的金币 ÷ 单价（SellGoodsForTradeAction.cs:52）。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-130 | Village.Bound 是附属（谁管你），可为城也可为堡，XML 写死 bound=Settlement.X。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-131 | Village.TradeBound 是市场归属（货卖去哪），永远是城，运行时按地理派。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-132 | 只有附属是堡的村才需要派归属；城属村 TradeBound ≡ Bound，getter Village.cs:107 / setter :115 两处守卫堵死。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-133 | 派归属的循环只遍历城堡（Campaign.Current.AllCastles），城属村不在其中。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-134 | Village.Bound 全库唯一写点是 Village.Deserialize（Village.cs:300，private set），附属不在运行时变。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-136 | GetTradeBoundToAssignForVillage 三步：最近同阵营城且距离<上限 → 最近非同阵营不交战城且距离<上限 → null。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-137 | UpdateTradeBounds 在开新档/读档/换领主/宣战/媾和/家族换国/家族灭亡时全部重算。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-138 | 距离上限化得掉两个运行时量：TradeBoundDistanceLimitAsDays × 队伍速度 × 24 = 平均城间距 × 3（Campaign.cs:1231）。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-140 | DistributeInitialItemsToTowns（:52-105）只在新档开局跑一次，把全图的村按距离摊给每座城铺底板存货。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-141 | 摊货权重：TradeBound 对上的给满 1.0，没对上的给 (同阵营?1.0:0.6)×0.5×((w_村+w_归属城)/2)，w=min(0.5, 0.5×600/距离^1.5)。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-142 | Village.GetItemPrice → TradeBound.Town.MarketData（Village.cs:261-274），村价与附属无关。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-151 | 界面提示：村『从属主城』=Bound、『贸易从属主城』=TradeBound；城『附属村庄』=BoundVillages、『贸易附属村庄』=TradeBoundVillages。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-153 | 村属城时『从属主城』与『贸易从属主城』两行必同。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-156 | Town.OnLoad() 清空缓存，调用点 Village.cs:117/119 与 :298/303（被 != SavedCampaign 挡着），读档后城自己附属的村不再登记回去。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-157 | DefaultSettlementFoodModel：+15 加每座附属村 (炉灶+1)×6，不看村产什么，村非 Normal 记 0。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-158 | CalculateDailyProductionAmount 的基础项整段包在 if (village.TradeBound != null) 里，没市场归属就不产。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-159 | 城的建筑对村产的加成走 Bound，不走市场归属。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| F-011 | SandBox/GUI/Brushes/MapBar.xml 里 BrushLayer 的 Name 等于对应元素的 StringId，是按字符串查表 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §5 第四路 |
| F-021 | WidgetInfo.CollectWidgetTypes 与 TextureProviderFactory.RefreshProviderTypes 靠反射遍历 AppDomain 登记类型 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7 第 1 问 |
| F-022 | 被反射扫描登记的控件类型无需任何注册动作，我们自己程序集里的类型同样会被扫到 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7 第 1 问 |
| F-025 | BrushLayer.Rotation 是 XML 可写的角度属性，Brush.Clone() 之后能逐控件设置 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §8 09-18 更正 |
| F-026 | 一段斜线可用一根细长条加一个角度画出来，平滑折线纯界面即可实现，不必走运行时贴图 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §8 |
| F-027 | TownMarketData.UpdateStores() 每天跑一次，是价格的全量重建纠偏点 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.5 实证 |
| F-028 | InStore 的写值点是 OnTownInventoryUpdated，挂在 ItemRoster.RosterUpdatedEvent 上，仓库一动就写，与日 tick 无关 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.5 第 3 步实证 |
| F-030 | AddNumberInStore 同时改 InStore（件数）和 InStoreValue（价值），而价格公式只吃 InStoreValue | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.5 附带一条 |
| F-034 | 广播事件的派发侧在 CampaignEventDispatcher.cs，触发点形态是 CampaignEventDispatcher.Instance.OnXxx( | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 2 步 |
| F-037 | 官方常把机制挂在 perk 上，DefaultPerks.cs 里的同名效果通常附一份参考实现 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 4 步 |
| F-051 | 原版 GraphWidget（ExtraWidgets.Graph）配 VM GraphVM（TaleWorlds.Library.Graph），属性名逐一对应，网格/轴标签/自动量程/坐标映射现成 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §8 实战范例 |
| F-055 | 给主控栏加一项只需实现一个 INavigationElement，不必改官方预制件 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §8 实战范例 |
| F-064 | 模组 bin/ 下有非 .NET 原生 dll，或存在 versions/<游戏版本>/ 目录，说明它做了进程内原生调用或跨版本适配 | 未标版本 |  | bannerlord-mod-recon §2 第 1 步 |
| F-066 | AnimusForge 生图是两段式：游戏数据 → 文本模型写角色卡 → 只取 appearance → 图像模型，比单段多一次模型调用 | 未标版本 |  | bannerlord-mod-recon §3 |
| T23 | UI 图集的 sheet 尺寸常常就是该 sprite 自身尺寸（ui_textures 分类 sheet2 = 1184×396 = paper_texture_tile 尺寸）。 | v1.3.15 | 是 | bannerlord-tpac-extraction §2 尺寸从哪来 |
| T32 | UI 图集条目名 = <SpriteCategory 分类名>_<sheetID>。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ★ UI 图集的命名与落点 |
| T34 | ui_textures 是原版唯一一族 AlwaysLoad 的材质，一张一个 sprite，sheet 尺寸等于 sprite 尺寸。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ★ UI 图集的命名与落点 |
| T47 | SpritePart 已给出 SheetID/Name/Width/Height/SheetX/SheetY/CategoryName，切片区域不必自己算格子。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 步骤 2 |
| T49 | 图集由 SpriteSheetGenerator 自动排版，格子大小不等：主控栏图标散在 X=3267~3755 之间，Y 恒为 4。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 ★ 为什么必须现读 SpriteData.xml |
| T50 | 同一分类的不同模块各有一份自己的图集，ui_mapbar 只可能在 SandBox 包里。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 ★ 为什么必须现读 SpriteData.xml |
| T54 | MeshId=11 即 banner_background_test_11，带 is_base_background，是纯色平底，王国旗都用它。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5（MeshId=11） |
| T63 | 原版实际用到的底纹只有 8 个：_1 左半、_11 平底、_14 宽斜分、_16 十字/星、_17 放射星芒、_24 中央竖条、_34 中央圆、_35 带城垛锯齿的横向分割。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 8 个实际被用到的底纹实测 |
| T64 | GetPrimaryColorId() 取层的 ColorId，GetSecondaryColorId() 取 ColorId2。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 ★ 底纹双色 |
| T65 | BannerVisual.ConvertToMultiMesh() 对背景层交换两色：mesh.Color=ColorId2、mesh.Color2=ColorId。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 BannerVisual.ConvertToMultiMesh() |
| S17 | 日志写入不 try/catch 吞掉异常，日志失败会把游戏带崩。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4（日志写一定要 try/catch） |
| S18 | 存档系统是类型驱动的：SyncData 每个字段必须能在启动时那份类型定义表里查到整个闭合泛型，查不到则整个存档失败。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 根因 |
| S19 | 存档失败日志串：Cant find definition for System.Collections.Generic.Dictionary`2[[Town],[Int32]]。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 症状（日志块） |
| S19b | 存档失败同时打印 Couldn't save because of errors listed below. 与 [0]SaveContext Error。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 症状（日志块） |
| S26 | 自定义 SaveableTypeDefiner 要占一段 saveId，必须自行保证不与其他模组撞号。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 改法乙（⚠ 要占一段 saveId） |
| S27 | 主控栏按钮是数据驱动的：MapBar.xml:64 的 ListPanel DataSource 绑 MapNavigation\NavigationItems。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8（MapBar.xml:64） |
| S28 | MapNavigationVM 构造函数遍历 GetElements()，逐个 new MapNavigationItemVM 加进 NavigationItems。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8（MapNavigationVM 构造函数） |
| S29 | 主控栏上一个按钮 = 一个 INavigationElement，点击入口是 OpenView()。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8（⇒ 一个按钮 = 一个 INavigationElement） |
| S42 | Harmony postfix MapBarVM 构造函数追加 NavigationItems 时，读档或重开战役会反复走，追加前必须按 ItemId 查重。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.1 步 2 |
| S44 | 日志文件没出现就等于模组没加载，多半是三个名字对不上或两处落点缺一处。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6（日志文件没出现 = 模组没加载） |

## 证据

- **bgda-046** DefaultSettlementProsperityModel.cs:180-198：ActivePolicies.Contains(DefaultPolicies.RoadTolls) → explainedNumber.Add(-0.2f, RoadTolls.Name)。
- **bgda-050** 读 CharacterDevelopment/FeatObject.cs。
- **bgda-052** XML 形如 cultural_feats/feat id=...；读入见 CultureObject.cs:395-407。
- **bgda-058** 读 ExplainedNumber 与 StatExplainer 的消费链。
- **bgda-073** 实测从任意目录启动都行。
- **bgda-080** 移动结算 MobileParty.NextMoveDistance = Speed * dt，dt 即小时增量。
- **bgda-082** 读 DefaultMapDistanceModel.GetDistance 实现。
- **bgda-100** 对 settlements.xml 坐标排序与游戏内南北对照实测。
- **bgda-104** 读 DefaultVillageTypes.cs 的构造调用。
- **bgda-107** 读 DefaultItems.cs 并全量 grep 传参。
- **bgda-108** 全原版模块 grep IsFood 属性，仅一份文件命中。
- **bgda-112** 读 settlements.xml 的 bound 属性取值。
- **bgda-116** 读该模型 CalculateDailyProductionAmount 实现。
- **bgda-117** 读 Village.GetHearthLevel()。
- **bgda-119** 读 DefaultSettlementFoodModel 相关实现。
- **bgda-120** 两条数值链只在村民挑粮进城这一动作上间接相连。
- **bgda-121** 读 TownMarketData / ItemConsumptionBehavior 的每日结算式。
- **bgda-122** ItemConsumptionBehavior.cs:215 把 categoryData.InStoreValue 当 dailySupply 传进去。
- **bgda-123** 读 DefaultTradeItemPriceFactorModel 实现。
- **bgda-124** 供给式只取城货架 InStoreValue，无来源字段。
- **bgda-128** 读 SellGoodsForTradeAction 实现。
- **bgda-130** 读 Village.cs 与 settlements.xml 的 bound 属性。
- **bgda-131** 读 Village.cs 与 VillageTradeBoundCampaignBehavior。
- **bgda-132** 读 Village.cs 两处分支。
- **bgda-133** VillageTradeBoundCampaignBehavior.cs:60 的 foreach (Town allCastle in Campaign.Current.AllCastles)；_castles 只装 IsCastle（Campaign.cs:709-712）。
- **bgda-134** 全库 grep Bound 赋值点后确认唯一。
- **bgda-136** 读 DefaultVillageTradeModel 实现。
- **bgda-137** 读 VillageTradeBoundCampaignBehavior 的事件订阅。
- **bgda-138** 读 Campaign.cs 的平均城间距定义与上限换算。
- **bgda-140** 读 VillageGoodProductionCampaignBehavior。
- **bgda-141** 读 DistributeInitialItemsToTowns 的权重式。
- **bgda-142** 读 Village.GetItemPrice。
- **bgda-151** 读详细提示拼装代码与本地化串 str_bound_settlement/str_trade_bound_settlement。
- **bgda-153** 由 TradeBound ≡ Bound 的守卫推出。
- **bgda-156** 读 Town.OnLoad 与 Village 的两处登记调用。
- **bgda-157** 读 DefaultSettlementFoodModel 实现。
- **bgda-158** 读 DefaultVillageProductionCalculatorModel 的分支。
- **bgda-159** if (village.Bound.IsFortification) village.Bound.Town.AddEffectOfBuildings(VillageProduction, ...)。
- **F-011** 读 SandBox/GUI/Brushes/MapBar.xml 的 BrushLayer 条目，与元素 StringId 逐条对照
- **F-021** 反编译 TaleWorlds.GauntletUI.PrefabSystem.dll，读这两个扫描函数的实现
- **F-022** 读 CollectWidgetTypes 实现：遍历 AppDomain 全部程序集，非白名单
- **F-025** 读官方 MapIncident.xml:76 的 Rotation 用法，并核对 Brush.Clone() 的语义
- **F-026** 由 MapIncident.xml:76 的 Rotation 用法推导（§8 明确更正此前'折线画不出来'的错断言）
- **F-027** 反编译读 TownMarketData.UpdateStores 及其订阅的 DailyTickTownEvent
- **F-028** grep InStore 的全部 Set*/Add* 方法，再 grep 这些方法的调用方，定位到 OnTownInventoryUpdated
- **F-030** 读 TownMarketData.AddNumberInStore 的实现与价格公式的入参
- **F-034** 读 CampaignEventDispatcher.cs 并 grep 该调用形态
- **F-037** 在 DefaultPerks.cs 找到 Trade.InsurancePlans，并定位到其实现 DestroyPartyAction.cs:19-22
- **F-051** 反编译 ExtraWidgets 与 TaleWorlds.Library，逐属性对照 GraphWidget 与 GraphVM
- **F-055** 由 MapNavigationHandler.OnCreateElements 的 protected virtual 扩展点推导
- **F-064** 在第三方模组目录结构上归纳（§2 第 1 步的判据）
- **F-066** 读其日志、提示词与请求体原文后归纳（§3 第 1 个提问角度）
- **T23** 把 NativeSpriteData.xml 的 SpriteSheetSize 与 SpritePart 尺寸对照得出。
- **T32** 用 NativeSpriteData.xml 的分类名与 sheetID 拼名后能在对应 tpac 里反查到条目。
- **T34** 读 NativeSpriteData.xml 中 ui_textures 分类的 AlwaysLoad 标记与 sheet 定义。
- **T47** 读 Modules\SandBox\GUI\SandBoxSpriteData.xml 的 <SpritePart> 节点字段。
- **T49** 读 SandBoxSpriteData.xml 中各 SpritePart 的 SheetX/SheetY 实测分布。
- **T50** 在 Native 与 SandBox 两处包内反查 ui_mapbar 分类，只在 SandBox 命中。
- **T54** 对照 banner_icons.xml 的 Background 表与 8 家王国 key 得出。
- **T63** 解出这 8 个网格并渲染后逐一辨认形状。
- **T64** 读 TaleWorlds.Core 的 Banner.cs 中两个取值方法的实现。
- **T65** 反编译 BannerVisual.ConvertToMultiMesh() 读其赋值顺序；图标层则是正序 Color=ColorId。
- **S17** skill §4 明确要求日志写必须吞异常，否则拖垮游戏。
- **S18** 症状为每次保存弹「保存出错！」，反推 SaveContext 按类型查表而非按字段跳过。
- **S19** skill §6.1 抄录的游戏内实际日志原文。
- **S19b** skill §6.1 症状日志块中与上一条同时出现的两行。
- **S26** skill §6.1 改法乙的风险说明。
- **S27** 读该 XML 第 64 行起的 ListPanel；其 ItemTemplate 用 IconOffsetButtonWidget。
- **S28** 反编译 MapNavigationVM 构造函数读其循环逻辑，参数来自 navigationHandler.GetElements()。
- **S29** 由 MapNavigationVM 的填充逻辑与 INavigationElement 接口成员推出。
- **S42** skill §8.1 第 2 步记录的重复进入路径与查重要求。
- **S44** skill §6 记录的排查结论。

---

# 字节布局（byte-layout）

> 共 20 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-114 | 官方 CNs XML 是 UTF-16＋BOM，按 utf-8 或按字节 grep 会静默 0 命中、误判没本地化。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| F-008 | .NET MemberRef 把被引用的类型名与方法名写进调用方程序集的字符串堆，可据此反查调用者 | 未标版本 |  | bannerlord-vanilla-mechanism-recon §4 第三路 |
| F-068 | .NET 托管 DLL 的字符串字面量存在 #US 堆里，编码是 UTF-16LE，纯 ASCII 扫描会漏掉绝大部分 | 未标版本 |  | bannerlord-mod-recon §4 第 3 条与 §2 第 3 步 |
| T01 | TPAC 容器头：偏移 0 为 'TPAC'，偏移 4 为 u32 版本=2，偏移 8 为 16B 包级 guid，偏移 28 为 u64 TOC 长度，条目从偏移 36 开始。 | v1.3.15 |  | bannerlord-tpac-extraction §1 容器格式（TPAC v2）表 |
| T02 | TPAC 头部偏移 24 的 u32 计数不总是条目数：gauntlet_ui.tpac 报 97，实扫只有 23 条。 | v1.3.15 | 是 | bannerlord-tpac-extraction §1 容器格式表（偏移 24 行） |
| T05 | TPAC 条目布局为 guid(16)+guid(16)+u32(aux)+u32(名字长)+名字[名字长]+u64(定义长)+定义[定义长]。 | v1.3.15 |  | bannerlord-tpac-extraction §1 条目布局 |
| T06 | 一个 tpac 的 TOC 里混着多个来源包的 guid，不能用固定偏移 d[36:52] 取条目 guid。 | v1.3.15 | 是 | bannerlord-tpac-extraction §1 条目布局（★ 坑） |
| T07 | 纹理定义体约 208~211 字节且不含像素：u32 类型(=3 表示纹理)、20B 保留、u32 源路径长+路径、8B hash、后接属性串。 | v1.3.15 |  | bannerlord-tpac-extraction §2 纹理定义 → 像素数据 |
| T08 | 定义体结束处紧跟 36 字节定位器：hash(8)+u32+u64 数据偏移+u64 解压后大小(expanded)+u64 存盘大小(stored)。 | v1.3.15 |  | bannerlord-tpac-extraction §2 定义结束处紧跟 36 字节定位器 |
| T09 | mesh 资产与贴图资产共用同一条 36B 定位器，只是载荷不同（贴图是 LZ4 压的 BC7，网格是明文顶点+索引）。 | v1.3.15 |  | bannerlord-tpac-extraction §5.1 底纹网格（★ mesh 与贴图共用定位器） |
| T20 | tpac 内纹理格式不一律是 BC7，定义体尾部给格式串：BC7 / DXT5(=BC3) / R8G8B8A8_UNORM。 | v1.3.15 |  | bannerlord-tpac-extraction §2（⚠ 格式不是一律 BC7） |
| T21 | BC7 与 DXT5 的 expanded == W*H，R8G8B8A8_UNORM 的 expanded == W*H*4。 | v1.3.15 |  | bannerlord-tpac-extraction §2 格式串表 |
| T52 | banner_key 每 10 位为一层。 | v1.3.15 |  | bannerlord-tpac-extraction §5 旗帜是怎么拼起来的 |
| T52b | banner_key 每层的字段序：MeshId、ColorId、ColorId2、Size.x、Size.y、Pos.x、Pos.y、DrawStroke、Mirror、Rotation(度)。 | v1.3.15 |  | bannerlord-tpac-extraction §5（每 10 位一层） |
| T55 | banner_icons.xml 的 texture_index 换算为 行=idx//4、列=idx%4。 | v1.3.15 |  | bannerlord-tpac-extraction §5（texture_index） |
| T58 | 图标贴图通道约定：绿通道=ColorId、蓝通道=ColorId2、alpha=形状；合成=绿×color1+蓝×color2，实心像素只有 (0,255,0) 与 (0,0,255)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5 ★ 图标贴图的通道约定 |
| T60 | mesh 载荷 = u32 顶点数 + N×(float32 x,y,z,1.0) + trailer + u32 三角形索引表。 | v1.3.15 |  | bannerlord-tpac-extraction §5.1 mesh 载荷布局 |
| T61 | mesh 顶点坐标范围 ±12 是一个单元方、按 banner_key 的 Size 缩放；顶点 0..3 是底块、4+ 是花纹且 z 抬高 ±0.02。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1（顶点 0..3 = 底块） |
| T62 | mesh 索引表带 0xFFFFFFFF 分隔符，因此一个载荷含两个子网格。 | v1.3.15 |  | bannerlord-tpac-extraction §5.1 索引表的稳找法 |
| S08 | .NET 用户字符串在 DLL 里以 UTF-16LE 存放。 | 未标版本 |  | bannerlord-mod-skeleton §2 坑 1（验证） |

## 证据

- **bgda-114** 2026-09-18 实测读 ModuleData/Languages/CNs/*.xml 得到 \x00 交错乱码。
- **F-008** 用 grep -l -a 在出货 DLL 二进制里直接搜类型名，命中的就是调用方程序集
- **F-068** 09-16 对同一份 DLL 先扫 ASCII 拿不到类名，改扫 UTF-16LE 才出 OnnxEmbedding／OnnxGate／BertNormalizer
- **T01** 在 v1.3.15 出货客户端的多个 .tpac 上按偏移直接读头实测，格式表见该 skill 的容器格式一节。
- **T02** 对现行 gauntlet_ui.tpac 实扫条目与头部字段逐条比对，头部计数与实扫数不符。
- **T05** 按该布局逐条解包 TOC 成功定位条目与定义体，见 §1 条目布局。
- **T06** 第一版按 d[36:52] 当条目 guid，结果漏掉 90% 条目；改用名字串反查才列全。
- **T07** 对 gauntlet_ui.tpac 条目定义体逐字段解出，长度落在 208~211 字节区间。
- **T08** 定义体起点 blob+bsize 之后按 <QQQ 读偏移 12 起的三个 u64，实测可解出像素数据。
- **T09** 对 banner_background_test_N 的 mesh 条目套用同一 36B 定位器成功取到载荷。
- **T20** 第一版按 BC7 写死解 UI 包三条全炸，改读定义体尾部格式串后全通。
- **T21** 按各条目 expanded 与 sheet 尺寸 W,H 逐个比对，两种关系分别成立。
- **T52** 对照 TaleWorlds.Core/Banner.cs（编码）与 BannerData.cs 的字段定义解出该编码。
- **T52b** 按该字段序解 banner_key 能还原出各家旗帜的网格、配色与摆位。
- **T55** 按该换算把 Icon 的 texture_index 映射回 4×4 图集格位并验证命中。
- **T58** 解出图标图集后统计实心像素颜色，只有两种纯色通道值。
- **T60** 对底纹 mesh 条目解包后按该布局成功还原顶点与三角形。
- **T61** 解出 8 个被用到的底纹后观察坐标范围与顶点分组。
- **T62** 在索引区读到 0xFFFFFFFF 分隔符，分开后可解释为底块与花纹两个子网格。
- **S08** 编译后在 DLL 里按 UTF-16LE 查找中文串可以命中。

---

# 数据库结构（db-schema）

> 共 13 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-008 | settlementType 取值为 town/village/castle/hideout/custom。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-021 | localization_entries 主键是 (language,stringId,filePath)，没有 key 列。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-022 | bannerlord_items 没有 id 列，主键是 entityId。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-024 | bannerlord_heroes.text 是人物背景描述，不是名字。 | v1.3.15 |  | bannerlord-game-data-access#英雄名中文查法 |
| bgda-066 | xml_documents_fts 用 tokenize=unicode61、prefix='2 3 4'，不在 camelCase 处切词，WidthSizePolicy 是整 token。 | v1.3.15 |  | bannerlord-game-data-access#FTS 切词 |
| bgda-085 | 游戏 settlementId 的小写 v 在 localization stringId 里是大写 V：castle_V6、castle_village_V6_2、town_V7、town_S1。 | v1.3.15 |  | bannerlord-game-data-access#陷阱 |
| bgda-089 | 聚落 name 字段是本地化 token，必须去 localization_entries 解。 | v1.3.15 |  | bannerlord-game-data-access#陷阱 |
| bgda-090 | 同一 stringId 会在多语言/多文件出现，CNs 与 CNt 都含简体/繁体，查询要带 language=CNs。 | v1.3.15 |  | bannerlord-game-data-access#陷阱 |
| bgda-091 | 官方文本有两处存放地：localization_entries 与 bannerlord_settlements.descriptionText。 | v1.3.15 |  | bannerlord-game-data-access#陷阱 |
| S20 | SaveableBasicTypeDefiner 的 saveBaseId=30000，装基础类型与 List<int>、Dictionary<string,int>。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 两张主表 |
| S21 | SaveableCampaignTypeDefiner 的 saveBaseId=2000，注册战役类型与 Dictionary<Settlement,int>、List<Town>。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 两张主表 |
| S23 | Dictionary<Settlement,int> 已在战役定义表注册，可作 Dictionary<Town,int> 的替代组合。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 改法甲 |
| S24 | CraftingCampaignBehaviorTypeDefiner 继承 SaveableTypeDefiner(150000)，用 AddClassDefinition 注册。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 改法乙 |

## 证据

- **bgda-008** 对 bannerlord_settlements.settlementType 去重统计。
- **bgda-021** 读建表定义与实测查询。
- **bgda-022** 读该表列清单。
- **bgda-024** 实测内容形如 {=WBl5hS0e}The northern third...。
- **bgda-066** 读 FTS 建表参数并实测查询。
- **bgda-085** LIKE 反查确认拼写后实测。
- **bgda-089** 实测值形如 {=Settlements.Settlement.name.castle_EN1}。
- **bgda-090** 实测同 stringId 多行命中。
- **bgda-091** 2026-09-12 判萨拉匹欧斯翻车后确立。
- **S20** ilspycmd -t TaleWorlds.SaveSystem.SaveableBasicTypeDefiner 反编译读出定义；另有 int[]、Dictionary<int,int>。
- **S21** ilspycmd 反编译 TaleWorlds.CampaignSystem.dll 读出定义；另有 Hero/Settlement/MobileParty、Dictionary<Hero,int>。
- **S23** 在 SaveableCampaignTypeDefiner 的定义里查到该组合，且它被判为 OK 的阳性对照。
- **S24** skill §6.1 改法乙引用的原版先例，另有 ConstructContainerDefinition 注册容器。

---

# 实测数值（measured-number）

> 共 103 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-001 | BannerlordSage 主库 bannerlord.db 约 528MB、43 张表。 | v1.3.15 |  | bannerlord-game-data-access#位置与状态 |
| bgda-002 | 同目录另有 csharp-index.json 约 43MB、mod-source.db、assets/{Source,Xmls}/、setup-state.json。 | v1.3.15 |  | bannerlord-game-data-access#位置与状态 |
| bgda-003 | 反编译源码实体共 10 个程序集、3030 份 .cs。 | v1.3.15 |  | bannerlord-game-data-access#位置与状态 |
| bgda-004 | XML 全文收在 xml_documents_fts，覆盖 436 个 Prefab 与 72 个 Brush。 | v1.3.15 |  | bannerlord-game-data-access#位置与状态 |
| bgda-006 | 索引入库 XML 3784 个文件，解析失败 0。 | v1.3.15 |  | bannerlord-game-data-access#位置与状态 |
| bgda-007 | bannerlord_settlements 表 494 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-009 | bannerlord_cultures 表 23 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-010 | bannerlord_kingdoms 表 8 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-011 | bannerlord_clans 表 95 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-012 | bannerlord_heroes 表 397 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-013 | bannerlord_troops 表 1696 行，含 occupation 与 upgradeTargetsJson 列。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-014 | bannerlord_items 表 3052 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-015 | bannerlord_perks 表 374 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-016 | bannerlord_policies 表 32 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-017 | bannerlord_skills 表 7 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-018 | localization_entries 表 272223 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-019 | xml_entities 表 300565 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-020 | csharp_types 4656 行、csharp_methods 40337 行。 | v1.3.15 |  | bannerlord-game-data-access#表与字段 |
| bgda-029 | lord_1_1（Lucon）官方中文名＝卢孔，属北帝国俄斯提科斯家族。 | v1.3.15 |  | bannerlord-game-data-access#八位统治者的官方译名 |
| bgda-030 | lord_1_7（Garios）官方中文名＝加里俄斯，属西帝国科穆诺斯家族。 | v1.3.15 |  | bannerlord-game-data-access#八位统治者的官方译名 |
| bgda-031 | lord_1_14（Rhagaea）官方中文名＝拉盖娅，属南帝国珀特洛斯家族。 | v1.3.15 |  | bannerlord-game-data-access#八位统治者的官方译名 |
| bgda-032 | lord_2_1（Raganvad）官方中文名＝朗瓦德，属斯特吉亚贡达罗夫家族。 | v1.3.15 |  | bannerlord-game-data-access#八位统治者的官方译名 |
| bgda-033 | lord_3_1（Unqid）官方中文名＝温吉德，属阿塞莱巴努·胡勒延家族。 | v1.3.15 |  | bannerlord-game-data-access#八位统治者的官方译名 |
| bgda-034 | lord_4_1（Derthert）官方中文名＝德泰尔，属瓦兰迪亚戴·梅罗克家族。 | v1.3.15 |  | bannerlord-game-data-access#八位统治者的官方译名 |
| bgda-035 | lord_5_1（Caladog）官方中文名＝卡拉多格，属巴旦尼亚芬·格鲁芬多克家族。 | v1.3.15 |  | bannerlord-game-data-access#八位统治者的官方译名 |
| bgda-036 | lord_6_1（Monchug）官方中文名＝蒙楚格，属库赛特兀儿浑乃特家族。 | v1.3.15 |  | bannerlord-game-data-access#八位统治者的官方译名 |
| bgda-037 | 八位统治者登记锚点全为 exact_base/installed，display_name_zh 与官方本地化逐字一致。 | v1.3.15 |  | bannerlord-game-data-access#八位统治者的官方译名 |
| bgda-051 | DefaultCulturalFeats.cs:88-134 原版实有 18 条文化专长，六大文化各 3 条。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-053 | 原版文化专长实例：巴旦尼亚林地减速 −50%、帝国驻军工资 −20%、阿塞莱商队建造 −30%。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-054 | HasFeat( 被 17 个 GameComponents/Default*Model.cs 消费。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-059 | Campaign.Current.Models 共 129 个模型接口。 | v1.3.15 |  | bannerlord-game-data-access#机械穷举四件套 |
| bgda-060 | CampaignBehaviors/*.cs 约 143 个。 | v1.3.15 |  | bannerlord-game-data-access#机械穷举四件套 |
| bgda-064 | Bankrupt 的 7 处命中全是工坊破产。 | v1.3.15 |  | bannerlord-game-data-access#机械穷举四件套 |
| bgda-067 | FTS命中：WidthSizePolicy 436、StretchToParent 418、SuggestedWidth 408、CoverChildren 380、LayoutMethod 301。 | v1.3.15 |  | bannerlord-game-data-access#FTS 切词 |
| bgda-081 | 把 BaseSpeed 4f 读成『每天』曾让一套距离→送达时长公式慢了 2.4 倍。 | v1.3.15 |  | bannerlord-game-data-access#MCP 直连 |
| bgda-093 | 53 座城镇全部有描述文，中文 93–221 字，内容是建城史/地名由来/地缘。 | v1.3.15 |  | bannerlord-game-data-access#陷阱 |
| bgda-099 | settlements.xml 有 120 个 gate_posX/gate_posY 城门坐标，正则会与 494 个聚落中心坐标混淆。 | v1.3.15 |  | bannerlord-game-data-access#解析 settlements.xml 坐标 |
| bgda-101 | 494 个聚落中心坐标跨度 X 706 / Y 548 地图单位，与运行时 CampaignVec2 同系。 | v1.3.15 |  | bannerlord-game-data-access#解析 settlements.xml 坐标 |
| bgda-102 | 原版部队基准 4 单位/小时，横穿大陆约 7 天。 | v1.3.15 |  | bannerlord-game-data-access#解析 settlements.xml 坐标 |
| bgda-105 | 22 种村庄类型里 21 种基础产出是 Grain 3，只有 wheat_farm 是 Grain 50。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-106 | 全图 273 个村每村都产谷物；只看 AddProductions 会漏掉 250 个村。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-109 | 全原版只有 9 件食物：谷物/鱼/葡萄/黄油/肉/橄榄/奶酪/枣/啤酒。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-110 | wine 与 oil 明写 IsFood=false，只给士气不进食物消耗；beer 是唯一两样都占的。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-111 | settlements.xml 里有被 XML 注释包住的村庄（例 village_comp_V4_1），数村前必须剥注释。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-113 | Vanilla 实测：村庄 273 · 城镇 53 · 城堡 67 · 文化 6。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-115 | 全机 CNs 有 300+ 份（第三方模组大量覆盖），取名字必须原版模块优先并打出处文件。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-118 | 只查代码不查地图产量必错：按基值直加全图谷物偏低 11%（3122 → 2777）。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-125 | 全图村庄谷物 2777/日 对 53 城胃口约 1600/日，最好的一座城也只覆盖 35%。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-129 | 城的金库水位 = 10,000 + 繁荣×12，即这座城的收粮能力。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-135 | 实测 141 座城属村里有 7 座（5%）离别的城更近，引擎仍不动它们，0 座超出上限。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-139 | 队伍速度常数是 3.43。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-143 | 实测 132/273 座村附属是堡（48%）。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-144 | 实测 49/53 座城的腹地 ≠ 领地（92%）。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-145 | 城镇侧村庄谷物产能按附属 1625/日，按市场归属 2777/日（城堡不承接村庄产出）。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-147 | 本机 105 个模组里只启用了 12 个。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-148 | 本机只有 ROT 命中（自带 ROTTradeBoundBehavior），但 ROT 全是 IsSelected=false；启用的 12 个没一个动这段。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-150 | BoundVillages 全库约 45 处读者。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-152 | Town.TradeBoundVillages 只有界面一个读者（str_trade_bound_village，中文 hash q7xpz1xb）。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-163 | troops.occupation 是游戏原生职业枚举，AWAKE 新增 profile 只能从它提取。 | v1.3.15 |  | bannerlord-game-data-access#在本项目里的用途 |
| F-002 | 整个 VM 层在 TaleWorlds.CampaignSystem.ViewModelCollection.dll，体积约 1.5 MB | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §2 |
| F-005 | 1.5 MB 的 DLL 用 ilspycmd -p 出 3.4 MB 可 grep 源码树，耗时约 10 秒 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §2 |
| F-018 | sage 索引只收 bin\Win64_Shipping_Client 下的 15 个 DLL，Modules\Native\bin\... 里的程序集不在索引内 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §6 工具坑表末行 |
| F-031 | 全部广播事件集中在 TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs，约 2800 行 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 1 步 |
| F-033 | 一次 grep 'public static IMbEvent' 就能拿到 200 个以上的广播事件全清单 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 1 步 |
| F-038 | DefaultPerks.Trade.InsurancePlans（贸易 7 级）= 商队被摧毁返还 5,000，实现在 DestroyPartyAction.cs:19-22 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 4 步实证 |
| F-044 | 约 40 个 *IssueBehavior 各自发一遍任务奖励，给钱语句分散在叶子类里 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.7 第 1 条实证 |
| F-045 | IssueBase.cs 整份文件只有 2 条给钱语句，分别在第 767 行与第 876 行 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.7 第 1 条实证 |
| F-047 | 同一个量曾被报成 88 处，实际只有 69 处，其中还混进 18 处相反方向的调用 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.7 第 2 条实证 |
| F-067 | AnimusForge 用 105:93 胸像，AWAKE 用 212×360 全身像，两边目标比例不同，构图词不能互抄 | 未标版本 |  | bannerlord-mod-recon §3 |
| F-075 | FreezeWatchdog_Timeline.txt 里 onnx=True 出现 443 次，是门控生效的硬证据 | 未标版本 |  | bannerlord-mod-recon §4 第 7 条 |
| F-076 | 官方 XML 里 Extend 类机制的关键词实为 <NineRegionSprite>，共出现 194 次 | v1.3.15 |  | bannerlord-mod-recon §5 别做的事 |
| T03 | core.tpac 体积 217MB，但 TOC 只有 611157 字节，TOC 区与数据区分离。 | v1.3.15 | 是 | bannerlord-tpac-extraction §1 TOC 与数据区是分开的 |
| T04 | core_game.tpac 体积 3GB，但 TOC 只有 5.5MB。 | v1.3.15 | 是 | bannerlord-tpac-extraction §1 TOC 与数据区是分开的 |
| T10 | 现行 gauntlet_ui.tpac 实测：ui_textures_1/_2/_3/_6/_7 的载荷已压缩，_4/_5/_8 未压缩。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 2、§2（现行 gauntlet_ui.tpac 实测） |
| T13 | 把 BGRA 当 RGBA 解会把红蓝对调：纸贴图修正前均值 (234,237,241)，修正后 (241,237,234)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 3 |
| T14 | stone_texture_continuous 是中性灰 (144,144,144)，通道对调在灰度图上完全看不出来。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 3、§4 表 |
| T15 | warm_overlay 实测均值 (120,115,101)，R>G>B，可作通道序正确的旁证。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 3（旁证手法） |
| T16 | 图集 sheet 尺寸随版本变且 sheet 序号会挪位：ui_textures_1 由 build31530 的 887×890 变为 build110062 的 888×888。 | v1.3.15 (build 110062) / 对照 build 31530 | 是 | bannerlord-tpac-extraction §0.1 坑 1 对照表 |
| T17 | ui_textures_2（paper_texture_tile）尺寸由 build31530 的 1185×396 变为 build110062 的 1184×396。 | v1.3.15 (build 110062) / 对照 build 31530 | 是 | bannerlord-tpac-extraction §0.1 坑 1 对照表 |
| T18 | ui_textures_3（popup_canvas_texture）尺寸由 build31530 的 492×602 变为 build110062 的 492×600。 | v1.3.15 (build 110062) / 对照 build 31530 | 是 | bannerlord-tpac-extraction §0.1 坑 1 对照表 |
| T19 | sheet 7 由旧版 warm_overlay 30×30 变为现行 gradient_texture 100×100；旧版无 sheet 8，现行为 warm_overlay 32×32。 | v1.3.15 (build 110062) / 对照 build 31530 | 是 | bannerlord-tpac-extraction §0.1 坑 1 对照表 |
| T22 | expanded 可能含 mip 链而大于 W*H：ui_textures_4 实测 expanded=349552，而 512×512=262144。 | v1.3.15 | 是 | bannerlord-tpac-extraction §2（⚠ expanded 可能含 mip 链） |
| T25 | 图标图集正确解码的统计特征是 60%~87% 的 alpha=0；实测 82% 透明 + 15% 实心。 | v1.3.15 | 是 | bannerlord-tpac-extraction §2 判据是统计特征 |
| T28 | custom_banner_icons 图集共 16 张 2048²，每张 4×4=16 格、每格 512²。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 已验证的落点表 |
| T30 | Native/AssetPackages/Banner.tpac 只有 8 个 mesh 条目。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 已验证的落点表 |
| T31 | gauntlet_ui.tpac 现行 324MB、TOC 仅 40542 字节；旧版 253MB；SandBox 那份 66MB。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 已验证的落点表 |
| T33 | 现行 gauntlet_ui.tpac 实测 23 条，条目形如 ui_textures_5、ui_loading_3、ui_fonts_1。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ★ UI 图集的命名与落点 |
| T35 | ui_textures_1 = game_over_mask，888×888，BC7，纯白蒙版。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T36 | ui_textures_2 = paper_texture_tile，1184×396，BC7，实测均值 (241,237,234) 近白暖白。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T37 | ui_textures_3 = popup_canvas_texture，492×600，BC7，实测均值 (46,46,46) 暗。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T38 | ui_textures_4 = stone_texture_continuous，512×512，DXT5 未压缩，实测均值 (144,144,144)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T39 | ui_textures_5 = stone_texture_overlay，512×512，BC7 未压缩，实测均值 (111,111,111)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T40 | ui_textures_6 = slider_progress_small，160×80，DXT5，解出为白色。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T41 | ui_textures_7 = gradient_texture，100×100，DXT5，实测均值 (19,15,9) 近黑。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T42 | ui_textures_8 = warm_overlay，32×32，DXT5 未压缩，实测均值 (120,115,101)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T43 | 新旧 build 重抠逐像素复核：stone 两条 MAE=0.00，纸 MAE 1.06 且 94% 像素差 ≤4。 | v1.3.15 (build 110062) 对照 build 31530 | 是 | bannerlord-tpac-extraction §4 复核结论（09-15） |
| T48 | 主控栏条目 ui_mapbar_1 位于 Modules/SandBox/AssetPackages/gauntlet_ui.tpac，尺寸 4096×128。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 步骤 3 |
| T53 | 旗帜画布 1528×1528、中心 (764,764)；8 家王国 key 的 Pos=764 恒等、Icon Size≈512 占 33.5%。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5 画布 1528×1528 |
| T56 | banner_icons.xml 的 <Background> 共 36 个，即 banner_background_test_1..36。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5（<Background id= mesh_name=> 共 36 个） |
| T57 | banner_icons.xml 尾部 <BannerColors> 共 229 个颜色。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5 配色 |
| T59 | 图标描边（蓝通道）吃 ColorId2：Lake Rats 的斧子用 c2=B57A1E 的橘铜色描边。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5 实测印证 |
| T64b | ChangePrimaryColor() 把两色设成同一个，这是 93 面平底旗 c1==c2 的原因。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 ★ 底纹双色 |
| T66 | 随机生成图标时 ColorId2 取 ReadOnlyColorPalette.Last().Key，即 116 近黑。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 旁证 |

## 证据

- **bgda-001** 读 C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db 的文件大小与表清单。
- **bgda-002** 实测 dist/games/bannerlord/ 下的辅助文件大小。
- **bgda-003** 数 assets/Source/bin/Win64_Shipping_Client/ 下 TaleWorlds.* 十个程序集目录的 .cs 文件，合计 3030 份。
- **bgda-004** 查 xml_documents_fts（fts5，字段 filePath/moduleName/content）；正文落在 xml_documents_fts_content 的 c2。
- **bgda-006** 本机实测 xml_files / xml_documents_fts 计数。
- **bgda-007** 直查该表行数；字段 settlementId/name/culture/settlementType/owner/boundSettlement/filePath。
- **bgda-009** 直查该表行数；字段 cultureId/name/isMainCulture/basicTroop/defaultPolicyIdsJson。
- **bgda-010** 直查该表行数；字段 kingdomId/name/culture/rulerTitle/initialHomeSettlement/policyIdsJson。
- **bgda-011** 直查该表行数；字段 clanId/name/culture/superFaction/tier/isNoble/isMinorFaction/isMercenary。
- **bgda-012** 直查该表行数；字段 heroId/faction/clan/spouse/father/mother/alive/isTemplate/text。
- **bgda-013** 直查该表行数与列；另有 characterId/culture/level。
- **bgda-014** 直查该表行数。
- **bgda-015** 直查该表行数。
- **bgda-016** 直查该表行数。
- **bgda-017** 直查该表行数。
- **bgda-018** 直查该表行数；字段 language/stringId/text/filePath。
- **bgda-019** 直查该表行数；字段 entityType/entityId/name/filePath。
- **bgda-020** 直查两表行数；两表都只到签名级。
- **bgda-029** 查 localization_entries CNs；登记锚点 entity.hero.lord_1_1。
- **bgda-030** 查 localization_entries CNs；登记锚点 entity.hero.lord_1_7。
- **bgda-031** 查 localization_entries CNs；登记锚点 entity.hero.lord_1_14。
- **bgda-032** 查 localization_entries CNs；登记锚点 entity.hero.lord_2_1。
- **bgda-033** 查 localization_entries CNs；登记锚点 entity.hero.lord_3_1。
- **bgda-034** 查 localization_entries CNs；登记锚点 entity.hero.lord_4_1。
- **bgda-035** 查 localization_entries CNs；登记锚点 entity.hero.lord_5_1。
- **bgda-036** 查 localization_entries CNs；登记锚点 entity.hero.lord_6_1。
- **bgda-037** 交叉验证 persona-entity 登记表与 localization_entries CNs。
- **bgda-051** 读该文件逐条计数。
- **bgda-053** 读 DefaultCulturalFeats.cs:88-134 的数值。
- **bgda-054** grep HasFeat( 后计数命中文件。
- **bgda-059** 扫 ComponentInterfaces/*.cs 的 public abstract 成员并计数。
- **bgda-060** 列目录文件计数。
- **bgda-064** 逐条读命中处上下文。
- **bgda-067** 对 xml_documents_fts 逐个属性名做前缀查询计数。
- **bgda-081** 2026-09-13 纠错记录。
- **bgda-093** 实测统计全部城镇的 descriptionText 与 CNs 长度。
- **bgda-099** 实测：不加 (?<!gate_) 负向断言会把城门坐标混入，曾据此报出 615 个样本的错误计数。
- **bgda-101** 对 494 个 posX/posY 求 min/max 差。
- **bgda-102** 由 4f/小时基准与 706/548 跨度推算。
- **bgda-105** 读 DefaultVillageTypes.cs 的 Initialize 参数表逐条统计。
- **bgda-106** 由基础产出 Grain 3 覆盖 21/22 类型推出。
- **bgda-109** 两条 IsFood 设置路径合起来枚举。
- **bgda-110** 读 XML 属性与队伍士气/食物消耗两条消费链。
- **bgda-111** 实测未剥注释会把村数与绑城一起数错。
- **bgda-113** 剥注释后对 settlements.xml 计数。
- **bgda-115** 全机扫描 Languages/CNs 目录计数。
- **bgda-118** 带炉灶倍率重算后的全图谷物日产量对比。
- **bgda-125** 带炉灶倍率的逐城产能 vs 胃口配平脚本结果。
- **bgda-129** 读城金库相关实现并实测数值。
- **bgda-135** 用坐标与距离上限脚本对城属村重算比对。
- **bgda-139** 读默认队伍速度相关模型取值。
- **bgda-143** 对 settlements.xml 的 bound 逐村分类统计。
- **bgda-144** 按 TradeBound 汇总的城腹地与按 Bound 的领地对照。
- **bgda-145** 两种归属口径分别汇总后的对比。
- **bgda-147** 统计 Configs/LauncherData.xml 的 IsSelected。
- **bgda-148** 扫 DLL + 查 LauncherData.xml 后结论：跑的是原版逻辑。
- **bgda-150** 全库 grep BoundVillages 计数。
- **bgda-152** 全库 grep 后确认唯一读者，且仅 extended 且是城时显示。
- **bgda-163** 对 bannerlord_troops.occupation 去重：Lord/Soldier/Townsfolk/Villager/Merchant/Headman/Tavernkeeper/RansomBroker/Artisan/Preacher 等。
- **F-002** 列出 bin\Win64_Shipping_Client 下各 DLL 体积并反编译该文件
- **F-005** 对 CampaignSystem.ViewModelCollection.dll 实跑 ilspycmd -p，统计产物大小与耗时
- **F-018** 对比 sage 索引覆盖范围与 Modules\Native\bin\Win64_Shipping_Client 的实际 DLL 清单
- **F-031** 对反编译工程树里的 CampaignEvents.cs 统计行数
- **F-033** 对 CampaignEvents.cs 跑该 grep 并计数
- **F-038** 读 DefaultPerks.cs 的 InsurancePlans 条目与 DestroyPartyAction.cs 第 19-22 行
- **F-044** rg 'SomeAction.Apply\|ChangeXxx(' 于反编译树，统计各 *IssueBehavior 内的发奖点
- **F-045** 读 IssueBase.cs 并定位全部给钱调用及其行号
- **F-047** 改用 rg -o --no-filename \| wc -l 重新计数并人工核对每处方向
- **F-067** 读其 Prefab 尺寸与 AWAKE 侧尺寸实测（§3 第 2 个提问角度）
- **F-075** 在该文件内计数 onnx=True（09-16 实测）
- **F-076** 在官方 Prefabs 里搜 Extend 零命中后，改搜 NineRegionSprite 并计数得 194
- **T03** 读 Native/EmAssetPackages/core/core.tpac 文件头实测体积与偏移 28 的 TOC 长度。
- **T04** 读 Native/AssetPackages/core_game.tpac 文件头实测体积与 TOC 长度。
- **T10** 逐个条目比较 stored 与 expanded 字段得出压缩与否的清单。
- **T13** 对 paper_texture_tile 解码结果按 RGBA 与换通道两种口径各算一次均值，恰为 R/B 互换。
- **T14** 解 ui_textures_4 后统计均值恒为 (144,144,144)，故该通道错误长期未被发现。
- **T15** 挑名字自带颜色方向的条目 warm_overlay 解码后算均值，得到暖色且 R>G>B。
- **T16** 对两个 build 的 NativeSpriteData.xml 与解出的图集尺寸做对照表。
- **T17** 同上版本对照表，宽差 1px 即会让整张图行错位。
- **T18** 同上版本对照表。
- **T19** 两版 NativeSpriteData.xml 的 sheet 序号与名字逐格对照。
- **T22** 读 ui_textures_4 的 36B 定位器 expanded 字段，与 sheet 尺寸相乘结果比较。
- **T25** 对解出的图标图集统计 alpha 分布得出透明/实心占比。
- **T28** 解 Native/EmAssetPackages/core/core.tpac 的 custom_banner_icons 系列条目后统计尺寸与格数。
- **T30** 对该包列条目名后统计，只有 8 条且都是 mesh。
- **T31** 分别读 Native 与 SandBox 两处 gauntlet_ui.tpac 的文件体积与 TOC 长度。
- **T33** 对该包逐条列名并统计条目数，另有 ui_encyclopedia_1、ui_barter_1、ui_options_1、ui_mploading_*。
- **T35** 反查条目解出图并统计，尺寸与格式取自现行 NativeSpriteData.xml 与定义体格式串。
- **T36** 解出后算全图均值，作为 UI 美术的纸面基准。
- **T37** 解出后算全图均值。
- **T38** 该条目 stored==expanded 未压缩，按 decode_bc3 解出后算均值。
- **T39** 该条目未压缩，按 BC7 解出后算均值。
- **T40** 解出后观察为纯白滑块底图。
- **T41** 解出后算均值；旧版该行是 warm_overlay 30×30。
- **T42** 解出后算均值；旧版没有这一格。
- **T43** 对 build 31530 与 build 110062 各解一遍同名条目后逐像素比较。
- **T48** 按 CategoryName=ui_mapbar + SheetID=1 拼名反查该包并读出 sheet 尺寸。
- **T53** 反推 8 家王国 spkingdoms.xml 的 banner_key 数值分布。
- **T56** 统计 banner_icons.xml 中 Background 节点数。
- **T57** 统计 banner_icons.xml 尾部 BannerColors 段内 Color 节点数。
- **T59** 按通道约定复算 Lake Rats 图标描边色，与 banner_key 里的 ColorId2 吻合。
- **T64b** 反编译该方法并与旗面数据中 93 面平底旗 c1==c2 的现象对照。
- **T66** 反编译随机图标生成路径读到该取值，并核对调色板末项为 116。

---

# 静默失败（silent-failure）

> 共 24 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-027 | 用 language=EN 反查会静默返回 0 行，不报错。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-041 | 按 text=? 0 行判非官方不可靠：Sarapios 曾被误判非官方，实为官方聚落描述文中人物。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-065 | rg -c 管道接 awk -F: 统计词频，在 Windows 绝对路径下计数全变 0（盘符冒号被当分隔符）。 | 未标版本 | 是 | bannerlord-game-data-access#机械穷举四件套 |
| bgda-069 | FTS 查属性名必须写全名，写子串会静默给 0 或 1 且不报错。 | v1.3.15 | 是 | bannerlord-game-data-access#FTS 切词 |
| bgda-084 | stringId 大小写敏感，且查不到时静默返回 0 行。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-092 | descriptionText 格式为 {=hash}English text，不进 localization_entries；按 .text.* 查反而 0 行。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-095 | 直调 rg.exe 不加 --color never 时，ANSI 高亮会把命中词本身替换成一个 n。 | 未标版本 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-154 | 本地化两段式：str_* → {=hash}English，中文按 hash 存在 std_module_strings_xml-zho-CN.xml，拿 str_* 直接搜 CNs 必零命中。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| F-006 | ilspycmd 是原生 exe，不认 MSYS 的 /d/ 路径；传错时退出码 0 但完全不产出任何文件 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §2 / §6 工具坑表 |
| F-010 | 旧 harness 里 bash for 循环扫全游戏目录的 grep 会在几分钟后被 SIGTERM 杀掉，且截断输出看起来像扫完了 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §4 / §6 |
| F-013 | 旧 harness 里 find 与 sort 命中的是 C:\Windows\system32 的同名 exe：find 报参数格式不正确，sort -u 报找不到指定的文件 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §6 工具坑表 |
| F-014 | 旧 harness 里 grep -E 的 {0,24} 量词被 shell 吃掉，报 [A-Za-z0-9_]0MapBar... No such file or directory | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §6 |
| F-016 | rg 不加 --color never 时命中词会被 ANSI 高亮码吃掉，显示成 n | 未标版本 | 是 | bannerlord-vanilla-mechanism-recon §6 |
| F-046 | rg -c 每个文件只吐一行，接 wc -l 得到的是命中文件数而不是出现次数 | 未标版本 |  | bannerlord-vanilla-mechanism-recon §7.7 第 2 条 |
| F-069 | 旧 harness 可用 tr -d '\000' < DLL \| grep -o -- '串' \| wc -l 提取 UTF-16LE 串；strings -a 会静默给 0 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-mod-recon §4 第 1 条 |
| F-070 | 旧 harness 里 grep -r 与 -n/-o 连用会被拼成 -rn 当命令执行，静默空输出且 exit 0 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-mod-recon §4 第 2 条 |
| F-071 | timeout 900 python xxx.py 命中的是 C:\Windows\system32\timeout.exe，报『错误: 无效语法。默认选项不允许超过 '1' 次。』 | Windows 未标版本 | 是 | bannerlord-mod-recon §4 第 4 条 |
| F-072 | 报错里出现『键入 "XXX /?" 以了解用法』这类中文 Windows 措辞时，是撞了同名 exe，不是脚本本身出错 | Windows 未标版本 |  | bannerlord-mod-recon §4 第 4 条 |
| F-073 | 把 /d/... 形式的路径喂给原生 Python 时，os.walk 会扫到 0 个文件却像正常结果 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-mod-recon §4 第 5 条 |
| S07 | UTF-8 无 BOM 的 .cs 中文串被 Roslyn 按系统 ANSI 码页（中文 Windows=GBK）读，编译不报错但游戏里是乱码。 | 未标版本 |  | bannerlord-mod-skeleton §2 坑 1 |
| S10 | 模组必须同时存在于 Modules\<ModId>\SubModule.xml 与 Modules\<ModId>\bin\Win64_Shipping_Client\<ModId>.dll 两处。 | v1.3.15 |  | bannerlord-mod-skeleton §2 坑 2 |
| S11 | 加了自己的界面资源后落点变成三处，多出 Modules\<ModId>\GUI\Prefabs\<面板名>.xml。 | v1.3.15 |  | bannerlord-mod-skeleton §8.2 |
| S12 | csproj 的 AssemblyName、SubModule.xml 的 <DLLName>、实际 DLL 文件名三者必须一致，不一致则静默失效。 | v1.3.15 |  | bannerlord-mod-skeleton §3（★ 三个名字必须一致） |
| S34 | 预制件里 IconID="@ItemId" 使按钮图标的查表键就是 StringId；自造 id 在原版 MapBar.Left.Icons 里没有帧，结果是按钮有底板、能点、有提示但图标为空。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3（★ 预制件里 IconID） |

## 证据

- **bgda-027** 实测 language='EN' AND text='Lucon' 返回 0 行。
- **bgda-041** 2026-09-12 实测翻车记录：官方文本还有 bannerlord_settlements.descriptionText 一处存放地。
- **bgda-065** 实测 C:/Users/... 路径导致字段错位；改用 awk -F: '{s+=$NF}' 取最后一段。
- **bgda-069** SizePolicy / LayoutMethod 这类子串直觉查询的实测结果。
- **bgda-084** 实测 castle_v6 返回 0 行，实为大小写写错。
- **bgda-092** 实测聚落描述文只在该列，取中文需剥 hash 再查 CNs。
- **bgda-095** 2026-09-18 实测：OwnedAlleys 显示成 n、OnAlleyOwnerChanged 显示成 nOwnerChanged，极易误判为混淆。
- **bgda-154** 读 Native/ModuleData/module_strings.xml 与 Languages/CNs/std_module_strings_xml-zho-CN.xml。
- **F-006** 在 Git Bash 下用 /d/ 路径实跑 ilspycmd，命令 exit 0 且输出目录为空
- **F-010** 实跑循环扫全目录，进程被 SIGTERM，输出不完整但形态与正常结束相同
- **F-013** 在 Git Bash 里跑 find . -name 与 sort -u，报出中文 Windows 错误文本
- **F-014** 实跑带花括号的 -E 正则，报错文本就是被曲解后的参数片段
- **F-016** 实跑 rg，输出里命中词显示为 n，加 --color never 后恢复正常文本
- **F-046** 对同一模式分别跑 rg -o --no-filename \| wc -l 与 rg -c \| wc -l 对比结果
- **F-069** 在 AnimusForge DLL 上跑该命令与 strings -a 对比，后者恒为 0
- **F-070** 实跑 grep -rn 形式，无任何输出且退出码为 0
- **F-071** 09-19 实跑带 timeout 前缀的命令，报错文本来自 Windows timeout.exe 而非脚本
- **F-072** 由 09-19 timeout 事故的报错文本归纳出的通用判据
- **F-073** 实跑 os.walk 于 /d/ 路径，返回 0 个文件
- **S07** 用无 BOM 的 .cs 写中文日志串，编译 0 错误但进游戏打出来乱码；加 CodePage 65001 后正常。
- **S10** 少一处游戏不报错，表现为模组列表里能看到但什么也没发生。
- **S11** 缺该文件时按钮点开是空层且不报错，见 §8.2。
- **S12** skill 明确记录三者不一致时游戏不报错、模组不加载。
- **S34** skill §8.3 记录该现象且不报错。

---

# API 怪癖（api-quirk）

> 共 45 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-028 | 名字解中文要两步：先按原文精确匹配 text=? 拿 stringId，再查该 stringId 的 CNs。 | v1.3.15 |  | bannerlord-game-data-access#英雄名中文查法 |
| bgda-040 | 判定专名是否官方设定：按原文 text=? 精确匹配，命中即官方。 | v1.3.15 |  | bannerlord-game-data-access#三个高频套路 |
| bgda-055 | 换掉 Campaign 模型的官方口子是 CampaignGameStarter.AddModel<T>(MBGameModel<T>)。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-056 | ExplainedNumber.AddFactor(float value, TextObject description) 在 ExplainedNumber.cs:238。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-057 | ExplainedNumber.Add 在 :221，LimitMin 在 :250，LimitMax 在 :259。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-061 | 广播事件清单是 CampaignEvents.cs 里 public static IMbEvent 的成员名。 | v1.3.15 |  | bannerlord-game-data-access#机械穷举四件套 |
| bgda-063 | Interest 的命中只是 interesting/Interface 的子串，不是机制。 | v1.3.15 | 是 | bannerlord-game-data-access#机械穷举四件套 |
| bgda-068 | 查 SizePolicy 只 1 条命中，是别的文件里小写 sizePolicy 的误命中。 | v1.3.15 | 是 | bannerlord-game-data-access#FTS 切词 |
| bgda-070 | search_bannerlord_docs 把多词 AND 全连：五词查询 0 条，单词 GauntletUI 5 条。 | v1.3.15 |  | bannerlord-game-data-access#FTS 切词 |
| bgda-074 | BANNERSAGE_TOOLSET=full 会多出写盘工具 create_mod_workspace 与 generate_xslt_patch。 | 未标版本 |  | bannerlord-game-data-access#MCP 直连 |
| bgda-075 | MCP 参数名：read_gauntlet_ui 用 uiFileName、resolve_localization 用 text，写错报 -32602 invalid_type。 | 未标版本 |  | bannerlord-game-data-access#MCP 直连 |
| bgda-127 | town.MarketData.GetCategoryData(X) 的 InStore/InStoreValue/Supply/Demand 与 GetPrice(item) 全是 public。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-146 | 查归属逻辑是否被模组改要两步：扫 DLL 找覆盖者（必带阳性对照）＋ 查 Configs/LauncherData.xml 的 IsSelected。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-166 | WAL 模式下 SQLite mode=ro 可能报错，此时改用 immutable=1。 | 未标版本 |  | bannerlord-game-data-access#连接方式 |
| F-015 | rg 单行正则抓不到跨行 XML 属性，输出为空，必须加 -U 多行模式 | 未标版本 |  | bannerlord-vanilla-mechanism-recon §6 |
| F-032 | CampaignEvents.cs 一行一个事件，形态固定为 public static IMbEvent<...> Xxx => Instance._xxx; | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 1 步 |
| F-039 | ChangeOwnerOfSettlementDetail 含 BySiege / ByBarter / ByKingDecision 等取值，即原版的沦陷原因全集 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 5 步实证 |
| F-040 | KillCharacterActionDetail 含 Lost 取值，说明原版已有'消失无踪'这个词 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 5 步实证 |
| F-041 | Village.VillageStates 含 BeingRaided 与 Looted，被劫掠在原版分两层 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 5 步实证 |
| F-042 | MobileParty.cs 有自分类属性 IsCaravan/IsVillager/IsLordParty/IsGarrison/IsMilitia/IsBandit 等 8 个 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 6 步实证 |
| F-048 | GiveGoldAction 的公开包装器把出/收两方整体反转：内部 ApplyInternal 收到的是 recipientHero 与 -amount | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.7 第 3 条实证 |
| F-049 | ApplyBetweenCharacters(null, Hero.MainHero, -金额) 是玩家在付钱（买雇佣兵），出方为 null 不能当方向判据 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.7 第 3 条实证 |
| F-050 | GiveGoldAction 里收方那半句不夹金额，出方那半句通常先 MathF.Min，'哪一侧被夹过'是读方向的旁证 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.7 第 3 条附带线索 |
| F-052 | SandBox\GUI\Prefabs\Map\MapBar.xml 左下栏那排按钮的 DataSource 是 {MapNavigation\NavigationItems} | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §8 实战范例 |
| F-053 | 导航列表由 INavigationHandler.GetElements() 供料 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §8 实战范例 |
| F-054 | MapNavigationHandler 在 SandBox.View.dll 中公开、非 sealed，OnCreateElements() 是 protected virtual | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §8 实战范例 |
| F-058 | 官方 XML 里的 Id 反引用例是 FillWidget="FillVisual" | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §9 未验 |
| F-065 | 遥测日志里的关键字段形态：stageMs= / totalMs= / elapsedMs= / matches= / scanned= / reason= / fallback= / mode= | 未标版本 |  | bannerlord-mod-recon §2 第 2 步 |
| T11 | stored == expanded 的条目硬喂 lz4.block.decompress 会抛 LZ4BlockError: Decompression failed: corrupt input。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 2、§2 |
| T12 | texture2ddecoder 的块解码函数（decode_bc1/bc3/bc7 等）返回 BGRA，不是 RGBA。 | 未标版本 |  | bannerlord-tpac-extraction §0.1 坑 3 |
| T45 | 命名坑：XML 里材质叫 custom_banner_icons_01，但包里资产名是 custom_banner_icons（没有 _01）。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 命名坑 |
| T46 | BrushLayer 的 Name 是代码里的 StringId，Sprite 才是图；两者编号常不一致（主控栏 icon6=王国、icon7=家族）。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 步骤 1（⚠ 层的 Name） |
| T51 | graphics 层的 manage_fleet 用了 Map\ship（另一分类的图），一个 brush 的层可以跨分类引用精灵。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 顺手可用的旁证 |
| T71 | read_csharp_type 会把长方法体折叠掉，需要改用 read_file(path,startLine,lineCount) 按行补读。 | 未标版本 | 是 | bannerlord-tpac-extraction §7 还没吃到的 |
| S09 | UTF-16LE 的中文字节两个都非零，所以 (?:[\x20-\xff]\x00){4,} 这种隔字节为 0 的模式匹配不到中文。 | 未标版本 |  | bannerlord-mod-skeleton §2 坑 1（⚠ 写检查正则时注意） |
| S13 | <SubModuleClassType> 必须是完整命名空间 + 类名。 | v1.3.15 |  | bannerlord-mod-skeleton §3 |
| S14b | 会话启动只能监听 CampaignEvents.OnSessionLaunchedEvent，回调签名 Action<CampaignGameStarter>。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4 最小写法 |
| S30 | INavigationElement 成员含 StringId、Permission、IsActive、Tooltip、OpenView()、GoToLink() 等。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3 |
| S31 | NavigationPermissionItem 是 struct，构造签名为 (bool isAuthorized, TextObject reasonString)。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3 |
| S32 | MapNavigationItemVM(INavigationElement) 是公开构造，内部 ItemId = StringId。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3 |
| S35 | MapNavigationElementBase 的构造函数要具体类型 MapNavigationHandler，继承它就得引用 SandBox.View.dll。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.1 步 1 |
| S36 | 预制件里 Text="@X" 绑定的属性必须在 setter 里调用 OnPropertyChangedWithValue，光改字段界面不动。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3（面板 VM 继承 ViewModel） |
| S37 | 预制件写 Command.Click="ExecuteXxx" 对应 VM 里 public void ExecuteXxx()。 | v1.3.15 |  | bannerlord-mod-skeleton §8.3（按钮命令） |
| S40 | GauntletLayer 用 (name, 546, false) 构造，原版层优先级取 540~547 区间。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.1 步 3 |
| S41 | LoadMovie 的参数是预制件文件名（不带 .xml）。 | v1.3.15 |  | bannerlord-mod-skeleton §8.1 步 4 |

## 证据

- **bgda-028** 实测 Caladog → stringId w51wGx4b → CNs 卡拉多格。
- **bgda-040** 实测 Arenicos 命中、Sarapios 0 行（后者后来被推翻，见下条）。
- **bgda-055** CampaignGameStarter.cs:71, 76。
- **bgda-056** 读 ExplainedNumber.cs。
- **bgda-057** 读 ExplainedNumber.cs 的行号。
- **bgda-061** 读 CampaignEvents.cs；事件名常比模型名更贴近玩法（如 OnPlayerEarnedGoldFromAssetEvent）。
- **bgda-063** 逐条看命中上下文后排除。
- **bgda-068** 实测查询 SizePolicy* 返回 1 条。
- **bgda-070** 实测 'Gauntlet UI prefab layout size policy' 0 条、'GauntletUI' 5 条。
- **bgda-074** 读 sage 的 toolset 定义。
- **bgda-075** 实测两处参数名报错记录。
- **bgda-127** 读 TownMarketData.cs 访问修饰符。
- **bgda-146** DLL 躺在 Modules 目录里不等于在跑。
- **bgda-166** 只读直查连接方式的实测备选。
- **F-015** 对官方 XML 的跨行属性实跑 rg，不加 -U 时输出空，加 -U 后命中
- **F-032** 读 CampaignEvents.cs 并 grep 'public static IMbEvent'
- **F-039** 读 ChangeOwnerOfSettlementDetail 枚举定义
- **F-040** 读 KillCharacterActionDetail 枚举定义
- **F-041** 读 Village.VillageStates 枚举定义
- **F-042** 读 MobileParty.cs 的这排属性定义：IsCaravan/IsVillager/IsPatrolParty/IsLordParty/IsCustomParty/IsGarrison/IsMilitia/IsBandit
- **F-048** 读 GiveGoldAction.ApplyForSettlementToCharacter(giverSettlement, recipientHero, amount) 与其 private ApplyInternal 的参数顺序
- **F-049** 定位买雇佣兵路径上的 ApplyBetweenCharacters 调用并核对其语义
- **F-050** 对比 ApplyInternal 两个分支的金额处理代码
- **F-052** 读 MapBar.xml 该排按钮的 DataSource 属性
- **F-053** 反编译 SandBox.View.dll，读 INavigationHandler 接口定义
- **F-054** 反编译 SandBox.View.dll，读 MapNavigationHandler 的类型声明与成员修饰符
- **F-058** 读官方 Prefabs 中 FillWidget 属性的实际写法
- **F-065** 在 AnimusForge 的 Timeline/Stats 文件里实际读到的字段名
- **T11** 在现行 gauntlet_ui.tpac 的未压缩条目 _4/_5/_8 上实测触发该异常串。
- **T12** 包文档原文写 decompresses bc7 textures to BGRA；实测纯红 BC1 块(color0=color1=0xF800)经 decode_bc1 输出前 4 字节为 [0,0,255,255]。
- **T45** 按 XML 名反查失败，剥掉尾部 _\d\d 后才在 core.tpac 命中。
- **T46** 读 SandBox\GUI\Brushes\MapBar.xml 的 BrushLayer 并与界面实际含义比对。
- **T51** 读 graphics 层既有实现的 Brush 定义，发现其 Sprite 指向另一分类。
- **T71** 原文记录 read_csharp_type 输出折叠长方法体，改按行读才拿到完整实现。
- **S09** 写检查正则时实测该模式对中文串不命中，改用按字节 count。
- **S13** skill 的 SubModule.xml 模板与 §3 说明要求写全名。
- **S14b** skill §4 给出的事件注册写法与回调签名。
- **S30** 反编译接口读出成员清单，另有 IsLockingNavigation、HasAlert、AlertTooltip，见 §8.3。
- **S31** 反编译读出该类型为 struct 及其构造函数参数。
- **S32** 反编译 MapNavigationItemVM 读出构造函数可见性与 ItemId 赋值。
- **S35** skill §8.1 第 1 步记录的基类构造函数签名约束。
- **S36** skill §8.3 记录的面板 VM 绑定要求。
- **S37** skill §8.3 记录的命令命名对应关系。
- **S40** skill §8.1 第 3 步记录的层构造与优先级区间。
- **S41** skill §8.1 第 4 步明确记录的调用约定。

---

# 否定式断言（negative-claim）

> 共 47 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-023 | bannerlord_heroes 没有 name 列。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-025 | 英雄名不能按 Hero.name.<heroId> 之类规则拼 stringId 查，实测全返回 0 行。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-026 | 索引库没有 EN 语言，实际为 TR/CNs/CNt/BR/DE/FR/IT/JP 等。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-044 | csharp_types 表只有签名级信息，不给方法体。 | v1.3.15 | 是 | bannerlord-game-data-access#读原版 C# 源码 |
| bgda-045 | bannerlord_policies 表没有任何数值列。 | v1.3.15 | 是 | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-047 | 『加一条政策就带一个百分比』在原版做不到。 | v1.3.15 | 是 | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-048 | 文化数值属性 militia_bonus/prosperity_bonus 不在 bannerlord_cultures 表，只在 spcultures.xml 的 Culture 元素属性上。 | v1.3.15 | 是 | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-062 | 源码实测 Loan/Credit/Lend 三个词真 0 命中。 | v1.3.15 | 是 | bannerlord-game-data-access#机械穷举四件套 |
| bgda-076 | MCP 的 search_source 在本机报 Executable not found in $PATH: rg，本机未装 ripgrep。 | 未标版本 | 是 | bannerlord-game-data-access#MCP 直连 |
| bgda-077 | read_csharp_type 只认识被索引的类型：查 CampaignSpeedModel 一律 type was not found，不代表该类不存在。 | v1.3.15 | 是 | bannerlord-game-data-access#MCP 直连 |
| bgda-079 | read_csharp_type 返回的方法体是 implementation collapsed，看不到真实实现。 | 未标版本 | 是 | bannerlord-game-data-access#MCP 直连 |
| bgda-083 | read_gauntlet_ui 只返回 data_sources 与 click_commands 两张清单，不解析尺寸/对齐/几何。 | 未标版本 | 是 | bannerlord-game-data-access#MCP 直连 |
| bgda-086 | 城堡没有描述文本：Settlements.Settlement.text.castle_V6 为 0 行，城堡只有 .name.* 条目。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-087 | 不能用 .text.* 是否命中断定某地是不是游戏实体，会把所有城堡误判成非游戏实体。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-088 | 聚落定义在 bannerlord_settlements（来自 ModuleData/settlements.xml），在 xml_entities 里查不到。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-096 | 反编译只有 10 个程序集，游戏自己的模块 SandBox/Native/StoryMode 不在里面。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-098 | settlements.xml 元素名是 Settlement（大写 S）且属性一行一个；小写 settlement 的正则写法 0 命中。 | v1.3.15 | 是 | bannerlord-game-data-access#解析 settlements.xml 坐标 |
| bgda-103 | ItemCategory 类里没有 IsFood，食物是逐件标记 ItemObject.IsFood（Core）。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-126 | 『某城下属村不产某货 ⇒ 该货必贵』是错的；没有哪座城能自给。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-149 | 这台机器没有 strings，用 strings 管道 grep 扫 DLL 会静默零命中、看着像干净阴性。 | 未标版本 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-155 | 要读界面代码得单独反编译 TaleWorlds.CampaignSystem.ViewModelCollection.dll，sage 索引不收它。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-160 | 索引里查不到某件货不等于游戏里没有：grain/meat 是代码造的，XML 里没有 Item id=grain。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-161 | 家族→王国映射在 SandBox/ModuleData/spclans.xml 的 super_faction，但元素名是 Faction 不是 Clan。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-167 | 索引零命中不等于游戏里没有：判『没有』前必须先跑机械穷举四件套。 | v1.3.15 | 是 | bannerlord-game-data-access#默认使用 |
| F-009 | GraphWidget 在 v1.3.15 出货 DLL 里只有定义方自引，全游戏没有其它调用方 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §4 |
| F-017 | 旧 harness 的 Git Bash 里没有 strings 命令，strings x.dll \| grep -q 永不命中 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §6 |
| F-023 | GraphWidget 的 LineBrush 是只存不读的死属性，设了不生效 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7 第 3 问 |
| F-024 | 官方没有任何图表控件用到贴图资源 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7 第 3 问 |
| F-029 | CampaignEvents.PlayerInventoryExchangeEvent 在反编译的 10 个程序集里只有订阅者，找不到触发方 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7.5 第 4 步 |
| F-035 | DefaultPerks.Trade.RapidDevelopment.PrimaryBonus（工坊所在城被攻占返还 5000）全树只命中定义处，没有实现 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7.6 第 3 步实证 |
| F-036 | WorkshopModel.DaysForPlayerSaveWorkshopFromBankruptcy 全树只命中定义处，没有实现 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7.6 第 3 步实证 |
| F-056 | 谁 new MapNavigationHandler() 在 v1.3.15 未找到，构造点不在 SandBox.View.dll 里 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §8 未决 |
| F-057 | ItemTemplate 内部能否用 Id 反引子控件，在 v1.3.15 未验证 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §9 未验 |
| F-062 | AnimusForge 的门控证据只在 FreezeWatchdog_Timeline.txt 里，Mod_Logic.txt 等业务日志一个都没有 | 未标版本 | 是 | bannerlord-mod-recon §1 与 §4 第 7 条 |
| F-074 | 在 Mod_Logic.txt／SETS.log／Event_Logs.txt 里搜 mode=onnx 得 0 命中，但该 onnx 机制实际是生效的 | 未标版本 | 是 | bannerlord-mod-recon §4 第 7 条 |
| T24 | 解错也是纯色，不能用熵或解出来全是纯色来判断解码对错。 | 未标版本 | 是 | bannerlord-tpac-extraction §2（不要用熵判断） |
| T27 | Bash 沙箱无外网，pip 装不上包。 | 未标版本 | 是 | bannerlord-tpac-extraction §3 环境 |
| T67 | banner_background_test_a 那张 2048² 图不是底纹网格采样的贴图，是编辑器用的形状表，不能当 UV 源反推格子。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 坑（绕了两轮） |
| T68 | tpac 里 mesh 资产的 LOD/法线/UV 流没有被细拆，只取了顶点+索引。 | v1.3.15 | 是 | bannerlord-tpac-extraction §7 还没吃到的 |
| T70 | bannerlord-sage MCP 的 search_source 与 search_bannerlord_knowledge 全废，因本机 PATH 无 rg。 | 未标版本 | 是 | bannerlord-tpac-extraction §7 还没吃到的 |
| S14 | CampaignBehaviorBase 没有 OnSessionLaunched 虚方法。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4（⚠ CampaignBehaviorBase） |
| S15 | Campaign 没有 Heroes 属性，要用 Campaign.Current.AliveHeroes / .Settlements。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4（⚠ Campaign 没有 Heroes 属性） |
| S22 | Dictionary<Town,int> 这个闭合泛型没有被原版注册，即使 Town 与 int 各自都已注册。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1（⚠ 里面的类型认识≠这个容器认识） |
| S25 | Dictionary<Town, CraftingOrderSlots> 确实存在，但它住在 CraftingCampaignBehavior 自己的 definer 里，不是「不用注册」的先例。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1（⚠⚠ 别把先例读反） |
| S33 | TextObject 没有 Empty，要用 TextObject.GetEmpty()。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3（⚠ TextObject 没有 Empty） |
| S38 | TaleWorlds.CampaignSystem.ViewModelCollection.dll 不在反编译源码树里，源码树只有 9 个程序集。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.4 |
| S43 | 离线五道验证验不到「游戏真的加载了模组」，必须留给人启动游戏看 <mod>.log 出没出现。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6（验不到的） |

## 证据

- **bgda-023** 读该表列清单：只有 heroId/faction/clan/spouse/father/mother/alive/isTemplate/text。
- **bgda-025** 对多个 heroId 实测拼接查询，均 0 行。
- **bgda-026** 对 localization_entries.language 去重。
- **bgda-044** 实测该表内容；要看 SizePolicy/ScaledSuggestedWidth 怎么算必须读 .cs。
- **bgda-045** 列只有 displayName/descriptionText/proposalText/effectsText/rulerSupport/lordsSupport/commonsSupport/defaultCultureIdsJson。
- **bgda-047** 政策表无数值列 + 数值硬编码在模型，两条合证。
- **bgda-048** 该表无这些列；属性写在 Modules/SandBoxCore/ModuleData/spcultures.xml。
- **bgda-062** 在 assets/Source/ 全量 grep 后计数为 0。
- **bgda-076** src/tools/search-source.ts:27 硬编码 rg，无 env 可覆盖。
- **bgda-077** 实测查询报错；1.3.15 里真名已改。
- **bgda-079** 实测该工具返回内容。
- **bgda-083** 实测其正则只抓 DataSource= 与 Command.Click=。
- **bgda-086** 实测该 stringId 查询返回 0 行。
- **bgda-087** 由 castle_V6 无描述文这一事实推出。
- **bgda-088** 实测按文本搜 xml_entities 找不到聚落。
- **bgda-096** assets/Source/ 下目录清单实测。
- **bgda-098** 实测 <settlement\b[^>]*> 因属性跨行而 0 命中。
- **bgda-103** 读 ItemCategory 与 ItemObject 源码。
- **bgda-126** 供给取货架值而非本地产量，由第 6 条公式推出。
- **bgda-149** 实测命令不存在导致的空结果。
- **bgda-155** 查 SettlementDecision 零命中，属索引未覆盖而非类不存在。
- **bgda-160** DefaultItems.Create("grain") → RegisterPresumedObject，故 bannerlord_items 零命中。
- **bgda-161** 该文件里一个 <Clan 都没有（曾为此白跑一轮）。
- **bgda-167** 由 grain/meat 零命中、Sarapios 误判两例反推。
- **F-009** grep -l -a GraphWidget 扫 bin 与各 Modules 的 Win64_Shipping_Client/*.dll，仅定义方命中
- **F-017** 实跑 strings --version 确认该命令不存在，写在循环里则每轮都失败
- **F-023** 通读 GraphWidget 的全部 Refresh* 方法，LineBrush 只有赋值处、没有消费处
- **F-024** 普查官方 Prefabs 里 GraphWidget 的用法，未见贴图引用
- **F-029** 在出货 DLL 反编译树里 grep 该事件的触发调用，无命中；触发方实际在 VM 层
- **F-035** rg 该字段名于反编译树，仅 1 处命中且就在定义文件内
- **F-036** rg 该属性名于反编译树，只命中定义处
- **F-056** 在 SandBox.View.dll 反编译树里搜该构造函数调用点，无命中
- **F-057** 官方 Id 反引用例 FillWidget="FillVisual" 不在 ItemTemplate 里，没有现成样板可依
- **F-062** 在 Mod_Logic.txt／SETS.log／Event_Logs.txt 搜 mode=onnx 得 0 命中，改查 Timeline 才见 onnx=True
- **F-074** 同一批日志里 0 命中，而 FreezeWatchdog_Timeline.txt 有 onnx=True 的实证
- **T24** 实测错误解码同样输出纯色图，故该判据无效，改用统计特征。
- **T27** 原文记录 bash 沙箱无外网，只能走 PowerShell 装包并落日志再读。
- **T67** 该图不透明像素几乎只有蓝通道≈0.5、没有绿通道掩码，且为手工拼版非规则网格。
- **T68** 原文自述本轮只取顶点与索引，其余流未解。
- **T70** 原文记录该 MCP 源码检索依赖 rg 而本机 PATH 无 rg，只有 read_csharp_type 可用。
- **S14** 反编译 CampaignBehaviorBase 未见该虚方法，会话启动只能靠事件监听。
- **S15** skill 明确记录 Campaign 无 Heroes 属性及替代访问路径。
- **S22** 对照两张定义表的 typeof(...) 建允许集后，Dictionary<Town,int> 不在其中，实测该字段导致存档全挂。
- **S25** 原文记录把这条先例读反的代价是让别人的存档坏掉。
- **S33** 反编译 TextObject 未见 Empty 成员，只有 GetEmpty()。
- **S38** skill §8.4 记录源码树缺该界面层程序集，凡涉及 VM 必须读 DLL。
- **S43** skill §6 明确把这一步列为离线验证的盲区。

---

# 版本事实（version-fact）

> 共 8 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-005 | 本机索引记录 gameVersion=v1.3.15.110062，与 AWAKE 模组目标 API v1.3.15 一致。 | v1.3.15 |  | bannerlord-game-data-access#位置与状态 |
| bgda-078 | 1.3.15 里该模型真名是 DefaultPartySpeedCalculatingModel（TaleWorlds.CampaignSystem.GameComponents/）。 | v1.3.15 |  | bannerlord-game-data-access#MCP 直连 |
| bgda-162 | 帝国是 empire / empire_w / empire_s 三个独立王国。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-164 | 旧文档里的类名在 1.3.15 里常已改名。 | v1.3.15 | 是 | bannerlord-game-data-access#默认使用 |
| F-059 | 本批两个源 skill 的全部实测结论基于游戏 v1.3.15（技能 §8 自标'本机 1.3.15'） | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §8 |
| T69 | EmAssetPackages 与 AssetPackages 两套并存，同一资产可能两边都有，优先取 EmAssetPackages。 | v1.3.15 | 是 | bannerlord-tpac-extraction §7 还没吃到的 |
| S01 | 游戏本体在 D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord，本批记录的项目版本是 v1.3.15.110062。 | v1.3.15.110062（当前已升级到 v1.4.8） | 是 | bannerlord-mod-skeleton §0 先决条件表 |
| S45 | 模组 csproj 的目标配置为 net472 / x64 / Library / LangVersion 10。 | v1.3.15 | 是 | bannerlord-mod-skeleton §1 目录结构 |

## 证据

- **bgda-005** 读 setup-state.json 的 gameVersion/fullVersion/gameDir。
- **bgda-078** 在 assets/Source/ 下 grep BaseSpeed/CalculateSpeed 定位。
- **bgda-162** 读 spclans.xml 的 super_faction 与 kingdoms 数据。
- **bgda-164** 实例：CampaignSpeedModel → DefaultPartySpeedCalculatingModel。
- **F-059** 读两份 SKILL.md 中显式写出的版本标注
- **T69** 在 Native 模块下同时观察到两套目录且内容有重叠。
- **S01** skill 开头先决条件表记录的本机实测路径与版本号。
- **S45** skill §1 目录结构里标注的 csproj 关键项。

---

# 路径事实（path-fact）

> 共 34 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-038 | 聚落名 stringId 格式为 Settlements.Settlement.name.<settlementId>。 | v1.3.15 |  | bannerlord-game-data-access#三个高频套路 |
| bgda-039 | 聚落描述文 stringId 格式为 Settlements.Settlement.text.<settlementId>。 | v1.3.15 |  | bannerlord-game-data-access#三个高频套路 |
| bgda-042 | 落点文件定性质：std_heroes_xml/std_lords_xml=英雄领主，std_world_lore_strings_xml=世界观，std_spclans_xml=特殊家族。 | v1.3.15 |  | bannerlord-game-data-access#三个高频套路 |
| bgda-043 | 原版 C# 源码在 assets/Source/bin/Win64_Shipping_Client/ 下可直接 Read/Grep。 | v1.3.15 |  | bannerlord-game-data-access#读原版 C# 源码 |
| bgda-049 | spcultures.xml 的文化属性读入位置是 CultureObject.cs:218-220、Deserialize :265-267。 | v1.3.15 |  | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-071 | 本机 ~/.workbuddy/mcp.json 已配 server bannerlord-sage，35 工具，toolset 默认 query-first。 | 未标版本 |  | bannerlord-game-data-access#MCP 直连 |
| bgda-072 | 起 MCP 服务的命令是 bun.exe run BannerlordSage-main/src/entrypoints/bannerlord-stdio.ts。 | 未标版本 |  | bannerlord-game-data-access#MCP 直连 |
| bgda-094 | 官方模块目录：Modules/SandBox/ModuleData/settlements.xml、Modules/Native/ModuleData/Languages/CNs/*。 | v1.3.15 |  | bannerlord-game-data-access#陷阱 |
| bgda-097 | 原版界面文案在 Modules/SandBox/ModuleData/Languages/CNs/std_SandBox-zho-CN.xml，约上千条，可反推界面行为与条件。 | v1.3.15 |  | bannerlord-game-data-access#陷阱 |
| bgda-165 | 《杠杆》仓库 out/supply/ 已有反编译产物清单，查之前先看一眼。 | v1.3.15 |  | bannerlord-game-data-access#村庄产出与可食用判定 |
| F-001 | 出货托管 DLL 在 bin\Win64_Shipping_Client\ 与 Modules\<模块>\bin\Win64_Shipping_Client\，全是明文托管 DLL | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §2 第一路 |
| F-003 | 全部扩展控件在 TaleWorlds.GauntletUI.ExtraWidgets.dll；控件工厂与预制系统在 TaleWorlds.GauntletUI.PrefabSystem.dll | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §2 |
| F-004 | 地图界面层的实现在 SandBox.View.dll，导航 handler 就在这个程序集里 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §2 |
| F-007 | 官方可用控件样本分布在 Modules 下 Native / SandBox / SandBoxCore / StoryMode / Multiplayer 五个模块的 GUI/Prefabs | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §3 第二路 |
| F-012 | MapBar.xml 的 BrushLayer 里存在官方自加的非原版项 manage_fleet，Sprite 指向 Map\ship | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §5 |
| F-019 | GraphWidget 的定义在 TaleWorlds.GauntletUI.ExtraWidgets.dll，不在 sage 索引覆盖的那 15 个 DLL 里 | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §6.1 |
| F-020 | OnShowInquiry 的订阅方在 TaleWorlds.MountAndBlade.GauntletUI.dll（Modules\Native\bin 下，索引外） | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §6.1 |
| F-043 | EscortMerchantCaravanIssueBehavior.cs:666 造的队伍连速度都是传参的，可直接复用来做'派队伍做事' | v1.3.15 |  | bannerlord-vanilla-mechanism-recon §7.6 第 7 步实证 |
| F-060 | AWAKE/docs 已有一批 AF-* 文档逐块拆过 AnimusForge，并记了类名（OnnxEmbeddingEngine.cs 等） | 未标版本 |  | bannerlord-mod-recon §0.5 |
| F-061 | AI-Retrieval-Memory-Principles-* 文档里列着 AnimusForge 检索/记忆部分的反模式结论 | 未标版本 |  | bannerlord-mod-recon §0.5 |
| F-063 | 模组的时间线/遥测文件常不叫 *Log*，而是 *Timeline.txt、*_Stats.txt、*Checkpoint.txt、Observability.jsonl | 未标版本 |  | bannerlord-mod-recon §1 与 §2 第 2 步 |
| T26 | texture2ddecoder 提供 decode_bc1/bc3/bc4/bc5/bc6/bc7/astc 解码函数。 | 未标版本 | 是 | bannerlord-tpac-extraction §3 环境（本机已验证） |
| T26b | 本机默认 python venv 在 C:\Users\26811\.workbuddy\binaries\python\envs\default。 | 未标版本 | 是 | bannerlord-tpac-extraction §3 环境（本机已验证） |
| T29 | 旗帜图标图集的条目名是 custom_banner_icons 与 custom_banner_icons_02..16，位于 core.tpac。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 已验证的落点表 |
| T44 | 纹理源文件路径写在定义体里，形如 $BASE/Modules/Native/AssetSources/GauntletUI/<条目名>.png。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 源文件路径在定义体里 |
| S02 | 游戏程序集位于游戏根的 bin\Win64_Shipping_Client\TaleWorlds.*.dll。 | v1.3.15 | 是 | bannerlord-mod-skeleton §0 先决条件表 |
| S03 | 模组落点是游戏根的 Modules\<ModId>\。 | v1.3.15 |  | bannerlord-mod-skeleton §0 先决条件表、§2 坑 2 |
| S04 | 本机 dotnet.exe 在 C:\Program Files\dotnet\dotnet.exe，装有 9.0.306 与 10.0.301 两个 SDK。 | 未标版本 | 是 | bannerlord-mod-skeleton §0 先决条件表 |
| S05 | 反编译工具 ilspycmd 在 ~/.dotnet/tools/ilspycmd.exe。 | 未标版本 | 是 | bannerlord-mod-skeleton §0 先决条件表 |
| S06 | 本机已有一个能编译能跑的同代模组模板 D:\AWAKE-Dev\AWAKE\（含 AWAKE.csproj、SubModule.xml、src\SubModule.cs）。 | 未标版本 | 是 | bannerlord-mod-skeleton §0 先决条件表（现成模板） |
| S16 | 模组日志落点是程序集所在目录，即 Modules\<ModId>\bin\Win64_Shipping_Client\<mod>.log。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4（日志落哪） |
| S39 | MapNavigationHandler 位于 SandBox.View.dll。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.4（MapNavigationHandler 就是这么捞出来的） |
| S46 | 现成存档字段闸脚本在 D:\卡拉迪亚金融大鳄\tools\gate_save_fields.py，含阳性对照用例。 | 未标版本 | 是 | bannerlord-mod-skeleton §6.1（现成模板） |
| S47 | 现成构建脚本 tools\build_mod.py 一条命令完成编译 + 部署 + 自检。 | 未标版本 | 是 | bannerlord-mod-skeleton §1、§5 |

## 证据

- **bgda-038** 实测 town_V7 → CNs 沙拉斯。
- **bgda-039** 实测 castle_village_V6_1 可查到 CNs 描述文。
- **bgda-042** 实测若干专名的 filePath 落点；落在 std_spclans_xml 的词是家族名，不能当人物引用。
- **bgda-043** 例 TaleWorlds.GauntletUI/TaleWorlds.GauntletUI.BaseTypes/Widget.cs、StackLayout.cs、UIContext.cs、GauntletUI.Data/GauntletMovie.cs。
- **bgda-049** 读 TaleWorlds.CampaignSystem/CultureObject.cs。
- **bgda-071** 读该配置文件与 sage 的 toolset 定义。
- **bgda-072** 手工 stdio JSON-RPC 起服务的实测命令。
- **bgda-094** 读游戏目录文件树。
- **bgda-097** 读该文件；搜业务词可捞出菜单项名、条件提示、后果说明、数值规则。
- **bgda-165** 含 TownMarketData/ItemData/ItemConsumptionBehavior/DefaultVillageTypes/Campaign 等，别重复反编译。
- **F-001** 在 D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord 下实地列目录并反编译上述两个位置的文件
- **F-003** 反编译出货目录这两个 DLL，读 WidgetFactory 与预制系统类型
- **F-004** 反编译 SandBox.View.dll，在其中定位到 MapNavigationHandler 等导航类型
- **F-007** 对这五个模块的 GUI/Prefabs 目录做 *.xml 标签名普查
- **F-012** 读 SandBox/GUI/Brushes/MapBar.xml，manage_fleet 条目的 Sprite 值为 Map\ship
- **F-019** 反编译 TaleWorlds.GauntletUI.ExtraWidgets.dll，在其中找到 GraphWidget 类型
- **F-020** grep -l -a OnShowInquiry 扫实际 DLL 文件，命中 Native 目录下这个程序集
- **F-043** 读该文件第 666 行附近的队伍创建代码
- **F-060** 读 AWAKE/docs 下 AF-CRITICAL-EVALUATION-*、AF-BORROWED-IDEAS-LOG.md、AF-Data-Study-*，其中记有 OnnxEmbeddingEngine.cs 等类名
- **F-061** 读该系列文档的反模式清单
- **F-063** 在 AnimusForge 的 Logs 目录实际列名归纳（09-16 实测）
- **T26** 在该 venv 内 pip install lz4 zstandard numpy pillow texture2ddecoder 后实测这些函数可用。
- **T26b** skill §3 记录该 venv 路径，并说明用其中的 python.exe 装解码依赖。
- **T29** 在 Native/EmAssetPackages/core/core.tpac 中按名字反查得到该条目名列表。
- **T44** 反查条目后打印定义体，读出其中的源路径串。
- **S02** skill 先决条件表记录的实测路径。
- **S03** skill 先决条件表与 §2 坑 2 记录的实测落点。
- **S04** skill 先决条件表记录的本机实测 SDK 版本与路径。
- **S05** skill 先决条件表记录的实测工具路径。
- **S06** skill 先决条件表与 §7 起手顺序均以该目录为抄写来源。
- **S16** skill §4 记录日志落点由 Assembly.GetExecutingAssembly().Location 决定。
- **S39** skill §8.4 记录在 bin\Win64_Shipping_Client 与内置模组 bin 里按类名捞到该类。
- **S46** skill §6.1 末尾记录的现成模板路径与内容说明。
- **S47** skill §1 与 §5 记录的脚本职责与路径。

---

> **待重验**：本文件中标 `待重验=是` 的共 **160** 条。
> 完整队列（跨全部领域）见 `AWAKE/docs/reference/reverify-queue.md` —— 本文件不重复内联，避免同一事实出现两次。

---

## 程序要点（从既有 skill 提取）

**仍可执行**

- 查游戏侧事实先只读 SQL 直查 bannerlord.db（mode=ro，WAL 报错时改 immutable=1）
- 读原版实现直接 Read/Grep assets/Source/ 下的 .cs，不要走 csharp_types
- 中文名两步查：原文 text=? 拿 stringId → 再查 language='CNs'
- 判『原版没有 X』前先跑机械穷举四件套：模型抽象成员/行为层/事件/动作
- 复用现成脚本：AWAKE tools/worldbook-runtime-sim/ 与《杠杆》tools/、out/supply/
- 查模组是否改了逻辑：先扫 DLL（带阳性对照），再查 LauncherData.xml 的 IsSelected
- 反编译出货 DLL（ilspycmd -p 或同类）出可 grep 源码树，产物放临时目录
- 在反编译树里 grep 类名/字段/调用方，定位写值点与死字段
- 读官方 XML（GUI/Prefabs、GUI/Brushes）确认按字符串查表的命名约定
- 读模组 Logs 下 Timeline/Stats/jsonl 取运行时证据（计数、耗时、开关）
- 任何计数/查找探针先做阳性对照：拿已知存在的串跑同一命令
- 抽事件全清单：在 CampaignEvents.cs 搜 'public static IMbEvent'
- 拆 tpac 用名字串反查定位条目，每 4 字节扫一遍，别按 bsize 跳。
- 解码前比 stored 与 expanded：相等按 raw 读，不等再 lz4.block.decompress。
- 按定义体格式串选 decode_bc7/decode_bc3 或 reshape，解完把 BGRA 换回 RGBA。
- 图集尺寸现读当前安装的 NativeSpriteData.xml，不抄记忆里的表。
- 建模组抄 AWAKE.csproj 的 Reference，配 SubModule.xml，部署两处落点并自检。
- 自检三件：ilspycmd 验基类、DLL 里 UTF-16LE count 中文、两张定义表对存档字段。

**已失效**（因 harness 变更）

- 起 bannerlord-sage MCP stdio 服务（bun.exe run bannerlord-stdio.ts）——本会话无 MCP 客户端
- 调 MCP 工具 read_gauntlet_ui / resolve_localization / read_csharp_type——无 MCP 客户端
- 用 MCP 的 search_bannerlord_docs 多词查询——无 MCP 客户端，且多词会被 AND 全连
- 用 bash 的 find/sort/sed（写 /usr/bin/sort 规避 Windows 同名 exe）——本机无 bash 工具
- rg -c 接 awk -F: 统计词频——无 bash/rg，且该管道在 Windows 路径下计数全变 0
- strings 管道 grep 扫 DLL 找模组覆盖者——本机没有 strings
- bash for 循环 grep 扫全游戏目录（无 bash 工具，且会 SIGTERM 截断）
- strings x.dll \| grep（本机无 strings，管道写法也已不存在）
- 命令里写 timeout 900 python（撞 Windows timeout.exe，交给 harness 超时）
- ilspycmd 传 MSYS /d/ 路径（必须传 D:\... 形式）
- 用 /usr/bin/sort、/usr/bin/grep -r、/usr/bin/find 绕同名 exe 的 bash 写法
- 把 sage 索引当出货 DLL 全量覆盖（只收 bin\Win64_Shipping_Client 下 15 个）
- 在 bash 里 pip install 或跑 python 脚本（本机无 bash 工具，改走 pwsh 调 venv python）。
- 用 bannerlord-sage MCP 的 search_source 检索源码（本机无 MCP 客户端）。
- 硬编码旧版图集尺寸表，或一律按 BC7 解码所有条目。
- 只在战役定义表里查 SyncData 字段（两张主表都要查）。
- 拿 banner_background_test_a 当底纹 UV 源反推格子。
- 按 icon 编号顺序推断 MapBar 按钮含义。
