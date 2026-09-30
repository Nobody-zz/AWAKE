# REVIEW — AWAKE 总设计评审

> 日期：2026-10-01　作者：巡检会话
> 性质：**只读评审**。本文件不预设任何已完成的实现，也不改变任何代码。
> 证据标注：**【实测】**＝本次亲自复现；**【审计】**＝代码级审计（逐文件阅读／IL 分析，审计方已核，本方未逐条复验）；**【未验】**＝需真机才能判定。
> 关联：`PLAN-REPAIR-WORLDBOOK-HASH-AND-FOUR-DEFECTS-20261001.md`（同批产出的修复计划）。

---

## 一、它想建成什么

**一句话设计主张：让游戏进程保持"哑"和"不阻塞"**——把所有危险与慢的东西赶出游戏进程，再用一条经过认证的管道接回来。

### 1.1 四层归属（跨仓库、跨进程）【审计】

| 层 | 唯一拥有 | 明确不拥有 |
|---|---|---|
| **Framework Core**（游戏内 `MarcusAwakeFramework.dll`） | Host、SessionLease、PermissionGate、Context/Command、存档锚点、IPC Client、typed result | Provider HTTP、SQLite/FTS5、Embedding/Rerank、凭据明文、服务物理库 |
| **Runtime Service**（独立 OS 进程） | Provider、SQLite/FTS5、Embedding/Rerank、Timeline、CredentialBroker、ExportPolicyEvaluator、EgressBroker、IPC Server | Bannerlord 实时对象、原版事实、玩家配置 UI、AWAKE 语义 |
| **AWAKE**（`src/`） | Bannerlord 事实适配、世界书语义、NPC 知识权限、关系/事件/周报、MCM 入口、游戏侧结算 | Framework 权限第二实现、Provider 凭据、RAG 物理库、任意网络出口 |
| **DevTools / SDK** | 作者维护、FakeHost、Analyzer、脱敏诊断 | 玩家运行时依赖、明文 Key、外发绕过路径 |

出处：`MARCUS-AWAKE-P1.5-CONTRACT-LOCK-DRAFT-20260824.md:24-31`；目录意图 `MARCUS-AWAKE-FUNCTION-SPLIT-v1-20260824.md:150-172`。

### 1.2 边界铁律

> **No API key, authorization header, database path, full endpoint URL or raw provider payload crosses the game-facing Framework API.**
> —— `MARCUS-AWAKE-P3B-IPC-HANDOFF-CONTRACT-20260826.md:12`

配套的 IPC 契约是认真的：`parent_start_proof`（不是显示名）、`connection_epoch`（重连即作废旧围栏）、`direction_nonce`（复用即拒）、能力未知 **fail closed**、`sha256` 校验、四义 ACK（`accepted`/`duplicate`/`retryable_reject`/`terminal_replay`/`rejected`）。

### 1.3 外发链：三道门，单调收窄【审计】

```
effective_context = WorldbookKnowledgeSet
                  ∩ FrameworkPermissionDecision.granted_fields
                  ∩ ExportGateDecision.allowed_field_ids
```

① AWAKE 世界书知识策略（**只能删候选**）→ ② Framework `PermissionGate`（唯一权限权威，未知 fail closed）→ ③ `ExportPolicyEvaluator`/`EgressBroker`（**只能拒绝或裁剪，不能加回**）。且 `allowed_field_ids=[]` **永远表示全拒**。
出处：`MARCUS-AWAKE-P1.5-CONTRACT-LOCK-DRAFT-20260824.md:645-651,670-675`。

### 1.4 数据权威【审计】

原版事实权威属 Bannerlord 运行时；AWAKE 自定义状态由 AWAKE 拥有；**AI 输出永远是临时提案，不拥有任何事实或持久状态**；世界事件记录必须持久化。
出处：`AWAKE-WORLD-RUNTIME-CONTROL-RULES-20260827.md:64-83`。

---

## 二、它做得好的三处（可验证，不是客套）

### 2.1 进程边界由**构建图**强制，不靠自觉【实测】

实测工程引用闭包：

```
Transport        -> (无)
Framework        -> Transport
Provider         -> Transport
Storage          -> Framework
RuntimeService   -> Framework, Storage, Transport
```

游戏侧闭包 = `{Awake, MarcusAwakeFramework, MarcusAwakeTransport}`；三者对 `Provider` / `Storage` / `RuntimeService` 的提及数 = **0**。
⇒ 违反边界会**编译不过**。绝大多数宣称"分层"的项目做不到这一点。

### 2.2 权限链是**可合成的代数**

三道门各自只能做减法、合取式、空集即全拒 ⇒ 越权外发在结构上不可能，而非靠调用点自觉。这是全案最漂亮的设计。

### 2.3 「自建降级 + 写明回家路线」的纪律真的被执行了

`AwakeImageClient.cs:35` 在游戏进程自建 `HttpClient`、`AwakeImageSecretStore.cs:82` 自建 DPAPI 密钥库。但 `AGENTS.md:42` 把 **Media/Assets 明列为「未启用服务」**并允许「自建降级」，而 `PLAN-IMAGE-PORT-TO-FRAMEWORK-20260915.md` 正是规则要求的隔离范围与迁移路径文档，含逐项 port 映射表、三条分片、以及一条只有真跑过才知道的细节（`:71`：`data:image/jpeg;base64,` 这 20 个字符**恰好全是 base64 合法字符**，不剥前缀会多解出 15 字节垃圾顶在文件头）。

---

## 三、八条治理轴的实测判定

规则出处：`AWAKE/AGENTS.md:84-94`。**4 轴违规 / 2 轴干净 / 2 轴轻微。**

| 轴 | 判定 | 关键证据 |
|---|---|---|
| 1 游戏数据读取 | ⚠️ **真违规，一半无解** | 45 处 `Campaign.Current`：27 处瞬时守卫（可辩）、**13 处域数据读取**、5 处假阳性；长期持有见 `SceneDialoguePreview.cs:12-13`。**`IGameDataService` 只有主角快照，NPC 名册无框架出口** |
| 2 战役状态只走 Storage | ⚠️ **真违规** | ModuleData 干净（0 写入）✅；但 2 处第二持久化面：`AwakeTerminalBehavior.cs:68-83`（世界书 overlay + activation 写存档）、`AwakeEventBehavior.cs:34-35`（`awake_last_weekly_report_day` + persona 锚点） |
| 3 AI 只走 Route + Schema | 🔴 **最严重** | 全仓**唯一**一处 `new HttpClient(` + **唯一**一处 `Authorization` 头，见 §四 |
| 4 效果只走 Command | ✅ **未发现绕过** | `WorldCommandBridge.cs:32-197` 权限 → Preflight → Submit → Drain 完整；幂等键由 `:211-228` 派生。1 处可辩：`AwakeWorldCommandAdapters.cs:604-605` 返回 `Succeeded` 时尚未扣款（扣款在 `AwakeGoldSettlementService.cs:245`，tick 驱动） |
| 5 事件只走 EventService | ✅ **调用点全干净** | 统一在 `WorldStateStore.cs:2502` `Publish(..., EventDelivery.Durable, ...)`；5 个 `CampaignEvents` 订阅只采集事实不改状态 |
| 6 错误按 Code 分支 | ✅ 0 处文本解析 | 1 处软违规：`BannerlordNativeSocialReader.cs:51-62,118-120` **10 处裸 `catch {}`** 吞掉原生关系读取，无日志无 correlation ⇒ 下游分不清「读不到」与「读到了 0」 |
| 7 异步/不阻塞 | 🔴 **真违规，系统性** | 2 处真实阻塞（`AwakeWorldKnowledgeSemanticIndex.cs:127` 1500ms、`:149` **120000ms**）；**+ 一整类假异步同步 IO**，见 §五 |
| 8 权限 | ✅ **完全干净** | 0 处硬编码权限串；未知权限 fail closed（`PermissionGate.cs:54-57,110-113`）；取消映射 `awake.cancelled`；`Evaluate`/`EnsureAsync` 分工正确 |

> 权限与命令这两条**最难**的轴反而最干净 ⇒ 这个项目不是不会做治理，**是只在有框架端口的地方才做得成**。

---

## 四、最严重的单点：图像路径绕过的，正是 §1.3 那道门

**框架已有正确抽象**：`IMarcusAiFrameworkHost.Media` → `IMediaService.GenerateImageAsync(ImageGenerationRequest, ...)`；`ImageGenerationRequest` 自带 `routeId` + `deadlineUtc` + `idempotencyKey` + `cloudExportClassification`，返回 `GeneratedAssetResult`（含 `AssetHandle`）。
**但它是空壳**：`HostApi.cs:81` `media = new UnavailableMediaService();`。

于是 AWAKE 自建整套平行实现，**同时绕开五个治理面**：

| 被绕过的面 | 后果 |
|---|---|
| Route | 无路由权限、无 Provider 路由与降级、无 `AwakeTokenUsage` 记账 |
| Output Schema | 框架无法校验产出 |
| **`CloudExportPolicy`** | **含 NPC 名与 persona 派生文本的提示词，不经云外发权限门就离开本机** |
| `AssetHandle` / CAS | 产出以裸文件散落 `PlayerExports/`，无账可查、无引用安全删除 |
| 凭据面 | 模组侧第二套 DPAPI 存储（`AwakeImageSecretStore.cs:12-14` 自陈框架读不到）⇒ **框架无法枚举、无法撤销** |

**阻塞面是干净的**（三处调用都在 `AwakeBackgroundTask.Run` 内、带 120s CT、结果经 `AwakeUiDispatcher.Enqueue` 回主线程）——违规只在「出网 + 持钥 + 无治理」。

**公平判定**：这是 `AGENTS.md:42` 允许的降级，迁移计划也已写、且自估成本「1 槽 1 行」。**但它付出的代价远超登记**：全案最强的保证（§1.3 的单调收窄代数）被唯一真正出网的路径整个跳过。
**真正的风险是可复制性**：本项目**自己写下过这条禁令**（`PLAN-MARCUS-AWAKE-P3D-A2-STREAMING-FALLBACK-20260828.md:150`「在游戏进程直接 HTTP/SSE …… **拒绝**」），随后为生图又做了一遍。于是「框架端口是空壳时，模组进程可以自己开 socket」成了一条既有先例。

---

## 五、第二严重：「异步存储」是假的，tick 线程上在做磁盘写入

`AwakeFileStorageService.JsonFileKeyValueStore.Save()`（`:233-249`）是**同步文件 IO**（`File.WriteAllText` + `File.Replace`），包在 `Task.FromResult` 里伪装成异步。完整可达链，**全程无线程切换**（`SemaphoreSlim.WaitAsync` 无竞争即同步完成）：

```
SubModule.cs:104  OnApplicationTick
  → AwakeTerminalBehavior.cs:100  AwakeGoldSettlementService.Tick()
  → :209/:224  PersistPhaseAsync
  → WorldStateStore.cs:2315  DrainAsync
  → :2430  TryApplyAsync  → :2832  store.SetAsync(...)   ← 磁盘写入落在 tick 线程
```

同类：`NpcDialogueVM.cs:550` → `AwakePortraitCache.cs:140`（主线程写整张 PNG）；`AwakeLog.cs:26-47`（**每行日志**做 6 级目录遍历 + `CreateDirectory` + `AppendAllText`，tick 路径高频调用；日志器惯例可辩）。

⇒ `AGENTS.md:91`「UI/campaign tick 中不得阻塞网络、文件或数据库操作」在**最热路径上被打破**。

---

## 六、设计层的根因：**端口面 < 不变量集**

三条独立证据，三种不同的窄化方向：

| 窄化方向 | 具体 | 被迫后果 |
|---|---|---|
| **host override 槽不足** | `FrameworkServiceOverrides` 只有 5 槽（`Permissions/Prompts/Storage/GameData/Rag`）；`HostApi.cs:76-86` 里 Media / Assets / Models / Log / SaveAnchors **硬编码空壳、无槽**；`events = new InMemoryEventService()` **硬编码** | 生图自建、日志绕开、存档锚点绕开、**事件静默不持久** |
| **client port 不足** | 服务端有 KV（`SqliteStorageAndRagBackend.cs:702`），`RuntimeServiceClient` 只暴露 RAG | 自建文件 KV ⇒ **§五假异步的根源** |
| **data port 不足** | `IGameDataService` 只有主角快照（合法调用点全仓 3 处） | 13 处 NPC 名册读取**无路可走** |

**凡落在缺口上的不变量，只有三种结局**：**自建**（生图）、**绕开**（日志／存档锚点）、**静默违约**（事件持久性、tick 阻塞）。

### 6.1 「名字停在名义」的六个切面

| 词汇 | 字面承诺 | 实际保证 |
|---|---|---|
| `CampaignSessionReady` | 战役会话已就绪 | 与 `Starting` **相邻两行**发出（`AwakeHostComposition.cs:189-190`），零间隙 |
| `EventDelivery.Durable` | 事件持久投递 | 背后 `InMemoryEventService`（`HostApi.cs:78`） |
| `Host.Media`/`Assets`/`Models` | 框架服务面 | `UnavailableXxxService` 空壳 |
| `Host.Log`/`SaveAnchors` | 框架服务面 | 空壳；玩法侧绕开 |
| `maf-lint` "0 blocking" | 静态门 | 脚本恒定 `exit 0`（`maf-lint.ps1:48`），**永不可能失败** |
| `DEPLOY_VALIDATE_OK` | 投送校验通过 | 只比对哈希字符串，不重算（`deploy_worldbook_to_game.ps1:82`） |

**注意区分层次**：事件那条**调用点是忠实的**（`WorldStateStore.cs:2502` 老实传了 `Durable`），不忠实的是**框架实现**。⇒ AWAKE 侧代码基本诚实，**它是在一个不会兑现承诺的地基上写的**。

### 6.2 一个具体实例

世界书 `contentHash` 跨组件口径不一致（运行时 `\"` vs Studio `\u0022`），导致**当前投送到游戏目录的 800 档世界书被运行时拒绝加载**、且**静默失败**（`worldbook_runtime_init_error` → 状态显示「世界书未加载」）。根因、完备性证明与已验证的修复见 `PLAN-REPAIR-WORLDBOOK-HASH-AND-FOUR-DEFECTS-20261001.md` §2。**这正是"校验器结构上测不出它该测的东西"的第七个实例。**

---

## 七、第三个结构问题：宏观被强制，微观为零

| 维度 | 判定 | 证据 |
|---|---|---|
| **宏观**（进程／仓库边界） | ✅ **构建图强制** | 见 §2.1 |
| **微观**（`src/` 内部） | ❌ **完全无约束** | **122/166 文件（73%）塌成一个强连通簇、560 条内部边**；全部 `internal` + 单命名空间 ⇒ ViewModel 伸手进存储，**没有任何东西会失败** |
| `WorldStateStore.cs` | ❌ **上帝对象** | 4,401 行、单类 `:261-3779`、**168 成员**、**18 个世界状态域**，同时兼任 namespace 注册表 + write-behind 队列 + 幂等账本 + JSON schema 强转库 + 持久化适配器 |
| 契约 id 纪律 | ❌ **只在一个子系统执行** | `AGENTS.md:92` 要求权限「不得各写一套字符串」并为此建了 `PermissionCatalog` ✅；但 `src/` 里有 **748 个 `"awake.*"` 字面量、524 个不同值**（`awake.world_report.v2` ×29 …） |
| 可测性 | ⚠️ 结构配不上 | 只有 **7 个 interface** vs 334 个类声明；**132 个 static class、118 个 static 字段**；`AwakeRuntime` 是 `internal static class`，被 **42 个文件引用 323 次** |

### 7.1 因果链（这是全案的答案）

**离线套件测了 74% 的纯逻辑，而四个缺陷 100% 长在那 26% 的接线/时序/闸门层里**【实测】：

| 缺陷所在文件 | 在离线套件里？ |
|---|---|
| `AwakeHostComposition.cs`（缺陷 2/4 时序根） | **否** |
| `SubModule.cs`（`OnGameStart` 入口 + 缺陷 3 入队点） | **否** |
| `AwakeEventEngine.cs`（缺陷 3 另一入队点） | **否** |
| `NpcDialogueLauncher.cs`（缺陷 3 的 ID 形状闸） | **否** |
| `NpcDialogueService.cs` / `NpcDialogueOverlay.cs`（缺陷 3 消费端与症状） | **否** |
| 三个 `*Behavior.cs` | **否** |
| `WorldStateStore.cs` / `AwakeFileStorageService.cs` / `ProbeExtension.cs` | 是 |

**这不是巧合，是因果**：

> **静态化 → 没有缝（7 个接口）→ 接线不可测 → 接线缺陷离线不可见 →（加上 `build.ps1` 根本不跑测试）→ 缺陷活几周。**

⇒ 回到最初的问题「他们干的都不怎么样」：**结构上不是不认真，是这个结构看不见自己的失败。**

---

## 八、总评

> **AWAKE 的问题不是设计差，是它站在一个比自己的规则更窄的地基上。**
>
> 它的**宏观边界**（进程、仓库、权限链）做得比绝大多数模组好，**微观却放任**（73% 一个簇、132 个静态类、7 个接口）；
> 它的治理规则要求担保 11 个服务面，而框架只给了 **5 个可替换槽 + 3 个不完整的客户端端口**；
> 于是凡落在缺口上的不变量，都退化成"一句名字"——而**没有任何机制去比对名字与实现**。

**一句话版本**：**凡是把契约写成"可合成的代数"的地方都很稳，凡是把契约写成"一句名字"的地方都在烂；它缺的那道门，恰恰是比对名字与实现的门。**

---

## 九、行动清单（按性价比）

| # | 动作 | 治什么 | 成本 | 验收判据 |
|---|---|---|---|---|
| **1** | 修世界书哈希一行 + 把 `SdkSmoke` 挂进 `build.ps1` | 让 800 档内容真能加载；**让门能失败** | 小 | 改前 `build.ps1` 必须红、改后绿（红→绿一对）；`SdkSmoke` 67/67 |
| **2** | 建 `check-layers.ps1` + `src/runtime/`、`src/ui/` 两目录 | 给"看不见失败"的结构装第一只眼睛 | 小（**不动一行逻辑**） | 脚本能对 `runtime → ui` 的边报错；7 个漏出的文件归位 |
| **3** | 拆分 `CampaignSessionReady` → 增 `CampaignWorldQueryable` | 缺陷 1/2/4 + `runtime_not_ready` + 语义臂重试的共同根 | 中 | 冷启后 `PlayerExports\AwakeState\` 不出现 `unbound\`；`native_readiness` 不再 Failed；journal 不再孤儿 |
| **4** | **拓宽 port/override 面**，或把缺口上的不变量**显式降级并登记欠账** | "名字停在名义"的根 | 中 | 二选一：① `Events`/`Media`/`Assets`/KV/`GameData` 有对应接口；② 宪法标注为"目标" + 欠账表逐条登记 |

**第 4 项必须二选一**：要么补接口，要么降级登记。**保持现状是最坏的选项**——它让每一条规则都变成可选的。

**补充（低成本、独立）**：
- `WorldStateStore.cs` 抽四段纯静态（`:2874-3086`、`:3087-3647`、`:3832-4242`、`:4243-4379`，约 1000 行）→ `WorldStateApply.cs`：**零行为变化，立刻可测**
- 把 748 个 `"awake.*"` 契约 id 收进一张表（照 `PermissionCatalog` 的样子）
- `BannerlordNativeSocialReader.cs` 那 10 处裸 `catch {}` 补日志与 correlation id
- 修 `AwakeFileStorageService` 的假异步（或至少在 tick 路径上改为真异步）

---

## 十、本评审未做的事

- **未修改任何代码、配置、游戏目录；未提交任何东西**
- §三 的 4 轴违规来自代码级审计，**本方未逐条复验**（§二 §六 §七 的关键结论已亲自复验）
- 所有结论**均未在真机复现**；§九 第 1/3 项的 E4 判据必须由用户进游戏后才能判
- 本文件是**新增未跟踪文件**；是否入库由用户决定
