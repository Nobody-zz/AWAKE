# -*- coding: utf-8 -*-
"""真孤立条目：正文里到底出现过哪些名字池成员？区分「正文真没提」与「提了但被 DF 阈值丢掉」。只读。"""
import json, io, re, collections

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
CJK = re.compile(r"[\u4e00-\u9fff]")
GAMECODE = re.compile(r"^[A-Za-z0-9_\-\.]+$")

name_owner = {}
for e in ents:
    t = title[e["id"]].strip()
    ns = set()
    if t:
        ns.add(t.split("·")[0].strip())
    for k in e.get("keywords") or []:
        k = (k or "").strip()
        if not k or GAMECODE.match(k) or not CJK.search(k):
            continue
        ns.add(k.split("·")[0].strip())
    for n in ns:
        if 2 <= len(n) <= 10 and CJK.search(n):
            name_owner.setdefault(n, set()).add(e["id"])
dfs = {}
def df_of(n):
    if n not in dfs:
        dfs[n] = sum(1 for t in bodies.values() if n in t)
    return dfs[n]

TARGETS = ["economy.goods-salt", "economy.goods-silver", "economy.items-cow", "economy.items-mule",
           "economy.items-pack_camel", "economy.items-saddle_horse", "economy.items-sheep",
           "economy.items-steppe_war_bow", "war.military-khuzait", "war.troops-mamluk",
           "war.weapons-armor-shield-heater", "war.weapons-armor-shield-wicker",
           "war.weapons-armor-torso-gambeson", "war.weapons-head-cheekguard", "war.weapons-head-oddity"]
byid = {e["id"]: e for e in ents}
for t in TARGETS:
    eid = "awake:entry:" + t
    txt = bodies[eid]
    print("=== %-42s 《%s》 ===" % (t, title[eid]))
    hits = []
    for n in name_owner:
        if n in txt:
            # 是否被孤立条目自己拥有
            owners = name_owner[n] - {eid}
            if not owners:
                continue
            hits.append((n, df_of(n), len(owners)))
    if not hits:
        print("  正文里出现的名字池成员：无")
    for n, d, o in sorted(hits, key=lambda x: -x[1]):
        flag = "（DF>%d ⇒ 成边被丢/单列）" % DF_MAX if d > DF_MAX else ""
        print("  提到「%s」 DF=%d  归属 %d 条%s" % (n, d, o, flag))
    print("  正文字数 %d" % len(txt))
    print()
