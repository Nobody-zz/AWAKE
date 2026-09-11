# P3D Provider / Credentials / Streaming Prototype

## Gate status

- `task_id`: `MARCUS-AWAKE-P3D-PROVIDER-CREDENTIALS-STREAMING-20260826`
- `plan_status`: `implementation_authorized`
- `review_status`: `two_read_only_reviews_recorded; residual_findings_bound_to_implementation`
- `user_signoff_required`: `false`
- `user_signoff`: `explicit_current_turn_authorization`
- `implementation_authorized`: `true`
- `write_set`: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeProvider\**`
- `forbidden_write_set`: existing Framework Core, Transport, RuntimeService, AWAKE source/project, `docs`, `dist`, game modules, and any path outside `write_set`

## Confirmed repository facts

1. `framework/MarcusAwakeFramework/src/ProviderAndEgressApi.cs` exposes `RouteProfile`, `ProviderCapability`, `IRouteProfileResolver`, `IEgressPolicy`, and a small `ProviderFailure` DTO. It does not expose HTTP, credentials, model discovery, SSE parsing, or a low-level provider adapter interface.
2. `framework/MarcusAwakeFramework/src/AiGatewayApi.cs` exposes the logical `IAiGateway` task boundary: `AiTaskRequest`, `AiTaskEvent`, `IAiTaskHandle`, and `IAiGateway`. It deliberately does not expose vendor request/response DTOs.
3. `framework/MarcusAwakeFramework/src/FrameworkErrors.cs` and `RequestContext.cs` define typed error categories, owner/correlation metadata, deadline, session cancellation, and `OperationResult<T>`; these are `net472` Core contracts and are not modified by this prototype.
4. `MarcusAIFramework_Reference/设计大纲/07_ai_gateway_provider_routing.md` requires Connection/Model/Route separation, ordered fallback, capability probing, ordered stream events, deadline/cancellation, and retry only for explicit transient failures. It also says that a stream must not silently concatenate answers after a visible-output route change.
5. `MarcusAIFramework_Reference/设计大纲/13_api_contract_catalog.md`, `SDK_20260815/docs/ROUTE_CONTRACT.md`, and `框架文档/DEVELOPER_GUIDE.md` keep Provider details behind an Adapter/Companion boundary and require redacted errors/logs, logical routes, and no API key in task requests.
6. `docs/AWAKE-CURRENT.md` says P3A offline Core/Runtime vertical contracts are complete and the next integration batch is real Runtime Service/IPC. This prototype therefore remains standalone and must not wire into AWAKE or the frozen candidate.

## Behavior contract

### Entry -> call -> settlement -> observable result

1. Caller constructs an in-memory OpenAI-compatible connection profile with base URL, default model, capability limits, and an in-memory credential handle or a credential reference.
2. Provider adapter sends only bounded, authenticated HTTP requests to `/models` or `/chat/completions` through an injected `HttpMessageInvoker`.
3. Adapter settles each operation into a typed success or `ProviderError`; stream operations settle with a terminal `Completed`, `Failed`, or `Cancelled` event.
4. Caller observes model identifiers, connectivity status, completion text/structured JSON, ordered deltas/usage, redacted diagnostics, and ordered fallback/route-change events.
5. No campaign/save state is persisted. Optional credential persistence is isolated behind `IProviderCredentialStore` and never included in request/task DTOs.

### Selected smallest viable design

- Create a standalone `net8.0` class library with no reference to the existing `net472` Core assembly, TaleWorlds assemblies, Provider SDKs, or external NuGet packages.
- Define a narrow local Provider contract that can later be mapped to Core `RouteProfile`, `ProviderCapability`, `AiTaskEvent`, and `FrameworkError` by an integration layer; do not add that mapping now.
- Inject `HttpMessageInvoker` so the adapter does not automatically follow redirects. Require absolute `http`/`https` base URLs and reject userinfo/query credentials.
- Expose a small capability snapshot with explicit `available / degraded / unavailable / unsupported / unverified` states. This batch verifies model discovery only; text, streaming, usage, and structured-output support remain `unverified` unless a caller supplies a deterministic probe result. No silent capability claim is made from a URL.
- Evaluate a caller-injected endpoint policy before every request. The adapter never follows a `Location` header, rejects every redirect response, and only constructs requests from the validated configured origin plus fixed endpoint paths. DNS rebinding and private-network governance remain an explicit integration policy seam, not an unclaimed security guarantee.
- Store API keys in an `ApiKeyCredential` char buffer while alive, clear the buffer on dispose, and never serialize or log it. A request necessarily creates a short-lived header string; the implementation will not claim process-level secret isolation.
- Provide `InMemoryCredentialStore` for tests and callers that already own secure storage. Provide `ProtectedFileCredentialStore` as an opt-in fallback: Windows DPAPI via P/Invoke when available; otherwise AES-GCM over a domain-separated deterministic current-user/machine-derived key with an explicit warning that same-user local processes can recover it. Writes are atomic, temporary plaintext/cipher buffers are cleared, and corrupt/unsupported records fail closed. Best-effort Unix user-only file mode is applied; no cross-platform ACL guarantee is claimed.
- Enforce response byte, structured JSON byte, SSE event byte, line/data-field byte, delta count, aggregate stream byte, and model-count limits while reading raw bytes. Send `Accept-Encoding: identity` and reject compressed response bodies so decompression cannot bypass the limit.
- Use the caller `CancellationToken` plus an absolute deadline. Classify caller cancellation separately from deadline timeout; do not use an unbounded HTTP timeout.
- Map HTTP failures without returning raw response bodies: `400/422` invalid request, `401` authentication, `403` forbidden, `404` not found, `408` timeout, `409` conflict, `3xx` redirect rejected, `429` rate limited with bounded delta-seconds/HTTP-date `Retry-After`, `5xx` server/unavailable, and transport failures as unavailable. Caller cancellation wins over deadline cancellation; deadline cancellation wins over provider transport cancellation. No retry delay is scheduled in this prototype, so `Retry-After` is metadata only and cannot overrun the deadline.
- Parse standard OpenAI-compatible `/models` and chat-completions JSON plus `text/event-stream` `data:` frames. Ignore unknown JSON fields and unknown SSE event fields; require `[DONE]` before successful stream completion.
- Structured output is opt-in. The adapter validates that the caller-supplied schema payload is bounded JSON and sends it unchanged; it validates returned message content as bounded syntactic JSON only. Full JSON Schema semantics remain the Core/Runtime integration responsibility and are not falsely claimed here.
- Use a terminal-state machine for streams: exactly one terminal event; caller cancellation wins a race before terminal emission, deadline timeout is distinct, malformed/oversized frames fail, EOF without `[DONE]` fails as incomplete, partial final lines are parsed but cannot complete without `[DONE]`, and response/request resources are disposed on every exit.
- `ProviderRouter` preserves candidate order, removes duplicate provider IDs and duplicate effective attempt identities (normalized endpoint + effective model + credential reference), invokes each candidate at most once per operation, and falls back only on errors explicitly marked retryable. It never sleeps and never retries the same effective target.
- Streaming fallback is allowed only before the first visible `TextDelta`; after visible output, the router emits the terminal failure and does not concatenate a second provider's answer. A pre-output fallback emits `RouteChanged` with the source error and next provider.
- Local stream events expose monotonically increasing `Sequence`, provider/model, redacted source failure, and `FromProviderId`/`ToProviderId` on `RouteChanged`, so fallback is observable without relying on Core internals.

## Acceptance cases and evidence

| Case | Observable assertion | Evidence |
| --- | --- | --- |
| Config validation | Invalid scheme, empty model, credential reference, unsafe URL, or policy rejection fails without HTTP | focused deterministic test |
| Credential memory/redaction | Disposed credential no longer exposes chars; recorded logs and errors contain neither key nor Authorization header | focused test with known sentinel key |
| Protected file fallback | DPAPI or degraded format round-trips; raw file does not contain the key; atomic/corrupt-file behavior and degraded risk are observable | deterministic file-store test under this directory |
| Model discovery | `/models` request uses correct path/auth; bounded raw-byte JSON yields stable model IDs; compressed/malformed/oversized body is typed failure | fake handler contract test |
| Connectivity/capability | Successful discovery reports model discovery `available` and other unprobed capabilities `unverified`; 401/429/5xx/transport failures preserve category, status, retryability, and safe details | fake handler contract tests |
| Non-stream completion | Request JSON contains model/messages/optional structured response; valid response returns text/model/usage | fake handler contract test |
| Structured cap | `Content-Length` and chunked raw bodies over limit fail before materialization; schema payload is bounded JSON; returned structured content is syntactic JSON only and malformed content fails | fake handler tests |
| SSE stream | Ordered `Started -> TextDelta* -> UsageUpdate? -> Completed`; `[DONE]` required; unknown fields ignored; UTF-8 split/chunked lines, compressed bodies, aggregate/delta/event/line limits, malformed frames, and EOF are deterministic | fake streaming handler tests |
| Cancellation/deadline | Caller cancellation maps to cancelled; expired deadline maps to timeout; cancellation race emits exactly one terminal event with deterministic precedence and no late success | controllable fake handler tests |
| Fallback | Ordered candidates are attempted once; duplicate IDs and duplicate effective endpoint/model/credential identities are skipped; retryable failure reaches next candidate; non-retryable failure stops | router tests with counters |
| Stream fallback safety | Pre-output retryable failure emits sequence-ordered `RouteChanged` with source/target metadata; post-output failure never starts or concatenates another provider | router stream tests |
| Redirect/endpoint policy | 3xx `Location` is rejected and never followed; every fixed request path passes endpoint policy; cross-origin/unsafe configured URLs are rejected | fake handler/policy tests |
| Build isolation | Only files under the declared write set change; library and test runner build with `net8.0` and no package restore dependency | PowerShell path audit plus `dotnet build`/test runner |

## Non-goals

- No modification or registration in Framework Core, Transport, RuntimeService, AWAKE, `AWAKE.csproj`, or game modules.
- No implementation of `IAiGateway`, `IRouteProfileResolver`, egress policy, Companion IPC, persistence of task state, MCM, model pricing/tokenization, tool calling, embeddings, images, audio, retries with backoff, circuit breakers, or live cloud calls.
- No provider-specific vendor extensions beyond the minimal OpenAI-compatible chat/models/SSE shape.
- No claim that the degraded file store protects against a malicious or privileged local process.
- No game, Bannerlord, save/load, or real endpoint evidence.

## Open decisions requiring sign-off

1. **Core reference:** recommended `no Core project reference`; the `net472` public contract remains an integration target, not a compile-time dependency of this `net8.0` prototype.
2. **Capability report:** recommended report model discovery as verified by `/models`, leave generation/stream/usage/structured support `unverified` unless a caller supplies a probe fixture; no speculative live generation probe.
3. **Endpoint safety:** recommended no redirects plus an injected exact-origin endpoint policy; DNS/private-network controls remain a later integration-owned policy seam rather than an implicit allow/deny guess.
4. **Credential persistence:** recommended DPAPI-first plus explicitly warned AES-GCM degraded file fallback; in-memory operation remains the default and no key is written unless the caller opts into the store.
5. **Post-delta stream fallback:** recommended `disabled` by default to preserve the reference rule against silently concatenating answers after visible output.
6. **Structured response form:** recommended bounded syntactic JSON validation only in this adapter; Core/Runtime integration performs schema semantics against `SchemaRef`.

## Minimum evidence and handoff

- `E0`: this revised plan plus a second independent read-only review log with `VERDICT: APPROVED`.
- `E1`: `dotnet build -c Release` for library and tests, zero warnings/errors where the SDK permits, XML/JSON fixtures parse.
- `E2`: deterministic fake-handler test runner passes all acceptance cases; no live endpoint is called.
- Not claimed: E3 synchronization, E4 game path, E5 persistence/restart.
- Integration suggestion after sign-off: add a separate Core/RuntimeService adapter in a later reviewed batch that maps provider results to `FrameworkError`, `AiTaskEvent`, `RouteChanged`, egress policy receipts, and `RequestContext`; that later batch must own the real Companion/IPC boundary and caller wiring.
