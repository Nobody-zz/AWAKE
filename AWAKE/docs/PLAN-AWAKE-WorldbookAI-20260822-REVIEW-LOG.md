# Plan Review Log: AWAKE Worldbook Studio AI Assistance
Act 1 (grill) complete — plan locked with the user. MAX_ROUNDS=5.

## Round 1 — Codex

**Adversarial Findings**
- **Data leak:** `/api/workspace` already exposes absolute workspace and schema paths; the plan does not define a redacted response policy for AI/provider diagnostics. **Fix:** Return workspace-relative identifiers only and explicitly forbid absolute paths, environment variable names, headers, and endpoint credentials in all Web/CLI/log DTOs.
- **Data leak:** “Remove secrets and original request content” before persisting suggestions can also remove provenance needed to verify what the provider saw. **Fix:** Persist only a separately defined redacted request manifest containing field allowlists, hashes, byte counts, and consent record—never perform ad-hoc string scrubbing.
- **Provider SSRF:** Arbitrary `WORLD_BOOK_LOCAL_WORKER_URL` and cloud `BaseUrl` permit loopback, private-network, redirect, or metadata-service access; “explicitly configured” is not a network boundary. **Fix:** Enforce HTTPS for cloud, loopback-only plus port allowlist for Worker, disable redirects, and reject private/link-local destinations unless explicitly approved.
- **Provider credential leak:** The plan does not specify whether API keys may enter proxy authentication, exception text, HTTP diagnostics, or provider response metadata. **Fix:** Construct authentication headers in one redaction-aware transport layer and scrub keys from exceptions, traces, serialized envelopes, and response bodies.
- **Consent bypass:** The existing Web endpoint mints confirmation tokens through unauthenticated `GET /api/confirmation-token`; loopback exposure alone does not prove the caller is the intended author. **Fix:** Bind consent to a short-lived server-side session plus exact document hash, provider, field set, and user action, and make token issuance POST-only.
- **Schema ambiguity:** `AssistanceRequest`, `AssistanceResult`, `SuggestionEnvelope`, and `KnowledgePatch` have no concrete versioned JSON schemas, required fields, size limits, or unknown-field policy. **Fix:** Add versioned `additionalProperties:false` schemas with bounded strings/arrays, explicit discriminators, maximum payload sizes, and stable error codes before implementation.
- **Patch safety gap:** “Allowed JSON Pointer whitelist” is unspecified and does not define array index bounds, duplicate operations, parent creation, root replacement, move/copy/test semantics, or YAML-to-JSON round-trip behavior. **Fix:** Publish an exact operation subset and per-path schema/type constraints, reject duplicate/ambiguous operations, and test canonical round trips.
- **Suggestion path conflict:** The current `WorkspaceWritePolicy` has no suggestion-specific policy. **Fix:** Add `RequireSuggestions` with a dedicated schema, retention rule, path traversal checks, and atomic-write contract.
- **Concurrency/CAS gap:** Hash validation before provider execution does not prevent two Web/CLI calls from saving, applying, or compiling against different buffers. **Fix:** Require an expected document revision/hash on every save/apply/compile and atomically reject stale writers with a distinct conflict code.
- **Atomicity gap:** “Atomically write suggestions” does not define temp-file naming, flush/rename, crash recovery, or orphan cleanup on Windows/OneDrive. **Fix:** Specify same-directory temp files, exclusive creation, flush/rename semantics, a completion marker, and orphan cleanup tests.
- **Provider compatibility:** Fixed `/chat/completions` plus “fixed JSON contract” does not address `response_format`, `max_tokens`/`max_completion_tokens`, content-part responses, markdown fences, or differing error envelopes. **Fix:** Define a bounded adapter matrix and one bounded fallback path with no provider-specific silent behavior.
- **Provider response integrity:** Schema validation alone does not bind a result to request hash, provider/model identity, schema version, or nonce. **Fix:** Require request hash, document hash, provider fingerprint, nonce, creation time, and integrity hash in every suggestion envelope.
- **UI wiring gap:** Current Web has no AI routes, state model, cancellation, stale-result handling, or diff/apply workflow. **Fix:** Specify concrete `/api/ai/*` routes and test consent preview, provider call, suggestion list, diff, apply/revert, cancellation, and stale-buffer rejection.
- **UI privacy gap:** Provider status and send ranges lack redaction rules. **Fix:** Define a privacy-safe status DTO and show stable error codes rather than endpoint or environment details.
- **CLI scope contradiction:** The plan names AI CLI commands but does not define their flags and output contract. **Fix:** Reconcile the command list and define exact flags, exit codes, and apply semantics.
- **CLI overwrite risk:** AI output paths are not bounded. **Fix:** Route every output through `WorkspaceWritePolicy`, reject reparse points and existing targets by default, and keep apply output on stdout in the first batch.
- **Testing gap:** Add SSRF/redirect, HTTPS, key scrubbing, oversized/deep JSON, malformed provider content, replayed consent, concurrent writers, rename failures, and reparse-race tests.
- **Browser-test gap:** Add a real browser flow for consent preview → stub provider → suggestions → diff → apply → stale rejection → revalidation, with secret non-disclosure assertions.
- **Scope gap:** The plan does not define whether unsaved buffers, sources, inherited profiles, or other documents are sent. **Fix:** Analyze only the saved selected document plus a bounded registry summary; prove the exact serialized projection with golden fixtures.
- **Status side effect:** Provider status must not perform network probes by default. **Fix:** Make status local configuration validation and require separate explicit probe consent.
- **Authority gap:** Applying a candidate patch must run the existing semantic validation before showing it as applicable. **Fix:** Validate every post-apply buffer and mark it unappliable on authority/control errors.

VERDICT: REVISE

## Round 5 — Codex

本轮未发现剩余的材料级阻塞。

已核对并确认：

- Web session 改为显式 `POST` bootstrap，静态 GET 不创建业务 session。
- Web 状态变更要求精确 Origin、HttpOnly/SameSite cookie 与 CSRF header；CLI consent 使用独立的 ACL-backed record，两个上下文不可互换。
- 建议读取由服务端 session/CLI record 派生绑定，不接受客户端提交可授权的明文 session ID。
- `DocumentCas` 覆盖 save、validate-buffer、compile、apply 及多 writer；CLI apply 的 expected hash、revision、buffer、suggestion hash 和 nonce 已明确。
- Cloud 请求使用独立 HttpClient、无代理、无重定向、无连接复用，并覆盖 DNS connect/reuse 测试。
- Worker HMAC、nonce、时间窗口、worker ID、响应大小及失败条件已定义，并纳入阻断测试。
- `SafeResponseMapper` 覆盖旧 workspace、confirmation、compile、export 接口，且旧接口脱敏回归已列为门禁。
- Provider HTTP/响应矩阵、CLI ACL、CAS、session bootstrap、跨文档隔离和“不运行/skip 即失败”的阻断规则均已具体化。

VERDICT: APPROVED

## Round 4 — Codex

仍有材料级阻塞，建议继续修订：

- **Web session 创建与 GET 无副作用矛盾：** 改为显式 `POST /api/ai/session/bootstrap`，精确 Origin 允许但不要求已有 CSRF；该调用只建立内存 session/cookie，不接触工作区或 Provider，并测试重复/缺失 Origin。
- **CLI apply 缺 CAS 输入：** 将 expected saved hash、revision、buffer ID、suggestion hash 和一次性 apply nonce 明确列入 CLI 参数或从受保护 suggestion record 原子读取；测试两个 CLI writer 的 stale rejection。
- **ConnectCallback 与连接池复用：** 每次 AI 请求创建并销毁独立 `HttpClient`/handler，禁用代理/重定向并关闭连接池复用，保证每次请求重新解析/校验/固定 IP；测试多地址、IPv4/IPv6 和连接复用。
- **CLI consent ACL：** 明确使用 Windows ACL，禁用继承、只允许当前用户、已有宽权限或 ACL 设置/验证失败均 fail-closed；记录存放在用户本地应用数据目录而非 workspace。
- **Worker HMAC 规范化：** 定义 UTF-8 length-prefixed 字段编码、Unix 秒时间戳、协议版本/worker ID 格式、Base64Url 签名、响应大小上限和错误码。
- **session ID 来源：** Web/CLI 客户端不得提交可授权的明文 session ID；Web 由 HttpOnly cookie 派生服务端 session，CLI 由受保护 consent record 派生绑定，建议文件只保存 hash。
- **旧端点脱敏：** `/api/confirmation-token`、`/api/compile`、`/api/export` 等现有端点也必须迁移到集中 `SafeResponseMapper` 并纳入全量阻断测试。

VERDICT: REVISE

## Round 3 — Codex (replacement session)

仍有材料级实现阻塞，不能批准：

- **CLI consent 与浏览器 session 未闭合：** CLI 两步调用需要独立的 CLI session/consent 存储；不能把浏览器 cookie、Origin/CSRF 或 CLI token 视为同一信任上下文。
- **CSRF/Origin 规则不够可执行：** 需要精确允许的 Origin、缺失/多值 Origin 行为、CLI 与浏览器的不同认证路径，以及统一状态变更保护；旧的未认证成人确认 GET 也应隔离或收紧。
- **Worker 信任协议没有落成契约：** “安装密钥或签名 challenge-response”必须定义握手消息、签名字段、nonce 生命周期、密钥边界、轮换/撤销和错误码。
- **DNS rebinding 防护缺少连接层方案：** 必须明确 HttpClient handler/ConnectCallback、固定 IP、Host/SNI、每次连接校验，以及 IPv4/IPv6、多地址、代理路径处理。
- **CAS 范围仍有遗漏：** save、compile、validate-buffer 和多窗口/多 CLI writer 也必须使用统一原子 revision 更新，不能只保护 apply。
- **建议读取隔离需要服务端强制语义：** 缺失或错误的 document/session/hash 参数必须统一拒绝且不可枚举，不能只作为查询过滤。
- **端点脱敏需要全局边界：** 现有 workspace、错误和诊断可能暴露绝对路径；需要集中 DTO/异常映射和所有端点的负向测试。
- **Provider 兼容矩阵仍不是实现规格：** 需要固定 HTTP 状态映射、响应路径、body 上限、空内容、超时/取消、错误脱敏和每类 fixture 的预期错误码。
- **阻断测试失败语义不明确：** session 隔离、CSRF、DNS rebinding、Worker spoofing、跨文档访问、endpoint 脱敏和 stale CAS 必须是 release gate；未运行、skip 或不确定都阻断。

VERDICT: REVISE

## Round 2 — Codex

**Resolved Prior Findings**
- Versioned strict schemas, discriminators, bounds, hashes, nonce, and integrity fields address the earlier contract ambiguity.
- SSRF controls, HTTPS, redirect rejection, loopback Worker limits, and provider compatibility fallbacks address the earlier provider risks.
- Consent is short-lived, single-use, session/document/provider/request bound.
- Suggestion storage has a dedicated boundary, atomic-write requirements, capacity limits, and crash cleanup.
- Patch operations, pointer restrictions, CAS, overlay validation, and control-field rejection are substantially clearer.
- Web/CLI routes, UI stale-state handling, browser testing, concurrency tests, and reparse/OneDrive fault tests are explicitly planned.
- The plan acknowledges the existing `/api/workspace` absolute-path leak and requires correcting it.

**Remaining Material Flaws**
- **CLI consent bypass:** `ai-analyze --confirm-cloud` does not define how the exact preview is confirmed. **Fix:** Use `ai-consent-preview` to emit a redacted preview plus single-use nonce; require `ai-analyze --consent <token>` and reject bare confirmation flags.
- **Loopback session security:** HttpOnly alone does not define session creation, SameSite/CSRF, browser binding, or malicious-page protection. **Fix:** Require startup session cookie, `SameSite=Strict`, per-session CSRF header, Origin allowlist, and both cookie/header for state changes.
- **Suggestion read isolation:** list/read routes lack document-hash/session binding. **Fix:** require exact document ID, source hash, current session and return only matching suggestions.
- **DNS rebinding:** configuration-time private-address checks can be bypassed at connect time. **Fix:** resolve and pin the endpoint immediately before connection and validate every resolved address in a custom handler.
- **Worker impersonation:** loopback/port limits do not authenticate the Worker. **Fix:** require a protected per-installation shared secret or signed challenge and test a spoofed Worker.
- **CAS scope:** repeated apply/revert and two tabs are not fully bound. **Fix:** bind suggestion/apply to document hash, revision, suggestion hash, session/buffer ID and one-time apply nonce; invalidate after mutation.
- **Atomic naming/race:** collision and duplicate logical suggestions are unspecified. **Fix:** use collision-resistant IDs, exclusive creation and deterministic dedupe by request/provider/nonce hash.
- **Compatibility matrix:** fallback behavior is not concrete enough. **Fix:** add a table of request fields, response extraction rules and exact fallback/error codes with fixtures.
- **Endpoint acceptance:** `/api/workspace` redaction is not a blocking test. **Fix:** test every endpoint, diagnostic and error DTO for absolute paths, hosts, env names, headers and secrets.
- **Content limits:** file/count limits do not bound decoded nested result sizes. **Fix:** enforce per-field, per-suggestion, total-result, depth and post-normalization limits.
- **Provider status disclosure:** status semantics and model/host exposure are underspecified. **Fix:** return only `configured/missing/invalid/blocked`, never host, model path, env name or raw validation details.
- **Sequential buffer semantics:** multiple suggestions do not define composition and undo. **Fix:** use monotonically increasing in-memory buffer revisions and deterministic undo records; each apply targets the immediately previous revision.
- **Security tests:** negative assertions must block approval. **Fix:** make session isolation, CSRF, Worker spoofing, DNS rebinding and cross-document access mandatory pass criteria.

VERDICT: REVISE
