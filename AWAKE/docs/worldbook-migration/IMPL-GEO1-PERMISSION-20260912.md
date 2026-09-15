# 实施稿：地理第一批（4 城）× 授权结构做真

> **状态**：独立只读审查已过（09-12，全部「必须改」已吸收进本稿）→ **待 Max 签收** → 实施 → 闭合复验 → 限定路径提交。本稿是 `PLAN-WORLDBOOK-GEO-BATCH-AND-PERMISSION-20260912.md` 的执行细化，五项裁定（09-12「照建议来」+「窄用法」）已全部生效，不再重复论证。
> **审查改定两条硬口径（审查员发现，已采纳）**：
> ① **不变式：每条 grant 的 `min_detail` 必须等于所在表达的 `layer`**。运行时没有"表达层 vs 身份上限"的自动闸（`SelectExpression` 只看查询方 RequestedDetail，不看身份能力）——低身份挂低 min_detail 却贴在高层表达上 = 内容越限且两引擎都放行。min_detail==layer 后，身份上限经 grant 匹配自动生效。
> ② **身份链以 profile-registry 的 `inherits` 为准**（noble→notable→commoner；`RuntimePackageCompiler.cs:26-37` + `WorldbookIdentityEvaluator.cs:183-186`），**不是**角色回退硬编码链（那条 noble 才只有自己）。此前"noble 链只有 noble"的说法只对"仅传 Role、无 IdentityId"的查询形态成立。

---

## 一、执行顺序（六步，一步一验）

| # | 步骤 | 产出 | 前置 |
|---|---|---|---|
| 0 | **deny 探针测试档**（§五）+ 首次真实编译 | 探针档编译产物 | 无 |
| 1 | 编译探针档 + 模拟器跑 deny/min_detail 实证 | 实测记录（读码结论↔实测对照） | 0 |
| 2 | **修老 12 档**：授权落上限 + 锚点迁移 + aliases + `corrections_20260912` 留痕 | 修订后的 authoring-out | 动手前 grep 哈希前 8 位确认不碰 golden |
| 3 | **写新档 10 份**（§三、§四） | authoring-out 新增 | 1 的实测确认 deny 行为 |
| 4 | 全量编译 + **越限自检脚本**（逐表达断言：①每条 grant ≤ 身份上限；②**不变式 grant.min_detail == 所在表达 layer**——审查发现旧定义查不出"低 min_detail 挂高层表达"的内容泄漏） | 编译产物 0 诊断 | 2、3 |
| 5 | **身份 × 档矩阵验收**（§六，**以运行时模拟器为准**） | 矩阵结果表 | 4 |

探针档放 `tools/worldbook-studio/workspace/authoring-test/`（gitignore 区），**不进** `authoring-out/` 正典库。

---

## 二、素材底账（A 级母本 + B 级编年史变体，均已实测提取）

### 2.1 新来源登记

既有 5 个 source 全是编年史（B 级）。本批官方文本登记新来源：

- `source_id: source.calradia.game.settlements`
- `source_version: bannerlord-1.3.15.110062`
- `source_content_hash`：取该城官方中文全文的 SHA256（大写；§2.2 表中只列前 8 位便于对照，实施写全 64 位）
- `locator` 约定：`bannerlord.db#settlements.<id>.descriptionText→localization[<desc sid>].CNs`
- `quote_hash` = SHA256(quote 原文, UTF-8) 大写。**算法已用既有档引文实测验证**（`沙拉斯湾是西大洋…半封闭海域` → `1C51B54DFDF09519…` 与 sara-bay.yaml 一致）。

### 2.2 四城关键 ID 与哈希（09-12 实测）

| 城镇 | settlementId | 锚点（登记表 exact_base） | owner clan | 文化 | 王国 | desc stringId | CN 全文 SHA256 前 8 |
|---|---|---|---|---|---|---|---|
| 沙拉斯 | `town_V7` | `entity.settlement.town_v7` | `Faction.clan_vlandia_4`（戴·科尔坦） | vlandia | vlandia | `0cZmKO76` | EDBA9220 |
| 瓦尔切格 | `town_S1` | `entity.settlement.town_s1` | `Faction.clan_sturgia_1`（贡达罗夫） | sturgia | sturgia | `i2VkRLBg` | 09BF1D33 |
| 侯森·富勒格 | `town_A2` | `entity.settlement.town_a2` | `Faction.clan_aserai_2`（巴努·萨兰） | aserai | aserai | `bnl7Va4c` | 29C6231D |
| 吕卡隆 | `town_ES4` | `entity.settlement.town_es4` | `Faction.clan_empire_south_1`（珀特洛斯） | empire | **empire_s** | `WGtzBswe` | 903B4156 |

- 维度取值格式已读码确认（`RuntimePackageCompiler.CanonicalConditionId` L258–281）：grant 里写 `entity.culture.vlandia` / `entity.kingdom.empire_s` / `entity.settlement.town_v7`，编译规范化为 `awake:<kind>:<code>`，**grant 维度不走 hero/clan/settlement 白名单**（白名单只管文档级 `entity_ids`，L245–256）。
- 四城锚点在登记表 890 实体中均为 `exact_base`，写法已核对（小写）。

### 2.3 官方中文全文（A 级引文母本，断言引文皆为其子串）

**沙拉斯（182 字）**：
> 这座如今被称为沙拉斯的城市，其历史最早可以追溯到第一批卡拉德殖民者登上这片大陆的海岸之时。在帝国如日中天的时候，其首都迁到了北方的巴拉维诺斯，而沙拉斯则成为了一座海务和贸易的重要港口，帝国贵族们在周边海湾附近的和煦海滩上纷纷建起自己的纳凉别墅，时不时地划着小艇在小岛的周边游弋。瓦兰迪亚人到来后，沙拉斯落入了无情的戴·科尔坦家族手中，其无穷的财富助长了后者的野心。

**瓦尔切格（218 字）**：
> 曾经是诺德人殖民地的瓦尔切格位于风雨交加的海崖上，此地正是昔日巴旦尼亚领土的卡恰尔半岛深处。建立这个沿海前哨的雅尔希望与内陆进行贸易，于是向巴旦尼亚至高王戴恩玛查宣誓效忠，但随后却开始涉足巴旦尼亚政治。恼怒的戴恩玛查雇佣了斯特吉亚军队来击败他不可靠的新封臣。斯特吉亚人奉命击溃了诺德人，但随后要求把新夺得的城镇作为他们的报酬。他们将这个城镇称为“瓦尔切格”，这是诺德词“誓言之同伴”的变体，以嘲讽两个因争斗而将战利品拱手让给他们的敌人。

**侯森·富勒格（221 字）**：
> 侯森·富勒格位于卡尔得亚的东南边缘，卡尔得亚是一片由河流和咸水潟湖构成的网络，它将帝国和阿塞莱的土地相连。富勒格，又名法耳科斯，是一位来自于极南之地的黑面孔雇佣兵酋长。他在此地建立了一座堡垒，又利用堡垒的战略位置发了财，从帝国和阿塞莱两边收取贿赂，并随时准备着选边站队。在玩了将近二十年的换队游戏之后，他驾着一艘满载黄金的船驶回了家，但许多阿塞莱的氏族仍然骄傲地自称是他的后裔。“富勒格的鬼点子”也成为了许多集市说书人口中经久不衰的故事桥段。

**吕卡隆（186 字）**：
> 吕卡隆这座天然要塞位于巍峨的奥尼石山向外突出的一处缓坡之上。在帝国征服的最后几年，帕拉部落同帝国协商，进行了和平的合邦，结果他们的家园又被叛军首领革图夺走。接踵而至的围城战仅仅是吕卡隆在接下来的几个世纪中屡遭围攻的第一次，因为该城周边有着丰富的银矿脉，任何想在内战中攫取权力的皇位觊觎者都会盯上这里。甚至有人传言，居住在石山顶上的秃鹫已经享用过卡拉迪亚大陆上的每一种民族。

**名称译名核对**：四城名均已按官方流程核对（英原文→stringId→CNs），与登记表 display_name_zh 一致，无考据转写风险。

### 2.4 编年史 B 源接入（09-12 晚增补，Max 裁定「A+B 双源」+「能用的用、坏的自整」）

**来源**：`source.calradia.chronicle.animusforge`（B 级主源，已裁定）。locator=`rules/<rule_*.json>#Variants[n].Content`。底本=`C:\Users\26811\Downloads\卡拉迪亚编年史.zip`（已验：CRC 全过、与游戏现役目录逐字节零差异、337 JSON 零解析失败）。quote_hash 算法同 §2.1。

**四城相关 10 条 rule 质检**（沙拉斯 7V／沙拉斯湾 3V／帕拉汶德 5V／吕卡隆 12V／吕卡隆事变 10V／吕卡里亚 8V／卡恰尔半岛 9V／侯森·富勒格骆驼 1V／德里亚特 4V／戴·科尔坦 8V，共 67 变体）：

| 判定 | 条目 | 处置 |
|---|---|---|
| ✅ 直接可用 | 沙拉斯全 7V、吕卡隆 V0–V10、卡恰尔 V0–V3/V5–V8、吕卡隆事变 V0–V8、帕拉汶德全 5V、戴·科尔坦全 8V、德里亚特全 4V、吕卡里亚 V0–V4/V6–V7、沙拉斯湾 V0–V1 | 按映射表（§3.5）进表达层 |
| ⚠️ 需改写再用 | **沙拉斯湾 V2**（「海洋学角度」「地质构造」「潮间带生态系统」——现当代科学视角，**违反内容硬规矩 2**）；吕卡隆 V11、吕卡隆事变 V9、吕卡里亚 V5（空 When 百科分析体，「从史料分析来看」腔） | 弃或重写为中世纪口吻后再用；百科体可作**中立内核候选**但须去腔 |
| ❌ 冲突/非法值 | 卡恰尔 **V4**：`Cultures=['nord']` 非官方文化值；「改名叫瓦尔切格，是‘**人的同伴**’」与官方「**誓言之同伴**（oathsworn companion）」冲突 | 该变体弃用；名称冲突以 A 为准（铁律） |

**编年史专名回官方库核对结果**（09-12 实测）：

| 编年史词 | 官方核对 | 结论 |
|---|---|---|
| 维吉亚卫队（编年史写「维基亚」） | ✅ `7tCl75xJ`/`DFW5i63X`「阿雷尼科斯麾下 **Vaegir Guard**」 | 官方 CN=维**吉**亚卫队；编年史用字漂移，写档从官方 |
| 卡恰尔半岛 | ✅ town_S1 描述文 EN=**Kachyar** peninsula | 官方拼写 Kachyar（非 Kachar），CN 卡恰尔 |
| 戴恩玛查（编年史 V5 写「戴恩玛」） | ✅ town_S1 EN=High King **Dernmachad** | 官方=戴恩玛查；编年史漂移，从 A |
| 奥尼石山 | ✅ town_ES4 EN=**Ornian** rock | 官方✅ |
| 塞堤斯河／坎特里翁军马／珀拉斯海／伊卡拉荒原／比尔里海／加隆托海峡 | ❌ localization_entries + descriptionText **两处均零命中** | **B 级专名**（编年史自有，官方无）；aliases 可挂、正文须按 B 级口径（与官方 A 不冲突处如实采用） |
| 因加泰尔 | ✅ `iaUQ9lt9`（前轮已核） | 官方✅ |

**范围裁定（防膨胀）**：本批 10 档只接**上表「直接可用」中与四城直接相关**的变体（§3.5 映射表）；帕拉汶德 5V、吕卡隆事变 10V、吕卡里亚 8V、戴·科尔坦 V1–V7、德里亚特 4V、富勒格骆驼 1V → 登记**第二步/第三步**素材池（帝国疆域史批、家族批、村庄与物产批），不在本批展开。侯森·富勒格**无编年史本体条目**（仅骆驼词条，且文风偏游戏说明书）→ **husn 两档本批保持纯 A 级**。

---

## 三、新档设计（4 簇 10 档）

### 3.1 档位总表

按 B4 六类不混 + 调取场景拆档；**留痕勘误（审查建议 5，采纳）**：`split_from` 是 `lifecycle` 子字段且要求 event_id+revision，**新档母本是官方文本、不是从既有档拆出，一律不写 split_from**——B4 依据记录在闭合复验与本稿；仅老档若发生真拆分才用。每簇断言 ≈6、每断言表达 ≤4。

| # | doc id | 标题 | domain | 断言 | 调取场景（B4 依据） |
|---|---|---|---|---|---|
| 1 | `doc.geography.charas-town` | 沙拉斯·城与港 | geography | 3 | 这城在哪、靠什么过活 |
| 2 | `doc.politics.charas-reign` | 沙拉斯·归属与科尔坦家 | politics | 2 | 这地如今归谁、怎么来的 |
| 3 | `doc.politics.charas-cortain-secret` | 沙拉斯·科尔坦家的账 | politics | 1 | **秘密档**（§3.3） |
| 4 | `doc.geography.varcheg-town` | 瓦尔切格·海崖与港 | geography | 2 | 同 1 |
| 5 | `doc.politics.varcheg-swap` | 瓦尔切格·三易其手与镇名 | politics | 3 | 这地怎么换的主人、名字哪来的 |
| 6 | `doc.geography.husn-fulq-town` | 侯森·富勒格·卡尔得亚边上的城 | geography | 3 | 同 1 |
| 7 | `doc.culture.husn-fulq-tales` | 富勒格其人与“鬼点子” | culture | 2 | 集市说书的故事 |
| 8 | `doc.geography.lycaron-town` | 吕卡隆·石山要塞 | geography | 3 | 同 1 |
| 9 | `doc.economy.lycaron-mines` | 吕卡隆·银矿 | economy | 2 | 银矿与兵祸的由来 |
| 10 | `doc.culture.lycaron-rock-tales` | 吕卡隆·秃鹫怪谈 | culture | 1 | 石山顶上的传言 |

断言→引文对应（quote 为 2.3 母本子串，实施时逐条算 hash）：

- **charas-town**：①「第一批卡拉德殖民者登上这片大陆的海岸」（位置/起源一句）；②「成为了一座海务和贸易的重要港口…建起自己的纳凉别墅」（海务贸易+贵族别墅）；③「海湾附近的和煦海滩…划着小艇在小岛的周边游弋」（海湾群岛地貌，与既有沙拉斯湾簇相邻互证）。
- **charas-reign**：①「瓦兰迪亚人到来后，沙拉斯落入了…戴·科尔坦家族手中」（归属沿革）；②「其无穷的财富助长了后者的野心」（财富与家族的关系，**事实面** detail 层；*解读面*归秘密档——见 3.3 文本差异化）。
- **charas-cortain-secret**：同引文②，但按"科尔坦家拿沙拉斯的财富养野心"作**秘密**口径分档（见 3.3 设计说明）。
- **varcheg-town**：①「位于风雨交加的海崖上…卡恰尔半岛深处」（海崖+半岛，顺带挂通卡恰尔簇）；②「曾经是诺德人殖民地…希望与内陆进行贸易」（港的生计由来）。（⚠ 审查建议 3，采纳：原③「击溃诺德人、要城镇作报酬」与归属沿革同属"怎么换的主人"场景，**移入 varcheg-swap**，与 lycaron 处理一致。B4 口径说明：「由来」类素材按**生计场景**并入城档（港哨/堡垒即生计引擎，同 husn ②③），**政权易手沿革**才归 politics 场景——与 lycaron 的处理是同一条线。）
- **varcheg-swap**：①「向巴旦尼亚至高王戴恩玛查宣誓效忠…雇佣了斯特吉亚军队…击溃了诺德人」（效忠→雇佣→击溃链）；②「要求把新夺得的城镇作为他们的报酬」（易手的达成）；③「称为“瓦尔切格”…诺德词“誓言之同伴”的变体，以嘲讽…」（镇名由来，rumor 层，与 T3 挂钩）。
- **husn-fulq-town**：①「位于卡尔得亚的东南边缘…河流和咸水潟湖构成的网络…将帝国和阿塞莱的土地相连」（卡尔得亚区位）；②「他在此地建立了一座堡垒」（堡垒起源）；③「从帝国和阿塞莱两边收取贿赂」（区位换财的生计逻辑，detail 层）。
- **husn-fulq-tales**：①「富勒格，又名法耳科斯…黑面孔雇佣兵酋长…满载黄金的船驶回了家」（其人其事）；②「“富勒格的鬼点子”…集市说书人口中经久不衰的故事桥段」（说书桥段，rumor 层）。
- **lycaron-town**：①「天然要塞位于巍峨的奥尼石山向外突出的一处缓坡之上」（地貌）；②「帕拉部落同帝国协商，进行了和平的合邦，结果他们的家园又被叛军首领革图夺走」（归属沿革，politics 场景→放 mines 或本档？→归本档 summary 层，见 grant 表）；③「屡遭围攻…银矿脉」（兵祸由来引子）。
  - ⚠ B4 自检：②属"历史沿革"、①属"地点简介"——**分开表达、同档不同断言仍算混写**吗？按 R3 判据"一个调取场景一档"，"这座城什么样"和"这城怎么来的"是两个场景 → ②的归属沿革**移入 doc 9（lycaron-mines）**作为其背景断言（银矿与兵祸正是沿革的因果），本档只留地貌与"屡遭围攻"的现状。已在下表 grant 设计中如此安排。
- **lycaron-mines**：①「帕拉部落…和平的合邦…被叛军首领革图夺走」（沿革）；②「银矿脉…任何想在内战中攫取权力的皇位觊觎者都会盯上这里」（矿与兵祸因果，T4 detail 层）。
  - ⚠ 类别妥协留痕（审查建议 4，采纳其"留痕"选项）：本档 domain=economy 但承载一条政治沿革断言——沿革与银矿是同一条因果链（合邦→被夺→银矿招兵祸），按 Max 第 4 条"不为拆而拆"不另立 politics 档；差异记录进闭合复验。
- **lycaron-rock-tales**：「有人传言，秃鹫已经享用过…每一种民族」（rumor 层，kind: rumor，写法=陈述"传言"本身）。

### 3.2 授权设计（核心表：表达层 × grant 模板）

五条硬口径：①grant 不越 `WorldbookIdentityCapabilityRules.cs` 上限；②**不变式：grant.min_detail == 所在表达 layer**（审查改定，见卷首）；③同一件事不同身份知道程度不同 → **拆表达**（一表达=一文本=一层），一表达内多 grant（OR）只体现"同层可达的多身份"；④常识层用「全员 OR 模板」；⑤维度（culture/kingdom/settlement_ids）按表启用。

⚠ 身份链勘误（审查确证）：registry 链 **noble→notable→commoner**、tavernkeeper→townsfolk→commoner——除 anonymous 外**全部身份都继承 commoner**。因此常识层由 commoner 行兜底即可达全身份；其余行保留是为 scope 验收覆盖与显式性，**noble 行 min_detail 用 rumor**（若写 summary 会把 <18 岁贵族挡在常识层外，而他们本经 commoner 行可达）。

**模板 T1（常识层，rumor 表达用，全员 OR）**——地理白描类断言：
```yaml
layer: rumor
grants:
  - {profile_id: profile.commoner,     scope: local,    min_detail: rumor}
  - {profile_id: profile.villager,     scope: local,    min_detail: rumor}
  - {profile_id: profile.tavernkeeper, scope: faction,  min_detail: rumor}
  - {profile_id: profile.ransom_broker, scope: faction, min_detail: rumor}
  - {profile_id: profile.townsfolk,    scope: regional, min_detail: rumor}
  - {profile_id: profile.notable,      scope: regional, min_detail: rumor}
  - {profile_id: profile.merchant,     scope: faction,  min_detail: rumor}
  - {profile_id: profile.headman,      scope: national, min_detail: rumor}
  - {profile_id: profile.soldier,      scope: national, min_detail: rumor}
  - {profile_id: profile.noble,        scope: elite,    min_detail: rumor}
```

**模板 T2（summary 表达用）**：
```yaml
layer: summary
grants:
  - {profile_id: profile.townsfolk, scope: regional, min_detail: summary}
  - {profile_id: profile.notable,   scope: regional, min_detail: summary}
  - {profile_id: profile.merchant,  scope: faction,  min_detail: summary}
  - {profile_id: profile.headman,   scope: national, min_detail: summary}
  - {profile_id: profile.soldier,   scope: national, min_detail: summary}
  - {profile_id: profile.noble,     scope: elite,    min_detail: summary}
```
（villager/commoner 不在此表：其 detail 上限=rumor，读 summary 层即越限——想让他们知道就另写 rumor 表达，不在此层放行。）

**模板 T2L（本地人层，rumor 表达 + settlement_ids，golden case 4 同源）**——"本镇人才这么说"：
```yaml
layer: rumor
grants:
  - {profile_id: profile.commoner, scope: local, min_detail: rumor, settlement_ids: [entity.settlement.town_v7]}
```
（settlement_ids 匹配**身份自己的归属聚落**：本镇本地人命中，外地同身份不命中 → "本地 vs 外地"对照。）

**模板 T3（文化/王国层，rumor 表达，golden case 4 主验证）**——瓦尔切格本地说法：
```yaml
layer: rumor
grants:
  - {profile_id: profile.villager, scope: local, min_detail: rumor,
     culture_ids: [entity.culture.sturgia], kingdom_ids: [entity.kingdom.sturgia]}
```
（两维度 AND：斯特吉亚文化+斯特吉亚王国的村民可见；文化对、王国不对的身份不可见 → 矩阵专设"巴旦尼亚文化×斯特吉亚王国"对照。）

**模板 T4（detail 表达用，行家层）**——物产/生计/银矿细节：
```yaml
layer: detail
grants:
  - {profile_id: profile.notable,  scope: regional, min_detail: detail}
  - {profile_id: profile.merchant, scope: faction,  min_detail: detail}
  - {profile_id: profile.headman,  scope: national, min_detail: detail}
  - {profile_id: profile.soldier,  scope: national, min_detail: detail}
```

**模板 T5（secret 表达用，贵族老话）**：
```yaml
layer: secret
grants:
  - {profile_id: profile.noble, scope: elite, min_detail: secret}
```
（noble 的 secret 上限=年龄 ≥45 或管理 ≥80；矩阵用 50 岁取到 / 30 岁取不到做对照。）

⚠ **denies 的 schema 形态（审查确证）**：`denies` 是**表达级必填字段**（可为空数组，须显式写 `denies: []`），规则字段与 grant 同构（`profile_id`+`scope`+`min_detail` 必填；运行时匹配只读 profile 链与维度，scope/min_detail 填 `local`/`rumor` 占位即可）。**文档根上没有 denies 属性**，不得写在档级。

**逐档「表达 × 模板」映射表**（实施按此写 YAML；自查时发现此表缺位会让 golden case 3 落空，补上）：

| 档 | 断言 | 表达（层 × 授权） |
|---|---|---|
| charas-town | ①位置 | rumor(T1) + summary(T2) |
| | ②海务贸易与别墅 | rumor(T1) + summary(T2) |
| | ③海湾群岛 | rumor(T1) + rumor(**T2L**，settlement_ids=[town_v7]) |
| charas-reign | ①归属沿革 | summary(T2) |
| | ②财富·事实面 | detail(T4) |
| charas-cortain-secret | 公开面 | summary(**T2 + tavernkeeper 诱饵行**，见 3.3) |
| | 秘密面 | secret(T5) |
| varcheg-town | ①海崖与半岛 | rumor(T1) + summary(T2) |
| | ②港的生计由来 | rumor(T1) + summary(T2) |
| varcheg-swap | ①效忠→雇佣→击溃 | detail(T4) |
| | ②报酬夺城 | detail(T4) |
| | ③镇名由来 | rumor(**T3**，culture×kingdom) |
| husn-fulq-town | ①卡尔得亚区位 | rumor(T1) + summary(T2) |
| | ②堡垒起源 | summary(T2) |
| | ③两头取财 | detail(T4) |
| husn-fulq-tales | ①其人其事 | summary(T2) |
| | ②鬼点子桥段 | rumor(**{tavernkeeper, faction, rumor} + {villager, local, rumor}**——golden case 3 主位) |
| lycaron-town | ①石山地貌 | rumor(T1) + summary(T2) |
| | ②屡遭围攻现状 | summary(T2) |
| lycaron-mines | ①沿革 | summary(T2) |
| | ②矿与兵祸 | detail(T4) |
| lycaron-rock-tales | ①秃鹫传言 | rumor(**{tavernkeeper, faction, rumor} + {villager, local, rumor}**) |

（规模复核：表达数/档 = 5/2/2/4/3/4/2/3/2/1，每断言表达 ≤2 ≤4 上限 ✓；tales 档 tavernkeeper 只有 rumor grant → 问 detail 即被挡 = golden case 3。）

### 3.3 秘密档设计（窄用法落地；deny 结构已按审查重做）

`doc.politics.charas-cortain-secret`，1 断言 2 表达，**两条表达文本实质不同**（审查存疑 4 的整改：秘密档必须有信息增量，不是公开文换层）：

- **summary 表达（公开面）**：「沙拉斯的海务财富尽归科尔坦家」——所有权事实（引文「落入了…戴·科尔坦家族手中」），grants = **T2 全行 + 显式追加一行 `{profile_id: profile.tavernkeeper, scope: faction, min_detail: summary}`**——这一行是**有意保留的诱饵**：tavernkeeper 上限（faction+detail）完全够读 summary，若没有 deny 它本可达；正是它让 golden case 1 成为"有 grant 仍被 deny 压制"的真对照，而不是"无 grant 不可见"的空测。kind: fact。
- **secret 表达（秘密面）**：「这些财富正被拿来操办科尔坦家的图谋」——政治解读（引文「其无穷的财富助长了后者的野心」的*解释*），grant 用 T5。kind: interpretation。

**denies（逐表达挂载，避开 noble 链）**：

```yaml
# secret 表达上：
denies:
  - {profile_id: profile.anonymous, scope: local, min_detail: rumor}
# summary（公开面）表达上：
denies:
  - {profile_id: profile.tavernkeeper, scope: local, min_detail: rumor}
```

- **为什么这样是"窄"**：deny 是档级核弹（`HasMatchingDeny` 遍历全档表达、任一命中→`Query` L52-56 `continue` 整档消失），所以铁律=公开档绝不挂 deny，deny 只存在于这份秘密档。
- **⚠ 审查改定的关键点**：deny `{profile_id: profile.commoner}` **不能用**——registry 链 noble→notable→commoner，commoner 的 deny 规则命中**所有后代闭包**（含 noble/notable/townsfolk/villager），会把 50 岁贵族也炸掉，golden case 2 必失败。因此 deny 一律**定向到具体身份**：tavernkeeper-deny 只命中 tavernkeeper 链（tavernkeeper→townsfolk→commoner，不含 noble）；anonymous-deny 只命中显式 `profile.anonymous` 查询。
- **golden case 1 实测位**：酒馆老板对公开面表达**有 grant**（其身份上限 faction+detail 完全够 summary）但命中 tavernkeeper-deny → **整档被拒**（含本可达的公开面）——deny > grant 从推演变实测。
- 其余身份（villager/commoner/townsfolk 本档无 grant；~~headman/merchant/soldier/…：无 grant 无 deny → unknown~~ **09-12 矩阵实测修正：headman/merchant/soldier 经 T2 全行在公开面有 grant，可读公开 summary；villager/commoner 才是 unknown——见 `MATRIX-RESULT-20260912.md` §三.2**）（golden case 5 同源：无匹配不得默认 public）。
- **noble 读秘密面的路径已核**：noble 链（noble→notable→commoner）**不含 tavernkeeper/anonymous** → 两条件 deny 均不命中；30 岁贵族卡在 grant（detail<secret），50 岁可达。

### 3.4 结构验收覆盖对照（对计划 §六）

| 验收项 | 由哪条承担 |
|---|---|
| secret 层 ≥1 | 档 3 secret 表达 |
| deny >0（窄用法） | 档 3 denies |
| scope 用满 5 档 | local(T1/T2L/T3)、regional(T1/T2/T4)、faction(T1/T4)、national(T1/T2/T4)、elite(T1/T2/T5) |
| profile 使用 ≥8 | commoner/villager/townsfolk/notable/merchant/tavernkeeper/ransom_broker/headman/soldier/noble/anonymous = **11** |
| culture_ids | T3（瓦尔切格 villager） |
| kingdom_ids | T3（同上） |
| settlement_ids | T2L（沙拉斯本地 rumor 表达，commoner+settlement_ids=[town_v7]） |
| 一表达多 grant OR | T1–T5 全部 |
| golden case 1 | 档 3 公开面：tavernkeeper **有 grant**（诱饵行）但命中定向 deny → 整档拒 |
| golden case 2 | 档 3 secret 只授 noble；30 岁贵族对照（deny 已避开 noble 链） |
| golden case 3 | 档 7②/10① tavernkeeper 只有 rumor grant → 问 detail 被挡 |
| golden case 4 | T3 的 culture×kingdom AND + 巴旦尼亚裔斯特吉亚村民对照 |
| golden case 5 | 探针档空 grants 场景（unknown，不默认 public） |

---

### 3.5 B 源变体 → 档映射（A+B 双源落地，本批接入 9 条 B 表达）

**接入原则**：①B 变体**只进表达层**，中立内核仍纯 A（内核不撮合、不分层）；②B 表达 `source_id=source.calradia.chronicle.animusforge`，`locator` 带变体号，`kind` 标 `viewpoint`（身份视角叙述）；③grant 即编年史 `When` 的直译（Cultures→`culture_ids`、KingdomIds→`kingdom_ids`、SettlementIds→`settlement_ids`、Roles/IdentityIds→对应 profile），scope/detail 按身份上限与 T 模板落；④每断言表达 ≤4 硬限不破；⑤`When` 与官方冲突的变体不接（§2.4 ❌ 行）。

| 档 | 断言 | 接入 B 变体 | grant 直译 | 表达层 |
|---|---|---|---|---|
| charas-town | ①位置/起源 | rule_沙拉斯 **V4**（帝国平民「爷爷说那是帝国最老最老的城」） | `{profile.commoner, local, rumor}` + `culture_ids:[entity.culture.empire]` | rumor／viewpoint |
| charas-town | ②港的生计 | rule_沙拉斯 **V2**（瓦兰迪亚平民「码头扛货一天顶内陆三天，面包贵一倍」） | `{profile.commoner, local, rumor}` + `culture_ids:[entity.culture.vlandia]` | rumor／viewpoint |
| charas-reign | ②财富归科尔坦 | rule_沙拉斯 **V1**（瓦兰迪亚贵族「钱袋子／榨得太狠，防波堤没人修」） | `{profile.noble, elite, detail}` + `culture_ids:[entity.culture.vlandia]` | detail／viewpoint |
| charas-cortain-secret | secret 面 | rule_沙拉斯 **V0**（因加泰尔第一人称「这些钱养着我的骑士，喂着我的野心」） | `{profile.noble, elite, secret}`（同 §3.3 grant） | secret／viewpoint |
| varcheg-town | ①海崖/半岛 | rule_卡恰尔 **V1**（斯特吉亚人「东边的大门…守着两条海路」） | `{profile.villager, local, rumor}` + `culture_ids:[entity.culture.sturgia]` + `kingdom_ids:[entity.kingdom.sturgia]`（=**T3 实战**） | rumor／viewpoint |
| varcheg-swap | ①效忠→雇佣→击溃链 | rule_卡恰尔 **V5**（巴旦尼亚人「不要雇比你更贪婪的人帮你打架」） | `{profile.villager, local, rumor}` + `culture_ids:[entity.culture.battania]`（**golden case 4 对照素材**：巴旦尼亚裔视角对同一事件的说法） | rumor／viewpoint |
| lycaron-town | ①地貌/围城 | rule_吕卡隆 **V5**（本地老兵二十年「亲眼见皇帝城墙上阅兵」） | `{profile.soldier, national, detail}` + `settlement_ids:[entity.settlement.town_es4]`（=**T2L 实战**） | detail／viewpoint |
| lycaron-town | ②银矿与内战 | rule_吕卡隆 **V2**（南帝国「先皇统治十八年…真正的帝国血脉还在吕卡隆」） | `{profile.townsfolk, regional, summary}` + `kingdom_ids:[entity.kingdom.empire_s]` | summary／viewpoint |
| lycaron-rock-tales | 秃鹫传言 | rule_吕卡隆 **V6**（斯特吉亚「秃鹫等着下一批送死的帝国人」） | `{profile.commoner, local, rumor}` + `culture_ids:[entity.culture.sturgia]` | rumor／viewpoint（怪谈的异邦版本） |

- 断言①的地貌类白描仍以 A 级官方引文为 kernel（B 变体作同断言另一身份的表达，**同一件事不同身份知道程度/说法不同**正是授权结构要验的东西）。
- lycaron-mines 本批**不加** B 表达（其 A 级 2 断言已够 T4/faction 验证；吕卡里亚平原 8V 留给平原立档批）。
- husn 两档纯 A（§2.4 范围裁定）。
- B 表达的 quote_hash 用编年史变体原文同算法计算；corrections 不涉及（全是新档）。
- 矩阵验收（§六）新增 3 个 B 源对照行：帝国平民→charas-town V4 可见；巴旦尼亚文化村民→varcheg-swap V5 可见且 V1（sturgia AND）不可见；吕卡隆本地老兵→V5 可见。

## 四、老档修复（12 档，走 corrections_20260912）

### 4.1 前置自检（不动手先跑）

```
grep -rin 6E17075F docs/  与  grep -rin 309D1058 docs/
```
确认 golden 钉死的两份登记表哈希不被本批触碰（authoring YAML 不在其列，预期干净；实测留痕进 corrections 记录）。

### 4.2 授权修复（按内容二分 + 不变式落地）

⚠ 修复必须同时满足两条：grant ≤ 身份上限，**且 grant.min_detail == 所在表达 layer**（不变式）。因此部分修复是"改 grant"，部分是"改表达 layer"，逐条判定：

| 现状 | 条数 | 修法 |
|---|---|---|
| `commoner + regional + summary` | 24 | **地理白描类**（kach-land、sara-bay 大部、lac-lake、der-vill、dawn-mtn 的白描表达）→ 表达 `layer: summary → rumor`（文本不动，"地理常识"本就该落 rumor 层），grant 换 **T1 全员 OR**；**叙事/内行类**（sara-tales、kach-tales、lac-tales 的叙事表达）→ 表达 layer 不动，grant 改挂对应身份（detail 层→notable；rumor 层→villager/tavernkeeper），不再挂 commoner |
| `villager + local + summary/detail` | 4 | 所在表达若为 rumor 层 → `min_detail: rumor`（不变式达成）；若为 summary/detail 层 → **该表达本就不该给村民**，grant 改挂 townsfolk/notable（村民读高层=越限泄漏，审查必须改 4 的机制） |
| `townsfolk + regional + detail` | 5 | 逐条判：文本确属"本地内行细节"→ 改挂 `notable + regional + detail`；一般叙述 → 表达降 `layer: summary` + T2。逐条判定结果写进 corrections |
| `merchant ×2 / notable ×4` | 6 | 核对不变式（min_detail 是否等于所在表达 layer），不等处随 corrections 记录 |
| `noble_high_steward + elite + detail`（lac-lake） | 1 | 改 `profile.noble`（registry 链即含 noble），scope/min_detail 维持 elite+detail（所在表达须为 detail 层，核对） |

### 4.3 锚点迁移（10 档 `entity.lore.*` 编译必炸 WB-DOC-003）

| 档 | 现锚点 | 处置 | 理由 |
|---|---|---|---|
| sara-bay | `entity.lore.shalas_bay` | → `entity.settlement.town_v7` | 湾与沙拉斯港本就一体（官方文：沙拉斯=海务贸易港+周边海湾），有真实聚落可挂 |
| sara-tales | 同上 | **去掉 entity_ids + 补 aliases** | 起源传说是帝国级叙事，非该聚落本身；概念类不硬挂 |
| kach-land / kach-own / kach-tales | `entity.lore.kachar_peninsula` | → `entity.settlement.town_s1` | 官方文：瓦尔切格正在卡恰尔半岛深处，半岛簇挂半岛上的城镇 |
| lac-lake / lac-tales | `entity.lore.lakonis_lake` | 去掉 + aliases | 湖泊概念，本批无邻近聚落可挂 |
| dawn-mtn / dawn-stew / dawn-taboo | `entity.lore.dawn_mountains`（+`entity.lore.khergit_wardens`） | 去掉 + aliases | 山脉与「看守人」概念（khergit_wardens，官方对位=库吉特）均为概念，无聚落可挂 |
| der-vill / der-furs | `entity.settlement.castle_village_v6_2` | 不动 | 已合法 |

### 4.4 aliases 补齐

12 老档全部补 `aliases`（`{"zh-CN": [...], "en": [...]}`，取官方名+编年史旧称；无锚点档**必须**靠它进 K1 检索——现有 0/12 是检索侧裸奔）。示例：sara-tales → `["沙拉斯湾","帝国摇篮","卡拉德登陆"]`；varcheg 相关 → `["瓦尔切格","Varcheg","誓言之同伴"]`。

⚠ **译名修正（09-12 三审定案：回库+官方 CN 全语言行对拍）**：`entity.lore.khergit_wardens` 显示名「合儿必特部」**错挂**——合儿必特=**Harfit**（`gXeF1yKn`=clan_khuzait_5，库赛特汗国封臣 tier 3；繁中「哈爾菲特」/日「ハルフィット」/俄「Харфиты」铁证其非 Khergit）。**Khergit 官方 CN=库吉特**（`Q1nAHnV7`=clan_khuzait_2，汗国封臣 tier 4、家城查坎德、首领墨速宜；非小派系非流亡，官方文「曾是联盟最大部族之一」）。喀拉库吉特=Karakhergit（`6BvGhzao`=karakhuzaits，**唯一 isMinorFaction=1**，拒兀儿浑乃特可汗定居令）。Warden 官方=看守人（`b2w4dmy2`）。→ dawn-stew 修档 aliases 用「库吉特」；**「合儿必特」不得列为 khergit 同义词**（它是另一家官方家族名，混挂会把两家搅在一起）；登记表显示名随 corrections 改。

### 4.5 留痕

`docs/worldbook-migration/projection/authoring-out/corrections_20260912/`：每档一条记录，**原值逐字保留**（旧 grant 行 + 旧 entity_ids 行 + 原因 + 对应计划条款）。

---

## 五、deny / min_detail 探针测试档规格（步骤 0）

⚠ **探针档 id 勘误（审查必须改 5，采纳）**：schema 的 doc id 要求 domain ∈ 五域，`doc.test.*` 不合法 → 改挂 `doc.geography.test-deny-probe` / `doc.geography.test-min-detail-probe`（仅入 workspace 测试区，不入正典库）。

**探针 A：deny**（`doc.geography.test-deny-probe`，2 断言）

- 断言 1「测试公开事实」：rumor 表达，grant `{tavernkeeper, local, rumor}`；挂 `denies: [{profile_id: profile.tavernkeeper, scope: local, min_detail: rumor}]`（自查修正：原稿此处写 `denies: []`、与"公开面挂 tavernkeeper-deny"自相矛盾）。
- 断言 2「测试秘密」：secret 表达，grant `{noble, elite, secret}`；挂 `denies: [{profile_id: profile.anonymous, scope: local, min_detail: rumor}]`。
- **探针的双重目的**：tavernkeeper 在断言 1（自己无 deny 的那断言本可达）命中 deny → 验证核弹**跨断言**炸掉整档（含断言 2）；noble 两条 deny 都不命中 → 验证定向 deny 的规避面。

**探针 B：min_detail 双探针**（`doc.geography.test-min-detail-probe`，1 断言 2 变体表达，同 detail 层）

- 变体 A（故意违反不变式）：detail 层表达，grant `{villager, local, rumor}`。
- 变体 B（不变式合法）：detail 层表达，grant `{villager, local, detail}`。
- ⚠ 预期已按审查重写（模拟器恒传 `RequestedDetail=secret`，`SelectExpression` 只看请求不看身份能力）：**变体 A → villager 读到 detail 文本**（泄漏实锤：表达层与身份上限之间无自动闸，证明不变式②的必要性）；**变体 B → grant 不匹配（rumor<detail 越上限）→ not_found**。跑完把两个引擎结果写进实测记录，替换本节预期为实测值。

模拟器预期结果表（探针 A，~~跑前写死~~ **已实测（09-12 晚，详见 `PROBE-MEASURED-20260912.md`）**）：

| 身份 | 实测（原预期） | 机制 |
|---|---|---|
| tavernkeeper | **blocked/permission（整档消失）**（原预期 not_found，state 名实测为 blocked） | 有 grant 但命中定向 deny → golden case 1 实测 |
| noble(50岁, 显式 IdentityId) | **known**（secret 层可见）✅ | noble 链不含 tavernkeeper/anonymous，deny 不命中 |
| noble(30岁) | **blocked/permission**（grant 不匹配，与 deny 无关）✅ | grant 不匹配（detail<secret） |
| villager | **not_found**（unknown）✅ | 无 grant 无 deny → 不得默认 public（golden case 5） |
| anonymous（显式 profile.anonymous 查询） | **blocked**（命中 anonymous-deny）✅ | 定向 deny 命中 |

探针 B 实测：不变式违规变体 → villager **拿到 detail 文本（state=partial，泄漏复现）**；requested=rumor 对照 → blocked。**全部预期命中，无读码结论被推翻。** ⚠ 新增实测知识：grant 拒与 deny 命中在 state 上都表现为 `blocked`（reason=permission），矩阵判定"整档被 deny"须结合 grant 有无来推。

⚠ **引擎分工（审查必须改 6，采纳）**：Studio `PreviewProjectionBuilder` 与运行时在 deny 作用域（逐表达 redacted vs 整档跳过）、min_detail 参照物（表达 layer vs 身份能力）、能力模型（无 CapabilityMatches 等价物）三处**已知分歧**——golden case 1/2 两边会给出相反结果。**验收一律以运行时模拟器为准**；Studio 投影仅作内容预览，不作权限验收。

---

## 六、身份 × 档矩阵验收（步骤 5）

**以运行时模拟器为准**（理由见 §五引擎分工）。矩阵行=身份构造（含 culture/kingdom/settlement/年龄变体），列=10 新档+探针，格=预期可见层。关键对照行：

| 身份构造 | 关键预期 |
|---|---|
| 沙拉斯本地村民（villager, vlandia, town_v7） | charas-town rumor 层**可见**（老档惨案不再重演）；cortain-secret **不可见**（无 grant） |
| 外地村民（villager, vlandia, 其它聚落） | charas-town T2L 本地 rumor 层**不可见**（settlement_ids 不命中） |
| 巴旦尼亚文化 × 斯特吉亚王国村民 | varcheg-town T3 rumor 层**不可见**（golden 4：culture 对 kingdom 不对） |
| 酒馆老板（tavernkeeper） | tales 档 rumor 可见；cortain-secret **整档被 deny**（golden 1）；summary/detail 层不可见 |
| 商人（merchant） | lycaron-mines detail 可见（faction scope 首次实战） |
| 士兵（soldier） | reign/swap 归属 detail 可见 |
| 贵族 30 岁 / 50 岁（显式 IdentityId） | secret：50 岁可见、30 岁不可见（golden 2，deny 已避开 noble 链） |
| 头人 headman | T1 常识层可见 + T2 summary、T4 detail 可见；secret 无 grant 无 deny → unknown |
| **仅传 Role、无 IdentityId**（对照行，审查存疑 3） | 走硬编码角色回退链（与 registry 链不同），矩阵记录两条路径的结果差异，作为登记链口径的实测证据 |

验收通过线：**每个预期格与实测一致**；新档不存在"目标身份取不到"的格（老档惨案回归测试）；两条身份路径的差异被记录并归档。

---

## 七、红线（沿用计划 §五，不变）

- 不动 `referral-registry.v1.json` / `profile-registry.v1.json`；不碰 C#；不碰游戏本体。
- 每条断言 `claim → origin` 可指回官方引文；引文一律取自 §2.3 母本。
- 新档 entity_ids 只写 `entity.settlement.<小写>`；概念档不写。
- 中世纪口径：无现代视角词；rumor 类只陈述"说法"本身。

## 八、独立审查结论 + 自查轮（09-12，已闭合）

**独立只读审查**：报告四档，【通过】9 项（上限口径、deny 档级核弹、schema 字段与 pattern、grant 维度编译路径、aliases 进 K1、规模口径等）；【必须改】6 项**全部吸收**：noble registry 链自炸（改定向 deny）、T1 noble 行（min_detail→rumor）、denies 逐表达挂载、min_detail 探针预期重写、探针档 id 合法化、引擎分工改定（矩阵以运行时为准）；【建议改】6 项采纳 5 项（不变式、T1 统一 rumor、varcheg ③归位、lycaron-mines 留痕、split_from 勘误），越限自检扩项 1 项；【存疑】4 项已核清或在矩阵加对照行。

**自查轮（Max 令，签收前）**：发现并修复 8 处——①上轮两处 Edit 报成功**未落盘**（varcheg ③归位、lycaron-mines 留痕），致文档与 §八声明矛盾，已重写并逐处 grep 复验落盘；②golden case 1 诱饵 grant 缺位（公开面"T2 摘要版"里没有 tavernkeeper 行，deny 无压制对象），§3.3 显式追加诱饵行；③**补逐档「表达 × 模板」映射表**（§3.2 末，此前 golden case 3 无落地路径）；④§3.4 scope 行的模板归属写错（national 误标 T5 等）；⑤探针 A 断言 1 `denies: []` 与"公开面挂 deny"自相矛盾；⑥§六 headman 行层别表述含混；⑦§4.3 "knex" 乱码→khergit_wardens；⑧split_from 勘误误留"每档 split_from 留痕"旧句。

三大待审点的最终立场（含审查员独立判断）：

1. **T1 全员 OR**：保留全列（补全是正确性），noble 行 min_detail→rumor；审查员确认"除 anonymous 外全身份继承 commoner，commoner 行兜底，其余行为 scope 验收保留"。
2. **锚点迁移**：审查员**接受**——entity_ids 白名单只认 hero/clan/settlement，手册 B4.1 明文背书"别名/旧称走 aliases/redirects，不自造 ID"；sara-bay→town_v7、kach-*→town_s1 属主题强相关，不构成滥用。
3. **秘密档选材**：审查员**有条件接受**——"官方文本是素材不是权限"立场成立；条件=deny 重设计（已做，§3.3）+ 秘密口径与公开面文本实质差异（已做，summary=所有权事实 / secret=政治解读）。

## 九、待 Max 签收

本稿连同审查结论一并提交签收。签收后按 §一顺序实施；实施中若实测推翻本稿任何"读码结论"，**停下回写本稿并报告**，不硬推。
