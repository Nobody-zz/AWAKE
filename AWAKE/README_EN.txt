# AWAKE: Awakened World AI

A **generic AI world runtime** for Mount & Blade II: Bannerlord — NPC intelligence, cross-session memory, world knowledge, events, command governance, and effect settlement all live in the runtime and are **not tied to any specific worldview**.

> **This mod contains no adult content** and ships no content pack. Worldview content (worldbook, events, letters) lives in separate, decoupled content packs.

---

## 1. What this is

AWAKE makes Calradia's NPCs actually remember things, hold a conversation, and know what they should know. It does not change combat or economy gameplay; it adds a **runtime layer for knowledge and dialogue**:

| Capability | What it does |
|---|---|
| **World knowledge** | A searchable knowledge base. When asked, an NPC's answer is gated by *who knows × when they knew × whether they believe it* |
| **Cross-session memory** | Save → quit → load; what an NPC remembers is still there |
| **NPC initiative** | NPCs approach you, send letters, bring up old matters |
| **Command governance** | NPC "actions" go through one auditable, revertible command channel |
| **Effect settlement** | Command-driven world changes follow explicit rules, not free improvisation |

---

## 2. Current state (as of 2026-09-30)

> Every acceptance criterion is checked **in-game**. Green offline does not count.

### Working

- ✅ **Has run in-game**: 2026-09-14 23:14–23:19 — `Awake.dll` loaded, registered, full session, clean exit (`pending_writes=0 dropped=0`; `rgl_log` 3553 lines, 0 exceptions).
- ✅ **Runtime portrait on screen**: `portrait_texture_ready` 212×360 ×3.
- ✅ **Image pipeline**: local endpoint → 200, 6.6 s, 512×512 JPEG (verified real by magic bytes `ffd8ffe0`).
- ✅ **NPC dialogue panel opened in-game**: `hero:lord_1_18`, `Negotiation` ↔ `Chat`, clean close.
- ✅ **Worldbook deployed**: 790 entries (`geography 416 / economy 151 / politics 132 / war 71 / culture 20`); repo-side and game-side byte-identical; registry hashes match the package byte-for-byte.
- ✅ **Offline tests green**: `Awake.SdkSmoke` `total=64 passed=64 failed=0`.

### Known defects (all observed in-game)

| Defect | Impact |
|---|---|
| `awake.world_fact.root_corrupt` ×4 | Weekly report / WeeklyDynamics chain unusable |
| `native_readiness` null-reference | Recurring since 09-10, yet also `Ready` repeatedly in the same session ⇒ **ordering/race**, not a constant fault |
| First two minutes `awake_host_resolution runtime_not_ready` | First two minutes of a session are non-functional |
| `npc_dialogue_open_failed` ×2 | Proactive-dialogue candidate built and accepted — **but the mouth won't open**. v0.3 blocked here |
| State written to `AwakeState/unbound/` (inferred, unconfirmed) | Storage bound before the save id was known, bound to nothing. v0.2 blocked here |

---

## 3. Requirements

| Item | Value |
|---|---|
| Game | Mount & Blade II: Bannerlord, **base game v1.4.8** (since 2026-09) |
| Target framework | `net472` |
| Required deps | `Bannerlord.Harmony`, `Bannerlord.ButterLib`, `Bannerlord.UIExtenderEx`, `Bannerlord.MBOptionScreen` |
| Official modules | `Native`, `SandBoxCore`, `Sandbox` |
| **Optional DLC** | **`NavalDLC` — not required.** See §5 |

---

## 4. Build and verify

```powershell
cd AWAKE
# Default BannerlordApi=1.4.8 (the current game version); pass it explicitly when switching
powershell -File tools\build.ps1 -BannerlordApi 1.4.8
```

`build.ps1` asserts that `BannerlordApi` matches the Native version under `GamePath` **exactly**, and fails otherwise (this guards against building for the wrong target and not noticing).

```powershell
# Offline smoke test
cd AWAKE.Tests
bin\Debug\net472\Awake.SdkSmoke.exe

# Localization validation
cd AWAKE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\validate_localization.ps1
```

> ⚠️ **Green offline ≠ working in-game.** All version criteria are judged in-game, not in a local preview.

---

## 5. About NavalDLC

**AWAKE does not hard-depend on the DLC — it works without it.** On the official side NavalDLC is `OfficialOptional`.

But it loads by default (`DefaultModule=true`), so it **must be handled**. Current state **is three-layered — not "nothing done"**:

| Layer | Status |
|---|---|
| **Tooling / reference** | ✅ **Built** (08-24): `docs/mappings/war-sails-reference/` (528 EN/ZH rows) + entity registry carries DLC state (`hero_official_dlc_not_installed: 53`) |
| **Content (worldbook)** | 🟡 Partial: 4 canon entries cover the DLC's **Nord** faction (`clan-clan_nord_1/2/3`, `military-nord`), but carry **no DLC condition** |
| **Runtime** | ❌ Not wired: zero `NavalDLC` hits in `src/*.cs`; worldbook schema has no DLC field |

Full assessment (three-layer correction, DLC content scale, to-dos N0–N2, open questions) is in **`docs/DLC-COMPAT-NAVAL-20260930.md`**.

---

## 6. Repository layout

```text
D:\AWAKE-Dev/
  AWAKE/                        # runtime module (main body)
    src/                        # runtime source (163 .cs files)
    ModuleData/                 # localization + worldbook package (Worldbook/packages/calradia/)
    GUI/                        # runtime UI (Prefab / Brush / SpriteParts)
    framework/                  # build dependency (ProjectReference, do not remove)
    tools/                      # build, sync, validation, Worldbook Studio
      worldbook-studio/         # worldbook toolchain (authoring -> compile -> deploy)
    docs/                       # roadmap, contracts, audits, assessments
  AWAKE.Tests/                  # offline smoke test Awake.SdkSmoke
  MarcusAIFramework_Reference/  # framework SDK reference
```

---

## 7. Not in this repo

- **Content packs**: worldview content ships as separate packs; this repo hosts the runtime only.
- **Legacy worldbook**: earlier commits bundled a Calradic Chronicle backup in the superseded `awake.worldbook.v1` layout, which the current runtime rejects (`WorldbookRuntime` accepts only `awake.worldbook.v2` or `awake.worldbook.registry.v1`). That tree has been removed from HEAD.

---

## 8. Version roadmap

**The criterion is what the player can newly do — not how much code was written.**

| Version | What the player can newly do |
|---|---|
| **v0.1 Lit** | Install, get in, see AWAKE running |
| **v0.2 Remembers** | Save → quit → load; state persists and is consistent |
| **v0.3 Alive** | NPCs approach you, send letters, bring up old matters |
| **v0.4 Has a face** | Panels, icons, and **portraits** all appear |
| **v0.5 Holds up** | Long sessions without crashes; coexists with common mods |
| **v1.0** | Ready for public release on the Workshop |

See **`docs/AWAKE-ROADMAP.md`**.

> The older `0.1.x`–`0.9.x` table, ordered by content module, is **retired** (it did not match the project's actual five parallel tracks).

---

## 9. Release compliance (required before v1.0)

- ⚠️ **`LICENSE` and `NOTICE` files are currently missing** — a hard blocker for publishing.
- The main (Workshop) package **contains no adult content**.
- Still needed: save-compat notes, dependency and load order, MCM setup guide, Workshop page copy and screenshots.

---

## 10. Remote and sync

- Repository: `https://github.com/Nobody-zz/AWAKE.git`
- Authoritative workspace: `D:\AWAKE-Dev` (the public GitHub mirror is **downstream**; sync only from this workspace)

```powershell
cd D:\AWAKE-Dev
# This working tree hosts several concurrent agents sharing one index.
# Do NOT stage the whole tree (`git add .`): commit only the paths you changed.
git commit -m "Update AWAKE runtime" -- AWAKE/src AWAKE/framework AWAKE/tools
git push origin main
```
