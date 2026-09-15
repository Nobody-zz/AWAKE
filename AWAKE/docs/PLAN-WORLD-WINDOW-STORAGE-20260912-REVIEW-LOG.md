# Review Log: 世界事实按周存储底座（S0）

## Round 1 — 2026-09-12

Scope: storage-only plan, direct storage/runtime lifecycle/permission callers and minimal framework storage API. Read-only; no build, sync or game launch.

Result: `VERDICT: REVISE`

| Finding | Severity | Revision made |
| --- | --- | --- |
| Mutable `facts-{ordinal}` conflicts with immutable manifest references | P0 | Added immutable event-ID delta keys, immutable packed chunks, serial append and rollover/compaction rules. |
| Evaluate-only requirement excluded the actual runtime/lifecycle callers | P0 | Added `AwakeRuntime.cs` and `ProbeExtension.cs`; CampaignSessionReady begins independent Evaluate-only storage recovery. |
| Implicit store context can use generation 0 after a generation-bound namespace open | P0 | Store captures/reuses the opened context and adds a generation-aware storage fixture. |
| Typed outcomes/window/key/manifest/capacity semantics were underspecified | P1 | Locked day formula, key grammar, manifest schema, `missing/empty/corrupt/unavailable`, byte measurement and error codes. |
| Plan HEAD was stale | P1 | Baseline updated to `b7eacaa4f679dfcdeb8a45b16337a35f60636a1f`. |

Revision evidence: plan SHA-256 `2C898AAC04BC32C717EBA2CC433309D3EF46D7B214ACB794297C99242D6D90A0`.

Next action: independent read-only round 2. No implementation is authorized before `APPROVED` and user sign-off.

## Round 2 — 2026-09-12

Scope: round-1 revision, direct Restore/runtime/permission paths, storage context and manifest/chunk protocol. Read-only; no build, sync or game launch.

Result: `VERDICT: REVISE`

| Finding | Severity | Revision made |
| --- | --- | --- |
| Native-ready restore could still call `EnsureAsync` | P0 | Restore now consumes the one Evaluate-only generation-keyed storage task/result; a zero-`RequestAsync` end-to-end assertion is required. |
| Packed payload lacked a schema and retry proof | P1 | Locked packed fields, sorting, source closure, greedy packing and delta/pack/manifest unknown-write matrix. |
| Noncommitted live phase could hide a prior committed manifest | P1 | Live key is committed-only; intent state uses separate keys; per-key atomic-replace behavior is an implementation prerequisite. |
| Reusing an open RequestContext could reuse an expired deadline | P1 | Store retains a lease/generation identity and creates a fresh operation context each time. |
| Window/key/capacity duplicate order was incomplete | P1 | Added bounds checks and duplicate-before-capacity rule. |
| eventId could merge different same-text facts | P1 | Locked supplied-key and full-canonical-fact ID derivation. |

Revision evidence: plan SHA-256 `41AC1B8FD7A17E5FE83CC935E78C58437A0DC4FDF2EE85C7A39B1D0066C35A7D`.

Next action: independent read-only round 3, the high-risk final review round. No implementation is authorized before `APPROVED` and user sign-off.

## Round 3 — 2026-09-12

Scope: final high-risk review of the S0 plan, manifest protocol, storage API/adapters and direct runtime boundaries. Read-only; no build, sync or game launch.

Result: `VERDICT: REVISE_CURRENT_SLICE`

| Finding | Severity | Revision made |
| --- | --- | --- |
| `SetAsync` atomic replacement was assumed but not a public/adaptor-proven contract | P1 | Added a Marcus framework contract-record and default/injected adapter proof as an S0 prerequisite; otherwise S0 blocks. |
| Payload order/ordinal, repeat rollover and packed-only duplicate search were incomplete | P1 | Locked contiguous manifest ordinal, expand-all/repack-all rollover, full duplicate scan and corresponding tests. |
| Invalid zero window could be returned as empty | P1 | Locked key grammar, end/start lower bounds and mismatch/overflow as corrupt. |
| event identity/canonical string rendering remained ambiguous | P1 | Added one canonicalizer, NFC/UTC/escaping rules, exact event-key delimiter and fail-closed v2 ID validation. |
| Header review state lagged the actual round | P1 | Plan and review state now mark the review cap exhausted. |

Revision evidence: plan SHA-256 `E440B38ECB522D761A0500618F5D7304C077486F4703AA23CAD1E2E105B6879E`.

Review budget: the three high-risk rounds are exhausted. The user directed a prerequisite split on 2026-09-12. S0 is blocked by `PLAN-MARCUS-KV-SEMANTICS-F0-20260912.md`; it is not approved and cannot resume until F0 is approved and E2-verified.
