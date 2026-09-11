# AWAKE Customer Delivery — Batch 8

- Batch: `AWAKE-CUSTOMER-DELIVERY-20260904`
- Parent: `AWAKE-WORLDBOOK-STUDIO-REPAIR-SEQUENCE-20260902`
- Risk: `security-release`
- Status: `planned_after_batch_7`

## Goal

Produce a reproducible customer package with honest evidence boundaries and a complete first-use workflow.

## Scope

1. Build all deliverable tools in Release configuration with zero warnings/errors.
2. Validate JSON, XML, schemas, manifests, DLL hashes, and package contents.
3. Package UI Workstation, Worldbook Studio, Persona Workbench, documentation, and recovery guidance.
4. Run offline and configured-local-Worker acceptance matrices.
5. Separate automated evidence, package evidence, user-run application evidence, game-run evidence, and unverified items.
6. Produce rollback, troubleshooting, and known-limitations documentation.

## Non-goals

- No automatic Bannerlord launch.
- No automatic enabling/disabling of modules.
- No claim of in-game success without user-provided runtime logs.
- No overwriting of historical packages or release archives.

## Acceptance

- Package opens from a clean machine/workspace path.
- Customer can create, review, save, reopen, recover, and export a worldbook draft.
- No Worker/configuration state fails silently.
- Package and source hashes match all declared delivery targets.
- Documentation matches actual routes, statuses, file locations, and recovery behavior.
- Final handoff lists verified, user-verified, and unverified items separately.

## Locked Delivery Contract

The authoritative delivery root is
`AWAKE/artifacts/customer-delivery/awake-customer-<build_id>/`; the sibling
delivery archive is
`AWAKE/artifacts/customer-delivery/awake-customer-<build_id>.zip`. The builder is
`AWAKE/tools/customer-delivery/package-customer.ps1`; it accepts only
`-OutputRoot`, `-RunOffline`, and `-RunWorker` and never writes `dist`, the game
directory, or historical archives in place. Each run creates a new build
directory and leaves older builds untouched.

Each component has exactly one authoritative customer startup entrypoint:
`ui-workstation/start-ui-workstation.ps1`,
`worldbook-studio/WorldbookStudio/Awake.WorldbookStudio.Launcher.exe`, or
`persona-workbench/PersonaWorkbench/PersonaWorkbench.Launcher.exe`.
`ui-workstation/stop-ui-workstation.ps1` is the paired UI shutdown command;
README files describe entrypoints but are not entrypoints. The customer starts
Worldbook Studio and Persona Workbench independently; UI Workstation only
orchestrates navigation and never starts either domain process. The sole UI
customer entrypoint is `ui-workstation/start-ui-workstation.ps1`;
`stop-ui-workstation.ps1` is its paired shutdown command, while
`UiWorkstation.Adapter.ps1` is an internal implementation detail invoked by
the start wrapper. Each package
directory contains its own executable/script, all required DLLs/assets, exactly
one `manifest.json`, exactly one `SHA256SUMS.txt`, and no PDB, secret, log,
temporary, or archive directory. The root package contains the three package
directories, exactly these `docs/` files:
`README.md`, `安装说明.md`, `故障排查.md`, `恢复与回滚.md`,
`本地Worker配置.md`, `已知限制.md`, and `证据边界.md`; and exactly these four
root metadata files: `delivery-manifest.json`, `SHA256SUMS.txt`,
`BUILD-ID.txt`, and `package-verification.json`. Each tool directory also
contains its own ZIP and `.sha256` sidecar, including a ZIP of the UI script
package; the root ZIP is a sibling of the expanded build directory and is not
copied into the expanded payload.

The root package contains exactly:

- `ui-workstation/` with `UiWorkstation.Adapter.ps1`, start/stop instructions,
  and its manifest;
- `worldbook-studio/` with the self-contained WBS package directory and ZIP;
- `persona-workbench/` with the self-contained PWB package directory and ZIP;
- `docs/` with one unified customer README, installation, troubleshooting,
  recovery, rollback, Worker setup, known limitations, and evidence boundary;
- `delivery-manifest.json`, `SHA256SUMS.txt`, `BUILD-ID.txt`, and
  `package-verification.json`.

The root `delivery-manifest.json` schema is
`awake.customer.delivery-manifest.v1` and has required fields
`build_id`, `generated_at_utc`, `source_revision`, `tools`, `documents`,
`files`, `package_sha256`, `zip_sha256`, `metadata_sha256`,
`rollback_pointer_sha256`, and `verification`.
`build_id`,
`generated_at_utc`, and `source_revision` are strings; `tools`, `documents`,
and `files` are arrays; `package_sha256` and `zip_sha256` are lowercase
64-hex strings; `metadata_sha256` is an object with required lowercase
64-hex fields for `delivery-manifest.json`, `SHA256SUMS.txt`, `BUILD-ID.txt`,
and `package-verification.json`; `rollback_pointer_sha256` is the lowercase
64-hex SHA-256 of the external `rollback-pointer.json` raw UTF-8 bytes;
`verification` is an object with required
boolean fields
`source_bound`, `tree_bound`, `zip_bound`, `extracted_bound`, `offline_passed`,
`local_worker_passed`, and `ready_for_user_verification`.
Each tool entry has required string fields `tool_id`, `source_manifest_sha256`,
`package_root`, `package_tree_sha256`, `zip_path`, `zip_sha256`, `entrypoint`,
and `status`, where `tool_id` is one of `ui_workstation`, `worldbook_studio`,
or `persona_workbench`, and `status` is `verified` or `unverified`.
Each document entry has `path`, `sha256`, and `status`, where `status` is one of
`verified`, `unverified`, or `not_run`; each file entry has `path`, `sha256`,
and integer `length`. `package_sha256` is the expanded root payload tree hash;
`zip_sha256` is the canonical ZIP content hash (sorted
`<relative_path>|<length>|<sha256>` records, excluding manifest metadata), and
the sibling `.sha256` sidecar is the sole authority for the raw-byte hash of
`../awake-customer-<build_id>.zip`. `package-verification.json.zip_file_sha256`
is `null` because embedding a raw ZIP hash inside that ZIP would be recursive.
File paths are relative to the delivery root,
use `/`, and are sorted by ordinal UTF-8 path comparison. `SHA256SUMS.txt`
contains the same `files` set in the same order and excludes itself,
`delivery-manifest.json`, `BUILD-ID.txt`, `package-verification.json`,
`rollback-pointer.json`, and all ZIP files; their hashes are represented by
`metadata_sha256`, `rollback_pointer_sha256`, or the dedicated package/ZIP
fields. `rollback_pointer_sha256` is immutable build provenance captured when
the build is created; it is not used to validate an archived build after the
external pointer changes. To avoid self-reference,
`metadata_sha256.delivery-manifest.json` is the SHA-256 of the canonical
manifest with that one value set to `null`; all other metadata hashes are over
their exact UTF-8 bytes.

The build ID is
`<UTC yyyyMMdd-HHmmssfff>-<first 12 lowercase hex of the SHA-256
of the canonical source-manifest tuple>-<8 lowercase random hex>`. The source
manifest tuple is the three records sorted by `tool_id`, each encoded as
`tool_id|source_manifest_sha256` and joined with LF without a trailing LF,
then UTF-8 hashed. Before writing any output, the builder checks that the
calculated build ID does not already exist as a directory, ZIP, manifest, or
rollback pointer target; a collision fails with `WB-DELIVERY-409` and never
overwrites or regenerates silently. The package tree hash is the SHA-256 of sorted LF records
`<relative_path>|<length>|<sha256>` using ordinal path comparison, `/` paths,
UTF-8, and no trailing LF; it excludes each package's manifest/sums, root
metadata, ZIPs, and archive directories. ZIP entries are compared after
normalizing `/`, rejecting duplicate names, and ignoring timestamps/permissions;
the extracted tree uses the same record algorithm. The builder verifies source
manifest → package tree → ZIP → extracted tree and fails on any missing,
extra, duplicate, changed, symlink, PDB, secret, log, or temporary file.

PWB delivery uses a ZIP plus its `.sha256`, matching the documentation. WBS
delivery uses its existing self-contained ZIP plus `.sha256`. UI Workstation
is a script package with no hidden dependency and is also delivered as a ZIP
plus `.sha256`; its internal adapter is
`ui-workstation/UiWorkstation.Adapter.ps1`, while its only customer entry is
`ui-workstation/start-ui-workstation.ps1`. The root README states that the
local Worker is an external prerequisite, names the loopback endpoint and
model configuration, and distinguishes `not_configured`, `ready`, `timeout`,
`transport_error`, `unknown`, and `needs_review`.

Rollback is represented by the single mutable pointer
`AWAKE/artifacts/customer-delivery/rollback-pointer.json`, not by a pointer
inside a build directory or customer ZIP. Its strict schema is
`{schema_version:"awake.customer.rollback-pointer.v1", current_build_id,
previous_build_id:string|null, current_root, previous_root:string|null,
updated_at_utc}` with relative roots and RFC 3339 UTC timestamps. The builder
writes it through a temp file plus atomic replace only after all package and ZIP
checks pass; an empty previous build is represented by `null`. A damaged or
hash-mismatched pointer fails closed and leaves the current package untouched.
`AWAKE/tools/customer-delivery/rollback-customer.ps1 -BuildId <id>` resolves
only a verified archived build, atomically updates the pointer, rewrites the
current deployment `package-verification.json`, and reruns the target
manifest/tree/ZIP checks without comparing the target's immutable
`rollback_pointer_sha256` to the newly updated pointer. No current or historical
build is overwritten.
Offline acceptance uses
a fresh temporary user/workspace directory and proves start, health, edit,
save, reopen, recovery, export, and no-Worker degradation. Configured Worker
acceptance is a separate run using the final WBS ZIP and real local
`awake.worker.v1`; it records handshake, analyze, candidate review, save,
readback, and `review_only` preservation. No test may start Bannerlord or touch
the game directory.

Evidence is written to
`AWAKE/docs/evidence/AWAKE-CUSTOMER-DELIVERY-<execution_utc_date>[-rN].json`
with separate
`automated`, `package`, `offline_customer`, `local_worker`, `user_verified`,
`game_runtime`, and `unverified` sections. The evidence schema is
`awake.customer.delivery-evidence.v1`; each section has `status` from
`passed`, `failed`, `not_run`, or `unverified`, a `command`, `inputs`, and
`cases` array. Offline cases use a deterministic fixture, a fresh directory
named `awake-customer-offline-<guid>`, 30-second health timeout and 20-second
request timeout; Worker cases record endpoint `http://127.0.0.1:11434`,
protocol `awake.worker.v1`, model ID, handshake/analyze hashes, and no secret.
A section is not promoted to package `verified` merely because another section
passed. The final status is
`ready_for_user_verification` until a user runs the package from a clean
directory and supplies the result.

Each `verification` boolean in `delivery-manifest.json` is derived only from
the matching evidence section and package/build hash: `offline_passed` is true
only for `offline_customer.status=passed`, `local_worker_passed` is true only
for `local_worker.status=passed`, and `ready_for_user_verification` is true
only when package checks pass while `user_verified.status` remains
`not_run` or `unverified`.

The evidence sections map to the AWAKE evidence levels as follows:
`automated=E1`, `package=E3`, `offline_customer=E2` plus `E3` when package
hashes are bound, `local_worker=E2` plus `E3` when the final package is bound,
`user_verified=E4` only after a user runs the matching BuildId package through
one customer startup entrypoint and completes the first-use workflow;
`game_runtime=E4` requires user-provided logs for that matching BuildId showing
the game-facing entry and one observed workflow, while `game_runtime=E5`
additionally requires the same BuildId to pass a user-observed save/load cycle
and a documented long-run regression of at least 30 minutes. `game_runtime`
cannot be E5 without both E4 evidence and the save/load/long-run artifacts, and
`unverified=E0`. The highest claimed level is the highest section actually
passed; an untouched game directory cannot produce E4 or E5.

All date-bearing filenames use the execution date in UTC, while
`generated_at_utc` is the authority for chronology. A rerun on a later UTC date
creates a new evidence filename and build ID; a rerun on the same date creates
a new build ID and the next unused evidence suffix `-rN`, starting at `-r1`.
The batch identifier remains `AWAKE-CUSTOMER-DELIVERY-20260904` and is not
changed retroactively. Evidence generated after September 4, 2026 is valid
when its `generated_at_utc`, build ID, package hashes, and command inputs are
recorded; it must not be presented as captured on September 4, 2026.
