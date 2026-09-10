# Persona Workbench Prompt Usability Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the real user experience of Persona Workbench's AI drafting path while preserving every existing Provider, Persona, DSL, batch, and authoring-handoff contract.

**Architecture:** Keep the existing two-stage user flow and existing request/response formats. Make a surgical change to the expansion system prompt so sparse character ideas become usable behavioral prose while unsupported biographical facts remain forbidden. Make only a clarifying change to the DSL conversion prompt so confirmed text retains contradictions and evidence grounding; do not add fields, stages, schemas, or endpoints.

**Tech Stack:** C#/.NET 10, ASP.NET Core, existing console-style Web tests, PowerShell verification scripts.

**Spec:** User request in conversation; repository baseline in `...\AWAKE\tools\persona-workbench\README-FreePreview.md`, `PersonaWorkbench-使用说明.md`, and existing AI closure plans.

## Global Constraints

- Do not change any HTTP route, request type, response type, JSON property, schema, parser, batch protocol, or authoring-handoff contract.
- Do not add a new AI stage, inference confidence field, worldbook profile, or persistence format.
- Expansion may infer observable behavior from supplied traits, but may not invent biography, people, factions, locations, events, titles, occupations, relationships, goals, or moral/political beliefs.
- DSL conversion remains a sparse evidence candidate and keeps exact source evidence, existing axes, existing flags, and existing output format.
- Batch generation continues to call the existing DSL conversion path serially.
- Do not start Bannerlord or leave Persona Workbench running after verification.
- This workspace is not a Git repository; use the recorded precise-file backup and review-state file instead of Git rollback.

---

## Behavior Contract

| Case | Required observable result | Evidence |
|---|---|---|
| Sparse character concept is expanded | Prompt asks for concrete public/private/stress behavior and preserves contradictions without inventing biography | Focused Web test inspects captured expansion request |
| Dark or unpleasant motivation is supplied | Prompt explicitly preserves it instead of sanitizing it into kindness/heroism | Focused Web test inspects captured expansion request |
| Expansion output contract | Response remains plain same-language prose; no JSON, headings, DSL, tags, or metadata instruction is removed | Existing expansion tests plus focused assertions |
| Confirmed text is converted | Prompt still requests one JSON object with the existing root fields and exact source evidence | Focused Web test inspects captured conversion request |
| Batch conversion | Existing batch route and item/result fields are unchanged | Existing batch tests and source diff review |
| Invalid/malformed provider output | Existing parser and error behavior are unchanged | Existing Web test suite |

## Non-goals

- No change to `PersonaWorkbench.Core` models or serializers.
- No change to `contracts\*.schema.json`.
- No change to `BatchGenerationService.cs`.
- No new prompt mode exposed in the UI.
- No new model, provider, retry, or fallback behavior.
- No claim of improved model output until the local tests and bounded evidence are run.

## File Map

- Modify: `...\AWAKE\tools\persona-workbench\src\PersonaWorkbench.Web\ProviderTextExpansionContract.cs` — expansion system prompt only.
- Modify: `...\AWAKE\tools\persona-workbench\src\PersonaWorkbench.Web\ProviderDslConversionContract.cs` — minimal wording clarification only.
- Modify: `...\AWAKE\tools\persona-workbench\tests\PersonaWorkbench.Web.Tests\Program.cs` — request-capture assertions for prompt intent and contract invariants.
- Create: `...\AWAKE\tools\persona-workbench\tools\_src_backup_PWB-PromptUsability-20260908\ProviderTextExpansionContract.cs` — exact pre-change backup.
- Create: `...\AWAKE\tools\persona-workbench\tools\_src_backup_PWB-PromptUsability-20260908\ProviderDslConversionContract.cs` — exact pre-change backup.
- Create: `...\AWAKE\tools\persona-workbench\docs\review-state\PWB-PROMPT-USAGE-20260908.json` — bounded standard review state.

## Tasks

### Task 1: Freeze baseline and write focused failing checks

**Files:**
- Test: `...\tests\PersonaWorkbench.Web.Tests\Program.cs`

- [x] Add one test that captures an expansion request and asserts the system message contains behavioral drafting, contradiction preservation, and anti-biography-invention rules.
- [x] Add one test that captures a conversion request and asserts the system message retains sparse-candidate, exact-source-evidence, and existing-root-field instructions.
- [x] Retain and verify the existing assertions that the expansion user message is the existing JSON envelope and the conversion user message is source text; no new envelope field was added.
- [x] Run only the Web test executable and verify the new assertions fail against the current prompts.

### Task 2: Make the smallest expansion prompt change

**Files:**
- Modify: `...\src\PersonaWorkbench.Web\ProviderTextExpansionContract.cs`

- [x] Keep the first-sentence byte-for-byte rule, same-language rule, plain-text output rule, length rule, and no-invented-facts rule.
- [x] Add concise guidance to turn supplied traits into observable public behavior, private behavior, pressure reactions, relationship behavior, and commitments when directly supported.
- [x] Add concise guidance to preserve opposing traits, shameful motives, dependency, fear, resentment, bargaining, and power-seeking without moral sanitization.
- [x] Do not add a new output section format, JSON requirement, field name, or user-visible control.
- [x] Run the focused test and verify it passes.

### Task 3: Make the smallest DSL prompt clarification

**Files:**
- Modify: `...\src\PersonaWorkbench.Web\ProviderDslConversionContract.cs`

- [x] Preserve the existing one-object-only instruction, allowed root fields, exact source wording requirement, axis/flag limits, and no-metadata rule.
- [x] Clarify that supported contradictions and unpleasant motives should remain represented in the existing fields rather than being reconciled or sanitized.
- [x] Defer any summary-paraphrase change because the current exact-source contract does not permit a safe format change in this batch; no new property was added.
- [x] Run the focused test and verify it passes.

### Task 4: Full verification and bounded self-review

**Files:**
- Review: the two modified C# files and the focused test region.
- Review state: `...\docs\review-state\PWB-PROMPT-USAGE-20260908.json`

- [x] Build the Web project in Release configuration.
- [x] Run the Web and Core test executables plus the bounded package, Provider, startup, and contract checks; record the existing startup-diagnostics incompatibility separately.
- [x] Parse changed JSON and inspect the two prompt source diffs, test diff, package metadata, and source/package binding with a bounded caller/callee review.
- [x] Check that no route, public DTO, schema, batch file, or contract file changed.
- [x] Record the standard review verdict. The self-review found no contract or format change; the summary does not claim the unrelated startup-diagnostics check passed.
- [x] Report fresh evidence separately from historical AI evidence; no live Ollama/Workbench acceptance is claimed.

## Self-Review Record

- Scope remained limited to two system-prompt string literals, two request-capture tests, the plan, exact pre-change backups, and a newly generated validation package.
- No new API route, DTO property, JSON field, schema, batch behavior, parser rule, persistence format, or authoring-handoff field was introduced.
- The expansion prompt now asks for observable behavior and preserves supported unpleasant contradictions while retaining all previous anti-invention and plain-text rules.
- The DSL prompt now preserves supported contradictions and unpleasant motives within the existing sparse candidate fields; the summary format was intentionally not relaxed.
- Fresh checks passed: Debug Web red/green cycle, Release Web build, Release Core tests, Release Web tests, Windows PowerShell compatibility, self-contained package, ZIP readback, stop script, authoring handoff, crosswalk, evidence pointer, manifest closure, and source/package binding.
- Known pre-existing check issue: `tests\PersonaWorkbench.StartupDiagnostics.Tests.ps1` fails under the current startup script because its missing-executable path reaches the strict-mode catch before `$exe` is initialized; this batch did not modify that unrelated startup path.
- Historical local AI evidence was not rerun; no real Provider acceptance claim is made for this Prompt revision.
