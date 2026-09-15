# AWAKE 远程信件送达闭环

- 批次：`AWAKE-LETTER-DELIVERY-20260913`
- 状态：`implemented_pending_signoff`（2026-09-13 实施完毕，构建 0/0、smoke **29/29**；同日修订：① 送达由常量为**按距离的固定公式**、精度到游戏小时；② 信使速度按原版骑兵基准校准为 **8 地图单位/小时**；③ 补齐**主动来信的生产者**（`NpcLetterInitiator`，§5.1），入站信可带在途时间，未读判据收紧为仅 `delivered`）
- 范围：**仅 AWAKE 运行时服务层**（`src/`，plus 离线 smoke）。**不含任何 UI**：Prefab、VM、绑定属性、语言文件均不在本批。
- 依据：`docs/AWAKE-Version-Roadmap-0.2.0-to-1.0.0-20260817.md` §6.2「远程写信正式加入送达流程」、§6.3「信件记录创建时间、发送者、接收者、送达、已读、回复关联、过期和失败原因」。

## 1. 现状（实查）

| 事实 | 证据 |
| --- | --- |
| 写信＝往 transcript 追加一行 | `AwakeLetterService.SendAsync` → `AwakeTranscriptService.AppendLetterAsync` |
| 无送达、无未读、无回复关联 | 全 `src` grep `unread` / 未读 **0 命中** |
| `AwakeLetterService.cs` 仅 64 行 | 只有字节数校验 + 一次 append |
| transcript 已有 `source="letter"` 合法值 | `AwakeTranscriptModels.cs:22` |
| 逐小时 tick 挂钩存在 | `AwakeEventBehavior.OnHourlyTick`（已调 `ConsolidateDailyForNearbyHeroesAsync`） |
| 地图距离可得 | 原版 `MapDistanceModel.GetDistance(Settlement, Settlement, …)` / `(MobileParty, Settlement, …)` 已在本机反编译源确认 |
| 部队速度基准 | `DefaultPartySpeedCalculatingModel`：`BaseSpeed => 4f`（地图单位/**游戏小时**；依据 `MobileParty.NextMoveDistance = Speed * dt`）、`MinimumSpeed => 1f` |

## 2. 数据权威划分

- **transcript 仍是正文与历史的唯一权威**，本批**不改其 schema**；信件正文只存 transcript 一行。
- 新增**信件账本**命名空间 `awake.letters`（键 `campaign.letters.v1`），**只存生命周期元数据，不复制正文**。
- 账本条目 id ＝ 该信 transcript 行的 id（`letter|<idempotencyKey>`）⇒ 1:1 可追溯、无正文双写。

## 3. 账本条目字段

| 字段 | 说明 |
| --- | --- |
| `id` | `letter|<idempotencyKey>`，＝ transcript 行 id |
| `contactKey` | canonical contact key（归属联系人） |
| `direction` | `outbound`（玩家→NPC）/ `inbound`（NPC→玩家） |
| `sender` / `recipient` | 显示名，供 UI 直接渲染 |
| **`createdHour`** | 创建时刻（绝对游戏小时，自战役起算）——**判定基准** |
| **`deliverHour`** | 送达时刻（绝对小时，见 §4） |
| `createdDay` / `deliverDay` | 由小时派生的日粒度，**仅供展示**；旧存档读入时按 `日 × 24` 自动迁移 |
| `status` | `sent` / `delivered` / `read` / `replied` / `expired` / `failed` |
| **`readHour`** | 已读时刻（绝对小时）；未读 ＝ `-1` |
| `replyToId` | 本信回复的是哪封（无 ＝ 空串） |
| `replyLetterId` | 本信被哪封回复（无 ＝ 空串） |
| `failureReason` | `failed` 的原因码 |

仓储惯例字段 `schema` / `letters[]` / `appliedKeys[]` 一并保留。

## 4. 状态机

| 事件 | 转移 |
| --- | --- |
| 发送（outbound） | 建条目 ⇒ `sent`；`deliverHour` ＝ `createdHour + travelHours`（见 §4.1） |
| NPC 主动来信（inbound） | 建条目 ⇒ `delivered`（即"未读"）；`createdHour = deliverHour = now` |
| 逐小时推进 `hour ≥ deliverHour` | `sent` ⇒ `delivered` |
| 逐小时推进超期（`hour - createdHour > ExpiryDays × 24`，未 `replied`） | `sent`/`delivered` ⇒ `expired` |
| 阅读 | `read`（记 `readHour`） |
| 回复 | 新信 `replyToId` 指向原信；原信 ⇒ `replied` 且 `replyLetterId` 回指 |
| 正文落盘失败且条目已建 | ⇒ `failed` + `failureReason` |

**未读** ＝ `inbound` 且 `status ∈ {sent, delivered}`；按 `contactKey` 聚合，另有总数。

### 4.1 送达耗时：固定公式，精度到游戏小时

```
distance    = MapDistanceModel.GetDistance(玩家队伍, 联系人所在队伍 / 聚落)   // 地图单位
travelHours = max(1, ceil(distance ÷ 8))                                   // 8 地图单位/游戏小时，向上取整到整点
deliverHour = createdHour + travelHours
```

- **连续**：距离每变化一点，耗时即变化；**不分档**，不存在"某区间内时间一模一样"。
- **速度依据（1.3.15 反编译源实测）**：`DefaultPartySpeedCalculatingModel.BaseSpeed = 4` 是**每游戏小时**推进的地图单位数——移动结算为 `MobileParty.NextMoveDistance = Speed * dt`，`dt` 即小时增量。旧稿写作"地图单位/天"是误读，已纠正。
  - 地图实测跨度 **706 × 548** 地图单位（本机解析 `settlements.xml` 的 494 个聚落中心坐标 `posX/posY`——另有 120 个 `gate_posX/gate_posY` 城门坐标，**不计入**；与运行时 `CampaignVec2` 同系）；部队 4 单位/小时 ⇒ 横穿大陆 **约 7 天**，与原版体感吻合。
  - 纯骑兵小队理论上限 ＝ `4 × (200/(200+1))^0.4 × 1.3 ≈ 5.2` 地图单位/小时（骑兵加成 `0.3 × cavalry/total`）。
  - 信使＝单人、无辎重、驿站换马、昼夜兼程 ⇒ 取骑兵上限的约 1.5 倍：**8 地图单位/小时（＝192 单位/天）**，且恒速、不计地形与夜间惩罚 ⇒ 实际快于任何部队。
  - 本条**取代** 08-16 稿 `PLAN-Awake-Dialogue-BatchC` 的 `distance / 40` 天粒度口径（同一次误读的产物）。
- **校准样本（全部取真实聚落坐标，非虚构数字）**。坐标来源＝本机 `settlements.xml` 的 `posX/posY`（与运行时 `CampaignVec2` 同系）；地名取 BannerlordSage 索引的官方简体中文（`CNs`）。

  | 真实路线 | 距离（地图单位） | 原版骑兵（≈5.2/h） | 信使（8/h） |
  | --- | --- | --- | --- |
  | 邓格拉尼斯 ↔ 卡·班塞斯（巴旦尼亚） | 34 | 7 小时 | **5 小时** |
  | 厄庇克洛忒亚（北帝国）↔ 萨涅俄帕（北帝国） | 119 | 23 小时 | **15 小时** |
  | 阿弥塔堤斯 ↔ 马凯布（库赛特） | 275 | 53 小时 | **35 小时（1.5 天）** |
  | 奥斯蒂港（瓦兰迪亚）↔ 奥多赫（库赛特） | 653 | 126 小时 | **82 小时（3.4 天）** |

  口径：按 53 座城镇两两配对统计，最近 34 单位、中位 275、最远 653；信使耗时恒为同路线骑兵的约 **0.65 倍**（8 vs 5.2）。上表即取"最近／同帝国邻城／中位／最远"四个真实代表点。复算脚本＝`tools/worldbook-runtime-sim/_probe_courier_real_pairs_20260913.py`（只读）。

- **降级（确定性）**：拿不到地图上下文（离线测试／无战役）⇒ 固定 `UnknownDistanceTravelHours = 24`，行为与"次日达"一致，离线 smoke 因此可复现。

### 4.2 未读的判据：只算"已到手却还没读"

`GetUnreadAsync` 原按 `sent | delivered` 统计入站信。引入在途入站信（§5.1）后这条不再成立——**还在信使路上的信玩家根本看不到**，计进未读会让计数指向一个空列表。故收紧为**仅 `delivered`**。

注意与 `NpcLetterInitiator` 的判据**刻意不同**：那边的"不再催"用 `IsUnreadStatus`（`sent | delivered`），因为"已经有一封在路上了"同样构成不重复寄的理由。两处语义各自正确，改任一处前先看清它服务的是"玩家能看什么"还是"该不该再寄"。

## 5. 接口（运行时；UI 后续接）

挂在 `AwakeLetterService`，**既有 `SendAsync` 签名不变**：

- `SendAsync(contactKey, conversationId, text, ct)` —— 写 transcript + 建 outbound 条目
- `CreateInboundAsync(contactKey, senderName, text, idempotencyKey, ct, travelHours = 0)` —— NPC 主动来信。
  `travelHours` 默认 0 ＝"当面递到"（落盘即可读，既有语义）；传正数表示信使在途 ⇒ 信先落 `sent`，到点由 `AdvanceAsync` 转 `delivered`，与玩家写的信走同一条时钟。
- `MarkReadAsync(letterId, ct)` —— 标记已读
- `ReplyAsync(inboundLetterId, contactKey, text, ct)` —— 回信（建 outbound + 双向关联）
- `GetLedgerAsync(contactKey, ct)` —— 某联系人的信件（按创建时刻倒序）
- `GetUnreadAsync(ct)` —— 未读聚合（按联系人 + 总数）
- `AdvanceAsync(currentHour, ct)` —— 逐小时推进（送达/过期）；由 `AwakeEventBehavior.OnHourlyTick` 调
- `TravelHoursForDistance(distanceMapUnits)` —— 送达公式（纯函数，可直接单测）
- `ResolveTravelHours(contactKey)` / `TryResolveDistance(contactKey, out distance)` —— 距离探针；取不到即降级

### 5.1 主动来信的生产者：`NpcLetterInitiator`

§5 的 `CreateInboundAsync` 是"写入端"，本批补齐它的**触发端**——此前全 `src` 内除 smoke 外**零调用者**，即"NPC 主动来信"这条链路只有半个身子（v0.3.0 §6.2 明列此功能）。

分工与 `NpcProactiveService` 互补，**不重叠**：

| | `NpcProactiveService`（既有） | `NpcLetterInitiator`（本批） |
| --- | --- | --- |
| 对象 | **附近**的人 | **远方**的熟人 |
| 动作 | 弹 inquiry，可能**当面**谈 | 寄一封信，走信使在途 |
| 打断玩家 | 是（inquiry 弹窗） | 否（只落未读计数） |
| 受面板开关约束 | 是（开着面板就不打扰） | **否**（开着通讯录也照样来信，否则"一开面板就收不到信"） |

- 入口：`NpcLetterInitiator.TryProduceAsync(currentHour, ct)`，挂在 `AwakeEventBehavior.OnHourlyTick` 的 `AdvanceAsync` **之后**（先结算送达，再决定是否寄新信）。
- **状态一律从账本推导，不新建存储**：
  - 该联系人已有 `sent` / `delivered` 的入站信 ⇒ **不催**（上一封玩家还没读到）；
  - 距最近一封入站信不足 `CooldownHours`（＝ `NpcProactiveConstants.CooldownDays × 24` ＝ 48 小时）⇒ 跳过；
  - 概率沿用 `NpcProactiveService.ComputeTriggerChance` 与关系取向（trust + love − hostility ），**复用**而非另立一套；
  - 动机加权选取复用 `NpcProactiveService.SelectMotive`（本批由 private 提为 `internal static`，两边共用同一口径）。
- **幂等键**＝`npc-letter|<contactKey>|<游戏日>`：同一联系人每个游戏日最多一封，同一天重复 tick 天然不重复产信。
- **一次 tick 最多产出 1 封**；候选打乱后只评估前 `EvaluationLimit`(8) 个，避免每小时遍历全部联系人与反复读关系。
- 正文为**确定性兜底文本**（`BuildLetterBody`，动机触发理由 ＋ openingHint ＋ 署名），不调用任何 Provider ⇒ 离线可复现；异步真机接入 AI 生成后，此处仍作为兜底。**正文绝不进日志。**
- 开关复用既有 `AwakeSettings.EnableNpcProactive`（与"主动搭话"同一个总闸，未新增配置项）。

## 6. 存储落点（4 处，全在既有通道内）

1. `AiTaskConstants`：新增 `LettersNamespace` / `LettersKey` / `LettersUpsertCommandId`，并把 `LettersNamespace` 加入 `StorageNamespaceIds`（命令权限经 `NewCommandIds` 自动派生，无需手登记）。
2. `WorldStateStore`：新增 `WorldStateKind.Letters` ＋ `GetLettersAsync` / `UpdateLettersAsync` ＋ `ApplyLetters` / `EnsureLettersShape` / `NewLettersState`（照 `Onboarding` 范式）。
3. `AwakeEventBehavior.OnHourlyTick`：新增 `AdvanceAsync` ＋ `NpcLetterInitiator.TryProduceAsync` 各一次（均放在既有守卫**之前**，使送达与来信都不受面板开关影响；顺序是先结算送达、再决定是否寄新信）。主动来信**不新增存储**（§5.1）。
4. `AwakeTranscriptService.AppendLetterAsync`：**扩参数**支持 inbound（`speaker` / `kind`），单调用点同步更新；**schema 不变**。

## 7. 验收（离线 E2）

smoke 新增用例，全部断言结构化日志次数与账本 JSON 字段：

| 场景 | 断言 |
| --- | --- |
| 送达公式 | 40→5h、400→50h、270→34h、10→2h；相邻单位差（41 vs 40）即改变小时桶；下限 1 小时（0 与 0.4 都 ⇒ 1）；**信使必须快于骑兵**（270 ⇒ 34h ＜ 骑兵 ≈52h） |
| 发送 | transcript 落 1 行 ＋ 账本 1 条 `sent`；`deliverHour` ＝ `createdHour + travelHours` |
| 逐小时推进 | 未到 `deliverHour`（差 1 小时）仍 `sent`；到达该小时 ⇒ `delivered`（**不等次日**） |
| 阅读 | ⇒ `read` 且 `readHour` ＝ 推进小时 |
| 回信 | 新条目 `replyToId` 指向原信；原信 `replied` ＋ `replyLetterId` 回指 |
| 未读计数 | inbound 未读聚合正确；读后减一；**仍在信使途中的信不计未读**——玩家看不到它，计进去只会指向一个空列表 |
| 主动来信 · 决策 | 上一封未读 ⇒ 不催（roll 0 也不寄）；冷却期内不寄；冷却已过 ＋ 亲密关系 ＋ roll 0 ⇒ 寄；roll 0.999 ⇒ 永不强行触发（概率有上限）；无关系 ⇒ 最低档概率；冷却口径与"主动搭话"同源 |
| 主动来信 · 契约 | 幂等键按"联系人 × 游戏日"稳定、跨日才翻页；正文含触发理由与署名、占位符全部替换、且 ≤ transcript 单行上限 |
| 主动来信 · 在途 | `travelHours = 34` ⇒ 先 `sent`、`deliverHour = createdHour + 34`；差 1 小时仍 `sent` 且未读保持 0；到点 ⇒ `delivered` 且未读 +1；正文不进日志；离线无战役联系人 ⇒ 安静返回空串不抛异常 |
| 过期 | 超期未回复 ⇒ `expired` |
| 失败 | 正文落盘失败 ⇒ `failed` ＋ `failureReason`，且不产生半写历史 |
| 幂等 | 同 `idempotencyKey` 重复 ⇒ 不产生第二条 |
| 敏感文本 | 信件正文不出现在 `CapturedLogs` |

E4/E5 不在本批；不因本地 smoke 宣称游戏内或存档通过。

## 8. 非目标

- 不做 UI（界面 / VM / 绑定 / 语言文件）——UI 侧另批，避免与并行 UI 线冲突。
- 不做大地图通知弹窗。
- 不做"复杂信使模拟"：逐日路径推进、延误、拦截、被俘阻断（§6.4 延期项）。**距离→时间的换算不在延期范围**。
- 不改 transcript schema、contact、persona、授权、命令允许列表。
- 不改游戏效果数值。

## 9. 执行结果（2026-09-13）

- 构建：`dotnet build -c Release -p:BannerlordApi=1.3.15` → **0 警告 0 错误**。
- smoke：**28/28 全绿**（新增 `letter-delivery-lifecycle`；原 27 项不变）。
- 新增/改动：
  - `src/AwakeLetterLedger.cs`（新）：账本常量、记录模型（**小时为判定基准**）、文档状态机。
  - `src/AwakeLetterService.cs`：扩为送达闭环（Send / CreateInbound / MarkRead / Reply / Advance / GetLedger / GetUnread）＋ **送达公式与距离探针**。
  - `src/AwakeRuntime.cs`：新增 `CurrentGameHours()`（`CampaignTime.Now.ToHours`）与 `CurrentGameAbsoluteHour()`（整点绝对值，原 `CurrentGameHourOfDay`，见 §9.1），带"仅有日粒度时按 `日×24`"的确定性降级。
  - `src/AiTaskConstants.cs`、`src/AwakeStorageContract.cs`、`src/WorldStateStore.cs`、`src/AwakeTranscriptService.cs`、`src/AwakeEventBehavior.cs`。
  - `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsLetters.cs`（新）＋工程清单与注册表。
- 实施中补掉两处存储通道缺口（不在原设计里，但不补则新命名空间无法落盘）：
  1. `AwakeStorageContract.ExpectedSchema` 缺 `WorldStateKind.Letters` 分支 ⇒ schema 无法归一、`appliedKeys` 为 null ⇒ 运行期 NRE（首跑即命中）。
  2. `WorldStateStore.NewState` 工厂缺 Letters 分支 ⇒ 首次写入拿到空 `JObject`。
- **可复用结论**：新增一个文档型命名空间要落 **6 处**——常量与命名空间清单、`WorldStateKind`、`ExpectedSchema`、`NewState` 工厂、命令分发 `switch`、读写方法。漏任一处都是运行期 NRE 或静默 schema 失配。

### 9.1 修订记录（同日）

- 送达时间由"当日/次日常量"改为**按距离的固定公式**（§4.1），精度从"游戏日"细化到"游戏小时"。
- 账本判定基准字段由 `createdDay` / `deliverDay` / `readDay` 升为 `createdHour` / `deliverHour` / `readHour`（绝对游戏小时）；日字段改为派生值，仅保留展示与旧存档兼容。
- 推进入口由 `AdvanceAsync(currentDay)` 改为 `AdvanceAsync(currentHour)`；新增公式纯函数与距离探针，smoke 补公式断言组（含连续性、整点取整、下限）。
- **信使速度校准**：`CourierMapUnitsPerDay = 40` ⇒ `CourierMapUnitsPerHour = 8`（§4.1），公式由 `distance × 24 ÷ 40` 改为 `distance ÷ 8`。原值等效 1.67 单位/小时，慢于原版步兵（4），违背"信使"语义；新值 ≈ 骑兵上限（5.2）的 1.5 倍。smoke 公式断言组同步重算，并新增"必须快于骑兵"回归断言。
- **真实地名复核**（同日）：§4.1 校准表改由 53 座城镇的真实坐标配对生成——最近 34（邓格拉尼斯↔卡·班塞斯）、同帝国邻城 119（厄庇克洛忒亚↔萨涅俄帕）、中位 275、全图最远 653（奥斯蒂港↔奥多赫 ⇒ 信使 82 小时 vs 骑兵 126 小时）。此前表中"270／690"为抽象距离，现换成可核对的地名。
- **主动来信链路补齐**（同日）：`CreateInboundAsync` 此前除 smoke 外**无调用者**——v0.3.0 §6.2 明列的"NPC 主动来信"只有写入端。本批新增生产者 `NpcLetterInitiator`（§5.1）：远方熟人 ＋ 动机命中 ⇒ 一封真信，带信使在途时间。状态全部从账本推导（上一封未读不催 ／ 48 小时冷却），幂等键＝`npc-letter|<contactKey>|<游戏日>`，正文为确定性兜底文本。`NpcProactiveService.SelectMotive` 由 private 提为 `internal static`，两边共用同一动机口径。
- **入站信新增在途时间**：`CreateInboundAsync` 加可选参数 `travelHours`（默认 0 ＝ 既有"当面递到"语义，既有调用点与 smoke 断言不受影响）。
- **未读判据收紧**（§4.2）：由 `sent | delivered` 改为**仅 `delivered`**——在途信玩家看不到，不应计入未读。
- smoke 28 → **29**（新增 `npc-proactive-letter-initiative`）。
- **`AwakeRuntime.CurrentGameHourOfDay()` 改名**（同日）：旧名读作"当日内小时（0–23）"，实现是 `(int)Math.Floor(CurrentGameHours())` ＝ **自战役起算的绝对小时**。账本链路按绝对小时比较所以行为一直正确，但名字会诱导后人做 `% 24`／跨日运算而静默出错，故改为 **`CurrentGameAbsoluteHour()`**。5 个调用点（`AwakeLetterService` ×4、`AwakeEventBehavior` ×1）同步更新，无行为变化。

## 10. 发现（本批如实记录，未修）

- **信件正文上限与 transcript 单行上限不一致**：`AwakeLetterConstants.MaximumLetterBytes = 2000`，`AwakeTranscriptConstants.MaximumTextUtf8Bytes = 1200`。1201–2000 字节的信能过信件校验、却被 transcript 拒绝 ⇒ 现在会落一条 `failed`（`failureReason=transcript_write_failed`）且不留半写历史。改任一侧都是可见行为变更，应单独决策；smoke 已把该行为固化为断言。
- （2026-09-13 修订）送达已由"当日/次日常量"改为**按距离的固定公式**、精度到游戏小时（见 §4.1）。§6.4 延期的"复杂信使模拟"指逐日路径推进／延误／拦截，**不含**距离→时间换算，二者不冲突。
- **信使速度常量（8 地图单位/小时）为设计值**，非原版直接数据；原版只给出部队 `BaseSpeed = 4f`（每游戏小时）与骑兵加成 `0.3 × cavalry/total`，由此推得骑兵上限 ≈5.2。信使取 8 ＝ 骑兵上限约 1.5 倍。若日后要更贴原版，可改为运行时引用一支理想"骑兵快队"的 `MobileParty.Speed` 再乘系数，但须保持"恒速、不随地形/时段抖动"，否则送达时刻不可预期。
- （2026-09-13 修订）旧值 40 地图单位/**天** ＝ 等效 1.67 单位/小时，**比原版步兵还慢 2.4 倍**（用户驳回："这是信使，不是部队"）。根因是把 `BaseSpeed = 4f` 误读为"每天"4 单位。现改为 8 单位/小时，中位城际 270 ⇒ 34 小时（原 162 小时）。
- 未读只做聚合，未接大地图通知（属 UI 批）。
- **主动来信的正文目前是确定性兜底文本**（动机理由 ＋ openingHint ＋ 署名），不含 AI 生成；真机接入 Provider 后应以生成文本替换、兜底保留。本批只保证链路与契约，不保证文采。
