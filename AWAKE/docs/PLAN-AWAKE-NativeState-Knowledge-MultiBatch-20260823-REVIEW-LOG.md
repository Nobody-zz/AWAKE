# Plan Review Log: AWAKE 原版状态与 NPC 世界知识多批次边界

Act 1 (grill) complete — the user authorized the recommended D1–D6 decisions as a whole on 2026-08-23. MAX_ROUNDS=5.

## Act 1 — Decision record

- D1: NPC formal runtime does not use the legacy `KnowledgeService`; any retained fallback is explicit, permission-gated, observable and fail-closed.
- D2: v1 stores player-taught typed knowledge in `awake.npc.memories`; a separate namespace requires profiling and a migration plan.
- D3: weekly reports are presentation projections only; NPC propagation consumes structured events/windows with visibility, location, identity and durability gates.
- D4: Campaign Storage/`WorldStateStore` is the runtime authority for mutable Overlay state; CAS import/export remains available.
- D5: v1 native social snapshot is player → current conversation NPC; DTOs retain source/target fields for a later NPC-NPC batch.
- D6: native relation writeback is a later independent batch after read-only integration and E4/E5 stability; it is not part of the first implementation wave.

## Act 2 — Independent review

The reviewer remains read-only and attacks:

1. authority and permission bypasses;
2. save/load schema and migration safety;
3. lifecycle ordering, async single-flight and backpressure;
4. event visibility, propagation fan-out and knowledge pollution;
5. AI/Token/CPU hard limits and observable acceptance evidence.

### Round 1 — Runtime/storage reviewer

Reviewer: `Carver` (`01a0301e-602f-7f62-a9c6-afa7837ef82e`)

Result: `VERDICT: REVISE`

- `B1` needed a shared readiness future, explicit failed/cancelled states, retry policy and recovery ordering.
- Storage bounded queue, backpressure, single-flight writes and shutdown drain needed to be a prerequisite rather than a late B7 concern.
- `B9` needed an explicit candidate snapshot scope because v1 freezes native relation snapshots to player → current NPC.
- Typed memory reuse needed versioned schema, legacy-read mapping, capacity/eviction, retry and round-trip fixtures.
- `B10` needed version-matched `ChangeRelationAction` evidence and an explicit failure/compensation matrix.

Action: incorporated into `B1`, new `B1-S`, `B7`, `B9`, `B10` and `B11-R`/`B11-F` boundaries.

### Round 1 — Knowledge/AI reviewer

Reviewer: `Halley` (`01a0301e-60d1-77b3-acaf-2be139853015`)

Result: `VERDICT: REVISE`

- Formal NPC dialogue could still reach legacy `KnowledgeService`; v2 missing/failure must be fail-closed and legacy must be offline-only.
- `blocked/referral/state` existed in the query model but was discarded before Prompt compilation; the full `KnowledgeDecision` must remain an AI hard gate.
- Player teaching lacked a runtime `ClaimDecision` contract; the plan now freezes it in B0 and leaves final scoring to code.
- Event records lacked visibility, location, target and stable propagation/idempotency fields; these must be frozen in B0 before B6.
- B5/B6 and B7 had a circular dependency; shared schemas now belong to B0, while B7 only persists them.
- Read-only E4/E5 must precede optional relation writeback; B11 is split into `B11-R` and `B11-F`.

Action: incorporated into `B0`, `B2`, `B5`, `B6`, `B7`, dependency graph and execution order.

### Review transport limitation

The prescribed external `codex exec` reviewer was attempted once and was blocked by the desktop sandbox policy before producing a result. It was not retried; the two independent read-only subagent reviews above are the available Act 2 evidence for this revision.

Status after revision: `REVISE_PENDING_SECOND_REVIEW`

No code, build, synchronization, game launch or frozen-candidate modification is authorized by this plan review.

### Round 2 — Runtime/storage reviewer

Reviewer: `Carver` (`01a0301e-602f-7f62-a9c6-afa7837ef82e`)

Result: `VERDICT: REVISE`

- Closed: B1-S readiness/backpressure, B9 native snapshot scope, B10 API/compensation gate and B11-R/B11-F order.
- Remaining: first implementation lease was still ambiguous (`B1+B1-S+B2` versus one batch); ClaimDecision status had no exact typed-memory mapping; B9 idempotency key/conflict strategy and readiness-owner ownership were not explicit; checkpoint lacked an explicit `REVISE` branch.

Action: added a normative B0 schema, exact claim-to-memory mapping, `campaign/hour/source/target/candidate/revision` CAS key, `AwakeRuntime` readiness ownership, B1-only first lease and checkpoint branch requirements.

### Round 2 — Knowledge/AI reviewer

Reviewer: `Halley` (`01a0301e-60d1-77b3-acaf-2be139853015`)

Result: `VERDICT: REVISE`

- Closed: shared B0 contracts, v2-only/fail-closed intent, AI hard gates and B11-R/B11-F split.
- Remaining: schemas were still too permissive, formal initialization of the legacy runtime was not explicitly forbidden, referral state needed a total `AI=0` matrix, B5/B6/B7 dependency wording needed clarification, D6 needed `E4-R/E5-R` versus `E4-F/E5-F` terminology, and `weekly_digest` could blur the event-window boundary.

Action: froze normative fields and enums in B0, prohibited formal legacy initialization, made all `state=referral` branches `AI=0`, clarified B7 implementation versus E5-R evidence, renamed the source to `event_window_projection`, and split final evidence names.

Status after revision: `REVISE_PENDING_FINAL_REVIEW`

No code, build, synchronization, game launch or frozen-candidate modification is authorized by this plan review.

### Round 3 — Final sign-off review

Reviewers: `Carver` (`01a0301e-602f-7f62-a9c6-afa7837ef82e`) and `Halley` (`01a0301e-60d1-77b3-acaf-2be139853015`)

Result: `VERDICT: APPROVED`

- The final B1-S evidence label was corrected to `B11-R E5-R`; it cannot borrow `B11-F E5-F`.
- No remaining material plan defect was found that makes a B1-only implementation lease unsafe.
- Approval is limited to the locked boundary plan and the first B1 implementation gate; it is not a claim of code, build, sync, gameplay or save/load completion.

Final review status: `APPROVED_FOR_B1_ONLY`

No code, build, synchronization, game launch or frozen-candidate modification was performed during this review.
