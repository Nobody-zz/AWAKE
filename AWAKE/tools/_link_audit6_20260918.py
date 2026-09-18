# -*- coding: utf-8 -*-
"""为「单向 vs 双向」这条设计问题补读数：
   ① 互提（双向）对按类别组合分布 —— 看双向到底长在哪儿；
   ② 入度最高 / 出度最高的条目 —— 看"单向边灌进中心节点"的风险有多大；
   ③ 单向边如果**反向使用**，会让中心节点新增多少条连接。
只读。
"""
import json, io, collections

PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
SL = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-should-link\20260918\should-link.v1.json"

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
DF_MAX = sl["params"]["dfMax"]
dfs = {}
def df_of(n):
    if n not in dfs:
        dfs[n] = sum(1 for t in bodies.values() if n in t)
    return dfs[n]

def cls(eid):
    return "-".join(eid.split(":")[-1].split("-")[:1])

def kind(e):
    d = df_of(e["viaName"])
    if d <= DF_MAX:
        return "proper"
    return "proper" if e["viaName"] not in ("城堡", "城镇", "村庄") else "star"

dirs = set()
for e in sl["edges"]:
    if kind(e) == "star":
        continue
    dirs.add((e["from"], e["to"]))

mutual = set()
oneway = set()
for (a, b) in dirs:
    (mutual if (b, a) in dirs else oneway).add((a, b))

print("=== ① 互提（双向）对按类别组合 ===")
g = collections.Counter()
for (a, b) in mutual:
    ka, kb = sorted([cls(a), cls(b)])
    g[(ka, kb)] += 1
tot = 0
for k, c in g.most_common(10):
    print("  %-26s ↔ %-26s %3d" % (k[0], k[1], c))
    tot += c
print("  组合数 %d，合计 %d（双向有向边；对数为 %d）" % (len(g), tot, len(mutual) // 2))

print()
print("=== ② 度最高的条目（排除类别星形边）===")
outd = collections.Counter()
ind = collections.Counter()
for (a, b) in dirs:
    outd[a] += 1
    ind[b] += 1
print("  入度最高（谁被最多条目提到）：")
for i, c in ind.most_common(8):
    print("    %-40s 入 %3d  出 %2d  《%s》" % (i.split(":")[-1], c, outd.get(i, 0), title[i]))
print("  出度最高：")
for i, c in outd.most_common(8):
    print("    %-40s 出 %3d  入 %2d  《%s》" % (i.split(":")[-1], c, ind.get(i, 0), title[i]))

print()
print("=== ③ 单向边若反向使用，中心节点会新增多少连接 ===")
extra = collections.Counter()
for (a, b) in oneway:
    extra[b] += 1          # 反向：b 的关联里加上 a
gained = [(i, c) for i, c in extra.most_common(8)]
for i, c in gained:
    print("    %-40s 反向新增 %3d（其一侧单向）《%s》" % (i.split(":")[-1], c, title[i]))

stdout = None
