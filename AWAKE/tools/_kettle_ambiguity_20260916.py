# -*- coding: utf-8 -*-
"""决定性的对照实验：把「食物义」的对手放进语料，看「锅盔」会不会被带跑。

背景（Max 2026-09-16 22:1x）：
  「锅盔这个词本身有多个意思……字段被中文模型测试读取的时候，会不受控制的或者不可避免的
    理解到食物这个方向去。现在要考虑的是，锅盔作为一个知识词条存在本地是不是合理的，
    而不是用这个明显有问题的关键词来苛求模组。」

已核实的两个事实（`_probe_helmet_names*_20260916.py`）：
  ① 官方 CNs 里**没有**恰好等于「锅盔」的条目（精确 0 条）；21 种含它的词形**全部带修饰**，
     最短的独立名是**「圆顶锅盔」**。⇒ 我们用的「锅盔」是官方名的截断。
  ② 游戏物品类型里**没有 Food**，食物在 **Goods**（23 条）。

本脚本要回答的问题：**当前测不出问题，是因为"词没问题"还是因为"库里没有对手"？**
做法：往语料副本里注入 3 条食物条目（模拟将来世界书铺到粮秣类），
      再看「锅盔」与官方名「圆顶锅盔」各指向哪里。**对手缺席时的干净不算干净。**
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
OUT = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/_kettle_ambiguity_result.json")
INSTRUCTION = "为这个句子生成表示以用于检索相关文章："

# ---- 注入的假条目（合成，不是真世界书内容；只为把"食物义"的对手放进来）----
INJECT = [
    {"id": "SYNTH:food.锅盔", "title": "锅盔",
     "summary": "一种用炭火烤出来的厚面饼，外皮硬、里面耐放，行军时当干粮带着吃。"},
    {"id": "SYNTH:food.面饼", "title": "面饼",
     "summary": "小麦磨粉烤成的干粮，平民和士兵都吃，配着汤或酒下肚。"},
    {"id": "SYNTH:food.干粮", "title": "干粮",
     "summary": "行军携带的耐存放食物，多为烤制或风干的饼与肉。"},
]

# ---- 要看的词 ----
PROBES = [
    ("现关键词", "锅盔"),
    ("官方名", "圆顶锅盔"),
    ("同族对照", "护鼻盔"),
    ("同族对照", "链甲围帽"),
    ("玩家口吻", "那顶锅盔怎么卖"),
    ("玩家口吻", "锅盔是啥做的"),
]


def zh_title(e):
    t = e.get("title") or {}
    return t.get("zh-CN") or (list(t.values())[0] if t else "")


def doc_text(e):
    return "。".join(x for x in (zh_title(e), "、".join(e.get("keywords") or []),
                                 (e.get("summary") or {}).get("zh-CN", "")) if x)


def main():
    entries = json.load(io.open(PKG, encoding="utf-8"))["entries"]
    print("基座语料 %d 条" % len(entries))

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

    def run(corpus, label):
        D = encode([doc_text(e) for e in corpus])
        Dn = D / (np.linalg.norm(D, axis=1, keepdims=True) + 1e-9)
        Q = encode([INSTRUCTION + q for _, q in PROBES])
        Qn = Q / (np.linalg.norm(Q, axis=1, keepdims=True) + 1e-9)
        S = Qn @ Dn.T
        print()
        print("=" * 78)
        print("%s（语料 %d 条）" % (label, len(corpus)))
        print("=" * 78)
        out = []
        for i, (kind, q) in enumerate(PROBES):
            order = np.argsort(-S[i])[:5]
            top = [(corpus[int(j)]["id"], round(float(S[i][int(j)]), 4)) for j in order]
            print("[%s] 『%s』" % (kind, q))
            for k, (eid, s) in enumerate(top):
                tag = "  ← 食物" if eid.startswith("SYNTH:food") else ""
                print("    %d. %.4f  %s%s" % (k + 1, s, eid.replace("awake:entry:", ""), tag))
            out.append({"kind": kind, "query": q, "top5": [{"id": a, "sim": b} for a, b in top]})
        return out

    base = run(entries, "A. 基座（无食物对手）")

    injected = list(entries) + [dict(id=e["id"], title={"zh-CN": e["title"]},
                                     summary={"zh-CN": e["summary"]}, keywords=[])
                                for e in INJECT]
    inj = run(injected, "B. 注入 3 条食物条目之后（模拟世界书铺到粮秣类）")

    print()
    print("=" * 78)
    print("判读")
    print("=" * 78)
    for a, b in zip(base, inj):
        ta = a["top5"][0]
        tb = b["top5"][0]
        flip = ("食物" in tb["id"]) and ("食物" not in ta["id"])
        print("%-10s 『%s』：基座 top1 %s(%.4f) → 注入后 top1 %s(%.4f)  %s"
              % (a["kind"], a["query"], ta["id"].replace("awake:entry:", ""), ta["sim"],
                 tb["id"].replace("awake:entry:", ""), tb["sim"],
                 "★ 被食物抢走" if flip else "守住"))
    json.dump({"base": base, "injected": inj}, io.open(OUT, "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    print("\n结果 -> %s" % OUT)


if __name__ == "__main__":
    main()
