# Plan: Worldbook Studio 批量作者实现修复 2026-08-25

- status: internal_candidate_ready_release_blocked
- parent_plan: `docs/PLAN-WORLDBOOK-STUDIO-BATCH-AUTHORING-20260824.md`
- contract: Revision 13, SHA-256 `217E7538DC7C356D092ABC029A79E7504EE36BC1000B0B3FD3F7FF261FDCB4A9`
- review_source: `docs/PLAN-WORLDBOOK-STUDIO-BATCH-AUTHORING-20260824-REVIEW-LOG.md` current implementation review
- code_gate: approved by current-primary independent read-only review; lease `WBSA-REPAIR-20260825-01`

## Confirmed facts

- The current contract checker passes under PowerShell 7 and Windows PowerShell 5.1.
- The current source compiles and the existing BatchTests pass, but the independent implementation review found eight P1 contract/projection defects.
- The public contract already contains the required behavior; this repair must not revise the contract, add states, or broaden the product scope.
- The user-authorized objective remains the complete batch authoring loop: scan, create, Provider facts, evidence review, metadata, needs_review document creation, recovery/cache/API/UI and tests.

## Bounded repair

1. Change cancelled pending items to the contract-supported public item state `skipped` while retaining a user-readable cancellation reason.
2. Reject every claim reason while another owner lease is unexpired; `manual_reclaim` is not an override.
3. Project report counts into exactly `total`, `queued`, `running`, `review_pending`, `ready_to_create`, `created`, `failed` and `unknown_result`, including zero values.
4. Recompute the accepted fact-set hash from the current facts result and accepted fact IDs before metadata generation/commit; compare it with the item and request hash.
5. Make metadata retry return the full current review projection, including existing facts, evidence, source unit and metadata candidates.
6. Add a dedicated public metadata projection that removes attempt/provider/internal fields and emits the public schema version.
7. Make item-detail return the contracted review projection wrapper with `item`, `facts`, `evidence`, `source_units` and `metadata_results`.
8. Bind every batch endpoint to the registry route names at startup and validate request/response/error projection boundaries without exposing internal fields.

### Fixed report mapping

- `queued` and `metadata_pending` → `queued`.
- `extracting` and `metadata_running` → `running`.
- `facts_review` → `review_pending`.
- `ready_to_create` → `ready_to_create`.
- `created` → `created`.
- `failed` and `skipped` → `failed` only when the item has a failure code; cancelled/skipped items remain represented in the item list but do not create an unrecognized count key.
- `unknown_result` → `unknown_result`.
- `total` always equals the item count; all eight keys are emitted with zero when absent.

## Acceptance cases

- Cancelling a running batch leaves every pending item in `skipped`, and the public item validates against `awake.worldbook.batch-item-public.v2`.
- A claim from a different owner fails before lease expiry for `process_restart`, `lease_expired` and `manual_reclaim`; it succeeds after expiry and increments `claim_generation`.
- The current owner cannot claim a second lease while its existing lease is valid; after a successful rebind, the old fence and generation are rejected and old unconsumed consent is revoked.
- An empty report still contains all eight fixed count fields with zero values; intermediate statuses map to the defined public buckets.
- The server recomputes the canonical accepted fact-set hash before metadata start and again before result commit; changing facts or accepted IDs causes a hash conflict, no Provider/cache materialization, and no metadata result commit.
- Metadata retry preserves the current facts/evidence/source-unit data in its response.
- Public metadata validates positively against `awake.worldbook.batch-metadata-result-public.v2`; item-detail validates positively against `public_review_projection` with `item`, `facts`, `evidence`, `source_units` and `metadata_results`, and contains no `attempt_id`, `provider_fingerprint`, `created_from_item_revision`, `document_path`, lease or owner fields.
- Every one of the 15 contract routes has a registry binding and positive request/response/error schema validation: `scan`, `create`, `get`, `consent`, `start`, `pause`, `cancel`, `claim`, `retry-item`, `review`, `create-documents`, `report`, `item-detail`, `source-unit`, and `prebatch-source-unit`.
- The complete HTTP Smoke reaches scan → create → consent → facts → review → metadata → create-documents → report, includes duplicate create-documents, item/source detail, pause/cancel/claim/retry route checks, CSRF/Origin rejection, and asserts all generated documents are `needs_review` with no expressions or identity/person/family bindings.
- `scripts/test.ps1` invokes BatchTests and `scripts/release-check.ps1` consumes the resulting exit code/evidence; both links are asserted in the release evidence.

## Invariants

- No change to Revision 13 contract JSON, schema hash, state enum, consent timing, cache formula or persistence authority.
- No automatic canon publication, no expressions generation, no person/family binding, no game startup and no game-directory synchronization.
- Existing single-draft, Draft, Launcher and Worker/Provider boundaries remain unchanged.
- All generated documents remain `needs_review`.

## Write set after approval

- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/BatchControlService.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/BatchExecutionService.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/BatchMetadataRepository.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/BatchWorkflowService.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/BatchEndpoints.cs`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.BatchTests/Program.cs`
- `tools/worldbook-studio/scripts/release-check.ps1`
- `tools/worldbook-studio/scripts/batch-workflow-smoke.ps1` (new)

## Evidence minimum

- Independent review: exact `VERDICT: APPROVED` for this bounded plan.
- Focused BatchTests covering all eight repairs.
- BatchTests cover the fixed report mapping, hash preconditions and commit recheck, current-owner/old-fence/old-consent claim cases, all public schema positives, and `needs_review`/no-binding invariants.
- Full Release build with zero warnings/errors.
- Real local HTTP Smoke with fake Provider/Worker and cleanup evidence.
- Route matrix evidence covers all 15 routes and their request/response/error schema references.
- Package/release-check against the current source and contract hash.
- `scripts/test.ps1 → BatchTests → scripts/release-check.ps1` exit-code and evidence chain.
- Final independent read-only review with no P0/P1 findings.

## Non-goals

- Do not redesign the batch contract or add a second workflow.
- Do not add the batch editor UI in this repair plan; UI is a separate follow-up implementation slice after the API contract is verified.
- Do not “fix” unrelated historical debt in Worldbook Studio.
- Do not claim cloud Provider or Bannerlord runtime verification from local tests.

## Implementation handoff

- BatchTests: `13/13 PASS`.
- Full Studio harness: `101/101 PASS`; Draft tests: `9/9 PASS`.
- Draft and Batch HTTP Smoke: `PASS`; generated documents remain `needs_review` with no expressions or identity/person/family bindings.
- `release-check.ps1` now executes `test.ps1`, `batch-contract-check.ps1`, and current authoring-schema hash verification.
- Current-source internal candidate: `tools/worldbook-studio/artifacts/WorldbookStudio-win-x64-internal-candidate.zip`.
- Formal release is intentionally blocked by the frozen Revision 13 contract field `test_entrypoints.wired_now=false` and `WB-RELEASE-033`. Do not modify Revision 13 in place; a future formal-release task requires a separately reviewed contract wiring/revision step.
