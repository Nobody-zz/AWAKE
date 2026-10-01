"""Read-only calibration supplement; does not change gates or source cards.

Run: py -3.11 AWAKE/tools/persona-workbench/tools/measure-three-criteria.py
Output is ASCII JSON so Windows console encoding cannot corrupt evidence.
"""
import collections
import copy
import hashlib
import json
from pathlib import Path
import re
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[4]
CARD_DIR = ROOT / "AWAKE/tools/persona-workbench/characters"


def git(*args):
    return subprocess.check_output(
        ["git", "-c", "core.quotepath=false", *args], cwd=ROOT,
        text=True, encoding="utf-8"
    ).splitlines()


def norm(text):
    return re.sub(r"\s", "", text) if isinstance(text, str) else ""


def units(card):
    result = []
    for key in ("hardLine", "negotiable", "breachSwitch"):
        text = norm(card.get("tensionAxes", {}).get(key, ""))
        if text and text.lower() != "none":
            result.append(("tensionAxes." + key, text))
    for key in ("selfClaimRules", "realSelfBehaviors"):
        values = card.get(key, [])
        if not isinstance(values, list):
            raise ValueError("Invalid source array: " + key)
        result.extend((key, norm(text)) for text in values if norm(text))
    return result


def measure(cards):
    inverse = collections.defaultdict(set)
    for path, card in cards:
        for key in units(card):
            inverse[key].add(path)
    result = {}
    for path, card in cards:
        support = max((len(inverse[key]) for key in units(card)), default=0)
        summary = norm(card.get("summary", ""))
        examples = [norm(text) for text in card.get("selfClaimExamples", [])]
        copied = sum(bool(summary) and summary in text for text in examples)
        name = norm(card.get("displayName", ""))
        pattern = (r"^" + re.escape(name) + r"[\uff08(][^\uff09)\r\n]+\u5bb6[\uff09)]") if name else r"(?!)"
        prefixed = sum(bool(re.search(pattern, text)) for text in examples)
        result[path] = (support, copied, prefixed)
    return result


def distribution(values):
    return {"min": min(values), "median": statistics.median(values),
            "max": max(values), "histogram": dict(sorted(collections.Counter(values).items()))}


def main():
    tracked = set(git("ls-files", "--", "AWAKE/tools/persona-workbench/characters/*.persona.json"))
    cards = []
    digest = hashlib.sha256()
    for path in sorted(CARD_DIR.glob("*.persona.json")):
        relative = path.relative_to(ROOT).as_posix()
        raw = path.read_bytes()
        digest.update(relative.encode("utf-8") + b"\0" + hashlib.sha256(raw).digest())
        cards.append((relative, json.loads(raw.decode("utf-8-sig"))))
    good = [(p, c) for p, c in cards if p in tracked]
    bad = [(p, c) for p, c in cards if p not in tracked]
    if (len(good), len(bad)) != (76, 279):
        raise ValueError("Calibration population changed; recalibration required")
    measured = measure(cards)
    # Frozen from the 76-card calibration, never re-derived from future candidates.
    limits = (2, 0, 0)
    if tuple(max(measured[p][i] for p, _ in good) for i in range(3)) != limits:
        raise ValueError("Good baseline changed; review calibration")
    report = {"head": git("rev-parse", "HEAD")[0], "corpus_sha256": digest.hexdigest(),
              "limits": limits, "groups": {}, "mutations": []}
    for label, population in (("good", good), ("bad", bad)):
        report["groups"][label] = {
            "count": len(population),
            "criteria": [{**distribution([measured[p][i] for p, _ in population]),
                          "flagged": [p for p, _ in population if measured[p][i] > limits[i]]}
                         for i in range(3)]}
    for i in range(3):
        mutated = copy.deepcopy(cards)
        targets = [(p, c) for p, c in mutated if p in tracked]
        if i == 0:
            existing = collections.defaultdict(set)
            for path, card in good:
                for key in units(card):
                    existing[key].add(path)
            (field, text), owners = next((key, paths) for key, paths in existing.items() if len(paths) == 2)
            target = next((p, c) for p, c in targets if p not in owners)
            targets = [target]
            if field.startswith("tensionAxes."):
                target[1]["tensionAxes"][field.split(".")[1]] = text
            else:
                target[1][field][0] = text
        elif i == 1:
            targets[0][1]["selfClaimExamples"][0] = targets[0][1]["summary"]
        else:
            card = targets[0][1]
            card["selfClaimExamples"][0] = card["displayName"] + "\uff08TEST\u5bb6\uff09" + card["selfClaimExamples"][0]
        path = targets[0][0]
        value = measure(mutated)[path][i]
        assert value > limits[i], "Mutation escaped"
        report["mutations"].append({"criterion": i + 1, "path": path,
                                    "before": measured[path][i], "after": value, "detected": True})
    print(json.dumps(report, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    main()
