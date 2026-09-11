# Plan Review Log: Worldbook Studio A4 CLI/Web semantic adapter boundary

## Round 1 — Independent read-only review

`VERDICT: NOT APPROVED`

Material findings accepted:

- Existing A1 smoke did not compare all eleven semantic values or provide a complete byte-level semantic proof. The revised plan adds an independent eleven-field fixture and full value assertions, and no longer overstates A1 evidence.
- Existing smoke did not cover apply/reject, invalid CSRF/session/consent, wrong nonce or CAS status. The revised plan adds an `-Extended` smoke mode with explicit positive and negative CLI/Web lifecycle checks while leaving default A1 smoke behavior unchanged.
- Friend assembly identity was not fixed. The revised plan records exact simple names: `worldbook-studio`, `Awake.WorldbookStudio.Web`, and `Awake.WorldbookStudio.Tests`, and identifies the Core `AssemblyInfo.cs` owner.
- Named-case manifest/hash was not reproducible. The revised plan fixes fixture path, baseline/target counts and hashes, case IDs, source-order and UTF-8 LF-join algorithm.
- Public API, CLI command, Web route and Schema non-drift lacked independent protection. The revised plan adds exact public type, command, route and 21-schema-hash baselines to the A4 contract golden.
- The smoke Worker is a deterministic local fake Worker, not a real external Worker. The revised plan names the evidence precisely and records Release DLL prerequisites and cleanup boundaries.

## Revision status

The revised plan is awaiting a second independent read-only review. No A4 production code has been changed.

## Round 2 — Independent read-only review

`VERDICT: NOT APPROVED`

Material findings accepted for the next revision:

- The semantic test must use a separately captured A4 in-memory envelope fixture rather than reusing A1 expected values. The plan now requires an independent eleven-field fixture with full-value assertions and forbids deriving expected values from the adapter implementation.
- The third named test must run the exact `pwsh -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\a1-authority-smoke.ps1 -Extended -EvidencePath <TEMP>` command and enumerate the CLI/Web positive and negative cases, status/exit-code/error-code/safe-message assertions. The plan now names this exact command and the complete required coverage.
- The 21 Schema paths, raw-byte hash algorithm, encoding and newline rules were not fixed. The plan now contains the complete relative path list rooted at `docs/worldbook-studio-plan` and requires lowercase SHA-256 over untouched bytes.
- The three A4 named case literals were not fixed. The plan now records all three exact `Run("...")` source lines, their order, the 96/99 counts, both hashes and the removal rule for reproducing the old baseline.
- The fake Worker hash boundary was ambiguous. The plan now explicitly records that the smoke uses a temporary PowerShell script, not a DLL; it records the raw UTF-8-without-BOM script SHA-256 and marks `fake_worker_dll_sha256` as null, while recording Release CLI/Web/Core DLL hashes separately.
- The command switch list, route list, public Core top-level type list and exact friend assembly names are now fixed literals in the plan and must be captured in the hand-written A4 fixture before implementation.

### Claude's response

All five findings are accepted. The plan was revised without changing the selected architecture or expanding the production seam. No A4 production code, fixture, smoke script, package, runtime module, or frozen candidate has been changed; re-review is required before implementation.

## Round 3 — Independent read-only review

`VERDICT: NOT APPROVED`

Material findings accepted:

- The wire ownership test did not explicitly read the independent A4 fixture. It now must compare A4's eleven-field CLI/Web arrays and all fixed metadata baselines; A1 remains immutable legacy evidence only.
- The smoke evidence contract lacked a fixed path and field structure. The plan now fixes the temporary filename, archive path, top-level keys, artifact/case/cleanup object shapes, raw-byte hash algorithm and false external-environment flags.
- The named-case hash did not fix insertion location. The plan now requires appending the three exact lines after the A3.4 final case and before the final harness summary.
- The smoke coverage list lacked exact expected status/exit/error/safe-message values. The plan now fixes every required CLI/Web case ID and expected outcome, including the existing default safe-message fallback for unmapped AI error codes.

### Claude's response

All four findings are accepted and recorded in the plan. The architecture, change budget and non-goals remain unchanged. No A4 production code or test fixture has been written; re-review remains the implementation gate.

## Round 4 — Independent read-only review

`VERDICT: APPROVED`

The reviewer confirmed that the A4 fixture ownership, fixed evidence path/schema, named-case insertion position/hash algorithm, exact smoke command, and per-case expected status/exit/error/safe-message contract are now sufficiently explicit to implement. No production code was changed during review.

## Round 5 — Independent read-only review

`VERDICT: NOT APPROVED`

Material findings accepted:

- The A4 fixture must be the sole expected-value authority, not merely a metadata fixture. The plan now requires the full independent envelope, all eleven projected values and all smoke expectations to be read from `a4-cli-web-contract-golden.v1.json`; duplicate hand-written expected values are prohibited.
- Evidence anchoring and encoding were incomplete. The plan now anchors the archive path to `AWAKE\\docs\\evidence` from the smoke `$root`, fixes the exact smoke schema version, exact Core/CLI/Web Release artifact relative paths, raw UTF-8-without-BOM/LF output, and exact case count/order.
- Every smoke case now declares all four expected/actual groups, including explicit JSON `null` for non-applicable values and the CLI error parsing rule.
- Failure evidence must survive individual assertion failures. The plan now requires per-case `passed=false` capture, continued case collection, unconditional cleanup, evidence writes, and only then a non-zero process exit.

### Claude's response

All four findings are accepted. The A4 fixture now contains `smoke_cases` with complete expectations and the independent envelope/projection values. The plan remains blocked until a fresh read-only re-review returns approval; the already-added RED tests are characterization work only and no A4 production code has been written.

## Round 6 — Independent read-only review

`VERDICT: APPROVED`

The reviewer confirmed that the A4 fixture is the sole expected-value authority, all 18 smoke cases have complete expectations with explicit nulls, evidence anchoring/schema/artifact paths/encoding and failure-collection boundaries are fixed, and the 21-schema/named-case/fake-Worker contracts remain reproducible. Implementation may proceed.
