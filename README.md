# AWAKE: Awakened World AI

AWAKE is a content-free AI world runtime for Mount & Blade II: Bannerlord. It consumes MarcusAIFramework and does not depend on AnimusForge, Love & Hate, or the Slanesh's Embrace content pack.

## Repository layout

```text
AWAKE/                     # runtime module
  src/                     # runtime source
  ModuleData/              # localization
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
dotnet build -c Release -p:BannerlordApi=1.3.15
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
