# Plan: Marcus-Awake P3C Storage / RAG Business Integration

> 状态：**`implemented`（2026-09-30 标记）。**
> 依据：本批所属子系统已存在：`framework/` 含 `MarcusAwakeFramework`／`MarcusAwakeProvider`（有真实源码，如 `Player2Provider.cs`）／`MarcusAwakeRuntimeService`／`MarcusAwakeStorage`／`MarcusAwakeTransport`。
> ⚠️ 本文件是**批次施工单**，其所属子系统已存在 ⇒ 本批视为已完成。**保留本文件**（不是 `superseded`）——
> 它记录了当时的设计意图与边界，仍有追溯价值。
> 现行方向：`AWAKE-ROADMAP.md`；分诊依据：`AUDIT-PLAN-TRIAGE-20260930.md`。


- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3C-STORAGE-RAG-20260827`
- `plan_status`: `offline_verified`
- `review_status`: `APPROVED`
- `user_signoff_required`: `covered_by_full_migration_authorization`
- `primary_executor`: `controller`
- `minimum_evidence`: `P3C-E1 + P3C-E2`
- `created`: `2026-08-27`

## 1. Scope

This batch turns the existing P3B authenticated Runtime Service into a real,
durable Storage/RAG boundary. It does not implement Provider HTTP, credentials,
AI generation, MCM, AWAKE caller wiring, game-directory synchronization, or
Bannerlord runtime verification.

The closed loop is:

```text
authenticated IPC frame
  -> strict business payload validation
  -> session/owner/permission-shaped storage operation
  -> SQLite transaction or bounded FTS5 query
  -> durable result/receipt marker where required
  -> typed response and redacted evidence
```

## 2. Confirmed existing contracts

- `MarcusAwakeTransport` v2 owns framing, integrity, session fence, sequence,
  and authentication; business payloads remain UTF-8 JSON inside `PipeEnvelope`.
- `MarcusAwakeFramework` already defines `IStorageService`, `IRagService`,
  `IKeyValueStore`, `RagIngestRequest`, `RagSearchRequest`, and `RagHit`.
- `MarcusAwakeStorage` defines `TimelineLedgerEvent` and
  `TimelineLedgerRecord`, and already implements SQLite/FTS5, KV namespaces,
  timeline append/read, corpus fingerprints, access-scope filtering, bounded
  queries, idempotent document upsert, cancellation, and typed failures.
- P3B's in-memory message/task ledger is not durable and cannot be presented as
  restart recovery. P3C must add a service-owned durable receipt ledger through
  the storage backend, and must not weaken P3B transport semantics.

## 3. Business frame contract

Add versioned message types and payload schemas for:

- `storage.kv_get.v1`, `storage.kv_set.v1`, `storage.kv_delete.v1`;
- `storage.timeline_append.v1`, `storage.timeline_read.v1`;
- `rag.ingest.v1`, `rag.search.v1`.

Every non-health business frame carries a complete session fence, owner, task
scope, deadline, payload hash, and correlation/causation IDs. The service must
reject missing or mismatched owner/session/task identity before touching SQLite.

The service derives the effective owner and session from the authenticated
envelope. A payload cannot select another owner, campaign, timeline, or session.
The minimum authorization matrix is:

| operation | required capability | storage scope | allowed owner |
|---|---|---|---|
| `storage.kv_get`, `storage.timeline_read`, `rag.search` | `storage.read` or `rag.read` | authenticated campaign/session | envelope owner only |
| `storage.kv_set`, `storage.kv_delete`, `storage.timeline_append`, `rag.ingest` | `storage.write` or `rag.write` | authenticated campaign/session | envelope owner only |

The Framework-side permission gate remains authoritative for extension policy;
the Service repeats this narrow fail-closed matrix so a forged or buggy caller
cannot cross an owner boundary.

Storage identity follows the existing backend exactly: campaign KV rows are
scoped by `(owner,campaign,timeline,namespace,key)` with an empty session ID;
session KV rows additionally include the authenticated session ID; timeline and
RAG rows are scoped by `(owner,campaign,timeline,...)`. The client cannot submit
an alternate campaign, timeline, or session in the payload.

Payloads must contain only typed storage fields and bounded text/arrays. They
must not accept raw SQL, database paths, provider DTOs, API keys, or live
Bannerlord objects.

The P3C wire limits are fixed and independently checked before deserialization
into storage APIs: namespace/key/event/document IDs <= 256 UTF-8 bytes; KV value
<= 128 KiB; timeline payload <= 128 KiB; one RAG document <= 64 KiB; one ingest
batch <= 16 documents; RAG query <= 8 KiB; timeline read <= 128 records; and
the encoded response payload <= 96 KiB. These limits are below the transport
frame bound and are not silently widened by storage defaults. The total business
payload is <= 96 KiB, business JSON depth <= 12, business object properties <=
32, and every business array <= 16 items unless a smaller operation-specific
limit above applies. Unknown fields are rejected; nested document objects may
contain only the declared fields.

Responses use the existing envelope fields and a versioned response payload.
Successful storage mutations may emit `ack_status=durably_recorded` only after
one SQLite transaction commits both the business mutation and a receipt row.
The durable idempotency key is stable across reconnect/restart and is supplied by
the caller; `task_id`, `message_id`, and `correlation_id` are attempt metadata and
must not determine receipt identity. The receipt table has a unique scope key
`(owner_id,campaign_guid,timeline_id,effective_session_id,operation,
idempotency_key)`, where campaign-scoped operations normalize
`effective_session_id` to empty and session-scoped operations use the
authenticated session ID. It stores the request payload hash, operation resource
key, response schema, response JSON, outcome, and durable event index. A
same-scope request with the same operation, idempotency key, and hash is an exact
duplicate; a different hash or resource key is a terminal conflict. A reconnect
or service restart looks up this stable scope row before dispatching the
operation. Read-only results remain non-durable. A crash before the transaction
commits leaves no receipt and is safe to retry; a crash after commit and before
response is proven by a test-only fault-injection fixture to replay the committed
response after restart.

## 4. Implementation boundary

### Transport

- Add only constants and validation for the new message types.
- Reuse `PipeEnvelope`, `TaskScopeEnvelope`, checksum, fence proof, and sequence
  logic; do not create a second framing or authentication path.

### Runtime Service

- Add a composition root that owns one service-lifetime
  `SqliteStorageAndRagBackend` under an explicit AWAKE runtime data root.
- Add a storage/RAG dispatcher with strict payload parsing, bounded concurrency,
  cancellation, and typed error mapping.
- Add a durable receipt repository in the same SQLite owner boundary. Business
  mutation and receipt commit must be atomic; receipt lookup must precede a
  retry and must distinguish exact duplicate from payload conflict.
- Use the existing campaign/timeline/session identity rules rather than creating
  a second storage scope model. The receipt repository must use the same database
  and transaction boundary as the mutation it records.
- Define stable idempotency behavior per operation: campaign KV uses the
  campaign-normalized receipt scope; session KV uses the authenticated session;
  timeline append uses event ID as its resource key; RAG ingest uses collection
  plus caller idempotency key. Retries may generate a new task/message/correlation
  ID but must reuse the idempotency key.
- Keep the current health/diagnostic state honest: report transport-ready,
  storage-ready, or degraded separately; never infer durable readiness from the
  P3B memory ledger.
- Dispose the backend during bounded service drain and classify failed drain as
  recovery-required.

### Tests and evidence

- Keep P3B harness output and evidence separate from P3C evidence.
- Add a real child-process/Named-Pipe P3C fixture covering KV round trip,
  timeline idempotency/conflict, RAG ingest/search and access filtering,
  stale-corpus rejection, cancellation, duplicate replay, service restart
  recovery, and malformed/oversized payload rejection.
- Record database path only as a redacted logical root, never expose an absolute
  user path in evidence.
- Evidence must include `schema`, tested service binary SHA-256, tested transport
  SHA-256, harness binary SHA-256, protocol/API versions, logical database-root
  fingerprint, case IDs, pass/fail per case, and a `durable_receipt_verified`
  boolean. No secret, absolute path, or raw payload is permitted.
- The restart case must record the same logical database fingerprint before and
  after service restart, the receipt row's scope/hash (redacted), and a
  `crash_after_commit_before_response_verified` boolean. The fault fixture is
  test-only and must not be enabled by normal AWAKE configuration.

## 5. Acceptance cases

1. A valid KV set/get/delete round trip survives a fresh client connection.
2. A duplicate timeline event returns the original sequence; a conflicting
   payload with the same event ID is rejected.
3. RAG ingest upserts the same document without duplication and search returns
   deterministic, access-scope-filtered hits under the requested corpus
   fingerprint.
4. A stale corpus fingerprint, unknown owner, envelope/task owner mismatch,
   wrong session, missing capability, missing task scope, oversized field or
   batch, unknown field, excessive business depth/properties, and malformed
   payload produce typed rejection without a database side effect. Each read and
   write operation is tested against both its allowed capability and a denied
   capability; owner, campaign, timeline, and session cross-boundary attempts
   are independently recorded.
5. A committed mutation is replayable after reconnect and after Runtime Service
   restart from the durable receipt row; an uncommitted/cancelled operation
   produces no false durable receipt. A same-scope different-payload retry is a
   terminal conflict.
6. Service drain closes the database and leaves no orphaned service process.

## 6. Non-goals and evidence limits

- No Provider, API key, cloud request, AI generation, streaming, MCM, or player
  UI claim belongs to P3C.
- No game launch, game-directory sync, E4, or E5 claim is allowed.
- `P3C-E1` means build/static/contract validation. `P3C-E2` means the real
  offline child-process service fixture. Neither proves Bannerlord behavior or
  a real external Provider.
- P3C-E2 must report each acceptance case individually; one aggregate PASS is
  insufficient evidence for durable restart recovery or owner isolation.

## 7. Verification result

- Verified at `2026-08-27` with a real child process and private Named Pipe.
- Release builds for Transport, Framework, Storage, Runtime Service and the P3C
  harness completed with `0 warnings / 0 errors`.
- P3C fixture passed `7/7`: KV round trip, timeline idempotency/conflict, RAG
  scope and stale corpus, capability denial, malformed/oversized payloads,
  restart receipt replay, and crash-after-commit-before-response recovery.
- `durable_receipt_verified=true` and
  `crash_after_commit_before_response_verified=true` are recorded in
  `framework/MarcusAwakeRuntimeService/tests/_build_out/Release/MARCUS-AWAKE-P3C-evidence.json`.
- The crash case is enabled only when the service receives the test-mode
  environment variables set by the harness; normal AWAKE configuration does
  not enable it.
- This evidence is offline E2 only; it does not prove Provider, MCM, AWAKE
  caller wiring, Bannerlord behavior, game-directory synchronization, E4 or E5.

## 8. Known risks to resolve before release

- P3B plan and implementation currently describe different bootstrap secret
  transport wording; this batch must not silently change that contract.
- `MarcusAwakeStorage` currently consumes the Framework assembly while the
  Runtime Service targets .NET 8; the build must prove the reference graph and
  packaging, or introduce a narrow shared contract without copying APIs.
- P3C durable receipts must not reuse the transport sequence as a durable event
  index.

## 9. Exit conditions

- P3C plan receives an independent read-only `VERDICT: APPROVED`.
- Transport, Framework, Storage, Runtime Service, and P3C fixture build with
  zero new warnings/errors.
- P3C evidence explicitly records the tested service binary and separate
  storage/RAG cases, with secrets and absolute paths redacted.
- The plan/checkpoint/current-state document is updated before any later P3D,
  P4, or P5 work.
