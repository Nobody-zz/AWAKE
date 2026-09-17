# -*- coding: utf-8 -*-
"""给 DECISION-20260917-设计初衷-知识传播与对话.md 追加：已拍板 + 底座盘点 + 第一步 + 不做什么。

依据全部来自真代码（本仓 src/），每条都写清文件与行号，便于复核。
"""
import io
import sys

PATH = r"D:\AWAKE-Dev\AWAKE\docs\DECISION-20260917-设计初衷-知识传播与对话.md"

BLOCK = """
## 六 已拍板（甲方 09-17）

| 岔口 | 决定 |
|---|---|
| 传播按"人"还是按"地"算 | **混着来** —— 有名字的那层**按人算**，无名群众**按地批量算** |
| 传播由什么驱动 | **事件为主、时间为底线** |

⇒ §四 那两个问题（连同我当时的倾向）**到此为止，按这个执行**。

## 七 ★ 底座盘点：这条链的零件其实都已经有了（读真代码，09-17）

**先说结论**：**"谁知道／何时知道／从谁听来"三段各有现成件，"把一件事从出生点发出去"这一根轴没有。**
缺的不是一个系统，是**一根轴**。

| 段 | 现成的件 | 落点（可复核） | 状态 |
|---|---|---|---|
| **谁知道** | 身份 → 能知范围，**两级**：`scope`（`local`／`regional`／`national`）× `detail`（`rumor`／`summary`／`detail`） | `WorldbookIdentityCapabilityRules.Resolve(role, isNoble, age, management)` | **已建**。★ 注意 `detail` 里**本来就有 `rumor` 这一档** —— "听来的糙版本"这个概念早就在词汇表里，只是没人拿它当真 |
| **何时知道** | 信使时钟：**距离 ÷ 速度**。`CourierMapUnitsPerHour = 8`（＝192 地图单位/天；中位城际 270 单位 ⇒ **34 小时**；横穿大陆 ≈ **3.5 天**） | `AwakeLetterService`（注释里连依据都写了：`BaseSpeed = 4` 是"每游戏小时"） | **已建**，而且**只服务"给玩家的信"** |
| 同上 | 消息在路上的状态机：先 `sent`，到点由 `AdvanceAsync` 转 `delivered` | `AwakeLetterService.CreateInboundAsync(travelHours)` | 已建 |
| **从谁听来** | 记忆条目自带**来源与时间**：`HeroId`／`Day`／`Source`／`EntrySource`／`ConversationId`（形状 `event|hero|source|guid`）／`Weight`／`Type` | `WorldStateStore.MemoryReservation` | **字段已建** |
| 出生点 | 事实账本：`FactId`／`EventKey`／`Day`／`TimeSlot`／`Kind`／`Entities(type,id,role)`／`PresentationSummary` | `WorldFact`（`WorldFactCapture` / `WorldFactJournal`） | 已建 |
| 每日钩子 | 每小时 tick 里已有"按天结算"这一句（**时间为底线的落点就在这**） | `AwakeEventBehavior` → `NpcMemoryService.ConsolidateDailyForNearbyHeroesAsync` | 已在跑 |

**缺的那一段（已核过，不是猜）：**

1. **「事件 → 某个人的记忆」写好了，没人调。**
   `NpcMemoryService.RecordEventFactAsync`（第 334 行）**全仓零调用方**（grep `EventFact` 的其余命中全是另一个类型 `AwakeEventFactTrigger`）。
   ⇒ 通往 `AppendEventMemoryAsync` 的路**修好了但没接上**。
2. **「每天给谁结算」名不副实。**
   `ConsolidateDailyForNearbyHeroesAsync` 名字说"就近"，实现是遍历 `Campaign.Current.CampaignObjectManager.AliveHeroes`、**取前 8 个 `break`**，
   **没有任何距离／位置筛选**。⇒ 叫"就近"，实为"前 8 个活着的英雄"。
3. **事实账本只进不出。**
   现在读它的只有**事件引擎与终端**（挑事件候选、查最近动态）；**没有一条路把它变成"别人知道了这件事"**。

⇒ 这三条合起来就是一句：**"谁知道"有了、"怎么算到达时间"有了、"来源字段"有了，**
**中间那根"把事实按关系与距离发出去"的轴没有。** 而那根轴就是你要的**传播路径**。

## 八 第一步（提案，**等你点头再动**）

只做这一根轴，**不铺开**：

1. **一件事 → 算出"该知道的人"**：距离直接用信使那套地图坐标（`CampaignVec2`／`Settlement` 位置），关系用 `BannerlordNativeSocialReader`（已有）。**不新建系统。**
2. **到达时间 ＝ 距离 ÷ 8 单位/小时**（信使常数**直接复用**），落在 `Day`／`TimeSlot` 上。
   ⇒ 天然产生「**他还没听说**」这个**合法状态**——不是"查不到"，是"还没传到"。
3. **到点写记忆**：调那个没人调的方法（`AppendEventMemoryAsync`），`Source` 写成 `heard_from:<谁>`。
   ⇒ **"从谁听来"这时候才有内容**（今天只有 `npc_dialogue`／`event` 这种粗来源）。
4. **按地批量**：无名群众**不逐人存**；聚落级挂一张"本村已知"表，
   `scope = local` ＋ `detail = rumor` 的人读它。（这正好对上第 1 条的七维里那两维。）
5. **时间为底线**：每日 tick 已在跑 ⇒ **底线＝每 N 天至少推进一档**，保证消息不永远卡住；
   但**主驱动是事件**（他刚拍的那条）。**不做"纯时间扩散"**——那样每条消息迟早全大陆都知道，地域差和"我不知道"全没了。
6. **走样**：`detail` 降一档（`detail → summary → rumor`）＝**传一手掉一层**；
   传到第三手只剩 `rumor` —— 正好对上普通村民本来就只配 `rumor`。

**离线能证死的判据（不开游戏）：**
- 给定一个事实与一个聚落，**到达时间是算得出来的确定值**（距离 ÷ 8），可逐条对照；
- 给定两个身份（领主 vs 村民），**同一件事拿到的 `detail` 档不同**（现有身份层直接判得出）；
- **"还没传到"的人查不到** —— 这一条正好把上次那条 `not_found` 从"召回的病"翻成**正确行为**。

## 九 不做什么（先划掉，免得又跑偏）

- **不做"每个 NPC 主动调模型学习"**（既有口径已否：成本高、不符周报/事件统一更新策略）。
- **不做逐人存储无名群众**（数量爆掉，玩家也看不出差别）。
- **不重做身份／权限层** —— 它已经建好了，**它就是"谁知道"**；上次把它当噪声是错的。
- **不把这条链的验收挂在检索命中率上**（命中率只是"取数段"的健康度）。
"""

with io.open(PATH, "r", encoding="utf-8") as fh:
    text = fh.read()

if "## 七 ★ 底座盘点" in text:
    sys.exit("已追加过，未改动")

text = text.replace("## 四 待甲方拍板的两个岔口（这两个决定第一批建什么）",
                    "## 四 待甲方拍板的两个岔口（这两个决定第一批建什么）"
                    "　**⇒ ✅ 09-17 已拍板，见 §六**")
if "✅ 09-17 已拍板，见 §六" not in text:
    sys.exit("§四 标题锚点没找到，未改动")

if not text.endswith("\n"):
    text += "\n"
with io.open(PATH, "w", encoding="utf-8", newline="\n") as fh:
    fh.write(text + BLOCK)

print("已追加 §六～§九，新增 %d 行" % (BLOCK.count("\n") - 1))
