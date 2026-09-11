# Plan Review Log: Persona Workbench Minimal Keyword Prompt

Act 1 complete. This is intentionally a small compiler-output batch to stop over-design and validate the core model-facing prompt shape before schema/Provider/runtime work.

## Review Closure

- The first draft was narrowed after review: this batch does not invent `TRIGGER/EFFECT/PRIORITY` rules and does not emit `[PERSONA_RULES]`.
- Existing prose fields now have explicit destinations in the canonical payload; `FoodPreference` and legacy summary-only sections are omitted from the model-facing output.
- User authorized implementation on 2026-08-21.

## Implementation Evidence

- `PersonaDslGenerator.Generate(...)` uses the keyword-constrained canonical generator; `GenerateLegacy(...)` remains the `[persona.*]` compatibility renderer.
- Canonical output includes `[PERSONA_LOAD]`, all seven fixed constraint tokens, `[PERSONA_IDENTITY]`, uppercase stable tag/axis IDs and `DATA_CN` prose markers.
- Trigger and boundary tags remain explicit tokens in `[PERSONALITY_CONTRADICTION]`; no unsupported rule semantics are generated.
- Shared golden fixture updated to the new canonical contract.
- Core console tests: PASS ALL on 2026-08-21.
- Web console tests: PASS ALL on 2026-08-21.
- Web project Release build: 0 warnings, 0 errors on 2026-08-21.
- Provider requests, Web UI flow, Persona schema, AWAKE runtime and game files were not changed by this batch.

## Closure Bugfixes

- Fixed the canonical budget regression that removed whole public/private keyword sections before optional prose and retained stale `SELF_IDENTITY` / `DESC_CN` trim targets.
- Added a regression proving oversized public/private prose is removed while `EXPRESSION_*`, `BEHAVIOR_*` and boundary keywords survive.
- Connected the already-existing sparse `ProviderDslConversionClient` to the production conversion action for named characters. The empty-name path intentionally retains the existing full draft client so automatic name extraction is not regressed.
- Added a service-level regression proving named conversion calls the sparse client once, does not call the full draft client, and reaches the canonical constraint payload.
- Packaged and verified standalone `r44`; these closure fixes still do not authorize the larger K1 schema/compiler or AWAKE runtime integration.

## Evidence Level

- Status: `offline_verified`.
- Evidence: `E2` for the standalone deterministic Workbench generator and its Web regression suite.
- Runtime/game evidence is not applicable to this isolated tool batch.
- Any future rule compiler, Provider contract change, schema migration or AWAKE integration requires a separate reviewed batch.
