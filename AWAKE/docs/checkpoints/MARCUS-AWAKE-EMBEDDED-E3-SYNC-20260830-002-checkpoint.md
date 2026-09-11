# Marcus-Awake embedded migration E3 sync checkpoint — 002

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-EMBEDDED-E3-SYNC-20260830-002`
- `status`: `pending_game`
- `evidence_level`: `E3`; synchronized file/package evidence is complete, E4/E5 are not claimed
- `execution_lease`: `released`
- `build_id`: `awake-20260829-marcus-embedded-002`
- `candidate_state`: `pending_game`
- `engineering_version`: `v0.2.0`

## Files changed

- `_houkai_merge/AWAKE/tools/sync_module.ps1`
- `_houkai_merge/AWAKE/tools/tests/sync_module.Tests.ps1`
- Current game module managed files under `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE`
- Durable pre-sync backup under `_houkai_merge/AWAKE-backups`

## Verification

- Sync regression: `15/15 PASS`.
- Sync report: `docs/sync-reports/sync-20260829-marcus-embedded-002-game-runtime-fix.json`, `state=verified`.
- Bannerlord/TaleWorlds process snapshot before sync: empty.
- `Awake.dll`, `MarcusAwakeFramework.dll`, and `MarcusAwakeTransport.dll` match source and dist SHA-256 values.
- Embedded Runtime: `197` files present in the game target; manifest and `SHA256SUMS.txt` match dist.
- `tools/release_check.ps1`: `RELEASE_CHECK_OK`, `RELEASE_STATUS=CANDIDATE_SYNC_OK`.
- No files removed by the sync; preserved target files passed the script verification.
- Pre-sync backup: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE-backups\game-AWAKE-before-awake-20260829-marcus-embedded-002-20260830-014707`.

## Known limitations

- Bannerlord has not been started with this BuildId; no E4 gameplay log exists.
- Real cloud Provider and real API Key have not been used or verified.
- Save/load, restart, duplicate-submit, timeline isolation, and long-run regression have no E5 evidence.
- The sync script required a Runtime-management fix because the first verified transaction did not copy `bin\\Win64_Shipping_Client\\Runtime`; the corrected script and regression test now cover this path.

## Next action

- User runs Bannerlord with BuildId `awake-20260829-marcus-embedded-002`, exercises the E4 checklist, exits the game, and provides the matching AWAKE log.
- After E4, run the matching E5 save/load, restart, cancellation, duplicate-submit, and long-run checks.

## Last error

- No unresolved sync error. The first wrapper invocation misclassified a completed PowerShell script because `$LASTEXITCODE` was empty; the written transaction report and independent verification established the actual `verified` result.
