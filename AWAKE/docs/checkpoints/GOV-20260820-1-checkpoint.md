# GOV-20260820-1 Checkpoint

- `task_id`: `GOV-20260820-1`
- `batch_id`: `awake-governance-implementation-20260820`
- `status`: `offline_verified`
- `build_id`: `awake-20260820-governance-001`
- `files_changed`: four Codex Skills; `AGENTS.md`; `docs/AWAKE-CURRENT.md`; `docs/AWAKE-ROADMAP.md`; `docs/AWAKE-VALIDATION.md`; `src/AwakeConstants.cs`; `src/SubModule.cs`; `src/WorldbookRuntime.cs`; `AWAKE.Tests/Program.cs`; `tools/release_check.ps1`; queue/build records; local dist DLL.
- `verification`: Skill frontmatter/UTF-8/responsibility scans passed; BuildId/hash test was written red then made green; dual API builds 0w/0e; SdkSmoke PASS ALL; JSON/XML parse 0 failures; localization 226/259/259; asset boundary 117; release check OK/BLOCKED_SYNC; MAF Preview lint exit 0.
- `candidate_hash`: source/dist DLL `9A9284ECBF96102BC681E8DBF937C523E7A397B0CE85F1BF52531E361121FA65`; game DLL `FF98881E58102E992CFFE2B959222B406165762BEA2E2E3819E1647AF8F61021`.
- `worldbook`: manifest `2115664E1BB6BB26E63D8CB2097B891B51C03A00504D76DF6F8520FD9448468A`; 759 JSON / 335 rules / 415 legacy persona files; placeholder audit still has 15 pre-existing findings.
- `known_limitations`: game directory not overwritten; no Bannerlord launch; no E4/E5 evidence; 0.2.1 remains blocked_sync; MAF preview warnings are non-blocking but not automatically resolved.
- `next_action`: after explicit user authorization, synchronize the frozen candidate to the game directory without changing BuildId, then collect a matching runtime log.
- `last_error`: none active. Earlier local Worker medium audit timed out and was abandoned without replay; a release-check PowerShell 5.1 compatibility issue was found and fixed during validation.