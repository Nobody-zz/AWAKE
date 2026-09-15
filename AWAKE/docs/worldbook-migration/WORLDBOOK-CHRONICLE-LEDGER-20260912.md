# 编年史第三步 · 全量 337 条归轴台账（2026-09-12）
> **性质**：编年史知识库全量逐条归轴的**机器初分台账**（口径＝盘点文档 §五-4 第三步：先地点/事件，后家族/人物，马种与旧作传承词单列）。**机器分类只作草判**，人工逐条裁定沿本台账推进。
> **数据源**：编年史 337 rule（B 级）× 官方实体登记表 890 实体（锚点判定）× 官方本地化 CN 全量文本（提及判定）。
> **机器件**：`projection/authoring-out/_chronicle_ledger_20260912.json`（全量结构化数据）＋ `_ledger337_20260912.py`（分类器，分类规则内联可查）。

---

## 批次进度注记（09-12 深夜 · 都城批）

- **新出成品档 2 份**（拆→核→重写工序，已编译 0 诊断）：
  - `projection/authoring-out/saneopa.yaml`（萨涅俄帕，doc.politics.saneopa，3 断言 8 表达；含**涅雷采斯朝定都史**＝D 级裁定 Max 09-12，登记于 `WORLDBOOK-IMPERIAL-CAPITALS-RESEARCH-20260912.md` §五）
  - `projection/authoring-out/paravenos.yaml`（帕拉汶德/巴拉维诺斯，doc.politics.paravenos，3 断言 8 表达；英文城名裁定＝Paravenos）
- **旧档核对**：charas×3、lycaron×3 无"阿雷尼科斯建立吕卡隆"类冲突表述，不需修订。
- **迁都链正典补录**：`WORLDBOOK-TIMELINE-CHAIN-DRAFT-20260912.md` §六（沙拉斯→巴拉维诺斯→萨涅俄帕→吕卡隆→南帝国都城五段，新增冲突⑦挂起）。
- 台账相关行指向更新：萨涅俄帕（第 75/354 行）、帕拉汶德（第 97 行）、沙拉斯（第 66/344 行）、吕卡隆（第 83 行）——素材已被上述两档与 §六 消化。

## 一、总览
| 维度 | 分布 |
|---|---|
| 条数 | **337**（变体 1,926；顶层 TextMappings 112 条；带 When 条件 288 条） |
| 类型草判 | 人物/概念 189 · 事件候选（按正文） 62 · 地点 43 · 物产/经济 27 · 家族/部族 8 · 事件 4 · 信仰（红线单列） 4 |
| 五域草判 | war 165 · politics 64 · culture 56 · geography 28 · economy 24 |
| 官方交叉 | **Ⅰ锚定（登记表精确命中实体）107** · A 文本提及（官方 CN 文本出现该词）151 · 无官方锚 79 |
| 已知源缺陷 | 双文件：`rule_中原`×2（同内容、别名不同：中原 vs 中土/珀拉斯特）、`rule_巴旦尼亚水之女神`×2（同 Id 同内容）→ 合并 keywords 后取一 |

## 二、Ⅰ锚定 107 条（有官方实体锚，立档最高优先级）

| 条目 | 官方实体 | 类型草判 | 五域 | 变体 | When 维度 |
|---|---|---|---|---|---|
| 俄耳特拉 | entity.settlement.village_en5_2 | 人物/概念 | culture | 4 | SettlementIds,KingdomIds |
| 兀尔浑 | entity.hero.dead_lord_6_1 | 人物/概念 | culture | 2 | Cultures |
| 卡南克 | entity.settlement.village_v8_2 | 人物/概念 | culture | 4 | Cultures,Roles |
| 卡琉斯 | entity.settlement.castle_village_v6_1 | 人物/概念 | culture | 4 | Cultures,Roles |
| 塔利维尔 | entity.settlement.castle_village_v7_1 | 人物/概念 | culture | 4 | Cultures,Roles |
| 奥曼法德 | entity.settlement.castle_village_v4_1 | 人物/概念 | culture | 4 | Cultures,Roles |
| 德拉庞 | entity.settlement.castle_village_v3_1 | 人物/概念 | culture | 4 | Cultures,Roles |
| 德里亚特 | entity.settlement.castle_village_v6_2 | 人物/概念 | culture | 4 | Cultures,Roles |
| 拉尔纳克 | entity.settlement.village_v3_3 | 人物/概念 | culture | 4 | Cultures,Roles |
| 梅罗克 | entity.settlement.village_v5_2 | 人物/概念 | culture | 4 | Cultures,Roles |
| 瓦尼人 | entity.settlement.village_s6_3 | 人物/概念 | culture | 1 | Cultures |
| 瓦朗比 | entity.settlement.castle_village_v3_2 | 人物/概念 | culture | 4 | Cultures,Roles |
| 阿利斯维斯特 | entity.settlement.village_v9_2 | 人物/概念 | culture | 4 | Cultures,Roles |
| 阿洛斯唐 | entity.settlement.village_v6_3 | 人物/概念 | culture | 4 | Cultures,Roles |
| 韦雷克桑 | entity.settlement.castle_village_v8_1 | 人物/概念 | culture | 4 | Cultures,Roles |
| 马雷汶 | entity.settlement.village_v2_1 | 物产/经济 | culture | 4 | Cultures,Roles |
| 于桑克 | entity.settlement.castle_village_v1_1 | 人物/概念 | economy | 4 | Cultures,Roles |
| 奥尔斯热 | entity.settlement.village_v8_1 | 人物/概念 | economy | 4 | Cultures,Roles |
| 帕利松 | entity.settlement.village_v3_4 | 人物/概念 | economy | 4 | Cultures,Roles |
| 弗雷吉昂 | entity.settlement.village_v2_3 | 人物/概念 | economy | 4 | Cultures,Roles |
| 翁加尔 | entity.settlement.castle_village_v2_1 | 人物/概念 | economy | 4 | Cultures,Roles |
| 莫特 | entity.settlement.village_v6_2 | 人物/概念 | economy | 4 | Cultures,Roles |
| 菲尔贝克 | entity.settlement.village_v5_1 | 人物/概念 | economy | 4 | Cultures,Roles |
| 西岚达克 | entity.settlement.castle_village_v5_2 | 人物/概念 | economy | 4 | Cultures,Roles |
| 诺格伦 | entity.settlement.village_v5_3 | 人物/概念 | economy | 4 | Cultures,Roles |
| 费顿 | entity.settlement.castle_village_v2_2 | 人物/概念 | economy | 4 | Cultures,Roles |
| 阿兰塔斯 | entity.settlement.village_v9_1 | 人物/概念 | economy | 4 | Cultures,Roles |
| 阿罗曼克 | entity.settlement.village_v6_1 | 人物/概念 | economy | 4 | Cultures,Roles |
| 鲁兰德 | entity.settlement.village_v3_2 | 人物/概念 | economy | 4 | Cultures,Roles |
| 马林 | entity.settlement.castle_village_v8_2 | 地点 | geography | 4 | Cultures,Roles |
| 伊丽卡 | entity.hero.dead_lord_7_2 | 人物/概念 | politics | 2 | Cultures |
| 伊拉 | entity.hero.lord_1_37 | 人物/概念 | politics | 12 | Cultures,IdentityIds,KingdomIds,Roles |
| 俄斯提科斯 | entity.clan.clan_empire_north_1 | 人物/概念 | politics | 4 | KingdomIds,Roles |
| 俄洛斯 | entity.hero.lord_1_17 | 人物/概念 | politics | 7 | Cultures,KingdomIds,Roles,IdentityIds |
| 兀尔浑乃特 | entity.clan.clan_khuzait_1 | 人物/概念 | politics | 3 | KingdomIds,Roles,IdentityIds |
| 克洛托耳 | entity.hero.lord_1_11 | 人物/概念 | politics | 3 | KingdomIds,Roles |
| 加里俄斯 | entity.hero.lord_1_7 | 人物/概念 | politics | 4 | KingdomIds,Roles |
| 勒芒塔尔 | entity.settlement.village_v8_3 | 人物/概念 | politics | 4 | Cultures,Roles |
| 卢孔 | entity.hero.lord_1_1 | 人物/概念 | politics | 3 | KingdomIds,Roles |
| 厄庇斐里娅 | entity.hero.lord_1_1_12 | 人物/概念 | politics | 2 | Roles |
| 吕西卡 | entity.hero.lord_1_12 | 人物/概念 | politics | 2 | Cultures,KingdomIds |
| 巴努·卡拉兹 | entity.clan.clan_aserai_4 | 家族/部族 | politics | 7 | KingdomIds,Cultures,Roles,SettlementIds |
| 库洛夫 | entity.clan.clan_sturgia_2 | 人物/概念 | politics | 6 | Cultures,KingdomIds,Roles |
| 德泰尔 | entity.hero.lord_4_1 | 人物/概念 | politics | 8 | Cultures,KingdomIds,IdentityIds,Roles |
| 悲伤的肖农 | entity.settlement.town_b4 | 人物/概念 | politics | 7 | Cultures,SettlementIds,Roles,KingdomIds,IdentityIds |
| 戴·阿罗曼克 | entity.clan.clan_vlandia_3 | 人物/概念 | politics | 11 | Cultures,Roles |
| 拉革塔的三方拉锯 | entity.hero.lord_1_111 | 人物/概念 | politics | 4 | Cultures,KingdomIds |
| 沙拉斯 | entity.settlement.town_v7 | 人物/概念 | politics | 7 | Cultures,KingdomIds,IdentityIds,Roles |
| 法戎 | entity.hero.lord_1_15 | 人物/概念 | politics | 12 | Cultures,KingdomIds,Roles,SettlementIds,IdentityIds |
| 泰伊斯 | entity.hero.lord_3_5 | 人物/概念 | politics | 10 | Cultures,KingdomIds,IdentityIds,Roles,SettlementIds |
| 珀特洛斯 | entity.clan.clan_empire_south_1 | 事件候选（按正文） | politics | 10 | Cultures,KingdomIds,Roles |
| 珀特洛斯家的床笫政治 | entity.hero.lord_1_27_2 | 人物/概念 | politics | 4 | Cultures,KingdomIds,Roles |
| 瓦达尔热血马 | entity.settlement.castle_village_a9_2 | 物产/经济 | politics | 18 | Cultures,Roles |
| 科穆诺斯家的兵营王朝 | entity.hero.lord_se9_c1 | 家族/部族 | politics | 3 | Cultures,KingdomIds,Roles |
| 芬·格鲁芬多克 | entity.clan.clan_battania_1 | 人物/概念 | politics | 8 | Cultures,Roles,KingdomIds,IdentityIds |
| 萨万特 | entity.settlement.village_v7_1 | 人物/概念 | politics | 4 | Cultures,Roles |
| 萨涅俄帕 | entity.settlement.town_en3 | 人物/概念 | politics | 4 | Cultures,Roles |
| 蒙楚格 | entity.hero.lord_6_1 | 人物/概念 | politics | 9 | Cultures,KingdomIds,Roles |
| 阿契特 | entity.clan.clan_khuzait_3 | 人物/概念 | politics | 12 | Cultures,KingdomIds,IdentityIds,SettlementIds,Roles |
| 阿庇斯 | entity.hero.lord_1_9 | 人物/概念 | politics | 8 | Cultures,KingdomIds,IdentityIds,Roles,SettlementIds |
| “饿人”沃尔比约恩 | entity.hero.dead_lord_7_1 | 人物/概念 | war | 4 | Cultures,Roles,KingdomIds |
| 卡利奥克 | entity.settlement.village_v1_1 | 人物/概念 | war | 4 | Cultures,Roles |
| 卡拉多格 | entity.hero.lord_5_1 | 事件候选（按正文） | war | 10 | Cultures,Roles |
| 卡拉蒂尔德 | entity.hero.lord_4_6 | 事件候选（按正文） | war | 10 | Cultures,Roles |
| 吕卡隆 | entity.settlement.town_es4 | 事件候选（按正文） | war | 12 | Cultures,KingdomIds,SettlementIds,Roles |
| 因加泰尔 | entity.hero.lord_4_16 | 人物/概念 | war | 11 | Cultures,Roles |
| 坎尼人的王国 | entity.settlement.town_a1 | 人物/概念 | war | 8 | Cultures,Roles |
| 埃尔贡 | entity.hero.lord_5_3 | 事件候选（按正文） | war | 13 | Cultures,Roles |
| 埃蒂尔菲德 | entity.settlement.village_v1_2 | 人物/概念 | war | 4 | Cultures,Roles |
| 墨斯特里卡洛斯 | entity.clan.clan_empire_south_3 | 人物/概念 | war | 2 | SettlementIds,KingdomIds |
| 墨速宜 | entity.hero.lord_6_4 | 事件候选（按正文） | war | 3 | SettlementIds,KingdomIds,Roles |
| 奥特尔 | entity.clan.clan_nord_3 | 事件候选（按正文） | war | 8 | KingdomIds,Cultures,Roles |
| 尼姆尔 | entity.hero.dead_lord_3_1 | 事件候选（按正文） | war | 2 | Cultures,Roles |
| 岁仑 | entity.hero.dead_lord_6_2 | 事件候选（按正文） | war | 2 | Cultures,Roles,SettlementIds |
| 巴努·吉勒德 | entity.clan.clan_aserai_3 | 家族/部族 | war | 2 | Cultures,Roles |
| 巴努·萨兰 | entity.clan.clan_aserai_2 | 家族/部族 | war | 8 | Cultures,Roles,KingdomIds |
| 巴旦尼亚水之女神 | entity.settlement.town_b5 | 信仰（红线单列） | war | 2 | Cultures |
| 巴旦尼亚的部族裂痕 | entity.hero.lord_5_13 | 家族/部族 | war | 3 | Cultures,Roles,KingdomIds |
| 帕拉汶德 | entity.settlement.town_v3 | 事件候选（按正文） | war | 5 | Cultures,Roles |
| 库吉特 | entity.clan.clan_khuzait_2 | 事件候选（按正文） | war | 12 | Cultures,Roles,HeroIds |
| 库由格 | entity.hero.dead_lord_6_3 | 事件候选（按正文） | war | 16 | Cultures,Roles,HeroIds |
| 彭同 | entity.hero.lord_1_5 | 人物/概念 | war | 9 | Cultures,KingdomIds,Roles,IdentityIds |
| 恩泰里 | entity.hero.lord_4_5 | 事件候选（按正文） | war | 11 | Cultures,Roles |
| 戴·提哈 | entity.clan.clan_vlandia_2 | 人物/概念 | war | 8 | Cultures,Roles,KingdomIds |
| 戴·梅罗克 | entity.clan.clan_vlandia_1 | 事件候选（按正文） | war | 12 | Cultures,KingdomIds,IdentityIds,Roles |
| 戴·科尔坦 | entity.clan.clan_vlandia_4 | 人物/概念 | war | 8 | Cultures,KingdomIds,Roles |
| 拉盖娅 | entity.hero.lord_1_14 | 人物/概念 | war | 15 | Cultures,KingdomIds,IdentityIds,Roles |
| 拉革塔 | entity.settlement.town_ew1 | 事件候选（按正文） | war | 7 | Cultures,KingdomIds,Roles |
| 斯特吉亚的世仇 | entity.hero.dead_lord_2_2 | 人物/概念 | war | 4 | Cultures,Roles,KingdomIds |
| 朗瓦德 | entity.hero.lord_2_1 | 人物/概念 | war | 7 | Cultures,KingdomIds,IdentityIds,Roles |
| 涅雷采斯 | entity.clan.clan_empire_north_3 | 人物/概念 | war | 6 | Cultures,KingdomIds,Roles |
| 淮娅 | entity.hero.lord_1_36 | 人物/概念 | war | 9 | Cultures,KingdomIds,IdentityIds,Roles |
| 温吉德 | entity.hero.lord_3_1 | 人物/概念 | war | 9 | Cultures,KingdomIds,IdentityIds,Roles |
| 温都勒 | entity.hero.dead_lord_6_4 | 事件候选（按正文） | war | 12 | Cultures,KingdomIds,IdentityIds,Roles |
| 狄亚特马 | entity.settlement.town_en2 | 物产/经济 | war | 1 | Roles |
| 狄俄尼科斯 | entity.clan.clan_empire_west_3 | 人物/概念 | war | 6 | Cultures,KingdomIds,Roles |
| 瓦吉罗夫 | entity.clan.clan_sturgia_3 | 人物/概念 | war | 10 | Cultures,Roles |
| 科穆诺斯 | entity.clan.clan_empire_west_1 | 事件候选（按正文） | war | 17 | Cultures,Roles,KingdomIds |
| 突剌格 | entity.hero.lord_6_5 | 人物/概念 | war | 12 | Cultures,Roles |
| 老奥列克 | entity.hero.dead_lord_2_1 | 人物/概念 | war | 11 | Cultures,KingdomIds,IdentityIds,Roles |
| 至高王蒙 | entity.settlement.town_b3 | 信仰（红线单列） | war | 15 | Cultures,Roles |
| 西加 | entity.hero.lord_2_4 | 人物/概念 | war | 14 | Cultures,KingdomIds,IdentityIds,Roles |
| 贡达罗夫 | entity.clan.clan_sturgia_1 | 事件候选（按正文） | war | 7 | Cultures,KingdomIds,Roles |
| 贾尔马律斯 | entity.settlement.town_ew3 | 物产/经济 | war | 8 | SettlementIds,KingdomIds,Roles,Cultures |
| 阿丝葛莎 | entity.hero.lord_7_5 | 物产/经济 | war | 7 | Cultures,KingdomIds,Roles |
| 阿塞莱的两条旧债 | entity.hero.lord_3_3 | 事件候选（按正文） | war | 4 | Cultures,KingdomIds,Roles |
| 阿尔德里克 | entity.hero.lord_4_3 | 人物/概念 | war | 14 | Cultures,KingdomIds,IdentityIds,Roles |

## 三、红线与单列（不进常规立档流）

| 条目 | 处置 | 依据 |
|---|---|---|
| 巴旦尼亚水之女神 | **红线**：信仰整节排除，仅其它内容自然涉及处带一句（B 级） | Max 09-12 裁定；官方交叉=Ⅰ锚定 |
| 巴旦尼亚水之女神 | **红线**：信仰整节排除，仅其它内容自然涉及处带一句（B 级） | Max 09-12 裁定；官方交叉=A文本提及 |
| 至高王蒙 | **红线**：信仰整节排除，仅其它内容自然涉及处带一句（B 级） | Max 09-12 裁定；官方交叉=Ⅰ锚定 |
| 荒野女神 | **红线**：信仰整节排除，仅其它内容自然涉及处带一句（B 级） | Max 09-12 裁定；官方交叉=无官方锚 |

## 四、地点（43）——第三步主攻第一批

| 条目 | 变体 | When 维度 | 五域 | 官方交叉 | aliases | 摘要头 |
|---|---|---|---|---|---|---|
| 乌卡利昂高原 | 3 | Cult | geography | 无官方锚 | 2 | 乌卡利昂高原是卡拉迪亚大陆最高高原。 |
| 伊勒坦山 | 3 | Cult | geography | A文本提及 | 1 | 伊勒坦运输线从伊勒坦山向帝国运输牛羊谷物 |
| 佛俄提斯湖 | 2 | Cult,Role | geography | 无官方锚 | 1 | 佛俄提斯湖位于帝国境内靠近帝都。 |
| 加隆托海峡 | 2 | Sett | geography | 无官方锚 | 1 | 加隆托海峡是卡拉迪亚与纳哈撒的交界处。 |
| 南部城邦 | 3 | King,Role | geography | 无官方锚 | 1 | 南部城邦控制珀拉斯海与大草原商路，人口富庶 |
| 厄吕特律斯山 | 11 | Cult,King,Role,Sett | geography | 无官方锚 | 1 | 厄吕特律斯山是卡拉迪亚大陆最雄伟的山峰，坐落在阿米尼斯河谷之中，由深色玄武巨岩构 |
| 喀拉卡兹河 | 1 | — | geography | 无官方锚 | 1 | 喀拉卡兹河是诺德与库赛特的界河 |
| 地下海 | 2 | King | geography | A文本提及 | 3 | 帝国传言：死者灵魂入地下海，生者可前往求教智者；南帝国拉耳图绪斯盐矿盐泉被视为地 |
| 弥戎河 | 2 | Role | geography | A文本提及 | 1 | 弥戎河是巴旦尼亚、北帝国、斯特吉亚的界河 |
| 德夫赛格高原 | 1 | Role | geography | A文本提及 | 1 | 德夫赛格高原位于卡拉迪亚与达西分界处 |
| 攻城塔 | 3 | Role | geography | A文本提及 | 2 | 攻城塔怕火油、投石车和弩炮攻击。 |
| 攻城锤 | 3 | Role | geography | 无官方锚 | 2 | 攻城锤是围城战中最踏实器械，撞城门无需准头。 |
| 格林托尔矮种马 | 1 | — | geography | A文本提及 | 1 | 格林托尔矮种马性能差劲，续航短体力差 |
| 沙漠强盗 | 2 | — | geography | A文本提及 | 1 | 哈拉米原意“扈从”，后变为“强盗”。 |
| 沙漠狂风 | 7 | Cult | geography | A文本提及 | 1 | 沙漠狂风是竞技场里起名的普通马，不适合战场 |
| 泰瓦尔湖 | 5 | Cult,Role | geography | A文本提及 | 1 | 泰瓦尔湖白天提供鳟鱼和灌溉，夜晚被视为通往彼世的门 |
| 海寇 | 1 | — | geography | A文本提及 | 1 | 海寇极难抓捕，装备精良善用标枪。 |
| 海运 | 1 | — | geography | 无官方锚 | 1 | 海运运载量大于陆路且速度较快 |
| 湖鼠帮 | 1 | — | geography | A文本提及 | 1 | 湖鼠帮在北方大湖沿岸沼泽地，用假灯塔引诱船只搁浅 |
| 绿林兄弟会 | 6 | Cult,Role,King | geography | A文本提及 | 1 | 绿林兄弟会起源于杰屈朗林子，曾是劫富济贫义军后变质 |
| 绿林强盗 | 1 | — | geography | A文本提及 | 1 | 绿林强盗藏身林中盗猎抢劫村民，擅长长弓偷袭 |
| 罗瓦尔山地 | 1 | — | geography | 无官方锚 | 2 | 罗瓦尔山地陆路通道奥曼法德走廊，易守难攻。 |
| 贝恩兰岛 | 4 | Sett,Role,Cult | geography | 无官方锚 | 3 | 贝恩兰岛名称由来与海象象牙有关 |
| 达尔马河 | 1 | — | geography | A文本提及 | 1 | 达玛尔河阿塞莱 |
| 韦桑村 | 4 | Cult,Role | geography | A文本提及 | 1 | 韦桑村位于沙拉斯附近，埃皮尔山溪水滋养橄榄树。 |
| 马林 | 4 | Cult,Role | geography | Ⅰ锚定 | 1 | 马林葡萄酒有清爽果香，奥克斯湖水汽滋养，军官常购。 |
| 黎明山脉 | 2 | Sett | geography | A文本提及 | 2 | 柯希·罗希尼黎明山脉位于德夫赛格高原东缘。 |
| 中原 ⚠dup | 4 | Sett,King,Role | war | 无官方锚 | 3 | 中原人富足保守勇猛尚武有民粹倾向 |
| 中原 ⚠dup | 4 | Sett,King,Role | war | 无官方锚 | 4 | 中原人富足保守勇猛尚武有民粹倾向 |
| 南部岛区 | 2 | — | war | 无官方锚 | 1 | 卡拉德人（帝国人）先祖一千年前来自南部岛区 |
| 卡恰尔半岛 | 9 | Cult,Role | war | A文本提及 | 1 | 卡恰尔半岛是斯特吉亚东边伸入海中的狭长陆地，地形多石崖 |
| 塔奈西斯湖 | 8 | Cult,Sett,Role | war | A文本提及 | 1 | 塔奈西斯湖又称达那孜海，被马凯布、席隆尼亚、柴坎三镇环绕 |
| 塞堤斯河 | 8 | Cult,Role | war | A文本提及 | 1 | 塞堤斯河发源于吕卡里亚平原以北的丘陵地带，自北向南穿过平原，最终注入珀拉斯海。 |
| 奥克斯湖 | 2 | Cult | war | A文本提及 | 2 | 奥克斯湖原名林·莫德里斯，原为沼泽池塘，传说中莫德里斯湖坐落于火山口，是一道巨人 |
| 奥尼石山 | 7 | Cult,King,Sett,Role | war | A文本提及 | 1 | 奥尼石山位于吕卡隆背后，拥有卡拉迪亚最丰富的银矿脉，是帝国国库和军饷的重要来源。 |
| 山贼 | 1 | — | war | A文本提及 | 3 | 山贼藏身卡拉迪亚丛林，以木寨为据点。 |
| 德律亚山 | 1 | — | war | A文本提及 | 1 | 德律亚山西起弥戎河东至阿耳戈隆形成天然屏障 |
| 拉科尼斯湖 | 3 | Cult,Role | war | A文本提及 | 2 | 拉科尼斯湖是帝国、斯特吉亚、库赛特三方势力之间的交通动脉。 |
| 沙拉斯湾 | 3 | Cult | war | A文本提及 | 1 | 沙拉斯湾被帝国称为摇篮之岛，卡拉德人先祖在此首次登陆。 |
| 珀拉斯海 | 14 | Cult,Role | war | A文本提及 | 5 | 珀拉斯海曾是帝国的内湖，商船自由通行。 |
| 纳哈撒沙漠 | 15 | Cult,Role | war | A文本提及 | 5 | 纳哈撒沙漠是阿塞莱人的天然屏障 |
| 罗多克谷地 | 1 | — | war | 无官方锚 | 2 | 罗多克谷地是瓦兰迪亚的经济政治文化中心 |
| 车尔特格山 | 2 | Sett | war | A文本提及 | 1 | 车尔特格山村民以伐木养猪为生 |

## 五、事件（名中明确+正文候选，4）——第三步主攻第二批

| 条目 | 变体 | When 维度 | 五域 | 官方交叉 | aliases | 摘要头 |
|---|---|---|---|---|---|---|
| 劫匪 | 1 | — | culture | A文本提及 | 1 | 劫匪聚集打劫良善村民 |
| 吕卡隆事变 | 10 | Cult,Role,King | war | 无官方锚 | 1 | 吕卡隆事变中阿雷尼科斯皇帝在皇宫遇刺身亡，维基亚卫队未能阻止，凶手至今成谜。 |
| 潘德拉克战役 | 31 | Cult,King,Iden,Role | war | A文本提及 | 1 | 阿雷尼科斯在潘德拉克战役被德洛修斯闲置，战后突围保骨血 |
| 瓦兰迪亚南北分裂 | 27 | Cult,Sett,Role | war | A文本提及 | 9 | 瓦兰迪亚南北矛盾：北方骑士与南方商人 |

## 六、家族/部族（8）

| 条目 | 变体 | When 维度 | 五域 | 官方交叉 | aliases | 摘要头 |
|---|---|---|---|---|---|---|
| 巴努·卡拉兹 | 7 | King,Cult,Role,Sett | politics | Ⅰ锚定 | 2 | 巴努·卡拉兹家族原为帝国卡拉修斯家族，五十年前与沙漠部落联姻融入阿塞莱 |
| 巴旦尼亚八部落的权力格局 | 3 | Cult,Role | politics | A文本提及 | 10 | 巴旦尼亚由八个部落组成，卡拉多格的王权建立在各部落信仰、恐惧和利益的复杂平衡之上 |
| 科穆诺斯家的兵营王朝 | 3 | Cult,King,Role | politics | Ⅰ锚定 | 7 | 科穆诺斯家族不是靠血统或元老院坐稳皇位的——是老兵们把加里俄斯推上来的，这份恩情 |
| 巴努·吉勒德 | 2 | Cult,Role | war | Ⅰ锚定 | 1 | 巴努·吉勒德部族起源与凶狠风格。 |
| 巴努·萨兰 | 8 | Cult,Role,King | war | Ⅰ锚定 | 1 | 巴努·萨兰曾被逐入沙漠百年，归来后占据原属胡勒延的东部边境 |
| 巴旦尼亚的部族裂痕 | 3 | Cult,Role,King | war | Ⅰ锚定 | 6 | 巴旦尼亚的团结是借来的——靠卡拉多格的战功、外敌的压力和头盖骨碗堆出来的面子。 |
| 斯特吉亚阶级与家族本位 | 3 | Role | war | A文本提及 | 6 | 大公值得尊敬但家族最重要。族长们常听调不听宣去搜刮美女。性被当成取暖和配种的实用 |
| 狼皮部落 | 16 | Cult,Role | war | A文本提及 | 4 | 狼皮部落遵循野人化传统，不受人类法律约束。 |

## 七、物产/经济（27）

| 条目 | 变体 | When 维度 | 五域 | 官方交叉 | aliases | 摘要头 |
|---|---|---|---|---|---|---|
| 包铁弩 | 3 | Cult | culture | A文本提及 | 1 | 包铁弩是狙击弩手的标配吗？ |
| 喀拉罕马 | 1 | — | culture | A文本提及 | 1 | 喀拉罕马是库赛特的高级军马 |
| 巴旦尼亚纯种马 | 1 | — | culture | A文本提及 | 1 | 巴旦尼亚纯种马因自愈能力而价值连城。 |
| 帕尔马廷马 | 1 | — | culture | A文本提及 | 1 | 帕尔马廷马是帝国的高级军马吗？ |
| 蒂亚尔马 | 1 | — | culture | A文本提及 | 1 | 蒂亚尔马产于斯特吉亚，品质优良 |
| 贵族武器 | 1 | — | culture | 无官方锚 | 2 | 贵族武器价格高昂 |
| 达西马 | 1 | — | culture | A文本提及 | 1 | 达西马是阿塞莱的制式军马 |
| 阿斯凯尔马 | 1 | — | culture | A文本提及 | 1 | 阿斯凯尔马的背景或来源。 |
| 阿萨利格马 | 1 | — | culture | A文本提及 | 1 | 阿萨利格马是库赛特顶级马，产自阿萨利格马场。 |
| 雷维尔越野马 | 1 | — | culture | A文本提及 | 1 | 雷维尔越野马是斯特吉亚的优良军马 |
| 马雷汶 | 4 | Cult,Role | culture | Ⅰ锚定 | 1 | 马雷汶是奥克斯·霍尔附近以伐木为生的村庄 |
| 响马 | 1 | — | economy | A文本提及 | 1 | 响马团行踪不定，极难抓捕的强盗团伙。 |
| 奥莫尔快步马 | 1 | Role | economy | A文本提及 | 1 | 奥莫尔快步马产自斯特吉亚奥莫尔地区，体型结实、耐力出众，适应北地寒冷与崎岖地形， |
| 纯血马 | 13 | Cult,Role | economy | A文本提及 | 3 | 纯血马是竞技大会奖品，也可从马商处高价购得。 |
| “铁臂”奥斯里克 | 5 | Cult,Role | politics | A文本提及 | 3 | 奥斯里克是瓦兰迪亚开国君主，夺取帝国西部土地。 |
| 侯森·富勒格骆驼 | 1 | — | politics | A文本提及 | 1 | 侯森·富勒格骆驼是阿塞莱商人驯化的极品骑乘骆驼 |
| 商队 | 2 | Role | politics | A文本提及 | 1 | 商队是城镇之间长途运输交易品的手段 |
| 瓦达尔热血马 | 18 | Cult,Role | politics | Ⅰ锚定 | 4 | 瓦达尔热血马的性能特点是什么？ |
| 狄亚特马 | 1 | Role | war | Ⅰ锚定 | 2 | 帝国将军在德律亚神殿废墟上建狄亚特马 |
| 狄亚特马之围 | 2 | Sett | war | A文本提及 | 2 | 德律亚人是帕拉诸部中最北边的部族，最后消失于帝国 |
| 王家竞技马 | 15 | Cult,Role | war | A文本提及 | 3 | 王家竞技马是瓦兰迪亚国王和显赫家族专属 |
| 科西亚马 | 15 | Cult,Role | war | A文本提及 | 3 | 科西亚马是帝国贵族常见坐骑，体格沉重冲击力强。 |
| 竞技马 | 15 | Cult,Role | war | A文本提及 | 4 | 瓦兰迪亚竞技马是方旗骑士标准坐骑，冲击力惊人。 |
| 纳哈撒维马 | 13 | Cult,Role | war | A文本提及 | 3 | 纳哈撒维马：阿塞莱沙漠战马，耐力极佳适应酷热 |
| 贾尔马律斯 | 8 | Sett,King,Role,Cult | war | Ⅰ锚定 | 2 | 贾尔马律斯是西帝国谷仓，也是军团兵源地，加里俄斯皇帝故乡。 |
| 阿丝葛莎 | 7 | Cult,King,Role | war | Ⅰ锚定 | 1 | 阿丝葛莎是贝恩兰的雅尔，奥特尔家族的盾女。 |
| 马穆鲁克 | 8 | Cult,Role,Iden | war | A文本提及 | 1 | 马穆鲁克是阿塞莱自幼购入并严格训练的军事奴隶。 |

## 八、人物/概念（189）——面最大，人工逐条裁

| 条目 | 变体 | When 维度 | 五域 | 官方交叉 | aliases | 摘要头 |
|---|---|---|---|---|---|---|
| 云梯 | 2 | — | culture | 无官方锚 | 1 | 云梯是用于攻城的大型梯子。 |
| 伊勒坦人 | 2 | — | culture | A文本提及 | 1 | 伊勒坦人是库赛特人的近亲 |
| 伊勒坦运输线 | 3 | Cult,Role | culture | A文本提及 | 1 | 伊勒坦运输线是帝国北方防务的粮食生命线 |
| 俄耳特拉 | 4 | Sett,King | culture | Ⅰ锚定 | 1 | 俄耳特拉由帝国将领命名，为骑兵提供马匹。 |
| 俘虏 | 1 | — | culture | A文本提及 | 1 | 卡拉迪亚俘虏处理规矩：一般不处决。 |
| 兀尔浑 | 2 | Cult | culture | Ⅰ锚定 | 2 | 兀尔浑统一库赛特并吞并帝国东方行省 |
| 具装骑兵 | 3 | Iden | culture | A文本提及 | 1 | 具装骑兵是帝国大杀器，人马具装。 |
| 卡南克 | 4 | Cult,Role | culture | Ⅰ锚定 | 1 | 卡南克猪肉因林中放养和橡子栗子而闻名。 |
| 卡尔索斯 | 2 | — | culture | A文本提及 | 2 | 贾尔马律斯将军卡尔索斯政变 |
| 卡拉迪亚大陆 | 1 | — | culture | A文本提及 | 1 | 卡拉迪亚大陆是卡拉迪亚的主体部分 |
| 卡琉斯 | 4 | Cult,Role | culture | Ⅰ锚定 | 1 | 卡琉斯村庄多雾湿润，村民口音独特难懂 |
| 埃什乌拉 | 1 | Cult | culture | A文本提及 | 2 | 传说巴努·阿提吉传奇女王埃什乌拉，智勇双全，击败扰民神明，囚于加西拉，以八芒星镇 |
| 塔利维尔 | 4 | Cult,Role | culture | Ⅰ锚定 | 2 | 塔利维尔橄榄油味道温和，瓦兰迪亚贵族喜爱。 |
| 夏尔 | 1 | Sett | culture | 无官方锚 | 1 | 夏尔是一位脾气暴躁、吝啬的祖先，因躲避血仇从海边逃至内陆。六十年来，他默默在附近 |
| 奥巴斯 | 1 | Cult | culture | A文本提及 | 1 | 奥巴斯是山上一位已故的斯特吉亚酋长，“奥巴斯之屋”（罗多巴斯）便是以他命名。该村 |
| 奥曼法德 | 4 | Cult,Role | culture | Ⅰ锚定 | 1 | 奥曼法德村位于奥曼法德堡附近，奥曼特尔山脚下隘口旁 |
| 德拉庞 | 4 | Cult,Role | culture | Ⅰ锚定 | 2 | 德拉庞是法尔海角的重要航海村落，村民捕鲸。 |
| 德里亚特 | 4 | Cult,Role | culture | Ⅰ锚定 | 2 | 德里亚特村庄位于瓦尔切格湾，产出毛皮油脂。 |
| 托加 | 1 | — | culture | A文本提及 | 1 | 托加是帝国贵族的衣服 |
| 投石车 | 3 | Role | culture | A文本提及 | 1 | 投石车基于扭力蓄能原理抛射装置 |
| 拉尔纳克 | 4 | Cult,Role | culture | Ⅰ锚定 | 2 | 拉尔纳克村庄位于特朗河谷下游 |
| 拉科尼亚人 | 1 | — | culture | A文本提及 | 1 | 拉科尼亚人原为帕拉人一支，投靠帝国同化 |
| 斯特吉亚亲卫骑兵 | 8 | Cult,Role,King,Iden | culture | A文本提及 | 1 | 斯特吉亚亲卫骑兵是瓦良格步兵晋升的顶点，是波耶最倚重的精锐力量 |
| 格吕 | 1 | Cult | culture | 无官方锚 | 1 | 格吕是雅尔阿加尔的乡绅战士 |
| 梅罗克 | 4 | Cult,Role | culture | Ⅰ锚定 | 2 | 梅罗克渔民擅捕金枪鱼沙丁鱼，腌制沙丁鱼闻名。 |
| 波耶 | 1 | — | culture | A文本提及 | 2 | 波耶是斯特吉亚部落酋长的尊称 |
| 火焰弩砲 | 3 | Role | culture | A文本提及 | 2 | 火焰弩炮低成本改装，用于制造火灾恐慌。 |
| 瓦兰迪亚地理志 | 2 | — | culture | 无官方锚 | 4 | 瓦兰迪亚地理分区：罗瓦尔山地、斯瓦迪亚丘陵、罗多克谷地。 |
| 瓦尼人 | 1 | Cult | culture | Ⅰ锚定 | 2 | 瓦尼人影响阿莱巴特口音 |
| 瓦朗比 | 4 | Cult,Role | culture | Ⅰ锚定 | 1 | 瓦朗比的马耐力好蹄子硬，是瓦兰迪亚军队马源。 |
| 瓦良格 | 2 | Cult | culture | A文本提及 | 1 | 瓦良格是斯特吉亚勇士的统称 |
| 纳尔 | 1 | Cult | culture | A文本提及 | 2 | 格纳特·纳尔村传说是纳尔头颅所在 |
| 罗多克 | 1 | — | culture | A文本提及 | 3 | 罗多克是瓦兰迪亚的南部土地。 |
| 西大洋 | 2 | — | culture | A文本提及 | 2 | 西大洋又称比斯坎海，位于卡拉迪亚大陆西岸 |
| 贝恩兰人 | 1 | — | culture | 无官方锚 | 1 | 贝恩兰人是贝恩兰岛上的原住民 |
| 达西人 | 2 | King | culture | A文本提及 | 1 | 达西人社会组织传统是什么？ |
| 那颜 | 1 | — | culture | A文本提及 | 3 | 那颜是库塞特各部酋长的称呼 |
| 部尔纳 | 4 | Cult,Role | culture | 无官方锚 | 1 | 橡果喂养的部尔纳猪肉质特点。 |
| 配种式抛石机 | 3 | Role | culture | A文本提及 | 1 | 配重式抛石机攻城：精度高，可重复砸塌城墙。 |
| 阿利斯维斯特 | 4 | Cult,Role | culture | Ⅰ锚定 | 2 | 阿利斯维斯特是罗瓦尔重要粮食产地。 |
| 阿洛斯唐 | 4 | Cult,Role | culture | Ⅰ锚定 | 2 | 阿洛斯唐村庄位于比斯坎山脚巨岩，俯瞰海洋。 |
| 雅尔 | 1 | — | culture | A文本提及 | 1 | 诺德酋长 |
| 韦雷克桑 | 4 | Cult,Role | culture | Ⅰ锚定 | 1 | 韦雷克桑村庄利用奥克斯湖水种植小麦，供应城堡。 |
| 于桑克 | 4 | Cult,Role | economy | Ⅰ锚定 | 1 | 于桑克村位于沙拉斯湾小海湾，遍布橄榄园。 |
| 元老 | 3 | King,Role | economy | A文本提及 | 1 | 元老是共和制基础，带领开发北方土地。 |
| 单位 | 1 | — | economy | A文本提及 | 4 | 谷物、肉、黄油、鱼的计量单位是什么？ |
| 坎尼人 | 2 | King,Role,Sett | economy | A文本提及 | 2 | 坎尼人是帕拉人分支，曾建立主导西海与珀拉斯海贸易的商业共和国，主要居住在俄尔堤西 |
| 坎尼王国 | 2 | King,Sett,Role,Cult | economy | 无官方锚 | 3 | 坎尼人曾建商业共和国于西帝国与阿塞莱交界，后被双方瓜分，虽居故土但已失话事权。 |
| 奥尔斯热 | 4 | Cult,Role | economy | Ⅰ锚定 | 2 | 奥尔斯热马匹与战士奥尔萨的传说。 |
| 帕利松 | 4 | Cult,Role | economy | Ⅰ锚定 | 2 | 帕利松土壤贫瘠，独特气候适宜葡萄生长。 |
| 帕迪沙阿国 | 1 | King | economy | A文本提及 | 2 | 帕迪沙阿国人民自称达西人，盛产香料 |
| 弗雷吉昂 | 4 | Cult,Role | economy | Ⅰ锚定 | 1 | 弗雷吉昂位于帕拉汶德北部平原，是气候分界点。 |
| 弩砲 | 3 | Role | economy | A文本提及 | 2 | 弩砲箭矢能穿透多人，但射程有限无法摧毁冲车。 |
| 拉格 | 1 | Cult | economy | A文本提及 | 2 | 拉格后裔捕鲸于瓦尔维克与瓦兰迪亚海峡 |
| 瓦兰迪亚风土人情 | 3 | Cult | economy | 无官方锚 | 2 | 瓦兰迪亚人是否被视为蛮子且不讲信誉？ |
| 翁加尔 | 4 | Cult,Role | economy | Ⅰ锚定 | 2 | 翁加尔曾是帝国腹地，现为瓦兰迪亚中心。 |
| 莫特 | 4 | Cult,Role | economy | Ⅰ锚定 | 2 | 杰屈朗诗人常去莫特，欣赏虞美人花海景色。 |
| 菲尔贝克 | 4 | Cult,Role | economy | Ⅰ锚定 | 2 | 菲尔贝克村庄位于比斯坎海岸巨岩，盛产葡萄酒。 |
| 西岚达克 | 4 | Cult,Role | economy | Ⅰ锚定 | 2 | 西岚达克村庄位于埃博半岛高山脚下 |
| 诺格伦 | 4 | Cult,Role | economy | Ⅰ锚定 | 1 | 诺格伦羊毛是加伦最好的，羊肉带有海风清香。 |
| 费顿 | 4 | Cult,Role | economy | Ⅰ锚定 | 2 | 费顿橄榄园在特朗河畔，近比斯坎丘陵山口。 |
| 阿兰塔斯 | 4 | Cult,Role | economy | Ⅰ锚定 | 1 | 阿兰塔斯村庄位于伊博半岛，以铁矿资源为经济支柱。 |
| 阿罗曼克 | 4 | Cult,Role | economy | Ⅰ锚定 | 2 | 阿罗曼克以橄榄树和香草芬芳著称。 |
| 鲁兰德 | 4 | Cult,Role | economy | Ⅰ锚定 | 1 | 鲁兰德村庄位于特朗林谷，靠近帕拉汶德，以橡子喂猪。 |
| “半耳”圭卡 | 3 | Role,Cult,King | politics | 无官方锚 | 1 | 哈尔达尔为何忌惮“半耳”圭卡？ |
| “金发”哈尔达尔 | 4 | Cult,Role,King | politics | 无官方锚 | 2 | 哈尔达尔国王统治不甘愿的土地。 |
| 伊丽卡 | 2 | Cult | politics | Ⅰ锚定 | 2 | 伊丽卡确保儿子哈尔达尔继位 |
| 伊拉 | 12 | Cult,Iden,King,Role | politics | Ⅰ锚定 | 1 | 伊拉·珀特洛斯是南帝国公主，热衷竞技场角斗。 |
| 伯爵 | 1 | — | politics | A文本提及 | 1 | 巴旦尼亚伯爵是部落酋长的尊称。 |
| 俄斯提科斯 | 4 | King,Role | politics | Ⅰ锚定 | 1 | 俄斯提科斯是北帝国最古老的家族 |
| 俄洛斯 | 7 | Cult,King,Role,Iden | politics | Ⅰ锚定 | 2 | 俄洛斯的战略决策反复无常且不与人商议 |
| 兀尔浑乃特 | 3 | King,Role,Iden | politics | Ⅰ锚定 | 2 | 兀尔浑乃特是库赛特统治家族，由兀尔浑统一草原各部。 |
| 元老院 | 3 | King,Role | politics | A文本提及 | 1 | 元老院选举皇帝并使其听命于元老院。 |
| 克洛托耳 | 3 | King,Role | politics | Ⅰ锚定 | 3 | 克洛托耳元帅战无不胜，效忠西帝国。 |
| 农奴 | 4 | Cult,Role | politics | A文本提及 | 2 | 瓦兰迪亚农奴无法离开土地，是贵族奴隶 |
| 加里俄斯 | 4 | King,Role | politics | Ⅰ锚定 | 6 | 加里俄斯是西帝国皇帝，军队最高统帅。 |
| 勒芒塔尔 | 4 | Cult,Role | politics | Ⅰ锚定 | 2 | 勒芒塔尔的毛皮在瓦兰迪亚很有名 |
| 北帝国的政治 | 3 | King,Role | politics | 无官方锚 | 4 | 北帝国政治为贵族元老院共和制 |
| 南帝国 | 5 | King,Role,Cult | politics | A文本提及 | 5 | 南帝国由女皇拉盖娅统治卡拉德南部领土 |
| 南帝国的政治 | 3 | Role,King | politics | 无官方锚 | 3 | 南帝国政治以世袭君主统治和世官制为核心 |
| 卢孔 | 3 | King,Role | politics | Ⅰ锚定 | 2 | 卢孔被北帝国元老视为领袖榜样。 |
| 厄尔辛 | 8 | Cult,Role | politics | 无官方锚 | 3 | 诺德猎人向厄尔辛祈祷，求丰厚猎物与精准箭矢。 |
| 厄庇斐里娅 | 2 | Role | politics | Ⅰ锚定 | 1 | 厄庇斐里娅是北帝国保守派女将军 |
| 可汗 | 1 | — | politics | A文本提及 | 1 | 可汗是库赛特汗国最高统治者头衔 |
| 吕西卡 | 2 | Cult,King | politics | Ⅰ锚定 | 2 | 吕西卡是克洛托耳元帅之妻兼女将军 |
| 哈卡 | 6 | Cult | politics | 无官方锚 | 1 | 哈卡许斯至今仍是诺德最偏远、最独立的定居点，居民不承认任何国王和雅尔的权威，靠捕 |
| 国王 | 1 | — | politics | A文本提及 | 1 | 国王是瓦兰迪亚与诺德的统治者头衔。 |
| 图兰 | 1 | Cult | politics | A文本提及 | 2 | 图兰王是阿塞莱传说中达西族的残暴统治者 |
| 奥奇莱甘和格拉尼斯决斗 | 2 | Cult | politics | A文本提及 | 3 | 吟游诗人流传的英雄故事，巴丹尼亚人人皆知，奥奇莱甘与暴君格拉尼斯的七子决斗。 |
| 小奥列克 | 3 | Cult,Role | politics | 无官方锚 | 2 | 小奥列克家族不接受君主制，坚信波耶才是土地主人。 |
| 库洛夫 | 6 | Cult,King,Role | politics | Ⅰ锚定 | 1 | 库洛夫家族是斯特吉亚古老的波耶世系，不服王公权威 |
| 库赛特的三角火药桶 | 4 | Cult,King,Role | politics | 无官方锚 | 6 | 库赛特是一个三角火药桶——乌尔浑汗家、阿契特旧宗主、库吉特残族三方互相积怨，汗廷 |
| 德泰尔 | 8 | Cult,King,Iden,Role | politics | Ⅰ锚定 | 1 | 德泰尔国王的软弱性格与仲裁执政方式 |
| 悲伤的肖农 | 7 | Cult,Sett,Role,King,Iden | politics | Ⅰ锚定 | 3 | 肖农城是巴旦尼亚的悲伤之城。 |
| 戴·阿罗曼克 | 11 | Cult,Role | politics | Ⅰ锚定 | 3 | 戴·阿罗曼克家族祖先私掠船俘虏沃提俄斯皇帝 |
| 拉革塔的三方拉锯 | 4 | Cult,King | politics | Ⅰ锚定 | 5 | 拉革塔是卡拉迪亚换手最频繁的城市——西帝国、巴旦尼亚、瓦兰迪亚三家在这里拉锯了几 |
| 斯特吉亚的文化 | 11 | Cult,Role | politics | 无官方锚 | 1 | 斯特吉亚建筑以厚实原木和陡峭屋顶为主，饮食包含黑面包、腌鱼、炖肉与烈酒。 |
| 斯特吉亚风土人情 | 14 | Cult,King,Role | politics | 无官方锚 | 1 | 斯特吉亚冬季严寒，居民在木屋喝格瓦斯与烈酒，婚丧嫁娶皆有歌声相伴。 |
| 斯瓦迪亚 | 2 | Role | politics | 无官方锚 | 2 | 瓦兰迪亚鹰派男爵的封地多在斯瓦迪亚。 |
| 斯瓦迪亚丘陵 | 1 | — | politics | 无官方锚 | 2 | 斯瓦迪亚丘陵包含哪些据点？ |
| 方旗骑士 | 8 | Cult,Role,Iden | politics | A文本提及 | 1 | 方旗骑士装备昂贵，重甲骑兵冲锋威慑力强。 |
| 求爱与婚姻 | 1 | — | politics | A文本提及 | 6 | 卡拉迪亚婚姻多由家庭安排 |
| 沙拉斯 | 7 | Cult,King,Iden,Role | politics | Ⅰ锚定 | 2 | 沙拉斯是戴·科尔坦家族控制的瓦兰迪亚港口城市，被称为龙兴之地 |
| 法戎 | 12 | Cult,King,Role,Sett,Iden | politics | Ⅰ锚定 | 1 | 法戎·列奥尼帕得斯是南帝国执政官，内政商贸才能出众 |
| 泰伊斯 | 10 | Cult,King,Iden,Role,Sett | politics | Ⅰ锚定 | 1 | 泰伊斯是吉勒德族长和阿塞莱强硬主战派 |
| 泽翁娜 | 2 | Cult,Sett | politics | A文本提及 | 3 | 泽翁娜以太后摄政，从未加冕但掌握实权 |
| 火焰投石车 | 3 | Role | politics | A文本提及 | 2 | 火焰投石车开花弹昂贵且制材特殊。 |
| 珀特洛斯家的床笫政治 | 4 | Cult,King,Role | politics | Ⅰ锚定 | 9 | 珀特洛斯家的政治有一半发生在吕卡隆的寝宫里——拉盖娅的床笫、伊拉的婚姻、帕堤耳的 |
| 瓦兰迪亚的南北受制 | 4 | Cult,Role,King | politics | 无官方锚 | 3 | 瓦兰迪亚表面稳固，实则德泰尔、因加泰尔与阿尔德里克互相牵制——两条裂谷同时从南北 |
| 男爵 | 14 | Cult,Role | politics | A文本提及 | 3 | 瓦兰迪亚男爵头衔统一，不分大小贵族，实力差异大。 |
| 芬·格鲁芬多克 | 8 | Cult,Role,King,Iden | politics | Ⅰ锚定 | 1 | 格鲁芬多克家族靠战功崛起，祖上无血统。 |
| 萨万特 | 4 | Cult,Role | politics | Ⅰ锚定 | 1 | 萨万特村庄位于沙拉斯湾北边草原，是瓦兰迪亚优质马场。 |
| 萨涅俄帕 | 4 | Cult,Role | politics | Ⅰ锚定 | 2 | 帝国一统后，萨涅俄帕迁回吕卡隆的争议 |
| 蒙楚格 | 9 | Cult,King,Role | politics | Ⅰ锚定 | 1 | 蒙楚格可汗以仲裁和分寸维持各部平衡 |
| 诺德风土人情 | 5 | Cult,Role,King | politics | 无官方锚 | 1 | 诺德人靠船斧生存，风土严酷不怕死 |
| 贡达尔 | 3 | Cult,Role | politics | A文本提及 | 1 | 贡达尔建立了贡达罗夫王朝。 |
| 贾沃勒 | 8 | Iden,King,Role,Cult | politics | A文本提及 | 2 | 贾瓦勒是纳哈撒沙漠的游牧联盟，向商队征收保护费。 |
| 阿契特 | 12 | Cult,King,Iden,Sett,Role | politics | Ⅰ锚定 | 1 | 阿契特部是古老的库赛特部族，曾被兀儿浑击败。 |
| 阿庇斯 | 8 | Cult,King,Iden,Role,Sett | politics | Ⅰ锚定 | 2 | 关于阿庇斯的信息 |
| 阿赫哈克王 | 2 | Cult | politics | A文本提及 | 1 | 阿赫哈克王双肩缠绕的蝮蛇有何特征？ |
| “饿人”沃尔比约恩 | 4 | Cult,Role,King | war | Ⅰ锚定 | 1 | “饿人”沃尔比约恩建立了诺德维格王国。 |
| 北帝国 | 5 | King,Role | war | A文本提及 | 5 | 北帝国是元老院与卡拉德人民的集合体。 |
| 北帝国的军事实力 | 4 | King,Role | war | 无官方锚 | 1 | 北帝国纸面实力强，但进攻能力受限 |
| 卡利奥克 | 4 | Cult,Role | war | Ⅰ锚定 | 1 | 卡利奥克是萨哥特附近村庄，位于纳尔谷。 |
| 可汗亲卫 | 10 | Cult,Role | war | 无官方锚 | 1 | 可汗亲卫是库赛特汗国最顶尖的精锐骑射手，担任可汗护卫，擅长在全速奔驰中精准射击。 |
| 吕卡里亚 | 8 | Cult,Role | war | A文本提及 | 3 | 吕卡里亚平原由塞堤斯河冲积形成，是帝国粮产与坎特里翁军马繁育地 |
| 喀拉库吉特 | 4 | Role,King | war | A文本提及 | 2 | 喀拉库吉特是库赛特唯一不定居的游牧部落。 |
| 因加泰尔 | 11 | Cult,Role | war | Ⅰ锚定 | 2 | 因加泰尔是戴·科尔坦家族族长，瓦兰迪亚最忌惮男爵。 |
| 坎尼人的王国 | 8 | Cult,Role | war | Ⅰ锚定 | 3 | 阿塞莱人如何导致坎尼人的王国消亡？ |
| 埃蒂尔菲德 | 4 | Cult,Role | war | Ⅰ锚定 | 2 | 埃蒂尔菲德位于巴旦尼亚边界，靠近萨哥特，种植小麦。 |
| 墨斯特里卡洛斯 | 2 | Sett,King | war | Ⅰ锚定 | 1 | 墨斯特里卡洛斯家族，南帝国贵族，领地以沃斯特鲁姆为中心。重视荣誉与帝国法统，内战 |
| 大公 | 2 | King,Cult,Role | war | A文本提及 | 1 | 斯特吉亚大公由贵族选举产生，非世袭。名义统御广袤领土，实权受制于家族平衡。需应对 |
| 巴旦尼亚地理 | 6 | Cult | war | 无官方锚 | 1 | 巴旦尼亚位于卡拉迪亚大陆西北的乌卡利昂高原之上，东接斯特吉亚，南临帝国故地，西邻 |
| 巴旦尼亚政治制度 | 11 | Cult,Role | war | 无官方锚 | 1 | 巴旦尼亚政治制度以至高王为名义上的最高统治者，由各部落推举并在圣山由德鲁伊见证加 |
| 帝国 | 12 | Cult,Role | war | A文本提及 | 4 | 卡拉迪亚帝国因阿雷尼科斯之死分裂 |
| 帝国政治制度 | 2 | Cult,Role | war | 无官方锚 | 1 | 帝国分裂原因：皇位继承规则缺失与权力角力 |
| 帝国风土人情 | 6 | Cult,Role | war | 无官方锚 | 1 | 帝国风土人情：卡拉德与帕拉文化交融，城市生活中心。 |
| 库赛特军事力量 | 11 | Cult,Role,Iden | war | 无官方锚 | 1 | 库赛特军队怯薛达尔罕牧民征召游射克制帝国 |
| 库赛特地理 | 7 | Cult,Role | war | 无官方锚 | 1 | 库赛特南部草原水草丰美适宜游牧 |
| 库赛特政治制度 | 4 | Cult,Role | war | 无官方锚 | 1 | 库赛特政治制度的核心，就是可汗和部落在集权与分权之间来回拉扯 |
| 库赛特风土人情 | 6 | Cult,Sett,Role,King | war | 无官方锚 | 1 | 游牧库赛特人住黑羊毛帐篷精于骑马射箭 |
| 弩 | 13 | Cult,Role,King | war | 无官方锚 | 1 | 弩是什么？ |
| 彭同 | 9 | Cult,King,Role,Iden | war | Ⅰ锚定 | 2 | 彭同管理萨涅俄帕并稳住了元老院 |
| 戴·提哈 | 8 | Cult,Role,King | war | Ⅰ锚定 | 1 | 戴·提哈祖先提哈是奥斯里克小儿子，获封帕拉汶德 |
| 戴·科尔坦 | 8 | Cult,King,Role | war | Ⅰ锚定 | 1 | 戴·科尔坦家在潘德拉克战役折损方旗骑士，事后责难国王调度 |
| 护盾兄弟会 | 1 | — | war | A文本提及 | 1 | 护盾兄弟会是主要由诺德人组成的雇佣兵团 |
| 拉盖娅 | 15 | Cult,King,Iden,Role | war | Ⅰ锚定 | 2 | 阿雷尼科斯死后拉盖娅举起血袍登位，驱逐元老院 |
| 斯特吉亚的世仇 | 4 | Cult,Role,King | war | Ⅰ锚定 | 6 | 斯特吉亚在潘德拉克后陷入了奥列克与朗瓦德两系的继承死斗，加上瓦尔拉的血债，两家的 |
| 斯特吉亚的军事制度 | 12 | Cult,Role,Iden | war | 无官方锚 | 1 | 斯特吉亚军事制度结合了波耶职业亲卫队与自由民动员，核心战术为重步兵盾墙。 |
| 暗影之子 | 5 | Cult,King,Role | war | A文本提及 | 2 | 巴努·奇拉勒又称暗影之子，是阿塞莱沙漠中从事暗杀和收保护费的贵族秘密组织。 |
| 朗瓦德 | 7 | Cult,King,Iden,Role | war | Ⅰ锚定 | 3 | 朗瓦德大公依靠护盾兄弟会监视酒馆言论 |
| 杰尔贾赖峭壁 | 4 | Cult,Role | war | A文本提及 | 1 | 杰尔贾赖峭壁是阿塞莱北方的天然屏障，阻挡帝国扩张 |
| 森民 | 5 | Cult,Role | war | A文本提及 | 1 | 森民是斯特吉亚林区游耕的瓦肯人后裔，刀耕火种不纳税 |
| 涅雷采斯 | 6 | Cult,King,Role | war | Ⅰ锚定 | 2 | 彭同·涅雷采斯放弃皇位，以萨涅俄帕为根基深耕元老院政治。 |
| 淮娅 | 9 | Cult,King,Iden,Role | war | Ⅰ锚定 | 1 | 淮娅是克洛托耳将军之女，代父镇守西北边境 |
| 温吉德 | 9 | Cult,King,Iden,Role | war | Ⅰ锚定 | 1 | 温吉德苏丹用商队和第纳尔赔偿了尼姆尔之死，未追究泰伊斯 |
| 火焰余烬 | 6 | Cult,Role,King | war | A文本提及 | 1 | 火焰余烬是帝国法外秩序，控制高利贷与调解纠纷。 |
| 狄俄尼科斯 | 6 | Cult,King,Role | war | Ⅰ锚定 | 1 | 狄俄尼科斯家族世代镇守西帝国西北边境拉革塔 |
| 瓦兰迪亚 | 6 | King,Role,Cult | war | A文本提及 | 4 | 瓦兰迪亚是卡拉迪亚最富裕易守难攻的国家。 |
| 瓦兰迪亚军事力量 | 11 | Cult,Role | war | 无官方锚 | 4 | 瓦兰迪亚军事力量强劲，依赖骑兵与弩手。 |
| 瓦兰迪亚政治制度 | 6 | Cult,Role | war | 无官方锚 | 4 | 瓦兰迪亚社会结构为国王、男爵、骑士、农奴金字塔。 |
| 瓦吉罗夫 | 10 | Cult,Role | war | Ⅰ锚定 | 2 | 瓦吉罗夫家族是斯特吉亚东大门，戈敦波耶守卫伊尔坦山。 |
| 秘密之手 | 14 | Cult,Role | war | A文本提及 | 4 | 秘密之手是南帝国最大犯罪组织，控制乡村城镇。 |
| 突剌格 | 12 | Cult,Role | war | Ⅰ锚定 | 2 | 突剌格精明谨慎，等待机会复仇西加。 |
| 竞技大会 | 20 | Cult,Role | war | A文本提及 | 4 | 竞技大会可赢取武器盔甲战马 |
| 罗德里 | 4 | Cult,Role | war | A文本提及 | 4 | 罗德里村庄因莱恩诺丘陵富铁红土壤得名。 |
| 老奥列克 | 11 | Cult,King,Iden,Role | war | Ⅰ锚定 | 2 | 老奥列克为斯特吉亚立下赫赫战功并担任波耶 |
| 苏丹 | 15 | Cult,Role | war | A文本提及 | 4 | 苏丹是沙漠共主，平衡部落利益，保护商路 |
| 萨拉庇俄斯 | 5 | Cult,King,Role,Sett | war | A文本提及 | 2 | 萨拉庇俄斯暴躁古怪，元老院称其天才与疯子之间 |
| 被弃者军团 | 7 | Iden,King,Role,Cult | war | A文本提及 | 2 | 被弃者军团纪律严明，战力可靠，为佣金而战。 |
| 西加 | 14 | Cult,King,Iden,Role | war | Ⅰ锚定 | 1 | 西加刺杀温都勒导致妹妹瓦尔拉被处决 |
| 西帝国 | 3 | King,Role | war | A文本提及 | 1 | 西帝国政治体制军事化，皇帝是最高军事指挥者 |
| 西帝国的政治 | 9 | Cult,King,Role | war | 无官方锚 | 2 | 西帝国政治决策链短，军令执行力在战时高效。 |
| 诺德 | 7 | Cult,King,Role,Iden | war | A文本提及 | 2 | 诺德人海上力量强大，能威胁沿海，但陆战经验不足且人口有限。 |
| 诺德军事力量 | 6 | Cult,Role | war | 无官方锚 | 1 | 诺德军队以盾墙战术为核心，双手斧战士近战爆发力惊人。 |
| 诺德皇家侍卫 | 15 | Cult,Role | war | A文本提及 | 4 | 诺德皇家侍卫是卡拉迪亚最强步兵之一 |
| 费奥纳 | 3 | Cult,Role | war | A文本提及 | 3 | 巴旦尼亚费奥纳大赛每年举行，包含多种比武。 |
| 达西 | 7 | Cult,King | war | A文本提及 | 3 | 达西是一个古老王国，曾同时与帝国东南边境和阿塞莱西境接壤，控制着通往东方的商路， |
| 达鲁索斯 | 6 | Cult,Role,Iden,King | war | A文本提及 | 1 | 达鲁索斯被废黜后下落不明，民间传说他将归来。 |
| 金冠 | 10 | Iden,Cult,Role,King | war | 无官方锚 | 2 | 瓦兰迪亚金冠是帝国皇帝赠予首任国王奥斯里克的礼物。 |
| 阿塞莱 | 7 | Cult,Role,Iden,King | war | A文本提及 | 2 | 阿塞莱苏丹国是松散部落联盟，苏丹是魅力仲裁者。 |
| 阿塞莱军事力量 | 4 | Cult,Role | war | 无官方锚 | 1 | 阿塞莱军事力量由贵族兵、平民兵、奴隶兵组成 |
| 阿塞莱地理 | 8 | Cult,Sett,Role,King | war | 无官方锚 | 1 | 纳哈撒沙漠和杰姆贾赖峭壁是阿塞莱的天然防御屏障。 |
| 阿塞莱政治制度 | 8 | Cult,King,Role | war | 无官方锚 | 1 | 阿塞莱政治制度是部族推选苏丹，各家族自治。 |
| 阿塞莱阶级与奴隶制 | 3 | Role | war | A文本提及 | 5 | 阿塞莱只有两种人：主人和奴隶。苏丹是所有主人的主人。男人的尺寸代表地位，女人身上 |
| 阿塞莱风土人情 | 10 | Cult,Sett,Role,King | war | 无官方锚 | 1 | 阿塞莱风土人情：商业繁荣因耕地稀少。 |
| 阿尔卡科斯 | 2 | Sett | war | A文本提及 | 2 | 阿尔卡科斯是沃斯特鲁姆城建立者，卡拉德佣兵出身。 |
| 阿尔德里克 | 14 | Cult,King,Iden,Role | war | Ⅰ锚定 | 1 | 阿尔德里克与德泰尔国王路线对立，寻求更多战争与征服。 |
| 黄金野猪兵团 | 3 | Role,Cult | war | A文本提及 | 1 | 黄金野猪兵团是瓦兰迪亚退伍士兵雇佣兵团。 |
