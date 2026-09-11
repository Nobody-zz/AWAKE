# AWAKE Three Workstations Integration — Batch 7

- Batch: `AWAKE-THREE-WORKSTATIONS-20260904`
- Parent: `AWAKE-WORLDBOOK-STUDIO-REPAIR-SEQUENCE-20260902`
- Risk: `high-risk`
- Status: `planned_after_batch_6`

## Goal

Verify and close the customer workflow across UI Workstation, Worldbook Studio, and Persona Workbench while keeping each tool's domain authority separate.

## Scope

1. Standardize launch, health, shutdown, and duplicate-instance behavior.
2. Standardize loading, empty, error, timeout, retry, cancellation, and unknown-result states.
3. Verify shared workspace/session/configuration boundaries without sharing mutation authority.
4. Verify Worldbook-to-Persona references and UI-to-worldbook navigation are explicit and stable.
5. Exercise long lists, filtering, detail navigation, and state restoration.
6. Test customer workflows from startup through save, reopen, and handoff.

## Non-goals

- No new world simulation or gameplay behavior.
- No change to Worldbook, Persona, or UI domain ownership.
- No game launch or savegame modification as an automatic test step.
- No release packaging redesign; Batch 8 owns delivery.

## Acceptance

- All three workstations expose a discoverable, observable entry point.
- Failure states provide a meaningful next action and do not lose unsaved work.
- Shared contracts agree on revision, stale, unknown, and recovery semantics.
- Long lists remain usable under representative customer-sized fixtures.
- Cross-workstation handoff preserves IDs, revisions, provenance, and review status.
- Focused workstation tests and cross-tool integration smoke pass.

## Locked Integration Contract

### Shared handoff envelope

The shared contract is `awake.workstation.handoff-envelope.v1`. Its authoritative
schema file is
`AWAKE/docs/workstation-contracts/awake.workstation.handoff-envelope.v1.schema.json`;
the consumer receipt schema is
`AWAKE/docs/workstation-contracts/awake.workstation.handoff-receipt.v1.schema.json`.
Both schemas use `additionalProperties=false`; unknown fields are rejected with
`WB-HANDOFF-400`. Producer-owned fields are
`schema_version`, `handoff_id`, `workspace_id`, `document_id`, `revision`,
`content_sha256`, `producer`, `provenance`, `review_only`, `review_status`,
`issued_at_utc`, `expires_at_utc`, `payload`, and `request_fingerprint`.
Consumer-owned fields are `consumer`, `lifecycle_status`, `accepted_at_utc`,
`consumed_at_utc`, `receipt_id`, `consumer_review_status`, and
`recovery_required`. `payload` contains the producer's canonical JSON and is
never treated as an instruction or as an AWAKE approval.

`producer` is one of `persona_workbench`, `worldbook_studio`, or `ui_workstation`.
`review_only` is a producer field, must be `true` for every handoff, and is never
changed by a consumer. Producer `review_status` is one of `approved_local`,
`needs_review`, or `rejected`; Persona Workbench may issue only
`approved_local`. Worldbook Studio preserves the producer value and sets the
consumer-only `consumer_review_status=needs_review` until a human review decision
is recorded. `lifecycle_status` is one of
`issued`, `accepted`, `consumed`, `expired`, `rejected`, `in_doubt`, or `unknown`.

The producer must issue `issued` only. `lifecycle_status` is not an envelope
field; it exists only in the consumer receipt. An incoming envelope containing it
is rejected as an unknown field. `handoff_id` must use the prefix matching
`producer` (`pwb-` for `persona_workbench`, `wbs-` for `worldbook_studio`,
`ui-` for `ui_workstation`). `provenance.source_sha256` is a lowercase
64-hex string, `provenance.source_revision` is an integer >= 1, and
`expires_at_utc` must be strictly later than `issued_at_utc`; equality is expired.
The consumer owns transitions to
`accepted`, `consumed`, `rejected`, `expired`, `in_doubt`, and `unknown`; a
consumer must not revive an expired handoff or treat `awakeApproval=not_requested`
as approval.

The frozen envelope schema uses JSON types and constraints:

- `schema_version`: required string, const `awake.workstation.handoff-envelope.v1`;
- `handoff_id`: required string matching
  `^(pwb|wbs|ui)-handoff-[a-f0-9]{32}$`;
- `workspace_id`: required string matching `^[a-z0-9][a-z0-9._-]{2,127}$`;
- `document_id`: required string, 1-128 characters, no control characters;
- `revision`: required integer, minimum 1;
- `content_sha256`: required lowercase 64-character hexadecimal string;
- `producer`: required enum;
- `provenance`: required object with required string fields `source_schema`,
  `source_id`, `source_sha256`, `issuer_id` and required integer
  `source_revision >= 1`;
- `review_only`: required boolean, const `true`;
- `review_status`: required enum;
- `issued_at_utc` and `expires_at_utc`: required RFC 3339 UTC strings ending in `Z`;
- `payload`: required string containing canonical JSON text;
- `request_fingerprint`: required lowercase SHA-256.

Canonical JSON is UTF-8 JSON with object keys sorted ordinally, arrays preserved,
no insignificant whitespace, LF line endings, and no BOM. `content_sha256` is
SHA-256 of the exact UTF-8 bytes of `payload`. `request_fingerprint` is SHA-256
of the UTF-8 bytes of the LF-joined sequence:
`schema_version`, `handoff_id`, `workspace_id`, `document_id`, decimal
`revision`, `content_sha256`, `producer`, lowercase `review_only`,
`review_status`, `issued_at_utc`, `expires_at_utc`, and `payload`.

Canonicalization rejects duplicate object keys, preserves JSON numbers in their
RFC 8259 lexical form after parsing, emits `null` and booleans as the lowercase
JSON tokens, escapes control characters and quotes using JSON escaping, and
does not append a trailing newline. The LF-joined fingerprint input also has no
trailing newline.

The consumer receipt schema is `awake.workstation.handoff-receipt.v1` and requires:
`schema_version`, `receipt_id` matching `^wbs-receipt-[a-f0-9]{32}$`,
`handoff_id`, `consumer=worldbook_studio`, `lifecycle_status`,
`consumer_review_status`, `recovery_required`, and `request_fingerprint`.
`accepted_at_utc` and `consumed_at_utc` are nullable RFC 3339 UTC strings or
null; `draft_id` is a nullable non-empty string. The receipt keeps the original envelope
`request_fingerprint` through every lifecycle state.
One receipt ID is created at import and remains unchanged through accept, consume,
recovery, and readback.

### Worldbook Studio consumer routes

Worldbook Studio exposes loopback-only routes:

- `POST /api/integration/persona/handoff/import`: validate schema, expiry, canonical
  JSON hash, document/revision/provenance, then persist a review-only inbox record.
- `POST /api/integration/persona/handoff/{handoffId}/accept`: claim the record
  idempotently and return an `accepted` receipt.
- `POST /api/integration/persona/handoff/{handoffId}/consume`: convert the accepted
  payload into a `needs_review` Worldbook draft without publishing canon.
- `POST /api/integration/persona/handoff/{handoffId}/recover`: resolve an
  `in_doubt` receipt with `retry_consume` or terminal `mark_unknown`.
- `GET /api/integration/persona/handoff/{handoffId}`: return the persisted envelope
  and consumer receipt/readback state.

Frozen request bodies are:

- `import`: `{envelope:<awake.workstation.handoff-envelope.v1>}`;
- `accept`: `{receipt_id, expected_status:"issued"}`;
- `consume`: `{receipt_id, expected_status:"accepted"}`.

The server recomputes canonical payload hash and request fingerprint; clients do
not choose a consumer receipt ID. Repeating import with the same handoff ID and
fingerprint returns the original receipt with HTTP 200. The same handoff ID with
a changed fingerprint returns `WB-HANDOFF-409`. Repeating accept after accepted,
or after consumed, returns the same receipt with HTTP 200. Accept on rejected,
expired, in-doubt, or unknown state returns the corresponding 409/410/503.
Repeating consume after consumed returns the same receipt and draft ID with HTTP
200. Consume before accept returns `WB-HANDOFF-409`; consume after expiry returns
`WB-HANDOFF-410`; consume while in-doubt or unknown returns `WB-HANDOFF-503`.

`recover` uses `{receipt_id, action:"retry_consume"|"mark_unknown", reason?}`.
`retry_consume` is allowed only from `in_doubt`, reuses the original fingerprint,
and returns 200 with `consumed` plus the existing draft ID when readback confirms
the draft. `mark_unknown` is allowed only from `in_doubt`, requires a non-empty
reason, and returns 200 with terminal `unknown`. `unknown` cannot be revived;
further accept/consume/recover calls return HTTP 503 with
`WB-HANDOFF-503`.

Every route requires the Worldbook Studio session and CSRF headers already used by
mutation routes. Responses use `{ok, data}` on success and
`{ok:false, error, message, correlation_id}` on failure. Stable mappings are:
`WB-HANDOFF-400` (malformed envelope, HTTP 400), `WB-HANDOFF-409` (fingerprint,
workspace, document, revision, or duplicate conflict, HTTP 409),
`WB-HANDOFF-410` (expired or non-revivable handoff, HTTP 410),
`WB-HANDOFF-422` (canonical hash/provenance/review validation failure, HTTP 422),
`WB-HANDOFF-503` (unknown or interrupted outcome, HTTP 503). Error responses
must include a next action in `message`; they must never claim `consumed` when
the outcome is unknown.

The exact success payloads are:

- `import`: `{ok:true, data:{handoff_id, receipt_id, lifecycle_status:"issued",
  consumer_review_status:"needs_review", review_only:true, document_id, revision,
  content_sha256}}`;
- `accept`: `{ok:true, data:{handoff_id, receipt_id, lifecycle_status:"accepted",
  accepted_at_utc}}`;
- `consume`: `{ok:true, data:{handoff_id, receipt_id, lifecycle_status:"consumed",
  draft_id, draft_status:"needs_review", review_only:true}}`;
- `GET`: `{ok:true, data:{envelope, receipt, lifecycle_status,
  consumer_review_status, recovery_required}}`.

The inbox ledger is append-only by `handoff_id` and `receipt_id`. Repeating the same
request fingerprint returns the original result; a changed payload, workspace,
document, revision, or hash returns a stable conflict. Import never writes the
Persona source tree and consume never changes AWAKE approval.

### Shared failure and recovery matrix

- `issued → accepted → consumed` is the only successful path.
- Expired input becomes `expired` and cannot be accepted or consumed.
- Hash, workspace, document, or revision mismatch becomes `rejected` with a
  human-readable retry action.
- A process interruption after acceptance but before consume becomes `in_doubt` and
  `recovery_required=true`; recovery can retry consume only with the same
  handoff/revision/hash and never with a new payload.
- Duplicate import/accept/consume is idempotent; duplicate requests with a changed
  fingerprint are conflicts.
- A failed pre-write validation becomes `rejected`; a failure after the consumer
  has claimed the handoff but before the draft write readback becomes `in_doubt`
  with `recovery_required=true`; a readback that cannot determine whether the
  draft write happened becomes terminal `unknown` and requires a manual retry
  decision. Timeout/transport failures never become `consumed` without readback.

### Workstation authority and health

For this batch, UI Workstation is the adapter project at
`AWAKE/tools/ui-workstation`. Its executable entry is
`AWAKE/tools/ui-workstation/UiWorkstation.Adapter.ps1`; it exposes the local
adapter endpoint on a dynamically selected loopback port and writes
`AWAKE/tools/ui-workstation/.runtime/ready.json` only after the endpoint is
ready. The adapter may only open or navigate to domain workstations and may not
write either domain's canonical files. `AWAKE/src` remains the game-facing
display surface, not the adapter authority. Each workstation exposes a stable
`workstation_id`, `instance_id`,
`protocol_version`, `build_id`, `workspace_id`, and public health state
`ready`, `degraded`, or `failed`. `starting`, `stopping`, and `stopped` are
process lifecycle observations, not reachable health states. PID, port, display
name, and browser tab are diagnostics only, not identity.

The observable health contract is `GET /health` for Worldbook Studio,
`GET /api/health` for Persona Workbench, and `GET /health` for the UI adapter.
A successful health response is HTTP 200 with
`{ok:true, workstation_id, instance_id, protocol_version, build_id,
workspace_id, state}`. Startup writes `state:"starting"` only to the pre-ready
runtime file; reachable health is `ready`. Degraded/failed states use HTTP 503.
A duplicate instance returns HTTP 409 with
`error=WB-WORKSTATION-INSTANCE-409`. Each process owns a named mutex
`Local\AWAKE.Workstation.<workstation_id>` and a lock file containing
`instance_id`, `pid`, `port`, and `started_at_utc`; a lock is stale only when
the recorded PID is absent and the endpoint does not answer. Graceful shutdown
returns HTTP 200 with `state=stopped`; failed browser opening returns HTTP 502
with `error=WB-WORKSTATION-BROWSER-502`. Startup and shutdown must be proven by
process, mutex/lock, and endpoint readback, not by a window title. A shutdown
response is separate from health: it returns HTTP 200 with
`{ok:true,state:"stopping",instance_id}` while the listener is draining; after
the endpoint disappears, the process is `stopped` and no health response exists.

Compatibility rules are additive: Worldbook Studio keeps both existing
`/health` and `/api/health`, and both return the unified health object while
retaining the existing `product`, `protocolVersion`, `instanceId`,
`workspaceHash`, and `port` properties. Persona Workbench adds both `/health`
and `/api/health`; its existing root page and API routes remain unchanged.
The UI adapter accepts `-Port 0`, binds an OS-selected loopback port, writes
`ready.json` with `{workstation_id, instance_id, port, address, state:"ready"}`,
and exposes `POST /shutdown` requiring the current `instance_id`.

The unified health JSON is exactly:
`{ok:boolean, workstation_id:string, instance_id:string,
protocol_version:string, build_id:string, workspace_id:string,
state:"ready"|"degraded"|"failed", product:string,
protocolVersion:string, instanceId:string, workspaceHash:string, port:integer}`.
For the UI adapter, legacy product/hash fields are still present with
`product:"AWAKE.UI.Workstation"` and `workspaceHash` equal to the first 16
lowercase hex characters of `SHA-256(workspace_id)`. WBS `/health` and
`/api/health` return byte-equivalent JSON; PWB `/health` and `/api/health`
return byte-equivalent JSON. HTTP 503 responses use the same body with
`state:"degraded"` or `state:"failed"`.

The UI adapter entry accepts exactly `-WorkspaceRoot`, `-WbsBaseUrl`, `-Port`
(default `0`), and `-RuntimeRoot` (default `<script directory>/.runtime`).
It uses `workstation_id:"ui_workstation"`,
`protocol_version:"awake.workstation.v1"`, and `build_id` from `build.json`
or the literal `dev` when absent. Its lock file is
`<RuntimeRoot>/ui-workstation.lock.json`; stale lock recovery requires both an
absent recorded PID and a failed health probe, then atomically replaces the lock
under the named mutex. `POST /shutdown` takes JSON body `{instance_id}`; a
matching live instance returns `{ok:true,state:"stopping",instance_id}`, a
mismatched instance returns `409/WB-WORKSTATION-INSTANCE-409`, and a repeated
shutdown returns `200/{ok:true,state:"stopping"}` until the endpoint disappears.
`starting` exists only in the pre-ready runtime file; reachable health is
`ready`, `degraded`, or `failed`. `stopping` is returned after shutdown is
accepted until the listener exits. A stopped process is proven by the absent PID,
released mutex/lock, and failed endpoint probe; it does not serve a `stopped`
health response.

The UI adapter navigation contract is
`POST /api/ui-workstation/navigate` with
`{workspace_id, document_id, revision, content_sha256, path,
target:"worldbook_studio"}`
and returns `{ok:true, data:{target, url, workspace_id, document_id, revision,
content_sha256}}`. Before constructing `url`, the adapter calls
`GET <wbs_base_url>/api/authoring/document?path=<encoded-path>` and compares
the returned `workspace_id`, `document_id`, `revision`, and `content_sha256`
with the request. A mismatch returns `409/WB-HANDOFF-409`; only the exact
readback revision may be opened. It rejects non-loopback targets, missing
fields, and targets other than `worldbook_studio`.

### Semantic validator and state table

The shared semantic validator is implemented twice, with identical test vectors:
`AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Core/WorkstationHandoffValidator.cs`
and
`AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/WorkstationHandoffValidator.cs`.
Both implementations consume the two schemas and apply the same checks in this
order: required/unknown fields, ID-prefix/producer match, lowercase hash shape,
RFC 3339 `Z` timestamps, `expires_at_utc > issued_at_utc`, canonical payload
parse without duplicate keys, `content_sha256`, then `request_fingerprint`.
Malformed shape or unknown fields map to `WB-HANDOFF-400`; a structurally valid
envelope that fails provenance, expiry, canonical hash, or fingerprint semantics
maps to `WB-HANDOFF-422`, with a field path in diagnostics.

The receipt state table is:

| State | `recovery_required` | `accepted_at_utc` | `consumed_at_utc` | `draft_id` | Allowed next action |
| --- | --- | --- | --- | --- | --- |
| `issued` | false | null | null | null | `accept` or expiry |
| `accepted` | false | non-null | null | null | `consume` or expiry |
| `consumed` | false | non-null | non-null | non-null | idempotent readback only |
| `expired` | false | null or non-null | null | null | readback only |
| `rejected` | false | null or non-null | null | null | readback only |
| `in_doubt` | true | non-null | null | null or non-null | `recover retry_consume` or `mark_unknown` |
| `unknown` | true | non-null | null or non-null | null or non-null | readback only; new handoff required |

The validator rejects every other field/state combination. `reason` for
`mark_unknown` is persisted in the receipt's `recovery_reason` field, which is
required only for `unknown`; `recovery_reason` is never accepted from the
producer envelope.

Persona Workbench owns Persona authoring/review and only issues handoffs.
Worldbook Studio owns Worldbook drafts, handoff inbox, acceptance, consumption,
and Worldbook review. UI Workstation only navigates, displays, and requests
domain operations; it has no Persona or Worldbook canonical write authority.

### Required integration evidence

The Batch 7 smoke must run in a fresh loopback-only temporary workspace and prove:

1. all three entry points expose health and duplicate-instance behavior;
2. Persona issues one envelope and Worldbook imports, accepts, consumes, and reads it back;
3. IDs, revision, content hash, provenance, and review status survive the handoff;
4. stale revision, expiry, changed hash, duplicate request, interrupted consume, and
   recovery paths return the declared states and error codes;
5. no game process, game directory, Manager management endpoint, external provider,
   or cloud API is touched.

The evidence file is
`docs/evidence/AWAKE-THREE-WORKSTATIONS-20260904.json` and must contain one
case object per requirement, with `id`, `passed`, `expected`, `observed`, and
`artifacts` fields. The mandatory case IDs are:
`health-all-ready`, `duplicate-instance`, `shutdown-releases-lock`,
`persona-issue`, `wbs-import`, `wbs-accept`, `wbs-consume`,
`handoff-readback`, `duplicate-idempotent`, `changed-fingerprint-conflict`,
`stale-revision`, `expired-handoff`, `interrupted-consume-in-doubt`,
`unknown-readback`, `recovery-retry`, `unsaved-draft-preserved`,
`list-filter-detail-restored`, and `ui-navigation-readback`.
The fixture uses 1000 Worldbook candidates and 200 Persona records; list/filter/
detail restoration passes when the selected stable ID and scroll anchor survive
refresh. Unsaved work passes when a forced reload leaves the local draft marked
dirty and offers `恢复本地草稿`; loss of the draft is a failure. Navigation
passes only when the UI adapter opens the exact WBS path and the WBS readback
returns the same `document_id`, `revision`, and `content_sha256`. The smoke
records raw request/response JSON, process IDs, lock files, endpoint snapshots,
workspace file hashes, and an external-resource audit showing no game,
Manager, cloud, or non-loopback access.
