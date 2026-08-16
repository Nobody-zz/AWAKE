1. Does the correction define a session record? Recommended: add `awake.dialogue.session.v1` with sessionId, targetId, entrySource, turns, state, and correlation before implementation, since `AwakeDialogueSessionCoordinator` currently stores only a source and target string.
2. Does the coordinator share a real session model or only a mutex? Recommended: replace the static flag with an active `AwakeDialogueSession` object and registry, because `TryAcquire`/`Close` manage no service or VM state.
3. Can multiple entry sources transition into one open contact hub? Recommended: define one-active-session transition rules instead of returning busy for every new source while `AwakeMessengerOverlay` already owns the panel.
4. Can scene T/Y open the 1280x760 hub over `MissionScreen` without leaking movement input? Recommended: test hub launch from held-T release and restore mission input restrictions on close.
5. Does map encounter selection preserve the encounter game menu? Recommended: open the hub over the encounter menu and return to that same menu on close, not to native conversation or map movement.
6. What happens when the unified hub fails to load? Recommended: define an explicit fallback policy for map encounter; do not silently call `NpcDialogueStarter.TryOpenConversation` when the correction requires panel dialogue.
7. Does opening the hub from scene/encounter auto-select the wrong contact? Recommended: support an explicit initial target and suppress `AwakeMessengerVM`’s current first-nearby auto-select for non-messenger entries.
8. Are encounter leaders and unnamed scene NPCs present in the contact list? Recommended: bind the current target even if `AwakeMessengerService.BuildContacts` would not list it, since it only adds nearby heroes plus met heroes.
9. Is a non-hero NPC’s stable identity safe across sessions? Recommended: split `StableId` into a runtime agent locator and a stable character key, because `AwakeNpcTarget` embeds `:a<agentIndex>` in the ID.
10. Is saved history keyed to an identity that survives save/load? Recommended: key persisted chats by hero ID or character `unnamedKey`, not by an agent-indexed stable ID that changes after reload.
11. Does shared session history include scene, encounter, event, and proactive turns? Recommended: route all dialogue turns through one history sink, because only `AwakeMessengerVM` currently calls `AwakeMessengerHistory.Append`.
12. Does the correction specify migration from `awake.messenger.v1` chats? Recommended: add a read-only v1 migration/compatibility path before replacing the messenger schema.
13. Can unified history fit the framework’s storage limits? Recommended: cap total entries and shard or prune history per target, because one 200-line-per-contact `campaign.messenger.v1` doc can exceed the 512KB value limit.
14. Should the hub load all contacts’ histories eagerly? Recommended: load lazily per selected contact instead of `AwakeMessengerHistory.LoadAsync` parsing the whole chat object on every hub open.
15. Does shared session keep raw UI history separate from prompt history? Recommended: preserve the current 12-turn `NpcDialogueService` prompt window and the 100-row UI cap as distinct layers.
16. Does switching contacts reinitialize the AI service and reload memory/worldbook? Recommended: define pause/suspend semantics so every contact switch does not create a new `NpcDialogueService` and repeat initialization.
17. Does event/proactive context stay within the existing prompt budget? Recommended: keep `MaxPromptUtf8Bytes=32768`, 2500-byte memory, and 4096-byte knowledge limits when adding source/event metadata.
18. Is event discussion grounded in the actual event? Recommended: pass event title/body plus `discussionAction.openingHint` as bounded session context rather than only a 240-character hint.
19. Does proactive dialogue preserve motive metadata? Recommended: extend queued dialogue records with motive, source, expiry, and correlation instead of `PendingDialogue`’s heroId/hint only.
20. Can opening hints be lost or swapped? Recommended: remove the single-slot `NpcDialogueContext` global and carry the hint in the session start payload.
21. What happens when the event/proactive queue overflows? Recommended: define explicit overflow feedback and requeue, since `EventDialogueQueue` silently drops items at 32.
22. Does event discussion depend on a later tick? Recommended: let the event choice request a direct hub open or drain the queue immediately after `OnChoice`, not only during the next application tick.
23. Can another hourly event fire while a discussion session is pending? Recommended: keep the event engine busy until the discussion session is accepted/closed so queued and new events do not race.
24. Is proactive acceptance idempotent? Recommended: mark the candidate accepted before showing the hub and verify `NpcProactiveService` cannot show or enqueue it twice.
25. Does closing/switching contacts cancel an in-flight reply unexpectedly? Recommended: define cancel-on-close versus suspend-on-switch and preserve one active generation per session.
26. Can two services for the same NPC overlap? Recommended: have the coordinator own service creation so only one `NpcDialogueService` is alive per target at a time.
27. Does stale overlay cleanup risk clearing a newer session? Recommended: close by session token, not by source/target match, because `AwakeDialogueSessionCoordinator.Close` silently ignores mismatches.
28. Is the unified hub included in lifecycle cleanup? Recommended: add it to `DialogueOverlayLifecycle.CloseAll`, which currently closes only `NpcDialogueOverlay`.
29. Is there one defined overlay priority? Recommended: replace layers 541/542/543 with one hub layer and a documented ordering above game/mission screens but below system popups.
30. Does the correction actually remove duplicate UIs? Recommended: replace `NpcDialogueVM`, `AwakeMessengerVM`, and their two prefabs with one hub VM/prefab; adding a coordinator alone leaves duplication.
31. Does the hub restore full input on close? Recommended: mirror `OpenLayer`/`Close` input-restriction handling and test both mission and game-menu contexts.
32. Is Escape behavior unified? Recommended: choose one cancel policy; the current NPC overlay blocks Escape for 60 seconds while messenger closes immediately.
33. Does the 1280x760 fixed hub fit low resolutions? Recommended: make the hub responsive with stable min/max dimensions and scrollable lists for 720p and ultrawide.
34. Is the proposed right character card actually in scope? Recommended: state whether Phase A includes the right card; current `AwakeMessenger.xml` only implements left contacts and center chat.
35. Can opening the hub enumerate hundreds of heroes without stutter? Recommended: cap, cache, and virtualize `AwakeMessengerService.BuildContacts`, whose remote loop iterates all alive met heroes uncapped.
36. Is search/filter needed once all sources enter one hub? Recommended: add search or a sensible contact cap before treating it as the universal panel.
37. Is rich-text rendering safe for player and AI text? Recommended: escape or sanitize text before binding to `RichTextWidget`, since both prefabs render dynamic text directly.
38. Is player input validated before prompt submission? Recommended: enforce `MaxPlayerInputLength` at input time and check UTF-16/UTF-8 length before `BuildPromptInputAsync`.
39. Are stale, dead, remote, or underage targets rejected at session start? Recommended: call `NpcDialogueLauncher.IsEligibleNpcTarget` on open and before each turn, not only at launcher time.
40. Does unified dialogue preserve the pure content tier? Recommended: keep `ContentTier="pure"` and the existing content-policy gating for every unified entry point.
41. Is the hub safe in non-campaign or multiplayer contexts? Recommended: fail closed when `Campaign.Current`, `ResolveHost`, or campaign storage is unavailable; do not create overlays or storage in unsupported modes.
42. Do queued proactive/event dialogues survive save/load? Recommended: persist pending dialogue queue or restore it with session state; `SubModule.ResetCampaignState` currently clears the queue.
43. Is history flushed before session end? Recommended: have the unified session flush raw history and memory through `WorldStateStore.BeginSessionEnd`/final drain.
44. Are duplicate messages prevented after reload? Recommended: preserve idempotency keys in the unified history writer and dedupe by session/turn ID.
45. Can one turn be appended twice by hub and messenger code paths? Recommended: centralize append in the session model so turn completion drains exactly once.
46. Is there regression coverage for coordinator transitions and storage migration? Recommended: add SdkSmoke tests for session takeover, queue dedupe, v1 migration, and hub launch; this repo has no test directory today.
47. Is merging every overlay into one hub the simplest correct design? Recommended: unify dialogue-bearing entries only; do not merge event inbox or weekly report into the contact hub.
48. Is a second service abstraction needed? Recommended: keep `NpcDialogueService` as the session model and let the coordinator own lifecycle, rather than introducing a parallel service layer.
49. Does unified routing remain compatible with existing event JSON and proactive content? Recommended: keep `dialogueAction`/`discussionAction` schemas loadable and make session additions backward compatible.
50. Can the change pass project quality gates? Recommended: require 0-warning Release build for Bannerlord 1.3.15 and `Awake.SdkSmoke.exe` PASS ALL after replacing the UIs and coordinator.
VERDICT: REVISE