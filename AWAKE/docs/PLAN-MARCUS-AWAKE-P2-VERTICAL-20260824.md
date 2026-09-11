# Plan: Marcus-Awake P2 Framework Core Vertical Integration
_Locked via grill — based on the approved Marcus-Awake migration boundary and repository evidence_

## Contract anchor

- P1.5 authority: `_houkai_merge/AWAKE/docs/MARCUS-AWAKE-P1.5-CONTRACT-LOCK-DRAFT-20260824.md`
- P1.5 status: `P1.5_CONTRACT_LOCKED`; SHA-256 at this plan revision: `DB45A9C4C130BC727394B61AFD9836508045F83B66BA7F23C0BF3686D596D619`
- Approval record: `_houkai_merge/AWAKE/docs/checkpoints/MARCUS-AWAKE-FULL-CAPABILITY-20260824-checkpoint.md` (`VERDICT: APPROVED` + user sign-off)
- Before implementation, recompute and compare this hash. A mismatch pauses P2 and requires a new contract review; P2 evidence must record the matched hash. P1.5 is the immutable contract anchor for this batch; later edits require a new P2 plan revision.

## Goal

在不修改 AWAKE 主工程、外部 Marcus 依赖、Runtime Service、发布目录或游戏目录的前提下，把 P1 的独立 Framework Core 契约接成一条可重复执行、可并发验证、可证明排空的纵向闭环：确定性 `FixtureLifecycleDriver` 调用 internal Bootstrap，Framework 创建并注册 Host，进入 SessionReady，创建绑定当前 `SessionLease` 代际的 `RequestContext`，执行一个经过 Session 验证的公共 GameData/Context/Diagnostics 调用，返回 typed success/error 与绑定当前会话的 `FrameworkProbeReceipt`，在 SessionEnding 时立即递增 fence generation、取消 CTS、拒绝新请求并隔离迟到结果，只有待处理操作清零后才允许 drain。P2 只证明 Framework Core 的离线入口、生命周期、上下文、结果和诊断链可运行，不宣称 AWAKE 游戏内已迁移或 AI Provider 已可用。

## Approach

1. 新增 internal `FrameworkBootstrap` 与受控测试可见性，集中按 `Host.Register → Host.Ready → Locator.Register → Session.BeginSession` 建立 Host；注册或 session 失败时执行 `Locator.Clear` 与 internal `AbortRegistration` 补偿回滚，不增加新的 public 类型或 Bootstrap 入口。更新 `FrameworkIdentity.Current()` 的 API major 为 `2.0`，使身份版本与 breaking contract 一致。
2. 修正 P1 中的 session 身份契约：`RequestContext` 改为从 `SessionLease` 创建并捕获 `SessionGeneration`；`SessionCoordinator.ValidateLease` 在同一同步边界内校验 SessionRef、generation 和 Ready 状态；同一 SessionRef 的新 generation 不能使旧 context 复活。P2 将 Framework API major 从 `1.0` 升为 `2.0`，正式废弃 P1 的旧构造函数和 Probe 返回类型，不保留旧 `MarcusAIFramework.Api` 兼容层；P1 evidence 仅作为历史基线。
3. 让 SessionCoordinator/SessionLease 成为线程安全状态机：`SessionCoordinator._sync` 串行化 current/nextGeneration/操作句柄，`SessionLease._sync` 串行化 state/generation/pending count；所有跨对象转换固定为“先 coordinator 锁、后 lease 锁”，lease 不反向获取 coordinator 锁。Ready→Closing 时在同步边界内递增 fence generation、生成稳定 `DrainTaskId`、发布关闭状态并取出 CTS，再在锁外调用 `Cancel()`，拒绝新操作；通过公开的最小 `SessionOperation` handle 跟踪 fixture 注入的异步操作，未清零时 `CompleteDrain` 返回 `session_drain_incomplete`，迟到完成返回 `session_stale_result` 且不更新任何状态。重复 Complete 返回 `session_operation_already_completed`，不得双重释放 pending 计数。
4. 修正 Diagnostics 入口为 `OperationResult<FrameworkProbeReceipt> Probe(...)`，`DiagnosticsService` 自己调用 `ValidateLease`，不接受调用方声称“已验证”的裸 context；通过同一 validation 结果生成 receipt。closing、drained、generation mismatch 和 deadline expired 只返回稳定 typed error，不生成成功 receipt；成功 receipt 的 generation 必须等于 validation 线性化点的当前 fence。
5. 增加 `FixtureLifecycleDriver` 与 `FrameworkCoreVerticalSmokeFixture`，覆盖 `Load → Host.Register → Host.Ready → Locator.Register → SessionReady → RequestContext → Sessions.ValidateLease → GameData/Context → typed result → receipt → SessionEnding → incomplete Drain rejection → late-result fence → complete Drain → fixture Unregister`；明确这是离线边界 fixture，不创建 Bannerlord SubModule、不调用真实 Bannerlord callback。
6. 补齐测试 `.csproj` 的显式 Compile 写入，运行 P1 Core Release build、P2 fixture、并发/迟到结果测试、静态边界扫描和证据哈希检查；仅在全部通过后按规定顺序生成 evidence、更新 P2 checkpoint，并在状态实际变化时更新 `AWAKE-CURRENT`，不修改游戏目录。

## Key decisions & tradeoffs

- P2 精确写集为：`framework/MarcusAwakeFramework/src/FrameworkBootstrap.cs`、`src/TestAccess.cs`、`src/FrameworkIdentity.cs`、`src/HostApi.cs`、`src/RequestContext.cs`、`src/SessionApi.cs`、`src/IpcAndDiagnosticsApi.cs`；`tests/FrameworkCoreVerticalSmoke.cs`、`tests/Program.cs`、`tests/MarcusAwakeFramework.Tests.csproj`；P2 evidence、P2 checkpoint、P2 plan/review log，以及在 P2 checkpoint 状态实际变化时才更新的 `docs/AWAKE-CURRENT.md`。不触碰 `AWAKE.csproj`、`SubModule.xml`、现有 AWAKE runtime `src`、`dist`、Runtime Service、MCM、DevTools、世界书和冻结候选。
- Bootstrap 采用确定性依赖工厂/内存替身，生产 Provider、IPC Server、SQLite/RAG、CredentialBroker、EgressBroker 留到 P3；这样能证明调用链而不把测试替身误当作运行时实现。
- `FixtureLifecycleDriver` 只代表未来 SubModule/Host 边界的离线契约，不命名为真实 SubModule，不调用 Bannerlord 生命周期，也不产生 E4 游戏证据；真实 Bannerlord `SubModule` 接入属于后续 AWAKE 适配批次。
- 不恢复旧 `MarcusAIFramework.Api` 兼容层，不复制旧 Companion，不让 AWAKE 形成新旧双权威路径。
- `SessionLease.Generation` 是当前 fence generation：打开时分配，进入 Closing 时原子递增一次并发布关闭状态，再在锁外取消 CTS；RequestContext/异步操作捕获创建时 generation，完成入口必须再次核对 generation、状态和操作 token。`CompleteDrain` 是唯一完成证明，不在生命周期回调中阻塞等待。
- `SessionCoordinator.BeginSession` 语义固定：同一 Ready 引用重复调用返回同一 lease；Closing 返回 `session_closing`；Drained 后只能创建新的 session generation，不能重新打开已 drained lease；RecoveryRequired 不允许静默替换，返回 `session_recovery_required`。`BeginClosing` 首次进入 Closing 时生成稳定 `DrainTaskId`，重复调用返回同一 task 标识。
- Diagnostics 的验证线性化点是 `ValidateLease`；Probe 在内部取得该调用返回的当前 lease generation。测试必须覆盖 Probe 前后关闭、过期 deadline、旧 generation 和 drained 四种结果，并检查失败没有 receipt；每个失败都必须保留输入 correlation。
- pending-operation barrier 是 Framework Core 的最小生产契约，不是只存在于测试的计数器：开始操作必须经过当前 context gate，完成操作无论成功/迟到都释放 pending 计数；未清零不得 drain。
- `GameData`/`ContextPlanner` 的低层接口不自行猜测 session；P2 的唯一公共调用入口必须是 `ValidateLease` 成功后再调用它们，fixture 记录该 gate。若发现可从未验证 context 直接产生成功结果，P2 失败。
- `IpcSequenceWindow` 和 `BackpressureGate` 属于同一 Core 的共享状态对象；P2 固定实现为：`IpcSequenceWindow` 用一个私有 `lock` 覆盖 `Evaluate`、接收字典、连续水位和只读计数，单次 `Evaluate` 在锁内完成校验→记录→推进水位，锁内位置是线性化点；`BackpressureGate` 用 `Interlocked.CompareExchange` 循环实现 `TryEnter`，成功 CAS 是线性化点，`Exit` 用原子递减且禁止下溢，`Active` 用 `Volatile.Read`。调用方不需要外部锁。

## Risks / open questions

- P1 API 当前是最小契约集合，尚未等同于旧 Marcus 的完整 Provider/Storage/Gateway API；P2 只修正已存在契约的身份/诊断绑定，不扩大为完整旧 API 兼容层。
- Bootstrap 保持 internal，通过 `InternalsVisibleTo("MarcusAwakeFramework.Tests")` 供确定性 fixture 使用；P2 evidence 必须记录 public API surface 未因 Bootstrap 增加。
- 如果现有状态机不能表达重复 SessionReady、closing 期间新请求、同一 SessionRef 重开后的旧 context、并发状态转换、drain 未完成和旧 receipt/迟到结果拒绝，先修正 Framework Core 契约/测试，不进入 AWAKE 接入。
- P2 离线证据最高为 E1；没有 Runtime Service、真实 Named Pipe、Bannerlord 启动、存档或 E2/E4/E5 证据。P3 才负责真实 Service/IPC 的 E2，P7 才负责同步与 E3，E4/E5 仍需用户实机验证。

## Out of scope

- 不修改 `AWAKE.csproj`、`SubModule.xml`、`AWAKE/src` 或删除外部 `MarcusAIFramework` 依赖。
- 不实现 Provider、API Key、MCM、Runtime Service、Named Pipe ACL、SQLite/RAG、Credential/Egress、DevTools、世界书和 NPC 对话。
- 不启动 Bannerlord，不同步游戏目录，不修改冻结候选，不提版本，不创建玩家可见配置入口。
- 不把测试替身、P2 fixture 或 Framework Core DLL 宣称为完整 Marcus-Awake 迁移。

## Acceptance cases

- **正常链路**：FixtureLifecycleDriver 完成 Host/Ready/Locator/SessionReady；当前 lease 创建 context；Sessions.ValidateLease、GameData/Context 调用成功；receipt 的 generation 与 lease 当前 fence 一致。
- **重复/冲突**：重复 Load/Register/SessionReady 返回固定幂等或 typed conflict；注册另一 Host 不替换当前 Host；Locator 冲突时候选 Host 回到 Created 且原 Host 保持不变。
- **过期与关闭**：旧 deadline、closing、drained、旧 generation 和同一 SessionRef 新 generation 下的旧 context 均返回稳定 typed error；pending 未清零不得 drain；迟到完成只能返回 `session_stale_result`，不得产生成功诊断 receipt 或晚到写入。
- **证据边界**：P2 evidence 记录精确写集、P1/P2 文件 SHA-256、DLL/测试 EXE 哈希、完整 fixture 输出、并发/迟到结果矩阵、public API surface diff、静态 forbidden-scope 扫描和 E1 限制；按 `offline_fixture`、`framework_build`、`real_bannerlord` 三类分组，后者固定为 `not_run`，不记录或暗示游戏内验证。

## Locked failure and evidence rules

- 稳定错误码固定为：`session_closing`（关闭中拒绝新 session/request）、`session_stale`（context 无效）、`session_generation_conflict`（drain/lease generation 不匹配）、`session_drain_incomplete`（仍有 pending operation）、`session_stale_result`（迟到完成被 fence）、`session_operation_already_completed`（操作句柄重复完成）、`session_recovery_required`（恢复态禁止静默替换）、`request_deadline_expired`（deadline 已过）。每个错误必须带 owner 和 correlation ID；P1.5 的 snake_case code 是唯一拼写，禁止在实现中混用点号变体。
- Bootstrap 的失败测试使用已占用 Locator 的独立 Host；失败时必须证明当前 Host 未被替换、候选 Host 回到 `Created`、Locator 保持原 Host，不能只检查返回 false。
- 证据生成顺序固定为：先校验 P1.5 hash → 保存原始构建/测试/扫描输出 → 计算文件和产物哈希 → 生成 `docs/evidence/MARCUS-AWAKE-FRAMEWORK-CORE-VERTICAL-SMOKE-E1-20260824.json` → 校验证据 JSON 可解析且结果与原始输出一致 → 更新 checkpoint → 仅在 checkpoint 状态变化时更新 `AWAKE-CURRENT`。任何失败只记录失败结果，不写完成声明；P2 不生成或冒充 P1.5 规定的 E2 证据。
- forbidden-scope 扫描只扫描 P2 精确写集与其构建输入，排除 `obj`、`_build_out`、`dist`、游戏目录和历史归档；扫描目标为 `AWAKE.csproj`、`SubModule.xml`、`AWAKE/src`、Runtime Service、MCM、DevTools、世界书和冻结候选，预期零修改/零写入命中。
- public API surface 验收比较 P1/P2 的导出类型、可见成员、参数、返回类型和 assembly visibility；internal Bootstrap、AbortRegistration、pending-operation helper 不得出现在导出清单中。`RequestContext` 和 `Probe` 的 breaking change 已明确接受，P2 必须同步更新 Framework Core 测试调用方，并在 evidence 中列出旧签名→新签名、P1 evidence 失效范围和后续 AWAKE 调用方迁移门；不得伪装成无变更。
- P2 测试工程必须显式包含 `tests/FrameworkCoreVerticalSmoke.cs`（不能只列 `Program.cs`）；所有 fixture 断言必须实际运行并输出稳定 case ID，不允许只留下未引用文件。
- `FrameworkHostLocator.Register` 只接受 `HostState.Ready` 的 Host；Bootstrap 每个失败点都必须验证候选 Host 回到 `Created`、候选 session 未打开、Locator 原值未被替换。`AbortRegistration` 只允许在未打开 session 的 Registered/Ready 候选上调用。
- 错误码与类别固定映射：`session_closing`→`Conflict`、`session_stale`→`Expired`、`session_generation_conflict`→`Conflict`、`session_drain_incomplete`→`Conflict`、`session_stale_result`→`Expired`、`session_operation_already_completed`→`Conflict`、`session_recovery_required`→`RecoveryRequired`、`request_deadline_expired`→`Expired`；所有传播链保留原 correlation/owner。
- `SessionOperation` 契约固定为：创建时绑定 owner coordinator、lease、operation ID、generation；首次 `Complete()` 原子释放 pending 并根据当前 fence 返回 success/stale；重复 `Complete()` 返回 already-completed；没有完成句柄不得通过 `CompleteDrain`。
- 并发 fixture 使用受控 barrier/event 和固定交错序列，不使用 `Thread.Sleep`、时间猜测或随机重试；至少覆盖“双 BeginSession、BeginClosing 与 Validate 并发、Complete 与 CompleteDrain 竞态、IPC 重复/乱序、Backpressure 超容量”五组 case，并输出每组最终状态/计数。
- RecoveryRequired 只允许由 `Closing` 在显式的 drain deadline/不可恢复失败 fault seam 上进入；P2 不提供静默恢复或原地替换。进入后该 coordinator 是终态：`BeginSession`、`ValidateLease`、`BeginOperation`、`CompleteDrain` 返回 `session_recovery_required`（`BeginClosing` 只幂等返回当前 recovery lease），迟到操作仍释放 pending 但返回 `session_stale_result`；只有外部明确丢弃该 Bootstrap 实例并完成恢复后，新的 Bootstrap/新 coordinator 才能创建新的 generation，P2 不自动调用此路径。
- Bootstrap rollback matrix 固定记录四个“成功后故障注入”阶段：`Host.Register`、`Host.Ready`、`Locator.Register`、`Session.BeginSession`；internal `BootstrapFaultPoint` 只在 fixture seam 存在。故障可发生在阶段状态已变更之后，用来证明部分注册也能补偿：注册/Ready 失败时 `AbortRegistration` 必须把候选 Host 重置为 `Created`；Locator 冲突时只能 `Clear(candidate)`，原 Locator 保持不变；Session 阶段失败时先对候选 lease 执行 `BeginClosing → CompleteDrain`（无 pending），再清 Locator、AbortRegistration。每个 case 断言候选 Host 最终为 `Created`、没有 live session、Locator 原值未被替换、下一次无故障启动可成功。
- P2 mandatory evidence 仅包含上述 E1 fixture/build/API/boundary 证明；release packaging、真实 Service/IPC、Provider、MCM 和游戏内证据属于后续阶段，不得为了填 evidence 伪造第二套实现。
