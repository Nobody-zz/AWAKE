# PersonaWorkbench × AWAKE Joint G3-S0 Storage Readiness Checkpoint

- `task_id`: `PERSONA-AWAKE-JOINT-G3-S0-20260824`
- `batch_id`: `persona-awake-joint-g3-s0-storage-readiness-20260824`
- `status`: `offline_verified_g3_s0`
- `execution_lease`: `none`（已释放 `g3-s0-20260824-114116`）
- `scope`: 仅建立 `awake.persona.state` typed Storage readiness 边界；不含 Persona runtime caller、完整 save/load、分支/重启恢复、游戏同步、PlayerExports、发布或 G3-A/G3-B。

## files_changed

- `_houkai_merge/AWAKE/src/AwakeStorageContract.cs`
- `_houkai_merge/AWAKE/src/AiTaskConstants.cs`
- `_houkai_merge/AWAKE/src/WorldStateStore.cs`
- `_houkai_merge/AWAKE/src/AwakeRuntime.cs`
- `_houkai_merge/AWAKE.Tests/Program.cs`
- `_houkai_merge/AWAKE.Tests/AwakeTestFakes.cs`
- `_houkai_merge/AWAKE/docs/persona-awake-joint-g3-s0-lease.v1.json`（关闭 lease）
- `_houkai_merge/AWAKE/docs/checkpoints/PERSONA-AWAKE-JOINT-G3-S0-20260824-checkpoint.md`
- focused evidence：`tools/persona-awake-joint/artifacts/g3-s0-readiness-focused.json`、`g3-s0-readiness-trace.json`

## implementation

- 注册 `awake.persona.continuity.v1`、`awake.persona.override.v1`、`awake.persona.recovery.v1`，并映射到三个 Persona `WorldStateKind`。
- 新增 `awake.persona.state` 到 readiness namespace 集合；保留 Worldbook SyncData/import/export 原路径和 schema 边界。
- 将 namespace 打开改为本地 staging：所有 required namespace 成功后才提交到 store；partial-open、异常、取消或缺失 required namespace 不发布半就绪 owner。
- readiness 不再复用缺少 required namespace 的旧 owner；失败后可重新创建 candidate 并成功 ready。
- 新增 G3-S0 专用 Fake Host/Storage/Permission 与六项 focused smoke，生成 scope-bound raw trace/report。

## verification

- `dotnet build _houkai_merge/AWAKE.Tests/AWAKE.Tests.csproj -c Debug`：0 warnings / 0 errors。
- `dotnet build _houkai_merge/AWAKE/AWAKE.csproj -c Debug -p:BannerlordApi=1.3.15`：0 warnings / 0 errors。
- G3-S0 focused smoke：`PASS`；覆盖 namespace owner、typed registry、partial-open no-owner、stale-owner no-use、failure retry、Worldbook compatibility。
- `verify-g3-s0-focused-evidence.ps1`：`pass/0`，24 项 focused 断言通过。
- `verify-native-prerequisite.ps1`：`pass/0`，`storageReady=true`；在 lease 释放前完成 scope-bound 验证。
- `verify-contract.ps1`：`pass/0`。
- `verify-g3-plan.ps1`：`pass/0`。
- `verify-e2-matrix.ps1`：`pass/0`，20/20 fixture outcome 匹配；`PWB-AWAKE-013` 保留既有 frozen-baseline drift warning，未改 frozen fixture。
- runtime bridge static：`reject/10`，为 G3-A runtime projection 尚未批准/实现的预期阻断，不计为 G3-S0 失败。

## full_smoke_note

全量 `Awake.SdkSmoke` 已执行到 G3-S0 focused smoke 并通过，随后在既有 `RunSharedPersonaGoldenFixtureSmoke` canonical fixture 断言处失败（`Program.cs:1153`）。该失败不在 G3-S0 写集或 focused readiness 链路内，本批未修改 Persona canonical renderer/fixture，也未将其归因给本批次。

## known_limitations

- 未实现 Persona runtime projection、`ContextSnapshot`/`RuntimeBundle`、实时更新、完整 persistence、save/load、branch/restart recovery 或游戏 prompt 消费。
- 未启动 Bannerlord、未同步游戏目录、未修改 `ModuleData`、`dist`、PlayerExports、冻结 candidate 或发布包。
- scope manifest 的原始 revision/hash 仍由 detached approval/lease 约束；lease 已释放，后续新批次必须重新取得对应批准和 lease。
- full Smoke 的 shared canonical fixture mismatch 仍需单独定位；不应把当前 G3-S0 focused PASS 解释为全量 Persona 回归通过。

## protected_boundary

本批未修改：

- `_houkai_merge/AWAKE/src/PersonaPersistenceModels.cs`
- `_houkai_merge/AWAKE/src/WorldbookRuntime.cs`
- `_houkai_merge/AWAKE/src/ProbeExtension.cs`
- `_houkai_merge/AWAKE/ModuleData`
- `_houkai_merge/AWAKE/dist`
- 游戏 `Modules\\AWAKE`
- PlayerExports、candidate ledger、frozen candidate roots

## next_action

下一批需另行批准后，在 G3-A 范围接通 Persona runtime projection/ContextSnapshot/RuntimeBundle；同时可单独安排 shared canonical fixture mismatch 的根因定位。不得自动把本 checkpoint 视为 G3-A/G3-B 授权。

## evidence

- `_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/g3-s0-readiness-focused.json`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/g3-s0-readiness-trace.json`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/.verify-g3-s0-focused-evidence.json`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/rev3-native-prerequisite.json`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/rev3-contract-g3-s0.json`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/rev3-g3-plan-g3-s0.json`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/rev3-e2-matrix-g3-s0.json`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/artifacts/rev3-runtime-bridge-g3-s0.json`

## last_error

`full_smoke_existing_shared_persona_canonical_fixture_mismatch; runtime_bridge_static_expected_reject; no_game_or_release_validation`
