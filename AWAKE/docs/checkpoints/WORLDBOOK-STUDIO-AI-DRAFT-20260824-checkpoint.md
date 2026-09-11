# Worldbook Studio AI Draft Checkpoint

- task_id: WORLDBOOK-STUDIO-AI-DRAFT-20260824
- status: offline_verified
- updated: 2026-08-24
- scope: AI-assisted authoring workflow only; no Bannerlord runtime, reader, save/load or game-directory synchronization.

## Completed

- Added the user-facing workflow: reference material import/paste, objective-fact extraction, per-fact evidence review, metadata generation, identity-perspective expression generation, per-expression review, and handoff to the normal author form.
- AI results remain review-only. The final create operation writes an author draft with status=needs_review; it does not publish canon and does not auto-save the reference text as a source record.
- Cloud Provider and loopback Local Worker remain supported. The package is self-contained win-x64; .NET 10 is not a manual end-user prerequisite.
- Hardened the acceptance boundary: stored fact/expression structure and evidence cannot be forged by the client; every expression binds exactly one fact and at least one registered identity; invalid kind/layer/category values fail with clear WB-AI-DRAFT-422 feedback.
- Added draft-create idempotency so a retry returns the original author-document path instead of creating a duplicate.
- Updated the Chinese zero-base guide with the complete workflow and review rules.

## Verification

- Release build: 0 warnings / 0 errors.
- Studio harness: 101/101 PASS.
- AI draft tests: 9/9 PASS.
- Offline Web/Worker Smoke: pass; includes session bootstrap, facts, metadata, expressions, manual acceptance, author-document readback, needs_review, no source auto-save, forged evidence rejection and duplicate-create idempotency.
- Package release-check: pass. Launcher tests: 14 PASS.
- Final ZIP SHA-256: 1bc24d8666b8831cf065ab9b012db8616b7a5f136007ed8b3d660c472888f1e5.
- Package manifest: 612 files, selfContained=true, rid=win-x64.
- Evidence file: docs/evidence/WORLDBOOK-STUDIO-DRAFT-20260824-smoke-r6.json.

## Remaining Limits

- No real cloud request was made and no real Local Worker deployment was verified.
- No Bannerlord runtime test, game-directory sync, worldbook reader integration or save/load compatibility test was performed.
- Source quotations remain temporary AI-draft evidence; the final author document stores accepted text and review markers, not a complete source sidecar.
- The package is ready for a content-editor usability trial, not yet a runtime/game release.

## Next Step

- Give the package and 新手指引_世界书内容编辑者.md to a non-developer content editor, observe the reference-to-draft workflow, and record friction points before the next bounded improvement batch.
