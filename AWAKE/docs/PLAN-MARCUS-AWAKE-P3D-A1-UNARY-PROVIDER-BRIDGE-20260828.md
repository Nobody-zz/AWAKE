# Plan: Marcus-Awake P3D-A1 Unary Provider Bridge

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3D-A1-UNARY-PROVIDER-BRIDGE-20260828`
- `plan_revision`: `7`
- `plan_status`: `approved_for_implementation`
- `review_status`: `completed_approved`
- `user_signoff_required`: `satisfied_by_explicit_autonomous_full_migration_authorization_2026-08-26`
- `primary_executor`: `controller`
- `minimum_evidence`: `P3D-A1-E1 + P3D-A1-E2`
- `evidence_ceiling`: offline `E2`; no real cloud Provider or Bannerlord claim

## 1. Goal and closed loop

Implement the first real unary Provider path without changing the frozen AWAKE candidate, game directory, or A0 evidence:

```text
Framework request/profile setup
  -> authenticated private IPC
  -> Runtime Service profile registry and credential lookup
  -> existing Marcus-Awake Provider adapter
  -> local fake HTTP endpoint in tests
  -> bounded Provider result/error
  -> typed Framework result / AiTaskHandle events
```

The batch must make these operations real inside the Runtime Service:

- `provider.profile_upsert.v1`
- `provider.profile_remove.v1`
- `provider.models.v1`
- `provider.complete.v1`

The batch must not implement streaming or route fallback; those remain A2 boundaries.

## 2. Confirmed facts

1. `MarcusAwakeProvider` already contains bounded OpenAI-compatible, Anthropic and Ollama adapters, endpoint-origin policy, request/response parsing, cancellation/deadline classification, and secret-safe Provider errors.
2. `MarcusAwakeProvider` already contains `InMemoryCredentialStore`, `ProtectedFileCredentialStore`, DPAPI on Windows, and degraded AES-GCM fallback; no new secret storage algorithm is needed here.
3. `RuntimeServiceHost` currently validates Provider frames and returns `provider_handler_deferred`; it does not instantiate Provider adapters, read credentials, access HTTP, or persist profiles.
4. `RuntimeServiceClient` currently advertises only health/echo/cancel/diagnostic capabilities and returns `runtime.ai_business_unavailable` from `SubmitAsync`; the A1 client path must be explicit and bounded.
5. A0 fixed the Provider wire IDs, task-scope identity, deadline precedence, error category mapping, redaction rules and response schema IDs. A1 must not revise those contracts in place.
6. `AWAKE.csproj` already references the embedded Framework project, but `SubModule.xml` still declares external `MarcusAIFramework`; dependency removal is a later migration boundary, not an A1 side effect.

## 3. Grill decision ledger

### Q1. Should A1 perform real HTTP?

**Decision:** Yes, but only against a local deterministic fake HTTP server in the offline harness. Production code receives no real cloud evidence from this batch. The fake server must observe request method/path/headers/body shape without recording the secret.

### Q2. Does the API key cross IPC?

**Decision:** No. IPC carries only `credential_reference`; Runtime Service reads the protected credential store. Raw key text must not enter payloads, logs, evidence, command-line arguments, or Framework public DTOs.

### Q3. Is profile durability part of A1?

**Decision:** No. A1 uses an owner/session-scoped in-memory profile registry. Protected credential files may already exist and are read-only from the Service path. Profile/route durability, provisioning, restart recovery and receipts belong to `P3D-B`.

### Q4. What is the registry scope?

**Decision:** Key profiles by `owner_id + campaign_guid + timeline_id + session_id + profile_id + provider_id + route_id`. A request from another owner, campaign, timeline, session or route must not reuse the profile. Re-upsert is required after a new session.

### Q5. Which Provider kinds are enabled?

**Decision:** Enable existing `OpenAiCompatible`, `Anthropic` and `Ollama` adapters through one Service-owned factory. A1 covers unary model discovery and completion; no stream/fallback/embedding/media/GGUF behavior.

### Q6. Where does validation live?

**Decision:** Transport admission and fixed payload shape remain in `RuntimeServiceHost`; semantic profile construction and Provider operation validation remain in `MarcusAwakeProvider`; Framework only maps bounded wire results to typed API results. No duplicate authority path is introduced.

### Q7. What does a successful profile operation return?

**Decision:** `provider.profile_result.v1` returns only `schema`, `operation`, `profile_id`, `provider_id`, `route_id`, and `status`. It never returns the API key or raw credential contents.

### Q8. What does a successful models operation return?

**Decision:** `provider.models_result.v1` returns `schema`, scope IDs, a bounded ordered `models` array (`id`, `display_name`), and a bounded capability-state projection. It does not persist the model list in A1.

### Q9. What does a successful completion return?

**Decision:** `provider.result.v1` returns `schema`, scope IDs, `model_id`, `content`, optional bounded `structured_json`, and optional bounded `usage`. It is an accepted non-durable response; durable Provider execution receipts are deferred to `P3D-B`.

### Q10. How are cancellation and deadlines handled?

**Decision:** The Service links the request deadline and connection/lifecycle cancellation into the existing adapter call. A deadline at or before the Service's effective current time is rejected before HTTP. Cancellation/deadline maps to the fixed Provider error category and never blocks the game thread.

### Q11. What is the minimum observable evidence?

**Decision:** A real Runtime Service child process and private Named Pipe must process profile upsert, models, and complete frames; a local fake HTTP endpoint must receive exactly the expected calls; the harness must verify typed success, error mapping, no key leakage, deadline/cancel behavior, duplicate/idempotent frame replay and child shutdown.

## 3A. Revision 2 correction ledger

The first independent challenge identified three execution gaps. Revision 2 closes them before implementation:

1. **Framework entry is mandatory, not optional.** `RuntimeServiceClient.SupportsAiBusinessFrames` must become true only after the negotiated capability set contains the requested Provider capability. `SubmitAsync` must encode a real `provider.complete.v1` frame, send it through the existing authenticated pipe path, decode the typed response, and publish the existing `AiTaskHandle` event sequence. `ProviderRuntimeApi.cs` is therefore a required file, not an “if required” placeholder. Profile, model-list and connection operations use the same typed client path.
2. **The Service owns credential storage and Provider construction.** `MarcusAwakeRuntimeService.csproj` must reference the existing `MarcusAwakeProvider` project output. `RuntimeServiceHost` initializes exactly one Service-owned `ProtectedFileCredentialStore` after bootstrap and disposes it on every shutdown path. Production credentials resolve under the Service-owned local application data root; test mode may inject a fixed temporary root through a test-only environment variable. IPC, Framework DTOs, process arguments, logs and evidence carry only `credential_reference`.
3. **Local non-cloud profiles must be valid by contract.** The existing `ProviderConnectionProfile` constructor must validate the effective `is_cloud` value rather than assuming every non-Ollama profile is cloud-backed. OpenAI-compatible and Anthropic profiles may be local when `is_cloud=false`; a credential reference is then optional, and the adapter receives a null credential. Cloud profiles still require a credential reference. This is a narrow contract correction, not a second Provider implementation.
4. **Cancellation and idempotency are executable.** The harness must cover cancellation before Provider admission, during HTTP, after HTTP completion but before IPC write, and during response write. Same idempotency key plus the same canonical payload replays the existing result or returns `task_in_progress`; the same key with a different payload returns a fixed conflict and never calls Provider. If Provider completed but the IPC write failed, an in-process retry must reuse the captured result and must not call Provider again.
5. **Evidence must bind to the real child process.** A1 evidence records the runner command, Service executable/arguments/PID, private pipe name, fake HTTP endpoint and request count. The per-case schema requires the actual process and capture paths rather than accepting direct in-process frame construction as proof.

## 3B. Revision 3 exact executable contract

### Framework public surface

`ProviderRuntimeApi.cs` must expose one typed port, without Provider DTOs, credentials or HTTP handles leaking into unrelated callers:

- `IProviderRuntimePort.UpsertProfileAsync(ProviderProfileRequest, RequestContext, CancellationToken)`
- `IProviderRuntimePort.RemoveProfileAsync(ProviderScopeRequest, RequestContext, CancellationToken)`
- `IProviderRuntimePort.ListModelsAsync(ProviderScopeRequest, RequestContext, CancellationToken)`

`ProviderProfileRequest` contains exactly `profile_id`, `provider_id`, `route_id`, `provider_kind`, `base_url`, `default_model`, nullable `credential_reference` and `is_cloud`. `ProviderScopeRequest` contains exactly the three scope IDs. Results contain only the bounded public projections already shown in this plan. No API key, credential object, `HttpMessageInvoker`, database handle or service path is public.

`RuntimeServiceClient` must implement the port and `IAiGateway` over the same authenticated `ClientConnection.SendBusinessAsync` path. `RuntimeServiceClientOptions` must request the three A1 Provider capabilities in addition to health/echo/cancel/diagnostic; `SupportsAiBusinessFrames` is true only after negotiation confirms the specific requested capability. A missing capability returns a typed unavailable result before payload construction.

### Completion input and event mapping

For A1, `AiTaskRequest.InputJson` is required to be a JSON object with only `model`, `messages`, `max_output_tokens`, `temperature` and `response_schema_json`, using the exact bounds already enforced by `RuntimeServiceHost`. The client wraps this object with the task-scope IDs and the fixed `marcus-awake.provider.complete.v1` schema; it never copies arbitrary caller properties into the Provider payload.

Successful completion maps to one handle sequence: `accepted(1) -> started(2) -> text_delta(3, when content is non-empty) -> usage_update(next, when usage exists) -> completed(last)`. A Provider error maps to `accepted -> started -> failed`; cancellation maps to `accepted -> started -> cancelled`. The response parser rejects missing/unknown fields, invalid schema IDs, invalid usage bounds, invalid structured JSON and mismatched scope IDs. The client must not report `completed` after a cancellation or Provider error.

### Service ownership and profile policy

`ProviderRegistry` has the fixed internal constructor `(IProviderCredentialStore credentialStore, HttpMessageInvoker invoker, IProviderEndpointPolicy endpointPolicy, ProviderLimits limits, IProviderClock clock)` and fixed operations `Upsert`, `Remove`, `ListModelsAsync` and `CompleteAsync`; it owns scoped profile metadata and the adapter factory but borrows the injected store, invoker, policy, limits and clock. It does not retain raw credentials in profile entries. Each operation loads a credential by reference for the duration of the adapter call, disposes it in a `finally` block and never serializes it. `RuntimeServiceHost` owns the store and invoker, constructs the registry after bootstrap, and disposes registry, invoker and store in that order on normal exit, parent exit, startup failure and drain timeout.

The explicit `is_cloud` flag is validated before credential validation. If `is_cloud=true`, every Provider kind requires a credential reference. If `is_cloud=false`, the credential reference is optional and the base URL host must be loopback (`localhost`, `127.0.0.1` or `::1`), with no userinfo, query or fragment. Cloud URLs retain the existing HTTP(S) and exact-origin policy. The Ollama local default is a UI convenience only; the UI serializes `is_cloud=false`, and omission is rejected by the Service.

### Deterministic request state machine

The Service maintains an idempotency record keyed by the complete owner/session/profile/provider/route/idempotency scope. Each record stores the canonical request hash, lifecycle state (`admitted`, `provider_in_flight`, `result_captured`, `response_published`, `cancelled`, `failed`) and the bounded response template when available. The fixed precedence is: malformed/unauthorized frame, then caller/lifecycle cancellation, then deadline expiry, then idempotency conflict, then Provider execution.

- Cancellation before Provider admission: terminal cancelled result, zero HTTP calls.
- Cancellation during HTTP: linked token stops the adapter; terminal cancelled result, no late completion.
- Provider completion before IPC write failure: capture the bounded response before writing; a replay returns that template and never invokes Provider again.
- Same idempotency scope and same canonical hash: replay the captured template or return `task_in_progress` while in flight.
- Same idempotency scope and different canonical hash: fixed conflict error; Provider call count does not increase.

### Evidence contract

The A1 evidence schema must be added as `docs/schemas/marcus-awake-p3d-a1-evidence.v1.schema.json` and validated by `tools/verify_marcus_awake_p3d_a1.ps1`. Every case record must bind `runner_executable`, `runner_arguments`, `service_executable`, `service_arguments`, `service_pid`, `private_pipe_name`, `fake_http_bind`, `fake_http_capture_path` and `provider_http_request_count`. Direct in-process frame construction is an auxiliary unit test only and cannot satisfy an A1 case. The verifier must recompute the case IDs, artifact hashes, redaction scan and the explicit false flags for external network, real cloud, Bannerlord start and game-directory synchronization.

## 3C. Revision 4 locked types, cancellation channel and evidence fields

### Exact Framework types

`ProviderRuntimeApi.cs` must define these public types and no Provider-assembly references:

```text
ProviderProfileRequest
  string ProfileId
  string ProviderId
  string RouteId
  string ProviderKind              // openai_compatible | anthropic | ollama
  string BaseUrl
  string DefaultModel
  string CredentialReference      // nullable/empty only for local profiles
  bool IsCloud

ProviderScopeRequest
  string ProfileId
  string ProviderId
  string RouteId

ProviderProfileResult
  string Operation                 // upsert | remove
  string ProfileId
  string ProviderId
  string RouteId
  string Status                    // ready | removed

ProviderModelInfo
  string Id
  string DisplayName

ProviderModelsResult
  string ProfileId
  string ProviderId
  string RouteId
  IReadOnlyList<ProviderModelInfo> Models
  IReadOnlyDictionary<string,string> Capabilities
```

`IProviderRuntimePort` has exactly these signatures:

```text
Task<OperationResult<ProviderProfileResult>> UpsertProfileAsync(ProviderProfileRequest request, RequestContext context, CancellationToken cancellationToken)
Task<OperationResult<ProviderProfileResult>> RemoveProfileAsync(ProviderScopeRequest request, RequestContext context, CancellationToken cancellationToken)
Task<OperationResult<ProviderModelsResult>> ListModelsAsync(ProviderScopeRequest request, RequestContext context, CancellationToken cancellationToken)
```

`IAiGateway.SubmitAsync` remains the sole completion entry. The client constructs the fixed Provider payload from `AiTaskRequest` and does not expose a second completion DTO. `RuntimeServiceClient` implements `IProviderRuntimePort`, `IRuntimeServicePort` and `IAiGateway`, and delegates all three surfaces to the same `ClientConnection` transport. A1 accepts only `AiTaskRequest.OutputSchema` equal to `marcus-awake.provider.result.v1` version `1.0`; any other output schema fails locally with `runtime.output_schema_mismatch` and sends no frame. `InputJson` is parsed once into the exact five-field input object; the client copies only `model`, `messages`, `max_output_tokens`, `temperature` and `response_schema_json` with their existing bounds.

### Exact business-frame mapping

`ClientConnection.SendBusinessAsync` accepts one fully formed `PipeEnvelope`, serializes it with `ProtocolCodec`, writes one request, reads the matching response, validates protocol/session/owner/epoch/nonce/fence/sequence, and returns the response envelope. It is the only business-frame read/write path. The request payloads are:

- profile upsert: exactly the A0-locked profile fields;
- profile remove/models: exactly `schema`, `profile_id`, `provider_id`, `route_id`;
- completion: exactly `schema`, `profile_id`, `provider_id`, `route_id`, `messages` plus optional `model`, `max_output_tokens`, `temperature`, `response_schema_json`.

`provider.result.v1` requires `schema`, `profile_id`, `provider_id`, `route_id`, `model_id`, `content`; `structured_json` and `usage` are optional. `usage` contains exactly non-negative bounded integer `input_tokens` and `output_tokens`. Unknown fields, missing required fields, scope mismatch, schema mismatch, invalid structured JSON or over-limit values are rejected as `provider_schema_mismatch`/typed Provider error according to the A0 mapping. The Framework maps fixed snake-case Provider categories to the existing mapping table: `invalid_request→InvalidRequest`, `authentication|forbidden|redirect_rejected|policy_denied→Denied`, `not_found→NotFound`, `conflict→Conflict`, `rate_limited→RateLimited`, `timeout→Timeout`, `unavailable|server_unavailable|transport_unavailable→Unavailable`, `malformed_response|incomplete_stream→ProviderFailure`, `cancelled→Cancelled`, `unsupported→Unsupported`, `resource_exhausted→ResourceExhausted`, `corrupt_credential→RecoveryRequired`, and `internal_failure→InternalFailure`; unknown values map to `InternalFailure`. The Framework error code is `provider.` plus the bounded wire `error_code`, owner is `MarcusAwakeRuntimeService`, and details may contain only scope IDs and numeric status code.

### Exact cancellation behavior

The existing `AiTaskHandle` gains an internal cancellation delegate (public constructor/API shape remains source-compatible). `RuntimeServiceClient.SubmitAsync` returns a handle whose delegate sends a `cancel` frame carrying the original task scope through a second authenticated connection. This uses no new Transport handshake field: the client derives a unique control session ID as `original_session_id + ".cancel." + nonce`, reuses the existing launch proofs and campaign/timeline/generation, and requests only health and cancel capabilities. `RuntimeServiceHost` marks a connection as control-only when its negotiated capability set is exactly health plus cancel, rejects every non-cancel business frame on it, and authorizes the target against the active task's stored owner/campaign/timeline/original session/generation. The cancel payload is exactly `{ "schema":"marcus-awake.cancel.v1", "task_id":"...", "target_session_id":"..." }`. The normal business connection remains single-reader/single-writer; no Transport protocol source change is allowed in A1.

The cancellation linearization point is the Service lock that changes the task record to `cancelled_requested` or observes an existing terminal state. Before that point, a caller cancellation produces no local terminal event. After an accepted cancel acknowledgement, the handle publishes exactly one `Cancelled` event. If the task is already `result_captured`, `response_published`, `failed` or `cancelled`, the existing terminal template wins and cancel returns `false`; if the control connection is unavailable, it returns a typed unavailable result and leaves the handle nonterminal. Provider completion captured before cancellation wins over a later cancel; response-write cancellation never invokes Provider again. The only legal task transitions are `admitted→provider_in_flight→result_captured→response_published`, `admitted|provider_in_flight→cancelled_requested→cancelled`, and `admitted|provider_in_flight→failed`; `result_captured` and every terminal state reject later cancellation as a no-op.

### Exact local/cloud rule

`is_cloud` is required and must be explicitly serialized on every profile-upsert wire payload; omission and default inference are rejected. `is_cloud=false` permits all three Provider kinds only when `base_url` resolves to `localhost`, `127.0.0.1` or `::1`; credential reference is optional. `is_cloud=true` requires a non-empty credential reference and permits only the existing HTTP(S) endpoint policy. `ProviderConnectionProfile` validates the explicit flag before credential requirements. The Ollama local default is a UI convenience only and is serialized as explicit `is_cloud=false` before the request reaches the Service.

### Exact evidence schema minimum

`marcus-awake-p3d-a1-evidence.v1.schema.json` requires top-level `schema`, `plan_revision=7`, `batch_id`, `runner`, `service`, `fake_http`, `artifacts`, `cases`, `redaction`, and `flags`. `runner` requires `executable`, `arguments`, `exit_code=0`, and `working_directory`; `service` requires `executable`, `arguments`, positive `pid`, `private_pipe_name`, `exit_code=0`, and `process_role=service_child`; `fake_http` requires `bind`, `capture_path`, `request_count`, `harness_request_count=0`, and `service_request_count`; each case requires fixed `id`, `status=pass`, `process_role=service_child`, `request_message_type`, `response_message_type`, `provider_call_count`, `http_request_count`, `ledger_state_before`, `ledger_state_after`, `cancel_observed`, `write_outcome`, `replay_outcome`, and `observation_hash`; `redaction` requires scan paths and `secret_found=false`; `flags` requires `external_network=false`, `real_cloud_provider=false`, `bannerlord_started=false`, and `game_directory_synced=false`. The verifier recomputes all fixed IDs, command/path/hash equality bindings, request counts, state transitions and redaction results.

## 4. Locked implementation boundary

### 4.1 Allowed production write set

- `framework/MarcusAwakeRuntimeService/MarcusAwakeRuntimeService.csproj`
- `framework/MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs`
- `framework/MarcusAwakeRuntimeService/src/ProviderRegistry.cs` (new)
- `framework/MarcusAwakeRuntimeService/src/ProviderWireAdapter.cs` (new)
- `framework/MarcusAwakeFramework/src/RuntimeServiceClient.cs`
- `framework/MarcusAwakeFramework/src/ProviderRuntimeApi.cs` (new, required typed configuration/query port)
- `framework/MarcusAwakeFramework/src/AiTaskHandle.cs` (internal cancellation delegate only; preserve public API compatibility)
- `framework/MarcusAwakeProvider/src/ProviderContracts.cs` (local/cloud credential contract correction only)
- `framework/MarcusAwakeTransport/src/ProtocolModels.cs` (Provider response message constants only)
- `framework/MarcusAwakeTransport/src/ProviderProtocolContract.cs` (central Provider response message whitelist only)
- `framework/MarcusAwakeTransport/src/ProtocolValidation.cs` (admit the three already-contracted Provider response message types only)

### 4.2 Allowed test/evidence write set

- `framework/MarcusAwakeRuntimeService/tests/P3DA1Harness.cs` (new)
- `framework/MarcusAwakeRuntimeService/tests/Program.cs`
- `framework/MarcusAwakeRuntimeService/tests/MarcusAwakeRuntimeService.Tests.csproj`
- `framework/MarcusAwakeFramework/tests/*` only for focused A1 client mapping fixtures
- `framework/MarcusAwakeProvider/tests/*` only for local/cloud profile contract fixtures
- `framework/MarcusAwakeTransport/tests/Program.cs` only for the Provider response message admission regression
- `docs/schemas/marcus-awake-p3d-a1-evidence.v1.schema.json`
- `tools/verify_marcus_awake_p3d_a1.ps1`
- `docs/evidence/MARCUS-AWAKE-P3D-A1-*.json`
- `docs/checkpoints/MARCUS-AWAKE-P3D-A1-20260828-checkpoint.md`
- `docs/AWAKE-CURRENT.md` only to replace the unique current `next_action` after evidence is produced

### 4.3 Explicitly forbidden in A1

- no edit to `AWAKE.csproj`, `AWAKE/src`, `SubModule.xml`, `ModuleData`, `dist`, frozen candidates or game directory;
- no Bannerlord launch, MCM UI implementation or save-model change;
- no real external/cloud Provider request;
- no API key in IPC, Framework DTO, process arguments, logs, evidence or fake-server recordings;
- no stream, SSE/NDJSON, fallback, retry policy beyond the existing adapter/router behavior, profile persistence or execution receipt;
- no second Provider adapter implementation or second credential-protection implementation;
- no automatic profile import from old Marcus data.

### 4.4 Revision 7 necessary transport admission correction

The A1 implementation exposed the three already-contracted success response message types from `RuntimeServiceHost`, but the Transport validator rejected them as unknown message types. The first response therefore consumed response sequence `1` before serialization failed; the exception path emitted a second response at sequence `2`, which surfaced as `SequenceOutOfOrder` in the harness. Independent read-only review returned exact `VERDICT: APPROVED` for this correction.

Revision 7 only adds the exact message values `provider_profile_result`, `provider_models_result`, and `provider_result` to the central Provider response whitelist and the response-message admission path. It does not add a request capability, alter the envelope fields, handshake, checksum, sequence algorithm, Provider request schema, output schema, or A0/P3B/P3C evidence. The regression must prove exact round-trip acceptance and rejection of near-miss values, then rerun the full A1 harness and all existing Transport/P3B/P3C regressions.

## 5. Concrete wire result shapes

These shapes extend the A0-fixed response schema IDs without changing the envelope contract.

### Profile result

```json
{
  "schema":"marcus-awake.provider.profile_result.v1",
  "operation":"upsert|remove",
  "profile_id":"profile.example",
  "provider_id":"provider.example",
  "route_id":"route.example",
  "status":"ready|removed"
}
```

### Models result

```json
{
  "schema":"marcus-awake.provider.models_result.v1",
  "profile_id":"profile.example",
  "provider_id":"provider.example",
  "route_id":"route.example",
  "models":[{"id":"model-id","display_name":"Model"}],
  "capabilities":{"model_discovery":"available","text_generation":"unverified"}
}
```

### Unary completion result

```json
{
  "schema":"marcus-awake.provider.result.v1",
  "profile_id":"profile.example",
  "provider_id":"provider.example",
  "route_id":"route.example",
  "model_id":"model-id",
  "content":"reply",
  "structured_json":"{\"ok\":true}",
  "usage":{"input_tokens":12,"output_tokens":3}
}
```

Optional fields are omitted rather than emitted as null. All response strings, arrays, capability keys and usage values remain bounded by the Provider and Transport limits.

## 6. Acceptance cases

### Service/Provider cases

- `P3D-A1-S01-profile_upsert_creates_scoped_registry_entry`
- `P3D-A1-S02-profile_remove_is_scoped_and_idempotent`
- `P3D-A1-S03-models_calls_fake_http_and_maps_models`
- `P3D-A1-S04-complete_calls_fake_http_and_maps_typed_result`
- `P3D-A1-S05-missing_credential_never_calls_http`
- `P3D-A1-S06-provider_http_error_maps_safe_typed_error`
- `P3D-A1-S07-deadline_and_cancellation_stop_unary_call`
- `P3D-A1-S08-profile_scope_isolation`
- `P3D-A1-S09-duplicate_frame_replays_same_non_durable_result`
- `P3D-A1-S10-oversized_or_malformed_provider_response_is_rejected`
- `P3D-A1-S11-no_secret_in_logs_evidence_or_fake_server_capture`
- `P3D-A1-S12-child_service_shutdown_has_no_orphan`
- `P3D-A1-S13-cancel_before_provider_admission_never_calls_http`
- `P3D-A1-S14-cancel_during_http_maps_cancelled_without_late_completion`
- `P3D-A1-S15-provider_completion_before_ipc_write_failure_is_reused_without_recall`
- `P3D-A1-S16-same_idempotency_payload_replays_without_recall`
- `P3D-A1-S17-different_payload_same_idempotency_key_is_conflict_without_recall`

### Framework cases

- `P3D-A1-C01-unary_success_publishes_accepted_started_completed`
- `P3D-A1-C02-provider_error_maps_to_framework_error`
- `P3D-A1-C03-structured_json_and_usage_bounds`
- `P3D-A1-C04-caller_cancel_maps_to_cancelled_terminal`
- `P3D-A1-C05-service_unavailable_preserves_degraded_mode`
- `P3D-A1-C06-client_uses_real_provider_business_frame`

### Regression cases

- Existing P3B `19/19` and P3C `7/7` evidence must remain green.
- Existing Provider focused tests must remain green with `0 warnings / 0 errors`.
- The Framework client harness must exercise `SubmitAsync` through a real child Service and private pipe; direct frame construction alone does not satisfy the gate.

## 7. Evidence requirements

`P3D-A1-E1` must include source/build/contract evidence for the Provider project, Runtime Service, Framework, tests and exact write-set scan.

`P3D-A1-E2` must include:

- real Runtime Service child PID and private Named Pipe proof;
- fake HTTP request count and bounded request observations;
- per-case pass/fail records for every fixed case ID;
- typed result/error observations without payload or secret text;
- artifact hashes for the tested binaries;
- redaction scans over stdout, stderr, evidence and captured fake-server metadata;
- P3B/P3C regression results;
- explicit `external_network=false`, `real_cloud_provider=false`, `bannerlord_started=false`, `game_directory_synced=false`.
- exact runner/service executable arguments, child PID, pipe name, fake HTTP bind address, request capture path and result provenance for every business case.

## 8. Exit gate

The batch may be marked `offline_verified` only when:

1. The independent read-only review returns exact `VERDICT: APPROVED` for this plan revision.
2. All declared builds pass with `0 warnings / 0 errors`.
3. Every fixed A1 case is executed and passes; aggregate PASS alone is insufficient.
4. The fake HTTP server proves the Service, not the game process, owns Provider HTTP.
5. No secret or raw Provider exception appears in any captured output.
6. A0 evidence remains immutable and P3B/P3C regressions remain green.
7. The checkpoint records all changed files, hashes, limitations and one next action.

## 9. Follow-up boundaries

- `P3D-A2`: stream/SSE/NDJSON, stream FSM wiring, cancellation terminal races and route fallback.
- `P3D-B`: profile/route durability, credential provisioning, restart recovery and durable Provider execution receipt.
- `P4`: AWAKE MCM visible API Key, URL/model selection, explicit model fetch/connect buttons and persisted configuration transaction.
- `P5`: DevTools/SDK diagnostics, redacted export and package validation.
- `P6`: AWAKE NPC/worldbook/event caller integration.
- `P7`: final dependency removal, package synchronization and user-provided Bannerlord E3/E4/E5 evidence.
