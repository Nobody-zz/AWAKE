# Worldbook Studio Batch Authoring Checkpoint

- task_id: WORLDBOOK-STUDIO-BATCH-AUTHORING-20260824
- status: release_ready
- plan: docs/PLAN-WORLDBOOK-STUDIO-BATCH-POST-APPROVAL-WIRING-20260825.md
- design: docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-r14-design-addendum.md
- contract: docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json
- review_log: docs/PLAN-WORLDBOOK-STUDIO-BATCH-POST-APPROVAL-WIRING-20260825-REVIEW-LOG.md
- execution_lease: released
- lease_id: WBSA-R14-WIRING-20260825-01
- batch_id: WBSA-R14-WIRING-20260825-01
- owner: codex-main-20260825
- approved_contract_sha256: 7D07B08DA4CE5AB27DFE70F2F81BB098597A04FD170160612E09C32DA4540808
- approval: independent read-only review returned exact `VERDICT: APPROVED` for the R14 wiring plan and design addendum; implementation and release gates passed.
- lease_scope: R14 current-contract wiring, full offline verification and package/release checks only; no game startup, game-directory synchronization, frozen candidate changes or automatic canonical publication.
- lease_invalidated_by: contract hash/revision change, new design decision, external Provider 429/in-doubt result, user cancellation, game/runtime validation request, or repeated operation failure twice without new evidence.
- scope: completed offline R14 release wiring; package is a candidate for author handoff, not automatic canon publication.
- short_task: r14-release-wiring
- files_changed:
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Core/BatchAuthoringContractRegistry.cs
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Web/BatchEndpoints.cs
  - tools/worldbook-studio/scripts/batch-contract-check.ps1
  - tools/worldbook-studio/scripts/package.ps1
  - tools/worldbook-studio/scripts/release-check.ps1
  - tools/worldbook-studio/tests/Awake.WorldbookStudio.BatchTests/Program.cs
  - docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json
  - docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-r14-design-addendum.md
  - docs/PLAN-WORLDBOOK-STUDIO-BATCH-POST-APPROVAL-WIRING-20260825.md
  - docs/PLAN-WORLDBOOK-STUDIO-BATCH-POST-APPROVAL-WIRING-20260825-REVIEW-LOG.md
  - docs/checkpoints/WORLDBOOK-STUDIO-BATCH-AUTHORING-20260824-checkpoint.md
  - docs/AWAKE-CURRENT.md
- completed_short_task: r14-release-wiring
- additional_files_changed:
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Core/BatchScanRepository.cs
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Workspace.cs

## Historical Revision 13 repair state

- Revision 12 independent read-only review by Helmholtz returned VERDICT: REVISE with no P0 and seven P1 findings.
- Revision 13 independent read-only review by Copernicus returned VERDICT: REVISE with no P0 and two P1 findings; both were corrected.
- The next Tesla review returned VERDICT: REVISE with two P1 findings about batch-level target selection/consent aggregation and generic failure back-edge ambiguity; both were corrected.
- Hilbert then returned VERDICT: REVISE with one P0 consent timing conflict and one P1 missing timing assertion; both were corrected.
- Socrates then returned VERDICT: REVISE with one P0 stale consent phrase and one P1 generic/promotion journal vocabulary ambiguity; both are now corrected.
- Revision 13 remains immutable historical authority for the pre-wiring repair; Revision 14 is the current package authority across contract, design addendum and plan. The checker passes under both PowerShell engines, and the R14 review log records the current approval.
- Existing Worldbook Studio single-draft workflow and release package remain unchanged. Frozen AWAKE runtime candidates remain untouched.

## Historical Revision 13 changes

- Added durable allocation_id and allocation_pending recovery anchor for the batch_id-to-prepared-journal crash window.
- Added promotion claim-rebind CAS, claim_generation increment and old-fence rejection after owner expiry.
- Completed promotion journal terminal mapping to reservation state, HTTP status, error code, retry behavior and stable batch_id retention.
- Added public metadata result projection to item-detail so editors can inspect title, summary, main category and subcategory candidates.
- Contracted accepted → metadata_pending scheduling, facts versus facts_and_metadata consent semantics, metadata retry and commit behavior.
- Split metadata_pending (waiting, active=none) from metadata_running (in flight, active=metadata); added stage-aware retry-item requeue and consent/hash rechecks.
- Added consent authorized_item_ids, start target_item_ids, all-or-none target scheduling, per-item partial outcomes and stage-specific consent reauthorization.
- Unified metadata consent consumption: schedule CAS/journal commit first, consume consent second, Provider call third; result commit never consumes consent.
- Separated generic batch journal states from promotion_journal phase states and added negative stale-wording checks.
- Replaced internal report response with closed public report projection; raw last_error and open-ended report_summary are not public API fields.
- Updated the deterministic checker for Revision 13, metadata state/retry assertions, batch start/consent assertions and unique consent timing assertion.

## Verification

- Contract JSON parse: PASS.
- Stale Revision 11/12 reference scan: PASS.
- batch-contract-check.ps1: PASS (1130/1130) under PowerShell 7 and Windows PowerShell 5.1.
- Existing release-check.ps1: PASS.
- The R14 plan and design addendum are bound to the current Revision 14 contract hash; R13 remains byte-identical historical evidence.
- Independent review: Herschel returned exact `VERDICT: APPROVED` for the R14 wiring plan and design addendum, with P0=0 and P1=0.
- Short-task verification: offline restore succeeded; full solution Release build passed with 0 warnings/0 errors; BatchTests passed 5/5.
- BatchTests cover approved contract revision/hash and route binding, changed-hash rejection, safe relative paths, rooted/backslash/dot/empty segment rejection, extension allowlist, case collision and NFC collision.
- Prebatch verification: BatchTests passed 7/7; scan writes normalized UTF-8/LF/NFC snapshots and paragraph units under `prebatches/<scan_id>`, and a new repository instance reopens the persisted scan/unit data.
- Current implementation verification: Worldbook Studio harness `101/101`, BatchTests `13/13`, Draft tests `9/9`, Draft HTTP Smoke PASS, Batch HTTP Smoke PASS, and Release build `0 warnings / 0 errors`.
- Historical R13 release wiring verification intentionally failed with `WB-RELEASE-033` while R13 reported `test_entrypoints.wired_now=false`; the current R14 release check passes with `wired_now=true`.
- Candidate package verification: R13 internal candidate remains preserved with manifest/SHA256SUMS `613/613`; ZIP `WorldbookStudio-win-x64-internal-candidate.zip` SHA-256 is `eaaad0f02d0828e0068de70cb4e39850c322d60b19a1d36e2ce1b9e9ea905074`.
- R14 completion: R14 contract hash `7D07B08DA4CE5AB27DFE70F2F81BB098597A04FD170160612E09C32DA4540808`; `wired_now=true`; contract checker `1132/1132`; standalone `release-check` PASS.
- R14 package: `WorldbookStudio-win-x64.zip`, SHA-256 `a2b775e76e63298999b44e73fa1cb56552f99584fb8dd25db83b6caefd9e2ee9`, manifest/SHA256SUMS `613/613`; Launcher tests `14 PASS`.

## Known limitations

- R13 internal candidate remains historical; the current R14 package is release-check ready. Game/runtime and cloud/Worker evidence remain outside this batch.
- The R14 code-writing lease is released. No further code action is authorized by this checkpoint; a new feature or hardening batch requires its own plan and review.
- The previous checker had a Windows PowerShell `$Error` automatic-variable collision; the route loop variable is now `$routeError`, and a `claim_generation` assertion was added.
- No real cloud Provider or Worker deployment verification, no Bannerlord runtime evidence, and no game-directory synchronization.
- The release gate is wired to the complete test and contract-check chain; the batch UI is still a separate follow-up slice.
- Batch UI, deeper metadata-cache hardening, finer Provider `unknown_result` classification, and formal promotion/create reservation remain deferred follow-up work.

## Next action

Hand the R14 package and `新手指引_世界书内容编辑者.md` to the worldbook author for offline authoring trial. Do not modify the R14 package, publish canon, start Bannerlord or synchronize the game directory in this batch.

## Last error

- Resolved: new BatchTests initially lacked project assets; offline restore generated them. NFC collision test initially exposed global invariant globalization; Directory.Build.props now enables Windows Unicode normalization and the full build plus BatchTests pass.
- Resolved: metadata fact-set regression test used the current fixed lease timestamp; BatchTests now reaches the intended `WB-BATCH-FACT-SET-409` assertion.
## Post-change code-debt audit — R14

- scope: R14 changed source/scripts/tests plus one caller/binding level; generated `bin/`, `obj/`, `artifacts/` and historical R13 files excluded.
- denominator: 1,381 mechanical nonblank/noncomment lines across six audited source/script/test files.
- confirmed_removable_lines: 0 (0%); no orphaned R14 route, registry symbol or package entry was found.
- confirmed_duplicate_patterns: 1; schema reference lookup is repeated in `RequireSchemaByReference` and `TryGetSchema`, but extraction would change a contract-sensitive class and is deferred.
- suspected_findings: 3; repeated endpoint failure wrappers, repeated R14 fallback path literals across runtime/test/scripts, and bounded `JsonNode.DeepClone`/full manifest hash scans. None has evidence of incorrect behavior or measured hot-path impact.
- complexity_findings: 0 high-complexity blockers; the three suspected items are P2 maintainability/efficiency follow-ups only.
- efficiency_risks: 2 static, unmeasured; upload memory is capped at 64 MiB and contract/hash work runs at startup, request response validation, or release time rather than campaign/UI ticks.
- evidence: R14 contract check `1132/1132`, standalone `release-check` PASS, manifest/SHA256SUMS `613/613`, R13 hash unchanged, R14/package hashes matched.
- safe_action: do not refactor this release-ready candidate; create a separately reviewed hardening batch before consolidating helpers or changing contract path ownership.
- confidence: high for reachability, contract binding and release integrity; medium for static maintainability findings; no real cloud Provider, Worker, Bannerlord runtime, game-directory or save/load evidence claimed.
