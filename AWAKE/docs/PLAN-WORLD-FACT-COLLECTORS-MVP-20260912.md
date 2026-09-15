# Plan: 世界事实采集最小批次

> Status: `revised_after_review_round_1`
> Revision: `R2`
> Baseline: source `b7eacaa4f679dfcdeb8a45b16337a35f60636a1f`; the unsynced weekly-dynamics candidate is at E2 only.
> Local API authority: Bannerlord `v1.3.15.110062` decompiled source under `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`. Official online API documents target a different version and are not used as signature authority.

## 1. Goal

Give **近期动态** and **本周动态** real campaign facts. This batch observes only five low-frequency, player-visible campaign changes and writes short factual Chinese records into the existing `WorldEventLedger`.

It does not create random events, notifications, dialogue, Worldbook content, Persona data, AI prompts, or new storage formats.

## 2. Locked scope

| Campaign event | Ledger kind | Report section | Display text template |
| --- | --- | --- | --- |
| `WarDeclared` | `war_declared` | 政治与外交 | `{甲}与{乙}开战。` |
| `MakePeace` | `peace_made` | 政治与外交 | `{甲}与{乙}议和。` |
| `OnSettlementOwnerChangedEvent` | `settlement_owner_changed` | 战争与领地 | `{地点}易主，现由{新领主}控制。` |
| `HeroKilledEvent` | `hero_killed` | 人物近况 | `{人物}死亡。` |
| `HeroPrisonerReleased` | `hero_prisoner_released` | 人物近况 | `{人物}结束囚禁，重获自由。` |

All five are considered report-worthy. `BattleStarted`, siege/raid completion, clan/kingdom changes, player choices, random adjustment, and every type of event effect are explicitly deferred.

## 3. Design decisions

| Topic | Decision |
| --- | --- |
| Registration | Add one new `AwakeWorldFactCollectorBehavior : CampaignBehaviorBase` in `SubModule.OnGameStart`. Do not add these observers to `AwakeEventBehavior`; it remains the legacy/random-event scheduler and report refresher. |
| Game-thread boundary | Each campaign callback immediately extracts only strings and scalar values: game day, IDs, display names, kind, domain and event key. It never retains a `Hero`, `Settlement`, `IFaction`, `PartyBase`, or any other TaleWorlds object after the callback returns. |
| Storage route | Before queuing, the callback only reads `AwakeRuntime.WorldStateStore` for a current session. If it is absent/stale, it writes `awake_world_fact_capture_unavailable` and returns without calling `QueueRecord`. Otherwise it hands the immutable snapshot to the existing `WorldEventServices.QueueRecord`. It does not write files, block, call `EnsureWorldStateReadyAsync`, or request permissions. Existing ledger retry/idempotency remains the sole write path. |
| Text | Text is fixed local templates, not an LLM prompt or Worldbook output. Missing required ID/name means skip that record and write a structured skip log; no invented “未知人物/势力” fact is saved. |
| Visibility | All five facts are public (`visibilityIdentityIds = null`). Private knowledge and rumor propagation are not part of this batch. |
| MCM | No MCM option. This is a small internal input pipeline with a fixed, conservative event set and no player-adjustable behaviour. |

## 4. Local v1.3.15 event signatures

The implementation must use these local decompiled signatures, not remembered API names or the mismatched online documentation:

| Event | Callback signature | Parameters used |
| --- | --- | --- |
| `WarDeclared` | `Action<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>` | both factions |
| `MakePeace` | `Action<IFaction, IFaction, MakePeaceAction.MakePeaceDetail>` | both factions |
| `OnSettlementOwnerChangedEvent` | `Action<Settlement, bool, Hero newOwner, Hero previousOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>` | settlement, `newOwner`, `previousOwner` |
| `HeroKilledEvent` | `Action<Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail, bool>` | victim |
| `HeroPrisonerReleased` | `Action<Hero prisoner, PartyBase, IFaction capturerFaction, EndCaptivityDetail, bool>` | prisoner, capturer faction, detail |

Required imports are the existing `TaleWorlds.CampaignSystem`, plus its `Actions` and `Party` namespaces. The settlement fixture must prove that `previousOwner` is written before `newOwner` in its event key, while the displayed sentence names `newOwner` as the current controller.

## 5. Stable identity and duplicate rules

Every collector passes a non-empty `eventKey`. It must be ASCII and use stable game IDs rather than localized names. `timeSlot` is the invariant decimal representation of the current campaign time in ten-minute game-time ticks, calculated synchronously in the callback as `floor(CampaignTime.Now.ToDays * 144)`. It distinguishes genuine same-day transitions while still making a duplicated dispatch at the same game instant idempotent.

| Kind | Event key grammar |
| --- | --- |
| `war_declared` | `war:{timeSlot}:{min(factionIdA,factionIdB)}:{max(factionIdA,factionIdB)}` |
| `peace_made` | `peace:{timeSlot}:{min(factionIdA,factionIdB)}:{max(factionIdA,factionIdB)}` |
| `settlement_owner_changed` | `settlement:{timeSlot}:{settlementId}:{previousOwnerHeroId}:{newOwnerHeroId}` |
| `hero_killed` | `hero-death:{timeSlot}:{victimHeroId}` |
| `hero_prisoner_released` | `hero-released:{timeSlot}:{prisonerHeroId}:{capturerFactionId}:{releaseDetail}` |

The collector normalizes both faction IDs ordinally for war/peace. For all other events, required IDs must be nonempty; an absent `previousOwnerHeroId` may be encoded as `none` because first ownership and non-hero transitions are valid. `releaseDetail` is the enum name converted to invariant ASCII. The event key prevents duplicate dispatch of the same game callback from creating duplicate weekly facts, while facts at different game-time slots remain distinct. A same-slot, same-identity collision is deliberately treated as the same observed state transition; its test documents that product boundary rather than silently losing it.

## 6. Closed runtime path

```text
Bannerlord CampaignEvents callback (game thread)
  -> snapshot strings/scalars and validate IDs
  -> AwakeWorldFactCollectorBehavior.QueueFact
  -> WorldEventServices.QueueRecord (background ledger retry + idempotency)
  -> WorldStateStore current-session persistence
  -> current rolling facts / completed weekly report
  -> 近期动态 / 本周动态
```

The collector does not require Native Knowledge, Worldbook, Persona, or the random event-engine setting. A running campaign with a current installed store records facts even if Native Knowledge is unavailable or `EnableEventEngine` is false.

If no current `WorldStateStore` is installed for the current session, the collector must log `awake_world_fact_capture_unavailable` with kind/day/event key and return before `QueueRecord`. It must not claim success. This is an explicit MVP limitation: it avoids background permission prompts; durable pre-store buffering is a separate batch if game evidence shows it is needed.

## 7. Required implementation

1. Add `AwakeWorldFactCollectorBehavior` with five v1.3.15 event subscriptions and empty `SyncData`.
2. Add it once from `SubModule.OnGameStart`, after the existing campaign behaviours. It has no save payload and no UI/MCM registration.
3. Implement five short callbacks plus shared snapshot/validation helpers. Callbacks catch/log their own unexpected exception so a bad display object cannot break a campaign event dispatch.
4. Map the new `kind` values explicitly in the weekly formatter: war/peace -> politics; settlement owner -> war; hero death/release -> culture. Do not rely on its current fallback inference, which classifies the two hero kinds incorrectly. Keep the established display labels from the weekly-dynamics MVP.
5. Add focused offline tests using the existing worldbook runtime smoke/test seam. Tests must invoke collector methods through an internal test seam or extracted snapshot builder; they must not require a running game process.
6. Update the build identity only at implementation start, creating a new collector candidate. The earlier weekly-dynamics DLL remains an unsynced E2 candidate and must not receive E3/E4/E5 claims after source changes.

Expected write set: `src/SubModule.cs`, one new `src/AwakeWorldFactCollectorBehavior.cs`, `src/WeeklyReportService.cs` only if its domain map lacks these kinds, focused tests/stubs, `src/AwakeConstants.cs`, and task-specific docs/evidence. The following are excluded: `AwakeEventBehavior.cs`, `AwakeEventEngine.cs`, `WorldStateStore.cs`, all Marcus framework projects, Worldbook content, Persona, dialogue, event rules, MCM, and game files.

## 8. Acceptance and evidence

| Case | Required offline proof |
| --- | --- |
| Registration | Source/static test proves the new behaviour is registered once on campaign start and subscribes exactly the five scoped CampaignEvents. |
| Snapshot safety | Tests prove queue inputs contain only expected scalar data; no live game object crosses into the background route. |
| Five facts | One fixture per event verifies kind, domain, day, Chinese text and exact event key. |
| Invalid inputs | Missing required identifiers skip the write and produce the stable skip/unavailable log route. |
| Duplicate dispatch and collision | Repeating the same fixture at the same time slot creates one ledger record only; war/peace faction order reversal produces the same event key. A second otherwise-identical fact at a different time slot creates a second record; an identical same-slot transition is documented and tested as one observed state transition. |
| Boundaries | Native unavailable and `EnableEventEngine=false` still permit collection when a current store is installed; no collector path calls readiness/permission APIs. |
| Storage unavailable | No installed/current store produces no fabricated record, does not call `QueueRecord` or any readiness/permission API, and emits `awake_world_fact_capture_unavailable`. |
| Compatibility | Existing random-event facts and v1 weekly report fixtures still render. No state key/schema migration. |
| E2 | Focused tests, project build, SDK smoke, localization parse and `git diff --check` pass. |
| E4/E5 | Only after explicit sync authorization: user verifies one of each feasible fact type appears in 近期动态, a completed week appears in 本周动态, then save/load keeps it. |

## 9. Non-goals and risks

- No guarantee for facts that occur before storage is available; do not hide this through fallback JSON or permission requests.
- No historical reconstruction on loading an old save.
- No attempt to decide why a war, peace, death, release or ownership change occurred; that is Worldbook/content interpretation, not this core collector.
- No direct campaign event can be assumed stable merely because it compiled; v1.3.15 game validation remains required.

## 10. Gate

The first independent review revised R1 for event-key collisions, signature/parameter locking, and storage-unavailable observability. One targeted re-review checks only those corrections. Code changes require `VERDICT: APPROVED` and explicit user sign-off.
