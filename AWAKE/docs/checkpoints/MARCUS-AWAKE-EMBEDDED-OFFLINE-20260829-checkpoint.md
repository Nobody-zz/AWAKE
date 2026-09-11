# Marcus-Awake embedded migration offline checkpoint

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `checkpoint_id`: `MARCUS-AWAKE-EMBEDDED-OFFLINE-20260829`
- `date`: `2026-08-29`
- `status`: `implementation_in_progress`
- `execution_lease`: `active`
- `blocker`: `source_changed_after_candidate_build`; the recorded artifact snapshot requires a rebuild before synchronization or final-candidate attribution
- `evidence_level`: `E2` for the recorded offline snapshot; current source has no rebuilt evidence; no E3/E4/E5 claim
- `build_id`: `awake-20260829-marcus-embedded-001` (recorded artifact snapshot, not current/final candidate)
- `candidate_state`: `stale_until_rebuild`
- `engineering_version`: `v0.2.0`

## Completed in this checkpoint

- Recorded AWAKE Release build for Bannerlord API `1.3.15`; the build completed with `0` errors and four pre-existing `CS1998` warnings.
- Rebuilt and validated the self-contained `win-x64` embedded Runtime package: `195` payload files plus `manifest.json` and `SHA256SUMS.txt` (`197` files total).
- Updated `tools/sync_module.ps1` so the declared `$managedRootFiles` list is actually used by `Get-ManagedFiles`; `BUILD_VERIFICATION.txt` is now copied as a managed root file during dist-only synchronization.
- Reconciled `docs/AWAKE-CURRENT.md`, `docs/AWAKE-VALIDATION.md`, root `BUILD_VERIFICATION.txt`, and dist `BUILD_VERIFICATION.txt` with the recorded artifact snapshot hashes.
- Completed dist-only synchronization twice after the fix; the latest recorded transaction is `docs/sync-reports/sync-20260829-212959651.json`, state `verified`, `skip_game=true`, completed at `2026-08-29T13:30:26.9432261Z`, and it did not touch the game directory.

## Recorded artifact snapshot identity

The following hashes identify the last internally consistent source/dist build snapshot. They do not fingerprint the current source worktree after the post-build edits listed below, and therefore do not constitute a current or final candidate.

| Artifact | SHA-256 |
|---|---|
| `Awake.dll` source/dist | `337E4C0B8D32B13912C30A45EE59B38BC1728027357B00367996C6C455616478` |
| `MarcusAwakeFramework.dll` source/dist/Runtime | `00B34BF79DEFADBE4A946612244EA8631CE777495C2B99ADD11F2B10CE8C7F55` |
| `MarcusAwakeTransport.dll` source/dist/Runtime | `DE81809AC08EA86BE90FD6F0AB77FA348B24B744588B0AF970DFFF2080E6DF48` |
| `MarcusAwakeRuntimeService.dll` source/dist Runtime | `8EE6C3F10A5412E79C6388E8F7DCCFFD1D9897B6B12A4EA70F300D4F1F7084D6` |
| `MarcusAwakeRuntimeService.exe` source/dist Runtime | `84CC4D6CB5617C35C6EF1AC795BE2952E1F60A6D367EDDC2108EDA3E83A1490A` |
| `MarcusAwakeProvider.dll` source/dist Runtime | `B74E38BFCE9DFDA5F51642933002819AA710B9442A53279B829A8EEBFACA8CEB` |
| `MarcusAwakeStorage.dll` source/dist Runtime | `8D23EA45E2F56A0D21B353634FE216F2A176E3D239795E3D0EC0964BD0C83F8A` |
| Runtime `manifest.json` | `58A601B48B32F03B6650AEA4B8694A22F262465ABB94A9D688B8DE11886122AC` |
| Runtime `SHA256SUMS.txt` | `765160E3F2424D3B6E5582AFE422B8C105770701A6B52A71F4C24719A25AF422` |

## Verification evidence

- Framework Core: `20/20`; P3D-A2 Framework: `6/6`.
- Provider regression: `PASS ALL`; Transport: `7/7`, P3D-A0 contract `6/6`, P3D-A1 contract `1/1`.
- Storage regression: `PASS ALL`.
- Runtime Service: P3B `19/19`, P3C `7/7`, P3D-A0 `8/8`, P3D-A1 `24/24`, P3D-A2 `21/21`.
- MCM static contract: `47/47`; AWAKE caller static contract: `7/7`.
- API layers: E1 `PASS`; legacy API `109/109` preserved, current public API `199`, additions `90`, unclassified `0`.
- Runtime package validation: `RUNTIME_PACKAGE_OK`.
- Recorded release gate: `RELEASE_CHECK_OK` with `RELEASE_STATUS=BLOCKED_SYNC`.
- Final runtime consistency: `16` pass, `0` failures, `4` expected `BLOCKED_SYNC` entries.

## Explicit limits

- The source worktree changed after the recorded `Awake.dll` build at `2026-08-29 21:10:21` (Asia/Shanghai): `src/AwakeConfig.cs`, `src/NpcDialogueService.cs`, and `src/NpcMemoryService.cs` are newer than that build and have not been rebuilt or revalidated. The recorded BuildId and hashes are consequently stale until a new build.
- The Bannerlord game directory still contains the prior candidate: current game `Awake.dll` is not the current BuildId hash, the new Framework/Transport DLLs are absent, and the embedded Runtime directory is absent.
- No Bannerlord process was started or modified; no game-directory synchronization was performed.
- No real cloud Provider, real API Key, or real network evidence was used.
- No matching BuildId game log exists; E3 synchronization, E4 gameplay closure, E5 save/load and long-run regression remain unverified.
- Static MCM/caller contracts prove source wiring only; they do not replace user-run in-game verification.

## Next action

- No four-agent read-only wait remains; the earlier P3D-A2 review is closed and its checkpoint is retained as historical evidence.
- Review the post-build changes in `src/AwakeConfig.cs`, `src/NpcDialogueService.cs`, and `src/NpcMemoryService.cs`, then rebuild AWAKE and the embedded Runtime package and generate a new BuildId/hash set.
- Rerun the release/runtime consistency checks, including the structured-result consumer path, before any game-directory synchronization or final-candidate claim.
- After the new candidate is internally verified, and only after explicit authorization with Bannerlord closed, synchronize it and request matching E4/E5 evidence.
