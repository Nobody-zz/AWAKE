# -*- coding: utf-8 -*-
"""军事批（09-20）名录刷新：按 v19 包本体数一遍，并和现役包对差。只读。

读 compiled/geo1-v19-military/runtime.json（未上线），列出：
  · 条目总数 / 域分布 / 子域分布；
  · war 域子域明细（本批新增加的 25 档落在哪几个子域）；
  · 每档的断言数 / 表达数 / 关键词数。
产物：docs/worldbook-migration/_mil_roster_20260920.json
"""
import collections
import io
import json
import os

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v19-military\runtime.json")
LIVE = os.path.join(REPO, r"ModuleData\Worldbook\packages\calradia\runtime.json")
OUT = os.path.join(REPO, r"docs\worldbook-migration\_mil_roster_20260920.json")

BATCH = ["troops-banner-knight", "troops-khans-guard", "troops-ghulam", "troops-vaegir-guard",
         "troops-druzhinnik-cavalry", "troops-golden-boar", "troops-legion-of-the-betrayed",
         "troops-cataphract", "military-legion-old", "military-legion-modern", "war_history-kuyug",
         "military-empire-system", "military-battania", "military-empire-north",
         "military-empire-south", "military-empire-west", "military-nord", "military-aserai",
         "weapons-crossbow-ironbound", "weapons-ballista", "weapons-ballista-fire",
         "weapons-mangonel", "weapons-mangonel-fire", "weapons-siege-tower", "weapons-siege-ram"]

DOCS = os.path.join(WS, r"compiled\geo1-v19-military\documents.json")

pkg = json.load(io.open(PKG, encoding="utf-8"))
ents = pkg["entries"]
live = json.load(io.open(LIVE, encoding="utf-8"))
live_ids = {e["id"] for e in live["entries"]}
# 断言在 documents.json（runtime.json 只有表达）
docs_json = json.load(io.open(DOCS, encoding="utf-8"))
doc_list = docs_json["documents"] if isinstance(docs_json, dict) else docs_json
assert_n = {d["id"]: len(d.get("assertions") or []) for d in doc_list}


def short(e):
    return e["id"].replace("awake:entry:", "")


by_domain = collections.Counter()
by_group = collections.Counter()
for e in ents:
    s = short(e)
    dom, _, rest = s.partition(".")
    sub = rest.split("-")[0]          # id 形状 domain.subdomain-slug
    by_domain[dom] += 1
    by_group["%s.%s" % (dom, sub)] += 1

print("包 = %s" % PKG)
print("条目总数 = %d（现役 %d，本批新增 %d）" % (len(ents), len(live_ids), len(ents) - len(live_ids)))
print()
print("== 域分布 ==")
for k, v in by_domain.most_common():
    print("   %-14s %4d" % (k, v))
print()
base = "_".join(sorted(by_group))
print("== war 域子域 ==")
for k, v in sorted(by_group.items()):
    if k.startswith("war"):
        print("   %-22s %4d" % (k, v))

print()
print("== 本批 25 档 ==")
rows = []
for slug in BATCH:
    did = "doc.war." + slug
    e = next((x for x in ents if short(x) == did.replace("doc.", "")), None)
    if e is None:
        print("   MISS", did)
        continue
    na = assert_n.get(did, 0)
    ne = len(e.get("expressions") or [])
    nk = len(e.get("keywords") or [])
    rows.append({"slug": slug, "assertions": na, "expressions": ne, "keywords": nk})
    print("   %-34s 断言%2d 表达%2d 关键词%2d" % (slug, na, ne, nk))
print()
print("合计：断言 %d / 表达 %d" % (sum(r["assertions"] for r in rows), sum(r["expressions"] for r in rows)))

json.dump({"package": "geo1-v19-military", "total": len(ents), "live_total": len(live_ids),
           "by_domain": dict(by_domain), "by_group": dict(by_group), "batch": rows},
          io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("\n已落盘:", OUT)
