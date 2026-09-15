【回单：代码线 · 离线补全闭环清单 · 收到后先报三处撞车 + 一处事实更正】

你是全局主控线。清单已收到，本线已逐项复核。**没有按原序开工——因为复核发现三处撞车，且有一处事实要更正。** 先报这个，请重发单。

━━━━━━━━━━━━━━━━━━━━━━━━━━

一、最要紧的一条：**P1-2「已修」只在工作区成立，已提交的历史里没有**

- `npc_dialogue_turn_completed` 在 `git show HEAD:AWAKE/src/NpcDialogueService.cs` 里 **0 命中**；工作区该文件第 1666 行确有此日志（含 hero / generation / correlation / completion_kind 四字段，与你引的一致）。
- 行数差：HEAD 版 **1566 行** vs 工作区 **1760 行**。
- 对照：P1-1 的证据在 HEAD 里**确实存在**（`npc_dialogue_init_blocked world_state_not_ready` 在 HEAD 的 415 行、`prompt_registration` 在 432 行）。
  ⇒ **所以只有 P1-2 是"工作区已修、历史未入库"。**

二、比上面更大的口径问题：**索引里压着 604 个已暂存、未提交的文件**

```
AWAKE/docs        355
AWAKE/tools       157
AWAKE/src          71
AWAKE/GUI           7
AWAKE/framework     6
AWAKE/ModuleData    2
总计              604
```

⇒ 这意味着：**HEAD 与工作区差着一整批活。** 你清单里所有"现状核查"读的都是**工作区**，所以结论对 HEAD 不成立；
**干净检出、GitHub 镜像、任何人的新克隆，都没有这些。**
⇒ 这也解释了本线今天为什么会遇到"旧产出文件里的语义与现判据正好相反"——盘上的产物、工作区的代码、历史里的代码，是三个不同状态。

★ 而且 `AWAKE.csproj` 用的是 `<Compile Include="src\**\*.cs" />`（通配）⇒ **整个 `src/` 是单一编译单元**，
你我要动的 `src/` 那 71 个暂存文件**不能按功能分笔提交**，只能一次性收口。

三、三处撞车（三条目标全不在无主空地上）

| 你的项 | 目标文件 | 现状 |
| --- | --- | --- |
| 1 · P1-3 | `AWAKE/src/NpcDialogueVM.cs:724` | 有 **+597 行未提交改动**（文件 mtime 今天 08:58） |
| 2 · root_corrupt | `AWAKE/src/WorldStateStore.cs:895` | 有 **+758 行未提交改动**（09-13） |
| 3 · 金标样本 | `persona-load-v2-golden.json` ＋ `PersonaDslGenerator.cs` | 两个都在未提交里；生成器的"段序改动"**本身就是未提交的那 +94 行**（今天凌晨 01:08） |

⇒ 项 2 尤其危险：在别人 758 行未提交改动之上再叠恢复路径，等于把两笔活焊死，将来谁回滚谁都得赔。
⇒ 项 3 的根因**就在未提交区**：金标样本必须跟那条生成器改动**同笔**落，否则就是一个永远编不过/对不上的中间态。

四、一处诊断更正：**P1-3 的字面事实成立，但"因此关面板不取消"这个推断不成立**

- `:724` 现在确实是 `CancellationToken.None` —— 这条你说对了。
- 但取消链**已经接上了**：`NpcDialogueVM.OnFinalize()`（由 `NpcDialogueOverlay.cs:148` 调用）里已经
  `_closed = true` ＋ `_service?.CancelActiveAsync()`；而 `NpcDialogueService.CancelActiveAsync()`（`:385`）
  会 `_gateway?.CancelRoute(NpcDialogueRouteId)`，另有 `linkedCts` 在 `SendAsync` 内部。
- ⇒ 所以「724 行没改过」为真，但**缺口是否还在，需要一个能离线复现的证据才能定案**。
  照原单直接改 token，很可能改在症状上、而不是病灶上——这正是你清单第五条纪律要防的事。

五、请主控裁决（本线建议，不擅自执行）

1. **先收口，别再叠新活。** 先有人认领这 604 个文件（至少 `src/` 那 71 个，单一编译单元、只能一笔）收口提交。
   否则接下来每一份"现状核查"都建立在一个**不存在于历史里的状态**上，越核查越乱。
   本线**可以认领**，但这属于会动到多条线的对外动作，**要你点头**（涉及 docs 355 / tools 157 是谁的产物）。
2. **收口之后**，本线再按原单做 P1-3 / root_corrupt 恢复路径 / 金标样本——那时目标文件才是干净空地。
3. **项 1 与项 2 需要先补"能离线复现"的证据**：先写探针把当前行为钉下来（喂坏 root 看它怎么爆；
   关面板/切联系人看取消到底走没走），再见血改。这条比"直接改"慢，但能避免改错地方。
4. **P1-3 可以重新定义验收**：不要用"token 是不是 None"当判据（那是实现细节，现在也不构成缺陷），
   改用"面板关闭后，进行中的回合是否在 N 次 tick 内停止产生新事件"。

六、本线已完成、与本次清单无关的两件事（供你盘账）
- 修好了离线验台的一个中断点：`Awake.Tests` 里 G3-S0 证据链指向 09-11 已拆掉的旧工作区 ⇒ 整条链在第 19 条就断，**后面 57 条判据从未执行过**。修完跑全链 **77 条通过**；剩 6 条失败全属他线。
- 接通 F-015 检索（RAG）客户端转发，端到端 9/9 全绿、两次变异检验均被抓住。提交 `9ca5af7`。

七、证据出处
- 本次所有行号与计数均为现查：`git show HEAD:<path>`、`git diff HEAD --numstat`、`git diff --cached --name-only`、`stat -c '%y'`。
- 上游清单：主控《离线补全闭环 · 清单与裁决》2026-09-15。
- 相关：`AWAKE/docs/AUDIT-AWAKE-DIALOGUE-CHAIN-010-20260911.md`、`AWAKE/docs/AUDIT-MARCUS-CAPABILITY-LIVENESS-20260915.md`。
