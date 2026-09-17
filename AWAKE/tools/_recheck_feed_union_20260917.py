# -*- coding: utf-8 -*-
"""只读复算：用 FEED 定案档（small_f32 + 拼法 D + instr）复算并集四分区，
核对 FEED §2.5 的「并集也拿不到」名单。只打印，不写文件。"""
import json, os, io, sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))

with io.open(os.path.join(HERE, "_feed_sweep_20260917.json"), "r", encoding="utf-8") as f:
    d = json.load(f)
with io.open(os.path.join(HERE, "_retrieval_cases_20260916.json"), "r", encoding="utf-8") as f:
    cases = json.load(f)

# 目标表：query -> target 短名
tgt = {}
for c in cases.get("cases", []):
    tgt[c["query"]] = c["target"].split(":")[-1]
dc = cases.get("deniedCases")
print("deniedCases type:", type(dc).__name__)
if isinstance(dc, dict):
    print("  deniedCases keys:", list(dc.keys()))
    for c in dc.get("cases", []) or []:
        print("  [denied]", json.dumps(c, ensure_ascii=False)[:220])
        if isinstance(c, dict) and "query" in c and "target" in c:
            tgt[c["query"]] = c["target"].split(":")[-1]
print("cases:", len(cases.get("cases", [])), " tgt table:", len(tgt))

SEL = "D|instr|small_f32"
raw = d["arms"]["small_f32"][SEL]
print("arm key:", SEL, "type:", type(raw).__name__)
if isinstance(raw, dict):
    print("arm sub keys:", list(raw.keys())[:12])
    rows = raw.get("rows")
    print("rows type:", type(rows).__name__, "len:",
          (len(rows) if hasattr(rows, "__len__") else "?"))
    if isinstance(rows, list) and rows:
        print("row[0]:", json.dumps(rows[0], ensure_ascii=False)[:300])
    elif isinstance(rows, dict):
        k0 = list(rows.keys())[0]
        print("row first key:", k0, "->", json.dumps(rows[k0], ensure_ascii=False)[:300])

# 逐条语义命中（hit1）+ top3
sem = {}
rows = raw.get("rows") if isinstance(raw, dict) else None
if isinstance(rows, list):
    for r in rows:
        if isinstance(r, dict):
            q = r.get("query") or r.get("q")
            if q:
                sem[q] = r
elif isinstance(rows, dict):
    sem = rows
print("sem entries:", len(sem))
if sem:
    k0 = list(sem.keys())[0]
    print("sem sample:", k0, "->", json.dumps(sem[k0], ensure_ascii=False)[:300])

lit = d["literal"]
print("lit entries:", len(lit))

# 补齐 tgt：smoke 的 MISS 行给的目标
TGT_FIX = {
    "哪座城堡底下管着两个村子？": "geography.castles-simira-castle",
}
for q, t in TGT_FIX.items():
    tgt.setdefault(q, t)

# 全集用 lit 的 26 个 key
queries = list(lit.keys())
missing = [q for q in queries if q not in tgt]
print("\nqueries:", len(queries), " 缺 target 的:", missing)

both, lonly, sonly, none = [], [], [], []
for q in queries:
    t = tgt.get(q)
    l1 = lit.get(q, {})
    s = sem.get(q, {})
    lh = 1 if (isinstance(l1, dict) and l1.get("hit1") == 1) else 0
    sh = 1 if (isinstance(s, dict) and s.get("hit1") == 1) else 0
    if lh and sh:
        both.append((q, t))
    elif lh:
        lonly.append((q, t))
    elif sh:
        sonly.append((q, t))
    else:
        none.append((q, t))

print("\n== small_f32 | D | instr 档 并集四分区 ==")
print("两臂都拿: %d   只有字面: %d   只有语义: %d   都拿不到: %d   （合计 %d）"
      % (len(both), len(lonly), len(sonly), len(none),
         len(both) + len(lonly) + len(sonly) + len(none)))
print("并集 hit1 = %d/%d" % (len(both) + len(lonly) + len(sonly), len(queries)))

print("\n-- 只有字面 (%d) --" % len(lonly))
for q, t in lonly:
    print("   ", q, "->", t)
print("-- 只有语义 (%d) --" % len(sonly))
for q, t in sonly:
    print("   ", q, "->", t)
print("-- 都拿不到 (%d) --" % len(none))
for q, t in none:
    print("   ", q, "->", t)

# 与 union 段自报的名单对照
u = d.get("union", {})
print("\n== union 段自报 ==")
print("arm:", u.get("arm"), " hit1:", u.get("hit1"), "/", u.get("total"))
print("semantic_only 名单:")
for g, q, t in u.get("semantic_only", []):
    print("   [%s] %s -> %s" % (g, q, t))
print("literal_only 名单:")
for g, q, t in u.get("literal_only", []):
    print("   [%s] %s -> %s" % (g, q, t))
