# AWAKE: Awakened World AI - v0.2.0 (transitional)

AWAKE is a generic AI world runtime for Mount & Blade II: Bannerlord. NPC intelligence, cross-session memory, world knowledge, events, command governance, and effect settlement belong to the runtime and are not tied to any specific worldview.

`SlaneshsEmbraceContent` is the content-pack base (worldbook, events, letters, non-erotic NPC proactive behavior). The goddess persona and the erotic mechanics are separate content-pack branches, not part of the runtime.

## Current state

- The runtime is content-free: it no longer references goddess, altar, body/estrus, captive, or letter code.
- All code names are AWAKE: ModId `AWAKE`, DLL `Awake.dll`, namespace `Awake`, routes `AWAKE.route.*`, storage `awake.*`, logs `Awake.log`.
- Runtime source and localization file names now use `Awake*` / `awake_*`; the `SlaneshsEmbraceContent` pack keeps its own Slaanesh identity.
- Content-pack base and goddess/erotic branches are frozen under `SlaneshsEmbraceContent/frozen`.
- Build: 0 warnings, 0 errors; `Awake.SdkSmoke` PASS ALL; localization validation passes.
- NPC deep-talk now lives in the AWAKE command deck: press the command-deck hotkey (default `Y`, configurable in MCM) to open a dedicated panel with "Deep Talk (AWAKE)" and "Developer Check", then reuse `NpcDialogueLauncher` for the overlay or native dialogue fallback. No extra town-menu options are injected.
- Runtime user-facing copy now uses AWAKE/醒世; Slanesh-era wording no longer leaks into permission prompts, menus, or dialogue titles.

## Directory layout

```text
_houkai_merge/
  AWAKE/                    # runtime project
    src/                    # runtime source
    docs/                   # roadmap, split, API contract, classification
    dist/                   # release output
    ModuleData/             # runtime localization
    GUI/                    # runtime UI
    tools/                  # validation scripts
  AWAKE.Tests/              # runtime SdkSmoke
  SlaneshsEmbraceContent/   # content pack (base + frozen branches)
  MarcusAIFramework_Reference/  # SDK/reference
  archive/                  # history, backups, deprecated projects
```

## Version roadmap

- `0.1.x`: runtime core baseline.
- `0.2.x`: content-pack base.
- `0.3.x`: world simulation.
- `0.4.x`: relationships and memory depth.
- `0.5.x`: content system and tooling.
- `0.6.x`: experience polish.
- `0.7.x`: ecosystem and cross-mod support.
- `0.8.x`: performance and observability.
- `0.9.x`: stability and release.

Full plan: `docs/Awake-Roadmap-0.1-0.9-20260815.md`.

## Next step

Complete the v0.1.x runtime loop (NPC deep-talk entry, public API implementation, storage pipe verification), then connect the v0.2.x content-pack base.
