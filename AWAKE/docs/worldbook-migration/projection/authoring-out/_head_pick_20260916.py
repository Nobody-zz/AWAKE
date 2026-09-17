# -*- coding: utf-8 -*-
"""单人头盔批 · 选品候选摊开（只读 _head_inventory_20260916.json）。

每文化按护甲值排序，列出高/中/低三档候选，供人工挑代表。
另标出「平民味 / 军士味 / 头领味」的命名线索（coif/cap/hat vs helmet vs lord/warlord/noble/crown）。
"""
import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
INV = os.path.join(HERE, "_head_inventory_20260916.json")

LEADER = re.compile(r"(lord|warlord|noble|crown|royal|king|prince|chieftain|battle_crown|guard)", re.I)
PLAIN = re.compile(r"(coif|cap|hat|hood|scarf|band|arming|peasant|villager|cloth)", re.I)


def tier(h):
    e = h["entityId"]
    if LEADER.search(e):
        return "头领"
    if PLAIN.search(e):
        return "平民"
    return "军士"


def main():
    inv = json.load(io.open(INV, encoding="utf-8"))
    by = {}
    for h in inv:
        by.setdefault(h["culture"] or "(none)", []).append(h)

    for cul in sorted(by, key=lambda c: -len(by[c])):
        rows = sorted(by[cul], key=lambda h: int(h["head"] or 0))
        print("\n===== %s  (%d 件) =====" % (cul, len(rows)))
        pick = rows[:5] + rows[len(rows) // 2 - 2:len(rows) // 2 + 2] + rows[-6:]
        seen = set()
        for h in pick:
            if h["entityId"] in seen:
                continue
            seen.add(h["entityId"])
            print("  %-42s | %-34s | %-12s | 头%-3s 重%-5s %-9s %s" % (
                h["entityId"], (h["en"] or "")[:34], (h["zh"] or "")[:12],
                h["head"], h["weight"], h["material"] or "-", tier(h)))
        print("  -- 头领味候选 --")
        for h in rows:
            if tier(h) == "头领":
                print("  %-42s | %-34s | %-12s | 头%-3s 重%-5s %s" % (
                    h["entityId"], (h["en"] or "")[:34], (h["zh"] or "")[:12],
                    h["head"], h["weight"], h["material"] or "-"))


if __name__ == "__main__":
    main()
