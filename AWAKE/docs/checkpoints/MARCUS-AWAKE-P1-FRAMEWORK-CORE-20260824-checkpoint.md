# Marcus-Awake P1 Framework Core checkpoint

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P1-FRAMEWORK-CORE-20260824`
- `status`: `offline_verified`

## files_changed

- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/MarcusAwakeFramework.csproj`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/ApiPrimitives.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/FrameworkIdentity.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/FrameworkErrors.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/RequestContext.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/CapabilityAndPermission.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/SessionApi.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/GameDataAndContext.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/CommandAndSaveApi.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/IpcAndDiagnosticsApi.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/HostApi.cs`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/tests/MarcusAwakeFramework.Tests.csproj`
- `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/tests/Program.cs`
- `_houkai_merge/AWAKE/docs/evidence/MARCUS-AWAKE-FRAMEWORK-CORE-API-20260824.json`

## verification

- Core command: `dotnet build MarcusAwakeFramework.csproj -c Release --nologo` → `0 warnings / 0 errors`.
- Test command: `dotnet build tests\MarcusAwakeFramework.Tests.csproj -c Release --nologo` → `0 warnings / 0 errors`.
- Runtime contract smoke: `_build_out\tests\Release\MarcusAwakeFramework.Tests.exe` → `PASS ALL: 7 Framework Core contract tests`.
- API surface: `67` exported types recorded in the evidence JSON.
- Core DLL SHA-256: `DEF0FBCCCBD1A51070599AA6409421E0240E26DA3C834E443462BD115265C72B`.
- Test executable SHA-256: `B0B8875638D8B99872D8D9F86B5E2607BDA5FFA3302B131F7F6BDB9D2CFA2CBC`.
- Static P1 boundary scan: no `MarcusAIFramework`, `TaleWorlds`, `Campaign.Current`, `HttpClient`, `SQLite`, `CancellationToken.None`, `.Wait()` or `.Result` references in P1 source/tests.
- P1 contract cases covered: identity/errors; capability owner/dependency/cycle; permission fail-closed; session lifecycle; GameData paging/visibility/deadline; context budget; command idempotency/settlement; Save Anchor state; IPC nonce/sequence/safe retry/ordered buffering; backpressure; host lifecycle/probe.

## known_limitations

- This is an independent Framework Core contract layer; it is not yet referenced by `Awake.dll`.
- No real Named Pipe ACL/Bootstrap process proof, Runtime Service, CredentialBroker, EgressBroker, SQLite/RAG, Provider, or Bannerlord adapter is implemented in P1.
- No `AWAKE.csproj`, `SubModule.xml`, existing `src`, MCM, Runtime Service, DevTools, worldbook, `dist`, game directory, frozen candidate, save or database was modified.
- No Bannerlord launch, gameplay, save/load, E3 synchronization, E4 or E5 evidence is claimed.
- In-memory services are deterministic contract doubles, not production persistence or network implementations.

## next_action

- Release the P1 implementation lease after recording this checkpoint.
- Before any P2 integration, create a separate P2 checkpoint and review the new write set; P1 approval does not authorize changing `AWAKE.csproj`, `SubModule.xml` or existing `src`.

## last_error

- Initial test invocation used an incorrect relative path; corrected without product impact.
- Initial context test expectation assumed two included contributions; corrected to match the declared visibility and token budget.
- Final P1 build and smoke test are clean.