# Plan: Worldbook Studio A4-Launcher 生命周期精进

> 状态：**`implemented`（2026-09-30 标记）。**
> 依据：本批所属子系统已存在：`tools/worldbook-studio/` 含 `Awake.WorldbookStudio.{Cli,Core,Launcher,Web}` 与 entity registry；本系列为该子系统的内部行为保持型重构批次。
> ⚠️ 本文件是**批次施工单**，其所属子系统已存在 ⇒ 本批视为已完成。**保留本文件**（不是 `superseded`）——
> 它记录了当时的设计意图与边界，仍有追溯价值。
> 现行方向：`AWAKE-ROADMAP.md`；分诊依据：`AUDIT-PLAN-TRIAGE-20260930.md`。

_Locked via autonomous grill — implementation completed and offline verified, 2026-08-23_

> Status: `offline_verified`. The bounded implementation and release checks below are complete. No Schema, CLI/Web wire contract, AI/CAS contract, AWAKE runtime, game directory, or frozen candidate was changed.

## Goal
在不改变 Worldbook Studio Schema、CLI/Web wire 契约、`StudioBootstrap`、`/health`、受控 shutdown、固定 `127.0.0.1:5077` 地址和免安装发布边界的前提下，修复现有 Launcher 的生命周期竞态与可恢复性缺口：关闭期间不再继续启动 Web，取消或异常退出后始终进入最终回收，重复启动路径不泄漏锁，旧工作区设置不再静默覆盖或自动重建，并让浏览器启动结果与真实 Shell 返回值一致。生命周期状态只存在 Launcher 内部；Core 不新增公共类型，CLI/Web golden 不变。

## Approach
1. 以现有 Launcher checkpoint、源码和 smoke 契约为基线，冻结外部入口、包布局、health/shutdown proof、日志脱敏和不触碰游戏目录的边界；计划本身不再宣称已批准。
2. 在 `WebProcessHost` 内实现仅限 Launcher 程序集的状态机：`Created → Starting → Running → Stopping → Stopped`，失败路径为 `Starting → Stopped`。同一 Host 的重复 `StartAsync` 返回同一启动任务或报告已启动，不创建第二组资源；重复 `StopAsync` 返回同一停止任务；`Stopped` 不支持重启，重启必须新建 Host。
3. 固定取消语义：启动令牌只取消 bootstrap/health 等待，取消后仍执行一次失败清理；停止令牌只取消优雅 shutdown 请求和等待，不得跳过 `TerminateJobObject`、最终 `WaitForSingleObject`、管道/Job/进程/线程句柄释放。所有临时句柄从创建起由 Host 统一 owner，`StopAsync` 在“无进程但有 Job/管道”的半启动状态也必须清理。
4. 精进 `LauncherForm`：使用一个生命周期任务和一个关闭请求标志；`FormClosing` 首次只取消启动/请求停止并暂缓关闭，待同一清理任务完成后再关闭窗口。明确维护 `_ownsMutex` 与一次性 cleanup 状态：非 owner 只 Dispose，owner 才 ReleaseMutex；重复 Close、duplicate-launch、smoke 自动退出和启动失败共用同一清理入口，Host 本体最终 Dispose。
5. 精进 `WorkspaceManager`：区分“首次运行”、“已保存路径缺失”、“marker 损坏/版本不支持”、“路径不安全”、“权限/IO 失败”；恢复既有设置时要求目录已存在且 marker 有效，禁止自动创建或重写 marker。失效设置显示明确可恢复错误，只有用户重新选择并确认后才创建目录、marker 和 settings；保留旧 settings 直到新选择成功。
6. 保持 Web 路由和 Core 公共契约不变；Host 所有本地地址从 `StudioRuntimeConstants.Port` 派生，固定可产生的错误码矩阵。启动未通过 health 前只依赖 Job/进程最终回收，不宣称存在可用 graceful shutdown；已启动时检查 shutdown HTTP status 并记录 accepted/rejected/transport 三类脱敏事件，但任何结果都不改变最终回收路径。
7. 修正 `ShellBrowserOpener`：以 `Process.Start` 返回的非空句柄作为“Shell 已接受请求”的成功条件；smoke 结果传递实际 bool，不从事件码反推。明确不宣称浏览器窗口已完成导航，保留复制地址 fallback。
8. 增加分层 Launcher 验证：package-level smoke 只覆盖真实包可驱动的 clean-start、browser success/failure、stale-settings（目录缺失与 marker 损坏）、duplicate-launch、graceful shutdown；startup cancellation、Stop cancellation、forced cleanup、native fault、同 Host 并发和 FormClosing 竞态全部只属于独立 Windows Launcher test host。每项记录 Launcher PID、Web child PID、端口、exit code、`HasExited` 的关闭前观察值和关闭后独立 PID 查询；禁止用已释放句柄的 `HasExited()==true` 作为唯一证据。
9. 运行 Launcher 目标框架的 Release 构建、既有 Studio harness、默认与扩展 smoke、发布包 release-check 和 A4 既有契约回归；只在上述证据齐全后记录变更文件哈希、债务审计、证据等级与剩余风险到 checkpoint，并更新 `AWAKE-CURRENT.md`。

## Lifecycle contract

所有状态读写和 task 发布都在同一个 `_lifecycleLock` 内完成；该 lock 是唯一线性化点，`StartAsync`、`StopAsync`、`Dispose` 不直接并发写字段。`StartAsync` 在 `Created` 发布 `_startTask` 后才允许原生调用；`StopAsync` 在 `Starting` 发布 `_stopTask`、取消 internal start CTS 后，必须等待同一个 `_startTask` 完成，再由 `EnsureFinalCleanupTaskLocked()` 发布或复用唯一 `_finalCleanupTask`；Start catch 不得另建第二套清理。`FormClosing` 只等待 `_stopTask`，不得自行访问 Host 句柄。

| 状态 | `StartAsync` | `StopAsync` | `Dispose` | 共享 owner |
|---|---|---|---|---|
| `Created` | 发布唯一 `_startTask`，进入 `Starting` | 发布已完成 `_stopTask`，进入 `Stopped` | 直接进入 `Disposed` | lifecycle lock |
| `Starting` | 返回同一 `_startTask`；caller token 只取消自己的等待 | 发布/返回同一 `_stopTask`，取消 internal start CTS，等待 start task 后复用唯一 final cleanup | 确保/等待同一 `_finalCleanupTask`，完成后进入 `Disposed` | start/stop/final task |
| `Running` | 返回同一已完成 task | 发布/返回同一 `_stopTask`，进入 `Stopping`，由 stop task 创建唯一 graceful CTS 和 final cleanup | 确保/等待同一 `_finalCleanupTask`，完成后进入 `Disposed` | stop/final task |
| `Stopping` | 抛出 `WB-LAUNCHER-STATE-409` | 返回同一 `_stopTask`；caller token 不影响共享 cleanup | 等待同一 stop task 完成 | stop task |
| `Stopped` | 抛出 `WB-LAUNCHER-STATE-409`；重启必须新建 Host | 返回已完成 task | 进入 `Disposed` | lifecycle lock |
| `Disposed` | 抛出 `ObjectDisposedException` | 完成 no-op | 完成 no-op | lifecycle lock |

取消分为三类且不混用：`caller-wait cancellation` 只使当前调用的 `WaitAsync(token)` 抛 `OperationCanceledException`，共享 task 继续；`internal-start cancellation` 只由 Form close/Stop/Dispose 触发 `internalStartCts`，start task 以 `WB-START-CANCELLED` 结束但不释放资源；唯一 `_finalCleanupTask` 负责后续回收；`graceful-shutdown cancellation` 由 stop task 创建并在 graceful 500ms deadline 触发 `internalGracefulCts.Cancel()`，caller token 不触碰该 CTS，stop task 继续强制回收并返回最终退出结果。`Dispose` 在所有状态都同步等待共享 final cleanup 后再标记 `Disposed`。

`FormClosing` 的唯一协议是：第一次事件设置 `e.Cancel=true`，请求同一 cleanup task；cleanup 完成后通过 UI 线程 `BeginInvoke` 设置一次性 allow-close 标志并再次调用 `Close()`，再次事件不得触碰已关闭控件。`duplicate-launch` 和 smoke 自动退出也走同一 cleanup task。

## Native ownership and cleanup contract

| 资源 | 创建后 owner | transfer/释放规则 |
|---|---|---|
| bootstrap read handle | Host 字段 `_bootstrapRead` | `CreateProcess` 成功且继承列表完成后由 Host 关闭父端 read；失败前一直由 Host 清理 |
| bootstrap write handle | Host 字段 `_bootstrapWrite` | `WriteBootstrapAsync` 成功/失败后由 Host 统一释放 |
| Job Object | Host 字段 `_job` | 创建即 owner；`_jobAssigned=true` 只在 Assign 成功后设置 |
| Web process/thread handle | Host 字段 `_process`/`_thread` | 原生返回后立即转移到 Host；任何后续失败都走统一 cleanup |
| PID/exit snapshot | Host immutable snapshot | 在关闭 process handle 前记录 `pid`、`exitCode`、`exitedObserved`；句柄置空后不得用空句柄伪造退出证据 |

最终顺序和时限固定为三阶段：graceful phase 总计 500ms（包含 authenticated shutdown 请求与短暂等待）；final cleanup phase 2s（按 `_jobAssigned` 选择 `TerminateJobObject` 或 `TerminateProcess`，Terminate 返回 false 时立即执行另一条可用路径）；Job-close fallback phase 500ms。若进入 fallback，先调用一次 `CloseJobHandleForFallback()`，将 `_job` 转移为 null 并记录 `jobClosedForFallback=true`；随后等待 500ms、独立查询 PID、记录 `exitedObserved`/exit snapshot，之后只释放 thread → process → write/read，不能再次释放 Job。若未进入 fallback，先在 final wait 后记录 snapshot，再按 thread → process → write/read → job 释放。总 `evidenceDeadlineUtc = cleanupStartedUtc + 3000ms`，超过该时限的唯一终态是 `WB-CLEANUP-408`、`ok=false`；不得以释放后的空句柄生成成功证据。

`AssignProcessToJobObject=false`、Resume 失败、Terminate 失败和等待超时通过内部 `IWebProcessNativeApi` 最小 seam 注入；fault harness 在独立临时端口/包根运行，测试结束前必须确认 PID 不存在，否则该用例失败并阻止后续 smoke。`CreateProcess` 返回到 Assign 之间的崩溃窗口仍是明确未证明边界。

## Observable error matrix

| 可观测条件 | 唯一事件/错误码 | 必须留下的证据 |
|---|---|---|
| caller-wait cancellation | caller `OperationCanceledException`，不写 smoke success code | 共享 start/stop task 仍继续，最终结果由后续观察取得 |
| internal-start cancellation 且未进入 health | `WB-START-CANCELLED` | start cancellation、Web PID、最终 `exitedObserved` |
| 内部 health deadline 到期 | `WB-HEALTH-408` | 10 秒 deadline、最后请求结果、Web PID 退出状态 |
| Web 在 health 前退出 | `WB-WEB-EXIT-001` | child exit code、PID 后查不存在 |
| 5077 已有匹配 Studio | `WB-PORT-409-OTHER` | preflight health identity，不发送 shutdown |
| 5077 有非 Studio 响应 | `WB-PORT-409-NON_STUDIO` | response status/脱敏类型，不发送 shutdown |
| authenticated shutdown 2xx | `WB-SHUTDOWN-ACCEPTED` | status、最终 PID 退出 |
| shutdown 非 2xx | `WB-SHUTDOWN-REJECTED` | status、最终强制回收 |
| shutdown transport/timeout | `WB-SHUTDOWN-TRANSPORT` | 异常类型哈希、最终强制回收 |
| final wait 后 PID 仍存在 | `WB-CLEANUP-408` | PID、exit code/null、`ok=false` |
| 第二 Launcher 取得 `Local\\` mutex 失败 | `WB-INSTANCE-409` | secondary Launcher PID/exit code，不启动第二 Web |
| 保存 settings 指向缺失目录 | `WB-SETTINGS-RECOVER-404` | settings before/after hash 相同，旧目录未创建 |
| marker 损坏/版本不支持 | `WB-SETTINGS-RECOVER-422` | settings/marker before/after hash 相同，Web 未启动 |
| 读取 settings 被拒绝 | `WB-SETTINGS-READ-403` | `exit=1`、`ok=false`、旧 settings hash 不变 |
| 新 settings 写入被拒绝 | `WB-SETTINGS-WRITE-403` | `exit=1`、`ok=false`、旧 settings hash 不变 |
| marker/settings 原子提交失败 | `WB-SETTINGS-COMMIT-500` | `exit=1`、`ok=false`、旧 settings hash 不变 |
| package 缺少 Web/schema | `WB-PACKAGE-001` | package manifest、Web PID 为 null |
| bootstrap/pipe 无效 | `WB-BOOTSTRAP-002` | Web PID、pipe fault、最终 PID 状态 |
| AssignProcessToJobObject 返回 false | `WB-JOB-001` | `exit=1`、`ok=false`、不 Resume、PID cleanup |
| ResumeThread 返回失败 | `WB-PROCESS-001` | `exit=1`、`ok=false`、Job cleanup、PID 查询 |
| TerminateJobObject 失败但 fallback 退出 | event `WB-CLEANUP-JOB-FALLBACK`；结果 code 保留原 shutdown code | `exit=0`、`ok=true`、fallback 次序、PID 查询 |
| Terminate/Job-close fallback 后仍存活 | `WB-CLEANUP-408` | `exit=1`、`ok=false`、PID、exit code/null、最终查询 |
| browser opener 返回 false/抛异常 | `WB-BROWSER-OPEN-FAILED` | `browserOpened=false`、Web 继续存活、复制地址可用 |

端口 preflight 只在明确观察到已有响应时分类；探测到自由端口后发生的竞态绑定失败统一走 Web exit/health 失败，不伪造 `WB-PORT-409-*`。`Local\\` mutex 的产品语义固定为“每个交互式 Windows session 单实例”；跨 session 的第二个实例由固定端口 preflight 产生可观测冲突，不自动换端口。

## Workspace and browser seams

`WorkspaceManager` 使用内部结构化 `WorkspaceResolution`，不再以 `null` 混淆缺失、损坏和 IO 失败：

| 分类 | code | 恢复动作与副作用 |
|---|---|---|
| 没有 settings 文件 | `WB-SETTINGS-FIRST-RUN` | 进入首次运行选择；用户确认后允许创建新目录/marker/settings |
| settings JSON 损坏或版本不支持 | `WB-SETTINGS-RECOVER-422` | 显示恢复提示，旧 settings 原样保留，不读路径、不启动 Web |
| 保存目录不存在 | `WB-SETTINGS-RECOVER-404` | 显示恢复提示，不 `CreateDirectory`，旧 settings 原样保留 |
| marker 缺失/损坏/版本不支持 | `WB-SETTINGS-RECOVER-422` | 显示恢复提示，不重写旧 marker，旧 settings 原样保留 |
| 路径命中保护根 | `WB-ROOT-001` | 显示安全提示，不写 marker/settings |
| workspace 是文件 | `WB-ROOT-005` | 显示安全提示，不写 marker/settings |
| 命中 Modules/PlayerExports | `WB-ROOT-006` | 显示安全提示，不写 marker/settings |
| 命中 Bannerlord 游戏根 | `WB-ROOT-007` | 显示安全提示，不写 marker/settings |
| 经过 junction/reparse point | `WB-ROOT-008` | 显示安全提示，不写 marker/settings |
| settings 读取权限不足 | `WB-SETTINGS-READ-403` | 显示错误，旧 settings 保留 |
| 新 settings 写入权限不足 | `WB-SETTINGS-WRITE-403` | 显示错误，旧 settings 保留 |
| marker/settings 原子替换失败 | `WB-SETTINGS-COMMIT-500` | 临时文件回滚，旧 settings 保留 |

正常模式的恢复提示只允许用户“重新选择”或“退出”；`PrepareExisting` 不创建目录，`PrepareNew` 只在用户确认新路径后执行，settings 替换在 marker 与写入探针成功后原子提交。smoke/test mode 对恢复分类直接写结构化结果并退出，不自动选择默认目录。

`LauncherForm` 的默认构造器固定委托给内部构造器 `LauncherForm(IBrowserOpener browser, IWebProcessHostFactory hostFactory, ILauncherEnvironment environment)`；生产路径注入 `ShellBrowserOpener` 和真实 Host，smoke 路径注入仅由环境变量选择的 `SmokeBrowserOpener`，Windows seam test 注入 fake opener/fake host。所有路径都调用同一个 `Open(address)`，`Process.Start` 返回 null 或抛异常才产生 `WB-BROWSER-OPEN-FAILED`；结果 JSON 使用实际 bool，不从 code 反推。

`WebProcessHost` 的内部测试构造器只接受 `IWebProcessNativeApi`、`IWebProcessHealthProbe` 和可注入时钟/超时配置；生产构造器仍使用现有 P/Invoke、HttpClient 与固定常量。所有 seam 类型为 internal，使用精确 friend assembly，不进入 Core 或发布 wire 契约。

## Test drivers and evidence

所有失败通过内部 `LauncherFailure`（`Code`、`Stage`、safe message）传递；Form 不再用 `Exception.Message.Split(':')` 推导 code。每个入口固定日志 event、结构化 `code`、Launcher exit code（成功 `0`，结构化失败 `1`）、`ok`、UI 行为和必需证据；caller-wait cancellation 只在调用方 task 观察到取消，不伪造 Launcher success/failure 结果。

`scripts\\smoke.ps1` 扩展为显式 `-Scenario`，每个场景写 schema `awake.worldbook.launcher-smoke.v2` 的结构化结果：

```json
{
  "scenario": "duplicate-launch",
  "actors": [{"role":"launcher-primary","pid":123,"exitCode":0},{"role":"launcher-secondary","pid":456,"exitCode":1},{"role":"web","pid":789,"exitCode":0}],
  "webExitSnapshot": {"pid":789,"exitCode":0,"exitedObserved":true},
  "postClosePidState": "not_found",
  "settingsHashes": {"before":"...","after":"..."},
  "httpEvents": [{"name":"health","status":200},{"name":"shutdown","status":200}],
  "portState": "freed",
  "code": "WB-INSTANCE-409",
  "browserOpened": null,
  "ok": true,
  "evidenceDeadlineUtc": "..."
}
```

`actors[]` 支持多个 Launcher/Web actor；`webExitSnapshot` 可为 null；`postClosePidState` 枚举为 `not_found|alive|access_denied|reused|not_applicable`；`settingsHashes.before/after` 固定为字节 SHA-256；`httpEvents[]` 分别记录 `health`、`api-health`、`shutdown` 的 status/outcome；`portState` 枚举为 `free|occupied_studio|occupied_non_studio|freed|not_applicable`。每个 scenario 在 fixture 中固定 expected `exitCode`、`code`、`ok`、actor 数、PID 状态、settings 变更和端口状态。

- `clean-start` / `browser-success` / `browser-failure`：现有真实 self-contained package，success/failure 均由注入的 `IBrowserOpener` 返回值驱动。
- `stale-settings-missing` / `stale-settings-marker`：临时 LocalAppData 写入旧 settings，分别删除目录或损坏 marker；不得创建/改写旧路径，不启动 Web。
- `duplicate-launch`：第一个 smoke Launcher 启动 Web 后写入 `<temp>\\ready.json`（含 primary PID/Web PID/continue token）并等待 `<temp>\\release.signal`；驱动读取 ready 后启动第二 Launcher，第二个必须得到 `WB-INSTANCE-409`、不产生第二 Web PID；驱动写 release，等待 primary 退出，再启动第三 Launcher 验证 owner mutex 已释放。
- `startup-cancel`、`stop-cancel`、`forced-cleanup`、`native-*`：不伪装成 package 黑盒场景，放在独立 `Awake.WorldbookStudio.Launcher.Tests` Windows test host；通过 fake health probe/native API/fake host 注入 gate/token/fault，使用 fake PID/handle model，不启动真实 Web、不占用固定端口，测试结束验证 fake owned resources 全部释放。
- `graceful-shutdown`：package smoke 验证真实 Host-owned authenticated shutdown；独立 PID 查询验证退出，并保存 `httpEvents[]`/exit snapshot。
- `start-idempotent` / `stop-idempotent` / `start-stop-race` / `dispose-during-start`：Windows test host 直接并发调用同一 Host，断言内部 `_startTask`/`_stopTask`/`_finalCleanupTask` identity、线性化点、状态终态和 cleanup ownership，不断言 caller-facing `WaitAsync` 包装 task 引用。
- `formclosing-during-start` / `duplicate-close`：Windows STA test host 注入 fake Web host，断言首次 `e.Cancel=true`、一次 cleanup task、一次 `BeginInvoke(Close)`，不访问已关闭控件。

测试执行链固定为：新增 `tests\\Awake.WorldbookStudio.Launcher.Tests\\Awake.WorldbookStudio.Launcher.Tests.csproj`（`net10.0-windows`，精确 friend assembly `Awake.WorldbookStudio.Launcher.Tests`）、加入 `Awake.WorldbookStudio.slnx`；新增 `scripts\\launcher-tests.ps1`，先 Release build Launcher test host，再运行并写 `artifacts\\WorldbookStudio\\smoke\\launcher-tests.v2.json`，退出码非零即阻止 package smoke 和发布检查。发布包不携带 test host、fake native API 或 fake health probe。

Launcher package smoke 只验证 Host 自己发出的 authenticated shutdown 得到 `WB-SHUTDOWN-ACCEPTED` 或明确 rejected/transport 事件，不从外部伪造原始 launch nonce；既有 A4 Web route/proof 测试继续作为独立契约基线。现有 A4 CLI/Web golden、Core public-type 列表、Schema 哈希和 CLI/Web smoke 必须原样通过，Launcher 新测试与 A4 named-case/hash manifest 分离。

## Key decisions & tradeoffs
- 采用最小等价 seam，不重写已有 Launcher，也不把 CLI/Web 状态合并成新的全局服务；代价是保留现有 WinForms/PInvoke 边界，但降低兼容风险。
- 生命周期状态机、mutex ownership、取消和真实退出快照全部限制在 Launcher 程序集；不把状态暴露到 Core，也不增加公共 DTO、CLI command、Web route 或 A4 golden 字段。
- 关闭流程优先“最终回收”而非无限等待优雅 shutdown：优雅关闭仍先尝试，超时或取消只影响等待，不得阻止 Job Object 回收。
- 已保存工作区是用户数据，失效时必须显式提示并让用户重新选择；不为便利而自动创建同名空目录，避免编辑者误以为旧内容仍在使用。
- 不新增端口自动切换、自动下载运行库、API Key 保存或游戏目录同步；这些属于其他批次或明确的安全/发布边界。
- Launcher smoke 继续使用本机真实进程与临时包；它证明的是 Launcher/Web 生命周期，不证明真实云端 Provider、Bannerlord 真机或存档行为。
- Job Object 的保证边界从“进程创建后任何 Launcher 崩溃都能回收”收紧为“进程加入 Job 后及所有可注入失败路径可回收”；`CreateProcess` 返回到 `AssignProcessToJobObject` 之间的极窄崩溃窗口单独记录为未证明风险，不用无证据的绝对表述包装。

## Risks / open questions
- WinForms `FormClosing` 不能直接等待异步任务，需采用一次性取消、后台清理和安全回调关闭窗口；实现必须避免重复 `Close` 和异常吞噬导致 mutex 未释放。
- 现有 smoke 包为 self-contained Windows x64；如果当前机器缺少可运行的发布包，需标记环境阻塞，不把源码编译替代为进程证据。
- 停止阶段的强制回收可能让 Web 来不及写完最后一条日志；这是可接受的退出优先级，但 checkpoint 必须记录。
- shutdown 路由在 Web 业务初始化失败前不可用；本批不重排 Web 初始化，不把“启动失败”包装成 graceful shutdown，必须用 Job/进程回收证据覆盖该路径。
- 端口冲突分类只允许报告实际可观测的事件码；如果当前探测不能区分非 Studio HTTP 与无响应，必须保留统一安全错误，不保留不可达的描述分支。

## Out of scope
- 不修改 Schema、authoring/editor model、CLI command、Web route、AI Provider/CAS、内容包或 AWAKE 游戏运行时。
- 不启动 Bannerlord，不同步 `Modules\\AWAKE`、`PlayerExports`、`dist`、冻结候选或任何游戏目录文件。
- 不改变发布为 MSI、不引入联网更新、不把 `.NET 10` 变成用户预装要求、不处理 ARM64/Linux/macOS。
- 不修改 `Awake.WorldbookStudio.Core` 的公共类型或 A4 CLI/Web golden；不实现 `CreateProcess` 到 Job 接管之间的不可证明崩溃窗口的跨进程守护器。
