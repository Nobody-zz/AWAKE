# Plan Review Log: Persona Workbench Keyword-Constrained Compiler

Act 1 complete from the accumulated user grill and codebase inspection. The locked decisions are: Workbench first; one future shared compiler; a fixed protocol prompt plus registered keyword payload; structured trigger/scope/priority constraints; evidence-backed AI classification; no arbitrary Provider DSL; and no mutation of the frozen AWAKE candidate. MAX_ROUNDS=3.

## Round 1 — Codex

- Reviewer: `gpt-5.6-sol`, Codex CLI `0.147.0`, read-only.
- Verdict: `REVISE`.
- Material findings: the two existing schemas and generators were not reconciled; v1 persistence would reject new fields; `net10.0` could not be shared directly with AWAKE `net472`; registry and Provider indices could drift; the production conversion endpoint still used the Provider-owned full-document path; evidence matching was not contiguous; rule ordering, conflict, budget and byte-format semantics were incomplete; prompt-role availability was assumed; diagnostics lacked attribution/redaction; and the first batch could violate the frozen candidate boundary.
- Full reviewer output: `C:\Users\26811\AppData\Local\Temp\persona-workbench-keyword-plan-verdict-round1.txt`.

### Main-model response

- Accepted the findings and revised the plan.
- Selected `awake.persona.authoring.v2` with explicit Workbench-v1 migration and deferred AWAKE-v1 adaptation.
- Selected a dependency-free `netstandard2.0` canonical compiler under `tools/persona-workbench/**` for K1-K3, keeping root `src/**` untouched.
- Replaced numeric Provider indices with stable selector IDs plus registry version/digest.
- Defined exact-span evidence, immutable review candidates, explicit adoption, Provider capability profiles, byte serialization, budgets, rule ordering, precedence, redacted reports, preview/runtime APIs and MCM assessment.
- Split implementation into K1 contract/compiler, K2 UI/persistence, K3 Provider conversion and K4 AWAKE integration with a new BuildId.

## Round 2 — Codex

- Verdict: `REVISE`.
- Full reviewer output: `C:\Users\26811\AppData\Local\Temp\persona-workbench-keyword-plan-verdict-round2.txt`.
- Remaining findings focused on product decisions still left to implementation: exact v2 fields and v1 mapping, document registry pinning, value-to-token maps, dependency cycles, unequal-priority conflicts, counterweight arithmetic, rule grammar, meta-layer precedence, normative budgets, JSON ownership, net472 proof, Provider evidence offsets, age authority, exact starter selectors, exhaustive legacy Provider removal, no-auto-retry, report ownership, canonical-v2 AWAKE loading, binary integration, MCM rationale and per-batch approvals.

### Main-model response

- Narrowed authorization to K1 only; K2–K4 now require independent plans and approvals.
- Added the exact `awake.persona.authoring.v2` root/source/authored/fact/observation/rule contracts and exhaustive Workbench-v1 migration table.
- Required document-pinned registry/instruction versions and digests, immutable value-to-token tables, DAG validation and retained old registries.
- Locked rule scopes, strengths, priority range, conflict suppression, counterweight arithmetic and canonical rule grammar.
- Moved immutable protocol/constraints into a non-overridable meta-layer and added a normative section order and byte-budget table.
- Clarified that hosts deserialize JSON while pure Core validates DTOs, and added a tools-only net472 compatibility smoke.
- Replaced Provider offsets in the future roadmap with exact-quote local span resolution, defined manual age authority, enumerated K1 starter selectors, separated Core/Web reports, and chose a versioned module-local project-reference DLL for future K4.

## Round 3 — Codex

- Verdict: `REVISE`.
- Full reviewer output: `C:\Users\26811\AppData\Local\Temp\persona-workbench-keyword-plan-verdict-round3.txt`.
- Remaining blockers: the legacy tag/strength/duplicate-signal mapping is not exhaustive; `TemplateVersion` preservation is unspecified; nested v2 fields, registry digest bytes and compiler-mismatch behavior are not fully normative; the fixed instruction text/digest is missing; identity/appearance and most payload line grammars are incomplete; escaping, selector conflicts, rule response values, counterweight activation/effective strength, trigger co-occurrence, romance age-gate failure, budget classes/ranks, migration timestamps and exhaustive fixtures still require exact decisions.
- This is the configured `MAX_ROUNDS=3` limit. No approval verdict is inferred and no implementation code is authorized.

### Resolution status

- The plan is materially improved but remains a pre-implementation draft.
- K1–K4 are not being executed in this turn.
- Before code, the remaining normative items must be resolved in a new bounded plan/review cycle or explicitly accepted by the user as a reduced contract.
