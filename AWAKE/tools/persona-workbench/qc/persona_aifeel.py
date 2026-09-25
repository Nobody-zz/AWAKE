#!/usr/bin/env python3
"""Reproducible Persona AI-feel probes. Counts locate risks; they do not certify prose.

No card is rewritten. `chain` materializes a copy in a temporary directory, uses the
real Persona DSL simulator, then asks the production-smoke executable to call the
actual C# prompt template and renderer. The temp directory is the only deletion scope.
"""

import argparse
from collections import Counter, defaultdict
from datetime import datetime, timezone
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
             "challenge": "你说的这条路子，我看不靠谱。",
             "unknown": "前几日盐运被谁截了？你手上有确凿消息吗？"}
OLLAMA_OPTIONS = {"temperature": 0.7, "top_p": 0.9, "num_predict": 500}


def find_ollama_model_tag(document, model):
    if not isinstance(document, dict) or not isinstance(document.get("models"), list):
        return None
    for entry in document["models"]:
        if isinstance(entry, dict) and entry.get("name") == model:
            return {key: entry.get(key) for key in ("name", "digest", "size")}
    return None


def ollama_error_detail(error):
    if isinstance(error, urllib.error.HTTPError):
        try:
            body = error.read().decode("utf-8", errors="replace").strip()
        except OSError:
            body = ""
        return f"HTTP {error.code}: {body[:1000]}" if body else f"HTTP {error.code}: {error.reason}"
    return f"{type(error).__name__}: {error}"


def build_unknown_fact_review_queue(items, samples_per_scenario):
    queue = []
    incomplete = False
    for item in items:
        responses = item.get("prompts", {}).get("unknown", [])
        for response in responses:
            if "raw" not in response:
                incomplete = True
                continue
            try:
                reply = parse_reply(response["raw"])
            except (json.JSONDecodeError, KeyError, TypeError, ValueError):
                incomplete = True
                continue
            queue.append({"card": item["card"], "sample": response["sample"],
                          "seed": response["seed"], "prompt_sha256": response["sha256"],
                          "reply": reply, "status": "needs_manual_grounding_review"})
        if len(responses) != samples_per_scenario:
            incomplete = True
    expected = len(items) * samples_per_scenario
    status = "incomplete" if incomplete or len(queue) != expected else "needs_human_review"
    return {"status": status, "expected": expected, "queued": len(queue), "items": queue}


def sample_seeds(base_seed, sample_count):
    if not isinstance(sample_count, int) or isinstance(sample_count, bool) or not 1 <= sample_count <= 5:
        raise ValueError("samples must be an integer from 1 through 5")
    return [(index + 1, base_seed + index) for index in range(sample_count)]


def reply_run_status(valid_count, invalid_count, expected_count):
    if valid_count == 0:
        return "no_valid_replies"
    if valid_count == expected_count and invalid_count == 0:
        return "scored"
    return "incomplete"


def normalize_connectives(text):
    # A small, explicit mutation set; not a Chinese syntax parser.
    for old, new in (("第一步", "先"), ("首先", "先"), ("起初", "先"),
                     ("随后", "再"), ("接着", "再"), ("然后", "再")):
        text = text.replace(old, new)
    return text


def shape(text):
    """Clause architecture, not exact wording: keeps connective/negation sequence."""
    s = re.sub(r"\s+", "", normalize_connectives(text))
    s = re.sub(r"[^，。！？；：、,—（）()\w]", "", s)
    pattern = "(" + "|".join(map(re.escape, MARKERS)) + "|[，。！？；：、,—（）()])"
    bits = re.split(pattern, s)
    return "".join(bit if bit in MARKERS or re.fullmatch(r"[，。！？；：、,—（）()]", bit or "")
                   else "X" for bit in bits if bit)


def frame_family(text):
    """Broad construction family; catches slot/particle changes that exact shapes miss."""
    s = re.sub(r"\s+", "", normalize_connectives(text))
    if "若" in s or "如果" in s or "只要" in s:
        return "conditional"
    if ("不只" in s or "不仅" in s) and any(marker in s for marker in ("也", "还", "更")):
        return "other"
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
    return "other"


def validate_definition_fields(obj, path):
    for field in FIELDS:
        value = obj.get(field)
        if value is None:
            continue
        if field in LIST_FIELDS:
            if not isinstance(value, list) or any(not isinstance(item, str) for item in value):
                raise ValueError(f"{path}: {field} must be an array of strings")
        elif not isinstance(value, str):
            raise ValueError(f"{path}: {field} must be a string")


def rows_from_definitions(directory):
    rows = []
    files = sorted(Path(directory).glob("*.definition.json"))
    if not files:
        raise ValueError(f"No definitions in {directory}")
    for path in files:
        obj = json.loads(path.read_text(encoding="utf-8"))
        validate_definition_fields(obj, path)
        for field in FIELDS:
            value = obj.get(field)
            values = value if isinstance(value, list) else [value]
            for index, item in enumerate(values):
                if isinstance(item, str) and item.strip():
                    rows.append({"card": path.name, "field": field, "index": index,
                                 "text": item.strip(), "shape": shape(item),
                                 "family": frame_family(item)})
    return rows


def rows_from_dsl(text, card="<dsl>"):
    """Read only DATA_CN values that actually survived into emitted Persona DSL."""
    rows, section = [], None
    for line_number, line in enumerate(text.splitlines(), 1):
        header = re.fullmatch(r"\[([A-Z][A-Z0-9_]*)\]", line.strip())
        if header:
            section = header.group(1)
            continue
        if not line.startswith("DATA_CN="):
            continue
        if section is None:
            raise ValueError(f"{card}:{line_number}: DATA_CN outside a DSL section")
        try:
            value = json.loads(line[len("DATA_CN="):])
        except json.JSONDecodeError as error:
            raise ValueError(f"{card}:{line_number}: invalid DATA_CN JSON string") from error
        if not isinstance(value, str):
            raise ValueError(f"{card}:{line_number}: DATA_CN must be a JSON string")
        if value.strip():
            rows.append({"card": card, "field": section, "index": line_number,
                         "text": value.strip(), "shape": shape(value),
                         "family": frame_family(value)})
    return rows


def validate_required_claims(path, card_names):
    """Load exact author-asserted runtime claims, keyed by source card basename."""
    document = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(document, dict) or set(document) != {"schemaVersion", "cards"}:
        raise ValueError("claims file must contain only schemaVersion and cards")
    if document["schemaVersion"] != "persona-aifeel.required-claims.v1":
        raise ValueError("unsupported required-claims schemaVersion")
    cards = document["cards"]
    if not isinstance(cards, dict):
        raise ValueError("claims.cards must be an object keyed by exact *.persona.json basename")
    unknown = sorted(set(cards) - set(card_names))
    if unknown:
        raise ValueError("claims file references cards not selected: " + ", ".join(unknown))
    missing = sorted(set(card_names) - set(cards))
    if missing:
        raise ValueError("claims file must explicitly include every selected card (use [] when none): " +
                         ", ".join(missing))
    validated = {}
    for card, claims in cards.items():
        if not isinstance(claims, list):
            raise ValueError(f"claims for {card} must be an array")
        seen = set()
        validated[card] = []
        for claim in claims:
            if not isinstance(claim, dict) or set(claim) != {"id", "text"}:
                raise ValueError(f"each claim for {card} must contain only id and text")
            claim_id, text = claim["id"], claim["text"]
            if not isinstance(claim_id, str) or not claim_id.strip() or claim_id in seen:
                raise ValueError(f"claim IDs for {card} must be non-empty and unique")
            if not isinstance(text, str) or not text.strip():
                raise ValueError(f"claim {claim_id} for {card} needs exact non-empty text")
            seen.add(claim_id)
            validated[card].append({"id": claim_id, "text": text})
    return validated


def check_claim_retention(claims, dsl, prompt, card="<card>"):
    """Verify exact claim text in parsed emitted DSL and that exact DSL was rendered."""
    rows = rows_from_dsl(dsl, card)
    rendered_dsl_present = json.dumps(dsl, ensure_ascii=False) in prompt
    results = []
    for claim in claims:
        dsl_present = any(claim["text"] in row["text"] for row in rows)
        results.append({"id": claim["id"], "text": claim["text"],
                        "in_runtime_dsl": dsl_present,
                        "in_final_rendered_prompt": dsl_present and rendered_dsl_present,
                        "status": "pass" if dsl_present and rendered_dsl_present else
                                  ("missing_from_dsl" if not dsl_present else "rendered_dsl_not_found")})
    return {"status": "pass" if rendered_dsl_present and all(
                item["status"] == "pass" for item in results) else "blocked",
            "rendered_dsl_present": rendered_dsl_present, "claims": results}


def metrics(rows, fields=None):
    report = {}
    fields = fields or (*FIELDS, "reply")
    for field in (*fields, "reply"):
        data = [r for r in rows if r["field"] == field]
        if not data:
            continue
        counts = Counter(r["shape"] for r in data)
        families = Counter(r["family"] for r in data)
        family_cards = {family: len({r["card"] for r in data if r["family"] == family})
                        for family in families}
        lengths = [len(r["text"]) for r in data]
        endings = Counter((r["text"][-1] if r["text"] else "") for r in data)
        max_count = max(counts.values())
        top = sorted(counts.items(), key=lambda x: (-x[1], x[0]))[:5]
        card_count = len({r["card"] for r in data})
        report[field] = {
            "n": len(data), "cards": card_count,
            "insufficient_cross_card_sample": card_count < 3,
            "top_shape_share": round(max_count / len(data), 4),
            "shape_diversity": round(len(counts) / len(data), 4),
            "frame_families": dict(families.most_common()),
            "family_card_coverage": family_cards,
            "first_family_share": round((families["first_then"] + families["first_then_implicit"]) / len(data), 4),
            "first_family_card_coverage": len({r["card"] for r in data
                                               if r["family"] in ("first_then", "first_then_implicit")}),
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
    if not isinstance(raw, str):
        raise ValueError("raw response is not a string")
    parsed = json.loads(raw.strip())
    if not isinstance(parsed, dict):
        raise ValueError("response is not a JSON object")
    if set(parsed) - {"reply", "mood", "effects", "command"}:
        raise ValueError("unexpected output property")
    if "command" in parsed:
        raise ValueError("command is forbidden in chat mode")
    reply = parsed["reply"]
    mood = parsed["mood"]
    if not isinstance(reply, str) or not reply.strip() or len(reply) > 4000:
        raise ValueError("reply is empty or not a string")
    if not isinstance(mood, str) or not mood.strip() or len(mood) > 8:
        raise ValueError("mood is empty, too long or not a string")
    effects = parsed.get("effects", [])
    if not isinstance(effects, list) or len(effects) > 8 or any(not isinstance(v, str) for v in effects):
        raise ValueError("effects must be an array of at most eight strings")
    return reply


def fenced_reply_for_diagnosis(raw):
    """Never contract-valid; recover only the wording inside an exact JSON fence."""
    if not isinstance(raw, str):
        return None
    match = re.fullmatch(r"```(?:json)?\s*(.*?)\s*```", raw.strip(), flags=re.I | re.S)
    if not match:
        return None
    try:
        body = json.loads(match.group(1))
    except json.JSONDecodeError:
        return None
    reply = body.get("reply") if isinstance(body, dict) else None
    return reply if isinstance(reply, str) and reply.strip() else None


def scenario_style(rows):
    return {scenario: metrics([r for r in rows if r["scenario"] == scenario])["reply"]
            for scenario in sorted({r["scenario"] for r in rows})}


def answer_metrics(path, version="after"):
    source = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(source, dict) or not isinstance(source.get("results"), list):
        raise ValueError("answers.results must be an array")
    rows, diagnostic_rows = [], []
    invalid = []
    for index, record in enumerate(source["results"]):
        if not isinstance(record, dict):
            invalid.append({"row": index, "error": "answer row is not an object"})
            continue
        if record.get("version") != version:
            continue
        if any(key not in record for key in ("stem", "scenario", "sample")):
            invalid.append({"row": index, "error": "answer row lacks stem/scenario/sample"})
            continue
        try:
            reply = parse_reply(record.get("raw", ""))
        except (json.JSONDecodeError, KeyError, TypeError, ValueError) as error:
            invalid.append({"row": index, "card": record.get("stem"),
                            "scenario": record.get("scenario"), "error": str(error),
                            "raw_excerpt": str(record.get("raw", ""))[:200]})
            recovered = fenced_reply_for_diagnosis(record.get("raw"))
            if recovered is not None:
                diagnostic_rows.append({"card": record["stem"], "scenario": record["scenario"],
                                        "field": "reply", "index": index, "text": recovered,
                                        "shape": shape(recovered), "family": frame_family(recovered)})
            continue
        rows.append({"card": record["stem"], "scenario": record["scenario"],
                     "sample": record["sample"], "field": "reply", "index": index,
                     "text": reply, "shape": shape(reply), "family": frame_family(reply)})
    return {"source_sha256": digest(path), "model": source.get("model"), "version": version,
            "status": "scored" if rows else "no_valid_replies",
            "valid": len(rows), "invalid": invalid, "by_scenario": scenario_style(rows),
            "diagnostic_only": {"n": len(diagnostic_rows), "by_scenario": scenario_style(diagnostic_rows)}}


def relation_report(definitions, links_path):
    """Evidence ceilings, not a semantic verifier or a claim of no relationship."""
    links_doc = json.loads(Path(links_path).read_text(encoding="utf-8"))
    links = defaultdict(list)
    for link in links_doc.get("links", []):
        links[link.get("heroId")].append(link)
    cards = []
    for path in sorted(Path(definitions).glob("*.definition.json")):
        obj = json.loads(path.read_text(encoding="utf-8"))
        validate_definition_fields(obj, path)
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
    matches = re.findall(r"^\[PERSONA_DSL_OK\][^\r\n]*\bfallback=(True|False)\b", log, flags=re.M)
    return len(matches) == 1 and matches[0] == "False" and "WARN persona.tag_unregistered" not in log


def validate_card_paths(cards):
    names = [card.name.casefold() for card in cards]
    if len(set(names)) != len(names):
        raise ValueError("Duplicate card basenames would overwrite the temporary input")
    sidecars = {}
    for card in cards:
        if not card.is_file() or not card.name.endswith(".persona.json"):
            raise ValueError(f"Expected existing *.persona.json: {card}")
        sidecar = card.with_name(card.name.replace(".persona.json", ".origins.json"))
        if not sidecar.is_file():
            raise ValueError(f"Missing hero identity sidecar: {sidecar}")
        sidecars[card.name] = sidecar
    return sidecars


def chain(args):
    samples = sample_seeds(args.seed, args.samples)
    cards = [Path(p).resolve() for p in args.cards]
    sidecars = validate_card_paths(cards)
    claims_by_card = validate_required_claims(args.claims, [card.name for card in cards]) if args.claims else None
    ollama_opener = None
    ollama_info = {"server_version": None, "model_tag": None, "metadata_error": None}
    if args.model:
        ollama_opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        for endpoint, key in (("version", "server_version"), ("tags", "model_tag")):
            try:
                with ollama_opener.open(f"http://127.0.0.1:11434/api/{endpoint}", timeout=15) as response:
                    metadata = json.load(response)
                ollama_info[key] = (find_ollama_model_tag(metadata, args.model) if endpoint == "tags"
                                    else metadata.get("version"))
            except (OSError, TimeoutError, urllib.error.URLError, json.JSONDecodeError,
                    AttributeError, TypeError, ValueError) as error:
                ollama_info["metadata_error"] = f"{endpoint}: {type(error).__name__}: {error}"
        ollama_info["metadata_status"] = (
            "complete" if ollama_info["server_version"] and ollama_info["model_tag"] and
            ollama_info["model_tag"].get("digest") else "incomplete")
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
            sidecar = sidecars[card.name]
            shutil.copy2(sidecar, source / sidecar.name)
        run(["powershell", "-NoProfile", "-File", materializer, "-CharactersDir", source,
             "-RegistryPath", registry, "-OutDir", defs])
        definitions = sorted(defs.glob("*.definition.json"))
        if len(definitions) != len(cards):
            raise RuntimeError("Materializer did not produce one definition per selected card")
        objects = [json.loads(path.read_text(encoding="utf-8")) for path in definitions]
        hero_ids = [obj.get("characterId") for obj in objects]
        if any(not hero for hero in hero_ids) or len(set(hero_ids)) != len(hero_ids):
            raise RuntimeError("Missing or duplicate heroId in materialized definitions")
        if {obj.get("materialization", {}).get("sourceFile") for obj in objects} != {card.name for card in cards}:
            raise RuntimeError("Materialized sourceFile does not match selected cards")
        result = {"source_hashes": {p.name: digest(p) for p in cards},
                  "sidecar_hashes": {name: digest(path) for name, path in sidecars.items()},
                  "registry_sha256": digest(registry),
                  "renderer_source_sha256": digest(REPO / "src/NpcDialoguePromptPipeline.cs"),
                  "template_source_sha256": digest(REPO / "src/Prompts/NpcPromptTemplate.cs"),
                  "renderer_exe_sha256": digest(renderer), "simulator_dll_sha256": digest(simulator),
                  "created_utc": datetime.now(timezone.utc).isoformat(),
                  "force_approved_for_offline_test": True,
                  "model": args.model, "ollama": ollama_info if args.model else None,
                  "sampling": ({"temperature": OLLAMA_OPTIONS["temperature"],
                                "top_p": OLLAMA_OPTIONS["top_p"],
                                "num_predict": OLLAMA_OPTIONS["num_predict"],
                                "samples_per_scenario": args.samples,
                                "seeds": [seed for _, seed in samples]} if args.model else None),
                  "seed": args.seed if args.model else None, "items": []}
        for definition, obj in zip(definitions, objects):
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
                item["prompts"][label] = []
                for sample_index, sample_seed in samples:
                    vars_file = root / f"variables-{label}-{sample_index}.json"
                    vars_file.write_text(json.dumps(variables, ensure_ascii=False), encoding="utf-8")
                    prompt_file = root / f"prompt-{label}-{sample_index}.txt"
                    run([renderer, "persona-prompt-render", vars_file, prompt_file])
                    prompt = prompt_file.read_text(encoding="utf-8")
                    sample_result = {"sample": sample_index, "seed": sample_seed,
                                     "sha256": digest(prompt_file), "prompt": prompt}
                    if claims_by_card is not None:
                        sample_result["claim_retention"] = check_claim_retention(
                            claims_by_card.get(obj.get("materialization", {}).get("sourceFile", ""), []),
                            dsl.read_text(encoding="utf-8"), prompt, definition.name)
                    if args.model:
                        payload = {"model": args.model, "messages": [{"role": "user", "content": prompt}],
                                   "stream": False, "options": {**OLLAMA_OPTIONS, "seed": sample_seed}}
                        req = urllib.request.Request("http://127.0.0.1:11434/api/chat",
                                                     data=json.dumps(payload).encode("utf-8"),
                                                     headers={"Content-Type": "application/json"})
                        try:
                            with ollama_opener.open(req, timeout=900) as response:
                                body = json.load(response)
                            sample_result["raw"] = body["message"]["content"]
                            sample_result["ollama_model"] = body.get("model")
                            sample_result["timing"] = {key: body[key] for key in
                                                        ("total_duration", "load_duration", "prompt_eval_count",
                                                         "eval_count") if key in body}
                        except (OSError, TimeoutError, urllib.error.URLError, json.JSONDecodeError,
                                KeyError, TypeError, ValueError) as error:
                            sample_result["error"] = ollama_error_detail(error)
                    item["prompts"][label].append(sample_result)
            result["items"].append(item)
        # Keep authoring-field diagnostics separate from the text the model actually receives.
        definition_rows = rows_from_definitions(defs)
        dsl_rows = [row for item in result["items"]
                    for row in rows_from_dsl(item["dsl"], item["card"])]
        result["definition_field_diagnostics"] = metrics(definition_rows)
        dsl_fields = sorted({row["field"] for row in dsl_rows})
        result["runtime_dsl_sections"] = dsl_fields
        result["runtime_dsl_metrics"] = metrics(dsl_rows, fields=dsl_fields)
        if claims_by_card is not None:
            result["claim_retention"] = {
                "status": "pass" if all(
                    sample["claim_retention"]["status"] == "pass"
                    for item in result["items"]
                    for samples_for_scenario in item["prompts"].values()
                    for sample in samples_for_scenario) else "blocked",
                "cards": {item["card"]: {
                    "required": len(claims_by_card.get(item["card"].replace(".definition.json", ".persona.json"), [])),
                    "samples": [{"scenario": scenario, "sample": entry["sample"],
                                 **entry["claim_retention"]}
                                for scenario, scenario_samples in item["prompts"].items()
                                for entry in scenario_samples]}
                    for item in result["items"]}}
        if args.model:
            reply_rows, diagnostic_rows, invalid = [], [], []
            for item in result["items"]:
                for label, responses in item["prompts"].items():
                    for response in responses:
                        if "error" in response:
                            invalid.append({"card": item["card"], "scenario": label,
                                            "sample": response["sample"], "seed": response["seed"],
                                            "error": response["error"]})
                            continue
                        try:
                            reply = parse_reply(response["raw"])
                        except (json.JSONDecodeError, KeyError, TypeError, ValueError) as error:
                            invalid.append({"card": item["card"], "scenario": label,
                                            "sample": response["sample"], "seed": response["seed"],
                                            "error": str(error)})
                            recovered = fenced_reply_for_diagnosis(response["raw"])
                            if recovered is not None:
                                diagnostic_rows.append({"card": item["card"], "field": "reply", "index": 0,
                                                        "text": recovered, "shape": shape(recovered),
                                                        "family": frame_family(recovered), "scenario": label,
                                                        "sample": response["sample"]})
                            continue
                        reply_rows.append({"card": item["card"], "field": "reply", "index": 0,
                                           "text": reply, "shape": shape(reply), "family": frame_family(reply),
                                           "scenario": label, "sample": response["sample"],
                                           "seed": response["seed"]})
            expected_count = len(result["items"]) * len(SCENARIOS) * args.samples
            result["reply_metrics"] = {
                "status": reply_run_status(len(reply_rows), len(invalid), expected_count),
                "expected": expected_count,
                "valid": len(reply_rows), "invalid": invalid,
                "by_scenario": scenario_style(reply_rows),
                "diagnostic_only": {"n": len(diagnostic_rows),
                                    "by_scenario": scenario_style(diagnostic_rows)},
            }
            result["unknown_fact_review"] = build_unknown_fact_review_queue(result["items"], args.samples)
        else:
            result["unknown_fact_review"] = {"status": "not_requested", "expected": 0,
                                              "queued": 0, "items": []}
        if claims_by_card is None:
            result["claim_retention"] = {"status": "not_requested"}
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
    live.add_argument("--samples", type=int, choices=range(1, 6), default=2,
                      help="repetitions per scenario (1-5; default: 2)")
    live.add_argument("--claims", type=Path,
                      help="optional persona-aifeel.required-claims.v1 manifest for final-prompt retention checks")
    parser.add_argument("--out", type=Path, help="write full machine-readable report instead of stdout")
    parser.add_argument("--quiet", action="store_true",
                        help="with --out, print only the REPORT line (no stats preamble)")
    args = parser.parse_args()
    result = (metrics(rows_from_definitions(args.definitions)) if args.command == "audit" else
              answer_metrics(args.file, args.version) if args.command == "answers" else
              relation_report(args.definitions, args.links) if args.command == "relations" else chain(args))
    payload = json.dumps(result, ensure_ascii=False, indent=2)
    if args.out and not args.quiet:
        # Stream progress so a long chain run is not silent. The machine-readable
        # report still goes to --out untouched.
        for item in result.get("items", []):
            print(f"[DSL] {item.get('card')} fallback=False hero={item.get('hero_id')}", file=sys.stderr)
            for label, samples in item.get("prompts", {}).items():
                for rendered in samples:
                    line = (f"[PROMPT] {item.get('card')} {label} sample={rendered['sample']} "
                            f"seed={rendered['seed']} sha256={rendered['sha256'][:16]}")
                    if "raw" in rendered:
                        line += f" raw={len(rendered['raw'])}B"
                    print(line, file=sys.stderr, flush=True)
        if "reply_metrics" in result:
            rm = result["reply_metrics"]
            print(f"[REPLY] status={rm['status']} valid={rm['valid']} invalid={len(rm['invalid'])} "
                  f"diagnostic_only={rm['diagnostic_only']['n']}", file=sys.stderr, flush=True)
    if args.out:
        args.out.write_text(payload + "\n", encoding="utf-8")
        print(f"REPORT={args.out} SHA256={digest(args.out)}")
    else:
        print(payload)
    if args.command == "answers" and result["status"] != "scored":
        sys.exit(2)
    if args.command == "chain" and args.model and result["reply_metrics"]["status"] != "scored":
        sys.exit(2)
    if args.command == "chain" and result.get("claim_retention", {}).get("status") == "blocked":
        sys.exit(2)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, FileNotFoundError, RuntimeError) as error:
        print(f"EVALUATION_BLOCKED: {error}", file=sys.stderr)
        sys.exit(2)
