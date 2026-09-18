# -*- coding: utf-8 -*-
"""把 DF>12 的「枢纽名」逐个判一遍：到底是**类别词**（星形，信息量低）还是**高频专名**（真引用，被 DF 一刀切误伤）。只读。

判据（可核）：看这个名字的边**都指向哪些目标条目**。
  · 目标全是概念/类别词条（id 含 settlement-types-、或标题是「城堡」「村庄」这类类别名）⇒ 类别星形边
  · 目标是具体实体词条（某个湖/某座城/某片海）⇒ 高频专名，边应进引用图
"""
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
ids = [e["id"] for e in ents]
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

hub_names = sorted({e["viaName"] for e in sl["edges"] if df_of(e["viaName"]) > DF_MAX},
                   key=lambda n: -df_of(n))
print("=== DF>%d 的枢纽名逐个判（共 %d 个）===" % (DF_MAX, len(hub_names)))
star_edges, proper_edges = 0, 0
star_names, proper_names = [], []
for n in hub_names:
    es = [e for e in sl["edges"] if e["viaName"] == n]
    tg = collections.Counter(e["to"] for e in es)
    tnames = ["%s《%s》" % (t.split(":")[-1], title[t]) for t in tg]
    is_star = all("settlement-types-" in t for t in tg)
    tag = "类别星形" if is_star else "高频专名"
    if is_star:
        star_edges += len(es); star_names.append(n)
    else:
        proper_edges += len(es); proper_names.append(n)
    print("  [%s] %-10s DF=%3d  边 %3d  目标 %d 个: %s"
          % (tag, n, df_of(n), len(es), len(tg), ", ".join(tnames)[:110]))
print()
print("  类别星形：%d 个名字 / %d 条边    → %s" % (len(star_names), star_edges, "、".join(star_names)))
print("  高频专名：%d 个名字 / %d 条边" % (len(proper_names), proper_edges))

# ---- 修订口径：应进引用图的边 = 专名边(DF<=12) + 高频专名边 ----
genu = [e for e in sl["edges"] if df_of(e["viaName"]) <= DF_MAX]
proper = [e for e in sl["edges"] if df_of(e["viaName"]) > DF_MAX and e["viaName"] in proper_names]
star = [e for e in sl["edges"] if df_of(e["viaName"]) > DF_MAX and e["viaName"] in star_names]

def pairs(es):
    return set(frozenset((e["from"], e["to"])) for e in es)

def touched(es):
    s = set()
    for e in es:
        s.add(e["from"]); s.add(e["to"])
    return s

hf, ht = set(), set()
for e in hi["edges"]:
    hf.add(e["from"])
    for t in (e["to"] if isinstance(e["to"], list) else [e["to"]]):
        ht.add(t)
struct = hf | ht

print()
print("=== 修订口径 ===")
for label, es in (("专名边(DF<=12)", genu), ("+ 高频专名边", proper), ("= 应进引用图", genu + proper),
                  ("单列：类别星形边", star), ("单列：弱边(别名)", [e for e in genu if e["strength"] == "weak"])):
    print("  %-22s 边 %5d   条目对 %5d" % (label, len(es), len(pairs(es))))

print()
print("=== 孤立条目（修订口径）===")
for label, s in (("专名边", touched(genu)), ("专名边+高频专名", touched(genu + proper)),
                 ("专名边+高频专名+类别星形", touched(genu + proper + star))):
    for lbl2, s2 in (("不加归属边", s), ("加归属边", s | struct)):
        iso = [i for i in ids if i not in s2]
        print("  %-26s %-12s 孤立 %3d" % (label, lbl2, len(iso)))

final = touched(genu + proper) | struct
iso = [i for i in ids if i not in final]
print()
print("=== 孤立清单（专名边+高频专名+归属边） 共 %d 条 ===" % len(iso))
c = collections.Counter("-".join(i.split(":")[-1].split("-")[:1]) for i in iso)
print("  类别分布：%s" % dict(c.most_common()))
for i in iso:
    print("    %-42s 《%s》" % (i.split(":")[-1], title[i]))

print()
print("=== 概念词条的入边（类别星形，单列）===")
cc = collections.Counter(e["to"] for e in star)
for t, n in cc.most_common():
    print("  %-46s ← %3d 条" % (t.split(":")[-1], n))
