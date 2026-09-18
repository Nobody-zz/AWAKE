# -*- coding: utf-8 -*-
"""补两个判断所需读数：① 引用对的单向/双向比例；② 归属边与正文互引的重合度（两条独立来源互证）。只读。"""
import json, io, collections

PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
SL = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-should-link\20260918\should-link.v1.json"
HI = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-settlement-hierarchy\20260918\settlement-hierarchy.v1.json"

def walk(o):
    if isinstance(o, dict):
        if isinstance(o.get("id"), str) and o["id"].startswith("awake:entry:"):
            yield o
        for v in o.values():
            yield from walk(v)
    elif isinstance(o, list):
        for v in o:
            yield from walk(v)

ents = list(walk(json.load(io.open(PKG, encoding="utf-8"))))
title = {e["id"]: ((e.get("title") or {}).get("zh-CN") or "") for e in ents}
def body(e):
    parts = [(e.get("summary") or {}).get("zh-CN") or ""]
    for ex in e.get("expressions") or []:
        parts.append((ex.get("text") or {}).get("zh-CN") or "")
    return "\n".join(p for p in parts if p)
bodies = {e["id"]: body(e) for e in ents}
sl = json.load(io.open(SL, encoding="utf-8"))
hi = json.load(io.open(HI, encoding="utf-8"))
DF_MAX = sl["params"]["dfMax"]
dfs = {}
def df_of(n):
    if n not in dfs:
        dfs[n] = sum(1 for t in bodies.values() if n in t)
    return dfs[n]

STAR = {"城堡", "城镇", "村庄"}
def bucket(e):
    d = df_of(e["viaName"])
    if d <= DF_MAX:
        return "proper"          # 专名边
    return "star" if e["viaName"] in STAR else "hubproper"   # 类别星形 / 高频专名

cnt = collections.Counter(bucket(e) for e in sl["edges"])
print("=== 边分桶 ===")
for k in ("proper", "hubproper", "star"):
    print("  %-10s %d" % (k, cnt[k]))

def pairs_of(bucketname):
    return set(frozenset((e["from"], e["to"])) for e in sl["edges"] if bucket(e) == bucketname)

for bn in ("proper", "hubproper"):
    ps = pairs_of(bn)
    directed = set()
    for e in sl["edges"]:
        if bucket(e) == bn:
            directed.add((e["from"], e["to"]))
    oneway = sum(1 for (a, b) in directed if (b, a) not in directed)
    both = len(directed) - oneway
    print()
    print("=== %s：有向边 %d ===" % (bn, len(directed)))
    print("  单向（只有 A 提 B，B 没提 A） %d  (%.0f%%)" % (oneway, 100.0 * oneway / max(1, len(directed))))
    print("  互为提及                    %d" % both)
    print("  无向对 %d" % len(ps))

# ---- 归属边 vs 正文互引 的重合度 ----
hp = set()
for e in hi["edges"]:
    for t in (e["to"] if isinstance(e["to"], list) else [e["to"]]):
        hp.add(frozenset((e["from"], t)))
allpairs = set(frozenset((e["from"], e["to"])) for e in sl["edges"])
overlap = hp & allpairs
print()
print("=== 归属边 vs 正文互引 重合度 ===")
print("  归属边（无向对）      %d" % len(hp))
print("  被正文互引覆盖        %d  (%.0f%%)" % (len(overlap), 100.0 * len(overlap) / max(1, len(hp))))
print("  归属边有、正文没提    %d" % len(hp - allpairs))
print("  例（没提的，前 8）：")
for p in list(hp - allpairs)[:8]:
    a, b = tuple(p)
    print("    %s《%s》 ↔ %s《%s》" % (a.split(":")[-1], title[a], b.split(":")[-1], title[b]))

# ---- 15 条真孤立 ----
hitp = set()
for e in sl["edges"]:
    if bucket(e) in ("proper", "hubproper"):
        hitp.add(e["from"]); hitp.add(e["to"])
for e in hi["edges"]:
    hitp.add(e["from"])
    for t in (e["to"] if isinstance(e["to"], list) else [e["to"]]):
        hitp.add(t)
iso = [i for i in title if i not in hitp]
print()
print("=== 真孤立（专名边∪高频专名边∪归属边 都没边） %d 条 ===" % len(iso))
for i in sorted(iso, key=lambda x: x.split(":")[-1]):
    print("    %-42s 《%s》" % (i.split(":")[-1], title[i]))
