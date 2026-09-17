# -*- coding: utf-8 -*-
"""看三个泛词的**原始探针记录**：查询返回了什么、命中几条、state 是什么。

输入：tools/_entry_fit_off_20260917.json（probe 原始输出）＋ _entry_fit_side_20260917.json
"""
import collections
import io
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

SIDE = "tools/_entry_fit_side_20260917.json"
OUT = "tools/_entry_fit_off_20260917.json"
PFX = "awake:entry:"

side = json.load(io.open(SIDE, encoding="utf-8"))["side"]
raw = json.load(io.open(OUT, encoding="utf-8"))
res = raw["queries"] if isinstance(raw, dict) and "queries" in raw else raw
by_name = {r["name"]: r for r in res}

GENERIC = ["村庄", "城堡", "城镇"]

print("=" * 78)
print("三个泛词的**全量**探针记录（每一档都用它自己的身份去问）")
print("=" * 78)
for g in GENERIC:
    rows = []
    for name, s in side.items():
        if s["kind"] != "kw_zh" or s["text"] != g:
            continue
        r = by_name.get(name, {})
        hits = [str(h).replace(PFX, "") for h in (r.get("hits") or [])]
        rows.append((s["entry"], r.get("state"), r.get("match_mode"),
                     r.get("literal_keyword_hits"), r.get("literal_term_hits"), hits))
    n = len(rows)
    states = collections.Counter(x[1] for x in rows)
    nhits = collections.Counter(len(x[5]) for x in rows)
    top = collections.Counter(x[5][0] for x in rows if x[5])
    print("\n── 「%s」 共 %d 条条目带这个词 ──" % (g, n))
    print("   state 分布：%s" % dict(states))
    print("   返回 hit 条数分布：%s" % dict(nhits))
    print("   命中的**第一条**是谁（Top5）：")
    for e, c in top.most_common(5):
        print("       %-38s %d 次" % (e, c))
    print("   空手（一个都没命中）的：%d 次" % sum(1 for x in rows if not x[5]))
    print("   前 3 条原始记录：")
    for row in rows[:3]:
        print("       属于 %-34s state=%-9s mode=%-8s kw=%-3s term=%-3s hits(%d)=%s"
              % (row[0], row[1], row[2], row[3], row[4], len(row[5]),
                 "、".join(row[5][:4]) or "—"))
