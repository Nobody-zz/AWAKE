# AWAKE Existing Settlement Smoke — 2026-09-03

## Purpose

重跑一个已经存在的 AWAKE fail-closed 生产 Smoke，确认当前离线基线仍能拒绝失败结算；不把它扩大解释成 Persona、存档或完整 NPC 闭环。

## Selected case

- Test name: `failed-drain-fail-closed`
- Source: `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsBoundary.cs`
- Runner registration: `tools/worldbook-runtime-production-smoke/Program.cs`
- Scope proven:
  - failed event/Store drain is not reported as success
  - readiness and replacement boundaries fail closed
- Scope not proven:
  - Persona recovery
  - generic save/load
  - full session isolation
  - Persona-to-NPC Prompt projection

## Commands

```powershell
dotnet build "..\AWAKE.Tests\AWAKE.Tests.csproj" -c Release --nologo -v:minimal
dotnet build "tools\worldbook-runtime-production-smoke\WorldbookRuntimeProductionSmoke.csproj" -c Release --nologo -v:minimal
& "tools\worldbook-runtime-production-smoke\artifacts\bin\Release\Awake.WorldbookRuntimeProductionSmoke.exe"
```

The complete stdout is saved as:

`docs\offline-closure\G3C-SETTLEMENT-SMOKE-20260903.txt`

The report must include:

- current source BuildId
- executable SHA-256
- test name
- stdout SHA-256
- pass/fail result
- explicit scope and non-scope

## Acceptance

- Build succeeds.
- The named test is present and executes.
- The named test passes.
- No claim is made for unsupported persistence, recovery, Persona, or prompt behavior.
- No game, network, Provider, or sync action occurs.

