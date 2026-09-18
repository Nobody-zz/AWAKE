# -*- coding: utf-8 -*-
"""语料缺口体检（2026-09-17）：把"合并仍拿不到的 3 条"倒过来查一遍。
问题：这 3 条到底是"检索找不到"，还是"该答的条目还没写/我选的靶子本来就不对"？
做法：读真运行包 runtime.json，把每条目标条目的正文打出来，并在全库按关键词统计覆盖。
"""
import json
import sys
import collections

sys.stdout.reconfigure(encoding="utf-8")

PACKAGE = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
OUT = r"D:\AWAKE-Dev\AWAKE\tools\_corpus_gap_check_20260917.txt"

MISSED = [
    ("这一带有好马吗？", "awake:entry:geography.villages-lamesa"),
    ("哪座城堡底下管着两个村子？", "awake:entry:geography.castles-simira-castle"),
    ("山里有不让进的地方吗？", "awake:entry:culture.tales-dawn-taboo"),
]

# 关键词覆盖统计：问句里出现、而语料里未必有的词
TERMS = ["马", "骏马", "战马", "马匹", "好马", "养马", "骑兵",
         "山", "禁忌", "不让进", "禁地", "不许",
         "村", "城堡", "管", "隶属", "辖"]

lines = []


def emit(text=""):
    print(text)
    lines.append(text)


data = json.load(open(PACKAGE, encoding="utf-8"))
entries = data["entries"]
if isinstance(entries, dict):
    items = list(entries.items())
else:
    items = [(e.get("id"), e) for e in entries]

emit("包 = %s@%s，条目 %d 条" % (data.get("packageId"), data.get("version"), len(items)))
emit()

domains = collections.Counter()
for eid, e in items:
    parts = str(eid).split(":")
    dom = parts[2].split(".")[0] if len(parts) > 2 else "(?)"
    domains[dom] += 1
emit("── 域分布 ──")
for dom, n in domains.most_common():
    emit("  %-14s %d" % (dom, n))
emit()


def blob(e):
    chunks = [str(e.get("title") or ""), str(e.get("summary") or "")]
    for k in (e.get("keywords") or []):
        chunks.append(str(k))
    for ex in (e.get("expressions") or []):
        chunks.append(str(ex.get("text") or ""))
    return " ".join(chunks)


for query, target in MISSED:
    emit("=" * 78)
    emit("问法：%s" % query)
    emit("目标：%s" % target)
    hit = None
    for eid, e in items:
        if eid == target:
            hit = e
            break
    if hit is None:
        emit("  ⚠️ 目标条目不在包里")
        emit()
        continue
    emit("  标题：%s" % hit.get("title"))
    emit("  摘要：%s" % (hit.get("summary") or "(空)"))
    kws = hit.get("keywords") or []
    emit("  关键词：%s" % ("、".join(str(k) for k in kws) if kws else "(无)"))
    for i, ex in enumerate(hit.get("expressions") or []):
        emit("  表达%d：%s" % (i + 1, str(ex.get("text") or "")[:160]))
    body = blob(hit)
    seen = [t for t in TERMS if t in body]
    emit("  ⇒ 该条目正文里出现的问句词：%s" % ("、".join(seen) if seen else "（一个都没有）"))
    emit()

emit("=" * 78)
emit("── 全库关键词覆盖（标题＋摘要＋关键词＋全部表达）──")
for t in TERMS:
    owners = [(eid, e.get("title")) for eid, e in items if t in blob(e)]
    emit("  %-6s %3d 条" % (t, len(owners)))
    if t in ("马", "骏马", "战马", "马匹", "好马", "养马"):
        for eid, title in owners[:12]:
            emit("        - %s  %s" % (title, eid))
emit()

open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("[落盘]", OUT)
