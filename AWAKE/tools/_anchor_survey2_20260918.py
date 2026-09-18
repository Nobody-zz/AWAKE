# -*- coding: utf-8 -*-
"""重扫：运行包里真正落下来的锚点是 extensions.entityRefs（不是顶层 entity_ids）。另扫 relatedDomains。只读。"""
import json, io, collections

PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
OUT = r"D:\AWAKE-Dev\AWAKE\tools\_anchor_survey_20260918.txt"

def walk(o):
    if isinstance(o, dict):
        if "id" in o and isinstance(o.get("id"), str) and o["id"].startswith("awake:entry:"):
            yield o
        for v in o.values():
            yield from walk(v)
    elif isinstance(o, list):
        for v in o:
            yield from walk(v)

ents = list(walk(json.load(io.open(PKG, encoding="utf-8"))))
w = []
def p(s=""):
    w.append(s); print(s)

def refs(e):
    return ((e.get("extensions") or {}).get("entityRefs")) or []

def rel(e):
    return ((e.get("extensions") or {}).get("relatedDomains")) or []

p("entries = %d" % len(ents))
has = [e for e in ents if refs(e)]
p("有 entityRefs = %d / %d (%.0f%%)" % (len(has), len(ents), 100.0 * len(has) / len(ents)))
p("有 relatedDomains = %d / %d" % (len([e for e in ents if rel(e)]), len(ents)))
p()

kinds = collections.Counter()
for e in has:
    for a in refs(e):
        seg = a.split(".")
        kinds[seg[1] if len(seg) > 1 else "?"] += 1
p("== 锚点 kind 分布 ==")
for k, v in kinds.most_common():
    p("  %-14s %d" % (k, v))
p()

n = collections.Counter(len(refs(e)) for e in has)
p("== 每条锚点个数 ==")
for k in sorted(n):
    p("  %d 个 : %d 条" % (k, n[k]))
p()

p("== 有锚点的样例 ==")
for e in has[:8]:
    p("  %-48s %s" % (e["id"][:48], refs(e)))
p()

# 按 id 类别段
byc = collections.defaultdict(lambda: [0, 0])
for e in ents:
    key = e["id"].split(":")[-1].split("-")[0]
    byc[key][0] += 1
    if refs(e):
        byc[key][1] += 1
p("== 按 id 类别段：有锚点 / 总数 ==")
for k in sorted(byc, key=lambda x: -byc[x][0]):
    t, h = byc[k]
    p("  %-30s %3d / %3d   (%.0f%%)" % (k, h, t, 100.0 * h / t if t else 0))

# 共享锚点 = 天然边
mp = collections.defaultdict(list)
for e in ents:
    for a in refs(e):
        mp[a].append(e["id"])
multi = {k: v for k, v in mp.items() if len(v) > 1}
p()
p("== 共享同一个锚点的条目组（= 天然的『条目↔条目』边）==")
p("  不同的锚点 %d 个，其中被 2 条以上共用的 %d 个" % (len(mp), len(multi)))
for k in sorted(multi, key=lambda x: -len(multi[x]))[:12]:
    p("  %-34s %d 条: %s" % (k, len(multi[k]), ", ".join(i.split(":")[-1] for i in multi[k])))

io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(w))
print("\nwritten", OUT)
