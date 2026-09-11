# Plan Review Log: Worldbook Studio AI 配置重基线

Act 1 (grill) complete — plan locked with the user. MAX_ROUNDS=5.

## Act 1 — Converged decisions

1. 本批次只审查 Worldbook Studio 的云端 Provider、Local Worker、配置来源、Provider 状态、请求/结果契约、Consent/CAS、失败回退和离线测试；MarcusAIFramework 完全排除。
2. `ProviderConfiguration.FromEnvironment()` 是唯一配置归一化权威，CLI、Web、状态页和实际调用不得各自解析。
3. 有效 DPAPI 本机云端配置优先；DPAPI 损坏、无法解密或字段不完整时，只有完整有效的环境变量配置才能整体回退；禁止部分字段混用，并显示非敏感警告。
4. Provider 必须由编辑者明确选择；云端和 Local Worker 不自动切换，不共享凭据、会话或失败重试状态。
5. Provider 的非敏感配置指纹和配置代次进入 Consent/请求生命周期；配置变化会使旧 Consent、请求哈希和待应用建议失效。
6. API Key/Worker Secret 轮换必须使旧授权和建议失效，但密钥本身、密钥哈希和 Secret 不进入持久记录、请求哈希或日志；DPAPI 保存/删除使用本机配置代次，环境变量变更以重启刷新。
7. Provider 状态页只做本地静态检查；不自动联网。主动“测试连接”只做一次有界探测/握手，不发送世界书正文、不调用模型、不消耗 Token。
8. 请求失败不自动重试；用户必须主动重做 Consent。配置变化期间的旧请求尽可能取消，不能取消的结果必须丢弃。
9. AI 结果始终 `review_only=true`，只能进入待审阅区；应用前重新通过 CAS、Provider 代次和路径权限检查。
10. 普通 Studio 操作离线；AI 仅在编辑者明确选择 Provider、预览发送范围并确认后单次启用；`--no-ai` 和离线审查模式 fail closed。
11. 日志只保留脱敏的本地诊断元数据，不保存密钥、完整提示词、原始响应或敏感 Header，不上传遥测。

## Round 1 — Codex

Reviewer verdict: `REVISE`.

### Critique

- P0: The plan called `ProviderConfiguration.FromEnvironment()` the sole normalization entry, but snapshot mode could fall back to the process environment, causing test/runtime drift. Fix: snapshot mode reads only the supplied dictionary.
- P0: `TryLoad()` returned only `bool`, so DPAPI corruption fallback could not expose a verifiable reason, warning, or source. Fix: introduce an immutable load/result snapshot shared by all entry points.
- P0: The plan's no-mixing statement conflicted with the implementation because DPAPI only covers cloud fields while Local Worker always uses environment variables. Fix: define atomic precedence per Provider group and report cloud/local sources separately.
- P0: No generation/fingerprint existed in the current implementation or Consent/suggestion records. Fix: bind configuration and credential generations plus a non-sensitive fingerprint to Web/CLI Consent, request and apply CAS.
- P0: Web re-created the request after consuming Consent without comparing the current Provider configuration. Fix: compare the captured and current configuration identifiers before request, result save and apply.
- P0: CLI Consent lacked the same configuration bindings and validation. Fix: extend the CLI schema and share the same CAS field set.
- P1: Web and CLI Consent stores had different invalidation semantics. Fix: keep physical isolation but create a shared validation contract and matrix.
- P1: Web Consent was deleted before provider execution and had no request state. Fix: model issued/running/completed/failed/cancelled transitions and require a new preview after failure.
- P1: Settings changes had no request cancellation registry. Fix: link per-session request cancellation sources to configuration generation changes.
- P1: Provider status lacked safe source/warning/generation fields. Fix: extend the status DTO while keeping `configured` as local validation only.
- P1: Invalid timeout/token fields were silently defaulted. Fix: distinguish missing, invalid, defaulted and valid values with structured warnings.
- P1: No independent test-connection endpoint or offline gate was visible. Fix: add a protocol-level health/handshake path behind a shared outbound gate.
- P1: Offline/no-AI was not proven for both Web and CLI. Fix: make the Core outbound gate mandatory for every Provider entry.
- P1: Provider routing and zero-retry behavior lacked factory/transport proof. Fix: add explicit route and one-attempt tests.
- P1: Logging had no centralized redaction boundary. Fix: map all HTTP/provider failures through a fixed redaction layer.
- P1: Public configuration records held plaintext secrets. Fix: separate safe snapshots from internal transport credential handles.
- P1: Credential rotation invalidation lacked an implementation mechanism. Fix: separate non-secret configuration/credential generations; do not persist secret-derived summaries.
- P1: Generation writes lacked atomicity. Fix: persist generation with settings atomically and verify after replacement.
- P1: CLI ACL argument construction was fragile. Fix: use `ArgumentList` and document Windows-only behavior.
- P2: Local Web host/origin boundary needed stronger proof. Fix: validate bind address, Host and Origin together.
- P2: Suggestion save lacked the second configuration CAS check. Fix: make `SuggestionStore.Save` accept and atomically validate the complete CAS envelope.
- P2: Existing checkpoint did not prove the new cases. Fix: list each new test, request count, input and assertion in the next checkpoint.
- P2: “No model/no Token” for test connection cannot be proven against a real upstream offline. Fix: use a mock health/handshake contract and keep real verification as a separate bounded demo.
- P2: Initial UI status loads needed an explicit no-network guarantee. Fix: split local status endpoints from outbound Provider operations and test the boundary.

### Codex verdict

The original plan was directionally correct but not implementable without the missing snapshot, generation, CAS and outbound-gate contracts. `VERDICT: REVISE`.

### Claude's response

- **Adopted all P0 and P1 findings.** The plan now requires provider-scoped atomic resolution, an immutable safe snapshot, an internal credential handle, explicit load states, configuration/credential generations, shared Web/CLI CAS fields, request lifecycle state, cancellation registration, a Core outbound gate, explicit zero-retry policy, a test-connection contract, and centralized redaction.
- **Adopted the material P2 findings.** Host/Origin validation, atomic suggestion-save CAS, per-test evidence, and local-status/no-network separation are now explicit.
- **Clarified rather than globally adopted the no-mixing wording.** Cloud DPAPI/environment values are atomic within the cloud group; Local Worker is an independent provider group by design. A cloud source and a local source appearing in one status snapshot is not field-level credential mixing.
- **Kept real-upstream Token/charging behavior out of this offline batch.** A fixed mock health/handshake contract can prove the local request shape and zero model body; it cannot prove an arbitrary upstream's billing behavior. That remains a separately labeled bounded demonstration.

## Round 2 — Codex

Reviewer verdict: `REVISE`.

### Critique

- The plan now names the missing snapshot, generations, CAS, request registry, outbound gate and redaction layers, but none is implemented yet; implementation is expected only after Act 2 and final sign-off, so these are treated as build obligations rather than proof of completion.
- P0: environment snapshot isolation and Provider-group atomic resolution still need explicit implementation and tests.
- P0: safe snapshot/transport credential separation, configuration/credential generation persistence, environment-credential cross-process rotation, shared Web/CLI CAS, request lifecycle/cancellation, second CAS before suggestion save, CLI state-file lifecycle and Core outbound gate remain mandatory.
- P1: health/handshake contract, zero-retry tests, status diagnostics, centralized redaction, and explicit test evidence remain mandatory.
- P1: the plan's new Origin policy conflicted with fixture F53, which intentionally allows a read-only Provider status GET without Origin. Resolve by defining a read-only local GET exception and keeping mutation/outbound endpoints strict.
- P1: the plan's `icacls` `ArgumentList` change was unrelated to the smallest configuration closure and would expand the batch.
- P1: the plan still said storage-contract changes were out of scope even though generation, Consent state and CAS require targeted storage changes.
- P2: the combined feature list risked scope inflation; split unrelated ACL/proxy/IPv6 hardening into a later batch.

### Claude's response

- **Accepted the required implementation obligations.** The plan now explicitly treats generation, Consent state, CAS, outbound gate and snapshot separation as implementation gates; the fact that code is unchanged during Act 2 is not being presented as completion.
- **Resolved F53 without weakening mutation security.** Read-only local Provider status GET keeps the existing no-`Origin` behavior, but requires the valid local session/CSRF and local binding; settings writes, Consent, analysis and test-connection POST/PUT remain strict exact-Origin/CSRF paths. F53 must remain a regression fixture.
- **Added a concrete cross-process rotation mechanism.** `ProviderGenerationStore` maintains non-sensitive cloud/local generations. DPAPI saves/deletes increment atomically; environment-secret rotation requires restart plus an explicit “credential rotated/invalidate old AI authorizations” action from the settings UI or CLI. No secret-derived digest is persisted.
- **Moved targeted storage extensions into scope.** Provider settings, generation state, Consent state and Suggestion CAS may be extended atomically; unrelated storage redesign remains out of scope.
- **Deferred unrelated hardening.** CLI `icacls` argument modernization, cross-platform ACL work, proxy/IPv6 Host expansion and general Web hardening are now follow-up work; this batch keeps only existing local-boundary and F53 regression proof.

## Round 3 — Codex

Reviewer verdict: `REVISE`.

### Critique

- P0: `ProviderGenerationStore` had no crash-consistency contract, and the plan appeared to have both DPAPI-local and shared generation authorities. Fix: make one `ProviderStateStore` record the sole authority, with a mutex, atomic replacement, read-back verification and startup cleanup.
- P0: Environment credential rotation lacked a unique cross-process source and concurrency semantics. Fix: define one atomic per-provider rotate operation and require the old process to re-read generation before every outbound gate.
- P1: Environment secret changes are inherently undetectable without a secret-derived digest. Fix: require an explicit non-sensitive rotation action and show a warning when it has not been performed.
- P1: Credential handles were not bound to a snapshot or lifetime. Fix: bind an internal short-lived handle to provider, generations and fingerprint; never serialize it.
- P1: Delete/source-switch semantics and complete CAS fields were not explicit. Fix: all relevant operations increment generations, and use one fixed `ProviderCas` envelope at analyze, save and apply/reject.
- P1: Consent state transitions, generation-change ordering and outbound gate inputs were underspecified. Fix: define the transition table, generation replacement as the linearization point, and a fixed one-shot `OutboundAuthorization` for analyze vs health handshake.
- P1: Zero-retry tests needed a complete failure matrix. Fix: cover both providers and both operations across connection, timeout, 429, auth, 5xx, JSON and cancellation failures with at most one send.
- P1: F53's local Host and CSRF prerequisites were not exact. Fix: pin Host/bind to `127.0.0.1:5077`, require session+CSRF, and keep the no-Origin exception only for that read-only GET.

### Claude's response

- **Accepted all contract findings.** `ProviderStateStore` is now the sole persisted generation/config authority; `ProviderGenerationStore` is only its access abstraction. Save/delete/source-switch/rotate use one mutex-protected atomic record, with old-or-new crash recovery.
- **Defined the environment rotation limitation safely.** Secret changes cannot be detected without storing a secret-derived value, so Web and CLI share an explicit `rotate-credential --provider cloud|local` operation. UI/CLI warn when environment credentials require rotation and never silently claim invalidation.
- **Closed the CAS and state-machine gaps.** The plan now fixes the complete envelope fields, three validation points, terminal state rules, generation linearization point and request cancellation ordering.
- **Closed the outbound gate gap.** `OutboundAuthorization` is now a fixed one-shot Core input with separate `analyze` and `health_handshake` operations; offline/no-ai fails before any Provider transport.
- **Kept the batch bounded.** The only acceptance path is snapshot → state generation → Consent/CAS → outbound gate → one Provider transport → second CAS save/apply. Unrelated ACL, proxy, IPv6 and general UI hardening remain deferred.

## Round 4 — Codex

Reviewer verdict: `APPROVED`.

### Final review result

- `ProviderStateStore` is now explicitly the sole generation/config authority; `ProviderGenerationStore` is only an access abstraction. The plan defines versioned records, per-user locking, temporary-file write, flush, replace, read-back verification, stale temporary cleanup and preserve-old-on-failure behavior.
- DPAPI save, delete, source switch and explicit rotation share one critical section and generation update contract.
- Environment credential rotation is explicitly `rotate-credential --provider cloud|local`; the plan acknowledges that secret value changes cannot be auto-detected and requires UI/CLI warnings instead of silently claiming invalidation.
- `CredentialHandle` is bound to Provider, configuration generation, credential generation and fingerprint, limited to one transport request, and forbidden from DTO/log/persistent serialization.
- `ProviderCas` fields and the three validation points are fixed; terminal state reuse, corrupted/crash recovery fail-closed behavior and generation linearization are defined.
- `OutboundAuthorization`, `offline/no-ai` priority, separate `health_handshake` authorization and the zero-retry matrix are concrete enough for offline tests.
- F53 is self-consistent: only the read-only GET on `127.0.0.1:5077` can omit `Origin`, while session, CSRF and exact Host remain required; writes, Consent, analysis and test connection remain strict.
- Scope is limited to the Provider invalidation chain; unrelated ACL, proxy/IPv6 and general Web hardening are explicitly deferred.

`VERDICT: APPROVED`
