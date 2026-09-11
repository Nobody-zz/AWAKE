# AWAKE Runtime / Core Content Repair — 2026-08-31

## Scope

Repair confirmed runtime lifecycle, persistence, and core content-boundary risks from the 2026-08-31 read-only audit. Current candidate awake-20260829-marcus-embedded-002 remains frozen; this batch must create a new BuildId.

## Confirmed facts

- World inbox loading synchronously waits for asynchronous storage.
- Dialogue, messenger, proactive, onboarding, event queue, and command paths can outlive their originating session or selection.
- Some persistence paths can report success before durable application or remove reservations before final settlement.
- Restore work can start before Runtime and Storage readiness.
- Some async paths may retain or reacquire live Campaign, Hero, or Settlement objects across awaits.
- Runtime Service dispatch drain is a separate cross-process boundary and can outlive a timeout.
- Request and Provider idempotency ledgers need bounded retention and restart semantics.
- Core source and localization contain content-pack-only goddess, oracle, favor, and estrus defaults.

## Locked decisions

- Existing save keys and serialized schemas remain unchanged. If implementation requires a schema or save-key change, stop and split a separate save-compatibility batch.
- Runtime work is bound to a captured session generation, expected Store, owner, and correlation identity.
- Live TaleWorlds objects do not cross asynchronous waits; stable IDs or immutable snapshots cross the boundary and objects are resolved again on the game thread before mutation.
- Core content isolation removes default entries and wording, not generic capability seams or ordinary religious worldbook knowledge.
- Existing save schemas remain authoritative: in-flight reservations are either durably represented by existing compatible records or conservatively recovered as Unknown, never silently treated as Applied.

## Acceptance contract

1. UI and campaign callbacks never synchronously wait for world storage. Loading completes asynchronously and posts observable results through the game-thread dispatcher.
2. Work started in session N cannot mutate session N+1. Generation and expected-Store checks run at task start, after waits, and immediately before mutation.
3. Messenger history and send callbacks use contact generation and active-contact keys so stale results cannot overwrite the selected contact.
4. Session end cancels and observes tracked module work. Unobserved exceptions are logged with owner and correlation identity.
5. Persistence exposes Reserved, Enqueued, Applied, Retryable, and Unknown outcomes. Enqueue-only success is not reported as durable success, and reservations survive until terminal settlement.
6. Restore starts only after RuntimeReady and StorageReady, is idempotent, and old-session restore cannot overwrite new-session state.
7. Runtime Service dispatch drain is cancelled and observed; timeout is reported as incomplete or unknown, never success.
8. Event trigger counters and cooldowns commit only after the observable event is accepted for display, or are explicitly rolled back on display failure.
9. AI-derived event knowledge retains provenance, access scope, and epistemic status; unapproved candidates are not published as player-known facts.
10. Request and Provider idempotency ledgers have bounded TTL/capacity, remove rejected-request ghosts, reclaim timed-out in-progress entries, and classify restart duplicates as Applied, Retryable, or Unknown without duplicate settlement.
11. Save keys, default values, and reference-rebuild behavior remain compatible with prior saves. Reserved and Enqueued work is durably recoverable through existing compatible records or conservatively mapped to Unknown; it is never silently treated as Applied. Fixtures cover old save defaults, repeated entry, session switch, stale completion, and restart recovery.
12. Runtime Service drain records connection close, parent exit, Provider slow response, duplicate submission, and late completion as explicit outcomes; no timed-out task can use released dependencies.
13. Without an optional content package, core exposes no goddess, oracle, favor, or estrus entry, default wording, or default output. With a registered optional capability, the extension remains reachable. Generic dialogue, identity, relationship, world-effect, and world-event behavior remains available.
14. Focused tests, XML/JSON parsing, Release build, save compatibility checks, bounded-ledger checks, and AWAKE static boundary checks pass. This batch does not start Bannerlord, call a real Provider, or sync the game directory.

## Non-goals

- Worldbook Studio R17 implementation.
- UI Lab Host and production ownership repair.
- Persona Workbench contract repair.
- Slanesh content package implementation.
- Provider routing, credential, and SQLite migration P2 items.

## Implementation order

1. Add session-scoped task supervision and stale-write guards.
2. Convert world inbox and restore paths to readiness-gated non-blocking dispatch.
3. Guard persistence settlement and Runtime Service drain outcomes.
4. Rework async boundaries around immutable snapshots and game-thread re-resolution.
5. Correct event trigger and event-knowledge settlement order.
6. Bound request and Provider ledgers with restart-safe outcome semantics.
7. Remove core content-only defaults while preserving optional capability seams.
8. Run focused tests and broader offline gates, then assign a new BuildId.
