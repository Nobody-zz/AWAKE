# Marcus-Awake P3C Storage / RAG Checkpoint

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3C-STORAGE-RAG-20260827`
- `status`: `offline_verified`
- `updated`: `2026-08-27`
- `plan`: `docs/PLAN-MARCUS-AWAKE-P3C-STORAGE-RAG-20260827.md`
- `evidence`: `framework/MarcusAwakeRuntimeService/tests/_build_out/Release/MARCUS-AWAKE-P3C-evidence.json`

## Completed

- Transport v2 authenticated business frames reach the real Runtime Service.
- SQLite KV, timeline ledger, RAG ingest/search and access-scope filtering are
  wired through the P3B IPC boundary.
- Durable receipts use the same SQLite transaction as the business mutation.
- Same-scope payload conflicts are rejected; exact retries replay the committed
  response without repeating the mutation.
- Real child-process P3C fixture passes `7/7`, including service restart and
  crash-after-commit-before-response recovery.

## Evidence

- Transport, Framework, Storage, Runtime Service and P3C harness Release builds:
  `0 warnings / 0 errors`.
- `real_child_process=true` and `real_private_named_pipe=true`.
- `durable_receipt_verified=true`.
- `crash_after_commit_before_response_verified=true`.
- Highest current evidence level: offline `E2`.

## Not completed

- Provider HTTP, API key storage, model discovery, streaming and fallback.
- AWAKE caller/Host/session wiring, MCM, game directory synchronization.
- Bannerlord E4, save/load E5 and real cloud Provider verification.

## Next action

Create and review the P3D Provider/credential batch. Reuse the existing
authenticated IPC and durable task scope; do not create a second pipe or put
Provider HTTP in the game process.
