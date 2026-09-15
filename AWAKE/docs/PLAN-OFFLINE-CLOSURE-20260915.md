# 离线补全闭环 · 清单与裁决

- 日期：2026-09-15
- 出具：全局主控线（**盘点与裁决，不写实现**）
- 缘起：甲方 Max 19:07 原话 ——
  > **「我现在不方便开游戏实测，能不能先补全闭环？」**
- 用途：**在无法开机实测期间，把"能离线补的"一次点清**；并说明补到哪一步为止。

---

## 一、答案

**能。但先核了一遍发现：离线能补的比想象中少——而少的原因是，我们大部分已经补过了。**

⚠ 本次特意先复核了 09-11 的那份对话链审查（`docs/AUDIT-AWAKE-DIALOGUE-CHAIN-010-20260911.md`）——**它给的三个 P1，现在两个已经修完了。**
⇒ **如果照搬 4 天前那份清单交出去，会有三分之二是让人重做已经做完的活。**
⇒（这正是项目那条「**取证有保鲜期**」：递锚点之前必须先核"现在还成立吗"。）

---

## 二、闭环的定义（用项目自己的口径，不另发明）

> 项目原话（`docs/AWAKE-GLOBAL-MECHANISM-AUDIT-20260827.md:445`）：
> **「可验证闭环＝每个功能都必须证明"入口 → 调用 → 结算 → 玩家可观察结果 → 存档恢复"，而不是只证明结构存在。」**

⇒ 由此，"闭环"不是一个动作，是**五段**。而**离线能证到第四段之前**，第五段（玩家可观察结果）与终段（存档恢复）**必须真机**。

---

## 三、离线能补的（按性价比排序）

### A. 对话链 —— 09-11 三项 P1 的现状（**已复核，非照抄**）

| # | 09-11 提出的问题 | **现在的实际状态** | 证据 |
|---|---|---|---|
| P1-1 | 初始化阶段没检查玩家绑定返回值 | ✅ **已修** | `NpcDialogueService.cs:474` 已接 `bool bound`，`:475` 不成立即早退；并新增两条 blocked 日志（`:478` `world_state_not_ready`、`:495` `prompt_registration`）；`_ready=true` 与 `npc_dialogue_ready`（`:503-506`）**都排在全部闸门之后** |
| P1-2 | 缺正向"回合完成"日志 | ✅ **已修** | `NpcDialogueService.cs:1666` `npc_dialogue_turn_completed hero= generation= correlation= completion_kind=` —— 正是当年要的那三个字段 |
| P1-3 | UI 发送用 `CancellationToken.None` | ❌ **仍未修** | `NpcDialogueVM.cs:724` 仍是 `CancellationToken.None`（`AwakeMessengerVM.cs:478` 那处是 `TaskContinuationOptions`，**不是同一回事，别误改**） |

⇒ **本节可离线补的只剩 P1-3 一项**，外加它的验证场景：关面板、切联系人、连点三次。

### B. ⭐ 四个真机缺陷里，有一条的修法**完全可离线**：周报链

真机缺陷① `awake.world_fact.root_corrupt`（周报／WeeklyDynamics 整链不可用）。

**复核结果**：`WorldStateStore.cs:895` —— 读到坏 root 时**只报错、不恢复**：

```csharp
if (rootStatus != WorldFactJournalReadStatus.Success)
    return new WorldFactJournalReadResult(WorldFactJournalReadStatus.Corrupt,
        errorCode: "awake.world_fact.root_corrupt");
```

⇒ **没有隔离、没有重建、没有降级**，一路把 Corrupt 抛上去 ⇒ 整链不可用。
⇒ **而这条链是纯内部逻辑**（codec ＋ store），**不需要游戏**：离线验台可以**直接喂一个坏 root**，看它现在怎么爆；补上"隔离坏分片 → 重建 root → 降级成只读"之后再验一遍。
⇒ **这是本次清单里最实的一条**：既能离线复现、又能离线验证修法。

### C. 另三个真机缺陷：能离线收窄，不能离线定案

| 缺陷 | 离线能做什么 | 离线不能做什么 |
|---|---|---|
| `runtime_not_ready`（会话头两分钟） | 验台**模拟时序**（晚绑定、慢存储、乱序回调），把触发条件穷举出来 | 证不了真机上是不是这个时序 |
| `native_readiness` 空引用（09-10 起反复、同场里也多次 `Ready` ⇒ **是竞态**） | 离线**压测/并发抖动**去复现 | 复现不出 ≠ 不存在（真机竞态窗口更窄） |
| `npc_dialogue_open_failed`（v0.3 卡这） | **穷举"张不开嘴"的前置条件**，把失败点收窄到具体判断（`NpcDialogueLauncher.TryOpenDialogue` 之后那一段） | **张不张嘴是 Gauntlet UI 的事，离线复现不了**——这一条只能真机 |

### D. 本来就与真机无关的既有待办（roadmap 原列，原样保留）

1. **入库**：美术线的源图与工具（`AssetSources/`、`GUI/SpriteParts/`、`tools/awake-art-lab/`）仍未入库；世界书线亦压着未提交产物。**当轮成果当轮提交**（精确 pathspec）。
2. **修 `AWAKE.Tests` 两条跨线红**：根因已坐实 —— `docs/fixtures/persona-load-v2-golden.json` 的 `expectedDsl` 未跟随 `PersonaDslGenerator` 的段序改动（`PUBLIC` 已后置）。**不需要游戏**。
3. **工具候选（A）**：真件已在框架、玩法侧 0 调用 ⇒ **纯接线**，是 v0.3「活起来」的素材。
4. **美术接线 ＋ 色值复核**（需要先新开 `ui_awake_slot` category）。

---

## 四、离线**补不了**的那一步（别把这一步算进"闭环已完成"）

**项目自己的规矩**：**过版判据一律以游戏内为准，离线全绿不算过版。**

⇒ 所以本轮做完的正确产出，**不是"闭环成立"，而是一个「等你方便时一次点亮」的候选**。
⇒ 真机那一步只需要看三处（`docs/AWAKE-ROADMAP.md` 现状节已有口径）：

| 看哪 | 看到什么算通 |
|---|---|
| `…\Modules\AWAKE\Logs\Awake.log` | `npc_dialogue_ready` → `npc_dialogue_turn_completed`（带 `hero/generation/correlation`）→ `transcript_turn_appended` |
| 引擎 `rgl_log_<pid>_errors_*.txt` | 只有表头 |
| 引擎 watchdog | 只有 "Waiting for an exception event..." |

⚠ **时间戳是 UTC，比北京时间晚 8 小时**；⚠ 每次启动一个新 pid ⇒ 先按 mtime 找最新的。

---

## 五、纪律（防止这轮补出来的"绿"是假的）

项目已经踩过并立过规矩的四条，本轮**逐条适用**：

1. **新验台必须做一次变异检验**：故意改坏被测代码，确认验台会 FAIL。**全绿不算证据。**
2. **跑不起来的探针 ＝ 没验过**（历史教训：`ai_chain_sim.py` 硬调 `pwsh`，本机只有 PS 5.1 ⇒ 该入口**从来没跑过**）。
3. **恒 0 命中的探针 ＝ 没验过**：跑计数/查找前先做**阳性对照**（09-14 连撞三次假阴性）。
4. **恒 True / 恒 False 的判据 ＝ 没测**：判据引用的对象，先 grep **写侧**是否真的产出。

---

## 六、交付给代码线（可直接粘贴的提示词）

> **背景**：AWAKE 是骑砍 2 的 AI 运行时模组，工作区 `D:\AWAKE-Dev`。甲方暂时无法开机实测，要求先把**离线可补的闭环**补掉。
>
> **关键事实（已由主控线复核，勿再从头查）**：
> 1. `docs/AUDIT-AWAKE-DIALOGUE-CHAIN-010-20260911.md` 的三个 P1 **已过期**：P1-1、P1-2 **已修完**（证据 `NpcDialogueService.cs:474-475`、`:1666`），**只剩 P1-3 未修**（`NpcDialogueVM.cs:724` 仍 `CancellationToken.None`；注意 `AwakeMessengerVM.cs:478` 不是同一处）。
> 2. `awake.world_fact.root_corrupt` 在 `WorldStateStore.cs:895` **只报错不恢复**（无隔离/重建/降级）⇒ 这条**可离线复现也可离线验证**，是清单里最实的一条。
> 3. `npc_dialogue_open_failed` **离线复现不了**（Gauntlet UI），只能离线把前置条件穷举、收窄失败点。
> 4. `AWAKE.Tests` 两条跨线红的根因：`docs/fixtures/persona-load-v2-golden.json` 的 `expectedDsl` 未跟随 `PersonaDslGenerator` 段序改动（`PUBLIC` 已后置）。
>
> **要你做的（按此序）**：
> 1. 修 P1-3（UI 会话 CancellationToken），验证三种场景：关面板／切联系人／连点三次。
> 2. 给 `world_fact.root_corrupt` 补恢复路径（隔离坏分片 → 重建 root → 降级只读），并**用离线验台喂坏 root** 做正反两验。
> 3. 修 `AWAKE.Tests` 两条跨线红（不需要游戏）。
> 4. 工具候选接线。
> 5. 入库：当轮成果当轮提交，**精确 pathspec，勿 `git add -A`**（索引里有历史暂存条目）。
>
> **约束**：
> - 离线全绿**不等于**闭环成立；**不要**把本轮任何一项标记为"实机完成"。
> - 新验台**必须做变异检验**（改坏被测代码确认会 FAIL）。
> - 探针**跑不起来 ＝ 没验过**；**恒 0 命中 ＝ 没验过**（先做阳性对照）。
> - 真机那一步只留一次：等甲方方便时看三处日志（`Awake.log` 的 `ready → turn_completed → transcript_turn_appended`、errors 只有表头、watchdog 只有等待行）。
>
> **证据出处**：本文件 §三；`docs/AWAKE-ROADMAP.md` 现状节与「下一步」；`docs/AUDIT-AWAKE-DIALOGUE-CHAIN-010-20260911.md`（**已过期部分见上文 1**）。
