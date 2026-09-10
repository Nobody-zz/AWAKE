# Persona Workbench authoring handoff v1

`persona-workbench.authoring-handoff.v1` is an immutable producer artifact. Persona
Workbench issues it only after a locally approved document has passed the existing
receipt and contract-asset validation. It does not grant or represent AWAKE runtime
approval.

## Cross-workstation envelope

The top-level `envelope` is the boundary payload consumed by another workstation.
Its field names are intentionally stable `snake_case` names:

| Field | Meaning |
| --- | --- |
| `workspace_id` | Stable workspace identity supplied by the producer caller. It is not a filesystem path. |
| `document_id` | The Persona document identity; it must equal the handoff `documentId`. |
| `revision` | Positive workspace/document revision supplied by the producer caller. Consumers use it for stale detection. |
| `content_sha256` | SHA-256 of the canonical authoring JSON; it must equal the handoff `contentSha256`. |
| `provenance` | Structured origin record: `producer=persona_workbench`, `authority=persona_local_authoring`, plus the source receipt and evidence IDs. |
| `review_status` | `local_approved`; this describes Workbench-local review only. It is not AWAKE approval. |
| `handoff_id` | Stable identity of this handoff; it must equal the handoff `handoffId`. |
| `expires_at` | UTC expiry instant; it must equal the handoff `expiresAtUtc`. |
| `lifecycle_status` | Producer output is always `issued`. The schema enumerates the consumer lifecycle vocabulary for shared semantics. |

## Lifecycle semantics

- `issued`: Persona Workbench has created and validated the immutable handoff. A
  consumer may verify it, but it has not claimed or applied it.
- `accepted`: A consuming workstation has verified the envelope and claimed the
  handoff for processing. This is a consumer-side state and is not emitted by
  Persona Workbench.
- `consumed`: A consuming workstation has applied the exact `content_sha256`
  payload once. This is a consumer-side state and does not change the source
  document or its authority.
- `expired`: `expires_at` has passed before acceptance/consumption. The handoff
  must be rejected and cannot be revived by changing local clocks or review
  labels.

The producer schema constrains emitted `lifecycle_status` to `issued`. A consumer
must record `accepted`, `consumed`, or `expired` in its own receipt/ledger rather
than mutating the producer handoff.

## Authority boundary

`localApproval=approved`, `review_status=local_approved`, and
`awakeApproval=not_requested` are deliberately separate. No producer route may
serialize `awakeApproval=approved`, infer it from local review, or use a handoff
as a substitute for AWAKE runtime approval.

`workspace_id` and `revision` are required at the handoff route boundary. Missing,
invalid, or non-positive values fail before the session-authorized production
operation is issued.
