# Plan Review Log: AWAKE Worldbook Hardening

## Round 1 — Local Codex read-only review

- Reviewer: local `codex-cli 0.147.0`, sandbox `read-only`
- Plan: `docs/PLAN-AWAKE-WorldbookHardening-20260822-DRAFT.md`
- Scope: current AWAKE source, nested AGENTS, Contract v1, Studio compiler/tests, Runtime loader/query, identity evaluator and event ledger.
- Result: no files modified, Bannerlord not started, game directory not synchronized.

### Findings

- **P0:** deny is evaluated per expression; a deny in one expression can be bypassed by a grant in another expression. Fix with an entry-wide deny phase before grant selection and terminal blocked behavior.
- **P0:** `effective_detail`, requested detail, Hero fallback and typed knowledge capability inputs are unresolved. Lock the request/effective-capability contract and unknown-versus-zero semantics before code.
- **P0:** content gate scope and non-disclosure are unspecified. Define query/entry/expression gate precedence; blocked output must contain no text, title, summary, referral or rule-derived metadata.
- **P0:** Runtime package loading does not yet verify declared hashes at the activation boundary. Define canonical bytes, verify manifest/content/package hashes before constructing the live service, and fail closed.
- **P0:** event uniqueness requires source/sequence propagation through producers, storage, reload and replay; changing only the ID formula is insufficient. Add concurrent duplicate and reload/replay tests.
- **P1:** referral candidates are emitted before final state and without public-target, deny or content-gate validation.
- **P1:** partial behavior for multiple expressions and byte-budget truncation is undefined.
- **P1:** condition namespaces need canonical conversion, normalization and no ambiguous suffix matching.
- **P1:** Contract/runtime field flow and schema authority need an explicit parity test.
- **P1:** Hero/social identity precedence and unknown identity behavior are not locked; unknown must not become commoner or noble.
- **P1:** missing skill capability must be distinguished from numeric skill value zero and fail closed.
- **P1:** the regression list needs adversarial combinations and zero-leak assertions, not only one test per work package.
- **P1:** checkpoint/candidate evidence needs exact contract versions, fixture IDs, canonical hash inputs and frozen-candidate before/after proof.
- **P2:** blocked reason classes, event retention/order, referral cycles/self-reference, TOCTOU hashing, exit codes and artifact provenance require explicit requirements.

VERDICT: REVISE

## Round 2 — Local Codex read-only re-review

- Same read-only Codex session resumed after the plan revision.
- Most Round 1 P0 findings were accepted as closed by the revised plan.

### Remaining findings

- **P1:** The revised identity precedence contradicts the existing Contract v1 matrix. The plan must either amend the contract explicitly or conform to `explicit_identity → office → skill → age → culture → kingdom → settlement → base_identity`, with a conflict fixture.
- **P1:** The plan still does not state that `contentHash` is the hash of the canonical path→canonical JSON object index used by the current `ContractHashing.ContentHash`, rather than raw runtime/index bytes. The compiler and runtime must share exact vectors.
- **P1:** The bounded Bannerlord adapter needed to convert game bare IDs into canonical runtime IDs is not explicitly in scope.
- **P2:** The checkpoint still lists the revised semantic decisions as open limitations and must be updated before implementation approval.

VERDICT: REVISE

## Round 3 — Local Codex read-only re-review

- Same read-only Codex session resumed after Round 2 revisions.
- Verified identity precedence against `permission-matrix.json`.
- Verified `contentHash` definition against the current `ContractHashing.ContentHash` implementation.
- Verified bounded `BannerlordWorldbookIdentityAdapter` is explicitly in scope while larger external adapters remain out of scope.
- No files modified, Bannerlord not started, game directory not synchronized.

### Result

- No remaining material blocker identified.
- Stale checkpoint limitations are implementation deliverables, not plan contradictions.

VERDICT: APPROVED
