# Plan: 本周动态（R0）

> Status: `superseded_by_minimal_e2_slice`  
> Dependency: `PLAN-WORLD-FACT-REPORT-COUPLING-20260912.md` and its E2-verified WorldFactJournal/WorldFactQuery input seam. The old Window Storage plan is not an implementation dependency for this slice.  
> Scope: deterministic report construction, report contracts, lifecycle caller and menu display. No storage-substrate redesign.

> 封存提示：本文件不再是当前实现依据。当前唯一权威计划是 `PLAN-WORLD-FACT-REPORT-MINIMAL-E2-20260912.md`。本文件中的 v1/v2、生命周期和菜单内容暂不实施。

## Goal

Turn complete, verified weekly world facts supplied by the storage substrate into a stable player-facing “本周动态”; show an explicitly separate “近期动态” only when no completed report exists.

## Locked UI and content decisions

- Formal title: `本周动态`; preview title: `近期动态`.
- Nonempty display sections: `政治与外交` (`politics`), `战争与领地` (`war`), `人物近况` (`people`), `地方情况` (`local`).
- P0 report content does not invent facts. No reliable input means no item and no empty filler section.
- V1 reports remain readable. V2 formal reports use `awake:report:weekly-v2-{endDay}` and never overwrite a v1 snapshot.

## Expected behavior

1. CampaignSessionReady invokes the storage substrate's background Evaluate-only readiness/result path.
2. Once a complete window is available, `EnsureFormalReportsReadyAsync` deterministically creates or reuses its v2 report.
3. Native Knowledge is optional: report persistence completes before a later Native-gated projection attempt.
4. Terminal/menu reads the latest valid formal report; absent formal report shows a read-only preview; unavailable/corrupt storage shows a clear nontechnical unavailable state.

## Report contract

V2 schema/fixtures and C# dispatch belong to this batch. The authoritative top-level field set, item-level source closure, canonical JSON and fingerprint rules are defined only by `PLAN-WORLD-FACT-REPORT-COUPLING-20260912.md`. In particular, v2 carries `sourceFactIds` as the authoritative closure and `sourceEventIds` only as an optional legacy mapping.

`policyVersion` is `awake.weekly-report.policy.v2`; extensions contain integer `awake:windowStartDay` and `awake:windowEndDay`. Fingerprint is uppercase SHA-256 of canonical UTF-8 JSON excluding the fingerprint itself; property order, sorted set arrays, field set and pinned fixture hashes are part of this contract.

## Expected write set

`WeeklyReportService.cs`, `WorldEventContracts.cs`, `WorldKnowledgeProjectionService.cs`, `AwakeRuntime.cs`, `ProbeExtension.cs`, `AwakeEventBehavior.cs`, `AwakeTerminalBehavior.cs`, `WeeklyReportBrowserVM.cs`, v1/v2 schema and fixtures, Chinese/English language resources, focused tests/SDK smoke. The storage key/chunk/manifest protocol is excluded.

## Acceptance

| Case | Evidence |
| --- | --- |
| Native false | Formal report is generated/reused from a complete storage window while Native readiness is false. |
| v1/v2 | Existing v1 fixture validates unchanged; v2 fixture validates with source-fact closure, projects and uses versioned ID. |
| Idempotency | Same complete window and same fingerprint reuses report; different content conflicts. |
| Empty week | Valid empty source window yields a valid empty report; it can repair an invalid report payload for that version/window. |
| Menu | Formal, preview and unavailable states reach BrowserVM and both language resources with locked wording. |
| Save/load | E5 user-run evidence proves completed report identity/text/source closure remain stable. |

## Non-goals

- No fact-storage keys, chunk protocol, permission-policy redesign, event observers, worldbook content, Persona/dialogue wiring, quarterly reports, sync or game launch.

## Gate

This plan is not implementable until the coupling contract is independently reviewed as `APPROVED`. After that review it starts a new implementation review budget and still requires user sign-off before code. The current status is not a user sign-off and does not authorize v2 implementation.
