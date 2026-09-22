# -*- coding: utf-8 -*-
"""别名收紧 · 影响面盘点：全库 doc.* 档的 aliases 里，哪些是「本体别称」、哪些是「入口挂名」。

机械可核的三类可疑：
  A 别名含游戏实体 id 模式（village_/castle_/town_/Settlement./Culture. 等）
  B 别名 == 别档的 title（跨档挂名）
  C 别名 == 本档 title（冗余）
其余需人判。
"""
import io
import os
import re
from collections import Counter, defaultdict

import yaml

AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
PAT_ID = re.compile(r"(village_|castle_|town_|Settlement\.|Culture\.|Clan\.|Kingdom\.|NPCCharacter\.|Item\.|_EW\d|_ES\d|_B\d+_|_S\d+_|_[A-Z]{2}\d+_\d+)")

docs = {}
for f in sorted(os.listdir(AO)):
    if not f.endswith(".yaml"):
        continue
    try:
        d = yaml.safe_load(io.open(os.path.join(AO, f), encoding="utf-8"))
    except Exception:
        continue
    if not isinstance(d, dict) or not str(d.get("id", "")).startswith("doc."):
        continue
    docs[d["id"]] = (f, d)

title2docs = defaultdict(list)
for did, (f, d) in docs.items():
    t = (d.get("title") or {}).get("zh-CN", "")
    if t:
        title2docs[t].append(did)

stat = Counter()
samples = {"A": [], "B": [], "C": []}
per_doc = []

for did, (f, d) in sorted(docs.items()):
    al = d.get("aliases") or {}
    zh = al.get("zh-CN") or []
    en = al.get("en") or []
    t_zh = (d.get("title") or {}).get("zh-CN", "")
    t_en = (d.get("title") or {}).get("en", "")
    bad = {"A": [], "B": [], "C": []}
    for a in zh + en:
        if PAT_ID.search(a):
            bad["A"].append(a)
        if a in title2docs and did not in title2docs[a]:
            bad["B"].append(a)
        if a in (t_zh, t_en):
            bad["C"].append(a)
    n = sum(len(v) for v in bad.values())
    if n:
        per_doc.append((did, n, bad))
    for k, v in bad.items():
        if v:
            stat[k] += 1
            if len(samples[k]) < 12:
                samples[k].append((did, v))

print("全库 doc.* 档：%d" % len(docs))
print("别名总条数：%d（中 %d / 英 %d）" % (
    sum(len((d.get("aliases") or {}).get("zh-CN") or []) + len((d.get("aliases") or {}).get("en") or []) for _, d in docs.values()),
    sum(len((d.get("aliases") or {}).get("zh-CN") or []) for _, d in docs.values()),
    sum(len((d.get("aliases") or {}).get("en") or []) for _, d in docs.values())))
print()
print("=" * 76)
print("三类可疑（按**档数**计）")
print("=" * 76)
for k, name in (("A", "别名含游戏实体 id"), ("B", "别名 == 别档 title（跨档挂名）"), ("C", "别名 == 本档 title（冗余）")):
    print("  %s %-34s 命中 %d 档" % (k, name, stat[k]))
    for did, v in samples[k]:
        print("      %-42s %s" % (did, v))

print()
print("=" * 76)
print("按域分布（含任一类可疑的档）")
print("=" * 76)
dom = Counter(did.split(".")[1] for did, _, _ in per_doc)
for k, v in dom.most_common():
    print("  %-12s %d 档" % (k, v))

print()
print("=" * 76)
print("命中数最多的 15 档")
print("=" * 76)
for did, n, bad in sorted(per_doc, key=lambda x: -x[1])[:15]:
    print("  %-44s %d 条  A%d/B%d/C%d" % (did, n, len(bad["A"]), len(bad["B"]), len(bad["C"])))

# ---- 专项：堡/村两族的「互为入口」规模 ----
print()
print("=" * 76)
print("专项：互挂规模（堡档收村名 / 村档收堡名 / 聚落档收 id）")
print("=" * 76)
n_village_alias_in_castle = 0
n_castle_alias_in_village = 0
for did, (f, d) in docs.items():
    pre = did.split(".", 2)[2].split("-")[0]
    al = (d.get("aliases") or {}).get("zh-CN") or []
    if pre == "castles":
        n_village_alias_in_castle += len([a for a in al if a not in title2docs or did in title2docs.get(a, [])])
    if pre == "villages":
        n_castle_alias_in_village += len(al)
print("  castles 档别名条数合计 / villages 档别名条数合计：%d / %d" % (n_village_alias_in_castle, n_castle_alias_in_village))
