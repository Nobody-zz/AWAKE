1. [scope] Does "borrow patterns" also permit reusing AliceMM prefab/VM/movie code and names? A: No; align with `AWAKE-UI-Borrow-Map` and allow only layout/interaction concepts, not XML, prefab, VM, or naming.
2. [asset] Does banning only artwork/portraits cover all proprietary assets? A: No; extend the ban to Alice/AF textures, sprites, brushes, icons, fonts, sounds, animations, and any referenced module paths.
3. [asset] Are game-native brushes and sprites acceptable? A: Yes; current `AwakeMessenger.xml` already uses Bannerlord `StdAssets`/`General` brushes, and new visuals should be AWAKE-owned under its module GUI.
4. [data] Can hero contacts get native portraits? A: Yes; use `Hero.CharacterObject` with game-native portrait APIs such as `CharacterImageIdentifier(CharacterCode.CreateFrom(...))`, never Alice portraits.
5. [data] Can unnamed NPC contacts get native portraits? A: Yes; use the troop `CharacterObject` via the same `CharacterCode` path with a generic AWAKE placeholder fallback.
6. [data] Are location NPC stable IDs safe as avatar keys? A: No; `AwakeNpcTarget.StableId` for `npc:*` can contain `:a{agentIndex}`, so key portraits and history by hero/troop identity and fallback for agent instances.
7. [data] Where should location/status come from? A: Build it from current Campaign/Hero state at panel open, persist only IDs, and refresh live, because `AwakeMessengerService.BuildContacts()` rebuilds contacts today.
8. [schema] Does `awake.messenger.v1` support day/location/pinned/unread/favorite/letter metadata? A: No; `ApplyMessenger` stores only speaker/text/day, so a new versioned schema is required.
9. [schema] What schema should the hub use? A: Add `awake.contact.v1` or upgrade messenger to v2, register it in `AwakeStorageContract`, and separate chat append commands from contact/card reads.
10. [schema] How should old messenger docs be handled? A: Keep a backward-compatible v1 loader or add v1-to-v2 migration through `TryNormalizeSchema`; never silently drop player history.
11. [storage] Which namespace should chat/letters use? A: Keep `AiTaskConstants.MessengerNamespace` for chat and register new letter/contact namespaces in `StorageNamespaceIds`; no custom JSON files.
12. [storage] Can 200 lines plus metadata/letters exceed the storage value cap? A: Recalculate against `StorageValueMaximumBytes` (512KB) and tighten per-line/letter byte limits after adding metadata.
13. [storage] Are hub writes idempotent? A: Yes; reuse `WorldStateCommand` plus idempotency keys and `DrainAsync(commandId, idempotencyKey, ...)` as `AppendMessengerMessageAsync` already does.
14. [storage] Can contact business data live in ModuleData? A: No; the project hard rule requires persistent state through Marcus Storage/`WorldStateStore`, not ModuleData JSON.
15. [storage] Can the VM hold live Hero/Character references? A: No; use open-time snapshots and stable IDs, routing reads through `GameData`/`ContextContribution` as required by AWAKE architecture.
16. [save/load] Is static contact/history state reset on campaign start/load? A: Yes; reuse `AwakeMessengerHistory.ResetForCampaign()` and existing `ResetCampaignState()` so stale contacts cannot leak across saves.
17. [save/load] How are delayed letters resumed? A: Persist send day, delivery day, sender/recipient IDs, read state, and reconcile in a CampaignBehavior tick instead of in-memory timers.
18. [save/load] Will old saves keep current 200-line chats? A: Yes; support legacy `awake.messenger.v1` loading while adding new fields, and add a save/load regression test.
19. [multiplayer] Is the hub safe in unsupported game modes? A: Guard to single-player campaign and fail closed when Campaign/game mode is unavailable; AWAKE is not built for multiplayer authority.
20. [compatibility] Do new hub views break `NpcDialogueOverlay` and scene T/Y? A: Preserve both overlays and route through `AwakeDialogueSessionCoordinator`; the existing half-done inventory already assigns scene vs remote UI boundaries.
21. [compatibility] Should avatar area/letters be configurable? A: Add `AwakeConfig` toggles like the existing scene visual and proactive toggles, defaulting safe for low-end or UI-sensitive installs.
22. [UI] Does the fixed 1280x760 messenger prefab survive a 3-column hub? A: No; redesign with `StretchToParent`/minimum widths and stable card heights so common 1366x768 and 1280x720 resolutions do not clip.
23. [UI] Can long CJK names and letters overflow rows? A: Keep `CanBreakWords` and clipped scroll panels from current prefabs, add a letter max length, and verify Chinese locale layout.
24. [UI] How are unread/favorite/search/filter modeled? A: Extend `AwakeContactRowVM` with boolean/search properties and bind them in the list template; current rows expose only DisplayName/Identity/Status.
25. [UI] Are action buttons fixed code commands or free AI text? A: Fixed `awake.action.*` buttons backed by `WorldCommandBridge` and schemas; AI may suggest but never execute arbitrary text.
26. [UI] Should letter compose be a separate overlay? A: No; embed compose as a hub tab/mode using the existing overlay/VM lifecycle to avoid duplicating the messenger panel.
27. [UI] Is streaming chat UI-thread safe? A: Yes; drain `NpcDialogueUiEvent` in `OnFrameTick` and marshal async updates through `AwakeUiDispatcher`, as existing VMs do.
28. [performance] Is enumerating all alive heroes on every open a hitch risk? A: Yes; `AwakeMessengerService.BuildContacts()` already enumerates `AliveHeroes`, so cache/lazy-build and cap rows.
29. [performance] Are native portraits/tableaus cached? A: Yes; cache `ImageIdentifier`/`CharacterImageIdentifier` per stable key and release on panel close rather than recreating textures per frame.
30. [performance] Does streaming cause property-change/rebind churn? A: Keep the existing 20,000-element `AppendStream` cap and batch UI notifications to avoid GC and layout churn.
31. [token] Does raw 200-line chat history go into AI prompts? A: No; use `NpcDialoguePromptPipeline.BuildBounded` and `HistoryCapacity`; raw history is for player browsing only.
32. [token] Do avatar/card fields enter AI prompts? A: No; keep card data in the UI VM only and continue using the controlled variables in `NpcDialogueService.BuildPromptInputAsync`.
33. [token] Are memory/event summaries bounded for the card? A: Yes; reuse `NpcMemorySelector.FormatTopK`/`MemoryBlockMaximumBytes` or equivalent; never serialize full memory/event arrays.
34. [security] Can action commands accept arbitrary JSON? A: No; validate through `AiTaskConstants.CommandInputSchema`, typed adapters, item/count/recipient checks, and reject unknown fields.
35. [security] Can letter text inject rich-text markup? A: Sanitize or render plain text because the existing chat uses `RichTextWidget`, which interprets markup.
36. [security] Can portrait resolution expose filesystem paths? A: No; return `ImageIdentifier`/sprite/asset handles only, per the architecture rule against exposing file paths in game UI.
37. [security] Do hub actions require permission gating? A: Yes; call `PermissionGate.EnsureAsync` with `AiTaskConstants.CommandPermission(...)`, fail closed for unknown commands, and disable buttons while pending.
38. [validation] What happens for dead/captured/out-of-range contacts? A: Revalidate at open/tick and show existing expired/remote-letter states instead of allowing send or actions.
39. [validation] Are letter recipients validated? A: Require `AwakeNpcTarget.TryParseStableId` and a known hero/troop ID before compose/send; reject empty, unknown, or unstable agent-only IDs where needed.
40. [validation] Are stored day/location/pinned values normalized? A: Coerce and clamp metadata on load like `AwakeMessengerHistory.IntValue`, ignoring corrupt fields without crashing.
41. [edge] How are duplicate scene and campaign contacts handled? A: Deduplicate by ordinal stable ID as `BuildContacts` does and show one canonical row per hero/troop.
42. [edge] What if contacts or host are unavailable? A: Render localized empty/offline states and disable send/actions, reusing existing `no_nearby`, `host_missing`, and contact-expired strings.
43. [edge] Can remote contacts use live AI chat? A: No; keep remote contacts on letter flow or the existing future-version placeholder until letter storage and scheduling exist.
44. [edge] What if a hero's name/identity changes? A: Keep stable ID as the key, refresh display/status from current game state at open, and preserve history under the stable ID.
45. [edge] Are letter bodies capped for Unicode/CJK? A: Yes; reuse `AwakeRuntime.TruncateTextElements` with a letter-specific limit and byte-size validation before persistence.
46. [simpler] Is a new `AwakeContactHubOverlay` required for Phase A? A: No; extend `AwakeMessengerOverlay`/`AwakeMessengerVM`/`AwakeMessenger.xml` first and split only if lifecycle/coordinator duplication appears.
47. [simpler] Is letter compose required for this correction? A: No; keep it as a stubbed tab per the concept's Phase C and ship avatar/chat/card UI first.
48. [simpler] Can unnamed NPC avatars be omitted? A: Yes; use a native troop icon/generic AWAKE placeholder and defer generated portraits, avoiding Alice visual dependency.
49. [simpler] Are action buttons required for this correction? A: No; keep `awake.relationship.delta.v1` and defer `awake.action.*` until commands, permissions, preflight, and idempotency tests exist.
50. [material] Does the correction distinguish borrowed patterns from copied implementation and non-art asset provenance? A: No; it bans only Alice artwork/portraits and leaves prefab, VM, naming, brush, and icon copying ambiguous, so revise it to ban non-pattern implementation and all Alice/AF assets before approval.
VERDICT: REVISE