# Plan: Marcus-Awake P3 Runtime / Provider / Storage Vertical Integration

_Current batch: P3A offline contract and deterministic vertical slice. The final objective remains full Marcus-Awake internal migration; this batch is a gate, not a completion claim._

## Contract anchors

- P1.5 contract: `_houkai_merge/AWAKE/docs/MARCUS-AWAKE-P1.5-CONTRACT-LOCK-DRAFT-20260824.md`, SHA-256 `DB45A9C4C130BC727394B61AFD9836508045F83B66BA7F23C0BF3686D596D619`.
- P2 evidence: `_houkai_merge/AWAKE/docs/evidence/MARCUS-AWAKE-FRAMEWORK-CORE-VERTICAL-SMOKE-E1-20260824.json`.
- Capability authority: `MARCUS-AWAKE-FULL-CAPABILITY-INVENTORY-v1-20260824.md`, `MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md`.
- Old Marcus AuthorSource and design documents are behavior references only; no unverified design statement is treated as implemented evidence.

## Version and evidence naming decision

- `MarcusAwakeFramework.Api` is a newly owned internal API major `2.0`. The old Marcus SDK `0.1` is a behavior/reference source only, not a compatibility target; P3A-E1 must prove the release assembly identifies as Framework API `2.0` and contains no old SDK reference.
- P1.5's phase-level `P3/E2` wording refers to the later integrated Runtime Service process/protocol smoke. This batch uses qualified evidence names: `P3A-E1` for compile/parse/static checks and `P3A-E2` for deterministic in-memory fixture execution. `P3A-E2` cannot satisfy the P1.5 integrated `P3/E2` gate; P3B/P3D must produce that later evidence.
- Governance records use one state: `plan_status=revised_for_review`, `review_status=pending` until an independent reviewer returns `APPROVED` for the current revision. The P1.5 phase-level `P3/E2` requirement remains separate and cannot be satisfied by P3A. The checkpoint must carry the same fields: `plan_status`, `review_status`, `user_signoff_required`, `primary_executor` and `minimum_evidence`; its lifecycle `status` may additionally be `needs_review`.

## Cross-document state mapping

The three governance artifacts describe different layers and are not required to share one literal status string. The machine mapping is fixed:

| Artifact | Status | Meaning | Allows P3A code? |
|---|---|---|---|
| P1.5 contract lock | `P1.5_CONTRACT_LOCKED` | authoritative contract fields and boundaries are frozen | only after the P3A plan is independently approved |
| Capability ownership map | `P1.5_CONTRACT_REVIEW_BASELINE` | canonical F-ID owner/fixture/phase inventory baseline | no implementation permission by itself |
| This P3A plan/checkpoint | `plan_status=revised_for_review`, `review_status=pending` | current batch is revised and awaiting independent approval | no; only `review_status=approved` unlocks P3A |

P1.5 and the ownership map remain unchanged anchors. Their status difference is intentional and is not a failed synchronization. The checkpoint and review log must mirror this plan's two fields exactly.

## Final direction

AWAKE will ship one internally owned `MarcusAwakeFramework` runtime. Players enable AWAKE only; the old Marcus module is not a runtime dependency. The game process owns safe Bannerlord reads, commands, session fences and player-facing configuration. A local Runtime Service owns HTTP, credentials, SQLite/RAG, provider adapters, streaming and large payloads. DevTools remains a separate developer surface.

## P3A minimum closed loop

```text
RuntimeService logical lifecycle
→ session-bound RuntimeGateway
→ route/profile resolution
→ bounded AiTaskRequest
→ neutral provider translation
→ accepted/started/delta/usage/terminal stream
→ typed ServiceReceipt (no game settlement in P3A)
→ scoped in-memory storage ledger
→ bounded keyword RAG query
→ redacted diagnostics
```

P3A is offline and deterministic. It must not perform HTTP, SQLite native I/O, Named Pipe I/O, file I/O, or call Bannerlord. Those real boundaries are P3B/P3C and require separate evidence.

## P3A scope and hard boundaries

Production source may contain only neutral contracts, typed models and pure validation/state transitions. Test fakes live only in the `MarcusAwakeFramework.Tests` assembly under a test-only namespace and are excluded from every release input.

The P3A source and compiled-reference scan rejects `HttpClient`, sockets, HTTP handlers, SQLite providers, `NamedPipe*`, `File`, `Directory`, `Process`, environment-variable secret reads, OS credential APIs, Bannerlord/TaleWorlds references and game-directory writes. P3A contains no API keys, authorization headers, provider DTOs, database paths or raw external payloads.

P3A does not expose MCM/Companion controls. There are no player-tunable runtime settings in this batch; MCM belongs to P4.

P3A receipts use `settlement_requirement=not_applicable` only. They are not `CommandLedgerEntry`, `SettlementReceipt`, save anchors or proof of a game effect.

## P3A behavior contract

1. The lifecycle fake is logical and in-memory: `Created → Starting → Ready → Draining → Stopped`; failed bounded drain enters `RecoveryRequired`. OS locks, parent proof, orphan/crash-loop handling and upgrade isolation are P3B only.
2. Every AI task carries `task_id`, `message_id`, `owner_id`, `campaign_guid`, `timeline_id`, `session_id`, `session_generation`, `correlation_id`, `causation_id`, `idempotency_key`, route/provider/profile, `settlement_requirement`, finite deadline, budgets and the session-owned cancellation token. `CancellationToken.None` is invalid. The existing `IdempotencyScope` remains the command-effect scope and is required only when `settlement_requirement=required`; the new `AiTaskScope` is the sole AI-task deduplication scope for both effect and non-effect requests and is added to `RequestContext.cs`.
3. Validation order is fixed: identity → active generation → owner/correlation → route/profile → settlement → canonical request fingerprint → existing `AiTaskScope` lookup → deadline → budgets → cancellation. Stale or cancelled tasks cannot emit success or write. The addendum below is the authoritative form of this order; no earlier abbreviated ordering is a second rule.
4. Route resolution is code-owned. Unknown route/provider capability, disabled egress, expired deadline, quota exhaustion and backpressure return stable typed errors. Provider DTOs never cross the game-facing API; provider wire types and backend adapters are P3D service/test scope, not Framework Core scope.
5. Stream events are `Accepted`, `Started`, `TextDelta`, `UsageUpdate`, `RouteChanged`, `Completed`, `Cancelled`, `Failed`. Per-task stream sequence, IPC sequence and durable event index remain separate; gaps, duplicates, post-terminal and stale-generation events are rejected/ignored with diagnostics.
6. Provider fixtures translate exact neutral requests for OpenAI-compatible, Anthropic and Ollama and normalize exact fixture responses. They report `fixture_only`/`unverified`; no live provider availability is claimed.
7. Prompt registration is owner/version/placeholder/schema/tool-allowlist bound. Invalid structured output is a typed failure; raw output is diagnostic-only.
8. Egress canonicalization uses the same deterministic algorithm family as task requests under a separate domain prefix `marcus-awake/egress/v1`: UTF-8 JSON, recursively sorted object keys, normalized Unicode NFC strings, LF line endings, no insignificant whitespace and no omitted nulls; duplicate object keys, NaN, Infinity and non-canonical numbers are rejected; integers use invariant decimal without leading zeros and finite decimals use invariant round-trip form. Arrays preserve order unless declared set-like; `allowed_field_ids[]`, `grant_rule_ids[]`, `archive_ids[]`, `entry_ids[]` and `allowed_domains[]` are sorted ordinal and deduplicated before hashing. The hash is `lowercase_hex(SHA-256(UTF-8("marcus-awake/egress/v1\n" + canonical_json)))`, so task and egress hashes cannot be confused.
9. Storage/RAG contracts carry only neutral query, provenance and scope envelopes. P3A's keyword-only in-memory fake is scoped by owner, campaign, timeline, session, collection/namespace, source/grant provenance, `identity_profile_hash`, `manifest_hash`, `corpus_fingerprint`, `grant_rule_ids[]`, policy/manifest epochs and `retrieval_mode=keyword` with `backend=memory_fake` and `semantic_available=false`. Worldbook-specific package/archive/entry fields belong to the AWAKE adapter's provenance envelope; they are not Framework Core semantics. Worldbook collections and the legacy `awake.knowledge` collection are separate namespaces; a query cannot cross either boundary. `hybrid` and `semantic` are accepted only as later-phase modes and return `unverified` in P3A. Set-like scope arrays are sorted ordinal and deduplicated before scope hashing. Any mismatch in package, archive, entry, identity profile hash, manifest hash, corpus fingerprint, grant rules, content/overlay revision or policy epoch returns `rag_index_stale` rather than serving stale data. Cross-owner/campaign/timeline/session reads return no data; replacement is atomic. Embedding, rerank, FTS5 and SQLite remain unverified/deferred.
10. Diagnostics are redacted and include owner, task/correlation, generation, resolved route/provider/model, usage, context provenance, policy receipt, queue counts, latency bucket and failure code. The deterministic field policy is: omit raw prompt/template, raw model output, API key, secret, authorization header, raw provider payload, raw Hero ID, database/filesystem path and full endpoint URL; replace credential-bearing values and fine-grained identity permissions with exactly `[REDACTED]`; retain only opaque task/receipt/correlation IDs. E2 asserts each forbidden field individually: omitted fields are absent from the serialized object, marker fields equal exactly `[REDACTED]`, and no forbidden value occurs in any remaining string.

## P3A implementation write set

### Production contract files

- `framework/MarcusAwakeFramework/src/RuntimeServiceApi.cs`
- `framework/MarcusAwakeFramework/src/AiGatewayApi.cs`
- `framework/MarcusAwakeFramework/src/PromptAndOutputApi.cs`
- `framework/MarcusAwakeFramework/src/StorageAndRagApi.cs`
- `framework/MarcusAwakeFramework/src/ProviderAndEgressApi.cs`
- `framework/MarcusAwakeFramework/src/CapabilityAndPermission.cs`
- `framework/MarcusAwakeFramework/src/GameDataAndContext.cs`
- `framework/MarcusAwakeFramework/src/CommandAndSaveApi.cs`
- `framework/MarcusAwakeFramework/src/RequestContext.cs` (add `AiTaskScope` without changing command-only `IdempotencyScope`)
- `framework/MarcusAwakeFramework/src/TaskRequestCanonicalizer.cs` (shared task/egress canonicalization and SHA-256 fingerprints)
- `framework/MarcusAwakeFramework/src/AssemblyInfo.cs` (explicit Framework assembly version `2.0.0.0`)
- `framework/MarcusAwakeFramework/src/HostApi.cs` (host-owned neutral runtime service port/factory)

These files may contain neutral DTOs, interfaces, typed errors and pure validation/state machines only. They must not contain fakes, test fixtures, I/O, provider SDKs or Bannerlord references. The P3A implementation must remove the existing `InMemory*` implementations from these production files; P2 behavior is preserved by test-only implementations listed below.

### Test-only files

- `framework/MarcusAwakeFramework/tests/RuntimeServiceComposition.cs`
- `framework/MarcusAwakeFramework/tests/RuntimeVerticalSmoke.cs`
- `framework/MarcusAwakeFramework/tests/FrameworkCoreVerticalSmoke.cs` (move/update existing P2 test-only construction sites)
- `framework/MarcusAwakeFramework/tests/TestDoubles/FrameworkCoreDoubles.cs`
- `framework/MarcusAwakeFramework/tests/Program.cs`
- `framework/MarcusAwakeFramework/tests/EvidenceWriter.cs`
- `framework/MarcusAwakeFramework/tests/EvidenceSchemaValidator.cs`
- `framework/MarcusAwakeFramework/tests/TaskRequestCanonicalizerVectors.cs`
- `framework/MarcusAwakeFramework/tests/Fixtures/task-request-canonicalizer-v1.json`
- `framework/MarcusAwakeFramework/tests/MarcusAwakeFramework.Tests.csproj`
- `AWAKE/tools/verify_marcus_awake_p3a.ps1` (named source/assembly scan and E1 evidence command wrapper)

The first implementation action is to relocate the existing P2-only `InMemoryCapabilityBroker`, `InMemoryGameDataService`, `InMemoryCommandService` and `InMemorySaveAnchorStore` implementations out of production `src` and into `tests/TestDoubles/FrameworkCoreDoubles.cs` under the `MarcusAwakeFramework.Tests.TestDoubles` namespace, updating only test construction sites and the test project include. No `InMemory*` test double may remain in the release framework assembly; a release build is blocked if the type names or their implementations are present in the production assembly.

Provider wire DTOs and provider-specific response fixtures are test-only data under `MarcusAwakeFramework.Tests`; production exposes only neutral request/response contracts. The E1 assembly scan must fail if an OpenAI, Anthropic or Ollama wire type is exported or if any provider payload is embedded in the release Framework assembly. The host port is only a neutral service boundary: P3A does not implement process, IPC, storage or provider work in the release Framework assembly. P3A must not use the existing `IpcSequenceWindow`'s `DurablyRecorded` label as evidence because the P3A ledger is non-durable; F-042/F-043 are handed off to the P3B IPC contract.

The production-only static scan also rejects `.Wait(`, `.Result`, `GetAwaiter().GetResult`, `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, reflection-based loading of the old Marcus assembly/namespace and any direct old-module path. The test harness may use bounded synchronization and an injected virtual clock only inside test-only files; those uses must be listed in E1 as test scope rather than silently ignored.

### Fixed evidence files and generation order

1. `docs/evidence/schemas/MARCUS-AWAKE-P3A-E1.schema.json` and `docs/evidence/schemas/MARCUS-AWAKE-P3A-E2.schema.json` — stable evidence schemas.
2. `docs/evidence/MARCUS-AWAKE-P3A-API-SURFACE-BASELINE-20260826.json` — checked-in expected public type/member baseline, authored before E1 and never generated from the same E1 run.
3. `docs/evidence/MARCUS-AWAKE-P3-RUNTIME-VERTICAL-E1-20260826.json` — `P3A-E1` compile/parse/static evidence only.
4. `docs/evidence/MARCUS-AWAKE-P3-RUNTIME-VERTICAL-E2-20260826.json` — `P3A-E2` deterministic execution evidence only.
5. `docs/checkpoints/MARCUS-AWAKE-P3-RUNTIME-VERTICAL-20260826-checkpoint.md` — status, hashes, failed cases and unverified scope.
6. `docs/MARCUS-AWAKE-P3-RUNTIME-VERTICAL-REVIEW-LOG-20260826.md` — review verdicts and accepted/rejected findings.

E1 JSON must record schema version, evidence level `P3A-E1`, exact commands `dotnet build "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\MarcusAwakeFramework.csproj" -c Release --no-restore`, `dotnet build "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\tests\MarcusAwakeFramework.Tests.csproj" -c Release --no-restore` and `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\verify_marcus_awake_p3a.ps1" -FrameworkRoot "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework" -EvidencePath "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\evidence\MARCUS-AWAKE-P3-RUNTIME-VERTICAL-E1-20260826.json" -ScanOnly`, source file list, source SHA-256, assembly SHA-256, API surface diff, parse results, forbidden source patterns, forbidden assembly references, exported type-name scan (including absence of `InMemory*`), warning/error counts and unverified items. E2 JSON must record schema version, evidence level `P3A-E2`, exact command `& "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\_build_out\tests\Release\MarcusAwakeFramework.Tests.exe" --runtime-vertical`, entry `Program.Main(args) → RuntimeVerticalSmoke.Run`, required exit code `0`, required stdout prefix `P3A-RUNTIME-SUMMARY PASS`, fixture IDs using ownership-map canonical names, exact input fingerprints, virtual clock, call/event sequence, expected/actual outcome, failure codes, terminal receipt assertions, per-field redaction assertions and expected mode, budget assertions and unverified items. Evidence is written only after the relevant command completes successfully. The runner must not be inferred from file existence: `Program.Main(args)` must dispatch `--runtime-vertical` to `RuntimeVerticalSmoke.Run`, and the test process must return nonzero on any failed case.

The checked-in API baseline is authoritative for E1: `verify_marcus_awake_p3a.ps1` must compare the release assembly's complete public type/member list exactly to `MARCUS-AWAKE-P3A-API-SURFACE-BASELINE-20260826.json`, require `FrameworkIdentity.Current().ApiVersion=2.0` and CLR `AssemblyName.Version=2.0.0.0`, and record the baseline hash plus the diff result. The baseline is authored before the E1 run and cannot be regenerated by the verification command.

E2 uses the explicit command `& "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\framework\MarcusAwakeFramework\_build_out\tests\Release\MarcusAwakeFramework.Tests.exe" --runtime-vertical --evidence-path "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\evidence\MARCUS-AWAKE-P3-RUNTIME-VERTICAL-E2-20260826.json"`. `Program.Main(args)` parses the options, calls `RuntimeVerticalSmoke.Run`, and only after all fixtures pass calls test-only `EvidenceWriter` plus `EvidenceSchemaValidator`; the writer is the sole E2 evidence producer and runs outside production Framework code. The fixed call chain is `Program.Main → RuntimeVerticalSmoke.Run → RuntimeServiceComposition.CreateFixture → FrameworkHost/SessionLease/RequestContext → RuntimeService.Start/Submit → neutral gateway/provider/storage/diagnostics`. The runner returns nonzero and writes no passing evidence on any failure; `--verify-evidence --evidence-path <path>` is a separate read-only validator command and also returns nonzero for schema or assertion failures.

The two schema files have these required top-level fields:

| Schema | Required fields | Deterministic rule |
|---|---|---|
| `MARCUS-AWAKE-P3A-E1.schema.json` | `schema_version`, `evidence_level`, `batch_id`, `commands[]`, `source_files[]`, `source_hashes`, `assembly_hashes`, `api_surface`, `parse_results`, `forbidden_scan`, `warning_count`, `error_count`, `unverified[]` | `evidence_level=P3A-E1`; all command exit codes are 0; `forbidden_scan.inmemory_test_double_exports=false` |
| `MARCUS-AWAKE-P3A-E2.schema.json` | `schema_version`, `evidence_level`, `batch_id`, `runner`, `virtual_clock`, `fixtures[]`, `event_sequences[]`, `terminal_receipts[]`, `redaction_assertions[]`, `budget_assertions[]`, `summary`, `unverified[]` | `evidence_level=P3A-E2`; `runner.exit_code=0`; `runner.stdout_prefix=P3A-RUNTIME-SUMMARY PASS`; every fixture has expected/actual result and failure code |

`redaction_assertions[]` is field-level, not a single string check. Each item contains `field`, `expected_mode` (`omitted|marker`), `actual_mode`, `forbidden_value_present` and `passed`; omitted fields must be absent, marker fields must equal exactly `[REDACTED]`, and every item must pass.

No P3A change may touch `AWAKE.csproj`, `SubModule.xml`, existing AWAKE `src`, `dist`, the game directory, the frozen candidate, or the Worldbook Studio package.

## Acceptance and evidence

- `P3A-001`: service lifecycle idempotency, bounded drain and recovery state.
- `P3A-002`: route/profile resolution and stable typed failures.
- `P3A-003`: session generation, deadline and cancellation fencing.
- `P3A-004`: streaming sequence and terminal-event state machine.
- `P3A-005`: OpenAI-compatible/Anthropic/Ollama neutral translation fixtures.
- `P3A-006`: prompt version/owner and structured-output rejection.
- `P3A-007`: scoped storage ledger and bounded RAG retrieval with atomic replacement.
- `P3A-008`: redacted diagnostics and quota/backpressure behavior.
- `P3A-009`: concurrent submit/cancel/complete operations with deterministic barriers.
- `P3A-010`: public API and forbidden-scope scan.
- `P3A-011`: task/egress canonicalizer golden vectors, including NFC, nulls, set-like arrays, number normalization and rejection cases.

Minimum evidence is E1 + E2: E1 is Release build with zero warnings/errors, contract/JSON parse, checked-in API surface baseline comparison, source/assembly hashes and forbidden-scope scan; E2 is the explicit runner execution with exact canonicalizer vectors, deterministic vertical fixtures, concurrency, redaction and budget checks plus schema validation. No E3/E4/E5 claim is allowed in this batch.

## Deferred stages

- P3B: real local Runtime Service process, authenticated loopback IPC, process lifecycle and management boundaries.
- P3C: service-owned SQLite platform/campaign storage, FTS5 RAG, durable event stream, timeline and migration receipts.
- P3D: credential reference, cloud egress policy and live OpenAI-compatible/Anthropic/Ollama provider smoke.
- P4: AWAKE MCM URL/key/model/test/save transaction and migration of current AWAKE callers.
- P5/P6/P7: DevTools/SDK, AWAKE gameplay/worldbook/relationship/event adapters, packaging, synchronization and user-run game evidence.

## Non-goals

- No compatibility shim that keeps the old `MarcusAIFramework.Api` as a second authority.
- No direct network, database, process, game, save or cloud calls.
- No player-visible MCM or Companion replacement in P3A.
- No assertion that deterministic fakes prove Provider, Worker, Bannerlord or save/load behavior.


## Revision addendum — 2026-08-26 review corrections

This addendum supersedes any earlier P3A wording that is less specific.

### Fixed resource limits

| Budget | P3A limit | On exceed |
|---|---:|---|
| concurrent tasks per owner | 4 | `quota_exhausted` |
| queued tasks per owner | 8 | `backpressure` |
| input bytes per task | 32 KiB | `input_budget_exceeded` |
| output bytes per task | 64 KiB | `output_budget_exceeded` |
| token budget per task | 2,048 | `token_budget_exceeded` |
| text deltas per task | 256 | `delta_budget_exceeded` |
| RAG chunks per query | 8 | truncate with receipt |
| RAG bytes per query | 16 KiB | truncate with receipt |
| lifecycle drain steps | 32 | `session_drain_incomplete` |

P3A reports deterministic operation counts, not real CPU or latency performance. Fairness is FIFO per owner with no unbounded retry.

### F-ID → fixture → phase gate

The fixture names below are the canonical names from the ownership map; P3A may not invent aliases. The only machine-readable statuses are `contract-only`, `fixture_only`, `planned`, `deferred` and `unverified`; explanatory wording belongs in the final column. Every row remains a contract/fixture status until its owning later phase records separate evidence.

| F-ID | P3A status and canonical fixture | Full implementation phase | P3A cannot claim |
|---|---|---|---|
| F-015 | contract-only; `RagBoundedRetrievalFixture` | P3C | SQLite/FTS5/semantic RAG |
| F-016 | contract-only; `RagProviderFallbackFixture` | P3C/A2 | embedding/rerank availability |
| F-017 | contract-only; `PromptRegistryFixture` | P3A then P6 | all AWAKE prompts migrated |
| F-018 | contract-only; `AiGatewayRouteFixture` | P3A then P4/P6 | game caller connected |
| F-019 | contract-only; `ProviderProfileFixture` | P3D/P4 | MCM configuration |
| F-020 | contract-only; `ProviderCapabilityFixture` | P3D | live provider capability |
| F-021 | fixture_only; `OpenAiCompatibleFixture` | P3D | live OpenAI-compatible provider |
| F-022 | fixture_only; `AnthropicAdapterFixture` | P3D | live Anthropic provider |
| F-023 | fixture_only; `OllamaAdapterFixture` | P3D | live Ollama provider |
| F-024 | deferred; `Player2UnavailableFixture` | P3D/A2 | Player2 availability |
| F-025 | deferred; `ComfyWorkflowFixture` | P6 | ComfyUI availability |
| F-026 | deferred; `ManagedGgufUnavailableFixture` | P3D/A2 | managed GGUF availability |
| F-027 | contract-only; `StreamingCancellationFixture` | P3B/P3D | IPC/live streaming |
| F-028 | contract-only; `RouteFallbackFixture` | P3D | live retry/fallback/pinning |
| F-029 | contract-only; `QuotaBackpressureFixture` | P3B/P3C | real CPU/provider quotas |
| F-030 | contract-only; `StructuredOutputFixture` | P3D/P6 | gameplay effect validation |
| F-031 | contract-only; `ToolCandidateValidationFixture` | P3/P6 | game command execution |
| F-032 | contract-only; `RuntimeEventBusFixture` (ownership-map status=`planned`, ownership phase=`P2`) | P2 contract / P3B runtime | P3A does not claim Event Bus availability; P2 contract status and P3B runtime status are separate |
| F-033 | contract-only; `DurableEventReplayFixture` | P3C/P6 | durable replay/ack |
| F-034 | contract-only; `DurableSpoolFixture` | P3B/P3C | disk spool |
| F-035 | contract-only; `PlatformStorageFixture` | P3C | platform.db |
| F-036 | contract-only; `CampaignStorageFixture` | P3C | campaign.db/save anchor |
| F-037 | contract-only; `StorageOwnershipFixture` | P3C | managed KV/sidecar/read views |
| F-042 | contract-only; `IpcHandshakeSequenceFixture` | P3B | authenticated process handshake, parent proof and SID binding |
| F-043 | contract-only; `SessionInvalidationFixture` | P3B/P3C | durable IPC sequence, checksum and crash recovery |
| F-044 | fixture_only; `ServiceLifecycleFixture` | P3B/P5 | logical fake only; no process management |
| F-046 | contract-only; `CredentialLifecycleFixture` | P3D/P4 | opaque reference only; no credential storage/key lifecycle |
| F-047 | fixture_only; `PermissionOrderAndEgressFixture` | P3D/P4 | policy fake only; no cloud egress |
| F-048 | contract-only; `DegradedModeFixture` | P3B/P3D/P4 | typed degraded errors only; no full offline fallback |
| F-057 | contract-only; `ObservabilityCorrelationFixture` | P3B/P5 | redacted contract only; no service logs/DevTools |
| F-058 | fixture_only; `PerformanceBackpressureFixture` | P3B/P3C | deterministic quota only; no real performance proof |

Every row uses exactly one status from `contract-only`, `fixture_only`, `planned`, `deferred` or `unverified`; P3A never promotes an F-ID to `available`.

P3A does not execute F-042/F-043 and does not reuse `IpcSequenceWindow` as a durable proof. The no-I/O handoff document `docs/MARCUS-AWAKE-P3B-IPC-HANDOFF-CONTRACT-20260826.md` fixes the future mapping for handshake identity, session/generation fence, direction nonce, global sequence, payload schema, checksum, ACK/retry and parent/process proof before P3B implementation begins.

### Test-only fake rule

`RuntimeServiceComposition.cs` and every provider/storage/policy/clock/lifecycle fake belong to the test assembly only, under a test-only namespace. Production files may contain contracts and pure validation only. The package/release scan must prove that test doubles, test executables, PDBs and fixture payloads are absent from player output.

### Complete stream and task rules

The neutral stream set is `Accepted`, `Started`, `TextDelta`, `UsageUpdate`, `RouteChanged`, `Completed`, `Cancelled`, `Failed`. Per-task `stream_sequence`, IPC direction sequence and durable `event_index` are distinct. `cancel_requested` is a progress/intent event, never a terminal event. An atomic in-memory terminal ledger admits exactly one terminal for each complete request scope; the first linearized compare-and-set wins. The compare-and-set is the only P3A linearization point; it is not a durable write and is lost when the fake process ends. A completion/cancel race is decided by that same in-memory linearization point: cancellation before executor completion may yield `cancelled`, while cancellation after a completed receipt is linearized returns the original `completed` receipt. No P3A statement uses durable/persisted semantics; durable first-write-wins belongs to P3C. Unknown-task cancellation returns `cancel_requested_unknown`; it cannot fabricate a terminal event. Duplicate terminals return `duplicate_terminal_event` and are audit-only. Unsupported media/tool events return explicit `unsupported`/`unverified`.

Every AI task must carry `task_id`, `message_id`, `owner_id`, `campaign_guid`, `timeline_id`, `session_id`, `session_generation`, `correlation_id`, `causation_id`, `idempotency_key`, route/provider/profile, `settlement_requirement`, finite deadline, token/byte/delta budgets and the session-owned cancellation token. Validation order is identity → active generation → owner/correlation → route/profile → settlement → canonical request fingerprint → existing `AiTaskScope` lookup → deadline → budgets → cancellation. `CancellationToken.None` is invalid for session-owned work. `AiTaskScope` is the sole AI-task deduplication authority; `IdempotencyScope` is referenced only for a required game-effect settlement and is never used for `not_applicable` tasks.

The submission deduplication key is the complete `AiTaskScope`: `owner_id + campaign_guid + timeline_id + session_id + route_id + provider_id + profile_id + message_id + idempotency_key + request_payload_hash + output_schema_ref`; `request_payload_hash` is required for both `required` and `not_applicable` tasks and is computed from the canonical neutral request before quota admission. The shared `TaskRequestCanonicalizer` is versioned as `marcus-awake/task-request/v1`: it excludes transport-only `task_id`, correlation/causation IDs, deadline and cancellation token; it includes route/provider/profile, message, input, output schema, settlement mode and all semantic budget fields; it emits canonical UTF-8 JSON and returns `lowercase_hex(SHA-256(UTF-8("marcus-awake/task-request/v1\n" + canonical_json)))`. A changed payload, provider/profile, route, schema or message under the same idempotency key returns `idempotency_conflict`.

P3A validation failures that refer to original invalid model output retain only `raw_output_present`, byte length and a SHA-256 fingerprint in the game-facing/serialized diagnostic; the original text is an in-memory test value in P3A and is not emitted to evidence. Later Runtime Service may retain restricted raw diagnostic material for DevTools under its own policy, but it is never part of the player-facing diagnostic. This is the P1.5 meaning of “retain original diagnostic”: retention is service-owned, access-controlled and export-redacted; P3A keeps only presence/length/hash metadata and never serializes the original text.

The P3A task receipt preserves `receipt_id`, `task_id`, `message_id`, `idempotency_key`, request payload hash, provider/profile, `correlation_id`, `causation_id`, owner, campaign/timeline/session, session generation, terminal status, `settlement_requirement` and nullable `settlement_id`. Receipt publication uses the same logical in-memory terminal ledger and compare-and-set linearization point that rejects duplicate terminal events and stale-generation writes; the ledger is not durable and provides no crash-recovery claim in P3A. Durable first-write-wins and restart recovery belong to P3C.

### Evidence split and gate record

- `P3A-E1` means Release compile, JSON/contract parse, API surface diff, exported-type scan and static source/assembly forbidden-scope scan, all with zero warnings/errors.
- `P3A-E2` means the test executable is invoked through its explicit Runtime Vertical entry, then deterministic fixture execution, concurrency smoke, exact translation assertions, receipt/terminal checks, field-level redaction checks and bounded-resource assertions are recorded.
- P1.5 phase-level `P3/E2` remains the later real Runtime Service process/protocol smoke; `P3A-E2` is only its offline predecessor and cannot satisfy it.
- No E3/E4/E5 claim is permitted for P3A. P3B/P3C/P3D require their own evidence.

Gate state after this revision: `plan_status=revised_for_review`, `review_status=pending`, `user_signoff_required=already_granted_by_autonomous_migration_authorization`, `primary_executor=controller`, `minimum_evidence=P3A-E1 + P3A-E2`, `later_evidence_gate=P3/E2-real-runtime-service-smoke`. Exact process transport, SQLite provider/package binding, OS credential backend and live provider matrix remain later-phase decisions.
