# Worldbook Studio boundary hardening plan

- Batch: `WORLDBOOK-STUDIO-BOUNDARY-HARDENING-20260902`
- Date: `2026-09-02`
- Risk: `high-risk`
- Parent audit: `WORLDBOOK-STUDIO-ARCHITECTURE-DEBT-AUDIT-20260901`
- Scope: HTTP error projection and unknown-exception containment only. Revision-1 is authoritative and supersedes the original SafeId/input-validation scope.

## Boundary

The existing authority service remains the authority. This batch does not change compile settlement, persistence, export, publish, or AI authoring behavior. It only makes malformed authority identifiers fail before filesystem access and makes authority HTTP failures safe and observable.

## Implementation decision

- Add one shared authority identifier validator for new caller-supplied IDs: NFC-normalized, 1..128 characters, ASCII `A-Z a-z 0-9 . _ -`, no path separators, colon, wildcard, control characters, trailing dot/space, or Windows reserved basename. Invalid input returns a stable `WB-AUTHORITY-400` before any read/write.
- Do not rewrite the existing `SafeId` mapping in this batch. Existing records remain readable through their current path mapping; new external operation/proof IDs are rejected unless valid. A later compatibility batch will define exact legacy record lookup and migration.
- Change `AuthorityFailure` to return only stable error code, safe public message, `side_effect=none`, and a generated correlation id. Raw exception messages remain local diagnostics only.
- Add explicit mapping for existing authority 404/409/422 codes, including `WB-AUTHORITY-STAGING-404`; unknown authority exceptions use a safe 500 response.
- Add a global web exception fallback so non-`InvalidOperationException` failures cannot emit framework/development details.

## Acceptance path

`authority route input -> RequireAuthorityId / error projection -> no filesystem side effect -> stable HTTP response with correlation id`

## Acceptance cases

- Valid existing operation/proof IDs continue to use the current routes.
- Empty, path-like, control-character, wildcard, colon, reserved-name, trailing-dot/space, overlong, and normalization-invalid IDs return `400` with no workspace changes.
- Authority responses never contain workspace root, absolute/relative internal paths, operation content, or raw exception text.
- Authority responses contain `ok=false`, stable `error`, safe `message`, `side_effect=none`, and non-empty `correlation_id`.
- `WB-AUTHORITY-STAGING-404` maps to HTTP 404; existing 409/422 mappings remain unchanged.
- A forced non-authority exception produces a safe 500 response without a developer exception page.
- Existing AuthorityGate, customer closure, HTTP smoke, and Studio harness remain green.

## Non-goals

- No compile operation journal, result replay, crash recovery, or cross-process locking.
- No Draft/Batch domain unification or segmentation UX.
- No change to existing legacy record path mapping, publish pointer semantics, package version, game directory, or real Provider access.

## Files

- Core: `src/Awake.WorldbookStudio.Core/AuthorityGate.cs` and a small validator if needed.
- Web: `src/Awake.WorldbookStudio.Web/Program.cs`.
- Tests: authority boundary tests, Web project tests, and existing closure fixtures.
- Docs: this plan, review state, and completion checkpoint.

## Verification

Write focused failing tests first. Then run authority tests, Web/HTTP tests, frontend customer closure, full Studio harness, Release build, package/release-check, and confirm no game directory/provider access.

Implementation remains blocked until terminal review approval and explicit user sign-off.
