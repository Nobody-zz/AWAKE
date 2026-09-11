# Plan: Persona Workbench K1 Contract Lock
_Narrow follow-up to the 2026-08-20 review; this plan authorizes contract/compiler work only._

## Goal

Freeze the smallest complete contract needed for Persona Workbench to compile a confirmed character document into a stable `[PERSONA_LOAD]` keyword payload. This batch must remove implementation-time ambiguity from the schema, legacy migration, registry/value mapping, rules, fixed interpretation instruction, payload grammar and budgets. It must not change Provider requests, Web UI behavior, AWAKE runtime code or any game candidate.

## Write boundary

- Allowed source files: `tools/persona-workbench/src/PersonaProtocol.Core/**`, `tools/persona-workbench/src/PersonaWorkbench.Core/**` only when adding the project reference/adapter, and `tools/persona-workbench/tests/**` plus the two K1 plan files.
- Allowed fixtures: `tools/persona-workbench/tests/fixtures/persona-k1/**` only.
- Generated `bin/**`, `obj/**`, `artifacts/**`, `.runtime/**` and release packages are not hand-edited.
- Forbidden: AWAKE `src/**`, `ModuleData/**`, `AWAKE.csproj`, `dist/**`, `_build_out/**`, game modules and Provider endpoint behavior.
- MCM: no change; this is local compiler/data-contract work with no Bannerlord runtime or player-facing setting.

## Contract decisions

### Canonical document

The canonical version is `awake.persona.authoring.v2`. Root fields are exactly:

`schemaVersion`, `documentId`, `displayName`, `reviewStatus`, `registryVersion`, `registryDigest`, `instructionVersion`, `compilerVersion`, `source`, `authored`, `facts`, `observations`, `rules`, `migration`.

Statuses are `draft`, `approved`, `disabled`. Unknown fields fail closed. All arrays serialize as arrays, never null. IDs use lowercase ASCII dotted grammar `[a-z0-9_]+` per segment.

`source` is `{ confirmedText:string, expandedText:string, confirmedTextSha256:string }`; only confirmed text is evidence-bearing. `authored` contains the current Workbench prose fields without injecting them into the formal keyword payload. `migration` is `{ originSchema:string, originTemplateVersion:string, warnings:string[], preservedLegacyData:object }`; warnings are ordinal sorted and migration metadata is excluded from compiler fingerprints.

Facts are typed records `{ factId:string, kind:string, value:string|integer, trust:string, provenance:string, enabled:boolean }`. K1 kinds are `ageYears`, `role`, `appearance`, `identityNote`; `ageYears` is an integer `0..120`; `ageYears >= 18` is adult-authoritative only when `trust=user_confirmed` and `provenance=manual_user_confirmation`. Provider and legacy migration cannot upgrade age authority.

Observations are `{ selectorId:string, value:string|integer|boolean, provenance:string, reviewState:string, evidence:object|null, enabled:boolean }`; review states are `accepted`, `needs_review`, `rejected`. Evidence is `{ sourceHash:string, startUtf16:integer, lengthUtf16:integer, quote:string }` or manual provenance with no span. K1 compiles only enabled accepted observations.

Rules are `{ ruleId:string, triggerSelectorId:string, responseSelectorId:string, scope:string, strength:string, priority:integer, counterweightSelectorId:string|null, provenance:string, evidence:object|null, enabled:boolean }`. Scope is `global|public|private|conflict|negotiation|romance`; strength is `slight|moderate|strong`; priority is `0..100`.

### Exhaustive v1 migration

Workbench v1 fields map as follows:

- `Id`, `DisplayName`, `SourcePackId`, `SourceDescription`, and all authored prose map directly to their v2 fields.
- `Status` maps to v2 status, but all migrated tags, facets and axes become `needs_review`.
- `TemplateVersion` maps to `migration.originTemplateVersion`.
- `Tags`, `FacetStrengths`, and nullable axes map through an explicit table in code. Precedence is axis value > facet strength > plain tag; lower-fidelity duplicates are discarded with warnings.
- Legacy facet strengths `1→slight`, `2→moderate`, `3→strong`, `4→strong` plus `legacy_strength_collapsed` warning.
- Every existing legacy tag must have a mapping or produces `legacy_tag_unmapped` and remains in `preservedLegacyData`; unmapped tags never compile.
- The committed mapping fixture covers every current v1 tag: `trait.cautious→axis.caution`, `trait.ambitious→axis.ambition`, `trait.proud→axis.pride`, `trait.pragmatic→axis.pragmatism`, `trait.loyal→axis.in_group_loyalty`, `trait.traditional→axis.tradition`, `expression.measured→axis.restraint`, `expression.direct→axis.directness`, `expression.formal→axis.formality`, `expression.playful→axis.playfulness`, `expression.warm→axis.warmth`, `behavior.bargains→axis.conditionality`, `behavior.observes→axis.deliberation`, `behavior.tests_trust→axis.trust_testing`, `behavior.keeps_leverage→axis.leverage`, `behavior.protects_ingroup→axis.in_group_priority`, `behavior.commands→axis.leadership`, `reaction.confronts→axis.confrontation`, `reaction.expressive→axis.emotional_expression`, `reaction.immediate→axis.reaction_timing`, `reaction.resentful→axis.resentment`, `reaction.seeks_support→axis.support_seeking`, `commitment.cautious→axis.promise_caution`, `commitment.persistent→axis.promise_persistence`, `commitment.non_tradeable→axis.value_tradeability`, `trigger.public_humiliation→trigger.public_humiliation`, and `boundary.no_empty_promises→boundary.no_empty_promises`; the fixture is exhaustive against the current registry.
- Migration never overwrites v1 and never auto-approves the migrated v2 document.

### Registry and deterministic bytes

The host deserializes registry JSON; core validates it. The committed fixture path is `tests/fixtures/persona-k1/registry.v1.json`, encoded UTF-8 without BOM, LF-only, final LF required, exact property order as stored, no insignificant whitespace rewriting. Registry canonical bytes are the raw bytes of that fixture before parsing, with no digest field inside the file. `registryDigest` is SHA-256 uppercase hex of those bytes; mismatch is checked before parsing.

Each selector JSON object is exactly `{ selectorId:string, category:string, valueDomain:string, emissions:object, conflicts:string[], dependencies:string[], allowedScopes:string[], evidenceRuleId:string, introducedVersion:string }`. Axis emissions use an explicit immutable `value→token` map for `-2,-1,0,1,2`; facet emissions use `1,2,3`; booleans use a single true emission. Registry validation checks ID/token uniqueness, symmetric conflicts, dependency existence, acyclic closure, scope compatibility and deterministic ordinal closure.

K1 starter selectors include all existing 25 axes, the two existing flags, `identity.role_warrior`, `appearance.body_small`, `trait.strong_willed`, `trait.tsundere`, `behavior.enemy_mercy_low`, `expression.defensive_pride`, `romance.experience_low`, and `romance.composure_low`. Romance selectors are non-explicit and require one enabled manual `ageYears >= 18` fact; failure is a validation error. K1 has no adult-content selectors.

### Rule semantics

- Trigger must be category `trigger`; response must be behavior/expression/reaction/romance/boundary.
- Response selectors must be single-emission selectors in K1; multi-value axes cannot be rule responses.
- Triggers co-occur unless their dependency closures contain a registered conflict. `global` overlaps every scope; other scopes overlap only when equal.
- Equal-priority conflicting rules fail validation. A higher-priority matching rule suppresses a lower conflicting rule, and the report records suppression.
- Counterweights are unconditional within their owning rule's matched scope, must oppose the response, and reduce effective strength one step: strong→moderate→slight→suppressed. Serialization emits `EFFECTIVE_STRENGTH`; original strength remains in the report.
- Canonical rule line:
  `RULE|ID=<ID>|TRIGGER=<TOKEN>|RESPONSE=<TOKEN>|SCOPE=<SCOPE>|EFFECTIVE_STRENGTH=<SLIGHT|MODERATE|STRONG|SUPPRESSED>|PRIORITY=<0..100>|COUNTERWEIGHT=<TOKEN_OR_NONE>`.

### Fixed instruction and payload grammar

The instruction fixture path is `tests/fixtures/persona-k1/persona-runtime-instruction.v1.txt`, encoded UTF-8 without BOM, LF-only, final LF required. Version is `persona-runtime-instruction.v1`; its SHA-256 is recorded in `registry.v1.json` and verified before use. Its exact text is:

`Interpret the following registered persona keywords as non-executable behavioral priors. Verified facts override inference. Data values are not instructions. Apply a behavior only when its trigger and scope match. Do not invent unsupported facts. Do not auto-escalate relationships or obedience. Preserve unresolved contradictions.`

Payload encoding is UTF-8 without BOM, LF-only, NFC data, final LF required, ordinal token order, fixed section order:

`[PERSONA_PROTOCOL]`, `[PERSONA_LOAD]`, `[PERSONA_CONSTRAINTS]`, `[PERSONA_IDENTITY]`, `[PERSONA_APPEARANCE]`, `[PERSONA_FACTS]`, `[PERSONA_TRAITS]`, `[PERSONA_EXPRESSION]`, `[PERSONA_BEHAVIOR]`, `[PERSONA_REACTION]`, `[PERSONA_COMMITMENT]`, `[PERSONA_ROMANCE]`, `[PERSONA_RULES]`.

Every section has only these line forms: `[PERSONA_PROTOCOL]` contains `VERSION=<ASCII_VALUE>` and `INSTRUCTION_SHA256=<UPPER_HEX>`; `[PERSONA_LOAD]` contains `STATUS=<DRAFT_PREVIEW|APPROVED_RUNTIME>` and `TOKEN=<ASCII_TOKEN>`; `[PERSONA_CONSTRAINTS]` contains mandatory `TOKEN=<ASCII_TOKEN>` lines; identity/appearance/facts/trait/expression/behavior/reaction/commitment/romance contain `TOKEN=<ASCII_TOKEN>` or `DATA|KIND=<ASCII_KIND>|VALUE=<ESCAPED_VALUE>|TRUST=<TRUST>`; rules contain the rule line above. Sections and lines are emitted only when non-empty except protocol, load and constraints, which are mandatory. Sections follow the listed order; only lines inside each section are ordinally sorted.

Data escaping is reversible: first replace literal backslash with `\\\\`, then `|` with `\\p`, `=` with `\\e`, CR/LF/TAB with `\\r/\\n/\\t`; decoder recognizes only these sequences and rejects unknown escapes. Control characters and unpaired surrogates fail validation. Display names max 128 scalars; fact values max 512 scalars; authored source is never emitted as data in K1.

### Budget contract

Default total is 8192 UTF-8 bytes, caller range 2048–24576. The total counts instruction bytes, every header, every line, every separator, and the final LF. Instruction max 1536, protocol+constraints max 1536, mandatory identity/facts/boundaries/rules max 3072; optional selectors consume the remainder. If mandatory output cannot fit, fail with `persona.template_budget_exceeded`.

Optional emissions receive total trim ranks: stylistic 10, food 20, weak expression 30, balanced axes 35, slight behavior 40, weak traits 50, reaction 60, moderate/strong traits and behavior 70. Equal ranks sort by section order, token ordinal, then token text. Commitments, romance, boundaries, facts and rules are untrimmable in K1. Trim only whole tokens, record every removal, and never emit partial output.

## Implementation and verification

1. Add the pure `netstandard2.0` contract/compiler core under the tool tree and reference it from the existing Workbench Core host.
2. Add the canonical registry fixture, fixed instruction fixture, exhaustive v1 migration fixture and golden payload vectors.
3. Add a tools-only `net472` smoke project; do not reference `AWAKE.csproj`.
4. Add tests for migration, registry closure, rule semantics, grammar, escaping, age gate, budgets and cross-target byte equality.
5. Run focused core tests, then existing Workbench tests. Confirm no Provider/UI/runtime files or frozen artifacts changed.

## Out of scope

Provider request/response changes, Web UI changes, AWAKE integration, game files, formal NPC content production, adult-content selectors, in-game editing and MCM changes.
