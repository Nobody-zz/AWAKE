# Worldbook Pilot Human Decision Table — 2026-09-03

## Decision boundary

- Authority source: `C:\Users\26811\Downloads\卡拉迪亚编年史\卡拉迪亚编年史`
- Source snapshot: `source.awake.worldbook.download-20260903`
- Snapshot SHA-256: `629a24fc633c035aa83d369b54d35cd3a893eb429d5fcf501e2e2ff0c717f129`
- This table is a decision aid only. It does not modify source files, Studio data, AWAKE `ModuleData`, registries, or published content.
- Until decisions are recorded, all affected items remain `needs_review`.

## Decisions

| ID | Item | Recommended decision | Alternative | Consequence |
|---|---|---|---|---|
| D1 | `personality_background/lord_1_14__拉盖娅.json` invalid CRLF | Repair only in a new immutable source-repair copy, preserve original hash, then reparse | Exclude file from the current package and defer repair | Full package remains blocked until `parse_error_count=0`; exclusion is allowed only for a bounded pilot |
| D2 | `rule_吕卡隆` assertion shape | Split into settlement geography, imperial history, factional interpretation, and dynamic state assertions | Keep one document but split all expressions by subject | Splitting improves Studio assertion provenance and prevents mixed permissions; keeping one document preserves topic grouping but increases permission ambiguity |
| D3 | `rule_吕卡隆` TextMappings | Validate `town_ES4`, `clan_empire_south_1`, and `empire_s` against an authoritative entity registry before migration | Mark all mappings unresolved and omit them from the first draft | Validation enables typed runtime mapping; omission loses dynamic names/status until a later mapping pass |
| D4 | `rule_塞堤斯河` duplicate variants 0/1 | Keep one shared expression only if the empire condition does not change meaning; otherwise rewrite the conditioned expression | Retain both with an explicit reason | Keeping both without semantic distinction creates duplicate output; rewriting preserves access distinction |
| D5 | `rule_贝恩兰岛` duplicate variants 0/3 | Compare scouting and lord/culture audiences; rewrite the narrower expression if detail differs | Merge to one expression with the broader intended audience | Rewrite is safer for permission clarity; merge is smaller but can erase audience intent |
| D6 | `rule_车尔特格山` duplicate variants 0/1 | Keep the settlement expression and rewrite the scouting expression only if scouting grants additional detail | Merge and retain the narrower condition as a diagnostic note | Do not silently delete the condition; the choice affects future `min_detail` mapping |
| D7 | `rule_攻城塔` heuristic truncation signal | Treat as parseable/P2 manual content review; do not repair automatically | Re-open source comparison if an external authoritative copy exists | No source repair is justified by the current evidence alone |
| D8 | Pilot source registry | Do not assign `license_status`, `use_status`, `content_tier`, universe, or era without authority | Keep package compile blocked and retain `needs_review` | A provisional base label must not become a publishable base package |
| D9 | 30-file pilot candidate | Keep prior 30 authoring-shaped files `superseded` and out of the repair pass | Start a new candidate batch after decisions | Reusing superseded candidate hashes would mix evidence across batches |

## Required response format

Record decisions as:

```text
D1=repair-copy
D2=split-assertions
D3=validate-registry
D4=rewrite-if-meaning-diff
D5=rewrite-if-meaning-diff
D6=rewrite-if-detail-diff
D7=manual-review-only
D8=keep-blocked
D9=exclude-superseded
```

For every changed decision, preserve:

- decision ID
- decision timestamp
- decider
- source snapshot ID
- source snapshot SHA-256
- source file SHA-256
- JSON locator
- reason
- expected loss or gain
- next batch ID

## Current recommended disposition

- Resolve the confirmed package-level parse gate through a separate source-repair copy.
- Split `吕卡隆` before any Studio authoring draft.
- Do not automatically delete the three duplicate expressions; decide based on audience/detail semantics.
- Keep `攻城塔` as a P2 manual review item.
- Keep the source registry and all 30 pilot documents out of compilation/publishing until source authority and permission inputs are accepted.

