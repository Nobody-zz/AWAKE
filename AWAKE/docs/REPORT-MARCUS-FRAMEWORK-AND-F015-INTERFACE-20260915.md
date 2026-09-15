# 移交报告 · Marcus 框架现状 与我实际实现的接口

- 日期：2026-09-15
- 出具：模组主体（代码）线
- 移交对象：**全局主控对话**，用于重新判断开发方向
- 范围：`AWAKE/framework/`（五个框架模块）＋ `AWAKE/src/`（玩法侧）＋ `AWAKE/AWAKE.Tests/`
- 本报告**只陈述事实与证据**，不改任何代码、不改任何权威文档；`§7` 列出待裁决项。

---

## 0. 三句话结论

1. **框架本体是真的，不是脚手架。** 五个模块合计 **36,732 行 C#**，含 SQLite+全文检索、四个 AI 平台适配、进程间通信、内嵌进游戏的那层客户端，各自带验台。
2. **断的不是"框架没写"，是"框架 → 玩法"那道缝。** 框架暴露 **37 个契约接口**，玩法侧真正从头调通的只有 7 面；**11 面挂在写死的空壳上、其中 3 面连替换的口子都没留**。
3. **本次我实装的只有一件事：把检索（RAG）这条已写好、对面是空壳的链路接通。** 接口级细节见 `§4`。**游戏内零验证**——本线至今一次都没进过游戏。

---

## 1. 证据与口径

- 一手清单（08-24，三份权威）：`AWAKE/docs/MARCUS-AWAKE-CAPABILITY-CARRYOVER-MATRIX-v1-20260824.md`（继承级别 A0/A1/A2/N 的唯一出处）、`MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md`（F-001…F-066 逐项）、`MARCUS-AWAKE-CAPABILITY-FULL-CAPABILITY-INVENTORY` 同名系列。
  ⚠️ 这三份的「当前状态」列**停在 2026-08-28**，之后无人更新；且矩阵 §10.5 明文：`contract_locked` **不表示** E2–E5 已通过。
- 本线的逐项校准（09-15 出具）：`AWAKE/docs/AUDIT-MARCUS-CAPABILITY-LIVENESS-20260915.md`（提交 `f513222`，同日同步 F-015 状态于 `5071b11`）。
- 规模、接口名、装配点均为**本次现查**（`grep`/`sed` 直读源码），不是转述。
- 「调用点数」一律来自 `grep -c`，是**引用行数不是执行次数**，只用于判"有没有人碰"，不能当覆盖度。

---

## 2. Marcus 框架在本仓库的实际形态

两进程架构：**游戏进程内**（`Awake.dll`，net472）→ 框架客户端 → **私有命名管道** → **游戏外的常驻服务进程**（net8）→ SQLite / 供应商适配。

| 模块 | 目标框架 | .cs | 行数 | 角色 |
| --- | --- | --- | --- | --- |
| `MarcusAwakeTransport` | netstandard2.0 | 12 | 3,889 | 协议契约（跨两侧共用） |
| `MarcusAwakeProvider` | net8.0 | 26 | 5,374 | 供应商适配（含 4 家平台的 API 形态） |
| `MarcusAwakeStorage` | net8.0 | 5 | 1,797 | SQLite + FTS5；**检索的真实现** |
| `MarcusAwakeFramework` | net472 | 92 | 12,610 | **嵌进游戏**的那层；IPC 客户端与 Host |
| `MarcusAwakeRuntimeService` | net8.0-windows | 18 | 13,062 | 常驻服务进程（可执行） |
| **合计** | | **153** | **36,732** | |

> 命名说明（甲方 09-15 已澄清）：`MarcusAwake*` 是**对 Marcus 的署名/致谢**，不是"依赖外部框架"的标记。里面的代码有他的、也有我们的。**改名不是议题。**

---

## 3. 框架暴露的契约接口面（37 个）

`MarcusAwakeFramework/src/` 下 `public interface I*` 全量：

```
IAiGateway              IAiModelService         IAiTaskHandle
IAssetService           ICapabilityBroker       ICommandAdapter
ICommandService         IContextPlanner         IContextProvider
IContextService         IDiagnosticsService     IEgressPolicy
IEventService           IEventSubscription      IExtensionRegistration
IFrameworkExtension     IGameDataService        IKeyValueStore
ILoggingService         IMarcusAiFrameworkHost  IMarcusAwakeFrameworkHost
IMediaService           IPermissionGate         IPermissionService
IPlayerSnapshotProvider IPromptRegistry         IPromptService
IProviderRuntimePort    IRagService             IRawSqlSession
IReadOnlySqlSession     IRouteProfileResolver   IRuntimeServicePort
ISaveAnchorStore        ISessionCoordinator     IStorageService
IToolCandidateService
```

### 3.1 装配事实（最关键的一张底牌）

`MarcusAwakeFramework/src/HostApi.cs:74-86`（`FrameworkHost` 构造函数）的实际赋值：

```
Runtime      = runtime ?? new StrictUnavailableRuntimeServicePort();
tools        = new HostToolCandidateService();          ← 真件
rag          = overrides?.Rag ?? new UnavailableRagService();   ← 有替换口
events       = new InMemoryEventService();              ← 内存版
ai           = runtime as IAiGateway ?? new UnavailableAiGateway();
models       = new UnavailableAiModelService();         ← 空壳，硬编码
media        = new UnavailableMediaService();           ← 空壳，硬编码
prompts      = overrides?.Prompts ?? new UnavailablePromptRegistry();  ← 有替换口
storage      = overrides?.Storage ?? new UnavailableStorageService();  ← 有替换口
assets       = new UnavailableAssetService();           ← 空壳，硬编码
permissions  = overrides?.Permissions ?? new UnavailablePermissionService(); ← 有替换口
log          = new UnavailableLoggingService();         ← 空壳，硬编码
```

替换口（`ServiceOverrides.cs:46-54`）**总共只有 5 个**：
`Permissions` / `Prompts` / `Storage` / `Rag` / `GameData`。

⇒ **`Models` / `Media` / `Assets` / `Log` / `Events` 这 5 面在框架里是写死的**：想接真实实现，**必须改框架源码**，没有从玩法侧注入的途径。这是本次对总控最重要的一条事实——**它决定了这 5 面是"接线活"还是"改框架活"**。

---

## 4. 我实际实现的接口（09-15，提交 `9ca5af7`）

### 4.1 结论前置

`F-015 SQLite FTS5 RAG`。**性质是「接入」，不是「迁移」**——判定依据（三处现查，非推断）：
① 服务侧早已有真实现 `MarcusAwakeStorage/src/SqliteStorageAndRagBackend.cs`（721 行，FTS5 + bm25 排序 + 访问域过滤）；
② 协议已在 `MarcusAwakeRuntimeService` 派发（`RuntimeServiceHost.cs` 处理 `rag.ingest` / `rag.search`）；
③ 参数化成空的是**客户端**——`RuntimeServiceClient.cs` 里 `grep "rag|storage"` 当时**零命中**。

### 4.2 接口层改动（可核）

| 项 | 改前 | 改后 |
| --- | --- | --- |
| `RuntimeServiceClient` 实现的接口 | `IRuntimeServicePort, IProviderRuntimePort, IAiGateway` | **＋`IRagService`** |
| 客户端能力声明 | 无 RAG | **＋`CapabilityRagRead` ＋ `CapabilityRagWrite`** |
| `AwakeHostComposition` 的 `Rag` 槽 | 空缺（原注释：「本批不接 Runtime RAG 数据面」） | **`Rag = runtimeClient`** |

`AwakeHostComposition.cs:73-75` 现行注释：
> RAG 数据面经 IPC 转发给 Runtime Service；SQLite/FTS5 与语料仍由服务侧独占。
> 服务未就绪时返回 typed unavailable，不静默降级成本地空实现。

⇒ 这是**框架设计意图的正确用法**：`RuntimeServiceClient` 本身就是"活网关"（同时充当 `IAiGateway` 与 Runtime 端口），RAG 只是同一客户端多实现一个接口。**没有新增第二份实现、没有平行分叉。**

### 4.3 线格式（我自己定的，服务侧认的就是这个）

新增 `MarcusAwakeFramework/src/StorageAndRagWire.cs`（302 行）＋ `RuntimeServiceClient.Rag.cs`（149 行）：

```
ingest schema  marcus-awake.rag.ingest.v1
search schema  marcus-awake.rag.search.v1
result schema  marcus-awake.rag.result.v1
RouteId        runtime.rag          ← 存储/RAG 无 Provider 身份，用固定占位
ProviderId     runtime.storage
ProfileId      runtime.storage
限额           collection_id 256B / 单文档 64KB / 单批 16 篇 / query 8KB / 访问域 16 个
```

⚠️ **两条踩过的线格式坑（写给后续接数据面的人）**：
- **存储/RAG 的 payload 不带 `"schema"` 字段**，而供应商（provider）的 payload 带。服务侧是**白名单校验**，多写一个字段就被 `business_unknown_field:<名>` 拒。**服务侧的帧适配器是唯一真相，别照 provider 的样子抄。**
- **能力名要双写**（服务侧 Host 的名单 ＋ 客户端 `DefaultCapabilities`）。漏写客户端一侧的现象是 `runtime.capability_unavailable`，而代码看起来全对。

### 4.4 判据与验证

新增 `MarcusAwakeRuntimeService/tests/RagClientTests.cs`（221 行），**9 条端到端判据，起真服务 + 真 SQLite**，全绿：

| 判据 | 验什么 |
| --- | --- |
| F015-01 | 写入后按关键词检索能往返 |
| F015-02 | 访问域过滤真的生效 |
| F015-03 | 条数上限被遵守 |
| F015-04 | 语料指纹过期 → typed conflict（可重试） |
| F015-05 | 写入指纹冲突 → typed |
| F015-06 | 空文本文档在本地就被拒 |
| F015-07 | 超批量在本地就被拒 |
| F015-08 | 非关键词检索模式**不被静默降级** |
| F015-09 | 无战役会话时返回 typed 错误（不是未包装异常） |

**两次变异检验**（故意改坏被测代码，确认判据会红；全绿不算证据）：
- 拿掉检索模式守卫 ⇒ **F015-08 变红**（8/9 FAIL）。
- 把 payload 的 `collection_id` 改名 ⇒ **5/9 变红**，服务侧直接吐 `business_unknown_field`。
两次均精确回滚、零残留，复跑回 9/9。

### 4.5 顺带抓出并修掉的服务侧真缺陷

`RuntimeServiceHost.BuildGenericErrorPayload` 原本**按错误码猜类别**，只认 5 个码，其余一律落 `invalid_request`。
⇒ 存储/RAG 后端精心设好的 `Conflict` / `ResourceExhausted` / `Denied` **全被压平**，`retryable` 一并丢失。
（`F015-04` 第一次跑就是红的，报 `stale_corpus_category_invalid:InvalidRequest` —— 判据抓住了它。）

修法：`BuildErrorResponse` / `BuildGenericErrorPayload` 加可选 typed error，有 typed 时用真实类别与 retryable；**其余调用点保持原启发式**（改动面最小），并把客户端映射表补成对称的 14 路。

> 这是"**静默丢分类**"的模板：`if (code == ...) return ...; else return "invalid_request";`

### 4.6 我自己堵掉的三类坑（都在客户端侧，因为服务侧不会替我们兜）

| 坑 | 若不堵的后果 |
| --- | --- |
| 错误码二次拼前缀 | 服务侧已带域前缀 ⇒ 会产出 `rag.rag.index_stale` |
| 检索模式静默降级 | `Hybrid`/`Semantic` 请求被当 `Keyword` 回答，**返回看似正确的错答案** |
| 非战役会话抛未包装异常 | 调用方拿到裸异常而非 typed 错误，无法分类处理 |

---

## 5. 当前「接通」全景（给总控做方向判断用）

口径见 §1。**"通"= 契约＋实现＋玩法侧真调用三样都在。**

### 5.1 通的（代码层）

| 能力面 | 玩法侧调用 | 框架侧实现 |
| --- | --- | --- |
| AI 网关 | 2 处 | 由 Runtime 客户端充当 |
| 会话生命周期 | 5 处 | 真件 |
| 权限 | 2 处 | 玩法侧自填 |
| 命令 | 2 处 | 真件 |
| 提示词 | 3 处 | 玩法侧自填 |
| 存储 | 2 处 | 玩法侧自填 |
| **检索 RAG** | **3 处** | **09-15 接通（本报告 §4）** |
| 诊断 / Runtime / GameData / Context | 9 / 10 / 5 / 2 处 | 真件 |

### 5.2 没接上的（这是方向判断的真正标的）

| 面 | 玩法侧调用 | 框架侧 | 性质 |
| --- | --- | --- | --- |
| 工具候选（Tool Candidate） | **0** | 真件 `HostToolCandidateService` | **接线活**：真件在，只是没人调 |
| 事件（Event） | 1 处 | `InMemoryEventService` | **半通**：调用点用了 `Durable`，背后是内存，落盘/spool 未接 ⇒ **改框架活** |
| 存档锚点（Save Anchor） | 0 | 空壳 | **split**：框架锚点未接，玩法侧走游戏原生 `SyncData` 自存（可用） |
| 模型清单（Models） | **0** | **空壳，无替换口** | **改框架活** |
| 媒体（Media：出图/配音） | **0** | **空壳，无替换口** | **改框架活**。注：供应商适配层的生图面**片 1 已实装**，断在"框架→玩法"接线 |
| 资产库（Assets） | **0** | **空壳，无替换口** | **改框架活** |
| 日志（Log） | 0 | **空壳，无替换口** | 玩法侧另写 `Awake.log` 绕开 |
| 能力协商（Capabilities） | 0 | 真件 | 有真件，没人调 |

### 5.3 一批"真件在、但玩法侧如何消费未取证"

`F-010` 稳定身份 / `F-011` 数据可见性范围 / `F-012` 快照一致性 / `F-020` 能力上报 / `F-027` 流式与取消 / `F-043` 会话失效 / `F-048` 故障收缩 / `F-058` 背压熔断。
⇒ 这些**不要当成"已通"**，也不该当成"没写"。**是本次未逐行取证。**

---

## 6. 未取证 / 本线未做（诚实标注）

1. **游戏内零验证。** 本线（模组主体代码线）至今**一次都没进过游戏**。上述所有"通"都**只在离线验台里成立**。
   ⇒ 按项目现行口径：**离线全绿不算过版**，过版判据一律以游戏内为准。
2. **08-24 三份权威文档里 `F-015` 仍记 `planned`**，与实现不符。**未同步**——不在本线写权内（本线只改了自家那份校准台账）。
3. `§5.3` 那批条目未逐行取证；`待验证` 不等于 `available`。
4. **另一条正在途的改动**：`AWAKE/src/AwakeRuntime.cs` 已被他线暂存 123 行（改的是世界状态 readiness 路径）。**本轮一次都没碰。**

---

## 7. 待总控裁决（本线只摆选项，不替总控定）

### 7.1 三条腿排哪条

| 选项 | 是什么 | 成本性质 |
| --- | --- | --- |
| **A. 工具候选（F-031）** | 真件已在，玩法侧零调用 | **纯接线**，最低成本 |
| **B. 事件落盘（F-033）** | 调用点已写 `Durable`，背后是内存 | 需动框架（spool/落盘） |
| **C. 资产与生图（F-049/F-050）** | 供应商层片 1 已实装，服务层未接；且 `Assets/Media` **无替换口** | **需动框架**（新增注入口＋服务层） |

### 7.2 一个必须由总控拍的结构性问题

`Models` / `Media` / `Assets` / `Log` 四面**没有替换口**。要让它们从空壳变真件，只有两条路：
- **① 给 `FrameworkServiceOverrides` 加槽**（改框架的一处装配，最小改动，符合现有 5 槽的设计模式）；
- **② 保持不注入，直接在框架内实现**（改动更大，但少一层间接）。

**本线倾向 ①**（与已有 5 槽同构、不新建平行实现），**但不擅自决定**。

### 7.3 一处残留冲突（已记录，未处理）

矩阵 §7 把 **生图** 定为 `A2`「不是第一版核心」，ownership map 记 `F-024` / `F-050` 为 `deferred`；
而甲方 09-15 口径是「把生图做回去」、代码已越过该定级实装片 1。
⇒ **需在矩阵 §3.1 / §7 与 ownership map 对应行补一笔留痕**，说明是甲方决策而非越权扩写。**本线未动权威文档。**

### 7.4 一条方法论建议

`§1` 那三份清单的「当前状态」列**必然停更**——手工维护的状态列一定会烂。
可行替代：**让状态从证据里长出来**（每个能力记录"去哪找证据"的三个锚点，由探针跑、自动出状态），而不是靠人记。
⇒ 否则三个月后会再来一次"三份文档说一套、代码是另一套"。

---

## 8. 本次交付清单（可核）

| 提交 | 内容 | 规模 |
| --- | --- | --- |
| `f513222` | A0 档能力「存活」校准台账 | 新增 137 行 |
| `9ca5af7` | F-015 RAG 客户端接入 ＋ 端到端判据 | 7 文件 / +724 −20 |
| `0092b13` | 修 G3-S0 证据路径（旧工作区已不存在，导致离线验台断在第 19 条，**后面 57 条判据从未执行过**） | 1 文件 / +28 −14 |
| `5071b11` | 校准台账同步 F-015 状态（blocked → 代码层已通） | 1 文件 / +12 −6 |

**当前离线基线**：主构建 0 警 0 错；RAG 端到端 9/9；框架主验台 19/19；框架测试 12/12；含玩法侧通编的离线烟测 31/31。
**离线验台整体**：修好路径后跑全链 = **77 条通过**；另有 6 条失败，**全部属他线**（5 条 persona/角色卡 + 1 条世界书旧红测要求 `awake.worldbook.v2` 而已定案形态是 `registry.v1`）。

---

## 9. 一条待认领的小事（不影响方向判断）

`MarcusAwakeRuntimeService/tests/MarcusAwakeRuntimeService.Tests.csproj` 第 25 行 `<Compile Include="RagClientTests.cs" />`
是新增验台的编译登记，**目前只在工作区、未提交**（该文件的暂存区归他线的 build-config 批量改动）。
⇒ **缺这一行，别人新检出时新验台不参与编译。** 甲方 09-15 已明确"那就不管"，故仅登记在案。
