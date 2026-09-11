# Worldbook Studio Launcher Checkpoint

## Batch

- `task_id`: `WORLDBOOK-STUDIO-LAUNCHER-20260822`
- `status`: `offline_verified`
- `scope`: Worldbook Studio source/tools only; no game directory or frozen runtime candidate changes.

## Delivered

- Windows x64 self-contained portable ZIP package with root `Awake.WorldbookStudio.Launcher.exe`.
- Launcher owns first-run workspace selection, LocalAppData settings/logs, Web child lifecycle, health handshake, browser open/fallback and safe shutdown.
- Web accepts production bootstrap only from Launcher, validates package/workspace/schema/instance binding, exposes `/health` and authenticated shutdown.
- Launcher exit-code reporting now preserves native Windows codes as `0x........ (unsigned)` instead of overflowing a checked `int` when a child fails before health readiness.
- Core path policy rejects protected Bannerlord trees, alternate recognized Bannerlord installations, `Modules`/`PlayerExports`, package roots and reparse-point traversal.
- Release pipeline produces `manifest.json`, `SHA256SUMS.txt`, external ZIP sidecar hash and ZIP-extract smoke.

## Evidence

- `scripts/test.ps1`: `F01-F62 PASS` (`62/57` harness cases), including author-mode CAS/revision/source-read-only coverage, bootstrap round-trip, env binding, alternate game tree rejection and malformed frame rejection.
- `scripts/package.ps1 -RunSmoke`: final Release build `0 warnings / 0 errors`, release-check `PASS`, ZIP extract smoke success `PASS` with `DOTNET_ROOT` and `DOTNET_ROOT(x86)` empty.
- `scripts/smoke.ps1 -Zip ... -Browser failure`: browser failure fallback `PASS`; independent success smoke also `PASS`; no orphan Web/Launcher process remains.
- Final package sidecar SHA-256: `369654363203f220ed9c3047dc1de063bf18dd88b497b7581a467f0f7dafde19`.
- Final package contains `585` files; manifest covers `583` payload files and excludes only `manifest.json`/`SHA256SUMS.txt`.
- Package: `tools/worldbook-studio/artifacts/WorldbookStudio-win-x64.zip`.

## Remaining

- User should manually double-click the ZIP's Launcher and confirm the Chinese first-run dialog, workspace selection, and real default-browser opening.
- No Bannerlord sync, game launch, frozen candidate change or game-directory validation was performed in this batch.

## Next action

- Use the ZIP package for content-editor acceptance; if the manual UI pass finds an issue, create a new Launcher batch without touching the frozen AWAKE runtime candidate.
