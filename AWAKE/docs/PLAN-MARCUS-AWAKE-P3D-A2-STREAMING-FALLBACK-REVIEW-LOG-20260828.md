# Marcus-Awake P3D-A2 流式 Provider 与 fallback 审查日志

- `batch_id`: `MARCUS-AWAKE-P3D-A2-STREAMING-FALLBACK-20260828`
- `plan_revision`: `6`
- `review_status`: `pending_independent_read_only_review`
- `implementation_authority`: `closed_until_exact_verdict_approved`
- `review_scope`: 仅审查 A2 计划、当前 Transport/Provider/RuntimeService/Framework 源码和项目规则；不得修改文件、启动 Bannerlord、访问真实 Provider 或同步游戏目录。

## Act 1 — 决策框架

### 已确认事实

- Provider 库已有有界 SSE/NDJSON、ProviderRouter fallback 和本地测试，但它尚未跨 Runtime Service/IPC/Framework 形成流式闭环。
- Transport 已有 `provider.stream.v1`、stream event schema 和 FSM；当前 Service/Client 仍按一请求一响应处理。
- AWAKE 的最终目标仍是全量内置迁移；本批只是 P3D 的一个垂直切片，不缩小最终目标。

### 用户/项目约束

- 运行时只保留 AWAKE 自有协议和实现，不恢复外部 Marcus 兼容运行时。
- AI/Provider 在后台服务，游戏侧只接收有界 typed result；游戏 tick 不做阻塞 I/O。
- 先做离线、可重复、可审计的闭环；不把 fake HTTP、离线 smoke 或编译结果包装成云端/游戏证据。

### 本批选择

- 复用已有 Provider stream parser 和 Router，不复制第二套供应商解析器。
- 通过多帧 `provider_stream_event` response 完成流式传输；同一 TaskScope 共用请求身份，envelope 与 stream 各自维护序列。
- `SubmitAsync` 优先 stream，能力未协商时才 unary fallback；Provider 运行失败不静默改变语义。
- 用 bounded stream templates 支持完整序列 replay，半截流不进入 terminal replay。

### 明确延期

- P3D-B 的 profile/route durable persistence、credential provisioning、重启恢复和 durable Provider receipt。
- P4 MCM、P5 DevTools/SDK、P6 AWAKE caller、P7 游戏/存档/同步。

## Act 2 — 独立审查记录

### Review 1

- `reviewer`: `01a048f0-cb73-7df3-bff7-98960c300f3e`
- `verdict`: `REVISE`
- `evidence`: read-only review of A2 plan and current Transport/Provider/RuntimeService/Framework sources; no files changed, no game or cloud access.
- `P0`: route-change payload contradicted the Transport FSM; `provider_stream_event` was declared but not wired through response identity, wire operation, host dispatch, registry and multi-frame client reading.
- `P1`: cancellation/failure used fixed Framework sequence `3`; Provider and Framework stream budgets were not aligned; replay MessageId/TaskScope rebinding was unspecified; `TaskScope.ProviderId` versus active Provider semantics were unspecified; reflected async enumeration lifecycle was underspecified; stream error retryability was too broad.
- `P2`: terminal field exclusivity and fixed E2 transcript/case coverage were underspecified.
- `infrastructure_note`: two additional review submissions later encountered local stable-proxy `502/503`; this does not constitute a verdict.

### Revision 2 correction set

- Clarifies route anchor versus active Provider and separates Provider, IPC, audit, and Framework sequences.
- Makes `provider_stream_event` a complete cross-layer contract, including response identity and bounded multi-frame reading.
- Defines legal `route_changed` fields, suppresses later candidate `started`, and narrows fallback to typed retryable categories.
- Adds an explicit reflected async-enumerator adapter lifecycle and redacted exception mapping.
- Aligns bounded stream frames with Framework snapshot budget and replaces fixed terminal sequence numbers with a linearized dynamic terminal path.
- Defines replay rebinding for a new envelope identity while preserving idempotency/task payload identity.
- Adds fixed `P3D-A2-S01`–`P3D-A2-S16` frame-transcript acceptance cases.

### Review 2

- `reviewer`: `01a048f0-cb73-7df3-bff7-98960c300f3e`
- `verdict`: `REVISE`
- `P0`: the Registry-to-Router single invocation path was not locked; the plan still allowed two conflicting service-side descriptions.
- `P1`: `AiTaskHandle.cs` was missing from the write set; the budget had no numeric formula; replay did not specify CorrelationId/CausationId/Sequence/FenceProof/EventIndex; suppressed candidate started events could create sequence gaps; `FallbackEligible` had no concrete implementation/mapping; fixed transcript IDs had no per-case expectations.
- `P2`: duplicate old Service section remained and the sequence taxonomy was imprecise.

### Revision 3 correction set

- Locks `HandleProviderBusinessAsync → ProviderRegistry.StreamAsync → ProviderRouteReflectionBridge → ProviderRouter.StreamAsync → ProviderStreamReflectionBridge` as the only route path.
- Adds an explicit `128 - 2 - 1 - 1 = 124` bounded stream budget and includes `AiTaskHandle.cs`/all necessary source files in the write set.
- Defines replay identity regeneration, EventIndex allocation, post-decision sequence normalization, concrete fallback categories, and a 16-case expected transcript table.
- Removes the duplicate Service section and separates two identity dimensions from four sequence dimensions.

## 终审结论

- `VERDICT: PENDING_REVISION_REVIEW`
- `implementation_authorized`: `false`
- `next_action`: 对 revision 3 重新执行独立只读审查；未获得 `VERDICT: APPROVED` 前不得修改 A2 代码。

### Review 3

- `reviewer`: `01a048f0-cb73-7df3-bff7-98960c300f3e`
- `verdict`: `REVISE`
- `evidence`: independent read-only review of revision 3 and current Provider/Transport/RuntimeService/Framework sources; no files changed, no game or cloud access.
- `P1`: 首帧失败仍可能绕过 `started`；结构化输出在 stream completed 中没有 wire 字段；Host 的逐帧 ledger、terminal claim/commit 和 replay promotion 提交点不够具体；S01–S16 仍有省略序列或语义矛盾；replay envelope 字段矩阵不完整；124 帧预算未覆盖 output bytes、text deltas 和 tokens。
- `P2`: 每种事件的 required/forbidden 字段矩阵不完整；反射 async-enumerator 的 Task/ValueTask 签名与 Dispose 失败语义未锁定；锚点缺失、多级 fallback、候选快照冻结和并发 profile 修改规则不完整。

### Revision 4 correction set

- 增加 canonical pre-attempt `started`，并规定 adapter `started` 抑制、空候选/锚点缺失的 synthetic `started -> failed` 语义。
- 将 `structured_json` 纳入 `completed` wire 字段并保留 Core 限制和 Framework 映射。
- 锁定工作 ledger、terminal 线性化、逐帧写成功后追加、完整 terminal 后 replay promotion，以及取消/断线/Dispose 失败优先级。
- 锁定 frame/output-byte/text-delta/token 取小预算和最后槽位 terminal 规则。
- 增加事件字段矩阵、反射签名、候选快照/锚点/多级 fallback 规则、完整 replay envelope preserve/recompute/override 矩阵和逐 case 的 W/I/E/F transcript。

### Revision 4 gate

- `plan_revision`: `4`
- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false`
- `next_action`: 对 revision 4 重新执行独立只读审查；未获得 `VERDICT: APPROVED` 前不得修改 A2 代码。

### Revision 4 internal contract correction before final review

- Existing Transport validation was rechecked locally before implementation: only `AckStatus=accepted|durably_recorded` and the existing outcome values are legal; `stream_event` is not a new outcome value. The plan now uses `AckStatus=accepted`/`OutcomeKind=accepted` for successful stream frames and `OutcomeKind=terminal_replay` only for replay.
- `ServiceId` was removed from the business response envelope preserve list and documented as handshake identity only; it is not a `PipeEnvelope` field.
- The implementation gate remains closed; this correction does not constitute an approval verdict.

### Review 4

- `reviewer`: `01a048f0-cb73-7df3-bff7-98960c300f3e`
- `verdict`: `REVISE`
- `evidence`: independent read-only review of current revision 4 and Provider/Transport/RuntimeService/Framework sources; no files changed, no game or cloud access.
- `P1`: structured-output fields were not in the explicit write set; frame budget had no actual request/Framework/Service carrier; replay matrix omitted deadline/payload schema/checksum/owner/schema-version/settlement fields and had non-unique failure semantics; stream credential lease ended before async enumeration completed.
- `P2`: suppressed-start budget subtraction was ambiguous; S13/S15/S16 did not provide fixed variants and full W/I/E/F values; stream-capability fallback conflicted with structured requests.

### Revision 5 correction set

- Explicitly adds Provider event/model/parser files, Transport codec/validation, Framework budget/API files, Service budget/lease files and all corresponding tests to the write set.
- Adds `RuntimeResourceBudget.FrameCount`, a `resource_budget` stream payload object and Framework/Service cap publication; changes the formula to `128 - 2 - 1 = 125` and excludes suppressed adapter starts from budget consumption.
- Extends `ProviderStreamEvent` and completed wire projection with Core-validated `structured_json`; fixes stream/unary capability fallback for structured requests.
- Keeps credential lease alive through enumerator construction, all `MoveNextAsync`, terminal write and `DisposeAsync`.
- Completes the replay field matrix and expands fixed transcript variants (`S13a/S13b`, `S16a-S16d`) with disconnect W/I/E/F values.

### Revision 5 gate

- `plan_revision`: `5`
- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false`
- `next_action`: 对 revision 5 重新执行独立只读审查；未获得 `VERDICT: APPROVED` 前不得修改 A2 代码。

### Review 5

- `reviewer`: `01a049a6-b51f-7a72-9f38-c9b3c1c730ff`
- `verdict`: `REVISE`
- `evidence`: independent read-only review of revision 5 and current Provider/Transport/RuntimeService/Framework sources; no files changed, no game or cloud access.
- `P0`: terminal text/usage semantics, credential lease ownership across async enumeration, and replay ledger atomicity/lock interaction were not executable enough to prevent duplicate text, premature secret release or partial replay promotion.
- `P1`: capability and frame-count carrier/defaults, structured JSON canonicalization and duplicate-key rules, deadline/fallback clock behavior, candidate snapshot/anchor atomicity, and rejection-case coverage were incomplete.
- `infrastructure_note`: the prior Fermat review instance failed with stable-proxy `502/503`; that failure is not a verdict. This review returned a real read-only `REVISE` verdict.

### Revision 6 correction set

- Locks incremental text versus terminal aggregate semantics: Router normalizes an aggregate-only completion into one delta, suppresses duplicate terminal text, and uses cumulative monotonic usage snapshots.
- Defines `ProviderStreamSession` ownership and the Host-only disposal boundary through enumerator construction, all reads, terminal write and disposal.
- Defines the working/committed/aborted stream ledger, exact `sync`/`WriterGate` lock order, duplicate/conflict behavior and terminal promotion point.
- Defines `FrameCount` lower/upper bounds, request/Framework/Service minimum calculation, default stream capability request and unary local projection fallback.
- Defines canonical structured JSON string rules, duplicate-key rejection, deadline checks, no-delay `RetryAfter` handling, candidate snapshot ordering and fail-closed anchor behavior.
- Adds independent malformed-field, budget, usage, lease and replay race acceptance cases.

### Revision 6 gate

- `plan_revision`: `6`
- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false`
- `next_action`: 对 revision 6 只审查新增 correction set 与其对应现有源码；若无新的实质缺陷，返回 exact `VERDICT: APPROVED` 后立即建立实现写入租约。

### Review 6

- `reviewer`: `01a049a6-b51f-7a72-9f38-c9b3c1c730ff`
- `verdict`: `REVISE`
- `evidence`: independent read-only review of revision 6 and current Provider/Transport/RuntimeService/Framework sources; no files changed, no game or cloud access.
- `P0`: the earlier field matrix still allowed `completed.text`; unary fallback did not explicitly use the same no-duplicate projection; `ProviderStreamSession` was not wired into the authoritative call chain; deadline behavior for providers that ignore cancellation was unbounded; the ledger lock order and terminal-claim rollback were incomplete.
- `P1`: budget definitions conflicted, Service capability/budget publication was unspecified, structured JSON canonicalization was incomplete, registration ordinal had no source, and committed replay retention had no bound.

### Revision 7 correction set

- Adds one authoritative execution section that overrides conflicting earlier wording.
- Closes terminal text/usage semantics and applies the same projection to unary fallback.
- Makes `ProviderStreamSession`, two-phase generation-checked snapshots and Host-only disposal part of the concrete call chain.
- Defines bounded async waits with deferred cleanup, complete lock ranking, terminal claim/commit/abort behavior and replay retention limits.
- Defines one budget meaning, handshake publication fields, canonical JSON output, deadline checks, registration ordinal and the fixed negative-test matrix.

### Revision 7 gate

- `plan_revision`: `7`
- `review_status`: `pending_independent_read_only_review`
- `implementation_authorized`: `false`
- `next_action`: 对 revision 7 的 authoritative execution contract 做最后一次只读审查；不得再扩大范围。仅在 exact `VERDICT: APPROVED` 后修改 A2 生产代码。

### Review 7

- `reviewer`: `01a049a6-b51f-7a72-9f38-c9b3c1c730ff`
- `verdict`: `REVISE`
- `evidence`: independent read-only review of revision 7 and current Provider/Transport/RuntimeService/Framework sources; no files changed, no game or cloud access.
- `P0`: canonical `started` was still after credential/generation work; timeout terminal commit versus abort semantics conflicted; `ConnectionContext.sync` was absent from the lock order; fallback usage across Provider attempts was ambiguous.
- `P1`: generation ABA, replay reservation/eviction synchronization and shared structured-JSON canonicalization were not fully specified.

### Revision 8 correction set

- Moves metadata-only admission and the canonical `started` write before credential lookup or adapter construction, with a bounded credential lookup failure path.
- Defines every successfully written terminal, including timeout/error terminals, as committed; only an unwritten terminal path aborts.
- Adds `ConnectionContext.sync` to the explicit rank order and forbids the reverse acquisition path.
- Defines per-attempt usage reset and one-time total settlement across fallback attempts.
- Adds process-wide non-resetting mutation generation, exact replay reservation formula/atomic eviction, and one shared Transport canonicalizer.
- Declares revision 8 the final contract review scope; implementation may begin only after an exact approval verdict.

### Revision 8 gate

- `plan_revision`: `8`
- `review_status`: `approved_independent_read_only_review`
- `implementation_authorized`: `true`
- `verdict`: `VERDICT: APPROVED`
- `reviewer`: `01a049d9-1294-7540-9d65-87097bc92476`
- `evidence`: final independent read-only review of revision 8; no files changed, no game or cloud access.
- `next_action`: enter A2 implementation under the approved write set; keep the contract and evidence ceiling unchanged.
