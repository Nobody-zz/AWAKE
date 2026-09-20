# 到「游戏基本玩法初步闭环」还差什么 · 2026-09-20

- 缘起：甲方 Max 14:03「梳理当前状态，然后给出当前到游戏基本玩法初步闭环的待完成项」
- 出具：全局主控线（**盘点与清单，不写实现**）
- **性质：这是读数 ＋ 待办清单，不是裁决。** 权威状态仍以 `docs/AWAKE-ROADMAP.md`「现状」节为准。
- ⚠ **口径**：文中所有状态**于 09-20 14:0x 实测**；凡未实测的一律写"未验"。

---

## 一、「基本玩法」指什么（用项目自己的口径，不另发明）

甲方 09-15 原话把一阶段定为：**NPC 对话、写信、事件、周报**。四件。
所以"初步闭环"＝**这四件在游戏里都能走完**，判据用项目自己那句
（`docs/AWAKE-GLOBAL-MECHANISM-AUDIT-20260827.md:445`）：

> **「可验证闭环＝每个功能都必须证明『入口 → 调用 → 结算 → 玩家可观察结果 → 存档恢复』。」**

⇒ 光"代码在"不算闭环；**必须游戏里看得见 ＋ 存读档后还在**。

---

## 二、游戏内的判据（看到什么算通）

| # | 看什么 | 看到什么算通 |
|---|---|---|
| 1 | 进游戏、AWAKE 在跑 | `Awake.log` 有 `register_ok`；会话跑满、干净退出 |
| 2 | MCM 选项页 | 能打开、6 组 36 项在 |
| 3 | NPC 对话 | `npc_dialogue_ready` → `npc_dialogue_turn_completed`（带 `hero/generation/correlation`）→ `transcript_turn_appended` |
| 4 | 写信 | 能发出、有送达与回信记录 |
| 5 | 事件 | 触发一次、进收件箱 |
| 6 | 周报 | 生成一份、打开能看 |
| 7 | 存档恢复 | 存盘 → 退出 → 读档，上面 3–6 的状态还在 |
| 8 | 干净 | 引擎 errors 只有表头；watchdog 只有等待行 |

---

## 三、逐环节实测状态（09-20 14:0x）

| 环节 | 代码 | 游戏内验过吗 | 卡在哪 |
|---|---|---|---|
| 1 进游戏 | ✅ | ✅ **09-14 23:14 跑通**（注册、跑满、干净退出） | — |
| 2 MCM 选项页 | ✅ | ❌ **未验**（09-14 记录明写「MCM 新排版也仍未实机看过」） | 只需开一次看 |
| 3 NPC 对话 | ✅ | ⚠ **半**：手动开过面板（`hero:lord_1_18`）；**主动对话张不开嘴** | `npc_dialogue_open_failed`（v0.3 卡这），09-18 起记"未见修复" |
| 4 写信 | ✅ `AwakeLetterService`／`NpcLetterInitiator`／`AwakeMessenger*` 齐 | ❌ **从未在游戏里触发过** | 需真机第一次验；另有一条已知小病（见下 A5） |
| 5 事件 | ✅ `AwakeEvent*`／`WorldEventInbox*` 齐 | ❌ **从未在游戏里触发过** | ★ **落盘是内存**（F-033）：调用点写 `EventDelivery.Durable`，背后是 `InMemoryEventService` ⇒ **退出即失忆** |
| 6 周报 | ✅ | ❌ **未验**（09-14 那次是坏的） | 离线已修（09-15 三笔），**待真机确认** |
| 7 存档恢复 | ✅ | ❌ **未验** | ★ 状态落 `AwakeState/unbound/`（绑了个空）；`AwakeFileStorageService.cs` **最后提交＝09-11 `0630a6d`，此后零改动** |
| 8 干净 | — | ✅ 09-14 引擎 errors 只有表头、watchdog 干净 | — |

**一句话读数：八项里，游戏内确认过的只有第 1 和第 8 两项。**

---

## 四、待完成项（**按"要等谁"分**，这才是能动的清单）

### A. 离线就能做，不用开游戏（6 项）

| # | 事项 | 为什么它在闭环上 | 证据／坐标 |
|---|---|---|---|
| A1 | ★ **事件落盘（F-033）** | 「事件」这条腿**退出即失忆**，直接违反第 7 项判据 | `DurableSpoolWriter` 在 `AWAKE/src` 与 `framework` **零命中**；`InMemoryEventService` 只在 `framework/.../src/HostApi.cs`。09-15 已定"搬 `Core/DurableSpoolWriter.cs`"，**至今未搬** |
| A2 | ★ **`g3-s0-focused-readiness` 那条红** | 它与第 7 项同区（存储命名空间归属） | 实测 FAIL_MSG ＝ `Missing required namespace must not reuse the stale owner.`（`Program.cs:3675` 起，测 `PersonaState/Transcript/Contacts` 三命名空间） |
| A3 | **P1-3**：`NpcDialogueVM.cs:724` 仍是 `CancellationToken.None` | 「对话」这条腿的会话生命周期 | 09-15 就点了名，**10 天未修**（实测仍在原处） |
| A4 | **工具候选（F-031）接线** | v0.3「活起来」的素材：工具接上，NPC 才谈得上"做事" | 真件 `HostToolCandidateService` 在框架、**玩法侧 0 调用**；09-15 定"纯接线，性价比最高一件" |
| A5 | **写信入口只对"远方"开** | 影响第 4 项可观察结果 | `AwakeMessengerVM:687` `CanWriteLetter = … && !contact.IsNearby`；而远方一律 `DeliveryDelayFor=1` ⇒ **玩家写的信 100% 次日达，"当日达"分支永不可达**（09-13 实测） |
| A6 | **`dialogue-chain-redtest` 判据对齐** | 它是"对话链"的回归闸 | 实测 FAIL_MSG ＝ `deployed manifest schemaVersion must be awake.worldbook.v2, got=awake.worldbook.registry.v1`（`DialogueChainRedtest.cs:274 ← :221`）⇒ **判据没跟 09-14 的决定走**；先定哪个形态对，再决定改判据还是改包 |

> **测试套件现状（09-20 14:04 重编后实测）**：`total=64 / passed=62 / failed=2`，红＝`g3-s0-focused-readiness`、`dialogue-chain-redtest`
> ⇒ **09-19 23:03 `5eca516` 把 6 条红降到 2 条**（4 条 persona 红收口）。剩下这两条**都在上面 A 档**。

### B. 必须开一次游戏（攒一次一起看，5 项）

| # | 事项 | 看什么 |
|---|---|---|
| B1 | ★ **`npc_dialogue_open_failed`** | 主动对话能不能真的张开嘴（v0.3 卡这）。**离线复现不了**（Gauntlet UI） |
| B2 | ★ **存档绑 `unbound`** | 状态有没有落进**带存档 id 的目录**（v0.2 卡这）。⚠ 是"绑在认出存档 id 之前"的推断，**未证实** |
| B3 | **周报确认** | 09-15 离线三笔（`bae3841`／`25de029`／`437bedd`）修好的根，在游戏里再看一次 |
| B4 | **写信 ＋ 事件 第一次验** | 这两件**从进游戏到现在，一次都没触发过** |
| B5 | **MCM 选项页** | 6 组 36 项能打开（v0.1 判据之一，09-14 漏看） |

> 建议**一次开机把 B1–B5 串着看**（同一局里依次触发即可），不要分五次。
> ⚠ 时间戳是 UTC，比北京时间晚 8 小时；每次启动一个新 pid ⇒ 先按 mtime 找最新的日志。

### C. 等甲方一句话（2 项，批了当天能动）

| # | 事项 | 现状 |
|---|---|---|
| C1 | **世界书包投不投游戏目录** | 游戏目录那份停在 **448**；仓库现役 **482**；而世界书线 09-20 已把编译推到 **`geo1-v22-links` 558 档 ＋ 互引边 1286 条**（**只落在 `compiled/`，未上线**）。⇒ 现在有三个档位可选（448／482／558） |
| C2 | **互引边接召回的运行时改动怎么收口** | 代码**已在工作区**（`WorldKnowledgeLoader.cs`／`WorldKnowledgeModels.cs`／`WorldKnowledgeQueryService.cs`，+94 行），**未提交、未上线** |

---

## 五、建议顺序（只说先做哪个，不排期）

1. **先做 A 档的 A1 ＋ A2**——这两件是"离线能补、且直接卡在闭环判据上"的，性价比最高。
2. **A3／A5／A6 是小的定点修**；A4 是接线；可以并进同一轮。
3. **A 档清完再开一次游戏**，把 B1–B5 串着看一遍。⚠ **离线全绿不等于闭环成立**，不得标记"实机完成"。
4. C1／C2 等甲方一句话。

⚠ **第三阶段及以后一律不排期**（甲方 09-15 明示；`docs/DECISION-20260915-PHASE3-BOUNDARY.md`）。

---

## 六、A 批次执行结果（09-20 14:4x 补 · 甲方令「做离线A批次」）

### ✅ 做完 3 件（提交 `3db0917`；测试套件 **64/64 全绿**，`PASS ALL Awake.SdkSmoke`，退出码 0）

| 项 | 结果 |
|---|---|
| **A2** `g3-s0-focused-readiness` 红 | ✅ 已修。真因：候选打开失败时，那个**同样凑不齐**的旧 owner 没被退役、继续对外冒充"已就绪"。新增 `RetireUnsatisfiedExistingWorldStateStoreAsync`（清发布 ＋ 异步 drain，仅在"仍是当前 owner 且会话代次未变"时动手）。**变异检验通过**（注释掉调用 ⇒ 重回红） |
| **A3** P1-3 | ✅ 已修。`NpcDialogueVM` 增 `_sendCts`：`ExecuteSend` 先立新的再取消旧的、`OnFinalize` 取消、`SendAsyncSafe` 接 token 且把取消与错误分开记日志。**服务侧无需改**（`SendAsync` 本就 `catch (OperationCanceledException)` → `ClearActive()` → 返回 `npc_dialogue.cancelled`）。**无回归** |
| **A6** `dialogue-chain-redtest` | ✅ 已修。**判据陈旧两处，均非实现回退**：① `:221` 期望写 `awake.worldbook.v2`，09-14 定案落地的是 `registry.v1`；② `registry.v1` 是**存根**，原实现拿存根直接 `ReadAndVerify` ⇒ 报 `WB2-SCHEMA-UNSUPPORTED:manifest`，应走「Load 存根 → Select 选包（重算三 hash）→ LoadVerified」。**变异检验通过**（`Select` 传不存在的包 id ⇒ 红 `WB2-WORLD-CONFLICT:requested_package_missing`） |

### ❌ 另 3 件经实测证明**不在 A 档范围内**（原判断有误，此处更正）

| 项 | 实测结论 |
|---|---|
| **A1 事件落盘** | ❌ **不是"搬一个文件"**。① `marcus_repo` 全树**没有** durable/spool 源文件（`find -iname '*durable*' -o -iname '*spool*'` 零命中）；② `InMemoryEventService`（`HostApi.cs:909`）本身就是**进程内 pub/sub**（`Dictionary<string, List<Action<EventEnvelope>>>`），要"Durable"等于**新写一个框架层组件**（落盘 ＋ 重放）；③ 玩法侧只有 **1 处**调用（`WorldStateStore.cs:2502`）。⇒ **升级为独立项**，须先定"落盘形态"（走 storage 面？新开 spool 文件？） |
| **A4 工具候选** | ❌ **不是"接一行"**。框架件是 `IToolCandidateService.Validate(candidate, allowedToolIds, context, currentTurn, …)`（`HostApi.cs:672`）—— 接它的前提是**玩法侧先有"工具候选"这条流程**（什么算候选、允许哪些工具、校验过了怎么执行）。⇒ 属**功能开发**，要先设计再接线 |
| **A5 写信入口** | ⚠️ **不是 bug**。`CanWriteLetter = … && !contact.IsNearby` ⇒「**当面谈／远方写信**」是**有意设计**（UI 文案就写着「远方联系人；可写信。」`AwakeMessengerVM.cs:701`）。且 09-13 记的那条依据 `DeliveryDelayFor` 在**现行 `src` 检索零命中** ⇒ 该记录已过时，**不据此动手** |

### ⚠ 额外更正（本次实测）

游戏目录那份世界书包当前是 **482 档**（红测输出：`entries=482 package=awake:worldbook.calradia`）
⇒ **roadmap 现状节与本文 §四 C1 记的"游戏目录那份还停 448"已过时**。
⇒ C1 的选项应改写为：**要不要把 09-20 新编的 558 档（军事 25 ＋ 经济 40 ＋ 暗面 11 ＋ 互引边 1286 条）投上去**。

**B 档（必开一次游戏）与 C 档（等甲方一句话）其余项不变。**

---

## 附 · 本次实测的证据出处

- 测试读数：09-20 14:04 重编（`build.ps1 -Configuration Debug`）后实跑 `AWAKE.Tests/bin/Debug/net472/Awake.SdkSmoke.exe`，`total=64 passed=62 failed=2`，退出码 1
- 两条红的真因：同一次运行的 `FAIL_MSG` 原文（上表 A2／A6）
- `src` 最后提交：`git log -1 -- AWAKE/src` ＝ `0bff7b9`（09-18 10:05）；`AwakeFileStorageService.cs` ＝ `0630a6d`（09-11 11:55）
- `src` 未提交改动：`git status --porcelain -- AWAKE/src` ＝ 3 个文件（互引边召回，`git diff --stat` ＋94 行）
- 事件落盘缺口：`DurableSpoolWriter` 在 `AWAKE/src` 与 `AWAKE/framework` 检索零命中
- 框架→玩法断口清单：`AWAKE/docs/AUDIT-MARCUS-CAPABILITY-LIVENESS-20260915.md`；流水 `.workbuddy/memory/2026-09-15.md:602-618`
- 闭环五段定义：`docs/PLAN-OFFLINE-CLOSURE-20260915.md` §二（引 `AWAKE-GLOBAL-MECHANISM-AUDIT-20260827.md:445`）
- 写信入口限制：`.workbuddy/memory/2026-09-13.md:785`
- MCM 未验：`.workbuddy/memory/2026-09-14.md:1662`
- 世界书 v22：`.workbuddy/memory/2026-09-20.md`（11:5x–12:0x 节）＝ `EDGE-RECALL-REPORT-20260920.md`
