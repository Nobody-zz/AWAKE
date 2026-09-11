# AWAKE Health 契约与探测语义修复立项 — 2026-09-11

## 立项结论

- 目标：让 MCM 的「AI 自检」（health 探测）**不再必然失败**；并且**探测失败不再处死 Runtime 进程**。
- 触发：用户 2026-09-11 实机反馈「进去了说配置没准备好 / 填 KEY 填不进去」，并明确要求「也不应该把 runtime 关掉」。
- 风险等级：`standard`（跨 framework 源码 + Runtime 打包链、可逆、无新存档格式、不改公共 API 签名）。通道：审查状态机，`max_rounds=2`。

## 根因（离线复现，非推测）

### R-1 契约字面量不一致（确定性，每次必失败）

| # | 事实 | 证据 |
|---|---|---|
| 1 | 客户端要求 health 应答 payload **逐字**等于 `HealthAckPayload` | `framework/MarcusAwakeFramework/src/RuntimeServiceClient.cs:2060`、常量 `:115` |
| 2 | 服务端在 `HandleHealthAsync` 传入同一字面量 | `framework/MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs:1221` |
| 3 | 但 `BuildResponse` 先过 `CanonicalizePayload`（`SerializeEnvelope` + `TryParseEnvelope` 往返） | 同文件 `:2558`、`:3254` |
| 4 | `StrictJson.Write` 在渲染对象时按 `StringComparer.Ordinal` 对键排序 | `framework/MarcusAwakeTransport/src/StrictJson.cs:243` |
| 5 | 离线 harness 打印真实服务应答：`json={"ledger":"non_durable","state":"ready"}` | 本次会话实测（见下方红/绿） |

结论：payload 经传输层规范化后键序变为字母序，客户端逐字比较**必然不等** →
`throw ClientProtocolException("response_health_payload_invalid")`。这与实机三次
（`14:14:35Z` / `15:57:04Z` / `16:15:38Z`）全部报同一 `protocol_detail` 一致；
「偶发 IPC 打嗝 / 错帧」的解释不成立。

### R-2 探测失败被当成连接失效（fail-closed 用错场合）

- `CheckHealthAsync`（`RuntimeServiceClient.cs:222-254`）三个 catch（取消 / 超时 / 协议异常）**全部**调用
  `FailConnectionAsync`（`:1537`）：`state=Draining` → 关连接 → **杀 Runtime 进程** → `state=Stopped`。
- 一次只读探测失败 ≠ 连接失效 ≠ 进程该被处死。实机后果：点一次「AI 自检」就毁掉整局 AI 能力，
  且直到 008 之前没有任何重启路径。

## 红 / 绿证据（本机离线，2026-09-11）

契约测试读**出货客户端程序集**里的 `HealthAckPayload` 常量，与服务端真实应答逐字比较：

```
RED   (客户端常量用旧顺序)：
      FAIL P3B-02 valid_handshake_and_health health_payload_contract_mismatch
      client={"state":"ready","ledger":"non_durable"} service={"ledger":"non_durable","state":"ready"}
      PASS_COUNT=18 FAIL_COUNT=1
GREEN (客户端常量改为规范序)：
      PASS_COUNT=19 FAIL_COUNT=0
```

## 决策锁定

| # | 决策点 | 结论 |
|---|---|---|
| D-1 | 契约方向 | 以**传输层规范形**（StrictJson 键序）为准：`HealthAckPayload` 改为 `{"ledger":"non_durable","state":"ready"}`；服务端不改（它的输出本来就已是规范形） |
| D-2 | 探测语义 | health 失败（协议 / 超时 / 取消）**只返回失败结果**，不拆连接、不杀进程、不改 `state` |
| D-3 | fail-closed 边界 | **业务请求**路径的 fail-closed 语义不动（协议异常仍拆连接）；只调整 health 这唯一一处探测 |
| D-4 | 回归防护 | runtime harness 增加契约断言，直接反射读客户端常量，常量或服务端任一漂移都会红 |
| D-5 | 存活兜底 | 保留 008 的 AWAKE 侧重启自救：进程真的没了仍能拉起来 |
| D-6 | 版本 | `v0.2.0` 不变；BuildId 另立，不改 `SubModule.xml` 程序集版本 |

## 契约（可观察结果）

`入口` 用户在 MCM 点「AI 自检」
→ `调用` `CheckHealthAsync` → 服务端 `health_ack` payload 逐字等于客户端常量
→ `结算` 校验通过，`state` 保持 `Ready`
→ `可观察结果` MCM 显示「AI 自检成功」；`Awake.log` **不再**出现 `response_health_payload_invalid`；
即使探测真的失败，也只出现一次 `provider_operation_failed operation=runtime_health`，
Runtime 进程与 `state=Ready` 保持不变（下一次业务请求才走原有清理路径）。

### 验收标准

1. runtime harness 契约断言绿（改前必红，红/绿证据已留）。
2. 离线回归全绿：runtime harness 全套、AWAKE 主 smoke、`--persona-anchor`、`--redtest-r1-behavioral`。
3. 实机：点「AI 自检」成功；`Awake.log` 无 `response_health_payload_invalid`；`runtime_service_start` 后
   `state` 保持 `Ready`，不再出现 `runtime=Stopped` 自杀。
4. 不改存档格式、不改 MCM 设置项、不改 `SubModule.xml` 版本号。

## 非目标

- 不改业务请求的 fail-closed。
- 不追查 `CanonicalizePayload` 对其余报文类型的影响（本批只钉 health 应答方向的逐字契约）。
- 不修 `permission_gate ... storage.namespace.write decision=Denied`、不修启动器退出崩溃。

## 已知取舍

- health 失败后保留的连接若实际已半死，后续业务请求会再失败一次并走原有清理路径；这是有意的两层判断。
- 今后新增「逐字比较 payload」的契约时，必须同时加契约断言，否则会重复本次事故。

## 待用户签收

- [ ] 用户签收本批次（审查状态机 round 1 → 修订 → round 2）。
