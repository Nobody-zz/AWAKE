# -*- coding: utf-8 -*-
"""按 domain/subdomain 统计，并挑出经济相关档。"""
import io, json, os, collections

REPO = r"D:\AWAKE-Dev\AWAKE"
PKG = os.path.join(REPO, "tools", "worldbook-studio", "workspace", "full-geo1",
                   "compiled", "geo1-v19-military")
rt = json.load(io.open(os.path.join(PKG, "runtime.json"), "r", encoding="utf-8"))
entries = rt["entries"]

dom = collections.Counter()
sub = collections.Counter()
for e in entries:
    d = e.get("domain") or "?"
    s = (e.get("extensions") or {}).get("subdomain") or "-"
    dom[d] += 1
    sub[(d, s)] += 1

print("== domain 分布 ==")
for k, v in dom.most_common():
    print("  %-16s %d" % (k, v))
print()
print("== subdomain 分布 ==")
for (d, s), v in sorted(sub.items()):
    print("  %-14s / %-16s %d" % (d, s, v))
print()

# 经济相关关键词
ECOKW = ["goods", "trade", "market", "price", "money", "coin", "denar", "merchant",
         "workshop", "caravan", "mine", "salt", "silver", "spice", "velvet", "fur",
         "cow", "sheep", "mule", "horse", "camel", "warehouse", "tax", "gold"]
print("== 疑似经济相关档（按 slug/标题） ==")
for e in entries:
    sid = e["id"].replace("awake:entry:", "")
    tzh = (e.get("title") or {}).get("zh-CN", "")
    blob = (sid + " " + tzh + " " + " ".join(e.get("keywords") or [])).lower()
    if any(k in blob for k in ECOKW):
        print("  %-46s | %-20s | %s" % (sid, (e.get("extensions") or {}).get("subdomain"), tzh))
