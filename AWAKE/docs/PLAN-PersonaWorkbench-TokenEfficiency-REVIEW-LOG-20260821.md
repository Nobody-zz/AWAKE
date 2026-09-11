# Plan Review Log: Persona Workbench Token Efficiency

Act 1 complete: the user approved the conservative optimization design on 2026-08-21.

- Plan: `PLAN-PersonaWorkbench-TokenEfficiency-20260821.md`
- MAX_ROUNDS: 5
- Review policy: read-only
- Reviewer model: Codex CLI default from `~/.codex/config.toml`

## Round 1 — Codex

Session: `01a0237c-a7d2-7723-a2a6-0af32c2887b4`

The reviewer returned `VERDICT: REVISE` with these material findings:

- The proposed dynamic budget had no verifiable formula and could increase truncation.
- UTF-8 bytes are not Provider tokens and must not drive `max_tokens` without a matching tokenizer.
- DSL source validation, dispatch and evidence checks currently use differently trimmed text.
- Expansion budgeting did not clearly include the full controls envelope.
- Prompt compression lacked exhaustive assertions for all 25 axes, both flags, allowed fields, value ranges and prohibitions.
- Existing tests did not prove every axis and flag still maps through the parser correctly.
- Usage lacked one closed result/action-response value contract.
- Usage parsing did not define strings, fractions, null, overflow, partial fields or inconsistent totals.
- Existing Provider parameter dialects were not explicitly preserved or negotiated.
- Usage parsing needed to remain independent from the existing `message.content` response-shape behavior.
- Persisting raw model IDs could create a user-input leakage path.
- A persistent diagnostics sink lacked capacity, lifecycle and concurrency rules.
- Failure-path elapsed-time semantics were undefined.
- Existing conversion coverage could accidentally exercise only the legacy client rather than the named sparse client.
- Browser smoke did not cover conversion request counts, duplicate clicks, failures or cancellation.
- New top-level tests could compile without being registered and executed.

### Main model response

- Accepted the tokenizer criticism and removed dynamic output budgeting from this batch. Both narrow clients retain the existing `1800` ceiling.
- Restricted UTF-8 bytes to deterministic prompt-size comparison only.
- Added one-source normalization for validation, dispatch, parsing and evidence checks.
- Added exhaustive table-driven prompt and parser coverage for axis `0..24` and flag `0..1`.
- Defined one optional immutable `ProviderUsage` with strict bounded integer and total-consistency rules.
- Removed persistent diagnostics, model-ID retention and latency tracking; usage is session-only response metadata.
- Added separate named sparse and unnamed legacy request-capture tests plus conversion browser smoke.
- Kept existing `max_tokens` and `response_format` behavior; Provider capability negotiation remains out of scope rather than being mixed into this optimization batch.

## Round 2 — Codex

The same session returned `VERDICT: REVISE` with six remaining boundary issues:

- Source normalization needed an exact algorithm and one owner across dispatch, parsing, evidence, storage and ID derivation.
- Prompt compression needed a numeric regression threshold.
- Usage scope relative to the legacy full-draft fallback was ambiguous.
- Usage addition needed `Int64` overflow handling.
- Browser usage could remain stale after a later unavailable sample.
- Privacy verification needed to inspect serialized responses, browser state, failures, retry and session teardown.

### Main model response

- Defined action-boundary normalization: raw `24 KiB` acceptance remains, then CRLF/CR becomes LF and only outer Unicode whitespace is trimmed; interior formatting is preserved.
- Required all downstream conversion paths to consume that one normalized value without further normalization.
- Set the prompt gate to at least 20% reduction from the measured 2610-byte baseline, with a maximum of 2088 UTF-8 bytes.
- Restricted usage to the two narrow clients; the legacy full-draft client remains untouched.
- Required `Int64` summation and rejection above `Int32.MaxValue`.
- Required usage clearing at every operation start and explicit unavailable rendering for omitted or invalid current samples.
- Expanded privacy verification across serialized action responses, browser state, failure modes, retry and session teardown.

## Round 3 — Codex

The same session confirmed the prior six issues were closed and returned `VERDICT: REVISE` for one remaining compatibility concern: using normalized text for accepted source storage and automatic identity derivation could change existing Persona documents and stable IDs.

### Main model response

- Kept the existing raw/trimmed source semantics for `SourceDescription`, local fallback, display-name extraction and automatic ID derivation.
- Restricted the normalized copy to Provider dispatch and exact-source evidence comparison.
- Required the parser to receive the normalized evidence source explicitly without changing document construction semantics.
- Added CRLF/LF, Unicode edge-whitespace, stored source, display-name and automatic-ID compatibility regressions.

## Round 4 — Codex

The same session returned `VERDICT: REVISE` with two implementation-contract gaps:

- The parser needed an explicit two-source API because its current single source drives both document construction and evidence matching.
- Moving the `24 KiB` check to the action boundary could change the established Provider failure, quarantine and HTTP response contract.

### Main model response

- Defined explicit `documentSourceText` and `evidenceSourceText` parser inputs; only the latter uses the line-ending/outer-whitespace normalized copy.
- Preserved existing case and interior-whitespace evidence semantics.
- Kept the `24 KiB` validation inside the existing narrow client and required a regression for status, error code, quarantine and HTTP action behavior.

## Round 5 — Codex

The same read-only session confirmed both remaining issues were closed:

- The parser now has a planned `documentSourceText` / `evidenceSourceText` dual-source contract while preserving existing Persona source, fallback, display-name and automatic-ID semantics.
- The raw `24 KiB` validation remains in the narrow client with explicit status, error, quarantine and HTTP action-response regression coverage.

No material new regression was identified.

`VERDICT: APPROVED`
