# Plan Review Log: PersonaWorkbench 代码债务修复与链路精进
Act 1 (grill) complete — recommended options accepted by the user directive. MAX_ROUNDS=5.

## Act 1 — Grill
- Scope: confirmed dead code plus bounded Provider/frontend duplication; no repository-wide cleanup.
- Architecture: narrow shared transport and response reader; business semantics remain in the three existing operations.
- Compatibility: preserve routes, JSON fields, error codes, cooldown domains, DSL output, and failure-no-overwrite behavior.
- Execution: characterization tests before each seam; optional file split only after measurable residual complexity.
- Test boundary: local Ollama Worker-low diagnostic first, then real Workbench routes; no cloud and no Bannerlord.

## Round 1 — Independent read-only reviewer
- The Codex CLI attempt was environment-blocked: `codex-cli 0.147.0` with configured `gpt-5.6-luna` started in `read-only` mode but Windows sandbox helper launch failed with `拒绝访问 (os error 5)`. No verdict was fabricated.
- A separate read-only review agent inspected the plan and callers. It returned `VERDICT: REVISE` with these material findings:
  - Bind Ollama evidence to a fresh package built from current source; historical r48 may be stale.
  - Preserve Draft 45s versus Expansion/DSL 120s timeout and `HttpClient.Timeout` contracts.
  - Define and test client-local gates, service-level cross-action gate, cross-operation 429 isolation, cancellation and timeout release.
  - Make the shared reader return raw content, envelope kind, finish/done reason, truncation and usage; keep operation-specific size/shape/error handling.
  - Add direct-DSL and expanded-DSL frontend stale-response tests covering core, expanded text, metadata, filename, content hash, DSL, usage, save and approve state.
  - Treat dead-code removal as a completed source-only preflight because the current source already lacks those symbols after Phase 1; do not delete adjacent code.
  - Define a characterization manifest with request/response/status/error/cooldown/usage/output hashes and exact validation commands.
  - Mark `/api/provider/generate-draft` as compatibility-only rather than claiming it is on the current frontend main path.

### Claude response
Accepted all material findings. The plan now freezes the timeout matrix, gate domains, envelope compatibility fields, BuildId-bound package evidence, manifest format, exact commands, frontend stale-state coverage and compatibility-only route status. Phase 1 remains a tested preflight; the next Provider seam is paused until this revision is approved.
VERDICT: REVISE

## Round 2 — Independent read-only reviewer
- The revised plan still had two material execution gaps:
  - BuildId binding did not specify the generation/storage location, source file hash scope, or an automatic mismatch rejection. Existing `package-free-preview.ps1` only writes a package-local manifest.
  - Validation items still described package/startup/Ollama checks without directly executable commands, explicit package path, BuildId/manifest inputs, exit codes and PASS conditions.

### Claude response
Accepted both recommendations. The plan now defines `tools\write-source-manifest.ps1`, `tests\verify-source-package-binding.ps1`, the exact source input set, the `PWB-20260823-<timestamp>-<hash>` BuildId format, package files, mismatch exit code, explicit package/test commands and PASS requirements. Request a final read-only review before implementing the next seam.
VERDICT: REVISE

## Round 3 — Independent read-only reviewer
- The plan still omitted build-affecting `PersonaWorkbench.Core.csproj`, `PersonaWorkbench.Web.csproj`, and the source-manifest helper from the source hash set.
- The validation commands still used undeclared `$root`/`$package` variables and did not provide a concrete runner command for the real Workbench Ollama route tests.

### Claude response
Accepted both recommendations. The source hash set now includes both project files and `tools\write-source-manifest.ps1`; all package and PowerShell commands use literal absolute paths; the Ollama section explicitly invokes the Skill first and then a bounded `tools\run-ollama-workbench-routes.ps1` runner for only the four real Workbench routes. Request final read-only approval.
VERDICT: REVISE

## Round 4 — Independent read-only reviewer
- The source manifest line still did not include `src\PersonaWorkbench.Core\PersonaWorkbench.Core.csproj` and `src\PersonaWorkbench.Web\PersonaWorkbench.Web.csproj`.
- The plan referenced `tools\run-ollama-workbench-routes.ps1` without declaring it as an implementation artifact to create before validation.

### Claude response
Accepted both recommendations. The manifest scope now explicitly includes both project files, and the plan declares the runner as a bounded test artifact with its inputs, package ownership check, four serial routes, report output and nonzero failure behavior. Request final approval.
VERDICT: REVISE

## Round 5 — Independent read-only reviewer
- No material blocker remains. The source manifest covers both project files and the helper; the Ollama runner is explicitly an implementation artifact with package/report paths, process ownership, serial four-route behavior and exit-code semantics; Worker-low remains first.
VERDICT: APPROVED
