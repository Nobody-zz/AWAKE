# Checkpoint: 世界事实采集最小批次

Status: `implemented_e3_pending_shared_smoke_and_game`

- User signed off on reviewed R2 plan.
- Implemented BuildId: `awake-20260912-world-fact-collectors-mvp-001`.
- Added a separate collector behaviour for war/peace, settlement ownership change, hero death, and hero prisoner release.
- The collector snapshots values on the campaign callback, queues only through the existing ledger, and never asks for storage permission. No current store logs an explicit unavailable result and does not enqueue.
- E2 build and focused smoke passed. SDK smoke has an unrelated parallel Persona golden-fixture failure; see E2 evidence.
- Game directory synchronized and hash-verified. Next action: user launches the game and provides logs for E4/E5; no launch was performed here.
