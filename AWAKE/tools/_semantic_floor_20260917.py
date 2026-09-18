# -*- coding: utf-8 -*-
"""语义臂的**相似度门槛**该定在哪（2026-09-17）。

为什么必须先量这个：字面臂有「一无所获就诚实说不知道」的性质，而**向量检索永远会返回最近的那几条**。
把语义并进检索链之后，阴性对照（`今天午饭吃什么` 这类）会从「0 条候选」变成「top-N 条最近的地名」——
`tools/worldbook-runtime-smoke` 的阴性门禁（候选 ≤3）会当场变红，产品上也等于把「没听说过」换成自信的错答。
⇒ 语义臂必须有一道**余弦门槛**，低于它的候选不进池子。

本脚本量三组数的余弦分布，用来选门槛（都**只读**盘上现成的东西，不重算字面臂）：
  · 26 条题集：目标条目自己的余弦 —— 门槛不能把它切掉（切掉就是拿掉语义的收益）
  · 阴性对照 3 条 + 过匹配 2 条：**最高**余弦 —— 门槛要能把它们挡住
  · 每条查询的 top1 是谁、多少分（看一眼就明白挡的是谁）

拼法与编码**直接复用** `_feed_sweep_20260917.py`（import，不复制）—— 平行实现必须同源。

运行：
  "C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe" -u AWAKE/tools/_semantic_floor_20260917.py
"""
import io
import json
import os
import sys

import numpy as np

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
import _feed_sweep_20260917 as sweep  # noqa: E402  只为拿 doc_text / Embedder / 模型路径

CASES = os.path.join(sweep.ROOT, "tools/_retrieval_cases_20260916.json")
OUT = os.path.join(sweep.ROOT, "tools/_semantic_floor_20260917.json")

# 与门禁验台 `RetrievalProbeCases.cs` 里的两组探针**逐字一致**（改一处要两处都改）
NEGATIVE = ["今天午饭吃什么", "帮我写一段排序代码", "火车时刻表在哪儿查"]
OVERMATCH = ["村民的一亩地归谁", "帝国的一座城"]

# 门槛候选：从保守到激进各切一刀，看两边各切掉多少
FLOORS = [0.0, 0.30, 0.35, 0.40, 0.45, 0.50, 0.55, 0.60, 0.65]


def main():
    tokenizer = sweep.Tokenizer.from_file(os.path.join(sweep.AF, "tokenizer.json"))
    # 与 _feed_sweep 的 main() 保持同一套开关：没开 padding 时 encode_batch 出来的 id 长度参差，
    # 组不成张量（第一版就栽在这里）。
    tokenizer.enable_truncation(max_length=512)
    tokenizer.enable_padding(pad_id=0, pad_token="[PAD]")
    embedder = sweep.Embedder(os.path.join(sweep.AF, "model.onnx"), tokenizer)
    print("模型 = small_f32（AF 盘上那份），池化 = %s" % embedder.pooled)
    probe_vector = sweep.norm(embedder.encode(["测试"]))[0]
    print("阳性对照 · 自比余弦 = %.7f（须 = 1）" % float(probe_vector @ probe_vector))

    entries = json.load(io.open(sweep.PKG, encoding="utf-8"))
    if isinstance(entries, dict):
        for key in ("entries", "items", "data"):
            if key in entries:
                entries = entries[key]
                break
    ids = [e["id"] for e in entries]
    idx_of = {x: i for i, x in enumerate(ids)}   # 包内 id 是全长（awake:entry:…），别按 ':' 截
    docs = [sweep.doc_text("D", e) for e in entries]
    print("语料 = %d 条（拼法 D），中位 %d 字符" % (len(docs), int(np.median([len(x) for x in docs]))))

    corpus = sweep.norm(embedder.encode(docs))
    blank = sum(1 for x in docs if not x.strip())
    print("空文本条目 = %d（这些条目在语义侧等于没有）" % blank)

    cases = json.load(io.open(CASES, encoding="utf-8"))["cases"]

    print()
    print("── 26 条题集：目标条目的余弦 ─────────────────────────────────────")
    tgt_sims = []
    for c in cases:
        q = c["query"]
        full = c["target"]
        tgt = full.split(":")[-1]
        v = sweep.norm(embedder.encode([q]))[0]
        sims = corpus @ v
        idx = idx_of[full]
        rank = int(np.sum(sims > sims[idx])) + 1
        tgt_sims.append((c["group"], q, tgt, float(sims[idx]), rank, ids[int(np.argmax(sims))].split(":")[-1], float(sims.max())))
    tgt_sims.sort(key=lambda x: x[3])
    print("%-3s %-30s %-40s %6s %5s" % ("组", "问法", "目标", "目标余弦", "名次"))
    for g, q, tgt, sim, rank, top1, top1sim in tgt_sims:
        print("%-3s %-30s %-40s %6.3f %5d" % (g, q, tgt, sim, rank))

    print()
    print("── 阴性对照 / 过匹配探针：最高余弦 ──────────────────────────────")
    probes = []
    for q in NEGATIVE + OVERMATCH:
        v = sweep.norm(embedder.encode([q]))[0]
        sims = corpus @ v
        top = int(np.argmax(sims))
        probes.append((q, float(sims[top]), ids[top]))
        print("%-24s top1=%6.3f  %s" % (q, sims[top], ids[top]))

    print()
    print("── 门槛扫描 ─────────────────────────────────────────────────────")
    print("%6s %14s %14s" % ("门槛", "26 条还剩", "探针仍在池"))
    rows = []
    for floor in FLOORS:
        kept = sum(1 for x in tgt_sims if x[3] >= floor)
        leaked = sum(1 for x in probes if x[1] >= floor)
        rows.append({"floor": floor, "cases_kept": kept, "probes_leaked": leaked})
        print("%6.2f %11d/26 %11d/%d" % (floor, kept, leaked, len(probes)))

    best = [r for r in rows if r["probes_leaked"] == 0]
    best = max(best, key=lambda r: r["cases_kept"]) if best else None
    print()
    if best:
        print("挡住全部探针的最高门槛 = %.2f（26 条还剩 %d）" % (best["floor"], best["cases_kept"]))
        print("目标余弦最低的 3 条：")
        for x in tgt_sims[:3]:
            print("   [%s] %-30s 目标余弦 %.3f（门槛 %.2f）⇒ %s"
                  % (x[0], x[1], x[3], best["floor"], "保" if x[3] >= best["floor"] else "切"))
    else:
        print("没有任何门槛能同时挡住全部探针 —— 需要换别的办法（限条数 / 单独判定）")

    json.dump({"rows": rows,
               "cases": [{"group": g, "query": q, "target": t, "sim": s, "rank": r, "top1": t1, "top1_sim": ts}
                         for g, q, t, s, r, t1, ts in tgt_sims],
               "probes": [{"query": q, "top1_sim": s, "top1": t} for q, s, t in probes]},
              io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print()
    print("结果已写 %s" % OUT)


if __name__ == "__main__":
    main()
