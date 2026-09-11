# Plan: Marcus-Awake P3B 真实 Runtime Service / IPC

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P3B-IPC-RUNTIME-20260826`
- `plan_status`: `core_offline_verified`
- `review_status`: `approved`
- `user_signoff_required`: `already_granted_by_autonomous_migration_authorization`
- `primary_executor`: `controller`
- `minimum_evidence`: `P3B-E1 + P3B-E2`
- `later_evidence_gate`: `P3/E2-real-runtime-service-smoke`
- `created`: `2026-08-26`

> 本批只实现新的 Marcus-Awake 本地 Runtime Service、受控 Named Pipe 协议和可离线复现的进程/协议证据。它是 P3A 的真实边界实现，不代表 SQLite、Provider、凭据、MCM、AWAKE caller 或游戏目录已迁移完成；这些仍按后续批次推进。

## 1. 已确认事实

| 来源 | 已确认事实 | 对本批的约束 |
|---|---|---|
| `framework/MarcusAwakeFramework` | P3A 已提供 `RuntimeService`、`IRuntimeServicePort`、`AiTaskScope`、请求规范化和 typed result，但没有物理进程、管道或文件 I/O | P3B 以新 transport/IPC adapter 接入，不把 P3A 测试替身变成生产后端 |
| P1.5 契约 | 新 API major 为 `2.0`；新协议必须携带实例、父进程、SID、epoch、方向 nonce、sequence、payload hash 和 fence proof | 旧 `marcus-ai-framework/1.0`、旧 pipe 名称、旧 `campaign_id` envelope 不兼容 |
| 游戏目录原 Marcus | 已安装旧 Companion 为 `.NET 8` 进程，含 SQLite/LLamaSharp/Provider/资产等实现；其源码只能作为行为参考 | 不复制旧二进制，不复用旧 storage root、pipe、协议或旧模块依赖 |
| AWAKE 工程 | `AWAKE.csproj` 仍引用外部 `MarcusAIFramework`，主调用方尚未迁移 | P3B 不改 `AWAKE.csproj`、`SubModule.xml`、AWAKE caller 或游戏目录；这些留给最终接线批次 |
| 运行边界 | 游戏 tick/主线程不得阻塞；Service 可独立退出且不能阻止游戏加载 | Service 生命周期全部异步/可取消，游戏侧只拿 typed degraded 状态 |

## 2. P3B 最小闭环

```text
AWAKE-side test client / future Framework IPC client
  → bootstrap launch descriptor
  → authenticated Runtime Service process
  → private Named Pipe accept
  → handshake and capability intersection
  → bounded framed request
  → checksum / session / epoch / nonce / sequence validation
  → deterministic echo/health response
  → ACK / retry / cancellation / disconnect handling
  → redacted evidence record
```

P3B 的业务响应只使用中性 `echo`、`health`、`cancel` 和 `diagnostic` 流程；真实 Provider、SQLite、凭据和世界语义不进入本批，以便把跨进程问题与后端问题分开证实。

## 3. 目标结构

```text
framework/
├─ MarcusAwakeFramework/                 # net472 + netstandard2.0 public/internal API
├─ MarcusAwakeTransport/                 # netstandard2.0 shared protocol/framing/auth primitives
└─ MarcusAwakeRuntimeService/             # net8.0-windows executable, service-owned process
tools/
└─ verify_marcus_awake_p3b.ps1           # static/build/negative/evidence runner
docs/
├─ MARCUS-AWAKE-P3B-IPC-HANDOFF-CONTRACT-20260826.md
├─ PLAN-MARCUS-AWAKE-P3B-IPC-RUNTIME-20260826.md
└─ evidence/MARCUS-AWAKE-P3B-*.json
```

### 3.1 Transport contract

`MarcusAwakeTransport` owns only platform-neutral protocol values and bounded serialization helpers:

- protocol id `marcus-awake-runtime`;
- protocol major `2`, minor `0`;
- pipe identity `MarcusAwake.Runtime.v2.<user-sid-fingerprint>`; the old Marcus name is rejected;
- handshake DTOs and frame DTOs matching P1.5 §5.1/§5.2;
- UTF-8 JSON body with a 4-byte little-endian length prefix and maximum frame bound;
- SHA-256 payload checksum, strict length check, duplicate-key rejection and bounded JSON depth/field counts;
- domain-separated HMAC/HKDF helpers for bootstrap challenge and per-frame fence proof;
- independent client→service and service→client sequence windows;
- typed protocol error codes without localized-text parsing.

No transport public type may expose an API key, provider DTO, database path, pipe handle, live TaleWorlds object or raw bootstrap secret.

### 3.2 Bootstrap and trust

The game-side bootstrap owns a one-run `launch_transaction_id`, `launch_nonce`, 256-bit `bootstrap_secret`, parent PID/create-time proof and instance epoch. The implementation passes the launch descriptor through redirected standard input and sends one-time key material as a second length-prefixed standard-input frame; it never places the secret in command-line arguments or a persistent file. The descriptor is single-use and erased after the Service has derived its challenge key.

The Service must verify:

1. current-user SID and private pipe ACL;
2. parent PID and parent process creation time;
3. launch transaction and executable hash proof;
4. protocol/framework API major and service identity;
5. one-time challenge response;
6. fresh connection epoch and direction nonces.

If Windows-only proof cannot be performed in a non-Windows harness, the harness uses a named `fake-windows-proof` implementation with the same transcript and negative cases; it must not silently mark the real proof as passed.

### 3.3 Frame and state machine

The new frame contains:

```text
message_type, message_id, correlation_id, campaign_guid, timeline_id,
session_id, session_generation, owner_id, deadline_utc, instance_epoch,
connection_epoch, direction_nonce, sequence, fence_proof?, payload_schema,
payload, payload_length, payload_sha256, checksum, task_scope?
```

`campaign_guid`, `timeline_id`, `session_id` and `session_generation` are mandatory on every task and cancellation frame. `task_scope` is mandatory for `echo` and `cancel` and contains the complete `AiTaskScope` identity, including `task_id`, `message_id`, `owner_id`, route/provider/profile, idempotency key, request payload hash, output schema and settlement requirement. A health frame is the only read-only exception: it may omit campaign/timeline/task scope and uses the authenticated connection fence alone. Every non-health frame must carry `payload_length` and `payload_sha256`; the checksum is not an authentication substitute.

Validation order is fixed: process/ACL identity → instance epoch/fence → session fence and task scope → connection epoch/nonce/sequence → payload length/hash/schema → route/permission/业务条件. The first P3B handlers only require `health`, `echo`, and `cancel` schemas, but `echo` and `cancel` use the complete task/session contract so later P3C/P3D handlers do not need a weaker legacy shape.

Sequence behavior:

- each direction has its own nonce, watermark and bounded window;
- contiguous next sequence is accepted;
- bounded future sequence is held or returns `sequence_out_of_order` according to the configured window;
- gap beyond the window returns `sequence_gap`;
- exact same sequence/message/payload hash returns the original ACK;
- any conflicting reuse returns `replay_rejected`;
- reconnect creates a new connection epoch, nonces and sequence origins;
- old session/epoch/nonce frames are rejected and never dispatched.

Task behavior:

- `message_id` is transport identity; `task_id`/`idempotency_key` remain business identity;
- duplicate requests return the existing ACK or terminal receipt;
- cancellation is idempotent and cannot invent a terminal receipt;
- Service shutdown first stops admission, cancels active work, drains bounded writes and reports orphaned work as `recovery_required`;
- P3B does not claim durable business receipts; durable first-write-wins is P3C.

### 3.4 ACK, outcome and durability reconciliation

P1.5 is authoritative for the transport `ack_status` field. P3B emits only `ack_status=accepted`, because this batch has no durable business ledger. It must never emit `ack_status=durably_recorded`; that value becomes legal only after P3C storage commits the corresponding event or receipt.

The P3B handoff meanings are represented in a separate typed `outcome_kind` field and do not redefine `ack_status`:

| `outcome_kind` | Meaning in P3B | Retry rule |
|---|---|---|
| `accepted` | frame passed authentication/schema checks and was admitted to the bounded handler | caller may await the response |
| `duplicate` | exact same message/sequence/payload was already admitted | return the original response; do not dispatch twice |
| `retryable_reject` | no side effect was committed and the caller may retry under the same scope | retry is allowed |
| `terminal_replay` | the original terminal response is available in the current process ledger | return that response; do not create a task |
| `rejected` | invalid, stale, unauthorized or conflicting frame | do not replay automatically |

P3B's in-memory ledger may provide `duplicate` and `terminal_replay` only for the lifetime of the service process. It must label the result `non_durable=true`; P3B evidence cannot call this a durable receipt or recovery proof. P3C will bind the same outcome vocabulary to durable `ServiceReceipt` records.

## 4. Ownership and write set

| Owner | Exact write set | Must not touch |
|---|---|---|
| Controller | this plan, review log, CURRENT/checkpoint, final integration | no shared source before review approval |
| Framework transport implementer | `framework/MarcusAwakeTransport/**`, its project files and focused tests | AWAKE, old SDK, game directory, Provider/SQLite |
| Runtime Service implementer | `framework/MarcusAwakeRuntimeService/**`, its project files and focused tests | Framework public API semantics, AWAKE caller, external Marcus binaries |
| Evidence verifier | `tools/verify_marcus_awake_p3b.ps1`, `framework/.../tests/P3B*`, `docs/evidence/MARCUS-AWAKE-P3B-*` | source behavior changes outside declared test/runner set |

One file has one active writer. Shared build manifests and CURRENT are controller-owned and are integrated only after child results are reviewed.

## 5. Acceptance cases

| ID | Case | Observable result | Minimum proof |
|---|---|---|---|
| P3B-01 | Service starts with valid launch descriptor | fresh service instance, PID/create-time proof and private pipe are reported without secrets | process harness + redacted evidence |
| P3B-02 | Valid handshake | protocol/API major, service identity, SID, parent proof, nonce and capability intersection are accepted | positive protocol fixture |
| P3B-03 | Invalid handshake | wrong major, service, SID, parent proof, nonce, old pipe or replayed challenge is rejected before task dispatch | negative matrix |
| P3B-04 | Valid framed echo/health | bounded request receives ordered typed ACK and response; no raw provider or key field crosses boundary | client/service process smoke |
| P3B-05 | Frame integrity | bad length, checksum, schema, duplicate key, oversized/deep JSON and deadline are rejected without large allocation | parser/security fixtures |
| P3B-06 | Sequence | gap, bounded out-of-order, exact retry and conflicting reuse produce the specified codes | sequence fixture |
| P3B-07 | Session/epoch | reconnect invalidates old epoch/session/nonce and late result is discarded | reconnect fixture |
| P3B-08 | Cancellation/drain | repeated cancel is safe; shutdown rejects new work, drains bounded work and leaves no orphaned process in harness | lifecycle fixture |
| P3B-09 | Parent exit | parent termination causes Service shutdown within the bounded grace period and no further writes are accepted | parent-process harness |
| P3B-10 | Legacy negative gate | old Marcus module/pipe/storage root is not selected in AWAKE-only mode | static and launch-path negative scan |
| P3B-11 | Game safety | unavailable/incompatible Service maps to typed degraded state and never blocks a game lifecycle callback | Framework adapter test; real game remains pending |

## 6. Evidence and exit gate

`P3B-E1`:

- transport and service Release build succeeds with zero warnings/errors;
- shared protocol JSON/schema parse succeeds;
- forbidden-scope scan rejects old Marcus references, API keys, raw paths and blocking waits in production code;
- API/protocol baseline and service executable hash are recorded.

`P3B-E2`:

- a real child process and real private pipe are exercised by the harness;
- positive handshake, frame, echo/health, sequence, reconnect, cancellation and drain cases pass;
- negative identity, parent proof, old protocol/pipe, checksum, size/depth and replay cases pass;
- evidence writer records redacted fields only and independent validator accepts the evidence schema;
- service crash/orphan cleanup and parent exit are observed in the bounded harness.

The evidence validator must assert that every task/cancel frame includes the full session fence and task scope, that health is the only permitted omission, that `ack_status` is never `durably_recorded` in P3B, and that duplicate/terminal replay results carry `non_durable=true`.

P3B cannot claim P3/E2 until a future AWAKE game client connects to the service. P3B cannot claim SQLite/Provider/credential functionality; those are P3C/P3D.

## 7. Non-goals and explicit deferrals

- no change to `AWAKE.csproj`, `SubModule.xml`, current AWAKE callers or external game module;
- no real SQLite/FTS5, Provider HTTP, LLamaSharp, Credential Manager, MCM, loopback management page or DevTools UI;
- no Bannerlord launch, deployment, save/load or E4/E5 claim;
- no automatic migration of old Marcus DB, pipe, profile or credentials;
- no compatibility mode that accepts old Marcus protocol or old module simultaneously;
- no copied decompiled source or old binary in the AWAKE package.

## 8. Open risks tracked, not silently resolved

1. Windows inherited-handle bootstrap needs a deterministic cross-process test without placing the secret in command-line or disk.
2. .NET 8 Service and net472 game client must share exactly one canonicalization implementation; a netstandard2.0 transport assembly is preferred over duplicate serializers.
3. Named Pipe ACL and impersonation require Windows evidence; fake proof cannot substitute for E4.
4. Service cleanup must not mistake a crashed executor for a completed receipt; P3C recovery ledger remains authoritative.
5. Packaging the Service and its native dependencies is a later release concern; this batch proves the process boundary from the build output, not a player installer.

## 9. Governance state

- `plan_status=core_offline_verified`
- `review_status=approved`
- `user_signoff_required=already_granted_by_autonomous_migration_authorization`
- `primary_executor=controller`
- `minimum_evidence=P3B-E1 + P3B-E2`
- `verified_at=2026-08-27`
- `verified_result=real child process and private Named Pipe harness 19/19 passed; current evidence is offline E2`
- `open_gates=P3B-10 full legacy module/storage-root negative scan; P3B-11 AWAKE game-side typed degraded safety fixture; E3/E4/E5`
- `user_signoff_record=2026-08-26 user authorized autonomous execution through full migration merge`
