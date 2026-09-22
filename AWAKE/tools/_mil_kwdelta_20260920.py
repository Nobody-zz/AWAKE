# -*- coding: utf-8 -*-
"""抽样看 391 档 keywords 到底怎么变的（判断是别名收紧、还是意外）。"""
import io
import json
import os

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v19-military\runtime.json")
LIVE = os.path.join(REPO, r"ModuleData\Worldbook\packages\calradia\runtime.json")

pkg = json.load(io.open(PKG, encoding="utf-8"))
live = json.load(io.open(LIVE, encoding="utf-8"))
bp = {"doc." + e["id"].replace("awake:entry:", "", 1): e for e in pkg["entries"]}
bl = {"doc." + e["id"].replace("awake:entry:", "", 1): e for e in live["entries"]}

n = 0
for d in sorted(set(bp) & set(bl)):
    a, b = bl[d], bp[d]
    if a.get("keywords") == b.get("keywords"):
        continue
    n += 1
    if n <= 8:
        print("### %s" % d)
        ka, kb = a.get("keywords") or [], b.get("keywords") or []
        print("   live kw(%d): %s" % (len(ka), ka))
        print("   v19  kw(%d): %s" % (len(kb), kb))
        print("   删掉: %s" % [x for x in ka if x not in kb])
        print("   新增: %s" % [x for x in kb if x not in ka])
    # 汇总统计
print("\n变化档数:", n)
