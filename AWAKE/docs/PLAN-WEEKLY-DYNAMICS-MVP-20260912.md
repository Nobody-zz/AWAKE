# Plan: 本周动态最小可用版（MVP）

> Status: `revised_after_review_round_1`
> Revision: `R2`
> Baseline: source `b7eacaa4f679dfcdeb8a45b16337a35f60636a1f`; current local evidence ceiling `E2`.
> Supersedes for this delivery only: `PLAN-WEEKLY-DYNAMICS-20260912.md`, `PLAN-WORLD-REPORT-FOUNDATION-20260912.md`, and `PLAN-WORLD-WINDOW-STORAGE-20260912.md`. They remain deferred design references and are not implementation prerequisites.

## 1. Goal

Make the existing world-fact ledger useful in game: the command terminal shows a saved completed-week report named **本周动态**, or an explicitly temporary **近期动态** when no completed week exists.

The completed-week report must be generated and saved without Native Knowledge / Worldbook readiness. Worldbook may later project a saved report, but it must never decide whether the report exists.

## 2. Fixed decisions

| Topic | Decision |
| --- | --- |
| Report contract | Keep the existing `awake.worldbook.weekly-report.v1` report shape and existing `weeklyReports` state. No v2 schema or save migration in this batch. |
| Storage | Reuse the current `WorldStateStore` and its current retention/capacity behavior. The current limits are a known MVP limit, not a promise of unlimited history. |
| Background permission | Background code only uses an already-installed current `WorldStateStore`; it must not call `EnsureWorldStateReadyAsync`, `PermissionGate.EnsureAsync`, or otherwise request permission. |
| Player action | Opening the existing terminal item is player intent. It may use the established readiness path to obtain storage when needed; if that fails, show an unavailable state rather than an invented empty report. |
| Naming | Formal: `本周动态`; temporary: `近期动态`. Sections, only when nonempty: `政治与外交`, `战争与领地`, `人物近况`, `地方情况`. |
| MCM | No new setting: this is an existing terminal view with no player-tunable frequency or rule. |

## 3. Closed behavior loop

### A. Automatic completed-week update

`OnHourlyTick` -> if the session is current and a `WorldStateStore` is already installed -> `EnsureFormalReportsReadyAsync(day)` -> load existing facts -> build/reuse **only the latest completed week** as a v1 report -> save/reuse its existing report state -> structured log.

This scheduling is outside the `EnableEventEngine` early return so turning off random event popups does not disable reporting. It never initializes storage or requests permissions.

### B. Player menu

Terminal item -> obtain `IMarcusAiFrameworkHost` through `FrameworkHostLocator` -> as this is a player click, call the existing `AwakeRuntime.EnsureWorldStateReadyAsync(host, sessionToken)` route -> on success load facts and formal report states, then ensure the latest completed report -> UI result:

- latest valid completed report: title `本周动态`, display saved report text;
- no completed week: title `近期动态`, display the current rolling seven-day preview;
- storage unavailable/read failure: title `本周动态`, display a short unavailable message; do not render `本周没有记录。` as if a read succeeded.

### C. Optional worldbook projection

After a formal report has been saved, the pre-existing Native-gated knowledge path may project it. Projection failure or absent Native readiness changes neither saved report state nor menu result.

There is no historical backfill: a late-game save generates or reuses only its most recently completed week. Earlier missing weeks remain absent rather than becoming fabricated empty reports.

## 4. Required code changes

1. Split formal report persistence from `WorldEventServices.EnsureKnowledgeReadyAsync` into a storage-only `EnsureFormalReportsReadyAsync`. It takes only a current day, current session/store checks, and cancellation; it has no Native/Worldbook check.
2. Keep `EnsureKnowledgeReadyAsync` as the optional projection caller, reusing saved formal reports rather than owning their creation.
3. Add a throttled hourly scheduler for the storage-only method before the event-engine toggle. It must no-op when no current store is installed. The no-permission guarantee applies to this new scheduler only; it does not claim to repair older background restore behavior.
4. Change the terminal load path to acquire the host and call `EnsureWorldStateReadyAsync` only as a direct consequence of the player's click. On success it returns a formal / preview result; absent host, denied readiness, or load failure returns unavailable. It must not silently fall back to `Recorder.SnapshotWeek(day)` after a failed storage operation.
5. Add a small read-only display result (state, title, status, body) and pass it from terminal -> `WeeklyReportBrowserOverlay` -> `WeeklyReportBrowserVM`, instead of forcing the VM to infer state from one string.
6. Update the report formatter's section names and both language files. Empty sections remain omitted.
7. Add focused offline coverage for the three menu states, Native-disabled formal save, event-engine-disabled scheduling, same-week reuse, late-save no-backfill, and unavailable storage. Reuse existing report/store test infrastructure; do not add framework contracts.

Expected write set: `src/WorldEventContracts.cs`, `src/AwakeEventBehavior.cs`, `src/AwakeTerminalBehavior.cs`, `src/WeeklyReportBrowserOverlay.cs`, `src/WeeklyReportBrowserVM.cs`, `src/WeeklyReportService.cs`, both language XML files, and focused tests/SDK smoke only. `WorldStateStore.cs`, `AwakeRuntime.cs`, `ProbeExtension.cs`, Marcus framework, content packages, Persona, dialogue, and event rules are excluded unless review proves a direct compile-time seam is unavoidable.

## 5. Acceptance and evidence

| Case | Required proof |
| --- | --- |
| Native unavailable | Focused test proves a current installed store receives/reuses a completed v1 report while `IsNativeKnowledgeReady()` is false. |
| Event engine disabled | Focused test proves the report scheduler remains reachable while `EnableEventEngine` is false; no popup/event-rule call is required. |
| Same week | Focused test proves reopening or repeated hourly scheduling reuses the saved report ID and does not create a second report state. |
| Late save | Focused test proves day N only considers `CompletedWindowEnds(N).Last()` and does not write earlier missing weeks. |
| Menu states | Focused test proves formal, preview, and unavailable results reach the Browser VM with the correct title/status/text. |
| Failure honesty | A failed storage readiness/read produces unavailable UI/log output, not an empty preview or formal report. |
| Compatibility | Existing v1 report fixtures and save/read behavior remain valid; no storage key, schema, or report ID migration occurs. |
| Offline quality | Targeted tests, project build, SDK smoke, and localization parse pass. This is E2 only. |
| Game validation | After explicit sync authorization, user verifies menu open, one completed-week generation, and save/load reuse against the new BuildId. These are E4/E5 and are not claimed by offline tests. |

## 6. Explicit non-goals

- No new facts, random events, event rules, diplomacy/battle observers, or event effects.
- No Worldbook content, content-package changes, Persona, NPC dialogue, AI call, or RAG work.
- No v2 report schema, content fingerprint, chunk storage, CAS/transaction work, retention redesign, or framework API changes.
- No synchronization, game launch, version bump, release, or change to existing unrelated dirty files.

## 7. Deferred risks

- Current fact retention is bounded. If real weekly reports lose needed facts, capacity is a separate evidence-driven batch.
- Existing restore currently waits for Native readiness. This MVP does not redesign restore; terminal player intent and already-ready background storage are the supported paths.
- Current state-store corruption classification is not redesigned here. Storage operation failures are displayed as unavailable; deeper repair semantics are deferred.

## 8. Gate

This is the sole plan for the MVP. One independent read-only review is next; a correction receives at most one targeted re-review. Only `VERDICT: APPROVED` plus explicit user sign-off authorizes code changes.
