# -*- coding: utf-8 -*-
"""归因：让食物条追到 0.014 的，到底是"名字"，还是**我们自己写的正文**？

起因（Max 22:1x）：怀疑「锅盔」这个名字被中文模型读到食物方向。
读源后发现两件事：
  ① 这一条**不是物品条目**，是**类条目**（锚了 6 件装备）⇒ 官方本就没有"类名"，"锅盔"是官方 21 种
     词形里的共同词根，不是瞎造；
  ② 它的 summary 第一句写的是「**一只倒扣的铁锅**，宽檐把从头顶劈下来的刀顺出去」——
     这等于**在文档里主动喂炊具语义**。

所以本探针把污染源拆开量：同一个食物对手在场，分别改"名字"和"正文比喻"，看食物条被拉到多近。
"""
import io
import json
import os

import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

ROOT = r"D:/AWAKE-Dev/AWAKE"
AF = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/ONNX"
PKG = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v9/runtime.json")
OUT = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/_kettle_pollution_source.json")
INSTRUCTION = "为这个句子生成表示以用于检索相关文章："
KETTLE = "awake:entry:war.weapons-head-kettle"

SUMMARY_NOW = ("一只倒扣的铁锅，宽檐把从头顶劈下来的刀顺出去。护头二十二到四十二，看底下垫了什么而定；"
               "瓦兰迪亚与帝国的低门槛铁盔。")
SUMMARY_NO_SIMILE = ("宽檐从头顶罩下来，把劈砍顺到肩外。护头二十二到四十二，看底下垫了什么而定；"
                     "瓦兰迪亚与帝国的低门槛铁盔。")
SUMMARY_WITH_DOMAIN = ("一只倒扣的铁锅，宽檐把从头顶劈下来的刀顺出去。护头二十二到四十二，看底下垫了什么而定；"
                       "瓦兰迪亚与帝国的低门槛铁盔。属头盔护具一类。")

# 变体：(名, title, summary, kw 追加)
VARIANTS = [
    ("1 现状", "锅盔", SUMMARY_NOW, []),
    ("2 正文去掉炊具比喻", "锅盔", SUMMARY_NO_SIMILE, []),
    ("3 名字换成圆顶锅盔", "圆顶锅盔", SUMMARY_NOW, []),
    ("4 正文加领域词", "锅盔", SUMMARY_WITH_DOMAIN, []),
    ("5 名字+正文都改", "圆顶锅盔", SUMMARY_NO_SIMILE, []),
    ("6 名字=锅盔（宽檐铁盔）", "锅盔（宽檐铁盔）", SUMMARY_NOW, []),
    ("7 名字=宽檐铁盔", "宽檐铁盔", SUMMARY_NOW, []),
    ("8 名字不动·别名加限定", "锅盔", SUMMARY_NOW, ["宽檐铁盔", "铁制宽檐盔"]),
    ("9 名字=圆顶锅盔·别名加限定", "圆顶锅盔", SUMMARY_NOW, ["宽檐铁盔"]),
    # 10/11 是"不改名字、只补同类官方名"的对照 —— 若有效，说明上面变体 3 的收益其实来自词频，
    # 而不是"官方名更准"。补的都是**同类物品的官方词形**（全含「锅盔」），所以不引入新语义。
    ("10 名字不动·别名补同类官方名", "锅盔", SUMMARY_NOW,
     ["尖刺锅盔衬链甲", "铁制圆顶锅盔衬链甲", "锅盔衬链甲", "圆顶锅盔衬皮革"]),
    ("11 名字不动·别名只补锅盔", "锅盔", SUMMARY_NOW, ["锅盔", "圆顶锅盔"]),
]

YES = ["锅盔", "那顶锅盔怎么卖", "锅盔是啥做的"]

INJECT = [
    {"id": "SYNTH:food.锅盔", "title": "锅盔",
     "summary": "一种用炭火烤出来的厚面饼，外皮硬、里面耐放，行军时当干粮带着吃。"},
    {"id": "SYNTH:food.面饼", "title": "面饼",
     "summary": "小麦磨粉烤成的干粮，平民和士兵都吃，配着汤或酒下肚。"},
    {"id": "SYNTH:food.干粮", "title": "干粮",
     "summary": "行军携带的耐存放食物，多为烤制或风干的饼与肉。"},
]
FOOD_IDS = {e["id"] for e in INJECT}


def zh_title(e):
    t = e.get("title") or {}
    return t.get("zh-CN") or (list(t.values())[0] if t else "")


def doc_text(e):
    return "。".join(x for x in (zh_title(e), "、".join(e.get("keywords") or []),
                                 (e.get("summary") or {}).get("zh-CN", "")) if x)


def main():
    entries = json.load(io.open(PKG, encoding="utf-8"))["entries"] + [
        {"id": e["id"], "title": {"zh-CN": e["title"]}, "keywords": [],
         "summary": {"zh-CN": e["summary"]}} for e in INJECT]
    ki = [i for i, e in enumerate(entries) if e["id"] == KETTLE][0]
    base = entries[ki]
    print("锅盔条现状：title=%s" % zh_title(base))
    print("  现关键词(aliases)=%s" % "、".join(base.get("keywords") or [])[:80])
    print("  语料 %d 条（含 3 条合成食物条）" % len(entries))

    tok = Tokenizer.from_file(os.path.join(AF, "tokenizer.json"))
    tok.enable_truncation(max_length=512)
    tok.enable_padding(pad_id=0, pad_token="[PAD]")
    so = ort.SessionOptions()
    so.log_severity_level = 3
    sess = ort.InferenceSession(os.path.join(AF, "model.onnx"), so, providers=["CPUExecutionProvider"])

    def encode(texts):
        encs = tok.encode_batch(texts)
        ids = np.array([e.ids for e in encs], dtype=np.int64)
        mask = np.array([e.attention_mask for e in encs], dtype=np.int64)
        feed = {"input_ids": ids, "attention_mask": mask, "token_type_ids": np.zeros_like(ids)}
        return sess.run(["sentence_embedding"], feed)[0].astype(np.float32)

    D0 = encode([doc_text(e) for e in entries])
    Q = encode([INSTRUCTION + q for q in YES])
    kw = list(base.get("keywords") or [])

    print()
    print("%-22s %-10s %-10s %-10s" % ("变体", "锅盔", "怎么卖", "啥做的"))
    rows = []
    for name, title, summary, extra in VARIANTS:
        D = D0.copy()
        D[ki] = encode(["。".join(x for x in (title, "、".join(kw + extra), summary) if x)])[0]
        Dn = D / (np.linalg.norm(D, axis=1, keepdims=True) + 1e-9)
        Qn = Q / (np.linalg.norm(Q, axis=1, keepdims=True) + 1e-9)
        S = Qn @ Dn.T
        rec = {"variant": name, "title": title, "probes": []}
        line = [name]
        for i, q in enumerate(YES):
            order = np.argsort(-S[i])[:6]
            top = [(entries[int(j)]["id"], float(S[i][int(j)])) for j in order]
            helm = [t for t in top if t[0] == KETTLE][0]
            food = [(t[0], t[1]) for t in top if t[0] in FOOD_IDS]
            f = food[0] if food else (None, 0.0)
            gap = helm[1] - f[1] if f[0] else None
            rec["probes"].append({"q": q, "helm": round(helm[1], 4),
                                  "food": round(f[1], 4), "gap": round(gap, 4) if gap else None})
            line.append("%.4f/%.4f" % (helm[1], f[1]) if f[0] else "%.4f/—" % helm[1])
        print("%-22s %-10s %-10s %-10s" % tuple(line))
        rows.append(rec)

    print()
    print("（每格 = 头盔条分 / 食物条分；食物条不在前 6 名时记为 —）")
    print()
    print("=" * 78)
    print("判读：看食物条被拉到多近（差越小越危险）")
    print("=" * 78)
    for r in rows:
        gs = [p["gap"] for p in r["probes"] if p["gap"] is not None]
        worst = min(gs) if gs else None
        print("%-22s 食物条与头盔条最小差 %s  %s"
              % (r["variant"], ("%.4f" % worst) if worst is not None else "（食物条没进前 6）",
                 ""))
    print()
    print("对比：")
    base_g = min(p["gap"] for p in rows[0]["probes"] if p["gap"] is not None)
    for r in rows[1:]:
        gs = [p["gap"] for p in r["probes"] if p["gap"] is not None]
        if not gs:
            print("   %-22s 食物条已被甩出前 6 ⇒ 比现状好" % r["variant"])
            continue
        w = min(gs)
        print("   %-22s 最小差 %.4f（现状 %.4f，%+.4f）" % (r["variant"], w, base_g, w - base_g))

    json.dump({"variants": rows}, io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("\n结果 -> %s" % OUT)


if __name__ == "__main__":
    main()
