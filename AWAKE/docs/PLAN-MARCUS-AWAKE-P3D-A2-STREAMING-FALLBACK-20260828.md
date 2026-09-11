# Marcus-Awake P3D-A2 流式 Provider 与 fallback 计划

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3D-A2-STREAMING-FALLBACK-20260828`
- `plan_revision`: `8`
- `plan_status`: `implementation_authorized`
- `review_status`: `approved_independent_read_only_review`
- `user_signoff_required`: `satisfied_by_explicit_autonomous_full_migration_authorization_2026-08-26`
- `primary_executor`: `controller`
- `minimum_evidence`: `P3D-A2-E1 + P3D-A2-E2`
- `evidence_ceiling`: offline `E2`; no real cloud Provider, Bannerlord or game-directory claim

## Revision 4 correction set

- Stream output now has one canonical pre-attempt `started` event. The router emits it before constructing or invoking any Provider enumerator; adapter-provided `started` events are validated and suppressed. This closes request-validation, credential, first-HTTP-failure, empty-route and missing-anchor cases without violating the stream FSM.
- A missing route anchor fails closed: the router emits the canonical synthetic `started` for the requested anchor and one typed `failed(route_anchor_unavailable|route_no_candidate)` terminal, without trying an unrelated candidate. An existing anchor may fall back through the frozen candidate snapshot; every `P→Q→R` transition emits exactly one `route_changed` and never forwards the next adapter's `started`.
- `provider_stream_event` now carries optional `structured_json` for `completed` only. It is validated as a JSON object using the Core limits; structured-output tasks remain on the stream path and must not silently lose the structured result.
- Host stream execution has an explicit working ledger, terminal claim and commit point. A complete frame template is appended only after its envelope write succeeds; only a fully written terminal sequence is atomically promoted to the replay ledger. Cancellation, write failure, disposal failure and disconnect have a fixed precedence and never create a second terminal.
- The effective resource budget is the minimum of the frame, serialized output-byte, text-delta and token budgets supplied by the request, Framework and Service. The event formula reserves the terminal slot; accumulated usage is validated and settled at the terminal rather than treated as an unbounded frame budget.
- Replay now has a field-by-field preserve/recompute/override matrix, including `AckStatus`, `OutcomeKind`, `ErrorCode`, `NonDurable`, `EventIndex`, `FenceProof`, payload length/hash and all request identities.
- The fixed transcript cases are expanded so every case specifies Provider event kind, Provider stream sequence, IPC sequence, EventIndex, active Provider, Framework sequence and terminal behavior. Invalid post-terminal input is rejected before forwarding and then discarded, not converted into a second terminal.

## Revision 5 correction set

- The structured-output write set is explicit: `ProviderContracts.cs`, `ProviderAdapterBase.cs`, `ProviderAdapters.cs`, `ProviderWireAdapter.cs`, `ProviderRegistry.cs`, Transport codec/validation files, `RuntimeServiceApi.cs`, `RuntimeService.cs`, `RuntimeServiceClient.cs` and the corresponding tests. `ProviderStreamEvent.StructuredJson`, completed-event construction, wire serialization, Core validation and Framework mapping must be changed together.
- The resource budget now has a real transport shape. `RuntimeResourceBudget` gains `FrameCount` (the existing four-argument constructor remains a compatibility overload defaulting to the framework cap); the stream request carries `frame_count`, `output_bytes`, `tokens` and `text_deltas`; Framework and Service each publish their caps; the effective value is the minimum at the Service boundary.
- Stream credential ownership is explicit: a credential lease remains alive from profile lookup through enumerator construction, all `MoveNextAsync` calls and `DisposeAsync`; it is released only after terminal/abort cleanup. The existing unary credential helper keeps its current shorter lifetime.
- Replay now classifies every `PipeEnvelope` and `TaskScopeEnvelope` field, including deadline, payload schema, checksum algorithm, outer owner, output schema versions and settlement requirement. Only legal existing `AckStatus`/`OutcomeKind` values are used.
- The transcript matrix now gives complete W/I/E/F values for disconnect and separate variants for no-candidate, missing-anchor and each protocol/structured-output rejection. Suppressed adapter `started` events consume no frame or sequence budget; the old `SuppressedProviderStarted=1` subtraction is removed.
- Stream capability fallback is explicit: when `provider.stream.v1` is unavailable, all requests—including structured-output requests—use unary `provider.complete.v1`; when unary is also unavailable, return `stream_capability_unavailable`/`complete_capability_unavailable` without issuing a Provider request.

## Revision 6 execution correction set

- Stream text semantics are now single-owner. Provider adapter `text_delta` values are incremental UTF-8 text. Adapter `completed.text` is treated as an internal aggregate only: Router emits one normalized `text_delta` when no earlier delta was emitted, discards the aggregate after any visible delta, and exposes an empty `completed.text` on the wire. Framework never appends terminal text a second time. `usage_update` and terminal `usage` are cumulative snapshots; a lower later value is rejected as `usage_non_monotonic` and the last accepted snapshot is used for settlement.
- Credential ownership is now an explicit `ProviderStreamSession` contract. `ProviderRegistry.BeginStream` takes an atomic candidate snapshot under `sync`, creates one idempotent lease per candidate, and returns a session owning those leases. The Host is the sole session disposer: it calls `GetAsyncEnumerator` once, keeps the session alive through every `MoveNextAsync`, terminal write and `DisposeAsync`, then releases it. No lease is released while an async enumerator is still reachable; `GetAsyncEnumerator`, `MoveNextAsync`, `DisposeAsync` and credential-store failures map to typed redacted errors.
- Stream replay now has explicit states and lock order. Under `sync`, admission reserves `working` and records `activeTasks`; no await occurs while holding `sync`. The Host releases `sync` before acquiring the per-connection `WriterGate`; no path acquires `sync` while holding `WriterGate`. A frame enters the working ledger only after its complete envelope write succeeds. A terminal write succeeds before the ledger is atomically promoted to `committed`; only committed ledgers are replayable. Write failure, disconnect, cancellation before terminal or disposal failure marks `aborted`, removes the active/idempotency reservation and never promotes a partial stream. Same-key committed requests replay all frame templates; same-key working requests return `task_in_progress`; a different payload returns `provider.idempotency_conflict` without Provider execution.
- Stream budget and capability behavior are executable. `RuntimeResourceBudget.FrameCount` includes the canonical `started` and exactly one terminal; effective limits are `min(request, Framework, Service)` for frame count, serialized payload bytes, UTF-8 text-delta bytes, and known cumulative tokens. Effective frame count below `2` is rejected as `stream_budget_invalid` before Provider invocation; the upper cap is `125`. The default requested capabilities include `provider.stream.v1`. If that capability is absent, Framework uses the existing unary operation and locally projects `Accepted -> Started -> optional TextDelta/UsageUpdate -> Completed/Failed`, preserving `StructuredJson`; if both capabilities are absent it returns `stream_capability_unavailable`/`complete_capability_unavailable` without a Provider call.
- Structured output is a canonical JSON string, never an arbitrary object or Provider-native fragment. A completed stream may carry `structured_json` only when the request has `response_schema_json`; the root must be an object, UTF-8 bytes must be `<= 65536`, depth `<= 16`, properties `<= 256`, and duplicate property names are rejected. The aggregate text is normalized once at terminal construction; the structured value is not copied into `text`, and malformed/oversized/duplicate-key output becomes `structured_json_invalid` with no raw JSON or exception text on the wire.
- Deadline and fallback are bounded by the injected Provider clock. The Router checks the deadline before each candidate, after every provider read, and after enumerator disposal. `RetryAfter` is diagnostic only in A2; no sleep is performed. A retryable failure may move to the next frozen candidate only when no visible text was emitted and time remains; if the deadline has expired, the terminal is `request.deadline_expired`. Dispose/lease errors are handled before the next candidate and never bypass the visible-output gate.
- Candidate snapshot and anchor rules are fixed. `BeginStream` copies the complete matching candidate set and its credential leases while holding `sync`; later profile upsert/remove cannot alter that stream. Candidates are ordered by the registered route ordinal, then provider ID, then effective model identity. The requested `TaskScope.ProviderId` must be present as the first anchor; a missing anchor emits canonical `started(anchor)` followed by `route_anchor_unavailable` and performs zero Provider HTTP calls. A zero candidate set emits `started(anchor)` followed by `route_no_candidate`.
- Protocol tests are expanded into independent rejection cases for unknown fields, empty deltas, non-monotonic usage, terminal field combinations, invalid route metadata, duplicate structured keys, depth/property/byte limits, frame count `0/1`, and each lease/replay race. These cases must assert no raw Provider payload, credential, exception text, second terminal or partial replay artifact is observable.

- Revision 6 was superseded by revision 7 and is retained only as historical correction context.

## Revision 7 authoritative execution contract

This section overrides any earlier wording that conflicts with it and is the only implementation authority for A2.

- **Terminal text and usage:** `text_delta.text` is incremental and is the only streamed text that Framework appends. `ProviderStreamEvent.Completed.Text` is adapter-internal aggregate input; Router emits it as one `text_delta` only when no prior visible delta exists, otherwise discards it, and every wire `completed` payload omits the `text` property. Unary fallback follows the same projection: one optional full-content `text_delta`, one optional cumulative `usage_update`, then `completed` with no text. `usage_update` and the terminal usage snapshot are cumulative per-field values; an omitted field keeps its prior value, a present field may not decrease, and a known input-plus-output total above the effective token limit fails before that usage event is forwarded. Missing usage is allowed but consumes no known-token budget and is recorded as unknown.
- **Wire field closure:** `started` has exactly the identity fields and `event_kind/stream_sequence`; `text_delta` additionally has non-empty `text`; `usage_update` additionally has `usage`; `route_changed` additionally has `from_provider_id/to_provider_id`; `completed` may have only `structured_json` and `usage`; `cancelled`/`failed` have only typed `error`. No wire `completed.text`, Provider-native fragment, raw exception, credential or unknown property is permitted. The Transport state machine gains explicit structured/usage validation while preserving the existing event names and outcome values.
- **Concrete stream session:** `ProviderRegistry.BeginStream(PipeEnvelope, ProviderWireRequest, deadline, cancellationToken)` returns a `ProviderStreamSession`, not a bare enumerable. The session implements `IAsyncEnumerable<ProviderWireStreamEvent>` and `IAsyncDisposable`, owns the immutable candidate descriptors and all credential leases, allows exactly one `GetAsyncEnumerator` call, and is disposed exactly once by Host after terminal write or abort cleanup. The authoritative call chain is `RuntimeServiceHost.HandleProviderBusinessAsync -> ProviderRegistry.BeginStream -> ProviderStreamSession -> ProviderRouteReflectionBridge -> ProviderRouter.StreamAsync -> ProviderStreamReflectionBridge`; no second Service fallback loop exists and Router never disposes session-owned base credentials.
- **Atomic snapshot without awaiting under a lock:** `Upsert` assigns a monotonic `RegistrationOrdinal` and increments a per-entry generation under Registry `sync`. `BeginStream` copies matching immutable descriptors (same owner/campaign/timeline/session, route, profile set), anchor identity and generations under `sync`, then releases it before credential lookup. After leases are acquired outside the lock, it re-enters `sync` and verifies every generation; on change it disposes the acquired leases and retries once, then returns `provider_profile_changed` without Provider HTTP. The frozen snapshot is sorted by `RegistrationOrdinal`, then provider ID, then effective model identity; the requested `TaskScope.ProviderId` must be the first anchor. No anchor yields `started(anchor) -> route_anchor_unavailable`; an empty matching set yields `started(anchor) -> route_no_candidate`, both with zero HTTP calls.
- **Bounded async waits:** Every `GetAsyncEnumerator`, `MoveNextAsync` and `DisposeAsync` operation is raced against the request deadline and linked cancellation using a bounded `Task.WhenAny` helper. A provider that ignores cancellation cannot hold the caller past the deadline: Host claims one timeout terminal and aborts the ledger. Enumerator disposal and credential release continue in a detached cleanup task with a fixed `250 ms` grace window; the lease is released only after the underlying operation completes, and the session records `cleanup_deferred` without starting a fallback candidate. No unbounded await occurs in the request path.
- **Complete lock order and terminal race:** Host state `sync` is rank 1, per-stream `StreamLedger.Gate` is rank 2, and connection `WriterGate` is rank 3. `sync` is never held across an await; code never acquires rank 1 or 2 while holding rank 3. Client `requestGate` is process-local and is held while the request connection reads the complete response sequence; cancellation uses the separate control connection and never participates in the Host lock order. A ledger transitions `working -> terminal_claimed -> committed` or `working/terminal_claimed -> aborted` under its gate. Serialization/write failure before a successful terminal write aborts and removes the active/idempotency reservation; after a successful terminal write, commit is unconditional and wins cancellation/disconnect races. Only `committed` ledgers are replayable, and every replay frame is written under the same WriterGate with a new envelope identity.
- **Single budget definition and publication:** `RuntimeResourceBudget` keeps the existing four-argument constructor and adds `FrameCount` with default `125`. For A2, `FrameCount` counts emitted stream events including canonical `started` and one terminal; `OutputBytes` is the sum of UTF-8 bytes in forwarded `text_delta.text` plus terminal `structured_json`; `TextDeltas` counts forwarded text-delta events; `Tokens` counts the known cumulative input-plus-output usage. Serialized envelope size is a separate `MaximumFrameBytes` check, and Provider adapter `MaxStreamBytes/MaxDeltas` remains an independent raw-provider parser limit. The Service publishes `stream_budget={frame_count:125,output_bytes:4194304,tokens:32768,text_deltas:4096}` in the handshake response; Framework publishes the same cap in its request projection. Effective values are the minimum of request, Framework and Service values; `FrameCount < 2` fails as `stream_budget_invalid` before Provider invocation, and values above the cap are clamped to the published cap.
- **Structured JSON canonicalization:** `structured_json` is a UTF-8 JSON string. Core canonicalizes it by parsing with the bounded parser, rejecting duplicate property names at every object, preserving property encounter order, emitting compact JSON with deterministic escaping and number spelling, and validating root object/depth/properties/bytes. The canonical string is produced once at terminal construction and reused for wire, Framework event and replay template; it is never copied into `text`. Any failure maps to `structured_json_invalid` without returning the source string.
- **Deadline and fallback:** `ProviderClock.UtcNow` is injected into Router. The Router checks deadline before candidate creation, before every read, after every read and after disposal. `RetryAfter` is diagnostic only; A2 performs no sleep. A retryable failure can move to the next frozen candidate only before visible text and while time remains; after visible text, after a terminal claim, after timeout or after deferred cleanup it cannot fallback. A capability-missing stream request uses the unary projection above; if unary is also unavailable, no Provider request is made and the Framework receives a typed capability failure.
- **Bounded replay retention:** The non-durable committed stream ledger is capped at `64` ledgers and `16 MiB` of serialized templates, with a `15 minute` TTL. Eviction removes only the oldest committed ledger; an evicted idempotency key is treated as a new non-durable request, while a working key still returns `task_in_progress` and a different request hash still returns `provider.idempotency_conflict`. Working ledgers are never evicted; if the cap would be exceeded by a new request, admission returns `runtime.ledger_capacity` without Provider invocation.
- **Revision 7 tests:** The fixed transcript cases additionally assert no `completed.text`, cumulative usage monotonicity and unknown-usage behavior; exact `BeginStream` ownership and generation-retry behavior; uncooperative `MoveNextAsync`/`DisposeAsync` deadline races; the complete lock-order/terminal-claim matrix; handshake budget projection, frame count `0/1` and clamping; canonical duplicate-key/number/escape output; registration-ordinal fallback order; and ledger TTL/capacity eviction semantics.

- Revision 7 was superseded by revision 8 and is retained only as historical correction context.

## Revision 8 final closure rules

The following rules supersede every earlier A2 sentence about these topics and are intended to be the final implementation contract.

- **Admission before credentials:** `RuntimeServiceHost` first calls a synchronous, metadata-only `ProviderRegistry.DescribeCandidates` under Registry `sync`; it does not read credentials or construct a Provider adapter. Host then creates and writes the canonical `started(anchor)` frame and records it in the working ledger. Only after that write succeeds may `ProviderRegistry.BeginStream` acquire leases and construct the session. Candidate/anchor failure therefore always has `started(anchor) -> failed(route_no_candidate|route_anchor_unavailable)` semantics. Credential lookup has its own bounded `Task.WhenAny` deadline wrapper; a lookup timeout/failure writes one typed terminal after `started`, and a late synchronous store result is released by deferred cleanup.
- **Written terminal is committed:** a terminal frame that is fully written to the pipe is always promoted to `committed`, including `cancelled`, `failed`, `request.deadline_expired`, `credential_lookup_timeout` and `stream_budget_invalid`. `aborted` is used only when no terminal frame was successfully written (disconnect, write failure, pre-terminal disposal failure or process shutdown). Deferred Provider cleanup never changes a committed ledger. A duplicate of a committed timeout/error replays the complete prefix plus that terminal; a duplicate of an aborted stream is admitted again subject to the idempotency policy.
- **Complete host lock order:** rank 1 is `StreamLedger.Gate`; rank 2 is Host `sync`; rank 3 is `ConnectionContext.WriterGate`; rank 4 is `ConnectionContext.sync`. The only permitted nested order is low rank to high rank. Host never holds `sync` while waiting for `StreamLedger.Gate`, never holds `StreamLedger.Gate` while awaiting, and never holds either ledger lock while waiting for `WriterGate`. `WriteResponseAsync` may hold `WriterGate` while calling `NextResponseSequence` and `RecordWrite`, but `RecordWrite` is the only path that takes `ConnectionContext.sync` under that gate. No path takes `ConnectionContext.sync` and then `WriterGate`. Ledger map updates, eviction and active-task removal happen in one Host `sync` section after the stream gate is released; the client-side `requestGate` is not part of this process lock order.
- **Per-attempt usage across fallback:** usage snapshots are cumulative only within the current active Provider attempt. `route_changed` starts a new attempt baseline; Service keeps `attemptInputTokens`/`attemptOutputTokens` per active provider and adds each attempt's final known values to `totalKnownTokens` exactly once at terminal settlement. A provider's lower counter after a route change is valid; a lower counter within the same provider attempt is `usage_non_monotonic`. Text/output budget counts only forwarded text and structured JSON, so discarded pre-output Provider text does not consume the visible-output budget.
- **ABA-proof snapshot generations:** Registry owns one process-wide `mutationGeneration` that increments on every profile upsert and remove, including remove/recreate of the same key. A snapshot records that global value plus each entry's immutable `EntryGeneration`; second-phase verification rejects any global or entry mismatch. No generation counter resets after deletion, and one retry is allowed before `provider_profile_changed` terminal.
- **Replay capacity reservation:** admission reserves `reservationBytes = min(16 MiB, max(4096, effectiveOutputBytes + effectiveFrameCount * 2048))` under Host `sync`; if the global reserved-plus-committed total would exceed `16 MiB`, it returns `runtime.ledger_capacity` before Provider invocation. Each working ledger tracks actual template bytes; commit replaces its reservation with actual bytes, and abort releases it. TTL eviction, reservation adjustment, `messageLedger`, `taskLedger` and `providerIdempotency` updates occur atomically in the same Host `sync` section. The `64`-ledger limit is applied to committed entries; working entries are never evicted.
- **Shared canonicalizer:** `MarcusAwakeTransport/src/StructuredJsonCanonicalizer.cs` is the sole implementation used by Provider, Runtime Service and Framework. It emits compact JSON, preserves object property encounter order, emits strings with deterministic JSON escaping, and preserves each validated number token exactly (`1`, `1.0` and `1e0` are intentionally distinct lexical values). It rejects duplicate names, invalid UTF-8/control escapes, non-object roots, depth `>16`, properties `>256` and UTF-8 bytes `>65536`. Wire, Framework event and replay all reuse the one returned string; no assembly maintains a competing canonicalizer.
- **Final rejection matrix:** revision 8 tests must include pre-session credential timeout/failure after canonical start, timeout terminal replay, Host/ConnectionContext lock inversion probes, fallback usage reset and total settlement, remove/recreate ABA, admission reservation/capacity/eviction, and byte-for-byte shared canonicalizer vectors. These are the final A2 contract checks; no additional review dimension is opened unless implementation reveals a concrete contract contradiction.

- Revision 8 is the current approved implementation contract; authoritative status is recorded in the header.

## 1. 目标与当前闭环

把已经存在并通过独立测试的 Provider 流式解析能力，接通到 AWAKE 自有 Runtime Service、私有 IPC、Framework `AiTaskHandle` 和调用方可观察事件：

```text
Framework SubmitAsync
  -> provider.stream.v1 request
  -> authenticated private IPC
  -> Runtime Service ProviderRegistry/ProviderRouter
  -> bounded SSE/NDJSON Provider stream
  -> ordered stream-event response frames
  -> Framework AiTaskEvent sequence
  -> exactly one terminal result
```

本批只完成流式和 fallback 的运行时接线，不引入 MCM、AWAKE 游戏 Caller、DevTools、持久化 profile/route、真实云端或游戏实机。

## 2. 已确认事实

1. `MarcusAwakeProvider` 已有 `SSE/NDJSON` 有界读取、Provider adapter 和 `ProviderRouter.StreamAsync`；Provider focused tests 已覆盖顺序、EOF、畸形帧、取消和“可见输出后不得切换路由”。这部分不是本批重新实现对象。
2. `MarcusAwakeTransport` 已固定 `provider.stream.v1` 请求、`marcus-awake.provider.stream_event.v1` payload 和 `ProviderStreamStateMachine`；事件内 `stream_sequence` 与 envelope 方向 `Sequence` 是两条不同序列。
3. `RuntimeServiceHost` 当前只把 Provider `Complete` 作为单个 response 处理；`ProviderWireAdapter` 没有把流事件投影为 response payload，`ProviderRegistry` 没有跨反射边界的流枚举入口。
4. `RuntimeServiceClient.ClientConnection.SendBusinessAsync` 当前一次写入只读取一帧，并由 `requestGate` 串行化连接；取消使用独立控制连接，因此流期间可以安全发送取消。
5. `RuntimeServiceClient.SubmitAsync` 当前固定使用 `provider.complete.v1`，默认 capability 列表也尚未请求 `provider.stream.v1`；`AiTaskHandle` 已有 `TextDelta`、`UsageUpdate`、`RouteChanged` 和唯一 terminal 状态校验。
6. 当前 Provider idempotency ledger 只保存一个 `ResponseTemplate`，不能直接重放多帧流；若不扩展，重复请求会把单个 terminal 当成完整流，违反 Framework 事件状态机。
7. A1 evidence、A0 evidence、冻结 AWAKE 候选、`AWAKE.csproj`、`SubModule.xml`、`dist`、游戏目录和现有 worldbook evidence 不属于本批写集。

## 3. 选定设计

### 3.1 跨进程流事件与身份语义

- 补齐稳定响应类型 `provider_stream_event`：Transport 常量、响应识别、输出契约、Runtime Service 投影和 Framework 解析必须作为同一组变更完成；不能只新增 schema 名称。
- A2 明确区分两类身份和四条序列：`TaskScope.ProviderId` 是本次逻辑 route 的首选/锚定 Provider，不随 fallback 改写；payload `provider_id` 是当前活动 Provider；Provider 内部 `ProviderStreamEvent.Sequence` 只作为输入、不直接信任，归一化为 payload `stream_sequence`；IPC envelope `Sequence` 由 Service 写出端单独递增；`EventIndex` 等于已发出的 `stream_sequence`，只作审计索引；Framework `AiTaskEvent.Sequence` 从已有本地 `Started(2)` 后动态递增。被抑制的候选事件不产生任何序列号。
- 每个流响应复用原始请求的 envelope `MessageId`、`RequestId`、`CorrelationId`、session fence 和原始 `TaskScope`。`TaskScope` 内的 route anchor 不因 fallback 变化；Framework 允许流 payload 的活动 `provider_id` 与 `TaskScope.ProviderId` 不同，但必须通过 FSM 和 Service 侧候选校验。
- `provider_stream_event` payload 的闭集为：`schema`、`profile_id`、`route_id`、`provider_id`、`model_id`、`event_kind`、`stream_sequence`，以及按事件类型允许出现的 `text`、`structured_json`、`usage`、`error`、`from_provider_id`、`to_provider_id`；禁止透传供应商原始 JSON、异常文本、凭据或未定义字段。
- 事件字段矩阵固定如下：`started` 必须是 `stream_sequence=1`，只带基础身份字段且 `terminal=false`；`text_delta` 必须带非空 `text`，不得带 `structured_json/usage/error/route`；`usage_update` 只能带 `usage`；`route_changed` 只能带非空且不同的 `from_provider_id/to_provider_id`，并满足 `provider_id=from_provider_id`，不得带 `text/structured_json/usage/error`；`completed` 可带最终 `text`、`structured_json` 和 `usage`，不得带 `error/route` 且 `terminal=true`；`cancelled`/`failed` 必须带 typed `error`，不得带 `text/structured_json/usage/route` 且 `terminal=true`。`structured_json` 必须为 object，最大 64 KiB、深度 16、属性 256。
- Service 与 Framework 各自运行 `ProviderStreamStateMachine`。公开 Router 输出在任何实际 Provider 调用前先发一次 canonical `started`；后续 adapter 的 `started` 只作输入校验并抑制。`completed`、`cancelled`、`failed` 必须是唯一 terminal，terminal 字段互斥；`route_changed` 的 `provider_id == from_provider_id == 当前活动 Provider`，不带 error，且只能发生在可见文本前。

### 3.2 Service 侧 Provider 流与唯一路由路径

- 唯一调用路径固定为：`RuntimeServiceHost.HandleProviderBusinessAsync` → `ProviderRegistry.StreamAsync` → `ProviderRouteReflectionBridge.CreateRouter(candidates)` → 动态 Provider assembly 的 `ProviderRouter.StreamAsync` → `ProviderStreamReflectionBridge`（`GetAsyncEnumerator`/`MoveNextAsync`/`Current`/`DisposeAsync`）→ Host 的逐帧投影和写出。Service 不另写 fallback 循环，`ProviderRouter` 是唯一 fallback authority。
- A2 不引入持久化 route 配置。Registry 从当前 session 中找出相同 owner/campaign/timeline/session/profile/route 的内存 profile；必须先确认 `TaskScope.ProviderId` 存在于候选快照，否则只发 synthetic `started(anchor)` + `failed(route_anchor_unavailable)`，不尝试其他候选；空候选使用 `failed(route_no_candidate)`。确认锚点存在后，锚点置首，其余按稳定 `provider_id` ordinal 排序；候选快照在首次 canonical `started` 前冻结，之后的 profile 修改不影响本次请求。P3D-B 再增加持久化的 route 顺序，不在本批扩张。
- 反射桥必须显式处理 `GetAsyncEnumerator(CancellationToken)` 返回的 enumerator、`MoveNextAsync` 返回 `Task<bool>` 或 `ValueTask<bool>`、`Current` 为 `ProviderStreamEvent`（或可验证的兼容类型）、`DisposeAsync` 返回 `Task` 或 `ValueTask`，并处理 `TargetInvocationException` 解包、取消、deadline 和释放失败；未知签名映射为 `provider_stream_bridge_invalid`。原始异常只保留类型/稳定错误码，所有 wire/log 输出使用脱敏 `ProviderWireError`。
- Router 只在首个可见文本前且 `IsFallbackEligible(error, deadline)` 为真时切换。错误映射表在实现和测试中固定：`TransportUnavailable`、`ServerUnavailable`、`Unavailable`、仍在 deadline 内的 retryable `Timeout`/`RateLimited` 可切换；Authentication、Forbidden、PolicyDenied、InvalidRequest、NotFound、Conflict、MalformedResponse、IncompleteStream、Cancelled、Unsupported、ResourceExhausted、CorruptCredential、InternalFailure 不切换。未知 Provider stream error 按 `MalformedResponse + non-retryable` 处理，不得默认变成可 fallback。
- Router 的序列归一化发生在“决定发出事件”之后：canonical `started` 已发出后，adapter 的 `started` 全部抑制，不占序号；fallback 只发一个 `route_changed`，其 `provider_id == from_provider_id == 当前活动 Provider`、`to_provider_id == 下一候选` 且不带 error。多级 fallback `P→Q→R` 最多产生两次 route change，且每次只在下一候选真正开始尝试前产生。
- Service 只在 terminal 已完整写出后写入完整流 replay ledger；缓存 bounded stream event templates。流帧预算固定为 `MaxSnapshot=128`、`LocalLifecycle=2`、`SafetyReserve=1`，因此 `ProtocolConstants.MaxProviderStreamEvents = 128 - 2 - 1 = 125`；adapter 的 suppressed `started` 不产生事件、序号或预算消耗。`RuntimeResourceBudget.FrameCount`、`OutputBytes`、`Tokens`、`TextDeltas` 通过 stream request 的 `resource_budget` 对象传输，Framework 使用 `RuntimeService.MaximumProviderStreamEvents/MaximumOutputBytes/MaximumTokens/MaximumTextDeltas` 发布上限，Service 再与自身上限和 Provider `MaxStreamBytes/MaxDeltas` 取最小值：`frame_count=min(125, request, Framework, Service)`、`output_bytes=min(request, Framework, Service)`、`text_deltas=min(request, Framework, Service)`、`tokens=min(request, Framework, Service)`。累计 `text`/`structured_json` UTF-8 字节、delta 数和 usage token，在写出前检查。到达任一上限时在保留的最后槽位发唯一 `failed(resource_exhausted)`；不得先写第 125 个非 terminal 再追加失败。usage 只在 `usage_update`/terminal 处观察和结算，不扩大 frame budget。
- 每一帧写出都由 `WriterGate` 保护；同一 business connection 的 `requestGate` 持有到唯一 terminal。工作 ledger 状态为 `open → terminal_claimed → committed` 或 `aborted`：先在线性化锁内 claim terminal，再构造/写出帧；每个成功写出的 frame 才追加模板；terminal 写出失败立即 `aborted`、丢弃整个工作 ledger，保留 Framework 的断线 failure；只有 terminal 写成功后才 promote 到 replay ledger。Provider credential lease 与 enumerator 同生命周期，必须覆盖所有 `MoveNextAsync`、terminal 写出和 `DisposeAsync`，最后才释放。取消与 Provider terminal 竞争时按同一锁的先到者获胜；取消获胜即停止枚举、丢弃迟到事件，terminal 获胜则取消请求不再追加事件。Dispose 失败若已有 terminal 只记脱敏诊断并保留 terminal；若尚无 terminal 则按取消/原始 failure 优先级合成一个 typed terminal。

### 3.3 Framework 侧调用

- 默认 `SubmitAsync` 在握手协商到 `provider.stream.v1` 时请求 stream；未协商到 stream 但协商到 `provider.complete.v1` 时，所有请求（包括 `ResponseSchemaJson`）使用 unary；两种 capability 都缺失时返回 typed `stream_capability_unavailable`/`complete_capability_unavailable`，不发起 Provider 请求。走 stream 时，`completed.structured_json` 必须经过 Core object 限制验证并映射到 `AiTaskEvent.StructuredJson`，缺失必需结构化结果时返回 typed `structured_output_missing`，不得以纯文本成功替代。
- `ClientConnection` 增加专用多帧读取循环：写入一次请求后持续读取到唯一 terminal，逐帧执行完整 envelope 校验、响应类型/schema 校验、payload 闭集校验和 FSM 校验，再交给 Framework 映射；unary `SendBusinessAsync` 保持原语义。
- Service 的 `started` 只用于 Framework 侧 stream FSM，不重复映射为 Framework `Started`；本地生命周期仍为 `Accepted(1) -> Started(2)`。
- `text_delta`、`usage_update`、`route_changed` 和 terminal 均通过动态 `nextSequence` 发布。必须修改 `AiTaskHandle.cs`，新增线程安全的 `PublishTerminal(kind, ...)`/等价原子路径，并让 `RuntimeServiceClient.cs` 的取消和失败辅助调用该路径，移除固定序号 `3`；任何 `Publish` 失败都必须导致连接收口和一个可观察的 typed terminal，不能忽略返回值。
- Framework 只接受按有效预算取小后的 Provider 事件/字节/delta/token 上限；由 `128 - 2 - 1 = 125` 的公式与 Service 共用 frame budget，suppressed adapter `started` 不计入预算。读到超限、跳号、错误 Provider、terminal 后事件或 stream EOF 时收口为唯一 typed failure。对于已收到的 terminal 后事件，先由 FSM 拒绝并记录 `stream_terminal_already_emitted`，然后停止读取；不向 Handle 发布第二 terminal。取消/timeout/断线与远端 terminal 竞争时，以 `AiTaskHandle` 的线性化点为准，迟到事件不得产生第二 terminal。

### 3.4 幂等重放

- 新请求的 idempotency key 与 payload hash 不变时，Service 重放完整 bounded stream event 序列；只有重放序列的 terminal 帧使用 `outcome_kind=terminal_replay`，中间帧仍使用 `outcome_kind=accepted`；payload `stream_sequence` 从 `1` 开始，IPC envelope `Sequence` 按当前连接递增。新请求 deadline 已过期时拒绝 replay，不使用旧 deadline 继续执行。
- Replay 的 envelope 字段矩阵固定为：保留 `ProtocolId/ProtocolMajor/ProtocolMinor/MessageType/PayloadSchema/ChecksumAlgorithm`、外层 `OwnerId/CampaignGuid/TimelineId/SessionId/SessionGeneration/InstanceEpoch`、以及 `TaskScope.TaskId/TaskScope.OwnerId/TaskScope.RouteId/TaskScope.ProviderId/TaskScope.ProfileId/TaskScope.IdempotencyKey/TaskScope.RequestPayloadHash/TaskScope.OutputSchemaId/TaskScope.OutputSchemaMajor/TaskScope.OutputSchemaMinor/TaskScope.SettlementRequirement`；按新请求/当前连接重算 `MessageId/RequestId/CorrelationId/CausationId/DeadlineUnixMilliseconds/ConnectionEpoch/DirectionNonce/Sequence/FenceProof/PayloadJson/PayloadLength/PayloadSha256/Checksum`；成功中间帧使用现有 `AckStatus=accepted`、`OutcomeKind=accepted`、`NonDurable=true`，terminal replay 使用现有 `OutcomeKind=terminal_replay`、`NonDurable=true`，且无错误时 `AckStatus=accepted`、有错误时 `AckStatus` 为空；`ErrorCode` 保留模板 terminal 的稳定错误码或为空，`EventIndex=模板 ordinal`，`TaskScope.MessageId=新请求 MessageId`。Framework 不得要求旧请求 MessageId。
- 同一 idempotency key 使用不同 payload hash 返回 `provider.idempotency_conflict`；相同任务仍在执行返回 `provider.task_in_progress`。
- 只缓存已经完整写出的帧；写出中断或流取消时不把半截序列标记为可重放完成。

## 4. 明确不采用的方案

1. **在游戏进程直接 HTTP/SSE**：违反 Runtime Service 所有权和游戏 tick 非阻塞边界，拒绝。
2. **把所有 delta 聚合后只返回一帧**：无法继承 Marcus 的流式体验，也无法验证取消、背压和路由事件，拒绝。
3. **把每个流事件写成独立 Task/MessageId**：破坏原始 TaskScope、幂等和客户端单流读取，拒绝。
4. **可见文本后自动切换 Provider 并拼接**：会产生两个答案的不可解释混合，违反已锁定 route-change 规则，拒绝。
5. **只保留 terminal replay**：重复请求无法重建 `Started`/delta/FSM，拒绝。
6. **借机实现 profile/route 持久化或 MCM**：扩大批次并混淆 A2 与后续 P3D-B/P4 ownership，延期；A2 只使用当前 session 内存候选和确定性排序。

## 5. 入口、结算和可观察结果

| 场景 | 入口 | 结算 | 可观察结果 |
|---|---|---|---|---|
| 正常流 | `SubmitAsync` | Provider stream 完整 terminal | `Accepted(1) -> Started(2) -> delta/usage/route? -> Completed`，事件序列严格递增 |
| 首帧前可重试失败 | ProviderRouter | 下一候选成功 | 已有 canonical `Started(P)`；每次切换只出现一个 `RouteChanged(P→Q)`，后续不转发 Q 的 `Started`，不暴露旧答案 |
| 可见文本后失败 | ProviderRouter | 不再切候选 | 只出现一个 `Failed`，已有 delta 保留，绝不拼接第二答案 |
| 玩家取消 | `AiTaskHandle.CancelAsync` | 控制连接请求 + task cancellation | 仅一个 `Cancelled`，迟到 Provider 事件被丢弃 |
| deadline | RequestContext/Provider | deadline 取消 | 仅一个 typed timeout/failed terminal，不发送已过期新请求 |
| 重复请求 | idempotency key | 完整流序列重放 | 新 envelope identity + 重绑定 TaskScope.MessageId，字段矩阵一致，`terminal_replay` + 可重新消费的完整 stream FSM |
| 同 key 不同 payload | Service ledger | 冲突拒绝 | `provider.idempotency_conflict`，不触发 Provider |
| 超大/畸形事件 | Transport/Provider | 有界拒绝 | `resource_exhausted` 或 `malformed_response`，不泄露原始异常/密钥 |
| capability 缺失 | Framework handshake | unary fallback | 明确记录 `stream_capability_unavailable`，不把运行失败伪装成 stream 成功 |
| 无候选/锚点缺失 | Registry preflight | 不调用 Provider | `Started(anchor) -> Failed(route_no_candidate|route_anchor_unavailable)`，Provider HTTP=0 |

## 6. 写集与禁止触碰范围

### 允许修改

- `framework/MarcusAwakeProvider/src/ProviderRouter.cs`、`ProviderResponseSupport.cs`、`ProviderContracts.cs`、`ProviderAdapterBase.cs`、`ProviderAdapters.cs` 及必要的安全事件投影文件。
- `framework/MarcusAwakeProvider/tests/`：A2 Provider/router 回归用例。
- `framework/MarcusAwakeTransport/src/ProviderProtocolContract.cs`、`ProtocolModels.cs`、`ProtocolCodec.cs`、`ProtocolValidation.cs` 与必要的协议常量/模型文件。
- `framework/MarcusAwakeTransport/tests/`：stream response contract/FSM 回归用例。
- `framework/MarcusAwakeRuntimeService/src/ProviderWireAdapter.cs`、`ProviderRegistry.cs`、`RuntimeServiceHost.cs` 及流式 credential lease/预算辅助文件。
- `framework/MarcusAwakeRuntimeService/tests/`：真实 Service/IPC 流式 harness 与回归用例。
- `framework/MarcusAwakeFramework/src/RuntimeServiceClient.cs`、`AiTaskHandle.cs`、`ProviderRuntimeApi.cs`、`RuntimeServiceApi.cs`、`RuntimeService.cs` 与必要的 AI 事件解析辅助文件。
- `framework/MarcusAwakeFramework/tests/`：Framework stream handle/client 回归用例。
- 本计划、对应 review log、checkpoint 和新 evidence 文件。

### 禁止修改

- A1/A0 evidence、既有 checkpoint 内容和冻结候选。
- `AWAKE.csproj`、`SubModule.xml`、AWAKE gameplay caller、MCM、`dist`、游戏目录和发布包。
- Provider 真实云端地址、API Key、外部网络和本机 Worker 部署。

## 7. 验收与证据

### E1 编译/契约

- Transport、Provider、Runtime Service、Framework 及 tests Release build 均 `0 warnings / 0 errors`。
- `provider_stream_event` response identity、payload schema、字段闭集、route anchor/active provider 语义、四种序列和 `ProviderStreamStateMachine` 正反例通过。
- `AiTaskHandle` 动态 terminal、snapshot budget、replay TaskScope 重绑定和 Provider error fallback 分类均有固定回归用例。
- 结构化流完成事件、首帧 canonical start、锚点缺失合成失败、terminal claim/commit、资源预算取小和 replay envelope 字段矩阵均有固定回归用例。
- 没有旧协议、未版本化 Provider response 或 secret-like 字段进入新 wire contract。

### E2 离线运行

- 使用真实 Runtime Service 子进程、私有 Named Pipe 和本地 fake HTTP；不访问外部网络。
- 正常 SSE/NDJSON 流、route-change 字段矩阵、首帧 canonical start、无候选/锚点缺失、首帧前 fallback、后续候选 started 抑制、可见输出后禁止 fallback、非 retryable 错误不 fallback、取消、deadline、断线、EOF、畸形帧、结构化结果、帧/字节/delta/token 超限和重复重放全部有固定 case ID。
- Harness 必须输出逐帧 transcript：`provider stream_sequence`、`IPC Sequence`、`EventIndex`、`Framework event sequence`、active provider、terminal count；固定前缀为 `P3D-A2-S01` 至 `P3D-A2-S16`，并包含 `S13a/S13b` 与 `S16a/S16b/S16c/S16d` 变体，任一帧缺失或多 terminal 即失败。
- 证据明确写出 `external_network=false`、`real_cloud_provider=false`、`bannerlord_started=false`、`game_directory_synced=false`。
- A1 的 23/23、IPC、Storage/RAG 回归保持通过；不修改 A1 evidence 文件。

### E2 固定预期

下表中的 `P` 为首选 Provider，`Q` 为 fallback Provider；`W` 为 wire `stream_sequence`，`I` 为 IPC envelope `Sequence`，`E` 为 `EventIndex`，`F` 为 Framework 事件序列。每个 case 必须逐项断言事件种类、身份、序列连续性和 terminal 数量，而不是只记录日志。

| Case | 预期 Provider 事件（W/E） | 预期 IPC 序列 I | 预期 Framework 事件（F） | 关键断言 |
|---|---|---|---|
| `P3D-A2-S01` | `started(P,1/1) -> text_delta(P,2/2) -> completed(P,3/3)` | `101,102,103` | `Accepted(1) -> Started(2) -> TextDelta(3) -> Completed(4)` | I/W/E/F 均连续；terminal=1 |
| `P3D-A2-S02` | `started(P,1/1) -> text_delta(P,2/2) -> usage_update(P,3/3) -> completed(P,4/4)` | `101..104` | `1..5` | usage 不带 text/error；terminal=1 |
| `P3D-A2-S03` | `started(P,1/1) -> route_changed(P→Q,2/2) -> text_delta(Q,3/3) -> completed(Q,4/4)` | `101..104` | `1..5` | Q 的 adapter started 不转发、不占任何序号 |
| `P3D-A2-S04` | `started(P,1/1) -> failed(P,2/2,authentication)` | `101,102` | `1,2,3` | 只尝试 P；无 route_changed |
| `P3D-A2-S05` | `started(P,1/1) -> text_delta(P,2/2) -> failed(P,3/3,transport)` | `101..103` | `1..4` | 可见文本后不切 Q |
| `P3D-A2-S06` | `started(P,1/1) -> route_changed(P→Q,2/2) -> text_delta(Q,3/3) -> usage_update(Q,4/4) -> completed(Q,5/5)` | `101..105` | `1..6` | Q 的 adapter started 被抑制且无跳号 |
| `P3D-A2-S07` | `started(P,1/1) -> text_delta(P,2/2) -> cancelled(P,3/3)` | `101..103` | `1..4` | 只有一个 cancelled terminal |
| `P3D-A2-S08` | `started(P,1/1) -> failed(P,2/2,timeout)` | `101,102` | `1,2,3` | deadline 后不发送新候选请求 |
| `P3D-A2-S09` | `started(P,1/1) -> text_delta(P,2/2) -> failed(P,3/3,incomplete_stream)` | `101..103` | `1..4` | EOF 不可完成；不 fallback |
| `P3D-A2-S10` | `started(P,1/1) -> failed(P,2/2,malformed_response)` | `101,102` | `1,2,3` | 原始 JSON/异常不出 wire |
| `P3D-A2-S11` | `started(P,1/1) -> 123 个非 terminal 事件(W/E=2..124) -> failed(P,125/125,resource_exhausted)` | `101..225` | `1..126` | 第 125 个 Provider 槽为 terminal；suppressed started 不占槽位；I 连续；不丢中间帧 |
| `P3D-A2-S12` | `started(P,1/1) -> text_delta(P,2/2) -> completed(P,3/3)` | `201,202,203` | 新 TaskScope 下 `1..4` | 新 Message/Request/Correlation/Causation/Nonce/Fence/IPC 序列；TaskId、anchor、idempotency、EventIndex 保留 |
| `P3D-A2-S13a` | `started(P,1/1) -> failed(P,2/2,route_no_candidate)`；Provider HTTP=0 | `101,102` | `1,2,3` | 空候选 fail closed；不调用 Provider |
| `P3D-A2-S13b` | `started(P,1/1) -> failed(P,2/2,route_anchor_unavailable)`；Provider HTTP=0 | `101,102` | `1,2,3` | 锚点缺失 fail closed；不尝试 Q；同 key 不同 hash 另返回 idempotency conflict |
| `P3D-A2-S14` | 输入 `started -> completed -> text_delta`；Service 在转发前拒绝迟到事件 | `101,102` | `1,2,3` | completed 后只保留一个 terminal；迟到事件错误码=`stream_terminal_already_emitted`，无第二 terminal |
| `P3D-A2-S15` | `started(P,1/1) -> text_delta(P,2/2)` 已写出后 IPC 断线；工作 ledger=`aborted` | `101,102` | `Accepted(1) -> Started(2) -> TextDelta(3) -> Failed(4,disconnect)` | W/I/E 只到 2；Framework 只收口一次本地 typed disconnect failure；半截流不进入 replay ledger |
| `P3D-A2-S16a` | `started(P,1/1) -> failed(P,2/2,stream_route_change_invalid)`；非法 route_changed 不转发 | `101,102` | `1,2,3` | 单一协议失败 terminal；无 Q 请求 |
| `P3D-A2-S16b` | `started(P,1/1) -> failed(P,2/2,stream_sequence_invalid)`；输入 text_delta 跳到 3 | `101,102` | `1,2,3` | 跳号事件拒绝；无非法帧写出 |
| `P3D-A2-S16c` | `started(P,1/1) -> failed(P,2/2,stream_provider_mismatch)`；活动 Provider 不匹配 | `101,102` | `1,2,3` | Provider 身份拒绝；无第二 terminal |
| `P3D-A2-S16d` | `started(P,1/1) -> failed(P,2/2,structured_json_invalid)`；completed structured JSON 非 object/超限 | `101,102` | `1,2,3` | 结构化限制 fail closed；原始 JSON 不出 wire |

`I` 使用本 case 的固定 connection response-sequence 起点，仅用于确定性 transcript；实现不得把示例数字当成跨连接全局序号。`EventIndex` 与 `W` 相同，抑制事件不产生 `W/I/E/F`。

## 8. 未解决风险

1. 多个 Provider stream response 共用同一 envelope `MessageId` 只在 AWAKE 自有 Client/Service 内承诺；旧 Marcus 消费者不在兼容目标内。
2. A2 的内存候选排序不是最终 route 配置；P3D-B 必须把 route 顺序持久化后再替换临时规则。
3. Stream replay 是内存 bounded ledger，不是 P3D-B 的 durable receipt；进程崩溃后不宣称可恢复。
4. Framework 的自定义小 snapshot limit 不能扩大 Service 上限；客户端实现必须在本批使用完整默认预算，外部自定义 handle 仍由其自身 contract 负责。
5. 本批完成后仍不能宣称 AWAKE MCM、真实 AWAKE caller、真实云端 Provider、Bannerlord 或存档完成。

### 8.1 序列与身份分配表

| 字段 | 分配方 | 起点/规则 | replay/断线规则 |
|---|---|---|---|
| Provider `Sequence` | Provider adapter/Router 输入 | 外部值仅作输入；Router 只对实际发出的事件重新编号 | 不直接写入 wire；被抑制事件无编号 |
| payload `stream_sequence` | Router/Service 投影 | 从 `1` 连续递增，包含首个 started、route/delta/usage 和唯一 terminal | replay 从 `1` 重放；缺号或重复拒绝 |
| IPC envelope `Sequence` | 当前 Service connection | 使用现有 `NextResponseSequence()`，每帧按连接连续递增 | 新连接重新按该连接窗口分配；不从模板复用 |
| `EventIndex` | Service stream ledger | 等于已发出的 payload `stream_sequence`，只用于审计和 transcript | replay 保留原 ordinal；断线半流不生成完成记录 |
| Framework `AiTaskEvent.Sequence` | `AiTaskHandle` | 本地 Accepted=1、Started=2；每个非 suppressed Provider 事件取 `nextSequence` | replay/远端新请求从本地 1、2 重新消费；terminal 只能线性化一次 |
| `TaskScope.ProviderId` | Framework request | 逻辑 route 首选/锚定 Provider | 全流和 replay 不变 |
| payload `provider_id` | Router/Service | 当前活动 Provider；fallback 后可变 | 由 payload FSM 校验，不改变 TaskScope |

### 8.2 Replay envelope 字段矩阵

| 分类 | 字段 |
|---|---|
| 保留 | `ProtocolId`、`ProtocolMajor`、`ProtocolMinor`、`MessageType`、`PayloadSchema`、`ChecksumAlgorithm`、外层 `OwnerId/CampaignGuid/TimelineId/SessionId/SessionGeneration/InstanceEpoch`、`TaskScope.TaskId/OwnerId/RouteId/ProviderId/ProfileId/IdempotencyKey/RequestPayloadHash/OutputSchemaId/OutputSchemaMajor/OutputSchemaMinor/SettlementRequirement`；`ServiceId` 仅由握手身份保留，不是业务 response envelope 字段 |
| 重算 | `MessageId`、`RequestId`、`CorrelationId`、`CausationId`、`DeadlineUnixMilliseconds`、`ConnectionEpoch`、`DirectionNonce`、`Sequence`、`FenceProof`、`PayloadJson`、`PayloadLength`、`PayloadSha256`、`Checksum` |
| 强制覆盖 | 成功中间帧使用现有 `AckStatus=accepted`、`OutcomeKind=accepted`、`NonDurable=true`；terminal replay 使用现有 `OutcomeKind=terminal_replay`、`NonDurable=true`，无错误时 `AckStatus=accepted`、有错误时为空；`ErrorCode=模板 terminal 稳定错误码或空`；`EventIndex=模板 ordinal`；`TaskScope.MessageId=新请求 MessageId` |

## 9. 批次退出条件

- 计划当前 revision 得到独立只读 `VERDICT: APPROVED`。
- 所有写集文件通过 focused tests 和真实 Service/IPC offline harness。
- 任一 terminal race、协议错误、流序列错误或秘密泄露测试失败，批次不得收口。
- checkpoint 登记变更文件、命令、证据等级、未验证项和下一批 P3D-B 边界。
