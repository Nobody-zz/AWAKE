# Plan: Marcus-Awake P3D-A0 Provider IPC Contract and Core Event Foundation

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3D-A0-PROVIDER-IPC-CONTRACT-20260827`
- `revision`: `23`
- `plan_status`: `implementing_revision_23`
- `review_status`: `approved_independent_read_only_review`
- `user_signoff_required`: `true`
- `user_signoff`: satisfied by the explicit autonomous full-migration authorization recorded on `2026-08-26`
- `primary_executor`: controller
- `implementation_authorized`: `true` after independent read-only review 26 returned `VERDICT: APPROVED` for revision 23
- `minimum_evidence`: `P3D-A0-E1 + P3D-A0-E2`
- `created`: `2026-08-27`

## 1. Purpose and closed loop

先把 Provider 接入所需的跨进程契约锁定，并实现到现有 P3B 通道的 Transport/Core 与最小 Service 协议门控：

`versioned request → capability/shape validation → Core event/result contract → deterministic reject/accept evidence`

本批不访问 Provider HTTP，不执行 Provider 请求，不建立 profile registry，不写配置数据库，不实现 MCM，也不接 AWAKE 游戏 Caller。允许对 RuntimeServiceHost 做最小协议级修改，只用于注册 versioned message/capability、执行 capability/task-scope/schema gate、返回明确的 contract-only deferred 错误、保留请求 deadline 和输出脱敏错误；不得加入 Provider handler、HTTP、凭据读取、Storage 业务或持久化。这样可以让 capability gate、deadline 和安全错误输出具备真实 Service 进程证据，同时避免在 Runtime Service 中边写 HTTP 边发明 wire contract。

## 2. Confirmed facts

1. `MarcusAwakeTransport` v2 与 P3B 的 Named Pipe、bootstrap、握手、session fence、checksum、连接 epoch 和 direction sequence 已固定；P3B 真实子进程/私有管道回归 `19/19` 通过。
2. P3C 已在同一 IPC 上承载 Storage/RAG 和 durable receipt；本批不能改变 P3C durable semantics。
3. `MarcusAwakeProvider` 原型独立测试 `18/18` 通过，但 Provider message、stream wire 和 Core mapping 尚未进入 Runtime Service。
4. `RuntimeServiceClient.SupportsAiBusinessFrames=false`，`SubmitAsync/GetReceiptAsync` 当前拒绝 AI business；Core `AiTaskEvent` 当前没有 structured result 字段。
5. `PipeEnvelope` 顶层不新增 capability 字段；握手 capability 数组和 Service 的静态 message→capability 映射是唯一权限来源。A0 允许把该映射接入现有 Service 的预解析 gate，但不允许旁路授权。
6. 当前 `framework/MarcusAwakeTransport/tests/MarcusAwakeTransport.Tests.csproj` 已有 `tests/Program.cs`；当前源代码 focused build/run 已通过 `0 warnings / 0 errors` 与 `7/7`。该 deterministic primitive suite 不能替代 P3B 真实子进程 harness。
7. 当前 `framework/MarcusAwakeFramework/tests/TestDoubles/FrameworkCoreDoubles.cs` 已改为调用 `RequestContext.IsExpiredAt(DateTimeOffset.UtcNow)`；生产 API 未为测试兼容性改变。该 focused build/run 已通过 `0 warnings / 0 errors` 与 `20/20`。
8. 当前 `ProtocolConstants`、`ProtocolValidation` 和 `RuntimeServiceHost` 尚未包含 Provider message/capability 分支；A0 必须把计划中的静态映射真正接入 Service 预解析入口，不能只新增 fixture 或文档表格。
9. 当前 `SequenceWindow` 对 future out-of-order frame 会先污染已见记录；A0 必须修复为拒绝但不记录，使原 message/payload 的重试仍可接受。
10. 当前 Service response 会将业务 deadline 重置为 `UtcNow + 5s`，且异常路径会写出异常 message/stack trace；A0 必须改为逐字继承请求 deadline，并只输出稳定、脱敏的错误信息。
11. revision 10 的 stream 描述必须同时约束 route-change 的可见文本边界和切换前后 `provider_id` 的含义，否则 `route-change after text` 测试没有唯一预期结果；revision 11 将其锁定为 A0 可执行的本地状态机规则，候选路由归属仍留给 A2。

## 3. Locked contract

### 3.1 Versioned IDs and capability matrix

在现有 `ProtocolConstants` 中固定下列稳定字符串，不再使用未带版本的 `provider.complete` 等名称：

| 类型 | ID |
|---|---|
| capability | `provider.configure.v1` |
| capability | `provider.models.v1` |
| capability | `provider.complete.v1` |
| capability | `provider.stream.v1` |
| request | `provider.profile_upsert.v1` |
| request | `provider.profile_remove.v1` |
| request | `provider.models.v1` |
| request | `provider.complete.v1` |
| request | `provider.stream.v1` |
| response schema | `marcus-awake.provider.result.v1` |
| response schema | `marcus-awake.provider.profile_result.v1` |
| response schema | `marcus-awake.provider.models_result.v1` |
| stream schema | `marcus-awake.provider.stream_event.v1` |
| error schema | `marcus-awake.provider.error.v1` |

静态映射必须唯一且可测试：

| request message | required handshake capability | task scope |
|---|---|---|
| `provider.profile_upsert.v1` | `provider.configure.v1` | required |
| `provider.profile_remove.v1` | `provider.configure.v1` | required |
| `provider.models.v1` | `provider.models.v1` | required |
| `provider.complete.v1` | `provider.complete.v1` | required |
| `provider.stream.v1` | `provider.stream.v1` | required |

TaskScope 的输出 schema 也必须按 request message 固定，caller 不得为同一种 request 自行选择另一套输出契约：

| request message | `TaskScope.OutputSchemaId` | major | minor |
|---|---|---:|---:|
| `provider.profile_upsert.v1` | `marcus-awake.provider.profile_result.v1` | 1 | 0 |
| `provider.profile_remove.v1` | `marcus-awake.provider.profile_result.v1` | 1 | 0 |
| `provider.models.v1` | `marcus-awake.provider.models_result.v1` | 1 | 0 |
| `provider.complete.v1` | `marcus-awake.provider.result.v1` | 1 | 0 |
| `provider.stream.v1` | `marcus-awake.provider.stream_event.v1` | 1 | 0 |

Schema 分层固定如下，避免把两种不同含义的 `output_schema` 混在一起：

- `payload.schema` 是 request schema，必须等于 envelope 的 `PayloadSchema`。
- `TaskScope.OutputSchemaId/major/minor` 是跨进程 response wire schema，必须逐字等于上表固定值；它不是玩家或业务作者为模型指定的结构化输出格式。
- `provider.complete.v1` 和 `provider.stream.v1` payload 不再包含 `output_schema_id`、`output_schema_major`、`output_schema_minor`。需要模型返回结构化 JSON 时，只使用可选的 `response_schema_json`，它是受限的 JSON object 文本，属于模型输出约束，不参与 TaskScope schema 比较。
- `provider.profile_result.v1`、`provider.models_result.v1`、`provider.result.v1` 和 `provider.stream_event.v1` 是 A0 的 wire response schema；其内部字段由对应 A1/A2 handler 批次继续实现，但 ID 和版本在此固定。

没有 capability 字段的旁路授权；握手 capability 只表示当前 IPC 连接允许进入哪类 Service handler，不等于 Framework `PermissionCatalog/PermissionGate` 的用户权限，也不等于 `CloudExportClassification` 的外发许可。任何未来 Provider 调用仍必须先通过 Framework 权限门和云端外发门；A0 只验证 IPC admission。握手未申请或未获得对应 capability 时，Service 在业务 payload 解析前返回 `capability_not_granted`。未知 message、未知 capability、未知 request schema 或不匹配的 TaskScope output schema 均 fail closed。

映射、task classification、output schema 和预解析 gate 必须在同一个 Service 入口使用同一份静态表；不能只在 fixture 中复制一份映射。对已识别但尚未实现 Provider handler 的合法请求，Service 在完成协议、作用域、TaskScope output schema 和业务形状校验后返回 `provider_handler_deferred`，不得访问网络或凭据。

Provider 请求和 stream event 同时携带 envelope 的 `PayloadSchema` 与 payload 对象的 `schema`。两者必须逐字相等；缺失、未知或不相等一律在业务 dispatch 前返回固定的通用或 Provider contract error，不采用“外层优先”或“内层优先”的隐式覆盖规则。profile 的 `base_url`、`credential_reference` 和 `is_cloud` 仅在私有、认证的 Service IPC contract 中作为受限配置字段存在；A0 不读取 credential、不访问 endpoint、不作云端外发、不写入日志/evidence、不在错误中回显。错误响应本身只携带稳定错误码和脱敏安全消息，不回显原始 payload。

### 3.1.1 Schema layers and output contract

`TaskScope.OutputSchemaId` 只描述 Service 返回给 Framework 的 wire 事件/结果容器；`response_schema_json` 只描述模型生成内容的内部 JSON 形状。两者不得互相替代、不得用同一个字段名表达。A0 的 profile/models 请求没有 `response_schema_json`；complete/stream 请求可以省略或提供它。`CloudExportClassification` 不在 A0 Provider payload 中重新定义；A1 的 Framework caller 必须从 `RequestContext/AiTaskRequest` 取得并在权限/外发门通过后，按独立字段传播到 Service execution context，不能由 profile 的 `is_cloud` 推断用户许可。

### 3.1.2 Admission phases and header-only capability gate

Service admission 的概念层分为 H0、H1、S0、B0、B1、E0；其中 H1 再细分为身份、完整性和 deadline 子阶段。可执行的唯一调用顺序、状态转换、sequence 消费和 response policy 以 3.1.2.1 为准，不能由其他方法另行解释。

1. H0 transport/header decode：PipeFrameIO 解出 UTF-8 frame；HeaderOnlyParser 只定位外层字段与嵌套 payload token，并验证 frame/UTF-8 字节上限、token 边界、语法深度和重复键。H0 不计算 payload_length、payload_sha256、checksum，不验证 fence，不读取业务字段语义；raw frame 失败时不记录 sequence。
2. H1 header gate：依次执行 H1-I 身份、H1-C 完整性和 H1-D deadline 检查；H1 失败不能读取业务 payload，也不能记录 sequence。
3. S0 resource/sequence gate：ValidateFrameAdmission 先在 S0 内调用 `TryEnterFrame` 获取唯一 in-flight slot；slot 不可用时返回 `ipc_backpressure`、`consume_sequence=false`、`response_policy=retryable_generic_error`，不得进入 B0/B1，且不得存在入口外的 backpressure 分支。slot 获取成功后才执行 sequence window；future/gap/nonce/session/integrity 失败不记录，合法序号一旦接受即消费。
4. B0 header-only business admission：依次执行 known message、outer schema、capability、task scope/owner/session、output schema、causation，不读取业务字段。
5. B1 business parse：只有 B0 全部通过后，才把保留 token 交给业务 parser，执行内外 schema、字段、长度、secret-like 字段和 operation shape 校验。
6. E0 deferred/settlement：只有 B1 合法后，A0 contract-only 分支才按 SettlementRequirement 选择 provider_handler_deferred 或 settlement_unavailable。

TryParseEnvelope 的现有完整校验路径不得被误称为 header-only；A0 必须新增或明确实现单独的 header-only parser/validation entry，并由真实 Service 入口先调用。H0 只负责 frame/外层 token 边界，H1-C 才负责 payload canonical UTF-8 bytes 的 length/hash/checksum 与 fence integrity；`TryEnterFrame` 由 `ValidateFrameAdmission` 的 S0 resource gate 统一拥有并在最终 settlement 后释放。原始 token 不进入业务错误、日志或 evidence。拒绝 capability 的 fixture 必须使用一个外层 JSON 有效、但业务 payload 字段类型错误并包含 secret-like 字段的请求，观察结果仍必须是 capability_not_granted，以证明业务字段解析没有提前发生。若要提取 raw token，允许在 ProtocolCodec.cs/StrictJson.cs 增加只定位外层 payload token 的辅助代码，但不得改变现有 envelope wire 结构。

### 3.1.2.1 Single executable admission entry and precedence

A0 必须把 admission 收束为一个 Service-owned 决策入口：RuntimeServiceHost.ValidateFrameAdmission(...)。DispatchFrameAsync 只能调用该入口取得 AdmissionDecision，不得在入口外分别调用序列窗口、完整 envelope 校验或业务 payload parser。AdmissionDecision 至少包含 phase、error_code、consume_sequence、allow_business_parse 和 response_policy 五个结果字段；测试直接断言这些字段，避免文字上分阶段、代码上绕开阶段。

真实接收入口的唯一接线固定为：`HandleConnectionAsync → ReadFrameAsync → HeaderOnlyParser → ValidateFrameAdmission → B1 business parser → Dispatch/response`。`HandleConnectionAsync` 在 `ValidateFrameAdmission` 返回 `allow_business_parse=true` 之前，不得调用完整 `TryParseEnvelope`、业务 DTO 反序列化、payload 字段访问或任何会物化业务字符串的 parser；拒绝路径必须只保留 transport/header 结果。`DispatchFrameAsync` 不得成为第二个 admission 入口。

唯一顺序固定为：

1. H0：外层 JSON/UTF-8/frame bound/token 定位；只做 transport syntax、token boundary 和 header-only payload 定位，不做 payload length/hash/checksum 或 fence integrity；失败时不解析业务字段、不记录 sequence。无法构造可信 response envelope 时按既有 transport reject/close 行为处理。
2. H1-I：protocol、message identity、instance/connection epoch、direction nonce 和 session fence；失败时不记录 sequence。`frame_fence_proof` 不属于 H1-I。
3. H1-C：checksum algorithm、payload canonical bytes 的 length/hash/checksum；失败时不记录 sequence。
4. H1-D：deadline 形状与有效期；缺失、不可转换、<=0、不可表示或已过期都不记录 sequence。
5. S0：在 H0/H1 全部通过且 deadline 尚未过期后，先执行唯一的 `TryEnterFrame` resource gate，再执行序列决策；slot 不足返回 `ipc_backpressure` 且不消费 sequence，future/gap/nonce/session/integrity 失败不记录，合法序号一旦接受即消费，即使后续 B0/B1 业务拒绝。
6. B0：按 known message → outer schema → capability → task scope/owner/session → output schema → causation 顺序做 header-only admission，不读取业务字段。
7. B1：完成内外 schema、业务字段、secret-like 字段和 operation shape 校验；只检查 Framework 已生成的 `TaskScope.RequestPayloadHash` 是否为 64 位十六进制字符串并原样传递，只有这里允许业务 parser。
8. E0：合法业务请求进入 A0 contract-only 分支，根据 settlement requirement 生成 deferred 或 settlement typed error；不得访问 HTTP、凭据或 Storage。

组合错误的唯一优先级就是上述阶段顺序；同一阶段内按列表从左到右取第一个错误。provider_causation_missing 不得被 deferred/settlement 错误覆盖；业务拒绝不回滚已消费的 sequence；duplicate 只允许返回已保存的 terminal template，不得重新解析 payload。

response_policy 固定为：H1-D 的 `deadline_missing`、`deadline_invalid`、`deadline_expired` 永远是 close/no-response，不能生成 generic error；合法 future deadline 才允许写入 response 且 response envelope 逐字继承请求 deadline；response 首字节写入前过期则 suppress/close/no-response，禁止半帧。`ipc_backpressure` 只在已通过 H1-D 的 S0 resource gate 产生，使用已验证 deadline 的 retryable generic response。所有结果必须有逐案证据。
### 3.1.3 B0 pre-business-payload generic error response

B0 阶段尚未解析业务 payload，因此只能使用 `PayloadSchema=marcus-awake.error.v1`；该 generic response 仅适用于已通过 H1-D 且允许写 response 的 header-only admission rejection。

```json
{
  "schema":"marcus-awake.error.v1",
  "error_code":"capability_not_granted",
  "category":"denied",
  "retryable":false,
  "safe_message":"The requested runtime capability was not granted."
}
```

B0 的固定错误集合和优先级如下；H1-D 的 deadline 错误不在本表内，而是按 close/no-response 规则结算：

| 检查位置 | `error_code` | category | retryable | fallback_allowed | 适用条件 |
|---|---|---|---:|---:|---|
| message header | `message_type_missing` | `invalid_request` | false | false | message type 缺失或不是已登记 request/探针 |
| outer schema header | `payload_schema_missing` | `invalid_request` | false | false | `PayloadSchema` 缺失 |
| outer schema header | `schema_unsupported` | `unsupported` | false | false | message 已知但外层 schema 不是该 message 的固定 request schema |
| capability | `capability_not_granted` | `denied` | false | false | 握手 capability 交集不含静态映射要求的 capability |
| task header | `task_scope_missing` | `invalid_request` | false | false | request 缺少 TaskScope |
| task header | `task_scope_invalid` | `invalid_request` | false | false | owner/session/task scope 不完整或跨界 |
| output header | `output_schema_mismatch` | `invalid_request` | false | false | TaskScope output schema 不等于固定矩阵 |
| causation header | `provider_causation_missing` | `invalid_request` | false | false | Provider task/profile/cancel 缺少 CausationId |

H0/H1-I/H1-C/H1-D 错误均在 sequence 之前处理：其中 H1-D 的 deadline_* 固定为关闭连接且不发送 response envelope，因此不消费 sequence；只有通过 H1-D 并进入 S0、已经接受 sequence 的 B0/B1 业务拒绝才会消费当前序列。S0 的 `TryEnterFrame` 和 sequence decision 都只能由 `ValidateFrameAdmission` 执行；slot 不足的 `ipc_backpressure` 使用已验证 deadline 的 generic retryable response，并在 admission 返回前释放未占用 slot。future out-of-order、sequence gap、nonce/session 不匹配和 integrity 失败不记录 sequence。H1-D 连接关闭后的同一序列重试必须重新握手并获得新 connection epoch/direction nonce；首字节前过期发生在 S0 之后，原序列已消费，不能在同一连接重放相同 message/sequence。

### 3.1.4 Contract-only error response

在 Provider 业务 payload 已完成 envelope、scope 和形状校验后，A0 的 deferred/settlement 错误必须使用 `PayloadSchema=marcus-awake.provider.error.v1`，payload 只允许且必须包含以下字段：

```json
{
  "schema":"marcus-awake.provider.error.v1",
  "error_code":"provider_handler_deferred",
  "category":"unavailable",
  "retryable":false,
  "fallback_allowed":false,
  "safe_message":"Provider handler is not enabled in this runtime build.",
  "provider_id":"provider.example",
  "profile_id":"profile.example",
  "route_id":"route.example"
}
```

`error_code` 在 B1 Provider contract-only 阶段只允许 `provider_handler_deferred`、`settlement_unavailable` 和 `provider_schema_mismatch`；`schema_unsupported` 只属于 H1 generic error。`category` 只允许对应的 `invalid_request` 或 `unavailable` 值。`retryable` 与 `fallback_allowed` 必须由 Service 的静态错误表生成，不接受 caller 传入。`safe_message` 为固定模板，UTF-8 ≤512 bytes，不得包含异常原文、payload、凭据、authorization、API key 或完整 endpoint query。`provider_id/profile_id/route_id` 必须分别等于原始 `TaskScope`；缺少 task scope、capability 未授予、缺少 CausationId 或 deadline 的预解析错误继续使用通用 `marcus-awake.error.v1`，且不得解析或回显 Provider payload。

| `error_code` | `category` | `retryable` | `fallback_allowed` | 固定 `safe_message` |
|---|---|---:|---:|---|
| `provider_handler_deferred` | `unavailable` | false | false | `Provider handler is not enabled in this runtime build.` |
| `settlement_unavailable` | `unavailable` | false | false | `Provider result cannot satisfy the requested settlement.` |
| `provider_schema_mismatch` | `invalid_request` | false | false | `Provider request schema does not match the negotiated contract.` |

### 3.2 Scope and profile-control payloads

P3D-A0 只定义和验证形状，不保存或执行 profile：

`provider.profile_upsert.v1` 允许且只允许以下 JSON 属性：

```json
{
  "schema":"marcus-awake.provider.profile_upsert.v1",
  "profile_id":"profile.example",
  "provider_id":"provider.example",
  "route_id":"route.example",
  "provider_kind":"openai_compatible|anthropic|ollama",
  "base_url":"https://example.invalid/v1/",
  "default_model":"model-id",
  "credential_reference":"credential.example",
  "is_cloud":true
}
```

字段约束：`schema/profile_id/provider_id/route_id/provider_kind/base_url/default_model/is_cloud` 必填；`credential_reference` 对 `is_cloud=true` 的 profile 必填，对 `is_cloud=false` 的 profile 可省略。A0 只把 `is_cloud` 当作声明性布尔值做形状校验，不在本批判断它是否与 provider kind 或 endpoint policy 一致；逐 Provider endpoint path、HTTPS/loopback 策略以及声明与实际端点不一致时的 `profile_policy_mismatch` 由 P3D-A1 Service adapter 负责。字符串限制：profile/provider/route/credential ID ≤160 UTF-8 bytes，model ≤256 UTF-8 bytes，base URL ≤2048 UTF-8 bytes。URL 必须是绝对 HTTP(S)，无 userinfo/query/fragment。

`provider.profile_remove.v1` 允许且只允许：`schema`、`profile_id`、`provider_id`、`route_id`；profile 控制帧必须使用 envelope 的 `owner_id + campaign_guid + timeline_id + session_id + session_generation` 作为作用域，并要求 payload 的 `profile_id/provider_id/route_id` 分别等于 `TaskScope.ProfileId/ProviderId/RouteId`。P3D-A0 不允许通过 profile_id 跨 owner/session 读取或删除。

### 3.3 Operation request payloads

`provider.models.v1` 只允许：`schema`、`profile_id`、`provider_id`、`route_id`；`profile_id/provider_id/route_id` 必须分别与 envelope task scope 的 `ProfileId/ProviderId/RouteId` 相同。

`provider.complete.v1` 和 `provider.stream.v1` 只允许：

```json
{
  "schema":"marcus-awake.provider.complete.v1",
  "profile_id":"profile.example",
  "provider_id":"provider.example",
  "route_id":"route.example",
  "model":"model-id",
  "messages":[{"role":"system|user|assistant","content":"..."}],
  "max_output_tokens":2048,
  "temperature":0.7,
  "response_schema_json":"{...}"
}
```

`profile_id/provider_id/route_id` 必填，并分别等于 `TaskScope.ProfileId/ProviderId/RouteId`；TaskScope 的固定 output schema 不在 payload 中重复。`messages` 至少一项、最多 64 项；每个 role 只能是 `system/user/assistant`，content ≤64 KiB；model 可省略但若存在 ≤256 UTF-8 bytes；`max_output_tokens` 为 1..65536；temperature 为有限数值 0..2；`response_schema_json` 可省略，存在时必须是 ≤64 KiB 的 JSON object 文本，它不是 wire output schema ID。整个 operation payload ≤96 KiB，未知字段、嵌套过深、重复键、非对象 schema 和 secret-like 字段（`api_key`、`authorization`、`password`、`secret` 等）拒绝。stream 的 schema 值改为 `marcus-awake.provider.stream.v1`，其余字段相同。

### 3.4 Stream event wire and FSM

Provider stream 不新增 envelope 顶层序列字段；每个 response envelope 的 `Sequence` 仍是 Service→client 的 direction sequence，payload 内另有 `stream_sequence`。

每个 `marcus-awake.provider.stream_event.v1` payload 只允许以下属性：

```json
{
  "schema":"marcus-awake.provider.stream_event.v1",
  "stream_id":"task-id",
  "stream_sequence":1,
  "event_kind":"started|text_delta|usage_update|route_changed|completed|cancelled|failed",
  "terminal":false,
  "provider_id":"provider.example",
  "profile_id":"profile.example",
  "model_id":"model-id",
  "text":"...",
  "structured_json":"{...}",
  "usage":{"input_tokens":12,"output_tokens":3},
  "from_provider_id":"",
  "to_provider_id":"",
  "error":{"code":"...","category":"...","retryable":false,"safe_message":"..."}
}
```

字段约束：`stream_id/stream_sequence/event_kind/terminal/provider_id/profile_id/model_id` 必填；`stream_id` 必须等于 `TaskScope.TaskId`，`profile_id` 必须等于原始请求的 `TaskScope.ProfileId`，`model_id` 必须等于已解析模型。`provider_id` 表示当前活动 Provider：`started` 必须等于原始 `TaskScope.ProviderId`，普通事件必须等于当前活动 Provider，`route_changed` 必须满足 `provider_id=from_provider_id=当前活动 Provider`，并在接受后把当前活动 Provider 更新为 `to_provider_id`。`stream_sequence` 必须从 `1` 开始并严格每次递增 `1`，不得跳号、重复或回退；单个 event payload ≤96 KiB，text ≤64 KiB，safe error message ≤512 bytes，`usage.input_tokens/output_tokens` 必须为整数 `0..16777216` 且两者之和 ≤`33554432`。

`usage` 的 wire 结构是闭集：只允许 `input_tokens` 和 `output_tokens` 两个整数属性，两个属性都必须存在，不接受 null、浮点、负数、未知属性或只给一侧；每个值范围为 `0..16777216`，总和不得超过 `33554432`。`error.category` 的 wire 闭集为 `invalid_request`、`incompatible`、`unsupported`、`unavailable`、`denied`、`not_found`、`conflict`、`expired`、`rate_limited`、`provider_failure`、`timeout`、`cancelled`、`resource_exhausted`、`recovery_required`、`internal_failure`；未知 category、category 与静态错误映射不一致或 retryable 与静态表不一致均拒绝。`fallback_allowed` 是 Service 内部路由结果和 evidence 字段，永远不出现在 stream wire `error` 对象；`error` 只允许 `code/category/retryable/safe_message` 四个字段。`error.code` 必须是 ASCII 标识符，长度 1..128 bytes，首字符为小写字母，其余只能是小写字母、数字、`.`、`_`、`-`，并且由 Service 固定错误表或 Provider 的安全错误码投影生成；原始异常文本不是 code。

事件字段矩阵固定为：`started` 只允许基础字段且 `terminal=false`、`stream_sequence=1`；`text_delta` 只允许非空 `text` 且 `terminal=false`；`usage_update` 只允许 `usage` 且 `terminal=false`；`route_changed` 只允许非空且不同的 `from_provider_id/to_provider_id`、`provider_id=from_provider_id` 且 `terminal=false`；`completed` 可带 `text/structured_json/usage` 但不得带 route/error 且 `terminal=true`；`failed` 和 `cancelled` 必须带 `error`、不得带 `text/structured_json/usage/route` 且 `terminal=true`。`error` 对象只允许且必须包含 `code/category/retryable/safe_message`，其中 `safe_message` ≤512 bytes 且不得包含原始异常或敏感信息；`structured_json` 若存在必须是受 Core 同等限制的 JSON object。每条事件的未知字段、条件不允许字段、重复键和 schema 不匹配均拒绝。FSM 为 `NotStarted → Started → (UsageUpdate|RouteChanged)* → (TextDelta|UsageUpdate)* → exactly one terminal`；`route_changed` 在首个可见 `text_delta` 后一律拒绝并返回 `route_change_after_visible_text`，terminal 后任何 event 都拒绝/丢弃并记录 `stream_terminal_already_emitted`，EOF 无 terminal 是 `stream_incomplete`。A0 只验证上述本地活动 Provider 状态、route 字段非空和 FSM 位置；`to_provider_id` 是否属于 Service 的实际候选路由由 P3D-A2 的 Service-owned route attempt registry 验证，不在 A0 虚构“下一候选”数据源。取消优先级以单一 handle 状态机的线性化点定义：取消请求先于 terminal commit 时产生唯一 `cancelled` terminal，否则保留已提交 terminal；不以墙上时钟推断“同时”。

### 3.5 Core event contract

Core `AiTaskEvent` 增加可选、bounded 的 `StructuredJson`、`ActiveProviderId`、`FromProviderId`、`ToProviderId`，保留现有构造函数并以空字符串表示未提供；Provider DTO 不进入 Core 公共 API。RouteChanged 映射为通用字符串元数据，不把 Provider 专用 DTO 带入 Framework。

P3D-A0 新增一个不连接 IPC 的 Framework-owned `AiTaskHandle` 状态机，供后续 RuntimeServiceClient/A1 复用；A0 不修改 `RuntimeServiceClient` 的 Provider business path，`SupportsAiBusinessFrames=false` 仍是明确的 deferred boundary。

`AiTaskHandle` 的可观察规则：

- 接受顺序：`Accepted`、`Started`，随后按 `stream_sequence` 映射 `TextDelta/UsageUpdate/RouteChanged`，最后只有一个 `Completed/Cancelled/Failed`。
- `StructuredJson` 只在完成事件或明确 structured result 中出现；超过 Core 限制或无法解析为 JSON object 时 typed fail。
- 每个 handle 至少保留 bounded event snapshot；订阅回调在后台读取任务上下文中串行触发，不在 Bannerlord tick 中执行网络/IPC。
- `CancelAsync` 的线性化点在 handle lock 内：状态为 Open 时设置 cancel-requested、追加唯一 `Cancelled` terminal 并返回成功 `true`；状态已为 Terminal 或 Disposed 时返回成功 `false`，不新增事件；调用 token 已取消时返回 typed `Cancelled` 且状态不变。
- `Dispose` 标记 Disposed、取消读取并丢弃迟到事件，不新增 terminal；Dispose 后 Subscribe 返回 no-op disposable，Snapshot 保留已发生事件；断线由 A1 负责映射为一次 typed unavailable/connection-lost terminal。
- 终端事件提交与取消请求共用同一 lock；谁先完成线性化谁生效，取消只在 terminal commit 之前优先。单个 handle 的 snapshot 最多保留 128 个事件，超过上限不静默丢弃，而是拒绝新非终端事件并返回 `resource_exhausted` typed error。
- P3D-A0 不实现 RuntimeServiceClient 的 single-flight IPC reader 或多 task demultiplex；这些属于 A1。后续扩展不得改变 wire schema。`StructuredJson` 必须是 JSON object，UTF-8 ≤64 KiB，最大深度 16、属性数 ≤256。A0 的 Core 代码只承载可复用的 `StructuredJson`、通用路由元数据、handle 终态规则和 typed error mapping；真实 Service 调用路径只验证 contract-only deferred 结果，不宣称 Provider 任务已经执行或产生 AI 成功结果。

### 3.6 Sequence, deadline and failure semantics

The exact public Core mapping contract is now closed for revision 22. `ProviderErrorMapping` must be a `public sealed` immutable result type with an `internal ProviderErrorMapping(string providerCategory, string providerId, string correlationId, FrameworkErrorCategory coreCategory, bool retryable, bool fallbackAllowed)` constructor and exactly these get-only properties: `ProviderCategory` (`public string`), `ProviderId` (`public string`), `CorrelationId` (`public string`), `CoreCategory` (`public FrameworkErrorCategory`), `Retryable` (`public bool`), and `FallbackAllowed` (`public bool`). Every property is public and get-only; no setter or mutable collection is exposed. `MapProviderError` is the only construction authority. A known provider category is matched by ordinal name against the 19-value Provider enum and is preserved; null, blank, unknown, or case-mismatched input becomes `ProviderCategory=Unknown`, `CoreCategory=InternalFailure`, `Retryable=false`, and `FallbackAllowed=false`. `providerId` and `correlationId` must pass the existing `ContractGuard.Id` check; only surrounding whitespace may be trimmed, and blank values throw `ArgumentException` rather than being silently replaced. Callers cannot provide or override the three mapped result fields.

- 修正 `SequenceWindow`：未来 out-of-order frame 返回 `sequence_out_of_order` 但不得写入 accepted/duplicate records；随后补齐缺失序列后，原 future frame 只有在 caller 以该下一序列重新发送时才可接受。已通过 H0 的业务拒绝仍消耗当前序列，不能依靠重发同一序列绕过 capability/schema/deadline gate。现有 P3B error code 和 protocol major 不变。
- Service response 的 `DeadlineUnixMilliseconds` 必须逐字沿用请求的业务 deadline；请求 deadline 缺失、非法、不可表示或已过期在 H1-D 阶段关闭连接且不发送 response，不能使用伪造新 deadline。IPC write/drain timeout 是独立控制；response 首字节前的 deadline recheck 由统一写入原语完成。
- Provider task、profile control、cancel 和对应 response envelope 必须携带非空 `CausationId`；`health`、`diagnostic` 等非任务探针可以省略。唯一检查顺序固定为 `H0 → H1-I → H1-C → H1-D → S0(resource gate → sequence) → B0 → B1 → E0`，对应真实接线为 `ReadFrameAsync → HeaderOnlyParser → ValidateFrameAdmission → B1 business parser → Dispatch/response`。`ipc_backpressure` 不得作为独立入口或绕过 admission coordinator；`provider_causation_missing` 只在 B0 到达 causation 检查点后产生，不得被 deferred/settlement 错误覆盖；response 的 `CausationId` 固定为触发该 response 的 request `MessageId`。
- P3D-A0 不宣称 Provider durable receipt；业务 payload 完整合法且 handler 未实现时，`SettlementRequirement.Required` 优先返回 typed `settlement_unavailable`，只有 `not_applicable` 才返回 `provider_handler_deferred`，两者均不访问网络、凭据或 Storage。
- 明确哈希分层和唯一权威：`PipeEnvelope.PayloadSha256`/`Checksum` 是 canonical UTF-8 wire payload bytes 的 transport integrity hash；`TaskScope.RequestPayloadHash` 只能由 Framework `TaskRequestCanonicalizer` 生成并验证，是语义任务输入的 idempotency hash。Framework 是 semantic hash 的唯一生成/验证 authority；Runtime Service 只检查该字段符合 `^[A-Fa-f0-9]{64}$`，随后逐字原样传递，不重算、不和 wire hash 比较、不用 payload 文本推导，也不把 wire hash 写入 durable task identity。`frame_fence_proof` 只在 H1-C 验证并保护已传入的 semantic hash。P3C durable receipt 继续使用 TaskScope/idempotency scope，不改变其唯一键或恢复语义。
- Provider 原型错误映射矩阵在 P3D-A0 固定，A0 只验证映射表，不执行 HTTP。稳定的 wire vocabulary 唯一规范来源是 `framework/MarcusAwakeTransport/src/ProviderProtocolContract.cs` 中冻结的 19 个字符串；`framework/MarcusAwakeProvider/src/ProviderContracts.cs` 的 `ProviderErrorCategory` 是 Provider 内部枚举投影，必须由 Provider focused test 逐字比对，新增、删除或改名都使 A0 失败并要求新的契约 revision。Core 的 category/retry/fallback 映射唯一运行时来源是 `MarcusAwakeFramework.Api.FrameworkErrors.MapProviderError`；Transport 不复制 Core mapping。该方法不是当前基线 API，而是本批必须新增并由 Provider focused runner 实际调用的公共契约：逻辑 authority ID 为 `marcus-awake.framework-errors.map-provider-error.v1`，真实符号为 `MarcusAwakeFramework.Api.FrameworkErrors.MapProviderError`，签名为 `public static ProviderErrorMapping MapProviderError(string providerCategory, string providerId, string correlationId)`，返回类型为 `MarcusAwakeFramework.Api.ProviderErrorMapping`，其中 `CoreCategory`、`Retryable`、`FallbackAllowed` 是唯一可观测结果；caller 不得传入或覆盖 retry/fallback。未知、缺失或不受支持的 provider category 必须返回 `InternalFailure,false,false`。这样 Provider（net8.0）、Framework（net472）和 Transport（netstandard2.0）不形成错误的编译期依赖，但仍有可执行 drift gate：

| Provider category | Core category | Core retryable | A2 fallback allowed |
|---|---|---:|---:|
| `InvalidRequest` | `InvalidRequest` | false | false |
| `Authentication` / `Forbidden` | `Denied` | false | false |
| `NotFound` | `NotFound` | false | false |
| `Conflict` | `Conflict` | false | false |
| `RateLimited` | `RateLimited` | true | true |
| `Timeout` | `Timeout` | false | false |
| `Unavailable` | `Unavailable` | true | false |
| `ServerUnavailable` / `TransportUnavailable` | `Unavailable` | true | true |
| `RedirectRejected` / `PolicyDenied` | `Denied` | false | false |
| `MalformedResponse` / `IncompleteStream` | `ProviderFailure` | false | false |
| `Cancelled` | `Cancelled` | false | false |
| `Unsupported` | `Unsupported` | false | false |
| `ResourceExhausted` | `ResourceExhausted` | false | false |
| `CorruptCredential` | `RecoveryRequired` | false | false |
| `InternalFailure` | `InternalFailure` | false | false |

`fallback_allowed` 是独立于 `Core retryable` 的路由门；任何未列举 category、缺失 provider error 或不安全原文均映射为 `InternalFailure` 且不 fallback。

### 3.6.1 Deadline boundary matrix

固定注入时钟为 `clock_id=fixture-unix-ms-v1`、`deadline_now_unix_ms=4102444799000`。比较规则是 `now < deadline` 才合法，`now >= deadline` 一律过期；因此 `now == deadline` 与 `now > deadline` 具有同一结算。

| 输入/时机 | 固定结果 | sequence | response 规则 |
|---|---|---|---|
| 字段缺失 | `deadline_missing` | 不消费 | 关闭连接且不发送 response envelope；证据中的 request/response deadline 为 `null` |
| 非整数、0 或负数 | `deadline_invalid` | 不消费 | 关闭连接且不发送 response envelope；证据中的 request/response deadline 为 `null` |
| `long.MaxValue` 或不能安全转换为 `DateTimeOffset` | `deadline_invalid` | 不消费 | 关闭连接且不发送 response envelope；证据中的 request/response deadline 为 `null` |
| 请求 deadline=`4102444799000` 且 now=`4102444799000` | `deadline_expired`（`now_at_or_after_deadline`） | 不消费 | 关闭连接且不发送 response envelope |
| 请求 deadline=`4102444800000` 且 now=`4102444799000` | `future_valid` | 消费 | response 的 DeadlineUnixMilliseconds 必须逐字等于 `4102444800000` |
| 合法请求在 response 首字节写入前过期 | `deadline_expired` | 已消费 | 取消写入、关闭连接、不发送 response envelope、不得产生半帧 |

Transport deadline 只负责可表示性和 Service admission；Framework 仍负责 request deadline 必须有限且不晚于 RequestContext。response 首字节写入是唯一的写入线性化点：首字节提交后不得再把结果改判为 deadline failure；harness 必须同时覆盖固定未来值、`now == deadline` 和 `now > deadline` 三个边界。H1-D close/no-response 不消费 sequence；首字节前过期发生在 S0 后，已消费 sequence，原连接关闭后不得重放同一 message/sequence。
### 3.6.2 Frame write commit contract

`WriteResponseAsync` 必须通过 `PipeFrameIO.WriteFrameAsync` 的受控写入重载完成 response：先在内存中构造完整 bounded frame，再调用一次 `BeforeFirstByteWrite` gate；gate 返回 false 时不调用 sink，记录 `write_started=false`、`first_byte_committed=false`、`bytes_written=0`、`frame_complete=false`、`response_suppressed=true`、`failure_kind=gate_rejected`，关闭连接且不发送 response。gate 返回 true 后才允许第一次 sink 写入；第一次成功写入返回的实际字节数大于 0 才是唯一 `first_byte_commit` 线性化点，之后 deadline 不得重新分类。受控 sink 的唯一契约是 `WriteAsync(buffer, offset, count, cancellationToken) -> actual_bytes_written`：返回 0、返回小于请求数、抛出异常和 flush 抛出异常都必须分别映射为 `zero_write`、`partial_write`、`write_exception`、`flush_failure`，立即关闭连接，`frame_complete=false`，不得写入 response/template ledger；`bytes_written` 必须累计 sink 实际返回值，不能用请求长度代替。只有累计实际字节数等于完整 frame 长度且 flush 成功，才记录 `frame_complete=true`、`response_sent=true` 并允许写入 terminal template。

本批允许修改 `framework/MarcusAwakeTransport/src/PipeFrameIO.cs`，新增最小 `IFrameWriteSink`、`FrameWriteResult` 和 `BeforeFirstByteWrite`/commit observation seam；生产 Stream adapter 是唯一正式 sink，test-only `FirstByteGateStream`/controlled sink 固定 zero、partial、exception、flush-failure 四种结果。`P3DA0Harness` 必须观察 `write_started`、`first_byte_committed`、`bytes_written`、`frame_bytes_expected`、`flush_completed`、`frame_complete`、`response_suppressed`、`connection_closed` 和 `failure_kind`。生产 Service 只通过 `WriteResponseAsync` 调用该 seam，不得另建第二套写入路径。
### 3.6.2.1 FrameSlotLease and backpressure

`TryEnterFrame` 由 `ValidateFrameAdmission` 唯一调用并返回受控 `FrameSlotLease`。slot 成功时 `slot_acquired=true`，lease 进入 `AdmissionDecision`；`ValidateFrameAdmission` 不释放成功 lease，`DispatchFrameAsync`/`HandleConnectionAsync` 在所有 B0/B1/E0、response、取消、异常和连接关闭路径的同一个 `finally` 中调用一次 `Release()`。lease 必须记录 `release_count`，成功获取时最终严格为 `1`，未获取时严格为 `0`；重复释放属于 harness 失败而不是静默幂等成功。slot 不可用时不创建 lease，唯一结果是 `ipc_backpressure`、`consume_sequence=false`、`response_policy=retryable_generic_error`，使用已通过 H1-D 的 deadline；不得进入 B0/B1，也不得由 coordinator 之外的入口再次尝试。Service evidence 必须同时记录 `slot_acquired`、`lease_release_count` 和 `backpressure`，verifier 要求三者与 admission 结果一致。

### 3.6.3 Cross-project dependency and authority graph

| 项目 | 生产引用方向 | 本批唯一职责 |
|---|---|---|
| MarcusAwakeTransport (netstandard2.0) | 无 Framework/Provider 引用 | wire constants/models、header-only parser、transport integrity、ProviderProtocolContract 的稳定字符串 authority |
| MarcusAwakeFramework (net472) | 只引用 Transport | AiTaskEvent、semantic request canonicalization、Core error/category mapping、handle contract |
| MarcusAwakeProvider (net8.0) | A0 不引用 Framework/RuntimeService；生产项目不新增 Transport 依赖 | Provider HTTP 与内部 ProviderErrorCategory；A0 只做 enum drift 证明 |
| MarcusAwakeProvider.Tests | Provider + 测试专用 Transport 与 Framework 引用 | 逐字比较 Provider enum 与 Transport wire vocabulary，并调用 Framework 的真实 `MapProviderError.v1` 结果；不得复制另一份映射表作为 authority |
| MarcusAwakeRuntimeService (net8.0-windows) | Storage + Framework + Transport | Service admission coordinator、versioned handler registry、contract-only Provider gate；A0 不引用 Provider HTTP 实现 |
| MarcusAwakeRuntimeService.Tests | Transport 与真实 Service executable | 子进程/Named Pipe 观察；不自行实现 Provider/Core authority |
| verify_marcus_awake_p3d_a0.ps1 | 调度上述构建和 runner | 重新计算证据聚合、哈希、case 集合和脱敏结果；不是业务实现 |

Authority 固定为：Transport 的 ProviderProtocolContract 负责 wire category 字符串；Provider 的 ProviderContracts.ProviderErrorCategory 负责内部 enum；Framework 的 `MarcusAwakeFramework.Api.FrameworkErrors.MapProviderError` 负责 Core mapping；RuntimeService 只消费 Transport contract，不复制 Provider/Core mapping。Provider 测试必须对 Transport 与 Framework 建立 ProjectReference，并由 verifier 检查这两条引用存在且没有被替换为自洽的手工映射；生产 Provider 不变，Provider/RuntimeService 生产代码不互相引用。

### 3.6.4 Redaction fixture provenance

Revision 22 capture provenance is explicit: `capture_binding.process_role` is `service_child`; `producer` identifies the parent runner `MarcusAwakeRuntimeService.Tests.exe --p3d-a0`; `capture_binding.executable` and `capture_binding.arguments` must equal the top-level `service_executable` and `service_arguments` byte-for-byte. The verifier must also require equal `run_id`, Service PID, and connection epoch, so the process that captured an artifact cannot be confused with the process that produced it.

Revision 23 additionally closes the artifact path binding: for each fixed `artifact_key`, `capture_binding.artifact_relative_path` must equal the enclosing scan artifact `relative_path` and the fixed path table (`service_stderr`, `response_envelope`, `deferred_payload`, `evidence_json`). The JSON Schema must enforce the four key/path pairs, and the verifier must repeat the equality check before reading or hashing any artifact.

四类脱敏扫描的输入来源固定为 `framework/MarcusAwakeRuntimeService/tests/fixtures/p3d-a0-redaction-inputs.v1.json`，文件必须通过独立 schema `marcus-awake/p3d-a0-redaction-inputs.v1`，且只能包含按 `artifact_key` ordinal 固定顺序排列的四个 fixture：`service_stderr`、`response_envelope`、`deferred_payload`、`evidence_json`。每项的 `artifact_key`、`case_id`、`subcase_id`、`relative_path`、`source_id`、`input_fields`、`input_fields_sha256` 和 `sensitive_input_digests` 都由 schema 固定；`input_fields` 采用按 artifact 分支的白名单，禁止任意键、原始 payload、异常原文、credential、authorization、API key、完整 endpoint query 和 stack trace。

canonicalizer ID 固定为 `json-ordinal-key-sort-array-order-preserving-number-invariant-unicode-nfc-lf-utf8-nobom-v2`：对象键按 ordinal 排序、数组保持顺序、字符串先做 Unicode NFC 与 CRLF/CR→LF、数字使用 invariant 表示、输出无空白并以 UTF-8 无 BOM 编码。禁止匹配规则固定为 `ascii-casefold-boundary-token-set-v1`，边界字符集合为 ASCII 字母、数字和下划线；匹配 token 为 `api_key`、`authorization`、`password`、`secret`、`stack_trace`、`inner_exception`、`endpoint_query`、`credential`、`payloadjson`。敏感输入 digest 按 `literal-redaction-sentinel-colon-id-utf8-sha256-uppercase-hex-v1` 派生，即对字符串 `redaction-sentinel:` 加敏感输入 ID 做 UTF-8 SHA-256 并输出大写十六进制；fixture 只保存 digest，不保存敏感原文。current revision 21 schema 必须把四个 artifact 的 `input_fields`、`input_fields_sha256`、敏感 digest ID/值集合全部固定为 const/位置闭集；verifier 另持不可变期望常量并从 manifest 重算 source/input hash、扫描规则和实际文件哈希，必须用变异 fixture 测试证明“同步修改输入与自报 hash”仍被拒绝，不能接受 manifest 自洽即可。

固定映射为：`service_stderr → P3D-A0-S07-redaction_no_payload_echo/stderr_artifact_bound → artifacts/redaction/service-stderr.txt → redaction.service_stderr.fixture.v1`、`response_envelope → P3D-A0-S07-redaction_no_payload_echo/response_envelope_artifact_bound → artifacts/redaction/response-envelope.json → redaction.response_envelope.fixture.v1`、`deferred_payload → P3D-A0-S07-redaction_no_payload_echo/deferred_payload_artifact_bound → artifacts/redaction/deferred-payload.json → redaction.deferred_payload.fixture.v1`、`evidence_json → P3D-A0-S07-redaction_no_payload_echo/evidence_json_artifact_bound → artifacts/redaction/evidence-canonical.json → redaction.evidence_json.fixture.v1`。

`source_sha256` 不是 fixture 自报字段，而是 verifier 对完整 fixture artifact（不含 verifier 生成的 evidence 自引用字段）按上述 canonicalizer 得到的 SHA-256；`evidence_json` 的 `input_fields` 只由四个扫描 subcase 的固定 ID、runner/case/subcase ID 集合和重算后的聚合布尔值组成，不包含 evidence 文件自身，避免自引用。
### 3.6.5 Post-S0 deadline suppression, replay and ledger

H1-D 过期在 S0 之前：不消费 sequence、不写任何 message/task/suppression ledger，直接关闭连接且无 response。S0 已接受 sequence 后，若 `BeforeFirstByteWrite` 因 deadline 返回 false，sequence 保持已消费；该请求只写入一条不含 payload/template 的内存 `suppressed` message-ledger marker，`taskLedger` 不写入，业务 handler 不重入。原连接不得重放相同 sequence；新 connection epoch 收到相同 `MessageId` 时，先通过新的 H1-D，再命中该 marker，返回固定 generic `marcus-awake.error.v1`：`error_code=deadline_suppressed_replay`、`category=expired`、`retryable=false`、`fallback_allowed=false`、`safe_message=The prior response expired before delivery.`，不重新解析 B1 payload、不执行 Provider/Storage 业务、不创建新的 terminal template。marker replay 的 response policy 固定为 `suppressed_replay_error`；若新请求 H1-D 仍缺失/非法/过期，H1-D close/no-response 优先。只有完整 frame 写入并 flush 成功后才允许把 terminal template 写入 `messageLedger`；partial/zero/exception/flush-failure 都不得写入 terminal template。A0 的 suppression marker 只在 Service 进程内存中存在，不冒充 P3C durable receipt；其命中、ledger action 和无 task ledger 必须进入 service observation。

## 3.6.6 Revision 18 evidence closure

- `admission.trace` 只能是固定接收顺序 `H0 → H1-I → H1-C → H1-D → S0 → B0 → B1 → E0` 的合法前缀；`phase` 必须等于 trace 最后一项。header-only parser、business parser 和 dispatch 的调用次数分别由 Service 真实运行路径观察，不能由 writer 事后自由填写。所有 Service case 必须包含 observation 和至少一个 subcase；列入联合向量的 case 必须使用计划中固定的 subcase ID 集合。
- `deadline` 使用固定注入时钟 `clock_id=fixture-unix-ms-v1` 和 `deadline_now_unix_ms=4102444799000`。`now < deadline` 才是 `future_valid`；`now == deadline` 与 `now > deadline` 一律是 `expired`，比较结果写为 `now_at_or_after_deadline`。缺失、非整数、非正数、不可表示输入的 request/response deadline 证据字段使用 `null`，不得伪造整数；verifier 按 state、comparison、origin、response policy 联合校验。
- `observation.write` 的权威字段是 `failure_kind` 与嵌套对象路径 `ledger.state`。schema 和 verifier 同时约束八种写入组合：成功完整写入、S0 前未尝试写入、S0 后 deadline suppression、zero write、partial write、write exception（首字节前/后）和 flush failure；首字节后发生 partial/exception/flush failure 时必须 `first_byte_committed=true`、`response_sent=false`、`frame_complete=false`、`connection_closed=true`，实际 `bytes_written` 由受控 sink 返回值重算。
- `ledger.suppression` 必须记录 marker 的 message ID、sequence、connection epoch 与 replay 字段。`marked` 只能有 marker、不能有 replay；`replayed` 必须 `marker_hit=true`、message/sequence 相同且 replay 使用不同 connection epoch；verifier 必须从同一 run 的两次连接观察重算关联，不能只信 `ledger.state`。
- 每个 redaction capture binding 必须绑定同一 run 的 `run_id`、实际 Service PID、connection epoch、命令 executable/arguments、捕获时间和 artifact 相对路径；`capture_binding.artifact_sha256`、scan artifact `sha256` 与实际文件 SHA-256 必须三者相等。缺失 provenance、路径不一致、哈希不一致或 forbidden token 规则不一致直接失败。
- Provider focused runner 必须由 verifier 启动实际 executable，并要求 `arguments` 精确为 [`--p3d-a0-provider`]、`exit_code=0`、固定四行 stdout、空 stderr、零 HTTP 计数和 `external_network=false`；executable 必须以 `MarcusAwakeProvider.Tests.exe` 结尾，缺参数、额外参数、错参数、非零退出、任何 HTTP 计数或外网标志均 fail-closed。Service runner 同理要求 executable 以 `MarcusAwakeRuntimeService.exe` 结尾、`arguments` 精确为 [`--p3d-a0`] 且 `service_exit_code=0`。Provider 19 个 subcase 的 ID 必须完整唯一；每项 mapping 由 runner 调用 Transport wire vocabulary 与 `MarcusAwakeFramework.Api.FrameworkErrors.MapProviderError`（逻辑 authority ID 为 `marcus-awake.framework-errors.map-provider-error.v1`）（本批新增的 Framework public contract；当前基线尚不存在，未实现前不得生成 Provider mapping evidence） 的实际结果生成，verifier 逐项重算，不接受任意手填组合。

`## 3.6.6` 的规则是实现和 verifier 的共同契约；JSON Schema 负责闭集、必填和结构约束，verifier 负责跨字段关系、实际进程 provenance、实际文件哈希和代码映射重算。

## 4. Alternatives rejected

| 方案 | 结论 | 原因 |
|---|---|---|
| 在 envelope 顶层新增 capability 字段 | 拒绝 | capability 由握手交集和静态 message 映射决定，避免 caller 伪造权限。 |
| 以 ProviderId 作为 profile 唯一键 | 拒绝 | 同 Provider 可有不同 endpoint/model/credential，且会造成错误去重。 |
| 把 stream sequence 复用为 IPC sequence | 拒绝 | 传输顺序与业务事件顺序生命周期不同。 |
| P3D-A0 同时写 HTTP、SQLite profile、MCM 和游戏 Caller | 延后 | 这些是独立生命周期与证据边界，拆到 P3D-A1/A2/P3D-B/P4/P5。 |
| 通过普通 JSON 传 API Key | 拒绝 | secret 不得进入 IPC/Core/日志/环境变量。 |
| 仅靠 Provider 独立单测证明 Service 接线 | 拒绝 | P3D-A1/A2 必须用真实 Service 子进程和本地 fake server。 |

## 5. Ownership and write set

允许修改：

- `framework/MarcusAwakeTransport/src/ProtocolModels.cs`、`ProtocolValidation.cs`、`ProtocolCodec.cs`、`StrictJson.cs`、`PipeFrameIO.cs`、`SequenceWindow.cs`、新增 `ProviderProtocolContract.cs`；只允许 versioned IDs、静态 request/capability/output-schema 映射、task classification、外层 payload token 定位、header-only parser/validation、schema/shape/FSM、future-frame retry 修复和 first-byte write commit seam，不得改变 envelope wire 结构
- `framework/MarcusAwakeTransport/tests/Program.cs`（新增/恢复）、`ProviderContractTests.cs`（新增）、`MarcusAwakeTransport.Tests.csproj`；`Main(string[] args)` 必须只在收到 `--p3d-a0-contract` 时运行固定 A0 contract cases，默认无参数仍运行基线套件；csproj 在 `EnableDefaultItems=false` 下显式列出 Program、所有新增测试源文件，并固定 case 数量断言，不执行 Core mapping
- `framework/MarcusAwakeFramework/src/AiGatewayApi.cs`、`FrameworkErrors.cs`、新增 `AiTaskHandle.cs`；只允许 `StructuredJson`、通用 route metadata、64 KiB/16 depth/256 property/128 event bounds、terminal 规则、handle 状态机和 contract-only deferred mapping，不新增 Provider DTO 或 HTTP 文件
- `framework/MarcusAwakeFramework/tests/Program.cs`、`tests/TestDoubles/FrameworkCoreDoubles.cs`、`AiGatewayContractTests.cs`（新增）、`MarcusAwakeFramework.Tests.csproj`；固定 `--p3d-a0-core` 入口及 Core case 数量断言，并修正当前 `CS1955` 测试 API 调用
- `framework/MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs`、`Program.cs` 中的 `HandleConnectionAsync`、`ReadFrameAsync`、`HeaderOnlyParser`、`ValidateFrameAdmission`、`TryEnterFrame`/backpressure ownership、`WriteResponseAsync`/first-byte commit 接线，`SupportedCapabilities`、`ValidateFrame` 所调用的 capability/task classification、Provider contract-only dispatch、`TryValidatePayload` Provider 分支、`BuildResponse` deadline 继承和异常脱敏输出；只允许协议门控、resource gate、deadline/write commit 和 deferred 错误，不允许 Provider/HTTP/Storage 实现。生产 stderr 只允许稳定错误码、异常类型和 correlation 标识，不输出异常 message/stack
- `framework/MarcusAwakeRuntimeService/tests/Program.cs`、`P3DA0Harness.cs`、`tests/FirstByteGateStream.cs`（新增）、`tests/fixtures/p3d-a0-redaction-inputs.v1.json`（新增）和 `MarcusAwakeRuntimeService.Tests.csproj`；`Main(string[] args)` 必须显式分派 `--p3d-a0`，并由父进程启动真实 Service 子进程/私有 Named Pipe，逐案写出 machine evidence；csproj 在 `EnableDefaultItems=false` 下显式包含 Program、P3DA0Harness、FirstByteGateStream 及其他新增测试源文件，fixture 只作为内容复制/运行时输入不参与编译
- `framework/MarcusAwakeProvider/tests/Program.cs`、`ProviderContractMappingTests.cs`、`MarcusAwakeProvider.Tests.csproj`；`Main(string[] args)` 必须显式分派 `--p3d-a0-provider`，输出并由 verifier 采集固定四行 stdout、空 stderr、exit code、run provenance 与 19 个唯一 mapping subcase；测试工程必须新增对 `..\..\MarcusAwakeTransport\MarcusAwakeTransport.csproj` 和 `..\..\MarcusAwakeFramework\MarcusAwakeFramework.csproj` 的 ProjectReference 以调用真实 authority；verifier 必须检查两条引用和构建顺序，缺失或替换为手工复制均失败，生产 Provider 不增加该引用
- `tools/verify_marcus_awake_p3d_a0.ps1`；只负责按固定顺序构建并以精确参数运行 Transport/Core/Provider/Service runner，拒绝缺失或额外参数，采集实际 executable/arguments/run_id/PID/epoch/stdout/stderr，读取 redaction input manifest，使用 HashSet 校验 ID，重算 evidence 聚合、合法 trace、deadline boundary、write/lease/ledger 关系、代码 mapping、fixture provenance、实际文件哈希和脱敏结果；不是业务实现
- `docs/schemas/marcus-awake-p3d-a0-evidence.v1.schema.json`、新增 `docs/schemas/marcus-awake-p3d-a0-redaction-inputs.v1.schema.json`、`docs/checkpoints/MARCUS-AWAKE-P3D-A0-20260827-checkpoint.md` 与 `framework/MarcusAwakeRuntimeService/tests/_build_out/Release/MARCUS-AWAKE-P3D-A0-evidence.json`
- `framework/MarcusAwakeProvider/tests/ProviderContractMappingTests.cs` 与 `framework/MarcusAwakeRuntimeService/tests/fixtures/p3d-a0-redaction-inputs.v1.json` 的 fixture 内容必须由 verifier 读取并按固定 canonicalizer 重算；不得把 writer 生成的 source_sha256 当成权威
- 本计划与 `PLAN-MARCUS-AWAKE-P3D-RUNTIME-INTEGRATION-REVIEW-LOG-20260827.md`

禁止修改：

- `framework/MarcusAwakeRuntimeService/src/**` 中除上述最小协议门控、deadline 和脱敏输出外的 Provider handler、HTTP、凭据、Storage、profile registry 或持久化逻辑
- `framework/MarcusAwakeProvider/src/**` 的 Provider HTTP 行为（A0 只固定中立类别契约与 enum drift fixture，Adapter 修复归 A1/A2）
- `AWAKE.csproj`、`SubModule.xml`、AWAKE `src/**`、GUI/MCM
- 游戏目录、本地 Modules、dist、冻结候选
- 旧 Marcus 模块、旧 Companion、旧 pipe、旧 storage root
- P3B protocol major、pipe identity、bootstrap transcript 和 P3C durable semantics

## 6. Acceptance cases

| ID | 入口 → 结算 → 可观察结果 | 固定证据 |
|---|---|---|
| P3D-A0-01 | versioned IDs、message→capability 矩阵和 request→TaskScope output schema 矩阵可序列化/解析；真实 Service 对未申请 capability 的 Provider frame 在业务 payload 前返回 `capability_not_granted` | Transport contract tests + real Service child fixture |
| P3D-A0-02 | header-only gate 与业务 payload parse 的边界可观察；拒绝 capability 的 malformed/secret-like payload 不进入业务解析；通过 header gate 后再验证 profile upsert/remove/models/complete/stream 的字段、内外 schema和 scope | Transport header fixture + real Service child fixture |
| P3D-A0-03 | stream event 字段约束和 FSM 对正常序列、重复 terminal、terminal 后事件、缺 terminal、route-change 后 text（固定拒绝 `route_change_after_visible_text`）、usage/error 闭集和 oversize 做确定性结果 | stream FSM fixture |
| P3D-A0-04 | `AiTaskEvent.StructuredJson` 与通用 route metadata round-trip、旧构造函数兼容；Framework-owned `AiTaskHandle` 按固定线性化规则完成事件顺序/terminal/dispose/cancel 行为 | Framework focused tests |
| P3D-A0-05 | SequenceWindow future frame reject 不污染 retry；`--p3d-a0` 真实 Service response 逐字保留请求期限；settlement required 得到 typed unavailable | Transport/Core tests + real Service deadline fixture |
| P3D-A0-06 | 中立 Provider 19 类别契约与 Provider enum 逐字一致；Provider error→Core category/retryability/fallback 映射覆盖完整枚举矩阵，且不触发真实 HTTP | Provider drift runner + Framework mapping fixture |
| P3D-A0-07 | P3B `19/19`、P3C `7/7` 继续通过；旧 Marcus identity 继续拒绝；A0 write-set、secret/diagnostic redaction scan 和精确命令输出均通过 | pinned build/run commands + per-case evidence JSON |
| P3D-A0-08 | Provider task/cancel 缺少 `CausationId` 按固定优先级在业务 dispatch 前返回 `provider_causation_missing`；合法请求按 `SettlementRequirement` 在 `settlement_unavailable` 与 `provider_handler_deferred` 之间确定选择；两类错误 payload 均使用固定字段和安全模板 | Transport/Core tests + real Service child fixture |
| P3D-A0-09 | A0 evidence JSON 的 schema、case 数量、逐案字段、artifact hash、redaction 标记和 P3B/P3C regression 汇总可被独立验证 | evidence validator |

### 6.1 Required joint vectors

固定顶层 case 数量不变；下列联合向量必须作为对应 case 的 subcases[] 记录，并由 verifier 要求 ID 集合完整、无重复、无未知值：

| 顶层 case | 必须覆盖的 subcase ID |
|---|---|
| P3D-A0-T04-sequence_retry_non_contaminating | future_reject_no_record、gap_fill_retry_accept |
| P3D-A0-C06-task_scope_semantic_identity | owner_mismatch、session_mismatch、profile_mismatch、route_mismatch、semantic_hash_format_invalid、semantic_hash_roundtrip_unchanged |
| P3D-A0-C05-unknown_error_fail_closed | unknown_category_null_input、unknown_category_blank_input、unknown_category_case_mismatch |
| P3D-A0-S02-task_scope_output_schema_gate | scope_owner_mismatch、scope_session_mismatch、scope_profile_mismatch、output_schema_mismatch |
| P3D-A0-S03-causation_and_error_precedence | missing_causation_before_deferred、missing_deadline、invalid_deadline、settlement_precedence |
| P3D-A0-S05-deadline_preserved | deadline_missing_closed_no_response、deadline_invalid_closed_no_response、deadline_unrepresentable_closed_no_response、deadline_expired_closed_no_response、deadline_preserved_on_deferred、deadline_expired_before_first_byte_no_response、same_sequence_retry_requires_new_connection、same_connection_retry_after_post_s0_expiry_rejected、ipc_backpressure_no_sequence_consumption、slot_lease_released_exactly_once、post_s0_suppression_marks_ledger、suppressed_replay_uses_generic_error、suppressed_request_has_no_task_ledger、write_zero_result、write_partial_result、write_exception_result、flush_failure_result |
| P3D-A0-S07-redaction_no_payload_echo | stderr_artifact_bound、response_envelope_artifact_bound、deferred_payload_artifact_bound、evidence_json_artifact_bound |
| P3D-A0-P01-provider_error_mapping_19 | provider_invalid_request、provider_authentication、provider_forbidden、provider_not_found、provider_conflict、provider_rate_limited、provider_timeout、provider_unavailable、provider_server_unavailable、provider_transport_unavailable、provider_redirect_rejected、provider_policy_denied、provider_malformed_response、provider_incomplete_stream、provider_cancelled、provider_unsupported、provider_resource_exhausted、provider_corrupt_credential、provider_internal_failure |

每个 subcase 与顶层 case 使用相同的 id/passed/expected/observed/error_code 字段；Service subcase 还可携带与顶层相同的 `observation`，Provider mapping subcase 必须携带 `mapping`。observed 只能是有限长度的结果摘要，禁止放入原始 payload、异常原文、stack trace、credential 或 endpoint query。
## 7. Non-goals and follow-up batches

- 不启动 Bannerlord，不修改游戏目录，不声明 E3/E4/E5。
- 不实现 Provider HTTP、真实 Provider handler、fake HTTP/SSE 子进程链路；P3D-A1/A2。A0 只允许 contract-only deferred Service 分支。
- 不实现 profile/route durable schema、credential provisioning、DPAPI migration、Provider execution receipt；P3D-B。
- 不实现 MCM API Key 可见输入和模型/URL UX；P4。
- 不接 AWAKE 游戏 Caller/Host/session；P5。
- 不做真实云端 Provider 验证；仍需单独的外部证据。

## 8. Evidence and exit gate

`P3D-A0-E1`：Transport/Core/RuntimeService/Provider 四个 focused test project Release 构建；contract/shape/FSM/mapping fixtures 通过；P3B/P3C 回归、write-set、secret scan 通过。当前 Transport 与 Framework focused-test 基线已修复并通过，但仍不替代 P3D-A0 新增的逐案证据。

`P3D-A0-E2`：`MarcusAwakeTransport.Tests.exe --p3d-a0-contract`、`MarcusAwakeFramework.Tests.exe --p3d-a0-core`、`MarcusAwakeProvider.Tests.exe --p3d-a0-provider` 和 `MarcusAwakeRuntimeService.Tests.exe --p3d-a0` 分别完成固定的 Transport、Core、Provider 和真实 Service 子进程 cases；Service runner 作为父进程启动真实 `MarcusAwakeRuntimeService.exe`，经真实私有 Named Pipe 完成 header-only capability/task-scope/output-schema/deadline/causation/deferred cases；不访问外部网络，并生成逐案 `MARCUS-AWAKE-P3D-A0-evidence.json`。不接受只检查类是否存在。

退出条件：

- P3D-A0 的所有 IDs、payload 字段、权限矩阵、stream FSM、Core event 和错误映射均有代码与逐案证据。
- P3B/P3C 既有证据继续通过，且 A0 不改变 P3B/P3C 的业务行为。
- capability gate、task scope、内外 schema mismatch 和 deadline 由真实 Runtime Service 子进程执行验证；不能用纯 fixture 代替。
- Provider contract-only deferred/settlement 错误的 schema、字段、固定安全模板和 `CausationId` 由真实 Runtime Service 子进程执行验证；stream sequence/FSM/usage 边界由 Transport/Core fixture 覆盖，route candidate membership 明确留给 A2。
- Service 的异常输出、contract-only deferred 响应和 evidence JSON 均不得包含 payload 原文、credential、authorization、API key 或完整 endpoint query。
- 文档明确 Provider HTTP、Service handler、durable profile/credential、MCM、AWAKE Caller、真实云端和游戏验证仍未完成。

A0 evidence JSON 固定为 `marcus-awake.p3d-a0-evidence.v1`，顶层必须包含 `plan_revision=23`、`all_cases_passed`、`runners`、`regressions`、`artifacts` 和 `redaction`，禁止保存 payload 原文。`runners.transport` 固定 6 个 case：`P3D-A0-T01-versioned_and_capability_matrix`、`P3D-A0-T02-output_schema_matrix`、`P3D-A0-T03-header_only_opaque_payload`、`P3D-A0-T04-sequence_retry_non_contaminating`、`P3D-A0-T05-stream_usage_closed_set`、`P3D-A0-T06-stream_error_category_closed_set`；`runners.core` 固定 6 个 case：`P3D-A0-C01-structured_json_bounds`、`P3D-A0-C02-event_structured_result_and_route_metadata`、`P3D-A0-C03-handle_terminal_dispose_cancel`、`P3D-A0-C04-deferred_settlement_mapping`、`P3D-A0-C05-unknown_error_fail_closed`、`P3D-A0-C06-task_scope_semantic_identity`；`runners.provider` 固定 1 个 case：`P3D-A0-P01-provider_error_mapping_19`；Provider runner 除 `case_count/cases` 外必须输出 `exit_code=0`、四行固定 stdout（`PASS_COUNT=1`、`FAIL_COUNT=0`、`HTTP_REQUEST_COUNT=0`、`EXTERNAL_NETWORK=false`）、`http_request_count=0`、`external_network=false`；`runners.service` 固定 8 个 case：`P3D-A0-S01-capability_before_payload_parse`、`P3D-A0-S02-task_scope_output_schema_gate`、`P3D-A0-S03-causation_and_error_precedence`、`P3D-A0-S04-contract_only_error_shape`、`P3D-A0-S05-deadline_preserved`、`P3D-A0-S06-deferred_vs_settlement`、`P3D-A0-S07-redaction_no_payload_echo`、`P3D-A0-S08-p3b_p3c_regression`。每个 runner 必须记录 `case_count`、`cases[]`；每个 case 必须包含 `id`、`passed`、`expected`、`observed`、`error_code`，不得以一个 aggregate PASS 替代逐案记录。`regressions` 必须记录 P3B 顶层 `19`、P3B 细分 `24`、P3C `7` 及其通过标志。`runners.service` 还必须记录 `run_id`、父 runner 的 `runner_executable`/`runner_arguments`/`runner_exit_code`、子 Service 的 `service_executable`/`service_arguments`/`service_pid`/`connection_epoch`/`service_exit_code`、`deadline_expected_unix_ms=4102444800000`、`deadline_observed_unix_ms=4102444800000`、`deadline_preserved=true`；每个 Service case 必须有 machine-readable `observation`，且必须记录 write/lease/ledger 字段；capture binding 的 run_id/PID/epoch/命令必须与这些字段一致；Provider mapping case 的 19 个固定 subcase 必须各有 `mapping` 对象，且每项同时记录 `provider_category`、固定 `wire_category`、`core_category`、`core_retryable` 和 `fallback_allowed`。`redaction.scans` 的四项路径/source_id/input binding 必须与 3.6.4 和 schema 的固定映射逐字一致。`all_cases_passed` 必须由验证器重新计算，不得只信任 evidence writer 写入的聚合值。

脱敏 artifact 的 evidence root 固定为 `framework/MarcusAwakeRuntimeService/tests/_build_out/Release/`，四个相对路径必须逐字固定为：`artifacts/redaction/service-stderr.txt`、`artifacts/redaction/response-envelope.json`、`artifacts/redaction/deferred-payload.json`、`artifacts/redaction/evidence-canonical.json`。每个扫描记录必须绑定产生它的固定 case 与输入 fixture 的 SHA-256；verifier 先按相对路径读取实际文件并重算内容哈希，再按 `input_binding` 重算输入绑定，最后执行 forbidden-token 扫描。evidence-canonical.json 的输入绑定来自脱敏 case 的规范化聚合输入，不得把 evidence 文件自身哈希作为输入，避免自引用。

### 8.1 Evidence writer/verifier invariants

Evidence writer 可以写入 all_cases_passed，但 verifier 永远忽略该字段作为权威，按以下固定顺序重新计算并与其比对：
1. 顶层 schema、plan_revision=23、runner 键集合和固定 case 数量先通过 JSON Schema；证据文件无法解析或结构不符直接失败。
2. 对每个 runner 的 cases 使用 `HashSet<string>` 按 `id` 建立集合，再检查缺失、未知、重复 ID 和数量不符；Provider runner 还必须逐字检查 `exit_code`、固定 stdout 四行、`http_request_count=0`、`external_network=false`；不得依赖 JSON Schema 的 `uniqueItems`，因为它只能比较对象整体相等，不能阻止“内容不同但 ID 相同”。
3. 对每个 case/subcase 使用同样的 ID 集合检查；随后检查 `case_count == cases.length`、固定 subcase 集合、`passed/expected/observed/error_code` 完整性和长度限制。Service case 还必须检查 `observation.phase/sequence_consumed/response_policy/response_sent/write_started/first_byte_committed/frame_complete/connection_closed/bytes_written/frame_bytes_expected/flush_completed/failure_kind/slot_acquired/lease_release_count/ledger.state`，Provider mapping case 的 19 个 subcase 必须逐项检查 `provider_category/wire_category/core_category/core_retryable/fallback_allowed`。
4. 校验 Service runner 的 `service_exit_code`、固定 deadline 三字段和每个 deadline case 的 machine observation；H1-D close/no-response 必须是 `sequence_consumed=false`、`slot_acquired=false`、`lease_release_count=0`；首字节前过期必须是 `sequence_consumed=true`、`response_suppressed=true`、`write_started=false`、`first_byte_committed=false`、`frame_complete=false`、`bytes_written=0`、`connection_closed=true`、`failure_kind=gate_rejected`、`ledger.state=suppression_marked`。受控 sink 的 zero/partial/exception/flush-failure 还必须逐一匹配 `bytes_written`、`frame_complete=false`、`connection_closed=true` 和对应 `failure_kind`，成功响应才允许 `frame_complete=true` 与 `ledger.state=response_template_stored`。
5. 读取固定 redaction input manifest，按 canonicalizer 重算四项 `input_binding.source_sha256`；固定 artifact key、subcase、relative_path、source_id 不匹配直接失败。
6. 从固定实际文件重新计算 component/artifact SHA-256，并核对 Service PID、connection epoch、退出码和每个 redaction artifact 的固定相对路径、input_binding、`forbidden_match_count=0`；不能信任 writer 直接写入的 hash 或扫描标志。
7. 对四个固定 artifact 分别执行 forbidden token 扫描；重算全部 runner case/subcase、regression、mapping 和 redaction 结果，任一失败、未知、缺失或与 writer 的 `all_cases_passed` 不一致时，整个 evidence 标记为 rejected。


JSON Schema 只负责结构约束；上述语义不变量必须由 tools/verify_marcus_awake_p3d_a0.ps1 的确定性函数执行，并把失败原因写入 checkpoint，而不是只输出一行 aggregate PASS。
固定验证入口：

1. `$root = 'C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE'`
2. `dotnet build "$root\framework\MarcusAwakeTransport\MarcusAwakeTransport.csproj" -c Release`
3. `dotnet build "$root\framework\MarcusAwakeFramework\MarcusAwakeFramework.csproj" -c Release`
4. `dotnet build "$root\framework\MarcusAwakeRuntimeService\MarcusAwakeRuntimeService.csproj" -c Release`
5. `dotnet build "$root\framework\MarcusAwakeTransport\tests\MarcusAwakeTransport.Tests.csproj" -c Release`
6. `dotnet build "$root\framework\MarcusAwakeFramework\tests\MarcusAwakeFramework.Tests.csproj" -c Release`
7. `dotnet build "$root\framework\MarcusAwakeRuntimeService\tests\MarcusAwakeRuntimeService.Tests.csproj" -c Release`
8. `dotnet build "$root\framework\MarcusAwakeProvider\tests\MarcusAwakeProvider.Tests.csproj" -c Release`
9. `Push-Location "$root\framework\MarcusAwakeTransport\_build_out\tests\Release"; & '.\MarcusAwakeTransport.Tests.exe' --p3d-a0-contract; Pop-Location`
10. `Push-Location "$root\framework\MarcusAwakeFramework\_build_out\tests\Release"; & '.\MarcusAwakeFramework.Tests.exe' --p3d-a0-core; Pop-Location`
11. `Push-Location "$root\framework\MarcusAwakeRuntimeService\tests\_build_out\Release"; & '.\MarcusAwakeRuntimeService.Tests.exe'; & '.\MarcusAwakeRuntimeService.Tests.exe' --p3c; & '.\MarcusAwakeRuntimeService.Tests.exe' --p3d-a0; Pop-Location`
12. `Push-Location "$root\framework\MarcusAwakeProvider\tests\_build_out\Release"; & '.\MarcusAwakeProvider.Tests.exe' --p3d-a0-provider; Pop-Location`
13. `& "$root\tools\verify_marcus_awake_p3d_a0.ps1"`

P3D-A0 专用 harness 的固定入口为：

`Push-Location 'C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeRuntimeService\tests\_build_out\Release'; & '.\MarcusAwakeRuntimeService.Tests.exe' --p3d-a0; Pop-Location`

拓扑固定为：父测试 runner `MarcusAwakeRuntimeService.Tests.exe --p3d-a0` → 子 `MarcusAwakeRuntimeService.exe`（无命令行参数、通过 stdin bootstrap）→ `MarcusAwake.Runtime.v2.<user-sid-fingerprint>`（真实私有 Named Pipe）。evidence 分开记录父 runner 的 executable/arguments/exit_code 与子 Service 的 executable/arguments/PID/epoch/exit_code；harness 必须记录 service PID、service artifact hash、connection epoch、每个 case 的 expected/observed error kind（不记录 payload 原文）。

P3D-A0 的脱敏断言固定为：捕获 Service stderr、响应 envelope、contract-only deferred payload 和 evidence JSON，均不得出现 `api_key`、`authorization`、`password`、`secret`、`credential`、原始 `PayloadJson`、完整 endpoint query 或 stack trace；Service 生产日志只允许稳定错误码、异常类型和 message/correlation 标识。

P3B hard gate 固定为以下 19 个 `RunCaseAsync` 顶层 ID，运行器标准输出必须为 `PASS_COUNT=19`、`FAIL_COUNT=0`：`P3B-01 service_start_descriptor_pipe`、`P3B-03 legacy_pipe_rejected`、`P3B-03 wrong_protocol_rejected`、`P3B-03 wrong_service_instance_rejected`、`P3B-03 wrong_sid_rejected`、`P3B-03 wrong_parent_rejected`、`P3B-03 wrong_nonce_rejected`、`P3B-02 valid_handshake_and_health`、`P3B-04 echo_response`、`P3B-05 checksum_and_length_rejected`、`P3B-05 duplicate_and_deep_json_rejected`、`P3B-05 oversized_frame_rejected`、`P3B-06 duplicate_and_conflicting_replay`、`P3B-06 sequence_gap_rejected`、`P3B-08 cancellation_and_terminal_replay`、`P3B-03 replayed_challenge_rejected`、`P3B-07 reconnect_new_epoch`、`P3B-08 disconnect_does_not_orphan`、`P3B-09 parent_exit_orphan_cleanup`。现有 P3B evidence 的 `cases` 字段是只追加成功细分检查项的字符串数组，不含 `passed` 字段；因此验证器必须同时要求该数组精确包含以下 24 个细分 ID，且不得把细分数量误写成 19：`P3B-01`、`P3B-03-legacy`、`P3B-03-protocol`、`P3B-03-service`、`P3B-03-sid`、`P3B-03-parent`、`P3B-03-nonce`、`P3B-02`、`P3B-04-health`、`P3B-04-echo`、`P3B-05-checksum`、`P3B-05-length`、`P3B-05-duplicate`、`P3B-05-depth`、`P3B-05-size`、`P3B-06-duplicate`、`P3B-06-conflict`、`P3B-06-gap`、`P3B-08-cancel`、`P3B-08-terminal-replay`、`P3B-03-replay`、`P3B-07`、`P3B-08-disconnect`、`P3B-09`。验证器不能要求当前 P3B JSON 增加未存在的 `case_count` 或 `passed` 字段。

P3C hard gate 固定为 7 个 case ID：`P3C-01 kv_round_trip`、`P3C-02 timeline_idempotency_conflict`、`P3C-03 rag_scope_and_stale_corpus`、`P3C-04 capability_denial`、`P3C-05 malformed_and_oversized`、`P3C-06 restart_receipt_replay`、`P3C-07 crash_after_commit_before_response`；现有 evidence 不含 `case_count` 字段，验证器必须要求 `cases.Count=7`、`all_cases_passed=true`、每项 `passed=true`，且两个 durable flags 均为 true。

deadline fixture 使用固定请求值 `4102444800000`，必须记录 `deadline_expected_unix_ms=4102444800000`、`deadline_observed_unix_ms=4102444800000` 和 `deadline_preserved=true`；不得用动态当前时间作为唯一证据。

Provider mapping fixture 必须固定 19 个逐项 subcase，按以下不可变 wire vocabulary 记录 `provider_category/wire_category/core_category/core_retryable/fallback_allowed`；wire 字符串必须逐字为 `invalid_request`、`authentication`、`forbidden`、`not_found`、`conflict`、`rate_limited`、`timeout`、`unavailable`、`server_unavailable`、`transport_unavailable`、`redirect_rejected`、`policy_denied`、`malformed_response`、`incomplete_stream`、`cancelled`、`unsupported`、`resource_exhausted`、`corrupt_credential`、`internal_failure`，顺序与 `ProviderErrorCategory` 枚举一致；缺失、未知、增加、删除、改名或映射漂移均失败；未知值必须映射为 `InternalFailure,false,false`。

StructuredJson fixture 必须覆盖空字符串、合法 object、array/scalar 拒绝、64 KiB 边界、64 KiB+1 拒绝、深度 16 边界、深度 17 拒绝、属性 256 边界、属性 257 拒绝和 snapshot 128/129 边界。

命令输出、Service/Transport/Core 二进制 SHA-256、源码 write-set 和脱敏扫描结果必须写入 P3D-A0 checkpoint；当前证据上限仍为离线 E2。

## 9. Follow-up boundary

- `MARCUS-AWAKE-P3D-A1-UNARY-PROVIDER-BRIDGE`：在 A0 contract approved 后，实现 Service 内存 profile registry、credential read-only binding、models/complete handler、Core unary mapping 和 fake HTTP child-process evidence。
- `MARCUS-AWAKE-P3D-A2-STREAM-FALLBACK`：独立实现 multi-frame stream writer/reader、fallback、cancel/disconnect terminal 和 SSE/NDNDJ evidence。
- `MARCUS-AWAKE-P3D-B-PROFILE-CREDENTIAL-DURABILITY`：独立实现 profile/route persistence、credential provisioning/DPAPI、restart recovery 和 Provider execution receipt。

## 10. Governance handoff

- `plan_status`: `implementing_revision_23`
- `review_status`: `approved_independent_read_only_review`
- `user_signoff_required`: `true`；`user_signoff_status`: `satisfied_by_explicit_autonomous_full_migration_authorization_2026-08-26`
- `implementation_authorized`: `true`（revision 23 已通过独立只读 `VERDICT: APPROVED`）
- `minimum_evidence`: `P3D-A0-E1 + P3D-A0-E2`
- `implementation_blocked_until`: `none`
## Revision 14 disposition

- Accepted all Review 15 findings as valid and converted them into executable rules rather than descriptive intent.
- Added one authoritative admission coordinator with explicit H0/H1/S0/B0/B1/E0 ordering, sequence-consumption behavior, deadline response suppression, and deterministic joint vectors.
- Added the evidence writer/verifier invariant contract: exact ID sets, semantic aggregate recomputation, artifact hash recomputation, service observation fields and four-output negative redaction scans.
- Added the cross-project dependency/authority graph and limited the new Transport reference to Provider tests only; no Provider HTTP, credentials, MCM, AWAKE Caller or game-directory work is included.
- Updated the evidence schema contract to revision 14 and kept the implementation gate closed pending independent approval.

## Review 16 gate — revision 14

- review_status=pending_independent_read_only_review
- implementation_authorized=false until revision 14 receives VERDICT: APPROVED


## Revision 15 disposition

- Recorded Review 16 的五项 REVISE findings as valid; no production implementation approval is inferred.
- Unified every admission description and real Service receive path to H0 → H1-I → H1-C → H1-D → S0 → B0 → B1 → E0, with the concrete ReadFrameAsync → HeaderOnlyParser → ValidateFrameAdmission → B1 business parser → Dispatch/response wiring.
- Replaced all deadline alternatives with unique close/no-response or exact-deadline-inherit outcomes and defined the response first-byte linearization point.
- Froze four redaction artifact paths, content hashes, scan flags, zero forbidden-match counts and input bindings; made verifier ID checks explicitly HashSet-based rather than relying on `uniqueItems`.
- Implementation remains closed until an independent read-only review returns the exact VERDICT: APPROVED for revision 15.

## Review 17 gate — revision 15

- review_status: pending_independent_read_only_review
- implementation_authorized: false until revision 15 receives VERDICT: APPROVED


## Revision 16 disposition

- Recorded Review 17 的六项 REVISE findings as valid; no production implementation approval is inferred.
- Unified H0 transport syntax, H1-C integrity, S0 resource/sequence ownership and B0/B1 admission so TryEnterFrame/ipc_backpressure cannot bypass the single coordinator.
- Removed deadline errors from generic response handling, fixed H1-D close/no-response and post-S0 first-byte commit semantics, and added explicit same-sequence retry rules.
- Added the PipeFrameIO first-byte write-commit seam and deterministic harness observation contract.
- Added a fixed redaction fixture manifest, schema-level artifact/source mapping, machine-readable Service/Provider evidence fields and explicit Provider build/run command.
- Implementation remains closed until an independent read-only review returns the exact VERDICT: APPROVED for revision 16.

## Review 18 gate — revision 16

- review_status: pending_independent_read_only_review
- implementation_authorized: false until revision 16 receives VERDICT: APPROVED


## Revision 17 disposition

- Recorded Review 18 的十项 REVISE findings as valid; no production implementation approval is inferred.
- Moved `frame_fence_proof` exclusively to H1-C; made Framework the sole semantic request hash authority and Service a format-check/pass-through boundary.
- Added a single `FrameSlotLease` lifecycle with exactly-once release, explicit backpressure evidence, and a controlled sink contract covering zero/partial/exception/flush-failure writes.
- Added required machine-readable write/lease/ledger observations, Service subcase observations, 19 Provider mapping subcases with fixed wire strings and a no-network runner output contract.
- Added the independent redaction-input schema and fixed artifact→case→subcase→fixture mapping with per-artifact input field whitelists.
- Fixed post-S0 deadline suppression: sequence remains consumed, suppression marker is recorded without task/terminal template ledger, reconnect replay returns one fixed generic error, and H1-D still has precedence.
- Explicitly added `MarcusAwakeRuntimeService.Tests.csproj` to the write set and kept implementation closed pending an independent read-only review.

## Review 19 gate — revision 17

- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false` until revision 17 receives `VERDICT: APPROVED`

## Revision 18 disposition

- Recorded the two independent read-only Review 20 findings as valid: runner parameters and test write set were not actually bound, Service evidence was optional, deadline boundary and write field names were ambiguous, suppression provenance was insufficient, redaction policy lacked executable rules, Provider output was not actual process evidence, and mapping subcases were not bound to a unique authority path.
- Closed the admission trace prefix, `now == deadline` boundary, nullable invalid-deadline evidence, write outcome combinations, suppression marker linkage, capture provenance/hash equality, executable Provider runner provenance and actual Core mapping recomputation.
- Updated the evidence schema and redaction input manifest to revision 18, preserving the no-production-code gate until a fresh independent read-only review returns the exact `VERDICT: APPROVED` for revision 18.

## Review 21 gate — revision 18

- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false` until revision 18 receives `VERDICT: APPROVED`

## Review 21 result — revision 18

- reviewer: independent read-only reviewer Euclid
- review_status: completed
- verdict: REVISE
- P1 findings: Core mapping authority was only a planned name and had no exact callable symbol, signature or result type; the redaction schema allowed mutable self-consistent sensitive digest/input values; focused runner arguments, Service exit semantics and the mandatory Provider test references were not bound to the executable write set.
- The reviewer also confirmed that the current baseline code has no A0 implementation, so no implementation approval is inferred.

## Revision 19 disposition

- Clarified the logical mapping authority ID, exact Framework namespace/type/member, public method signature, result type and unknown-category behavior; the implementation write set now adds the callable mapping result contract, and Provider tests must reference Transport and Framework.
- Closed each redaction artifact to fixture-specific const input fields, hashes and ordered sensitive digest tuples; the verifier must carry immutable expected constants and reject synchronized input/hash mutations.
- Bound Provider and Service runner arguments, executable suffixes and exit codes in both plan and evidence schema; these fields are mandatory and verifier-enforced.
- Revision 19 remains contract-only and implementation remains closed until a fresh independent read-only review returns `VERDICT: APPROVED` for revision 19.

## Review 22 gate — revision 19

- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false` until revision 19 receives `VERDICT: APPROVED`
## Review 22 result — revision 19

- reviewer: independent read-only reviewer Heisenberg
- review_status: completed
- verdict: REVISE
- P1 findings: the evidence schema still identified plan revision 18; Service runner provenance conflated parent harness and child Service; the Service and Provider focused runners were not yet dispatched by their current entry points; Provider test ProjectReference paths were absent and the plan paths were one directory short; the Core mapping method remained planned rather than implemented.
- The reviewer confirmed the redaction schema is now fixture-specific and closed, but mutation rejection and independent verifier evidence remain unimplemented. No implementation approval is inferred.

## Revision 20 disposition

- Unified plan and evidence schema to plan revision 20.
- Split Service provenance into parent runner and child Service executable, arguments and exit code; the child receives no command-line argument, while the parent receives exactly `--p3d-a0`.
- Corrected the Provider test ProjectReference paths to resolve from `framework\MarcusAwakeProvider\tests` and made the verifier check both references and build order.
- Kept the Core mapping authority exact and added the remaining implementation requirement: the public result type/method must be implemented and exercised by the Provider runner before evidence can pass.
- Revision 20 remains contract-only and implementation remains closed until a fresh independent read-only review returns `VERDICT: APPROVED` for revision 20.

## Review 23 gate — revision 20

- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false` until revision 20 receives `VERDICT: APPROVED`

## Revision 21 disposition

- Recorded the remaining revision 20 findings as valid without opening the production implementation gate.
- Closed the exact immutable `ProviderErrorMapping` result shape, input validation and preservation rules, and unknown-category result.
- Added the explicit `service_child` capture role and executable/arguments equality requirement.
- Updated the active plan status and evidence contract to revision 21; missing verifier and runner files remain implementation-stage deliverables, not contract-review failures.

## Review 24 gate — revision 21

- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false` until revision 21 receives `VERDICT: APPROVED`

## Revision 22 disposition

- Recorded the revision 21 review findings as valid without inferring implementation approval.
- Locked the public type of every `ProviderErrorMapping` property and kept all properties get-only.
- Added the fixed unknown/blank/case-mismatch mapping evidence vector to Core case `P3D-A0-C05-unknown_error_fail_closed`.
- Made `capture_binding.process_role` schema-required and kept child Service command equality verifier-enforced.
- Updated active evidence references to revision 22; implementation remains closed until a fresh independent read-only review returns `VERDICT: APPROVED` for revision 22.

## Review 25 gate — revision 22

- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false` until revision 22 receives `VERDICT: APPROVED`

## Revision 23 disposition

- Recorded the revision 22 review findings as valid without inferring implementation approval.
- Closed the four fixed artifact-key to relative-path pairs in both the contract and verifier requirements.
- Updated active plan status to revision 23; implementation remains closed until a fresh independent read-only review returns `VERDICT: APPROVED` for revision 23.

## Review 26 gate — revision 23

- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false` until revision 23 receives `VERDICT: APPROVED`

## Review 26 result — revision 23

- reviewer: independent read-only reviewer Dirac
- review_status: completed
- verdict: APPROVED
- No contract defects remained after the revision 23 fixes. Implementation gate is open for the locked A0 write set only.
