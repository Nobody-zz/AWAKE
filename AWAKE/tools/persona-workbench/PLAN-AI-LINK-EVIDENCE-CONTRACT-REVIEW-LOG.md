# Plan Review Log: PersonaWorkbench AI 链路证据独立复算契约
Act 1 (grill) complete — user approved the evidence-hardening follow-up. MAX_ROUNDS=5.

## Round 1 — independent read-only review
Reviewer model: Lorentz (subagent; read-only)

Findings:
- Schema was only named, not formally fixed: required fields, types, enums, unknown fields and duplicate-key behavior were unspecified.
- The plan said six Workbench stages, while the existing runner executes four: `expand-short`, `convert-short`, `expand-long`, `convert-long`.
- The existing runner capture records the Workbench outer request, not the Provider body that contains the real system prompt and envelope.
- JSON Pointer paths for system/user messages and the meaning of `envelopeBytes` were not fixed.
- Baseline comparison had no baseline capture and no complete comparison/threshold rule.
- BuildId contains generation time and cannot be independently regenerated from source inputs; package-manifest canonical hashing was not explicit.
- Validator read-only semantics conflicted with capture deletion and process/port cleanup responsibility.
- Exit-code precedence for malformed inputs, evidence failures and cleanup failures was not fixed.

Decision: revise the plan before implementation.

Revisions applied:
- Fixed a formal schema artifact, strict fields/enums, duplicate-key rejection and four Workbench stage IDs.
- Added candidate and baseline capture paths plus explicit baseline comparison and latency threshold.
- Required a diagnostic-only Provider-client capture hook for actual UTF-8 request bytes and stage scope; outer Workbench requests are not evidence.
- Fixed message extraction, byte/hash rules, manifest reuse, BuildId metadata binding and cleanup ownership/exit-code precedence.

VERDICT: REVISE

## Round 2 — independent read-only review
Reviewer model: Lorentz (same read-only review session)

Findings:
- The four Workbench stage IDs now match the existing runner.
- Baseline authority remained incomplete: one candidate source/package set could not independently validate a different baseline, and exact-vs-threshold comparison rules were not closed.
- Provider body capture was named, but async stage propagation, scope cleanup, and missing/duplicate/concurrent capture behavior were not contractual.
- Validator read-only behavior and capture/process/port cleanup ownership and ordering were still mixed.

Decision: revise the plan before implementation.

Revisions applied:
- Added `WorkspaceRoot` plus independent candidate and baseline source/package/Worker/matrix/capture paths.
- Fixed exact identity comparisons, per-stage self-consistency checks and the `2x` duration threshold.
- Fixed protected loopback stage header, `AsyncLocal` scope, one-record-per-stage rule and capture writer lifecycle.
- Split pure-read validator from wrapper cleanup; validator runs before deletion and wrapper maps any cleanup failure to final exit code `1`.

VERDICT: REVISE

## Round 3 — independent read-only review
Reviewer model: Lorentz (same read-only review session)

Findings:
- Four stages, independent candidate/baseline inputs, AsyncLocal capture propagation, validator-to-wrapper cleanup ordering, and exit-code precedence were closed.
- One material contradiction remained: the plan required candidate and baseline to use the same BuildId while also giving them independent package/source inputs.

Decision: revise the BuildId wording before approval.

Revision applied:
- Candidate stages/Worker/report/evidence now share one candidate BuildId; baseline has an independent baseline BuildId. Validator checks each side internally and does not require the two BuildIds to match.

VERDICT: REVISE

## Round 4 — final independent read-only review
Reviewer model: Lorentz (same read-only review session)

The BuildId wording is now consistent: candidate artifacts share one candidate BuildId, baseline artifacts share an independent baseline BuildId, and the validator checks internal binding without requiring equality across builds.

VERDICT: APPROVED
