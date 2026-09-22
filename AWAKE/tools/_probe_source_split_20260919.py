# -*- coding: utf-8 -*-
"""把 482 档按「用了哪些来源」切开：谁只有官方游戏快照、谁已经有编年史第二来源。

只读。用于挑「够格写流言」的聚落：判据＝同一条里同时存在两种来源（官方快照 + 编年史）。
"""
import glob
import io
import os
import re
from collections import Counter, defaultdict

import yaml

ROOT = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
SRC = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring\sources"

files = sorted(glob.glob(os.path.join(ROOT, "*.yaml")))

by_nature = Counter()
docs_by_nature_set = Counter()
chronicle_docs = defaultdict(list)
kind_by_nature = Counter()
settlement_chronicle = []

for path in files:
    try:
        doc = yaml.safe_load(io.open(path, encoding="utf-8"))
    except Exception:                                # noqa: BLE001
        continue
    if not isinstance(doc, dict):
        continue
    sids = set()
    for s in (doc.get("sources") or []):
        if isinstance(s, dict) and s.get("source_id"):
            sids.add(str(s["source_id"]))
    for a in (doc.get("assertions") or []):
        if not isinstance(a, dict):
            continue
        for s in (a.get("sources") or []):
            if isinstance(s, dict) and s.get("source_id"):
                sids.add(str(s["source_id"]))
    natures = set()
    for sid in sids:
        # source.calradia.<nature>.<rest>
        parts = sid.split(".")
        nat = parts[2] if len(parts) > 2 else "?"
        natures.add(nat)
        by_nature[nat] += 1
    docs_by_nature_set[tuple(sorted(natures))] += 1
    if "chronicle" in natures:
        chronicle_docs[doc.get("subdomain")].append(os.path.basename(path))
        if str(doc.get("domain")) == "geography":
            settlement_chronicle.append(os.path.basename(path))
    for a in (doc.get("assertions") or []):
        if isinstance(a, dict):
            kind_by_nature[str(a.get("kind"))] += 1

print("=== 全库 档 引用的来源性质 ===")
for k, v in by_nature.most_common():
    print("  %-14s %d 处" % (k, v))
print()
print("=== 每档的「来源性质」组合 ===")
for k, v in docs_by_nature_set.most_common():
    print("  %-34s %d 档" % (str(list(k)), v))
print()
print("=== 引用了编年史（chronicle）的档，按 subdomain ===")
for k, v in sorted(chronicle_docs.items(), key=lambda x: -len(x[1])):
    print("  %-24s %d 档: %s" % (k, len(v), v[:6]))
print()
print("=== 其中 domain=geography（聚落等）===", len(settlement_chronicle))
for n in settlement_chronicle[:30]:
    print("   ", n)
print()
print("=== 来源登记表里的 chronicle 来源 ===")
for f in sorted(glob.glob(os.path.join(SRC, "source-chronicle-*.yaml"))):
    d = yaml.safe_load(io.open(f, encoding="utf-8"))
    root = d.get("locator_root")
    p = os.path.join(SRC, root)
    sz = os.path.getsize(p) if os.path.exists(p) else -1
    print("  %-52s %-34s %8d B" % (d.get("source_id"), root, sz))
