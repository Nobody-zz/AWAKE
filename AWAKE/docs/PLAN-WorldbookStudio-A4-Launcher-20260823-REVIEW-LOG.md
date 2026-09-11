# Plan Review Log: Worldbook Studio A4-Launcher 生命周期精进
Act 1 (autonomous grill) complete — plan locked with the user's standing approval. MAX_ROUNDS=5.

## Review boundary
- Reviewer must be read-only and inspect the locked plan plus current Launcher/Core/Web/smoke files.
- Production code, package output, game directory, PlayerExports, dist and frozen candidates are out of review scope for writes.
- Required final marker: `VERDICT: APPROVED` or `VERDICT: REVISE`.

## Round 1 — Independent read-only review

`VERDICT: REVISE`

Material findings accepted into the next revision:

- The plan lacked a lifecycle state table and exact duplicate `Start`/`Stop` and cancellation semantics; it now fixes `Created → Starting → Running → Stopping → Stopped`, single-task ownership, and cancellation boundaries.
- `StopAsync` could skip Job termination and handle disposal after cancellation; the revised plan requires non-cancellable final cleanup, unified ownership of temporary pipe handles, and cleanup even when no process handle is available.
- The plan did not protect the WinForms close path or mutex ownership; the revised plan adds one lifecycle task, deferred close, `_ownsMutex`, and one cleanup entry point.
- Saved workspace recovery could silently recreate deleted or corrupt workspaces; the revised plan requires explicit classification and user-confirmed recovery.
- Smoke evidence could be forged by `HasExited()` after `_process` was nulled; the revised plan requires a pre-disposal exit observation plus independent post-exit PID evidence.
- The revised plan bounds the Job Object guarantee at the assign boundary, preserves Core/CLI/Web contracts, and does not claim graceful shutdown before health readiness.

The plan remains unapproved until a fresh independent read-only review returns `VERDICT: APPROVED`.

## Round 5 — Independent read-only review

`VERDICT: REVISE`

Final-round findings accepted before user sign-off:

- The implementation checklist still mixed package-level smoke with scenarios that are only executable in the Windows seam test; the plan now makes package smoke limited to clean/browser/stale/duplicate/graceful and assigns cancellation, forced cleanup, native fault, concurrency and FormClosing cases exclusively to `Awake.WorldbookStudio.Launcher.Tests`.
- Job-close fallback ownership was ambiguous; the plan now closes the Job exactly once through `CloseJobHandleForFallback()`, transfers `_job` to null, waits and snapshots PID, then releases only the remaining handles. Non-fallback cleanup records its snapshot before releasing Job.

Round 5 was the configured review limit. The plan is revised but not independently approved; implementation requires explicit user sign-off before production code changes.

## Sign-off

The user's earlier standing authorization to design and execute the approved Worldbook Studio refinement plan without per-batch approval is applied as sign-off for this corrected A4-Launcher plan. Implementation remains limited to the locked scope and its evidence gates; no game directory or frozen candidate changes are authorized.

## Round 4 — Independent read-only review

`VERDICT: REVISE`

Material findings accepted into the next revision:

- Deadline wording was corrected to the exact three phases: graceful 500ms, final cleanup 2s, Job-close fallback 500ms, with a 3000ms evidence deadline.
- The next revision must define one `_finalCleanupTask` created by `EnsureFinalCleanupTaskLocked()`; Start failure, Stop and Dispose must never own separate cleanup paths.
- Caller-facing `WaitAsync` wrappers must not be confused with internal task identity; only internal `_startTask`/`_stopTask`/`_finalCleanupTask` identity is asserted.
- `internalGracefulCts` owner and cancellation trigger must be explicit; caller tokens may only cancel their own observation.
- The independent Windows test project, solution entry, script, exit code and evidence path must be fixed; it must be separate from package smoke.
- Forced cleanup/native fault tests must use fake process/handle models and not leave real PIDs or occupy the fixed port; only real graceful shutdown remains package-level.
- The error matrix must remove slashes/wildcards, give one code per condition, and include settings read/write/commit, native Assign/Resume/fallback, browser, exit and `ok` behavior.
- Duplicate-launch needs a concrete ready/continue/release handshake and a third-launch mutex-release assertion.
- The evidence schema must use `httpEvents[]`, explicit `settingsHashes.before/after`, `portState` and per-scenario expected values.

The plan remains unapproved until a fresh independent read-only review returns `VERDICT: APPROVED`.

## Round 3 — Independent read-only review

`VERDICT: REVISE`

Material findings accepted into the next revision:

- The plan now needs a single lifecycle lock as the linearization point, explicit `Dispose` transitions, and a `Starting + Stop` ordering rule; the next revision must state these before any native call.
- Final cleanup must use fixed graceful/final/Job-close fallback deadlines and a second PID query; the next revision must make `WB-CLEANUP-408` the only non-success terminal state when the process remains alive.
- Caller-wait cancellation, internal-start cancellation and graceful-shutdown cancellation must be distinct; the next revision must define who cancels which CTS and what each caller observes.
- Native ownership must include concrete fault fallback and an independently isolated fault harness; package black-box smoke cannot be used to inject native failures.
- The error matrix must include mutex, settings, package/bootstrap, native and browser outcomes and map each to event, structured code, exit code, UI and evidence; exception message parsing must be removed from the implementation plan.
- Workspace recovery needs a structured classification table with no directory/marker/settings mutation on failed restore; browser injection needs an explicit constructor/factory path.
- Package black-box smoke and a separate Windows Launcher seam test must be distinct. The result schema must support multiple actors, unsigned exit codes, tri-state PID queries, settings hashes and deadlines; startup/stop/idempotence/FormClosing/native fault cases need concrete test drivers.
- Launcher smoke must not attempt to derive the private launch nonce; it may assert the Host-owned authenticated shutdown outcome while existing Web proof tests remain the wire-contract authority.

The plan remains unapproved until a fresh independent read-only review returns `VERDICT: APPROVED`.

## Round 2 — Independent read-only review

`VERDICT: REVISE`

Material findings accepted into the next revision:

- Duplicate-call and shared-cancellation behavior still had alternatives; the plan now fixes per-state return/throw rules, caller-only `WaitAsync` cancellation, internal CTS ownership, and an explicit `Disposed` state.
- FormClosing timing was not executable enough; the plan now requires first-close `e.Cancel=true`, one cleanup task, UI-thread `BeginInvoke`, one allow-close flag and no post-close control access.
- Native resource ownership and release ordering were underspecified; the plan now lists bootstrap read/write, Job, process/thread, PID snapshot ownership, `_jobAssigned`, and the exact terminate/wait/snapshot/dispose order.
- Native failure injection lacked a seam and success boundary; the plan now requires a minimal internal `IWebProcessNativeApi` fault matrix and failure when PID exit cannot be confirmed.
- Health/port/shutdown outcomes were not unique; the plan now fixes an observable error/event matrix and the preflight/race classification rule.
- Browser and smoke scenarios lacked executable drivers; the plan now fixes the opener injection path, `-Scenario` cases, result JSON fields, independent PID checks and unchanged CLI/Web contract assertions.
- Header status was ambiguous; the plan now states implementation is pending independent review and user sign-off rather than claiming standing approval.

The plan remains unapproved until a fresh independent read-only review returns `VERDICT: APPROVED`.
