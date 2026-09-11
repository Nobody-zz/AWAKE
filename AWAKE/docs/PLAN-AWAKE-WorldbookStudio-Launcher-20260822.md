# Plan: AWAKE Worldbook Studio Editor-Friendly Launcher
_Locked via design continuation — by Codex + user, 2026-08-22_

## Goal

把当前仅适合开发者手动运行的 Worldbook Studio 发布包，升级为 Windows x64 内容编辑者可直接使用的免安装便携包：不要求用户预装 .NET 或设置环境变量，双击启动器即可完成首次工作区准备、启动本地 Web 编辑器、打开浏览器、显示中文错误和安全退出；不写入游戏目录，不修改冻结 AWAKE 运行时候选。

## Approach

1. 新增 `Awake.WorldbookStudio.Launcher` WinForms `WinExe`，负责首次运行向导、工作区选择、子进程生命周期、健康检查、浏览器打开、中文错误提示、日志和安全退出。
2. 将默认用户数据与程序目录分离：默认工作区为系统“文档\\AWAKE\\WorldbookStudio”，启动器设置与日志位于当前用户 LocalAppData；程序包只读携带 Web、CLI、schemas 和模板。
3. 将 Launcher、Web、CLI 以 `win-x64`、`self-contained=true` 发布；Launcher 使用单文件入口，Web 保留静态资源目录，发布前清理旧输出并验证包内 runtime 文件和真实启动，不要求用户安装 .NET 10，不自动联网下载运行库。
4. 固定包布局为根目录 Launcher、`web`、`cli`、`schemas` 和说明文件；所有入口从 `AppContext.BaseDirectory` 解析，不使用包目录或当前目录作为用户工作区 fallback。
5. 启动时仅允许启动包内相对路径的 Web EXE，并通过受控启动管道传入工作区、schemas、实例 ID 和启动 nonce；健康端点返回协议版本、实例标识和不可逆 workspace 标识，Launcher 区分本 Studio、旧实例、其他程序占用和启动失败。
6. Launcher 使用单实例锁和 Windows Job Object 管理 Web 子进程；正常退出先请求受控关闭并等待，异常退出由 Job Object 回收，禁止误杀非本 Launcher 创建的进程。
7. 首次运行提供中文选择：使用默认工作区、选择已有工作区或打开空白工作区；统一校验 package root、游戏根、UNC/映射盘、`.`/`..`、junction/reparse point 和读写权限，失败时只能重新选择或退出，禁止静默 fallback。
8. 增加 Launcher 诊断与 smoke：自包含包文件完整性、缺少/损坏文件、端口冲突、Web 启动失败、健康超时、浏览器打开失败、关闭后子进程退出、重复启动和日志脱敏；日志只记录事件码、阶段、端口、退出码和哈希前缀，不记录 API Key、Authorization、Cookie、完整 URL 或完整路径。
9. 更新发布脚本、包清单、相对路径清单、中文使用说明和测试；继续保留 CLI 作为高级工具，不让普通编辑者接触命令行。

## Implementation contract

### Package layout

发行包根目录固定为 `<package>`，不得把用户工作区放在包内：

```text
<package>/Awake.WorldbookStudio.Launcher.exe
<package>/web/Awake.WorldbookStudio.Web.exe
<package>/web/wwwroot/**
<package>/cli/worldbook-studio.exe
<package>/schemas/**
<package>/README_使用说明.txt
<package>/manifest.json
<package>/SHA256SUMS.txt
```

Launcher 只允许启动 `<package>/web/Awake.WorldbookStudio.Web.exe`；`cli` 仅供高级维护，不由首次运行流程启动。生产模式只接受 Launcher 注入的绝对路径：`AWAKE_WB_WORKSPACE`、`AWAKE_WB_SCHEMA_ROOT`、`AWAKE_WB_PACKAGE_ROOT`，以及实例 ID 和匿名 bootstrap 管道句柄 `AWAKE_WB_INSTANCE_ID`、`AWAKE_WB_BOOTSTRAP_HANDLE`。缺失、相对或不在预期根目录内时立即失败；Web 不再使用 `Environment.CurrentDirectory` 推导 workspace 或 schema。

### Health and ownership protocol

固定监听 `127.0.0.1:5077`。Launcher 每次新建实例生成 `Guid.NewGuid().ToString("N")` 的 `instanceId` 和至少 32 字节随机 `launchNonce`；Web 只在内存中保存原值，并在 `GET /health` 返回以下无秘密字段：

```json
{
  "ok": true,
  "product": "AWAKE.WorldbookStudio",
  "protocolVersion": 1,
  "instanceId": "...",
  "launchNonceHash": "sha256-16-hex",
  "workspaceIdHash": "sha256-16-hex",
  "port": 5077
}
```

`launchNonceHash` 是完整 nonce 的 SHA-256 前 16 个十六进制字符；`workspaceIdHash` 是规范化 workspace 路径的 SHA-256 前 16 个十六进制字符，绝不返回原路径。Launcher 只有在 `product`、协议版本、端口、实例 ID、nonce hash 和 workspace hash 全部匹配时才接受新 Web；健康响应不匹配时不得把端口误认为本次启动成功。关闭接口为 `POST /api/launcher/shutdown`，只接受 loopback、无 `Origin` 的请求头 `X-AWAKE-Instance-Id` 与 `X-AWAKE-Shutdown-Proof`；proof 为 `HMAC-SHA256(launchNonce, "shutdown:v1:" + instanceId)` 的十六进制值，禁止把秘密放进 URL、query 或 body。接口以原子状态只接受一次，重复请求返回幂等成功，成功后 Web 停止监听。

### Launcher startup state machine

启动顺序固定为：单实例互斥锁 → 读取/完成工作区设置 → 校验包文件和 schema → 探测固定端口 → 创建 Job Object → 以 suspended 状态创建 Web → 加入 Job → 恢复 Web → 健康握手 → 打开浏览器。锁持有者同时写入仅当前用户可读的临时 runtime 元数据（`instanceId`、Launcher PID、端口），用于第二次启动把互斥锁、PID 存活和 health `instanceId` 关联；元数据只做诊断，内核互斥锁生命周期才是权威，不依赖残留文件。状态结果固定如下：

- 已持有 `Local\\AWAKE.WorldbookStudio.Launcher` 互斥锁：显示“编辑器已经在运行”，不创建第二个 Web。
- 端口返回完整匹配的健康响应但没有本次锁：显示“已有 Studio 实例或旧启动器占用”，只提供复制地址/退出，不自动接管已有 PID。
- 端口返回非 Studio 响应或监听错误：显示“5077 端口被其他程序占用”，不自动换端口。
- 无响应后 Web 创建成功但进程提前退出：读取退出码和已脱敏的 stderr 事件，显示启动失败。
- Web 存活但健康握手超时或字段不匹配：终止本次 Job 中的 Web，显示“版本/实例校验失败”。

Job Object 设置 `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`，句柄由 `SafeHandle` 持有并由 `try/finally` 覆盖所有启动、UI 异常和退出路径。Web 使用 `CreateProcess` + `CREATE_SUSPENDED` 创建，加入仅属于本次 Launcher 的 Job 后才 `ResumeThread`；加入失败时先终止 suspended PID，再关闭线程、进程和 Job 句柄，绝不把未成功加入 Job 的进程当作已启动。禁止把已有 PID 加入 Job。正常退出先发送受控 shutdown，最多等待 2 秒，超时只终止本次 Job；Launcher 崩溃或被强制结束时依赖 Job 句柄关闭回收 Web。子进程 stdout/stderr 不直接显示或写日志，统一经过事件码脱敏器。

### Workspace transaction and path policy

Core 提供唯一 `WorkspacePathPolicy`，Launcher 的首次运行和 Web 的所有读写共用同一规则。用户选择的目录本身就是允许的 workspace 根，不另造含义不清的“安全根”。策略输入为 `packageRoot`、`schemaRoot` 和可选 `immutableRoots`；逐级规范化绝对路径，解析现有目录的 junction/reparse target，要求最终路径不位于包目录、游戏目录、`Modules\\AWAKE`、`PlayerExports`、`dist`、`_build_out`、`candidate_frozen` 或 `pending_game` 内，也不允许把这些受保护目录作为 workspace 祖先。拒绝空路径、文件、磁盘根、不可解析的 UNC/映射盘路径、路径穿越和备用数据流；正常的 OneDrive 文档目录可以作为 workspace。

每次创建目录或写文件前都重新检查父链和最终句柄：新目录按组件逐级创建并在每次创建后用 `CreateFileW(FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS)` + `GetFinalPathNameByHandleW` 核对最终路径；文件先在已核对的父目录中写入同卷临时文件，替换前再次核对父目录句柄后用原子 `File.Move`，失败时删除本次临时文件。初始化与 Web 写入口都复用这条路径策略，测试覆盖“检查后替换 junction”故障注入。

Launcher 的 `packageRoot` 永远由自身 `AppContext.BaseDirectory` 取得，不接受用户覆盖；固定子路径只有 `web`、`cli` 和 `schemas`。Web 根据自身目录反推 `<package>`，要求 `AWAKE_WB_PACKAGE_ROOT` 与该值规范化后完全一致，且 `AWAKE_WB_SCHEMA_ROOT` 必须等于 `<package>\\schemas`；不符合或缺失时拒绝启动。

设置文件固定为 `%LOCALAPPDATA%\\AWAKE\\WorldbookStudio\\settings.json`，版本为 `1`，只保存最后确认的 workspace 绝对路径和语言。默认路径为 `Environment.SpecialFolder.MyDocuments\\AWAKE\\WorldbookStudio`；文档目录解析失败时直接报错，不回退到当前目录、临时目录或程序目录。首次初始化顺序为：路径解析 → 读写临时文件探针 → 创建必要目录 → 原子写入 `.awake-worldbook-workspace.json` marker → 原子写入 settings；任一步失败都保留旧 settings，并清理本次新建且仍为空的 marker/目录。已有 workspace 只接受有效 marker，或空目录并由用户确认初始化；非空陌生目录、只读目录和 marker 版本不支持必须重新选择或退出。

### Browser and diagnostics

浏览器通过可替换的 `IBrowserOpener` 调用 `Process.Start`，只使用本地固定地址 `http://127.0.0.1:5077/`。失败、没有默认关联或异常时保留 Web 和 Launcher 生命周期，显示中文错误、可复制地址、重试和退出按钮；日志不写完整 URL。

日志写入 `%LOCALAPPDATA%\\AWAKE\\WorldbookStudio\\logs\\launcher-YYYYMMDD.log`。统一 sanitizer 只允许事件码、阶段、端口、PID、退出码、异常类型、固定长度 hash 前缀和 correlation ID；异常消息、stderr、请求正文、API key、Authorization、Cookie、完整路径和 URL 一律不落盘。Web API 也只返回错误码与安全中文提示，不把原始异常消息直接返回浏览器。

### Release smoke contract

`package.ps1` 必须执行 `clean → offline restore → Release build → win-x64 self-contained publish → package manifest/hash → release-check → extract-to-temp smoke`。Smoke 固定清空/移除 `DOTNET_ROOT` 与 `DOTNET_ROOT(x86)`，从 ZIP 解压目录启动 Launcher，使用临时 Documents/LocalAppData，验证：Launcher/Web/CLI 都能直接运行、包内 runtime 文件存在、健康字段全匹配、工作区不在包目录或游戏目录、浏览器成功路径由 `IBrowserOpener` 的 fake-success 注入测试、浏览器失败由 fake-failure 注入并断言 smoke result 中的中文事件码/可复制地址状态且 Web 仍存活、重复启动不创建第二个 Web、端口冲突分类正确、Launcher 退出后 Web PID 不存在、日志中不存在秘密/完整路径/URL。Launcher 提供仅供 smoke 使用的无 UI 模式，输入 `AWAKE_WB_SMOKE=1`、`AWAKE_WB_SMOKE_BROWSER=success|failure` 和结果文件路径，输出结构化 `smoke-result.json`，正常用户流程不显示该入口。Smoke 输出 `manifest`、ZIP 外部 SHA-256 sidecar、环境摘要、每项断言和退出码；本批完整性目标仅防传输/部署损坏，不宣称抗篡改，签名和 Authenticode 留到发行批次。`release-check.ps1` 只接受新布局，发现旧 framework-dependent DLL-only 布局、缺少 `Launcher.exe`、缺少 runtime 或缺少 `wwwroot` 时直接失败。

## Final implementation checklist

### Web contract migration

- Web 的权威健康端点为 `GET /health`；为兼容开发期探测可暂时保留 `/api/health`，但生产 Launcher 只调用 `/health`，两者必须返回同一版本化 DTO，不能各自拼接字段。
- Web 启动配置只接受 `AWAKE_WB_WORKSPACE`、`AWAKE_WB_SCHEMA_ROOT`、`AWAKE_WB_PACKAGE_ROOT`、`AWAKE_WB_INSTANCE_ID` 和 `AWAKE_WB_BOOTSTRAP_HANDLE`。旧的 `WORLD_BOOK_WORKSPACE`、`WORLD_BOOK_SCHEMA_ROOT`、当前目录 fallback 和 `FindSchemaRoot` 的开发路径候选只能保留在显式 `AWAKE_WB_DEV_MODE=1` 下；发行包不设置该变量，缺失生产配置直接退出并返回 `WB-BOOTSTRAP-001`。
- Web 必须把 `/api/launcher/shutdown` 注册在应用服务创建前或独立的最小 host 上，避免 workspace 载入失败时无法回收；shutdown 校验失败只返回固定错误码，不返回异常文本。
- 现有 `/api/health`、环境变量和默认 workspace 行为属于必须移除/隔离的旧路径；实现 checklist 必须以 `rg` 证明发行构建中没有可达的当前目录 workspace fallback。

### Bootstrap pipe protocol

- Launcher 创建匿名继承管道：父端仅在 Launcher 持有，子端句柄设为可继承；仅把子端整数句柄通过 `AWAKE_WB_BOOTSTRAP_HANDLE` 传入，禁止把 nonce 放入命令行或环境变量。Launcher 在 `CreateProcess` 前关闭子端在父进程的副本，并在写完后关闭父端。
- 首包是 UTF-8 JSON，带 4 字节 little-endian unsigned length；长度必须在 `1..65536`，内容必须一次性包含 `protocolVersion=1`、`instanceId`、原始 `launchNonce`、规范化 workspace、schemaRoot 和 packageRoot。Web 从管道读取首包，读取总超时 5 秒，长度/JSON/字段/路径任一失败都以 `WB-BOOTSTRAP-002` 退出，不监听端口。
- 管道只存在于本次启动；Launcher 生成至少 32 字节 nonce，写入后立即清零发送缓冲并关闭句柄。Web 只在内存保存 nonce，健康端点只返回 hash，日志、异常、环境快照和诊断文件禁止输出原值。
- 子进程创建使用 `bInheritHandles=true`，并通过 `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` 只允许 bootstrap pipe 子句柄继承；若属性初始化或句柄继承失败，Launcher 不 Resume Web，走启动失败清理路径。

### Fixed-port ownership and bind race

- Launcher 不把“探测端口空闲”当作成功条件；探测只用于区分明显的已有响应。真正的成功条件是本次 Web 进程完成 bind 后的 `/health` 完整握手。
- 若 Web 因 `AddressInUse`/WinSock `10048` 退出，分类为 `WB-PORT-409-OTHER`，不重试、不换端口；若进程存活但健康超时，分类为 `WB-PORT-408-HEALTH_TIMEOUT`，终止本次 Job；若收到非匹配 HTTP 响应，分类为 `WB-PORT-409-NON_STUDIO`，不发送 shutdown 给陌生服务。
- 端口探测、进程创建和健康轮询都使用 correlation ID；所有失败路径先关闭 bootstrap pipe，再回收本次 Job，最后释放单实例锁。启动器只等待固定 10 秒健康窗口，轮询间隔 100ms，禁止无限等待。
- 第二次启动先尝试内核互斥锁；获取失败时只读取健康端点和当前用户 runtime 元数据，完整匹配才显示“已在运行”，不完整匹配统一显示端口/实例冲突，不接管、不杀进程。

### Workspace path policy

- 受保护根的固定列表为：游戏根 `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`、其 `Modules\\AWAKE`、`Modules\\AWAKE\\ModuleData`、`Modules\\AWAKE\\PlayerExports`、`Modules\\AnimusForge\\PlayerExports`，以及 AWAKE 项目根下的 `dist`、`_build_out`、`candidate_frozen`、`pending_game`。传入 `immutableRoots` 时追加规范化后的绝对路径。
- 受保护根除固定列表外，还必须执行安装树识别：向上查找用户选择路径的祖先，若祖先目录名为 `Mount & Blade II Bannerlord` 且同时存在 `Modules\\Native\\SubModule.xml`、`Modules\\SandBoxCore\\SubModule.xml` 或 `bin\\Win64_Shipping_Client` 中任一当前版本运行文件，则将该祖先视为 Bannerlord 游戏根并拒绝；若路径位于任意 `Modules` 子树、`PlayerExports` 子树或识别出的游戏根内同样拒绝。无法读取祖先或存在歧义时 fail closed，不允许用“不是默认 D 盘”作为放行依据。
- 比较前统一去除 Win32 `\\?\\` 前缀、规范化大小写、去除末尾分隔符；通过最终句柄解析处理现有 reparse point；拒绝 `\\Device\\`、备用数据流 `path:stream`、磁盘根、文件目标、不可解析的 UNC 和映射盘路径。只允许 workspace 位于受保护根之外。
- workspace 内的普通文件不允许逃逸其 workspace 根；所有读写入口必须经过 Core `WorkspacePathPolicy`，对父目录 reparse point 和最终句柄进行检查。测试必须包含 drive-case、`\\?\\`、UNC、junction、检查后替换 junction、`..` 和 ADS。

- Bootstrap JSON 是唯一运行时配置权威；环境变量只承担 `workspace`、`schemaRoot`、`packageRoot` 的启动前定位和管道句柄传递。Web 读取管道首包后，必须把其中三条路径与环境变量值分别规范化，再逐字节比较规范化 UTF-8 表示；`instanceId` 也必须与 `AWAKE_WB_INSTANCE_ID` 完全相等。任一缺失、格式不同、大小写/最终句柄不一致或环境变量多余覆盖都以 `WB-BOOTSTRAP-003` 退出，不允许“以管道优先”或“以环境优先”。健康、workspace hash 和 shutdown proof 全部只基于通过比较后的 bootstrap 值。

### Bootstrap handle representation

- `AWAKE_WB_BOOTSTRAP_HANDLE` 固定为无符号十进制字符串，使用 `ulong.TryParse(..., NumberStyles.None, CultureInfo.InvariantCulture)` 解析；值必须非零且不超过当前进程指针宽度，32 位进程拒绝大于 `uint.MaxValue` 的值。禁止十六进制、空白、符号、路径或句柄名称。
- Web 使用 `OpenProcess` 不参与句柄获取，只用 `SafeFileHandle(new IntPtr((long)handle), ownsHandle: true)` 接管已继承句柄，并通过 `GetFileType`、`GetNamedPipeInfo` 确认它是当前匿名 pipe 的可读端；类型不符、读取端不可用、EOF 前后重复使用、超过一次首包或父端 PID 不符合当前启动关联时以 `WB-BOOTSTRAP-004` 退出。
- Launcher 在创建子进程时用 `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` 仅继承该句柄；管道首包读取成功或任何失败后 Web 立即关闭句柄，Launcher 关闭父端并等待 EOF。句柄不能通过命令行、日志、health、错误响应或 runtime 元数据再次暴露。

### Windows lifetime ownership

- `JobHandle`、`ProcessHandle`、`ThreadHandle`、bootstrap pipe handle 和 mutex handle 均由独立 `SafeHandle` 持有；所有权转移表必须写入代码旁的测试，禁止同一 native handle 双重释放。
- 生命周期顺序固定为 `CreateJob → SetKillOnClose → CreatePipe → CreateSuspendedProcess → AssignProcessToJobObject → close parent pipe duplicate → ResumeThread`。任一步失败都执行 `TerminateProcess`（仅本次 PID）、等待 2 秒、关闭线程/进程/管道/Job，并释放 mutex。
- 正常退出发送已认证 shutdown，等待 Web 进程最多 2 秒；超时调用 `TerminateJobObject`，随后验证 PID 不再存在。UI 异常、未处理异常和窗口关闭都进入同一 finally 清理路径。

### Publish and manifest contract

- Launcher/Web/CLI 工程统一 `net10.0-windows`（Core 继续 `net10.0`）；Launcher 使用 `<OutputType>WinExe</OutputType>`、`<UseWindowsForms>true</UseWindowsForms>`、`<RuntimeIdentifier>win-x64</RuntimeIdentifier>`、`<SelfContained>true</SelfContained>`、`<PublishSingleFile>true</PublishSingleFile>`。Web/CLI 使用 `win-x64`、self-contained，Web 不单文件以保留 `wwwroot`。
- 发布前删除目标目录；发布命名固定为 `Awake.WorldbookStudio.Launcher.exe`、`Awake.WorldbookStudio.Web.exe`、`worldbook-studio.exe`。脚本不得复制旧 `bin`/`obj` 或 framework-dependent DLL-only 输出。
- `manifest.json` 固定为 `{ "schemaVersion": 1, "product": "AWAKE.WorldbookStudio", "rid": "win-x64", "selfContained": true, "files": [{"path":"...","sha256":"64-hex","length":123}] }`；路径使用 `/`、相对包根、大小写保留但比较不区分大小写。`manifest.json` 和 `SHA256SUMS.txt` 自身不进入 files 清单，校验器先验证清单覆盖所有其他文件且无重复/遗漏，再验证 hash/length。
- `SHA256SUMS.txt` 使用同一相对路径规范；发布脚本另外生成 `<zip>.sha256` 作为包外传输校验。release-check 必须拒绝清单缺失、额外可执行文件、hash/length 不匹配、绝对路径、旧 DLL-only 布局和缺少 runtime。

### Smoke test matrix

每项测试都有临时根目录、固定超时、结构化断言和清理动作；统一命令为 `scripts\\package.ps1 -RunSmoke`，结果写入 `artifacts\\WorldbookStudio\\smoke\\smoke-result.json`：

| Case | Setup | Pass condition |
|---|---|---|
| clean-start | 空 Documents/LocalAppData，移除 DOTNET_ROOT | 启动成功，workspace 与包根不同，health 全字段匹配 |
| stale-settings | settings 指向不存在目录/旧 marker | 显示可恢复错误，不写新 settings，不启动 Web |
| marker-rollback | 模拟 marker 原子替换失败 | 旧 settings 保留，无半成品 marker，子进程退出 |
| bootstrap-malformed | 管道长度越界/JSON 截断/超时 | `WB-BOOTSTRAP-002`，Web 不监听，Job 回收 |
| port-race | 另一进程在 probe 后绑定 5077 | `WB-PORT-409-OTHER`，不换端口，不误杀其他 PID |
| non-studio-port | 5077 返回非 Studio HTTP | `WB-PORT-409-NON_STUDIO`，不发送 shutdown |
| bad-health | Web 返回字段缺失/nonce 不匹配 | `WB-HEALTH-409`，只回收本次 Web |
| job-failure | 注入 AssignProcessToJobObject 失败 | 不 Resume，子进程不存在 |
| browser-success | `IBrowserOpener` fake-success | `browserOpened=true`，Web 继续存活 |
| browser-failure | `IBrowserOpener` fake-failure | 中文事件码、地址复制状态存在，Web 继续存活 |
| duplicate-launch | 第二个 Launcher | 无第二 Web，返回已有实例状态 |
| graceful-exit | 正常关闭窗口 | shutdown 认证成功，2 秒内 Web PID 消失 |
| crash-cleanup | 强制结束 Launcher | Job close 后 Web PID 消失 |
| log-redaction | 注入路径、URL、API key、stderr | 日志只含白名单事件字段，不含秘密/完整路径/URL |

### Implementation gate

写代码前必须先完成一项清单核对：新增 Launcher 工程并加入 `.slnx`；Core 增加共享路径/设置/协议模型；Web 完成生产启动配置、`/health`、shutdown 和旧 fallback 隔离；脚本完成清理、发布、manifest、ZIP、sidecar hash 和 smoke；新增测试项目覆盖上表；更新中文说明和当前 checkpoint。实现完成后，必须以 `rg`、构建输出、发布目录清单、ZIP 解压 smoke 结果和进程观察共同证明没有旧路径继续可达。

## Key decisions & tradeoffs

- 采用免安装便携 ZIP，不做首批需要管理员权限的 MSI/安装器；代价是包体积增加，但能彻底消除 .NET 前置依赖和安装权限问题。
- 首批只发布 Windows x64；不在本批引入 ARM64、x86、macOS 或 Linux 分支。
- Web 使用随包自包含运行时但保留 `wwwroot`，避免静态资源和单文件发布组合造成运行时不确定性。
- 默认工作区放在用户文档目录，程序目录只读；作者可以通过首次运行向导选择其他合法工作区。
- AI Provider 的真实密钥配置不由启动器保存或打印；AI 未配置不阻塞编辑、校验、预览和编译，Provider 设置继续走受控高级配置路径。
- 固定端口仍作为首选兼容地址，但 Launcher 先识别健康实例；端口冲突不会自动换端口，避免 Web Origin/CSRF 契约和用户排障复杂化。

## Risks / open questions

- 某些杀毒软件可能对自包含单文件 Launcher 首次运行进行扫描；启动器必须把可操作日志位置显示给用户。
- Windows 默认浏览器可能不可用；启动器应给出复制本地地址和手动打开的备用按钮。
- OneDrive/重定向文档目录可能短暂不可写；首次运行必须报告权限和实际路径，而不是静默切换到程序目录。
- 自包含包无法在当前机器完全模拟“未安装 .NET”的系统，但可以验证包内 runtime 文件、清空 `DOTNET_ROOT` 并从发布目录直接运行。
- Windows Job Object、WinForms 和 self-contained 发布必须在当前 Windows x64 环境实测；测试不得把旧 framework-dependent artifacts 当成新包。

## Out of scope

- 不启动 Bannerlord，不同步 `Modules\\AWAKE`、`PlayerExports`、`dist` 或冻结候选。
- 不实现游戏内世界书编辑、NPC 学习或运行时读取器改造。
- 不实现自动下载运行库、自动更新、MSI 安装器、系统注册表开机启动或云端账号登录。
- 不把 API Key 写入工作区、日志、包清单或普通用户界面。
