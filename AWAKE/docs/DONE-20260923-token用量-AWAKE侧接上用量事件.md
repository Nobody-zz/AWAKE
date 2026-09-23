# DONE · token 用量统计：AWAKE 侧接上用量事件（2026-09-23）

> 对单：`docs/HANDOFF-TOKEN-STATS-20260922-MAIN.md`（全局主控线 2026-09-22 出）
> 结论一句话：**数据三天前就送到门口了，缺的是最后那一行的接引；现在已经接上，并且改掉了一个会把账算错的地方。**

---

## 一、改了什么

| 文件 | 改动 |
|---|---|
| `src/AwakeTokenUsage.cs` | **新增**。零依赖记账：不碰存储、不碰游戏对象、不发事故 ⇒ 离线验台能直接压到它（沿用 P1-05 摘 `AwakeLetterCommit.cs` 的同一手法） |
| `src/NpcDialogueService.cs` | `OnTaskEvent` 里，**在事件分支之前**统一过一遍 `AwakeTokenUsage.Track(evt, NpcDialogueConstants.RouteId)` |
| `src/NpcMemoryService.cs` | `SummarizeAsync` 的事件回调里，**在条件链之前**统一过一遍（`NpcMemoryConstants.RouteId`） |
| `AWAKE.Tests/AWAKE.Tests.csproj` | 把新文件纳入离线验台编译清单 |
| `AWAKE.Tests/Program.cs` | 新增用例 `token-usage` |

**为什么放在分支之前，而不是给 switch 补一个 `case`：**
这条链上新增事件类型很容易漏。`NpcDialogueService` 是 `switch`（原本只接 5 类），`NpcMemoryService` 是 `if` 链（原本只接 3 类，连文本流都不处理）——两处形状不同，各自补分支就会各自漏。
放在入口统一过一遍，**以后框架再加事件类型，这里不需要记得改**。

---

## 二、一个会把账算错的地方（本单最有价值的修正）

**同一次请求，用量事件会来好几次；后一次报的是「从开始到现在累计」的值，不是这一段新增加的量。**

取证：

- `framework/MarcusAwakeProvider/src/ProviderAdapters.cs:179-183`：每一帧解析出 usage 就整体替换并发一次用量更新。
- `framework/MarcusAwakeFramework/src/RuntimeServiceClient.cs:911-913`：每收到一个用量帧就发一个同名事件给上层。
- Anthropic 协议尤其明显：累积多少是在 `message_delta` 帧里报的完整值，不是增量。

⇒ 所以**必须各自取最大的那一次，不能累加**。累加会把同一笔反复计入。
（这一点我也做成了判据：见第三节变异 B，累加实现会被挡下来。）

两边都照顾到：

- **终态事件的值也并入最大值** —— 有些 provider 只在终态给数（单发路径就是先发一次用量更新、紧接着发完成事件，两边同值）。
- **框架兜底发出的取消/失败，携带的是硬编码 0**（`RuntimeServiceClient.cs` 的 `PublishCancellation` / `PublishFailure` 写死 0/0）。这时全靠前面攒下来的用量更新才救得回来 —— **这正是"别只接终态"这句话的实际用处**，不是理论。

---

## 三、判据读数（先红后绿，另加两组变异）

用例：`AWAKE.Tests` 里的 `token-usage`，跑法 `cd AWAKE.Tests && dotnet run`。

```
① 改完之后（绿）
[21/67] ok   token-usage
PROBE token-usage 对话路由累计 in=150 out=100；记忆归纳路由累计 in=7 out=3
RESULT total=67 passed=67 failed=0

② 变异 A —— 停掉落日志那一行
[21/67] FAIL token-usage
FAIL_MSG ① 流式用量更新：一个任务应当恰好结算一行用量日志，实得 0 行。
RESULT total=67 passed=66 failed=1

③ 变异 B —— 把「取最大」改成「累加」
[21/67] FAIL token-usage
FAIL_MSG ① 输出口径：累计值取最大，不许把 30 和 80 加在一起：
        用量日志里缺 [out=80]。
        实得行：... in=100 out=110 route_total_in=100 route_total_out=110
RESULT total=67 passed=66 failed=1

④ 恢复之后
RESULT total=67 passed=67 failed=0
```

变异 B 的实得行就是活证据：**累加会把同一笔算成 110，正解是 80。**

**关于"改动前的红"**：这次没有用"编译不过"当红的读数——那不算判据有分辨力，只算东西还不存在。
改动前的真实状态另行取证：**用量事件在 `AWAKE/src/` 全树零命中**，两个消费点接的事件类型分别为 5 类与 3 类，都不含这一类。这个读数在今天跑 `grep -rn "UsageUpdate" AWAKE/src/` 仍可得（改完为 3 处）。

产品工程构建通过（`tools/build.ps1`，产物 `obj/Debug/Awake.dll` 时间戳已刷新）。

---

## 四、这条判据压不住的地方（必须知道）

判据压住的是**记账逻辑本身**（它是两处调用点共用的同一份实现，不存在两份口径）。
但**两个调用点各那一行，离线验台没有判据**：

- `NpcDialogueService` 编不进离线工程（经 Messenger 服务再到启动器会触及 Gauntlet 界面层）；
- `NpcMemoryService` 那条链要经提示词注册与真实网关才走得到。

⇒ **没验过就不冒充验过。** 真机确认方式：与 NPC 说一次话、再触发一次每日记忆归纳，去看游戏目录下 `Logs/` 里的日志，应当出现：

```
token_usage route=AWAKE.route.npc.dialogue task=... model=... outcome=completed in=... out=... route_total_in=... route_total_out=...
token_usage route=AWAKE.route.memory.daily  ...
```

**这一步我没跑，按"未验"记账。**

---

## 五、还没做的（要真机）

**单子 C 项：两条路由各花多少。** 离线造不出来，是这一批里唯一必须真机的部分。

一个坑要提醒：**日志满 2 MB 就轮转，而且只留一代**（`src/AwakeLog.cs` 的轮转逻辑）⇒ 长局会把早期记录冲掉。要做"切前切后对比"，就把这两条单独捞出来存到别处，别指望原日志文件留住。

---

## 六、自报四条

1. **另外两个 `switch (evt.Kind)` 不用改。** 在界面层还搜到两处同名结构，但它们处理的是**界面自己的事件**（流文本、完成、需要确认等），不是 AI 事件，跟用量无关。别误改。
2. **另外两条路由不是漏接，是根本没有人在用。** 路由常量里还有 `preprocess` / `postprocess` 两条，但它们在 `AWAKE/src/` 里**零引用**。真正烧 token 的就是已接的那两条 ⇒ 覆盖是完整的。
3. **预算门禁不在这条线上。** 用量的累计与卡额度在框架侧；AWAKE 侧现在只有观测。将来真要做"按成本决定走本地还是走云"，开关仍在框架那边 —— 本次改动只提供数据，不提供控制。
4. **有一类事件会被有意丢弃，因此不会结算**：对话侧对每个回合带"代号"做守卫，过期回合的事件一律不处理。这种情况下那一笔不会出现在日志里，账本里残留的上限有兜底（`PendingMaximum`），不会无限增长。

---

*—— 阿砚（主干线）· 2026-09-23*
