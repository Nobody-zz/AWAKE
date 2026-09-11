# Plan: Persona Workbench Token Efficiency
_Locked via user approval on 2026-08-21._

## Goal

Reduce repeated Provider token overhead in the current Persona Workbench prose-to-DSL workflow without changing the accepted Persona document, DSL, evidence, parser, fallback or security contracts. Add bounded usage observability so future optimization is based on Provider-reported data rather than estimates.

## Approach

1. Add failing Web tests for the existing two Provider clients.
   - Assert the DSL conversion request retains every supported axis, both flags, sparse evidence requirements and the existing response format while reducing the current `2610`-byte system prompt by at least 20% and keeping it at or below `2088` UTF-8 bytes.
   - Assert the two narrow clients retain the existing `1800` output ceiling; UTF-8 size is used only as a reproducible prompt-size metric, never as a token estimator.
   - Assert OpenAI-compatible `usage` fields are captured when valid, remain unavailable when omitted, and never affect success parsing.
   - Assert malformed, negative, fractional, string, overflowing, incomplete or internally inconsistent usage values are ignored rather than failing an otherwise valid Provider result.
   - Register every new top-level test explicitly and prove the test executable returns nonzero when an assertion fails.

2. Compress only the DSL conversion system prompt.
   - Preserve all 25 axis meanings, the two supported flags, exact-source evidence, sparse output, allowed root fields and invention prohibitions.
   - Replace repeated prose with one compact grammar and one indexed axis map.
   - Do not change field names, axis indices, value range, response schema or local candidate validation.
   - Add table-driven prompt-contract assertions for axis `0..24`, both directions, flags `0..1`, allowed root/nested fields, evidence requirements, value range and forbidden output classes.
   - Add table-driven parser tests proving all 25 axes and both flags still map correctly, including duplicate, invalid-index and mismatched-evidence rejection.
   - Leave the prose-expansion prompt unchanged in this batch.

3. Normalize the DSL source once and keep output budgets stable.
   - Keep the existing raw `24 KiB` validation inside `ProviderDslConversionClient`, preserving its current Provider status, error code, quarantine and HTTP action-response behavior.
   - After that validation succeeds, create an evidence-normalized copy by converting CRLF and CR to LF and trimming only leading/trailing Unicode whitespace; do not collapse interior spaces or blank lines.
   - Use that exact normalized copy only for HTTP dispatch and exact-source evidence comparison. The existing raw/trimmed values used by `SourceDescription`, local fallback, display-name extraction and automatic ID derivation remain unchanged in this batch.
   - Extend the sparse candidate parser with an explicit dual-source contract: `documentSourceText` retains the existing Core/SourceDescription/document-construction semantics, while `evidenceSourceText` is used only by evidence validation.
   - Preserve the current evidence comparison's case and whitespace semantics after line-ending/outer-whitespace normalization; no case folding or interior-whitespace collapsing is introduced.
   - Add CRLF/LF, leading/trailing Unicode whitespace, `SourceDescription`, display-name and automatic-ID regression tests proving this optimization does not change accepted Persona identity or stored source representation.
   - Add an oversized-source regression proving the existing Provider status, `provider.intermediate_request_invalid` code, quarantine behavior and HTTP action response remain unchanged.
   - Keep both narrow clients at the existing `1800` output ceiling until live Provider usage and truncation samples justify a separate budget change.
   - Do not alter the legacy full-draft client's `4096` compatibility budget.

4. Add optional Provider usage metadata.
   - Define one immutable optional `ProviderUsage` value containing bounded non-negative `prompt_tokens`, `completion_tokens` and `total_tokens`.
   - Accept usage only when all three fields are JSON integers within `Int32`, the sum is calculated in `Int64`, the sum does not exceed `Int32.MaxValue`, `total_tokens == prompt_tokens + completion_tokens`, and the usage container is an object.
   - Ignore unknown usage extensions such as cached or reasoning details.
   - Missing or invalid usage becomes unavailable and never changes an otherwise valid Provider response.
   - Do not retain model ID, endpoint, API key, source prose, expanded prose, candidate JSON or Persona DSL as telemetry.

5. Expose usage ephemerally through the two narrow-client result and action-response layers only.
   - Propagate the optional value through successful and response-invalid Provider results where a valid HTTP body supplied usage.
   - Do not add usage parsing or fields to the legacy full-draft client or its unnamed fallback contract in this batch.
   - Show the latest operation's reported counts in the current browser session only; do not write a diagnostics file, collection, browser storage or Persona field.
   - Clear the displayed usage at every Provider operation start and render `unavailable` when the current operation omits or supplies invalid usage; never retain the previous operation's counts.
   - Keep usage informational; it must not change drafts, errors, cooldown, quarantine, approval or save behavior.

6. Verify with focused tests, full Core/Web tests, zero-warning Release build and packaged fake-Provider smoke.
   - Capture the exact before/after DSL system-prompt UTF-8 byte count.
   - Capture named sparse conversion and unnamed legacy fallback independently with request handlers; assert the intended client and exactly one HTTP request.
   - Extend browser smoke to cover expansion and conversion success, duplicate click, failure and cancellation request counts.
   - Inspect serialized action responses and browser state for success, response-invalid, HTTP failure, cancellation and retry; only bounded counts may appear, stale counts must clear, and session teardown must remove the display state.
   - Confirm no secrets, model IDs, endpoints or character content are persisted or exposed by usage display.

## Key Decisions & Tradeoffs

- Optimize the narrow production paths first; retain the legacy full-draft path unchanged for compatibility.
- Prefer a compact fixed prompt over a second AI round or runtime schema discovery, avoiding extra requests and new failure modes.
- Keep output ceilings stable in this batch; actual output-budget changes require Provider-reported usage and truncation samples.
- Treat usage as optional telemetry because compatible Providers may omit it or use nonstandard extensions.
- Preserve behavioral correctness over maximal prompt reduction; the existing local parser remains the final authority.
- Preserve the existing `max_tokens` and `response_format` Provider dialect; capability negotiation is a separate compatibility batch.

## Risks / Open Questions

- The custom `gpt-5.6-luna` tokenizer is not locally available, so prompt byte reduction is the deterministic optimization metric until live usage is returned.
- Some Providers may count hidden reasoning separately or omit standard usage fields; the UI must label these samples as unavailable rather than complete.
- Over-compressing the axis map could reduce model compliance. Existing malformed-candidate tests and a fake-Provider request capture gate must catch contract drift.

## Out of Scope

- Changing Persona schema, DSL syntax, tag registry, AWAKE runtime loading or game UI.
- Removing the legacy full-draft compatibility client.
- Provider pricing calculations, currency estimates or account quota monitoring.
- Provider capability negotiation for `max_completion_tokens`, alternate response formats or non-Chat-Completions APIs.
- Persisting prompts, responses, API keys or endpoints for analytics.
- Synchronizing or modifying the frozen AWAKE runtime candidate.
