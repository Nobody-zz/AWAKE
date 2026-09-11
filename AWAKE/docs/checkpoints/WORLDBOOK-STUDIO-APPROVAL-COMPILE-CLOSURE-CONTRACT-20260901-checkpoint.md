# Worldbook Studio Approval → Compile Closure Contract Checkpoint

- task_id: `WORLDBOOK-STUDIO-APPROVAL-COMPILE-CLOSURE-CONTRACT-20260901`
- batch_id: `WORLDBOOK-STUDIO-APPROVAL-COMPILE-CLOSURE-CONTRACT-20260901`
- status: `completed_with_follow_up`
- review_state: `approved`
- user_signoff: `true`
- last_error: `null`
- game_directory_touched: `false`
- provider_or_api_key_used: `false`

## Implementation

- Core now settles customer compile only through `CompileProof -> CompileApproved -> CompileResult/compiled path`.
- Web and CLI customer compile/export routes require proof-backed wire fields and reject legacy/unknown fields before side effects.
- Approval, selection, document revision/content hash, registry/reference closure, content tier, and operation digest are validated and replay/conflict semantics are fixed.
- Frontend clears stale proof on open/save/edit/conflict and sends only `compile_proof_id` plus an optional confirmation token for compile/export.
- Removed a duplicate `authorizeCompileButton` DOM id from `src/Awake.WorldbookStudio.Web/wwwroot/index.html:37` and added a regression assertion in `tests/frontend/customer-closure.test.js:16`.

## Verification

- AuthorityGate: `PASS 3/3`, including HTTP route matrix/zero-side-effect, restart recovery, and compile-proof/staging pointer CAS.
- Main Worldbook Studio harness: `108/108 PASS`.
- Editor content: `7/7 PASS`.
- Batch: `18/18 PASS`.
- Draft: `20/20 PASS`.
- Launcher: `14 PASS` with `AWAKE_WB_TEST_PACKAGE` set to the current test package.
- Draft, authoring-save, and batch HTTP smoke: all passed; loopback-only; no game directory access.
- Customer closure frontend harness: passed after the duplicate-id fix.
- Web Release build: passed with `0 warnings / 0 errors`.
- Node runtime used for frontend checks: `C:\Users\26811\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe`.

## Post-change audit

- Scope: changed Core/Web/CLI/frontend files, their direct callers/bindings, and the customer closure test.
- Confirmed finding fixed: duplicate compile-authorization DOM id.
- Confirmed orphaned new code: none.
- Suspected historical debt left untouched: frontend draft/batch segmentation and review ergonomics remain broader UX work.
- No unrelated cleanup or authority-path expansion performed.

## User journey review

Reviewed journey: open worldbook → provide reference material → AI extraction → fact/metadata/expression review → edit/accept/reject → save → explicit authorization → compile/export.

### P0/P1 for a follow-up UX batch

- Segmentation explainability: each generated fact should show the source span and why it is a separate unit; provide merge/split or “keep as one fact” correction without re-running the whole generation.
- Review workload: distinguish low-risk directly evidenced facts from inferred/high-risk items; allow safe grouped acceptance while preserving per-item evidence and independent rejection.
- Recovery clarity: preserve an interrupted draft and user edits; make unknown save results, stale revisions, provider failures, and expired CompileProof show one concrete next action each.
- Long-source handling: avoid forcing users into arbitrary manual batches; use heading/paragraph-aware units and show progress plus remaining scope.

### P2/P3

- P2: persistent proof status panel with states `未授权/已登记/已选择/已批准/可编译/已失效`, rather than relying mainly on toast/status text.
- P2: source-highlight navigation from a fact card to the exact quote and neighboring context.
- P3: review filters, keyboard shortcuts, and compact “only unresolved” mode.

These UX findings do not block the current authority closure and must be planned as a separate batch before implementation.

## Next action

Create a separate UX batch for AI knowledge-entry segmentation and review reduction; do not reopen this approved authority contract unless a new boundary defect is found.
