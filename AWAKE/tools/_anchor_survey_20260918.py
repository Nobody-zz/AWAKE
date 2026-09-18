# -*- coding: utf-8 -*-
"""盘 482 条世界书条目的「实体锚点」实况：谁有、谁没有、值长什么样、按类别怎么分布。只读。"""
import json, io, collections, sys

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

d = json.load(io.open(PKG, encoding="utf-8"))
ents = list(walk(d))

def cat(e):
    # id 第二段 = domain（482/482 一致，之前实测过）
    parts = e["id"].split(":")
    seg = parts[-1] if parts else ""
    return seg.split("-")[0] if "-" in seg else seg

w = []
def p(s=""):
    w.append(s); print(s)

p("entries = %d" % len(ents))
have = [e for e in ents if e.get("entity_ids")]
p("有 entity_ids 的 = %d / %d (%.0f%%)" % (len(have), len(ents), 100.0 * len(have) / len(ents)))
p()

# 值的形态
pref = collections.Counter()
kinds = collections.Counter()
for e in have:
    for a in e["entity_ids"]:
        pref[".".join(a.split(".")[:2])] += 1
        kinds[a.split(".")[1] if len(a.split(".")) > 1 else "?"] += 1
p("== 锚点值的前缀分布（按出现次数）==")
for k, v in pref.most_common():
    p("  %-34s %d" % (k, v))
p()

# 每条有几个锚点
n = collections.Counter(len(e["entity_ids"]) for e in have)
p("== 每条锚点个数分布 ==")
for k in sorted(n):
    p("  %d 个 : %d 条" % (k, n[k]))
p()

# 按类别（id 第二段-第三段 的类）统计有/无
byc = collections.defaultdict(lambda: [0, 0])
for e in ents:
    seg = e["id"].split(":")[-1]
    cls = "-".join(seg.split("-")[:1]) or seg
    key = seg.split("-")[0]
    byc[key][0] += 1
    if e.get("entity_ids"):
        byc[key][1] += 1
p("== 按 id 类别段：有锚点 / 总数 ==")
for k in sorted(byc, key=lambda x: -byc[x][0]):
    t, h = byc[k]
    p("  %-28s %3d / %3d   (%.0f%%)" % (k, h, t, 100.0 * h / t))
p()

# domain / subdomain 维度
dom = collections.defaultdict(lambda: [0, 0])
for e in ents:
    k = (e.get("domain") or "?")
    dom[k][0] += 1
    if e.get("entity_ids"):
        dom[k][1] += 1
p("== 按 domain：有锚点 / 总数 ==")
for k in sorted(dom, key=lambda x: -dom[x][0]):
    t, h = dom[k]
    p("  %-16s %3d / %3d" % (k, h, t))
p()

# 实例
p("== 有锚点的样例（前 6 条）==")
for e in have[:6]:
    p("  %-46s %s" % (e["id"][:46], e["entity_ids"]))
p()
p("== 没有锚点的样例（每类取 1 条）==")
seen = set()
for e in ents:
    if e.get("entity_ids"):
        continue
    key = e["id"].split(":")[-1].split("-")[0]
    if key in seen:
        continue
    seen.add(key)
    p("  %-46s title=%s" % (e["id"][:46], (e.get("title") or {}).get("zh-CN")))

io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(w))
print("\nwritten", OUT)
