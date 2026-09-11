# AWAKE Persona Fixture Baseline — 2026-09-03

## Purpose

建立当前 Persona Workbench × AWAKE 既有 fixture 的可复现离线基线，不声称 Persona Runtime Projection、ContextSnapshot、RuntimeBundle Prompt 投影、Persona persistence 或游戏内角色闭环已经完成。

## Scope

- 使用现有 Persona schema、crosswalk 和 fixture runner。
- 固定并运行五个既有 fixture：
  - `PWB-AWAKE-001-valid-approved`：预期 `pass/0`
  - `PWB-AWAKE-008-last-known-good`：预期 `pass/0`
  - `PWB-AWAKE-003-missing-selection-rejected`：预期 reject
  - `PWB-AWAKE-004-unknown-tag-rejected`：预期 reject
  - `PWB-AWAKE-015-stale-selection-rejected`：预期 reject
- 记录每个 fixture 的输入 hash、预期 status/error、实际输出、报告 hash 和执行时间。
- 仅报告现有 runner 支持的行为。

## Explicit non-goals

- 不实现新的 Persona runtime projection。
- 不定义新的 `ContextSnapshot` 或扩展现有 `RuntimeBundle`。
- 不实现 Persona persistence/recovery handler。
- 不修改 Persona Workbench、Worldbook Studio、UI Workstation 或 AWAKE Runtime。
- 不把 `persistence`, `native-runtime-caller`, `dynamic-invalidation` 的 `not_attempted` 当作通过。
- 不启动 Bannerlord、不访问真实 Provider、不同步游戏目录。

## Commands

```powershell
dotnet build "..\AWAKE.Tests\AWAKE.Tests.csproj" -c Release --nologo -v:minimal

pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" `
  -FixtureId "PWB-AWAKE-001-valid-approved" `
  -ReportPath "tools\persona-awake-joint\artifacts\persona-baseline-001.json"

pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" `
  -FixtureId "PWB-AWAKE-008-last-known-good" `
  -ReportPath "tools\persona-awake-joint\artifacts\persona-baseline-008.json"

pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" `
  -FixtureId "PWB-AWAKE-003-missing-selection-rejected" `
  -ReportPath "tools\persona-awake-joint\artifacts\persona-baseline-003.json"

pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" `
  -FixtureId "PWB-AWAKE-004-unknown-tag-rejected" `
  -ReportPath "tools\persona-awake-joint\artifacts\persona-baseline-004.json"

pwsh -NoProfile -File "tools\persona-awake-joint\run-fixtures.ps1" `
  -FixtureId "PWB-AWAKE-015-stale-selection-rejected" `
  -ReportPath "tools\persona-awake-joint\artifacts\persona-baseline-015.json"
```

## Acceptance

| Case | Expected |
|---|---|
| Valid approved | `pass/0` |
| Last-known-good fallback | `pass/0` |
| Missing selection | expected reject tuple |
| Unknown tag | expected reject tuple |
| Stale selection | expected reject tuple |
| Unsupported persistence | `not_attempted`, never pass |

## Deliverables

- `docs/persona-baseline/PERSONA-FIXTURE-BASELINE-20260903.json`
- `docs/persona-baseline/PERSONA-FIXTURE-BASELINE-20260903.md`
- runner reports under `tools/persona-awake-joint/artifacts/`
- no authoritative registry, export, selection, or published pointer changes

