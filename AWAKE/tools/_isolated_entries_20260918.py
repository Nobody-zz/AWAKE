# -*- coding: utf-8 -*-
"""补一个判断所需的读数：**哪些条目在两种图里都没有边（孤立）**。只读。

⚠️ 口径警告：本脚本是**最宽口径** —— 任何提及边（**含枢纽边与类别词边**）都算连通。
   所以它给出的"孤立 13 条"是**下限**，其中 5 条实际是靠「城镇／村庄」这类类别词虚接的。
   修订口径（专名边 ∪ 高频专名边 ∪ 归属边）下的真孤立是 **15 条**，读数见 `_link_audit4_20260918.py`。
   —— 此注释与代码口径曾不一致（注释写"专名边"、代码用全量边），09-18 核对时改正。
"""
import json, io, collections, os

PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
SL = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-should-link\20260918\should-link.v1.json"
HIER = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-settlement-hierarchy\20260918\settlement-hierarchy.v1.json"

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
ids = [e["id"] for e in ents]
sl = json.load(io.open(SL, encoding="utf-8"))
hi = json.load(io.open(HIER, encoding="utf-8"))

# 宽口径：任何提及边（含枢纽边/类别星形边）都算连通 —— 见文件头警告
DF_MAX = sl["params"]["dfMax"]
edge_touched = set()
for e in sl["edges"]:
    edge_touched.add(e["from"]); edge_touched.add(e["to"])
# 结构边
struct = set()
for e in hi["edges"]:
    struct.add(e["from"])
    for t in e["to"]:
        struct.add(t)

touched = edge_touched | struct
iso = [i for i in ids if i not in touched]

def cls(i):
    seg = i.split(":")[-1]
    p = seg.split("-")[0]
    return ".".join(seg.split("-")[:1]) if p else seg

print("条目 %d" % len(ids))
print("被任何边碰到的 %d" % len(touched))
print("**孤立（两种图里都没边）  %d**" % len(iso))
print()
print("== 孤立条目的类别分布 ==")
c = collections.Counter(cls(i) for i in iso)
for k, v in c.most_common():
    print("  %-28s %d" % (k, v))
print()
print("== 孤立条目清单（前 60）==")
for i in iso[:60]:
    print("  " + i.split(":")[-1])
