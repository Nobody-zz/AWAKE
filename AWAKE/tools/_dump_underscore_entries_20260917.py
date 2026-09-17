# -*- coding: utf-8 -*-
"""把 4 个「含下划线却仍自命中」的条目整条挖出来：看它的 title/summary/keywords 里到底有没有这个词。"""
import io
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
pkg = json.load(io.open(PKG, encoding="utf-8"))
PFX = "awake:entry:"
by = {e["id"].replace(PFX, ""): e for e in pkg["entries"]}

WANT = ["economy.items-crossbow_c", "economy.items-heavy_round_shield",
        "economy.items-saddle_horse", "economy.items-steppe_war_bow",
        "economy.items-battania_mercenary_armor"]


def flat(v):
    if isinstance(v, dict):
        return v.get("zh-CN") or v.get("en") or json.dumps(v, ensure_ascii=False)
    return v


for w in WANT:
    e = by.get(w)
    if not e:
        print("%s 不在包里" % w)
        continue
    kws = e.get("keywords")
    kws = (kws.get("zh-CN") or []) if isinstance(kws, dict) else (kws or [])
    title = flat(e.get("title"))
    summ = flat(e.get("summary"))
    print("─" * 70)
    print("id      : %s" % w)
    print("title   : %s" % title)
    print("keywords: %s" % kws)
    print("summary : %s" % summ)
    tail = w.split(".", 1)[1].split("-", 1)[1] if "-" in w else ""
    for probe in [w, tail]:
        print("   探测串「%s」 在 title? %s / 在 summary? %s / 在 keywords? %s"
              % (probe, probe in str(title), probe in str(summ), probe in str(kws)))
