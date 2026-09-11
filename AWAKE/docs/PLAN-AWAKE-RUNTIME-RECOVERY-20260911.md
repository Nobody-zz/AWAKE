# AWAKE Runtime 掉线自助恢复立项 — 2026-09-11

## 立项结论

- 目标：Runtime Service 一旦进入 `Stopped`（例如一次健康检查被判为协议异常后客户端按 fail-closed
  拆掉它），AWAKE 侧要能在**同一局战役内自助重启**，而不是整局只剩「尚未就绪」。
- 触发证据（2026-09-10 实机，BuildId `awake-20260910-baseurl-tolerance-007`）：
  - `15:56:33Z` 用户存 Key → `15:56:39Z` 账本 `provider-models` **`Status=1`**（**拉模型成功**）。
  - `15:57:04Z` `runtime_health` → `runtime.ipc_integrity_failed` `protocol_detail=response_health_payload_invalid`。
  - 之后 `15:57:05Z` / `15:57:12Z` 全部 `provider_runtime_unavailable`「尚未就绪」；
    `15:58:05Z awake_host_resolution status=runtime_not_ready runtime=Stopped`。
  - 早前 `14:14:35Z` 同一现象。
- 风险等级：`standard`（跨文件、可逆、无新存档格式、不动公共接口与 Runtime 包）。通道：审查状态机，`max_rounds=2`。
- 本文件是**立项**；实现授权来自用户 2026-09-11 的「修」。

## 机制（已核实的代码链路）

| # | 事实 | 证据 |
|---|---|---|
| 1 | 健康响应必须逐字等于 `marcus-awake.health.v1` + `{"state":"ready","ledger":"non_durable"}`，否则抛协议异常 | `F/MarcusAwakeFramework/src/RuntimeServiceClient.cs:2060` |
| 2 | `CheckHealthAsync` 的每个 catch（取消 / 超时 / 协议 / 其他）都调 `FailConnectionAsync` | 同文件 `:199-253` |
| 3 | `FailConnectionAsync` = `state=Draining` → 关连接 + **杀 Runtime 进程** → `state=Stopped` | 同文件 `:1537-1556` |
| 4 | 之后全无重启路径：`StartRuntimeService` 只在 campaign session 就绪时调用一次 | `S/AwakeHostComposition.cs:171`（唯一调用点） |
| 5 | 框架**允许**从 `Stopped` 重新 `Start`（只拦 `Draining`/`Starting`/`Ready`/`RecoveryRequired`） | `F/…/RuntimeServiceClient.cs:468-486` `ScheduleStart` |
| 6 | AWAKE 侧两处「未就绪」关口目前只报错、不尝试恢复 | `S/AwakeProviderConfiguration.cs:386`、`:541` |

结论：**掉线是框架的 fail-closed 设计，不自恢复是 AWAKE 侧缺少重启触发**。第 5 条说明重启是框架支持的合法路径。

## 决策锁定

| # | 决策点 | 结论 |
|---|---|---|
| D-1 | 修在哪 | **AWAKE 侧**。008 自身不动框架的 `FailConnectionAsync`、健康载荷校验、协议常量，也不动 Runtime 包。（后续 009 批次单独修正了框架层的 health 载荷契约与探测语义，见 `PLAN-AWAKE-HEALTH-CONTRACT-20260911.md`；008 的重启自救作为兜底保留。） |
| D-2 | 何时重启 | 仅当 `Status.State == Stopped` 且当前战役 session 仍 `Ready` 时，由两处 MCM 关口触发 |
| D-3 | 节流 | 冷却窗 + 每局次数上限；每次尝试写一行 `runtime_service_relaunch`，不静默 |
| D-4 | 不越权 | `Ready`/`Starting`/`Draining`/`RecoveryRequired`/`Status==null` 一律不动；框架本身也拒绝 `RecoveryRequired` 重启 |
| D-5 | 触发方式 | 拉（用户下一次 AI 动作）而非推（后台轮询），避免在 tick 里做重活 |
| D-6 | 版本 | 另立 BuildId，不改 `SubModule.xml` / 程序集版本；`v0.2.0` 不变 |

## 契约（可观察结果）

`入口` Runtime 处于 `Stopped`，用户在 MCM 点「AI 自检 / 连接测试 / 拉取模型」
→ `调用` 关口发现 `Stopped` → `TryRelaunchStoppedRuntime`
→ `结算` 重启 Runtime（新进程 + 重新握手 + 重新应用 profile）
→ `可观察结果` `Awake.log` 出现 `runtime_service_relaunch reason=… attempt=…` 与随后的
`runtime_service_start state=Ready`；用户重试即可用，而不是整局「尚未就绪」。

### 验收标准

1. `Stopped` + 活战役 session 时，下一次 MCM AI 动作**恰好触发一次**重启尝试，并把结果写进日志。
2. 冷却窗内的重复动作**不重复触发**；每局尝试次数有上限（两者都用离线用例覆盖）。
3. `Ready`/`Starting`/`Draining`/`RecoveryRequired`/`Status==null` 一律**不触发**（离线用例覆盖）。
4. 离线回归全绿：新增 smoke 用例、主 smoke、`--persona-anchor`。
5. 不动框架 DLL / Runtime 包 / 存档格式 / 版本号 / MCM 设置项。

## 非目标

- 不追查 `response_health_payload_invalid` 的**触发源**（错帧假设未证实）；本批只保证掉线可恢复。
- 不改 fail-closed 语义 —— 一次协议异常仍然拆连接，只是不再整局判死。
- 不做后台自动保活轮询。
- 不修 `permission_gate ... storage.namespace.write decision=Denied`、不修启动器退出崩溃。

## 已知取舍

- 若异常**持续**复发（例如每 25 秒一次），本批表现为"反复重启"而非"永久死亡"；次数上限到顶后回到"尚未就绪"，
  日志里会留下明确计数，便于下一步定位。
- 重启会丢失 Runtime 进程内的内存态；账本落盘（`awake-runtime.provider-ledger.json`）不受影响，
  但**未验证**重启后账本是否被重新加载 —— 记作待验证项，不作结论。

## 待用户签收

- [ ] 用户签收本批次（审查状态机 round 1 → 修订 → round 2）。
