# -*- coding: utf-8 -*-
"""
AWAKE 世界书「端到端」本机模拟（C 层）

链路：
  真实世界书检索（C# 真实运行时代码，结果在 awake-sim-retrieval.json）
    -> 真实 NPC 提示词模板（逐字取自 AWAKE/src/Prompts/NpcPromptTemplate.cs 的 TemplateText）
      -> 本机 Ollama 生成 NPC 回答

特点：
  - 不启动游戏、不联网、不使用任何云端 API Key
  - 提示词模板从源码现读，保证与模组一致，不是手写近似版

用法：
  python e2e_ollama.py [检索json路径] [模型名]
  python e2e_ollama.py %TEMP%\\awake-sim-retrieval.json qwen2.5:latest

已知可用模型：qwen2.5:latest（实测通过）
已知不可用：gpt-oss:20b（r48 实测超时、无可用输出，见 AWAKE/docs/AWAKE-CURRENT.md）
"""
import json, os, re, sys, time, urllib.request

TEMP = os.environ.get("TEMP", r"C:\Users\26811\AppData\Local\Temp")
DEFAULT_DUMP = os.path.join(TEMP, "awake-sim-retrieval.json")
SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "..", "src", "Prompts", "NpcPromptTemplate.cs")
OLLAMA = "http://127.0.0.1:11434/api/chat"


def load_template():
    """从 AWAKE 源码逐字提取 NPC 提示词模板。"""
    raw = open(SRC, encoding="utf-8-sig").read()
    m = re.search(r'TemplateText\s*=\s*@"(.*?)";\s*\n\s*internal const string OutputSchemaJson',
                  raw, re.S)
    if not m:
        sys.exit("未能从源码提取 TemplateText：" + SRC)
    return m.group(1).replace('""', '"')


def call_ollama(prompt, model, timeout=300):
    body = json.dumps({
        "model": model,
        "messages": [{"role": "user", "content": prompt}],
        "stream": False,
        "options": {"temperature": 0.7, "num_predict": 600},
    }, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(OLLAMA, data=body,
                                 headers={"Content-Type": "application/json"})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=timeout) as r:
        data = json.loads(r.read().decode("utf-8"))
    return data.get("message", {}).get("content", ""), time.time() - t0


def build_prompt(tpl, identity_zh, knowledge, player_turn):
    vals = {
        "npc_identity": identity_zh,
        "persona_dsl": "# 无额外人格模板（本机模拟，未加载人设卡）",
        "retrieved_knowledge": knowledge or "（本次没有检索到任何世界书知识）",
        "npc_memory": "",
        "npc_state": "陌生",
        "dialogue_history": "（无）",
        "player_known": "玩家是路过此地的外乡人。",
        "scene": "村庄的土路上，日头偏西。",
        "opening_hint": "",
        "player_turn": player_turn,
        "npc_id": '"hero_sim_001"',
    }
    out = tpl
    for k, v in vals.items():
        out = out.replace("{{%s}}" % k, v)
    return out


ID_ZH = {
    "profile.commoner": "平民", "profile.soldier": "士兵", "profile.noble": "贵族",
    "profile.merchant": "商人", "profile.headman": "村长", "profile.notable": "乡绅",
    "profile.anonymous": "陌生人",
}
Q_ZH = {
    "收成": "今年收成怎么样？粮税重不重？",
    "劫匪": "北边路上不太平吧？",
    "领主": "你们领主是个什么样的人？",
}
# 默认挑选的混合用例：既有「门控给了知识」，也有「门控不给」——后者用来看模型会不会胡编
DEFAULT_PICKS = [
    ("profile.commoner", "收成"),  # 有知识
    ("profile.noble", "领主"),  # 有知识
    ("profile.soldier", "收成"),  # 门控不给
    ("profile.noble", "收成"),  # 门控不给
]


def main():
    dump_path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_DUMP
    model = sys.argv[2] if len(sys.argv) > 2 else "qwen2.5:latest"

    cases = json.load(open(dump_path, encoding="utf-8"))
    tpl = load_template()

    print("模型 = %s（本机 Ollama %s）" % (model, OLLAMA))
    print("检索结果 = %s" % dump_path)
    print("=" * 72)

    for ident, q in DEFAULT_PICKS:
        hit = next((c for c in cases if c["identity"] == ident and c["question"] == q), None)
        if not hit:
            continue
        knowledge = hit["text"]
        prompt = build_prompt(tpl, ID_ZH.get(ident, ident), knowledge, Q_ZH.get(q, q))
        print("\n【用例】身份=%s  提问=「%s」" % (ID_ZH.get(ident, ident), Q_ZH.get(q, q)))
        print("  门控 state=%s   检索到字数=%d" % (hit["state"], len(knowledge)))
        if knowledge:
            print("  喂给模型的知识：%s" % knowledge.replace("\n", " ")[:80])
        else:
            print("  喂给模型的知识：（空——门控没给）")
        try:
            reply, dt = call_ollama(prompt, model)
        except Exception as e:
            print("  调用失败: %r" % (e,))
            continue
        print("  耗时 %.1fs" % dt)
        print("  ---- NPC 回答原文 ----")
        print("  " + (reply or "").strip().replace("\n", "\n  ")[:1000])
        print("-" * 72)


if __name__ == "__main__":
    main()
