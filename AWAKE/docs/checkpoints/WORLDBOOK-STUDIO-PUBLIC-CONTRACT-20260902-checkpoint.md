# Worldbook Studio Public Contract Checkpoint

- Batch: `WORLDBOOK-STUDIO-PUBLIC-CONTRACT-20260902`
- Status: `approved`
- Revision: `1`
- User authorization: approved on `2026-09-02`

## Fixed findings

- `WBPC-20260902-F01`: Authority Web/CLI success responses now use allowlisted projections; document paths are workspace-relative or opaque, and raw record paths are not returned.
- `WBPC-20260902-F02`: `WB-AUTHORITY-MUTATION-UNKNOWN` maps to HTTP `503` and the same public code/status is emitted by CLI with `side_effect=unknown`.
- `WBPC-20260902-F03`: the route registry now declares `design_catalog`; the partial runtime surface is explicit and only advertises the currently supported action IDs.
- `WBPC-20260902-F04`: a real Web host smoke verifies retired routes return `410`, stable error code, matching correlation ID, and `side_effect=none`.

## Changed files

- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthorityPublicProjection.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/Program.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/AuthorityHttpErrorProjection.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/Program.cs`
- `tools/worldbook-studio/contracts/authoring-action-route-registry.v1.json`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Program.cs`
- `tools/worldbook-studio/scripts/public-contract-smoke.ps1`
- `tools/worldbook-studio/scripts/test.ps1`

## Verification

- Web Release build: `0 warnings / 0 errors`.
- CLI Release build: `0 warnings / 0 errors`.
- Main harness: `113/113 PASS`.
- AuthorityGate: `5/5 PASS`.
- Contract checker: `1132/1132 PASS`.
- Public contract smoke: `PASS`.
- Full Worldbook Studio regression: editor `12/12`, editor safety `4/4`, editor content `7/7`, Batch `18/18`, Draft `20/20`, Draft/Batch HTTP smoke `PASS`, main harness `113/113`, public contract smoke `PASS`.

## Boundaries

- No Bannerlord launch.
- No game-directory synchronization.
- No real Provider access or API key read.
- The 79-action registry remains a design catalog; unimplemented future AI workflow actions are not claimed as runtime-complete.

## Next action

- Independent final verdict: `VERDICT: APPROVED`; user signoff recorded on `2026-09-02`.
- Then create a new review state for `WORLDBOOK-STUDIO-AI-AUTHORING-20260902`; do not alter the signed-off SafeId-A state.