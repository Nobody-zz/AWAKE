# AWAKE Runtime/content repair checkpoint — 2026-08-31 pass 1

- task_id: `MARCUS-AWAKE-RUNTIME-CONTENT-REPAIR-20260831`
- batch_id: `AWAKE-RUNTIME-CONTENT-REPAIR-20260831`
- status: `source_verified`
- candidate: `awake-20260829-marcus-embedded-002` remains unchanged and historical; current source candidate is `awake-20260831-marcus-embedded-003`, source/staging only.
- files_changed: `AwakeRuntime.cs`, `WorldEventInboxOverlay.cs`, `AwakeTerminalBehavior.cs`, `AwakeTranscriptService.cs`, `NpcDialogueService.cs`, `AwakeOnboardingService.cs`, `EventDialogueQueue.cs`, `ProbeExtension.cs`, `AwakeMessengerVM.cs`, `NpcProactiveService.cs`, `AwakeEventBehavior.cs`, `AwakeEventEngine.cs`, `WorldStateStore.cs`, `RuntimeServiceHost.cs`, `tools/worldbook-runtime-production-smoke/Program.cs`, `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsBoundary.cs`, core localization/content-boundary files.
- verification: AWAKE Release `0 warnings / 0 errors`; Runtime Service Release `0 warnings / 0 errors`; provider outcome ledger `6/6`; production smoke `18/18`; P3B `19/19`; P3C `7/7`; P3D-A0/A1/A2 pass; Provider stream models `3/3`; API layers and P3A E1 pass; runtime staging package `195` payload files validates.
- known_limitations: no Bannerlord E4/E5; no game-directory sync; no real Provider or API key; candidate `003` has not been promoted to `dist` or game modules.
- next_action: keep Runtime batch closed at source verification; obtain separate user signoff for UI Lab, Worldbook Studio, and Persona Workbench before implementation.
- last_error: none in current build and harness runs.
