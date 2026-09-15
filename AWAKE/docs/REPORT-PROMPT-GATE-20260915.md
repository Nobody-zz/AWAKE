# 内置提示词的离线判据补齐（2026-09-15）

> 模组主体线。上游问题是「内置提示词支持你测试了吗」——当时的回答是：**只测到了外壳，内容一层零覆盖**。
> 本文记录补了哪四条、各自钉住什么、以及顺带查出的一个真分叉。
> 提交 `8d52fda`（判据四件 ＋ 旧渲染入口同源化），2 文件 +222/−6。

## 一、补之前的覆盖（实测，非推断）

读法：`AWAKE.Tests` 全域 grep `Prompt` / `BuildBounded`。

**已覆盖（外壳层）**

- 注册失败可重试、失败态不可用：case `prompt-registration-coordinator`（`Program.cs:82` → `PromptRegistrationCoordinator.EnsureAsync`）。
- 注册 → 编译 → 变量替换 → 未知 revision 失败关闭：`DialogueChainRedtest.cs:119-155`（`RunPromptChecks`）。断言只有"编译文本含 `村民`""不残留 `{{player_turn}}`""未知修订必须失败"。
- 模板必须含关系指令：`Program.cs:2802`（`TemplateText` 含 `awake.relationship.delta.v1`）。
- 场景喊话模板**不得**含关系指令：`Program.cs:3376`。
- `NpcMemorySummaryTemplate` 的 PromptId：`Program.cs:2066`。
- 旧入口 `BuildDirectInput` 的 heroId 不被双引号包裹：`Program.cs:358`。

**零覆盖（而真入口走这里）**

- `NpcDialogueService.cs:1190 / 1194 / 1229` 依次调 `RecordContextDiagnostics`、`BuildBounded`、`EnsureBudget`。
  这就是"NPC 这一轮到底看到什么"的全部组装逻辑。
  **阳性对照**：`AWAKE.Tests` 全域 grep `BuildBounded` **0 命中**，而 `src` 侧有 6 处调用 ⇒ 确认是零覆盖，不是搜索问题。
- 两个诊断入口（`BuildContextDiagnosticRows` / `RecordContextDiagnostics`）只在
  `AWAKE/tools/worldbook-runtime-production-smoke/ProductionSmokeTestsDialogue.cs:74 / 208 / 223` 被调，
  **那是另一个工具、不在主验台**，其 `Logs/AwakeProbe.log` grep `RESULT|FAIL_CASE` 0 命中 ⇒ 不算覆盖。

## 二、新增四条（加在末尾，保持前 58 条序号不变）

### 1. `prompt-budget` — 预算不变量

钉一条**任何情况下都必须成立**的：成品字节数 ≤ 预算。宁可最后整段硬截，也不许把超长提示词发给模型。

覆盖的边界：
- 预算充足 ⇒ 原样通过、不做任何截断，且 `IsDirectOnly == false`；
- 超预算（长变量 ＋ 真模板 ＋ 2048 字节）⇒ 收缩后必须落进预算内；
- **模板自身就超预算、又没有可砍的变量** ⇒ 必须走 `direct-only` 兜底，同样不得超；
- 预算 0 ⇒ 必须得空串；
- `EnsureBudget(null, 16)` ⇒ 空串而不是 null；
- 截断按**文本元素**切 ⇒ 中英混排下不得出现 `U+FFFD`（真机语料全是中文，这是最容易踩的一条）。

### 2. `prompt-render-boundary` — 渲染边界

渲染是**单趟**替换、值按 **JSON 字符串字面量**注入 —— 这两条是"玩家输入无法改写提示词结构"的全部依据。

- 未提供的占位符**原样保留**（既不清空、也不抛错）；模板为 null ⇒ 空串；
- 值里的引号与换行必须被转义，注入后不得出现裸换行；
- **值里再写占位符不得被二次替换**（防注入，见第三节）；
- 同一占位符出现多次必须都替换（不是只换第一个）；
- 只认 `{{[A-Za-z0-9_]+}}`：带空格、连字符、空名字的都不是占位符。

### 3. `prompt-template-contract` — 模板自洽

模板正文与 `RequiredVariables`（注入方照它填变量）必须**双向一致**：

- 声明了却没在正文里用 ⇒ 白填；
- 正文用了却没声明 ⇒ 提示词里留一串 `{{xxx}}` **直接发给模型**，这是真机质量事故。

另补契约层隔离：`SceneShoutPromptTemplate.OutputSchemaJson` 的 `properties` 里**不得出现 command**
（正文断言之外的第二道）；`NpcPromptTemplate.OutputSchemaJson` 必须 `required` 里含 `reply`、且暴露可选 `command`。

### 4. `prompt-render-source-parity` — 两条渲染路径必须同源

唯一实现是 `NpcDialoguePromptPipeline.RenderTemplate`（单趟正则，`AwakePromptRegistry.cs:82` 也走它）。
判据要求 `NpcPromptTemplate.BuildDirectInput` 与它**逐字符一致**，且渲染必须是单趟。见下节 —— 这条正是补出来的。

## 三、顺带查出的真分叉（已修）

`NpcPromptTemplate.BuildDirectInput`（`src/Prompts/NpcPromptTemplate.cs:129`）原本是**一份平行的渲染实现**：
在 `StringBuilder` 上对每个变量做 `Replace` 循环。与唯一实现分叉。

实测（判据的 PROBE 行，变量 `player_turn = "{{npc_id}}"`、`npc_id = "hero:someone-else"`，渲染真模板）：

| 路径 | `hero:someone-else` 命中次数 | 含义 |
|---|---|---|
| 唯一实现 `RenderTemplate` | **1** | 值里的 `{{npc_id}}` 原样保留 ✓ |
| 旧入口 `BuildDirectInput` | **2** | 值里的 `{{npc_id}}` **被二次替换** ✗ |

⇒ 玩家在对话里发一句 `{{npc_id}}`，若这条路径被用上，就能把**自己的发言**改写成别人的身份写进提示词。属注入面。

处置：该入口**零生产调用**（全仓 grep `BuildDirectInput` 只有 `AWAKE.Tests/Program.cs:358` 在用），
当天改为**只转发**唯一实现；判据随之从"记录"升为"门禁"（两条路径必须逐字符一致）。

## 四、读数

- 补前：`RESULT total=58 passed=52 failed=6`
- 补后：`RESULT total=62 passed=56 failed=6`
- `FAILED_CASES` 两版**完全一致**（既有 6 条，不属本线）：`g3-s0-focused-readiness`、`persona-template`、
  `shared-persona-golden-fixture`、`persona-persistence`、`persona-anchor`、`dialogue-chain-redtest`。
- 新增四条全绿；`PROBE render_parity pipeline_hits=1 legacy_hits=1 identical=True`。

## 五、变异检验（三处，全部回红后回滚、零残留）

| 变异 | 应红的判据 | 实测 |
|---|---|---|
| `TruncateUtf8` 改成按字符截断 | `prompt-budget` | 红：`a bounded prompt exceeded its byte budget: 6012` |
| `RenderTemplate` 改成 `Replace` 循环 | `prompt-render-boundary`、`prompt-render-source-parity` | 红：`... got substituted again (prompt injection)`；`hits=2` |
| `RequiredVariables` 删掉一项 | `prompt-template-contract` | 红：`npc template uses an undeclared placeholder: npc_commitments` |

变异期 `RESULT total=62 passed=52 failed=10`（6 基线 ＋ 4 变异）；回滚后 `grep -rn MUTATION AWAKE/src/` 零命中，
复跑回到 `passed=56 failed=6`。

## 六、未做 / 请办

1. **两个诊断入口仍无判据**（`BuildContextDiagnosticRows` / `RecordContextDiagnostics`）：本轮判断成本高于收益，未做。
2. **收缩顺序不含 `player_known`**（`TruncationOrder` 六项里有 `player_turn`/`npc_memory`/`retrieved_knowledge` 等，唯独没有玩家情报）⇒
   玩家情报永远不会被收缩，只会被最后的整段硬截连坐。**记录，未改**；若这是有意为之，请在源码补一句注释。
3. **`NpcMemorySummaryPrompt` 的正文分节仍未测**：本轮只覆盖了 NPC 对话与场景喊话两套模板。
   记忆摘要那条已有 PromptId 校验（`Program.cs:2066`），但没有"模板 ↔ 变量表"一致性判据。
