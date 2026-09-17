# -*- coding: utf-8 -*-
"""把"传播层拍板＋底座盘点"写进记忆：当日日志 + TOPIC-CODE。"""
import io
import sys

MEM = r"D:\AWAKE-Dev\.workbuddy\memory"
TOPIC = MEM + r"\TOPIC-CODE.md"
DAILY = MEM + r"\2026-09-17.md"


def load(p):
    with io.open(p, "r", encoding="utf-8") as fh:
        return fh.read().split("\n")


def save(p, lines):
    with io.open(p, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))


DAILY_BLOCK = """
## 20. 传播层：拍板 ＋ 底座盘点（09-17 上午）

**甲方拍板**：① 传播**混着来**（有名字的按人算、无名群众按地批量）；② **事件为主、时间为底线**。

**★ 底座盘点（读真代码，结论已在 `DECISION-20260917-设计初衷` §七）——零件几乎都有，缺一根轴：**

| 段 | 现成件 | 状态 |
|---|---|---|
| 谁知道 | `WorldbookIdentityCapabilityRules.Resolve(role,isNoble,age,management)` → `scope(local/regional/national) × detail(rumor/summary/detail)` | **已建**；`detail` 里本来就有 `rumor` 一档＝"听来的糙版本"早有词汇 |
| 何时知道 | `AwakeLetterService.CourierMapUnitsPerHour = 8`（192 单位/天；中位城际 270 单位 ⇒ 34 h；横穿大陆 ≈3.5 天）；`CreateInboundAsync(travelHours)` → `sent`，`AdvanceAsync` 到点转 `delivered` | **已建，但只服务"给玩家的信"** |
| 从谁听来 | `WorldStateStore.MemoryReservation`：`HeroId`／`Day`／`Source`／`EntrySource`／`ConversationId`（`event|hero|source|guid`）／`Weight`／`Type` | **字段已建** |
| 出生点 | `WorldFact`：`FactId`／`EventKey`／`Day`／`TimeSlot`／`Kind`／`Entities(type,id,role)`／`PresentationSummary` | 已建 |
| 每日钩子 | `AwakeEventBehavior` 小时 tick → `NpcMemoryService.ConsolidateDailyForNearbyHeroesAsync` | 已在跑 |

**缺的那一段（三条，已核过）：**
1. **`NpcMemoryService.RecordEventFactAsync`（:334）全仓零调用** ⇒ "事件→某人记忆"的路**修好没接上**
   （grep `EventFact` 其余命中全是另一类型 `AwakeEventFactTrigger`）。
2. **`ConsolidateDailyForNearbyHeroesAsync` 名不副实**：遍历 `AliveHeroes` **取前 8 个 break**，
   **无任何距离／位置筛选** ⇒ 叫"就近"实为"前 8 个活着的英雄"。
3. **事实账本只进不出**：读它的只有事件引擎与终端，**没有一条路把它变成"别人知道了"**。

⇒ 一句话：**"谁知道／何时知道／从谁听来"各有件，"把事实按关系与距离发出去"那根轴没有** —— 那根轴就是传播路径。

**第一步（提案，等甲方点头，写在该文档 §八）**：距离÷8 算到达时间 → 到点写记忆、`Source = heard_from:<谁>` →
无名群众按聚落挂"本村已知"表（`local + rumor` 的人读它）→ 每日底线"每 N 天推进一档" → `detail` 降档＝传一手掉一层。
**离线判据**：到达时间可算可对／两种身份拿到的 `detail` 档不同／**"还没传到"的人查不到**
（这条把上次那条 `not_found` 从"病"翻成**正确行为**）。
**不做**：每 NPC 主动调模型学习／逐人存无名群众／重做身份层／把这条链的验收挂在命中率上。

**文档**：`docs/DECISION-20260917-设计初衷-知识传播与对话.md` 追加 §六（已拍板）／§七（底座盘点）／§八（第一步）／§九（不做什么）。**未提交。**
"""

TOPIC_BLOCK = """
## ★ 知识传播层（09-17 起）—— 底座、缺口与第一步

> 起因：甲方重申设计初衷「**用 AI 模拟真实知识传播路径和对话**」。详见
> `docs/DECISION-20260917-设计初衷-知识传播与对话.md`（原话＋底座盘点＋第一步）。
> **本节的检索数字（语义链那几节）是"取数段的健康度"，不是成败标准。**
> 甲方已拍板：**传播混着来**（有名字的按人、无名群众按地批量）＋**事件为主、时间为底线**。

**★ 读真代码盘出来的三段（零件都在，缺一根轴）**

- **谁知道**（已建）：`WorldbookIdentityCapabilityRules.Resolve(role,isNoble,age,management)`
  → `scope`（`local`／`regional`／`national`）× `detail`（`rumor`／`summary`／`detail`）。
  ★ `detail` 里**本来就有 `rumor` 一档** ⇒ "听来的糙版本"这个概念早就在词汇表里，只是没人当真用。
- **何时知道**（已建）：`AwakeLetterService.CourierMapUnitsPerHour = 8` ⇒ 192 地图单位/天；
  中位城际 270 单位 ⇒ **34 小时**；横穿大陆 **≈3.5 天**（依据写在注释里：`BaseSpeed = 4` 是"每游戏小时"）。
  `CreateInboundAsync(travelHours)` 先落 `sent`，`AdvanceAsync` 到点转 `delivered`。
  ⚠️ **只服务"给玩家的信"**，NPC 之间没接。
- **从谁听来**（字段已建）：`WorldStateStore.MemoryReservation` = `HeroId`／`Day`／`Source`／`EntrySource`／
  `ConversationId`（形状 `event|hero|source|guid`）／`Weight`／`Type`。写入 `AppendEventMemoryAsync` / `FlushMemoryFactsAsync`。
- **出生点**（已建）：`WorldFact` = `FactId`／`EventKey`／`Day`／`TimeSlot`／`Kind`／`Entities(type,id,role)`／`PresentationSummary`。
- **每日钩子**（已在跑）：`AwakeEventBehavior` 小时 tick → `NpcMemoryService.ConsolidateDailyForNearbyHeroesAsync`。

**★ 三个缺口（已核，别再猜）**

1. **`NpcMemoryService.RecordEventFactAsync`（:334）全仓零调用** ⇒ 事件→记忆的路**修好没接上**。
   （grep `EventFact` 的其余命中全是另一类型 `AwakeEventFactTrigger`，别被名字骗。）
2. **`ConsolidateDailyForNearbyHeroesAsync` 名不副实**：遍历 `Campaign.Current.CampaignObjectManager.AliveHeroes`
   **取前 8 个 `break`**，**无任何距离／位置筛选**。
3. **事实账本只进不出**：读它的只有 `AwakeEventEngine`／`AwakeTerminalBehavior`，**没有一条路把它变成别人的记忆**。

⇒ **结论：缺的不是系统，是一根轴** —— "把事实按**关系＋距离**发出去"。

**★ 第一步（提案，等甲方点头）**：距离 ÷ 8 算到达时间 → 到点写记忆、`Source = heard_from:<谁>` →
无名群众按聚落挂"本村已知"表（`local + rumor` 的人读它）→ 每日底线"每 N 天推进一档" →
`detail` 降档＝**传一手掉一层**（第三手只剩 `rumor`）。
**离线可证死**：到达时间是确定值／两种身份拿到的 `detail` 档不同／**"还没传到"的人查不到**。

**★ 不做**：每 NPC 主动调模型学习（既有口径已否）／逐人存无名群众／重做身份层（**它就是"谁知道"**，
上次把它当噪声是错的）／把这条链验收挂在检索命中率上。
"""


def append_block(path, head_marker, block, title):
    lines = load(path)
    if any(head_marker in ln for ln in lines):
        print("%s 已写过，跳过" % title)
        return
    if lines and lines[-1].strip() != "":
        lines.append("")
    lines.extend(block.strip("\n").split("\n"))
    save(path, lines)
    print("%s 已更新" % title)


append_block(DAILY, "## 20. 传播层：拍板", DAILY_BLOCK, "当日日志")
append_block(TOPIC, "## ★ 知识传播层（09-17 起）", TOPIC_BLOCK, "TOPIC-CODE")
print("完成")
