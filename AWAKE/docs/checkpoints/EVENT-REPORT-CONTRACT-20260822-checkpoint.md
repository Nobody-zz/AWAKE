# AWAKE Event / Weekly Report Contract Checkpoint — 2026-08-22

## Batch

- `task_id`: `EVENT-REPORT-CONTRACT-20260822`
- `batch_id`: `event-report-contract-v1-20260822`
- `status`: `offline_verified`
- `execution_lease`: source-only; frozen candidate and game directory untouched
- `frozen_candidate`: `awake-20260820-syncpack-001`

## Implemented

- Added `src/WorldEventContracts.cs` with `IWorldEventRecorder`, `IWeeklyReportService`, the single `WorldEventServices` façade, and v1 event/report projection validators.
- Routed all production event writes, weekly report generation, terminal report access, inbox loading and campaign reset through `WorldEventServices`.
- Kept `WorldEventLedger` and `WeeklyReportService` as the only underlying implementations; no parallel writer or report store was introduced.
- Projected `WorldEventRecord` into `event-record.v1` with conservative visibility defaults and `extensions.awake:gameDay`.
- Normalized event type/domain values and made weekly reports filter the exact seven-day inclusive game-day window with an exclusive period end.
- Persisted `domain` and `occurredAt` while retaining deterministic fallback for old event records.
- Extended memory, storage-shape cleanup and reload/replay deduplication to reject duplicate `eventKey` values even when malformed data carries different event IDs.
- Added façade, contract, window, storage-parity and same-key-different-ID replay regressions to Runtime smoke.

## Verification

- Runtime smoke: `PASS: runtime loader/query/overlay/identity/registry/weekly-report/event-ledger smoke`.
- Main project: `dotnet build AWAKE.csproj -c Release -p:BannerlordApi=1.3.15 --no-restore`, `0 warnings / 0 errors`.
- Studio tests: `F01-F30`, `30/30 PASS`.
- Studio package and `release-check.ps1` under bundled `pwsh`: `PASS`.
- Contract JSON parse: `17 files`, no failures.
- Source build DLL SHA-256: `97F79C9160394CDCE8786BEB860E6D8B00532DA256D6ED33A5AA05A2912FEB05`.
- Frozen dist/game DLL SHA-256 remains `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`.
- Frozen worldbook manifest SHA-256 remains `2115664E1BB6BB26E63D8CB2097B891B51C03A00504D76DF6F8520FD9448468A` in source/dist/game.
- Root Release Check intentionally reports `BLOCKED_SYNC` because the source-only build is newer than the frozen dist/game candidate; no synchronization was performed.
- Bannerlord was not started and no game-directory file was modified.

## Remaining limits

- Runtime structural validation is intentionally a bounded validator, not a full JSON-Schema engine.
- v1 event visibility is a conservative default because current Ledger producers do not provide structured identity/location visibility inputs.
- E4/E5 gameplay and save/load evidence remain pending user-side validation of the frozen candidate; this batch does not alter that candidate.

## Next action

Close this source-only batch. Any future runtime candidate must receive a new BuildId and a separate review/sync step; do not overwrite `awake-20260820-syncpack-001`.
