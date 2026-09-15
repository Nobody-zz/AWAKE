# AWAKE Validation Matrix

## 迁移后当前重基线（2026-09-11）

- 权威工作区：`D:\AWAKE-Dev`。
- 当前源码 BuildId：`awake-20260911-dialogue-chain-010`。
- E1：AWAKE 构建通过；测试工程构建通过，0 warning / 0 error。
- E2：`Awake.SdkSmoke.exe` 为 `PASS ALL`；Persona Workbench Core Tests 通过。
- G3-S0：当前 Storage readiness focused evidence 与 scope 校验均为 `pass/0`；实现未超出批准六文件写集，lease 已释放。
- E3：`unverified`。当前工作区没有可重新核验的 `docs/sync-reports`，不能把历史同步报告当作 010 的同步证据。
- E4/E5：`not attempted` / `unverified`。没有当前 010 的用户游戏日志或存档回归证据。
- 当前候选状态：`source_only_pending_rebaseline`；未授权游戏同步。
- 详细迁移工作：[PLAN-WORKSPACE-MIGRATION-REPAIR-20260911.md](PLAN-WORKSPACE-MIGRATION-REPAIR-20260911.md)。

以下旧矩阵与候选哈希记录保留用于历史追溯；若与本节冲突，以本节为当前状态。

> Evidence is cumulative but not interchangeable. Updated: 2026-08-30 00:46 (Asia/Shanghai).
> BuildId `awake-20260903-awake-runtime-repair-004` is the current unsynchronized source candidate. It has offline E1/E2 evidence only; historical `002`/`003` evidence is not attributable to `004`.
> The 2026-08-20 candidate hashes and sync status are historical and are not current evidence.

## Evidence Levels

| Level | Meaning | Does not prove |
|---|---|---|
| `E0` | Plan, static contract, approved scope | Runtime behavior |
| `E1` | Compile, JSON/XML parse, static structural checks | Call-chain execution, external service behavior, or gameplay |
| `E2` | Offline tests, harnesses, SDK smoke, deterministic audits | Installed-game behavior, real Provider access, or save/load correctness |
| `E3` | Current candidate files synchronized across required targets with matching hashes/manifests | Player-visible behavior |
| `E4` | A matching BuildId in a user-run Bannerlord log proves entry → call → settlement/persistence → observable result | Long-term save/load correctness |
| `E5` | A matching BuildId proves save/load, restart, duplicate-submit, and relevant long-run regression behavior | Unreviewed future changes |

`blocked` means the gate is known to be unmet. `not verified` means supporting evidence exists but does not prove the row's claim. `not attempted` means no matching run or log exists.

## Current Matrix

| Area | E0 | E1 | E2 | E3 | E4 | E5 | Current state |
|---|---:|---:|---:|---:|---:|---:|---|
| Governance skills and routing | pass | pass | pass | n/a | n/a | n/a | offline_verified |
| Marcus embedded Framework/API layers | pass | pass | pass | pass | not attempted | not attempted | synced_pending_game |
| BuildId, runtime package, and synchronization | pass | pass | pass | pass | not attempted | not attempted | pending_game |
| Runtime Service and Provider fallback | pass | pass | pass | pass | not attempted | not attempted | synced_pending_game |
| Worldbook runtime/Studio consumer path | pass | pass | pass | pass | not attempted | not attempted | synced_pending_game |
| AWAKE caller / dialogue-history-memory loop | pass | pass | pass | pass | not attempted | not attempted | synced_pending_game |
| Persona Workbench | pass | pass | pass | n/a | not attempted | not attempted | offline_verified |

## Current Evidence Anchors

- `docs/evidence/MARCUS-AWAKE-API-LAYERS-E1-20260829-002.json`: `E1`, `pass=true`; legacy API `109/109` preserved; current public API `199`; `90` additions classified; `0` unclassified; unexpected source, assembly, and public-API findings `0`.
- `docs/evidence/MARCUS-AWAKE-P3A-E1-20260829-002.json`: current phase-scoped P3A-E1 `pass=true`; build exit `0`, warnings/errors `0`, API baseline matched, forbidden scan passed, source/assembly findings `0` (`22` files scanned, `53` deferred).
- 当前 `002` 的 source-only 门禁通过 MCM 静态契约 `47/47` 与 AWAKE caller 静态契约 `7/7`；这些证明源码接线，不替代 Bannerlord 内的真实调用闭环。
- 当前 `002` 的离线回归通过：Framework `20/20` + `6/6`、Transport `7/7` + `6/6` + `1/1`、Provider `PASS ALL`、Storage `PASS ALL`、Runtime Service `19/19` + `7/7` + `8/8` + `23/23` + `21/21`、生产 smoke `17/17`、同步脚本 `14/14`。
- `docs/evidence/MARCUS-AWAKE-P3D-A2-20260828.json` remains a historical A2 record; the current `002` A2 rerun passed `21/21` with fake HTTP only, no external network, real cloud Provider, Bannerlord startup, or game-directory sync.
- `tools/release_check.ps1` returned `RELEASE_CHECK_OK` / `RELEASE_STATUS=BLOCKED_SYNC` against current `002`; the blocked portion is limited to the intentionally unsynchronized game copy.
- `docs/sync-reports/sync-20260829-marcus-embedded-002-game-runtime-fix.json` records `state=verified`, `process_before=[]`, `197` Runtime files copied, and `0` files removed.
- Durable pre-sync backup: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE-backups\game-AWAKE-before-awake-20260829-marcus-embedded-002-20260830-014707`; backup manifest SHA-256 `9312E25653A138CC55C21B5AB0506688939051E478A94BF1C2DDAFC5B2E7ED46`.
- `tools/release_check.ps1` returned `RELEASE_CHECK_OK` / `RELEASE_STATUS=CANDIDATE_SYNC_OK` after the current `002` was synchronized; this is file/package evidence only.

## Current Attribution and Sync

- Current BuildId: `awake-20260903-awake-runtime-repair-004`.
- Candidate state: `source_only_pending_sync`; current source changes are included and revalidated, but no dist/game synchronization was performed.
- Engineering version remains `v0.2.0`; `0.2.1` remains a development target, not an E4/E5-verified release claim.

| Artifact | Recorded artifact evidence |
|---|---|
| `Awake.dll` | Current source build SHA-256 `B25A5A4F1F7E95D7182BBD48FDC41A894E8366366440ECA4D27F2582AF74C17E`; dist remains historical |
| `MarcusAwakeFramework.dll` | Source/dist SHA-256 `00B34BF79DEFADBE4A946612244EA8631CE777495C2B99ADD11F2B10CE8C7F55` |
| `MarcusAwakeTransport.dll` | Source/dist SHA-256 `DE81809AC08EA86BE90FD6F0AB77FA348B24B744588B0AF970DFFF2080E6DF48` |
| Embedded Runtime package | `RUNTIME_PACKAGE_OK`; self-contained win-x64, `195` payload + `2` metadata files (`197` total); dist manifest SHA-256 `58A601B48B32F03B6650AEA4B8694A22F262465ABB94A9D688B8DE11886122AC`; dist `SHA256SUMS` SHA-256 `765160E3F2424D3B6E5582AFE422B8C105770701A6B52A71F4C24719A25AF422` |

- `tools/sync_module.ps1 -SkipGame` remains the only synchronization mode used for the game-facing tree; current `004` has not been copied to dist or the game directory.
- Strict release staging report: `docs/sync-reports/release-staging-awake-20260829-marcus-embedded-002.json`, state `verified`, allowlist `972` files; Runtime subset `197` files.
- The game directory remains the prior candidate: game `Awake.dll` SHA-256 `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`; game `SubModule.xml` SHA-256 `179989FB3B054A4D67AAA4D035C1C059043747F84469B36E5B7886ABCB466F32` still contains the legacy `MarcusAIFramework` dependency and differs from source/dist; the new embedded Framework/Transport DLLs and Runtime package are absent. No current BuildId game hash or game-target manifest match is claimed.

## Unverified Boundaries

- Source-to-artifact and E3 synchronization attribution: current `002` hashes, package manifests, and game target files are internally consistent; they still do not prove gameplay.
- Real Provider and real API key: not used or verified. The P3D-A2 evidence is offline fake-HTTP coverage, not a real cloud or local Provider run.
- Bannerlord gameplay: not started; there is no matching current-BuildId game log, so no `E4` entry → call → settlement → observable-result claim exists.
- AWAKE MCM and caller static contracts pass (`47/47` and `7/7`), and the offline production smoke covers structured dialogue/memory consumers; no matching BuildId Bannerlord runtime evidence exists.
- Save/load, restart, duplicate submission, timeline isolation, and long-run regression: no matching evidence exists, so `E5` remains unverified.

## Required Gates Before Claims

- `E1`: keep compile, parse, and static API evidence separate from call-chain claims; the current API-layer evidence is `E1 PASS`.
- `E2`: report only offline harness/smoke results; the current P3D-A2 result is `21/21 PASS` with no external network or real Provider.
- `E3`: current `004` requires an authorized source/dist/game synchronization with Bannerlord closed and all target hashes/manifests recorded in a new sync report.
- `E4`: require a user-run Bannerlord log containing `awake-20260903-awake-runtime-repair-004` and the complete observable path.
- `E5`: require a matching BuildId save anchor/timeline plus restart/read-load, duplicate-submit, and relevant long-run checks.
