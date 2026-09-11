# Worldbook Studio AI Authoring R1 — Paused Checkpoint

- Task: `WORLDBOOK-STUDIO-AI-AUTHORING-20260902-R1`
- Date: `2026-09-04` (Asia/Shanghai)
- Status: `implementing`
- Evidence boundary: `E2`; no Bannerlord launch, no game-directory sync, no real Provider access, and no AWAKE Worker endpoint execution.
- Review: first R1 re-review returned `REVISE`; latest independent reviews found no confirmed P0, but reliability P1 items remain. Final R1 review is still pending.
- User direction: local Worker may be used after implementation, but test samples must be newly created from actual Worldbook Studio worldbook editing scenarios, not generic fixed samples.

## Changes made before pause

- Added `src/Awake.WorldbookStudio.Core/AuthoringLifecycleContracts.cs` with source snapshot, generation packet, candidate, candidate-set, provider fingerprint, invalidation, and public projection contracts.
- Added `AuthoringDraftStage.Complete` and complete-request source transport support.
- Added candidate-set projection to Draft HTTP output.
- Draft provider results now attach a candidate set and resolved provider fingerprint.
- Draft store now treats empty stage output as authoritative for that stage instead of silently preserving the old layer.
- Frontend first Draft action now requests one complete generation; legacy stage actions remain available.
- Batch cache evidence replay now validates and reuses stored offsets instead of first-match rebinding.
- Batch zero-fact results now enter explicit `no_candidate` state and can be deliberately retried.
- Batch document creation now carries source evidence/provenance and uses a named cross-process mutex; uncertain `creating` state is recovered or quarantined.

## Current blocker

- The original compiler blocker is fixed; Release build is `0 warnings / 0 errors`.
- Focused evidence: Draft `21/21`, Batch `20/20`, Worldbook Studio harness `113/113`, EditorContent `7/7`, Draft HTTP smoke `9/9`, authoring-save smoke `9/9`, Batch HTTP smoke `13/13`, and public-contract smoke passed.
- Draft attempts now expose `attemptId`, reject stale writes, classify timeout/transport/cancel as `unknown`, and permit explicit retry with a new attempt.
- Cache evidence now reuses exact offsets and rejects stale offsets; duplicate-quote and stale-cache regressions are covered.
- Generated author documents now preserve source/candidate/provider provenance and evidence under the validated `author_created.provenance` block.
- Draft state is now persisted under `authoring/draft-state/state.v1.json`; attempt status/result recovery and explicit reconcile route are implemented.
- Draft results now accept multiple candidate payloads; candidate selection is exposed in the editor and selected candidate IDs are bound during document creation.
- Batch evidence groups preserve all sidecars with binding/hash validation; Batch attempts carry a canonical packet hash into document provenance.
- Candidate selection now rejects switching candidates after a document has been created instead of returning the old document silently.
- Draft validation now indexes flat results, CandidateSet, and compatibility Candidates together; omitted evidence groups are restored from the server-authoritative result.
- Batch attempts persist packet input projections and document creation independently recomputes and validates packet hashes.
- Draft state persistence now uses a named cross-process mutex, `FileOptions.WriteThrough`, explicit stream flush, and temporary-file cleanup before atomic replacement.
- Draft state now carries a persisted revision and rejects stale cross-process writer snapshots with `WB-AI-DRAFT-CAS-409`; pure state reads no longer increment the revision.
- Batch control, facts commit, metadata commit, execution settlement, and document creation now share one batch-scoped named writer lease.
- Added startup `BatchRecoveryService`: interrupted `extracting`/`metadata_running` items are not replayed; they are settled as `unknown_result`, leases are cleared, attempts are recorded when possible, and the batch is marked `needs_reconcile`.
- Added recovery fault-injection coverage: an interrupted extracting item is converted to `unknown_result + needs_reconcile`, its stale lease is cleared, and an unknown attempt record is persisted (`BatchTests 21/21`).
- Full regression on `2026-09-04`: main harness `113/113`, EditorContent `7/7`, Draft `26/26`, Batch `21/21`; Draft HTTP, Batch HTTP, Authoring-save, and Public-contract smoke all passed.
- Added `scripts/real-worker-worldbook-preflight.ps1`; it re-hashes the real AWAKE worldbook sample and records the configured-worker boundary without treating direct Ollama access as an AWAKE Worker run. Current result: sample hash verified, `WORLD_BOOK_LOCAL_WORKER_URL` and `WORLD_BOOK_LOCAL_WORKER_SECRET_ENV` unset, `worker_execution=not_attempted_worker_unconfigured`.
- New reliability evidence: Draft `26/26`; Batch `20/20`; Batch HTTP smoke passed after the shared writer lease change.
- Current loopback Draft smoke covers two-candidate generation, candidate-2 document readback, candidate fingerprint/evidence binding, and candidate-switch rejection.
- Current Release evidence: Draft `25/25`, Batch `20/20`, main harness `113/113`, EditorContent `7/7`, Draft HTTP smoke passed, Batch HTTP smoke passed, Authoring-save smoke passed, and Public-contract smoke passed.
- Real sample created at `tools/worldbook-studio/_tmp/r1-real-worldbook-authoring-sample.md`; sample SHA-256 is `8d5c664ff2e035eacdb02078a49910792c91b2c46c5970d07ed956b87dc5ba73` and references three current AWAKE rule files.
- Ollama is reachable on `127.0.0.1:11434`, but no AWAKE `/awake/handshake` endpoint is present and `WORLD_BOOK_LOCAL_WORKER_URL/SECRET` are not configured; real Worker validation remains unverified.

## Files changed in this paused slice

- `src/Awake.WorldbookStudio.Core/AuthoringLifecycleContracts.cs`
- `src/Awake.WorldbookStudio.Core/AuthoringDraftContracts.cs`
- `src/Awake.WorldbookStudio.Core/AuthoringDraftProviders.cs`
- `src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs`
- `src/Awake.WorldbookStudio.Core/AuthoringDraftDocumentBuilder.cs`
- `src/Awake.WorldbookStudio.Core/Application.cs`
- `src/Awake.WorldbookStudio.Web/AuthoringDraftEndpoints.cs`
- `src/Awake.WorldbookStudio.Web/AuthoringDraftStore.cs`
- `src/Awake.WorldbookStudio.Web/wwwroot/studio-draft.js`
- `src/Awake.WorldbookStudio.Core/BatchProviderService.cs`
- `src/Awake.WorldbookStudio.Core/BatchFactReviewRepository.cs`
- `src/Awake.WorldbookStudio.Core/BatchControlService.cs`
- `src/Awake.WorldbookStudio.Core/BatchDocumentService.cs`

## Next action

Next: complete the independent R1 re-review against the latest source, then add explicit late-result fence and internal attempt/result schema readback checks. Run the real AWAKE Worker sample only after its URL and secret are explicitly configured. Do not launch Bannerlord or sync game files.

## Final R1 closure — 2026-09-04

- Real Worker smoke passed with local Ollama `qwen2.5:latest` behind the formal `awake.worker.v1` `/awake/handshake` and `/awake/analyze` endpoints.
- Real source set: three current AWAKE rule files; all three source hashes and the authoring sample hash were recorded and verified.
- Real Worker result: 3 candidates; topic boundaries were kingdom founding/governance, Haldar succession crisis, and Guika/Shoulder family political suspicion; each candidate remained `pending`/review-only and passed evidence quote-hash and `profile.commoner` binding checks.
- No document was automatically created, published, compiled, or synced to the game directory.
- R1 implementation, local regression, package release-check, and real Worker protocol evidence are complete. Batch 6 (merge/split and review UX) remains intentionally not started per user direction.
