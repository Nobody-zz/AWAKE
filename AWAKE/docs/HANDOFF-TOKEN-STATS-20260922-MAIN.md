# 派工单 · 主干线 · token 统计（2026-09-22）

> **这份文件整份就是提示词，全选复制给主干线会话即可。**
> 出处与取证记录在 `.workbuddy/memory/2026-09-22.md`；本单只给任务。

---

## 【背景】

甲方 09-22 裁定 **「主对话用API」**（`docs/DECISION-20260922-主对话走云端API.md`）。
⇒ 本地化能动的只剩**一层**：每日记忆归纳（`AWAKE.route.memory.daily`）。
⇒ 但"切到本地能省多少"**没有数** —— 09-16 就定了「**先补 token 统计**」，到今天没做。

**先给一条判断，省你半天：token 数据不是没有，是送到了、没人接。**

## 【事实层：这条链已经走到第 5 环，只差最后一步】

我逐环查过，**每一环你都可以自己复核**：

| 环 | 干什么 | 出处 |
|---|---|---|
| 1 | Provider 解析 usage | `framework/MarcusAwakeProvider/src/ProviderResponseSupport.cs:117`（OpenAI 单发）、`:230`（流式）、`:266/:271`（Anthropic）、`:302-305`（Ollama） |
| 2 | 适配器把 usage 发成流事件 | `framework/MarcusAwakeProvider/src/ProviderAdapters.cs:182,380,573` |
| 3 | 运行时服务累计 + 预算门禁 | `framework/MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs:3896-3943` |
| 4 | 框架客户端发 `UsageUpdate` 事件、终态带真值 | `framework/MarcusAwakeFramework/src/RuntimeServiceClient.cs:913`（流式）、`:976`（单发）、`:984`（终态） |
| 5 | 框架自己测过 | `framework/MarcusAwakeFramework/tests/P3DA2FrameworkTests.cs:85,146` |
| **6** | **AWAKE 侧接住** | ❌ **缺口就在这里** |

**缺口的确切形状**（这是全单最要紧的一段）：

**事件类型一共 8 个**（`framework/MarcusAwakeFramework/src/AiGatewayApi.cs:8-17`）：
`Accepted`｜`Started`｜`TextDelta`｜**`UsageUpdate`**｜`RouteChanged`｜`Completed`｜`Cancelled`｜`Failed`。

- `UsageUpdate` 这个类型，**在 `AWAKE/src/` 全树零命中**。框架发了，AWAKE 一个分支都没写。
- `src/NpcDialogueService.cs:1289-1307` 是 `switch (evt.Kind)`，只接 5 个：
  `TextDelta`／`RouteChanged`／`Completed`／`Failed`／`Cancelled`
  ⇒ **漏 3 个：`Accepted`／`Started`／`UsageUpdate`**（前两个你不关心无所谓，`UsageUpdate` 是这次要的）。
- `src/NpcDialogueService.cs:1315 HandleCompleted`：拿到了完整的 `AiTaskEvent evt`，**只读 `StructuredJson` 和 `Text`**。
- `src/NpcMemoryService.cs:492-510` ⚠️ **这里不是 `switch`、是 `if` 链**，只处理 `Completed`／`Failed`／`Cancelled` 三个
  （连 `TextDelta` 都不处理，记忆归纳不需要流式）
  ⇒ **加 `UsageUpdate` 得多加一条分支，别只改上面那处 `switch` 就以为完事。**
- `src/AwakeLog.cs`（76 行）只有一个 `Write(string)` ＋ 一个 `Recorder` 钩子，**没有任何累计/汇总能力**。

⇒ 所以 09-16 那条读数「日志里 `token` 0 命中」**没错，但归因浅了**：
不是"没采集"，是**上游三天前就采完了，最后一个 `switch` 没写那一行**。

**补一条细节，别漏**：失败和取消的终态带的是 `0`（`RuntimeServiceClient.cs:990,996`），
但**取消的调用照样烧了 token**（已经流出来的部分）。所以别只接终态，`UsageUpdate` 也得接。

## 【要什么】

**A. 接上（改代码）**
两个消费点读 `evt.InputTokens` / `evt.OutputTokens`，落成一行**能按路由区分**的日志。
路由标识从哪来你定（`NpcMemoryConstants.RouteId` / 对话那条路由）。

**B. 先给红的读数（这一步不能省）**
现在全绿，说明这条没人测。**你要先让判据红起来，再让它绿**；只交绿的不算完成。

⚠️ **先消一个误会**：`AWAKE.Tests/RedtestR1Behavioral.cs:85-91` 有两条用例写着"离线不可构造"。
那句说的是**"经真实 Provider 提交产生事件"**那条路径（要 framework internal 的 `ICompatibilityGameDataService`）。
**本单不走那条路** —— 造一个假网关**直接发事件**即可，与那句注释不冲突。别被它挡回来。

离线可证，而且**工具是现成的**：

- `src/AwakeLog.cs:10` 有一个 `internal static Action<string> Recorder` 钩子
  ⇒ 测试里设它就能抓日志行，**不用读文件、不用真机**。
- 假宿主的 AI 是空的：`AWAKE.Tests/AwakeTestFakes.cs:267` `public IAiGateway Ai => null;`
  ⇒ 现在走不到 AI 链。这是唯一的拦路石。
- **参照物现成**：`framework/MarcusAwakeFramework/tests/TestDoubles/FixtureAiGateway.cs:84-90`
  的 `Complete(taskId, text, inputTokens, outputTokens)` 会依次发
  `TextDelta` → **`UsageUpdate`** → `Completed`。照它做一个 AWAKE 侧的即可。

**C. 真数（要真机，可另附一笔）**
跑一次游戏，看 `npc.dialogue` 与 `memory.daily` 各花多少。**这个数离线造不出来**，是两张单里唯一必须真机的部分。
⚠️ 顺带一个坑：日志 2 MB 就轮转，且**只留一代**（`src/AwakeLog.cs:60-70`）⇒ 长局会把早期记录冲掉。
要"切前切后对比"的话，你自己决定要不要落到别处。

## 【验收】

1. **先红后绿**，红的读数与绿的读数都给出来。
2. **变异检验**：把新加的那一行注释掉 ⇒ 判据必须重回红。证明不了它还能 FAIL，就等于没测。
3. 日志里能按路由区分，能看出哪条花了多少。
4. 若你判断"其实不是缺陷"，**写明这也算有效产出** —— 但要有证据。

## 【红线】

- **只动 AI 事件消费这几个文件。** `src/WorldKnowledge{Loader,Models,QueryService}.cs` 是世界书线的在途改动，**不许碰**（它们跟你在同一个编译单元里，是既有事实，不是你能改的）。
- **不宽 pathspec 提交**：`src/` 是多线共父目录，只带你自己的文件。
- **不排期**（项目纪律）。
- **不动 `framework/`**。缺的那一步在 AWAKE 侧。若你查完认为框架侧也有问题，写进报告，别改。

## 【出处】

- 甲方裁定：`docs/DECISION-20260922-主对话走云端API.md`
- 现状与四层本地状态：`docs/STATE-LOCAL-MODELS-20260922.md`
- 09-16 三件已定的账：`docs/ANIMUSFORGE-LOCAL-MODEL-ANALYSIS-20260916.md` §8.4
- 本单取证过程：`.workbuddy/memory/2026-09-22.md`

---

*—— 全局主控线 · 2026-09-22*
