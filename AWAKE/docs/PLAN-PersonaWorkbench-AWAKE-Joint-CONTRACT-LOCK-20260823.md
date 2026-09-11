# Contract Lock Annex: PersonaWorkbench × AWAKE Joint Bridge

> Normative planning annex for `PLAN-PersonaWorkbench-AWAKE-Joint-20260823.md`. This file freezes contract and evidence obligations; it does not authorize source, dist, game-directory, or frozen-candidate changes.

## 1. Canonical schema/version matrix

| Object | `$id` | `schemaVersion` | Canonical file/object | Owner | Runtime role | Unknown fields | Digest bytes |
|---|---|---|---|---|---|---|---|
| Workbench source | `persona-workbench.character.v1` | `persona-workbench.character.v1` | Workbench document JSON | PersonaWorkbench | Migration input only | Workbench reader contract; adapter preserves raw source | Original UTF-8 bytes as read |
| Persona authoring | `awake.persona.authoring.v2` | `awake.persona.authoring.v2` | `docs/persona-contract/awake.persona.authoring.v2.schema.json` | Joint Persona contract | Authoring/intermediate only | Reject at root and nested objects; only declared `extensions` may vary | Canonical JSON bytes, excluding detached digest |
| Persona export envelope | `awake.persona.export.v1` | `awake.persona.export.v1` | `docs/persona-contract/awake.persona.export.v1.schema.json` | Joint adapter | Candidate transport | Reject at root and nested objects | Canonical envelope bytes, excluding detached digest |
| Runtime Persona definition | `awake.persona.definition.v1` | `awake.persona.definition.v1` | `ModuleData/Worldbook/persona_definitions/definitions/*.json` in a future candidate | AWAKE runtime | Approved runtime input | Reject; no silent ignore | Canonical definition bytes |
| Persona registry | `awake.persona.tags.v1` | `awake.persona.tags.v1` | `ModuleData/Worldbook/persona_definitions/tag_registry.json` | AWAKE content/runtime | Target vocabulary | Reject | Raw UTF-8 registry bytes |
| Worldbook package manifest | `awake.worldbook.package.v2` | `awake.worldbook.v2` | `ModuleData/Worldbook/manifest.json` in a future v2 candidate | AWAKE runtime | Package entry | Reject | Canonical manifest bytes |
| Worldbook runtime payload | `awake.worldbook.runtime.v2` | `awake.worldbook.v2` | Manifest `entrypoints.runtime`, normally `runtime.json` | AWAKE runtime | Knowledge payload | Reject | Canonical runtime bytes |
| Worldbook index payload | `awake.worldbook.index.v1` | `awake.worldbook.index.v1` | Manifest `entrypoints.index`, normally `index.json` | AWAKE runtime | Index payload | Reject | Canonical index bytes |
| Worldbook installed registry | `awake.worldbook.registry.v1` | `awake.worldbook.registry.v1` | Explicit installed-registry path only | AWAKE runtime | Package selection envelope | Reject | Canonical registry bytes |
| Candidate ledger | `awake.candidate-ledger.v1` | `awake.candidate-ledger.v1` | `docs/persona-awake-candidate-ledger.v1.json` | Evidence controller | Attribution only | Reject | Canonical ledger bytes |

`awake.worldbook.v1` is a legacy migration input only. The new runtime accepts a v2 package manifest, or a registry.v1 envelope whose selected package resolves to v2 package/runtime/index objects. `registry.v1` is not a synonym for package manifest. The current v1 source/dist/game manifests are therefore not an active v2 runtime candidate.

## 2. Authoring-v2 minimum contract

The existing K1 plan remains `REVISE`; it is evidence, not implementation approval. This annex freezes the minimum K1A-compatible shape required by this bridge. The exact root fields are:

```text
schemaVersion, documentId, displayName, reviewStatus,
registryVersion, registryDigest, instructionVersion, compilerVersion,
sourcePackId, source, authored, facts, observations, rules, migration
```

Rules:

- `schemaVersion` is exactly `awake.persona.authoring.v2`.
- `documentId` is lowercase dotted grammar: segments `[a-z0-9_]+`, separated by single dots; hyphens are rejected.
- `reviewStatus` is `draft|approved|disabled`; migrated v1 observations and rules are always `needs_review` regardless of copied v1 status.
- `registryVersion`, `registryDigest`, `instructionVersion` and `compilerVersion` are required pinned strings; missing pinned assets return `persona.migration_required`.
- `sourcePackId` is optional in authoring-v2, but required by export-v1 when the runtime definition declares a content source. It is never inferred from a filename or display name.
- `source` is `{confirmedText, expandedText, confirmedTextSha256}`; only confirmed text is evidence-bearing. `confirmedTextSha256` hashes NFC-normalized confirmed text.
- `authored` is exactly `{core, identityFacts, summary, publicDescription, privateDescription, contradictionDescription, foodPreference, selfClaimRules, realSelfBehaviors, selfClaimExamples}`.
- `facts` are `{factId, kind, value, trust, provenance, enabled}`; K1 kinds are `ageYears|role|appearance|identityNote`.
- `observations` are `{selectorId, value, provenance, reviewState, evidence, enabled}`; `reviewState` is `accepted|needs_review|rejected`.
- `rules` are `{ruleId, triggerSelectorId, responseSelectorId, scope, strength, priority, counterweightSelectorId, provenance, evidence, enabled}`; scope is `global|public|private|conflict|negotiation|romance`, strength is `slight|moderate|strong`, priority is `0..100`.
- `migration` is `{originSchema, originTemplateVersion, authoringRevision, warnings, preservedLegacyData}`. Migration warnings are sorted and excluded from compiler fingerprints.
- Unknown root/nested fields fail closed; arrays serialize as arrays, never null.

The authoritative schema hash, sourcePackId handling, ID grammar and migration status are the files created/approved under this joint Contract-Lock batch, not the unresolved K1 review log. No implementation may create a second authoring-v2 variant.

## 3. Workbench-v1 migration and runtime selection

Workbench-v1 migration is loss-preserving and never overwrites the v1 source. The mapping is fixed:

| Workbench v1 | Authoring v2 | Rule |
|---|---|---|
| `Id` | `documentId` | Copy after grammar validation; reject invalid ID |
| `DisplayName` | `displayName` | Copy as data |
| `Status` | `reviewStatus` | Copy status, but migrated observations remain `needs_review` |
| `SourcePackId` | `sourcePackId` | Copy; empty remains empty |
| `SourceDescription` | `source.confirmedText` | NFC normalize and hash |
| prose fields | matching `authored` fields | Lossless copy |
| `Tags` | `observations` | `legacy_v1` provenance, `needs_review`, no fabricated evidence |
| `FacetStrengths` | `observations` | Explicit strength mapping, `needs_review` |
| 25 nullable axes | axis observations | Non-null values copied, `needs_review`; null emits no observation |
| reaction/commitment free text | matching `authored` fields | Copy only; never invent formal rules |
| unknown legacy values | `migration.preservedLegacyData` | Preserve verbatim; never compile |

Migration may return `preserve_only` warnings. Export/runtime promotion rejects any runtime-meaningful unmapped value, absent provenance, unknown target registry ID or digest mismatch. `preserve_only` is never a legal runtime-definition status.

## 4. Export envelope `awake.persona.export.v1`

The envelope has exactly these root fields:

```text
schemaVersion, exportId, sourceSchema, sourceDocumentId, sourceRevision,
sourceSha256, authoringSchema, authoringRevision, authoringSha256,
registrySchema, registrySha256, mappingReportId, mappingReportSha256,
approval, selection, definition, revoke, supersedes
```

Types and rules:

- All `*Revision` fields are positive integers; `exportId`, `mappingReportId` and `sourceDocumentId` are stable lowercase dotted IDs.
- All `*Sha256` fields are uppercase 64-character hexadecimal strings.
- `approval` is exactly `{workbench, awake}`. Each entry is `{status, actorId, approvedAt, revision, evidenceId}`; Workbench status is `approved|not_approved`, AWAKE status is `approved|not_approved`. `approvedAt` is an RFC3339 UTC timestamp and `actorId/evidenceId` are non-empty stable IDs.
- `selection` is exactly `{characterId, identityId, role, scope, priority}`. `characterId` and `identityId` use `WorldbookEntityId.Canonical(kind,value)` output; empty canonical output rejects. `role` is normalized lowercase and non-empty; `scope` is `character|identity|role|culture|global`; `priority` is an integer `0..100000`.
- `definition` is an `awake.persona.definition.v1` object. Its repeated selection fields must equal `selection` after canonicalization; otherwise reject. The envelope selection is the sole matching authority.
- `revoke` is `null` or `{reason, actorId, occurredAt, replacementRequired}`. `supersedes` is `null` or an earlier export ID. A revoke without a valid replacement keeps the last-known-good runtime bundle.
- Promotion requires Workbench approval, complete authoring-v2 migration, complete mapping report, registry digest match and explicit AWAKE approval. Workbench approval never implies AWAKE approval.

## 5. Crosswalk and loss/provenance closure

The crosswalk artifact is `docs/persona-contract/persona-workbench-to-awake.crosswalk.v1.json`, pinned to a Persona registry SHA-256. It has one row for every current Workbench tag, every 25 axis selector, both boolean flags, every facet-strength field, every legacy trigger/boundary field and every provenance field affecting runtime output.

Each row is exactly:

```text
sourceId, sourceKind, targetField, targetId, mappingKind,
valueEncoding, lossPolicy, provenancePath, registrySha256, status
```

- `mappingKind` is `identity|axis_to_observation|flag_to_observation|facet_to_observation|legacy_token|preserve_only|unmapped`.
- `valueEncoding` is explicit. Axes use `-2,-1,0,1,2`; `null` emits nothing and `0` emits the registered balanced token. Facets use `1=slight,2=moderate,3=strong`; value `4` maps to `strong` with `legacy_strength_collapsed` warning. Boolean flags use `false=no observation`, `true=one registered observation`.
- `lossPolicy` is `reject|preserve_only|drop_non_runtime_metadata`. `preserve_only` is legal only in migration; `reject` is mandatory for runtime meaning without a target encoding, missing provenance, unknown target ID or registry mismatch.
- `status=unmapped` or a non-runtime loss in an export candidate blocks promotion. Persona registry digest and Worldbook registry/package digests are separate fields.
- Closure test compares the artifact's source vocabulary against the pinned Workbench vocabulary and AWAKE registry; missing or duplicate rows fail.

## 6. Transition records and idempotency

Every state transition writes an immutable `awake.persona.transition.v1` record:

```text
transitionId, artifactId, fromStatus, toStatus, actorId, occurredAt,
inputRevision, outputRevision, idempotencyKey, sourceSha256,
authoringSha256, mappingReportSha256, registrySha256, result, reason
```

`result` is `applied|duplicate|rejected|revoked`. The idempotency key is `artifactId|fromRevision|toStatus|actorId`. Replaying the same key returns `duplicate` without a second write; concurrent transitions on the same input revision allow one `applied` and return `rejected_stale_revision` for the other. Revocation without replacement returns `rejected_replacement_required` and leaves the selected bundle unchanged.

## 7. Runtime authority, identity and failure semantics

Production Persona selection has one facade: `PersonaRuntimeProvider.BuildProjection(ContextSnapshot)`. The forbidden production caller set is:

```text
WorldbookRuntime.Current.BuildPersona
PersonaDslGenerator.GenerateLegacy
PersonaDslGenerator.BuildLegacyFallback
KnowledgeRuntime.EnsureCreated for Persona selection
KnowledgeRuntime.Current for Persona selection
WorldbookRuntime.Knowledge for Persona selection
```

The static scan must search actual symbols and call sites, not only these names; it must include `PersonaDslGenerator.Generate` paths that call the fallback and direct `WorldbookRuntime.Current`/`KnowledgeRuntime.Current` reads.

`RuntimeBundle` is an immutable DTO with defensive copies of all lists/dictionaries. It contains worldbook package/manifest/runtime/index activation, knowledge snapshot, persona snapshot, selection revision, all Persona/Worldbook digests and BuildId. Candidate load constructs a complete bundle off-thread/side-by-side, validates it, then publishes one reference with `Interlocked.Exchange`; prompt builds read one local reference and never combine versions. A failed reload never clears the old bundle or mutates activation metadata.

First-load failure returns a fixed identity-only DSL:

```text
[PERSONA_LOAD]
STATUS=RUNTIME_FALLBACK
REASON=<stable error code>
[PERSONA_IDENTITY]
CHARACTER_ID=<canonical identity or UNKNOWN>
IDENTITY_ID=<canonical identity or UNKNOWN>
ROLE=<normalized role or UNKNOWN>
```

The fallback contains exactly five key-value lines (`STATUS`, `REASON`, `CHARACTER_ID`, `IDENTITY_ID`, `ROLE`); identity/role values may be canonical values or `UNKNOWN`, while `REASON` is always a stable error code. It must not contain Workbench text, legacy personality/background, any definition prose, unmapped fields or unapproved tags. Fixtures compare exact bytes and scan forbidden source markers. `RUNTIME_FALLBACK` is observable and is never an approved Persona.

## 8. ContextSnapshot and fingerprint

`ContextSnapshot` is produced once per prompt build by the dialogue context owner and consumed by knowledge, Persona projection and prompt assembly. Fields are typed and normalized as follows:

| Field group | Type/default | Normalizer | Fingerprint inclusion |
|---|---|---|---|
| `campaignId,timelineId,branchId` | stable string / `UNKNOWN` | `WorldbookEntityId.Canonical` where applicable | yes |
| `sequence` | non-negative `long`, monotonic per dialogue context | no coercion | yes |
| `characterId,identityId` | canonical string / `UNKNOWN` | canonical ID resolver | yes |
| `normalizedRole` | lowercase non-empty / `UNKNOWN` | trim, lowercase, reject control chars | yes |
| hero/culture/kingdom/clan fields | string / empty | NFC + trim | yes |
| relation/currentState/memoryHint | string / empty | NFC + trim, no authored inference | yes |
| sceneKeywords/contextModes | ordinal-sorted unique string arrays | NFC + trim | yes |
| continuity/playerOverride | immutable DTO + revision | defensive copy; null becomes empty DTO | yes |
| `worldbookRevision,personaRevision` | positive integer / `0` only for fallback | no coercion | yes |

Fingerprint formula:

```text
fingerprint = UPPER_HEX(SHA256(UTF8(CanonicalJson(NormalizedContextSnapshot))))
```

`CanonicalJson` sorts object properties ordinally, sorts semantically unordered arrays by stable ID/token, preserves ordered arrays by index, represents missing/unknown distinctly from empty, uses NFC strings, invariant numeric formatting and no insignificant whitespace. A mutation fixture changes one field at a time and must change the fingerprint or produce the exact documented no-op for a field not rendered into the DSL.

The identity resolver is the only producer of `snapshot.identityId`; knowledge, Persona and prompt must assert the same value. Unknown/unnamed heroes use `UNKNOWN` and identity-only fallback; display names never become stable IDs.

## 9. Candidate ledger and hash/BuildId contract

`docs/persona-awake-candidate-ledger.v1.json` is the only attribution authority. Root fields are:

```text
schemaVersion, ledgerId, status, activeCandidateId, evidenceCeiling,
candidates, reconciliationRules
```

Each candidate has:

```text
candidateId, buildId, version, status, evidenceLevel, claimedEvidenceLevel,
createdAt, supersedes, sourceRoot, distRoot, gameRoot,
sourceSha256, distSha256, gameSha256,
dllSha256:{buildOut,source,dist,game},
worldbookManifestSha256, worldbookRuntimeSha256, worldbookIndexSha256,
personaDefinitionSha256, personaRegistrySha256, mappingReportSha256,
observations, conflicts, notes
```

`sourceSha256/distSha256/gameSha256` are normalized tree digests. Tree digest sorts relative paths ordinally and hashes `relativePath + NUL + fileBytesSha256 + LF` for every allowlisted file. `dllSha256` is an artifact map, not a tree digest. BuildId is a label bound to the complete tuple, not a hash substitute. A candidate's effective `evidenceLevel` is `min(claimedEvidenceLevel, root.evidenceCeiling)`; unresolved/contradictory rows are `blocked` even if a historical document claimed E3.

A current ledger conflict sets root `status=needs_reconcile`, `activeCandidateId=null`, and hard-blocks E3/E4/E5. Frozen candidates are never edited in place; a new runtime bridge always gets a new BuildId and `supersedes` value.

## 10. Fixed fixtures and report contract

Fixtures live under `docs/fixtures/persona-awake-joint/<fixtureId>/`. Each contains `input.json`, optional `expected.json`, `expected-error.txt`, `manifest.sha256.txt`, `README.md`. The runner is `tools/persona-awake-joint/run-fixtures.ps1`; it writes `tools/persona-awake-joint/artifacts/<fixtureId>.json` and returns status/exit code:

| Fixture | Gate | Required result |
|---|---|---|
| `PWB-AWAKE-001-valid-approved` | E2 | exact v1→authoring-v2→export-v1→definition-v1 output and selection |
| `PWB-AWAKE-002-workbench-draft-rejected` | E2 | no promotion and source unchanged |
| `PWB-AWAKE-003-missing-selection-rejected` | E2 | stable missing-field error |
| `PWB-AWAKE-004-unknown-tag-rejected` | E2 | no silent loss |
| `PWB-AWAKE-005-loss-report-rejected` | E2 | runtime-meaning loss/provenance/digest mismatch rejected |
| `PWB-AWAKE-006-schema-skew-rejected` | E2 | unknown root/nested field and v1 runtime manifest rejected |
| `PWB-AWAKE-007-duplicate-and-path-rejected` | E2 | duplicate ID/path/reparse/partial export rejected |
| `PWB-AWAKE-008-last-known-good` | E2 | old RuntimeBundle and activation metadata unchanged after bad reload |
| `PWB-AWAKE-009-v1-runtime-rejection` | E2 | v1 runtime entry is rejected with stable schema error; no automatic v1→v2 runtime migration |
| `PWB-AWAKE-010-runtime-caller` | owner lease required | blocked/not_attempted before Native approval; later proves facade-only caller and exact fallback |
| `PWB-AWAKE-011-dynamic-invalidation` | owner lease required | blocked/not_attempted before Native approval; later proves one-field fingerprint mutation |
| `PWB-AWAKE-012-persistence` | owner lease required | blocked/not_attempted before Storage owner/lease; later proves save/load/branch/restart |
| `PWB-AWAKE-013-frozen-candidate-isolation` | E2 | attempted overwrite/delete/pointer mutation rejected; frozen root hashes unchanged |
| `PWB-AWAKE-014-missing-approval-rejected` | E2 | approved source without distinct approval evidence is rejected |
| `PWB-AWAKE-015-stale-selection-rejected` | E2 | selection source revision mismatch is rejected |
| `PWB-AWAKE-016-unsupported-rule-rejected` | E2 | enabled rule not representable in definition-v1 is rejected |
| `PWB-AWAKE-017-selection-schema-rejected` | E2 | selection without schemaVersion is rejected |

Every report has `schemaVersion=awake.persona.fixture-report.v1`, `fixtureId`, command line, normalized cwd, input/output hash arrays, a nullable `handoff` object, exit code, `status=pass|reject|blocked|not_attempted|error`, named assertions, observed errors and cleanup object. A successful migration report must expose source/authoring/selection revisions, export selection revision, all handoff digests and both approval evidence IDs. `010–012` cannot be `pass` while Native review/lease is absent.

## 11. Exact command and prerequisite contract

Existing build command is:

```text
pwsh -NoProfile -File tools/build.ps1 -BannerlordApi 1.3.15 -Configuration Release -SkipGameVersionCheck
```

It is not allowed to invent `-SkipGame`. The joint runner/wrappers that produce JSON reports are implementation deliverables:

```text
pwsh -NoProfile -File tools/persona-awake-joint/run-fixtures.ps1 -FixtureId <id> -ReportPath <path>
pwsh -NoProfile -File tools/persona-awake-joint/verify-contract.ps1 -ReportPath <path>
pwsh -NoProfile -File tools/persona-awake-joint/verify-old-entry-scan.ps1 -ReportPath <path>
pwsh -NoProfile -File tools/persona-awake-joint/verify-candidate-ledger.ps1 -ReportPath <path>
pwsh -NoProfile -File tools/persona-awake-joint/verify-runtime-bridge-static.ps1 -ReportPath <path>
pwsh -NoProfile -File tools/persona-awake-joint/verify-runtime-static-evidence.ps1 -InputReportPath <path> -ReportPath <path> -Case auto
pwsh -NoProfile -File tools/persona-awake-joint/verify-native-prerequisite.ps1 -ReportPath <path>
pwsh -NoProfile -File tools/persona-awake-joint/verify-g3-s0-scope.ps1 -ReportPath <path>
pwsh -NoProfile -File tools/persona-awake-joint/verify-g3-s0-focused-evidence.ps1 -InputReportPath <path> -ReportPath <path>
pwsh -NoProfile -File tools/persona-awake-joint/verify-g3-plan.ps1 -ReportPath <path>
pwsh -NoProfile -File tools/persona-awake-joint/verify-e2-matrix.ps1 -ReportPath <path>
```

`verify-runtime-bridge-static.ps1` must also report Persona persistence model presence, external runtime wiring, continuity/override/recovery schema registration, and the existing Overlay persistence path. A blocking static reject must expose one `awake.persona.adapter-error.v1` entry per failed blocking check in `observedErrors`; the `checks` array remains the detailed diagnostic source. `verify-runtime-static-evidence.ps1` consumes the final JSON report and returns `pass/0` only when the fixed check set, failed-blocking-to-observed-error mapping, and status/exit pair are internally consistent; the target report may legitimately remain `reject/10`.

`verify-g3-s0-scope.ps1` is the authoritative checker for `docs/persona-awake-joint-g3-s0-scope.v1.json`, but the manifest is not an approval authority. Detached `persona-awake-joint-g3-s0-approval.v1.json` and `persona-awake-joint-g3-s0-lease.v1.json` records must bind to the manifest's raw SHA-256, task identity and exact write set; the verifier requires independent review evidence, user signoff and one active disjoint lease from those records. `pending_approval` or an unleased record returns `blocked/20` and never self-authorizes source writes. Its declared six-file write set must remain disjoint from the isolated tooling set and all protected runtime/candidate roots.

`verify-native-prerequisite.ps1` must consume the G3-S0 scope result and the current scope-bound focused report/trace. Legacy Native plan, checkpoint and Storage documents may be reported as optional diagnostics but are not readiness authority. It returns `status=pass` only when both child verifiers exit `0`, both child reports are `pass/0`, the Persona scope is exactly approved, has one matching active lease, has a disjoint write set and the focused trace independently proves Storage readiness and Worldbook characterization; B1-only approval without that evidence is insufficient. Until it returns `status=pass`, shared runtime/caller/storage fixtures are `blocked` and no implementation lease is granted.

`verify-g3-plan.ps1` checks the corrected `G3-S0 -> G3-A -> G3-B -> G3-C -> G4` graph, concrete runtime write paths, readiness/persistence separation, cross-document gate references and the explicit review-pending/blocking status. Its `pass/0` only proves the plan is internally coherent; it never grants an execution lease or authorizes `AWAKE/src` writes.

`verify-e2-matrix.ps1` runs every E2 fixture and aggregates expected `pass/reject/blocked` results. `PWB-AWAKE-013-frozen-candidate-isolation` is special: if its frozen source baseline differs from the observed protected source tree, the report must classify it as `protected_baseline_drift` with a warning, not rewrite the frozen fixture or misreport the drift as a new feature regression. Matrix `pass/0` means all fixture outcomes match their declared policy; it does not raise the evidence ceiling.

E0–E2 run only in source/isolated candidate roots. E3 needs a reconciled ledger, a new BuildId, release-prep approval and explicit sync authorization. E4/E5 need user-run game evidence with matching ledger tuple. Current stage is E2 ceiling; E3–E5 are `blocked` or `not_attempted`.

## 12. Frozen-candidate and write-set boundary

`PWB-AWAKE-013` records before/after hashes for every frozen candidate root and attempts only in an isolated copy; it must never mutate the real frozen root. Any path equal to, under, or an ancestor of a protected root is rejected, including junction/reparse resolution.

Before Native approval/lease, permitted write set is limited to:

```text
AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-20260823.md
AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-20260823-REVIEW-LOG.md
AWAKE/docs/PLAN-PersonaWorkbench-AWAKE-Joint-CONTRACT-LOCK-20260823.md
AWAKE/docs/persona-contract/* (contract artifacts only)
AWAKE/docs/fixtures/persona-awake-joint/* (isolated fixtures only)
AWAKE/docs/persona-awake-candidate-ledger.v1.draft.json
AWAKE/docs/persona-awake-joint-g3-s0-scope.v1.json
AWAKE/docs/persona-awake-joint-g3-s0-approval.v1.json
AWAKE/docs/persona-awake-joint-g3-s0-lease.v1.json
```

AWAKE/tools/persona-awake-joint/* (isolated offline runner, adapters, verifiers and reports only)

No source/runtime/storage/UI/ModuleData/dist/game/frozen-candidate files are in this pre-lease write set.

## 13. Final normative closures (supersede shorthand above)

### 13.1 Ledger authority

`docs/persona-awake-candidate-ledger.v1.json` is the sole ledger authority. `persona-awake-candidate-ledger.v1.draft.json` is a non-authoritative editable staging copy; tools must ignore it and a promotion check must reject the pair unless the two files are byte-identical. The canonical file currently has `status=needs_reconcile`, `activeCandidateId=null`, and `evidenceCeiling=E2`.

### 13.2 Selection and revision input closure

Workbench-v1 has no stable game selection fields. The adapter therefore requires an explicit sidecar/request object `awake.persona.selection.v1` and never derives selection from display name, filename, mtime or ordinary Workbench `Id`:

```text
schemaVersion, characterId, identityId, role, scope, priority,
sourceRevision, selectionRevision, suppliedBy, suppliedAt
```

- `characterId` and `identityId` must be canonical outputs of `WorldbookEntityId.Canonical`; empty output rejects with `persona.selection_invalid`.
- `role` is lowercase and non-empty; `scope` is `character|identity|role|culture|global`; `priority` is `0..100000`.
- `sourceRevision` is the explicit Workbench document-store/CAS revision. Direct-file adapters must receive it in the sidecar; they may not infer it from timestamps.
- `selectionRevision` is a positive integer in the export root and digest tuple; it is not part of the five-field runtime matching object `selection`.
- `suppliedBy` and `suppliedAt` are evidence metadata; missing values reject. The sidecar is an input artifact, not an automatic approval.
- `authoringRevision` is a required positive integer stored in `migration.authoringRevision` and repeated in the export root; it starts at 1 for a source hash and increases only when source bytes, mapping configuration or pinned registry changes. Same input tuple must reproduce the same revision and output.

### 13.3 Runtime definition and registry target closure

`awake.persona.definition.v1` has `additionalProperties=false` and exactly these root fields:

```text
schemaVersion, id, characterId, identityId, role, sourcePackId,
templateVersion, status, priority, scope, core, identityFacts,
relationStyle, currentStateHints, summary, publicDescription,
privateDescription, contradictionDescription, foodPreference,
selfClaimRules, realSelfBehaviors, selfClaimExamples, tags,
bundles, experiences
```

`tags[]` is exactly `{id, priority, sceneKeywords, contextModes}`. `experiences[]` is exactly `{id, text, status, source, priority}`. Runtime definition status is `approved|draft|disabled`; only `approved` is selectable. `authoring.authored` prose maps to the matching definition prose fields; accepted observation rows map through the target registry to `tags[]`; disabled/rejected observations are omitted. Enabled rules with no representable `definition.v1` field reject with `persona.rule_unsupported_for_definition_v1` rather than being silently flattened.

The target Persona registry is the existing AWAKE shape, not the K1 selector registry shape:

```text
{ schemaVersion: "awake.persona.tags.v1", tags: [{id, category, displayName, meaning, promptText}], bundles: [{id, displayName, tags}] }
```

Crosswalk generation must validate this exact target shape and pin its raw registry bytes. The K1 selector registry is an authoring/compiler input and cannot be passed directly to AWAKE runtime.

### 13.4 Approval/promotion closure

A Workbench document can enter export only when:

1. Workbench root status is `approved`;
2. explicit selection sidecar is valid;
3. all runtime-relevant migrated observations are `accepted` and enabled;
4. no enabled rule is unsupported by definition-v1;
5. authoring, source, selection, registry and mapping digests match; and
6. export approval records a distinct AWAKE approval event.

Any migrated `needs_review` observation, stale approval revision, missing evidence or unsupported rule returns a stable rejection and writes no candidate. Thus `draft/approved` at the Workbench layer cannot falsely promote an unreviewed migration.

### 13.5 Canonical bytes and report schemas

All new Persona JSON artifacts use `persona-canonical-json.v1`: UTF-8 without BOM, NFC strings, LF line endings, final LF, invariant JSON numbers, no insignificant whitespace, object properties sorted by ordinal name, and arrays preserved in declared order unless the schema marks them as a set (then sort by the declared stable ID). The digest excludes detached digest fields. The same function is used for authoring-v2, selection-v1, export-v1, definition-v1, crosswalk-v1, mapping-report-v1, adapter-error-v1 and candidate-ledger-v1.

`awake.persona.mapping-report.v1` is exactly `{schemaVersion, reportId, sourceSha256, authoringSha256, targetRegistrySha256, rows, warnings, errors, status}`. Each row is the crosswalk row plus `sourcePath`, `targetPath`, `emittedValue`, `lossPolicy`, `provenancePath`, `result=emitted|preserved|rejected`. `status` is `pass|reject`.

`awake.persona.adapter-error.v1` is exactly `{schemaVersion, errorId, code, stage, artifactId, path, detail, retryable}`. Error codes used by this batch include `persona.selection_invalid`, `persona.source_revision_required`, `persona.authoring_observation_unreviewed`, `persona.rule_unsupported_for_definition_v1`, `persona.registry_digest_mismatch`, `persona.mapping_loss`, `persona.schema_unknown_field`, `persona.stale_revision`, `persona.path_protected`, `persona.runtime_bridge_check_failed`, `persona.runtime_static_evidence_invalid` and `persona.runtime_static_case_not_supported`.

### 13.6 Worldbook v2 index/hash closure

`awake.worldbook.index.v1` is a standalone object with `additionalProperties=false`, required `schemaVersion` and `entries`, where `entries` is a unique array of stable IDs. It is distinct from the embedded `runtime.indexes` object. Package hashes follow the existing `tools/worldbook-contract/v1/golden-hashes.json` procedure exactly: sorted-object-keys/no-whitespace canonical JSON, slash-and-case-folded path sort, and `packageHash = raw32(manifestDigest) || raw32(contentDigest)` hashed with SHA-256. The package manifest `hashes` fields must equal independently recomputed manifest/content/package hashes before runtime publication.

### 13.7 Fallback byte closure

The fallback DSL has five key-value lines; section headers are not keys. Its exact grammar is:

```text
[PERSONA_LOAD]
STATUS=RUNTIME_FALLBACK
REASON=<stable error code>
[PERSONA_IDENTITY]
CHARACTER_ID=<canonical ID or UNKNOWN>
IDENTITY_ID=<canonical ID or UNKNOWN>
ROLE=<normalized role or UNKNOWN>
```

`REASON` is a stable error code, while the three identity values may be `UNKNOWN`; it is not required to equal `UNKNOWN`. The diagnostic envelope (owner, correlation ID, BuildId, revision, digests) is logged/report data and is not inserted into this DSL. Fixtures compare these exact lines, reject any authored prose/tag token, and distinguish fallback from approved output by `STATUS`.

## 14. User-accepted gate corrections

The continuation instruction accepts the controller recommendations from the MAX_ROUNDS deadlock:

- `tools/persona-awake-joint/*` is an allowed isolated pre-lease write set for offline tooling and reports. It cannot edit `src`, `ModuleData`, runtime, dist, game, or frozen roots and cannot self-authorize a lease.
- `PWB-AWAKE-009-v1-runtime-rejection` tests only that a v1 runtime entry is rejected. v1→v2 conversion, if ever needed, is a separate offline migration tool and is not part of the runtime contract.
- Export root explicitly carries `selectionRevision` and `authoringRevision`; `selection` remains the five-field matching object; `migration` explicitly carries `authoringRevision`.
- Status/exit mapping is fixed: `pass=0`, `reject=10`, `blocked=20`, `not_attempted=30`, `error=40`.
- Native readiness requires exact approved scope plus a unique active lease with a disjoint write set. `approved_for_b1_only` and `execution_lease=none` remain blocked.
- `ContextSnapshot` must distinguish `sourceHeroId`, `targetHeroId`, `characterId` and `personaIdentityId`, and include `sessionGeneration`, `captureToken`, `activeBundleId`, `buildId` and all selected bundle digests in the fingerprint input.
- Frozen-candidate isolation hashes a read-only snapshot of protected roots, performs destructive attempts only in a sibling temporary copy, and compares the real protected roots before/after.



