# -*- coding: utf-8 -*-
"""AWAKE 模组 AI 组合链路模拟器。

链路：真实事实/周报代码 -> 真实世界书门控 -> 真实人物 DSL ->
真实 NPC 提示词模板 -> 本机 Ollama -> JSON/门控回归检查。

这是开发探针，不是游戏运行时，也不把模型输出写回内容或源码。
"""
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import urllib.request
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SIM_PROJECT = ROOT / "tools" / "worldbook-runtime-sim" / "WorldbookRuntimeSim.csproj"
MANIFEST = ROOT / "release" / "awake-worldbook-pilot" / "manifest.json"
CARDS = ROOT / "tools" / "persona-workbench" / "characters"
MATERIALIZER = ROOT / "tools" / "persona-workbench" / "tools" / "materialize-definitions.ps1"
REGISTRY = ROOT / "ModuleData" / "Worldbook" / "persona_definitions" / "tag_registry.json"
TEMPLATE = ROOT / "src" / "Prompts" / "NpcPromptTemplate.cs"
OLLAMA = "http://127.0.0.1:11434/api/chat"
DEFAULT_MODEL = "qwen2.5:latest"


def run_sim(args):
    command = ["dotnet", "run", "--project", str(SIM_PROJECT), "-c", "Release", "--no-build", "--", *args]
    result = subprocess.run(command, cwd=str(ROOT), capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode != 0:
        raise RuntimeError("模拟器失败（%s）:\n%s\n%s" % (result.returncode, result.stdout, result.stderr))
    return result.stdout


def load_template():
    raw = TEMPLATE.read_text(encoding="utf-8-sig")
    match = re.search(r'TemplateText\s*=\s*@"(.*?)";\s*\n\s*internal const string OutputSchemaJson', raw, re.S)
    if not match:
        raise RuntimeError("无法从源码提取 NPC TemplateText: %s" % TEMPLATE)
    return match.group(1).replace('""', '"')


def build_prompt(template, persona_dsl, identity, knowledge, report_text, memory, player_turn):
    evidence = "【本周动态（来自真实 WeeklyReportService 的结构化报告）】\n%s\n\n" % (report_text or "（本周没有正式动态）")
    evidence += "【世界书检索结果（按身份门控）】\n%s" % (knowledge or "（本次没有检索到任何世界书知识）")
    values = {
        "npc_identity": identity,
        "persona_dsl": persona_dsl,
        "retrieved_knowledge": evidence,
        "npc_memory": memory,
        "npc_state": "戒备",
        "dialogue_history": "玩家：先说说最近边境的动静。",
        "player_known": "玩家是刚进入此地的外乡人。",
        "scene": "巴旦尼亚边境的一处石砌厅堂，傍晚。",
        "opening_hint": "",
        "player_turn": player_turn,
        "npc_id": '"hero_sim_caladog"',
        "dialogue_action_mode": "chat：本轮只进行普通交谈；不得输出 command。",
    }
    output = template
    for key, value in values.items():
        output = output.replace("{{%s}}" % key, value)
    return output


def call_ollama(prompt, model, timeout=300):
    body = json.dumps({
        "model": model,
        "messages": [{"role": "user", "content": prompt}],
        "stream": False,
        "options": {"temperature": 0.7, "num_predict": 600},
    }, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(OLLAMA, data=body, headers={"Content-Type": "application/json"})
    started = time.time()
    with urllib.request.urlopen(request, timeout=timeout) as response:
        payload = json.loads(response.read().decode("utf-8"))
    return payload.get("message", {}).get("content", ""), time.time() - started


def parse_json_reply(content):
    if not content:
        return None, "empty_model_output"
    text = content.strip()
    candidates = [text]
    fenced = re.search(r"```(?:json)?\s*(.*?)```", text, re.S | re.I)
    if fenced:
        candidates.append(fenced.group(1).strip())
    for candidate in candidates:
        try:
            value = json.loads(candidate)
            if isinstance(value, dict) and isinstance(value.get("reply"), str) and value["reply"].strip():
                return value, "ok"
        except (ValueError, TypeError):
            continue
    return None, "invalid_json_or_reply"


def parse_args(argv):
    model = DEFAULT_MODEL
    output = Path(tempfile.gettempdir()) / "awake-ai-chain-sim.json"
    index = 0
    while index < len(argv):
        if argv[index] == "--model" and index + 1 < len(argv):
            model = argv[index + 1]
            index += 2
        elif argv[index] == "--out" and index + 1 < len(argv):
            output = Path(argv[index + 1]).resolve()
            index += 2
        else:
            raise SystemExit("用法: python ai_chain_sim.py [--model qwen2.5:latest] [--out result.json]")
    return model, output


def main(argv):
    model, output_path = parse_args(argv)
    if not MANIFEST.exists() or not REGISTRY.exists() or not CARDS.exists() or not MATERIALIZER.exists():
        raise RuntimeError("缺少当前可用的世界书或人物卡输入")
    with tempfile.TemporaryDirectory(prefix="awake-ai-chain-") as work:
        work_path = Path(work)
        context_path = work_path / "context.json"
        retrieval_path = work_path / "retrieval.json"
        dsl_path = work_path / "caladog.dsl.txt"
        generated_defs = work_path / "definitions"
        materialized = subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(MATERIALIZER),
             "-CharactersDir", str(CARDS), "-RegistryPath", str(REGISTRY), "-OutDir", str(generated_defs)],
            cwd=str(ROOT), capture_output=True, text=True, encoding="utf-8", errors="replace")
        if materialized.returncode != 0:
            raise RuntimeError("角色卡临时物化失败:\n%s\n%s" % (materialized.stdout, materialized.stderr))
        context_stdout = run_sim(["full-context", str(context_path)])
        retrieval_stdout = run_sim([str(MANIFEST), str(retrieval_path), "领主,收成", "profile.noble,profile.soldier"])
        persona_stdout = run_sim(["persona", str(generated_defs), str(REGISTRY), "lord_5_1", str(dsl_path), "8000", "--force-approved"])
        context = json.loads(context_path.read_text(encoding="utf-8"))
        retrieval = json.loads(retrieval_path.read_text(encoding="utf-8"))
        dsl = dsl_path.read_text(encoding="utf-8")
        template = load_template()

        selected = [
            ("noble", "领主", "卡拉多格", "known_or_partial"),
            ("noble", "收成", "卡拉多格", "not_found"),
        ]
        cases = []
        in_doubt = False
        for label, question, identity, expected in selected:
            hit = next((item for item in retrieval if item.get("identity") == "profile." + label and item.get("question") == question), None)
            if hit is None:
                raise RuntimeError("找不到组合测试用例: %s/%s" % (label, question))
            gate_state = hit.get("state")
            knowledge_text = hit.get("text") or ""
            prompt = build_prompt(
                template,
                dsl,
                identity,
                knowledge_text,
                context.get("reportText", ""),
                "玩家刚才问过边境动静；这不是事实来源。",
                "你认为最近的动静会影响这里吗？",
            )
            prompt_hash = hashlib.sha256(prompt.encode("utf-8")).hexdigest()
            case = {
                "case": label + ":" + question,
                "identity": identity,
                "question": question,
                "gateState": gate_state,
                "knowledgeBytes": len(knowledge_text.encode("utf-8")),
                "expectedGate": expected,
                "promptSha256": prompt_hash,
                "promptBytes": len(prompt.encode("utf-8")),
            }
            if gate_state not in ("known", "partial") or not knowledge_text.strip():
                # 与运行时 NpcDialogueService.BuildPromptInputAsync 对齐：门控没有给出可用知识时，
                # 直接走受控回复，不把"无资料"交给模型自行发挥。
                case.update({
                    "replySource": "knowledge_direct_fallback",
                    "outputStatus": "direct",
                    "reply": "这件事我没听说过。",
                    "mood": "茫然",
                    "hasCommand": False,
                    "aiCalled": False,
                })
            else:
                try:
                    raw, elapsed = call_ollama(prompt, model)
                    parsed, parse_status = parse_json_reply(raw)
                    case.update({
                        "replySource": "ollama",
                        "aiCalled": True,
                        "elapsedSeconds": round(elapsed, 3),
                        "rawReply": raw[:4000],
                        "outputStatus": parse_status,
                    })
                    if parsed is not None:
                        case["reply"] = parsed.get("reply")
                        case["mood"] = parsed.get("mood", "")
                        case["hasCommand"] = "command" in parsed
                    else:
                        in_doubt = True
                except Exception as error:
                    case.update({"outputStatus": "in_doubt", "error": repr(error)})
                    in_doubt = True
            cases.append(case)

        gate_checks = {
            "reportV2Validated": context.get("validation", {}).get("reportV2") is True,
            "personaDslPresent": bool(dsl.strip()),
            "positiveKnowledgeReturned": any(c["case"] == "noble:领主" and c["knowledgeBytes"] > 0 and c["gateState"] in ("known", "partial") for c in cases),
            "negativeKnowledgeBlocked": any(c["case"] == "noble:收成" and c["knowledgeBytes"] == 0 and c["gateState"] == "not_found" for c in cases),
            "allRepliesUsable": all(c.get("outputStatus") in ("ok", "direct") for c in cases),
        }
        result = {
            "schemaVersion": "awake.ai.simulation-result.v1",
            "status": "in_doubt" if in_doubt else ("pass" if all(gate_checks.values()) else "fail"),
            "model": model,
            "ollamaEndpoint": OLLAMA,
            "sources": {
                "runtimeSimulator": str(SIM_PROJECT),
                "worldbookManifest": str(MANIFEST),
                "personaCandidates": str(CARDS),
                "personaMaterializer": str(MATERIALIZER),
                "promptTemplate": str(TEMPLATE),
            },
            "deterministic": {
                "contextCommand": context_stdout.splitlines()[-1:] if context_stdout else [],
                "retrievalCommand": retrieval_stdout.splitlines()[-1:] if retrieval_stdout else [],
                "personaCommand": persona_stdout.splitlines()[-1:] if persona_stdout else [],
                "reportSchema": context.get("report", {}).get("schemaVersion"),
                "reportSourceFactCount": len(context.get("facts", [])),
                "personaDslBytes": len(dsl.encode("utf-8")),
                "checks": gate_checks,
            },
            "cases": cases,
            "limitations": [
                "不启动 Bannerlord，不执行真实战役回调、存档或 UI。",
                "周报事实使用真实 WeeklyReportService，但事实本身是固定测试 fixture。",
                "模型输出只用于观察提示词和门控效果，不会写回世界书、人物卡或源码。",
            ],
        }
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps({"status": result["status"], "out": str(output_path), "checks": gate_checks}, ensure_ascii=False))
        return 0 if result["status"] == "pass" else 2


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except Exception as error:
        print("AI-CHAIN-IN-DOUBT: %r" % (error,), file=sys.stderr)
        raise SystemExit(2)
