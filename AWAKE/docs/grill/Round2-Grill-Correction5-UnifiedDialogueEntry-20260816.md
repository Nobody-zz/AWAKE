1. [data/schema] Does the revised correction define the exact JSON shape for `awake.dialogue.session.v1`, including required fields, version, and max sizes? Recommended: Publish a schema contract with sessionId/targetId/entrySource/state/correlation/token/turns and explicit limits before implementation.
2. [data/schema] Is the session token a separate unforgeable value, and is it stored in every session record? Recommended: Generate a GUID token at acquisition and persist it in the record for token-close and takeover checks.
3. [data/schema] Are turn records part of the session schema with a stable turnId, role, day, location, and source, or only UI chat rows? Recommended: Add an ordered turns array with stable IDs and append-only semantics.
4. [data/schema] Does the revision separate serializable session state from runtime `NpcDialogueService` references? Recommended: Persist only serializable state and keep service references in coordinator runtime memory.
5. [data/schema] Does the persisted queue entry include idempotency/correlation/motive/expiry/source fields beyond heroId+hint? Recommended: Extend the queue schema with id, source, motive, state, day, expiry, and correlation.
6. [data/schema] Does “v1 migration” identify both legacy `awake.messenger.v1` and the target transcript schema, and is it read-only? Recommended: Define a legacy-to-transcript mapping and migration marker without deleting old data until verified.
7. [storage] Does unified transcript stay under the framework’s 512KB storage value limit? Recommended: Enforce byte caps and shard per contact/day/block instead of one campaign document.
8. [storage] Does history writing stop using whole-document read-modify-write under a single messenger key? Recommended: Use per-target or append-block keys in a dedicated transcript namespace.
9. [storage] Are transcript and queue writes routed through `WorldStateStore` commands with idempotency keys? Recommended: Add dedicated commands and `WorldStateKind` entries for session/transcript/queue writes.
10. [storage] Is final drain changed to flush session/transcript/queue writes before session end? Recommended: Include the new commands in `BeginSessionEnd`/`BeginFinalDrain` and log dropped counts.
11. [storage] Are fire-and-forget append tasks replaced by tracked or awaited writes? Recommended: Make transcript appends tracked with retry and failure ledger instead of `_ = Append...`.
12. [storage] Does storage apply idempotency across save/load and duplicate replay? Recommended: Preserve applied keys and per-line IDs, and dedupe by line/turn ID.
13. [storage] Are retention/pruning rules applied by bytes and pinned status, not only 200 lines? Recommended: Cap bytes and keep pinned/queued entries exempt from pruning.
14. [UI] Does the revised hub remove `NpcDialogueOverlay`/`NpcDialogueVM` and `AwakeMessengerOverlay`/`AwakeMessengerVM`, not just add a coordinator? Recommended: Delete or retire both old overlay/VM paths and route all launchers through one hub.
15. [UI] Does hub open accept an explicit target and suppress auto-select? Recommended: Require an explicit target payload for scene/encounter/event/proactive and auto-select only for messenger entry.
16. [UI] Does hub bind an explicit target even if `BuildContacts` excludes it? Recommended: Insert the current target as a synthetic contact if it is not already listed.
17. [UI] Does hub restore the exact underlying screen/menu on close? Recommended: Store return context including screen and menu ID, and restore it rather than falling through to map/native conversation.
18. [UI] Are input restrictions and focus handled uniformly for `MissionScreen` and encounter/map menus? Recommended: Centralize input save/restore and test held-T release and Escape from each context.
19. [UI] Is the fixed 1280x760 layout replaced with responsive min/max constraints? Recommended: Define responsive scaling or scroll for 1280x720 and ultrawide and verify no text overlap.
20. [UI] Is the right character card explicitly out of scope in this revision? Recommended: State Phase A scope as existing left contacts plus center chat unless the card is separately planned.
21. [performance] Is contact building capped/cached instead of enumerating all AliveHeroes on every open? Recommended: Cache a bounded contact list and resolve the explicit target first.
22. [performance] Is transcript loading lazy per selected contact? Recommended: Load only the selected target’s history/shard and do not parse the full chat document on hub open.
23. [performance] Does contact switching suspend/reuse one service instead of creating a new service per switch? Recommended: Keep one service per active target and avoid repeated memory/worldbook initialization.
24. [performance] Are stream deltas coalesced and bounded per frame? Recommended: Drain deltas per frame, cap stream text, and keep the UI event queue bounded.
25. [performance] Does event choice open the hub immediately rather than waiting for the next tick? Recommended: Drain/launch directly from `OnChoice` while keeping the event engine busy.
26. [token cost] Does the hub keep raw transcript out of the prompt and preserve the 12-turn prompt window? Recommended: Keep `NpcDialogueService.HistoryCapacity` as the prompt window and use transcript only for UI/migration.
27. [token cost] Are session/entry metadata additions within `MaxPromptUtf8Bytes=32768`? Recommended: Bound and assert metadata bytes before compile/submit.
28. [token cost] Does event discussion pass only bounded title/body/hint context? Recommended: Construct a bounded event block with fixed max bytes and never send the full event payload.
29. [token cost] Does proactive queue carry bounded motive/urgency metadata into the prompt? Recommended: Use short fixed-size structured fields and validate them before injection.
30. [token cost] Is opening hint moved into the session start payload and removed from the global single-slot context? Recommended: Carry the hint in the session payload and reduce global context to a migration shim.
31. [security] Is eligibility rechecked at hub open, contact switch, and each send? Recommended: Call `IsEligibleNpcTarget` at every turn and fail closed for stale/dead/underage targets.
32. [security] Is dynamic text sanitized before binding to `RichTextWidget`? Recommended: Escape markup or use plain text rendering for all player/AI/system content.
33. [security] Is player input validated by UTF-8 byte count, not only character count? Recommended: Check both text elements and UTF-8 bytes before prompt submission.
34. [security] Are permission and content-tier checks preserved for every unified entry? Recommended: Keep `PermissionGate` and `ContentTier="pure"` in hub/service for all entry sources.
35. [security] Can a stale overlay close a newer session? Recommended: Close by session token only and ignore source/target string matches.
36. [validation] Does the revision define valid session transitions and takeover rules for all entry sources? Recommended: Specify one-active-session rules and test each source transition.
37. [validation] Does Dispose/Switch invalidate in-flight async turn callbacks? Recommended: Bind callbacks to session token/generation and ignore them after dispose.
38. [validation] Does queue dedupe prevent proactive/event double-show after reload? Recommended: Dedupe by queue ID and persist accepted/consumed state.
39. [validation] Does SdkSmoke cover session takeover, close-by-token, migration, queue persistence, and hub state? Recommended: Add pure logic tests for these cases and require `PASS ALL`.
40. [validation] Does the revision define a fallback policy when hub load/open fails? Recommended: Define explicit fail-closed behavior per entry and do not silently fall through to native conversation for scene/encounter.
41. [validation] Are queue overflow and duplicate entries explicitly validated instead of silently dropped? Recommended: Add max-size, overflow, and dedupe validation with ledger feedback.
42. [save/load] Is an active session safely persisted or ended across save/load? Recommended: Persist pending session/queue before end and restore only as queued dialogue or fail closed.
43. [save/load] Does the event/proactive queue survive save/load and campaign reset? Recommended: Persist the queue in storage and clear it only on a new campaign, not on load.
44. [save/load] Do unnamed NPC IDs survive reload without agent-index drift? Recommended: Separate stable identity key from runtime agent locator and remap on load.
45. [save/load] Is transcript flushed before session end? Recommended: Await final drain and verify no pending transcript/queue writes before releasing the store.
46. [save/load] Does reload prevent replaying already-shown dialogue? Recommended: Persist shown/accepted state and idempotency keys so no duplicate messages after load.
47. [compatibility] Does the correction remain compatible with existing event JSON and proactive content schemas? Recommended: Keep `dialogueAction`/`discussionAction` loadable and add new session fields backward-compatibly.
48. [edge cases] Does the hub fail closed in unsupported, non-campaign, or multiplayer contexts? Recommended: Require `Campaign.Current`, host, and storage before creating hub/service.
49. [simpler alternative] Is a new session abstraction needed beyond upgrading the coordinator to own `NpcDialogueService` and a persisted transcript? Recommended: Extend the existing service/coordinator rather than introducing a parallel service layer.
50. [simpler alternative] Should event inbox and weekly report be merged into the hub? Recommended: Keep them separate as the revised correction states; unify only dialogue-bearing entries.
VERDICT: REVISE