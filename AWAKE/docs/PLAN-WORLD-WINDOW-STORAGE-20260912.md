# Plan: 世界事实按周存储底座（S0）

> Status: `blocked_by_f0_kv_contract`  
> Baseline: working tree BuildId `awake-20260911-g3b-persona-storage-001`, HEAD `b7eacaa4f679dfcdeb8a45b16337a35f60636a1f`; pre-existing dirty files must be fingerprinted before implementation.  
> Scope: AWAKE runtime persistence only. No weekly report generation, UI, worldbook, Persona, dialogue or event observer.

> F0 dependency: `contract_id=marcus.kv.semantics.v1`; `contract_path=docs/contracts/MARCUS-KV-SEMANTICS-v1.json`; required status `verified_e2`. This only supplies KV adapter semantics. S0 separately owns the typed raw-read caller and its generation-bound handoff test; S0's verifier rejects any additional KV semantic dependency or a non-verified F0 record.

## Goal

Provide one private, storage-only way to append, read and recover world facts by complete campaign-week window without relying on the existing 50-record memory cache or a single 512 KiB value.

Closed loop:

```text
normalized fact -> background access Evaluate -> week bundle write
-> manifest commit -> load by window -> typed result
```

## Contract

- Existing v1 `campaign.world_events.v1` state remains readable and untouched. No destructive migration or v1 rewrite.
- V2 has no global growing index. Window keys are derived from `endDay`:

```text
campaign.world_events.v2.week-{endDay}.manifest
campaign.world_events.v2.week-{endDay}.facts-{ordinal}
```

- Manifest is the only commit/visibility point. It contains schema, inclusive day bounds, phase, local revision, deterministic write-intent ID, at most 64 fact chunk references and their hashes; it holds no facts.
- Each chunk is at most 384 KiB serialized UTF-8. A 65th chunk fails closed with `awake.world_state.weekly_window_capacity_exceeded`; facts are not silently dropped.
- Query result is exactly `missing`, `empty`, `success`, `corrupt`, or `unavailable`. Invalid JSON, invalid fields, missing referenced chunk or hash mismatch are `corrupt`, never `empty`.
- A background path must use `PermissionGate.Evaluate` only. Denied, unavailable or indeterminate access returns `unavailable`; it never calls `EnsureAsync` or creates UI.

## Commit and recovery protocol

The host exposes only per-key Get/Set/Delete; it has no CAS or transaction. This batch supports one AWAKE writer in one campaign session and no external/multi-process writer.

1. Normalize a fact and select deterministic week/chunk key.
2. Write or verify the fact chunk; same key with different normalized hash is a conflict.
3. Read back and hash-verify every chunk referenced by the intended manifest.
4. Write the compact manifest last.
5. On unknown write result, reread manifest and chunks. Only an exact write-intent ID, revision, references and hashes is success.

An interruption before manifest visibility leaves deterministic reusable data; an interruption after manifest visibility must load as a complete hash-verified window. The storage layer may not create reports or report states.

## Files and ownership

Expected write set: `AiTaskConstants.cs`, `WorldStateStore.cs`, `WorldEventLedger.cs`, `WorldEventContracts.cs`, focused storage fixtures/tests, and a dedicated storage contract document if needed. `AwakeRuntime`, `ProbeExtension`, menus, localization and report code are excluded.

`WorldStateStore` owns key access, typed load outcomes, chunk/manifest integrity and recovery. `WorldEventLedger` owns fact normalization and callers' compatibility facade. No caller may read raw v2 keys directly.

## Acceptance

| Case | Observable result | Evidence |
| --- | --- | --- |
| Legacy read | Existing v1 state still loads unchanged through compatibility path | fixture test |
| Chunked write | >200 facts spanning several chunks load in stable ID/time order | focused test |
| Immutable append | Two successful appends leave the first manifest's payload hashes unchanged; same event concurrent retry is idempotent | focused concurrency test |
| Packed rollover | 64 referenced payloads compact into immutable packed chunks before the next append becomes visible | focused fault-injection test |
| Capacity | 65th chunk fails with the fixed code and no fact disappears | focused test |
| Crash before commit | Uncommitted chunks are invisible; retry verifies/reuses them | fault-injection test |
| Crash after commit | Manifest and all chunks reload complete | fault-injection test |
| Corruption | Missing/hash-mismatched chunk yields `corrupt`, not empty | negative test |
| Permission | Background call uses Evaluate only and denied access is `unavailable` | fake-gate test |
| Generation lease | V1 read and v2 write/load reuse the opened campaign generation; a generation-0 or mismatched context is rejected | generation-aware storage fixture |
| Session fence | Old generation cannot write or expose a new session window | cancellation/generation test |

## Non-goals

- No v2 weekly-report schema, report snapshot, report UI or localization.
- No Native Knowledge readiness, worldbook projection, game event callback or player-facing permission request.
- No long-term history browser, compaction, cloud/provider action, game sync or launch.

## Round 1 lock changes (supersedes conflicting earlier wording)

### Immutable append and key grammar

V2 never overwrites a chunk referenced by a committed manifest. Campaign day `d > 0` belongs to the inclusive window `[endDay - 6, endDay]`, where `endDay = 7 * ceil(d / 7)`; `endDay` is formatted as eight decimal digits and fact-chunk ordinals start at `0000`.

Every normalized fact has a stable `eventId`. Its delta key is immutable:

```text
campaign.world_events.v2.week-{endDay:D8}.delta-{SHA256(eventId)[0..15]}
```

The delta payload is canonical UTF-8 JSON containing exactly `schemaVersion`, `eventId`, `day`, `kind`, `domain`, `text`, `eventKey`, `occurredAt`, `visibilityIdentityIds`; properties are ordinally ordered, and identity IDs are ordinally sorted. Its uppercase SHA-256 is stored in the manifest. A fact payload larger than 384 KiB fails with `awake.world_state.world_fact_too_large` before any write.

One session-local writer serializes append intents by `eventId`. A manifest revision references immutable deltas and, when needed, immutable packed chunks:

```text
campaign.world_events.v2.week-{endDay:D8}.packed-r{revision:D8}-c{ordinal:D4}
```

Before a manifest would reference more than 64 payloads, the writer creates packed chunks from currently referenced deltas, verifies them, then commits a later manifest revision referencing packed chunks plus any remaining deltas. Only after that manifest is visible may dereferenced deltas be best-effort deleted; deletion failure leaves harmless orphans. Thus an append never changes data visible through an earlier manifest. Tests cover two sequential appends, competing same-event append, packed rollover and retry after interruption.

### Lifecycle, permissions and generation-bound context

S0's write set includes `AwakeRuntime.cs` and `ProbeExtension.cs`. `CampaignSessionReady` starts a separate storage-only task before the Native readiness continuation. That task uses a new background-only `EvaluateWorldStateStorageAccess(...)` seam; it calls `PermissionGate.Evaluate`, never `EnsureAsync`/`RequestAsync`, and exits `unavailable` on denial, absence or indeterminacy. Native continuation remains responsible only for Native-dependent work.

`WorldStateStore` captures the `RequestContext`/lease used to open its campaign namespaces and reuses that generation-bound context for all implicit Get/Set/Delete operations. It must reject a caller context whose campaign/session/generation differs from the captured lease. Focused fixtures use a generation-aware store that returns `storage.scope_denied` for generation `0` or any mismatched context; both v1 read and v2 write/load must pass with the real captured generation.

### Typed results, manifest schema and capacity details

The v2 manifest is canonical JSON with required fields `schemaVersion`, `windowStartDay`, `windowEndDay`, `phase`, `revision`, `writeIntentId`, `payloads`. `schemaVersion` is `awake.world-events.window.v2`; `phase` is one of `collecting`, `sealed`, `committed`; `revision` is a positive integer; `payloads` is an ordinally ordered array of `{ key, sha256, kind }`, with `kind` `delta` or `packed` and at most 64 items. `writeIntentId` is the uppercase SHA-256 of `campaignId|sessionGeneration|endDay|revision|orderedPayloadHashes`.

The exact result mapping is: absent key (`Succeeded(null)`) is `missing`; syntactically valid committed manifest with zero payloads is `empty`; valid committed manifest with verified payloads is `success`; null/empty/whitespace document, invalid JSON/shape/phase, collecting/sealed manifest, missing payload or hash mismatch is `corrupt`; denied permission, namespace open failure, non-not-found Get failure or stale session is `unavailable`.

Serialized payload size is measured as `Encoding.UTF8.GetByteCount(canonicalJson)`. A delta/packed payload must be `<= 393216` bytes. The writer chooses the first available ordinal that keeps the manifest at no more than 64 payloads; after packing cannot produce a valid 64-payload manifest, it returns `awake.world_state.weekly_window_capacity_exceeded` without dropping the incoming fact.

## Gate

`plan_status = revised_after_review_round_3`  
`review_status = revision_required; review_cap_exhausted`  
`user_signoff_required = yes, after APPROVED`  
`minimum_evidence = E2; E5 is required before save/load completion is claimed`  

Blocked by user-directed prerequisite split: `PLAN-MARCUS-KV-SEMANTICS-F0-20260912.md` owns the atomic-replace and missing-key behavioral contract. S0 may not resume review or implementation until F0 has passed its own approval and E2 verification.

## Round 2 lock changes (supersedes conflicting earlier wording)

### Restore consumes the installed storage result

`CampaignSessionReady` creates exactly one generation-keyed storage task, `EnsureBackgroundWorldStateStoreAsync`. It evaluates storage permission, opens namespaces with a captured lease and installs the store only on a successful current-generation result. `RestoreCampaignStateAsync` and Native-ready continuation consume that task/result; neither may call `EnsureWorldStateReadyAsync`, `PermissionGate.EnsureAsync` or `RequestAsync`. If the task result is unavailable, restore records the result and exits without retrying through an interactive path. A fake permission service asserts `RequestAsync` call count is zero for the complete CampaignSessionReady → restore path.

### Immutable payload schemas and retry matrix

Delta JSON remains the exact single-fact canonical object in section 75. Packed JSON has exactly `schemaVersion`, `windowStartDay`, `windowEndDay`, `sourceDeltaHashes`, `facts`; `schemaVersion` is `awake.world-events.packed.v2`. `facts` contains canonical delta objects sorted by `(day, eventId)` ordinally; `sourceDeltaHashes` is the ordinally sorted, duplicate-free closure of those facts' delta hashes. Packing greedily takes the longest ordered prefix that fits `393216` UTF-8 bytes; it must contain at least one fact. A packed chunk and every referenced delta are schema-validated and hash-verified before manifest commit.

For an unknown Set result: unknown delta → Get and require exact canonical hash before continuing; unknown packed → Get and require exact canonical hash plus source-delta closure; unknown live manifest → Get and require exact committed manifest hash/intent/revision. Any other result is retryable/failed, never success. Tests inject each unknown result separately.

### Live manifest is committed-only

The live key `campaign.world_events.v2.week-{endDay:D8}.manifest` is absent or contains only a fully formed `committed` manifest. `collecting` and `sealed` intent records, if persisted for diagnostics, use `campaign.world_events.v2.week-{endDay:D8}.intent-{writeIntentId}` and are never read as live state. Before implementation, the active `IKeyValueStore` adapters must demonstrate atomic replace semantics for one key (reader observes old complete value or new complete value, never a partial value); otherwise S0 is blocked pending a separate framework storage-contract batch.

### Lease, window and duplicate invariants

`WorldStateStore` stores a durable `WorldStorageLease` made of campaign ID, timeline ID, session ID and session generation, not a temporary `RequestContext`. Every implicit operation creates a fresh deadline-bearing context from that lease and rejects generation 0 or any caller/lease mismatch with `storage.scope_denied`.

For every live manifest, `windowEndDay` equals the eight-digit endDay embedded in its key, `windowStartDay == windowEndDay - 6`, and every delta/packed fact has `day > 0` within those bounds. Before any capacity calculation, append checks whether the eventId is already committed: equal canonical fact/hash is an idempotent success; different content is `awake.world_state.world_fact_conflict`. Only a new fact can receive a capacity error.

If `eventKey` is supplied, event ID is `awake:event:` plus uppercase SHA-256 of `key|eventKey`. Otherwise it is `awake:event:` plus uppercase SHA-256 of the full canonical fact excluding eventId, including day, normalized kind/domain, text, occurredAt and sorted visibility identities. Thus same text with a different time or visibility is distinct; same supplied event key with altered content conflicts.

### Additional acceptance

| Case | Required observation |
| --- | --- |
| Restore permission | Complete CampaignSessionReady → Native-ready restore makes zero `RequestAsync`/`EnsureAsync` calls. |
| Packed validation | Invalid packed order, duplicate source hash, absent source closure or invalid fact is rejected before commit. |
| Live-manifest fault | A collecting/sealed intent is never returned as live; reader observes old committed or new committed manifest only. |
| Fresh lease | Operations after an open-context deadline use a newly created matching-generation context; mismatched/zero generation is rejected. |
| Full-window check | Key bounds and every fact day are verified before `success`. |
| Full-capacity duplicate | A repeated committed fact succeeds idempotently even when 64 payload references are already present. |

## Round 3 lock changes (supersedes conflicting earlier wording)

### Framework atomic-replace prerequisite

S0 cannot assume atomic replacement from the existing `IKeyValueStore` signature. Before implementation, a versioned Marcus framework contract record must declare the behavioral semantic: a successful `SetAsync(key, value)` makes readers of that key observe either the complete previous value or the complete new value, never a partial/truncated intermediate value. The record names the public declaration, default SQLite adapter, injected/file adapter, existing callers and deterministic verification.

The framework prerequisite is exclusively `docs/contracts/MARCUS-KV-SEMANTICS-v1.json` under `PLAN-MARCUS-KV-SEMANTICS-F0-20260912.md`. S0 does not own framework API or adapter writes. It remains blocked until F0 is approved and E2-verified; then S0 consumes the verified adapter semantics through a typed raw-read path and may begin a fresh review budget.

### Manifest ordering, packing and duplicate lookup

Every manifest payload entry is exactly `{ ordinal, kind, key, sha256 }`. `ordinal` is a zero-based integer, unique and contiguous. Entries are ordered by ordinal. For a new manifest revision, the writer first expands every referenced delta and packed fact, validates their schemas/hashes, deduplicates by eventId, then orders all facts by `(day, eventId)` ordinally. It greedily packs that complete ordered fact sequence; resulting packed chunks receive ordinals in output order. A delta may remain only when a one-fact delta is itself the deterministic packed output for that fact; mixed entries still use the same contiguous output ordinal rule. `writeIntentId` hashes the complete canonical ordered manifest, not an unordered hash list.

On every rollover—including one whose previous manifest already contains packed chunks—the writer expands the whole committed fact set and writes a new immutable packed set for the next revision. It never “packs only current deltas”. Duplicate lookup likewise scans the complete expanded delta+packed fact set before capacity calculation; at 64 entries an equal eventId/canonical fact is idempotent and a different one conflicts before any capacity decision. Tests cover two rollovers and a full 64-entry packed manifest followed by duplicate retry.

### Strict key, window and identity validation

Live manifest keys must match `campaign.world_events.v2.week-[0-9]{8}.manifest`; `windowEndDay` is an integer multiple of seven and at least seven; `windowStartDay == windowEndDay - 6` and is at least one. All delta/packed facts have positive integer days within those inclusive bounds. Invalid, overflowed or mismatched keys/bounds are `corrupt`, including a zero-payload `week-00000000`; only a valid window with end day at least seven and zero payloads is `empty`.

There is one `WorldFactCanonicalizer`. It applies Unicode NFC to input strings, lowercases normalized kind/domain invariantly, preserves normalized text after existing length validation, trims eventKey/identity IDs, de-duplicates and ordinally sorts identities, and serializes compact JSON using ordinal property order, invariant numeric formatting and JSON default escaping. `occurredAt` is converted to UTC and rendered exactly `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`.

For a supplied event key, event ID is `awake:event:` + uppercase SHA-256 of UTF-8 `awake:event-key:v2\0{normalizedEventKey}`. Otherwise it is `awake:event:` + uppercase SHA-256 of the canonical fact excluding eventId. V2 read validates that the stored eventId equals this derivation; invalid IDs are `corrupt` and are never silently regenerated.
