# Plan Review Log: AWAKE Safe Packaging, Synchronization, and Candidate Re-freeze
Act 1 (grill) complete — the user approved the five-item scope on 2026-08-20; codebase evidence resolved the technical branches. MAX_ROUNDS=3.

## Act 1 Decisions
- Correct manifest-defined Persona paths and reject the nested duplicate.
- Preserve configuration, logs, exports and unknown game-only data.
- Use a narrow allowlist sync with explicit obsolete-path cleanup, not a destructive mirror.
- Keep public version `v0.2.0` and generate a new BuildId.
- Stop at E3/pending_game; E4/E5 require user gameplay and save/load evidence.
## Round 1 — Codex review
- Verdict: VERDICT: REVISE
- Findings: the plan needed an explicit game-sync authorization switch, a concrete runtime allowlist, canonical/reparse path checks, staging plus automatic rollback, precise nested-path deletion gates, PowerShell 5.1 tests, and stronger E3 evidence.

### Claude's response
- Revised the plan to require -ConfirmGameSync, an explicit allowlist, path/reparse validation, staged hashes, backup and automatic rollback, narrow obsolete-path cleanup, PowerShell 5.1 regression tests, and machine-readable E3 evidence. No implementation started before review convergence.

## Round 2 — Codex review
- Verdict: VERDICT: REVISE
- Findings: the plan needed a concrete final allowlist, an exact two-file cleanup list with refusal on unknown nested files, complete transaction/rollback states, zero-side-effect -WhatIf, and auditable E3 records.

### Claude's response
- Narrowed the allowlist to known runtime files and directories; limited cleanup to the two verified nested Persona files; required refusal on unknown nested files; specified backup/new/deleted/created-directory manifests, rollback re-verification, WhatIf side-effect rules, the sole authorization gate, and full E3 audit records.

## Round 3 — Codex review
- Verdict: VERDICT: REVISE
- Findings: directory globs were still not a provable allowlist; cleanup/transaction and E3 audit requirements needed explicit file lists and state fields.

### Claude's response
- Replaced runtime globs with a fixed file contract plus only manifest-declared runtime directories; made the two-file cleanup list exact; added transaction states and a full per-file E3 audit record contract. No implementation started before final review convergence.
