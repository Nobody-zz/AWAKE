# Plan Review Log: PersonaWorkbench 语义丰富高密度 DSL`nAct 1 (grill) complete — plan locked with the user autonomous implementation request. MAX_ROUNDS=5.`n
## Round 1 — Codex
Findings:
- Core data loss: current budget algorithm removes whole public/private sections and can delete executable behavior.
- Identity loss: current fallback removes NAME.
- Priority inversion: reaction conditions are deleted before lower-value prose.
- UTF-8/semantic truncation is not implemented; arbitrary UTF-16 slicing can split content.
- Truncation double-escapes already escaped text.
- Diagnostic result/API contract is underspecified.
- Error propagation does not distinguish core-budget failure from optional compression.
- Observability and exact deduplication rules are underspecified.
- Tests do not cover all protected fields, escaping, typed errors, and compatibility.

VERDICT: REVISE

### Claude's response
Accepted all material findings. The revised plan now defines protected identity/behavior fields, a typed backward-compatible diagnostic result, explicit `template_core_budget_exceeded` versus optional compression, raw-value single escaping, exact same-field deduplication only, structured diagnostics without source text or secrets, and deterministic tests for every protected field and encoding boundary.

## Round 2 — Codex
Findings:
- Preview response exposes only DSL/errors and would miss diagnostics.
- Provider failure quarantine cannot represent canonical diagnostics and is in-memory only; retention scope must be explicit.
- DisplayName is not validator-required although NAME is protected.
- GenerateLegacy remains a second public generation path.
- Protected-field overflow and atomic versus segmentable behavior were not deterministic.

VERDICT: REVISE

### Claude's response
Accepted the interface and determinism findings. The revised plan now exposes nullable diagnostics on both preview and Provider responses, keeps canonical diagnostics response/session scoped instead of mixing them into Provider failure records or promising durable retention, normalizes a non-empty local display name before generation, locks `PersonaDslGenerator.Generate` as the only production path, and defines atomic identity/axis/trigger fields versus sentence-segmentable core evidence with an explicit core-budget failure.

## Round 3 — Codex
Findings:
- Preview overflow currently becomes an unstructured HTTP 500.
- Preview and Provider do not share identity normalization.
- Provider conversion maps canonical generator exceptions to ProviderFailure and quarantines them.
- GenerateLegacy remains a publicly callable divergent path.
- Compatibility tests and exact canonical error/status mapping were not explicit.

VERDICT: REVISE

### Claude's response
Accepted all findings. The final revision defines typed canonical generation states, a shared identity normalizer for preview/Provider/generator entry points, an independent canonical action status with no Provider quarantine, preview 4xx mapping, removal/internalization of GenerateLegacy, and explicit backward-compatibility tests for clients that ignore nullable diagnostics.

## Round 4 — Codex
Findings:
- Preview 4xx compatibility and error envelope were not precise enough.
- Typed result states and error codes lacked a single mapping table.
- Syntax reparse alone did not prove protected semantic fields round-trip unchanged.
- Byte-count convention was not explicitly frozen.
- “Protected fields” could incorrectly require absent optional fields.

VERDICT: REVISE

### Claude's response
Accepted all findings. The final revision adds the exact status/error/HTTP mapping table, preserves the existing preview 400 envelope, freezes UTF-8 counting over the emitted DSL, requires protected semantic snapshot equality after parse-render, and protects only validated fields that are present in the source document.

## Round 5 — Codex
Findings:
- The optional-field priority order was described only as a fixed priority, without a complete deterministic matrix or a same-priority deletion order.
- Text deduplication lacked an exact normalization contract, including Unicode form, whitespace folding, punctuation preservation, case comparison, field scope, and which occurrence wins.

VERDICT: REVISE

### Claude's response
Accepted both findings. The plan now fixes the complete priority order from identity through low-value evidence, requires stable section and input ordering, defines low-priority-first and later-item-first deletion, and specifies NFC/Trim/whitespace folding with punctuation preservation, Chinese non-rewriting, OrdinalIgnoreCase comparison for English, same-field-only exact deduplication, and first-occurrence retention.

## Round 6 — Codex
Findings:
- No remaining material implementation, compatibility, or validation blockers were found. The priority matrix, normalization/deduplication rules, protected-field semantics, UTF-8 and escaping checks, typed status mapping, preview/Provider error isolation, backward compatibility, and test coverage are sufficiently specified.

VERDICT: APPROVED
