# Worldbook Studio settlement and boundary plan review revision

- Parent plan: `PLAN-WORLDBOOK-STUDIO-SETTLEMENT-BOUNDARY-20260902.md`
- Review target: `WORLDBOOK-STUDIO-SETTLEMENT-BOUNDARY-20260902`
- Revision: `1`
- Status: `REVISE_PENDING_REVIEW`

The independent challenge requires these additional contracts before implementation:

- Fixed server-owned operation id: `customer.compile.v1.<sha256(compile_proof_id)>`.
- Complete replayable result envelope; replay never recompiles.
- Cross-process atomic reservation; losers replay by digest or return `409`.
- Recovery validates operation, kind, digest, proof, canonical output, target, marker, and manifest bindings.
- Missing or mismatched bindings become `failed_recovery`; orphan compiled output is quarantined or marked non-consumable.
- Output is canonicalized only after workspace policy validation; invalid output fails before a prepared record is written.
- Strict id rules apply to new writes; historical records are read by exact filename and never silently rewritten.
- Operation responses and queries expose a fixed DTO with operation id, state, target, manifest hash, result, and error code.
- Web and CLI share the complete error and exit-code matrix; raw exception text never crosses the wire and responses include correlation ids.
- Tests cover full result replay, output/token conflicts, forged or incomplete markers, manifest mismatch, cross-process reservation, invalid ids, in-process HTTP responses, CLI parity, and operation-query recovery.

Implementation remains blocked until terminal review approval and explicit user sign-off.
