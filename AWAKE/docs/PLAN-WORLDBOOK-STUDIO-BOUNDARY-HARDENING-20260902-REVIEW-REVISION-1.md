# Worldbook Studio boundary hardening review revision 2

- Parent plan: `PLAN-WORLDBOOK-STUDIO-BOUNDARY-HARDENING-20260902.md`
- Review target: `WORLDBOOK-STUDIO-BOUNDARY-HARDENING-20260902`
- Revision: `2`
- Status: `REVISE_PENDING_REVIEW`

The batch is narrowed to HTTP error projection and unknown-exception containment. This revision is authoritative over the parent plan. SafeId and legacy identifier migration are explicitly deferred because old records use the existing sanitized filename mapping and cannot be safely distinguished from new invalid input without a controlled legacy index.

## Exact error contract

- Preflight validation failures that are guaranteed to happen before mutation return `side_effect=none`.
- Operation failures after filesystem work may have occurred return `side_effect=unknown`; they include a correlation id so the operation query or local log can be used for diagnosis. The batch does not falsely claim zero side effects.
- Retired routes return `410`, `side_effect=none`, a safe message, and correlation id.
- Known authority codes have an explicit HTTP mapping: 400 for malformed request, 404 for missing authority/staging record, 409 for CAS/operation/proof conflict, 422 for semantic validation, and 500 only for unknown/unclassified failures.
- Unknown exceptions are caught by one exception boundary before a response is started. If the response has already started, the boundary only logs the correlation id and closes the response; it never writes a developer exception page.
- Error bodies contain only `ok=false`, stable `error`, safe `message`, `side_effect`, and `correlation_id`. Raw exception text, workspace paths, operation records, proof contents, and owner details are excluded.
- Successful register/export/publish/operation responses are out of scope; their existing relative-path and operation projections are recorded as a separate public-projection batch.

## Required implementation seams

- Extract the authority error projection into a Web-internal mapper/helper with a deterministic code-to-status and code-to-safe-message table.
- Add one global exception boundary around the Web pipeline and one correlation-id generator; both are testable without starting Bannerlord or a real Provider.
- Keep existing route paths, customer compile route `/api/authoring/compile`, retired-route behavior, and Core authority semantics unchanged.

## Required tests

- Unit tests for every known authority status group, `WB-AUTHORITY-STAGING-404`, unknown code, and unknown exception.
- Response-body assertions for safe fields, correlation id, side-effect classification, and absence of raw exception/path text.
- A Web in-process smoke/host fixture or equivalent loopback HTTP test that forces an unknown exception and proves no developer exception page is returned.
- Existing AuthorityGate, customer closure, HTTP smoke, and Studio harness remain green.

Implementation remains blocked until terminal review approval and explicit user sign-off.

## Authority of this revision

This revision formally supersedes the original parent-plan scope. The only implementation unit that may be approved from this document is Web HTTP error projection and unknown-exception containment. It does not authorize SafeId changes, legacy-record migration, success-response projection, compile settlement, persistence, recovery, or route-registry changes.

## Failure context and side-effect contract

Every projected failure carries an internal `AuthorityFailureContext` selected at the route boundary, never inferred from the exception message alone:

- `Preflight`: request binding, null/missing fields, identifier/shape checks, proof lookup and semantic checks that complete before any mutation call. Projection is `side_effect=none`.
- `MutationUnknown`: the route has entered a Core operation that may read, write, publish, or otherwise mutate filesystem state and then throws. Projection is `side_effect=unknown`, including for `IOException`, `UnauthorizedAccessException`, `TimeoutException`, and an unclassified exception from the operation call.
- `RetiredRoute`: the middleware rejects a retired endpoint before dispatch. Projection is HTTP `410`, `side_effect=none`.

The implementation must pass the context explicitly to the mapper. A catch block must not label a failure `none` merely because the exception is an `InvalidOperationException`; route handlers must mark the boundary immediately before invoking a mutating authority method. Preflight failures raised before that point remain `none`. The current authority operation semantics and their persistence/recovery behavior remain unchanged.

## Exact HTTP error matrix

The mapper extracts only the stable prefix before the first `:`. Unknown or empty prefixes are classified as `WB-AUTHORITY-UNKNOWN-500`.

| Error code | HTTP status | Safe message key | Preflight context | Mutation context |
|---|---:|---|---|---|
| `WB-AUTHORITY-400` | 400 | `WB-AUTHORITY-400` | `none` | `unknown` |
| `WB-AUTHORITY-404` | 404 | `WB-AUTHORITY-404` | `none` | `unknown` |
| `WB-AUTHORITY-SELECTION-404` | 404 | `WB-AUTHORITY-SELECTION-404` | `none` | `unknown` |
| `WB-AUTHORITY-STAGING-404` | 404 | `WB-AUTHORITY-STAGING-404` | `none` | `unknown` |
| every existing `WB-AUTHORITY-*-409` code, including `CAS`, `STAGING`, `OPERATION`, `APPROVAL`, `PROOF`, `CLOSURE`, `COMPILE`, `POINTER` | 409 | code-specific safe text | `none` | `unknown` |
| every existing `WB-AUTHORITY-*-422` code, including `DOCUMENT`, `SELECTION`, `TIER`, `COMPILE` | 422 | code-specific safe text | `none` | `unknown` |
| `WB-AUTHORITY-PROOF-403` | 403 | `WB-AUTHORITY-PROOF-403` | `none` | `unknown` |
| `WB-AUTHORITY-LEGACY-410` | 410 | `WB-AUTHORITY-LEGACY-410` | `none` | n/a |
| unknown authority code or unknown exception | 500 | `WB-AUTHORITY-UNKNOWN-500` | `none` | `unknown` |

The table is exhaustive for this batch: no authority error falls through to HTTP `400`, and no unknown exception is exposed as a framework error page. If a future authority code is added, the mapper test must fail until the code is classified explicitly.

## Public response algorithm

1. Create a correlation id at the first error boundary using a test-injectable generator. The default format is a lowercase, hyphenated UUID v4 string; the exact value is opaque and must not encode paths, IDs, or exception text.
2. Log the correlation id, route, HTTP method, stable error code, failure context, and exception type to the local diagnostic sink. Raw exception details remain in that sink only.
3. If `HttpResponse.HasStarted` is false, write exactly one JSON body with these fields only: `ok=false`, `error`, `message`, `side_effect`, `correlation_id`. `error` is the stable mapped code, `message` is a fixed Chinese safe message, and `side_effect` is `none` or `unknown` from the explicit context.
4. Set the `X-AWAKE-Correlation-Id` response header to the same value before writing the body. The body field and header must match.
5. If the response has already started, do not write or append a body, do not attempt to change the status, and do not emit a second error response. Log the correlation id and rethrow/close through the host pipeline according to ASP.NET Core semantics.

Retired-route middleware uses the same algorithm and schema, with `WB-AUTHORITY-LEGACY-410`, HTTP `410`, and `RetiredRoute` context. It must no longer return the reduced legacy body currently present in `Program.cs`.

## Route truth and deferred registry work

- The customer-facing compile route currently registered in `Program.cs` is `/api/authoring/compile`; its authority-backed implementation is in the `CustomerCompileRoute` handler.
- `/api/ai/authoring/compile` appears in `contracts/authoring-action-route-registry.v1.json` but is not the current executable customer route. This batch records the discrepancy only; it does not add an alias, change the registry, or alter the route contract.
- The route registry reconciliation is a separate route-contract batch and must not be smuggled into the error-boundary patch.

## Fault-injection seams and executable fixtures

The Web project must expose internal test seams without requiring Bannerlord, a real Provider, API keys, or a live external service:

- `IAuthorityFailureCorrelationIds` (or equivalent internal abstraction) supplies deterministic IDs in unit tests and UUID v4 IDs in production.
- `IAuthorityFailureDiagnostics` (or equivalent internal abstraction) captures the structured diagnostic tuple without returning raw details to HTTP.
- A test-only route/service seam throws a non-authority exception before mutation (`Preflight` fixture) and after entering a mutation call (`MutationUnknown` fixture). The latter must create or attempt a sentinel filesystem mutation so the test proves the response is `unknown`, not `none`.
- A loopback/in-process Web host fixture invokes both seams and asserts status, header/body correlation equality, safe-field allowlist, absence of exception/path text, and absence of a developer exception page.

Minimum focused tests:

- One table-driven unit test for every row/group in the HTTP matrix, including `WB-AUTHORITY-STAGING-404`, an unknown authority prefix, and an exception with no colon/prefix.
- One preflight test proving `side_effect=none` and no workspace change.
- One mutation-failure test proving `side_effect=unknown`, correlation logging, and no false success response.
- One retired-route test proving `410`, the shared correlation schema, and `side_effect=none`.
- One response-started test proving the boundary does not append a second body or overwrite the started response.
- Existing AuthorityGate and customer-closure tests remain unchanged and green.

## Explicitly deferred work

- Compile operation ID/digest formula, reservation window, marker authenticity, `.tmp`/`.previous` recovery, orphan-output policy, and persisted CompileResult replay belong to the separate settlement batch already split from this review.
- SafeId exact legacy lookup and migration belong to a compatibility batch.
- Successful response public projection belongs to a public-wire-contract batch.
- Draft/Batch domain unification and explainable AI segmentation belong to the authoring-domain batch.
