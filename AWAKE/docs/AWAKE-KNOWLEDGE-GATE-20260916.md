# 知识门 · 一句判断：它被当成「发言闸门」用了（09-16）

> **态**：工作区 ＝ HEAD（`de2b9a0`）。`NpcDialogueService.cs`(09-13 15:03) / `WorldKnowledgeModels.cs`(09-12 16:58) / `WorldKnowledgeQueryService.cs`(09-14 02:07)
> 三份均 `git status --porcelain` 为空 ⇒ **下述行为在工作区与 HEAD 一致**，不是未提交的中间态。
> **性质**：不是"新发现的 bug"。是一条**设计与实现的正面冲突**（§三），且**验收站在了实现那一侧并把它写成了期望值**（§四）。本文只出判断，**未改任何代码**。
>
> ⏳ **09-17 晚通读复核（红测第十七轮）：判断与证据仍成立；行号已全面失效，别照抄。**
> - **一手证据复核过、逐字对上**：`docs/evidence/ai-chain-sim-fix-20260914/result.json` 两个用例
>   `noble:领主` → `aiCalled=true`／`replySource=ollama`，`noble:收成` → `aiCalled=false`／
>   `replySource=knowledge_direct_fallback` —— **正是 §一 那张表的两行**。该文件自己也写明"不启动 Bannerlord"（§六 的免责一致）。
> - ~~**行为今天仍在**：`AllowsAi` 仍只认 `known/partial`、`:1125` 仍提前 return、`:278` 仍走 `CompleteDirectKnowledgeTurn`。~~
>   🚩 **09-18 00:10 起这句不成立了**：`AllowsAi`（现 `WorldKnowledgeModels.cs:151`）改成"只有 `blocked`／`referral` 闭嘴"，
>   `not_found` 放行 ⇒ `:278` 不再走 `CompleteDirectKnowledgeTurn`（该函数现只服务 `blocked`／`referral`）。
>   **这句原本是在说 09-17 那天的态**，别当现状读。落地记录见 §七 与 `docs/DONE-20260918-拆开知道与开口.md`。
> - **但本文所有行号是 `de2b9a0` 那版的**，09-17 同一批文件都改过 ⇒ 实际位置已整体后移（`QueryService` 那组偏约 +122）。
>   常用几个的对照：本文 `WorldKnowledgeQueryService.cs:206-223 FindCandidates` ⇒ 现 **`:328`**；
>   `WorldKnowledgeModels.cs:121 默认值` ⇒ 现 **`:125`**；`:137 AllowsAi` ⇒ 现 **`:141`**；
>   `:297 ShouldCallAi` ⇒ 现 **`:301`**；`NpcDialogueService.cs:1125`／`:278`／`:1080`／`:1059`／`BuildDirectReply`（原 `:268`）
>   ⇒ 现 **`:1125`／`:278`／`:1080`／`:1059`／`:272`**（这几个恰好没漂）。
> - ~~**裁决状态未变**：§七 仍是「待裁决、不自行开工」。~~ ✅ **09-17 23:40 已裁**：甲方批「动」
>   （原话「可以，那就做」；他选的是**"拆那根裁决"**）⇒ §七 已改成「**已批准，待开工**」，
>   并补了两项开工前必读（**行号核正** / **连带面**）。上下文登记在 `docs/DECISION-20260916-无命中仍须正常对话.md` 的确认节。
> - ⚠️ 而 09-17 新加的判据 E 用 `AllowsAi == false` 当"该空手"的正确观测 ——
>   **裁定下来之后这条更严重了**：不再是"裁决前把现状认成了对"，而是**判据与裁定方向相反**
>   ⇒ 判据 E 的观测点必须跟着改（见 §七 连带面第 1 条），否则会留一条**恒绿/恒红的假闸**。

---

## 一、判断

**代码没有限制 AI 怎么说话；代码决定了这一轮让不让 AI 开口。** 而这道闸门的开启条件被设成了「必须命中世界书关键词」。

后果不是"回答得不好"，是**整轮对话被短路**——云端 AI 根本不会被调用，玩家只得到一句写死的台词：

| 玩家的说法 | 实际发生 | 玩家看到 |
|---|---|---|
| 说中一个关键词（`领主`） | `state=partial` → 调 AI，正常生成 | 「边境紧张，帝国部队已深入。箭雨准备好没？」 |
| 没说中（`收成`） | `state=not_found` → **不调 AI** | **「这件事我没听说过。」**（mood=茫然） |

第二行有一手实测：`docs/evidence/ai-chain-sim-fix-20260914/result.json`（case `noble:收成`，`aiCalled=false`、`replySource=knowledge_direct_fallback`）。

---

## 二、这条链的完整形状（逐环出处）

> 🚩 **本节描述的是 09-17 23:40 裁决之前的形状，不再是现状。** 09-18 00:10 落地后，下面
> **第 53-59 行那三环（`AllowsAi` → `:1125` → `:278` → `BuildDirectReply`）已经改了**：
> `AllowsAi` 对 `not_found` 返 true、知识格由 `BuildPromptBlock` 单独决定给不给、`:278` 不再短路。
> 本节的**上半段（玩家原话 → 检索 → 候选为空 → 默认 not_found）仍然逐字成立**，只有出口变了。
> 新的出口形状见 `docs/DONE-20260918-拆开知道与开口.md`。**保留原文是为了留改动前的证据，不要照它读现状。**

```
玩家原话
  └─▶ 每轮必经，无条件：NpcDialogueService.cs:1080  worldbook.Query(worldbookQuery)
        （worldbookQuery.PlayerText = 玩家原话，:1059）
        └─▶ WorldKnowledgeQueryService.cs:206-223  FindCandidates(PlayerText)
              纯子串双向匹配：text.IndexOf(kw) 或 kw.IndexOf(text)
              一个关键词都没命中 ⇒ ids 为空
        └─▶ :42 candidates 空 → :50 循环不执行 → :75 builder.Length==0
              → 既无 referral 也无 blocked ⇒ **State 保持默认值**
              （默认值 = not_found，WorldKnowledgeModels.cs:121）
        └─▶ WorldKnowledgeModels.cs:137  AllowsAi
              = (known || partial) && RetrievedText 非空  ⇒ **false**
        └─▶ NpcDialogueService.cs:1125  if (!knowledgeDecision.AllowsAi)
              return new NpcKnowledgePromptBuildResult(decision, string.Empty)   ← PromptText 空
        └─▶ NpcDialogueService.cs:278  if (!promptBuild.ShouldCallAi)
              ⇒ CompleteDirectKnowledgeTurn(...)   ← **在这里离开链路，永不 SubmitAsync**
        └─▶ BuildDirectReply:268  返回「这件事我没听说过。」
              AwakeLog: worldbook_direct / mood=茫然
```

**关键点：这条路上没有一处是"异常"或"降级"。** 全部是正常分支、正常返回。它是一条**设计好的正常路径**。

### ★ 而且它挡在装配流程的**最前面**——挡掉的不只是知识

`:1125` 那个提前 return **位于所有材料装配之前**。它一旦命中，下面这些**全部不执行**（全在 `:1130` 之后）：

| 本该装上的材料 | 位置 | 内容 |
|---|---|---|
| 角色设定 | `:1136` `PersonaSessionHydrationAdapter.HydrateAsync` → `:1144` `BuildPersonaProjection` | `persona_dsl`（这个 NPC 是谁） |
| 角色身份 | `:1174` `BuildNpcIdentity()` | `npc_identity` |
| 角色状态 | `:1153-1167` | `npc_state`（身体、处境、未命名约束） |
| 记忆 | `:1173` `_memoryBlock` | `npc_memory`（他记得什么） |
| 未决承诺 | `:1177` `_npcCommitments` | `npc_commitments` |
| 玩家已知 | `:1180` `SerializePlayerKnown(...)` | `player_known`（他认识玩家吗） |
| 场景 | `:1181` `_sceneKeywords` | `scene` |

⇒ **NPC 丢掉的不只是"这件事"，还有"他是谁、他记得什么、他和玩家什么关系"。**
玩家问「收成」，NPC 回「这件事我没听说过」——**这是一个连自己是谁都没带上的 NPC 说出的话。**
（"正常对话"至少需要三样：**这个人是谁 ＋ 他记得什么 ＋ 他知道什么**。前两样代码里早就写好了，就排在知识门后面。）

### ★ 是两道锁，不是一道

想放行 AI，**光拆 `:1125` 不够**：

1. **第一道**：`:1125` 提前 return ⇒ 装配根本不跑。
2. **第二道**：`WorldKnowledgeModels.cs:297` `ShouldCallAi = Knowledge.AllowsAi && PromptText 非空`
   —— 而 `AllowsAi`（`:137`）只认 `known/partial`。**即使装配跑完、prompt 编译成功，`ShouldCallAi` 仍然是 false。**
3. **`NpcDialogueService.cs:278` 拿 `ShouldCallAi` 做判据** ⇒ 照样 `CompleteDirectKnowledgeTurn`。

⇒ 两道锁各自独立、都指向"不命中就不说话" ⇒ **这个意图是被刻意强化过的**（不能记成"当初没意识到"，见 §三）。

> 🚩 **09-18 00:10 落地结果**：**第一道锁根本没动**（`:1125` 的提前 return 保留），**只改第二道（`AllowsAi` 的定义）**。
> 原以为"两处必须同时改"，实测下来落点选在 `AllowsAi` 上就自动成立 —— 详见 §七 末尾的实测修正。
> 保留下来的那道 return 现在只拦 `blocked`／`referral`，反而省掉一遍白装配。**"两道锁"这个描述已经过时，现状是一道半。**

### 附 · 完整链路全景（一轮对话从头到尾）

**① 打开面板时（只做一次，结果缓存在字段里，本轮不再读）**

| 装什么 | 出处 | 从哪读 |
|---|---|---|
| 记忆块 `_memoryBlock` | `:489` / `:567` → `LoadMemoryBlockAsync`（`:659`） | `NpcMemoryService.LoadMemoryBlockAsync(heroId)` |
| 状态 `_npcState` ＋ 未决承诺 `_npcCommitments` | `:490` / `:568` → `LoadNpcStateAsync`（`:680`） | `store.GetRelationshipAsync` ＋ `store.GetInteractionsAsync` |
| 玩家已知（姓名/家族/王国） | `LoadPlayerKnownAsync`，按天刷新（`_playerKnownRefreshDay`，`:649`） | 游戏对象 |

**② 每一轮发言**

```
空输入挡掉（:225 ImmediateFail「对方在等你开口。」）
  → 构造世界书查询（:1044-1061，塞入玩家原话 ＋ NPC 身份/文化/王国/聚落/角色/性别/技能 ＋ 场景词）
  → 查世界书（:1080）
  → 判五态（:1110，known/partial/referral/blocked/not_found）
  → ★ 门①（:1125）
  → 装配角色材料（:1130-1189：角色卡 hydrate＋投影成 DSL / 身份 / 状态 / 记忆 / 承诺 / 玩家已知 / 场景）
  → 填模板 ＋ 定预算（:1194 BuildBounded）
  → 权限门（:1208 PermissionGate）
  → 编译提示词（:1219 _host.Prompts.CompileAsync，PromptId=awake.npc.v1）
  → ★ 门②（ShouldCallAi）
  → 发往云端（:305 _gateway.SubmitAsync，路由 AWAKE.route.npc.dialogue，输出契约 awake.npc.output.v1）
  → 事件流回来（onEvent → OnTaskEvent）→ 解析 reply/mood/effects/command → 上屏
```
提示词硬上限 **32 KB**（`MaxPromptUtf8Bytes = 32768`）。

**③ 关闭面板时（只做一次）**

`Dispose()`（`:398`）→ `NpcMemoryService.Reserve`（`:416`）→ 后台 `CloseConversationAfterCommandsAsync`（`:420`）把整段对话压成摘要。
⇒ **这一步会再发一次云端调用**（路由 `AWAKE.route.memory.daily`）⇒ 一次对话 = 两次云端往返。

**④ 路由与模型配置**（`docs/profiles.awake.deepseek.routes.json`）

| 路由 | enabled | allowCloud | pinModel | 实际在用？ |
|---|---|---|---|---|
| `AWAKE.route.npc.dialogue` | true | true | true | ✅ 主对话 |
| `AWAKE.route.memory.daily` | true | true | true | ✅ 关面板时 |
| `AWAKE.route.preprocess` | true | true | true | ❓ **待验证**（`Awake.log` 里 0 命中） |
| `AWAKE.route.postprocess` | true | true | true | ❓ **待验证** |

模型：`deepseek.deepseek-v4-flash` = **enabled**；`qwen2.5-local` = **disabled**（本机 Ollama 可用，但未接）。
⇒ 两条 preprocess/postprocess 路由**配好了、可能是空的**（配了但没接线）——这是另一处"配置与执行不一致"，未验证，见 §六。


---

## 三、设计侧本来想要的是什么（08-15 原话）

`docs/AI-Retrieval-Memory-Principles-20260815.md:21-24` 把三级回退写成了：

```
→ 语义层：Marcus RAG（慢、但语义泛化）
→ 本地回退：倒排索引关键词命中
→ 无命中：不注入，用女神本知/角色本知作答     ← 第 23 行
```

同篇 `:368` 再写一遍：

> 如果 RAG 无命中，把它的预算让给记忆或角色状态，**而不是留空**。

**⇒ 设计意图是双重的、且方向明确：「无命中」不等于「不说话」。**
它说的是：**世界书那块不注入，但 AI 照常调用**，让角色用本知 + 记忆 + 角色状态来说；那块预算让给记忆，别留空。

**实现做的是另一件事**（见 §二）：无命中 ⇒ `AllowsAi=false` ⇒ **压根不调 AI** ⇒ 一句固定台词。

| | 「无命中」时该怎么办 |
|---|---|
| **设计（08-15，两处原话）** | 不注入世界书，**用角色本知作答**（AI 照常跑） |
| **实现（当前 HEAD）** | 不注入，**也不作答**（AI 不跑，回固定台词） |

⇒ **这是一处「设计与实现的正面冲突」，不是取舍问题。** 二者都同意"不注入"，分歧全在"要不要继续说"。
⇒ **而卡在中间的是验收**（见下节）：验收站在了实现这一侧，并把这一侧写成了期望值 ⇒ 设计意图永远无法生效。

---

## 四、为什么说它是"被验收过的设计"——这才是最要命的

它被三处档案各自记过，且**每一处都当作「正确行为」**：

| 出处 | 怎么写的 | 定性 |
|---|---|---|
| `docs/AWAKE-GLOBAL-MECHANISM-AUDIT-20260827.md:109` | 「无命中时返回 `not_found`。`AllowsAi` 只允许 `known/partial` 且有正文时调用 AI」 | 记为**机制说明**，未列为问题 |
| `docs/PLAN-AWAKE-DIALOGUE-CHAIN-010-20260911.md:78`（D-13） | 「**负路径**（无关键词命中）`AllowsAi=false state=not_found`」 | 记为**验收通过项** |
| `docs/evidence/ai-chain-sim-fix-20260914/result.json` | `"expectedGate": "not_found"`，全场 `"status": "pass"` | 验台判**绿**，且写成了**期望值** |

⇒ **`not_found` 不是被漏掉的边界，是被写进期望值的正确答案。**

⇒ 因此：**改这道门不会挂任何一条现有测试**——测试会告诉你"你改坏了"。这是本次最重要的风险提示。

> 与项目既有纪律的呼应：这属于「**判据失效**」而非「字段死掉」。验台把缺陷固化成期望值之后，
> 它不再具备证伪能力（`验台必须做变异检验`那条纪律，在这里正好被违反：把 `not_found` 改成别的值会红，但改成对的也红）。

---

## 五、与 09-16 关键词红队报告的关系

`docs/worldbook-migration/REDTEST-KEYWORD-20260916.md` 测的是**第 2 环**（匹配准不准），结论：48 条真人式对抗样本 **RED 30 / OK 18**。

本文补的是**第 4–5 环**（没命中之后会怎样）。两份拼起来才是完整的因果链：

```
真人式说话  →  子串漏报（红队：62%）  →  not_found  →  不调 AI  →  一句死台词
             └── 红队报告的边界 ──┘                 └── 本文补的 ──┘
```

**⇒ 这把红队报告的伤害等级上调了。** 报告里"漏报 19/35"读起来像"检索准确率问题"，接上第 4–5 环后，它的真实含义是：

> **玩家每 3 句真人式提问里约有 2 句，NPC 会回「这件事我没听说过」。**

不是"答得不准"，是**零信息输出**。

**而且触发方向是反的**：玩家说得越口语、越像正常人聊天，越容易不命中（关键词库里大量是专名——`REDTEST-KEYWORD` §三：458/458 条目都把 `doc.<domain>.<slug>` 写进了 keywords，`geography` 一条覆盖 408 档）。**只有说出一个专名，NPC 才会活过来** ⇒ 当前入口实质上是「关键词触发器」，不是「对话」。

---

## 六、边界与未验项（避免夸大）

1. **未在真机验证过这个现象。** 判据全部来自源码 + 离线模拟（09-14 那次是 `qwen2.5:latest` + 模拟器，不启动游戏）。真机上是否每轮都走到这道门，**待验**。
   验法（成本很低）：真机跑一次，看 `Awake.log` 里 `npc_dialogue_knowledge_decision` 的 `state=` 分布，以及 `worldbook_direct` 出现频次。**但注意 `Awake.log` 目前没有 ms/token 记账**（见 `ANIMUSFORGE-LOCAL-MODEL-ANALYSIS-20260916.md` §九），这条日志是否真的每轮都写，也需一并确认。
2. **未测真人语料。** 62% 出自**设计好的对抗样本**，不是玩家真实说法（`REDTEST-KEYWORD` §四-3 已自陈）。
3. **"有意"但没写理由。** 两道独立的锁（§二·★）说明这个行为是**刻意强化过的**，不能记成"当初没意识到"。
   但三处档案里**没有任何一处写下理由**（没有任何一句话说"我们故意让无命中的轮次不开口"）——而设计文档写的恰好相反（§三）。
   **仍需甲方裁决**：是后来改了主意但没更新设计文档，还是两处各自演化、从没对过。
4. **未评估改动的连带影响。** 一旦放开这道门，`PersonaSessionHydrationAdapter.HydrateAsync` / `_host.Prompts.CompileAsync` 等后面几段会被真正跑到（现在被短路挡在前面），**可能暴露出新的性能与失败点**——09-14 的 82 次 `ShoutContext.Build` 累计 155,102 ms 就是在有命中的路径上测到的。
5. **本文只出结论，未改代码、未改数据。**

---

## 七、修法方向（09-17 23:40 已批准 → **09-18 00:10 已落地**）

> ✅ **落地记录见 `docs/DONE-20260918-拆开知道与开口.md`。**
> ⚠️ 本节下面的「改动清单」**与实际做法有出入**，以 DONE 那份为准。最要紧的一条先摘出来：
> 第 1 项「删掉 `:1125-1128` 的提前 return」**没做，也不需要做** ——
> 那处代码的语义（"不能开口就别装配"）本来就对，错的是 `AllowsAi` 的定义；
> 把定义改掉它自动就对，而保留短路反而省掉 `blocked`／`referral` 那一遍白装配。

> ✅ **09-17 23:40 甲方批了「动」。**（原话一字不改：「可以，那就做」。
> 此前我列了三件候选请他指认"那就做"指哪一件，他选的是**「拆那根裁决」** —— 即本节这件事。
> 上下文登记在 `docs/DECISION-20260916-无命中仍须正常对话.md` 的确认节。）
> ⇒ 本节从"**待裁决**"变成"**开工单**"。**但开工前先读下面两个格**：本节写于 09-16，
> 之后代码动过、验台也动过，而且**判据 E 是 09-17 才加的、本节没覆盖它**。
>
> ⚠️ **行号已核正（红测第十九轮，09-17 晚）** —— 本节原文一共引了 **4 处**位置，
> 其中 **3 处已失效、1 处没漂**（见下表末行）：
>
> | 本节原文 | 今天 |
> |---|---|
> | `WorldKnowledgeModels.cs:137` `AllowsAi` | **`:141`** |
> | `WorldKnowledgeModels.cs:297` `ShouldCallAi` | **`:301`** |
> | `WorldKnowledgeDecisionPolicy.cs:226-232` | **`WorldKnowledgeModels.cs:230-236`** |
> | `NpcDialogueService.cs:1125-1128` | **`:1125-1128`**（这个**没漂** ✔） |
>
> 其中第三行**文件名也是错的**：`WorldKnowledgeDecisionPolicy` 是 **`WorldKnowledgeModels.cs` 里的一个类**，
> **没有独立文件**。照原文去找那个文件会找不到。
> 机制本身没变：**`not_found` 之下**"清空三个列表 ＋ 清 `blocked_reason`"仍是那 7 行（`WorldKnowledgeModels.cs:230-236`）。
> ⚠️ **别读成 `blocked`**（红测自纠）：`blocked` 那一支是 **`:223-229`**，它做的是"把 `blocked_reason` 补成 `"unknown"`、**不清它**"，
> 而 §七 明说 **`blocked` 保持现状、不动**。要改的是 **`not_found`** 这一支 —— 把原来那 7 行的清空，换成填一段"未知约束"文本。

> **修法不是新方案——就是回到 08-15 已经定下的那句（§三）：无命中时「不注入，但照常作答」。**
> 代码要做的是「**别在无命中时离开链路**」，不是「发明一套新兜底」。所以这条的成本被严重低估了：它更像是**接线错误**，不是**能力缺失**。

问题的形状是**两件事被绑成了一根**：

| 该分开的两件事 | 现在 | 应该 |
|---|---|---|
| 「这事 NPC 知不知道」 | 与下一行**共用同一个开关** | 只影响**给模型什么材料** |
| 「这一轮让不让 NPC 说话」 | = 知道才说 | 只有**系统故障**（`blocked`）才该拦住 AI |

⇒ 方向：**把「不知道」和「闭嘴」解耦** —— 让这道门**只决定"给多少知识"，不决定"要不要开口"**。
换个说法：**把门往后挪**，挪到"知识"这一格上，而不是挡在整个装配流程前面（§二·★）。

**改动清单（两处，必须同时改，只改一处等于没改）**：

| # | 位置 | 现在 | 改成 |
|---|---|---|---|
| 1 | `NpcDialogueService.cs:1125-1128` | `!AllowsAi` ⇒ 提前 return 空 prompt，**装配不跑** | **删掉这个提前 return**，让它继续装配（`:1130-1189` 本来就是写好的） |
| 2 | `WorldKnowledgeModels.cs:137` `AllowsAi` 或 `:297` `ShouldCallAi` | 只认 `known/partial` | 让 `not_found` 也能出话；**知识那一格保持空/换成"未知约束"，其余材料照常注入** |

- 只改 1 ⇒ 装配跑完、prompt 编译成功，但 `ShouldCallAi` 仍 false ⇒ **照旧短路**（第二道锁，见 §二）。
- 只改 2 ⇒ 装配根本没跑 ⇒ **照样是空 prompt**。

> 🚩 **09-18 落地时实测修正 —— 上面这两句的前提不成立。**
> 它们按的是 `AllowsAi` 的**旧定义**（只认 known/partial）。实际把落点选在第 2 项（改 `AllowsAi` 定义）之后：
> `ShouldCallAi = AllowsAi && PromptText 非空`，装配跑完 prompt 编译成功 ⇒ **只改这一处，链路就通**，
> 第 1 项那处提前 return 自动就对了、**不需要删**。
> ⇒ 「两处必须同时改」这个前提，**只在落点选在第 1 项时才成立**。
> 第 2 处（`BuildPromptBlock`）仍然改了，但理由换了一个：不让"给不给知识"继续绑在"能不能开口"上，
> 并保住 `dialogue_context.knowledge` 的 present/absent 语义。详见 DONE 文档 §三。

- `blocked`（世界书不可用 / 权限 / 内容门）—— **保持现状**：这是故障与合规，拦住 AI 是对的。
  这也正是 09-11 F-14 当初记录的场景（v2 包被拒），那条**没有问题**。
- `not_found` —— **应放行 AI**。⚠️ **09-18 更正**：原写"改为注入一条硬约束"、并且"填在 `WorldKnowledgeDecisionPolicy` 里"，
  **这一半被推翻了**。甲方 09-17 晚提的是「未知态得**回到对话层**、用一个内置提示词之类的来表述」⇒ 正确的切法是：
  **知识层照旧给空**（`BuildPromptBlock` 在 `not_found` 时返回空串），**说明句补在提示词模板里**。
  这样做还有个附带好处：`dialogue_context.knowledge` 才能如实记 `absent`；若在知识层塞文案，它会一律变成 `present`，观测点当场作废。
  模板那句**还没补**（甲方指定与"内置提示词怎么完善"一起规划），见 DONE 文档 §七。
  🚩 **09-18 实测把这个切法也否了 —— 但"回到对话层"这个大方向仍然对，换的是落点。**
  六种写法（空串／告知句／禁令句／放开字数下限／结构化状态行／状态行＋短指令）**27 次采样 27 次编造**，
  一种都没拦住；阳性对照 6/6 精确照说 ⇒ 不是探针太钝。⇒ **"在【检索到的知识】那一格上做文章"这条路已关掉。**
  另有一条结构限制：`RenderTemplate`（`NpcDialoguePromptPipeline.cs:122-132`）**没有条件分支** ⇒
  写进模板的句子在 `known` 轮次也会出现，那时格子有正文、旁边一句"这格空着"就自相矛盾 ⇒
  **"只在空着时说"在模板层做不到。**
  病灶在**产出侧**（模板逼它给"具体、有画面感、80-180 字"的答案，"我不知道"八个字无处安放）。
  详见 `docs/PROBE-20260918-空知识格实测-未知态该在哪一层表达.md`。

**优先级建议**：排在「修数据（`doc.` 关键词污染）」之后、「上检索算法（向量/编辑距离）」之前。
理由：修数据能把漏报从 62% 压下来，但**压不到 0**；只要还留着"没命中就不说话"，残余漏报每一条都仍是零信息输出。**先拆绑，再提准。**

### ★ 开工的连带面：**产品那两处改了还不够，还有三处读数会跟着失真**

本节写于 09-16，那时**还没有判据 E**（09-17 才加）⇒ 本节没覆盖"拆开之后，谁读的那根绳子会失效"。
**这是开工前必须先处理的一段，否则拆完会留下一批假读数。**

| # | 谁在读 `AllowsAi` | 现在拿它当什么 | 拆开之后 |
|---|---|---|---|
| 1 | `tools/worldbook-rag-merge/Program.cs:603`（`bool answered = decision.AllowsAi;`） | **判据 E 的观测点** —— "这次结果会不会被送去调模型" | ❌ **失真**：拆开后 `AllowsAi==false` 只等于"没知识"，不再等于"不调 AI" ⇒ **判据 E 变成一条恒绿（或恒红）的假闸** |
| 2 | `tools/worldbook-pilot-package/Program.cs:117` 正路径 `Require(decision.AllowsAi, …)` | "命中条目就该允许 AI" | ⚠️ 拆开后 `not_found` 也允许 AI ⇒ 这条**不再能区分正负路径** |
| 3 | 同上 `:139-140` 负路径 `Require(!negativeDecision.AllowsAi, …)` | "无命中不许允许 AI" | ❌ **与本决定直接相反**，拆开后必然红 |
| 4 | `src/NpcDialogueService.cs:1116` 日志字段 `ai=` | 记的是 `AllowsAi` | ⚠️ 一个字段混了"知不知道"和"让不让说" ⇒ **下一轮红测会再被它迷糊**；应拆成 `has_knowledge=` 与 `may_speak=` |

**⇒ 判据 E 的新观测点要换成"送进模型的东西里有没有世界书事实"，而不是"调没调 AI"**：
- `expect=empty` 的题 → 要求 **prompt 里没有知识块**（而不是要求不调 AI）
- `expect=concept` 的题 → 要求有知识块、且概念条排第一

三个常量（`NoAnswerMaxOffenders=0`／`ConceptMaxOffenders=0`／`KnownGapMax=3`）的含义随之调整，
**数值只许降，不许因为改判据而放松**。三根门禁同批复跑，数字变动逐条解释。

⚠️ **开工前最该想清楚的一条（本节的方案里已经埋了落点，但没点明风险）**：
放行 `not_found` 调 AI 之后，**NPC 会不会开始编？**
上面提的"未知约束"（填进 `WorldKnowledgeModels.cs:230-236`）必须写死"**你不清楚这件事、禁止编造世界书里的事实**；
可以说你不知道、可以转述听来的说法、可以反问"。
**这一条不写，拆开就是把"哑"换成"胡编"——比现状更糟。**

---

## 八、取证命令（可复现）

```bash
# 判据本体
grep -n "AllowsAi" -A 8 AWAKE/src/WorldKnowledgeModels.cs
grep -n "ShouldCallAi" AWAKE/src/WorldKnowledgeModels.cs AWAKE/src/NpcDialogueService.cs
grep -n "knowledgeDecision.AllowsAi" -A 3 AWAKE/src/NpcDialogueService.cs
# 默认值 = not_found
grep -n "State { get; set; }" AWAKE/src/WorldKnowledgeModels.cs
# 一手实测（不调 AI 的那条）
python -c "import json;d=json.load(open('AWAKE/docs/evidence/ai-chain-sim-fix-20260914/result.json'));[print(c['case'],c['gateState'],c['aiCalled'],c['reply']) for c in d['cases']]"
# 判据被固化成期望值
grep -rn "not_found" AWAKE/docs/PLAN-AWAKE-DIALOGUE-CHAIN-010-20260911.md
```
