# Marcus-Awake P3D-A0 Provider IPC Contract checkpoint

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3D-A0-PROVIDER-IPC-CONTRACT-20260827`
- `status`: `offline_verified`
- `plan`: `docs/PLAN-MARCUS-AWAKE-P3D-RUNTIME-INTEGRATION-20260827.md`
- `plan_revision`: `23`
- `review_status`: `completed_approved`
- `execution_lease`: `released`
- `blocker`: `none`

## Completed preparation

- Reconciled the revision 10 contract metadata and retained the required `route_id` in `provider.profile_upsert.v1`.
- Reconciled the P3B regression gate with the actual evidence shape: 19 top-level harness cases and 24 successful fine-grained evidence IDs.
- Reconciled the P3C gate with the actual evidence shape: 7 per-case records, `all_cases_passed=true`, and both durable recovery flags true.
- Confirmed the focused test output paths from the current Transport, Framework, and Runtime Service test project files.
- Recorded and repaired the two source focused-test defects: restored the Transport focused test entry and changed the Framework test double to call `RequestContext.IsExpiredAt(DateTimeOffset.UtcNow)`.
- Added the revision 10 corrections required by the independent review: exact contract-only error payload, declarative-only A0 `is_cloud` validation, executable stream sequence/field matrix, Provider `CausationId` gate, explicit file-level write set, and the user-signoff/implementation-authority distinction.
- Added the revision 11 correction that makes route-change behavior executable: active-provider tracking, route-change-after-visible-text rejection, and explicit ownership of candidate-membership validation by A2.
- Added the revision 12 corrections: fixed request→TaskScope output schemas, explicit header-only capability gate, pre-payload generic error contract, closed usage/error categories, fixed error precedence, Provider 19-value mapping authority, ProtocolCodec/StrictJson write-set, fixed Transport/Core/Service case IDs and counts, and frozen A0 evidence JSON shape. Clarified that header-only retains the nested JSON payload token/subtree without business-field parsing or envelope wire changes.
- Recorded Review 15 REVISE findings and locked revision 14 corrections: single admission coordinator, deadline matrix, verifier invariants, dependency graph and required joint subcases.

## Verification

- Existing P3B real child-process/private Named Pipe harness: `PASS_COUNT=19`, `FAIL_COUNT=0`.
- Existing P3B evidence: `real_child_process=true`, `real_private_named_pipe=true`, 24 fine-grained case IDs.
- Existing P3C evidence: 7 cases, all passed, durable receipt and crash-after-commit recovery both verified.
- No P3D-A0 production runtime code has been written.
- The two pre-existing focused-test defects in the declared A0 write set were repaired: restored `MarcusAwakeTransport/tests/Program.cs` and changed the Framework test double to call `RequestContext.IsExpiredAt(DateTimeOffset.UtcNow)`.

## Current source baseline verification

- `dotnet build MarcusAwakeTransport.Tests.csproj -c Release --no-restore`: passes with `0 warnings / 0 errors`; focused runner passes `7/7`.
- `dotnet build MarcusAwakeFramework.Tests.csproj -c Release --no-restore`: passes with `0 warnings / 0 errors`; Framework Core runner passes `20/20` cases.
- Production Release builds for `MarcusAwakeTransport`, `MarcusAwakeFramework`, `MarcusAwakeStorage`, `MarcusAwakeRuntimeService` and `MarcusAwakeProvider`: `0 warnings / 0 errors`.
- Existing `MarcusAwakeProvider.Tests.exe` focused suite: all listed Provider cases passed, including credential redaction, endpoint policy, structured output, SSE ordering, cancellation/deadline and fallback behavior.
- Current local Runtime Service harness regression: P3B `PASS_COUNT=19`, `FAIL_COUNT=0`; P3C evidence remains `7/7`. These are baseline regressions and do not constitute P3D-A0 evidence.

## Known limitations

- P3D-A0 revision 23 has independent `VERDICT: APPROVED`; the locked A0 write set is now implemented and independently verified.
- The current source focused-test baseline is buildable and runnable for Transport and Framework. The Transport test entry is a deterministic primitive suite, not a replacement for the P3B real child-process harness. Stale `_build_out` artifacts still do not satisfy any current-source gate.
- The latest completed independent read-only review returned `VERDICT: REVISE`; fresh revision 10/11 dispatches also failed before a verdict with local proxy `502 Bad Gateway` after upstream `503` retries. Neither authorizes implementation.
- Provider HTTP execution, credentials, profile persistence, MCM, AWAKE Caller, game-directory synchronization, real cloud Provider and Bannerlord evidence remain deferred to later boundaries.

## Next action

Create and independently review the P3D-A1 unary Provider bridge plan. Keep the A0 contract and evidence immutable; do not add Provider HTTP, credentials, MCM, AWAKE Caller, or game-directory changes to the A0 batch.

## Last error

`none`: revision 23 was approved and the locked A0 write set passed independent verification.


## Revision 15 preparation

- Recorded Review 16 as REVISE with five findings: admission-order contradiction, missing real header-only接线, non-unique deadline observation, unverifiable redaction outputs and schema-only ID uniqueness.
- Locked the concrete receive path, unique deadline outcomes, first-byte write linearization, four fixed redaction artifact paths with input bindings, and HashSet-based verifier checks.
- No P3D-A0 production runtime code has been written; implementation remains unauthorized pending the revision 15 independent read-only verdict.


## Revision 16 preparation

- Recorded Review 17 as REVISE with six findings: deadline/sequence conflict, duplicated admission ownership, unbound first-byte write commit, unverifiable redaction provenance, missing machine-readable evidence fields and missing Provider runner gate.
- Locked H0/H1-C/S0 ownership, H1-D close/no-response semantics, the PipeFrameIO write-commit seam, fixed redaction fixture manifest and schema mappings, machine evidence fields and Provider build/run commands.
- No P3D-A0 production runtime code has been written; implementation remains unauthorized pending the revision 16 independent read-only verdict.


## Revision 17 preparation

- Recorded Review 18 findings and revised the plan/schema without changing production code.
- Made H1-C the sole `frame_fence_proof` owner and Framework the sole semantic request hash generator/validator; Service only checks format and passes the value through unchanged.
- Added the controlled write sink contract, exact write observations, slot lease/backpressure lifecycle and post-S0 suppression marker/replay rules.
- Added the independent redaction-input schema and fixture, fixed artifact provenance, input whitelists, Provider 19-item mapping/output evidence contract, and the Runtime Service test project to the explicit write set.
- No P3D-A0 production runtime code has been written; implementation remains closed pending the revision 18 independent read-only verdict.


## Revision 18 preparation

- Recorded the two independent Review 20 REVISE verdicts and closed their evidence-contract findings.
- Added legal admission prefixes, required Service observations/subcases, explicit `now == deadline` expiry semantics, nullable invalid-deadline fields, write outcome invariants, suppression marker linkage, actual capture provenance, executable Provider runner provenance and actual Core mapping recomputation.
- Updated the evidence schema and redaction input manifest to revision 18; no production runtime code has been written and implementation remains unauthorized pending the revision 18 independent read-only verdict.

## Revision 18 continuation evidence — 2026-08-27

- Local JsonSchema.Net 9.4.0 verification succeeded with 0 warnings / 0 errors; the redaction-input fixture is valid, and the evidence schema rejects an incomplete fixture as expected.
- The two fresh independent review attempts failed before verdict with local proxy 502 Bad Gateway after upstream 503 retries. No implementation approval is inferred.

## Revision 19 contract preparation

- Recorded Euclid independent REVISE findings and clarified the exact Core mapping authority, result type, immutable redaction constants and mandatory runner/test-project bindings.
- The updated evidence and redaction schemas were recompiled with JsonSchema.Net 9.4.0; the redaction fixture remains valid.
- No production runtime code has been written; implementation remains unauthorized pending the revision 19 independent review.
## Revision 20 contract preparation

- Recorded Heisenberg independent REVISE findings and split parent/child Service provenance, unified the evidence plan revision, and corrected the Provider test project paths.
- The evidence schema now fixes plan_revision=20 and distinguishes runner_executable/runner_arguments/runner_exit_code from service_executable/service_arguments/service_exit_code.
- No production runtime code has been written; implementation remains unauthorized pending the revision 20 independent review.

## Revision 21 contract preparation

- Recorded the remaining revision 20 findings and completed the exact immutable `ProviderErrorMapping` result shape, input validation/preservation rules, and unknown-category result.
- Added `process_role=service_child` and exact capture command equality against the child Service provenance fields.
- Updated the evidence schema to `plan_revision=21` and kept all production implementation files untouched pending a fresh independent read-only review.

## Revision 22 contract preparation

- Recorded the revision 21 review findings and locked the exact public property types/accessors for `ProviderErrorMapping`.
- Added the three fixed unknown-category Core vectors and made `capture_binding.process_role` schema-required.
- Updated the active evidence contract and checkpoint to `plan_revision=22`; production implementation remains untouched pending review.

## Revision 23 contract preparation

- Recorded the revision 22 path-binding finding and closed the fixed artifact-key to scan-path to capture-path equality in the schema and verifier contract.
- Updated the active checkpoint to `plan_revision=23`; production implementation remains untouched pending a fresh independent read-only review.

## Revision 23 approval

- Independent reviewer Dirac returned `VERDICT: APPROVED`.
- Implementation gate is open only for the files and behaviors listed in the locked A0 write set.

## Revision 23 implementation and verification — 2026-08-28

- Implemented the locked A0 Transport/Core/Runtime Service protocol gate, Provider error mapping runner, redaction fixture handling and evidence capture without adding Provider HTTP, credentials, MCM or game Caller behavior.
- Added `tools/verify_marcus_awake_p3d_a0.ps1`; it rebuilds the seven fixed Release projects, runs the Transport/Core/Provider/Service entry points, recomputes case sets, Provider mapping, artifact hashes, fixture canonical hash and redaction scans.
- Independent verifier result: `P3D-A0 VERIFIER PASS`, exit code `0`; all seven builds passed with `0 warnings / 0 errors`.
- A0 evidence: Transport `6/6`, Core `6/6`, Provider mapping `19/19`, Service `8/8`; Provider runner reported `HTTP_REQUEST_COUNT=0` and `EXTERNAL_NETWORK=false`; existing IPC and Storage/RAG regressions remained green.
- Latest evidence: `framework/MarcusAwakeRuntimeService/tests/_build_out/Release/MARCUS-AWAKE-P3D-A0-evidence.json`; highest evidence level is offline `E2`.
- This checkpoint does not claim Provider HTTP execution, durable profile/credential storage, MCM configuration, AWAKE runtime Caller integration, Bannerlord runtime, game-directory synchronization or real cloud evidence.

## Next boundary

- Create a separately reviewed P3D-A1 unary Provider bridge plan. Keep the A0 contract and evidence immutable; do not modify the game directory or frozen candidates.
