# Plan Review Log: Persona Workbench Expansion Guidance

Act 1 completed from accumulated user tests and the explicit instruction to use recommended answers for subsequent technical choices.

- Plan: `PLAN-PersonaWorkbench-ExpansionGuidance-20260821.md`
- MAX_ROUNDS: 5
- Reviewer CLI: `codex-cli 0.147.0`
- Reviewer model: `gpt-5.6-sol`
- Reviewer reasoning effort: `high`
- Write policy: read-only
- The prior K1 review failed with an account-balance `403`; this review uses a new request and does not replay the failed call.

## Round 1 — External Codex

Session: `01a022fc-dc81-7fa2-a4c0-47e1d83f915c`

The reviewer confirmed no files were modified and returned `VERDICT: REVISE`.

Material findings:

- Add one authoritative server normalizer; define old-request defaults, Unicode/count limits, deterministic duplicate merging, and pre-dispatch rejection.
- Remove or define the ambiguous `custom` preset.
- Treat description and every control as untrusted data; use non-terminator-only framing and hostile-input tests.
- Preserve existing Chat Completions and official DeepSeek fields; detect truncated and malformed responses with stable errors.
- Align the UI and service limits, define accessible keyword editing, and add browser-level tests rather than source-string checks only.
- Prevent expansion/DSL/key/endpoint cross-operation overlap with one shared Provider-operation state.
- Keep invalid controls out of HTTP dispatch, quarantine and cooldown; distinguish cancellation, transport, rate-limit, truncation, malformed, unchanged, and Provider failures.
- Test that controls never enter Persona JSON, DSL, logs, quarantine, approval state, or adopted source fields.
- Make release gates executable: focused tests, Core tests, zero-warning Release build, launcher/cold-start/log smoke, fake Provider request count, process shutdown, port cleanup and package hash.

### Main model response

Accepted the material findings. The plan now removes `custom`, defines backward-compatible defaults and exact normalization, uses length-prefixed untrusted data blocks, locks existing DeepSeek/OpenAI-compatible request semantics, adds truncation/format errors, specifies accessible controls, introduces shared browser/server Provider operation gates, and expands acceptance evidence. Language detection and local-model suggestions remain out of scope.

## Round 2 — External Codex

The same read-only review session returned `VERDICT: REVISE` after checking the revised plan.

Remaining findings:

- The prompt framing still had an inconsistent block count and did not specify array serialization or prefix units.
- “Controls never become facts” was too strong to prove without a local fact extractor; the plan needed to state the best-effort boundary and test hostile responses as isolated drafts.
- The declared shared Provider gate lacked a stable busy error, complete cross-operation matrix, and explicit cancellation/exception release behavior.
- “Browser smoke” had no executable browser harness or command and could be reduced to source-string assertions.
- Response-shape and structured-output categories lacked a complete stable error-code table.

### Main model response

Accepted all five findings. The plan now defines one canonical five-field JSON envelope with deterministic property order and UTF-8 byte sizing; treats Provider prose as an untrusted editable draft rather than proven fact; defines `provider.request_in_flight` and `finally` release across all Provider-facing operations; specifies an installed Edge CDP harness and exact smoke command; and maps every listed response failure to a stable error code.

## Rounds 3–4 — External Codex

Round 3 retained three issues: per-field versus serialized-envelope size measurement, the Key→operation composite lease, and the lack of a programmable fake Provider for browser smoke. All three were incorporated.

Round 4 confirmed the composite lease and fake Provider were sufficiently closed, but found one stale sentence that still implied every limit was measured after JSON serialization. The sentence was corrected: field limits are pre-serialization, only the independent 32 KiB envelope ceiling is post-serialization, and overflow maps to `provider.expansion_envelope_size_invalid` before dispatch.

## Final Review — External Codex

The same read-only review session confirmed the envelope-size contradiction is corrected and returned:

`VERDICT: APPROVED`

The plan is now implementation-ready. This approval covers the expansion-guidance batch only; it does not approve K1 schema/compiler implementation, AWAKE runtime integration, game synchronization, or release publication.
