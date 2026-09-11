# Worldbook Studio boundary hardening checkpoint

- Batch: `WORLDBOOK-STUDIO-BOUNDARY-HARDENING-20260902`
- Completed: `2026-09-02`
- Scope: Web HTTP authority error projection and unknown-exception containment only.
- User sign-off: recorded in `docs/review-state/WORLDBOOK-STUDIO-BOUNDARY-HARDENING-20260902.json`.

## Implemented

- Added `AuthorityHttpErrorProjection` with explicit authority status mapping and fixed safe messages.
- Added `Preflight`, `MutationUnknown`, and `RetiredRoute` failure contexts.
- Added correlation ID generation, response header/body parity, local diagnostic logging, and response-started containment.
- Replaced the retired-route reduced response with the shared `410` error contract.
- Marked authority mutation entry points so post-entry failures report `side_effect=unknown`.
- Added deterministic unit/fixture coverage for status mapping, safe messages, correlation parity, unknown exceptions, and response-started behavior.

## Evidence

- Web Release build: `0 warnings / 0 errors`.
- Worldbook Studio harness: `113/113`.
- AuthorityGate suite: `3/3`.
- Customer closure route harness: passed.
- `scripts/test.ps1 -Suite All`: passed, including editor, Draft, Batch, save, and HTTP smoke suites.
- Package/release check: passed.
- Launcher tests: `14` passed.
- Launcher smoke: clean start, browser failure, stale settings recovery, duplicate launch, and graceful shutdown passed.
- No Bannerlord process was started, no game directory was synchronized, and no real Provider/API key was used.

## Deferred risks

- Compile settlement remains non-idempotent across response loss and requires a new high-risk batch.
- SafeId historical compatibility remains unresolved and requires a separate compatibility batch.
- Successful response public projection and route registry reconciliation remain separate work.
- Draft/Batch unification and explainable AI segmentation remain separate authoring-domain work.

## Delivery note

The current test package was rebuilt after the change. This checkpoint does not claim in-game runtime verification; that evidence remains user-run.
