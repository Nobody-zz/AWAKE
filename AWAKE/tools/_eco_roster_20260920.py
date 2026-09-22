# -*- coding: utf-8 -*-
"""经济批名录刷新：读 v20 产物，按 domain/subdomain 出全量名录 + 本批 40 档清单。"""
import io
import json
import os
import collections

REPO = r"D:\AWAKE-Dev\AWAKE"
PKG = os.path.join(REPO, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v20-goods")
OUT = os.path.join(REPO, "docs/worldbook-migration/_eco_roster_20260920.json")

rt = json.load(io.open(os.path.join(PKG, "runtime.json"), encoding="utf-8"))
doc = json.load(io.open(os.path.join(PKG, "documents.json"), encoding="utf-8"))
entries = rt["entries"]

# 断言数在 documents.json
docs = doc if isinstance(doc, list) else doc.get("documents") or []

assert_by_entry = {}
for d in docs:
    did = d.get("id") or d.get("documentId") or ""
    n = 0
    for a in d.get("assertions") or []:
        n += 1
    assert_by_entry[did] = n

BATCH = []
for f in ["_eco_A1_20260920.json", "_eco_A2B_20260920.json"]:
    dd = json.load(io.open(os.path.join(REPO, "docs/worldbook-migration/projection/authoring-out", f), encoding="utf-8"))
    BATCH += [x["slug"] for x in dd["docs"]]

dom = collections.Counter()
sub = collections.Counter()
rows = []
for e in entries:
    sid = e["id"].replace("awake:entry:", "")
    d = e.get("domain") or "?"
    s = (e.get("extensions") or {}).get("subdomain") or "-"
    dom[d] += 1
    sub[(d, s)] += 1
    tzh = (e.get("title") or {}).get("zh-CN", "")
    nassert = assert_by_entry.get("doc." + sid, 0)
    rows.append({"id": sid, "domain": d, "subdomain": s, "title_zh": tzh,
                 "keywords": len(e.get("keywords") or []), "expressions": len(e.get("expressions") or []),
                 "assertions": nassert})

print("== 全量 domain 分布（共 %d 档）==" % len(entries))
for k, v in dom.most_common():
    print("  %-14s %d" % (k, v))
print()
print("== subdomain 分布 ==")
for (d, s), v in sorted(sub.items()):
    print("  %-12s / %-14s %d" % (d, s, v))
print()
print("== economy 域全部档 ==")
eco = [r for r in rows if r["domain"] == "economy"]
print("  共 %d 档" % len(eco))
for r in sorted(eco, key=lambda x: (x["subdomain"], x["id"])):
    mark = "★本批" if r["id"].replace("economy.", "") in BATCH else "     "
    print("  %s %-34s %-8s %-12s kw=%-3d expr=%-3d asrt=%d" % (
        mark, r["id"], r["subdomain"], r["title_zh"], r["keywords"], r["expressions"], r["assertions"]))
print()

batch_rows = [r for r in rows if r["id"].replace("economy.", "") in BATCH]
print("== 本批 40 档小计 ==")
print("  档 %d / 断言 %d / 表达 %d / keywords %d" % (
    len(batch_rows), sum(r["assertions"] for r in batch_rows),
    sum(r["expressions"] for r in batch_rows), sum(r["keywords"] for r in batch_rows)))

json.dump({"total": len(entries), "domain": dict(dom), "subdomain": {"%s/%s" % k: v for k, v in sub.items()},
           "economy": eco, "batch_40": batch_rows},
          io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("\n已落盘:", OUT)
