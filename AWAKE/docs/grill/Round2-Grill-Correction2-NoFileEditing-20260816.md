1. Data/schema: What schema version should the new history commands and stored transcript use? Recommended: Define `awake.history.command.v1` and a versioned transcript schema separate from raw `awake.messenger.v1`.
2. Data/schema: How are `pinned`, `location`, `source`, and `conversationId` represented when `ApplyMessenger` currently writes only `id/speaker/text/day`? Recommended: Add typed fields to a new line schema and normalize old documents on read/first write.
3. Data/schema: Is pin stored by mutating each line or in a bounded metadata collection? Recommended: Keep line text immutable and track `pinnedIds` separately to simplify undo and migration.
4. Data/schema: What is the exact scope of clear? Recommended: Make clear per contact/conversation with explicit `targetId`/`conversationId`, never campaign-wide.
5. Data/schema: What happens to line identity after deletion? Recommended: Hard-delete the line after audit captures it, preserving the original ID only in the inverse/audit record.
6. Data/schema: Are command type and idempotency key separate fields? Recommended: Yes, `commandId` selects operation type and `idempotencyKey` identifies one user intent, both required and bounded.
7. Data/schema: How is `kind` (player, NPC, system, event, letter) validated? Recommended: Add a bounded enum/string and reject unknown kinds in the adapter.
8. Storage: Can delete/pin/clear/undo safely modify the current single `campaign.messenger.v1` value containing all contacts? Recommended: Split transcript into per-contact keys or enforce bounded per-contact pages before destructive commands.
9. Storage: Where does the compact inverse/audit ledger live? Recommended: Use a separate bounded `awake.history.audit` namespace/key so it cannot bloat the transcript value.
10. Storage: How does clear undo avoid effectively becoming a full-document snapshot? Recommended: Make clear non-undoable with explicit confirmation or model it as bounded deferred deletion; do not retain all cleared text for undo.
11. Storage: Is idempotency safe after `AppliedKeysMaximum` trims old keys? Recommended: Bound the replay horizon and reject stale operation IDs so a trimmed key cannot be re-applied to newer state.
12. Storage: What should a delete retry do after the line is already gone? Recommended: Return duplicate/conflict from the idempotency record instead of searching for another line or resurrecting it.
13. Storage: Does every final write recheck the 512KB limit after audit/metadata is added? Recommended: Serialize and measure immediately before `SetAsync`, failing hard on oversized output.
14. UI: Does the panel have explicit loading, empty, and corrupt states? Recommended: Yes, with corrupt state read-only and diagnostic actions only.
15. UI: Are confirmation and command arguments bound to the line ID rather than row index? Recommended: Bind to line ID/operation ID so reordered rows cannot delete the wrong message.
16. UI: Are history actions disabled while a dialogue turn or prior command is pending? Recommended: Disable mutation controls until drain and store refresh complete.
17. UI: Does the panel refresh from store after drain instead of optimistic cache edits? Recommended: Yes, use the store as source of truth and refresh selected contact after command observation.
18. UI: What happens if clear runs while an active conversation is appending? Recommended: Reject clear while active writes are in flight to prevent in-memory rows repopulating after clear.
19. UI: Should clear be a normal player action given it cannot be compactly undone? Recommended: Keep clear developer-only or require explicit non-undoable confirmation; do not expose it as a routine player feature.
20. UI: Does the developer-menu split reuse `AwakeSettings.Current.EnableDeveloperMenu` and enforce it at command entry? Recommended: Yes, gate export/undo by that existing setting and check at command entry, not only by hiding buttons.
21. UI: Are all new labels, warnings, and errors localized? Recommended: Route every visible string through `AwakeLocalization` and add entries to both language files.
22. Performance: Does the new hub repeat the existing `AwakeMessengerVM` constructor's synchronous `.GetResult()` load? Recommended: No; use async load with loading/disabled states.
23. Performance: Do delete/pin/clear/undo commands deserialize the whole transcript per action? Recommended: Load only the selected contact's bounded page and operate through cached per-contact state.
24. Performance: How is pinned status found without scanning all lines every frame? Recommended: Maintain a small bounded `pinnedIds` metadata index loaded with the contact page.
25. Performance: Is audit/undo lookup indexed by line/operation ID? Recommended: Yes, use an indexed bounded ledger instead of linear scans.
26. Performance: How is the contact list built without scanning all alive heroes on every open? Recommended: Cache and cap contact metadata, rebuilding only on campaign/settlement/game-day changes.
27. Performance: Can UI stay responsive while `WorldCommandBridge` awaits permission/preflight/submit/drain? Recommended: Run execution off the game thread and marshal only UI updates through `AwakeUiDispatcher`.
28. Performance: What is the worst-case cost of a 200-line, 4000-char-per-line contact? Recommended: Bound page size, line text, and command payload so any single action stays well below 512KB and remains sub-frame.
29. Token cost: Can raw deleted text inside inverse/audit records enter AI context? Recommended: No; raw transcript and audit stay local and are excluded from every route/context provider.
30. Token cost: Does command output include raw history text? Recommended: No; command output is a short status string, never message payload.
31. Token cost: Can exported history be imported back or replayed into commands? Recommended: No; export is read-only diagnostic text and import/replay paths are rejected.
32. Token cost: Are pinned lines bounded so bytes and future summaries cannot grow without limit? Recommended: Cap pinned count and per-line size at existing message limits, measuring total bytes.
33. Token cost: Is history management independent from C1 AI summarization token budget? Recommended: Yes; history UI/commands use storage only and consolidation is scheduled separately with existing caps.
34. Security: Can a crafted `targetId` address another contact's history? Recommended: Canonicalize and resolve the target in the adapter and require it to match the current contact/session before enqueue.
35. Security: Are delete, pin, clear, and undo permissions distinct and fail closed? Recommended: Define separate hard permissions per command and return `awake.permission_unknown` for any missing catalog entry.
36. Security: Which history actions use `EnsureAsync` versus `Evaluate`? Recommended: Player UI actions use explicit `EnsureAsync`; background jobs only `Evaluate`.
37. Security: Are arguments rejected before `JObject.Parse`/storage for JSON shape and size? Recommended: Require object payload and validate schema/length in adapter preflight.
38. Security: Can a stale undo replay deleted text into changed history? Recommended: Validate line absence and monotonic operation sequence; reject stale undo as conflict.
39. Security: Is the audit ledger append-only and protected from rewrite? Recommended: Append-only records with IDs, timestamps, owner, and correlation; no update path.
40. Security: Does diagnostic export exclude keys, provider config, and paths? Recommended: Export only AWAKE history/state summaries and never framework secrets or filesystem paths.
41. Validation: Which validator handles `hero:<StringId>` and `npc:<CharacterId>:a<index>` history targets? Recommended: Add a dedicated history target validator; relationship hero validation cannot accept valid non-hero targets.
42. Validation: How does the adapter verify a line belongs to the target before pin/delete? Recommended: Check `targetId` and `lineId` in one bounded load and return not-found/conflict before writing.
43. Validation: What are the exact bounds for targetId, lineId, speaker, text, reason, and undo payloads? Recommended: Reuse existing clamps and add explicit byte limits at the adapter boundary.
44. Validation: Does preflight include snapshot token validation so a stale clear cannot execute? Recommended: Yes, derive and verify a snapshot token from the command payload in Execute.
45. Validation: How are old lines without IDs handled before delete/pin can address them? Recommended: Assign deterministic migration IDs and refuse destructive commands until migration completes.
46. Validation: Are delete/clear of already-deleted/cleared state treated as idempotent or conflict? Recommended: Return duplicate for the same idempotencyKey; return conflict if a new operation sees changed state.
47. Save/load: Do history commands and audit survive a save/load before final drain? Recommended: Continue final drain on session end and lazily re-read storage after load so no command is silently lost.
48. Save/load: Is the new history cache reset in `ResetCampaignState` with the existing messenger reset? Recommended: Yes, reset all new hub/audit caches in the same lifecycle hook.
49. Compatibility: Are new history commands registered in `AwakeExtension.Register` and validated against the static manifest/constants at load? Recommended: Register every descriptor/adapter and fail loudly if command, permission, risk, or manifest arrays diverge.
50. Simpler alternative: Is delete/clear/undo worth doing before the schema and storage split exist? Recommended: No; Phase A should ship read-only history plus pin, defer destructive commands until per-contact storage and compact undo are explicitly designed.
VERDICT: REVISE