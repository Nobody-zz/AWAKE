# AWAKE Offline Closure G3-A Revision 2 — 2026-09-03

## Batch identity

- batch_id: `AWAKE-OFFLINE-CLOSURE-G3A-REV2-20260903`
- task_id: `AWAKE-OFFLINE-CLOSURE-G3A-REV2-20260903`
- risk_class: `high-risk`
- current baseline: `awake-20260903-awake-runtime-repair-004`
- engineering version: `v0.2.0`
- target candidate: new BuildId after implementation; do not modify or relabel `004`
- supersedes_plan: `docs/PLAN-AWAKE-OFFLINE-CLOSURE-G3A-20260903.md`
- review_state: `docs/review-state/AWAKE-OFFLINE-CLOSURE-G3A-REV2-20260903.json`
- review_log: same JSON state file; all review rounds use this file

## Objective

Close three separately gated offline slices in order: `G3A-1` existing Persona fixture validation; `G3B-1` report-only legacy Worldbook analysis against Studio v2 contracts; and `G3C-1` one existing AWAKE settlement/recovery smoke. Full Persona projection, `ContextSnapshot`, `RuntimeBundle` prompt payloads, Persona-to-NPC prompt consumption, and new cross-system adapters are deferred to separately reviewed contracts.

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

1. Validate existing Persona authoring/export/selection/definition fixtures; no new runtime projection is claimed.
2. Validate existing Persona IDs and existing tag-registry bindings only; no new cross-system registry binding is created.
3. Validation of existing runtime-bundle activation metadata only; full `ContextSnapshot` / `RuntimeBundle` prompt payload is deferred.
4. No Persona context consumption by the existing AWAKE NPC dialogue path is claimed in this revision.
5. Report-only analysis of 1–5 legacy Worldbook files in one topic domain; no authoring candidate is treated as compiled or Runtime-consumable.
6. Reuse existing Studio schema/permission fixtures where available; missing cases are reported as `not_available`, not invented.
7. Run one existing AWAKE fail-closed settlement Smoke; do not create a new settlement or recovery contract.
8. Record offline observability and diagnostics for the above existing runners.

### Sub-batch order

#### G3A-1 — Existing Persona fixture validation

- Initial fixture cardinality: existing supported fixtures only. No Persona recovery round trip is required because the current runner reports Persona persistence as `not_attempted`.
- Required negative fixtures: existing runner-supported rejection/fallback fixtures only; unsupported operations remain `not_attempted`.
- Existing fixture contract authority: `docs/persona-contract/awake.persona.fixture-input.v1.schema.json`, `docs/persona-contract/awake.persona.definition.v1.schema.json`, and `docs/persona-contract/awake.persona.selection.v1.schema.json`. The existing `runtimeBundle` object is activation metadata only.
- Do not invent a new `ContextSnapshot` or expand `RuntimeBundle` in this batch. A full prompt-context DTO requires a separate reviewed contract batch.
- This sub-batch validates only operations supported by the existing Persona runner and schemas. It does not introduce scope precedence, a new cross-system registry map, or a new revoke/supersedes contract.
- Existing runner fixture root: `docs/fixtures/persona-awake-joint/`; runner: `tools/persona-awake-joint/run-fixtures.ps1` with required `-FixtureId` and `-ReportPath`.
- The runner's `persistence`, `native-runtime-caller`, and `dynamic-invalidation` operations are `not_attempted` today and must not be counted as passes.
- Write scope after approval: only AWAKE offline fixture/report support if required to record an existing result; no new Persona runtime projection or persistence handler.
- No Worldbook v2 candidate is injected into the current AWAKE Runtime in this sub-batch. NPC prompt consumption remains an explicit deferred boundary, not a pass claim.
- Exit command set:
  - `dotnet build ..\\AWAKE.Tests\\AWAKE.Tests.csproj -c Release --nologo -v:minimal`
- `pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" -FixtureId "PWB-AWAKE-001-valid-approved" -ReportPath "tools\persona-awake-joint\artifacts\g3a-rev2-PWB-AWAKE-001-valid-approved.json"`
- `pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" -FixtureId "PWB-AWAKE-003-missing-selection-rejected" -ReportPath "tools\persona-awake-joint\artifacts\g3a-rev2-PWB-AWAKE-003-missing-selection-rejected.json"`
- `pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" -FixtureId "PWB-AWAKE-004-unknown-tag-rejected" -ReportPath "tools\persona-awake-joint\artifacts\g3a-rev2-PWB-AWAKE-004-unknown-tag-rejected.json"`
- `pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" -FixtureId "PWB-AWAKE-008-last-known-good" -ReportPath "tools\persona-awake-joint\artifacts\g3a-rev2-PWB-AWAKE-008-last-known-good.json"`
- `pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" -FixtureId "PWB-AWAKE-015-stale-selection-rejected" -ReportPath "tools\persona-awake-joint\artifacts\g3a-rev2-PWB-AWAKE-015-stale-selection-rejected.json"`
- Persona schema and crosswalk validation
- Report exact observed status/error tuples; unsupported operations do not count as passes.

#### G3B-1 — Legacy Worldbook pilot as Studio v2 compile/query fixture

- Initial fixture cardinality: 1 topic domain, 1–5 legacy files, and no claim that the v2 candidate is consumed by current AWAKE `ModuleData`.
- Expansion gate: only after the first fixture is valid may the pilot expand to 20–50 files.
- Required outputs: source snapshot, source unit records, duplicate/conflict/entity/loss reports, and `needs_review`; any proposed ID or authoring conversion remains report-only.
- Every document/assertion/expression provenance reference must include source ID/version/content hash/locator/quote hash, or be explicitly `author_created`.
- Source registry handling is report-only under `docs/worldbook-migration/`; no candidate source registry file is required by this revision.
- Worldbook permission checks reuse existing Studio fixture schemas and test entry points; each of the nine named cases is mapped to an existing fixture or recorded as `not_available`. This batch does not add a new permission implementation.
- This sub-batch ends at Studio v2 compile/query/permission evidence. A v2→AWAKE Runtime adapter is explicitly out of scope and would require a separate reviewed contract.
- ID output is report-only: no `event_id`, redirect, authoritative registration, or published migration event is created.
- Write scope after approval: only AWAKE `docs\\worldbook-migration\\` artifacts and offline fixtures; never Studio/PWB/UI Workstation source, AWAKE `ModuleData`, `src`, `dist`, PlayerExports, or game files.

#### G3C-1 — Existing fail-closed settlement Smoke

- Initial fixture cardinality: one existing named production Smoke case.
- No new dialogue receipt, Persona Prompt projection, or persistence handler is introduced.
- Required final observable object: the existing production Smoke report for one named settlement/recovery case; no new receipt schema is introduced in this batch.
- Exit command set:
  - `dotnet build "..\\AWAKE.Tests\\AWAKE.Tests.csproj" -c Release --nologo -v:minimal`
  - `..\\AWAKE.Tests\\bin\\Release\\net472\\Awake.SdkSmoke.exe`
  - `dotnet build "tools\worldbook-runtime-production-smoke\WorldbookRuntimeProductionSmoke.csproj" -c Release --nologo -v:minimal`
  - `tools\worldbook-runtime-production-smoke\artifacts\bin\Release\Awake.WorldbookRuntimeProductionSmoke.exe`
  - report the five existing fixture IDs and their exact output paths under `tools\persona-awake-joint\artifacts\`; do not invent a new runner invocation
  - existing Runtime Service/Provider/Storage project commands and pass counts reported by `tools\verify_marcus_awake_p3d_a0.ps1`

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

### A. Persona schema and adapter fixture validation

- Existing authoring/export/selection/definition fixtures are parsed and checked against their existing schemas.
- Existing supported invalid, missing, stale, unknown-tag, and last-known-good cases produce their recorded status/error tuple.
- No new Runtime Projection, prompt payload, byte/token budget, or live NPC consumption claim is made.

### B. Worldbook report-only pilot

- Legacy inputs are snapshotted before transformation.
- Each pilot output is a report-only analysis artifact, not a Studio authoring candidate, compiled package, canon, or Runtime input.
- Every assertion has `sources` or `author_created`, never both.
- Uncertain facts, conflicts, missing IDs, and unmapped fields enter `needs_review` or a loss report.
- No legacy file is overwritten.
- Base and optional adult content remain separated.

### C. Joint context

- Full Persona + Worldbook + memory + relationship + scene + PlayerKnown context assembly is deferred until a separate ContextSnapshot contract is approved.
- This batch does not claim joint context, prompt, or preview closure.

### D. Dialogue and settlement

- G3C-1 re-runs the existing named `failed-drain-fail-closed` production Smoke case and reports its existing observable result.
- New dialogue-to-Persona prompt closure and a new settlement receipt contract are deferred.

### E. Persistence and recovery

- Duplicate submission does not double-apply.
- Retryable and Unknown outcomes are not reported as Applied.
- Session N cannot mutate session N+1.
- Persona continuity/override/recovery are not claimed in this revision because the current runner reports persistence as `not_attempted`.
- Existing keys/defaults remain compatible.
- Restart-style fixtures recover conservatively without treating uncertain writes as applied.
- Existing Persona persistence operations are report-only unless the current runner explicitly supports them; unsupported operations remain `not_attempted`.
- Existing runners' outcome vocabulary is authoritative; unsupported outcomes remain `not_attempted`.

## Implementation order after approval

1. Freeze existing Persona fixture IDs and source fingerprints.
2. Run the five named Persona fixtures and record actual outcomes.
3. Freeze 1–5 legacy Worldbook files and produce report-only migration analysis.
4. Map Studio permission cases to existing fixtures or mark them `not_available`.
5. Run the named existing settlement Smoke case and save its stdout report under `docs\\offline-closure\\G3C-SETTLEMENT-SMOKE-20260903.txt`.
6. Run the exact commands listed by each sub-batch.
7. Record a source-only checkpoint; no new BuildId unless AWAKE runtime code changes.
8. Stop before sync or gameplay validation.

## Required evidence

- E0: this plan, review state, source/contract authority map.
- E1: schema/JSON parse, static contracts, Release build.
- E2: current-run Persona fixture reports, report-only Worldbook analysis, named settlement Smoke, and explicitly listed current offline regression commands; historical candidate evidence is background only and never counts toward this batch.
- E3: not claimed in this batch unless an explicitly authorized synchronization batch is later executed.
- E4/E5: not attempted; require user-run Bannerlord logs and matching BuildId.

## Explicit acceptance-to-evidence map

| Acceptance | Required fixture or static evidence | Minimum result |
|---|---|---:|
| Persona valid selection | existing supported Persona fixture | report actual result |
| Persona positive | `PWB-AWAKE-001-valid-approved` | `pass/0` |
| Persona fallback | `PWB-AWAKE-008-last-known-good` | `pass/0` |
| Persona rejection | `PWB-AWAKE-003-missing-selection-rejected`, `PWB-AWAKE-004-unknown-tag-rejected`, `PWB-AWAKE-015-stale-selection-rejected` | `3/3` expected rejects |
| Persona persistence/recovery | existing runner operation | `not_attempted`; remains unverified |
| Full context reaches NPC path | separate ContextSnapshot contract and named joint caller | deferred, not claimed |
| Persona ↔ Worldbook registry map | report-only unresolved mapping list | report generated; no closure claim |
| Export revoke/supersedes | existing supported Persona fixtures | supported cases only; unsupported cases `not_available` and unverified |
| Persona persistence | existing runner output | `not_attempted`; does not pass persistence requirement |
| Worldbook migration traceability | source snapshot, inventory, loss, duplicate, conflict reports | 1–5 files, all hash-bound |
| Studio authoring validity | existing Studio schema/fixture validator | report actual result; no invented candidate; unavailable remains unverified |
| Permission isolation | existing Studio permission fixture/test paths | each case mapped or `not_available`; unavailable remains unverified |
| Structured dialogue | existing smoke output | report actual result; no new prompt claim |
| Governed settlement | `failed-drain-fail-closed` in `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsBoundary.cs` | one existing case reported |
| Persistence truthfulness | not covered by this named case | unverified |
| Session isolation | not covered by this named case | unverified |
| Recovery | not covered by this named case | unverified |

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
- existing Persona runner reports under `tools/persona-awake-joint/artifacts/`
- `docs/offline-closure/G3C-SETTLEMENT-SMOKE-20260903.txt`, containing stdout, the named case, pass/fail counts, BuildId, executable SHA-256, and report SHA-256
- A new checkpoint with BuildId, hashes, commands, pass/fail counts, and unverified boundaries.

New migration snapshots and reports must remain under:

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\worldbook-migration\`

They must not be written to Worldbook Studio, Persona Workbench, UI Workstation, `src`, `ModuleData`, `GUI`, `dist`, PlayerExports, or the game directory. Existing runner outputs may remain in their established artifact directory and must not be reclassified as new authoritative evidence.

## Report-only migration records

- This revision produces reports only.
- It does not create `event_id`, redirect, authoritative ID registration, migration events, or compiled authoring documents.
- Any proposed ID or migration relationship remains inside a report with input hash, source snapshot ID, batch ID, and `needs_review`.
- `not_available` and `not_attempted` never count as pass; a required acceptance containing either status remains `unverified` or `blocked` and cannot make this batch closed.

## Deferred contracts

- full `ContextSnapshot` payload contract;
- full `RuntimeBundle` prompt payload contract;
- Studio v2 to current AWAKE Runtime adapter;
- Persona runtime prompt projection;
- live NPC prompt consumption;
- authoritative Studio registry mutation.
- expansion from the initial one-character/1–5-file gate to larger cardinalities.
