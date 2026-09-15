# Plan: 世界事实采集可靠性补丁

> Status: `superseded_by_user_direction`
> Revision: `R2`
> Baseline candidate: `awake-20260912-world-fact-collectors-mvp-001`, synchronized at E3 but not game-validated.

> Superseded before implementation: the user chose all valid hero facts to be collected and selectively queried, rather than filtering ordinary heroes at capture time. This document must not be signed off or implemented.

## 1. Goal

Make the existing five-event collector quiet enough for ordinary play and less likely to lose facts during the brief period before campaign storage becomes available.

This is a hardening patch for the core collector, not a new event system.

## 2. Locked behavior

| Topic | Decision |
| --- | --- |
| War, peace, settlement ownership | Continue recording every valid scoped event. |
| Hero death / prisoner release | Record only when the hero is a lord (`IsLord`) or the player's companion (`IsPlayerCompanion`). Ordinary notables and other heroes are ignored with a structured filter log. |
| No current store | Keep the immutable fact in a per-behaviour in-memory queue rather than immediately dropping it. No permission/readiness call is permitted. |
| Buffer capacity | Maximum 32 facts per campaign-behaviour instance. Preserve earlier queued facts; when full, reject the new fact and log `awake_world_fact_capture_dropped_full`. |
| Buffer duplicate | The same `eventKey` is held once. A duplicate callback before storage is available does not consume another slot. |
| Flush | The collector subscribes to `HourlyTickEvent`. When a current store exists, it starts at most one background flush for the captured campaign generation. It calls the existing recorder's observable `RecordAsyncForCampaign` route for each buffered fact. Remove a fact only after `Persisted` or `DuplicateConfirmed`; retain it after retryable, unknown, failed or stale results. |
| Session/save boundary | The buffer is intentionally not saved. It is short-lived protection for the readiness gap, not a second campaign-state store. Facts still held when the campaign ends or the process exits are lost and logged; durable recovery is out of scope. |
| MCM | No setting. Capacity and importance policy are fail-safe implementation limits, not player-tunable gameplay rules in this first hardening patch. |

## 3. Closed loop

```text
Campaign event callback
  -> hero importance gate where relevant
  -> scalar snapshot / deterministic event key
  -> current store? QueueRecord : bounded in-memory buffer
  -> HourlyTick sees current store
  -> QueueRecord existing ledger route
  -> 近期动态 / 本周动态
```

The queue holds `WorldFactCapture` only: day, kind, text and event key. It never stores TaleWorlds objects. The flush captures generation/store before starting, exits on a stale session, and has an interlocked single-flight guard so hourly ticks cannot concurrently send the same buffered item. It does not call `EnsureWorldStateReadyAsync`, `PermissionGate`, `EnsureAsync`, disk I/O, or an AI service.

## 4. Required implementation

1. Add a small pure `WorldFactBuffer` with capacity, event-key de-duplication, accepted-remove and ordered snapshot/flush operations. Include it in the focused smoke project.
2. Give `AwakeWorldFactCollectorBehavior` one buffer, a lock, one hourly listener and one interlocked flush guard. Refactor its existing dispatch into: build scalar fact -> current-store `QueueRecord` or buffer.
3. Implement background flush with `WorldEventServices.Recorder.RecordAsyncForCampaign(..., capturedGeneration, capturedSessionToken)`. Only `Persisted` / `DuplicateConfirmed` remove the matching event key. All other statuses remain buffered and log the stable status; a stale store/session stops the flush without removal.
4. Gate only `HeroKilledEvent` and `HeroPrisonerReleased` on `hero.IsLord || hero.IsPlayerCompanion` before building the scalar fact. The callback must not retain `Hero` after this decision.
5. Log stable outcomes: `awake_world_fact_capture_filtered`, `awake_world_fact_capture_buffered`, `awake_world_fact_capture_flushed`, `awake_world_fact_capture_dropped_full`, and existing unavailable/error logs where applicable.
6. Change BuildId at implementation start. This produces a new candidate and supersedes the currently synchronized but unvalidated collector candidate; no E4/E5 claim transfers.

Expected write set: `src/AwakeWorldFactCollectorBehavior.cs`, new `src/WorldFactBuffer.cs`, focused smoke project/test, `src/AwakeConstants.cs`, and task docs/evidence. Excluded: `WorldStateStore.cs`, `WorldEventLedger.cs`, `AwakeRuntime.cs`, `AwakeEventBehavior.cs`, Marcus framework, save schema, Worldbook/Persona/dialogue, MCM, random event engine, and game files.

## 5. Acceptance

| Case | Evidence |
| --- | --- |
| Importance | Offline fixture proves lord and player companion are accepted; an ordinary hero is filtered before a fact is made or queued. |
| Buffer | Offline fixture proves no-store facts enter the buffer, preserve input order, and expose no live game object. |
| De-duplication | Same event key keeps one buffered fact. |
| Capacity | The 33rd distinct fact is rejected; first 32 remain in order. |
| Flush | A simulated `Persisted` / `DuplicateConfirmed` result removes the matching buffered fact; retryable, unknown, failed and stale results remain in order for a later hourly retry. |
| Single flight | A second hourly tick while a flush is in progress does not issue a second write for the same buffered fact. |
| Boundaries | Static test proves five original subscriptions remain, exactly one hourly flush subscription exists, observable `RecordAsyncForCampaign` is used for buffer flushing, and no readiness/permission call occurs. |
| Compatibility | Existing five fact templates/domain mappings and weekly-report fixtures remain valid. No storage or save-format migration. |
| E2 | Focused smoke, project build, `git diff --check`; SDK smoke result is reported separately if the unrelated Persona golden fixture is still failing. |
| E3–E5 | New candidate must be explicitly synchronized, then user verifies a lord fact, an ordinary-hero filter, pre-store buffering (if reproducible), weekly report, and save/load. |

## 6. Non-goals

- No new event types: no battle, siege, raid, diplomacy detail, player action, random event or Worldbook interpretation.
- No promise of durable recovery before storage is available.
- No report item ranking, summary compression or UI controls.

## 7. Gate

Round 1 revised the unsafe interpretation of `QueueRecord` success: buffer removal now requires a confirmed recorder result. One targeted re-review checks only observable flush/removal semantics and the single-flight guard. Code changes require `VERDICT: APPROVED` and explicit user sign-off.
