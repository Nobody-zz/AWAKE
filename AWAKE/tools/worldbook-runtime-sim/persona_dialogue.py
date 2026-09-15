# -*- coding: utf-8 -*-
"""
AWAKE persona「人物层」端到端对话（P 层 + C 层）

链路：
  真实 persona DSL（PersonaDialogueSim force-approved 生成，与线上 PersonaDslGenerator 同源）
    -> 真实 NPC 提示词模板（逐字取自 AWAKE/src/Prompts/NpcPromptTemplate.cs 的 TemplateText）
      -> 本机 Ollama 生成 NPC 回答（多轮、带对话历史）

用法：
  python persona_dialogue.py <dsl.txt> [模型名]

例：
  python persona_dialogue.py _dsl/caladog_battania.dsl.txt qwen2.5:latest

已知可用模型：qwen2.5:latest（实测通过）
"""
import json, os, re, sys, time, urllib.request

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "..", "src", "Prompts", "NpcPromptTemplate.cs")
OLLAMA = "http://127.0.0.1:11434/api/chat"
MAX_HISTORY = 8


def load_template():
    raw = open(SRC, encoding="utf-8-sig").read()
    m = re.search(r'TemplateText\s*=\s*@"(.*?)";\s*\n\s*internal const string OutputSchemaJson',
                  raw, re.S)
    if not m:
        sys.exit("未能从源码提取 TemplateText：" + SRC)
    return m.group(1).replace('""', '"')


def call_ollama(messages, model, timeout=300):
    body = json.dumps({
        "model": model,
        "messages": messages,
        "stream": False,
        "options": {"temperature": 0.7, "num_predict": 600},
    }, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(OLLAMA, data=body,
                                 headers={"Content-Type": "application/json"})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=timeout) as r:
        data = json.loads(r.read().decode("utf-8"))
    return data.get("message", {}).get("content", ""), time.time() - t0


def build_prompt(tpl, dsl, identity_zh, player_turn, history):
    hid = "\n".join("  %s" % h for h in history) if history else "（无）"
    vals = {
        "npc_identity": identity_zh,
        "persona_dsl": dsl,
        "retrieved_knowledge": "（本对话未附世界书检索；仅凭你作为该角色的认知作答，不可编造来源。）",
        "npc_memory": "",
        "npc_state": "陌生",
        "dialogue_history": hid,
        "player_known": "玩家是路过此地的外乡人。",
        "scene": "巴旦尼亚领地内一处会见场合。",
        "opening_hint": "",
        "player_turn": player_turn,
        "npc_id": '"hero_sim_persona"',
        "dialogue_action_mode": "chat：本轮只进行普通交谈；不得输出 command。",
    }
    out = tpl
    for k, v in vals.items():
        out = out.replace("{{%s}}" % k, v)
    return out


def parse_reply(content):
    """从模型输出里尽力解出 reply（容忍含代码块/前后缀）。"""
    if not content:
        return "", ""
    text = content.strip()
    try:
        obj = json.loads(text)
        return obj.get("reply", ""), obj.get("mood", "")
    except Exception:
        pass
    m = re.search(r'"reply"\s*:\s*"(.*?)"(?:\s*,\s*"mood")?', text, re.S)
    if m:
        return m.group(1), ""
    return text, ""


def resolve_display_name(dsl):
    """从 DSL 提取中文扮演名：取第一处 DATA_CN 里「X是…」的人名（identity 段的“巴旦尼亚至高王”不含“是”，会跳过）。"""
    m = re.search(r'DATA_CN="([^"，。]+?)是', dsl)
    if m:
        return m.group(1).strip()
    return "该角色"


def main():
    if len(sys.argv) < 2:
        sys.exit("用法: python persona_dialogue.py <dsl.txt> [模型名]")
    dsl_path = sys.argv[1]
    model = sys.argv[2] if len(sys.argv) > 2 else "qwen2.5:latest"

    dsl = open(dsl_path, encoding="utf-8").read()
    identity_zh = resolve_display_name(dsl)
    tpl = load_template()
    if "persona_dsl" not in tpl:
        sys.exit("模板里找不到 {{persona_dsl}} 占位符：" + SRC)

    print("模型 = %s（本机 Ollama %s）" % (model, OLLAMA))
    print("DSL  = %s（%d 字节）" % (dsl_path, len(dsl.encode("utf-8"))))
    print("身份 = %s" % identity_zh)
    print("=" * 72)

    # 会话：交替存储 <回合|玩家/角色|文本> 的纯文本历史；真正发给模型的是拼好的单轮 prompt
    history = []  # 形如 "玩家：..." / "卡拉多格：..."

    first = "（你先向这位角色开口。他会因 persona 而定夺态度。）"
    while True:
        try:
            user_line = input("\n你 > ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\n结束对话。")
            break
        if not user_line:
            continue
        if user_line.lower() in ("exit", "quit", "q"):
            print("结束对话。")
            break
        history.append("玩家：" + user_line)
        # 截断历史
        keep = history[-MAX_HISTORY:]
        prompt = build_prompt(tpl, dsl, identity_zh, user_line, keep)
        print("  （等待本地模型…）")
        try:
            reply, dt = call_ollama([{"role": "user", "content": prompt}], model)
        except Exception as e:
            print("  调用失败: %r" % (e,))
            continue
        text, mood = parse_reply(reply)
        print("  耗时 %.1fs  mood=%s" % (dt, mood or "—"))
        print("  %s > %s" % (identity_zh, text or "（模型未返回 reply）"))
        if text:
            history.append(identity_zh + "：" + text)


if __name__ == "__main__":
    main()
