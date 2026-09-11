# Plan Review Log: Persona Workbench Free Preview

Act 1 (grill) complete — plan locked with the user on 2026-08-18. `MAX_ROUNDS=5`.

## Locked decisions

- First delivery is a standalone local Free Preview; AWAKE authoring follows as an adapter after feedback.
- Tool is a local Windows entry point hosting a browser UI on `127.0.0.1`; no default network listener or telemetry.
- AI is a core assistance path, but is provider-configured, explicit per action, and only creates drafts.
- Two modes share a schema/DSL core: free experimentation with generic tags, and AWAKE authoring with authoritative AWAKE tags and stable CharacterId constraints.
- AWAKE source overrides are one-character-one-file under an overrides directory; draft and approved states are explicit, conflicts are rejected.
- Long-lived persona is separate from runtime identity; current kingdom/family/role remains runtime context or preview sample.
- Local write safety includes per-file history, temp-then-validate-then-replace, one writable instance, and external-change detection.
- 429 handling is a quiet reliability feature, not a product claim.

## Act 2

### Round 1 — Independent read-only review

Review transport note: the Windows Store `apply_patch` wrapper could not launch its bundled CLI because the app resource executable returned access denied, and the desktop execution policy blocked nested `codex exec`. A separate read-only review agent was used instead; it made no file changes.

- P0: loopback binding alone does not establish a local-session boundary; require high-entropy startup token, exact Host/Origin checks, CSRF checks, CORS denial, and loopback peer verification.
- P0: arbitrary compatible endpoints can become SSRF/data-exfiltration paths; allow only HTTPS or verified loopback, disable redirects/proxy inheritance, recheck DNS results, and confirm every non-loopback target.
- P0: key storage needed a DPAPI, frontend/command-line exclusion, and redaction contract.
- P0: the proposed AWAKE overrides directory was not read by the current manifest/loader; add a versioned manifest/loader override contract before writing it.
- P0: current unsupported schema handling could leave a definition selectable; require hard rejection or migration before approval/runtime selection.
- P1: define an ownership key and reject overlapping approved overrides instead of relying on priority.
- P1: specify resource limits, unknown-field policy, escaping, and isolation for AI JSON.
- P1: define same-volume flush/replace, locking, journal recovery, and OneDrive conflict handling.
- P1: add package manifest/release exclusions and security/recovery integration tests.

VERDICT: REVISE

### Plan response

Accepted all findings. The plan now defines local session boundaries, endpoint and secret handling, AI payload limits, manifest/loader prerequisites for overrides, hard schema rejection, ownership conflict rules, journaled atomic writes, package exclusions, and dedicated security/recovery tests. A second read-only review is required.

### Round 2 — Independent read-only review

- P0: the old fallback wording could still permit plaintext keys; fail closed if DPAPI cannot protect a persistent key.
- P1: bootstrap token delivery in a URL can leak; use fragment-only, one-time cookie exchange, URL cleanup, and no-referrer.
- P1: apply untrusted-text rendering and CSP to all persisted/imported sources, not only AI output.
- P1: add a pre-replace content-hash check and conflict-draft outcome for OneDrive races.
- P1: canonicalize and fence manifest paths, reject reparse points, and test traversal/junction escapes.

VERDICT: REVISE

### Plan response

Accepted all findings. Persistent keys now fail closed without DPAPI; the session bootstrap is one-time and URL-safe; rendering treats every persisted/imported source as untrusted; writes require a final content-hash precondition; and adapter directory fencing explicitly covers canonical paths, traversal, and reparse points. A third read-only review is required.

### Round 3 — Independent read-only review

- P0: a cookie is scoped by host rather than port, so it can be replayed by another loopback service; do not use cookies as the workbench capability.

VERDICT: REVISE

### Plan response

Accepted. The bootstrap exchange now returns a short-lived token bound to the server instance; it is held only in the launched page's origin-scoped memory/session storage and must be sent in a custom header. Cookie-only authentication and cross-instance token use are explicitly rejected and tested. A fourth read-only review is required.

### Round 4 — Independent read-only review

The local-session capability no longer relies on cookies: it is instance-bound, short-lived, origin-scoped, header-required, and explicitly tested against cookie-only and cross-instance replay. The revised plan also closes the previously material DPAPI, endpoint, schema, rendering, recovery, path-fence, and packaging-test gaps.

VERDICT: APPROVED

## Resolution

Plan approved after four independent read-only review rounds. No implementation code was written during Grill Act 1 or Act 2. Awaiting explicit user sign-off before beginning the bounded Free Preview implementation tasks.
