# Plan Review Log: Persona Workbench K1 Contract Lock

Act 1 completed from the prior three-round review. This is a narrowed K1-only contract plan; K2 UI, K3 Provider and K4 AWAKE integration are explicitly excluded and require separate approvals.

## Independent Read-Only Review — 2026-08-21

Reviewer confirmed the current r44 Core/Web tests pass and reviewed only the proposed K1 plan. No files were modified by the reviewer.

### P0 findings

1. The claimed exhaustive v1 migration table uses several IDs that do not exist in `PersonaTagRegistry.CreateDefault()` and mixes legacy tags with newer reaction/commitment axes.
2. `sourcePackId` has no valid destination in the exact v2 root schema.
3. The proposed ID grammar rejects existing hyphenated IDs such as `golden-pack` and `free-experiment`.
4. Migrating the document status while marking all observations `needs_review` creates falsely approved but non-compilable documents.
5. r44 has no per-observation acceptance UI/API, so a migrated formal path is unreachable inside a UI-excluded K1 batch.
6. Trigger and boundary selectors have no deterministic payload section or safe rule synthesis path.
7. r44 source, expanded, adopted, and confirmed text semantics cannot be inferred from current field names alone.
8. Replacing `PersonaDslGenerator.Generate` would change the already verified r44 endpoint output even without editing Web files.

### Required direction

- Freeze r44 and add a separate `PersonaV2Compiler`; do not redirect current endpoints or overwrite r44 golden vectors.
- Split K1 into K1A (`v2 DTO + real v1 migration + registry + rule-free token compiler`) and K1B (rules based on real trigger/response examples).
- Generate migration coverage from the real tag registry and five profile types; keep tag and axis mappings separate.
- Migrate all legacy documents as `draft`; preserve original status and ambiguous source fields in migration metadata.
- Define exact fact-to-section routing, display-name ownership, canonical source normalization, trusted digests, fingerprints, byte outputs, escaping vectors, and netstandard2.0 API restrictions before implementation.
- Remove compile-time scene matching, effective-strength suppression, and counterweights from K1A.

### Secondary findings

The plan also needs a composable or explicitly limited scope model, a complete selector schema, fail-closed nested enums and uniqueness rules, deterministic canonical JSON or a clearly separated asset hash, one coherent budget table, and executable net472 compatibility smoke.

VERDICT: REVISE
