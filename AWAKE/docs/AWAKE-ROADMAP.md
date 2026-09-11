# AWAKE Roadmap

> Version goals only. Batch execution state belongs in `AWAKE-CURRENT.md` and checkpoints.
> Updated: 2026-08-20

## Version Objectives

| Version | Main goal | Entry criteria | Release gate |
|---|---|---|---|
| `0.1.x` | Runtime core: Marcus integration, routes, storage, permissions, diagnostics | Core contracts exist | E1/E2 core smoke |
| `0.2.x` | Content package foundation: worldbook, events, letters, NPC proactive basics | Runtime core stable | E3 candidate plus E4 gameplay closure |
| `0.3.x` | World simulation and social loop: reports, proactive dialogue, hub/session integration | 0.2.1 gameplay evidence | E4 entry/settlement/persistence evidence |
| `0.4.x` | Relationships and memory: accepted memory, promises, rumor propagation, timeline safety | Stable social loop | E4/E5 save-load regression |
| `0.5.x` | Content system: event packages, four worldbook styles, authoring contracts | Stable runtime contracts | Content audit plus gameplay sample |
| `0.6.x` | Player experience: UI, diagnostics, media and onboarding | Core UX paths complete | UX/gameplay acceptance |
| `0.7.x` | Ecosystem: content packages, external persona/worldbook interfaces, compatibility | Public contracts frozen | Compatibility matrix |
| `0.8.x` | Performance and author tools: indexing, caching, batch tools, Persona Workbench integration | Feature surface stable | Performance budget and author workflow |
| `0.9.x` | Release candidate: regression, packaging, migration, crash and fallback cleanup | No P0/P1 blockers | RC checklist |
| `1.0.0` | Stable public release | 0.9.x accepted | Full release, migration and rollback plan |

## Current Order

1. Keep `GOV-20260820-1` as completed offline evidence.
2. Repair the Persona Definition dist layout and add release/synchronization gates in a separately reviewed preparation batch.
3. Re-freeze an attributable `0.2.1` candidate, then synchronize only after explicit authorization.
4. Use matching BuildId logs to close the minimum 0.2.1 gameplay loop and its save/load checks.
5. Fix evidence-backed gameplay blockers before opening the approved `0.3.0` batch; do not expand later-version mechanisms while 0.2.1 is blocked.

## Version Rules

- Version changes follow playable acceptance, not file count or implementation volume.
- A prior verified build is historical evidence, not evidence for a new candidate.
- A release candidate must have attributable hashes and separated offline/game/save-load evidence.