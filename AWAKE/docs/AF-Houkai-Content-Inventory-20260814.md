# AF 版齁改（AnimusForgeHoukai）现有内容清单

> 日期：2026-08-14
> 基线：AnimusForgeHoukai v0.3.4.0
> 用途：整理可融入 `SlaaneshsEmbrace（斯拉涅斯之拥）` 的现有内容。本文只列现状与建议，不写实现代码。
> 决策标记：
> - ✅ 内容/文案/数值可直接复用
> - 🔧 机制需在斯拉涅斯之拥里重建（改用 MarcusAIFramework API，不复制 AF 依赖）
> - ❌ 不迁移（AF/爱与恨耦合层）
> - 🧩 斯拉涅斯之拥已有近似功能
> - 📋 待你整理决策

## 1. 模块概况

| 项 | 值 |
| --- | --- |
| 当前版本 | v0.3.4.0（DLL SHA-256 `BC44F59C...`） |
| 定位 | Standalone + Adaptive 合并版；Adaptive 依赖爱与恨/AF |
| 游戏依赖 | AnimusForge v1.3.2、爱与恨 RelationshipPatch v0.6.1、Harmony、MCM |
| 代码规模 | 根 src 50 个 C# + LoveHate 25 个 C#（不含备份） |
| 事件总数 | 388（78 自有引擎事件 + 310 插件式事件） |
| 语言包 | 24 个 CNs XML |
| UI | 6 个 Gauntlet Prefab + 1 张运行时图片 |
| 验证工具 | tools 下约 30 个脚本/测试 |
| 文档 | 根目录 87 个 md/txt（计划、审查、清单、路线） |

## 2. 事件内容清单（共 388 条）

### 2.1 自有事件引擎 HoukaiEvents（78 条，JSON 格式，含冷却/权重/选项/效果）

#### alchemy.json（炼金工作台配方，7 条）

- `hk_alchemy_lust_oil` 情欲之油
- `hk_alchemy_sacred_refine` 圣化精炼
- `hk_alchemy_taboo_trade` 禁忌交易
- `hk_alchemy_battle_elixir` 战意药剂
- `hk_alchemy_mobility_tonic` 迅捷药剂
- `hk_alchemy_healing_water` 疗愈圣水
- `hk_alchemy_astral_dew` 星露药剂

#### captivity.json（俘虏事件，36 条，男女各一套）

- `hk_captivity_first_touch` 囚笼初触（含 `_male`）
- `hk_captivity_first_touch_march` 行军途中（含 `_male`）
- `hk_captivity_gentle_counsel` 低声抚慰（含 `_male`）
- `hk_captivity_strict_order` 勒令服从（含 `_male`）
- `hk_captivity_reward_promise` 恩威并施（含 `_male`）
- `hk_captivity_public_display` 当众展示（含 `_male`）
- `hk_captivity_public_aftermath` 展示余波（含 `_male`）
- `hk_captivity_night_patrol` 夜巡（含 `_male`）
- `hk_captivity_night_bond` 夜下交心（含 `_male`）
- `hk_captivity_escape_attempt` 逃跑未遂（含 `_male`）
- `hk_captivity_escape_pardon` 逃跑后的宽宥（含 `_male`）
- `hk_captivity_break_through` 破开防线（含 `_male`）
- `hk_captivity_full_tame` 驯熟（含 `_male`）
- `hk_captivity_town_cell` 城镇囚室（含 `_male`）
- `hk_captivity_castle_dungeon` 城堡地牢（含 `_male`）
- `hk_captivity_village_barn` 村中谷仓（含 `_male`）
- `hk_captivity_sea_hold` 海上囚舱（含 `_male`）
- `hk_captivity_settlement_corner` 聚落一角（含 `_male`）

#### divine.json（女神神谕，6 条）

- `hk_divine_morning_prayer` 晨祷
- `hk_divine_divination` 占卜
- `hk_divine_flower_register` 花期登记
- `hk_divine_flower_pray` 求孕祈愿
- `hk_divine_stigmata_offer` 圣痕献祭
- `hk_divine_mothers_blessing` 母神祝福

#### divine_taboo.json（禁忌仪式，8 条）

- `hk_taboo_sinful_dream` 罪愆夜梦
- `hk_taboo_mark_offer` 禁忌烙印
- `hk_taboo_black_ritual` 黑祭
- `hk_taboo_blood_pact` 血契献金
- `hk_taboo_lust_absolution` 情欲赎罪
- `hk_taboo_sin_fuse` 罪愆引信
- `hk_taboo_blood_sacrifice` 血祭强身
- `hk_taboo_consecration` 禁忌祝圣

#### heat_stage.json（发情阶段事件，4 条）

- `hk_heat_rising_touch` 前奏试探
- `hk_heat_peak_embrace` 高峰邀约
- `hk_heat_waning_cozy` 消退温存
- `hk_heat_calm_banter` 平静日常

#### indulgence_month.json（放纵月，2 条）

- `hk_indulgence_spring_festival` 春祭放纵月
- `hk_indulgence_autumn_feast` 秋宴放纵月

#### sexual_power.json（性即权力，3 条）

- `hk_social_sex_favor` 床笫外交
- `hk_social_debt_body` 人情债抵偿
- `hk_social_reputation` 名声扩散

#### global_heat.json（插件式 Definitions，12 条）

- `heat_cycle_rising_signal` 发情前兆
- `heat_peak_release` 发情高峰
- `heat_aftercare_rest` 发情后的照料
- `dev_mouth_sensitivity_map` 口部敏感图谱
- `dev_chest_tolerance_drill` 胸部耐受训练
- `dev_hand_touch_calibration` 手部触碰校准
- `dev_foot_desensitization` 足部脱敏训练
- `dev_rear_adaptation_prep` 后穴适应准备
- `dev_pelvic_core_conditioning` 核心调理训练
- `dev_back_shoulder_bodywork` 背肩放松与感应
- `dev_fullbody_surface_map` 全身敏感图谱
- `dev_aftercare_recovery` 开发后照料

### 2.2 文化事件 CulturalEvents（91 条，7 文化 × 13）

每文化 13 条：9 条地域文化事件 + 4 条放纵月（年历/准备/当地活动/余波）。

- 阿塞莱 `aserai.json`：市场条款、估价与担保、商队夜市、契约履行、市场仲裁、双语契约草拟、印章真伪核验、香料辨识、夜市艺人观演、黑桃贸易展销年历、展销备货与契约草拟、黑桃展销夜、契约清算与认证复核
- 巴旦尼亚 `battania.json`：林地入仪、费奥纳试炼、季节祭礼、氏族见证、林地盟约、标记林地安全路径、立石旧事讲述、春种祝福准备、林地射术赛、森林庆典年历、林间备宴与试炼场、林地月夜、氏族清算与林地复原
- 帝国 `empire.json`：公共浴场礼法、药剂师问诊、私人养生法、派系沙龙、浴场赞助人、一起泡澡、蒸汽室谈话、浴油按摩、私人浴间共浴、浴场与酒神祭年历、浴场储备与祭典筹备、浴场酒神夜、祭后清算与浴场复常
- 库赛特 `khuzait.json`：帐篷客礼、骑手试炼、马匹照料盟约、诺颜大会、天空誓言、合搭毡帐、客帐奶茶、夜间守群、草原短途赛马、那颜大会与祭天年历、帐篷城与祭坛筹备、祭天盛典夜、草场分配与祭后清算
- 诺德 `nord.json`：炉火客礼、战利品发言权、长船归航、冬季耐力赛、雅尔誓言、炉火大锅共炊、来客长凳夜谈、冬衣与皮毛修整、靠港修补长船、长船狂欢年历、港口备宴与长船修整、长船狂欢夜、战利品分配与誓言清算
- 斯特吉亚 `sturgia.json`：蒸汽浴恢复、炉边比试、盾誓、深冬庆典、解冻清算、共采浴场柴薪、白桦枝拍浴、雪地冷却轮次、冬储清点、深冬庆典与垂脱节年历、冬储与祭台筹备、深冬垂脱夜、解冻清算
- 瓦兰迪亚 `vlandia.json`：侍从礼、竞技场誓言、家族徽记、庄园仪式、誓言裁断、披甲协作、庄园马厩巡查、胜者庆功宴、败者体面和解、犬舍宴会年历、庄园备宴与纹章布置、犬舍宴会夜、宴后誓言与债务清算

### 2.3 情色事件 EroticEvents（157 条）

#### global.json（身体开发通用，28 条）

- 口舌侍奉训练、胸部侍奉训练、蜜穴开发、后穴开发、手技训练、足技训练、腋下侍奉训练、呼吸与示意练习、胸部敏感图谱、润滑与放松准备、清洁与外部放松、握力与压力校准、足部清洁与按摩、清洁与气味分寸、基础口舌侍奉、舌尖与节律控制、温热敷与按摩、节律敏感训练、初阶容纳适应、节律与肌肉控制、初阶容纳训练、深度与节律适应、单手节律练习、双手协调练习、足弓与脚趾控制、单足节律练习、腋下敏感图谱、夹持力度练习

#### camp_erotic.json（营地/俘虏，17 条）

- 帐篷暖夜、营火贴身舞、夜哨私会、领主帐中夜、俘虏夜约与侍奉、赎金之外的身体偿债、战俘分类标价、按军功分战俘、犬舍登记、黑桃认证、赎金里的身体条款、性奴服役与退役出路、营中事后照料、营地水浴、军帐跪礼与服从誓言、营火驯服与献礼、战后包扎与归属宣告

#### 七文化情色事件（7 文件 × 16 = 112 条）

每文化 16 条，结构一致：5 条文化特色侍奉/交合 + 邀约礼 + 体位练习 + 器具练习 + 补剂练习 + 1 条越矩 + 敏感带探索 + 幻想披露 + 角色扮演 + 季节性私会 + 追责 + 恢复。

- 阿塞莱 `aserai_erotic.json`：契约手技侍奉、香油足技侍奉、香氛腋胸侍奉、认证口舌侍奉、商队密契交合、阿塞莱邀约礼、阿塞莱体位练习、阿塞莱器具练习、阿塞莱补剂练习、阿塞莱契约越矩、阿塞莱敏感带探索、阿塞莱幻想披露、阿塞莱角色扮演、阿塞莱秋冬商队私会、阿塞莱市场仲裁、阿塞莱赔偿与恢复
- 巴旦尼亚 `battania_erotic.json`：林地手礼、赤足大地仪式、林间身体涂绘、费奥纳胜者侍奉、林地月夜交合、巴旦尼亚邀约礼、巴旦尼亚体位练习、巴旦尼亚器具练习、巴旦尼亚补剂练习、巴旦尼亚林地禁忌越矩、巴旦尼亚敏感带探索、巴旦尼亚幻想披露、巴旦尼亚角色扮演、巴旦尼亚满月林地私会、巴旦尼亚氏族见证裁决、巴旦尼亚林地禁入与恢复
- 帝国 `empire_erotic.json`：浴场侍奉、秘药敏化校准、帝国私室调理、沙龙密室侍奉、酒神祭私仪、帝国邀约礼、帝国体位练习、帝国器具练习、帝国补剂练习、帝国浴场或药剂越矩、帝国敏感带探索、帝国幻想披露、帝国角色扮演、帝国季节私仪、帝国浴场与药剂越矩裁决、帝国越矩恢复与追责
- 库赛特 `khuzait_erotic.json`：帐中手技侍奉、骑手靴足礼、马鞍汗息侍奉、那颜帐内侍奉、祭天庆典交合、库赛特邀约礼、库赛特体位练习、库赛特器具练习、库赛特补剂练习、库赛特客礼破坏、库赛特敏感带探索、库赛特幻想披露、库赛特角色扮演、库赛特秋季扎营私会、库赛特那颜大会追责、库赛特部族庇护与恢复
- 诺德 `nord_erotic.json`：长屋手技慰劳、解冻靴足侍奉、炉火汗息侍奉、胜利口舌奖赏、分赃誓言共寝、诺德邀约礼、诺德体位练习、诺德器具练习、诺德补剂练习、诺德分赃滥用、诺德敏感带探索、诺德幻想披露、诺德角色扮演、诺德风暴后私会、诺德长屋挑战与重新分赃、诺德雅尔裁决与恢复
- 斯特吉亚 `sturgia_erotic.json`：蒸汽汗息侍奉、炉火手足侍奉、皮草卧席侍奉、深冬交合仪式、盾誓配种契约、斯特吉亚邀约礼、斯特吉亚体位练习、斯特吉亚器具练习、斯特吉亚补剂练习、斯特吉亚仪式胁迫、斯特吉亚敏感带探索、斯特吉亚幻想披露、斯特吉亚角色扮演、斯特吉亚深冬私会、斯特吉亚解冻清算、斯特吉亚血仇调停与恢复
- 瓦兰迪亚 `vlandia_erotic.json`：侍从手礼、骑靴足礼、徽记敏化仪式、庄园私训、骑士私誓共寝、瓦兰迪亚邀约礼、瓦兰迪亚体位练习、瓦兰迪亚器具练习、瓦兰迪亚补剂练习、瓦兰迪亚誓言越矩、瓦兰迪亚敏感带探索、瓦兰迪亚幻想披露、瓦兰迪亚角色扮演、瓦兰迪亚季节幽会、瓦兰迪亚誓言裁决、瓦兰迪亚誓言修复与清算

### 2.4 系统事件 SystemEvents（62 条）

#### camp.json（营地，8 条）

- 营火夜谈、辎重与口粮、双人夜哨、行军餐分食、伤患巡营、篝火女神祝祷、营中体温照料、帐篷客礼

#### economy_industry.json（经济/行业，7 条）

- 性声誉与市场估价、彩礼嫁妆与婚姻条款、精液与母乳市场、身体改造工坊与恢复、帝国禁药行会与黑市、商队性货与走私路线、妓院名妓与情报交易

#### faith_ritual.json（信仰/仪式，2 条）

- 女神神术场见闻、花期与生育祈祷

#### general_social.json（一般社交/关系，19 条）

- 账本互查、猎物分配、回忆第一次见面、一日结束复盘、季末共同记忆、第一次正式约会、关系纪念日、释放后的再会、城门晨行、清晨面包与热汤、安营选址、酒馆听闻、确认彼此好感、第一次亲吻、恋爱中的分歧商谈、定居点请愿听取、税赋方案辩论、战前任务分配、代寄俘虏家书

#### military_crime.json（军事/犯罪，8 条）

- 军旅俘虏与犯罪网络、军队中的性秩序检查、随军家属的生存安排、雇佣兵战斗与性服务合同、决斗竞技与赌注争议、围城心理威胁与民情处置、战俘分类赎金与分配争议、强盗海寇与小派系

#### politics_household.json（政治/家宅，9 条）

- 宫廷情妇与内宅权力、外交与朝贡中的性维度、宫廷情妇与枕边政治、后宫与内宅权力规则、外交中的性维度、附属国朝贡与身份条款、偷情与通奸的公开后果、阶级跨越与身份风险、求爱婚姻开放关系与家宅商谈

#### world_cycle.json（世界节律，9 条）

- 季节与欲望周期、七文化节庆日历、放纵月准备、放纵月当地活动、放纵月后的社会重排、节庆礼数公示、外来客礼说明、节庆市场巡查、节后恢复所

## 3. 数值与配置数据

### 3.1 BodyBalance.json（身体开发平衡）

- 标准分区 9 个：口部、胸部、核心、后庭、手、足、腋下、背、肩
- 发情参数：周期默认 14 天档位表（Rising/Heat/Waning/Sated），可调 8-21 天；强度、欲望、开发影响权重
- 恢复参数：体力/疲劳/酸痛/伤情/唤起按地点与时间恢复
- 单次事件上限：适应/控制/敏感/耐受/疼痛/劳损/伤情
- 开发公式：ExposureScore = `min(100, round(100*sqrt(ExposureXp/600)))`；分区开发 = 0.35 暴露 + 0.35 适应 + 0.30 控制
- 档位 7 级：未开发/青涩/熟悉/适应/熟练/深度养成/（90-100）

### 3.2 HeatStagePrompts.json（7 天发情周期提示词）

- 5 阶段文案：平静（Normal）/前奏（Rising）/高峰（Heat）/消退（Waning）/旧档余韵（Sated）
- 每阶段配套“适合推进什么”的对话口径

### 3.3 CultureAliases.json

- 文化别名表：用于关键词/文化判定兼容

### 3.4 Config.json（56 个设置项）

核心分组：

- 总开关：Enabled、EnableHeatCycle、EnableBodyDevelopment、EnableCaptivity、EnableEventEngine、EnableGoddessDialogue、EnableDivine、EnableAlchemy、EnableIndulgenceMonth、EnableNpcInitiation、EnableDialogueBridge、EnableNativeWorldEffects
- 概率/冷却：EventChancePercent(8)、GlobalEventCooldownHours(48)、PlayerDivineEventCooldownHours(72)、CaptivityEventChancePercent(17)、NpcInitiationChancePercent(35)、GoddessDialogueCooldownHours(1)、GoddessAffinityDailyGainLimit(3)
- 世界影响日限：金币 2000、声望 50、影响力 100、士气 20、关系 10、技能 1500、每日世界批次数 2、炼金净值 500000
- 女神对话：ReplyMaxTokens 700、AnalystMaxTokens 240、DebugMockGoddessReplyText
- UI：ShowMapCycleHud、ShowEventPopups、ShowExactValues
- 开发者：DeveloperMode、DebugTargetHeroId、DebugRequest* 一次性调试请求

## 4. 女神提示词与世界书/知识语料

### 4.1 DivinePrompts

- `goddess_slanesh.txt`：女神身份、六条世界铁律、性格语气（主动/享受/爱找乐子/亲昵不廉价/点燃不强扭）、对话规则、硬底线
- `goddess_analyst.txt`：效果提取器系统提示词，把女神回复转成受控命令

### 4.2 四版世界书 PlayerExports

四档对应：`卡拉迪亚（炫压抑）=clean`、`（黑暗且压抑）=dark`、`（超绝炫压抑）=extreme`、`（血腥且压抑）=bloody`。

- 每档 rules 467 个文件、personality_background 487 个文件
- KingdomProfiles 每档 1 份
- ActVariants 14 个：normal/female/suppressed/main_4_0/current_game_active、length_short/long、第四爱/小男娘/女同/傲娇/熟女MILF/跨性别/御姐/BDSM 玩法补充包
- OptionalAssets：自定义政策、世界外交、叛乱王国、NPC 生成（A/B/C）、NPC 统治者政策四档、周报 persona、记忆压缩 3 类
- 游戏运行目录：`Modules\AnimusForge\PlayerExports`
- 发布归档：`发布\齁改4.0文本更新包`、`齁改4.0大礼包（ohohoho）`

### 4.3 斯拉涅斯之拥现有知识库

- `ModuleData/Knowledge/slaanesh_knowledge.json`：20 篇知识（铁律、女神、放纵月、花期、献祭、神殿、七文化、历史、俘虏、娼妓、神术、势力等）

## 5. 代码机制清单（按系统）

### 5.1 生命周期/状态/存档

- `Source.cs`：程序集入口/版本
- `HoukaiLog.cs`：日志
- `HoukaiMergeRuntime.cs`：Standalone/Adaptive 模式判定
- `HoukaiBehavior.cs`：战役行为总控（185KB，含大量机制；迁移时需按子系统拆）
- `HoukaiModels.cs`：NPC/玩家/女神/俘虏/政治状态模型
- `HoukaiStorageCodec.cs`：JSON 存档编解码
- `HoukaiConfig.cs` / `HoukaiConfigBridge.cs`：MCM 配置与双向同步
- `HoukaiPaths.cs`、`HoukaiTimedCache.cs`、`HoukaiConflictGuard.cs`

### 5.2 发情/身体/UI

- `HoukaiCalculator.cs`：周期/阶段/强度/身体开发计算
- `HoukaiBalance.cs`：周期与日历常量
- `HoukaiBodyPackDetector.cs`：身体包检测（GT Carbon Body / BetterFemaleBodyShaved）
- `HoukaiBodyPanel.cs`：身体开发面板（3D 角色渲染、分区、裸体切换）
- `HoukaiMapHud.cs`：大地图周期 HUD
- `HoukaiStatDescriptions.cs`：玩家可见数值文案

### 5.3 事件引擎

- `HoukaiEventEngine.cs`：条件/权重/冷却/弹窗/结算/俘虏保底
- `HoukaiEventModels.cs`：事件模型
- `HoukaiEventPopup.cs`：事件弹窗 UI
- `HoukaiEventPromptComposer.cs`：事件注入对话提示词
- `HoukaiMechanicServices.cs`：放纵月/恢复/地区神场等机制服务

### 5.4 对话/记忆/NPC 主动

- `HoukaiConversationBridge.cs`：AF 对话注入（AF 专用）
- `HoukaiConversationResultBridge.cs`：对话结果回写
- `HoukaiMemory.cs`：短期记录/压缩/长期记忆
- `HoukaiPromptComposer.cs`：权威状态提示词
- `HoukaiPromptProvider.cs`、`HoukaiDialogueDirective.cs`
- `HoukaiNpcInitiation.cs`：NPC 主动概率/开场/待续

### 5.5 女神/神术/炼金/世界效果

- `HoukaiDivinePromptProvider.cs`：女神 Persona/Analyst 加载
- `HoukaiDivineCodeTable.cs`：白名单表
- `HoukaiDivineEffects.cs`：信仰/眷顾/罪愆/圣痕/花期/祝福/求子结算
- `HoukaiAlchemyCore.cs` / `HoukaiAlchemyService.cs`：炼金配方/档位/工作台
- `HoukaiGoddessAnalyst.cs`：效果命令解析
- `HoukaiGoddessDialogueService.cs`：两段式对话/效果结算
- `HoukaiGoddessDialogueVM.cs` / `Screen.cs` / `ImageBridge.cs`：女神面板 UI 与徽记注入
- `HoukaiWorldContextProvider.cs`：世界书 RAG/王国档案
- `HoukaiWorldEffectBridge.cs`：金币/声望/影响力/士气/技能/关系世界效果
- `HoukaiVowService.cs`：承诺体系

### 5.6 俘虏/菜单/导航

- `HoukaiCaptivity.cs`：主队俘虏管理
- `HoukaiNavigation.cs`：总览/营地/菜单注册
- `HoukaiEntryBehavior.cs`：城镇/城堡/村庄/领主大厅入口

## 6. AF/爱与恨专用层（不直接迁移）

以下代码强依赖 AF 或爱与恨签名/状态，斯拉涅斯之拥不复制，只作功能参考：

- `HoukaiAfGoddessBridge.cs`：AF 辅助 API 调用
- `HoukaiAfMemoryBridge.cs`：AF 非英雄记忆写入
- `HoukaiAfOpeningBridge.cs`：AF NPC 主动开场注入
- `HoukaiAfResumeBridge.cs`：AF 百科/周报/履历桥
- `HoukaiAfSceneReplyProbe.cs`：AF 自定义格式探针
- `LoveHate/HoukaiPatches.cs`：爱与恨 Harmony patch 全集
- `LoveHate/HoukaiPromptOverrideService.cs`：爱与恨提示词覆盖
- `LoveHate/HoukaiStateBridge.cs`：状态桥（反射读写爱与恨状态）
- `LoveHate/PluginEventBridge.cs`：322 候选事件注册桥
- `LoveHate/CulturalEventRegistry.cs`、`CulturalEligibilityService.cs`：爱与恨事件目录/资格
- `LoveHate/RelationshipSettings.cs`、`RelationshipStorageCodec.cs`、`RelationshipLog.cs`
- `LoveHate/OrdinaryReflection.cs`：反射签名表
- `LoveHate/HoukaiRuntimeDetector.cs`：爱与恨版本探测
- `LoveHate/HoukaiConfigBridge` 相关同步逻辑

## 7. UI 与资产

- Prefab：`HoukaiGoddessDialogue.xml`、`HoukaiBodyPanel.xml`、`HoukaiEventPopup.xml`、`AnimusForgeHoukaiBodyDevelopment.xml`、`AnimusForgeHoukaiEventSummary.xml`、`HoukaiMapCycleHud.xml`
- 图片：`GUI/Images/goddess_emblem.png`（女神徽记，运行时注入）
- 资产库：`AssetLibrary/UI/gui_icon_ui_source_01.png` + `ASSET_MANIFEST.md`

## 8. 工具与验证

- 构建/同步：`tools/build.ps1`、`tools/sync_module.ps1`、`tools/refresh_package_hashes.ps1`
- 离线验证：`tools/verify_all.ps1`（串联事件解析、效果结算、持久化、旧档迁移、热缓存、UI 审计、包审计、内容哈希）
- 运行时验证：`tools/verify_goddess_runtime.ps1`、`tools/wait_for_runtime.ps1`、`tools/collect_logs.ps1`
- 专项测试：GoddessParserTest、DivineEffectsTest、DivinePersistenceTest、OldSaveMigrationTest、HotPathCacheTest
- 审计脚本：事件 schema、事件 ContentHash、现代词、玩家可见文本、工作台事务、炼金档位、身体面板、UI 叠加、菜单集成、性别视角、情报消费、架构边界、风格回退、包级审计

## 9. 关键文档

- `通俗内容说明.md`：玩家视角功能说明
- `版本路线与玩家体验.md`、`0.3.x_推进清单.md`、`0.3.x_运行验证清单.md`
- `审查与验证记录.md`、`全量问题清单_20260814.md`（50 项）
- 各功能 PLAN + REVIEW LOG：女神接口、炼金/禁忌、俘虏、女档视角、对话接管、旧档迁移、UI 压力等
- `架构边界与数据流契约_20260811.md`、`反射兼容清单.md`

## 10. 与斯拉涅斯之拥现状对照

斯拉涅斯之拥 v0.2.0 已有（README A1-A28）：

| 斯拉涅斯之拥已有 | 对应 AF 内容 | 建议 |
| --- | --- | --- |
| 女神对话屏 + 祭坛 | GoddessDialogue + divine 事件 | 🔧 面板可借鉴布局，机制改用马库斯路由 |
| 20 篇知识库 | 四版世界书 + DivinePrompts | ✅ 世界观文本可并入知识语料 |
| 关系/身体/发情存储与命令 | HoukaiModels 关系身体发情字段 | 🔧 按马库斯 namespace 重建 |
| 身体开发全分区 | BodyBalance + BodyPanel | 🔧 数值公式可直接复用，UI 自建 |
| 7 天发情周期 | HeatStagePrompts + 7 天时钟 | 🔧 阶段文案可直接复用 |
| 事件引擎（代码定义） | HoukaiEvents JSON 388 条 | 🔧 需把 JSON 事件迁移到新引擎或做数据驱动 |
| NPC 主动/俘虏/玩家被俘/营地事件 | HoukaiNpcInitiation + captivity + camp | 🔧 内容文案可迁移，触发链路自建 |
| 周报/状态总览 | 世界效果摘要 + 状态总览 | 🔧 设计参考 |

## 11. 迁移整理建议

### P0 内容优先（✅ 可直接搬）

1. 女神 Persona 文本（goddess_slanesh.txt）→ 并入女神 Prompt
2. 六条世界铁律 + 七文化知识 → 扩充 slaanesh_knowledge.json
3. 388 条事件正文/选项/Aftermath → 转为斯拉涅斯之拥事件内容库
4. BodyBalance 公式与 7 档位、HeatStagePrompts 阶段文案
5. 炼金配方文案与档位定价表

### P1 机制重建（🔧）

1. 事件引擎数据驱动化：当前 Slaanesh 事件是代码硬编码，建议先把 JSON 内容灌入事件库
2. 身体开发/发情状态机：按马库斯 `body.develop`、`estrus.tick` 命令补齐
3. 世界效果结算：金币/声望/影响力/士气/技能/关系改走马库斯世界命令 + 日限
4. 女神效果解析：把 GoddessAnalyst 的受控命令换成马库斯 allowlist 命令
5. UI：女神面板、身体面板、事件弹窗按现有 Slaanesh Gauntlet 结构重做

### P2 暂缓/不迁移（❌）

1. AF/爱与恨全部反射、Harmony、状态桥、插件事件注册
2. Standalone+Adaptive 双模式判定
3. AF 记忆/周报桥（改为马库斯存储与 Timeline）

### 待你拍板（📋）

1. 388 条事件是否全量搬，还是按“营地/神谕/炼金/身体/文化”分批
2. 世界书四档是否保留四档分档，还是先并成一份知识语料
3. 炼金/禁忌是否在斯拉涅斯之拥第一波就做，还是等事件库稳定后接入
4. 俘虏/玩家被俘/营地三块玩法是否合并为一个“权力关系”系统
