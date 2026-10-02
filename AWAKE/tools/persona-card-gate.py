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
                         --min-tag-count-values distinct values, and no single
                         value may exceed --max-tag-count-share UNLESS the tag
                         SETS are diverse (>= --min-tag-set-diversity unique
                         sets): a repeated count only evidences a template when
                         the sets repeat with it
                         (good 8 values, max 38% / bad 1 value, 100% with 5
                          distinct sets over 279 cards)
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
  R13 placeholder-text   no string anywhere in the card may contain an unfilled
                         template marker: （...heroId）/（...待填）/ {...} /
                         ${...} / <...> / TODO / TBD / FIXME / XXX
                         (good 0/76 / bad 0/262 / leaked batch 8/17)
                         HARD because identityFacts IS a runtime narrative
                         field: the leaked batch ships （游戏内配偶 heroId）
                         verbatim to the model.
  R14 summary-derived-from-description
                         summary must not be a prefix-extension of the card's
                         own publicDescription / privateDescription. Both the
                         raw field AND the field with a leading 对外，/私下里，
                         label stripped are checked (the leaked batch extended
                         the stripped text; extending the labelled text is the
                         same defect). Whitespace is ignored and the
                         description must be >= --min-derived-len characters
                         before it counts.
                         (good 0/76 / bad 0/262 / leaked batch 17/17)
                         HARD because summary IS in the runtime payload: a
                         mechanical splice means the card has no real summary.
  R15 profile-key-nonconformant
                         every PRESENT profile dict (traitProfile,
                         expressionProfile, behaviorProfile, reactionProfile,
                         commitmentProfile) must use exactly the canonical key
                         names from
                         PersonaWorkbench.Web/ProviderDraftContract.cs:307-310
                         and :738-743
                         (good 0/76 / bad 0/262 / leaked batch 17/17 on four
                         of the five profiles -- traitProfile is conformant
                         there; the leaked batch abbreviates every other key,
                         e.g. cond/delib/trust/lev/ing/lead instead of
                         conditionality/deliberation/trustTesting/leverage/
                         inGroupPriority/leadership)
                         HARD, not cosmetic: persona-awake-joint.ps1:591
                         requires exact field names, and :1092-1093 splices
                         seven of these values into the runtime narrative
                         fields, so an abbreviated profile is either rejected
                         or silently loses content.
  R16 facet-mirrors-tags WARNING ONLY -- never sets the exit code unless
                         --fail-on-facet-mirror is passed.
                         facetStrengths must not be a bare copy of tags; good
                         cards make it a strict superset
                         (good 2/52 present / bad 0/262 / leaked batch 17/17)
                         Deliberately soft: AUTHORING-GUIDELINES.zh-CN.md:61
                         classes facetStrengths as an author-side draft field
                         that the materializer drops.

  R17 batch-tag-stamp     HARD. A tag set that covers a large share of ONE
                          PRODUCTION BATCH is a stamp even when that batch is
                          diluted inside a big corpus. R4 asks "is this set a
                          large share of --cards?"; R17 asks "is this set a
                          large share of the cards that were generated
                          together?" -- and one --cards cannot serve both,
                          because R10-R12 REQUIRE the whole corpus while R4
                          needs a homogeneous population. That tension is the
                          defect R17 closes.
                          A batch is examined only when it holds
                          >= --min-batch-size cards; inside it R17 fires when
                          the largest tag-set group has
                          >= --min-batch-stamp-count cards AND more than
                          --max-batch-tag-share of the batch. The COUNT guard
                          (default 4) is what keeps small healthy batches
                          quiet: a 10-card batch in which two cards happen to
                          share a set is coincidence, not a stamp.
                          Grouping comes from --batch-map (authoritative and
                          deterministic) or, failing that, from mtime gaps
                          (--auto-batch-gap seconds, default 3600; 0 disables).
                          On a fresh git checkout every mtime collapses to one
                          value, so the whole corpus becomes a single batch and
                          R17 degenerates to exactly R4 -- a safe degradation,
                          never a false positive.
                          Measured 2026-10-01 (auto-gap 3600s): the only
                          healthy batch of >= 5 cards is 4/70 = 5.7% PASS;
                          LEAK17 forms its own batch at 5/17 = 29% FAIL; the
                          bad batch is 230/259 = 89% FAIL. R4 alone cannot see
                          the middle one -- it reads 5/355 = 1.4% -- which is
                          exactly how those 17 cards slipped through.
                          --max-batch-tag-share therefore defaults to the SAME
                          0.10 as R4: one share threshold to explain, and the
                          measured healthy rate sits 1.75x below it while the
                          smallest known stamp sits 2.9x above.

R13-R15 are LOCAL rules: pure per-card structural checks. R17 is local to a
BATCH rather than to a card, but it is still population-independent -- it never
reads a global share, so the size of the surrounding corpus cannot hide a stamp.
None of the four is gated by --min-population.

R10-R12 DELEGATE: the rule logic lives in exactly one place -- the external
criteria script (default persona-workbench/tools/measure-three-criteria.py,
override with --criteria-script). This gate only applies thresholds, so the two
implementations cannot drift apart. Their cross-card counts are always computed
over the WHOLE --cards directory, never over the --include-list subset: counting
inside a subset collapses every support value to 1 and the criterion could
never fire. NOTE that --cards itself defines "the corpus": point it at a
subdirectory and a duplicate with a card outside that subdirectory is invisible.
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
  py -3 AWAKE/tools/persona-card-gate.py --cards <dir> --auto-batch-gap 1800
  py -3 AWAKE/tools/persona-card-gate.py --cards <dir> --batch-map <file.tsv>

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
import datetime
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

# Canonical profile keys. Authority is the source, NOT a reverse-engineering of
# the good cards: PersonaWorkbench.Web/ProviderDraftContract.cs:307-310 lists
# the four authoring profiles and :738-743 lists traitProfile. The same names
# are hardcoded in persona-awake-joint.ps1:583-586 and :1057-1060.
CANONICAL_PROFILES = {
    "traitProfile": ("caution", "ambition", "pride", "pragmatism",
                     "inGroupLoyalty", "tradition"),
    "expressionProfile": ("restraint", "directness", "formality",
                          "playfulness", "warmth"),
    "behaviorProfile": ("conditionality", "deliberation", "trustTesting",
                        "leverage", "inGroupPriority", "leadership"),
    "reactionProfile": ("confrontation", "expression", "timing", "resentment",
                        "supportSeeking", "sensitiveConditions",
                        "conditionalResponses"),
    "commitmentProfile": ("promiseCaution", "promisePersistence",
                          "valueTradeability", "priorityOrder",
                          "protectedValues", "applicableScope",
                          "exceptionCost", "breachResponse"),
}

# R13: unfilled template markers. The measured case is （游戏内配偶 heroId）;
# the rest are the usual authoring escape hatches. Deliberately narrow -- every
# alternative names an identifier-shaped token, so ordinary CJK prose cannot
# trip it.
PLACEHOLDER = re.compile(
    u"[\uff08(][^\uff09)\r\n]{0,60}?"
    u"(heroId|heroID|hero_id|HeroId|charId|characterId|personaId"
    u"|TODO|TBD|FIXME|XXX|\\{\\{|\\$\\{|待填|待补|占位)"
    u"[^\uff09)\r\n]{0,60}?[\uff09)]"
    u"|\\{\\{[^{}]{1,60}\\}\\}"
    u"|\\$\\{[^{}]{1,60}\\}"
    u"|<[A-Za-z_][A-Za-z0-9_]{1,40}>"
    u"|\\{[A-Za-z_][A-Za-z0-9_.]{2,40}\\}")

# R14: the label the leaked batch prepends to description fields before
# splicing them into summary.
LEAD_PREFIX = re.compile(u"^(对外|私下里|私下|明面上|人前|人后)[，,：:]?")

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


def iter_strings(node, path=u"$"):
    """Yield (json-path, text) for every string anywhere in the document.

    Used by R13: an unfilled placeholder is a defect wherever it hides, so the
    scan is field-agnostic rather than an allowlist of known-bad fields.
    """
    if isinstance(node, dict):
        for k in node:
            for item in iter_strings(node[k], path + u"." + str(k)):
                yield item
    elif isinstance(node, list):
        for i, v in enumerate(node):
            for item in iter_strings(v, path + u"[%d]" % i):
                yield item
    elif isinstance(node, str):
        yield (path, node)


def squash(text):
    """Drop all whitespace: card text wraps, so comparisons must not care."""
    return re.sub(u"\\s+", u"", text or u"")


def strip_lead(text):
    """Remove the 对外，/私下里， label the leaked batch prepends."""
    return LEAD_PREFIX.sub(u"", (text or u"").strip())


def find_placeholder(text):
    """Return a short excerpt of the first unfilled marker, or None."""
    m = PLACEHOLDER.search(text or u"")
    if not m:
        return None
    hit = m.group(0)
    return hit if len(hit) <= 40 else hit[:40] + u"..."


def load_batch_map(path, cards_dir):
    """Read a TSV batch map: '<batch-id>\\t<path>' per line.

    Relative paths resolve against --cards (not the cwd) so a map stays valid
    wherever the gate is invoked from. Blank lines and '#' comments are ignored.
    Returns a list of (batch_id, normalised absolute path) in file order.
    """
    out = []
    with io.open(path, "r", encoding="utf-8-sig") as fh:
        for raw in fh:
            line = raw.rstrip(u"\r\n")
            if not line.strip() or line.lstrip().startswith(u"#"):
                continue
            if u"\t" in line:
                bid, p = line.split(u"\t", 1)
            else:
                parts = line.split(None, 1)
                if len(parts) != 2:
                    continue
                bid, p = parts
            p = p.strip()
            if not os.path.isabs(p):
                p = os.path.join(cards_dir, p)
            out.append((bid.strip(), norm(os.path.abspath(p))))
    return out


def group_by_mtime(entries, gap):
    """Split (key, mtime, label) triples into runs separated by > gap seconds.

    Returns a list of (batch_name, [key, ...]) in chronological order. Every
    run is one production batch under the assumption that cards written more
    than `gap` seconds apart did not come out of the same run.
    """
    ordered = sorted(entries, key=lambda t: (t[1], t[0]))
    runs, cur, prev = [], [], None
    for key, mt, label in ordered:
        if prev is not None and (mt - prev) > gap:
            runs.append(cur)
            cur = []
        cur.append((key, label))
        prev = mt
    if cur:
        runs.append(cur)
    return [(u"auto#%d@%s" % (i + 1, run[0][1]), [k for k, _ in run])
            for i, run in enumerate(runs)]


def read_cards(cards_dir, include, exclude):
    cards = []
    for p in sorted(glob.glob(os.path.join(cards_dir, "*.persona.json"))):
        # load_list() stores absolute normalized paths, so the key MUST be absolute too.
        # With a relative --cards, glob() returns relative paths and every key misses the
        # include set -> silently zero cards selected. Absolutize both sides.
        key = norm(os.path.abspath(p))
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
    ap.add_argument("--min-tag-set-diversity", type=float, default=0.25)
    ap.add_argument("--include-list", default=None)
    ap.add_argument("--exclude-list", default=None)
    ap.add_argument("--criteria-script", default=DEFAULT_CRITERIA_SCRIPT)
    ap.add_argument("--skip-external-criteria", action="store_true")
    ap.add_argument("--max-cross-card-support", type=int, default=2)
    ap.add_argument("--max-summary-copies", type=int, default=0)
    ap.add_argument("--max-speaker-prefix", type=int, default=0)
    ap.add_argument("--min-derived-len", type=int, default=10)
    ap.add_argument("--fail-on-facet-mirror", action="store_true")
    ap.add_argument("--batch-map", default=None)
    ap.add_argument("--auto-batch-gap", type=int, default=3600)
    ap.add_argument("--min-batch-size", type=int, default=5)
    ap.add_argument("--min-batch-stamp-count", type=int, default=4)
    ap.add_argument("--max-batch-tag-share", type=float, default=0.10)
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

    failures = []          # (rule, detail-line) -- these set the exit code
    warns = []             # (rule, detail-line) -- advisory, never fail

    def fail(rule, line):
        failures.append((rule, line))

    # ---------- R1 / R2 / R5 / R6 / R8 / R13 / R14 / R15 / R16 : per card ----
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

        # R13 -- unfilled template markers, anywhere in the card.
        for jpath, text in iter_strings(d):
            hit = find_placeholder(text)
            if hit:
                fail("PERSONA_PLACEHOLDER_TEXT",
                     "%s :: %s=%s" % (c["name"], jpath, hit))

        # R14 -- summary must be its own text, not a splice of a description.
        # BOTH the raw field and the field with its 对外，/私下里， label
        # stripped are checked. The mutation harness caught the raw form
        # slipping through: the leaked batch extended the stripped text, but
        # extending the labelled text is the same defect.
        su = squash(d.get("summary") or u"")
        if su:
            hit = None
            for label in ("publicDescription", "privateDescription"):
                field = d.get(label) or u""
                for note, src in ((u"", squash(field)),
                                  (u" (label stripped)", squash(strip_lead(field)))):
                    if len(src) >= args.min_derived_len and su.startswith(src):
                        hit = (label, note, len(src))
                        break
                if hit:
                    break
            if hit:
                fail("PERSONA_SUMMARY_DERIVED_FROM_DESCRIPTION",
                     "%s :: summary starts with %s%s (%d chars)" % (
                         c["name"], hit[0], hit[1], hit[2]))

        # R15 -- present profile dicts must use the canonical key names.
        for pname in sorted(CANONICAL_PROFILES):
            if pname not in d:
                continue
            val = d[pname]
            if not isinstance(val, dict):
                fail("PERSONA_PROFILE_KEY_NONCONFORMANT",
                     "%s :: %s is %s, expected object" % (
                         c["name"], pname, type(val).__name__))
                continue
            got = set(val.keys())
            want = set(CANONICAL_PROFILES[pname])
            if got != want:
                fail("PERSONA_PROFILE_KEY_NONCONFORMANT",
                     "%s :: %s keys=%d expected=%d extra=%s missing=%s" % (
                         c["name"], pname, len(got), len(want),
                         u",".join(sorted(got - want)) or u"-",
                         u",".join(sorted(want - got)) or u"-"))

        # R16 -- facetStrengths mirroring tags. Warning by default.
        fs = d.get("facetStrengths")
        if isinstance(fs, dict) and fs:
            tset = set(tag_ids(d))
            if tset and set(fs.keys()) == tset:
                line = "%s :: facetStrengths == tags (%d keys)" % (
                    c["name"], len(tset))
                if args.fail_on_facet_mirror:
                    fail("PERSONA_FACET_MIRRORS_TAGS", line)
                else:
                    warns.append(("PERSONA_FACET_MIRRORS_TAGS", line))

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
            sets = set(tuple(sorted(tag_ids(c["data"])))
                       for c in cards if not c["error"])
            diversity = len(sets) / float(n)
            count_stamp = distinct < args.min_tag_count_values
            # A concentrated tag COUNT only evidences a template when the tag
            # SETS repeat with it. Measured 2026-10-02: 355 cards carry 348
            # distinct tag sets (98%) yet 233 of them hold exactly 6 tags
            # (66%) -- an authoring convention, not a stamp. The 2026-10-01 bad
            # batch (279 cards, 5 distinct sets, one count value at 100%) still
            # trips both branches, so the rule did not lose its red case.
            shape_stamp = (share > args.max_tag_count_share
                           and diversity < args.min_tag_set_diversity)
            if count_stamp or shape_stamp:
                fail("PERSONA_TAG_COUNT_STAMP",
                     "distinct=%d (min=%d) largest=%d tag(s) x%d (%.0f%% max=%.0f%%) "
                     "tag_sets=%d (%.0f%% min=%.0f%%)" % (
                         distinct, args.min_tag_count_values, val, cnt,
                         share * 100, args.max_tag_count_share * 100,
                         len(sets), diversity * 100,
                         args.min_tag_set_diversity * 100))

    # ---------- R17 : batch-local tag stamp (hard, population-independent) ---
    # R4 asks "is this tag set a large share of --cards?". That question cannot
    # be answered with one --cards, because R10-R12 need the WHOLE corpus while
    # R4 needs a HOMOGENEOUS one -- so a 5/17 stamp diluted into 5/355 hides.
    # R17 asks the batch-local question instead: a tag set covering most of one
    # production run is a stamp however thinly it is spread across the corpus.
    batches = None
    batch_src = u""
    if args.batch_map:
        by_bid, order = defaultdict(list), []
        for bid, key in load_batch_map(args.batch_map, args.cards):
            if bid not in by_bid:
                order.append(bid)
            by_bid[bid].append(key)
        listed = set()
        for bid in order:
            listed.update(by_bid[bid])
        batches = [(bid, by_bid[bid]) for bid in order]
        unlisted = [norm(os.path.abspath(c["path"])) for c in cards
                    if not c["error"]
                    and norm(os.path.abspath(c["path"])) not in listed]
        if unlisted:
            batches.append((u"(unlisted)", unlisted))
        batch_src = u"map=%s" % os.path.basename(args.batch_map)
    elif args.auto_batch_gap > 0:
        entries = []
        for c in cards:
            if c["error"]:
                continue
            try:
                mt = os.stat(c["path"]).st_mtime
            except OSError:
                continue
            entries.append((norm(os.path.abspath(c["path"])), mt,
                            datetime.datetime.fromtimestamp(mt).strftime(
                                "%Y-%m-%d %H:%M")))
        batches = group_by_mtime(entries, args.auto_batch_gap)
        batch_src = u"auto-gap=%ds" % args.auto_batch_gap
    if batches is None:
        emit("PERSONA_GATE_NOTE no batch grouping "
             "(--batch-map / --auto-batch-gap) : R17 skipped")
    else:
        by_key = {}
        for c in cards:
            if not c["error"]:
                by_key[norm(os.path.abspath(c["path"]))] = c
        emit("PERSONA_GATE_BATCHES total=%d source=%s" % (len(batches), batch_src))
        checked = 0
        for bname, keys in batches:
            members = [by_key[k] for k in keys if k in by_key]
            if len(members) < args.min_batch_size:
                continue
            checked += 1
            groups = Counter(tuple(sorted(tag_ids(m["data"]))) for m in members)
            if not groups:
                continue
            tags, cnt = groups.most_common(1)[0]
            share = cnt / float(len(members))
            emit("PERSONA_GATE_BATCH name=%s cards=%d largest=%d/%d" % (
                bname, len(members), cnt, len(members)))
            if cnt >= args.min_batch_stamp_count and share > args.max_batch_tag_share:
                fail("PERSONA_BATCH_TAG_STAMP",
                     "batch %s :: largest tag set %d/%d (%.0f%%) tags=%s" % (
                         bname, cnt, len(members), share * 100, ",".join(tags)))
        emit("PERSONA_GATE_BATCHES_CHECKED count=%d min_size=%d" % (
            checked, args.min_batch_size))

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
                 "PERSONA_SUMMARY_COPIED_INTO_EXAMPLE", "PERSONA_SPEAKER_LABEL_PREFIX",
                 "PERSONA_PLACEHOLDER_TEXT", "PERSONA_SUMMARY_DERIVED_FROM_DESCRIPTION",
                 "PERSONA_PROFILE_KEY_NONCONFORMANT", "PERSONA_FACET_MIRRORS_TAGS",
                 "PERSONA_BATCH_TAG_STAMP"):
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

    # ---------- advisory section (R16) -- never affects the exit code --------
    by_warn = Counter(r for r, _ in warns)
    for rule in ("PERSONA_FACET_MIRRORS_TAGS",):
        if not by_warn.get(rule):
            continue
        emit("WARN %s count=%d" % (rule, by_warn[rule]))
        shown = 0
        for r, line in warns:
            if r != rule:
                continue
            if shown >= args.max_detail:
                emit("     ... %d more" % (by_warn[rule] - shown))
                break
            emit("     " + line)
            shown += 1
    if warns:
        emit("PERSONA_GATE_WARNINGS total=%d" % len(warns))

    # failed_cards counts CARD-attached failures only. R17 reports per batch,
    # not per card, so its detail lines must not inflate the card count -- that
    # is why the prefix is matched against the real card names instead of being
    # trusted as one.
    known_names = set(c["name"] for c in cards)
    prefixes = [line.split(" :: ")[0] for _, line in failures if " :: " in line]
    failed_cards = len(set(p for p in prefixes if p in known_names))
    group_level = len(set(p for p in prefixes if p not in known_names))
    emit("PERSONA_GATE_SUMMARY total=%d failed_cards=%d violations=%d" % (
        n, failed_cards, len(failures)))
    if group_level:
        emit("PERSONA_GATE_GROUP_LEVEL groups=%d (batch/whole-corpus rules; "
             "not attached to any single card)" % group_level)
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
