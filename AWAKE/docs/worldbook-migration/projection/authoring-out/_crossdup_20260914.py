# -*- coding: utf-8 -*-
"""列出全库跨档重复 quote_hash（cross_dup）明细：档 id、定位、引文原文。"""
import os, glob, yaml, io, collections

ROOT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
by_hash = collections.defaultdict(list)   # hash -> [(doc_id, quote)]
for f in sorted(glob.glob(os.path.join(ROOT, "*.yaml"))):
    d = yaml.safe_load(io.open(f, encoding="utf-8"))
    if not str(d.get("id", "")).startswith("doc."):
        continue
    seen = set()

    def walk(o):
        if isinstance(o, dict):
            if "quote_hash" in o:
                h = o["quote_hash"]
                if h not in seen:
                    seen.add(h)
                    by_hash[h].append((d["id"], o.get("quote", "")))
            for v in o.values():
                walk(v)
        elif isinstance(o, list):
            for v in o:
                walk(v)
    walk(d)

dups = {h: v for h, v in by_hash.items() if len({x[0] for x in v}) >= 2}
print("跨档重复引文组数:", len(dups))
for h, v in sorted(dups.items(), key=lambda kv: -len({x[0] for x in kv[1]})):
    docs = sorted({x[0] for x in v})
    print("=" * 90)
    print("hash", h[:16], "跨档数", len(docs))
    print("  档:", "、".join(docs))
    print("  引文:", (v[0][1] or "")[:120])
