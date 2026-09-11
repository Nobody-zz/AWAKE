# Worldbook Studio public contract plan

- Batch: `WORLDBOOK-STUDIO-PUBLIC-CONTRACT-20260902`
- Parent: `WORLDBOOK-STUDIO-SAFEID-A-POSTPATCH-20260902`
- Risk: `high-risk`
- Objective: keep Web, CLI, and route behavior aligned while exposing only stable public data.

## Scope

- Audit authority success/error projections, Web routes, CLI exit codes, and retired-route behavior.
- Preserve internal operation ownership, fence tokens, absolute paths, and provider secrets.
- Keep current Draft/Batch domain behavior unchanged except where the public contract is visibly broken.

## Acceptance

- Every supported route reaches one authoritative service path and returns the documented casing/status shape.
- SafeId, authority, CAS, mutation-unknown, and retired-route failures map to stable public status/exit contracts.
- Success projections contain no owner, fence, token, absolute path, or internal storage identity.
- Web and CLI smoke tests cover valid, invalid, conflict, retired, and unknown-failure paths.

## Non-goals

- No Draft/Batch domain unification in this batch.
- No AI segmentation UX or workstation integration in this batch.
- No real Provider access, game launch, or game-directory synchronization.
