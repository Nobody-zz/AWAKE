# person­a DSL 预算审计

> 2026-09-13 · 只读审计 · 未改任何源码、未提交
> 起因：Max 问「dsl 的预算会不会给少了？」
> 结论：**给了，而且少了不止一半；但比"少"更严重的是"先砍谁"。**

---

## 一、先回答：真实预算是多少

追调用链（`NpcDialogueService` → `WorldbookRuntime` → `PersonaDslGenerator`）：

```
NpcDialogueService.cs:1144   WorldbookRuntime.BuildPersonaProjection(snapshot)      ← 不传预算
WorldbookRuntime.cs:30       PersonaRuntimeProvider.BuildProjection(provider, snap)
WorldbookRuntime.cs:289      provider.BuildProjection(snapshot, 4096)               ← 硬编码 4096
PersonaDslGenerator.cs:11    Generate(bundle, snapshot, maximumBytes)
PersonaDslGenerator.cs:14    maximumBytes <= 0 ? 4096 : maximumBytes                 ← 兜底也是 4096
```

**真实运行时给人格 DSL 的预算是 4096 字节。**

而本地模拟器 `PersonaDialogueSim.cs:39` 的默认值是 **8000**：

```csharp
int maximumBytes = args.Length > 4 && int.TryParse(args[4], out int mb) ? mb : 8000;
```

⇒ **本线此前所有基于模拟器的实测（第 1–4 跑擂台），预算都比真实链路宽一倍。** 口径须更正。

---

## 二、4096 到底够不够：实测 76 张卡

方法：用真实 `PersonaDslGenerator.Generate`（经模拟器 dll，`--force-approved` 只在内存放行），
对全部 76 张卡在三档预算下各生成一次，对比产物。

### 2.1 截断率曲线

| 预算 | 成功 | 被截断 | 强制段被砍(`mandatory_budget_exceeded`) |
|---|---|---|---|
| **4096（真实）** | 76/76 | **35（46.1%）** | **31** |
| 6144 | 76/76 | 2（2.6%） | 2 |
| 8192 | 76/76 | 1（1.3%） | 1 |
| 10240 | 76/76 | 0 | 0 |
| 16000 | 76/76 | 0 | 0 |

**跳变极陡：4096 → 6144（只加 2048 字节）就把截断从 46% 打到 2.6%。**

注意 **31 张报 `mandatory_budget_exceeded`**——这不是"优雅地丢掉可选段"，是**连强制内容都装不下，只能硬砍字符串**。

### 2.2 卡的自然长度

| 指标 | 值 |
|---|---|
| 最短 | 3522（拔该） |
| 中位数 | 4068 |
| 最长 | **8640（蒙楚格）** |
| > 4096 的卡 | **35/76（46.1%）** |

**中位数 4068 就压在 4096 线上**——这不是个别卡写太长，是**普遍位置就在边界**。

损失分布极不均匀（越下功夫写的卡被砍越狠）：

| 卡 | 自然 | 4096 实得 | 丢失 |
|---|---|---|---|
| 蒙楚格 | 8640 | 4094 | **52.6%** |
| 拉盖娅 | 6691 | 4096 | 38.8% |
| 那得娅 | 5813 | 4095 | 29.6% |
| 梅利迪尔 | 5405 | 4095 | 24.2% |

---

## 三、比"给少了"更要命的一条：先砍谁

截断实现是从**尾部砍**（`NpcDialoguePromptPipeline.TruncateUtf8`：从头累加，超限即 break）。

而 DSL 的段序是固定的（`PersonaDslGenerator.BuildCanonicalSections` + `BuildDynamicSections`）：

```
[PERSONA_LOAD]              ← 保住
[PERSONA_CONSTRAINTS]       ← 保住
[PERSONA_IDENTITY]          ← 保住
[PERSONALITY_CORE]          ← 保住
[PERSONALITY_PUBLIC]        ← 保住（但常被腰斩）
[PERSONALITY_PRIVATE]       ← 先死：私我、真实行为
[PERSONALITY_CONTRADICTION] ← 先死：矛盾、底线
[CURRENT_IDENTITY]          ← 先死：当下身份（王国/家族/职位）
[CURRENT_RELATION]          ← 先死：当前关系
[CURRENT_STATE]             ← 先死：当前状态
[MEMORY_HINT]               ← 先死：记忆
[SCENE_CONTEXT]             ← 先死：眼前场面
[PERSONALITY_EXPERIENCE]    ← 先死：经历
```

### 实测丢段频次（4096 档，相对不截断的 16000 档）

| 段 | 76 张卡中出现 | 4096 下丢失 | 丢失率 |
|---|---|---|---|
| PERSONA_LOAD / CONSTRAINTS / IDENTITY / CORE / PUBLIC | 76 | 0 | 0% |
| **PERSONALITY_PRIVATE**（私我） | 76 | **8** | 23% |
| **PERSONALITY_CONTRADICTION**（矛盾与底线） | 76 | **25** | 71% |
| **CURRENT_IDENTITY**（当下身份） | 76 | **35** | **100%** |

**被截断的 35 张卡，100% 丢掉了「当下身份」；71% 丢掉了「矛盾与底线」。**

蒙楚格实测（4096 vs 无截断）：

| 段 | 4096 | 无截断 | 结果 |
|---|---|---|---|
| PERSONA_LOAD | 143 | 143 | 保住 |
| PERSONA_CONSTRAINTS | 410 | 410 | 保住 |
| PERSONA_IDENTITY | 705 | 705 | 保住 |
| PERSONALITY_CORE | 1459 | 1459 | 保住 |
| PERSONALITY_PUBLIC | **1378** | 3432 | **腰斩 60%** |
| PERSONALITY_PRIVATE | **0** | 2031 | **整段丢失** |
| PERSONALITY_CONTRADICTION | **0** | 397 | **整段丢失** |
| CURRENT_IDENTITY | **0** | 52 | **整段丢失** |

### 这意味着什么

**先保住的是「对外表演层」（CORE + PUBLIC），先牺牲的是「内里」（PRIVATE + CONTRADICTION + 当下境况）。**

而"脸谱化"的定义恰恰就是：**只有表演层，没有内里。**

⇒ 这与本线的核心目标（反脸谱）**直接冲突**，且是**结构性的**，不是调个数字能完全解决的。

---

## 四、因果闭环：这解释了前几跑的现场观察

第 4 跑（即时应答）观察到"卡里的负向规矩会被忽略"——蒙楚格卡里明明写着不许爆粗，模型回了「放个屁！」。

现在能指出机制原因了。蒙楚格卡里那两条约束的落点：

| 卡里的原文 | 所在字段 | 进哪个段 | 4096 下的命运 |
|---|---|---|---|
| 「**他立威不靠骂街**，靠话少、决断、说到做到」 | `realSelfBehaviors` | `[PERSONALITY_PRIVATE]` | **整段被丢**（实测确认） |
| 「**他不拿粗话立威**；越怒，话越短、语气越冷」 | `selfClaimRules` | `[PERSONALITY_PUBLIC]` | ~~大概率被砍~~ → **实测：活下来了**（见下方更正） |

**不是演员不听话，是这两条实机根本没送到模型面前。**

> **⚠️ 更正（2026-09-13 晚，第 5 跑实测）**
>
> 上表第二行原写「大概率被砍」，是**推断**，现予**作废**。实测：该句在 4096 档产物中
> **完整保留**（第 40 行，恰在截断线前一行；真正的截断点在其下一句"会盟散后…"处，断在半句）。
>
> 连带作废的还有第 4 跑那条观察「卡里的负向规矩会被忽略 → 模型回『放个屁！』」：
> 在**真实模板**下，两个家族、两档预算、共 59 段回复中**粗话命中全部为 0**。
> 那次爆粗用的是**自建提示词**，真实链路不吃那套 ⇒ 该观察从"卡的证据"降级为**实验装置的 artifact**。
>
> 出处：`docs/evidence/persona-stage-20260913/STAGE-REPORT-5.md` 第四节。


同理，`CURRENT_IDENTITY` 100% 丢失 ⇒ **角色不知道自己此刻是谁、效忠哪个王国、什么职位**——
这解释了为什么角色说话像在念人设标语，而不是在接眼前的场面。

---

## 五、那能不能提？算总账

persona DSL 只是总 prompt 的一个变量，总预算是 `NpcDialogueConstants.MaxPromptUtf8Bytes = 32768`。

### 5.1 关键：persona_dsl 不在削减名单里

`NpcDialoguePromptPipeline.TruncationOrder`（总量超出时的递减顺序）：

```csharp
{ "player_turn", "dialogue_history", "npc_memory", "retrieved_knowledge", "opening_hint", "npc_state" }
```

**`persona_dsl` 不在其中 ⇒ 它在总预算层不可动。**

- 好消息：**给它加预算不会被总预算逻辑吃掉**，4096 卡的是它自己。
- 坏消息：它挤别人（见 5.3）。

### 5.2 其它块的配额（源码）

| 块 | 配额 | 出处 |
|---|---|---|
| persona_dsl | 4096 | `WorldbookRuntime.cs:289` |
| retrieved_knowledge | **4096** | `KnowledgeConstants.MaximumRetrievedBlockBytes` |
| npc_memory | 2000 | `NpcMemoryOverviewBuilder.DefaultMaximumBytes` |
| dialogue_history | 12 条 × 每条 ≤400 元素 | `HistoryCapacity` / `SerializeHistory` |
| player_turn | 4000 元素 | `MaxPlayerInputLength` |
| **总量** | **32768** | `MaxPromptUtf8Bytes` |

### 5.3 实算（真实模板逐字渲染）

| 场景 | 总 prompt | 余量 |
|---|---|---|
| 典型对话（各块常见值） | **14971** | **+17797** |
| 各块顶格 | 35422 | −2654（超） |

**典型情况下，32768 的池子只用掉 46%，躺着 17797 字节没人用；而 persona 却被硬扣在 4096。**
⇒ 问题不是"总量不够"，是**分配错了**。

反推 persona 提到多少仍安全：

| persona 预算 | 总 prompt | 余量 |
|---|---|---|
| 4096 | 14866 | +17902 |
| 8192 | 18961 | +13807 |
| 10240 | 21010 | +11758 |
| 12288 | 23059 | +9709 |
| 16384 | 27154 | +5614 |

**提到 12KB 总量才用 70%，安全。**

### 5.4 但有个边界风险

真正能把总量撑爆的不是 persona，是 **`dialogue_history`**：12 条 × 每条 400 元素，顶格可达 **14496 字节**。

若历史很长 + persona 提到 12288 ⇒ 可逼近 32768；一旦超线，`TruncationOrder` **第一个砍 `player_turn`（玩家原话）** ⇒ NPC 答非所问。

⇒ **调 persona 预算时，应同时复核 dialogue_history 的顶格容量。**

---

## 六、建议（三项，按收益排序）

### ① 提预算到 8192，推荐 10240

| 值 | 依据 | 效果 |
|---|---|---|
| 6144 | 4096→6144 跳变 | 截断 46%→2.6% |
| 8192 | 留动态层余量 | 仅剩最长的蒙楚格 |
| **10240** | 实测零截断线 | 76/76 全不截 |

代价：总 prompt 从 14866 → 21010，仍余 11758。**极低。**

### ② 调段序：把「私我 / 矛盾」提前，把「公开面（尤其范例句）」放后

既然截断从尾砍，就该让**最不能丢的先保住**。现序恰好相反。
建议至少把 `[PERSONALITY_CONTRADICTION]`（含底线）提到 `[PERSONALITY_PUBLIC]` 之前。

### ③ 处理 `selfClaimExamples`：它是最大的字节消耗者，又是复读源

蒙楚格 `[PERSONALITY_PUBLIC]` 3432 字节里，绝大部分是 5 条 `selfClaimExamples`（整段范例台词）。

而第 1/2 跑已实测：**卡里"能被整句背出来"的成分会变成演员的复读点**（范例句是最典型的一类）。

⇒ 这 5 条范例句**既把后面的私我/矛盾挤掉，又给演员提供了背诵材料**。是**唯一一个"又占地方又有害"的成分**，应优先压缩或改形态。

---

## 七、本审计的边界（须注明）

1. 本审计只读。**未改任何源码**；4096 仍在代码里。
2. 段丢失统计基于**模拟器构造的最小上下文**（只给 CharacterId / HeroName / Role）。
   真实运行时还会填 `memoryHint` / `sceneKeywords` / `experiences`，动态层占用更大 ⇒ **真实截断只会比本报告更严重，不会更轻。**
3. 动态段（CURRENT_* / MEMORY_HINT / SCENE / EXPERIENCE）在模拟器下内容少，其"丢失"在真实下同样是整段消失，但绝对字节更多。
4. 总账为**字节推算**（用真实模板逐字渲染 + 按配额构造各块），非实机抓取。
   实机日志无 prompt 字节记录（已查 `Awake.log`），故无法直接对照。

---

## 八、复现

```bash
# 单卡三档对比（真实 dll，非 dotnet run）
cd AWAKE/tools/worldbook-runtime-sim
dotnet bin/Release/net10.0/WorldbookRuntimeSim.dll persona \
  "../../ModuleData/Worldbook/persona_definitions/definitions" \
  "../../ModuleData/Worldbook/persona_definitions/tag_registry.json" \
  "蒙楚格_monchug_urkhunait_khuzait" /tmp/m4.txt 4096 --force-approved

# 全量审计（76 卡 × N 档）
python docs/evidence/persona-stage-20260913/budget_audit.py 4096,6144,8192,10240,16000

# 总账
python docs/evidence/persona-stage-20260913/prompt_budget_account.py
```

产物：
- `docs/evidence/persona-stage-20260913/budget-audit-summary.md`（分档汇总）
- `docs/evidence/persona-stage-20260913/budget-dsl/`（每卡每档的 DSL 原文 + JSON）
- `docs/evidence/persona-stage-20260913/prompt-budget-account.md`（总账）
