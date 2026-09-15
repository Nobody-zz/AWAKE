# AWAKE 主干代码审核 · 第一轮（2026-09-13）

> 对象：`AWAKE/src` —— **143 个 .cs / 38,901 行**（模组主干的全部生产代码）。
> 性质：**首轮为只读审核**；同日的两轮修订（F1/F2 修、F3 复核纠正、F8 新发现）改动了生产代码，逐条标注。
> 基线：构建 0 警告 0 错误、production smoke 29/29（审核开始时的状态）。
> **修订（同日·第一轮）**：F1 已修 —— 命令分发补 `default` 拦截 ＋ `appliedKeys` 安全取值，新增回归 `unknown-state-kind-rejected`。
> **修订（同日·第二轮）**：F2 已修（`IsKnownSchema` 补 `awake.letters.v1` ＋ 防漂移断言）；F3 **复核纠正**——6 个"整类零引用"里 **2 个是假阳性**（复核命令漏了 `AWAKE.Tests`）；新增 **F8**：测试工程长期编不过。
> 基线（本轮结束）：构建 0/0、production smoke **31/31**。

## 0. 方法与局限

方法：机械扫描（正则 + 文本级引用计数）+ 人工复核关键候选 + 构建/smoke 作对照基线。

**局限（结论必须先按此打折）**：

- 文本级匹配，不解析语法树。**反射调用、字符串拼名调用不计入**（本仓库 `AwakeRuntime` 大量使用反射读游戏类型，若自家类型也被反射调用会漏判——已对下列每条做过字符串复核，排除）。
- 同名重载互相计数；通用名（`Format`、`CanonicalJson` 等）在"仅夹具引用"分组里会造成误报，该分组**只有已复核的条目才可作为结论**。
- 本轮**未覆盖**：权限门禁语义（`PermissionGate` / 硬门不变式）、并发与生命周期、UI 线（`tools/awake-ui-lab` 属 UI 会话）、文档↔代码一致性。

## 1. 结论摘要

| 级别 | 类别 | 数量 | 状态 |
| --- | --- | --- | --- |
| **P1** | 静默失败陷阱：半声明状态种类（有 schema、无状态工厂、无落库分支） | 2 个 kind | **已修**（同日，`default` 拦截 + 安全取值；回归 `unknown-state-kind-rejected`） |
| **P2** | 契约声明不完整：`IsKnownSchema` 漏 `awake.letters.v1` | 1 处 | **已修**（补登记 ＋ 防漂移断言 `storage-schema-contract`，smoke 30→31） |
| **P1** | 测试工程长期编不过：`AWAKE.Tests` 缺失依赖源登记 | 1 处（2 → 6 个编译错误） | **已修**（见 F8；修好后暴露出 1 个既存 persona 用例失败） |
| **P2** | 功能未接通：生产代码零调用 | ≥1 确认（脚本给 16 个候选） | 部分复核 |
| **P3** | 遗留死代码：整类零引用 | 申报 6 个 → **复核后 2 个为假阳性**，余 4 待定 | **已复核纠正**，未删除任何文件 |
| **P3** | 错误吞没：空 `catch` 不留痕 | 58 处（全 src 424 个 catch） | 抽样复核，多为有意防御 |
| **P3** | 随机数使用 | 3 处 | 已复核 |
| **待判** | 同步阻塞 `GetAwaiter().GetResult()` | 5 处确认阻塞 + 3 处待判 | 未逐个判线程亲和性 |

## 2. 详细发现

### F1（P1）静默失败陷阱：`PersonaOverride` / `PersonaRecovery` 是"半声明"状态种类

**证据**（四处对照，缺两处）：

| 登记点 | 位置 | PersonaOverride | PersonaRecovery |
| --- | --- | --- | --- |
| 枚举声明 | `src/WorldStateStore.cs:32-33` | 有 | 有 |
| schema 映射 | `src/AwakeStorageContract.cs:81-84` | 有 | 有 |
| 状态工厂 `NewState` | `src/WorldStateStore.cs:2864-2885` | **无**（落 `default: return new JObject()`） | **无** |
| 落库分发 `Apply` | `src/WorldStateStore.cs:2750-2797` | **无 case** | **无 case** |

`WorldStateKind.PersonaOverride` 在全 `src` 仅出现 1 次（即 `ExpectedSchema` 内），**无任何调用方**：

```
grep -rn "WorldStateKind.PersonaOverride" AWAKE/src   # → 仅 AwakeStorageContract.cs:81
```

**影响**：当前无行为影响（没人发这类命令）。真正的风险在 `Apply` 的 switch **没有 `default` 分支**：

```csharp
// WorldStateStore.cs:2748-2801（节选）
JObject eventPayload = null;
string applyError = string.Empty;
switch (command.Kind)
{
    case WorldStateKind.Memory: applyError = ApplyMemory(...); break;
    // ... 16 个 case，无 default
}
if (!string.IsNullOrWhiteSpace(applyError)) { return new WorldApplyResult { Applied = false, ... }; }
state["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
// → 继续写回 store
```

未匹配任何 case ⇒ `applyError` 保持空字符串 ⇒ **判为成功**、更新 `updatedUtc`、把**未修改的文档**写回。即：一旦有人按 schema 表发出 `Kind = PersonaOverride` 的命令，会拿到"成功"回执，而**什么都没落库**。属静默丢写，不抛错、不留痕、无日志。

**建议**：`Apply` 的 switch 补 `default: return new WorldApplyResult { Applied = false, Retryable = false, Code = "awake.world_state.unknown_kind" };`（或对这两个 kind 补齐实现）。补 default 是低风险且能一次性兜住将来所有"新增枚举忘登记"的情况。

> 附注：这正是本仓库已登记的坑 ——"新增文档型命名空间要落 6 处，漏任一处都是运行期 NRE 或静默 schema 失配"。F1 是这条规律的第 7 个落点（枚举↔实现），且因为缺 default，失效形态是**静默**而非 NRE。

**已修（2026-09-13，同日）**

1. `WorldStateStore.TryApplyCoreAsync` 的命令分发 switch 补 `default`：写 `world_state_unknown_kind key=<key> kind=<kind>` 日志，并置 `applyError = "awake.world_state.unknown_kind"`（`Retryable = false`）。
2. 同方法内 `appliedKeys` 由强制转换改为 `as JArray` ＋ `?? Enumerable.Empty<JToken>()`。**这是实施时才暴露的第二条通路**：`NewState` 对未知 kind 落 `new JObject()`（没有 `appliedKeys`），强制转换得到 null 后 `foreach` 抛空引用；该异常被 drain 的兜底 `catch` 转成**可重试**错误码，既产生噪音又掩盖真正的接线问题。本报告原稿未列出这条通路——只补 `default` 并不足以修好 F1。
3. 新增回归 `unknown-state-kind-rejected`（`tools/worldbook-runtime-production-smoke`）：投一条 `Kind = PersonaOverride` 的命令，断言被丢弃（`DroppedItems == 1`）、存储零写入、有日志痕迹。smoke 29 → **30**。

**未采纳的替代方案**：给这两个 kind 补齐 `NewState` / `Apply` 实现。它们在全仓库零调用方，补实现等于凭空引入无人使用的存储形状；语义应由角色卡线在真正需要时定义。

### F2（P2）`IsKnownSchema` 名单不完整（已修）

**证据（复核后）**：

- **不是零调用者**：`AWAKE.Tests/Program.cs:1072` 与 `:3726` 各有一处真实调用。原稿只 grep 了 `AWAKE/src` 就下了"死代码"的定性，**不准确**（见 F8：测试工程当时根本编不过，所以没跑起来）。
- 名单（原 `AwakeStorageContract.cs:30-46`）列出 17 个 schema，**缺 `LettersSchema`（`awake.letters.v1`）**，而 `ExpectedSchema(WorldStateKind.Letters)` 恰恰返回它 ⇒ **同一份契约内部自相矛盾**（工厂产出 A，白名单不认 A）。
- `WorldbookOverlaySchema` 性质不同：它没有对应的 `WorldStateKind`，属"无 Kind 的伴随常量"，`docs/PLAN-PersonaWorkbench-AWAKE-Joint-ARCHITECTURE-MATRIX-20260824.md:131` 已登记为"待确认的历史债务"。本次不动。
- 外部引用：`tools/persona-awake-joint/verify-runtime-bridge-static.ps1:82` 对它做文本检查。

**已修（第二轮）**：补 `|| StringComparer.Ordinal.Equals(schema, LettersSchema)`。**未采纳**"标废弃并删掉"——它有真实调用者，且 persona joint 契约（`docs/PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-20260824.md:112`）把"常量 / `IsKnownSchema` / `ExpectedSchema` 三重映射"写成硬要求，删掉会破坏该契约。

**新增不变式 ＋ 回归**：`ExpectedSchema` 的任何返回值都必须被 `IsKnownSchema` 认可。已落两处断言 —— production smoke 新增用例 `storage-schema-contract`（smoke 30 → 31）、`AWAKE.Tests` b9 infra smoke 新增同款遍历。`AwakeStorageContract` 的 XML 注释亦写明该不变式。

**遗留（跨线，未动）**：`verify-runtime-bridge-static.ps1:98` 的 `persona_storage_schema_registered` 是在 `IsKnownSchema` 的**方法体文本里搜三个 schema 字面量**，而该实现用常量引用、不含字面量 ⇒ **恒为 false（误报 reject）**。这**不是新发现**：`docs/REVIEW-G3-A-TECHNICAL-PRE-REVIEW-20260911.md:48-50` 已记过并写明修订要求（改为验证三重映射、不得要求复制字面量）。该文件属 persona joint 工具链，**留给该线认领**。

### F3（P3）整类零引用：原报 6 个，**复核后 2 个是假阳性**（未删除）

**先说错误**：原稿复核命令写作 `grep -rn … AWAKE/src AWAKE/tools AWAKE/GUI`，**漏了 `AWAKE.Tests`**；审计脚本也只扫 `src/` + `tools/` 两个语料。于是那句"全仓库零引用"实为"src+tools 内零引用"——**两个词面很像、含义不同**。逐名重查（`grep -rn <name> D:\AWAKE-Dev`，仅排除 `artifacts/`）：

| 类型 | 位置 | 复核结论 |
| --- | --- | --- |
| `WorldbookLoader`（**590 行**） | `src/WorldbookLoader.cs:9` | ❌ **假阳性**：`AWAKE.Tests/Program.cs:1908/1940/1958` 在调 `TryParseRule` |
| `WorldEventInboxFormatter` | `src/WorldEventInboxFormatter.cs:6` | ❌ **假阳性**：`AWAKE.Tests/Program.cs:760/767` 在调 `Format` |
| `NarrativeReportBuilder` | `src/NarrativeReportBuilder.cs:7` | ✅ 三语料零调用（仅 `AWAKE.Tests.csproj` 编译登记）。**未删** —— UI 线 `AF-UI-Assets-Inventory-20260816.md:14` 把它标为「世界周报 | 命令台文本弹窗 | 复用」，属 UI 线规划资产 |
| `BannerlordNativeSocialReader` | `src/BannerlordNativeSocialReader.cs:8` | ✅ 零调用（全仓 .cs 仅 1 次命中＝定义处）。**未删** —— `AWAKE-NativeState-Adapter-Spec-v1.md:454` 将其列为规格组件。⚠️ 上表原记的"文档冲突"**已查清**：`RUNTIME-HERO-DATA-READABILITY-20260911.md:11` 那句"relation 已在产"是**误述**——同文档第 76 行写明实时关系实际走 `NpcDialogueService._npcState`（`FormatState` → `{{npc_state}}` / DSL `CURRENT_STATE`），与 `BannerlordNativeSocialReader` 无关；该表述也与 `AWAKE-GLOBAL-MECHANISM-AUDIT-20260827.md:232`（"已能读取…但源码内无有效消费者"）不一致 |
| `AwakeDialogueQueueEntry` | `src/EventDialogueQueue.cs:9` | ✅ **已删（`66e4056`）**。同文件 `PendingDialogue` / `EventDialogueQueue` 仍在使用（`Queue<>` 与 `RunDialogueQueueSmoke`）⇒ 只删类型、保留文件；编译器 0 错为机器证明 |
| `PersonaPersistenceProducerContract` | `src/PersonaPersistenceService.cs:115` | ✅ 零调用。**未删** —— 属角色卡线语义 |

**裁决与执行（2026-09-13）**：用户批"删"后逐条核，4 个候选中**只有 1 个**同时满足「代码线自有 + 三语料零引用 + XML/JSON 资源亦零引用 + 无任何文档牵扯」——`AwakeDialogueQueueEntry`，已删（`66e4056`）。另 3 个均**停在红线前未动**（理由见上表"未删"），因为删它们＝删 UI 线规划资产 / 规格组件 / 角色卡线内容。

**起因回顾**：① 两个假阳性说明原取证方法有缺陷，不能拿它当删除依据；② `WorldbookLoader` 由世界书线自己认领为清理项（`docs/worldbook-migration/STATUS-20260912.md:60`、`docs/artifact-retention/CLEANUP-EXECUTION-20260911.md:73` 均记"挂账，等独立清理批次"）。

**已修工具**：审计脚本补了第三个语料 `../AWAKE.Tests/`，"全仓零引用"重定义为**三语料同时为零**，防同类误判复发。

**遗留待裁（3 个）**：`NarrativeReportBuilder`（需 UI 线确认是否仍要复用）、`BannerlordNativeSocialReader`（需规格侧确认是否保留预埋）、`PersonaPersistenceProducerContract`（角色卡线自决）。判据建议统一为：**"被取代"则删，"预埋且有名有姓的规划文档在引用"则留并加注**。

### F4（P2）生产代码零调用：已实现但未接通的入口

脚本对 16 个候选报了"仅夹具引用"。**此分组存在词面误报**（`Format`、`CanonicalJson` 这类通用名会命中无关代码），因此**只有逐个复核过的条目才能作为结论**。本轮已复核：

| 方法 | 位置 | 引用情况 |
| --- | --- | --- |
| `AwakeLetterService.GetUnreadAsync` | `src/AwakeLetterService.cs:405` | `src` 内零调用；`tools` 内 5 次（全部为 smoke 断言） |

这与已知状态一致：信件未读**只做了聚合，尚未接大地图通知**（设计稿 `PLAN-AWAKE-LETTER-DELIVERY-20260913.md` §10 亦记为未接）。即"未读数是算得出来的，但没有任何界面/通知会去读它"。

**建议**：这不是缺陷，是**已登记的未完成项**；建议在设计稿里把它标为"生产零调用"，以免日后误以为已上线。

> 方法论提醒（**第二轮更正**）：原文写"脚本 A 组（全仓库零引用）可信度高"，是错的 —— A 组当时**漏了 `AWAKE.Tests` 语料**，而抽样复核又用了同样漏项的命令，等于自证。现 A 组 = 三语料同时为零；**A/B 两组都必须逐个复核后才能采信**。

### F5（P3）错误吞没：空 `catch` 58 处，全部不留痕

**精确计数**（`tools/src-audit/_audit_counts_20260913.py`）：全 `src` 146 个文件共 **424 个 `catch`**，其中**空体 58 个**。

**分布**：`AiTaskGateway` `PermissionGate` `BannerlordNativeSocialReader` `AwakeTerminalBehavior` `NpcDialogueLauncher` `NpcMemoryService` `NpcDialogueService` `SceneDialoguePreview` `SubModule` 等。

**抽样判定**：多数是**有意的防御性吞没**，属正确设计，例如：

- `AwakeFeedback.cs:23`（`InformationManager.DisplayMessage` 失败不该崩游戏）
- `KnowledgeRuntime.cs:24`（`Dispose` 异常）
- `BannerlordNativeSocialReader.cs:51-55`（逐个原生 API 取值，取不到就留空）
- `AiTaskGateway.cs:323-324`、`PermissionGate.cs:269`（`Cancellation`/`Subscription` 的 `Dispose`）

**问题不在"吞"，在"不留痕"**：本仓库有统一的 `AwakeLog`，但这些分支一条日志都不写。当原版 API 因版本升级改名、某些数据静默取不到时，**现地不会有任何线索**——只能靠猜。

**建议**：保留吞没语义，但在 `catch` 内补 `AwakeLog.Write("...")`（可加"低频/仅首次"抑制刷屏）。优先级低于 F1/F2。

### F6（P3）随机数

| 位置 | 写法 | 风险 |
| --- | --- | --- |
| `src/AwakeEventEngine.cs:199` | `AwakeEventEngineCore.SelectWeighted(eligible, new Random())` | **每次调用新建**：.NET Framework 上 `Random()` 无参构造以 `Environment.TickCount` 为种子，**同一 tick 内连续构造会得到完全相同的序列** ⇒ 同一次 tick 内多次选取结果可能雷同 |
| `src/NpcProactiveService.cs:28` | `private readonly Random _random = new Random();` | 实例字段，可用；但**非线程安全**，若被多线程调用会有小概率返回 0 |
| `src/NpcLetterInitiator.cs:31` | `private static readonly Random Roll = new Random();` | 同上，且为 `static`（跨会话共享）。当前仅主线程每小时 tick 调用，风险低 |

**建议**：统一改用一个进程级随机源（或加锁/`RandomNumberGenerator`）。`AwakeEventEngine:199` 的"每次 new"建议至少改为复用实例。

### F7（待判）同步阻塞

`GetAwaiter().GetResult()` / `.Result` / `.Wait()` 共 14 行命中。**确认以同步方式等待异步任务**的 5 处：

- `src/AwakeRuntime.cs:485`（`_nativeReadinessTask`）、`:1022`（`ResetSessionStateForTestingAsync`，仅测试路径）、`:1170`（退役 drain）
- `src/AwakeGoldSettlementService.cs:144`、`:259`

**判定为安全**：`src/ProbeExtension.cs:246-284` 的 6 行 —— 均先判 `task.IsCompleted` / `Status == RanToCompletion` 再读 `.Result`，属"已完成任务的读值"。

**未逐个判**（3 处）：`src/AwakeHostComposition.cs:360`、`src/AwakeMessengerVM.cs:474`、`src/AwakeUiDispatcher.cs:65` —— 读的是 `completed.Result` / `task.Result`，形式上像"已完成读值"，但未追进上游任务确认。

**风险**：若调用点位于 UI 线程、且被等待的任务需要回到 UI 线程，会死锁。列为下一轮项。

### F8（P1）`AWAKE.Tests` 长期编不过（已修）

**发现**：`dotnet build AWAKE.Tests/AWAKE.Tests.csproj -c Release` → **2 个错误**：

```
src/AwakeLetterService.cs(387,37): error CS0246: 未能找到类型 AwakeLetterRecord
src/AwakeLetterService.cs(502,41): error CS0246
```

`AwakeLetterRecord` / `AwakeLetterConstants` 定义在 `src/AwakeLetterLedger.cs`，而 `AWAKE.Tests.csproj` 只登记了 `AwakeLetterService.cs`（`:87`），**漏了它的依赖**。

**归因（git 证据，不猜）**：`AwakeLetterService.cs` 由 `8ba65bf` 登记进测试清单，当时它是**自洽**的（`git show 8ba65bf:AWAKE/src/AwakeLetterService.cs | grep AwakeLetterRecord` → 0 次）；`AwakeLetterLedger.cs` 由 `e9c9069`（**本线**"远程信件送达闭环"）新建，`AwakeLetterRecord` 同笔引入 `AwakeLetterService.cs` ⇒ **是本特性线把测试工程弄红的**，违反红线②"AWAKE.Tests.csproj 引新源须与源同笔"。

**修法（尝试与取舍）**：补依赖时发现链子是 `AwakeLetterService` → `AwakeMessengerService` → `NpcDialogueLauncher` → `AwakeMessengerOverlay` / `NpcDialogueOverlay`（**Gauntlet UI**），而测试工程不引用 `TaleWorlds.MountAndBlade` ⇒ 整链补登记是死路。故按**可编译性切分**：

1. `MaximumLetterBytes` 常量由 `AwakeLetterService` 移入 `AwakeLetterConstants`（账本文件，零依赖、本就在册）；
2. 纯校验器 `AwakeLetterRequestValidator`（`Program.cs` 真在用它）**拆为独立文件** `src/AwakeLetterRequestValidation.cs`（零外部依赖）；
3. 测试清单登记 `AwakeLetterLedger.cs` + `AwakeLetterRequestValidation.cs`，并注明 `AwakeLetterService.cs` 为何不在列。

修后 `AWAKE.Tests` **0 警告 0 错误**。

**连带暴露一个既存失败（不属本线，未动）**：测试工程能编之后跑起来，`RunPersonaTemplateSmoke`（`Program.cs:1179`）抛 `approved persona should generate canonical authored DSL`。该用例只依赖 `PersonaDslGenerator` / `PersonaDataLoader` / `PersonaTagRegistry`，**与本次改动无关**，是被"编不过"掩盖至今的既存失败。且该 SDK smoke **顺序执行、遇错即终止**（`RunB9InfraSmoke()` 在调用序列第 124 行，位于失败点 `:94` 之后）⇒ 失败点之后的用例**全部未执行**。**交角色卡线处理。**

## 3. 纪律亮点（同样要说，避免只报坏消息）

以下高危模式**全仓库为零**，属明确的正向结论：

- **无 `async void`**：全 `src` 146 个文件里 `async void` 计数为 **0**，`async Task`/`ValueTask` 共 169 处（脚本精确计数）
- **无 `Console.WriteLine` / `Debug.WriteLine`**（日志统一走 `AwakeLog`）
- **无 `TODO` / `FIXME` / `HACK` / "待实现"**
- **无硬编码绝对路径**（文件 IO 全部走 `Path.Combine` + 模块目录定位，集中在 `AwakeLog` / `AwakeFileStorageService` / `ProbeExtension` / `WorldbookRuntime` 四个受控点）
- **静态可变状态仅 4 处**：`AwakeRuntime.CurrentGameDayProvider` / `CurrentGameHoursProvider`、`ProbeExtension.Reset`、`WorldEventLedger.UtcTicksProviderForTesting`（其中 3 处是测试注入点）

## 4. 第二轮建议范围

0. **把测试工程纳入判据**：基线由"`dotnet build` + production smoke"扩为 **再 ＋ `dotnet build AWAKE.Tests/AWAKE.Tests.csproj -c Release`**。F8 之所以能潜伏（自 `e9c9069` 至今），正因为测试工程不在任何人的构建闸口上。**注意**：该工程现已能编译，但**跑起来会在 persona 用例处失败**（见 F8）⇒"能编译"可立即入闸，"运行全绿"须待角色卡线修好后再入闸。

1. **权限门禁专项**：`PermissionGate` / `WorldKnowledgeQueryService` / `WorldbookIdentityEvaluator` 的硬门不变式（`grant.min_detail == layer`、`Deny > Grant > 继承 > unknown`）是否有绕过路径；`detail` 非法值（非 `rumor/summary/detail/secret` → `DetailRank = -1` → 假授权故障）是否已加校验。
2. **并发与生命周期**：F7 的 8 处同步阻塞逐点判线程亲和性；`WorldStateStore` 的 `_gate` / `ApplyLocked` 与动态槽单写者不变式（先 `Bind` 后注入）。
3. **重试/幂等**：`appliedKeys` 幂等键在 16 个 kind 上的覆盖是否一致（F1 的两个 kind 若补齐，是否也要接幂等）。
4. **文档↔代码一致性**：`docs/` 里声明的机制与 `src` 实际实现逐条比对。

## 附录 A：本轮使用的脚本与原始输出

- `tools/src-audit/_audit_unwired_members_20260913.py`（新增，只读）
  用法：`python _audit_unwired_members_20260913.py`；排除 `out/bin/obj/node_modules/__pycache__/dist/release/workspace`
  语料：`src/` + `tools/` + **`../AWAKE.Tests/`（第二轮补，见 F3）**
- `tools/src-audit/_audit_counts_20260913.py`（新增，只读）
  用途：`async void` / `async Task` / `catch` 总数 / 空体 `catch` 的精确计数
- 原始输出：`tools/src-audit/_result_unwired_20260913.txt`（**第一轮版本，未含测试语料**；跑 F3 结论请以重跑为准）

## 附录 B：复核命令集合

```bash
# 零引用候选复核 —— ⚠️ 三个语料都要扫；只扫 src+tools 会把"测试在用"误判成死代码（F3 的错就出在这）
for n in WorldbookLoader NarrativeReportBuilder BannerlordNativeSocialReader \
         WorldEventInboxFormatter AwakeDialogueQueueEntry PersonaPersistenceProducerContract \
         HasNearbyHero IsEligibleNearbyHero CapturePlayerToCurrentNpc WriteCode; do
  echo -n "$n : "; grep -rn --include=*.cs "\b$n\b" AWAKE/src AWAKE/tools AWAKE.Tests | wc -l
done

# 半声明 kind 复核
grep -rn "WorldStateKind.PersonaOverride\|WorldStateKind.PersonaRecovery" AWAKE/src

# IsKnownSchema 调用面（含测试工程）
grep -rn "IsKnownSchema" AWAKE/src AWAKE.Tests

# 测试工程可编译性（本线判据之一；F8 即由此发现）
dotnet build AWAKE.Tests/AWAKE.Tests.csproj -c Release
```
