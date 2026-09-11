# Marcus-Awake P3 Runtime Vertical checkpoint

- `task_id`: `MARCUS-AWAKE-P3-RUNTIME-VERTICAL-20260826`
- `parent_task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3-RUNTIME-VERTICAL-20260826`
- `status`: `offline_verified` (P3A implementation and offline evidence are complete; later P3B/P3C/P3D boundaries remain open)
- `plan`: `docs/PLAN-MARCUS-AWAKE-P3-RUNTIME-VERTICAL-20260826.md`
- `plan_status`: `revised_for_review`
- `review_status`: `approved`
- `user_signoff_required`: `already_granted_by_autonomous_migration_authorization`
- `primary_executor`: `controller`
- `minimum_evidence`: `P3A-E1 + P3A-E2`
- `later_evidence_gate`: `P3/E2-real-runtime-service-smoke`

## files_changed

- P2 Framework Core remains E1 offline-verified.
- Production `InMemory*` doubles were removed from Framework and moved to `tests/TestDoubles/`; `RuntimeService`, task scope/idempotency fields, canonicalizer input validation and diagnostic redaction are implemented.
- P3A evidence writer/validator, Runtime Vertical composition, deterministic task/provider/prompt/storage/RAG/egress fixtures and 11 canonicalizer vectors are checked into the test assembly.

## verification

- P2 contract anchor remains SHA-256 `DB45A9C4C130BC727394B61AFD9836508045F83B66BA7F23C0BF3686D596D619`.
- P3A current revision received `VERDICT: APPROVED` from an independent read-only review.
- Framework Release build: `0 warnings / 0 errors`; Framework Core: `20/20` cases; default contract tests: `8/8` groups.
- P3A Runtime Vertical: `7/7` scenarios; canonicalizer: `11` vectors; E2 evidence generation and independent validation: `PASS`.
- P3A-E1 static verification: `PASS` (`build=0`, `api=True`, `forbidden=True`).
- Evidence files: `docs/evidence/MARCUS-AWAKE-P3A-E1-20260826.json`, `docs/evidence/MARCUS-AWAKE-P3A-E2-20260826.json`, `docs/evidence/MARCUS-AWAKE-P3-API-SURFACE-BASELINE-20260826.json`.
- No real Runtime Service, Named Pipe/IPC, SQLite/FTS5, live Provider, credentials, MCM, AWAKE caller, game or package evidence is claimed.

## known_limitations

- AWAKE still compiles against and loads the external `MarcusAIFramework`; switching that dependency is deferred to the later caller-migration batch.
- The P3A memory backends are test-only doubles, not SQLite, HTTP or local Worker implementations.
- The default clock fallback is deterministic `DateTimeOffset.MinValue`; real wall-clock ownership remains a later Runtime Service boundary.

## next_action

- Create and independently review the P3B real local Runtime Service/IPC batch; do not modify AWAKE.csproj, SubModule.xml, AWAKE callers, frozen candidates or the game directory before that gate.

## last_error

- None. Previous P2 transport failures remain historical and are not a P3 verdict.

- last_error: independent review dispatch returned `429 Too Many Requests`; no P3A source code was written.

- review_dispatch_attempt_2: `429 Too Many Requests`; P3A code remains unauthorized.

- latest_review: `REVISE`; fake isolation, E1/E2 contradiction, canonical fixture names, terminal receipt semantics, policy decision fields, storage worldbook scope, diagnostic field bans and exact evidence schema were revised.

- latest_review: `REVISE`; P3A/P3 evidence naming, full task/receipt scope, fake relocation write set, canonical policy and RAG fields, evidence schemas/runner and API 2.0 decision were revised.

- latest_review: `REVISE`; P3A/P3 evidence naming, complete task/receipt scope, fake relocation, canonical policy/RAG rules, evidence runner/schema, F-032, status synchronization and API version decision were revised.

- latest_review: `REVISE`; E2 evidence writing/validation, unique Host-to-runtime call chain, checked-in API surface baseline, canonicalizer golden vectors, F-042/F-043 IPC handoff, and legacy Marcus absence gates were added; P3A code remains unauthorized.

- latest_review: `APPROVED`; P3A implementation is authorized only within the offline neutral contract/test vertical slice. Real IPC, SQLite, Provider, MCM, AWAKE caller, Bannerlord and E3-E5 remain deferred.
- final_evidence: `P3A-E1=PASS`, `P3A-E2=PASS`, highest evidence level `E2`.
