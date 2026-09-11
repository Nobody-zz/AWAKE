# Plan Review Log: AWAKE Persona Template System

Act 1 (grill) complete — plan locked with the user. MAX_ROUNDS=5.

Reviewer model: `gpt-5.6-sol` (configured model) — codex-cli `0.147.0`.

## External review — 2026-08-18

Reviewer: `gpt-5.6-sol`, read-only review via codex-cli `0.147.0`.

Verdict: `REVISE`

Key findings accepted:
- Persona schemas were not registered in `AwakeStorageContract`/`WorldStateStore`, with no concrete key, size, migration, authority, or unknown-field contract.
- Save-completed lifecycle was not proven; `SyncData` and asynchronous final drains cannot support a claimed `save_committed` state.
- Existing hero-scoped keys cannot support timeline branches without branch-scoping every relevant projection.
- `AcceptedSequence` must be a branch commit-group sequence with independent projection watermarks, not an untyped global comparison.
- Current transcript, command settlement, and memory close are independent asynchronous operations; the existing three-second wait is not atomic or recoverable.
- The scope was split into a prompt-visible deterministic core, safe legacy migration/overrides, and a deferred save/timeline foundation.

Plan revision:
- Batch A/B are now the implementation target.
- Batch C is deferred until Bannerlord v1.3.15 save lifecycle and Marcus Storage capabilities are verified.
- No implementation may claim save completion, timeline isolation, or atomic commit groups before those contracts are proven.

## Short-task checkpoint — 2026-08-18

Task: `persona-save-lifecycle-static-audit`

Completed:
- Checked the AWAKE source and Marcus SDK reference for save-completed callbacks, durable storage markers, timeline/branch APIs, and transaction/recovery contracts.
- Confirmed existing runtime evidence is limited to Bannerlord `SyncData`, session lifecycle handling, storage writes, durable event publication, and existing interaction recovery indexes.
- No verified post-save callback, branch-scoped storage key contract, durable commit-group journal, or cross-projection acceptance watermark is available to implement safely.

Result: `paused` / `deferred`

Next action:
- Do not add `save_committed`, timeline branching, or atomic commit groups yet.
- Continue only after a concrete v1.3.15 lifecycle/API source or an explicit pre-save-anchor fallback contract is available.

Verification:
- Persona core builds and SDK smoke already passed in the prior checkpoint.
- This short task was read-only; no runtime source was changed.

## Short-task checkpoint — 2026-08-18

Task: `persona-persistence-contract-and-recovery-smoke`

Completed:
- Added branch-aware timeline identity, per-projection watermarks, persistence envelope, and recovery record contracts.
- Added validation that rejects watermark-ahead data, invalid root fork sequences, unknown recovery states, and `save_committed` without a confirmed save anchor.
- Added SDK Smoke coverage for projection acceptance and recovery status transitions.

Files:
- `_houkai_merge/AWAKE/src/PersonaPersistenceModels.cs`
- `_houkai_merge/AWAKE.Tests/Program.cs`
- `_houkai_merge/AWAKE.Tests/AWAKE.Tests.csproj`

Verification:
- `AWAKE.Tests` Release build: 0 warnings, 0 errors.
- `PASS persona persistence smoke`.
- `PASS ALL Awake.SdkSmoke`.
- AWAKE `1.3.15` Release build: 0 warnings, 0 errors.

Limit:
- Contracts are not registered in `AwakeStorageContract` or wired to Bannerlord save callbacks yet. This is intentional until the save lifecycle is proven.

Next action:
- Review whether these contracts can be mapped to the existing `WorldStateStore` without claiming save completion; otherwise keep them as a deferred schema/test foundation.

## Short-task checkpoint — 2026-08-18

Task: `persona-branch-isolation-key`

Completed:
- Added `PersonaStorageKey` as a pure branch-aware key builder.
- Key includes escaped `campaignId`, `timelineId`, `branchId`, and `characterId`; missing identity fields are rejected.
- Added Smoke coverage proving the branch ID is present in the generated key.

Verification:
- `AWAKE.Tests` Release build: 0 warnings, 0 errors.
- `PASS persona persistence smoke`.
- `PASS ALL Awake.SdkSmoke`.
- AWAKE `1.3.15` Release build: 0 warnings, 0 errors.

Limit:
- The key builder is not wired to `WorldStateStore`; no save callback or atomic commit behavior is claimed.

Next action:
- Evaluate a read-only adapter around the existing storage API, or pause if wiring would imply unproven save semantics.

## Short-task checkpoint — 2026-08-18

Task: `persona-safe-batch-release-verification`

Completed:
- Built AWAKE Release for Bannerlord APIs `1.3.15` and `1.4.8` with 0 warnings and 0 errors.
- Re-synced the current `1.3.15` DLL plus Persona worldbook manifest/definitions to `dist` and the installed game module while Bannerlord was not running.
- Verified release package hashes, localization, and asset boundary checks.

Evidence:
- `PASS persona persistence smoke`.
- `PASS ALL Awake.SdkSmoke`.
- `RELEASE_CHECK_OK`.
- Persona safe batch release state is ready for game-log validation.

Deferred by evidence, not omitted:
- No reliable save-completed callback or durable multi-projection commit primitive was found.
- `save_committed`, automatic timeline branch switching, and atomic commit groups remain deferred until that lifecycle/API evidence exists.

Game validation next action:
- Start a new/known campaign, open an NPC dialogue, confirm the log records Persona generation and that the generated DSL affects the prompt without breaking fallback dialogue; then provide the AWAKE log for review.
