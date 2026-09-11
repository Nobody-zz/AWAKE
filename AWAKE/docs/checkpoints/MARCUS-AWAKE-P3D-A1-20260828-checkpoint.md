# Marcus-Awake P3D-A1 Unary Provider Bridge checkpoint

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3D-A1-UNARY-PROVIDER-BRIDGE-20260828`
- `status`: `offline_verified`
- `plan`: `docs/PLAN-MARCUS-AWAKE-P3D-A1-UNARY-PROVIDER-BRIDGE-20260828.md`
- `plan_revision`: `7`
- `review_status`: `completed_approved`
- `execution_lease`: `released`
- `blocker`: `none`
- `evidence_level`: `offline E2`; no real cloud Provider or Bannerlord evidence is claimed

## Files changed

- `framework/MarcusAwakeRuntimeService/tests/P3DA1Harness.cs`
- `framework/MarcusAwakeRuntimeService/tests/Program.cs`
- `tools/verify_marcus_awake_p3d_a1.ps1`
- `docs/evidence/MARCUS-AWAKE-P3D-A1-20260828.json`
- `docs/evidence/MARCUS-AWAKE-P3D-A1-fake-http-capture-20260828.json`
- `docs/evidence/MARCUS-AWAKE-P3D-A1-harness-stdout-20260828.txt`
- `docs/evidence/MARCUS-AWAKE-P3D-A1-harness-stderr-20260828.txt`

## Verification

- Six declared Release builds passed with `0 warnings / 0 errors`.
- A1 fixed cases passed `23/23`; every planned S01–S17 and C01–C06 ID was executed and matched exactly.
- The harness used a real Runtime Service child process and a private authenticated Named Pipe.
- The primary Service completed explicit shutdown with exit code `0`; the child-shutdown case passed without an orphan.
- Local fake Provider HTTP observed `12` requests and `10` responses; the harness itself made no Provider HTTP request.
- Existing IPC regression passed `19/19`; Storage/RAG regression passed `7/7`; Provider contract mapping regression passed.
- Final evidence passed the `marcus-awake/p3d-a1-evidence.v1` JSON Schema using local `JsonSchema.Net 9.4.0`.
- Redaction scan passed over harness stdout, stderr, fake HTTP capture, regression outputs and final evidence; no secret marker or forbidden credential field was found.
- Evidence flags are explicitly `external_network=false`, `real_cloud_provider=false`, `bannerlord_started=false`, and `game_directory_synced=false`.

## Artifact hashes

- `MarcusAwakeTransport.dll`: `4acde7e5b6f365676a9bf0edc79a5cf85720f4ab7634f600ac157006518b5f14`
- `MarcusAwakeFramework.dll`: `b39520c44575ade5156e8f95b9edd6ce75e82e796d5a852ca8b9413831fd9e11`
- `MarcusAwakeProvider.dll`: `be02545f1f152789f9a1749d8d5458ba2da35ed05f2c562cd33059254f9f0cbf`
- `MarcusAwakeRuntimeService.exe`: `84cc4d6cb5617c35c6ef1ac795be2952e1f60a6d367eddc2108eda3e83a1490a`
- `MarcusAwakeRuntimeService.Tests.exe`: `8f28381969baf1a938ac5b64b676f38ca011da87852e4d80ae1e9592bfe07693`

## Known limitations

- A1 covers unary models and completion only. Streaming/SSE/NDJSON, stream cancellation races and route fallback remain P3D-A2.
- Profile/route durability, credential provisioning, restart recovery and durable Provider execution receipts remain P3D-B.
- AWAKE MCM, AWAKE caller integration, removal of the old `MarcusAIFramework` dependency, game-directory synchronization and Bannerlord E3/E4/E5 remain deferred.
- The Provider endpoint was a local deterministic fake; no real cloud or local Worker call was made.

## Next action

Create and independently review the P3D-A2 streaming/SSE/NDJSON and fallback plan; keep A1 evidence immutable.

## Last error

`none`
