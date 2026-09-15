# -*- coding: utf-8 -*-
"""单轮 DSL 对照：同一模板/同一问题，灌两张 DSL，对比口吻。临时对照用，不落 src。"""
import sys, io, os, time
import urllib.request
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.stdin.reconfigure(encoding="utf-8"); sys.stdout.reconfigure(encoding="utf-8")
import persona_dialogue as pd

def ask(dsl_path, player_turn, model="qwen2.5:latest"):
    dsl = open(dsl_path, encoding="utf-8").read()
    identity = pd.resolve_display_name(dsl)
    tpl = pd.load_template()
    prompt = pd.build_prompt(tpl, dsl, identity, player_turn, [])
    messages = [{"role": "user", "content": prompt}]
    body = json_dumps(messages, model)
    t0 = time.time()
    req = urllib.request.Request(pd.OLLAMA, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=300) as r:
        data = json.loads(r.read().decode("utf-8"))
    content = data.get("message", {}).get("content", "")
    text, mood = pd.parse_reply(content)
    return identity, text, mood, round(time.time() - t0, 1)

import json
def json_dumps(messages, model):
    return json.dumps({"model": model, "messages": messages, "stream": False,
                       "options": {"temperature": 0.7, "num_predict": 600}},
                      ensure_ascii=False).encode("utf-8")

if __name__ == "__main__":
    rewrite, orig = sys.argv[1], sys.argv[2]
    question = "（外乡人在宴席上当面问道：）你出身低微，凭什么坐上巴旦尼亚至高王的位置？"
    print("=" * 72)
    print("同一问题 -> 分别灌 原版DSL / 改写版DSL，驱动 qwen2.5")
    print("问题：" + question)
    print("=" * 72)
    for label, path in [("原版(史学评述)", orig), ("改写(部落主语)", rewrite)]:
        identity, text, mood, dt = ask(path, question)
        print()
        print("[%s] 身份=%s  mood=%s  耗时%ss" % (label, identity, mood or "—", dt))
        print("  应答> " + (text or "（无返回）"))
        print()