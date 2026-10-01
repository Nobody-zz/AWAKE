#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Build the persona authoring fact sheet.

Joins four authoritative sources into one TSV row per card, so that card
authoring never has to invent identity or personality:

  1. the card's own `.origins.json` sidecar   -> heroId          (W4 identity anchor)
  2. Modules/SandBox/ModuleData/lords.xml      -> native Bannerlord personality traits
                                                  (Valor / Honor / Mercy / Generosity /
                                                  Calculating / Egalitarian / Oligarchic /
                                                  Authoritarian), age, sex, voice, culture
  3. BannerlordSage bannerlord_heroes          -> father / mother / spouse
  4. docs/mappings/character-names-zh-en.tsv   -> the authoritative Chinese name (W3)
  5. docs/mappings/persona-game/persona-game-mapping.v1.json
                                               -> clanId / kingdomId / home settlement /
                                                  clan owner / is_noble_clan

The five native personality traits are the game's OWN per-character personality
data.  They are the correct basis for a card's tags -- not invented prose, and not
the AnimusForge `personality_background` JSON, which docs/mappings/persona-game/
persona-game-mapping.v1.json:6 explicitly forbids reading ("Read AF filenames only;
do not read personality_background JSON content.").

Read-only.  Writes one TSV.  stdout is ASCII only.
"""

import argparse
import io
import json
import os
import re
import sqlite3
import sys

DEFAULT_ROOT = r"D:\AWAKE-Dev\AWAKE"
DEFAULT_GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
DEFAULT_SAGE = (r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main"
                r"\dist\games\bannerlord\bannerlord.db")

PERSONALITY = ("Valor", "Honor", "Mercy", "Generosity", "Calculating")
POLITICAL = ("Egalitarian", "Oligarchic", "Authoritarian")
ALL_TRAITS = PERSONALITY + POLITICAL

COLUMNS = [
    "card_file", "hero_id", "name_zh", "name_en", "culture",
    "kingdom_id", "kingdom_zh", "clan_id", "clan_en", "clan_zh",
    "is_noble_clan", "clan_owner_hero_id", "clan_owner_zh",
    "home_settlement_id", "home_settlement_zh",
    "age", "is_female", "voice",
    "father", "father_zh", "mother", "mother_zh", "spouse", "spouse_zh",
    "personality_traits", "political_traits", "match_status",
]


def parse_lords(path):
    """heroId -> dict(name, culture, voice, female, age, traits)"""
    text = io.open(path, encoding="utf-8-sig").read()
    out = {}
    for block in re.findall(r"<NPCCharacter\b.*?</NPCCharacter>", text, re.S):
        m = re.search(r'\bid="([^"]+)"', block)
        if not m:
            continue

        def g(pat, default=""):
            mm = re.search(pat, block)
            return mm.group(1) if mm else default

        out[m.group(1)] = {
            "name": g(r'name="\{=[^}]+\}([^"]*)"'),
            "culture": g(r'culture="([^"]*)"'),
            "voice": g(r'voice="([^"]*)"'),
            "female": g(r'is_female="([^"]*)"'),
            "age": g(r'\bage="([^"]*)"'),
            "traits": dict(re.findall(
                r'<Trait\s+id="([^"]+)"\s+value="(-?\d+)"', block)),
        }
    return out


def strip_hero_prefix(value):
    """Sage stores `Hero.lord_1_40_1`; the bare id is what everything else uses."""
    if not value:
        return ""
    return value.split(".", 1)[1] if value.startswith("Hero.") else value


def read_names(path):
    """heroId -> (chinese, english) from the authoritative name table (W3)."""
    out = {}
    if not os.path.exists(path):
        return out
    with io.open(path, encoding="utf-8-sig") as handle:
        header = handle.readline().rstrip("\n").split("\t")
        try:
            i_id = header.index("hero_id")
            i_en = header.index("english")
            i_cn = header.index("chinese")
        except ValueError:
            return out
        for line in handle:
            parts = line.rstrip("\n").split("\t")
            if len(parts) > max(i_id, i_en, i_cn):
                out[parts[i_id]] = (parts[i_cn], parts[i_en])
    return out


def read_game_mapping(path):
    """heroId -> the anchor row (clan / kingdom / settlement / clan owner)."""
    out = {}
    if not os.path.exists(path):
        return out
    data = json.loads(io.open(path, encoding="utf-8").read())
    for row in data.get("rows", []):
        hid = row.get("hero_id")
        if hid:
            out[hid] = row
    return out


def read_sage_relations(path):
    """heroId -> (father, mother, spouse), stripped of the `Hero.` prefix."""
    out = {}
    if not os.path.exists(path):
        return out
    con = sqlite3.connect("file:" + path + "?mode=ro", uri=True)
    try:
        for hid, father, mother, spouse in con.execute(
                "SELECT heroId, father, mother, spouse FROM bannerlord_heroes"):
            out[hid] = (strip_hero_prefix(father or ""),
                        strip_hero_prefix(mother or ""),
                        strip_hero_prefix(spouse or ""))
    finally:
        con.close()
    return out


def read_code_table(path):
    """`code -> (english, chinese)` for the code-keyed mapping tables."""
    out = {}
    if not os.path.exists(path):
        return out
    with io.open(path, encoding="utf-8-sig") as handle:
        header = handle.readline().rstrip("\n").split("\t")
        try:
            i_code = header.index("code")
            i_en = header.index("english")
            i_cn = header.index("chinese")
        except ValueError:
            return out
        for line in handle:
            parts = line.rstrip("\n").split("\t")
            if len(parts) > max(i_code, i_en, i_cn):
                out[parts[i_code]] = (parts[i_en], parts[i_cn])
    return out


def read_kingdom_table(path):
    """`kingdom_id -> (english, chinese)`."""
    out = {}
    if not os.path.exists(path):
        return out
    with io.open(path, encoding="utf-8-sig") as handle:
        header = handle.readline().rstrip("\n").split("\t")
        try:
            i_id = header.index("kingdom_id")
            i_en = header.index("english")
            i_cn = header.index("chinese")
        except ValueError:
            return out
        for line in handle:
            parts = line.rstrip("\n").split("\t")
            if len(parts) > max(i_id, i_en, i_cn):
                out[parts[i_id]] = (parts[i_en], parts[i_cn])
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=DEFAULT_ROOT)
    ap.add_argument("--game", default=DEFAULT_GAME)
    ap.add_argument("--sage-db", default=DEFAULT_SAGE)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    cards_dir = os.path.join(args.root, "tools", "persona-workbench", "characters")
    lords_path = os.path.join(args.game, "Modules", "SandBox", "ModuleData", "lords.xml")
    names_path = os.path.join(args.root, "docs", "mappings", "character-names-zh-en.tsv")
    mapping_path = os.path.join(args.root, "docs", "mappings", "persona-game",
                                "persona-game-mapping.v1.json")

    lords = parse_lords(lords_path)
    names = read_names(names_path)
    anchors = read_game_mapping(mapping_path)
    relations = read_sage_relations(args.sage_db)
    clans = read_code_table(os.path.join(args.root, "docs", "mappings",
                                         "persona-names-zh-en.tsv"))
    kingdoms = read_kingdom_table(os.path.join(args.root, "docs", "mappings",
                                               "kingdom-names-zh-en.tsv"))
    settlements = read_code_table(os.path.join(args.root, "docs", "mappings",
                                               "settlement-names-zh-en.tsv"))

    def hero_zh(hid):
        return names.get(hid, ("", ""))[0] if hid else ""

    def code_zh(table, code):
        return table.get(code, ("", ""))[1] if code else ""

    rows = []
    skipped = []
    for name in sorted(os.listdir(cards_dir)):
        if not name.endswith(".persona.json"):
            continue
        sidecar = os.path.join(cards_dir, name[:-len(".persona.json")] + ".origins.json")
        hero_id = ""
        if os.path.exists(sidecar):
            try:
                hero_id = (json.loads(io.open(sidecar, encoding="utf-8").read())
                           .get("heroId") or "")
            except (ValueError, IOError):
                hero_id = ""
        if not hero_id:
            skipped.append((name, "no heroId in sidecar"))
            continue

        lord = lords.get(hero_id)
        if lord is None:
            skipped.append((name, "heroId %s not in lords.xml" % hero_id))
            continue

        anchor = anchors.get(hero_id, {})
        name_zh, name_en = names.get(hero_id, ("", lord["name"]))
        father, mother, spouse = relations.get(hero_id, ("", "", ""))
        traits = lord["traits"]

        kingdom_id = anchor.get("kingdom_id", "")
        clan_id = anchor.get("clan_id", "")
        owner_id = anchor.get("clan_owner_hero_id", "")
        settlement_id = anchor.get("home_settlement_id", "")
        clan_en, clan_zh = clans.get(clan_id, ("", ""))

        rows.append([
            name, hero_id, name_zh, name_en,
            (anchor.get("culture_id") or lord["culture"].replace("Culture.", "")),
            kingdom_id, code_zh(kingdoms, kingdom_id), clan_id, clan_en, clan_zh,
            "1" if anchor.get("is_noble_clan") else "0",
            owner_id, hero_zh(owner_id),
            settlement_id, code_zh(settlements, settlement_id),
            lord["age"], "1" if lord["female"].strip().lower() == "true" else "0",
            lord["voice"],
            father, hero_zh(father), mother, hero_zh(mother), spouse, hero_zh(spouse),
            ";".join("%s=%s" % (k, traits[k]) for k in PERSONALITY if k in traits),
            ";".join("%s=%s" % (k, traits[k]) for k in POLITICAL if k in traits),
            anchor.get("match_status", ""),
        ])

    with io.open(args.out, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\t".join(COLUMNS) + "\n")
        for row in rows:
            handle.write("\t".join(row) + "\n")

    with_traits = sum(1 for r in rows if r[COLUMNS.index("personality_traits")])
    with_family = sum(1 for r in rows
                      if r[COLUMNS.index("father")] or r[COLUMNS.index("mother")]
                      or r[COLUMNS.index("spouse")])
    with_zh = sum(1 for r in rows if r[COLUMNS.index("name_zh")])
    with_anchor = sum(1 for r in rows if r[COLUMNS.index("clan_id")])

    print("AUTHORING_FACTS cards=%d rows=%d skipped=%d" % (
        len(rows) + len(skipped), len(rows), len(skipped)))
    print("AUTHORING_FACTS with_native_personality=%d with_family=%d with_zh_name=%d "
          "with_clan_anchor=%d" % (with_traits, with_family, with_zh, with_anchor))
    for name, why in skipped[:20]:
        print("AUTHORING_FACTS skipped: %s (%s)" % (name.encode("ascii", "replace").decode(),
                                                    why))
    print("AUTHORING_FACTS out=%s" % args.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
