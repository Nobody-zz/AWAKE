# AWAKE: Awakened World AI

AWAKE is a content-free AI world runtime for Mount & Blade II: Bannerlord. It consumes MarcusAIFramework and does not depend on AnimusForge, Love & Hate, or the Slanesh's Embrace content pack.

## Repository layout

```text
AWAKE/                     # runtime module
  src/                     # runtime source
  ModuleData/              # localization only (no world book)
  GUI/                     # runtime UI
  tools/                   # validation scripts
  docs/                    # current AWAKE docs
AWAKE.Tests/               # Awake.SdkSmoke
MarcusAIFramework_Reference/
  SDK_20260815/            # SDK reference used by this repo
```

## Build

Set `GamePath` to your Bannerlord installation, or rely on the default:

```powershell
cd AWAKE
dotnet build -c Release -p:BannerlordApi=1.4.8 -m:1
```

Run the smoke test:

```powershell
cd AWAKE.Tests
dotnet build -c Release
bin\Release\net472\Awake.SdkSmoke.exe
```

Validate localization:

```powershell
cd AWAKE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\validate_localization.ps1
```

## Content pack

The Slanesh's Embrace content pack is intentionally excluded from this repository. The runtime must stay clean, content-free, and independently playable.

The world book is not published here either. Earlier commits bundled the Calradic Chronicle backup in the superseded `awake.worldbook.v1` layout, which the current runtime rejects (`WorldbookRuntime` accepts only `awake.worldbook.v2` or `awake.worldbook.registry.v1`). That tree has been removed from HEAD; it still exists in the git history and in the local workspace only.

## Remote

- Repository: `https://github.com/Nobody-zz/AWAKE.git`
- Authoritative workspace: `D:\AWAKE-Dev`
- The public GitHub mirror is downstream; synchronize from this workspace only.

After runtime code changes, run the sync script, then commit and push from this directory:

```powershell
cd D:\AWAKE-Dev
# This working tree hosts several concurrent agents sharing one index.
# Do NOT stage the whole tree (`git add .`): commit only the paths you changed.
git commit -m "Update AWAKE runtime" -- AWAKE/src AWAKE/framework AWAKE/tools
git push origin main
```
