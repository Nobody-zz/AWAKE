# 周报坏根修复记录（`awake.world_fact.root_corrupt` → 已解）

- **产出线**：模组主体（代码线）
- **时间**：2026-09-15 20:00–20:40
- **涉及提交**：`bae3841`（第一刀）／`25de029`（往返判据）／`437bedd`（第二刀·自愈）
- **证据**：真机日志 `<game>\Modules\AWAKE\Logs\Awake.log`（09-14 23:17）×4 提交的离线实跑

---

## 一、真机症状（一手）

```
2026-09-14 15:15:04Z awake_world_fact_collector_behavior_added
2026-09-14 15:17:39Z awake_world_fact_query policy=WeeklyDynamics status=Corrupt count=0 generation=1 error=awake.world_fact.root_corrupt
2026-09-14 15:17:39Z awake_weekly_report_fact_query_failed window=91077 status=Corrupt error=awake.world_fact.root_corrupt
2026-09-14 15:17:39Z awake_weekly_report_refresh status=unavailable code=awake.world_fact.root_corrupt
（15:17:41 / 15:17:43 同样三轮）
```

三轮同一结果 ⇒ **不是瞬时故障，是稳定坏、永不恢复**。周报整链不可用。

盘上旁证：`<game>\Modules\AWAKE\PlayerExports\AwakeState\unbound\` 里
`awake.world.events` / `awake.letters` / `awake.dialogue.queue` / `awake.npc.proactive` / `awake.onboarding`
**都有文件**，**唯独 world fact journal 一个文件都没有** ⇒ 那本账从来没落过盘。

---

## 二、真因（三步，全部一手）

1. **存储层**：`AwakeFileStorageService.cs:142-143` 对"**这个 key 不存在**"返回的是
   **`Succeeded("")`**（成功＋空串）—— **不是** `storage.key_not_found`、**也不是** null。
2. **codec 层**：`WorldFactJournalCodec.ReadRoot` 把空串判成 **Corrupt**（`WorldFactJournal.cs:132`）。
3. **写侧**：`AppendWorldFactJournalAsync` 一开场先读 journal，读到 Corrupt 就**直接放弃写入**
   （`WorldStateStore.cs:3657-3663`）。

⇒ **读坏 ⇒ 不写 ⇒ 永远空 ⇒ 永远读坏。永久死锁。**

**为什么隔壁"世界事件"那本账没事**：它的读取路径多了半行
`if (string.IsNullOrWhiteSpace(loaded.Value)) return NewWorldEventsState();`（`WorldStateStore.cs:856`）。
**同一份数据、同一个后端，一条路防了、一条没防。**

---

## 三、改动

| 提交 | 改动 |
|---|---|
| `bae3841` | `WorldFactJournal.cs:132` 空/空白 ⇒ **Missing**（原 Corrupt）。只改 root；`ReadChunk` 的"空 ⇒ 坏"**保留**（root 说有分片却读不到分片，那是真不一致）。<br>`AwakeTestFakes.cs` 替身 `GetAsync` 改回 `Succeeded("")`，与真件同源。<br>`Program.cs` 新增 case `world-fact-journal`（该链此前**零覆盖**）。 |
| `25de029` | `Program.cs` 新增 case `world-fact-journal-roundtrip`：空账本 ⇒ 读 Missing ⇒ 第一条落盘 ⇒ 读得回来。 |
| `437bedd` | `WorldStateStore.cs`：**第二半死锁**。Corrupt 不再直接放弃 —— 先把坏值**隔离**到 `<rootKey>.quarantine`（只复制、不删原件，数据不丢），再按空账本继续、由新 root 覆盖。隔离失败则 `Retryable=true`。`Unavailable` 仍单独拦（环境问题，隔离解决不了）。<br>`IsStorageKeyNotFound` 加注释、**刻意不改行为**（见下）。<br>`Program.cs` 新增 case `world-fact-journal-recovery`。 |

### 为什么不动 `IsStorageKeyNotFound`（虽然它恒为 False）

真后端**从不产出** `storage.key_not_found`（唯一产出该码的地方在 `tools/` 的**离线替身**里），
所以这个判断在真后端上**恒为 False**。但逐处核过 **15 个调用点全部嵌在 `if (!loaded.IsSuccess)` 之内**，
它实际只决定"要不要打一条失败日志"，**没有一处因此发生功能故障**。
改它要动 15 处语义、收益近零 ⇒ **刻意不改，改为把规矩写在方法注释里**：

> 凡"缺 key 就用默认值"的逻辑，**必须另有 `IsNullOrWhiteSpace(值)` 兜底**。

---

## 四、验证（均为实跑）

| 项 | 结果 |
|---|---|
| 判据有牙（改前跑） | `[57/58] FAIL world-fact-journal-recovery`，msg「the corrupt root must be quarantined before it is replaced.」⇒ 写侧确实什么都没写 |
| 修复后 | `PASS world fact journal smoke` / `…roundtrip smoke` / `…recovery smoke`（三条全过） |
| **变异检验**（把空判定改回 Corrupt 再跑） | 两条判据变红，其中一条报 **`got=Corrupt code=awake.world_fact.root_corrupt`** —— **与真机日志错误码一字不差**，即在离线复现了真机症状 |
| 全链无回归 | `RESULT total=58 passed=52 failed=6`，`FAILED_CASES` 与改动前**完全一致** |
| 恢复无残留 | 还原后重跑一致，`git diff` 对 `WorldFactJournal.cs` 为空 |

**一条已知边界**：真后端 `AwakeFileStorageService` 依赖 `AwakeUiDispatcher` 的游戏线程与
`AwakeModulePaths` 的模块目录，**离线验台里不能直接用**。故 store 层判据只能用替身；
替身与真件的同源性靠"读真件代码 ＋ 在判据里注明出处"保证，**不是跑出来的**。

---

## 五、未做 / 请办

1. **真机复验（唯一剩下的验收）**：跑一次游戏，确认
   ① `Awake.log` 不再出现 `root_corrupt`；② `awake_weekly_report_refresh` 不再是 `unavailable`；
   ③ 若这次采集到了事实，`PlayerExports\AwakeState\unbound\` 下应出现 world fact journal 的文件。
   —— **过版判据以游戏内为准，离线全绿不算过版。**
2. **请总控更新** `docs/AWAKE-ROADMAP.md` 的「现状」节（第 80 / 143 / 149 行仍记着此缺陷为未修）。
3. 本次未触碰 `AWAKE/AGENTS.md`、`ROADMAP` 等他人归属文件。
