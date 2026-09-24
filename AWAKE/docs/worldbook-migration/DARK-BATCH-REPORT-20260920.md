# 暗面批（城镇地下秩序）· 完工报告（2026-09-20）

给甲方：本批按 `ECONOMY-SYSTEM-SCOPE-20260920.md` §七建议的顺序，做**经济体系层的第 ④ 段「暗面」**——巷子、帮派、犯罪、走私、农奴、强盗、小阵营，卡拉迪亚「法外的那一半」。
已推到「编译 ＋ 探针」全绿。**未上线**（现役包仍是 482 档的 v18 基线）。

---

## 一、交付内容

**11 档**入库，**11 条断言 / 24 条表达 / 22 条关键词**：

| # | 档 | 域/子域 | 断言性质 |
|---|---|---|---|
| 1 | 巷子 `underworld-alleys` | politics/law | fact |
| 2 | 巷子头目 `underworld-gang-leaders` | politics/law | **interpretation** |
| 3 | 巷子争夺 `underworld-struggle` | politics/law | fact |
| 4 | 帮派 `underworld-gangs` | politics/law | fact |
| 5 | 犯罪等级 `underworld-crime-rating` | politics/law | fact |
| 6 | 赎罪金 `underworld-blood-money` | politics/law | fact |
| 7 | 走私货 `underworld-smuggling` | economy/trade | fact |
| 8 | 要人 `notables` | culture/identity | fact |
| 9 | 农奴 `serfs` | culture/identity | **interpretation**（3 层） |
| 10 | 强盗 `underworld-bandits` | politics/law | **interpretation**（3 层） |
| 11 | 小阵营 `small-factions` | politics/diplomacy | fact |

**本批真正要补的不是「一批词条」，是两根空轴。**开工前实测现役包：`era`／`lifecycle`／`status` 编译后**全为 0**（「何时知道」无载体）、`secret` 表达**仅 2 条**（「信不信」没料）。而钱与权的事天生多说法——同一件事，不同的人嘴里是两回事。本批据此把「说法分层」做成可验收的形态。

## 二、验收读数（当场实测，非记忆）

- **引文校验**：A 级（游戏本体 CN 原文）**56 条** ＋ B 级（编年史变体）**7 条** ＝ **63 条，逐条比对出处原文，全部命中**。
- **双写**：现役目录与镜像目录 11 档 `diff` **逐字节一致（0/11 不一致）**。
- **编译**：`Valid=True`、`error=0`；产物 `compiled/geo1-v21-dark`，**558 档**；`documents.json` 558 篇与条目数一致；21 条诊断**全为 warning**（`WB-INDEX-AMBIGUOUS`，堡与村同名，**涉本批 0 命中**）；`audit-report` 零事件。
- **增量**：对现役包（482 档）**新增 76 档、消失 0 档**。76 ＝ 本批 11 档 ＋ 上一轮军事 25 档 ＋ 经济 40 档。
- **探针（逐 grant 驱动，110 问 × 2 模式）**：真实能力模式下 **可达 110 / 被压制 0 / 不达 0**；把身份×文化×王国×角色这一维单独拉出来测（钉死 scope/detail）亦 **110/110 全可达** ⇒ 我写的每一道门都被对应受众打中。
- **★ 说法分层阳性对照（本批新做，专门证「信不信」这一轴真的在动）**：拿 `serfs`／`underworld-bandits` 两档，**同一句问话**（`农奴`／`强盗`）用三种身份去问，断言三次返回正文互不相等。

  | 问话 | 村民 | 城镇居民 | 贵族 |
  |---|---|---|---|
  | 农奴 | 「我们就是所谓的农奴……老爷还给我们份地，交完公粮剩下的都归自己」 | 「城里人各有各的口吻……君君臣臣，都是有契约的」 | 「太文绉绉了。他们一般直接叫他们屁民、群氓、交税工具、后备兵源」 |
  | 强盗 | 「劫匪三三五五聚起来……谁都打不过，也就欺负欺负村民」 | 「沙漠里那些不一样……不肯低头去给人当奴隶兵，就骑着马进了沙丘」 | 「林子里的那批……破烂布衣，少数人裹着偷来的破损皮甲」 |

  **结论：2/2 档通过，「不同说法数」均为 3/3。**三种身份不只拿到三段不同的字，而且正落在 `rumor`／`summary`／`detail` 三层上——「谁知道」与「信不信」两轴同时生效。
- **名录**：`_dark_roster_20260920.json`——politics 域 **7 → 15 档**、culture **6 → 8 档**、economy **61 → 62 档**；全包 547 → 558。

## 三、本批的做法（三条硬约束，可复用）

1. **受众必须互斥。** `SelectExpression` 每档只送一条表达，`分数 = 规则分×10 ＋ 层号`，**同分取先** ⇒ 同一身份被两层都挂上，**层号低的那条永远送不到人耳**。本批 `serfs`／`underworld-bandits` 用三层：`rumor`→(普通平民, 普通村民)、`summary`→(城镇居民)、`detail`→(地方重要人物, 商人, 酒馆老板, 赎金经纪人, 头人, 士兵, 贵族)。生成器里对「同时有 rumor 与 summary 的档」**自动把 townsfolk 从 rumor 层摘掉**，免得自己压自己。
2. **能力上限不许越。**逐行读一手 `WorldbookIdentityCapabilityRules.cs`：`villager/commoner=(local,rumor)`｜`townsfolk=(regional,summary)`｜`headman/soldier=(national,detail)`｜`notable=(regional,detail)`｜`merchant/tavernkeeper/ransom_broker=(faction,detail)`｜`noble=(elite,detail)`。序：scope `local<regional<national<faction<elite<private`；detail `rumor<summary<detail<secret`。
3. **说法必须出自原文**，不许我们自己编排对立。`serfs` 的三条直接对应编年史 `rule_农奴` 的四个变体（农奴自述「我们过的都挺好的」／外人叹「生生世世都是贵族的奴隶」＋「君君臣臣，都是有契约的」／体面人口中的「屁民、群氓、交税工具、后备兵源」）。

另按甲方裁定收紧成一条更安全的：**数值只在官方文本里有的才写。**`underworld-crime-rating` 的 **100 与 30** 出自官方正文；其余机制数值（巷子收益「城越兴旺、进项越多」、赃物「半价」）一律**定性表述，不写成公式**。

## 四、本批暴露、但未擅自改的两件事

1. **★ 分类表没有「法外／地下」这一格。**现存五域（politics / economy / culture / war / geography）只能把暗面 11 档挤进 `politics/law ×7`、`culture/identity ×2`、`economy/trade ×1`、`politics/diplomacy ×1`。建议以后补 `politics/lawlessness` 或 `culture/underworld`。**本次不动**——按项目记录，分类表一改 Studio 包就过期（`scripts/package.ps1` 把整个 plan 目录拷进包 `schemas/`），要另走一次定向刷新。
2. **强盗是「空手身份」。**能力表里 `bandit` 角色已预留（`=(local, rumor)`），但包里的身份清单**只有 12 条，没有 `awake:identity:bandit`** ⇒ 强盗理论上拿不到知识。补它要动身份登记表 ⇒ **全部 558 档的 `registry_bindings.profile_registry_hash` 一并作废**，属专项，单独立项。**本批只记账。**（本批 10 档不受影响，`underworld-bandits` 档改用村民／镇民／贵族三层说话，不依赖这个身份。）

## 五、取齐但未立档的料（交接给下一段）

- **一手取数打出的官方 CN 全文快照**：`巷子` 46 条（含接管／失去／被攻打／需 Roguery ＋ Mercy 门槛／一聚落最多一条／头目控制后街·移民棚户区·滨水区等文案）——本批只用了一部分。
- **B 级编年史暗面素材**：`rule_山贼`、`rule_沙漠强盗`（2 变体）、`rule_海寇`、`rule_湖鼠帮`、`rule_绿林强盗`、`rule_劫匪`；另有 `rule_阿塞莱阶级与奴隶制`——**含成人向段落，本批及以后都不用**。
- **巷子在田野里的实际归属**（哪几座城归哪个帮派）未取。若要写「某城现在归谁」，得现查。

## 六、未做 ／ 搁置

- **经济体系层余下三段**（①行情与消息 ②税与抽成 ③豪门与权）——甲方此前划出、本次未指定，仍未开工。
- **分类表加格**、**`profile.bandit` 身份补登记**——见 §四，均属专项。
- **回补已上线的 5 件货品档与 5 档牲畜档**（盐／毛皮／天鹅绒／香料／银矿石；牛／羊／骡／旅行马／驮运骆驼）——经济批规格列为第二段，未动。
- **不上线**：`geo1-v21-dark` 只落在 `compiled/`，未复制进 `ModuleData/Worldbook/packages`。
- **未提交**：按纪律不主动提交，等甲方发话。

## 附：产物与脚本

- 包：`tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v21-dark/`（`Valid=True`、`error=0`、21 warning）
- 规格：`docs/worldbook-migration/DARK-BATCH-SPEC-20260920.md`
- L2 数据：`docs/worldbook-migration/projection/authoring-out/_dark_A_20260920.json`（11 档，按档自带域/子域）
- 生成器：`docs/worldbook-migration/projection/authoring-out/_gen_dark_20260920.py`（双写 ＋ 引文校验 ＋ 别名死条硬断言 ＋ 按档分域 ＋ 三层表达）
- 链式编译：`tools/_dark_chain_20260920.py`（`OPBASE=dark20260920a`）；探针：`tools/_dark_gate_probe_20260920.py`（支持 `--pure`）；名录：`tools/_dark_roster_20260920.py`
- **说法分层阳性对照：`tools/_dark_split_check_20260920.py`**（本批新做，可复用）
- 一手／二手取数脚本（`tools/_dark_*_20260920.py`）：DB 全表摸底、简中暗面关键词扫描（巷子 46／帮派 57／犯罪 21／走私 24…）、巷子文案全打、补词（棚户／滨水／赃物／敲诈…）、编年史暗面挖掘、专名存在性核对
