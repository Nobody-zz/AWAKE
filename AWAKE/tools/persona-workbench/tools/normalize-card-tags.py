#!/usr/bin/env python3
"""normalize-card-tags.py -- migrate unregistered persona tags onto the closed 39-tag registry.

WHY THIS EXISTS
    Between 2026-09-24 and 2026-09-25 a generator batch wrote 14 tag ids that are not in
    ModuleData/Worldbook/persona_definitions/tag_registry.json.  A single unregistered tag makes
    PersonaTagRegistry.TryExpand() return false, which makes PersonaDslGenerator fall back to the
    legacy template -- the card loses its entire personality DSL.  275 of 355 cards are affected.

    The project's stated position (skill persona-authoring; compile-verify.ps1, which was
    deliberately changed to fail on unregistered tags) is that the 39 tags are CLOSED.  So the
    cards are normalized onto the vocabulary; the registry is NOT extended (extending it would
    also invalidate targetRegistry.sha256 in docs/persona-contract/persona-workbench-to-awake.crosswalk.v1.json).

    Every mapping below is a judgement call and is recorded in
    AWAKE/docs/PERSONA-TAG-NORMALIZATION-20261001.md.  The one DROP is flagged.

WHAT IT TOUCHES
    Only "tags" and "facetStrengths" on each card.  Nothing else is rewritten; the file is
    re-serialized with json.dumps(indent=4, ensure_ascii=False) which was measured byte-identical
    to the existing formatting on 259/355 cards (the other 96 differ only by a trailing newline,
    which is preserved).

USAGE
    py -3 normalize-card-tags.py --cards <dir> [--dry-run] [--report <file>] [--backup <dir>]
    py -3 normalize-card-tags.py --emit-map
"""

import argparse
import glob
import io
import json
import os
import shutil
import sys

# ---------------------------------------------------------------- alias map
# unregistered id -> registered id, or None to drop the tag.
TAG_ALIASES = {
    # "cold ruthless calculator" archetype (235 cards)
    "trait.ruthless": "trait.deceitful",
    "expression.cold": "expression.understated",
    "behavior.calculating": "behavior.keeps_leverage",
    "trigger.family_interest": "trigger.family_safety",
    # "gentle protector" archetype (31 cards)
    "trait.warm": "trait.kind",
    "trait.compassionate": "trait.kind",  # collides with trait.warm -> dedupe
    "expression.soft": "expression.warm",
    "trigger.civilian_safety": "trigger.threat_to_home",
    # "tradition-bound" archetype (5 cards)
    "expression.reserved": "expression.understated",
    "behavior.rule_bound": None,  # meaning already carried by trait.traditional in the same set
    "trigger.tradition": "trigger.social_slight",  # weakest mapping in this table
    # "loyal ally" archetype (4 cards)
    "trait.generous": "trait.kind",
    "behavior.loyal": "trait.loyal",
    "trigger.ally_interest": "trigger.loyalty_or_betrayal",
}

SCHEMA_REL = os.path.join(
    "tools", "persona-workbench", "contracts", "persona-workbench.character.v1.schema.json"
)


def repo_root(start):
    d = os.path.abspath(start)
    while True:
        if os.path.isdir(os.path.join(d, "AWAKE")) and os.path.isdir(os.path.join(d, ".git")):
            return d
        parent = os.path.dirname(d)
        if parent == d:
            return None
        d = parent


def facet_enum(root):
    """Read the legal facetStrengths keys straight out of the schema (no hardcoded list)."""
    path = os.path.join(root, "AWAKE", SCHEMA_REL)
    with io.open(path, encoding="utf-8") as fh:
        schema = json.load(fh)
    node = schema["properties"]["facetStrengths"]["propertyNames"]
    return list(node["enum"])


def read_card(path):
    with io.open(path, encoding="utf-8") as fh:
        return fh.read()


def map_tags(tags):
    """Map, drop, and de-duplicate a tag list, preserving first-seen order."""
    out, dropped, collisions = [], [], []
    for tag in tags:
        target = TAG_ALIASES.get(tag, tag)
        if target is None:
            dropped.append(tag)
            continue
        if target in out:
            collisions.append((tag, target))
            continue
        out.append(target)
    return out, dropped, collisions


def map_facets(facets, legal):
    """Rename facet keys that are legal, drop facet keys that map to a non-facet target."""
    out, dropped, collisions = {}, [], []
    for key, value in facets.items():
        target = TAG_ALIASES.get(key, key)
        if target is None or target not in legal:
            dropped.append((key, target))
            continue
        if target in out:
            collisions.append((key, target, out[target], value))
            out[target] = max(out[target], value)
        else:
            out[target] = value
    return out, dropped, collisions


def main():
    ap = argparse.ArgumentParser(description="Normalize unregistered persona tags onto the registry.")
    ap.add_argument("--cards", help="directory holding *.persona.json")
    ap.add_argument("--dry-run", action="store_true", help="report only; write nothing")
    ap.add_argument("--report", help="write the report here (UTF-8)")
    ap.add_argument("--backup", help="copy each changed card here before writing")
    ap.add_argument("--emit-map", action="store_true", help="print the alias map as JSON and exit")
    args = ap.parse_args()

    if args.emit_map:
        sys.stdout.write(json.dumps(TAG_ALIASES, indent=2, ensure_ascii=False, sort_keys=True) + "\n")
        return 0

    if not args.cards:
        sys.stderr.write("ERROR: --cards is required\n")
        return 2

    root = repo_root(args.cards) or repo_root(os.getcwd())
    if root is None:
        sys.stderr.write("ERROR: cannot locate repo root (AWAKE/ + .git/)\n")
        return 2
    legal = facet_enum(root)

    paths = sorted(glob.glob(os.path.join(os.path.abspath(args.cards), "*.persona.json")))
    if not paths:
        sys.stderr.write("ERROR: no *.persona.json under %s\n" % args.cards)
        return 2

    lines = []
    total = changed = errors = 0
    tag_hits, facet_hits = {}, {}
    dropped_all, collide_all = {}, {}
    fdropped_all, fmerged_all = {}, {}

    for path in paths:
        total += 1
        name = os.path.basename(path)
        try:
            raw = read_card(path)
            card = json.loads(raw)
        except Exception as exc:  # noqa: BLE001 - report and continue, never abort the corpus
            errors += 1
            lines.append("ERROR %s :: %s" % (name, exc))
            continue

        old_tags = card.get("tags")
        old_facets = card.get("facetStrengths")
        new_tags = old_tags
        new_facets = old_facets
        dropped_tags, collide_tags = [], []
        dropped_facets, collide_facets = [], []

        if isinstance(old_tags, list):
            new_tags, dropped_tags, collide_tags = map_tags(old_tags)
        if isinstance(old_facets, dict):
            new_facets, dropped_facets, collide_facets = map_facets(old_facets, legal)

        if new_tags == old_tags and new_facets == old_facets:
            continue

        changed += 1
        for tag in old_tags or []:
            if tag in TAG_ALIASES:
                tag_hits[tag] = tag_hits.get(tag, 0) + 1
        for key in (old_facets or {}):
            if key in TAG_ALIASES:
                facet_hits[key] = facet_hits.get(key, 0) + 1
        for tag in dropped_tags:
            dropped_all[tag] = dropped_all.get(tag, 0) + 1
        for tag, target in collide_tags:
            k = "%s->%s" % (tag, target)
            collide_all[k] = collide_all.get(k, 0) + 1
        for key, target in dropped_facets:
            k = "%s(->%s)" % (key, target)
            fdropped_all[k] = fdropped_all.get(k, 0) + 1
        for key, target, keep, val in collide_facets:
            k = "%s->%s" % (key, target)
            fmerged_all[k] = fmerged_all.get(k, 0) + 1

        lines.append("CARD %s" % name)
        lines.append("  tags   - %s" % ",".join(old_tags or []))
        lines.append("  tags   + %s" % ",".join(new_tags or []))
        if isinstance(old_facets, dict):
            lines.append("  facet  - %s" % ",".join(old_facets.keys()))
            lines.append("  facet  + %s" % ",".join((new_facets or {}).keys()))
        for tag in dropped_tags:
            lines.append("  drop   tag %s" % tag)
        for tag, target in collide_tags:
            lines.append("  dedupe tag %s -> %s" % (tag, target))
        for key, target in dropped_facets:
            lines.append("  drop   facet %s (target %s)" % (key, target))
        for key, target, keep, val in collide_facets:
            lines.append("  merge  facet %s -> %s (keep max %d of %d/%d)" % (key, target, max(keep, val), keep, val))

        if args.dry_run:
            continue

        if args.backup:
            if not os.path.isdir(args.backup):
                os.makedirs(args.backup)
            shutil.copyfile(path, os.path.join(args.backup, name))

        card["tags"] = new_tags
        if isinstance(old_facets, dict):
            card["facetStrengths"] = new_facets
        text = json.dumps(card, indent=4, ensure_ascii=False)
        if raw.endswith("\n"):
            text += "\n"
        with io.open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(text)

    lines.append("")
    lines.append("SUMMARY cards=%d changed=%d unchanged=%d errors=%d dry_run=%s"
                 % (total, changed, total - changed - errors, errors, bool(args.dry_run)))
    lines.append("ALIAS_HITS_IN_TAGS")
    for tag in sorted(tag_hits):
        lines.append("  %-28s %d" % (tag, tag_hits[tag]))
    lines.append("ALIAS_HITS_IN_FACETSTRENGTHS")
    for tag in sorted(facet_hits):
        lines.append("  %-28s %d" % (tag, facet_hits[tag]))
    lines.append("DROPPED_TAGS")
    for tag in sorted(dropped_all):
        lines.append("  %-28s %d" % (tag, dropped_all[tag]))
    lines.append("DEDUPED_TAGS")
    for tag in sorted(collide_all):
        lines.append("  %-28s %d" % (tag, collide_all[tag]))
    lines.append("DROPPED_FACET_KEYS")
    for key in sorted(fdropped_all):
        lines.append("  %-40s %d" % (key, fdropped_all[key]))
    lines.append("MERGED_FACET_KEYS")
    for key in sorted(fmerged_all):
        lines.append("  %-40s %d" % (key, fmerged_all[key]))

    text = "\n".join(lines) + "\n"
    if args.report:
        with io.open(args.report, "w", encoding="utf-8", newline="") as fh:
            fh.write(text)
        sys.stdout.write("REPORT %s\n" % args.report)
    sys.stdout.write("SUMMARY cards=%d changed=%d errors=%d dry_run=%s\n"
                     % (total, changed, errors, bool(args.dry_run)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
