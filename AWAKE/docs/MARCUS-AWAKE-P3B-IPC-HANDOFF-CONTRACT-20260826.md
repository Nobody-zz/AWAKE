# Marcus-Awake P3B IPC handoff contract

## Purpose

This document is a no-I/O design handoff for P3B. It does not implement Named Pipe, process management, credentials, SQLite or a live provider. P3A may define neutral ports and deterministic in-memory fixtures only; P3B must implement and separately verify the process/protocol boundary.

## Ownership

- The Bannerlord process owns the active game session, session generation, player-facing permission decision and command settlement.
- The Runtime Service process owns HTTP, credentials, provider adapters, streaming transport, SQLite/RAG and durable event/receipt storage.
- The IPC boundary owns authentication, instance identity, parent proof, connection epoch, direction sequence, payload schema and checksum validation.
- No API key, authorization header, database path, full endpoint URL or raw provider payload crosses the game-facing Framework API.

## Handshake envelope

Every connection starts with one `hello` request and one `hello_ack` response. The canonical envelope fields are:

| Field | Rule |
|---|---|
| `protocol_version` | Stable protocol major/minor; incompatible major rejects the connection. |
| `framework_api_major` | Must equal `2`; the service must not impersonate the legacy Marcus API. |
| `service_id` | Stable `marcus-awake.runtime-service` identifier. |
| `service_instance_id` | Fresh opaque instance ID per process start. |
| `parent_process_id` | The verified Bannerlord parent process identity; never used as a long-term game identity. |
| `parent_start_proof` | Service-owned proof bound to the observed parent start, not a display name. |
| `user_sid_fingerprint` | Opaque fingerprint used for same-user binding; raw SID is never logged or returned to the game. |
| `connection_epoch` | Monotonic per service instance; increments after reconnect and invalidates the old fence. |
| `direction_nonce` | Fresh per-direction nonce; reused nonces reject the connection. |
| `capabilities` | Versioned capability IDs only; unknown or unsupported capabilities fail closed. |
| `max_frame_bytes` | Negotiated bounded frame size; the lower side wins. |
| `checksum_algorithm` | `sha256` for P3B; no implicit algorithm negotiation. |
| `correlation_id` | Opaque request correlation ID retained in redacted diagnostics. |

The handshake is accepted only when the protocol major, Framework API major, service ID, same-user binding, parent proof, nonce, frame bound and checksum algorithm all match. A stale `connection_epoch` or invalid parent proof closes the connection without dispatching a task.

## Data frame

After handshake, each frame contains:

```text
protocol_version
connection_epoch
direction_nonce
direction_sequence
message_id
message_kind
schema_id
schema_version
payload_byte_length
payload_sha256
payload
correlation_id
causation_id
session_fence
```

`session_fence` contains `campaign_guid`, `timeline_id`, `session_id` and `session_generation`. `task_id` and `message_id` are distinct; `direction_sequence` is transport ordering and must not be used as the durable event index. The canonical payload is UTF-8 JSON produced by the same request canonicalization family used by `MarcusAwakeFramework.Api`; payload bytes are checked before deserialization.

The service rejects duplicate object keys, oversized payloads, invalid schema/version, checksum mismatch, stale connection epoch, stale session generation, sequence gaps without an explicit retry path, and any frame containing credentials or an unapproved raw provider payload.

## ACK and retry

The only P3B ACK meanings are:

- `accepted`: frame passed authentication and schema checks but is not yet durable;
- `duplicate`: the same `message_id` and payload fingerprint was already accepted;
- `retryable_reject`: no side effect was committed and the caller may retry under the same idempotency scope;
- `terminal_replay`: the service returns the previously committed terminal receipt;
- `rejected`: the frame is invalid, stale or unauthorized and must not be replayed.

`durably_recorded` is permitted only after the service has committed the corresponding durable event/receipt in P3C storage. P3A's in-memory fixture must never emit or assert that status. A transport ACK, task stream sequence and durable event index remain three separate values.

## Task and cancellation fence

Every task frame carries the complete `AiTaskScope` and request payload hash. A task is admitted only if the connection epoch and session generation are current. Cancellation is a request tied to the same task scope; it does not fabricate a terminal receipt. The service publishes exactly one durable terminal for a task scope. Completion versus cancellation is decided at the durable compare-and-set point, and a reconnect may replay only the committed terminal receipt.

## Negative gates before P3B/P4

The P3B/P4 evidence matrix must include negative fixtures proving that:

- the old `MarcusAIFramework` module, old Companion process, legacy pipe name or legacy storage root is not selected when AWAKE-only mode is active;
- an old API assembly cannot satisfy the new `framework_api_major=2` handshake;
- a stale service instance cannot accept frames from a new connection epoch;
- a frame from another user, parent process, campaign, timeline or session is rejected;
- a provider/credential failure returns a typed degraded result without blocking Bannerlord lifecycle callbacks.

These are handoff requirements, not P3A evidence. The implementation and evidence files belong to P3B/P3D/P4 and must be approved as their own batch before real process or game-directory work.

## Required P3B exit evidence

P3B cannot be declared complete from a class or pipe name alone. It must provide a separate evidence schema and runner that records the exact service binary, protocol version, service instance, parent proof result, handshake fields (redacted), frame/checksum decisions, reconnect/epoch fixtures, cancellation/terminal replay, crash/orphan cleanup and the matching Framework/AWAKE build hashes. No live provider or SQLite claim belongs in P3B; those are P3C/P3D gates.
