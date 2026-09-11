# Worldbook Studio 批量作者 Revision 14 设计附录（草案）

- status: approved
- parent_behavior_design: `docs/superpowers/specs/2026-08-24-worldbook-studio-batch-authoring-design.md`
- parent_contract: Revision 13, SHA-256 `217E7538DC7C356D092ABC029A79E7504EE36BC1000B0B3FD3F7FF261FDCB4A9`
- current_plan: `docs/PLAN-WORLDBOOK-STUDIO-BATCH-POST-APPROVAL-WIRING-20260825.md`
- proposed_current_source: `docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json`
- proposed_current_package_path: `schemas/batch/awake.worldbook.batch-authoring-contracts-r14.json`

## Purpose

This addendum does not redesign the batch authoring workflow. It establishes the authority and release-state transition required after the Revision 13 post-approval test wiring is complete.

## Authority

- Revision 13 remains immutable historical authority for the pre-wiring contract and internal candidate evidence.
- Revision 14 becomes the current package authority only after its contract hash, mechanical checks, independent review and user signoff are complete.
- The R14 contract must explicitly identify itself as the current revision and set `test_entrypoints.wired_now=true`.
- The R14 contract must retain the R13 state machine, public projections, consent timing, cache formulas, no-canon boundary and no-expression/no-identity-binding rules.

## Release transition

`R13 internal candidate → approved R14 contract → current loader/package references → full test chain → release-check=0 → R14 internal candidate/release artifact`

The transition is not complete if only the JSON revision changes. The loader, contract checker, package source, release checker, test golden and package manifest must all resolve the same R14 contract.

## Compatibility boundary

- Existing R13 candidate files remain readable as historical evidence but are not silently promoted to the R14 current package.
- No Bannerlord runtime reader, save data, game directory, frozen AWAKE candidate or canonical worldbook is changed by this transition.
- A failed R14 transition must leave the existing R13 candidate untouched and must not create a ZIP named as a formal release.

## Acceptance

- R13 byte hash remains unchanged.
- R14 contract and this addendum agree on revision, authority, route registry and release wiring.
- `release-check` rejects a package whose contract reports `wired_now=false` and accepts the complete R14 package after all checks pass.
- Full batch behavior remains identical in the existing 15-route HTTP Smoke and all generated documents remain `needs_review` without expressions or identity/person/family bindings.

## Deferred

Metadata cache materialization hardening, finer Provider `unknown_result` classification, batch UI and Bannerlord/game-directory validation remain separate work and are not hidden inside the R14 release transition.
