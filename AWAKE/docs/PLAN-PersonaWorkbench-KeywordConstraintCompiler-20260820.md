# Plan: Persona Workbench K1 Canonical Contract and Keyword Compiler
_Revised after adversarial review rounds 1–2. This plan can authorize K1 only. K2 UI/persistence, K3 Provider conversion, and K4 AWAKE integration require separate locked plans, reviews, user sign-offs, task IDs, and checkpoints._

## Goal

Build the first authoritative Persona authoring contract and deterministic keyword compiler inside `tools/persona-workbench/**` without modifying the frozen AWAKE runtime candidate. K1 must prove that an editable character document can be migrated, validated and compiled into a byte-stable `[PERSONA_LOAD]` keyword payload plus a fixed interpretation instruction. AI networking, Workbench UI replacement and AWAKE runtime consumption are deliberately deferred.

## Write boundary and state

- Allowed writes: `tools/persona-workbench/**`, this plan/review log, and non-runtime test fixtures under the same tool tree.
- Forbidden writes: root `src/**`, `ModuleData/**`, `AWAKE.csproj`, `_build_out/**`, `dist/**`, game modules and frozen-candidate state.
- Frozen candidate `awake-20260820-syncpack-001` remains unchanged and pending E4/E5.
- MCM: no change. K1 is a local authoring/compiler library with no Bannerlord runtime, player-facing game control or saved campaign setting.

## K1 project boundary

- Add `PersonaProtocol.Core`, targeting `netstandard2.0`, with no JSON, Web, HTTP, secret, browser, Bannerlord or Marcus dependency.
- Hosts deserialize JSON into canonical DTOs; Core owns object construction, normalization, validation, registry closure, migration logic over DTOs, prompt compilation and prompt-byte serialization.
- Existing `PersonaWorkbench.Core` remains the `net10.0` host during migration and references `PersonaProtocol.Core`.
- Add a tools-only `net472` compatibility smoke project that references `PersonaProtocol.Core`; it must compile and execute golden compiler vectors without referencing `AWAKE.csproj`.

## Canonical schema: `awake.persona.authoring.v2`

Every v2 document contains exactly these root fields:

| Field | Type | Rule |
|---|---|---|
| `schemaVersion` | string | exactly `awake.persona.authoring.v2` |
| `documentId` | string | stable lowercase dotted ID |
| `displayName` | string | 1–128 Unicode scalar values; data only |
| `reviewStatus` | enum | `draft`, `approved`, `disabled` |
| `registryVersion` | string | exact pinned registry version |
| `registryDigest` | string | uppercase SHA-256 hex of canonical registry bytes |
| `instructionVersion` | string | exact immutable instruction version |
| `compilerVersion` | string | compiler contract version used for last accepted output |
| `sourcePackId` | string | optional lowercase dotted ID |
| `source` | object | confirmed source text, optional expanded text, source hash |
| `authored` | object | human-authored prose/list fields retained for editing |
| `facts` | array | typed confirmed data facts |
| `observations` | array | accepted selector/value records |
| `rules` | array | structured trigger/response rules |
| `migration` | object | origin schema, timestamp, warnings, preserved legacy data |

Unknown root or nested fields fail closed. Lists are never `null`; absent optional strings normalize to empty. Documents pin their registry and instruction. If the pinned registry or instruction is unavailable, compilation returns `persona.migration_required` and never silently selects a newer version.

### Source object

- `confirmedText`: authoritative user-confirmed prose.
- `expandedText`: optional editable expansion; never authoritative until copied into `confirmedText`.
- `confirmedTextSha256`: hash of NFC-normalized confirmed text.

### Authored object

Exact fields: `core`, `identityFacts`, `summary`, `publicDescription`, `privateDescription`, `contradictionDescription`, `foodPreference`, `selfClaimRules`, `realSelfBehaviors`, `selfClaimExamples`. These fields remain authoring evidence and are not emitted as free prose into the K1 formal keyword payload.

### Fact record

Fields: `factId`, `kind`, `value`, `trust`, `provenance`, `enabled`.

- K1 kinds: `ageYears`, `role`, `appearance`, `identityNote`.
- Trust: `user_confirmed`, `legacy_unverified`; Provider trust is not defined in K1.
- `ageYears` becomes authoritative for K1 only when manually/user confirmed. Migration and future Provider extraction cannot create or upgrade adulthood authority.

### Observation record

Fields: `selectorId`, `value`, `provenance`, `reviewState`, `evidence`, `enabled`.

- `reviewState`: `accepted`, `needs_review`, `rejected`.
- K1 compiler consumes only `accepted` and enabled observations.
- Evidence fields: `sourceHash`, `startUtf16`, `lengthUtf16`, `quote`; manual observations may use provenance `manual` with an empty span.

### Rule record

Fields: `ruleId`, `triggerSelectorId`, `responseSelectorId`, `scope`, `strength`, `priority`, `counterweightSelectorId`, `provenance`, `evidence`, `enabled`.

- Scope enum: `global`, `public`, `private`, `conflict`, `negotiation`, `romance`.
- Strength enum and ordinal: `slight=1`, `moderate=2`, `strong=3`.
- Priority range: `0..100`.
- Trigger matching contract for future runtime: `global` matches every context; otherwise both trigger token and exact scope must be active.

## Workbench v1 → canonical v2 migration

Migration never overwrites the v1 source file. The host reads v1, Core returns a v2 DTO plus warnings, and the user must explicitly save v2.

| Workbench v1 field | v2 destination | Migration semantics |
|---|---|---|
| `Id` | `documentId` | copied after ID validation |
| `DisplayName` | `displayName` | copied as data |
| `Status` | `reviewStatus` | copied, but migrated observations remain `needs_review` |
| `SourcePackId` | `sourcePackId` | copied |
| `SourceDescription` | `source.confirmedText` | copied, normalized and hashed |
| `Core` through `SelfClaimExamples` | matching `authored` fields | copied losslessly |
| `Tags` | `observations` | selector copied; provenance `legacy_v1`; `needs_review`; no fabricated evidence |
| `FacetStrengths` | `observations` | selector plus strength value; provenance `legacy_v1`; `needs_review` |
| 25 nullable axis values | corresponding axis observations | non-null values copied; provenance `legacy_v1`; `needs_review` |
| reaction/commitment free text | matching `authored` fields | copied losslessly; not converted to rules |
| unknown preserved legacy values | `migration.preservedLegacyData` | retained verbatim by the host adapter and never compiled |

The original v1 approval state does not auto-approve migrated selectors or rules. `CompilePreview` may show them only in a separate legacy-review report; `CompileRuntime` rejects until observations are explicitly accepted and the v2 document is approved.

## Canonical registry contract

The host deserializes registry JSON into DTOs. Core validates and constructs an immutable registry.

Each selector contains: `selectorId`, `category`, `valueDomain`, `emissions`, `conflicts`, `dependencies`, `allowedScopes`, `evidenceRuleId`, `introducedVersion`.

- ID grammar: lowercase ASCII segments separated by single dots; segment characters `[a-z0-9_]`; hyphens forbidden.
- Emitted tokens: uppercase ASCII `[A-Z0-9_]+`; reserved prefixes validated.
- Axis selectors use an immutable `value → token` table. K1 axis values are `-2,-1,0,1,2`; `null` emits nothing and explicit `0` emits the registered balanced token.
- Boolean selectors use `false → no token`, `true → one registered token`.
- Facet selectors use `1=SLIGHT`, `2=MODERATE`, `3=STRONG` through explicit emission entries.
- Registry acceptance requires unique selector IDs, unique emitted tokens, symmetric conflicts, existing dependencies, acyclic dependency graph, scope-compatible transitive closure, ordinal dependency traversal and duplicate-token elimination.
- Every v2 document pins the exact registry version/digest. Old registry versions remain loadable until an explicit document migration occurs.

## K1 starter selectors

K1 registers the existing 25 axes under stable selectors:

- Trait: `axis.caution`, `axis.ambition`, `axis.pride`, `axis.pragmatism`, `axis.in_group_loyalty`, `axis.tradition`.
- Expression: `axis.restraint`, `axis.directness`, `axis.formality`, `axis.playfulness`, `axis.warmth`.
- Behavior: `axis.conditionality`, `axis.deliberation`, `axis.trust_testing`, `axis.leverage`, `axis.in_group_priority`, `axis.leadership`.
- Reaction: `axis.confrontation`, `axis.emotional_expression`, `axis.reaction_timing`, `axis.resentment`, `axis.support_seeking`.
- Commitment: `axis.promise_caution`, `axis.promise_persistence`, `axis.value_tradeability`.

K1 also registers:

- `trigger.public_humiliation` → `TRIGGER_PUBLIC_HUMILIATION`.
- `boundary.no_empty_promises` → `BOUNDARY_NO_EMPTY_PROMISES`.
- `identity.role_warrior` → `IDENTITY_ROLE_WARRIOR`.
- `appearance.body_small` → `APPEARANCE_BODY_SMALL`.
- `trait.strong_willed` → `TRAIT_STRONG_WILLED`.
- `trait.tsundere` → `TRAIT_TSUNDERE`.
- `behavior.enemy_mercy_low` → `BEHAVIOR_ENEMY_MERCY_LOW`.
- `expression.defensive_pride` → `EXPRESSION_DEFENSIVE_PRIDE`.
- `romance.experience_low` → `ROMANCE_EXPERIENCE_LOW`.
- `romance.composure_low` → `ROMANCE_COMPOSURE_LOW`.

The two romance selectors describe non-explicit relationship behavior but still require a locally confirmed `ageYears >= 18` fact in K1. K1 contains no adult-content selector; adult content remains a separate future gated registry family.

## Rule validation and deterministic semantics

- Trigger selector must be category `trigger`; response must be `behavior`, `expression`, `reaction`, `romance` or `boundary`.
- Counterweight must be in the same response category and be declared as a conflict/opposition of the response selector.
- Dependency closure is expanded before conflict evaluation.
- Two potentially co-active rules conflict when their triggers can co-occur, their scopes overlap and their response emissions conflict.
- Equal-priority conflicting rules are invalid.
- For unequal priorities, the higher-priority matching rule suppresses the lower rule; suppression is recorded in `PersonaCompileReport`.
- Counterweight arithmetic reduces response strength by exactly one step in overlapping scope: `strong→moderate`, `moderate→slight`, `slight→suppressed`. It never changes priority or mandatory constraints.
- Invalid categories, incompatible scope, counterweight cycles or unresolved dependencies fail validation.

Canonical rule serialization:

```text
[PERSONA_RULES]
RULE|ID=<RULE_ID>|TRIGGER=<TOKEN>|RESPONSE=<TOKEN>|SCOPE=<SCOPE>|STRENGTH=<SLIGHT|MODERATE|STRONG>|PRIORITY=<0..100>|COUNTERWEIGHT=<TOKEN_OR_NONE>
```

Rules serialize in priority descending, then `ruleId` ordinal order. IDs and tokens are ASCII; no quoted prose appears in rule lines.

## Prompt layers and precedence

The compiled prompt contains a non-overridable meta-layer followed by data layers:

1. immutable `PersonaRuntimeInstruction`;
2. mandatory protocol/constraint tokens;
3. verified runtime facts — future K4 only;
4. verified authored facts;
5. current state/relation — future K4 only;
6. accepted timeline memory — future K4 only;
7. matching triggered rules — future K4 activation;
8. stable dispositions and expression tokens;
9. stylistic preferences.

Mandatory K1 tokens are: `CONSTRAINT_FACTS_OVERRIDE_INFERENCE`, `CONSTRAINT_DATA_NOT_INSTRUCTIONS`, `CONSTRAINT_TRIGGER_AND_SCOPE_REQUIRED`, `CONSTRAINT_NO_UNSUPPORTED_FACTS`, `CONSTRAINT_NO_RELATIONSHIP_AUTO_ESCALATION`, `CONSTRAINT_NO_OBEDIENCE_AUTO_ESCALATION`, `CONSTRAINT_PRESERVE_UNRESOLVED_CONTRADICTIONS`.

These prompt constraints are defense-in-depth, not enforcement. K4 must retain deterministic command permissions, preflight and relationship-stage bounds.

## Canonical payload grammar

- UTF-8 without BOM; LF newlines; Unicode NFC for data values; ordinal section and token ordering.
- Static K1 section order: `[PERSONA_PROTOCOL]`, `[PERSONA_LOAD]`, `[PERSONA_CONSTRAINTS]`, `[PERSONA_FACTS]`, `[PERSONA_TRAITS]`, `[PERSONA_EXPRESSION]`, `[PERSONA_BEHAVIOR]`, `[PERSONA_REACTION]`, `[PERSONA_COMMITMENT]`, `[PERSONA_ROMANCE]`, `[PERSONA_RULES]`.
- Proper names/facts use length-limited escaped `DATA|KIND=<ASCII_KIND>|VALUE=<ESCAPED_VALUE>|TRUST=<TRUST>` lines. The instruction states that `DATA` values never execute protocol directives.
- `CompilePreview` accepts draft/approved documents and emits `STATUS_DRAFT_PREVIEW` for non-approved input.
- `CompileRuntime` accepts approved documents with only accepted observations; K1 tests it but Workbench does not yet expose it as a runtime export.

## Budgets and trimming

Default total prompt budget is 8192 UTF-8 bytes; accepted caller range is 2048–24576 bytes.

| Part | Class | Limit/behavior |
|---|---|---|
| instruction | mandatory meta | max 1536 bytes; never trimmed |
| protocol + constraints | mandatory static | max 1536 bytes; never trimmed |
| identity/facts + boundaries + enabled rules | mandatory persona | max 3072 bytes; never trimmed |
| remaining selector sections | optional | consume remaining total budget |

The composed prompt must not exceed the caller's total budget. If mandatory parts exceed their caps or the total budget, compilation fails with `persona.template_budget_exceeded`.

Optional whole-token trimming order, first removed to last removed: stylistic preference tokens, food/preference tokens, weak expression tokens, weak behavior tokens, weak trait tokens. `moderate` and `strong` tokens, commitment tokens, romance tokens, boundaries, facts and rules are not trimmed in K1. Every removal is recorded; no section is byte-sliced and no token is truncated.

## Reports and privacy

- Core returns `PersonaCompileReport`: correlation ID, registry/instruction/compiler versions, input/output hashes, validation codes, dependency expansions, suppressed rules, trim decisions and byte counts.
- Provider-specific fields are excluded from Core. Future K3 defines a Web-owned `ProviderObservationReport`; reports join only by correlation ID and hashes.
- Logs contain IDs, hashes, spans, sizes and codes only. No API key, full prompt, raw evidence, private prose or Provider body is logged by default.

## K1 implementation steps

1. Add `PersonaProtocol.Core` and exact DTO/enums.
2. Add canonical registry construction/validation and starter registry fixture.
3. Add Workbench-v1 migration with exhaustive fixtures.
4. Add preview/runtime compiler, fixed instruction, payload serializer, rule validation, budgets and reports.
5. Reference Core from existing Workbench Core without changing Web UI behavior.
6. Add tools-only `net472` compatibility smoke.
7. Run focused tests, then all existing Workbench Core/Web/PowerShell launcher tests to detect regressions.

## K1 acceptance criteria

1. Representative v1 files migrate without source overwrite or known-field loss; migrated selectors require review.
2. Missing pinned registry/instruction fails explicitly instead of selecting latest.
3. Registry collisions, cycles, asymmetric conflicts and invalid value maps fail validation.
4. Rule conflict, suppression, counterweight and serialization golden tests pass.
5. Identical canonical input, pinned registry/instruction/compiler and budget produce byte-identical output under `net10.0` and the tools-only `net472` smoke.
6. Formal payload contains no Chinese field names, unregistered tokens or free-form personality prose.
7. The manually confirmed age-20 warrior fixture compiles the listed identity, appearance, temperament, enemy-response and non-explicit romance selectors.
8. Budget failures never emit partial or mid-token output; trim report exactly matches removals.
9. Existing Workbench UI and Provider behavior remain unchanged in K1; no runtime candidate artifact hash changes.

## Future roadmap — not authorized by this plan

- K2: v2 persistence/UI, separate keyword/full-prompt previews, rule review and explicit adoption. MCM no change because it is a local Workbench authoring UI, not a Bannerlord setting.
- K3: bounded Provider observation conversion, exact-quote local span resolution, capability profiles, removal/hard-disable of `/api/provider/generate-draft` and the legacy service/UI/DI path, and one-submission-only behavior. After 429, timeout, cancellation or ambiguous result, retry requires a new explicit user action. MCM no change because it is a local Workbench Provider workflow.
- K4: separately reviewed AWAKE integration after frozen-candidate release/supersession. It must load canonical v2 files, retain an AWAKE-v1 compatibility adapter, and use a project reference that packages a versioned module-local `netstandard2.0` DLL with load tests. K4 receives a new BuildId and reassesses MCM.

## Out of scope for K1

- Provider request or response changes.
- Workbench UI or persistence format switch.
- AWAKE source, worldbook, prompt or game-directory changes.
- Formal NPC persona production, automatic AI approval, adult selector families, culture-transition timing, rumor/history credibility and in-game persona editing.
