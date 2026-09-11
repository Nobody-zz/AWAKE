# PREP-20260820-1 Checkpoint

- `task_id`: `PREP-20260820-1`
- `batch_id`: `awake-next-round-preflight-20260820`
- `status`: `pending_game`
- `files_changed`: `AWAKE.csproj`; `..\\AWAKE.Tests\\AWAKE.Tests.csproj`; `tools/build.ps1`; `tools/sync_module.ps1`; `tools/release_check.ps1`; `tools/tests/sync_module.Tests.ps1`; canonical dist managed files; `docs/sync-reports/dist-repair-20260820-230850.json`; this checkpoint; `docs/AWAKE-CURRENT.md`.
- `verification`: 1.3.15 Release Rebuild `BUILD_OK`; AWAKE.Tests rebuild passed; `Awake.SdkSmoke` PASS ALL; sync regression tests PASS ALL (7); E3 transaction `verified`; release check `RELEASE_CHECK_OK` / `RELEASE_STATUS=CANDIDATE_SYNC_OK`; source/dist/game DLL SHA-256 `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`; source/dist/game worldbook manifest matches; canonical Persona paths pass in all three locations; game nested Persona count 0; Bannerlord process snapshot before sync count 0.
- `known_limitations`: 3 pre-existing CS1998 warnings remain; no matching v1.4.8 Bannerlord game root/reference set exists locally, so no truthful 1.4.8 build was claimed; 15 pre-existing worldbook placeholder findings remain; E4/E5 require user gameplay and save/load evidence.
- `next_action`: user launches the frozen candidate, performs E4 gameplay checks, exits Bannerlord, then provides the matching log and save/load sequence for E5 review.
- `last_error`: none; E3 verified, waiting for E4/E5 game evidence.