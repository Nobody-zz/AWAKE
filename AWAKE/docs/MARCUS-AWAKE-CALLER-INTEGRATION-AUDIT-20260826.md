# Marcus-AWAKE Caller Integration Audit

> 审查目标：只读核对 `AWAKE/src/ProbeExtension.cs`、`AWAKE/src/SubModule.cs`、`AWAKE/src/AwakeRuntime.cs` 的生命周期接线缺口，并与当前工作区内置 Marcus-Awake Framework Host API 对照。
>
> 报告文件名按用户指定保留 `20260826`；实际审查日期为 **2026-08-27**（Asia/Shanghai）。本轮未启动 Bannerlord、未执行构建或测试、未同步文件、未修改代码/既有状态文件，也未读取或修改 P3B 文件。以下结论以静态源码和只读文件元数据为证，明确区分未验证的游戏行为。

## 结论摘要

**当前 AWAKE 不能静态证明已完成“Framework Host → Session → `AwakeExtension.OnLifecycle` → AWAKE 业务”的真实接线。** 现状是：扩展声明、注册体和生命周期处理体都存在，但 caller、Host 类型桥、真实 `SessionLease`、关闭排空和可观察闭环均有缺口。

最高优先级阻塞项如下：

1. `AwakeExtension.OnLifecycle` 在 AWAKE 源码内没有调用者；`SubModule` 只登记扩展，不证明 Framework 会实际调度 `CampaignSessionReady` 或 `SessionEnding`。
2. `SubModule.OnSubModuleLoad` 调用的 `FrameworkHostLocator.Register(IFrameworkExtension)`，在当前内置 `HostApi.cs` 中固定返回 `host.unavailable`；它不是 `FrameworkHost.Register()` → `Ready()` → `FrameworkHostLocator.Register(host)` 的真实 Host 注册路径。
3. AWAKE 使用旧的 `IMarcusAiFrameworkHost` 兼容接口，而当前内置 Host 实现保存的是 `IMarcusAwakeFrameworkHost`；当前源码没有 Host 类型适配器。`TryGetHost` 只是把当前 Host 强制转换为旧接口。
4. `AwakeRuntime.CreateContext` 从 `CurrentSession` 构造兼容租约，而不是绑定真实 `SessionLease`；AWAKE 没有保存或消费 Framework `ISessionCoordinator` 的会话租约。
5. `SessionEnding` 中存在 `GetAwaiter().GetResult()` 阻塞等待，且最终排空是未跟踪的 continuation；没有完成 Framework session drain，也没有明确的 Host unload/locator 清理链。

因此，本报告的静态结论为：**NOT READY / 未完成 caller integration**。在真实 Host/程序集契约未统一前，MCM/GUI 应继续显示 `Offline (degraded)` 或 `session:not_ready`，不得把现有状态文字解释为已连接。

## 证据边界

### 已确认的静态证据（E0）

- `SubModule` 的加载、战役启动和应用 tick 入口存在，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\SubModule.cs:21`、`:45`、`:79`。
- `AwakeExtension` 声明 `IFrameworkExtension`、Manifest、capability、context provider 和 commands，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:78`、`:83`、`:95`。
- `OnLifecycle` 实现了 `CampaignSessionReady`、`SessionEnding`、`Unregistered` 三段 AWAKE 本地处理，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:195`、`:202`、`:254`、`:310`。
- 当前 AWAKE 源码搜索到的 `OnLifecycle` 唯一命中是上述定义；没有 AWAKE 内部 dispatcher/caller 命中。
- 当前 AWAKE 源码搜索不到 `SessionLease`、`Sessions.BeginSession`、`Sessions.BeginClosing` 或 `Sessions.CompleteDrain` 的调用。

### 仅作为参考、不能升级为游戏证据的材料

- `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\tests` 中有 Host/Session/Runtime fixture；本轮没有运行它们。
- `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\MarcusAIFramework\SubModule.xml` 静态显示外部模块版本 `v0.1.0`；其 DLL 的文件版本为 `0.1.0.0`，ProductVersion 为 `0.1.0-preview.1+bannerlord.1.3.15...`。这只证明文件存在，不证明 Bannerlord 已加载、Host 已 Ready 或 Companion 已连接。
- 历史 `_build_out`、`dist` 和游戏目录文件不作为当前 AWAKE 源码实现的权威，也不作为 E4/E5 证据。当前可见构建产物的程序集引用还存在不一致，详见“程序集/发布风险”。

## 入口 → 调用 → 结算 → 可观察结果

### 当前静态链路

| 阶段 | 当前入口/调用 | 静态结果 | 证据边界 |
|---|---|---|---|
| 入口 | Bannerlord 通过 `SubModule.xml` 创建 `Awake.SubModule`；`OnSubModuleLoad` 新建 `AwakeExtension` 并调用 `FrameworkHostLocator.Register(_extension)` | 有登记动作，但使用的是兼容 overload | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\SubModule.cs:21`、`:31`、`:32`；`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\SubModule.xml:14`、`:24` |
| 战役入口 | `OnGameStart` 注册 `AwakeTerminalBehavior`、`AwakeEncounterBehavior`、`AwakeEventBehavior`，并绑定 UI/主动行为 hook | 有 Bannerlord 行为接线；没有 Host session 接线 | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\SubModule.cs:45`、`:50`、`:65` |
| Framework caller | Framework 应调用 `AwakeExtension.OnLifecycle(stage, session)` | AWAKE 内部无 caller；外部 DLL dispatcher 未由本轮游戏或可读源码验证 | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:195` |
| Ready 初始化 | `CampaignSessionReady` 中 reset、native readiness、store load、memory/worldbook/proactive 初始化 | 处理体存在，但是否被调度、是否绑定真实 session 未证实 | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:202`、`:204`、`:223`、`:242`、`:250` |
| 业务调用 | 下游服务通过 `AwakeRuntime.ResolveHost()` 调用 `Ai`、`Storage`、`Rag`、`Commands`、`Prompts` 等 | 只有在 locator 返回兼容 Host 且 context 有效时才可能继续；当前 context 有空/兼容租约 fallback | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:95`、`:108` |
| 结算/持久化 | `WorldStateStore`、`AiTaskGateway`、`WorldCommandBridge` 负责存储、AI task 和命令桥 | 代码路径存在，但没有真实 Framework session lease 的静态证明，不能宣称完成 settlement | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:360`；下游调用需以实际 Host/Session 为前提 |
| 可观察结果 | 注册日志、`AwakeProbe.log`、MCM AI 状态、Developer Check 报告 | 可以观察“失败/降级/未就绪”；不能由静态源码证明成功链 | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\SubModule.cs:27`、`:35`、`:39`；`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeMarcusLinkService.cs:15`、`:18`；`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeDeveloperReport.cs:18`、`:19` |

### 目标最小闭环

目标链路应当是：

```text
Bannerlord load
  -> Framework-owned Host Register/Ready
  -> FrameworkHostLocator publishes the ready Host
  -> AWAKE extension registration succeeds
  -> safe campaign point creates stable SessionRef
  -> host.Sessions.BeginSession() returns SessionLease
  -> Framework dispatches CampaignSessionReady
  -> AWAKE stores the lease and creates lease-bound RequestContext
  -> player/system entry calls AI/RAG/Storage/Command service
  -> typed result / command receipt / durable storage settlement
  -> AWAKE log + MCM status + Developer Check show the same session/result
```

关闭链应分离“战役 session 关闭”和“整个 Framework Host 卸载”：

```text
SessionEnding
  -> AWAKE stops new work and closes UI intent
  -> host.Sessions.BeginClosing(sessionRef) cancels SessionLease token
  -> bounded background drain completes
  -> host.Sessions.CompleteDrain(sessionRef, closingGeneration)
  -> next campaign may reuse the Ready Host

Framework unload / Unregistered
  -> no active session remains
  -> Host.BeginClosing()
  -> Host.CompleteDrain()
  -> FrameworkHostLocator.Clear(host)
```

`SessionEnding` 不应每次都关闭全局 Host；如果外部 Framework 采用不同的所有权策略，必须在公共契约中明确，而不能由 AWAKE 的本地静态 flag 推断。

## 具体缺口

### F-01 / P0：`OnLifecycle` 存在但没有已证实的 caller

- `AwakeExtension.OnLifecycle` 是唯一实现，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:195`。
- `SubModule.OnSubModuleLoad` 只保存 `_extension` 并调用注册 overload；`OnGameStart` 只注册 Bannerlord behaviors/hooks，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\SubModule.cs:19`、`:31`、`:45`。
- AWAKE 源码没有 `OnLifecycle(...)` 调用，也没有 lifecycle dispatcher。
- 影响：`CampaignSessionReady` 里的 memory/worldbook/proactive 初始化、`SessionEnding` 清理和 probe 日志都可能永远不执行。按项目规则，“存在但未调用”按 P0 处理。
- 证据结论：只能说“外部 Framework 可能会调用”，不能说“当前已接线”。

### F-02 / P0：注册 overload 不是实际 Host 注册

- AWAKE 调用 `FrameworkHostLocator.Register(_extension)`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\SubModule.cs:32`。
- 当前内置 Host API 的真实 Host overload 要求 Host 已处于 `HostState.Ready`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\HostApi.cs:126`、`:130`、`:135`。
- 同一文件的 `Register(IFrameworkExtension)` 兼容 overload 直接返回 `host.unavailable`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\HostApi.cs:141`、`:144`。
- 正确的 Host/Session 顺序只出现在 `FrameworkBootstrap`：`host.Register()`、`host.Ready()`、locator 注册、`host.Sessions.BeginSession()`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\FrameworkBootstrap.cs:27`、`:36`、`:40`、`:44`、`:48`、`:52`。
- `FrameworkBootstrap` 是 `internal`，不能作为 AWAKE 的公开集成入口；AWAKE 当前没有调用它，也没有等价的公开 Host composition 入口。

### F-03 / P0：新旧 Host 类型没有桥

- 内置完整 Host 类型是 `IMarcusAwakeFrameworkHost`，包含 `State`、`Sessions`、`SaveAnchors`、`Diagnostics` 和 `Runtime`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\HostApi.cs:15`、`:23`、`:26`。
- AWAKE 业务使用的是 `IMarcusAiFrameworkHost` 兼容接口；该接口暴露 `CurrentSession` 和服务属性，但没有 `ISessionCoordinator` 或 `SessionLease`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\Compat\IMarcusAiFrameworkHost.cs:4`、`:8`、`:36`。
- locator 内部存储 `IMarcusAwakeFrameworkHost`，`TryGetHost` 通过 `current as IMarcusAiFrameworkHost` 返回旧接口，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\HostApi.cs:128`、`:147`、`:151`。
- `ApiCompatibilityExtensions.cs` 只提供 diagnostics、game-data、command service 的旧投影，没有 Host 对象适配，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\ApiCompatibilityExtensions.cs:8`、`:26`。
- 影响：即使有一个新接口 Host 已 Ready，也没有静态证据表明它能被 AWAKE 的旧接口路径取回。

### F-04 / P0：AWAKE 没有真实 `SessionLease`

- `AwakeRuntime.ResolveHost` 只取测试 override 或 `FrameworkHostLocator.TryGetHost`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:95`、`:103`。
- `CreateContext` 只读取 `host.CurrentSession`，缺失时创建全空 `SessionRef`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:108`、`:110`。
- `RequestContext(ExtensionId, SessionRef, ...)` 会创建 generation 为 `0` 的 compatibility lease，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\RequestContext.cs:8`、`:45`、`:47`。
- Framework 正常路径的 `SessionLease` 才拥有真实 generation、状态和取消 token，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\SessionApi.cs:16`、`:32`、`:37`。
- 影响：AWAKE 请求可能携带空 session 或独立的合成 lease；Framework 的 session stale/closing/generation fence 不能可靠约束这些请求。

### F-05 / P1：本地 session flag 没有推进 Framework session

- `ResetSessionStateForCampaign` 和 `BeginSessionEnd` 只修改 `_sessionGeneration`、`_sessionEnded`、binding task 和 world store，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:451`、`:482`。
- AWAKE 源码没有调用 `host.Sessions.BeginSession`、`BeginClosing` 或 `CompleteDrain`。
- readiness 只用本地 generation 进行 stale 判断，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:275`、`:288`、`:311`；这不能替代 Framework lease generation。
- `EnsureWorldStateReadyAsync` 使用当前 `CreateContext`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:360`、`:384`。
- `EnsureCurrentHeroBoundAsync` 的后台结果在写回 `_currentHeroId` 时只检查 `_sessionEnded`，没有比较真实 Framework session lease/generation，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:550`、`:591`。

### F-06 / P1：SessionEnding 违反非阻塞和排空契约

- `OnLifecycle(SessionEnding)` 在 `NpcMemoryService.DrainBackgroundAsync(5000)` 上调用 `GetAwaiter().GetResult()`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:254`、`:264`、`:270`。
- 同一分支随后只设置 AWAKE 本地 session end，并启动 `WorldStateStore.BeginFinalDrainAsync()` 的未跟踪 continuation，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:277`、`:288`、`:296`。
- 没有调用 Framework `Sessions.BeginClosing` 取得并传播取消 token，也没有 `Sessions.CompleteDrain` 的 generation 确认。
- 影响：若 lifecycle callback 在游戏线程执行，可能卡住主线程；若 continuation 尚未完成就进入下一 session，旧结果可能迟到；Host 也无法从 AWAKE 代码确认 session 已排空。
- `OnApplicationTick` 只能继续承担 UI dispatcher/完成通知的非阻塞泵职责，不能把网络、文件、数据库或同步 drain 塞进 tick，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\SubModule.cs:79`、`:84`。

### F-07 / P0：程序集身份和当前发布产物不一致

- AWAKE 项目引用本地 `framework\\MarcusAwakeFramework\\MarcusAwakeFramework.csproj`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\AWAKE.csproj:10`、`:25`；该项目程序集名为 `MarcusAwakeFramework`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\MarcusAwakeFramework.csproj:4`，版本属性为 `2.0.0.0`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\AssemblyInfo.cs:3`。
- `SubModule.xml` 声明的外部模块却是 `MarcusAIFramework`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\SubModule.xml:14`、`:24`；当前游戏目录文件名为 `MarcusAIFramework.dll`，静态文件版本为 `0.1.0.0`。
- 当前 `AWAKE\\_build_out\\1.3.15\\Release\\Awake.dll` 的静态字符串引用包含 `MarcusAwakeFramework`，而较旧 `AWAKE\\dist\\Modules\\AWAKE\\bin\\Win64_Shipping_Client\\Awake.dll` 的静态字符串引用包含 `MarcusAIFramework`。这些产物时间和哈希也不一致，不能当成同一候选。
- 影响：在未统一程序集名、API namespace、版本和发布策略前，可能出现程序集加载失败、重复 API 类型、locator 取不到 Host 或运行时方法签名不匹配。此项必须先于 caller 接线处理。

### F-08 / P2：MCM/GUI 是观察和人工入口，不是生命周期 bootstrap

- MCM 的 AI 状态是只读文本；路由同步、状态刷新、打开 AI 设置台、打开诊断台是按钮动作，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeConfig.cs:30`、`:44`、`:52`、`:56`、`:60`。
- `RefreshAiStatus` 只调用 `AwakeMarcusLinkService.BuildStatusText()`；`SyncRoutesAsync` 只提示并打开设置台，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeMcmActions.cs:15`、`:20`，以及 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeMarcusLinkService.cs:81`、`:88`。
- locator 没有 Host 时状态明确返回 `Offline (degraded)`；有旧接口 Host 时只根据 `CurrentSession` 显示 `session:not_ready` 或 `session:ready`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeMarcusLinkService.cs:15`、`:18`、`:66`。
- Developer Check 能显示 Host、会话、玩家和 world state，但这些是当前 AWAKE 本地投影，不是 Host session lease 的证明，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeDeveloperReport.cs:18`、`:19`、`:20`、`:21`。

## 最小安全接线清单

以下是实现阶段应采用的最小边界；本轮只审查，不执行其中任何修改。

### A. 先统一公共程序集和 Host 所有权

- [ ] 选择唯一运行时公共 API：AWAKE 必须引用与已安装 `MarcusAIFramework.dll` 相匹配的 `MarcusAIFramework.Api`，或由外部 Framework 提供一个明确的、同程序集身份的兼容 facade；不能让 `MarcusAwakeFramework.dll` 和 `MarcusAIFramework.dll` 同时作为未定义的 API 来源。
- [ ] 不在 AWAKE 内复制/自建 Provider、IPC、SQLite 或另一个全局 Host；Host、Companion 和服务 composition 由 Marcus Framework 所有。
- [ ] 由 Framework 提供公开的 Host composition/registration 入口，或者由 Framework 自己调用内部 `FrameworkBootstrap`。AWAKE 不应绕过 `internal FrameworkBootstrap`，也不应在 `OnSubModuleLoad` 自行拼装所有服务实现。

### B. 进程加载和扩展登记

- [ ] `OnSubModuleLoad` 只做轻量的扩展登记、版本/能力声明和日志；保持不访问 `Campaign.Current` 的规则。
- [ ] 使用真实 Framework 的扩展登记入口，并记录 `OperationResult` 的 code/category；登记失败时保持 `degraded/unavailable`，不要伪造 Host connected。
- [ ] Framework 侧确认顺序为 `Host.Register()` → `Host.Ready()` → `FrameworkHostLocator.Register(host)` → 扩展登记/通知 `Registered`、`FrameworkReady`。
- [ ] AWAKE 的 `OnLifecycle` 必须由 Framework dispatcher 实际调用；仅实现接口不算完成。

### C. Campaign session 和 RequestContext

- [ ] 在经版本核验的安全 Campaign 生命周期点创建稳定 `SessionRef`，不得在 `OnSubModuleLoad` 建 session。
- [ ] 调用 `host.Sessions.BeginSession(reference)`，保存返回的 `SessionLease`；唯一保留一份 session-bound runtime state。
- [ ] `AwakeRuntime.CreateContext` 改为使用该 lease 的构造路径：`caller + SessionLease + correlationId + finite deadline`。
- [ ] 所有 AWAKE AI、RAG、Storage、Command、Prompt 调用复用同一 lease 的 `CancellationToken`；取消、closing、generation conflict 按 `FrameworkError.Code/Category` 处理。
- [ ] 删除生产路径的全空 `SessionRef` fallback 和 compatibility lease fallback；没有 Ready session 时直接返回 typed unavailable/stale 结果。
- [ ] 每个后台结果除了本地代际，还要比较 `SessionRef` 和 Framework lease generation；旧 session 结果只丢弃，不写入新 session。

### D. Ready、关闭和卸载

- [ ] `CampaignSessionReady` 只启动与该 lease 绑定的 world state、NPC memory、knowledge/proactive 资源，并记录 `sessionRef`、generation 和 correlation。
- [ ] `SessionEnding` 先停止新任务，再调用 Framework session close；不要在同步 `void OnLifecycle` 中 `.Wait()`、`.Result` 或 `GetAwaiter().GetResult()`。
- [ ] 采用 bounded、可观察的后台 drain：Framework 负责等待/轮询 extension drain 状态；AWAKE 只报告完成、失败或超时，不阻塞游戏线程。
- [ ] session drain 完成后由 session owner 调用 `Sessions.CompleteDrain(reference, closingGeneration)`；不能只调用 AWAKE 的 `BeginSessionEnd()`。
- [ ] Framework Host 跨战役复用时，`SessionEnding` 不关闭全局 Host；Framework unload/`Unregistered` 时才执行 Host `BeginClosing()` → `CompleteDrain()` → `FrameworkHostLocator.Clear(host)`。
- [ ] 若当前 `void OnLifecycle` 契约无法传回异步 drain completion，必须先补充 Framework-owned non-blocking drain acknowledgement；不能以主线程同步等待掩盖契约缺口。

### E. Tick、MCM 和可观察性

- [ ] `OnApplicationTick` 只保留游戏线程 UI dispatcher、完成通知和轻量状态轮询；Host/Companion 的网络、IPC、文件、数据库工作不能放入 tick。
- [ ] MCM 的 `RefreshAiStatus`、`OpenAiSetup`、`OpenDiagnostics` 继续作为玩家可见诊断入口，不承担隐式 bootstrap。
- [ ] Developer Check 至少同时显示：Host state、session reference/session state、lease generation、AWAKE local generation、world state、最后一个 typed error code/correlation。
- [ ] 只有看到当前候选的 `register_ok`、`CampaignSessionReady`、真实 session id/generation、业务 receipt/storage result 和关闭 drain 记录，才可声称入口闭环；MCM 文本自身不构成 E4。

### 明确禁止的“看似最小修复”

- 不要只把 `Register(IFrameworkExtension)` 的失败日志改成成功。
- 不要只在 `OnGameStart` 里调用本地 reset 就把它当作 `BeginSession`。
- 不要只把 `SessionRef` 传给兼容构造函数就把它当作真实 `SessionLease`。
- 不要把 `FrameworkBootstrap` 的 internal 实现复制到 AWAKE。
- 不要在 `OnApplicationTick` 或 `SessionEnding` 中同步等待数据库、IPC、网络或 drain。

## Bannerlord v1.3.15 兼容风险

### 已满足或相对低风险的静态点

- `AWAKE.csproj` 默认 `BannerlordApi` 为 `1.3.15`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\AWAKE.csproj:8`。
- `AwakeExtension.Manifest` 的 Bannerlord 白名单包含 `1.3.15`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:83`、`:89`。
- `OnSubModuleLoad` 当前没有直接读取 `Campaign.Current`，符合轻量加载边界，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\SubModule.cs:21`。
- 本地已安装 `Native`、`SandBoxCore`、`Sandbox` 的 `SubModule.xml` 静态版本均为 `v1.3.15`；这仍不是实际游戏启动证据。

### 仍需处理的风险

1. **API/程序集绑定风险（P0）**：源码 ProjectReference 是 `MarcusAwakeFramework`，外部模块是 `MarcusAIFramework`；当前候选构建/旧 dist 还显示相反的引用方向。
2. **Preview Host 行为风险（P0）**：外部 Framework 模块静态为 `v0.1.0` / preview.1；没有本轮可读的真实 dispatcher 证据，不能假设 `OnLifecycle` 会被调用或会在预期线程调用。
3. **生命周期时机风险（P1）**：把 session 开始移动到 `OnSubModuleLoad` 会违反 Bannerlord 生命周期规则；应在 Framework 已确认的 Campaign session ready 点执行。
4. **主线程阻塞风险（P1）**：`SessionEnding` 的同步 drain 可能在 Bannerlord 主线程执行，带来卡顿、死锁或 watchdog 风险。
5. **旧结果污染风险（P1）**：本地 `_sessionGeneration` 与 Framework lease generation 脱节；campaign 切换或二次进入时，迟到的 readiness、hero binding、store 操作可能没有被真实 lease 取消。
6. **依赖版本声明风险（P1）**：AWAKE 的 `SubModule.xml` 只声明 `MarcusAIFramework`，没有像已安装 Marcus 模块那样写 `DependentVersion`；版本匹配需由发布/加载验证补齐，不能只依赖目录存在。
7. **MCM/GUI 误导风险（P2）**：当前状态页面可以显示 `session:ready`，但旧接口的 `CurrentSession` 不等于真实 `SessionLease.State == Ready`；需要在适配后改为显示 Host/session authoritative state。
8. **双版本支持风险（P2）**：AWAKE Manifest 当前只声明 `1.3.15`，而 Framework 文档支持 `1.4.8` 和 `1.3.15`；若未来要支持 `1.4.8`，必须使用对应 SDK/reference 分目录构建，不能沿用当前 v1.3.15 产物。

## 当前外部 Marcus 依赖清单

### 直接声明/运行时前置

| 项目 | 当前静态证据 | 状态判定 |
|---|---|---|
| `MarcusAIFramework` Bannerlord 模块 | `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\SubModule.xml:14`、`:24`；游戏目录模块 `v0.1.0`，DLL 为 `MarcusAIFramework.dll` | **必需外部模块；文件存在已确认，Host Ready/dispatcher 未验证** |
| Marcus Companion | `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\MarcusAIFramework\Companion\` 存在 Companion/SQLite/LLamaSharp 等文件 | **外部运行时资产存在；本轮未验证进程、连接、Provider 或 IPC** |
| Marcus Framework API | AWAKE 当前项目通过 `ProjectReference` 指向本地 `AWAKE\\framework\\MarcusAwakeFramework\\MarcusAwakeFramework.csproj`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\AWAKE.csproj:10`、`:25` | **当前源码的 build-time API 来源；不是已证明与外部 `MarcusAIFramework.dll` 同一程序集** |

### 工作区存在但未被 AWAKE 当前工程声明的 Marcus 组件

以下项目在 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\` 下存在，但没有被 `AWAKE.csproj` 或 `SubModule.xml` 作为当前 Bannerlord 运行时依赖接入：

- `MarcusAwakeProvider`：`net8.0`。
- `MarcusAwakeRuntimeService`：`net8.0-windows`，并引用 `MarcusAwakeTransport`。
- `MarcusAwakeStorage`：`net8.0`，引用本地 `MarcusAwakeFramework`，并使用 `Microsoft.Data.Sqlite 8.0.21` 与 `SQLitePCLRaw.bundle_e_sqlite3 2.1.10`。
- `MarcusAwakeTransport`：`netstandard2.0`。

它们是工作区内的 Framework/Companion 侧候选组件或离线实现，不是当前 AWAKE 已证明可由 Bannerlord `net472` DLL 直接加载的外部依赖。不能因为项目文件存在就把 Provider、Runtime Service、Storage 或 Transport 视为已接线。

### AWAKE 的非 Marcus 直接前置（仅用于加载风险上下文）

`SubModule.xml` 还声明 `Bannerlord.Harmony`、`Bannerlord.ButterLib`、`Bannerlord.UIExtenderEx`、`Bannerlord.MBOptionScreen`、`Native`、`SandBoxCore`、`Sandbox`，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\SubModule.xml:9`、`:15`。这些是 Bannerlord/GUI 侧依赖，不应与 Marcus Host/API 依赖混为一谈。

## 可测试入口

### 已存在的离线入口（本轮未运行）

| 入口 | 能验证什么 | 不能验证什么 |
|---|---|---|
| `MarcusAwakeFramework.Tests.exe --runtime-vertical` | Framework Runtime vertical fixture、Host/Session/请求/receipt 的离线契约；参数分支见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\tests\Program.cs:29`、`:54` | 不能证明 AWAKE `SubModule` caller、真实 Bannerlord、外部 Host dispatcher、Companion 或真实 Provider |
| Framework 测试默认入口 | 包含 `HostLifecycleAndProbe`，可验证 Host `Register`/`Ready`、locator、session begin/close/drain/clear；见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\tests\Program.cs:30`、`:39`、`:225` | 不能证明 AWAKE 三文件已调用这些 API |
| `AwakeRuntime.SetHostOverrideForTesting` | 注入兼容 Host，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:103` | 只绕过 locator，不能证明真实 external Host 绑定 |
| `AwakeRuntime.NativeReadinessProbeForTesting` | 测 ready/cancel/stale-session 分支，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:93`、`:275` | 仍只覆盖 AWAKE 本地 generation，不覆盖 Framework lease |
| `AwakeRuntime.ResetSessionStateForTesting` | 测试隔离和本地 session reset，见 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeRuntime.cs:466` | 不是 `Sessions.BeginSession` 或真实 session close |

### 当前缺失的关键测试入口

应补一个不启动游戏的 AWAKE integration fixture，至少记录以下顺序：

```text
fake Framework host Created
  -> Host.Register / Host.Ready
  -> FrameworkHostLocator.Register(host)
  -> register AwakeExtension
  -> SessionCoordinator.BeginSession(reference)
  -> dispatcher invokes AwakeExtension.OnLifecycle(CampaignSessionReady, reference)
  -> AWAKE RequestContext uses the same lease/reference/generation
  -> dispatcher invokes SessionEnding
  -> BeginClosing cancels token
  -> bounded drain
  -> CompleteDrain(reference, closingGeneration)
```

该 fixture 当前不存在于 AWAKE 源码中；直接调用 `OnLifecycle` 只能证明处理体可执行，不能证明 `SubModule → Framework dispatcher → OnLifecycle` 的 caller 链已成立。`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\ProbeExtension.cs:11` 的 `InternalsVisibleTo("Awake.SdkSmoke")` 为未来测试提供了可见性入口，但本轮没有新增或运行测试。

### 未来游戏验证入口（本轮明确未执行）

必须用同一个当前候选绑定 Bannerlord BuildId、`Awake.dll` SHA-256、Framework DLL SHA-256、`SubModule.xml` 版本和一段新的日志 session。最低观察项：

1. AWAKE 日志出现 `register_ok`，而不是 `register_failed code=host.unavailable`。
2. `AwakeProbe.log` 出现 `lifecycle stage=CampaignSessionReady session=<stable-id>`。
3. 随后出现 `native_readiness status=Ready`、memory service 初始化和 world state ready；这些必须带同一 session/generation 关联。
4. MCM/Developer Check 显示 authoritative Host/session state，而不是只显示 `CurrentSession` 非空。
5. 触发一个现有玩家/事件入口，观察 route/command/storage 的 typed result 或 receipt，并在 UI、日志或持久状态中看到同一 correlation/settlement。
6. 离开战役或卸载时观察 `SessionEnding`、token cancellation、bounded drain、session `CompleteDrain`；不能以游戏没有立即崩溃代替 drain 证据。

这些步骤需要用户实际运行游戏并提供当前 BuildId 匹配日志；本报告不把它们标成已验证。

## 证据等级矩阵

| 证据等级 | 本轮状态 | 说明 |
|---|---|---|
| E0 静态存在/结构 | **已达到** | 已读取目标源码、内置 Host/Session API、项目配置和只读安装元数据 |
| E1 编译/解析 | **未执行** | 本轮没有 build、XML/程序集加载验证或 lint |
| E2 离线测试/Smoke | **未执行** | 现有 Framework fixture 只作为可测试入口记录，不作为本轮结果 |
| E3 同步/哈希一致 | **未达到** | 当前 `_build_out`、`dist` 和外部 Framework 产物存在引用/哈希不一致；本轮不修改或同步 |
| E4 当前 BuildId 游戏入口闭环 | **未达到** | 未启动 Bannerlord，没有用户提供的当前 BuildId 日志 |
| E5 存读档/长时回归 | **未达到** | 未启动游戏，也没有存档回归证据 |

## 最终判定

**VERDICT: NOT READY — AWAKE caller/session integration is not statically proven.**

在下一次实现前，最小前置顺序应是：

1. 统一 `MarcusAIFramework` 与 `MarcusAwakeFramework` 的程序集/API 身份和发布策略。
2. 由 Framework 所有者提供真实 Host registration、lifecycle dispatcher 和 session-bound lease 入口。
3. 让 AWAKE 使用真实 lease/cancellation，而不是 compatibility lease、空 session 和本地 flag 代替。
4. 重写为非阻塞 session drain，并补齐 session/Host 的 owner、generation、clear 顺序。
5. 先通过离线 caller fixture，再由用户运行同一候选做 v1.3.15 当前 BuildId 的 E4/E5 验证。

