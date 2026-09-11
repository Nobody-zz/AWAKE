# Plan: Unified PERSONA_LOAD Template for Workbench and AWAKE
_Locked via grill — by Codex + user, 2026-08-18_

## Goal
Make the `[PERSONA_LOAD]`-style template the single canonical persona format for both Persona Workbench and AWAKE runtime. The Workbench will edit and preview the same template that AWAKE loads into dialogue prompts; only runtime context such as current kingdom, clan, role, relation, memory, and scene is appended through the same section grammar. The existing Chinese `[persona.*]` preview and AWAKE `<persona:v1>` output become legacy compatibility inputs only and must not be produced by new code.

## Approach
1. Define a versioned canonical template contract with English section names, English stable IDs, optional natural-language values, deterministic section ordering, and explicit draft/approved provenance.
2. Extend the shared Persona model and registry to map profile axes into registered IDs such as `TRAIT_RISK_BOLD_SLIGHT`, while preserving `null` versus `_BALANCED` semantics.
3. Replace the Workbench human-readable `[persona.*]` renderer with the canonical renderer; keep a separate Chinese editor summary that is never exported as the formal template.
4. Replace AWAKE `PersonaDslGenerator` output with the canonical renderer and add runtime sections for current identity, relation, memory, and scene without rewriting static personality sections.
5. Add legacy readers/migration for existing `[persona.*]` data and `<persona:v1>` data; migrated output remains draft until validated and approved.
6. Extend Provider draft output to produce canonical structured fields and registered IDs; reject unknown IDs, unsupported sections, invalid axis values, and unsupported free-form structural keys.
7. Add golden tests proving Workbench preview and AWAKE runtime output are byte-stable and identical for the same static definition and context.
8. Run Core/Web tests, AWAKE SDK smoke/build checks, template structure audits, package checks, and report game-unverified items separately.

## Key decisions & tradeoffs
- Workbench and AWAKE use one formal `[PERSONA_LOAD]`-style template contract; there is no new Workbench-vs-runtime syntax adapter.
- Stable semantic identifiers are English uppercase IDs. Chinese is retained only for character content values, dialogue examples, names, places, and optional human-readable summaries.
- `[PERSONALITY_CORE]`, `[PERSONALITY_PUBLIC]`, `[PERSONALITY_PRIVATE]`, `[PERSONALITY_CONTRADICTION]`, `[PERSONALITY_SUMMARY]`, `[SELF_IDENTITY]`, `[SELF_CLAIM_RULES]`, `[REAL_SELF_BEHAVIOR]`, `[SELF_CLAIM_EXAMPLES]`, and `[FOOD_PREFERENCE]` are the initial canonical sections.
- `[PERSONALITY_LAYERS]`, private-space modes, and interaction keyword libraries remain extensible sections and are not required for the first migration.
- Runtime identity is split from static author identity. Current kingdom, clan, role, marriage, location, relation, memory, and scene are appended as dynamic sections using the same grammar.
- New code never emits `[persona.*]` or `<persona:v1>`. Compatibility readers may accept them and migrate them to draft data.
- AI may propose only registered IDs and evidence-backed values. Missing evidence remains `null`; AI output is never auto-approved.

## Risks / open questions
- The reference templates contain multiple historical section variants; the first canonical section registry must define aliases and migration behavior without silently merging conflicting meanings.
- Existing AWAKE prompt consumers may expect `<persona:v1>` delimiters; the migration must update the single prompt injection path and retain a temporary compatibility fallback for unapproved or unmigrated data.
- English ID taxonomy may need additional approved IDs for romance, contradiction, and behavior patterns; unknown IDs must fail closed rather than be silently emitted.
- Full game validation of the new prompt format remains pending until the user runs AWAKE and supplies runtime logs.

## Out of scope
- No automatic approval of AI-generated personas.
- No game launch, live module overwrite, or hot reload in this batch.
- No redesign of the entire tag taxonomy beyond IDs required by the canonical template and approved examples.
- No deletion of legacy readers or old files until migration and runtime fallback tests pass.
