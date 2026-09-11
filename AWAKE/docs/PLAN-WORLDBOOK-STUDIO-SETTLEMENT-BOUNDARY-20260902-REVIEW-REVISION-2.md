# Worldbook Studio settlement and boundary plan review revision 2

- Parent plan: `PLAN-WORLDBOOK-STUDIO-SETTLEMENT-BOUNDARY-20260902.md`
- Review target: `WORLDBOOK-STUDIO-SETTLEMENT-BOUNDARY-20260902`
- Revision: `2`
- Status: `REVISE_PENDING_REVIEW`

## Exact compile operation contract

The operation record is stored at `authoring-v1/operations/<operation_id>.json`:

```json
{
  "schema_version": "awake.worldbook.authoring-v1.compile-operation",
  "operation_id": "customer.compile.v1.<sha256>",
  "kind": "compile_settlement",
  "state": "prepared|committed|failed_recovery",
  "request_digest": "sha256",
  "compile_proof_id": "compile.*",
  "proof_hash": "sha256",
  "canonical_output_root": "relative workspace path",
  "confirmation_token_hash": "sha256 of token or empty string",
  "target": "relative compiled path",
  "manifest_hash": "sha256 or null while prepared",
  "result_path": "relative result envelope path",
  "result_hash": "sha256 or null while prepared",
  "error_code": "string or null",
  "created_at": "ISO-8601",
  "committed_at": "ISO-8601 or null"
}
```

The marker is stored at `authoring-v1/commit-markers/<operation_id>.json` and must contain the same `operation_id`, `kind`, `request_digest`, `compile_proof_id`, `proof_hash`, `target`, `manifest_hash`, `result_path`, and `result_hash`, plus `state=committed` and a self-hash. Recovery compares every binding and recomputes the marker self-hash; target manifest and result envelope hashes must match before a prepared record can become committed.

The result envelope is stored below `authoring-v1/operations/results/` and contains the serialized public compile response fields: `schema_version`, `operation_id`, `compile_proof_id`, `compiled_path`, `manifest_hash`, `files`, `validation`, `manifest`, and `result_hash`. It is immutable after commit. A replay loads this envelope and verifies both envelope hash and target manifest hash; it does not call `CompileExact` or `WriteCompiled`.

## Reservation and recovery

- The reservation path is `authoring-v1/operations/<operation_id>.reservation`.
- The winner obtains it with `FileMode.CreateNew`, writes `operation_id`, `request_digest`, `owner_instance_id`, and `created_at`, then atomically creates the prepared operation record.
- A second process seeing the reservation reads the operation record. If it is committed and digest-equal, it replays; if digest-different, it returns `WB-AUTHORITY-OPERATION-409`; if the record is absent or prepared, it returns a deterministic `WB-AUTHORITY-OPERATION-409` and does not compile.
- Reservations do not expire automatically. On startup, a prepared operation is either recovered only by the complete marker/manifest/result binding above or transitioned to `failed_recovery`; the reservation is retained as evidence.
- An orphan target produced before a crash is moved to `authoring-v1/quarantine/compile/<operation_id>/` using an atomic directory move when possible. If movement fails, the operation remains failed-closed and the target is never returned as a successful compiled path.

## Canonical input and compatibility

- Validate the requested output with `WorkspaceWritePolicy.RequireCompiled` before computing the digest or creating a prepared operation. Canonical output is the normalized absolute path converted back to a workspace-relative path with `/` separators.
- Null and empty confirmation token are equivalent and hash as the empty string. Non-empty token is never persisted in clear text.
- New IDs use NFC, length `1..128`, ASCII `[A-Za-z0-9._-]`, and reject reserved names and control/path characters. Existing records are loaded by exact original filename and checked for record-id/path consistency; they are never silently rewritten.

## Web and CLI contract

- Customer compile success returns `operation_id`, `operation_state=committed`, `compiled`, `manifestHash`, `files`, `validation`, and `manifest`.
- Customer compile conflict returns `operation_id`, `operation_state`, stable `error`, safe `message`, `correlation_id`, and `side_effect=none`.
- Operation query returns the exact public projection of the operation record and, for committed operations, the verified result envelope; internal paths and owner details are omitted.
- Web status mapping explicitly covers authority 400, 404, 409, 422, and 500 codes, including `WB-AUTHORITY-STAGING-404`. CLI uses the same code table and deterministic exit codes. Unknown exceptions are logged locally with correlation id and become a safe 500 response without raw exception text.

## Required fixtures

- First compile then same-request replay with byte-identical public result and unchanged target manifest.
- Same proof with changed output or token returns 409 and leaves a complete workspace snapshot unchanged.
- Two processes compete for the same reservation; only one calls compile/write and the loser replays or conflicts.
- Prepared operation with no marker, incomplete marker, forged marker self-hash, mismatched target, mismatched proof, and mismatched manifest each become `failed_recovery` and quarantine or block the orphan.
- Historical unsafe id reads by exact filename without silent rewrite; new unsafe id is rejected before any side effect.
- In-process HTTP and CLI tests assert code, status/exit code, response fields, correlation id, and absence of workspace paths/raw exception text.

Implementation remains blocked until this revision receives terminal review approval and explicit user sign-off.
