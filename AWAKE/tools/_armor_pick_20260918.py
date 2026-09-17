# -*- coding: utf-8 -*-
"""护甲批 · 代表件清单（写 L2 文案时挑引证用；只读，2026-09-18）。

读 `_armor_taxonomy_20260918.json`，每张卡按护值降序打印前若干件，
给出 entityId / 中文名 / 护值 / 文化 / 材质 —— 供人工挑 refs（生成器会断言 refs 必须在快照里、
且不跨卡重复）。
"""
import io
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "_armor_taxonomy_20260918.json")

TOP = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 12


def main():
    d = json.load(io.open(SRC, encoding="utf-8"))

    # 只列 id（一行一卡，紧凑，写文案时挑 refs 用；确保不跨卡取）
    if len(sys.argv) > 1 and sys.argv[1] == "ids":
        for c in d["cards"]:
            ids = [it["entityId"] for it in c["items"]]
            print("%s (%d): %s" % (c["key"], len(ids), ", ".join(ids)))
        return

    for c in d["cards"]:
        print("=== %-18s %-16s n=%d  护值 %s–%s ===" % (
            c["key"], c["title"], c["n"], c["armor_min"], c["armor_max"]))
        for it in c["items"][:TOP]:
            print("   %-46s %-18s 护 %-5s 属 %-16s 材 %s" % (
                it["entityId"], (it["zh"] or "")[:18], it["armor"], it["culture"], it["material"]))
        print()


main()
