# Marcus-Awake embedded migration offline checkpoint — 002

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `checkpoint_id`: `MARCUS-AWAKE-EMBEDDED-OFFLINE-20260829-002`
- `date`: `2026-08-29`
- `status`: `offline_candidate_ready_for_authorized_sync`
- `execution_lease`: `released`
- `evidence_level`: `E2` for the current offline candidate; no E3/E4/E5 claim
- `build_id`: `awake-20260829-marcus-embedded-002`
- `candidate_state`: `ready_for_authorized_sync`
- `engineering_version`: `v0.2.0`

## Completed

- Rebuilt AWAKE Release for Bannerlord API `1.3.15` with `BUILD_OK`, exit code `0`, and no compiler warnings or errors reported by the direct MSBuild run.
- Rebuilt the self-contained `win-x64` embedded Runtime package: `195` payload files plus `manifest.json` and `SHA256SUMS.txt` (`197` files total); `RUNTIME_PACKAGE_OK`.
- Ran Framework `20/20` plus P3D-A2 Framework `6/6`; Transport `7/7` plus P3D-A0 `6/6` and P3D-A1 `1/1`; Provider `PASS ALL`; Storage `PASS ALL`.
- Ran Runtime Service `19/19`; P3C `7/7`; P3D-A0 `8/8`; P3D-A1 `23/23`; P3D-A2 `21/21`; provider stream cleanup `3/3`.
- Ran production worldbook/runtime smoke `17/17`; synchronization script regression `14/14`.
- Ran current API layering E1 audit: legacy `109/109` preserved, current public API `199`, additions `90`, unclassified `0`, unexpected source/assembly/public-API findings `0`.
- Ran current MCM static contract `47/47` and AWAKE caller static contract `7/7`.
- Ran `release_check.ps1`: `RELEASE_CHECK_OK` / `RELEASE_STATUS=BLOCKED_SYNC`; the blocked portion is only the intentionally old game copy.

## Artifact identity

| Artifact | SHA-256 |
|---|---|
| `Awake.dll` source/dist/staging | `D00478D7B796974B4FFF6EDF385B6E31A7B3F712BECE89E5C013F7438957FEBE` |
| `MarcusAwakeFramework.dll` | `00B34BF79DEFADBE4A946612244EA8631CE777495C2B99ADD11F2B10CE8C7F55` |
| `MarcusAwakeTransport.dll` | `DE81809AC08EA86BE90FD6F0AB77FA348B24B744588B0AF970DFFF2080E6DF48` |
| Runtime `manifest.json` | `58A601B48B32F03B6650AEA4B8694A22F262465ABB94A9D688B8DE11886122AC` |
| Runtime `SHA256SUMS.txt` | `765160E3F2424D3B6E5582AFE422B8C105770701A6B52A71F4C24719A25AF422` |
| Worldbook `manifest.json` | `2115664E1BB6BB26E63D8CB2097B891B51C03A00504D76DF6F8520FD9448468A` |

## Clean staging

- Staging root: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE-release-staging\awake-20260829-marcus-embedded-002`.
- Staging report: `docs/sync-reports/release-staging-awake-20260829-marcus-embedded-002.json`.
- Strict allowlist contains `972` files; Runtime subset contains `197` files; development docs, source, PDB, logs, task queues and secret-bearing files are excluded.

## Explicit limits

- The game directory was not synchronized. Game `Awake.dll` remains the prior hash `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`; embedded Framework/Transport and Runtime are absent there.
- Bannerlord was not started; no matching `002` game log exists. E3 synchronization, E4 gameplay and E5 save/load/long-run evidence remain unverified.
- No real cloud Provider, real API Key, or external network was used. Provider tests use fake HTTP/SSE or in-memory fixtures.
- The current phase-scoped `verify_marcus_awake_p3a.ps1` passes (`build=0`, `api=True`, `forbidden=True`, source/assembly findings `0`, scanned `22`, deferred `53`); `verify_marcus_awake_api_layers.ps1` also passes and remains the authoritative API-layer audit. Only the earlier broad forbidden-scan rule is historical because its assumptions predate the embedded IPC/process/runtime architecture.
- Worldbook Studio candidates remain frozen and were not modified.

## Next action

- If game validation is desired, confirm Bannerlord is closed and explicitly synchronize this `002` candidate; then collect a user-run log containing the same BuildId before claiming E4/E5.
