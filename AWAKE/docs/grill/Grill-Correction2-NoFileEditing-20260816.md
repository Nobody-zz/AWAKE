1. Should the contact hub reuse the existing `awake.messenger.v1` document or create a new schema? Recommended: reuse `awake.messenger`/`awake.messenger.v1` for append-only chat storage and add a separate schema only for profile/tab metadata that needs versioning.

2. How will existing `awake.messenger.v1` documents gain `day`, `location`, and `pinned` fields without data loss? Recommended: add a versioned `awake.contact.v2` shape with idempotent `Ensure*Shape` migration in `WorldStateStore`, never mutating unknown fields destructively.

3. Are unnamed NPC histories keyed by `AwakeNpcTarget.StableId` even though that ID includes transient `:a<agentIndex>` segments? Recommended: persist character-level IDs for contacts and treat agent-instance IDs only as transient scene identifiers.

4. If a hero dies or becomes a prisoner, should the panel hide the contact or preserve history? Recommended: keep stored history and display an unavailable/dead status derived from `GameData`/Hero state, without changing game state from the UI.

5. Does Bannerlord campaign multiplayer require any network synchronization for this panel? Recommended: no, keep everything local and CampaignSession-scoped through `IKeyValueStore`, because this is a single-player campaign mod.

6. How does the panel guarantee save/load persistence without a custom save path? Recommended: only read/write `host.Storage.OpenCampaignNamespaceAsync` namespaces opened by `WorldStateStore`, matching the existing `awake.*` lifecycle.

7. What prevents a contributor from reintroducing an AF/LoveHate save-path dependency while copying the panel pattern? Recommended: enforce a static rule that no gameplay state uses `File`, `Directory`, `Path.Combine`, or non-`awake.*` storage namespaces.

8. Can the aggregate contact history exceed `AiTaskConstants.StorageValueMaximumBytes` (512 KB)? Recommended: cap total history and paginate per contact so no single storage value approaches the existing limit.

9. Should opening the contact hub eagerly load all conversations for every NPC? Recommended: no, load contact metadata first and lazy-load the selected conversation and detail tab, like the current per-contact messenger history model.

10. Is synchronous storage loading on the game thread acceptable for a richer panel? Recommended: no, avoid the existing `AwakeMessengerVM` pattern of `.GetAwaiter().GetResult()` in constructors and use async load with UI status/disabled states.

11. Does the history manager ever send 200 raw lines into the AI prompt? Recommended: no, keep `NpcDialogueConstants.HistoryCapacity` and let `NpcDialoguePromptPipeline` truncate before any route submission.

12. Are raw dialogue, compressed NPC memory, and relationship state kept as distinct data sources? Recommended: yes, raw chat stays in messenger storage, summaries stay under `awake.npc.memories`, and relationship numbers stay under `awake.relationships`.

13. Where is the `pinned` field implemented for the borrowed history management? Recommended: add it to a new messenger schema and enforce a bounded pin count, because `WorldStateStore.ApplyMessenger` currently stores only speaker/text/day/id.

14. How does the panel delete one message without direct JSON mutation? Recommended: add an `awake.history.delete` command adapter that validates the message ID and enqueues a storage command through `WorldStateStore`.

15. How is undo implemented now that disk snapshot restore is banned? Recommended: use compact inverse/compensating commands plus a bounded audit ledger, never persist full document snapshots for rollback.

16. Should "clear history" be exposed as a normal player action? Recommended: make it an explicit command with confirmation, risk tier, permission, and audit record rather than reusing `ClearForTesting`.

17. If the panel offers export, can that exported data be imported back? Recommended: no, export only diagnostic text/log from storage and never write imported state back into `WorldStateStore`.

18. How does search/filter stay performant when `IKeyValueStore.GetAsync` has no query API? Recommended: search the already-loaded bounded page or add a small indexed metadata document; do not scan or deserialize the full 512 KB history per keystroke.

19. Where do the detail tabs get relationship, memory, and event data? Recommended: aggregate read-only from `GetRelationshipAsync`, `GetMemoriesAsync`, and `WorldEventLedger`/`GetWorldEventsAsync`, without duplicating those records in a new document.

20. Should `AwakeContactInfo.IsNearby` and location be persisted in storage? Recommended: no, keep volatile proximity/location as render-time data from `AwakeNpcTarget`/Hero and persist only stable identity and history metadata.

21. Does the panel show contacts the player has never met just because storage has history or all alive heroes exist? Recommended: respect the existing `hero.HasMet` and history existence so the UI does not invent relationships.

22. Are new history actions included in `PermissionCatalog` and `CommandRiskPolicy`? Recommended: yes, add every `awake.history.*` command to both plus `AiTaskConstants.NewCommandIds`, otherwise `WorldCommandBridge` or `PermissionGate` will reject them.

23. What happens if a history permission is missing from the catalog? Recommended: fail closed with `awake.permission_unknown`, matching `PermissionGate` behavior, rather than silently allowing the action.

24. Is `targetId` validated before entering the command queue? Recommended: reuse the existing identifier/length rules from `AwakeRelationshipDeltaAdapter.Validate` for history commands and reject oversized arguments at the adapter boundary.

25. Are delete/clear/pin operations idempotent? Recommended: use stable operation IDs and preserve `appliedKeys` semantics, because retry can otherwise delete or pin the wrong line after a drain retry.

26. If storage returns corrupt JSON, what does the panel show? Recommended: map `awake.world_state.corrupt` to a read-only error state and offer only diagnostic options, never automatic destructive repair.

27. Does `EnsureMessengerShape` get extended for new fields such as pinned/location/compression state? Recommended: yes, default missing fields and validate types so old documents can open without crashing.

28. Can the static `AwakeMessengerHistory` cache and `WorldStateStore` drain race with UI mutations? Recommended: route every player-facing mutation through the store command queue and refresh the cache from the store after drain instead of editing the in-memory list directly.

29. Is the new hub cache added to `SubModule.ResetCampaignState`? Recommended: yes, reset it alongside `AwakeMessengerHistory.ResetForCampaign` so new campaigns cannot inherit old contact data.

30. What happens if the panel opens before storage is ready? Recommended: keep the existing retry-interval behavior and do not permanently mark the hub loaded until a store read succeeds.

31. Are fire-and-forget history writes wrapped for observed failures? Recommended: use `AwakeBackgroundTask.Run` or equivalent and surface `AwakeLog` errors, avoiding unobserved task exceptions.

32. Does clicking a history command block the game thread while `WorldCommandBridge` awaits preflight/submit/drain? Recommended: no, invoke asynchronously and marshal UI updates through `AwakeUiDispatcher`.

33. Is the new overlay registered with `AwakeDialogueSessionCoordinator` and `SubModule.OnApplicationTick`? Recommended: yes, so it participates in overlay-open checks and cannot stack with NPC dialogue or other AWAKE panels.

34. Do tab/input controls keep keyboard focus and avoid scene T/Y hotkey conflicts? Recommended: use the existing Gauntlet input restriction/focus pattern and test against the configured terminal and scene selection keys.

35. Are all new tab labels and action strings localized? Recommended: route every visible string through `AwakeLocalization` and add entries for both language files before release.

36. Does the borrowed panel fit the current fixed 1280x760 canvas and supported Bannerlord resolutions? Recommended: keep the existing fixed-canvas pattern or make layout responsive, then verify text and controls at minimum supported widths.

37. Is a new `AwakeContactHubOverlay` necessary, or can the existing `AwakeMessengerOverlay` be extended? Recommended: extend the existing overlay/VM and prefab first, adding tabs and a profile pane without duplicating session and storage plumbing.

38. Is full history management including undo needed in the first implementation? Recommended: split it out; Phase A can ship a read-only contact/detail panel and append-only history, leaving delete/pin/undo as command-led later phases.

39. Can content-specific tabs such as body or intimate state be hardcoded in the core panel? Recommended: no, keep core tabs neutral and let content packages register additional read-only tab data through the content API.

40. Does the default pure campaign require adult-content tabs or wording in this panel? Recommended: no, keep the default panel content-agnostic and gate any content-pack-specific details behind `ContentPolicy`/tier.

41. If a player deletes history or undoes an entry, should relationship values be rolled back too? Recommended: only with an inverse relationship command carrying the original command ID, never by overwriting the whole relationship document.

42. Are world events copied into the contact hub or read from `WorldEventLedger`/`awake.world_events`? Recommended: read from the existing bounded event ledger so the same event is not persisted twice.

43. Do secret/memory tabs respect existing permission and content policy boundaries? Recommended: only render data already permitted by `PermissionGate`/retrieval rules; the panel must not become a bypass for sensitive content.

44. Does opening the NPC detail tab trigger a new AI summarization call? Recommended: no, display existing `NpcMemoryConsolidator` summaries and let scheduled consolidation produce new summaries.

45. Are command arguments bounded before they reach `ApplyMessenger`? Recommended: reuse existing `ClampTextElements` limits for speaker/text/day and reject or clamp oversized JSON in the adapter.

46. Will new history commands be registered in `AwakeExtension.Register` and manifest arrays, not just added to `WorldStateStore`? Recommended: yes, update `AiTaskConstants`, `PermissionCatalog`, `CommandRiskPolicy`, and the framework registration in one change.

47. Does "audit" degrade into full document snapshots under another name? Recommended: no, store compact operation records/inverse commands with IDs and timestamps, capped like `AppliedKeysMaximum`.

48. Are `ModuleData` files treated as read-only configuration rather than runtime state? Recommended: yes, only logs and the existing rule/worldbook loaders may read module files; all contact state writes go to framework storage.

49. Can the panel render a historical contact when `CampaignObjectManager` no longer contains the live Hero? Recommended: use stored display-name/history fallback and mark the contact unavailable instead of crashing.

50. How do tests prove this correction? Recommended: add SdkSmoke for history command validation, idempotency, delete/undo, schema migration, and a static check that no gameplay state write uses `File`/non-AWAKE save paths.

VERDICT: APPROVED