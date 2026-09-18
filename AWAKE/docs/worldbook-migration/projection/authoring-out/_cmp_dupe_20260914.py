# -*- coding: utf-8 -*-
"""比 authoring-out 与 WS_AUTH 两份 village-*.yaml 是否逐档一致；并统计总数。"""
import os, hashlib

A = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
B = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"

def lst(d):
    return sorted(f for f in os.listdir(d) if f.startswith("village-") and f.endswith(".yaml"))

la, lb = lst(A), lst(B)
print("OUT:", len(la), " WS_AUTH:", len(lb))
sa, sb = set(la), set(lb)
print("仅 OUT:", sorted(sa - sb)[:10])
print("仅 WS_AUTH:", sorted(sb - sa)[:10])
bad = []
for f in la:
    p, q = os.path.join(A, f), os.path.join(B, f)
    if not os.path.exists(q):
        bad.append((f, "missing"))
        continue
    ha = hashlib.sha256(open(p, "rb").read()).hexdigest()
    hb = hashlib.sha256(open(q, "rb").read()).hexdigest()
    if ha != hb:
        bad.append((f, ha[:8] + "!=" + hb[:8]))
print("不一致:", len(bad))
for f, r in bad[:20]:
    print("  ", f, r)
