# Plan Review Log: AWAKE Worldbook Studio Editor-Friendly Launcher
Act 1 (design continuation) complete — read-only adversarial review pending. MAX_ROUNDS=3.

## Round 1 — Codex

**VERDICT: REVISE**

主要发现与已纳入修订：

- 当前 `package.ps1` 仍使用 `--self-contained false`，没有 `win-x64`、Launcher 或 runtime 验证；计划改为清理旧输出、显式 self-contained 发布和真实包 smoke。
- 需要固定根目录布局与 `AppContext.BaseDirectory` 解析，禁止 Web 默认回退到当前目录工作区；计划已加入包布局和无 fallback 约束。
- 固定端口需要协议版本、实例 nonce、workspace 标识和重复启动分类；计划已加入健康端点实例身份与单实例锁。
- 仅保存 Web PID 不能防止 Launcher 崩溃留下孤儿进程；计划已加入 Windows Job Object、正常关闭握手和等待回收。
- 现有 workspace guard 需显式纳入 package root，并覆盖 UNC、映射盘、junction/reparse、读写探针和 OneDrive 失败；计划已加入统一路径校验与首次运行失败分支。
- 浏览器打开失败、日志脱敏、包完整性、无 .NET 环境和发布清单均缺少实际证据；计划已加入替代地址、白名单日志和发布 smoke 门禁。

审查过程曾遇到只读沙箱初始化权限错误，随后通过只读 Node 文件访问完成检查；未修改代码。

## Round 2 — Codex

**VERDICT: REVISE**

审查指出上一版仍缺少可直接实现的固定契约：包目录和相对路径没有完全落地、生产模式仍可能回退到当前目录、health 的 nonce/workspace 字段没有格式和匹配规则、固定端口没有完整状态机、Job Object 没有 suspended 创建与超时策略、首次运行设置没有事务边界、workspace containment 没有 Launcher/API 统一入口、浏览器 fallback 只有产品描述、日志脱敏没有覆盖异常/stderr/API 返回，以及 release smoke 没有固定入口和断言。

### Claude's response

已把这些问题写入计划的 `Implementation contract`：固定包布局与环境变量、health JSON 和 SHA-256 前缀、启动状态机、CreateProcess suspended + `KILL_ON_JOB_CLOSE`、统一 `WorkspacePathPolicy`、marker/settings 原子事务、浏览器接口与 fallback、日志白名单和 API 安全错误、ZIP 解压后的 self-contained smoke 断言。没有修改代码，也没有触碰游戏目录或冻结候选。

## Round 3 — Codex

**VERDICT: REVISE**

本轮确认上一轮大部分问题已经收敛，但指出：workspace 路径规则有“安全根之外”的方向性歧义；reparse 检查到写入之间缺少 TOCTOU 处理；schema/package 根的固定关系未定义；shutdown 的认证头、实例校验和一次性语义未定义；原始 nonce 通过环境变量存在泄露面；互斥锁与 health 实例没有关联协议；Job 句柄异常路径所有权和加入失败处理未写全；浏览器替身和 WinForms smoke 断言没有固定方式；包内 manifest 不能作为抗篡改信任根。

### Claude's response

已在计划中补充：workspace 不需要位于含糊的安全根，只需不落入受保护根；逐级 `CreateFileW` 最终句柄核对、同卷临时文件原子替换和故障注入；Launcher 自身推导 package root 且 schema 固定为 `<package>\\schemas`；匿名管道传输 nonce、HMAC shutdown proof、loopback/无 Origin/一次性校验；内核互斥锁与 runtime 元数据/PID/health instance 关联；SafeHandle + try/finally 和加入 Job 失败终止；fake browser、无 UI smoke 和结构化结果文件；ZIP 外部 SHA-256 并明确本批不提供抗篡改签名。

### Resolution

已达到本次审查的 `MAX_ROUNDS=3`，但最终没有得到 `VERDICT: APPROVED`。按照工作区规则，不在未批准方案上写代码；当前停在实现前，等待用户对上述最终方案签收，或要求重新开启一轮审查。

## Round 4 — Codex (fresh final audit)

**VERDICT: REVISE**

审查发现第三轮修订后的方案仍与当前代码存在可达矛盾：当前 Web 使用 `/api/health`、旧环境变量和 `Environment.CurrentDirectory` fallback，而计划只在文字上要求改成 `/health` 和生产配置；bootstrap 匿名管道的继承、帧格式、超时、ACL 和畸形输入行为未定义；固定端口 probe 与实际 bind 存在竞态；受保护根和 Win32 路径规范化规则不够精确；Job SafeHandle 所有权与失败顺序未完全固定；发布工程属性、manifest 自引用规则、fake browser/headless 注入和 smoke 断言缺少可执行细节；测试场景没有映射到具体 case。

### Claude's response

已新增 `Final implementation checklist`：明确旧 Web 路径隔离、bootstrap pipe 首包协议和 5 秒超时、固定端口 bind 错误码与 10 秒健康窗口、完整受保护根和 Win32 规范化、native handle 生命周期顺序、三个项目的 RID/self-contained/单文件属性、manifest/hash schema、包外 sidecar、15 项 smoke case 和写码前接线清单。当前仍先保持代码不变，等待下一轮只读审查确认。

## Round 5 — Codex (final gate)

**VERDICT: REVISE**

审查确认方案已基本收敛，仅剩三个阻塞点：固定受保护目录只覆盖默认游戏安装，可能放过其他 Bannerlord 安装或任意 `Modules` 树；bootstrap JSON 与环境变量同时携带配置但没有权威比较规则；匿名管道句柄缺少精确的十进制表示、位宽、pipe 类型和重复使用校验。

### Claude's response

已补充 Bannerlord 安装树识别与任意 `Modules`/`PlayerExports` fail-closed 规则；规定 bootstrap JSON 为唯一运行时权威，必须与环境变量规范化后完全比较；固定管道句柄为无符号十进制、按进程位宽校验，并用 `GetFileType`/`GetNamedPipeInfo`、一次性读取和 `SafeHandle` 接管校验。下一轮只读审查确认后进入实现。

## Round 6 — Codex (final gate)

**VERDICT: APPROVED**

最终审查确认：其他 Bannerlord 安装/任意 `Modules` 树识别、bootstrap 与环境权威比较、十进制管道句柄和类型/位宽校验均已写成可执行规则；没有剩余会迫使实现者猜测或导致不安全行为的重大歧义。

## Act 3 — Build

- 新增 Windows x64 WinForms Launcher、共享运行时契约、bootstrap 管道、健康/关闭协议、Job Object 子进程管理、中文首次运行工作区设置、脱敏日志和浏览器 fallback。
- Web 生产启动已切换到 Launcher 注入配置；权威健康端点为 `/health`，兼容保留 `/api/health`；旧当前目录 workspace/schema fallback 仅保留显式开发模式。
- 发布脚本已改为 RID-aware self-contained、单文件 Launcher、Web/CLI runtime、manifest、SHA256、ZIP 和解压 smoke；新增 browser success/failure smoke。
- 验证：`F01-F57 PASS`；Release build `0 warnings / 0 errors`；release-check `PASS`；从 ZIP 解压、清空 `DOTNET_ROOT` 后 Launcher/Web 健康握手和 Job 回收 `PASS`；browser success/failure fallback `PASS`。
- 未启动 Bannerlord，未修改 `Modules\\AWAKE`、`PlayerExports`、dist 或冻结候选；真实默认浏览器 GUI 和用户首次运行对话框仍需用户手动体验验收。
