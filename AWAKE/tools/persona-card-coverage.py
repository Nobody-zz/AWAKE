#!/usr/bin/env python3
"""Persona card coverage measurement against the installed game data.

Why this exists
---------------
The character-card line has a coverage objective ("every lord that can hold a
conversation has a card").  That number must be measured against the game data
that is actually installed, not against a stale third-party snapshot: the
BannerlordSage index in this workspace was built from v1.3.15 and lists 397
heroes, while the local game is v1.4.8 and ships 390 ``lord_*`` ids.  Mixing the
two produces two different "missing" counts (52 vs 35), and both of them were
quoted at some point.  This tool is the single, reproducible source.

What it measures
----------------
1. Every ``lord_*`` id in ``lords.xml`` and whether a card's ``.origins.json``
   sidecar claims it.
2. The reverse direction: sidecars whose heroId is absent from ``lords.xml``
   (orphans) and duplicate heroIds across sidecars.
3. The identity anchor required by the authoring skill (W4): every card's
   ``sourceDescription`` must cite its own heroId.
4. For each missing hero: English name, official Chinese name from
   ``docs/mappings/character-names-zh-en.tsv``, sex, age, culture, clan,
   voice and native personality traits -- so the gap can be classified as
   "adult, writable" vs "minor / no official name, not writable".

Evidence level: E2 (offline measurement over installed game data).

Output: stdout is ASCII only; the full table is written to a UTF-8 file.
"""

from __future__ import annotations

import argparse
import io
import json
import os
import re
import sys

DEFAULT_LORDS = (
    r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\SandBox\ModuleData\lords.xml"
)
DEFAULT_CARDS = r"D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters"
DEFAULT_NAMES = r"D:\AWAKE-Dev\AWAKE\docs\mappings\character-names-zh-en.tsv"

PERSONALITY_TRAITS = ("Valor", "Honor", "Mercy", "Generosity", "Calculating")

BLOCK_RE = re.compile(r"<NPCCharacter\b(.*?)(?:/>|</NPCCharacter>)", re.S)
HERO_RE = re.compile(r"<Hero\b(.*?)(?:/>|</Hero>)", re.S)
ID_RE = re.compile(r'\bid="(lord_[A-Za-z0-9_]+)"')
CLAN_RE = re.compile(r"\b(clan_[a-z0-9_]+)\b")
HERO_REF_RE = re.compile(r"\bHero\.(lord_[A-Za-z0-9_]+)")
TRAIT_RE = re.compile(r'id="([A-Za-z]+)"\s+value="(-?\d+)"')
LOCNAME_RE = re.compile(r'<Name\s+name="\{=[^}]+\}([^"]+)"')
ANCHOR_RE = re.compile(r"^(对外|私下里|私下|明面上|人前|人后)[，,：:]?")


def attr(block: str, key: str) -> str:
    m = re.search(r'\b%s="([^"]*)"' % key, block)
    return m.group(1) if m else ""


def load_lords(path: str) -> dict:
    raw = io.open(path, encoding="utf-8-sig", errors="replace").read()
    out = {}
    for block in BLOCK_RE.findall(raw):
        m = ID_RE.search(block)
        if not m:
            continue
        hid = m.group(1)
        localised = LOCNAME_RE.search(block)
        traits = {k: int(v) for k, v in TRAIT_RE.findall(block)}
        clans = CLAN_RE.findall(block)
        out[hid] = {
            "heroId": hid,
            "nameAttr": attr(block, "name"),
            "nameLocalised": localised.group(1) if localised else "",
            "female": str(attr(block, "is_female")).strip().lower() == "true",
            "age": attr(block, "age"),
            "culture": attr(block, "culture"),
            "voice": attr(block, "voice"),
            "occupation": attr(block, "occupation"),
            "clans": clans,
            "traits": traits,
        }
    return out


def load_names(path: str) -> dict:
    out = {}
    if not os.path.exists(path):
        return out
    lines = io.open(path, encoding="utf-8-sig").read().splitlines()
    for line in lines[1:]:
        cols = line.split("\t")
        if len(cols) >= 3:
            out[cols[0]] = (cols[1], cols[2])
    return out


def load_heroes(path: str) -> dict:
    """Clan / parentage for heroes whose ``lords.xml`` block omits them.

    ``lords.xml`` only carries an inline ``<Hero ... faction=...>`` for some
    heroes (mostly the ones with a hand-written block).  Children of lords get
    their clan and parents from ``SandBox/ModuleData/heroes.xml`` instead, so
    without this second source every minor would show an empty clan.
    """
    out = {}
    if not os.path.exists(path):
        return out
    raw = io.open(path, encoding="utf-8-sig", errors="replace").read()
    for block in HERO_RE.findall(raw):
        m = ID_RE.search(block)
        if not m:
            continue
        clans = CLAN_RE.findall(block)

        def ref(key: str) -> str:
            r = HERO_REF_RE.search(attr(block, key))
            return r.group(1) if r else ""

        out[m.group(1)] = {
            "clan": clans[0] if clans else "",
            "father": ref("father"),
            "mother": ref("mother"),
            "spouse": ref("spouse"),
        }
    return out


def load_cards(cards_dir: str) -> dict:
    """Map heroId -> list of card basenames claiming it, from .origins.json."""
    claims = {}
    for name in sorted(os.listdir(cards_dir)):
        if not name.endswith(".origins.json"):
            continue
        path = os.path.join(cards_dir, name)
        try:
            data = json.load(io.open(path, encoding="utf-8-sig"))
        except Exception as exc:  # noqa: BLE001 - report, never crash the survey
            claims.setdefault("<unparsable>", []).append("%s (%s)" % (name, exc))
            continue
        hid = data.get("heroId")
        claims.setdefault(hid, []).append(name[: -len(".origins.json")])
    return claims


def read_persona(cards_dir: str, stem: str) -> dict | None:
    path = os.path.join(cards_dir, stem + ".persona.json")
    if not os.path.exists(path):
        return None
    try:
        return json.load(io.open(path, encoding="utf-8-sig"))
    except Exception:  # noqa: BLE001
        return None


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--lords", default=DEFAULT_LORDS)
    ap.add_argument("--heroes", default="",
                    help="defaults to heroes.xml beside --lords")
    ap.add_argument("--cards", default=DEFAULT_CARDS)
    ap.add_argument("--names", default=DEFAULT_NAMES)
    ap.add_argument("--report", default="")
    ap.add_argument("--adult-age", type=int, default=18,
                    help="heroes at or above this age are treated as writable.")
    args = ap.parse_args()

    if not os.path.exists(args.lords):
        sys.stderr.write("lords.xml not found: %s\n" % args.lords)
        return 2
    if not os.path.isdir(args.cards):
        sys.stderr.write("cards dir not found: %s\n" % args.cards)
        return 2

    lords = load_lords(args.lords)
    heroes_path = args.heroes or os.path.join(os.path.dirname(args.lords),
                                              "heroes.xml")
    heroes = load_heroes(heroes_path)
    names = load_names(args.names)
    claims = load_cards(args.cards)

    sidecar_ids = set(k for k in claims if k and not k.startswith("<"))
    covered = set(lords) & sidecar_ids
    missing = sorted(set(lords) - sidecar_ids)
    orphans = sorted(sidecar_ids - set(lords))
    duplicates = sorted(k for k, v in claims.items()
                        if k and not k.startswith("<") and len(v) > 1)

    # W4 identity anchor: sourceDescription must cite the card's own heroId.
    anchor_ok = 0
    anchor_bad = []
    for hid in sorted(covered):
        for stem in claims[hid]:
            card = read_persona(args.cards, stem)
            if card is None:
                anchor_bad.append((hid, stem, "persona.json unreadable"))
                continue
            src = str(card.get("sourceDescription", ""))
            if hid in src:
                anchor_ok += 1
            else:
                anchor_bad.append((hid, stem, "sourceDescription omits " + hid))

    total = len(lords)
    pct = (100.0 * len(covered) / total) if total else 0.0

    out = []
    out.append("# Persona card coverage vs installed game data")
    out.append("")
    out.append("lords_xml      = %s" % args.lords)
    out.append("heroes_xml     = %s" % heroes_path)
    out.append("cards_dir      = %s" % args.cards)
    out.append("name_table     = %s" % args.names)
    out.append("")
    out.append("lord_ids       = %d" % total)
    out.append("cards          = %d" % len(sidecar_ids))
    out.append("covered        = %d" % len(covered))
    out.append("missing        = %d" % len(missing))
    out.append("orphan_cards   = %d" % len(orphans))
    out.append("duplicate_ids  = %d" % len(duplicates))
    out.append("coverage_pct   = %.1f" % pct)
    out.append("anchor_ok      = %d" % anchor_ok)
    out.append("anchor_bad     = %d" % len(anchor_bad))
    out.append("")

    adults, minors, nameless = [], [], []
    for hid in missing:
        info = lords[hid]
        age = int(info["age"]) if str(info["age"]).isdigit() else -1
        en, zh = names.get(hid, (info["nameLocalised"] or info["nameAttr"], ""))
        if not zh and not info["nameLocalised"]:
            nameless.append(hid)
        (adults if age >= args.adult_age else minors).append(hid)

    out.append("missing_adults = %d" % len(adults))
    out.append("missing_minors = %d" % len(minors))
    out.append("missing_without_official_name = %d" % len(nameless))
    out.append("")
    out.append("## Missing heroes")
    out.append("")
    out.append("heroId\tname_en\tname_zh\tsex\tage\tculture\tclan\tfather\t"
               "mother\tvoice\tofficial_name\tnative_personality_traits")
    for hid in missing:
        info = lords[hid]
        kin = heroes.get(hid, {})
        en, zh = names.get(hid, (info["nameLocalised"] or info["nameAttr"], ""))
        official = "yes" if (zh or info["nameLocalised"]) else "NO"
        traits = ";".join("%s=%d" % (k, info["traits"][k])
                          for k in PERSONALITY_TRAITS if k in info["traits"])
        out.append("\t".join([
            hid, en or info["nameAttr"], zh, "F" if info["female"] else "M",
            info["age"], info["culture"],
            kin.get("clan") or (info["clans"][0] if info["clans"] else ""),
            kin.get("father", ""), kin.get("mother", ""),
            info["voice"], official, traits or "-",
        ]))

    if orphans:
        out.append("")
        out.append("## Orphan cards (heroId absent from lords.xml)")
        for hid in orphans:
            out.append("%s\t%s" % (hid, ", ".join(claims[hid])))

    if duplicates:
        out.append("")
        out.append("## Duplicate heroId claims")
        for hid in duplicates:
            out.append("%s\t%s" % (hid, ", ".join(claims[hid])))

    if anchor_bad:
        out.append("")
        out.append("## Cards missing the W4 identity anchor")
        for hid, stem, why in anchor_bad:
            out.append("%s\t%s\t%s" % (hid, stem, why))

    text = "\n".join(out) + "\n"

    if args.report:
        io.open(args.report, "w", encoding="utf-8", newline="\n").write(text)

    # ASCII-only stdout summary; the Chinese table goes to the report file.
    print("PERSONA_COVERAGE lords=%d cards=%d covered=%d missing=%d "
          "orphans=%d duplicates=%d coverage=%.1f%% adults_missing=%d "
          "minors_missing=%d nameless_missing=%d anchor_ok=%d anchor_bad=%d"
          % (total, len(sidecar_ids), len(covered), len(missing), len(orphans),
             len(duplicates), pct, len(adults), len(minors), len(nameless),
             anchor_ok, len(anchor_bad)))
    if args.report:
        print("PERSONA_COVERAGE_REPORT %s" % os.path.abspath(args.report))
    return 0


if __name__ == "__main__":
    sys.exit(main())
