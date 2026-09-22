# -*- coding: utf-8 -*-
"""暗面批（09-20）名录刷新：数一遍 v21 包本体，并和现役包对差。只读。

读 compiled/geo1-v21-dark/{runtime.json,documents.json}（未上线），列出：
  · 条目总数 / 域分布 / 子域分布；
  · 本批 11 档的断言数 / 表达数 / 关键词数 / 分域；
  · 与现役包（482 档）的差集。
产物：docs/worldbook-migration/_dark_roster_20260920.json
"""
import collections
import io
import json
import os

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v21-dark\runtime.json")
DOCS = os.path.join(WS, r"compiled\geo1-v21-dark\documents.json")
LIVE = os.path.join(REPO, r"ModuleData\Worldbook\packages\calradia\runtime.json")
A_JSON = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out\_dark_A_20260920.json")
OUT = os.path.join(REPO, r"docs\worldbook-migration\_dark_roster_20260920.json")

BATCH = [(x["slug"], x.get("domain"), x.get("subdomain"))
         for x in json.load(io.open(A_JSON, encoding="utf-8"))["docs"]]

pkg = json.load(io.open(PKG, encoding="utf-8"))
ents = pkg["entries"]
live = json.load(io.open(LIVE, encoding="utf-8"))
live_ids = {e["id"] for e in live["entries"]}

docs_json = json.load(io.open(DOCS, encoding="utf-8"))
doc_list = docs_json["documents"] if isinstance(docs_json, dict) else docs_json
assert_n = {d["id"]: len(d.get("assertions") or []) for d in doc_list}
assert_kind = collections.Counter()
for d in doc_list:
    for a in (d.get("assertions") or []):
        assert_kind[a.get("kind")] += 1


def short(e):
    return e["id"].replace("awake:entry:", "")


by_domain = collections.Counter()
by_group = collections.Counter()
for e in ents:
    s = short(e)
    dom, _, rest = s.partition(".")
    sub = rest.split("-")[0]
    by_domain[dom] += 1
    by_group["%s.%s" % (dom, sub)] += 1

print("包 = %s" % PKG)
print("条目总数 = %d（现役 %d，差 %+d）" % (len(ents), len(live_ids), len(ents) - len(live_ids)))
print()
print("== 域分布 ==")
for k, v in by_domain.most_common():
    print("   %-14s %4d" % (k, v))
print()
new_ids = {short(e) for e in ents} - {i.replace("awake:entry:", "") for i in live_ids}
print("== 本批 11 档 ==")
rows = []
for slug, dom, sub in BATCH:
    did = "doc.%s.%s" % (dom, slug)
    e = next((x for x in ents if short(x) == did.replace("doc.", "")), None)
    if e is None:
        print("   MISS", did)
        continue
    na = assert_n.get(did, 0)
    ne = len(e.get("expressions") or [])
    nk = len(e.get("keywords") or [])
    rows.append({"slug": slug, "domain": dom, "subdomain": sub,
                 "assertions": na, "expressions": ne, "keywords": nk})
    print("   %-20s %-18s 断言%2d 表达%2d 关键词%2d" % (slug, "%s/%s" % (dom, sub), na, ne, nk))
print()
print("合计：断言 %d / 表达 %d" % (sum(r["assertions"] for r in rows), sum(r["expressions"] for r in rows)))
print("断言 kind 分布：%s" % dict(assert_kind))
print("新增条目 id 数 = %d" % len(new_ids))

json.dump({"package": "geo1-v21-dark", "total": len(ents), "live_total": len(live_ids),
           "delta": len(ents) - len(live_ids), "by_domain": dict(by_domain), "by_group": dict(by_group),
           "assert_kind": dict(assert_kind), "batch": rows},
          io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("\n已落盘:", OUT)
