# -*- coding: utf-8 -*-
"""只读复算 2：对全部 48 档（4 模型 × 6 拼法 × instr/raw）算 26 条并集四分区，
看哪些题「换任何模型/拼法/指令都拿不到」——直接检验 FEED §2.5 那句话的范围。
只打印，不写文件。"""
import json, os, io, sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))

with io.open(os.path.join(HERE, "_feed_sweep_20260917.json"), "r", encoding="utf-8") as f:
    d = json.load(f)
with io.open(os.path.join(HERE, "_retrieval_cases_20260916.json"), "r", encoding="utf-8") as f:
    cases = json.load(f)

tgt = {}
for c in cases.get("cases", []):
    tgt[c["query"]] = c["target"].split(":")[-1]
dc = cases.get("deniedCases")
if isinstance(dc, dict):
    for c in dc.get("cases", []) or []:
        if isinstance(c, dict) and "query" in c:
            tgt.setdefault(c["query"], c["target"].split(":")[-1])
tgt.setdefault("哪座城堡底下管着两个村子？", "geography.castles-simira-castle")

lit = d["literal"]
queries = list(lit.keys())

# 逐档：并集四分区
per_arm = {}
for mk, arms in d["arms"].items():
    for key, v in arms.items():
        if not isinstance(v, dict) or "rows" not in v:
            continue
        sem = {}
        for r in v["rows"]:
            if isinstance(r, dict):
                sem[r.get("query")] = r
        buckets = {"both": [], "lonly": [], "sonly": [], "none": []}
        for q in queries:
            l1 = lit.get(q, {})
            s = sem.get(q, {})
            lh = 1 if (isinstance(l1, dict) and l1.get("hit1") == 1) else 0
            sh = 1 if (isinstance(s, dict) and s.get("hit1") is True) else 0
            if lh and sh:
                buckets["both"].append(q)
            elif lh:
                buckets["lonly"].append(q)
            elif sh:
                buckets["sonly"].append(q)
            else:
                buckets["none"].append(q)
        per_arm[key] = buckets

print("档数:", len(per_arm))
union_h1 = {k: len(b["both"]) + len(b["lonly"]) + len(b["sonly"]) for k, b in per_arm.items()}
print("并集 hit1 范围: %d ~ %d" % (min(union_h1.values()), max(union_h1.values())))
lo = min(union_h1, key=union_h1.get)
hi = max(union_h1, key=union_h1.get)
print("最低档:", lo, union_h1[lo], " 最高档:", hi, union_h1[hi])

# 每条题在多少档里"并集拿到"
from collections import Counter
hitcnt = Counter()
for q in queries:
    for k, b in per_arm.items():
        if q in b["both"] or q in b["lonly"] or q in b["sonly"]:
            hitcnt[q] += 1

print("\n== 各题在 %d 档里「并集能拿到」的档数（0 = 任何档都拿不到）==" % len(per_arm))
for q in sorted(queries, key=lambda x: hitcnt[x]):
    t = tgt.get(q, "?")
    mark = "  ★任何档都拿不到" if hitcnt[q] == 0 else ""
    print("  %2d/%d  %-32s -> %s%s" % (hitcnt[q], len(per_arm), q, t, mark))

# 主档（small_f32 D|instr）的四分区
main = per_arm.get("D|instr|small_f32")
print("\n== 主档 small_f32|D|instr ==")
for k in ("both", "lonly", "sonly", "none"):
    print("  %-6s %d" % (k, len(main[k])))
print("  并集 hit1 = %d/%d" % (len(main["both"]) + len(main["lonly"]) + len(main["sonly"]), len(queries)))
print("  -- none 名单 --")
for q in main["none"]:
    print("     ", q, "->", tgt.get(q))
