# 派工单回执 · Overlay 逐条导入的「半提交」（世界书线 · 2026-09-22）

> **一句话结论**：**半提交是真的** —— 判据已写出并红了；已按「逐条尽力 ＋ 失败可观察」改掉，判据转绿；**三条变异**各自都能把它打回红。
>
> 派工单：`AWAKE/docs/HANDOFF-STORAGE-20260922-WORLDBOOK.md`。审计出处：`AUDIT-SYSTEM-CODEX-20260922-R2.md` P1-04 行。

---

## 〇 回执摘要（三样齐）

| 项 | 读数 | 出处 |
|---|---|---|
| **判据先红** | `[65/66] FAIL worldbook-overlay-import-partial` → `FAIL_MSG 一条操作失败不许中断整批：第 3 条没有落地。summary=[旧摘要。]` | `_redtest_overlay_20260922.txt` |
| **修后绿** | `RESULT total=66 passed=66 failed=0`（最终版连续 **3** 次） | `_green3.txt`、`_overlay_mutation_result_20260922.txt` |
| **变异检验** | 三条全红，且红因各自落在预期断言上（见 §五） | `AWAKE/tools/_overlay_mutation_check_20260922.py` |

**半提交本体的取证**：红的那一次运行里，「第 1 条已经生效」那条断言（`Program.cs:1811`）是**过**的
—— 也就是说 `TryImportOverlay` **报失败，而 live 状态已经被改了一半**。这就是缺陷本身，不是我推断出来的。

---

## 一 判据（写在哪、怎么造）

- 用例 `worldbook-overlay-import-partial`，注册 `AWAKE.Tests/Program.cs:136`，方法 `:1747`。
- 夹具备料：手工 `runtime.json`（1 档 1 表达）→ `WorldKnowledgeLoader.LoadVerified` → 真 `WorldKnowledgeQueryService`。
  **零替身**，跑的是真导入路径。
- 导入件 3 条操作，`baseRevision` 顺序 0/1/2（与真导出件同形）：

  | # | 操作 | 期望 |
  |---|---|---|
  | 1 | `replace_title` → 存在的档 | 落地 |
  | 2 | `replace_text` → **不存在的表达** | 失败（`WB2-REFERENCE-MISSING`） |
  | 3 | `replace_summary` → 存在的档 | **必须也落地**（逐条尽力） |

- 七条断言：夹具前提（`:1811`）｜失败且给因（`:1815`）｜**第 3 条不许被连锁带倒**（`:1822`）｜
  结构化失败清单（`:1832`）｜warnings 留痕（`:1840`）｜**门牌日志**（`:1848`）｜
  错码不误导（`:1865`）｜**阳性对照：干净导出件往返逐字一致**（`:1889`/`:1893`/`:1895`）。

---

## 二 【必须回答】第 1 步实际撞到的第一堵墙

**第一堵墙：派工单给的材料够不到这个缺陷。**

`WorldStateStore.InjectStoreForTesting`（`WorldStateStore.cs:319`）＋ `FakeKeyValueStore` 是**存储层**的夹具，
而这条缺陷**完全不在存储层**：在现行链路上，
`AWAKE/src/WorldKnowledgeQueryService.cs` 对 `IKeyValueStore`／`WorldStateStore`／`IMarcusAiFrameworkHost`
**零引用**（`grep -n "IKeyValueStore\|WorldStateStore\|IMarcusAiFrameworkHost\|store"` = 0 命中）。
`TryImportOverlay` 全程只拿一把 `_gate`、整批在同一把锁里改内存快照 ——
**没有任何一次 store 写入参与**，所以 `FailSet`／`FailSetAfter` 这两个开关一条都拧不到它。
（`FakeKeyValueStore` 在这条路径上**没有用武之地**，我一个字没改它。）

**第二堵墙：唯一的生产调用方在离线验台里够不到。**

`AwakeTerminalBehavior.SyncData`（`AwakeTerminalBehavior.cs:66-75`）是 `CampaignBehaviorBase`，
**不在 `AWAKE.Tests.csproj` 的显式清单里**（`grep -rn "AwakeTerminalBehavior" AWAKE.Tests/` 只有把它当**文本**读的断言）。
⇒ 我不能从"存档往返"那一头造夹具，只能**直接对服务**造（本用例的做法）。
顺带说明为什么这是可接受的：`WorldbookRuntime.SetKnowledgeForTesting`（`WorldbookRuntime.cs:21`）本来就是为这类注入留的口子，
只是它要静态状态，不如直接 new 一个服务干净。

**第三堵墙（最贵的一堵，代码里看不出来）：CAS 把「逐条尽力」堵死了。**

这是本任务的主要发现。`TryApplyOverlay` 的 CAS 是
`if (baseRevision != _overlay.Revision)`（`WorldKnowledgeQueryService.cs:171` 附近，改前）
—— 拿每条操作声明的 `baseRevision` 跟 **live revision 逐条比**。
而**失败的那条不推进 revision** ⇒ 中间有一条没落地，**后面每一条都会撞 CAS**。
⇒ **光把 `return false` 改成 `continue` 等于没改**：只是把「停在第 2 条」换成「第 2 条以后全报失败」。
真正的修法是让 CAS 归**批次边界**：单条编辑照旧比 live revision，整批导入比**导入游标**。
这条不写出来，下一个人还会以为"加个 continue 就完了"。

---

## 三 改了哪里（file:line）

只改两个文件：`AWAKE/src/WorldKnowledgeQueryService.cs`、`AWAKE.Tests/Program.cs`。

| 位置 | 改什么 | 为什么 |
|---|---|---|
| `WorldKnowledgeQueryService.cs:31` | 新增 `_overlayImportFailures` | 半成功必须留痕 |
| `:61` / `:64` | `Warnings` 并入失败清单；新增 `OverlayImportFailures` | 一个给人看、一个给调用方用 |
| `:158` | `BuildStatusText()` 增 `overlay_unapplied=N`（**为 0 时不出现**） | 落点在游戏内「重载世界书」的反馈串（`AwakeDeveloperTestActions.cs:153`） |
| `:180` / `:203` | `TryApplyOverlay` = CAS ＋ 新 `TryApplyOverlayCore`（**不含 CAS**） | 让 CAS 归批次边界 |
| `:274` | `TryImportOverlay` 重写：游标 ＋ 逐条尽力 ＋ 失败清单 ＋ 门牌日志 | 本任务正题 |
| `:289` | 导入游标（**成功失败都 +1**） | 否则尾段连锁全灭 |
| `:313` / `:317` | 收失败 ＋ 落 `worldbook_overlay_import_partial` 日志 | 带齐「第几条／改谁／为什么」 |

**口径**：`TryApplyOverlay`（**单条编辑入口** —— 今天只有开发者测试面板在用，见 §九）与 `TryImportOverlay`（整批导入）的**对外签名与返回语义都没变**
—— `error` 仍是"第一条失败的原因码"，调用方（`WorldbookRuntime.ImportOverlayJson`、`AwakeTerminalBehavior.SyncData`）一行没改。

**门牌日志的形制照抄同日姊妹件**：信件半提交 `AwakeLetterCommit.cs:127`（`letter_send_half_committed key=… letter=… idem=… reason=…`，
commit `637d648`，P1-05）。两条是同一族缺陷，留痕方式不另起一套。

---

## 四 顺带抓到两处（都在同一方法里，都已修）

### 1. 错码把「引用没了」说成「操作被禁」—— 第一次跑红就是这么暴露的

第一版夹具我用"档不存在"当非法那条，`error` 拿到的是 **`WB2-OVERLAY-FORBIDDEN`** 而不是 `WB2-REFERENCE-MISSING`。
真因：原实现把「kind 合法」和「档找得到」**并进同一个 `if`**，档找不到就顺势掉到最后的 `else`。
⇒ 玩家按 `FORBIDDEN` 查，只会以为"这种操作不允许"，查不到"这个档没了"。
已拆成两个判断（`:203` 起），并加断言 `:1865` 守它。

### 2. ★ 每读一次存档，玩家所有编辑的「为什么」都被改写成同一句 —— 阳性对照抓到的

这条**不在派工范围里**，是我加的阳性对照（"干净导出件往返必须逐字一致"）当场抓出来的：

```
原=[…"kind":"replace_title","baseRevision":0,"value":"往返标题","reason":{"zh-CN":"测试"}…]
后=[…"kind":"replace_title","baseRevision":0,"value":"往返标题","reason":{"zh-CN":"导入战役 Overlay"}…]
```

导入时**一律用常量 `"导入战役 Overlay"` 重建 `reason`**，把操作自带的那条缘由丢掉。
⇒ 玩家写下的"为什么改"只活到第一次存档；下一次读档，全部变成同一句。
`reason` 这个字段存在的全部意义就是记缘由 —— 丢掉它等于这个字段白设。
已修：导入时**原样带回**（`:302` 附近），`TryApplyOverlayCore` 的 `reason` 参数改成收 `JObject`。

---

## 五 变异检验（三条，**都对最终版跑的**）

脚本 `AWAKE/tools/_overlay_mutation_check_20260922.py`（逐条注释关键行 → 重建 → 只跑该用例 → **按字节还原**）。
明细 `AWAKE/tools/_overlay_mutation_result_20260922.txt`。

| 变异 | 注释掉什么 | 结果 | 红因（判据哪一条响的） |
|---|---|---|---|
| **A** | `cursor++`（`:289`） | **红** `65/66` | `导入件里有一条非法操作时，必须失败并给出原因。ok=False error=WB2-OVERLAY-CAS` |
| **B** | 门牌日志 `:317` | **红** `65/66` | `半提交必须落门牌日志（带 改谁/为什么）。logged=[]` |
| **C** | 逐条尽力（改回"首败即 return"） | **红** `65/66` | `一条操作失败不许中断整批：第 3 条没有落地。summary=[旧摘要。]` |

**源码按字节还原：OK**（脚本自带校验，失败即非零退出）。

三条各守一处：A 守游标（CAS 归批次边界）、B 守"看得见"、C 守"逐条尽力"。
⇒ 判据不是只跟着某一处实现走，三处各自都能把它打回红。

---

## 六 全库回归 ＋ 另外两条红的取证

- **最终版连续 3 次 `total=66 passed=66 failed=0`。**
- **生产工程 `AWAKE/AWAKE.csproj` Release 构建：0 警告 0 错误**（`src/` 是通配编译，这一份才是权威目标）。

**另外两条红不是我的，已取证**（`letter-half-commit`、`g3-s0-focused-readiness`）：

1. 把我这条用例**临时禁用**后跑 65 条 ⇒ **同样这两条红**（`_ab_baseline_20260922.txt`）。⇒ 与我的用例无因果。
2. 真因是**他线当时正在改这两个文件**：`AWAKE/src/AwakeLetterService.cs` 写于 `15:22`、
   `AWAKE/src/AwakeLetterCommit.cs` 写于 `15:27`，他线 `15:33` 提交 `637d648`（`fix(letter): P1-05 信件半提交`）。
   我在 `15:2x` 的构建**编到了他们半成品的那一版**。
3. 现在两条都已绿（HEAD 已一致）。

> ⚠️ **顺带记一条跨线观察（不是我的盘子，我没动）**：在那次提交落地**之前**，
> `AWAKE.Tests.csproj` 已经引用了 `..\AWAKE\src\AwakeLetterCommit.cs`，而该文件当时**还没被 git 跟踪**
> ⇒ 那段窗口里，干净克隆 HEAD 得到的是**编不过的测试工程**（`CS2001: 源文件找不到`）。
> 现在已一致（`git ls-files AWAKE/src/AwakeLetterCommit.cs` 有它，清单与索引零差）。
> 与既有那条"**清单先入库、源文件后入库 ＝ 中间窗口编不过**"是同一类。**请他们自查提交顺序。**

---

## 七 否定断言（一律限定口径）

- **在现行链路上**，`AWAKE/src/WorldKnowledgeQueryService.cs` 对存储**零引用**
  （`IKeyValueStore`／`WorldStateStore`／`IMarcusAiFrameworkHost` 三词在该文件 0 命中）。
  —— 不说"全仓没有"，只说这个文件、只说"在现行链路上"。
- **在现行链路上**，`letter-half-commit`／`g3-s0-focused-readiness` 两条的失败与本次改动无关（§六 A/B 取证）。
- 我**没有**改：`WorldStateStore.cs`、`AwakeTestFakes.cs`、`AwakeLetter*.cs`、`AWAKE.Tests.csproj`、
  任何 `docs/` 下的他线文档。也没 stash／回退任何在途改动。

---

## 八 未做 ／ 待定

- **未提交**（理由见下，等你点头）。
- 未跑真机（离线全绿不算过版）。
- 未深挖：`disable_expression` / `enable_expression` 走 `FindExpression` 是 **O(档数×表达数)** 线性扫
  （`:640` 附近）—— 导入时逐条调它，ops 一多就是 O(n·m)。**本次没量、没改**，只记一笔。

### 提交这件事需要你定

`AWAKE/src/WorldKnowledgeQueryService.cs` 里**同时躺着两件事**：
① 今天这份 overlay 修复；② **09-20 的互引边表接进召回**（+53 行，当时你说"没上线没提交，等你发话"）。
而 09-20 那件事**还牵动 `WorldKnowledgeLoader.cs` ＋ `WorldKnowledgeModels.cs`**（少了它们编不过）。

⇒ 三条路，请挑一条：

| | 做法 | 代价 |
|---|---|---|
| **甲** | 一起提三个 src 文件 ＋ 测试 ＋ 报告 | 一条提交含两件事；等于把 09-20 那件也提了 |
| **乙** | 用 `git add -p` 把 overlay 的 hunk 与边表的 hunk 拆成两笔 | 要人工分 hunk；且"HEAD ＋ 只有 overlay"这个组合我没测过（边表那半测过的是合体版） |
| **丙** | 先不提，只留工作区 | 与现状相同 |

我倾向 **甲**，理由：三个文件是**同一个编译单元**、分开提会留下编不过的中间态；
且 09-20 那件的验证（四对照）已经做完、结论在 `EDGE-RECALL-REPORT-20260920.md`。

**精确 pathspec（甲）**：

```
AWAKE/src/WorldKnowledgeQueryService.cs
AWAKE/src/WorldKnowledgeLoader.cs
AWAKE/src/WorldKnowledgeModels.cs
AWAKE.Tests/Program.cs
AWAKE/tools/_overlay_mutation_check_20260922.py
AWAKE/tools/_overlay_mutation_result_20260922.txt
AWAKE/docs/WORLDBOOK-OVERLAY-HALFCOMMIT-20260922.md
```

无宽目录、无 `git add -A`；`AWAKE.Tests/Program.cs` 已核过**只有我这两处 hunk（+163 行）**、不含他线内容。

---

## 九 口径更正：这条链上今天**没有玩家**（2026-09-24，甲方一句「词条除了你没人改」）

**我先前把这条链的写入者说成了「玩家」，是我编的。** 本报告 §三 口径行、§四.2、以及当日流水里凡出现「玩家」二字，**一律按下述读**。

**实情（在现行链路上）**：

| 环节 | 今天谁在写 | 证据 |
|---|---|---|
| **词条本体** | **我**（作者侧，写 `authoring/`） | `tools/worldbook-studio/` 里 `overlay` **零命中** —— 编辑器根本不走这条路 |
| **Overlay 里的"改动"** | **只有开发者测试面板** | `AwakeTerminalBehavior.cs:1227`/`:1234` 的 `worldbook_edit` / `worldbook_export`，都在 `HandleDeveloperTestSelection`（**调试多选面板**）里；落到 `AwakeDeveloperTestActions.cs:206` |

- `WorldbookRuntime.TryApplyOverlay` 的**唯一调用方**就是那个调试面板（全 `src/` grep 只此一处）。
- ⇒ **没有玩家入口。** 代码里那些 `"玩家编辑"` 字面是**预留的意图**，不是今天的实情。（我照抄了注释里的字面，没去数调用方 —— 又一次"取错"。）

**这条更正改什么、不改什么**：

- **不改**：判据、修法、三条变异。测的是机制，与"谁写"无关。
- **不改**：缺陷仍然成立、仍然要修。因为 `TryImportOverlay` 跑在**每次读档**上（`AwakeTerminalBehavior.cs:74`），
  只要存在 overlay 文件，读档就会重放它；静默丢改动**不因"写的人是我"而减轻** —— 恰恰相反：
  **写的人只有我，静默丢掉我更不会察觉。**
- **改（定性）**：这不是"玩家的编辑会丢"，而是「**这条路径今天只有开发面板一个写入方，却已经长齐了导入/导出/版本号/失败留痕一整套机制**」。
  ⇒ 待决的是**它的定位**：**给它一个正当写入方，还是明确它只是开发期通道**（若是后者，就不该挂在正式包的读档路径上）。**这一条不在派工单里，我没动，留甲方定。**

**顺带一条纪律（这已经是同一枚硬币的第三面）**：判断"谁在用"，**不能读注释里的自称，只能数调用方**。

### ✅ 09-24 21:15 甲方裁定：**「以后能改词条」** —— 留口子

⇒ **口径定死**：这条链是「**将来在游戏里改词条**」的预留机制，**不是待删的开发残留**。
- **机制全留**（导出／导入／版本号／失败留痕）**不动**；缺的只是**正经入口**（今天唯一写入方＝调试面板），待做、不在本任务内。
- ⇒ **本次修复由「顺手修」升格为前置条件**：将来接上入口、写入方变多，**静默丢几条没人会发现**。
- ⚠️ **口径同写**：`"玩家编辑"` 字面**是将来时、不是现状** —— 今天仍是 **0 个玩家入口**，别把预留读成已有。
