# Plan: AWAKE Safe Packaging, Synchronization, and Candidate Re-freeze
_Locked via grill — by Codex + user approval on 2026-08-20_

## Goal
Repair the Persona Definition package layout, make release validation reject the same defect, add a deterministic and config-preserving synchronization workflow, generate a new attributable BuildId without changing the public `v0.2.0` version, synchronize the re-frozen candidate to the stopped Bannerlord installation, and leave AWAKE at E3 ready for the existing `0.2.1` gameplay and save/load acceptance run.

## Approach
1. Add regression gates before changing artifacts:
   - release validation must require the correct Persona registry and definition paths in source and dist;
   - source/dist hashes for those files must match;
   - the nested `persona_definitions/persona_definitions/` path must be absent from dist and game after synchronization;
   - developer-only `AGENTS.md` must not be treated as a runtime synchronization requirement.
2. Repair dist packaging deterministically:
   - copy the two authoritative source Persona files to their manifest-defined relative paths;
   - remove only the verified obsolete nested Persona files and empty nested directories;
   - keep all unrelated dist files untouched.
3. Add `tools/sync_module.ps1` as the authoritative synchronization entry:
   - require an explicit `-ConfirmGameSync` switch for any game-directory write; without it, reject even when Bannerlord is stopped;
   - validate absolute source/dist/game roots against the expected AWAKE module roots, resolve final paths, reject reparse-point components and path escapes, and verify the game process is absent;
   - build the managed file list from a fixed contract, not arbitrary globs: `SubModule.xml`; `bin/Win64_Shipping_Client/Awake.dll`; `GUI/Prefabs/AwakeMessenger.xml`, `DeveloperCheck.xml`, `NpcDialogue.xml`, `SceneDialogueStatus.xml`, `WeeklyReportBrowser.xml`, `WorldEventInbox.xml`; `ModuleData/Languages/awake_strings.xml`, `language_data.xml`, `CNs/awake_strings-zh-HANS.xml`, `CNs/language_data.xml`; `ModuleData/Worldbook/manifest.json`, `migration_report.json`; and regular files only under the manifest-declared runtime directories `rules`, `personality_background`, `unnamed_persona`, `voice_mapping`, `event_data`, `debt`, `dialogue_history`, `compressed_memory`, and `persona_definitions/definitions`, with the registry file `persona_definitions/tag_registry.json`; reject any source path outside this fixed contract and never copy `AGENTS.md`, `docs/**`, `BUILD_VERIFICATION.txt`, task queues, or developer artifacts;
   - preserve `Config.json`, `Logs/**`, `PlayerExports/**`, `Runtime/**`, `Saves/**`, `Cache/**`, and all unknown game-only files; report them as preserved;
   - stage source files into a temporary directory, validate staged hashes and layout, and make `-WhatIf` perform no backup, directory creation, deletion or write; for a real sync, create a transaction manifest with `planned`, `backed_up`, `applied`, `verified`, `rollback_started`, `rollback_verified` and `failed` states covering overwritten files, new files, removed files and created directories, fail before mutation if backup creation fails, apply the batch, verify all final hashes, and on any failure restore files and directories then re-verify every managed and removed path; a rollback failure blocks E3;
   - remove only the two verified nested Persona files `ModuleData/Worldbook/persona_definitions/persona_definitions/tag_registry.json` and `ModuleData/Worldbook/persona_definitions/persona_definitions/definitions/hero_default.json`; if any other file exists under the nested directory, refuse cleanup and require manual review; apply canonical path, file-type, parent-boundary, and expected-hash checks, then remove empty nested directories only;
   - support `-SkipGame` and `-WhatIf`, and emit a machine-readable summary containing exact command parameters, authorization state, pre-write process snapshot, source/dist/game per-file hashes, copied, unchanged, preserved, removed, created-directory, backup, rollback and final verification lists;
   - add PowerShell 5.1 syntax, `-WhatIf`, process-block, missing/extra-path, file-directory conflict, and injected-failure rollback tests.
4. Re-freeze the runtime candidate:
   - update BuildId to `awake-20260820-syncpack-001` while keeping version `0.2.0`;
   - update BuildId regression expectations;
   - build Bannerlord API `1.3.15` and `1.4.8`;
   - run SdkSmoke, JSON/XML parsing, localization, asset, placeholder and release checks;
   - copy the `1.3.15` DLL and managed package files into dist through the new workflow.
5. Synchronize E3 after the offline candidate is green:
   - confirm `-ConfirmGameSync` is present as the sole authorization gate, confirm Bannerlord is stopped immediately before the write, and record the exact command parameters and process snapshot;
   - back up only files that will be overwritten or explicitly removed to a timestamped temporary directory;
   - run the authoritative sync script without `-SkipGame`;
   - verify and record the exact source/dist/game hash manifest for every managed file, correct Persona paths, absence of nested duplicates, preserved paths, backup location, copied/unchanged/removed/preserved lists, rollback status, command parameters, authorization switch and pre-write process snapshot.
6. Update `AWAKE-CURRENT.md`, validation matrix, task queue, checkpoint and `BUILD_VERIFICATION.txt` with the new BuildId, hashes and E3 evidence.
7. Move the active state to `pending_game` and provide the existing `0.2.1` E4/E5 checklist: matching BuildId module/worldbook logs, map dialogue history, memory commit/read-load timeline behavior, worldbook Persona counts, UI/diagnostic paths, duplicate-submit and restart checks.

## Key decisions & tradeoffs
- Public version remains `v0.2.0`; BuildId changes because the frozen candidate and distributable artifact set change.
- Synchronization is allowlist-based and non-mirroring. It preserves unknown game-only data rather than deleting everything absent from dist.
- Cleanup is intentionally narrow: only the proven nested Persona duplicate is deleted automatically.
- `Config.json` and runtime/user data are never sourced from dist during game synchronization.
- `AGENTS.md` may remain in historical dist packages, but the new sync path excludes it and release validation does not use it as runtime equality evidence.
- E3 can be completed offline; E4/E5 still require the user to launch Bannerlord and provide matching logs and save/load evidence.

## Risks / open questions
- Existing game-only files outside the preserved roots are retained; the sync report must make them visible so future stale managed files can be handled explicitly rather than silently deleted.
- PowerShell 5.1 compatibility is required because existing project checks invoke Windows PowerShell; syntax and behavior tests must run under that host.
- OneDrive may alter file attributes during reads; synchronization must use exact resolved paths and verify hashes after staging and after copying.
- If any build/test changes the frozen source unexpectedly, the batch stops and re-establishes candidate hashes before game synchronization.
- A failed multi-file copy must restore the pre-sync target set and report whether rollback itself succeeded; a rollback failure blocks E3.
## Out of scope
- No gameplay feature changes, Persona schema changes, content rewriting, version bump, game launch, MCM changes, save-file mutation, Provider call, Git commit or push.
- No general-purpose mirror/delete engine and no cleanup of unrelated historical artifacts.