# Marcus 能力「存活」校准 · A0 档

- 日期：2026-09-15
- 范围：`AWAKE/framework/`（五个框架模块）＋ `AWAKE/src/`（玩法侧）＋ `AWAKE/tools/`
- 依据（三份 08-24 权威文档，本次不新增口径）：
  - `MARCUS-AWAKE-CAPABILITY-CARRYOVER-MATRIX-v1-20260824.md` — 继承级别 A0/A1/A2/N 的**唯一**出处
  - `MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md` — F-001…F-066 的 owner / 计划产物 / 阶段 / 状态
  - `MARCUS-AWAKE-FULL-CAPABILITY-INVENTORY-v1-20260824.md` — F-ID 的能力定义
- 目的：这三份清单的「当前状态」列停在 2026-08-28，之后无人更新，于是从工作台退化成历史文档。本文档给这一列重新装上证据底。
- 方法：只看三样——① 契约在不在；② 框架侧实现是**真件**还是 `Unavailable*` **空壳**；③ 玩法侧（`AWAKE/src`）有没有真的调用它。
- **本文档不修改运行时代码，不改变任何 F-ID 的 owner / 阶段；只校准「当前状态」一列。**

---

## 1. 一句话结论

**契约基本齐、框架真件不少、断口集中在「框架 → 玩法」那道缝上。**

A0 那一档里，真正从头到尾通的是：AI 网关、会话生命周期、权限、命令、提示词、存储、**以及 RAG（09-15 当天接通，见 §4）**。而**媒体、模型、资产、工具候选、日志、存档锚点这六面，玩法侧一行都没调过**。

---

## 2. Host 能力面实测

统计口径：`AWAKE/src/**/*.cs` 里对 `host.<能力面>` 的引用行数；框架侧实现指 `HostApi.cs:74-86` 的实际赋值。

| 能力面 | 玩法侧调用 | 框架侧实现 | 判定 |
| --- | --- | --- | --- |
| Sessions | 5 | 真件 `SessionCoordinator` | 通 |
| GameData | 5 | 玩法侧自填 `AwakePlayerSnapshotProvider` | 通 |
| Diagnostics | 9 | 真件 `HostDiagnosticsService` | 通 |
| Runtime | 10 | 真件 `RuntimeServiceClient` | 通 |
| Ai（网关） | 2 | `runtime as IAiGateway`，由 Runtime 客户端充当 | 通（`AiTaskGateway.cs:206`） |
| Prompts | 3 | 玩法侧自填 `AwakePromptRegistry` | 通 |
| Storage | 2 | 玩法侧自填 `AwakeFileStorageService` | 通 |
| Permissions | 2 | 玩法侧自填 `AwakePermissionService` | 通 |
| Commands | 2 | 真件 `HostCommandService` | 通 |
| Context | 2 | 真件 `ContextPlanner` | 通 |
| Events | 1 | `InMemoryEventService` | **半通**：调用点用了 `EventDelivery.Durable`，但背后是内存版，落盘/spool 未接 |
| Rag | 3 | `runtimeClient` 充当 `IRagService`（09-15 填槽） | **通（代码层）**：`KnowledgeService.cs:199/283/322` 真在调；服务侧 `SqliteStorageAndRagBackend` 真件经 IPC 到达。**游戏内未验证** |
| Models | 0 | **`UnavailableAiModelService`**，且无 override 槽 | 未碰 |
| Media | 0 | **`UnavailableMediaService`**，且无 override 槽 | 未碰 |
| Assets | 0 | **`UnavailableAssetService`**，且无 override 槽 | 未碰 |
| Tools | 0 | 真件 `HostToolCandidateService` | 未碰（有真件，没人调） |
| Log | 0 | **`UnavailableLoggingService`** | 未碰（玩法侧另写 `Awake.log` 绕开） |
| Capabilities | 0 | 真件 `HostCapabilityBroker` | 未碰（有真件，没人调） |
| SaveAnchors | 0 | **`UnavailableSaveAnchorStore`** | 框架侧空壳；玩法侧用原生 SyncData 自存（`PersonaContinuitySync.cs`） |

`FrameworkServiceOverrides` 只有 5 个槽：`Permissions / Prompts / Storage / GameData / Rag`。**`Rag` 槽原为空缺**（`AwakeHostComposition.cs` 注释原文：「本批不接 Runtime RAG 数据面（011 再决策）」），于是 `KnowledgeService` 那三处调用落在 `UnavailableRagService` 上、是**确定失败**的路径——不是"没写"，是"写了、对面是空壳"。**09-15 当天已改填 `runtimeClient`，见 §4 第 3 条。**

`Models / Media / Assets` 是 `HostApi.cs` 里**硬编码**的空壳，**连 override 槽都没有**（`PLAN-AI-PORTRAIT-IMAGE-20260913.md §1③` 已记）。

---

## 3. A0 逐项校准

A0 = 矩阵 §2「第一版内置框架不可缺失；缺失即不再是 Marcus-Awake」。以下 F-ID 取自矩阵 §3–§6 中继承级别为 A0 的条目。

| F-ID | 能力 | 契约 | 框架实现 | 玩法侧调用 | 校准后状态 |
| --- | --- | --- | --- | --- | --- |
| F-002 | Host / Extension 生命周期 | 在 | 真件 | 5 处 | **available** |
| F-009 | GameDataQuery | 在 | 玩法侧自填 | 5 处 | **available** |
| F-010 | EntityRef 与稳定身份 | 在（7 文件） | 真件 | — | **available**（未见玩法侧显式消费，待验证） |
| F-011 | 数据可见性 Scope | 在（4 文件） | 真件 | — | **available**（待验证） |
| F-012 | Snapshot / 一致性 | 在（8 文件） | 真件 | — | **available**（待验证） |
| F-013 | Context Provider / Planner | 在 | 真件 `ContextPlanner` | 2 处 | **available** |
| F-015 | SQLite FTS5 RAG | 在 | **服务侧真件**；客户端 09-15 补齐转发 | **3 处（已通）** | **available（代码层）** ★原最大缺口，09-15 已接 |
| F-017 | Prompt Registry | 在 | 玩法侧自填 | 3 处 | **available** |
| F-018 | AI Gateway | 在 | Runtime 客户端充当 | 2 处 | **available** |
| F-019 | Connection / Model / Route | 在 | 真件（5 路分派） | 经 Runtime | **available** |
| F-020 | Provider Capability Report | 在（9 文件） | 真件 | — | **available**（待验证） |
| F-021 | OpenAI-compatible Adapter | 在 | 真件 | — | **available**（片 1 已补生图面） |
| F-022 | Anthropic Adapter | 在（13 文件） | 真件 | — | **available** |
| F-023 | Ollama Adapter | 在（13 文件） | 真件 | — | **available** |
| F-024 | Player2 Adapter | 在 | 真件（片 1 已落地） | — | **available**（**与 ownership map 的 `deferred` 冲突，见 §4**） |
| F-027 | 流式与取消 | 在（23 文件） | 真件 | — | **available** |
| F-030 | Structured Output | 在 | 真件（符号名待考） | — | **available**（命名待验证） |
| F-031 | Tool Candidate 循环 | 在 | 真件 `HostToolCandidateService` | **0** | **contract_only**：有真件，玩法侧没接 |
| F-038 | Save Anchor | 在 | **空壳** | 0（玩法侧走原生 SyncData） | **split**：框架锚点未接，原生路径可用 |
| F-040 | R0–R3 Command Authority | 在（4 文件 `Preflight`） | 真件 | 2 处 | **available** |
| F-041 | Preflight / Revalidation / Receipt | 在（3 文件 `SettlementReceipt`） | 真件 | — | **available** |
| F-042 | Named Pipe 协议 | 在（8 文件 `Handshake`） | 真件 | 经 Runtime | **available** |
| F-043 | Session Invalidation | 在 | 真件（`SessionLease` / epoch / fence，符号名待考） | — | **available**（命名待验证） |
| F-044 | Service Process Management | 在 | 真件 | 经 Runtime | **available** |
| F-046 | Credential Reference | 在 | 玩法侧 `AwakeImageSecretStore` | — | **available** |
| F-047 | Cloud Export Policy | 在（5 文件 `CloudExport`） | 真件（`EgressBroker` 符号名待考） | — | **available**（命名待验证） |
| F-048 | 离线与故障收缩 | 在 | 真件 | — | **available**（待验证） |
| F-054 | MCM 快速配置 | 在 | 玩法侧 | 已闭环（09-13） | **available** |
| F-057 | Observability | 在 | 真件 `Diagnostics` | 9 处 | **available** |
| F-058 | 性能 / 背压 / 熔断 | 在 | 真件 | — | **available**（待验证） |
| F-060 | 包 / 许可证 / 来源 | 在 | — | — | **available**（静态证据） |
| F-061 | 双版本兼容 | 在 | — | — | **available**（静态证据） |
| F-062 | 配置 / 存档 / 索引迁移 | 在 | — | — | **available**（静态证据） |

**状态词义**：`available` = 契约＋实现＋调用三样都在（或明确由静态证据支撑）；`contract_only` = 契约和真件都在、玩法侧没接线；`blocked` = 有调用点但对面是空壳；`split` = 有一条通、有一条断；`待验证` = 本次未逐行取证，不写死。

**缺口排序（按"接了就能用"排名）**：
1. ~~**F-015 RAG** — 唯一"调用点已写、只要填槽就活"的一条~~ ⇒ **09-15 当天已接**（填槽 + 客户端转发 + 端到端判据 9/9）。原判断成立：确实是"改一个赋值点就活"。
2. **F-031 Tool Candidate** — 真件在，玩法侧零调用。
3. **F-049/F-050 资产与生图** — 服务侧空壳（片 1 已把 Provider 层补齐，**服务层未接**）。
4. **F-033 Durable Event** — 调用点写了 `Durable`，背后是内存。

---

## 4. 冲突与留痕（跨线，先记录不动手）

1. **F-024 Player2 / F-050 Image Generation 的继承级别**：矩阵 §7 把 Image generation 定为 **A2**，§3.1 明确 A2 = 「不是第一版核心」；ownership map 记 F-024 `deferred`、F-050 `deferred`。
   而 2026-09-15 甲方口径是「**把 Marcus 原版生图做回去，上限很重要**」，片 1 已实装 `Player2Provider` / `OpenAiCompatibleProvider` 生图面。
   ⇒ **代码已越过矩阵的 A2 定级。** 需要在矩阵 §3.1 / §7 与 ownership map 的 F-024 / F-050 行**补一笔留痕**，说明这次提前实装是甲方决策、不是越权扩写。**本次只记录，不擅自改权威文档。**

2. **08-24 清单的「当前状态」列**：`contract_locked` 是 P1.5 时代的值（矩阵 §10.5 明文：它**不表示** E2–E5 已通过）。§3 校准列与它不冲突，是给同一列补上更细的三段底。

3. **F-015 RAG 状态变更留痕（09-15 当天，本表同步）**：本表首次落库（提交 `f513222`）时 F-015 记为 `blocked`，依据是"调用点已写、槽位为空、对面是 `UnavailableRagService`"。同日稍后该链路接通（提交 `9ca5af7`：`AwakeHostComposition` 填 `Rag = runtimeClient`；`RuntimeServiceClient` 补 `IRagService` 实现与 RAG 两能力名；新增 `StorageAndRagWire.cs` / `RuntimeServiceClient.Rag.cs` / `RagClientTests.cs`，端到端 9/9、两次变异检验均被抓住）。⇒ 本次把 §1 / §2 / §3 / §6 四处一并改为「代码层已通」。

   ⚠️ **仍未取证**：① **游戏内未验证**（本线至今未进游戏）；② 08-24 三份权威文档里 F-015 仍记 `planned`，**未同步**（那三份不在本线写权内，另行处理）。

---

## 5. AuthorSource 四个示范模组：尚未开采

- 位置：`MarcusAIFramework_Reference/AuthorSource/src_20260813/`，**165 个文件**。
- 矩阵 §8 明文：`MarcusAINpc` / `MarcusAIRelationships` / `MarcusAIDiplomacy` / `MarcusAIWorldEvents` 的**全部行为纳入 AWAKE 适配与验收**，只是不把玩法代码塞进 Framework Core。
- 现状：**基本未读**。对应 F-063（deferred）/ F-064（partial）/ F-065（deferred）/ F-066（partial）。
- 自我约束（09-15）：**读它们之前，不再从零设计 NPC 对话那条链路。**
- 判断：这是 A0 骨架之外真正决定"上限"的料。A0 决定"在不在"，这四个模组决定"像不像、好不好玩"。**不是第一优先级，但也不能一直放着。**

---

## 6. 下一步（三条腿，按能不能有 > 能不能用 > 好不好用 排）

1. ~~**接 RAG（F-015）**：`AwakeHostComposition` 填 `Rag` 槽 → `KnowledgeService` 那三处从"确定失败"变"真检索"。这是**改一个赋值点**就能让一条已写好的链路活过来的活。~~
   ⇒ **09-15 已完成**（提交 `9ca5af7`：填槽 + 客户端转发 + 端到端判据 9/9）。**原判断成立**——确实是"改一个赋值点就活"。
   同一条腿的**延伸**：这条链至今**只在离线验台里通过**，本线一次都没进过游戏。
2. **把本表变成会自己更新的东西**：现状是"人手工维护一列状态 → 必然停更"。可行的替代是让状态从证据里长出来（对每个 F-ID 记录"去哪找证据"的三个锚点，由探针跑）。
3. **开采 AuthorSource**：先出四份"行为清单"（每个模组干了哪些事、对应哪个 F-ID、AWAKE 现在有没有），再决定接哪条。

---

## 7. 未取证项（诚实标注）

- 标 `待验证` / `命名待考` 的条目：本次用符号名 grep 命中数做粗判（如 `StructuredOutputSchema` / `SessionInvalidation` / `EgressBroker` 零命中，大概率是**改名**而非缺失），**未逐文件核对**。
- `F-010 / F-011 / F-012` 在框架里是真件，但**玩法侧如何消费**未取证。
- 本文档所有"调用点数"来自 `grep -c`，是**引用行数**不是**执行次数**；只用来判"有没有人碰"，不能当覆盖度指标。
