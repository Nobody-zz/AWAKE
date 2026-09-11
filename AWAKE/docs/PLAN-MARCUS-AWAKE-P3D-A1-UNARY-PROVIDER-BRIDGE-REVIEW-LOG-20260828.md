# Review Log: Marcus-Awake P3D-A1 Unary Provider Bridge

- `batch_id`: `MARCUS-AWAKE-P3D-A1-UNARY-PROVIDER-BRIDGE-20260828`
- `plan_revision`: `6`
- `review_status`: `pending_independent_read_only_review`
- `implementation_authority`: closed until exact `VERDICT: APPROVED`

## Review protocol

The independent reviewer must read only the AWAKE project rules, current state, A0 checkpoint, this plan, and the declared source files. It must not modify files, start Bannerlord, access a real cloud Provider, or infer approval from prior batches.

Required challenge areas:

- whether A1 keeps the full migration goal instead of shrinking it to a fake-only feature;
- whether Service ownership of HTTP and Framework ownership of typed results are unambiguous;
- whether profile/session/owner isolation and credential redaction are executable;
- whether the three Provider adapters are reused without an accidental second implementation;
- whether deadline, cancellation, duplicate replay, bounded responses and service shutdown are covered;
- whether the result shapes are sufficient for later MCM and AWAKE callers without prematurely coupling them;
- whether the write set accidentally touches frozen runtime candidates or external dependency declarations;
- whether E1/E2 evidence can distinguish real Service/IPC/HTTP behavior from unit-test-only behavior.

## Verdict records

### Review 1 — revision 1

- result: `REVISE`
- source: previous independent read-only challenge recorded during task handoff
- findings:
  - Framework business entry remained optional: `SupportsAiBusinessFrames` was false and `SubmitAsync` still returned `runtime.ai_business_unavailable`.
  - Runtime Service did not reference or own the existing Provider assembly and credential store; profile/credential initialization and test cleanup were unspecified.
  - `ProviderConnectionProfile` rejected non-Ollama profiles without credentials even when `is_cloud=false`.
  - Cancellation, duplicate payload conflict, Provider-completed/IPC-write-failure recovery and real child-process evidence were not executable enough.
- disposition: all findings were incorporated into revision 2; the implementation gate remains closed until a fresh independent review returns the exact approval line.

### Revision 2 review request

- required verdict: exact `VERDICT: APPROVED` or `VERDICT: REVISE`
- review scope: revision 2 plan plus the minimum declared Framework, Transport, Provider and Runtime Service source files
- implementation authority: closed until approval
- non-goals for reviewer: no file edits, no Bannerlord launch, no real cloud Provider call, no approval inferred from A0 evidence

### Review 2 — revision 2

- result: `REVISE`
- source: Schrodinger, independent read-only challenge
- findings:
  - P0: Framework entry still lacked exact `ProviderRuntimeApi` methods/DTOs, serialization, error mapping and `AiTaskHandle` settlement; the current client only had health transport.
  - P0: Service-owned Provider reference, factory/registry, credential-store lifecycle, HTTP invoker ownership and cleanup were not locked to a buildable implementation.
  - P1: local/cloud profile validation order and loopback URL policy were not fixed; `is_cloud=false` was not independent from credential validation.
  - P1: cancellation precedence, concurrent idempotency behavior and Provider-complete-before-IPC-write ledger semantics remained nondeterministic.
  - P1: success/error payload closure and verifier/evidence bindings were still example-level.
- disposition: revision 3 adds exact Framework methods and payload bounds, event mapping, Service ownership and disposal, local/cloud URL and credential rules, a deterministic idempotency state machine, and a schema/verifier with real child-process bindings. A fresh independent review is required; implementation remains closed.

### Revision 3 review request

- required verdict: exact `VERDICT: APPROVED` or `VERDICT: REVISE`
- review scope: revision 3 plan, review log, AWAKE project rules, A0 checkpoint and only the declared Framework/Transport/Provider/Runtime Service files
- implementation authority: closed until approval

### Review 3 — revision 3

- result: `REVISE`
- source: Lovelace, independent read-only challenge
- findings:
  - Framework typed contract still lacked exact return DTOs, field types, and the `AiTaskHandle` cancellation-to-IPC behavior.
  - The plan allowed an omitted Ollama `is_cloud` value while the current wire validator requires the field.
  - Cancellation and response-write races lacked a unique Service linearization point.
  - E1/E2 evidence fields, required types, exact command/exit bindings and test-project wiring were not fixed.
- disposition: revision 4 fixes the public DTO/signature set, makes `is_cloud` explicitly required on wire, defines the dedicated authenticated cancel-control connection and cancellation linearization, and locks the evidence schema minimum and verifier bindings. A fresh independent review remains required.

### Revision 4 review request

- required verdict: exact `VERDICT: APPROVED` or `VERDICT: REVISE`
- review scope: revision 4 plan, this log, AWAKE project rules, A0 checkpoint and only the declared source/project files
- implementation authority: closed until approval

### Review 4 — revision 4

- result: `REVISE`
- source: Schrodinger, independent read-only challenge
- findings:
  - P0: Framework completion input/output/error mapping remained underspecified, and the plan contained a stale `ProviderRuntimeClient` name.
  - P0: the proposed `cancel_control` role required a new handshake field absent from the locked Transport write set.
  - P0: idempotency scope still conflicted with the existing `MessageId`-inclusive key and did not define cross-connection replay after IPC write failure.
  - P1: Provider/HTTP/credential ownership lacked fixed constructors and shutdown order.
  - P1: AiTaskHandle field-level mapping and evidence observations were not sufficient to prove the behavior.
- disposition: revision 5 removes the new handshake role, uses an authenticated derived-session cancel connection over the existing wire contract, fixes the Framework mapping and error table, defines the registry ownership contract, linearizes task states, and expands the evidence schema fields and bindings. A fresh independent review remains required.

### Revision 5 review request

- required verdict: exact `VERDICT: APPROVED` or `VERDICT: REVISE`
- review scope: revision 5 plan, this log, AWAKE project rules, A0 checkpoint and the declared source/project files
- implementation authority: closed until approval

### Review 5 — revision 5

- result: `REVISE`
- source: Copernicus, independent read-only challenge
- finding: the reviewer was interrupted before reading the complete minimum source set; exact DTO/signature, cancellation authorization, state-machine, disposal, local/cloud policy and evidence-binding readiness therefore remain unverified.
- disposition: procedural only; no new implementation defect was asserted. A complete fresh read-only review of the unchanged revision 5 plan is required.

### Review 6 — revision 5

- result: `REVISE`
- source: Schrodinger, independent read-only challenge
- finding: the plan simultaneously said that `is_cloud` is required on the wire and that Ollama may omit it and receive a local default; this made normalization, credential validation, idempotency hashing and client/service serialization ambiguous.
- disposition: revision 6 makes `is_cloud` mandatory for every profile-upsert wire payload, rejects omission/default inference in the Service, and keeps the Ollama local default only as an explicit UI-side value. A fresh independent review is required.

### Review 7 — revision 6

- result: `REVISE`
- source: Schrodinger, independent read-only challenge
- finding: the evidence section still hard-coded `plan_revision=5` while the active plan and log were revision 6.
- disposition: the evidence contract is now explicitly bound to `plan_revision=6`; a fresh independent review is required.

### Review 8 — revision 6

- result: `APPROVED`
- source: Schrodinger, independent read-only challenge
- finding: no remaining blocker in the bounded revision-6 plan; revision and evidence bindings are internally consistent.
- implementation_authority: open for the declared A1 write set only

### Review 9 — revision 7 transport response admission correction

- result: `APPROVED`
- source: Arendt, independent read-only challenge
- finding: A1 success response message types were already generated by `RuntimeServiceHost` and fixed by the output schema matrix, but `ProtocolValidation` did not admit them. Serialization failed after response sequence allocation, causing the observed sequence-2 fallback error. The correction is necessary and bounded.
- disposition: revision 7 adds only the three exact Provider response message constants and a central response whitelist used by message validation; it does not change handshake, envelope fields, sequence logic, request schemas, or existing A0/P3B/P3C behavior. Required regression cases cover exact response round-trip, near-miss rejection, and A1/P3B/P3C reruns.
- implementation_authority: open for the revision 7 correction write set only
