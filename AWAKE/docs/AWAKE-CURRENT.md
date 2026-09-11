# AWAKE Current State

> Updated: 2026-09-11 (Asia/Shanghai). Two paired batches are built, package-verified and synced from source/dist to the game directory: **008 runtime recovery** (`AWAKE-RUNTIME-RECOVERY-20260911`) and **009 health contract** (`AWAKE-HEALTH-CONTRACT-20260911`), both delivered under BuildId `awake-20260911-health-contract-009`. Root cause of "配置没准备好 / 填 KEY 填不进去": the client required the health ack payload to be byte-for-byte `{"state":"ready","ledger":"non_durable"}` while the transport's `StrictJson` renders object keys in Ordinal order, so the service always answered `{"ledger":"non_durable","state":"ready"}` and **every** self-check threw `response_health_payload_invalid`; `CheckHealthAsync` then called `FailConnectionAsync`, which killed the Runtime process, and AWAKE had no restart path inside a session. Fixes: `HealthAckPayload` now uses the StrictJson order, a failed health probe no longer tears the connection or the process down, and AWAKE can relaunch a `Stopped` runtime from the MCM gates (20 s cooldown, 3 attempts per session). Verified offline (runtime harness 19/0 with a red-then-green contract assertion, framework tests PASS ALL, AWAKE smoke / persona-anchor / red test all green) and synced (`sync-20260911-002651257.json`, `sync-20260911-002818395.json`). In-game verification against BuildId 009 is the user's step; user sign-off on both batches is still open.
>
> Previous update: 2026-09-10 (Asia/Shanghai). AWAKE R1 offline behavioral negative-path red test executed for candidate `004`, and the user explicitly authorized the game sync. The P0 Persona charter is approved and signed off, and the **persona anchor slice** (BuildId `awake-20260910-persona-anchor-005`) is now built, package-verified and synced from source/dist to the game directory. The user ran the game and **E4 passed in-game**: `persona_continuity_saved` and `persona_continuity_loaded` appear as a pair, and the same save loaded twice returns the same campaign/character/schema/sequence. E5 content-level remains `blocked_not_wired`. Four pre-existing runtime issues observed during the run are tracked as a separate repair batch (see the 2026-09-10 实机记录 section). The first of those items was chartered and implemented as the provider-diagnostics batch (BuildId `awake-20260910-provider-diagnostics-006`): every MCM provider failure writes a structured `Awake.log` line and the runtime service mirrors its own stderr into `awake-runtime-service.log`. Independent review returned one REVISE (the runtime log file was created lazily, so it could stay absent) and the revision is applied and round 2 closed APPROVED with no new P0/P1; the batch is awaiting user sign-off at that time. The second item was then chartered and implemented as the base-URL tolerance batch (BuildId `awake-20260910-baseurl-tolerance-007`): the MCM provider-address field now accepts a complete endpoint such as https://api.deepseek.com/chat/completions by stripping a trailing /chat/completions, /completions or /models (a rewrite is logged as provider_base_url_normalized from=... to=...), the Chinese rejection text and the Runtime contract are unchanged, and the en/zh MCM hint says a full endpoint may be pasted. Round 1 APPROVED (2xP2 + 1xP3) -> revision -> round 2 APPROVED with no new P0/P1; both batches were signed off by the user on 2026-09-10 (`status=approved / user_signoff=true`). Both batches are now packaged and synced - `dist` and the game directory hold `awake-20260910-baseurl-tolerance-007`.

> Compact recovery state. Read this before historical queues or long review logs.
> Updated: 2026-08-31 (Asia/Shanghai)

## Current Thread Focus

- active_delivery_batch: `AWAKE-HEALTH-CONTRACT-20260911` (paired with `AWAKE-RUNTIME-RECOVERY-20260911`); delivered BuildId `awake-20260911-health-contract-009`; engineering version stays `v0.2.0`.
- active_redtest_target: `AWAKE-REDTEST-PREP-20260908`; red-test preparation is now the active AWAKE本体 track. Worldbook migration remains an external review-only dependency and is not part of this target.
- redtest_status: `behavioral_offline_cases_executed_bannerlord_cases_pending`; R1 offline behavioral negative-path red test ran on 2026-09-10 and was extended to `19 PASS / 0 FAIL / 2 RISK / 1 BLOCKED / 7 NOT_ATTEMPTED`. Bannerlord E4 cases and E5 save/recovery remain open. No Bannerlord run and no real Provider access occurred; the game directory sync is recorded separately below.
- redtest_plan: `docs/PLAN-AWAKE-REDTEST-PREP-20260908.md`; the red-test candidate was `004`, which was synced to `dist` and the game directory on 2026-09-10 and has since been superseded there by `awake-20260910-persona-anchor-005`.
- redtest_evidence: `docs/evidence/AWAKE-REDTEST-R1-STATIC-20260908.json`, `docs/evidence/AWAKE-REDTEST-R1-SDKSMOKE-20260908.txt`, `docs/evidence/AWAKE-REDTEST-R1-BEHAVIORAL-20260908.json` (+ `.md`); harness entry `Awake.SdkSmoke.exe --redtest-r1-behavioral`. 注意：红测 harness 现在把 `build_id` 绑到 `AwakeVersion.BuildId`（不再写死字面量），所以 2026-09-10 重跑后的 behavior 证据记录的是当前源码 BuildId `awake-20260910-persona-anchor-005`，逐案结果不变（19 PASS / 0 FAIL / 2 RISK / 1 BLOCKED / 7 NOT_ATTEMPTED）。
- redtest_blockers: P0 Persona persistence is now **anchor wired** through `SyncData` (`awake_persona_continuity_v1`), but persona **content** (projection / sequence / watermarks / recovery) is still not wired to runtime projection, so E5 stays `blocked_not_wired`; P1 risks include SaveId not entering Persona storage keys, duplicate Behavior registration, overlay ownership/focus, uncancellable UI send, unbounded UI drain, Config.json fallback, ContentPolicy gate, amount validation, timeout policy, and missing crime path.
- redtest_next_action: convert the 7 NOT_ATTEMPTED cases into the R3 in-game checklist; the P0 Persona persistence wiring is now chartered (`PLAN-AWAKE-PERSONA-PERSISTENCE-20260910.md`) and its anchor slice has since been synced and verified in game (see `redtest_e4_result` below); the P1 Config.json fallback / content-policy gate gaps still need a separate approved repair batch rather than edits to candidate `004`; preserve all P0/P1 findings. E5 remains blocked_not_wired until the Persona content slice lands; do not launch Bannerlord without user action.
- redtest_sync: `completed_20260910`; user authorized the sync on 2026-09-10. `tools\package_embedded_runtime.ps1` rebuilt the stale embedded Runtime (`RUNTIME_PACKAGE_OK`, manifest `FBBDF4A8…A9B2857`), then `tools\sync_module.ps1 -ConfirmGameSync` -> `state=verified` (`docs/sync-reports/sync-20260910-200556708.json`, `sync-20260910-200754788.json`). Game `Modules\AWAKE` upgraded from `002` (`D00478D7…957FEBE`) to `004` (`B25A5A4F…F74C17E`); source/dist/game are byte-identical for `Awake.dll`, `MarcusAwakeFramework.dll`, `MarcusAwakeTransport.dll`, `SubModule.xml`, worldbook `manifest.json` and `BUILD_VERIFICATION.txt`. Version stays `v0.2.0`.
- redtest_e4_access: the game directory has since been upgraded from `004` to the persona-anchor slice `awake-20260910-persona-anchor-005` (see the 2026-09-10 实机记录 section), so every run must confirm a matching `build_id=` line in `Modules\AWAKE\Logs\Awake.log` before any result is attributed to a candidate. E5 stays blocked_not_wired.
- redtest_e4_result: `2026-09-10` the user ran the game and the persona anchor E4 **passed** - `persona_continuity_saved` / `persona_continuity_loaded` appear as a pair on `campaign=F8yU7kuRWVFL`, and the same save loaded twice returns the same campaign/character/schema/sequence. Four pre-existing runtime issues seen in the same run are deferred to a separate repair batch: AI provider `provider-models` failure -> `runtime=Stopped`, a one-shot `native_readiness_probe_error` on first start, `worldbook_runtime_init_error WB2-SCHEMA-UNSUPPORTED:entry`, and `permission_gate_evaluate permission=storage.namespace.write decision=Denied` -> `awake_campaign_state_restore_skipped reason=storage_not_ready`. E5 stays `blocked_not_wired`.
- redtest_r1_extension: 2026-09-10 extended the R1 offline behavioral suite with 7 `AiTaskGateway.SubmitAsync` boundary cases (context_missing, caller cancellation short-circuit, route_missing, cataloged route permission denied, unknown cloud-export classification, cloud-export disabled for `player_state`, `awake.player_unbound`). Totals are now `19 PASS / 0 FAIL / 2 RISK / 1 BLOCKED / 7 NOT_ATTEMPTED`; the offline boundary stops before hero binding because the framework `ICompatibilityGameDataService` is internal, so provider-submit, duplicate-terminal-event and deadline-cancellation cases stay NOT_ATTEMPTED.

- persona_persistence_project: `P0 Persona 持久化` opened on 2026-09-10 as a `high-risk` review-channel batch. Charter `docs/PLAN-AWAKE-PERSONA-PERSISTENCE-20260910.md` (revision B); review state (max 3 rounds) `docs/review-state/AWAKE-PERSONA-PERSISTENCE-20260910.review.json` is now `round=1 / revising` after an independent read-only round-1 review returned `REVISE` with 3xP0 + 10xP1 (reviewer was a separate Codex subagent, not the author). Verified gap: `PersonaPersistenceModels`/validator and the `awake.persona.*` storage schemas already exist and the `awake.persona.state` namespace is registered, but nothing constructs a `PersonaPersistenceEnvelope` or reads one back, no Persona anchor is synced through `SyncData`, and `PersonaEnvelope` has zero producers and zero consumers. Revision B retracts the A-version premise: `Campaign.Current` is non-null at `OnGameStart` (`SandBox.decompiled.cs:5178-5191`, `CampaignSystem.decompiled.cs:10329-10337`), but a new campaign's `UniqueGameId` is only generated later (`:10569` vs `:10369`) and behavior `SyncData` load also runs after `OnGameStart` (`:10407`). Consequence: the recovery entry moved to `CampaignEvents.OnGameLoadedEvent` / `OnNewGameCreatedEvent`, and the recovery state machine plus the composed identity key were dropped in favour of a single fixed `SyncData` key `awake_persona_continuity_v1` holding one self-contained JSON snapshot with explicit `oldSave`/empty/mismatch fail-closed branches. Status: `charter_revised_pending_round2`; the same day revision C was approved and signed off, and the anchor slice was implemented (see `persona_persistence_review` / `persona_anchor_slice`) with a new BuildId and without editing candidate `004`.
- persona_persistence_review: `completed_20260910`. Charter went through the full `high-risk` review channel with three **independent** read-only reviewers (separate Codex subagents, never the author): round 1 `REVISE` (3xP0 + 10xP1) -> revision B, round 2 `REVISE` (all 3 P0 CLOSED, **no new P0**, 7x blocking P1) -> revision C, round 3 **`APPROVED`** against frozen revision C `sha256 2CA39534CC83727C363AC9C82001D098547A692EF2CED5710CD8E0D050B3460E`. Review state is now `round=3 / approved / user_signoff=true`; **the approved revision is frozen and must not be edited** - further fixes go into the implementation batch. Scope of the approved slice is an **anchor slice** only: persist/load player persona identity (campaign/character/timeline/branch) + schema version through `SyncData` key `awake_persona_continuity_v1`; `sequence`/`watermarks` stay `0` and the persona content producer is explicitly deferred to the next slice, so E5 must stay `blocked_not_wired` after this batch and may only become `anchor wired`. Four non-blocking P2 carry-overs to fix inside the implementation batch: (1) say "no campaign logic dependency" for the seam, since `IDataStore` still comes from `TaleWorlds.CampaignSystem`; (2) pin payload property casing (model serializes PascalCase by default while the plan example is camelCase); (3) restate "SyncData shrinks to one line" as "existing `awake_last_weekly_report_day` sync is preserved, persona becomes one seam call", so the weekly-report key is not silently dropped; (4) `Adopt` must treat empty/whitespace JSON as `no_persona` without warning so legacy saves do not spam `persona.persistence.rejected`. Next action: user signoff (`Validate-ReviewState.ps1 -Action approve -UserSignoff`), then implement the anchor slice under a **new BuildId**.
- delivery_state: `synced_pending_ingame_and_signoff`; build `BUILD_OK`; embedded Runtime repackaged (manifest `9421A94F…18AFB`, `SHA256SUMS.txt` `37E556A7…8A333`); `Awake.dll` `B2845D47…33889` and `MarcusAwakeFramework.dll` `B7B1E8E5…7C066` are byte-identical across build/dist/game; sync reports `docs/sync-reports/sync-20260911-002651257.json` (copied=8) and `sync-20260911-002818395.json` (copied=2).
- delivery_open_items: user in-game check that an "AI 自检" click succeeds and that `response_health_payload_invalid` no longer appears in `Awake.log`; user sign-off on both review state files (both sit at `awaiting_signoff`).
- delivery_evidence: `docs/evidence/AWAKE-RUNTIME-RECOVERY-20260911.json` / `.md`, `docs/evidence/AWAKE-HEALTH-CONTRACT-20260911.json` / `.md`; charters `docs/PLAN-AWAKE-RUNTIME-RECOVERY-20260911.md`, `docs/PLAN-AWAKE-HEALTH-CONTRACT-20260911.md`.
- persona_anchor_slice: `2026-09-10` 已实现**锚点切片**（BuildId `awake-20260910-persona-anchor-005`；离线构建 `BUILD_OK`、嵌入式 Runtime 打包 `RUNTIME_PACKAGE_OK`，并已同步 source → dist → 游戏目录，三地一致）：新增接缝 `src/PersonaContinuitySync.cs`（固定键 `awake_persona_continuity_v1`、用 `IDataStore.IsSaving` 判方向、`Adopt` 分类）；`AwakeEventBehavior.SyncData` 保留既有 `awake_last_weekly_report_day` 并追加一次接缝调用，装载点 `CampaignEvents.OnGameLoadedEvent`、新档 `OnNewGameCreatedEvent`、换主角 `OnPlayerCharacterChangedEvent`；payload = `PersonaPersistenceEnvelope` 序列化本身（camelCase 已用 `[JsonProperty]` 钉死、无时间戳、`sequence`／`watermarks` 恒 0），失败分支 `campaign_id_empty`／`campaign_id_not_unique`(`oldSave`)／`campaign_mismatch`／`character_mismatch`／`schema_unsupported`／损坏 JSON 全部 fail-closed，旧档缺键静默不告警。清理：schema 字面量单点化、`PersonaStateNamespace` 退出默认打开列表（常量保留）、未启用模型已标注未启用。G3-S0 聚焦证据校验重跑仍 `pass`；其 scope 校验 `blocked/20` 是既有 released 租约所致，与本批无关。
- persona_anchor_evidence: `docs/evidence/AWAKE-PERSONA-ANCHOR-20260910.json`（+.md）。离线 `Awake.SdkSmoke.exe --persona-anchor` 15/15 PASS、exit 0；主 smoke `PASS ALL Awake.SdkSmoke`；payload 逐字节稳定（sha256 `2AE019E0A1C01C92A135DD61FCDFE6FAD2729E9189B83094F9B81E29F821C36C`）；`Awake.dll` sha256 `C2130FC0FDAE76BC6C4245D45C422061CB1449175DC53D0016847AD3E8346599`，source/dist/game 三地一致。**E4 实机已通过**：`persona_continuity_saved` / `persona_continuity_loaded` 成对出现（同档 `campaign=F8yU7kuRWVFL`），同一档二次读档结果一致（详见本文 2026-09-10 实机记录与 JSON `inGameEvidence`）。E5 仍 `blocked_not_wired`；锚点恢复身份，不恢复 persona 内容。

- provider_diagnostics_batch: `AWAKE-PROVIDER-DIAGNOSTICS-20260910`（`standard`，max_rounds=2）已实现：BuildId `awake-20260910-provider-diagnostics-006`，版本仍 `v0.2.0`。**该批次现已随 007 一起打包并同步进 `dist` 与游戏目录**（此前仅源码 / 未同步的说明已过期）。改动：MCM Provider 失败（应用配置 / 自动应用 / 拉取模型 / 连接测试 / 保存 API Key / AI 自检 / runtime health）统一写结构化 `Awake.log` 行（`provider_operation_failed` / `provider_operation_rejected`，含 code / category / retryable / correlation / details(provider,profile,route,status_code) / message）；Runtime 新增 `RuntimeServiceLog`，启动即建 `awake-runtime-service.log` 并镜像自身 stderr（`MARCUS_AWAKE_RUNTIME_DATA_ROOT` 可覆盖、2 MB 轮转）。用户可见文案与本地化零改动。
- provider_diagnostics_review: 独立只读审查 round 1 = `REVISE`（P1：Runtime 日志文件懒创建，目标故障路径不写 stderr 时文件不出现，与验收标准 3 冲突；P2：`Enable()` / 环境变量根目录路径无测试）。已修订：`Enable()` 先把 banner 直写文件再挂 tee（stderr 字节不变），补第 4 个 Runtime 日志用例（环境变量覆盖 + 建文件 + banner + stderr 镜像），验收标准 3 与实机步骤改写为"文件必然出现，但 Provider 失败本身走结构化错误帧、不写 stderr"。审查者已确认：IL 字面量差分显示零 CJK 文案增删、无凭据/正文落盘、调用点无漏改、`FrameworkError` / 账本结构 / `SubModule.xml` / 版本号未触碰。状态：`round=2 / awaiting_signoff`，round 2 = **APPROVED**（无新 P0/P1；唯一遗留为 P3 非阻塞建议：Runtime 存活期间日志句柄常开，读该文件请先退游戏、不要运行期手工删除，已写入证据 `residualRisks`）。**已签收**（2026-09-10，`status=approved / user_signoff=true`）；该批次现已随 007 一起同步进 `dist` 与游戏目录，实机验证仍是用户步骤。
- provider_diagnostics_next: 已随 007 一起同步进 `dist` 与游戏目录。用户点 MCM「AI 自检 / 连接测试 / 拉取模型」后应见 `Awake.log` 出现 `provider_operation_failed` / `provider_operation_rejected`，并见 `awake-runtime-service.log` 存在（banner 行）。证据：`docs/evidence/AWAKE-PROVIDER-DIAGNOSTICS-20260910.json`（+ `.md`）、立项 `docs/PLAN-PROVIDER-DIAGNOSTICS-20260910.md`。
- provider_baseurl_tolerance_batch: `AWAKE-PROVIDER-BASEURL-TOLERANCE-20260910`（`standard`，max_rounds=2）已实现、打包并**同步**：BuildId `awake-20260910-baseurl-tolerance-007`，取代 `006`，版本仍 `v0.2.0`、`SubModule.xml` 未改。动机：MCM「服务地址」要求填 API 根，但用户自然会粘完整接口地址（`https://api.deepseek.com/chat/completions`），Runtime 再拼子路径得到 `…/chat/completions/models` → 404；用户自己的 WorldbookStudio 本就容忍三种写法，故修在 AWAKE 入口而非要求用户改习惯。改动：`src/AwakeProviderConfiguration.cs:557 TryCaptureSnapshot` 改走新 `TryNormalizeProviderBaseUrl`（`:599`）——剥离末尾 `/chat/completions`、`/completions`、`/models`（忽略大小写、容忍尾斜杠），保留其余路径（如 `/v1`）；校验与中文拒绝文案逐字不变；不自动补 `/v1`；发生改写时写 `provider_base_url_normalized from=… to=…`（不静默）。Runtime 契约 `BuildEndpointUri` / `ValidateProfilePolicy` / `ExactOriginEndpointPolicy` 未改。声明的用户可见变更：中英 MCM 提示词说明可直接粘贴完整接口地址（各 1 处，键数仍 291）——这是**改写非追加**，原 `http://127.0.0.1:11434` 示例不再出现。
- provider_baseurl_tolerance_review: 独立只读审查 round 1 = **APPROVED**（2×P2：`/models` 静默剥离；`AWAKE.Tests` 的 `AssertProviderBaseUrlEndpoint` 复刻 Runtime 路径拼接规则属契约镜像，`net472` 无法引用 `net8.0` Provider；1×P3：尚未同步）→ 修订补 `provider_base_url_normalized` 日志行、把镜像漂移写进立项与风险 → round 2 = **APPROVED**，无新 P0/P1。**状态文件只记 round 1**（`round=1 / awaiting_signoff`）：状态机在 `awaiting_signoff` 后拒绝再写 review，故 round 2 落在证据文件而非状态 JSON（工具文件未手改）。**用户已签收**：2026-09-10 `approve -UserSignoff` 落账 `status=approved / user_signoff=true`。
- provider_baseurl_tolerance_sync: `package_embedded_runtime.ps1` → `RUNTIME_PACKAGE_OK`（manifest `cbb14d38…0cd9`、sums `8e29d144…a9d`）；`sync_module.ps1 -ConfirmGameSync` → `state=verified`，报告 `docs/sync-reports/sync-20260910-233108757.json`（copied=8）与补打包后的 `sync-20260910-233307078.json`（copied=3）；写完本记录后再同步传播 `BUILD_VERIFICATION.txt`（`sync-20260910-233944971.json` / `sync-20260910-234128539.json`，各 copied=2，`BUILD_VERIFICATION.txt` 三地一致 `F058227F…688C0B`）。**排序陷阱**：`sync_module.ps1` 只校验、不重建内嵌 Runtime，必须先跑 `package_embedded_runtime.ps1` 再同步，否则游戏目录会留旧 Runtime。四地一致：`Awake.dll` `6ECB27A0…C950`、`zh-HANS` `31BED748…8EB11`、`en` `903DB5A9…21FB6`、`SubModule.xml` `378E9C0B…E80F66`（未改）、`RuntimeService.exe` `84CC4D6C…A1490A`；内嵌 Runtime dist=game 逐字节相同、`SHA256SUMS.txt` 195/195 命中、006 的 `RuntimeServiceLog` 已在部署 DLL。
- provider_baseurl_tolerance_evidence: `docs/evidence/AWAKE-PROVIDER-BASEURL-TOLERANCE-20260910.json`（+ `.md`）、立项 `docs/PLAN-PROVIDER-BASEURL-TOLERANCE-20260910.md`（sha256 `0C03B773…A31899`）。离线全绿：主 smoke `PASS ALL`（新增 4 条 `provider.base_url.*`）、`--persona-anchor` `PASS ALL`、`--redtest-r1-behavioral` no FAIL、`validate_localization.ps1` `LOCALIZATION_OK source=241 en=291 cn=291`。**未验证**：API Key / 凭据是否存在、本次 `provider-models` 失败根因是否即地址形状、早前 `runtime=Stopped` 原因；无实机结论，实机验证仍待用户。
- mirror_repo_sync: `AWAKE-Repo` was synchronized and pushed on 2026-09-10 (`946f02f..1120ef3`, `main`). `sync_awake_repo.ps1` now also excludes generated trees (`artifacts`, `_tmp`, `node_modules`, `.runtime`, `.packages`, `workspace*`, `.publish*`); without this the mirror tried to publish ~35 GB, now ~13 MB. Commit contents: `AWAKE/src` (128 `.cs`), `AWAKE.Tests`, `AWAKE/docs`, `AWAKE/GUI`, tool sources (`AWAKE/tools`: worldbook-studio 196 files, persona-workbench 111, plus the build/verify/sync scripts), SDK reference.
- mirror_repo_worldbook_removed: 2026-09-10, `cc7b057` (`1120ef3..cc7b057`, `main`). The repository is **public** and still carried the Calradic Chronicle world book migrated from `AnimusForge\PlayerExports\卡拉迪亚编年史` (415 `personality_background` + 335 `rules` + `migration_report.json`, 759 files / 2.99 MB). Verified that tree is the **superseded `awake.worldbook.v1` backup**, not the official world book: `WorldbookRuntime.EnsureCreated` accepts only `awake.worldbook.registry.v1` or `awake.worldbook.v2` and otherwise throws `WB2-SCHEMA-UNSUPPORTED:entry` (`src/WorldbookRuntime.cs:92-110`, catch at `:123-126`), while `LocateManifest()` does find this `ModuleData/Worldbook/manifest.json` (`:156-168`) - so the runtime currently initializes with **no world book at all** (the installed game module has the same v1 manifest). Action taken: untracked from HEAD, added to `.gitignore`, excluded in `sync_awake_repo.ps1` (exclude name `Worldbook`), README now states the world book is not published here. Remote verified: `AWAKE/ModuleData/Worldbook` returns 404 and `AWAKE/ModuleData` now holds only `Languages` and `Rules`. Tracked files 1452 -> 693. **The 759 files still exist in git history (`4667d93`, `1120ef3`) and in the local workspace - removing them from history would need a rewrite plus force push, which is not authorized.** The official world book is still being authored in the local `worldbook-studio` editor, so no publish path is defined yet; when a `awake.worldbook.v2`/`registry.v1` package lands, re-decide the publish path and drop the `Worldbook` exclusion if it must ship.

- active_repair_batch: `AWAKE-OFFLINE-EVIDENCE-BASELINE-20260903`; Runtime source candidate, Persona fixture baseline, and legacy Worldbook report-only evidence are being reconciled before any new implementation batch.
- active_repair_status: `offline_evidence_reconciled_with_worldbook_blocker`; current source remains bound to unsynchronized `004`; historical `002`/`003` evidence is not current evidence.
- next_repair_action: human-review the five semantic rewrite candidates, resolve the Downloads source registry and full-source parse gates, then establish a separate approved Persona Projection/Persistence contract; do not sync or launch Bannerlord.

- task_id: MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824
- status: `superseded_by_repair`; previous `002` candidate remains only historical evidence and is not valid for the current repaired source.
- execution_lease: `active`; source repair is implementing; no game sync and no real cloud Provider access.
- checkpoint: docs/checkpoints/AWAKE-RUNTIME-PERSISTENCE-REPAIR-20260903-checkpoint.md。
- plan: docs/PLAN-AWAKE-WORLDBOOK-PILOT-REPAIR-20260903.md; Persona and settlement evidence plans remain separate.
- verification: source AWAKE Release `0 warnings / 0 errors`; `Awake.SdkSmoke` `PASS ALL`; Worldbook Runtime Production Smoke `18/18`; Persona fixture baseline `1` positive + `1` fallback + `3` expected rejects; Downloads Worldbook report-only artifacts reconciled to one snapshot/hash pair.
- source_worktree: `repair_pass2_verified`; source and local build outputs are current, while synchronized `002` artifacts and prior `003` staging remain historical only.
- candidate_state: `source_only_pending_sync`; BuildId `awake-20260903-awake-runtime-repair-004` is the current source candidate; `002` and `003` remain unchanged and historical.
- next_action: use the structurally validated 30-file derived child snapshot for human semantic review; keep all candidates `needs_review` until source/permission/content gates pass; current `004` remains source-only.
- boundaries: 本轮未启动 Bannerlord、未同步游戏目录、未访问真实云 Provider、未读取真实 API Key；Worldbook Studio 客户包已生成但未宣称真人验收或真实 Provider 验证。

## Source Candidate — 004 (superseded in `dist` and the game directory; the game directory now holds `awake-20260910-baseurl-tolerance-007`)

- BuildId: `awake-20260903-awake-runtime-repair-004`; engineering version remains `v0.2.0`，未擅自提版；`004` was synced to `dist` and the game directory on 2026-09-10 (see `redtest_sync`), and is superseded there by `awake-20260910-persona-anchor-005` (see the 2026-09-10 实机记录 section).
- Source hashes: `Awake.dll` `b25a5a4f1f7e95d7182bbd48fdc41a894e8366366440eca4d27f2582af74c17e`; `MarcusAwakeFramework.dll` `00b34bf79defadbe4a946612244ea8631ce777495c2b99add11f2b10ce8c7f55`; `MarcusAwakeTransport.dll` `de81809ac08ea86be90fd6f0ab77fa348b24b744588b0af970dfff2080e6df48`; Runtime Service `288c5d2aa24fbccb5731a1bd4391db18aaec0607196cd10f8bb4e96ccc83a128`.
- Runtime staging: `C:\Users\26811\OneDrive\文档\New project\AWAKE-release-staging\awake-20260831-marcus-embedded-003\Runtime`; manifest SHA-256 `fbbdf4a8a4e0dca9f00b42dd4dac938438d59b62826e409139488dc95a9b2857`; sums SHA-256 `2a21ed84c914e1a06985d101209c5efed1a5d390bdf11c0e4a9ff234ff60f497`.
- Runtime durable repair: provider idempotency outcomes persist without credentials or response bodies; `Applied`, `Retryable`, `Unknown`, `Failed`, and hash conflict are classified across restart; in-flight records recover conservatively as `Unknown`.
- AWAKE persistence repair: storage write callers now fail closed on retryable/unknown/hard-failure drains; memory reservations retain their payload through final drain recovery.
- Evidence boundary: current `004` has offline E1/E2 source evidence only; Bannerlord E4/E5, game-directory synchronization, real Provider access, and final release pointer remain pending.

## Historical Verified Snapshot — 002 (superseded; the game directory now holds `awake-20260910-baseurl-tolerance-007`)

- BuildId: `awake-20260829-marcus-embedded-002`; engineering version remains `v0.2.0`，没有擅自提版。
- Artifact hashes: `Awake.dll` `D00478D7B796974B4FFF6EDF385B6E31A7B3F712BECE89E5C013F7438957FEBE`; `MarcusAwakeFramework.dll` `00B34BF79DEFADBE4A946612244EA8631CE777495C2B99ADD11F2B10CE8C7F55`; `MarcusAwakeTransport.dll` `DE81809AC08EA86BE90FD6F0AB77FA348B24B744588B0AF970DFFF2080E6DF48`。
- Runtime package: `RUNTIME_PACKAGE_OK`; self-contained `win-x64`; `195` payload files plus `manifest.json` and `SHA256SUMS.txt` (`197` files); manifest SHA-256 `58A601B48B32F03B6650AEA4B8694A22F262465ABB94A9D688B8DE11886122AC`; sums SHA-256 `765160E3F2424D3B6E5582AFE422B8C105770701A6B52A71F4C24719A25AF422`。
- Offline integration: production smoke `17/17 PASS`; sync tests `15/15 PASS`; release check `RELEASE_CHECK_OK` with `RELEASE_STATUS=CANDIDATE_SYNC_OK` after the game copy was updated.
- Current P3A evidence: phase-scoped verifier `PASS` (`build=0`, `api=True`, `forbidden=True`, source/assembly findings `0`, scanned `22`, deferred `53`); evidence file is `docs/evidence/MARCUS-AWAKE-P3A-E1-20260829-002.json`.
- latest_skip_game_sync: `docs/sync-reports/sync-20260830-003245689.json`; state `verified`, `skip_game=true`, `confirm_game_sync=false`, process snapshot before sync empty.
- latest_game_sync: `docs/sync-reports/sync-20260829-marcus-embedded-002-game-runtime-fix.json`; state `verified`, process snapshot before sync empty, `197` Runtime files copied, `0` files removed.
- durable_pre_sync_backup: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE-backups\game-AWAKE-before-awake-20260829-marcus-embedded-002-20260830-014707`; manifest SHA-256 `9312E25653A138CC55C21B5AB0506688939051E478A94BF1C2DDAFC5B2E7ED46`.
- offline_delivery_audit: `21/21 PASS`; active docs, evidence anchors, BuildId attribution, source/dist hashes, staging and sync report are consistent.
- Clean deliverable: strict staging at `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE-release-staging\awake-20260829-marcus-embedded-002`; allowlist `972` files; staging report `docs/sync-reports/release-staging-awake-20260829-marcus-embedded-002.json`; no source, docs, task queue, PDB, log, or secret-bearing development files included.
- Game copy: `Awake.dll`, embedded Framework/Transport, and the `197`-file Runtime package now match source/dist; game Runtime manifest SHA-256 is `58A601B48B32F03B6650AEA4B8694A22F262465ABB94A9D688B8DE11886122AC`; no files were removed and preserved files were verified.
- Static boundary note: current `verify_marcus_awake_api_layers.ps1` passes (`109/109` legacy API preserved, `199` current public types, `90` additions classified, `0` unclassified), and the phase-scoped `verify_marcus_awake_p3a.ps1` passes (`build=0`, `api=True`, `forbidden=True`, source/assembly findings `0`, scanned `22`, deferred `53`). Only the prior broad forbidden-scan rule is historical; it is not the current embedded-architecture gate because the migrated Framework intentionally owns IPC/process/runtime path code.

## Closed P3D-A2 Batch (Historical)

- task_id: `MARCUS-AWAKE-P3D-A2-STREAMING-FALLBACK-20260828`
- status: `offline_verified`; execution_lease: `released`; blocker: `none_for_offline_batch`
- checkpoint: `docs/checkpoints/MARCUS-AWAKE-P3D-A2-20260828-checkpoint.md`
- plan: `docs/PLAN-MARCUS-AWAKE-P3D-A2-STREAMING-FALLBACK-20260828.md` revision 8
- verification: A2 Service/IPC harness `21/21`; Framework Core `20/20` plus P3D-A2 Framework `6/6`; Provider, Transport and Storage regressions passed；MCM 静态契约 `47/47`、AWAKE caller 静态契约 `7/7`；AWAKE Release build has `0` errors and four pre-existing `CS1998` warnings.
- latest_local_validation: `docs/evidence/MARCUS-AWAKE-P3D-A2-20260828.json`; run_id=`p3d-a2-53e2a88c0d174f449d589de17341ae27`; file SHA-256=`3CC2107B0AA5F32C3FC077A38B0928E581FD44CCC16607EEA6878C08E40FD5D5`; `21/21` pass; no external network, real cloud Provider, Bannerlord startup or game-directory sync.
- next_action: none for this closed batch; its checkpoint remains a historical evidence record. The top-level checkpoint above is the sole active recovery source. Real Provider, game-directory sync, and Bannerlord E3/E4/E5 remain later gates. S07 cancellation and S14 late-event cases are covered.

## Offline Mechanism Audit (2026-08-30)

- `REVIEW_TARGET`: Marcus-AWAKE embedded runtime slice after `002` synchronization; game-side lifecycle, dialogue, events, memory, storage, Runtime Service, IPC, Provider, UI dispatch, and cross-save boundaries.
- `REVISION`: `MARCUS-AWAKE-OFFLINE-AUDIT-20260830`; one baseline pass plus one targeted convergence pass completed under the updated review rules.
- `DECISION`: `REVISE_CURRENT_SLICE`; `17` deduplicated findings are recorded in `docs/AUDIT-MARCUS-AWAKE-OFFLINE-20260830.md`, with priority fixes led by persistence truthfulness, reservation safety, load ordering, campaign isolation, thread/session boundaries, and world-event provenance.
- Scope result: no source, build artifact, `002` candidate, staging candidate, or game-directory runtime file was changed by this audit; two audit-only temporary files were removed and verified absent.
- Evidence boundary remains `E3`; Bannerlord E4 gameplay and E5 save/load/long-run evidence are still pending. Any implementation fix must use a new BuildId and invalidate no part of the frozen `002` evidence retroactively.
- Next action: prepare a separate approved repair batch from the report; do not modify `awake-20260829-marcus-embedded-002` while it awaits user-run validation.

## Recorded Embedded API and Package Evidence — 2026-08-29

- `docs/evidence/MARCUS-AWAKE-API-LAYERS-E1-20260829.json`: PASS；旧 P3A API `109/109` 保留，当前内置 Framework 公共 API `199`，新增 `90`，全部归入 P3A/P3B/P3C/P3D/P5，无未分类类型或未授权静态命中。
- `docs/evidence/MARCUS-AWAKE-P3-API-SURFACE-BASELINE-CURRENT-20260829.json`: 当前合并 API 基线；旧 `MARCUS-AWAKE-P3-API-SURFACE-BASELINE-20260826.json` 保留为不可变历史 P3A-109 基线，未覆盖。
- 自包含 Runtime 包已在记录构建中重建到 `dist`，`RUNTIME_PACKAGE_OK`，包含 `195` 个 payload + `manifest.json` 与 `SHA256SUMS.txt`（共 `197` 个文件）；记录的 dist manifest SHA-256 为 `58A601B48B32F03B6650AEA4B8694A22F262465ABB94A9D688B8DE11886122AC`，`SHA256SUMS.txt` SHA-256 为 `765160E3F2424D3B6E5582AFE422B8C105770701A6B52A71F4C24719A25AF422`；记录构建输出与 `dist` 产物一致，但不代表当前 source 工作树已重建。
- `release_check.ps1` 的记录结果为 `RELEASE_CHECK_OK` / `RELEASE_STATUS=BLOCKED_SYNC`；该结果对应记录构建快照，source 漂移后必须重新运行；游戏目录仍为旧候选，缺少新的内置 Framework、Transport 和 Runtime 包，未进行覆盖。
- 记录的离线构建 BuildId：`awake-20260829-marcus-embedded-001`；`Awake.dll` source/dist SHA-256：`337E4C0B8D32B13912C30A45EE59B38BC1728027357B00367996C6C455616478`；`MarcusAwakeFramework.dll`：`00B34BF79DEFADBE4A946612244EA8631CE777495C2B99ADD11F2B10CE8C7F55`；`MarcusAwakeTransport.dll`：`DE81809AC08EA86BE90FD6F0AB77FA348B24B744588B0AF970DFFF2080E6DF48`；这些身份不是当前最终候选。
- latest_skip_game_sync: `docs/sync-reports/sync-20260829-212959651.json`；该记录为 `state=verified`、`skip_game=true`、`0` 个游戏目录变更，完成时间为 `2026-08-29T13:30:26.9432261Z`（Asia/Shanghai `21:30:26`）。

## Repair Sequence Control Plane — 2026-09-02

- Approved by user: execute all repair batches in fixed order; current batch is `WORLDBOOK-STUDIO-PUBLIC-CONTRACT-20260902`.
- Batch status: `revision_verified_pending_independent_signoff`; checkpoint: `docs/checkpoints/WORLDBOOK-STUDIO-PUBLIC-CONTRACT-20260902-checkpoint.md`.
- Evidence: Web/CLI Release `0 warnings / 0 errors`; contract checker `1132/1132`; main harness `113/113`; AuthorityGate `5/5`; public contract smoke `PASS`; full editor/Batch/Draft regression `PASS`.
- Current boundary: no Bannerlord launch, no game-directory sync, no real Provider/API key; route registry is an explicit partial design catalog, not a claim of 79 wired routes.
- Next action: record independent final verdict and user signoff, then open a new review state for `WORLDBOOK-STUDIO-AI-AUTHORING-20260902`.
- Sequence order: public contract → Draft/Batch authority unification → split/merge/review UX → three workstations → package and customer acceptance.
## Repair Sequence Control Plane — 2026-09-02

- Approved by user: execute all repair batches in fixed order; current batch is `WORLDBOOK-STUDIO-AI-AUTHORING-20260902`.
- Previous batch: `WORLDBOOK-STUDIO-PUBLIC-CONTRACT-20260902` is `approved`; checkpoint: `docs/checkpoints/WORLDBOOK-STUDIO-PUBLIC-CONTRACT-20260902-checkpoint.md`.
- Current batch status: `review_active`; plan: `docs/PLAN-WORLDBOOK-STUDIO-AI-AUTHORING-20260902.md`; review state: `docs/review-state/WORLDBOOK-STUDIO-AI-AUTHORING-20260902.json`.
- Current scope: Draft/Batch lifecycle authority, source/evidence reuse, candidate cardinality, layered review state, and duplicate confirmation removal.
- Boundaries: no Bannerlord launch, no game-directory sync, no real Provider/API key; merge/split UX and three-workstation integration remain later batches.
- Next action: complete independent read-only review, record user-approved repair, then implement only this structural slice.
## Worldbook Studio Focus

- task_id: WORLDBOOK-STUDIO-V2-PIPELINE-20260827
- status: offline_verified；Studio v2 compile/export 与 AWAKE 完整性、加载、查询离线链路已修复并通过验证。
- execution_lease: released；本批次没有待执行的代码写租约。
- checkpoint: docs/checkpoints/WORLDBOOK-STUDIO-V2-PIPELINE-20260827-checkpoint.md。
- candidate: Studio 程序发行包 `tools/worldbook-studio/artifacts/content-candidate-20260827-v2-boundary-repair/WorldbookStudio-win-x64.zip`；SHA-256 `51C928768D01869FF176D432CF19B22407EB0DD322FDA77F13B2130EBAADD11D`。v2 内容候选由 `export` 在工作区候选目录中生成，并由 F76–F78 端到端消费验证，不嵌入该 ZIP 顶层。
- verification: Studio harness 108/108（含 F79–F81）、Editor content core 7/7、BatchTests 13/13、Draft tests 10/10、Release build 0 warnings / 0 errors、A4 CLI/Web 18/18、候选目录 release-check PASS、AWAKE worldbook-runtime-smoke PASS；最高证据等级 E2。
- next_action: 为真实 v2 世界知识内容建立独立迁移批次，再决定是否生成新的 AWAKE runtime 候选；迁移前不覆盖当前 v1 `ModuleData`，不启动 Bannerlord、不同步游戏目录。
- boundaries: 当前只是 Studio 与 AWAKE 消费链路的离线验证；未完成真实云端 Provider/Worker、真实浏览器 DOM、Bannerlord 运行时、游戏目录同步、存档验证或 E3/E4/E5；不修改冻结 AWAKE 候选。

## AWAKE World Knowledge Independent Focus — 2026-08-28

- `task_id`: `AWAKE-KNOWLEDGE-INDEPENDENT-20260828`
- `status`: `offline_verified`
- `execution_lease`: `released`；本批实现、验证和债务审查已完成
- `plan`: `docs/PLAN-AWAKE-KNOWLEDGE-INDEPENDENT-20260828.md` revision 10；Revision 9 独立只读复审 `VERDICT: APPROVED`
- `checkpoint`: `docs/checkpoints/AWAKE-KNOWLEDGE-INDEPENDENT-20260828-checkpoint.md`
- `verification`: `WorldbookRuntimeSmoke` Release-r16 `0 warnings / 0 errors`、exit `0`；`WorldbookRuntimeProductionSmoke` Release-r16 `0 warnings / 0 errors`、`15/15 PASS`；限定范围债务审查通过；最高证据 `E2`
- `next_action`: 单独建立真实 v2 世界知识内容迁移/包候选批次；先解决 Marcus Framework 的 `netstandard, Version=2.0.0.0` 正式构建阻塞，再考虑匹配 BuildId 的 E3/E4/E5
- `boundaries`: 本批未修改 `AWAKE.csproj`、`SubModule.xml`、`ModuleData`、`dist`、Marcus framework、游戏目录或冻结候选；未启动游戏、未同步目录、未取得 E3/E4/E5；正式 AWAKE 构建失败原因已记录在 checkpoint

## Product Direction Discussion (Non-Execution)

- `task_id`: `AWAKE-BIG-DIRECTION-DISCUSSION-20260826`
- `status`: `contract_ready_for_user_signoff`; `evidence_level`: `E0`; `execution_lease`: `none`; `blocker`: `user_signoff_required`
- `checkpoint`: `docs/checkpoints/AWAKE-NATIVE-KNOWLEDGE-B3-20260826-checkpoint.md`
- `previous_checkpoint`: `docs/checkpoints/AWAKE-BIG-DIRECTION-20260826-checkpoint.md`
- `active_plan`: `docs/PLAN-AWAKE-NativeKnowledge-B3-InquiryOnly-20260826.md`
- `implementation_contract`: `docs/PLAN-AWAKE-NativeKnowledge-B3-IMPLEMENTATION-CONTRACT-20260826.md`
- `implementation_contract_review`: `APPROVED`（第二轮独立复核）
- `user_signoff`: `false`; `implementation_authorized`: `false`
- `save_protocol`: `docs/AWAKE-DISCUSSION-SAVE-PROTOCOL.md`
- `scope`: AWAKE 产品方向、季度玩家周报、世界事件、玩家知识、NPC 认知和多阶段 AI 接口讨论；不替换当前 Worldbook Studio 实现批次。
- `next_action`: 用户签收 B3 实施合同后，进行一次实现前 Bannerlord/AI/存档边界复核，再创建实现租约；签收前不改代码、不构建、不同步游戏目录。
- `important_conflict`: 现有 `WORLDBOOK-EVENT-REPORT-CONTRACT-v1.md` 仍是 7 个游戏日 `weekly-report.v1`；季度玩家报告是新的产品方向，尚未迁移实现。B3 方案和实施合同已通过独立审查，但入口、通用副作用隔离、询问记忆过滤和调用计数仍没有实现证据。
## Other Paused Focus

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `status`: `offline_verified`; P2/P3A、P3B 核心 IPC 与 P3C Storage/RAG 均已通过离线验证；不代表 Provider、MCM、AWAKE caller 或游戏运行已完成。
- `checkpoint`: `docs/checkpoints/MARCUS-AWAKE-P3C-STORAGE-RAG-20260827-checkpoint.md`。
- `next_action`: 进入 P3D Provider/凭据设计与独立审查；继续保持 AWAKE 主工程、SubModule 和游戏目录不变。

## Previous Offline Focus

- `task_id`: `WORLDBOOK-STUDIO-A4-LAUNCHER-20260823`
- `status`: `offline_verified`; A4 CLI/Web and A4-Launcher lifecycle refinement were independently offline verified.
- `checkpoint`: `docs/checkpoints/WORLDBOOK-STUDIO-A4-LAUNCHER-20260823-checkpoint.md`.
- Prior package ZIP SHA-256: `b7d9e7f92cd7649702a39b4ca677514868d3aa86bfbf17635abca6e58433fe78`.

## Worldbook Studio A4-Launcher Batch

- Implemented the bounded lifecycle refinement: explicit Host state transitions, idempotent start/stop task reuse, cancellation-safe final cleanup, Job/process fallback, real exit snapshots, mutex ownership separation, deferred WinForms close, injectable browser/host/environment seams, and structured Launcher failures.
- Workspace recovery now distinguishes first run from invalid saved state: missing or corrupt saved paths are not recreated or silently rewritten; successful user-selected replacement uses atomic settings/marker commit with rollback on write failure.
- Verification: Release build `0 warnings / 0 errors`; Launcher seam tests `14 PASS`; Studio harness `99/99 PASS`; six final package smoke scenarios passed; extended A1 CLI/Web smoke `18/18`; package release-check passed; package manifest `589` files.
- Evidence: `tools/worldbook-studio/artifacts/WorldbookStudio-smoke/launcher-tests.v2.json`, `tools/worldbook-studio/artifacts/WorldbookStudio-smoke/clean-start.v2.json`, `tools/worldbook-studio/artifacts/WorldbookStudio-smoke/browser-failure.v2.json`, `tools/worldbook-studio/artifacts/WorldbookStudio-smoke/stale-settings-missing.v2.json`, `tools/worldbook-studio/artifacts/WorldbookStudio-smoke/stale-settings-marker.v2.json`, `tools/worldbook-studio/artifacts/WorldbookStudio-smoke/duplicate-launch.v2.json`, `tools/worldbook-studio/artifacts/WorldbookStudio-smoke/graceful-shutdown.v2.json`, `tools/worldbook-studio/artifacts/WorldbookStudio-smoke/a1-cli-web-20260823-final.json`, `docs/evidence/worldbook-studio-a4-launcher-debt-20260823.md`.
- Evidence level: `E2` offline verified. No Bannerlord runtime, game-directory synchronization, save/load, real cloud Provider/Worker, or frozen candidate change is claimed.
- Documentation refresh: added `tools/worldbook-studio/新手指引_世界书内容编辑者.md`; the package now includes a zero-based author guide covering startup, authoring, permissions, preview, save/export, troubleshooting and step-by-step AI assistant use.
- Terminology clarification: the guide now names `era` as “知识适用时期”, covering when knowledge occurs, is valid or applies; it explicitly states that this field is not a game-event trigger.

## Worldbook Studio A4 CLI/Web Batch

- Extracted CLI/Web `SuggestionEnvelope` semantic field selection into one internal pure `SuggestionSemanticProjection.Project` seam while preserving separate snake_case/camelCase DTOs, CLI exit codes, Web status/error mappings, consent/session/CSRF/nonce/CAS lifecycles and public commands/routes.
- Added the hand-captured A4 contract golden with independent eleven-field projection values, exact friend assemblies, public Core/CLI/Web baselines, 21 Schema raw-byte hashes, 96→99 named-case manifest and 18 complete smoke expectations.
- Verification: full solution Release build `0 warnings / 0 errors`; Studio harness `99/99 PASS`; default A1 CLI/Web smoke `PASS`; extended CLI/Web smoke `18/18 PASS`; existing package release-check `PASS`; worldbook-studio-plan JSON parse `21` files `PASS`; A4 debt audit `passed` with denominator `3429` logical lines across 6 changed files, `confirmed=0`, `suspected=8`, `static_risk=688`, `unknown=0`.
- Evidence: `docs/evidence/WORLDBOOK-STUDIO-A4-CLI-WEB-20260823-smoke.json`, `docs/evidence/worldbook-studio-a4-cli-web-debt-20260823.json`, `docs/checkpoints/WORLDBOOK-STUDIO-A4-CLI-WEB-20260823-checkpoint.md`.
- Evidence level: `E2` offline verified. No real cloud Provider/Worker, Bannerlord runtime, save/load, game-directory synchronization, repackaging or frozen-candidate change is claimed.

## Worldbook Studio A3.4 Registry Snapshot Batch

- Extracted registry JSON-to-`RegistrySnapshot` projection and profile parent-chain diagnostics into one internal pure `RegistrySnapshotBuilder.Build` authority without changing file reads, SnapshotInputStore, Schema diagnostics, hashes, profile chains, editor catalog or Preview output.
- Verification: harness `96/96 PASS`; Release build `0 warnings / 0 errors`; A1 CLI/Web authority smoke `PASS`; existing package release-check `PASS`; worldbook-studio-plan JSON parse `21` files `PASS`; A3.4 debt audit `passed` with `confirmed=0`, `suspected=8`, `static_risk=212`.
- Evidence level: `E2` offline verified. A1 package hash is preserved; no package rebuild, game-directory synchronization, Bannerlord runtime, save/load, cloud Provider or local Worker evidence is claimed.

## Worldbook Studio A3.3 Preview Batch

- Extracted Preview's document/assertion/expression permission projection into one internal pure `PreviewProjectionBuilder.Build` authority without changing deny precedence, profile inheritance, identity dimensions/thresholds, layer gating, referral filtering, source distinct order, diagnostics, Envelope or DTO output.
- Verification: harness `93/93 PASS`; Release build `0 warnings / 0 errors`; A1 CLI/Web authority smoke `PASS`; existing package release-check `PASS`; worldbook-studio-plan JSON parse `PASS`; A3.3 debt audit `passed` with `confirmed=0`, `suspected=4`, `static_risk=134`.
- Evidence level: `E2` offline verified. A1 package hash is preserved; no package rebuild, game-directory synchronization, Bannerlord runtime, save/load, cloud Provider or local Worker evidence is claimed.

## Worldbook Studio A3.2 Content Graph Batch

- Extracted `BuildContentGraph` into one internal pure `ContentGraphBuilder.Build` authority without changing graph traversal, node/edge order, de-duplication, tier closure, diagnostics, Schema validation or compile output.
- Verification: harness `90/90 PASS`; Release build `0 warnings / 0 errors`; A1 CLI/Web authority smoke `PASS`; existing package release-check `PASS`; worldbook-studio-plan JSON parse `PASS`; A3.2 debt audit `passed` with `confirmed=0`, `suspected=4`, `static_risk=201`.
- Evidence level: `E2` offline verified. A1 package hash is preserved; no package rebuild, game-directory synchronization, Bannerlord runtime, save/load, cloud Provider or local Worker evidence is claimed.

## Worldbook Studio A3.1 Authoring Template Batch

- Extracted `BuildAuthoringTemplate` into an internal pure factory without changing `CreateDocument` normalization, validation precedence, registry binding, JSON/YAML output or save/re-read behavior.
- Verification: harness `87/87 PASS`; Release build `0 warnings / 0 errors`; A1 CLI/Web authority smoke `PASS`; A3.1 debt audit `passed` with `confirmed=0`, `suspected=4`, `static_risk=249`.
- Evidence level: `E2` offline verified. A1 package hash is preserved; no package rebuild, game-directory synchronization, Bannerlord runtime, save/load, cloud Provider or local Worker evidence is claimed.

## Worldbook Studio A2 Snapshot Batch (closed)

- Same-byte parsing/hash, operation-scoped immutable snapshot, complete input closure, read-inventory equality, bounded TOCTOU retry, revision rollback protection and staging/atomic output protection remain verified in `docs/checkpoints/WORLDBOOK-STUDIO-A2-SNAPSHOT-20260823-checkpoint.md`.
- A2 verification remains `83/83 PASS`, Release build `0 warnings / 0 errors`, A1 smoke `PASS`, debt audit `passed`.

## Worldbook Studio A2 Snapshot Batch

- Same-byte parsing/hash, operation-scoped immutable snapshot, complete input closure, read-inventory equality, bounded TOCTOU retry, revision rollback protection and staging/atomic output protection are implemented.
- Verification: Release build `0 warnings / 0 errors`; Studio harness `83/83 PASS`; A1 CLI/Web authority smoke `PASS`; debt audit `worldbook-studio-a2-snapshot-20260823.json` passed with `confirmed=0`, `suspected=4`, `static_risk=488`.
- Evidence level: `E2` offline verified. Real Provider/Worker, Bannerlord runtime, save/load and game-directory synchronization remain unverified. A1 package hash is preserved; A2 was not repackaged.

## Worldbook Studio A1 Authority Batch

- `72/72` Studio harness cases pass; Release build reports `0 warnings / 0 errors`.
- Real subprocess/HTTP smoke passes CLI and Web Provider validation, consent/analyze flow, revision/hash authority and separate snake_case/camelCase Suggestion wire contracts.
- Package `release-check`, Launcher success smoke and browser-open failure fallback smoke pass.
- Debt audit: `tools/code-debt-audit/reports/worldbook-studio-a1-authority-20260823.json`; denominator `1937` logical lines; confirmed `0`; suspected `8`; static risk `0`; scope limited `false`.
- A1 changed only Worldbook Studio source/tests/fixtures/smoke and audit/status records; it did not change Schema, routes, CLI commands, AI contracts, `Modules\\AWAKE`, `PlayerExports`, dist or the frozen runtime candidate.

## Previous Current Thread Focus

- `task_id`: `WORLDBOOK-STUDIO-LAUNCHER-20260822`
- `status`: `offline_verified`; the editor-friendly Windows x64 self-contained package is built and verified without touching the frozen runtime candidate or game directory.
- `execution_lease`: none; Launcher/Studio source and package only. Do not synchronize `Modules\\AWAKE`, `PlayerExports`, dist or frozen candidates.
- `checkpoint`: `docs/checkpoints/WORLDBOOK-STUDIO-LAUNCHER-20260822-checkpoint.md`.
- `next_action`: user manually double-clicks `tools/worldbook-studio/artifacts/WorldbookStudio-win-x64.zip`'s Launcher and verifies the Chinese first-run dialog, workspace selection and real default-browser opening.
- Final package SHA-256 sidecar: `369654363203f220ed9c3047dc1de063bf18dd88b497b7581a467f0f7dafde19`.
- Previous focus remains recorded below: `WORLDBOOK-AI-20260822` and its checkpoint are independent and complete.

- `task_id`: `WORLDBOOK-AI-20260822`
- `status`: `offline_verified`; the Worldbook Studio AI assistance batch is complete for source/package validation.
- `execution_lease`: none; do not touch the frozen candidate or synchronize the game directory.
- The frozen runtime candidate `awake-20260820-syncpack-001` remains unchanged and continues to await E4/E5 evidence independently.
- `next_action`: if the user configures a cloud Provider or local Worker, run one bounded demo-fixture consent/analyze call; otherwise use the packaged Studio for authoring.
- Checkpoint: `docs/checkpoints/WORLDBOOK-AI-20260822-checkpoint.md`.
- New-dialog handoff: `docs/HANDOFF-PERSONA-PLATFORM-20260821.md`.

## Worldbook Studio AI Provider and Assistance Batch

- `task_id`: `WORLDBOOK-AI-PROVIDER-AUDIT-20260823`
- `status`: `offline_verified`; source and package only, independent of the frozen runtime candidate.
- Added a Chinese “云端设置” dialog for HTTPS endpoint, model and API Key; the Key is stored only in the Windows DPAPI-protected local settings file and is never returned in plain text.
- Added ten AI focus choices: comprehensive review, completion, fact correction, knowledge permissions, hierarchy, expression level, metadata, world style, NPC voice and domain style. They share the existing consent/provider/CAS path.
- Added clearer scope/help text, enhanced suggestion cards with category/severity/confidence/affected fields/candidate text, and a local-only check button that works without a Provider.
- Verification: `F01-F70 PASS`, strict request schema `PASS`, Studio contract JSON parse `PASS`, front-end syntax `PASS`, Release `0 warnings / 0 errors`, package/release-check `PASS`, launcher smoke `PASS`, in-app Browser DOM/interaction smoke `PASS` with no console errors or warnings.
- External status: cloud and local Worker are not configured for this verification; no real external call, game launch, runtime worldbook write or frozen candidate change was performed.

## Active Hardening Goal

- `task_id`: `EVENT-REPORT-CONTRACT-20260822`
- `status`: `offline_verified`
- `execution_lease`: none; frozen candidate and game directory remain out of scope.
- Draft plan: `docs/PLAN-AWAKE-WorldbookHardening-20260822-DRAFT.md`.
- Checkpoint: `docs/checkpoints/EVENT-REPORT-CONTRACT-20260822-checkpoint.md`.
- Current next action: close the event/report source-only batch; no candidate preparation or game sync is authorized by this checkpoint.
- Review evidence: local Codex CLI Round 1/2 returned `VERDICT: REVISE`; Round 3 returned `VERDICT: APPROVED`; the multi-agent reviewer path separately encountered `502/503`.

## Worldbook Platform Batch

- Contract root: `tools/worldbook-contract/v1/`.
- Studio output: `runtime.json`, `index.json`, `package-manifest.json`, `awake.runtime_mapping_report.v2`.
- Runtime entry: v2-only `WorldbookRuntime` → `IWorldKnowledgeQuery` → NPC dialogue prompt input.
- Player edit path: terminal status/search → edit existing summary → CAS Overlay → campaign `SyncData` / JSON export.
- Registry/activation path: v2 registry → explicit/default universe selection → package manifest hash validation → campaign activation export/import.
- Identity path: explicit known identity → role/office → inheritance → age/management/skills → culture/kingdom/settlement conditions → deny-before-grant resolution.
- Weekly report path: structured event ledger → deterministic four-domain `weekly-report.v1` → current text UI; no per-NPC AI learning loop.
- Event/report contract path: `WorldEventServices` is the only production façade; `WorldEventContract` projects Ledger records to `event-record.v1` and validates report/source closure offline.
- Worldbook Studio editor path: authoring document list/read/new → Chinese five-step author mode or advanced YAML/JSON mode → save-and-validate → profile preview → compile → isolated candidate export; Web and CLI still share `WorldbookApplicationService`.
- Offline evidence: Studio `F01-F62 PASS`, author-mode browser closure PASS, clean first-load page with no current page/console errors, ZIP success/failure smoke PASS, Runtime smoke `PASS` including event-ledger replay/concurrency/capacity checks, Contract JSON parse `17 files`, AWAKE Release build `0 warnings / 0 errors`, Studio package/release-check `PASS`.
- Studio package: `tools/worldbook-studio/artifacts/WorldbookStudio`; source-only, independent of the frozen runtime candidate.
- Current source build DLL SHA-256: `97F79C9160394CDCE8786BEB860E6D8B00532DA256D6ED33A5AA05A2912FEB05`.
- This source build is not synchronized; `dist` and game remain frozen candidate `awake-20260820-syncpack-001` with hash `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`.

## Independent Offline Tool Batch

- `task_id`: `PERSONA-WORKBENCH-KEYWORD-PROMPT-20260821`
- `status`: `offline_verified`; independent of the frozen AWAKE runtime candidate below.
- Canonical Workbench preview now uses `[PERSONA_LOAD]`, seven fixed constraint tokens, English stable keyword IDs and explicit `DATA_CN` prose markers.
- Budget trimming now removes optional public/private prose before removing their keyword sections and no longer references obsolete section names.
- The production `/api/provider/convert-to-dsl` route now uses the existing sparse conversion client when a local display name is present; the established full-draft route remains only for empty-name automatic extraction and compatibility hosts.
- The tested baseline is `r48`: `tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-r48-20260821`; a separate Usage-diagnostic fix candidate is `tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-r48-usage-fix-20260821`.
- The r48 local Ollama pretest is now recorded as a real negative result, not a release pass: short expansion/DSL and long expansion timed out; long DSL reached candidate parsing but failed nested-field validation.
- Verification: Core/Web PASS ALL after Usage action-layer fix; Release build 0 warnings / 0 errors; fix candidate self-contained publish, manifest and HTTP smoke pass; r48 live Ollama test used the expected `gpt-oss:20b` digest and produced no accepted expansion/DSL output. No Workbench service remains running.
- `next_action`: design a bounded local Ollama pretest strategy and rerun the fixed short/long fixtures only after that strategy is reviewed; keep Luna and AWAKE runtime integration separate.

## PersonaWorkbench Authoring-v2 Migration Preview — 2026-08-30

- Status: source implementation verified to E2 within the isolated Workbench boundary; no AWAKE runtime, game directory, frozen candidate, save data, or live Persona-card behavior was changed.
- Workbench now maps the v1 form snapshot to `awake.persona.authoring.v2` through embedded, hash-checked schema/crosswalk/canonicalization/registry assets and exposes a read-only preview plus canonical JSON download.
- Separate AI expansion text remains provenance data and is not substituted into authored Core; unmapped legacy values remain in migration data with warnings; failure responses are non-destructive.
- Core/Web regression harnesses and Release Web build pass; source HTTP smoke and Worker-low Ollama diagnostic pass.
- The fixed historical r48 package was correctly rejected before startup because it lacks the runner-required `BUILD-ID.txt` and `BUILD-SOURCE-MANIFEST.sha256.txt`; this is a package-evidence issue, not a source implementation failure.
- A correctly attributed current K1A package passed the four real local Workbench Provider routes with the fixed fixtures, model `gpt-oss:20b` and required digest, serial/no-retry execution, accepted outputs, usage data, and complete cleanup. Reports: `tools/persona-workbench/tools/pretest-report-k1a-20260830-run2.json` and `tools/persona-workbench/tools/ai-link-evidence-k1a-20260830.json`.
- This remains an E2 Workbench result only; it does not claim AWAKE runtime, game entry, save/reload, or live Persona-card behavior. Next action: use the authoring-v2 JSON as the input contract for a separately reviewed runtime/selection/export batch.

## Active Batch

- `task_id`: `PREP-20260820-1`
- `batch_id`: `awake-next-round-preflight-20260820`
- `status`: `pending_game`
- `execution_lease`: none; E3 synchronization completed and the candidate is frozen; game launch and E4/E5 validation require the user
- `next_action`: user launches Bannerlord with the frozen candidate, exercises the E4 checklist, exits, and provides the matching AWAKE log plus save/load evidence for E5

## Delivered Scope

- One workflow dispatcher, narrow continuity/short-task/Worker Skills, compact state files, one-active-batch/one-candidate rules, E0-E5 evidence levels, BuildId attribution, DLL/worldbook fingerprints and sync-aware release checks.
- Bannerlord was not started; the game directory was not overwritten; version remains `v0.2.0`.

## Responsibilities

- `bannerlord-mod-development-orchestrator`: task classification, routing, lease, stopping, evidence and final report.
- `awake-task-continuity`: restore this file, the active checkpoint, blocker and one recorded next step only.
- `long-horizon-short-task-execution`: execute approved bounded tasks and continue automatically inside the lease.
- `local-ollama-batch-worker`: explicit repetitive batch screening only; cloud remains the authority for decisions and mutations.
- `abort-aware-execution`: terminal handling after cancellation or detachment.

## Preflight Finding

- E3 synchronization completed while Bannerlord was closed; release status is `CANDIDATE_SYNC_OK`.
- The new candidate is present in source, dist and game; all three managed DLL hashes match.
- Source, dist and game now use the canonical Persona Definition paths; nested Persona file count is 0.
- Release check passes source/dist/game Persona layout and reports `CANDIDATE_SYNC_OK`.
- E3 is complete; remaining evidence is gameplay and save/load validation.

## Frozen Candidate

- Runtime version: `v0.2.0`.
- BuildId: `awake-20260820-syncpack-001`.
- Source/dist DLL SHA-256: `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`.
- Game DLL SHA-256: `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`.
- Worldbook manifest SHA-256 (source/dist/game): `2115664E1BB6BB26E63D8CB2097B891B51C03A00504D76DF6F8520FD9448468A`.
- Worldbook counts: 759 JSON files; 335 rules; 415 legacy personality/background files.
- Candidate state: `pending_game`; do not modify the frozen candidate before E4/E5 evidence is collected.

## Evidence

- `E0`: governance design, approved plan, project rules and four Skills updated.
- `E1`: Skill validation, JSON/XML parse, 1.3.15 rebuild, and SDK test-project rebuild passed; 3 pre-existing CS1998 warnings remain. 1.4.8 is not runnable locally because no matching 1.4.8 game root/reference set exists.
- `E2`: `Awake.SdkSmoke` PASS ALL; localization `226/259/259`; asset boundary `117`; release check OK; MAF Preview lint exit 0.
- `E2 limitation`: worldbook placeholder audit reports 15 pre-existing content findings; they require a separate content-fix batch and were not silently edited here.
- `E3`: verified; source/dist/game DLL and manifest hashes match, canonical Persona paths exist in all three locations, nested Persona count is 0, process snapshot before sync was empty, and transaction rollback was not needed.
- `E4`: pending user gameplay with BuildId `awake-20260820-syncpack-001`.
- `E5`: pending user save/load and timeline-isolation evidence.

## Blockers and Rules

- The `0.2.1` validation task remains `blocked_sync`; this governance batch does not complete it.
- A frozen runtime candidate may not be changed while awaiting game validation. New runtime changes require a new BuildId and candidate record.
- `429`, cancellation, detachment, external timeout or ambiguous result stops the lease and requires a checkpoint before any retry.
- Game-directory synchronization requires explicit user authorization after the user has exited Bannerlord.

## Checkpoint

- `docs/checkpoints/PREP-20260820-1-checkpoint.md`
- `docs/sync-reports/dist-repair-20260820-230850.json`
- `docs/sync-reports/e3-sync-20260820-231150.json`
- Historical queue: `docs/AWAKE-Task-Queue-20260816.md` (archive/traceability source, not the compact recovery source).

## Marcus-Awake Embedded Migration — 2026-08-24 continuation

- `MARCUS-AWAKE-FULL-CAPABILITY-INVENTORY-v1-20260824.md` now serves as the full capability baseline, covering F-001–F-066.
- The baseline includes all framework domains: lifecycle/SDK, GameData, context/RAG, Gateway, six Provider adapters, streaming/cancellation, structured output, events, storage, timeline, commands, IPC, credentials, media/CAS, MCM, DevTools, observability, compatibility and migration.
- The four Marcus AuthorSource extensions are treated as complete behavior references and AWAKE integration acceptance slices, not as code to place inside Framework Core.
- `MARCUS-AWAKE-CAPABILITY-CARRYOVER-MATRIX-v1-20260824.md` has been upgraded in place to v2 semantics; the filename remains stable for existing references.
- `PLAN-MARCUS-AWAKE-EMBEDDED-20260824.md` now contains Phase 0.5 full inventory and Phase 1.5 API/protocol/storage/failure contract lock.
- Recovery checkpoint: `docs/checkpoints/MARCUS-AWAKE-FULL-CAPABILITY-20260824-checkpoint.md`.
- P1.5 contract is approved and hash-pinned; P1 Framework Core is offline-verified; P2's current write set passed the Round 8 incremental review and is offline-verified with E1 evidence. The next gate is an independent P3 Runtime Service / Provider / IPC / SQLite-RAG plan review. Do not touch `AWAKE.csproj`, `SubModule.xml`, runtime code, build, sync, or the game directory until that gate is approved.











## Worldbook Studio AI Draft Workflow — 2026-08-24 final

- Status: offline_verified; the reference-to-draft workflow is implemented and packaged.
- Workflow: external TXT/Markdown/YAML/JSON or pasted text → AI extracts objective facts with evidence → editor accepts facts one by one → AI generates metadata → editor selects/checks category → AI generates identity expressions → editor accepts them → creates a needs_review author draft. AI output never enters canon automatically.
- Safety refinements: expression must bind exactly one fact and at least one registered identity; invalid fact kinds/layers are rejected instead of defaulted; missing/invalid category is rejected instead of defaulting to politics; duplicate create requests for one draft return the same document.
- Verification: Release build 0 warnings / 0 errors; Studio harness 101/101 PASS; Draft tests 9/9 PASS; offline Web/Worker draft Smoke passed including duplicate-create idempotency, post-create readback, needs_review preservation, source-not-auto-saved and forged-evidence rejection; package release-check passed; Launcher tests 14 PASS.
- Final package: tools/worldbook-studio/artifacts/WorldbookStudio-win-x64.zip; SHA-256 1bc24d8666b8831cf065ab9b012db8616b7a5f136007ed8b3d660c472888f1e5; manifest files 612; self-contained win-x64.
- Evidence: docs/evidence/WORLDBOOK-STUDIO-DRAFT-20260824-smoke-r6.json; launcher/package evidence under tools/worldbook-studio/artifacts/WorldbookStudio-smoke.
- User guide updated: tools/worldbook-studio/新手指引_世界书内容编辑者.md now explains the complete reference-to-draft workflow.
- Boundaries: no Bannerlord startup, no game-directory sync, no real cloud Provider verification, no real Worker deployment verification, no runtime reader/save compatibility claim, and no frozen AWAKE candidate change. Evidence level remains E2.
- Next action: hand the ZIP and guide to the worldbook author for offline authoring trial; collect usability feedback before adding further workflow features.


## Worldbook Studio Batch Authoring — 2026-08-25 Revision 13 implementation repair

- Status: implementing; Revision 13 contract and the bounded implementation repair plan both returned exact `VERDICT: APPROVED` from the current-primary independent read-only review.
- Plan: docs/PLAN-WORLDBOOK-STUDIO-BATCH-IMPLEMENTATION-REPAIR-20260825.md.
- Design: docs/superpowers/specs/2026-08-24-worldbook-studio-batch-authoring-design.md.
- Contract: docs/superpowers/specs/2026-08-24-worldbook-studio-batch-authoring-contracts.v2.json (Revision 13, SHA-256 `217E7538DC7C356D092ABC029A79E7504EE36BC1000B0B3FD3F7FF261FDCB4A9`).
- Checkpoint: docs/checkpoints/WORLDBOOK-STUDIO-BATCH-AUTHORING-20260824-checkpoint.md.
- Review log: docs/PLAN-WORLDBOOK-STUDIO-BATCH-AUTHORING-20260824-REVIEW-LOG.md.
- Current offline evidence: Worldbook Studio harness `101/101`, BatchTests `13/13`, Draft tests `9/9`, Draft HTTP Smoke passed, Batch HTTP Smoke passed, and Release build `0 warnings / 0 errors`.
- The approved repair covers cancellation state, lease reclaim, fixed report projection, accepted-fact hash rechecks, metadata retry/public projections, item-detail shape and route registry validation. The Revision 13 contract itself is frozen.
- `release-check.ps1` now executes the complete `test.ps1` chain, runs `batch-contract-check.ps1`, checks the current authoring schema hash, and rejects Revision 13 `test_entrypoints.wired_now=false` with `WB-RELEASE-033`.
- Internal candidate package: `tools/worldbook-studio/artifacts/WorldbookStudio-win-x64-internal-candidate.zip`, manifest/SHA256SUMS `613/613`, ZIP SHA-256 `eaaad0f02d0828e0068de70cb4e39850c322d60b19a1d36e2ce1b9e9ea905074`. It is not a formal release package because the frozen contract intentionally keeps `wired_now=false`.
- The batch UI is a separate follow-up slice and is not included in this repair lease.
- Boundaries: no automatic canon publication, expressions generation, person/family/identity binding, Bannerlord startup, game-directory synchronization or frozen AWAKE candidate change.
- Post-approval plan: `docs/PLAN-WORLDBOOK-STUDIO-BATCH-POST-APPROVAL-WIRING-20260825.md` is drafted with status `needs_review`; it preserves R13 and proposes a separately reviewed R14 current contract.
- Next action: obtain the plan's independent read-only verdict and user signoff before changing any contract/loader/package code; do not edit Revision 13 in place or relabel this candidate as a release.

## Worldbook Studio Batch Authoring — 2026-08-25 Revision 14 release wiring

- Status: `release_ready`; the R14 post-approval wiring plan and design addendum received exact `VERDICT: APPROVED` from an independent read-only review.
- R13 remains immutable historical authority: `217E7538DC7C356D092ABC029A79E7504EE36BC1000B0B3FD3F7FF261FDCB4A9`.
- R14 current contract: `docs/superpowers/specs/2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json`; SHA-256 `7D07B08DA4CE5AB27DFE70F2F81BB098597A04FD170160612E09C32DA4540808`; `wired_now=true`.
- Verification: contract checker `1132/1132`; Studio harness `101/101`; BatchTests `13/13`; Draft tests `9/9`; Draft/Batch HTTP Smoke PASS; Launcher tests `14 PASS`; Release build `0 warnings / 0 errors`; standalone `release-check` PASS.
- Current package: `tools/worldbook-studio/artifacts/WorldbookStudio-win-x64.zip`; ZIP SHA-256 `a2b775e76e63298999b44e73fa1cb56552f99584fb8dd25db83b6caefd9e2ee9`; manifest/SHA256SUMS `613/613`.
- Boundaries: generated documents remain `needs_review`; no expressions, identity/person/family bindings, automatic canon publication, Bannerlord startup, game-directory synchronization, cloud semantic validation or Worker deployment claim.
- Next action: hand the R14 package and author guide to the worldbook author for offline authoring trial; future metadata-cache/unknown-result hardening remains separate.

## Customer Delivery Closure — 2026-09-06 continuation

- Status: `package_verified_local_worker_verified_authority_export_verified_pending_customer_and_game_validation`.
- Latest customer package: `artifacts/customer-delivery/awake-customer-20260906-091802884-181cac391605-3cfe6696`; ZIP SHA-256 `c5cef782a17aa643779545b9cb3d84528a2a44b92b24793d4d162af55e80711d`.
- WBS current package was rebuilt through `tools/worldbook-studio/scripts/package.ps1`; Release Check passed with `0 warnings / 0 errors`; current package validation passed with `657` files.
- Local Worker acceptance passed using loopback Ollama `qwen2.5:latest`: generated candidates, exact evidence, accepted camelCase request mapping, needs-review document creation/readback, no canon write, and process/temp cleanup all passed. Evidence: `docs/evidence/AWAKE-CUSTOMER-LOCAL-WORKER-20260906-final-r7.json`.
- Root cause repaired in `tools/customer-delivery/local-worker-package-acceptance.ps1`: generated response wire fields are snake_case while WBS request DTOs require camelCase; optional evidence fields are now handled without strict-mode failures; readback asserts the actual `status=needs_review` plus `author_created.review_status=draft` contract.
- Workstation handoff smoke was repaired for current time and strict RFC 3339 UTC `Z` timestamps; current loopback handoff evidence passed.
- Authority Gate independent suite passed `5/5`, including compile proof, staging export, idempotent replay, pointer CAS, and publish boundaries. This is separate authority evidence; the customer offline smoke intentionally still reports `export=not_run`.
- Dedicated customer-package authority export smoke now passes registration → selection → approval → CompileProof → staging export, while preserving the current pointer. Evidence: `docs/evidence/AWAKE-CUSTOMER-AUTHORITY-EXPORT-20260906-final-r5.json`.
- Root cause repaired in `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/Program.cs`: the authority export response reused a parented `JsonNode`, causing a real `500 unknown`; the route now projects the staging path as a scalar value.
- Customer delivery documentation now states the local Worker loopback boundary, the exact package validation commands, the authority staging distinction, and the required manual review before canon publication.
- After freeing stale temporary package extracts, the latest customer package was rebuilt and passed `validate-customer-delivery.ps1` with `657` files; authority export smoke passed again. Evidence: `docs/evidence/AWAKE-CUSTOMER-AUTHORITY-EXPORT-20260906-final-r6.json`.
- A later `package-only-acceptance.ps1` attempt passed validation for the existing package but was unable to create its second temporary ZIP because the system temporary drive ran out of space; the named temporary directory was removed. This is an environment-capacity limitation, not a product assertion failure, and the retained latest package still passes `validate-customer-delivery.ps1`.
- Boundaries: no Bannerlord launch, no game-directory synchronization, no real cloud Provider, no customer clean-machine verification, and no E4/E5 claim. Keep the goal active; next action is hand the latest package to the user for clean Windows validation and archive final evidence.

## Worldbook Studio Launcher Occupancy Repair — 2026-09-06

- Root cause: the Launcher used one global mutex, `Local\AWAKE.WorldbookStudio.Launcher`, so a test package and a customer package could incorrectly block each other even when they used different package roots.
- Repair: the default mutex is now derived from the normalized package-root hash; the explicit `AWAKE_WB_MUTEX_NAME` override remains available, and the same package still allows only one Launcher instance.
- Current WBS and customer package were rebuilt after the repair. Latest customer BuildId: `awake-customer-20260906-130127495-d1efabf7a27c-25c04fad`; ZIP SHA-256 `1e8dd8dfff4863c19d4d206d1fc9c7bd04bb5b4e5bf8bd1c591919a6da9fd881`.
- Package validation passed with `657` files and no active WBS process remains. Launcher focused tests compiled successfully; two host lifecycle tests require their existing `AWAKE_WB_TEST_PACKAGE` fixture and were not treated as product failures.

## 实机记录 — 2026-09-10 AWAKE `awake-20260910-persona-anchor-005`（用户实机运行，Codex 未启动游戏）

### 运行环境

- 启动命令行：`/singleplayer _MODULES_*Bannerlord.Harmony*BetterExceptionWindow*Bannerlord.ButterLib*Bannerlord.UIExtenderEx*Bannerlord.MBOptionScreen*Native*SandBoxCore*Sandbox*AWAKE*_MODULES_ no_watchdog`。
- 证据位置：`Modules\AWAKE\Logs\Awake.log`、`Modules\AWAKE\Logs\AwakeProbe.log`、`C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_40556.txt`（失败两次为 `rgl_log_35480.txt`、`rgl_log_5000.txt`）。

### E4（persona 锚点）—— 通过（写入侧 + 读取侧）

- 加载侧：`14:10:37Z module_load id=Awake version=0.2.0 build_id=awake-20260910-persona-anchor-005 dll_sha256=C2130FC0…`、`register_ok`、`host_campaign_session_ready session=session-6c94c53515e04d4889a7228d46428c33`、`game_start version=0.2.0`；`14:23:08Z` 二次启动同样命中 `build_id=awake-20260910-persona-anchor-005`。
- 写入侧：`14:11:35Z persona_continuity_new_campaign campaign=F8yU7kuRWVFL`；`14:15:21Z`、`14:15:24Z`、`14:24:58Z` 各一条 `persona_continuity_saved key=awake_persona_continuity_v1 bytes=375 campaign=F8yU7kuRWVFL`；退出时 `host_campaign_session_drained success=True`、`pending_writes=0 dropped=0`。
- 读取侧：`14:23:31Z persona_continuity_loaded campaign=F8yU7kuRWVFL character=main_hero schema=awake.persona.continuity.v1 sequence=0`，`14:25:11Z` 再次读到同一条。
- 结论：**saved / loaded 成对出现，同一档二次读档结果一致（campaign / character / schema / sequence 全部相同）→ E4 通过**。
- E5 仍为 `blocked_not_wired`：锚点只恢复身份，不恢复 persona 内容（`sequence` / `watermarks` 恒 0，尚无内容生产者）。

### 实机观察到的既有问题（非本批次引入，均未修复，待排优先级）

1. **AI 配置失败（用户报告"runtime 有点问题"）**：`%LOCALAPPDATA%\AWAKE\RuntimeData\awake-runtime.provider-ledger.json` 显示 5 条 route 的 `provider-profile-upsert` 均为 `Status=1 (Applied)`，但 `provider-models` 三次均为 `Status=4 (Failed)`（`14:13:20.765Z` / `14:13:33.607Z` / `14:14:03.908Z`，三次 payload hash 相同）。随后 `14:14:35Z awake_host_resolution status=runtime_not_ready runtime=Stopped`，`14:14:49Z map_shout_open_failed reason=no_host`；MCM 的 AI 自检在 runtime 非 `Ready` 时返回"AWAKE Runtime Service 尚未就绪，请稍候再试。"。`RuntimeServiceClient` 仅在 `Dispose()` 中把状态置为 `Stopped`，且 Windows 应用程序日志无 `MarcusAwakeRuntimeService` 崩溃事件，故属于受控停止而非崩溃。**缺口**：Runtime 侧 Provider 失败原因（HTTP 状态码/错误码/消息）当前不落盘，AWAKE 只记录 `Status=Failed`，离线无法归因到 BaseUrl / 模型名 / API Key / 网络中的哪一环。**该诊断缺口已在 `awake-20260910-provider-diagnostics-006` 关闭**（现随 007 一起同步进游戏目录）：下次 Provider 失败会在 `Awake.log` 落结构化行，Runtime 自身 stderr 另有文件镜像。**地址形状坑已由 `awake-20260910-baseurl-tolerance-007` 处理**：MCM 服务地址现在接受完整接口地址（剥离末尾 /chat/completions / /completions / /models 并记录 `provider_base_url_normalized`）。失败**根因本身仍需下次实机按新地址复现确认**。
2. `native_readiness_probe_error`（首启一次性，已自愈）：`14:11:24Z native_readiness_probe_error error=Object reference not set to an instance of an object.` → `native_readiness status=Failed generation=1 code=native_probe_exception`；但当日两次重读档均为 `14:23:25Z` / `14:25:06Z native_readiness status=Ready`，故目前按"首启一次性失败、之后自愈"记录，是否仍需修复待定。
3. `worldbook_runtime_init_error error=WB2-SCHEMA-UNSUPPORTED:entry`（两次读档均复现：`14:23:25Z`、`14:25:06Z`）：与"仓库内世界书为后备版、`awake.worldbook.v1` 格式已淘汰"的既有结论一致，正式版世界书仍在 `worldbook-studio` 本地编辑器中制作，尚未大规模产出。
4. **world-state 存储权限被拒**（两次读档均复现）：`14:23:27Z` / `14:25:08Z` `permission_gate_evaluate permission=storage.namespace.write decision=Denied` → `permission_gate_request permission=storage.namespace.write granted=false code=host.permissions.unavailable` → `world_state_storage_permission_denied code=host.permissions.unavailable` → `awake_campaign_state_restore_skipped reason=storage_not_ready`。persona 锚点走 `SyncData`，不经过该存储命名空间，故不受影响，E4 结论成立。

### 下一步

- E4 已闭合（写入侧 + 读取侧），本批 persona 锚点切片可结；`BUILD_VERIFICATION.txt` 追加 `awake-20260910-persona-anchor-005` 记录，不改版本号（仍 `v0.2.0`）。
- 上述第 1-4 条（AI 配置失败 / native 首启探针 / worldbook schema / world-state 存储权限）归入独立修复批次，与本批次 persona 锚点交付解耦。
