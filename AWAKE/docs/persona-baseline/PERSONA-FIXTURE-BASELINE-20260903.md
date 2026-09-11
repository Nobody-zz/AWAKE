# Persona Fixture Baseline — 2026-09-03

- Source candidate: `awake-20260903-awake-runtime-repair-004`
- Evidence: E2 current-run fixture reports

| Class | Fixture | Result |
|---|---|---|
| Positive | `PWB-AWAKE-001-valid-approved` | `pass/0` |
| Fallback | `PWB-AWAKE-008-last-known-good` | `pass/0` |
| Rejection | `PWB-AWAKE-003-missing-selection-rejected` | `reject/10`, expected `persona.schema_required_field` |
| Rejection | `PWB-AWAKE-004-unknown-tag-rejected` | `reject/10`, expected `persona.unknown_tag` |
| Rejection | `PWB-AWAKE-015-stale-selection-rejected` | `reject/10`, expected `persona.stale_revision` |

Persistence/recovery remains `not_attempted`; no Persona runtime projection or Prompt consumption claim is made.
