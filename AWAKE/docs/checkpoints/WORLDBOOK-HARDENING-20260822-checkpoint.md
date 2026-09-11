# AWAKE Worldbook Hardening Checkpoint — 2026-08-22

## Batch

- `task_id`: `WORLDBOOK-HARDENING-20260822`
- `batch_id`: `worldbook-hardening-plan-20260822`
- `status`: `implementing`
- `plan`: `docs/PLAN-AWAKE-WorldbookHardening-20260822-DRAFT.md`
- `frozen_candidate`: `awake-20260820-syncpack-001` unchanged

## Files changed

- Added `docs/PLAN-AWAKE-WorldbookHardening-20260822-DRAFT.md` and recorded implementation approval.
- Updated `src/WorldbookIdentityEvaluator.cs` to reject unknown rule `scope` and `min_detail` values.
- Updated `tools/worldbook-runtime-smoke/Program.cs` with direct fail-closed regression cases.
- Added `src/WorldbookIdentityCapabilityRules.cs` and `src/BannerlordWorldbookIdentityAdapter.cs` for the narrow v1.3.15 NPC identity/capability mapping.
- Updated `src/NpcDialogueService.cs` to feed adapter-derived profile/scope/detail data into the v2 query path.
- Updated runtime smoke project and fixtures with villager/headman/noble progression coverage.
- Added `src/WorldbookEntityId.cs` and fixed Studio `RuntimePackageCompiler` condition mapping so culture/kingdom/settlement/role IDs retain their type.
- Added `src/WorldbookPackageIntegrity.cs`; activation now validates manifest/runtime/index identity, canonical manifest/content/package hashes, and index closure before constructing the live query service.
- Updated `WorldbookRuntime.cs`, `WorldbookPackageRegistry.cs`, `WorldKnowledgeLoader.cs`, and runtime smoke fixtures for verified-package activation.
- Added `publicly_askable` to the Studio referral registry and `publiclyAskable` to Runtime referral records; compiler, preview, loader and query now reject or suppress non-public targets and never emit referrals from denied/disabled/directly visible paths.
- Mapped authoring `fallback_referral_ids` into Runtime grant `referral_ids` and added Studio F26-F30 coverage for compile parity, unknown targets, denied paths, unknown previews and direct visibility.
- Added stable `EventKey` handling, memory/storage/reload deduplication, weekly-report deduplication and event-record schema support; runtime smoke now covers same-key retry, distinct-key same-text events, replay, concurrency and capacity.
- Refreshed `tools/worldbook-studio/artifacts/WorldbookStudio`; package and release-check pass.
- No dist artifact, frozen candidate or game file changed.

## Verification

- Current state, Contract v1, project rules and existing platform implementation reread.
- Plan review Round 3 returned `VERDICT: APPROVED`; the user's current instruction and prior explicit implementation authorization provide the execution sign-off.
- `dotnet run --project tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj -c Release` returned `PASS: runtime loader/query/overlay/identity/registry/weekly-report/event-ledger smoke`.
- `dotnet build AWAKE.csproj -c Release -p:BannerlordApi=1.3.15 --no-restore` returned 0 warnings / 0 errors.
- Studio `dotnet run --project tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Awake.WorldbookStudio.Tests.csproj -c Release` returned `PASS: Worldbook Studio F01-F30 harness (30/30)`.
- `tools/worldbook-studio/scripts/package.ps1 -Output artifacts\WorldbookStudio` and `release-check.ps1 -Package artifacts\WorldbookStudio` both returned `PASS`.
- Contract root JSON parse returned `17 files`, with no parse failures; source build DLL SHA-256 is `779281D1843AABD874B8136DD8116D9669F753F054F12E83F485F1D1341BB6C2`.
- Runtime golden vectors matched Studio; runtime tamper, index tamper and registry hash mismatch all failed closed in smoke.
- Bannerlord was not started and the game directory was not touched.

## Known limitations

- Independent read-only review Round 1 and Round 2 returned `VERDICT: REVISE`; Round 3 returned `VERDICT: APPROVED`.
- The revised plan locks effective detail, default NPC request detail, unknown Hero fallback, query/entry gate scope, event key semantics, Contract precedence and canonical hash inputs; permission, identity, typed condition mapping, package integrity, public referral behavior and event uniqueness now have offline evidence.
- The narrow adapter is now wired for Bannerlord `v1.3.15`; it does not yet cover external large-Mod role adapters or real gameplay evidence.
- Runtime integrity uses a local canonical writer matching the current Studio golden vectors; runtime schema validation is structural rather than a full JSON-Schema engine.
- The multi-agent reviewer path returned `502 Bad Gateway` after upstream `503`; the local Codex CLI read-only path completed all three recorded reviews.

## Next action

Close this source-only hardening batch. Do not modify `awake-20260820-syncpack-001`; next batch should first define event-record/weekly-report contract parity and the single recorder/report façade.

## Last error

- Historical tool error: `multi_agent_v1` reviewer returned `unexpected status 502 Bad Gateway: stable-proxy exhausted retries: upstream status 503`.
- Current plan review result: `VERDICT: APPROVED` (Round 3); permission, identity, typed condition mapping and package integrity portions are implemented and offline-verified.
