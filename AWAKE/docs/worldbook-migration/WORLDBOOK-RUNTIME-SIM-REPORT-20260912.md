# 本机模拟 AWAKE 世界书运行时 — 实测报告（2026-09-12）

> 回答 Max 的问题："你能在本机模拟 AWAKE 模组的 AI 模块运作吗？"
> 结论：**能。而且不是"理论上能"——已经跑通了，用的是真实运行时代码 + 真实试点包。**
> 全部过程：不启动游戏、不联网、不写回仓库（包复制到临时目录再加载）。

---

## 一、能模拟什么（三层，前两层已实测，第三层具备条件）

| 层 | 是什么 | 本机能不能跑 | 证据 |
|---|---|---|---|
| **A 知识检索与门控** | 提问 → 命中词条 → 按身份/详细度给不给、给哪一段 | ✅ **已跑通** | 见下节实测输出 |
| **B 提示词组装** | 把检索结果 + 人设 + 上下文拼成给模型的提示词 | 🟡 可读代码，未跑 | `NpcPromptTemplate.cs` / `AwakePromptRegistry.cs` / `PersonaDslGenerator.cs` |
| **C 模型生成回答** | 真正让模型开口说话 | ✅ **具备条件，未做** | 本机 Ollama 0.23.1 已运行（11434），4 个本地模型可用；AWAKE 的 provider 里就有「Ollama」这一档，且明确"本机 Ollama 一般不需要 API Key" |

### 已有的现成工具（不是我新造的）

`AWAKE/tools/worldbook-runtime-smoke/` —— 项目里早就有的离线冒烟程序。它把真实运行时源码（`WorldKnowledgeLoader` / `WorldKnowledgeQueryService` / `WorldbookIdentityEvaluator` / `WorldbookPackageIntegrity` 等）和一份替身文件（`SmokeStubs.cs`，把游戏依赖 `AwakeRuntime` / `WorldStateStore` / `AwakeLog` 换成假的）编在一起，**不需要游戏就能跑**。

实测：

```
$ cd AWAKE/tools/worldbook-runtime-smoke/bin/Debug/net10.0
$ ./WorldbookRuntimeSmoke.exe
PASS: runtime loader/query/overlay/identity/registry/weekly-report/event-ledger smoke
EXIT=0
```

本机 `dotnet 10.0.301` 可用，重新编译也没问题。

---

## 二、实测：用真实试点包跑门控

我在系统临时目录另建了一个只读模拟程序（`C:\Users\26811\AppData\Local\Temp\awake-sim`，不落在仓库里），把 `AWAKE/release/awake-worldbook-pilot/` 复制到临时目录后用真实代码加载。

**试点包**：`awake:pilot.worldbook` rev1，3 个词条 / 6 个身份。

### 输出（节选）

```
--- 提问：收成 ---
  profile.commoner   state=partial   今年的收成要看天时。粮税按领主定下的份额收…
  profile.soldier    state=not_found
  profile.noble      state=not_found
  profile.merchant   state=partial   今年的收成要看天时。…（同平民，一字不差）
  profile.headman    state=partial   同上
  profile.notable    state=partial   同上
  profile.anonymous  state=not_found

--- 提问：领主 ---
  profile.soldier    state=partial   领主的本分是守住封地、按时纳贡、战时带人出征…
  profile.noble      state=partial   同上
  其余全部          state=not_found
```

**身份分流是对的**：士兵问"收成"答不上来，平民答得上来；"领主的本分"只有士兵和贵族知道。
**但四个身份拿到的文本一字不差** —— 因为每个词条只有 1 条表达。所谓"不同人说法不同"在这个包里**根本不存在**。

---

## 三、两个实测发现，直接改我前两天的提议

### 发现 1：`scope` 不是"地理传播范围"，是"接触门槛"

代码（`WorldbookIdentityEvaluator.cs:104`）：

```csharp
return ScopeRank(evaluation.EffectiveScope) >= ScopeRank(rule.Scope)
    && DetailRank(evaluation.EffectiveDetail) >= DetailRank(rule.MinDetail);
```

等级：`local=1 < regional=2 < national=3 < faction=4 < elite=5 < private=6`。

意思是**问话人的圈层必须 ≥ 这条知识要求的圈层**。`local` 是**最低门槛，谁都够得着**；`private` 才是最高。

实测验证——同一身份问同一句，改 scope：

```
scope=local     state=known
scope=regional  state=known
scope=national  state=known
scope=faction   state=known
scope=elite     state=known
scope=private   state=known     ← 全给，毫无差别
```

**`local` 的知识对所有人可见，改 scope 完全不影响结果。**

→ **我前两天提的"梯度规则（流传越广知道越浅，近 detail 远 rumor）"用 scope 是实现不了的。** 我的理解错了：我把 scope 当成了地理距离，它其实是保密层级。这条提议要重写——真要表达"离得远的只知道传闻"，得用 `conditions.settlement_ids`（地理条件）或 detail 维度，不能用 scope。

### 发现 2：请求详细度是"上限"，请求 `rumor` 会被拒

代码（`WorldKnowledgeQueryService.cs:252`）：

```csharp
if (expressionDetail < 0 || requestedDetail < 0 || expressionDetail > requestedDetail)
    → PermissionLimited
```

等级：`rumor=1 < summary=2 < detail=3 < secret=4`。

实测：

```
requested=rumor    state=blocked    ← 要"传闻级"，反而被拒
requested=summary  state=known
requested=detail   state=partial
requested=secret   state=partial
```

因为词条的表达是 `summary(2)`，请求 `rumor(1)` 时 `2 > 1` → 超限 → 拒绝。
**请求详细度 = 允许给到的最细程度，不是"我想要多细"。** 这是个容易踩反的坑。

### 附带确认（与之前判断一致）

- **referrals 全 0** —— 六个身份 × 三个问题，转介一次都没触发。C13 未接线，实测坐实。
- **12 档从未编译** —— 这次能跑的是 `release/awake-worldbook-pilot`（3 词条试点包），不是那 12 档。

---

## 四、不能模拟什么（边界）

| 不能 | 原因 |
|---|---|
| 启动游戏 / 真实战役对象 | 硬边界；也是用替身的意义 |
| 接真实云端模型 / 用真实 API Key | 硬边界。但**本机 Ollama 不受此限**，可以走 |
| 真跑一次"玩家说话 → NPC 回答"全链路 | **没做过**。提示词组装层（B）我读过代码，但没跑；C 层本地模型具备条件但没接 |
| 验证那 12 档内容 | 它们没编译成运行时包，加载不进去。**要先过编译** |

---

## 五、C 层已打通：接本机 Ollama 跑完整链路（14:40 追加）

Max 确认："我之前的确是这么在 codex 配置来测试的，你也可以这样做。"

先查旧记录，避免重复踩坑（`AWAKE/docs/AWAKE-CURRENT.md`）：

| 模型 | 前人结论 |
|---|---|
| `qwen2.5:latest` | ✅ Local Worker 验收**通过**（生成候选、证据、camelCase 映射、needs-review 文档创建/读回全过） |
| `gpt-oss:20b` | ❌ r48 实测**超时、无可用输出**，已记为真实负面结果 |

**所以选 `qwen2.5:latest`。** 链路：真实检索 → 真实提示词模板（逐字取自 `NpcPromptTemplate.cs`，源码现读）→ 本机 Ollama。

### 实测结果（4 个用例）

| 用例 | 门控 | 模型回答 |
|---|---|---|
| 平民 / 收成 | known（96 字） | "今年雨水少，麦子长得稀。粮税照旧，可别指望宽裕些。" 合理用了知识 |
| 贵族 / 领主 | partial（49 字） | "领主是个硬气的人，守规矩也讲情面。" 合理 |
| **士兵 / 收成** | **not_found（0 字）** | **"今年雨水足，收成还好。不过税官总是狮子大开口。" ← 编的** |
| **贵族 / 收成** | **not_found（0 字）** | **"今年雨水少，谷粒稀。你若来得早，或许还能分些余粮。" ← 编的** |

### ⭐ 新发现：门控挡得住知识，挡不住模型胡编

门控 `not_found`、一条知识都没给，模型照样言之凿凿地编出收成和税官。

**原因**：真实提示词模板只对"跨会话记忆为空"写了"不要编造共同经历"，对 **`retrieved_knowledge` 为空没有任何约束**。

→ 这是模板的真实缺口，建议在 `NpcPromptTemplate.cs` 补一条：检索知识为空时必须承认不知道，不许编。
→ 也说明：**只做内容分层不够，提示词层得配合**，否则知识壁垒在输出端失效。

单次调用耗时约 12 秒（qwen2.5:latest）。

---

## 六、工具已固化为可复用程序

路径：**`AWAKE/tools/worldbook-runtime-sim/`**（README.md 内含完整用法与实测发现）
从临时目录搬到仓库工具位，方便交给其他 agent 复用。

---

## 七、重跑方法

```bash
# A 层：现成冒烟（已构建）
cd AWAKE/tools/worldbook-runtime-smoke/bin/Debug/net10.0 && ./WorldbookRuntimeSmoke.exe

# A+B 层：门控矩阵（真实试点包）
cd AWAKE/tools/worldbook-runtime-sim && dotnet run -c Release

# C 层：接本机 Ollama 看 NPC 真开口
cd AWAKE/tools/worldbook-runtime-sim && python e2e_ollama.py %TEMP%\awake-sim-retrieval.json qwen2.5:latest
```

`worldbook-runtime-sim` 的临时程序做法（现已固定在仓库里）：新建 csproj，把 `AWAKE/src/` 下这 14 个文件按链接方式编进来
（`WorldKnowledgeModels` `WorldbookIdentityEvaluator` `WorldbookIdentityCapabilityRules` `WorldbookEntityId`
`WorldbookPackageIntegrity` `WorldKnowledgeLoader` `WorldKnowledgeQueryService` `WorldKnowledgeProjectionService`
`WorldbookPackageRegistry` `WorldEventLedger` `WorldEventContracts` `WeeklyReportService` `WorldFactCapture`
`WorldFactJournal` `WorldFactQuery`），再编入 `SmokeStubs.cs` 顶掉游戏依赖；`Newtonsoft.Json` 引用游戏目录里的那份（只读引用，不启游戏）。

---

## 六、我的建议

1. **把本机模拟当成内容验收的常规手段**。写完词条 → 编译 → 用这个程序按 6 个身份 × N 个问题跑一遍 → 看"谁答得上来、谁答不上来、说法有没有差别"。这比读代码猜强太多。
2. **在做完整样本之前先把 scope 的语义确认清楚**（它是门槛不是距离），否则样本会按错误的方向写。
3. **C 层（接本地 Ollama 让模型真开口）值得试一次**——它是唯一能在不开游戏、不碰云端的前提下，验证"这套知识喂给模型后，NPC 说出来的话对不对"的办法。

---

**状态**：实测报告，未提交。2026-09-12 14:35。
