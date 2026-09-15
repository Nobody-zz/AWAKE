# 世界书运行时模拟器（WorldbookRuntimeSim）

**一句话**：不开游戏、不联网、不用 API Key，在本机用 **AWAKE 真实运行时代码**跑一遍
「玩家提问 → 世界书检索 → 门控 → 拼提示词 → 本地模型开口回答」的完整链路。

建立时间：2026-09-12　建立者：阿砚（经 Max 确认，沿用其在 codex 上的本地 Ollama 测试路子）

---

## 一、它是干什么的

写世界书内容时，最常踩的三个坑：

1. 写完一个词条，**不知道 NPC 到底答不答得上来**
2. 想让"不同身份说法不同"，**不知道分层到底生效没有**
3. 想验证"这条知识只有贵族知道"，**不知道门控挡没挡住**

这个工具就是用来**提前把这三个问题问出来**的——不用等编译进游戏、不用开游戏。

---

## 二、三层能力

| 层 | 干什么 | 状态 |
|---|---|---|
| **A 检索与门控** | 加载运行时包，按「身份 × 问题 × 范围 × 详细度」矩阵查询，输出 known / partial / blocked / not_found | ✅ 已跑通 |
| **B 提示词组装** | 逐字使用 `AWAKE/src/Prompts/NpcPromptTemplate.cs` 里的真实模板（从源码现读，不是手写近似） | ✅ 已跑通 |
| **C 模型生成** | 走本机 Ollama，让模型真的以 NPC 身份开口 | ✅ 已跑通（`qwen2.5:latest`） |

---

## 三、怎么用

### 前置

- .NET 10（本机已装 10.0.301）
- 本机 Ollama 在跑（默认 `http://127.0.0.1:11434`）— 只需 C 层；A/B 层不需要
- `Newtonsoft.Json.dll`：csproj 里用 HintPath 只读引用游戏目录那份（`D:\SteamLibrary\...\Win64_Shipping_Client\`）。**只是编译期引用，不启动游戏**

### A/B 层：跑门控矩阵

```bash
cd AWAKE/tools/worldbook-runtime-sim
dotnet build -c Release
dotnet run -c Release
```

可选参数：

```bash
dotnet run -c Release -- [manifest.json路径] [导出json路径] [问题,逗号分隔] [身份,逗号分隔]

# 例：只测两个身份、两个问题
dotnet run -c Release -- ^
  "D:\AWAKE-Dev\AWAKE\release\awake-worldbook-pilot\manifest.json" ^
  out.json "收成,领主" "profile.commoner,profile.noble"
```

输出包括：
- 包概览（词条数 / 表达数 / 关键词 / 身份表）
- **门控矩阵**：谁问什么 → known / partial / blocked / not_found + 拿到的文本
- **详细度梯度**：改 `requestedDetail`
- **范围梯度**：改 `scope`
- 导出 `%TEMP%\awake-sim-retrieval.json` 给 C 层用

### C 层：接本地模型

```bash
python e2e_ollama.py [检索json路径] [模型名]
python e2e_ollama.py %TEMP%\awake-sim-retrieval.json qwen2.5:latest
```

默认跑 4 个混合用例：2 个「门控给了知识」+ 2 个「门控不给」（后者用来看**模型会不会胡编**）。

### D 层：组合链路（开发回归探针）

如果要同时观察事实/周报、世界书门控、人物卡和 NPC 回答，使用：

```bash
python ai_chain_sim.py --model qwen2.5:latest --out %TEMP%\awake-ai-chain-sim.json
```

Windows 未安装 Python 时可直接使用等价入口：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\ai_chain_sim.ps1 -Model qwen2.5:latest -Out "$env:TEMP\awake-ai-chain-sim.json"
```

它会先调用真实运行时代码生成固定周报上下文，再把 `tools/persona-workbench/characters/` 的候选卡临时物化为 definition，生成真实人物 DSL，按两个正反用例组装真实 `NpcPromptTemplate`，最后顺序调用本机 Ollama。输出为 JSON；`pass` 只表示离线组合探针通过，`in_doubt` 表示本地模型超时、网络错误或返回无法解析的结果。

该层是开发辅助，不代表游戏内 E4/E5 证据，也不会修改源码、内容包或游戏目录。

### E 层：多人物同场景对照

按 `kingdomId` 从 `tools/persona-workbench/characters/` 各抽一张候选卡，固定周报、世界书问题、记忆、场景和玩家话术，只比较人物 DSL 与模型回答：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\persona_batch_compare.ps1 -Model qwen2.5:latest -Out "$env:TEMP\awake-persona-batch-sim.json"
```

该测试的 `pass` 只表示 8 张（或更多）卡都能进入同一提示词链路并返回结构化 JSON；人物回答是否足够像本人仍单独记录，不能由结构化状态代替。

要检查关系命令是否只在明确关系行为时出现，在同一批卡上运行双场景矩阵：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\persona_batch_compare.ps1 -CommandMatrix -Model qwen2.5:latest -Out "$env:TEMP\awake-persona-command-matrix.json"
```

---

## 四、实测发现（2026-09-12，用 `release/awake-worldbook-pilot`）

### 1. `scope` 是"接触门槛"，不是"地理范围"

`WorldbookIdentityEvaluator.cs:104`：

```csharp
ScopeRank(evaluation.EffectiveScope) >= ScopeRank(rule.Scope)
```

等级 `local=1 < regional=2 < national=3 < faction=4 < elite=5 < private=6`。
**`local` 是最低门槛，谁都够得着。** 实测把 scope 从 local 改到 private，六种**全部 known，毫无差别**。

> ⚠ 想表达"离得远的人只知道传闻"，**不能用 scope**，要用 `conditions.settlement_ids`。

### 2. 请求详细度是"上限"，不是"想要多细"

`WorldKnowledgeQueryService.cs:252`：`expressionDetail > requestedDetail` → PermissionLimited
（`rumor=1 < summary=2 < detail=3 < secret=4`）

实测请求 `rumor` 反而 **blocked**（表达是 summary(2) > 请求 rumor(1)）。**极易踩反。**

### 3. 门控挡得住知识，挡不住模型胡编 ⭐

士兵问"今年收成怎么样"，门控 **not_found**（一条知识都没给），模型照样编：

> "今年雨水足，收成还好。不过税官总是狮子大开口。"

原因：真实提示词模板对"检索到的知识为空"**没有约束**——只对"跨会话记忆为空"写了"不要编造共同经历"。
**这是一个真实缺口**，值得在模板里补一条：`retrieved_knowledge` 为空时必须承认不知道，不许编。

### 4. 试点包里"不同人说法不同"根本不存在

4 个身份拿到的是**一字不差**的同一段文本（每个词条只有 1 条表达）。
分层结构有，分层内容没有。

### 5. 转介一次都没触发

6 身份 × 3 问题，referrals 全 0（C13 未接线）。

---

## 五、边界（别越线）

- ❌ 不启动游戏、不同步游戏目录
- ❌ 不访问云端 Provider / 真实 API Key
- ❌ 不修改 `src\` / `GUI\` / `ModuleData\` / 迁移候选 / 世界书正文 / 模组本体
- ✅ 包会先复制到临时目录再加载，**零写回**
- ⚠ 目前能跑的是 `release/awake-worldbook-pilot`（3 词条 / 6 身份）。**那 12 档从未编译成运行时包，加载不进去**——要验它们，先过编译

---

### 6. 授权上限：写 grant 前必看这张表

`AWAKE/src/WorldbookIdentityCapabilityRules.cs` 把每个身份的**范围与详细度上限写死了**：

| 身份 | scope 上限 | detail 上限 |
|---|---|---|
| 平民 / 村民 | local | **rumor** |
| 市民 | regional | summary |
| 乡绅 | regional | detail |
| 商人 / 酒馆老板 / 赎金经纪人 | faction | detail |
| 士兵 / 头人 | national | detail |
| 贵族 | elite | detail |

**grant 的 scope 与 min_detail 一旦越过该身份上限，这个身份永远 blocked。**
模拟器启动时会打印这张表，便于对照。

### 7. `deny` 是整条词条的核弹

`WorldKnowledgeQueryService.cs:52` 的 `HasMatchingDeny(entry, ...)` 命中即跳过**整条词条**，不是只挡那一条表达。
实测：加了 3 条 deny 后，**连没被 deny 的身份也被一起挡**。用 deny 前先想清楚。

## 六、已知坑

- 权限门**每步都要新的 `--operation` id**，复用报 `WB-AUTHORITY-OPERATION-409`
- 条件字段（`kingdom_ids` / `settlement_ids`）一旦设置，查询方必须提供对应的
  `KingdomId` / `SettlementId` 才会匹配；模拟器目前不传，所以带条件的 grant 一律不匹配
- `entity.lore.*` 编译会抛 `WB-DOC-003`；`hero` / `clan` / `settlement` 正常

| 模型 | 结果 | 出处 |
|---|---|---|
| `qwen2.5:latest` | ✅ **可用**。Local Worker 验收通过（生成候选、证据、needs-review 文档创建/读回全通过） | `AWAKE/docs/AWAKE-CURRENT.md` |
| `gpt-oss:20b` | ❌ **别用**。r48 实测超时、无可用 expansion/DSL 输出，记为真实负面结果 | 同上（r48 段落） |

## 六之二、模型选型（前人踩过的坑）

---

## 七、文件说明

| 文件 | 作用 |
|---|---|
| `WorldbookRuntimeSim.csproj` | 把 15 个真实运行时源文件 + 替身编成控制台程序 |
| `Program.cs` | 门控矩阵、详细度/范围梯度、导出 JSON |
| `SmokeStubs.cs` | 顶掉 `AwakeRuntime` / `WorldStateStore` / `AwakeLog` / `AwakeBackgroundTask` 等游戏依赖（抄自既有 `worldbook-runtime-smoke`） |
| `e2e_ollama.py` | 读真实提示词模板 + 调本机 Ollama |
| `README.md` | 本文件 |

## 八、与既有工具的区别

`tools/worldbook-runtime-smoke/` 是**回归测试**（造合成包、断言固定行为，产出 PASS/FAIL）。
本工具是**内容验收用的探针**（加载真实包、打矩阵、导出结果、可接模型看人话）。
两者共用同一套"真实源码 + 替身"的编译方式。

---

**状态**：未提交（新建未跟踪文件）。2026-09-12 14:40。
