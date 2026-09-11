# Worldbook Studio Approval to Compile Closure Contract Addendum

- Batch: WORLDBOOK-STUDIO-APPROVAL-COMPILE-CLOSURE-CONTRACT-20260901
- Date: 2026-09-01
- Risk: high-risk
- Parent plan: PLAN-WORLDBOOK-STUDIO-APPROVAL-COMPILE-CLOSURE-20260901.md
- Purpose: close the Web, CLI, wire-schema, frontend proof-lifecycle, and export-staging idempotency gaps found by the challenge review.
- Scope: this document only defines the contract and acceptance addendum. The implementation file list below is downstream scope after explicit signoff; no implementation file is modified in this review batch.

## Scope boundary

This addendum does not change publish-operation binding, recovery takeover, cross-process CAS, R14 migration, game files, real providers, versioning, or customer ZIP generation.

## Contracts to lock

### Customer HTTP wire schema

- /api/authoring/compile allows exactly compile_proof_id, confirmation_token, and output.
- /api/authoring/export-staging allows exactly compile_proof_id, confirmation_token, and output.
- Customer request fields use snake_case. Unknown fields must be rejected instead of silently ignored.
- path, source_hash, revision, Path, sourceHash, Revision, and equivalent casing aliases return 422 with zero side effects. content_tier and operation_id are also rejected on customer routes.
- Missing body, malformed JSON, or missing compile_proof_id returns 400. Unknown proof returns 404. Invalid approval, selection, document, registry, or reference-closure binding returns 409.
- Customer routes do not accept operation_id. The authority export route keeps its explicit operation_id; the two contracts must not be mixed.

### Approval and selection binding

- Compile-proof requests accept operation_id, approval_id, and content_tier. selection_id is derived server-side from ApprovalProof.selection_id and is not a second client authority input.
- Issuing a CompileProof must verify that the approval points to an existing selection, that IDs and selection hashes agree, and that the approval item snapshot equals the selection item snapshot. Any mismatch returns 409.
- CompileProof persists approval_id, derived selection_id, selection_hash, document revision/content hashes, registry snapshot hash, reference closure hash, content tier, and canonical request digest. Consumption rechecks the complete closure.

### Export-staging idempotency

- Authority export keeps caller-supplied operation_id. Its canonical digest includes compile proof ID, proof hash, selection ID, selection hash, canonical output root, and confirmation-token semantic summary. Same ID plus same digest returns the original staging; same ID plus different digest returns 409.
- Customer export generates one server-side operation ID per compile proof and excludes it from the customer wire schema: customer.export.<compile_proof_id>. The output root is normalized and policy-checked before digest comparison; raw path strings are never concatenated into an unsafe ID.
- Retrying customer export with the same proof, output, and confirmation-token semantic digest returns the same staging. Changing output or confirmation-token semantics always returns 409 and never creates a second operation or overwrites prior staging.
- Staging calls AtomicCandidatePublisher.Publish with updatePointer false. The current pointer and existing PublishStaging contract remain unchanged.

### Core and CLI call surface

- The parent batch narrows WorldbookApplicationService.Compile, CompileExact, Export, and WriteCompiled to Core-internal implementation. Existing test friend assemblies may use them for focused compiler tests, but Web and CLI cannot use them as bypasses.
- AuthorityGate.CompileApproved(compileProofId, confirmationToken, outputRoot) is the only Web and CLI compiled settlement entry point. It returns an explicit DTO containing CompileResult and compiled path. Invalid proof fails before any output, journal, or provider side effect.
- src/Awake.WorldbookStudio.Cli/Program.cs is in scope. CLI compile must stop calling WriteCompiled directly; CLI export remains proof-backed staging. Web and CLI share authority error codes. CLI failures emit side_effect=none and exit code 3; --proof maps to compile_proof_id.
- Retired Web routes remain 410 with side_effect=none. Missing proof on current compile/export commands is not disguised as a retired route.

### Frontend proof lifecycle

- Opening or saving a document clears old proof state. State stores compile proof ID, approval ID, selection ID, revision/content hashes, registry/reference closure hashes, and content tier.
- Compile and export-staging controls are enabled only while the current proof exists and remains bound. Requests contain proof-backed fields only; no path, sourceHash, revision, or contentTier fields.
- Frontend proof state is an explicit state machine: none → registered → selected → approved → proof_issued → ready; any document open/save or authority 409 transitions to stale, disables compile/export, clears the proof ID, and requires a fresh register/selection/approve/compile-proof sequence. No implicit approval or automatic stale-proof replay.

## Files and tests

- Core: src/Awake.WorldbookStudio.Core/AuthorityGate.cs, Application.cs, and required contracts/models.
- Web: src/Awake.WorldbookStudio.Web/Program.cs and strict JSON request validation.
- CLI: src/Awake.WorldbookStudio.Cli/Program.cs.
- Frontend: src/Awake.WorldbookStudio.Web/wwwroot/studio-editor-safety.js and directly related state code.
- Tests: AuthorityGate tests, existing Core test call-site migrations, CLI contract tests, HTTP wire fixtures, and tests/frontend/customer-closure.test.js. Add a UTF-8/control-character scan and exact token scan for this contract document before review completion.

## Acceptance matrix

1. Customer compile/export exact-set fields: old or unknown fields return 422; missing proof returns 400; unknown proof returns 404.
2. Approval, selection, and proof ID/hash mismatch returns 409; document, registry, or reference-closure changes return 409; no output, staging, journal, or provider call occurs.
3. Same proof operation digest is idempotent; different approval, selection, selection hash, tier, output, or token semantics return the fixed conflict and cannot replay the old proof. Customer export retries with changed output/token always return 409 without a second operation.
4. Web and CLI do not call WriteCompiled directly; the four original Core methods cannot provide an unproofed external path.
5. Positive flow reaches approval proof, compile proof, CompileApproved, CompileResult, and compiled path. Export-staging leaves current pointer unchanged.
6. Frontend state transitions are observable for none/registered/selected/approved/proof_issued/ready/stale; proof state expires on open, save, and 409; compile/export requests contain only proof-backed fields.
7. Web and CLI error semantics match; retired routes remain 410 with side_effect=none.

## Review gate

- Run up to three independent review rounds for this high-risk addendum.
- After review reaches awaiting_signoff, wait for explicit user signoff.
- After signoff, add failing regressions first, implement the parent batch plus this addendum, then run AuthorityGate, CLI, HTTP wire, frontend, and full Studio harnesses.
- Before signoff, verify this file is UTF-8 without control characters and contains the exact contract tokens compile_proof_id, approval_id, selection_id, revision, source_hash, content_tier, and operation_id.
