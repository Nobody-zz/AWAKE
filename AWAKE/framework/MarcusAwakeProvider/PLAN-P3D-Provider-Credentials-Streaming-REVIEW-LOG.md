# P3D Provider Prototype Review Log

## Review 1

- `reviewer`: independent read-only Codex reviewer
- `scope`: revised-plan challenge only; no implementation files existed or were modified
- `write_set`: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeProvider\**`
- `verdict`: `REVISE`

### Accepted findings and plan corrections

1. **P1 capability probing underspecified** — added explicit capability states and narrowed this batch to verified `/models` discovery plus `unverified` generation/stream/usage/structured capabilities.
2. **P1 endpoint/redirect safety incomplete** — added an injected endpoint policy, exact-origin/fixed-path request construction, explicit redirect rejection, and a no-claim boundary for DNS/private-network governance.
3. **P1 credential persistence claims too strong** — added atomic writes, corrupt-record fail-closed behavior, buffer clearing, best-effort Unix mode, and an explicit no-ACL/no-process-isolation limitation.
4. **P1 bounded reads incomplete** — changed the contract to raw-byte incremental reads, bounded SSE line/data builders, aggregate accounting, `Accept-Encoding: identity`, and compressed-body rejection.
5. **P1 SSE terminal/cancellation behavior incomplete** — added a one-terminal state machine, EOF-without-`[DONE]` failure, partial-line handling, cancellation precedence, and disposal assertions.
6. **P1 429/deadline precedence incomplete** — bounded `Retry-After` parsing is metadata-only; caller cancellation wins over deadline, deadline wins over transport cancellation, and the prototype never sleeps or schedules a retry.
7. **P1 structured validation ambiguous** — narrowed the adapter to bounded syntactic JSON validation and leaves full JSON Schema semantics to the later Core/Runtime mapping.
8. **P2 fallback identity too narrow** — added normalized endpoint/effective-model/credential-reference attempt identity in addition to provider ID.
9. **P2 route-change observability incomplete** — added sequence, provider/model, source error, and from/to fields to the local stream event contract.
10. **P2 retryability matrix incomplete** — acceptance now covers each status and stream terminal path explicitly.
11. **P3 isolation evidence wording** — all audit/test artifacts are required to stay under the declared prototype directory.

### Remaining decisions for second review

- Verify that the revised raw-byte SSE design is proportionate and does not introduce unnecessary abstractions.
- Verify that the capability snapshot is honest and does not pretend to be a live handshake.
- Verify that the endpoint-policy seam and degraded credential-store warning are explicit enough for a standalone prototype.

## Gate

- `second_review_verdict`: `REVISE`; its residual findings are implemented as explicit transport/policy contracts, deterministic clock/KDF seams, terminal-state tests, and local-only risk documentation.
- `user_signoff_required`: `false`
- `user_signoff`: explicit authorization in the current user turn on 2026-08-26
- `implementation_authorized`: `true`
- `next_gate`: implement only under the bounded provider directory, then run focused offline build/tests and write-set audit
