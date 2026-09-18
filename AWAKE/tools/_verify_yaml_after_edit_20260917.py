# -*- coding: utf-8 -*-
"""把两个 authoring 目录里的 YAML 全量解析一遍（改完必须过这一关），
并打印三条新概念词条的结构摘要与引文哈希自检。
"""
import glob
import io
import os
import sys
import hashlib

import yaml

sys.stdout.reconfigure(encoding="utf-8")

DIRS = ["tools/worldbook-studio/workspace/full-geo1/authoring",
        "docs/worldbook-migration/projection/authoring-out"]
NEW = ["settlement-types-village.yaml", "settlement-types-castle.yaml", "settlement-types-town.yaml"]


def sha(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest().upper()


bad = 0
for d in DIRS:
    n = 0
    for f in sorted(glob.glob(os.path.join(d, "*.yaml"))):
        n += 1
        try:
            yaml.safe_load(io.open(f, encoding="utf-8"))
        except Exception as e:
            bad += 1
            print("PARSE-FAIL %s :: %s" % (f, str(e)[:120]))
    print("=== %s 解析 %d 档，失败 %d" % (d, n, bad))

print()
print("=" * 78)
print("三条新概念词条的结构与引文哈希自检（取 workspace 那份）")
ws = DIRS[0]
for name in NEW:
    doc = yaml.safe_load(io.open(os.path.join(ws, name), encoding="utf-8"))
    print("─" * 70)
    print("id      : %s  revision=%s status=%s" % (doc["id"], doc["revision"], doc["status"]))
    print("title   : %s / %s" % (doc["title"]["zh-CN"], doc["title"]["en"]))
    print("domain  : %s / %s   entity_ids=%s" % (doc["domain"], doc["subdomain"], doc["entity_ids"]))
    print("aliases : zh=%s en=%s" % (doc["aliases"]["zh-CN"], doc["aliases"]["en"]))
    print("summary : %s" % doc["summary"]["zh-CN"])
    # 引文哈希自检
    qbad = 0
    nq = 0
    for lst in [doc.get("sources") or []] + [
        (a.get("sources") or []) for a in doc["assertions"]] + [
        (e.get("sources") or []) for a in doc["assertions"] for e in a["expressions"]]:
        for s in lst:
            nq += 1
            if sha(s["quote"]) != s["quote_hash"]:
                qbad += 1
                print("   QUOTE-HASH-MISMATCH %s" % s["locator"])
    print("       引文 %d 条，哈希不符 %d" % (nq, qbad))
    for a in doc["assertions"]:
        layers = [(e["layer"], ",".join(g["profile_id"].replace("profile.", "") for g in e["grants"]))
                  for e in a["expressions"]]
        print("   断言 %-34s kind=%-14s src=%d" % (a["id"], a["kind"], len(a["sources"])))
        print("        层：%s" % " | ".join("%s(%s)" % (l, p) for l, p in layers))
