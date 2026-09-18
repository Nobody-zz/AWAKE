# -*- coding: utf-8 -*-
"""核「宽口径 13 条孤立」与「修订口径 18 条孤立」差集的那 5 条，各靠什么连上。只读。"""
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

FOCUS = ["awake:entry:economy.items-cow", "awake:entry:war.military-khuzait",
         "awake:entry:geography.settlement-types-castle",
         "awake:entry:geography.settlement-types-town",
         "awake:entry:geography.settlement-types-village"]
for f in FOCUS:
    print("=== %s 《%s》 ===" % (f.split(":")[-1], title[f]))
    es = [e for e in sl["edges"] if e["from"] == f or e["to"] == f]
    if not es:
        print("  无任何提及边")
    for e in es:
        d = "出" if e["from"] == f else "入"
        other = e["to"].split(":")[-1] if e["from"] == f else e["from"].split(":")[-1]
        print("  [%s] via「%s」DF=%d strength=%s  ↔ %s" % (d, e["viaName"], df_of(e["viaName"]), e["strength"], other))
        if e["evidence"]:
            print("       原文：%s" % e["evidence"][0][:80])
    print()
