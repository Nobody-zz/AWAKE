# Worldbook Studio architecture debt audit checkpoint — 2026-09-01

- `REVIEW_TARGET`: `WORLDBOOK-STUDIO-ARCHITECTURE-DEBT-AUDIT-20260901`
- `REVISION`: `1`
- `DECISION`: `DEFERRED`
- `status`: `read_only_audit_complete`
- `approved_authority_batch`: `WORLDBOOK-STUDIO-APPROVAL-COMPILE-CLOSURE-CONTRACT-20260901` remains `approved`; this audit does not reopen or alter that batch.
- `game_directory_touched`: `false`
- `provider_or_api_key_used`: `false`
- `evidence_ceiling`: `E3` (static inspection, offline tests/smoke, package/release checks; no real Provider, Bannerlord runtime, or customer usability trial).

## Delivery identity

- Rebuilt current-test package after the previous source/package drift finding.
- Package: `tools/worldbook-studio/artifacts/current-test/WorldbookStudio-win-x64.zip`.
- `CURRENT-TEST-PACKAGE.json` records whole-`wwwroot` fingerprints, not only `studio-batch.js`.
- `manifestSha256`: `45ee4c0b192e8fe7e9d0d11382c74ca2a0346c1e308473e98c947a49ced07d3b`.
- `sourceWebAssetsSha256` = `packageWebAssetsSha256`: `688f89ce5f949ae50942a15316de29d8c673da0028a5b52d85c19482aaaf81e9`.
- Launcher `14` tests and six launcher smoke scenarios passed during the rebuild; no game directory or real AI Provider was used.

## Audit coverage

Audited changed files and one bounded caller/callee neighborhood:

- Core: `AuthorityGate.cs`, `AuthoringDraftContracts.cs`, `BatchProviderService.cs`.
- Web: `Program.cs`, `AuthoringDraftEndpoints.cs`.
- Frontend: `studio-draft.js`, `studio-batch.js`.
- Delivery: `scripts/package.ps1`.
- Code-debt runner: `2,727` logical lines across `8` files; `0` confirmed removable lines, `0` suspected dead-code lines, `188` low-severity static-complexity-risk lines, `0` unknown lines.

## Confirmed structural findings

### P1 — compile settlement has no durable operation identity

`AuthorityGateService.CompileApproved` validates the proof, recompiles, and calls `WriteCompiled`, but does not persist a compile operation ID/journal or expose replay/query semantics. A response loss or client retry can repeat the compile and replacement. `ExportStaging` has a separate operation journal, so compile and export have asymmetric recovery contracts.

Action: create a separate compile-settlement contract batch. Define server-owned operation identity, same-request replay, changed output/token conflict, crash recovery, and an observable operation query before implementation.

### P1 — AI authoring has two parallel domain workflows

Reference Draft and Batch Authoring separately model source, source unit, fact, evidence, review status, metadata, provider normalization, and document creation. The split is visible in `AuthoringDraft*` and `Batch*` contracts/services/repositories. This causes different semantics for expressions, evidence fallback, review status, and recovery.

Action: define a common authoring domain model (`SourceDocument → SourceUnit → CandidateClaim → Evidence → ReviewDecision → AuthoringDraft`) with paste/file ingestion adapters. Do not merge implementations blindly; preserve batch leases/cache and draft session/consent as infrastructure adapters.

### P1 — segmentation is not user-correctable

Batch splitting is paragraph-separated and then hard-capped at `80,000` UTF-16 characters (`BatchScanRepository.SplitUnits`, `splitter.v1`). Draft accepts a single `80,000`-character source and sends facts generation over the whole source. There is no first-class explanation of why a claim became a separate unit, source-span navigation with neighboring context, or merge/split/keep-together correction.

Action: add source-unit provenance and correction operations to a new UX/data-model batch; do not solve by adding more confirmation dialogs.

### P1 — review workload is not risk-shaped

Facts carry `inferred`, `certainty`, and evidence, and Batch has green/yellow risk, but the customer workflow still centers on item-by-item review plus broad accept-all behavior. There is no first-class grouping for directly evidenced low-risk claims versus inferred/high-risk claims, no “accept safe group, inspect exceptions” contract, and no independent correction path after one item is rejected.

Action: make review decisions explicit and independently addressable; preserve evidence and rejection without regenerating the entire source.

### P2 — authority and HTTP layers remain concentrated

`Application.cs` (~`837` nonblank/noncomment logical lines), `Program.cs` (~`705`), and `AuthorityGate.cs` (~`530`) mix routing, DTOs, error mapping, state transitions, persistence, compile, publish, and compatibility gates. Static complexity flags `ValidateCompileProof` and multiple frontend orchestration functions, but the audit found no safe dead-code removal.

Action: split by lifecycle/ownership in a later architecture batch: customer façade, authority command handlers, operation journal, compile/publish adapters, and route modules. Preserve one authoritative settlement path.

### P2 — duplicated commit machinery

`Commit` and `CommitArtifactOperation` implement overlapping prepared/marker/committed journal flows with different metadata and recovery behavior. They are not proven behavior-equivalent, so deleting one now would be unsafe.

Action: introduce a shared internal operation writer only after characterization tests cover document/selection/proof, staging artifact, and publish pointer semantics.

## Suspected / needs targeted proof

- `SafeId` only replaces slash, backslash, and `..`; unlike `BatchPathValidator.RequireIdentifier`, it does not define a strict character set, Windows reserved-name policy, or collision policy. Targeted invalid-ID and collision tests are required before calling it exploitable.
- `AuthorityFailure` returns `ex.Message` while editor/authoring failures use `SafeMessage(code)`. This may disclose workspace-relative paths and operation details. Confirm with an HTTP fixture and then switch to safe public text plus a server-side correlation ID in a separate security/contract change.
- Export replay currently validates proof freshness before looking up a committed operation. Whether stale-proof failure or replay takes precedence must be fixed as an explicit contract; current order is not necessarily wrong.
- Frontend evidence is VM/static harness evidence, not real browser E2E. DOM binding, layout, disabled states, toast progression, and long-text usability remain unverified.
- `studio-batch.js` performs local full-file reads for statistics before upload and uses whole-report refreshes; this is a bounded usability/performance risk, not a measured regression.

## Not findings

- Current code-debt scan does not justify deleting code based on file length or no direct static caller.
- Whole-`wwwroot` package fingerprinting is fixed in `scripts/package.ps1`; the current package was rebuilt and verified.
- No new P0 was found in the approved authorization/compile-proof closure.

## Next independent batches

1. `WORLDBOOK-STUDIO-COMPILE-SETTLEMENT-IDEMPOTENCY-20260901`: operation identity, replay/conflict, crash recovery, queryability; high-risk review before code.
2. `WORLDBOOK-STUDIO-AUTHORING-DOMAIN-UNIFICATION-20260901`: shared source/claim/evidence/review model with paste/file adapters; architecture RFC and migration fixtures.
3. `WORLDBOOK-STUDIO-AI-SEGMENTATION-REVIEW-UX-20260901`: explainable spans, merge/split/keep-together, risk-shaped review, resumable generation; customer browser E2E required.
4. `WORLDBOOK-STUDIO-BOUNDARY-HARDENING-20260901`: strict IDs, safe authority errors, correlation IDs, exact HTTP contract tests.

No code repair is applied in this checkpoint because the confirmed items cross persistence, public contract, or domain-model boundaries and require separate review states. The current approved authority closure remains deliverable only at the documented `E3` evidence ceiling.
