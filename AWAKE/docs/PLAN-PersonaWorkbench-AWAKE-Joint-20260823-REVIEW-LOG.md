# Plan Review Log: PersonaWorkbench × AWAKE Joint Persona Contract and Runtime Bridge

Act 1 (grill) complete — plan locked under the user's autonomous recommended-path instruction. MAX_ROUNDS=5.

Reviewer model: `gpt-5.6-luna` from `C:\Users\26811\.codex\config.toml`; `codex-cli 0.147.0`; model not pinned on review command.

## Act 1 decisions
- Scope is the Workbench authoring/export boundary plus AWAKE runtime Persona selection/projection.
- Workbench and AWAKE remain separate schemas and separate approval states.
- No source, `dist`, game-directory, or frozen-candidate mutation is authorized during planning/review.
- Existing Native Knowledge Boundary work is a dependency for dynamic state lifecycle, not a second implementation target in this batch.

## Review transport
The independent reviewer must run read-only for every round. Each round appends the complete critique and the controller's response below. No reviewer may edit this plan, source, checkpoint, lock, or game files.

## Round 1 — independent read-only review

Three independent reviewers all returned `VERDICT: REVISE`. Their findings agree on the following material blockers:

- The current manifest is `awake.worldbook.v1`, while `WorldbookRuntime` accepts only `awake.worldbook.registry.v1` or `awake.worldbook.v2`; the canonical schema and migration/compatibility policy must be selected before adapter work.
- The v2 runtime path initializes knowledge but leaves `WorldbookRuntime.Current` empty, while `NpcDialogueService` still builds Persona only through `Current`; a single facade and a real caller smoke are required.
- `WorldbookRuntime.Reload()` destroys the current instance before validating the candidate; it cannot satisfy last-known-good semantics.
- Workbench `character.v1` lacks AWAKE selection fields (`CharacterId`, `IdentityId`, `Role`, `Scope`, `Priority`), and there is no versioned crosswalk covering tags, axes, flags, reaction/commitment, provenance and loss policy.
- Legacy fallback can generate DSL when the approved/runtime path is missing or invalid; this conflicts with an approved-only runtime contract and must be explicitly disabled, migrated, or represented as a known-good snapshot.
- The plan did not freeze a state machine, fixture IDs, exact commands, reports, candidate allowlist, hash/BuildId composition, or the write-set boundary with the Native Knowledge Boundary batch.
- Existing validation/current documents contain conflicting candidate claims; no new E4/E5 evidence can be attributed until a single candidate ledger is reconciled.
- Dynamic relation, memory, current-state, continuity and player override inputs are not proven to enter the production Persona projection; cache fingerprints and stable identity normalization are incomplete.
- Persistence validators are model-level only; no runtime Storage/SaveData lifecycle evidence exists.

Controller decision: all material findings are accepted. No code is written in Round 1. The plan is revised to split canonical schema/ledger/contract prerequisites from runtime implementation and to make all evidence and ownership explicit.

### Controller revision before Round 2

The controller resolved the remaining schema choices from repository evidence:

- Runtime worldbook canonical: `awake.worldbook.v2`; current `awake.worldbook.v1` is migration input only and is not auto-consumed by the new runtime.
- Authoring canonical: `awake.persona.authoring.v2` from the existing K1 contract; Workbench `persona-workbench.character.v1` is migrated into it and is not a second authoring truth.
- Runtime persona canonical: `awake.persona.definition.v1`, produced from approved authoring-v2 through an explicit adapter.
- First-load failure behavior: deterministic identity-only `runtime_fallback` DSL, with no unapproved authored persona content; never an empty silent string and never an implicit legacy persona fallback.

These are selected design decisions, not open questions. Round 2 should review their compatibility and completeness.

## Round 2 — independent read-only review

All three reviewers returned `VERDICT: REVISE`. The remaining material findings were accepted and addressed by adding `PLAN-PersonaWorkbench-AWAKE-Joint-CONTRACT-LOCK-20260823.md` and `persona-awake-candidate-ledger.v1.draft.json`.

Accepted corrections:

- Freeze a schema/version matrix with object identity, `$id`, path, unknown-field policy and digest inputs; distinguish worldbook v1 migration input, registry.v1 envelope and v2 package manifest.
- Do not treat the rejected K1 plan as implementation approval; freeze only a K1A-compatible minimum in this annex and require independent review of `sourcePackId`, ID grammar, registry closure and migration state.
- Define `awake.persona.export.v1`, exact selection authority, field equality rules, approval evidence and digest tuple.
- Define transition actors/evidence/rejection/revoke/rollback semantics instead of a bare state sequence.
- Make migration `preserve_only` distinct from export/runtime `reject` loss policy; distinguish Persona registry digest from Worldbook package registry digest.
- Add candidate ledger, fixed fixture IDs/paths/commands/reports, hash/BuildId attribution and the hard E0–E2 ceiling while ledger/Native lease/E3 preparation remain unresolved.
- Add explicit forbidden old callers, immutable RuntimeBundle, identity-only fallback, ContextSnapshot fingerprint coverage and Native Storage owner prerequisite.

No source, dist, game-directory, frozen-candidate or runtime state was modified in Round 2.

## Round 3 — independent read-only review

All three reviewers returned `VERDICT: REVISE`. The remaining findings were accepted and addressed in the Contract-Lock annex and ledger draft:

- Added exact Worldbook `$id`/`schemaVersion` separation for package manifest, runtime payload, index payload and installed registry.
- Added the K1A-compatible authoring-v2 minimum, explicit sourcePackId/ID/approval rules, full export-v1 nested field contract, and transition record/idempotency semantics.
- Added exhaustive crosswalk row shape, axis/flag/facet encodings, staged `preserve_only` versus export/runtime `reject`, and registry digest separation.
- Added identity resolver ownership, actual forbidden legacy symbols/call sites, immutable defensive-copy RuntimeBundle, Interlocked publication, exact identity-only fallback bytes and field-level fingerprint normalization/formula.
- Rebuilt the ledger draft to match its declared schema, separated observed/claimed evidence, capped effective evidence at E2, and recorded the actual read-only hash observations (including missing dist/game DLL paths).
- Marked runtime caller/dynamic/persistence fixtures owner-lease-required, specified the frozen-candidate isolation fixture, corrected the build command to `-SkipGameVersionCheck`, and specified (but did not yet implement) the machine-checkable Native prerequisite wrapper.

No source, runtime, dist, game-directory or frozen-candidate files were modified. Round 3 remains a planning review until a later verdict.


## Round 4 closure

Fourth-round review found and the controller corrected:

- Canonical ledger authority is now the non-draft file; the draft is explicitly non-authoritative staging and must be byte-identical before promotion.
- Actual DLL paths/hashes and release-check failure are recorded as observations; the ledger does not claim E3.
- Selection is now an explicit `awake.persona.selection.v1` sidecar/request with source/selection revision provenance; no filename/display-name inference.
- Runtime definition-v1 and the existing `awake.persona.tags.v1` target shape are frozen, including rejection of unsupported enabled rules.
- Promotion requires accepted runtime observations and no unrepresentable rules; Workbench approval alone cannot promote.
- A single Persona canonical-byte algorithm, mapping/error report schemas, standalone Worldbook index shape and existing golden hash procedure are fixed.
- Fallback byte grammar now distinguishes five key-value lines from headers and separates diagnostic logging from DSL content.

The implementation paths in `tools/persona-awake-joint` and fixture directories remain specified but not yet created; the planning log must not describe them as implemented. E3–E5 remain blocked.

## Round 5 — deadlock at MAX_ROUNDS

All three final read-only reviewers returned `VERDICT: REVISE`. Per `grill-me-codex`, the review loop stops here and does not fake convergence.

### Unresolved points and controller recommendations

1. **Native approval versus lease:** Current state is `approved_for_b1_only` with `execution_lease: none`. Recommendation: require both an exact approved scope and a unique lease; B1-only approval never authorizes `NpcDialogueService`, shared RuntimeBundle, or Storage changes.
2. **ContextSnapshot/session identity:** Recommendation: add explicit `sessionGeneration`, `captureToken`, `sourceHeroId`, `targetHeroId`, `characterId`, `personaIdentityId`, `activeBundleId`, `buildId`, and all Persona/Worldbook digests; identity resolution is one-way and unknown values use `UNKNOWN` rather than profile/hero-name inference.
3. **Fallback contract:** Recommendation: retain exactly five key-value lines (`STATUS`, `REASON`, `CHARACTER_ID`, `IDENTITY_ID`, `ROLE`) plus two headers; remove the stale “four keys” wording and define canonical ID kinds/unknown behavior.
4. **Runner write set:** Recommendation: allow `tools/persona-awake-joint/*` as isolated pre-lease verification tooling only; it may never create or grant a runtime lease, write source/runtime/dist/game/frozen roots, or mark E3+.
5. **Revision carriers:** Recommendation: put `selectionRevision` in the export root digest metadata (not inside the five-field `selection` match object) and put `authoringRevision` explicitly in `migration` and the export root; both are required and hashed.
6. **Frozen isolation:** Recommendation: take an immutable read-only snapshot of each protected frozen root before the test, execute destructive attempts only against a sibling temporary copy, and compare the real protected roots before/after; documentation/ledger files are outside the frozen candidate roots.
7. **Report exit codes:** Recommendation: `pass=0`, `reject=10`, `blocked=20`, `not_attempted=30`, `error=40`; every report stores both status and exit code and the wrapper must not reinterpret a blocked result as pass.

### Current gate

- No source/runtime/storage/UI/dist/game/frozen-candidate implementation has been authorized.
- Canonical ledger and staging draft are byte-identical, root `needs_reconcile`, `activeCandidateId=null`, evidence ceiling `E2`.
- E3/E4/E5 remain blocked; Native caller/dynamic/persistence fixtures remain owner-lease-required.
- The next implementation, if authorized after resolving the above, is limited to isolated offline E0–E2 contract/adapter/fixture artifacts.

## User continuation resolution

The user continued the active goal after the MAX_ROUNDS deadlock. Controller recommendations are accepted for the next bounded batch:

- implement only isolated offline E0–E2 contract/adapter/fixture/report artifacts;
- permit `tools/persona-awake-joint/*` in the isolated pre-lease write set;
- change the v1 fixture to runtime rejection-only;
- require exact Native scope approval plus unique lease for shared runtime work;
- keep E3–E5 and fixtures 010–012 blocked/not_attempted.

This is not a claim that the AWAKE runtime bridge is implemented or that the game candidate is synchronized.

## G2 evidence regression review — 2026-08-24 16:20

Two independent read-only reviews were run before integrating the runtime-static evidence regression. The accepted findings were:

- A consumer verifier was required; the producer's own `observedErrors` construction could not be its sole proof.
- The verifier had to lock the complete 13-item check ID/severity set, reject unknown/missing/downgraded checks, compare failed-blocking paths as a one-to-one mapping, and keep target `reject/10` separate from verifier `pass/0`.
- The existing adapter-error contract had a real gap: runtime-static code/stage values were emitted but absent from both schema copies; the static artifact ID also used an invalid hyphenated stable ID. Both schema copies were aligned and the producer now emits `runtime_bridge_static`.
- Empty JSON arrays exposed a PowerShell collection-unrolling bug in the new verifier; explicit `List[object]` handling was added and pass/reject symmetry was re-run.

Evidence accepted for this bounded batch:

- `gate-20260824-runtime-static-evidence-final.json`: `pass/0` against the live `reject/10` report.
- `gate-20260824-runtime-static-pass-evidence-v4.json`: `pass/0` against a synthetic pass case with a non-blocking contract diagnostic.
- Negative mutation cases for missing/unknown/downgraded checks and unsupported status all returned their expected non-pass result.

No `AWAKE/src`, `ModuleData`, `dist`, game directory, Native lease, Storage state, or frozen candidate was modified. G3-A, G3-B and G4 remain blocked; the evidence change only strengthens the E2 gate.

## G3 execution-gate correction — 2026-08-24

The controller found a sequencing contradiction: the old task graph placed full G3-B persistence after G3-A while the shared prerequisite check also required Storage readiness. The bounded correction is recorded in `PLAN-PersonaWorkbench-AWAKE-Joint-G3-EXECUTION-GATE-20260824.md`:

```text
G3-S0 readiness contract -> G3-A runtime projection -> G3-B persistence -> G3-C offline integration -> G4 game evidence
```

G3-S0 is readiness only; it does not claim save/load, branch, restart or recovery. No runtime source, Storage state, candidate, package or game file was changed in this correction. The new plan remains `REVIEW_PENDING`; the unavailable/hung read-only reviewer is not treated as approval.

The `PWB-AWAKE-012-persistence` negative fixture also had a stale `expected-error.txt` path (`storageOwner`) while the canonical report and expected JSON use `$.storagePrerequisite.owner`. The path and its manifest hash were aligned; the fixture still correctly returns `blocked/20` with all assertions passing.
