1. Are command IDs and input/output schemas for give gold, give item, trade, and request already defined? Add versioned `awake.action.*` schemas and adapters, because `AiTaskConstants.NewCommandIds` currently contains only the relationship command.
2. For give gold, is the source/destination always player-to-NPC or must both directions exist? Define explicit `playerHero`/`targetHero` plus a positive capped amount and reject unsupported directions in v1.
3. For give item, is the item selected by stable `ItemObject.StringId`, stack index, or AI free text? Use stable StringId plus count/stack selection and never parse free-text item names.
4. Is trade a unilateral deterministic settlement or a native negotiation flow with counteroffers? Use Bannerlord's native trade screen or a narrow two-sided confirmed offer; don't settle an unconfirmed trade.
5. For request, what is actually settled: an NPC promise, a pending player promise, or dialogue flavor? Model it as an explicit promise ledger entry with `pending/accepted/rejected` status rather than a resource mutation.
6. Are numeric bounds, item limits, target IDs, and optional fields specified for every action argument? Add schema-level bounds in the adapter rather than relying on output JSON shape.
7. Does the current NPC output schema expose these new commands with per-command argument validation? Keep the generic `command` object but register and enforce each `CommandDescriptor` input schema.
8. How does a UI button proposal map to the exact same command object as an AI suggestion? Add one `CommandProposal` type shared by button handlers and `NpcDialogueOutputValidator`.
9. Which public API supplies gold, item, or trade data? `IGameDataService` exposes only current player and clan hero queries, so add a bounded interaction snapshot or framework capability before adapters can validate.
10. Does `HeroDto`/`PlayerSnapshotDto` include player gold, party inventory, equipment, or trade state? It does not, so those fields must be added or read through a permitted game-data path before preflight.
11. How are gifts/trades resolved for unnamed or scene contacts that have no concrete hero inventory? Disable resource interactions for targets without a stable hero, matching the current remote-contact fallback.
12. Can an AI-emitted command target a hero other than the currently selected contact? Require the adapter to compare target hero ID with the active conversation target and reject mismatches.
13. Does item transfer distinguish player inventory, party chest, equipped gear, horses, and prisoners? Define exactly one supported inventory scope in v1 and fail closed for everything else.
14. Where are interaction settlements persisted? `AwakeStorageContract` has no interaction namespace, so add `awake.interactions.v1` rather than overloading relationship or messenger state.
15. Are gold/item changes authoritative in the Bannerlord save and separately audited in AWAKE storage? Persist the game mutation through TaleWorlds save state and append a compact ledger for history, with a defined ordering contract.
16. What prevents the same command from being applied as a game mutation and replayed from `WorldStateStore` after restart? Make the mutation idempotent and persist the applied idempotency key before or atomically with the mutation.
17. Does the existing outbox/result ledger handle pending resource commands across session end? Add interaction-specific drain semantics and never consider settlement complete until the game mutation and ledger both record success.
18. What schema migration exists for old saves without `awake.interactions.v1`? Treat missing interaction state as empty and do not auto-replay legacy records.
19. How are promise/request entries bounded and evicted? Reuse existing capped/pinned memory patterns and enforce a per-NPC maximum request ledger size.
20. Are item IDs stable across mod load order or item removal? Store `ItemObject.StringId`, count, and day, and degrade gracefully when the item is no longer available.
21. Does the ledger store enough for interaction history without duplicating chat lines? Store type, target, amount/items, result, day, correlation, and short reason only.
22. Where do the action buttons appear in the existing Gauntlet overlays, which currently expose only send and close? Add an action strip bound to available commands with disabled states and localized reasons.
23. Does the UI suppress give/trade for remote contacts, scene shout, missions, and missing inventories? Compute per-target eligibility before rendering action buttons and fail closed otherwise.
24. When AI suggests an action, does it execute automatically in `NpcDialogueService.ExecuteCommandAsync`? It must not; render the suggestion as a pending confirmation button and execute only after the player clicks.
25. Can the player edit an AI-suggested amount/item before settlement? Allow editing only bounded UI fields, then rebuild the command request from the edited values.
26. Are action labels, confirmations, and error messages localized in both language files? Add `awake.action.*` strings and avoid hardcoded UI text.
27. What happens on rapid double-click of an action button? Disable actions while a command is pending and derive idempotency per player confirmation click, not per AI turn.
28. Can the AI reply overwrite or fabricate the settlement result? Show a code-generated status/ledger line after preflight/execution, separate from the streamed reply.
29. Does opening the contact panel enumerate player/NPC inventories every frame? Snapshot gold and item availability only when opening a contact or when relevant state changes.
30. How many commands and how much inventory data enter the prompt or UI? Keep v1 to four fixed actions and never send full rosters to the model.
31. What inventory/gold context is needed for the AI to suggest actions? Send only bounded summary data such as player gold and item counts/categories, not full item lists.
32. What prevents the model from emitting repeated or multiple commands in one turn? Limit output to at most one command per completed turn and enforce the existing 16 KiB command budget.
33. Does the AI free-text path ever reach game state before adapter validation? No, keep allowlist, schema parse, preflight, permission, and adapter settlement mandatory before any mutation.
34. Can negative, floating-point, string, or overflow amounts pass validation? Use strict integer parsing with explicit minimum/maximum bounds and reject every other token type.
35. Can a malicious model set target IDs, item IDs, or reasons to bypass permissions? Validate all identifiers and lengths against allowlists and current-session targets before preflight.
36. What risk tier are give/trade commands in `CommandRiskPolicy`, and does `IsWorldBridgeAllowed` permit them? Mark them R2Gameplay (or R3 if strategic) and update the allowlist, permissions, and bridge policy together.
37. Do buttons and AI suggestions use the correct permission path? Player-active button clicks use `PermissionGate.EnsureAsync`; background AI paths only propose until the click.
38. Can command adapters perform network/DB/async I/O in `Preflight`/`Execute`? No, keep adapters synchronous game-thread checks and route durable I/O through Storage/outbox.
39. Does `NpcDialogueOutputValidator` reject unknown command IDs before creating a proposal? Add the new IDs to `NpcDialogueConstants.AllowedCommandIds` and validate there, not only at execution.
40. Does the current `SnapshotFromArguments` protect against state races for gold/item commands? Replace the args-only hash with a preflight snapshot containing balance, inventory, and target state, rechecked in `Execute`.
41. Are duplicate JSON properties and extra command fields rejected? Validate with the command input schema and reject extra or duplicate fields before settlement.
42. What stable error codes are returned for insufficient gold, missing item, wrong location, denied permission, and snapshot mismatch? Define `awake.action.*` codes with category and retry semantics, preserving owner and correlation.
43. Is settlement atomic if gold is deducted but item transfer or ledger write fails? Perform game mutations in one main-thread adapter with rollback/compensation for partial failures.
44. Are relationship deltas derived from the settled amount/item/trade rather than trusted from AI arguments? Compute deterministic code-generated relationship changes from the validated interaction result.
45. What happens to an in-flight action if the player saves and exits during execution? Persist the command as pending with durable idempotency state and never replay an unmarked game mutation.
46. Is every command guarded against missions, map loading, scene shout, unsupported sessions, and any hypothetical network/multiplayer state? Check campaign/session eligibility and proximity before preflight and fail closed outside supported single-player campaign states.
47. Does this design preserve native Bannerlord trade/conversation behavior and compatibility with other inventory mods? Prefer native UI/API and keep AWAKE commands limited to bounded player-initiated actions.
48. Does the correction depend on `CapabilityBroker`, which AGENTS says is not yet enabled? Do not rely on it; either implement self-built game-thread adapters or explicitly defer item/trade until framework support exists.
49. What happens when the player has zero gold, no matching item, the target is dead/imprisoned, or the item is modded away? Preflight returns a typed denial and the UI disables the button with a localized reason.
50. Is implementing give gold, give item, trade, and request all at once the simplest viable correction? Narrow v1 to deterministic give gold plus request/promise ledger, and add item/trade only after native or game-data support is proven.
VERDICT: REVISE