# -*- coding: utf-8 -*-
"""AWAKE persona-card quality gate.

Checks a directory of persona-workbench character cards and exits non-zero on
any violation. Thresholds are CALIBRATED against the 2026-10-01 measured
distribution (76 human-reviewed cards vs 279 bulk-generated cards), never
guessed -- see AWAKE/docs/PERSONA-*.md.

Rules
  R1 core-too-short      core must be >= --min-core-len characters
                         (good min 187 / bad max 129 => 150 is provably safe)
  R2 core-unterminated   core must end with terminal punctuation
                         (good 0% / bad 94% violate)
  R3 template-load       a card's boundary-field sentences may not be shared
                         with >= --share-threshold other cards, above
                         --max-template-load of its sentences
                         (good 0.000 / bad 0.600)
  R4 tag-stamp           the largest identical tag set may not exceed
                         --max-tag-share of the population
                         (good max 5.3% / bad 84%)
  R5 kin-not-a-name      every entry after the identityFacts kin marker must be
                         a bare name once its role word is stripped
                         (good: field absent / bad: 117 of 409 entries fail)
  R6 id-pattern          id must match the authoring schema pattern
                         ^calradia\\.[a-z0-9_]+(\\.[a-z0-9_]+){1,2}$
                         (good 0/76 / bad 279/279 -- capitalised name segment)
  R7 tag-unregistered    every tag id must exist in tag_registry.json
                         (good 0/76 / bad 276/279)
                         NOT cosmetic: PersonaDslGenerator.cs:123 TryExpand fails
                         -> :127 -> :131 UsedLegacyFallback = true -> the card
                         never uses its own persona DSL at runtime.
  R8 field-shape         when PRESENT, realSelfBehaviors must be an array.
                         The key is optional: 8 of the 76 good cards omit it,
                         so absence passes and only a wrong shape fails.
                         NON-DISCRIMINATING on the 2026-10-01 sample (0/355
                         cards violate it). Kept as an invariant and proven to
                         fire by mutation -- it is not carrying the gate.
                         NOTE the real defect is on the DEFINITION side: those
                         same 8 characters' .definition.json contain
                         realSelfBehaviors = {} because the materializer emits
                         an empty object when the source card omits the field.
  R9 tag-count-stamp     the per-card tag count must have >=
                         --min-tag-count-values distinct values and no single
                         value above --max-tag-count-share
                         (good 8 values, max 38% / bad 1 value, 100%)
  R10 cross-card-duplicate
                         no boundary entry (tensionAxes.* / selfClaimRules[] /
                         realSelfBehaviors[]) may appear in more than
                         --max-cross-card-support cards
                         (good max 2 / bad 259 of 279)
  R11 summary-copied-into-example
                         a card's whole summary may not be a substring of one
                         of its own selfClaimExamples[]
                         (good 0/76 / bad 262/279)
  R12 speaker-label-prefix
                         no selfClaimExamples[] entry may start with
                         "<displayName>\uff08...\u5bb6\uff09"
                         (good 0/76 / bad 262/279)

R10-R12 DELEGATE: the rule logic lives in exactly one place -- the external
criteria script (default persona-workbench/tools/measure-three-criteria.py,
override with --criteria-script). This gate only applies thresholds, so the two
implementations cannot drift apart. Their cross-card counts are always computed
over the WHOLE card directory, never over the --include-list subset: counting
inside a subset collapses every support value to 1 and the criterion could
never fire.
CALIBRATION NOTE (2026-10-01 corpus): R10/R11/R12 flag exactly the SAME 262 of
279 bad cards, so they are three shapes of one defect, not three independent
signals. The 17 they miss are all empire_* cards and are already caught by
R1/R2/R6/R7.
If the criteria script cannot be loaded the gate FAILS loudly
(PERSONA_CRITERIA_UNAVAILABLE) instead of silently losing three criteria; pass
--skip-external-criteria to opt out deliberately.

Usage
  py -3 AWAKE/tools/persona-card-gate.py --cards <dir>
  py -3 AWAKE/tools/persona-card-gate.py --cards <dir> --include-list <file>
  py -3 AWAKE/tools/persona-card-gate.py --cards <dir> --exclude-list <file>
  py -3 AWAKE/tools/persona-card-gate.py --cards <dir> --tag-registry <file>

Exit codes
  0  PERSONA_GATE_GREEN
  1  PERSONA_GATE_RED
  2  usage / IO error

stdout is deliberately ASCII-only (Windows consoles here decode as GBK and
mangle CJK). Pass --report <file> to get the full UTF-8 detail and read it
with the file reader.
"""
from __future__ import print_function

import argparse
import glob
import io
import json
import os
import re
import sys
from collections import Counter, defaultdict

TERMINAL = u"。！？…\u201d\u300d\uff09)"          # 。！？…”」）)
FUNCTION_CHARS = set(u"的是在以与和对把被从为而则将使让给向往于之其也就并且或及")
ROLE_WORDS = re.compile(
    u"^(父亲|母亲|父|母|族长|丈夫|妻子|配偶|女儿|儿子|子女|家眷|兄长|弟弟|姐姐|妹妹|族中|亲族部属)")
KIN_MARKER = re.compile(u"已知亲属[：:](.+?)(?:。|$)")
SENT_SPLIT = re.compile(u"[。！？；\\n]")
BOUNDARY_FIELDS = ("selfClaimRules", "realSelfBehaviors")
ID_PATTERN = re.compile(r"^calradia\.[a-z0-9_]+(\.[a-z0-9_]+){1,2}$")
DEFAULT_TAG_REGISTRY = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)), os.pardir,
    "ModuleData", "Worldbook", "persona_definitions", "tag_registry.json"))
DEFAULT_CRITERIA_SCRIPT = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "persona-workbench", "tools",
    "measure-three-criteria.py"))

REPORT = []


def emit(line):
    """stdout stays ASCII; the raw line goes to the optional report file."""
    REPORT.append(line)
    print(line.encode("ascii", "replace").decode("ascii"))


def load_list(path):
    out = set()
    if not path:
        return out
    with io.open(path, encoding="utf-8-sig") as fh:
        for line in fh:
            line = line.strip()
            if line:
                out.add(os.path.normpath(os.path.abspath(line)))
    return out


def norm(p):
    return os.path.normpath(p)


def sentences(texts):
    out = []
    for t in texts:
        for s in SENT_SPLIT.split(t or u""):
            s = s.strip()
            if len(s) >= 4:
                out.append(s)
    return out


def tag_ids(data):
    """Card tags are plain strings; tolerate the definition-side dict form."""
    out = []
    for x in (data.get("tags") or []):
        if isinstance(x, str):
            out.append(x)
        elif isinstance(x, dict):
            v = x.get("id") or x.get("tag")
            if isinstance(v, str):
                out.append(v)
    return [i for i in out if i]


def load_registry(path):
    """Return the set of registered tag ids, or None when unreadable.

    Only registry["tags"][*]["id"] counts. Bundle ids are NOT tags: a bundle id
    in a card's tags list must still fail R7.
    """
    try:
        with io.open(path, encoding="utf-8") as fh:
            raw = json.load(fh)
    except Exception:
        return None
    tags = raw.get("tags") if isinstance(raw, dict) else raw
    out = set()
    if isinstance(tags, list):
        for t in tags:
            if isinstance(t, dict) and isinstance(t.get("id"), str):
                out.add(t["id"])
            elif isinstance(t, str):
                out.add(t)
    return out


def load_criteria_module(path):
    """Import the external criteria script as a module.

    Never runs its main() (that entry point re-asserts a frozen 76/279
    calibration population and would refuse to run on any other corpus).
    Returns None when the file is missing, unimportable, or does not expose the
    two functions this gate depends on.
    """
    if not path or not os.path.isfile(path):
        return None
    try:
        import importlib.util
        spec = importlib.util.spec_from_file_location("awake_external_criteria", path)
        if spec is None or spec.loader is None:
            return None
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
    except Exception:
        return None
    for attr in ("units", "measure"):
        if not hasattr(mod, attr):
            return None
    return mod


def read_cards(cards_dir, include, exclude):
    cards = []
    for p in sorted(glob.glob(os.path.join(cards_dir, "*.persona.json"))):
        key = norm(p)
        if include and key not in include:
            continue
        if exclude and key in exclude:
            continue
        try:
            with io.open(p, encoding="utf-8") as fh:
                data = json.load(fh)
        except Exception as exc:                       # malformed card is a failure
            cards.append(dict(path=p, name=os.path.basename(p), error=str(exc), data={}))
            continue
        cards.append(dict(path=p, name=os.path.basename(p), error=None, data=data))
    return cards


def main():
    ap = argparse.ArgumentParser(add_help=True)
    ap.add_argument("--cards", required=True)
    ap.add_argument("--min-core-len", type=int, default=150)
    ap.add_argument("--max-template-load", type=float, default=0.05)
    ap.add_argument("--share-threshold", type=int, default=5)
    ap.add_argument("--max-tag-share", type=float, default=0.10)
    ap.add_argument("--min-population", type=int, default=20)
    ap.add_argument("--tag-registry", default=DEFAULT_TAG_REGISTRY)
    ap.add_argument("--min-tag-count-values", type=int, default=4)
    ap.add_argument("--max-tag-count-share", type=float, default=0.50)
    ap.add_argument("--include-list", default=None)
    ap.add_argument("--exclude-list", default=None)
    ap.add_argument("--criteria-script", default=DEFAULT_CRITERIA_SCRIPT)
    ap.add_argument("--skip-external-criteria", action="store_true")
    ap.add_argument("--max-cross-card-support", type=int, default=2)
    ap.add_argument("--max-summary-copies", type=int, default=0)
    ap.add_argument("--max-speaker-prefix", type=int, default=0)
    ap.add_argument("--max-detail", type=int, default=25)
    ap.add_argument("--report", default=None)
    ap.add_argument("--label", default="")
    args = ap.parse_args()

    if not os.path.isdir(args.cards):
        emit("PERSONA_GATE_ERROR cards directory not found: " + args.cards)
        return 2

    cards = read_cards(args.cards, load_list(args.include_list), load_list(args.exclude_list))
    n = len(cards)
    emit("PERSONA_GATE_START label=%s cards=%d dir=%s" % (args.label, n, args.cards))
    if n == 0:
        emit("PERSONA_GATE_ERROR no cards matched")
        return 2

    failures = []          # (rule, detail-line)

    def fail(rule, line):
        failures.append((rule, line))

    # ---------- R1 / R2 / R5 / R6 / R8 : per card ----------
    for c in cards:
        if c["error"]:
            fail("PERSONA_CARD_UNREADABLE", "%s :: %s" % (c["name"], c["error"]))
            continue
        d = c["data"]
        core = d.get("core") or u""

        # R1
        if len(core) < args.min_core_len:
            fail("PERSONA_CORE_TOO_SHORT",
                 "%s :: len=%d min=%d" % (c["name"], len(core), args.min_core_len))

        # R2
        stripped = core.strip()
        if stripped and stripped[-1] not in TERMINAL:
            fail("PERSONA_CORE_UNTERMINATED",
                 "%s :: tail=%s" % (c["name"], repr(stripped[-6:])))

        # R5
        ident = d.get("identityFacts") or u""
        m = KIN_MARKER.search(ident)
        if m:
            for entry in re.split(u"[、,，]", m.group(1)):
                entry = entry.strip()
                if not entry:
                    continue
                rest = ROLE_WORDS.sub(u"", entry).strip()
                bad = (not rest) or (len(rest) > 5) or any(ch in FUNCTION_CHARS for ch in rest)
                if bad:
                    fail("PERSONA_KIN_NOT_A_NAME",
                         "%s :: entry=%s" % (c["name"], entry))

        # R6
        cid = d.get("id")
        if not isinstance(cid, str) or not ID_PATTERN.match(cid):
            fail("PERSONA_ID_PATTERN",
                 "%s :: id=%r" % (c["name"], cid))

        # R8 -- the card schema does NOT require this key (8 of the 76 good
        # cards omit it), so absence is legal; only a wrong shape fails.
        if "realSelfBehaviors" in d and not isinstance(d["realSelfBehaviors"], list):
            fail("PERSONA_FIELD_SHAPE",
                 "%s :: realSelfBehaviors is %s, expected array" % (
                     c["name"], type(d["realSelfBehaviors"]).__name__))

    # ---------- R3 : cross-card template load ----------
    sent_cards = defaultdict(set)
    for c in cards:
        if c["error"]:
            continue
        txt0 = []
        for f in BOUNDARY_FIELDS:
            txt0 += c["data"].get(f) or []
        for s in set(sentences(txt0)):
            sent_cards[s].add(c["name"])
    if n >= args.share_threshold + 1:
        for c in cards:
            if c["error"]:
                continue
            txt = []
            for f in BOUNDARY_FIELDS:
                txt += c["data"].get(f) or []
            mine = list(set(sentences(txt)))
            if not mine:
                continue
            shared = [s for s in mine if len(sent_cards[s]) >= args.share_threshold]
            load = len(shared) / float(len(mine))
            if load > args.max_template_load:
                fail("PERSONA_TEMPLATE_LOAD",
                     "%s :: %.2f (%d/%d) e.g. %s" % (
                         c["name"], load, len(shared), len(mine), shared[0][:30]))

    # ---------- R4 : tag stamp ----------
    if n >= args.min_population:
        tag_groups = Counter(tuple(sorted(tag_ids(c["data"])))
                             for c in cards if not c["error"])
        if tag_groups:
            tags, cnt = tag_groups.most_common(1)[0]
            share = cnt / float(n)
            if share > args.max_tag_share:
                fail("PERSONA_TAG_STAMP",
                     "largest tag set %d/%d (%.0f%%) tags=%s" % (
                         cnt, n, share * 100, ",".join(tags)))
    else:
        emit("PERSONA_GATE_NOTE population=%d < %d : R3/R4/R9 skipped" % (n, args.min_population))

    # ---------- R7 : unregistered tags (hard, population-independent) ----------
    registry = load_registry(args.tag_registry) if args.tag_registry else None
    if registry is None:
        emit("PERSONA_GATE_NOTE tag registry unreadable at %s : R7 skipped" % args.tag_registry)
    else:
        emit("PERSONA_GATE_REGISTRY tags=%d path=%s" % (len(registry), args.tag_registry))
        for c in cards:
            if c["error"]:
                continue
            miss = [t for t in tag_ids(c["data"]) if t not in registry]
            if miss:
                fail("PERSONA_TAG_UNREGISTERED",
                     "%s :: %s" % (c["name"], ",".join(miss)))

    # ---------- R9 : tag-count stamp ----------
    if n >= args.min_population:
        shape = Counter(len(tag_ids(c["data"])) for c in cards if not c["error"])
        if shape:
            distinct = len(shape)
            val, cnt = shape.most_common(1)[0]
            share = cnt / float(n)
            if distinct < args.min_tag_count_values or share > args.max_tag_count_share:
                fail("PERSONA_TAG_COUNT_STAMP",
                     "distinct=%d (min=%d) largest=%d tag(s) x%d (%.0f%% max=%.0f%%)" % (
                         distinct, args.min_tag_count_values, val, cnt,
                         share * 100, args.max_tag_count_share * 100))

    # ---------- R10 / R11 / R12 : delegated to the external criteria script --
    # The rule logic lives in exactly one place (measure-three-criteria.py);
    # this gate only applies thresholds. Cross-card counts MUST come from the
    # whole corpus: computing them inside the --include-list subset would
    # collapse every support value to 1 and the criterion could never fire.
    if args.skip_external_criteria:
        emit("PERSONA_GATE_NOTE external criteria deliberately skipped (--skip-external-criteria)")
    else:
        mod = load_criteria_module(args.criteria_script)
        if mod is None:
            fail("PERSONA_CRITERIA_UNAVAILABLE",
                 "cannot load criteria script at %s" % args.criteria_script)
        else:
            corpus = read_cards(args.cards, set(), set())
            pairs, by_key = [], {}
            for c in corpus:
                if c["error"]:
                    continue
                key = norm(os.path.abspath(c["path"]))
                pairs.append((key, c["data"]))
                by_key[key] = c["name"]
            readings = None
            try:
                readings = mod.measure(pairs)
            except Exception as exc:
                fail("PERSONA_CRITERIA_ERROR",
                     "measure() raised %s: %s" % (type(exc).__name__, exc))
            if readings is not None:
                emit("PERSONA_GATE_CRITERIA script=%s corpus=%d" % (
                    args.criteria_script, len(pairs)))
                gated = set(norm(os.path.abspath(c["path"])) for c in cards)
                for key, (support, copied, prefixed) in sorted(readings.items()):
                    if key not in gated:
                        continue
                    name = by_key.get(key, os.path.basename(key))
                    if support > args.max_cross_card_support:
                        fail("PERSONA_CROSS_CARD_DUPLICATE",
                             "%s :: shared with %d cards (max=%d)" % (
                                 name, support, args.max_cross_card_support))
                    if copied > args.max_summary_copies:
                        fail("PERSONA_SUMMARY_COPIED_INTO_EXAMPLE",
                             "%s :: %d example(s) carry the whole summary (max=%d)" % (
                                 name, copied, args.max_summary_copies))
                    if prefixed > args.max_speaker_prefix:
                        fail("PERSONA_SPEAKER_LABEL_PREFIX",
                             "%s :: %d example(s) start with a speaker label (max=%d)" % (
                                 name, prefixed, args.max_speaker_prefix))

    # ---------- report ----------
    by_rule = Counter(r for r, _ in failures)
    for rule in ("PERSONA_CARD_UNREADABLE", "PERSONA_CORE_TOO_SHORT", "PERSONA_CORE_UNTERMINATED",
                 "PERSONA_KIN_NOT_A_NAME", "PERSONA_ID_PATTERN", "PERSONA_FIELD_SHAPE",
                 "PERSONA_TEMPLATE_LOAD", "PERSONA_TAG_STAMP", "PERSONA_TAG_UNREGISTERED",
                 "PERSONA_TAG_COUNT_STAMP", "PERSONA_CRITERIA_UNAVAILABLE",
                 "PERSONA_CRITERIA_ERROR", "PERSONA_CROSS_CARD_DUPLICATE",
                 "PERSONA_SUMMARY_COPIED_INTO_EXAMPLE", "PERSONA_SPEAKER_LABEL_PREFIX"):
        if not by_rule.get(rule):
            continue
        emit("FAIL %s count=%d" % (rule, by_rule[rule]))
        shown = 0
        for r, line in failures:
            if r != rule:
                continue
            if shown >= args.max_detail:
                emit("     ... %d more" % (by_rule[rule] - shown))
                break
            emit("     " + line)
            shown += 1

    failed_cards = len(set(line.split(" :: ")[0] for _, line in failures if " :: " in line))
    emit("PERSONA_GATE_SUMMARY total=%d failed_cards=%d violations=%d" % (
        n, failed_cards, len(failures)))
    if failures:
        emit("PERSONA_GATE_RED")
        code = 1
    else:
        emit("PERSONA_GATE_GREEN")
        code = 0

    if args.report:
        try:
            with io.open(args.report, "w", encoding="utf-8") as fh:
                fh.write(u"\n".join(REPORT) + u"\n")
            print("PERSONA_GATE_REPORT " + args.report)
        except Exception as exc:
            print("PERSONA_GATE_REPORT_FAILED " + str(exc))
    return code


if __name__ == "__main__":
    sys.exit(main())
