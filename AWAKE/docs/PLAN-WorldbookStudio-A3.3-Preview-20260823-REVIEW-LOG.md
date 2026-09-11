# Plan Review Log: Worldbook Studio A3.3 Preview projection seam
Act 1 (grill) complete — plan locked with autonomous execution authorization; independent read-only review pending.

## Review status

- Round 1 independent read-only review: `VERDICT: REVISE`.
- Required revisions: add executable wiring/deletion checks; expand the case matrix for rumor, empty rules, multiple rule/source/referral order, identity threshold/dimension behavior, no-visible-item and missing-text branches; separate snapshot/envelope/author/result diagnostics; record capture harness/input hashes and raw output provenance; make path/hash normalization and 90→93 manifest assertions self-contained.
- No A3.3 implementation files have been changed.

## Round 1 findings disposition

1. **Single reachable authority** — fixed by requiring exactly one `PreviewProjectionBuilder.Build` call and static absence of the old inline projection loop and helper names from `Application.cs`.
2. **Branch coverage** — fixed by expanding to seven legal/raw cases covering visible, rumor, deny/grant ordering, unknown/fallback, empty rules, identity dimensions/thresholds, invalid profile and missing text.
3. **Diagnostic separation** — fixed by requiring three separately recorded sequences: seam author diagnostics, snapshot report diagnostics, and final `PreviewResult.Diagnostics`.
4. **Golden provenance** — fixed by requiring capture harness SHA-256, complete input file/hash inventory, old application SHA-256, raw Envelope bytes/hash/base64 and capture metadata.
5. **Path/hash and manifest evidence** — fixed by embedding exact normalization rules and exact three-case-name/93-count/90-case-hash assertions in the plan.

- Round 2 independent read-only review: `VERDICT: REVISE`.
- Required revisions: correct missing-text behavior against `npc-preview.v1.schema.json`; state all nine cases explicitly; define exact Core scan counts for wiring/deletion; make the golden itself the fixed raw capture archive rather than relying on a temporary path.
6. **Schema boundary** — fixed by stating missing-text produces the original authoring snapshot Schema diagnostic while empty `npc_preview.text` remains allowed and produces no new Envelope Schema diagnostic.
7. **Case count** — fixed by requiring all nine named case inputs in the golden and tests.
8. **Authority scan/provenance** — fixed by exact source occurrence rules and `baseline_capture.archive_path` pointing to the versioned golden containing raw bytes and hashes.

## Final review

- Round 3 independent read-only review: `VERDICT: APPROVED`.
- The revised plan is accepted for implementation. The missing-text schema boundary, all nine characterization cases, exact wiring/deletion scans, fixed raw golden archive, and provenance requirements are sufficiently bounded.
- Implementation authorization: proceed with TDD; do not modify the AWAKE runtime candidate, game directory, dist, PlayerExports, or existing A1 package.
- Mechanical preflight correction: the source currently has 90 named cases; its 90-case manifest hash is `35468699f85151a83729e828bc792210e69c3489677deffe115f0160f01a089f`. The previously recorded `880ce9b5f90216094ede7b441063ad4bb8ec567afe3c6f931490e6e4bc78e5c4` is verified as the post-A3.2-removal 87-case hash and is retained as historical context.
- Characterization correction: the fixed base fixture grants `summary`, so its `rumor_commoner` case correctly captures `unknown` plus `layer=rumor`; `status=rumor` requires a separate grant whose `min_detail` is no higher than `rumor` and is not part of this unchanged baseline.
