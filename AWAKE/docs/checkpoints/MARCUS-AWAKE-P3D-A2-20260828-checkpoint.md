# Marcus-Awake P3D-A2 Streaming Provider and Fallback checkpoint

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3D-A2-STREAMING-FALLBACK-20260828`
- `status`: `offline_verified`
- `plan`: `docs/PLAN-MARCUS-AWAKE-P3D-A2-STREAMING-FALLBACK-20260828.md`
- `plan_revision`: `8`
- `review_status`: `implementation_authorized`
- `execution_lease`: `active`
- `blocker`: `none_for_offline_batch`; remaining limits are evidence ceilings and later integration scope
- `evidence_level`: `offline E2`; no real cloud Provider, Bannerlord or game-directory evidence is claimed

## Files changed

- `src/AwakeHostComposition.cs`
- `framework/MarcusAwakeFramework/MarcusAwakeFramework.csproj`
- `framework/MarcusAwakeFramework/tests/Program.cs`
- `framework/MarcusAwakeRuntimeService/src/ProviderRegistry.cs`
- `framework/MarcusAwakeRuntimeService/tests/P3DA2Harness.cs`
- `docs/evidence/MARCUS-AWAKE-P3D-A2-rerun-20260828.json`
- `docs/AWAKE-CURRENT.md`
- `tools/verify_marcus_awake_p3a.ps1`
- `tools/verify_marcus_awake_api_layers.ps1`
- `docs/evidence/MARCUS-AWAKE-P3-API-SURFACE-BASELINE-CURRENT-20260829.json`
- `docs/evidence/MARCUS-AWAKE-API-LAYERS-E1-20260829.json`

## Implementation result

- Host disposal no longer marks the composition disposed and then short-circuits its own session drain.
- Session drain is tracked as one idempotent task; Runtime Service is disposed only after the tracked drain completes.
- Extension unregistration, Host close and Locator clear are deferred until the drain completion path.
- `FrameworkHostLocator.Register(extension, runtime)` is covered by a new regression asserting that the injected Runtime is retained by `host.Runtime` and exposed through `host.Ai`.
- The legacy no-Runtime default Host path remains covered and still reports the explicit unavailable AI gateway.
- Temporary bridge-stage `Console.Error` output and duplicate Service stderr printing were removed; typed bridge errors and test protocol output remain.
- The Framework project now explicitly references the local `netstandard` and `System.ValueTuple` facades required by the current `net472` build entry.

## Verification

- `tools/build.ps1 -BannerlordApi 1.3.15 -Configuration Release` completed with `0` errors; the existing build emits four `CS1998` warnings outside this patch.
- `MarcusAwakeFramework.Tests.exe` passed `20` Framework Core cases, `6` P3D-A2 Framework cases, and the new injected Runtime Host assertions.
- `MarcusAwakeRuntimeService.Tests.exe --p3d-a2` passed `21/21` Service/IPC cases using a local child process, private Named Pipe and local fake HTTP.
- `MarcusAwakeProvider.Tests.exe` passed all Provider regression cases.
- `MarcusAwakeTransport.Tests.exe` passed `7/7` cases.
- Rerun evidence: `docs/evidence/MARCUS-AWAKE-P3D-A2-rerun-20260828.json`, SHA-256 `97A6AEA1BFB899F47D6DBD4AF9F6A2E5201DCED094217E64F1005E1B9AD7E2DE`.
- AWAKE Release DLL SHA-256: `16A3ABF41785AD8FDEF3A2B27909553608BD6867971C7E6521EA02FFC4A58DFA`.
- Framework, Provider, Transport and Runtime Service artifact hashes were recorded from the current local Release outputs during this run.
- `verify_marcus_awake_api_layers.ps1` passed E1: legacy API `109/109`, current API `199`, added `90`, unclassified `0`, unexpected static findings `0`.
- `package_embedded_runtime.ps1` rebuilt and validated the self-contained `win-x64` Runtime package with `195` files; `release_check.ps1` passed with `RELEASE_STATUS=BLOCKED_SYNC`.

## Known limitations

- The 21 planned scenarios are now all executed and pass; the remaining limitations below are evidence-quality or later-scope limits, not missing S07/S14 cases.
- Current P3D-A2 evidence does not yet prove Ollama NDJSON execution, replay capacity/TTL, reservation and working-ledger eviction behavior, real deadline races, fallback usage settlement, remove/recreate ABA, lock-order probing, or cross-assembly byte-for-byte canonicalizer vectors.
- The current `S16a-S16c` evidence labels the Service surface but exercises the local state machine directly rather than a full IPC path; this remains an evidence-quality issue, not a new runtime claim.
- `AWAKE.csproj`, `SubModule.xml`, Runtime Service packaging, AWAKE MCM, real AWAKE caller integration, old dependency removal, game-directory synchronization, Bannerlord runtime and save/load remain outside this checkpoint.

## Next action

Preserve the fixed 21-case evidence set, integrate the new layered API/package gates into the release flow, then continue real AWAKE caller wiring. Do not start Bannerlord or synchronize the game directory in this offline batch.

## Last error

`none` in the completed verification runs; the batch remains open because real caller integration, game-directory synchronization, Bannerlord runtime and E3/E4/E5 evidence remain outside this offline batch.
