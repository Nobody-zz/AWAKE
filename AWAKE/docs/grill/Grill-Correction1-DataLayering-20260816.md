1. Q: Does `AwakeStorageContract` currently define a transcript schema separate from memory and messenger? A: Add explicit `awake.transcript.v1` and register it in `IsKnownSchema`/`ExpectedSchema`.
2. Q: Can current raw rows carry `day`, `location`, `pinned`, `source`, and `conversationId`? A: Extend `AwakeMessengerChatLine` and `ApplyMessenger` because today they only persist speaker/text/day plus a stored id.
3. Q: Is 30-game-day rollover implemented for raw transcripts? A: Implement explicit day-based plus count-based retention with pinned exemption; current `ApplyMessenger` only trims to 200 lines.
4. Q: Is transcript `pinned` semantically distinct from memory `weight == 3`? A: Keep pin state in the transcript schema and memory pinning in `awake.npc.memory.v1`; do not overload the same flag.
5. Q: Can 200 lines of up to 4000 chars fit under `AiTaskConstants.StorageValueMaximumBytes` of 512KB? A: No, split transcript data into per-contact keys or chunks so any JSON value stays under the limit.
6. Q: Does every transcript append read and rewrite the whole campaign messenger blob? A: Use per-contact append records or batching to avoid repeated full-blob read-modify-write.
7. Q: Does `TryNormalizeSchema` stop corrupt or mismatched transcript roots from being overwritten? A: Make schema mismatch a hard `awake.world_state.schema_mismatch` error instead of logging and continuing.
8. Q: Can existing `awake.messenger.v1` saves migrate to a transcript layer without loss? A: Add a one-way migration preserving existing ids/day/text and defaulting missing metadata fields.
9. Q: Does the current prompt path ever receive raw dialogue lines? A: Yes, `NpcDialoguePromptPipeline.SerializeHistory` injects up to `HistoryCapacity` raw entries, so replace it with a bounded session summary/memory block.
10. Q: Can raw history consume the 32768-byte prompt budget before `npc_memory` and `retrieved_knowledge`? A: Remove `dialogue_history` from truncation or place it last with guaranteed memory/knowledge budget first.
11. Q: Does `BuildMemorySummaryHint` send raw conversation tail to an AI summarizer? A: Keep summarization input to validated facts plus a small fixed-size hint, or explicitly gate raw-tail usage.
12. Q: Does `NpcMemoryConsolidator` actually generate summaries from raw transcripts? A: It only merges existing summary/facts, so add a separate transcript compressor/summarizer if raw logs must seed new memories.
13. Q: Can memory consolidation delete messenger rows through the current command path? A: Assert `ApplyMemory` never touches MessengerNamespace and add a test that consolidation leaves transcript storage unchanged.
14. Q: Is unpinned raw data rolled only after the compressed memory write succeeds? A: Require durable memory append/patch and an audit event before any unpinned transcript roll.
15. Q: Are raw lines linked to their memory entries by conversation ID? A: Persist `conversationId` on both raw line and memory entry so replay and compression are traceable.
16. Q: Is relationship state derived directly from raw transcript text? A: It must come from validated `awake.relationship.delta.v1` command results; raw transcript is evidence, not the source of truth.
17. Q: Does the current relationship schema contain location and recent events for the profile card? A: Keep per-hero relationship state and campaign event store separate and compose them in the UI instead of duplicating fields.
18. Q: Are transcript writes awaited or guaranteed flushed before session end? A: Await/queue transcript writes through `WorldStateStore` and verify final drain before releasing the store.
19. Q: Does messenger opening block the UI by calling `LoadAsync(...).GetAwaiter().GetResult()`? A: Make history loading async with a loading state and lazy selected-contact loading.
20. Q: Are in-memory transcript caches invalidated on campaign/session change? A: Reset and reload on `CampaignSessionReady` and key caches by session/campaign.
21. Q: Does the 256-entry `AppliedKeysMaximum` ledger still dedupe transcript writes after heavy use? A: Use per-line stable IDs plus transcript-specific dedupe metadata, not the global 256-entry ledger.
22. Q: Are speaker/role/text values validated before raw append? A: Reject unknown roles, empty/overlong text, and unmarked system/failure lines.
23. Q: Are malformed stored transcript lines handled safely during load? A: Validate and skip malformed lines with corruption logging, not crash or silently overwrite the document.
24. Q: Can raw transcripts enter knowledge/RAG/worldbook retrieval? A: Exclude the transcript namespace from all knowledge queries; only memory summaries may be retrieved.
25. Q: Is raw transcript cloud export blocked by default? A: Apply `CloudExportPolicy` and content-policy gating before any transcript-backed summary call, with raw logs default no-export.
26. Q: Are optional/adult transcripts filtered in pure default mode? A: Gate transcript-backed UI/summarization through the existing ContentPolicy so pure runtime never sees optional adult content.
27. Q: Does the contact panel show raw history and memory summaries as distinct UI layers? A: Add a memory-summary card from `NpcMemoryService` and a separate raw-history tab.
28. Q: Does the current messenger expose pin, rollover, or compression status? A: Add pin toggle, day/location metadata, compression status, and developer-only delete/export/undo.
29. Q: Does the 100-row `AwakeMessengerVM` UI cap conflict with 200-line storage retention? A: Paginate or virtualize history so the UI limit never implies or enforces storage deletion.
30. Q: Do direct `NpcDialogueOverlay` conversations persist raw transcripts like messenger does? A: Route every dialogue entry source through the same transcript service; direct overlay currently does not persist raw history.
31. Q: Are failed/system turns stored as valid dialogue transcript? A: Store only completed turns in raw transcript or mark `kind=system/failed` so compression ignores them.
32. Q: Does memory consolidation risk running for every alive hero and stalling the tick? A: Keep a per-day nearby cap and retry budget similar to the existing 8-hero daily consolidation limit.
33. Q: Are transcript load/summarize/drain operations async with deadlines? A: Use async, cancellation, and timeouts like existing AI calls; never block the UI on disk or network.
34. Q: Can unnamed NPC `StableId` values containing `:a<agentIndex>` be durable transcript keys? A: No, agent-index IDs are ephemeral; store transcripts only for heroes or character-scoped stable keys.
35. Q: Should scene shouts be written into per-NPC contact transcripts? A: Exclude scene shouts or store them under `scene:current` with a non-memory kind.
36. Q: Is there a pinned-transcript cap and an explicit unpin path? A: Define a finite pinned cap and explicit unpin/manual delete; pinned lines are exempt only while pinned.
37. Q: Does rollover handle day 0, negative days, or campaign-time changes? A: Validate nonnegative monotonic game day and roll from save state, not wall clock or in-memory day.
38. Q: Can loading an older save delete newer transcripts by current-day rollover? A: Make transcript operations append/idempotent per saved timeline and never roll destructively from a current cache.
39. Q: Does adding a transcript schema force a module version bump? A: No, keep the version tied to playable acceptance; schema migration can be internal and additive.
40. Q: Should `WorldStateKind` add Transcript instead of overloading Messenger? A: Add explicit transcript kind, namespace, key, and command IDs for clean contracts and migration.
41. Q: Is raw transcript scoped to the local player/campaign save? A: Use the framework campaign namespace and player/session ownership; never treat it as shared world state.
42. Q: Can UI, memory, and proactive writes interleave transcript appends? A: Reuse `WorldStateStore` drain serialization and per-line idempotency so appends cannot interleave or duplicate.
43. Q: Does every NPC need 200 raw lines? A: Start hero-only or use a lower global cap to reduce storage, UI, and token risk; unnamed NPCs can remain memory-only.
44. Q: Can raw transcripts be player-facing only? A: Yes, that is the core correction: make transcript non-retrievable and memory/relationship the only prompt sources.
45. Q: Is there a test that memory consolidation leaves transcript rows and schema unchanged? A: Add a smoke/unit test that appends raw lines, consolidates memory, and asserts transcript count/schema are unchanged.
46. Q: Is there a test that the NPC prompt contains no raw transcript history? A: Add a prompt-builder unit test asserting raw history is absent/replaced and total bytes stay under 32768.
47. Q: Is there a test that pinned raw lines survive rollover and memory compression? A: Add retention tests for pin exemption, day/count rollover, and manual delete/undo.
48. Q: Is there a test for transcript save/load after session end? A: Add a lifecycle test that queued lines flush, reload with the same ids, and produce no duplicates after retry.
49. Q: Are rolled transcripts audited instead of silently deleted? A: Write an audit/developer record with rolled IDs, reason, and linked memory summary IDs.
50. Q: Can developers inspect the three-layer separation at runtime? A: Add a developer report showing raw transcript, memory, and relationship counts, schema versions, pin counts, and last compression.
VERDICT: REVISE