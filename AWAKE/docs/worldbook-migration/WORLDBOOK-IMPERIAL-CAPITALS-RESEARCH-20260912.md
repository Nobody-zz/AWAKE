# 卡拉德帝国历代都城考据（四都城：沙拉斯 / 巴拉维诺斯 / 萨涅俄帕 / 吕卡隆）

- 日期：2026-09-12
- 提出：Max（口述四城及各自都城地位，要求考据核实）
- 执行：阿砚
- 数据源：BannerlordSage v1.3.15.110062（`localization_entries` 272,223 条全语言 + `bannerlord_settlements` / `bannerlord_kingdoms` / `bannerlord_clans` + `Modules/SandBox/ModuleData/lords.xml` + 反编译源码 `assets/Source/bin/Win64_Shipping_Client/`）
- 方法：英文原文精确匹配 → stringId → CNs 官方简中（项目标准工序）；"capital / 首都 / 帝都 / 迁都"全库清剿；代码层变量追踪。
- 状态：**待 Max 审阅签收**。本稿为考据研究稿，不直接改任何正典。

---

## 一、结论速览

| 城市 | 英文原名 | 官方简中 | 游戏 ID（可锚定） | 1084 开局归属 | 都城地位（证据等级） |
|---|---|---|---|---|---|
| 沙拉斯 | Charas（描述文中亦作 Charasea） | 沙拉斯 | `town_V7` | 瓦兰迪亚 clan_vlandia_4（戴·科尔坦） | **第一块殖民地/登陆之地**（A：官方描述文 0cZmKO76） |
| 巴拉维诺斯 | **Paravenos**（⚠ 不是 Baravinlos） | 巴拉维诺斯（繁中：帕拉維諾斯） | 无独立 ID，**即今帕拉汶德 `town_V3`** | 瓦兰迪亚 clan_vlandia_2（戴·提尔） | **卡拉狄乌斯大帝建第二殖民地，取代沙拉斯为首都；后帝国重心东移**（A：官方描述文 Nx0qK2g6） |
| 萨涅俄帕 | Saneopa | 萨涅俄帕 | `town_EN3` | **涅雷采斯家**（clan_empire_north_3） | 涅雷采斯家根据地＝**A**（开局领地）；"德洛修斯时期首都"＝**官方无载（B：考据说法）** |
| 吕卡隆 | Lycaron | 吕卡隆 | `town_ES4` | **珀特洛斯家**（clan_empire_south_1）；南帝国 initialHome | **帝国末都/阿雷尼科斯时期首都**（**A：代码实锤**）；银矿、要塞＝A（描述文 WGtzBswe） |

> ⚠ 译名陷阱（与"涅雷采斯/內雷澤斯"同族）：巴拉维诺斯的**城名**英文原名是 **Paravenos**（P-，全语言一致：パラヴェノス/파라베노스/Паравенос…），繁中作"帕拉維諾斯"，简中作"巴拉维诺斯"。
> **裁定（Max 09-12）**：巴拉维诺斯的英文城名**定为 Paravenos**。官方另有战役名"Baravenos Encirclement"（巴拉维诺斯之围，hash YRabuUqx，std_native_strings）用 B 系拼法——那是战役专名，**不影响城名拼写**；繁中同文自身也不一致（帕拉維諾斯/巴拉韋諾斯之圍）。结论：
> - 中文一律"**巴拉维诺斯**"（CNs 5 处命中零波动）；
> - 英文城名一律 **Paravenos**；
> - "Bara**vin**los"（-in-）全库 0 命中，禁用。

---

## 二、逐城考据

### 1. 沙拉斯（Charas，`town_V7`）

**A 级引文**（官方描述文，hash `0cZmKO76`，`std_common_strings_xml`）：

> EN: "The city known today as **Charas** is traditionally reckoned to be **the first Calradian colony** on the shores of their new continent. At the Empire's height, **when the capital moved north to Paravenos**, Charasea remained a key hub of sea-faring and trade… When the Vlandians came, Charas fell into the hands of the ruthless House of **dey Cortain**…"
>
> CNs: "这座如今被称为沙拉斯的城市，其历史最早可以追溯到**第一批卡拉德殖民者登上这片大陆的海岸之时**。在帝国如日中天的时候，其首都迁到了北方的巴拉维诺斯，而沙拉斯则成为了一座海务和贸易的重要港口……瓦兰迪亚人到来后，沙拉斯落入了无情的**戴·科尔坦**家族手中……"

**对照 Max 说法**：
- "最初登陆之地" → ✅ 实锚（第一批殖民者登陆海岸、第一块殖民地）。
- "建国之地" → ⚠ 官方措辞是"第一块殖民地"，帝国是后来才有的事；写内核时用"第一批殖民者登陆之处/第一块殖民地"，不要拔高成"建国"。
- 都城地位：仅早期一代，随即北迁巴拉维诺斯。

### 2. 巴拉维诺斯（Paravenos → 今帕拉汶德，`town_V3`）

**A 级引文**（官方聚落描述文，hash `Nx0qK2g6`，`std_settlements_xml`，落点即 town_V3 帕拉汶德的 descriptionText）：

> EN: "**Paravenos** was the second major colony founded by **Calradios the Great**, and it soon succeeded Charasea as **the capital of the Calradians**. Eventually, the empire's center of gravity moved east, but Paravenos retained its primacy as the economic center of the West. When **Osric Iron-Arm** the Vlandian invaded, he recognized Paravenos would be far more valuable as a seat of power than as simply as a source of loot, and negotiated its surrender with local senators. It passed to a cadet branch of Osric's line, the **dey Tihr** clan, and **is today known as Pravend**."
>
> CNs: "巴拉维诺斯是由卡拉狄乌斯大帝建立的第二座重要的殖民地，并在随后迅速取代沙拉斯，成为了卡拉德人的首都。最终，帝国的统治重心东移，但巴拉维诺斯依旧是西部的经济重镇。当瓦兰迪亚人'铁壁'奥斯里克入侵时……同当地的元老协商让该城投降。此地传承到了奥斯里克的旁支，**戴·提尔**家族手中，**如今人们称其为帕拉汶德**。"

**旁证**（A）：贾尔马律斯描述文 `TI3fS2xN` 提到"巴拉维诺斯乐意接受从这里掠夺的财物和奴隶"——印证其为西部重镇。

**对照 Max 说法**：
- "数百年长期定都" → ⚠ 官方只说"取代沙拉斯为首都"+"最终统治重心东移"，**定都时长无载**。写实可用官方措辞；"数百年"只能进说法层（且是推断，须标注）。
- 与既有产出一致性：`docs/mappings/settlement-aliases.v1.json` 已登记帕拉汶德 aliases＝["巴拉维诺斯","Paravenos"]，证据引文同源，✅ 无冲突。
- 时间链条衔接：帕拉汶德 hist 档（geo1/hist3/succ2）已有"以巴拉维诺斯为前身"的线索，本稿为其补全 A 级引文。

### 3. 萨涅俄帕（Saneopa，`town_EN3`）

**A 级引文**（官方描述文，hash `EJZLzhJk`）：

> EN: "Saneopa sits on a low pass leading from the Nevys valley to the north to the pool of the Ophthys. The ancient Palaic peoples of this region, like the Battanians, treated lakes as sacred sites… The hustle and bustle of Saneopa, **one of the main trading centers of inland Calradia**, stands in sharp contrast to the dark and silent waters below."
>
> CNs: "萨涅俄帕坐落在一道涅维斯谷通往北方俄佛堤斯湖的低矮隘口之上……人声嘈杂、百业兴旺的萨涅俄帕是**卡拉迪亚内陆重要的贸易中心**……"

**"涅雷采斯家的根据地"＝A 级实锚**：
- 1084 开局 `settlements.xml`：town_EN3 owner ＝ `Faction.clan_empire_north_3` ＝ **Neretzes（涅雷采斯）家族**（`bannerlord_clans` 确认 clanId/clan 名，tier 4，隶属北方帝国）。
- 英雄背景文（`std_heroes_xml`，hash `04xAg0yN`，lord_1_5 彭同）："由于**涅雷采斯家族一直以来就属于寡头阵营**，因此彭同在内战中选择了支持卢孔。"

**"德洛修斯·涅雷采斯时期的首都"＝官方无载**：
- 全库清剿：27.2 万条 localization（全语言）+ 全部英文源文件中，"capital"仅 16 处命中、"首都"仅 1 处（泛指性台词 bFZrLY8W）——**没有任何一处把萨涅俄帕称作首都**。
- 反编译源码中 town_EN3 零引用（对照：吕卡隆 town_ES4 被代码硬指为首都，见下）。
- 判定：这是**考据圈说法（B 级）**，恰好能填官方"重心东移"之后、阿雷尼科斯迁都之前的空档，叙事上自洽，但**不能进内核当事实**。可进说法层（"有人说德洛修斯朝定都于此"），是否收录待 Max 裁定。

### 4. 吕卡隆（Lycaron，`town_ES4`）

**A 级引文**（官方描述文，hash `WGtzBswe`）：

> EN: "The natural fortress of Lycaron sits on a spur jutting out of the great Ornian rock. In the final years of the imperial conquest, the Palaic tribe here negotiated a peaceful federation with the empire, only to see their home seized by the rebel leader **Gethuz**. The ensuing siege was only the first of many… as its nearby **silver mines** made it a prize for **every imperial pretender who ever tried to seize power in a civil war**…"
>
> CNs: "吕卡隆这座天然要塞位于巍峨的奥尼石山……接踵而至的围城战仅仅是吕卡隆在接下来的几个世纪中屡遭围攻的第一次，因为该城周边有着**丰富的银矿脉**，**任何想在内战中攫取权力的皇位觊觎者都会盯上这里**……"

**"帝国首都"＝A 级代码实锤**（反编译 `TaleWorlds.CampaignSystem.CampaignBehaviors.LordConversationsCampaignBehavior.cs` L1751）：

```csharp
MBTextManager.SetTextVariable("IMPERIALCAPITAL",
    Settlement.FindFirst((Settlement x) => x.StringId == "town_ES4").Name);
```

- 官方文本里"帝国首都"是动态变量 `{IMPERIALCAPITAL}`，运行时代码把它**硬绑定到 town_ES4＝吕卡隆**。
- 使用该变量的 8 条官方流浪者出身文，内容全部指向**阿雷尼科斯死期前后的首都暴乱**：竞技场（hippodrome）骚乱、"帝国卫队"放贷邻居、"首都被烧毁的那天"、维吉亚卫队老兵（`DFW5i63X`："阿雷尼科斯在位时，我在维吉亚卫队服役……骚乱发生了，我在{IMPERIALCAPITAL}的所见所闻……"）。
- 1084 开局：town_ES4 owner ＝ `clan_empire_south_1` ＝ **Pethros（珀特洛斯）家族**；`bannerlord_kingdoms`：南帝国（empire_s）`initialHomeSettlement` ＝ town_ES4。阿雷尼科斯·珀特洛斯出身珀特洛斯家——**首都与其家族根据地重合**。

**对照 Max 说法**：
- "阿雷尼科斯·珀特洛斯**建立**的新都" → ⚠ **"建立"与官方冲突**：官方明言吕卡隆是帕拉人故城（帝国征服末期和平合邦），早于阿雷尼科斯数百年。应修正为"**阿雷尼科斯时期定都于此/帝国末都**"。
- "军事要地、银矿资源丰富" → ✅ 实锚（天然要塞＋银矿＋内战觊觎者）。
- "平衡各方势力" → 官方无载（若指迁都动机，属推断，进说法层）。

---

## 三、迁都链（官方可立骨架 + 已裁填充）

1. **沙拉斯**：第一批卡拉德殖民者登陆之地，第一块殖民地（0cZmKO76，A）。
2. → **巴拉维诺斯**：卡拉狄乌斯大帝所建第二殖民地，帝国鼎盛时取代沙拉斯为首都（Nx0qK2g6，A）。
3. → 官方明言"**帝国的统治重心东移**"（Nx0qK2g6，A），东移后都城何在官方无载。
4. → **萨涅俄帕**：涅雷采斯朝都城（**D 级，Max 09-12 裁定**，见 §六）。
5. → **吕卡隆**：帝国末期（阿雷尼科斯时期）首都，代码实锤（IMPERIALCAPITAL＝town_ES4，A）；阿雷尼科斯死后首都暴乱、内战中皇位觊觎者皆盯上此城（WGtzBswe + 流浪者出身文，A）。

时间锚：潘德拉克战役＝正典 1077（德洛修斯·涅雷采斯战死于该役，04xAg0yN A 级）→ 元老院弃其子彭同、改选阿雷尼科斯将军继位 → 阿雷尼科斯"裁军改革"（bhxVA9w7）→ 离奇死亡 → 三宣称者内战（1084 开局）。

> 裁定后的链条自洽性检查：萨涅俄帕（东，内陆商路枢纽）承接"重心东移"✅；阿雷尼科斯出身珀特洛斯家（南方），迁都家族城吕卡隆与其"军事要地+银矿"属性吻合 ✅；潘德拉克 1077 涅雷采斯朝终结、阿雷尼科斯即位，两都更替有明确断代点 ✅。与全部 A 级文本零冲突。

---

## 四、逐条裁定（Max 09-12 已裁）

| Max 原说法 | 考据结论 | 裁定处置 |
|---|---|---|
| 沙拉斯＝最初登陆之地、建国之地 | 第一殖民地实锚；"建国"拔高 | 内核写"第一批殖民者登陆之处、第一块殖民地"；不写"建国" |
| 巴拉维诺斯＝数百年长期定都 | 首都地位实锚；时长无载 | 内核写"取代沙拉斯为首都、后重心东移仍为西部经济重镇"；"数百年"只进说法层或不写 |
| 巴拉维诺斯拼写 | 英文城名 **Paravenos**（统一）、战役名 Baravenos Encirclement 保留不动、简中巴拉维诺斯（5 处一致）、繁中自相不一致、今名帕拉汶德 | **已裁**：英文城名固定 **Paravenos**；Baravenos 仅存在于战役专名，不作城名拼写；中文一律"巴拉维诺斯"；禁用 Baravinlos（0 命中） |
| 萨涅俄帕＝德洛修斯时期首都 | 官方无载（B 级考据说法）；论证链：涅雷采斯家出过皇帝＋根据地在此＋内陆枢纽地位，与全部 A 级零冲突 | **已裁（Max 09-12）**：采纳，定为**涅雷采斯朝都城**；来源级别＝D 级开发者原创填空白，**须登记来源台账**；表达层写法见 §六 |
| 萨涅俄帕＝涅雷采斯家根据地 | ✅ A 级（开局领地＋寡头阵营） | 可进内核（措辞：涅雷采斯家的领地/重镇） |
| 吕卡隆＝阿雷尼科斯**建立**的新都 | 首都地位 A 级实锤；"建立"与官方冲突 | 改写为"阿雷尼科斯时期定都于此"；其"帕拉人故城、和平合邦"身世照官方写 |
| 吕卡隆＝军事要地、银矿丰富 | ✅ A 级 | 可进内核 |
| 吕卡隆＝平衡各方势力 | 官方无载 | 说法层或不写 |

## 五、裁定记录（§六 已并入本节前请以本节为准）

> 2026-09-12 23:40 前后，Max 三次口头裁定：
> 1. **萨涅俄帕＝帝国曾都**："由于涅雷采斯家曾经做过皇帝，且涅雷采斯家的根据地就在萨涅俄帕，萨涅俄帕的地理位置和重要程度也可见一斑，论证萨涅俄帕是帝国曾经的都城并不是什么问题。"→ 采纳为正典填充（D 级，登记）。
> 2. **巴拉维诺斯英文城名固定为 Paravenos**；Baravenos Encirclement 战役名保留不动，不作城名拼写。
> 3. 由此迁都链定稿：沙拉斯 → 巴拉维诺斯 →（重心东移，A）→ 萨涅俄帕（涅雷采斯朝，D）→ 吕卡隆（阿雷尼科斯时期，A 代码）。

> 2026-09-13 凌晨，**"铁臂/铁壁"奥斯里克裁定**（Max 质疑"官方不是铁臂吗"触发复核）：
> - **英文本源 = Osric Iron-Arm**（std_settlements_xml.xml），全语言皆"臂/拳"系（Eisenarm / Brazo Férreo / Pugno di ferro / Demir Kol…）。
> - **官方简中自混用 2:2**：生平叙述用"铁臂"（瓦兰迪亚文化文 IudGLWQZ"王位承袭自'铁臂'奥斯里克"；德泰尔领主台词 rKKAYQkJ"我的曾祖父'铁臂'奥斯里克"）；两座城描述文顺带提及时写"铁壁"（帕拉汶德 Nx0qK2g6、加伦 79R6gi4l）——意译走样。
> - 编年史原声（B 级）同样混用（铁臂 1 处、铁壁 2 处），系被官方污染。
> - **裁定：全项目叙述统一"铁臂"**（依英文本源，巴拉维诺斯拼写先例同款）；引文照原文保留（引 Nx0qK2g6 时"铁壁"不动，忠实引用原则）。成品档 paravenos.yaml revision 2 已改（summary + assertion-2），编译复证 runtime 铁臂 3 / 铁壁 0。
> - 附带发现（登记不采信）：rKKAYQkJ 称奥斯里克为"曾祖父"，与 200 年时间线不合，属领主台词口语夸张，只进说法层，不进内核。

**表达层写法约束（D 级内容的分层呈现）**：
- 内核层只陈述 A 级事实与"说法本身"：涅雷采斯家出过皇帝（德洛修斯）、其根据地在萨涅俄帕、萨涅俄帕是内陆贸易重镇——都是 A；"涅雷采斯朝定都于此"作为这个世界里**人们普遍的说法/史家的记述**呈现，不写"据推测"。
- 身份分层：帝国高层（贵族/士族 scope）可闻"萨涅俄帕曾为帝都"的完整叙事；平民/村民层只到"萨涅俄帕是大商埠、涅雷采斯老爷们的老家"（rumor 层）。
- 登记义务：D 级填空白须在来源台账登记"萨涅俄帕定都说＝开发者原创（Max 裁定 09-12）"，发布白名单三件套天然不含 sources，无外泄之虞。

## 六、引文清单（复核用）

| hash | stringId/落点 | 性质 |
|---|---|---|
| 0cZmKO76 | town_V7 描述文（沙拉斯） | 聚落描述 |
| Nx0qK2g6 | town_V3 描述文（巴拉维诺斯/帕拉汶德） | 聚落描述 |
| EJZLzhJk | town_EN3 描述文（萨涅俄帕） | 聚落描述 |
| WGtzBswe | town_ES4 描述文（吕卡隆） | 聚落描述 |
| 04xAg0yN | lord_1_5 彭同·涅雷采斯背景 | 英雄背景 |
| TI3fS2xN | 贾尔马律斯描述（旁证巴拉维诺斯） | 聚落描述 |
| DFW5i63X / PGa4Ehaa / axnObjSY / pD92hewA 等 8 条 | `{IMPERIALCAPITAL}` 流浪者出身文 | 出身文 |
| — | `LordConversationsCampaignBehavior.cs:1751` | 反编译源码 |
| — | `bannerlord_settlements` / `bannerlord_clans` / `bannerlord_kingdoms` 表 | 游戏数据 |

> 探针脚本：`.workbuddy/tmp/_capitals_probe*.py`（1–10 号，全部只读模式）。
