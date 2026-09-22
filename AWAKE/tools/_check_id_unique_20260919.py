# -*- coding: utf-8 -*-
"""全库查 id 唯一性：断言 id、表达 id 是否有重复（我手写的 21 档最容易撞）。"""
import glob
import io
import os
from collections import defaultdict

import yaml

MIRR = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
aid = defaultdict(list)
eid = defaultdict(list)
docs = [p for p in sorted(glob.glob(os.path.join(MIRR, "*.yaml")))
        if not os.path.basename(p).startswith(("_", "source-"))]
for p in docs:
    d = yaml.safe_load(io.open(p, encoding="utf-8"))
    nm = os.path.basename(p)
    for a in d.get("assertions") or []:
        aid[a["id"]].append(nm)
        for e in a.get("expressions") or []:
            eid[e["id"]].append(nm)

dupa = {k: v for k, v in aid.items() if len(v) > 1}
dupe = {k: v for k, v in eid.items() if len(v) > 1}
print("档数:", len(docs), " 断言 id 总数:", len(aid), " 表达 id 总数:", len(eid))
print("重复断言 id:", len(dupa))
for k, v in list(dupa.items())[:10]:
    print("   ", k, v)
print("重复表达 id:", len(dupe))
for k, v in list(dupe.items())[:10]:
    print("   ", k, v)
