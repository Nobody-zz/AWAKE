# Plan: Worldbook Studio A4 CLI/Web semantic adapter boundary

> 状态：**`implemented`（2026-09-30 标记）。**
> 依据：本批所属子系统已存在：`tools/worldbook-studio/` 含 `Awake.WorldbookStudio.{Cli,Core,Launcher,Web}` 与 entity registry；本系列为该子系统的内部行为保持型重构批次。
> ⚠️ 本文件是**批次施工单**，其所属子系统已存在 ⇒ 本批视为已完成。**保留本文件**（不是 `superseded`）——
> 它记录了当时的设计意图与边界，仍有追溯价值。
> 现行方向：`AWAKE-ROADMAP.md`；分诊依据：`AUDIT-PLAN-TRIAGE-20260930.md`。


_Locked candidate plan; independent read-only review converged at Round 6 with `VERDICT: APPROVED`; implementation may begin under the acceptance contract below._

## Goal

在不改变 CLI 命令、参数、退出码、Web 路由、HTTP 状态、错误码、错误消息、AI Consent/Session/CSRF/CAS 生命周期或 CLI snake_case/Web camelCase wire 契约的前提下，消除 CLI 与 Web 对 `SuggestionEnvelope` 语义字段选择的重复权威，并用真实 CLI/Web smoke 与版本化 wire golden 证明边界保持不变。

## Evidence and decision

- Existing A1 evidence proves only partial semantic alignment: the wire golden has seven shared semantic names and the smoke compares four values. A4 therefore adds an independent eleven-field semantic fixture and full-value assertions rather than claiming existing A1 evidence is sufficient.
- `Program.cs` in CLI and Web each independently select the same eleven semantic values from `SuggestionEnvelope`; only the output property casing differs. Consent token, session, CSRF, apply nonce, revision and CAS paths are not equivalent and remain separate.
- The selected seam is an internal Core projection consumed by friend assemblies; it is not a wire DTO and adds no public type or route. Rejected alternative: merge CLI/Web anonymous wire objects, because that would erase the casing contract. Rejected alternative: merge Consent/Session/CAS state, because lifecycle and security ownership differ.

## Scope

1. Add one pure internal Core projection: `SuggestionSemanticProjection.Project(SuggestionEnvelope)` returning all eleven semantic values currently selected by both adapters, without file, network, process, time or global state access.
2. Add narrowly scoped `InternalsVisibleTo` declarations in `src/Awake.WorldbookStudio.Core/Properties/AssemblyInfo.cs` for these exact simple assembly names only: Core `Awake.WorldbookStudio.Core` remains the owner; CLI `worldbook-studio`; Web `Awake.WorldbookStudio.Web`; tests `Awake.WorldbookStudio.Tests`. The friend attribute is assembly-wide authorization and is tested as such; the projection remains internal and no public API is added.
3. Replace only the field-selection portion of CLI/Web `ToSuggestionDto`; keep separate anonymous objects and exact property casing/order: CLI `candidate_text`, `review_only`, `apply_nonce`, `suggestion_hash`; Web `candidateText`, `reviewOnly`, `applyNonce`, `suggestionHash`.
4. Add `tests/fixtures/a4-cli-web-contract-golden.v1.json` with eleven semantic field names, full CLI/Web wire field arrays, exact friend assembly names, public Core type baseline, CLI command baseline, Web route baseline, 21 schema file hashes, and reproducible named-case manifest values. The fixture is hand-captured before implementation and never regenerated at test runtime.
5. Add exactly three named harness cases: semantic projection full-value golden, wire casing/field-order and API-boundary ownership, and CLI/Web smoke coverage. Extend the existing smoke with an explicit `-Extended` mode so the default A1 smoke remains unchanged while A4 exercises positive/negative apply/reject, invalid CSRF/session/consent, wrong nonce/CAS, HTTP status, CLI exit code, error code and safe message.

## A4 fixed baselines

The following literals are part of the A4 review and implementation contract. They are not generated from the post-change source at test runtime. `a4-cli-web-contract-golden.v1.json` is the sole A4 expected-value authority: it contains the complete independent envelope, all eleven projected values, wire arrays, metadata baselines, schema hashes and smoke case expectations. Tests and smoke must read these values from that fixture; no duplicate hand-written expected value is allowed in code.

### Named-case manifest

Add these exact source lines, in this order, to `tests/Awake.WorldbookStudio.Tests/Program.cs`:

```text
Run("A4 semantic suggestion projection golden", () =>
Run("A4 CLI/Web wire casing and ownership", () =>
Run("A4 CLI/Web smoke coverage", () =>
```

The manifest reader selects lines whose first non-whitespace characters are `Run("`, preserves complete line text including indentation, joins lines with one LF, encodes UTF-8 without BOM, and hashes with SHA-256. The three lines must be appended immediately after the existing `A3.4 registry wiring and determinism` case and immediately before the final `Console.WriteLine` in `tests/Awake.WorldbookStudio.Tests/Program.cs`; no earlier insertion or interleaving is allowed. The A4 fixture records the three exact lines above, the pre-change count `96`, the pre-change hash `e24d6fc8df2e16966eecf4538a385b068445b792b51b638a16937c94a82b6879`, the post-change count `99`, and the post-change hash `ab72827684cd83cb44714162097ef459be8229774df9b47a64a0e4f4e1756918`. To verify the pre-change baseline, remove only those three exact lines from the post-change source before hashing; no other filtering or normalization is allowed.

### CLI command baseline

The exact command switch list, in source switch order, is:

```text
init
validate
compile
preview
export
ai-providers
ai-consent-preview
ai-analyze
ai-apply
ai-reject
doctor
```

The A4 fixture stores this list and the default/unknown-command exit-code baseline (`doctor` = `0`, invalid command = `3`). A4 does not add, remove, rename, or reorder a command.

### Web route baseline

The exact method/path list, in registration order, is:

```text
GET /health
GET /api/health
POST /api/launcher/shutdown
GET /api/workspace
GET /api/documents
GET /api/document
GET /api/editor-document
GET /api/editor-catalog
POST /api/validate
POST /api/compile
POST /api/save-authoring
POST /api/save-editor-document
POST /api/document/new
POST /api/export
GET /api/confirmation-token
GET /api/preview
POST /api/ai/session/bootstrap
GET /api/ai/providers
GET /api/ai/provider-settings
PUT /api/ai/provider-settings
POST /api/ai/consent-preview
POST /api/ai/analyze
POST /api/ai/apply
POST /api/ai/reject
```

The A4 fixture stores this ordered list. The projection change must not edit route registration or status/error mapping.

### Smoke evidence file

The AWAKE root is the parent of the `tools` directory containing the Studio root: from the script's `$root` (`...\\AWAKE\\tools\\worldbook-studio`), the archive directory is `Path.GetFullPath((Join-Path $root '..\\..\\docs\\evidence'))`. The extended smoke writes its temporary evidence to the caller-provided `<TEMP>\\a4-cli-web-smoke.json` and, whether cases pass or fail, writes an evidence object there and then copies the exact raw UTF-8-without-BOM, LF-terminated JSON bytes to the fixed repository evidence path `docs/evidence/WORLDBOOK-STUDIO-A4-CLI-WEB-20260823-smoke.json`. The evidence object has exactly these top-level keys:

```text
schema_version
batch_id
started_at_utc
finished_at_utc
working_directory
command
release_artifacts
fake_worker_artifact_path
fake_worker_artifact_sha256
fake_worker_dll_sha256
cases
cleanup
game_directory_touched
real_provider
real_worker
```

`schema_version` is exactly `awake.worldbook.studio.a4-cli-web-smoke.v1`; `release_artifacts` is an ordered array of `{ role, path, sha256 }` for the exact relative paths `src\\Awake.WorldbookStudio.Core\\bin\\Release\\net10.0\\Awake.WorldbookStudio.Core.dll`, `src\\Awake.WorldbookStudio.Cli\\bin\\Release\\net10.0\\worldbook-studio.dll`, and `src\\Awake.WorldbookStudio.Web\\bin\\Release\\net10.0\\Awake.WorldbookStudio.Web.dll`; `cases` is an ordered array of exactly 18 objects `{ id, boundary, operation, expected_exit_code, actual_exit_code, expected_http_status, actual_http_status, expected_error_code, actual_error_code, expected_safe_message, actual_safe_message, passed }`; and `cleanup` is `{ web_process_stopped, worker_process_stopped, temp_workspace_removed }`. Every case object always contains all four expected/actual groups; a non-applicable exit/status/error/message value is explicitly JSON `null`. CLI error output is parsed as `actual_error_code` equal to the prefix before the first `:`, and `actual_safe_message` equal to the complete `error` string (CLI has no separate `message` property). The artifact hash algorithm is lowercase SHA-256 over raw file bytes. `game_directory_touched`, `real_provider`, and `real_worker` must all be `false`.

### Extended smoke expected outcomes

The evidence `cases` array must contain these 18 IDs in this exact order and exact expectations. The same objects, including explicit JSON `null` values for all non-applicable groups, are stored under `smoke_cases` in the A4 fixture and must be read from there by the smoke; the text below is the review-readable expansion of that fixture data:

```text
cli-invalid-provider: exit=3, http=null, error=WB-AI-PROVIDER-400, safe="WB-AI-PROVIDER-400: Provider 必须是 local 或 cloud。"
cli-consent-success: exit=0, http=null, error=null, safe=null
cli-analyze-success: exit=0, http=null, error=null, safe=null
cli-apply-success: exit=0, http=null, error=null, safe=null
cli-reject-success: exit=0, http=null, error=null, safe=null
cli-wrong-nonce: exit=3, http=null, error=WB-AI-CAS-409, safe="WB-AI-CAS-409: 编辑器缓冲区已变化或建议已失效。"
cli-wrong-cas: exit=3, http=null, error=WB-AI-SUGGESTION-404, safe="WB-AI-SUGGESTION-404: AI 建议不存在或已失效。"
web-session-bootstrap: exit=null, http=200, error=null, safe=null
web-invalid-csrf: exit=null, http=403, error=WB-AI-CSRF-403, safe="请求无法完成，请检查档案校验结果。"
web-invalid-session: exit=null, http=403, error=WB-AI-CSRF-403, safe="请求无法完成，请检查档案校验结果。"
web-invalid-consent: exit=null, http=404, error=WB-AI-CONSENT-404, safe="请求无法完成，请检查档案校验结果。"
web-invalid-provider: exit=null, http=400, error=WB-AI-PROVIDER-400, safe="请求无法完成，请检查档案校验结果。"
web-consent-success: exit=null, http=200, error=null, safe=null
web-analyze-success: exit=null, http=200, error=null, safe=null
web-apply-success: exit=null, http=200, error=null, safe=null
web-reject-success: exit=null, http=200, error=null, safe=null
web-wrong-nonce: exit=null, http=409, error=WB-AI-CAS-409, safe="请求无法完成，请检查档案校验结果。"
web-wrong-cas: exit=null, http=404, error=WB-AI-SUGGESTION-404, safe="请求无法完成，请检查档案校验结果。"
```

For success cases the smoke also asserts the existing success payload markers (`ok`, `buffer_id`/`bufferId`, `suggestions`, `rejected`) without adding new wire fields. Each case is executed inside a capture wrapper that records `passed=false` and actual values on failure, continues collecting the remaining cases, always runs process/temp cleanup, writes both evidence files, and only then returns a non-zero exit code. The expected values above are captured before implementation and compared to actual evidence values; checking only that a case produced output is insufficient.

### Public Core type baseline

The fixture stores the following ordinal-sorted public top-level type names from `Awake.WorldbookStudio.Core` before implementation; nested types are excluded and any public type drift fails A4:

```text
AssistanceAnalysisKind
AssistanceAnalysisNames
AssistanceBinding
AssistanceFocusKind
AssistanceFocusMapping
AssistanceProviderFactory
AssistanceRequest
AssistanceRequestSerializer
AssistanceResult
AssistanceResultParser
AssistanceService
AssistanceSuggestion
AtomicCandidatePublisher
AuditLedgerService
AuthoringDocumentFile
AuthoringDocumentSummary
AuthoringEditorProjection
CanonicalJson
CompileResult
ContractHashing
Diagnostic
DocumentBuffer
DocumentCas
Hashing
IAssistanceProvider
IdentitySnapshot
JsonPatchEngine
KnowledgePatch
KnowledgePatchOperation
LocalWorkerProvider
OpenAICompatibleCloudProvider
OpenAICompatibleResponseParser
PreviewItem
PreviewResult
ProviderConfiguration
ProviderEndpointPolicy
ProviderSettingsStore
ProviderStatus
PublishFaultPoint
RegistryService
RegistrySnapshot
RuntimePackageCompilation
RuntimePackageCompiler
SafeYamlLoader
SchemaValidator
SnapshotInputFile
SnapshotInputStore
SnapshotReadInventoryEntry
SourceRegistryService
StoredCloudProviderSettings
StudioBootstrap
StudioBootstrapCodec
StudioBootstrapValidation
StudioHealth
StudioRuntimeConstants
StudioRuntimeHashing
StudioSettings
SuggestionEnvelope
SuggestionStore
ValidatedSnapshot
ValidationReport
WorkerHandshake
WorkerHandshakeResult
WorkspaceMarker
WorkspaceOptions
WorkspacePathPolicy
WorkspaceReadEvent
WorkspaceReadProbe
WorkspaceReadStage
WorkspaceRootGuard
WorkspaceService
WorkspaceWritePolicy
WorldbookApplicationService
WorldbookInputNormalization
```

### Schema byte baseline

The fixture root is `docs/worldbook-studio-plan`. The complete relative file list is:

```text
ai-consent.v1.schema.json
assistance.request.v1.schema.json
assistance.result.v1.schema.json
audit-event.v1.schema.json
author-diagnostics.v1.schema.json
awake.worldbook.authoring.v1.schema.json
content-graph.v1.schema.json
current-pointer.v1.schema.json
fixture-F12-atomic-pointer.json
fixture-F15-v1-boundary.json
id-ledger.v1.schema.json
knowledge-patch.v1.schema.json
npc-preview.v1.schema.json
preview-fixture.v1.schema.json
profile-registry.v1.json
profile-registry.v1.schema.json
provider-status.v1.schema.json
referral-registry.v1.json
referral-registry.v1.schema.json
source.registry.v1.schema.json
suggestion-envelope.v1.schema.json
```

For every listed file, read the original file bytes relative to that root, without parsing, reserialization, newline conversion, trimming, or BOM insertion, and store the lowercase SHA-256 of those bytes. The test compares both the exact path set and every hash; an extra or missing JSON file fails A4.

### Friend assemblies and fake Worker artifact

The exact `InternalsVisibleTo` simple names are `worldbook-studio`, `Awake.WorldbookStudio.Web`, and `Awake.WorldbookStudio.Tests`; no wildcard or additional friend assembly is permitted. A4's smoke uses the existing deterministic local PowerShell fake Worker, not a Worker DLL and not a real Worker. The script is materialized at the smoke temp root as `worker.ps1`; evidence records `fake_worker_artifact_path` relative to the temp root and `fake_worker_artifact_sha256`, computed over its raw UTF-8-without-BOM bytes before launch. `fake_worker_dll_sha256` is explicitly `null` because no DLL is used. The smoke evidence must also record the Release CLI/Web/Core DLL paths and lowercase SHA-256 values computed over their raw file bytes.

## Acceptance contract

- `SuggestionSemanticProjection.Project` is one internal static pure authority with one call in CLI and one call in Web; no `File`, `Directory`, `HttpClient`, process, time or environment dependency.
- All eleven semantic values (`id`, `kind`, `severity`, `confidence`, `title`, `reason`, `candidate_text`, `patch`, `review_only`, `apply_nonce`, `suggestion_hash`) are compared by value against an independent in-memory envelope fixture. CLI and Web serialized fields retain their distinct A1 field order/casing; the shared projection never serializes a wire object.
- Existing CLI commands (`ai-consent-preview`, `ai-analyze`, `ai-apply`, `ai-reject`) retain exit codes, error codes and safe messages; existing Web AI routes retain HTTP statuses, error codes and safe messages for valid/invalid CSRF, session, consent, nonce and CAS paths.
- `scripts/a1-authority-smoke.ps1 -Extended -EvidencePath <TEMP>` is the authoritative real-boundary evidence command: real CLI/Web processes plus a deterministic local fake Worker. It must cover CLI invalid Provider, CLI consent/analyze, CLI apply success, CLI reject success, CLI wrong nonce/CAS, Web session bootstrap, invalid CSRF, invalid consent/session, Web consent/analyze, Web apply/reject, Web wrong nonce/CAS, and HTTP status, CLI exit code, error code and safe-message assertions. It records the prerequisite Release DLL hashes and cleans Web, fake Worker and temporary workspace/processes in `finally`; it does not claim real cloud Provider or real Worker compatibility.
- Harness count increases from `96` to `99`; the fixed manifest `tests/fixtures/a4-cli-web-contract-golden.v1.json` records baseline `96` / `e24d6fc8df2e16966eecf4538a385b068445b792b51b638a16937c94a82b6879` and target `99` / `ab72827684cd83cb44714162097ef459be8229774df9b47a64a0e4f4e1756918`. The hash is SHA-256 over UTF-8 `LF`-joined named `Run("...")` lines in source order; A4 rows are removed before checking the old baseline.
- The same fixture's public Core type list, CLI command list, Web route list and 21 schema hashes must match exactly; any public metadata, command, route or Schema drift fails A4.
- Release build remains `0 warnings / 0 errors`; 21 contract JSON files parse; existing package `release-check` passes; A4 debt audit has `confirmed=0`; no package rebuild, game start, real Provider/Worker or sync.

## Tests

1. **A4 semantic projection golden**: construct an independent A4 in-memory `SuggestionEnvelope` fixture with values different from the A1 smoke fixture, compare all eleven projected fields with hand-written fixture values (never values read from CLI/Web adapters or A1 expected output), assert the projection is internal/pure by source scan and reflection, and assert the projection does not contain wire casing.
2. **A4 CLI/Web wire casing and ownership**: inspect both adapter source files, the immutable A1 fixture, and the independent A4 fixture; assert exactly one semantic projection call per adapter, exact eleven-field snake/camel arrays and order from A4, exact public Core/command/route/schema/friend baselines from A4, and no shared anonymous wire DTO. A1 is consulted only to prove its old golden remains immutable.
3. **A4 CLI/Web smoke coverage**: assert the versioned smoke source contains the exact command `pwsh -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\a1-authority-smoke.ps1 -Extended -EvidencePath <TEMP>` and the required positive/negative case names, then execute the equivalent command with a real temporary evidence directory as the authoritative real-boundary check. The test must inspect the evidence JSON and fail if any required case, status, exit code, error code, safe message, Release DLL hash or cleanup result is absent.

## Change budget and risks

- Production: one Core projection file, one assembly friend declaration file, two adapter call-site edits, and the `-Extended`/`-EvidencePath` branch plus evidence writer in the existing smoke script; no route or command registration edits.
- Test/fixture: exactly three named cases plus one independent A4 contract golden; existing A1 wire golden remains immutable. The public/command/route/schema lists are fixed in that golden and compared by tests.
- Risk: assembly-name mismatch for the CLI single-file assembly or accidental wire property reordering. Mitigate with exact simple-name metadata, reflection plus compile-time use, full eleven-value assertions, source ownership scans, field-order assertions and real smoke.

## Non-goals

- Do not merge CLI/Web wire DTOs, Provider settings, error status maps, Token/Nonce generation, ConsentStore, WebAiSessionStore, CSRF, SuggestionStore, DocumentCas or revision handling.
- Do not change Schema, CLI command names/arguments, HTTP routes, AI Provider/Worker contracts, Launcher, UI, AWAKE runtime, `Modules\\AWAKE`, `PlayerExports`, dist or frozen candidates.
