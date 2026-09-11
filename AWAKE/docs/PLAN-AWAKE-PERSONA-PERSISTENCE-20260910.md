# AWAKE P0 Persona 持久化立项 — 2026-09-10（修订 C）

## 立项结论

- 目标：把已存在但零接线的 Persona 持久化模型接入 Bannerlord `SyncData`，
  让 **persona 锚点**（本档身份 + 分支 + schema 版本）随存档往返，解除 E5 的第一道阻塞。
- 本切片范围 = **锚点切片**：只做"写 → 存 → 读 → 校验 → 装载"。persona **内容**
  （sequence / watermarks 的真实生产者 = persona 投影）属于**下一批次**，本批次不接线、
  也不得声称"persona 内容恢复"。
- 风险等级：`high-risk`（新存档格式 + 稳定 ID 契约 + 跨模块状态）。
  强制通道：`grill-me-codex` + 审查状态机（最多 3 轮审查）。
- 本文件是**立项**，不是实现授权。未获用户对本文档的签收前，不修改任何 Runtime 源码。
- 与当前候选关系：候选 `awake-20260903-awake-runtime-repair-004` 冻结为红测基线，
  本批次实现必须另立 BuildId，不得就地改 `004`。
- 修订记录：A → round 1 独立评审 `REVISE`（3×P0 / 10×P1）→ B → round 2 独立评审
  `REVISE`（3×P0 全部 CLOSED、**无新增 P0**、7×阻塞 P1）→ C（本版）。
  审查状态文件：`docs/review-state/AWAKE-PERSONA-PERSISTENCE-20260910.review.json`
  （`round=2`，`status=revising`，`max_rounds=3`）。

### 证据路径缩写

- `CS:` = `C:\Users\26811\OneDrive\文档\New project\.csys_full_decomp\TaleWorlds.CampaignSystem.decompiled.cs`
- `CO:` = `C:\Users\26811\OneDrive\文档\New project\.core_full_decomp\TaleWorlds.Core.decompiled.cs`
- `SB:` = `C:\Users\26811\OneDrive\文档\New project\.sandbox_full_decomp\SandBox.decompiled.cs`
- `S:` = `_houkai_merge\AWAKE\src\`
- `T:` = `_houkai_merge\AWAKE.Tests\`

## 当前事实（已核实）

### 已存在

- 模型与校验：`S/PersonaPersistenceModels.cs`
  - `PersonaPersistenceEnvelope`（`Schema` / `CharacterId` / `Timeline` / `Sequence` / `Watermarks` / `Source` / `PayloadHash`）
  - `PersonaTimelineIdentity`（CampaignId / SaveId / TimelineId / BranchId / ParentBranchId / ForkSequence）
  - `PersonaProjectionWatermarks`（`Transcript/Effects/Memory/PersonaAcceptedSequence`）
  - `PersonaRecoveryRecord` + `PersonaStorageKey.TryBuild` + `PersonaPersistenceValidator`
- 存储契约：`S/AwakeStorageContract.cs` 已登记 `awake.persona.continuity.v1` /
  `awake.persona.override.v1` / `awake.persona.recovery.v1`。
- 状态种类：`WorldStateKind.PersonaContinuity / PersonaOverride / PersonaRecovery`（`S/WorldStateStore.cs:29-31`）。
- 测试工程已编译 `S/PersonaPersistenceModels.cs` 与 `S/AwakeStorageContract.cs`
  （`T/AWAKE.Tests.csproj:105`、`:87`）→ 模型级用例可离线执行。

### 缺失（本批次要补的 P0）

1. **无写入方**：全源码没有任何代码构造 `PersonaPersistenceEnvelope` 并写入任何存储。
2. **无读取方**：读档后没有任何读取与装载路径。
3. **`SyncData` 无锚点**：`S/AwakeEventBehavior.cs:23` 只同步 `awake_last_weekly_report_day`。
4. **零接线**：`PersonaPersistenceEnvelope` 全源码仅在该文件内出现（无生产者、无消费者）。

### 生命周期与存档时序事实（round 1 已核实，round 2 复核确认）

| 事实 | 证据 |
|---|---|
| `Campaign.Current` 在 `OnGameStart` 时**必非空**（`SetLoadingParameters` 无条件 `Current = this`，且先于 `DoLoading`） | `SB:5178-5191`、`CS:10329-10337` |
| `SubModule.OnGameStart` → `BeginCampaignSession()` 发生在 `Campaign.OnInitialize` 内部 | `CS:10344`、`CS:10369`、`S/SubModule.cs:47-53` |
| **行为 `SyncData` 的载入早于 `OnGameLoadedEvent`**（`LoadBehaviorData` 在 `InitializeFirstStep`） | `CS:10406-10408` → `CS:167161-167168`、`CS:11151-11164`；事件派发在 `CS:10638`/`CS:10648` → `CS:9658-9670` |
| **新档 `UniqueGameId` 在 `OnGameStart` 时为 `null`**：赋值在 `OnNewGameCreatedInternal`，调用点在 `PostInitializeFourthState` | `CS:10569`、`CS:10672`（严格晚于 `CS:10369`） |
| 读档时 `UniqueGameId` 已随存档反序列化可用（`[SaveableProperty(80)]`） | `CS:9198-9199` |
| 老档升级（<v1.2.2 且 `UniqueGameId == null`）会被置为 `"oldSave"`，**所有此类档共享同一值**且此后不再重赋值 | `CS:9591-9593` |
| 新档 `OnNewGameCreatedEvent` 派发前，行为 `RegisterEvents` 已执行；且不会重复订阅 | `CS:10561`→`CS:10602`→`CS:10563`；`CS:167144-167150`；`CS:23003-23009` |
| 新档不触发 `OnGameLoadedEvent`，读档不触发 `OnNewGameCreatedEvent`（两条分支互斥） | `CS:10641-10658`、`CS:10659-10674` |
| 保存时序：`OnSaveStarted` → `OnBeforeSave`（此处冻结行为数据）→ 落盘 → `OnSaveOver` | `CS:46308`、`CS:46313`、`CS:46323-46329`、`CS:46367`、`CS:167152-167158` |
| 保存分支 `_records.Add(key, data)`（同键第二次即抛异常）；载入分支缺键静默返回 `false` 且不改 `ref`；存档数据存在该档文件的记录字典内 | `CS:11151-11163`、`CS:11183-11203` |
| `IDataStore` 暴露 `IsSaving` / `IsLoading` / `SyncData<T>`，可用于判方向 | `CS:11127-11129`、`CS:11142-11144` |
| 换主角事件可挂：`CampaignEvents.OnPlayerCharacterChangedEvent`（`Hero, Hero, MobileParty, bool`） | `CS:19727`、`CS:21061-21063` |

## 决策锁定（修订 C）

| 决策点 | 结论 | 依据 |
|---|---|---|
| D-1 权威存储位置 | **A2 存档权威**：persona 锚点经 `SyncData` 写入游戏存档；SQLite 不做 persona 权威 | 沿用 `awake_worldbook_overlay_v1` 先例（`S/AwakeTerminalBehavior.cs:69,77`） |
| D-2 身份绑定时机 | **惰性绑定**：`BeginCampaignSession` 不读 persona；读档用 `OnGameLoadedEvent`，新档用 `OnNewGameCreatedEvent` | 新档 `OnGameStart` 时 `UniqueGameId` 为 `null`（`CS:10569` vs `CS:10369`）；载入数据此刻尚未到位（`CS:10406-10408`） |
| D-3 载荷与提交模型 | **单一固定键 + 一份自洽快照**；无 `session_pending`/`unsaved_recovery`/`save_committed` 状态机，不依赖 `OnSaveOverEvent` | 存档内容在 `OnBeforeSave` 冻结，`OnSaveOver` 之后才触发（`CS:46313`/`CS:46367`） |
| D-4 存储键 | 键固定、不随 campaign 变化（存档文件已隔离）；campaign 身份只作 payload 内校验字段 | `BehaviorSaveData` 同键重复即抛异常（`CS:11153-11157`） |
| D-5 隔离粒度 | campaign 级 `Campaign.Current.UniqueGameId`（仅一致性校验） | 槽级 `MBSaveLoad.ActiveSaveSlotName` 是名字不是 ID，AutoSave 仅 3 名轮转 |
| D-6 清档行为 | 随存档自动消失（无需额外策略） | 数据与被删存档同在 |
| D-7 闭环范围 | **仅玩家角色**（`Hero.MainHero`），不含 NPC | E5 卡点即"读档后 persona 丢失"；NPC persona 依赖未完成的人物内容线 |
| D-8 唯一读路径 | `SyncData` **只搬运**（保存前刷新字符串 / 载入取回字符串），**唯一解析点在 `OnGameLoadedEvent`** | 消除 round 2 的 P1-1 自相矛盾 |
| D-9 方向判别 | 用 `dataStore.IsSaving`；值变化启发式**不作**判定依据 | `CS:11127-11129`；值变化启发式会把"缺键/空值/值相同"混为一类 |
| D-10 本批次 out-of-scope | persona 内容（sequence/watermarks 生产）、`PersonaOverride`、`PersonaRecoveryRecord`、`PersonaStorageKey` 定位、投影裁剪、recovery 状态机 | 无生产者/无消费者；保留模型但不进契约，标注未启用 |

## 契约设计（C 锁定版）

### 载体

- **唯一载体**：现有 `S/AwakeEventBehavior.cs` 的 `SyncData`（已注册行为，避免第二个行为导致重复注册与同键冲突）。
- **接缝文件（新增）**：`S/PersonaContinuitySync.cs`，无 Bannerlord 逻辑依赖，只做三件事：
  - `internal const string SaveKey = "awake_persona_continuity_v1";`
  - `internal static void Sync(IDataStore dataStore, ref string json, Func<string> snapshotProvider)`
    （保存方向：`json = snapshotProvider()` 后 `SyncData(SaveKey, ref json)`；载入方向：只 `SyncData` 取回，不解析）
  - `internal static PersonaLoadOutcome Adopt(string json, string currentCampaignId, string currentCharacterId, out PersonaPersistenceEnvelope envelope, out string reason)`
- **该文件必须加入 `T/AWAKE.Tests.csproj` 编译列表**（与 `PersonaPersistenceModels.cs` 同批），
  否则 E1 的接线断言无法离线执行（round 2 的 P1-2）。
- `AwakeEventBehavior.SyncData` 收缩为一行调用 `PersonaContinuitySync.Sync(...)`，
  并在保存方向追加日志 `persona_continuity_saved key=... bytes=... campaign=...`（E4 佐证）。

### Payload = `PersonaPersistenceEnvelope` 的序列化结果（字段映射表）

```json
{
  "schema": "awake.persona.continuity.v1",
  "characterId": "<Hero.MainHero.StringId>",
  "timeline": {
    "campaignId": "<Campaign.Current.UniqueGameId>",
    "saveId": "",
    "timelineId": "awake.timeline",
    "branchId": "root",
    "parentBranchId": "",
    "forkSequence": 0
  },
  "sequence": 0,
  "watermarks": {
    "transcriptAcceptedSequence": 0,
    "effectsAcceptedSequence": 0,
    "memoryAcceptedSequence": 0,
    "personaAcceptedSequence": 0
  },
  "source": "",
  "payloadHash": ""
}
```

| payload 字段 | 来源 | 说明 |
|---|---|---|
| `schema` | 常量 | 只允许 `awake.persona.continuity.v1` |
| `characterId` | `Hero.MainHero.StringId` | 稳定 id，不用显示名 |
| `timeline.campaignId` | `Campaign.Current.UniqueGameId` | 空/`oldSave` 走失败分支 |
| `timeline.timelineId` | 常量 `awake.timeline` | — |
| `timeline.branchId` | 常量 `root` | 校验器要求非空 |
| `timeline.saveId` / `parentBranchId` / `forkSequence` | 恒定 `""` / `""` / `0` | 保留字段，**不参与契约**（根分支约束：父空 + fork 0 合法） |
| `sequence` / `watermarks.*` | 本批次恒 `0` | 真实生产者 = 下一批次的 persona 投影；本批次不得声称内容恢复 |
| `source` / `payloadHash` | 恒定 `""` | 校验器不检查，**明确不参与契约** |

说明：payload **直接取模型序列化结果**，因此 `PersonaPersistenceValidator` 可无映射直接消费；
不再使用扁平字段名，也**不写入任何时间戳**（时间戳只出现在日志里，避免"同档重复保存哈希必须相等"无法成立）。

### 生命周期与触发点

| 时机 | 动作 |
|---|---|
| `OnSubModuleLoad` | 只注册，不碰 `Campaign.Current` |
| `SubModule.OnGameStart`（`CS:10369`） | `BeginCampaignSession()` 只起 runtime session；**不读 persona、不取身份** |
| `AwakeEventBehavior.SyncData`（载入，`CS:10406-10408`） | 只把 JSON 字符串取回；缺键 → 保持原值，视为"无 persona"，不报错 |
| `CampaignEvents.OnGameLoadedEvent`（读档，`CS:10648`） | **唯一解析点**：`PersonaContinuitySync.Adopt(...)` → 校验 → 比对 campaign/character → 装载锚点 |
| `CampaignEvents.OnNewGameCreatedEvent`（新档，`CS:10563`） | 建立本 campaign 身份（此时 `UniqueGameId` 已生成）；首存前允许无 payload |
| `AwakeEventBehavior.SyncData`（保存，`CS:46313`） | `snapshotProvider` 生成当前锚点 JSON → `SyncData` 写入 + 日志 |

### 边缘分支（必须 fail-closed，不得静默）

| 情况 | 行为 / 原因码 |
|---|---|
| 保存时 `Campaign.Current.UniqueGameId` 为 **null 或空白**（用 `string.IsNullOrWhiteSpace`；新档首存前为 `null` 而不是空串） | 不写 persona；`persona.persistence.skipped reason=campaign_id_empty` |
| `campaignId == "oldSave"`（`CS:9591-9593`，跨档共享且永久不回写） | 拒绝写入、拒绝装载；`reason=campaign_id_not_unique`。**已接受后果**：该存档线 persona 持久化永久关闭，只告警 |
| 装载时 `campaignId` 与当前 campaign 不一致 | 丢弃不装载；`reason=campaign_mismatch`（**纯防御**：存档数据本就按文件隔离，见隔离证据节） |
| 装载时 `characterId != Hero.MainHero.StringId` | 丢弃不装载；`reason=character_mismatch` |
| `schema != awake.persona.continuity.v1`（含 override、未知） | 丢弃不装载；`reason=schema_unsupported`（装载器前置判等；校验器本身仍接受两种 schema，属既有行为，不改） |
| 存档缺键（旧档） | 视为无 persona，正常运行，不刷告警 |
| JSON 损坏 / 校验失败（含 watermark > sequence） | 丢弃全部，不做部分装载；`persona.persistence.rejected reason=...` |
| 主英雄变更（`CampaignEvents.OnPlayerCharacterChangedEvent`） | 作废本 session 锚点，`persona.persistence.character_changed`；旧数据不迁移、不删除 |
| 每次进入 campaign（读档/新档/换档） | 重建锚点身份，旧 session 身份一律作废 |

### 提交语义与并发

- **无状态机**：落盘内容就是锚点快照，"能读回即可用"；不记录"是否已提交"。
  现有 `PersonaRecoveryRecord` 与本批次无关，**保留模型但标注未启用**（不删除既有代码）。
- **原子快照**：锚点以不可变快照表示，更新 = 原子替换引用；保存路径只读一次引用。
- **保存期间的写入**：保存进行中产生的新提交不进入本次快照，顺延到下一批。

### 隔离证据的归因（避免虚假验收）

- 跨档隔离的**真实机制是存档文件隔离**：行为数据存放在该档文件的记录字典内（`CS:11183-11203`），
  同一存档文件不可能夹带另一 campaign 的 payload。
- 因此 `campaign_mismatch` 是**纯防御分支**，不能当作"跨档隔离已验证"的证据；
  E5 的隔离证据必须写成"同一 payload 只出现在写入它的存档文件中"。

### 既有代码的清理项（本批次必须一并处理）

1. `PersonaPersistenceConstants` 与 `AwakeStorageContract` 重复定义同一批 schema 字面量 → 改为单点常量。
2. `PersonaStateNamespace` 从 `AiTaskConstants.StorageNamespaceIds`（`S/AiTaskConstants.cs:125`）**默认打开列表**移除：
   - 影响面：仅影响走默认列表的调用方（`S/AwakeRuntime.cs:737-755` 的回落路径，被
     `S/AwakeEventEngine.cs:256,507`、`S/NpcDialogueService.cs:413,481,1380`、`S/ProbeExtension.cs:345` 使用）；
     显式传列表的调用方（如 `S/AwakeLetterService.cs:41`）不受影响。
   - `WorldStateStore.HasNamespaces` 只检查传入列表（`S/WorldStateStore.cs:317-328`），不会被破坏。
   - 同批必须更新既有证据与期望：`docs/persona-awake-joint-g3-s0-*.json`、`T/Program.cs:3268-3269`，
     否则留下陈旧的不变量记录。
3. `PersonaStorageKey` / `PersonaRecoveryRecord` / `PersonaOverride` / `IsAcceptedForProjection`
   → 标注未启用，不给消费者（**投影裁剪属下一批次**：本批次水位恒 0，无裁剪可做；
   transcript/memory/effects 命名空间的作用域也留到下一批次一并定义）。

## 验收定义（入口 → 调用 → 结算 → 可观察结果）

1. **入口**：读档进入 campaign → `CampaignEvents.OnGameLoadedEvent` → persona 锚点装载服务。
   （新档入口 `OnNewGameCreatedEvent`；**不得挂在 `BeginCampaignSession`**。）
2. **调用**：`AwakeEventBehavior.SyncData` 已把 `awake_persona_continuity_v1` 取回到 `ref` 字段
   （时序 `CS:10406-10408` < `CS:10648`），装载服务读取该字段。
3. **结算**：`PersonaContinuitySync.Adopt` 通过 schema / campaign / character 三重比对 +
   `PersonaPersistenceValidator` 校验后装载锚点。
4. **可观察结果**：`Awake.log` 出现
   `persona_continuity_loaded campaign=... character=... schema=...`；
   同档再次保存后 payload 与装载时**逐字段相等**（无时间戳，故可比较）；
   同档二次读档结论稳定；同一 payload 只出现在写入它的存档文件中。

任何"模型存在 / 编译通过 / JSON 可解析"均不构成完成。

## 验收矩阵

| 层 | 用例 | 证据 | 可执行性 |
|---|---|---|---|
| E1 模型 | envelope 正反例：schema 未知、字段缺失、`watermark > sequence`、`sequence < 0` | `Awake.SdkSmoke` 分支 + evidence JSON | 离线可执行（`T/AWAKE.Tests.csproj:105` 已编译模型） |
| E1 接线断言 | 用 fake `IDataStore` 驱动 `PersonaContinuitySync.Sync`：保存方向写入 1 次且键名 `awake_persona_continuity_v1`；载入方向（有键）取回字符串；载入方向（缺键）保持原值不抛 | `Awake.SdkSmoke` 新分支 + evidence JSON | 离线可执行（fake 由本批次新增；`TaleWorlds.CampaignSystem` 已被测试工程引用） |
| E2 装载分支 | `Adopt` 的 accept / `schema_unsupported` / `campaign_mismatch` / `character_mismatch` / 损坏 JSON / `oldSave` 六类结果 | 同上 | 离线可执行（纯函数） |
| E4 实机 | 存档 → 退出 → 读档 → `persona_continuity_saved` 与 `persona_continuity_loaded` 日志成对出现 | 用户提供 `Modules\AWAKE\Logs\Awake.log` | 需用户实机（不得由我启动游戏） |
| E5 锚点恢复 | 同档二次读档结论稳定；同一 payload 只出现在写入它的存档文件中（隔离证据归因存档文件） | 同上 + 两次读档对比 | 需用户实机 |

**未在测试工程编译的文件（本批次需要新增/保留的关系）**：
`S/AwakeEventBehavior.cs` 不在 `T/AWAKE.Tests.csproj` 编译列表内
（现为 93 个 `Compile Include`），因此"behavior 确实调用接缝"这一环由
（a）一行调用的代码审查 +（b）E4 的 `persona_continuity_saved` 日志共同证明；
离线只断言接缝本身。这一分工写进 evidence，不得含糊。

## 本批次最小垂直切片（APPROVED 后第一条交付）

- 只做**玩家 persona 锚点**的 写 → 存 → 读 → 校验 → 装载 一条线。
- 完成标志 = 上面验收定义 1-4 全部有证据，E1/E2 离线用例全绿，E4 日志成对出现。
- 完成后 `docs/AWAKE-CURRENT.md` 只能把状态从"not wired to SyncData"更新为
  "**anchor** wired"，E5 **仍为 `blocked_not_wired`**，直到 persona 内容生产者落地。

## 边界与不做

- 不启动 Bannerlord、不改启动器启用状态、不强制结束游戏进程。
- 不同步 dist / 游戏目录 / 测试包，除非用户单独授权。
- 不做世界书或人物正文内容工作；不做 MCM 配置项新增或改名。
- 不修改候选 `004`；实现另立 BuildId。
- 不引入第二条 persona 权威路径（含 SQLite 写入与 `awake.persona.state` 命名空间）。
- 不做 persona 内容投影、不做 watermark 裁剪、不做 recovery 状态机。

## 已识别风险（C 版状态）

- 已关闭：round 1 的 3×P0；round 2 的 7×阻塞 P1（读路径归属、接线断言可执行性、
  payload 映射、schema 收紧、内容生产者缺失、哈希验收冲突、`character_mismatch`、方向判别）。
- 遗留（下一批次）：persona 内容生产者与 watermark 裁剪；投影命名空间作用域；
  `oldSave` 线永久关闭（已接受）。
- 存档格式一旦落地即为兼容契约 → key/schema 必须先冻结再写代码。

## 附录：评审历史

### round 1（A → B）

VERDICT `REVISE`；3×P0（入口时序 / 新档身份空值 / 提交状态机写晚一次）+ 10×P1。
处置：D-2 惰性绑定、D-3 删状态机、D-4 固定键、边缘分支表、验收重写。

### round 2（B → C）

VERDICT `REVISE`；**3×P0 全部 CLOSED，无新增 P0**；7 个阻塞 P1：

| 编号 | 发现 | 处置 |
|---|---|---|
| P1-1 | 读路径三处自相矛盾 | D-8：`SyncData` 只搬运，唯一解析点 = `OnGameLoadedEvent` |
| P1-2 | E1 接线断言离线不可执行（测试工程不含 `AwakeEventBehavior.cs`、无 `IDataStore` 假件） | 新增 `S/PersonaContinuitySync.cs` 接缝 + 加入测试工程 + 新增 fake `IDataStore`；离线只断言接缝，behavior 调用由代码审查 + E4 日志证明 |
| P1-3 | 扁平 payload 与嵌套模型无映射 | payload 改为模型序列化结果 + 显式字段映射表 |
| P1-4 | 校验器也接受 override schema；且 `sequence`/`watermarks` 无生产者 | 装载前置 schema 判等；本切片改名"锚点切片"，内容生产者归下一批次 |
| P1-5 | `savedAtUtc` 使"再次保存哈希一致"不可能成立 | payload 去掉时间戳，时间戳只进日志 |
| P1-6 | 值变化启发式不可靠 | D-9：改用 `dataStore.IsSaving` |
| P1-7 | 载入未比对 `characterId` | 增加 `character_mismatch` 失败分支 |
| P2-1 | 命名空间清理影响面描述不准、G3-S0 证据会陈旧 | 清理项重写为"仅默认打开列表"+ 同批更新 `docs/persona-awake-joint-g3-s0-*.json` 与 `T/Program.cs:3268-3269` |
| P2-2 | `campaign_mismatch` 近乎不可达 | 标注纯防御，隔离证据归因存档文件隔离 |
| P2-3 | null vs 空串、`oldSave` 永久关闭未写实 | 改用 `IsNullOrWhiteSpace`；写明已接受后果 |
| P2-4 | 裁剪规则无归属方 | 明确归下一批次，本批次水位恒 0 |
| P3 | `CO:14130-14143` 引用间接 | 改为 `CS:9198-9199`（`[SaveableProperty(80)]`） |

## 下一步

1. 按修订 C 做 round 3 独立只读评审（确认 7 个阻塞 P1 全部关闭）。
2. 通过 → `tools\code-debt-audit\Validate-ReviewState.ps1 -Action review -Verdict APPROVED`
   → 用户 `-Action approve -UserSignoff`。
3. 签收后立即交付锚点切片（新 BuildId），并同步 `docs/AWAKE-CURRENT.md`。
