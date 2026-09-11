# Plan Review Log: Marcus-Awake P2 Framework Core Vertical Integration

Act 1 (grill) complete — scope was resolved from the existing P1 checkpoint, P1.5 contract, capability matrix, and current AWAKE source. MAX_ROUNDS=5.

## Act 1 — Resolved boundary

- P2 is Framework Core vertical integration, not AWAKE gameplay migration.
- The write set is limited to the independent Framework Core source/tests plus P2 evidence/checkpoint documents.
- The fixture must prove the ordered call chain and stale-session rejection, while explicitly labeling the SubModule entry as a deterministic boundary fixture rather than real Bannerlord execution.
- No old Marcus compatibility layer, no dual runtime path, no game launch, and no game-directory synchronization.

## Round 1 — Independent read-only review

**Result:** `VERDICT: REVISE`

### Findings

- P0: `RequestContext` does not carry a session generation; `SessionCoordinator.Validate` compares only `SessionRef` and state, so an old context may pass after the same reference is reopened. Fix: bind context creation to `SessionLease.Generation` and validate reference, generation, and Ready state.
- P0: `DiagnosticsService.Probe` always writes receipt generation `0` without validating the current session. Fix: generate receipts only through a current lease/session-validated path; stale, closing, and drained requests must return typed failure.
- P0: P2 names an `Unregistered` state that P1 Host/Session APIs do not have. Fix: use an explicit fixture lifecycle driver and treat locator clear after drain as the fixture's unregister step, or extend the contract explicitly.
- P0: `FrameworkCoreVerticalSmoke.cs` would not compile because the test project disables default compile items and includes only `Program.cs`. Fix: add an explicit Compile item or merge the fixture into Program.
- P0: Bootstrap visibility was ambiguous: public would expand the API surface, internal lacked test access. Fix: keep it internal, add controlled test visibility, and record that the public API surface is unchanged.
- P0: The plan required updating `AWAKE-CURRENT.md` but did not include it in the declared write set. Fix: either remove that update or add the exact file and timing to the write set.
- P1: “real lifecycle” wording conflicted with the claim that no Bannerlord lifecycle is being faked. Fix: call it `FixtureLifecycleDriver`, explicitly no SubModule/Bannerlord callback and no E4 evidence.
- P1: Host, locator, and session operations had no single ordering, rollback, or duplicate-call contract. Fix: freeze `Host.Register → Locator.Register → Host.Ready → Session.BeginSession` and test rollback/conflict behavior.
- P1: GameData and ContextPlanner do not themselves reject stale sessions. Fix: make `Sessions.Validate(context)` the fixture's mandatory common gate and assert closing, drained, generation mismatch, and deadline failures.
- P1: Evidence requirements were too vague. Fix: record exact write set, all relevant SHA-256 values, fixture output, API surface diff, forbidden-scope scan, and E2 limitations in a P2 evidence JSON.

### Controller response

- Accepted all findings. The plan now allows only the minimum P1 contract corrections needed for generation and diagnostic binding; it does not add an old API compatibility layer or broaden into Provider/Service work.
- Replaced simulated SubModule terminology with `FixtureLifecycleDriver`; real Bannerlord integration remains explicitly out of scope.
- Added `FrameworkBootstrap.cs`, `TestAccess.cs`, the three P1 contract files, the explicit test project file, `AWAKE-CURRENT.md` timing, rollback rules, and evidence requirements to the exact candidate write set.
- P2 remains in `needs_review`; no code write lease is active until a second independent review returns `VERDICT: APPROVED`.

## Round 2 — Review transport failure

- The independent reviewer failed before returning a verdict with `502 Bad Gateway`; the proxy reported upstream `503` after retries were exhausted.
- No project code was changed and no approval was inferred from the failed request.
- Per retry policy, the P2 batch is `blocked_external`; the next action is one fresh review attempt after an explicit continuation signal.

## Round 3 — Independent read-only review (2026-08-26)

**Result:** `VERDICT: REVISE`

### Findings

- P0: `DiagnosticsService.Probe` still returns a bare receipt, cannot report typed session/deadline failure, and hard-codes generation `0`.
- P0: `RequestContext` still lacks a generation binding; a context tied to the same `SessionRef` could survive a later open.
- P0: `SessionCoordinator` and `SessionLease` are not synchronized; concurrent start/close/drain calls can violate the state machine.
- P0: `CompleteDrain` only changes state and does not prove pending work, late-result fencing, or clean drain.
- P0: Bootstrap rollback and the Host/Locator/Ready ordering are not a single testable contract; the current Host API cannot restore a candidate Host after a failed registration path.
- P1: The plan did not lock repeated Ready/Closing/Drained/RecoveryRequired semantics or the generation increment point required by the P1.5 contract.
- P1: No fixture path exercised a controllable pending operation or a late completion that must be rejected without mutating state.
- P2: Evidence layering, forbidden-scope scan rules, API-surface comparison, and evidence-generation order were not concrete enough to prevent a false completion claim.

### Controller response

- Accepted the findings. The plan now includes `HostApi.cs` for an internal `AbortRegistration` compensation path, fixes the order to `Host.Register → Host.Ready → Locator.Register → Session.BeginSession`, and requires proof that a conflicted candidate returns to `Created` without replacing the current Host.
- Locked `RequestContext` creation from `SessionLease`, current fence generation, `ValidateLease`, typed Diagnostics results, thread-safe Session/Lease transitions, pending-operation drain barrier, and stale-result fencing.
- Locked repeated lifecycle semantics: Ready is idempotent for the same reference, Closing returns `session_closing`, Drained opens only a new generation, and RecoveryRequired cannot be silently replaced.
- Added exact stable failure codes, concurrent/late-result acceptance cases, evidence class separation (`offline_fixture`, `framework_build`, `real_bannerlord`), generation/API diff requirements, forbidden-scope scan scope, and evidence update order.
- No code was changed; P2 remains `needs_review` until a new independent reviewer returns `VERDICT: APPROVED`.

## Round 4 — Independent read-only review (2026-08-26)

**Result:** `VERDICT: REVISE`

### Findings

- P0: P1.5 was approved in its checkpoint but the source document still advertised `P1.5_CONTRACT_REVISE_REQUIRED`; P2 had no immutable contract hash. This could let implementation proceed against a different contract revision.
- P0: The revised plan still did not define a concrete `SessionOperation` lifecycle, so double completion, racing completion/close, and release-after-drain remained ambiguous.
- P0: Thread-safety required a named lock ordering and a safe cancellation boundary; simply saying “thread-safe” was insufficient.
- P0: P2’s public constructor/return-type changes were breaking changes but had no explicit API-major/version rule or statement that old P1 evidence is historical only.
- P1: P1.5’s exact E1 evidence filename and snake_case error codes were not yet aligned with the P2 plan.
- P1: `DrainTaskId`, RecoveryRequired behavior, and the candidate Host rollback assertions were not fully locked.
- P1: `GameData`/`ContextPlanner` gate ownership and the shared IPC/backpressure synchronization policy needed an explicit testable rule.

### Controller response

- Accepted all findings. The P1.5 document header now matches the approved checkpoint (`P1.5_CONTRACT_LOCKED`), and its SHA-256 is pinned in the P2 plan/checkpoint.
- Locked the P2 API break as Framework API major `2.0`, with no old namespace compatibility layer; P1 API/evidence remains a historical baseline and the later AWAKE caller migration is a separate gate.
- Added a public `SessionOperation` handle contract: owner/lease/generation/operation ID binding, atomic first completion, stale-result return after fencing, duplicate-completion error, and pending-count release.
- Added the exact coordinator/lease lock ordering, cancellation outside locks, `DrainTaskId`, RecoveryRequired and Drained semantics, Bootstrap candidate rollback assertions, gate ownership, IPC/backpressure synchronization requirement, E1 evidence filename, and canonical snake_case error codes.
- No code was changed; P2 remains `needs_review` until another independent review returns `VERDICT: APPROVED`.

## Round 5 — Independent read-only review (2026-08-26)

**Result:** `VERDICT: REVISE`

### Findings

- P1: IPC/backpressure wording still allowed “lock/atomic” alternatives without naming the primitive or linearization point.
- P1: RecoveryRequired stated that replacement is forbidden but did not define the entry condition, permitted operations, or recovery-to-new-generation path.
- P1: Bootstrap failure injection did not state how a failure after partial Host.Register mutation is compensated and verified.

### Controller response

- Accepted all findings. `IpcSequenceWindow` is now locked to one private `lock` covering validation, receive bookkeeping and watermark advancement; `BackpressureGate` is locked to `Interlocked.CompareExchange`/`Volatile.Read` with explicit CAS linearization and no underflow.
- RecoveryRequired is now an explicit Closing-only terminal state for the coordinator, entered only by the drain-deadline/unrecoverable fault seam; all new/validation/drain operations fail with `session_recovery_required`, and only a fresh Bootstrap after external recovery may create a new generation.
- Bootstrap rollback now injects failures after each successful stage, including Host.Register; `AbortRegistration` must restore the candidate to Created, clean a candidate session through close/drain when needed, preserve any pre-existing Locator, and prove a no-fault restart succeeds.
- No code was changed; P2 remains `needs_review` pending a fresh independent approval verdict.

## Round 6 — Independent read-only review (2026-08-26)

**Result:** `VERDICT: APPROVED`

### Review evidence

- The reviewer recomputed the P1.5 contract hash as `DB45A9C4C130BC727394B61AFD9836508045F83B66BA7F23C0BF3686D596D619`; it matched the P2 plan and checkpoint, and the status was `P1.5_CONTRACT_LOCKED`.
- No remaining P0/P1 blocker was found in the current P2 plan. The review explicitly confirmed the API 2.0 break, historical P1 evidence boundary, SessionOperation lifecycle, lock order, cancellation boundary, Bootstrap rollback, drain/recovery semantics, typed diagnostics, validation gate, IPC/backpressure linearization, E1 evidence filename, fixture compile entry, write set and evidence order.
- This approval applies only to the P2 Framework Core offline implementation write set. It does not authorize `AWAKE.csproj`, `SubModule.xml`, current AWAKE runtime code, Runtime Service, MCM, DevTools, worldbook, packaging, synchronization or game launch.

### Controller response

- P2 code write lease is enabled. Implementation must follow the approved plan exactly and stop before the listed out-of-scope paths.

## Round 7 — Incremental write-set review required (2026-08-26)

- During implementation preparation, the controller found that the approved API 2.0 breaking contract also requires `FrameworkIdentity.Current()` to report API major `2.0`.
- `src/FrameworkIdentity.cs` was therefore added to the exact P2 write set; this is a narrow contract-consistency expansion, not an implementation approval.
- The P2 checkpoint is returned to `needs_review` until an incremental read-only review confirms that this file addition introduces no other scope change.

## Round 8 — Independent incremental write-set review (2026-08-26)

**Result:** `VERDICT: APPROVED`

### Review evidence

- The independent reviewer found no P0/P1/P2 blocker in the narrow incremental scope.
- `FrameworkIdentity.cs` is the only newly added source file in the write set; changing `FrameworkIdentity.Current()` from API `1.0` to `2.0` is required by the already locked breaking contract and does not add a type, member, entry point, dependency, or runtime scope.
- The reviewer rechecked the P1.5 hash, the P2 plan/checkpoint consistency, and the existing treatment of transport failures as non-verdicts.
- This approval covers only the current P2 Framework Core offline write set. It does not cover AWAKE runtime, Provider, MCM, DevTools, worldbook, packaging, synchronization, `AWAKE.csproj`, `SubModule.xml`, or game execution.

### Controller response

- P2 implementation write lease is enabled. The implementation will follow the locked write set and produce only offline E1 evidence before moving to P3.
