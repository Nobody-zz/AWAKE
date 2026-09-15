# 战帆 DLC（War Sails / NavalDLC）内容评估：要不要补进世界书

> **状态**：评估稿，待 Max 签收。日期 2026-09-12。
> **素材底本**：`C:\Users\26811\Downloads\std_*.xml`（21 份，English 底本 + 氏族 CN 对照 2 份），自游戏 DLC 导出。
> **一句话结论**：**要补，而且是大补**——战帆不是"加了船"，是加了一整个北方文化圈（诺德文化+王国+49 聚落+284 条聚落描述文），并且直接冲击我们三条已有判定。

## 一、内容量盘点（全量实测解析）

| 类别 | 量 | 说明 |
|---|---|---|
| **新文化** | 1 | **Nord（诺德）**——文化描述全文（`bLTkig9T`）：Byalic 海外的峡湾老家 Nordvyg、西迁 Beinland 岛、东进 Jumne 河黑森林；生计=海象象牙+鲸油+劫掠长船 |
| **新王国** | 1 | Nordvyg 王国（`m6ZgnpN9` 全文）：**「饿人」沃尔比约恩**在帝国 Vaegar 卫队当十年雇佣兵→带金归乡→血与收买统一北方，强制酋长称 jarl；今其子**「金发」哈尔达尔**在位，"在一片不愿为国的土地上为王" |
| **新英雄** | 5（全文） | 哈尔达尔（现任王，背父债）／**伊尔丽卡**（王太后，林中神龛守护者，"死了——王室这么说。月圆之夜有人在林里见她游荡"=现成 rumor 层素材）／**格里卡·半耳**（烧了自家大厅上位的 jarl）／**阿丝戈莎**（盾女，Beinland 女 jarl，亡夫死于与斯特吉亚冲突）／沃尔比约恩（"下令把自己和黄金一起活封进坟冢"） |
| **新氏族** | 9 | 官方 CN 齐：特罗斯尼尔／维斯迪尔／胡尔德／瓦罗弗／许尔夫／盖于特／奥特尔／伦格尼尔／肖尔德 |
| **新聚落** | **49**（9 城堡+40 城镇村庄） | 北方新地图：Ulikshorn／**Rovalatys**（埃博半岛银矿，诺德移民 outnumber 帝国人）／Fimbulgard／Hakarshus／Skarthness／Hvalvik／Gretysfjord／**Ostican**（全文 36 次，DLC 剧情母港）／Draugmyr／Nifdal… |
| **聚落描述文** | **284 条长文本** | 体量对齐本体的 descriptionText——等于北部地理风貌一次给齐 |
| **船** | 53 型（21 长描述） | snekkja／knarr／cog／liburna／dromon／**qalguk**（塔奈西斯湖船工造的草原可汗战船）／**dhow**（"坎尼克墓壁刻着一千年前的轮廓"）／birlinn（巴旦尼亚）…=经济+战争两域新料 |
| **兵种** | 102 名 | 诺德兵树全套：Ulfhedinn／Berserkir／Skjaldbrestir／Beinlandsk Fyrdman… |
| **领主名** | 53 | 北方风命名 |
| **对话** | NavalDLC.xml 154KB | DLC 剧情（Gunnar、Golden Wasp 号、"Take me to Ostican now"） |

## 二、对既有判定的冲击（3 条，2 条要改档）

1. **卡恰尔 V4 `cultures=['nord']` 改判**：昨晚判「非法值」是**版本相对的错**——基础库 v1.3.15 无 nord，DLC 里 nord 是正式文化。处置：变体本身仍不接入本批（「人的同伴」与官方「誓言之同伴」的名称冲突不变），但"nord 非法"的理由作废；将来接 `entity.culture.nord` 需先扩实体登记表。→ 实施稿 §2.4 已改。
2. **库吉特／合儿必特／喀拉库吉特三名钉死（Max 两次纠偏后回库+官方 CN 全语言行定案；我此前两连错全部收回）**：

   | 官方 CN | EN（stringId） | 游戏实体 | 身份 |
   |---|---|---|---|
   | 库赛特 | Khuzait（`sZLd6VHi`／Khuzait Khanate `bF7HmNGQ`） | 文化+汗国 | 文化/王国 |
   | **库吉特** | Khergit（`Q1nAHnV7`） | **clan_khuzait_2**（tier 4，家城查坎德，首领墨速宜/Mesui） | **汗国封臣家族**（isMinorFaction=0，非流亡），官方文「曾是联盟最大部族之一」 |
   | **合儿必特** | **Harfit**（`gXeF1yKn`） | **clan_khuzait_5**（tier 3） | **另一家汗国封臣家族**——不是 Khergit！繁中「哈爾菲特」/日「ハルフィット」/俄「Харфиты」铁证 |
   | 喀拉库吉特 | Karakhergit（`6BvGhzao`） | karakhuzaits（isMinorFaction=**1**） | 唯一小派系：「西部大草原最后的数支游牧部族之一」，拒兀儿浑乃特可汗的定居令 |

   与 wiki（Khuzait 页 vassal 名单）逐行吻合：Urkhunait（兀儿浑乃特， ruling）／Khergit（墨速宜）／Arkit／Tigrit／**Harfit**／Baltait／Koltit／Yanserit／Oburit + minor: Karakhergit。
   **影响判定**：dawn-stew 的 `khergit_wardens` aliases 用「**库吉特**」；「合儿必特」**不得**列为 khergit 同义词（它是 Harfit 的官方名，混挂会把两家官方家族搅在一起）。我曾两连错：先判「合儿必特=编年史私造」（错，它官方存在但属于 Harfit）、再判「合儿必特=Khergit 官方小派系名」（错，小派系只有喀拉库吉特；合儿必特是封臣家族且非 Khergit）。
3. **Vaegir/Vaegar 官方内部不一致**：基础游戏文本=**Vaegir** Guard（`7tCl75xJ`），DLC 王国文=**Vaegar** Guard（`m6ZgnpN9`）。官方自身两拼，写档从基础游戏 Vaegir（维吉亚），DLC 拼写如实记录。

## 三、编年史 B 级专名升 A 机会（待 CN 对照核）

战帆文本实测词频：**Byalic 16／Beinland 22／Nordvyg 15／Jumne 12／Tanaesis 13／Iltan(hindrun) 6／Ebor(半岛) 8／Kannic 5**。对照编年史 B 级专名：

| 编年史词 | DLC 候选 | 判定 |
|---|---|---|
| 比尔里海 | **Byalic Sea** | 高度疑似同水体（卡恰尔以北诺德人的海），**待官方 CN 对照后升 A** |
| 埃博半岛（德里亚特条） | **Ebor Peninsula**（Rovalatys 条） | 疑似同地，待 CN |
| 塔奈西斯湖 | **Lake Tanaesis**（qalguk 条） | 疑似同湖，待 CN |
| 塞堤斯河／坎特里翁军马／珀拉斯海／伊卡拉荒原／加隆托海峡 | — | 两库均零命中，维持 **B 级**（编年史自有） |

## 四、补充方案建议（按性价比排序）

1. **来源登记**：`source.calradia.game.warsails`（A 级，DLC 官方），locator=`std_*.xml#strings[id]`；本批 21 份 XML 复制入 `docs/worldbook-migration/sources/warsails/` 存档。基础库 v1.3.15 的 bannerlord.db **不含** DLC 内容——这批 XML 是当前唯一 A 级底本（后续可从游戏目录导更全的 settlements/spcultures XML）。
2. **catalog 扩容**：Nord 文化（Ⅱ 体系+Ⅲ 常识双形态）、Nordvyg 王国与四英雄（Ⅰ 锚定，等实体登记表扩容后挂 hero/clan）、北方 49 聚落（Ⅰ）→ 建议立**第二批「北方沿海批」**（与瓦尔切格/卡恰尔簇地理相接，编年史卡恰尔/瓦尔切格变体里"诺德人的海"全部有了官方落点）。
3. **湖鼠帮类现成素材**：官方小派系 CN 文（`std_spclans_xml-zho-CN.xml`）里整批好料——湖鼠帮（"假灯塔引船搁浅"，瓦尔切格湾风味）、护盾兄弟会（诺德人雇佣兵团，僧侣式戒律）、森民、狼皮部落、古拉姆、被弃者军团（含**阿雷尼科斯废常备军**官方表述）、火焰余烬（含**达鲁索斯被将军推翻**官方表述，十世纪危机 A 级旁证）——**全部 A 级**，可直接进骨架与第二三批。
4. **试点批不动**（范围已锁）；实体登记表扩容（49 聚落+9 氏族+53 领主+Nord 文化+王国）是第二批前置，涉及 persona-entity 管线，属跨线工作，先登记不实施。

## 五、遗留待办

- [ ] 官方 CN 全量对照（需 settlements/kingdoms/heroes 的 -zho-CN 导出；手头只有氏族 CN 两份）
- [ ] Ostican 剧情线细读（NavalDLC.xml 154KB 对话）
- [ ] 比尔里海/埃博半岛/塔奈西斯湖 的 CN 对照定案
- [ ] 实体登记表 v2 范围（跨线，等 Max 发话）
