# AWAKE Provider 诊断落盘立项 — 2026-09-10

## 立项结论

- 目标：Provider（AI 配置应用 / 拉取模型 / 连接测试 / AI 自检）失败时，**离线可从日志归因** ——
  错误码、错误类别、是否可重试、Provider HTTP 状态码、provider/profile/route、correlation。
- 触发证据（2026-09-10 实机）：`%LOCALAPPDATA%\AWAKE\RuntimeData\awake-runtime.provider-ledger.json`
  记录 `provider-models` 连续三次 `Status=4 (Failed)`（`14:13:20Z` / `14:13:33Z` / `14:14:03Z`），
  随后 `14:14:35Z awake_host_resolution status=runtime_not_ready runtime=Stopped`，
  `14:14:49Z map_shout_open_failed reason=no_host`。AWAKE 侧把失败压成一句泛化文案，
  Runtime 侧 stderr 被读走后丢弃 → 现有证据无法归因到 BaseUrl / 模型名 / API Key / 网络。
- 风险等级：`standard`（跨文件、可逆、无新存档格式、无权限变更、无公共接口变更）。
  通道：审查状态机，`max_rounds=2`（一次独立审查 + 一次修订）。
- 本文件是**立项**。实现授权来自用户 2026-09-10 的"开"（先做 AI 配置失败原因落盘）；
  批次签收与审查轮次见下方"修订记录"与 `docs\review-state\AWAKE-PROVIDER-DIAGNOSTICS-20260910.review.json`。

### 证据路径缩写

- `S:` = `_houkai_merge\AWAKE\src\`
- `F:` = `_houkai_merge\AWAKE\framework\MarcusAwakeFramework\src\`
- `R:` = `_houkai_merge\AWAKE\framework\MarcusAwakeRuntimeService\src\`
- `T:` = `_houkai_merge\AWAKE.Tests\`

## 当前事实（已核实）

### 诊断信息在哪一步丢失

| # | 丢失点 | 证据 |
|---|---|---|
| 1 | AWAKE 把 `FrameworkError` 压成类别文案，丢弃 code / details / status_code | `S/AwakeProviderConfiguration.cs:615-635` |
| 2 | 4+1 个失败分支只调用 `DescribeFailure(...)`，不写 `AwakeLog` | 同文件 `:121`（应用配置）、`:173`（拉取模型）、`:215`（连接测试）、`:392`（AI 自检）、`:438`（API Key 保存） |
| 3 | 只有**异常**路径写日志，操作结果失败不写 | 同文件 `:493` `provider_mcm_operation_failed operation=… error=<exception.Message>` |
| 4 | Runtime stderr 被读到 EOF 后**丢弃**（读取结果未被使用） | `F/RuntimeServiceClient.cs:1329-1339` |
| 5 | Runtime 诊断只写 `Console.Error`，无文件落点 | `R/RuntimeServiceHost.cs:112`、`:155`、`:336`、`:427`、`:673`、`:1581`、`:1818`、`:1997`、`:2018`、`:2026`、`:2415`、`:2668`、`:2675`、`:2679`、`:2693`、`:3355`、`:3369`、`:3383`；`R/Program.cs:19`、`:28` |
| 6 | Provider 结果账本只存 `RequestPayloadHash` / `Status` / `UpdatedUtc`，无原因字段 | `R/RuntimeProviderOutcomeLedger.cs:30-35`、`:117-130` |

### 已经存在、可直接用

- `FrameworkError` 携带 `Code` / `Category` / `Retryable` / `SafeFallback` / `Details` / `CorrelationId`；
  Provider 失败时 `Details` 含 `provider_id` / `profile_id` / `route_id`，有 HTTP 状态码时含 `status_code`
  （`F/FrameworkErrors.cs:25-52`、`F/RuntimeServiceClient.cs:753-768`）。
- `AwakeLog.Write` 写 `Modules\AWAKE\Logs\Awake.log`，带 `Recorder` 测试钩子与 2 MB 轮转（`S/AwakeLog.cs`）。
- `S/AwakeProviderConfiguration.cs` 与 `S/AwakeLog.cs` **已被测试工程编译**（`T/AWAKE.Tests.csproj`）
  → 日志内容可离线断言。
- Runtime 数据根可被环境变量覆盖：`MARCUS_AWAKE_RUNTIME_DATA_ROOT`（`R/RuntimeServiceHost.cs:3299-3311`），
  provider 账本就落在此目录（`:3313-3316`）。

## 决策锁定

| # | 决策点 | 结论 |
|---|---|---|
| D-1 | 范围 | 只做**诊断可见性**；不改失败分类、重试语义、生命周期状态机、用户可见文案 |
| D-2 | AWAKE 侧 | MCM Provider 失败分支写结构化日志行（经 `AwakeLog`）；保留现有中文文案不变 |
| D-3 | Runtime 侧 | Runtime 把自身 `Console.Error` 同步落到 `awake-runtime-service.log`（与 provider 账本同目录、带尺寸轮转）；stderr 本身行为不变 |
| D-4 | 明确不改 | `FrameworkError` 公共类型、`RuntimeServiceClientOptions` 公共属性、provider 账本 JSON 结构、`SubModule.xml` / 版本号 / 既有候选结论 |
| D-5 | 安全 | 只记录 code / category / retryable / status_code / profile 标识 / correlation；不记录 API Key、请求与响应正文、prompt、模型输出 |
| D-6 | 版本 | 另立 BuildId，不改 `004` / `005` 的既有结论 |

## 契约（可观察结果）

`入口` MCM 点「AI 自检 / 连接测试 / 拉取模型 / 应用 AI 配置」
→ `调用` `AwakeProviderConfiguration` 失败分支
→ `结算` 写一行结构化 `Awake.log`（Runtime 侧同时写 `awake-runtime-service.log`）
→ `可观察结果` 离线读日志即可回答：哪一步失败、什么类别、是否可重试、HTTP 状态码、
哪个 provider/profile/route、correlation。

### 验收标准

1. `Awake.log` 出现字段齐全的失败行（`code` / `category` / `retryable` / 存在时的 `status_code` / provider / profile / route / correlation）。
2. 用户可见文案不变（不引入新 MCM 文案、不改本地化）。
3. `awake-runtime-service.log` 在 Runtime **启动后即存在**（`Enable()` 直写 banner 行），
   并在 Runtime 自身输出 stderr 诊断时包含该行。注意口径：预期内的 Provider 失败由 Runtime
   映射成结构化错误帧返回，**不写 stderr**，因此该故障的归因以 AWAKE 侧 `Awake.log` 行为准，
   runtime 侧文件只是补充。
4. 安全断言：日志中不出现凭据、请求/响应正文。
5. 离线回归全绿：`Awake.SdkSmoke`（含 `--persona-anchor` 与主 smoke）、Framework / Runtime 既有套件。

## 非目标

- 不修 Provider 失败本身（BaseUrl / API Key / 模型适配）。
- 不追查本次 `runtime=Stopped` 的直接原因；本批次只保证**下次有据可查**。若日志仍不足，另立批次。
- 不新增 MCM 设置项、不新增公共 API、不动 provider 账本结构。

## 修订记录

### round 1（独立只读审查，2026-09-10）→ REVISE

| 编号 | 级别 | 发现 | 处置 |
|---|---|---|---|
| R1-1 | P1 | `RuntimeServiceLog` 懒创建文件：只有 Runtime 自己写 stderr 时才建文件，而目标故障路径（Provider 失败）根本不写 stderr → 验收标准 3 与实机自检步骤会误判为失败 | `Enable()` 改为直写 banner 到文件再挂 tee（stderr 字节不变）；验收标准 3 与实机步骤同步改写 |
| R1-2 | P2 | 三个用例都直接调 `CreateTee`，绕过 `Enable()` / `ResolvePath()`，`MARCUS_AWAKE_RUNTIME_DATA_ROOT` 覆盖路径无覆盖 | 增加第 4 个用例（环境变量覆盖 + 建文件 + banner + stderr 镜像），并还原环境与 `Console.Error` |
| R1-3 | P3 | 立项写"未签收前不改实现"，但实现已先行 | 随审查状态机落账，不追加代码改动 |

审查者同时确认通过：用户可见文案零改动（IL 字面量差分）、无凭据/正文落盘、无漏改调用点、
`FrameworkError` / 账本结构 / `SubModule.xml` / 版本号均未触碰、smoke 为真断言、10 个源码哈希逐字一致。

## 待用户签收

- [ ] 用户签收本批次（审查状态机 `round=1 REVISE → 修订 → round=2 待判`）。
