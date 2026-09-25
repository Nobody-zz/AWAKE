#!/usr/bin/env python3
"""Reproducible Persona AI-feel probes. Counts locate risks; they do not certify prose.

No card is rewritten. `chain` materializes a copy in a temporary directory, uses the
real Persona DSL simulator, then asks the production-smoke executable to call the
actual C# prompt template and renderer. The temp directory is the only deletion scope.
"""

import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import re
import shutil
import statistics
import subprocess
import sys
import tempfile
import urllib.request

REPO = Path(__file__).resolve().parents[3]
FIELDS = ("core", "identityFacts", "summary", "publicDescription",
          "privateDescription", "contradictionDescription", "selfClaimRules",
          "realSelfBehaviors", "selfClaimExamples")
LIST_FIELDS = FIELDS[-3:]
MARKERS = ("先", "再", "才", "只", "不", "别", "若", "但", "却", "把", "就", "便")
SCENARIOS = {"identity": "你是谁？家里做什么的？",
             "boundary": "我要从你这儿拿走一样东西——你最不愿放手的那件。你怎么说？",
             "challenge": "你说的这条路子，我看不靠谱。"}


def shape(text):
    """Clause architecture, not exact wording: keeps connective/negation sequence."""
    s = re.sub(r"\s+", "", text)
    s = re.sub(r"[^，。！？；：、,—（）()\w]", "", s)
    pattern = "(" + "|".join(map(re.escape, MARKERS)) + "|[，。！？；：、,—（）()])"
    bits = re.split(pattern, s)
    return "".join(bit if bit in MARKERS or re.fullmatch(r"[，。！？；：、,—（）()]", bit or "")
                   else "X" for bit in bits if bit)


def frame_family(text):
    """Broad construction family; catches slot/particle changes that exact shapes miss."""
    s = re.sub(r"\s+", "", text)
    if "先" in s and ("再" in s or "才" in s or "然后" in s):
        return "first_then"
    if "先" in s:
        return "first_then_implicit"
    if "只" in s and ("不" in s or "别" in s):
        return "only_not"
    if "不" in s and ("不" in s[s.index("不") + 1:] or "别" in s):
        return "double_refusal"
    if "把" in s:
        return "disposal_ba"
    if "若" in s or "如果" in s:
        return "conditional"
    return "other"


def rows_from_definitions(directory):
    rows = []
    files = sorted(Path(directory).glob("*.definition.json"))
    if not files:
        raise ValueError(f"No definitions in {directory}")
    for path in files:
        obj = json.loads(path.read_text(encoding="utf-8"))
        for field in FIELDS:
            value = obj.get(field)
            values = value if isinstance(value, list) else [value]
            for index, item in enumerate(values):
                if isinstance(item, str) and item.strip():
                    rows.append({"card": path.name, "field": field, "index": index,
                                 "text": item.strip(), "shape": shape(item),
                                 "family": frame_family(item)})
    return rows


def metrics(rows):
    report = {}
    for field in (*FIELDS, "reply"):
        data = [r for r in rows if r["field"] == field]
        if not data:
            continue
        counts = Counter(r["shape"] for r in data)
        families = Counter(r["family"] for r in data)
        lengths = [len(r["text"]) for r in data]
        endings = Counter((r["text"][-1] if r["text"] else "") for r in data)
        max_count = max(counts.values())
        top = sorted(counts.items(), key=lambda x: (-x[1], x[0]))[:5]
        report[field] = {
            "n": len(data), "cards": len({r["card"] for r in data}),
            "top_shape_share": round(max_count / len(data), 4),
            "shape_diversity": round(len(counts) / len(data), 4),
            "frame_families": dict(families.most_common()),
            "first_family_share": round((families["first_then"] + families["first_then_implicit"]) / len(data), 4),
            "top_non_other_family_share": round(max((v for k, v in families.items() if k != "other"), default=0) / len(data), 4),
            "length_min": min(lengths), "length_median": statistics.median(lengths),
            "length_max": max(lengths),
            "length_cv": round(statistics.pstdev(lengths) / (statistics.mean(lengths) or 1), 4),
            "top_ending_share": round(max(endings.values()) / len(data), 4),
            "top_shapes": [{"shape": sig, "n": count,
                            "examples": [{"card": r["card"], "index": r["index"], "text": r["text"]}
                                         for r in data if r["shape"] == sig][:4]}
                           for sig, count in top],
        }
    report["review_flags"] = [
        {"card": r["card"], "field": r["field"], "index": r["index"], "text": r["text"],
         "reason": "second_person_in_example"}
        for r in rows if r["field"] == "selfClaimExamples" and "你" in r["text"]
    ]
    report["manual_review_queue"] = [
        {"card": r["card"], "field": r["field"], "index": r["index"], "text": r["text"],
         "check": ("Identify a concrete scene and addressee; confirm '你' does not become player"
                   if r["field"] == "selfClaimExamples" else
                   "Cite authority/evidence for this behavior; place association alone is insufficient")}
        for r in rows if r["field"] in LIST_FIELDS
    ]
    return report


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def parse_reply(raw):
    text = re.sub(r"^```(?:json)?\s*|\s*```$", "", raw.strip(), flags=re.I).strip()
    parsed = json.loads(text)
    reply = parsed["reply"]
    if not isinstance(reply, str) or not reply.strip():
        raise ValueError("reply is empty or not a string")
    return reply


def answer_metrics(path, version="after"):
    source = json.loads(Path(path).read_text(encoding="utf-8"))
    rows = []
    invalid = []
    for index, record in enumerate(source["results"]):
        if record.get("version") != version:
            continue
        try:
            reply = parse_reply(record.get("raw", ""))
        except (json.JSONDecodeError, KeyError, TypeError, ValueError) as error:
            invalid.append({"row": index, "card": record.get("stem"),
                            "scenario": record.get("scenario"), "error": str(error),
                            "raw_excerpt": record.get("raw", "")[:200]})
            continue
        rows.append({"card": record["stem"], "scenario": record["scenario"],
                     "sample": record["sample"], "field": "reply", "index": index,
                     "text": reply, "shape": shape(reply), "family": frame_family(reply)})
    if not rows:
        raise ValueError("No valid replies for requested version; refusing empty-pass")
    by_scenario = {scenario: metrics([r for r in rows if r["scenario"] == scenario])["reply"]
                   for scenario in sorted({r["scenario"] for r in rows})}
    return {"source_sha256": digest(path), "model": source.get("model"), "version": version,
            "valid": len(rows), "invalid": invalid, "by_scenario": by_scenario}


def relation_report(definitions, links_path):
    """Evidence ceilings, not a semantic verifier or a claim of no relationship."""
    links_doc = json.loads(Path(links_path).read_text(encoding="utf-8"))
    links = defaultdict(list)
    for link in links_doc.get("links", []):
        links[link.get("heroId")].append(link)
    cards = []
    for path in sorted(Path(definitions).glob("*.definition.json")):
        obj = json.loads(path.read_text(encoding="utf-8"))
        hero = obj.get("characterId")
        matched = links.get(hero, [])
        ceilings = sorted({link.get("confirmedFor") or "unspecified" for link in matched
                           if link.get("state") == "confirmed"})
        lines = [{"field": field, "index": i, "text": text,
                  "manual_question": "Which source proves this action, power or motive? A place link alone does not."}
                 for field in LIST_FIELDS for i, text in enumerate(obj.get(field) or [])]
        cards.append({"card": path.name, "hero_id": hero,
                      "confirmed_for": ceilings,
                      "place_only": bool(ceilings) and ceilings == ["place"],
                      "no_link_recorded": not matched,
                      "boundary_lines_to_verify": lines})
    if not cards:
        raise ValueError(f"No definitions in {definitions}")
    return {"definitions": str(definitions), "links_sha256": digest(links_path), "cards": cards}


def run(command):
    result = subprocess.run([str(x) for x in command], text=True, encoding="utf-8",
                            errors="replace", capture_output=True)
    if result.returncode:
        raise RuntimeError(f"command failed ({result.returncode}): {command}\n{result.stdout}\n{result.stderr}")
    return result.stdout + result.stderr


def dsl_usable(log):
    return ("[PERSONA_DSL_OK]" in log and "fallback=False" in log
            and "WARN persona.tag_unregistered" not in log)


def chain(args):
    cards = [Path(p).resolve() for p in args.cards]
    for card in cards:
        if not card.is_file() or not card.name.endswith(".persona.json"):
            raise ValueError(f"Expected existing *.persona.json: {card}")
    registry = REPO / "ModuleData/Worldbook/persona_definitions/tag_registry.json"
    materializer = REPO / "tools/persona-workbench/tools/materialize-definitions.ps1"
    simulator = REPO / "tools/worldbook-runtime-sim/bin/Release/net10.0-windows/WorldbookRuntimeSim.dll"
    renderer = REPO / "tools/worldbook-runtime-production-smoke/artifacts/bin/Release/Awake.WorldbookRuntimeProductionSmoke.exe"
    if not simulator.is_file() or not renderer.is_file():
        raise FileNotFoundError("Build WorldbookRuntimeSim Release and WorldbookRuntimeProductionSmoke Release first")
    with tempfile.TemporaryDirectory(prefix="awake-aifeel-") as temp:
        root = Path(temp)
        source, defs = root / "cards", root / "definitions"
        source.mkdir()
        defs.mkdir()
        for card in cards:
            shutil.copy2(card, source / card.name)
            sidecar = card.with_name(card.name.replace(".persona.json", ".origins.json"))
            if sidecar.is_file():
                shutil.copy2(sidecar, source / sidecar.name)
        run(["powershell", "-NoProfile", "-File", materializer, "-CharactersDir", source,
             "-RegistryPath", registry, "-OutDir", defs])
        definitions = sorted(defs.glob("*.definition.json"))
        if len(definitions) != len(cards):
            raise RuntimeError("Materializer did not produce one definition per selected card")
        result = {"source_hashes": {p.name: digest(p) for p in cards},
                  "registry_sha256": digest(registry),
                  "renderer_source_sha256": digest(REPO / "src/NpcDialoguePromptPipeline.cs"),
                  "template_source_sha256": digest(REPO / "src/Prompts/NpcPromptTemplate.cs"),
                  "renderer_exe_sha256": digest(renderer), "simulator_dll_sha256": digest(simulator),
                  "model": args.model, "seed": args.seed if args.model else None, "items": []}
        for definition in definitions:
            obj = json.loads(definition.read_text(encoding="utf-8"))
            hero_id = obj["characterId"]
            display_name = obj.get("materialization", {}).get("sourceFile", definition.name).split("_")[0]
            dsl = root / (definition.stem + ".dsl.txt")
            log = run(["dotnet", simulator, "persona", defs, registry, hero_id, dsl,
                       "6144", "--force-approved"])
            if not dsl_usable(log):
                raise RuntimeError(f"Persona fallback; cannot evaluate style: {definition.name}\n{log}")
            item = {"card": definition.name, "hero_id": hero_id,
                    "definition_sha256": digest(definition), "dsl_sha256": digest(dsl),
                    "dsl": dsl.read_text(encoding="utf-8"), "dsl_log": log.strip(), "prompts": {}}
            for label, player_turn in SCENARIOS.items():
                variables = {
                    "retrieved_knowledge": "当前没有检索到可用的世界书条目。", "npc_memory": "",
                    "npc_identity": display_name, "persona_dsl": dsl.read_text(encoding="utf-8"),
                    "npc_state": "当前没有已记录的角色状态。", "npc_commitments": "当前没有已记录的未决承诺。",
                    "player_known": "玩家名号不详，家族不详，所属不详。", "scene": "城堡大厅，无旁人。",
                    "opening_hint": "", "player_turn": player_turn, "npc_id": hero_id,
                    "dialogue_action_mode": "chat：本轮只进行普通交谈；不得输出 command。", "dialogue_history": "",
                }
                vars_file = root / "variables.json"
                vars_file.write_text(json.dumps(variables, ensure_ascii=False), encoding="utf-8")
                prompt_file = root / "prompt.txt"
                run([renderer, "persona-prompt-render", vars_file, prompt_file])
                prompt = prompt_file.read_text(encoding="utf-8")
                item["prompts"][label] = {"sha256": digest(prompt_file), "prompt": prompt}
                if args.model:
                    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
                    payload = {"model": args.model, "messages": [{"role": "user", "content": prompt}],
                               "stream": False, "options": {"temperature": 0.7, "top_p": 0.9,
                                                            "num_predict": 500, "seed": args.seed}}
                    req = urllib.request.Request("http://127.0.0.1:11434/api/chat",
                                                 data=json.dumps(payload).encode("utf-8"),
                                                 headers={"Content-Type": "application/json"})
                    with opener.open(req, timeout=900) as response:
                        body = json.load(response)
                    item["prompts"][label]["raw"] = body["message"]["content"]
                    item["prompts"][label]["ollama_model"] = body.get("model")
            result["items"].append(item)
        result["text_metrics"] = metrics(rows_from_definitions(defs))
        if args.model:
            reply_rows, invalid = [], []
            for item in result["items"]:
                for label, response in item["prompts"].items():
                    try:
                        reply = parse_reply(response["raw"])
                    except (json.JSONDecodeError, KeyError, TypeError, ValueError) as error:
                        invalid.append({"card": item["card"], "scenario": label, "error": str(error)})
                        continue
                    reply_rows.append({"card": item["card"], "field": "reply", "index": 0,
                                       "text": reply, "shape": shape(reply), "family": frame_family(reply),
                                       "scenario": label})
            result["reply_metrics"] = {
                "valid": len(reply_rows), "invalid": invalid,
                "by_scenario": {label: metrics([r for r in reply_rows if r["scenario"] == label]).get("reply")
                                for label in SCENARIOS},
            }
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    audit = sub.add_parser("audit", help="score materialized definitions, no model calls")
    audit.add_argument("--definitions", type=Path, required=True)
    answers = sub.add_parser("answers", help="measure existing Ollama raw JSON, after-only by default")
    answers.add_argument("file", type=Path)
    answers.add_argument("--version", default="after")
    relations = sub.add_parser("relations", help="place/person link evidence ceiling vs boundary lines")
    relations.add_argument("--definitions", type=Path, required=True)
    relations.add_argument("--links", type=Path, required=True)
    live = sub.add_parser("chain", help="copy selected cards, run true materialize/DSL/render chain")
    live.add_argument("cards", nargs="+", help="exact *.persona.json files")
    live.add_argument("--model", choices=("ministral-3:8b", "qwen2.5:latest"))
    live.add_argument("--seed", type=int, default=1700)
    parser.add_argument("--out", type=Path, help="write full machine-readable report instead of stdout")
    args = parser.parse_args()
    result = (metrics(rows_from_definitions(args.definitions)) if args.command == "audit" else
              answer_metrics(args.file, args.version) if args.command == "answers" else
              relation_report(args.definitions, args.links) if args.command == "relations" else chain(args))
    payload = json.dumps(result, ensure_ascii=False, indent=2)
    if args.out:
        args.out.write_text(payload + "\n", encoding="utf-8")
        print(f"REPORT={args.out} SHA256={digest(args.out)}")
    else:
        print(payload)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, FileNotFoundError, RuntimeError) as error:
        print(f"EVALUATION_BLOCKED: {error}", file=sys.stderr)
        sys.exit(2)
