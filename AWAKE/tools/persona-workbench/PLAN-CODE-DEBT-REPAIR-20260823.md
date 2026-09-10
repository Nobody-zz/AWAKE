# Plan: PersonaWorkbench 代码债务修复与链路精进
_Locked via grill — recommended options accepted by the user directive on 2026-08-23; revised after Round 1 read-only adversarial review._

## Review status
Round 5 returned `VERDICT: APPROVED`. Implementation may proceed in bounded seams with the recorded gates and evidence requirements.

## Goal
在不改变 PersonaWorkbench 当前 DSL、Provider API、错误码、失败不覆盖表单和本机/云端配置边界的前提下，清除已确认死代码，收敛 Provider 传输与响应解析重复，合并前端 DSL 成功回填逻辑，并以绑定到当前源码构建包的自动化测试和本机 Ollama 双层证据证明重构未改变行为。

## Current evidence
- Phase 0 baseline executed before the next seam: Core tests PASS; Web tests PASS.
- Phase 1 bounded cleanup executed and verified: `PersonaDslGenerator.GenerateLegacy`、其旧专属字段/辅助方法及两个非-Draft `IsOfficialDeepSeekEndpoint` 已不存在；`ProviderDraftContract` 中仍被调用的同名方法保留；Core/Web tests PASS。
- The next shared files are not yet part of the behavioral path; they remain provisional until the revised plan is approved and then must either be fully wired or removed before completion.

## Approach
1. Create a characterization manifest bound to the current source snapshot. For every operation record request shape, provider response fixture, HTTP status, error code, cooldown, usage, output hash/length, and failure-no-overwrite result.
2. Treat dead-code cleanup as a completed source-only preflight, not as a future deletion task. Re-run source, test, reflection, serialization and route searches before finalizing.
3. Extract a narrow Provider request seam while preserving the operation matrix: Draft timeout 45 seconds and its existing `HttpClient.Timeout`; Expansion/DSL timeout 120 seconds with linked deadline and infinite `HttpClient.Timeout`; separate client-local gates plus the service-level cross-action lease.
4. Extract a response reader that returns raw content, envelope kind (`ollama_native` or `openai_compatible`), finish/done reason, truncation state, and usage. Each business operation retains its own response-size limit, shape error code, truncation error code, normalization, candidate validation and usage-preservation behavior.
5. Add a cross-operation contract matrix: same-operation busy/cooldown, cross-operation in-flight lease, cross-operation 429 isolation, cancellation/timeout gate release, and service-level cross-action blocking.
6. Extract the frontend DSL success-application function. Test direct conversion and expansion-then-conversion separately, including document/edit/expanded epochs, provider settings, expansion controls, core text, metadata, filename, content hash, DSL, usage, save and approve invariants.
7. Generate a fresh explicit test package from the current source before real-route testing. Record a unique BuildId plus source manifest, executable hash and static asset hashes; never use the historical r48 package as evidence for current source behavior.
8. Run exact automatic commands and then the `persona-workbench-ollama-pretest` paired protocol: Worker low first, real Workbench routes second, short/long fixtures serially, no cloud and no Bannerlord.
9. Re-run the bounded post-change code-debt audit and preserve the review log.

## Key decisions & tradeoffs
- Use a narrow shared transport/lifecycle seam, not a universal ProviderService; prompts, budgets, candidate parsing, evidence validation and error-code prefixes stay operation-specific.
- Preserve timeout and HTTP-client contracts exactly: Draft 45s; Expansion/DSL 120s; do not silently standardize them.
- Keep client-local `_requestInFlight`/cooldown domains and the existing `ProviderDraftActionService` cross-action gate distinct; add tests rather than relying on names.
- The shared response reader may normalize only the common Chat Completion envelope. It must expose raw content, envelope kind, `done_reason`/`finish_reason`, truncation and usage so callers retain their current diagnostics.
- Mark `/api/provider/generate-draft` as `compatibility-only`: it is not the current frontend main path, but its route, service and tests remain supported until a separate migration plan.
- BuildId-bound package testing is mandatory for current-source claims; historical artifacts are comparison-only.
- All unresolved grill choices use the recommended option: minimum compatible change, characterization before refactor, one seam at a time, local Ollama as supplementary smoke rather than sole acceptance.

## Acceptance contract

### Baseline manifest
- A checked-in or explicitly archived manifest names the source snapshot, test package, BuildId, executable/static hashes, fixtures and expected result columns.
- A baseline is complete only when Core/Web/Browser/PowerShell/start-stop evidence is tied to that manifest.

### Provider compatibility
- Draft, Expansion and DSL preserve their current timeout, `HttpClient.Timeout`, native-Ollama routing, OpenAI-compatible routing, `think`/`thinking` behavior, response-size limits, Usage parsing and error-code mapping.
- Same-operation busy/cooldown remains local; a 429 in one client does not create a cooldown in another client; the service-level cross-action lease remains authoritative where currently used.
- Cancellation releases all relevant gates; timeout remains distinct from user cancellation.

### Frontend compatibility
- Direct conversion and expansion-then-conversion both apply successful payloads correctly.
- Any stale response leaves newer core text, expanded text, metadata, filename, content hash, DSL, usage, save state and approval state untouched.
- Provider failure never replaces the editable draft/form.

### Package/runtime evidence
- The tested package is generated from the current source and carries a unique BuildId and hash manifest.
- Worker-low and Workbench results are reported separately; `PASS`, `FAIL` and `IN_DOUBT` are not conflated.

## Exact validation commands
Run serially unless commands have independent output directories:

1. `dotnet run --project tests/PersonaWorkbench.Core.Tests/PersonaWorkbench.Core.Tests.csproj -c Release --no-restore`
2. `dotnet run --project tests/PersonaWorkbench.Web.Tests/PersonaWorkbench.Web.Tests.csproj -c Release --no-restore`
3. `dotnet build src/PersonaWorkbench.Web/PersonaWorkbench.Web.csproj -c Release --no-restore`
4. `powershell -ExecutionPolicy Bypass -File tests/browser-smoke.ps1`
5. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\package-free-preview.ps1" -Destination "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\artifacts\PersonaWorkbench-FreePreview-code-debt-20260823"`; require exit code 0 and the package executable, `PACKAGE-MANIFEST.sha256.txt`, `BUILD-ID.txt`, and `BUILD-SOURCE-MANIFEST.sha256.txt`.
6. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\tests\verify-source-package-binding.ps1" -SourceRoot "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench" -PackagePath "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\artifacts\PersonaWorkbench-FreePreview-code-debt-20260823"`; require exit code 0 and `PASS source/package BuildId and manifest match`.
7. Run each command with the literal package path and require exit code 0: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\tests\PersonaWorkbench.WindowsPowerShell.Tests.ps1" -PackagePath "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\artifacts\PersonaWorkbench-FreePreview-code-debt-20260823"`; repeat the same form for `PersonaWorkbench.SelfContained.Tests.ps1`, `PersonaWorkbench.Launcher.Tests.ps1`, `PersonaWorkbench.Launcher.ColdStart.Tests.ps1`, `PersonaWorkbench.LauncherButtonStart.Tests.ps1`, and `PersonaWorkbench.StopScript.Tests.ps1`.
8. `dotnet run --project "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\tests\PersonaWorkbench.BrowserSmoke\PersonaWorkbench.BrowserSmoke.csproj" -c Release --no-restore -- "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench"`; require the existing source browser smoke `PASS` lines plus the new direct/expanded DSL stale-state assertions.
9. First invoke the `persona-workbench-ollama-pretest` Skill with the exact package path `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\artifacts\PersonaWorkbench-FreePreview-code-debt-20260823`, then run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\tools\run-ollama-workbench-routes.ps1" -PackagePath "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\artifacts\PersonaWorkbench-FreePreview-code-debt-20260823" -ReportPath "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\artifacts\PersonaWorkbench-FreePreview-code-debt-20260823\ollama-workbench-routes.json"`; the runner covers only the four real Workbench routes and must not replace the Worker-low Skill step.

Every command must record exit code and expected `PASS` condition in the manifest; a missing environment is `IN_DOUBT`, not success.

## BuildId and source/package binding contract

The package step must be extended with one deterministic helper, `tools\write-source-manifest.ps1`, and one verifier, `tests\verify-source-package-binding.ps1`:

- The source manifest includes sorted relative paths and SHA-256 values for `src\PersonaWorkbench.Core\**\*.cs`, `src\PersonaWorkbench.Core\PersonaWorkbench.Core.csproj`, `src\PersonaWorkbench.Web\**\*.cs`, `src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj`, `src\PersonaWorkbench.Web\wwwroot\**\*`, `src\PersonaWorkbench.Launcher\Program.cs`, `src\PersonaWorkbench.Launcher\app.manifest`, `package-free-preview.ps1`, `tools\\write-source-manifest.ps1`, `start-free-preview.ps1`, `stop-free-preview.ps1`, `launch-free-preview.vbs`, `stop-free-preview.vbs`, `README-FreePreview.md`, and the selected `PersonaWorkbench-*.md` usage document; it excludes `bin`, `obj`, `artifacts`, tests, backups and generated files.
- Before package validation, implementation must create `tools\run-ollama-workbench-routes.ps1`; it is a bounded test runner, not an existing artifact. It accepts `-PackagePath` and `-ReportPath`, verifies the named package executable and port owner, starts/stops only that package, calls the four real Workbench Provider routes serially, writes the paired Workbench route report, and exits nonzero on any route failure. The Worker-low half remains the explicit `persona-workbench-ollama-pretest` Skill invocation.
- The helper writes `BUILD-SOURCE-MANIFEST.sha256.txt` into the package and computes `SourceManifestHash` as SHA-256 over the UTF-8 bytes of the sorted manifest lines.
- `BUILD-ID.txt` contains `PWB-20260823-<UTC timestamp>-<first 12 chars of SourceManifestHash>` and the full `SourceManifestHash`.
- `package-free-preview.ps1` invokes the helper after copying the release files and before writing the final package manifest. The source manifest and BuildId are excluded from the source input set so the hash is stable.
- `verify-source-package-binding.ps1` recomputes the source manifest from `-SourceRoot`, reads the package `BUILD-ID.txt`, verifies the full source hash and checks that the package executable/static files are covered by `PACKAGE-MANIFEST.sha256.txt`; mismatch exits nonzero with `SOURCE_PACKAGE_BINDING_MISMATCH`.
- The Ollama test report records the exact package path, BuildId, source hash, package manifest hash, executable hash and static `wwwroot` hash. A historical r48 package can be reported only as comparison data, never as current-source evidence.
## Risks / open questions
- The existing source tree is not a Git repository; use precise file backups and explicit hash manifests, not Git rollback assumptions.
- The current r48 artifact may not match source and is never valid evidence for a new source change.
- The shared response reader must not change native-vs-compatible envelope interpretation or operation-specific diagnostic strings.
- Frontend Browser Smoke needs direct-DSL and expanded-DSL assertions in addition to existing stale-expansion coverage.
- If new reflection/serialization/route evidence appears for a removed symbol, stop and classify it as `unknown_non_actionable`.

## Out of scope
- No change to semantic DSL priority, budget compression, deduplication rules, prompts or AI generation strategy from `PLAN-SEMANTIC-DENSE-DSL-20260821.md`.
- No Bannerlord, AWAKE runtime, worldbook, global model configuration, cloud Provider or historical package changes.
- No repository-wide cleanup, no automatic newest-package selection, and no claim that Worker output is Workbench acceptance.




