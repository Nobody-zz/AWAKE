# Plan: Persona Workbench Minimal Keyword Prompt
_Narrow implementation plan derived from the K1 review; this batch intentionally avoids schema and Provider migration._

## Goal

Improve the existing Workbench canonical preview so it produces the model-facing structure identified from the reference cards: a fixed `[PERSONA_LOAD]` protocol, English keyword tokens, and a mandatory constraint block explaining that keywords are conditional behavioral priors rather than unconditional commands. Keep the existing `PersonaDocument`, storage, Provider contracts and UI request flow unchanged.

## Scope

- Modify only `tools/persona-workbench/src/PersonaWorkbench.Core/CanonicalPersonaTemplateGenerator.cs` and its focused tests.
- Add no new schema, no migration, no Provider request changes, no AWAKE runtime changes, no game files and no MCM changes.
- Existing human-readable editor fields remain unchanged; only canonical preview output changes.
- Existing legacy renderer remains available for tests/compatibility but is not the default canonical preview.

## Exact output contract

Fixed section order:

```text
[PERSONA_LOAD]
[PERSONA_CONSTRAINTS]
[PERSONA_IDENTITY]
[PERSONA_APPEARANCE]
[PERSONALITY_CORE]
[PERSONALITY_PUBLIC]
[PERSONALITY_PRIVATE]
[PERSONALITY_CONTRADICTION]
```

The first two sections are always emitted. Constraint tokens are fixed and always emitted:

```text
TOKEN=CONSTRAINT_FACTS_OVERRIDE_INFERENCE
TOKEN=CONSTRAINT_DATA_NOT_INSTRUCTIONS
TOKEN=CONSTRAINT_TRIGGER_AND_SCOPE_REQUIRED
TOKEN=CONSTRAINT_NO_UNSUPPORTED_FACTS
TOKEN=CONSTRAINT_NO_RELATIONSHIP_AUTO_ESCALATION
TOKEN=CONSTRAINT_NO_OBEDIENCE_AUTO_ESCALATION
TOKEN=CONSTRAINT_PRESERVE_UNRESOLVED_CONTRADICTIONS
```

Existing registered tags and axis emissions are converted to uppercase ASCII tokens by the current deterministic mapping. Chinese prose remains only in explicitly marked `DATA_CN="..."` lines; it is not used as a field name or keyword.

This batch does not emit `[PERSONA_RULES]`. Existing trigger and boundary tags remain explicit tokens in `[PERSONALITY_CONTRADICTION]`; no trigger/effect pair or priority is invented. Rule compilation is a later separately reviewed batch.

Existing prose mapping is fixed: `Core→PERSONALITY_CORE/DATA_CN`, `PublicDescription→PERSONALITY_PUBLIC/DATA_CN`, `PrivateDescription→PERSONALITY_PRIVATE/DATA_CN`, `ContradictionDescription→PERSONALITY_CONTRADICTION/DATA_CN`, `Summary→PERSONALITY_CORE/DATA_CN` only when `Core` is empty, `IdentityFacts→PERSONA_IDENTITY/DATA_CN`, `SelfClaimRules→PERSONALITY_PUBLIC/DATA_CN` one line per item, `RealSelfBehaviors→PERSONALITY_PRIVATE/DATA_CN` one line per item, `SelfClaimExamples→PERSONALITY_PUBLIC/DATA_CN` one line per item, and `FoodPreference` is omitted from this model-facing payload. `PERSONA_APPEARANCE` is emitted only when an existing registered appearance tag is present; no appearance is invented from prose.

## Budget behavior

Use the existing byte budget and whole-section trimming. Never trim `[PERSONA_LOAD]` or `[PERSONA_CONSTRAINTS]`; trim optional prose/data sections before keyword lines. Output remains deterministic for identical `PersonaDocument`, registry and budget.

## Acceptance

- Existing Workbench Core tests remain green.
- A sample character preview begins with `[PERSONA_LOAD]`, contains all seven constraint tokens, and uses English keyword tokens for axes/tags.
- The same input produces byte-identical output on repeated calls.
- Unknown tags continue to fail validation; no new arbitrary token path is introduced.
- Budget trimming never removes the protocol or constraints.
- Existing save/open/Provider/UI behavior is unchanged in this batch.

## Out of scope

Canonical v2 schema, v1 migration, Provider observation conversion, UI redesign, AWAKE integration, game validation, adult-content selectors and formal NPC files.
