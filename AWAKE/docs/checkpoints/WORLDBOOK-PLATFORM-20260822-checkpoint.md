# AWAKE Worldbook Platform Checkpoint — 2026-08-22

## Batch

- `task_id`: `WORLDBOOK-PLATFORM-20260822`
- `status`: `offline_verified`
- `plan`: `docs/PLAN-AWAKE-WorldbookPlatform-20260822.md`
- `contract`: `tools/worldbook-contract/v1/`
- `frozen_candidate`: `awake-20260820-syncpack-001` unchanged

## Completed

- Added Contract v1 assets for registry, package manifest, activation, runtime snapshot, authoring, overlay, export, events, weekly reports, adapter, enums, permissions and errors.
- Fixed stable ID distinction: package IDs use two segments; world/object IDs use three segments.
- Added canonical SHA-256 hashing and golden fixture; manifest self-hash excludes the `hashes` field; package hash concatenates raw 32-byte manifest/content digests.
- Studio now emits `runtime.json`, `index.json`, `package-manifest.json` and `awake.runtime_mapping_report.v2` while retaining author diagnostics.
- AWAKE Runtime now accepts only `awake.worldbook.v2`; it loads runtime JSON/indexes and does not fall back to v1 worldbook files.
- NPC dialogue knowledge retrieval now uses `IWorldKnowledgeQuery` and the v2 identity/permission path.
- Added identity inheritance, keyword index, `known/referral/not_found` result states and byte-budgeted output.
- Added player Overlay editing for existing summaries, CAS revision checks, export, import replay and campaign `SyncData` storage.
- Added in-game developer terminal actions for status, search, edit and export.
- Added registry discovery and campaign activation selection with explicit package IDs, trusted-root path checks and package-hash checks.
- Added runtime identity evaluation for explicit identity, role/office, inheritance, age, management, skills, culture, kingdom and settlement conditions; denies remain higher priority than grants.
- Added deterministic code-generated weekly reports grouped into politics, economy, culture and war, with stable report/section/item IDs and source-event closure; the existing UI now renders through the new service.
- Studio authoring conditions now compile into runtime namespace IDs and v2 condition fields; authoring schema accepts role, gender, age, management and skill constraints.

## Verification

- Studio Core Release build: 0 warnings / 0 errors.
- Studio CLI Release build: 0 warnings / 0 errors.
- Studio Web Release build: 0 warnings / 0 errors.
- Studio harness: `F01-F25 PASS`.
- Runtime smoke: loader/query/overlay/CAS/import/identity/registry/weekly-report `PASS`.
- Contract JSON parse: 17 files pass.
- Static audit: no production `WorldbookService.Query(` call, no `unsupported_for_v1`, no `WorldbookLoader.LoadDirectory(` call, no legacy `NarrativeReportBuilder.Build` call.
- AWAKE Release build: `0 errors`; 3 pre-existing CS1998 warnings remain.
- Source build DLL SHA-256: `41D89B5B63F7951999E2B8651983906FC6BE78830592542B3EABAE8E1382C7CB`.
- `dist` and game DLL hash remain frozen candidate `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`.
- `dist` and game DLL hash remain frozen candidate `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`.

## Not done

- No game launch, E4/E5 validation or game-directory synchronization.
- No new v2 candidate BuildId/package assembled for game testing.
- No game launch, E4/E5 validation or game-directory synchronization.
- Weekly report JSON is generated deterministically on demand and rendered through the current UI; durable report-instance storage remains a later persistence refinement.

## Next action

Create a separate v2 candidate BuildId/package for the completed offline batch, then run candidate-only packaging and cross-root checks; keep `awake-20260820-syncpack-001` and all game files untouched until explicit game-validation authorization.
