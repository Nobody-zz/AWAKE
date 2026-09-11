# Plan Review Log: Marcus-Awake P3 Runtime Vertical

## Act 1 — Framed decision

- Final goal remains full internal Marcus-Awake migration, not just a fake provider or a renamed external dependency.
- P3A deliberately starts with an offline, deterministic service contract so the runtime call chain can be tested before introducing network, process, SQLite or game side effects.
- P3A's primary owner is the embedded Framework/Runtime contract; AWAKE gameplay and MCM remain out of scope until P3A/P3B boundaries are proven.
- The three materially different implementation choices were considered: (a) direct HTTP/SQLite in the game process, rejected for tick safety and secret/storage ownership; (b) a compatibility DLL that pretends to be old Marcus, rejected as a second authority and ABI trap; (c) a neutral internal runtime contract with a later local service, selected.

## Review status

- `plan_status`: `revised_for_review`
- `review_status`: `approved` (final independent review returned `APPROVED`; P3A implementation is authorized within the locked boundary)
- `user_signoff_required`: `already_granted_by_autonomous_migration_authorization`
- `primary_executor`: controller
- `minimum_evidence`: `P3A-E1` + `P3A-E2`
- `later_evidence_gate`: `P3/E2-real-runtime-service-smoke`
- `open_decisions`: exact SQLite provider/package binding and loopback process transport remain P3B/P3C decisions; they do not block the P3A neutral contract.


## Act 2 — First independent review

- Verdict: `REVISE` (read-only reviewer, 2026-08-26).
- Findings accepted: test doubles were not explicitly isolated; F-ID ownership and later-phase gates were incomplete; logical lifecycle was too easy to mistake for process management; task identity omitted campaign/timeline/idempotency/causation/settlement fields; stream terminal semantics were incomplete; permission/credential/egress and storage/RAG boundaries were not executable; budgets/diagnostics were underspecified; evidence levels mixed E1 and E2.
- Findings rejected: none.

## Act 3 — Revision applied

- The plan now contains an explicit production-vs-test write boundary, forbidden source/assembly scan, fixed task identity and validation order, complete neutral stream state machine, separate policy fakes, scoped keyword-only RAG contract, fixed resource limits, redacted diagnostics fields, F-ID→fixture→phase mapping, and split E1/E2 evidence.
- P3A remains offline-only and does not authorize changes to AWAKE.csproj, SubModule.xml, AWAKE runtime callers, game files, MCM or Worldbook Studio.
- Next gate: one independent read-only review of the revised plan.


## Act 4 — Review dispatch blocked

- The second independent reviewer could not start because the reviewer service returned `429 Too Many Requests`.
- Per execution rules, no retry is issued in this turn and no P3A code is authorized.
- P3A remains `revised_for_review`; only an independent `VERDICT: APPROVED` can unlock implementation.


## Act 5 — Second review dispatch blocked

- A fresh independent read-only review attempt again returned `429 Too Many Requests`.
- This is the second consecutive rate-limit failure; no P3A code was written and no approval was inferred.


## Act 6 — Second independent review

- Verdict: `REVISE` (read-only reviewer, 2026-08-26).
- Findings accepted: production/test fake conflict; E1/E2 wording conflict; non-canonical fixture aliases and missing F-020/F-022 rows; incomplete one-terminal receipt semantics; incomplete policy epoch/payload hash/decision consumption fields; incomplete worldbook package/archive/entry scope and legacy collection isolation; insufficient diagnostic forbidden-field assertions; imprecise evidence filenames/schema/order.
- Findings rejected: none.

## Act 7 — Second revision applied

- Production write set now explicitly removes P2 `InMemory*` doubles and relocates them to `tests/TestDoubles/FrameworkCoreDoubles.cs`; the release assembly must contain no in-memory test doubles.
- E1 is compile/parse/static evidence and E2 is deterministic execution evidence; the minimum is E1+E2 everywhere in the P3A plan and review state.
- The F-ID table now uses ownership-map canonical fixture names and includes applicable F-015 through F-037, including F-020/F-022 and explicit F-032 coverage, plus F-044, F-046–F-048 and F-057–F-058.
- Terminal, policy, storage scope, diagnostic redaction and exact evidence JSON requirements are now locked.
- Next gate: third independent read-only review.


## Act 8 — Third independent review

- Verdict: `REVISE` (read-only reviewer, 2026-08-26).
- Findings accepted: P1.5 phase-level `P3/E2` needed explicit separation from offline `P3A-E2`; task scope needed `message_id`, complete retry/receipt identity and non-durable fake wording; P2 test write set needed `FrameworkCoreVerticalSmoke.cs` and explicit exported-type scans; policy decision needed complete schema and canonicalization; storage needed canonical hash/fingerprint names and `rag_index_stale`; evidence needed schemas, runner entry and field-level redaction assertions; API major 2.0 needed an explicit migration decision; F-032 needed an explicit row.
- Findings rejected: none.

## Act 9 — Third revision applied

- P3A evidence is now qualified as `P3A-E1` and `P3A-E2`; the P1.5 integrated `P3/E2` requirement is explicitly deferred to the real Runtime Service phase.
- P2 in-memory doubles are explicitly scheduled for relocation to `tests/TestDoubles/FrameworkCoreDoubles.cs`; `FrameworkCoreVerticalSmoke.cs` is in the write set; release scans include exported type names and reject `InMemory*`.
- The complete task deduplication and receipt schema, terminal CAS race, egress decision schema/canonicalization/consumption, canonical RAG scope and stale-index code, diagnostic field policy, evidence schemas and explicit runner command are now fixed.
- `MarcusAwakeFramework.Api` major `2.0` is intentionally the new internal authority; old Marcus SDK `0.1` is not a compatibility target.
- Next gate: fourth independent read-only review.


## Act 10 — Fourth independent review

- Verdict: `REVISE` (read-only reviewer, 2026-08-26).
- Findings accepted: the canonical F-032 row was still absent; evidence schema path and executable runner were not fixed; governance fields drifted; non-effect dedupe lacked request hash/provider/profile and pre-quota lookup; RAG mode and collection canonicalization were not mapped to P1.5; egress consumption lacked atomic receipt binding/recovery; terminal wording mixed durable and in-memory semantics; raw-output diagnostic retention was not distinguished from player-visible redaction; F-ID statuses were not machine-enumerable.
- Findings rejected: none.

## Act 11 — Fourth revision applied

- P3A now qualifies `P3A-E1`/`P3A-E2` separately from P1.5 integrated `P3/E2`, adds the exact `Program.Main(args) → RuntimeVerticalSmoke.Run` runner contract and both schema paths, and synchronizes checkpoint fields.
- The plan adds F-032 `RuntimeEventBusFixture`, canonical machine-readable statuses, full request dedupe/receipt identity, in-memory CAS wording, complete egress schema and rollback rule, canonical RAG mapping/sorting/stale code, and per-field diagnostic assertions.
- API major `2.0` remains the intentional internal authority; old SDK `0.1` remains reference-only.
- Next gate: fifth independent read-only review.


## Act 12 — Fourth revision details

- The current plan now contains an explicit `RuntimeEventBusFixture` row, machine-enumerable F-ID statuses, a full request scope for both effect and non-effect tasks, pre-quota dedupe lookup, in-memory CAS wording, explicit egress transaction rollback/no-send behavior, P1.5 retrieval-mode mapping and set canonicalization, P3A raw-diagnostic metadata only, and exact evidence schema/runner/build commands.
- P3A evidence is named `P3A-E1` and `P3A-E2`; P1.5 integrated `P3/E2` remains a later real Runtime Service process/protocol gate.
- Current governance: `plan_status=revised_for_review`, `review_status=pending`, `minimum_evidence=P3A-E1 + P3A-E2`; awaiting final independent approval.


## Act 13 — Governance synchronization

- The P3A plan, checkpoint and review log now use `plan_status=revised_for_review`, `review_status=pending`, `primary_executor=controller`, `minimum_evidence=P3A-E1 + P3A-E2` and `later_evidence_gate=P3/E2-real-runtime-service-smoke`; P1.5 and the ownership map retain their intentionally different contract/inventory statuses as documented in the plan mapping.
- The P3A evidence schemas now have named required fields and deterministic field-level redaction assertion rules.
- The exact test runner is `MarcusAwakeFramework.Tests.exe --runtime-vertical`, dispatched by `Program.Main(args)` to `RuntimeVerticalSmoke.Run`; file presence alone is not evidence.
- Next gate: independent review of the current revision.

- Current revision write set explicitly includes `RequestContext.cs` for `AiTaskScope` and `TaskRequestCanonicalizer.cs` for shared task/egress fingerprints.

## Act 14 — Fifth independent review

- Verdict: `REVISE` (read-only reviewers, 2026-08-26).
- Findings accepted: E2 required an executable evidence writer/validator and explicit options; the vertical chain needed a host-owned neutral runtime port; E1 needed a checked-in API surface baseline and explicit CLR assembly version; canonicalization needed named golden vectors; provider wire DTOs needed a test-only boundary; F-042/F-043 needed an IPC handoff contract; and legacy Marcus absence/negative gates needed to be scheduled before the AWAKE caller migration.
- Findings accepted: P3A must not treat the existing in-memory `DurablyRecorded` IPC label as durable evidence. The production scan must also reject blocking waits and wall-clock reads in production code while permitting them only in the bounded test harness where explicitly required.
- Findings rejected: none.

## Act 15 — Fifth revision applied

- The P3A plan now fixes `EvidenceWriter`/`EvidenceSchemaValidator`, `--runtime-vertical --evidence-path`, `--verify-evidence`, the `Program.Main → RuntimeVerticalSmoke → RuntimeServiceComposition → FrameworkHost/SessionLease/RequestContext → RuntimeService` call chain, and the checked-in API baseline/version rules.
- The plan narrows Framework Core to neutral runtime, provider, storage and provenance contracts; provider wire DTOs remain test-only, while worldbook semantic fields remain AWAKE adapter data.
- The plan adds canonicalizer golden-vector acceptance, F-042/F-043 rows, and the no-I/O P3B IPC handoff document. Governance remains `plan_status=revised_for_review`, `review_status=pending`; P3A implementation is still locked until a new independent verdict returns `APPROVED`.

## Act 16 — Final independent approval

- Verdict: `APPROVED` (read-only reviewer, 2026-08-26).
- Approved boundary: implement only the P3A offline neutral contracts, host-owned runtime port, test-only fakes, deterministic vertical fixtures, canonicalizer vectors, evidence writer/validator and P3A-E1/P3A-E2 checks.
- Explicitly deferred: real Runtime Service process, Named Pipe/IPC, SQLite/FTS5, live Provider, credentials, MCM, AWAKE caller migration, Bannerlord execution, game-directory synchronization and E3/E4/E5 evidence.
- Governance transition: `plan_status=revised_for_review`, `review_status=approved`, `status=implementing`.

## Act 17 — P3A implementation and offline evidence

- Production `InMemory*` test doubles were removed from the Framework assembly and relocated to the test-only `TestDoubles` namespace.
- `RuntimeService` now owns the neutral logical lifecycle, session-bound task scope construction, idempotency conflict detection, resource budgets, bounded drain and task-handle accounting without introducing process, network, database or game dependencies.
- Task canonicalization now validates and canonicalizes nested JSON before composing the outer request; task and egress domains remain hash-isolated and are covered by 11 checked-in golden vectors.
- `P3A-E2`: `MarcusAwakeFramework.Tests.exe --runtime-vertical --evidence-path ...` returned `P3A-RUNTIME-SUMMARY PASS` (`7` scenarios, `11` vectors); the separate `--verify-evidence` command returned `P3A-E2-EVIDENCE PASS`.
- `P3A-E1`: `tools/verify_marcus_awake_p3a.ps1 -Mode Verify` returned `P3A-E1 PASS build=0 api=True forbidden=True`.
- Existing Framework Core regression remained green: `20/20` fixture cases and `8/8` contract groups.
- P3A highest evidence level is `E2`; real Runtime Service process/IPC, SQLite/FTS5, live Provider, credentials, MCM, AWAKE caller migration, game-directory synchronization and E3/E4/E5 remain deferred.
