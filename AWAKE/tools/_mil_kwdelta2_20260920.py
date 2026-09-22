# -*- coding: utf-8 -*-
"""归类 390 档 keywords 的删/增：是否全是内部实体 id（`^[a-z]+_[A-Za-z0-9]+$`）。"""
import io
import json
import os
import re

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v19-military\runtime.json")
LIVE = os.path.join(REPO, r"ModuleData\Worldbook\packages\calradia\runtime.json")

pkg = json.load(io.open(PKG, encoding="utf-8"))
live = json.load(io.open(LIVE, encoding="utf-8"))
bp = {"doc." + e["id"].replace("awake:entry:", "", 1): e for e in pkg["entries"]}
bl = {"doc." + e["id"].replace("awake:entry:", "", 1): e for e in live["entries"]}

IDRE = re.compile(r"^[a-z][a-z0-9]*_[A-Za-z0-9]+$")
removed, added, other_removed, other_added = [], [], [], []
ndoc = 0
for d in sorted(set(bp) & set(bl)):
    a, b = bl[d], bp[d]
    ka, kb = a.get("keywords") or [], b.get("keywords") or []
    if ka == kb:
        continue
    ndoc += 1
    for x in ka:
        if x not in kb:
            (removed if IDRE.match(x) else other_removed).append(x)
    for x in kb:
        if x not in ka:
            (added if IDRE.match(x) else other_added).append(x)

print("变化档数:", ndoc)
print("删除：内部id样式 %d 条，非id样式 %d 条" % (len(removed), len(other_removed)))
print("  非id样例:", sorted(set(other_removed))[:20])
print("新增：内部id样式 %d 条，非id样式 %d 条" % (len(added), len(other_added)))
print("  非id样例:", sorted(set(other_added))[:20])
# 还改了别的字段的档？
other_field = []
for d in sorted(set(bp) & set(bl)):
    a, b = bl[d], bp[d]
    if a == b:
        continue
    f = [k for k in ("summary", "expressions", "title") if a.get(k) != b.get(k)]
    if f:
        other_field.append((d, f))
print("\n同时改了 summary/expressions/title 的档:", len(other_field), other_field[:10])
