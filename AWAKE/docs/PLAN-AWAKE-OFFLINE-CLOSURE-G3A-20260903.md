# AWAKE Offline Closure G3-A — 2026-09-03

## Batch identity

- batch_id: `AWAKE-OFFLINE-CLOSURE-G3A-20260903`
- task_id: `AWAKE-OFFLINE-CLOSURE-G3A-20260903`
- risk_class: `high-risk`
- current baseline: `awake-20260903-awake-runtime-repair-004`
- engineering version: `v0.2.0`
- target candidate: new BuildId after implementation; do not modify or relabel `004`
- review_state: `docs/review-state/AWAKE-OFFLINE-CLOSURE-G3A-20260903.json`
- review_log: same JSON state file; all review rounds use this file

## Objective

Close three separately gated offline slices in order: `G3A-1` Persona contract validation and one-character selection/recovery path; `G3B-1` Worldbook Studio v2 compile/query/permission fixtures without claiming current AWAKE Runtime consumption; and `G3C-1` governed settlement and recovery. A full Persona-to-NPC context projection is explicitly deferred until its contract is separately approved.

## Confirmed baseline

- AWAKE Runtime `004` has offline source/build/smoke evidence but no current BuildId E4/E5 evidence.
- Current `ModuleData\\Worldbook` is a legacy transitional input and is not the final Worldbook Studio format.
- Persona Workbench crosswalk and schema contracts exist:
  - `persona-workbench.character.v1`
  - `awake.persona.authoring.v2`
  - `awake.persona.definition.v1`
  - `awake.persona.selection.v1`
- `awake.persona.state` Storage readiness is covered by the prior G3-S0 focused batch.
- Persona runtime projection, full `ContextSnapshot`, complete `RuntimeBundle` payload, full persistence/recovery, and NPC prompt consumption are not yet closed.
- Existing Persona fixture schema only defines bundle activation metadata (`bundleId`, `buildId`, revisions, activation); it is not evidence of a complete prompt context payload.
- Worldbook Studio authoring v1 is the content-format authority; old `knowledge/rules` remains migration input only.
- Studio Runtime Mapping Contract currently produces an `awake.worldbook.v2` candidate and does not authorize copying it into AWAKE `ModuleData` or claiming current v1 Runtime consumption.

## Scope

### In scope

1. Persona source-to-runtime projection for a bounded fixture set of 10–20 characters.
2. Stable character/entity/profile ID validation and registry binding.
3. `ContextSnapshot` / `RuntimeBundle` construction for NPC dialogue.
4. Persona context consumption by the existing AWAKE NPC dialogue path.
5. Representative legacy Worldbook migration pilot of 20–50 files in one topic domain.
6. Studio authoring v1 candidate generation for the pilot, including assertions, expressions, sources or `author_created`, grants/denies, and `needs_review` results.
7. Joint Persona + Worldbook query/preview fixture.
8. Fake Provider structured dialogue completion.
9. Governed memory, relationship, and event settlement.
10. Existing Storage-compatible duplicate, retryable, unknown, cancellation, stale-session, and restart-style fixtures.
11. Offline observability and diagnostics sufficient to identify source, identity, permission, route, settlement, and failure state.

### Sub-batch order

#### G3A-1 — One Persona selection and recovery contract

- Initial fixture cardinality: exactly 1 character, 1 effective definition, 1 selection, 1 runtime bundle activation record, and 1 recovery round trip. No claim of full prompt context projection.
- Required negative fixtures: invalid ID, missing registry, stale revision, revoked export/selection, and no-Persona fallback (`5/5`).
- Existing fixture contract authority: `awake.persona.fixture-input.v1.schema.json`, `awake.persona.definition.v1.schema.json`, and `awake.persona.selection.v1.schema.json`. The existing `runtimeBundle` object is activation metadata only.
- Do not invent a new `ContextSnapshot` or expand `RuntimeBundle` in this batch. A full prompt-context DTO requires a separate reviewed contract batch.
- Effective Persona definition rule: for a `(characterId, runtime context)`, choose the highest matching scope, then highest priority, then highest revision; equal scope/priority/revision with different content fails closed. `export` approval/revoke/supersedes and `selection` revisions must all be consistent before selection.
- ID authority matrix: Persona `characterId`/`identityId` maps through an explicit fixture table to Worldbook `entity.*` and `profile.*`; unknown or hash-mismatched mappings fail closed. No implicit name-based mapping.
- Write scope after approval: AWAKE Persona projection/context code and focused tests only.
- No Worldbook v2 candidate is injected into the current AWAKE Runtime in this sub-batch. NPC prompt consumption remains an explicit deferred boundary, not a pass claim.
- Exit command set:
  - `dotnet build ..\\AWAKE.Tests\\AWAKE.Tests.csproj -c Release --nologo -v:minimal`
  - named Persona selection/recovery cases: `1/1` positive and `5/5` negative
  - Persona schema and crosswalk validation

#### G3B-1 — Legacy Worldbook pilot as Studio v2 compile/query fixture

- Initial fixture cardinality: 1 topic domain, 1–5 legacy files, and no claim that the v2 candidate is consumed by current AWAKE `ModuleData`.
- Expansion gate: only after the first fixture is valid may the pilot expand to 20–50 files.
- Required outputs: source snapshot, source unit records, ID ledger candidates, duplicate/conflict/entity/loss reports, Studio authoring v1 candidates, and `needs_review`.
- Every document/assertion/expression provenance reference must include source ID/version/content hash/locator/quote hash, or be explicitly `author_created`.
- Worldbook permission fixtures must include the six current contract golden cases, including deny-over-grant, scope/detail, empty grants/denies unknown, unknown profile compile block, and diagnostics-only anonymous profile.
- This sub-batch ends at Studio v2 compile/query/permission evidence. A v2→AWAKE Runtime adapter is explicitly out of scope and would require a separate reviewed contract.
- Write scope after approval: only AWAKE `docs\\worldbook-migration\\` artifacts and offline fixtures; never Studio/PWB/UI Workstation source, AWAKE `ModuleData`, `src`, `dist`, PlayerExports, or game files.

#### G3C-1 — Governed settlement and recovery

- Initial fixture cardinality: 1 dialogue result, 1 memory write, 1 relationship change, and 1 event record.
- Expansion gate: only after the single vertical slice passes may the matrix expand to valid, invalid, duplicate, retryable, unknown, cancelled, timed-out, stale-session, restart-style, and final-drain cases.
- Required final observable object: a golden settlement receipt containing route, request ID, context hash, command ID, preflight result, permission result, settlement outcome, memory/relationship/event deltas, and persistence outcome.
- Exit command set:
  - `Awake.SdkSmoke.exe`
  - Worldbook Runtime Production Smoke
  - storage/command/provider regression harnesses
  - save-compatible fixture round trips

### Out of scope

- Worldbook Studio source or UI changes.
- Persona Workbench source or UI changes.
- UI Workstation changes.
- Final full-worldbook migration.
- Final content canon decisions for unresolved legacy conflicts.
- New public Framework APIs unless a separate reviewed contract is approved.
- Save-key or Saveable ID changes.
- Bannerlord startup or gameplay verification.
- Game-directory, `dist`, PlayerExports, or release-package synchronization.
- Real Provider, API Key, Worker, network, or external service access.
- Adult-content expansion; adult material remains isolated and unresolved material goes to review.

## Authority boundaries

- Host game state is authoritative for live game facts.
- Persona Workbench source and approved Persona contracts are authoritative for role-source semantics.
- Worldbook Studio authoring v1 is authoritative for new content structure.
- AWAKE Runtime owns projection, context assembly, command governance, and observable game-facing behavior.
- AI output is untrusted intent/data; it cannot directly mutate game or Storage state.
- Existing save keys and schemas remain authoritative.

## Acceptance contract

### A. Persona projection

- A valid Workbench character maps to exactly one stable AWAKE runtime definition.
- Missing registry, stale revision, invalid ID, revoked selection, or conflicting definition fails closed.
- Public/private/contradictory behavior remains distinguishable after projection.
- Projection is bounded by explicit byte/token limits.

### B. Worldbook pilot

- Legacy inputs are snapshotted before transformation.
- Each pilot output is a Studio authoring v1 candidate, not an automatic canon.
- Every assertion has `sources` or `author_created`, never both.
- Uncertain facts, conflicts, missing IDs, and unmapped fields enter `needs_review` or a loss report.
- No legacy file is overwritten.
- Base and optional adult content remain separated.

### C. Joint context

- Full Persona + Worldbook + memory + relationship + scene + PlayerKnown context assembly is deferred until a separate ContextSnapshot contract is approved.
- This batch may validate only the existing, explicitly named input boundaries; it must not claim full joint context closure.
- Deny overrides grant.
- Hidden source text, private content, and author diagnostics do not leak into NPC preview or unauthorized prompt context.
- Duplicate context entries are removed deterministically.

### D. Dialogue and settlement

- Entry is reachable through the existing NPC dialogue caller.
- Fake Provider returns a valid structured response.
- Invalid, cancelled, expired, timed-out, or stale responses produce no unauthorized effect.
- Effects go through command/preflight/permission/idempotency governance.
- Memory, relationship, and event changes settle through Storage and produce an observable offline result.
- The first acceptance case is exactly one deterministic golden settlement receipt; cardinality expansion requires a separate recorded gate.

### E. Persistence and recovery

- Duplicate submission does not double-apply.
- Retryable and Unknown outcomes are not reported as Applied.
- Session N cannot mutate session N+1.
- Persona continuity/override/recovery fixtures survive reconstruction.
- Existing keys/defaults remain compatible.
- Restart-style fixtures recover conservatively without treating uncertain writes as applied.
- Continuity/override/recovery round trips must name the actual existing namespace/key, old default, selected revision, registry hash, and post-load reference rebuild result.

## Implementation order after approval

1. Freeze fixture inputs and write source/registry fingerprints.
2. Implement Persona projection and runtime bundle using existing contracts.
3. Add focused Persona projection and stale/revision tests.
4. Wire the existing NPC dialogue prompt path to the runtime bundle.
5. Add joint Persona + Worldbook context fixtures.
6. Implement the bounded legacy Worldbook migration pilot outside source/runtime directories.
7. Add compile/query/permission fixtures for the pilot.
8. Close dialogue-to-memory/relationship/event settlement fixtures.
9. Add persistence/restart/duplicate/cancel/timeout/stale-session regression fixtures.
10. Run focused tests, full AWAKE Smoke, Worldbook Runtime Smoke, production Smoke, schema checks, and static contracts.
11. Assign a new BuildId and record source-only evidence.
12. Stop before sync or gameplay validation.

## Required evidence

- E0: this plan, review state, source/contract authority map.
- E1: schema/JSON parse, static contracts, Release build.
- E2: focused Persona, joint context, migration, settlement, and recovery fixtures; full offline Smoke.
- E3: not claimed in this batch unless an explicitly authorized synchronization batch is later executed.
- E4/E5: not attempted; require user-run Bannerlord logs and matching BuildId.

## Explicit acceptance-to-evidence map

| Acceptance | Required fixture or static evidence | Minimum result |
|---|---|---:|
| Persona valid projection | G3A-1 valid-character fixture | 1/1 |
| Persona fail closed | invalid ID, missing registry, stale revision, revoked selection | 4/4 |
| Persona selection/recovery | named G3A-1 fixtures plus Persona schema validation | 1/1 positive + 5/5 negative |
| Full context reaches NPC path | separate ContextSnapshot contract and named joint caller | deferred, not claimed |
| Worldbook migration traceability | source snapshot, inventory, loss, duplicate, conflict reports | all files hash-bound |
| Studio authoring validity | authoring v1 schema validator | 100% pilot documents valid |
| Permission isolation | grant/deny, profile, detail-layer fixtures | 100% expected outcomes |
| Structured dialogue | Fake Provider valid/invalid/timeout/cancel fixtures | all expected outcomes |
| Governed settlement | memory, relationship, event command fixtures | no unauthorized mutation |
| Persistence truthfulness | applied, duplicate, retryable, unknown, hard-failure fixtures | no false Applied |
| Session isolation | session N/N+1 stale completion fixtures | 0 stale writes |
| Recovery | restart/final-drain/old-default fixtures | all expected recoveries |

## Stop conditions

Stop and split a new batch if implementation requires:

- changing Studio schema or Persona schema;
- changing save keys, Saveable IDs, route IDs, or public Framework contracts;
- modifying Worldbook Studio, Persona Workbench, or UI Workstation;
- deciding unresolved canon conflicts automatically;
- changing content-package ownership or adult-content policy;
- writing to `dist`, game modules, PlayerExports, or release pointers;
- a new P0/P1 finding outside this acceptance contract.

## Deliverables

- `docs/worldbook-migration/SOURCE-SNAPSHOT-*.json`
- `docs/worldbook-migration/SOURCE-INVENTORY-*.json`
- `docs/worldbook-migration/DUPLICATE-REPORT-*.json`
- `docs/worldbook-migration/CONFLICT-REPORT-*.json`
- `docs/worldbook-migration/ENTITY-CANDIDATE-REPORT-*.json`
- `docs/worldbook-migration/MIGRATION-LOSS-REPORT-*.json`
- `docs/worldbook-migration/NEEDS-REVIEW-*.json`
- `docs/worldbook-migration/ID-LEDGER-*.json`
- `docs/worldbook-migration/MIGRATION-EVENT-*.json`
- Persona projection fixtures and joint context fixtures in the existing offline test scope.
- A new checkpoint with BuildId, hashes, commands, pass/fail counts, and unverified boundaries.

All migration snapshots, reports, fixtures, and candidate authoring documents must remain under:

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\worldbook-migration\`

They must not be written to Worldbook Studio, Persona Workbench, UI Workstation, `src`, `ModuleData`, `GUI`, `dist`, PlayerExports, or the game directory.
