[b]Marcus AI Framework SDK Preview[/b]

Version: 0.1.0-preview.1
Public API: 0.1
Protocol: 1.0
Bannerlord APIs: v1.4.8 and v1.3.15

This directory is a compile-time SDK for extension authors, not an installable mod. An extension references ref/MarcusAIFramework.dll but must not redistribute that DLL, TaleWorlds DLLs, the four prerequisite DLLs, credentials, databases, or real asset paths.

[b]Quick start[/b]

1. Copy templates/ExtensionTemplate and replace YourCompany.YourExtension, display text, and version.
2. Build the framework for the target API, then point FrameworkReferenceRoot at this SDK directory.
3. Declare the exact Bannerlord API allowlist, permissions, and capability URIs in ExtensionManifest.
4. Use fake-host/FakeHost.cs and test-kit/MafAssertions.cs for contract tests without TaleWorlds objects.
5. Run analyzers/maf-lint.ps1 -ExtensionRoot <path>. The Preview linter reports warnings and never edits files.
6. Build separately against real v1.4.8 and v1.3.15 references. FakeHost does not replace an in-game smoke test.

[b]Contract boundary[/b]

The SDK verifies the compile-time shape of public DTO and governance contracts. It does not prove TaleWorlds adapters, Gauntlet layout, provider network protocols, or in-game lifecycle behavior.
