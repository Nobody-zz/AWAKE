# -*- coding: utf-8 -*-
"""把某个条目的全文打出来（2026-09-17）。
用途：核对"检索答出来的那一条第 1 名"到底写了什么，判断它为什么会被选中。
"""
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

PACKAGE = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"

TARGETS = [
    "awake:entry:geography.villages-geunat-nal",
    "awake:entry:war.military-sturgia",
]


def zh(value):
    if isinstance(value, dict):
        return value.get("zh-CN") or value.get("en") or str(value)
    return str(value)


data = json.load(open(PACKAGE, encoding="utf-8"))
entries = data["entries"]
items = list(entries.items()) if isinstance(entries, dict) else [(e.get("id"), e) for e in entries]

for tid in TARGETS:
    print("=" * 78)
    print("条目 id：%s" % tid)
    hit = None
    for eid, e in items:
        if eid == tid:
            hit = e
            break
    if hit is None:
        print("  ⚠️ 不在包里")
        print()
        continue
    print("  标题：%s" % zh(hit.get("title")))
    print("  摘要：%s" % zh(hit.get("summary")))
    kws = hit.get("keywords") or []
    print("  关键词：%s" % ("、".join(zh(k) for k in kws) if kws else "(无)"))
    for i, ex in enumerate(hit.get("expressions") or []):
        print("  表达%d（等级 %s）：%s" % (i + 1, ex.get("detail"), zh(ex.get("text"))))
    print()

# 顺带：全库还有哪些条目正文里出现了"纳尔"这两个字
print("=" * 78)
print("── 全库含「纳尔」的条目 ──")
count = 0
for eid, e in items:
    chunks = [zh(e.get("title")), zh(e.get("summary"))]
    chunks += [zh(k) for k in (e.get("keywords") or [])]
    chunks += [zh(x.get("text")) for x in (e.get("expressions") or [])]
    body = " ".join(chunks)
    if "纳尔" in body:
        count += 1
        if count <= 12:
            print("  %s  %s" % (zh(e.get("title")), eid))
print("  共 %d 条" % count)
