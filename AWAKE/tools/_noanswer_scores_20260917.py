# -*- coding: utf-8 -*-
"""判据 E 里那几条题，究竟是靠什么分数被扯到条目上的（2026-09-17）。

起因：甲方追问「没提到、没关联，也硬扯到这个词条上面来吗」。

之前那份读数只给了 top1 的**名字**，没给**分数**；而我在回复里说
「这几条 0.53／0.57」——那两个数其实是**过匹配探针**（村民的一亩地归谁 / 帝国的一座城）的，
不是这两句的。**这是把两批数混着说了**，属于推断当事实。本脚本把它量出来。

要回答两个问题：
  ① 「这边的人怎么样」「附近有什么好东西」跟它们答出来的那个条目的余弦是多少？
  ② 这个分数**跟真问题命中的分数**是交错还是分得开？
     · 交错 ⇒ 「调门槛救不了这一档」成立（要挡住它们就得连真题一起砍）。
     · 分得开 ⇒ 那句话要撤回，改说「门槛可以救」。

拼法与编码**直接复用** `_feed_sweep_20260917.py`（import，不复制）——平行实现必须同源；
模型目录也跟 `_semantic_floor_20260917.py` **用同一份**（AF/ONNX），否则跟那张基线表不可比。

运行：
  "C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe" -u AWAKE/tools/_noanswer_scores_20260917.py
"""
import io
import json
import os
import sys

import numpy as np

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
import _feed_sweep_20260917 as sweep  # noqa: E402

OUT = os.path.join(sweep.ROOT, "tools/_noanswer_scores_20260917.json")
FLOOR = os.path.join(sweep.ROOT, "tools/_semantic_floor_20260917.json")

# 判据 E 里那三条「该空手」的题（与题集 JSON 里逐字一致）
NOANSWER = ["领主", "这边的人怎么样", "附近有什么好东西"]

# 对照组一：完全无关的话（阴性探针，与两处门禁逐字一致）
NEGATIVE = ["今天午饭吃什么", "帮我写一段排序代码", "火车时刻表在哪儿查"]


def main():
    tokenizer = sweep.Tokenizer.from_file(os.path.join(sweep.AF, "tokenizer.json"))
    tokenizer.enable_truncation(max_length=512)
    tokenizer.enable_padding(pad_id=0, pad_token="[PAD]")
    embedder = sweep.Embedder(os.path.join(sweep.AF, "model.onnx"), tokenizer)
    print("模型 = AF/ONNX，池化 = %s" % embedder.pooled)
    probe_vector = sweep.norm(embedder.encode(["测试"]))[0]
    print("阳性对照 · 自比余弦 = %.7f（须 = 1）" % float(probe_vector @ probe_vector))

    entries = json.load(io.open(sweep.PKG, encoding="utf-8"))
    if isinstance(entries, dict):
        for key in ("entries", "items", "data"):
            if key in entries:
                entries = entries[key]
                break
    ids = [e["id"] for e in entries]
    docs = [sweep.doc_text("D", e) for e in entries]
    vecs = sweep.norm(embedder.encode(docs))
    print("语料 %d 条，向量 %s" % (len(ids), vecs.shape))

    queries = NOANSWER + NEGATIVE
    qvecs = sweep.norm(embedder.encode(queries))
    sims = qvecs @ vecs.T          # (Q, N)，两边都已 L2 归一 ⇒ 点积即余弦

    report = {"model": os.path.join(sweep.AF, "model.onnx"), "passage_plan": "D",
              "queries": [], "bands": {}}
    for qi, q in enumerate(queries):
        row = sims[qi]
        order = np.argsort(-row)[:5]
        tops = [{"id": ids[i], "sim": float(row[i])} for i in order]
        kind = "该空手" if q in NOANSWER else "完全无关"
        tag = "NOANSWER" if q in NOANSWER else "NEGATIVE"
        print("\n[%s·%s] %s" % (tag, kind, q))
        for t in tops:
            print("    %.4f  %s" % (t["sim"], t["id"]))
        report["queries"].append({"query": q, "kind": kind, "top5": tops})

    # 对照：这张表是**基线条**的来源，直接读，不重算（避免和它分叉）
    floor = json.load(io.open(FLOOR, encoding="utf-8"))
    real = [c for c in floor["cases"] if c.get("target")]
    hit = sorted(c["sim"] for c in real if c["rank"] <= 3)
    allreal = sorted(c["sim"] for c in real)
    report["bands"] = {
        "real_hit3_target_sim_min": hit[0] if hit else None,
        "real_hit3_target_sim_max": hit[-1] if hit else None,
        "real_all_target_sim_min": allreal[0] if allreal else None,
        "real_all_target_sim_max": allreal[-1] if allreal else None,
    }
    print("\n── 对照基线（来自 _semantic_floor_20260917.json，真问题目标条目自己的余弦）──")
    print("    命中前 3 的那些：%.4f ~ %.4f" % (report["bands"]["real_hit3_target_sim_min"],
                                            report["bands"]["real_hit3_target_sim_max"]))
    print("    全部 %d 条：%.4f ~ %.4f" % (len(real), report["bands"]["real_all_target_sim_min"],
                                            report["bands"]["real_all_target_sim_max"]))
    print("    ⚠️ 真问题是「题目→目标条目」的余弦；上面那几条没有目标条目，"
          "所以要看的是**它们 top1 的分数落在哪个段**，而不是直接比大小。")

    with io.open(OUT, "w", encoding="utf-8") as f:
        f.write(json.dumps(report, ensure_ascii=False, indent=1))
    print("\n写到 " + OUT)


if __name__ == "__main__":
    main()
